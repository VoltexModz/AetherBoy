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
        public static Color Secondary => From(palette.Secondary);
        public static Color OnPrimary => From(palette.OnPrimary);
        public static nanoboy.Core.UiButtonGradient ButtonGradient(bool hovered, bool pressed) =>
            pressed ? palette.ButtonPressed : hovered ? palette.ButtonHover : palette.Button;
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
            if (Equals(control.Tag, "theme-swatch")) return;
            Branding.AppBrand.RefreshMark(control);
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

            // Dock/scroll layouts can temporarily give a button a zero-sized row during scaling.
            if (Width < 3 || Height < 3) return;

            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using GraphicsPath path = CreateChamferedPath(bounds, Math.Min(8, Height / 3));

            Color foreground = Enabled ? AetherColors.Text : AetherColors.Muted;
            Color border = Focused ? AetherColors.Cyan : AetherColors.Hairline;
            Color surface = AetherColors.SurfaceRaised;

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
                var ramp = AetherColors.ButtonGradient(hovered, pressed);
                using var fill = new LinearGradientBrush(bounds,
                    Color.FromArgb(ramp.Start.R, ramp.Start.G, ramp.Start.B),
                    Color.FromArgb(ramp.End.R, ramp.End.G, ramp.End.B), 0f);
                graphics.FillPath(fill, path);
                foreground = Color.FromArgb(ramp.Text.R, ramp.Text.G, ramp.Text.B);
                border = Focused ? AetherColors.Cyan : AetherColors.Primary;
            }
            else
            {
                if (pressed && Enabled)
                {
                    surface = AetherColors.Chrome;
                }
                else if (selected && Enabled)
                {
                    surface = AetherColors.SurfaceRaised;
                    border = AetherColors.Cyan;
                }
                else if (hovered && Enabled && kind != AetherButtonKind.Danger)
                    border = Focused ? AetherColors.Cyan : AetherColors.Violet;

                using var fill = new SolidBrush(surface);
                graphics.FillPath(fill, path);
                if (Enabled && (selected || hovered) && Width > 12 && Height > 12)
                {
                    using var signal = new Pen(selected ? AetherColors.Cyan : AetherColors.Violet, 2f);
                    graphics.DrawLine(signal, 4, 5, 4, Height - 9);
                }
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

            if (Focused && Width > 12 && Height > 12)
            {
                Rectangle focusBounds = Rectangle.Inflate(bounds, -5, -5);
                AetherWidgetPaint.Focus(graphics, focusBounds, foreground);
            }
        }

        private static GraphicsPath CreateChamferedPath(Rectangle bounds, int cut)
        {
            var path = new GraphicsPath();
            Span<nanoboy.Core.UiPoint> vertices = stackalloc nanoboy.Core.UiPoint[nanoboy.Core.UiChamfer.VertexCount];
            nanoboy.Core.UiChamfer.Write(vertices, bounds.X, bounds.Y, bounds.Width, bounds.Height, cut);
            var points = new PointF[vertices.Length];
            for (int index = 0; index < points.Length; index++)
                points[index] = new PointF(vertices[index].X, vertices[index].Y);
            path.AddPolygon(points);
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
            if (Width < 2 || Height < 2) return;
            e.Graphics.Clear(BackColor);
            Rectangle border = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using var borderPen = new Pen(AetherColors.Hairline);
            e.Graphics.DrawRectangle(borderPen, border);
            if (AccentEdge)
            {
                using var accent = new LinearGradientBrush(ClientRectangle, AetherColors.Primary, AetherColors.Secondary, 90f);
                e.Graphics.FillRectangle(accent, 0, 1, Math.Min(2, Width), Height - 2);
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

            using var line = new LinearGradientBrush(ClientRectangle, AetherColors.Primary, AetherColors.Secondary, 0f);
            e.Graphics.FillRectangle(line, 0, Math.Max(0, Height - 2), Width, Math.Min(2, Height));
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
                global::AetherBoy.Runtime.Localization.UiText.Get("GAME SCREEN"),
                labelFont,
                new Rectangle(18, 12, Math.Max(1, Width - 36), 20),
                AetherColors.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            if (Width > 120 && Height > 24)
            {
                using var signal = new Pen(AetherColors.Cyan, 2f);
                e.Graphics.DrawLine(signal, Width - 58, 22, Width - 20, 22);
            }
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
