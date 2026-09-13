using SDL3;

namespace AetherBoy.Desktop;

internal sealed class LinuxSdlTextClipboard : ILinuxTextClipboard
{
    private readonly Func<string?> read;
    private readonly Action clearError;
    private readonly Func<string?> error;
    private readonly Action<string> write;

    public LinuxSdlTextClipboard(Func<string?>? read = null, Action? clearError = null,
        Func<string?>? error = null, Action<string>? write = null)
    {
        this.read = read ?? (() => SDL.GetClipboardText());
        this.clearError = clearError ?? (() => SDL.ClearError());
        this.error = error ?? (() => SDL.GetError());
        this.write = write ?? (value =>
        { if (!SDL.SetClipboardText(value)) throw new InvalidOperationException(SDL.GetError()); });
    }

    public string Read()
    {
        clearError();
        string? text = read();
        if (string.IsNullOrEmpty(text) && error() is { Length: > 0 } message)
            throw new InvalidOperationException(message);
        return text ?? "";
    }
    public void Write(string value) => write(value);
}

internal sealed partial class WaylandEmulatorHost
{
    private enum TextField { None, Title, Search, Cheat, RoomCode, RoomServer, RoomAccessKey }

    private TextField onlineEditingField;
    private readonly LinuxTextEditor textEditor = new(80);
    private ILinuxTextClipboard textClipboard = new LinuxSdlTextClipboard();
    private TextField editorField;
    private bool editorHasFocus = true;
    private bool draggingTextSelection;
    private readonly Dictionary<TextField, SDL.FRect> textEntryBounds = new();
    private TextField ActiveTextField => editingTitleIdentity is not null ? TextField.Title
        : editingSearch ? TextField.Search : editingCheat ? TextField.Cheat : controlCenterVisible && showOnlineLinkPage && !showLegacyOnlineLink ? onlineEditingField : TextField.None;
    private string? ActiveTextEntryName => ActiveTextField switch
    { TextField.Title => "Cartridge title", TextField.Search => "Search cartridges", TextField.Cheat => "Cheat code", TextField.RoomCode => "Room code", TextField.RoomServer => "Room server address", TextField.RoomAccessKey => "Server access key", _ => null };
    private string? ActiveTextName => ActiveTextEntryName;
    private bool ActiveTextReadOnly => ActiveTextField == TextField.Title && libraryMutation is not null;
    private string ActiveTextValue
    {
        get { EnsureTextEditor(); return ActiveTextField == TextField.None ? "" : ActiveTextField == TextField.RoomAccessKey ? new string('*', textEditor.Text.Length) : textEditor.Text; }
        set
        {
            if (ActiveTextField == TextField.None || ActiveTextReadOnly) return;
            EnsureTextEditor(); textEditor.SetText(value); SyncTextEditor();
        }
    }

    private string TextFieldValue(TextField field) => field switch
    { TextField.Title => titleInput, TextField.Search => librarySearch, TextField.Cheat => cheatCode,
      TextField.RoomCode => roomCodeInput, TextField.RoomServer => roomServerInput, TextField.RoomAccessKey => roomAccessKeyInput, _ => "" };

    private void EnsureTextEditor()
    {
        var field = ActiveTextField;
        textEditor.MaximumLength = field is TextField.RoomServer or TextField.RoomAccessKey ? 256 : field == TextField.RoomCode ? 14 : 80;
        string value = TextFieldValue(field);
        if (editorField != field || textEditor.Text != value)
        { textEditor.SetText(value); editorField = field; editorHasFocus = true; }
        if (field == TextField.Title && titleSelectedAll && !textEditor.AllSelected) textEditor.SelectAll();
    }

    private void SyncTextEditor()
    {
        switch (ActiveTextField)
        {
            case TextField.Title: titleInput = textEditor.Text; titleSelectedAll = textEditor.AllSelected; break;
            case TextField.Search: librarySearch = textEditor.Text; libraryPage = 0; break;
            case TextField.Cheat: cheatCode = textEditor.Text; break;
            case TextField.RoomCode: roomCodeInput = textEditor.Text; break;
            case TextField.RoomServer: roomServerInput = textEditor.Text; break;
            case TextField.RoomAccessKey: roomAccessKeyInput = textEditor.Text; break;
        }
    }

    private void BeginTextEditing(TextField field)
    {
        if (ActiveTextReadOnly) return;
        if (textEditor.IsComposing) SDL.ClearComposition(window);
        onlineEditingField = field is TextField.RoomCode or TextField.RoomServer or TextField.RoomAccessKey ? field : TextField.None;
        editingSearch = field == TextField.Search;
        editingCheat = field == TextField.Cheat;
        editorField = TextField.None;
        EnsureTextEditor(); editorHasFocus = true; focusedControl = -1;
        pressedKeys.Clear();
        SDL.StartTextInput(window);
    }

    private void ReceiveTextInput(string text)
    {
        if (ActiveTextField == TextField.None || ActiveTextReadOnly) return;
        EnsureTextEditor();
        if (!editorHasFocus) return;
        textEditor.Insert(text); SyncTextEditor();
    }

    private void ReceiveTextComposition(string text, int start, int length)
    {
        if (ActiveTextField == TextField.None || ActiveTextReadOnly) return;
        EnsureTextEditor();
        if (!editorHasFocus) return;
        textEditor.SetComposition(text, start, length);
    }

    private void CancelTextComposition()
    {
        textEditor.CancelComposition();
        SDL.ClearComposition(window);
        draggingTextSelection = false;
    }

    private void CommitActiveText()
    {
        if (ActiveTextReadOnly) return;
        EnsureTextEditor();
        if (textEditor.IsComposing) return;
        SyncTextEditor();
        if (ActiveTextField == TextField.Title) { EndTitleEdit(true); return; }
        editingSearch = editingCheat = false;
        onlineEditingField = TextField.None;
        editorField = TextField.None; SDL.StopTextInput(window);
    }

    private void CancelActiveText()
    {
        if (ActiveTextReadOnly) return;
        EnsureTextEditor();
        if (textEditor.IsComposing) { CancelTextComposition(); return; }
        if (ActiveTextField == TextField.Title) { EndTitleEdit(false); return; }
        CommitActiveText(); // Search/cheat drafts survive leaving the field.
    }

    private bool HandleTextEditorKey(SDL.KeyboardEvent key, bool pressed)
    {
        if (ActiveTextField == TextField.None) return false;
        if (!pressed || ActiveTextReadOnly) return true;
        EnsureTextEditor();
        if (key.Scancode == SDL.Scancode.Escape) { CancelActiveText(); return true; }
        if (key.Scancode is SDL.Scancode.Tab or SDL.Scancode.F6)
        {
            if (textEditor.IsComposing) return true;
            if (ActiveTextField != TextField.Title) CommitActiveText();
            else SDL.StopTextInput(window);
            editorHasFocus = false;
            return false;
        }
        if (!editorHasFocus) return false;
        if (key.Scancode == SDL.Scancode.Return) { CommitActiveText(); return true; }
        bool ctrl = (key.Mod & SDL.Keymod.Ctrl) != 0 && (key.Mod & SDL.Keymod.Alt) == 0;
        LinuxTextKey? command = ctrl ? key.Scancode switch
        {
            SDL.Scancode.A => LinuxTextKey.SelectAll, SDL.Scancode.C => LinuxTextKey.Copy,
            SDL.Scancode.X => LinuxTextKey.Cut, SDL.Scancode.V => LinuxTextKey.Paste, _ => null
        } : key.Scancode switch
        {
            SDL.Scancode.Left => LinuxTextKey.Left, SDL.Scancode.Right => LinuxTextKey.Right,
            SDL.Scancode.Home => LinuxTextKey.Home, SDL.Scancode.End => LinuxTextKey.End,
            SDL.Scancode.Backspace => LinuxTextKey.Backspace, SDL.Scancode.Delete => LinuxTextKey.Delete, _ => null
        };
        if (command is { } action)
        {
            try { textEditor.Key(action, (key.Mod & SDL.Keymod.Shift) != 0, textClipboard); SyncTextEditor(); }
            catch (Exception ex) { statusMessage = "Clipboard action failed: " + ex.Message; }
        }
        return true;
    }

    private void DrawTextEntry(TextField field, float x, float y, float width, float height, string placeholder, bool enabled = true)
    {
        textEntryBounds[field] = new SDL.FRect { X = x, Y = y, W = width, H = height };
        bool active = ActiveTextField == field;
        string value = TextFieldValue(field);
        if (field == TextField.RoomAccessKey) value = new string('*', value.Length);
        ActionButton(x, y, width, height, "", () => BeginTextEditing(field), false, enabled, focusId: "text:" + field);
        if (!active)
        { Ink(x + 12, y + (height - 22) / 2, textRenderer.Fit(string.IsNullOrEmpty(value) ? placeholder : value, width - 24, 16), 16, string.IsNullOrEmpty(value) ? Colors.Muted : Colors.Text); return; }
        EnsureTextEditor();
        float lineHeight = Math.Max(16, textRenderer.MinimumSize) + 8;
        float contentWidth = width - 24, top = y + (height - lineHeight) / 2;
        float Measure(string text) => textRenderer.Measure(field == TextField.RoomAccessKey ? new string('*', text.Length) : text, 16);
        textEditor.EnsureCaretVisible(contentWidth, Measure);
        float origin = x + 12 - textEditor.ScrollOffset;
        string display = field == TextField.RoomAccessKey ? new string('*', textEditor.DisplayText.Length) : textEditor.DisplayText;
        bool clipped = SDL.RenderClipEnabled(renderer);
        SDL.GetRenderClipRect(renderer, out SDL.Rect previous);
        var clip = new SDL.Rect { X = (int)(x + 10), Y = (int)y, W = (int)(width - 20), H = (int)height };
        SDL.SetRenderClipRect(renderer, in clip);
        try
        {
            int start = textEditor.DisplaySelectionStart, length = textEditor.DisplaySelectionLength;
            if (length > 0)
                Paint(origin + Measure(display[..start]), top, Measure(display.Substring(start, length)), lineHeight, Colors.Border);
            Ink(origin, top, display, 16);
            if (textEditor.IsComposing)
                Paint(origin + Measure(display[..textEditor.SelectionStart]), top + lineHeight - 2, Measure(textEditor.Composition), 2, Colors.Cyan);
            float caret = origin + Measure(display[..textEditor.DisplayCaret]);
            if (editorHasFocus && !ActiveTextReadOnly) Paint(caret, top, 2, lineHeight, Colors.Cyan);
            SDL.RenderCoordinatesToWindow(renderer, x, y, out float windowX, out float windowY);
            SDL.RenderCoordinatesToWindow(renderer, x + width, y + height, out float windowRight, out float windowBottom);
            SDL.RenderCoordinatesToWindow(renderer, caret, y, out float windowCaret, out _);
            var inputArea = new SDL.Rect { X = (int)windowX, Y = (int)windowY, W = Math.Max(1, (int)(windowRight - windowX)), H = Math.Max(1, (int)(windowBottom - windowY)) };
            SDL.SetTextInputArea(window, in inputArea, Math.Clamp((int)(windowCaret - windowX), 0, inputArea.W));
        }
        finally
        {
            if (clipped) SDL.SetRenderClipRect(renderer, in previous);
            else SDL.SetRenderClipRect(renderer, IntPtr.Zero);
        }
    }

    private bool HandleTextPointerDown(float x, float y, bool shift)
    {
        draggingTextSelection = false;
        TextField field = !controlCenterVisible ? TextField.None : controlCenterPage switch
        {
            ControlCenterPage.Library when !showPatchLab => editingTitleIdentity is not null ? TextField.Title : TextField.Search,
            ControlCenterPage.Tools when session is not null => TextField.Cheat,
            _ => TextField.None
        };
        if (controlCenterVisible && showOnlineLinkPage && !showLegacyOnlineLink && onlineLinkTransport is null)
        {
            field = TextField.None;
            foreach (var candidate in showRoomSetup ? new[] { TextField.RoomServer, TextField.RoomAccessKey } : onlineRoomTransport is null ? new[] { TextField.RoomCode } : Array.Empty<TextField>())
                if (textEntryBounds.TryGetValue(candidate, out var rectangle) && Hit(x, y, rectangle.X, rectangle.Y, rectangle.W, rectangle.H)) field = candidate;
        }
        if (field == TextField.None || !textEntryBounds.TryGetValue(field, out var bounds) || !Hit(x, y, bounds.X, bounds.Y, bounds.W, bounds.H))
        {
            if (ActiveTextField != TextField.None && !textEditor.IsComposing)
            { editorHasFocus = false; SDL.StopTextInput(window); }
            return false;
        }
        if (ActiveTextReadOnly) return true;
        if (ActiveTextField != field) BeginTextEditing(field);
        else { EnsureTextEditor(); editorHasFocus = true; focusedControl = -1; SDL.StartTextInput(window); }
        if (textEditor.IsComposing) return true;
        textEditor.PlaceCaret(x - bounds.X - 12 + textEditor.ScrollOffset, value => textRenderer.Measure(ActiveTextField == TextField.RoomAccessKey ? new string('*', value.Length) : value, 16), shift);
        SyncTextEditor(); draggingTextSelection = true;
        return true;
    }

    private void HandleTextPointerMotion(float x)
    {
        if (!draggingTextSelection || ActiveTextReadOnly || !editorHasFocus ||
            !textEntryBounds.TryGetValue(ActiveTextField, out var bounds)) return;
        textEditor.PlaceCaret(x - bounds.X - 12 + textEditor.ScrollOffset, value => textRenderer.Measure(ActiveTextField == TextField.RoomAccessKey ? new string('*', value.Length) : value, 16), extend: true);
        textEditor.EnsureCaretVisible(bounds.W - 24, value => textRenderer.Measure(ActiveTextField == TextField.RoomAccessKey ? new string('*', value.Length) : value, 16));
        SyncTextEditor();
    }
}
