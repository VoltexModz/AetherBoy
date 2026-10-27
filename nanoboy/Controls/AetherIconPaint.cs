using System.Drawing;
using System.Drawing.Drawing2D;
using nanoboy.Core;

namespace nanoboy.Controls;

internal static class AetherIconPaint
{
    internal static void Draw(Graphics graphics, UiIcon icon, Rectangle bounds, Color color)
    {
        using var pen = new Pen(color, System.Math.Max(1.3f, bounds.Width / 12f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var lines = UiIconGeometry.Lines(icon);
        for (int i = 0; i < lines.Length; i += 4)
            graphics.DrawLine(pen, bounds.X + lines[i] * bounds.Width / 24, bounds.Y + lines[i + 1] * bounds.Height / 24,
                bounds.X + lines[i + 2] * bounds.Width / 24, bounds.Y + lines[i + 3] * bounds.Height / 24);
    }

    internal static void Theme(Graphics graphics, Rectangle box, UiThemePreset preset)
    {
        var palette = new UiThemePalette(preset.Primary, preset.Secondary, preset.Background);
        static Color ColorOf(UiRgb color) => Color.FromArgb(color.R, color.G, color.B);
        using var bg = new SolidBrush(ColorOf(palette.Background));
        using var surface = new SolidBrush(ColorOf(palette.Surface));
        using var primary = new SolidBrush(ColorOf(palette.Primary));
        using var secondary = new SolidBrush(ColorOf(palette.Secondary));
        graphics.FillRectangle(bg, box);
        graphics.FillRectangle(surface, box.X + box.Width / 5, box.Y + 8, box.Width * 3 / 4, System.Math.Max(1, box.Height - 16));
        graphics.FillRectangle(primary, box.X + 4, box.Y + 8, box.Width / 9, 3);
        graphics.FillRectangle(primary, box.X + 4, box.Y + 16, box.Width / 9, 3);
        graphics.FillRectangle(secondary, box.Right - box.Width / 4, box.Bottom - 6, box.Width / 5, 3);
    }
}
