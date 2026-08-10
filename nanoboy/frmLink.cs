using System;
using System.Windows.Forms;
using nanoboy.Core;
using nanoboy.Controls;

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
            ConfigureAetherLayout();
            AetherDialog.Apply(
                this,
                "LINK LAB // 05",
                "Konzeptoberfläche für eine zukünftige deterministische Verbindung");
        }

        private void ConfigureAetherLayout()
        {
            ClientSize = new System.Drawing.Size(700, 420);
            MinimumSize = Size;
            MaximumSize = Size;

            lblStatus.Location = new System.Drawing.Point(26, 24);
            lblStatus.Size = new System.Drawing.Size(648, 24);
            lblStatus.Text = "OFFLINE  //  LINK-TRANSPORT NICHT FREIGEGEBEN";
            lblStatus.ForeColor = AetherColors.Danger;
            lblStatus.Tag = "danger";

            var warning = new Label
            {
                Name = "lblLinkWarning",
                AutoSize = false,
                Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Regular),
                ForeColor = AetherColors.Muted,
                Location = new System.Drawing.Point(26, 58),
                Size = new System.Drawing.Size(648, 48),
                Text = "Die Oberfläche bleibt als Zukunftsentwurf sichtbar. Netzwerkaktionen sind gesperrt, bis Serial-Timing, Synchronisation und Fehlerfälle verifiziert sind."
            };
            Controls.Add(warning);

            grpHost.Location = new System.Drawing.Point(24, 126);
            grpHost.Size = new System.Drawing.Size(316, 206);
            grpHost.Text = "HOST // PORT 8765";
            lblHostDesc.AutoSize = false;
            lblHostDesc.Location = new System.Drawing.Point(22, 44);
            lblHostDesc.Size = new System.Drawing.Size(268, 42);
            lblHostDesc.Text = "Lokale Sitzung öffnen und auf einen zweiten Emulator warten.";
            btnHost.Location = new System.Drawing.Point(22, 132);
            btnHost.Size = new System.Drawing.Size(270, 42);
            btnHost.Text = "HOST STARTEN";

            grpClient.Location = new System.Drawing.Point(360, 126);
            grpClient.Size = new System.Drawing.Size(316, 206);
            grpClient.Text = "CLIENT // DIRECT IP";
            lblIP.Location = new System.Drawing.Point(22, 42);
            lblIP.Text = "IP-ADRESSE";
            txtIP.Location = new System.Drawing.Point(22, 66);
            txtIP.Size = new System.Drawing.Size(270, 30);
            btnConnect.Location = new System.Drawing.Point(22, 132);
            btnConnect.Size = new System.Drawing.Size(270, 42);
            btnConnect.Text = "VERBINDEN";

            btnDisconnect.Location = new System.Drawing.Point(502, 356);
            btnDisconnect.Size = new System.Drawing.Size(174, 40);
            btnDisconnect.Text = "TRENNEN";
            if (btnDisconnect is AetherButton disconnectButton)
            {
                disconnectButton.Kind = AetherButtonKind.Danger;
            }

            grpHost.Enabled = false;
            grpClient.Enabled = false;
            btnDisconnect.Enabled = false;
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
