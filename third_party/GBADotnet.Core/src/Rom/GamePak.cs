using GameboyAdvanced.Core.Dma;
using System.Text;

namespace GameboyAdvanced.Core.Rom;

public enum RomBackupType
{
    EEPROM,
    SRAM,
    FLASH64,
    FLASH128,
}

public class GamePak
{
    public readonly byte[] _header = new byte[0xC0];
    public readonly byte[] Data = new byte[0x200_0000]; // Max 32MB ROM size
    public readonly uint RomMask;
    public readonly byte[] _sram = new byte[0x1_0000];
    public readonly FlashBackup? _flashBackup;
    public readonly EEPromBackup? _eepromBackup;
    public readonly GpioRtc? _rtc;

    public readonly uint RomEntryPoint;
    public readonly byte[] LogoCompressed = new byte[156];
    public readonly string GameTitle;
    public readonly string GameCode;
    public readonly string MakerCode;
    public readonly byte FixedValue;
    public readonly byte MainUnitCode;
    public readonly byte DeviceType;
    public readonly byte[] ReservedArea1 = new byte[7];
    public readonly byte SoftwareVersion;
    public readonly byte ComplementCheck;
    public readonly byte[] ReservedArea2 = new byte[2];
    public readonly RomBackupType RomBackupType;

    // TODO - Multiboot details

    public GamePak(
        byte[] data,
        RomBackupType? romBackupType = null,
        Func<DateTime>? rtcClock = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.LongLength < 0xC0) throw new ArgumentException("Rom must be >= 0xC0 in size to fit cartridge header", nameof(data));
        Array.Copy(data, 0, _header, 0, _header.Length);

        RomMask = data.Length <= 0x100_0000 ? 0xFF_FFFFu : 0x1FF_FFFF;
        Data = (byte[])data.Clone();

        RomEntryPoint = Utils.ReadWord(_header, 0, 0xFFFF_FFFF);
        Array.Copy(_header, 4, LogoCompressed, 0, LogoCompressed.Length);
        GameTitle = ReadHeaderText(_header.AsSpan(0xA0, 12));
        GameCode = ReadHeaderText(_header.AsSpan(0xAC, 4));
        MakerCode = ReadHeaderText(_header.AsSpan(0xB0, 2));
        FixedValue = _header[178];
        MainUnitCode = _header[179];
        DeviceType = _header[180];
        Array.Copy(_header, 181, ReservedArea1, 0, ReservedArea1.Length);
        SoftwareVersion = _header[188];
        ComplementCheck = _header[189];
        Array.Copy(_header, 190, ReservedArea2, 0, ReservedArea2.Length);

        Array.Fill<byte>(_sram, 0xFF);

        RomBackupType = romBackupType ?? CalculateRomBackupType(data);
        if (data.AsSpan().IndexOf("SIIRTC_V"u8) >= 0 ||
            data.AsSpan().IndexOf("RTC_V"u8) >= 0)
        {
            _rtc = new GpioRtc(rtcClock);
        }

        if (RomBackupType == RomBackupType.FLASH128)
        {
            _flashBackup = new FlashBackup(0x62, 0x13, supportsBankSwitching: true); // Sanyo
        }
        else if (RomBackupType == RomBackupType.FLASH64)
        {
            _flashBackup = new FlashBackup(0xBF, 0xD4, supportsBankSwitching: false); // SST
        }
        else if (RomBackupType == RomBackupType.EEPROM)
        {
            // EEPROM is visible only through the final Game Pak ROM window.
            // Carts larger than 16 MiB decode only its final 256 bytes.
            var mask = Data.Length > 0x0100_0000 ? 0x0DFF_FF00u : 0x0D00_0000u;

            _eepromBackup = new EEPromBackup(mask);
        }
    }

    internal void SetDmaDataUnit(DmaDataUnit dma)
    {
        if (_eepromBackup != null)
        {
            _eepromBackup.DmaDataUnit = dma;
        }
    }

    internal static RomBackupType CalculateRomBackupType(ReadOnlySpan<byte> rom)
    {
        // Nintendo's SDK leaves an ASCII identifier in ROM. Search the bytes
        // directly instead of allocating a second, ROM-sized string.
        if (rom.IndexOf("EEPROM_V"u8) >= 0)
            return RomBackupType.EEPROM;
        if (rom.IndexOf("FLASH1M_V"u8) >= 0)
            return RomBackupType.FLASH128;
        if (rom.IndexOf("FLASH512_V"u8) >= 0 || rom.IndexOf("FLASH_V"u8) >= 0)
            return RomBackupType.FLASH64;
        if (rom.IndexOf("SRAM_F_V"u8) >= 0 || rom.IndexOf("SRAM_V"u8) >= 0)
            return RomBackupType.SRAM;

        // Preserve upstream compatibility for homebrew without an SDK marker.
        return RomBackupType.SRAM;
    }

    private static string ReadHeaderText(ReadOnlySpan<byte> bytes)
    {
        int terminator = bytes.IndexOf((byte)0);
        if (terminator >= 0)
            bytes = bytes[..terminator];
        return Encoding.ASCII.GetString(bytes).TrimEnd(' ');
    }

    /// <summary>
    /// Backup storage is only available over an 8 bit bus and behaves very 
    /// differently depending on what type of storage is used on the cart.
    /// </summary>
    /// 
    /// <remarks>
    /// TODO - Potentially improve performance here if it turns out 
    /// to be hit a lot by predeciding which function will be correct
    /// using delegate* at construction
    /// </remarks>
    internal byte ReadBackupStorage(uint address) => RomBackupType switch
    {
        RomBackupType.SRAM => _sram[address & 0x0EFF_FFFF & 0x7FFF],
        RomBackupType.FLASH64 => _flashBackup!.Read(address),
        RomBackupType.FLASH128 => _flashBackup!.Read(address),
        RomBackupType.EEPROM => _sram[address & 0x0EFF_FFFF & 0x7FFF], //_eepromBackup!.Read(address),
        _ => throw new Exception($"Invalid backup storage type {RomBackupType}")
    };

    internal void WriteBackupStorage(uint address, byte value)
    {
        switch (RomBackupType)
        {
            case RomBackupType.SRAM:
                _sram[address & 0x0EFF_FFFF & 0x7FFF] = value;
                break;
            case RomBackupType.FLASH128:
            case RomBackupType.FLASH64:
                _flashBackup!.Write(address, value);
                break;
            case RomBackupType.EEPROM:
                _sram[address & 0x0EFF_FFFF & 0x7FFF] = value;
                break;
        }
    }

    internal void Write(uint address, byte value)
    {
        uint offset = address & RomMask;
        if (_rtc != null && offset is GpioRtc.DataOffset or GpioRtc.DirectionOffset or GpioRtc.ControlOffset)
        {
            _rtc.Write(offset, value);
            return;
        }
        if (RomBackupType == RomBackupType.EEPROM && _eepromBackup!.IsEEPromAddress(address))
        {
            _eepromBackup!.Write(address, value);
        }
    }

    internal byte ReadByte(uint address)
    {
        uint offset = address & RomMask;

        if (_rtc != null && offset is >= GpioRtc.DataOffset and <= GpioRtc.ControlOffset + 1)
        {
            uint aligned = offset & 0xFFFF_FFFE;
            ushort romValue = aligned + 1 < Data.Length
                ? Utils.ReadHalfWord(Data, aligned, RomMask)
                : (ushort)0xFFFF;
            ushort value = _rtc.Read(aligned, romValue);
            return (byte)(value >> (int)((offset & 1) * 8));
        }

        if (_eepromBackup != null && _eepromBackup.IsEEPromAddress(address))
        {
            return _eepromBackup.Read(address);
        }

        return offset < Data.Length ?
            Data[offset] :
            (byte)(offset >> 1 >> (int)((offset & 1) * 8));
    }

    internal ushort ReadHalfWord(uint address)
    {
        uint offset = address & RomMask & 0xFFFF_FFFE;
        if (_rtc != null && offset is GpioRtc.DataOffset or GpioRtc.DirectionOffset or GpioRtc.ControlOffset)
        {
            ushort romValue = offset + 1 < Data.Length
                ? Utils.ReadHalfWord(Data, offset, RomMask)
                : (ushort)0xFFFF;
            return _rtc.Read(offset, romValue);
        }
        if (_eepromBackup != null && _eepromBackup.IsEEPromAddress(address))
        {
            return _eepromBackup.Read(address);
        }

        return (address & RomMask) < Data.Length
            ? Utils.ReadHalfWord(Data, address, RomMask)
            : (ushort)(address >> 1);
    }

    internal uint ReadWord(uint address)
    {
        uint offset = address & RomMask & 0xFFFF_FFFC;
        if (_rtc != null && offset is GpioRtc.DataOffset or GpioRtc.DirectionOffset or GpioRtc.ControlOffset)
        {
            return (uint)(ReadHalfWord(address) | (ReadHalfWord(address + 2) << 16));
        }
        if (_eepromBackup != null && _eepromBackup.IsEEPromAddress(address))
        {
            return _eepromBackup.Read(address);
        }

        return (address & RomMask) < Data.Length
            ? Utils.ReadWord(Data, address, RomMask)
            : (((address & 0xFFFF_FFFC) >> 1) & 0xFFFF) | (((((address & 0xFFFF_FFFC) + 2) >> 1) & 0xFFFF) << 16);
    }

    internal static bool CheckAddressIsPageBoundary(uint address) => (address & 0x1_FFFF) == 0;

    public override string ToString()
    {
        return $"{GameTitle} - {RomBackupType} - {RomMask:X8}";
    }
}
