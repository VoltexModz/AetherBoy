using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace nanoboy.Controls;

/// <summary>Native EDIT is used solely for Unicode/IME, selection and undo. All chrome is ours.</summary>
internal sealed class AetherTextBox : UserControl
{
    private readonly Editor edit;
    private readonly AetherScrollBar vertical = new() { TabStop = false, AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Text vertikal scrollen") };
    private readonly AetherScrollBar horizontal = new() { Direction = Orientation.Horizontal, TabStop = false, AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Text horizontal scrollen") };
    private bool syncing;
    private int horizontalOffset;
    private ScrollBars scrollBars;
    internal TextBox NativeEditor => edit;
    public AetherTextBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoScaleMode = AutoScaleMode.None; Size = new Size(160, 32); TabStop = true;
        edit = new Editor(this) { BorderStyle = BorderStyle.None, AutoSize = false, TabStop = false };
        Controls.AddRange([edit, vertical, horizontal]);
        edit.TextChanged += (_, _) => { SyncScroll(); OnTextChanged(EventArgs.Empty); };
        edit.KeyDown += (_, e) => OnKeyDown(e); edit.KeyUp += (_, e) => OnKeyUp(e); edit.KeyPress += (_, e) => OnKeyPress(e);
        edit.GotFocus += (_, _) => Invalidate(); edit.LostFocus += (_, _) => Invalidate();
        edit.MouseDown += (_, e) => OnMouseDown(e);
        vertical.ValueChanged += (_, _) => { if (!syncing) ScrollLines(vertical.Value - FirstVisibleLine); };
        horizontal.ValueChanged += (_, _) => { if (!syncing) { SendMessage(edit.Handle, 0xB6, (IntPtr)(horizontal.Value - horizontalOffset), IntPtr.Zero); horizontalOffset = horizontal.Value; } };
        RefreshTheme();
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override string Text { get => edit?.Text ?? ""; set { if (edit is not null) edit.Text = value; } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool ReadOnly { get => edit.ReadOnly; set => edit.ReadOnly = value; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool Multiline { get => edit.Multiline; set { edit.Multiline = value; Arrange(); } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool WordWrap { get => edit.WordWrap; set { edit.WordWrap = value; Arrange(); } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool AcceptsReturn { get => edit.AcceptsReturn; set => edit.AcceptsReturn = value; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool AcceptsTab { get => edit.AcceptsTab; set => edit.AcceptsTab = value; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool ShortcutsEnabled { get => edit.ShortcutsEnabled; set => edit.ShortcutsEnabled = value; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool UseSystemPasswordChar { get => edit.UseSystemPasswordChar; set => edit.UseSystemPasswordChar = value; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int MaxLength { get => edit.MaxLength; set => edit.MaxLength = value; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal string PlaceholderText { get => edit.PlaceholderText; set => edit.PlaceholderText = value; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal CharacterCasing CharacterCasing { get => edit.CharacterCasing; set => edit.CharacterCasing = value; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal HorizontalAlignment TextAlign { get => edit.TextAlign; set => edit.TextAlign = value; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal ScrollBars ScrollBars { get => scrollBars; set { scrollBars = value; Arrange(); } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int SelectionStart { get => edit.SelectionStart; set => edit.SelectionStart = value; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int SelectionLength { get => edit.SelectionLength; set => edit.SelectionLength = value; }
    internal int TextLength => edit.TextLength;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal string[] Lines { get => edit.Lines; set => edit.Lines = value; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal string SelectedText { get => edit.SelectedText; set { if (!ReadOnly) SendMessageText(edit.Handle, 0xC2 /* EM_REPLACESEL */, (IntPtr)1, value); } }
    internal bool CanUndo => edit.CanUndo;
    internal int FirstVisibleLine => edit.IsHandleCreated ? (int)SendMessage(edit.Handle, 0xCE, IntPtr.Zero, IntPtr.Zero) : 0;
    internal void Select(int start, int length) => edit.Select(start, length);
    internal void SelectAll() => edit.SelectAll();
    internal void Clear() => edit.Clear();
    internal void AppendText(string text) => edit.AppendText(text);
    internal void ScrollToCaret() { edit.ScrollToCaret(); SyncScroll(); }
    internal void Undo() { if (!ReadOnly) edit.Undo(); }
    internal void Copy() { if (!UseSystemPasswordChar) edit.Copy(); }
    internal void Cut() { if (!ReadOnly && !UseSystemPasswordChar) edit.Cut(); }
    internal void Paste() { if (!ReadOnly) edit.Paste(); }
    internal int GetLineFromCharIndex(int index) => edit.GetLineFromCharIndex(index);
    internal int GetCharIndexFromPosition(Point point) => edit.GetCharIndexFromPosition(new Point(point.X - edit.Left, point.Y - edit.Top));
    internal int GetFirstCharIndexFromLine(int line) => edit.GetFirstCharIndexFromLine(line);
    internal void ScrollLines(int lines) { SendMessage(edit.Handle, 0xB6, IntPtr.Zero, (IntPtr)lines); SyncScroll(); }
    internal void RefreshTheme()
    {
        if (edit is null) return;
        edit.BackColor = AetherColors.SurfaceRaised; edit.ForeColor = Enabled ? AetherColors.Text : AetherColors.Muted;
        edit.AccessibleName = AccessibleName ?? Name; edit.AccessibleDescription = AccessibleDescription;
    }
    protected override void OnEnter(EventArgs e) { base.OnEnter(e); edit?.Focus(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); RefreshTheme(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (edit is not null) { edit.Font = Font; Arrange(); } }
    protected override void OnResize(EventArgs e) { base.OnResize(e); Arrange(); }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Arrange(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        RefreshTheme(); e.Graphics.Clear(Parent?.BackColor ?? AetherColors.Void);
        if (Width < 3 || Height < 3) return;
        using var path = AetherWidgetPaint.Outline(new Rectangle(0, 0, Width - 1, Height - 1));
        using var fill = new SolidBrush(AetherColors.SurfaceRaised); using var pen = new Pen(ContainsFocus ? AetherColors.Cyan : AetherColors.Hairline);
        e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(pen, path);
    }
    private void Arrange()
    {
        if (edit is null || syncing) return;
        int track = Math.Max(14, 16 * DeviceDpi / 96), inset = Math.Max(4, 6 * DeviceDpi / 96);
        bool v = Multiline && scrollBars is ScrollBars.Vertical or ScrollBars.Both;
        bool h = Multiline && !WordWrap && scrollBars is ScrollBars.Horizontal or ScrollBars.Both;
        vertical.Visible = v; horizontal.Visible = h;
        edit.Bounds = new Rectangle(inset, Multiline ? inset : Math.Max(2, (Height - Font.Height) / 2), Math.Max(1, Width - inset * 2 - (v ? track : 0)), Math.Max(1, Multiline ? Height - inset * 2 - (h ? track : 0) : Font.Height + 1));
        vertical.Bounds = new Rectangle(Width - track - 3, 4, track, Math.Max(1, Height - 8 - (h ? track : 0)));
        horizontal.Bounds = new Rectangle(4, Height - track - 3, Math.Max(1, Width - 8 - (v ? track : 0)), track);
        SyncScroll();
    }
    internal void SyncScroll()
    {
        if (edit is null || !edit.IsHandleCreated || syncing) return;
        syncing = true;
        try
        {
            int lines = Math.Max(1, (int)SendMessage(edit.Handle, 0xBA, IntPtr.Zero, IntPtr.Zero));
            vertical.Configure(lines, Math.Max(1, edit.Height / Math.Max(1, Font.Height)));
            vertical.Value = FirstVisibleLine;
            int columns = Math.Max(1, edit.Width / Math.Max(1, TextRenderer.MeasureText("M", Font, Size.Empty, TextFormatFlags.NoPadding).Width));
            horizontal.Configure(Lines.Select(line => line.Length).DefaultIfEmpty(0).Max(), columns);
            if (Multiline && !WordWrap)
            {
                int lineStart = Math.Max(0, edit.GetFirstCharIndexFromLine(FirstVisibleLine));
                horizontalOffset = Math.Max(0, edit.GetCharIndexFromPosition(Point.Empty) - lineStart);
            }
            horizontalOffset = Math.Min(horizontalOffset, horizontal.Maximum); horizontal.Value = horizontalOffset;
        }
        finally { syncing = false; }
    }
    internal void OpenEditMenu(Point screen)
    {
        if (FindForm() is null) return;
        bool selection = SelectionLength > 0 && !UseSystemPasswordChar;
        _ = new AetherPopup(this, screen,
            (global::AetherBoy.Runtime.Localization.UiText.Get("Rückgängig"), !ReadOnly && CanUndo, Undo), (global::AetherBoy.Runtime.Localization.UiText.Get("Ausschneiden"), !ReadOnly && selection, () => ClipboardAction(Cut)),
            (global::AetherBoy.Runtime.Localization.UiText.Get("Kopieren"), selection, () => ClipboardAction(Copy)), (global::AetherBoy.Runtime.Localization.UiText.Get("Einfügen"), !ReadOnly, () => ClipboardAction(Paste)),
            (global::AetherBoy.Runtime.Localization.UiText.Get("Löschen"), !ReadOnly && SelectionLength > 0, () => SelectedText = ""), (global::AetherBoy.Runtime.Localization.UiText.Get("Alles auswählen"), TextLength > 0, SelectAll));
    }
    private void ClipboardAction(Action action)
    {
        try { action(); }
        catch (ExternalException) { AetherSignal.Show(FindForm()!, global::AetherBoy.Runtime.Localization.UiText.Get("Die Zwischenablage ist gerade nicht verfügbar. Versuche es erneut."), global::AetherBoy.Runtime.Localization.UiText.Get("Zwischenablage"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    protected override AccessibleObject CreateAccessibilityInstance() => new InputAccessibility(this);
    private sealed class InputAccessibility(AetherTextBox input) : ControlAccessibleObject(input)
    {
        public override AccessibleRole Role => AccessibleRole.Text;
        public override string? Value { get => input.UseSystemPasswordChar ? "" : input.Text; set { if (!input.ReadOnly) input.Text = value ?? ""; } }
        public override AccessibleStates State => base.State | (input.ReadOnly ? AccessibleStates.ReadOnly : 0) | (input.UseSystemPasswordChar ? AccessibleStates.Protected : 0);
    }
    private sealed class Editor(AetherTextBox owner) : TextBox
    {
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); owner.Arrange(); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x7B) { owner.OpenEditMenu(m.LParam == (IntPtr)(-1) ? PointToScreen(new Point(8, Height)) : new Point(unchecked((short)m.LParam.ToInt64()), unchecked((short)(m.LParam.ToInt64() >> 16)))); return; }
            base.WndProc(ref m);
            if (m.Msg is 0x115 or 0x114 or 0x100 or 0x202 or 0x20A or 0xC2) owner.SyncScroll();
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        { owner.ScrollLines(-e.Delta / 120 * Math.Max(1, SystemInformation.MouseWheelScrollLines)); if (e is HandledMouseEventArgs handled) handled.Handled = true; }
    }
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageText(IntPtr window, int message, IntPtr wParam, string text);
}
