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
    internal sealed class frmControlCenter : Form
    {
        private readonly ControlCenterBridge bridge;
        private readonly Dictionary<string, Panel> pages = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AetherButton> navigation = new(StringComparer.Ordinal);
        private readonly List<AetherButton> filterButtons = new();
        private readonly List<AetherButton> paletteButtons = new();
        private readonly List<AetherButton> scaleButtons = new();
        private readonly List<AetherButton> volumeButtons = new();
        private readonly List<AetherButton> latencyButtons = new();
        private AetherButton gpuButton = null!, vsyncButton = null!, integerButton = null!;
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
        private Label inputStatus = null!;
        private Label inputIdentity = null!;
        private Label inputBindings = null!;
        private Label saveStatus = null!;
        private Label saveFiles = null!;
        private Label bootRomStatus = null!;
        private RichTextBox diagnostics = null!;
        private AetherButton testerExportButton = null!;
        private AetherButton testerFolderButton = null!;
        private AetherButton recordingPreferenceButton = null!;
        private AetherButton problemMarkerButton = null!;
        private Label recordingStatus = null!, recordingPreferenceStatus = null!;

        public frmControlCenter(ControlCenterBridge bridge)
        {
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));

            Text = "Aether Control Center";
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
            AddPage("display", BuildDisplayPage());
            AddPage("audio", BuildAudioPage());
            AddPage("input", BuildInputPage());
            AddPage("saves", BuildSavesPage());
            AddPage("system", BuildSystemPage());
            AddPage("diagnostics", BuildDiagnosticsPage());
            AddPage("storage", BuildStoragePage());

            AetherDialog.Apply(
                this,
                "CONTROL CENTER // 09.2",
                "Alle lokalen Emulator-, Eingabe- und Sicherheitsfunktionen an einem Ort");
            // This pane has its own Aether card and scroll actions, not native white chrome.
            diagnostics.BorderStyle = BorderStyle.None;
            diagnostics.ScrollBars = RichTextBoxScrollBars.None;

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
                ForeColor = AetherColors.Cyan,
                Location = new Point(18, 20),
                Size = new Size(180, 20),
                Tag = "accent",
                Text = "AETHER SYSTEM MATRIX"
            });
            nav.Controls.Add(new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI Semibold", 15f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = AetherColors.Text,
                Location = new Point(18, 42),
                Size = new Size(182, 54),
                Tag = "value",
                Text = "CONTROL\r\nCENTER"
            });

            AddNavigationButton(nav, "overview", "01  OVERVIEW", 114);
            AddNavigationButton(nav, "display", "02  DISPLAY", 158);
            AddNavigationButton(nav, "audio", "03  AUDIO", 202);
            AddNavigationButton(nav, "input", "04  INPUT", 246);
            AddNavigationButton(nav, "saves", "05  SAVES", 290);
            AddNavigationButton(nav, "system", "06  SYSTEM", 334);
            AddNavigationButton(nav, "diagnostics", "07  DIAGNOSTICS", 378);
            AddNavigationButton(nav, "storage", "08  ORDNER", 422);

            var privacy = new Label
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
                AutoSize = false,
                Font = new Font("Cascadia Mono", 7.25f, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = AetherColors.Muted,
                Location = new Point(18, 575),
                Size = new Size(180, 66),
                Text = "LOCAL CONTROL PLANE\r\nNO CLOUD SYNC\r\nNO TELEMETRY"
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
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            pages.Add(key, page);
            contentHost.Controls.Add(page);
        }

        private void ShowPage(string key)
        {
            foreach ((string pageKey, Panel page) in pages)
            {
                page.Visible = pageKey == key;
                if (page.Visible)
                {
                    page.BringToFront();
                }
            }
            foreach ((string navKey, AetherButton button) in navigation)
            {
                button.Selected = navKey == key;
            }
            RefreshAll();
        }

        private Panel BuildOverviewPage()
        {
            Panel page = CreatePage("controlCenterPageOverview", "OVERVIEW", "Live-Zustand und direkte Werkzeuge");
            AetherSurfacePanel signal = CreateCard(page, 0, 72, 788, 92, "SESSION SIGNAL");
            overviewState = CreateValueLabel(signal, 20, 36, 748, 38, 15f);

            AetherSurfacePanel game = CreateCard(page, 0, 180, 252, 126, "CARTRIDGE");
            overviewGame = CreateValueLabel(game, 18, 38, 216, 70, 10f);
            AetherSurfacePanel input = CreateCard(page, 268, 180, 252, 126, "INPUT ROUTE");
            overviewInput = CreateValueLabel(input, 18, 38, 216, 70, 10f);
            AetherSurfacePanel save = CreateCard(page, 536, 180, 252, 126, "SAVE SAFETY");
            overviewSave = CreateValueLabel(save, 18, 38, 216, 70, 10f);

            AetherSurfacePanel actions = CreateCard(page, 0, 322, 788, 180, "QUICK ACCESS // OPERATOR DECK");
            AddActionButton(actions, "CONTROL MAPPING", 18, 46, 230, bridge.OpenControls);
            AddActionButton(actions, "SAVE SAFETY", 264, 46, 230, bridge.OpenSaveSafety);
            AddActionButton(actions, "AUDIO INSPECTOR", 510, 46, 230, bridge.OpenAudioInspector);
            AddActionButton(actions, "QUICK SAVE", 18, 104, 230, bridge.QuickSave, AetherButtonKind.Primary);
            AddActionButton(actions, "QUICK LOAD", 264, 104, 230, bridge.QuickLoad);
            AddActionButton(actions, "FULLSCREEN", 510, 104, 230, bridge.ToggleFullscreen);

            page.Controls.Add(CreateFootnote(
                "Alle Einstellungen bleiben lokal. Firmware gilt beim erneuten ROM-Öffnen, die Diagnose-Aufzeichnung ab dem nächsten Programmstart.",
                522));
            return page;
        }

        private Panel BuildDisplayPage()
        {
            Panel page = CreatePage("controlCenterPageDisplay", "DISPLAY", "Bildcharakter, Palette und Fenstergeometrie");
            AetherSurfacePanel filter = CreateCard(page, 0, 72, 788, 116, "PIXEL PIPELINE");
            AddSelector(filter, new[] { "SHARP", "SMOOTH", "LCD GRID" }, 18, 48, 236, filterButtons, bridge.SetDisplayFilter);

            AetherSurfacePanel palette = CreateCard(page, 0, 204, 788, 132, "DMG COLOR SIGNATURE");
            AddSelector(
                palette,
                new[] { "POCKET", "PEA GREEN", "GB LIGHT", "SEPIA", "CYBER" },
                18,
                50,
                140,
                paletteButtons,
                bridge.SetPalette);

            AetherSurfacePanel geometry = CreateCard(page, 0, 352, 788, 152, "WINDOW GEOMETRY");
            AddSelector(geometry, new[] { "1×", "2×", "3×", "4×" }, 18, 50, 128, scaleButtons, index => bridge.SetWindowScale(index + 1));
            AddActionButton(geometry, "BORDERLESS FULLSCREEN", 570, 50, 198, bridge.ToggleFullscreen, AetherButtonKind.Primary);
            geometry.Controls.Add(CreateSmallLabel(
                "F11 / Alt+Enter: Vollbild · Esc: zurück. Integer Scaling hält Pixel ganzzahlig; das Seitenverhältnis bleibt erhalten.",
                18,
                102,
                750,
                30));
            AetherSurfacePanel renderer = CreateCard(page, 0, 520, 788, 154, "WINDOWS PRESENTATION");
            gpuButton = AddActionButton(renderer, "GPU", 18, 38, 230, () =>
            { bridge.Settings.GpuRendering = !bridge.Settings.GpuRendering; bridge.ApplyVideoSettings(); RefreshAll(); });
            vsyncButton = AddActionButton(renderer, "VSYNC", 270, 38, 230, () =>
            { bridge.Settings.VideoVSync = !bridge.Settings.VideoVSync; bridge.ApplyVideoSettings(); RefreshAll(); });
            integerButton = AddActionButton(renderer, "INTEGER", 522, 38, 230, () =>
            { bridge.Settings.IntegerScaling = !bridge.Settings.IntegerScaling; bridge.ApplyVideoSettings(); RefreshAll(); });
            gpuButton.Name = "controlCenterGpuButton";
            vsyncButton.Name = "controlCenterVSyncButton";
            integerButton.Name = "controlCenterIntegerButton";
            videoDetails = CreateSmallLabel("", 18, 94, 746, 50);
            renderer.Controls.Add(videoDetails);
            return page;
        }

        private Panel BuildAudioPage()
        {
            Panel page = CreatePage("controlCenterPageAudio", "AUDIO", "Master-Ausgabe, Kanäle und Pegel");
            AetherSurfacePanel master = CreateCard(page, 0, 72, 788, 112, "MASTER OUTPUT // WASAPI + WINMM FALLBACK");
            audioMasterButton = AddActionButton(master, "AUDIO ON", 18, 48, 200, ToggleAudio, AetherButtonKind.Primary);
            audioDetails = CreateSmallLabel("", 242, 34, 510, 74);
            master.Controls.Add(audioDetails);

            AetherSurfacePanel channels = CreateCard(page, 0, 200, 788, 124, "CHANNEL MATRIX");
            string[] channelNames = { "CH1 PULSE", "CH2 PULSE", "CH3 WAVE", "CH4 NOISE" };
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

            AetherSurfacePanel volume = CreateCard(page, 0, 340, 788, 130, "OUTPUT LEVEL");
            AddSelector(
                volume,
                new[] { "MUTE", "25%", "50%", "75%", "100%" },
                18,
                50,
                140,
                volumeButtons,
                index => SetVolume(index * 25));

            AetherSurfacePanel tools = CreateCard(page, 0, 486, 788, 86, "SIGNAL TOOL");
            AddActionButton(tools, "OPEN AUDIO INSPECTOR", 18, 36, 260, bridge.OpenAudioInspector, AetherButtonKind.Secondary);
            tools.Controls.Add(CreateSmallLabel("Live-Kanäle, Frequenzen und Wave-RAM ansehen oder als WAV aufnehmen.", 298, 38, 460, 30));
            AetherSurfacePanel latency = CreateCard(page, 0, 588, 788, 144, "OUTPUT LATENCY // WINDOWS DEFAULT DEVICE");
            AddSelector(latency, new[] { "20 MS", "40 MS", "60 MS", "100 MS" }, 18, 38, 170,
                latencyButtons, index =>
                {
                    bridge.Settings.AudioLatencyMs = new[] { 20, 40, 60, 100 }[index];
                    bridge.ApplyAudioSettings();
                    RefreshAll();
                });
            latency.Controls.Add(CreateSmallLabel(
                "40 ms Standard. Bei Knacken 60/100 ms wählen. Gerätewechsel folgt Windows automatisch.\r\nZielpuffer ≠ Gesamtlatenz; Vorpuffer, Resampling und Treiber kommen hinzu. GB/GBC: Mono · GBA: Stereo.",
                18, 92, 748, 44));
            return page;
        }

        private Panel BuildInputPage()
        {
            Panel page = CreatePage("controlCenterPageInput", "INPUT", "Tastatur, Gamepad und Live-Erkennung");
            AetherSurfacePanel live = CreateCard(page, 0, 72, 788, 128, "ACTIVE CONTROLLER");
            inputStatus = CreateValueLabel(live, 18, 38, 748, 38, 13f);
            inputIdentity = CreateSmallLabel("WINDOWS GAME INPUT + XINPUT FALLBACK", 18, 78, 748, 28);
            live.Controls.Add(inputIdentity);

            AetherSurfacePanel mapping = CreateCard(page, 0, 216, 788, 160, "CURRENT MAP");
            inputBindings = CreateValueLabel(mapping, 18, 40, 748, 100, 9f);

            AetherSurfacePanel editor = CreateCard(page, 0, 392, 788, 132, "INPUT MATRIX EDITOR");
            AddActionButton(editor, "OPEN CONTROL MAPPING", 18, 50, 276, bridge.OpenControls, AetherButtonKind.Primary);
            editor.Controls.Add(CreateSmallLabel(
                "Tastatur- und Controller-Belegung mit Live-Capture. D-Pad und linker Stick bleiben gemeinsam als Richtungseingabe aktiv.",
                318,
                44,
                444,
                56));
            return page;
        }

        private Panel BuildSavesPage()
        {
            Panel page = CreatePage("controlCenterPageSaves", "SAVES", "Save States und Batterie-RAM-Schutz");
            AetherSurfacePanel slots = CreateCard(page, 0, 72, 788, 116, "ACTIVE STATE SLOT");
            AddSelector(slots, new[] { "SLOT 1", "SLOT 2", "SLOT 3", "SLOT 4", "SLOT 5" }, 18, 48, 140, slotButtons, index => bridge.SetSaveSlot(index + 1));

            AetherSurfacePanel operations = CreateCard(page, 0, 204, 788, 106, "STATE OPERATIONS");
            AddActionButton(operations, "QUICK SAVE · F5", 18, 46, 230, bridge.QuickSave, AetherButtonKind.Primary);
            AddActionButton(operations, "QUICK LOAD · F8", 264, 46, 230, bridge.QuickLoad);
            AddActionButton(operations, "SAVE SAFETY CENTER", 510, 46, 230, bridge.OpenSaveSafety);

            AetherSurfacePanel battery = CreateCard(page, 0, 326, 788, 170, "BATTERY RAM // ROTATING BACKUPS");
            saveStatus = CreateValueLabel(battery, 18, 38, 748, 46, 11f);
            saveFiles = CreateSmallLabel("Keine Cartridge aktiv.", 18, 88, 748, 62);
            battery.Controls.Add(saveFiles);
            page.Controls.Add(CreateFootnote(
                "Save States (.ss1–.ss5) und originale Batterie-Saves (.sav) sind getrennte Systeme. Save Safety schützt ausschließlich Batterie-RAM.",
                516));
            stateFeedback = CreateSmallLabel("", 0, 566, 788, 72);
            stateFeedback.Name = "controlCenterStateFeedback";
            page.Controls.Add(stateFeedback);
            AddActionButton(page, "STATE-GALERIE / FORTSETZEN / RÜCKGÄNGIG", 0, 650, 540,
                bridge.OpenStateGallery, AetherButtonKind.Primary).Name = "controlCenterStateGalleryButton";
            return page;
        }

        private Panel BuildSystemPage()
        {
            Panel page = CreatePage("controlCenterPageSystem", "SYSTEM", "Emulationsverhalten und lokale Firmware");
            AetherSurfacePanel frames = CreateCard(page, 0, 72, 788, 130, "FRAME PRESENTATION");
            AddSelector(frames, new[] { "NONE", "SKIP 1", "SKIP 2", "SKIP 3", "SKIP 4" }, 18, 48, 140, frameskipButtons, bridge.SetFrameskip);
            frames.Controls.Add(CreateSmallLabel("Frameskip verändert nur die Bildausgabe, nicht die emulierte Hardwarezeit.", 18, 91, 748, 28));

            AetherSurfacePanel boot = CreateCard(page, 0, 218, 788, 142, "BOOT ROM POLICY");
            bootRomButton = AddActionButton(boot, "BOOT ROM · AUTO", 18, 40, 230, ToggleBootRom, AetherButtonKind.Secondary);
            AddActionButton(boot, "FIRMWARE VERWALTEN", 18, 90, 230,
                () => { bridge.OpenFirmwareManager(); RefreshAll(); }).Name = "controlCenterFirmwareManagerButton";
            bootRomStatus = CreateSmallLabel(string.Empty, 270, 36, 492, 98);
            bootRomStatus.Name = "controlCenterFirmwareStatus";
            boot.Controls.Add(bootRomStatus);

            AetherSurfacePanel reset = CreateCard(page, 0, 376, 788, 152, "SETTINGS RECOVERY");
            AddActionButton(reset, "RESET ALL SETTINGS", 18, 50, 240, ResetSettings, AetherButtonKind.Danger);
            reset.Controls.Add(CreateSmallLabel(
                "Setzt Video, Audio, Eingabe, Save-Slot und Boot-ROM-Verhalten zurück. Die Diagnose-Aufzeichnungswahl, ROMs und Spielstände bleiben erhalten.",
                286,
                42,
                476,
                72));
            AetherSurfacePanel profile = CreateCard(page, 0, 544, 788, 172, "SPIELPROFIL // GLOBALE WERTE BLEIBEN ERHALTEN");
            profileButton = AddActionButton(profile, "PROFIL", 18, 40, 320, () => { bridge.ToggleGameProfile(); RefreshAll(); });
            profileButton.Name = "controlCenterGameProfileButton";
            profileResetButton = AddActionButton(profile, "ÜBERSCHREIBUNGEN ZURÜCKSETZEN", 360, 40, 400,
                () => { bridge.ResetGameProfile(); RefreshAll(); });
            profileDetails = CreateSmallLabel("", 18, 92, 748, 70);
            profile.Controls.Add(profileDetails);
            return page;
        }

        private Panel BuildStoragePage()
        {
            Panel page = CreatePage("controlCenterPageStorage", "LOKALE ORDNER", "Deine Bibliothek, Spielstände und Einstellungen");
            WindowsDataPaths paths = WindowsDataPaths.Default;
            var folders = new[]
            {
                ("AETHERBOY-ORDNER", paths.Root, "Alle lokalen Daten", "Root"),
                ("ROM-ORDNER ÖFFNEN", paths.Roms, "Importierte Kopien deiner GB-, GBC- und GBA-ROMs", "Roms"),
                ("SPIELSTÄNDE ÖFFNEN", paths.Saves, "Batterie-Saves, RTC und drei rotierende Sicherungen", "Saves"),
                ("SAVE STATES ÖFFNEN", paths.States, "Fünf Slots pro ROM-Inhalt", "States"),
                ("EINSTELLUNGEN", paths.Settings, "Belegung, Bild, Audio und zuletzt gespielte Titel", "Settings"),
                ("FIRMWARE-ORDNER", paths.Firmware, "Optional: dmg_boot.bin, gbc_boot.bin, gba_bios.bin", "Firmware"),
                ("SCREENSHOTS ÖFFNEN", paths.Screenshots, "Unveränderte Spielbilder als PNG, getrennt nach ROM", "Screenshots"),
                ("DEVELOPMENT-ORDNER", paths.Development, "Lokale Sitzungsberichte und Development-Crashlogs", "Development")
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
            page.Controls.Add(CreateSmallLabel(paths.Root + "\r\nROM-Originale bleiben beim Import erhalten. Bereits zentrale Spielstände haben Vorrang.",
                0, y + 4, 788, 60));
            return page;
        }

        private Panel BuildDiagnosticsPage()
        {
            Panel page = CreatePage("controlCenterPageDiagnostics", "DIAGNOSTICS", "Lokale Aufzeichnung, transparente Freigabe und Laufzeit-Werkzeuge");
            AetherSurfacePanel recording = CreateCard(page, 0, 72, 788, 150, "LOCAL RECORDER // DEINE ENTSCHEIDUNG");
            recordingStatus = CreateSmallLabel("", 18, 34, 750, 48);
            recordingStatus.Name = "controlCenterRecordingStatus";
            recording.Controls.Add(recordingStatus);
            recordingPreferenceButton = AddActionButton(recording, "NÄCHSTER START · AUFZEICHNUNG", 18, 88, 334,
                ToggleRecordingPreference);
            recordingPreferenceButton.Name = "controlCenterRecordNextSessionButton";
            recordingPreferenceStatus = CreateSmallLabel("", 370, 78, 396, 64);
            recordingPreferenceStatus.Name = "controlCenterRecordingPreferenceStatus";
            recording.Controls.Add(recordingPreferenceStatus);

            AetherSurfacePanel summary = CreateCard(page, 0, 238, 788, 236, "SHAREABLE SUMMARY // OHNE DATEIPFADE");
            diagnostics = new RichTextBox
            {
                BackColor = AetherColors.SurfaceRaised,
                BorderStyle = BorderStyle.None,
                Font = new Font("Cascadia Mono", 9f, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = AetherColors.Text,
                Location = new Point(18, 38),
                Name = "controlCenterDiagnosticsText",
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Size = new Size(704, 182),
                DetectUrls = false,
                WordWrap = true
            };
            summary.Controls.Add(diagnostics);
            AddActionButton(summary, "↑", 738, 38, 32, () => ScrollDiagnostics(-8)).AccessibleName = "Diagnose nach oben scrollen";
            AddActionButton(summary, "↓", 738, 178, 32, () => ScrollDiagnostics(8)).AccessibleName = "Diagnose nach unten scrollen";
            AetherButton copyButton = AddActionButton(
                page,
                "COPY DIAGNOSTICS",
                0,
                490,
                220,
                CopyDiagnostics,
                AetherButtonKind.Primary);
            copyButton.Name = "controlCenterCopyDiagnosticsButton";
            testerExportButton = AddActionButton(
                page,
                "EXPORT TEST REPORT",
                236,
                490,
                260,
                bridge.ExportTesterReport);
            testerExportButton.Name = "controlCenterExportTesterReportButton";
            testerFolderButton = AddActionButton(
                page,
                "OPEN TEST FOLDER",
                512,
                490,
                250,
                bridge.OpenTesterFolder);
            testerFolderButton.Name = "controlCenterOpenTesterFolderButton";
            page.Controls.Add(CreateSmallLabel(
                "Nur lokal, kein automatischer Upload. Berichte enthalten Build-/Gerätedaten, ROM-Kennung und Laufzeit-Ereignisse; keine ROM-/Save-Inhalte oder Dateipfade. Minimale Crashlogs bleiben auch ohne Sitzungsaufzeichnung aktiv. Vorhandene Berichte bleiben erhalten.",
                2,
                602,
                786,
                70));
            AddActionButton(page, "SCREENSHOT · F12", 0, 546, 184, bridge.CaptureScreenshot).Name = "controlCenterScreenshotButton";
            AddActionButton(page, "PERFORMANCE · F9", 200, 546, 184, bridge.TogglePerformanceOverlay).Name = "controlCenterOverlayButton";
            AddActionButton(page, "QUICK DECK · F10", 400, 546, 184, bridge.OpenQuickMenu).Name = "controlCenterQuickMenuButton";
            problemMarkerButton = AddActionButton(page, "PROBLEM MARKIEREN", 600, 546, 184, bridge.MarkProblem);
            problemMarkerButton.Name = "controlCenterMarkProblemButton";
            return page;
        }

        private static Panel CreatePage(string name, string title, string subtitle)
        {
            var page = new Panel
            {
                AutoScroll = true,
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
            RefreshSelectors();
            RefreshOverview();
            RefreshInput();
            RefreshSaves();
            RefreshSystem();
            RefreshDiagnostics();
        }

        private void RefreshSelectors()
        {
            SelectIndex(filterButtons, bridge.Settings.DisplayFilterIndex);
            SelectIndex(paletteButtons, bridge.Settings.PaletteIndex);
            SelectIndex(scaleButtons, bridge.Settings.VideoScaleFactor - 1);
            SelectIndex(volumeButtons, (int)Math.Round(bridge.Settings.AudioVolume / 25d));
            SelectIndex(latencyButtons, Array.IndexOf(new[] { 20, 40, 60, 100 }, bridge.Settings.AudioLatencyMs));
            gpuButton.Selected = bridge.Settings.GpuRendering;
            gpuButton.Text = bridge.Settings.GpuRendering ? "GPU · ON" : "CPU · GDI";
            vsyncButton.Selected = bridge.Settings.VideoVSync;
            vsyncButton.Enabled = bridge.Settings.GpuRendering;
            vsyncButton.Text = bridge.Settings.VideoVSync ? "VSYNC · ON" : "VSYNC · OFF";
            integerButton.Selected = bridge.Settings.IntegerScaling;
            integerButton.Text = bridge.Settings.IntegerScaling ? "INTEGER · ON" : "INTEGER · OFF";
            videoDetails.Text = bridge.VideoOutputProvider();
            SelectIndex(frameskipButtons, bridge.Settings.Frameskip);
            SelectIndex(slotButtons, bridge.Settings.SaveSlot - 1);

            audioMasterButton.Selected = bridge.Settings.AudioEnable;
            audioMasterButton.Text = bridge.Settings.AudioEnable ? "AUDIO · ON" : "AUDIO · OFF";
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
            bootRomButton.Text = bridge.Settings.BootRomEnable ? "BOOT ROM · AUTO" : "BOOT ROM · BYPASS";
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
                ? "OFFLINE  //  READY FOR CARTRIDGE"
                : $"{snapshot.State.ToString().ToUpperInvariant()}  //  FRAME {snapshot.EmulatedFrameCount:N0}";
            overviewGame.Text = rom == null
                ? "NO CARTRIDGE\r\nDMG / CGB / GBA READY"
                : $"{SafeTitle(rom)}\r\n{GetModelName(rom)} · {rom.CartridgeType}";

            HostGamepadState gamepad = bridge.GamepadProvider();
            overviewInput.Text = gamepad.IsConnected
                ? $"GAMEPAD LIVE\r\n{gamepad.DeviceName?.ToUpperInvariant() ?? "CONNECTED"}"
                : "KEYBOARD ACTIVE\r\nGAMEPAD STANDBY";
            overviewSave.Text = GetSaveSummary(rom);
        }

        private void RefreshInput()
        {
            HostGamepadState gamepad = bridge.GamepadProvider();
            inputStatus.Text = gamepad.IsConnected
                ? gamepad.DeviceName?.ToUpperInvariant() ?? "GAMEPAD CONNECTED"
                : "NO GAMEPAD CONNECTED";
            inputStatus.ForeColor = gamepad.IsConnected ? AetherColors.Success : AetherColors.Muted;
            string source = gamepad.Source switch
            {
                GamepadInputSource.WindowsGamingInput => "WINDOWS GAME INPUT",
                GamepadInputSource.XInput => "XINPUT FALLBACK",
                _ => "KEYBOARD ROUTE"
            };
            string hardware = gamepad.VendorId == 0 && gamepad.ProductId == 0
                ? string.Empty
                : $" · USB {gamepad.VendorId:X4}:{gamepad.ProductId:X4}";
            inputIdentity.Text = source + hardware;
            inputBindings.Text =
                $"KEYBOARD  A {bridge.Settings.KeyA}  ·  B {bridge.Settings.KeyB}  ·  START {bridge.Settings.KeyStart}  ·  SELECT {bridge.Settings.KeySelect}\r\n" +
                $"DIRECTION  {bridge.Settings.KeyUp}/{bridge.Settings.KeyDown}/{bridge.Settings.KeyLeft}/{bridge.Settings.KeyRight}\r\n" +
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
                        ? $"\r\nSlot {bridge.Settings.SaveSlot}: Datei vorhanden · {stateFile.LastWriteTime:g} · {stateFile.Length:N0} Bytes"
                        : $"\r\nSlot {bridge.Settings.SaveSlot}: noch leer";
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { stateFeedback.Text += "\r\nSlot-Datei nicht lesbar."; }
            }
            if (rom == null || string.IsNullOrEmpty(romPath))
            {
                saveStatus.Text = "NO ACTIVE CARTRIDGE";
                saveFiles.Text = "Starte ein Spiel, um Batterie-RAM und Backup-Generationen zu prüfen.";
                return;
            }
            if (!rom.BatterySave.IsEnabled || rom.BatterySave.ExpectedLength <= 0)
            {
                saveStatus.Text = "CARTRIDGE HAS NO BATTERY RAM";
                saveFiles.Text = "Die fünf Save-State-Slots bleiben unabhängig davon vollständig verfügbar.";
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
                    ? $"PRIMARY READY  //  {validBackups}/3 BACKUPS"
                    : current.Exists ? $"PRIMARY DAMAGED  //  {validBackups}/3 BACKUPS READY" : "WAITING FOR FIRST IN-GAME SAVE";
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
                saveStatus.Text = "SAVE PATH UNAVAILABLE";
                saveStatus.ForeColor = AetherColors.Danger;
                saveFiles.Text = exception.Message;
            }
        }

        private void RefreshSystem()
        {
            profileButton.Enabled = profileResetButton.Enabled = bridge.Settings.HasGameProfile;
            profileButton.Selected = bridge.Settings.GameProfileEnabled;
            profileButton.Text = bridge.Settings.GameProfileEnabled ? "SPIELPROFIL · AN" : "GLOBAL · PROFIL AKTIVIEREN";
            profileDetails.Text = bridge.Settings.ProfileStatus + "\r\nVideo, Lautstärke und Belegung: nur geänderte Werte werden im aktiven Spielprofil gespeichert.\r\nBoot-ROM und State-Slot bleiben global.";
            string policy = bridge.Settings.BootRomEnable ? "EXTERNE FIRMWARE · AUTO" : "INTEGRIERTER START · BYPASS";
            bootRomStatus.Text =
                $"{policy}\r\n{bridge.FirmwareStatusProvider()}\r\nÄnderung gilt beim erneuten ROM-Öffnen, nicht beim Reset.";
        }

        private void RefreshDiagnostics()
        {
            EmulationSnapshot? snapshot = bridge.SnapshotProvider();
            RomSnapshot? rom = snapshot?.Rom;
            HostGamepadState gamepad = bridge.GamepadProvider();
            var text = new StringBuilder();
            text.AppendLine($"AETHERBOY       {ProductInfo.DisplayName}");
            text.AppendLine($"SESSION         {snapshot?.State.ToString() ?? "Offline"}");
            text.AppendLine($"FRAME           {snapshot?.EmulatedFrameCount.ToString("N0") ?? "—"}");
            string audioRate = rom?.IsGameBoyAdvance == true ? "65,536 Hz" : "44,100 Hz";
            text.AppendLine($"AUDIO           {(bridge.Settings.AudioEnable ? "On" : "Off")} · {bridge.Settings.AudioVolume}% · {audioRate}");
            text.AppendLine($"VIDEO           Filter {bridge.Settings.DisplayFilterIndex} · Scale {bridge.Settings.VideoScaleFactor}× · Frameskip {bridge.Settings.Frameskip}");
            text.AppendLine(bridge.AudioOutputProvider());
            text.AppendLine(bridge.VideoOutputProvider());
            text.AppendLine($"INPUT           {(gamepad.IsConnected ? gamepad.DeviceName : "Keyboard")}");
            text.AppendLine($"DIAGNOSE        {(bridge.TesterModeProvider() ? "RECORDING LOCALLY" : "OFF")} · {ProductInfo.BuildChannel}");
            text.AppendLine("BEOBACHTUNG     " + bridge.HealthStatusProvider());
            text.AppendLine("NÄCHSTER START " + bridge.DiagnosticsPreferenceStatusProvider());
            if (bridge.TesterLogPathProvider() is string testerLogPath)
            {
                text.AppendLine(
                    $"TEST LOG        {Path.GetFileName(Path.GetDirectoryName(testerLogPath))} / {Path.GetFileName(testerLogPath)}");
            }
            text.AppendLine();
            if (rom == null)
            {
                text.AppendLine("CARTRIDGE       None");
            }
            else
            {
                // A ROM title can fall back to a private filename; the hash identifies this report.
                text.AppendLine($"MODEL           {GetModelName(rom)}");
                text.AppendLine($"MAPPER          {rom.CartridgeType}");
                text.AppendLine($"ROM SIZE        {rom.RomSize:N0} bytes");
                text.AppendLine($"RAM SIZE        {rom.RamSize:N0} bytes");
                text.AppendLine($"REGION          {(rom.IsJapanese ? "Japan" : "International")}");
                text.AppendLine($"SGB             {(rom.HasSuperGameBoyFeatures ? "Yes" : "No")}");
                text.AppendLine($"ROM SHA-256      {rom.RomSha256}");
                text.AppendLine($"BATTERY SAVE    {GetSaveSummary(rom).Replace("\r\n", " · ")}");
            }
            if (snapshot?.DiagnosticEvents.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("GBA CORE EVENTS  LOCAL ONLY · NO ROM DATA · NO TELEMETRY");
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
                int firstLine = (int)SendMessage(diagnostics.Handle, 0x00CE /* EM_GETFIRSTVISIBLELINE */, IntPtr.Zero, IntPtr.Zero);
                int selectionStart = diagnostics.SelectionStart, selectionLength = diagnostics.SelectionLength;
                diagnostics.Text = nextDiagnostics;
                int start = Math.Min(selectionStart, diagnostics.TextLength);
                diagnostics.Select(start, Math.Min(selectionLength, diagnostics.TextLength - start));
                int currentLine = (int)SendMessage(diagnostics.Handle, 0x00CE, IntPtr.Zero, IntPtr.Zero);
                ScrollDiagnostics(firstLine - currentLine);
            }
            bool isRecording = bridge.TesterModeProvider();
            testerExportButton.Enabled = bridge.TesterReportAvailableProvider();
            testerFolderButton.Enabled = true;
            problemMarkerButton.Enabled = isRecording;
            recordingStatus.Text = isRecording
                ? "AKTUELL · ZEICHNET LOKAL AUF\r\n" + bridge.TesterRecordingStatusProvider()
                : "AKTUELL · KEINE SITZUNGSAUFZEICHNUNG\r\n" + bridge.TesterRecordingStatusProvider();
            recordingStatus.ForeColor = isRecording ? AetherColors.Success : AetherColors.Muted;
            bool recordNext = bridge.RecordNextSessionProvider();
            recordingPreferenceButton.Selected = recordNext;
            recordingPreferenceButton.Text = recordNext ? "NÄCHSTER START · AUFZEICHNUNG AN" : "NÄCHSTER START · AUFZEICHNUNG AUS";
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
                "Alle Emulator-, Audio-, Video- und Eingabeeinstellungen auf Standard zurücksetzen?\n\nDie Diagnose-Aufzeichnungswahl, ROMs, Batterie-Saves und Save States bleiben unangetastet.",
                "Einstellungen zurücksetzen",
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
                    $"Die Diagnose konnte nicht in die Zwischenablage kopiert werden.\n\n{exception.Message}",
                    "Zwischenablage nicht verfügbar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void ScrollDiagnostics(int lines) =>
            SendMessage(diagnostics.Handle, 0x00B6 /* EM_LINESCROLL */, IntPtr.Zero, (IntPtr)lines);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        private static string SafeTitle(RomSnapshot rom) =>
            string.IsNullOrWhiteSpace(rom.Title) ? "UNTITLED CARTRIDGE" : rom.Title.Trim().ToUpperInvariant();

        private static string GetModelName(RomSnapshot rom) =>
            rom.IsGameBoyAdvance ? "GBA" : rom.HasColorFeatures ? "CGB" : "DMG";

        private static string GetSaveSummary(RomSnapshot? rom)
        {
            if (rom == null)
            {
                return "NO CARTRIDGE\r\nNO SAVE ROUTE";
            }
            if (!rom.BatterySave.IsEnabled)
            {
                return "NO BATTERY RAM\r\nSTATE SLOTS READY";
            }
            if (rom.BatterySave.RecoveredFromBackup)
            {
                return $"RECOVERED · B{rom.BatterySave.LoadedGeneration}\r\nAUTO-REPAIR ACTIVE";
            }
            return "BATTERY RAM ARMED\r\n3 BACKUP GENERATIONS";
        }

        private static string FormatBinding(HostGamepadButtons binding) =>
            binding.ToString().Replace("Shoulder", string.Empty, StringComparison.Ordinal);
    }
}
