using System;
using System.Windows.Forms;

namespace nanoboy
{
    public partial class frmAbout : Form
    {
        public frmAbout()
        {
            InitializeComponent();
            Text = $"Über {ProductInfo.Name}";
            textBox1.Text =
                $"{ProductInfo.DisplayName}\r\n\r\n" +
                "Experimenteller Game-Boy- und Game-Boy-Color-Emulator für Windows.\r\n\r\n" +
                "Der aktuelle Stand ist eine Alpha und noch kein stabiler Release. " +
                "Save States, Rewind und Link-Kabel sind bis zur technischen Sanierung deaktiviert.\r\n\r\n" +
                "Projektursprung: nanoboy von Frédéric Meyer (2014)\r\n" +
                "Lizenz: GNU GPL v3\r\n\r\n" +
                "Nintendo und Game Boy sind Marken ihrer jeweiligen Rechteinhaber. " +
                "AetherBoy ist nicht mit Nintendo verbunden.";

            DarkTheme.Apply(this);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Dispose();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
