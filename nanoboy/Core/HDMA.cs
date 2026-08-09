using System;

namespace nanoboy.Core
{
    public sealed class HDMA
    {
        private readonly Memory memory;
        private int remainingBlocks;
        private bool transferActive;
        private bool transferCancelled;

        public HDMA(Memory memory)
        {
            this.memory = memory;
        }

        public int SourceAddress { get; set; }
        public int DestinationAddress { get; set; }
        public int RemainingBlocks => remainingBlocks;
        public bool IsHBlank => transferActive;

        public byte ReadControl()
        {
            if (transferActive) {
                return (byte)(remainingBlocks - 1);
            }
            if (transferCancelled) {
                return (byte)(0x80 | Math.Max(0, remainingBlocks - 1));
            }

            return 0xFF;
        }

        public void WriteControl(byte value)
        {
            if (transferActive && (value & 0x80) == 0) {
                transferActive = false;
                transferCancelled = true;
                return;
            }

            remainingBlocks = (value & 0x7F) + 1;
            transferCancelled = false;
            if ((value & 0x80) != 0) {
                transferActive = true;
                return;
            }

            transferActive = false;
            while (remainingBlocks > 0) {
                CopyBlock();
            }
        }

        public void PerformHBlank()
        {
            if (!transferActive || remainingBlocks == 0) {
                return;
            }

            CopyBlock();
            if (remainingBlocks == 0) {
                transferActive = false;
            }
        }

        private void CopyBlock()
        {
            for (int offset = 0; offset < 0x10; offset++) {
                byte value = memory.ReadByteForDma((SourceAddress + offset) & 0xFFFF);
                memory.Video.WriteVRAMDirect(
                    memory.Video.VRAMBank,
                    (DestinationAddress + offset) & 0x1FFF,
                    value);
            }

            SourceAddress = (SourceAddress + 0x10) & 0xFFF0;
            DestinationAddress = (DestinationAddress + 0x10) & 0x1FF0;
            remainingBlocks--;
        }
    }
}
