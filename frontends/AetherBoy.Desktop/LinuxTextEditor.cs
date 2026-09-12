using System.Globalization;
using System.Text;

namespace AetherBoy.Desktop;

internal interface ILinuxTextClipboard
{
    string Read();
    void Write(string value);
}

internal enum LinuxTextKey { Left, Right, Home, End, Backspace, Delete, SelectAll, Copy, Cut, Paste }

/// <summary>Single-line, UTF-16-indexed editor whose caret and selection always lie on grapheme boundaries.</summary>
internal sealed class LinuxTextEditor(int maximumLength = 80)
{
    public string Text { get; private set; } = "";
    public int Caret { get; private set; }
    public int Anchor { get; private set; }
    public int SelectionStart => Math.Min(Caret, Anchor);
    public int SelectionLength => Math.Abs(Caret - Anchor);
    public bool HasSelection => Caret != Anchor;
    public bool AllSelected => Text.Length > 0 && SelectionLength == Text.Length;
    public string Composition { get; private set; } = "";
    public int CompositionCursor { get; private set; }
    public int CompositionSelectionLength { get; private set; }
    public bool IsComposing => Composition.Length > 0;
    public float ScrollOffset { get; private set; }
    public string DisplayText => IsComposing
        ? Text[..SelectionStart] + Composition + Text[(SelectionStart + SelectionLength)..] : Text;
    public int DisplayCaret => IsComposing ? SelectionStart + CompositionCursor + CompositionSelectionLength : Caret;
    public int DisplaySelectionStart => IsComposing ? SelectionStart + CompositionCursor : SelectionStart;
    public int DisplaySelectionLength => IsComposing ? CompositionSelectionLength : SelectionLength;

    public void SetText(string value)
    {
        Text = Limit(Clean(value), maximumLength);
        Caret = Anchor = Text.Length;
        ScrollOffset = 0;
        CancelComposition();
    }

    public void Select(int anchor, int caret)
    {
        CancelComposition();
        Anchor = Boundary(Text, anchor);
        Caret = Boundary(Text, caret);
    }

    public void SelectAll() => Select(0, Text.Length);

    public void PlaceCaret(float position, Func<string, float> measure, bool extend)
    {
        if (IsComposing) return;
        int previous = 0, target = Text.Length;
        foreach (int end in StringInfo.ParseCombiningCharacters(Text).Skip(1).Append(Text.Length))
        {
            if (position < (measure(Text[..previous]) + measure(Text[..end])) / 2) { target = previous; break; }
            previous = end;
        }
        Caret = target;
        if (!extend) Anchor = target;
    }

    public void Insert(string value)
    {
        string insertion = Limit(Clean(value), Math.Max(0, maximumLength - Text.Length + SelectionLength));
        int start = SelectionStart;
        Text = Text[..start] + insertion + Text[(start + SelectionLength)..];
        // Combining characters can merge with adjacent text. Snap the resulting caret forward.
        Caret = Anchor = Boundary(Text, start + insertion.Length, forward: true);
        CancelComposition();
    }

    public void SetComposition(string value, int start, int length)
    {
        Composition = Clean(value);
        int cursor = start < 0 ? Composition.Length : RuneOffset(Composition, start);
        int end = start < 0 ? cursor : RuneOffset(Composition, start + Math.Max(0, length));
        CompositionCursor = Boundary(Composition, cursor);
        CompositionSelectionLength = Boundary(Composition, end, forward: true) - CompositionCursor;
    }

    public void CancelComposition()
    {
        Composition = "";
        CompositionCursor = CompositionSelectionLength = 0;
    }

    public void Key(LinuxTextKey key, bool shift, ILinuxTextClipboard clipboard)
    {
        if (IsComposing) return; // SDL/IME owns navigation until commit or explicit composition cancellation.
        if (key == LinuxTextKey.SelectAll) { SelectAll(); return; }
        if (key is LinuxTextKey.Copy or LinuxTextKey.Cut)
        {
            if (HasSelection)
            {
                clipboard.Write(Text.Substring(SelectionStart, SelectionLength));
                if (key == LinuxTextKey.Cut) Insert("");
            }
            return;
        }
        if (key == LinuxTextKey.Paste)
        {
            string pasted = Clean(clipboard.Read());
            if (pasted.Length > 0) Insert(pasted);
            return;
        }
        if (key is LinuxTextKey.Backspace or LinuxTextKey.Delete)
        {
            if (HasSelection) { Insert(""); return; }
            int other = key == LinuxTextKey.Backspace ? Previous(Text, Caret) : Next(Text, Caret);
            Select(Caret, other);
            Insert("");
            return;
        }
        int target = key switch
        {
            LinuxTextKey.Home => 0,
            LinuxTextKey.End => Text.Length,
            LinuxTextKey.Left => !shift && HasSelection ? SelectionStart : Previous(Text, Caret),
            LinuxTextKey.Right => !shift && HasSelection ? SelectionStart + SelectionLength : Next(Text, Caret),
            _ => Caret
        };
        Caret = target;
        if (!shift) Anchor = target;
    }

    public void EnsureCaretVisible(float width, Func<string, float> measure)
    {
        width = Math.Max(1, width);
        string display = DisplayText;
        float caret = measure(display[..DisplayCaret]);
        float content = measure(display);
        if (caret < ScrollOffset) ScrollOffset = caret;
        if (caret > ScrollOffset + width - 2) ScrollOffset = caret - width + 2;
        ScrollOffset = Math.Clamp(ScrollOffset, 0, Math.Max(0, content - width + 2));
    }

    private static string Clean(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new StringBuilder(value.Length);
        foreach (Rune rune in value.EnumerateRunes()) if (!Rune.IsControl(rune) && rune.Value is not (0x2028 or 0x2029)) result.Append(rune.ToString());
        return result.ToString();
    }

    private static string Limit(string value, int length)
    {
        if (value.Length <= length) return value;
        return value[..Boundary(value, length)];
    }

    private static int Boundary(string value, int index, bool forward = false)
    {
        index = Math.Clamp(index, 0, value.Length);
        if (index == value.Length) return index;
        int[] starts = StringInfo.ParseCombiningCharacters(value);
        int found = Array.BinarySearch(starts, index);
        if (found >= 0) return index;
        int insertion = ~found;
        return forward ? insertion < starts.Length ? starts[insertion] : value.Length
            : insertion > 0 ? starts[insertion - 1] : 0;
    }

    private static int Previous(string text, int caret) => Boundary(text, Math.Max(0, caret - 1));
    private static int Next(string text, int caret) => Boundary(text, Math.Min(text.Length, caret + 1), forward: true);
    private static int RuneOffset(string value, int count)
    {
        int offset = 0;
        foreach (Rune rune in value.EnumerateRunes()) { if (count-- <= 0) break; offset += rune.Utf16SequenceLength; }
        return offset;
    }
}
