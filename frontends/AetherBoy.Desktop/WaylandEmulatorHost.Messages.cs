using System.Globalization;
using AetherBoy.Runtime.Localization;
using SDL3;

namespace AetherBoy.Desktop;

internal readonly record struct LinuxWrappedLine(int Start, int Length)
{
    public string Read(string text) => text.Substring(Start, Length);
}

/// <summary>Lossless ranges into the original message; soft wraps never alter copied paths or diagnostics.</summary>
internal static class LinuxMessageLayout
{
    public static LinuxWrappedLine[] Wrap(string text, float width, Func<string, float> measure)
    {
        if (text.Length == 0) return [new(0, 0)];
        var lines = new List<LinuxWrappedLine>();
        int start = 0, end = 0, lastBreak = -1;
        foreach (int next in StringInfo.ParseCombiningCharacters(text).Skip(1).Append(text.Length))
        {
            string element = text[end..next];
            if (element is "\n" or "\r" or "\r\n")
            {
                lines.Add(new(start, end - start));
                start = end = next; lastBreak = -1; continue;
            }
            if (end > start && measure(text[start..next]) > Math.Max(1, width))
            {
                int split = lastBreak > start ? lastBreak : end;
                lines.Add(new(start, split - start)); start = split; lastBreak = -1;
            }
            end = next;
            if (char.IsWhiteSpace(element[0])) lastBreak = end;
        }
        lines.Add(new(start, text.Length - start));
        return lines.ToArray();
    }

    public static int Boundary(string text, int index, bool forward = false)
    {
        index = Math.Clamp(index, 0, text.Length);
        if (index == text.Length) return index;
        int[] boundaries = StringInfo.ParseCombiningCharacters(text);
        int found = Array.BinarySearch(boundaries, index);
        if (found >= 0) return index;
        int insertion = ~found;
        return forward ? insertion < boundaries.Length ? boundaries[insertion] : text.Length
            : insertion > 0 ? boundaries[insertion - 1] : 0;
    }
}

internal sealed partial class WaylandEmulatorHost
{
    private bool showMessageDetails;
    private string messageDetailsText = "";
    private string? errorDetails;
    private string? errorDetailsSummary;
    private int messageCaret, messageAnchor, messageScrollLine;
    private bool draggingMessageSelection;
    private LinuxWrappedLine[] messageLines = [];
    private string? messageLayoutText;
    private float messageLayoutWidth;
    private int messageLayoutSize;
    private SDL.FRect messageViewport;
    private int messageVisibleLines = 1;
    private float MessageLineHeight => Math.Max(16, textRenderer.MinimumSize) + 7;
    private FocusIdentity? messageReturnFocus;
    private AetherBoy.Runtime.EmulationSession? messagePausedSession;
    private bool messageResumeOnClose;
    private bool messageReturnTextFocus;

    private void SetMessageFailure(string summary, Exception exception)
    {
        loadError = statusMessage = summary;
        errorDetailsSummary = summary;
        errorDetails = summary + "\n\n" + UiText.TechnicalDetails(exception.ToString());
    }

    private void OpenMessageDetails(string text)
    {
        if (textEditor.IsComposing) return;
        messageReturnTextFocus = editingTitleIdentity is not null && editorHasFocus;
        if (editingTitleIdentity is not null)
        {
            EnsureTextEditor(); SyncTextEditor();
            editorHasFocus = false; draggingTextSelection = false;
            showOnScreenKeyboard = false; SDL.StopTextInput(window);
        }
        else CommitActiveText();
        messageReturnFocus = focusedControl >= 0 && focusedControl < focusIdentities.Count ? focusIdentities[focusedControl] : null;
        messageDetailsText = text;
        messageCaret = messageAnchor = messageScrollLine = 0;
        draggingMessageSelection = false;
        showMessageDetails = true;
        focusedControl = -1;
        pressedKeys.Clear();
        ReleaseMouseTurbo();
        messagePausedSession = session;
        messageResumeOnClose = false;
        if (!IsOnlineLink && session is { } active && active.State is AetherBoy.Runtime.SessionState.Running or AetherBoy.Runtime.SessionState.Paused)
        {
            active.SetTurboAsync(false).GetAwaiter().GetResult();
            messageResumeOnClose = !active.LatestSnapshot.IsPaused;
            if (messageResumeOnClose) active.SetPausedAsync(true).GetAwaiter().GetResult();
            PostInput(active); audioOutput?.Clear();
        }
        PauseLocalLinkForMessageDetails();
    }

    private void CloseMessageDetails()
    {
        showMessageDetails = false; draggingMessageSelection = false;
        if (messageResumeOnClose && ReferenceEquals(session, messagePausedSession) && session is { State: AetherBoy.Runtime.SessionState.Paused } active)
        {
            if (!windowFocused && options.PauseOnFocusLoss) resumeAfterFocus = true;
            else if (showSofaLibrary) sofaResumeOnClose = true;
            else if (showQuickDeck) quickDeckResumeOnClose = true;
            else if (controlCenterVisible) resumeAfterControlCenter = true;
            else active.SetPausedAsync(false).GetAwaiter().GetResult();
        }
        messageResumeOnClose = false; messagePausedSession = null;
        RestoreLocalLinkFromMessageDetails();
        focusedControl = -1;
        DrawShell();
        if (messageReturnFocus is { } previous) focusedControl = focusIdentities.IndexOf(previous);
        if (messageReturnTextFocus && editingTitleIdentity is not null)
        { EnsureTextEditor(); editorHasFocus = true; focusedControl = -1; SDL.StartTextInput(window); }
        messageReturnTextFocus = false;
        messageReturnFocus = null;
    }

    private void CopyMessageDetails(bool selectionOnly = false)
    {
        int first = Math.Min(messageAnchor, messageCaret), length = Math.Abs(messageCaret - messageAnchor);
        if (selectionOnly && length == 0) return;
        try { textClipboard.Write(length > 0 && selectionOnly ? messageDetailsText.Substring(first, length) : messageDetailsText); }
        catch (Exception ex) { diagnostics.Failure("message_clipboard", ex); }
    }

    // A bounded footer keeps keyboard help separate. Every shortened message has an explicit full-text route.
    private void DrawMessageStatus(float x, float y, float width, float height, string text, bool error, bool singleLine = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        int size = singleLine ? 12 : 14;
        float step = Math.Max(size, textRenderer.MinimumSize) + 7;
        float buttonWidth = Math.Max(124, textRenderer.Measure(UiText.Get("Details"), 13, true) + 24);
        string? details = error && loadError == errorDetailsSummary ? errorDetails : null;
        int maximumLines = singleLine ? 1 : Math.Max(1, (int)(height / step));
        var lines = LinuxMessageLayout.Wrap(text, width, value => textRenderer.Measure(value, size));
        bool needsDetails = details is not null || lines.Length > maximumLines;
        if (needsDetails)
        {
            float available = width - buttonWidth - 16;
            lines = LinuxMessageLayout.Wrap(text, available, value => textRenderer.Measure(value, size));
            string summary = lines.Length <= maximumLines ? text : ShortStatusSummary(text, error);
            lines = LinuxMessageLayout.Wrap(summary, available, value => textRenderer.Measure(value, size));
            for (int i = 0; i < Math.Min(lines.Length, maximumLines); i++)
                Ink(x, y + i * step, i == maximumLines - 1 && lines.Length > maximumLines
                    ? textRenderer.Fit(lines[i].Read(summary) + "…", available, size) : lines[i].Read(summary), size, error ? Colors.Danger : Colors.Muted);
            string full = details ?? text;
            ActionButton(x + width - buttonWidth, y - 3, buttonWidth, singleLine ? 30 : 42, UiText.Get("Details"), () => OpenMessageDetails(full), focusId: "message:details");
        }
        else for (int i = 0; i < lines.Length; i++) Ink(x, y + i * step, lines[i].Read(text), size, error ? Colors.Danger : Colors.Muted);
    }

    private static string ShortStatusSummary(string text, bool error)
    {
        if (text == UiText.Get("Loading failed. The previous resume point is kept. Check Details before trying again."))
            return UiText.Get("Loading failed. The previous resume point is kept.");
        if (text == UiText.Get("Save information could not be read. Refresh and try again."))
            return UiText.Get("Save information could not be read.");
        if (text == UiText.Get("The state could not be saved or loaded. Check Details and try again."))
            return UiText.Get("Saving or loading failed.");
        if (text == UiText.Get("File access was denied. Check permissions and try again."))
            return UiText.Get("File access was denied.");
        if (text == UiText.Get("The selected data is invalid. Check the file and try again."))
            return UiText.Get("The selected data is invalid.");
        if (text == UiText.Get("A file could not be read or saved. Check the drive and try again."))
            return UiText.Get("A file could not be read or saved.");
        return UiText.Get(error ? "The action failed. Open Details." : "Read the full message in Details.");
    }

    private void DrawMessageDetails()
    {
        float x = 48, y = 36, width = LogicalWidth - 96, height = LogicalHeight - 72;
        Panel(x, y, width, height);
        Ink(x + 24, y + 20, UiText.Get("Details"), 24, bold: true);
        messageViewport = new() { X = x + 24, Y = y + 72, W = width - 48, H = height - 198 };
        float wrapWidth = messageViewport.W - 30;
        int wrapSize = Math.Max(16, textRenderer.MinimumSize);
        if (messageLayoutText != messageDetailsText || messageLayoutWidth != wrapWidth || messageLayoutSize != wrapSize)
        {
            messageLines = LinuxMessageLayout.Wrap(messageDetailsText, wrapWidth, value => textRenderer.Measure(value, 16));
            messageLayoutText = messageDetailsText; messageLayoutWidth = wrapWidth; messageLayoutSize = wrapSize;
        }
        messageVisibleLines = Math.Max(1, (int)(messageViewport.H / MessageLineHeight));
        messageScrollLine = Math.Clamp(messageScrollLine, 0, Math.Max(0, messageLines.Length - messageVisibleLines));
        Paint(messageViewport.X, messageViewport.Y, messageViewport.W, messageViewport.H, Colors.Void);
        int selectionStart = Math.Min(messageAnchor, messageCaret), selectionEnd = Math.Max(messageAnchor, messageCaret);
        for (int i = messageScrollLine; i < Math.Min(messageLines.Length, messageScrollLine + messageVisibleLines); i++)
        {
            LinuxWrappedLine line = messageLines[i];
            float top = messageViewport.Y + (i - messageScrollLine) * MessageLineHeight + 3;
            int start = Math.Clamp(selectionStart, line.Start, line.Start + line.Length), end = Math.Clamp(selectionEnd, line.Start, line.Start + line.Length);
            if (end > start)
                Paint(messageViewport.X + 10 + textRenderer.Measure(messageDetailsText[line.Start..start], 16), top,
                    textRenderer.Measure(messageDetailsText[start..end], 16), MessageLineHeight, Colors.Border);
            Ink(messageViewport.X + 10, top, line.Read(messageDetailsText), 16);
        }
        float trackX = messageViewport.X + messageViewport.W - 9;
        Paint(trackX, messageViewport.Y, 3, messageViewport.H, Colors.Border);
        float thumbHeight = Math.Max(24, messageViewport.H * Math.Min(1, messageVisibleLines / (float)messageLines.Length));
        float thumbY = messageViewport.Y + (messageViewport.H - thumbHeight) * messageScrollLine / Math.Max(1, messageLines.Length - messageVisibleLines);
        Paint(trackX - 2, thumbY, 7, thumbHeight, Colors.Cyan);
        Ink(x + 24, y + height - 108, UiText.Format("Lines {0}–{1} of {2}", messageScrollLine + 1,
            Math.Min(messageLines.Length, messageScrollLine + messageVisibleLines), messageLines.Length), 14, Colors.Muted);
        float buttonY = y + height - 72;
        ActionButton(x + 24, buttonY, 138, 42, UiText.Get("Previous"), () => ScrollMessageDetails(-messageVisibleLines), enabled: messageScrollLine > 0, focusId: "message:previous");
        ActionButton(x + 174, buttonY, 138, 42, UiText.Get("Next"), () => ScrollMessageDetails(messageVisibleLines), enabled: messageScrollLine + messageVisibleLines < messageLines.Length, focusId: "message:next");
        ActionButton(x + 324, buttonY, 196, 42, UiText.Get("Select all"), () => { messageAnchor = 0; messageCaret = messageDetailsText.Length; }, focusId: "message:select");
        ActionButton(x + width - 350, buttonY, 172, 42, UiText.Get("Copy all"), () => CopyMessageDetails(), focusId: "message:copy");
        ActionButton(x + width - 166, buttonY, 142, 42, UiText.Get("Close"), CloseMessageDetails, focusId: "message:close");
    }

    private void ScrollMessageDetails(int amount) => messageScrollLine = Math.Clamp(messageScrollLine + amount, 0, Math.Max(0, messageLines.Length - messageVisibleLines));

    private bool HandleMessageDetailsKey(SDL.KeyboardEvent key, bool pressed)
    {
        if (!showMessageDetails) return false;
        if (!pressed) return true;
        if (key.Scancode == SDL.Scancode.Escape) { CloseMessageDetails(); return true; }
        if (key.Scancode == SDL.Scancode.F7 && (key.Mod & SDL.Keymod.Ctrl) != 0) { OpenAccessibleControls(); return true; }
        if ((key.Mod & SDL.Keymod.Ctrl) != 0)
        {
            if (key.Scancode == SDL.Scancode.A) { messageAnchor = 0; messageCaret = messageDetailsText.Length; }
            else if (key.Scancode == SDL.Scancode.C) CopyMessageDetails(selectionOnly: true);
            else if (key.Scancode == SDL.Scancode.Home) messageScrollLine = 0;
            else if (key.Scancode == SDL.Scancode.End) messageScrollLine = Math.Max(0, messageLines.Length - messageVisibleLines);
            return true;
        }
        if (key.Scancode is SDL.Scancode.Up or SDL.Scancode.Down or SDL.Scancode.Pageup or SDL.Scancode.Pagedown)
        { ScrollMessageDetails((key.Scancode is SDL.Scancode.Up or SDL.Scancode.Pageup ? -1 : 1) * (key.Scancode is SDL.Scancode.Pageup or SDL.Scancode.Pagedown ? messageVisibleLines : 1)); return true; }
        if (key.Scancode is SDL.Scancode.Left or SDL.Scancode.Right or SDL.Scancode.Home or SDL.Scancode.End)
        {
            messageCaret = key.Scancode switch
            {
                SDL.Scancode.Home => 0, SDL.Scancode.End => messageDetailsText.Length,
                SDL.Scancode.Left => LinuxMessageLayout.Boundary(messageDetailsText, messageCaret - 1),
                _ => LinuxMessageLayout.Boundary(messageDetailsText, messageCaret + 1, true)
            };
            if ((key.Mod & SDL.Keymod.Shift) == 0) messageAnchor = messageCaret;
            int line = Math.Max(0, Array.FindLastIndex(messageLines, item => item.Start <= messageCaret));
            if (line < messageScrollLine) messageScrollLine = line;
            if (line >= messageScrollLine + messageVisibleLines) messageScrollLine = line - messageVisibleLines + 1;
            return true;
        }
        if (!key.Repeat && key.Scancode is SDL.Scancode.Tab or SDL.Scancode.F6)
        { MoveMessageFocus((key.Mod & SDL.Keymod.Shift) == 0 ? 1 : -1); return true; }
        if (!key.Repeat && key.Scancode is SDL.Scancode.Return or SDL.Scancode.Space) ActivateMessageFocus();
        return true;
    }

    private void MoveMessageFocus(int direction)
    {
        DrawShell();
        if (focusTargets.Count > 0) focusedControl = focusedControl < 0 ? direction > 0 ? 0 : focusTargets.Count - 1
            : (focusedControl + direction + focusTargets.Count) % focusTargets.Count;
    }

    private void ActivateMessageFocus()
    {
        DrawShell();
        if (focusedControl >= 0 && focusedControl < focusTargets.Count)
        { var target = focusTargets[focusedControl]; HandleMouseClick(target.X + target.W / 2, target.Y + target.H / 2); }
    }

    private bool HandleMessageDetailsGamepad(SDL.GamepadButton button, bool down)
    {
        if (!showMessageDetails) return false;
        if (!down) return true;
        if (button == SDL.GamepadButton.East) CloseMessageDetails();
        else if (button == SDL.GamepadButton.South) ActivateMessageFocus();
        else if (button is SDL.GamepadButton.DPadUp or SDL.GamepadButton.DPadDown) ScrollMessageDetails(button == SDL.GamepadButton.DPadUp ? -1 : 1);
        else if (button is SDL.GamepadButton.DPadLeft or SDL.GamepadButton.DPadRight) MoveMessageFocus(button == SDL.GamepadButton.DPadLeft ? -1 : 1);
        return true;
    }

    private bool HandleMessagePointerDown(float x, float y, bool extend)
    {
        if (!showMessageDetails || !Hit(x, y, messageViewport.X, messageViewport.Y, messageViewport.W, messageViewport.H)) return false;
        draggingMessageSelection = true;
        PlaceMessageCaret(x, y, extend);
        focusedControl = -1;
        return true;
    }

    private void PlaceMessageCaret(float x, float y, bool extend)
    {
        if (messageLines.Length == 0) return;
        int lineIndex = Math.Clamp(messageScrollLine + (int)Math.Floor((y - messageViewport.Y) / MessageLineHeight), 0, messageLines.Length - 1);
        var line = messageLines[lineIndex];
        string text = line.Read(messageDetailsText);
        int previous = 0, target = text.Length;
        foreach (int end in StringInfo.ParseCombiningCharacters(text).Skip(1).Append(text.Length))
        {
            if (x - messageViewport.X - 10 < (textRenderer.Measure(text[..previous], 16) + textRenderer.Measure(text[..end], 16)) / 2) { target = previous; break; }
            previous = end;
        }
        messageCaret = line.Start + target;
        if (!extend) messageAnchor = messageCaret;
    }

    private void HandleMessagePointerMotion(float x, float y)
    {
        if (!showMessageDetails || !draggingMessageSelection) return;
        if (y < messageViewport.Y) ScrollMessageDetails(-1);
        if (y > messageViewport.Y + messageViewport.H) ScrollMessageDetails(1);
        PlaceMessageCaret(x, y, true);
    }
}
