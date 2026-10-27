using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Storage;
using UiText = AetherBoy.Runtime.Localization.UiText;

namespace nanoboy
{
    public partial class frmNano
    {
        private const int WmNcHitTest = 0x0084;
        private const int WmNcLeftButtonDown = 0x00A1;
        private const int HtCaption = 2;
        private const int HtLeft = 10;
        private const int HtRight = 11;
        private const int HtTop = 12;
        private const int HtTopLeft = 13;
        private const int HtTopRight = 14;
        private const int HtBottom = 15;
        private const int HtBottomLeft = 16;
        private const int HtBottomRight = 17;

        private Panel desktopNavigation = null!, desktopPlay = null!, desktopHeader = null!, desktopTransport = null!;
        private TableLayoutPanel desktopColumns = null!;
        private AetherScrollViewport desktopRailViewport = null!;
        private AetherSelect desktopSlot = null!;
        private Label desktopController = null!;
        private bool arrangingDesktop;
        private Size desktopLayoutClient, desktopLayoutBody;
        private float desktopLayoutScale;
        private bool desktopLayoutPlaying;
        private bool aetherShellInitialized;
        private Panel aetherRoot = null!;
        private AetherSaveNotice settingsSaveNotice = null!;
        private AetherChromePanel aetherTitleBar = null!;
        private AetherStagePanel aetherStage = null!;
        private Panel aetherEmptyState = null!;
        private AetherSurfacePanel aetherSessionRail = null!;
        private AetherStatusDot aetherStatusDot = null!;
        private Label aetherRomTitle = null!;
        private Label aetherStateValue = null!;
        private Label aetherModelValue = null!;
        private Label aetherAudioValue = null!;
        private Label aetherFilterValue = null!;
        private Label aetherInputValue = null!;
        private Label aetherSlotValue = null!;
        private AetherButton aetherOpenButton = null!;
        private AetherButton aetherDeckOpenButton = null!;
        private AetherButton aetherControlCenterButton = null!;
        private AetherButton aetherPauseButton = null!;
        private AetherButton aetherRewindButton = null!;
        private AetherButton aetherSaveButton = null!;
        private AetherButton aetherLoadButton = null!;
        private AetherButton aetherSaveSafetyButton = null!;
        private AetherButton aetherTurboButton = null!;
        private AetherButton aetherMaximizeButton = null!;
        private AetherCommand desktopCommands = null!;
        private readonly AetherSaveSlotInventory aetherSaveSlots = new();
        private AetherCommandMenu? aetherCommandMenu;
        private AetherButton? aetherActiveNav;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        private void InitializeAetherShell()
        {
            SuspendLayout();

            FormBorderStyle = FormBorderStyle.None;
            KeyPreview = true;
            AllowDrop = true;
            BackColor = AetherColors.Void;
            MinimumSize = new Size(780, 600);
            Font = AetherTypography.Create(10.5f);
            Padding = new Padding(1);

            aetherRoot = new Panel
            {
                BackColor = AetherColors.Void,
                Dock = DockStyle.Fill,
                Padding = Padding.Empty,
                Name = "aetherRoot"
            };

            desktopCommands = new AetherCommand(UiText.Get("Weitere Aktionen"));
            desktopCommands.DropDownItems.AddRange(menuStrip.Items.ToArray());
            aetherTitleBar = BuildTitleBar();
            Control content = BuildMainContent();

            aetherRoot.Controls.Add(content);
            aetherRoot.Controls.Add(BuildDesktopStatus());
            settingsSaveNotice = new AetherSaveNotice(() => settings.FlushPendingSavesAsync(),
                error => testerSession?.RecordException("settings.retry_save_failed", error)) { Dock = DockStyle.Top };
            aetherRoot.Controls.Add(settingsSaveNotice);
            aetherRoot.Controls.Add(aetherTitleBar);
            Controls.Add(aetherRoot);
            aetherRoot.BringToFront();

            DragEnter += frmNano_DragEnter;
            DragDrop += frmNano_DragDrop;
            Resize += frmNano_AetherResize;

            aetherUiScale = settings.UiScalePercent / 100f;
            InterfaceScale.Apply(aetherRoot, aetherUiScale);
            aetherShellInitialized = true;
            ClientSize = GetAetherClientSize(settings.VideoScaleFactor);
            UpdateAetherSessionUi(null);
            ResumeLayout(performLayout: true);
        }

        private AetherChromePanel BuildTitleBar()
        {
            var bar = new AetherChromePanel { Dock = DockStyle.Top, Height = 64, Name = "aetherTitleBar" };
            var mark = new PictureBox { Bounds = new Rectangle(20, 13, 38, 38), SizeMode = PictureBoxSizeMode.Zoom, TabStop = false };
            Branding.AppBrand.BindMark(mark);
            var name = CreateUiLabel("AetherBoy", 13.5f, FontStyle.Bold, AetherColors.Text, DockStyle.None, 32);
            name.Bounds = new Rectangle(70, 16, 150, 32);
            var title = CreateUiLabel(UiText.Get("Spielen"), 9.75f, FontStyle.Regular, AetherColors.Muted, DockStyle.Fill, 64, ContentAlignment.MiddleCenter);
            title.Name = "desktopPageTitle";
            title.Padding = new Padding(220, 0, 180, 0);
            bar.SizeChanged += (_, _) => title.Visible = bar.Width >= 620 * Math.Max(1, aetherUiScale * DeviceDpi / 96f);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 164, Padding = new Padding(0, 12, 12, 0), WrapContents = false, BackColor = AetherColors.Chrome };
            var minimize = CreateChromeButton("", UiText.Get("Fenster minimieren"), (_, _) => WindowState = FormWindowState.Minimized);
            minimize.Icon = UiIcon.Minimize; minimize.IconOnly = true;
            aetherMaximizeButton = CreateChromeButton("", UiText.Get("Fenster maximieren"), (_, _) => ToggleAetherMaximize());
            aetherMaximizeButton.Icon = UiIcon.Maximize; aetherMaximizeButton.IconOnly = true;
            var close = CreateChromeButton("", UiText.Get("Fenster schließen"), (_, _) => Close());
            close.Icon = UiIcon.Close; close.IconOnly = true;
            actions.Controls.AddRange([minimize, aetherMaximizeButton, close]);
            bar.Controls.Add(title); bar.Controls.Add(actions); bar.Controls.Add(name); bar.Controls.Add(mark); name.BringToFront(); mark.BringToFront();
            foreach (Control control in new Control[] { bar, title, name, mark }) WireWindowDrag(control);
            return bar;
        }

        private Control BuildMainContent()
        {
            var body = new Panel { Dock = DockStyle.Fill, BackColor = AetherColors.Void };
            desktopNavigation = BuildDesktopNavigation();
            desktopPlay = new Panel { Dock = DockStyle.Fill, BackColor = AetherColors.Void, Padding = new Padding(24) };
            desktopHeader = new Panel { Dock = DockStyle.Top, Height = 84, BackColor = AetherColors.Void };
            aetherRomTitle = new AetherHeading { Dock = DockStyle.Top, Padding = new Padding(0, 0, 220, 0) };
            aetherModelValue = CreateUiLabel("", 9.75f, FontStyle.Regular, AetherColors.Muted, DockStyle.Top, 24);
            aetherStateValue = CreateUiLabel("", 9.75f, FontStyle.Regular, AetherColors.Cyan, DockStyle.None, 30, ContentAlignment.MiddleRight);
            aetherStateValue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            desktopHeader.Controls.Add(aetherModelValue); desktopHeader.Controls.Add(aetherRomTitle); desktopHeader.Controls.Add(aetherStateValue);
            aetherStateValue.BringToFront();
            desktopHeader.SizeChanged += (_, _) => aetherStateValue.Bounds = new Rectangle(Math.Max(0, desktopHeader.Width - 220), 10, 220, 30);
            desktopColumns = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = AetherColors.Void, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            desktopColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            desktopColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 244));
            desktopColumns.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var game = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 20, 0), BackColor = AetherColors.Void };
            aetherStage = new AetherStagePanel { Dock = DockStyle.Fill, Name = "aetherStage" };
            gameView.Dock = DockStyle.Fill; gameView.Margin = Padding.Empty; gameView.BackColor = Color.Black;
            aetherStage.Controls.Add(gameView);
            aetherEmptyState = BuildEmptyState(); aetherStage.Controls.Add(aetherEmptyState);
            desktopTransport = (Panel)BuildCommandDeck();
            desktopTransport.SizeChanged += (_, _) => ArrangeDesktopTransport();
            game.Controls.Add(aetherStage); game.Controls.Add(desktopTransport);
            aetherSessionRail = BuildSessionRail();
            desktopRailViewport = new AetherScrollViewport { Dock = DockStyle.Fill, Margin = Padding.Empty, Name = "aetherSessionRailViewport" };
            desktopRailViewport.SetContent(aetherSessionRail, new Size(0, 500));
            desktopColumns.Controls.Add(game, 0, 0); desktopColumns.Controls.Add(desktopRailViewport, 1, 0);
            desktopPlay.Controls.Add(desktopColumns); desktopPlay.Controls.Add(desktopHeader);
            body.Controls.Add(desktopPlay); body.Controls.Add(desktopNavigation);
            body.SizeChanged += (_, _) => ArrangeDesktop();
            return body;
        }

        internal void NavigateDesktop(string page)
        {
            if (page == "play") { controlCenter?.Close(); Activate(); gameView.Focus(); }
            else if (page == "library") menuOpen_Click(this, EventArgs.Empty);
            else if (page == "link") ShowOnlineRoomDialog(true);
            else { OpenControlCenter(); controlCenter?.NavigateTo(page == "settings" ? "appearance" : page); }
        }

        private Panel BuildDesktopNavigation()
        {
            var nav = new Panel { Dock = DockStyle.Left, Width = 72, BackColor = AetherColors.Chrome, Padding = new Padding(12, 28, 12, 16), Name = "aetherDesktopNavigation" };
            void Add(string label, UiIcon icon, Action action, int index, bool selected = false)
            {
                var button = CreateActionButton(UiText.Get(label), AetherButtonKind.Ghost, 48);
                button.Icon = icon; button.IconOnly = true; button.AlignLeft = true; button.Selected = selected;
                button.AccessibleName = button.Text; button.Name = "desktopNav" + icon;
                button.SetBounds(12, 28 + index * 52, 48, 46);
                button.Click += (_, _) => action(); nav.Controls.Add(button);
                if (icon == UiIcon.Settings) { button.Dock = DockStyle.Bottom; aetherControlCenterButton = button; }
            }
            Add("Spielen", UiIcon.Play, () => gameView.Focus(), 0, true);
            Add("Bibliothek", UiIcon.Library, () => menuOpen_Click(this, EventArgs.Empty), 1);
            Add("Spielstände", UiIcon.Save, () => { OpenControlCenter(); controlCenter?.NavigateTo("saves"); }, 2);
            Add("Link", UiIcon.Link, () => ShowOnlineRoomDialog(true), 3);
            Add("Werkzeuge", UiIcon.Tools, () => { OpenControlCenter(); controlCenter?.NavigateTo("tools"); }, 4);
            Add("Einstellungen", UiIcon.Settings, () => { OpenControlCenter(); controlCenter?.NavigateTo("appearance"); }, 5);
            return nav;
        }

        private Control BuildDesktopStatus()
        {
            var footer = new Panel { Name = "aetherDesktopStatus", Dock = DockStyle.Bottom, Height = 32, BackColor = AetherColors.Chrome, Padding = new Padding(24, 0, 24, 0) };
            desktopController = CreateUiLabel(UiText.Get("Tastatur"), 8.25f, FontStyle.Regular, AetherColors.Muted, DockStyle.Fill, 32);
            var hint = CreateUiLabel(UiText.Get("F11  Vollbild"), 8.25f, FontStyle.Regular, AetherColors.Muted, DockStyle.Right, 32, ContentAlignment.MiddleRight);
            hint.Width = 150; footer.Controls.Add(desktopController); footer.Controls.Add(hint);
            return footer;
        }

        private void ArrangeDesktop()
        {
            if (arrangingDesktop || desktopNavigation is null || desktopColumns is null) return;
            float scale = Math.Max(1, aetherUiScale * DeviceDpi / 96f);
            bool playing = session?.LatestSnapshot.Rom is not null;
            Size bodySize = desktopNavigation.Parent?.ClientSize ?? Size.Empty;
            // Session status refreshes every frame; rearrange only when geometry or page changes.
            if (desktopLayoutClient == ClientSize && desktopLayoutBody == bodySize &&
                desktopLayoutScale == scale && desktopLayoutPlaying == playing) return;
            arrangingDesktop = true;
            try
            {
                var layout = UiDesktopLayout.Create(ClientSize.Width / scale, ClientSize.Height / scale);
                desktopNavigation.Width = (int)(layout.Navigation * scale);
                int navIndex = 0;
                float step = Math.Min(52 * scale, (desktopNavigation.Height - desktopNavigation.Padding.Vertical) / 6f);
                int navHeight = Math.Max(28, (int)Math.Min(46 * scale, step - 6));
                foreach (AetherButton button in desktopNavigation.Controls.OfType<AetherButton>())
                {
                    button.Dock = DockStyle.None;
                    button.SetBounds(desktopNavigation.Padding.Left,
                        button.Icon == UiIcon.Settings ? desktopNavigation.Height - desktopNavigation.Padding.Bottom - navHeight : desktopNavigation.Padding.Top + (int)(navIndex * step),
                        desktopNavigation.Width - desktopNavigation.Padding.Horizontal, navHeight);
                    button.IconOnly = layout.Navigation < 100; navIndex++;
                }
                desktopPlay.Padding = new Padding((int)(layout.Inset * scale));
                desktopHeader.Visible = playing; desktopTransport.Visible = playing; desktopRailViewport.Visible = playing;
                desktopColumns.ColumnStyles[1].Width = playing ? layout.RailWidth * scale : 0;
                desktopColumns.Controls[0].Margin = playing ? new Padding(0, 0, (int)(20 * scale), 0) : Padding.Empty;
                desktopRailViewport.SetMinimumContent(new Size(0, (int)(550 * scale)));
                ArrangeDesktopTransport();
                desktopLayoutClient = ClientSize; desktopLayoutBody = bodySize;
                desktopLayoutScale = scale; desktopLayoutPlaying = playing;
            }
            finally { arrangingDesktop = false; }
        }

        private void ArrangeDesktopTransport()
        {
            if (aetherPauseButton is null) return;
            float scale = Math.Max(1, aetherUiScale * DeviceDpi / 96f);
            bool narrow = desktopTransport.Width < 440 * scale;
            aetherRewindButton.Visible = aetherTurboButton.Visible = !narrow;
            aetherPauseButton.IconOnly = desktopTransport.Width < 350 * scale;
            aetherPauseButton.Width = (int)((aetherPauseButton.IconOnly ? 44 : 156) * scale);
        }

        private Panel BuildEmptyState()
        {
            var empty = new Panel { BackColor = AetherColors.Void, Dock = DockStyle.Fill, Name = "aetherEmptyState" };
            var layout = new TableLayoutPanel { ColumnCount = 1, RowCount = 8, BackColor = AetherColors.Void };
            foreach (float height in new[] { 0f, 110, 58, 42, 56, 40, 52, 0 })
                layout.RowStyles.Add(height == 0 ? new RowStyle(SizeType.Percent, 50) : new RowStyle(SizeType.Absolute, height));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var mark = new PictureBox { Anchor = AnchorStyles.None, Size = new Size(90, 90), SizeMode = PictureBoxSizeMode.Zoom, TabStop = false };
            Branding.AppBrand.BindMark(mark);
            layout.Controls.Add(mark, 0, 1);
            layout.Controls.Add(CreateUiLabel(UiText.Get("Öffne ein Spiel"), 21, FontStyle.Bold, AetherColors.Text, DockStyle.Fill, 58, ContentAlignment.MiddleCenter), 0, 2);
            layout.Controls.Add(CreateUiLabel(UiText.Get("Wähle eine ROM-Datei oder ziehe sie in dieses Fenster."), 10.5f, FontStyle.Regular, AetherColors.Muted, DockStyle.Fill, 42, ContentAlignment.MiddleCenter), 0, 3);
            var actions = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.None, WrapContents = false, Margin = Padding.Empty };
            aetherOpenButton = CreateActionButton(UiText.Get("Spiel öffnen"), AetherButtonKind.Primary, 196);
            aetherOpenButton.Icon = UiIcon.Open; aetherOpenButton.Click += (_, _) => OpenRomFromAetherUi();
            var library = CreateActionButton(UiText.Get("Zur Bibliothek"), AetherButtonKind.Secondary, 172);
            library.Icon = UiIcon.Library; library.Click += (_, _) => menuOpen_Click(this, EventArgs.Empty);
            actions.Controls.Add(aetherOpenButton); actions.Controls.Add(library); layout.Controls.Add(actions, 0, 4);
            layout.Controls.Add(CreateUiLabel(UiText.Get("Dateiformate: .gb, .gbc, .gba"), 9, FontStyle.Regular, AetherColors.Muted, DockStyle.Fill, 40, ContentAlignment.MiddleCenter), 0, 5);
            layout.Controls.Add(CreateUiLabel(UiText.Get("Strg + O   Spiel öffnen"), 9, FontStyle.Regular, AetherColors.Muted, DockStyle.Fill, 52, ContentAlignment.MiddleCenter), 0, 6);
            var viewport = new AetherScrollViewport { Dock = DockStyle.Fill, Name = "desktopStartViewport" };
            viewport.SetContent(layout, new Size(400, 370));
            viewport.SizeChanged += (_, _) =>
            {
                float scale = Math.Max(1, aetherUiScale * DeviceDpi / 96f);
                viewport.SetMinimumContent(new Size((int)(400 * scale), (int)(370 * scale)));
            };
            empty.Controls.Add(viewport); return empty;
        }

        private AetherSurfacePanel BuildSessionRail()
        {
            var rail = new AetherSurfacePanel { AccentEdge = false, BackColor = AetherColors.Surface, Name = "aetherSessionRail", Padding = new Padding(16) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 12, BackColor = AetherColors.Surface, Margin = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 38, 48, 30, 56, 56, 36, 30, 36, 36, 36, 48, 0 })
                layout.RowStyles.Add(height == 0 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, height));
            layout.Controls.Add(CreateUiLabel(UiText.Get("Spielzustand"), 12, FontStyle.Bold, AetherColors.Text, DockStyle.Fill, 38), 0, 0);
            desktopSlot = new AetherSelect { Dock = DockStyle.Fill, Name = "desktopSaveSlot", AccessibleName = UiText.Get("Speicherplatz"), Margin = new Padding(0, 0, 0, 4) };
            for (int slot = 1; slot <= 5; slot++) desktopSlot.Items.Add(UiText.Format("Platz {0}", slot));
            desktopSlot.SelectedIndex = Math.Clamp(settings.SaveSlot - 1, 0, 4);
            desktopSlot.SelectedIndexChanged += (_, _) => { if (desktopSlot.SelectedIndex >= 0) { SelectSaveSlot(desktopSlot.SelectedIndex + 1); UpdateAetherSlotButtons(); } };
            layout.Controls.Add(desktopSlot, 0, 1);
            aetherSlotValue = CreateUiLabel("", 9, FontStyle.Regular, AetherColors.Muted, DockStyle.Fill, 30); layout.Controls.Add(aetherSlotValue, 0, 2);
            aetherSaveButton = CreateActionButton(UiText.Get("Zustand speichern"), AetherButtonKind.Secondary, 192); aetherSaveButton.Icon = UiIcon.Save;
            aetherSaveButton.Click += (_, _) => { QuickSave(); gameView.Focus(); };
            aetherLoadButton = CreateActionButton(UiText.Get("Zustand laden"), AetherButtonKind.Secondary, 192); aetherLoadButton.Icon = UiIcon.Rewind;
            aetherLoadButton.Click += (_, _) => { QuickLoad(); gameView.Focus(); };
            foreach (var button in new[] { aetherSaveButton, aetherLoadButton }) { button.Dock = DockStyle.Fill; button.Margin = new Padding(0, 0, 0, 12); }
            layout.Controls.Add(aetherSaveButton, 0, 3); layout.Controls.Add(aetherLoadButton, 0, 4);
            layout.Controls.Add(CreateUiLabel(UiText.Get("F5 save · F8 load"), 8.25f, FontStyle.Regular, AetherColors.Muted, DockStyle.Fill, 36), 0, 5);
            layout.Controls.Add(CreateUiLabel(UiText.Get("Anzeige"), 10.5f, FontStyle.Bold, AetherColors.Text, DockStyle.Fill, 30), 0, 6);
            layout.Controls.Add(CreateMetricRow("Audio", out aetherAudioValue), 0, 7);
            layout.Controls.Add(CreateMetricRow(UiText.Get("Bildfilter"), out aetherFilterValue), 0, 8);
            layout.Controls.Add(CreateMetricRow(UiText.Get("Steuerung"), out aetherInputValue), 0, 9);
            aetherSaveSafetyButton = CreateActionButton(UiText.Get("Manage saves"), AetherButtonKind.Ghost, 192);
            aetherSaveSafetyButton.Dock = DockStyle.Fill; aetherSaveSafetyButton.Margin = Padding.Empty;
            aetherSaveSafetyButton.Click += (_, args) => menuBatterySaveSafety_Click(aetherSaveSafetyButton, args);
            layout.Controls.Add(aetherSaveSafetyButton, 0, 10);
            var more = CreateNavButton(UiText.Get("Weitere Aktionen"), desktopCommands, "SYSTEM"); more.Icon = UiIcon.More; more.Dock = DockStyle.Bottom; more.Height = 44;
            layout.Controls.Add(more, 0, 11);
            aetherStatusDot = new AetherStatusDot { Visible = false }; rail.Controls.Add(aetherStatusDot);
            rail.Controls.Add(layout); return rail;
        }

        private Control BuildCommandDeck()
        {
            var deck = new Panel { Dock = DockStyle.Bottom, Height = 68, Padding = new Padding(0, 16, 0, 0), BackColor = AetherColors.Void };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            aetherPauseButton = CreateActionButton("Pause", AetherButtonKind.Primary, 156); aetherPauseButton.Icon = UiIcon.Play;
            aetherPauseButton.Click += aetherPauseButton_Click;
            aetherRewindButton = CreateActionButton(UiText.Get("Rewind"), AetherButtonKind.Secondary, 44); aetherRewindButton.Icon = UiIcon.Rewind; aetherRewindButton.IconOnly = true;
            aetherRewindButton.AccessibleName = aetherRewindButton.Text; aetherRewindButton.Click += (_, args) => menuRewind_Click(aetherRewindButton, args);
            aetherTurboButton = CreateActionButton("Turbo", AetherButtonKind.Secondary, 44); aetherTurboButton.Icon = UiIcon.Turbo; aetherTurboButton.IconOnly = true;
            aetherTurboButton.AccessibleName = "Turbo"; aetherTurboButton.Click += aetherTurboButton_Click;
            aetherDeckOpenButton = CreateNavButton(UiText.Get("Weitere Aktionen"), desktopCommands, "GAME"); aetherDeckOpenButton.Icon = UiIcon.More; aetherDeckOpenButton.IconOnly = true; aetherDeckOpenButton.Width = 44; aetherDeckOpenButton.Height = 44;
            var fullscreen = CreateActionButton(UiText.Get("Vollbild"), AetherButtonKind.Ghost, 44); fullscreen.Icon = UiIcon.Fullscreen; fullscreen.IconOnly = true; fullscreen.AccessibleName = fullscreen.Text;
            fullscreen.Dock = DockStyle.Right; fullscreen.Click += (_, _) => ToggleAetherFullscreen();
            actions.Controls.AddRange([aetherPauseButton, aetherRewindButton, aetherTurboButton, aetherDeckOpenButton]);
            deck.Controls.Add(actions); deck.Controls.Add(fullscreen); return deck;
        }

        private AetherButton CreateChromeButton(
            string text,
            string accessibleName,
            EventHandler click,
            AetherButtonKind kind = AetherButtonKind.Ghost)
        {
            AetherButton button = CreateActionButton(text, kind, 38);
            button.AccessibleName = accessibleName;
            button.Height = 36;
            button.Margin = new Padding(3, 0, 0, 0);
            button.Font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point);
            button.Click += click;
            return button;
        }

        private AetherButton CreateNavButton(string text, nanoboy.Controls.AetherCommand menu, string identity)
        {
            AetherButton button = CreateActionButton(text, AetherButtonKind.Ghost, 72);
            button.Width = Math.Max(72, TextRenderer.MeasureText(text, button.Font, Size.Empty, TextFormatFlags.NoPadding).Width + 24);
            button.Height = 34;
            button.Margin = new Padding(2, 0, 2, 0);
            button.Name = "aetherNav" + identity;
            button.Click += (_, _) =>
            {
                bool same = aetherActiveNav == button;
                aetherCommandMenu?.Dismiss();
                if (same) return;
                input.Clear();
                keyboardAdvanceButtons = gamepadAdvanceButtons = GameBoyAdvanceButtons.None;
                turboPressed = false;
                gamepadAwaitNeutral = true;
                if (session is not null)
                {
                    ObserveSessionCommand(session.SetButtonsAsync(GameBoyButtons.None));
                    ObserveSessionCommand(session.SetGameBoyAdvanceButtonsAsync(GameBoyAdvanceButtons.None));
                    if (!IsOnlineLink) ObserveSessionCommand(session.SetTurboAsync(false));
                }
                aetherActiveNav = button;
                button.Selected = true;
                aetherCommandMenu = new AetherCommandMenu(this, aetherRoot, button, menu, text, () =>
                {
                    button.Selected = false;
                    aetherCommandMenu = null;
                    aetherActiveNav = null;
                    gamepadAwaitNeutral = true;
                });
            };
            return button;
        }

        private static AetherButton CreateActionButton(string text, AetherButtonKind kind, int width)
        {
            return new AetherButton
            {
                Kind = kind,
                Margin = new Padding(0, 0, 9, 0),
                Size = new Size(width, 44),
                Text = text
            };
        }

        private static Label CreateUiLabel(
            string text,
            float size,
            FontStyle style,
            Color color,
            DockStyle dock,
            int height,
            ContentAlignment alignment = ContentAlignment.MiddleLeft)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                Dock = dock,
                Font = AetherTypography.Create(size, style),
                ForeColor = color,
                Height = height,
                Text = text,
                TextAlign = alignment
            };
        }

        private static Panel CreateMetricRow(string label, out Label value)
        {
            var row = new Panel { BackColor = AetherColors.Surface, Dock = DockStyle.Fill };
            Label key = CreateUiLabel(label, 9f, FontStyle.Regular, AetherColors.Muted, DockStyle.Left, 30);
            key.Width = 80;
            value = CreateUiLabel("—", 9f, FontStyle.Bold, AetherColors.Text, DockStyle.Fill, 30, ContentAlignment.MiddleRight);
            row.Controls.Add(value);
            row.Controls.Add(key);
            return row;
        }

        private static Panel CreateDivider()
        {
            return new Panel
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                BackColor = AetherColors.Hairline,
                Height = 1,
                Margin = new Padding(0, 8, 0, 8)
            };
        }

        private void UpdateAetherGamepadUi(Input.HostGamepadState state)
        {
            if (!aetherShellInitialized || aetherInputValue == null)
            {
                return;
            }

            if (desktopController is not null) desktopController.Text = UiText.Get(state.IsConnected ? "Controller verbunden" : "Tastatur");
            aetherInputValue.Text = state.IsConnected ? global::AetherBoy.Runtime.Localization.UiText.Get("Gamepad") : global::AetherBoy.Runtime.Localization.UiText.Get("Keyboard");
        }

        private void OpenRomFromAetherUi()
        {
            if (romPreparation != null)
            {
                CancelRomPreparation();
                return;
            }
            using var picker = new AetherFileDialog { Title = UiText.Get("Spiel öffnen"), Filter = UiText.Get("Spiele und Archive") + "|*.gb;*.gbc;*.gba;*.zip;*.7z", InitialDirectory = WindowsDataPaths.Default.Roms };
            if (picker.ShowDialog(this) == DialogResult.OK) _ = PrepareRomLoadAsync(picker.FileName);
            gameView.Focus();
        }

        private void aetherPauseButton_Click(object? sender, EventArgs e)
        {
            focusPausedSession = null; // An explicit user decision takes over from automatic focus pause.
            if (stateOperationInProgress) return;
            EmulationSession? currentSession = session;
            if (currentSession == null)
            {
                return;
            }

            bool pause = !currentSession.LatestSnapshot.IsPaused;
            ObserveSessionCommand(currentSession.SetPausedAsync(pause));
            gameView.Focus();
        }

        private void aetherTurboButton_Click(object? sender, EventArgs e)
        {
            if (stateOperationInProgress || IsOnlineLink) return;
            EmulationSession? currentSession = session;
            if (currentSession == null)
            {
                return;
            }

            bool turbo = !currentSession.LatestSnapshot.IsTurboEnabled;
            ObserveSessionCommand(currentSession.SetTurboAsync(turbo));
            gameView.Focus();
        }

        private void UpdateAetherSessionUi(EmulationSnapshot? snapshot)
        {
            if (!aetherShellInitialized)
            {
                return;
            }

            bool hasSession = session != null && snapshot != null;
            bool hasRom = snapshot?.Rom != null;
            aetherSaveSlots.Update(hasRom && !IsOnlineLink ? session : null,
                hasRom && !IsOnlineLink ? currentRomPath : null);
            aetherEmptyState.Visible = !hasRom;
            gameView.Visible = hasRom || immersiveFullscreen;
            if (!hasRom)
            {
                aetherEmptyState.BringToFront();
            }

            aetherRomTitle.Text = snapshot?.Rom?.Title?.Trim() is { Length: > 0 } title
                ? title
                : hasSession ? global::AetherBoy.Runtime.Localization.UiText.Get("Loading game") : global::AetherBoy.Runtime.Localization.UiText.Get("No game open");
            aetherModelValue.Text = snapshot?.Rom == null
                ? "—"
                : snapshot.Rom.IsGameBoyAdvance
                    ? "Game Boy Advance"
                    : snapshot.Rom.HasColorFeatures ? "Game Boy Color" : "Game Boy";
            aetherAudioValue.Text = settings.AudioEnable ? global::AetherBoy.Runtime.Localization.UiText.Get("On") : global::AetherBoy.Runtime.Localization.UiText.Get("Muted");
            aetherFilterValue.Text = gameView.Filter switch
            {
                GameDisplayFilter.Smooth => global::AetherBoy.Runtime.Localization.UiText.Get("Smooth"),
                GameDisplayFilter.LcdGrid => global::AetherBoy.Runtime.Localization.UiText.Get("LCD grid"),
                _ => global::AetherBoy.Runtime.Localization.UiText.Get("Sharp")
            };
            aetherSlotValue.Text = settings.SaveSlot.ToString();

            SessionState? state = snapshot?.State;
            string stateText = state switch
            {
                SessionState.Starting => global::AetherBoy.Runtime.Localization.UiText.Get("Lädt"),
                SessionState.Running when snapshot?.IsTurboEnabled == true => "Turbo",
                SessionState.Running => global::AetherBoy.Runtime.Localization.UiText.Get("Spielt"),
                SessionState.Paused => global::AetherBoy.Runtime.Localization.UiText.Get("Pausiert"),
                SessionState.Stopping => global::AetherBoy.Runtime.Localization.UiText.Get("Beendet…"),
                SessionState.Stopped => global::AetherBoy.Runtime.Localization.UiText.Get("Beendet"),
                SessionState.Faulted => global::AetherBoy.Runtime.Localization.UiText.Get("Fehler"),
                _ => global::AetherBoy.Runtime.Localization.UiText.Get("Bereit")
            };
            aetherStateValue.Text = hasRom && !IsOnlineLink
                ? stateText + " · " + frmRomLibrary.FormatTime(sessionPlaySeconds)
                : stateText;
            aetherStatusDot.SignalColor = state switch
            {
                SessionState.Running => snapshot?.IsTurboEnabled == true ? AetherColors.Cyan : AetherColors.Success,
                SessionState.Paused => AetherColors.Pulse,
                SessionState.Starting or SessionState.Stopping => AetherColors.Violet,
                SessionState.Faulted => AetherColors.Danger,
                _ => AetherColors.Muted
            };
            aetherStatusDot.Animated = state is
                SessionState.Starting or SessionState.Running or SessionState.Stopping;

            bool actionsEnabled = hasSession && state is not SessionState.Stopping and not SessionState.Stopped and not SessionState.Faulted;
            aetherPauseButton.Enabled = actionsEnabled && !stateOperationInProgress;
            EmulationFeature features = snapshot?.Rom is null
                ? EmulationFeature.GameBoyStandard
                : snapshot.Features;
            bool supportsSaveStates = !IsOnlineLink && (features & EmulationFeature.SaveStates) != 0;
            bool supportsRewind = !IsOnlineLink && (features & EmulationFeature.Rewind) != 0;
            aetherRewindButton.Enabled = actionsEnabled && supportsRewind && !stateOperationInProgress;
            aetherSaveButton.Enabled = actionsEnabled && supportsSaveStates && !stateOperationInProgress;
            aetherSaveSafetyButton.Enabled = actionsEnabled && !IsOnlineLink &&
                snapshot?.Rom?.BatterySave.IsEnabled == true &&
                snapshot.Rom.BatterySave.ExpectedLength > 0 &&
                !stateOperationInProgress;
            aetherTurboButton.Enabled = actionsEnabled && !stateOperationInProgress && !IsOnlineLink;
            menuSaveState.Enabled = supportsSaveStates;
            menuRewind.Enabled = supportsRewind;
            menuCheats.Enabled = !IsOnlineLink && (features & EmulationFeature.Cheats) != 0;
            menuPalette.Enabled = (features & EmulationFeature.MonochromePalettes) != 0;
            bool supportsAudioChannels = (features & EmulationFeature.AudioChannelControls) != 0;
            menuAudioC1.Enabled = supportsAudioChannels;
            menuAudioC2.Enabled = supportsAudioChannels;
            menuAudioC3.Enabled = supportsAudioChannels;
            menuAudioC4.Enabled = supportsAudioChannels;
            menuItem5.Enabled = supportsAudioChannels;
            menuAudioInspector.Enabled = (features & EmulationFeature.AudioInspector) != 0;
            aetherDeckOpenButton.Kind = hasRom ? AetherButtonKind.Secondary : AetherButtonKind.Primary;
            aetherPauseButton.Kind = hasRom ? AetherButtonKind.Primary : AetherButtonKind.Secondary;
            aetherPauseButton.Text = snapshot?.IsPaused == true ? global::AetherBoy.Runtime.Localization.UiText.Get("Resume") : "Pause";
            aetherPauseButton.Selected = snapshot?.IsPaused == true;
            aetherTurboButton.Selected = snapshot?.IsTurboEnabled == true;
            UpdateAetherSlotButtons();
            ArrangeDesktop();
        }

        private void UpdateAetherSlotButtons()
        {
            if (aetherLoadButton is not null)
            {
                EmulationSnapshot? current = session?.LatestSnapshot;
                aetherLoadButton.Enabled = current?.Rom is not null &&
                    current.State is not SessionState.Stopping and not SessionState.Stopped and not SessionState.Faulted &&
                    current.Supports(EmulationFeature.SaveStates) && !IsOnlineLink && !stateOperationInProgress &&
                    aetherSaveSlots[settings.SaveSlot] == AetherSaveSlotState.Saved;
            }
            if (desktopSlot is not null && desktopSlot.SelectedIndex != settings.SaveSlot - 1) desktopSlot.SelectedIndex = settings.SaveSlot - 1;
            if (aetherSlotValue != null)
            {
                aetherSlotValue.Text = global::AetherBoy.Runtime.Localization.UiText.Format("{0} · {1}", settings.SaveSlot,
                    AetherSaveSlotLabel(aetherSaveSlots[settings.SaveSlot]));
                aetherSlotValue.AutoEllipsis = true;
            }
        }

        private string AetherSaveSlotLabel(AetherSaveSlotState state) =>
            IsOnlineLink ? global::AetherBoy.Runtime.Localization.UiText.Get("Save States sind im Online-Link gesperrt") :
            global::AetherBoy.Runtime.Localization.UiText.Get(state switch
            {
                AetherSaveSlotState.Reading => "wird gelesen",
                AetherSaveSlotState.Empty => "leer",
                AetherSaveSlotState.Saved => "gespeichert",
                AetherSaveSlotState.Unreadable => "nicht lesbar",
                _ => "Spiel öffnen"
            });

        private void frmNano_DragEnter(object? sender, DragEventArgs e)
        {
            e.Effect = TryGetDroppedRom(e.Data, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void frmNano_DragDrop(object? sender, DragEventArgs e)
        {
            if (TryGetDroppedRom(e.Data, out string? path))
            {
                _ = PrepareRomLoadAsync(path);
                gameView.Focus();
            }
        }

        private static bool TryGetDroppedRom(IDataObject? data, out string? path)
        {
            return RomFiles.TryGetSingleDrop(data, out path);
        }

        private void WireWindowDrag(Control control)
        {
            control.MouseDown += (_, args) =>
            {
                if (args.Button != MouseButtons.Left)
                {
                    return;
                }

                if (args.Clicks > 1)
                {
                    ToggleAetherMaximize();
                    return;
                }

                ReleaseCapture();
                SendMessage(Handle, WmNcLeftButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
            };
        }

        private void ToggleAetherMaximize()
        {
            if (immersiveFullscreen) SetImmersiveFullscreen(false);
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
            UpdateAetherWindowButton();
        }

        private void ToggleAetherFullscreen()
        {
            SetImmersiveFullscreen(!immersiveFullscreen);
        }

        private void frmNano_AetherResize(object? sender, EventArgs e)
        {
            UpdateAetherWindowButton();
        }

        private void UpdateAetherWindowButton()
        {
            if (aetherMaximizeButton != null)
            {
                aetherMaximizeButton.Text = WindowState == FormWindowState.Maximized ? "❐" : "□";
                aetherMaximizeButton.AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get(
                    WindowState == FormWindowState.Maximized ? "Fenster wiederherstellen" : "Fenster maximieren");
            }
        }

        private Size GetAetherClientSize(int scale)
        {
            int normalizedScale = Math.Clamp(scale, 1, 4);
            int displayWidth = gameView.VideoGeometry.Width * normalizedScale;
            int displayHeight = gameView.VideoGeometry.Height * normalizedScale;
            Size logicalSize = new Size(
                Math.Max(1000, displayWidth + 360),
                Math.Max(740, displayHeight + 240));
            double dpiScale = (IsHandleCreated ? DeviceDpi / 96d : 1d) * aetherUiScale;
            return new Size((int)Math.Round(logicalSize.Width * dpiScale),
                (int)Math.Round(logicalSize.Height * dpiScale));
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (bootIntro is not null && keyData != (Keys.Alt | Keys.F4))
            {
                if (keyData is Keys.Escape or Keys.Enter or Keys.Space) bootIntro.Skip();
                return true;
            }
            FinishStoppedOnlineLink();
            if (keyData == (Keys.Control | Keys.Shift | Keys.F11) && sofaMode) { ExitSofaMode(); return true; }
            if (sofaMode && keyData == Keys.Escape) { _ = OpenQuickMenuAsync(); return true; }
            if (keyData == (Keys.Control | Keys.F10)) { ShowOnlineRoomDialog(true); return true; }
            if (keyData == (Keys.Control | Keys.Shift | Keys.F10)) { ShowOnlineRoomDialog(false); return true; }
            if (aetherCommandMenu is not null) return aetherCommandMenu.HandleNavigation(keyData);
            if (keyData == Keys.F10) { _ = OpenQuickMenuAsync(); return true; }
            if (keyData == Keys.F12) { _ = CaptureScreenshotAsync(); return true; }
            if (keyData == Keys.F9) { TogglePerformanceOverlay(); return true; }
            if (keyData == Keys.F7) { menuRewind_Click(this, EventArgs.Empty); return true; }
            if (keyData == (Keys.Control | Keys.K)) { OpenControlCenter(); controlCenter.FocusSettingsSearch(); return true; }
            if (keyData == Keys.F11 || keyData == (Keys.Alt | Keys.Enter))
            {
                if (sofaMode) { ExitSofaMode(); return true; }
                ToggleAetherFullscreen();
                return true;
            }
            if (keyData == Keys.Escape && immersiveFullscreen)
            {
                SetImmersiveFullscreen(false);
                return true;
            }
            if (keyData == (Keys.Control | Keys.O))
            {
                OpenRomFromAetherUi();
                return true;
            }

            if (keyData == Keys.F5)
            {
                QuickSave();
                return true;
            }

            if (keyData == Keys.F6)
            {
                OpenStateGallery();
                return true;
            }
            if (keyData == (Keys.Control | Keys.F8))
            {
                _ = LoadCheckpointAsync(0, undo: true);
                return true;
            }

            if (keyData == Keys.F8)
            {
                QuickLoad();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmNcHitTest && WindowState == FormWindowState.Normal && !immersiveFullscreen)
            {
                base.WndProc(ref message);
                if ((int)message.Result != 1)
                {
                    return;
                }

                long packed = message.LParam.ToInt64();
                Point clientPoint = PointToClient(new Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff)));
                int grip = Math.Max(6, (int)Math.Ceiling(DeviceDpi / 96f * 8f));
                bool left = clientPoint.X <= grip;
                bool right = clientPoint.X >= ClientSize.Width - grip;
                bool top = clientPoint.Y <= grip;
                bool bottom = clientPoint.Y >= ClientSize.Height - grip;

                if (left && top) message.Result = (IntPtr)HtTopLeft;
                else if (right && top) message.Result = (IntPtr)HtTopRight;
                else if (left && bottom) message.Result = (IntPtr)HtBottomLeft;
                else if (right && bottom) message.Result = (IntPtr)HtBottomRight;
                else if (left) message.Result = (IntPtr)HtLeft;
                else if (right) message.Result = (IntPtr)HtRight;
                else if (top) message.Result = (IntPtr)HtTop;
                else if (bottom) message.Result = (IntPtr)HtBottom;
                return;
            }

            base.WndProc(ref message);
        }
    }
}
