using AetherBoy.Runtime;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private bool showQuickDeck;
    private bool quickDeckResumeOnClose;

    private void ToggleQuickDeck()
    {
        if (showQuickDeck) { CloseQuickDeck(); return; }
        if (session is not { } active || IsOnlineLink || IsLoading || controlCenterVisible || showLocalLinkPage || showSofaLibrary || stateOperation is not null) return;
        quickDeckResumeOnClose = !active.LatestSnapshot.IsPaused;
        if (quickDeckResumeOnClose) active.SetPausedAsync(true).GetAwaiter().GetResult();
        audioOutput?.Clear();
        showQuickDeck = true;
        if (sofaMode) { active.SetTurboAsync(false).GetAwaiter().GetResult(); mouseTurbo = false; pressedKeys.Clear(); sofaAwaitNeutral = true; }
        focusedControl = -1;
    }

    private void CloseQuickDeck()
    {
        if (sofaMode && stateOperation is not null) return;
        showQuickDeck = false;
        if (sofaMode) { pressedKeys.Clear(); sofaAwaitNeutral = true; }
        if (quickDeckResumeOnClose && session is { } active && active.State == SessionState.Paused)
            active.SetPausedAsync(false).GetAwaiter().GetResult();
        quickDeckResumeOnClose = false;
        focusedControl = -1;
    }

    private void KeepGamePaused()
    {
        quickDeckResumeOnClose = false;
        CloseQuickDeck();
        statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Game paused. Open the quick menu to resume.");
    }

    private void DrawQuickDeck()
    {
        // Modal commands replace the hidden game's hit targets and focus targets.
        shellCommands.Clear(); focusTargets.Clear(); focusIdentities.Clear();
        float x = LogicalWidth / 2f - 272, y = LogicalHeight / 2f - 202;
        Panel(x, y, 544, 404, global::AetherBoy.Runtime.Localization.UiText.Get("QUICK MENU"));
        Ink(x + 26, y + 58, global::AetherBoy.Runtime.Localization.UiText.Get("Your game is paused while this menu is open."), 14, Colors.Muted);
        ActionButton(x + 26, y + 99, 230, 44, global::AetherBoy.Runtime.Localization.UiText.Get("RESUME GAME"), CloseQuickDeck);
        ActionButton(x + 286, y + 99, 230, 44, global::AetherBoy.Runtime.Localization.UiText.Get("KEEP PAUSED"), KeepGamePaused);
        Ink(x + 26, y + 169, global::AetherBoy.Runtime.Localization.UiText.Get("Save-state slot"), 14, Colors.Text, true);
        for (int i = 0; i < 5; i++)
        {
            int slot = i + 1;
            ActionButton(x + 26 + i * 98, y + 194, 84, 40, slot.ToString(), () => SelectSaveSlot(slot), options.SaveSlot == slot);
        }
        ActionButton(x + 26, y + 263, 230, 42, global::AetherBoy.Runtime.Localization.UiText.Get("CHANGE VIDEO FILTER"), () =>
            SetVideoFilter((LinuxVideoFilter)(((int)options.VideoFilter + 1) % 3)));
        Ink(x + 282, y + 276, global::AetherBoy.Runtime.Localization.UiText.Get("Current: ") + options.VideoFilter, 13, Colors.Muted);
        ActionButton(x + 26, y + 328, 490, 42, global::AetherBoy.Runtime.Localization.UiText.Get("OPEN FULL SETTINGS"), () =>
        { CloseQuickDeck(); OpenControlPage(ControlCenterPage.Overview); });
    }
}
