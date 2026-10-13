using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private sealed record AetherChoice(string Value, string Label);
    private sealed class AetherSelection(string id, string title, IReadOnlyList<AetherChoice> choices,
        string selected, Action<string> apply, FocusIdentity? owner)
    {
        public readonly string Id = id, Title = title, Selected = selected;
        public readonly IReadOnlyList<AetherChoice> Choices = choices;
        public readonly Action<string> Apply = apply;
        public readonly FocusIdentity? Owner = owner;
        public int Cursor = Math.Max(0, choices.ToList().FindIndex(choice => choice.Value == selected));
    }
    private AetherSelection? aetherSelection;
    private SDL.FRect aetherSelectionBounds;

    private void AetherSelectionButton(float x, float y, float width, float height, string label,
        string id, IReadOnlyList<AetherChoice> choices, string selected, Action<string> apply, bool enabled = true,
        string? displayLabel = null)
    {
        ActionButton(x, y, width, height, label, () =>
        {
            if (textEditor.IsComposing) return;
            if (editingTitleIdentity is not null)
            {
                EnsureTextEditor(); SyncTextEditor(); editorHasFocus = false;
                draggingTextSelection = false; showOnScreenKeyboard = false; SDL.StopTextInput(window);
            }
            else CommitActiveText();
            var owner = new FocusIdentity(FocusContext, "select:" + id, x, y, width, height);
            aetherSelection = new(id, label, choices, selected, apply, owner);
            focusedControl = -1;
            DrawShell();
        }, enabled: enabled, focusId: "select:" + id, displayLabel: displayLabel, labelRightInset: 24);
        // Keep the selection affordance independent of bundled font glyph coverage.
        SDL.Color color = enabled ? Colors.Text : Colors.Muted;
        SDL.SetRenderDrawColor(renderer, color.R, color.G, color.B, 255);
        float centerX = x + width - 19, centerY = y + height / 2 - 2;
        for (int stroke = 0; stroke < 2; stroke++)
        {
            SDL.RenderLine(renderer, centerX - 5, centerY + stroke, centerX, centerY + 5 + stroke);
            SDL.RenderLine(renderer, centerX, centerY + 5 + stroke, centerX + 5, centerY + stroke);
        }
    }

    private void CloseAetherSelection(string? value = null)
    {
        if (aetherSelection is not { } popup) return;
        aetherSelection = null;
        if (value is not null) popup.Apply(value);
        DrawShell();
        focusedControl = popup.Owner is { } owner ? focusIdentities.FindIndex(identity => identity == owner) : -1;
    }

    private void DrawAetherSelectionPopup()
    {
        if (aetherSelection is not { } popup) return;
        // Only the popup participates in activation/focus/AT-SPI while it is open.
        shellCommands.Clear(); focusTargets.Clear(); focusIdentities.Clear();
        accessibleCommands.Clear(); accessibleDescriptions.Clear(); textEntryBounds.Clear();
        focusedControl = -1;
        float width = Math.Min(560, LogicalWidth - 64), rowHeight = Math.Max(44, textRenderer.MinimumSize + 26);
        float height = 100 + popup.Choices.Count * (rowHeight + 6);
        float x = (LogicalWidth - width) / 2, y = (LogicalHeight - height) / 2;
        aetherSelectionBounds = new() { X = x, Y = y, W = width, H = height };
        Panel(x, y, width, height);
        Ink(x + 20, y + 18, textRenderer.Fit(popup.Title, width - 90, 18, true), 18, Colors.Text, true);
        ActionButton(x + width - 62, y + 12, 42, 36, "×", () => CloseAetherSelection(), focusId: "select:close");
        for (int i = 0; i < popup.Choices.Count; i++)
        {
            AetherChoice choice = popup.Choices[i];
            ActionButton(x + 20, y + 66 + i * (rowHeight + 6), width - 40, rowHeight, choice.Label,
                () => CloseAetherSelection(choice.Value), choice.Value == popup.Selected, focusId: "option:select:" + popup.Id + ":" + choice.Value);
        }
        focusedControl = Math.Clamp(popup.Cursor + 1, 0, focusTargets.Count - 1);
        var focus = focusTargets[focusedControl];
        SDL.SetRenderDrawColor(renderer, Colors.Cyan.R, Colors.Cyan.G, Colors.Cyan.B, 255);
        focus.X += 4; focus.Y += 4; focus.W -= 8; focus.H -= 8;
        SDL.RenderRect(renderer, in focus);
    }

    private bool HandleAetherSelectionKey(SDL.KeyboardEvent key, bool down)
    {
        if (aetherSelection is not { } popup) return false;
        if (!down) return true;
        if (!key.Repeat && key.Scancode == SDL.Scancode.F7 && (key.Mod & SDL.Keymod.Ctrl) != 0)
        { OpenAccessibleControls(); return true; }
        switch (key.Scancode)
        {
            case SDL.Scancode.Escape: CloseAetherSelection(); break;
            case SDL.Scancode.Return or SDL.Scancode.Space when !key.Repeat:
                CloseAetherSelection(popup.Choices[popup.Cursor].Value); break;
            case SDL.Scancode.Home: popup.Cursor = 0; break;
            case SDL.Scancode.End: popup.Cursor = popup.Choices.Count - 1; break;
            case SDL.Scancode.Up or SDL.Scancode.Left: popup.Cursor = (popup.Cursor + popup.Choices.Count - 1) % popup.Choices.Count; break;
            case SDL.Scancode.Down or SDL.Scancode.Right: popup.Cursor = (popup.Cursor + 1) % popup.Choices.Count; break;
            case SDL.Scancode.Tab:
                popup.Cursor = (popup.Cursor + ((key.Mod & SDL.Keymod.Shift) != 0 ? popup.Choices.Count - 1 : 1)) % popup.Choices.Count; break;
            case SDL.Scancode.Pageup: popup.Cursor = Math.Max(0, popup.Cursor - 5); break;
            case SDL.Scancode.Pagedown: popup.Cursor = Math.Min(popup.Choices.Count - 1, popup.Cursor + 5); break;
        }
        return true;
    }

    private bool HandleAetherSelectionPointer(float x, float y)
    {
        if (aetherSelection is null) return false;
        DrawShell();
        if (!Hit(x, y, aetherSelectionBounds.X, aetherSelectionBounds.Y, aetherSelectionBounds.W, aetherSelectionBounds.H))
        { CloseAetherSelection(); return true; }
        foreach (var command in shellCommands)
            if (Hit(x, y, command.Bounds.X, command.Bounds.Y, command.Bounds.W, command.Bounds.H))
            { command.Action(); break; }
        return true;
    }

    private bool HandleAetherSelectionGamepad(SDL.GamepadButton button, bool down)
    {
        if (aetherSelection is null) return false;
        var key = button switch
        {
            SDL.GamepadButton.East => SDL.Scancode.Escape,
            SDL.GamepadButton.South => SDL.Scancode.Return,
            SDL.GamepadButton.DPadUp => SDL.Scancode.Up,
            SDL.GamepadButton.DPadDown => SDL.Scancode.Down,
            SDL.GamepadButton.DPadLeft => SDL.Scancode.Left,
            SDL.GamepadButton.DPadRight => SDL.Scancode.Right,
            _ => SDL.Scancode.Unknown
        };
        HandleAetherSelectionKey(new() { Scancode = key }, down);
        return true;
    }
}
