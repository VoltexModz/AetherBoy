using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Input;
using nanoboy.Storage;

namespace nanoboy
{
    internal sealed partial class frmControlCenter : Form
    {
        private readonly ControlCenterBridge bridge;
        private readonly Dictionary<string, Panel> pages = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AetherButton> navigation = new(StringComparer.Ordinal);
        private readonly List<AetherButton> filterButtons = new();
        private readonly List<AetherButton> paletteButtons = new();
        private readonly List<AetherButton> scaleButtons = new();
        private readonly List<AetherButton> volumeButtons = new();
        private readonly List<AetherButton> latencyButtons = new();
        private readonly List<(Panel Swatch, AetherButton Picker, Func<string> Read)> themeControls = new();
        private readonly List<(UiThemePreset Preset, AetherButton Button)> themePresetButtons = new();
        private AetherButton gpuButton = null!, vsyncButton = null!;
        private readonly List<AetherButton> pictureSizeButtons = new();
        private Label videoDetails = null!, stateFeedback = null!;
        private Label profileDetails = null!;
        private AetherButton profileButton = null!, profileResetButton = null!;
        private readonly List<AetherButton> frameskipButtons = new();
        private readonly List<AetherButton> slotButtons = new();
        private readonly AetherButton[] channelButtons = new AetherButton[4];
        private readonly System.Windows.Forms.Timer liveTimer;
        private readonly Panel contentHost;

        private AetherButton audioMasterButton = null!;
        private Label audioDetails = null!;
        private AetherButton bootRomButton = null!;
        private Label overviewState = null!;
        private Label overviewGame = null!;
        private Label overviewInput = null!;
        private Label overviewSave = null!;
        private Label gameplayRecordingStatus = null!;
        private AetherButton gameplayRecordingButton = null!;
        private Label inputStatus = null!;
        private Label inputIdentity = null!;
        private Label inputBindings = null!;
        private Label saveStatus = null!;
        private Label saveFiles = null!;
        private Label bootRomStatus = null!;
        private nanoboy.Controls.AetherTextBox diagnostics = null!;
        private AetherButton testerExportButton = null!;
        private AetherButton testerFolderButton = null!;
        private AetherButton recordingPreferenceButton = null!;
        private AetherButton problemMarkerButton = null!;
        private Label recordingStatus = null!, recordingPreferenceStatus = null!;

        public frmControlCenter(ControlCenterBridge bridge)
        {
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));

            Text = global::AetherBoy.Runtime.Localization.UiText.Get("Einstellungen");
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            ClientSize = new Size(1_080, 780);
            MinimumSize = Size;
            MaximumSize = Size;
            Branding.AppBrand.ApplyIcon(this);

            var nav = BuildNavigation();
            contentHost = new Panel
            {
                BackColor = AetherColors.Void,
                Dock = DockStyle.Fill,
                Name = "controlCenterContentHost",
                Padding = new Padding(26, 20, 26, 20)
            };
            Controls.Add(contentHost);
            Controls.Add(nav);

            AddPage("overview", BuildOverviewPage());
            AddPage("appearance", BuildAppearancePage());
            AddPage("display", BuildDisplayPage());
            AddPage("audio", BuildAudioPage());
            AddPage("input", BuildInputPage());
            AddPage("saves", BuildSavesPage());
            AddPage("system", BuildSystemPage());
            AddPage("diagnostics", BuildDiagnosticsPage());
            AddPage("storage", BuildStoragePage());
            ConfigureSettingsNavigation();

            AetherDialog.Apply(
                this,
                "AetherBoy",
                global::AetherBoy.Runtime.Localization.UiText.Get("Einstellung suchen oder einen Bereich auswählen."));
            // This pane has its own Aether card and scroll actions, not native white chrome.
            diagnostics.ScrollBars = ScrollBars.None;

            liveTimer = new System.Windows.Forms.Timer { Interval = 300 };
            liveTimer.Tick += (_, _) => RefreshAll();
            Shown += (_, _) =>
            {
                ShowPage("overview");
                RefreshAll();
                liveTimer.Start();
            };
            FormClosed += (_, _) => liveTimer.Dispose();
        }

        private Control BuildNavigation()
        {
            var nav = new AetherSurfacePanel
            {
                AccentEdge = true,
                BackColor = AetherColors.Chrome,
                Dock = DockStyle.Left,
                Name = "controlCenterNavigation",
                Padding = new Padding(18, 22, 18, 18),
                Width = 220
            };

            nav.Controls.Add(new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = AetherColors.Muted,
                Location = new Point(18, 20),
                Size = new Size(180, 20),
                Tag = "accent",
                Text = "AETHERBOY"
            });
            nav.Controls.Add(new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = AetherColors.Text,
                Location = new Point(18, 42),
                Size = new Size(182, 54),
                Tag = "value",
                Text = global::AetherBoy.Runtime.Localization.UiText.Get("Einstellungen")
            });

            AddNavigationButton(nav, "overview", global::AetherBoy.Runtime.Localization.UiText.Get("Übersicht"), 164);
            AddNavigationButton(nav, "appearance", global::AetherBoy.Runtime.Localization.UiText.Get("Aussehen"), 208);
            AddNavigationButton(nav, "display", global::AetherBoy.Runtime.Localization.UiText.Get("Grafik"), 252);
            AddNavigationButton(nav, "audio", "Audio", 296);
            AddNavigationButton(nav, "input", global::AetherBoy.Runtime.Localization.UiText.Get("Steuerung"), 340);
            AddNavigationButton(nav, "saves", global::AetherBoy.Runtime.Localization.UiText.Get("Spielstände"), 384);
            AddNavigationButton(nav, "system", global::AetherBoy.Runtime.Localization.UiText.Get("App und System"), 428);
            AddNavigationButton(nav, "diagnostics", global::AetherBoy.Runtime.Localization.UiText.Get("Diagnose"), 472);
            AddNavigationButton(nav, "storage", global::AetherBoy.Runtime.Localization.UiText.Get("Ordner"), 516);

            var privacy = new Label
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
                AutoSize = false,
                Font = new Font("Cascadia Mono", 7.25f, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = AetherColors.Muted,
                Location = new Point(18, 575),
                Size = new Size(180, 66),
                Text = global::AetherBoy.Runtime.Localization.UiText.Get("Auf diesem Computer gespeichert")
            };
            nav.Controls.Add(privacy);
            return nav;
        }

        private void AddNavigationButton(Control parent, string key, string text, int y)
        {
            var button = new AetherButton
            {
                Kind = AetherButtonKind.Ghost,
                Location = new Point(18, y),
                Name = "controlCenterNav" + char.ToUpperInvariant(key[0]) + key[1..],
                Size = new Size(184, 38),
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
            button.Click += (_, _) => ShowPage(key);
            navigation.Add(key, button);
            parent.Controls.Add(button);
        }

        private void AddPage(string key, Panel page)
        {
            var viewport = new AetherScrollViewport { Name = page.Name + "Viewport", Dock = DockStyle.Fill, Visible = false };
            page.Visible = false;
            pages.Add(key, page);
            pageViewports.Add(key, viewport);
            viewport.SetContent(page, Size.Empty, measureChildren: true);
            contentHost.Controls.Add(viewport);
        }

        private void ShowPage(string key)
        {
            if (key != "search") { currentSettingsPage = key; ClearSearchText(); }
            foreach ((string pageKey, Panel page) in pages)
            {
                bool selected = pageKey == key;
                page.Visible = selected;
                pageViewports[pageKey].Visible = selected;
                if (selected)
                {
                    pageViewports[pageKey].BringToFront();
                }
            }
            foreach ((string navKey, AetherButton button) in navigation)
            {
                button.Selected = navKey == SettingsSection(key);
            }
            RefreshSectionTabs(key);
            RefreshAll();
        }

        private Panel BuildOverviewPage()
        {
            Panel page = CreatePage("controlCenterPageOverview", global::AetherBoy.Runtime.Localization.UiText.Get("Overview"), global::AetherBoy.Runtime.Localization.UiText.Get("Your current game and useful shortcuts"));
            AetherSurfacePanel signal = CreateCard(page, 0, 72, 788, 92, global::AetherBoy.Runtime.Localization.UiText.Get("CURRENT GAME"));
            overviewState = CreateValueLabel(signal, 20, 36, 748, 38, 15f);

            AetherSurfacePanel game = CreateCard(page, 0, 180, 252, 126, global::AetherBoy.Runtime.Localization.UiText.Get("GAME"));
            overviewGame = CreateValueLabel(game, 18, 38, 216, 70, 10f);
            AetherSurfacePanel input = CreateCard(page, 268, 180, 252, 126, global::AetherBoy.Runtime.Localization.UiText.Get("CONTROLS"));
            overviewInput = CreateValueLabel(input, 18, 38, 216, 70, 10f);
            AetherSurfacePanel save = CreateCard(page, 536, 180, 252, 126, global::AetherBoy.Runtime.Localization.UiText.Get("SAVE DATA"));
            overviewSave = CreateValueLabel(save, 18, 38, 216, 70, 10f);

            AetherSurfacePanel actions = CreateCard(page, 0, 322, 788, 180, global::AetherBoy.Runtime.Localization.UiText.Get("QUICK ACTIONS"));
            AddActionButton(actions, global::AetherBoy.Runtime.Localization.UiText.Get("Change controls"), 18, 46, 230, bridge.OpenControls);
            AddActionButton(actions, global::AetherBoy.Runtime.Localization.UiText.Get("Manage saves"), 264, 46, 230, bridge.OpenSaveSafety);
            AddActionButton(actions, global::AetherBoy.Runtime.Localization.UiText.Get("Audio inspector"), 510, 46, 230, bridge.OpenAudioInspector);
            AddActionButton(actions, global::AetherBoy.Runtime.Localization.UiText.Get("Save game"), 18, 104, 230, bridge.QuickSave, AetherButtonKind.Primary);
            AddActionButton(actions, global::AetherBoy.Runtime.Localization.UiText.Get("Load game"), 264, 104, 230, bridge.QuickLoad);
            AddActionButton(actions, global::AetherBoy.Runtime.Localization.UiText.Get("Fullscreen"), 510, 104, 230, bridge.ToggleFullscreen);

            AetherSurfacePanel capture = CreateCard(page, 0, 518, 788, 214, global::AetherBoy.Runtime.Localization.UiText.Get("GAMEPLAY AUFNEHMEN"));
            gameplayRecordingButton = AddActionButton(capture, global::AetherBoy.Runtime.Localization.UiText.Get("Video aufnehmen"), 18, 42, 230, bridge.ToggleGameplayRecording);
            gameplayRecordingButton.Name = "gameplayRecordingToggle";
            AddActionButton(capture, global::AetherBoy.Runtime.Localization.UiText.Get("Aufnahmeordner öffnen"), 264, 42, 260,
                () => WindowsDataPaths.OpenFolder(this, WindowsDataPaths.Default.Recordings));
            gameplayRecordingStatus = CreateValueLabel(capture, 18, 96, 748, 46, 10f);
            capture.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("AVI mit Bild und Spielton, maximal 2 GiB pro Video. Ohne zusätzlichen Encoder; die Dateien sind groß.\nTurbo, Reset und Zurückspulen beenden die Aufnahme. Das Spiel läuft weiter."), 18, 149, 748, 54));

            page.Controls.Add(CreateFootnote(
                global::AetherBoy.Runtime.Localization.UiText.Get("Alle Einstellungen bleiben lokal. Firmware gilt beim erneuten ROM-Öffnen, die Diagnose-Aufzeichnung ab dem nächsten Programmstart."),
                748));
            return page;
        }

        private Panel BuildAppearancePage()
        {
            Panel page = CreatePage("controlCenterPageAppearance", global::AetherBoy.Runtime.Localization.UiText.Get("Aussehen"), global::AetherBoy.Runtime.Localization.UiText.Get("Wähle ein Design oder passe seine Farben selbst an."));
            AetherSurfacePanel presets = CreateCard(page, 0, 72, 788, 174, global::AetherBoy.Runtime.Localization.UiText.Get("FARBDESIGNS"));
            for (int i = 0; i < UiThemePresets.All.Count; i++)
            {
                UiThemePreset preset = UiThemePresets.All[i];
                int x = 18 + (i % 3) * 254, y = 44 + (i / 3) * 58;
                presets.Controls.Add(new Panel { Location = new Point(x, y + 5), Size = new Size(12, 31),
                    BackColor = ColorTranslator.FromHtml(preset.Primary), Tag = "theme-swatch" });
                presets.Controls.Add(new Panel { Location = new Point(x + 12, y + 5), Size = new Size(12, 31),
                    BackColor = ColorTranslator.FromHtml(preset.Secondary), Tag = "theme-swatch" });
                AetherButton button = AddActionButton(presets, preset.Name, x + 30, y, 208,
                    () => ApplyThemePreset(preset));
                button.Name = "themePreset_" + preset.Id;
                themePresetButtons.Add((preset, button));
            }
            AddThemeColorCard(page, 262, global::AetherBoy.Runtime.Localization.UiText.Get("HAUPTFARBE"), global::AetherBoy.Runtime.Localization.UiText.Get("Schaltflächen und aktive Auswahl"),
                () => bridge.Settings.UiPrimaryColor, value => bridge.Settings.UiPrimaryColor = value);
            AddThemeColorCard(page, 394, global::AetherBoy.Runtime.Localization.UiText.Get("ZWEITFARBE"), global::AetherBoy.Runtime.Localization.UiText.Get("Fokus und Verbindungsdetails"),
                () => bridge.Settings.UiSecondaryColor, value => bridge.Settings.UiSecondaryColor = value);
            AddThemeColorCard(page, 526, global::AetherBoy.Runtime.Localization.UiText.Get("HINTERGRUND"), global::AetherBoy.Runtime.Localization.UiText.Get("Grundfarbe hinter den Menüs"),
                () => bridge.Settings.UiBackgroundColor, value => bridge.Settings.UiBackgroundColor = value);

            AetherSurfacePanel brand = CreateCard(page, 0, 664, 788, 132, "AETHER ORIGINAL");
            var originalMark = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(18, 36), Size = new Size(82, 82),
            };
            Branding.AppBrand.BindMark(originalMark, followsTheme: false);
            brand.Controls.Add(originalMark);
            brand.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Das Originaldesign nutzt Violett und Cyan. Eigene Farben gelten für alle Spiele."),
                118, 39, 630, 35));
            AddActionButton(brand, global::AetherBoy.Runtime.Localization.UiText.Get("Originaldesign wiederherstellen"), 118, 79, 300,
                () => ApplyThemePreset(UiThemePresets.All[0])).Name = "controlCenterRestoreTheme";
            page.Controls.Add(CreateFootnote(global::AetherBoy.Runtime.Localization.UiText.Get("Text und Warnungen passen sich automatisch an die gewählten Farben an."), 810));
            return page;
        }

        private void ApplyThemePreset(UiThemePreset preset)
        {
            bridge.Settings.UiPrimaryColor = preset.Primary;
            bridge.Settings.UiSecondaryColor = preset.Secondary;
            bridge.Settings.UiBackgroundColor = preset.Background;
            bridge.ApplyUiTheme();
            RefreshAll();
        }

        private void AddThemeColorCard(Control page, int y, string heading, string description,
            Func<string> read, Action<string> write)
        {
            AetherSurfacePanel card = CreateCard(page, 0, y, 788, 116, heading);
            card.Controls.Add(CreateSmallLabel(description, 110, 42, 410, 30));
            var swatch = new AetherColorSwatch { Location = new Point(18, 43), Size = new Size(72, 52) };
            card.Controls.Add(swatch);
            AetherButton picker = AddActionButton(card, global::AetherBoy.Runtime.Localization.UiText.Get("Farbe wählen"), 546, 45, 222, () =>
            {
                UiRgb.TryParse(read(), out UiRgb current);
                using var dialog = new AetherColorDialog
                {
                    Color = Color.FromArgb(current.R, current.G, current.B),
                };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                write($"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
                bridge.ApplyUiTheme(); RefreshAll();
            });
            themeControls.Add((swatch, picker, read));
        }

        private Panel BuildDisplayPage()
        {
            Panel page = CreatePage("controlCenterPageDisplay", global::AetherBoy.Runtime.Localization.UiText.Get("Grafik"), global::AetherBoy.Runtime.Localization.UiText.Get("Bildfilter, Skalierung und Fenstergröße einstellen."));
            AetherSurfacePanel filter = CreateCard(page, 0, 72, 788, 116, global::AetherBoy.Runtime.Localization.UiText.Get("BILDFILTER"));
            AddSelector(filter, new[] { global::AetherBoy.Runtime.Localization.UiText.Get("Scharf"), global::AetherBoy.Runtime.Localization.UiText.Get("Weich"), global::AetherBoy.Runtime.Localization.UiText.Get("LCD-Raster") }, 18, 48, 236, filterButtons, bridge.SetDisplayFilter);

            AetherSurfacePanel palette = CreateCard(page, 0, 204, 788, 132, global::AetherBoy.Runtime.Localization.UiText.Get("GAME-BOY-PALETTE"));
            AddSelector(
                palette,
                new[] { "POCKET", global::AetherBoy.Runtime.Localization.UiText.Get("PEA GREEN"), "GB LIGHT", "SEPIA", "CYBER" },
                18,
                50,
                140,
                paletteButtons,
                bridge.SetPalette);

            AetherSurfacePanel geometry = CreateCard(page, 0, 352, 788, 152, global::AetherBoy.Runtime.Localization.UiText.Get("FENSTERGRÖSSE"));
            AddSelector(geometry, new[] { "1×", "2×", "3×", "4×" }, 18, 50, 128, scaleButtons, index => bridge.SetWindowScale(index + 1));
            AddActionButton(geometry, global::AetherBoy.Runtime.Localization.UiText.Get("Vollbild"), 570, 50, 198, bridge.ToggleFullscreen, AetherButtonKind.Primary);
            geometry.Controls.Add(CreateSmallLabel(
                global::AetherBoy.Runtime.Localization.UiText.Get("F11 oder Alt+Enter öffnet das Vollbild. Esc kehrt zum Fenster zurück."),
                18,
                102,
                750,
                30));
            AetherSurfacePanel renderer = CreateCard(page, 0, 520, 788, 154, global::AetherBoy.Runtime.Localization.UiText.Get("BILDAUSGABE UNTER WINDOWS"));
            gpuButton = AddActionButton(renderer, "GPU", 18, 38, 230, () =>
            { bridge.Settings.GpuRendering = !bridge.Settings.GpuRendering; bridge.ApplyVideoSettings(); RefreshAll(); });
            vsyncButton = AddActionButton(renderer, "VSYNC", 270, 38, 230, () =>
            { bridge.Settings.VideoVSync = !bridge.Settings.VideoVSync; bridge.ApplyVideoSettings(); RefreshAll(); });
            gpuButton.Name = "controlCenterGpuButton";
            vsyncButton.Name = "controlCenterVSyncButton";
            videoDetails = CreateSmallLabel("", 18, 94, 746, 50);
            renderer.Controls.Add(videoDetails);
            return page;
        }

        private Panel BuildAudioPage()
        {
            Panel page = CreatePage("controlCenterPageAudio", "AUDIO", global::AetherBoy.Runtime.Localization.UiText.Get("Master-Ausgabe, Kanäle und Pegel"));
            AetherSurfacePanel master = CreateCard(page, 0, 72, 788, 112, global::AetherBoy.Runtime.Localization.UiText.Get("MASTER OUTPUT // WASAPI + WINMM FALLBACK"));
            audioMasterButton = AddActionButton(master, global::AetherBoy.Runtime.Localization.UiText.Get("AUDIO ON"), 18, 48, 200, ToggleAudio, AetherButtonKind.Primary);
            audioDetails = CreateSmallLabel("", 242, 34, 510, 74);
            master.Controls.Add(audioDetails);

            AetherSurfacePanel channels = CreateCard(page, 0, 200, 788, 124, global::AetherBoy.Runtime.Localization.UiText.Get("CHANNEL MATRIX"));
            string[] channelNames = { global::AetherBoy.Runtime.Localization.UiText.Get("CH1 PULSE"), global::AetherBoy.Runtime.Localization.UiText.Get("CH2 PULSE"), global::AetherBoy.Runtime.Localization.UiText.Get("CH3 WAVE"), global::AetherBoy.Runtime.Localization.UiText.Get("CH4 NOISE") };
            for (int index = 0; index < channelButtons.Length; index++)
            {
                int channel = index;
                channelButtons[index] = AddActionButton(
                    channels,
                    channelNames[index],
                    18 + (index * 187),
                    50,
                    170,
                    () => ToggleChannel(channel));
            }

            AetherSurfacePanel volume = CreateCard(page, 0, 340, 788, 130, global::AetherBoy.Runtime.Localization.UiText.Get("OUTPUT LEVEL"));
            AddSelector(
                volume,
                new[] { global::AetherBoy.Runtime.Localization.UiText.Get("MUTE"), "25%", "50%", "75%", "100%" },
                18,
                50,
                140,
                volumeButtons,
                index => SetVolume(index * 25));

            AetherSurfacePanel tools = CreateCard(page, 0, 486, 788, 86, global::AetherBoy.Runtime.Localization.UiText.Get("SIGNAL TOOL"));
            AddActionButton(tools, global::AetherBoy.Runtime.Localization.UiText.Get("OPEN AUDIO INSPECTOR"), 18, 36, 260, bridge.OpenAudioInspector, AetherButtonKind.Secondary);
            tools.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Live-Kanäle, Frequenzen und Wave-RAM ansehen oder als WAV aufnehmen."), 298, 38, 460, 30));
            AetherSurfacePanel latency = CreateCard(page, 0, 588, 788, 144, global::AetherBoy.Runtime.Localization.UiText.Get("OUTPUT LATENCY // WINDOWS DEFAULT DEVICE"));
            AddSelector(latency, new[] { "20 MS", "40 MS", "60 MS", "100 MS" }, 18, 38, 170,
                latencyButtons, index =>
                {
                    bridge.Settings.AudioLatencyMs = new[] { 20, 40, 60, 100 }[index];
                    bridge.ApplyAudioSettings();
                    RefreshAll();
                });
            latency.Controls.Add(CreateSmallLabel(
                global::AetherBoy.Runtime.Localization.UiText.Get("40 ms Standard. Bei Knacken 60/100 ms wählen. Gerätewechsel folgt Windows automatisch.\r\nZielpuffer ≠ Gesamtlatenz; Vorpuffer, Resampling und Treiber kommen hinzu. GB/GBC: Mono · GBA: Stereo."),
                18, 92, 748, 44));
            return page;
        }

        private Panel BuildInputPage()
        {
            Panel page = CreatePage("controlCenterPageInput", global::AetherBoy.Runtime.Localization.UiText.Get("Steuerung"), global::AetherBoy.Runtime.Localization.UiText.Get("Tastatur oder Controller für dein Spiel einrichten."));
            AetherSurfacePanel live = CreateCard(page, 0, 72, 788, 128, global::AetherBoy.Runtime.Localization.UiText.Get("AKTIVER CONTROLLER"));
            inputStatus = CreateValueLabel(live, 18, 38, 748, 38, 13f);
            inputIdentity = CreateSmallLabel("WINDOWS GAME INPUT + XINPUT FALLBACK", 18, 78, 748, 28);
            live.Controls.Add(inputIdentity);

            AetherSurfacePanel mapping = CreateCard(page, 0, 216, 788, 160, global::AetherBoy.Runtime.Localization.UiText.Get("AKTUELLE BELEGUNG"));
            inputBindings = CreateValueLabel(mapping, 18, 40, 748, 100, 9f);

            AetherSurfacePanel editor = CreateCard(page, 0, 392, 788, 132, global::AetherBoy.Runtime.Localization.UiText.Get("BELEGUNG ÄNDERN"));
            AddActionButton(editor, global::AetherBoy.Runtime.Localization.UiText.Get("Controller einrichten"), 18, 50, 276, () => OpenInputEditor("controller"), AetherButtonKind.Primary);
            editor.Controls.Add(CreateSmallLabel(
                global::AetherBoy.Runtime.Localization.UiText.Get("Steuerkreuz-Richtungen und Tasten lassen sich pro Controller belegen. Der linke Stick bleibt ein zusätzlicher Richtungseingang."),
                318,
                44,
                444,
                56));
            return page;
        }

        private Panel BuildSavesPage()
        {
            Panel page = CreatePage("controlCenterPageSaves", global::AetherBoy.Runtime.Localization.UiText.Get("SAVES"), global::AetherBoy.Runtime.Localization.UiText.Get("Save States und Batterie-RAM-Schutz"));
            AetherSurfacePanel slots = CreateCard(page, 0, 72, 788, 116, global::AetherBoy.Runtime.Localization.UiText.Get("ACTIVE STATE SLOT"));
            AddSelector(slots, new[] { "SLOT 1", "SLOT 2", "SLOT 3", "SLOT 4", "SLOT 5" }, 18, 48, 140, slotButtons, index => bridge.SetSaveSlot(index + 1));

            AetherSurfacePanel operations = CreateCard(page, 0, 204, 788, 106, global::AetherBoy.Runtime.Localization.UiText.Get("STATE OPERATIONS"));
            AddActionButton(operations, global::AetherBoy.Runtime.Localization.UiText.Get("QUICK SAVE · F5"), 18, 46, 230, bridge.QuickSave, AetherButtonKind.Primary);
            AddActionButton(operations, global::AetherBoy.Runtime.Localization.UiText.Get("QUICK LOAD · F8"), 264, 46, 230, bridge.QuickLoad);
            AddActionButton(operations, global::AetherBoy.Runtime.Localization.UiText.Get("SAVE SAFETY CENTER"), 510, 46, 230, bridge.OpenSaveSafety);

            AetherSurfacePanel battery = CreateCard(page, 0, 326, 788, 170, global::AetherBoy.Runtime.Localization.UiText.Get("BATTERY RAM // ROTATING BACKUPS"));
            saveStatus = CreateValueLabel(battery, 18, 38, 748, 46, 11f);
            saveFiles = CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Keine Cartridge aktiv."), 18, 88, 748, 62);
            battery.Controls.Add(saveFiles);
            page.Controls.Add(CreateFootnote(
                global::AetherBoy.Runtime.Localization.UiText.Get("Save States (.ss1–.ss5) und originale Batterie-Saves (.sav) sind getrennte Systeme. Save Safety schützt ausschließlich Batterie-RAM."),
                516));
            stateFeedback = CreateSmallLabel("", 0, 566, 788, 72);
            stateFeedback.Name = "controlCenterStateFeedback";
            page.Controls.Add(stateFeedback);
            AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("STATE-GALERIE / FORTSETZEN / RÜCKGÄNGIG"), 0, 650, 540,
                bridge.OpenStateGallery, AetherButtonKind.Primary).Name = "controlCenterStateGalleryButton";
            return page;
        }

        private Panel BuildSystemPage()
        {
            Panel page = CreatePage("controlCenterPageSystem", global::AetherBoy.Runtime.Localization.UiText.Get("App und System"), global::AetherBoy.Runtime.Localization.UiText.Get("Bedienung, Spielprofile und lokale Firmware verwalten."));
            AetherSurfacePanel frames = CreateCard(page, 0, 72, 788, 130, global::AetherBoy.Runtime.Localization.UiText.Get("BILDER AUSLASSEN"));
            AddSelector(frames, new[] { global::AetherBoy.Runtime.Localization.UiText.Get("Keine"), global::AetherBoy.Runtime.Localization.UiText.Get("1 Bild"), global::AetherBoy.Runtime.Localization.UiText.Get("2 Bilder"), global::AetherBoy.Runtime.Localization.UiText.Get("3 Bilder"), global::AetherBoy.Runtime.Localization.UiText.Get("4 Bilder") }, 18, 48, 140, frameskipButtons, bridge.SetFrameskip);
            frames.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Frameskip verändert nur die Bildausgabe, nicht die emulierte Hardwarezeit."), 18, 91, 748, 28));

            AetherSurfacePanel boot = CreateCard(page, 0, 218, 788, 142, global::AetherBoy.Runtime.Localization.UiText.Get("BOOT ROM POLICY"));
            bootRomButton = AddActionButton(boot, global::AetherBoy.Runtime.Localization.UiText.Get("BOOT ROM · AUTO"), 18, 40, 230, ToggleBootRom, AetherButtonKind.Secondary);
            AddActionButton(boot, global::AetherBoy.Runtime.Localization.UiText.Get("FIRMWARE VERWALTEN"), 18, 90, 230,
                () => { bridge.OpenFirmwareManager(); RefreshAll(); }).Name = "controlCenterFirmwareManagerButton";
            bootRomStatus = CreateSmallLabel(string.Empty, 270, 36, 492, 98);
            bootRomStatus.Name = "controlCenterFirmwareStatus";
            boot.Controls.Add(bootRomStatus);

            AetherSurfacePanel reset = CreateCard(page, 0, 376, 788, 152, global::AetherBoy.Runtime.Localization.UiText.Get("EINSTELLUNGEN ZURÜCKSETZEN"));
            AddActionButton(reset, global::AetherBoy.Runtime.Localization.UiText.Get("Einstellungen zurücksetzen"), 18, 50, 240, ResetSettings, AetherButtonKind.Danger);
            reset.Controls.Add(CreateSmallLabel(
                global::AetherBoy.Runtime.Localization.UiText.Get("Setzt Video, Audio, Eingabe, Save-Slot und Boot-ROM-Verhalten zurück. Die Diagnose-Aufzeichnungswahl, ROMs und Spielstände bleiben erhalten."),
                286,
                42,
                476,
                72));
            AetherSurfacePanel profile = CreateCard(page, 0, 544, 788, 172, global::AetherBoy.Runtime.Localization.UiText.Get("SPIELPROFIL // GLOBALE WERTE BLEIBEN ERHALTEN"));
            profileButton = AddActionButton(profile, global::AetherBoy.Runtime.Localization.UiText.Get("PROFIL"), 18, 40, 320, () => { bridge.ToggleGameProfile(); RefreshAll(); });
            profileButton.Name = "controlCenterGameProfileButton";
            profileResetButton = AddActionButton(profile, global::AetherBoy.Runtime.Localization.UiText.Get("ÜBERSCHREIBUNGEN ZURÜCKSETZEN"), 360, 40, 400,
                () => { bridge.ResetGameProfile(); RefreshAll(); });
            profileDetails = CreateSmallLabel("", 18, 92, 748, 70);
            profile.Controls.Add(profileDetails);
            return page;
        }

        private Panel BuildStoragePage()
        {
            Panel page = CreatePage("controlCenterPageStorage", global::AetherBoy.Runtime.Localization.UiText.Get("LOKALE ORDNER"), global::AetherBoy.Runtime.Localization.UiText.Get("Deine Bibliothek, Spielstände und Einstellungen"));
            WindowsDataPaths paths = WindowsDataPaths.Default;
            var folders = new[]
            {
                (global::AetherBoy.Runtime.Localization.UiText.Get("AETHERBOY-ORDNER"), paths.Root, global::AetherBoy.Runtime.Localization.UiText.Get("Alle lokalen Daten"), "Root"),
                (global::AetherBoy.Runtime.Localization.UiText.Get("ROM-ORDNER ÖFFNEN"), paths.Roms, global::AetherBoy.Runtime.Localization.UiText.Get("Importierte Kopien deiner GB-, GBC- und GBA-ROMs"), "Roms"),
                (global::AetherBoy.Runtime.Localization.UiText.Get("SPIELSTÄNDE ÖFFNEN"), paths.Saves, global::AetherBoy.Runtime.Localization.UiText.Get("Batterie-Saves, RTC und drei rotierende Sicherungen"), "Saves"),
                (global::AetherBoy.Runtime.Localization.UiText.Get("SAVE STATES ÖFFNEN"), paths.States, global::AetherBoy.Runtime.Localization.UiText.Get("Fünf Slots pro ROM-Inhalt"), "States"),
                (global::AetherBoy.Runtime.Localization.UiText.Get("EINSTELLUNGEN"), paths.Settings, global::AetherBoy.Runtime.Localization.UiText.Get("Belegung, Bild, Audio und zuletzt gespielte Titel"), "Settings"),
                (global::AetherBoy.Runtime.Localization.UiText.Get("FIRMWARE-ORDNER"), paths.Firmware, "Optional: dmg_boot.bin, gbc_boot.bin, gba_bios.bin", "Firmware"),
                (global::AetherBoy.Runtime.Localization.UiText.Get("SCREENSHOTS ÖFFNEN"), paths.Screenshots, global::AetherBoy.Runtime.Localization.UiText.Get("Unveränderte Spielbilder als PNG, getrennt nach ROM"), "Screenshots"),
                (global::AetherBoy.Runtime.Localization.UiText.Get("DEVELOPMENT-ORDNER"), paths.Development, global::AetherBoy.Runtime.Localization.UiText.Get("Lokale Sitzungsberichte und Development-Crashlogs"), "Development")
            };
            int y = 76;
            foreach (var (label, path, description, name) in folders)
            {
                AetherButton button = AddActionButton(page, label, 0, y, 286,
                    () => WindowsDataPaths.OpenFolder(this, path));
                button.Name = "controlCenterOpen" + name + "FolderButton";
                page.Controls.Add(CreateSmallLabel(description, 306, y + 4, 478, 38));
                y += 62;
            }
            page.Controls.Add(CreateSmallLabel(paths.Root + global::AetherBoy.Runtime.Localization.UiText.Get("\r\nROM-Originale bleiben beim Import erhalten. Bereits zentrale Spielstände haben Vorrang."),
                0, y + 4, 788, 60));
            return page;
        }

        private Panel BuildDiagnosticsPage()
        {
            Panel page = CreatePage("controlCenterPageDiagnostics", global::AetherBoy.Runtime.Localization.UiText.Get("DIAGNOSTICS"), global::AetherBoy.Runtime.Localization.UiText.Get("Lokale Aufzeichnung, transparente Freigabe und Laufzeit-Werkzeuge"));
            AetherSurfacePanel recording = CreateCard(page, 0, 72, 788, 150, global::AetherBoy.Runtime.Localization.UiText.Get("LOCAL RECORDER // DEINE ENTSCHEIDUNG"));
            recordingStatus = CreateSmallLabel("", 18, 34, 750, 48);
            recordingStatus.Name = "controlCenterRecordingStatus";
            recording.Controls.Add(recordingStatus);
            recordingPreferenceButton = AddActionButton(recording, global::AetherBoy.Runtime.Localization.UiText.Get("NÄCHSTER START · AUFZEICHNUNG"), 18, 88, 334,
                ToggleRecordingPreference);
            recordingPreferenceButton.Name = "controlCenterRecordNextSessionButton";
            recordingPreferenceStatus = CreateSmallLabel("", 370, 78, 396, 64);
            recordingPreferenceStatus.Name = "controlCenterRecordingPreferenceStatus";
            recording.Controls.Add(recordingPreferenceStatus);

            AetherSurfacePanel summary = CreateCard(page, 0, 238, 788, 236, global::AetherBoy.Runtime.Localization.UiText.Get("SHAREABLE SUMMARY // OHNE DATEIPFADE"));
            diagnostics = new nanoboy.Controls.AetherTextBox
            {
                BackColor = AetherColors.SurfaceRaised,
                Multiline = true,
                Font = new Font("Cascadia Mono", 9f, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = AetherColors.Text,
                Location = new Point(18, 38),
                Name = "controlCenterDiagnosticsText",
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Size = new Size(704, 182),
                WordWrap = true
            };
            summary.Controls.Add(diagnostics);
            AddActionButton(summary, "↑", 738, 38, 32, () => ScrollDiagnostics(-8)).AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Diagnose nach oben scrollen");
            AddActionButton(summary, "↓", 738, 178, 32, () => ScrollDiagnostics(8)).AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Diagnose nach unten scrollen");
            AetherButton copyButton = AddActionButton(
                page,
                global::AetherBoy.Runtime.Localization.UiText.Get("COPY DIAGNOSTICS"),
                0,
                490,
                220,
                CopyDiagnostics,
                AetherButtonKind.Primary);
            copyButton.Name = "controlCenterCopyDiagnosticsButton";
            testerExportButton = AddActionButton(
                page,
                global::AetherBoy.Runtime.Localization.UiText.Get("EXPORT TEST REPORT"),
                236,
                490,
                260,
                bridge.ExportTesterReport);
            testerExportButton.Name = "controlCenterExportTesterReportButton";
            testerFolderButton = AddActionButton(
                page,
                global::AetherBoy.Runtime.Localization.UiText.Get("OPEN TEST FOLDER"),
                512,
                490,
                250,
                bridge.OpenTesterFolder);
            testerFolderButton.Name = "controlCenterOpenTesterFolderButton";
            page.Controls.Add(CreateSmallLabel(
                global::AetherBoy.Runtime.Localization.UiText.Get("Nur lokal, kein automatischer Upload. Berichte enthalten Build-/Gerätedaten, ROM-Kennung und Laufzeit-Ereignisse; keine ROM-/Save-Inhalte oder Dateipfade. Minimale Crashlogs bleiben auch ohne Sitzungsaufzeichnung aktiv. Vorhandene Berichte bleiben erhalten."),
                2,
                602,
                786,
                70));
            AddActionButton(page, "SCREENSHOT · F12", 0, 546, 184, bridge.CaptureScreenshot).Name = "controlCenterScreenshotButton";
            AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("PERFORMANCE · F9"), 200, 546, 184, bridge.TogglePerformanceOverlay).Name = "controlCenterOverlayButton";
            AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("QUICK DECK · F10"), 400, 546, 184, bridge.OpenQuickMenu).Name = "controlCenterQuickMenuButton";
            problemMarkerButton = AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("PROBLEM MARKIEREN"), 600, 546, 184, bridge.MarkProblem);
            problemMarkerButton.Name = "controlCenterMarkProblemButton";
            return page;
        }

        private static Panel CreatePage(string name, string title, string subtitle)
        {
            var page = new Panel
            {
                BackColor = AetherColors.Void,
                Name = name
            };
            page.Controls.Add(new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = AetherColors.Text,
                Location = new Point(0, 0),
                Size = new Size(788, 38),
                Tag = "value",
                Text = title
            });
            page.Controls.Add(new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = AetherColors.Muted,
                Location = new Point(2, 40),
                Size = new Size(786, 24),
                Text = subtitle
            });
            return page;
        }

        private static AetherSurfacePanel CreateCard(
            Control parent,
            int x,
            int y,
            int width,
            int height,
            string heading)
        {
            var card = new AetherSurfacePanel
            {
                AccentEdge = true,
                Location = new Point(x, y),
                Size = new Size(width, height)
            };
            card.Controls.Add(new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = AetherColors.Cyan,
                Location = new Point(18, 12),
                Size = new Size(Math.Max(1, width - 36), 20),
                Tag = "accent",
                Text = heading
            });
            parent.Controls.Add(card);
            return card;
        }

        private static Label CreateValueLabel(
            Control parent,
            int x,
            int y,
            int width,
            int height,
            float fontSize)
        {
            var label = new Label
            {
                AutoEllipsis = true,
                AutoSize = false,
                Font = new Font("Segoe UI Semibold", fontSize, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = AetherColors.Text,
                Location = new Point(x, y),
                Size = new Size(width, height),
                Tag = "value",
                TextAlign = ContentAlignment.MiddleLeft
            };
            parent.Controls.Add(label);
            return label;
        }

        private static Label CreateSmallLabel(string text, int x, int y, int width, int height) =>
            new()
            {
                AutoEllipsis = true,
                AutoSize = false,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = AetherColors.Muted,
                Location = new Point(x, y),
                Size = new Size(width, height),
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };

        private static Label CreateFootnote(string text, int y) =>
            CreateSmallLabel(text, 2, y, 786, 50);

        private static AetherButton AddActionButton(
            Control parent,
            string text,
            int x,
            int y,
            int width,
            Action action,
            AetherButtonKind kind = AetherButtonKind.Secondary)
        {
            var button = new AetherButton
            {
                Kind = kind,
                Location = new Point(x, y),
                Size = new Size(width, 40),
                Text = text
            };
            button.Click += (_, _) => action();
            parent.Controls.Add(button);
            return button;
        }

        private static void AddSelector(
            Control parent,
            IReadOnlyList<string> labels,
            int x,
            int y,
            int width,
            ICollection<AetherButton> destination,
            Action<int> selected)
        {
            for (int index = 0; index < labels.Count; index++)
            {
                int value = index;
                var button = new AetherButton
                {
                    Kind = AetherButtonKind.Secondary,
                    Location = new Point(x + (index * (width + 9)), y),
                    Size = new Size(width, 38),
                    Text = labels[index]
                };
                button.Click += (_, _) => selected(value);
                destination.Add(button);
                parent.Controls.Add(button);
            }
        }

        private void RefreshAll()
        {
            UiThemePalette theme = new(bridge.Settings.UiPrimaryColor, bridge.Settings.UiSecondaryColor, bridge.Settings.UiBackgroundColor);
            UiThemePreset? selectedPreset = UiThemePresets.Match(theme.Primary.Hex, theme.Secondary.Hex, theme.Background.Hex);
            foreach ((UiThemePreset preset, AetherButton button) in themePresetButtons)
                button.Selected = preset.Id == selectedPreset?.Id;
            UiRgb[] colors = [theme.Primary, theme.Secondary, theme.Background];
            for (int i = 0; i < themeControls.Count; i++)
            {
                UiRgb color = colors[i];
                themeControls[i].Swatch.BackColor = Color.FromArgb(color.R, color.G, color.B);
                themeControls[i].Picker.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Wählen · ") + color.Hex;
            }
            RefreshSelectors();
            RefreshOverview();
            RefreshInput();
            RefreshSaves();
            RefreshSystem();
            RefreshDiagnostics();
            RefreshDiscord();
            RefreshUpdates();
            RefreshBarcodeBoy();
        }

        private void RefreshSelectors()
        {
            SelectIndex(filterButtons, bridge.Settings.DisplayFilterIndex);
            SelectIndex(paletteButtons, bridge.Settings.PaletteIndex);
            SelectIndex(scaleButtons, bridge.Settings.VideoScaleFactor - 1);
            SelectIndex(volumeButtons, (int)Math.Round(bridge.Settings.AudioVolume / 25d));
            SelectIndex(latencyButtons, Array.IndexOf(new[] { 20, 40, 60, 100 }, bridge.Settings.AudioLatencyMs));
            gpuButton.Selected = bridge.Settings.GpuRendering;
            gpuButton.Text = bridge.Settings.GpuRendering ? global::AetherBoy.Runtime.Localization.UiText.Get("GPU · ON") : "CPU · GDI";
            vsyncButton.Selected = bridge.Settings.VideoVSync;
            vsyncButton.Enabled = bridge.Settings.GpuRendering;
            vsyncButton.Text = bridge.Settings.VideoVSync ? global::AetherBoy.Runtime.Localization.UiText.Get("VSYNC · ON") : global::AetherBoy.Runtime.Localization.UiText.Get("VSYNC · OFF");
            SelectIndex(pictureSizeButtons, bridge.Settings.VideoScalingMode);
            videoDetails.Text = bridge.VideoOutputProvider();
            SelectIndex(frameskipButtons, bridge.Settings.Frameskip);
            SelectIndex(slotButtons, bridge.Settings.SaveSlot - 1);

            audioMasterButton.Selected = bridge.Settings.AudioEnable;
            audioMasterButton.Text = bridge.Settings.AudioEnable ? global::AetherBoy.Runtime.Localization.UiText.Get("AUDIO · ON") : global::AetherBoy.Runtime.Localization.UiText.Get("AUDIO · OFF");
            bool[] channels =
            {
                bridge.Settings.Channel1Enable,
                bridge.Settings.Channel2Enable,
                bridge.Settings.Channel3Enable,
                bridge.Settings.Channel4Enable
            };
            EmulationSnapshot? snapshot = bridge.SnapshotProvider();
            bool isGameBoyAdvance = snapshot?.Rom?.IsGameBoyAdvance == true;
            bool supportsAudioChannels = snapshot?.Rom is null ||
                snapshot.Supports(EmulationFeature.AudioChannelControls);
            audioDetails.Text = bridge.AudioOutputProvider();
            for (int index = 0; index < channelButtons.Length; index++)
            {
                channelButtons[index].Selected = channels[index];
                channelButtons[index].Enabled = supportsAudioChannels;
            }
            foreach (AetherButton paletteButton in paletteButtons)
                paletteButton.Enabled = !isGameBoyAdvance;

            bootRomButton.Selected = bridge.Settings.BootRomEnable;
            bootRomButton.Text = bridge.Settings.BootRomEnable ? global::AetherBoy.Runtime.Localization.UiText.Get("BOOT ROM · AUTO") : global::AetherBoy.Runtime.Localization.UiText.Get("BOOT ROM · BYPASS");
        }

        private static void SelectIndex(IReadOnlyList<AetherButton> buttons, int selectedIndex)
        {
            for (int index = 0; index < buttons.Count; index++)
            {
                buttons[index].Selected = index == selectedIndex;
            }
        }

        private void RefreshOverview()
        {
            EmulationSnapshot? snapshot = bridge.SnapshotProvider();
            RomSnapshot? rom = snapshot?.Rom;
            overviewState.Text = snapshot == null
                ? global::AetherBoy.Runtime.Localization.UiText.Get("OFFLINE  //  READY FOR CARTRIDGE")
                : global::AetherBoy.Runtime.Localization.UiText.Format("{0}  //  FRAME {1:N0}", global::AetherBoy.Runtime.Localization.UiLabels.Session(snapshot.State), snapshot.EmulatedFrameCount);
            overviewGame.Text = rom == null
                ? global::AetherBoy.Runtime.Localization.UiText.Get("NO CARTRIDGE\r\nDMG / CGB / GBA READY")
                : $"{SafeTitle(rom)}\r\n{GetModelName(rom)} · {rom.CartridgeType}";

            HostGamepadState gamepad = bridge.GamepadProvider();
            overviewInput.Text = gamepad.IsConnected
                ? global::AetherBoy.Runtime.Localization.UiText.Format("GAMEPAD LIVE\r\n{0}", gamepad.DeviceName?.ToUpperInvariant() ?? global::AetherBoy.Runtime.Localization.UiText.Get("CONNECTED"))
                : global::AetherBoy.Runtime.Localization.UiText.Get("KEYBOARD ACTIVE\r\nGAMEPAD STANDBY");
            overviewSave.Text = GetSaveSummary(rom);
            gameplayRecordingButton.Text = bridge.GameplayRecordingActive() ? global::AetherBoy.Runtime.Localization.UiText.Get("Video beenden") : global::AetherBoy.Runtime.Localization.UiText.Get("Video aufnehmen");
            gameplayRecordingButton.Enabled = snapshot?.Rom is not null || bridge.GameplayRecordingActive();
            gameplayRecordingStatus.Text = bridge.GameplayRecordingStatus();
        }

        private void RefreshInput()
        {
            HostGamepadState gamepad = bridge.GamepadProvider();
            inputStatus.Text = gamepad.IsConnected
                ? gamepad.DeviceName?.ToUpperInvariant() ?? global::AetherBoy.Runtime.Localization.UiText.Get("GAMEPAD CONNECTED")
                : global::AetherBoy.Runtime.Localization.UiText.Get("NO GAMEPAD CONNECTED");
            inputStatus.ForeColor = gamepad.IsConnected ? AetherColors.Success : AetherColors.Muted;
            string source = gamepad.Source switch
            {
                GamepadInputSource.WindowsGamingInput => "WINDOWS GAME INPUT",
                GamepadInputSource.XInput => "XINPUT FALLBACK",
                _ => global::AetherBoy.Runtime.Localization.UiText.Get("KEYBOARD ROUTE")
            };
            string hardware = gamepad.VendorId == 0 && gamepad.ProductId == 0
                ? string.Empty
                : $" · USB {gamepad.VendorId:X4}:{gamepad.ProductId:X4}";
            inputIdentity.Text = source + hardware;
            inputBindings.Text =
                global::AetherBoy.Runtime.Localization.UiText.Format("KEYBOARD  A {0}  ·  B {1}  ·  START {2}  ·  SELECT {3}\r\n", bridge.Settings.KeyA, bridge.Settings.KeyB, bridge.Settings.KeyStart, bridge.Settings.KeySelect) +
                global::AetherBoy.Runtime.Localization.UiText.Format("DIRECTION  {0}/{1}/{2}/{3}\r\n", bridge.Settings.KeyUp, bridge.Settings.KeyDown, bridge.Settings.KeyLeft, bridge.Settings.KeyRight) +
                $"GAMEPAD  A {FormatBinding(bridge.Settings.GamepadA)}  ·  B {FormatBinding(bridge.Settings.GamepadB)}  ·  " +
                $"START {FormatBinding(bridge.Settings.GamepadStart)}  ·  SELECT {FormatBinding(bridge.Settings.GamepadSelect)}" +
                (bridge.SnapshotProvider()?.Rom?.IsGameBoyAdvance == true
                    ? $"\r\nGBA  L {bridge.Settings.KeyL} / {FormatBinding(bridge.Settings.GamepadL)} / LT  ·  " +
                      $"R {bridge.Settings.KeyR} / {FormatBinding(bridge.Settings.GamepadR)} / RT"
                    : string.Empty);
        }

        private void RefreshSaves()
        {
            stateFeedback.Text = bridge.SaveFeedbackProvider();
            RomSnapshot? rom = bridge.SnapshotProvider()?.Rom;
            string? romPath = bridge.RomPathProvider();
            if (!string.IsNullOrEmpty(romPath))
            {
                string statePath = WindowsRomLibrary.Default.GetStatePath(romPath, bridge.Settings.SaveSlot);
                try
                {
                    var stateFile = new FileInfo(statePath);
                    stateFeedback.Text += stateFile.Exists
                        ? global::AetherBoy.Runtime.Localization.UiText.Format("\r\nSlot {0}: Datei vorhanden · {1:g} · {2:N0} Bytes", bridge.Settings.SaveSlot, stateFile.LastWriteTime, stateFile.Length)
                        : global::AetherBoy.Runtime.Localization.UiText.Format("\r\nSlot {0}: noch leer", bridge.Settings.SaveSlot);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { stateFeedback.Text += global::AetherBoy.Runtime.Localization.UiText.Get("\r\nSlot-Datei nicht lesbar."); }
            }
            if (rom == null || string.IsNullOrEmpty(romPath))
            {
                saveStatus.Text = global::AetherBoy.Runtime.Localization.UiText.Get("NO ACTIVE CARTRIDGE");
                saveFiles.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Starte ein Spiel, um Batterie-RAM und Backup-Generationen zu prüfen.");
                return;
            }
            if (!rom.BatterySave.IsEnabled || rom.BatterySave.ExpectedLength <= 0)
            {
                saveStatus.Text = global::AetherBoy.Runtime.Localization.UiText.Get("CARTRIDGE HAS NO BATTERY RAM");
                saveFiles.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Die fünf Save-State-Slots bleiben unabhängig davon vollständig verfügbar.");
                return;
            }

            string savePath = WindowsRomLibrary.Default.GetSavePath(romPath);
            try
            {
                IReadOnlyList<BatterySaveFile> files = BatterySaveStore.Inspect(
                    savePath,
                    rom.BatterySave.ExpectedLength);
                int validBackups = files.Count(file =>
                    file.Generation >= BatterySaveGeneration.Backup1 && file.IsValid);
                BatterySaveFile current = files[0];
                saveStatus.Text = current.IsValid
                    ? global::AetherBoy.Runtime.Localization.UiText.Format("PRIMARY READY  //  {0}/3 BACKUPS", validBackups)
                    : current.Exists ? global::AetherBoy.Runtime.Localization.UiText.Format("PRIMARY DAMAGED  //  {0}/3 BACKUPS READY", validBackups) : global::AetherBoy.Runtime.Localization.UiText.Get("WAITING FOR FIRST IN-GAME SAVE");
                saveStatus.ForeColor = current.Exists && !current.IsValid
                    ? AetherColors.Danger
                    : AetherColors.Success;
                saveFiles.Text =
                    $"{Path.GetFileName(savePath)} · {rom.BatterySave.ExpectedLength:N0} Bytes\r\n" +
                    string.Join("  ·  ", files.Skip(1).Select(file =>
                        $"B{(int)file.Generation} {(file.IsValid ? "READY" : file.Exists ? "DAMAGED" : "EMPTY")}"));
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException)
            {
                saveStatus.Text = global::AetherBoy.Runtime.Localization.UiText.Get("SAVE PATH UNAVAILABLE");
                saveStatus.ForeColor = AetherColors.Danger;
                saveFiles.Text = global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message);
            }
        }

        private void RefreshSystem()
        {
            profileButton.Enabled = profileResetButton.Enabled = bridge.Settings.HasGameProfile;
            profileButton.Selected = bridge.Settings.GameProfileEnabled;
            profileButton.Text = bridge.Settings.GameProfileEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("SPIELPROFIL · AN") : global::AetherBoy.Runtime.Localization.UiText.Get("GLOBAL · PROFIL AKTIVIEREN");
            profileDetails.Text = bridge.Settings.ProfileStatus + global::AetherBoy.Runtime.Localization.UiText.Get("\r\nVideo, Lautstärke und Belegung: nur geänderte Werte werden im aktiven Spielprofil gespeichert.\r\nBoot-ROM und State-Slot bleiben global.");
            string policy = bridge.Settings.BootRomEnable ? global::AetherBoy.Runtime.Localization.UiText.Get("EXTERNE FIRMWARE · AUTO") : global::AetherBoy.Runtime.Localization.UiText.Get("INTEGRIERTER START · BYPASS");
            bootRomStatus.Text =
                global::AetherBoy.Runtime.Localization.UiText.Format("{0}\r\n{1}\r\nÄnderung gilt beim erneuten ROM-Öffnen, nicht beim Reset.", policy, bridge.FirmwareStatusProvider());
        }

        private void RefreshDiagnostics()
        {
            EmulationSnapshot? snapshot = bridge.SnapshotProvider();
            RomSnapshot? rom = snapshot?.Rom;
            HostGamepadState gamepad = bridge.GamepadProvider();
            var text = new StringBuilder();
            text.AppendLine($"AETHERBOY       {ProductInfo.DisplayName}");
            text.AppendLine(global::AetherBoy.Runtime.Localization.UiText.Format("SESSION         {0}", snapshot is null ? "Offline" : global::AetherBoy.Runtime.Localization.UiLabels.Session(snapshot.State)));
            text.AppendLine(global::AetherBoy.Runtime.Localization.UiText.Format("FRAME           {0}", snapshot?.EmulatedFrameCount.ToString("N0") ?? "—"));
            string audioRate = rom?.IsGameBoyAdvance == true ? "65,536 Hz" : "44,100 Hz";
            text.AppendLine(global::AetherBoy.Runtime.Localization.UiText.Format("AUDIO           {0} · {1}% · {2}", (bridge.Settings.AudioEnable ? global::AetherBoy.Runtime.Localization.UiText.Get("On") : global::AetherBoy.Runtime.Localization.UiText.Get("Off")), bridge.Settings.AudioVolume, audioRate));
            text.AppendLine(global::AetherBoy.Runtime.Localization.UiText.Format("VIDEO           Filter {0} · Scale {1}× · Frameskip {2}", bridge.Settings.DisplayFilterIndex, bridge.Settings.VideoScaleFactor, bridge.Settings.Frameskip));
            text.AppendLine(bridge.AudioOutputProvider());
            text.AppendLine(bridge.VideoOutputProvider());
            text.AppendLine(global::AetherBoy.Runtime.Localization.UiText.Format("INPUT           {0}", (gamepad.IsConnected ? gamepad.DeviceName : global::AetherBoy.Runtime.Localization.UiText.Get("Keyboard"))));
            text.AppendLine(global::AetherBoy.Runtime.Localization.UiText.Format("DIAGNOSE        {0} · {1}", (bridge.TesterModeProvider() ? "RECORDING LOCALLY" : global::AetherBoy.Runtime.Localization.UiText.Get("OFF")), ProductInfo.BuildChannel));
            text.AppendLine(global::AetherBoy.Runtime.Localization.UiText.Get("BEOBACHTUNG     ") + bridge.HealthStatusProvider());
            text.AppendLine(global::AetherBoy.Runtime.Localization.UiText.Get("NÄCHSTER START ") + bridge.DiagnosticsPreferenceStatusProvider());
            if (bridge.TesterLogPathProvider() is string testerLogPath)
            {
                text.AppendLine(
                    global::AetherBoy.Runtime.Localization.UiText.Format("TEST LOG        {0} / {1}", Path.GetFileName(Path.GetDirectoryName(testerLogPath)), Path.GetFileName(testerLogPath)));
            }
            text.AppendLine();
            if (rom == null)
            {
                text.AppendLine(global::AetherBoy.Runtime.Localization.UiText.Get("CARTRIDGE       None"));
            }
            else
            {
                // A ROM title can fall back to a private filename; the hash identifies this report.
                text.AppendLine(global::AetherBoy.Runtime.Localization.UiText.Format("MODEL           {0}", GetModelName(rom)));
                text.AppendLine($"MAPPER          {rom.CartridgeType}");
                text.AppendLine(global::AetherBoy.Runtime.Localization.UiText.Format("ROM SIZE        {0:N0} bytes", rom.RomSize));
                text.AppendLine(global::AetherBoy.Runtime.Localization.UiText.Format("RAM SIZE        {0:N0} bytes", rom.RamSize));
                text.AppendLine(global::AetherBoy.Runtime.Localization.UiText.Format("REGION          {0}", (rom.IsJapanese ? "Japan" : "International")));
                text.AppendLine(global::AetherBoy.Runtime.Localization.UiText.Format("SGB             {0}", (rom.HasSuperGameBoyFeatures ? global::AetherBoy.Runtime.Localization.UiText.Get("Yes") : global::AetherBoy.Runtime.Localization.UiText.Get("No"))));
                text.AppendLine($"ROM SHA-256      {rom.RomSha256}");
                text.AppendLine(global::AetherBoy.Runtime.Localization.UiText.Format("BATTERY SAVE    {0}", GetSaveSummary(rom).Replace("\r\n", " · ")));
            }
            if (snapshot?.DiagnosticEvents.Count > 0)
            {
                text.AppendLine();
                text.AppendLine(global::AetherBoy.Runtime.Localization.UiText.Get("GBA CORE EVENTS  LOCAL ONLY · NO ROM DATA · NO TELEMETRY"));
                foreach (GbaDiagnosticEventSnapshot entry in snapshot.DiagnosticEvents.TakeLast(12))
                {
                    string address = entry.Address.HasValue ? $" @ {entry.Address.Value:X8}" : string.Empty;
                    // Free-form messages can contain paths. Share only structured core context.
                    text.AppendLine($"{entry.Category,-16} C{entry.Cycle:N0}{address}");
                }
            }
            string nextDiagnostics = text.ToString();
            if (!string.Equals(diagnostics.Text, nextDiagnostics, StringComparison.Ordinal))
            {
                int firstLine = diagnostics.FirstVisibleLine;
                int selectionStart = diagnostics.SelectionStart, selectionLength = diagnostics.SelectionLength;
                diagnostics.Text = nextDiagnostics;
                int start = Math.Min(selectionStart, diagnostics.TextLength);
                diagnostics.Select(start, Math.Min(selectionLength, diagnostics.TextLength - start));
                int currentLine = diagnostics.FirstVisibleLine;
                ScrollDiagnostics(firstLine - currentLine);
            }
            bool isRecording = bridge.TesterModeProvider();
            testerExportButton.Enabled = bridge.TesterReportAvailableProvider();
            testerFolderButton.Enabled = true;
            problemMarkerButton.Enabled = isRecording;
            recordingStatus.Text = isRecording
                ? global::AetherBoy.Runtime.Localization.UiText.Get("AKTUELL · ZEICHNET LOKAL AUF\r\n") + bridge.TesterRecordingStatusProvider()
                : global::AetherBoy.Runtime.Localization.UiText.Get("AKTUELL · KEINE SITZUNGSAUFZEICHNUNG\r\n") + bridge.TesterRecordingStatusProvider();
            recordingStatus.ForeColor = isRecording ? AetherColors.Success : AetherColors.Muted;
            bool recordNext = bridge.RecordNextSessionProvider();
            recordingPreferenceButton.Selected = recordNext;
            recordingPreferenceButton.Text = recordNext ? global::AetherBoy.Runtime.Localization.UiText.Get("NÄCHSTER START · AUFZEICHNUNG AN") : global::AetherBoy.Runtime.Localization.UiText.Get("NÄCHSTER START · AUFZEICHNUNG AUS");
            recordingPreferenceStatus.Text = bridge.DiagnosticsPreferenceStatusProvider();
        }

        private void ToggleRecordingPreference()
        {
            bridge.SetRecordNextSession(!bridge.RecordNextSessionProvider());
            RefreshAll();
        }

        private void ToggleAudio()
        {
            bridge.Settings.AudioEnable = !bridge.Settings.AudioEnable;
            bridge.ApplyAudioSettings();
            RefreshAll();
        }

        private void ToggleChannel(int channel)
        {
            switch (channel)
            {
                case 0: bridge.Settings.Channel1Enable = !bridge.Settings.Channel1Enable; break;
                case 1: bridge.Settings.Channel2Enable = !bridge.Settings.Channel2Enable; break;
                case 2: bridge.Settings.Channel3Enable = !bridge.Settings.Channel3Enable; break;
                case 3: bridge.Settings.Channel4Enable = !bridge.Settings.Channel4Enable; break;
            }
            bridge.ApplyAudioSettings();
            RefreshAll();
        }

        private void SetVolume(int volume)
        {
            bridge.Settings.AudioVolume = volume;
            bridge.ApplyAudioSettings();
            RefreshAll();
        }

        private void ToggleBootRom()
        {
            bridge.Settings.BootRomEnable = !bridge.Settings.BootRomEnable;
            RefreshAll();
        }

        private void ResetSettings()
        {
            DialogResult result = AetherSignal.Show(
                this,
                global::AetherBoy.Runtime.Localization.UiText.Get("Alle Emulator-, Audio-, Video- und Eingabeeinstellungen auf Standard zurücksetzen?\n\nDie Diagnose-Aufzeichnungswahl, ROMs, Batterie-Saves und Save States bleiben unangetastet."),
                global::AetherBoy.Runtime.Localization.UiText.Get("Einstellungen zurücksetzen"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
            {
                return;
            }

            bridge.ResetSettings();
            RefreshAll();
        }

        private void CopyDiagnostics()
        {
            try
            {
                Clipboard.SetText(diagnostics.Text);
            }
            catch (Exception exception) when (
                exception is System.Runtime.InteropServices.ExternalException ||
                exception is ThreadStateException)
            {
                AetherSignal.Show(
                    this,
                    global::AetherBoy.Runtime.Localization.UiText.Format("Die Diagnose konnte nicht in die Zwischenablage kopiert werden.\n\n{0}", global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message)),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Zwischenablage nicht verfügbar"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void ScrollDiagnostics(int lines) =>
            diagnostics.ScrollLines(lines);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        private static string SafeTitle(RomSnapshot rom) =>
            string.IsNullOrWhiteSpace(rom.Title) ? global::AetherBoy.Runtime.Localization.UiText.Get("UNTITLED CARTRIDGE") : rom.Title.Trim().ToUpperInvariant();

        private static string GetModelName(RomSnapshot rom) =>
            rom.IsGameBoyAdvance ? "GBA" : rom.HasColorFeatures ? "CGB" : "DMG";

        private static string GetSaveSummary(RomSnapshot? rom)
        {
            if (rom == null)
            {
                return global::AetherBoy.Runtime.Localization.UiText.Get("NO CARTRIDGE\r\nNO SAVE ROUTE");
            }
            if (!rom.BatterySave.IsEnabled)
            {
                return global::AetherBoy.Runtime.Localization.UiText.Get("NO BATTERY RAM\r\nSTATE SLOTS READY");
            }
            if (rom.BatterySave.RecoveredFromBackup)
            {
                return global::AetherBoy.Runtime.Localization.UiText.Format("RECOVERED · B{0}\r\nAUTO-REPAIR ACTIVE", rom.BatterySave.LoadedGeneration);
            }
            return global::AetherBoy.Runtime.Localization.UiText.Get("BATTERY RAM ARMED\r\n3 BACKUP GENERATIONS");
        }

        private static string FormatBinding(HostGamepadButtons binding) =>
            binding.ToString().Replace(global::AetherBoy.Runtime.Localization.UiText.Get("Shoulder"), string.Empty, StringComparison.Ordinal);
    }
}
