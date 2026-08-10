namespace nanoboy
{
    partial class frmCheats
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
            this.lstCheats = new System.Windows.Forms.ListView();
            this.colStatus = new System.Windows.Forms.ColumnHeader();
            this.colName = new System.Windows.Forms.ColumnHeader();
            this.colCode = new System.Windows.Forms.ColumnHeader();
            this.colType = new System.Windows.Forms.ColumnHeader();
            this.lblName = new System.Windows.Forms.Label();
            this.txtName = new System.Windows.Forms.TextBox();
            this.lblCode = new System.Windows.Forms.Label();
            this.txtCode = new System.Windows.Forms.TextBox();
            this.btnAdd = new nanoboy.Controls.AetherButton();
            this.btnToggle = new nanoboy.Controls.AetherButton();
            this.btnRemove = new nanoboy.Controls.AetherButton();
            this.lblExperimentalInfo = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // lstCheats
            //
            this.lstCheats.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colStatus,
            this.colName,
            this.colCode,
            this.colType});
            this.lstCheats.FullRowSelect = true;
            this.lstCheats.HideSelection = false;
            this.lstCheats.Location = new System.Drawing.Point(12, 12);
            this.lstCheats.MultiSelect = false;
            this.lstCheats.Name = "lstCheats";
            this.lstCheats.Size = new System.Drawing.Size(460, 220);
            this.lstCheats.TabIndex = 0;
            this.lstCheats.UseCompatibleStateImageBehavior = false;
            this.lstCheats.View = System.Windows.Forms.View.Details;
            //
            // colStatus
            //
            this.colStatus.Text = "Status";
            this.colStatus.Width = 60;
            //
            // colName
            //
            this.colName.Text = "Name / Beschreibung";
            this.colName.Width = 160;
            //
            // colCode
            //
            this.colCode.Text = "Cheat Code";
            this.colCode.Width = 120;
            //
            // colType
            //
            this.colType.Text = "Typ";
            this.colType.Width = 100;
            //
            // lblName
            //
            this.lblName.AutoSize = true;
            this.lblName.Location = new System.Drawing.Point(12, 245);
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(42, 15);
            this.lblName.TabIndex = 1;
            this.lblName.Text = "Name:";
            //
            // txtName
            //
            this.txtName.Location = new System.Drawing.Point(60, 242);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(150, 23);
            this.txtName.TabIndex = 2;
            //
            // lblCode
            //
            this.lblCode.AutoSize = true;
            this.lblCode.Location = new System.Drawing.Point(220, 245);
            this.lblCode.Name = "lblCode";
            this.lblCode.Size = new System.Drawing.Size(38, 15);
            this.lblCode.TabIndex = 3;
            this.lblCode.Text = "Code:";
            //
            // txtCode
            //
            this.txtCode.Location = new System.Drawing.Point(260, 242);
            this.txtCode.Name = "txtCode";
            this.txtCode.Size = new System.Drawing.Size(120, 23);
            this.txtCode.TabIndex = 4;
            //
            // btnAdd
            //
            this.btnAdd.Location = new System.Drawing.Point(390, 241);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(82, 25);
            this.btnAdd.TabIndex = 5;
            this.btnAdd.Text = "+ GameShark";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            //
            // btnToggle
            //
            this.btnToggle.Location = new System.Drawing.Point(12, 304);
            this.btnToggle.Name = "btnToggle";
            this.btnToggle.Size = new System.Drawing.Size(100, 25);
            this.btnToggle.TabIndex = 6;
            this.btnToggle.Text = "An / Aus";
            this.btnToggle.UseVisualStyleBackColor = true;
            this.btnToggle.Click += new System.EventHandler(this.btnToggle_Click);
            //
            // btnRemove
            //
            this.btnRemove.Location = new System.Drawing.Point(118, 304);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(100, 25);
            this.btnRemove.TabIndex = 7;
            this.btnRemove.Text = "Löschen";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            //
            // lblExperimentalInfo
            //
            this.lblExperimentalInfo.AutoSize = true;
            this.lblExperimentalInfo.Location = new System.Drawing.Point(12, 278);
            this.lblExperimentalInfo.Name = "lblExperimentalInfo";
            this.lblExperimentalInfo.Size = new System.Drawing.Size(389, 15);
            this.lblExperimentalInfo.TabIndex = 8;
            this.lblExperimentalInfo.Text = "Experimentell: nur GameShark-RAM-Codes im Format 01XXYYZZ.";
            //
            // frmCheats
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(484, 341);
            this.Controls.Add(this.lblExperimentalInfo);
            this.Controls.Add(this.btnRemove);
            this.Controls.Add(this.btnToggle);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.txtCode);
            this.Controls.Add(this.lblCode);
            this.Controls.Add(this.txtName);
            this.Controls.Add(this.lblName);
            this.Controls.Add(this.lstCheats);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "frmCheats";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "GameShark Cheat Manager (experimentell) - AetherBoy";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView lstCheats;
        private System.Windows.Forms.ColumnHeader colStatus;
        private System.Windows.Forms.ColumnHeader colName;
        private System.Windows.Forms.ColumnHeader colCode;
        private System.Windows.Forms.ColumnHeader colType;
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.TextBox txtName;
        private System.Windows.Forms.Label lblCode;
        private System.Windows.Forms.TextBox txtCode;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Button btnToggle;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Label lblExperimentalInfo;
    }
}
