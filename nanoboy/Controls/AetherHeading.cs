using System.Drawing;
using System.Windows.Forms;

namespace nanoboy.Controls;

/// <summary>A single-line heading. The full title remains available to accessibility.</summary>
internal sealed class AetherHeading : Label
{
    internal AetherHeading()
    {
        AutoSize = false;
        AutoEllipsis = false;
        UseMnemonic = false;
        Tag = "value";
        Font = AetherTypography.Create(21, FontStyle.Bold);
        ForeColor = AetherColors.Text;
        BackColor = AetherColors.Void;
        Height = 48;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var bounds = new Rectangle(Padding.Left, Padding.Top,
            System.Math.Max(1, Width - Padding.Horizontal), System.Math.Max(1, Height - Padding.Vertical));
        TextRenderer.DrawText(e.Graphics, Text, Font, bounds, ForeColor,
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
    }
}
