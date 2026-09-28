using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private enum GraphicsSection { Picture, Palette, Performance }
    private enum SystemSection { Overview, Desktop, Profiles, Firmware, Files }
    private GraphicsSection graphicsSection;
    private SystemSection systemSection;
    private bool showInputShortcuts;
    private string settingsSearch = "";
    private bool editingSettingsSearch;
    private int settingsSearchPage;
    private bool ShowingSettingsSearch => editingSettingsSearch || !string.IsNullOrWhiteSpace(settingsSearch);
    private const int SettingsResultsPerPage = 4;

    private void ResetSettingsNavigation()
    {
        settingsSearch = "";
        editingSettingsSearch = false;
        settingsSearchPage = 0;
        graphicsSection = GraphicsSection.Picture;
        systemSection = SystemSection.Overview;
        showInputShortcuts = false;
        showControllerStick = false;
    }

    private void FocusSettingsSearch()
    {
        if (editingTitleIdentity is not null)
        { statusMessage = "Save or cancel the title before searching settings."; return; }
        CommitActiveText();
        rebindingAction = null; rebindingGamepad = null;
        BeginTextEditing(TextField.SettingsSearch);
    }

    private void ClearSettingsSearch()
    {
        CommitActiveText();
        settingsSearch = "";
        settingsSearchPage = 0;
        focusedControl = -1;
    }

    private void OpenSettingsDestination(LinuxSettingsDestination destination)
    {
        if (editingTitleIdentity is not null) return;
        CommitActiveText();
        ControlCenterPage page = destination switch
        {
            LinuxSettingsDestination.Picture or LinuxSettingsDestination.Scaling or LinuxSettingsDestination.Fullscreen or
                LinuxSettingsDestination.Palette or LinuxSettingsDestination.Performance => ControlCenterPage.Display,
            LinuxSettingsDestination.Keyboard or LinuxSettingsDestination.Controller or LinuxSettingsDestination.ControllerStick or LinuxSettingsDestination.Shortcuts => ControlCenterPage.Input,
            LinuxSettingsDestination.Volume or LinuxSettingsDestination.Channels => ControlCenterPage.Audio,
            LinuxSettingsDestination.Saves or LinuxSettingsDestination.Backups or LinuxSettingsDestination.Gallery => ControlCenterPage.Saves,
            LinuxSettingsDestination.Library or LinuxSettingsDestination.PatchLab => ControlCenterPage.Library,
            LinuxSettingsDestination.Tools or LinuxSettingsDestination.Online => ControlCenterPage.Tools,
            LinuxSettingsDestination.Diagnostics => ControlCenterPage.Diagnostics,
            _ => ControlCenterPage.System
        };
        SelectControlCenterPage(page);
        graphicsSection = destination == LinuxSettingsDestination.Palette ? GraphicsSection.Palette
            : destination == LinuxSettingsDestination.Performance ? GraphicsSection.Performance : GraphicsSection.Picture;
        showController = destination is LinuxSettingsDestination.Controller or LinuxSettingsDestination.ControllerStick;
        showControllerStick = destination == LinuxSettingsDestination.ControllerStick;
        showInputShortcuts = destination == LinuxSettingsDestination.Shortcuts;
        systemSection = destination switch
        {
            LinuxSettingsDestination.Desktop or LinuxSettingsDestination.TextSize => SystemSection.Desktop,
            LinuxSettingsDestination.Profiles => SystemSection.Profiles,
            LinuxSettingsDestination.Firmware => SystemSection.Firmware,
            LinuxSettingsDestination.Storage => SystemSection.Files,
            _ => SystemSection.Overview
        };
        if (destination == LinuxSettingsDestination.Appearance) OpenAppearancePage();
        if (destination == LinuxSettingsDestination.Backups) showBackups = true;
        if (destination == LinuxSettingsDestination.Gallery) showGallery = true;
        if (destination == LinuxSettingsDestination.PatchLab) OpenPatchLab();
        if (page == ControlCenterPage.Tools) showOnlineLinkPage = false;
        if (destination == LinuxSettingsDestination.Online) OpenOnlineLinkPage();
    }

    private void BackFromSettings()
    {
        if (ShowingSettingsSearch) { ClearSettingsSearch(); return; }
        if (controlCenterPage == ControlCenterPage.System && showAppearance) { CommitActiveText(); showAppearance = false; focusedControl = -1; return; }
        if (controlCenterPage == ControlCenterPage.System && systemSection != SystemSection.Overview)
        { systemSection = SystemSection.Overview; focusedControl = -1; return; }
        if (controlCenterPage == ControlCenterPage.Input && showControllerStick) { showControllerStick = false; focusedControl = -1; return; }
        if (controlCenterPage == ControlCenterPage.Input && (showController || showInputShortcuts))
        { SetInputTab(false); return; }
        if (controlCenterPage == ControlCenterPage.Display && graphicsSection != GraphicsSection.Picture)
        { graphicsSection = GraphicsSection.Picture; focusedControl = -1; return; }
        CloseControlCenter();
    }

    // Keep prose on the left and the action on the right. Rows have room for two lines at large text sizes.
    private void SettingsLink(float y, string title, string description, string button, Action action, string id)
    {
        Ink(302, y + 3, title, 18, bold: true);
        DrawSettingsParagraph(302, y + 31, description, 584, 14);
        ActionButton(910, y + 9, 200, 42, button, action, focusId: "setting:" + id);
    }

    private float DrawSettingsParagraph(float x, float y, string text, float width, int size = 14, SDL.Color? color = null)
    {
        string line = "";
        float step = Math.Max(size, textRenderer.MinimumSize) + 7;
        foreach (string word in text.Split(' '))
        {
            string candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && textRenderer.Measure(candidate, size) > width)
            { Ink(x, y, line, size, color ?? Colors.Muted); y += step; line = word; }
            else line = candidate;
        }
        if (line.Length > 0) { Ink(x, y, line, size, color ?? Colors.Muted); y += step; }
        return y;
    }

    private void DrawSettingsOverview()
    {
        SettingsLink(198, "Graphics", "Picture size, filters and Game Boy colors.", "Open graphics",
            () => OpenSettingsDestination(LinuxSettingsDestination.Picture), "graphics");
        SettingsLink(284, "Controller", "Change button mappings and stick sensitivity.", "Set up controller",
            () => OpenSettingsDestination(LinuxSettingsDestination.Controller), "controller");
        SettingsLink(370, "Keyboard", "Choose the keys you use to play.", "Change keys",
            () => OpenSettingsDestination(LinuxSettingsDestination.Keyboard), "keyboard");
        SettingsLink(456, "Appearance", "Change the app colors or make text larger.", "Open appearance",
            () => OpenSettingsDestination(LinuxSettingsDestination.Appearance), "appearance");
        SettingsLink(542, "Save states", "Save, resume or manage your saved games.", "Open save states",
            () => OpenSettingsDestination(LinuxSettingsDestination.Saves), "saves");
    }

    private void DrawSettingsResults()
    {
        LinuxSettingEntry[] results = LinuxSettingsCatalog.Search(settingsSearch);
        settingsSearchPage = Math.Clamp(settingsSearchPage, 0, Math.Max(0, (results.Length - 1) / SettingsResultsPerPage));
        Ink(302, 196, results.Length == 1 ? "1 setting found" : $"{results.Length} settings found", 16, Colors.Muted);
        ActionButton(944, 190, 166, 36, "Clear search", ClearSettingsSearch);
        if (results.Length == 0)
        {
            Ink(302, 285, "No matching settings", 22, bold: true);
            DrawSettingsParagraph(302, 327, "Try a shorter word, such as controller, volume, palette or BIOS. You can also choose a section in the sidebar.", 690, 16);
            return;
        }
        int start = settingsSearchPage * SettingsResultsPerPage;
        for (int i = start; i < Math.Min(results.Length, start + SettingsResultsPerPage); i++)
        {
            var entry = results[i];
            float y = 246 + (i - start) * 82;
            Ink(302, y, entry.Title, 18, bold: true);
            Ink(302, y + 30, entry.Location, 14, Colors.Muted);
            ActionButton(930, y + 6, 180, 42, "Open setting", () => OpenSettingsDestination(entry.Destination),
                focusId: "result:" + entry.Destination);
        }
        if (results.Length > SettingsResultsPerPage)
        {
            ActionButton(302, 584, 140, 38, "Previous", () => { settingsSearchPage--; focusedControl = -1; }, enabled: start > 0);
            Ink(470, 592, $"{start + 1}–{Math.Min(results.Length, start + SettingsResultsPerPage)} of {results.Length}", 14, Colors.Muted);
            ActionButton(970, 584, 140, 38, "Next", () => { settingsSearchPage++; focusedControl = -1; }, enabled: start + SettingsResultsPerPage < results.Length);
        }
    }

    private void DrawDisplayPage()
    {
        string[] tabs = ["Picture", "Game Boy colors", "Performance"];
        for (int i = 0; i < tabs.Length; i++)
        {
            var section = (GraphicsSection)i;
            ActionButton(300 + i * 274, 194, 262, 42, tabs[i], () => { graphicsSection = section; focusedControl = -1; statusMessage = ""; }, graphicsSection == section,
                focusId: "option:graphics:" + section);
        }
        if (graphicsSection == GraphicsSection.Palette) { DrawPaletteSettings(); return; }
        if (graphicsSection == GraphicsSection.Performance) { DrawGraphicsPerformance(); return; }

        Ink(300, 270, "Picture filter", 18, bold: true);
        DrawSettingsParagraph(300, 299, "Sharp keeps edges. Smooth softens them. LCD grid adds lines.", 350);
        string[] filters = ["Sharp", "Smooth", "LCD grid"];
        for (int i = 0; i < filters.Length; i++)
        {
            var filter = (LinuxVideoFilter)i;
            ActionButton(686 + i * 144, 280, 136, 44, filters[i], () => SetVideoFilter(filter), options.VideoFilter == filter,
                focusId: "option:filter:" + filter);
        }
        Paint(300, 373, 810, 1, Colors.Border);
        Ink(300, 396, "Picture size", 18, bold: true);
        DrawSettingsParagraph(300, 426, "Whole pixels keeps pixels even. Fit fills more space without stretching.", 350);
        string[] scales = ["Auto", "Whole pixels", "Fit"];
        for (int i = 0; i < scales.Length; i++)
        {
            var scaling = (LinuxVideoScaling)i;
            ActionButton(new float[] { 686, 802, 998 }[i], 406, new float[] { 104, 184, 112 }[i], 44, scales[i], () => { options.VideoScaling = scaling; MarkSettingsChanged(); }, options.VideoScaling == scaling,
                focusId: "option:scaling:" + scaling);
        }
        DrawSettingsParagraph(686, 463, "Auto: Fit for Smooth; whole pixels otherwise.", 424);
        Paint(300, 521, 810, 1, Colors.Border);
        Ink(300, 547, "Fullscreen", 18, bold: true);
        Ink(300, 577, "You can also press F11 while playing.", 14, Colors.Muted);
        ActionButton(814, 554, 296, 44, isFullscreen ? "Use a window" : "Enter fullscreen", Fullscreen);
    }

    private void DrawPaletteSettings()
    {
        Ink(300, 273, "Original Game Boy colors", 20, bold: true);
        DrawSettingsParagraph(300, 309, "These palettes recolor original Game Boy games. Game Boy Color and Game Boy Advance games keep their own colors.", 785, 16);
        uint[][] swatches = [
            [0xFFF5F5F5, 0xFFA0A0A0, 0xFF505050, 0xFF000000],
            [0xFF9BBC0F, 0xFF8BAC0F, 0xFF306230, 0xFF0F380F],
            [0xFF00FFCD, 0xFF00A597, 0xFF00665E, 0xFF00332F],
            [0xFFF5EA8C, 0xFFD4B055, 0xFF8C5620, 0xFF381900],
            [0xFF00FFFF, 0xFFFF00FF, 0xFF800080, 0xFF000040]
        ];
        string[] names = ["Pocket", "Original", "Light", "Sepia", "Cyber"];
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            ActionButton(300 + i * 164, 402, 152, 44, names[i], () => SetPalette(index), options.PaletteIndex == i,
                focusId: "option:palette:" + i);
            for (int c = 0; c < 4; c++)
            { uint color = swatches[i][c]; Paint(300 + i * 164 + c * 38, 457, 38, 42, Rgb((byte)(color >> 16), (byte)(color >> 8), (byte)color)); }
        }
        SettingsLink(546, "Looking for app colors?", "Change the menus and background under Appearance.", "Open appearance",
            () => OpenSettingsDestination(LinuxSettingsDestination.Appearance), "appearance");
    }

    private void DrawGraphicsPerformance()
    {
        Ink(300, 275, "Frames to display", 20, bold: true);
        DrawSettingsParagraph(300, 312, "Keep All frames for the smoothest motion. Skip frames only if rendering is slow; the emulated game speed stays the same.", 775, 16);
        for (int i = 0; i <= 2; i++)
        {
            int value = i;
            ActionButton(300 + i * 230, 394, 218, 44, i == 0 ? "All frames" : $"Skip {i}", () => SetFrameskip(value), options.Frameskip == i,
                focusId: "option:frameskip:" + i);
        }
        Paint(300, 476, 810, 1, Colors.Border);
        Ink(300, 504, "Performance overlay", 18, bold: true);
        DrawSettingsParagraph(300, 536, "Show frame rate and audio timing while playing. F9 toggles it.", 530);
        ActionButton(910, 518, 200, 44, options.PerformanceOverlay ? "Overlay on" : "Overlay off", TogglePerformanceOverlay,
            options.PerformanceOverlay, focusId: "option:overlay");
    }

    private void SetInputTab(bool controller, bool shortcuts = false)
    {
        showController = controller; showInputShortcuts = shortcuts; showControllerStick = false;
        rebindingAction = null; rebindingGamepad = null; focusedControl = -1;
    }

    private void DrawInputTabs()
    {
        ActionButton(300, 194, 262, 40, "Keyboard", () => SetInputTab(false), !showController && !showInputShortcuts, focusId: "option:input:keyboard");
        ActionButton(574, 194, 262, 40, "Controller", () => SetInputTab(true), showController, focusId: "option:input:controller");
        ActionButton(848, 194, 262, 40, "App shortcuts", () => SetInputTab(false, true), showInputShortcuts, focusId: "option:input:shortcuts");
    }

    private void DrawInputShortcuts()
    {
        Ink(300, 256, "These app shortcuts are fixed. Change game keys in the Keyboard tab.", 14, Colors.Muted);
        (string Action, string Key)[] shortcuts = [
            ("Open a game", "O"), ("Settings", "C"), ("Save state", "F5"), ("Rewind one step", "F7"),
            ("Load state", "F8"), ("Performance overlay", "F9"), ("Online Link", "F10"),
            ("Fullscreen", "F11"), ("Screenshot", "F12"), ("Find a setting", "Ctrl+K"),
            ("Accessible controls", "Ctrl+F7"), ("Select save slot", "1–5")
        ];
        for (int i = 0; i < shortcuts.Length; i++)
        {
            float x = 300 + i / 6 * 424, y = 302 + i % 6 * 47;
            Ink(x, y, shortcuts[i].Action, 15);
            Ink(x + 274, y, shortcuts[i].Key, 15, Colors.Cyan, true);
        }
    }

    private void DrawSystemSettings()
    {
        if (showAppearance) { DrawAppearancePage(); return; }
        if (systemSection == SystemSection.Overview)
        {
            SettingsLink(198, "Appearance", "Accent and background colors for the whole app.", "Change colors", OpenAppearancePage, "appearance");
            SettingsLink(284, "Desktop & accessibility", "Text size, automatic pause and accessible controls.", "Open desktop", () => systemSection = SystemSection.Desktop, "desktop");
            SettingsLink(370, "Game profiles", "Choose global defaults or settings for this game.", "Open profiles", () => systemSection = SystemSection.Profiles, "profiles");
            SettingsLink(456, "Firmware", "Import boot ROMs and choose how games start.", "Open firmware", () => systemSection = SystemSection.Firmware, "firmware");
            SettingsLink(542, "Files", "Find your games, saved data and captures.", "Open folders", () => systemSection = SystemSection.Files, "files");
            return;
        }
        ActionButton(300, 194, 182, 40, "Back", () => { systemSection = SystemSection.Overview; focusedControl = -1; });
        switch (systemSection)
        {
            case SystemSection.Desktop:
                SettingsLink(267, "Text size", "Applies to the whole interface and every game.", TextSizeName, CycleTextSize, "text-size");
                SettingsLink(375, "Pause when unfocused", "Pause when you switch to another window.", options.PauseOnFocusLoss ? "On" : "Off",
                    () => { options.PauseOnFocusLoss = !options.PauseOnFocusLoss; MarkSettingsChanged(); }, "auto-pause");
                SettingsLink(483, "Accessible controls", "Open the native interface for screen readers.", "Open controls", OpenAccessibleControls, "accessible");
                break;
            case SystemSection.Profiles:
                Ink(300, 272, usingGameProfile ? "This game has its own settings" : "Using global defaults", 22, bold: true);
                DrawSettingsParagraph(300, 319, "A game profile keeps graphics, audio and keyboard changes with the current game. Unchanged values continue to use global defaults. Controller mappings stay with the controller.", 775, 16);
                ActionButton(300, 453, 400, 46, usingGameProfile ? "Use global defaults" : "Create profile for this game", ToggleGameProfile,
                    enabled: session is not null && stateOperation is null);
                if (session is null) Ink(300, 525, "Open a game first to create its profile.", 16, Colors.Muted);
                break;
            case SystemSection.Firmware:
                Ink(300, 272, "Boot ROM and BIOS", 22, bold: true);
                DrawSettingsParagraph(300, 320, "Import your firmware file, then open a game to use it. Without a matching file, AetherBoy uses its built-in boot process.", 775, 16);
                ActionButton(300, 432, 300, 46, "Import firmware", ShowFirmwareDialog);
                ActionButton(624, 432, 400, 46, options.UseFirmware ? "Use imported firmware: on" : "Use imported firmware: off",
                    () => { options.UseFirmware = !options.UseFirmware; MarkSettingsChanged(); }, options.UseFirmware, focusId: "option:firmware");
                Ink(300, 517, "Changes apply the next time you open a game.", 16, Colors.Muted);
                break;
            case SystemSection.Files:
                SettingsLink(272, "Games", "Choose a ROM from your computer.", "Open a game", ShowRomDialog, "open-rom");
                SettingsLink(380, "AetherBoy data", "Saved games and other app data on this computer.", "Open data folder", () => OpenFolder(dataPaths.Data), "data-folder");
                SettingsLink(488, "Screenshots", "Game images captured with F12.", "Open screenshots", () => OpenFolder(Path.Combine(dataPaths.Data, "screenshots")), "screenshots");
                break;
        }
    }
}
