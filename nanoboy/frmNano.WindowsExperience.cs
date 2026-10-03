using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using nanoboy.Controls;

namespace nanoboy;

public partial class frmNano
{
    private float aetherUiScale = 1f;
    private AetherBoy.Runtime.EmulationSession? focusPausedSession;
    private bool immersiveFullscreen;
    private Rectangle windowedBounds;
    private FormWindowState windowedState;
    private Size windowedMinimum;
    private Label? saveFeedbackLabel;
    private string saveFeedback = global::AetherBoy.Runtime.Localization.UiText.Get("F5 speichern · F8 laden · F11 Vollbild · Esc zurück");

    private void InitializeWindowsExperience()
    {
        saveFeedbackLabel = new Label
        {
            Name = "aetherSaveFeedback", Text = saveFeedback, Dock = DockStyle.Bottom,
            Height = 26, Padding = new Padding(16, 0, 4, 0), AutoEllipsis = true,
            BackColor = AetherColors.Chrome, ForeColor = AetherColors.Muted,
            TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8.5f)
        };
        Controls.Add(saveFeedbackLabel);
        saveFeedbackLabel.SendToBack(); // Dock the footer before the fill-sized shell/viewport.
        DpiChanged += (_, _) =>
        {
            if (immersiveFullscreen) Bounds = Screen.FromHandle(Handle).Bounds;
            else FitWindowToScreen();
        };
        Shown += (_, _) => FitWindowToScreen();
        Activated += (_, _) => ResumeAfterFocus();
        ApplyWindowsVideoSettings();
    }

    private void PauseForFocusLoss()
    {
        if (!settings.PauseOnFocusLoss || IsOnlineLink || stateOperationInProgress || quickMenuOpen ||
            session?.State != AetherBoy.Runtime.SessionState.Running) return;
        focusPausedSession = session;
        ObserveSessionCommand(session.SetPausedAsync(true));
    }

    private void ResumeAfterFocus()
    {
        if (bootIntro is not null) return;
        var paused = focusPausedSession;
        focusPausedSession = null;
        if (paused is null || !ReferenceEquals(paused, session) || IsOnlineLink ||
            paused.State is not (AetherBoy.Runtime.SessionState.Running or AetherBoy.Runtime.SessionState.Paused)) return;
        // Commands are ordered: this also handles focus returning before the pause was processed.
        ObserveSessionCommand(paused.SetPausedAsync(false));
    }

    private void ApplyWindowsVideoSettings()
    {
        gameView.GpuEnabled = settings.GpuRendering;
        gameView.VSyncEnabled = settings.VideoVSync;
        gameView.IntegerScaling = settings.IntegerScaling;
    }

    private string DescribeWindowsAudio()
    {
        var audio = Volatile.Read(ref audioOutput)?.Snapshot;
        return audio is null ? global::AetherBoy.Runtime.Localization.UiText.Get("AUDIO OFF / NO SESSION") :
            $"{audio.Backend} · {audio.DeviceName}\r\n" +
            global::AetherBoy.Runtime.Localization.UiText.Format("{0:N0} Hz · {1} ch · Ziel {2} ms · Queue {3:F1} ms\r\n", audio.SampleRate, audio.Channels, audio.TargetLatencyMs, audio.BufferedMs) +
            global::AetherBoy.Runtime.Localization.UiText.Format("Unterläufe {0} · Verworfen {1} · Ausgabe-Neustarts {2}", audio.Underruns, audio.DroppedSamples, audio.Reconnects) +
            (audio.ErrorCode is null ? "" : $" · {audio.ErrorCode}");
    }

    private string DescribeWindowsVideo() =>
        global::AetherBoy.Runtime.Localization.UiText.Format("{0} · VSync {1} · {2} DPI\r\n", gameView.RendererStatus,
            global::AetherBoy.Runtime.Localization.UiText.Get(settings.VideoVSync ? "angefordert" : "AUS"), DeviceDpi) +
        global::AetherBoy.Runtime.Localization.UiText.Format("Präsentiert {0:N0} · vor Anzeige ersetzt {1:N0}", gameView.PresentedFrames, gameView.SupersededFrames) +
        (gameView.RendererError is null ? "" : $" · {gameView.RendererError}");

    private void SetSaveFeedback(string text, bool failed)
    {
        saveFeedback = text;
        if (saveFeedbackLabel == null || IsDisposed) return;
        saveFeedbackLabel.Text = text;
        saveFeedbackLabel.ForeColor = failed ? AetherColors.Danger : AetherColors.Success;
        saveFeedbackLabel.AccessibleName = text;
    }

    private void SetImmersiveFullscreen(bool enabled)
    {
        if (immersiveFullscreen == enabled) return;
        SuspendLayout();
        if (enabled)
        {
            windowedState = WindowState;
            windowedBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            windowedMinimum = MinimumSize;
            Rectangle screen = Screen.FromHandle(Handle).Bounds;
            immersiveFullscreen = true;
            WindowState = FormWindowState.Normal;
            MinimumSize = Size.Empty;
            Padding = Padding.Empty;
            aetherRoot.Visible = false;
            gameView.Parent = this;
            gameView.Dock = DockStyle.Fill;
            gameView.Visible = true;
            gameView.BringToFront();
            Bounds = screen;
        }
        else
        {
            immersiveFullscreen = false;
            gameView.Parent = aetherStage;
            gameView.Dock = DockStyle.Fill;
            aetherRoot.Visible = true;
            Padding = new Padding(1);
            MinimumSize = windowedMinimum;
            WindowState = FormWindowState.Normal;
            Bounds = windowedBounds;
            FitWindowToScreen();
            WindowState = windowedState;
            UpdateAetherSessionUi(session?.LatestSnapshot);
        }
        ResumeLayout(true);
        gameView.Focus();
    }

    private void FitWindowToScreen()
    {
        if (immersiveFullscreen || WindowState != FormWindowState.Normal) return;
        Rectangle work = Screen.FromHandle(Handle).WorkingArea;
        MinimumSize = new Size(Math.Min(MinimumSize.Width, work.Width), Math.Min(MinimumSize.Height, work.Height));
        int width = Math.Min(Width, work.Width), height = Math.Min(Height, work.Height);
        Bounds = new Rectangle(Math.Clamp(Left, work.Left, work.Right - width),
            Math.Clamp(Top, work.Top, work.Bottom - height), width, height);
    }
}
