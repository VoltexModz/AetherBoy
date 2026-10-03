namespace nanoboy
{
    partial class frmNano
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
            if (disposing) StopSessionAfterDirectDispose();
            if (disposing && (components != null))
            {
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
            this.menuStrip = new nanoboy.Controls.AetherCommandSet();
            this.menuFile = new nanoboy.Controls.AetherCommand();
            this.menuOpen = new nanoboy.Controls.AetherCommand();
            this.menuRecentFiles = new nanoboy.Controls.AetherCommand();
            this.toolStripSeparator1 = new nanoboy.Controls.AetherCommandSeparator();
            this.menuSaveState = new nanoboy.Controls.AetherCommand();
            this.menuSaveStateQuickSave = new nanoboy.Controls.AetherCommand();
            this.menuSaveStateQuickLoad = new nanoboy.Controls.AetherCommand();
            this.menuBatterySaveSafety = new nanoboy.Controls.AetherCommand();
            this.toolStripSeparatorSave = new nanoboy.Controls.AetherCommandSeparator();
            this.menuSaveSlot1 = new nanoboy.Controls.AetherCommand();
            this.menuSaveSlot2 = new nanoboy.Controls.AetherCommand();
            this.menuSaveSlot3 = new nanoboy.Controls.AetherCommand();
            this.menuSaveSlot4 = new nanoboy.Controls.AetherCommand();
            this.menuSaveSlot5 = new nanoboy.Controls.AetherCommand();
            this.toolStripSeparatorClose = new nanoboy.Controls.AetherCommandSeparator();
            this.menuClose = new nanoboy.Controls.AetherCommand();
            this.menuItem1 = new nanoboy.Controls.AetherCommand();
            this.menuControlCenter = new nanoboy.Controls.AetherCommand();
            this.menuItem2 = new nanoboy.Controls.AetherCommand();
            this.menuAudioOn = new nanoboy.Controls.AetherCommand();
            this.toolStripSeparator2 = new nanoboy.Controls.AetherCommandSeparator();
            this.menuAudioC1 = new nanoboy.Controls.AetherCommand();
            this.menuAudioC2 = new nanoboy.Controls.AetherCommand();
            this.menuAudioC3 = new nanoboy.Controls.AetherCommand();
            this.menuAudioC4 = new nanoboy.Controls.AetherCommand();
            this.menuItem5 = new nanoboy.Controls.AetherCommand();
            this.menuAudioQ1 = new nanoboy.Controls.AetherCommand();
            this.menuAudioQ2 = new nanoboy.Controls.AetherCommand();
            this.menuAudioQ3 = new nanoboy.Controls.AetherCommand();
            this.menuAudioQ4 = new nanoboy.Controls.AetherCommand();
            this.menuItem3 = new nanoboy.Controls.AetherCommand();
            this.menuItem13 = new nanoboy.Controls.AetherCommand();
            this.menuFrameSkip0 = new nanoboy.Controls.AetherCommand();
            this.menuFrameSkip1 = new nanoboy.Controls.AetherCommand();
            this.menuFrameSkip2 = new nanoboy.Controls.AetherCommand();
            this.menuFrameSkip3 = new nanoboy.Controls.AetherCommand();
            this.menuFrameSkip4 = new nanoboy.Controls.AetherCommand();
            this.menuItem19 = new nanoboy.Controls.AetherCommand();
            this.menuSize1 = new nanoboy.Controls.AetherCommand();
            this.menuSize2 = new nanoboy.Controls.AetherCommand();
            this.menuSize3 = new nanoboy.Controls.AetherCommand();
            this.menuSize4 = new nanoboy.Controls.AetherCommand();
            this.menuSizeFull = new nanoboy.Controls.AetherCommand();
            this.menuPalette = new nanoboy.Controls.AetherCommand();
            this.menuPalettePocket = new nanoboy.Controls.AetherCommand();
            this.menuPalettePeaGreen = new nanoboy.Controls.AetherCommand();
            this.menuPaletteGBLight = new nanoboy.Controls.AetherCommand();
            this.menuPaletteSepia = new nanoboy.Controls.AetherCommand();
            this.menuPaletteCyberpunk = new nanoboy.Controls.AetherCommand();
            this.menuControls = new nanoboy.Controls.AetherCommand();
            this.menuItem21 = new nanoboy.Controls.AetherCommand();
            this.menuAudioInspector = new nanoboy.Controls.AetherCommand();
            this.menuItem4 = new nanoboy.Controls.AetherCommand();
            this.menuRomInfo = new nanoboy.Controls.AetherCommand();
            this.menuAbout = new nanoboy.Controls.AetherCommand();
            this.toolStripSeparator3 = new nanoboy.Controls.AetherCommandSeparator();
            this.menuVideoFilter = new nanoboy.Controls.AetherCommand();
            this.menuFilterSharp = new nanoboy.Controls.AetherCommand();
            this.menuFilterSmooth = new nanoboy.Controls.AetherCommand();
            this.menuFilterLCDGrid = new nanoboy.Controls.AetherCommand();
            this.menuCheats = new nanoboy.Controls.AetherCommand();
            this.menuRewind = new nanoboy.Controls.AetherCommand();
            this.menuLinkCable = new nanoboy.Controls.AetherCommand();
            this.menuChangelog = new nanoboy.Controls.AetherCommand();
            this.updateTimer = new System.Windows.Forms.Timer(this.components);
            this.gameView = new nanoboy.Controls.GameDisplayControl();
            this.SuspendLayout();
            //
            // nanoboy.Controls.AetherCommandSet
            //
            this.menuStrip.Items.AddRange(new nanoboy.Controls.AetherCommandItem[] {
            this.menuFile,
            this.menuItem1,
            this.menuItem21,
            this.menuItem4});
            //
            // menuFile
            //
            this.menuFile.DropDownItems.AddRange(new nanoboy.Controls.AetherCommandItem[] {
            this.menuOpen,
            this.menuRecentFiles,
            this.toolStripSeparator1,
            this.menuSaveState,
            this.toolStripSeparatorClose,
            this.menuClose});
            this.menuFile.Name = "menuFile";
            this.menuFile.Size = new System.Drawing.Size(67, 20);
            this.menuFile.Text = "Emulator";
            //
            // menuOpen
            //
            this.menuOpen.Name = "menuOpen";
            this.menuOpen.Size = new System.Drawing.Size(180, 22);
            this.menuOpen.Text = global::AetherBoy.Runtime.Localization.UiText.Get("ROM öffnen");
            this.menuOpen.Click += new System.EventHandler(this.menuOpen_Click);
            //
            // menuRecentFiles
            //
            this.menuRecentFiles.Name = "menuRecentFiles";
            this.menuRecentFiles.Size = new System.Drawing.Size(180, 22);
            this.menuRecentFiles.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Zuletzt geöffnet");
            //
            // toolStripSeparator1
            //
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(177, 6);
            //
            // menuSaveState
            //
            this.menuSaveState.DropDownItems.AddRange(new nanoboy.Controls.AetherCommandItem[] {
            this.menuSaveStateQuickSave,
            this.menuSaveStateQuickLoad,
            this.menuBatterySaveSafety,
            this.toolStripSeparatorSave,
            this.menuSaveSlot1,
            this.menuSaveSlot2,
            this.menuSaveSlot3,
            this.menuSaveSlot4,
            this.menuSaveSlot5});
            this.menuSaveState.Name = "menuSaveState";
            this.menuSaveState.Size = new System.Drawing.Size(180, 22);
            this.menuSaveState.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Save States");
            this.menuSaveState.ToolTipText = global::AetherBoy.Runtime.Localization.UiText.Get("ROM-gebundene, integritätsgeprüfte Spielstände in fünf Slots.");
            //
            // menuSaveStateQuickSave
            //
            this.menuSaveStateQuickSave.Name = "menuSaveStateQuickSave";
            this.menuSaveStateQuickSave.Size = new System.Drawing.Size(201, 22);
            this.menuSaveStateQuickSave.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Schnellspeichern (F5)");
            this.menuSaveStateQuickSave.Click += new System.EventHandler(this.menuSaveStateQuickSave_Click);
            //
            // menuSaveStateQuickLoad
            //
            this.menuSaveStateQuickLoad.Name = "menuSaveStateQuickLoad";
            this.menuSaveStateQuickLoad.Size = new System.Drawing.Size(201, 22);
            this.menuSaveStateQuickLoad.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Schnellladen (F8)");
            this.menuSaveStateQuickLoad.Click += new System.EventHandler(this.menuSaveStateQuickLoad_Click);
            //
            // menuBatterySaveSafety
            //
            this.menuBatterySaveSafety.Name = "menuBatterySaveSafety";
            this.menuBatterySaveSafety.Size = new System.Drawing.Size(246, 22);
            this.menuBatterySaveSafety.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Save Safety Center");
            this.menuBatterySaveSafety.ToolTipText = global::AetherBoy.Runtime.Localization.UiText.Get("Batterie-Spielstand und drei rotierende Backups prüfen oder wiederherstellen.");
            this.menuBatterySaveSafety.Click += new System.EventHandler(this.menuBatterySaveSafety_Click);
            //
            // toolStripSeparatorSave
            //
            this.toolStripSeparatorSave.Name = "toolStripSeparatorSave";
            this.toolStripSeparatorSave.Size = new System.Drawing.Size(198, 6);
            //
            // menuSaveSlot1
            //
            this.menuSaveSlot1.Name = "menuSaveSlot1";
            this.menuSaveSlot1.Size = new System.Drawing.Size(201, 22);
            this.menuSaveSlot1.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Slot 1 (Aktiv)");
            this.menuSaveSlot1.Click += new System.EventHandler(this.menuSaveSlot1_Click);
            //
            // menuSaveSlot2
            //
            this.menuSaveSlot2.Name = "menuSaveSlot2";
            this.menuSaveSlot2.Size = new System.Drawing.Size(201, 22);
            this.menuSaveSlot2.Text = "Slot 2";
            this.menuSaveSlot2.Click += new System.EventHandler(this.menuSaveSlot2_Click);
            //
            // menuSaveSlot3
            //
            this.menuSaveSlot3.Name = "menuSaveSlot3";
            this.menuSaveSlot3.Size = new System.Drawing.Size(201, 22);
            this.menuSaveSlot3.Text = "Slot 3";
            this.menuSaveSlot3.Click += new System.EventHandler(this.menuSaveSlot3_Click);
            //
            // menuSaveSlot4
            //
            this.menuSaveSlot4.Name = "menuSaveSlot4";
            this.menuSaveSlot4.Size = new System.Drawing.Size(201, 22);
            this.menuSaveSlot4.Text = "Slot 4";
            this.menuSaveSlot4.Click += new System.EventHandler(this.menuSaveSlot4_Click);
            //
            // menuSaveSlot5
            //
            this.menuSaveSlot5.Name = "menuSaveSlot5";
            this.menuSaveSlot5.Size = new System.Drawing.Size(201, 22);
            this.menuSaveSlot5.Text = "Slot 5";
            this.menuSaveSlot5.Click += new System.EventHandler(this.menuSaveSlot5_Click);
            //
            // toolStripSeparatorClose
            //
            this.toolStripSeparatorClose.Name = "toolStripSeparatorClose";
            this.toolStripSeparatorClose.Size = new System.Drawing.Size(177, 6);
            //
            // menuClose
            //
            this.menuClose.Name = "menuClose";
            this.menuClose.Size = new System.Drawing.Size(180, 22);
            this.menuClose.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Emulator schließen");
            this.menuClose.Click += new System.EventHandler(this.menuClose_Click);
            //
            // menuItem1
            //
            this.menuItem1.DropDownItems.AddRange(new nanoboy.Controls.AetherCommandItem[] {
            this.menuControlCenter,
            this.menuItem2,
            this.menuItem3,
            this.menuPalette,
            this.menuControls});
            this.menuItem1.Name = "menuItem1";
            this.menuItem1.Size = new System.Drawing.Size(69, 20);
            this.menuItem1.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Optionen");
            //
            // menuControlCenter
            //
            this.menuControlCenter.Name = "menuControlCenter";
            this.menuControlCenter.Size = new System.Drawing.Size(180, 22);
            this.menuControlCenter.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Control Center");
            this.menuControlCenter.Click += new System.EventHandler(this.menuControlCenter_Click);
            //
            // menuItem2
            //
            this.menuItem2.DropDownItems.AddRange(new nanoboy.Controls.AetherCommandItem[] {
            this.menuAudioOn,
            this.toolStripSeparator2,
            this.menuAudioC1,
            this.menuAudioC2,
            this.menuAudioC3,
            this.menuAudioC4,
            this.menuItem5});
            this.menuItem2.Name = "menuItem2";
            this.menuItem2.Size = new System.Drawing.Size(180, 22);
            this.menuItem2.Text = "Audio";
            //
            // menuAudioOn
            //
            this.menuAudioOn.Name = "menuAudioOn";
            this.menuAudioOn.Size = new System.Drawing.Size(117, 22);
            this.menuAudioOn.Text = global::AetherBoy.Runtime.Localization.UiText.Get("An");
            this.menuAudioOn.Click += new System.EventHandler(this.menuAudioOn_Click);
            //
            // toolStripSeparator2
            //
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(114, 6);
            //
            // menuAudioC1
            //
            this.menuAudioC1.Name = "menuAudioC1";
            this.menuAudioC1.Size = new System.Drawing.Size(117, 22);
            this.menuAudioC1.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Kanal 1");
            this.menuAudioC1.Click += new System.EventHandler(this.menuAudioC1_Click);
            //
            // menuAudioC2
            //
            this.menuAudioC2.Name = "menuAudioC2";
            this.menuAudioC2.Size = new System.Drawing.Size(117, 22);
            this.menuAudioC2.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Kanal 2");
            this.menuAudioC2.Click += new System.EventHandler(this.menuAudioC2_Click);
            //
            // menuAudioC3
            //
            this.menuAudioC3.Name = "menuAudioC3";
            this.menuAudioC3.Size = new System.Drawing.Size(117, 22);
            this.menuAudioC3.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Kanal 3");
            this.menuAudioC3.Click += new System.EventHandler(this.menuAudioC3_Click);
            //
            // menuAudioC4
            //
            this.menuAudioC4.Name = "menuAudioC4";
            this.menuAudioC4.Size = new System.Drawing.Size(117, 22);
            this.menuAudioC4.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Kanal 4");
            this.menuAudioC4.Click += new System.EventHandler(this.menuAudioC4_Click);
            //
            // menuItem5
            //
            this.menuItem5.DropDownItems.AddRange(new nanoboy.Controls.AetherCommandItem[] {
            this.menuAudioQ1,
            this.menuAudioQ2,
            this.menuAudioQ3,
            this.menuAudioQ4});
            this.menuItem5.Name = "menuItem5";
            this.menuItem5.Size = new System.Drawing.Size(117, 22);
            this.menuItem5.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Qualität");
            //
            // menuAudioQ1
            //
            this.menuAudioQ1.Name = "menuAudioQ1";
            this.menuAudioQ1.Size = new System.Drawing.Size(123, 22);
            this.menuAudioQ1.Text = "8192 Hz";
            this.menuAudioQ1.Click += new System.EventHandler(this.menuAudioQ1_Click);
            //
            // menuAudioQ2
            //
            this.menuAudioQ2.Name = "menuAudioQ2";
            this.menuAudioQ2.Size = new System.Drawing.Size(123, 22);
            this.menuAudioQ2.Text = "16384 Hz";
            this.menuAudioQ2.Click += new System.EventHandler(this.menuAudioQ2_Click);
            //
            // menuAudioQ3
            //
            this.menuAudioQ3.Name = "menuAudioQ3";
            this.menuAudioQ3.Size = new System.Drawing.Size(123, 22);
            this.menuAudioQ3.Text = "32768 Hz";
            this.menuAudioQ3.Click += new System.EventHandler(this.menuAudioQ3_Click);
            //
            // menuAudioQ4
            //
            this.menuAudioQ4.Name = "menuAudioQ4";
            this.menuAudioQ4.Size = new System.Drawing.Size(123, 22);
            this.menuAudioQ4.Text = "44100 Hz";
            this.menuAudioQ4.Click += new System.EventHandler(this.menuAudioQ4_Click);
            //
            // menuItem3
            //
            this.menuItem3.DropDownItems.AddRange(new nanoboy.Controls.AetherCommandItem[] {
            this.menuItem13,
            this.menuItem19,
            this.menuVideoFilter});
            this.menuItem3.Name = "menuItem3";
            this.menuItem3.Size = new System.Drawing.Size(180, 22);
            this.menuItem3.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Video");
            //
            // menuVideoFilter
            //
            this.menuVideoFilter.DropDownItems.AddRange(new nanoboy.Controls.AetherCommandItem[] {
            this.menuFilterSharp,
            this.menuFilterSmooth,
            this.menuFilterLCDGrid});
            this.menuVideoFilter.Name = "menuVideoFilter";
            this.menuVideoFilter.Size = new System.Drawing.Size(145, 22);
            this.menuVideoFilter.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Grafikfilter");
            //
            // menuFilterSharp
            //
            this.menuFilterSharp.Name = "menuFilterSharp";
            this.menuFilterSharp.Size = new System.Drawing.Size(190, 22);
            this.menuFilterSharp.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Scharfe Pixel (Nearest)");
            this.menuFilterSharp.Click += new System.EventHandler(this.menuFilterSharp_Click);
            //
            // menuFilterSmooth
            //
            this.menuFilterSmooth.Name = "menuFilterSmooth";
            this.menuFilterSmooth.Size = new System.Drawing.Size(190, 22);
            this.menuFilterSmooth.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Sanft (Bilinear)");
            this.menuFilterSmooth.Click += new System.EventHandler(this.menuFilterSmooth_Click);
            //
            // menuFilterLCDGrid
            //
            this.menuFilterLCDGrid.Name = "menuFilterLCDGrid";
            this.menuFilterLCDGrid.Size = new System.Drawing.Size(190, 22);
            this.menuFilterLCDGrid.Text = global::AetherBoy.Runtime.Localization.UiText.Get("LCD Subpixel Grid");
            this.menuFilterLCDGrid.Click += new System.EventHandler(this.menuFilterLCDGrid_Click);
            //
            // menuItem13
            //
            this.menuItem13.DropDownItems.AddRange(new nanoboy.Controls.AetherCommandItem[] {
            this.menuFrameSkip0,
            this.menuFrameSkip1,
            this.menuFrameSkip2,
            this.menuFrameSkip3,
            this.menuFrameSkip4});
            this.menuItem13.Name = "menuItem13";
            this.menuItem13.Size = new System.Drawing.Size(125, 22);
            this.menuItem13.Text = "Frameskip";
            this.menuItem13.Visible = false;
            //
            // menuFrameSkip0
            //
            this.menuFrameSkip0.Name = "menuFrameSkip0";
            this.menuFrameSkip0.Size = new System.Drawing.Size(103, 22);
            this.menuFrameSkip0.Text = global::AetherBoy.Runtime.Localization.UiText.Get("None");
            this.menuFrameSkip0.Click += new System.EventHandler(this.menuFrameSkip0_Click);
            //
            // menuFrameSkip1
            //
            this.menuFrameSkip1.Name = "menuFrameSkip1";
            this.menuFrameSkip1.Size = new System.Drawing.Size(103, 22);
            this.menuFrameSkip1.Text = "1";
            this.menuFrameSkip1.Click += new System.EventHandler(this.menuFrameSkip1_Click);
            //
            // menuFrameSkip2
            //
            this.menuFrameSkip2.Name = "menuFrameSkip2";
            this.menuFrameSkip2.Size = new System.Drawing.Size(103, 22);
            this.menuFrameSkip2.Text = "2";
            this.menuFrameSkip2.Click += new System.EventHandler(this.menuFrameSkip2_Click);
            //
            // menuFrameSkip3
            //
            this.menuFrameSkip3.Name = "menuFrameSkip3";
            this.menuFrameSkip3.Size = new System.Drawing.Size(103, 22);
            this.menuFrameSkip3.Text = "3";
            this.menuFrameSkip3.Click += new System.EventHandler(this.menuFrameSkip3_Click);
            //
            // menuFrameSkip4
            //
            this.menuFrameSkip4.Name = "menuFrameSkip4";
            this.menuFrameSkip4.Size = new System.Drawing.Size(103, 22);
            this.menuFrameSkip4.Text = "4";
            this.menuFrameSkip4.Click += new System.EventHandler(this.menuFrameSkip4_Click);
            //
            // menuItem19
            //
            this.menuItem19.DropDownItems.AddRange(new nanoboy.Controls.AetherCommandItem[] {
            this.menuSize1,
            this.menuSize2,
            this.menuSize3,
            this.menuSize4,
            this.menuSizeFull});
            this.menuItem19.Name = "menuItem19";
            this.menuItem19.Size = new System.Drawing.Size(125, 22);
            this.menuItem19.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Größe");
            //
            // menuSize1
            //
            this.menuSize1.Name = "menuSize1";
            this.menuSize1.Size = new System.Drawing.Size(114, 22);
            this.menuSize1.Text = "1x";
            this.menuSize1.Click += new System.EventHandler(this.menuSize1_Click);
            //
            // menuSize2
            //
            this.menuSize2.Name = "menuSize2";
            this.menuSize2.Size = new System.Drawing.Size(114, 22);
            this.menuSize2.Text = "2x";
            this.menuSize2.Click += new System.EventHandler(this.menuSize2_Click);
            //
            // menuSize3
            //
            this.menuSize3.Name = "menuSize3";
            this.menuSize3.Size = new System.Drawing.Size(114, 22);
            this.menuSize3.Text = "3x";
            this.menuSize3.Click += new System.EventHandler(this.menuSize3_Click);
            //
            // menuSize4
            //
            this.menuSize4.Name = "menuSize4";
            this.menuSize4.Size = new System.Drawing.Size(114, 22);
            this.menuSize4.Text = "4x";
            this.menuSize4.Click += new System.EventHandler(this.menuSize4_Click);
            //
            // menuSizeFull
            //
            this.menuSizeFull.Name = "menuSizeFull";
            this.menuSizeFull.Size = new System.Drawing.Size(114, 22);
            this.menuSizeFull.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Vollbild");
            this.menuSizeFull.Click += new System.EventHandler(this.menuSizeFull_Click);
            //
            // menuPalette
            //
            this.menuPalette.DropDownItems.AddRange(new nanoboy.Controls.AetherCommandItem[] {
            this.menuPalettePocket,
            this.menuPalettePeaGreen,
            this.menuPaletteGBLight,
            this.menuPaletteSepia,
            this.menuPaletteCyberpunk});
            this.menuPalette.Name = "menuPalette";
            this.menuPalette.Size = new System.Drawing.Size(180, 22);
            this.menuPalette.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Farbpalette (DMG)");
            //
            // menuPalettePocket
            //
            this.menuPalettePocket.Name = "menuPalettePocket";
            this.menuPalettePocket.Size = new System.Drawing.Size(217, 22);
            this.menuPalettePocket.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Game Boy Pocket (Grau)");
            this.menuPalettePocket.Click += new System.EventHandler(this.menuPalettePocket_Click);
            //
            // menuPalettePeaGreen
            //
            this.menuPalettePeaGreen.Name = "menuPalettePeaGreen";
            this.menuPalettePeaGreen.Size = new System.Drawing.Size(217, 22);
            this.menuPalettePeaGreen.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Pea Green (Original DMG)");
            this.menuPalettePeaGreen.Click += new System.EventHandler(this.menuPalettePeaGreen_Click);
            //
            // menuPaletteGBLight
            //
            this.menuPaletteGBLight.Name = "menuPaletteGBLight";
            this.menuPaletteGBLight.Size = new System.Drawing.Size(217, 22);
            this.menuPaletteGBLight.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Game Boy Light (Türkis)");
            this.menuPaletteGBLight.Click += new System.EventHandler(this.menuPaletteGBLight_Click);
            //
            // menuPaletteSepia
            //
            this.menuPaletteSepia.Name = "menuPaletteSepia";
            this.menuPaletteSepia.Size = new System.Drawing.Size(217, 22);
            this.menuPaletteSepia.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Sepia (Warm)");
            this.menuPaletteSepia.Click += new System.EventHandler(this.menuPaletteSepia_Click);
            //
            // menuPaletteCyberpunk
            //
            this.menuPaletteCyberpunk.Name = "menuPaletteCyberpunk";
            this.menuPaletteCyberpunk.Size = new System.Drawing.Size(217, 22);
            this.menuPaletteCyberpunk.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Cyberpunk (Neon)");
            this.menuPaletteCyberpunk.Click += new System.EventHandler(this.menuPaletteCyberpunk_Click);
            //
            // menuControls
            //
            this.menuControls.Name = "menuControls";
            this.menuControls.Size = new System.Drawing.Size(180, 22);
            this.menuControls.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Hotkeys");
            this.menuControls.Click += new System.EventHandler(this.menuControls_Click);
            //
            // menuItem21
            //
            this.menuItem21.DropDownItems.AddRange(new nanoboy.Controls.AetherCommandItem[] {
            this.menuAudioInspector,
            this.menuCheats,
            this.menuRewind,
            this.menuLinkCable});
            this.menuItem21.Name = "menuItem21";
            this.menuItem21.Size = new System.Drawing.Size(79, 20);
            this.menuItem21.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Werkzeuge");
            //
            // menuAudioInspector
            //
            this.menuAudioInspector.Name = "menuAudioInspector";
            this.menuAudioInspector.Size = new System.Drawing.Size(250, 22);
            this.menuAudioInspector.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Audio Inspector");
            this.menuAudioInspector.Click += new System.EventHandler(this.menuAudioInspector_Click);
            //
            // menuCheats
            //
            this.menuCheats.Name = "menuCheats";
            this.menuCheats.Size = new System.Drawing.Size(250, 22);
            this.menuCheats.Text = global::AetherBoy.Runtime.Localization.UiText.Get("GameShark-Cheats (experimentell)");
            this.menuCheats.ToolTipText = global::AetherBoy.Runtime.Localization.UiText.Get("Experimentelle RAM-Codes im Format 01XXYYZZ.");
            this.menuCheats.Click += new System.EventHandler(this.menuCheats_Click);
            //
            // menuRewind
            //
            this.menuRewind.Name = "menuRewind";
            this.menuRewind.Size = new System.Drawing.Size(250, 22);
            this.menuRewind.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Zurückspulen");
            this.menuRewind.ToolTipText = global::AetherBoy.Runtime.Localization.UiText.Get("Springt zum vorherigen Zustand im begrenzten Rewind-Puffer.");
            this.menuRewind.Click += new System.EventHandler(this.menuRewind_Click);
            //
            // menuLinkCable
            //
            this.menuLinkCable.Enabled = true;
            this.menuLinkCable.Name = "menuLinkCable";
            this.menuLinkCable.Size = new System.Drawing.Size(250, 22);
            this.menuLinkCable.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Lokales Link-Kabel · GB/GBC/GBA (experimentell)");
            this.menuLinkCable.ToolTipText = global::AetherBoy.Runtime.Localization.UiText.Get("Zwei Spielansichten auf einem PC mit getrennten Spielständen. Zwei GB/GBC oder zwei GBA; kein Netzwerk oder Wireless.");
            this.menuLinkCable.Click += new System.EventHandler(this.menuLinkCable_Click);
            //
            // menuItem4
            //
            this.menuItem4.DropDownItems.AddRange(new nanoboy.Controls.AetherCommandItem[] {
            this.menuChangelog,
            this.menuRomInfo,
            this.menuAbout,
            this.toolStripSeparator3});
            this.menuItem4.Name = "menuItem4";
            this.menuItem4.Size = new System.Drawing.Size(44, 20);
            this.menuItem4.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Hilfe");
            //
            // menuChangelog
            //
            this.menuChangelog.Name = "menuChangelog";
            this.menuChangelog.Size = new System.Drawing.Size(250, 22);
            this.menuChangelog.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Projektgeschichte && Changelog");
            this.menuChangelog.Click += new System.EventHandler(this.menuChangelog_Click);
            //
            // menuRomInfo
            //
            this.menuRomInfo.Name = "menuRomInfo";
            this.menuRomInfo.Size = new System.Drawing.Size(180, 22);
            this.menuRomInfo.Text = global::AetherBoy.Runtime.Localization.UiText.Get("ROM Informationen");
            this.menuRomInfo.Click += new System.EventHandler(this.menuRomInfo_Click);
            //
            // menuAbout
            //
            this.menuAbout.Name = "menuAbout";
            this.menuAbout.Size = new System.Drawing.Size(180, 22);
            this.menuAbout.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Informationen");
            this.menuAbout.Click += new System.EventHandler(this.menuAbout_Click);
            //
            // toolStripSeparator3
            //
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(177, 6);
            // updateTimer
            //
            this.updateTimer.Interval = 16;
            this.updateTimer.Tick += new System.EventHandler(this.updateTimer_Tick);
            //
            // gameView
            //
            this.gameView.BackColor = System.Drawing.Color.Black;
            this.gameView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gameView.Location = new System.Drawing.Point(0, 24);
            this.gameView.Name = "gameView";
            this.gameView.Size = new System.Drawing.Size(320, 288);
            this.gameView.TabIndex = 1;
            this.gameView.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.gameView_PreviewKeyDown);
            this.gameView.KeyUp += new System.Windows.Forms.KeyEventHandler(this.gameView_KeyUp);
            //
            // frmNano
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(320, 312);
            this.Controls.Add(this.gameView);
            this.Name = "frmNano";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "AetherBoy 4.8.0-alpha.1 (Alpha)";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmNano_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private nanoboy.Controls.AetherCommandSet menuStrip;
        private nanoboy.Controls.AetherCommand menuFile;
        private nanoboy.Controls.AetherCommand menuOpen;
        private nanoboy.Controls.AetherCommand menuRecentFiles;
        private nanoboy.Controls.AetherCommandSeparator toolStripSeparator1;
        private nanoboy.Controls.AetherCommand menuSaveState;
        private nanoboy.Controls.AetherCommand menuSaveStateQuickSave;
        private nanoboy.Controls.AetherCommand menuSaveStateQuickLoad;
        private nanoboy.Controls.AetherCommand menuBatterySaveSafety;
        private nanoboy.Controls.AetherCommandSeparator toolStripSeparatorSave;
        private nanoboy.Controls.AetherCommand menuSaveSlot1;
        private nanoboy.Controls.AetherCommand menuSaveSlot2;
        private nanoboy.Controls.AetherCommand menuSaveSlot3;
        private nanoboy.Controls.AetherCommand menuSaveSlot4;
        private nanoboy.Controls.AetherCommand menuSaveSlot5;
        private nanoboy.Controls.AetherCommandSeparator toolStripSeparatorClose;
        private nanoboy.Controls.AetherCommand menuClose;
        private nanoboy.Controls.AetherCommand menuItem1;
        private nanoboy.Controls.AetherCommand menuControlCenter;
        private nanoboy.Controls.AetherCommand menuItem2;
        private nanoboy.Controls.AetherCommand menuItem3;
        private nanoboy.Controls.AetherCommand menuPalette;
        private nanoboy.Controls.AetherCommand menuPalettePocket;
        private nanoboy.Controls.AetherCommand menuPalettePeaGreen;
        private nanoboy.Controls.AetherCommand menuPaletteGBLight;
        private nanoboy.Controls.AetherCommand menuPaletteSepia;
        private nanoboy.Controls.AetherCommand menuPaletteCyberpunk;
        private nanoboy.Controls.AetherCommand menuControls;
        private nanoboy.Controls.AetherCommand menuAudioOn;
        private nanoboy.Controls.AetherCommandSeparator toolStripSeparator2;
        private nanoboy.Controls.AetherCommand menuAudioC1;
        private nanoboy.Controls.AetherCommand menuAudioC2;
        private nanoboy.Controls.AetherCommand menuAudioC3;
        private nanoboy.Controls.AetherCommand menuAudioC4;
        private nanoboy.Controls.AetherCommand menuItem13;
        private nanoboy.Controls.AetherCommand menuFrameSkip0;
        private nanoboy.Controls.AetherCommand menuFrameSkip1;
        private nanoboy.Controls.AetherCommand menuFrameSkip2;
        private nanoboy.Controls.AetherCommand menuFrameSkip3;
        private nanoboy.Controls.AetherCommand menuFrameSkip4;
        private nanoboy.Controls.AetherCommand menuItem19;
        private nanoboy.Controls.AetherCommand menuSize1;
        private nanoboy.Controls.AetherCommand menuSize2;
        private nanoboy.Controls.AetherCommand menuSize3;
        private nanoboy.Controls.AetherCommand menuSize4;
        private nanoboy.Controls.AetherCommand menuSizeFull;
        private nanoboy.Controls.GameDisplayControl gameView;
        private nanoboy.Controls.AetherCommand menuItem21;
        private nanoboy.Controls.AetherCommand menuAudioQ1;
        private nanoboy.Controls.AetherCommand menuAudioQ2;
        private nanoboy.Controls.AetherCommand menuAudioQ3;
        private nanoboy.Controls.AetherCommand menuAudioQ4;
        private nanoboy.Controls.AetherCommand menuItem5;
        private System.Windows.Forms.Timer updateTimer;
        private nanoboy.Controls.AetherCommand menuAudioInspector;
        private nanoboy.Controls.AetherCommand menuRomInfo;
        private nanoboy.Controls.AetherCommand menuAbout;
        private nanoboy.Controls.AetherCommandSeparator toolStripSeparator3;
        private nanoboy.Controls.AetherCommand menuItem4;
        private nanoboy.Controls.AetherCommand menuVideoFilter;
        private nanoboy.Controls.AetherCommand menuFilterSharp;
        private nanoboy.Controls.AetherCommand menuFilterSmooth;
        private nanoboy.Controls.AetherCommand menuFilterLCDGrid;
        private nanoboy.Controls.AetherCommand menuCheats;
        private nanoboy.Controls.AetherCommand menuRewind;
        private nanoboy.Controls.AetherCommand menuLinkCable;
        private nanoboy.Controls.AetherCommand menuChangelog;
    }
}
