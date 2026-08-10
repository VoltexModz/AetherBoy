using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using nanoboy.Controls;

namespace nanoboy
{
    public partial class frmControls : Form
    {
        private NanoboySettings settings;

        public frmControls(NanoboySettings settings)
        {
            InitializeComponent();
            Branding.AppBrand.ApplyIcon(this);
            this.settings = settings;
            Text = $"Steuerung – {ProductInfo.DisplayName}";
            ConfigureAetherLayout();
            AetherDialog.Apply(
                this,
                "INPUT MATRIX // 03",
                "Feld auswählen und anschließend die gewünschte Taste drücken");
        }

        private void ConfigureAetherLayout()
        {
            ClientSize = new Size(720, 430);
            MinimumSize = Size;
            MaximumSize = Size;

            var hint = new Label
            {
                Name = "lblControlsHint",
                AutoSize = false,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(28, 24),
                Size = new Size(664, 44),
                Text = "Jede Belegung wird direkt gespeichert. Doppelte Tasten sind möglich, aber für saubere Eingabe nicht empfohlen."
            };
            Controls.Add(hint);

            ArrangeBinding(label1, txtKeyA, "A BUTTON", 28, 88);
            ArrangeBinding(label2, txtKeyB, "B BUTTON", 370, 88);
            ArrangeBinding(label4, txtKeyStart, "START", 28, 158);
            ArrangeBinding(label3, txtKeySelect, "SELECT", 370, 158);
            ArrangeBinding(label6, txtKeyUp, "DPAD UP", 28, 228);
            ArrangeBinding(label5, txtKeyDown, "DPAD DOWN", 370, 228);
            ArrangeBinding(label8, txtKeyLeft, "DPAD LEFT", 28, 298);
            ArrangeBinding(label7, txtKeyRight, "DPAD RIGHT", 370, 298);

            button1.Location = new Point(564, 374);
            button1.Size = new Size(128, 40);
            button1.Text = "FERTIG";
            if (button1 is AetherButton aetherButton)
            {
                aetherButton.Kind = AetherButtonKind.Primary;
            }

            AcceptButton = button1;
            CancelButton = button1;
            StartPosition = FormStartPosition.CenterParent;
        }

        private static void ArrangeBinding(
            Label label,
            TextBox input,
            string caption,
            int x,
            int y)
        {
            label.AutoSize = false;
            label.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold, GraphicsUnit.Point);
            label.Location = new Point(x, y);
            label.Size = new Size(120, 20);
            label.Text = caption;

            input.Location = new Point(x + 128, y - 8);
            input.Size = new Size(164, 34);
            input.Font = new Font("Cascadia Mono", 10f, FontStyle.Bold, GraphicsUnit.Point);
            input.TextAlign = HorizontalAlignment.Center;
            input.Cursor = Cursors.Hand;
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
