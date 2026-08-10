using System;
using System.Drawing;
using System.Windows.Forms;
using nanoboy.Controls;

namespace nanoboy
{
    public partial class frmAbout : Form
    {
        public frmAbout()
        {
            InitializeComponent();
            Branding.AppBrand.ApplyIcon(this);
            Image? previousImage = pictureBox1.Image;
            Image mark = Branding.AppBrand.CreateMarkBitmap();
            pictureBox1.Image = mark;
            previousImage?.Dispose();
            Disposed += (_, _) => mark.Dispose();
            Text = $"Über {ProductInfo.Name}";
            textBox1.Text =
                $"{ProductInfo.DisplayName}\r\n\r\n" +
                "Experimenteller Game-Boy- und Game-Boy-Color-Emulator für Windows.\r\n\r\n" +
                "Der aktuelle Stand ist eine Alpha und noch kein stabiler Release. " +
                "ROM-gebundene Save States und begrenztes Rewind sind aktiviert; " +
                "das Link-Kabel bleibt bis zur technischen Sanierung deaktiviert.\r\n\r\n" +
                "Projektursprung: nanoboy von Frédéric Meyer (2014)\r\n" +
                "Lizenz: GNU GPL v3\r\n\r\n" +
                "Nintendo und Game Boy sind Marken ihrer jeweiligen Rechteinhaber. " +
                "AetherBoy ist nicht mit Nintendo verbunden.";

            ConfigureAetherLayout();
            AetherDialog.Apply(
                this,
                "IDENTITY // 01",
                "Projekt, Herkunft und aktueller Entwicklungsstatus");
            textBox1.BorderStyle = BorderStyle.None;
            textBox1.BackColor = AetherColors.Void;
            button1.Focus();
        }

        private void ConfigureAetherLayout()
        {
            ClientSize = new Size(760, 410);
            MinimumSize = Size;
            MaximumSize = Size;

            pictureBox1.Location = new Point(28, 38);
            pictureBox1.Size = new Size(238, 238);

            var versionLabel = new Label
            {
                Name = "lblAetherVersion",
                AutoSize = false,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = AetherColors.Cyan,
                Location = new Point(304, 30),
                Size = new Size(420, 22),
                Tag = "accent",
                Text = $"BUILD {ProductInfo.Version.ToUpperInvariant()}  //  {ProductInfo.Status.ToUpperInvariant()}"
            };
            Controls.Add(versionLabel);

            textBox1.Location = new Point(304, 64);
            textBox1.Size = new Size(428, 250);
            textBox1.Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
            textBox1.TabStop = false;

            button1.Location = new Point(604, 344);
            button1.Size = new Size(128, 40);
            button1.Text = "VERSTANDEN";
            if (button1 is AetherButton aetherButton)
            {
                aetherButton.Kind = AetherButtonKind.Primary;
            }

            AcceptButton = button1;
            CancelButton = button1;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
