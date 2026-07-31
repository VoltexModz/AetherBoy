using System;

namespace nanoboy.Core
{
    public sealed class Interrupt
    {
        public int IE;
        public int IF;
        private CPU cpu;

        public Interrupt(CPU cpu)
        {
            this.cpu = cpu;
        }

        public void Tick()
        {
            if (cpu.IME) {
                int masked = IE & IF;
                if ((masked & 1) == 1) {
                    cpu.Interrupt(0x40);
                    IF &= ~1;
                    return;
                }
                if ((masked & 2) == 2) {
                    cpu.Interrupt(0x48);
                    IF &= ~2;
                    return;
                }
                if ((masked & 4) == 4) {
                    cpu.Interrupt(0x50);
                    IF &= ~4;
                    return;
                }
                if ((masked & 8) == 8) {
                    cpu.Interrupt(0x58);
                    IF &= ~8;
                    return;
                }
                if ((masked & 16) == 16) {
                    cpu.Interrupt(0x60);
                    IF &= ~16;
                    return;
                }
            }
        }

    }
}
