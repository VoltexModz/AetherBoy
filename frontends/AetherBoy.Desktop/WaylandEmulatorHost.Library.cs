using System.Runtime.InteropServices;
using AetherBoy.Runtime;
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

    private Task<(IReadOnlyList<LinuxLibraryEntry> Entries, HashSet<string> Existing, LinuxLibraryEntry? Resume, string? Warning)>? libraryRefresh;
    private string? libraryWarning;
    private HashSet<string> existingLibraryFiles = new();
    private LinuxLibraryEntry? lastResumeEntry;
    private string? pendingResumeIdentity;
    private string libraryFilter = "ALL";
    private bool favoritesOnly;
    private string? editingTitleIdentity;
    private string titleInput = "";
    private string tagsInput = "";
    private string editGenre = "";
    private int editRating;
    private string libraryGenreFilter = "";
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
            var entries = library.Read(out string? warning);
            var existing = entries.Where(entry => File.Exists(entry.Path)).Select(entry => entry.Identity).ToHashSet();
            var resume = entries.FirstOrDefault(entry => existing.Contains(entry.Identity) && File.Exists(Path.Combine(dataPaths.Data, "states", entry.Identity, "game.resume")));
            return (entries, existing, resume, warning);
        });
    }
    private void PollLibraryRefresh()
    {
        if (libraryMutation is { IsCompleted: true })
        {
            var mutation = libraryMutation; libraryMutation = null;
            try { mutation.GetAwaiter().GetResult(); libraryMutationCompleted?.Invoke(); statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Library updated."); RefreshLibrary(); }
            catch (Exception ex) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Library update failed: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message); if (showSofaLibrary) sofaNotice = global::AetherBoy.Runtime.Localization.UiText.Get("Your selection could not be saved. Check write access to the data folder."); if (editingTitleIdentity is not null && titleSaveVersion == titleEditVersion) SDL.StartTextInput(window); }
            libraryMutationCompleted = null;
        }
        if (libraryRefresh is not { IsCompleted: true }) return;
        var task = libraryRefresh; libraryRefresh = null;
        try { var result = task.GetAwaiter().GetResult(); libraryEntries = result.Entries; existingLibraryFiles = result.Existing; lastResumeEntry = result.Resume; libraryWarning = result.Warning; }
        catch (Exception ex) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Library could not be refreshed: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message); }
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
        statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Updating library…");
        libraryMutation = Task.Run(() => library.Update(identity, update));
    }
    private void EndTitleEdit(bool save)
    {
        if (editingTitleIdentity is null) return;
        if (libraryMutation is not null) return;
        if (save)
        {
            if (textEditor.IsComposing) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Finish composing text before saving."); return; }
            if (string.IsNullOrWhiteSpace(titleInput)) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Enter a title before saving."); return; }
            string[] tags;
            try { tags = LibraryMetadata.ParseTags(tagsInput); }
            catch (InvalidDataException) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Use at most eight tags, each up to 24 characters."); return; }
            string identity = editingTitleIdentity, title = titleInput.Trim();
            string genre = editGenre;
            int rating = editRating;
            int version = titleEditVersion; titleSaveVersion = version;
            libraryMutation = Task.Run(() => library.Update(identity, entry => entry with
            { Title = title, HasCustomTitle = true, Tags = tags, Genre = genre, Rating = rating }));
            libraryMutationCompleted = () => { if (version != titleEditVersion || editingTitleIdentity != identity) return; editingTitleIdentity = null; SDL.StopTextInput(window); focusedControl = -1; };
            SDL.StopTextInput(window); statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Saving title…");
            return;
        }
        titleEditVersion++; editingTitleIdentity = null; SDL.StopTextInput(window); focusedControl = -1; RefreshLibrary();
    }
    private void BeginLibraryEdit(LinuxLibraryEntry entry)
    {
        titleEditVersion++;
        editingTitleIdentity = entry.Identity;
        titleInput = entry.Title;
        tagsInput = string.Join(", ", entry.Tags ?? []);
        editGenre = entry.Genre;
        editRating = entry.Rating;
        titleSelectedAll = false;
        BeginTextEditing(TextField.Title);
    }
    private void DrawLibraryPage()
    {
        if (showPatchLab) { DrawPatchLab(); return; }
        if (editingTitleIdentity is not null)
        {
            Ink(300, 208, global::AetherBoy.Runtime.Localization.UiText.Get("CARTRIDGE DETAILS"), 18, Colors.Cyan, true);
            Ink(300, 242, global::AetherBoy.Runtime.Localization.UiText.Get("Choose a title, genre, rating and your own tags for this library only."), 15, Colors.Muted);
            DrawTextEntry(TextField.Title, 300, 280, 810, 48, global::AetherBoy.Runtime.Localization.UiText.Get("Cartridge title"), libraryMutation is null);
            Ink(300, 343, global::AetherBoy.Runtime.Localization.UiText.Get("TAGS · SEPARATE WITH COMMAS"), 15, Colors.Muted);
            DrawTextEntry(TextField.Tags, 300, 371, 810, 48, global::AetherBoy.Runtime.Localization.UiText.Get("Add up to eight tags"), libraryMutation is null);
            ActionButton(300, 450, 390, 44, global::AetherBoy.Runtime.Localization.UiText.Get("GENRE: ") + global::AetherBoy.Runtime.Localization.UiLabels.Genre(editGenre), () =>
            { int index = Array.IndexOf(LibraryMetadata.Genres, editGenre); editGenre = LibraryMetadata.Genres[(index + 1) % LibraryMetadata.Genres.Length]; }, enabled: libraryMutation is null);
            ActionButton(714, 450, 386, 44, global::AetherBoy.Runtime.Localization.UiText.Get("RATING: ") + (editRating == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("NONE") : editRating + " / 5"), () => editRating = (editRating + 1) % 6,
                enabled: libraryMutation is null);
            Ink(300, 522, global::AetherBoy.Runtime.Localization.UiText.Get("The ROM and save file are not changed."), 14, Colors.Muted);
            ActionButton(300, 564, 250, 44, global::AetherBoy.Runtime.Localization.UiText.Get("SAVE DETAILS"), () => EndTitleEdit(true), true, libraryMutation is null && !textEditor.IsComposing && !string.IsNullOrWhiteSpace(titleInput));
            ActionButton(574, 564, 250, 44, global::AetherBoy.Runtime.Localization.UiText.Get("CANCEL"), () => EndTitleEdit(false), enabled: libraryMutation is null);
            return;
        }
        DrawTextEntry(TextField.Search, 300, 198, 540, 44, global::AetherBoy.Runtime.Localization.UiText.Get("SEARCH CARTRIDGES"));
        ActionButton(860, 198, 240, 44, global::AetherBoy.Runtime.Localization.UiText.Get("CLEAR SEARCH"), () => { librarySearch = ""; libraryPage = 0; });
        ActionButton(300, 254, 250, 38, global::AetherBoy.Runtime.Localization.UiText.Get("SYSTEM: ") + (libraryFilter == "ALL" ? global::AetherBoy.Runtime.Localization.UiText.Get("Alle") : libraryFilter), () =>
        { string[] types = ["ALL", "GB", "GBC", "GBA"]; libraryFilter = types[(Array.IndexOf(types, libraryFilter) + 1) % types.Length]; libraryPage = 0; focusedControl = -1; });
        ActionButton(570, 254, 250, 38, favoritesOnly ? global::AetherBoy.Runtime.Localization.UiText.Get("FAVORITES ONLY") : global::AetherBoy.Runtime.Localization.UiText.Get("ALL CARTRIDGES"), () => { favoritesOnly = !favoritesOnly; libraryPage = 0; focusedControl = -1; }, favoritesOnly);
        ActionButton(840, 254, 260, 38, libraryRefresh is null ? global::AetherBoy.Runtime.Localization.UiText.Get("REFRESH LIBRARY") : global::AetherBoy.Runtime.Localization.UiText.Get("REFRESHING…"), RefreshLibrary, enabled: libraryRefresh is null);
        ActionButton(300, 298, 250, 34, global::AetherBoy.Runtime.Localization.UiText.Get("SORT: ") + global::AetherBoy.Runtime.Localization.UiLabels.LibrarySort(librarySort.ToString()), () =>
        { librarySort = (LinuxLibrarySort)(((int)librarySort + 1) % 7); libraryPage = 0; focusedControl = -1; });
        ActionButton(570, 298, 250, 34, libraryGrid ? global::AetherBoy.Runtime.Localization.UiText.Get("GRID VIEW") : global::AetherBoy.Runtime.Localization.UiText.Get("LIST VIEW"), () =>
        { libraryGrid = !libraryGrid; libraryPage = 0; focusedControl = -1; });
        ActionButton(840, 298, 260, 34, global::AetherBoy.Runtime.Localization.UiText.Get("GENRE: ") + (libraryGenreFilter.Length == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("ANY") : global::AetherBoy.Runtime.Localization.UiLabels.Genre(libraryGenreFilter)), () =>
        { int index = Array.IndexOf(LibraryMetadata.Genres, libraryGenreFilter); libraryGenreFilter = LibraryMetadata.Genres[(index + 1) % LibraryMetadata.Genres.Length]; libraryPage = 0; focusedControl = -1; });
        var matches = LinuxLibraryView.Select(libraryEntries, librarySearch, libraryFilter, favoritesOnly, librarySort, libraryGenreFilter);
        int pageSize = libraryGrid ? 4 : 3;
        libraryPage = Math.Clamp(libraryPage, 0, Math.Max(0, (matches.Length - 1) / pageSize));
        if (matches.Length == 0)
        {
            Ink(300, 356, libraryRefresh is not null ? global::AetherBoy.Runtime.Localization.UiText.Get("Reading your library…") : libraryEntries.Count == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("Open a cartridge to start your library.") : global::AetherBoy.Runtime.Localization.UiText.Get("No matches. Try another filter or clear your search."), 16);
            Ink(300, 400, global::AetherBoy.Runtime.Localization.UiText.Get("Original files stay in place; patched results are stored in AetherBoy."), 14, Colors.Muted);
        }
        int row = 0;
        foreach (var entry in matches.Skip(libraryPage * pageSize).Take(pageSize))
        {
            int index = row++;
            float x = libraryGrid ? 300 + (index % 2) * 405 : 300;
            float y = libraryGrid ? 342 + (index / 2) * 100 : 342 + index * 64;
            bool exists = existingLibraryFiles.Contains(entry.Identity);
            if (libraryGrid)
            {
                DrawLibraryPreview(entry.Identity, x, y, 82, 78);
                ActionButton(x + 88, y, 198, 38, entry.Title, () => TryLoadRom(entry.Path), enabled: exists, focusId: "open:" + entry.Identity);
                ActionButton(x + 292, y, 96, 38, entry.Favorite ? global::AetherBoy.Runtime.Localization.UiText.Get("STARRED") : global::AetherBoy.Runtime.Localization.UiText.Get("STAR"), () => ChangeLibraryEntry(entry.Identity, item => item with { Favorite = !item.Favorite }), entry.Favorite, enabled: libraryMutation is null, focusId: "favorite:" + entry.Identity);
                ActionButton(x + 88, y + 42, 116, 34, global::AetherBoy.Runtime.Localization.UiText.Get("DETAILS"), () => BeginLibraryEdit(entry), enabled: libraryMutation is null, focusId: "rename:" + entry.Identity);
                Ink(x + 210, y + 51, entry.System + (exists ? global::AetherBoy.Runtime.Localization.UiText.Format(": {0}m played", (int)(entry.PlaySeconds / 60)) : global::AetherBoy.Runtime.Localization.UiText.Get(": File missing")), 12, exists ? Colors.Muted : Colors.Danger);
            }
            else
            {
                ActionButton(x, y, 520, 38, entry.Title, () => TryLoadRom(entry.Path), enabled: exists, focusId: "open:" + entry.Identity);
                ActionButton(840, y, 124, 38, entry.Favorite ? global::AetherBoy.Runtime.Localization.UiText.Get("STARRED") : global::AetherBoy.Runtime.Localization.UiText.Get("STAR"), () => ChangeLibraryEntry(entry.Identity, item => item with { Favorite = !item.Favorite }), entry.Favorite, enabled: libraryMutation is null, focusId: "favorite:" + entry.Identity);
                ActionButton(980, y, 120, 38, global::AetherBoy.Runtime.Localization.UiText.Get("DETAILS"), () => BeginLibraryEdit(entry), enabled: libraryMutation is null, focusId: "rename:" + entry.Identity);
                Ink(x, y + 40, entry.System + ": " + (exists ? global::AetherBoy.Runtime.Localization.UiText.Format("Played {0}h {1}m. Last opened {2:dd MMM yyyy}.", (int)(entry.PlaySeconds / 3600), (int)(entry.PlaySeconds / 60) % 60, entry.LastPlayed.ToLocalTime()) : global::AetherBoy.Runtime.Localization.UiText.Get("File missing. Use Open / Relocate below.")), 13, exists ? Colors.Muted : Colors.Danger);
            }
        }
        ActionButton(300, 548, 160, 44, global::AetherBoy.Runtime.Localization.UiText.Get("PREVIOUS"), () => { libraryPage--; focusedControl = -1; }, enabled: libraryPage > 0);
        ActionButton(476, 548, 160, 44, global::AetherBoy.Runtime.Localization.UiText.Get("NEXT"), () => { libraryPage++; focusedControl = -1; }, enabled: (libraryPage + 1) * pageSize < matches.Length);
        ActionButton(660, 548, 200, 44, global::AetherBoy.Runtime.Localization.UiText.Get("OPEN / RELOCATE"), ShowRomDialog, true);
        ActionButton(880, 548, 220, 44, "PATCH LAB", OpenPatchLab);
        Ink(300, 606, libraryWarning ?? global::AetherBoy.Runtime.Localization.UiText.Format("{0} games, page {1} of {2}. Saves are tied to game content.", matches.Length, libraryPage + 1, Math.Max(1, (matches.Length + pageSize - 1) / pageSize)), 14, libraryWarning is null ? Colors.Muted : Colors.Danger);
    }

    private void ShowFirmwareDialog()
    {
        if (fileDialogOpen != 0 || IsLoading) return;
        pickingFirmware = true;
        ShowRomDialog();
        statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Choose a DMG boot ROM (256 B), CGB boot ROM (2304 B), or GBA BIOS (16 KiB).");
    }

    private void ImportFirmware(string path)
    {
        long length = new FileInfo(path).Length;
        string name = length switch { 256 => "dmg_boot.bin", 2304 => "cgb_boot.bin", 16384 => "gba_bios.bin",
            _ => throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Firmware must be 256, 2304 or 16384 bytes. No file was imported.")) };
        string directory = Path.Combine(dataPaths.Data, "firmware");
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, name);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.Copy(path, temporary); File.Move(temporary, destination, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        options.UseFirmware = true;
        MarkSettingsChanged();
        statusMessage = name + global::AetherBoy.Runtime.Localization.UiText.Get(" imported. It will be used the next time you open a cartridge.");
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
        if (new FileInfo(firmware).Length != expected) throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("The imported firmware has changed size. Import it again."));
        return File.ReadAllBytes(firmware);
    }
}
