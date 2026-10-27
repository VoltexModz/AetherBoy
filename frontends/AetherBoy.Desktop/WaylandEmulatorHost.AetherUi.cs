using AetherBoy.Runtime;
using nanoboy.Core;
using SDL3;
using UiText = AetherBoy.Runtime.Localization.UiText;

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
        public readonly SDL.Color Selection = From(theme.Selection);
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
    private string FocusContext => showMessageDetails ? "message:details" : archiveSelection is not null ? "archive:" + archiveSelection.Path + ":" + archiveSelectedIndex / ArchivePageSize
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
            && focusTargets[focusedControl].X >= 278;
        focusedControl = inContent
            ? focusIdentities.FindIndex(identity => identity.Action == "nav:settings:" + ActiveSettingsSection)
            : focusTargets.FindIndex(target => target.X >= 278 && target.Y >= 180);
    }
    private IntPtr brandTexture;
    private IntPtr brandTexture64, brandTexture128;
    private string? brandVariant;
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

    private void LoadBrandMark(string? primary = null, string? secondary = null)
    {
        string variant = UiThemePresets.ResolveBrandVariant(primary ?? options.UiPrimaryColor, secondary ?? options.UiSecondaryColor);
        if (variant == brandVariant && brandTexture != IntPtr.Zero) return;
        IntPtr replacement = IntPtr.Zero, replacement64 = IntPtr.Zero, replacement128 = IntPtr.Zero;
        try
        {
            replacement = LoadBrandTexture(variant, 512);
            replacement64 = LoadBrandTexture(variant, 64);
            replacement128 = LoadBrandTexture(variant, 128);
            // Launcher/window icons retain the original identity on both platforms.
            if (brandTexture == IntPtr.Zero)
            {
                IntPtr icon = SDL.LoadPNG(Path.Combine(AppContext.BaseDirectory, "Assets", "aetherboy-mark.png"));
                if (icon != IntPtr.Zero)
                    try { SDL.SetWindowIcon(window, icon); }
                    finally { SDL.DestroySurface(icon); }
            }
            IntPtr previous = brandTexture, previous64 = brandTexture64, previous128 = brandTexture128;
            brandTexture = replacement;
            brandTexture64 = replacement64;
            brandTexture128 = replacement128;
            replacement = replacement64 = replacement128 = IntPtr.Zero;
            brandVariant = variant;
            if (previous != IntPtr.Zero) SDL.DestroyTexture(previous);
            if (previous64 != IntPtr.Zero) SDL.DestroyTexture(previous64);
            if (previous128 != IntPtr.Zero) SDL.DestroyTexture(previous128);
        }
        finally
        {
            if (replacement != IntPtr.Zero) SDL.DestroyTexture(replacement);
            if (replacement64 != IntPtr.Zero) SDL.DestroyTexture(replacement64);
            if (replacement128 != IntPtr.Zero) SDL.DestroyTexture(replacement128);
        }
    }

    private IntPtr LoadBrandTexture(string variant, int size)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Themes");
        if (size != 512) directory = Path.Combine(directory, size.ToString(System.Globalization.CultureInfo.InvariantCulture));
        IntPtr surface = SDL.LoadPNG(Path.Combine(directory, variant + ".png"));
        if (surface == IntPtr.Zero) throw new IOException(global::AetherBoy.Runtime.Localization.UiText.Get("Cannot load the bundled AetherBoy logo: ") + SDL.GetError());
        try
        {
            IntPtr texture = SDL.CreateTextureFromSurface(renderer, surface);
            if (texture == IntPtr.Zero) throw new IOException(global::AetherBoy.Runtime.Localization.UiText.Get("Cannot create logo texture: ") + SDL.GetError());
            SDL.SetTextureScaleMode(texture, SDL.ScaleMode.Linear);
            SDL.SetTextureBlendMode(texture, SDL.BlendMode.Blend);
            return texture;
        }
        finally { SDL.DestroySurface(surface); }
    }

    private void Mark(float x, float y, float size)
    {
        SDL.FRect destination = new() { X = x, Y = y, W = size, H = size };
        // Pre-filtered sizes avoid undersampling thin strokes when SDL reduces
        // the 512-pixel intro artwork to a tiny title-bar logo. Account for DPI.
        float pixels = size;
        if (SDL.GetCurrentRenderOutputSize(renderer, out int outputWidth, out int outputHeight))
            pixels *= Math.Min(outputWidth / (float)LogicalWidth, outputHeight / (float)LogicalHeight);
        IntPtr texture = pixels <= 64 ? brandTexture64 : pixels <= 128 ? brandTexture128 : brandTexture;
        SDL.RenderTexture(renderer, texture, IntPtr.Zero, in destination);
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

        if (stage) Paint(x + w - 58, y + 22, 38, 2, Colors.Cyan);
        if (caption is not null) Ink(x + 22, y + 18, caption, 12, Colors.Muted, true);
    }

    private void AccentLine(float x, float y, float w, float h, bool vertical = false) =>
        AetherShapeRenderer.Fill(renderer, x, y, w, h, 0, Colors.Primary, Colors.Secondary, vertical);

    private void ChamferFill(float x, float y, float w, float h, float cut, SDL.Color start, SDL.Color? end = null) =>
        AetherShapeRenderer.Fill(renderer, x, y, w, h, cut, start, end ?? start);

    private void ActionButton(float x, float y, float w, float h, string label, Action action,
        bool primary = false, bool enabled = true, string? focusId = null, bool emphasis = false,
        string? displayLabel = null, float labelRightInset = 0)
    {
        enabled = enabled && (!IsLoading || label == global::AetherBoy.Runtime.Localization.UiText.Get("CANCEL LOAD") || (archiveSelection is not null && focusId?.StartsWith("archive:") == true));
        DrawButton(x, y, w, h, label, primary, enabled, focusId, emphasis, displayLabel, labelRightInset);
        if (enabled) shellCommands.Add((new SDL.FRect { X = x, Y = y, W = w, H = h }, action));
    }

    private void DrawButton(float x, float y, float width, float height, string label, bool selected, bool enabled = true, string? focusId = null, bool emphasis = false,
        string? displayLabel = null, float labelRightInset = 0)
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
        bool quiet = navigation || focusId?.StartsWith("window:") == true;
        bool hovered = enabled && Hit(mouseX, mouseY, x, y, width, height);
        bool gradient = enabled && emphasis;
        SDL.Color fill = selected && enabled ? Colors.Selection : hovered ? Colors.Raised : quiet ? Colors.Chrome : Colors.Surface;
        SDL.Color first = gradient ? hovered ? Colors.ButtonHoverStart : Colors.ButtonStart : fill;
        SDL.Color last = gradient ? hovered ? Colors.ButtonHoverEnd : Colors.ButtonEnd : fill;
        SDL.Color foreground = !enabled ? Colors.Muted : gradient ? hovered ? Colors.ButtonHoverText : Colors.ButtonText : Colors.Text;
        SDL.Color border = keyboardFocused ? Colors.Cyan : selected && enabled ? Colors.Selection
            : hovered ? Colors.Violet : gradient ? Colors.Primary : quiet ? Colors.Chrome : Colors.Border;
        ChamferFill(x, y, width, height, gradient ? 8 : 0, border);
        ChamferFill(x + 1, y + 1, width - 2, height - 2, gradient ? 7 : 0, first, last);
        if (!gradient && enabled && selected)
            Paint(x + 4, y + 5, 2, Math.Max(1, height - 14), selected ? Colors.Cyan : Colors.Violet);
        if (keyboardFocused && width > 12 && height > 12)
        {
            // Keep keyboard/controller focus visible even on a bright accent ramp.
            SDL.SetRenderDrawColor(renderer, foreground.R, foreground.G, foreground.B, 255);
            SDL.FRect focus = new() { X = x + 5, Y = y + 5, W = width - 10, H = height - 10 };
            SDL.RenderRect(renderer, in focus);
        }
        drawingButtonLabel = true;
        label = textRenderer.Fit(displayLabel ?? label, width - 16 - labelRightInset, 13, true);
        if (navigation) Ink(x + 16, y + (height - 20) / 2, label, 15, foreground, selected);
        else Center(x + (width - labelRightInset) / 2, y + (height - 17) / 2 - 1, label, 12,
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
        float scale = Math.Min(1, Math.Min(width / 1180f, height / 760f));
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
        if (showMessageDetails) { DrawMessageDetails(); return; }
        if (archiveSelection is not null) { DrawArchiveSelection(); return; }
        if (sofaMode)
        {
            if (showSofaLibrary) { DrawSofaLibrary(); DrawLoadingOverlay(); }
            else DrawSofaGame();
            return;
        }
        if (controlCenterVisible) { DrawControlCenter(); DrawLoadingOverlay(); return; }
        if (showLocalLinkPage) { DrawLocalLinkPage(); return; }
        DrawDesktopChrome(UiText.Get("Spielen"), "play");
        var layout = UiDesktopLayout.Create(LogicalWidth, LogicalHeight);
        EmulationSnapshot? snapshot = session?.LatestSnapshot;
        if (snapshot?.Rom is null)
        {
            float cx = layout.Navigation + (LogicalWidth - layout.Navigation) / 2, cy = LogicalHeight / 2f - 155;
            Mark(cx - 45, cy, 90);
            Center(cx, cy + 112, UiText.Get(IsLoading ? "Starting your game…" : "Öffne ein Spiel"), 28, bold: true);
            Center(cx, cy + 168, UiText.Get("Wähle eine ROM-Datei oder ziehe sie in dieses Fenster."), 14, Colors.Muted);
            DesktopButton(cx - 190, cy + 208, 196, 44, UiText.Get("Spiel öffnen"), UiIcon.Open, ShowRomDialog, emphasis: true, enabled: !IsLoading && fileDialogOpen == 0);
            DesktopButton(cx + 18, cy + 208, 172, 44, UiText.Get("Zur Bibliothek"), UiIcon.Library, () => OpenControlPage(ControlCenterPage.Library));
            Center(cx, cy + 272, UiText.Get("Dateiformate: .gb, .gbc, .gba"), 12, Colors.Muted);
            if (lastResumeEntry is not null)
                DesktopButton(cx - 160, cy + 312, 320, 44, UiText.Get("Continue last game"), UiIcon.Play, ContinueLastSession);
            else Center(cx, cy + 336, UiText.Get("Strg + O   Spiel öffnen"), 12, Colors.Muted);
        }
        else
        {
            Ink(layout.ContentX, 64 + layout.Inset, textRenderer.Fit(CartridgeTitle, layout.ContentWidth - 180, 28, true), 28, bold: true);
            Ink(layout.ContentX, 107 + layout.Inset, snapshot.Rom.IsGameBoyAdvance ? "Game Boy Advance" : snapshot.Rom.HasColorFeatures ? "Game Boy Color" : "Game Boy", 13, Colors.Muted);
            Ink(LogicalWidth - layout.Inset - 156, 80 + layout.Inset, textRenderer.Fit(StateLabel, 156, 12), 12, Colors.Cyan);
            Paint(layout.GameX, layout.GameY, layout.GameWidth, layout.GameHeight, Colors.Chrome);
            if (frameTexture != IntPtr.Zero && snapshot.HasVideoFrame)
            {
                SDL.FRect destination = GetGameDestination(frameGeometry);
                SDL.RenderTexture(renderer, frameTexture, IntPtr.Zero, in destination);
                if (options.VideoFilter == LinuxVideoFilter.LcdGrid) DrawLcdGrid(in destination, frameGeometry);
            }
            float transportY = layout.GameY + layout.GameHeight + 16;
            bool playable = !IsLoading && stateOperation is null;
            DesktopButton(layout.GameX, transportY, 156, 44, snapshot.IsPaused ? UiText.Get("Resume") : "Pause", UiIcon.Play, TogglePause, emphasis: true, enabled: playable, id: "desktop:pause");
            DesktopButton(layout.GameX + 168, transportY, 44, 44, UiText.Get("Rewind"), UiIcon.Rewind, Rewind, iconOnly: true, enabled: playable && !IsOnlineLink && snapshot.Supports(EmulationFeature.Rewind));
            DesktopButton(layout.GameX + 224, transportY, 44, 44, UiText.Get("Turbo (hold)"), UiIcon.Turbo, HoldMouseTurbo, iconOnly: true, enabled: playable && !IsOnlineLink);
            if (layout.GameWidth > 480)
            {
                Icon(layout.GameX + layout.GameWidth - 140, transportY + 13, UiIcon.Volume, Colors.Muted);
                Ink(layout.GameX + layout.GameWidth - 112, transportY + 13, options.AudioEnabled ? options.AudioVolume + " %" : UiText.Get("Muted"), 12, Colors.Muted);
            }
            DesktopButton(layout.GameX + layout.GameWidth - 44, transportY, 44, 44, UiText.Get("Vollbild"), UiIcon.Fullscreen, Fullscreen, iconOnly: true);
            float x = layout.RailX, y = layout.GameY, w = layout.RailWidth, inset = 16;
            Paint(x, y, w, LogicalHeight - 32 - layout.Inset - y, Colors.Surface);
            Icon(x + inset, y + 18, UiIcon.Save, Colors.Violet);
            Ink(x + 44, y + 16, UiText.Get("Spielzustand"), 16, bold: true);
            AetherSelectionButton(x + inset, y + 54, w - 32, 44, UiText.Format("Platz {0}", options.SaveSlot), "desktop-slot",
                Enumerable.Range(1, 5).Select(slot => new AetherChoice(slot.ToString(), UiText.Format("Platz {0}", slot))).ToArray(), options.SaveSlot.ToString(), value => SelectSaveSlot(int.Parse(value)));
            Ink(x + inset, y + 106, UiText.Get(HasSelectedState ? "gespeichert" : "leer"), 12, Colors.Muted);
            DesktopButton(x + inset, y + 140, w - 32, 44, UiText.Get("Zustand speichern"), UiIcon.Save, QuickSave, enabled: playable && !IsOnlineLink && snapshot.Supports(EmulationFeature.SaveStates), id: "desktop:save");
            DesktopButton(x + inset, y + 196, w - 32, 44, UiText.Get("Zustand laden"), UiIcon.Rewind, QuickLoad, enabled: playable && HasSelectedState && !IsOnlineLink, id: "desktop:load");
            Ink(x + inset, y + 256, UiText.Get("F5 save · F8 load"), 11, Colors.Muted);
            Paint(x + inset, y + 286, w - 32, 1, Colors.Border);
            Ink(x + inset, y + 302, UiText.Get("Anzeige"), 14, bold: true);
            Ink(x + inset, y + 340, UiText.Get("Bildfilter"), 12, Colors.Muted);
            Ink(x + w - 94, y + 340, UiText.Get(options.VideoFilter == LinuxVideoFilter.Sharp ? "Sharp" : options.VideoFilter == LinuxVideoFilter.LcdGrid ? "LCD grid" : "Smooth"), 12);
            DesktopButton(x + inset, y + 374, w - 32, 44, UiText.Get("Manage saves"), UiIcon.Save, () => OpenControlPage(ControlCenterPage.Saves));
            DesktopButton(x + inset, Math.Max(y + 430, LogicalHeight - 32 - layout.Inset - 60), w - 32, 44, UiText.Get("Weitere Aktionen"), UiIcon.More, ToggleQuickDeck, enabled: !IsOnlineLink);
            DrawPerformanceOverlay();
        }
        if (!string.IsNullOrEmpty(loadError ?? statusMessage))
        {
            Paint(0, LogicalHeight - 32, LogicalWidth, 32, Colors.Chrome);
            DrawMessageStatus(24, LogicalHeight - 32, LogicalWidth - 48, 32, loadError ?? statusMessage, loadError is not null, singleLine: true);
        }
        if (aetherSelection is not null) DrawAetherSelectionPopup();
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
        if (!ShowingSettingsSearch && controlCenterPage == ControlCenterPage.Library && !showPatchLab && editingTitleIdentity is null && !libraryGrid)
        { DrawDesktopLibrary(); return; }
        if (!ShowingSettingsSearch && controlCenterPage == ControlCenterPage.System && showAppearance)
        { DrawDesktopAppearance(); return; }
        DrawSettingsChrome();
        Ink(280, 96, CurrentPageName, 28, bold: true);
        Ink(280, 139, textRenderer.Fit(CurrentPageDescription, LogicalWidth - 312), 14, Colors.Muted);
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
        DrawMessageStatus(280, LogicalHeight - 76,
            LogicalWidth - 312, 36, loadError ?? (string.IsNullOrEmpty(statusMessage) ? scope : statusMessage), loadError is not null, singleLine: true);
        if (aetherSelection is not null) DrawAetherSelectionPopup();
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
        LoadBrandMark(primary.Hex, secondary.Hex);
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
