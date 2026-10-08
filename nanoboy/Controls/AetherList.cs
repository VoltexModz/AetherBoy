using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace nanoboy.Controls;

internal sealed class AetherListItem
{
    internal AetherList? Owner;
    private bool selected;
    private bool isChecked;
    internal List<Cell> SubItems { get; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal string Text { get => SubItems[0].Text; set { SubItems[0].Text = value; Owner?.Invalidate(); } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal object? Tag { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int ImageIndex { get; set; } = -1;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal string ToolTipText { get; set; } = "";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool Selected { get => selected; set { if (value == selected) return; selected = value; Owner?.SelectionChanged(this); } }
    internal bool Checked { get => isChecked; set { if (isChecked == value) return; isChecked = value; Owner?.CheckStateChanged(this); } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool Focused { get => Selected; set { if (value) Selected = true; } }
    internal Rectangle Bounds => Owner?.ItemBounds(Owner.Items.IndexOf(this)) ?? Rectangle.Empty;
    internal void EnsureVisible() { if (Owner is not null) Owner.EnsureVisible(Owner.Items.IndexOf(this)); }
    internal AetherListItem(string[] cells) => SubItems = (cells.Length == 0 ? new[] { "" } : cells).Select(text => new Cell(text)).ToList();
    internal AetherListItem(string text) : this([text]) { }
    internal sealed class Cell(string text) { internal string Text { get; set; } = text; }
}

/// <summary>Painted rows, headers and tiles with no native ListView or header window.</summary>
internal sealed class AetherList : Control
{
    private readonly AetherScrollBar vertical = new() { TabStop = false, AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Liste vertikal scrollen") };
    private readonly AetherScrollBar horizontal = new() { Direction = Orientation.Horizontal, TabStop = false, AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Liste horizontal scrollen") };
    private readonly System.Windows.Forms.Timer hintTimer = new() { Interval = 650 };
    private AetherHint? hint;
    private int hovered = -1, updating, resizingColumn = -1, resizeStart, resizeWidth;
    private bool selecting;
    private View view = View.Details;
    private string search = ""; private long searchedAt;
    internal ItemCollection Items { get; }
    internal ColumnCollection Columns { get; }
    internal List<AetherListItem> SelectedItems => Items.Where(item => item.Selected).ToList();
    internal IndexSelection SelectedIndices => new(this);
    internal event EventHandler? SelectedIndexChanged;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool CheckBoxes { get; set; }
    internal event Action<AetherListItem>? ItemCheckRequested;
    internal void RequestItemCheck(AetherListItem item)
    {
        if (Enabled && CheckBoxes && item.Owner == this) ItemCheckRequested?.Invoke(item);
    }
    internal void CheckStateChanged(AetherListItem item)
    {
        Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.StateChange, Items.IndexOf(item));
    }
    internal Rectangle CheckBounds(int index)
    {
        Rectangle row = ItemBounds(index);
        int size = Math.Min(RowHeight - 6, Math.Max(18 * DeviceDpi / 96, Font.Height));
        return new Rectangle(row.Left + 8, row.Top + (RowHeight - size) / 2, size, size);
    }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal View View { get => view; set { if (value is not (View.Details or View.LargeIcon)) throw new ArgumentOutOfRangeException(nameof(value)); view = value; Reflow(); } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool FullRowSelect { get; set; } = true;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool MultiSelect { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool HideSelection { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool UseCompatibleStateImageBehavior { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool ShowItemToolTips { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal ImageList? LargeImageList { get; set; }
    private int RowHeight => Math.Max(28 * DeviceDpi / 96, Font.Height + 12);
    private int HeaderHeight => View == View.Details ? RowHeight : 0;
    private int TileWidth => Math.Max(180 * DeviceDpi / 96, (LargeImageList?.ImageSize.Width ?? 160) + 20);
    private int TileHeight => (LargeImageList?.ImageSize.Height ?? 112) + Font.Height * 3 + 16;
    internal int TileColumns => Math.Max(1, ContentArea.Width / TileWidth);
    private Rectangle ContentArea => new(2, HeaderHeight + 2, Math.Max(1, Width - 4 - (vertical.Visible ? vertical.Width : 0)), Math.Max(1, Height - HeaderHeight - 4 - (horizontal.Visible ? horizontal.Height : 0)));
    public AetherList()
    {
        Items = new ItemCollection(this); Columns = new ColumnCollection(this);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardDoubleClick, true);
        TabStop = true; Size = new Size(400, 240); AccessibleRole = AccessibleRole.List;
        Controls.AddRange([vertical, horizontal]);
        vertical.ValueChanged += (_, _) => { CloseHint(); Invalidate(); };
        horizontal.ValueChanged += (_, _) => { CloseHint(); Invalidate(); };
        hintTimer.Tick += (_, _) => { hintTimer.Stop(); if (ShowItemToolTips && hovered >= 0 && hovered < Items.Count && FindForm() is not null) hint = new AetherHint(this, Items[hovered].ToolTipText.Length > 0 ? Items[hovered].ToolTipText : string.Join(" · ", Items[hovered].SubItems.Select(cell => cell.Text))); };
    }
    internal void BeginUpdate() => updating++;
    internal void EndUpdate() { updating = Math.Max(0, updating - 1); Reflow(); }
    internal void SelectionChanged(AetherListItem item)
    {
        if (selecting) return;
        selecting = true;
        try { if (item.Selected && !MultiSelect) foreach (var other in Items) if (other != item) other.Selected = false; }
        finally { selecting = false; }
        Invalidate(); AccessibilityNotifyClients(AccessibleEvents.Selection, Items.IndexOf(item)); SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
    }
    internal Rectangle ItemBounds(int index) => View == View.Details
        ? new Rectangle(2 - horizontal.Value, HeaderHeight + 2 + index * RowHeight - vertical.Value, Math.Max(ContentArea.Width, Columns.Sum(column => column.Width)), RowHeight)
        : new Rectangle(2 + index % TileColumns * TileWidth, 2 + index / TileColumns * TileHeight - vertical.Value, TileWidth, TileHeight);
    internal void EnsureVisible(int index)
    {
        if (index < 0 || index >= Items.Count) return;
        Rectangle bounds = ItemBounds(index), area = ContentArea;
        if (bounds.Top < area.Top) vertical.Value += bounds.Top - area.Top;
        else if (bounds.Bottom > area.Bottom) vertical.Value += bounds.Bottom - area.Bottom;
    }
    private void Reflow()
    {
        if (updating != 0 || Items is null) return;
        int track = Math.Max(14, 16 * DeviceDpi / 96);
        vertical.Bounds = new Rectangle(Width - track - 1, HeaderHeight + 1, track, Math.Max(1, Height - HeaderHeight - track - 2));
        horizontal.Bounds = new Rectangle(1, Height - track - 1, Math.Max(1, Width - track - 2), track);
        horizontal.Visible = View == View.Details && Columns.Sum(column => column.Width) > Width - track - 4;
        int height = View == View.Details ? Items.Count * RowHeight : ((Items.Count + Math.Max(1, (Width - track - 4) / TileWidth) - 1) / Math.Max(1, (Width - track - 4) / TileWidth)) * TileHeight;
        vertical.Visible = height > Height - HeaderHeight - 4 - (horizontal.Visible ? track : 0);
        height = View == View.Details ? Items.Count * RowHeight : ((Items.Count + TileColumns - 1) / TileColumns) * TileHeight;
        vertical.Configure(height, ContentArea.Height);
        horizontal.Configure(Columns.Sum(column => column.Width), ContentArea.Width);
        Invalidate();
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); Reflow(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Reflow(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(AetherColors.SurfaceRaised);
        var state = e.Graphics.Save(); e.Graphics.SetClip(ContentArea);
        int first = View == View.Details ? vertical.Value / RowHeight : vertical.Value / TileHeight * TileColumns;
        int count = View == View.Details ? ContentArea.Height / RowHeight + 2 : (ContentArea.Height / TileHeight + 2) * TileColumns;
        for (int i = first; i < Math.Min(Items.Count, first + count); i++)
        {
            var item = Items[i]; Rectangle bounds = ItemBounds(i);
            using var fill = new SolidBrush(item.Selected ? AetherColors.Surface : AetherColors.SurfaceRaised); e.Graphics.FillRectangle(fill, bounds);
            Color color = !Enabled ? AetherColors.Muted : item.Selected ? AetherColors.Cyan : AetherColors.Text;
            if (View == View.Details)
            {
                int x = bounds.Left;
                for (int c = 0; c < Columns.Count; c++)
                {
                    if (c == 0 && CheckBoxes)
                        AetherCheckBox.DrawIndicator(e.Graphics, CheckBounds(i), item.Checked ? CheckState.Checked : CheckState.Unchecked,
                            Enabled, item.Selected && ContainsFocus || i == hovered, DeviceDpi);
                    else DrawText(e.Graphics, c < item.SubItems.Count ? item.SubItems[c].Text : "", new Rectangle(x + 8, bounds.Top, Math.Max(1, Columns[c].Width - 16), bounds.Height), color);
                    x += Columns[c].Width;
                }
            }
            else
            {
                if (LargeImageList is { } images && item.ImageIndex >= 0 && item.ImageIndex < images.Images.Count)
                    images.Draw(e.Graphics, bounds.Left + (bounds.Width - images.ImageSize.Width) / 2, bounds.Top + 6, item.ImageIndex);
                TextRenderer.DrawText(e.Graphics, item.Text, Font, new Rectangle(bounds.Left + 6, bounds.Top + (LargeImageList?.ImageSize.Height ?? 112) + 8, bounds.Width - 12, Font.Height * 3), color, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
            }
            if (item.Selected) AetherWidgetPaint.Focus(e.Graphics, Rectangle.Inflate(Rectangle.Intersect(bounds, ContentArea), -2, -2), ContainsFocus ? AetherColors.Cyan : AetherColors.Hairline);
        }
        e.Graphics.Restore(state);
        if (View == View.Details)
        {
            using var fill = new SolidBrush(AetherColors.Chrome); e.Graphics.FillRectangle(fill, new Rectangle(1, 1, Width - 2, HeaderHeight));
            int x = 2 - horizontal.Value;
            foreach (var column in Columns) { DrawText(e.Graphics, column.Text, new Rectangle(x + 8, 1, Math.Max(1, column.Width - 16), HeaderHeight), AetherColors.Muted); x += column.Width; }
        }
        using var edge = new Pen(ContainsFocus ? AetherColors.Cyan : AetherColors.Hairline);
        if (Width > 2 && Height > 2) { using var path = AetherWidgetPaint.Outline(new Rectangle(0, 0, Width - 1, Height - 1)); e.Graphics.DrawPath(edge, path); }
    }
    private void DrawText(Graphics graphics, string text, Rectangle bounds, Color color) => TextRenderer.DrawText(graphics, text, Font, bounds, color, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    private int Hit(Point at)
    {
        if (!ContentArea.Contains(at)) return -1;
        int index = View == View.Details ? (at.Y - ContentArea.Top + vertical.Value) / RowHeight : (at.Y - 2 + vertical.Value) / TileHeight * TileColumns + (at.X - 2) / TileWidth;
        return index >= 0 && index < Items.Count && ItemBounds(index).Contains(at) ? index : -1;
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus(); CloseHint();
        if (View == View.Details && e.Y < HeaderHeight && e.Button == MouseButtons.Left)
        {
            int x = 2 - horizontal.Value;
            for (int i = 0; i < Columns.Count; i++) { x += Columns[i].Width; if (Math.Abs(e.X - x) < 6) { resizingColumn = i; resizeStart = e.X; resizeWidth = Columns[i].Width; Capture = true; return; } }
        }
        int index = Hit(e.Location);
        if (index >= 0)
        {
            var item = Items[index]; item.Selected = true;
            if (e.Button == MouseButtons.Left && e.Clicks == 1 && CheckBoxes && View == View.Details && CheckBounds(index).Contains(e.Location))
                RequestItemCheck(item);
        }
        base.OnMouseDown(e);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (resizingColumn >= 0) { Columns[resizingColumn].Width = Math.Max(40, resizeWidth + e.X - resizeStart); Reflow(); return; }
        int index = Hit(e.Location); if (index != hovered) { CloseHint(); hovered = index; if (index >= 0 && ShowItemToolTips) hintTimer.Start(); }
        base.OnMouseMove(e);
    }
    protected override void OnMouseUp(MouseEventArgs e) { resizingColumn = -1; Capture = false; base.OnMouseUp(e); }
    protected override void OnMouseCaptureChanged(EventArgs e) { resizingColumn = -1; base.OnMouseCaptureChanged(e); }
    protected override void OnMouseLeave(EventArgs e) { hintTimer.Stop(); hovered = -1; base.OnMouseLeave(e); }
    protected override void OnMouseWheel(MouseEventArgs e) { vertical.Value -= e.Delta / 120 * RowHeight * Math.Max(1, SystemInformation.MouseWheelScrollLines); if (e is HandledMouseEventArgs handled) handled.Handled = true; }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space && CheckBoxes && SelectedItems.FirstOrDefault() is { } checkedItem)
        { RequestItemCheck(checkedItem); e.Handled = e.SuppressKeyPress = true; return; }
        if (e.KeyCode == Keys.Enter) { OnDoubleClick(EventArgs.Empty); e.Handled = e.SuppressKeyPress = true; return; }
        if (e.KeyCode == Keys.F10 && e.Shift) { if (SelectedItems.FirstOrDefault() is { } item) hint = new AetherHint(this, string.Join("\r\n", item.SubItems.Select(cell => cell.Text))); e.Handled = true; return; }
        int current = SelectedIndices.Count > 0 ? SelectedIndices[0] : -1, step = View == View.LargeIcon ? TileColumns : 1;
        int next = e.KeyCode switch { Keys.Up => current - step, Keys.Down => current + step, Keys.Left => current - 1, Keys.Right => current + 1, Keys.Home => 0, Keys.End => Items.Count - 1, Keys.PageUp => current - Math.Max(1, ContentArea.Height / RowHeight), Keys.PageDown => current + Math.Max(1, ContentArea.Height / RowHeight), _ => int.MinValue };
        if (next != int.MinValue && Items.Count > 0) { next = Math.Clamp(next, 0, Items.Count - 1); Items[next].Selected = true; EnsureVisible(next); e.Handled = e.SuppressKeyPress = true; }
        base.OnKeyDown(e);
    }
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && Items.Count > 0)
        {
            if (Environment.TickCount64 - searchedAt > 900) search = ""; searchedAt = Environment.TickCount64;
            search = search.Length == 1 && char.ToUpperInvariant(search[0]) == char.ToUpperInvariant(e.KeyChar) ? search : search + e.KeyChar;
            int current = SelectedIndices.Count > 0 ? SelectedIndices[0] : -1;
            for (int i = 1; i <= Items.Count; i++) { int index = (current + i) % Items.Count; if (Items[index].Text.StartsWith(search, StringComparison.CurrentCultureIgnoreCase)) { Items[index].Selected = true; EnsureVisible(index); break; } }
            e.Handled = true;
        }
        base.OnKeyPress(e);
    }
    private void CloseHint() { hintTimer.Stop(); hint?.Dispose(); hint = null; }
    protected override void Dispose(bool disposing) { if (disposing) { CloseHint(); hintTimer.Dispose(); } base.Dispose(disposing); }
    protected override AccessibleObject CreateAccessibilityInstance() => new ListAccessibility(this);
    private sealed class ListAccessibility(AetherList list) : ControlAccessibleObject(list)
    {
        public override int GetChildCount() => list.Items.Count;
        public override AccessibleObject? GetChild(int index) => index >= 0 && index < list.Items.Count ? new RowAccessibility(list, list.Items[index]) : null;
        public override AccessibleObject? GetSelected() => list.SelectedItems.FirstOrDefault() is { } item ? new RowAccessibility(list, item) : null;
    }
    private sealed class RowAccessibility(AetherList list, AetherListItem item) : AccessibleObject
    {
        public override string? Name { get => string.Join(" · ", item.SubItems.Select(cell => cell.Text)); set { } }
        public override AccessibleRole Role => AccessibleRole.ListItem;
        public override Rectangle Bounds => list.RectangleToScreen(item.Bounds);
        public override AccessibleStates State => AccessibleStates.Selectable | (item.Selected ? AccessibleStates.Selected : 0) |
            (list.CheckBoxes && item.Checked ? AccessibleStates.Checked : 0) | (!list.Enabled ? AccessibleStates.Unavailable : 0);
        public override string? DefaultAction => list.CheckBoxes
            ? global::AetherBoy.Runtime.Localization.UiText.Get(item.Checked ? "Häkchen entfernen" : "Häkchen setzen") : base.DefaultAction;
        public override void Select(AccessibleSelection flags) { item.Selected = true; item.EnsureVisible(); list.Focus(); }
        public override void DoDefaultAction()
        { Select(AccessibleSelection.TakeSelection); if (list.CheckBoxes) list.RequestItemCheck(item); else list.OnDoubleClick(EventArgs.Empty); }
    }
    internal sealed class ItemCollection(AetherList owner) : Collection<AetherListItem>
    {
        protected override void InsertItem(int index, AetherListItem item) { base.InsertItem(index, item); item.Owner = owner; owner.Reflow(); }
        protected override void ClearItems() { foreach (var item in this) item.Owner = null; base.ClearItems(); owner.CloseHint(); owner.Reflow(); owner.SelectedIndexChanged?.Invoke(owner, EventArgs.Empty); }
        protected override void RemoveItem(int index) { this[index].Owner = null; base.RemoveItem(index); owner.Reflow(); owner.SelectedIndexChanged?.Invoke(owner, EventArgs.Empty); }
    }
    internal sealed class ColumnCollection(AetherList owner) : Collection<ColumnHeader>
    {
        internal void Add(string text, int width) => Add(new ColumnHeader { Text = text, Width = width });
        internal void AddRange(ColumnHeader[] columns) { foreach (var column in columns) Add(column); }
        protected override void InsertItem(int index, ColumnHeader item) { base.InsertItem(index, item); owner.Reflow(); }
    }
    internal sealed class IndexSelection(AetherList list)
    {
        internal int Count => list.SelectedItems.Count;
        internal int this[int index] => list.Items.IndexOf(list.SelectedItems[index]);
        internal void Clear() { foreach (var item in list.SelectedItems) item.Selected = false; }
    }
}
