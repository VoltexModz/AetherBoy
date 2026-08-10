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
            bool showMinimize = false)
        {
            ArgumentNullException.ThrowIfNull(form);

            form.SuspendLayout();

            Size contentSize = form.ClientSize;
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
            form.ClientSize = new Size(contentSize.Width, contentSize.Height + HeaderHeight);

            if (fixedSize)
            {
                form.MinimumSize = form.Size;
                form.MaximumSize = form.Size;
            }
            else if (requestedMinimumSize.Width > 0 || requestedMinimumSize.Height > 0)
            {
                form.MinimumSize = new Size(
                    requestedMinimumSize.Width,
                    requestedMinimumSize.Height + HeaderHeight);
            }

            var body = new Panel
            {
                Name = "aetherDialogBody",
                BackColor = AetherColors.Void,
                Bounds = new Rectangle(1, HeaderHeight + 1, contentSize.Width - 2, contentSize.Height - 1),
                Dock = DockStyle.Fill
            };

            foreach (Control control in existingControls)
            {
                body.Controls.Add(control);
            }

            var header = new AetherChromePanel
            {
                Name = "aetherDialogHeader",
                Dock = DockStyle.Top,
                Height = HeaderHeight,
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
                AutoEllipsis = true,
                AutoSize = false,
                ForeColor = AetherColors.Muted,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(84, 53),
                Size = new Size(Math.Max(120, contentSize.Width - 220), 18),
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

            form.Controls.Add(body);
            form.Controls.Add(header);
            Style(body.Controls);

            form.KeyDown += (_, eventArgs) =>
            {
                if (eventArgs.KeyCode == Keys.Escape)
                {
                    form.Close();
                }
            };
            form.Disposed += (_, _) => mark.Dispose();
            form.ResumeLayout(performLayout: true);
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

                    case Button button:
                        button.BackColor = AetherColors.SurfaceRaised;
                        button.ForeColor = AetherColors.Text;
                        button.FlatStyle = FlatStyle.Flat;
                        button.FlatAppearance.BorderColor = AetherColors.Violet;
                        button.FlatAppearance.BorderSize = 1;
                        button.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
                        button.Cursor = button.Enabled ? Cursors.Hand : Cursors.Default;
                        button.UseVisualStyleBackColor = false;
                        break;

                    case TextBox textBox:
                        textBox.BackColor = AetherColors.SurfaceRaised;
                        textBox.ForeColor = AetherColors.Text;
                        textBox.BorderStyle = BorderStyle.FixedSingle;
                        break;

                    case RichTextBox richTextBox:
                        richTextBox.BackColor = AetherColors.SurfaceRaised;
                        richTextBox.ForeColor = AetherColors.Text;
                        richTextBox.BorderStyle = BorderStyle.FixedSingle;
                        break;

                    case ListView listView:
                        StyleListView(listView);
                        break;

                    case ListBox listBox:
                        listBox.BackColor = AetherColors.SurfaceRaised;
                        listBox.ForeColor = AetherColors.Text;
                        listBox.BorderStyle = BorderStyle.FixedSingle;
                        break;

                    case ComboBox comboBox:
                        comboBox.BackColor = AetherColors.SurfaceRaised;
                        comboBox.ForeColor = AetherColors.Text;
                        comboBox.FlatStyle = FlatStyle.Flat;
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

        private static void StyleListView(ListView listView)
        {
            listView.BackColor = AetherColors.SurfaceRaised;
            listView.ForeColor = AetherColors.Text;
            listView.BorderStyle = BorderStyle.FixedSingle;
            listView.Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            listView.GridLines = false;
            listView.OwnerDraw = true;
            listView.DrawColumnHeader += (_, eventArgs) =>
            {
                using var fill = new SolidBrush(AetherColors.Chrome);
                using var edge = new Pen(AetherColors.Hairline);
                using var headerFont = new Font("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Point);
                eventArgs.Graphics.FillRectangle(fill, eventArgs.Bounds);
                eventArgs.Graphics.DrawLine(
                    edge,
                    eventArgs.Bounds.Left,
                    eventArgs.Bounds.Bottom - 1,
                    eventArgs.Bounds.Right,
                    eventArgs.Bounds.Bottom - 1);
                TextRenderer.DrawText(
                    eventArgs.Graphics,
                    eventArgs.Header?.Text ?? string.Empty,
                    headerFont,
                    Rectangle.Inflate(eventArgs.Bounds, -8, 0),
                    AetherColors.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
            listView.DrawItem += (_, _) => { };
            listView.DrawSubItem += (_, eventArgs) =>
            {
                bool selected = eventArgs.Item.Selected;
                Color background = selected ? Color.FromArgb(39, 32, 68) : AetherColors.SurfaceRaised;
                Color foreground = eventArgs.SubItem.Text switch
                {
                    "READY" => AetherColors.Success,
                    "MISSING" => AetherColors.Danger,
                    _ => selected ? AetherColors.Cyan : AetherColors.Text
                };
                using var fill = new SolidBrush(background);
                eventArgs.Graphics.FillRectangle(fill, eventArgs.Bounds);
                TextRenderer.DrawText(
                    eventArgs.Graphics,
                    eventArgs.SubItem.Text,
                    listView.Font,
                    Rectangle.Inflate(eventArgs.Bounds, -8, 0),
                    foreground,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
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
