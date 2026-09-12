using System.IO.Compression;
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
        ActionButton(300, 198, 180, 42, "BACK TO SLOTS", () => { showBackups = false; pendingBatteryRestore = null; });
        var battery = session?.LatestSnapshot.Rom?.BatterySave;
        if (battery?.IsEnabled != true || storage is null)
        { Ink(300, 280, "Open a cartridge with battery-backed save data first.", 16); return; }
        Ink(300, 260, "Restore restarts the game. Your current save is backed up first.", 14, Colors.Muted);
        var files = diskSnapshot?.Backups ?? [];
        if (diskSnapshot is null) Ink(300, 430, "Loading backup information…", 14, Colors.Muted);
        else if (diskSnapshot.Error is not null) Ink(300, 430, textRenderer.Fit(diskSnapshot.Error, 800), 14, Colors.Danger);
        foreach (var file in files.Where(file => file.Generation != BatterySaveGeneration.Current))
        {
            int generation = (int)file.Generation;
            float y = 295 + (generation - 1) * 58;
            ActionButton(300, y, 180, 42, $"BACKUP {generation}", () =>
            {
                pendingBatteryRestore = BatterySaveStore.ReadBackup(storage.SavePath, battery.ExpectedLength, file.Generation);
                pendingRestoreLabel = $"Backup {generation}";
            }, enabled: file.Exists && file.IsValid);
            Ink(500, y + 11, !file.Exists ? "No backup yet" : !file.IsValid ? "Invalid backup — cannot restore" :
                file.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") + $" · {file.Length} bytes", 14, Colors.Muted);
        }
        ActionButton(300, 480, 220, 42, "EXPORT SAVES ZIP", ExportSaves);
        ActionButton(540, 480, 220, 42, "IMPORT BATTERY .SAV", () =>
        { if (fileDialogOpen == 0) { pickingBatterySave = true; ShowRomDialog(); } });
        if (pendingBatteryRestore is not null)
        {
            Ink(300, 537, pendingRestoreLabel + " selected. Replace battery data and restart?", 14, Colors.Cyan);
            ActionButton(300, 568, 260, 42, "CONFIRM RESTORE", RestoreBattery, true);
            ActionButton(580, 568, 180, 42, "CANCEL", () => pendingBatteryRestore = null);
        }
        else Ink(300, 560, "Import checks size; choose a save belonging to this exact game.", 14, Colors.Muted);
    }

    private void SelectBatteryImport(string path)
    {
        int expected = session?.LatestSnapshot.Rom?.BatterySave.ExpectedLength ?? 0;
        if (expected <= 0 || new FileInfo(path).Length != expected)
            throw new InvalidDataException($"Battery save must contain exactly {expected} bytes.");
        pendingBatteryRestore = File.ReadAllBytes(path);
        pendingRestoreLabel = "Imported .sav";
        showBackups = true;
    }

    private void RestoreBattery()
    {
        if (pendingBatteryRestore is null || storage is null || session is null || IsLoading || stateOperation is not null) return;
        byte[] bytes = pendingBatteryRestore;
        pendingBatteryRestore = null;
        int expected = session.LatestSnapshot.Rom!.BatterySave.ExpectedLength;
        TryUiAction(() => RestartAroundSaveOperation(() =>
        {
            // Export all data, including RTC sidecars, before changing the selected battery image.
            ExportSaveArchive();
            BatterySaveStore.Restore(storage.SavePath, expected, bytes);
            diagnostics.Record("battery_restored", new { bytes = expected });
        }), "Battery save restored. The game has restarted; previous data is in exports.");
    }

    private void RestartAroundSaveOperation(Action operation)
    {
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

    private string ExportSaveArchive()
    {
        if (storage is null) throw new InvalidOperationException("Open a cartridge first.");
        string directory = Path.Combine(dataPaths.Data, "exports");
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, $"saves-{storage.Identity[..12]}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(target, ZipArchiveMode.Create);
        foreach (string folder in new[] { Path.GetDirectoryName(storage.SavePath)!, Path.GetDirectoryName(storage.StateBasePath)! })
            foreach (string file in Directory.EnumerateFiles(folder).Where(file => !file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)))
                archive.CreateEntryFromFile(file, Path.GetFileName(Path.GetDirectoryName(file)) == storage.Identity && file.Contains(Path.DirectorySeparatorChar + "states" + Path.DirectorySeparatorChar)
                    ? "states/" + Path.GetFileName(file) : "battery/" + Path.GetFileName(file));
        return target;
    }

    private void ExportSaves() => TryUiAction(() =>
    {
        if (session is null) return;
        // Export the last persisted battery family and state files while the Control Center is paused.
        ExportSaveArchive();
        OpenFolder(Path.Combine(dataPaths.Data, "exports"));
    }, "Exported persisted saves and states. Recent in-memory battery changes save on close.");
}
