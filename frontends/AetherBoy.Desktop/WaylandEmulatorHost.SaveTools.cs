using AetherBoy.Runtime;
using nanoboy.Core;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private bool showBackups;
    private byte[]? pendingBatteryRestore;
    private string pendingRestoreLabel = "";
    private bool pickingBatterySave;

    private void DrawBackupPage()
    {
        if (IsOnlineLink) { DrawOnlineLinkPage(); return; }
        ActionButton(300, 198, 180, 42, global::AetherBoy.Runtime.Localization.UiText.Get("BACK TO SLOTS"), () => { showBackups = false; pendingBatteryRestore = null; });
        var battery = session?.LatestSnapshot.Rom?.BatterySave;
        if (battery?.IsEnabled != true || storage is null)
        { Ink(300, 280, global::AetherBoy.Runtime.Localization.UiText.Get("Open a cartridge with battery-backed save data first."), 16); return; }
        Ink(300, 260, global::AetherBoy.Runtime.Localization.UiText.Get("Restore restarts the game. Your current save is backed up first."), 14, Colors.Muted);
        var files = diskSnapshot?.Backups ?? [];
        if (diskSnapshot is null) Ink(300, 430, global::AetherBoy.Runtime.Localization.UiText.Get("Loading backup information…"), 14, Colors.Muted);
        else if (diskSnapshot.Error is not null) Ink(300, 430, textRenderer.Fit(diskSnapshot.Error, 800), 14, Colors.Danger);
        foreach (var file in files.Where(file => file.Generation != BatterySaveGeneration.Current))
        {
            int generation = (int)file.Generation;
            float y = 295 + (generation - 1) * 58;
            ActionButton(300, y, 180, 42, global::AetherBoy.Runtime.Localization.UiText.Format("BACKUP {0}", generation), () =>
            {
                pendingBatteryRestore = BatterySaveStore.ReadBackup(storage.SavePath, battery.ExpectedLength, file.Generation);
                pendingRestoreLabel = global::AetherBoy.Runtime.Localization.UiText.Format("Backup {0}", generation);
            }, enabled: file.Exists && file.IsValid);
            Ink(500, y + 11, !file.Exists ? global::AetherBoy.Runtime.Localization.UiText.Get("No backup yet") : !file.IsValid ? global::AetherBoy.Runtime.Localization.UiText.Get("Invalid backup — cannot restore") :
                file.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") + $" · {file.Length} bytes", 14, Colors.Muted);
        }
        ActionButton(300, 480, 220, 42, global::AetherBoy.Runtime.Localization.UiText.Get("EXPORT SAVES ZIP"), ExportSaves);
        ActionButton(540, 480, 220, 42, global::AetherBoy.Runtime.Localization.UiText.Get("IMPORT BATTERY .SAV"), () =>
        { if (fileDialogOpen == 0) { pickingBatterySave = true; ShowRomDialog(); } });
        if (pendingBatteryRestore is not null)
        {
            Ink(300, 537, pendingRestoreLabel + global::AetherBoy.Runtime.Localization.UiText.Get(" selected. Replace battery data and restart?"), 14, Colors.Cyan);
            ActionButton(300, 568, 260, 42, global::AetherBoy.Runtime.Localization.UiText.Get("CONFIRM RESTORE"), RestoreBattery, true);
            ActionButton(580, 568, 180, 42, global::AetherBoy.Runtime.Localization.UiText.Get("CANCEL"), () => pendingBatteryRestore = null);
        }
        else Ink(300, 560, global::AetherBoy.Runtime.Localization.UiText.Get("Import checks size; choose a save belonging to this exact game."), 14, Colors.Muted);
    }

    private void SelectBatteryImport(string path)
    {
        if (IsOnlineLink) throw new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("Battery imports are disabled during Online Link."));
        int expected = session?.LatestSnapshot.Rom?.BatterySave.ExpectedLength ?? 0;
        if (expected <= 0)
            throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Format("Battery save must contain exactly {0} bytes.", expected));
        using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (source.Length != expected)
            throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Format("Battery save must contain exactly {0} bytes.", expected));
        byte[] candidate = new byte[expected];
        source.ReadExactly(candidate);
        if (source.ReadByte() != -1)
            throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Format("Battery save must contain exactly {0} bytes.", expected));
        pendingBatteryRestore = candidate;
        pendingRestoreLabel = global::AetherBoy.Runtime.Localization.UiText.Get("Imported .sav");
        showBackups = true;
    }

    private void RestoreBattery()
    {
        if (pendingBatteryRestore is null || storage is null || session is null || IsLoading || stateOperation is not null) return;
        byte[] bytes = pendingBatteryRestore;
        pendingBatteryRestore = null;
        int expected = session.LatestSnapshot.Rom!.BatterySave.ExpectedLength;
        TryUiAction(() =>
        {
            try
            {
                RestartAroundSaveOperation(() =>
                {
                    // Export all data, including RTC sidecars, before changing the selected battery image.
                    ExportSaveArchive(expected);
                    BatterySaveStore.Restore(storage.SavePath, expected, bytes);
                });
                diagnostics.Record("battery_restored", new { bytes = expected });
                diagnostics.Record("operation.completed", new { operation = "battery_restore", succeeded = true });
            }
            catch (Exception error)
            {
                diagnostics.Record("operation.completed", new { operation = "battery_restore", succeeded = false,
                    reason = error.GetBaseException().GetType().Name });
                throw;
            }
        }, global::AetherBoy.Runtime.Localization.UiText.Get("Battery save restored. The game has restarted; previous data is in exports."));
    }

    private void RestartAroundSaveOperation(Action operation)
    {
        if (IsOnlineLink) throw new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("Battery restore and restart are disabled during Online Link."));
        if (session is null || storage is null || romPath is null) return;
        FinishStateWork();
        undoState = null; undoIdentity = null;
        EmulationSession old = session;
        old.AudioSamplesAvailable -= OnAudioSamplesAvailable;
        // Dispose must succeed before data is modified. Keep storage's exclusive ownership.
        old.Dispose();
        session = null;
        audioOutput?.Clear();
        try { operation(); }
        finally
        {
            session = new EmulationSession(romPath, storage.SavePath, FirmwareFor(romPath),
                options.CreateEmulatorConfiguration(EnsureAudioOutput()), options.PaletteIndex);
            if (audioOutput is not null) session.AudioSamplesAvailable += OnAudioSamplesAvailable;
            session.SetPausedAsync(controlCenterVisible).GetAwaiter().GetResult();
            resumeAfterControlCenter = controlCenterVisible;
            displayedFrameSequence = 0;
            RequestDiskRefresh();
        }
    }

    private string ExportSaveArchive(int? expectedSaveLength = null)
    {
        if (storage is null) throw new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("Open a cartridge first."));
        string directory = Path.Combine(dataPaths.Data, "exports");
        int expected = expectedSaveLength ?? session?.LatestSnapshot.Rom?.BatterySave.ExpectedLength ?? 0;
        return LinuxSaveArchive.Export(storage, directory, expected);
    }

    private void ExportSaves() => TryUiAction(() =>
    {
        if (session is null) return;
        // Export the last persisted battery family and state files while the Control Center is paused.
        ExportSaveArchive();
        OpenFolder(Path.Combine(dataPaths.Data, "exports"));
    }, global::AetherBoy.Runtime.Localization.UiText.Get("Exported persisted saves and states. Recent in-memory battery changes save on close."));
}
