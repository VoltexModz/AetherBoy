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
        var sofa = new nanoboy.Controls.AetherCommand(global::AetherBoy.Runtime.Localization.UiText.Get("Sofa-Modus öffnen"), null, (_, _) => _ = OpenSofaLibraryAsync()) { Name = "menuSofaMode" };
        menuFile.DropDownItems.Add(sofa);
        sofaGameMenu = new AetherButton { Text = global::AetherBoy.Runtime.Localization.UiText.Get("Spielmenü"), Name = "sofaGameMenu", Size = new Size(158, 42),
            Anchor = AnchorStyles.Top | AnchorStyles.Right, Visible = false };
        gameView.Controls.Add(sofaGameMenu);
        void PositionSofaMenu() => sofaGameMenu.Location = new Point(Math.Max(0, gameView.Width - sofaGameMenu.Width - 16), 16);
        gameView.Resize += (_, _) => PositionSofaMenu(); PositionSofaMenu();
        sofaGameMenu.Click += (_, _) => _ = OpenQuickMenuAsync();
        GamepadNavigation.Attach(this, () => session == null && aetherCommandMenu is null);
        performanceOverlay = new Label { Name = "gamePerformanceOverlay", AutoSize = false, TabStop = false,
            Text = global::AetherBoy.Runtime.Localization.UiText.Get("PERFORMANCE · noch kein Bild"), Bounds = new(10, 10, 390, 84),
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
        string state = snapshot is null ? global::AetherBoy.Runtime.Localization.UiText.Get("KEIN SPIEL") : global::AetherBoy.Runtime.Localization.UiLabels.Session(snapshot.State);
        performanceOverlay.Text = global::AetherBoy.Runtime.Localization.UiText.Format("{0} · Ausgabe {1:F1} FPS\r\n", state, metrics.FramesPerSecond) +
            global::AetherBoy.Runtime.Localization.UiText.Format("Bildabstand Ø {0:F1} · P95 {1:F1} ms\r\n", metrics.AverageMs, metrics.P95Ms) +
            (audio == null ? global::AetherBoy.Runtime.Localization.UiText.Get("Audio aus") : global::AetherBoy.Runtime.Localization.UiText.Format("Audio {0} ch · {1:F1}/{2} ms · XRUN {3}", audio.Channels, audio.BufferedMs, audio.TargetLatencyMs, audio.Underruns)) +
            global::AetherBoy.Runtime.Localization.UiText.Format("\r\n{0} · F9 aus/an", gameView.RendererStatus);
    }
    private async Task CaptureScreenshotAsync()
    {
        if (screenshotInProgress || session == null || currentRomPath == null) return;
        string rom = currentRomPath;
        VideoGeometry geometry = session.LatestSnapshot.VideoGeometry;
        int[] pixels = new int[geometry.PixelCount]; long sequence = 0;
        if (!session.TryCopyLatestFrame(pixels, ref sequence)) { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Noch kein Spielbild für einen Screenshot"), true); return; }
        screenshotInProgress = true;
        try
        {
            string path = await Task.Run(() => new WindowsScreenshotStore(WindowsDataPaths.Default).Write(rom, geometry, pixels));
            if (!IsDisposed) SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Screenshot gespeichert · ") + Path.GetFileName(path), false);
            testerSession?.RecordOperation("screenshot", null, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.ExternalException)
        {
            testerSession?.RecordException("screenshot.failed", ex);
            if (!IsDisposed) AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.Get("Screenshot konnte nicht gespeichert werden.\n\n") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message),
                "Screenshot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { screenshotInProgress = false; }
    }
    private async Task OpenQuickMenuAsync()
    {
        if (IsOnlineLink) { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Online-Link: TOOLS öffnet Verbindung und Sitzungsspielstände · F12 Screenshot"), false); return; }
        if (quickMenuOpen || stateOperationInProgress || IsDisposed) return;
        if (session == null) { OpenRomFromAetherUi(); return; }
        EmulationSession current = session;
        bool pauseAfter = current.LatestSnapshot.IsPaused;
        Action? next = null;
        quickMenuOpen = true; gamepadAwaitNeutral = true;
        ReleaseGamepadInput();
        try
        {
            if (sofaMode) await current.SetTurboAsync(false);
            await current.SetPausedAsync(true);
            if (IsDisposed || !ReferenceEquals(current, session)) return;
            if (sofaMode)
            {
                using var sofaMenu = new frmSofaQuickMenu(settings, SelectSaveSlot,
                    () => SaveCheckpointAsync(settings.SaveSlot), () => LoadCheckpointAsync(settings.SaveSlot),
                    CaptureScreenshotAsync, () => _ = OpenSofaLibraryAsync(), ExitSofaMode, () => saveFeedback);
                sofaMenu.ShowDialog(this); next = sofaMenu.NextAction;
                if (sofaMenu.ResumeRequested) pauseAfter = false;
            }
            else
            {
            using var menu = new frmQuickMenu(settings, pauseAfter, SelectSaveSlot,
                () => SaveCheckpointAsync(settings.SaveSlot), () => LoadCheckpointAsync(settings.SaveSlot),
                CaptureScreenshotAsync, TogglePerformanceOverlay, ToggleAetherFullscreen,
                OpenRomFromAetherUi, OpenControlCenter, OpenStateGallery, () => saveFeedback, MarkSessionProblem);
            menu.ShowDialog(this);
            pauseAfter = menu.PauseOnExit; next = menu.NextAction;
            }
        }
        catch (InvalidOperationException ex)
        {
            testerSession?.RecordException("quick_menu.failed", ex);
            if (!IsDisposed) SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Quick Deck derzeit nicht verfügbar"), true);
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
