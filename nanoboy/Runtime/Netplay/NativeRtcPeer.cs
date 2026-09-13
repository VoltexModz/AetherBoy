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
    private readonly Rtc.StateCallback stateCallback, gatheringCallback, channelCallback;
    private readonly Rtc.SimpleCallback openCallback, closeCallback;
    private readonly Rtc.ErrorCallback errorCallback;
    private readonly Rtc.MessageCallback messageCallback;
    private int peer = -1, channel = -1;

    internal NativeRtcPeer(IceServer[] servers, bool relayOnly, ChannelWriter<byte[]> received, Action<Exception?> stopped)
    {
        this.received = received; this.stopped = stopped;
        stateCallback = (_, state, _) => { if (state == 4) Fail("Could not connect through the relay. Check the server's relay ports."); else if (state == 5) stopped(null); };
        gatheringCallback = (_, state, _) => { if (state == 2) gathered.TrySetResult(); };
        channelCallback = (_, id, _) => { if (!incomingChannel.TrySetResult(id)) Fail("The peer opened more than one data channel."); };
        openCallback = (_, _) => opened.TrySetResult();
        closeCallback = (_, _) => stopped(null);
        errorCallback = (_, _, _) => Fail("The online data channel failed.");
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
        await gathered.Task.WaitAsync(TimeSpan.FromSeconds(45), cancellation).ConfigureAwait(false);
        byte[] buffer = new byte[100_001];
        int length = Check(Rtc.rtcGetLocalDescription(peer, buffer, buffer.Length));
        if (length > buffer.Length) throw new IOException("Online description exceeds its limit.");
        return Encoding.UTF8.GetString(buffer, 0, Math.Max(0, length - 1));
    }
    internal void AcceptAnswer(string sdp) => Check(Rtc.rtcSetRemoteDescription(peer, sdp, "answer"));
    internal async Task WaitForOpenAsync(bool host, CancellationToken cancellation)
    {
        if (!host) Bind(await incomingChannel.Task.WaitAsync(TimeSpan.FromSeconds(45), cancellation).ConfigureAwait(false));
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(45), cancellation).ConfigureAwait(false);
    }
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
        if (peer < 0) return;
        if (channel >= 0) { Rtc.rtcDeleteDataChannel(channel); channel = -1; }
        Rtc.rtcDeletePeerConnection(peer); peer = -1;
        GC.KeepAlive(this); // Keep every delegate rooted until native deletion has finished waiting for callbacks.
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
