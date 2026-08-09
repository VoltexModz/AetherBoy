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
        private byte serialControl;
        public byte[] BootROM;
        public bool BootROMEnabled;

        public ROM ROM => rom;
        public IMemoryDevice MBC => mbc;
        public HDMA HDMA => hdma;
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
            Audio = new Audio.Audio();
            serial = new SerialConsole();
            serialControl = 0;
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
                        return serial.Read();
                    case 0x02:
                        return (byte)((rom.HasColorFeatures ? 0x7C : 0x7E) | serialControl);
                    case 0x04:
                        return (byte)Timer.DIV;
                    case 0x05:
                        return (byte)Timer.TIMA;
                    case 0x06:
                        return (byte)Timer.TMA;
                    case 0x07:
                        return (byte)Timer.TAC;
                    case 0x0F:
                        return (byte)Interrupt.IF;
                    case 0x10:
                        value = Audio.Channel1.SweepShift |
                                ((int)Audio.Channel1.SweepDirection << 3) |
                                (Audio.Channel1.SweepTime << 4);
                        return (byte)value;
                    case 0x11:
                        value = Audio.Channel1.SoundLengthRaw |
                                (Audio.Channel1.WavePatternDuty << 6);
                        return (byte)value;
                    case 0x12:
                        value = Audio.Channel1.EnvelopeSweep |
                                ((int)Audio.Channel1.EnvelopeDirection << 3) |
                                (Audio.Channel1.Volume << 4);
                        return (byte)value;
                    case 0x14:
                        return (byte)(Audio.Channel1.StopOnLengthExpired ? 0x40 : 0x00);
                    case 0x16:
                        value = Audio.Channel2.SoundLengthRaw |
                                (Audio.Channel2.WavePatternDuty << 6);
                        return (byte)value;
                    case 0x17:
                        value = Audio.Channel2.EnvelopeSweep |
                                ((int)Audio.Channel2.EnvelopeDirection << 3) |
                                (Audio.Channel2.Volume << 4);
                        return (byte)value;
                    case 0x19:
                        return (byte)(Audio.Channel2.StopOnLengthExpired ? 0x40 : 0x00);
                    case 0x1A:
                        return (byte)(Audio.Channel3.On ? 0x80 : 0x00);
                    case 0x1B:
                        return (byte)Audio.Channel3.SoundLengthRaw;
                    case 0x1C:
                        return (byte)(Audio.Channel3.OutputLevel << 5);
                    case 0x1E:
                        return (byte)(Audio.Channel3.StopOnLengthExpired ? 0x40 : 0x00);
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
                        value = Audio.Channel3.WaveRAM[(address & 0xF) * 2 + 1] |
                                (Audio.Channel3.WaveRAM[(address & 0xF) * 2] << 4);
                        return (byte)value;
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
                        return (byte)Video.VRAMBank;
                    case 0x51:
                        return (byte)(hdma.SourceAddress >> 8);
                    case 0x52:
                        return (byte)hdma.SourceAddress;
                    case 0x53:
                        return (byte)(hdma.DestinationAddress >> 8);
                    case 0x54:
                        return (byte)hdma.DestinationAddress;
                    case 0x55:
                        return hdma.ReadControl();
                    case 0x68:
                        value = Video.BackgroundPaletteIndex;
                        value |= Video.BackgroundPaletteAI ? 0x80 : 0x00;
                        return (byte)value;
                    case 0x69:
                        return Video.ReadPRAM(0);
                    case 0x6A:
                        value = Video.ObjectPaletteIndex;
                        value |= Video.ObjectPaletteAI ? 0x80 : 0x00;
                        return (byte)value;
                    case 0x6B:
                        return Video.ReadPRAM(1);
                    case 0x70:
                        return (byte)wrambank;
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
                switch (address - 0xFF00) {
                    case 0x00:
                        Joypad.WriteSelection(value);
                        break;
                    case 0x01:
                        serial.Write(value);
                        break;
                    case 0x02:
                        serialControl = (byte)(value & (rom.HasColorFeatures ? 0x03 : 0x01));
                        if ((value & 0x80) == 0x80) {
                            serial.Start();
                            Interrupt.Request(8);
                        }
                        break;
                    case 0x04:
                        Timer.WriteDiv();
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
                        Interrupt.IF = value;
                        break;
                    case 0x10:
                        Audio.Channel1.SweepShift = value & 7;
                        Audio.Channel1.SweepDirection = (SweepMode)((value >> 3) & 1);
                        Audio.Channel1.SweepTime = (value >> 4) & 7;
                        break;
                    case 0x11:
                        Audio.Channel1.SoundLengthRaw = value & 0x3F;
                        Audio.Channel1.WavePatternDuty = (value >> 6) & 3;
                        break;
                    case 0x12:
                        Audio.Channel1.EnvelopeSweep = value & 7;
                        Audio.Channel1.EnvelopeDirection = (EnvelopeMode)((value >> 3) & 1);
                        Audio.Channel1.Volume = (value >> 4) & 0xF;
                        break;
                    case 0x13:
                        Audio.Channel1.Frequency = (Audio.Channel1.Frequency & 0x700) | value;
                        break;
                    case 0x14:
                        Audio.Channel1.Frequency = (Audio.Channel1.Frequency & 0xFF) | ((value & 7) << 8);
                        Audio.Channel1.StopOnLengthExpired = (value & 0x40) == 0x40;
                        if ((value & 0x80) == 0x80) {
                            Audio.Channel1.Restart();
                        }
                        break;
                    case 0x16:
                        Audio.Channel2.SoundLengthRaw = value & 0x3F;
                        Audio.Channel2.WavePatternDuty = (value >> 6) & 3;
                        break;
                    case 0x17:
                        Audio.Channel2.EnvelopeSweep = value & 7;
                        Audio.Channel2.EnvelopeDirection = (EnvelopeMode)((value >> 3) & 1);
                        Audio.Channel2.Volume = (value >> 4) & 0xF;
                        break;
                    case 0x18:
                        Audio.Channel2.Frequency = (Audio.Channel2.Frequency & 0x700) | value;
                        break;
                    case 0x19:
                        Audio.Channel2.Frequency = (Audio.Channel2.Frequency & 0xFF) | ((value & 7) << 8);
                        Audio.Channel2.StopOnLengthExpired = (value & 0x40) == 0x40;
                        if ((value & 0x80) == 0x80) {
                            Audio.Channel2.Restart();
                        }
                        break;
                    case 0x1A:
                        Audio.Channel3.On = (value & 0x80) == 0x80;
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
                        Audio.Channel3.StopOnLengthExpired = (value & 0x40) == 0x40;
                        if ((value & 0x80) == 0x80) {
                            Audio.Channel3.Restart();
                        }
                        break;
                    case 0x20:
                        Audio.Channel4.SoundLengthRaw = value & 0x3F;
                        break;
                    case 0x21:
                        Audio.Channel4.EnvelopeSweep = value & 7;
                        Audio.Channel4.EnvelopeDirection = (EnvelopeMode)((value >> 3) & 1);
                        Audio.Channel4.Volume = (value >> 4) & 0xF;
                        break;
                    case 0x22:
                        Audio.Channel4.ClockFrequency = value >> 4;
                        Audio.Channel4.CounterStep = (value & 8) == 8;
                        Audio.Channel4.Counter = Audio.Channel4.CounterStep ? 0x7F : 0x7FFF;
                        Audio.Channel4.DividingRatio = value & 7;
                        break;
                    case 0x23:
                        Audio.Channel4.StopOnLengthExpired = (value & 0x40) == 0x40;
                        if ((value & 0x80) == 0x80) {
                            Audio.Channel4.Restart();
                        }
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
                        Audio.Channel3.WaveRAM[(address & 0xF) * 2] = (byte)(value >> 4);
                        Audio.Channel3.WaveRAM[(address & 0xF) * 2 + 1] = (byte)(value & 0xF);
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
                        hdma.SourceAddress = hdma.SourceAddress & 0xFF | (value << 8);
                        break;
                    case 0x52:
                        hdma.SourceAddress = hdma.SourceAddress & 0xFF00 | (value & 0xF0);
                        break;
                    case 0x53:
                        hdma.DestinationAddress = hdma.DestinationAddress & 0xFF | ((value & 0x1F) << 8);
                        break;
                    case 0x54:
                        hdma.DestinationAddress = hdma.DestinationAddress & 0xFF00 | (value & 0xF0);
                        break;
                    case 0x55:
                        hdma.WriteControl(value);
                        break;
                    case 0x68:
                        Video.BackgroundPaletteIndex = value & 0x3F;
                        Video.BackgroundPaletteAI = (value & 0x80) == 0x80;
                        break;
                    case 0x69:
                        Video.WritePRAM(0, value);
                        break;
                    case 0x6A:
                        Video.ObjectPaletteIndex = value & 0x3F;
                        Video.ObjectPaletteAI = (value & 0x80) == 0x80;
                        break;
                    case 0x6B:
                        Video.WritePRAM(1, value);
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
                writer.Write(serialControl);
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
                int nextSerialControl = reader.ReadByte();
                StatePayload.RequireRange(nextSerialControl, 0, 3, nameof(serialControl));

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
                    serialControl = (byte)nextSerialControl;
                });
            });
        }

        public void AttachSerialDevice(ISerialDevice device)
        {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            serial.Stop();
            serial = device;
            serialControl = 0;
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
