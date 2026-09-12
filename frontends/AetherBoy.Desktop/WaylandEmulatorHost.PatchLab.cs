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
    private string patchMessage = "Choose a source ROM and a patch. Your original and its saves stay unchanged.";
    private bool patchFailed;

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
        statusMessage = selection == PatchSelection.Source ? "Choose the source .gb, .gbc or .gba cartridge." : "Choose an IPS, BPS or UPS patch.";
    }

    private void SelectPatchFile(string path, PatchSelection selection)
    {
        if (pendingPatch is not null) return;
        patchResult = null;
        try
        {
            if (selection == PatchSelection.Source && !LinuxRomPatchService.IsRomPath(path))
                throw new InvalidDataException("Source must be a .gb, .gbc or .gba ROM.");
            if (selection == PatchSelection.Patch && !LinuxRomPatchService.IsPatchPath(path))
                throw new InvalidDataException("Patch must be .ips, .bps or .ups. Extract ZIP files first.");
            if (selection == PatchSelection.Source) patchSourcePath = Path.GetFullPath(path);
            else { patchFilePath = Path.GetFullPath(path); reverseUps = false; }
            patchResult = null;
            patchFailed = false;
            patchMessage = "Ready when both files are selected. Patched games use their own saves.";
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidDataException)
        { patchFailed = true; patchMessage = ex.Message; }
    }

    private void StartPatch()
    {
        if (pendingPatch is not null || fileDialogOpen != 0 || patchSourcePath is null || patchFilePath is null) return;
        string source = patchSourcePath, patch = patchFilePath;
        bool reverse = reverseUps;
        patchResult = null;
        patchFailed = false;
        patchMessage = "Applying patch and checking the result…";
        pendingPatch = Task.Run(() => new LinuxRomPatchService(dataPaths).ApplyAndImport(source, patch, reverse));
    }

    private void CompletePendingPatch()
    {
        if (pendingPatch is not { IsCompleted: true }) return;
        var completed = pendingPatch;
        pendingPatch = null;
        try
        {
            patchResult = completed.GetAwaiter().GetResult();
            patchMessage = patchResult.Warning ?? (patchResult.Reused ? "Existing matching cartridge kept. " : "Result saved to your library. ")
                + (patchResult.ChecksumsVerified ? "CRC32 checks passed." : "IPS has no checksums; verify that you chose the correct base ROM.");
            patchFailed = false;
            try { RefreshLibrary(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { patchMessage += " Library refresh failed; the result is still available below."; }
            statusMessage = "Patch Lab · Result ready.";
            diagnostics.Record("rom_patched", new { format = patchResult.Format, reversed = patchResult.Reversed, reused = patchResult.Reused });
        }
        catch (Exception ex)
        {
            patchFailed = true;
            patchMessage = "Patch failed: " + ex.Message;
            statusMessage = "Patch Lab · Could not apply patch.";
            diagnostics.Record("patch_failed", new { error = ex.GetType().Name });
        }
    }

    private void DrawPatchLab()
    {
        CompletePendingPatch();
        bool available = pendingPatch is null && fileDialogOpen == 0 && !IsLoading;
        Ink(300, 198, "PATCH LAB · IPS / BPS / UPS", 16, Colors.Cyan, true);
        ActionButton(910, 190, 190, 40, "BACK TO LIBRARY", () => { showPatchLab = false; focusedControl = -1; });
        ActionButton(300, 244, 600, 44, patchSourcePath is null ? "1 · CHOOSE SOURCE ROM" : "SOURCE · " + Path.GetFileName(patchSourcePath),
            () => ShowPatchDialog(PatchSelection.Source), enabled: available);
        ActionButton(920, 244, 190, 44, "USE CURRENT GAME", () => SelectPatchFile(romPath!, PatchSelection.Source), enabled: available && romPath is not null);
        ActionButton(300, 308, 810, 44, patchFilePath is null ? "2 · CHOOSE PATCH FILE" : "PATCH · " + Path.GetFileName(patchFilePath),
            () => ShowPatchDialog(PatchSelection.Patch), enabled: available);
        ActionButton(300, 372, 340, 40, reverseUps ? "UPS DIRECTION: RESTORE ORIGINAL" : "UPS DIRECTION: APPLY PATCH", () =>
        { reverseUps = !reverseUps; patchResult = null; patchMessage = reverseUps ? "Choose the patched ROM as source to restore its original." : "Choose the original ROM as source to apply this patch."; patchFailed = false; },
            reverseUps, available && Path.GetExtension(patchFilePath ?? "").Equals(".ups", StringComparison.OrdinalIgnoreCase));
        Ink(664, 384, "Undo requires the same UPS patch.", 14, Colors.Muted);
        Ink(300, 430, "Drop files here, or choose them above. IPS cannot verify the base ROM.", 14, Colors.Muted);
        ActionButton(300, 460, 250, 44, pendingPatch is not null ? "WORKING…" : reverseUps ? "RESTORE ORIGINAL" : "APPLY PATCH", StartPatch,
            true, available && patchSourcePath is not null && patchFilePath is not null);
        ActionButton(570, 460, 250, 44, "OPEN RESULT FOLDER", () => OpenFolder(Path.GetDirectoryName(patchResult!.Path)!), enabled: patchResult is not null);
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
        ActionButton(300, 582, 250, 40, "OPEN RESULT", () => TryLoadRom(patchResult!.Path), enabled: patchResult is not null && !IsLoading);
        ActionButton(570, 582, 250, 40, "CLEAR SELECTION", () =>
        { patchSourcePath = patchFilePath = null; patchResult = null; reverseUps = patchFailed = false; patchMessage = "Choose a source ROM and a patch."; }, enabled: available);
    }
}
