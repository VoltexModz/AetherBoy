using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using AetherBoy.Runtime.Netplay;
using nanoboy;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsConnectionTestTests
{
    [STATestMethod]
    public void MenuOpensOneStyledProbeWithoutRomBrowserOrEmulatorSession()
    {
        using var main = new frmNano();
        main.OnlineLinkBrowserLauncher = _ => Assert.Fail("No browser should open.");
        main.OnlineProbeSessionFactory = (_, _, _, _) => throw new AssertFailedException("Opening the dialog must not start the network.");
        main.Show();
        var tools = (ToolStripMenuItem)typeof(frmNano).GetField("menuItem21", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
        var entry = tools.DropDownItems.Find("menuOnlineProbe", true).Single();
        entry.PerformClick(); Application.DoEvents();
        var dialog = main.OwnedForms.OfType<frmOnlineConnectionTest>().Single();
        Assert.AreEqual(FormBorderStyle.None, dialog.FormBorderStyle);
        Assert.IsTrue(Find<TextBox>(dialog, "probeServerKey").UseSystemPasswordChar);
        Assert.IsTrue(Find<Button>(dialog, "probeCreate").Enabled);
        Assert.IsNull(typeof(frmNano).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main));
        entry.PerformClick(); Assert.AreEqual(1, main.OwnedForms.OfType<frmOnlineConnectionTest>().Count());
        dialog.Close(); main.Close();
    }

    [STATestMethod]
    public void GameRoomAndProbeUseOneSettingsEditorAndActiveProbePreventsGameLinkStart()
    {
        using var main = new frmNano();
        var fake = new Probe();
        main.OnlineProbeSessionFactory = (_, _, _, _) => fake;
        main.Show();
        var showRoom = typeof(frmNano).GetMethod("ShowOnlineRoomDialog", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var showProbe = typeof(frmNano).GetMethod("ShowOnlineConnectionTest", BindingFlags.Instance | BindingFlags.NonPublic)!;
        showRoom.Invoke(main, new object[] { true });
        var gameRoom = main.OwnedForms.Single();
        showProbe.Invoke(main, null); Application.DoEvents(); Assert.IsTrue(gameRoom.IsDisposed);
        var probe = main.OwnedForms.OfType<frmOnlineConnectionTest>().Single();
        showRoom.Invoke(main, new object[] { false }); Application.DoEvents(); Assert.IsTrue(probe.IsDisposed);
        showProbe.Invoke(main, null); Application.DoEvents();
        probe = main.OwnedForms.OfType<frmOnlineConnectionTest>().Single();
        Find<TextBox>(probe, "probeServerUrl").Text = "https://rooms.example.com";
        Find<TextBox>(probe, "probeServerKey").Text = new string('b', 64);
        Click(probe, "probeCreate"); Assert.IsTrue(probe.IsTestActive);
        Assert.IsFalse(main.StartOnlineLink(false, confirm: false));
        showRoom.Invoke(main, new object[] { true }); Application.DoEvents();
        Assert.AreSame(probe, main.OwnedForms.Single());
        main.Dispose(); Assert.IsTrue(fake.Disposed);
    }

    [STATestMethod]
    public void ValidatedSettingsProgressResultsAndRestartStayIndependentOfGames()
    {
        using var scope = new Scope();
        var fake = new Probe(); int starts = 0; string? requestedCode = null;
        using var dialog = new frmOnlineConnectionTest(scope.SettingsPath, scope.Reports, false, () => true,
            (_, _, code, reports) => { starts++; requestedCode = code; Assert.IsNull(reports); return fake; });
        dialog.Show();
        Click(dialog, "probeCreate"); Assert.AreEqual(0, starts);
        Find<TextBox>(dialog, "probeServerUrl").Text = "https://rooms.example.com";
        Find<TextBox>(dialog, "probeServerKey").Text = new string('b', 64);
        Click(dialog, "probeSaveServer");
        Assert.AreEqual(new OnlineRoomSettings("https://rooms.example.com", new string('b', 64)), OnlineRoomSettings.Load(scope.SettingsPath));
        Find<TextBox>(dialog, "probeRoomCode").Text = "bad";
        Click(dialog, "probeJoin"); Assert.AreEqual(0, starts);
        Find<TextBox>(dialog, "probeRoomCode").Text = "abcde-fghjk";
        Click(dialog, "probeJoin"); Assert.AreEqual(1, starts); Assert.AreEqual("ABCDEFGHJK", requestedCode);
        Assert.IsTrue(Find<TextBox>(dialog, "probeServerKey").ReadOnly);
        Assert.IsFalse(Find<Button>(dialog, "probeCreate").Enabled);
        Assert.IsTrue(Find<Button>(dialog, "probeStop").Enabled);
        string? copied = null; dialog.CopyText = value => copied = value;
        Click(dialog, "probeCopyCode"); Assert.AreEqual("ABCDE-FGHJK", copied);
        fake.State = fake.State with { Phase = OnlineProbePhase.Testing, Progress = new(4, 5, 32) };
        dialog.RefreshStatus(); Assert.IsTrue(Find<Label>(dialog, "probeStep3").Text.Contains("4 / 32"));
        Capture(dialog, scope, "connection-test-progress.png");
        fake.Pass(); dialog.RefreshStatus();
        Assert.IsTrue(Find<Label>(dialog, "probeStatus").Text.Contains("BEIDE"));
        Assert.IsFalse(Find<Button>(dialog, "probeCreate").Enabled, "Local PASS must retain the connection for the peer.");
        Assert.IsTrue(Find<TextBox>(dialog, "probeResults").Text.Contains("4096"));
        Capture(dialog, scope, "connection-test-passed.png");
        Click(dialog, "probeCopyReport"); Assert.AreEqual(fake.GetDiagnosticReport(), copied);
        Assert.IsFalse(copied!.Contains(new string('b', 64)));
        Click(dialog, "probeStop"); Until(() => Find<Button>(dialog, "probeJoin").Enabled);
        Assert.AreEqual(1, fake.Stops);
        Find<TextBox>(dialog, "probeRoomCode").Text = "KLMNP-QRSTU";
        dialog.RefreshStatus(); Assert.AreEqual("KLMNP-QRSTU", Find<TextBox>(dialog, "probeRoomCode").Text);
        fake = new Probe(); Click(dialog, "probeJoin"); Assert.AreEqual(2, starts); Assert.AreEqual("KLMNPQRSTU", requestedCode);
        dialog.Close(); Until(() => dialog.IsDisposed);
    }

    [STATestMethod]
    public void PendingCancellationKeepsUiResponsiveAndBlocksRestartUntilCleanupFinishes()
    {
        using var scope = new Scope(configured: true);
        var fake = new Probe { HoldStop = true };
        using var dialog = new frmOnlineConnectionTest(scope.SettingsPath, scope.Reports, false, () => true, (_, _, _, _) => fake);
        dialog.Show(); Click(dialog, "probeCreate");
        Click(dialog, "probeStop"); Application.DoEvents();
        Assert.AreEqual(1, fake.Stops);
        Assert.IsFalse(Find<Button>(dialog, "probeCreate").Enabled);
        Assert.IsFalse(dialog.IsDisposed);
        dialog.Close(); Application.DoEvents(); Assert.IsFalse(dialog.IsDisposed);
        fake.FinishStop(); Until(() => dialog.IsDisposed);
        Assert.AreEqual(1, fake.Stops);
    }

    [STATestMethod]
    public void DisposalStopsBackgroundProbeAndGameGuardBlocksStartingAnotherSession()
    {
        using var scope = new Scope(configured: true);
        bool allowed = false; var fake = new Probe(); int starts = 0;
        var dialog = new frmOnlineConnectionTest(scope.SettingsPath, scope.Reports, false, () => allowed,
            (_, _, _, _) => { starts++; return fake; });
        dialog.Show(); Assert.IsFalse(Find<Button>(dialog, "probeCreate").Enabled);
        allowed = true; dialog.RefreshStatus(); Click(dialog, "probeCreate"); Assert.AreEqual(1, starts);
        dialog.Dispose(); Assert.IsTrue(fake.Disposed);
    }

    [STATestMethod]
    public void ExplicitRecordingChoiceAndReportFailureNeverExposeCredentials()
    {
        using var scope = new Scope(configured: true);
        string? requestedReports = null; var fake = new Probe();
        using var dialog = new frmOnlineConnectionTest(scope.SettingsPath, scope.Reports, false, () => true,
            (_, _, _, reports) => { requestedReports = reports; return fake; });
        dialog.Show(); Assert.IsFalse(Find<CheckBox>(dialog, "probeRecording").Checked);
        Find<CheckBox>(dialog, "probeRecording").Checked = true;
        Click(dialog, "probeCreate"); Assert.AreEqual(scope.Reports, requestedReports);
        fake.State = fake.State with { ReportWriteFailed = true };
        dialog.RefreshStatus();
        Assert.IsTrue(Find<TextBox>(dialog, "probeResults").Text.Contains("nicht gespeichert"));
        Assert.IsTrue(Find<Button>(dialog, "probeCopyReport").Enabled);
        dialog.Close(); Until(() => dialog.IsDisposed);
    }

    [STATestMethod]
    [DataRow(OnlineProbeFailure.AccessKey, "Zugangsschlüssel abgewiesen")]
    [DataRow(OnlineProbeFailure.RoomMissing, "Raum nicht gefunden")]
    [DataRow(OnlineProbeFailure.RoomMismatch, "anderes Profil")]
    [DataRow(OnlineProbeFailure.ServerProfile, "transport-probe-v1")]
    [DataRow(OnlineProbeFailure.Timeout, "Zeitlimit")]
    [DataRow(OnlineProbeFailure.Integrity, "Datenprüfung fehlgeschlagen")]
    public void FailureIsActionableAndNeverLooksLikeSuccessfulTrade(OnlineProbeFailure failure, string expected)
    {
        using var scope = new Scope(configured: true);
        var fake = new Probe();
        using var dialog = new frmOnlineConnectionTest(scope.SettingsPath, scope.Reports, false, () => true, (_, _, _, _) => fake);
        dialog.Show(); Click(dialog, "probeCreate");
        fake.State = fake.State with { Active = false, Phase = OnlineProbePhase.Failed, Failure = failure,
            Connection = failure == OnlineProbeFailure.Integrity ? new("ABCDE-FGHJK", true, true, false, "connected")
                : new("", false, false, false, "room-admission") };
        dialog.RefreshStatus();
        Assert.IsTrue(Find<Label>(dialog, "probeStatus").Text.Contains(expected));
        Assert.IsTrue(Find<Button>(dialog, "probeCreate").Enabled);
        Assert.AreNotEqual("Bestätigt", Find<Label>(dialog, "probeStep3").Text);
        Capture(dialog, scope, "connection-test-failed-" + failure + ".png");
        dialog.Close();
    }

    private static T Find<T>(Control parent, string name) where T : Control => (T)parent.Controls.Find(name, true).Single();
    private static void Click(Control parent, string name) { Find<Button>(parent, name).PerformClick(); Application.DoEvents(); }
    private static void Until(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition()) { Assert.IsTrue(clock.Elapsed < TimeSpan.FromSeconds(5)); Application.DoEvents(); Thread.Sleep(5); }
    }
    private static void Capture(Form dialog, Scope scope, string name)
    {
        string? path = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(path);
        using var image = new Bitmap(dialog.ClientSize.Width, dialog.ClientSize.Height);
        dialog.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(path, name));
    }
    private sealed class Scope : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "aetherboy-probe-ui-" + Guid.NewGuid().ToString("N"));
        public string SettingsPath => Path.Combine(directory, "online-room.json");
        public string Reports => Path.Combine(directory, "reports");
        public Scope(bool configured = false)
        {
            Directory.CreateDirectory(directory);
            if (configured) new OnlineRoomSettings("https://rooms.example.com", new string('b', 64)).Save(SettingsPath);
        }
        public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    private sealed class Probe : IOnlineProbeSession
    {
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly OnlineRoomDiagnostics diagnostics = new();
        public OnlineProbeSnapshot State = new(OnlineProbePhase.Connecting, true, false,
            new("ABCDE-FGHJK", true, false, false, "remote-answer"), new(0, 0, 32), null, OnlineProbeFailure.None, null, false);
        public int Stops;
        public bool HoldStop, Disposed;
        public OnlineProbeSnapshot Snapshot => State;
        public Task Completion => completion.Task;
        public string GetDiagnosticReport() => diagnostics.ToJson();
        public void Pass() => State = State with { Phase = OnlineProbePhase.Passed, Connection = new("ABCDE-FGHJK", true, true, true, "connected"),
            Progress = new(32, 32, 32), Result = new(32, 32, new[] { 32, 256, 1024, 4096 }.Select(size => new OnlineTransportProbeSize(size, 8, 12, 18, 25, 29)).ToArray()) };
        public void FinishStop()
        {
            State = State with { Active = false, Stopping = false, Phase = State.Phase == OnlineProbePhase.Passed ? OnlineProbePhase.Passed : OnlineProbePhase.Cancelled };
            completion.TrySetResult();
        }
        public Task StopAsync() { Stops++; State = State with { Stopping = true }; if (!HoldStop) FinishStop(); return completion.Task; }
        public void Dispose() { Disposed = true; FinishStop(); }
        public async ValueTask DisposeAsync() => await StopAsync();
    }
}
