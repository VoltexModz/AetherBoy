using System.Reflection;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxTextEditorTests
{
    [TestMethod]
    public void CheatPasteKeepsCodeBoundariesAndOrdinaryFieldsKeepTheirSingleLineBehavior()
    {
        var editor = new LinuxTextEditor(32768) { LineBreakReplacement = " + " };
        var clipboard = new Clipboard { Value = "9ABCDEF0 1234\r\n932C7D22 AC8D\n82000004 BEEF" };
        editor.Key(LinuxTextKey.Paste, false, clipboard);
        Assert.AreEqual("9ABCDEF0 1234 + 932C7D22 AC8D + 82000004 BEEF", editor.Text);
        editor.LineBreakReplacement = "";
        editor.SetText("room\rcode");
        Assert.AreEqual("roomcode", editor.Text);
    }

    private sealed class Clipboard : ILinuxTextClipboard
    {
        public string Value = "";
        public int Reads, Writes;
        public string Read() { Reads++; return Value; }
        public void Write(string value) { Writes++; Value = value; }
    }

    [TestMethod]
    public void SelectionNavigationAndReplacementOperateAtTheCaret()
    {
        var editor = new LinuxTextEditor(); var clipboard = new Clipboard();
        editor.SetText("abcd");
        editor.Key(LinuxTextKey.Home, false, clipboard);
        editor.Key(LinuxTextKey.Right, false, clipboard);
        editor.Key(LinuxTextKey.Right, true, clipboard);
        editor.Key(LinuxTextKey.Right, true, clipboard);
        Assert.AreEqual(1, editor.SelectionStart); Assert.AreEqual(2, editor.SelectionLength);
        editor.Insert("XY"); Assert.AreEqual("aXYd", editor.Text); Assert.AreEqual(3, editor.Caret);
        editor.Key(LinuxTextKey.Home, true, clipboard);
        editor.Key(LinuxTextKey.Left, false, clipboard);
        Assert.AreEqual(0, editor.Caret); Assert.IsFalse(editor.HasSelection);
        editor.Key(LinuxTextKey.End, true, clipboard);
        Assert.IsTrue(editor.AllSelected);
        Assert.AreEqual(0, clipboard.Reads + clipboard.Writes);
    }

    [TestMethod]
    public void GraphemeDeletionPreservesEmojiSurrogatesCombiningMarksAndZwjSequences()
    {
        var editor = new LinuxTextEditor(); var clipboard = new Clipboard();
        const string family = "👩‍👩‍👧‍👦";
        editor.SetText("Ae\u0301" + family + "Z");
        editor.Key(LinuxTextKey.Left, false, clipboard);
        editor.Key(LinuxTextKey.Backspace, false, clipboard);
        Assert.AreEqual("Ae\u0301Z", editor.Text);
        editor.Key(LinuxTextKey.Left, false, clipboard);
        editor.Key(LinuxTextKey.Delete, false, clipboard);
        Assert.AreEqual("AZ", editor.Text);
        editor.SetText("😀B"); editor.Key(LinuxTextKey.Home, false, clipboard);
        editor.Key(LinuxTextKey.Delete, false, clipboard); Assert.AreEqual("B", editor.Text);
        editor.SetText(""); editor.Key(LinuxTextKey.Backspace, false, clipboard); editor.Key(LinuxTextKey.Delete, false, clipboard);
        Assert.AreEqual(0, editor.Caret); Assert.AreEqual("", editor.Text);
    }

    [TestMethod]
    public void ClipboardIsAccessedOnlyForExplicitCommandsAndPasteIsSingleLine()
    {
        var editor = new LinuxTextEditor(); var clipboard = new Clipboard();
        editor.SetText("hello"); editor.Key(LinuxTextKey.Copy, false, clipboard);
        Assert.AreEqual(0, clipboard.Writes);
        editor.Select(1, 4); editor.Key(LinuxTextKey.Copy, false, clipboard); Assert.AreEqual("ell", clipboard.Value);
        editor.Key(LinuxTextKey.Cut, false, clipboard); Assert.AreEqual("ho", editor.Text);
        clipboard.Value = "😀\n\tX\u2028Y";
        editor.Key(LinuxTextKey.Paste, false, clipboard);
        Assert.AreEqual("h😀XYo", editor.Text); Assert.AreEqual(1, clipboard.Reads); Assert.AreEqual(2, clipboard.Writes);
    }

    [TestMethod]
    public void EmptyOrFailedClipboardPastePreservesTextAndSelection()
    {
        var editor = new LinuxTextEditor(); editor.SetText("keep this"); editor.SelectAll();
        var empty = new Clipboard();
        editor.Key(LinuxTextKey.Paste, false, empty);
        Assert.AreEqual("keep this", editor.Text); Assert.IsTrue(editor.AllSelected);
        string error = "stale error"; bool cleared = false;
        var failing = new LinuxSdlTextClipboard(
            read: () => { Assert.IsTrue(cleared); error = "clipboard unavailable"; return ""; },
            clearError: () => { cleared = true; error = ""; }, error: () => error, write: _ => { });
        Assert.ThrowsExactly<InvalidOperationException>(() => editor.Key(LinuxTextKey.Paste, false, failing));
        Assert.AreEqual("keep this", editor.Text); Assert.IsTrue(editor.AllSelected);
        var staleOnly = new LinuxSdlTextClipboard(read: () => "", clearError: () => error = "", error: () => error, write: _ => { });
        editor.Key(LinuxTextKey.Paste, false, staleOnly);
        Assert.AreEqual("keep this", editor.Text); Assert.IsTrue(editor.AllSelected);
        editor.Key(LinuxTextKey.Delete, false, empty); Assert.AreEqual("", editor.Text);
    }

    [TestMethod]
    public void LengthLimitNeverSplitsATextElement()
    {
        var editor = new LinuxTextEditor(4);
        editor.SetText("abc😀"); Assert.AreEqual("abc", editor.Text);
        editor.SetText("abe\u0301"); Assert.AreEqual("abe\u0301", editor.Text);
        editor.Select(0, 2); editor.Insert("😀😀"); Assert.AreEqual("😀e\u0301", editor.Text);
        Assert.AreEqual(2, editor.Caret);
    }

    [TestMethod]
    public void HorizontalScrollTracksCaretAndReturnsToOriginForEmptyText()
    {
        var editor = new LinuxTextEditor(); var clipboard = new Clipboard();
        editor.SetText(new string('W', 40));
        editor.EnsureCaretVisible(100, text => text.Length * 10);
        Assert.IsTrue(editor.ScrollOffset > 0);
        Assert.IsTrue(editor.Caret * 10 - editor.ScrollOffset <= 100);
        editor.Key(LinuxTextKey.Home, false, clipboard); editor.EnsureCaretVisible(100, text => text.Length * 10);
        Assert.AreEqual(0f, editor.ScrollOffset);
        editor.SelectAll(); editor.Insert(""); editor.EnsureCaretVisible(100, text => text.Length * 10);
        Assert.AreEqual(0f, editor.ScrollOffset);
    }

    [TestMethod]
    public void ImeCompositionUsesCodepointOffsetsAndCommitsWithoutDuplicatingSelectedText()
    {
        var editor = new LinuxTextEditor(); var clipboard = new Clipboard();
        editor.SetText("AoldB"); editor.Select(1, 4);
        editor.SetComposition("😀你好", 1, 1);
        Assert.AreEqual("AoldB", editor.Text); Assert.AreEqual("A😀你好B", editor.DisplayText);
        Assert.AreEqual(2, editor.CompositionCursor); Assert.AreEqual(1, editor.CompositionSelectionLength);
        editor.Key(LinuxTextKey.Paste, false, clipboard); Assert.AreEqual(0, clipboard.Reads);
        editor.Insert("你好"); Assert.AreEqual("A你好B", editor.Text); Assert.IsFalse(editor.IsComposing);
        editor.SetComposition("😀", -1, -1); Assert.AreEqual(2, editor.CompositionCursor);
        editor.CancelComposition(); Assert.AreEqual("A你好B", editor.Text);
        editor.SetComposition("", 0, 0); Assert.IsFalse(editor.IsComposing);
    }

    [TestMethod]
    public void PointerPlacementAndShiftSelectionSnapToWholeGraphemes()
    {
        var editor = new LinuxTextEditor(); editor.SetText("A😀B");
        editor.PlaceCaret(16, value => value.Length * 10, false); Assert.AreEqual(1, editor.Caret);
        editor.PlaceCaret(24, value => value.Length * 10, false); Assert.AreEqual(3, editor.Caret);
        editor.PlaceCaret(0, value => value.Length * 10, true);
        Assert.AreEqual(0, editor.SelectionStart); Assert.AreEqual(3, editor.SelectionLength);
        editor.PlaceCaret(200, value => value.Length * 10, true);
        Assert.AreEqual(3, editor.SelectionStart); Assert.AreEqual(1, editor.SelectionLength);
    }

    [TestMethod]
    public void NativeFieldsShareEditingClipboardAndCompositionWithoutDiscardOnNavigation()
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Requires the native Wayland UI test session."); return; }
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-text-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string settings = Path.Combine(directory, "settings.json");
        LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false });
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
        Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events | SDL.InitFlags.Gamepad), SDL.GetError());
        try
        {
            using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
            var clipboard = new Clipboard(); Set(host, "textClipboard", clipboard);
            void Key(SDL.Scancode code, SDL.Keymod mods = SDL.Keymod.None) => Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = code, Mod = mods }, true);
            foreach (var fields in new[] { ("editingSearch", "librarySearch"), ("editingCheat", "cheatCode") })
            {
                Set(host, fields.Item1, true); Set(host, fields.Item2, "AB");
                Key(SDL.Scancode.Left); Call(host, "ReceiveTextInput", "😀");
                Assert.AreEqual("A😀B", Field<string>(host, fields.Item2));
                Key(SDL.Scancode.A, SDL.Keymod.Ctrl); Key(SDL.Scancode.C, SDL.Keymod.Ctrl);
                Assert.AreEqual("A😀B", clipboard.Value);
                Key(SDL.Scancode.V, SDL.Keymod.Ctrl | SDL.Keymod.Alt);
                Assert.AreEqual(0, clipboard.Reads, "AltGr-style Ctrl+Alt must not trigger clipboard paste.");
                Key(SDL.Scancode.Escape); Assert.IsFalse(Field<bool>(host, fields.Item1));
                Assert.AreEqual("A😀B", Field<string>(host, fields.Item2));
            }
            Call(host, "ToggleControlCenter");
            Type page = typeof(WaylandEmulatorHost).GetNestedType("ControlCenterPage", BindingFlags.NonPublic)!;
            Call(host, "SelectControlCenterPage", Enum.Parse(page, "Library"));
            Set(host, "editingTitleIdentity", new string('A', 64)); Set(host, "titleInput", new string('W', 80));
            Call(host, "DrawShell");
            Assert.IsTrue(Field<LinuxTextEditor>(host, "textEditor").ScrollOffset > 0);
            Key(SDL.Scancode.A, SDL.Keymod.Ctrl); Call(host, "ReceiveTextComposition", "😀你", 1, 1);
            Assert.AreEqual(new string('W', 80), Field<string>(host, "titleInput"));
            Key(SDL.Scancode.Escape); Assert.IsNotNull(Field<string?>(host, "editingTitleIdentity"));
            Call(host, "ReceiveTextInput", "Title😀"); Assert.AreEqual("Title😀", Field<string>(host, "titleInput"));
            Key(SDL.Scancode.Tab); Assert.AreEqual("Title😀", Field<string>(host, "titleInput"));
            Assert.IsFalse(SDL.TextInputActive(Field<IntPtr>(host, "window")));
            Call(host, "ReceiveTextInput", "hidden"); Call(host, "ReceiveTextComposition", "hidden", 0, 1);
            Assert.AreEqual("Title😀", Field<string>(host, "titleInput"));
            Assert.IsFalse(Field<LinuxTextEditor>(host, "textEditor").IsComposing);
            Call(host, "DrawShell");
            var entries = Field<System.Collections.IDictionary>(host, "textEntryBounds");
            object titleKey = entries.Keys.Cast<object>().Single(key => key.ToString() == "Title");
            var titleBounds = (SDL.FRect)entries[titleKey]!;
            Call(host, "HandleTextPointerDown", titleBounds.X + 12, titleBounds.Y + titleBounds.H / 2, false);
            Assert.IsTrue(SDL.TextInputActive(Field<IntPtr>(host, "window")));
            Assert.AreEqual(0, Field<LinuxTextEditor>(host, "textEditor").Caret);
            Call(host, "HandleTextPointerMotion", titleBounds.X + titleBounds.W - 12);
            Assert.IsTrue(Field<LinuxTextEditor>(host, "textEditor").AllSelected);
            Set(host, "draggingTextSelection", false);
            Call(host, "CloseControlCenter"); Assert.IsTrue(Field<bool>(host, "controlCenterVisible"));
            var busy = new TaskCompletionSource(); Set(host, "libraryMutation", busy.Task);
            Key(SDL.Scancode.Escape); Call(host, "ReceiveTextInput", "ignored");
            Assert.AreEqual("Title😀", Field<string>(host, "titleInput")); Assert.IsNotNull(Field<string?>(host, "editingTitleIdentity"));
            Set(host, "libraryMutation", null); Key(SDL.Scancode.Escape);
            Assert.IsNull(Field<string?>(host, "editingTitleIdentity"));
            Assert.AreEqual(0, clipboard.Reads); Assert.AreEqual(2, clipboard.Writes);
            if (Environment.GetEnvironmentVariable("AETHERBOY_TEXT_CAPTURE_DIR") is { } capture)
            {
                Set(host, "editingTitleIdentity", new string('A', 64));
                Set(host, "titleInput", "Pokémon – Eine längere Reise mit Freunden und einem neuen Titel");
                Set(host, "titleSelectedAll", false);
                Call(host, "EnsureTextEditor");
                var editor = Field<LinuxTextEditor>(host, "textEditor");
                editor.Select(12, 19);
                editor.SetComposition("Größe e\u0301 😀", 1, 4);
                Field<LinuxFrontendOptions>(host, "options").TextSize = 18;
                SDL.SetWindowSize(Field<IntPtr>(host, "window"), 900, 650);
                SDL.PumpEvents(); Call(host, "DrawShell");
                Directory.CreateDirectory(capture);
                IntPtr surface = SDL.RenderReadPixels(Field<IntPtr>(host, "renderer"), null);
                Assert.AreNotEqual(IntPtr.Zero, surface);
                try { Assert.IsTrue(SDL.SavePNG(surface, Path.Combine(capture, "title-ime-large.png"))); }
                finally { SDL.DestroySurface(surface); }
            }
        }
        finally { SDL.Quit(); Directory.Delete(directory, true); }
    }

    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private)!.GetValue(target)!;
    private static void Set(object target, string name, object? value) => target.GetType().GetField(name, Private)!.SetValue(target, value);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private)!.Invoke(target, args);
}
