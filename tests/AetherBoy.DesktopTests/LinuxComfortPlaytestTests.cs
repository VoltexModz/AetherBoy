using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using AetherBoy.Runtime;
using SDL3;

namespace AetherBoy.Desktop.Tests;

/// <summary>Independent failure-oriented acceptance for the Linux comfort update. No long runs.</summary>
[TestClass]
[DoNotParallelize]
public sealed class LinuxComfortPlaytestTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private string root = null!;
    private LinuxDataPaths paths = null!;
    [TestInitialize] public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "aetherboy-comfort-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        paths = LinuxDataPaths.Isolated(Path.Combine(root, "storage"));
    }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    [TestMethod]
    public void FavoritesCustomTitleAndConcurrentPlaytimeSurviveRelinkAndRestart()
    {
        string rom = Rom("one.gb"), hash = LinuxRomStorage.Identify(rom);
        var library = new LinuxLibrary(paths);
        library.Remember(hash, rom);
        library.Update(hash, entry => entry with { Favorite = true, Title = "My cartridge", HasCustomTitle = true });
        Parallel.For(0, 30, _ => new LinuxLibrary(paths).Update(hash, entry => entry with { PlaySeconds = entry.PlaySeconds + 1 }));
        string moved = Path.Combine(root, "relocated.gb"); File.Move(rom, moved);
        library.Remember(hash, moved);
        var loaded = new LinuxLibrary(paths).Read().Single();
        Assert.AreEqual("My cartridge", loaded.Title);
        Assert.IsTrue(loaded.Favorite);
        Assert.AreEqual(30d, loaded.PlaySeconds);
        Assert.AreEqual(moved, loaded.Path);
        Assert.AreEqual("GB", loaded.System);
        Assert.IsFalse(Directory.EnumerateFiles(paths.Data, "*.tmp", SearchOption.AllDirectories).Any());
    }

    [TestMethod]
    public void InvalidMetadataMutationPreservesPreviousEntry()
    {
        string rom = Rom("one.gb"), hash = LinuxRomStorage.Identify(rom);
        var library = new LinuxLibrary(paths); library.Remember(hash, rom);
        var original = library.Read().Single();
        Assert.ThrowsExactly<InvalidDataException>(() => library.Update(hash, entry => entry with { PlaySeconds = -1 }));
        Assert.ThrowsExactly<InvalidDataException>(() => library.Update(hash, entry => entry with { Title = "bad\nname" }));
        Assert.ThrowsExactly<InvalidDataException>(() => library.Update(hash, entry => entry with { Identity = new string('b', 64) }));
        Assert.AreEqual(original, library.Read().Single());
    }

    [TestMethod]
    public void ProfileOverridesRemainIsolatedAndInheritedValuesFollowGlobalChanges()
    {
        string hashA = new('a', 64), hashB = new('b', 64);
        var global = new LinuxFrontendOptions { AudioVolume = 75, PaletteIndex = 0, Frameskip = 0 };
        var current = new LinuxFrontendOptions { AudioVolume = 19, PaletteIndex = 0, Frameskip = 0 };
        current.Keys.Bind(LinuxInputAction.A, SDL.Scancode.V);
        var store = new LinuxProfileStore(paths);
        store.Write(hashA, LinuxGameProfile.FromDifference(current, global));
        var loaded = store.Read(hashA, out string? error); Assert.IsNull(error); Assert.IsNotNull(loaded);
        var effective = new LinuxFrontendOptions { AudioVolume = 88, PaletteIndex = 3, Frameskip = 2 };
        loaded.ApplyTo(effective);
        Assert.AreEqual(19, effective.AudioVolume);
        Assert.AreEqual(3, effective.PaletteIndex, "A field not overridden by the game must keep the current global value.");
        Assert.AreEqual(2, effective.Frameskip);
        Assert.AreEqual(SDL.Scancode.V, effective.Keys[LinuxInputAction.A]);
        Assert.AreEqual(75, global.AudioVolume); Assert.AreEqual(SDL.Scancode.Z, global.Keys[LinuxInputAction.A]);
        Assert.IsNull(store.Read(hashB, out error)); Assert.IsNull(error);
    }

    [TestMethod]
    [DataRow("{\"AudioVolume\":-1}")]
    [DataRow("{\"Keys\":{\"A\":27,\"B\":27}}")]
    [DataRow("null")]
    [DataRow("{")]
    public void CorruptProfileFallsBackWithoutOverwritingEvidence(string json)
    {
        string hash = new('a', 64), directory = Path.Combine(paths.Config, "profiles");
        Directory.CreateDirectory(directory); string file = Path.Combine(directory, hash + ".json");
        File.WriteAllText(file, json);
        var loaded = new LinuxProfileStore(paths).Read(hash, out string? error);
        Assert.IsNull(loaded); Assert.IsNotNull(error);
        Assert.AreEqual(json, File.ReadAllText(file));
    }

    [TestMethod]
    public void GalleryRejectsStaleAndMalformedPreviewButKeepsLoadableState()
    {
        string stateBase = Path.Combine(root, "game.rom");
        byte[] first = [1, 2, 3], second = [4, 5, 6];
        int[] pixels = Enumerable.Repeat(unchecked((int)0xFF00AACC), 160 * 144).ToArray();
        LinuxStateGallery.Write(stateBase, 1, first, 160, 144, pixels);
        var card = LinuxStateGallery.Inspect(stateBase, null, 0).States.Single(card => card.Slot == 1);
        Assert.IsTrue(card.Exists); Assert.IsNull(card.Error); CollectionAssert.AreEqual(pixels, card.Pixels);
        // External state replacement must never inherit a misleading old screenshot.
        LinuxSaveStateStore.WriteAtomic(stateBase, 1, second);
        card = LinuxStateGallery.Inspect(stateBase, null, 0).States.Single(card => card.Slot == 1);
        Assert.IsTrue(card.Exists); Assert.IsNull(card.Error); Assert.IsNull(card.Pixels);
        CollectionAssert.AreEqual(second, LinuxSaveStateStore.Read(stateBase, 1));
        LinuxStateGallery.Write(stateBase, 1, second, 160, 144, pixels);
        string preview = LinuxStateGallery.PathFor(stateBase, 1) + ".preview";
        byte[] malformed = File.ReadAllBytes(preview); BitConverter.GetBytes(int.MaxValue).CopyTo(malformed, 36); File.WriteAllBytes(preview, malformed);
        card = LinuxStateGallery.Inspect(stateBase, null, 0).States.Single(card => card.Slot == 1);
        Assert.IsTrue(card.Exists); Assert.IsNull(card.Pixels);
        // Resume is a distinct sixth slot and never overwrites manual slot one.
        LinuxStateGallery.Write(stateBase, 0, first, 160, 144, pixels);
        Assert.HasCount(6, LinuxStateGallery.Inspect(stateBase, null, 0).States);
        CollectionAssert.AreEqual(first, LinuxSaveStateStore.ReadPath(LinuxStateGallery.PathFor(stateBase, 0)));
        CollectionAssert.AreEqual(second, LinuxSaveStateStore.Read(stateBase, 1));
    }

    [TestMethod]
    public void NativeStateLoadUndoCorruptionResumeAndCartridgeSwitchPreserveState()
    {
        WithNativeHost((host, settings) =>
        {
            string a = Rom("timeline.gb");
            Load(host, a); var session = Field<EmulationSession>(host, "session");
            AdvanceAndPause(session, 4);
            byte[] first = Capture(session);
            Call(host, "QuickSave"); DrainState(host);
            string stateBase = Field<LinuxRomStorage>(host, "storage").StateBasePath;
            Assert.IsTrue(File.Exists(LinuxStateGallery.PathFor(stateBase, 1)));
            if (Environment.GetEnvironmentVariable("AETHERBOY_COMFORT_CAPTURE_DIR") is not null)
            {
                Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "PollDiskRefresh"); return Field<LinuxDiskSnapshot?>(host, "diskSnapshot")?.States.Any(card => card.Slot == 1 && card.Pixels is not null) == true; }, TimeSpan.FromSeconds(10)));
                Field<LinuxFrontendOptions>(host, "options").TextSize = 18; SDL.SetWindowSize(Field<IntPtr>(host, "window"), 900, 650);
                CapturePage(host, "Saves", "gallery", gallery: true);
            }
            AdvanceAndPause(session, 4); byte[] beforeLoad = Capture(session);
            CollectionAssert.AreNotEqual(first, beforeLoad);
            Call(host, "QuickLoad"); DrainState(host);
            CollectionAssert.AreEqual(first, Capture(session));
            Assert.IsNotNull(Field<byte[]?>(host, "undoState"));
            LinuxSaveStateStore.WriteAtomic(stateBase, 1, [0x42, 0x41, 0x44]);
            Call(host, "QuickLoad"); DrainState(host);
            Assert.Contains("failed", Field<string>(host, "statusMessage"));
            CollectionAssert.AreEqual(first, Capture(session), "Corrupt load must not alter the running machine.");
            Call(host, "UndoLoad"); DrainState(host);
            CollectionAssert.AreEqual(beforeLoad, Capture(session), "Failed load must retain the preceding valid undo.");
            Assert.IsNull(Field<byte[]?>(host, "undoState"));
            Call(host, "CloseSession");
            CollectionAssert.AreEqual(beforeLoad, LinuxSaveStateStore.ReadPath(LinuxStateGallery.PathFor(stateBase, 0)));
            Load(host, a); session = Field<EmulationSession>(host, "session"); session.SetPausedAsync(true).GetAwaiter().GetResult();
            Call(host, "LoadResume"); DrainState(host);
            CollectionAssert.AreEqual(beforeLoad, Capture(session));
            Assert.IsNotNull(Field<byte[]?>(host, "undoState"));
            string b = Rom("different.gb"); byte[] different = File.ReadAllBytes(b); different[0x200] ^= 1; File.WriteAllBytes(b, different);
            Load(host, b);
            Assert.IsNull(Field<byte[]?>(host, "undoState"), "A second cartridge must never inherit the first cartridge's undo.");
            Assert.IsTrue(SpinWait.SpinUntil(() =>
            { Call(host, "PollDiskRefresh"); return Field<LinuxDiskSnapshot?>(host, "diskSnapshot")?.BasePath == Field<LinuxRomStorage>(host, "storage").StateBasePath; }, TimeSpan.FromSeconds(10)));
            Assert.IsFalse(Field<LinuxDiskSnapshot>(host, "diskSnapshot").States.Any(card => card.Exists), "The new cartridge must not show the old cartridge's slots.");
        });
    }

    [TestMethod]
    public void NativeGameProfileChangesDoNotLeakAcrossGamesOrIntoGlobalSettings()
    {
        WithNativeHost((host, settings) =>
        {
            string a = Rom("profile-a.gb"), b = Rom("profile-b.gb");
            byte[] bytes = File.ReadAllBytes(b); bytes[0x200] ^= 1; File.WriteAllBytes(b, bytes);
            Load(host, a);
            Call(host, "ToggleGameProfile");
            var options = Field<LinuxFrontendOptions>(host, "options");
            options.AudioVolume = 17; options.VideoFilter = LinuxVideoFilter.Smooth; options.VideoScaling = LinuxVideoScaling.Fit;
            options.Keys.Bind(LinuxInputAction.A, SDL.Scancode.V);
            Call(host, "MarkSettingsChanged"); Call(host, "FlushSettingsIfDue", true);
            Assert.AreEqual(75, LinuxSettingsStore.Load(settings, out _).AudioVolume);
            Load(host, b);
            Assert.AreEqual(75, options.AudioVolume); Assert.AreEqual(LinuxVideoFilter.Sharp, options.VideoFilter);
            Assert.AreEqual(LinuxVideoScaling.Automatic, options.VideoScaling);
            Assert.AreEqual(SDL.Scancode.Z, options.Keys[LinuxInputAction.A]);
            Load(host, a);
            Assert.AreEqual(17, options.AudioVolume); Assert.AreEqual(LinuxVideoFilter.Smooth, options.VideoFilter);
            Assert.AreEqual(LinuxVideoScaling.Fit, options.VideoScaling);
            Assert.AreEqual(SDL.Scancode.V, options.Keys[LinuxInputAction.A]);
            if (Environment.GetEnvironmentVariable("AETHERBOY_COMFORT_CAPTURE_DIR") is not null)
            {
                var library = Field<LinuxLibrary>(host, "library");
                library.Update(LinuxRomStorage.Identify(a), entry => entry with { Favorite = true, HasCustomTitle = true, Title = "A long custom cartridge title to verify library truncation and action spacing" });
                Call(host, "RefreshLibrary");
                Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "PollLibraryRefresh"); return Field<Task?>(host, "libraryRefresh") is null; }, TimeSpan.FromSeconds(10)));
                options.TextSize = 18; SDL.SetWindowSize(Field<IntPtr>(host, "window"), 900, 650);
                CapturePage(host, "Library", "library");
                CapturePage(host, "Input", "input-large-text");
                CapturePage(host, "System", "system-profile-large-text");
                CapturePage(host, "Display", "display-large-text");
                if (Environment.GetEnvironmentVariable("AETHERBOY_MAIN_CAPTURE_ONLY") == "1")
                {
                    Call(host, "DrawShell");
                    IntPtr surface = SDL.RenderReadPixels(Field<IntPtr>(host, "renderer"), null);
                    Assert.AreNotEqual(IntPtr.Zero, surface);
                    try { Assert.IsTrue(SDL.SavePNG(surface, Path.Combine(Environment.GetEnvironmentVariable("AETHERBOY_COMFORT_CAPTURE_DIR")!, "main-large-text.png"))); }
                    finally { SDL.DestroySurface(surface); }
                }
            }
            Call(host, "ToggleGameProfile");
            Assert.AreEqual(75, options.AudioVolume); Assert.AreEqual(SDL.Scancode.Z, options.Keys[LinuxInputAction.A]);
        });
    }

    [TestMethod]
    public void NativeStaleDiskReadCannotPublishPreviousCartridgeGallery()
    {
        WithNativeHost((host, settings) =>
        {
            string a = Rom("cache-a.gb"), b = Rom("cache-b.gb");
            byte[] bytes = File.ReadAllBytes(b); bytes[0x200] ^= 1; File.WriteAllBytes(b, bytes);
            Load(host, a);
            string oldBase = Field<LinuxRomStorage>(host, "storage").StateBasePath;
            var delayed = new TaskCompletionSource<LinuxDiskSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            // Deterministically hold the old I/O completion while the user changes cartridge.
            host.GetType().GetField("diskRefresh", Private)!.SetValue(host, delayed.Task);
            Load(host, b);
            delayed.SetResult(new LinuxDiskSnapshot(oldBase, [new LinuxStateCard(1, true, DateTime.UtcNow, null)], [], null));
            string newBase = Field<LinuxRomStorage>(host, "storage").StateBasePath;
            Assert.IsTrue(SpinWait.SpinUntil(() =>
            { Call(host, "PollDiskRefresh"); return Field<LinuxDiskSnapshot?>(host, "diskSnapshot")?.BasePath == newBase; }, TimeSpan.FromSeconds(10)));
            Call(host, "PollDiskRefresh");
            var current = Field<LinuxDiskSnapshot>(host, "diskSnapshot");
            Assert.AreEqual(newBase, current.BasePath); Assert.IsFalse(current.States.Any(card => card.Exists));
        });
    }

    [TestMethod]
    public void NativePlaytimeExcludesPauseAndSettingsThenPersistsOnClose()
    {
        WithNativeHost((host, settings) =>
        {
            string rom = Rom("playtime.gb"); Load(host, rom);
            var session = Field<EmulationSession>(host, "session");
            Call(host, "UpdateComfort"); Thread.Sleep(120); Call(host, "UpdateComfort");
            double played = Field<double>(host, "playedSeconds"); Assert.IsTrue(played >= 0.10);
            session.SetPausedAsync(true).GetAwaiter().GetResult();
            Thread.Sleep(120); Call(host, "UpdateComfort");
            Assert.AreEqual(played, Field<double>(host, "playedSeconds"));
            Call(host, "ToggleControlCenter"); Thread.Sleep(120); Call(host, "UpdateComfort");
            Assert.AreEqual(played, Field<double>(host, "playedSeconds"));
            Call(host, "CloseSession");
            var entry = Field<LinuxLibrary>(host, "library").Read().Single();
            Assert.AreEqual(played, entry.PlaySeconds);
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NativePlaytimePersistsEvenWhenResumeFailsOrSessionStopped(bool stopped)
    {
        WithNativeHost((host, settings) =>
        {
            string rom = Rom("playtime-resume-failure.gb"); Load(host, rom);
            var session = Field<EmulationSession>(host, "session");
            Call(host, "UpdateComfort"); Thread.Sleep(120); Call(host, "UpdateComfort");
            double played = Field<double>(host, "playedSeconds"); Assert.IsTrue(played >= 0.10);
            if (stopped) session.DisposeAsync().AsTask().GetAwaiter().GetResult();
            else
            {
                string resume = LinuxStateGallery.PathFor(Field<LinuxRomStorage>(host, "storage").StateBasePath, 0);
                Directory.CreateDirectory(resume); // Directory at the target blocks the state write.
            }
            Call(host, "CloseSession");
            Assert.AreEqual(played, Field<LinuxLibrary>(host, "library").Read().Single().PlaySeconds);
        });
    }

    [TestMethod]
    public void NativeImmediateProfileToggleKeepsPendingGlobalVolumeInGlobalScope()
    {
        WithNativeHost((host, settings) =>
        {
            Load(host, Rom("scope.gb"));
            Call(host, "SetVolume", 23);
            // No debounce wait or explicit flush: this is the real rapid-click race.
            Call(host, "ToggleGameProfile");
            Assert.AreEqual(23, LinuxSettingsStore.Load(settings, out _).AudioVolume);
            Assert.IsTrue(Field<bool>(host, "usingGameProfile"));
            Call(host, "SetVolume", 17);
            Call(host, "ToggleGameProfile");
            Assert.IsFalse(Field<bool>(host, "usingGameProfile"));
            Assert.AreEqual(23, Field<LinuxFrontendOptions>(host, "options").AudioVolume);
            Assert.AreEqual(23, LinuxSettingsStore.Load(settings, out _).AudioVolume);
        });
    }

    [TestMethod]
    public void NativeFailedProfileSaveBlocksSwitchAndRetainsEditsForRetry()
    {
        WithNativeHost((host, settings) =>
        {
            string a = Rom("failed-profile-a.gb"), b = Rom("failed-profile-b.gb");
            byte[] bytes = File.ReadAllBytes(b); bytes[0x200] ^= 1; File.WriteAllBytes(b, bytes);
            Load(host, a); Call(host, "ToggleGameProfile");
            string file = Path.Combine(paths.Config, "profiles", LinuxRomStorage.Identify(a) + ".json");
            File.Delete(file); Directory.CreateDirectory(file);
            Call(host, "SetVolume", 17); Call(host, "TryLoadRom", b);
            Assert.AreEqual(a, Field<string>(host, "romPath"));
            Assert.IsNull(Field<object?>(host, "pendingSession"));
            Assert.IsTrue(Field<bool>(host, "settingsDirty"));
            Assert.AreEqual(17, Field<LinuxFrontendOptions>(host, "options").AudioVolume);
            Assert.AreEqual(75, LinuxSettingsStore.Load(settings, out _).AudioVolume);
            Directory.Delete(file);
            Call(host, "FlushSettingsIfDue", true);
            Assert.IsFalse(Field<bool>(host, "settingsDirty"));
            Load(host, b); Load(host, a);
            Assert.AreEqual(17, Field<LinuxFrontendOptions>(host, "options").AudioVolume);
        });
    }

    [TestMethod]
    public void NativeF6JumpsBetweenSidebarAndPageIncludingControllerSubview()
    {
        WithNativeHost((host, settings) =>
        {
            Call(host, "ToggleControlCenter"); SelectPage(host, "Input");
            Key(host, SDL.Scancode.F6);
            Assert.AreEqual(300f, Focus(host).X);
            Key(host, SDL.Scancode.F6);
            Assert.AreEqual(22f, Focus(host).X); Assert.AreEqual(374f, Focus(host).Y);
            Key(host, SDL.Scancode.F6); Key(host, SDL.Scancode.Tab); Key(host, SDL.Scancode.Return);
            Assert.IsTrue(Field<bool>(host, "showController"));
            Key(host, SDL.Scancode.F6);
            Assert.IsTrue(Focus(host).X >= 278 && Focus(host).Y >= 180);
            Key(host, SDL.Scancode.F6);
            Assert.AreEqual(22f, Focus(host).X);
            Key(host, SDL.Scancode.Return);
            Assert.IsFalse(Field<bool>(host, "showController"), "Sidebar selection must leave the controller subview.");
            Key(host, SDL.Scancode.F6); Assert.AreEqual(300f, Focus(host).X);
        });
    }

    [TestMethod]
    public void NativeRefreshPreservesSameActionAndNeverRetargetsReorderedLibraryRows()
    {
        WithNativeHost((host, settings) =>
        {
            string a = Rom("focus-a.gb"), b = Rom("focus-b.gb");
            byte[] bytes = File.ReadAllBytes(b); bytes[0x200] ^= 1; File.WriteAllBytes(b, bytes);
            var library = Field<LinuxLibrary>(host, "library");
            string hashA = LinuxRomStorage.Identify(a), hashB = LinuxRomStorage.Identify(b);
            library.Remember(hashA, a); library.Remember(hashB, b);
            library.Update(hashA, entry => entry with { Title = "Same name", HasCustomTitle = true, LastPlayed = DateTime.UtcNow.AddMinutes(2) });
            library.Update(hashB, entry => entry with { Title = "Same name", HasCustomTitle = true, LastPlayed = DateTime.UtcNow });
            Call(host, "ToggleControlCenter"); SelectPage(host, "Library"); RefreshCatalog(host);
            FocusByKeyboard(host, "rename:" + hashA);
            // Disabling an earlier open button shifts every following numerical focus index.
            File.Delete(a); RefreshCatalog(host);
            Key(host, SDL.Scancode.Return);
            Assert.AreEqual(hashA, Field<string?>(host, "editingTitleIdentity"), "A disabled preceding button must not shift focus to another action.");
            Key(host, SDL.Scancode.Escape);
            RefreshCatalog(host); FocusByKeyboard(host, "rename:" + hashA);
            library.Update(hashB, entry => entry with { LastPlayed = DateTime.UtcNow.AddMinutes(4) });
            RefreshCatalog(host);
            Key(host, SDL.Scancode.Return);
            Assert.IsNull(Field<string?>(host, "editingTitleIdentity"), "A different cartridge at identical geometry and title must not inherit keyboard focus.");
            Assert.AreEqual(-1, Field<int>(host, "focusedControl"));
        });
    }

    [TestMethod]
    public void NativeHomeContinueRestoresLastSessionAndRejectsReplacedCartridge()
    {
        WithNativeHost((host, settings) =>
        {
            string rom = Rom("continue.gb"); Load(host, rom);
            var session = Field<EmulationSession>(host, "session"); AdvanceAndPause(session, 4);
            byte[] expected = Capture(session); Call(host, "CloseSession");
            RefreshCatalog(host); Assert.IsNotNull(Field<LinuxLibraryEntry?>(host, "lastResumeEntry"));
            Call(host, "ToggleControlCenter"); // Keep restored state paused for an exact comparison.
            Call(host, "ContinueLastSession");
            Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "CompletePendingLoad"); return Field<object?>(host, "pendingSession") is null && Field<Task?>(host, "romPreparation") is null; }, TimeSpan.FromSeconds(10)));
            DrainState(host);
            session = Field<EmulationSession>(host, "session");
            Assert.IsTrue(session.LatestSnapshot.IsPaused);
            CollectionAssert.AreEqual(expected, Capture(session));
            Call(host, "CloseSession"); RefreshCatalog(host);
            byte[] replaced = File.ReadAllBytes(rom); replaced[0x200] ^= 1; File.WriteAllBytes(rom, replaced);
            Call(host, "ContinueLastSession");
            Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "CompletePendingLoad"); return Field<Task?>(host, "romPreparation") is null; }, TimeSpan.FromSeconds(10)));
            Assert.IsNull(Field<EmulationSession?>(host, "session"));
            Assert.IsNull(Field<object?>(host, "pendingSession"));
            Assert.AreEqual("Cartridge could not be opened. Check the file and the local report, then try again.",
                Field<string>(host, "statusMessage"));
        });
    }

    [TestMethod]
    public void NativeFailedHomeResumeNeverOverwritesTheRecoveryPointOnClose()
    {
        WithNativeHost((host, settings) =>
        {
            string rom = Rom("failed-resume.gb"); Load(host, rom);
            string resume = LinuxStateGallery.PathFor(Field<LinuxRomStorage>(host, "storage").StateBasePath, 0);
            Call(host, "CloseSession");
            byte[] recoveryEvidence = [0x46, 0x55, 0x54, 0x55, 0x52, 0x45]; File.WriteAllBytes(resume, recoveryEvidence);
            RefreshCatalog(host); Call(host, "ContinueLastSession");
            Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "CompletePendingLoad"); return Field<object?>(host, "pendingSession") is null && Field<Task?>(host, "romPreparation") is null; }, TimeSpan.FromSeconds(10)));
            DrainState(host); Assert.Contains("failed", Field<string>(host, "statusMessage"));
            Call(host, "CloseSession");
            CollectionAssert.AreEqual(recoveryEvidence, File.ReadAllBytes(resume), "A failed resume must remain available for recovery after closing the booted game.");
        });
    }

    [TestMethod]
    public void NativeRenameCtrlAReplacesTextAndPersistsTheResult()
    {
        WithNativeHost((host, settings) =>
        {
            string rom = Rom("rename.gb"), hash = LinuxRomStorage.Identify(rom);
            var library = Field<LinuxLibrary>(host, "library"); library.Remember(hash, rom);
            library.Update(hash, entry => entry with { Title = "A long custom cartridge title to replace using the keyboard", HasCustomTitle = true });
            Call(host, "ToggleControlCenter"); SelectPage(host, "Library"); RefreshCatalog(host);
            FocusByKeyboard(host, "rename:" + hash); Key(host, SDL.Scancode.Return);
            Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.A, Mod = SDL.Keymod.Ctrl }, true);
            Assert.IsTrue(Field<bool>(host, "titleSelectedAll"));
            string? folder = Environment.GetEnvironmentVariable("AETHERBOY_COMFORT_CAPTURE_DIR");
            if (folder is not null && Environment.GetEnvironmentVariable("AETHERBOY_MAIN_CAPTURE_ONLY") != "1")
            {
                Field<LinuxFrontendOptions>(host, "options").TextSize = 18;
                SDL.SetWindowSize(Field<IntPtr>(host, "window"), 900, 650); Call(host, "DrawShell");
                IntPtr surface = SDL.RenderReadPixels(Field<IntPtr>(host, "renderer"), null);
                try { Assert.IsTrue(SDL.SavePNG(surface, Path.Combine(folder, "rename-large-text.png"))); }
                finally { SDL.DestroySurface(surface); }
            }
            TextInput(host, "Replacement"); Assert.AreEqual("Replacement", Field<string>(host, "titleInput"));
            Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.A, Mod = SDL.Keymod.Ctrl }, true);
            Key(host, SDL.Scancode.Backspace); Assert.AreEqual("", Field<string>(host, "titleInput"));
            TextInput(host, "Saved title"); Key(host, SDL.Scancode.Return);
            Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "PollLibraryRefresh"); return Field<Task?>(host, "libraryMutation") is null; }, TimeSpan.FromSeconds(10)));
            Assert.AreEqual("Saved title", library.Read().Single().Title);
        });
    }

    private static void TextInput(object host, string text)
    {
        IntPtr value = Marshal.StringToCoTaskMemUTF8(text), memory = Marshal.AllocHGlobal(Marshal.SizeOf<SDL.Event>());
        try
        {
            Marshal.StructureToPtr(new SDL.Event(), memory, false);
            Marshal.WriteInt32(memory, (int)SDL.EventType.TextInput);
            Marshal.WriteIntPtr(memory, (int)Marshal.OffsetOf<SDL.TextInputEvent>("Text"), value);
            Call(host, "HandleEvent", Marshal.PtrToStructure<SDL.Event>(memory));
        }
        finally { Marshal.FreeHGlobal(memory); Marshal.FreeCoTaskMem(value); }
    }

    private static void SelectPage(object host, string page)
    {
        Type pageType = host.GetType().GetNestedType("ControlCenterPage", BindingFlags.NonPublic)!;
        Call(host, "SelectControlCenterPage", Enum.Parse(pageType, page)); Call(host, "DrawShell");
    }
    private static void RefreshCatalog(object host)
    {
        Call(host, "RefreshLibrary");
        Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "PollLibraryRefresh"); return Field<Task?>(host, "libraryRefresh") is null; }, TimeSpan.FromSeconds(10)));
        Call(host, "DrawShell");
    }
    private static void Key(object host, SDL.Scancode key) => Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = key }, true);
    private static SDL.FRect Focus(object host)
    {
        int index = Field<int>(host, "focusedControl"); Assert.IsTrue(index >= 0, "No keyboard focus.");
        return Field<List<SDL.FRect>>(host, "focusTargets")[index];
    }
    private static void FocusByKeyboard(object host, string action)
    {
        for (int i = 0; i < 80; i++)
        {
            Key(host, SDL.Scancode.Tab);
            int index = Field<int>(host, "focusedControl");
            Assert.IsTrue(index >= 0, "No keyboard focus.");
            object identity = Field<System.Collections.IList>(host, "focusIdentities")[index]!;
            if ((string?)identity.GetType().GetProperty("Action")!.GetValue(identity) == action) return;
        }
        Assert.Fail("Requested action could not be reached by Tab: " + action);
    }

    private static void CapturePage(object host, string page, string name, bool gallery = false)
    {
        string? folder = Environment.GetEnvironmentVariable("AETHERBOY_COMFORT_CAPTURE_DIR");
        if (folder is null || Environment.GetEnvironmentVariable("AETHERBOY_MAIN_CAPTURE_ONLY") == "1"
            || (Environment.GetEnvironmentVariable("AETHERBOY_COMFORT_CAPTURE_PAGE") is { } requested && requested != page)) return;
        Call(host, "ToggleControlCenter");
        Type pageType = host.GetType().GetNestedType("ControlCenterPage", BindingFlags.NonPublic)!;
        Call(host, "SelectControlCenterPage", Enum.Parse(pageType, page));
        if (gallery) host.GetType().GetField("showGallery", Private)!.SetValue(host, true);
        Call(host, "DrawShell");
        Directory.CreateDirectory(folder);
        IntPtr surface = SDL.RenderReadPixels(Field<IntPtr>(host, "renderer"), null);
        Assert.AreNotEqual(IntPtr.Zero, surface);
        try { Assert.IsTrue(SDL.SavePNG(surface, Path.Combine(folder, name + ".png"))); }
        finally { SDL.DestroySurface(surface); }
        Call(host, "CloseControlCenter");
    }

    private void WithNativeHost(Action<WaylandEmulatorHost, string> action)
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Set AETHERBOY_UI_TESTS=1 in a native Wayland session."); return; }
        string settings = Path.Combine(root, "settings.json");
        LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false });
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
        Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events | SDL.InitFlags.Gamepad), SDL.GetError());
        try { using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true); action(host, settings); }
        finally { SDL.Quit(); }
    }
    private static void Load(object host, string path)
    {
        Call(host, "TryLoadRom", path);
        Assert.IsTrue(SpinWait.SpinUntil(() =>
        { Call(host, "CompletePendingLoad"); return Field<object?>(host, "pendingSession") is null && Field<Task?>(host, "romPreparation") is null; }, TimeSpan.FromSeconds(10)));
        Assert.IsNull(Field<string?>(host, "loadError")); Assert.IsNotNull(Field<EmulationSession?>(host, "session"));
        Assert.AreEqual(path, Field<string>(host, "romPath"));
    }
    private static void AdvanceAndPause(EmulationSession session, int frames)
    {
        long start = session.LatestSnapshot.EmulatedFrameCount;
        session.SetPausedAsync(false).GetAwaiter().GetResult();
        Assert.IsTrue(SpinWait.SpinUntil(() => session.LatestSnapshot.EmulatedFrameCount >= start + frames, TimeSpan.FromSeconds(10)));
        session.SetPausedAsync(true).GetAwaiter().GetResult();
    }
    private static byte[] Capture(EmulationSession session) => session.CaptureStateAsync().GetAwaiter().GetResult();
    private static void DrainState(object host) => Assert.IsTrue(SpinWait.SpinUntil(() =>
    { Call(host, "CompleteStateOperation"); return Field<Task?>(host, "stateOperation") is null; }, TimeSpan.FromSeconds(10)), "State action timed out.");

    private string Rom(string name)
    {
        string path = Path.Combine(root, name); File.WriteAllBytes(path, LinuxPlaytestTests.MakeBatteryRom(false)); return path;
    }
    private static T Field<T>(object host, string name) => (T)host.GetType().GetField(name, Private)!.GetValue(host)!;
    private static void Call(object host, string method, params object[] args) => host.GetType().GetMethod(method, Private)!.Invoke(host, args);
}
