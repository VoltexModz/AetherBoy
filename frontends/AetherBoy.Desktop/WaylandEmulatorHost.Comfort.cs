using System.Runtime.InteropServices;
using AetherBoy.Runtime;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private Task<LinuxDiskSnapshot>? diskRefresh;
    private LinuxDiskSnapshot? diskSnapshot;
    private int diskGeneration;
    private int requestedDiskGeneration;
    private Task<StateOutcome>? stateOperation;
    private byte[]? undoState;
    private bool preserveResumeAfterFailure;
    private int resumeOperation; // 1: load, 2: explicit/automatic save; captured before completing the task.
    private string? undoIdentity;
    private bool showGallery;
    private int gallerySlot = 1;
    private readonly Dictionary<int, IntPtr> previewTextures = new();
    private long nextResumeAt = Environment.TickCount64 + 60_000;
    private readonly ActivePlaytimeClock playtimeClock = new();
    private readonly PlaytimeJournal playtimeJournal = new();
    private Task<IReadOnlyList<Exception>>? playtimeFlush;
    private long nextPlaytimeFlush = Environment.TickCount64 + 30_000;
    private double playedSeconds;
    private LinuxGameProfile globalProfile = null!;
    private LinuxGameProfile? pendingGameProfile;
    private bool usingGameProfile;
    private string? profileNotice;
    private sealed record StateOutcome(string Message, byte[]? Undo = null, bool ClearUndo = false);

    private bool diskRefreshAgain;
    private void RequestDiskRefresh()
    {
        diskGeneration++;
        diskRefreshAgain = storage is not null && !IsOnlineLink;
        if (diskSnapshot?.BasePath != StateBasePath) { diskSnapshot = null; ClearPreviewTextures(); }
        StartDiskRefreshIfReady();
    }
    private void StartDiskRefreshIfReady()
    {
        if (diskRefresh is not null || !diskRefreshAgain || storage is null || IsOnlineLink) return;
        diskRefreshAgain = false;
        string path = storage.StateBasePath, save = storage.SavePath;
        int length = session?.LatestSnapshot.Rom?.BatterySave.ExpectedLength ?? 0;
        requestedDiskGeneration = diskGeneration;
        diskRefresh = Task.Run(() => LinuxStateGallery.Inspect(path, save, length));
    }
    private void PollDiskRefresh()
    {
        if (diskRefresh is not { IsCompleted: true }) { StartDiskRefreshIfReady(); return; }
        var finished = diskRefresh; diskRefresh = null;
        try
        {
            var snapshot = finished.GetAwaiter().GetResult();
            if (requestedDiskGeneration == diskGeneration && snapshot.BasePath == StateBasePath)
            { diskSnapshot = snapshot; ClearPreviewTextures(); }
        }
        catch (Exception ex) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Save information unavailable: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message); }
        StartDiskRefreshIfReady();
    }
    private LinuxStateCard? StateCard(int slot) => diskSnapshot?.States.FirstOrDefault(card => card.Slot == slot);
    private void ClearPreviewTextures()
    { foreach (IntPtr texture in previewTextures.Values) SDL.DestroyTexture(texture); previewTextures.Clear(); }

    private void UpdateComfort()
    {
        PollGameplayCapture();
        UpdateControllerRumble();
        CompleteStateOperation(); PollDiskRefresh(); PollLibraryRefresh();
        long now = Environment.TickCount64;
        var snapshot = session?.LatestSnapshot;
        double elapsed = playtimeClock.Sample(now, snapshot?.State ?? SessionState.Stopped,
            snapshot?.IsPaused != false || controlCenterVisible || showQuickDeck || IsLoading || IsOnlineLink || stateOperation is not null);
        if (storage is not null && elapsed > 0)
        { playedSeconds += elapsed; playtimeJournal.Add(storage.Identity, elapsed); }
        PollPlaytimeFlush();
        if (now >= nextPlaytimeFlush && playtimeFlush is null)
        { nextPlaytimeFlush = now + 30_000; playtimeFlush = Task.Run(PersistPlaytime); }
        if (snapshot is { State: SessionState.Running, IsPaused: false } && !IsOnlineLink && stateOperation is null && !preserveResumeAfterFailure && now >= nextResumeAt)
        { nextResumeAt = now + 60_000; QueueSaveState(0); }
    }
    private IReadOnlyList<Exception> PersistPlaytime() => playtimeJournal.Flush((identity, seconds) =>
        library.Update(identity, entry => entry with
        { PlaySeconds = Math.Min(LibraryMetadata.MaximumPlaySeconds, entry.PlaySeconds + seconds) }));

    private void PollPlaytimeFlush()
    {
        if (playtimeFlush is not { IsCompleted: true }) return;
        var completed = playtimeFlush; playtimeFlush = null;
        ReportPlaytimeFailures(completed.GetAwaiter().GetResult());
    }
    private void ReportPlaytimeFailures(IReadOnlyList<Exception> failures)
    {
        foreach (var failure in failures) diagnostics.Failure("library_playtime", failure);
        if (failures.Count > 0) statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Playtime could not be saved. Will retry while AetherBoy stays open.");
    }
    private void FlushPlaytimeOnClose()
    {
        playtimeClock.Reset();
        playtimeFlush?.GetAwaiter().GetResult();
        PollPlaytimeFlush();
        ReportPlaytimeFailures(PersistPlaytime());
    }
    private void QueueSaveState(int slot)
    {
        if (IsOnlineLink) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Save states are disabled during Online Link. Use the game's save menu."); return; }
        if (session is null || storage is null || IsLoading || stateOperation is not null) return;
        EmulationSession owner = session; string path = storage.StateBasePath;
        resumeOperation = slot == 0 ? 2 : 0;
        statusMessage = slot == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("Saving a resume point…") : global::AetherBoy.Runtime.Localization.UiText.Format("Saving slot {0}…", slot);
        stateOperation = Task.Run(async () =>
        {
            byte[] state = await owner.CaptureStateAsync();
            var geometry = owner.LatestSnapshot.VideoGeometry;
            int[] pixels = new int[geometry.PixelCount]; long sequence = -1;
            if (!owner.TryCopyLatestFrame(pixels, ref sequence)) pixels = [];
            LinuxStateGallery.Write(path, slot, state, geometry.Width, geometry.Height, pixels);
            return new StateOutcome(slot == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("Resume point saved separately from manual slots.") : global::AetherBoy.Runtime.Localization.UiText.Format("Saved slot {0}.", slot));
        });
    }
    private void QueueLoadState(int slot)
    {
        if (IsOnlineLink) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Save states are disabled during Online Link."); return; }
        if (session is null || storage is null || IsLoading || stateOperation is not null) return;
        EmulationSession owner = session; string path = storage.StateBasePath;
        resumeOperation = slot == 0 ? 1 : 0;
        statusMessage = slot == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("Loading resume point…") : global::AetherBoy.Runtime.Localization.UiText.Format("Loading slot {0}…", slot);
        stateOperation = Task.Run(async () =>
        {
            byte[] target = LinuxSaveStateStore.ReadPath(LinuxStateGallery.PathFor(path, slot));
            byte[] before = await owner.CaptureStateAsync();
            await owner.RestoreStateAsync(target);
            return new StateOutcome(slot == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("Session resumed. Undo load is available.") : global::AetherBoy.Runtime.Localization.UiText.Format("Loaded slot {0}. Undo load is available.", slot), before);
        });
    }
    private void LoadResume() => QueueLoadState(0);
    private void UndoLoad()
    {
        if (IsOnlineLink) return;
        if (session is null || stateOperation is not null || IsLoading || undoState is null || undoIdentity != storage?.Identity) return;
        var owner = session; byte[] bytes = undoState;
        stateOperation = Task.Run(async () => { await owner.RestoreStateAsync(bytes); return new StateOutcome(global::AetherBoy.Runtime.Localization.UiText.Get("Previous state restored."), ClearUndo: true); });
    }
    private void CompleteStateOperation()
    {
        if (stateOperation is not { IsCompleted: true }) return;
        var completed = stateOperation; stateOperation = null;
        int completedResumeOperation = resumeOperation; resumeOperation = 0;
        try
        {
            StateOutcome result = completed.GetAwaiter().GetResult();
            if (completedResumeOperation == 2 || result.Undo is not null) preserveResumeAfterFailure = false;
            if (result.Undo is not null) { undoState = result.Undo; undoIdentity = storage?.Identity; }
            if (result.ClearUndo) { undoState = null; undoIdentity = null; }
            audioOutput?.Clear(); displayedFrameSequence = 0;
            statusMessage = result.Message;
            RequestDiskRefresh();
        }
        catch (Exception ex)
        {
            if (completedResumeOperation == 1) preserveResumeAfterFailure = true;
            statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("State operation failed: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message);
            if (preserveResumeAfterFailure) statusMessage += global::AetherBoy.Runtime.Localization.UiText.Get(" Previous resume kept; replace it explicitly in Gallery.");
            diagnostics.Failure("state_operation", ex);
        }
    }
    private void FinishStateWork()
    {
        try { stateOperation?.GetAwaiter().GetResult(); } catch { }
        CompleteStateOperation();
    }
    private void SaveResumeOnClose()
    {
        if (IsOnlineLink) return;
        FinishStateWork();
        if (session is null || storage is null || session.State is SessionState.Faulted or SessionState.Stopped) return;
        try
        {
            if (!preserveResumeAfterFailure)
            {
            byte[] state = session.CaptureStateAsync().GetAwaiter().GetResult();
            var geometry = session.LatestSnapshot.VideoGeometry; int[] pixels = new int[geometry.PixelCount]; long sequence = -1;
            if (!session.TryCopyLatestFrame(pixels, ref sequence)) pixels = [];
            LinuxStateGallery.Write(storage.StateBasePath, 0, state, geometry.Width, geometry.Height, pixels);
            }
        }
        catch (Exception ex) { diagnostics.Failure("resume_on_close", ex); statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Could not save resume point: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message); }
    }

    private string TextSizeName => options.TextSize >= 18 ? global::AetherBoy.Runtime.Localization.UiText.Get("Larger") : options.TextSize >= 16 ? global::AetherBoy.Runtime.Localization.UiText.Get("Large") : "Standard";

    private void CycleTextSize()
    { options.TextSize = options.TextSize >= 18 ? 14 : options.TextSize + 2; MarkSettingsChanged(); focusedControl = -1; }

    private void ToggleGameProfile()
    {
        if (storage is null) return;
        FlushSettingsIfDue(force: true);
        if (settingsDirty) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Save current settings before changing profile scope."); return; }
        try
        {
            var store = new LinuxProfileStore(dataPaths);
            if (usingGameProfile)
            {
                store.Delete(storage.Identity);
                globalProfile.ApplyTo(options); usingGameProfile = false;
                ApplyEmulatorConfiguration(); session?.SetPaletteAsync(options.PaletteIndex).GetAwaiter().GetResult();
                if (frameTexture != IntPtr.Zero) SDL.SetTextureScaleMode(frameTexture, options.TextureScaleMode);
            }
            else { store.Write(storage.Identity, new LinuxGameProfile()); usingGameProfile = true; }
            statusMessage = usingGameProfile ? global::AetherBoy.Runtime.Localization.UiText.Get("Changes to video, sound and keys now apply only to this game.") : global::AetherBoy.Runtime.Localization.UiText.Get("Using global video, sound and keys again.");
        }
        catch (Exception ex) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Profile change failed: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message); }
    }
    private void DrawStateGallery()
    {
        ActionButton(300, 198, 200, 42, global::AetherBoy.Runtime.Localization.UiText.Get("BACK TO SAVES"), () => { showGallery = false; focusedControl = -1; });
        ActionButton(520, 198, 200, 42, global::AetherBoy.Runtime.Localization.UiText.Get("REFRESH"), RequestDiskRefresh, enabled: diskRefresh is null);
        Ink(740, 210, stateOperation is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Select a slot to inspect it") : global::AetherBoy.Runtime.Localization.UiText.Get("Working…"), 14, Colors.Muted);
        for (int i = 0; i < 6; i++)
        {
            int slot = i;
            ActionButton(300 + i * 136, 260, 124, 40, i == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("RESUME") : $"SLOT {i}", () => gallerySlot = slot, gallerySlot == i);
        }
        LinuxStateCard? card = StateCard(gallerySlot);
        Panel(300, 324, 312, 228);
        if (card?.Pixels is not null)
        {
            if (!previewTextures.TryGetValue(gallerySlot, out IntPtr texture))
            {
                texture = SDL.CreateTexture(renderer, SDL.PixelFormat.ARGB8888, SDL.TextureAccess.Static, card.Width, card.Height);
                SDL.UpdateTexture(texture, IntPtr.Zero, MemoryMarshal.AsBytes(card.Pixels.AsSpan()).ToArray(), card.Width * 4);
                SDL.SetTextureScaleMode(texture, SDL.ScaleMode.Nearest); previewTextures[gallerySlot] = texture;
            }
            float scale = Math.Min(288f / card.Width, 204f / card.Height);
            SDL.FRect target = new() { X = 312 + (288 - card.Width * scale) / 2, Y = 336 + (204 - card.Height * scale) / 2, W = card.Width * scale, H = card.Height * scale };
            SDL.RenderTexture(renderer, texture, IntPtr.Zero, in target);
        }
        else Ink(322, 420, card is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Loading…") : card.Error is not null ? global::AetherBoy.Runtime.Localization.UiText.Get("Unavailable") : card.Exists ? global::AetherBoy.Runtime.Localization.UiText.Get("No verified preview") : global::AetherBoy.Runtime.Localization.UiText.Get("Empty slot"), 16, Colors.Muted);
        Ink(642, 330, gallerySlot == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("LAST SESSION") : global::AetherBoy.Runtime.Localization.UiText.Format("MANUAL SLOT {0}", gallerySlot), 18, Colors.Cyan, true);
        Ink(642, 370, card is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Loading save information…") : card.Error is not null ? global::AetherBoy.Runtime.Localization.UiText.Get("Cannot read this state") : !card.Exists ? global::AetherBoy.Runtime.Localization.UiText.Get("Empty slot") : card.SavedAt.ToString("dd MMM yyyy · HH:mm"), 16);
        Ink(642, 410, global::AetherBoy.Runtime.Localization.UiText.Get("Preview shows the last completed frame."), 14, Colors.Muted);
        Ink(642, 444, global::AetherBoy.Runtime.Localization.UiText.Get("Loading keeps an undo point in this session."), 14, Colors.Muted);
        ActionButton(642, 496, 205, 44, gallerySlot == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("CONTINUE") : global::AetherBoy.Runtime.Localization.UiText.Get("LOAD SLOT"), () => QueueLoadState(gallerySlot), true, card?.Exists == true && card.Error is null && stateOperation is null);
        ActionButton(865, 496, 235, 44, global::AetherBoy.Runtime.Localization.UiText.Get("UNDO LAST LOAD"), UndoLoad, enabled: undoState is not null && stateOperation is null);
        ActionButton(300, 580, 310, 42, gallerySlot == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("UPDATE RESUME POINT") : global::AetherBoy.Runtime.Localization.UiText.Get("SAVE TO THIS SLOT"), () => QueueSaveState(gallerySlot), enabled: session is not null && stateOperation is null);
        Ink(642, 590, global::AetherBoy.Runtime.Localization.UiText.Get("Resume never replaces your five manual slots."), 14, Colors.Muted);
    }
}
