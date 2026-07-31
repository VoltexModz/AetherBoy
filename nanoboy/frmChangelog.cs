using System;
using System.IO;
using System.Windows.Forms;

namespace nanoboy
{
    public partial class frmChangelog : Form
    {
        public frmChangelog()
        {
            InitializeComponent();
            LoadChangelogText();
            DarkTheme.Apply(this);
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
