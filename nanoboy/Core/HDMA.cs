using System;

namespace nanoboy.Core
{
    public sealed class HDMA
    {

        public int SourceAddress;
        public int DestinationAddress;
        public int Length;
        public bool IsHBlank;

        private Memory memory;
        private bool inhblankdma;
        private int hblankremaining;
        private int hblankprogress;

        public HDMA(Memory memory)
        {
            this.memory = memory;
        }

        public void PerformGeneralPurpose()
        {

            for (int i = 0; i < Length; i++) {
                memory.WriteByte(0x8000 + DestinationAddress, memory.ReadByte(SourceAddress));
            }


            IsHBlank = true;
            Length = 0x7F;
        }

        public void PerformHBlank()
        {
            if (!inhblankdma) {
                inhblankdma = true;
                hblankremaining = Length;
                hblankprogress = 0;
            }

            for (int i = 0; i < 0x10; i++) {
                memory.WriteByte(0x8000 + DestinationAddress + hblankprogress + i,
                                 memory.ReadByte(SourceAddress + hblankprogress + i));
            }

            hblankprogress += 0x10;

            if (hblankprogress == hblankremaining) {
                IsHBlank = false;
                inhblankdma = false;
            }
        }

    }
}
