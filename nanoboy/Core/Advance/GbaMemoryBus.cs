using System;
using System.IO;

namespace nanoboy.Core.Advance
{
    public sealed class GbaMemoryBus
    {
        public const uint RomBase = 0x08000000;
        public const uint IoBase = 0x04000000;
        public const uint PaletteBase = 0x05000000;
        public const uint VramBase = 0x06000000;
        public const uint OamBase = 0x07000000;

        private readonly byte[] bios = new byte[0x4000];
        private readonly byte[] ewram = new byte[0x40000];
        private readonly byte[] iwram = new byte[0x8000];
        private readonly byte[] io = new byte[0x400];
        private readonly byte[] palette = new byte[0x400];
        private readonly byte[] vram = new byte[0x18000];
        private readonly byte[] oam = new byte[0x400];
        private readonly byte[] sram = new byte[0x10000];
        private readonly byte[] rom;

        public GbaMemoryBus(ReadOnlySpan<byte> romData)
        {
            if (romData.Length is < 4 or > 32 * 1024 * 1024)
                throw new InvalidDataException("A GBA ROM must contain between 4 bytes and 32 MiB.");
            rom = romData.ToArray();
        }

        public byte Read8(uint address)
        {
            uint region = address >> 24;
            return region switch
            {
                0x00 when address < bios.Length => bios[address],
                0x02 => ewram[address & 0x3FFFF],
                0x03 => iwram[address & 0x7FFF],
                0x04 when (address & 0xFFFFFF) < io.Length => io[address & 0x3FF],
                0x05 => palette[address & 0x3FF],
                0x06 => vram[NormalizeVram(address)],
                0x07 => oam[address & 0x3FF],
                >= 0x08 and <= 0x0D => ReadRomByte(address),
                0x0E or 0x0F => sram[address & 0xFFFF],
                _ => 0
            };
        }

        public ushort Read16(uint address) => (ushort)(Read8(address) | Read8(address + 1) << 8);

        public uint Read32(uint address) =>
            (uint)(Read8(address) | Read8(address + 1) << 8 |
                Read8(address + 2) << 16 | Read8(address + 3) << 24);

        public void Write8(uint address, byte value)
        {
            uint region = address >> 24;
            switch (region)
            {
                case 0x02: ewram[address & 0x3FFFF] = value; break;
                case 0x03: iwram[address & 0x7FFF] = value; break;
                case 0x04 when (address & 0xFFFFFF) < io.Length: io[address & 0x3FF] = value; break;
                case 0x05: palette[address & 0x3FF] = value; break;
                case 0x06: vram[NormalizeVram(address)] = value; break;
                case 0x07: oam[address & 0x3FF] = value; break;
                case 0x0E:
                case 0x0F: sram[address & 0xFFFF] = value; break;
            }
        }

        public void Write16(uint address, ushort value)
        {
            Write8(address, (byte)value);
            Write8(address + 1, (byte)(value >> 8));
        }

        public void Write32(uint address, uint value)
        {
            Write16(address, (ushort)value);
            Write16(address + 2, (ushort)(value >> 16));
        }

        internal ReadOnlySpan<byte> VideoRam => vram;
        public ushort DisplayControl => Read16(IoBase);

        private byte ReadRomByte(uint address)
        {
            uint offset = (address - RomBase) & 0x01FFFFFF;
            return offset < rom.Length ? rom[offset] : (byte)0;
        }

        private static int NormalizeVram(uint address)
        {
            int offset = (int)(address & 0x1FFFF);
            if (offset >= 0x18000)
                offset -= 0x8000;
            return offset;
        }
    }
}
