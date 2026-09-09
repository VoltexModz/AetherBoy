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

        public frmControlCenter(ControlCenterBridge bridge)
        {
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));

            Text = "Aether Control Center";
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            ClientSize = new Size(1_060, 680);
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

            AetherDialog.Apply(
                this,
                "CONTROL CENTER // 09.2",
                "Alle lokalen Emulator-, Eingabe- und Sicherheitsfunktionen an einem Ort");

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
                "Das Control Center arbeitet ausschließlich lokal. Änderungen werden sofort angewendet und persistent gespeichert.",
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
                "Integer-Faktoren bewahren harte Pixelkanten; das Display bleibt unabhängig von der Fenstergröße im korrekten Seitenverhältnis.",
                18,
                102,
                750,
                30));
            return page;
        }

        private Panel BuildAudioPage()
        {
            Panel page = CreatePage("controlCenterPageAudio", "AUDIO", "Master-Ausgabe, Kanäle und Pegel");
            AetherSurfacePanel master = CreateCard(page, 0, 72, 788, 112, "MASTER OUTPUT // WINMM");
            audioMasterButton = AddActionButton(master, "AUDIO ON", 18, 48, 200, ToggleAudio, AetherButtonKind.Primary);
            audioDetails = CreateSmallLabel("Dynamische Core-Rate · Mono Host Mix · 100 ms Ziel-Latenz", 242, 50, 510, 32);
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
            return page;
        }

        private Panel BuildSystemPage()
        {
            Panel page = CreatePage("controlCenterPageSystem", "SYSTEM", "Emulationsverhalten und lokale Firmware");
            AetherSurfacePanel frames = CreateCard(page, 0, 72, 788, 130, "FRAME PRESENTATION");
            AddSelector(frames, new[] { "NONE", "SKIP 1", "SKIP 2", "SKIP 3", "SKIP 4" }, 18, 48, 140, frameskipButtons, bridge.SetFrameskip);
            frames.Controls.Add(CreateSmallLabel("Frameskip verändert nur die Bildausgabe, nicht die emulierte Hardwarezeit.", 18, 91, 748, 28));

            AetherSurfacePanel boot = CreateCard(page, 0, 218, 788, 142, "BOOT ROM POLICY");
            bootRomButton = AddActionButton(boot, "BOOT ROM · AUTO", 18, 48, 230, ToggleBootRom, AetherButtonKind.Secondary);
            bootRomStatus = CreateSmallLabel(string.Empty, 270, 42, 492, 72);
            boot.Controls.Add(bootRomStatus);

            AetherSurfacePanel reset = CreateCard(page, 0, 376, 788, 152, "SETTINGS RECOVERY");
            AddActionButton(reset, "RESET ALL SETTINGS", 18, 50, 240, ResetSettings, AetherButtonKind.Danger);
            reset.Controls.Add(CreateSmallLabel(
                "Setzt Video, Audio, Eingabe, Save-Slot und Boot-ROM-Verhalten auf sichere Standardwerte zurück. ROMs und Spielstände werden nicht gelöscht.",
                286,
                42,
                476,
                72));
            return page;
        }

        private Panel BuildDiagnosticsPage()
        {
            Panel page = CreatePage("controlCenterPageDiagnostics", "DIAGNOSTICS", "Reproduzierbare Laufzeit- und Cartridge-Daten");
            diagnostics = new RichTextBox
            {
                BackColor = AetherColors.SurfaceRaised,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Cascadia Mono", 9f, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = AetherColors.Text,
                Location = new Point(0, 72),
                Name = "controlCenterDiagnosticsText",
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Size = new Size(788, 450),
                WordWrap = false
            };
            page.Controls.Add(diagnostics);
            AddActionButton(page, "COPY DIAGNOSTICS", 0, 540, 220, CopyDiagnostics, AetherButtonKind.Primary);
            page.Controls.Add(CreateSmallLabel(
                "Enthält keine ROM-Daten. Dateipfad und Hash bleiben lokal und werden nur auf deine ausdrückliche Aktion kopiert.",
                242,
                538,
                546,
                44));
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
            audioDetails.Text = isGameBoyAdvance
                ? "65.536 kHz · GBA PSG + Direct Sound · Stereo Core Mix"
                : "44.1 kHz · Mono Core Mix · 100 ms Ziel-Latenz";
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
            RomSnapshot? rom = bridge.SnapshotProvider()?.Rom;
            string? romPath = bridge.RomPathProvider();
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

            string savePath = Path.ChangeExtension(romPath, "sav");
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
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            bool dmgAvailable = File.Exists("dmg_boot.bin") || File.Exists(Path.Combine(baseDirectory, "dmg_boot.bin"));
            bool cgbAvailable = File.Exists("gbc_boot.bin") || File.Exists(Path.Combine(baseDirectory, "gbc_boot.bin"));
            bool gbaAvailable = File.Exists("gba_bios.bin") || File.Exists(Path.Combine(baseDirectory, "gba_bios.bin"));
            string policy = bridge.Settings.BootRomEnable ? "AUTO-DETECT ENABLED" : "BYPASS ENABLED";
            bootRomStatus.Text =
                $"{policy}\r\nDMG {(dmgAvailable ? "FOUND" : "MISSING")}  ·  CGB {(cgbAvailable ? "FOUND" : "MISSING")}  ·  GBA {(gbaAvailable ? "FULL BIOS FOUND" : "BUILT-IN HLE READY")}\r\nÄnderung gilt beim nächsten Cartridge-Start.";
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
            text.AppendLine($"INPUT           {(gamepad.IsConnected ? gamepad.DeviceName : "Keyboard")}");
            text.AppendLine();
            if (rom == null)
            {
                text.AppendLine("CARTRIDGE       None");
            }
            else
            {
                text.AppendLine($"TITLE           {SafeTitle(rom)}");
                text.AppendLine($"MODEL           {GetModelName(rom)}");
                text.AppendLine($"MAPPER          {rom.CartridgeType}");
                text.AppendLine($"ROM SIZE        {rom.RomSize:N0} bytes");
                text.AppendLine($"RAM SIZE        {rom.RamSize:N0} bytes");
                text.AppendLine($"REGION          {(rom.IsJapanese ? "Japan" : "International")}");
                text.AppendLine($"SGB             {(rom.HasSuperGameBoyFeatures ? "Yes" : "No")}");
                text.AppendLine($"ROM SHA-256      {rom.RomSha256}");
                text.AppendLine($"ROM PATH        {bridge.RomPathProvider()}");
                text.AppendLine($"BATTERY SAVE    {GetSaveSummary(rom).Replace("\r\n", " · ")}");
            }
            if (snapshot?.DiagnosticEvents.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("GBA CORE EVENTS  LOCAL ONLY · NO ROM DATA · NO TELEMETRY");
                foreach (GbaDiagnosticEventSnapshot entry in snapshot.DiagnosticEvents.TakeLast(12))
                {
                    string address = entry.Address.HasValue ? $" @ {entry.Address.Value:X8}" : string.Empty;
                    text.AppendLine($"{entry.Category,-16} C{entry.Cycle:N0}{address} · {entry.Message}");
                }
            }
            string nextDiagnostics = text.ToString();
            if (!string.Equals(diagnostics.Text, nextDiagnostics, StringComparison.Ordinal))
            {
                diagnostics.Text = nextDiagnostics;
            }
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
                "Alle Emulator-, Audio-, Video- und Eingabeeinstellungen auf Standard zurücksetzen?\n\nROMs, Batterie-Saves und Save States bleiben unangetastet.",
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
