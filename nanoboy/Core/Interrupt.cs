using System;
using System.Threading;

namespace nanoboy.Core
{
    public sealed class Interrupt
    {
        public int IE;
        private int interruptFlags;
        private CPU cpu;

        public int IF
        {
            get => Volatile.Read(ref interruptFlags);
            set => Volatile.Write(ref interruptFlags, value);
        }

        public Interrupt(CPU cpu)
        {
            this.cpu = cpu;
        }

        public int ServicePending()
        {
            int masked = IE & IF & 0x1F;
            if (masked == 0)
            {
                return 0;
            }

            // A pending enabled interrupt releases HALT even while IME is clear.
            cpu.WaitForInterrupt = false;
            if (!cpu.IME)
            {
                return 0;
            }

            int interruptMask;
            int vector;
            if ((masked & 1) != 0)
            {
                interruptMask = 1;
                vector = 0x40;
            }
            else if ((masked & 2) != 0)
            {
                interruptMask = 2;
                vector = 0x48;
            }
            else if ((masked & 4) != 0)
            {
                interruptMask = 4;
                vector = 0x50;
            }
            else if ((masked & 8) != 0)
            {
                interruptMask = 8;
                vector = 0x58;
            }
            else
            {
                interruptMask = 16;
                vector = 0x60;
            }

            Interlocked.And(ref interruptFlags, ~interruptMask);
            cpu.Interrupt(vector);
            return 20;
        }

        internal void Request(int mask)
        {
            Interlocked.Or(ref interruptFlags, mask & 0x1F);
        }

        public void Tick()
        {
            ServicePending();
        }

        internal byte[] CaptureStatePayload()
        {
            return StatePayload.Write(writer => {
                writer.Write((byte)IE);
                writer.Write((byte)IF);
            });
        }

        internal Action PrepareStateRestore(byte[] payload)
        {
            return StatePayload.Read(payload, reader => {
                int nextIe = reader.ReadByte();
                int nextIf = reader.ReadByte();
                return (Action)(() => {
                    IE = nextIe;
                    IF = nextIf;
                });
            });
        }

    }
}
