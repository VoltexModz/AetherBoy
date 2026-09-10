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
            Assert.HasCount(1, Directory.GetFiles(Path.GetDirectoryName(path)!));
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

    private static void WithSettingsPath(Action<string> action)
    {
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-prefs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(Path.Combine(directory, "settings.json")); }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
