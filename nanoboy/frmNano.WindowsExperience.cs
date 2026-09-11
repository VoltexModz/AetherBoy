using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using nanoboy.Controls;

namespace nanoboy;

public partial class frmNano
{
    private bool immersiveFullscreen;
    private Rectangle windowedBounds;
    private FormWindowState windowedState;
    private Size windowedMinimum;
    private Label? saveFeedbackLabel;
    private string saveFeedback = "F5 speichern · F8 laden · F11 Vollbild · Esc zurück";

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
        ApplyWindowsVideoSettings();
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
        return audio is null ? "AUDIO OFF / NO SESSION" :
            $"{audio.Backend} · {audio.DeviceName}\r\n" +
            $"{audio.SampleRate:N0} Hz · {audio.Channels} ch · Ziel {audio.TargetLatencyMs} ms · Queue {audio.BufferedMs:F1} ms\r\n" +
            $"Unterläufe {audio.Underruns} · Verworfen {audio.DroppedSamples} · Ausgabe-Neustarts {audio.Reconnects}" +
            (audio.ErrorCode is null ? "" : $" · {audio.ErrorCode}");
    }

    private string DescribeWindowsVideo() =>
        $"{gameView.RendererStatus} · VSync {(settings.VideoVSync ? "requested" : "off")} · {DeviceDpi} DPI\r\n" +
        $"Präsentiert {gameView.PresentedFrames:N0} · vor Anzeige ersetzt {gameView.SupersededFrames:N0}" +
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
