using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class LinuxPreferencesTests
{
    [TestMethod]
    public void BindingAnOccupiedKeySwapsActionsWithoutLosingControls()
    {
        var keys = new LinuxKeyBindings();
        keys.Bind(LinuxInputAction.A, SDL.Scancode.X);
        Assert.AreEqual(SDL.Scancode.X, keys[LinuxInputAction.A]);
        Assert.AreEqual(SDL.Scancode.Z, keys[LinuxInputAction.B]);
        Assert.AreEqual(12, keys.ToDictionary().Values.Distinct().Count());
    }

    [TestMethod]
    [DataRow(SDL.Scancode.Escape)]
    [DataRow(SDL.Scancode.C)]
    [DataRow(SDL.Scancode.O)]
    [DataRow(SDL.Scancode.F5)]
    [DataRow(SDL.Scancode.F6)]
    [DataRow(SDL.Scancode.Alpha1)]
    [DataRow(SDL.Scancode.Unknown)]
    public void ReservedKeysCannotReplaceGameControls(SDL.Scancode key)
    {
        var keys = new LinuxKeyBindings();
        Assert.ThrowsExactly<ArgumentException>(() => keys.Bind(LinuxInputAction.A, key));
        Assert.AreEqual(SDL.Scancode.Z, keys[LinuxInputAction.A]);
    }

    [TestMethod]
    public void PersistenceRestoresSwapsLowVolumeAndMute()
    {
        WithSettingsPath(path =>
        {
            var options = new LinuxFrontendOptions { AudioEnabled = false };
            options.SetVolume(1);
            options.Keys.Bind(LinuxInputAction.A, SDL.Scancode.X);
            options.Keys.Bind(LinuxInputAction.Up, SDL.Scancode.W);
            options.Keys.Bind(LinuxInputAction.Pause, SDL.Scancode.P);
            LinuxSettingsStore.Save(path, options);
            var loaded = LinuxSettingsStore.Load(path, out var error);
            Assert.IsNull(error);
            Assert.AreEqual(1, loaded.AudioVolume);
            Assert.IsFalse(loaded.AudioEnabled);
            CollectionAssert.AreEquivalent(options.Keys.ToDictionary().ToArray(), loaded.Keys.ToDictionary().ToArray());

            loaded.SetVolume(0);
            LinuxSettingsStore.Save(path, loaded);
            Assert.AreEqual(0, LinuxSettingsStore.Load(path, out error).AudioVolume);
            Assert.IsNull(error);
            Assert.HasCount(2, Directory.GetFiles(Path.GetDirectoryName(path)!));
        });
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("null")]
    [DataRow("{\"Version\":2}")]
    [DataRow("{\"Keys\":{\"A\":\"X\"}}")]
    public void CorruptPreferencesFallBackWithoutOverwritingTheFile(string json)
    {
        WithSettingsPath(path =>
        {
            File.WriteAllText(path, json);
            var options = LinuxSettingsStore.Load(path, out var error);
            Assert.IsNotNull(error);
            Assert.AreEqual(75, options.AudioVolume);
            Assert.AreEqual(SDL.Scancode.Z, options.Keys[LinuxInputAction.A]);
            Assert.AreEqual(json, File.ReadAllText(path));
        });
    }

    [TestMethod]
    public void FormerF6BindingMigratesWithoutLosingOtherPreferences()
    {
        var keys = new LinuxKeyBindings().ToDictionary();
        keys[LinuxInputAction.A] = SDL.Scancode.F6;
        keys[LinuxInputAction.L] = SDL.Scancode.Z;
        var migrated = LinuxKeyBindings.FromDictionary(keys);
        Assert.AreEqual(SDL.Scancode.Z, migrated[LinuxInputAction.L]);
        Assert.AreNotEqual(SDL.Scancode.F6, migrated[LinuxInputAction.A]);
        Assert.AreEqual(12, migrated.ToDictionary().Values.Distinct().Count());
    }

    [TestMethod]
    public void DuplicatePersistedKeysAreRejected()
    {
        var saved = new LinuxKeyBindings().ToDictionary();
        saved[LinuxInputAction.A] = SDL.Scancode.X;
        Assert.ThrowsExactly<InvalidDataException>(() => LinuxKeyBindings.FromDictionary(saved));
    }

    [TestMethod]
    [DataRow(-5, 0)]
    [DataRow(1, 1)]
    [DataRow(101, 100)]
    public void LoadedVolumeIsClampedWithoutRoundingQuietValues(int volume, int expected)
    {
        WithSettingsPath(path =>
        {
            File.WriteAllText(path, "{\"AudioVolume\":" + volume + "}");
            var options = LinuxSettingsStore.Load(path, out var error);
            Assert.IsNull(error);
            Assert.AreEqual(expected, options.AudioVolume);
        });
    }

    [TestMethod]
    public void AllControlCenterPreferencesSurviveRestart()
    {
        WithSettingsPath(path =>
        {
            var options = new LinuxFrontendOptions
            {
                VideoFilter = LinuxVideoFilter.LcdGrid, Frameskip = 2, PaletteIndex = 4, SaveSlot = 5,
                Channel1Enabled = false, Channel2Enabled = true, Channel3Enabled = false, Channel4Enabled = false,
            };
            LinuxSettingsStore.Save(path, options);
            var loaded = LinuxSettingsStore.Load(path, out var error);
            Assert.IsNull(error);
            Assert.AreEqual(LinuxVideoFilter.LcdGrid, loaded.VideoFilter);
            Assert.AreEqual(2, loaded.Frameskip);
            Assert.AreEqual(4, loaded.PaletteIndex);
            Assert.AreEqual(5, loaded.SaveSlot);
            Assert.IsFalse(loaded.Channel1Enabled);
            Assert.IsTrue(loaded.Channel2Enabled);
            Assert.IsFalse(loaded.Channel3Enabled);
            Assert.IsFalse(loaded.Channel4Enabled);
        });
    }

    [TestMethod]
    public void LegacyPreferencesKeepDefaultsForNewFields()
    {
        WithSettingsPath(path =>
        {
            File.WriteAllText(path, "{\"Version\":1,\"AudioVolume\":5}");
            var loaded = LinuxSettingsStore.Load(path, out var error);
            Assert.IsNull(error);
            Assert.AreEqual(5, loaded.AudioVolume);
            Assert.AreEqual(1, loaded.SaveSlot);
            Assert.AreEqual(LinuxVideoFilter.Sharp, loaded.VideoFilter);
            Assert.IsTrue(loaded.Channel1Enabled && loaded.Channel2Enabled && loaded.Channel3Enabled && loaded.Channel4Enabled);
        });
    }

    [TestMethod]
    public void OutOfRangeDisplayPreferencesAreNormalized()
    {
        WithSettingsPath(path =>
        {
            File.WriteAllText(path, "{\"VideoFilter\":999,\"Frameskip\":99,\"PaletteIndex\":-9,\"SaveSlot\":8}");
            var loaded = LinuxSettingsStore.Load(path, out var error);
            Assert.IsNull(error);
            Assert.AreEqual(LinuxVideoFilter.Sharp, loaded.VideoFilter);
            Assert.AreEqual(2, loaded.Frameskip);
            Assert.AreEqual(0, loaded.PaletteIndex);
            Assert.AreEqual(5, loaded.SaveSlot);
        });
    }

    private static void WithSettingsPath(Action<string> action)
    {
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-prefs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(Path.Combine(directory, "settings.json")); }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
