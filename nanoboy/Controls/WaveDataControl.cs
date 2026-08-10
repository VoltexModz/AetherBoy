using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace nanoboy.Controls
{
    public class WaveDataControl : Control
    {

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public byte[] WaveForm {
            get {
                return waveform;
            }
            set {
                waveform = value ?? new byte[32];
                Invalidate();
            }
        }
        private byte[] waveform = new byte[32];

        public WaveDataControl()
        {
            Width = 32 * 24;
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
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(AetherColors.Chrome);

            using (var grid = new Pen(Color.FromArgb(105, AetherColors.Hairline)))
            {
                for (int index = 1; index < 4; index++)
                {
                    int y = index * Height / 4;
                    graphics.DrawLine(grid, 0, y, Width, y);
                }

                for (int index = 1; index < 8; index++)
                {
                    int x = index * Width / 8;
                    graphics.DrawLine(grid, x, 0, x, Height);
                }
            }

            int sampleCount = System.Math.Min(32, waveform.Length);
            if (sampleCount > 0)
            {
                var points = new PointF[sampleCount];
                float usableHeight = System.Math.Max(1, Height - 18);
                for (int index = 0; index < sampleCount; index++)
                {
                    float x = 8 + (index * (Width - 16f) / System.Math.Max(1, sampleCount - 1));
                    float normalized = System.Math.Clamp(waveform[index] / 15f, 0f, 1f);
                    float y = 9 + ((1f - normalized) * usableHeight);
                    points[index] = new PointF(x, y);
                }

                using var glowBrush = new LinearGradientBrush(
                    ClientRectangle,
                    Color.FromArgb(120, AetherColors.Violet),
                    Color.FromArgb(120, AetherColors.Cyan),
                    0f);
                using var glow = new Pen(glowBrush, 5f);
                graphics.DrawLines(glow, points);

                using var signalBrush = new LinearGradientBrush(
                    ClientRectangle,
                    AetherColors.Violet,
                    AetherColors.Cyan,
                    0f);
                using var signal = new Pen(signalBrush, 2f);
                graphics.DrawLines(signal, points);
            }

            using var border = new Pen(AetherColors.Hairline);
            graphics.DrawRectangle(border, 0, 0, System.Math.Max(1, Width - 1), System.Math.Max(1, Height - 1));
        }

    }
}
