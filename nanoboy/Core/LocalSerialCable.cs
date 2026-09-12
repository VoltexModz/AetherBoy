using System;

namespace nanoboy.Core
{
    /// <summary>
    /// A two-player GB/GBC cable exchanging simultaneous bits at emulated serial
    /// clock boundaries. Both machines and this cable must run on one owner
    /// thread with coordinated stepping; this is not a cross-thread transport.
    /// </summary>
    public sealed class LocalSerialCable : IDisposable
    {
        private readonly Memory first;
        private readonly Memory second;
        private readonly ISerialDevice previousFirst;
        private readonly ISerialDevice previousSecond;
        private readonly Endpoint firstEndpoint;
        private readonly Endpoint secondEndpoint;
        private bool cableConnected = true;
        private bool disposed;

        public LocalSerialCable(Memory first, Memory second)
        {
            this.first = first ?? throw new ArgumentNullException(nameof(first));
            this.second = second ?? throw new ArgumentNullException(nameof(second));
            if (ReferenceEquals(first, second)) {
                throw new ArgumentException("A link cable requires two different machines.", nameof(second));
            }
            if (first.SerialDevice is Endpoint || second.SerialDevice is Endpoint) {
                throw new InvalidOperationException("A machine is already attached to a local link cable.");
            }

            previousFirst = first.SerialDevice;
            previousSecond = second.SerialDevice;
            firstEndpoint = new Endpoint(this, first, second, isFirst: true);
            secondEndpoint = new Endpoint(this, second, first, isFirst: false);
            first.AttachSerialDevice(firstEndpoint);
            try {
                second.AttachSerialDevice(secondEndpoint);
            } catch {
                first.AttachSerialDevice(previousFirst);
                throw;
            }
        }

        private bool EndpointsAttached =>
            ReferenceEquals(first.SerialDevice, firstEndpoint) &&
            ReferenceEquals(second.SerialDevice, secondEndpoint);

        public bool Connected => !disposed && cableConnected && EndpointsAttached;

        /// <summary>
        /// Plugs or unplugs the physical cable without changing either machine's
        /// serial registers, pending transfer, or clock phase. While unplugged,
        /// internal clocks continue receiving high bits and external clocks wait.
        /// </summary>
        public void SetConnected(bool connected)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (connected && !EndpointsAttached) {
                throw new InvalidOperationException("A serial endpoint was replaced; this cable cannot reconnect it.");
            }
            cableConnected = connected;
        }

        /// <summary>Clock edges exchanged while both endpoints were connected.</summary>
        public long ClockEdges { get; private set; }

        /// <summary>
        /// Edges where both machines requested internal clock. Real cable clock
        /// contention is unsupported; this implementation consistently lets the
        /// first machine drive, allowing software to recover without deadlock.
        /// </summary>
        public long ContendedClockEdges { get; private set; }

        public void Dispose()
        {
            if (disposed) {
                return;
            }

            disposed = true;
            // Never replace a device the caller deliberately attached meanwhile.
            // AttachSerialDevice also aborts any half-finished transfer, without
            // raising a fabricated serial completion interrupt.
            try {
                if (ReferenceEquals(first.SerialDevice, firstEndpoint)) {
                    first.AttachSerialDevice(previousFirst);
                }
            } finally {
                if (ReferenceEquals(second.SerialDevice, secondEndpoint)) {
                    second.AttachSerialDevice(previousSecond);
                }
            }
        }

        private sealed class Endpoint : ISerialBitDevice, ISerialClockArbiter
        {
            private readonly LocalSerialCable cable;
            private readonly Memory owner;
            private readonly Memory peer;
            private readonly bool isFirst;

            public Endpoint(LocalSerialCable cable, Memory owner, Memory peer, bool isFirst)
            {
                this.cable = cable;
                this.owner = owner;
                this.peer = peer;
                this.isFirst = isFirst;
            }

            public bool CanDriveClock => !cable.Connected || isFirst || !peer.HasInternalSerialClock;

            public bool ExchangeBit(bool outgoingBit)
            {
                if (!cable.Connected) {
                    return true;
                }

                cable.ClockEdges++;
                if (owner.HasInternalSerialClock && peer.HasInternalSerialClock) {
                    cable.ContendedClockEdges++;
                }

                // TryClock returns the peer's outgoing bit before changing its
                // shift register; our owner shifts only after ExchangeBit returns.
                // An idle/disconnected serial data line is pulled high.
                return !peer.TryClockLinkedSerialBit(outgoingBit, out bool incomingBit) || incomingBit;
            }

            public void Start() { }
            public void Stop() { }
            public void Write(byte value) { }
            public byte Read() => 0xFF;
        }
    }
}
