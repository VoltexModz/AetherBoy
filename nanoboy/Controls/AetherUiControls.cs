using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace nanoboy.Controls
{
    internal static class AetherColors
    {
        public static readonly Color Void = Color.FromArgb(5, 7, 18);
        public static readonly Color Chrome = Color.FromArgb(8, 11, 24);
        public static readonly Color Surface = Color.FromArgb(12, 16, 31);
        public static readonly Color SurfaceRaised = Color.FromArgb(17, 22, 41);
        public static readonly Color Hairline = Color.FromArgb(47, 55, 83);
        public static readonly Color Text = Color.FromArgb(241, 244, 255);
        public static readonly Color Muted = Color.FromArgb(139, 148, 177);
        public static readonly Color Violet = Color.FromArgb(139, 56, 255);
        public static readonly Color Pulse = Color.FromArgb(169, 66, 245);
        public static readonly Color Cyan = Color.FromArgb(41, 226, 237);
        public static readonly Color Success = Color.FromArgb(84, 237, 176);
        public static readonly Color Danger = Color.FromArgb(255, 92, 132);
    }

    internal enum AetherButtonKind
    {
        Primary,
        Secondary,
        Ghost,
        Danger
    }

    internal sealed class AetherButton : Button
    {
        private bool hovered;
        private bool pressed;
        private AetherButtonKind kind;
        private bool selected;

        public AetherButton()
        {
            AutoSize = false;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
            ForeColor = AetherColors.Text;
            Size = new Size(112, 38);
            TabStop = true;
            UseVisualStyleBackColor = false;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
        }

        [DefaultValue(AetherButtonKind.Primary)]
        public AetherButtonKind Kind
        {
            get => kind;
            set
            {
                if (kind == value)
                {
                    return;
                }

                kind = value;
                Invalidate();
            }
        }

        [DefaultValue(false)]
        public bool Selected
        {
            get => selected;
            set
            {
                if (selected == value)
                {
                    return;
                }

                selected = value;
                Invalidate();
            }
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = false;
            pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            if (mevent.Button == MouseButtons.Left)
            {
                pressed = true;
                Invalidate();
            }

            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics graphics = pevent.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Parent?.BackColor ?? AetherColors.Void);

            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using GraphicsPath path = CreateChamferedPath(bounds, Math.Min(8, Height / 3));

            Color foreground = Enabled ? AetherColors.Text : Color.FromArgb(88, AetherColors.Muted);
            Color border = selected ? AetherColors.Cyan : AetherColors.Hairline;
            Color surface = AetherColors.SurfaceRaised;

            if (kind == AetherButtonKind.Danger)
            {
                border = AetherColors.Danger;
            }
            else if (kind == AetherButtonKind.Ghost)
            {
                border = Color.FromArgb(hovered ? 76 : 32, AetherColors.Muted);
                surface = hovered ? AetherColors.SurfaceRaised : AetherColors.Chrome;
            }

            if (kind == AetherButtonKind.Primary)
            {
                Color left = pressed
                    ? Color.FromArgb(112, 42, 213)
                    : hovered ? Color.FromArgb(157, 70, 255) : AetherColors.Violet;
                Color right = pressed
                    ? Color.FromArgb(29, 177, 191)
                    : hovered ? Color.FromArgb(67, 239, 247) : AetherColors.Cyan;
                using var fill = new LinearGradientBrush(bounds, left, right, 0f);
                graphics.FillPath(fill, path);
                foreground = AetherColors.Void;
                border = Color.FromArgb(180, AetherColors.Cyan);
            }
            else
            {
                if (pressed)
                {
                    surface = Color.FromArgb(24, 30, 54);
                }
                else if (hovered || selected)
                {
                    surface = Color.FromArgb(22, 28, 51);
                }

                using var fill = new SolidBrush(surface);
                graphics.FillPath(fill, path);
            }

            using (var outline = new Pen(border, selected ? 1.6f : 1f))
            {
                graphics.DrawPath(outline, path);
            }

            if (kind != AetherButtonKind.Primary && (hovered || selected))
            {
                using var signal = new Pen(selected ? AetherColors.Cyan : AetherColors.Violet, 2f);
                graphics.DrawLine(signal, 4, 4, 4, Height - 5);
            }

            TextRenderer.DrawText(
                graphics,
                Text,
                Font,
                bounds,
                foreground,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPadding);

            if (Focused && ShowFocusCues)
            {
                Rectangle focusBounds = Rectangle.Inflate(bounds, -5, -5);
                ControlPaint.DrawFocusRectangle(graphics, focusBounds, foreground, Color.Transparent);
            }
        }

        private static GraphicsPath CreateChamferedPath(Rectangle bounds, int cut)
        {
            var path = new GraphicsPath();
            path.AddPolygon(
                new[]
                {
                    new Point(bounds.Left, bounds.Top),
                    new Point(bounds.Right - cut, bounds.Top),
                    new Point(bounds.Right, bounds.Top + cut),
                    new Point(bounds.Right, bounds.Bottom),
                    new Point(bounds.Left + cut, bounds.Bottom),
                    new Point(bounds.Left, bounds.Bottom - cut)
                });
            path.CloseFigure();
            return path;
        }
    }

    internal class AetherSurfacePanel : Panel
    {
        public AetherSurfacePanel()
        {
            BackColor = AetherColors.Surface;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        [DefaultValue(false)]
        public bool AccentEdge { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Rectangle border = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using var borderPen = new Pen(AetherColors.Hairline);
            e.Graphics.DrawRectangle(borderPen, border);

            if (AccentEdge && Height > 2)
            {
                using var accent = new LinearGradientBrush(
                    new Rectangle(0, 0, 2, Height),
                    AetherColors.Violet,
                    AetherColors.Cyan,
                    LinearGradientMode.Vertical);
                e.Graphics.FillRectangle(accent, 0, 0, 2, Height);
            }
        }
    }

    internal sealed class AetherChromePanel : Panel
    {
        public AetherChromePanel()
        {
            BackColor = AetherColors.Chrome;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            using var line = new LinearGradientBrush(
                new Rectangle(0, Height - 2, Width, 2),
                AetherColors.Violet,
                AetherColors.Cyan,
                0f);
            e.Graphics.FillRectangle(line, 0, Height - 2, Width, 2);

            using var fragment = new SolidBrush(Color.FromArgb(105, AetherColors.Violet));
            int start = Math.Max(280, Width / 3);
            e.Graphics.FillRectangle(fragment, start, 14, 34, 3);
            e.Graphics.FillRectangle(fragment, start + 18, 22, 22, 3);
            e.Graphics.FillRectangle(fragment, start + 29, 30, 13, 3);
        }
    }

    internal sealed class AetherStagePanel : AetherSurfacePanel
    {
        private readonly Font labelFont = new Font("Segoe UI", 7.5f, FontStyle.Bold, GraphicsUnit.Point);

        public AetherStagePanel()
        {
            BackColor = AetherColors.Void;
            Padding = new Padding(18, 42, 18, 18);
            AccentEdge = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            TextRenderer.DrawText(
                e.Graphics,
                "DISPLAY // 160 × 144",
                labelFont,
                new Rectangle(18, 12, Math.Max(1, Width - 36), 20),
                AetherColors.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            using var signal = new Pen(AetherColors.Cyan, 2f);
            e.Graphics.DrawLine(signal, Width - 58, 22, Width - 20, 22);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                labelFont.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    internal sealed class AetherStatusDot : Control
    {
        private Color signalColor = AetherColors.Muted;

        public AetherStatusDot()
        {
            Size = new Size(12, 12);
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color SignalColor
        {
            get => signalColor;
            set
            {
                signalColor = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? AetherColors.Surface);
            Rectangle dot = Rectangle.Inflate(ClientRectangle, -2, -2);
            using var brush = new SolidBrush(signalColor);
            e.Graphics.FillEllipse(brush, dot);
        }
    }
}
