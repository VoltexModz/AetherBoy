using System;
using System.IO;

namespace nanoboy.Core
{
    public interface ICartridgeMapper : IMemoryDevice, IDisposable
    {
        Mbc CartridgeType { get; }
        CartridgeMapperState CaptureState();
        void ValidateState(CartridgeMapperState state);
        void RestoreState(CartridgeMapperState state);
        void FlushPersistentState();
    }

    public sealed class CartridgeMapperState
    {
        private readonly byte[] registers;
        private readonly byte[] ram;

        public const ushort CurrentFormatVersion = 1;

        public CartridgeMapperState(Mbc cartridgeType, byte[] registers, byte[] ram)
        {
            CartridgeType = cartridgeType;
            this.registers = registers == null
                ? throw new ArgumentNullException(nameof(registers))
                : (byte[])registers.Clone();
            this.ram = ram == null
                ? throw new ArgumentNullException(nameof(ram))
                : (byte[])ram.Clone();
        }

        public ushort FormatVersion => CurrentFormatVersion;
        public Mbc CartridgeType { get; }
        public int RegisterLength => registers.Length;
        public int RamLength => ram.Length;

        public byte[] CopyRegisters() => (byte[])registers.Clone();
        public byte[] CopyRam() => (byte[])ram.Clone();
    }

    internal sealed class CartridgeRam : IDisposable
    {
        private readonly byte[] data;
        private readonly string? saveFile;
        private bool dirty;
        private bool disposed;

        public CartridgeRam(int size, bool batteryBacked, string? saveFile)
        {
            if (size < 0) {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            data = new byte[size];
            this.saveFile = batteryBacked && !string.IsNullOrWhiteSpace(saveFile)
                ? saveFile
                : null;

            if (data.Length > 0 && this.saveFile != null && File.Exists(this.saveFile)) {
                byte[] savedData = File.ReadAllBytes(this.saveFile);
                Array.Copy(savedData, data, Math.Min(savedData.Length, data.Length));
            }
        }

        public int Length => data.Length;

        public byte Read(int index)
        {
            if (data.Length == 0) {
                return 0xFF;
            }

            return data[Normalize(index, data.Length)];
        }

        public void Write(int index, byte value)
        {
            if (data.Length == 0) {
                return;
            }

            int normalizedIndex = Normalize(index, data.Length);
            if (data[normalizedIndex] == value) {
                return;
            }

            data[normalizedIndex] = value;
            dirty = true;
        }

        public byte[] Capture() => (byte[])data.Clone();

        public void Restore(byte[] source)
        {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }
            if (source.Length != data.Length) {
                throw new InvalidDataException(
                    $"Cartridge RAM size mismatch: expected {data.Length}, got {source.Length}.");
            }

            Array.Copy(source, data, data.Length);
            dirty = true;
        }

        public void Flush()
        {
            if (!dirty || saveFile == null) {
                return;
            }

            string directory = Path.GetDirectoryName(saveFile);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }

            string temporaryFile = saveFile + ".tmp";
            File.WriteAllBytes(temporaryFile, data);
            File.Move(temporaryFile, saveFile, true);
            dirty = false;
        }

        public void Dispose()
        {
            if (disposed) {
                return;
            }

            Flush();
            disposed = true;
        }

        private static int Normalize(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }
    }

    public abstract class CartridgeMapper : ICartridgeMapper
    {
        private const int RomBankSize = 0x4000;
        private bool disposed;

        protected CartridgeMapper(
            byte[] romData,
            Mbc cartridgeType,
            int romSize,
            int ramSize,
            bool batteryBacked,
            string? saveFile)
        {
            if (romData == null) {
                throw new ArgumentNullException(nameof(romData));
            }
            if (romSize < RomBankSize || romSize % RomBankSize != 0) {
                throw new InvalidDataException($"Invalid cartridge ROM size: {romSize} bytes.");
            }
            if (romData.Length < romSize) {
                throw new InvalidDataException(
                    $"Truncated cartridge ROM: header declares {romSize} bytes, file contains {romData.Length}.");
            }

            Rom = new byte[romSize];
            Array.Copy(romData, Rom, romSize);
            CartridgeType = cartridgeType;
            Ram = new CartridgeRam(ramSize, batteryBacked, saveFile);
        }

        protected byte[] Rom { get; }
        private protected CartridgeRam Ram { get; }
        protected int RomBankCount => Rom.Length / RomBankSize;

        public Mbc CartridgeType { get; }

        public abstract byte ReadByte(int address);
        public abstract void WriteByte(int address, byte value);
        public abstract CartridgeMapperState CaptureState();
        public abstract void ValidateState(CartridgeMapperState state);
        public abstract void RestoreState(CartridgeMapperState state);

        public virtual void FlushPersistentState() => Ram.Flush();

        public void Dispose()
        {
            if (disposed) {
                return;
            }

            try {
                FlushPersistentState();
            } finally {
                Ram.Dispose();
                disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        protected byte ReadRom(int bank, int offset)
        {
            int normalizedBank = Normalize(bank, RomBankCount);
            return Rom[normalizedBank * RomBankSize + offset];
        }

        protected static void EnsureCartridgeAddress(int address)
        {
            if (address < 0 || address > 0xBFFF || (address > 0x7FFF && address < 0xA000)) {
                throw new ArgumentOutOfRangeException(nameof(address), address, "Not a cartridge address.");
            }
        }

        protected static void ValidateState(
            CartridgeMapperState state,
            Mbc cartridgeType,
            int registerLength,
            int ramLength)
        {
            if (state == null) {
                throw new ArgumentNullException(nameof(state));
            }
            if (state.CartridgeType != cartridgeType) {
                throw new InvalidDataException(
                    $"Mapper state is for {state.CartridgeType}, not {cartridgeType}.");
            }
            if (state.RegisterLength != registerLength || state.RamLength != ramLength) {
                throw new InvalidDataException("Mapper state layout does not match this cartridge.");
            }
        }

        protected static int Normalize(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }
    }

    public sealed class NoMBC : CartridgeMapper
    {
        public NoMBC(byte[] romData, Mbc cartridgeType, int romSize)
            : this(romData, cartridgeType, romSize, 0, false, null)
        {
        }

        public NoMBC(
            byte[] romData,
            Mbc cartridgeType,
            int romSize,
            int ramSize,
            bool batteryBacked,
            string? saveFile)
            : base(romData, cartridgeType, romSize, ramSize, batteryBacked, saveFile)
        {
        }

        public override byte ReadByte(int address)
        {
            EnsureCartridgeAddress(address);
            if (address <= 0x7FFF) {
                return Rom[address % Rom.Length];
            }

            return Ram.Read(address - 0xA000);
        }

        public override void WriteByte(int address, byte value)
        {
            EnsureCartridgeAddress(address);
            if (address >= 0xA000) {
                Ram.Write(address - 0xA000, value);
            }
        }

        public override CartridgeMapperState CaptureState() =>
            new CartridgeMapperState(CartridgeType, Array.Empty<byte>(), Ram.Capture());

        public override void ValidateState(CartridgeMapperState state) =>
            ValidateState(state, CartridgeType, 0, Ram.Length);

        public override void RestoreState(CartridgeMapperState state)
        {
            ValidateState(state);
            Ram.Restore(state.CopyRam());
        }
    }

    public sealed class Mbc1 : CartridgeMapper
    {
        private int romBankLow = 1;
        private int bankHigh;
        private bool ramEnabled;
        private int mode;

        public Mbc1(byte[] romData, Mbc cartridgeType, int romSize, string? saveFile)
            : this(romData, cartridgeType, romSize, 0x8000, true, saveFile)
        {
        }

        public Mbc1(
            byte[] romData,
            Mbc cartridgeType,
            int romSize,
            int ramSize,
            bool batteryBacked,
            string? saveFile)
            : base(romData, cartridgeType, romSize, ramSize, batteryBacked, saveFile)
        {
        }

        public int CurrentROMBank
        {
            get => (bankHigh << 5) | romBankLow;
            set
            {
                romBankLow = value & 0x1F;
                if (romBankLow == 0) {
                    romBankLow = 1;
                }
                bankHigh = (value >> 5) & 0x03;
            }
        }

        public int CurrentRAMBank
        {
            get => bankHigh;
            set => bankHigh = value & 0x03;
        }

        public bool RAMEnable
        {
            get => ramEnabled;
            set => ramEnabled = value;
        }

        public int Mode
        {
            get => mode;
            set => mode = value & 0x01;
        }

        public override byte ReadByte(int address)
        {
            EnsureCartridgeAddress(address);
            if (address <= 0x3FFF) {
                int bank = mode == 1 ? bankHigh << 5 : 0;
                return ReadRom(bank, address);
            }
            if (address <= 0x7FFF) {
                return ReadRom((bankHigh << 5) | romBankLow, address - 0x4000);
            }
            if (!ramEnabled) {
                return 0xFF;
            }

            int ramBank = mode == 1 ? bankHigh : 0;
            return Ram.Read(ramBank * 0x2000 + address - 0xA000);
        }

        public override void WriteByte(int address, byte value)
        {
            EnsureCartridgeAddress(address);
            if (address <= 0x1FFF) {
                ramEnabled = (value & 0x0F) == 0x0A;
            } else if (address <= 0x3FFF) {
                romBankLow = value & 0x1F;
                if (romBankLow == 0) {
                    romBankLow = 1;
                }
            } else if (address <= 0x5FFF) {
                bankHigh = value & 0x03;
            } else if (address <= 0x7FFF) {
                mode = value & 0x01;
            } else if (ramEnabled) {
                int ramBank = mode == 1 ? bankHigh : 0;
                Ram.Write(ramBank * 0x2000 + address - 0xA000, value);
            }
        }

        public override CartridgeMapperState CaptureState() =>
            new CartridgeMapperState(
                CartridgeType,
                new[] { (byte)romBankLow, (byte)bankHigh, (byte)mode, ramEnabled ? (byte)1 : (byte)0 },
                Ram.Capture());

        public override void ValidateState(CartridgeMapperState state)
        {
            ValidateState(state, CartridgeType, 4, Ram.Length);
            byte[] registers = state.CopyRegisters();
            if (registers[0] < 1 || registers[0] > 0x1F ||
                registers[1] > 0x03 ||
                registers[2] > 1 ||
                registers[3] > 1) {
                throw new InvalidDataException("MBC1 state contains invalid register values.");
            }
        }

        public override void RestoreState(CartridgeMapperState state)
        {
            ValidateState(state);
            byte[] registers = state.CopyRegisters();
            romBankLow = registers[0] & 0x1F;
            if (romBankLow == 0) {
                romBankLow = 1;
            }
            bankHigh = registers[1] & 0x03;
            mode = registers[2] & 0x01;
            ramEnabled = registers[3] != 0;
            Ram.Restore(state.CopyRam());
        }
    }

    public sealed class Mbc2 : CartridgeMapper
    {
        private int romBank = 1;
        private bool ramEnabled;

        public Mbc2(
            byte[] romData,
            Mbc cartridgeType,
            int romSize,
            bool batteryBacked,
            string? saveFile)
            : base(romData, cartridgeType, romSize, 0x200, batteryBacked, saveFile)
        {
        }

        public override byte ReadByte(int address)
        {
            EnsureCartridgeAddress(address);
            if (address <= 0x3FFF) {
                return ReadRom(0, address);
            }
            if (address <= 0x7FFF) {
                return ReadRom(romBank, address - 0x4000);
            }
            if (!ramEnabled) {
                return 0xFF;
            }

            return (byte)(0xF0 | (Ram.Read(address & 0x01FF) & 0x0F));
        }

        public override void WriteByte(int address, byte value)
        {
            EnsureCartridgeAddress(address);
            if (address <= 0x3FFF) {
                if ((address & 0x0100) == 0) {
                    ramEnabled = (value & 0x0F) == 0x0A;
                } else {
                    romBank = value & 0x0F;
                    if (romBank == 0) {
                        romBank = 1;
                    }
                }
            } else if (address >= 0xA000 && ramEnabled) {
                Ram.Write(address & 0x01FF, (byte)(value & 0x0F));
            }
        }

        public override CartridgeMapperState CaptureState() =>
            new CartridgeMapperState(
                CartridgeType,
                new[] { (byte)romBank, ramEnabled ? (byte)1 : (byte)0 },
                Ram.Capture());

        public override void ValidateState(CartridgeMapperState state)
        {
            ValidateState(state, CartridgeType, 2, Ram.Length);
            byte[] registers = state.CopyRegisters();
            if (registers[0] < 1 || registers[0] > 0x0F || registers[1] > 1) {
                throw new InvalidDataException("MBC2 state contains invalid register values.");
            }

            byte[] ram = state.CopyRam();
            for (int index = 0; index < ram.Length; index++) {
                if (ram[index] > 0x0F) {
                    throw new InvalidDataException("MBC2 state contains non-nibble RAM data.");
                }
            }
        }

        public override void RestoreState(CartridgeMapperState state)
        {
            ValidateState(state);
            byte[] registers = state.CopyRegisters();
            romBank = registers[0] & 0x0F;
            if (romBank == 0) {
                romBank = 1;
            }
            ramEnabled = registers[1] != 0;
            Ram.Restore(state.CopyRam());
        }
    }

    public sealed class Mbc3 : CartridgeMapper
    {
        private const uint RtcFileMagic = 0x43545241;
        private const ushort RtcFileVersion = 1;
        private const long RtcPeriodSeconds = 512L * 24 * 60 * 60;

        private int romBank = 1;
        private int ramRtcSelect;
        private bool ramTimerEnabled;
        private byte previousLatchWrite;
        private readonly byte[] rtcRegisters = new byte[5];
        private readonly byte[] latchedRtcRegisters = new byte[5];
        private bool rtcLatched;
        private readonly bool hasRtc;
        private readonly string? rtcFile;
        private readonly TimeProvider timeProvider;
        private DateTimeOffset rtcUpdatedAt;

        public Mbc3(byte[] romData, Mbc cartridgeType, int romSize, string? saveFile)
            : this(romData, cartridgeType, romSize, 0x8000, true, saveFile)
        {
        }

        public Mbc3(
            byte[] romData,
            Mbc cartridgeType,
            int romSize,
            int ramSize,
            bool batteryBacked,
            string? saveFile)
            : this(
                romData,
                cartridgeType,
                romSize,
                ramSize,
                batteryBacked,
                saveFile,
                TimeProvider.System)
        {
        }

        public Mbc3(
            byte[] romData,
            Mbc cartridgeType,
            int romSize,
            int ramSize,
            bool batteryBacked,
            string? saveFile,
            TimeProvider timeProvider)
            : base(romData, cartridgeType, romSize, ramSize, batteryBacked, saveFile)
        {
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            hasRtc = cartridgeType == Mbc.ROM_MBC3_TIMER_BATT ||
                cartridgeType == Mbc.ROM_MBC3_TIMER_RAM_BATT;
            rtcFile = hasRtc && batteryBacked && !string.IsNullOrWhiteSpace(saveFile)
                ? saveFile + ".rtc"
                : null;
            rtcUpdatedAt = this.timeProvider.GetUtcNow();
            LoadRtc();
        }

        public int CurrentROMBank
        {
            get => romBank;
            set
            {
                romBank = value & 0x7F;
                if (romBank == 0) {
                    romBank = 1;
                }
            }
        }

        public int CurrentRAMBank
        {
            get => ramRtcSelect;
            set => ramRtcSelect = value & 0x0F;
        }

        public bool RAMEnable
        {
            get => ramTimerEnabled;
            set => ramTimerEnabled = value;
        }

        public override byte ReadByte(int address)
        {
            EnsureCartridgeAddress(address);
            if (address <= 0x3FFF) {
                return ReadRom(0, address);
            }
            if (address <= 0x7FFF) {
                return ReadRom(romBank, address - 0x4000);
            }
            if (!ramTimerEnabled) {
                return 0xFF;
            }
            if (ramRtcSelect <= 0x07) {
                return Ram.Read(ramRtcSelect * 0x2000 + address - 0xA000);
            }
            if (ramRtcSelect >= 0x08 && ramRtcSelect <= 0x0C) {
                if (!hasRtc) {
                    return 0xFF;
                }

                AdvanceRtc();
                int register = ramRtcSelect - 0x08;
                return rtcLatched ? latchedRtcRegisters[register] : rtcRegisters[register];
            }

            return 0xFF;
        }

        public override void WriteByte(int address, byte value)
        {
            EnsureCartridgeAddress(address);
            if (address <= 0x1FFF) {
                ramTimerEnabled = (value & 0x0F) == 0x0A;
            } else if (address <= 0x3FFF) {
                romBank = value & 0x7F;
                if (romBank == 0) {
                    romBank = 1;
                }
            } else if (address <= 0x5FFF) {
                ramRtcSelect = value & 0x0F;
            } else if (address <= 0x7FFF) {
                byte latchValue = (byte)(value & 0x01);
                if (previousLatchWrite == 0 && latchValue == 1) {
                    AdvanceRtc();
                    Array.Copy(rtcRegisters, latchedRtcRegisters, rtcRegisters.Length);
                    rtcLatched = true;
                }
                previousLatchWrite = latchValue;
            } else if (ramTimerEnabled && ramRtcSelect <= 0x07) {
                Ram.Write(ramRtcSelect * 0x2000 + address - 0xA000, value);
            } else if (
                ramTimerEnabled &&
                hasRtc &&
                ramRtcSelect >= 0x08 &&
                ramRtcSelect <= 0x0C) {
                WriteRtcRegister(ramRtcSelect - 0x08, value);
            }
        }

        public override CartridgeMapperState CaptureState()
        {
            AdvanceRtc();
            byte[] registers = new byte[15];
            registers[0] = (byte)romBank;
            registers[1] = (byte)ramRtcSelect;
            registers[2] = ramTimerEnabled ? (byte)1 : (byte)0;
            registers[3] = previousLatchWrite;
            registers[4] = rtcLatched ? (byte)1 : (byte)0;
            Array.Copy(rtcRegisters, 0, registers, 5, rtcRegisters.Length);
            Array.Copy(latchedRtcRegisters, 0, registers, 10, latchedRtcRegisters.Length);
            return new CartridgeMapperState(CartridgeType, registers, Ram.Capture());
        }

        public override void ValidateState(CartridgeMapperState state)
        {
            ValidateState(state, CartridgeType, 15, Ram.Length);
            byte[] registers = state.CopyRegisters();
            if (registers[0] < 1 || registers[0] > 0x7F ||
                registers[1] > 0x0F ||
                registers[2] > 1 ||
                registers[3] > 1 ||
                registers[4] > 1) {
                throw new InvalidDataException("MBC3 state contains invalid mapper register values.");
            }

            ValidateRtcRegisters(registers, 5);
            ValidateRtcRegisters(registers, 10);
        }

        public override void RestoreState(CartridgeMapperState state)
        {
            ValidateState(state);
            byte[] registers = state.CopyRegisters();
            romBank = registers[0] & 0x7F;
            if (romBank == 0) {
                romBank = 1;
            }
            ramRtcSelect = registers[1] & 0x0F;
            ramTimerEnabled = registers[2] != 0;
            previousLatchWrite = (byte)(registers[3] & 0x01);
            rtcLatched = registers[4] != 0;
            Array.Copy(registers, 5, rtcRegisters, 0, rtcRegisters.Length);
            Array.Copy(registers, 10, latchedRtcRegisters, 0, latchedRtcRegisters.Length);
            rtcUpdatedAt = timeProvider.GetUtcNow();
            Ram.Restore(state.CopyRam());
        }

        private static void ValidateRtcRegisters(byte[] registers, int offset)
        {
            if (registers[offset] > 59 ||
                registers[offset + 1] > 59 ||
                registers[offset + 2] > 23 ||
                (registers[offset + 4] & ~0xC1) != 0) {
                throw new InvalidDataException("MBC3 state contains invalid RTC register values.");
            }
        }

        public override void FlushPersistentState()
        {
            base.FlushPersistentState();
            if (rtcFile == null) {
                return;
            }

            AdvanceRtc();
            string directory = Path.GetDirectoryName(rtcFile);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }

            string temporaryFile = rtcFile + ".tmp";
            using (var stream = File.Open(temporaryFile, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream)) {
                writer.Write(RtcFileMagic);
                writer.Write(RtcFileVersion);
                writer.Write(rtcRegisters);
                writer.Write(timeProvider.GetUtcNow().ToUnixTimeSeconds());
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryFile, rtcFile, true);
        }

        private void WriteRtcRegister(int register, byte value)
        {
            AdvanceRtc();
            switch (register) {
                case 0:
                    rtcRegisters[0] = (byte)(value % 60);
                    break;
                case 1:
                    rtcRegisters[1] = (byte)(value % 60);
                    break;
                case 2:
                    rtcRegisters[2] = (byte)(value % 24);
                    break;
                case 3:
                    rtcRegisters[3] = value;
                    break;
                case 4:
                    rtcRegisters[4] = (byte)(value & 0xC1);
                    break;
            }
        }

        private void AdvanceRtc()
        {
            if (!hasRtc) {
                return;
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            if ((rtcRegisters[4] & 0x40) != 0 || now <= rtcUpdatedAt) {
                rtcUpdatedAt = now;
                return;
            }

            long elapsedSeconds = (long)(now - rtcUpdatedAt).TotalSeconds;
            if (elapsedSeconds <= 0) {
                return;
            }

            rtcUpdatedAt = rtcUpdatedAt.AddSeconds(elapsedSeconds);
            long days = rtcRegisters[3] | ((rtcRegisters[4] & 0x01) << 8);
            long totalSeconds =
                days * 24 * 60 * 60 +
                rtcRegisters[2] * 60 * 60 +
                rtcRegisters[1] * 60 +
                rtcRegisters[0] +
                elapsedSeconds;

            bool carry = (rtcRegisters[4] & 0x80) != 0 || totalSeconds >= RtcPeriodSeconds;
            totalSeconds %= RtcPeriodSeconds;
            days = totalSeconds / (24 * 60 * 60);
            totalSeconds %= 24 * 60 * 60;
            rtcRegisters[2] = (byte)(totalSeconds / (60 * 60));
            totalSeconds %= 60 * 60;
            rtcRegisters[1] = (byte)(totalSeconds / 60);
            rtcRegisters[0] = (byte)(totalSeconds % 60);
            rtcRegisters[3] = (byte)days;
            byte dayHigh = (byte)((days >> 8) & 0x01);
            if (carry) {
                dayHigh |= 0x80;
            }
            rtcRegisters[4] = dayHigh;
        }

        private void LoadRtc()
        {
            if (rtcFile == null || !File.Exists(rtcFile)) {
                return;
            }

            try {
                using var stream = File.OpenRead(rtcFile);
                using var reader = new BinaryReader(stream);
                if (reader.ReadUInt32() != RtcFileMagic || reader.ReadUInt16() != RtcFileVersion) {
                    return;
                }

                byte[] registers = reader.ReadBytes(rtcRegisters.Length);
                if (registers.Length != rtcRegisters.Length) {
                    return;
                }
                long savedAt = reader.ReadInt64();
                Array.Copy(registers, rtcRegisters, rtcRegisters.Length);
                rtcUpdatedAt = DateTimeOffset.FromUnixTimeSeconds(savedAt);
                AdvanceRtc();
            } catch (EndOfStreamException) {
                Array.Clear(rtcRegisters, 0, rtcRegisters.Length);
                rtcUpdatedAt = timeProvider.GetUtcNow();
            }
        }
    }

    public sealed class Mbc5 : CartridgeMapper
    {
        private readonly bool hasRumble;
        private int romBank;
        private int ramBank;
        private bool ramEnabled;

        public Mbc5(
            byte[] romData,
            Mbc cartridgeType,
            int romSize,
            int ramSize,
            bool batteryBacked,
            bool hasRumble,
            string? saveFile)
            : base(romData, cartridgeType, romSize, ramSize, batteryBacked, saveFile)
        {
            this.hasRumble = hasRumble;
        }

        public bool RumbleEnabled { get; private set; }

        public override byte ReadByte(int address)
        {
            EnsureCartridgeAddress(address);
            if (address <= 0x3FFF) {
                return ReadRom(0, address);
            }
            if (address <= 0x7FFF) {
                return ReadRom(romBank, address - 0x4000);
            }
            if (!ramEnabled) {
                return 0xFF;
            }

            return Ram.Read(ramBank * 0x2000 + address - 0xA000);
        }

        public override void WriteByte(int address, byte value)
        {
            EnsureCartridgeAddress(address);
            if (address <= 0x1FFF) {
                ramEnabled = (value & 0x0F) == 0x0A;
            } else if (address <= 0x2FFF) {
                romBank = (romBank & 0x100) | value;
            } else if (address <= 0x3FFF) {
                romBank = (romBank & 0xFF) | ((value & 0x01) << 8);
            } else if (address <= 0x5FFF) {
                RumbleEnabled = hasRumble && (value & 0x08) != 0;
                ramBank = value & (hasRumble ? 0x07 : 0x0F);
            } else if (address >= 0xA000 && ramEnabled) {
                Ram.Write(ramBank * 0x2000 + address - 0xA000, value);
            }
        }

        public override CartridgeMapperState CaptureState() =>
            new CartridgeMapperState(
                CartridgeType,
                new[] {
                    (byte)romBank,
                    (byte)(romBank >> 8),
                    (byte)ramBank,
                    ramEnabled ? (byte)1 : (byte)0,
                    RumbleEnabled ? (byte)1 : (byte)0
                },
                Ram.Capture());

        public override void ValidateState(CartridgeMapperState state)
        {
            ValidateState(state, CartridgeType, 5, Ram.Length);
            byte[] registers = state.CopyRegisters();
            int maximumRamBank = hasRumble ? 0x07 : 0x0F;
            if (registers[1] > 1 ||
                registers[2] > maximumRamBank ||
                registers[3] > 1 ||
                registers[4] > 1 ||
                (!hasRumble && registers[4] != 0)) {
                throw new InvalidDataException("MBC5 state contains invalid register values.");
            }
        }

        public override void RestoreState(CartridgeMapperState state)
        {
            ValidateState(state);
            byte[] registers = state.CopyRegisters();
            romBank = registers[0] | ((registers[1] & 0x01) << 8);
            ramBank = registers[2] & (hasRumble ? 0x07 : 0x0F);
            ramEnabled = registers[3] != 0;
            RumbleEnabled = hasRumble && registers[4] != 0;
            Ram.Restore(state.CopyRam());
        }
    }
}
