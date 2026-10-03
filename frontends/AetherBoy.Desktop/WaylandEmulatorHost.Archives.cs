using AetherBoy.Runtime.Cartridges;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private sealed record ArchiveSelection(string Path, string? ExpectedIdentity, IReadOnlyList<RomArchiveChoice> Choices);
    private ArchiveSelection? archiveSelection;
    private int archiveSelectedIndex;
    private const int ArchivePageSize = 5;

    private void OpenSelectedArchiveGame()
    {
        if (archiveSelection is not { } selection) return;
        var choice = selection.Choices[archiveSelectedIndex];
        archiveSelection = null; focusedControl = -1;
        BeginRomLoad(selection.Path, selection.ExpectedIdentity, choice);
    }

    private void MoveArchiveSelection(int delta)
    {
        if (archiveSelection is null) return;
        archiveSelectedIndex = Math.Clamp(archiveSelectedIndex + delta, 0, archiveSelection.Choices.Count - 1);
        focusedControl = -1;
    }

    private void DrawArchiveSelection()
    {
        var selection = archiveSelection!;
        float x = (LogicalWidth - 940) / 2f, y = (LogicalHeight - 650) / 2f;
        Panel(x, y, 940, 650, global::AetherBoy.Runtime.Localization.UiText.Get("Choose a game from the archive"));
        Ink(x + 24, y + 57, global::AetherBoy.Runtime.Localization.UiText.Get("Only the selected ROM is imported. The archive stays unchanged."), 16, Colors.Text);
        Ink(x + 24, y + 83, global::AetherBoy.Runtime.Localization.UiText.Get("Saved games inside the archive are not imported."), 14, Colors.Muted);
        int first = archiveSelectedIndex / ArchivePageSize * ArchivePageSize;
        for (int row = 0; row < ArchivePageSize && first + row < selection.Choices.Count; row++)
        {
            int index = first + row; var choice = selection.Choices[index];
            ActionButton(x + 24, y + 125 + row * 55, 890, 46,
                $"{index + 1}. {choice.DisplayName} ({choice.System}, {choice.Size / 1024d:0.#} KiB)",
                () => { archiveSelectedIndex = index; }, index == archiveSelectedIndex, focusId: "archive:item:" + index);
        }
        var selected = selection.Choices[archiveSelectedIndex];
        // Bound each measurement; do not repeatedly fit entire long archive paths.
        string remaining = selected.DisplayName;
        for (int row = 0; row < 5 && remaining.Length > 0; row++)
        {
            int count = Math.Min(remaining.Length, 180);
            while (count > 1 && textRenderer.Measure(remaining[..count], 14) > 880) count--;
            if (count < remaining.Length && char.IsHighSurrogate(remaining[count - 1])) count--;
            string line = remaining[..count];
            if (row == 4 && count < remaining.Length) line = textRenderer.Fit(line + "...", 880, 14);
            Ink(x + 24, y + 415 + row * 20, line, 14, Colors.Muted);
            remaining = remaining[count..];
        }
        ActionButton(x + 24, y + 532, 210, 42, global::AetherBoy.Runtime.Localization.UiText.Get("Previous page"), () => MoveArchiveSelection(-ArchivePageSize),
            enabled: first > 0, focusId: "archive:previous");
        ActionButton(x + 246, y + 532, 210, 42, global::AetherBoy.Runtime.Localization.UiText.Get("Next page"), () => MoveArchiveSelection(ArchivePageSize),
            enabled: first + ArchivePageSize < selection.Choices.Count, focusId: "archive:next");
        Ink(x + 482, y + 542, global::AetherBoy.Runtime.Localization.UiText.Format("Game {0} of {1}", archiveSelectedIndex + 1, selection.Choices.Count), 14, Colors.Muted);
        ActionButton(x + 24, y + 590, 432, 42, global::AetherBoy.Runtime.Localization.UiText.Get("Open selected game"), OpenSelectedArchiveGame, true, focusId: "archive:open");
        ActionButton(x + 482, y + 590, 432, 42, global::AetherBoy.Runtime.Localization.UiText.Get("Cancel"), CancelRomLoad, focusId: "archive:cancel");
    }

    private void HandleArchiveKeyboard(SDL.KeyboardEvent key, bool down)
    {
        if (!down || key.Repeat) return;
        if (key.Scancode == SDL.Scancode.F7 && (key.Mod & SDL.Keymod.Ctrl) != 0)
        { OpenAccessibleControls(); return; }
        switch (key.Scancode)
        {
            case SDL.Scancode.Escape: CancelRomLoad(); break;
            case SDL.Scancode.Up: MoveArchiveSelection(-1); break;
            case SDL.Scancode.Down: MoveArchiveSelection(1); break;
            case SDL.Scancode.Left: MoveArchiveSelection(-ArchivePageSize); break;
            case SDL.Scancode.Right: MoveArchiveSelection(ArchivePageSize); break;
            case SDL.Scancode.Tab:
                DrawShell();
                int direction = (key.Mod & SDL.Keymod.Shift) != 0 ? -1 : 1;
                if (focusTargets.Count > 0) focusedControl = (focusedControl + direction + focusTargets.Count) % focusTargets.Count;
                break;
            case SDL.Scancode.Return:
            case SDL.Scancode.Space:
                DrawShell();
                if (focusedControl >= 0 && focusedControl < focusTargets.Count)
                { var target = focusTargets[focusedControl]; HandleMouseClick(target.X + target.W / 2, target.Y + target.H / 2); }
                else OpenSelectedArchiveGame();
                break;
        }
    }
}
