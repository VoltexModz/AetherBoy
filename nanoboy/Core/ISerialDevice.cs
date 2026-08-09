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
