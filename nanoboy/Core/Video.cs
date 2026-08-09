using System;
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
        private int mode3Duration;
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
        public int CurrentMode3Duration => mode3Duration;

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
            mode3Duration = 172;
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
            mode3Duration = 172;
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
                mode3Duration = 172;
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
                        mode3Duration = CalculateMode3Duration();
                    }
                    break;

                case 3:
                    if (clock >= mode3Duration) {

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
                    if (clock >= EmulationClock.DotsPerScanline - 80 - mode3Duration) {
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

        private int CalculateMode3Duration()
        {
            int duration = 172 + (SCX & 7);
            bool windowVisible =
                WindowEnable &&
                LY >= WY &&
                WX <= 166 &&
                (hasColorFeatures || BackgroundEnable);
            if (windowVisible) {
                duration += 6;
                if (WX == 0 && (SCX & 7) != 0) {
                    duration--;
                }
            }

            if (!ObjectEnable) {
                return duration;
            }

            Span<int> selectedX = stackalloc int[10];
            int selectedCount = 0;
            int spriteHeight = ObjectSize ? 16 : 8;
            for (int tableIndex = 0; tableIndex < 40 && selectedCount < selectedX.Length; tableIndex++) {
                int spriteY = oam[tableIndex * 4] - 16;
                if (LY >= spriteY && LY < spriteY + spriteHeight) {
                    selectedX[selectedCount++] = oam[tableIndex * 4 + 1];
                }
            }

            for (int left = 0; left < selectedCount - 1; left++) {
                for (int right = left + 1; right < selectedCount; right++) {
                    if (selectedX[left] > selectedX[right]) {
                        (selectedX[left], selectedX[right]) = (selectedX[right], selectedX[left]);
                    }
                }
            }

            Span<int> consideredTiles = stackalloc int[10];
            consideredTiles.Fill(int.MinValue);
            int consideredCount = 0;
            int windowX = WX - 7;
            for (int sprite = 0; sprite < selectedCount; sprite++) {
                int oamX = selectedX[sprite];
                if (oamX == 0) {
                    duration += 11;
                    continue;
                }
                if (oamX >= 168) {
                    continue;
                }

                int screenX = oamX - 8;
                bool usesWindow = windowVisible && screenX >= windowX;
                int tilePosition;
                int tileKey;
                if (usesWindow) {
                    int windowPixel = screenX - windowX;
                    tilePosition = windowPixel & 7;
                    tileKey = 0x100 | (windowPixel >> 3);
                } else {
                    int backgroundPixel = (screenX + SCX) & 0xFF;
                    tilePosition = backgroundPixel & 7;
                    tileKey = backgroundPixel >> 3;
                }

                bool firstSpriteForTile = true;
                for (int considered = 0; considered < consideredCount; considered++) {
                    if (consideredTiles[considered] == tileKey) {
                        firstSpriteForTile = false;
                        break;
                    }
                }
                if (firstSpriteForTile) {
                    consideredTiles[consideredCount++] = tileKey;
                    duration += Math.Max(0, 5 - tilePosition);
                }
                duration += 6;
            }

            return Math.Min(duration, 289);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RenderLine()
        {
            if (!LCDEnable || LY >= FrameHeight) {
                return;
            }

            Span<byte> backgroundColorIndices = stackalloc byte[FrameWidth];
            Span<byte> backgroundPriorities = stackalloc byte[FrameWidth];
            if (hasColorFeatures || BackgroundEnable) {
                RenderBackgroundLine(backgroundColorIndices, backgroundPriorities);
            } else {
                uint colorZero = GetPaletteEntry(0, BGP);
                frame.AsSpan(LY * FrameWidth, FrameWidth).Fill(colorZero);
                backgroundColorIndices.Clear();
                backgroundPriorities.Clear();
            }

            if (WindowEnable && LY >= WY && (hasColorFeatures || BackgroundEnable)) {
                RenderWindowLine(backgroundColorIndices, backgroundPriorities);
            }
            if (ObjectEnable) {
                RenderSpriteLine(backgroundColorIndices, backgroundPriorities);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RenderBackgroundLine(
            Span<byte> backgroundColorIndices,
            Span<byte> backgroundPriorities)
        {
            int mapAddress = BackgroundTileMapSelect ? 0x1C00 : 0x1800;
            int sourceY = (LY + SCY) & 0xFF;
            for (int screenX = 0; screenX < FrameWidth; screenX++) {
                int sourceX = (screenX + SCX) & 0xFF;
                RenderTilemapPixel(
                    mapAddress,
                    sourceX,
                    sourceY,
                    screenX,
                    backgroundColorIndices,
                    backgroundPriorities);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RenderWindowLine(
            Span<byte> backgroundColorIndices,
            Span<byte> backgroundPriorities)
        {
            int windowX = WX - 7;
            if (windowX >= FrameWidth) {
                return;
            }

            int mapAddress = WindowTileMapSelect ? 0x1C00 : 0x1800;
            int sourceY = LY - WY;
            int firstScreenX = Math.Max(0, windowX);
            for (int screenX = firstScreenX; screenX < FrameWidth; screenX++) {
                RenderTilemapPixel(
                    mapAddress,
                    screenX - windowX,
                    sourceY,
                    screenX,
                    backgroundColorIndices,
                    backgroundPriorities);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RenderTilemapPixel(
            int mapAddress,
            int sourceX,
            int sourceY,
            int screenX,
            Span<byte> backgroundColorIndices,
            Span<byte> backgroundPriorities)
        {
            int tileMapAddress = mapAddress + ((sourceY >> 3) * 32) + (sourceX >> 3);
            int tileIndex = vram[0, tileMapAddress];
            int tileAddress = TileDataSelect
                ? tileIndex * 16
                : 0x1000 + (sbyte)tileIndex * 16;
            int attributes = hasColorFeatures ? vram[1, tileMapAddress] : 0;
            int tileY = sourceY & 7;
            int tileX = sourceX & 7;
            if ((attributes & 0x40) != 0) {
                tileY = 7 - tileY;
            }
            if ((attributes & 0x20) != 0) {
                tileX = 7 - tileX;
            }

            int bank = (attributes >> 3) & 1;
            byte low = vram[bank, tileAddress + tileY * 2];
            byte high = vram[bank, tileAddress + tileY * 2 + 1];
            int bit = 7 - tileX;
            int colorIndex = (((high >> bit) & 1) << 1) | ((low >> bit) & 1);
            backgroundColorIndices[screenX] = (byte)colorIndex;
            backgroundPriorities[screenX] = (byte)((attributes >> 7) & 1);
            frame[LY * FrameWidth + screenX] = hasColorFeatures
                ? GetPaletteEntryColor(colorIndex, pram1, attributes & 7)
                : GetPaletteEntry(colorIndex, BGP);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RenderSpriteLine(
            ReadOnlySpan<byte> backgroundColorIndices,
            ReadOnlySpan<byte> backgroundPriorities)
        {
            Span<SpriteEntry> entries = stackalloc SpriteEntry[10];
            int entryCount = 0;
            int spriteHeight = ObjectSize ? 16 : 8;

            for (int i = 0; i < 40; i++) {
                var entry = new SpriteEntry {
                    Y = oam[i * 4] - 16,
                    X = oam[i * 4 + 1] - 8,
                    TileNumber = oam[i * 4 + 2],
                    Attributes = oam[i * 4 + 3],
                    TableIndex = i
                };

                if (LY >= entry.Y && LY < entry.Y + spriteHeight) {
                    entries[entryCount++] = entry;
                    if (entryCount == entries.Length) {
                        break;
                    }
                }
            }

            for (int left = 0; left < entryCount - 1; left++) {
                for (int right = left + 1; right < entryCount; right++) {
                    if (!DrawsBefore(entries[left], entries[right])) {
                        (entries[left], entries[right]) = (entries[right], entries[left]);
                    }
                }
            }

            for (int entryIndex = 0; entryIndex < entryCount; entryIndex++) {
                SpriteEntry entry = entries[entryIndex];
                int displacementy = LY - entry.Y;
                int tilenumber = entry.TileNumber;
                int tilebank = (entry.Attributes >> 3) & 1;
                int palette = (entry.Attributes & 0x10) == 0x10 ? OBP1 : OBP0;
                bool flipx = (entry.Attributes & 0x20) == 0x20;
                bool flipy = (entry.Attributes & 0x40) == 0x40;
                bool behind = (entry.Attributes & 0x80) == 0x80;
                if (flipy) {
                    displacementy = spriteHeight - 1 - displacementy;
                }
                if (ObjectSize) {
                    tilenumber = (tilenumber & 0xFE) | (displacementy / 8);
                    displacementy %= 8;
                }

                int tileAddress = tilenumber * 16 + displacementy * 2;
                byte low = vram[tilebank, tileAddress];
                byte high = vram[tilebank, tileAddress + 1];
                for (int pixel = 0; pixel < 8; pixel++) {
                    int screenX = entry.X + pixel;
                    if ((uint)screenX >= FrameWidth) {
                        continue;
                    }

                    int sourceX = flipx ? 7 - pixel : pixel;
                    int bit = 7 - sourceX;
                    int colorIndex = (((high >> bit) & 1) << 1) | ((low >> bit) & 1);
                    if (colorIndex == 0) {
                        continue;
                    }

                    bool backgroundIsOpaque = backgroundColorIndices[screenX] != 0;
                    bool hidden = hasColorFeatures
                        ? BackgroundEnable && backgroundIsOpaque &&
                          (backgroundPriorities[screenX] != 0 || behind)
                        : behind && backgroundIsOpaque;
                    if (!hidden) {
                        frame[LY * FrameWidth + screenX] = hasColorFeatures
                            ? GetPaletteEntryColor(colorIndex, pram2, entry.Attributes & 7)
                            : GetPaletteEntry(colorIndex, palette);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool DrawsBefore(SpriteEntry left, SpriteEntry right)
        {
            if (hasColorFeatures || left.X == right.X) {
                return left.TableIndex > right.TableIndex;
            }

            return left.X > right.X;
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

        internal byte[] CaptureStatePayload()
        {
            lock (framePublishLock) {
                return StatePayload.Write(writer => {
                    writer.Write(hasColorFeatures);
                    writer.Write(FrameReady);
                    writer.Write(Frameskip);
                    writer.Write(LCDEnable);
                    writer.Write(WindowTileMapSelect);
                    writer.Write(WindowEnable);
                    writer.Write(TileDataSelect);
                    writer.Write(BackgroundTileMapSelect);
                    writer.Write(ObjectSize);
                    writer.Write(ObjectEnable);
                    writer.Write(BackgroundEnable);
                    writer.Write(CoincidenceInterrupt);
                    writer.Write(OAMInterrupt);
                    writer.Write(VBlankInterrupt);
                    writer.Write(HBlankInterrupt);
                    writer.Write(CoincidenceFlag);
                    writer.Write(ModeFlag);
                    writer.Write(SCY);
                    writer.Write(SCX);
                    writer.Write(LY);
                    writer.Write(LYC);
                    writer.Write(WY);
                    writer.Write(WX);
                    writer.Write(BGP);
                    writer.Write(OBP0);
                    writer.Write(OBP1);
                    writer.Write(BackgroundPaletteAI);
                    writer.Write(BackgroundPaletteIndex);
                    writer.Write(ObjectPaletteAI);
                    writer.Write(ObjectPaletteIndex);
                    writer.Write(VRAMBank);
                    writer.Write(clock);
                    writer.Write(mode3Duration);
                    writer.Write(publishedFrameSequence);
                    writer.Write(statInterruptLine);
                    writer.Write(framecounter);
                    writer.Write(updaterequired);

                    for (int bank = 0; bank < 2; bank++) {
                        for (int offset = 0; offset < 0x2000; offset++) {
                            writer.Write(vram[bank, offset]);
                        }
                    }
                    writer.Write(oam);
                    writer.Write(pram1);
                    writer.Write(pram2);
                    WriteUInt32Array(writer, monochromepalette);
                    WriteUInt32Array(writer, frame);
                    WriteUInt32Array(writer, publishedFrame);
                });
            }
        }

        internal Action PrepareStateRestore(byte[] payload)
        {
            return StatePayload.Read(payload, reader => {
                bool stateHasColorFeatures = StatePayload.ReadBoolean(reader);
                if (stateHasColorFeatures != hasColorFeatures) {
                    throw new InvalidOperationException("Video state targets a different hardware model.");
                }

                bool nextFrameReady = StatePayload.ReadBoolean(reader);
                int nextFrameskip = reader.ReadInt32();
                bool nextLcdEnable = StatePayload.ReadBoolean(reader);
                bool nextWindowTileMapSelect = StatePayload.ReadBoolean(reader);
                bool nextWindowEnable = StatePayload.ReadBoolean(reader);
                bool nextTileDataSelect = StatePayload.ReadBoolean(reader);
                bool nextBackgroundTileMapSelect = StatePayload.ReadBoolean(reader);
                bool nextObjectSize = StatePayload.ReadBoolean(reader);
                bool nextObjectEnable = StatePayload.ReadBoolean(reader);
                bool nextBackgroundEnable = StatePayload.ReadBoolean(reader);
                bool nextCoincidenceInterrupt = StatePayload.ReadBoolean(reader);
                bool nextOamInterrupt = StatePayload.ReadBoolean(reader);
                bool nextVBlankInterrupt = StatePayload.ReadBoolean(reader);
                bool nextHBlankInterrupt = StatePayload.ReadBoolean(reader);
                bool nextCoincidenceFlag = StatePayload.ReadBoolean(reader);
                int nextModeFlag = reader.ReadInt32();
                int nextScy = reader.ReadInt32();
                int nextScx = reader.ReadInt32();
                int nextLy = reader.ReadInt32();
                int nextLyc = reader.ReadInt32();
                int nextWy = reader.ReadInt32();
                int nextWx = reader.ReadInt32();
                int nextBgp = reader.ReadInt32();
                int nextObp0 = reader.ReadInt32();
                int nextObp1 = reader.ReadInt32();
                bool nextBackgroundPaletteAi = StatePayload.ReadBoolean(reader);
                int nextBackgroundPaletteIndex = reader.ReadInt32();
                bool nextObjectPaletteAi = StatePayload.ReadBoolean(reader);
                int nextObjectPaletteIndex = reader.ReadInt32();
                int nextVramBank = reader.ReadInt32();
                int nextClock = reader.ReadInt32();
                int nextMode3Duration = reader.ReadInt32();
                long nextPublishedFrameSequence = reader.ReadInt64();
                bool nextStatInterruptLine = StatePayload.ReadBoolean(reader);
                int nextFrameCounter = reader.ReadInt32();
                bool nextUpdateRequired = StatePayload.ReadBoolean(reader);

                StatePayload.RequireRange(nextFrameskip, 0, 60, nameof(Frameskip));
                StatePayload.RequireRange(nextModeFlag, 0, 3, nameof(ModeFlag));
                StatePayload.RequireRange(nextScy, 0, 0xFF, nameof(SCY));
                StatePayload.RequireRange(nextScx, 0, 0xFF, nameof(SCX));
                StatePayload.RequireRange(nextLy, 0, 153, nameof(LY));
                StatePayload.RequireRange(nextLyc, 0, 0xFF, nameof(LYC));
                StatePayload.RequireRange(nextWy, 0, 0xFF, nameof(WY));
                StatePayload.RequireRange(nextWx, 0, 0xFF, nameof(WX));
                StatePayload.RequireRange(nextBgp, 0, 0xFF, nameof(BGP));
                StatePayload.RequireRange(nextObp0, 0, 0xFF, nameof(OBP0));
                StatePayload.RequireRange(nextObp1, 0, 0xFF, nameof(OBP1));
                StatePayload.RequireRange(nextBackgroundPaletteIndex, 0, 0x3F, nameof(BackgroundPaletteIndex));
                StatePayload.RequireRange(nextObjectPaletteIndex, 0, 0x3F, nameof(ObjectPaletteIndex));
                StatePayload.RequireRange(nextVramBank, 0, 1, nameof(VRAMBank));
                StatePayload.RequireRange(nextClock, 0, EmulationClock.DotsPerScanline - 1, nameof(clock));
                StatePayload.RequireRange(nextMode3Duration, 172, 289, nameof(mode3Duration));
                if (nextPublishedFrameSequence < 0) {
                    throw new InvalidOperationException("Video frame sequence cannot be negative.");
                }
                StatePayload.RequireRange(nextFrameCounter, 0, nextFrameskip, nameof(framecounter));

                byte[] nextVram = StatePayload.ReadBytes(reader, 2 * 0x2000, "VRAM");
                byte[] nextOam = StatePayload.ReadBytes(reader, 0xA0, "OAM");
                byte[] nextPram1 = StatePayload.ReadBytes(reader, 0x40, "background palette RAM");
                byte[] nextPram2 = StatePayload.ReadBytes(reader, 0x40, "object palette RAM");
                uint[] nextMonochromePalette = ReadUInt32Array(reader, 4);
                uint[] nextFrame = ReadUInt32Array(reader, FramePixelCount);
                uint[] nextPublishedFrame = ReadUInt32Array(reader, FramePixelCount);

                return (Action)(() => {
                    FrameReady = nextFrameReady;
                    Frameskip = nextFrameskip;
                    LCDEnable = nextLcdEnable;
                    WindowTileMapSelect = nextWindowTileMapSelect;
                    WindowEnable = nextWindowEnable;
                    TileDataSelect = nextTileDataSelect;
                    BackgroundTileMapSelect = nextBackgroundTileMapSelect;
                    ObjectSize = nextObjectSize;
                    ObjectEnable = nextObjectEnable;
                    BackgroundEnable = nextBackgroundEnable;
                    CoincidenceInterrupt = nextCoincidenceInterrupt;
                    OAMInterrupt = nextOamInterrupt;
                    VBlankInterrupt = nextVBlankInterrupt;
                    HBlankInterrupt = nextHBlankInterrupt;
                    CoincidenceFlag = nextCoincidenceFlag;
                    ModeFlag = nextModeFlag;
                    SCY = nextScy;
                    SCX = nextScx;
                    LY = nextLy;
                    LYC = nextLyc;
                    WY = nextWy;
                    WX = nextWx;
                    BGP = nextBgp;
                    OBP0 = nextObp0;
                    OBP1 = nextObp1;
                    BackgroundPaletteAI = nextBackgroundPaletteAi;
                    BackgroundPaletteIndex = nextBackgroundPaletteIndex;
                    ObjectPaletteAI = nextObjectPaletteAi;
                    ObjectPaletteIndex = nextObjectPaletteIndex;
                    VRAMBank = nextVramBank;
                    clock = nextClock;
                    mode3Duration = nextMode3Duration;
                    statInterruptLine = nextStatInterruptLine;
                    framecounter = nextFrameCounter;
                    updaterequired = nextUpdateRequired;

                    int position = 0;
                    for (int bank = 0; bank < 2; bank++) {
                        for (int offset = 0; offset < 0x2000; offset++) {
                            vram[bank, offset] = nextVram[position++];
                        }
                    }
                    Array.Copy(nextOam, oam, oam.Length);
                    Array.Copy(nextPram1, pram1, pram1.Length);
                    Array.Copy(nextPram2, pram2, pram2.Length);
                    Array.Copy(nextMonochromePalette, monochromepalette, monochromepalette.Length);
                    Array.Copy(nextFrame, frame, frame.Length);
                    lock (framePublishLock) {
                        Array.Copy(nextPublishedFrame, publishedFrame, publishedFrame.Length);
                        publishedFrameSequence = nextPublishedFrameSequence;
                    }
                });
            });
        }

        private static void WriteUInt32Array(System.IO.BinaryWriter writer, uint[] values)
        {
            for (int index = 0; index < values.Length; index++) {
                writer.Write(values[index]);
            }
        }

        private static uint[] ReadUInt32Array(System.IO.BinaryReader reader, int length)
        {
            var values = new uint[length];
            for (int index = 0; index < values.Length; index++) {
                values[index] = reader.ReadUInt32();
            }
            return values;
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
            if (LCDEnable && ModeFlag == 3) {
                return 0xFF;
            }
            if (index == 0) {
                return pram1[BackgroundPaletteIndex & 0x3F];
            }
            return pram2[ObjectPaletteIndex & 0x3F];
        }

        public void WritePRAM(int index, byte value)
        {
            bool writeBlocked = LCDEnable && ModeFlag == 3;
            if (index == 0) {
                int paletteIndex = BackgroundPaletteIndex & 0x3F;
                if (!writeBlocked) {
                    pram1[paletteIndex] = value;
                }
                if (BackgroundPaletteAI) {
                    BackgroundPaletteIndex = (paletteIndex + 1) & 0x3F;
                }
            } else {
                int paletteIndex = ObjectPaletteIndex & 0x3F;
                if (!writeBlocked) {
                    pram2[paletteIndex] = value;
                }
                if (ObjectPaletteAI) {
                    ObjectPaletteIndex = (paletteIndex + 1) & 0x3F;
                }
            }
        }

    }
}
