using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy.Controls;
using nanoboy.Input;
using nanoboy.Storage;

namespace nanoboy;

public partial class frmNano
{
    private bool gamepadAwaitNeutral = true, quickMenuOpen, screenshotInProgress;
    private Label? performanceOverlay;
    private long performanceUpdateAt;

    private void InitializePlayerTools()
    {
        GamepadNavigation.Attach(this, () => session == null);
        performanceOverlay = new Label { Name = "gamePerformanceOverlay", AutoSize = false, TabStop = false,
            Text = "PERFORMANCE · noch kein Bild", Bounds = new(10, 10, 390, 84),
            BackColor = Color.FromArgb(14, 17, 30), ForeColor = AetherColors.Cyan,
            Font = new Font("Consolas", 9f), Padding = new Padding(8), Visible = settings.PerformanceOverlay };
        gameView.Controls.Add(performanceOverlay); performanceOverlay.BringToFront();
        gameView.Resize += (_, _) => performanceOverlay.Width = Math.Max(1, Math.Min((int)(390 * DeviceDpi / 96f), gameView.Width - 20));
    }
    private void TogglePerformanceOverlay()
    {
        settings.PerformanceOverlay = !settings.PerformanceOverlay;
        if (performanceOverlay != null) performanceOverlay.Visible = settings.PerformanceOverlay;
        performanceUpdateAt = 0;
    }
    private void UpdatePerformanceOverlay(EmulationSnapshot? snapshot)
    {
        if (performanceOverlay == null) return;
        performanceOverlay.Visible = settings.PerformanceOverlay;
        if (!settings.PerformanceOverlay || Environment.TickCount64 - performanceUpdateAt < 500) return;
        performanceUpdateAt = Environment.TickCount64;
        var metrics = gameView.FrameTimings.Read(Stopwatch.GetTimestamp() * 1000d / Stopwatch.Frequency);
        if (snapshot?.State != SessionState.Running) metrics = default;
        var audio = Volatile.Read(ref audioOutput)?.Snapshot;
        string state = snapshot?.State.ToString().ToUpperInvariant() ?? "KEIN SPIEL";
        performanceOverlay.Text = $"{state} · Ausgabe {metrics.FramesPerSecond:F1} FPS\r\n" +
            $"Bildabstand Ø {metrics.AverageMs:F1} · P95 {metrics.P95Ms:F1} ms\r\n" +
            (audio == null ? "Audio aus" : $"Audio {audio.Channels} ch · {audio.BufferedMs:F1}/{audio.TargetLatencyMs} ms · XRUN {audio.Underruns}") +
            $"\r\n{gameView.RendererStatus} · F9 aus/an";
    }
    private async Task CaptureScreenshotAsync()
    {
        if (screenshotInProgress || session == null || currentRomPath == null) return;
        string rom = currentRomPath;
        VideoGeometry geometry = session.LatestSnapshot.VideoGeometry;
        int[] pixels = new int[geometry.PixelCount]; long sequence = 0;
        if (!session.TryCopyLatestFrame(pixels, ref sequence)) { SetSaveFeedback("Noch kein Spielbild für einen Screenshot", true); return; }
        screenshotInProgress = true;
        try
        {
            string path = await Task.Run(() => new WindowsScreenshotStore(WindowsDataPaths.Default).Write(rom, geometry, pixels));
            if (!IsDisposed) SetSaveFeedback("Screenshot gespeichert · " + Path.GetFileName(path), false);
            testerSession?.RecordOperation("screenshot", null, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.ExternalException)
        {
            testerSession?.RecordException("screenshot.failed", ex);
            if (!IsDisposed) AetherSignal.Show(this, "Screenshot konnte nicht gespeichert werden.\n\n" + ex.Message,
                "Screenshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { screenshotInProgress = false; }
    }
    private async Task OpenQuickMenuAsync()
    {
        if (quickMenuOpen || stateOperationInProgress || IsDisposed) return;
        if (session == null) { OpenRomFromAetherUi(); return; }
        EmulationSession current = session;
        bool pauseAfter = current.LatestSnapshot.IsPaused;
        Action? next = null;
        quickMenuOpen = true; gamepadAwaitNeutral = true;
        ReleaseGamepadInput();
        try
        {
            await current.SetPausedAsync(true);
            if (IsDisposed || !ReferenceEquals(current, session)) return;
            using var menu = new frmQuickMenu(settings, pauseAfter, SelectSaveSlot,
                () => SaveCheckpointAsync(settings.SaveSlot), () => LoadCheckpointAsync(settings.SaveSlot),
                CaptureScreenshotAsync, TogglePerformanceOverlay, ToggleAetherFullscreen,
                OpenRomFromAetherUi, OpenControlCenter, OpenStateGallery, () => saveFeedback, MarkSessionProblem);
            menu.ShowDialog(this);
            pauseAfter = menu.PauseOnExit; next = menu.NextAction;
        }
        catch (InvalidOperationException ex)
        {
            testerSession?.RecordException("quick_menu.failed", ex);
            if (!IsDisposed) SetSaveFeedback("Quick Deck derzeit nicht verfügbar", true);
        }
        finally
        {
            if (!IsDisposed && ReferenceEquals(current, session) && current.State == SessionState.Paused)
            {
                try { await current.SetPausedAsync(pauseAfter); } catch (InvalidOperationException) { }
            }
            quickMenuOpen = false; gamepadAwaitNeutral = true;
        }
        if (!IsDisposed) { next?.Invoke(); if (next == null) gameView.Focus(); }
    }
}
