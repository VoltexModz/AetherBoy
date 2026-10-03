using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace nanoboy.Controls;

internal static class AetherWidgetPaint
{
    internal static GraphicsPath Outline(Rectangle bounds, int cut = 6)
    {
        var path = new GraphicsPath();
        cut = System.Math.Clamp(cut, 0, System.Math.Max(0, System.Math.Min(bounds.Width, bounds.Height) / 2));
        path.AddPolygon(new Point[] { new(bounds.Left, bounds.Top), new(bounds.Right - cut, bounds.Top),
            new(bounds.Right, bounds.Top + cut), new(bounds.Right, bounds.Bottom),
            new(bounds.Left + cut, bounds.Bottom), new(bounds.Left, bounds.Bottom - cut) });
        return path;
    }

    internal static void Focus(Graphics graphics, Rectangle bounds, Color color)
    {
        if (bounds.Width < 4 || bounds.Height < 4) return;
        using var path = Outline(bounds, 4);
        using var pen = new Pen(color, 1.5f);
        graphics.DrawPath(pen, path);
    }
}

internal sealed class AetherColorSwatch : Panel
{
    public AetherColorSwatch() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 2 || Height < 2) return;
        using var pen = new Pen(AetherColors.Muted);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
