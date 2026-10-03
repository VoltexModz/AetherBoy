namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private sealed record SettingsSnapshot(long Generation, byte[] Global, string? Identity, byte[]? Profile);
    private Task? settingsWrite;
    private long settingsGeneration;
    private long settingsWriteGeneration;
    private string? settingsWriteError;
    // Deterministic slow/failing filesystem seam. Production workers only receive serialized bytes.
    internal Func<Task>? SettingsWriteCheckpoint { get; set; }

    private void FlushSettingsIfDue(bool force = false)
    {
        bool failed = CompleteSettingsWrite(force);
        if (settingsWrite is not null || failed || !settingsDirty
            || (!force && Environment.TickCount64 - settingsChangedAt < 300)) return;
        try
        {
            SettingsSnapshot snapshot = CaptureSettingsSnapshot();
            var checkpoint = SettingsWriteCheckpoint;
            settingsWriteGeneration = snapshot.Generation;
            settingsWrite = Task.Run(async () =>
            {
                if (checkpoint is not null) await checkpoint().ConfigureAwait(false);
                // Keep the historical order: commit this game's overrides before global
                // settings. A failure leaves the entire generation dirty for retry.
                if (snapshot.Identity is not null)
                    new LinuxProfileStore(dataPaths).WriteSnapshot(snapshot.Identity, snapshot.Profile!);
                LinuxSettingsStore.WriteSnapshot(settingsPath, snapshot.Global);
            });
            if (force) CompleteSettingsWrite(wait: true);
        }
        catch (Exception exception) { SettingsWriteFailed(exception); }
    }

    private SettingsSnapshot CaptureSettingsSnapshot()
    {
        var effective = LinuxGameProfile.Capture(options);
        string? identity = usingGameProfile ? storage?.Identity : null;
        byte[]? profile = null;
        if (identity is not null)
        {
            var defaults = new LinuxFrontendOptions(); globalProfile.ApplyTo(defaults);
            profile = LinuxProfileStore.SerializeSnapshotBytes(LinuxGameProfile.FromDifference(options, defaults));
        }
        else globalProfile = effective;
        try
        {
            globalProfile.ApplyTo(options);
            return new SettingsSnapshot(settingsGeneration, LinuxSettingsStore.SerializeSnapshotBytes(options), identity, profile);
        }
        finally { effective.ApplyTo(options); }
    }

    // Only explicit scope/ROM/shutdown boundaries wait. Ordinary render ticks merely poll.
    private bool CompleteSettingsWrite(bool wait)
    {
        if (settingsWrite is null || (!wait && !settingsWrite.IsCompleted)) return false;
        var completed = settingsWrite;
        settingsWrite = null;
        try
        {
            completed.GetAwaiter().GetResult();
            if (settingsWriteGeneration == settingsGeneration)
            {
                settingsDirty = false;
                if (loadError == settingsWriteError) loadError = null;
                if (settingsWriteError is not null) statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Preferences saved.");
                settingsWriteError = null;
            }
            return false;
        }
        catch (Exception exception) { SettingsWriteFailed(exception); return true; }
    }

    private void SettingsWriteFailed(Exception exception)
    {
        settingsDirty = true;
        settingsChangedAt = Environment.TickCount64 + 1700;
        var failure = new IOException(global::AetherBoy.Runtime.Localization.UiText.Get("Preferences could not be saved: ") + exception.Message, exception);
        ReportError(failure);
        settingsWriteError = loadError;
        statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Preferences are still unsaved. Retrying shortly; you can keep editing.");
    }
}
