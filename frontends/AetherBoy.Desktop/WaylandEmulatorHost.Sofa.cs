using AetherBoy.Runtime;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private bool sofaMode, showSofaLibrary, fullscreenBeforeSofa, sofaResumeOnClose, sofaAwaitNeutral;
    private SofaLibrarySection sofaSection;
    private int sofaPage, sofaStickDirection;
    private long sofaStickRepeatAt;
    private EmulationSession? sofaPausedSession;
    private string? sofaNotice;

    private void OpenSofaLibrary()
    {
        if (IsLoading || IsOnlineLink || localLinkSession is not null || localLinkStartupTask is not null || stateOperation is not null)
        { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("End the link session or wait for the current operation before opening sofa mode."); return; }
        if (controlCenterVisible) { CloseControlCenter(); if (controlCenterVisible) return; }
        if (showQuickDeck) CloseQuickDeck();
        if (!sofaMode)
        {
            fullscreenBeforeSofa = isFullscreen;
            if (!isFullscreen && !SDL.SetWindowFullscreen(window, true))
            { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Fullscreen could not be opened. Check your desktop settings and try again."); return; }
            isFullscreen = true; sofaMode = true;
        }
        sofaPausedSession = session;
        sofaResumeOnClose = session is { } active && !active.LatestSnapshot.IsPaused;
        if (session is { } owner)
        {
            owner.SetTurboAsync(false).GetAwaiter().GetResult();
            owner.SetPausedAsync(true).GetAwaiter().GetResult();
        }
        mouseTurbo = false; pressedKeys.Clear(); audioOutput?.Clear();
        showSofaLibrary = true; sofaAwaitNeutral = true; focusedControl = -1;
        sofaNotice = null;
        RefreshLibrary();
        statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Choose a game. Favorites and your sofa selection are stored separately.");
    }

    private void CloseSofaLibrary()
    {
        if (IsLoading || stateOperation is not null) return;
        showSofaLibrary = false; sofaAwaitNeutral = true; pressedKeys.Clear(); focusedControl = -1;
        if (sofaResumeOnClose && ReferenceEquals(session, sofaPausedSession) && session?.State == SessionState.Paused)
            session.SetPausedAsync(false).GetAwaiter().GetResult();
        sofaPausedSession = null; sofaResumeOnClose = false;
    }

    private void ExitSofaMode()
    {
        if (!sofaMode || IsLoading || stateOperation is not null) return;
        if (showQuickDeck) CloseQuickDeck();
        CloseSofaLibrary();
        sofaMode = false;
        if (SDL.SetWindowFullscreen(window, fullscreenBeforeSofa)) isFullscreen = fullscreenBeforeSofa;
        else statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Sofa mode ended, but the desktop could not restore the previous fullscreen setting.");
    }

    private void StartSofaGame(LinuxLibraryEntry entry)
    {
        if (entry.Identity == storage?.Identity) { CloseSofaLibrary(); return; }
        BeginRomLoad(entry.Path);
    }

    private void DrawSofaLibrary()
    {
        float width = LogicalWidth, height = LogicalHeight, cardWidth = (width - 96) / 3;
        Mark(30, 20, 62); Ink(108, 23, "AetherBoy", 28, Colors.Cyan, true);
        Ink(108, 62, global::AetherBoy.Runtime.Localization.UiText.Get("Sofa mode"), 18, Colors.Muted);
        string[] tabs = [global::AetherBoy.Runtime.Localization.UiText.Get("Recently played"), global::AetherBoy.Runtime.Localization.UiText.Get("Favorites"), global::AetherBoy.Runtime.Localization.UiText.Get("My selection"), global::AetherBoy.Runtime.Localization.UiText.Get("All games")];
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            ActionButton(30 + i * (width - 60) / 4, 106, (width - 60) / 4 - 8, 54, tabs[i],
                () => { sofaSection = (SofaLibrarySection)index; sofaPage = 0; focusedControl = -1; }, (int)sofaSection == i, focusId: "sofa:tab:" + i);
        }
        var games = SofaLibrary.Select(libraryEntries.Select(entry => new SofaLibraryGame(entry.Identity, entry.Title, entry.System,
            entry.LastPlayed == default ? null : new DateTimeOffset(entry.LastPlayed), entry.Favorite, entry.SofaSelected,
            existingLibraryFiles.Contains(entry.Identity))), sofaSection);
        sofaPage = SofaLibrary.ClampPage(sofaPage, games.Length);
        int column = 0;
        foreach (var game in games.Skip(sofaPage * SofaLibrary.PageSize).Take(SofaLibrary.PageSize))
        {
            var entry = libraryEntries.First(item => item.Identity == game.Id);
            float x = 30 + column++ * (cardWidth + 18), bottom = height - 255;
            Panel(x, 222, cardWidth, height - 392);
            DrawLibraryPreview(entry.Identity, x + 18, 240, cardWidth - 36, Math.Max(40, height - 586));
            Ink(x + 18, bottom - 42, entry.System, 18, Colors.Cyan, true);
            // Full title is available to accessibility; two visual lines avoid tiny card text.
            DrawSettingsParagraph(x + 18, bottom, entry.Title, cardWidth - 36, 18);
            ActionButton(x, height - 170, cardWidth, 48, game.Available ? global::AetherBoy.Runtime.Localization.UiText.Get("Play game") : global::AetherBoy.Runtime.Localization.UiText.Get("File missing"),
                () => StartSofaGame(entry), true, game.Available, "sofa:play:" + game.Id);
            ActionButton(x, height - 114, cardWidth / 2 - 4, 42, game.Favorite ? global::AetherBoy.Runtime.Localization.UiText.Get("Unfavorite") : global::AetherBoy.Runtime.Localization.UiText.Get("Favorite"),
                () => ChangeLibraryEntry(game.Id, old => old with { Favorite = !old.Favorite }), game.Favorite,
                libraryMutation is null, "sofa:favorite:" + game.Id);
            ActionButton(x + cardWidth / 2 + 4, height - 114, cardWidth / 2 - 4, 42, game.Selected ? global::AetherBoy.Runtime.Localization.UiText.Get("Remove selection") : global::AetherBoy.Runtime.Localization.UiText.Get("Select for sofa"),
                () => ChangeLibraryEntry(game.Id, old => old with { SofaSelected = !old.SofaSelected }), game.Selected,
                libraryMutation is null, "sofa:select:" + game.Id);
        }
        if (games.Length == 0)
            DrawSettingsParagraph(40, 220, libraryRefresh is not null ? global::AetherBoy.Runtime.Localization.UiText.Get("Reading your library…") : libraryEntries.Count == 0
                ? global::AetherBoy.Runtime.Localization.UiText.Get("No games yet. Leave sofa mode and open a ROM first.")
                : global::AetherBoy.Runtime.Localization.UiText.Get("No games in this section. Open All games to choose favorites or build your sofa selection."), width - 80, 23);
        ActionButton(width - 410, 24, 178, 50, global::AetherBoy.Runtime.Localization.UiText.Get("Back to game"), CloseSofaLibrary, enabled: session is not null, focusId: "sofa:resume");
        ActionButton(width - 220, 24, 190, 50, global::AetherBoy.Runtime.Localization.UiText.Get("Leave sofa mode"), ExitSofaMode, focusId: "sofa:exit");
        ActionButton(30, height - 58, 144, 40, global::AetherBoy.Runtime.Localization.UiText.Get("Previous"), () => { sofaPage--; focusedControl = -1; }, enabled: sofaPage > 0);
        ActionButton(186, height - 58, 144, 40, global::AetherBoy.Runtime.Localization.UiText.Get("Next"), () => { sofaPage++; focusedControl = -1; }, enabled: (sofaPage + 1) * 3 < games.Length);
        Ink(348, height - 53, global::AetherBoy.Runtime.Localization.UiText.Format("Page {0} · D-pad / stick: move · A: select · B: back", sofaPage + 1), 14, Colors.Muted);
        Ink(348, height - 30, global::AetherBoy.Runtime.Localization.UiText.Get("F10 or L3+R3: game menu · Ctrl+Shift+F11: leave sofa mode"), 14, Colors.Muted);
        DrawSettingsParagraph(40, 176, sofaNotice ?? (libraryWarning is not null
            ? global::AetherBoy.Runtime.Localization.UiText.Get("Some library entries could not be read. Leave sofa mode and check the game library.")
            : global::AetherBoy.Runtime.Localization.UiText.Get("Choose a game, or mark favorites and your own sofa selection.")), width - 80, 16);
    }

    private void DrawSofaGame()
    {
        if (frameTexture != IntPtr.Zero && session?.LatestSnapshot.HasVideoFrame == true)
        {
            var destination = GetGameDestination(frameGeometry);
            SDL.RenderTexture(renderer, frameTexture, IntPtr.Zero, in destination);
            if (options.VideoFilter == LinuxVideoFilter.LcdGrid) DrawLcdGrid(in destination, frameGeometry);
        }
        if (showQuickDeck) DrawSofaQuickMenu();
        else ActionButton(LogicalWidth - 200, 16, 184, 42, global::AetherBoy.Runtime.Localization.UiText.Get("Game menu"), ToggleQuickDeck);
        DrawLoadingOverlay();
    }

    private void DrawSofaQuickMenu()
    {
        shellCommands.Clear(); focusTargets.Clear(); focusIdentities.Clear();
        float x = LogicalWidth / 2f - 300, y = LogicalHeight / 2f - 232;
        Panel(x, y, 600, 464, global::AetherBoy.Runtime.Localization.UiText.Get("Game menu"));
        Ink(x + 24, y + 54, global::AetherBoy.Runtime.Localization.UiText.Get("Your game is paused while the menu is open."), 18, Colors.Muted);
        bool ready = stateOperation is null;
        ActionButton(x + 24, y + 96, 264, 50, global::AetherBoy.Runtime.Localization.UiText.Get("Resume game"), () => { quickDeckResumeOnClose = true; CloseQuickDeck(); }, true, ready);
        ActionButton(x + 312, y + 96, 264, 50, global::AetherBoy.Runtime.Localization.UiText.Get("Game library"), OpenSofaLibrary, enabled: ready);
        ActionButton(x + 24, y + 158, 264, 50, global::AetherBoy.Runtime.Localization.UiText.Get("Save slot: ") + options.SaveSlot,
            () => SelectSaveSlot(options.SaveSlot % 5 + 1), enabled: ready);
        ActionButton(x + 312, y + 158, 264, 50, global::AetherBoy.Runtime.Localization.UiText.Get("Leave sofa mode"), ExitSofaMode, enabled: ready);
        ActionButton(x + 24, y + 220, 264, 50, global::AetherBoy.Runtime.Localization.UiText.Get("Quick save"), QuickSave, enabled: ready);
        ActionButton(x + 312, y + 220, 264, 50, global::AetherBoy.Runtime.Localization.UiText.Get("Quick load"), QuickLoad, enabled: ready && HasSelectedState);
        ActionButton(x + 24, y + 282, 264, 50, global::AetherBoy.Runtime.Localization.UiText.Get("Save screenshot"), CaptureScreenshot, enabled: ready);
        DrawSettingsParagraph(x + 24, y + 356, statusMessage, 552, 16);
    }

    private bool HandleSofaKeyboard(SDL.KeyboardEvent key, bool down)
    {
        if (!sofaMode) return false;
        if (key.Scancode == SDL.Scancode.F11 && (key.Mod & SDL.Keymod.Ctrl) != 0 && (key.Mod & SDL.Keymod.Shift) != 0)
        { if (down && !key.Repeat) ExitSofaMode(); return true; }
        if (IsLoading) return false;
        if (!showSofaLibrary && !showQuickDeck)
        {
            if (key.Scancode is SDL.Scancode.Escape or SDL.Scancode.F10)
            { if (down && !key.Repeat) ToggleQuickDeck(); return true; }
            if (key.Scancode == SDL.Scancode.F11) { if (down && !key.Repeat) ExitSofaMode(); return true; }
            return false;
        }
        if (!down) return true;
        if (key.Scancode is SDL.Scancode.Escape or SDL.Scancode.F10)
        {
            if (key.Repeat) return true;
            if (showQuickDeck) CloseQuickDeck(); else if (session is not null) CloseSofaLibrary(); else ExitSofaMode();
        }
        else if (key.Scancode is SDL.Scancode.Up or SDL.Scancode.Down or SDL.Scancode.Left or SDL.Scancode.Right or SDL.Scancode.Tab)
            MoveSofaFocus(key.Scancode);
        else if (key.Scancode == SDL.Scancode.Return && !key.Repeat)
        {
            DrawShell();
            if (focusedControl >= 0 && focusedControl < focusTargets.Count)
            { var target = focusTargets[focusedControl]; HandleMouseClick(target.X + target.W / 2, target.Y + target.H / 2); }
            else if (focusTargets.Count > 0) focusedControl = 0;
        }
        return true;
    }

    private void MoveSofaFocus(SDL.Scancode key)
    {
        DrawShell();
        if (focusTargets.Count == 0) return;
        if (focusedControl < 0) { focusedControl = 0; return; }
        if (key == SDL.Scancode.Tab) { focusedControl = (focusedControl + 1) % focusTargets.Count; return; }
        var current = focusTargets[focusedControl];
        bool horizontal = key is SDL.Scancode.Left or SDL.Scancode.Right;
        int direction = key is SDL.Scancode.Left or SDL.Scancode.Up ? -1 : 1;
        var candidates = focusTargets.Select((target, index) =>
        {
            float dx = target.X + target.W / 2 - current.X - current.W / 2;
            float dy = target.Y + target.H / 2 - current.Y - current.H / 2;
            return (index, along: (horizontal ? dx : dy) * direction, score: Math.Abs(horizontal ? dx : dy) + 4 * Math.Abs(horizontal ? dy : dx));
        }).Where(item => item.along > 4).OrderBy(item => item.score).ToArray();
        if (candidates.Length > 0) focusedControl = candidates[0].index;
    }

    private bool HandleSofaGamepad(SDL.GamepadButton button, bool down)
    {
        if (!sofaMode) return false;
        if (sofaAwaitNeutral || !windowFocused) return true;
        if (button is SDL.GamepadButton.LeftStick or SDL.GamepadButton.RightStick)
        {
            if (down && SDL.GetGamepadButton(gamepad, SDL.GamepadButton.LeftStick) && SDL.GetGamepadButton(gamepad, SDL.GamepadButton.RightStick) && !showSofaLibrary)
            { ToggleQuickDeck(); sofaAwaitNeutral = true; }
            return true;
        }
        if (!showSofaLibrary && !showQuickDeck) return false;
        SDL.Scancode key = button switch
        {
            SDL.GamepadButton.South => SDL.Scancode.Return, SDL.GamepadButton.East => SDL.Scancode.Escape,
            SDL.GamepadButton.DPadUp => SDL.Scancode.Up, SDL.GamepadButton.DPadDown => SDL.Scancode.Down,
            SDL.GamepadButton.DPadLeft => SDL.Scancode.Left, SDL.GamepadButton.DPadRight => SDL.Scancode.Right,
            SDL.GamepadButton.LeftShoulder => SDL.Scancode.Left, SDL.GamepadButton.RightShoulder => SDL.Scancode.Right,
            _ => SDL.Scancode.Unknown
        };
        HandleSofaKeyboard(new SDL.KeyboardEvent { Scancode = key }, down); return true;
    }

    private void PollSofaController()
    {
        if (!sofaMode && !sofaAwaitNeutral) return;
        if (gamepad == IntPtr.Zero) { sofaAwaitNeutral = false; return; }
        if (!windowFocused) { sofaAwaitNeutral = true; return; }
        int x = SDL.GetGamepadAxis(gamepad, SDL.GamepadAxis.LeftX), y = SDL.GetGamepadAxis(gamepad, SDL.GamepadAxis.LeftY);
        bool neutral = Math.Abs(x) < 10000 && Math.Abs(y) < 10000 &&
            !Enumerable.Range(0, (int)SDL.GamepadButton.Count).Any(index => SDL.GetGamepadButton(gamepad, (SDL.GamepadButton)index));
        if (sofaAwaitNeutral) { if (neutral) sofaAwaitNeutral = false; return; }
        if (!sofaMode) return;
        if (!showSofaLibrary && !showQuickDeck) return;
        int direction = Math.Abs(x) > Math.Abs(y) ? x > 19000 ? 4 : x < -19000 ? 3 : 0 : y > 19000 ? 2 : y < -19000 ? 1 : 0;
        long now = Environment.TickCount64;
        if (direction == 0) { sofaStickDirection = 0; return; }
        if (direction == sofaStickDirection && now < sofaStickRepeatAt) return;
        MoveSofaFocus(direction switch { 1 => SDL.Scancode.Up, 2 => SDL.Scancode.Down, 3 => SDL.Scancode.Left, _ => SDL.Scancode.Right });
        sofaStickRepeatAt = now + (direction == sofaStickDirection ? 120 : 420); sofaStickDirection = direction;
    }
}
