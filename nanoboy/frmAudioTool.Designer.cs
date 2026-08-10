namespace nanoboy
{
    partial class frmAudioTool
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
            this.components = new System.ComponentModel.Container();
            this.levelDisplayControl1 = new nanoboy.Controls.LevelDisplayControl();
            this.levelDisplayControl2 = new nanoboy.Controls.LevelDisplayControl();
            this.levelDisplayControl3 = new nanoboy.Controls.LevelDisplayControl();
            this.levelDisplayControl4 = new nanoboy.Controls.LevelDisplayControl();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox1 = new nanoboy.Controls.AetherGroupBox();
            this.groupBox2 = new nanoboy.Controls.AetherGroupBox();
            this.labelQ1SoundLength = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.labelQ1EnvelDirection = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.labelQ1EnvelSweep = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.labelQ1WaveDuty = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.labelQ1SweepDirection = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.labelQ1SweepShift = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.labelQ1SweepCycles = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.labelQ1Freq = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.groupBox3 = new nanoboy.Controls.AetherGroupBox();
            this.labelQ2SoundLength = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.labelQ2EnvelDirection = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.labelQ2EnvelSweep = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.labelQ2WaveDuty = new System.Windows.Forms.Label();
            this.label17 = new System.Windows.Forms.Label();
            this.labelQ2SweepDirection = new System.Windows.Forms.Label();
            this.label19 = new System.Windows.Forms.Label();
            this.labelQ2SweepShift = new System.Windows.Forms.Label();
            this.label21 = new System.Windows.Forms.Label();
            this.labelQ2SweepCycles = new System.Windows.Forms.Label();
            this.label23 = new System.Windows.Forms.Label();
            this.labelQ2Freq = new System.Windows.Forms.Label();
            this.label25 = new System.Windows.Forms.Label();
            this.groupBox4 = new nanoboy.Controls.AetherGroupBox();
            this.labelWSoundLength = new System.Windows.Forms.Label();
            this.labelWFreq = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.waveDataControl1 = new nanoboy.Controls.WaveDataControl();
            this.groupBox5 = new nanoboy.Controls.AetherGroupBox();
            this.labelNSoundLength = new System.Windows.Forms.Label();
            this.labelNEnvelDirection = new System.Windows.Forms.Label();
            this.labelNEnvelSweep = new System.Windows.Forms.Label();
            this.labelNResultFreq = new System.Windows.Forms.Label();
            this.labelNCounter = new System.Windows.Forms.Label();
            this.labelNCounterBits = new System.Windows.Forms.Label();
            this.labelNDividingRatio = new System.Windows.Forms.Label();
            this.labelNClockFreq = new System.Windows.Forms.Label();
            this.label24 = new System.Windows.Forms.Label();
            this.label22 = new System.Windows.Forms.Label();
            this.label26 = new System.Windows.Forms.Label();
            this.label20 = new System.Windows.Forms.Label();
            this.label27 = new System.Windows.Forms.Label();
            this.label18 = new System.Windows.Forms.Label();
            this.label16 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.btnRecordWav = new nanoboy.Controls.AetherButton();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.SuspendLayout();
            //
            // timer1
            //
            this.timer1.Enabled = true;
            this.timer1.Interval = 20;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            //
            // checkBox1
            //
            this.checkBox1.AutoSize = true;
            this.checkBox1.Checked = true;
            this.checkBox1.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox1.Location = new System.Drawing.Point(12, 350);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(86, 17);
            this.checkBox1.TabIndex = 1;
            this.checkBox1.Text = "Aktualisieren";
            this.checkBox1.UseVisualStyleBackColor = true;
            this.checkBox1.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            //
            // levelDisplayControl1
            //
            this.levelDisplayControl1.Location = new System.Drawing.Point(10, 39);
            this.levelDisplayControl1.Name = "levelDisplayControl1";
            this.levelDisplayControl1.Size = new System.Drawing.Size(24, 120);
            this.levelDisplayControl1.TabIndex = 0;
            //
            // levelDisplayControl2
            //
            this.levelDisplayControl2.Location = new System.Drawing.Point(38, 39);
            this.levelDisplayControl2.Name = "levelDisplayControl2";
            this.levelDisplayControl2.Size = new System.Drawing.Size(24, 120);
            this.levelDisplayControl2.TabIndex = 1;
            //
            // levelDisplayControl3
            //
            this.levelDisplayControl3.Location = new System.Drawing.Point(66, 39);
            this.levelDisplayControl3.Name = "levelDisplayControl3";
            this.levelDisplayControl3.Size = new System.Drawing.Size(24, 120);
            this.levelDisplayControl3.TabIndex = 2;
            //
            // levelDisplayControl4
            //
            this.levelDisplayControl4.Location = new System.Drawing.Point(94, 39);
            this.levelDisplayControl4.Name = "levelDisplayControl4";
            this.levelDisplayControl4.Size = new System.Drawing.Size(24, 120);
            this.levelDisplayControl4.TabIndex = 3;
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(15, 20);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(96, 13);
            this.label1.TabIndex = 5;
            this.label1.Text = "Q1    Q2     W     N";
            //
            // groupBox1
            //
            this.groupBox1.Controls.Add(this.levelDisplayControl4);
            this.groupBox1.Controls.Add(this.levelDisplayControl3);
            this.groupBox1.Controls.Add(this.levelDisplayControl2);
            this.groupBox1.Controls.Add(this.levelDisplayControl1);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(126, 170);
            this.groupBox1.TabIndex = 6;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Kanal Lautstärke";
            //
            // groupBox2
            //
            this.groupBox2.Controls.Add(this.labelQ1SoundLength);
            this.groupBox2.Controls.Add(this.label7);
            this.groupBox2.Controls.Add(this.labelQ1EnvelDirection);
            this.groupBox2.Controls.Add(this.label5);
            this.groupBox2.Controls.Add(this.labelQ1EnvelSweep);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.labelQ1WaveDuty);
            this.groupBox2.Controls.Add(this.label10);
            this.groupBox2.Controls.Add(this.labelQ1SweepDirection);
            this.groupBox2.Controls.Add(this.label8);
            this.groupBox2.Controls.Add(this.labelQ1SweepShift);
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Controls.Add(this.labelQ1SweepCycles);
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Controls.Add(this.labelQ1Freq);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Location = new System.Drawing.Point(144, 12);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(200, 170);
            this.groupBox2.TabIndex = 7;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Quad Kanal 1 (Q1)";
            //
            // labelQ1SoundLength
            //
            this.labelQ1SoundLength.AutoSize = true;
            this.labelQ1SoundLength.Location = new System.Drawing.Point(100, 130);
            this.labelQ1SoundLength.Name = "labelQ1SoundLength";
            this.labelQ1SoundLength.Size = new System.Drawing.Size(13, 13);
            this.labelQ1SoundLength.TabIndex = 15;
            this.labelQ1SoundLength.Text = "0";
            //
            // label7
            //
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(12, 130);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(77, 13);
            this.label7.TabIndex = 14;
            this.label7.Text = "Sound Length:";
            //
            // labelQ1EnvelDirection
            //
            this.labelQ1EnvelDirection.AutoSize = true;
            this.labelQ1EnvelDirection.Location = new System.Drawing.Point(100, 113);
            this.labelQ1EnvelDirection.Name = "labelQ1EnvelDirection";
            this.labelQ1EnvelDirection.Size = new System.Drawing.Size(13, 13);
            this.labelQ1EnvelDirection.TabIndex = 13;
            this.labelQ1EnvelDirection.Text = "?";
            //
            // label5
            //
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(12, 113);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(88, 13);
            this.label5.TabIndex = 12;
            this.label5.Text = "Envel. Direction: ";
            //
            // labelQ1EnvelSweep
            //
            this.labelQ1EnvelSweep.AutoSize = true;
            this.labelQ1EnvelSweep.Location = new System.Drawing.Point(100, 95);
            this.labelQ1EnvelSweep.Name = "labelQ1EnvelSweep";
            this.labelQ1EnvelSweep.Size = new System.Drawing.Size(13, 13);
            this.labelQ1EnvelSweep.TabIndex = 11;
            this.labelQ1EnvelSweep.Text = "0";
            //
            // label3
            //
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 95);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(76, 13);
            this.label3.TabIndex = 10;
            this.label3.Text = "Envel. Sweep:";
            //
            // labelQ1WaveDuty
            //
            this.labelQ1WaveDuty.AutoSize = true;
            this.labelQ1WaveDuty.Location = new System.Drawing.Point(100, 147);
            this.labelQ1WaveDuty.Name = "labelQ1WaveDuty";
            this.labelQ1WaveDuty.Size = new System.Drawing.Size(13, 13);
            this.labelQ1WaveDuty.TabIndex = 9;
            this.labelQ1WaveDuty.Text = "?";
            //
            // label10
            //
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(12, 147);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(67, 13);
            this.label10.TabIndex = 8;
            this.label10.Text = "Wave Duty: ";
            //
            // labelQ1SweepDirection
            //
            this.labelQ1SweepDirection.AutoSize = true;
            this.labelQ1SweepDirection.Location = new System.Drawing.Point(100, 77);
            this.labelQ1SweepDirection.Name = "labelQ1SweepDirection";
            this.labelQ1SweepDirection.Size = new System.Drawing.Size(13, 13);
            this.labelQ1SweepDirection.TabIndex = 7;
            this.labelQ1SweepDirection.Text = "?";
            //
            // label8
            //
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(12, 77);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(88, 13);
            this.label8.TabIndex = 6;
            this.label8.Text = "Sweep Direction:";
            //
            // labelQ1SweepShift
            //
            this.labelQ1SweepShift.AutoSize = true;
            this.labelQ1SweepShift.Location = new System.Drawing.Point(100, 59);
            this.labelQ1SweepShift.Name = "labelQ1SweepShift";
            this.labelQ1SweepShift.Size = new System.Drawing.Size(13, 13);
            this.labelQ1SweepShift.TabIndex = 5;
            this.labelQ1SweepShift.Text = "0";
            //
            // label6
            //
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(12, 59);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(70, 13);
            this.label6.TabIndex = 4;
            this.label6.Text = "Sweep Shift: ";
            //
            // labelQ1SweepCycles
            //
            this.labelQ1SweepCycles.AutoSize = true;
            this.labelQ1SweepCycles.Location = new System.Drawing.Point(100, 40);
            this.labelQ1SweepCycles.Name = "labelQ1SweepCycles";
            this.labelQ1SweepCycles.Size = new System.Drawing.Size(13, 13);
            this.labelQ1SweepCycles.TabIndex = 3;
            this.labelQ1SweepCycles.Text = "0";
            //
            // label4
            //
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(12, 40);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(77, 13);
            this.label4.TabIndex = 2;
            this.label4.Text = "Sweep Cycles:";
            //
            // labelQ1Freq
            //
            this.labelQ1Freq.AutoSize = true;
            this.labelQ1Freq.Location = new System.Drawing.Point(100, 21);
            this.labelQ1Freq.Name = "labelQ1Freq";
            this.labelQ1Freq.Size = new System.Drawing.Size(26, 13);
            this.labelQ1Freq.TabIndex = 1;
            this.labelQ1Freq.Text = "0Hz";
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 21);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(54, 13);
            this.label2.TabIndex = 0;
            this.label2.Text = "Frequenz:";
            //
            // groupBox3
            //
            this.groupBox3.Controls.Add(this.labelQ2SoundLength);
            this.groupBox3.Controls.Add(this.label11);
            this.groupBox3.Controls.Add(this.labelQ2EnvelDirection);
            this.groupBox3.Controls.Add(this.label13);
            this.groupBox3.Controls.Add(this.labelQ2EnvelSweep);
            this.groupBox3.Controls.Add(this.label15);
            this.groupBox3.Controls.Add(this.labelQ2WaveDuty);
            this.groupBox3.Controls.Add(this.label17);
            this.groupBox3.Controls.Add(this.labelQ2SweepDirection);
            this.groupBox3.Controls.Add(this.label19);
            this.groupBox3.Controls.Add(this.labelQ2SweepShift);
            this.groupBox3.Controls.Add(this.label21);
            this.groupBox3.Controls.Add(this.labelQ2SweepCycles);
            this.groupBox3.Controls.Add(this.label23);
            this.groupBox3.Controls.Add(this.labelQ2Freq);
            this.groupBox3.Controls.Add(this.label25);
            this.groupBox3.Location = new System.Drawing.Point(350, 12);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(185, 170);
            this.groupBox3.TabIndex = 16;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Quad Kanal 2 (Q2)";
            //
            // labelQ2SoundLength
            //
            this.labelQ2SoundLength.AutoSize = true;
            this.labelQ2SoundLength.Location = new System.Drawing.Point(100, 130);
            this.labelQ2SoundLength.Name = "labelQ2SoundLength";
            this.labelQ2SoundLength.Size = new System.Drawing.Size(13, 13);
            this.labelQ2SoundLength.TabIndex = 15;
            this.labelQ2SoundLength.Text = "0";
            //
            // label11
            //
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(12, 130);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(63, 13);
            this.label11.TabIndex = 14;
            this.label11.Text = "Klanglänge:";
            //
            // labelQ2EnvelDirection
            //
            this.labelQ2EnvelDirection.AutoSize = true;
            this.labelQ2EnvelDirection.Location = new System.Drawing.Point(100, 113);
            this.labelQ2EnvelDirection.Name = "labelQ2EnvelDirection";
            this.labelQ2EnvelDirection.Size = new System.Drawing.Size(13, 13);
            this.labelQ2EnvelDirection.TabIndex = 13;
            this.labelQ2EnvelDirection.Text = "?";
            //
            // label13
            //
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(12, 113);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(88, 13);
            this.label13.TabIndex = 12;
            this.label13.Text = "Envel. Direction: ";
            //
            // labelQ2EnvelSweep
            //
            this.labelQ2EnvelSweep.AutoSize = true;
            this.labelQ2EnvelSweep.Location = new System.Drawing.Point(100, 95);
            this.labelQ2EnvelSweep.Name = "labelQ2EnvelSweep";
            this.labelQ2EnvelSweep.Size = new System.Drawing.Size(13, 13);
            this.labelQ2EnvelSweep.TabIndex = 11;
            this.labelQ2EnvelSweep.Text = "0";
            //
            // label15
            //
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(12, 95);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(76, 13);
            this.label15.TabIndex = 10;
            this.label15.Text = "Envel. Sweep:";
            //
            // labelQ2WaveDuty
            //
            this.labelQ2WaveDuty.AutoSize = true;
            this.labelQ2WaveDuty.Location = new System.Drawing.Point(100, 147);
            this.labelQ2WaveDuty.Name = "labelQ2WaveDuty";
            this.labelQ2WaveDuty.Size = new System.Drawing.Size(13, 13);
            this.labelQ2WaveDuty.TabIndex = 9;
            this.labelQ2WaveDuty.Text = "?";
            //
            // label17
            //
            this.label17.AutoSize = true;
            this.label17.Location = new System.Drawing.Point(12, 147);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(67, 13);
            this.label17.TabIndex = 8;
            this.label17.Text = "Wave Duty: ";
            //
            // labelQ2SweepDirection
            //
            this.labelQ2SweepDirection.AutoSize = true;
            this.labelQ2SweepDirection.Location = new System.Drawing.Point(100, 77);
            this.labelQ2SweepDirection.Name = "labelQ2SweepDirection";
            this.labelQ2SweepDirection.Size = new System.Drawing.Size(10, 13);
            this.labelQ2SweepDirection.TabIndex = 7;
            this.labelQ2SweepDirection.Text = "-";
            //
            // label19
            //
            this.label19.AutoSize = true;
            this.label19.Location = new System.Drawing.Point(12, 77);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(88, 13);
            this.label19.TabIndex = 6;
            this.label19.Text = "Sweep Direction:";
            //
            // labelQ2SweepShift
            //
            this.labelQ2SweepShift.AutoSize = true;
            this.labelQ2SweepShift.Location = new System.Drawing.Point(100, 59);
            this.labelQ2SweepShift.Name = "labelQ2SweepShift";
            this.labelQ2SweepShift.Size = new System.Drawing.Size(10, 13);
            this.labelQ2SweepShift.TabIndex = 5;
            this.labelQ2SweepShift.Text = "-";
            //
            // label21
            //
            this.label21.AutoSize = true;
            this.label21.Location = new System.Drawing.Point(12, 59);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(70, 13);
            this.label21.TabIndex = 4;
            this.label21.Text = "Sweep Shift: ";
            //
            // labelQ2SweepCycles
            //
            this.labelQ2SweepCycles.AutoSize = true;
            this.labelQ2SweepCycles.Location = new System.Drawing.Point(100, 40);
            this.labelQ2SweepCycles.Name = "labelQ2SweepCycles";
            this.labelQ2SweepCycles.Size = new System.Drawing.Size(10, 13);
            this.labelQ2SweepCycles.TabIndex = 3;
            this.labelQ2SweepCycles.Text = "-";
            //
            // label23
            //
            this.label23.AutoSize = true;
            this.label23.Location = new System.Drawing.Point(12, 40);
            this.label23.Name = "label23";
            this.label23.Size = new System.Drawing.Size(77, 13);
            this.label23.TabIndex = 2;
            this.label23.Text = "Sweep Cycles:";
            //
            // labelQ2Freq
            //
            this.labelQ2Freq.AutoSize = true;
            this.labelQ2Freq.Location = new System.Drawing.Point(100, 21);
            this.labelQ2Freq.Name = "labelQ2Freq";
            this.labelQ2Freq.Size = new System.Drawing.Size(26, 13);
            this.labelQ2Freq.TabIndex = 1;
            this.labelQ2Freq.Text = "0Hz";
            //
            // label25
            //
            this.label25.AutoSize = true;
            this.label25.Location = new System.Drawing.Point(12, 21);
            this.label25.Name = "label25";
            this.label25.Size = new System.Drawing.Size(54, 13);
            this.label25.TabIndex = 0;
            this.label25.Text = "Frequenz:";
            //
            // groupBox4
            //
            this.groupBox4.Controls.Add(this.waveDataControl1);
            this.groupBox4.Controls.Add(this.labelWSoundLength);
            this.groupBox4.Controls.Add(this.labelWFreq);
            this.groupBox4.Controls.Add(this.label12);
            this.groupBox4.Controls.Add(this.label9);
            this.groupBox4.Location = new System.Drawing.Point(12, 188);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(332, 156);
            this.groupBox4.TabIndex = 17;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Wellen Kanal (W)";
            //
            // labelWSoundLength
            //
            this.labelWSoundLength.AutoSize = true;
            this.labelWSoundLength.Location = new System.Drawing.Point(85, 40);
            this.labelWSoundLength.Name = "labelWSoundLength";
            this.labelWSoundLength.Size = new System.Drawing.Size(13, 13);
            this.labelWSoundLength.TabIndex = 4;
            this.labelWSoundLength.Text = "0";
            //
            // labelWFreq
            //
            this.labelWFreq.AutoSize = true;
            this.labelWFreq.Location = new System.Drawing.Point(85, 21);
            this.labelWFreq.Name = "labelWFreq";
            this.labelWFreq.Size = new System.Drawing.Size(26, 13);
            this.labelWFreq.TabIndex = 3;
            this.labelWFreq.Text = "0Hz";
            //
            // label12
            //
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(7, 40);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(63, 13);
            this.label12.TabIndex = 2;
            this.label12.Text = "Klanglänge:";
            //
            // label9
            //
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(7, 21);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(54, 13);
            this.label9.TabIndex = 1;
            this.label9.Text = "Frequenz:";
            //
            // waveDataControl1
            //
            this.waveDataControl1.Location = new System.Drawing.Point(6, 60);
            this.waveDataControl1.Name = "waveDataControl1";
            this.waveDataControl1.Size = new System.Drawing.Size(320, 88);
            this.waveDataControl1.TabIndex = 5;
            //
            // groupBox5
            //
            this.groupBox5.Controls.Add(this.labelNSoundLength);
            this.groupBox5.Controls.Add(this.labelNEnvelDirection);
            this.groupBox5.Controls.Add(this.labelNEnvelSweep);
            this.groupBox5.Controls.Add(this.labelNResultFreq);
            this.groupBox5.Controls.Add(this.labelNCounter);
            this.groupBox5.Controls.Add(this.labelNCounterBits);
            this.groupBox5.Controls.Add(this.labelNDividingRatio);
            this.groupBox5.Controls.Add(this.labelNClockFreq);
            this.groupBox5.Controls.Add(this.label24);
            this.groupBox5.Controls.Add(this.label22);
            this.groupBox5.Controls.Add(this.label26);
            this.groupBox5.Controls.Add(this.label20);
            this.groupBox5.Controls.Add(this.label27);
            this.groupBox5.Controls.Add(this.label18);
            this.groupBox5.Controls.Add(this.label16);
            this.groupBox5.Controls.Add(this.label14);
            this.groupBox5.Location = new System.Drawing.Point(350, 188);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(185, 156);
            this.groupBox5.TabIndex = 18;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Geräuschkanal (N)";
            //
            // labelNSoundLength
            //
            this.labelNSoundLength.AutoSize = true;
            this.labelNSoundLength.Location = new System.Drawing.Point(111, 136);
            this.labelNSoundLength.Name = "labelNSoundLength";
            this.labelNSoundLength.Size = new System.Drawing.Size(13, 13);
            this.labelNSoundLength.TabIndex = 26;
            this.labelNSoundLength.Text = "0";
            //
            // labelNEnvelDirection
            //
            this.labelNEnvelDirection.AutoSize = true;
            this.labelNEnvelDirection.Location = new System.Drawing.Point(111, 120);
            this.labelNEnvelDirection.Name = "labelNEnvelDirection";
            this.labelNEnvelDirection.Size = new System.Drawing.Size(13, 13);
            this.labelNEnvelDirection.TabIndex = 25;
            this.labelNEnvelDirection.Text = "?";
            //
            // labelNEnvelSweep
            //
            this.labelNEnvelSweep.AutoSize = true;
            this.labelNEnvelSweep.Location = new System.Drawing.Point(111, 102);
            this.labelNEnvelSweep.Name = "labelNEnvelSweep";
            this.labelNEnvelSweep.Size = new System.Drawing.Size(13, 13);
            this.labelNEnvelSweep.TabIndex = 24;
            this.labelNEnvelSweep.Text = "0";
            //
            // labelNResultFreq
            //
            this.labelNResultFreq.AutoSize = true;
            this.labelNResultFreq.Location = new System.Drawing.Point(111, 86);
            this.labelNResultFreq.Name = "labelNResultFreq";
            this.labelNResultFreq.Size = new System.Drawing.Size(13, 13);
            this.labelNResultFreq.TabIndex = 23;
            this.labelNResultFreq.Text = "0";
            //
            // labelNCounter
            //
            this.labelNCounter.AutoSize = true;
            this.labelNCounter.Location = new System.Drawing.Point(111, 68);
            this.labelNCounter.Name = "labelNCounter";
            this.labelNCounter.Size = new System.Drawing.Size(13, 13);
            this.labelNCounter.TabIndex = 22;
            this.labelNCounter.Text = "0";
            //
            // labelNCounterBits
            //
            this.labelNCounterBits.AutoSize = true;
            this.labelNCounterBits.Location = new System.Drawing.Point(111, 51);
            this.labelNCounterBits.Name = "labelNCounterBits";
            this.labelNCounterBits.Size = new System.Drawing.Size(13, 13);
            this.labelNCounterBits.TabIndex = 21;
            this.labelNCounterBits.Text = "0";
            //
            // labelNDividingRatio
            //
            this.labelNDividingRatio.AutoSize = true;
            this.labelNDividingRatio.Location = new System.Drawing.Point(111, 34);
            this.labelNDividingRatio.Name = "labelNDividingRatio";
            this.labelNDividingRatio.Size = new System.Drawing.Size(13, 13);
            this.labelNDividingRatio.TabIndex = 20;
            this.labelNDividingRatio.Text = "0";
            //
            // labelNClockFreq
            //
            this.labelNClockFreq.AutoSize = true;
            this.labelNClockFreq.Location = new System.Drawing.Point(111, 16);
            this.labelNClockFreq.Name = "labelNClockFreq";
            this.labelNClockFreq.Size = new System.Drawing.Size(13, 13);
            this.labelNClockFreq.TabIndex = 19;
            this.labelNClockFreq.Text = "0";
            //
            // label24
            //
            this.label24.AutoSize = true;
            this.label24.Location = new System.Drawing.Point(12, 137);
            this.label24.Name = "label24";
            this.label24.Size = new System.Drawing.Size(63, 13);
            this.label24.TabIndex = 18;
            this.label24.Text = "Klanglänge:";
            //
            // label22
            //
            this.label22.AutoSize = true;
            this.label22.Location = new System.Drawing.Point(12, 86);
            this.label22.Name = "label22";
            this.label22.Size = new System.Drawing.Size(93, 13);
            this.label22.TabIndex = 4;
            this.label22.Text = "Result Frequency:";
            //
            // label26
            //
            this.label26.AutoSize = true;
            this.label26.Location = new System.Drawing.Point(12, 120);
            this.label26.Name = "label26";
            this.label26.Size = new System.Drawing.Size(88, 13);
            this.label26.TabIndex = 17;
            this.label26.Text = "Envel. Direction: ";
            //
            // label20
            //
            this.label20.AutoSize = true;
            this.label20.Location = new System.Drawing.Point(12, 35);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(79, 13);
            this.label20.TabIndex = 3;
            this.label20.Text = "Dividing Ratio: ";
            //
            // label27
            //
            this.label27.AutoSize = true;
            this.label27.Location = new System.Drawing.Point(12, 102);
            this.label27.Name = "label27";
            this.label27.Size = new System.Drawing.Size(76, 13);
            this.label27.TabIndex = 16;
            this.label27.Text = "Envel. Sweep:";
            //
            // label18
            //
            this.label18.AutoSize = true;
            this.label18.Location = new System.Drawing.Point(12, 69);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(50, 13);
            this.label18.TabIndex = 2;
            this.label18.Text = "Counter: ";
            //
            // label16
            //
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(12, 52);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(67, 13);
            this.label16.TabIndex = 1;
            this.label16.Text = "Counter Bits:";
            //
            // label14
            //
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(12, 16);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(93, 13);
            this.label14.TabIndex = 0;
            this.label14.Text = "Clock Frequency: ";
            //
            // btnRecordWav
            //
            this.btnRecordWav.Location = new System.Drawing.Point(350, 349);
            this.btnRecordWav.Name = "btnRecordWav";
            this.btnRecordWav.Size = new System.Drawing.Size(185, 23);
            this.btnRecordWav.TabIndex = 19;
            this.btnRecordWav.Text = "Audio aufnehmen (.wav)";
            this.btnRecordWav.UseVisualStyleBackColor = true;
            this.btnRecordWav.Click += new System.EventHandler(this.btnRecordWav_Click);
            //
            // frmAudioTool
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(548, 372);
            this.Controls.Add(this.btnRecordWav);
            this.Controls.Add(this.groupBox5);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.checkBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.Name = "frmAudioTool";
            this.Text = "AetherBoy GBC Emulator - Audio-Inspector";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Controls.LevelDisplayControl levelDisplayControl1;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.CheckBox checkBox1;
        private Controls.LevelDisplayControl levelDisplayControl2;
        private Controls.LevelDisplayControl levelDisplayControl3;
        private Controls.LevelDisplayControl levelDisplayControl4;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label labelQ1Freq;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label labelQ1SweepDirection;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label labelQ1SweepShift;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label labelQ1SweepCycles;
        private System.Windows.Forms.Label labelQ1WaveDuty;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label labelQ1EnvelDirection;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label labelQ1EnvelSweep;
        private System.Windows.Forms.Label labelQ1SoundLength;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label labelQ2SoundLength;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label labelQ2EnvelDirection;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label labelQ2EnvelSweep;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.Label labelQ2WaveDuty;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.Label labelQ2SweepDirection;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.Label labelQ2SweepShift;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.Label labelQ2SweepCycles;
        private System.Windows.Forms.Label label23;
        private System.Windows.Forms.Label labelQ2Freq;
        private System.Windows.Forms.Label label25;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.GroupBox groupBox5;
        private Controls.WaveDataControl waveDataControl1;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label labelWSoundLength;
        private System.Windows.Forms.Label labelWFreq;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.Label label22;
        private System.Windows.Forms.Label label24;
        private System.Windows.Forms.Label label26;
        private System.Windows.Forms.Label label27;
        private System.Windows.Forms.Label labelNClockFreq;
        private System.Windows.Forms.Label labelNSoundLength;
        private System.Windows.Forms.Label labelNEnvelDirection;
        private System.Windows.Forms.Label labelNEnvelSweep;
        private System.Windows.Forms.Label labelNResultFreq;
        private System.Windows.Forms.Label labelNCounter;
        private System.Windows.Forms.Label labelNCounterBits;
        private System.Windows.Forms.Label labelNDividingRatio;
        private System.Windows.Forms.Button btnRecordWav;
    }
}
