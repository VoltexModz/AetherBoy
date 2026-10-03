using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using AetherBoy.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsGameDataTests
{
    private string root = null!;
    private WindowsDataPaths paths = null!;
    private WindowsRomLibrary library = null!;
    private WindowsSaveStateStore states = null!;
    private WindowsGameLibraryStore catalog = null!;

    [TestInitialize]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "aetherboy-game-data-" + Guid.NewGuid().ToString("N"));
        paths = new(Path.Combine(root, "data")); library = new(paths); states = new(paths); catalog = new(paths);
        Directory.CreateDirectory(root);
    }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    private string Rom(string title, byte variant = 0)
    {
        string source = Path.Combine(root, title + ".gb");
        byte[] data = new byte[0x8000];
        new byte[] { 0x04, 0x18, 0xFD }.CopyTo(data, 0x100); // INC B / JR: generated program, no commercial ROM.
        Encoding.ASCII.GetBytes(title[..Math.Min(title.Length, 14)]).CopyTo(data, 0x134);
        data[0x200] = variant;
        File.WriteAllBytes(source, data);
        return library.Import(source);
    }
    private static SavedCheckpoint Checkpoint(byte value, string title = "Generated") => new(new[] { value, (byte)2, (byte)3 },
        new StatePreview(title, DateTimeOffset.UtcNow, "", WindowsSaveStateStore.EncodeFrame(
            Enumerable.Repeat(Color.MediumPurple.ToArgb(), 23040).ToArray(), VideoGeometry.GameBoy), 100));

    [TestMethod]
    public void LibraryTitleIsMetadataAndNeverChangesRomOrSaveIdentity()
    {
        string rom = Rom("RENAME");
        string identity = library.GetIdentity(rom);
        string savePath = library.GetSavePath(rom);
        byte[] original = File.ReadAllBytes(rom);

        catalog.Update(rom, old => old with { Title = "Mein Spiel", HasCustomTitle = true });
        Assert.AreEqual("Mein Spiel", new WindowsGameLibraryStore(paths).Read(rom).Title);
        Assert.AreEqual(identity, library.GetIdentity(rom));
        Assert.AreEqual(savePath, library.GetSavePath(rom));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(rom));

        catalog.Update(rom, old => old with { Title = "RENAME", HasCustomTitle = false });
        Assert.AreEqual("RENAME", new WindowsGameLibraryStore(paths).Read(rom).Title);
    }

    [TestMethod]
    public void ManualAndResumeSlotsStaySeparateAndKeepPreviousRawState()
    {
        string rom = Rom("SLOTS");
        states.Write(rom, 1, Checkpoint(1));
        states.Write(rom, 0, Checkpoint(9));
        Assert.AreEqual((byte)1, states.Read(rom, 1)[0]);
        Assert.AreEqual((byte)9, states.Read(rom, 0)[0]);
        states.Write(rom, 1, Checkpoint(4));
        Assert.AreEqual((byte)1, File.ReadAllBytes(states.PathFor(rom, 1) + ".bak")[0]);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => states.PathFor(rom, 6));
        Assert.IsFalse(File.Exists(library.GetStatePath(rom, 5)));
    }

    [TestMethod]
    public void PreviewsAreBoundToActualStateBytesAndLegacyStatesStillLoad()
    {
        string rom = Rom("PREVIEW");
        states.Write(rom, 1, Checkpoint(1));
        Assert.IsNotNull(states.Inspect(rom, 1).Preview);
        File.WriteAllBytes(states.PathFor(rom, 1), new byte[] { 8, 7, 6 });
        Assert.IsNull(states.Inspect(rom, 1).Preview);
        Assert.IsNotNull(states.Inspect(rom, 1).Warning);
        File.WriteAllBytes(states.PathFor(rom, 2), new byte[] { 4, 5 });
        Assert.IsNull(states.Inspect(rom, 2).Preview);
        CollectionAssert.AreEqual(new byte[] { 4, 5 }, states.Read(rom, 2));
        Assert.IsFalse(states.Inspect(rom, 3).Exists);
    }

    [TestMethod]
    public void FailedMetadataCommitDoesNotReplaceAnExistingState()
    {
        string rom = Rom("FAILED WRITE");
        states.Write(rom, 1, Checkpoint(1));
        string metadata = states.PathFor(rom, 1) + ".preview.json";
        File.Delete(metadata);
        Directory.CreateDirectory(metadata); // Force an I/O failure at the optional metadata destination.
        Assert.Throws<IOException>(() => states.Write(rom, 1, Checkpoint(9)));
        Assert.AreEqual((byte)1, states.Read(rom, 1)[0]);
    }

    [TestMethod]
    public void CorruptMetadataFallsBackButUnreadableDataIsNotSilentlyOverwritten()
    {
        string rom = Rom("CATALOG");
        catalog.Update(rom, entry => entry with { Favorite = true, PlayedSeconds = 120 });
        catalog.Update(rom, entry => entry with { PlayedSeconds = 180 });
        string file = Path.Combine(paths.Root, "Library", library.GetIdentity(rom) + ".json");
        File.WriteAllText(file, "broken");
        Assert.IsTrue(catalog.Read(rom).Favorite);
        Assert.AreEqual(120d, catalog.Read(rom).PlayedSeconds);
        catalog.Update(rom, entry => entry with { PlayedSeconds = 240 });
        File.WriteAllText(file, "broken");
        Assert.AreEqual(120d, catalog.Read(rom).PlayedSeconds, "Repair must preserve the readable backup.");
        File.WriteAllText(file + ".bak", "also broken");
        Assert.ThrowsExactly<InvalidDataException>(() => catalog.Update(rom, entry => entry with { Favorite = false }));
        Assert.AreEqual("broken", File.ReadAllText(file));
    }

    [TestMethod]
    public void ProfilesLayerValuesWithoutChangingGlobalsAndRemainSeparatePerRom()
    {
        string first = Rom("PROFILE A"), second = Rom("PROFILE B", 1);
        var store = new WindowsGameProfileStore(paths);
        using var settings = new NanoboySettings(store);
        int globalVolume = settings.AudioVolume, globalPalette = settings.PaletteIndex;
        bool globalGpu = settings.GpuRendering;
        settings.UseGameProfile(first);
        settings.EnableGameProfile(true);
        settings.FlushPendingSavesAsync().GetAwaiter().GetResult();
        Assert.AreEqual(0, store.Read(first).Overrides.Count);
        settings.AudioVolume = 23; settings.PaletteIndex = 3; settings.GpuRendering = !globalGpu;
        settings.KeyA = Keys.J;
        Assert.AreEqual(globalVolume, nanoboy.Properties.Settings.Default.AudioVolume);
        Assert.AreEqual(globalPalette, nanoboy.Properties.Settings.Default.PaletteIndex);
        Assert.AreEqual(globalGpu, nanoboy.Properties.Settings.Default.GpuRendering);
        settings.UseGameProfile(second);
        Assert.AreEqual(globalVolume, settings.AudioVolume);
        settings.EnableGameProfile(true); settings.AudioVolume = 42;
        settings.UseGameProfile(first);
        Assert.AreEqual(23, settings.AudioVolume); Assert.AreEqual(Keys.J, settings.KeyA);
        Assert.AreEqual(!globalGpu, settings.GpuRendering);
        settings.EnableGameProfile(false); Assert.AreEqual(globalVolume, settings.AudioVolume);
        settings.EnableGameProfile(true); Assert.AreEqual(23, settings.AudioVolume);
        settings.ResetGameProfile(); Assert.AreEqual(globalVolume, settings.AudioVolume);
        settings.FlushPendingSavesAsync().GetAwaiter().GetResult();
        Assert.AreEqual(0, store.Read(first).Overrides.Count);
    }

    [TestMethod]
    public void ApplyingInheritedSettingsDoesNotCreateOverridesAndBadFieldsFallBack()
    {
        string rom = Rom("INHERIT");
        var store = new WindowsGameProfileStore(paths);
        using var settings = new NanoboySettings(store);
        settings.UseGameProfile(rom); settings.EnableGameProfile(true);
        settings.AudioVolume = settings.AudioVolume; settings.PaletteIndex = settings.PaletteIndex;
        settings.FlushPendingSavesAsync().GetAwaiter().GetResult();
        Assert.AreEqual(0, store.Read(rom).Overrides.Count);
        store.Write(rom, new GameSettingsProfile { Enabled = true,
            Overrides = new() { ["AudioVolume"] = "bad", ["SaveSlot"] = "5", ["Frameskip"] = "999" } });
        settings.UseGameProfile(rom);
        Assert.AreEqual(nanoboy.Properties.Settings.Default.AudioVolume, settings.AudioVolume);
        Assert.AreEqual(nanoboy.Properties.Settings.Default.SaveSlot, settings.SaveSlot);
        Assert.AreEqual(4, settings.Frameskip);
    }

    [TestMethod]
    public void ProfileWriteFailureReportsErrorAndKeepsPendingValueForRetry()
    {
        string rom = Rom("PROFILE IO");
        var store = new WindowsGameProfileStore(paths);
        using var settings = new NanoboySettings(store);
        settings.UseGameProfile(rom); settings.EnableGameProfile(true); settings.AudioVolume = 19;
        settings.FlushPendingSavesAsync().GetAwaiter().GetResult();
        int global = nanoboy.Properties.Settings.Default.AudioVolume;
        string file = Path.Combine(paths.Settings, "Profiles", library.GetIdentity(rom) + ".json");
        File.Delete(file); Directory.CreateDirectory(file);
        Exception? failure = null; settings.ProfileSaveFailed += ex => failure = ex;
        settings.AudioVolume = 71;
        Assert.ThrowsExactly<InvalidOperationException>(() => settings.FlushPendingSavesAsync().GetAwaiter().GetResult());
        Assert.IsNotNull(failure);
        Assert.AreEqual(71, settings.AudioVolume);
        Assert.AreEqual(global, nanoboy.Properties.Settings.Default.AudioVolume);
        Directory.Delete(file);
        settings.FlushPendingSavesAsync().GetAwaiter().GetResult();
        Assert.AreEqual(71, store.Read(rom).Overrides["AudioVolume"] is string value ? int.Parse(value) : -1);
    }

    [TestMethod]
    public void ThumbnailDecoderRejectsOversizedAndNonPngData()
    {
        Assert.IsNull(WindowsSaveStateStore.DecodePreview("not-base64"));
        byte[] data = Convert.FromBase64String(Checkpoint(1).Preview.Png!);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(16, 4), 100000);
        Assert.IsNull(WindowsSaveStateStore.DecodePreview(Convert.ToBase64String(data)));
        using Bitmap? image = WindowsSaveStateStore.DecodePreview(Checkpoint(1).Preview.Png);
        Assert.IsNotNull(image); Assert.AreEqual(new Size(160, 144), image.Size);
    }

    [STATestMethod]
    public void WindowSavesLoadsUndoesAndCreatesResumeOnClose()
    {
        string original = Rom("WINDOW TEST");
        string imported = WindowsRomLibrary.Default.Import(original);
        bool audio = nanoboy.Properties.Settings.Default.AudioEnable;
        bool boot = nanoboy.Properties.Settings.Default.BootRomEnable;
        bool gpu = nanoboy.Properties.Settings.Default.GpuRendering;
        try
        {
            nanoboy.Properties.Settings.Default.AudioEnable = false;
            nanoboy.Properties.Settings.Default.BootRomEnable = false;
            nanoboy.Properties.Settings.Default.GpuRendering = false; // GPU coverage has a separate hardware opt-in.
            using var main = new frmNano(); main.Show(); main.LoadRomFile(imported);
            var session = Field<EmulationSession>(main, "session");
            PumpUntil(() => session.LatestSnapshot.EmulatedFrameCount >= 3);
            Pump(session.SetPausedAsync(true));
            Pump(InvokeTask(main, "SaveCheckpointAsync", 1, false));
            StateSlotInfo slot = WindowsSaveStateStore.Default.Inspect(imported, 1);
            Assert.IsNotNull(slot.Preview);
            long frame = session.LatestSnapshot.EmulatedFrameCount;
            Pump(session.SetPausedAsync(false)); PumpUntil(() => session.LatestSnapshot.EmulatedFrameCount >= frame + 4);
            Pump(session.SetPausedAsync(true));
            byte[] before = PumpResult(session.CaptureStateAsync());
            Pump(InvokeTask(main, "LoadCheckpointAsync", 1, false));
            Assert.IsNotNull(Field<SavedCheckpoint?>(main, "undoQuickLoad"));
            Pump(InvokeTask(main, "LoadCheckpointAsync", 0, true));
            CollectionAssert.AreEqual(before, PumpResult(session.CaptureStateAsync()));
            Assert.IsNull(Field<SavedCheckpoint?>(main, "undoQuickLoad"));
            main.Close();
            Assert.IsTrue(File.Exists(WindowsSaveStateStore.Default.PathFor(imported, 0)));
            Assert.IsTrue(File.Exists(WindowsSaveStateStore.Default.PathFor(imported, 1)));
            Assert.IsNotNull(WindowsGameLibraryStore.Default.Read(imported).LastPlayedUtc);
            using var resumed = new frmNano(); resumed.Show(); resumed.LoadRomFile(imported, resume: true);
            Field<System.Windows.Forms.Timer>(resumed, "updateTimer").Stop();
            var resumedSession = Field<EmulationSession>(resumed, "session");
            Pump(resumedSession.SetPausedAsync(true));
            Call(resumed, "TrackGameActivity", resumedSession.LatestSnapshot);
            PumpUntil(() => !Field<bool>(resumed, "pendingResume") && !Field<bool>(resumed, "stateOperationInProgress"));
            Assert.IsNotNull(Field<SavedCheckpoint?>(resumed, "undoQuickLoad"), "Resume must execute the restore path.");
            CollectionAssert.AreEqual(WindowsSaveStateStore.Default.Read(imported, 0), PumpResult(resumedSession.CaptureStateAsync()));
            var settings = Field<NanoboySettings>(resumed, "settings");
            int globalVolume = nanoboy.Properties.Settings.Default.AudioVolume;
            settings.EnableGameProfile(true); settings.AudioVolume = 17;
            Call(resumed, "ApplyGameProfilePreferences");
            Assert.AreEqual(globalVolume, nanoboy.Properties.Settings.Default.AudioVolume);
            Assert.AreEqual(17, settings.AudioVolume);
            Call(resumed, "OpenControlCenter");
            var center = Field<frmControlCenter>(resumed, "controlCenter");
            Application.DoEvents();
            Call(center, "ShowPage", "system"); Application.DoEvents();
            Capture(center, "game-profile.png"); center.Close();
            resumed.Close();
        }
        finally
        {
            nanoboy.Properties.Settings.Default.AudioEnable = audio;
            nanoboy.Properties.Settings.Default.BootRomEnable = boot;
            nanoboy.Properties.Settings.Default.GpuRendering = gpu;
            RemoveTestImport(imported);
        }
    }

    [STATestMethod]
    public void LibraryFiltersPersistsFavoritesAndGalleryShowsSixSlots()
    {
        string imported = WindowsRomLibrary.Default.Import(Rom("VAULT TEST"));
        try
        {
            WindowsSaveStateStore.Default.Write(imported, 1, Checkpoint(1, "VAULT TEST"));
            WindowsSaveStateStore.Default.Write(imported, 0, Checkpoint(2, "VAULT TEST"));
            WindowsGameLibraryStore.Default.Update(imported, entry => entry with { Title = "VAULT TEST", PlayedSeconds = 3720,
                PreviewPng = Checkpoint(1).Preview.Png, LastPlayedUtc = DateTimeOffset.UtcNow });
            using var vault = new frmRomLibrary(new[] { imported }); vault.Show(); Application.DoEvents();
            var list = Field<nanoboy.Controls.AetherList>(vault, "romList");
            ((nanoboy.Controls.AetherTextBox)vault.Controls.Find("romLibrarySearch", true).Single()).Text = "VAULT TEST";
            Assert.AreEqual(1, list.Items.Count);
            list.Items[0].Selected = true; Application.DoEvents();
            ((AetherButton)vault.Controls.Find("romLibraryFavoriteButton", true).Single()).PerformClick();
            Assert.IsTrue(WindowsGameLibraryStore.Default.Read(imported).Favorite);
            ((AetherSelect)vault.Controls.Find("romLibrarySystemFilter", true).Single()).SelectedItem = "GBA";
            Assert.AreEqual(0, list.Items.Count);
            ((AetherSelect)vault.Controls.Find("romLibrarySystemFilter", true).Single()).SelectedItem = "GB";
            Assert.AreEqual(1, list.Items.Count);
            Application.DoEvents();
            using (var tile = new Bitmap(list.Width, list.Height))
            {
                list.DrawToBitmap(tile, new Rectangle(Point.Empty, tile.Size));
                Rectangle item = list.Items[0].Bounds;
                Assert.AreEqual(Color.MediumPurple.ToArgb(), tile.GetPixel(item.Left + item.Width / 2, item.Top + 35).ToArgb(),
                    "The actual owner-drawn tile must contain its preview, not a blank list surface.");
            }
            Capture(vault, "cartridge-vault.png");
            vault.Close();
            using var gallery = new frmStateGallery(imported, (_, _) => Task.CompletedTask, (_, _) => Task.CompletedTask, () => true, () => false);
            gallery.Show(); Pump(gallery.RefreshSlotsAsync());
            for (int n = 0; n <= 5; n++) Assert.AreEqual(1, gallery.Controls.Find($"stateGalleryLoad{n}", true).Length);
            Capture(gallery, "state-gallery.png");
            gallery.Close();
        }
        finally { RemoveTestImport(imported); }
    }

    private static void RemoveTestImport(string imported)
    {
        // AssemblyInitialize redirects all UI storage to the test-owned temporary root.
        var paths = WindowsDataPaths.Default;
        Assert.IsTrue(paths.Root.Contains("aetherboy-windows-tests-", StringComparison.Ordinal));
        string id = WindowsRomLibrary.Default.GetIdentity(imported);
        foreach (string parent in new[] { paths.Roms, paths.States, paths.Saves })
        { string directory = Path.Combine(parent, id); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        foreach (string parent in new[] { Path.Combine(paths.Root, "Library"), Path.Combine(paths.Settings, "Profiles") })
        foreach (string suffix in new[] { ".json", ".json.bak" })
        { string file = Path.Combine(parent, id + suffix); if (File.Exists(file)) File.Delete(file); }
    }
    private static void PumpUntil(Func<bool> predicate)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!predicate()) { if (timeout.Elapsed.TotalSeconds > 8) Assert.Fail("UI/session operation timed out."); Application.DoEvents(); Thread.Sleep(1); }
    }
    private static void Pump(Task task) { PumpUntil(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static T PumpResult<T>(Task<T> task) { Pump(task); return task.GetAwaiter().GetResult(); }
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    private static Task InvokeTask(object instance, string method, params object[] args) =>
        (Task)instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, args)!;
    private static void Call(object instance, string method, params object[] args) =>
        instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, args);
    private static void Capture(Form form, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(directory, name));
    }
}
