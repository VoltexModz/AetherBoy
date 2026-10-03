using System;
using System.IO;
using System.Threading.Tasks;
using AetherBoy.Runtime;
using nanoboy.Storage;

namespace nanoboy;

public partial class frmNano
{
    private GameplayRecorder? lastGameplayRecording;
    private bool gameplayCapturePending;

    private async Task ToggleGameplayRecordingAsync()
    {
        if (gameplayCapturePending) return;
        var current = session;
        var recording = current?.GameplayRecording ?? lastGameplayRecording;
        if (recording is { IsRecording: true })
        {
            recording.Stop();
            SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Das Video wird fertig gespeichert. Du kannst weiterspielen."), false);
            return;
        }
        if (recording is { Completion.IsCompleted: false })
        { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Warte, bis das bisherige Video fertig gespeichert ist."), false); return; }
        if (current is null) { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Öffne zuerst ein Spiel."), false); return; }
        if (current.LatestSnapshot.IsTurboEnabled)
        { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Lass Turbo los und starte dann die Videoaufnahme."), false); return; }
        gameplayCapturePending = true;
        try
        {
            string path = Path.Combine(WindowsDataPaths.Default.Recordings, $"aetherboy-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.avi");
            lastGameplayRecording = await current.StartGameplayRecordingAsync(path);
            SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Die Videoaufnahme läuft. Beenden kannst du sie in den Einstellungen unter Übersicht."), false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            testerSession?.RecordException("capture.start_failed", exception);
            SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Die Videoaufnahme konnte nicht starten. Prüfe den Aufnahmeordner und versuche es erneut."), true);
        }
        finally { gameplayCapturePending = false; }
    }

    private string GetGameplayRecordingStatus()
    {
        var recording = session?.GameplayRecording ?? lastGameplayRecording;
        if (recording is null) return global::AetherBoy.Runtime.Localization.UiText.Get("Noch kein Video aufgenommen.");
        if (recording.IsRecording) return global::AetherBoy.Runtime.Localization.UiText.Format("Videoaufnahme läuft: {0:mm\\:ss}. Pausen werden nicht mit aufgenommen.", recording.Duration);
        if (!recording.Completion.IsCompleted) return global::AetherBoy.Runtime.Localization.UiText.Get("Das Video wird fertig gespeichert. Bitte das Programm noch nicht schließen.");
        return recording.StopReason switch
        {
            GameplayRecordingStop.WriteError => global::AetherBoy.Runtime.Localization.UiText.Get("Video nicht gespeichert. Prüfe Speicherplatz und Schreibrechte; eine unvollständige .partial-Datei kann zurückbleiben."),
            GameplayRecordingStop.QueueFull => global::AetherBoy.Runtime.Localization.UiText.Get("Aufnahme wegen zu langsamer Verarbeitung beendet. Der bisherige Teil liegt im Aufnahmeordner."),
            GameplayRecordingStop.SizeLimit => global::AetherBoy.Runtime.Localization.UiText.Get("Das Video hat die 2-GiB-Grenze erreicht und wurde gespeichert. Du kannst eine neue Aufnahme starten."),
            GameplayRecordingStop.Turbo => global::AetherBoy.Runtime.Localization.UiText.Get("Turbo hat die Aufnahme beendet. Das bisherige Video wurde gespeichert."),
            GameplayRecordingStop.TimelineChanged => global::AetherBoy.Runtime.Localization.UiText.Get("Ein Zeitsprung oder Reset hat die Aufnahme beendet. Das bisherige Video wurde gespeichert."),
            GameplayRecordingStop.AudioChanged => global::AetherBoy.Runtime.Localization.UiText.Get("Das Tonformat hat sich geändert. Das bisherige Video wurde gespeichert; starte eine neue Aufnahme."),
            _ => global::AetherBoy.Runtime.Localization.UiText.Format("Video gespeichert ({0:mm\\:ss}). Öffne den Aufnahmeordner, um es anzusehen.", recording.Duration)
        };
    }
}
