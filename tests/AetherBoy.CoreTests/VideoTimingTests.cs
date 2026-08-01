using System.Runtime.CompilerServices;
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

    private static Video CreateVideo(out Interrupt interrupt)
    {
        var cpu = new CPU();
        interrupt = new Interrupt(cpu);
        var rom = (ROM)RuntimeHelpers.GetUninitializedObject(typeof(ROM));
        return new Video(interrupt, new HDMA(null!), rom);
    }

    private static void Tick(Video video, int count)
    {
        for (int i = 0; i < count; i++)
        {
            video.Tick();
        }
    }
}
