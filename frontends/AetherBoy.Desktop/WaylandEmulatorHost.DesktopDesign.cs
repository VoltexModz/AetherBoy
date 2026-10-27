using nanoboy.Core;
using SDL3;
using UiText = AetherBoy.Runtime.Localization.UiText;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private void Icon(float x, float y, UiIcon icon, SDL.Color color, float size = 18)
    {
        SDL.SetRenderDrawColor(renderer, color.R, color.G, color.B, 255);
        var lines = UiIconGeometry.Lines(icon);
        for (int i = 0; i < lines.Length; i += 4)
            SDL.RenderLine(renderer, x + lines[i] * size / 24, y + lines[i + 1] * size / 24,
                x + lines[i + 2] * size / 24, y + lines[i + 3] * size / 24);
    }

    private void DesktopButton(float x, float y, float width, float height, string label, UiIcon icon, Action action,
        bool emphasis = false, bool enabled = true, bool iconOnly = false, bool selected = false, string? id = null, bool left = false)
    {
        ActionButton(x, y, width, height, label, action, selected, enabled, id, emphasis, displayLabel: "");
        var foreground = !enabled ? Colors.Muted : emphasis ? Colors.ButtonText : selected ? Colors.Violet : Colors.Text;
        float textWidth = iconOnly ? 0 : textRenderer.Measure(label, 14);
        float iconX = iconOnly ? x + (width - 18) / 2 : left ? x + 14 : x + Math.Max(12, (width - textWidth - 27) / 2);
        Icon(iconX, y + (height - 18) / 2, icon, foreground);
        if (!iconOnly)
        {
            drawingButtonLabel = true;
            Ink(iconX + 27, y + (height - textRenderer.MinimumSize) / 2 - 2, textRenderer.Fit(label, x + width - iconX - 35, 14), 14, foreground);
            drawingButtonLabel = false;
        }
    }

    private void DrawDesktopChrome(string title, string active)
    {
        DrawDesktopWindowChrome(title);
        var layout = UiDesktopLayout.Create(LogicalWidth, LogicalHeight);
        Paint(0, 64, layout.Navigation, LogicalHeight - 96, Colors.Chrome);
        Paint(layout.Navigation - 1, 64, 1, LogicalHeight - 96, Colors.Border);
        (string Key, string Label, UiIcon Icon, Action Action)[] entries =
        [
            ("play", "Spielen", UiIcon.Play, () => { if (controlCenterVisible) CloseControlCenter(); }),
            ("library", "Bibliothek", UiIcon.Library, () => OpenControlPage(ControlCenterPage.Library)),
            ("saves", "Spielstände", UiIcon.Save, () => OpenControlPage(ControlCenterPage.Saves)),
            ("link", "Link", UiIcon.Link, OpenOnlineLinkPage),
            ("tools", "Werkzeuge", UiIcon.Tools, () => { showOnlineLinkPage = false; OpenControlPage(ControlCenterPage.Tools); }),
            ("settings", "Einstellungen", UiIcon.Settings, () => { OpenControlPage(ControlCenterPage.System); OpenAppearancePage(); })
        ];
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            DesktopButton(12, i == 5 ? LogicalHeight - 94 : 92 + i * 52, layout.Navigation - 24, 46,
                UiText.Get(entry.Label), entry.Icon, entry.Action, iconOnly: layout.Navigation < 100,
                selected: active == entry.Key, id: "nav:desktop:" + entry.Key, left: true);
        }
    }

    private void DrawDesktopWindowChrome(string title)
    {
        Paint(0, 0, LogicalWidth, 64, Colors.Chrome);
        Paint(0, 63, LogicalWidth, 1, Colors.Border);
        Mark(20, 13, 38); Ink(70, 19, "AetherBoy", 18, bold: true);
        Center(LogicalWidth / 2f, 23, title, 13, Colors.Muted);
        DesktopButton(LogicalWidth - 164, 12, 40, 40, UiText.Get("Fenster minimieren"), UiIcon.Minimize, () => SDL.MinimizeWindow(window), iconOnly: true, id: "window:minimize");
        DesktopButton(LogicalWidth - 112, 12, 40, 40, UiText.Get("Fenster maximieren"), UiIcon.Maximize, () =>
        { if ((SDL.GetWindowFlags(window) & SDL.WindowFlags.Maximized) != 0) SDL.RestoreWindow(window); else SDL.MaximizeWindow(window); }, iconOnly: true, id: "window:maximize");
        DesktopButton(LogicalWidth - 60, 12, 40, 40, UiText.Get("Fenster schließen"), UiIcon.Close, () => running = false, iconOnly: true, id: "window:close");
        Paint(0, LogicalHeight - 32, LogicalWidth, 32, Colors.Chrome);
        Paint(0, LogicalHeight - 32, LogicalWidth, 1, Colors.Border);
        Icon(24, LogicalHeight - 24, UiIcon.Play, Colors.Cyan, 15);
        Ink(47, LogicalHeight - 30 + Math.Max(2, (30 - textRenderer.MinimumSize) / 2), UiText.Get(gamepad == IntPtr.Zero ? "Tastatur" : "Controller verbunden"), 11, Colors.Muted);
        string fullscreenHint = UiText.Get("F11  Vollbild");
        Ink(LogicalWidth - 24 - textRenderer.Measure(fullscreenHint, 11), LogicalHeight - 30 + Math.Max(2, (30 - textRenderer.MinimumSize) / 2), fullscreenHint, 11, Colors.Muted);
    }
    private void DrawDesktopLibrary()
    {
        DrawDesktopChrome(UiText.Get("Bibliothek"), "library");
        var layout = UiDesktopLayout.Create(LogicalWidth, LogicalHeight);
        float x = layout.ContentX, width = layout.ContentWidth, top = 64 + layout.Inset;
        Ink(x, top, UiText.Get("Bibliothek"), 28, bold: true);
        Ink(x, top + 43, UiText.Format("{0} Spiele in deiner Sammlung", libraryEntries.Count), 13, Colors.Muted);
        DesktopButton(x + width - 196, top + 8, 196, 44, UiText.Get("Spiel öffnen"), UiIcon.Open, ShowRomDialog, emphasis: true, enabled: fileDialogOpen == 0);
        float searchY = top + 85;
        DrawTextEntry(TextField.Search, x, searchY, width - 310, 44, UiText.Get("Spiele suchen …"));
        AetherSelectionButton(x + width - 298, searchY, 174, 44, libraryFilter == "ALL" ? UiText.Get("Alle Systeme") : libraryFilter,
            "library-system", [new("ALL", UiText.Get("Alle Systeme")), new("GB", "Game Boy"), new("GBC", "Game Boy Color"), new("GBA", "Game Boy Advance")], libraryFilter,
            value => { libraryFilter = value; libraryPage = 0; });
        ActionButton(x + width - 112, searchY, 112, 44, UiText.Get("Favoriten"), () => { favoritesOnly = !favoritesOnly; libraryPage = 0; }, favoritesOnly, focusId: "option:library:favorites");
        var matches = LinuxLibraryView.Select(libraryEntries, librarySearch, libraryFilter, favoritesOnly, librarySort, libraryGenreFilter);
        float heroY = searchY + 68, heroHeight = LogicalHeight >= 850 ? 238 : 170;
        if (matches.FirstOrDefault() is { } recent)
        {
            Paint(x, heroY, width, heroHeight, Colors.Surface);
            float previewWidth = Math.Min(350, width * .34f);
            DrawLibraryPreview(recent.Identity, x, heroY, previewWidth, heroHeight);
            Ink(x + previewWidth + 28, heroY + 22, UiText.Get("Zuletzt gespielt"), 12, Colors.Cyan);
            Ink(x + previewWidth + 28, heroY + 50, textRenderer.Fit(recent.Title, width - previewWidth - 56, 24, true), 24, bold: true);
            Ink(x + previewWidth + 28, heroY + 86, recent.System + " · " + UiText.Format("{0} min gespielt", (int)(recent.PlaySeconds / 60)), 13, Colors.Muted);
            DesktopButton(x + previewWidth + 28, heroY + heroHeight - 60, 196, 44, UiText.Get("Spiel starten"), UiIcon.Play,
                () => TryLoadRom(recent.Path), emphasis: true, enabled: existingLibraryFiles.Contains(recent.Identity), id: "recent:" + recent.Identity);
        }
        else
        {
            DrawSettingsParagraph(x, heroY + 32, libraryRefresh is not null ? UiText.Get("Reading your library…") : UiText.Get("No matches. Try another filter or clear your search."), width, 16);
        }
        float listY = heroY + heroHeight + 24;
        Ink(x, listY + 10, UiText.Get("Alle Spiele"), 16, bold: true);
        string sort = AetherBoy.Runtime.Localization.UiLabels.LibrarySort(librarySort.ToString());
        float sortWidth = Math.Max(220, Enum.GetValues<LinuxLibrarySort>().Max(value => textRenderer.Measure(AetherBoy.Runtime.Localization.UiLabels.LibrarySort(value.ToString()), 13, true)) + 44);
        AetherSelectionButton(x + width - sortWidth, listY, sortWidth, 38, UiText.Get("SORT: ") + sort, "library-sort",
            Enum.GetValues<LinuxLibrarySort>().Select(value => new AetherChoice(value.ToString(), AetherBoy.Runtime.Localization.UiLabels.LibrarySort(value.ToString()))).ToArray(), librarySort.ToString(),
            value => { librarySort = Enum.Parse<LinuxLibrarySort>(value); libraryPage = 0; }, displayLabel: sort);
        float footer = LogicalHeight - 106, rowHeight = Math.Max(54, textRenderer.MinimumSize + 34), rowsTop = listY + 50;
        int pageSize = Math.Max(1, (int)((footer - rowsTop - 12) / rowHeight));
        if (libraryPageSize > 0 && libraryPageSize != pageSize) libraryPage = libraryPage * libraryPageSize / pageSize;
        libraryPageSize = pageSize; libraryPage = Math.Clamp(libraryPage, 0, Math.Max(0, (matches.Length - 1) / pageSize));
        int row = 0;
        foreach (var entry in matches.Skip(libraryPage * pageSize).Take(pageSize))
        {
            float y = rowsTop + row++ * rowHeight;
            Paint(x, y, width, 1, Colors.Border);
            DesktopButton(x, y + 4, width - 230, rowHeight - 8, entry.Title, UiIcon.Play, () => TryLoadRom(entry.Path),
                enabled: existingLibraryFiles.Contains(entry.Identity), id: "open:" + entry.Identity, left: true);
            Ink(x + width - 220, y + 17, entry.System, 12, Colors.Muted);
            DesktopButton(x + width - 130, y + 4, 44, 44, UiText.Get("Favorit"), UiIcon.Favorite,
                () => ChangeLibraryEntry(entry.Identity, item => item with { Favorite = !item.Favorite }), iconOnly: true, selected: entry.Favorite, enabled: libraryMutation is null, id: "favorite:" + entry.Identity);
            DesktopButton(x + width - 44, y + 4, 44, 44, UiText.Get("Weitere Aktionen"), UiIcon.More,
                () => BeginLibraryEdit(entry), iconOnly: true, enabled: libraryMutation is null, id: "rename:" + entry.Identity);
        }
        DesktopButton(x, footer, 44, 38, UiText.Get("PREVIOUS"), UiIcon.Rewind, () => ChangeLibraryPage(-1), enabled: libraryPage > 0, iconOnly: true, id: "library:previous");
        DesktopButton(x + 56, footer, 44, 38, UiText.Get("NEXT"), UiIcon.Turbo, () => ChangeLibraryPage(1), enabled: (libraryPage + 1) * pageSize < matches.Length, iconOnly: true, id: "library:next");
        AetherSelectionButton(x + 112, footer, 180, 38, libraryGenreFilter.Length == 0 ? UiText.Get("Alle Genres") : AetherBoy.Runtime.Localization.UiLabels.Genre(libraryGenreFilter), "library-genre",
            AetherBoy.Runtime.LibraryMetadata.Genres.Select(genre => new AetherChoice(genre, genre.Length == 0 ? UiText.Get("Alle Genres") : AetherBoy.Runtime.Localization.UiLabels.Genre(genre))).ToArray(), libraryGenreFilter,
            value => { libraryGenreFilter = value; libraryPage = 0; });
        ActionButton(x + 304, footer, 200, 38, UiText.Get("REFRESH LIBRARY"), RefreshLibrary, enabled: libraryRefresh is null);
        ActionButton(x + 516, footer, 160, 38, UiText.Get("GRID VIEW"), () => { libraryGrid = true; libraryPage = 0; }, focusId: "option:library:grid");
        ActionButton(x + width - 140, footer, 140, 38, "Patch Lab", OpenPatchLab);
        if (libraryWarning is not null || loadError is not null)
            DrawMessageStatus(x, LogicalHeight - 66, width, 30, loadError ?? libraryWarning ?? "", true, singleLine: true);
        if (aetherSelection is not null) DrawAetherSelectionPopup();
        if (showOnScreenKeyboard) DrawOnScreenKeyboard();
    }

    private void DrawDesktopAppearance()
    {
        DrawSettingsChrome();
        float x = 280, width = LogicalWidth - 312, top = 96;
        Ink(x, top, UiText.Get("Aussehen"), 28, bold: true);
        Ink(x, top + 43, UiText.Get("Passe die Oberfläche an dich an."), 13, Colors.Muted);
        float themesY = top + 90, cardWidth = (width - 60) / 6;
        Ink(x, themesY, UiText.Get("Themes"), 16, bold: true);
        var selected = UiThemePresets.Match(options.UiPrimaryColor, options.UiSecondaryColor, options.UiBackgroundColor);
        for (int i = 0; i < UiThemePresets.All.Count; i++)
        {
            var preset = UiThemePresets.All[i]; var palette = new UiThemePalette(preset.Primary, preset.Secondary, preset.Background);
            float cx = x + i * (cardWidth + 12), cy = themesY + 36;
            ActionButton(cx, cy, cardWidth, 96, preset.Name, () => SelectThemePreset(preset), selected?.Id == preset.Id, focusId: "theme:" + preset.Id, displayLabel: "");
            static SDL.Color C(UiRgb value) => Rgb(value.R, value.G, value.B);
            Paint(cx + 4, cy + 4, cardWidth - 8, 88, C(palette.Background));
            Paint(cx + 8, cy + 14, 16, 3, C(palette.Primary)); Paint(cx + 8, cy + 25, 16, 3, C(palette.Primary));
            Paint(cx + 30, cy + 20, cardWidth - 40, 52, C(palette.Surface));
            Paint(cx + cardWidth - 45, cy + 79, 34, 4, C(palette.Secondary));
            DrawSettingsParagraph(cx, cy + 100, preset.Name, cardWidth, 12, selected?.Id == preset.Id ? Colors.Text : Colors.Muted);
        }
        float colorsY = themesY + 200, fieldWidth = (width - 32) / 3;
        Ink(x, colorsY, UiText.Get("Eigene Farben"), 16, bold: true);
        (string Label, TextField Field, string Value)[] fields = [("Primary", TextField.ThemePrimary, primaryColorInput), ("Secondary", TextField.ThemeSecondary, secondaryColorInput), ("Background", TextField.ThemeBackground, backgroundColorInput)];
        for (int i = 0; i < fields.Length; i++)
        {
            float cx = x + i * (fieldWidth + 16); var field = fields[i];
            Ink(cx, colorsY + 34, UiText.Get(field.Label), 12, Colors.Muted);
            DrawTextEntry(field.Field, cx + 36, colorsY + 58, fieldWidth - 36, 44, "#RRGGBB");
            if (UiRgb.TryParse(field.Value, out var rgb)) Paint(cx, colorsY + 70, 24, 24, Rgb(rgb.R, rgb.G, rgb.B));
        }
        float sizeY = colorsY + 116;
        Paint(x, sizeY, width, 1, Colors.Border);
        Ink(x, sizeY + 20, UiText.Get("Oberflächengröße"), 14, bold: true);
        ActionButton(x + width - 174, sizeY + 12, 174, 44, UiText.Format("Text: {0}", TextSizeName), CycleTextSize);
        Ink(x, sizeY + 82, UiText.Get("Anzeigesprache"), 14, bold: true);
        AetherSelectionButton(x + width - 174, sizeY + 70, 174, 44, UiText.Get(options.DisplayLanguage == "de" ? "Deutsch" : options.DisplayLanguage == "en" ? "English" : "System"), "desktop-language",
            [new("system", UiText.Get("System")), new("de", "Deutsch"), new("en", "English")], options.DisplayLanguage,
            value => { options.DisplayLanguage = value; MarkSettingsChanged(); statusMessage = UiText.Get("Die Sprache wird nach einem Neustart geändert."); });
        ActionButton(x, LogicalHeight - 112, Math.Max(246, textRenderer.Measure(UiText.Get("Restore Aether Original"), 14, true) + 24), 44, UiText.Get("Restore Aether Original"), ResetAppearanceColors);
        float applyWidth = Math.Max(196, textRenderer.Measure(UiText.Get("Farben anwenden"), 14) + 62);
        DesktopButton(x + width - applyWidth, LogicalHeight - 112, applyWidth, 44, UiText.Get("Farben anwenden"), UiIcon.Save, ApplyAppearanceColors, emphasis: true);
        DrawMessageStatus(x, LogicalHeight - 64, width, 28, loadError ?? statusMessage, loadError is not null, singleLine: true);
        if (aetherSelection is not null) DrawAetherSelectionPopup();
        if (showOnScreenKeyboard) DrawOnScreenKeyboard();
    }

}
