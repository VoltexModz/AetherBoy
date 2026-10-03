using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace nanoboy.Controls;

/// <summary>Native check state, keyboard and accessibility; all visual states are Aether-owned.</summary>
internal sealed class AetherCheckBox : CheckBox
{
    private bool hovered;
    public AetherCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        ForeColor = AetherColors.Text;
        Cursor = Cursors.Hand;
        AutoSize = false;
        Size = new Size(160, 30);
        UseVisualStyleBackColor = false;
    }

    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnCheckStateChanged(EventArgs e) { base.OnCheckStateChanged(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int box = Math.Max(18 * DeviceDpi / 96, Font.Height);
        Size text = TextRenderer.MeasureText(Text, Font);
        return new Size(box + text.Width + 18 * DeviceDpi / 96, Math.Max(box + 8, text.Height + 8));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 4 || Height < 4) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color ink = Enabled ? AetherColors.Text : AetherColors.Muted;
        int size = Math.Min(Height - 4, Math.Max(18 * DeviceDpi / 96, Font.Height));
        bool rtl = RightToLeft == RightToLeft.Yes;
        var box = new Rectangle(rtl ? Width - size - 2 : 2, (Height - size) / 2, size, size);
        using var path = AetherWidgetPaint.Outline(box, 4 * DeviceDpi / 96);
        using var fill = new SolidBrush(AetherColors.SurfaceRaised);
        using var border = new Pen(Enabled && (Focused || hovered) ? AetherColors.Cyan : AetherColors.Muted);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
        if (CheckState != CheckState.Unchecked)
        {
            using var mark = new Pen(Enabled ? AetherColors.Cyan : AetherColors.Muted, Math.Max(2, size / 9f));
            if (CheckState == CheckState.Indeterminate)
                e.Graphics.DrawLine(mark, box.Left + size / 4, box.Top + size / 2, box.Right - size / 4, box.Top + size / 2);
            else
                e.Graphics.DrawLines(mark, new PointF[] { new(box.Left + size * .23f, box.Top + size * .5f),
                    new(box.Left + size * .43f, box.Top + size * .72f), new(box.Left + size * .8f, box.Top + size * .28f) });
        }
        var text = new Rectangle(rtl ? 4 : box.Right + 9, 2, Math.Max(1, Width - size - 17), Height - 4);
        TextRenderer.DrawText(e.Graphics, Text, Font, text, ink,
            TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak |
            (rtl ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : TextFormatFlags.Left));
        if (Focused) AetherWidgetPaint.Focus(e.Graphics, Rectangle.Inflate(ClientRectangle, -1, -1), AetherColors.Cyan);
    }
}
