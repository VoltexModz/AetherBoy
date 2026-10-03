using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using nanoboy.Branding;

namespace nanoboy.Controls
{
    internal static class AetherDialog
    {
        private const int HeaderHeight = 82;
        private const int WmNclButtonDown = 0x00A1;
        private const int HtCaption = 0x0002;

        public static Panel Apply(
            Form form,
            string section,
            string description,
            bool showMinimize = false,
            Func<bool>? gamepadNavigationEnabled = null,
            int descriptionLines = 1)
        {
            ArgumentNullException.ThrowIfNull(form);
            int headerHeight = HeaderHeight + (Math.Clamp(descriptionLines, 1, 3) - 1) * 18;

            form.SuspendLayout();
            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.AutoScaleDimensions = new SizeF(96, 96);

            Size contentSize = form.ClientSize;
            IButtonControl? acceptButton = form.AcceptButton, cancelButton = form.CancelButton;
            Size requestedMinimumSize = form.MinimumSize;
            Size requestedMaximumSize = form.MaximumSize;
            bool fixedSize = requestedMinimumSize.Width > 0 &&
                requestedMaximumSize == requestedMinimumSize;
            var existingControls = new List<Control>();
            foreach (Control control in form.Controls)
            {
                existingControls.Add(control);
            }

            form.Controls.Clear();
            form.FormBorderStyle = FormBorderStyle.None;
            form.BackColor = AetherColors.Hairline;
            form.ForeColor = AetherColors.Text;
            form.Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            form.KeyPreview = true;
            form.MaximizeBox = false;
            form.MinimizeBox = showMinimize;
            form.Padding = new Padding(1);
            form.MinimumSize = Size.Empty;
            form.MaximumSize = Size.Empty;
            form.ClientSize = new Size(contentSize.Width, contentSize.Height + headerHeight);

            if (fixedSize)
            {
                form.MinimumSize = form.Size;
                form.MaximumSize = form.Size;
            }
            else if (requestedMinimumSize.Width > 0 || requestedMinimumSize.Height > 0)
            {
                form.MinimumSize = new Size(
                    requestedMinimumSize.Width,
                    requestedMinimumSize.Height + headerHeight);
            }

            var body = new Panel
            {
                Name = "aetherDialogBody",
                BackColor = AetherColors.Void,
                Bounds = new Rectangle(1, headerHeight + 1, contentSize.Width - 2, contentSize.Height - 2),
                Dock = DockStyle.None,
                Location = Point.Empty
            };
            var viewport = new AetherScrollViewport
            {
                Name = "aetherDialogViewport", Dock = DockStyle.Fill,
                BackColor = AetherColors.Void
            };
            viewport.SetContent(body, contentSize);
            void FitBody()
            {
                float factor = form.DeviceDpi / 96f;
                viewport.SetMinimumContent(new Size((int)((contentSize.Width - 2) * factor),
                    (int)((contentSize.Height - 2) * factor)));
            }
            viewport.SizeChanged += (_, _) => FitBody();
            void FitDialog()
            {
                Rectangle work = Screen.FromHandle(form.Handle).WorkingArea;
                form.MinimumSize = Size.Empty;
                form.MaximumSize = Size.Empty;
                Size ideal = new Size((int)(contentSize.Width * form.DeviceDpi / 96f),
                    (int)((contentSize.Height + headerHeight) * form.DeviceDpi / 96f));
                form.Size = new Size(Math.Min(ideal.Width, work.Width), Math.Min(ideal.Height, work.Height));
                form.Location = new Point(Math.Clamp(form.Left, work.Left, work.Right - form.Width),
                    Math.Clamp(form.Top, work.Top, work.Bottom - form.Height));
                FitBody();
            }
            form.Shown += (_, _) => FitDialog();
            form.DpiChanged += (_, _) => FitDialog();

            foreach (Control control in existingControls)
            {
                body.Controls.Add(control);
            }

            var header = new AetherChromePanel
            {
                Name = "aetherDialogHeader",
                Dock = DockStyle.Top,
                Height = headerHeight,
                Width = contentSize.Width - 2,
                Cursor = Cursors.SizeAll
            };

            Image mark = AppBrand.CreateMarkBitmap();
            var logo = new PictureBox
            {
                Name = "aetherDialogMark",
                Image = mark,
                Location = new Point(18, 14),
                Size = new Size(54, 50),
                SizeMode = PictureBoxSizeMode.Zoom,
                TabStop = false
            };

            var sectionLabel = new Label
            {
                AutoSize = false,
                ForeColor = AetherColors.Cyan,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(84, 10),
                Size = new Size(Math.Max(120, contentSize.Width - 220), 17),
                Text = section.ToUpperInvariant()
            };

            var titleLabel = new Label
            {
                AutoEllipsis = true,
                AutoSize = false,
                ForeColor = AetherColors.Text,
                Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(82, 25),
                Size = new Size(Math.Max(120, contentSize.Width - 220), 27),
                Text = form.Text
            };

            var descriptionLabel = new Label
            {
                Name = "aetherDialogDescription",
                AutoEllipsis = true,
                AutoSize = false,
                ForeColor = AetherColors.Muted,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(84, 53),
                Size = new Size(Math.Max(120, contentSize.Width - 220), headerHeight - 64),
                Text = description
            };

            var closeButton = new AetherButton
            {
                Name = "aetherDialogCloseButton",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Kind = AetherButtonKind.Ghost,
                Location = new Point(contentSize.Width - 55, 20),
                Size = new Size(38, 34),
                Text = "×",
                TabStop = false
            };
            closeButton.Click += (_, _) => form.Close();

            header.Controls.Add(logo);
            header.Controls.Add(sectionLabel);
            header.Controls.Add(titleLabel);
            header.Controls.Add(descriptionLabel);
            header.Controls.Add(closeButton);

            if (showMinimize)
            {
                var minimizeButton = new AetherButton
                {
                    Name = "aetherDialogMinimizeButton",
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    Kind = AetherButtonKind.Ghost,
                    Location = new Point(contentSize.Width - 99, 20),
                    Size = new Size(38, 34),
                    Text = "−",
                    TabStop = false
                };
                minimizeButton.Click += (_, _) => form.WindowState = FormWindowState.Minimized;
                header.Controls.Add(minimizeButton);
            }

            MouseEventHandler dragWindow = (_, eventArgs) =>
            {
                if (eventArgs.Button != MouseButtons.Left)
                {
                    return;
                }

                ReleaseCapture();
                SendMessage(form.Handle, WmNclButtonDown, HtCaption, 0);
            };
            header.MouseDown += dragWindow;
            logo.MouseDown += dragWindow;
            sectionLabel.MouseDown += dragWindow;
            titleLabel.MouseDown += dragWindow;
            descriptionLabel.MouseDown += dragWindow;

            form.Controls.Add(viewport);
            form.Controls.Add(header);
            // Moving buttons out of Form.Controls can clear WinForms' default-button references.
            form.AcceptButton = acceptButton;
            form.CancelButton = cancelButton;
            Style(body.Controls);

            float uiScale = Math.Clamp(Properties.Settings.Default.UiScalePercent, 100, 150) / 100f;
            if (uiScale != 1f)
            {
                InterfaceScale.Apply(body, uiScale);
                contentSize = new Size((int)Math.Ceiling(contentSize.Width * uiScale), (int)Math.Ceiling(contentSize.Height * uiScale));
            }

            form.KeyDown += (_, eventArgs) =>
            {
                if (eventArgs.KeyCode == Keys.Escape)
                {
                    form.Close();
                }
            };
            form.Disposed += (_, _) => mark.Dispose();
            form.ResumeLayout(performLayout: true);
            nanoboy.Input.GamepadNavigation.Attach(form, gamepadNavigationEnabled);
            return body;
        }

        private static void Style(Control.ControlCollection controls)
        {
            foreach (Control control in controls)
            {
                switch (control)
                {
                    case AetherButton button:
                        button.UseVisualStyleBackColor = false;
                        break;

                    case AetherTextBox input:
                        input.RefreshTheme();
                        continue; // The encapsulated EDIT backend must never receive generic styling.

                    case AetherList:
                        break;

                    case AetherGroupBox:
                        break;

                    case GroupBox groupBox:
                        groupBox.BackColor = AetherColors.Surface;
                        groupBox.ForeColor = AetherColors.Cyan;
                        groupBox.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
                        break;

                    case CheckBox checkBox:
                        checkBox.BackColor = Color.Transparent;
                        checkBox.ForeColor = AetherColors.Text;
                        checkBox.Font = new Font("Segoe UI", 8.75f, FontStyle.Bold, GraphicsUnit.Point);
                        break;

                    case Label label:
                        label.BackColor = Color.Transparent;
                        label.ForeColor = ResolveLabelColor(label);
                        break;

                    case Panel panel when Equals(panel.Tag, "theme-swatch"):
                        // Preset previews are fixed RGB samples, not dialog surfaces.
                        break;

                    case Panel panel when panel is not AetherSurfacePanel:
                        panel.BackColor = AetherColors.Void;
                        break;
                }

                if (control.HasChildren)
                {
                    Style(control.Controls);
                }
            }
        }

        private static Color ResolveLabelColor(Label label)
        {
            if (label.Tag is string role)
            {
                return role switch
                {
                    "accent" => AetherColors.Cyan,
                    "danger" => AetherColors.Danger,
                    "value" => AetherColors.Text,
                    _ => AetherColors.Muted
                };
            }

            if (label.Name.StartsWith("labelQ", StringComparison.Ordinal) ||
                label.Name.StartsWith("labelW", StringComparison.Ordinal) ||
                label.Name.StartsWith("labelN", StringComparison.Ordinal))
            {
                return AetherColors.Text;
            }

            return AetherColors.Muted;
        }


        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int message, int wParam, int lParam);
    }

    internal sealed class AetherGroupBox : GroupBox
    {
        public AetherGroupBox()
        {
            BackColor = AetherColors.Surface;
            ForeColor = AetherColors.Cyan;
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
            Padding = new Padding(12, 22, 12, 12);
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(AetherColors.Surface);
            Rectangle frame = new Rectangle(0, 9, Math.Max(1, Width - 1), Math.Max(1, Height - 10));
            using var border = new Pen(AetherColors.Hairline);
            e.Graphics.DrawRectangle(border, frame);

            Size textSize = TextRenderer.MeasureText(Text, Font);
            var textBounds = new Rectangle(13, 0, textSize.Width + 12, 18);
            using var textBackground = new SolidBrush(AetherColors.Surface);
            e.Graphics.FillRectangle(textBackground, textBounds);
            TextRenderer.DrawText(
                e.Graphics,
                Text.ToUpperInvariant(),
                Font,
                new Rectangle(18, 0, Math.Max(1, Width - 36), 18),
                AetherColors.Cyan,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            using var accent = new LinearGradientBrush(
                new Rectangle(1, 10, 2, Math.Max(1, Height - 12)),
                AetherColors.Violet,
                AetherColors.Cyan,
                LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(accent, 1, 10, 2, Math.Max(1, Height - 12));
        }
    }
}
