using AetherBoy.Runtime;
using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop;

// Linux rendering of frmNano.AetherUi / frmControlCenter. The runtime stays shared.
internal sealed partial class WaylandEmulatorHost
{
    private sealed class UiColors(UiThemePalette theme)
    {
        private static SDL.Color From(UiRgb value) => Rgb(value.R, value.G, value.B);
        public readonly SDL.Color Void = From(theme.Background);
        public readonly SDL.Color Chrome = From(theme.Chrome);
        public readonly SDL.Color Surface = From(theme.Surface);
        public readonly SDL.Color Raised = From(theme.Raised);
        public readonly SDL.Color Border = From(theme.Border);
        public readonly SDL.Color Text = From(theme.Text);
        public readonly SDL.Color Muted = From(theme.Muted);
        public readonly SDL.Color Violet = From(theme.PrimaryText);
        public readonly SDL.Color Cyan = From(theme.SecondaryText);
        public readonly SDL.Color Primary = From(theme.Primary);
        public readonly SDL.Color OnPrimary = From(theme.OnPrimary);
        public readonly SDL.Color Danger = From(theme.DangerText);
    }
    private UiColors Colors = new(new UiThemePalette(null, null, null));

    private readonly List<(SDL.FRect Bounds, Action Action)> shellCommands = new();
    private readonly List<SDL.FRect> focusTargets = new();
    private readonly record struct FocusIdentity(string Context, string Action, float X, float Y, float Width, float Height);
    private readonly List<FocusIdentity> focusIdentities = new();
    private FocusIdentity? previousFocus;
    private int focusedControl = -1;
    private string FocusContext => !controlCenterVisible ? "main:" + storage?.Identity
        : $"{controlCenterPage}:{showAppearance}:{showController}:{showBackups}:{showGallery}:{showPatchLab}:{editingTitleIdentity}:{storage?.Identity}";

    private void BeginFocusFrame()
    {
        previousFocus = focusedControl >= 0 && focusedControl < focusIdentities.Count ? focusIdentities[focusedControl] : null;
        focusedControl = -1;
        focusTargets.Clear(); focusIdentities.Clear();
    }

    private void JumpControlCenterRegion()
    {
        bool inContent = focusedControl >= 0 && focusedControl < focusTargets.Count
            && focusTargets[focusedControl].X >= 278 && focusTargets[focusedControl].Y >= 180;
        focusedControl = inContent
            ? focusTargets.FindIndex(target => target.X < 248 && target.Y == 224 + (int)controlCenterPage * 50)
            : focusTargets.FindIndex(target => target.X >= 278 && target.Y >= 180);
    }
    private IntPtr brandTexture;
    private bool mouseTurbo;
    private bool showAppearance;
    private string primaryColorInput = "", secondaryColorInput = "", backgroundColorInput = "";
    private static readonly string[] PageNames = ["Overview", "Video", "Audio", "Controls", "Save states", "System", "Diagnostics", "Library", "Tools"];
    private static readonly string[] PageDescriptions =
    [
        "Your current game and useful shortcuts", "Picture style, palettes and display options",
        "Volume, channels and audio output", "Keyboard mapping and controller setup",
        "Save, load and manage your game data", "Desktop, storage and firmware", "Session health and local reports", "Your games, most recently played first", "Audio recording and game tools"
    ];
    private string CurrentPageName => showAppearance && controlCenterPage == ControlCenterPage.System
        ? "Appearance" : PageNames[(int)controlCenterPage];
    private string CurrentPageDescription => showAppearance && controlCenterPage == ControlCenterPage.System
        ? "Choose logo accents and a background for the whole app" : PageDescriptions[(int)controlCenterPage];

    private static SDL.Color Rgb(byte r, byte g, byte b) => new() { R = r, G = g, B = b, A = 255 };
    private void Paint(float x, float y, float w, float h, SDL.Color color) => Fill(x, y, w, h, color.R, color.G, color.B);
    private void Ink(float x, float y, string text, int size = 14, SDL.Color? color = null, bool bold = false)
    {
        DescribeAccessibleText(x, y, text);
        SDL.Color c = color ?? Colors.Text;
        textRenderer.Draw(x, y, text, c.R, c.G, c.B, size, bold);
    }
    private void Label(float x, float y, string text, int size) => Ink(x, y, text, size);
    private void Center(float center, float y, string text, int size, SDL.Color? color = null, bool bold = false) =>
        Ink(center - textRenderer.Measure(text, size, bold) / 2, y, text, size, color, bold);

    private void LoadBrandMark()
    {
        IntPtr surface = SDL.LoadPNG(Path.Combine(AppContext.BaseDirectory, "Assets", "aetherboy-mark.png"));
        if (surface == IntPtr.Zero) throw new IOException("Cannot load the bundled AetherBoy logo: " + SDL.GetError());
        try
        {
            SDL.SetWindowIcon(window, surface);
            brandTexture = SDL.CreateTextureFromSurface(renderer, surface);
            if (brandTexture == IntPtr.Zero) throw new IOException("Cannot create logo texture: " + SDL.GetError());
            SDL.SetTextureScaleMode(brandTexture, SDL.ScaleMode.Linear);
        }
        finally { SDL.DestroySurface(surface); }
    }

    private void Mark(float x, float y, float size)
    {
        SDL.FRect destination = new() { X = x, Y = y, W = size, H = size };
        SDL.RenderTexture(renderer, brandTexture, IntPtr.Zero, in destination);
    }

    private void RoundedFill(float x, float y, float w, float h, float radius, SDL.Color color)
    {
        Paint(x + radius, y, w - radius * 2, h, color);
        Paint(x, y + radius, w, h - radius * 2, color);
        for (int row = 0; row < radius; row++)
        {
            float inset = radius - MathF.Sqrt(radius * radius - MathF.Pow(radius - row - .5f, 2));
            Paint(x + inset, y + row, w - inset * 2, 1, color);
            Paint(x + inset, y + h - row - 1, w - inset * 2, 1, color);
        }
    }

    private void Panel(float x, float y, float w, float h, string? caption = null, bool stage = false)
    {
        RoundedFill(x, y, w, h, 12, Colors.Border);
        RoundedFill(x + 1, y + 1, w - 2, h - 2, 11, stage ? Colors.Chrome : Colors.Surface);
        if (caption is not null) Ink(x + 22, y + 18, caption, 12, Colors.Muted, true);
    }

    private void ActionButton(float x, float y, float w, float h, string label, Action action,
        bool primary = false, bool enabled = true, string? focusId = null)
    {
        enabled = enabled && (!IsLoading || label == "CANCEL LOAD");
        DrawButton(x, y, w, h, label, primary, enabled, focusId);
        if (enabled) shellCommands.Add((new SDL.FRect { X = x, Y = y, W = w, H = h }, action));
    }

    private void DrawButton(float x, float y, float width, float height, string label, bool selected, bool enabled = true, string? focusId = null)
    {
        enabled = enabled && (!IsLoading || label == "CANCEL LOAD");
        AddAccessibleCommand(x, y, width, height, label, selected, enabled, focusId);
        var identity = new FocusIdentity(FocusContext, focusId ?? label, x, y, width, height);
        bool keyboardFocused = enabled && previousFocus == identity;
        if (keyboardFocused) focusedControl = focusTargets.Count;
        if (enabled)
        {
            focusTargets.Add(new SDL.FRect { X = x, Y = y, W = width, H = height });
            focusIdentities.Add(identity);
        }
        bool hovered = enabled && Hit(mouseX, mouseY, x, y, width, height);
        SDL.Color fill = selected && enabled ? Colors.Primary : hovered ? Colors.Raised : Colors.Surface;
        SDL.Color border = keyboardFocused ? Colors.Cyan : selected && enabled ? Colors.Primary : Colors.Border;
        RoundedFill(x, y, width, height, 8, border);
        RoundedFill(x + 1, y + 1, width - 2, height - 2, 7, fill);
        if (keyboardFocused)
        {
            RoundedFill(x + 3, y + 3, width - 6, height - 6, 5, Colors.Cyan);
            RoundedFill(x + 5, y + 5, width - 10, height - 10, 4, fill);
        }
        drawingButtonLabel = true;
        label = textRenderer.Fit(label, width - 16, 13, true);
        Center(x + width / 2, y + (height - 17) / 2 - 1, label, 12,
            !enabled ? Colors.Muted : selected ? Colors.OnPrimary : Colors.Text, true);
        drawingButtonLabel = false;
    }

    private void OpenControlPage(ControlCenterPage page)
    {
        if (!controlCenterVisible) ToggleControlCenter();
        if (controlCenterVisible) SelectControlCenterPage(page);
    }
    private void Fullscreen()
    {
        isFullscreen = !isFullscreen;
        SDL.SetWindowFullscreen(window, isFullscreen);
    }
    private void HoldMouseTurbo()
    {
        if (session is null || IsOnlineLink || IsLoading || controlCenterVisible) return;
        mouseTurbo = true;
        session.SetTurboAsync(true).GetAwaiter().GetResult();
    }
    private void ReleaseMouseTurbo()
    {
        if (!mouseTurbo) return;
        mouseTurbo = false;
        if (session is not null && !IsOnlineLink)
            session.SetTurboAsync(pressedKeys.Contains(options.Keys[LinuxInputAction.Turbo])).GetAwaiter().GetResult();
    }
    private string StateLabel => IsLoading ? "Loading" : session is null ? "Ready"
        : session.OnlineLink is { } link ? "LINK " + link.Phase.ToString().ToUpperInvariant()
        : session.LatestSnapshot.IsPaused ? "Paused" : "Playing";
    private string CartridgeTitle => romPath is null ? "No game open" : Path.GetFileNameWithoutExtension(romPath);
    private string InputLabel => gamepad == IntPtr.Zero ? "KEYBOARD" : SDL.GetGamepadName(gamepad) ?? "GAMEPAD";
    private string ModelLabel => session?.LatestSnapshot.Rom is not { } rom ? "—"
        : rom.IsGameBoyAdvance ? "GBA" : rom.HasColorFeatures ? "CGB" : "DMG";

    private void UpdateLayout()
    {
        SDL.GetWindowSize(window, out int width, out int height);
        if (width <= 0 || height <= 0) return;
        float scale = Math.Min(width / 1180f, height / 760f);
        textRenderer.MinimumSize = Math.Max(14, (int)Math.Ceiling(12 / scale)) + Math.Clamp(options.TextSize - 14, 0, 4);
        int logicalWidth = (int)Math.Round(width / scale);
        int logicalHeight = (int)Math.Round(height / scale);
        if (logicalWidth == LogicalWidth && logicalHeight == LogicalHeight) return;
        LogicalWidth = logicalWidth;
        LogicalHeight = logicalHeight;
        SDL.SetRenderLogicalPresentation(renderer, LogicalWidth, LogicalHeight, SDL.RendererLogicalPresentation.Letterbox);
    }

    private void DrawShell()
    {
        UpdateLayout();
        PollDiskRefresh(); PollLibraryRefresh(); CompleteStateOperation();
        accessibleCommands.Clear(); accessibleDescriptions.Clear();
        shellCommands.Clear();
        BeginFocusFrame();
        float dx = LogicalWidth - 1180;
        float dy = LogicalHeight - 760;
        float centerX = 442 + dx / 2;
        float centerY = dy / 2;
        Paint(0, 0, LogicalWidth, LogicalHeight, Colors.Void);
        if (controlCenterVisible) { DrawControlCenter(); DrawLoadingOverlay(); return; }
        Paint(0, 0, LogicalWidth, 78, Colors.Chrome);
        Paint(0, 77, LogicalWidth, 1, Colors.Border);
        Mark(20, 12, 50);
        Ink(84, 15, "AetherBoy", 21, bold: true);
        Ink(84, 46, "GAME BOY · COLOR · ADVANCE", 11, Colors.Muted);
        ActionButton(568 + dx, 18, 132, 42, "Library", () => OpenControlPage(ControlCenterPage.Library));
        ActionButton(708 + dx, 18, 132, 42, "Video", () => OpenControlPage(ControlCenterPage.Display));
        ActionButton(848 + dx, 18, 132, 42, "Save states", () => OpenControlPage(ControlCenterPage.Saves));
        ActionButton(988 + dx, 18, 168, 42, "Settings", () => OpenControlPage(ControlCenterPage.Overview));

        Panel(24, 100, 836 + dx, 548 + dy, stage: true);
        Ink(44, 115, "GAME SCREEN", 11, Colors.Muted, true);
        EmulationSnapshot? snapshot = session?.LatestSnapshot;
        if (frameTexture != IntPtr.Zero && snapshot?.HasVideoFrame == true)
        {
            SDL.FRect destination = GetGameDestination(frameGeometry);
            SDL.RenderTexture(renderer, frameTexture, IntPtr.Zero, in destination);
            if (options.VideoFilter == LinuxVideoFilter.LcdGrid) DrawLcdGrid(in destination, frameGeometry);
            if (snapshot.IsPaused)
            {
                RoundedFill(centerX - 64, 345 + centerY, 128, 46, 8, Colors.Surface);
                Center(centerX, 356 + centerY, "Paused", 17, bold: true);
            }
        }
        else
        {
            Mark(centerX - 42, 225 + centerY, 84);
            Center(centerX, 344 + centerY, session is null ? "Ready for your next game?" : "Starting your game…", 23, bold: true);
            Center(centerX, 386 + centerY, session is null ? "Open a game or drop a .gb, .gbc or .gba file here." : "Waiting for the first frame.", 13, Colors.Muted);
            if (session is null) ActionButton(centerX - 96, 452 + centerY, 192, 46, "Open game", ShowRomDialog, true, !IsLoading && fileDialogOpen == 0);
            if (session is null && lastResumeEntry is { } recent)
            {
                ActionButton(centerX - 160, 514 + centerY, 320, 44, "Continue last game", ContinueLastSession, enabled: !IsLoading);
                Center(centerX, 575 + centerY, textRenderer.Fit(recent.Title, 490, 14), 14, Colors.Muted);
            }
            else if (session is null) Center(centerX, 517 + centerY, "Shortcut: O or Ctrl+O", 11, Colors.Muted);
        }

        DrawPerformanceOverlay();
        Panel(880 + dx, 100, 276, 548 + dy, "NOW PLAYING");
        Ink(906 + dx, 158, textRenderer.Fit(CartridgeTitle, 224, 17, true), 17, bold: true);
        Ink(906 + dx, 201, StateLabel, 13, Colors.Muted, true);
        Paint(902 + dx, 240, 232, 1, Colors.Border);
        string audio = !options.AudioEnabled ? "MUTED" : audioError is not null ? "UNAVAILABLE"
            : audioOutput is null ? "READY" : $"{audioOutput.SourceRate / 1000.0:0.0}K · {(audioOutput.Channels == 2 ? "STEREO" : "MONO")}";
        string[] keys = ["System", "Audio", "Video", "Controls"];
        string[] values = [ModelLabel, audio,
            options.VideoFilter == LinuxVideoFilter.LcdGrid ? "LCD GRID" : options.VideoFilter.ToString().ToUpperInvariant(),
            gamepad == IntPtr.Zero ? "KEYBOARD" : "GAMEPAD"];
        for (int i = 0; i < keys.Length; i++)
        {
            Ink(906 + dx, 263 + i * 40, keys[i], 12, Colors.Muted);
            string value = textRenderer.Fit(values[i], 152, 12, true);
            Ink(1134 + dx - textRenderer.Measure(value, 12, true), 263 + i * 40, value, 12, bold: true);
        }
        Ink(906 + dx, 485, "SAVE SLOT", 12, Colors.Muted, true);
        ActionButton(902 + dx, 509, 232, 28, "Manage saves", () => OpenControlPage(ControlCenterPage.Saves));
        for (int i = 0; i < 5; i++)
        {
            int slot = i + 1;
            ActionButton(904 + dx + i * 47, 548, 40, 32, slot.ToString(), () => SelectSaveSlot(slot), options.SaveSlot == slot);
        }
        Ink(906 + dx, 601, IsOnlineLink ? "Private online save" : "F5 save · F8 load", 11, Colors.Muted);
        Ink(906 + dx, 619, textRenderer.Fit(IsOnlineLink ? "F10 connection and saves" : "Hold Tab for turbo", 228, 12), 12, Colors.Muted);
        Ink(24, 659 + dy, textRenderer.Fit(loadError ?? statusMessage, LogicalWidth - 48, 12), 12, loadError is null ? Colors.Muted : Colors.Danger);
        Paint(0, 688 + dy, LogicalWidth, 72, Colors.Chrome);
        Paint(0, 688 + dy, LogicalWidth, 1, Colors.Border);
        bool playable = session is not null && !IsLoading;
        ActionButton(24, 704 + dy, 164, 40, fileDialogOpen != 0 ? "File picker…" : "Open game", ShowRomDialog, enabled: !IsLoading && fileDialogOpen == 0);
        ActionButton(200, 704 + dy, 142, 40, "Settings", () => OpenControlPage(ControlCenterPage.Overview), enabled: !IsLoading);
        ActionButton(354, 704 + dy, 142, 40, snapshot?.IsPaused == true ? "Resume" : "Pause", TogglePause, playable, playable);
        ActionButton(508, 704 + dy, 142, 40, "Rewind", Rewind, enabled: playable && !IsOnlineLink);
        ActionButton(662, 704 + dy, 142, 40, "Save", QuickSave, enabled: playable && !IsOnlineLink);
        ActionButton(816, 704 + dy, 142, 40, "Load", QuickLoad, enabled: playable && HasSelectedState);
        ActionButton(970, 704 + dy, 164 + dx, 40, "Turbo (hold)", HoldMouseTurbo, mouseTurbo, playable && !IsOnlineLink);
        DrawLoadingOverlay();
    }

    private void DrawLoadingOverlay()
    {
        if (!IsLoading) return;
        float centerX = LogicalWidth / 2f, top = LogicalHeight / 2f - 95;
        Panel(centerX - 228, top, 456, 190);
        Center(centerX, top + 22, romPreparationCancellation?.IsCancellationRequested == true ? "CANCELLING…" : "LOADING CARTRIDGE", 19, bold: true);
        Center(centerX, top + 60, textRenderer.Fit(Path.GetFileName(pendingRomPath ?? ""), 410), 14, Colors.Muted);
        Center(centerX, top + 91, "Your current session is kept until ready.", 12, Colors.Muted);
        ActionButton(centerX - 105, top + 128, 210, 42, "CANCEL LOAD", CancelRomLoad,
            enabled: romPreparationCancellation?.IsCancellationRequested == false);
    }

    private void DrawControlCenter()
    {
        Paint(0, 0, LogicalWidth, 84, Colors.Chrome);
        Mark(20, 14, 52);
        Ink(92, 12, "AetherBoy", 12, Colors.Muted, true);
        Ink(92, 29, "Settings", 22, bold: true);
        Ink(206, 37, "Make the game feel right for you.", 12, Colors.Muted);
        ActionButton(LogicalWidth - 66, 23, 42, 36, "×", CloseControlCenter);
        Paint(0, 83, LogicalWidth, 1, Colors.Border);
        Paint(0, 84, 248, LogicalHeight - 84, Colors.Chrome);
        Paint(247, 84, 1, LogicalHeight - 84, Colors.Border);
        Ink(22, 117, "PREFERENCES", 11, Colors.Muted, true);
        Ink(22, 154, "Browse settings", 16, bold: true);
        for (int i = 0; i < PageNames.Length; i++)
        {
            ControlCenterPage page = (ControlCenterPage)i;
            ActionButton(22, 224 + i * 50, 204, 42, PageNames[i], () => SelectControlCenterPage(page), page == controlCenterPage);
        }
        Ink(24, LogicalHeight - 71, "Saved on this computer", 11, Colors.Muted);
        Ink(280, 108, CurrentPageName, 26);
        Ink(280, 150, textRenderer.Fit(CurrentPageDescription + (session is not null && controlCenterPage is ControlCenterPage.Display or ControlCenterPage.Audio or ControlCenterPage.Input ? (usingGameProfile ? " · THIS GAME" : " · GLOBAL SETTINGS") : ""), 850), 13, Colors.Muted);
        if (controlCenterPage != ControlCenterPage.Overview) Panel(278, 180, LogicalWidth - 302, 456);
        switch (controlCenterPage)
        {
            case ControlCenterPage.Overview: DrawOverviewPage(); break;
            case ControlCenterPage.Display: DrawDisplayPage(); break;
            case ControlCenterPage.Audio: DrawAudioPage(); break;
            case ControlCenterPage.Input: DrawInputPage(); break;
            case ControlCenterPage.Saves: DrawSavesPage(); break;
            case ControlCenterPage.System: DrawSystemPage(); break;
            case ControlCenterPage.Diagnostics: DrawDiagnosticsPage(); break;
            case ControlCenterPage.Library: DrawLibraryPage(); break;
            case ControlCenterPage.Tools: DrawToolsPage(); break;
        }
        Ink(280, 648, textRenderer.Fit(loadError ?? statusMessage, 852, 12), 12, loadError is null ? Colors.Muted : Colors.Danger);
        Ink(280, LogicalHeight - 69, textRenderer.Fit("F6: sidebar / page · Tab: next · Ctrl+Tab: section · Enter: select · Esc: back", LogicalWidth - 310, 12), 12, Colors.Muted);
    }

    private void DrawOverviewPage()
    {
        Panel(278, 184, 878, 98, "CURRENT SESSION");
        Ink(302, 224, textRenderer.Fit(session is null ? "Ready to open a cartridge" : $"{StateLabel}  //  {ModelLabel}", 510, 21), 21);
        ActionButton(842, 214, 270, 44, "CONTINUE SESSION", LoadResume, true, StateCard(0) is { Exists: true, Error: null } && stateOperation is null);
        Panel(278, 300, 282, 132, "CARTRIDGE");
        Ink(300, 351, textRenderer.Fit(CartridgeTitle, 240), 14);
        Ink(300, 379, session is null ? "DMG / CGB / GBA READY" : ModelLabel, 14);
        Panel(576, 300, 282, 132, "CONTROLS");
        Ink(598, 351, gamepad == IntPtr.Zero ? "KEYBOARD READY" : "GAMEPAD LIVE", 14);
        Ink(598, 379, textRenderer.Fit(InputLabel.ToUpperInvariant(), 238), 14);
        Panel(874, 300, 282, 132, "SAVE STATUS");
        Ink(896, 351, session is null ? "NO CARTRIDGE" : $"STATE SLOT {options.SaveSlot} / 5", 14);
        Ink(896, 379, session is null ? "NO SAVE ROUTE" : "LOCAL SAVE FILES", 14);
        Panel(278, 450, 878, 168, "QUICK ACCESS");
        ActionButton(300, 497, 264, 42, "INPUT SETTINGS", () => SelectControlCenterPage(ControlCenterPage.Input));
        ActionButton(582, 497, 264, 42, "SAVE CENTER", () => SelectControlCenterPage(ControlCenterPage.Saves));
        ActionButton(864, 497, 270, 42, "AUDIO", () => SelectControlCenterPage(ControlCenterPage.Audio));
        ActionButton(300, 558, 264, 42, "QUICK SAVE", QuickSave, true, session is not null && !IsOnlineLink);
        ActionButton(582, 558, 264, 42, "QUICK LOAD", QuickLoad, enabled: HasSelectedState);
        ActionButton(864, 558, 270, 42, "FULLSCREEN", Fullscreen);
    }

    private void DrawSystemPage()
    {
        if (showAppearance) { DrawAppearancePage(); return; }
        Ink(300, 200, "NATIVE LINUX", 11, Colors.Cyan, true);
        Ink(300, 236, desktop.DisplayName, 20);
        ActionButton(842, 198, 268, 42, $"TEXT: {TextSizeName}", CycleTextSize);
        ActionButton(842, 440, 268, 42, "ACCESSIBLE UI", OpenAccessibleControls);
        ActionButton(842, 506, 268, 44, "APPEARANCE COLORS", OpenAppearancePage);
        Ink(300, 284, "VIDEO, SOUND & KEYBOARD", 14, Colors.Cyan, true);
        ActionButton(300, 322, 810, 44, usingGameProfile ? "THIS GAME HAS ITS OWN SETTINGS · USE GLOBAL DEFAULTS" : "USING GLOBAL SETTINGS · CREATE A PROFILE FOR THIS GAME", ToggleGameProfile, usingGameProfile, session is not null && stateOperation is null);
        Ink(300, 380, usingGameProfile ? "Your changes apply to this game. Unchanged values inherit global defaults." : "Create a profile to keep this game's settings separate from other games.", 14, Colors.Muted);
        ActionButton(300, 440, 504, 42, options.PauseOnFocusLoss ? "AUTO-PAUSE WHEN UNFOCUSED: ON" : "AUTO-PAUSE WHEN UNFOCUSED: OFF", () =>
        { options.PauseOnFocusLoss = !options.PauseOnFocusLoss; MarkSettingsChanged(); }, options.PauseOnFocusLoss);
        ActionButton(300, 506, 242, 44, "OPEN ROM", ShowRomDialog, true, fileDialogOpen == 0 && !IsLoading);
        ActionButton(562, 506, 242, 44, "OPEN DATA FOLDER", () => OpenFolder(dataPaths.Data));
        ActionButton(300, 562, 242, 42, "IMPORT FIRMWARE", ShowFirmwareDialog);
        ActionButton(560, 562, 242, 42, options.UseFirmware ? "FIRMWARE ON" : "BUILT-IN BOOT", () =>
        { options.UseFirmware = !options.UseFirmware; MarkSettingsChanged(); }, options.UseFirmware);
    }

    private void OpenAppearancePage()
    {
        primaryColorInput = options.UiPrimaryColor;
        secondaryColorInput = options.UiSecondaryColor;
        backgroundColorInput = options.UiBackgroundColor;
        showAppearance = true;
        focusedControl = -1;
    }

    private void DrawAppearancePage()
    {
        ActionButton(300, 198, 158, 42, "BACK", () => { CommitActiveText(); showAppearance = false; });
        Ink(482, 206, "Appearance colors", 20);
        Ink(300, 254, "PRIMARY ACCENT · actions and selection", 13, Colors.Muted);
        DrawColorEntry(TextField.ThemePrimary, primaryColorInput, 282);
        Ink(300, 348, "SECONDARY ACCENT · focus and details", 13, Colors.Muted);
        DrawColorEntry(TextField.ThemeSecondary, secondaryColorInput, 376);
        Ink(300, 442, "BACKGROUND · panels follow this color", 13, Colors.Muted);
        DrawColorEntry(TextField.ThemeBackground, backgroundColorInput, 470);
        ActionButton(300, 548, 278, 44, "APPLY COLORS", ApplyAppearanceColors, true);
        ActionButton(598, 548, 278, 44, "RESTORE LOGO COLORS", ResetAppearanceColors);
        Ink(300, 604, "Enter #RRGGBB. Text and button contrast adjust automatically.", 13, Colors.Muted);
    }

    private void DrawColorEntry(TextField field, string input, float y)
    {
        SDL.Color swatch = UiRgb.TryParse(input, out UiRgb color) ? Rgb(color.R, color.G, color.B) : Colors.Border;
        RoundedFill(300, y, 58, 44, 8, Colors.Border);
        RoundedFill(302, y + 2, 54, 40, 6, swatch);
        DrawTextEntry(field, 374, y, 502, 44, "#RRGGBB");
    }

    private void ApplyAppearanceColors()
    {
        CommitActiveText();
        if (!UiRgb.TryParse(primaryColorInput, out UiRgb primary) ||
            !UiRgb.TryParse(secondaryColorInput, out UiRgb secondary) ||
            !UiRgb.TryParse(backgroundColorInput, out UiRgb background))
        { statusMessage = "Use six hexadecimal digits for each color, for example #8B38FF."; return; }
        options.UiPrimaryColor = primaryColorInput = primary.Hex;
        options.UiSecondaryColor = secondaryColorInput = secondary.Hex;
        options.UiBackgroundColor = backgroundColorInput = background.Hex;
        Colors = new UiColors(new UiThemePalette(primary.Hex, secondary.Hex, background.Hex));
        MarkSettingsChanged();
        statusMessage = "Appearance colors saved for the whole app.";
    }

    private void ResetAppearanceColors()
    {
        CommitActiveText();
        primaryColorInput = UiThemePalette.DefaultPrimary;
        secondaryColorInput = UiThemePalette.DefaultSecondary;
        backgroundColorInput = UiThemePalette.DefaultBackground;
        ApplyAppearanceColors();
    }

    private void DrawDiagnosticsPage()
    {
        Ink(300, 200, "SESSION HEALTH", 14, Colors.Cyan, true);
        Ink(300, 238, textRenderer.Fit("Build " + LinuxBuildInfo.Version, 790), 14);
        Ink(300, 273, $"Video: {SDL.GetCurrentVideoDriver()} · VSync: {(vsyncEnabled ? "on" : "unavailable")}", 14);
        Ink(300, 308, $"Audio: {audioOutput?.DriverName ?? "not open"} · Queue: {audioOutput?.QueuedMilliseconds ?? 0:0.0} ms", 14);
        Ink(300, 343, $"{StateLabel} · Frame {displayedFrameSequence} · Dropped audio blocks: {audioOutput?.DroppedBlocks ?? 0}", 14);
        Ink(300, 382, diagnostics.Enabled ? "Local session recording is on." : "Session recording is off.", 14, Colors.Muted);
        Ink(300, 417, textRenderer.Fit(loadError ?? audioError ?? diagnostics.Error ?? "No errors reported.", 790), 14,
            loadError is null && audioError is null ? Colors.Muted : Colors.Danger);
        ActionButton(300, 474, 242, 44, "EXPORT REPORT ZIP", ExportDiagnostics, true, diagnostics.Enabled);
        ActionButton(560, 474, 242, 44, "OPEN REPORTS", () => OpenFolder(dataPaths.State));
        ActionButton(300, 535, 502, 42, options.RecordDiagnostics ? "RECORD NEXT SESSION: ON" : "RECORD NEXT SESSION: OFF", () =>
        { options.RecordDiagnostics = !options.RecordDiagnostics; MarkSettingsChanged(); statusMessage = "Recording preference saved. Applies after restarting AetherBoy."; }, options.RecordDiagnostics);
        Ink(300, 590, "Local technical events only. No ROM/save contents and no uploads.", 14, Colors.Muted);
    }
}
