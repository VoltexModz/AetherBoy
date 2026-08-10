using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace nanoboy.Controls
{
    public class LevelDisplayControl : Control
    {

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Level {
            get => level;
            set {
                int nextLevel = System.Math.Clamp(value, 0, Height);
                if (level == nextLevel)
                {
                    return;
                }

                level = nextLevel;
                Invalidate();
            }
        }
        private int level;

        public LevelDisplayControl()
        {
            Width = 24;
            Height = 120;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.Clear(AetherColors.Chrome);

            const int segmentCount = 12;
            const int gap = 3;
            int innerHeight = System.Math.Max(1, Height - 8);
            int segmentHeight = System.Math.Max(2, (innerHeight - ((segmentCount - 1) * gap)) / segmentCount);
            int activeSegments = Height == 0
                ? 0
                : (int)System.Math.Ceiling(level / (double)Height * segmentCount);

            for (int index = 0; index < segmentCount; index++)
            {
                int y = Height - 4 - segmentHeight - (index * (segmentHeight + gap));
                if (y < 3)
                {
                    break;
                }

                Rectangle segment = new Rectangle(4, y, System.Math.Max(1, Width - 8), segmentHeight);
                Color color = index < activeSegments
                    ? Blend(AetherColors.Violet, AetherColors.Cyan, index / (float)(segmentCount - 1))
                    : AetherColors.SurfaceRaised;
                using var fill = new SolidBrush(color);
                graphics.FillRectangle(fill, segment);
            }

            using var border = new Pen(AetherColors.Hairline);
            graphics.DrawRectangle(border, 0, 0, System.Math.Max(1, Width - 1), System.Math.Max(1, Height - 1));
        }

        private static Color Blend(Color start, Color end, float amount)
        {
            return Color.FromArgb(
                (int)(start.R + ((end.R - start.R) * amount)),
                (int)(start.G + ((end.G - start.G) * amount)),
                (int)(start.B + ((end.B - start.B) * amount)));
        }

    }
}
