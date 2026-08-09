using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class VideoTimingTests
{
    [TestMethod]
    public void VBlankInterrupt_IsRequestedWhenLine144Begins()
    {
        Video video = CreateVideo(out Interrupt interrupt);

        Tick(video, 144 * EmulationClock.DotsPerScanline);

        Assert.AreEqual(144, video.LY);
        Assert.AreEqual(1, video.ModeFlag);
        Assert.AreEqual(1, interrupt.IF & 1);
        Assert.IsTrue(video.FrameReady);
    }

    [TestMethod]
    public void DmgOamStatSource_AlsoRisesWhenVBlankBegins()
    {
        Video dmg = CreateVideo(hasColorFeatures: false, out Interrupt dmgInterrupt);
        Video cgb = CreateVideo(hasColorFeatures: true, out Interrupt cgbInterrupt);

        Tick(dmg, 143 * EmulationClock.DotsPerScanline + 80);
        Tick(cgb, 143 * EmulationClock.DotsPerScanline + 80);
        dmg.WriteStat(0x20);
        cgb.WriteStat(0x20);
        dmgInterrupt.IF = 0;
        cgbInterrupt.IF = 0;

        Tick(dmg, EmulationClock.DotsPerScanline - 80);
        Tick(cgb, EmulationClock.DotsPerScanline - 80);

        Assert.AreEqual(144, dmg.LY);
        Assert.AreEqual(2, dmgInterrupt.IF & 2);
        Assert.AreEqual(0, cgbInterrupt.IF & 2);
    }

    [TestMethod]
    public void CompleteFrame_ReturnsToLineZeroAndPublishesSnapshot()
    {
        Video video = CreateVideo(out _);
        int[] snapshot = new int[Video.FramePixelCount];
        long sequence = 0;

        Tick(video, EmulationClock.DotsPerFrame);

        Assert.AreEqual(0, video.LY);
        Assert.AreEqual(2, video.ModeFlag);
        Assert.IsTrue(video.TryCopyPublishedFrame(snapshot, ref sequence));
        Assert.AreEqual(1L, sequence);
    }

    [TestMethod]
    public void MonochromePalettes_PreservePackedArgbValues()
    {
        Video video = CreateVideo(out _);
        uint[][] expected =
        {
            new uint[] { 0xFFF5F5F5u, 0xFFA0A0A0u, 0xFF505050u, 0xFF000000u },
            new uint[] { 0xFF9BBC0Fu, 0xFF8BAC0Fu, 0xFF306230u, 0xFF0F380Fu },
            new uint[] { 0xFF00FFCDu, 0xFF00A597u, 0xFF00665Eu, 0xFF00332Fu },
            new uint[] { 0xFFF5EA8Cu, 0xFFD4B055u, 0xFF8C5620u, 0xFF381900u },
            new uint[] { 0xFF00FFFFu, 0xFFFF00FFu, 0xFF800080u, 0xFF000040u }
        };

        for (int palette = 0; palette < expected.Length; palette++)
        {
            video.SetMonochromePalette(palette);
            for (int color = 0; color < expected[palette].Length; color++)
            {
                Assert.AreEqual(
                    expected[palette][color],
                    video.ReadMonochromePaletteColor(color),
                    $"Palette {palette}, color {color}");
            }
        }
    }

    [TestMethod]
    public void StatInterrupt_UsesTheCombinedLineRisingEdge()
    {
        Video video = CreateVideo(out Interrupt interrupt);
        video.WriteStat(0x28);
        Assert.AreEqual(2, interrupt.IF & 2, "Enabling the current Mode 2 source raises STAT.");

        interrupt.IF = 0;
        Tick(video, 80);
        Assert.AreEqual(3, video.ModeFlag);
        Tick(video, 172);
        Assert.AreEqual(0, video.ModeFlag);
        Assert.AreEqual(2, interrupt.IF & 2, "Entering HBlank raises the combined STAT line.");
    }

    [TestMethod]
    public void LcdDisable_ImmediatelyResetsLyAndMakesVideoMemoryAccessible()
    {
        Video video = CreateVideo(out _);
        video.WriteVRAMDirect(0, 0, 0x11);
        video.WriteOAMDirect(0, 0x22);

        Tick(video, 100);
        Assert.AreEqual(3, video.ModeFlag);
        Assert.AreEqual(0xFF, video.ReadVRAM(0));
        Assert.AreEqual(0xFF, video.ReadOAM(0));

        video.WriteLcdc(0x00);
        Assert.AreEqual(0, video.LY);
        Assert.AreEqual(0, video.ModeFlag);
        Assert.AreEqual(0x11, video.ReadVRAM(0));
        Assert.AreEqual(0x22, video.ReadOAM(0));
    }

    [TestMethod]
    public void LcdDisable_FreezesCoincidenceUntilTheComparisonClockRestarts()
    {
        Video video = CreateVideo(out Interrupt interrupt);
        video.WriteStat(0x40);
        video.WriteLyc(0);
        Assert.IsTrue(video.CoincidenceFlag);

        video.WriteLcdc(0x00);
        interrupt.IF = 0;
        video.WriteLyc(1);
        Assert.IsTrue(video.CoincidenceFlag);
        Assert.AreEqual(0, interrupt.IF & 2);

        video.WriteLcdc(0x80);
        Assert.IsFalse(video.CoincidenceFlag);
        Assert.AreEqual(0, video.ModeFlag);

        video.WriteLcdc(0x00);
        video.WriteLyc(0);
        interrupt.IF = 0;
        video.WriteLcdc(0x80);
        Assert.IsTrue(video.CoincidenceFlag);
        Assert.AreEqual(2, interrupt.IF & 2);
    }

    [TestMethod]
    public void ReenabledLcd_StartsLineZeroInModeZeroBeforeModeThree()
    {
        Video video = CreateVideo(out _);
        video.WriteLcdc(0x00);
        video.WriteLcdc(0x91);

        Assert.AreEqual(0, video.ModeFlag);
        Tick(video, 79);
        Assert.AreEqual(0, video.ModeFlag);
        Tick(video, 1);
        Assert.AreEqual(3, video.ModeFlag);

        Tick(video, video.CurrentMode3Duration);
        Assert.AreEqual(0, video.ModeFlag);
        Tick(video, 454 - 80 - video.CurrentMode3Duration);
        Assert.AreEqual(1, video.LY);
        Assert.AreEqual(2, video.ModeFlag);
    }

    [TestMethod]
    public void OamScan_ExposesTheFinalTwoDotBoundaryWindow()
    {
        Video video = CreateVideo(out _);
        video.WriteOAMDirect(0, 0x11);

        Tick(video, 77);
        video.WriteOAM(0, 0x22);
        Assert.AreEqual(0x11, video.ReadOAMDirect(0));

        Tick(video, 1);
        video.WriteOAM(0, 0x33);
        Assert.AreEqual(0x33, video.ReadOAMDirect(0));

        Tick(video, 2);
        video.WriteOAM(0, 0x44);
        Assert.AreEqual(0x33, video.ReadOAMDirect(0));
    }

    [TestMethod]
    public void OffscreenWindowAndPaletteAutoIncrement_DoNotOverflowBuffers()
    {
        Video video = CreateVideo(out _);
        video.WriteLcdc(0xB1);
        video.WX = 255;
        Tick(video, 252);
        Assert.AreEqual(0, video.ModeFlag);

        video.BackgroundPaletteIndex = 0x3F;
        video.BackgroundPaletteAI = true;
        video.WritePRAM(0, 0x7A);
        Assert.AreEqual(0, video.BackgroundPaletteIndex);
        video.BackgroundPaletteIndex = 0x3F;
        Assert.AreEqual(0x7A, video.ReadPRAM(0));

        video.ObjectPaletteIndex = 0x3F;
        video.ObjectPaletteAI = true;
        video.WritePRAM(1, 0x6B);
        Assert.AreEqual(0, video.ObjectPaletteIndex);
    }

    [TestMethod]
    public void FineScroll_ExtendsModeThreeAndShortensHBlank()
    {
        Video video = CreateVideo(out _);
        video.SCX = 7;

        Tick(video, 80);
        Assert.AreEqual(179, video.CurrentMode3Duration);
        Tick(video, 178);
        Assert.AreEqual(3, video.ModeFlag);
        Tick(video, 1);
        Assert.AreEqual(0, video.ModeFlag);
        Tick(video, 196);
        Assert.AreEqual(0, video.ModeFlag);
        Tick(video, 1);
        Assert.AreEqual(1, video.LY);
        Assert.AreEqual(2, video.ModeFlag);
    }

    [TestMethod]
    public void VisibleWindow_AddsTheSixDotFetcherRestart()
    {
        Video video = CreateVideo(out _);
        video.WY = 0;
        video.WX = 7;
        video.WriteLcdc(0xB1);

        Tick(video, 80);

        Assert.AreEqual(178, video.CurrentMode3Duration);
    }

    [TestMethod]
    public void SpriteFetch_AddsTileWaitAndFetchPenalties()
    {
        Video video = CreateVideo(out _);
        video.WriteOAMDirect(0, 16);
        video.WriteOAMDirect(1, 8);
        video.WriteLcdc(0x93);

        Tick(video, 80);

        Assert.AreEqual(183, video.CurrentMode3Duration);
    }

    [TestMethod]
    public void OverlappingSprites_ShareTheTileWaitButKeepTheirFetchPenalty()
    {
        Video video = CreateVideo(out _);
        for (int sprite = 0; sprite < 2; sprite++) {
            video.WriteOAMDirect(sprite * 4, 16);
            video.WriteOAMDirect(sprite * 4 + 1, 0);
        }
        video.WriteLcdc(0x93);

        Tick(video, 80);

        Assert.AreEqual(189, video.CurrentMode3Duration);
    }

    [TestMethod]
    public void CgbPaletteRam_BlocksModeThreeDataButStillAutoIncrementsTheIndex()
    {
        Video video = CreateVideo(hasColorFeatures: true, out _);
        video.BackgroundPaletteIndex = 0;
        video.WritePRAM(0, 0x5A);
        video.BackgroundPaletteIndex = 0;
        video.BackgroundPaletteAI = true;
        Tick(video, 80);

        Assert.AreEqual(0xFF, video.ReadPRAM(0));
        video.WritePRAM(0, 0xA5);
        Assert.AreEqual(1, video.BackgroundPaletteIndex);
        Tick(video, video.CurrentMode3Duration);

        video.BackgroundPaletteAI = false;
        video.BackgroundPaletteIndex = 0;
        Assert.AreEqual(0x5A, video.ReadPRAM(0));
        video.BackgroundPaletteIndex = 1;
        Assert.AreEqual(0x00, video.ReadPRAM(0));
    }

    [TestMethod]
    public void ModeThreeDuration_RemainsWithinTheDocumentedHardwareMaximum()
    {
        Video video = CreateVideo(out _);
        video.SCX = 7;
        video.WY = 0;
        video.WX = 7;
        for (int sprite = 0; sprite < 10; sprite++) {
            video.WriteOAMDirect(sprite * 4, 16);
            video.WriteOAMDirect(sprite * 4 + 1, 0);
        }
        video.WriteLcdc(0xB3);

        Tick(video, 80);

        Assert.IsLessThanOrEqualTo(289, video.CurrentMode3Duration);
    }

    [TestMethod]
    public void CgbBackgroundPriority_ObeysTheLcdcMasterPriorityBit()
    {
        Video priorityEnabled = CreatePriorityScene(hasColorFeatures: true, lcdc: 0x93);
        Video priorityDisabled = CreatePriorityScene(hasColorFeatures: true, lcdc: 0x92);
        int[] enabledFrame = RenderFrame(priorityEnabled);
        int[] disabledFrame = RenderFrame(priorityDisabled);

        Assert.AreEqual(unchecked((int)0xFFF80000u), enabledFrame[0],
            "A priority background pixel must cover the sprite while LCDC.0 is set.");
        Assert.AreEqual(unchecked((int)0xFF00F800u), disabledFrame[0],
            "CGB background pixels remain visible with LCDC.0 clear, but lose priority.");
    }

    [TestMethod]
    public void DmgSpritePriority_UsesTheRawBackgroundColorIndex()
    {
        Video video = CreateVideo(out _);
        video.BGP = 0x00;
        video.OBP0 = 0x0C;
        video.WriteVRAMDirect(0, 0x0000, 0x80);
        video.WriteVRAMDirect(0, 0x0010, 0x80);
        video.WriteOAMDirect(0, 16);
        video.WriteOAMDirect(1, 8);
        video.WriteOAMDirect(2, 1);
        video.WriteOAMDirect(3, 0x80);
        video.WriteLcdc(0x93);

        int[] frame = RenderFrame(video);

        Assert.AreEqual(unchecked((int)0xFFF5F5F5u), frame[0],
            "Background color 1 must remain opaque even when the palette maps it to color 0.");
    }

    [TestMethod]
    public void DmgBackgroundDisable_ClearsPriorityForBehindSprites()
    {
        Video video = CreateVideo(out _);
        video.OBP0 = 0x0C;
        video.WriteVRAMDirect(0, 0x0010, 0x80);
        video.WriteOAMDirect(0, 16);
        video.WriteOAMDirect(1, 8);
        video.WriteOAMDirect(2, 1);
        video.WriteOAMDirect(3, 0x80);
        video.WriteLcdc(0x92);

        int[] frame = RenderFrame(video);

        Assert.AreEqual(unchecked((int)0xFF000000u), frame[0]);
    }

    [TestMethod]
    public void RenderingACompleteFrame_DoesNotAllocatePerTileOrPerScanline()
    {
        Video video = CreateVideo(out _);
        Tick(video, EmulationClock.DotsPerFrame);

        long before = GC.GetAllocatedBytesForCurrentThread();
        Tick(video, EmulationClock.DotsPerFrame);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.IsLessThan(1_024L, allocated,
            $"A complete video frame allocated {allocated} bytes.");
    }

    private static Video CreateVideo(out Interrupt interrupt)
    {
        return CreateVideo(hasColorFeatures: false, out interrupt);
    }

    private static Video CreateVideo(bool hasColorFeatures, out Interrupt interrupt)
    {
        var cpu = new CPU();
        interrupt = new Interrupt(cpu);
        var video = new Video(interrupt, new HDMA(null!), hasColorFeatures);
        video.WriteLcdc(0x91);
        return video;
    }

    private static Video CreatePriorityScene(bool hasColorFeatures, byte lcdc)
    {
        Video video = CreateVideo(hasColorFeatures, out _);
        video.WriteLcdc(0x00);

        video.BackgroundPaletteIndex = 2;
        video.WritePRAM(0, 0x1F);
        video.BackgroundPaletteIndex = 3;
        video.WritePRAM(0, 0x00);
        video.ObjectPaletteIndex = 2;
        video.WritePRAM(1, 0xE0);
        video.ObjectPaletteIndex = 3;
        video.WritePRAM(1, 0x03);

        video.WriteVRAMDirect(0, 0x0000, 0x80);
        video.WriteVRAMDirect(1, 0x1800, 0x80);
        video.WriteVRAMDirect(0, 0x0010, 0x80);
        video.WriteOAMDirect(0, 16);
        video.WriteOAMDirect(1, 8);
        video.WriteOAMDirect(2, 1);
        video.WriteOAMDirect(3, 0x00);
        video.WriteLcdc(lcdc);
        return video;
    }

    private static int[] RenderFrame(Video video)
    {
        int[] snapshot = new int[Video.FramePixelCount];
        long sequence = 0;
        Tick(video, EmulationClock.DotsPerFrame);
        Assert.IsTrue(video.TryCopyPublishedFrame(snapshot, ref sequence));
        return snapshot;
    }

    private static void Tick(Video video, int count)
    {
        for (int i = 0; i < count; i++)
        {
            video.Tick();
        }
    }
}
