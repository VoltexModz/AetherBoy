namespace nanoboy
{
    partial class frmLink
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.lblStatus = new System.Windows.Forms.Label();
            this.grpHost = new System.Windows.Forms.GroupBox();
            this.btnHost = new System.Windows.Forms.Button();
            this.lblHostDesc = new System.Windows.Forms.Label();
            this.grpClient = new System.Windows.Forms.GroupBox();
            this.txtIP = new System.Windows.Forms.TextBox();
            this.lblIP = new System.Windows.Forms.Label();
            this.btnConnect = new System.Windows.Forms.Button();
            this.btnDisconnect = new System.Windows.Forms.Button();
            this.grpHost.SuspendLayout();
            this.grpClient.SuspendLayout();
            this.SuspendLayout();
            //
            // lblStatus
            //
            this.lblStatus.AutoSize = true;
            this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblStatus.Location = new System.Drawing.Point(12, 9);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(155, 17);
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Text = "Status: Nicht verbunden";
            //
            // grpHost
            //
            this.grpHost.Controls.Add(this.btnHost);
            this.grpHost.Controls.Add(this.lblHostDesc);
            this.grpHost.Location = new System.Drawing.Point(12, 35);
            this.grpHost.Name = "grpHost";
            this.grpHost.Size = new System.Drawing.Size(360, 75);
            this.grpHost.TabIndex = 1;
            this.grpHost.TabStop = false;
            this.grpHost.Text = "Option 1: Als Host (Server) starten";
            //
            // btnHost
            //
            this.btnHost.Location = new System.Drawing.Point(240, 30);
            this.btnHost.Name = "btnHost";
            this.btnHost.Size = new System.Drawing.Size(110, 25);
            this.btnHost.TabIndex = 1;
            this.btnHost.Text = "Host Starten";
            this.btnHost.UseVisualStyleBackColor = true;
            this.btnHost.Click += new System.EventHandler(this.btnHost_Click);
            //
            // lblHostDesc
            //
            this.lblHostDesc.AutoSize = true;
            this.lblHostDesc.Location = new System.Drawing.Point(10, 35);
            this.lblHostDesc.Name = "lblHostDesc";
            this.lblHostDesc.Size = new System.Drawing.Size(188, 15);
            this.lblHostDesc.TabIndex = 0;
            this.lblHostDesc.Text = "Öffnet Server auf Port 8765 für LAN";
            //
            // grpClient
            //
            this.grpClient.Controls.Add(this.txtIP);
            this.grpClient.Controls.Add(this.lblIP);
            this.grpClient.Controls.Add(this.btnConnect);
            this.grpClient.Location = new System.Drawing.Point(12, 120);
            this.grpClient.Name = "grpClient";
            this.grpClient.Size = new System.Drawing.Size(360, 80);
            this.grpClient.TabIndex = 2;
            this.grpClient.TabStop = false;
            this.grpClient.Text = "Option 2: Mit Mitspieler verbinden (Client)";
            //
            // txtIP
            //
            this.txtIP.Location = new System.Drawing.Point(85, 33);
            this.txtIP.Name = "txtIP";
            this.txtIP.Size = new System.Drawing.Size(145, 23);
            this.txtIP.TabIndex = 2;
            this.txtIP.Text = "127.0.0.1";
            //
            // lblIP
            //
            this.lblIP.AutoSize = true;
            this.lblIP.Location = new System.Drawing.Point(10, 36);
            this.lblIP.Name = "lblIP";
            this.lblIP.Size = new System.Drawing.Size(65, 15);
            this.lblIP.TabIndex = 1;
            this.lblIP.Text = "IP-Adresse:";
            //
            // btnConnect
            //
            this.btnConnect.Location = new System.Drawing.Point(240, 32);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(110, 25);
            this.btnConnect.TabIndex = 0;
            this.btnConnect.Text = "Verbinden";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            //
            // btnDisconnect
            //
            this.btnDisconnect.Location = new System.Drawing.Point(252, 210);
            this.btnDisconnect.Name = "btnDisconnect";
            this.btnDisconnect.Size = new System.Drawing.Size(120, 25);
            this.btnDisconnect.TabIndex = 3;
            this.btnDisconnect.Text = "Trennen";
            this.btnDisconnect.UseVisualStyleBackColor = true;
            this.btnDisconnect.Click += new System.EventHandler(this.btnDisconnect_Click);
            //
            // frmLink
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(384, 245);
            this.Controls.Add(this.btnDisconnect);
            this.Controls.Add(this.grpClient);
            this.Controls.Add(this.grpHost);
            this.Controls.Add(this.lblStatus);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "frmLink";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Link-Kabel (experimentell/deaktiviert) - AetherBoy";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmLink_FormClosing);
            this.grpHost.ResumeLayout(false);
            this.grpHost.PerformLayout();
            this.grpClient.ResumeLayout(false);
            this.grpClient.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.GroupBox grpHost;
        private System.Windows.Forms.Button btnHost;
        private System.Windows.Forms.Label lblHostDesc;
        private System.Windows.Forms.GroupBox grpClient;
        private System.Windows.Forms.TextBox txtIP;
        private System.Windows.Forms.Label lblIP;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Button btnDisconnect;
    }
}
