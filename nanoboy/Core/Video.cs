using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace nanoboy.Core
{
    public sealed class Video
    {
        public const int FrameWidth = 160;
        public const int FrameHeight = 144;
        public const int FramePixelCount = FrameWidth * FrameHeight;
        private const int FrameByteCount = FramePixelCount * sizeof(uint);

        public bool FrameReady;
        public int Frameskip;

        public bool LCDEnable;
        public bool WindowTileMapSelect;
        public bool WindowEnable;
        public bool TileDataSelect;
        public bool BackgroundTileMapSelect;
        public bool ObjectSize;
        public bool ObjectEnable;
        public bool BackgroundEnable;

        public bool CoincidenceInterrupt;
        public bool OAMInterrupt;
        public bool VBlankInterrupt;
        public bool HBlankInterrupt;
        public bool CoincidenceFlag;
        public int ModeFlag;

        public int SCY;
        public int SCX;
        public int LY;
        public int LYC;
        public int WY;
        public int WX;


        public int BGP;
        public int OBP0;
        public int OBP1;

        public bool BackgroundPaletteAI;
        public int BackgroundPaletteIndex;

        public bool ObjectPaletteAI;
        public int ObjectPaletteIndex;

        public int VRAMBank;

        private byte[,] vram;
        private byte[] oam;
        private byte[] pram1;
        private byte[] pram2;

        private readonly bool hasColorFeatures;
        private Interrupt interrupt;
        private HDMA hdma;
        private int clock;
        private uint[] monochromepalette;
        private uint[] frame;
        private readonly uint[] publishedFrame;
        private readonly object framePublishLock = new object();
        private long publishedFrameSequence;
        private bool statInterruptLine;
        private int framecounter;
        private bool updaterequired;

        public byte ReadVRAMDirect(int bank, int offset) => vram[bank, offset];
        public void WriteVRAMDirect(int bank, int offset, byte value) => vram[bank, offset] = value;
        public byte ReadOAMDirect(int offset) => oam[offset];
        public void WriteOAMDirect(int offset, byte value) => oam[offset] = value;

        public void SetMonochromePalette(int paletteIndex)
        {
            switch (paletteIndex)
            {
                case 1: // Pea Green (Original DMG)
                    monochromepalette = new uint[] {
                        0xFF9BBC0Fu,
                        0xFF8BAC0Fu,
                        0xFF306230u,
                        0xFF0F380Fu
                    };
                    break;
                case 2: // Game Boy Light (Teal)
                    monochromepalette = new uint[] {
                        0xFF00FFCDu,
                        0xFF00A597u,
                        0xFF00665Eu,
                        0xFF00332Fu
                    };
                    break;
                case 3: // Sepia
                    monochromepalette = new uint[] {
                        0xFFF5EA8Cu,
                        0xFFD4B055u,
                        0xFF8C5620u,
                        0xFF381900u
                    };
                    break;
                case 4: // Cyberpunk
                    monochromepalette = new uint[] {
                        0xFF00FFFFu,
                        0xFFFF00FFu,
                        0xFF800080u,
                        0xFF000040u
                    };
                    break;
                default: // Game Boy Pocket (Gray)
                    monochromepalette = new uint[] {
                        0xFFF5F5F5u,
                        0xFFA0A0A0u,
                        0xFF505050u,
                        0xFF000000u
                    };
                    break;
            }
        }

        internal uint ReadMonochromePaletteColor(int index)
        {
            if ((uint)index >= monochromepalette.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return monochromepalette[index];
        }

        private struct SpriteEntry
        {
            public int X;
            public int Y;
            public byte TileNumber;
            public byte Attributes;
            public int TableIndex;
        }

        public Video(Interrupt interrupt, HDMA hdma, bool hasColorFeatures)
        {
            this.hasColorFeatures = hasColorFeatures;
            this.interrupt = interrupt;
            this.hdma = hdma;
            vram = new byte[2, 0x2000];
            oam = new byte[0xA0];
            pram1 = new byte[0x40];
            pram2 = new byte[0x40];
            clock = 0;
            monochromepalette = new uint[] {
                0xFFF5F5F5u,
                0xFF666666u,
                0xFF444444u,
                0xFF000000u
            };
            frame = new uint[FramePixelCount];
            publishedFrame = new uint[FramePixelCount];
            ModeFlag = 2;
            VRAMBank = 0;
        }

        public bool TryCopyPublishedFrame(int[] destination, ref long sequence)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (destination.Length != FramePixelCount)
            {
                throw new ArgumentException(
                    $"A frame snapshot must contain exactly {FramePixelCount} pixels.",
                    nameof(destination));
            }

            lock (framePublishLock)
            {
                if (sequence == publishedFrameSequence)
                {
                    return false;
                }

                Buffer.BlockCopy(publishedFrame, 0, destination, 0, FrameByteCount);
                sequence = publishedFrameSequence;
                return true;
            }
        }

        internal void ResetTiming()
        {
            clock = 0;
            LY = 0;
            ModeFlag = 2;
            FrameReady = false;
            CoincidenceFlag = LY == LYC;
            statInterruptLine = false;
            framecounter = 0;
            updaterequired = false;
        }

        public byte ReadStat()
        {
            int value = 0x80;
            value |= CoincidenceInterrupt ? 0x40 : 0;
            value |= OAMInterrupt ? 0x20 : 0;
            value |= VBlankInterrupt ? 0x10 : 0;
            value |= HBlankInterrupt ? 0x08 : 0;
            value |= CoincidenceFlag ? 0x04 : 0;
            value |= LCDEnable ? ModeFlag & 0x03 : 0;
            return (byte)value;
        }

        public void WriteStat(byte value)
        {
            CoincidenceInterrupt = (value & 0x40) != 0;
            OAMInterrupt = (value & 0x20) != 0;
            VBlankInterrupt = (value & 0x10) != 0;
            HBlankInterrupt = (value & 0x08) != 0;
            UpdateStatInterruptLine();
        }

        public void WriteLyc(byte value)
        {
            LYC = value;
            UpdateCoincidence();
            UpdateStatInterruptLine();
        }

        public void WriteLcdc(byte value)
        {
            bool wasEnabled = LCDEnable;
            LCDEnable = (value & 0x80) != 0;
            WindowTileMapSelect = (value & 0x40) != 0;
            WindowEnable = (value & 0x20) != 0;
            TileDataSelect = (value & 0x10) != 0;
            BackgroundTileMapSelect = (value & 0x08) != 0;
            ObjectSize = (value & 0x04) != 0;
            ObjectEnable = (value & 0x02) != 0;
            BackgroundEnable = (value & 0x01) != 0;

            if (wasEnabled && !LCDEnable) {
                clock = 0;
                LY = 0;
                ModeFlag = 0;
                FrameReady = false;
            } else if (!wasEnabled && LCDEnable) {
                clock = 0;
                LY = 0;
                ModeFlag = 2;
            }

            UpdateCoincidence();
            UpdateStatInterruptLine();
        }

        public void Tick()
        {
            if (!LCDEnable) {
                clock = 0;
                LY = 0;
                ModeFlag = 0;
                UpdateCoincidence();
                UpdateStatInterruptLine();
                return;
            }

            UpdateCoincidence();
            UpdateStatInterruptLine();
            clock++;

            switch (ModeFlag)
            {

                case 2:
                    if (clock >= 80) {
                        ModeFlag = 3;
                        clock = 0;
                    }
                    break;

                case 3:
                    if (clock >= 172) {

                        if (hdma.IsHBlank) {
                            hdma.PerformHBlank();
                        }
                        ModeFlag = 0;
                        clock = 0;

                        if (Frameskip == 0 || framecounter == Frameskip) {
                            RenderLine();
                            updaterequired = true;
                        } else {
                            updaterequired = false;
                        }
                    }
                    break;

                case 0:
                    if (clock >= 204) {
                        clock = 0;
                        LY++;
                        if (LY == 144) {

                            ModeFlag = 1;
                            interrupt.Request(1);

                            FrameReady = updaterequired;
                            if (updaterequired) {
                                PublishFrame();
                            }
                            framecounter = (framecounter + 1) % (Frameskip + 1);
                        } else {

                            ModeFlag = 2;
                        }
                    }
                    break;

                case 1:
                    if (clock >= 456) {
                        clock = 0;
                        LY++;
                        if (LY > 153) {

                            ModeFlag = 2;
                            LY = 0;
                        }
                    }
                    break;
            }

            UpdateCoincidence();
            UpdateStatInterruptLine();
        }

        private void UpdateCoincidence()
        {
            CoincidenceFlag = LY == LYC;
        }

        private void UpdateStatInterruptLine()
        {
            bool nextLine =
                (CoincidenceInterrupt && CoincidenceFlag) ||
                (LCDEnable && (
                    (OAMInterrupt && ModeFlag == 2) ||
                    (VBlankInterrupt && ModeFlag == 1) ||
                    (HBlankInterrupt && ModeFlag == 0)));
            if (nextLine && !statInterruptLine) {
                interrupt.Request(2);
            }

            statInterruptLine = nextLine;
        }

        private void PublishFrame()
        {
            lock (framePublishLock)
            {
                Buffer.BlockCopy(frame, 0, publishedFrame, 0, FrameByteCount);
                publishedFrameSequence++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RenderLine()
        {
            if (LCDEnable && LY < 144) {
                if (BackgroundEnable) {
                    RenderBackgroundLine();
                }
                if (WindowEnable && LY >= WY) {
                    RenderWindowLine();
                }
                if (ObjectEnable) {
                    RenderSpriteLine();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RenderBackgroundLine()
        {
            int mapaddress = BackgroundTileMapSelect ? 0x1C00 : 0x1800;
            int wrapx = SCX + 160 > 256 ? (SCX + 160) - 256 : 0;
            int wrapy = (LY + SCY) % 256;
            int displacementy = wrapy % 8;
            int row = (wrapy - displacementy) / 8;
            uint[] mapline = RenderTilemapLine(mapaddress, row, displacementy);
            Buffer.BlockCopy(mapline, SCX * 4, frame, LY * 160 * 4, (160 - wrapx) * 4);
            Buffer.BlockCopy(mapline, 0, frame, (LY * 160 + 160 - wrapx) * 4, wrapx * 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RenderWindowLine()
        {
            int mapaddress = WindowTileMapSelect ? 0x1C00 : 0x1800;
            int difference = LY - WY;
            int displacementy = difference % 8;
            int row = (difference - displacementy) / 8;
            uint[] mapline = RenderTilemapLine(mapaddress, row, displacementy);
            int wx = WX - 7;
            if (wx >= FrameWidth) {
                return;
            }
            if (wx < 0) {
                Buffer.BlockCopy(mapline, wx * -4, frame, LY * 160 * 4, (160 - (wx * -1)) * 4);
            } else {
                Buffer.BlockCopy(mapline, 0,  frame, (LY * 160 + wx) * 4, (160 - wx) * 4);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint[] RenderTilemapLine(int mapaddress, int row, int displacement)
        {
            uint[] mapline = new uint[256];
            for (int x = 0; x < 32; x++) {
                int address = mapaddress + 32 * row + x;
                int tileindex = vram[0, address];
                int tileaddress;
                uint[] tiledata;
                if (TileDataSelect) {
                    tileaddress = tileindex * 16;
                } else {
                    sbyte signedindex = (sbyte)tileindex;
                    tileaddress = 0x1000 + signedindex * 16;
                }
                if (hasColorFeatures) {
                    int tileattributes = vram[1, address];
                    tiledata = ReadTileLineColor(tileaddress, displacement, tileattributes, pram1);
                } else {
                    tiledata = ReadTileLine(tileaddress, displacement, BGP);
                }
                Buffer.BlockCopy(tiledata, 0, mapline, x * 32, 32);
            }
            return mapline;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RenderSpriteLine()
        {
            List<SpriteEntry> entries = new List<SpriteEntry>();
            int tolerance = ObjectSize ? 16 : 8;

            for (int i = 0; i < 40; i++) {
                SpriteEntry entry = new SpriteEntry();
                entry.Y = oam[i * 4] - 16;
                entry.X = oam[i * 4 + 1] - 8;
                entry.TileNumber = oam[i * 4 + 2];
                entry.Attributes = oam[i * 4 + 3];
                entry.TableIndex = i;

                if (entry.Y < 144 && entry.Y > LY - tolerance && entry.Y < LY + 1) {
                    entries.Add(entry);
                    if (entries.Count == 10) {
                        break;
                    }
                }
            }

            entries.Sort((left, right) => {
                if (hasColorFeatures || left.X == right.X) {
                    return right.TableIndex.CompareTo(left.TableIndex);
                }

                return right.X.CompareTo(left.X);
            });

            foreach (SpriteEntry entry in entries) {
                int displacementy = LY - entry.Y;
                int colorpalette = entry.Attributes & 7;
                int tilenumber = entry.TileNumber;
                int tilebank = (entry.Attributes >> 3) & 1;
                int palette = (entry.Attributes & 0x10) == 0x10 ? OBP1 : OBP0;
                bool flipx = (entry.Attributes & 0x20) == 0x20;
                bool flipy = (entry.Attributes & 0x40) == 0x40;
                bool behind = (entry.Attributes & 0x80) == 0x80;
                uint[] tileline;
                if (flipy) {
                    displacementy = tolerance - 1 - displacementy;
                }
                if (ObjectSize) {
                    tilenumber = (tilenumber & 0xFE) | (displacementy / 8);
                    displacementy %= 8;
                }
                if (hasColorFeatures) {
                    int attributes = colorpalette | (tilebank << 3);
                    tileline = ReadTileLineColor(tilenumber * 16, displacementy, attributes, pram2, true);
                } else {
                    tileline = ReadTileLine(tilenumber * 16, displacementy, palette, true);
                }
                if (flipx) {
                    Array.Reverse(tileline);
                }
                DrawTile(tileline, entry.X, LY, behind);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawTile(uint[] tileline, int x, int y, bool behindbackground = false)
        {
            for (int i = 0; i < 8; i++) {
                if (tileline[i] != 0 && x + i >= 0 && x + i < 160) {
                    int position = y * 160 + x + i;
                    if (!behindbackground ||
                        hasColorFeatures ||
                        frame[position] == GetPaletteEntry(0, BGP)) {
                        frame[position] = tileline[i];
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint[] ReadTileLine(int tileaddress, int displacement, int palette, bool alpha = false)
        {
            byte byte1 = vram[0, tileaddress + displacement * 2];
            byte byte2 = vram[0, tileaddress + displacement * 2 + 1];
            uint[] data = new uint[8];
            for (int x = 0; x < 8; x++) {
                int color = (((byte2 >> (7 - x)) & 1) << 1) + ((byte1 >> (7 - x)) & 1);
                if (!alpha || color != 0) {
                    data[x] = GetPaletteEntry(color, palette);
                }
            }
            return data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint[] ReadTileLineColor(int tileaddress, int displacement, int attributes, byte[] paletteram, bool alpha = false)
        {
            int bank = (attributes >> 3) & 1;
            bool hflip = (attributes & 0x20) == 0x20;
            bool vflip = (attributes & 0x40) == 0x40;
            int palette = attributes & 7;
            byte byte1;
            byte byte2;
            uint[] data = new uint[8];
            if (vflip) {
                displacement = 7 - displacement;
            }
            byte1 = vram[bank, tileaddress + displacement * 2];
            byte2 = vram[bank, tileaddress + displacement * 2 + 1];
            for (int x = 0; x < 8; x++) {
                int color = (((byte2 >> (7 - x)) & 1) << 1) + ((byte1 >> (7 - x)) & 1);
                if (!alpha || color != 0) {
                    data[x] = GetPaletteEntryColor(color, paletteram, palette);
                }
            }
            if (hflip) {
                Array.Reverse(data);
            }
            return data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint GetPaletteEntry(int index, int palette)
        {
            return monochromepalette[(palette >> (index * 2)) & 3];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint GetPaletteEntryColor(int index, byte[] ram, int palette)
        {
            int position = palette * 8 + index * 2;
            uint entry = (uint)(ram[position] |
                               (ram[position + 1] << 8));
            uint value = 0xFF000000;
            value |= ((entry & 0x1F) * 8) << 16;
            value |= (((entry >> 5) & 0x1F) * 8) << 8;
            value |= ((entry >> 10) & 0x1F) * 8;
            return value;
        }

        public byte ReadVRAM(int address)
        {
            if (LCDEnable && ModeFlag == 3) {
                return 0xFF;
            }
            return vram[VRAMBank, address];
        }

        public void WriteVRAM(int address, byte value)
        {
            if (LCDEnable && ModeFlag == 3) {
                return;
            }
            vram[VRAMBank, address] = value;
        }

        public byte ReadOAM(int address)
        {
            if (LCDEnable && (ModeFlag == 2 || ModeFlag == 3)) {
                return 0xFF;
            }
            return oam[address];
        }

        public void WriteOAM(int address, byte value)
        {
            if (LCDEnable && (ModeFlag == 2 || ModeFlag == 3)) {
                return;
            }
            oam[address] = value;
        }

        public byte ReadPRAM(int index)
        {
            if (index == 0) {
                return pram1[BackgroundPaletteIndex & 0x3F];
            }
            return pram2[ObjectPaletteIndex & 0x3F];
        }

        public void WritePRAM(int index, byte value)
        {
            if (index == 0) {
                int paletteIndex = BackgroundPaletteIndex & 0x3F;
                pram1[paletteIndex] = value;
                if (BackgroundPaletteAI) {
                    BackgroundPaletteIndex = (paletteIndex + 1) & 0x3F;
                }
            } else {
                int paletteIndex = ObjectPaletteIndex & 0x3F;
                pram2[paletteIndex] = value;
                if (ObjectPaletteAI) {
                    ObjectPaletteIndex = (paletteIndex + 1) & 0x3F;
                }
            }
        }

    }
}
