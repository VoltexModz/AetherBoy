using System;
using System.Diagnostics;
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
    private long activityTimestamp;
    private bool activityWasRunning;
    private double pendingPlaySeconds;
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
        { AetherSignal.Show(this, ex.Message, "Spielprofil", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
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
            if (!automatic) SetSaveFeedback($"{SlotName(slot)} · wird gespeichert …", false);
            SavedCheckpoint checkpoint = await WindowsSaveStateStore.CaptureAsync(current);
            await Task.Run(() => WindowsSaveStateStore.Default.Write(rom, slot, checkpoint));
            RememberPreview(rom, checkpoint);
            if (!automatic && ReferenceEquals(current, session))
                SetSaveFeedback($"{SlotName(slot)} · gespeichert · {DateTime.Now:HH:mm:ss}", false);
            testerSession?.RecordOperation(automatic ? "auto_resume_save" : "quick_save", slot, true);
        }
        catch (Exception ex)
        {
            testerSession?.RecordException("checkpoint.save_failed", ex);
            if (ReferenceEquals(current, session)) SetSaveFeedback($"{SlotName(slot)} · Speichern fehlgeschlagen", true);
            if (!automatic) AetherSignal.Show(this, ex.Message, "Save State fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { stateOperationInProgress = false; }
    }

    private async Task LoadCheckpointAsync(int slot, bool undo = false)
    {
        EmulationSession? current = session;
        string? rom = currentRomPath;
        if (current == null || rom == null || stateOperationInProgress || IsOnlineLink) return;
        if (undo && undoQuickLoad == null) { SetSaveFeedback("Kein Schnellladen zum Rückgängigmachen", true); return; }
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
            SetSaveFeedback(undo ? "Schnellladen rückgängig gemacht" : $"{SlotName(slot)} · geladen · Rückgängig ist verfügbar", false);
            testerSession?.RecordOperation(undo ? "quick_load_undo" : "quick_load", slot, true);
        }
        catch (Exception ex)
        {
            testerSession?.RecordException("checkpoint.load_failed", ex);
            SetSaveFeedback($"{SlotName(slot)} · Laden fehlgeschlagen · laufender Zustand bleibt erhalten", true);
            AetherSignal.Show(this, ex is FileNotFoundException ? "Dieser Slot ist noch leer." : ex.Message,
                "Save State nicht geladen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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
            AetherSignal.Show(this, "Der Fortsetzen-Slot konnte nicht aktualisiert werden.\n" +
                "Vorhandene manuelle Slots bleiben erhalten.\n\n" + ex.Message,
                "Fortsetzen nicht gesichert", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void TrackGameActivity(EmulationSnapshot snapshot)
    {
        if (IsOnlineLink) { pendingResume = false; activityWasRunning = false; return; }
        long now = Stopwatch.GetTimestamp();
        bool running = snapshot.State == SessionState.Running && !stateOperationInProgress;
        if (running && activityWasRunning && activityTimestamp != 0)
        {
            double elapsed = (now - activityTimestamp) / (double)Stopwatch.Frequency;
            if (elapsed < 2) pendingPlaySeconds += elapsed; // Do not count suspend/hibernation gaps.
        }
        activityTimestamp = now;
        activityWasRunning = running;
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
        if (currentRomPath == null || session?.LatestSnapshot.Rom == null) return;
        try
        {
            var rom = session.LatestSnapshot.Rom;
            WindowsGameLibraryStore.Default.Update(currentRomPath, entry => entry with
            {
                Title = entry.HasCustomTitle || string.IsNullOrWhiteSpace(rom.Title) ? entry.Title : rom.Title.Trim(),
                System = rom.IsGameBoyAdvance ? "GBA" : rom.HasColorFeatures ? "GBC" : "GB",
                PlayedSeconds = entry.PlayedSeconds + pendingPlaySeconds,
                LastPlayedUtc = DateTimeOffset.UtcNow
            });
            pendingPlaySeconds = 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { testerSession?.RecordException("library.activity_failed", ex); }
    }

    private void OpenStateGallery()
    {
        if (IsOnlineLink) { SetSaveFeedback("Save States sind im Online-Link gesperrt", true); return; }
        if (currentRomPath == null || session == null) return;
        if (stateGallery is { IsDisposed: false }) { stateGallery.Activate(); return; }
        stateGallery = new frmStateGallery(currentRomPath, SaveCheckpointAsync, LoadCheckpointAsync,
            () => undoQuickLoad != null, () => stateOperationInProgress);
        stateGallery.FormClosed += (_, _) => stateGallery = null;
        stateGallery.Show(this);
    }

    private static string SlotName(int slot) => slot == WindowsSaveStateStore.ResumeSlot ? "FORTSETZEN" : $"SLOT {slot}";
}
