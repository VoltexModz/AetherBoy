using System;
using System.Windows.Forms;
using nanoboy.Core;

namespace nanoboy
{
    public partial class frmLink : Form
    {
        private LinkCable linkCable;

        public frmLink(LinkCable cable)
        {
            InitializeComponent();
            Branding.AppBrand.ApplyIcon(this);
            Text = $"Link-Kabel (deaktiviert) – {ProductInfo.DisplayName}";
            linkCable = cable;

            if (linkCable != null)
            {
                linkCable.Connected += OnConnected;
                linkCable.Disconnected += OnDisconnected;
            }

            UpdateStatus();
            DarkTheme.Apply(this);
        }

        private void OnConnected()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(OnConnected));
                return;
            }
            lblStatus.Text = "Status: Verbunden! 🌐 (Link-Kabel Aktiv)";
            lblStatus.ForeColor = System.Drawing.Color.LightGreen;
        }

        private void OnDisconnected()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(OnDisconnected));
                return;
            }
            lblStatus.Text = "Status: Nicht verbunden";
            lblStatus.ForeColor = System.Drawing.Color.Salmon;
        }

        private void UpdateStatus()
        {
            if (linkCable != null && linkCable.IsConnected)
            {
                OnConnected();
            }
            else
            {
                OnDisconnected();
            }
        }

        private void btnHost_Click(object sender, EventArgs e)
        {
            if (linkCable == null) return;
            linkCable.StartServer(8765);
            lblStatus.Text = "Warte auf Mitspieler (Port 8765)...";
            lblStatus.ForeColor = System.Drawing.Color.LightSkyBlue;
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (linkCable == null) return;
            string ip = txtIP.Text.Trim();
            if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";

            lblStatus.Text = $"Verbinde mit {ip}...";
            if (!linkCable.ConnectClient(ip, 8765))
            {
                MessageBox.Show("Verbindung fehlgeschlagen. Ist der Host gestartet?", "Link-Kabel", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus();
            }
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            linkCable?.Disconnect();
            UpdateStatus();
        }

        private void frmLink_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (linkCable != null)
            {
                linkCable.Connected -= OnConnected;
                linkCable.Disconnected -= OnDisconnected;
            }
        }
    }
}
