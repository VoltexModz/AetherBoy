namespace AetherBoy.Desktop.Tests;

/// <summary>Adversarial storage checks using only temporary, generated cartridge data.</summary>
[TestClass]
public sealed class LinuxStoragePlaytestTests
{
    [TestMethod]
    public void DiagnosticRetentionKeepsNewestReportsAndTheActiveSession()
    {
        WithDirectory(root =>
        {
            var paths = LinuxDataPaths.Isolated(root);
            string sessions = Path.Combine(paths.State, "sessions");
            Directory.CreateDirectory(sessions);
            for (int i = 0; i < 24; i++)
            {
                string file = Path.Combine(sessions, $"old-{i}.jsonl");
                File.WriteAllText(file, "{}\n");
                File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-30).AddMinutes(i));
            }
            var diagnostics = new LinuxDiagnostics(paths, enabled: true);
            diagnostics.Dispose();
            Assert.IsNull(diagnostics.Error);
            Assert.AreEqual(20, Directory.GetFiles(sessions, "*.jsonl").Length);
            Assert.IsFalse(File.Exists(Path.Combine(sessions, "old-4.jsonl")));
            Assert.IsTrue(File.Exists(Path.Combine(sessions, "old-5.jsonl")));
            Assert.IsTrue(File.Exists(diagnostics.ReportPath));
        });
    }

    [TestMethod]
    public void ControllerMappingSwapsDuplicateButtonsAndPersistsPerDevice()
    {
        WithDirectory(root =>
        {
            var profile = new LinuxGamepadProfile { Deadzone = 8000 };
            profile.Bind(LinuxInputAction.A, SDL3.SDL.GamepadButton.East);
            Assert.AreEqual(SDL3.SDL.GamepadButton.East, profile.Buttons[LinuxInputAction.A]);
            Assert.AreEqual(SDL3.SDL.GamepadButton.South, profile.Buttons[LinuxInputAction.B]);
            Assert.AreEqual(profile.Buttons.Count, profile.Buttons.Values.Distinct().Count());
            Assert.ThrowsExactly<ArgumentException>(() => profile.Bind(LinuxInputAction.A, SDL3.SDL.GamepadButton.RightStick));
            string identity = new string('A', 32);
            var options = new LinuxFrontendOptions();
            options.Gamepads[identity] = profile;
            string settings = Path.Combine(root, "settings.json");
            LinuxSettingsStore.Save(settings, options);
            var restored = LinuxSettingsStore.Load(settings, out string? error);
            Assert.IsNull(error);
            Assert.AreEqual(8000, restored.Gamepads[identity].Deadzone);
            Assert.AreEqual(SDL3.SDL.GamepadButton.East, restored.Gamepads[identity].Buttons[LinuxInputAction.A]);
            Assert.AreEqual(SDL3.SDL.GamepadButton.South, restored.Gamepads[identity].Buttons[LinuxInputAction.B]);
        });
    }

    [TestMethod]
    public void CorruptLibraryEntriesDoNotHideValidCartridgesOrCrashThePage()
    {
        WithDirectory(root =>
        {
            var paths = LinuxDataPaths.Isolated(root);
            var library = new LinuxLibrary(paths);
            string rom = Path.Combine(root, "valid.gb");
            File.WriteAllBytes(rom, new byte[32768]);
            library.Remember(new string('A', 64), rom);
            string[] corruptEntries =
            [
                "{}", "null", "{", "{\"Identity\":null,\"Path\":null,\"Title\":null}",
                "{\"Identity\":\"" + new string('B', 64) + "\",\"Path\":null,\"Title\":\"broken\"}"
            ];
            for (int i = 0; i < corruptEntries.Length; i++)
                File.WriteAllText(Path.Combine(paths.Data, "library", $"broken-{i}.json"), corruptEntries[i]);
            var entries = library.Read();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(rom, entries[0].Path);
        });
    }

    [TestMethod]
    public void ReadOnlyRomDirectoryStillAllowsCentralBatteryAndStateWrites()
    {
        if (OperatingSystem.IsWindows()) { Assert.Inconclusive("Unix file permissions required."); return; }
        WithDirectory(root =>
        {
            if (OperatingSystem.IsWindows()) return;
            string romDirectory = Path.Combine(root, "read-only-roms");
            Directory.CreateDirectory(romDirectory);
            string rom = Path.Combine(romDirectory, "game.gb");
            File.WriteAllBytes(rom, new byte[32768]);
            File.SetUnixFileMode(romDirectory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            try
            {
                var paths = LinuxDataPaths.Isolated(Path.Combine(root, "app"));
                using var storage = LinuxRomStorage.Open(paths, rom);
                File.WriteAllBytes(storage.SavePath, [0x42]);
                LinuxSaveStateStore.WriteAtomic(storage.StateBasePath, 3, new byte[] { 1, 2, 3 });
                Assert.AreEqual((byte)0x42, File.ReadAllBytes(storage.SavePath)[0]);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, LinuxSaveStateStore.Read(storage.StateBasePath, 3));
                Assert.AreEqual(1, Directory.GetFiles(romDirectory).Length);
            }
            finally { File.SetUnixFileMode(romDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
        });
    }

    [TestMethod]
    public void InterruptedMigrationCanRetryWithoutCommittingPartialBatteryFamily()
    {
        if (OperatingSystem.IsWindows()) { Assert.Inconclusive("Unix file permissions required."); return; }
        WithDirectory(root =>
        {
            if (OperatingSystem.IsWindows()) return;
            string rom = Path.Combine(root, "game.gb");
            File.WriteAllBytes(rom, new byte[32768]);
            string legacy = Path.ChangeExtension(rom, "sav");
            File.WriteAllText(legacy, "battery");
            File.WriteAllText(legacy + ".bak1", "older");
            var paths = LinuxDataPaths.Isolated(Path.Combine(root, "app"));
            string destination = Path.Combine(paths.Data, "saves", LinuxRomStorage.Identify(rom));
            File.SetUnixFileMode(legacy + ".bak1", UnixFileMode.None);
            try
            {
                Assert.ThrowsExactly<UnauthorizedAccessException>(() => LinuxRomStorage.Open(paths, rom));
                Assert.IsFalse(Directory.Exists(destination), "Failed family copy must not become a completed migration.");
            }
            finally { File.SetUnixFileMode(legacy + ".bak1", UnixFileMode.UserRead | UnixFileMode.UserWrite); }

            using var reopened = LinuxRomStorage.Open(paths, rom);
            Assert.AreEqual("battery", File.ReadAllText(reopened.SavePath));
            Assert.AreEqual("older", File.ReadAllText(reopened.SavePath + ".bak1"));
            Assert.AreEqual("battery", File.ReadAllText(legacy));
            Assert.AreEqual("older", File.ReadAllText(legacy + ".bak1"));
            Assert.IsEmpty(Directory.EnumerateDirectories(Path.GetDirectoryName(destination)!, ".migration-*"));
        });
    }

    private static void WithDirectory(Action<string> test)
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-storage-playtest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { test(root); }
        finally { Directory.Delete(root, recursive: true); }
    }
}
