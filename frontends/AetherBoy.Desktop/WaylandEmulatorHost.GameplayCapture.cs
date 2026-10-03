using AetherBoy.Runtime;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private bool showGameplayCapture;
    private GameplayRecorder? lastGameplayRecording;
    private Task<GameplayRecorder>? gameplayCaptureStart;

    private void ToggleGameplayCapture()
    {
        if (gameplayCaptureStart is not null) return;
        var recording = session?.GameplayRecording ?? lastGameplayRecording;
        if (recording is { IsRecording: true }) { recording.Stop(); return; }
        if (recording is { Completion.IsCompleted: false }) return;
        if (session is null) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Open a cartridge before recording video."); return; }
        if (session.LatestSnapshot.IsTurboEnabled)
        { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Release Turbo before starting a recording."); return; }
        string path = Path.Combine(dataPaths.Data, "recordings", $"aetherboy-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.avi");
        gameplayCaptureStart = session.StartGameplayRecordingAsync(path);
    }

    private void PollGameplayCapture()
    {
        if (gameplayCaptureStart is not { IsCompleted: true }) return;
        var task = gameplayCaptureStart; gameplayCaptureStart = null;
        try { lastGameplayRecording = task.GetAwaiter().GetResult(); statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Video recording started. Return to your game."); }
        catch (Exception exception)
        {
            diagnostics.Record("capture.start_failed", new { reason = exception.GetType().Name });
            statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Video recording could not start. Check the recordings folder and try again.");
        }
    }

    private void DrawGameplayCapturePage()
    {
        var recording = session?.GameplayRecording ?? lastGameplayRecording;
        Ink(300, 198, global::AetherBoy.Runtime.Localization.UiText.Get("Record gameplay"), 20, Colors.Cyan, true);
        Ink(300, 243, global::AetherBoy.Runtime.Localization.UiText.Get("AVI records the native picture and game audio without an extra encoder."), 14, Colors.Text);
        Ink(300, 273, global::AetherBoy.Runtime.Localization.UiText.Get("Files are large. Each clip stops at 2 GiB; pauses are left out."), 14, Colors.Muted);
        Ink(300, 303, global::AetherBoy.Runtime.Localization.UiText.Get("Turbo, reset or loading an earlier state ends the clip. Your game continues."), 14, Colors.Muted);
        ActionButton(300, 353, 245, 44, recording?.IsRecording == true ? global::AetherBoy.Runtime.Localization.UiText.Get("Stop video") : global::AetherBoy.Runtime.Localization.UiText.Get("Start video"), ToggleGameplayCapture,
            recording?.IsRecording == true, gameplayCaptureStart is null && session is not null);
        ActionButton(565, 353, 245, 44, global::AetherBoy.Runtime.Localization.UiText.Get("Open recordings"), () => OpenFolder(Path.Combine(dataPaths.Data, "recordings")));
        ActionButton(830, 353, 280, 44, global::AetherBoy.Runtime.Localization.UiText.Get("Back to tools"), () => showGameplayCapture = false);
        string state = recording is null ? global::AetherBoy.Runtime.Localization.UiText.Get("No video recorded yet.") : recording.IsRecording
            ? global::AetherBoy.Runtime.Localization.UiText.Format("Recording {0:mm\\:ss}. Return to the game to continue playing.", recording.Duration)
            : !recording.Completion.IsCompleted ? global::AetherBoy.Runtime.Localization.UiText.Get("Finishing the video. Please keep the application open.") : recording.StopReason switch
            {
                GameplayRecordingStop.WriteError => global::AetherBoy.Runtime.Localization.UiText.Get("Video not saved. Check free disk space and folder permissions."),
                GameplayRecordingStop.QueueFull => global::AetherBoy.Runtime.Localization.UiText.Get("Recording stopped: writing could not keep up. The earlier part was saved."),
                GameplayRecordingStop.SizeLimit => global::AetherBoy.Runtime.Localization.UiText.Get("Video saved at the 2 GiB limit. You can start another recording."),
                GameplayRecordingStop.Turbo => global::AetherBoy.Runtime.Localization.UiText.Get("Turbo ended the recording. The earlier part was saved."),
                GameplayRecordingStop.TimelineChanged => global::AetherBoy.Runtime.Localization.UiText.Get("A reset or state change ended the clip. The earlier part was saved."),
                GameplayRecordingStop.AudioChanged => global::AetherBoy.Runtime.Localization.UiText.Get("The audio format changed. Video saved; start a new recording."),
                _ => global::AetherBoy.Runtime.Localization.UiText.Format("Video saved ({0:mm\\:ss}). Open recordings to watch it.", recording.Duration)
            };
        Ink(300, 437, state, 14, Colors.Text);
        if (recording?.StopReason == GameplayRecordingStop.WriteError)
            Ink(300, 469, global::AetherBoy.Runtime.Localization.UiText.Get("An incomplete .partial file may remain; it is not a finished video."), 14, Colors.Muted);
        Ink(300, 528, global::AetherBoy.Runtime.Localization.UiText.Get("Game sound is captured before speaker volume. No microphone or desktop audio."), 14, Colors.Muted);
        Ink(300, 558, global::AetherBoy.Runtime.Localization.UiText.Get("This first version records a single game, not the local-link split view."), 14, Colors.Muted);
    }
}
