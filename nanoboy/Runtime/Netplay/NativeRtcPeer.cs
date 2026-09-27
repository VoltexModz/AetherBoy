using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AetherBoy.Runtime.Netplay;

// libdatachannel 0.24.5 C ABI. All native calls except callback delivery are owned by one transport worker.
internal sealed class NativeRtcPeer : IDisposable
{
    private const string ChannelName = "aetherboy-link-v1";
    private readonly TaskCompletionSource gathered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<int> incomingChannel = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<Exception?> stopped;
    private readonly ChannelWriter<byte[]> received;
    private readonly OnlineRoomDiagnostics diagnostics;
    private readonly Rtc.StateCallback stateCallback, iceCallback, gatheringCallback, channelCallback;
    private readonly Rtc.SimpleCallback openCallback, closeCallback;
    private readonly Rtc.ErrorCallback errorCallback;
    private readonly Rtc.MessageCallback messageCallback;
    private int peer = -1, channel = -1;
    private int inspectPair;
    private long nextPairCheck;
    private string lastPair = "";
    private bool loggerRegistered;

    internal NativeRtcPeer(IceServer[] servers, bool relayOnly, ChannelWriter<byte[]> received, Action<Exception?> stopped, OnlineRoomDiagnostics diagnostics)
    {
        this.received = received; this.stopped = stopped; this.diagnostics = diagnostics;
        stateCallback = (_, state, _) => Callback(() =>
        {
            diagnostics.Record("peer-state", StateName(state, false));
            Interlocked.Exchange(ref inspectPair, 1);
            if (state == 4) Fail("Native connection failed. Check the connection report; the cause is not yet determined.");
            else if (state == 5) stopped(null);
        });
        iceCallback = (_, state, _) => Callback(() =>
        {
            diagnostics.Record("ice-state", StateName(state, true));
            Interlocked.Exchange(ref inspectPair, 1);
        });
        gatheringCallback = (_, state, _) => Callback(() =>
        {
            diagnostics.Record("gathering", state switch { 0 => "new", 1 => "in-progress", 2 => "complete", _ => "unknown" });
            if (state == 2) gathered.TrySetResult();
        });
        channelCallback = (_, id, _) => Callback(() => { if (!incomingChannel.TrySetResult(id)) Fail("The peer opened more than one data channel."); });
        openCallback = (_, _) => Callback(() => { diagnostics.Record("data-channel", "open"); opened.TrySetResult(); });
        closeCallback = (_, _) => Callback(() => { diagnostics.Record("data-channel", "closed"); stopped(null); });
        errorCallback = (_, error, _) => Callback(() =>
        {
            string detail = NativeRtcLogger.Sanitize(NativeRtcLogger.CopyMessage(error));
            diagnostics.Record("data-channel-error", detail);
            Fail("The online data channel failed: " + detail);
        });
        messageCallback = (_, data, size, _) =>
        {
            try
            {
                if (size is < 1 or > 4096) { Fail("Invalid online packet size."); return; }
                byte[] packet = new byte[size]; Marshal.Copy(data, packet, 0, size);
                if (!received.TryWrite(packet)) Fail("The online receive queue is full.");
            }
            catch (Exception) { Fail("Could not receive an online packet."); }
        };
        var allocations = new List<IntPtr>();
        try
        {
            NativeRtcLogger.Register(diagnostics, servers); loggerRegistered = true;
            diagnostics.Record("native-config", "libdatachannel=0.24.5 relay-only=" + relayOnly.ToString().ToLowerInvariant());
            IntPtr array = Marshal.AllocHGlobal(IntPtr.Size * servers.Length); allocations.Add(array);
            for (int i = 0; i < servers.Length; i++)
            {
                string url = servers[i].NativeUrl();
                IntPtr text = Marshal.StringToCoTaskMemUTF8(url); allocations.Add(text);
                Marshal.WriteIntPtr(array, i * IntPtr.Size, text);
            }
            var config = new Rtc.Configuration { IceServers = array, IceServersCount = servers.Length,
                DisableAutoNegotiation = 1, TransportPolicy = relayOnly ? 1 : 0, MaxMessageSize = 4096 };
            peer = Check(Rtc.rtcCreatePeerConnection(ref config));
            Check(Rtc.rtcSetStateChangeCallback(peer, stateCallback));
            Check(Rtc.rtcSetIceStateChangeCallback(peer, iceCallback));
            Check(Rtc.rtcSetGatheringStateChangeCallback(peer, gatheringCallback));
            Check(Rtc.rtcSetDataChannelCallback(peer, channelCallback));
        }
        catch { Dispose(); throw; }
        finally
        {
            for (int i = 1; i < allocations.Count; i++) Marshal.FreeCoTaskMem(allocations[i]);
            if (allocations.Count > 0) Marshal.FreeHGlobal(allocations[0]);
        }
    }

    private void Callback(Action action)
    {
        try { action(); }
        catch (Exception) { diagnostics.Record("callback-error", "Managed callback failed; see surrounding state events."); }
    }
    private static string StateName(int state, bool ice) => ice
        ? state switch { 0 => "new", 1 => "checking", 2 => "connected", 3 => "completed", 4 => "failed", 5 => "disconnected", 6 => "closed", _ => "unknown" }
        : state switch { 0 => "new", 1 => "connecting", 2 => "connected", 3 => "disconnected", 4 => "failed", 5 => "closed", _ => "unknown" };

    private void Fail(string message) => stopped(new IOException(message));
    internal async Task<string> DescriptionAsync(bool host, string? remote, CancellationToken cancellation)
    {
        if (host)
        {
            var init = new Rtc.DataChannelInit { Protocol = Marshal.StringToCoTaskMemUTF8(ChannelName) };
            try { Bind(Check(Rtc.rtcCreateDataChannelEx(peer, ChannelName, ref init))); }
            finally { Marshal.FreeCoTaskMem(init.Protocol); }
        }
        else Check(Rtc.rtcSetRemoteDescription(peer, remote!, "offer"));
        Check(Rtc.rtcSetLocalDescription(peer, host ? "offer" : "answer"));
        diagnostics.Record("sdp-local", "gathering-started");
        await WaitWithDiagnostics(gathered.Task.WaitAsync(TimeSpan.FromSeconds(45), cancellation), cancellation).ConfigureAwait(false);
        byte[] buffer = new byte[100_001];
        int length = Check(Rtc.rtcGetLocalDescription(peer, buffer, buffer.Length));
        if (length > buffer.Length) throw new IOException("Online description exceeds its limit.");
        diagnostics.Record("sdp-local", "complete bytes=" + length);
        return Encoding.UTF8.GetString(buffer, 0, Math.Max(0, length - 1));
    }
    internal void AcceptAnswer(string sdp) => Check(Rtc.rtcSetRemoteDescription(peer, sdp, "answer"));
    internal async Task WaitForOpenAsync(bool host, CancellationToken cancellation)
    {
        if (!host)
        {
            Task<int> waitingChannel = incomingChannel.Task.WaitAsync(TimeSpan.FromSeconds(45), cancellation);
            await WaitWithDiagnostics(waitingChannel, cancellation).ConfigureAwait(false);
            Bind(await waitingChannel.ConfigureAwait(false));
        }
        await WaitWithDiagnostics(opened.Task.WaitAsync(TimeSpan.FromSeconds(45), cancellation), cancellation).ConfigureAwait(false);
        PollDiagnostics(force: true);
    }
    private async Task WaitWithDiagnostics(Task waiting, CancellationToken cancellation)
    {
        while (!waiting.IsCompleted)
        {
            PollDiagnostics();
            await Task.WhenAny(waiting, Task.Delay(100, cancellation)).ConfigureAwait(false);
        }
        PollDiagnostics(force: true);
        await waiting.ConfigureAwait(false);
    }

    // Called on the transport worker, never within native callbacks. A pair can be unavailable
    // during ICE transitions and may change later. The native API supplies full candidate strings.
    internal void PollDiagnostics(bool force = false)
    {
        if (peer < 0) return;
        bool changed = Interlocked.Exchange(ref inspectPair, 0) != 0;
        long now = Environment.TickCount64;
        if (!force && !changed && now < nextPairCheck) return;
        nextPairCheck = now + 2000;
        int required = Rtc.rtcGetSelectedCandidatePair(peer, null, 0, null, 0);
        if (required < 0) { PairResult("unavailable code=" + required); return; }
        for (int attempt = 0; attempt < 3 && required is > 0 and <= 16384; attempt++)
        {
            byte[] local = new byte[required], remote = new byte[required];
            int result = Rtc.rtcGetSelectedCandidatePair(peer, local, local.Length, remote, remote.Length);
            if (result == -4 || result > required)
            { required = Rtc.rtcGetSelectedCandidatePair(peer, null, 0, null, 0); continue; }
            if (result < 0) { PairResult("unavailable code=" + result); return; }
            string Decode(byte[] value)
            {
                int end = Array.IndexOf(value, (byte)0);
                return Encoding.UTF8.GetString(value, 0, end < 0 ? value.Length : end);
            }
            PairResult("local=" + OnlineRoomDiagnostics.CandidateType(Decode(local)) +
                " remote=" + OnlineRoomDiagnostics.CandidateType(Decode(remote)) + " endpoints=redacted");
            return;
        }
        PairResult("read-failed-or-size-limit");
    }
    private void PairResult(string value)
    { if (value != lastPair) { lastPair = value; diagnostics.Record("candidate-pair", value); } }
    private void Bind(int id)
    {
        channel = id;
        byte[] label = new byte[128], protocol = new byte[128];
        int labelLength = Check(Rtc.rtcGetDataChannelLabel(id, label, label.Length));
        int protocolLength = Check(Rtc.rtcGetDataChannelProtocol(id, protocol, protocol.Length));
        Check(Rtc.rtcGetDataChannelReliability(id, out var reliability));
        if (Encoding.UTF8.GetString(label, 0, labelLength - 1) != ChannelName ||
            Encoding.UTF8.GetString(protocol, 0, protocolLength - 1) != ChannelName || reliability.Unordered != 0 || reliability.Unreliable != 0)
            throw new IOException("The peer uses an incompatible data channel.");
        Check(Rtc.rtcSetMessageCallback(id, messageCallback));
        Check(Rtc.rtcSetOpenCallback(id, openCallback));
        Check(Rtc.rtcSetClosedCallback(id, closeCallback));
        Check(Rtc.rtcSetErrorCallback(id, errorCallback));
        if (Rtc.rtcIsOpen(id)) opened.TrySetResult();
    }
    internal bool CanSend => Check(Rtc.rtcGetBufferedAmount(channel)) == 0;
    internal void Send(byte[] packet) => Check(Rtc.rtcSendMessage(channel, packet, packet.Length));
    private static int Check(int result) => result >= 0 ? result : throw new IOException("Native WebRTC operation failed (" + result + ").");
    public void Dispose()
    {
        try
        {
            if (channel >= 0) { Rtc.rtcDeleteDataChannel(channel); channel = -1; }
            if (peer >= 0) { Rtc.rtcDeletePeerConnection(peer); peer = -1; }
            GC.KeepAlive(this);
        }
        finally
        {
            if (loggerRegistered) { NativeRtcLogger.Unregister(diagnostics); loggerRegistered = false; }
        }
    }

    private static class Rtc
    {
        private const string Library = "datachannel";
        [StructLayout(LayoutKind.Sequential)] internal struct Configuration
        {
            public IntPtr IceServers; public int IceServersCount; public IntPtr ProxyServer, BindAddress;
            public int CertificateType, TransportPolicy;
            public byte EnableIceTcp, EnableIceUdpMux, DisableAutoNegotiation, ForceMediaTransport;
            public ushort PortRangeBegin, PortRangeEnd; public int Mtu, MaxMessageSize;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct Reliability
        { public byte Unordered, Unreliable; public uint MaxPacketLifeTime, MaxRetransmits; }
        [StructLayout(LayoutKind.Sequential)] internal struct DataChannelInit
        { public Reliability Reliability; public IntPtr Protocol; public byte Negotiated, ManualStream; public ushort Stream; }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void StateCallback(int id, int state, IntPtr user);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void SimpleCallback(int id, IntPtr user);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void ErrorCallback(int id, IntPtr error, IntPtr user);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void MessageCallback(int id, IntPtr data, int size, IntPtr user);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcCreatePeerConnection(ref Configuration config);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcDeletePeerConnection(int pc);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcSetStateChangeCallback(int pc, StateCallback cb);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcSetIceStateChangeCallback(int pc, StateCallback cb);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcGetSelectedCandidatePair(int pc, [Out] byte[]? local, int localSize, [Out] byte[]? remote, int remoteSize);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcSetGatheringStateChangeCallback(int pc, StateCallback cb);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcSetDataChannelCallback(int pc, StateCallback cb);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcSetLocalDescription(int pc, [MarshalAs(UnmanagedType.LPUTF8Str)] string type);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcSetRemoteDescription(int pc, [MarshalAs(UnmanagedType.LPUTF8Str)] string sdp, [MarshalAs(UnmanagedType.LPUTF8Str)] string type);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcGetLocalDescription(int pc, [Out] byte[] buffer, int size);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcCreateDataChannelEx(int pc, [MarshalAs(UnmanagedType.LPUTF8Str)] string label, ref DataChannelInit init);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcDeleteDataChannel(int dc);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcGetDataChannelLabel(int dc, [Out] byte[] buffer, int size);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcGetDataChannelProtocol(int dc, [Out] byte[] buffer, int size);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcGetDataChannelReliability(int dc, out Reliability reliability);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcSetOpenCallback(int dc, SimpleCallback cb);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcSetClosedCallback(int dc, SimpleCallback cb);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcSetErrorCallback(int dc, ErrorCallback cb);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcSetMessageCallback(int dc, MessageCallback cb);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] [return: MarshalAs(UnmanagedType.I1)] internal static extern bool rtcIsOpen(int dc);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcGetBufferedAmount(int dc);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int rtcSendMessage(int dc, byte[] data, int size);
    }
}

internal sealed record IceServer(string Urls, string Username = "", string Credential = "")
{
    internal string NativeUrl()
    {
        if (Urls is null || Username is null || Credential is null || Urls.Length > 512 || Username.Length > 256 || Credential.Length > 512) throw new IOException("Invalid relay configuration.");
        if (Urls.StartsWith("turns:", StringComparison.Ordinal) || Urls.Contains("transport=tcp", StringComparison.Ordinal) || Urls.Contains("transport=tls", StringComparison.Ordinal))
            throw new IOException("This native build needs a TURN/UDP address. Use the manual browser connection for TURN/TCP or TLS.");
        int separator = Urls.IndexOf(':');
        if (separator < 0 || Urls[..separator] is not ("turn" or "turns" or "stun" or "stuns")) throw new IOException("Invalid relay URL.");
        string authority = Urls[(separator + 1)..].TrimStart('/');
        if (authority.Contains('@') || authority.Contains('#') || authority.Contains(' ')) throw new IOException("Invalid relay URL.");
        return Urls[..separator] + "://" + (Username.Length > 0 ? Uri.EscapeDataString(Username) + ":" + Uri.EscapeDataString(Credential) + "@" : "") + authority;
    }
}
