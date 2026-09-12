using AetherBoy.Runtime.Video;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class PresentationStatisticsTests
{
    [TestMethod]
    public void BothPlatformsMeasurePresentationAndDiscardIdleIntervals()
    {
        var statistics = new PresentationStatistics();
        for (int frame = 0; frame <= 120; frame++) statistics.Presented(frame * 1000d / 60);
        var metrics = statistics.Read(2000);
        Assert.AreEqual(60d, metrics.FramesPerSecond, .001);
        Assert.AreEqual(1000d / 60, metrics.P95Ms, .001);
        Assert.AreEqual(default(PresentationMetrics), statistics.Read(3000));
        statistics.Presented(3000);
        Assert.AreEqual(default(PresentationMetrics), statistics.Read(3000));
        statistics.Reset();
        Assert.AreEqual(default(PresentationMetrics), statistics.Read(0));
    }
}
