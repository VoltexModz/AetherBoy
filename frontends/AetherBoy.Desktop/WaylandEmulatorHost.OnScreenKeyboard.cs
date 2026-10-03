using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private bool showOnScreenKeyboard;
    private bool onScreenLowercase, onScreenSymbols;

    private void OpenOnScreenKeyboard()
    {
        if (ActiveTextField == TextField.None || ActiveTextReadOnly || textEditor.IsComposing) return;
        EnsureTextEditor();
        showOnScreenKeyboard = true;
        onScreenLowercase = false; onScreenSymbols = false;
        focusedControl = -1;
    }

    private void InsertOnScreenKey(string value)
    {
        if (ActiveTextField == TextField.None || textEditor.IsComposing) return;
        EnsureTextEditor(); textEditor.Insert(value); SyncTextEditor();
    }

    private void RemoveOnScreenCharacter()
    {
        if (ActiveTextField == TextField.None || textEditor.IsComposing) return;
        EnsureTextEditor(); textEditor.Key(LinuxTextKey.Backspace, false, textClipboard); SyncTextEditor();
    }

    private void DrawOnScreenKeyboard()
    {
        if (!showOnScreenKeyboard || ActiveTextField == TextField.None) { showOnScreenKeyboard = false; return; }
        shellCommands.Clear(); focusTargets.Clear(); focusIdentities.Clear();
        accessibleCommands.Clear(); accessibleDescriptions.Clear();
        float x = 283, y = 238;
        Panel(x, y, LogicalWidth - 305, 391, global::AetherBoy.Runtime.Localization.UiText.Get("ON-SCREEN KEYBOARD"));
        Ink(x + 24, y + 44, ActiveTextField == TextField.RoomAccessKey
            ? global::AetherBoy.Runtime.Localization.UiText.Get("The access key stays masked. It is not added to suggestions or reports.")
            : global::AetherBoy.Runtime.Localization.UiText.Get("Choose characters with the controller. The physical keyboard still works."), 13, Colors.Muted);
        string[] rows = onScreenSymbols
            ? ["1234567890", "!@#$%^&*()", "[]{};:'\",.", "<>?/\\|_+-="]
            : ["1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM-. "];
        for (int row = 0; row < rows.Length; row++)
        {
            string keys = rows[row];
            float width = Math.Min(72, (LogicalWidth - 360f) / keys.Length);
            for (int column = 0; column < keys.Length; column++)
            {
                char key = keys[column];
                string label = key == ' ' ? global::AetherBoy.Runtime.Localization.UiText.Get("SPACE") : key.ToString();
                string value = onScreenLowercase ? label.ToLowerInvariant() : label;
                if (key == ' ') value = " ";
                ActionButton(x + 22 + column * (width + 3), y + 77 + row * 49, width, 40, label,
                    () => InsertOnScreenKey(value), focusId: "virtual-key:" + row + ":" + column);
            }
        }
        ActionButton(x + 22, y + 288, 132, 42, onScreenLowercase ? global::AetherBoy.Runtime.Localization.UiText.Get("USE CAPITALS") : global::AetherBoy.Runtime.Localization.UiText.Get("USE LOWERCASE"),
            () => onScreenLowercase = !onScreenLowercase);
        ActionButton(x + 166, y + 288, 128, 42, onScreenSymbols ? global::AetherBoy.Runtime.Localization.UiText.Get("USE LETTERS") : global::AetherBoy.Runtime.Localization.UiText.Get("USE SYMBOLS"),
            () => onScreenSymbols = !onScreenSymbols);
        ActionButton(x + 306, y + 288, 128, 42, global::AetherBoy.Runtime.Localization.UiText.Get("BACKSPACE"), RemoveOnScreenCharacter);
        ActionButton(x + 446, y + 288, 128, 42, global::AetherBoy.Runtime.Localization.UiText.Get("DONE"), CommitActiveText);
        ActionButton(x + 586, y + 288, 128, 42, global::AetherBoy.Runtime.Localization.UiText.Get("CANCEL"), CancelActiveText);
    }
}
