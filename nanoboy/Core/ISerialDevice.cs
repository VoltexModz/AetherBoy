using System;

namespace nanoboy.Core
{
    public interface ISerialDevice
    {
        void Start();
        void Stop();
        void Write(byte value);
        byte Read();
    }

    /// <summary>
    /// Optional extension for serial devices that exchange data one bit at a time.
    /// Devices that only implement <see cref="ISerialDevice"/> continue to exchange
    /// a complete byte at the start of a transfer.
    /// </summary>
    public interface ISerialBitDevice : ISerialDevice
    {
        bool ExchangeBit(bool outgoingBit);
    }

    // A locally coordinated cable arbitrates competing internal clock sources
    // without blocking the CPU or allowing both ends to shift the same edge.
    internal interface ISerialClockArbiter
    {
        bool CanDriveClock { get; }
    }

    // A network endpoint owns its serial clock after an owner-thread handshake.
    // Its Tick never blocks; the runtime stops whole-machine stepping when the
    // endpoint needs a packet, preserving emulated time instead of sleeping here.
    internal interface ISerialScheduledDevice : ISerialDevice
    {
        void TickSerialCycle();
        void SerialDataWritten();
    }

    public class SerialConsole : ISerialDevice
    {

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void Write(byte value)
        {
        }

        public byte Read()
        {
            return 0xFF;
        }
    }
}
