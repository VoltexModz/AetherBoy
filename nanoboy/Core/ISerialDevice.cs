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
            return 0;
        }
    }
}
