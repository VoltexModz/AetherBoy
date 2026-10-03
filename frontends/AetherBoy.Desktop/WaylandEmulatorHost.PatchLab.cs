using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private enum PatchSelection { None, Source, Patch }
    private bool showPatchLab;
    private PatchSelection pickingPatch;
    private string? patchSourcePath;
    private string? patchFilePath;
    private bool reverseUps;
    private Task<LinuxPatchedRom>? pendingPatch;
    private LinuxPatchedRom? patchResult;
    private string patchMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Choose a source ROM and a patch. Your original and its saves stay unchanged.");
    private bool patchFailed;
    private bool pendingPatchLaunch;
    private AetherBoy.Runtime.EmulationSession? patchLaunchSession;

    private void OpenPatchLab()
    {
        editingSearch = editingCheat = false;
        SDL.StopTextInput(window);
        focusedControl = -1;
        showPatchLab = true;
        if (patchSourcePath is null && romPath is not null) patchSourcePath = romPath;
    }

    private void ShowPatchDialog(PatchSelection selection)
    {
        if (fileDialogOpen != 0 || IsLoading || pendingPatch is not null) return;
        pickingPatch = selection;
        ShowRomDialog();
        statusMessage = selection == PatchSelection.Source ? global::AetherBoy.Runtime.Localization.UiText.Get("Choose the source .gb, .gbc or .gba cartridge.") : global::AetherBoy.Runtime.Localization.UiText.Get("Choose an IPS, BPS or UPS patch.");
    }

    private void SelectPatchFile(string path, PatchSelection selection)
    {
        if (pendingPatch is not null) return;
        patchResult = null;
        try
        {
            if (selection == PatchSelection.Source && !LinuxRomPatchService.IsRomPath(path))
                throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Source must be a .gb, .gbc or .gba ROM."));
            if (selection == PatchSelection.Patch && !LinuxRomPatchService.IsPatchPath(path))
                throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Patch must be .ips, .bps or .ups. Extract ZIP files first."));
            if (selection == PatchSelection.Source) patchSourcePath = Path.GetFullPath(path);
            else { patchFilePath = Path.GetFullPath(path); reverseUps = false; }
            patchResult = null;
            patchFailed = false;
            patchMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Ready when both files are selected. Patched games use their own saves.");
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidDataException)
        { patchFailed = true; patchMessage = ex.Message; }
    }

    private void StartPatch() => BeginPatch(false);
    private void StartPatchAndPlay() => BeginPatch(true);
    private void BeginPatch(bool launch)
    {
        if (pendingPatch is not null || fileDialogOpen != 0 || patchSourcePath is null || patchFilePath is null || IsLoading) return;
        if (launch && (IsOnlineLink || showLocalLinkPage || localLinkSession is not null || stateOperation is not null))
        { patchMessage = global::AetherBoy.Runtime.Localization.UiText.Get("End the link session or pending operation before starting a patched game."); return; }
        pendingPatchLaunch = launch; patchLaunchSession = session;
        string source = patchSourcePath, patch = patchFilePath;
        bool reverse = reverseUps;
        patchResult = null;
        patchFailed = false;
        patchMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Applying patch and checking the result…");
        pendingPatch = Task.Run(() => new LinuxRomPatchService(dataPaths).ApplyAndImport(source, patch, reverse));
    }

    private void CompletePendingPatch()
    {
        if (pendingPatch is not { IsCompleted: true }) return;
        var completed = pendingPatch;
        pendingPatch = null;
        bool launch = pendingPatchLaunch && controlCenterVisible && controlCenterPage == ControlCenterPage.Library && showPatchLab
            && ReferenceEquals(session, patchLaunchSession) && !IsLoading && !IsOnlineLink && !showLocalLinkPage
            && localLinkSession is null && localLinkStartupTask is null && stateOperation is null;
        pendingPatchLaunch = false; patchLaunchSession = null;
        try
        {
            patchResult = completed.GetAwaiter().GetResult();
            patchMessage = patchResult.Warning ?? (patchResult.Reused ? global::AetherBoy.Runtime.Localization.UiText.Get("Existing matching cartridge kept. ") : global::AetherBoy.Runtime.Localization.UiText.Get("Result saved to your library. "))
                + (patchResult.ChecksumsVerified ? global::AetherBoy.Runtime.Localization.UiText.Get("CRC32 checks passed.") : global::AetherBoy.Runtime.Localization.UiText.Get("IPS has no checksums; verify that you chose the correct base ROM."));
            patchFailed = false;
            try { RefreshLibrary(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { patchMessage += global::AetherBoy.Runtime.Localization.UiText.Get(" Library refresh failed; the result is still available below."); }
            statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Patch Lab · Result ready.");
            diagnostics.Record("rom_patched", new { format = patchResult.Format, reversed = patchResult.Reversed, reused = patchResult.Reused });
            if (launch) { CloseControlCenter(); TryLoadRom(patchResult.Path); }
        }
        catch (Exception ex)
        {
            patchFailed = true;
            patchMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Patch failed: ") + ex.Message;
            statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Patch Lab · Could not apply patch.");
            diagnostics.Record("patch_failed", new { error = ex.GetType().Name });
        }
    }

    private void DrawPatchLab()
    {
        CompletePendingPatch();
        bool available = pendingPatch is null && fileDialogOpen == 0 && !IsLoading;
        Ink(300, 198, "PATCH LAB · IPS / BPS / UPS", 16, Colors.Cyan, true);
        ActionButton(830, 190, 280, 40, global::AetherBoy.Runtime.Localization.UiText.Get("BACK TO LIBRARY"), () => { pendingPatchLaunch = false; showPatchLab = false; focusedControl = -1; });
        ActionButton(300, 244, 510, 44, patchSourcePath is null ? global::AetherBoy.Runtime.Localization.UiText.Get("1 · CHOOSE SOURCE ROM") : global::AetherBoy.Runtime.Localization.UiText.Get("SOURCE · ") + Path.GetFileName(patchSourcePath),
            () => ShowPatchDialog(PatchSelection.Source), enabled: available);
        ActionButton(830, 244, 280, 44, global::AetherBoy.Runtime.Localization.UiText.Get("USE CURRENT GAME"), () => SelectPatchFile(romPath!, PatchSelection.Source), enabled: available && romPath is not null);
        ActionButton(300, 308, 810, 44, patchFilePath is null ? global::AetherBoy.Runtime.Localization.UiText.Get("2 · CHOOSE PATCH FILE") : "PATCH · " + Path.GetFileName(patchFilePath),
            () => ShowPatchDialog(PatchSelection.Patch), enabled: available);
        ActionButton(300, 372, 340, 40, reverseUps ? global::AetherBoy.Runtime.Localization.UiText.Get("UPS DIRECTION: RESTORE ORIGINAL") : global::AetherBoy.Runtime.Localization.UiText.Get("UPS DIRECTION: APPLY PATCH"), () =>
        { reverseUps = !reverseUps; patchResult = null; patchMessage = reverseUps ? global::AetherBoy.Runtime.Localization.UiText.Get("Choose the patched ROM as source to restore its original.") : global::AetherBoy.Runtime.Localization.UiText.Get("Choose the original ROM as source to apply this patch."); patchFailed = false; },
            reverseUps, available && Path.GetExtension(patchFilePath ?? "").Equals(".ups", StringComparison.OrdinalIgnoreCase));
        Ink(664, 384, global::AetherBoy.Runtime.Localization.UiText.Get("Undo requires the same UPS patch."), 14, Colors.Muted);
        Ink(300, 430, global::AetherBoy.Runtime.Localization.UiText.Get("Drop files here, or choose them above. IPS cannot verify the base ROM."), 14, Colors.Muted);
        ActionButton(300, 460, 250, 44, pendingPatch is not null ? global::AetherBoy.Runtime.Localization.UiText.Get("WORKING…") : reverseUps ? global::AetherBoy.Runtime.Localization.UiText.Get("RESTORE ORIGINAL") : global::AetherBoy.Runtime.Localization.UiText.Get("APPLY PATCH"), StartPatch,
            true, available && patchSourcePath is not null && patchFilePath is not null);
        ActionButton(570, 460, 250, 44, global::AetherBoy.Runtime.Localization.UiText.Get("OPEN RESULT FOLDER"), () => OpenFolder(Path.GetDirectoryName(patchResult!.Path)!), enabled: patchResult is not null);
        ActionButton(840, 460, 270, 44, reverseUps ? global::AetherBoy.Runtime.Localization.UiText.Get("Restore and play") : global::AetherBoy.Runtime.Localization.UiText.Get("Patch and play"), StartPatchAndPlay,
            enabled: available && patchSourcePath is not null && patchFilePath is not null && !IsOnlineLink && localLinkSession is null);
        // Keep errors beside the operation and wrap them instead of hiding the cause in a toast.
        string remaining = string.Concat(patchMessage.Select(c => char.IsControl(c) ? ' ' : c));
        for (int row = 0; row < 3 && remaining.Length > 0; row++)
        {
            int count = remaining.Length;
            while (count > 1 && textRenderer.Fit(remaining[..count], 810) != remaining[..count]) count--;
            if (count < remaining.Length && remaining.LastIndexOf(' ', count - 1, count) is int space && space > 0) count = space;
            Ink(300, 520 + row * 19, remaining[..count], 14, patchFailed ? Colors.Danger : Colors.Muted);
            remaining = remaining[count..].TrimStart();
        }
        ActionButton(300, 582, 250, 40, global::AetherBoy.Runtime.Localization.UiText.Get("OPEN RESULT"), () => TryLoadRom(patchResult!.Path), enabled: patchResult is not null && !IsLoading);
        ActionButton(570, 582, 250, 40, global::AetherBoy.Runtime.Localization.UiText.Get("CLEAR SELECTION"), () =>
        { patchSourcePath = patchFilePath = null; patchResult = null; reverseUps = patchFailed = false; patchMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Choose a source ROM and a patch."); }, enabled: available);
    }
}
