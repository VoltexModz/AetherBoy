using System;
using System.ComponentModel;
using System.Diagnostics;
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
            Text = global::AetherBoy.Runtime.Localization.UiText.Format("Über {0}", ProductInfo.Name);
            textBox1.Text =
                $"{ProductInfo.DisplayName}\r\n\r\n" +
                global::AetherBoy.Runtime.Localization.UiText.Get("Game Boy, Game Boy Color und experimentell Game Boy Advance.\r\n\r\n") +
                global::AetherBoy.Runtime.Localization.UiText.Format("Entwickelt und weitergeführt von {0}.\r\n", ProductInfo.TeamName) +
                global::AetherBoy.Runtime.Localization.UiText.Get("Ein gemeinsames Projekt für Windows und Linux / Wayland.\r\n\r\n") +
                global::AetherBoy.Runtime.Localization.UiText.Get("Herkunft: nanoboy von Frédéric Meyer (2014), später ChiiBoy Color.\r\n") +
                global::AetherBoy.Runtime.Localization.UiText.Get("GBA-Basis: GBADotnet von David Tyler, als gepflegter Fork integriert.\r\n") +
                global::AetherBoy.Runtime.Localization.UiText.Get("Lizenz: GPL-3.0-only; Drittanbieter mit eigenen Lizenzhinweisen.\r\n\r\n") +
                global::AetherBoy.Runtime.Localization.UiText.Get("Alpha-Software: GBA-Spielkompatibilität wird weiter erprobt.\r\n") +
                global::AetherBoy.Runtime.Localization.UiText.Get("Nintendo und Game Boy sind Marken ihrer jeweiligen Rechteinhaber. ") +
                global::AetherBoy.Runtime.Localization.UiText.Get("AetherBoy ist nicht mit Nintendo verbunden.");

            ConfigureAetherLayout();
            AetherDialog.Apply(
                this,
                global::AetherBoy.Runtime.Localization.UiText.Get("IDENTITY // 01"),
                global::AetherBoy.Runtime.Localization.UiText.Format("{0} · Projekt, Mitwirkende und Herkunft", ProductInfo.TeamName));
            textBox1.BackColor = AetherColors.Void;
            button1.Focus();
        }

        private void ConfigureAetherLayout()
        {
            ClientSize = new Size(800, 480);
            MinimumSize = Size;
            MaximumSize = Size;

            pictureBox1.Location = new Point(28, 38);
            pictureBox1.Size = new Size(238, 238);

            Controls.Add(new Label
            {
                Name = "aboutTeamLabel",
                AutoSize = false,
                Text = ProductInfo.TeamName,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = AetherColors.Cyan,
                Tag = "accent",
                Location = new Point(20, 284),
                Size = new Size(260, 40)
            });
            Controls.Add(new Label
            {
                Name = "aboutTeamCaption",
                AutoSize = false,
                Text = global::AetherBoy.Runtime.Localization.UiText.Get("Das Team hinter AetherBoy"),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = AetherColors.Muted,
                Location = new Point(20, 328),
                Size = new Size(260, 30)
            });

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
            textBox1.Size = new Size(468, 328);
            textBox1.Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
            textBox1.TabStop = false;
            textBox1.ScrollBars = ScrollBars.Vertical;

            var repositoryButton = new AetherButton
            {
                Name = "aboutRepositoryButton",
                Text = global::AetherBoy.Runtime.Localization.UiText.Get("PROJEKT AUF GITHUB"),
                Location = new Point(28, 418),
                Size = new Size(238, 40)
            };
            repositoryButton.Click += (_, _) => OpenExternalLink(new Uri(ProductInfo.RepositoryUrl));
            Controls.Add(repositoryButton);

            var coffeeButton = new AetherButton
            {
                Name = "aboutCoffeeButton",
                Text = global::AetherBoy.Runtime.Localization.UiText.Get("Spendiert uns einen Kaffee"),
                Kind = AetherButtonKind.Primary,
                Location = new Point(304, 418),
                Size = new Size(238, 40),
                AccessibleDescription = ProductInfo.SupportUri is null
                    ? global::AetherBoy.Runtime.Localization.UiText.Get("Kommt bald. Der Support-Link ist noch nicht hinterlegt.")
                    : global::AetherBoy.Runtime.Localization.UiText.Format("Unterstütze {0}.", ProductInfo.TeamName)
            };
            coffeeButton.Click += (_, _) => ShowCoffeeSupport();
            Controls.Add(coffeeButton);
            Controls.Add(new Label
            {
                Name = "aboutCoffeeStatus",
                Text = global::AetherBoy.Runtime.Localization.UiText.Get("KOMMT BALD · Support-Link folgt"),
                Visible = ProductInfo.SupportUri is null,
                AutoEllipsis = true,
                Font = new Font("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = AetherColors.Muted,
                Location = new Point(304, 394),
                Size = new Size(318, 20)
            });

            button1.Location = new Point(644, 418);
            button1.Size = new Size(128, 40);
            button1.Text = global::AetherBoy.Runtime.Localization.UiText.Get("VERSTANDEN");
            if (button1 is AetherButton aetherButton)
            {
                aetherButton.Kind = AetherButtonKind.Primary;
            }

            AcceptButton = button1;
            CancelButton = button1;
        }

        private void ShowCoffeeSupport()
        {
            if (ProductInfo.SupportUri is Uri supportUri)
            {
                OpenExternalLink(supportUri);
                return;
            }

            AetherSignal.Show(this,
                global::AetherBoy.Runtime.Localization.UiText.Format("Danke, dass du {0} unterstützen möchtest!\n\n", ProductInfo.TeamName) +
                global::AetherBoy.Runtime.Localization.UiText.Get("Der Coffee-Link ist in dieser Entwicklungsversion noch nicht hinterlegt. ") +
                global::AetherBoy.Runtime.Localization.UiText.Get("Er wird mit einem späteren Update ergänzt."),
                global::AetherBoy.Runtime.Localization.UiText.Get("Coffee kommt bald"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OpenExternalLink(Uri uri)
        {
            try
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
            {
                AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.Format("Der Browser konnte nicht geöffnet werden.\n\n{0}", uri.AbsoluteUri),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Link öffnen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
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
