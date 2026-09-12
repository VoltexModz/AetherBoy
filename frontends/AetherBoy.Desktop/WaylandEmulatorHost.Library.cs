using System.Runtime.InteropServices;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private readonly LinuxLibrary library;
    private IReadOnlyList<LinuxLibraryEntry> libraryEntries = [];
    private string librarySearch = "";
    private bool editingSearch;
    private int libraryPage;
    private bool pickingFirmware;

    private Task<(IReadOnlyList<LinuxLibraryEntry> Entries, HashSet<string> Existing, LinuxLibraryEntry? Resume)>? libraryRefresh;
    private HashSet<string> existingLibraryFiles = new();
    private LinuxLibraryEntry? lastResumeEntry;
    private string? pendingResumeIdentity;
    private string libraryFilter = "ALL";
    private bool favoritesOnly;
    private string? editingTitleIdentity;
    private string titleInput = "";
    private bool titleSelectedAll;
    private int titleEditVersion;
    private int titleSaveVersion;

    private bool libraryRefreshAgain;
    private Task? libraryMutation;
    private Action? libraryMutationCompleted;
    private void RefreshLibrary()
    {
        if (libraryRefresh is not null) { libraryRefreshAgain = true; return; }
        libraryRefresh = Task.Run(() =>
        {
            var entries = library.Read();
            var existing = entries.Where(entry => File.Exists(entry.Path)).Select(entry => entry.Identity).ToHashSet();
            var resume = entries.FirstOrDefault(entry => existing.Contains(entry.Identity) && File.Exists(Path.Combine(dataPaths.Data, "states", entry.Identity, "game.resume")));
            return (entries, existing, resume);
        });
    }
    private void PollLibraryRefresh()
    {
        if (libraryMutation is { IsCompleted: true })
        {
            var mutation = libraryMutation; libraryMutation = null;
            try { mutation.GetAwaiter().GetResult(); libraryMutationCompleted?.Invoke(); statusMessage = "Library updated."; RefreshLibrary(); }
            catch (Exception ex) { statusMessage = "Library update failed: " + ex.Message; if (editingTitleIdentity is not null && titleSaveVersion == titleEditVersion) SDL.StartTextInput(window); }
            libraryMutationCompleted = null;
        }
        if (libraryRefresh is not { IsCompleted: true }) return;
        var task = libraryRefresh; libraryRefresh = null;
        try { var result = task.GetAwaiter().GetResult(); libraryEntries = result.Entries; existingLibraryFiles = result.Existing; lastResumeEntry = result.Resume; }
        catch (Exception ex) { statusMessage = "Library could not be refreshed: " + ex.Message; }
        if (libraryRefreshAgain) { libraryRefreshAgain = false; RefreshLibrary(); }
    }
    private void ContinueLastSession()
    {
        if (lastResumeEntry is not { } entry || session is not null || IsLoading) return;
        BeginRomLoad(entry.Path, entry.Identity);
    }
    private void ChangeLibraryEntry(string identity, Func<LinuxLibraryEntry, LinuxLibraryEntry> update)
    {
        if (libraryMutation is not null) return;
        statusMessage = "Updating library…";
        libraryMutation = Task.Run(() => library.Update(identity, update));
    }
    private void EndTitleEdit(bool save)
    {
        if (editingTitleIdentity is null) return;
        if (libraryMutation is not null) return;
        if (save)
        {
            if (editorField == TextField.Title && textEditor.IsComposing) { statusMessage = "Finish composing the title before saving."; return; }
            if (string.IsNullOrWhiteSpace(titleInput)) { statusMessage = "Enter a title before saving."; return; }
            string identity = editingTitleIdentity, title = titleInput.Trim();
            int version = titleEditVersion; titleSaveVersion = version;
            libraryMutation = Task.Run(() => library.Update(identity, entry => entry with { Title = title, HasCustomTitle = true }));
            libraryMutationCompleted = () => { if (version != titleEditVersion || editingTitleIdentity != identity) return; editingTitleIdentity = null; SDL.StopTextInput(window); focusedControl = -1; };
            SDL.StopTextInput(window); statusMessage = "Saving title…";
            return;
        }
        titleEditVersion++; editingTitleIdentity = null; SDL.StopTextInput(window); focusedControl = -1; RefreshLibrary();
    }
    private void DrawLibraryPage()
    {
        if (showPatchLab) { DrawPatchLab(); return; }
        if (editingTitleIdentity is not null)
        {
            Ink(300, 208, "CARTRIDGE TITLE", 18, Colors.Cyan, true);
            Ink(300, 258, "Shift: select · Ctrl+A/C/X/V · Enter: save · Esc: cancel", 16, Colors.Muted);
            Panel(300, 308, 810, 76);
            DrawTextEntry(TextField.Title, 310, 318, 790, 56, "Cartridge title", libraryMutation is null);
            ActionButton(300, 425, 250, 44, "SAVE TITLE", () => EndTitleEdit(true), true, libraryMutation is null && !textEditor.IsComposing && !string.IsNullOrWhiteSpace(titleInput));
            ActionButton(574, 425, 250, 44, "CANCEL", () => EndTitleEdit(false), enabled: libraryMutation is null);
            ActionButton(850, 425, 250, 44, "CLEAR TITLE", () => { titleInput = ""; titleSelectedAll = false; BeginTextEditing(TextField.Title); }, enabled: libraryMutation is null && titleInput.Length > 0);
            return;
        }
        DrawTextEntry(TextField.Search, 300, 198, 540, 44, "SEARCH CARTRIDGES");
        ActionButton(860, 198, 240, 44, "CLEAR SEARCH", () => { librarySearch = ""; libraryPage = 0; });
        ActionButton(300, 254, 250, 38, "SYSTEM: " + libraryFilter, () =>
        { string[] types = ["ALL", "GB", "GBC", "GBA"]; libraryFilter = types[(Array.IndexOf(types, libraryFilter) + 1) % types.Length]; libraryPage = 0; focusedControl = -1; });
        ActionButton(570, 254, 250, 38, favoritesOnly ? "FAVORITES ONLY" : "ALL CARTRIDGES", () => { favoritesOnly = !favoritesOnly; libraryPage = 0; focusedControl = -1; }, favoritesOnly);
        ActionButton(840, 254, 260, 38, libraryRefresh is null ? "REFRESH LIBRARY" : "REFRESHING…", RefreshLibrary, enabled: libraryRefresh is null);
        var matches = libraryEntries.Where(entry => entry.Title.Contains(librarySearch, StringComparison.OrdinalIgnoreCase)
            && (libraryFilter == "ALL" || entry.System == libraryFilter) && (!favoritesOnly || entry.Favorite)).ToArray();
        libraryPage = Math.Clamp(libraryPage, 0, Math.Max(0, (matches.Length - 1) / 3));
        if (matches.Length == 0)
        {
            Ink(300, 356, libraryRefresh is not null ? "Reading your library…" : libraryEntries.Count == 0 ? "Open a cartridge to start your library." : "No matches. Try another filter or clear your search.", 16);
            Ink(300, 400, "Original files stay in place; patched results are stored in AetherBoy.", 14, Colors.Muted);
        }
        int row = 0;
        foreach (var entry in matches.Skip(libraryPage * 3).Take(3))
        {
            float y = 310 + row++ * 70;
            bool exists = existingLibraryFiles.Contains(entry.Identity);
            ActionButton(300, y, 520, 40, entry.Title, () => TryLoadRom(entry.Path), enabled: exists, focusId: "open:" + entry.Identity);
            ActionButton(840, y, 124, 40, entry.Favorite ? "STARRED" : "STAR", () => ChangeLibraryEntry(entry.Identity, item => item with { Favorite = !item.Favorite }), entry.Favorite, enabled: libraryMutation is null, focusId: "favorite:" + entry.Identity);
            ActionButton(980, y, 120, 40, "RENAME", () =>
            { titleEditVersion++; editingTitleIdentity = entry.Identity; titleInput = entry.Title; titleSelectedAll = false; BeginTextEditing(TextField.Title); }, enabled: libraryMutation is null, focusId: "rename:" + entry.Identity);
            Ink(300, y + 44, entry.System + " · " + (exists ? $"{(int)(entry.PlaySeconds / 3600)}h {(int)(entry.PlaySeconds / 60) % 60}m played · Last opened {entry.LastPlayed.ToLocalTime():dd MMM yyyy}" : "File missing — use Open / Relocate below"), 14, exists ? Colors.Muted : Colors.Danger);
        }
        ActionButton(300, 548, 160, 44, "PREVIOUS", () => { libraryPage--; focusedControl = -1; }, enabled: libraryPage > 0);
        ActionButton(476, 548, 160, 44, "NEXT", () => { libraryPage++; focusedControl = -1; }, enabled: (libraryPage + 1) * 3 < matches.Length);
        ActionButton(660, 548, 200, 44, "OPEN / RELOCATE", ShowRomDialog, true);
        ActionButton(880, 548, 220, 44, "PATCH LAB", OpenPatchLab);
        Ink(300, 606, $"{matches.Length} cartridges · Page {libraryPage + 1} of {Math.Max(1, (matches.Length + 2) / 3)} · Saves follow ROM content", 14, Colors.Muted);
    }

    private void ShowFirmwareDialog()
    {
        if (fileDialogOpen != 0 || IsLoading) return;
        pickingFirmware = true;
        ShowRomDialog();
        statusMessage = "Choose a DMG boot ROM (256 B), CGB boot ROM (2304 B), or GBA BIOS (16 KiB).";
    }

    private void ImportFirmware(string path)
    {
        long length = new FileInfo(path).Length;
        string name = length switch { 256 => "dmg_boot.bin", 2304 => "cgb_boot.bin", 16384 => "gba_bios.bin",
            _ => throw new InvalidDataException("Firmware must be 256, 2304 or 16384 bytes. No file was imported.") };
        string directory = Path.Combine(dataPaths.Data, "firmware");
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, name);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.Copy(path, temporary); File.Move(temporary, destination, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        options.UseFirmware = true;
        MarkSettingsChanged();
        statusMessage = name + " imported. It will be used the next time you open a cartridge.";
    }

    private byte[]? FirmwareFor(string path) => ReadFirmware(dataPaths, path, options.UseFirmware);

    private static byte[]? ReadFirmware(LinuxDataPaths dataPaths, string path, bool useFirmware)
    {
        if (!useFirmware) return null;
        string name;
        if (Path.GetExtension(path).Equals(".gba", StringComparison.OrdinalIgnoreCase)) name = "gba_bios.bin";
        else
        {
            using var input = File.OpenRead(path);
            input.Position = 0x143;
            int color = input.ReadByte();
            name = (color & 0x80) != 0 ? "cgb_boot.bin" : "dmg_boot.bin";
        }
        string firmware = Path.Combine(dataPaths.Data, "firmware", name);
        if (!File.Exists(firmware)) return null;
        long expected = name == "gba_bios.bin" ? 16384 : name == "cgb_boot.bin" ? 2304 : 256;
        if (new FileInfo(firmware).Length != expected) throw new InvalidDataException("The imported firmware has changed size. Import it again.");
        return File.ReadAllBytes(firmware);
    }
}
