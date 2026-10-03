using System.Windows.Forms;
using nanoboy;
using nanoboy.Controls;
using AetherBoy.Runtime.Localization;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Storage;
using nanoboy.Core;
using System.Reflection;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsSaveNoticeTests
{
    [STATestMethod]
    public void DirectDisposeStopsOwnerAndReleasesItsWriteLease()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-dispose-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            byte[] rom = new byte[32768]; rom[0x100] = 0x18; rom[0x101] = 0xFE;
            string path = Path.Combine(root, "synthetic.gb"), locked = Path.Combine(root, "game.sav.lock");
            File.WriteAllBytes(path, rom);
            using var owner = new EmulationSession(path, Path.Combine(root, "game.sav"), null,
                new EmulatorConfiguration(0, false, true, true, true, true, 44100));
            owner.SetPausedAsync(true).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            using var main = new frmNano(); main.Show();
            typeof(frmNano).GetField("session", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(main, owner);
            typeof(frmNano).GetField("romWriteLease", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(main, RomWriteLease.Acquire(locked));
            main.Dispose(); // Intentionally no Close()/FormClosing.
            owner.Completion.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Assert.AreEqual(SessionState.Stopped, owner.State);
            Assert.IsTrue(SpinWait.SpinUntil(() =>
            {
                try { using var nextOwner = RomWriteLease.Acquire(locked); return true; }
                catch (IOException) { return false; }
            }, TimeSpan.FromSeconds(5)), "The completed owner must no longer retain its lease.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [STATestMethod]
    public void NoticeKeepsSessionMetricsInsideScrollableRail()
    {
        using var main = new frmNano(); main.Show();
        typeof(frmNano).GetMethod("ShowSettingsSaveFailure", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(main, [new IOException("Synthetic failure")]);
        Application.DoEvents();
        var rail = main.Controls.Find("aetherSessionRail", true).Single();
        var layout = rail.Controls.OfType<TableLayoutPanel>().Single();
        foreach (Control row in layout.Controls)
            Assert.IsTrue(row.Right <= layout.ClientSize.Width, "Metric row exceeds the scrollable rail width.");
        var viewport = (AetherScrollViewport)main.Controls.Find("aetherSessionRailViewport", true).Single();
        viewport.ScrollTo(0, int.MaxValue);
        Assert.IsGreaterThan(0, viewport.Offset.Y);
    }

    [STATestMethod]
    public void BackgroundFailureDoesNotOpenModalAndRetryDoesNotBlockUi()
    {
        using var window = new AetherWindow();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int requests = 0;
        using var notice = new AetherSaveNotice(() => { requests++; return completion.Task; }, _ => Assert.Fail("Unexpected retry failure"));
        window.Controls.Add(notice); window.Show();
        int forms = Application.OpenForms.Count;
        notice.SetFailure(new IOException("Synthetic failure"));
        notice.SetFailure(new IOException("Repeated failure"));
        Assert.IsTrue(notice.Visible);
        Assert.AreEqual(forms, Application.OpenForms.Count);
        Task retry = notice.RetryAsync();
        Assert.IsFalse(retry.IsCompleted);
        Assert.IsTrue(notice.IsSaving);
        notice.RetryAsync().GetAwaiter().GetResult();
        Assert.AreEqual(1, requests);
        bool dispatched = false;
        window.BeginInvoke(() => dispatched = true);
        Application.DoEvents();
        Assert.IsTrue(dispatched, "The UI queue must keep processing while disk I/O is pending.");
        completion.SetResult();
        PumpUntil(retry);
        Assert.IsFalse(notice.Visible);
        Assert.IsFalse(notice.IsSaving);
    }

    [STATestMethod]
    public void FailedRetryRemainsVisibleAndReportsCauseWithoutOpeningModal()
    {
        using var window = new AetherWindow();
        Exception? recorded = null;
        var failure = new IOException("Synthetic failure");
        using var notice = new AetherSaveNotice(() => Task.FromException(failure), error => recorded = error);
        window.Controls.Add(notice); window.Show();
        int forms = Application.OpenForms.Count;
        notice.SetFailure(failure);
        PumpUntil(notice.RetryAsync());
        Assert.AreSame(failure, recorded);
        Assert.IsTrue(notice.Visible);
        Assert.AreEqual(forms, Application.OpenForms.Count);
        Assert.IsTrue(notice.Controls.OfType<Panel>().Single().Controls.OfType<AetherButton>().All(button => button.Enabled));
    }

    [STATestMethod]
    [DataRow("de")]
    [DataRow("en")]
    public void ControllerKeyboardHelpHasRoomForTwoLines(string language)
    {
        string previous = UiText.Language;
        try
        {
            UiText.Initialize(language);
            using var keyboard = new frmControllerKeyboard("");
            var description = keyboard.Controls.Find("aetherDialogDescription", true).Single();
            var needed = TextRenderer.MeasureText(description.Text, description.Font,
                new System.Drawing.Size(description.Width, int.MaxValue), TextFormatFlags.WordBreak);
            Assert.IsLessThanOrEqualTo(description.Height, needed.Height);
        }
        finally { UiText.Initialize(previous); }
    }

    private static void PumpUntil(Task task)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && clock.Elapsed < TimeSpan.FromSeconds(5)) { Application.DoEvents(); Thread.Sleep(1); }
        Assert.IsTrue(task.IsCompleted);
        task.GetAwaiter().GetResult();
    }
}
