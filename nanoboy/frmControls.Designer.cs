namespace nanoboy
{
    partial class frmControls
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.txtKeyA = new System.Windows.Forms.TextBox();
            this.txtKeyB = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.txtKeySelect = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.txtKeyStart = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.txtKeyDown = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.txtKeyUp = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.txtKeyRight = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.txtKeyLeft = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.button1 = new nanoboy.Controls.AetherButton();
            this.SuspendLayout();
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(31, 26);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(17, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "A:";
            //
            // txtKeyA
            //
            this.txtKeyA.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtKeyA.Location = new System.Drawing.Point(49, 16);
            this.txtKeyA.Name = "txtKeyA";
            this.txtKeyA.ReadOnly = true;
            this.txtKeyA.Size = new System.Drawing.Size(56, 20);
            this.txtKeyA.TabIndex = 1;
            this.txtKeyA.KeyUp += new System.Windows.Forms.KeyEventHandler(this.txtKeyA_KeyUp);
            //
            // txtKeyB
            //
            this.txtKeyB.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtKeyB.Location = new System.Drawing.Point(157, 16);
            this.txtKeyB.Name = "txtKeyB";
            this.txtKeyB.ReadOnly = true;
            this.txtKeyB.Size = new System.Drawing.Size(56, 20);
            this.txtKeyB.TabIndex = 3;
            this.txtKeyB.KeyUp += new System.Windows.Forms.KeyEventHandler(this.txtKeyB_KeyUp);
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(134, 23);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(17, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "B:";
            //
            // txtKeySelect
            //
            this.txtKeySelect.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtKeySelect.Location = new System.Drawing.Point(157, 42);
            this.txtKeySelect.Name = "txtKeySelect";
            this.txtKeySelect.ReadOnly = true;
            this.txtKeySelect.Size = new System.Drawing.Size(56, 20);
            this.txtKeySelect.TabIndex = 7;
            this.txtKeySelect.KeyUp += new System.Windows.Forms.KeyEventHandler(this.txtKeySelect_KeyUp);
            //
            // label3
            //
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(111, 49);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(40, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = "Select:";
            //
            // txtKeyStart
            //
            this.txtKeyStart.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtKeyStart.Location = new System.Drawing.Point(49, 42);
            this.txtKeyStart.Name = "txtKeyStart";
            this.txtKeyStart.ReadOnly = true;
            this.txtKeyStart.Size = new System.Drawing.Size(56, 20);
            this.txtKeyStart.TabIndex = 5;
            this.txtKeyStart.KeyUp += new System.Windows.Forms.KeyEventHandler(this.txtKeyStart_KeyUp);
            //
            // label4
            //
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(16, 49);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(32, 13);
            this.label4.TabIndex = 4;
            this.label4.Text = "Start:";
            //
            // txtKeyDown
            //
            this.txtKeyDown.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtKeyDown.Location = new System.Drawing.Point(157, 69);
            this.txtKeyDown.Name = "txtKeyDown";
            this.txtKeyDown.ReadOnly = true;
            this.txtKeyDown.Size = new System.Drawing.Size(56, 20);
            this.txtKeyDown.TabIndex = 11;
            this.txtKeyDown.KeyUp += new System.Windows.Forms.KeyEventHandler(this.txtKeyDown_KeyUp);
            //
            // label5
            //
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(111, 76);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(42, 13);
            this.label5.TabIndex = 10;
            this.label5.Text = "Runter:";
            //
            // txtKeyUp
            //
            this.txtKeyUp.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtKeyUp.Location = new System.Drawing.Point(49, 69);
            this.txtKeyUp.Name = "txtKeyUp";
            this.txtKeyUp.ReadOnly = true;
            this.txtKeyUp.Size = new System.Drawing.Size(56, 20);
            this.txtKeyUp.TabIndex = 9;
            this.txtKeyUp.KeyUp += new System.Windows.Forms.KeyEventHandler(this.txtKeyUp_KeyUp);
            //
            // label6
            //
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(16, 76);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(36, 13);
            this.label6.TabIndex = 8;
            this.label6.Text = "Hoch:";
            //
            // txtKeyRight
            //
            this.txtKeyRight.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtKeyRight.Location = new System.Drawing.Point(157, 94);
            this.txtKeyRight.Name = "txtKeyRight";
            this.txtKeyRight.ReadOnly = true;
            this.txtKeyRight.Size = new System.Drawing.Size(56, 20);
            this.txtKeyRight.TabIndex = 15;
            this.txtKeyRight.KeyUp += new System.Windows.Forms.KeyEventHandler(this.txtKeyRight_KeyUp);
            //
            // label7
            //
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(111, 101);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(44, 13);
            this.label7.TabIndex = 14;
            this.label7.Text = "Rechts:";
            //
            // txtKeyLeft
            //
            this.txtKeyLeft.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtKeyLeft.Location = new System.Drawing.Point(49, 94);
            this.txtKeyLeft.Name = "txtKeyLeft";
            this.txtKeyLeft.ReadOnly = true;
            this.txtKeyLeft.Size = new System.Drawing.Size(56, 20);
            this.txtKeyLeft.TabIndex = 13;
            this.txtKeyLeft.KeyUp += new System.Windows.Forms.KeyEventHandler(this.txtKeyLeft_KeyUp);
            //
            // label8
            //
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(16, 101);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(35, 13);
            this.label8.TabIndex = 12;
            this.label8.Text = "Links:";
            //
            // button1
            //
            this.button1.Location = new System.Drawing.Point(19, 123);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 16;
            this.button1.Text = "OK";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            //
            // frmControls
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(222, 157);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.txtKeyRight);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.txtKeyLeft);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.txtKeyDown);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.txtKeyUp);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.txtKeySelect);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.txtKeyStart);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.txtKeyB);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.txtKeyA);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "frmControls";
            this.Text = "AetherBoy - Steuerung";
            this.Load += new System.EventHandler(this.frmControls_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtKeyA;
        private System.Windows.Forms.TextBox txtKeyB;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtKeySelect;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox txtKeyStart;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtKeyDown;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox txtKeyUp;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox txtKeyRight;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox txtKeyLeft;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Button button1;
    }
}
