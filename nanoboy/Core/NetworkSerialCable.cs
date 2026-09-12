using System;
using System.Collections.Generic;
using System.IO;

namespace nanoboy.Core
{
    public enum NetworkSerialPacketKind : byte
    {
        Offer = 1,
        Ready = 2,
        Complete = 3,
        Cancel = 4
    }

    /// <summary>
    /// Transport-neutral, bounded serial messages. IDs are scoped to one freshly
    /// authenticated session. The transport must preserve reliable message order.
    /// Offers carry an SB snapshot, SC bits 0-1, and a bit period in 4 MHz dots;
    /// Ready/Complete name both transfers and carry no data. Cancel names only its
    /// sender's transfer. This is not a wire format or an authentication mechanism.
    /// </summary>
    public readonly record struct NetworkSerialPacket(
        NetworkSerialPacketKind Kind,
        long TransferId,
        long PeerTransferId = 0,
        byte Data = 0,
        byte Control = 0,
        int ClockPeriodDots = 0);

    /// <summary>
    /// Original two-player GB/GBC paired-transfer network prototype. No sockets,
    /// wall-clock timers, callbacks on network threads, or fabricated reply bytes
    /// live in the core. All operations run on the construction/CPU owner thread.
    /// The runtime drains messages and checks WaitingForPeer between instructions,
    /// stopping the whole machine (not just its serial peripheral) while waiting.
    ///
    /// Unlike a physical unplugged cable, an internal-clock probe without an armed
    /// peer waits for a runtime timeout. This deliberate restriction, per-byte WAN
    /// round trips, and unsupported mid-byte register changes mean this is not a
    /// general-purpose replacement for LocalSerialCable or verified game netplay.
    /// </summary>
    public sealed class NetworkSerialCable : ISerialScheduledDevice, IDisposable
    {
        private const int MaximumQueuedPackets = 64;
        private readonly Memory owner;
        private readonly ISerialDevice previous;
        private readonly bool isHost;
        private readonly int ownerThread = Environment.CurrentManagedThreadId;
        private readonly Queue<NetworkSerialPacket> outgoing = new();
        private NetworkSerialPacket? local;
        private NetworkSerialPacket? remote;
        private NetworkSerialPacket? deferredStart;
        private NetworkSerialPacket? lastRemoteOffer;
        private long nextTransferId;
        private long remoteTerminatedId;
        private bool readySent;
        private bool peerReady;
        private bool localComplete;
        private bool peerComplete;
        private bool running;
        private bool connected = true;
        private bool disposed;
        private int bitPeriodDots;
        private int halfDots;
        private byte incoming;

        public NetworkSerialCable(Memory owner, bool isHost)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.isHost = isHost;
            previous = owner.SerialDevice;
            if (previous is not SerialConsole) {
                throw new InvalidOperationException("The machine already has a serial device attached.");
            }
            owner.AttachSerialDevice(this);
        }

        public bool IsConnected => connected && !disposed && ReferenceEquals(owner.SerialDevice, this);

        /// <summary>
        /// A false value allows one more owner-thread instruction. External-only
        /// transfers remain runnable until a real peer clock is negotiated, so
        /// polling software can cancel or change clock ownership without deadlock.
        /// A fault remains paused until the runtime explicitly ends the session.
        /// </summary>
        public bool WaitingForPeer => !IsConnected || localComplete ||
            (!running && local.HasValue && (readySent || (local.Value.Control & 1) != 0));

        public long TransfersCompleted { get; private set; }
        public string? FailureReason { get; private set; }

        public bool TryDequeueOutgoing(out NetworkSerialPacket packet)
        {
            VerifyOwner();
            return outgoing.TryDequeue(out packet);
        }

        public void Receive(NetworkSerialPacket packet)
        {
            VerifyOwner();
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!IsConnected) {
                throw new InvalidOperationException("The network serial session is not connected.");
            }
            ValidatePacket(packet);
            switch (packet.Kind) {
                case NetworkSerialPacketKind.Offer:
                    ReceiveOffer(packet);
                    break;
                case NetworkSerialPacketKind.Cancel:
                    ReceiveCancel(packet);
                    break;
                case NetworkSerialPacketKind.Ready:
                    if (!MatchesCurrentPair(packet)) return;
                    if (!readySent) ProtocolFault("A peer acknowledged an unnegotiated serial transfer.");
                    if (peerReady) return;
                    peerReady = true;
                    running = true;
                    break;
                case NetworkSerialPacketKind.Complete:
                    if (!MatchesCurrentPair(packet)) return;
                    if (!peerReady) ProtocolFault("A peer completed a serial transfer before acknowledging it.");
                    peerComplete = true;
                    FinishIfBothComplete();
                    break;
            }
        }

        public void Abort(string reason)
        {
            VerifyOwner();
            if (disposed || !connected) return;
            connected = false;
            FailureReason = string.IsNullOrWhiteSpace(reason) ? "The network serial session ended." :
                reason.Length <= 240 ? reason : reason.Substring(0, 240);
            outgoing.Clear();
            running = false;
            local = remote = deferredStart = null;
            owner.AbortLinkedSerialTransfer();
        }

        public void Dispose()
        {
            VerifyOwner();
            if (disposed) return;
            Abort("The network serial session was closed.");
            disposed = true;
            if (ReferenceEquals(owner.SerialDevice, this)) {
                owner.AttachSerialDevice(previous);
            }
        }

        void ISerialDevice.Start()
        {
            if (!IsConnected) {
                owner.AbortLinkedSerialTransfer();
                return;
            }
            if (nextTransferId == long.MaxValue) {
                Abort("The serial transfer ID space was exhausted.");
                return;
            }
            var offer = new NetworkSerialPacket(NetworkSerialPacketKind.Offer,
                ++nextTransferId, Data: owner.SerialData, Control: owner.SerialControl,
                ClockPeriodDots: owner.SerialClockPeriodDots);
            // An instruction may start a new transfer just after the previous
            // eighth edge. Do not let that bounded instruction overshoot bypass
            // the completion barrier or publish its next offer too early.
            if (localComplete) {
                deferredStart = offer;
                return;
            }
            local = offer;
            ResetPair();
            Enqueue(offer);
            TryPair();
        }

        void ISerialDevice.Stop()
        {
            if (!IsConnected) return;
            if (owner.SerialBitsRemaining == 0 && running) {
                running = false;
                localComplete = true;
                Enqueue(new(NetworkSerialPacketKind.Complete, local!.Value.TransferId, remote!.Value.TransferId));
                FinishIfBothComplete();
                return;
            }
            if (deferredStart.HasValue) {
                deferredStart = null;
                return;
            }
            if (localComplete || !local.HasValue) return;
            if (running) {
                Abort("Changing serial control during an agreed network byte is not supported.");
                return;
            }
            Enqueue(new(NetworkSerialPacketKind.Cancel, local.Value.TransferId));
            local = null;
            ResetPair();
        }

        void ISerialDevice.Write(byte value) { }
        byte ISerialDevice.Read() => 0xFF; // Never shifted: the scheduled endpoint owns every edge.

        void ISerialScheduledDevice.SerialDataWritten()
        {
            if (!IsConnected) return;
            if (running || readySent) {
                Abort("Changing serial data during an agreed network byte is not supported.");
                return;
            }
            if (deferredStart.HasValue) {
                deferredStart = deferredStart.Value with { Data = owner.SerialData };
                return;
            }
            if (local.HasValue && !localComplete) {
                ((ISerialDevice)this).Stop();
                ((ISerialDevice)this).Start();
            }
        }

        void ISerialScheduledDevice.TickSerialCycle()
        {
            if (!IsConnected || !running) return;
            // Half dots retain phase across a CGB speed change. Neither host
            // wall time nor the peer's CPU frequency changes the chosen clock.
            halfDots += owner.SerialCpuDoubleSpeed ? 1 : 2;
            if (halfDots < bitPeriodDots * 2) return;
            halfDots -= bitPeriodDots * 2;
            bool bit = (incoming & 0x80) != 0;
            incoming <<= 1;
            if (!owner.TryClockLinkedSerialBit(bit, out _)) {
                Abort("The local serial transfer ended before its agreed network byte.");
            }
        }

        private void ReceiveOffer(NetworkSerialPacket packet)
        {
            if (lastRemoteOffer.HasValue && packet.TransferId == lastRemoteOffer.Value.TransferId) {
                if (packet != lastRemoteOffer.Value) ProtocolFault("A serial transfer ID was reused with different data.");
                return; // Idempotent replay, including a completed transfer.
            }
            if (packet.TransferId <= remoteTerminatedId ||
                (lastRemoteOffer.HasValue && packet.TransferId < lastRemoteOffer.Value.TransferId)) return;
            if (remote.HasValue) {
                ProtocolFault("A peer replaced a pending serial offer without cancelling it.");
            }
            lastRemoteOffer = packet;
            remote = packet;
            TryPair();
        }

        private void ReceiveCancel(NetworkSerialPacket packet)
        {
            if (!lastRemoteOffer.HasValue || packet.TransferId > lastRemoteOffer.Value.TransferId) {
                ProtocolFault("A peer cancelled an unknown serial transfer.");
            }
            if (!remote.HasValue || packet.TransferId != remote.Value.TransferId) return;
            if (running || localComplete) {
                ProtocolFault("A peer cancelled an already agreed serial byte.");
            }
            remoteTerminatedId = Math.Max(remoteTerminatedId, packet.TransferId);
            remote = null;
            ResetPair();
        }

        private void TryPair()
        {
            if (!IsConnected || readySent || !local.HasValue || !remote.HasValue) return;
            bool localClock = (local.Value.Control & 1) != 0;
            bool remoteClock = (remote.Value.Control & 1) != 0;
            if (!localClock && !remoteClock) return;
            // Both internal clocks are electrical contention on real hardware.
            // This prototype follows our local lab's deterministic P1/host winner.
            bool localDrives = localClock && (!remoteClock || isHost);
            bitPeriodDots = localDrives ? local.Value.ClockPeriodDots : remote.Value.ClockPeriodDots;
            incoming = remote.Value.Data;
            halfDots = 0;
            readySent = true;
            Enqueue(new(NetworkSerialPacketKind.Ready, local.Value.TransferId, remote.Value.TransferId));
        }

        private bool MatchesCurrentPair(NetworkSerialPacket packet)
        {
            if (packet.PeerTransferId > nextTransferId || !lastRemoteOffer.HasValue ||
                packet.TransferId > lastRemoteOffer.Value.TransferId) {
                ProtocolFault("A peer referred to an unknown serial transfer pair.");
            }
            return local.HasValue && remote.HasValue &&
                packet.TransferId == remote.Value.TransferId && packet.PeerTransferId == local.Value.TransferId;
        }

        private void FinishIfBothComplete()
        {
            if (!localComplete || !peerComplete) return;
            remoteTerminatedId = Math.Max(remoteTerminatedId, remote!.Value.TransferId);
            TransfersCompleted++;
            local = remote = null;
            ResetPair();
            if (deferredStart.HasValue) {
                local = deferredStart;
                deferredStart = null;
                Enqueue(local.Value);
            }
        }

        private void ResetPair()
        {
            readySent = peerReady = localComplete = peerComplete = running = false;
            halfDots = 0;
        }

        private void Enqueue(NetworkSerialPacket packet)
        {
            if (!IsConnected) return;
            if (outgoing.Count >= MaximumQueuedPackets) {
                Abort("The runtime did not drain its bounded serial packet queue.");
                return;
            }
            outgoing.Enqueue(packet);
        }

        private void ValidatePacket(NetworkSerialPacket packet)
        {
            if (packet.TransferId <= 0) ProtocolFault("Serial transfer IDs must be positive.");
            switch (packet.Kind) {
                case NetworkSerialPacketKind.Offer:
                    if (packet.PeerTransferId != 0 || packet.Control > 3 ||
                        packet.ClockPeriodDots is not (8 or 16 or 256 or 512) ||
                        (((packet.Control & 2) != 0) != (packet.ClockPeriodDots <= 16))) {
                        ProtocolFault("A serial offer contains invalid register or clock values.");
                    }
                    break;
                case NetworkSerialPacketKind.Ready:
                case NetworkSerialPacketKind.Complete:
                    if (packet.PeerTransferId <= 0 || packet.Data != 0 || packet.Control != 0 || packet.ClockPeriodDots != 0) {
                        ProtocolFault("A serial acknowledgement contains unexpected fields.");
                    }
                    break;
                case NetworkSerialPacketKind.Cancel:
                    if (packet.PeerTransferId != 0 || packet.Data != 0 || packet.Control != 0 || packet.ClockPeriodDots != 0) {
                        ProtocolFault("A serial cancellation contains unexpected fields.");
                    }
                    break;
                default:
                    ProtocolFault("An unknown serial packet kind was received.");
                    break;
            }
        }

        private void ProtocolFault(string message)
        {
            Abort(message);
            throw new InvalidDataException(message);
        }

        private void VerifyOwner()
        {
            if (Environment.CurrentManagedThreadId != ownerThread) {
                throw new InvalidOperationException("Network serial state may only be accessed by its emulation owner thread.");
            }
        }
    }
}
