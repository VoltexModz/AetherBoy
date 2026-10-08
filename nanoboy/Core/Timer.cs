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
        private bool reloadedThisCycle;

        // The serial prescaler uses the running system divider, including its
        // low bits that are not visible through the DIV register.
        internal ushort DividerCounter => dividerCounter;

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
            reloadedThisCycle = false;
        }

        public void Tick()
        {
            reloadedThisCycle = false;
            if (reloadDelay > 0)
            {
                reloadDelay--;
                if (reloadDelay == 0)
                {
                    tima = tma;
                    interrupt.Request(4);
                    reloadedThisCycle = true;
                }
            }

            bool oldSignal = GetTimerSignal(dividerCounter, tac);
            dividerCounter++;
            bool newSignal = GetTimerSignal(dividerCounter, tac);
            IncrementOnFallingEdge(oldSignal, newSignal);
        }

        public bool WriteDiv(bool doubleSpeed = false)
        {
            bool oldSignal = GetTimerSignal(dividerCounter, tac);
            bool apuFallingEdge = ApuDividerHigh(doubleSpeed);
            dividerCounter = 0;
            bool newSignal = GetTimerSignal(dividerCounter, tac);
            IncrementOnFallingEdge(oldSignal, newSignal);
            return apuFallingEdge;
        }

        internal bool ApuDividerHigh(bool doubleSpeed = false)
        {
            int dividerBit = doubleSpeed ? 13 : 12;
            return ((dividerCounter >> dividerBit) & 1) != 0;
        }

        public void WriteTima(byte value)
        {
            if (reloadedThisCycle)
            {
                return;
            }

            tima = value;
            if (reloadDelay > 0)
            {
                reloadDelay = 0;
            }
        }

        public void WriteTma(byte value)
        {
            tma = value;
            if (reloadedThisCycle)
            {
                tima = value;
            }
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

        internal byte[] CaptureStatePayload()
        {
            return StatePayload.Write(writer => {
                writer.Write(dividerCounter);
                writer.Write(tima);
                writer.Write(tma);
                writer.Write(tac);
                writer.Write(reloadDelay);
                writer.Write(reloadedThisCycle);
            });
        }

        internal Action PrepareStateRestore(byte[] payload)
        {
            return StatePayload.Read(payload, reader => {
                ushort nextDivider = reader.ReadUInt16();
                byte nextTima = reader.ReadByte();
                byte nextTma = reader.ReadByte();
                byte nextTac = reader.ReadByte();
                int nextReloadDelay = reader.ReadInt32();
                bool nextReloadedThisCycle = StatePayload.ReadBoolean(reader);
                StatePayload.RequireRange(nextTac, 0, 7, nameof(tac));
                StatePayload.RequireRange(nextReloadDelay, 0, 4, nameof(reloadDelay));

                return (Action)(() => {
                    dividerCounter = nextDivider;
                    tima = nextTima;
                    tma = nextTma;
                    tac = nextTac;
                    reloadDelay = nextReloadDelay;
                    reloadedThisCycle = nextReloadedThisCycle;
                });
            });
        }
    }
}
