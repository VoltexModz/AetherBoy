using System;
using System.IO;

namespace nanoboy.Core
{
    public sealed class HDMA
    {
        private readonly Memory memory;
        private int remainingBlocks;
        private bool transferActive;
        private bool generalTransferActive;
        private bool transferCancelled;
        private bool blockTransferActive;
        private int blockByteIndex;
        private int blockDotPhase;
        private bool oamTransferActive;
        private bool oamStartPending;
        private int oamSourceAddress;
        private byte oamSourceRegister;
        private int oamStartDelay;
        private int oamByteIndex;
        private int oamCyclePhase;
        private int pendingCpuStallDots;

        public HDMA(Memory memory)
        {
            this.memory = memory;
        }

        public int SourceAddress { get; set; }
        public int DestinationAddress { get; set; }
        public int RemainingBlocks => remainingBlocks;
        public bool IsHBlank => transferActive;
        public bool IsGeneralTransferActive => generalTransferActive;
        public bool IsOamTransferActive => oamTransferActive;
        public bool IsOamTransferPending => oamStartPending;
        public int OamBytesTransferred => oamByteIndex;
        public byte OamSourceRegister => oamSourceRegister;
        public int PendingCpuStallDots => pendingCpuStallDots;

        internal bool IsCpuBusBlocked(int address) =>
            oamTransferActive && address != 0xFF46 && (address < 0xFF80 || address > 0xFFFE);

        internal void StartOamDma(byte sourceHigh)
        {
            oamSourceRegister = sourceHigh;
            oamStartPending = true;
            oamStartDelay = 8;
        }

        internal void TickOamDma()
        {
            if (oamStartPending) {
                oamStartDelay--;
                if (oamStartDelay == 0) {
                    oamStartPending = false;
                    oamTransferActive = true;
                    oamSourceAddress = oamSourceRegister << 8;
                    oamByteIndex = 0;
                    oamCyclePhase = 0;
                    return;
                }
            }

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

        internal bool ConsumeCpuStallDot()
        {
            if (pendingCpuStallDots == 0) {
                return false;
            }

            pendingCpuStallDots--;
            if (blockTransferActive) {
                blockDotPhase++;
                if (blockDotPhase == 2) {
                    blockDotPhase = 0;
                    memory.Video.WriteVRAMDirect(
                        memory.Video.VRAMBank,
                        (DestinationAddress + blockByteIndex) & 0x1FFF,
                        memory.ReadByteForDma((SourceAddress + blockByteIndex) & 0xFFFF));
                    blockByteIndex++;
                    if (blockByteIndex == 0x10) {
                        CompleteBlockTransfer();
                    }
                }
            }
            return true;
        }

        internal void Reset()
        {
            SourceAddress = 0;
            DestinationAddress = 0;
            remainingBlocks = 0;
            transferActive = false;
            generalTransferActive = false;
            transferCancelled = false;
            blockTransferActive = false;
            blockByteIndex = 0;
            blockDotPhase = 0;
            oamTransferActive = false;
            oamStartPending = false;
            oamSourceAddress = 0;
            oamSourceRegister = 0xFF;
            oamStartDelay = 0;
            oamByteIndex = 0;
            oamCyclePhase = 0;
            pendingCpuStallDots = 0;
        }

        public byte ReadControl()
        {
            if (transferActive || generalTransferActive) {
                return (byte)(remainingBlocks - 1);
            }
            if (transferCancelled) {
                return (byte)(0x80 | Math.Max(0, remainingBlocks - 1));
            }

            return 0xFF;
        }

        public void WriteControl(byte value)
        {
            if (generalTransferActive) {
                return;
            }
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
            generalTransferActive = true;
            blockTransferActive = true;
            blockByteIndex = 0;
            blockDotPhase = 0;
            pendingCpuStallDots = remainingBlocks * 32;
        }

        public void PerformHBlank()
        {
            if (!transferActive || remainingBlocks == 0 || blockTransferActive || memory.CpuIsHalted) {
                return;
            }

            blockTransferActive = true;
            blockByteIndex = 0;
            blockDotPhase = 0;
            pendingCpuStallDots += 32;
        }

        private void CompleteBlockTransfer()
        {
            SourceAddress = (SourceAddress + 0x10) & 0xFFF0;
            DestinationAddress = (DestinationAddress + 0x10) & 0x1FF0;
            remainingBlocks--;
            blockByteIndex = 0;
            blockDotPhase = 0;
            if (generalTransferActive) {
                if (remainingBlocks == 0) {
                    generalTransferActive = false;
                    blockTransferActive = false;
                }
            } else {
                blockTransferActive = false;
                if (remainingBlocks == 0) {
                    transferActive = false;
                }
            }
        }

        internal byte[] CaptureStatePayload()
        {
            return StatePayload.Write(writer => {
                writer.Write(SourceAddress);
                writer.Write(DestinationAddress);
                writer.Write(remainingBlocks);
                writer.Write(transferActive);
                writer.Write(generalTransferActive);
                writer.Write(transferCancelled);
                writer.Write(blockTransferActive);
                writer.Write(blockByteIndex);
                writer.Write(blockDotPhase);
                writer.Write(oamTransferActive);
                writer.Write(oamStartPending);
                writer.Write(oamSourceAddress);
                writer.Write(oamSourceRegister);
                writer.Write(oamStartDelay);
                writer.Write(oamByteIndex);
                writer.Write(oamCyclePhase);
                writer.Write(pendingCpuStallDots);
            });
        }

        internal Action PrepareStateRestore(byte[] payload)
        {
            return StatePayload.Read(payload, reader => {
                int nextSourceAddress = reader.ReadInt32();
                int nextDestinationAddress = reader.ReadInt32();
                int nextRemainingBlocks = reader.ReadInt32();
                bool nextTransferActive = StatePayload.ReadBoolean(reader);
                bool nextGeneralTransferActive = StatePayload.ReadBoolean(reader);
                bool nextTransferCancelled = StatePayload.ReadBoolean(reader);
                bool nextBlockTransferActive = StatePayload.ReadBoolean(reader);
                int nextBlockByteIndex = reader.ReadInt32();
                int nextBlockDotPhase = reader.ReadInt32();
                bool nextOamTransferActive = StatePayload.ReadBoolean(reader);
                bool nextOamStartPending = StatePayload.ReadBoolean(reader);
                int nextOamSourceAddress = reader.ReadInt32();
                byte nextOamSourceRegister = reader.ReadByte();
                int nextOamStartDelay = reader.ReadInt32();
                int nextOamByteIndex = reader.ReadInt32();
                int nextOamCyclePhase = reader.ReadInt32();
                int nextPendingCpuStallDots = reader.ReadInt32();
                StatePayload.RequireRange(nextSourceAddress, 0, 0xFFF0, nameof(SourceAddress));
                StatePayload.RequireRange(nextDestinationAddress, 0, 0x1FF0, nameof(DestinationAddress));
                StatePayload.RequireRange(nextRemainingBlocks, 0, 0x80, nameof(remainingBlocks));
                if ((nextTransferActive || nextGeneralTransferActive) &&
                    (nextTransferCancelled || nextRemainingBlocks == 0)) {
                    throw new InvalidOperationException("HDMA state contains contradictory transfer flags.");
                }
                if (nextTransferActive && nextGeneralTransferActive) {
                    throw new InvalidOperationException("Both VRAM DMA modes cannot be active together.");
                }
                StatePayload.RequireRange(nextBlockByteIndex, 0, 0x0F, nameof(blockByteIndex));
                StatePayload.RequireRange(nextBlockDotPhase, 0, 1, nameof(blockDotPhase));
                StatePayload.RequireRange(nextOamSourceAddress, 0, 0xFF00, nameof(oamSourceAddress));
                StatePayload.RequireRange(nextOamStartDelay, 0, 8, nameof(oamStartDelay));
                StatePayload.RequireRange(nextOamByteIndex, 0, 0xA0, nameof(oamByteIndex));
                StatePayload.RequireRange(nextOamCyclePhase, 0, 3, nameof(oamCyclePhase));
                StatePayload.RequireRange(nextPendingCpuStallDots, 0, 0x1000, nameof(pendingCpuStallDots));
                if (nextBlockTransferActive != (nextPendingCpuStallDots > 0) ||
                    (nextBlockTransferActive && !nextTransferActive && !nextGeneralTransferActive) ||
                    (nextGeneralTransferActive && !nextBlockTransferActive) ||
                    (!nextBlockTransferActive && (nextBlockByteIndex != 0 || nextBlockDotPhase != 0))) {
                    throw new InvalidDataException("VRAM DMA state has contradictory progress flags.");
                }
                if ((nextOamTransferActive && nextOamByteIndex >= 0xA0) ||
                    (!nextOamTransferActive && nextOamByteIndex != 0 && nextOamByteIndex != 0xA0) ||
                    (!nextOamTransferActive && nextOamCyclePhase != 0) ||
                    (nextOamStartPending != (nextOamStartDelay > 0))) {
                    throw new InvalidDataException("OAM DMA state has contradictory progress flags.");
                }

                return (Action)(() => {
                    SourceAddress = nextSourceAddress;
                    DestinationAddress = nextDestinationAddress;
                    remainingBlocks = nextRemainingBlocks;
                    transferActive = nextTransferActive;
                    generalTransferActive = nextGeneralTransferActive;
                    transferCancelled = nextTransferCancelled;
                    blockTransferActive = nextBlockTransferActive;
                    blockByteIndex = nextBlockByteIndex;
                    blockDotPhase = nextBlockDotPhase;
                    oamTransferActive = nextOamTransferActive;
                    oamStartPending = nextOamStartPending;
                    oamSourceAddress = nextOamSourceAddress;
                    oamSourceRegister = nextOamSourceRegister;
                    oamStartDelay = nextOamStartDelay;
                    oamByteIndex = nextOamByteIndex;
                    oamCyclePhase = nextOamCyclePhase;
                    pendingCpuStallDots = nextPendingCpuStallDots;
                });
            });
        }
    }
}
