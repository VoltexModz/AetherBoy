using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class EmulationClockTests
{
    [TestMethod]
    public void FrameDuration_MatchesGameBoyRefreshRate()
    {
        TimeSpan duration = EmulationClock.FrameDuration;

        Assert.AreEqual(16.7427062988d, duration.TotalMilliseconds, 0.0001d);
        Assert.AreEqual(59.7275005696d, 1d / duration.TotalSeconds, 0.001d);
        Assert.AreEqual(
            EmulationClock.FrameSeconds,
            duration.TotalSeconds,
            TimeSpan.FromTicks(1).TotalSeconds);
    }
}
