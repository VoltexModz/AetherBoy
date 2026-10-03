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
        public readonly SDL.Color Secondary = From(theme.Secondary);
        public readonly SDL.Color OnPrimary = From(theme.OnPrimary);
        public readonly SDL.Color ButtonStart = From(theme.Button.Start);
        public readonly SDL.Color ButtonEnd = From(theme.Button.End);
        public readonly SDL.Color ButtonText = From(theme.Button.Text);
        public readonly SDL.Color ButtonHoverStart = From(theme.ButtonHover.Start);
        public readonly SDL.Color ButtonHoverEnd = From(theme.ButtonHover.End);
        public readonly SDL.Color ButtonHoverText = From(theme.ButtonHover.Text);
        public readonly SDL.Color Danger = From(theme.DangerText);
    }
    private UiColors Colors = new(new UiThemePalette(null, null, null));

    private readonly List<(SDL.FRect Bounds, Action Action)> shellCommands = new();
    private readonly List<SDL.FRect> focusTargets = new();
    private readonly record struct FocusIdentity(string Context, string Action, float X, float Y, float Width, float Height);
    private readonly List<FocusIdentity> focusIdentities = new();
    private FocusIdentity? previousFocus;
    private int focusedControl = -1;
    private string FocusContext => archiveSelection is not null ? "archive:" + archiveSelection.Path + ":" + archiveSelectedIndex / ArchivePageSize
        : sofaMode ? "sofa:" + showSofaLibrary + ":" + showQuickDeck + ":" + sofaSection + ":" + sofaPage
        : !controlCenterVisible ? "main:" + storage?.Identity
        : $"{controlCenterPage}:{graphicsSection}:{systemSection}:{showInputShortcuts}:{showControllerStick}:{settingsSearch}:{settingsSearchPage}:{showAppearance}:{showController}:{showBackups}:{showGallery}:{showPatchLab}:{editingTitleIdentity}:{storage?.Identity}";

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
    private static readonly string[] PageNames = ["Overview", "Graphics", "Audio", "Controls", "Save states", "App & files", "Diagnostics", "Library", "Tools"];
    private static readonly string[] PageDescriptions =
    [
        "Choose a topic below, or search for a specific setting", "Choose how your games look on screen",
        "Volume, channels and audio output", "Keyboard mapping and controller setup",
        "Save, load and manage your game data", "Appearance, desktop behavior, game profiles and files", "Session health and local reports", "Your games, most recently played first", "Audio recording and game tools"
    ];
    private string CurrentPageName => ShowingSettingsSearch ? global::AetherBoy.Runtime.Localization.UiText.Get("Search settings") : showAppearance && controlCenterPage == ControlCenterPage.System
        ? global::AetherBoy.Runtime.Localization.UiText.Get("Appearance") : global::AetherBoy.Runtime.Localization.UiText.Get(PageNames[(int)controlCenterPage]);
    private string CurrentPageDescription => ShowingSettingsSearch ? global::AetherBoy.Runtime.Localization.UiText.Get("Search by name or purpose, then open the matching section") : showAppearance && controlCenterPage == ControlCenterPage.System
        ? global::AetherBoy.Runtime.Localization.UiText.Get("Choose colors for the whole app, then select Apply colors") : global::AetherBoy.Runtime.Localization.UiText.Get(PageDescriptions[(int)controlCenterPage]);

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
        if (surface == IntPtr.Zero) throw new IOException(global::AetherBoy.Runtime.Localization.UiText.Get("Cannot load the bundled AetherBoy logo: ") + SDL.GetError());
        try
        {
            SDL.SetWindowIcon(window, surface);
            brandTexture = SDL.CreateTextureFromSurface(renderer, surface);
            if (brandTexture == IntPtr.Zero) throw new IOException(global::AetherBoy.Runtime.Localization.UiText.Get("Cannot create logo texture: ") + SDL.GetError());
            SDL.SetTextureScaleMode(brandTexture, SDL.ScaleMode.Linear);
            SDL.SetTextureBlendMode(brandTexture, SDL.BlendMode.Blend);
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
        Paint(x, y, w, h, Colors.Border);
        Paint(x + 1, y + 1, w - 2, h - 2, stage ? Colors.Void : Colors.Surface);
        AccentLine(x, y + 1, 2, h - 2, vertical: true);
        if (stage) Paint(x + w - 58, y + 22, 38, 2, Colors.Cyan);
        if (caption is not null) Ink(x + 22, y + 18, caption, 12, Colors.Muted, true);
    }

    private void AccentLine(float x, float y, float w, float h, bool vertical = false) =>
        AetherShapeRenderer.Fill(renderer, x, y, w, h, 0, Colors.Primary, Colors.Secondary, vertical);

    private void ChamferFill(float x, float y, float w, float h, float cut, SDL.Color start, SDL.Color? end = null) =>
        AetherShapeRenderer.Fill(renderer, x, y, w, h, cut, start, end ?? start);

    private void ActionButton(float x, float y, float w, float h, string label, Action action,
        bool primary = false, bool enabled = true, string? focusId = null, bool emphasis = false)
    {
        enabled = enabled && (!IsLoading || label == global::AetherBoy.Runtime.Localization.UiText.Get("CANCEL LOAD") || (archiveSelection is not null && focusId?.StartsWith("archive:") == true));
        DrawButton(x, y, w, h, label, primary, enabled, focusId, emphasis);
        if (enabled) shellCommands.Add((new SDL.FRect { X = x, Y = y, W = w, H = h }, action));
    }

    private void DrawButton(float x, float y, float width, float height, string label, bool selected, bool enabled = true, string? focusId = null, bool emphasis = false)
    {
        enabled = enabled && (!IsLoading || label == global::AetherBoy.Runtime.Localization.UiText.Get("CANCEL LOAD") || (archiveSelection is not null && focusId?.StartsWith("archive:") == true));
        AddAccessibleCommand(x, y, width, height, label, selected, enabled, focusId);
        var identity = new FocusIdentity(FocusContext, focusId ?? label, x, y, width, height);
        bool keyboardFocused = enabled && previousFocus == identity;
        if (keyboardFocused) focusedControl = focusTargets.Count;
        if (enabled)
        {
            focusTargets.Add(new SDL.FRect { X = x, Y = y, W = width, H = height });
            focusIdentities.Add(identity);
        }
        bool navigation = focusId?.StartsWith("nav:") == true;
        bool hovered = enabled && Hit(mouseX, mouseY, x, y, width, height);
        bool gradient = enabled && emphasis;
        SDL.Color fill = selected && enabled || hovered ? Colors.Raised : navigation ? Colors.Chrome : Colors.Raised;
        SDL.Color first = gradient ? hovered ? Colors.ButtonHoverStart : Colors.ButtonStart : fill;
        SDL.Color last = gradient ? hovered ? Colors.ButtonHoverEnd : Colors.ButtonEnd : fill;
        SDL.Color foreground = !enabled ? Colors.Muted : gradient ? hovered ? Colors.ButtonHoverText : Colors.ButtonText : Colors.Text;
        SDL.Color border = keyboardFocused || selected && enabled ? Colors.Cyan
            : hovered ? Colors.Violet : gradient ? Colors.Primary : navigation ? Colors.Chrome : Colors.Border;
        ChamferFill(x, y, width, height, 8, border);
        ChamferFill(x + 1, y + 1, width - 2, height - 2, 7, first, last);
        if (!gradient && enabled && (selected || hovered))
            Paint(x + 4, y + 5, 2, Math.Max(1, height - 14), selected ? Colors.Cyan : Colors.Violet);
        if (keyboardFocused && width > 12 && height > 12)
        {
            // Keep keyboard/controller focus visible even on a bright accent ramp.
            SDL.SetRenderDrawColor(renderer, foreground.R, foreground.G, foreground.B, 255);
            SDL.FRect focus = new() { X = x + 5, Y = y + 5, W = width - 10, H = height - 10 };
            SDL.RenderRect(renderer, in focus);
        }
        drawingButtonLabel = true;
        label = textRenderer.Fit(label, width - 16, 13, true);
        if (navigation) Ink(x + 16, y + (height - 20) / 2, label, 15, foreground, selected);
        else Center(x + width / 2, y + (height - 17) / 2 - 1, label, 12,
            foreground, true);
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
    private string StateLabel => IsLoading ? global::AetherBoy.Runtime.Localization.UiText.Get("Loading") : session is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Ready")
        : session.OnlineLink is { } link ? global::AetherBoy.Runtime.Localization.UiLabels.Link(link.Phase)
        : session.LatestSnapshot.IsPaused ? global::AetherBoy.Runtime.Localization.UiText.Get("Paused") : global::AetherBoy.Runtime.Localization.UiText.Get("Playing");
    private string CartridgeTitle => romPath is null ? global::AetherBoy.Runtime.Localization.UiText.Get("No game open") : Path.GetFileNameWithoutExtension(romPath);
    private string InputLabel => gamepad == IntPtr.Zero ? global::AetherBoy.Runtime.Localization.UiText.Get("KEYBOARD") : SDL.GetGamepadName(gamepad) ?? global::AetherBoy.Runtime.Localization.UiText.Get("GAMEPAD");
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
        PollDiskRefresh(); PollLibraryRefresh(); PollLibraryPreview(); CompleteStateOperation();
        accessibleCommands.Clear(); accessibleDescriptions.Clear();
        shellCommands.Clear();
        textEntryBounds.Clear();
        BeginFocusFrame();
        float dx = LogicalWidth - 1180;
        float dy = LogicalHeight - 760;
        float centerX = 442 + dx / 2;
        float centerY = dy / 2;
        Paint(0, 0, LogicalWidth, LogicalHeight, Colors.Void);
        if (introClock is not null) { DrawBootIntro(); return; }
        if (archiveSelection is not null) { DrawArchiveSelection(); return; }
        if (sofaMode)
        {
            if (showSofaLibrary) { DrawSofaLibrary(); DrawLoadingOverlay(); }
            else DrawSofaGame();
            return;
        }
        if (controlCenterVisible) { DrawControlCenter(); DrawLoadingOverlay(); return; }
        if (showLocalLinkPage) { DrawLocalLinkPage(); return; }
        Paint(0, 0, LogicalWidth, 78, Colors.Chrome);
        AccentLine(0, 76, LogicalWidth, 2);
        Mark(20, 12, 50);
        Ink(84, 15, "AetherBoy", 21, bold: true);
        Ink(84, 46, "GAME BOY · COLOR · ADVANCE", 11, Colors.Muted);
        ActionButton(568 + dx, 18, 132, 42, global::AetherBoy.Runtime.Localization.UiText.Get("Library"), () => OpenControlPage(ControlCenterPage.Library));
        ActionButton(392 + dx, 18, 164, 42, global::AetherBoy.Runtime.Localization.UiText.Get("Sofa mode"), OpenSofaLibrary);
        ActionButton(708 + dx, 18, 132, 42, global::AetherBoy.Runtime.Localization.UiText.Get("Graphics"), () => OpenControlPage(ControlCenterPage.Display));
        ActionButton(848 + dx, 18, 132, 42, global::AetherBoy.Runtime.Localization.UiText.Get("Save states"), () => OpenControlPage(ControlCenterPage.Saves));
        ActionButton(988 + dx, 18, 168, 42, global::AetherBoy.Runtime.Localization.UiText.Get("Settings"), () => OpenControlPage(ControlCenterPage.Overview));

        Panel(24, 100, 836 + dx, 548 + dy, stage: true);
        Ink(44, 115, global::AetherBoy.Runtime.Localization.UiText.Get("GAME SCREEN"), 11, Colors.Muted, true);
        EmulationSnapshot? snapshot = session?.LatestSnapshot;
        if (frameTexture != IntPtr.Zero && snapshot?.HasVideoFrame == true)
        {
            SDL.FRect destination = GetGameDestination(frameGeometry);
            SDL.RenderTexture(renderer, frameTexture, IntPtr.Zero, in destination);
            if (options.VideoFilter == LinuxVideoFilter.LcdGrid) DrawLcdGrid(in destination, frameGeometry);
            if (snapshot.IsPaused)
            {
                ChamferFill(centerX - 64, 345 + centerY, 128, 46, 8, Colors.Surface);
                Center(centerX, 356 + centerY, global::AetherBoy.Runtime.Localization.UiText.Get("Paused"), 17, bold: true);
            }
        }
        else
        {
            Mark(centerX - 42, 225 + centerY, 84);
            Center(centerX, 344 + centerY, session is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Ready for your next game?") : global::AetherBoy.Runtime.Localization.UiText.Get("Starting your game…"), 23, bold: true);
            Center(centerX, 386 + centerY, session is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Open a game or drop a .gb, .gbc or .gba file here.") : global::AetherBoy.Runtime.Localization.UiText.Get("Waiting for the first frame."), 13, Colors.Muted);
            if (session is null) ActionButton(centerX - 96, 452 + centerY, 192, 46, global::AetherBoy.Runtime.Localization.UiText.Get("Open game"), ShowRomDialog, true, !IsLoading && fileDialogOpen == 0, emphasis: true);
            if (session is null && lastResumeEntry is { } recent)
            {
                ActionButton(centerX - 160, 514 + centerY, 320, 44, global::AetherBoy.Runtime.Localization.UiText.Get("Continue last game"), ContinueLastSession, enabled: !IsLoading);
                Center(centerX, 575 + centerY, textRenderer.Fit(recent.Title, 490, 14), 14, Colors.Muted);
            }
            else if (session is null) Center(centerX, 517 + centerY, global::AetherBoy.Runtime.Localization.UiText.Get("Shortcut: O or Ctrl+O"), 11, Colors.Muted);
        }

        DrawPerformanceOverlay();
        Panel(880 + dx, 100, 276, 548 + dy, global::AetherBoy.Runtime.Localization.UiText.Get("NOW PLAYING"));
        Ink(906 + dx, 158, textRenderer.Fit(CartridgeTitle, 224, 17, true), 17, bold: true);
        Ink(906 + dx, 201, StateLabel, 13, Colors.Muted, true);
        Paint(902 + dx, 240, 232, 1, Colors.Border);
        string audio = !options.AudioEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("MUTED") : audioError is not null ? global::AetherBoy.Runtime.Localization.UiText.Get("UNAVAILABLE")
            : audioOutput is null ? global::AetherBoy.Runtime.Localization.UiText.Get("READY") : $"{audioOutput.SourceRate / 1000.0:0.0}K · {(audioOutput.Channels == 2 ? "STEREO" : "MONO")}";
        string[] keys = [global::AetherBoy.Runtime.Localization.UiText.Get("System"), "Audio", global::AetherBoy.Runtime.Localization.UiText.Get("Video"), global::AetherBoy.Runtime.Localization.UiText.Get("Controls")];
        string[] values = [ModelLabel, audio,
            options.VideoFilter == LinuxVideoFilter.LcdGrid ? global::AetherBoy.Runtime.Localization.UiText.Get("LCD GRID") : global::AetherBoy.Runtime.Localization.UiText.Get(options.VideoFilter == LinuxVideoFilter.Sharp ? "SHARP" : "SMOOTH"),
            gamepad == IntPtr.Zero ? global::AetherBoy.Runtime.Localization.UiText.Get("KEYBOARD") : global::AetherBoy.Runtime.Localization.UiText.Get("GAMEPAD")];
        for (int i = 0; i < keys.Length; i++)
        {
            Ink(906 + dx, 263 + i * 40, keys[i], 12, Colors.Muted);
            string value = textRenderer.Fit(values[i], 152, 12, true);
            Ink(1134 + dx - textRenderer.Measure(value, 12, true), 263 + i * 40, value, 12, bold: true);
        }
        if (session is not null && !IsOnlineLink)
        {
            Ink(906 + dx, 446, global::AetherBoy.Runtime.Localization.UiText.Get("THIS SESSION"), 12, Colors.Muted);
            string duration = global::AetherBoy.Runtime.Localization.UiText.Format("{0}h {1}m", (int)(playedSeconds / 3600), (int)(playedSeconds / 60) % 60);
            Ink(1134 + dx - textRenderer.Measure(duration, 12, true), 446, duration, 12, bold: true);
        }
        Ink(906 + dx, 485, global::AetherBoy.Runtime.Localization.UiText.Get("SAVE SLOT"), 12, Colors.Muted, true);
        ActionButton(902 + dx, 509, 232, 28, global::AetherBoy.Runtime.Localization.UiText.Get("Manage saves"), () => OpenControlPage(ControlCenterPage.Saves));
        for (int i = 0; i < 5; i++)
        {
            int slot = i + 1;
            ActionButton(904 + dx + i * 47, 548, 40, 32, slot.ToString(), () => SelectSaveSlot(slot), options.SaveSlot == slot);
        }
        Ink(906 + dx, 601, IsOnlineLink ? global::AetherBoy.Runtime.Localization.UiText.Get("Private online save") : global::AetherBoy.Runtime.Localization.UiText.Get("F5 save · F8 load"), 11, Colors.Muted);
        Ink(906 + dx, 619, textRenderer.Fit(IsOnlineLink ? global::AetherBoy.Runtime.Localization.UiText.Get("F10 connection and saves") : global::AetherBoy.Runtime.Localization.UiText.Get("Hold Tab for turbo"), 228, 12), 12, Colors.Muted);
        Ink(24, 659 + dy, textRenderer.Fit(loadError ?? statusMessage, LogicalWidth - 48, 12), 12, loadError is null ? Colors.Muted : Colors.Danger);
        Paint(0, 688 + dy, LogicalWidth, 72, Colors.Chrome);
        AccentLine(0, 688 + dy, LogicalWidth, 2);
        bool playable = session is not null && !IsLoading;
        ActionButton(24, 704 + dy, 164, 40, fileDialogOpen != 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("File picker…") : global::AetherBoy.Runtime.Localization.UiText.Get("Open game"), ShowRomDialog, enabled: !IsLoading && fileDialogOpen == 0, emphasis: true);
        ActionButton(200, 704 + dy, 142, 40, global::AetherBoy.Runtime.Localization.UiText.Get("Settings"), () => OpenControlPage(ControlCenterPage.Overview), enabled: !IsLoading);
        ActionButton(354, 704 + dy, 142, 40, snapshot?.IsPaused == true ? global::AetherBoy.Runtime.Localization.UiText.Get("Resume") : "Pause", TogglePause, playable, playable);
        ActionButton(508, 704 + dy, 142, 40, global::AetherBoy.Runtime.Localization.UiText.Get("Rewind"), Rewind, enabled: playable && !IsOnlineLink);
        ActionButton(662, 704 + dy, 142, 40, global::AetherBoy.Runtime.Localization.UiText.Get("Save"), QuickSave, enabled: playable && !IsOnlineLink);
        ActionButton(816, 704 + dy, 142, 40, global::AetherBoy.Runtime.Localization.UiText.Get("Load"), QuickLoad, enabled: playable && HasSelectedState);
        ActionButton(970, 704 + dy, 164 + dx, 40, global::AetherBoy.Runtime.Localization.UiText.Get("Turbo (hold)"), HoldMouseTurbo, mouseTurbo, playable && !IsOnlineLink);
        if (showQuickDeck) DrawQuickDeck();
        DrawLoadingOverlay();
    }

    private void DrawLoadingOverlay()
    {
        if (!IsLoading) return;
        float centerX = LogicalWidth / 2f, top = LogicalHeight / 2f - 95;
        Panel(centerX - 228, top, 456, 190);
        Center(centerX, top + 22, romPreparationCancellation?.IsCancellationRequested == true ? global::AetherBoy.Runtime.Localization.UiText.Get("CANCELLING…") : global::AetherBoy.Runtime.Localization.UiText.Get("LOADING CARTRIDGE"), 19, bold: true);
        Center(centerX, top + 60, textRenderer.Fit(Path.GetFileName(pendingRomPath ?? ""), 410), 14, Colors.Muted);
        Center(centerX, top + 91, global::AetherBoy.Runtime.Localization.UiText.Get("Your current session is kept until ready."), 12, Colors.Muted);
        ActionButton(centerX - 105, top + 128, 210, 42, global::AetherBoy.Runtime.Localization.UiText.Get("CANCEL LOAD"), CancelRomLoad,
            enabled: romPreparationCancellation?.IsCancellationRequested == false);
    }

    private void DrawControlCenter()
    {
        Paint(0, 0, LogicalWidth, 84, Colors.Chrome);
        Mark(20, 14, 52);
        Ink(92, 12, "AetherBoy", 12, Colors.Muted, true);
        Ink(92, 29, global::AetherBoy.Runtime.Localization.UiText.Get("Settings"), 22, bold: true);
        Ink(92, 60, global::AetherBoy.Runtime.Localization.UiText.Get("Find settings with Ctrl+K"), 12, Colors.Muted);
        ActionButton(LogicalWidth - 66, 23, 42, 36, "×", CloseControlCenter);
        AccentLine(0, 82, LogicalWidth, 2);
        Paint(0, 84, 248, LogicalHeight - 84, Colors.Chrome);
        Paint(247, 84, 1, LogicalHeight - 84, Colors.Border);
        Ink(22, 117, global::AetherBoy.Runtime.Localization.UiText.Get("Find a setting"), 16, bold: true);
        DrawTextEntry(TextField.SettingsSearch, 22, 164, 204, 42, global::AetherBoy.Runtime.Localization.UiText.Get("Search settings"));
        for (int i = 0; i < PageNames.Length; i++)
        {
            ControlCenterPage page = (ControlCenterPage)i;
            ActionButton(22, 224 + i * 50, 204, 42, global::AetherBoy.Runtime.Localization.UiText.Get(PageNames[i]), () => SelectControlCenterPage(page), !ShowingSettingsSearch && page == controlCenterPage, focusId: "nav:" + page);
        }
        DrawSettingsParagraph(24, LogicalHeight - 78, settingsDirty || settingsWrite is not null ? global::AetherBoy.Runtime.Localization.UiText.Get("Saving changes…") : global::AetherBoy.Runtime.Localization.UiText.Get("Changes save automatically"), 200, 12);
        Ink(280, 108, CurrentPageName, 26);
        Ink(280, 150, textRenderer.Fit(CurrentPageDescription, LogicalWidth - 310), 14, Colors.Muted);
        Panel(278, 180, LogicalWidth - 302, 456);
        if (ShowingSettingsSearch) DrawSettingsResults();
        else
        switch (controlCenterPage)
        {
            case ControlCenterPage.Overview: DrawSettingsOverview(); break;
            case ControlCenterPage.Display: DrawDisplayPage(); break;
            case ControlCenterPage.Audio: DrawAudioPage(); break;
            case ControlCenterPage.Input: DrawInputPage(); break;
            case ControlCenterPage.Saves: DrawSavesPage(); break;
            case ControlCenterPage.System: DrawSystemSettings(); break;
            case ControlCenterPage.Diagnostics: DrawDiagnosticsPage(); break;
            case ControlCenterPage.Library: DrawLibraryPage(); break;
            case ControlCenterPage.Tools: DrawToolsPage(); break;
        }
        if (showOnScreenKeyboard) DrawOnScreenKeyboard();
        string scope = controlCenterPage is ControlCenterPage.Display or ControlCenterPage.Audio or ControlCenterPage.Input
            ? ShowingSettingsSearch || showInputShortcuts ? "" : showController ? global::AetherBoy.Runtime.Localization.UiText.Get("Controller mappings are saved for this device.")
            : usingGameProfile ? global::AetherBoy.Runtime.Localization.UiText.Get("Changes apply to this game’s profile.") : global::AetherBoy.Runtime.Localization.UiText.Get("Changes apply to global defaults.")
            : "";
        DrawSettingsParagraph(280, 648, loadError ?? (string.IsNullOrEmpty(statusMessage) ? scope : statusMessage), LogicalWidth - 310, 12,
            loadError is null ? Colors.Muted : Colors.Danger);
        DrawSettingsParagraph(280, LogicalHeight - 59, global::AetherBoy.Runtime.Localization.UiText.Get("F6 switches region. Tab moves focus; Ctrl+Tab changes section. Enter selects; Esc returns."), LogicalWidth - 310, 12);
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
        ActionButton(300, 198, 158, 42, global::AetherBoy.Runtime.Localization.UiText.Get("Back"), () => { CommitActiveText(); showAppearance = false; });
        Ink(482, 206, global::AetherBoy.Runtime.Localization.UiText.Get("App colors"), 20);
        ActionButton(906, 198, 204, 42, global::AetherBoy.Runtime.Localization.UiText.Format("Text: {0}", TextSizeName), CycleTextSize);
        Ink(300, 251, global::AetherBoy.Runtime.Localization.UiText.Get("Choose a theme"), 14, Colors.Text, true);
        var selected = UiThemePresets.Match(options.UiPrimaryColor, options.UiSecondaryColor, options.UiBackgroundColor);
        for (int i = 0; i < UiThemePresets.All.Count; i++)
        {
            UiThemePreset preset = UiThemePresets.All[i];
            float x = 300 + i % 3 * 274, y = 278 + i / 3 * 49;
            ActionButton(x, y, 260, 40, preset.Name, () => SelectThemePreset(preset), selected?.Id == preset.Id);
            if (UiRgb.TryParse(preset.Primary, out UiRgb swatch))
                RoundedFill(x + 8, y + 9, 22, 22, 5, Rgb(swatch.R, swatch.G, swatch.B));
        }
        Ink(300, 383, global::AetherBoy.Runtime.Localization.UiText.Get("Or enter your own colors"), 14, Colors.Text, true);
        Ink(300, 410, global::AetherBoy.Runtime.Localization.UiText.Get("Primary"), 12, Colors.Muted);
        Ink(574, 410, global::AetherBoy.Runtime.Localization.UiText.Get("Secondary"), 12, Colors.Muted);
        Ink(848, 410, global::AetherBoy.Runtime.Localization.UiText.Get("Background"), 12, Colors.Muted);
        DrawCompactColorEntry(TextField.ThemePrimary, primaryColorInput, 300, 433);
        DrawCompactColorEntry(TextField.ThemeSecondary, secondaryColorInput, 574, 433);
        DrawCompactColorEntry(TextField.ThemeBackground, backgroundColorInput, 848, 433);
        ActionButton(300, 502, 278, 44, global::AetherBoy.Runtime.Localization.UiText.Get("Apply custom colors"), ApplyAppearanceColors, emphasis: true);
        ActionButton(598, 502, 278, 44, global::AetherBoy.Runtime.Localization.UiText.Get("Restore Aether Original"), ResetAppearanceColors);
        Ink(300, 568, selected is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Custom colors are active.") : selected.Name + global::AetherBoy.Runtime.Localization.UiText.Get(" is active."), 13, Colors.Muted);
        Ink(300, 595, global::AetherBoy.Runtime.Localization.UiText.Get("Your existing colors stay unchanged until you choose a theme or apply new ones."), 12, Colors.Muted);
    }

    private void DrawCompactColorEntry(TextField field, string input, float x, float y)
    {
        SDL.Color swatch = UiRgb.TryParse(input, out UiRgb color) ? Rgb(color.R, color.G, color.B) : Colors.Border;
        RoundedFill(x, y, 44, 44, 8, Colors.Border);
        RoundedFill(x + 2, y + 2, 40, 40, 6, swatch);
        DrawTextEntry(field, x + 50, y, 210, 44, "#RRGGBB");
    }

    private void SelectThemePreset(UiThemePreset preset)
    {
        CommitActiveText();
        primaryColorInput = preset.Primary;
        secondaryColorInput = preset.Secondary;
        backgroundColorInput = preset.Background;
        ApplyAppearanceColors();
        statusMessage = preset.Name + global::AetherBoy.Runtime.Localization.UiText.Get(" applied to the whole app.");
    }

    private void ApplyAppearanceColors()
    {
        CommitActiveText();
        if (!UiRgb.TryParse(primaryColorInput, out UiRgb primary) ||
            !UiRgb.TryParse(secondaryColorInput, out UiRgb secondary) ||
            !UiRgb.TryParse(backgroundColorInput, out UiRgb background))
        { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Use six hexadecimal digits for each color, for example #8B38FF."); return; }
        options.UiPrimaryColor = primaryColorInput = primary.Hex;
        options.UiSecondaryColor = secondaryColorInput = secondary.Hex;
        options.UiBackgroundColor = backgroundColorInput = background.Hex;
        Colors = new UiColors(new UiThemePalette(primary.Hex, secondary.Hex, background.Hex));
        MarkSettingsChanged();
        statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Appearance colors saved for the whole app.");
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
        Ink(300, 200, global::AetherBoy.Runtime.Localization.UiText.Get("SESSION HEALTH"), 14, Colors.Cyan, true);
        Ink(300, 238, textRenderer.Fit("Build " + LinuxBuildInfo.Version, 790), 14);
        Ink(300, 273, global::AetherBoy.Runtime.Localization.UiText.Format("Video: {0} · VSync: {1}", SDL.GetCurrentVideoDriver(), global::AetherBoy.Runtime.Localization.UiText.Get(vsyncEnabled ? "On" : "Unavailable")), 14);
        Ink(300, 308, global::AetherBoy.Runtime.Localization.UiText.Format("Audio: {0} · Queue: {1:0.0} ms", audioOutput?.DriverName ?? global::AetherBoy.Runtime.Localization.UiText.Get("Not open"), audioOutput?.QueuedMilliseconds ?? 0), 14);
        Ink(300, 343, global::AetherBoy.Runtime.Localization.UiText.Format("{0}. Presented frame {1}; dropped audio blocks: {2}.", StateLabel, displayedFrameSequence, audioOutput?.DroppedBlocks ?? 0), 14);
        Ink(300, 382, diagnostics.Enabled ? global::AetherBoy.Runtime.Localization.UiText.Get("Local session recording is on.") : global::AetherBoy.Runtime.Localization.UiText.Get("Session recording is off."), 14, Colors.Muted);
        Ink(300, 417, textRenderer.Fit(latestHealthHint is null ? global::AetherBoy.Runtime.Localization.UiText.Get("No progress issue suspected.")
            : global::AetherBoy.Runtime.Localization.UiText.Get("Last suspected issue: ") + global::AetherBoy.Runtime.Localization.UiLabels.Health(latestHealthHint) + global::AetherBoy.Runtime.Localization.UiText.Get(". This is not a confirmed fault."), 790), 14,
            latestHealthHint is null ? Colors.Muted : Colors.Danger);
        ActionButton(300, 474, 242, 44, global::AetherBoy.Runtime.Localization.UiText.Get("EXPORT REPORT ZIP"), ExportDiagnostics, true, diagnostics.Enabled);
        ActionButton(560, 474, 242, 44, global::AetherBoy.Runtime.Localization.UiText.Get("OPEN REPORTS"), () => OpenFolder(dataPaths.State));
        ActionButton(820, 474, 290, 44, global::AetherBoy.Runtime.Localization.UiText.Get("MARK PROBLEM NOW"), MarkSessionProblem, enabled: diagnostics.Enabled);
        ActionButton(300, 535, 502, 42, options.RecordDiagnostics ? global::AetherBoy.Runtime.Localization.UiText.Get("RECORD NEXT SESSION: ON") : global::AetherBoy.Runtime.Localization.UiText.Get("RECORD NEXT SESSION: OFF"), () =>
        { options.RecordDiagnostics = !options.RecordDiagnostics; MarkSettingsChanged(); statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Recording preference saved. Applies after restarting AetherBoy."); }, options.RecordDiagnostics);
        Ink(300, 590, global::AetherBoy.Runtime.Localization.UiText.Get("Local technical events only. No ROM/save contents and no uploads."), 14, Colors.Muted);
    }

    private void MarkSessionProblem()
    {
        long now = Environment.TickCount64;
        if (lastProblemMarkerAt != long.MinValue && now - lastProblemMarkerAt < 2000) return;
        lastProblemMarkerAt = now;
        LinuxHealthSample? sample = latestHealthSample;
        diagnostics.Record("session.problem_marked", new
        {
            source = "user", state = sample?.State.ToString(), emulated_frames = sample?.EmulatedFrames,
            video_frames = sample?.VideoFrames, presented_frames = sample?.PresentedFrames,
            audio_frames = sample?.AudioFrames ?? Interlocked.Read(ref audioFramesObserved), suppressed = sample?.Suppressed,
            local_link_frames = localLinkSession?.LatestSnapshot.FrameCount
        });
        statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Problem time marked in the local report. Export the ZIP when you are ready.");
    }
}
