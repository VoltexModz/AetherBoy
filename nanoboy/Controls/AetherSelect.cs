using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace nanoboy.Controls;

/// <summary>Read-only choice field with an in-window Aether popup, no native combobox/listbox.</summary>
internal sealed class AetherSelect : Control, IMessageFilter
{
    private int selectedIndex = -1;
    private bool hovered;
    private ChoicePopup? popup;
    private Form? popupOwner;
    private string search = "";
    private long searchedAt;
    internal event EventHandler? SelectedIndexChanged;
    internal ChoiceItems Items { get; }
    internal bool IsOpen => popup is not null;
    internal Control? PopupControl => popup;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (value < -1 || value >= Items.Count) throw new ArgumentOutOfRangeException(nameof(value));
            if (selectedIndex == value) return;
            selectedIndex = value;
            base.Text = SelectedItem?.ToString() ?? "";
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedItem { get => selectedIndex >= 0 ? Items[selectedIndex] : null; set => SelectedIndex = value is null ? -1 : Items.IndexOf(value); }
    public override string Text { get => SelectedItem?.ToString() ?? ""; set { if (Items is not null) SelectedIndex = Items.ToList().FindIndex(item => item.ToString() == value); } }
    public AetherSelect()
    {
        Items = new ChoiceItems(this);
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = true; Size = new Size(160, 32); Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.ComboBox;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? AetherColors.Void);
        if (Width < 4 || Height < 4) return;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = AetherWidgetPaint.Outline(bounds);
        using var fill = new SolidBrush(AetherColors.SurfaceRaised);
        using var border = new Pen(Enabled && (Focused || hovered || IsOpen) ? AetherColors.Cyan : AetherColors.Hairline);
        e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(border, path);
        bool rtl = RightToLeft == RightToLeft.Yes;
        int arrow = Math.Min(28 * DeviceDpi / 96, Width / 3);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(rtl ? arrow : 8, 2, Math.Max(1, Width - arrow - 12), Height - 4),
            Enabled ? AetherColors.Text : AetherColors.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
            (rtl ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : TextFormatFlags.Left));
        int x = rtl ? arrow / 2 : Width - arrow / 2, y = Height / 2;
        using var pen = new Pen(Enabled ? AetherColors.Cyan : AetherColors.Muted, 1.5f);
        e.Graphics.DrawLines(pen, new Point[] { new(x - 4, y - 2), new(x, y + 2), new(x + 4, y - 2) });
        if (Focused) AetherWidgetPaint.Focus(e.Graphics, Rectangle.Inflate(bounds, -3, -3), AetherColors.Cyan);
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { if (!Enabled) CloseDropDown(); base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnVisibleChanged(EventArgs e) { if (!Visible) CloseDropDown(); base.OnVisibleChanged(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus(); if (IsOpen) CloseDropDown(); else OpenDropDown();
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F4 || keyData == (Keys.Alt | Keys.Down) || keyData == Keys.Space || keyData == Keys.Enter)
        { OpenDropDown(); return true; }
        if (Items.Count > 0 && (keyData is Keys.Up or Keys.Down or Keys.Home or Keys.End))
        {
            SelectedIndex = keyData switch { Keys.Home => 0, Keys.End => Items.Count - 1,
                Keys.Up => Math.Max(0, selectedIndex - 1), _ => Math.Min(Items.Count - 1, selectedIndex + 1) };
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar)) { Search(e.KeyChar); e.Handled = true; }
        base.OnKeyPress(e);
    }
    private void Search(char character)
    {
        if (Environment.TickCount64 - searchedAt > 900) search = "";
        searchedAt = Environment.TickCount64;
        // Repeated single letters cycle through choices, longer prefixes narrow them down.
        search = search.Length == 1 && char.ToUpperInvariant(search[0]) == char.ToUpperInvariant(character) ? search : search + character;
        int start = popup?.Highlight ?? selectedIndex;
        for (int i = 1; i <= Items.Count; i++)
        {
            int index = (Math.Max(-1, start) + i) % Items.Count;
            if (!(Items[index].ToString() ?? "").StartsWith(search, StringComparison.CurrentCultureIgnoreCase)) continue;
            if (popup is not null) popup.SetHighlight(index); else SelectedIndex = index;
            return;
        }
    }
    internal void OpenDropDown()
    {
        if (IsOpen || !Enabled || !Visible || Items.Count == 0 || FindForm() is not Form owner) return;
        popupOwner = owner;
        popup = new ChoicePopup(this) { Name = Name + global::AetherBoy.Runtime.Localization.UiText.Get("Choices"), Font = Font };
        owner.Controls.Add(popup);
        PositionPopup();
        popup.BringToFront();
        owner.Deactivate += OwnerChanged; owner.SizeChanged += OwnerChanged; owner.LocationChanged += OwnerChanged;
        Application.AddMessageFilter(this);
        AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
        Invalidate();
    }
    private void PositionPopup()
    {
        if (popup is null || popupOwner is null) return;
        Rectangle anchor = popupOwner.RectangleToClient(RectangleToScreen(ClientRectangle));
        int row = popup.RowHeight;
        int width = Math.Max(Width, Items.Max(item => TextRenderer.MeasureText(item.ToString(), Font).Width) + 40 * DeviceDpi / 96);
        width = Math.Min(width, Math.Max(1, popupOwner.ClientSize.Width - 8));
        int availableBelow = popupOwner.ClientSize.Height - anchor.Bottom - 6;
        int availableAbove = anchor.Top - 6;
        bool above = availableBelow < Math.Min(Items.Count, 4) * row && availableAbove > availableBelow;
        int height = Math.Min(Math.Min(Items.Count, 8) * row + 4, Math.Max(1, above ? availableAbove : availableBelow));
        popup.Bounds = new Rectangle(Math.Clamp(anchor.Left, 4, Math.Max(4, popupOwner.ClientSize.Width - width - 4)),
            above ? anchor.Top - height - 2 : anchor.Bottom + 2, width, height);
        popup.SetHighlight(Math.Max(0, selectedIndex));
    }
    internal void CloseDropDown()
    {
        if (popup is null) return;
        bool restoreFocus = popup.ContainsFocus && Form.ActiveForm == popupOwner;
        Application.RemoveMessageFilter(this);
        if (popupOwner is not null)
        {
            popupOwner.Deactivate -= OwnerChanged; popupOwner.SizeChanged -= OwnerChanged; popupOwner.LocationChanged -= OwnerChanged;
        }
        ChoicePopup old = popup; popup = null; popupOwner = null;
        old.Parent?.Controls.Remove(old); old.Dispose();
        AccessibilityNotifyClients(AccessibleEvents.StateChange, -1); Invalidate();
        if (restoreFocus && !Disposing && !IsDisposed && CanSelect) Focus();
    }
    private void OwnerChanged(object? sender, EventArgs e) => CloseDropDown();
    internal void AcceptHighlight()
    {
        if (popup is null) return;
        int index = popup.Highlight; CloseDropDown(); SelectedIndex = index;
        if (!IsDisposed) Focus();
    }
    internal void MoveHighlight(int delta) { popup?.SetHighlight(popup.Highlight + delta); }
    public bool PreFilterMessage(ref Message m)
    {
        if (popup is null) return false;
        if (m.Msg is 0x201 or 0x204 or 0x207 or 0xA1)
        {
            Point screen = MousePosition;
            if (!popup.RectangleToScreen(popup.ClientRectangle).Contains(screen) && !RectangleToScreen(ClientRectangle).Contains(screen)) CloseDropDown();
        }
        if (m.Msg is 0x100 or 0x104)
        {
            Keys key = (Keys)(int)m.WParam;
            switch (key)
            {
                case Keys.Escape: case Keys.F4: CloseDropDown(); return true;
                case Keys.Enter: case Keys.Space: AcceptHighlight(); return true;
                case Keys.Up: MoveHighlight(-1); return true;
                case Keys.Down: MoveHighlight(1); return true;
                case Keys.Home: popup.SetHighlight(0); return true;
                case Keys.End: popup.SetHighlight(Items.Count - 1); return true;
                case Keys.PageUp: MoveHighlight(-popup.VisibleRows); return true;
                case Keys.PageDown: MoveHighlight(popup.VisibleRows); return true;
                case Keys.Tab: CloseDropDown(); return false;
            }
        }
        if (m.Msg == 0x102 && !char.IsControl((char)(int)m.WParam)) { Search((char)(int)m.WParam); return true; }
        return false;
    }
    protected override void Dispose(bool disposing) { if (disposing) CloseDropDown(); base.Dispose(disposing); }

    internal sealed class ChoiceItems(AetherSelect owner) : Collection<object>
    {
        internal void AddRange(object[] items) { foreach (object item in items) Add(item); }
        protected override void InsertItem(int index, object item)
        {
            ArgumentNullException.ThrowIfNull(item); owner.CloseDropDown();
            base.InsertItem(index, item);
            if (owner.selectedIndex >= index) owner.selectedIndex++;
            owner.Invalidate();
        }
        protected override void SetItem(int index, object item)
        { ArgumentNullException.ThrowIfNull(item); owner.CloseDropDown(); base.SetItem(index, item); owner.Invalidate(); }
        protected override void RemoveItem(int index)
        {
            owner.CloseDropDown(); bool selected = index == owner.selectedIndex;
            base.RemoveItem(index);
            if (selected) owner.SelectedIndex = -1;
            else if (owner.selectedIndex > index) owner.selectedIndex--;
            owner.Invalidate();
        }
        protected override void ClearItems() { owner.CloseDropDown(); base.ClearItems(); owner.SelectedIndex = -1; owner.Invalidate(); }
    }

    private sealed class ChoicePopup : Control
    {
        private readonly AetherSelect select;
        private readonly AetherScrollBar scroll = new() { AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Auswahlliste scrollen"), TabStop = false };
        internal int Highlight { get; private set; }
        internal int RowHeight => Math.Max(Font.Height + 14 * DeviceDpi / 96, 30 * DeviceDpi / 96);
        internal int VisibleRows => Math.Max(1, (Height - 4) / RowHeight);
        internal ChoicePopup(AetherSelect select)
        {
            this.select = select; TabStop = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Controls.Add(scroll); scroll.ValueChanged += (_, _) => Invalidate();
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (scroll is null) return;
            scroll.Bounds = new Rectangle(Math.Max(0, Width - 18 * DeviceDpi / 96), 2, 16 * DeviceDpi / 96, Math.Max(1, Height - 4));
            scroll.Configure(select.Items.Count, VisibleRows);
            scroll.Visible = select.Items.Count > VisibleRows;
        }
        internal void SetHighlight(int index)
        {
            Highlight = Math.Clamp(index, 0, select.Items.Count - 1);
            if (Highlight < scroll.Value) scroll.Value = Highlight;
            if (Highlight >= scroll.Value + VisibleRows) scroll.Value = Highlight - VisibleRows + 1;
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(AetherColors.Surface);
            using var border = new Pen(AetherColors.Cyan);
            e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            for (int row = 0; row < VisibleRows && row + scroll.Value < select.Items.Count; row++)
            {
                int index = row + scroll.Value;
                var bounds = new Rectangle(3, 2 + row * RowHeight, Math.Max(1, Width - (scroll.Visible ? scroll.Width + 6 : 6)), RowHeight);
                if (index == Highlight)
                {
                    using var fill = new SolidBrush(AetherColors.SurfaceRaised); e.Graphics.FillRectangle(fill, bounds);
                    AetherWidgetPaint.Focus(e.Graphics, Rectangle.Inflate(bounds, -1, -1), AetherColors.Cyan);
                }
                TextRenderer.DrawText(e.Graphics, select.Items[index].ToString(), Font, Rectangle.Inflate(bounds, -9, 0),
                    index == Highlight ? AetherColors.Cyan : AetherColors.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int row = (e.Y - 2) / RowHeight;
            if (e.Y >= 2 && e.Y < Height - 2 && row < VisibleRows)
            { Highlight = Math.Clamp(scroll.Value + row, 0, select.Items.Count - 1); Invalidate(); }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && e.Y >= 2 && e.Y < Height - 2 && (e.Y - 2) / RowHeight < VisibleRows)
            { SetHighlight(scroll.Value + (e.Y - 2) / RowHeight); select.AcceptHighlight(); }
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            scroll.Value -= Math.Sign(e.Delta) * Math.Max(1, SystemInformation.MouseWheelScrollLines);
            if (e is HandledMouseEventArgs handled) handled.Handled = true;
        }
    }
    protected override AccessibleObject CreateAccessibilityInstance() => new ChoiceAccessibility(this);
    private sealed class ChoiceAccessibility(AetherSelect select) : ControlAccessibleObject(select)
    {
        public override AccessibleStates State => base.State | (select.IsOpen ? AccessibleStates.Expanded : AccessibleStates.Collapsed);
        public override string? Value { get => select.Text; set => select.Text = value ?? ""; }
        public override string DefaultAction => global::AetherBoy.Runtime.Localization.UiText.Get("Auswahl öffnen");
        public override void DoDefaultAction() => select.OpenDropDown();
        public override int GetChildCount() => select.Items.Count;
        public override AccessibleObject? GetChild(int index) => index < 0 || index >= select.Items.Count ? null : new ChoiceItemAccessibility(select, index);
    }
    private sealed class ChoiceItemAccessibility(AetherSelect select, int index) : AccessibleObject
    {
        public override AccessibleRole Role => AccessibleRole.ListItem;
        public override AccessibleObject Parent => select.AccessibilityObject;
        public override string? Name { get => index < select.Items.Count ? select.Items[index].ToString() : ""; set { } }
        public override AccessibleStates State => AccessibleStates.Selectable | (index == select.SelectedIndex ? AccessibleStates.Selected : AccessibleStates.None);
        public override string DefaultAction => global::AetherBoy.Runtime.Localization.UiText.Get("Auswählen");
        public override void DoDefaultAction() { if (select.Enabled && index < select.Items.Count) { select.CloseDropDown(); select.SelectedIndex = index; } }
    }
}
