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
            AetherSelectionButton(300, 450, 390, 44, global::AetherBoy.Runtime.Localization.UiText.Get("GENRE: ") + global::AetherBoy.Runtime.Localization.UiLabels.Genre(editGenre),
                "detail-genre", LibraryMetadata.Genres.Select(genre => new AetherChoice(genre, global::AetherBoy.Runtime.Localization.UiLabels.Genre(genre))).ToArray(), editGenre,
                value => editGenre = value, enabled: libraryMutation is null);
            AetherSelectionButton(714, 450, 386, 44, global::AetherBoy.Runtime.Localization.UiText.Get("RATING: ") + (editRating == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("NONE") : editRating + " / 5"),
                "detail-rating", Enumerable.Range(0, 6).Select(rating => new AetherChoice(rating.ToString(), rating == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("NONE") : rating + " / 5")).ToArray(), editRating.ToString(),
                value => editRating = int.Parse(value), enabled: libraryMutation is null);
            Ink(300, 522, global::AetherBoy.Runtime.Localization.UiText.Get("The ROM and save file are not changed."), 14, Colors.Muted);
            ActionButton(300, 564, 250, 44, global::AetherBoy.Runtime.Localization.UiText.Get("SAVE DETAILS"), () => EndTitleEdit(true), true, libraryMutation is null && !textEditor.IsComposing && !string.IsNullOrWhiteSpace(titleInput));
            ActionButton(574, 564, 250, 44, global::AetherBoy.Runtime.Localization.UiText.Get("CANCEL"), () => EndTitleEdit(false), enabled: libraryMutation is null);
            return;
        }
        var layout = LinuxLibraryLayout.Create(LogicalWidth, LogicalHeight, textRenderer.MinimumSize, libraryGrid);
        float width = layout.Width;
        float searchWidth = width - 370;
        DrawTextEntry(TextField.Search, 300, 198, searchWidth, 44, global::AetherBoy.Runtime.Localization.UiText.Get("SEARCH CARTRIDGES"));
        ActionButton(300 + searchWidth + 12, 198, 168, 44, global::AetherBoy.Runtime.Localization.UiText.Get("CLEAR SEARCH"), () => { librarySearch = ""; libraryPage = 0; });
        ActionButton(300 + width - 178, 198, 178, 44, libraryRefresh is null ? global::AetherBoy.Runtime.Localization.UiText.Get("REFRESH LIBRARY") : global::AetherBoy.Runtime.Localization.UiText.Get("REFRESHING…"), RefreshLibrary, enabled: libraryRefresh is null);
        float filterExtra = (width - 810) / 5;
        float systemWidth = 100 + filterExtra, favoritesWidth = 142 + filterExtra,
            sortWidth = 202 + filterExtra, genreWidth = 154 + filterExtra, viewWidth = 172 + filterExtra;
        string systemValue = libraryFilter == "ALL" ? global::AetherBoy.Runtime.Localization.UiText.Get("Alle") : libraryFilter;
        string sortValue = global::AetherBoy.Runtime.Localization.UiLabels.LibrarySort(librarySort.ToString());
        string genreValue = libraryGenreFilter.Length == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("ANY") : global::AetherBoy.Runtime.Localization.UiLabels.Genre(libraryGenreFilter);
        AetherSelectionButton(300, 254, systemWidth, 38, global::AetherBoy.Runtime.Localization.UiText.Get("SYSTEM: ") + systemValue,
            "library-system", [new("ALL", global::AetherBoy.Runtime.Localization.UiText.Get("Alle")), new("GB", "GB"), new("GBC", "GBC"), new("GBA", "GBA")], libraryFilter,
            value => { libraryFilter = value; libraryPage = 0; }, displayLabel: systemValue);
        float filterX = 300 + systemWidth + 10;
        ActionButton(filterX, 254, favoritesWidth, 38, favoritesOnly ? global::AetherBoy.Runtime.Localization.UiText.Get("FAVORITES ONLY") : global::AetherBoy.Runtime.Localization.UiText.Get("ALL CARTRIDGES"),
            () => { favoritesOnly = !favoritesOnly; libraryPage = 0; }, favoritesOnly, focusId: "option:library:favorites");
        filterX += favoritesWidth + 10;
        AetherSelectionButton(filterX, 254, sortWidth, 38, global::AetherBoy.Runtime.Localization.UiText.Get("SORT: ") + sortValue,
            "library-sort", Enum.GetValues<LinuxLibrarySort>().Select(sort => new AetherChoice(sort.ToString(), global::AetherBoy.Runtime.Localization.UiLabels.LibrarySort(sort.ToString()))).ToArray(), librarySort.ToString(),
            value => { librarySort = Enum.Parse<LinuxLibrarySort>(value); libraryPage = 0; }, displayLabel: sortValue);
        filterX += sortWidth + 10;
        AetherSelectionButton(filterX, 254, genreWidth, 38, global::AetherBoy.Runtime.Localization.UiText.Get("GENRE: ") + genreValue,
            "library-genre", LibraryMetadata.Genres.Select(genre => new AetherChoice(genre, genre.Length == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("ANY") : global::AetherBoy.Runtime.Localization.UiLabels.Genre(genre))).ToArray(), libraryGenreFilter,
            value => { libraryGenreFilter = value; libraryPage = 0; }, displayLabel: genreValue);
        filterX += genreWidth + 10;
        ActionButton(filterX, 254, viewWidth, 38, libraryGrid ? global::AetherBoy.Runtime.Localization.UiText.Get("GRID VIEW") : global::AetherBoy.Runtime.Localization.UiText.Get("LIST VIEW"),
            () => { libraryGrid = !libraryGrid; libraryPage = 0; }, libraryGrid, focusId: "option:library:grid");
        var matches = LinuxLibraryView.Select(libraryEntries, librarySearch, libraryFilter, favoritesOnly, librarySort, libraryGenreFilter);
        int pageSize = layout.PageSize;
        // Keep the first visible game in view when resizing instead of jumping by a newly computed page size.
        if (libraryPageSize != 0 && libraryPageSize != pageSize) libraryPage = libraryPage * libraryPageSize / pageSize;
        libraryPageSize = pageSize;
        libraryPage = Math.Clamp(libraryPage, 0, Math.Max(0, (matches.Length - 1) / pageSize));
        if (matches.Length == 0)
        {
            DrawSettingsParagraph(300, layout.Top + 16, libraryRefresh is not null ? global::AetherBoy.Runtime.Localization.UiText.Get("Reading your library…") : libraryEntries.Count == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("Open a cartridge to start your library.") : global::AetherBoy.Runtime.Localization.UiText.Get("No matches. Try another filter or clear your search."), width, 16, Colors.Text);
            DrawSettingsParagraph(300, layout.Top + 68, global::AetherBoy.Runtime.Localization.UiText.Get("Original files stay in place; patched results are stored in AetherBoy."), width, 14);
        }
        int row = 0;
        foreach (var entry in matches.Skip(libraryPage * pageSize).Take(pageSize))
        {
            int index = row++;
            float x = 300 + (index % layout.Columns) * layout.ColumnWidth;
            float y = layout.Top + (index / layout.Columns) * layout.RowHeight;
            bool exists = existingLibraryFiles.Contains(entry.Identity);
            if (libraryGrid)
            {
                float cardWidth = layout.ColumnWidth - 12;
                DrawLibraryPreview(entry.Identity, x, y, 82, 78);
                ActionButton(x + 88, y, cardWidth - 88, 38, entry.Title, () => TryLoadRom(entry.Path), enabled: exists, focusId: "open:" + entry.Identity);
                ActionButton(x + 88, y + 44, (cardWidth - 96) / 2, 34, entry.Favorite ? global::AetherBoy.Runtime.Localization.UiText.Get("STARRED") : global::AetherBoy.Runtime.Localization.UiText.Get("STAR"), () => ChangeLibraryEntry(entry.Identity, item => item with { Favorite = !item.Favorite }), entry.Favorite, enabled: libraryMutation is null, focusId: "favorite:" + entry.Identity);
                ActionButton(x + 96 + (cardWidth - 96) / 2, y + 44, (cardWidth - 96) / 2, 34, global::AetherBoy.Runtime.Localization.UiText.Get("DETAILS"), () => BeginLibraryEdit(entry), enabled: libraryMutation is null, focusId: "rename:" + entry.Identity);
                Ink(x, y + 82, textRenderer.Fit(entry.System + (exists ? global::AetherBoy.Runtime.Localization.UiText.Format(": {0}m played", (int)(entry.PlaySeconds / 60)) : global::AetherBoy.Runtime.Localization.UiText.Get(": File missing")), cardWidth, 12), 12, exists ? Colors.Muted : Colors.Danger);
            }
            else
            {
                ActionButton(x, y, width - 274, 36, entry.Title, () => TryLoadRom(entry.Path), enabled: exists, focusId: "open:" + entry.Identity);
                ActionButton(300 + width - 258, y, 124, 36, entry.Favorite ? global::AetherBoy.Runtime.Localization.UiText.Get("STARRED") : global::AetherBoy.Runtime.Localization.UiText.Get("STAR"), () => ChangeLibraryEntry(entry.Identity, item => item with { Favorite = !item.Favorite }), entry.Favorite, enabled: libraryMutation is null, focusId: "favorite:" + entry.Identity);
                ActionButton(300 + width - 120, y, 120, 36, global::AetherBoy.Runtime.Localization.UiText.Get("DETAILS"), () => BeginLibraryEdit(entry), enabled: libraryMutation is null, focusId: "rename:" + entry.Identity);
                string metadata = entry.System + ": " + (exists ? global::AetherBoy.Runtime.Localization.UiText.Format("Played {0}h {1}m. Last opened {2:dd MMM yyyy}.", (int)(entry.PlaySeconds / 3600), (int)(entry.PlaySeconds / 60) % 60, entry.LastPlayed.ToLocalTime()) : global::AetherBoy.Runtime.Localization.UiText.Get("File missing. Use Open / Relocate below."));
                Ink(x, y + 39, textRenderer.Fit(metadata, width, 13), 13, exists ? Colors.Muted : Colors.Danger);
            }
        }
        DrawMessageStatus(300, layout.SummaryY, width, layout.FooterY - layout.SummaryY - 4,
            libraryWarning ?? global::AetherBoy.Runtime.Localization.UiText.Format("{0} games, page {1} of {2}. Saves are tied to game content.", matches.Length, libraryPage + 1, Math.Max(1, (matches.Length + pageSize - 1) / pageSize)),
            libraryWarning is not null, singleLine: true);
        ActionButton(300, layout.FooterY, 160, 44, global::AetherBoy.Runtime.Localization.UiText.Get("PREVIOUS"), () => ChangeLibraryPage(-1), enabled: libraryPage > 0, focusId: "library:previous");
        ActionButton(476, layout.FooterY, 160, 44, global::AetherBoy.Runtime.Localization.UiText.Get("NEXT"), () => ChangeLibraryPage(1), enabled: (libraryPage + 1) * pageSize < matches.Length, focusId: "library:next");
        ActionButton(660, layout.FooterY, 200, 44, global::AetherBoy.Runtime.Localization.UiText.Get("OPEN / RELOCATE"), ShowRomDialog, true);
        ActionButton(880, layout.FooterY, width - 580, 44, "PATCH LAB", OpenPatchLab);
    }

    private int libraryPageSize;
    private void ChangeLibraryPage(int direction)
    {
        libraryPage += direction;
        DrawShell();
        // Paging keeps focus at the first game; a final page with a missing file still offers its details.
        focusedControl = focusIdentities.FindIndex(identity => identity.Action.StartsWith("open:", StringComparison.Ordinal) || identity.Action.StartsWith("favorite:", StringComparison.Ordinal));
    }

    private bool HandleLibraryNavigationKey(SDL.KeyboardEvent key, bool down)
    {
        if (!controlCenterVisible || controlCenterPage != ControlCenterPage.Library || ShowingSettingsSearch || showPatchLab || editingTitleIdentity is not null || ActiveTextField != TextField.None) return false;
        if (!down) return key.Scancode is SDL.Scancode.Pageup or SDL.Scancode.Pagedown;
        if (key.Scancode is SDL.Scancode.Pageup or SDL.Scancode.Pagedown)
        {
            DrawShell();
            var matches = LinuxLibraryView.Select(libraryEntries, librarySearch, libraryFilter, favoritesOnly, librarySort, libraryGenreFilter);
            int last = Math.Max(0, (matches.Length - 1) / libraryPageSize);
            int next = Math.Clamp(libraryPage + (key.Scancode == SDL.Scancode.Pagedown ? 1 : -1), 0, last);
            if (next != libraryPage) ChangeLibraryPage(next - libraryPage);
            return true;
        }
        if (key.Scancode is SDL.Scancode.Up or SDL.Scancode.Down or SDL.Scancode.Left or SDL.Scancode.Right)
        {
            DrawShell();
            if (focusTargets.Count > 0)
            {
                int direction = key.Scancode is SDL.Scancode.Down or SDL.Scancode.Right ? 1 : -1;
                focusedControl = focusedControl < 0 ? focusIdentities.FindIndex(identity => identity.Action.StartsWith("open:", StringComparison.Ordinal))
                    : (focusedControl + direction + focusTargets.Count) % focusTargets.Count;
            }
            return true;
        }
        return false;
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
