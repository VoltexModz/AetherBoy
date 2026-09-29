using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using nanoboy.Core;

namespace AetherBoy.Runtime.Netplay;

public enum OnlineLinkPhase { WaitingForBrowser, WaitingForPeer, Playing, WaitingForTransfer, Closed, Faulted }
public sealed record OnlineLinkStatus(OnlineLinkPhase Phase, long TransfersCompleted, string WorkingSavePath, string? Failure)
{
    public string ProfileId { get; init; } = "gb-gbc-v1";
    public string DisplayName { get; init; } = "GB/GBC Online Link";
    public bool IsPaused { get; init; }
    public long CommandsSent { get; init; }
    public long CommandsReceived { get; init; }
    public long CommandsDelivered { get; init; }
}

internal sealed class OnlineLinkState(string savePath, string profileId = "gb-gbc-v1", string displayName = "GB/GBC Online Link")
{
    private OnlineLinkStatus snapshot = new(OnlineLinkPhase.WaitingForBrowser, 0, savePath, null) { ProfileId = profileId, DisplayName = displayName };
    internal OnlineLinkStatus Snapshot => Volatile.Read(ref snapshot);
    internal void Publish(OnlineLinkPhase phase, long count, string? failure = null, bool paused = false,
        long? commandsSent = null, long? commandsReceived = null, long? commandsDelivered = null)
    {
        var previous = Snapshot;
        long sent = commandsSent ?? previous.CommandsSent, received = commandsReceived ?? previous.CommandsReceived,
            delivered = commandsDelivered ?? previous.CommandsDelivered;
        if (previous.Phase != phase || previous.TransfersCompleted != count || previous.Failure != failure || previous.IsPaused != paused ||
            previous.CommandsSent != sent || previous.CommandsReceived != received || previous.CommandsDelivered != delivered)
            Volatile.Write(ref snapshot, new(phase, count, savePath, failure) { ProfileId = profileId, DisplayName = displayName, IsPaused = paused,
                CommandsSent = sent, CommandsReceived = received, CommandsDelivered = delivered });
    }
}

/// <summary>Owner-thread-only wire handling. Network waits stop emulated time, never send invented data.</summary>
internal sealed class OnlineLinkCoordinator : IDisposable
{
    private readonly IOnlineLinkTransport transport;
    private readonly bool host;
    private readonly OnlineLinkState state;
    private readonly byte[] nonce = RandomNumberGenerator.GetBytes(16);
    private readonly NetworkSerialCable cable;
    private byte[]? peerNonce;
    private bool helloSent, disposed;
    private ulong sent, received;
    private long lastReceive = Stopwatch.GetTimestamp(), lastHeartbeat, waitingSince;
    private readonly TimeSpan peerTimeout;
    private readonly TimeSpan transferTimeout;

    internal OnlineLinkCoordinator(Memory memory, bool host, IOnlineLinkTransport transport, OnlineLinkState state,
        TimeSpan? peerTimeout = null, TimeSpan? transferTimeout = null)
    {
        this.transport = transport; this.host = host; this.state = state;
        this.peerTimeout = peerTimeout ?? TimeSpan.FromSeconds(30);
        this.transferTimeout = transferTimeout ?? TimeSpan.FromMinutes(2);
        cable = new NetworkSerialCable(memory, host);
    }

    internal bool Waiting { get; private set; } = true;

    internal void Pump()
    {
        try
        {
            if (transport.Fault is { } fault) throw new IOException("Online link disconnected; original saves are unchanged.", fault);
            if (!transport.Connected)
            {
                if (helloSent || transport.Completion.IsCompleted)
                    throw new IOException("Online link closed; the session copy has been kept.");
                Waiting = true;
                state.Publish(OnlineLinkPhase.WaitingForBrowser, cable.TransfersCompleted);
                return;
            }
            if (!helloSent)
            {
                transport.Send(OnlineLinkProtocol.CreateHello(host, nonce));
                helloSent = true; lastReceive = Stopwatch.GetTimestamp();
            }
            int remaining = 128;
            while (remaining-- > 0 && transport.TryReceive(out byte[] data))
            {
                if (peerNonce is null)
                {
                    peerNonce = OnlineLinkProtocol.ReadHello(data, host);
                    if (peerNonce.AsSpan().SequenceEqual(nonce)) throw new InvalidDataException("A session cannot connect to itself.");
                }
                else
                {
                    var decoded = OnlineLinkProtocol.Decode(data, peerNonce, checked(++received));
                    if (decoded.Kind == OnlineLinkProtocol.Serial) cable.Receive(decoded.Packet);
                }
                lastReceive = Stopwatch.GetTimestamp();
            }
            if (Stopwatch.GetElapsedTime(lastReceive) > peerTimeout)
                throw new TimeoutException("The peer stopped responding. No substitute cable bytes were injected.");
            if (peerNonce is null)
            {
                Waiting = true; state.Publish(OnlineLinkPhase.WaitingForPeer, cable.TransfersCompleted); return;
            }
            if (!cable.IsConnected) throw new InvalidDataException(cable.FailureReason ?? "The serial protocol failed.");
            while (cable.TryDequeueOutgoing(out NetworkSerialPacket packet))
                transport.Send(OnlineLinkProtocol.Encode(OnlineLinkProtocol.Serial, nonce, checked(++sent), packet));
            if (lastHeartbeat == 0 || Stopwatch.GetElapsedTime(lastHeartbeat) >= TimeSpan.FromSeconds(2))
            {
                transport.Send(OnlineLinkProtocol.Encode(OnlineLinkProtocol.Heartbeat, nonce, checked(++sent)));
                lastHeartbeat = Stopwatch.GetTimestamp();
            }
            Waiting = cable.WaitingForPeer;
            if (Waiting)
            {
                if (waitingSince == 0) waitingSince = Stopwatch.GetTimestamp();
                if (Stopwatch.GetElapsedTime(waitingSince) > transferTimeout)
                    throw new TimeoutException(DescribeTransferTimeout(cable.WaitReason));
            }
            else waitingSince = 0;
            state.Publish(Waiting ? OnlineLinkPhase.WaitingForTransfer : OnlineLinkPhase.Playing, cable.TransfersCompleted);
        }
        catch (Exception ex)
        {
            Waiting = true;
            cable.Abort("The network session ended.");
            state.Publish(OnlineLinkPhase.Faulted, cable.TransfersCompleted, ex.Message);
            throw;
        }
    }

    private static string DescribeTransferTimeout(NetworkSerialWaitReason reason) => reason switch
    {
        NetworkSerialWaitReason.UnpairedInternalClock =>
            "The local game requested an internal-clock transfer, but no matching peer offer arrived before the cable timeout. Start a new session.",
        NetworkSerialWaitReason.PeerReady =>
            "Both games offered a serial transfer, but the peer's readiness acknowledgement did not arrive before the cable timeout. Start a new session.",
        NetworkSerialWaitReason.PeerCompletion =>
            "The local serial byte completed, but the peer's completion acknowledgement did not arrive before the cable timeout. Start a new session.",
        _ => "The GB/GBC cable transfer did not complete before its timeout. Start a new session."
    };

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        cable.Dispose();
        if (state.Snapshot.Phase != OnlineLinkPhase.Faulted)
            state.Publish(OnlineLinkPhase.Closed, cable.TransfersCompleted);
    }
}
