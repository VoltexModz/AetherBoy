using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private LinuxWavRecorder? recorder;
    private string cheatCode = "";
    private bool editingCheat;
    private int cheatPage;

    private void ToggleRecording() => TryUiAction(() =>
    {
        if (recorder is not null) { StopRecording(); return; }
        if (session is null || audioOutput is null) throw new InvalidOperationException("Open a cartridge with audio enabled first.");
        string directory = Path.Combine(dataPaths.Data, "recordings");
        Directory.CreateDirectory(directory);
        recorder = new LinuxWavRecorder(Path.Combine(directory, $"aetherboy-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.wav"),
            audioOutput.SourceRate, audioOutput.Channels);
    }, "Recording updated. WAV files are saved in your recordings folder.");

    private void StopRecording()
    {
        var previous = Interlocked.Exchange(ref recorder, null);
        previous?.Dispose();
    }

    private void DrawToolsPage()
    {
        Ink(300, 198, "WAV RECORDING", 14, Colors.Cyan, true);
        ActionButton(300, 233, 245, 44, recorder is null ? "START RECORDING" : "STOP RECORDING", ToggleRecording,
            recorder is not null, session is not null && audioOutput is not null);
        ActionButton(565, 233, 245, 44, "OPEN RECORDINGS", () => OpenFolder(Path.Combine(dataPaths.Data, "recordings")));
        Ink(300, 291, recorder?.Error ?? (recorder is null ? "PCM WAV · Start recording, then close settings to resume the game." :
            $"Recording · Dropped blocks: {recorder.DroppedBlocks} · Limit: 128 MiB"), 14, Colors.Muted);
        Ink(300, 338, "CHEATS · CURRENT SESSION", 14, Colors.Cyan, true);
        DrawTextEntry(TextField.Cheat, 300, 371, 510, 42, "ENTER CHEAT CODE", session is not null);
        ActionButton(830, 371, 120, 42, "ADD", () => TryUiAction(() =>
        {
            if (session is null) return;
            session.AddCheatAsync("Cheat", cheatCode).GetAwaiter().GetResult();
            editingCheat = false; SDL.StopTextInput(window); cheatCode = "";
        }, "Cheat added for this session."), enabled: session is not null && !string.IsNullOrWhiteSpace(cheatCode));
        var cheats = session?.LatestSnapshot.Cheats;
        cheatPage = Math.Clamp(cheatPage, 0, Math.Max(0, ((cheats?.Count ?? 0) - 1) / 2));
        int row = 0;
        if (cheats is not null)
            foreach (var cheat in cheats.Skip(cheatPage * 2).Take(2))
            {
                float y = 435 + row++ * 50;
                ActionButton(300, y, 510, 42, (cheat.Enabled ? "ON · " : "OFF · ") + cheat.Code,
                    () => TryUiAction(() => session!.ToggleCheatAsync(cheat.Id).GetAwaiter().GetResult(), "Cheat toggled."));
                ActionButton(830, y, 120, 42, "REMOVE", () => TryUiAction(() => session!.RemoveCheatAsync(cheat.Id).GetAwaiter().GetResult(), "Cheat removed."));
            }
        ActionButton(300, 550, 130, 38, "PREVIOUS", () => cheatPage--, enabled: cheatPage > 0);
        ActionButton(445, 550, 130, 38, "NEXT", () => cheatPage++, enabled: (cheatPage + 1) * 2 < (cheats?.Count ?? 0));
        Ink(600, 561, "Support varies by system and code type.", 14, Colors.Muted);
        Ink(300, 601, "Game Genie and Action Replay/PAR v3 are not supported.", 14, Colors.Muted);
    }
}
