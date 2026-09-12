using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using GameboyAdvanced.Core.Serial;

namespace AetherBoy.Runtime.Netplay;

/// <summary>Bounded owner-thread bridge. Network callbacks never touch GBA registers.</summary>
internal sealed class GbaOnlineLinkCoordinator : IDisposable
{
    private readonly IOnlineLinkTransport transport;
    private readonly PokemonGen3SerialAdapter adapter;
    private readonly OnlineLinkState state;
    private readonly bool host;
    private readonly byte[] nonce = RandomNumberGenerator.GetBytes(16);
    private readonly TimeSpan peerTimeout;
    private readonly TimeSpan closeTimeout;
    private byte[]? peerNonce;
    private bool helloSent, disposed, localPaused, peerPaused, pauseDirty = true;
    private ulong sent, received;
    private long lastReceive = Stopwatch.GetTimestamp(), lastHeartbeat;
    private long closingSince, closeGraceStarted;
    private bool closeRequested, closeSent, peerCloseRequested, closeAckSent, closeAckReceived, closeReceiptSent, closeReceiptReceived;
    internal bool Waiting { get; private set; } = true;
    internal bool Paused => localPaused || peerPaused;
    internal bool StopReady { get; private set; }

    internal GbaOnlineLinkCoordinator(SerialController serial, bool host, IOnlineLinkTransport transport,
        OnlineLinkState state, TimeSpan? peerTimeout = null, TimeSpan? closeTimeout = null)
    {
        this.transport = transport; this.host = host; this.state = state;
        this.peerTimeout = peerTimeout ?? TimeSpan.FromSeconds(30);
        this.closeTimeout = closeTimeout ?? TimeSpan.FromSeconds(3);
        adapter = new(serial, host);
    }

    internal void SetPaused(bool paused)
    {
        if (localPaused == paused) return;
        localPaused = paused; pauseDirty = true;
    }

    internal void RequestStop()
    {
        if (closeRequested) return;
        closeRequested = true; closingSince = Stopwatch.GetTimestamp(); Waiting = true;
        // No game cycle has run before its peer's profile handshake.
        if (peerNonce is null) StopReady = true;
    }

    internal void Pump()
    {
        try
        {
            if (StopReady) return;
            if (transport.Fault is { } fault) throw new IOException("GBA Online disconnected; the working save copy is preserved.", fault);
            if (!transport.Connected && !(closeRequested && peerNonce is not null))
            {
                if (helloSent || transport.Completion.IsCompleted) throw new IOException("GBA Online connection closed. No trade completion is implied.");
                Waiting = true; state.Publish(OnlineLinkPhase.WaitingForBrowser, adapter.TransfersCompleted); return;
            }
            if (!helloSent)
            {
                transport.Send(GbaOnlineLinkProtocol.CreateHello(host, nonce));
                helloSent = true; lastReceive = Stopwatch.GetTimestamp();
            }
            int remaining = 128;
            while (remaining-- > 0 && transport.TryReceive(out byte[] data))
            {
                if (peerNonce is null)
                {
                    peerNonce = GbaOnlineLinkProtocol.ReadHello(data, host);
                    if (peerNonce.AsSpan().SequenceEqual(nonce)) throw new InvalidDataException("A GBA online session cannot connect to itself.");
                }
                else
                {
                    var packet = GbaOnlineLinkProtocol.Decode(data, peerNonce, checked(++received));
                    switch (packet.Kind)
                    {
                        case GbaOnlineLinkProtocol.Message:
                            if (peerCloseRequested) throw new InvalidDataException("The peer sent a new Gen3 message after requesting closure.");
                            adapter.Receive(packet.Message); break;
                        case GbaOnlineLinkProtocol.Pause: peerPaused = packet.Paused; break;
                        case GbaOnlineLinkProtocol.CloseRequest:
                            if (peerCloseRequested) throw new InvalidDataException("Duplicate GBA close request.");
                            peerCloseRequested = true; RequestStop(); break;
                        case GbaOnlineLinkProtocol.CloseAck:
                            if (!closeSent || closeAckReceived) throw new InvalidDataException("Unexpected GBA close acknowledgement.");
                            closeAckReceived = true; break;
                        case GbaOnlineLinkProtocol.CloseReceipt:
                            if (!closeAckSent || closeReceiptReceived) throw new InvalidDataException("Unexpected GBA close receipt.");
                            closeReceiptReceived = true; break;
                    }
                }
                lastReceive = Stopwatch.GetTimestamp();
            }
            if (Stopwatch.GetElapsedTime(lastReceive) > peerTimeout)
                throw new TimeoutException("The GBA peer stopped responding. No payload or save success was fabricated.");
            if (peerNonce is null)
            {
                Waiting = true; state.Publish(OnlineLinkPhase.WaitingForPeer, adapter.TransfersCompleted); return;
            }
            if (closeRequested)
            {
                // Stop generation first. A partial/in-flight application block makes the result uncertain,
                // so it can never be laundered into a clean-stop acknowledgement.
                if (adapter.HasPendingPayload)
                    throw new IOException("GBA Online ended with undelivered commands. The working copies need manual review.");
                // A peer can close immediately after its final ACK was queued locally. Consume and
                // validate those already-received bytes before classifying the transport close.
                if (closeReceiptSent && closeReceiptReceived)
                {
                    // This peer has processed our ACK; retain the browser bridge briefly for its own
                    // final receipt to drain. This grace is not a two-computer atomic save commit.
                    if (closeGraceStarted == 0) closeGraceStarted = Stopwatch.GetTimestamp();
                    StopReady = Stopwatch.GetElapsedTime(closeGraceStarted) >= TimeSpan.FromMilliseconds(250);
                    Waiting = true; return;
                }
                if (!transport.Connected)
                    throw new IOException("The GBA connection closed before both close acknowledgements arrived.");
                while (adapter.TryDequeueOutgoing(out PokemonGen3Message final))
                    transport.Send(GbaOnlineLinkProtocol.Encode(GbaOnlineLinkProtocol.Message, nonce, checked(++sent), final));
                if (!closeSent)
                {
                    transport.Send(GbaOnlineLinkProtocol.Encode(GbaOnlineLinkProtocol.CloseRequest, nonce, checked(++sent)));
                    closeSent = true;
                }
                if (peerCloseRequested && !closeAckSent)
                {
                    transport.Send(GbaOnlineLinkProtocol.Encode(GbaOnlineLinkProtocol.CloseAck, nonce, checked(++sent)));
                    closeAckSent = true;
                }
                if (closeAckReceived && !closeReceiptSent)
                {
                    transport.Send(GbaOnlineLinkProtocol.Encode(GbaOnlineLinkProtocol.CloseReceipt, nonce, checked(++sent)));
                    closeReceiptSent = true;
                }
                if (!StopReady && Stopwatch.GetElapsedTime(closingSince) > closeTimeout)
                    throw new TimeoutException("The peer did not confirm a quiescent GBA close. The trade outcome is uncertain.");
                Waiting = true;
                state.Publish(OnlineLinkPhase.WaitingForTransfer, adapter.TransfersCompleted);
                return;
            }
            if (pauseDirty)
            {
                transport.Send(GbaOnlineLinkProtocol.Encode(GbaOnlineLinkProtocol.Pause, nonce, checked(++sent), paused: localPaused));
                pauseDirty = false;
            }
            if (!Paused) adapter.Poll();
            if (!adapter.IsConnected) throw new InvalidDataException(adapter.FailureReason ?? "GBA Gen3 protocol aborted.");
            while (adapter.TryDequeueOutgoing(out PokemonGen3Message message))
                transport.Send(GbaOnlineLinkProtocol.Encode(GbaOnlineLinkProtocol.Message, nonce, checked(++sent), message));
            if (lastHeartbeat == 0 || Stopwatch.GetElapsedTime(lastHeartbeat) >= TimeSpan.FromSeconds(2))
            {
                transport.Send(GbaOnlineLinkProtocol.Encode(GbaOnlineLinkProtocol.Heartbeat, nonce, checked(++sent)));
                lastHeartbeat = Stopwatch.GetTimestamp();
            }
            Waiting = Paused || adapter.WaitingForPeer;
            state.Publish(Waiting ? OnlineLinkPhase.WaitingForTransfer : OnlineLinkPhase.Playing,
                adapter.TransfersCompleted, paused: Paused, commandsSent: adapter.CommandsSent,
                commandsReceived: adapter.CommandsReceived, commandsDelivered: adapter.CommandsDelivered);
        }
        catch (Exception ex)
        {
            Waiting = true; adapter.Abort("The GBA network session ended.");
            state.Publish(OnlineLinkPhase.Faulted, adapter.TransfersCompleted, ex.Message);
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (adapter.HasPendingPayload && state.Snapshot.Phase != OnlineLinkPhase.Faulted)
            state.Publish(OnlineLinkPhase.Faulted, adapter.TransfersCompleted, "GBA Online closed with undelivered commands; inspect the working copies.");
        adapter.Dispose();
        if (state.Snapshot.Phase != OnlineLinkPhase.Faulted)
            state.Publish(OnlineLinkPhase.Closed, adapter.TransfersCompleted);
    }
}
