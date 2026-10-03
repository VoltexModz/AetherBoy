using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace nanoboy.Controls;

/// <summary>A painted scroll track, not a Win32 scrollbar. Maximum is the last content offset.</summary>
internal sealed class AetherScrollBar : Control
{
    private int value, maximum, viewport = 1, dragStart, dragValue;
    private bool dragging, hovered;
    internal event EventHandler? ValueChanged;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Orientation Direction { get; init; } = Orientation.Vertical;
    internal int Maximum => maximum;
    internal int ViewportSize => viewport;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => value;
        set
        {
            int next = Math.Clamp(value, 0, maximum);
            if (this.value == next) return;
            this.value = next; Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    internal void Configure(int contentLength, int viewportLength)
    {
        viewport = Math.Max(1, viewportLength);
        maximum = Math.Max(0, contentLength - viewport);
        Value = value;
        Invalidate();
    }
    public AetherScrollBar()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = true;
        Size = new Size(16, 100);
        AccessibleRole = AccessibleRole.ScrollBar;
    }
    internal Rectangle Thumb
    {
        get
        {
            int length = Math.Max(1, (Direction == Orientation.Vertical ? Height : Width) - 4);
            int thumb = Math.Clamp((int)((long)length * viewport / Math.Max(1L, (long)maximum + viewport)),
                Math.Min(length, 24 * DeviceDpi / 96), length);
            int start = maximum == 0 ? 0 : (int)((long)(length - thumb) * value / maximum);
            return Direction == Orientation.Vertical ? new(3, 2 + start, Math.Max(1, Width - 6), thumb)
                : new(2 + start, 3, thumb, Math.Max(1, Height - 6));
        }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(AetherColors.Chrome);
        Rectangle thumb = Thumb;
        using var path = AetherWidgetPaint.Outline(thumb, 3);
        using var fill = new SolidBrush(Enabled && (hovered || dragging || Focused) ? AetherColors.Cyan : AetherColors.Muted);
        e.Graphics.FillPath(fill, path);
        if (Focused) AetherWidgetPaint.Focus(e.Graphics, Rectangle.Inflate(ClientRectangle, -1, -1), AetherColors.Cyan);
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus(); int at = Direction == Orientation.Vertical ? e.Y : e.X;
        if (Thumb.Contains(e.Location)) { dragging = true; dragStart = at; dragValue = value; Capture = true; }
        else Value += at < (Direction == Orientation.Vertical ? Thumb.Top : Thumb.Left) ? -viewport : viewport;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!dragging) return;
        int length = Direction == Orientation.Vertical ? Height - 4 - Thumb.Height : Width - 4 - Thumb.Width;
        if (length > 0) Value = (int)Math.Clamp(dragValue + (long)((Direction == Orientation.Vertical ? e.Y : e.X) - dragStart) * maximum / length, 0, maximum);
    }
    protected override void OnMouseUp(MouseEventArgs e) { dragging = false; Capture = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnMouseCaptureChanged(EventArgs e) { dragging = false; base.OnMouseCaptureChanged(e); }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Up: case Keys.Left: Value -= Math.Max(16, Font.Height); break;
            case Keys.Down: case Keys.Right: Value += Math.Max(16, Font.Height); break;
            case Keys.PageUp: Value -= viewport; break;
            case Keys.PageDown: Value += viewport; break;
            case Keys.Home: Value = 0; break;
            case Keys.End: Value = maximum; break;
            default: base.OnKeyDown(e); return;
        }
        e.Handled = e.SuppressKeyPress = true;
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        int lines = SystemInformation.MouseWheelScrollLines;
        Value -= e.Delta * (lines < 0 ? viewport : Math.Max(0, lines) * Math.Max(16, Font.Height)) / 120;
        if (e is HandledMouseEventArgs handled) handled.Handled = true;
    }
    protected override AccessibleObject CreateAccessibilityInstance() => new ScrollAccessibility(this);
    private sealed class ScrollAccessibility(AetherScrollBar bar) : ControlAccessibleObject(bar)
    {
        public override string? Value { get => bar.Value.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, out int next)) bar.Value = next; } }
    }
}
