using SDL3;
using AetherBoy.Desktop;

namespace AetherBoy.DesktopTests;

[TestClass]
public sealed class LinuxPlayerToolOptionsTests
{
    [TestMethod]
    public void ToolShortcutsAreReservedAndOldBindingsMigrateWithoutResettingTheOthers()
    {
        var keys = new LinuxKeyBindings();
        var saved = keys.ToDictionary();
        saved[LinuxInputAction.A] = SDL.Scancode.F9;
        saved[LinuxInputAction.B] = SDL.Scancode.F12;
        saved[LinuxInputAction.L] = SDL.Scancode.F10;
        var migrated = LinuxKeyBindings.FromDictionary(saved);
        Assert.IsFalse(LinuxKeyBindings.CanBind(SDL.Scancode.F9));
        Assert.IsFalse(LinuxKeyBindings.CanBind(SDL.Scancode.F12));
        Assert.IsFalse(LinuxKeyBindings.CanBind(SDL.Scancode.F10));
        Assert.ThrowsExactly<ArgumentException>(() => keys.Bind(LinuxInputAction.A, SDL.Scancode.F10));
        Assert.AreEqual(SDL.Scancode.Z, migrated[LinuxInputAction.A]);
        Assert.AreEqual(SDL.Scancode.X, migrated[LinuxInputAction.B]);
        Assert.AreEqual(SDL.Scancode.Q, migrated[LinuxInputAction.L]);
        Assert.AreEqual(keys[LinuxInputAction.Turbo], migrated[LinuxInputAction.Turbo]);
        Assert.AreEqual(12, migrated.ToDictionary().Values.Distinct().Count());
    }

    [TestMethod]
    public void PerformancePreferenceRoundTripsInVersionOneSettings()
    {
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-tools-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "settings.json");
        try
        {
            LinuxSettingsStore.Save(path, new LinuxFrontendOptions { PerformanceOverlay = true });
            var loaded = LinuxSettingsStore.Load(path, out string? error);
            Assert.IsNull(error);
            Assert.IsTrue(loaded.PerformanceOverlay);
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public void SharedPlayerToolsAndTextSizeRemainGlobalAcrossGameProfileChanges()
    {
        var options = new LinuxFrontendOptions { PerformanceOverlay = true, TextSize = 18, AudioVolume = 75 };
        var global = LinuxGameProfile.Capture(options);
        var game = new LinuxGameProfile { AudioVolume = 23, VideoFilter = LinuxVideoFilter.Smooth };
        game.ApplyTo(options);
        Assert.AreEqual(23, options.AudioVolume);
        Assert.IsTrue(options.PerformanceOverlay);
        Assert.AreEqual(18, options.TextSize);
        global.ApplyTo(options);
        Assert.AreEqual(75, options.AudioVolume);
        Assert.IsTrue(options.PerformanceOverlay);
        Assert.AreEqual(18, options.TextSize);
    }
}
