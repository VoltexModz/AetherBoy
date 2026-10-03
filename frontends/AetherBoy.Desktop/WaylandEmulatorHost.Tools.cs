using SDL3;
using AetherBoy.Runtime;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private LinuxWavRecorder? recorder;
    private string cheatCode = "";
    private bool editingCheat;
    private int cheatPage;
    private CheatCodeFormat cheatFormat;

    private void ToggleRecording() => TryUiAction(() =>
    {
        if (recorder is not null) { StopRecording(); return; }
        if (session is null || audioOutput is null) throw new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("Open a cartridge with audio enabled first."));
        string directory = Path.Combine(dataPaths.Data, "recordings");
        Directory.CreateDirectory(directory);
        recorder = new LinuxWavRecorder(Path.Combine(directory, $"aetherboy-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.wav"),
            audioOutput.SourceRate, audioOutput.Channels);
    }, global::AetherBoy.Runtime.Localization.UiText.Get("Recording updated. WAV files are saved in your recordings folder."));

    private void StopRecording()
    {
        var previous = Interlocked.Exchange(ref recorder, null);
        previous?.Dispose();
    }

    private void DrawToolsPage()
    {
        if (showBarcodeBoy) { DrawBarcodeBoyPage(); return; }
        if (showOnlineLinkPage) { DrawOnlineLinkPage(); return; }
        if (showGameplayCapture) { DrawGameplayCapturePage(); return; }
        Ink(300, 198, global::AetherBoy.Runtime.Localization.UiText.Get("WAV RECORDING"), 14, Colors.Cyan, true);
        ActionButton(830, 184, 280, 38, global::AetherBoy.Runtime.Localization.UiText.Get("OPEN BARCODE BOY"), () => OpenSettingsDestination(LinuxSettingsDestination.BarcodeBoy));
        ActionButton(300, 233, 245, 44, recorder is null ? global::AetherBoy.Runtime.Localization.UiText.Get("START RECORDING") : global::AetherBoy.Runtime.Localization.UiText.Get("STOP RECORDING"), ToggleRecording,
            recorder is not null, session is not null && audioOutput is not null);
        ActionButton(565, 233, 245, 44, global::AetherBoy.Runtime.Localization.UiText.Get("OPEN RECORDINGS"), () => OpenFolder(Path.Combine(dataPaths.Data, "recordings")));
        ActionButton(830, 233, 280, 44, "ONLINE LINK · F10", OpenOnlineLinkPage);
        Ink(300, 291, textRenderer.Fit(recorder?.Error ?? (recorder is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Start recording, then return to the game.") :
            global::AetherBoy.Runtime.Localization.UiText.Format("Recording · Dropped blocks: {0} · Limit: 128 MiB", recorder.DroppedBlocks)), 510, 14), 14, Colors.Muted);
        ActionButton(830, 286, 280, 40, global::AetherBoy.Runtime.Localization.UiText.Get("OPEN LOCAL LINK"), OpenLocalLinkPage);
        Ink(300, 338, global::AetherBoy.Runtime.Localization.UiText.Get("Cheats for this session"), 14, Colors.Cyan, true);
        bool gbaCheats = session?.LatestSnapshot.Rom?.IsGameBoyAdvance == true;
        if (gbaCheats)
        {
            ActionButton(830, 328, 280, 32, session!.LatestSnapshot.CheatButtonPressed ? global::AetherBoy.Runtime.Localization.UiText.Get("RELEASE DEVICE BUTTON") : global::AetherBoy.Runtime.Localization.UiText.Get("HOLD DEVICE BUTTON"),
                () => TryUiAction(() => session!.SetCheatButtonAsync(!session.LatestSnapshot.CheatButtonPressed).GetAwaiter().GetResult(),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Cheat-device button updated.")), session.LatestSnapshot.CheatButtonPressed, !IsOnlineLink);
            string[] formats = [global::AetherBoy.Runtime.Localization.UiText.Get("AUTO"), "CODEBREAKER", "GS v1/v2", "GS RAW", "AR v3", "AR v3 RAW"];
            ActionButton(967, 371, 143, 42, formats[(int)cheatFormat], () => cheatFormat = (CheatCodeFormat)(((int)cheatFormat + 1) % formats.Length), enabled: !IsOnlineLink);
        }
        DrawTextEntry(TextField.Cheat, 300, 371, 510, 42, global::AetherBoy.Runtime.Localization.UiText.Get("ENTER CHEAT CODE"), session is not null && !IsOnlineLink);
        ActionButton(830, 371, 120, 42, global::AetherBoy.Runtime.Localization.UiText.Get("ADD"), () => TryUiAction(() =>
        {
            if (session is null) return;
            session.AddCheatAsync("Cheat", gbaCheats ? CheatCodeInput.Prepare(cheatCode, cheatFormat) : cheatCode).GetAwaiter().GetResult();
            editingCheat = false; SDL.StopTextInput(window); cheatCode = "";
        }, global::AetherBoy.Runtime.Localization.UiText.Get("Cheat added for this session.")), enabled: session is not null && !IsOnlineLink && !string.IsNullOrWhiteSpace(cheatCode));
        var cheats = session?.LatestSnapshot.Cheats;
        cheatPage = Math.Clamp(cheatPage, 0, Math.Max(0, ((cheats?.Count ?? 0) - 1) / 2));
        int row = 0;
        if (cheats is not null)
            foreach (var cheat in cheats.Skip(cheatPage * 2).Take(2))
            {
                float y = 435 + row++ * 50;
                ActionButton(300, y, 510, 42, textRenderer.Fit((cheat.Enabled ? global::AetherBoy.Runtime.Localization.UiText.Get("Enabled: ") : global::AetherBoy.Runtime.Localization.UiText.Get("Disabled: ")) +
                    cheat.Name + " — " + cheat.Code, 490, 12),
                    () => TryUiAction(() => session!.ToggleCheatAsync(cheat.Id).GetAwaiter().GetResult(), global::AetherBoy.Runtime.Localization.UiText.Get("Cheat toggled.")));
                ActionButton(830, y, 120, 42, global::AetherBoy.Runtime.Localization.UiText.Get("REMOVE"), () => TryUiAction(() => session!.RemoveCheatAsync(cheat.Id).GetAwaiter().GetResult(), global::AetherBoy.Runtime.Localization.UiText.Get("Cheat removed.")));
            }
        ActionButton(300, 550, 130, 38, global::AetherBoy.Runtime.Localization.UiText.Get("PREVIOUS"), () => cheatPage--, enabled: cheatPage > 0);
        ActionButton(445, 550, 130, 38, global::AetherBoy.Runtime.Localization.UiText.Get("NEXT"), () => cheatPage++, enabled: (cheatPage + 1) * 2 < (cheats?.Count ?? 0));
        Ink(600, 550, gbaCheats ? global::AetherBoy.Runtime.Localization.UiText.Get("Choose a format. Join master and code lines with +.") : "GB/GBC: GameShark, Genie, CodeBreaker, AAAA:VV", 12, Colors.Muted);
        Ink(600, 572, global::AetherBoy.Runtime.Localization.UiText.Get("Check your codes: wrong codes can change your save."), 12, Colors.Muted);
        ActionButton(300, 596, 175, 38, "SCREENSHOT · F12", CaptureScreenshot,
            enabled: session is not null && pendingScreenshot is null);
        ActionButton(487, 596, 210, 38, global::AetherBoy.Runtime.Localization.UiText.Get("OPEN SCREENSHOTS"), () => OpenFolder(Path.Combine(dataPaths.Data, "screenshots")));
        ActionButton(709, 596, 230, 38, global::AetherBoy.Runtime.Localization.UiText.Get("PERFORMANCE · F9"), TogglePerformanceOverlay, options.PerformanceOverlay);
        ActionButton(951, 596, 159, 38, global::AetherBoy.Runtime.Localization.UiText.Get("RECORD VIDEO"), () => showGameplayCapture = true);
    }
}
