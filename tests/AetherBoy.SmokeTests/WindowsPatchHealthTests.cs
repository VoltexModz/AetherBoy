using System.Buffers.Binary;
using System.Diagnostics;
using System.Drawing;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Diagnostics;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsPatchHealthTests
{
    private string root = null!;
    [TestInitialize] public void Setup() { root = Path.Combine(Path.GetTempPath(), "aetherboy-patch-health-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    private string Rom(bool gba = false)
    {
        byte[] bytes = new byte[32768];
        if (gba) BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0xEAFFFFFE);
        else new byte[] { 0x04, 0x18, 0xFD }.CopyTo(bytes, 0x100);
        string path = Path.Combine(root, "generated" + (gba ? ".gba" : ".gb"));
        File.WriteAllBytes(path, bytes); return path;
    }
    private string Patch()
    {
        string path = Path.Combine(root, "My Hack.ips");
        File.WriteAllBytes(path, [.. "PATCH"u8.ToArray(), 0, 2, 0, 0, 1, 99, .. "EOF"u8.ToArray()]); return path;
    }
    [TestMethod]
    public void PatchImportPreservesOriginalSeparatesSavesAndRecordsOnlyHashes()
    {
        var paths = new WindowsDataPaths(Path.Combine(root, "data")); var library = new WindowsRomLibrary(paths);
        string source = Rom(), patch = Patch();
        File.WriteAllBytes(Path.ChangeExtension(source, "sav"), [1, 2, 3]);
        string original = library.Import(source); byte[] before = File.ReadAllBytes(source);
        var service = new WindowsRomPatchService(paths);
        PatchedRomImport result = service.ApplyAndImport(original, patch, "My Hack");
        CollectionAssert.AreEqual(before, File.ReadAllBytes(source)); CollectionAssert.AreEqual(before, File.ReadAllBytes(original));
        Assert.AreEqual((byte)99, File.ReadAllBytes(result.Path)[0x200]);
        Assert.AreNotEqual(library.GetSavePath(original), library.GetSavePath(result.Path));
        Assert.IsFalse(File.Exists(library.GetSavePath(result.Path)));
        Assert.AreEqual("My Hack", new WindowsGameLibraryStore(paths).Read(result.Path).Title);
        Assert.IsTrue(new WindowsGameLibraryStore(paths).Read(result.Path).HasCustomTitle);
        Assert.AreEqual(result.Path, service.ApplyAndImport(source, patch, "My Hack").Path);
        Assert.AreEqual(2, library.GetRoms().Count);
        string manifest = File.ReadAllText(Directory.GetFiles(Path.GetDirectoryName(result.Path)!, "patch-*.json").Single());
        Assert.IsFalse(manifest.Contains(root)); Assert.IsFalse(manifest.Contains(Path.GetFileName(source)));
        StringAssert.Contains(manifest, result.SourceSha256); StringAssert.Contains(manifest, result.TargetSha256);
        Assert.AreEqual(0, Directory.GetDirectories(paths.Roms, ".patch-*").Length);
    }
    [TestMethod]
    public void InvalidPatchNeverCreatesLibraryRom()
    {
        var paths = new WindowsDataPaths(Path.Combine(root, "data")); string source = Rom(), patch = Patch();
        File.WriteAllBytes(patch, "BPS1broken"u8.ToArray());
        Assert.Throws<InvalidDataException>(() => new WindowsRomPatchService(paths).ApplyAndImport(source, patch, "Broken"));
        Assert.AreEqual(0, new WindowsRomLibrary(paths).GetRoms().Count);
    }
    [TestMethod]
    public void HealthHintsAreDeduplicatedAndDistinguishAudioFromVideoProgress()
    {
        var analyzer = new SessionHealthAnalyzer(); var hints = new List<SessionHealthHint>();
        for (int n = 0; n < 40; n++) hints.AddRange(analyzer.Observe(Sample(n, frames: 0, video: 0, presented: 0), n * 1000));
        Assert.AreEqual(1, hints.Count(h => h.Code == "emulation.stalled_suspected"));
        Assert.IsTrue(hints.Single(h => h.Code == "emulation.stalled_suspected").AudioAdvancing);
        Assert.AreEqual(1, hints.Count(h => h.Code == "video.uniform_suspected"));
        analyzer = new(); hints.Clear();
        for (int n = 0; n < 15; n++) hints.AddRange(analyzer.Observe(Sample(n, video: 0, presented: 0) with { UniformRgb = null }, n * 1000));
        Assert.IsTrue(hints.Any(h => h.Code == "video.no_frames_suspected"));
        Assert.IsFalse(hints.Any(h => h.Code == "emulation.stalled_suspected"));
    }
    [TestMethod]
    public void HealthIgnoresPausedMinimizedAndSleepGapsButDetectsUiAndStartupStalls()
    {
        foreach (var mode in new[] { SessionState.Paused, SessionState.Stopped, SessionState.Faulted })
        {
            var analyzer = new SessionHealthAnalyzer();
            for (int n = 0; n < 50; n++) Assert.AreEqual(0, analyzer.Observe(Sample(n, 0, 0, 0) with { State = mode }, n * 1000).Count);
        }
        var suppressed = new SessionHealthAnalyzer();
        for (int n = 0; n < 40; n++) Assert.AreEqual(0, suppressed.Observe(Sample(n, 0, 0, 0) with { Suppressed = true }, n * 1000).Count);
        Assert.AreEqual(0, suppressed.Observe(Sample(0, 0, 0, 0), 90_000).Count);
        var startup = new SessionHealthAnalyzer(); var ui = new SessionHealthAnalyzer();
        var startHints = new List<SessionHealthHint>(); var uiHints = new List<SessionHealthHint>();
        for (int n = 0; n < 35; n++)
        {
            startHints.AddRange(startup.Observe(Sample(n) with { State = SessionState.Starting }, n * 1000));
            uiHints.AddRange(ui.Observe(Sample(n) with { UniformRgb = null, UiAgeMs = n * 1000 }, n * 1000));
        }
        Assert.AreEqual(1, startHints.Count); Assert.AreEqual("startup.slow_suspected", startHints[0].Code);
        Assert.AreEqual(1, uiHints.Count); Assert.AreEqual("ui.unresponsive_suspected", uiHints[0].Code);
    }
    [TestMethod]
    public void StaticNonUniformMenusDoNotCountAsWhiteScreensAndRecoveryRearmsDetection()
    {
        Assert.IsNull(SessionHealthAnalyzer.UniformColor([0, 1, 0]));
        Assert.AreEqual(0xFFFFFF, SessionHealthAnalyzer.UniformColor([unchecked((int)0xFFFFFFFF), 0xFFFFFF]));
        var analyzer = new SessionHealthAnalyzer();
        for (int n = 0; n < 40; n++) Assert.AreEqual(0, analyzer.Observe(Sample(n) with { UniformRgb = null }, n * 1000).Count);
        var hints = new List<SessionHealthHint>();
        for (int n = 40; n < 65; n++) hints.AddRange(analyzer.Observe(Sample(n), n * 1000));
        Assert.AreEqual(1, hints.Count(h => h.Code == "video.uniform_suspected"));
        analyzer.Observe(Sample(65) with { UniformRgb = null }, 65_000); hints.Clear();
        for (int n = 66; n < 90; n++) hints.AddRange(analyzer.Observe(Sample(n), n * 1000));
        Assert.AreEqual(1, hints.Count(h => h.Code == "video.uniform_suspected"));
    }
    [TestMethod]
    public void HealthReportAndManualMarkerContainNoFramePixelsOrExtraFiles()
    {
        using var report = new WindowsTesterSession(Path.Combine(root, "reports"));
        report.RecordHealthHint(new("video.uniform_suspected", 20_000, true), Sample(20));
        using (var monitor = new WindowsSessionHealthMonitor(report))
        { Assert.IsTrue(monitor.MarkProblem()); Assert.IsFalse(monitor.MarkProblem()); }
        using var reader = new StreamReader(new FileStream(report.LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        string log = reader.ReadToEnd();
        StringAssert.Contains(log, "session.health_hint"); StringAssert.Contains(log, "session.problem_marked");
        Assert.IsFalse(log.Contains("uniform_rgb")); Assert.IsFalse(log.Contains(root));
        using var bundle = ZipFile.OpenRead(report.CreateBundle(Path.Combine(root, "report.zip")));
        CollectionAssert.AreEquivalent(new[] { "README.txt", "session.jsonl" }, bundle.Entries.Select(e => e.Name).ToArray());
    }

    [STATestMethod]
    public void PatchLabImportsFromUiWithoutLaunchingAGame()
    {
        using var form = new frmRomPatcher(Rom()); form.Show(); Application.DoEvents();
        ((TextBox)form.Controls.Find("patchFile", true).Single()).Text = Patch();
        ((TextBox)form.Controls.Find("patchTitle", true).Single()).Text = "UI PATCH TEST";
        ((AetherButton)form.Controls.Find("patchApply", true).Single()).PerformClick();
        PumpUntil(() => form.ImportedRomPath != null && form.Controls.Find("patchDone", true).Single().Enabled);
        Assert.IsTrue(File.Exists(form.ImportedRomPath));
        Assert.AreEqual("UI PATCH TEST", WindowsGameLibraryStore.Default.Read(form.ImportedRomPath!).Title);
        StringAssert.Contains(form.Controls.Find("patchStatus", true).Single().Text, "importiert");
        Capture(form, "patch-lab.png"); form.Close();
    }
    [STATestMethod]
    public void GbaInspectorShowsDirectSoundAndRecordsStereoAtNativeRate()
    {
        using var session = new EmulationSession(Rom(true), Path.Combine(root, "audio.sav"), null,
            new EmulatorConfiguration(0, true, true, true, true, true, 44100));
        using var inspector = new frmAudioTool { Session = session }; inspector.Show();
        PumpUntil(() => session.LatestSnapshot.EmulatedFrameCount >= 3);
        Call(inspector, "timer1_Tick", inspector, EventArgs.Empty);
        string channels = inspector.Controls.Find("audioDirectSoundStatus", true).Single().Text;
        StringAssert.Contains(channels, "FIFO A"); StringAssert.Contains(channels, "FIFO B");
        string path = Path.Combine(root, "gba.wav");
        Pump((Task)Call(inspector, "StartRecordingAsync", path, 65536, session)!);
        long frame = session.LatestSnapshot.EmulatedFrameCount;
        PumpUntil(() => session.LatestSnapshot.EmulatedFrameCount >= frame + 10);
        Pump((Task)Call(inspector, "BeginStopRecordingAsync", false)!);
        byte[] wav = File.ReadAllBytes(path);
        Assert.IsTrue(wav.Length > 44);
        Assert.AreEqual(2, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(22)));
        Assert.AreEqual(65536, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(24)));
        Capture(inspector, "gba-audio-inspector.png");
        inspector.Session = null; inspector.Close();
    }
    private static SessionHealthSample Sample(int n, long? frames = null, long? video = null, long? presented = null) =>
        new(SessionState.Running, frames ?? n * 60, video ?? n * 60, presented ?? n * 60, n * 44100, 0, 0xFFFFFF, true, false);
    private static object? Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
    private static void Pump(Task task) { PumpUntil(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static void PumpUntil(Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        while (!done()) { Application.DoEvents(); Thread.Sleep(8); if (watch.Elapsed.TotalSeconds > 10) Assert.Fail("UI task timed out."); }
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
