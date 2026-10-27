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
        public static Color Selection => From(palette.Selection);
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
            Color[] before = [Void, Chrome, Surface, SurfaceRaised, Hairline, Text, Muted, Violet, Cyan, Primary, Success, Danger, Selection];
            palette = next;
            Color[] after = [Void, Chrome, Surface, SurfaceRaised, Hairline, Text, Muted, Violet, Cyan, Primary, Success, Danger, Selection];
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
            Font = AetherTypography.Create(10.5f);
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

        [DefaultValue(nanoboy.Core.UiIcon.None)]
        public nanoboy.Core.UiIcon Icon { get; set; }
        [DefaultValue(false)]
        public bool IconOnly { get; set; }
        [DefaultValue(false)]
        public bool AlignLeft { get; set; }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public nanoboy.Core.UiThemePreset? ThemePreview { get; set; }

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
                AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            }
        }

        protected override AccessibleObject CreateAccessibilityInstance() =>
            new AetherButtonAccessibleObject(this);

        private sealed class AetherButtonAccessibleObject(AetherButton owner)
            : ButtonBaseAccessibleObject(owner)
        {
            // One ButtonBaseAccessibleObject follows the owner handle lifecycle, including handle recreation.
            // A selected command remains a button and explicit role overrides stay authoritative.
            public override AccessibleRole Role => owner.AccessibleRole == AccessibleRole.Default
                ? AccessibleRole.PushButton : owner.AccessibleRole;
            public override AccessibleStates State => base.State |
                (owner.Selected ? AccessibleStates.Selected : AccessibleStates.None);
            public override void DoDefaultAction()
            {
                // UIA Invoke must return before a click handler opens a modal dialog.
                // Recheck at dispatch time: the owner may have closed or become disabled.
                if (owner.IsDisposed || !owner.IsHandleCreated || !owner.CanSelect) return;
                owner.BeginInvoke(() =>
                {
                    if (!owner.IsDisposed && owner.IsHandleCreated && owner.CanSelect) owner.PerformClick();
                });
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
            using GraphicsPath path = CreateChamferedPath(bounds, kind == AetherButtonKind.Primary ? Math.Min(8 * DeviceDpi / 96, Height / 3) : 0);

            Color foreground = Enabled ? AetherColors.Text : AetherColors.Muted;
            Color border = Focused ? AetherColors.Cyan : AetherColors.Hairline;
            Color surface = Enabled ? AetherColors.Surface : AetherColors.SurfaceRaised;

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
                    surface = AetherColors.Selection;
                    border = AetherColors.Selection;
                }
                else if (hovered && Enabled && kind != AetherButtonKind.Danger)
                    border = Focused ? AetherColors.Cyan : AetherColors.Violet;

                using var fill = new SolidBrush(surface);
                graphics.FillPath(fill, path);
                if (Enabled && selected && Width > 12 && Height > 12)
                {
                    using var signal = new Pen(selected ? AetherColors.Cyan : AetherColors.Violet, 2f);
                    graphics.DrawLine(signal, 4, 5, 4, Height - 9);
                }
            }

            if (kind != AetherButtonKind.Ghost || hovered || Focused)
            using (var outline = new Pen(border, 1f))
            {
                graphics.DrawPath(outline, path);
            }

            Rectangle textBounds = bounds;
            if (ThemePreview is { } preview)
            {
                int captionHeight = Math.Max(Font.Height + 6, 25 * DeviceDpi / 96);
                AetherIconPaint.Theme(graphics, new Rectangle(8, 8, Width - 16, Math.Max(12, Height - captionHeight - 12)), preview);
                textBounds = new Rectangle(4, Height - captionHeight, Width - 8, captionHeight);
            }
            else if (Icon != nanoboy.Core.UiIcon.None)
            {
                int size = Math.Max(16, (int)(18 * DeviceDpi / 96f));
                int textWidth = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding).Width;
                int x = IconOnly ? (Width - size) / 2 : AlignLeft ? 14 * DeviceDpi / 96 : Math.Max(12, (Width - size - 9 - textWidth) / 2);
                AetherIconPaint.Draw(graphics, Icon, new Rectangle(x, (Height - size) / 2, size, size), selected && kind != AetherButtonKind.Primary ? AetherColors.Violet : foreground);
                textBounds = new Rectangle(x + size + 9, 0, Math.Max(1, Width - x - size - 18), Height);
            }
            if (!IconOnly)
                TextRenderer.DrawText(graphics, Text, Font, textBounds, foreground,
                    (AlignLeft || Icon != nanoboy.Core.UiIcon.None ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter) |
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            if (Focused && Width > 12 && Height > 12)
            {
                Rectangle focusBounds = Rectangle.Inflate(bounds, -5, -5);
                AetherWidgetPaint.Focus(graphics, focusBounds, foreground);
            }
        }

        private static GraphicsPath CreateChamferedPath(Rectangle bounds, int cut)
        {
            var path = new GraphicsPath();
            if (cut == 0) { path.AddRectangle(bounds); return path; }
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

        [DefaultValue(false)]
        public bool DividerOnly { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 2 || Height < 2) return;
            e.Graphics.Clear(BackColor);
            Rectangle border = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using var borderPen = new Pen(AetherColors.Hairline);
            if (DividerOnly) e.Graphics.DrawLine(borderPen, 0, Height - 1, Width, Height - 1);
            else e.Graphics.DrawRectangle(borderPen, border);
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

            using var line = new SolidBrush(AetherColors.Hairline);
            e.Graphics.FillRectangle(line, 0, Math.Max(0, Height - 1), Width, 1);
        }
    }

    internal sealed class AetherStagePanel : AetherSurfacePanel
    {
        public AetherStagePanel()
        {
            BackColor = AetherColors.Void;
            Padding = Padding.Empty;
            AccentEdge = false;
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
