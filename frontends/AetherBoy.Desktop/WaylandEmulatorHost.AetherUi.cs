using AetherBoy.Runtime;
using SDL3;

namespace AetherBoy.Desktop;

// Linux rendering of frmNano.AetherUi / frmControlCenter. The runtime stays shared.
internal sealed partial class WaylandEmulatorHost
{
    private static class Colors
    {
        public static readonly SDL.Color Void = Rgb(5, 7, 18);
        public static readonly SDL.Color Chrome = Rgb(8, 11, 24);
        public static readonly SDL.Color Surface = Rgb(12, 16, 31);
        public static readonly SDL.Color Raised = Rgb(17, 22, 41);
        public static readonly SDL.Color Border = Rgb(47, 55, 83);
        public static readonly SDL.Color Text = Rgb(241, 244, 255);
        public static readonly SDL.Color Muted = Rgb(139, 148, 177);
        public static readonly SDL.Color Violet = Rgb(164, 92, 255);
        public static readonly SDL.Color Cyan = Rgb(41, 226, 237);
        public static readonly SDL.Color Danger = Rgb(255, 92, 132);
    }

    private readonly List<(SDL.FRect Bounds, Action Action)> shellCommands = new();
    private readonly List<SDL.FRect> focusTargets = new();
    private readonly record struct FocusIdentity(string Context, string Action, float X, float Y, float Width, float Height);
    private readonly List<FocusIdentity> focusIdentities = new();
    private FocusIdentity? previousFocus;
    private int focusedControl = -1;
    private string FocusContext => !controlCenterVisible ? "main:" + storage?.Identity
        : $"{controlCenterPage}:{showController}:{showBackups}:{showGallery}:{showPatchLab}:{editingTitleIdentity}:{storage?.Identity}";

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
    private static readonly string[] PageNames = ["OVERVIEW", "DISPLAY", "AUDIO", "INPUT", "SAVES", "SYSTEM", "DIAGNOSTICS", "LIBRARY", "TOOLS"];
    private static readonly string[] PageDescriptions =
    [
        "Your session and frequently used actions", "Picture style, palettes and display options",
        "Volume, channels and audio output", "Keyboard mapping and controller setup",
        "Save slots, battery data and recovery", "Desktop, storage and firmware", "Session health and local reports", "Your cartridges, most recently played first", "Audio recording and session cheats"
    ];

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

    private void Panel(float x, float y, float w, float h, string? caption = null, bool stage = false)
    {
        Paint(x, y, w, h, Colors.Border);
        Paint(x + 1, y + 1, w - 2, h - 2, stage ? Colors.Void : Colors.Surface);
        SDL.FColor violet = new() { R = 164 / 255f, G = 92 / 255f, B = 1, A = 1 };
        SDL.FColor cyan = new() { R = 41 / 255f, G = 226 / 255f, B = 237 / 255f, A = 1 };
        ReadOnlySpan<SDL.Vertex> edge =
        [
            new() { Position = new() { X = x, Y = y + 1 }, Color = violet },
            new() { Position = new() { X = x + 2, Y = y + 1 }, Color = violet },
            new() { Position = new() { X = x + 2, Y = y + h - 1 }, Color = cyan },
            new() { Position = new() { X = x, Y = y + h - 1 }, Color = cyan },
        ];
        ReadOnlySpan<int> indices = [0, 1, 2, 0, 2, 3];
        SDL.RenderGeometry(renderer, IntPtr.Zero, edge, edge.Length, indices, indices.Length);
        if (caption is not null) Ink(x + 22, y + 18, caption, 11, Colors.Cyan, true);
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
        float cut = Math.Min(9, height / 4);
        // The same six-point chamfer and horizontal violet/cyan gradient as AetherButton.
        SDL.Color flat = hovered ? Rgb(27, 34, 57) : Colors.Raised;
        SDL.FColor ColorAt(float t)
        {
            SDL.Color c = selected && enabled
                ? Rgb((byte)(164 - 123 * t), (byte)(92 + 134 * t), (byte)(255 - 18 * t)) : flat;
            return new SDL.FColor { R = c.R / 255f, G = c.G / 255f, B = c.B / 255f, A = 1 };
        }
        Span<SDL.Vertex> vertices = stackalloc SDL.Vertex[6];
        vertices[0] = new() { Position = new() { X = x, Y = y }, Color = ColorAt(0) };
        vertices[1] = new() { Position = new() { X = x + width - cut, Y = y }, Color = ColorAt((width - cut) / width) };
        vertices[2] = new() { Position = new() { X = x + width, Y = y + cut }, Color = ColorAt(1) };
        vertices[3] = new() { Position = new() { X = x + width, Y = y + height }, Color = ColorAt(1) };
        vertices[4] = new() { Position = new() { X = x + cut, Y = y + height }, Color = ColorAt(cut / width) };
        vertices[5] = new() { Position = new() { X = x, Y = y + height - cut }, Color = ColorAt(0) };
        ReadOnlySpan<int> indices = [0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5];
        SDL.RenderGeometry(renderer, IntPtr.Zero, vertices, vertices.Length, indices, indices.Length);
        SDL.Color edge = hovered ? Colors.Cyan : Colors.Border;
        SDL.SetRenderDrawColor(renderer, edge.R, edge.G, edge.B, 255);
        ReadOnlySpan<SDL.FPoint> points =
        [
            new() { X = x, Y = y }, new() { X = x + width - cut, Y = y },
            new() { X = x + width, Y = y + cut }, new() { X = x + width, Y = y + height },
            new() { X = x + cut, Y = y + height }, new() { X = x, Y = y + height - cut }, new() { X = x, Y = y }
        ];
        for (int i = 0; i < points.Length - 1; i++)
            SDL.RenderLine(renderer, points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y);
        if (keyboardFocused)
        {
            SDL.SetRenderDrawColor(renderer, Colors.Cyan.R, Colors.Cyan.G, Colors.Cyan.B, 255);
            SDL.FRect focus = new() { X = x + 4, Y = y + 4, W = width - 8, H = height - 8 };
            SDL.RenderRect(renderer, in focus);
        }
        drawingButtonLabel = true;
        label = textRenderer.Fit(label, width - 16, 12, true);
        Center(x + width / 2, y + (height - 17) / 2 - 1, label, 12,
            !enabled ? Colors.Muted : selected ? Colors.Void : Colors.Text, true);
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
        if (session is null || IsLoading || controlCenterVisible) return;
        mouseTurbo = true;
        session.SetTurboAsync(true).GetAwaiter().GetResult();
    }
    private void ReleaseMouseTurbo()
    {
        if (!mouseTurbo) return;
        mouseTurbo = false;
        if (session is not null)
            session.SetTurboAsync(pressedKeys.Contains(options.Keys[LinuxInputAction.Turbo])).GetAwaiter().GetResult();
    }
    private string StateLabel => IsLoading ? "LOADING" : session is null ? "IDLE"
        : session.LatestSnapshot.IsPaused ? "PAUSED" : "PLAYING";
    private string CartridgeTitle => romPath is null ? "NO CARTRIDGE" : Path.GetFileNameWithoutExtension(romPath);
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
        Mark(20, 12, 52);
        Ink(86, 16, "AETHERBOY", 21, bold: true);
        Ink(86, 47, "GB · GBC · GBA", 10, Colors.Muted, true);
        for (int i = 0; i < 3; i++) Paint(330 + i * 5, 22 + i * 8, 25, 3, Colors.Violet);
        Paint(330, 76, 110, 2, Colors.Violet);
        ActionButton(568 + dx, 18, 132, 42, "LIBRARY", () => OpenControlPage(ControlCenterPage.Library));
        ActionButton(708 + dx, 18, 132, 42, "DISPLAY", () => OpenControlPage(ControlCenterPage.Display));
        ActionButton(848 + dx, 18, 132, 42, "SAVES", () => OpenControlPage(ControlCenterPage.Saves));
        ActionButton(988 + dx, 18, 168, 42, "REPORTS", () => OpenControlPage(ControlCenterPage.Diagnostics));

        Panel(24, 100, 836 + dx, 548 + dy, stage: true);
        Ink(44, 113, $"DISPLAY // {frameGeometry.Width} × {frameGeometry.Height}", 10, Colors.Muted, true);
        Paint(798 + dx, 121, 40, 2, Colors.Cyan);
        EmulationSnapshot? snapshot = session?.LatestSnapshot;
        if (frameTexture != IntPtr.Zero && snapshot?.HasVideoFrame == true)
        {
            SDL.FRect destination = GetGameDestination(frameGeometry);
            SDL.RenderTexture(renderer, frameTexture, IntPtr.Zero, in destination);
            if (options.VideoFilter == LinuxVideoFilter.LcdGrid) DrawLcdGrid(in destination, frameGeometry);
            if (snapshot.IsPaused)
            {
                Paint(centerX - 64, 345 + centerY, 128, 46, Colors.Surface);
                Center(centerX, 356 + centerY, "PAUSED", 17, bold: true);
            }
        }
        else
        {
            Mark(centerX - 56, 223 + centerY, 112);
            Center(centerX, 350 + centerY, session is null ? "Ready to play" : "Starting cartridge…", 23, bold: true);
            Center(centerX, 391 + centerY, session is null ? "Drop a .gb, .gbc or .gba file here." : "Waiting for the first frame.", 13, Colors.Muted);
            if (session is null) ActionButton(centerX - 96, 452 + centerY, 192, 46, "OPEN ROM", ShowRomDialog, true, !IsLoading && fileDialogOpen == 0);
            if (session is null && lastResumeEntry is { } recent)
            {
                ActionButton(centerX - 160, 514 + centerY, 320, 44, "CONTINUE LAST SESSION", ContinueLastSession, enabled: !IsLoading);
                Center(centerX, 575 + centerY, textRenderer.Fit(recent.Title, 490, 14), 14, Colors.Muted);
            }
            else if (session is null) Center(centerX, 516 + centerY, "O / CTRL+O  ·  OPEN A CARTRIDGE", 11, Colors.Violet, true);
        }

        Panel(880 + dx, 100, 276, 548 + dy, "CURRENT SESSION");
        Ink(906 + dx, 158, textRenderer.Fit(CartridgeTitle, 224, 17, true), 17, bold: true);
        Ink(906 + dx, 201, StateLabel, 13, Colors.Muted, true);
        Paint(902 + dx, 240, 232, 1, Colors.Border);
        string audio = !options.AudioEnabled ? "MUTED" : audioError is not null ? "UNAVAILABLE"
            : audioOutput is null ? "READY" : $"{audioOutput.SourceRate / 1000.0:0.0}K · {(audioOutput.Channels == 2 ? "STEREO" : "MONO")}";
        string[] keys = ["MODEL", "STATE", "FRAME", "AUDIO", "FILTER", "INPUT", "SLOT"];
        string[] values = [ModelLabel, StateLabel, session is null ? "—" : displayedFrameSequence.ToString(), audio,
            options.VideoFilter == LinuxVideoFilter.LcdGrid ? "LCD GRID" : options.VideoFilter.ToString().ToUpperInvariant(), InputLabel.ToUpperInvariant(), options.SaveSlot.ToString()];
        for (int i = 0; i < keys.Length; i++)
        {
            Ink(906 + dx, 253 + i * 31, keys[i], 10, Colors.Muted, true);
            string value = textRenderer.Fit(values[i], 152, 12, true);
            Ink(1134 + dx - textRenderer.Measure(value, 12, true), 250 + i * 31, value, 12, bold: true);
        }
        Ink(906 + dx, 485 + dy, "STATE BANK", 10, Colors.Muted, true);
        ActionButton(902 + dx, 509 + dy, 232, 28, "SAVE CENTER", () => OpenControlPage(ControlCenterPage.Saves));
        for (int i = 0; i < 5; i++)
        {
            int slot = i + 1;
            ActionButton(904 + dx + i * 47, 548 + dy, 40, 32, slot.ToString(), () => SelectSaveSlot(slot), options.SaveSlot == slot);
        }
        Ink(906 + dx, 601 + dy, $"{BindingLabel(LinuxInputAction.Turbo).ToUpperInvariant()} HOLD · TURBO", 10, Colors.Muted, true);
        Ink(906 + dx, 619 + dy, textRenderer.Fit("F6 ACTIONS · F5/F8 SAVE", 228, 12, true), 12, Colors.Muted, true);
        Ink(24, 659 + dy, textRenderer.Fit(loadError ?? statusMessage, LogicalWidth - 48, 12), 12, loadError is null ? Colors.Muted : Colors.Danger);
        Paint(1, 688 + dy, LogicalWidth - 2, 71, Colors.Border);
        Paint(2, 689 + dy, LogicalWidth - 4, 69, Colors.Chrome);
        bool playable = session is not null && !IsLoading;
        ActionButton(24, 704 + dy, 164, 40, fileDialogOpen != 0 ? "PICKER OPEN…" : "OPEN ROM", ShowRomDialog, true, !IsLoading && fileDialogOpen == 0);
        ActionButton(200, 704 + dy, 142, 40, "SETTINGS", () => OpenControlPage(ControlCenterPage.Overview), enabled: !IsLoading);
        ActionButton(354, 704 + dy, 142, 40, snapshot?.IsPaused == true ? "RESUME" : "PAUSE", TogglePause, enabled: playable);
        ActionButton(508, 704 + dy, 142, 40, "REWIND", Rewind, enabled: playable);
        ActionButton(662, 704 + dy, 142, 40, "SAVE", QuickSave, enabled: playable);
        ActionButton(816, 704 + dy, 142, 40, "LOAD", QuickLoad, enabled: playable && HasSelectedState);
        ActionButton(970, 704 + dy, 164 + dx, 40, "TURBO (HOLD)", HoldMouseTurbo, mouseTurbo, playable);
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
        Mark(20, 14, 54);
        Ink(92, 10, "AETHERBOY SETTINGS", 10, Colors.Cyan, true);
        Ink(92, 27, "Aether Control Center", 22);
        Ink(92, 51, "Display, sound, controls and save data — all stored on this computer.", 12, Colors.Muted);
        ActionButton(LogicalWidth - 66, 23, 42, 36, "×", CloseControlCenter);
        Paint(0, 82, LogicalWidth, 2, Colors.Cyan);
        Paint(0, 82, 248, 2, Colors.Violet);
        Paint(0, 84, 248, LogicalHeight - 84, Colors.Chrome);
        Paint(247, 84, 1, LogicalHeight - 84, Colors.Border);
        Ink(22, 109, "PREFERENCES", 11, Colors.Cyan, true);
        Ink(26, 143, "SETTINGS", 22);
        Ink(26, 176, "CENTER", 22);
        for (int i = 0; i < PageNames.Length; i++)
        {
            ControlCenterPage page = (ControlCenterPage)i;
            ActionButton(22, 224 + i * 50, 204, 42, $"{i + 1:00}  {PageNames[i]}", () => SelectControlCenterPage(page));
            if (page == controlCenterPage)
            {
                Paint(25, 230 + i * 50, 2, 30, Colors.Cyan);
                Paint(30, 265 + i * 50, 178, 1, Colors.Violet);
            }
        }
        Ink(24, LogicalHeight - 87, "LOCAL CONTROL", 10, Colors.Muted, true);
        Ink(24, LogicalHeight - 66, "ON THIS COMPUTER", 10, Colors.Muted, true);
        Ink(280, 108, PageNames[(int)controlCenterPage], 26);
        Ink(280, 150, textRenderer.Fit(PageDescriptions[(int)controlCenterPage] + (session is not null && controlCenterPage is ControlCenterPage.Display or ControlCenterPage.Audio or ControlCenterPage.Input ? (usingGameProfile ? " · THIS GAME" : " · GLOBAL SETTINGS") : ""), 850), 13, Colors.Muted);
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
        ActionButton(300, 558, 264, 42, "QUICK SAVE", QuickSave, true, session is not null);
        ActionButton(582, 558, 264, 42, "QUICK LOAD", QuickLoad, enabled: HasSelectedState);
        ActionButton(864, 558, 270, 42, "FULLSCREEN", Fullscreen);
    }

    private void DrawSystemPage()
    {
        Ink(300, 200, "NATIVE LINUX", 11, Colors.Cyan, true);
        Ink(300, 236, desktop.DisplayName, 20);
        ActionButton(842, 198, 268, 42, $"TEXT: {TextSizeName}", CycleTextSize);
        ActionButton(842, 440, 268, 42, "ACCESSIBLE UI", OpenAccessibleControls);
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
