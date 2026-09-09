using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace nanoboy.Core
{
    public enum Mbc
    {
        ROM_NONE = 0x00,
        ROM_MBC1 = 0x01,
        ROM_MBC1_RAM = 0x02,
        ROM_MBC1_RAM_BATT = 0x03,
        ROM_MBC2 = 0x05,
        ROM_MBC2_BATT = 0x06,
        ROM_RAM = 0x08,
        ROM_RAM_BATT = 0x09,
        ROM_MMM01 = 0x0B,
        ROM_MMM01_RAM = 0x0C,
        ROM_MMM01_RAM_BATT = 0x0D,
        ROM_MBC3_TIMER_BATT = 0x0F,
        ROM_MBC3_TIMER_RAM_BATT = 0x10,
        ROM_MBC3 = 0x11,
        ROM_MBC3_RAM = 0x12,
        ROM_MBC3_RAM_BATT = 0x13,
        ROM_MBC4 = 0x15,
        ROM_MBC4_RAM = 0x16,
        ROM_MBC4_BATT = 0x17,
        ROM_MBC5 = 0x19,
        ROM_MBC5_RAM = 0x1A,
        ROM_MBC5_RAM_BATT = 0x1B,
        ROM_MBC5_RUMBLE = 0x1C,
        ROM_MBC5_RUMBLE_RAM = 0x1D,
        ROM_MBC5_RUMBLE_RAM_BATT = 0x1E,
        ROM_POCKET_CAMERA = 0xFC,
        ROM_BANDAI_TAMA5 = 0xFD,
        ROM_HUC3 = 0xFE,
        ROM_HUC1_RAM_BATT = 0xFF
    }

    public sealed class ROM
    {
        private const int MinimumHeaderLength = 0x150;

        public ROM(string path, string savePath)
        {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("A ROM path is required.", nameof(path));
            }

            byte[] data = File.ReadAllBytes(path);
            if (data.Length < MinimumHeaderLength) {
                throw new InvalidDataException(
                    $"ROM is too small to contain a cartridge header: {data.Length} bytes.");
            }

            HasColorFeatures = data[0x143] == 0x80 || data[0x143] == 0xC0;
            HasSGBFeatures = data[0x146] == 0x03;
            Title = ReadTitle(data, HasColorFeatures);
            CartridgeType = (Mbc)data[0x147];
            ROMSize = DecodeRomSize(data[0x148]);
            RAMSize = DecodeRamSize(data[0x149]);
            Japanese = data[0x14A] == 0x00;

            if (data.Length < ROMSize) {
                throw new InvalidDataException(
                    $"Truncated ROM: header declares {ROMSize} bytes, file contains {data.Length}.");
            }

            RomSha256 = Convert.ToHexString(SHA256.HashData(data.AsSpan(0, ROMSize)));
            MBC = CreateMapper(data, savePath);
        }

        public Mbc CartridgeType { get; }
        public ICartridgeMapper MBC { get; }
        public int RAMSize { get; }
        public int ROMSize { get; }
        public bool HasColorFeatures { get; }
        public bool HasSGBFeatures { get; }
        public string Title { get; }
        public bool Japanese { get; }
        public string RomSha256 { get; }

        private ICartridgeMapper CreateMapper(byte[] data, string savePath)
        {
            bool batteryBacked = HasBattery(CartridgeType);
            switch (CartridgeType) {
                case Mbc.ROM_NONE:
                case Mbc.ROM_RAM:
                case Mbc.ROM_RAM_BATT:
                    return new NoMBC(
                        data,
                        CartridgeType,
                        ROMSize,
                        RAMSize,
                        batteryBacked,
                        savePath);

                case Mbc.ROM_MBC1:
                case Mbc.ROM_MBC1_RAM:
                case Mbc.ROM_MBC1_RAM_BATT:
                    return new Mbc1(
                        data,
                        CartridgeType,
                        ROMSize,
                        RAMSize,
                        batteryBacked,
                        savePath,
                        isMulticart: Mbc1MulticartDetector.LooksLikeMulticart(data.AsSpan(0, ROMSize)));

                case Mbc.ROM_MBC2:
                case Mbc.ROM_MBC2_BATT:
                    return new Mbc2(data, CartridgeType, ROMSize, batteryBacked, savePath);

                case Mbc.ROM_MBC3_TIMER_BATT:
                case Mbc.ROM_MBC3_TIMER_RAM_BATT:
                case Mbc.ROM_MBC3:
                case Mbc.ROM_MBC3_RAM:
                case Mbc.ROM_MBC3_RAM_BATT:
                    return new Mbc3(
                        data,
                        CartridgeType,
                        ROMSize,
                        RAMSize,
                        batteryBacked,
                        savePath);

                case Mbc.ROM_MBC5:
                case Mbc.ROM_MBC5_RAM:
                case Mbc.ROM_MBC5_RAM_BATT:
                case Mbc.ROM_MBC5_RUMBLE:
                case Mbc.ROM_MBC5_RUMBLE_RAM:
                case Mbc.ROM_MBC5_RUMBLE_RAM_BATT:
                    return new Mbc5(
                        data,
                        CartridgeType,
                        ROMSize,
                        RAMSize,
                        batteryBacked,
                        HasRumble(CartridgeType),
                        savePath);

                default:
                    throw new NotSupportedException(
                        $"Cartridge type 0x{(byte)CartridgeType:X2} ({CartridgeType}) is not supported.");
            }
        }

        private static string ReadTitle(byte[] data, bool hasColorFeatures)
        {
            int titleLength = hasColorFeatures ? 15 : 16;
            int terminator = Array.IndexOf(data, (byte)0, 0x134, titleLength);
            if (terminator < 0) {
                terminator = 0x134 + titleLength;
            }

            return Encoding.ASCII
                .GetString(data, 0x134, terminator - 0x134)
                .TrimEnd(' ');
        }

        private static int DecodeRomSize(byte code)
        {
            if (code <= 0x08) {
                return 0x8000 << code;
            }

            switch (code) {
                case 0x52:
                    return 72 * 0x4000;
                case 0x53:
                    return 80 * 0x4000;
                case 0x54:
                    return 96 * 0x4000;
                default:
                    throw new InvalidDataException($"Unsupported ROM size code 0x{code:X2}.");
            }
        }

        private static int DecodeRamSize(byte code)
        {
            switch (code) {
                case 0x00:
                case 0x01:
                    return 0;
                case 0x02:
                    return 0x2000;
                case 0x03:
                    return 0x8000;
                case 0x04:
                    return 0x20000;
                case 0x05:
                    return 0x10000;
                default:
                    throw new InvalidDataException($"Unsupported RAM size code 0x{code:X2}.");
            }
        }

        private static bool HasBattery(Mbc cartridgeType) =>
            cartridgeType == Mbc.ROM_RAM_BATT ||
            cartridgeType == Mbc.ROM_MBC1_RAM_BATT ||
            cartridgeType == Mbc.ROM_MBC2_BATT ||
            cartridgeType == Mbc.ROM_MBC3_TIMER_BATT ||
            cartridgeType == Mbc.ROM_MBC3_TIMER_RAM_BATT ||
            cartridgeType == Mbc.ROM_MBC3_RAM_BATT ||
            cartridgeType == Mbc.ROM_MBC5_RAM_BATT ||
            cartridgeType == Mbc.ROM_MBC5_RUMBLE_RAM_BATT;

        private static bool HasRumble(Mbc cartridgeType) =>
            cartridgeType == Mbc.ROM_MBC5_RUMBLE ||
            cartridgeType == Mbc.ROM_MBC5_RUMBLE_RAM ||
            cartridgeType == Mbc.ROM_MBC5_RUMBLE_RAM_BATT;
    }
}
