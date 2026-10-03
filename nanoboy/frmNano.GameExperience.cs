using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Storage;

namespace nanoboy;

public partial class frmNano
{
    private SavedCheckpoint? undoQuickLoad;
    private bool pendingResume;
    private readonly ActivePlaytimeClock activityClock = new();
    private readonly PlaytimeJournal activityJournal = new();
    private double sessionPlaySeconds;
    private long lastLibraryFlush, lastResumeSave;
    private frmStateGallery? stateGallery;

    private void ApplyGameProfilePreferences()
    {
        input.Clear();
        keyboardAdvanceButtons = GameBoyAdvanceButtons.None;
        gamepadAdvanceButtons = GameBoyAdvanceButtons.None;
        postedAdvanceButtons = GameBoyAdvanceButtons.None;
        if (session != null)
        {
            ObserveSessionCommand(session.SetButtonsAsync(nanoboy.Core.GameBoyButtons.None));
            ObserveSessionCommand(session.SetGameBoyAdvanceButtonsAsync(GameBoyAdvanceButtons.None));
        }
        SetPalette(settings.PaletteIndex);
        SetDisplayFilter(settings.DisplayFilterIndex);
        LoadConfiguration();
        ApplyAudioSettingsFromControlCenter();
        if (!immersiveFullscreen && WindowState == FormWindowState.Normal)
        { ClientSize = GetAetherClientSize(settings.VideoScaleFactor); if (IsHandleCreated) FitWindowToScreen(); }
    }

    private void ChangeGameProfile(bool reset)
    {
        try
        {
            if (reset) settings.ResetGameProfile();
            else settings.EnableGameProfile(!settings.GameProfileEnabled);
            ApplyGameProfilePreferences();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        { AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message), global::AetherBoy.Runtime.Localization.UiText.Get("Spielprofil"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private async Task SaveCheckpointAsync(int slot, bool automatic = false)
    {
        EmulationSession? current = session;
        string? rom = currentRomPath;
        if (current == null || rom == null || stateOperationInProgress || IsOnlineLink ||
            !current.LatestSnapshot.Supports(EmulationFeature.SaveStates)) return;
        stateOperationInProgress = true;
        try
        {
            if (!automatic) SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Format("{0} · wird gespeichert …", SlotName(slot)), false);
            SavedCheckpoint checkpoint = await WindowsSaveStateStore.CaptureAsync(current);
            await Task.Run(() => WindowsSaveStateStore.Default.Write(rom, slot, checkpoint));
            RememberPreview(rom, checkpoint);
            if (!automatic && ReferenceEquals(current, session))
                SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Format("{0} · gespeichert · {1:HH:mm:ss}", SlotName(slot), DateTime.Now), false);
            testerSession?.RecordOperation(automatic ? "auto_resume_save" : "quick_save", slot, true);
        }
        catch (Exception ex)
        {
            testerSession?.RecordException("checkpoint.save_failed", ex);
            if (ReferenceEquals(current, session)) SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Format("{0} · Speichern fehlgeschlagen", SlotName(slot)), true);
            if (!automatic) AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message), global::AetherBoy.Runtime.Localization.UiText.Get("Save State fehlgeschlagen"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { stateOperationInProgress = false; }
    }

    private async Task LoadCheckpointAsync(int slot, bool undo = false)
    {
        EmulationSession? current = session;
        string? rom = currentRomPath;
        if (current == null || rom == null || stateOperationInProgress || IsOnlineLink) return;
        if (undo && undoQuickLoad == null) { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Kein Schnellladen zum Rückgängigmachen"), true); return; }
        stateOperationInProgress = true;
        bool wasPaused = current.LatestSnapshot.IsPaused;
        bool pausedByOperation = false;
        try
        {
            byte[] target = undo ? undoQuickLoad!.State : await Task.Run(() => WindowsSaveStateStore.Default.Read(rom, slot));
            await current.SetPausedAsync(true);
            pausedByOperation = true;
            SavedCheckpoint before = await WindowsSaveStateStore.CaptureAsync(current);
            await current.RestoreStateAsync(target);
            if (!ReferenceEquals(session, current)) return;
            undoQuickLoad = undo ? null : before; // A failed restore must never replace the last valid undo point.
            Volatile.Read(ref audioOutput)?.ClearBuffer();
            displayedFrameSequence = 0;
            SetSaveFeedback(undo ? global::AetherBoy.Runtime.Localization.UiText.Get("Schnellladen rückgängig gemacht") : global::AetherBoy.Runtime.Localization.UiText.Format("{0} · geladen · Rückgängig ist verfügbar", SlotName(slot)), false);
            testerSession?.RecordOperation(undo ? "quick_load_undo" : "quick_load", slot, true);
        }
        catch (Exception ex)
        {
            testerSession?.RecordException("checkpoint.load_failed", ex);
            SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Format("{0} · Laden fehlgeschlagen · laufender Zustand bleibt erhalten", SlotName(slot)), true);
            AetherSignal.Show(this, ex is FileNotFoundException ? global::AetherBoy.Runtime.Localization.UiText.Get("Dieser Slot ist noch leer.") : global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message),
                global::AetherBoy.Runtime.Localization.UiText.Get("Save State nicht geladen"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            try
            {
                if (pausedByOperation && !wasPaused && ReferenceEquals(session, current) && current.State == SessionState.Paused)
                    await current.SetPausedAsync(false);
            }
            catch (InvalidOperationException) { }
            stateOperationInProgress = false;
        }
    }

    private void RememberPreview(string rom, SavedCheckpoint checkpoint)
    {
        try { WindowsGameLibraryStore.Default.Update(rom, entry => entry with { PreviewPng = checkpoint.Preview.Png ?? entry.PreviewPng }); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        { testerSession?.RecordException("library.preview_failed", ex); }
    }

    private void SaveResumeBeforeStop(EmulationSession current, string rom)
    {
        if (current.OnlineLink is not null) return;
        if (current.State is not (SessionState.Running or SessionState.Paused) ||
            !current.LatestSnapshot.Supports(EmulationFeature.SaveStates) || current.LatestSnapshot.EmulatedFrameCount < 1 || pendingResume) return;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            SavedCheckpoint checkpoint = WindowsSaveStateStore.CaptureAsync(current, timeout.Token)
                .WaitAsync(timeout.Token).GetAwaiter().GetResult();
            WindowsSaveStateStore.Default.Write(rom, WindowsSaveStateStore.ResumeSlot, checkpoint);
            RememberPreview(rom, checkpoint);
        }
        catch (Exception ex)
        {
            testerSession?.RecordException("resume.on_exit_failed", ex);
            AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.Get("Der Fortsetzen-Slot konnte nicht aktualisiert werden.\n") +
                global::AetherBoy.Runtime.Localization.UiText.Get("Vorhandene manuelle Slots bleiben erhalten.\n\n") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message),
                global::AetherBoy.Runtime.Localization.UiText.Get("Fortsetzen nicht gesichert"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void TrackGameActivity(EmulationSnapshot snapshot)
    {
        if (IsOnlineLink) { pendingResume = false; activityClock.Reset(); return; }
        bool running = snapshot.State == SessionState.Running && !snapshot.IsPaused &&
            !stateOperationInProgress && !quickMenuOpen && Enabled && romPreparation is null &&
            controlCenter is not { IsDisposed: false, Visible: true };
        double elapsed = activityClock.Sample(Environment.TickCount64, snapshot.State, !running);
        if (currentRomPath is not null) activityJournal.Add(currentRomPath, elapsed);
        sessionPlaySeconds += elapsed;
        if (Environment.TickCount64 - lastLibraryFlush >= 30000) FlushGameActivity();
        if (pendingResume && snapshot.Rom != null && !stateOperationInProgress && snapshot.State is SessionState.Running or SessionState.Paused)
        {
            pendingResume = false;
            _ = LoadCheckpointAsync(WindowsSaveStateStore.ResumeSlot);
        }
        else if (running && !stateOperationInProgress && Environment.TickCount64 - lastResumeSave >= 60000)
        {
            lastResumeSave = Environment.TickCount64;
            _ = SaveCheckpointAsync(WindowsSaveStateStore.ResumeSlot, automatic: true);
        }
    }

    private void FlushGameActivity()
    {
        lastLibraryFlush = Environment.TickCount64;
        if (currentRomPath != null && session?.LatestSnapshot.Rom is { } rom)
        {
            try
            {
            WindowsGameLibraryStore.Default.Update(currentRomPath, entry => entry with
            {
                Title = entry.HasCustomTitle || string.IsNullOrWhiteSpace(rom.Title) ? entry.Title : rom.Title.Trim(),
                System = rom.IsGameBoyAdvance ? "GBA" : rom.HasColorFeatures ? "GBC" : "GB",
                LastPlayedUtc = DateTimeOffset.UtcNow
            });
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            { testerSession?.RecordException("library.activity_failed", ex); }
        }
        var failures = activityJournal.Flush((path, seconds) => WindowsGameLibraryStore.Default.Update(path, entry => entry with
        { PlayedSeconds = Math.Min(LibraryMetadata.MaximumPlaySeconds, entry.PlayedSeconds + seconds) }));
        foreach (var failure in failures) testerSession?.RecordException("library.activity_failed", failure);
        if (failures.Count > 0) SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Spielzeit nicht gespeichert. Erneuter Versuch folgt."), true);
    }

    private void OpenStateGallery()
    {
        if (IsOnlineLink) { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Save States sind im Online-Link gesperrt"), true); return; }
        if (currentRomPath == null || session == null) return;
        if (stateGallery is { IsDisposed: false }) { stateGallery.Activate(); return; }
        stateGallery = new frmStateGallery(currentRomPath, SaveCheckpointAsync, LoadCheckpointAsync,
            () => undoQuickLoad != null, () => stateOperationInProgress);
        stateGallery.FormClosed += (_, _) => stateGallery = null;
        stateGallery.Show(this);
    }

    private static string SlotName(int slot) => slot == WindowsSaveStateStore.ResumeSlot ? global::AetherBoy.Runtime.Localization.UiText.Get("FORTSETZEN") : $"SLOT {slot}";
}
