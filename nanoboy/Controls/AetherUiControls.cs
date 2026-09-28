using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using UiRgb = nanoboy.Core.UiRgb;
using UiThemePalette = nanoboy.Core.UiThemePalette;

namespace nanoboy.Controls
{
    internal static class AetherColors
    {
        private static UiThemePalette palette = new(null, null, null);
        private static Color From(UiRgb value) => Color.FromArgb(value.R, value.G, value.B);
        public static Color Void => From(palette.Background);
        public static Color Chrome => From(palette.Chrome);
        public static Color Surface => From(palette.Surface);
        public static Color SurfaceRaised => From(palette.Raised);
        public static Color Hairline => From(palette.Border);
        public static Color Text => From(palette.Text);
        public static Color Muted => From(palette.Muted);
        public static Color Violet => From(palette.PrimaryText);
        public static Color Pulse => Violet;
        public static Color Cyan => From(palette.SecondaryText);
        public static Color Primary => From(palette.Primary);
        public static Color OnPrimary => From(palette.OnPrimary);
        public static Color Success => From(palette.SuccessText);
        public static Color Danger => From(palette.DangerText);

        public static void Apply(UiThemePalette next)
        {
            Color[] before = [Void, Chrome, Surface, SurfaceRaised, Hairline, Text, Muted, Violet, Cyan, Primary, Success, Danger];
            palette = next;
            Color[] after = [Void, Chrome, Surface, SurfaceRaised, Hairline, Text, Muted, Violet, Cyan, Primary, Success, Danger];
            foreach (Form form in Application.OpenForms)
                Recolor(form, before, after);
        }

        private static void Recolor(Control control, Color[] before, Color[] after)
        {
            for (int i = 0; i < before.Length; i++)
            {
                if (control.BackColor == before[i]) { control.BackColor = after[i]; break; }
            }
            for (int i = 0; i < before.Length; i++)
            {
                if (control.ForeColor == before[i]) { control.ForeColor = after[i]; break; }
            }
            if (control is AetherStatusDot dot)
                for (int i = 0; i < before.Length; i++)
                    if (dot.SignalColor == before[i]) { dot.SignalColor = after[i]; break; }
            foreach (Control child in control.Controls) Recolor(child, before, after);
            control.Invalidate();
        }
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
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
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
            using GraphicsPath path = CreateRoundedPath(bounds, Math.Min(8, Height / 3));

            Color foreground = Enabled ? AetherColors.Text : AetherColors.Muted;
            Color border = Focused ? AetherColors.Cyan : AetherColors.Hairline;
            Color surface = hovered ? AetherColors.SurfaceRaised : AetherColors.Surface;

            if (kind == AetherButtonKind.Danger)
            {
                border = AetherColors.Danger;
            }
            else if (kind == AetherButtonKind.Ghost)
            {
                surface = hovered ? AetherColors.SurfaceRaised : Parent?.BackColor ?? AetherColors.Chrome;
            }

            if (kind == AetherButtonKind.Primary && Enabled)
            {
                using var fill = new SolidBrush(pressed ? Color.FromArgb(190, AetherColors.Primary) : AetherColors.Primary);
                graphics.FillPath(fill, path);
                foreground = AetherColors.OnPrimary;
                border = AetherColors.Primary;
            }
            else
            {
                if (pressed)
                {
                    surface = AetherColors.Chrome;
                }
                else if (selected)
                {
                    surface = AetherColors.SurfaceRaised;
                    border = AetherColors.Primary;
                }

                using var fill = new SolidBrush(surface);
                graphics.FillPath(fill, path);
            }

            using (var outline = new Pen(border, selected ? 1.6f : 1f))
            {
                graphics.DrawPath(outline, path);
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

            if (Focused)
            {
                Rectangle focusBounds = Rectangle.Inflate(bounds, -5, -5);
                ControlPaint.DrawFocusRectangle(graphics, focusBounds, foreground, Color.Transparent);
            }
        }

        private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
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
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? AetherColors.Void);
            Rectangle border = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using var shape = new GraphicsPath();
            shape.AddArc(border.Left, border.Top, 20, 20, 180, 90);
            shape.AddArc(border.Right - 20, border.Top, 20, 20, 270, 90);
            shape.AddArc(border.Right - 20, border.Bottom - 20, 20, 20, 0, 90);
            shape.AddArc(border.Left, border.Bottom - 20, 20, 20, 90, 90);
            shape.CloseFigure();
            using var fill = new SolidBrush(BackColor);
            e.Graphics.FillPath(fill, shape);
            using var borderPen = new Pen(AetherColors.Hairline);
            e.Graphics.DrawPath(borderPen, shape);
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

            using var line = new SolidBrush(AetherColors.Hairline);
            e.Graphics.FillRectangle(line, 0, Height - 1, Width, 1);
        }
    }

    internal sealed class AetherStagePanel : AetherSurfacePanel
    {
        private readonly Font labelFont = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point);

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
                "GAME SCREEN",
                labelFont,
                new Rectangle(18, 12, Math.Max(1, Width - 36), 20),
                AetherColors.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

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
        private readonly Timer animationTimer;
        private Color signalColor = AetherColors.Muted;
        private bool animated;
        private float phase;

        public AetherStatusDot()
        {
            Size = new Size(12, 12);
            animationTimer = new Timer { Interval = 55 };
            animationTimer.Tick += (_, _) =>
            {
                phase += 0.12f;
                Invalidate();
            };
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

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Animated
        {
            get => animated;
            set
            {
                if (animated == value)
                {
                    return;
                }

                animated = value;
                animationTimer.Enabled = value;
                if (!value)
                {
                    phase = 0f;
                }
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? AetherColors.Surface);
            if (animated)
            {
                float pulse = 0.5f + (0.5f * (float)Math.Sin(phase));
                Rectangle halo = Rectangle.Inflate(ClientRectangle, -1, -1);
                using var haloBrush = new SolidBrush(Color.FromArgb(28 + (int)(pulse * 34), signalColor));
                e.Graphics.FillEllipse(haloBrush, halo);
            }

            Rectangle dot = Rectangle.Inflate(ClientRectangle, -2, -2);
            using var brush = new SolidBrush(signalColor);
            e.Graphics.FillEllipse(brush, dot);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animationTimer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
