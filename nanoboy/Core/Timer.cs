using System;

namespace nanoboy.Core
{
    public sealed class Timer
    {
        public int DIV;
        public int TIMA;
        public int TMA;
        public int TAC;
        private Interrupt interrupt;
        private int divcycles;
        private int timacycles;

        public Timer(Interrupt interrupt)
        {
            this.interrupt = interrupt;
        }

        public void Tick(bool doublespeed)
        {
            int divclock = doublespeed ? 128 : 256;
            int timaclock = 0;

            divcycles++;
            timacycles++;

            if (divcycles == divclock) {
                divcycles = 0;
                DIV = (DIV + 1) % 256;
            }

            if ((TAC & 0x4) == 0x4) {
                switch (TAC & 3)
                {
                    case 0: timaclock = 1024; break;
                    case 1: timaclock = 16; break;
                    case 2: timaclock = 64; break;
                    case 3: timaclock = 256; break;
                }

                if (timacycles == timaclock) {
                    timacycles = 0;
                    TIMA++;
                    if (TIMA > 256) {
                        TIMA = TMA;
                        interrupt.IF |= 4;
                    }
                }
            }
        }

    }
}
