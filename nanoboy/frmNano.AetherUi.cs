using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy.Controls;
using nanoboy.Core;

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
        private AetherButton[] aetherSlotButtons = Array.Empty<AetherButton>();
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
            Padding = new Padding(1);

            aetherRoot = new Panel
            {
                BackColor = AetherColors.Void,
                Dock = DockStyle.Fill,
                Padding = Padding.Empty,
                Name = "aetherRoot"
            };

            aetherTitleBar = BuildTitleBar();
            Control commandDeck = BuildCommandDeck();
            Control content = BuildMainContent();

            aetherRoot.Controls.Add(content);
            aetherRoot.Controls.Add(commandDeck);
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
            var titleBar = new AetherChromePanel
            {
                Dock = DockStyle.Top,
                Height = 64,
                Padding = new Padding(0, 0, 0, 2), // Keep the accent line below docked title-bar children.
                Name = "aetherTitleBar"
            };

            var identity = new Panel
            {
                BackColor = AetherColors.Chrome,
                Dock = DockStyle.Left,
                Width = 300,
                Padding = new Padding(15, 9, 0, 7)
            };
            var mark = new PictureBox
            {
                Dock = DockStyle.Left,
                Size = new Size(46, 46),
                SizeMode = PictureBoxSizeMode.Zoom,
                TabStop = false
            };
            Branding.AppBrand.BindMark(mark);

            var nameStack = new Panel
            {
                BackColor = AetherColors.Chrome,
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 3, 0, 0)
            };
            var productName = CreateUiLabel(
                "AetherBoy",
                13f,
                FontStyle.Bold,
                AetherColors.Text,
                DockStyle.Top,
                24);
            var productSignal = CreateUiLabel(
                "GAME BOY · COLOR · ADVANCE",
                8.5f,
                FontStyle.Bold,
                AetherColors.Muted,
                DockStyle.Top,
                18);
            productSignal.Name = "aetherTeamSignature";
            nameStack.Controls.Add(productSignal);
            nameStack.Controls.Add(productName);
            identity.Controls.Add(nameStack);
            identity.Controls.Add(mark);

            var windowControls = new FlowLayoutPanel
            {
                BackColor = AetherColors.Chrome,
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 12, 10, 0),
                WrapContents = false,
                Width = 136
            };
            AetherButton minimize = CreateChromeButton("—", (_, _) => WindowState = FormWindowState.Minimized);
            aetherMaximizeButton = CreateChromeButton("□", (_, _) => ToggleAetherMaximize());
            AetherButton close = CreateChromeButton("×", (_, _) => Close(), AetherButtonKind.Danger);
            windowControls.Controls.Add(minimize);
            windowControls.Controls.Add(aetherMaximizeButton);
            windowControls.Controls.Add(close);

            var navigation = new FlowLayoutPanel
            {
                BackColor = AetherColors.Chrome,
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 13, 12, 0),
                WrapContents = false,
                Width = 330
            };
            navigation.Controls.Add(CreateNavButton(global::AetherBoy.Runtime.Localization.UiText.Get("GAME"), menuFile, "SYSTEM"));
            navigation.Controls.Add(CreateNavButton(global::AetherBoy.Runtime.Localization.UiText.Get("VIDEO"), menuItem1, "TUNE"));
            navigation.Controls.Add(CreateNavButton(global::AetherBoy.Runtime.Localization.UiText.Get("TOOLS"), menuItem21, "TOOLS"));
            navigation.Controls.Add(CreateNavButton(global::AetherBoy.Runtime.Localization.UiText.Get("HELP"), menuItem4, "INFO"));
            navigation.Width = navigation.Padding.Horizontal + navigation.Controls.Cast<Control>().Sum(button => button.Width + button.Margin.Horizontal);

            titleBar.Controls.Add(navigation);
            titleBar.Controls.Add(windowControls);
            titleBar.Controls.Add(identity);
            WireWindowDrag(titleBar);
            WireWindowDrag(identity);
            WireWindowDrag(nameStack);
            WireWindowDrag(productName);
            WireWindowDrag(productSignal);
            WireWindowDrag(mark);
            return titleBar;
        }

        private Control BuildMainContent()
        {
            var contentHost = new Panel
            {
                BackColor = AetherColors.Void,
                Dock = DockStyle.Fill,
                Padding = new Padding(22, 20, 22, 18)
            };
            var layout = new TableLayoutPanel
            {
                BackColor = AetherColors.Void,
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                RowCount = 1
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 248f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            aetherStage = new AetherStagePanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 14, 0),
                Name = "aetherStage"
            };
            gameView.Dock = DockStyle.Fill;
            gameView.Margin = Padding.Empty;
            gameView.BackColor = Color.Black;
            aetherStage.Controls.Add(gameView);
            aetherEmptyState = BuildEmptyState();
            aetherStage.Controls.Add(aetherEmptyState);
            aetherEmptyState.BringToFront();

            aetherSessionRail = BuildSessionRail();
            aetherSessionRail.Dock = DockStyle.None;
            var railViewport = new AetherScrollViewport { Dock = DockStyle.Fill, Margin = Padding.Empty,
                Name = "aetherSessionRailViewport" };
            railViewport.SetContent(aetherSessionRail, new Size(0, 438));
            void FitRail() => railViewport.SetMinimumContent(new Size(0,
                (int)Math.Ceiling(438 * aetherUiScale * DeviceDpi / 96f)));
            railViewport.SizeChanged += (_, _) => FitRail();
            layout.Controls.Add(aetherStage, 0, 0);
            layout.Controls.Add(railViewport, 1, 0);
            contentHost.Controls.Add(layout);
            return contentHost;
        }

        private Panel BuildEmptyState()
        {
            var empty = new Panel
            {
                BackColor = AetherColors.Void,
                Dock = DockStyle.Fill,
                Name = "aetherEmptyState"
            };
            var layout = new TableLayoutPanel
            {
                BackColor = AetherColors.Void,
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                RowCount = 7
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            var mark = new PictureBox
            {
                Anchor = AnchorStyles.None,
                Size = new Size(104, 104),
                SizeMode = PictureBoxSizeMode.Zoom,
                TabStop = false
            };
            Branding.AppBrand.BindMark(mark);
            var headline = CreateUiLabel(
                global::AetherBoy.Runtime.Localization.UiText.Get("Ready for your next game?"),
                15f,
                FontStyle.Bold,
                AetherColors.Text,
                DockStyle.Fill,
                34,
                ContentAlignment.MiddleCenter);
            var copy = CreateUiLabel(
                global::AetherBoy.Runtime.Localization.UiText.Get("Open a game or drop a .gb, .gbc or .gba file here."),
                9f,
                FontStyle.Regular,
                AetherColors.Muted,
                DockStyle.Fill,
                48,
                ContentAlignment.TopCenter);
            aetherOpenButton = CreateActionButton(global::AetherBoy.Runtime.Localization.UiText.Get("Spiel öffnen"), AetherButtonKind.Primary, 158);
            aetherOpenButton.Anchor = AnchorStyles.None;
            aetherOpenButton.Click += (_, _) => OpenRomFromAetherUi();
            var hint = CreateUiLabel(
                global::AetherBoy.Runtime.Localization.UiText.Get("SHORTCUT: CTRL+O"),
                8.5f,
                FontStyle.Bold,
                AetherColors.Muted,
                DockStyle.Fill,
                30,
                ContentAlignment.MiddleCenter);

            layout.Controls.Add(new Panel { BackColor = AetherColors.Void }, 0, 0);
            layout.Controls.Add(mark, 0, 1);
            layout.Controls.Add(headline, 0, 2);
            layout.Controls.Add(copy, 0, 3);
            layout.Controls.Add(aetherOpenButton, 0, 4);
            layout.Controls.Add(hint, 0, 5);
            layout.Controls.Add(new Panel { BackColor = AetherColors.Void }, 0, 6);
            empty.Controls.Add(layout);
            return empty;
        }

        private AetherSurfacePanel BuildSessionRail()
        {
            var rail = new AetherSurfacePanel
            {
                AccentEdge = true,
                BackColor = AetherColors.Surface,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Name = "aetherSessionRail",
                Padding = new Padding(20, 18, 18, 16)
            };
            var layout = new TableLayoutPanel
            {
                BackColor = AetherColors.Surface,
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                RowCount = 13
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 12f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));

            layout.Controls.Add(CreateUiLabel(global::AetherBoy.Runtime.Localization.UiText.Get("NOW PLAYING"), 8.5f, FontStyle.Bold, AetherColors.Muted, DockStyle.Fill, 24), 0, 0);
            aetherRomTitle = CreateUiLabel(global::AetherBoy.Runtime.Localization.UiText.Get("No game open"), 12f, FontStyle.Bold, AetherColors.Text, DockStyle.Fill, 58);
            aetherRomTitle.AutoEllipsis = true;
            layout.Controls.Add(aetherRomTitle, 0, 1);

            var stateRow = new Panel { BackColor = AetherColors.Surface, Dock = DockStyle.Fill };
            aetherStatusDot = new AetherStatusDot { Location = new Point(0, 8) };
            aetherStateValue = CreateUiLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Ready"), 8.5f, FontStyle.Bold, AetherColors.Muted, DockStyle.Fill, 30);
            aetherStateValue.Padding = new Padding(22, 0, 0, 0);
            stateRow.Controls.Add(aetherStateValue);
            stateRow.Controls.Add(aetherStatusDot);
            layout.Controls.Add(stateRow, 0, 2);

            layout.Controls.Add(CreateDivider(), 0, 3);
            layout.Controls.Add(CreateMetricRow(global::AetherBoy.Runtime.Localization.UiText.Get("System"), out aetherModelValue), 0, 4);
            layout.Controls.Add(CreateMetricRow("Audio", out aetherAudioValue), 0, 5);
            layout.Controls.Add(CreateMetricRow(global::AetherBoy.Runtime.Localization.UiText.Get("Video"), out aetherFilterValue), 0, 6);
            layout.Controls.Add(CreateMetricRow(global::AetherBoy.Runtime.Localization.UiText.Get("Controls"), out aetherInputValue), 0, 7);
            aetherInputValue.AutoEllipsis = true;
            layout.Controls.Add(CreateMetricRow("Slot", out aetherSlotValue), 0, 8);
            layout.Controls.Add(CreateUiLabel(global::AetherBoy.Runtime.Localization.UiText.Get("SAVE SLOT"), 8.5f, FontStyle.Bold, AetherColors.Muted, DockStyle.Fill, 30), 0, 9);

            var slots = new FlowLayoutPanel
            {
                BackColor = AetherColors.Surface,
                Dock = DockStyle.Fill,
                // The 36px row includes 5px top padding and 30px buttons.
                // Default 3px table-cell margins would clip their lower edge.
                Margin = Padding.Empty,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 5, 0, 0),
                WrapContents = false
            };
            aetherSlotButtons = new AetherButton[5];
            for (int slot = 1; slot <= aetherSlotButtons.Length; slot++)
            {
                int selectedSlot = slot;
                AetherButton button = CreateActionButton(slot.ToString(), AetherButtonKind.Secondary, 34);
                button.Height = 30;
                button.Margin = new Padding(0, 0, 7, 0);
                button.Click += (_, _) =>
                {
                    SelectSaveSlot(selectedSlot);
                    UpdateAetherSlotButtons();
                    gameView.Focus();
                };
                aetherSlotButtons[slot - 1] = button;
                slots.Controls.Add(button);
            }
            layout.Controls.Add(slots, 0, 11);

            aetherSaveSafetyButton = CreateActionButton(
                global::AetherBoy.Runtime.Localization.UiText.Get("Manage saves"),
                AetherButtonKind.Ghost,
                190);
            aetherSaveSafetyButton.Dock = DockStyle.Bottom;
            aetherSaveSafetyButton.MinimumSize = new Size(0, 38);
            aetherSaveSafetyButton.Margin = Padding.Empty;
            aetherSaveSafetyButton.Click += (_, args) => menuBatterySaveSafety_Click(aetherSaveSafetyButton, args);
            layout.Controls.Add(aetherSaveSafetyButton, 0, 10);

            var hint = CreateUiLabel(
                global::AetherBoy.Runtime.Localization.UiText.Get("F5 save · F8 load\r\nF10 quick menu"),
                8.5f,
                FontStyle.Bold,
                AetherColors.Muted,
                DockStyle.Fill,
                40,
                ContentAlignment.BottomLeft);
            layout.Controls.Add(hint, 0, 12);
            rail.Controls.Add(layout);
            return rail;
        }

        private Control BuildCommandDeck()
        {
            var deck = new AetherSurfacePanel
            {
                BackColor = AetherColors.Chrome,
                Dock = DockStyle.Bottom,
                Height = 78,
                Padding = new Padding(22, 17, 22, 16)
            };
            var commands = new TableLayoutPanel
            {
                BackColor = AetherColors.Chrome,
                Dock = DockStyle.Fill,
                ColumnCount = 8,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            commands.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            aetherDeckOpenButton = CreateActionButton(global::AetherBoy.Runtime.Localization.UiText.Get("Spiel öffnen"), AetherButtonKind.Primary, 126);
            aetherDeckOpenButton.Click += (_, _) => OpenRomFromAetherUi();
            aetherControlCenterButton = CreateActionButton(global::AetherBoy.Runtime.Localization.UiText.Get("Settings"), AetherButtonKind.Secondary, 94);
            aetherControlCenterButton.Click += (_, _) => OpenControlCenter();
            AetherButton quick = CreateActionButton(global::AetherBoy.Runtime.Localization.UiText.Get("Quick"), AetherButtonKind.Secondary, 82);
            quick.Name = "aetherQuickMenuButton";
            quick.Click += (_, _) => _ = OpenQuickMenuAsync();
            aetherPauseButton = CreateActionButton("Pause", AetherButtonKind.Secondary, 92);
            aetherPauseButton.Click += aetherPauseButton_Click;
            aetherRewindButton = CreateActionButton(global::AetherBoy.Runtime.Localization.UiText.Get("Rewind"), AetherButtonKind.Secondary, 94);
            aetherRewindButton.Click += (_, args) =>
            {
                menuRewind_Click(aetherRewindButton, args);
                gameView.Focus();
            };
            aetherSaveButton = CreateActionButton(global::AetherBoy.Runtime.Localization.UiText.Get("Save"), AetherButtonKind.Secondary, 82);
            aetherSaveButton.Click += (_, _) =>
            {
                QuickSave();
                gameView.Focus();
            };
            aetherLoadButton = CreateActionButton(global::AetherBoy.Runtime.Localization.UiText.Get("Load"), AetherButtonKind.Secondary, 82);
            aetherLoadButton.Click += (_, _) =>
            {
                QuickLoad();
                gameView.Focus();
            };
            aetherTurboButton = CreateActionButton("Turbo", AetherButtonKind.Secondary, 88);
            aetherTurboButton.Click += aetherTurboButton_Click;

            AetherButton[] actions = [aetherDeckOpenButton, aetherControlCenterButton, quick, aetherPauseButton,
                aetherRewindButton, aetherSaveButton, aetherLoadButton, aetherTurboButton];
            for (int index = 0; index < actions.Length; index++)
            {
                actions[index].Dock = DockStyle.Fill;
                actions[index].Margin = new Padding(0, 0, index == actions.Length - 1 ? 0 : 8, 0);
                commands.Controls.Add(actions[index], index, 0);
            }
            deck.Controls.Add(commands);
            bool arranging = false;
            void ArrangeCommands()
            {
                if (arranging || commands.ClientSize.Width == 0) return;
                arranging = true;
                try
                {
                    int[] widths = actions.Select(button => TextRenderer.MeasureText(button.Text, button.Font,
                        Size.Empty, TextFormatFlags.NoPadding).Width + (int)Math.Ceiling(button.Font.Height * 1.8) + button.Margin.Horizontal).ToArray();
                    int columns = widths.Sum() <= commands.ClientSize.Width ? 8 : 4;
                    commands.SuspendLayout();
                    commands.ColumnCount = columns;
                    commands.RowCount = 8 / columns;
                    commands.ColumnStyles.Clear();
                    commands.RowStyles.Clear();
                    for (int column = 0; column < columns; column++)
                        commands.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,
                            columns == 8 ? widths[column] : Math.Max(widths[column], widths[column + 4])));
                    for (int row = 0; row < commands.RowCount; row++)
                        commands.RowStyles.Add(new RowStyle(SizeType.Percent, 1));
                    for (int index = 0; index < actions.Length; index++)
                        commands.SetCellPosition(actions[index], new TableLayoutPanelCellPosition(index % columns, index / columns));
                    deck.Height = deck.Padding.Vertical + commands.RowCount * (actions[0].Font.Height + 26);
                    commands.ResumeLayout(true);
                }
                finally { arranging = false; }
            }
            commands.SizeChanged += (_, _) => ArrangeCommands();
            foreach (AetherButton action in actions) action.FontChanged += (_, _) => ArrangeCommands();
            return deck;
        }

        private AetherButton CreateChromeButton(
            string text,
            EventHandler click,
            AetherButtonKind kind = AetherButtonKind.Ghost)
        {
            AetherButton button = CreateActionButton(text, kind, 38);
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
                Size = new Size(width, 40),
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
                Font = new Font("Segoe UI", size, style, GraphicsUnit.Point),
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
            key.Width = 70;
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

            aetherInputValue.Text = state.IsConnected ? global::AetherBoy.Runtime.Localization.UiText.Get("Gamepad") : global::AetherBoy.Runtime.Localization.UiText.Get("Keyboard");
        }

        private void OpenRomFromAetherUi()
        {
            if (romPreparation != null)
            {
                CancelRomPreparation();
                return;
            }
            menuOpen_Click(this, EventArgs.Empty);
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
                    ? "GBA"
                    : snapshot.Rom.HasColorFeatures ? "CGB" : "DMG";
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
            aetherLoadButton.Enabled = actionsEnabled && supportsSaveStates && !stateOperationInProgress;
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
        }

        private void UpdateAetherSlotButtons()
        {
            for (int index = 0; index < aetherSlotButtons.Length; index++)
            {
                aetherSlotButtons[index].Selected = settings.SaveSlot == index + 1;
            }

            if (aetherSlotValue != null)
            {
                aetherSlotValue.Text = settings.SaveSlot.ToString();
            }
        }

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
            }
        }

        private Size GetAetherClientSize(int scale)
        {
            int normalizedScale = Math.Clamp(scale, 1, 4);
            int displayWidth = gameView.VideoGeometry.Width * normalizedScale;
            int displayHeight = gameView.VideoGeometry.Height * normalizedScale;
            Size logicalSize = new Size(
                Math.Max(860, displayWidth + 330),
                Math.Max(646, displayHeight + 228));
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
