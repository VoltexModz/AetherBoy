using System.Drawing;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Cartridges;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsPhase3Tests
{
    private string root = null!;
    [TestInitialize] public void Setup() { root = Path.Combine(Path.GetTempPath(), "aetherboy-win-phase3-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    [TestMethod]
    [DataRow("gb")][DataRow("gbc")][DataRow("gba")]
    public void ArchiveImportPersistsAndReusesIdentityWithoutImportingSaves(string extension)
    {
        byte[] rom = new byte[32768]; rom[0x200] = 17;
        string input = Path.Combine(root, "game.zip");
        using (var zip = ZipFile.Open(input, ZipArchiveMode.Create))
        {
            using (var entry = zip.CreateEntry("folder/game." + extension).Open()) entry.Write(rom);
            using (var entry = zip.CreateEntry("folder/game.sav").Open()) entry.Write(new byte[] { 77 });
        }
        byte[] before = File.ReadAllBytes(input);
        var paths = new WindowsDataPaths(Path.Combine(root, "data")); var library = new WindowsRomLibrary(paths);
        string imported;
        using (var prepared = RomArchiveSource.Open(input, paths.Roms)) imported = library.Import(prepared.Path);
        Assert.IsTrue(File.Exists(imported)); CollectionAssert.AreEqual(rom, File.ReadAllBytes(imported));
        Assert.IsFalse(File.Exists(library.GetSavePath(imported)));
        using (var prepared = RomArchiveSource.Open(input, paths.Roms)) Assert.AreEqual(imported, library.Import(prepared.Path));
        Assert.AreEqual(1, library.GetRoms().Count); CollectionAssert.AreEqual(before, File.ReadAllBytes(input));
        File.Delete(input); Assert.IsTrue(File.Exists(new WindowsRomLibrary(paths).GetRoms().Single()));
    }

    [STATestMethod]
    public void ArchivePickerDropStartupAndLibraryAcceptZipAndSevenZip()
    {
        foreach (string extension in new[] { "zip", "7z" })
        {
            string path = Path.Combine(root, "game." + extension); File.WriteAllBytes(path, []);
            var data = new DataObject(DataFormats.FileDrop, new[] { path });
            Assert.IsTrue(RomFiles.TryGetSingleDrop(data, out string? dropped)); Assert.AreEqual(path, dropped);
            Assert.IsFalse(RomFiles.IsSupportedPath(path), "Managed ROM enumeration still excludes archives.");
            using var picker = new frmRomBrowser(root);
            var list = (nanoboy.Controls.AetherList)picker.Controls.Find("romBrowserFiles", true).Single();
            Assert.IsTrue(list.Items.Cast<nanoboy.Controls.AetherListItem>().Any(item => item.Text == Path.GetFileName(path)));
            using var library = new frmRomLibrary([]);
            Call(library, "AcceptRom", path, false); Assert.AreEqual(path, library.SelectedRomPath);
            object?[] args = [new[] { path }, null];
            var program = typeof(frmNano).Assembly.GetType("nanoboy.Program")!;
            Assert.AreEqual(true, program.GetMethod("TryGetStartupRom", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args));
            Assert.AreEqual(path, args[1]);
        }
    }

    [TestMethod]
    public void PatchedRomSurvivesSourceRemovalAndTitleObeysSharedMetadataLimit()
    {
        string source = Path.Combine(root, "source.gb"), patch = Path.Combine(root, "hack.ips");
        byte[] original = new byte[32768]; File.WriteAllBytes(source, original);
        File.WriteAllBytes(patch, [.. "PATCH"u8.ToArray(), 0, 2, 0, 0, 1, 99, .. "EOF"u8.ToArray()]);
        var paths = new WindowsDataPaths(Path.Combine(root, "data"));
        var result = new WindowsRomPatchService(paths).ApplyAndImport(source, patch, new string('X', 110));
        Assert.IsNull(result.Warning); CollectionAssert.AreEqual(original, File.ReadAllBytes(source));
        File.Delete(source); File.Delete(patch);
        Assert.AreEqual(99, File.ReadAllBytes(result.Path)[0x200]);
        Assert.AreEqual(LibraryMetadata.MaximumTitleLength, new WindowsGameLibraryStore(paths).Read(result.Path).Title.Length);
        Assert.IsTrue(new WindowsRomLibrary(paths).GetRoms().Contains(result.Path));
    }

    [STATestMethod]
    public void RecordingControlsAndGermanHelpAreVisibleAndCaptureStatusFits()
    {
        using var main = new frmNano();
        Call(main, "OpenControlCenter");
        var center = (frmControlCenter)typeof(frmNano).GetField("controlCenter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
        try
        {
            Call(center, "ShowPage", "overview"); Application.DoEvents();
            var button = (AetherButton)center.Controls.Find("gameplayRecordingToggle", true).Single();
            Assert.AreEqual("Video aufnehmen", button.Text);
            Assert.IsTrue(TextRenderer.MeasureText(button.Text, button.Font).Width < button.Width);
            Assert.IsTrue(WindowsSettingsCatalog.Search("avi").Any(entry => entry.Page == "overview"));
            var panel = (Panel)center.Controls.Find("controlCenterPageOverview", true).Single();
            panel.ScrollControlIntoView(button); Application.DoEvents();
            string output = Path.Combine(AppContext.BaseDirectory, "phase3-capture-ui.png");
            using var bitmap = new Bitmap(center.Width, center.Height); center.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(output);
            Assert.IsTrue(button.Visible);
            foreach (Label label in button.Parent!.Controls.OfType<Label>())
            {
                var measured = TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                Assert.IsTrue(measured.Height <= label.Height + 3, $"Capture text clipped: {label.Text}");
            }
        }
        finally { center.Close(); main.Close(); }
    }

    private static object? Call(object target, string name, params object?[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);

    [STATestMethod]
    public void ArchiveChoiceDialogUsesThemeAndReturnsOnlyExplicitSelection()
    {
        string archive = Path.Combine(root, "many.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            for (int i = 0; i < 12; i++) { using var data = zip.CreateEntry($"Edition {i}/Pokémon Demo.gbc").Open(); data.Write(new byte[32768]); }
        var choices = Assert.Throws<RomArchiveSelectionRequiredException>(() => RomArchiveSource.Open(archive, root)).Choices;
        using var form = new frmArchiveChoice(choices); form.Show(); Application.DoEvents();
        var games = (nanoboy.Controls.AetherList)form.Controls.Find("archiveGames", true).Single();
        Assert.HasCount(12, games.Items); games.Items[0].Selected = false; games.Items[9].Selected = true; games.Items[9].EnsureVisible();
        Application.DoEvents();
        Assert.AreEqual(choices[9].DisplayName, form.Controls.Find("archiveEntryName", true).Single().Text);
        using (var bitmap = new Bitmap(form.Width, form.Height))
        { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(AppContext.BaseDirectory, "phase3-archive-selection.png")); }
        foreach (var button in new[] { "archiveOpen", "archiveCancel" }.Select(name => (AetherButton)form.Controls.Find(name, true).Single()))
            Assert.IsTrue(TextRenderer.MeasureText(button.Text, button.Font).Width < button.Width);
        Assert.IsNull(form.SelectedChoice);
        ((AetherButton)form.Controls.Find("archiveOpen", true).Single()).PerformClick();
        Assert.AreSame(choices[9], form.SelectedChoice);
        using var cancelled = new frmArchiveChoice(choices); cancelled.Show(); Application.DoEvents();
        ((AetherButton)cancelled.Controls.Find("archiveCancel", true).Single()).PerformClick();
        Assert.IsNull(cancelled.SelectedChoice);
    }

    [STATestMethod]
    public void ArchiveLoadSelectionAndCancelPreserveCurrentSession()
    {
        string archive = Path.Combine(root, "two.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            for (int i = 0; i < 2; i++)
            {
                byte[] rom = new byte[32768]; rom[0x100] = 0x18; rom[0x101] = 0xFE; rom[0x200] = (byte)(90 + i);
                using var data = zip.CreateEntry($"{i}.gb").Open(); data.Write(rom);
            }
        using var main = new frmNano();
        var preferences = (NanoboySettings)typeof(frmNano).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
        preferences.GpuRendering = false;
        preferences.AudioEnable = false;
        Call(main, "ApplyWindowsVideoSettings");
        main.Show(); Application.DoEvents();
        bool cancel = false;
        var unexpected = new List<string>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        using var timer = new System.Windows.Forms.Timer { Interval = 25 };
        timer.Tick += (_, _) =>
        {
            foreach (var dialog in Application.OpenForms.Cast<Form>().Where(f => f.GetType().Name == "AetherSignalDialog").ToArray())
            {
                static IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
                unexpected.Add(string.Join(" | ", Descendants(dialog).OfType<Label>().Select(label => label.Text)));
                dialog.Close();
            }
            if (Application.OpenForms.OfType<frmArchiveChoice>().FirstOrDefault() is not { } picker) return;
            if (watch.Elapsed > TimeSpan.FromSeconds(20)) { unexpected.Add("Archive selector timed out."); picker.Close(); return; }
            if (cancel) { picker.Close(); return; }
            var list = (nanoboy.Controls.AetherList)picker.Controls.Find("archiveGames", true).Single();
            list.Items[0].Selected = false; list.Items[1].Selected = true;
            ((AetherButton)picker.Controls.Find("archiveOpen", true).Single()).PerformClick();
        };
        timer.Start();
        using var ui = new UiThreadContext();
        try
        {
            ui.Install();
            var task = main.PrepareStartupRomAsync(archive); PumpUntil(() => task.IsCompleted); task.GetAwaiter().GetResult();
            Assert.IsEmpty(unexpected, string.Join("\n", unexpected));
            var current = typeof(frmNano).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main);
            Assert.IsNotNull(current);
            string loaded = (string)typeof(frmNano).GetField("currentRomPath", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
            Assert.AreEqual((byte)91, File.ReadAllBytes(loaded)[0x200]);
            cancel = true;
            ui.Install();
            task = main.PrepareStartupRomAsync(archive); PumpUntil(() => task.IsCompleted); task.GetAwaiter().GetResult();
            Assert.AreSame(current, typeof(frmNano).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main));
            Assert.IsNull(typeof(frmNano).GetField("romPreparation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main));
        }
        finally { timer.Stop(); main.Close(); }
    }

    [STATestMethod]
    [DataRow(false)][DataRow(true)]
    public void PatchStartIsExplicitAndResultPersists(bool launch)
    {
        using var ui = new UiThreadContext();
        string rom = Path.Combine(root, "base.gb"), patch = Path.Combine(root, "change.ips");
        File.WriteAllBytes(rom, new byte[32768]);
        File.WriteAllBytes(patch, [.. "PATCH"u8.ToArray(), 0, 2, 0, 0, 1, 43, .. "EOF"u8.ToArray()]);
        using var form = new frmRomPatcher(rom); form.Show(); Application.DoEvents();
        ((nanoboy.Controls.AetherTextBox)form.Controls.Find("patchFile", true).Single()).Text = patch;
        ui.Click((AetherButton)form.Controls.Find(launch ? "patchPlay" : "patchApply", true).Single());
        PumpUntil(() => form.ImportedRomPath is not null && (launch ? form.LaunchRequested : form.Controls.Find("patchDone", true).Single().Enabled));
        Assert.AreEqual(launch, form.LaunchRequested);
        Assert.AreEqual((byte)43, File.ReadAllBytes(form.ImportedRomPath!)[0x200]);
        Assert.AreEqual((byte)0, File.ReadAllBytes(rom)[0x200]);
        if (!launch)
        {
            using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
            image.Save(Path.Combine(AppContext.BaseDirectory, "phase3-patch-play.png"));
            foreach (var button in new[] { "patchApply", "patchPlay", "patchDone" }.Select(name => (AetherButton)form.Controls.Find(name, true).Single()))
                Assert.IsTrue(TextRenderer.MeasureText(button.Text, button.Font).Width < button.Width, button.Text);
            ((AetherButton)form.Controls.Find("patchPlay", true).Single()).PerformClick();
            Assert.IsTrue(form.LaunchRequested);
        }
    }

    [STATestMethod]
    public void FailedPatchCannotRequestLaunchAndPatchPickerHidesArchives()
    {
        using var ui = new UiThreadContext();
        string rom = Path.Combine(root, "source.gb"), patch = Path.Combine(root, "invalid.ips");
        File.WriteAllBytes(rom, new byte[32768]); File.WriteAllBytes(patch, [1]);
        File.WriteAllBytes(Path.Combine(root, "game.zip"), []);
        using var picker = new frmRomBrowser(root, includeArchives: false);
        Assert.IsFalse(((nanoboy.Controls.AetherList)picker.Controls.Find("romBrowserFiles", true).Single()).Items.Cast<nanoboy.Controls.AetherListItem>().Any(item => item.Text.EndsWith(".zip")));
        using var form = new frmRomPatcher(rom); form.Show(); Application.DoEvents();
        ((nanoboy.Controls.AetherTextBox)form.Controls.Find("patchFile", true).Single()).Text = patch;
        ui.Click((AetherButton)form.Controls.Find("patchPlay", true).Single());
        PumpUntil(() => form.Controls.Find("patchDone", true).Single().Enabled);
        Assert.IsFalse(form.LaunchRequested); Assert.IsNull(form.ImportedRomPath);
        form.Close();
    }

    private static void PumpUntil(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!ready() && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(5); }
        Assert.IsTrue(ready(), "UI operation did not finish.");
    }

    // Match Application.Run; the test runner's STA and DoEvents do not keep a UI context installed.
    private sealed class UiThreadContext : IDisposable
    {
        private readonly SynchronizationContext? previous = SynchronizationContext.Current;
        private readonly WindowsFormsSynchronizationContext context = new();
        private readonly bool previousCheck = Control.CheckForIllegalCrossThreadCalls;
        public UiThreadContext() { Install(); Control.CheckForIllegalCrossThreadCalls = true; }
        public void Install() => SynchronizationContext.SetSynchronizationContext(context);
        public void Click(AetherButton button) { Install(); button.PerformClick(); }
        public void Dispose()
        { SynchronizationContext.SetSynchronizationContext(previous); Control.CheckForIllegalCrossThreadCalls = previousCheck; context.Dispose(); }
    }
}
