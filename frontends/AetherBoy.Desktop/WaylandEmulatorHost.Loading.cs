using AetherBoy.Runtime;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private sealed record PreparedRom(string Path, LinuxRomStorage? Storage, EmulationSession? Session,
        LinuxGameProfile? Profile, string? ProfileNotice);
    private Task<PreparedRom>? romPreparation;
    private CancellationTokenSource? romPreparationCancellation;
    private bool IsLoading => romPreparation is not null || pendingSession is not null;
    // Test seam blocks after acquiring the new cartridge lock, without touching SDL or a Runtime owner.
    internal Func<CancellationToken, Task>? RomPreparationCheckpoint { get; set; }

    private void BeginRomLoad(string candidate, string? expectedIdentity = null)
    {
        if (IsLoading || stateOperation is not null)
        { statusMessage = "Please wait for the current operation."; return; }
        FlushSettingsIfDue(force: true);
        if (settingsDirty)
        { statusMessage = "Current preferences could not be saved. Retry before switching games."; return; }
        loadError = null;
        pendingRomPath = candidate;
        pendingResumeIdentity = expectedIdentity;
        resumeAfterLoad = session is not null && !session.LatestSnapshot.IsPaused;
        pressedKeys.Clear();
        var previous = session;
        string? activeIdentity = storage?.Identity;
        var defaults = globalProfile;
        bool useFirmware = options.UseFirmware;
        bool audioAvailable = EnsureAudioOutput(); // SDL remains confined to the window thread.
        var checkpoint = RomPreparationCheckpoint;
        var cancellation = romPreparationCancellation = new CancellationTokenSource();
        statusMessage = "Preparing cartridge… Press Esc or Cancel to stop.";
        romPreparation = Task.Run(async () =>
        {
            LinuxRomStorage? acquired = null;
            EmulationSession? next = null;
            var token = cancellation.Token;
            try
            {
                token.ThrowIfCancellationRequested();
                string path = LinuxRomPath.Resolve(candidate);
                string identity = LinuxRomStorage.Identify(path, token);
                if (expectedIdentity is not null && identity != expectedIdentity)
                    throw new InvalidDataException("This cartridge file changed. Open it again to update your library.");
                if (identity == activeIdentity)
                {
                    token.ThrowIfCancellationRequested();
                    library.Remember(identity, path);
                    return new PreparedRom(path, null, null, null, null);
                }
                acquired = LinuxRomStorage.OpenIdentified(dataPaths, path, identity, token);
                if (checkpoint is not null) await checkpoint(token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                byte[]? firmware = ReadFirmware(dataPaths, path, useFirmware);
                var nextOptions = new LinuxFrontendOptions();
                defaults.ApplyTo(nextOptions);
                var profile = new LinuxProfileStore(dataPaths).Read(identity, out string? notice);
                profile?.ApplyTo(nextOptions);
                token.ThrowIfCancellationRequested();
                if (previous is not null)
                {
                    await previous.SetTurboAsync(false).ConfigureAwait(false);
                    await previous.SetPausedAsync(true).ConfigureAwait(false);
                    // The vendored GBA CPU shares static scratch registers. Finish its partial
                    // instruction before constructing another Runtime owner, even when cancelled.
                    if (previous.LatestSnapshot.Rom?.IsGameBoyAdvance == true)
                        _ = await previous.CaptureStateAsync().ConfigureAwait(false);
                }
                token.ThrowIfCancellationRequested();
                next = new EmulationSession(path, acquired.SavePath, firmware,
                    nextOptions.CreateEmulatorConfiguration(audioAvailable), nextOptions.PaletteIndex);
                // Keep cleanup, including a late/cancelled constructor, on this bounded worker.
                while (next.State == SessionState.Starting)
                    await Task.Delay(5, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (next.State == SessionState.Faulted)
                    throw next.Fault ?? new InvalidOperationException("The ROM could not be started.");
                return new PreparedRom(path, acquired, next, profile, notice);
            }
            catch
            {
                DisposeSession(next);
                acquired?.Dispose();
                throw;
            }
        });
    }

    private void CancelRomLoad()
    {
        if (romPreparation is null) return;
        romPreparationCancellation!.Cancel();
        statusMessage = "Cancelling cartridge preparation… Your current session is kept.";
    }

    private void CompleteRomPreparation()
    {
        if (romPreparation is not { IsCompleted: true }) return;
        var task = romPreparation;
        bool cancelled = romPreparationCancellation!.IsCancellationRequested;
        if (cancelled && task.Status == TaskStatus.RanToCompletion)
        {
            // Cancellation can arrive after the worker published its result. Dispose the
            // late owner off the UI thread before releasing the single-operation guard.
            var late = task.GetAwaiter().GetResult();
            PreparedRom DisposeLate()
            {
                DisposeSession(late.Session);
                late.Storage?.Dispose();
                throw new OperationCanceledException();
            }
            romPreparation = Task.Run(DisposeLate);
            return;
        }
        romPreparation = null;
        romPreparationCancellation.Dispose();
        romPreparationCancellation = null;
        try
        {
            var prepared = task.GetAwaiter().GetResult();
            if (prepared.Session is null)
            {
                romPath = prepared.Path;
                pendingRomPath = null;
                pendingResumeIdentity = null;
                ResumeAfterFailedLoad();
                statusMessage = "Cartridge location updated. This ROM is already open.";
                return;
            }
            pendingRomPath = prepared.Path;
            pendingStorage = prepared.Storage;
            pendingSession = prepared.Session;
            pendingGameProfile = prepared.Profile;
            profileNotice = prepared.ProfileNotice;
            audioOutput?.Clear();
        }
        catch (OperationCanceledException)
        {
            pendingRomPath = null;
            pendingResumeIdentity = null;
            ResumeAfterFailedLoad();
            statusMessage = "Cartridge preparation cancelled. Your current session is kept.";
        }
        catch (Exception exception)
        {
            pendingRomPath = null;
            pendingResumeIdentity = null;
            ResumeAfterFailedLoad();
            ReportError(exception);
            statusMessage = "Cartridge could not be opened: " + exception.GetBaseException().Message;
        }
    }

    private void FinishRomPreparation()
    {
        if (romPreparation is null) return;
        romPreparationCancellation!.Cancel();
        try
        {
            var prepared = romPreparation.GetAwaiter().GetResult();
            DisposeSession(prepared.Session);
            prepared.Storage?.Dispose();
        }
        catch { /* Cancellation/failure must still release the worker's storage lock. */ }
        romPreparation = null;
        romPreparationCancellation.Dispose();
        romPreparationCancellation = null;
        pendingResumeIdentity = null;
        ResumeAfterFailedLoad();
    }
}
