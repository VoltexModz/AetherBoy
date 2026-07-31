using System;
using System.IO;

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

    public class ROM
    {
        public Mbc CartridgeType { get; set; }
        public IMemoryDevice MBC { get; set; }
        public int RAMSize { get; set; }
        public int ROMSize { get; set; }
        public bool HasColorFeatures { get; set; }
        public bool HasSGBFeatures { get; set; }
        public string Title { get; set; }
        public bool Japanese { get; set; }

        public ROM(string path, string save_path)
        {
            byte[] data = File.ReadAllBytes(path);
            Title = string.Empty;
            for (int i = 0; i < 16; i++) {
                Title += (char)data[0x134 + i];
            }
            HasColorFeatures = data[0x143] == 0x80 || data[0x143] == 0xC0;
            HasSGBFeatures = data[0x146] == 0x03;
            CartridgeType = (Mbc) data[0x147];
            ROMSize = 32768 << data[0x148];
            switch (data[0x149])
            {
                case 0x0:
                    RAMSize = 0;
                    break;
                case 0x1:
                    RAMSize = 2048;
                    break;
                case 0x2:
                    RAMSize = 8192;
                    break;
                case 0x03:
                    RAMSize = 32768;
                    break;
            }
            Japanese = data[0x14A] == 0x00;
            switch (CartridgeType)
            {
                case Mbc.ROM_NONE:
                    MBC = new NoMBC(data, CartridgeType, ROMSize);
                    break;
                case Mbc.ROM_MBC1_RAM_BATT:
                case Mbc.ROM_MBC1:
                    MBC = new Mbc1(data, CartridgeType, ROMSize, save_path);
                    break;
                case Mbc.ROM_MBC3_RAM:
                case Mbc.ROM_MBC3_RAM_BATT:
                case Mbc.ROM_MBC3_TIMER_BATT:
                case Mbc.ROM_MBC3_TIMER_RAM_BATT:
                    MBC = new Mbc3(data, CartridgeType, ROMSize, save_path);
                    break;
                case Mbc.ROM_MBC5:
                case Mbc.ROM_MBC5_RAM:
                case Mbc.ROM_MBC5_RAM_BATT:
                case Mbc.ROM_MBC5_RUMBLE:
                case Mbc.ROM_MBC5_RUMBLE_RAM:
                case Mbc.ROM_MBC5_RUMBLE_RAM_BATT:
                    MBC = new Mbc3(data, CartridgeType, ROMSize, save_path);
                    break;
                default:
                    throw new Exception("Nicht unterstützter Romtyp " + CartridgeType);
            }
        }
    }
}
