namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class LinuxStorageTests
{
    [TestMethod]
    public void XdgRootsRejectRelativePathsAndHonorAbsoluteOverrides()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "xdg-tests"));
        var paths = LinuxDataPaths.FromEnvironment(key => key == "XDG_DATA_HOME" ? Path.Combine(root, "custom") : "relative", Path.Combine(root, "user"));
        Assert.AreEqual(Path.Combine(root, "custom", "aetherboy"), paths.Data);
        Assert.AreEqual(Path.Combine(root, "user", ".config", "aetherboy"), paths.Config);
        Assert.AreEqual(Path.Combine(root, "user", ".local", "state", "aetherboy"), paths.State);
    }

    [TestMethod]
    public void MovingRomKeepsSavesAndDifferentContentGetsSeparateStorage()
    {
        WithDirectory(root =>
        {
            var paths = LinuxDataPaths.Isolated(root);
            string rom = MakeRom(root, "game.gb", 1);
            string save;
            using (var storage = LinuxRomStorage.Open(paths, rom))
            {
                save = storage.SavePath;
                File.WriteAllBytes(save, [1, 2, 3]);
            }
            string moved = Path.Combine(root, "renamed.gb");
            File.Move(rom, moved);
            using (var storage = LinuxRomStorage.Open(paths, moved))
            {
                Assert.AreEqual(save, storage.SavePath);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(storage.SavePath));
            }
            using var other = LinuxRomStorage.Open(paths, MakeRom(root, "renamed.gbc", 2));
            Assert.AreNotEqual(save, other.SavePath);
        });
    }

    [TestMethod]
    public void MigrationCopiesWholeFamiliesAndNeverReplacesCentralSaves()
    {
        WithDirectory(root =>
        {
            var paths = LinuxDataPaths.Isolated(root);
            string rom = MakeRom(root, "game.gb", 1);
            string legacy = Path.ChangeExtension(rom, "sav");
            foreach (string suffix in new[] { "", ".rtc", ".bak1", ".guard", ".rtc.bak3.guard" })
                File.WriteAllText(legacy + suffix, "old");
            File.WriteAllText(Path.ChangeExtension(rom, "ss5"), "state");
            using (var storage = LinuxRomStorage.Open(paths, rom))
            {
                Assert.AreEqual("old", File.ReadAllText(storage.SavePath + ".rtc.bak3.guard"));
                Assert.AreEqual("state", File.ReadAllText(LinuxSaveStateStore.GetPath(storage.StateBasePath, 5)));
                File.WriteAllText(storage.SavePath, "new");
            }
            using var reopened = LinuxRomStorage.Open(paths, rom);
            Assert.AreEqual("new", File.ReadAllText(reopened.SavePath));
            Assert.AreEqual("old", File.ReadAllText(legacy));
            Assert.IsNotNull(reopened.MigrationNotice);
        });
    }

    [TestMethod]
    public void ConcurrentWriterIsRejectedAndCanOpenAfterOwnerCloses()
    {
        WithDirectory(root =>
        {
            var paths = LinuxDataPaths.Isolated(root);
            string rom = MakeRom(root, "game.gb", 1);
            using (var storage = LinuxRomStorage.Open(paths, rom))
                Assert.ThrowsExactly<IOException>(() => LinuxRomStorage.Open(paths, rom));
            using var reopened = LinuxRomStorage.Open(paths, rom);
            Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(reopened.SavePath)));
        });
    }

    [TestMethod]
    public void CorruptSettingsRecoverLastReadableBackupAndKeepItOnNextWrite()
    {
        WithDirectory(root =>
        {
            string path = Path.Combine(root, "settings.json");
            LinuxSettingsStore.Save(path, new LinuxFrontendOptions { AudioVolume = 24 });
            LinuxSettingsStore.Save(path, new LinuxFrontendOptions { AudioVolume = 48 });
            File.WriteAllText(path, "{");
            var restored = LinuxSettingsStore.Load(path, out string? error);
            Assert.AreEqual(24, restored.AudioVolume);
            Assert.IsNotNull(error);
            LinuxSettingsStore.Save(path, restored);
            Assert.AreEqual(24, LinuxSettingsStore.Load(path + ".bak", out _).AudioVolume);
        });
    }

    private static string MakeRom(string root, string name, byte marker)
    {
        string path = Path.Combine(root, name);
        byte[] bytes = new byte[32768];
        bytes[0x150] = marker;
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static void WithDirectory(Action<string> test)
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { test(root); }
        finally { Directory.Delete(root, true); }
    }
}
