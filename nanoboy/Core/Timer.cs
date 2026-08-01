using System;

namespace nanoboy.Core
{
    public sealed class Timer
    {
        private readonly Interrupt interrupt;
        private ushort dividerCounter;
        private byte tima;
        private byte tma;
        private byte tac;
        private int reloadDelay;

        public int DIV
        {
            get { return dividerCounter >> 8; }
            set { dividerCounter = (ushort)((value & 0xFF) << 8); }
        }

        public int TIMA
        {
            get { return tima; }
            set { WriteTima((byte)value); }
        }

        public int TMA
        {
            get { return tma; }
            set { WriteTma((byte)value); }
        }

        public int TAC
        {
            get { return tac; }
            set { WriteTac((byte)value); }
        }

        public Timer(Interrupt interrupt)
        {
            this.interrupt = interrupt;
        }

        internal void Reset()
        {
            dividerCounter = 0;
            tima = 0;
            tma = 0;
            tac = 0;
            reloadDelay = 0;
        }

        public void Tick()
        {
            if (reloadDelay > 0)
            {
                reloadDelay--;
                if (reloadDelay == 0)
                {
                    tima = tma;
                    interrupt.Request(4);
                }
            }

            bool oldSignal = GetTimerSignal(dividerCounter, tac);
            dividerCounter++;
            bool newSignal = GetTimerSignal(dividerCounter, tac);
            IncrementOnFallingEdge(oldSignal, newSignal);
        }

        public void WriteDiv()
        {
            bool oldSignal = GetTimerSignal(dividerCounter, tac);
            dividerCounter = 0;
            bool newSignal = GetTimerSignal(dividerCounter, tac);
            IncrementOnFallingEdge(oldSignal, newSignal);
        }

        public void WriteTima(byte value)
        {
            tima = value;
            if (reloadDelay > 0)
            {
                reloadDelay = 0;
            }
        }

        public void WriteTma(byte value)
        {
            tma = value;
        }

        public void WriteTac(byte value)
        {
            bool oldSignal = GetTimerSignal(dividerCounter, tac);
            tac = (byte)(value & 0x07);
            bool newSignal = GetTimerSignal(dividerCounter, tac);
            IncrementOnFallingEdge(oldSignal, newSignal);
        }

        private static bool GetTimerSignal(ushort divider, byte control)
        {
            if ((control & 0x04) == 0)
            {
                return false;
            }

            int dividerBit;
            switch (control & 0x03)
            {
                case 0: dividerBit = 9; break;
                case 1: dividerBit = 3; break;
                case 2: dividerBit = 5; break;
                default: dividerBit = 7; break;
            }

            return ((divider >> dividerBit) & 1) != 0;
        }

        private void IncrementOnFallingEdge(bool oldSignal, bool newSignal)
        {
            if (oldSignal && !newSignal)
            {
                IncrementTima();
            }
        }

        private void IncrementTima()
        {
            // Further timer edges are ignored during the overflow/reload window.
            if (reloadDelay > 0)
            {
                return;
            }

            if (tima == 0xFF)
            {
                tima = 0;
                reloadDelay = 4;
                return;
            }

            tima++;
        }
    }
}
