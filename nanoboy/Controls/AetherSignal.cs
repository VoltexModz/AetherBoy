using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace nanoboy.Controls
{
    internal static class AetherSignal
    {
        public static DialogResult Show(
            IWin32Window? owner,
            string message,
            string caption,
            MessageBoxButtons buttons = MessageBoxButtons.OK,
            MessageBoxIcon icon = MessageBoxIcon.None)
        {
            using var dialog = new AetherSignalDialog(message, caption, buttons, icon);
            return owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        }

        public static DialogResult Show(
            string message,
            string caption,
            MessageBoxButtons buttons = MessageBoxButtons.OK,
            MessageBoxIcon icon = MessageBoxIcon.None) =>
            Show(null, message, caption, buttons, icon);
    }

    internal sealed class AetherSignalDialog : Form
    {
        private readonly MessageBoxButtons buttons;

        internal AetherSignalDialog(
            string message,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon)
        {
            this.buttons = buttons;
            Text = string.IsNullOrWhiteSpace(caption) ? ProductInfo.Name : caption;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Branding.AppBrand.ApplyIcon(this);

            SignalSpec signal = SignalSpec.FromIcon(icon);
            using var measureFont = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
            int messageHeight = TextRenderer.MeasureText(
                message,
                measureFont,
                new Size(490, 0),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;
            int contentHeight = Math.Clamp(messageHeight + 148, 242, 410);
            ClientSize = new Size(680, contentHeight);
            MinimumSize = Size;
            MaximumSize = Size;

            var glyph = new AetherSignalGlyph
            {
                Location = new Point(28, 34),
                SignalColor = signal.Color,
                Glyph = signal.Glyph,
                Size = new Size(82, 82)
            };
            Controls.Add(glyph);

            var category = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = signal.Color,
                Location = new Point(136, 30),
                Size = new Size(510, 22),
                Tag = "value",
                Text = signal.Category
            };
            Controls.Add(category);

            var messageLabel = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = AetherColors.Text,
                Location = new Point(136, 58),
                Size = new Size(510, Math.Max(80, contentHeight - 148)),
                Tag = "value",
                Text = message,
                UseMnemonic = false
            };
            Controls.Add(messageLabel);

            var divider = new Panel
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                BackColor = AetherColors.Hairline,
                Location = new Point(28, contentHeight - 76),
                Size = new Size(624, 1)
            };
            Controls.Add(divider);

            BuildButtons(contentHeight);
            AetherDialog.Apply(
                this,
                $"SYSTEM SIGNAL // {signal.Code}",
                signal.Description);
            category.ForeColor = signal.Color;
            messageLabel.ForeColor = AetherColors.Text;
        }

        private void BuildButtons(int contentHeight)
        {
            IReadOnlyList<ButtonSpec> specs = ButtonSpec.For(buttons);
            const int width = 124;
            const int gap = 10;
            int left = ClientSize.Width - 28 - (specs.Count * width) - ((specs.Count - 1) * gap);

            for (int index = 0; index < specs.Count; index++)
            {
                ButtonSpec spec = specs[index];
                var button = new AetherButton
                {
                    DialogResult = spec.Result,
                    Kind = spec.Kind,
                    Location = new Point(left + (index * (width + gap)), contentHeight - 58),
                    Name = $"aetherSignal{spec.Result}Button",
                    Size = new Size(width, 40),
                    Text = spec.Text
                };
                Controls.Add(button);

                if (spec.IsDefault)
                {
                    AcceptButton = button;
                }

                if (spec.Result == DialogResult.Cancel)
                {
                    CancelButton = button;
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.None)
            {
                DialogResult = buttons switch
                {
                    MessageBoxButtons.YesNo => DialogResult.No,
                    MessageBoxButtons.OK => DialogResult.OK,
                    _ => DialogResult.Cancel
                };
            }

            base.OnFormClosing(e);
        }

        private readonly record struct SignalSpec(
            string Code,
            string Category,
            string Description,
            string Glyph,
            Color Color)
        {
            public static SignalSpec FromIcon(MessageBoxIcon icon)
            {
                return icon switch
                {
                    MessageBoxIcon.Error => new(
                        "ERR",
                        "ACTION COULD NOT COMPLETE",
                        "AetherBoy hat den Vorgang sicher abgebrochen.",
                        "×",
                        AetherColors.Danger),
                    MessageBoxIcon.Warning => new(
                        "WARN",
                        "ATTENTION REQUIRED",
                        "Bitte prüfe die folgenden Informationen.",
                        "!",
                        Color.FromArgb(255, 190, 92)),
                    MessageBoxIcon.Question => new(
                        "QUERY",
                        "DECISION REQUIRED",
                        "AetherBoy wartet auf deine Entscheidung.",
                        "?",
                        AetherColors.Violet),
                    _ => new(
                        "INFO",
                        "STATUS UPDATE",
                        "Neue Information aus der laufenden Sitzung.",
                        "i",
                        AetherColors.Cyan)
                };
            }
        }

        private readonly record struct ButtonSpec(
            string Text,
            DialogResult Result,
            AetherButtonKind Kind,
            bool IsDefault = false)
        {
            public static IReadOnlyList<ButtonSpec> For(MessageBoxButtons buttons)
            {
                return buttons switch
                {
                    MessageBoxButtons.OKCancel => new[]
                    {
                        new ButtonSpec("ABBRECHEN", DialogResult.Cancel, AetherButtonKind.Ghost),
                        new ButtonSpec("OK", DialogResult.OK, AetherButtonKind.Primary, true)
                    },
                    MessageBoxButtons.YesNo => new[]
                    {
                        new ButtonSpec("NEIN", DialogResult.No, AetherButtonKind.Secondary),
                        new ButtonSpec("JA", DialogResult.Yes, AetherButtonKind.Primary, true)
                    },
                    MessageBoxButtons.YesNoCancel => new[]
                    {
                        new ButtonSpec("ABBRECHEN", DialogResult.Cancel, AetherButtonKind.Ghost),
                        new ButtonSpec("NEIN", DialogResult.No, AetherButtonKind.Secondary),
                        new ButtonSpec("JA", DialogResult.Yes, AetherButtonKind.Primary, true)
                    },
                    MessageBoxButtons.RetryCancel => new[]
                    {
                        new ButtonSpec("ABBRECHEN", DialogResult.Cancel, AetherButtonKind.Ghost),
                        new ButtonSpec("ERNEUT", DialogResult.Retry, AetherButtonKind.Primary, true)
                    },
                    MessageBoxButtons.AbortRetryIgnore => new[]
                    {
                        new ButtonSpec("ABBRECHEN", DialogResult.Abort, AetherButtonKind.Danger),
                        new ButtonSpec("IGNORIEREN", DialogResult.Ignore, AetherButtonKind.Secondary),
                        new ButtonSpec("ERNEUT", DialogResult.Retry, AetherButtonKind.Primary, true)
                    },
                    _ => new[]
                    {
                        new ButtonSpec("OK", DialogResult.OK, AetherButtonKind.Primary, true)
                    }
                };
            }
        }
    }

    internal sealed class AetherSignalGlyph : Control
    {
        private readonly Timer animationTimer;
        private float phase;

        public AetherSignalGlyph()
        {
            DoubleBuffered = true;
            animationTimer = new Timer { Interval = 45, Enabled = true };
            animationTimer.Tick += (_, _) =>
            {
                phase += 0.09f;
                Invalidate();
            };
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color SignalColor { get; set; } = AetherColors.Cyan;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Glyph { get; set; } = "i";

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? AetherColors.Void);

            float pulse = 0.5f + (0.5f * (float)Math.Sin(phase));
            Rectangle halo = Rectangle.Inflate(ClientRectangle, -4, -4);
            using (var haloBrush = new SolidBrush(Color.FromArgb(18 + (int)(pulse * 20), SignalColor)))
            {
                e.Graphics.FillEllipse(haloBrush, halo);
            }

            Rectangle core = Rectangle.Inflate(ClientRectangle, -13, -13);
            using (var fill = new SolidBrush(AetherColors.SurfaceRaised))
            using (var border = new Pen(SignalColor, 2f))
            {
                e.Graphics.FillEllipse(fill, core);
                e.Graphics.DrawEllipse(border, core);
            }

            using var font = new Font("Segoe UI", 24f, FontStyle.Bold, GraphicsUnit.Point);
            TextRenderer.DrawText(
                e.Graphics,
                Glyph,
                font,
                core,
                SignalColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
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
