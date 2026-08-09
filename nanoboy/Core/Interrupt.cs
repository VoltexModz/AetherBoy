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
            if (cpu.IsLockedUp)
            {
                return 0;
            }

            (int initialMask, _) = SelectPending();
            if (initialMask == 0)
            {
                return 0;
            }

            // A pending enabled interrupt releases HALT even while IME is clear.
            cpu.WaitForInterrupt = false;
            if (!cpu.IME)
            {
                return 0;
            }

            int interruptMask = cpu.DispatchInterrupt(SelectPending);
            if (interruptMask != 0)
            {
                Interlocked.And(ref interruptFlags, ~interruptMask);
            }
            return 20;
        }

        private (int Mask, int Vector) SelectPending()
        {
            int masked = IE & IF & 0x1F;
            if ((masked & 1) != 0)
            {
                return (1, 0x40);
            }
            if ((masked & 2) != 0)
            {
                return (2, 0x48);
            }
            if ((masked & 4) != 0)
            {
                return (4, 0x50);
            }
            if ((masked & 8) != 0)
            {
                return (8, 0x58);
            }
            if ((masked & 16) != 0)
            {
                return (16, 0x60);
            }
            return (0, 0);
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
