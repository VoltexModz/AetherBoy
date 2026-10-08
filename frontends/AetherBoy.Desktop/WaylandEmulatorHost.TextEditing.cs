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
    private enum TextField { None, Title, Tags, Search, SettingsSearch, Cheat, RoomCode, RoomServer, RoomAccessKey, ThemePrimary, ThemeSecondary, ThemeBackground, DiscordApplicationId, Barcode, EReaderTitle }

    private TextField onlineEditingField;
    private TextField appearanceEditingField;
    private readonly LinuxTextEditor textEditor = new(80);
    private ILinuxTextClipboard textClipboard = new LinuxSdlTextClipboard();
    private TextField editorField;
    private TextField libraryEditingField = TextField.Title;
    private bool editorHasFocus = true;
    private bool draggingTextSelection;
    private readonly Dictionary<TextField, SDL.FRect> textEntryBounds = new();
    private TextField ActiveTextField => editingTitleIdentity is not null ? libraryEditingField
        : editingSettingsSearch ? TextField.SettingsSearch : editingSearch ? TextField.Search : editingCheat ? TextField.Cheat
        : controlCenterVisible && controlCenterPage == ControlCenterPage.System && showAppearance ? appearanceEditingField
        : controlCenterVisible && controlCenterPage == ControlCenterPage.System && systemSection == SystemSection.Discord && editingDiscordId ? TextField.DiscordApplicationId
        : controlCenterVisible && controlCenterPage == ControlCenterPage.Tools && showBarcodeBoy && editingBarcode ? TextField.Barcode
        : controlCenterVisible && showEReader && showEReaderLibrary && editingEReaderTitle ? TextField.EReaderTitle
        : controlCenterVisible && showOnlineLinkPage && !showLegacyOnlineLink ? onlineEditingField : TextField.None;
    private string? ActiveTextEntryName => ActiveTextField == TextField.EReaderTitle ? global::AetherBoy.Runtime.Localization.UiText.Get("Titel des Kartensatzes") : ActiveTextField switch
    { TextField.Title => global::AetherBoy.Runtime.Localization.UiText.Get("Cartridge title"), TextField.Tags => global::AetherBoy.Runtime.Localization.UiText.Get("Cartridge tags"), TextField.Search => global::AetherBoy.Runtime.Localization.UiText.Get("Search cartridges"), TextField.SettingsSearch => global::AetherBoy.Runtime.Localization.UiText.Get("Search settings"), TextField.Cheat => global::AetherBoy.Runtime.Localization.UiText.Get("Cheat code"), TextField.RoomCode => global::AetherBoy.Runtime.Localization.UiText.Get("Room code"), TextField.RoomServer => global::AetherBoy.Runtime.Localization.UiText.Get("Room server address"), TextField.RoomAccessKey => global::AetherBoy.Runtime.Localization.UiText.Get("Server access key"),
      TextField.ThemePrimary => global::AetherBoy.Runtime.Localization.UiText.Get("Primary UI color"), TextField.ThemeSecondary => global::AetherBoy.Runtime.Localization.UiText.Get("Secondary UI color"), TextField.ThemeBackground => global::AetherBoy.Runtime.Localization.UiText.Get("UI background color"), TextField.DiscordApplicationId => global::AetherBoy.Runtime.Localization.UiText.Get("Discord application ID"), TextField.Barcode => global::AetherBoy.Runtime.Localization.UiText.Get("Barcode Boy card code"), _ => null };
    private string? ActiveTextName => ActiveTextEntryName;
    private bool ActiveTextReadOnly => ActiveTextField is TextField.Title or TextField.Tags && libraryMutation is not null;
    private string ActiveTextValue
    {
        get { EnsureTextEditor(); return ActiveTextField == TextField.None ? "" : ActiveTextField == TextField.RoomAccessKey ? new string('*', textEditor.Text.Length) : textEditor.Text; }
        set
        {
            if (ActiveTextField == TextField.None || ActiveTextReadOnly) return;
            EnsureTextEditor(); textEditor.SetText(value); SyncTextEditor();
        }
    }

    private string TextFieldValue(TextField field) => field == TextField.EReaderTitle ? eReaderTitleInput : field switch
    { TextField.Title => titleInput, TextField.Tags => tagsInput, TextField.Search => librarySearch, TextField.SettingsSearch => settingsSearch, TextField.Cheat => cheatCode,
      TextField.RoomCode => roomCodeInput, TextField.RoomServer => roomServerInput, TextField.RoomAccessKey => roomAccessKeyInput,
      TextField.ThemePrimary => primaryColorInput, TextField.ThemeSecondary => secondaryColorInput, TextField.ThemeBackground => backgroundColorInput, TextField.DiscordApplicationId => discordApplicationIdInput, TextField.Barcode => barcodeInput, _ => "" };

    private void EnsureTextEditor()
    {
        var field = ActiveTextField;
        textEditor.LineBreakReplacement = field == TextField.Cheat ? " + " : "";
        textEditor.MaximumLength = field is TextField.RoomServer or TextField.RoomAccessKey ? 256
            : field == TextField.Tags ? 240
            : field == TextField.EReaderTitle ? 120
            : field == TextField.Cheat ? 32768
            : field is TextField.ThemePrimary or TextField.ThemeSecondary or TextField.ThemeBackground ? 7
            : field == TextField.DiscordApplicationId ? 20 : field == TextField.RoomCode ? 14 : 80;
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
            case TextField.Tags: tagsInput = textEditor.Text; break;
            case TextField.SettingsSearch: settingsSearch = textEditor.Text; settingsSearchPage = 0; focusedControl = -1; break;
            case TextField.Search: librarySearch = textEditor.Text; libraryPage = 0; break;
            case TextField.Cheat: cheatCode = textEditor.Text; break;
            case TextField.RoomCode: roomCodeInput = textEditor.Text; break;
            case TextField.RoomServer: roomServerInput = textEditor.Text; break;
            case TextField.RoomAccessKey: roomAccessKeyInput = textEditor.Text; break;
            case TextField.ThemePrimary: primaryColorInput = textEditor.Text; break;
            case TextField.ThemeSecondary: secondaryColorInput = textEditor.Text; break;
            case TextField.ThemeBackground: backgroundColorInput = textEditor.Text; break;
            case TextField.DiscordApplicationId: discordApplicationIdInput = textEditor.Text; break;
            case TextField.Barcode: barcodeInput = textEditor.Text; break;
            case TextField.EReaderTitle: eReaderTitleInput = textEditor.Text; break;
        }
    }

    private void BeginTextEditing(TextField field)
    {
        if (ActiveTextReadOnly) return;
        if (textEditor.IsComposing) SDL.ClearComposition(window);
        onlineEditingField = field is TextField.RoomCode or TextField.RoomServer or TextField.RoomAccessKey ? field : TextField.None;
        appearanceEditingField = field is TextField.ThemePrimary or TextField.ThemeSecondary or TextField.ThemeBackground ? field : TextField.None;
        if (field is TextField.Title or TextField.Tags) libraryEditingField = field;
        editingSettingsSearch = field == TextField.SettingsSearch;
        editingSearch = field == TextField.Search;
        editingCheat = field == TextField.Cheat;
        editingDiscordId = field == TextField.DiscordApplicationId;
        editingBarcode = field == TextField.Barcode;
        editingEReaderTitle = field == TextField.EReaderTitle;
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
        showOnScreenKeyboard = false;
        if (ActiveTextField is TextField.Title or TextField.Tags) { EndTitleEdit(true); return; }
        editingSettingsSearch = false;
        editingSearch = editingCheat = false;
        editingDiscordId = false;
        editingBarcode = false;
        editingEReaderTitle = false;
        onlineEditingField = TextField.None;
        appearanceEditingField = TextField.None;
        editorField = TextField.None; SDL.StopTextInput(window);
    }

    private void CancelActiveText()
    {
        if (ActiveTextReadOnly) return;
        EnsureTextEditor();
        if (textEditor.IsComposing) { CancelTextComposition(); return; }
        showOnScreenKeyboard = false;
        if (ActiveTextField == TextField.SettingsSearch) { ClearSettingsSearch(); return; }
        if (ActiveTextField is TextField.Title or TextField.Tags) { EndTitleEdit(false); return; }
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
            if (ActiveTextField is not (TextField.Title or TextField.Tags)) CommitActiveText();
            else SDL.StopTextInput(window);
            editorHasFocus = false;
            return false;
        }
        if (!editorHasFocus) return false;
        if (key.Scancode == SDL.Scancode.Return)
        {
            if (ActiveTextField == TextField.SettingsSearch && !textEditor.IsComposing)
            {
                var first = LinuxSettingsCatalog.Search(settingsSearch).Skip(settingsSearchPage * SettingsResultsPerPage).FirstOrDefault();
                if (first is not null) OpenSettingsDestination(first.Destination);
                else CommitActiveText();
            }
            else CommitActiveText();
            return true;
        }
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
            catch (Exception ex) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Clipboard action failed: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message); }
        }
        return true;
    }

    private void DrawTextEntry(TextField field, float x, float y, float width, float height, string placeholder, bool enabled = true)
    {
        if (field == TextField.SettingsSearch)
        {
            // Search is available while a page is open, but never steals an unfinished title edit.
            enabled = enabled && editingTitleIdentity is null;
        }
        if (enabled && !IsLoading) textEntryBounds[field] = new SDL.FRect { X = x, Y = y, W = width, H = height };
        bool active = ActiveTextField == field;
        string value = TextFieldValue(field);
        if (field == TextField.RoomAccessKey) value = new string('*', value.Length);
        ActionButton(x, y, width, height, "", () => { if (field == TextField.SettingsSearch) FocusSettingsSearch(); else BeginTextEditing(field); }, false, enabled, focusId: "text:" + field);
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
        TextField field = TextField.None;
        if (controlCenterVisible)
            foreach (var candidate in textEntryBounds)
                if (Hit(x, y, candidate.Value.X, candidate.Value.Y, candidate.Value.W, candidate.Value.H))
                    field = candidate.Key;
        if (field == TextField.SettingsSearch && editingTitleIdentity is not null) return true;
        if (field == TextField.None || !textEntryBounds.TryGetValue(field, out var bounds) || !Hit(x, y, bounds.X, bounds.Y, bounds.W, bounds.H))
        {
            if (ActiveTextField != TextField.None && !textEditor.IsComposing)
            { editorHasFocus = false; SDL.StopTextInput(window); }
            return false;
        }
        if (ActiveTextReadOnly) return true;
        if (ActiveTextField != field)
        { if (field == TextField.SettingsSearch) FocusSettingsSearch(); else BeginTextEditing(field); }
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
