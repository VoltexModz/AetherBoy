using System;
using System.IO;

namespace nanoboy.Core
{
    public sealed class HDMA
    {
        private readonly Memory memory;
        private int remainingBlocks;
        private bool transferActive;
        private bool transferCancelled;
        private bool oamTransferActive;
        private int oamSourceAddress;
        private int oamByteIndex;
        private int oamCyclePhase;

        public HDMA(Memory memory)
        {
            this.memory = memory;
        }

        public int SourceAddress { get; set; }
        public int DestinationAddress { get; set; }
        public int RemainingBlocks => remainingBlocks;
        public bool IsHBlank => transferActive;
        public bool IsOamTransferActive => oamTransferActive;
        public int OamBytesTransferred => oamByteIndex;

        internal bool IsCpuBusBlocked(int address) =>
            oamTransferActive && (address < 0xFF80 || address > 0xFFFE);

        internal void StartOamDma(byte sourceHigh)
        {
            oamTransferActive = true;
            oamSourceAddress = sourceHigh << 8;
            oamByteIndex = 0;
            oamCyclePhase = 0;
        }

        internal void TickOamDma()
        {
            if (!oamTransferActive) {
                return;
            }

            oamCyclePhase++;
            if (oamCyclePhase < 4) {
                return;
            }

            oamCyclePhase = 0;
            memory.Video.WriteOAMDirect(
                oamByteIndex,
                memory.ReadByteForDma((oamSourceAddress + oamByteIndex) & 0xFFFF));
            oamByteIndex++;
            if (oamByteIndex == 0xA0) {
                oamTransferActive = false;
            }
        }

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

        internal byte[] CaptureStatePayload()
        {
            return StatePayload.Write(writer => {
                writer.Write(SourceAddress);
                writer.Write(DestinationAddress);
                writer.Write(remainingBlocks);
                writer.Write(transferActive);
                writer.Write(transferCancelled);
                writer.Write(oamTransferActive);
                writer.Write(oamSourceAddress);
                writer.Write(oamByteIndex);
                writer.Write(oamCyclePhase);
            });
        }

        internal Action PrepareStateRestore(byte[] payload)
        {
            return StatePayload.Read(payload, reader => {
                int nextSourceAddress = reader.ReadInt32();
                int nextDestinationAddress = reader.ReadInt32();
                int nextRemainingBlocks = reader.ReadInt32();
                bool nextTransferActive = StatePayload.ReadBoolean(reader);
                bool nextTransferCancelled = StatePayload.ReadBoolean(reader);
                bool nextOamTransferActive = StatePayload.ReadBoolean(reader);
                int nextOamSourceAddress = reader.ReadInt32();
                int nextOamByteIndex = reader.ReadInt32();
                int nextOamCyclePhase = reader.ReadInt32();
                StatePayload.RequireRange(nextSourceAddress, 0, 0xFFF0, nameof(SourceAddress));
                StatePayload.RequireRange(nextDestinationAddress, 0, 0x1FF0, nameof(DestinationAddress));
                StatePayload.RequireRange(nextRemainingBlocks, 0, 0x80, nameof(remainingBlocks));
                if (nextTransferActive && (nextTransferCancelled || nextRemainingBlocks == 0)) {
                    throw new InvalidOperationException("HDMA state contains contradictory transfer flags.");
                }
                StatePayload.RequireRange(nextOamSourceAddress, 0, 0xFF00, nameof(oamSourceAddress));
                StatePayload.RequireRange(nextOamByteIndex, 0, 0xA0, nameof(oamByteIndex));
                StatePayload.RequireRange(nextOamCyclePhase, 0, 3, nameof(oamCyclePhase));
                if ((nextOamTransferActive && nextOamByteIndex >= 0xA0) ||
                    (!nextOamTransferActive && nextOamByteIndex != 0 && nextOamByteIndex != 0xA0) ||
                    (!nextOamTransferActive && nextOamCyclePhase != 0)) {
                    throw new InvalidDataException("OAM DMA state has contradictory progress flags.");
                }

                return (Action)(() => {
                    SourceAddress = nextSourceAddress;
                    DestinationAddress = nextDestinationAddress;
                    remainingBlocks = nextRemainingBlocks;
                    transferActive = nextTransferActive;
                    transferCancelled = nextTransferCancelled;
                    oamTransferActive = nextOamTransferActive;
                    oamSourceAddress = nextOamSourceAddress;
                    oamByteIndex = nextOamByteIndex;
                    oamCyclePhase = nextOamCyclePhase;
                });
            });
        }
    }
}
