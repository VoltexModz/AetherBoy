using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsUpsPatchTests
{
    private string root = null!;
    [TestInitialize] public void Setup() { root = Path.Combine(Path.GetTempPath(), "aetherboy-ups-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    [TestMethod]
    [DataRow(".gb")]
    [DataRow(".gbc")]
    [DataRow(".gba")]
    public void ImportAndUndoPreserveBothRomsAndExistingOriginalData(string extension)
    {
        var fixture = Fixture(extension);
        var paths = new WindowsDataPaths(Path.Combine(root, "data"));
        var library = new WindowsRomLibrary(paths); var metadata = new WindowsGameLibraryStore(paths);
        var profiles = new WindowsGameProfileStore(paths); var service = new WindowsRomPatchService(paths);
        string original = library.Import(fixture.Source);
        metadata.Update(original, e => e with { Title = "My original", HasCustomTitle = true, Favorite = true, PlayedSeconds = 720 });
        profiles.Write(original, new() { Enabled = true, Overrides = new() { ["SoundVolume"] = "42" } });
        File.WriteAllBytes(library.GetSavePath(original), [3, 1, 4]);
        File.WriteAllBytes(library.GetStatePath(original, 1), [1, 5, 9]);
        var forward = service.ApplyAndImport(original, fixture.Patch, "UPS Hack");
        Assert.AreEqual("UPS", forward.Format); Assert.IsTrue(forward.ChecksumsVerified); Assert.IsFalse(forward.Reversed);
        CollectionAssert.AreEqual(File.ReadAllBytes(fixture.Target), File.ReadAllBytes(forward.Path));
        Assert.AreEqual(65536L, new FileInfo(forward.Path).Length);
        Assert.AreEqual("UPS Hack", metadata.Read(forward.Path).Title);
        Assert.IsFalse(File.Exists(library.GetSavePath(forward.Path)));
        Assert.IsFalse(profiles.Read(forward.Path).Enabled);
        File.WriteAllBytes(library.GetSavePath(forward.Path), [2, 7, 1]);

        var restored = service.ApplyAndImport(forward.Path, fixture.Patch, "Must not rename the original", reverseUps: true);
        Assert.AreEqual(original, restored.Path); Assert.IsTrue(restored.Reversed);
        Assert.AreEqual(32768L, new FileInfo(restored.Path).Length);
        CollectionAssert.AreEqual(File.ReadAllBytes(fixture.Source), File.ReadAllBytes(original));
        CollectionAssert.AreEqual(File.ReadAllBytes(fixture.Target), File.ReadAllBytes(forward.Path));
        CollectionAssert.AreEqual(new byte[] { 3, 1, 4 }, File.ReadAllBytes(library.GetSavePath(original)));
        CollectionAssert.AreEqual(new byte[] { 1, 5, 9 }, File.ReadAllBytes(library.GetStatePath(original, 1)));
        CollectionAssert.AreEqual(new byte[] { 2, 7, 1 }, File.ReadAllBytes(library.GetSavePath(forward.Path)));
        Assert.AreEqual("My original", metadata.Read(original).Title); Assert.IsTrue(metadata.Read(original).Favorite);
        Assert.AreEqual(720d, metadata.Read(original).PlayedSeconds);
        Assert.AreEqual("42", profiles.Read(original).Overrides["SoundVolume"]);
        Assert.AreEqual(2, library.GetRoms().Count);
        Assert.AreEqual(0, Directory.GetDirectories(paths.Roms, ".patch-*").Length);
        string manifest = File.ReadAllText(Directory.GetFiles(Path.GetDirectoryName(original)!, "patch-*.json").Single());
        Assert.IsFalse(manifest.Contains(root));
        using var json = JsonDocument.Parse(manifest);
        Assert.IsTrue(json.RootElement.GetProperty("reversed").GetBoolean());
        Assert.IsTrue(json.RootElement.GetProperty("checksums_verified").GetBoolean());
    }

    [TestMethod]
    public void UndoCanRestoreAnOriginalNotPreviouslyInTheLibrary()
    {
        var fixture = Fixture(".gba"); var paths = new WindowsDataPaths(Path.Combine(root, "data"));
        File.WriteAllBytes(Path.ChangeExtension(fixture.Target, "sav"), [8, 8, 8]);
        var result = new WindowsRomPatchService(paths).ApplyAndImport(fixture.Target, fixture.Patch, "Restored original", true);
        CollectionAssert.AreEqual(File.ReadAllBytes(fixture.Source), File.ReadAllBytes(result.Path));
        Assert.AreEqual("Restored original", new WindowsGameLibraryStore(paths).Read(result.Path).Title);
        Assert.IsFalse(File.Exists(new WindowsRomLibrary(paths).GetSavePath(result.Path)));
        Assert.AreEqual(1, new WindowsRomLibrary(paths).GetRoms().Count);
    }

    [TestMethod]
    public void InvalidCrcWrongDirectionAndUnchangedPatchNeverCreateLibraryFiles()
    {
        var fixture = Fixture(".gb"); var paths = new WindowsDataPaths(Path.Combine(root, "data"));
        var service = new WindowsRomPatchService(paths);
        Assert.Throws<InvalidDataException>(() => service.ApplyAndImport(fixture.Target, fixture.Patch, "Wrong direction"));
        Assert.Throws<InvalidDataException>(() => service.ApplyAndImport(fixture.Source, fixture.Patch, "Wrong direction", true));
        byte[] patch = File.ReadAllBytes(fixture.Patch); patch[^1] ^= 1; File.WriteAllBytes(fixture.Patch, patch);
        Assert.Throws<InvalidDataException>(() => service.ApplyAndImport(fixture.Source, fixture.Patch, "Corrupt"));
        byte[] source = File.ReadAllBytes(fixture.Source);
        var unchanged = new List<byte>("UPS1"u8.ToArray()); Number(unchanged, source.Length); Number(unchanged, source.Length);
        WritePatch(fixture.Patch, unchanged, source, source);
        Assert.Throws<InvalidDataException>(() => service.ApplyAndImport(fixture.Source, fixture.Patch, "Unchanged"));
        Assert.IsFalse(Directory.Exists(paths.Roms));
    }

    [TestMethod]
    public void UnreadableOptionalMetadataDoesNotHideASuccessfulImport()
    {
        var fixture = Fixture(".gb"); var paths = new WindowsDataPaths(Path.Combine(root, "data"));
        var service = new WindowsRomPatchService(paths);
        var first = service.ApplyAndImport(fixture.Source, fixture.Patch, "UPS Hack");
        string metadataPath = Path.Combine(paths.Root, "Library", first.TargetSha256 + ".json");
        File.WriteAllText(metadataPath, "invalid JSON"); File.WriteAllText(metadataPath + ".bak", "invalid backup");
        var again = service.ApplyAndImport(fixture.Source, fixture.Patch, "UPS Hack");
        Assert.AreEqual(first.Path, again.Path); Assert.IsNotNull(again.Warning);
        CollectionAssert.AreEqual(File.ReadAllBytes(fixture.Target), File.ReadAllBytes(again.Path));
    }

    [STATestMethod]
    [DataRow("IPS")]
    [DataRow("BPS")]
    [DataRow("UPS")]
    public void InvalidPatchesDisplayAnErrorAndKeepUiEditable(string format)
    {
        using var ui = new UiThreadContext();
        var fixture = Fixture(".gba");
        if (format == "UPS")
        {
            byte[] corrupt = File.ReadAllBytes(fixture.Patch); corrupt[^1] ^= 1; File.WriteAllBytes(fixture.Patch, corrupt);
        }
        else File.WriteAllText(fixture.Patch, format == "IPS" ? "PATCHbroken" : "BPS1broken");
        using var form = new frmRomPatcher(fixture.Source); form.Show(); Application.DoEvents();
        ((nanoboy.Controls.AetherTextBox)form.Controls.Find("patchFile", true).Single()).Text = fixture.Patch;
        var apply = (AetherButton)form.Controls.Find("patchApply", true).Single();
        int before = WindowsRomLibrary.Default.GetRoms().Count;
        ui.Click(apply); PumpUntil(() => form.Controls.Find("patchDone", true).Single().Enabled);
        Assert.IsNull(form.ImportedRomPath);
        StringAssert.Contains(form.Controls.Find("patchStatus", true).Single().Text, "Patch nicht importiert:");
        Assert.IsTrue(apply.Enabled); Assert.IsTrue(form.Controls.Find("patchReverseUps", true).Single().Enabled);
        Assert.AreEqual(before, WindowsRomLibrary.Default.GetRoms().Count);
        if (format == "UPS") Capture(form, "patch-lab-ups-error.png");
        form.Close();
    }

    [STATestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PatchLabAppliesUpsAndRecoversFromWrongDirectionInUi(bool reverse)
    {
        using var ui = new UiThreadContext();
        var fixture = Fixture(".gba");
        using var form = new frmRomPatcher(reverse ? fixture.Target : fixture.Source); form.Show(); Application.DoEvents();
        ((nanoboy.Controls.AetherTextBox)form.Controls.Find("patchFile", true).Single()).Text = fixture.Patch;
        ((nanoboy.Controls.AetherTextBox)form.Controls.Find("patchTitle", true).Single()).Text = reverse ? "UPS UI ORIGINAL" : "UPS UI HACK";
        var checkbox = (CheckBox)form.Controls.Find("patchReverseUps", true).Single();
        var apply = (AetherButton)form.Controls.Find("patchApply", true).Single();
        Control status = form.Controls.Find("patchStatus", true).Single(), done = form.Controls.Find("patchDone", true).Single();
        Assert.IsFalse(checkbox.Checked);
        if (reverse)
        {
            ui.Click(apply); PumpUntil(() => done.Enabled);
            Assert.IsNull(form.ImportedRomPath); StringAssert.Contains(status.Text, "bereits gepatcht");
            Assert.IsTrue(checkbox.Enabled); Assert.IsTrue(apply.Enabled);
            checkbox.Checked = true; Assert.AreEqual("Original wiederherstellen", apply.Text);
        }
        ui.Click(apply); PumpUntil(() => done.Enabled);
        Assert.IsNotNull(form.ImportedRomPath, status.Text);
        CollectionAssert.AreEqual(File.ReadAllBytes(reverse ? fixture.Source : fixture.Target), File.ReadAllBytes(form.ImportedRomPath));
        StringAssert.Contains(status.Text, "UPS:"); StringAssert.Contains(status.Text, "CRC32-Prüfungen bestanden");
        Assert.IsFalse(checkbox.Enabled); Assert.IsFalse(apply.Enabled);
        Capture(form, reverse ? "patch-lab-ups-reverse.png" : "patch-lab-ups.png"); form.Close();
    }

    private (string Source, string Target, string Patch) Fixture(string extension)
    {
        byte[] source = new byte[32768];
        source[0] = 0x55; source[1] = 0x50; source[2] = 0x53; // Synthetic data only; never launched.
        if (extension == ".gbc") source[0x143] = 0x80;
        byte[] target = new byte[65536]; source.CopyTo(target, 0); target[0x200] = 99; target[^1] = 51;
        var bytes = new List<byte>("UPS1"u8.ToArray()); Number(bytes, source.Length); Number(bytes, target.Length);
        Number(bytes, 0x200); bytes.AddRange([99, 0]);
        Number(bytes, target.Length - 1 - 0x202); bytes.AddRange([51, 0]);
        string original = Path.Combine(root, "original" + extension), modified = Path.Combine(root, "modified" + extension);
        string patch = Path.Combine(root, "Synthetic UPS.ups");
        File.WriteAllBytes(original, source); File.WriteAllBytes(modified, target); WritePatch(patch, bytes, source, target);
        return (original, modified, patch);
    }
    private static void WritePatch(string path, List<byte> bytes, byte[] source, byte[] target)
    {
        bytes.AddRange(BitConverter.GetBytes(Crc(source))); bytes.AddRange(BitConverter.GetBytes(Crc(target)));
        bytes.AddRange(BitConverter.GetBytes(Crc(bytes.ToArray()))); File.WriteAllBytes(path, bytes.ToArray());
    }
    private static void Number(List<byte> bytes, long value)
    {
        while (true)
        {
            byte chunk = (byte)(value & 127); value >>= 7;
            if (value == 0) { bytes.Add((byte)(chunk | 128)); return; }
            bytes.Add(chunk); value--;
        }
    }
    private static uint Crc(byte[] bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte b in bytes)
        {
            crc ^= b;
            for (int n = 0; n < 8; n++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xEDB88320u);
        }
        return ~crc;
    }
    // STA alone does not guarantee a WinForms synchronization context in the test
    // runner. Match Application.Run so async dialog continuations stay on this UI
    // thread, instead of racing assertions on partially updated controls.
    private sealed class UiThreadContext : IDisposable
    {
        private readonly SynchronizationContext? previous = SynchronizationContext.Current;
        private readonly WindowsFormsSynchronizationContext context = new();
        private readonly bool previousCheck = Control.CheckForIllegalCrossThreadCalls;
        internal UiThreadContext()
        {
            SynchronizationContext.SetSynchronizationContext(context);
            Control.CheckForIllegalCrossThreadCalls = true;
        }
        internal void Click(AetherButton button)
        {
            // DoEvents tears down its temporary message loop and can uninstall
            // the context. Reinstall immediately before starting an async action.
            SynchronizationContext.SetSynchronizationContext(context);
            button.PerformClick();
        }
        public void Dispose()
        {
            SynchronizationContext.SetSynchronizationContext(previous);
            Control.CheckForIllegalCrossThreadCalls = previousCheck;
            context.Dispose();
        }
    }
    private static void PumpUntil(Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        while (!done()) { Application.DoEvents(); Thread.Sleep(8); if (watch.Elapsed.TotalSeconds > 10) Assert.Fail("UPS UI task timed out."); }
        Application.DoEvents();
    }
    private static void Capture(Form form, string name)
    {
        string? output = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(output)) return;
        Directory.CreateDirectory(output);
        using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
        image.Save(Path.Combine(output, name));
    }
}
