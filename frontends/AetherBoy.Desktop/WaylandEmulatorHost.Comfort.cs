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
    private long lastPlayTick = Environment.TickCount64;
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
        diskRefreshAgain = storage is not null;
        if (diskSnapshot?.BasePath != StateBasePath) { diskSnapshot = null; ClearPreviewTextures(); }
        StartDiskRefreshIfReady();
    }
    private void StartDiskRefreshIfReady()
    {
        if (diskRefresh is not null || !diskRefreshAgain || storage is null) return;
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
        catch (Exception ex) { statusMessage = "Save information unavailable: " + ex.Message; }
        StartDiskRefreshIfReady();
    }
    private LinuxStateCard? StateCard(int slot) => diskSnapshot?.States.FirstOrDefault(card => card.Slot == slot);
    private void ClearPreviewTextures()
    { foreach (IntPtr texture in previewTextures.Values) SDL.DestroyTexture(texture); previewTextures.Clear(); }

    private void UpdateComfort()
    {
        CompleteStateOperation(); PollDiskRefresh(); PollLibraryRefresh();
        long now = Environment.TickCount64;
        if (session is not null && !session.LatestSnapshot.IsPaused && !controlCenterVisible && !IsLoading)
            playedSeconds += Math.Clamp((now - lastPlayTick) / 1000d, 0, 1);
        lastPlayTick = now;
        if (session is not null && !session.LatestSnapshot.IsPaused && stateOperation is null && !preserveResumeAfterFailure && now >= nextResumeAt)
        { nextResumeAt = now + 60_000; QueueSaveState(0); }
    }
    private void QueueSaveState(int slot)
    {
        if (session is null || storage is null || IsLoading || stateOperation is not null) return;
        EmulationSession owner = session; string path = storage.StateBasePath;
        resumeOperation = slot == 0 ? 2 : 0;
        statusMessage = slot == 0 ? "Saving a resume point…" : $"Saving slot {slot}…";
        stateOperation = Task.Run(async () =>
        {
            byte[] state = await owner.CaptureStateAsync();
            var geometry = owner.LatestSnapshot.VideoGeometry;
            int[] pixels = new int[geometry.PixelCount]; long sequence = -1;
            if (!owner.TryCopyLatestFrame(pixels, ref sequence)) pixels = [];
            LinuxStateGallery.Write(path, slot, state, geometry.Width, geometry.Height, pixels);
            return new StateOutcome(slot == 0 ? "Resume point saved separately from manual slots." : $"Saved slot {slot}.");
        });
    }
    private void QueueLoadState(int slot)
    {
        if (session is null || storage is null || IsLoading || stateOperation is not null) return;
        EmulationSession owner = session; string path = storage.StateBasePath;
        resumeOperation = slot == 0 ? 1 : 0;
        statusMessage = slot == 0 ? "Loading resume point…" : $"Loading slot {slot}…";
        stateOperation = Task.Run(async () =>
        {
            byte[] target = LinuxSaveStateStore.ReadPath(LinuxStateGallery.PathFor(path, slot));
            byte[] before = await owner.CaptureStateAsync();
            await owner.RestoreStateAsync(target);
            return new StateOutcome(slot == 0 ? "Session resumed. Undo load is available." : $"Loaded slot {slot}. Undo load is available.", before);
        });
    }
    private void LoadResume() => QueueLoadState(0);
    private void UndoLoad()
    {
        if (session is null || stateOperation is not null || IsLoading || undoState is null || undoIdentity != storage?.Identity) return;
        var owner = session; byte[] bytes = undoState;
        stateOperation = Task.Run(async () => { await owner.RestoreStateAsync(bytes); return new StateOutcome("Previous state restored.", ClearUndo: true); });
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
            statusMessage = "State operation failed: " + ex.Message;
            if (preserveResumeAfterFailure) statusMessage += " Previous resume kept; replace it explicitly in Gallery.";
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
            if (playedSeconds > 0) library.Update(storage.Identity, item => item with { PlaySeconds = item.PlaySeconds + playedSeconds });
        }
        catch (Exception ex) { diagnostics.Failure("resume_on_close", ex); statusMessage = "Could not save resume point: " + ex.Message; }
    }

    private string TextSizeName => options.TextSize >= 18 ? "LARGER" : options.TextSize >= 16 ? "LARGE" : "STANDARD";

    private void CycleTextSize()
    { options.TextSize = options.TextSize >= 18 ? 14 : options.TextSize + 2; MarkSettingsChanged(); focusedControl = -1; }

    private void ToggleGameProfile()
    {
        if (storage is null) return;
        FlushSettingsIfDue(force: true);
        if (settingsDirty) { statusMessage = "Save current settings before changing profile scope."; return; }
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
            statusMessage = usingGameProfile ? "Changes to video, sound and keys now apply only to this game." : "Using global video, sound and keys again.";
        }
        catch (Exception ex) { statusMessage = "Profile change failed: " + ex.Message; }
    }
    private void DrawStateGallery()
    {
        ActionButton(300, 198, 200, 42, "BACK TO SAVES", () => { showGallery = false; focusedControl = -1; });
        ActionButton(520, 198, 200, 42, "REFRESH", RequestDiskRefresh, enabled: diskRefresh is null);
        Ink(740, 210, stateOperation is null ? "Select a slot to inspect it" : "Working…", 14, Colors.Muted);
        for (int i = 0; i < 6; i++)
        {
            int slot = i;
            ActionButton(300 + i * 136, 260, 124, 40, i == 0 ? "RESUME" : $"SLOT {i}", () => gallerySlot = slot, gallerySlot == i);
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
        else Ink(322, 420, card is null ? "Loading…" : card.Error is not null ? "Unavailable" : card.Exists ? "No verified preview" : "Empty slot", 16, Colors.Muted);
        Ink(642, 330, gallerySlot == 0 ? "LAST SESSION" : $"MANUAL SLOT {gallerySlot}", 18, Colors.Cyan, true);
        Ink(642, 370, card is null ? "Loading save information…" : card.Error is not null ? "Cannot read this state" : !card.Exists ? "Empty slot" : card.SavedAt.ToString("dd MMM yyyy · HH:mm"), 16);
        Ink(642, 410, "Preview shows the last completed frame.", 14, Colors.Muted);
        Ink(642, 444, "Loading keeps an undo point in this session.", 14, Colors.Muted);
        ActionButton(642, 496, 205, 44, gallerySlot == 0 ? "CONTINUE" : "LOAD SLOT", () => QueueLoadState(gallerySlot), true, card?.Exists == true && card.Error is null && stateOperation is null);
        ActionButton(865, 496, 235, 44, "UNDO LAST LOAD", UndoLoad, enabled: undoState is not null && stateOperation is null);
        ActionButton(300, 580, 310, 42, gallerySlot == 0 ? "UPDATE RESUME POINT" : "SAVE TO THIS SLOT", () => QueueSaveState(gallerySlot), enabled: session is not null && stateOperation is null);
        Ink(642, 590, "Resume never replaces your five manual slots.", 14, Colors.Muted);
    }
}
