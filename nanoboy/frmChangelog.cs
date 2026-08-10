using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using nanoboy.Controls;

namespace nanoboy
{
    public partial class frmChangelog : Form
    {
        public frmChangelog()
        {
            InitializeComponent();
            Branding.AppBrand.ApplyIcon(this);
            LoadChangelogText();
            ConfigureAetherLayout();
            AetherDialog.Apply(
                this,
                "PROJECT LOG // 02",
                "Release-Historie, technische Meilensteine und nächste Schritte");
            txtChangelog.SelectionStart = 0;
            txtChangelog.SelectionLength = 0;
            txtChangelog.TabStop = false;
            btnClose.Focus();
        }

        private void ConfigureAetherLayout()
        {
            ClientSize = new Size(920, 600);
            MinimumSize = new Size(720, 540);

            txtChangelog.Location = new Point(24, 24);
            txtChangelog.Size = new Size(872, 502);
            txtChangelog.Font = new Font("Cascadia Mono", 9.5f, FontStyle.Regular, GraphicsUnit.Point);

            var sourceLabel = new Label
            {
                Name = "lblChangelogSource",
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
                AutoSize = false,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = AetherColors.Muted,
                Location = new Point(24, 552),
                Size = new Size(420, 20),
                Text = "READ ONLY  //  CHANGELOG.MD"
            };
            Controls.Add(sourceLabel);

            btnClose.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            btnClose.Location = new Point(768, 542);
            btnClose.Size = new Size(128, 40);
            btnClose.Text = "SCHLIESSEN";
            if (btnClose is AetherButton aetherButton)
            {
                aetherButton.Kind = AetherButtonKind.Secondary;
            }

            CancelButton = btnClose;
        }

        private void LoadChangelogText()
        {
            Text = $"Changelog – {ProductInfo.DisplayName}";
            string path = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");

            try
            {
                txtChangelog.Text = File.Exists(path)
                    ? File.ReadAllText(path)
                    : $"{ProductInfo.DisplayName}\r\n\r\nDas vollständige CHANGELOG.md wurde nicht mitkopiert.";
            }
            catch (IOException ex)
            {
                txtChangelog.Text = $"Changelog konnte nicht gelesen werden:\r\n{ex.Message}";
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
