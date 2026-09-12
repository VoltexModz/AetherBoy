using System;
using System.IO;
using System.Security.Cryptography;
using nanoboy.Core.Audio;

namespace nanoboy.Core
{
    public sealed class Memory : IMemoryDevice, IDisposable
    {
        public Interrupt Interrupt;
        public Video Video;
        public Audio.Audio Audio;
        public Joypad Joypad;
        public Timer Timer;
        private CPU cpu;
        private HDMA hdma;
        private IMemoryDevice mbc;
        private ROM rom;
        private byte[,] wram;
        private int wrambank;
        private byte[] hram;
        private ISerialDevice serial;
        private byte serialData;
        private byte serialControl;
        private byte serialIncomingData;
        private int serialClock;
        private int serialBitsRemaining;
        private bool serialTransferActive;
        public byte[] BootROM;
        public bool BootROMEnabled;

        public ROM ROM => rom;
        public IMemoryDevice MBC => mbc;
        public HDMA HDMA => hdma;
        internal bool CpuIsHalted => cpu.WaitForInterrupt;
        internal bool HasColorHardware => rom.HasColorFeatures;
        public int WRAMBank { get => wrambank; set => wrambank = value; }
        public byte ReadWRAMDirect(int bank, int offset) => wram[bank, offset];
        public void WriteWRAMDirect(int bank, int offset, byte value) => wram[bank, offset] = value;
        public byte ReadHRAMDirect(int offset) => hram[offset];
        public void WriteHRAMDirect(int offset, byte value) => hram[offset] = value;

        public Memory(CPU cpu, ROM rom)
        {
            this.cpu = cpu;
            this.mbc = rom.MBC;
            this.rom = rom;
            hdma = new HDMA(this);
            wram = new byte[8, 0x1000];
            wrambank = 1;
            hram = new byte[0x7F];
            Audio = new Audio.Audio(dmgMode: !rom.HasColorFeatures);
            serial = new SerialConsole();
            serialData = 0;
            serialControl = 0;
            serialIncomingData = 0xFF;
            serialClock = 0;
            serialBitsRemaining = 0;
            serialTransferActive = false;
            Interrupt = new Interrupt(cpu);
            Video = new Video(Interrupt, hdma, rom.HasColorFeatures);
            Joypad = new Joypad(Interrupt, cpu.WakeFromStop);
            Timer = new Timer(Interrupt);
        }

        public byte ReadByte(int address)
        {
            if (hdma.IsCpuBusBlocked(address)) {
                return 0xFF;
            }

            if (BootROMEnabled && BootROM != null) {
                if (address < 0x0100) {
                    return BootROM[address];
                }
                if (BootROM.Length > 0x0100 && address >= 0x0200 && address < 0x0900) {
                    return BootROM[address];
                }
            }
            if (address <= 0x7FFF) {
                return mbc.ReadByte(address);
            } else if (address <= 0x9FFF) {
                return Video.ReadVRAM(address - 0x8000);
            } else if (address <= 0xBFFF) {
                return mbc.ReadByte(address);
            } else if (address <= 0xCFFF) {
                return wram[0, address - 0xC000];
            } else if (address <= 0xDFFF) {
                return wram[wrambank, address - 0xD000];
            } else if (address <= 0xEFFF) {
                return wram[0, address - 0xE000];
            } else if (address <= 0xFDFF) {
                return wram[wrambank, address - 0xF000];
            } else if (address <= 0xFE9F) {
                return Video.ReadOAM(address - 0xFE00);
            } else if (address <= 0xFEFF) {

                return 0;
            } else if (address <= 0xFF7F) {
                int value = 0;
                switch (address - 0xFF00)
                {
                    case 0x00:
                        return Joypad.ReadRegister();
                    case 0x01:
                        return serialData;
                    case 0x02:
                        return (byte)((rom.HasColorFeatures ? 0x7C : 0x7E) |
                            serialControl |
                            (serialTransferActive ? 0x80 : 0x00));
                    case 0x04:
                        return (byte)Timer.DIV;
                    case 0x05:
                        return (byte)Timer.TIMA;
                    case 0x06:
                        return (byte)Timer.TMA;
                    case 0x07:
                        return (byte)Timer.TAC;
                    case 0x0F:
                        return (byte)(0xE0 | (Interrupt.IF & 0x1F));
                    case 0x10:
                        value = Audio.Channel1.SweepShift |
                                ((int)Audio.Channel1.SweepDirection << 3) |
                                (Audio.Channel1.SweepTime << 4);
                        return (byte)(0x80 | value);
                    case 0x11:
                        value = Audio.Channel1.SoundLengthRaw |
                                (Audio.Channel1.WavePatternDuty << 6);
                        return (byte)(0x3F | value);
                    case 0x12:
                        value = Audio.Channel1.EnvelopeSweep |
                                ((int)Audio.Channel1.EnvelopeDirection << 3) |
                                (Audio.Channel1.Volume << 4);
                        return (byte)value;
                    case 0x14:
                        return (byte)(0xBF | (Audio.Channel1.StopOnLengthExpired ? 0x40 : 0x00));
                    case 0x13:
                    case 0x15:
                        return 0xFF;
                    case 0x16:
                        value = Audio.Channel2.SoundLengthRaw |
                                (Audio.Channel2.WavePatternDuty << 6);
                        return (byte)(0x3F | value);
                    case 0x17:
                        value = Audio.Channel2.EnvelopeSweep |
                                ((int)Audio.Channel2.EnvelopeDirection << 3) |
                                (Audio.Channel2.Volume << 4);
                        return (byte)value;
                    case 0x19:
                        return (byte)(0xBF | (Audio.Channel2.StopOnLengthExpired ? 0x40 : 0x00));
                    case 0x18:
                        return 0xFF;
                    case 0x1A:
                        return (byte)(0x7F | (Audio.Channel3.On ? 0x80 : 0x00));
                    case 0x1B:
                        return 0xFF;
                    case 0x1C:
                        return (byte)(0x9F | (Audio.Channel3.OutputLevel << 5));
                    case 0x1E:
                        return (byte)(0xBF | (Audio.Channel3.StopOnLengthExpired ? 0x40 : 0x00));
                    case 0x1D:
                    case 0x1F:
                        return 0xFF;
                    case 0x20:
                        return 0xFF;
                    case 0x21:
                        value = Audio.Channel4.EnvelopeSweep |
                                ((int)Audio.Channel4.EnvelopeDirection << 3) |
                                (Audio.Channel4.Volume << 4);
                        return (byte)value;
                    case 0x22:
                        value = Audio.Channel4.DividingRatio |
                                (Audio.Channel4.CounterStep ? 0x08 : 0x00) |
                                (Audio.Channel4.ClockFrequency << 4);
                        return (byte)value;
                    case 0x23:
                        return (byte)(0xBF |
                            (Audio.Channel4.StopOnLengthExpired ? 0x40 : 0x00));
                    case 0x24:
                        return Audio.MasterVolume;
                    case 0x25:
                        return Audio.OutputRouting;
                    case 0x26:
                        return Audio.ReadStatus();
                    case 0x27:
                    case 0x28:
                    case 0x29:
                    case 0x2A:
                    case 0x2B:
                    case 0x2C:
                    case 0x2D:
                    case 0x2E:
                    case 0x2F:
                        return 0xFF;
                    case 0x30:
                    case 0x31:
                    case 0x32:
                    case 0x33:
                    case 0x34:
                    case 0x35:
                    case 0x36:
                    case 0x37:
                    case 0x38:
                    case 0x39:
                    case 0x3A:
                    case 0x3B:
                    case 0x3C:
                    case 0x3D:
                    case 0x3E:
                    case 0x3F:
                        return Audio.Channel3.ReadWaveRam(address);
                    case 0x40:
                        value = Video.LCDEnable ? 0x80 : 0x0;
                        value += Video.WindowTileMapSelect ? 0x40 : 0x0;
                        value += Video.WindowEnable ? 0x20 : 0x0;
                        value += Video.TileDataSelect ? 0x10 : 0x0;
                        value += Video.BackgroundTileMapSelect ? 0x08 : 0x0;
                        value += Video.ObjectSize ? 0x04 : 0x0;
                        value += Video.ObjectEnable ? 0x02 : 0x0;
                        value += Video.BackgroundEnable ? 0x01 : 0x0;
                        return (byte)value;
                    case 0x41:
                        return Video.ReadStat();
                    case 0x42:
                        return (byte)Video.SCY;
                    case 0x43:
                        return (byte)Video.SCX;
                    case 0x44:
                        return (byte)Video.LY;
                    case 0x45:
                        return (byte)Video.LYC;
                    case 0x46:
                        return hdma.OamSourceRegister;
                    case 0x47:
                        return (byte)Video.BGP;
                    case 0x48:
                        return (byte)Video.OBP0;
                    case 0x49:
                        return (byte)Video.OBP1;
                    case 0x4A:
                        return (byte)Video.WY;
                    case 0x4B:
                        return (byte)Video.WX;
                    case 0x4D:
                        if (!rom.HasColorFeatures) {
                            return 0xFF;
                        }
                        value = 0x7E | (cpu.IsDoubleSpeed ? 0x80 : 0x00);
                        value |= cpu.PrepareSpeedSwitch ? 1 : 0;
                        return (byte)value;
                    case 0x4F:
                        return rom.HasColorFeatures ? (byte)(0xFE | Video.VRAMBank) : (byte)0xFF;
                    case 0x51:
                    case 0x52:
                    case 0x53:
                    case 0x54:
                        return 0xFF;
                    case 0x55:
                        return rom.HasColorFeatures ? hdma.ReadControl() : (byte)0xFF;
                    case 0x68:
                        if (!rom.HasColorFeatures) {
                            return 0xFF;
                        }
                        value = Video.BackgroundPaletteIndex;
                        value |= Video.BackgroundPaletteAI ? 0x80 : 0x00;
                        return (byte)value;
                    case 0x69:
                        return rom.HasColorFeatures ? Video.ReadPRAM(0) : (byte)0xFF;
                    case 0x6A:
                        if (!rom.HasColorFeatures) {
                            return 0xFF;
                        }
                        value = Video.ObjectPaletteIndex;
                        value |= Video.ObjectPaletteAI ? 0x80 : 0x00;
                        return (byte)value;
                    case 0x6B:
                        return rom.HasColorFeatures ? Video.ReadPRAM(1) : (byte)0xFF;
                    case 0x70:
                        return rom.HasColorFeatures ? (byte)(0xF8 | wrambank) : (byte)0xFF;
                    default:

                        return 0;
                }
            } else if (address <= 0xFFFE) {
                return hram[address - 0xFF80];
            } else {
                return (byte)Interrupt.IE;
            }
        }

        internal byte ReadByteForDma(int address)
        {
            if (BootROMEnabled && BootROM != null) {
                if (address < 0x0100) {
                    return BootROM[address];
                }
                if (BootROM.Length > 0x0100 && address >= 0x0200 && address < 0x0900) {
                    return BootROM[address];
                }
            }
            if (address <= 0x7FFF) {
                return mbc.ReadByte(address);
            }
            if (address <= 0x9FFF) {
                return Video.ReadVRAMDirect(Video.VRAMBank, address - 0x8000);
            }
            if (address <= 0xBFFF) {
                return mbc.ReadByte(address);
            }
            if (address <= 0xCFFF) {
                return wram[0, address - 0xC000];
            }
            if (address <= 0xDFFF) {
                return wram[wrambank, address - 0xD000];
            }
            if (address <= 0xEFFF) {
                return wram[0, address - 0xE000];
            }
            if (address <= 0xFDFF) {
                return wram[wrambank, address - 0xF000];
            }

            return ReadByte(address);
        }

        public void WriteByte(int address, byte value)
        {
            if (hdma.IsCpuBusBlocked(address)) {
                return;
            }

            if (address <= 0x7FFF) {
                mbc.WriteByte(address, value);
            } else if (address <= 0x9FFF) {
                Video.WriteVRAM(address - 0x8000, value);
            } else if (address <= 0xBFFF) {
                mbc.WriteByte(address, value);
            } else if (address <= 0xCFFF) {
                wram[0, address - 0xC000] = value;
            } else if (address <= 0xDFFF) {
                wram[wrambank, address - 0xD000] = value;
            } else if (address <= 0xEFFF) {
                wram[0, address - 0xE000] = value;
            } else if (address <= 0xFDFF) {
                wram[wrambank, address - 0xF000] = value;
            } else if (address <= 0xFE9F) {
                Video.WriteOAM(address - 0xFE00, value);
            } else if (address <= 0xFEFF) {

            } else if (address <= 0xFF7F) {
                int ioOffset = address - 0xFF00;
                bool dmgLengthWriteWhilePoweredOff =
                    !rom.HasColorFeatures &&
                    (ioOffset == 0x11 || ioOffset == 0x16 || ioOffset == 0x1B || ioOffset == 0x20);
                if (ioOffset >= 0x10 && ioOffset <= 0x25 && !Audio.Powered &&
                    !dmgLengthWriteWhilePoweredOff) {
                    return;
                }
                switch (ioOffset) {
                    case 0x00:
                        Joypad.WriteSelection(value);
                        break;
                    case 0x01:
                        serialData = value;
                        if (serialTransferActive && serial is ISerialScheduledDevice scheduledData) {
                            scheduledData.SerialDataWritten();
                        }
                        break;
                    case 0x02:
                        serialControl = (byte)(value & (rom.HasColorFeatures ? 0x03 : 0x01));
                        if ((value & 0x80) == 0x80) {
                            StartSerialTransfer();
                        } else if (serialTransferActive) {
                            AbortSerialTransfer();
                        }
                        break;
                    case 0x04:
                        Audio.ResetFrameSequencerDivider(Timer.WriteDiv(cpu.IsDoubleSpeed));
                        break;
                    case 0x05:
                        Timer.WriteTima(value);
                        break;
                    case 0x06:
                        Timer.WriteTma(value);
                        break;
                    case 0x07:
                        Timer.WriteTac(value);
                        break;
                    case 0x0F:
                        Interrupt.IF = value & 0x1F;
                        break;
                    case 0x10:
                        Audio.Channel1.SweepShift = value & 7;
                        Audio.Channel1.SweepDirection = (SweepMode)((value >> 3) & 1);
                        Audio.Channel1.SweepTime = (value >> 4) & 7;
                        break;
                    case 0x11:
                        Audio.Channel1.SoundLengthRaw = value & 0x3F;
                        if (Audio.Powered) {
                            Audio.Channel1.WavePatternDuty = (value >> 6) & 3;
                        }
                        break;
                    case 0x12:
                        Audio.Channel1.EnvelopeSweep = value & 7;
                        Audio.Channel1.EnvelopeDirection = (EnvelopeMode)((value >> 3) & 1);
                        Audio.Channel1.Volume = (value >> 4) & 0xF;
                        Audio.Channel1.ApplyDacState();
                        break;
                    case 0x13:
                        Audio.Channel1.Frequency = (Audio.Channel1.Frequency & 0x700) | value;
                        break;
                    case 0x14:
                        Audio.Channel1.Frequency = (Audio.Channel1.Frequency & 0xFF) | ((value & 7) << 8);
                        Audio.Channel1.WriteControl(
                            (value & 0x40) != 0,
                            (value & 0x80) != 0,
                            Audio.ShouldClockLengthOnWrite);
                        break;
                    case 0x16:
                        Audio.Channel2.SoundLengthRaw = value & 0x3F;
                        if (Audio.Powered) {
                            Audio.Channel2.WavePatternDuty = (value >> 6) & 3;
                        }
                        break;
                    case 0x17:
                        Audio.Channel2.EnvelopeSweep = value & 7;
                        Audio.Channel2.EnvelopeDirection = (EnvelopeMode)((value >> 3) & 1);
                        Audio.Channel2.Volume = (value >> 4) & 0xF;
                        Audio.Channel2.ApplyDacState();
                        break;
                    case 0x18:
                        Audio.Channel2.Frequency = (Audio.Channel2.Frequency & 0x700) | value;
                        break;
                    case 0x19:
                        Audio.Channel2.Frequency = (Audio.Channel2.Frequency & 0xFF) | ((value & 7) << 8);
                        Audio.Channel2.WriteControl(
                            (value & 0x40) != 0,
                            (value & 0x80) != 0,
                            Audio.ShouldClockLengthOnWrite);
                        break;
                    case 0x1A:
                        Audio.Channel3.On = (value & 0x80) == 0x80;
                        Audio.Channel3.ApplyDacState();
                        break;
                    case 0x1B:
                        Audio.Channel3.SoundLengthRaw = value;
                        break;
                    case 0x1c:
                        Audio.Channel3.OutputLevel = (value >> 5) & 3;
                        break;
                    case 0x1D:
                        Audio.Channel3.FrequencyRaw = (Audio.Channel3.FrequencyRaw & 0x700) | value;
                        break;
                    case 0x1E:
                        Audio.Channel3.FrequencyRaw = (Audio.Channel3.FrequencyRaw & 0xFF) | ((value & 7) << 8);
                        Audio.Channel3.WriteControl(
                            (value & 0x40) != 0,
                            (value & 0x80) != 0,
                            Audio.ShouldClockLengthOnWrite);
                        break;
                    case 0x20:
                        Audio.Channel4.SoundLengthRaw = value & 0x3F;
                        break;
                    case 0x21:
                        Audio.Channel4.EnvelopeSweep = value & 7;
                        Audio.Channel4.EnvelopeDirection = (EnvelopeMode)((value >> 3) & 1);
                        Audio.Channel4.Volume = (value >> 4) & 0xF;
                        Audio.Channel4.ApplyDacState();
                        break;
                    case 0x22:
                        Audio.Channel4.ClockFrequency = value >> 4;
                        Audio.Channel4.CounterStep = (value & 8) == 8;
                        Audio.Channel4.Counter = Audio.Channel4.CounterStep ? 0x7F : 0x7FFF;
                        Audio.Channel4.DividingRatio = value & 7;
                        break;
                    case 0x23:
                        Audio.Channel4.WriteControl(
                            (value & 0x40) != 0,
                            (value & 0x80) != 0,
                            Audio.ShouldClockLengthOnWrite);
                        break;
                    case 0x24:
                        Audio.MasterVolume = value;
                        break;
                    case 0x25:
                        Audio.OutputRouting = value;
                        break;
                    case 0x26:
                        Audio.SetPower(
                            (value & 0x80) != 0,
                            Timer.ApuDividerHigh(cpu.IsDoubleSpeed));
                        break;

                    case 0x30:
                    case 0x31:
                    case 0x32:
                    case 0x33:
                    case 0x34:
                    case 0x35:
                    case 0x36:
                    case 0x37:
                    case 0x38:
                    case 0x39:
                    case 0x3A:
                    case 0x3B:
                    case 0x3C:
                    case 0x3D:
                    case 0x3E:
                    case 0x3F:
                        Audio.Channel3.WriteWaveRam(address, value);
                        break;
                    case 0x40:
                        Video.WriteLcdc(value);
                        break;
                    case 0x41:
                        Video.WriteStat(value);
                        break;
                    case 0x42:
                        Video.SCY = value;
                        break;
                    case 0x43:
                        Video.SCX = value;
                        break;
                    case 0x45:
                        Video.WriteLyc(value);
                        break;
                    case 0x46:
                        hdma.StartOamDma(value);
                        break;
                    case 0x47:
                        Video.BGP = value;
                        break;
                    case 0x48:
                        Video.OBP0 = value;
                        break;
                    case 0x49:
                        Video.OBP1 = value;
                        break;
                    case 0x4A:
                        Video.WY = value;
                        break;
                    case 0x4B:
                        Video.WX = value;
                        break;
                    case 0x4D:
                        if (rom.HasColorFeatures) {
                            cpu.PrepareSpeedSwitch = (value & 1) == 1;
                        }
                        break;
                    case 0x4F:
                        if (rom.HasColorFeatures) {
                            Video.VRAMBank = value & 1;
                        }
                        break;
                    case 0x50:
                        if (value != 0) {
                            BootROMEnabled = false;
                        }
                        break;
                    case 0x51:
                        if (rom.HasColorFeatures) {
                            hdma.SourceAddress = hdma.SourceAddress & 0xFF | (value << 8);
                        }
                        break;
                    case 0x52:
                        if (rom.HasColorFeatures) {
                            hdma.SourceAddress = hdma.SourceAddress & 0xFF00 | (value & 0xF0);
                        }
                        break;
                    case 0x53:
                        if (rom.HasColorFeatures) {
                            hdma.DestinationAddress = hdma.DestinationAddress & 0xFF | ((value & 0x1F) << 8);
                        }
                        break;
                    case 0x54:
                        if (rom.HasColorFeatures) {
                            hdma.DestinationAddress = hdma.DestinationAddress & 0xFF00 | (value & 0xF0);
                        }
                        break;
                    case 0x55:
                        if (rom.HasColorFeatures) {
                            hdma.WriteControl(value);
                        }
                        break;
                    case 0x68:
                        if (rom.HasColorFeatures) {
                            Video.BackgroundPaletteIndex = value & 0x3F;
                            Video.BackgroundPaletteAI = (value & 0x80) == 0x80;
                        }
                        break;
                    case 0x69:
                        if (rom.HasColorFeatures) {
                            Video.WritePRAM(0, value);
                        }
                        break;
                    case 0x6A:
                        if (rom.HasColorFeatures) {
                            Video.ObjectPaletteIndex = value & 0x3F;
                            Video.ObjectPaletteAI = (value & 0x80) == 0x80;
                        }
                        break;
                    case 0x6B:
                        if (rom.HasColorFeatures) {
                            Video.WritePRAM(1, value);
                        }
                        break;
                    case 0x70:
                        if (rom.HasColorFeatures) {
                            if (value == 0) {
                                value = 1;
                            }
                            wrambank = value & 7;
                        }
                        break;
                    default:

                        break;
                }
            } else if (address <= 0xFFFE) {
                hram[address - 0xFF80] = value;
            } else {
                Interrupt.IE = value;
            }
        }

        internal void TickSerial()
        {
            if (serial is ISerialScheduledDevice scheduled) {
                scheduled.TickSerialCycle();
                return;
            }
            if (!serialTransferActive || (serialControl & 0x01) == 0) {
                return;
            }

            if (serial is ISerialClockArbiter arbiter && !arbiter.CanDriveClock) {
                return;
            }

            int clockPeriod = (rom.HasColorFeatures && (serialControl & 0x02) != 0) ? 16 : 512;
            serialClock++;
            if (serialClock < clockPeriod) {
                return;
            }

            serialClock = 0;
            AdvanceSerialBit();
        }

        /// <summary>
        /// Supplies one externally clocked serial bit. Returns false unless an
        /// external-clock transfer is active; otherwise returns the outgoing bit.
        /// </summary>
        public bool TryClockSerialBit(bool incomingBit, out bool outgoingBit)
        {
            return TryClockSerialBit(incomingBit, allowInternalClock: false, out outgoingBit);
        }

        internal ISerialDevice SerialDevice => serial;
        internal bool HasInternalSerialClock => serialTransferActive && (serialControl & 0x01) != 0;
        internal byte SerialData => serialData;
        internal byte SerialControl => serialControl;
        internal int SerialBitsRemaining => serialBitsRemaining;
        internal bool SerialCpuDoubleSpeed => cpu.IsDoubleSpeed;
        internal int SerialClockPeriodDots =>
            ((rom.HasColorFeatures && (serialControl & 2) != 0) ? 16 : 512) /
            (cpu.IsDoubleSpeed ? 2 : 1);

        // Abort on a network fault without inventing a completed transfer/IRQ.
        internal void AbortLinkedSerialTransfer()
        {
            if (serialTransferActive) {
                AbortSerialTransfer();
            }
        }

        // Only the coordinated local cable may override an internally selected
        // clock. Its arbiter suppresses that peer's own TickSerial clock source.
        internal bool TryClockLinkedSerialBit(bool incomingBit, out bool outgoingBit)
        {
            return TryClockSerialBit(incomingBit, allowInternalClock: true, out outgoingBit);
        }

        private bool TryClockSerialBit(bool incomingBit, bool allowInternalClock, out bool outgoingBit)
        {
            outgoingBit = false;
            if (!serialTransferActive || (!allowInternalClock && (serialControl & 0x01) != 0)) {
                return false;
            }

            outgoingBit = (serialData & 0x80) != 0;
            serialClock = 0;
            AdvanceSerialBit(incomingBit);
            return true;
        }

        internal void ResetSerial()
        {
            if (serialTransferActive) {
                serial.Stop();
            }

            serialData = 0;
            serialControl = 0;
            serialIncomingData = 0xFF;
            serialClock = 0;
            serialBitsRemaining = 0;
            serialTransferActive = false;
        }

        private void StartSerialTransfer()
        {
            if (serialTransferActive) {
                serial.Stop();
            }

            serialClock = 0;
            serialBitsRemaining = 8;
            serialTransferActive = true;
            serial.Write(serialData);
            serial.Start();
            serialIncomingData = serial is ISerialBitDevice ? (byte)0xFF : serial.Read();
        }

        private void AbortSerialTransfer()
        {
            serial.Stop();
            serialClock = 0;
            serialBitsRemaining = 0;
            serialTransferActive = false;
        }

        private void AdvanceSerialBit(bool? suppliedIncomingBit = null)
        {
            bool outgoingBit = (serialData & 0x80) != 0;
            bool incomingBit;
            if (suppliedIncomingBit.HasValue) {
                incomingBit = suppliedIncomingBit.Value;
            } else if (serial is ISerialBitDevice bitDevice) {
                incomingBit = bitDevice.ExchangeBit(outgoingBit);
            } else {
                incomingBit = (serialIncomingData & 0x80) != 0;
                serialIncomingData <<= 1;
            }

            serialData = (byte)((serialData << 1) | (incomingBit ? 1 : 0));
            serialBitsRemaining--;
            if (serialBitsRemaining > 0) {
                return;
            }

            serial.Stop();
            serialClock = 0;
            serialTransferActive = false;
            Interrupt.Request(8);
        }

        public void Dispose()
        {
            try {
                try {
                    serial.Stop();
                } finally {
                    if (mbc is IDisposable disposableMapper) {
                        disposableMapper.Dispose();
                    }
                }
            } finally {
                Audio.Dispose();
            }
        }

        internal byte[] CaptureStatePayload()
        {
            if (BootROMEnabled && (BootROM == null || BootROM.Length == 0)) {
                throw new InvalidOperationException("Boot ROM is enabled but no boot image is attached.");
            }

            return StatePayload.Write(writer => {
                writer.Write(BootROMEnabled);
                if (BootROMEnabled) {
                    writer.Write(BootROM.Length);
                    writer.Write(SHA256.HashData(BootROM));
                } else {
                    writer.Write(0);
                }
                writer.Write((byte)wrambank);
                for (int bank = 0; bank < 8; bank++) {
                    for (int offset = 0; offset < 0x1000; offset++) {
                        writer.Write(wram[bank, offset]);
                    }
                }
                writer.Write(hram);
                writer.Write(serialData);
                writer.Write(serialControl);
                writer.Write(serialIncomingData);
                writer.Write(serialClock);
                writer.Write(serialBitsRemaining);
                writer.Write(serialTransferActive);
            });
        }

        internal Action PrepareStateRestore(byte[] payload)
        {
            return StatePayload.Read(payload, reader => {
                bool nextBootRomEnabled = StatePayload.ReadBoolean(reader);
                int expectedBootRomLength = reader.ReadInt32();
                if (nextBootRomEnabled) {
                    if (expectedBootRomLength <= 0 || BootROM == null || BootROM.Length != expectedBootRomLength) {
                        throw new InvalidDataException("State requires a different boot ROM image.");
                    }
                    byte[] expectedBootRomHash = StatePayload.ReadBytes(reader, 32, "BootROM SHA-256");
                    if (!CryptographicOperations.FixedTimeEquals(
                        expectedBootRomHash,
                        SHA256.HashData(BootROM))) {
                        throw new InvalidDataException("State requires a different boot ROM image.");
                    }
                } else if (expectedBootRomLength != 0) {
                    throw new InvalidDataException("Disabled boot ROM state contains an invalid binding.");
                }

                int nextWramBank = reader.ReadByte();
                StatePayload.RequireRange(nextWramBank, 1, 7, nameof(wrambank));
                byte[] nextWram = StatePayload.ReadBytes(reader, 8 * 0x1000, "WRAM");
                byte[] nextHram = StatePayload.ReadBytes(reader, 0x7F, "HRAM");
                int nextSerialData = reader.ReadByte();
                int nextSerialControl = reader.ReadByte();
                StatePayload.RequireRange(nextSerialControl, 0, 3, nameof(serialControl));
                int nextSerialIncomingData = reader.ReadByte();
                int nextSerialClock = reader.ReadInt32();
                StatePayload.RequireRange(nextSerialClock, 0, 511, nameof(serialClock));
                int nextSerialBitsRemaining = reader.ReadInt32();
                StatePayload.RequireRange(nextSerialBitsRemaining, 0, 8, nameof(serialBitsRemaining));
                bool nextSerialTransferActive = StatePayload.ReadBoolean(reader);
                if (nextSerialTransferActive != (nextSerialBitsRemaining > 0)) {
                    throw new InvalidDataException("Serial transfer activity and remaining bit count disagree.");
                }
                if (!nextSerialTransferActive && nextSerialClock != 0) {
                    throw new InvalidDataException("An inactive serial transfer cannot retain a clock phase.");
                }

                return (Action)(() => {
                    BootROMEnabled = nextBootRomEnabled;
                    wrambank = nextWramBank;
                    int position = 0;
                    for (int bank = 0; bank < 8; bank++) {
                        for (int offset = 0; offset < 0x1000; offset++) {
                            wram[bank, offset] = nextWram[position++];
                        }
                    }
                    Array.Copy(nextHram, hram, hram.Length);
                    serialData = (byte)nextSerialData;
                    serialControl = (byte)nextSerialControl;
                    serialIncomingData = (byte)nextSerialIncomingData;
                    serialClock = nextSerialClock;
                    serialBitsRemaining = nextSerialBitsRemaining;
                    serialTransferActive = nextSerialTransferActive;
                });
            });
        }

        public void AttachSerialDevice(ISerialDevice device)
        {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            ResetSerial();
            serial = device;
        }

        internal byte[] CaptureSerialStatePayload()
        {
            if (serial is not SerialConsole) {
                throw new NotSupportedException(
                    $"Serial device {serial.GetType().Name} does not expose deterministic state.");
            }
            return Array.Empty<byte>();
        }

        internal Action PrepareSerialStateRestore(byte[] payload)
        {
            if (serial is not SerialConsole) {
                throw new NotSupportedException(
                    $"Serial device {serial.GetType().Name} does not expose deterministic state.");
            }
            if (payload == null || payload.Length != 0) {
                throw new InvalidDataException("SerialConsole state must be empty.");
            }
            return () => { };
        }
    }
}
