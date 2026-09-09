using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class LinuxFrontendOptionsTests
{
    [TestMethod]
    public void DefaultsCreatePlayableConfiguration()
    {
        var options = new LinuxFrontendOptions();

        var configuration = options.CreateEmulatorConfiguration(audioOutputAvailable: true);

        Assert.IsTrue(configuration.AudioEnabled);
        Assert.IsTrue(configuration.Channel1Enabled);
        Assert.IsTrue(configuration.Channel2Enabled);
        Assert.IsTrue(configuration.Channel3Enabled);
        Assert.IsTrue(configuration.Channel4Enabled);
        Assert.AreEqual(44_100, configuration.SampleRate);
        Assert.AreEqual(1, options.SaveSlot);
        Assert.AreEqual(SDL.ScaleMode.PixelArt, options.TextureScaleMode);
    }

    [TestMethod]
    public void UnavailableOutputDisablesCoreAudioWithoutLosingUserPreference()
    {
        var options = new LinuxFrontendOptions { AudioEnabled = true };

        var configuration = options.CreateEmulatorConfiguration(audioOutputAvailable: false);

        Assert.IsFalse(configuration.AudioEnabled);
        Assert.IsTrue(options.AudioEnabled);
    }

    [TestMethod]
    public void SmoothFilterUsesLinearTextureSampling()
    {
        var options = new LinuxFrontendOptions { VideoFilter = LinuxVideoFilter.Smooth };

        Assert.AreEqual(SDL.ScaleMode.Linear, options.TextureScaleMode);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(6)]
    public void RejectsInvalidSaveSlots(int slot)
    {
        var options = new LinuxFrontendOptions();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => options.SelectSaveSlot(slot));
    }
}
