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

    private static Video CreateVideo(out Interrupt interrupt)
    {
        var cpu = new CPU();
        interrupt = new Interrupt(cpu);
        return new Video(interrupt, new HDMA(null!), hasColorFeatures: false);
    }

    private static void Tick(Video video, int count)
    {
        for (int i = 0; i < count; i++)
        {
            video.Tick();
        }
    }
}
