using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace nanoboy
{
    public partial class frmControls : Form
    {
        private NanoboySettings settings;

        public frmControls(NanoboySettings settings)
        {
            InitializeComponent();
            this.settings = settings;
            Text = $"Steuerung – {ProductInfo.DisplayName}";
            DarkTheme.Apply(this);
        }

        private void frmControls_Load(object sender, EventArgs e)
        {
            txtKeyA.Text = settings.KeyA.ToString();
            txtKeyB.Text = settings.KeyB.ToString();
            txtKeyStart.Text = settings.KeyStart.ToString();
            txtKeySelect.Text = settings.KeySelect.ToString();
            txtKeyUp.Text = settings.KeyUp.ToString();
            txtKeyDown.Text = settings.KeyDown.ToString();
            txtKeyLeft.Text = settings.KeyLeft.ToString();
            txtKeyRight.Text = settings.KeyRight.ToString();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void txtKeyA_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyA = e.KeyCode;
            txtKeyA.Text = e.KeyCode.ToString();
        }

        private void txtKeyB_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyB = e.KeyCode;
            txtKeyB.Text = e.KeyCode.ToString();
        }

        private void txtKeyStart_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyStart = e.KeyCode;
            txtKeyStart.Text = e.KeyCode.ToString();
        }

        private void txtKeySelect_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeySelect = e.KeyCode;
            txtKeySelect.Text = e.KeyCode.ToString();
        }

        private void txtKeyUp_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyUp = e.KeyCode;
            txtKeyUp.Text = e.KeyCode.ToString();
        }

        private void txtKeyDown_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyDown = e.KeyCode;
            txtKeyDown.Text = e.KeyCode.ToString();
        }

        private void txtKeyLeft_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyLeft = e.KeyCode;
            txtKeyLeft.Text = e.KeyCode.ToString();
        }

        private void txtKeyRight_KeyUp(object sender, KeyEventArgs e)
        {
            settings.KeyRight = e.KeyCode;
            txtKeyRight.Text = e.KeyCode.ToString();
        }
    }
}
