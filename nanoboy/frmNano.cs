using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Diagnostics;
using nanoboy.Input;
using nanoboy.Platform.Audio;
using nanoboy.Storage;

namespace nanoboy
{
    public partial class frmNano : Form
    {

        private EmulationSession session;
        private NanoboySettings settings;
        private frmAudioTool audiotoolwindow;
        private frmControlCenter controlCenter;
        private NAudioSoundOut audioOutput;
        private readonly InputAggregator input = new InputAggregator();
        private GameBoyAdvanceButtons keyboardAdvanceButtons;
        private GameBoyAdvanceButtons gamepadAdvanceButtons;
        private GameBoyAdvanceButtons postedAdvanceButtons;
        private bool turboPressed;
        private bool sessionFaultReported;
        private int[] displayFrame = new int[EmulationSnapshot.FramePixelCount];
        private long displayedFrameSequence;
        private HostGamepadState lastPadState;
        private string currentRomPath;
        private bool stateOperationInProgress;
        private Platform.Video.WindowsFrameTiming frameTiming;
        private bool batteryRecoveryNoticeShown;
        private bool testerRomIdentityRecorded;
        private bool currentSessionUsesExternalBootRom;
        private readonly WindowsTesterSession? testerSession;
        private readonly WindowsSessionHealthMonitor? healthMonitor;
        private static readonly TimeSpan SessionShutdownTimeout = TimeSpan.FromSeconds(2);

        public frmNano() : this(null)
        {
        }

        internal frmNano(WindowsTesterSession? testerSession)
        {
            this.testerSession = testerSession;
            InitializeComponent();
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            frameTiming = new Platform.Video.WindowsFrameTiming(components);
            updateTimer.Interval = 8; // Sample the latest frame without a second 60-Hz clock beating against the core.
            Branding.AppBrand.ApplyIcon(this);
            Text = ProductInfo.DisplayName;
            Deactivate += frmNano_Deactivate;
            settings = new NanoboySettings();
            settings.ProfileSaveFailed += exception =>
            {
                testerSession?.RecordException("profile.save_failed", exception);
                AetherSignal.Show(this, "Die Änderung konnte nicht im Spielprofil gespeichert werden.\n" +
                    "Die bisherigen Einstellungen bleiben erhalten.\n\n" + exception.Message,
                    "Spielprofil nicht gespeichert", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };
            Disposed += (_, _) => settings.Dispose();
            LoadConfiguration();
            RebuildRecentFilesMenu();
            SelectSaveSlot(settings.SaveSlot);
            SetPalette(settings.PaletteIndex);
            SetDisplayFilter(settings.DisplayFilterIndex);
            DarkTheme.Apply(this);
            InitializeAetherShell();
            InitializeWindowsExperience();
            InitializePlayerTools();
            if (testerSession != null)
            {
                healthMonitor = new WindowsSessionHealthMonitor(testerSession);
                Disposed += (_, _) => healthMonitor.Dispose();
            }
            testerSession?.RecordWindowReady(settings);
            updateTimer.Start();
        }

        private bool StopSession(bool ownsStateOperation = false)
        {
            if (stateOperationInProgress && !ownsStateOperation) return false;
            if (session != null && currentRomPath != null)
            {
                FlushGameActivity();
                SaveResumeBeforeStop(session, currentRomPath);
            }
            stateGallery?.Close();
            undoQuickLoad = null;
            frameTiming.SetActive(false);
            turboPressed = false;
            input.Clear();
            keyboardAdvanceButtons = GameBoyAdvanceButtons.None;
            gamepadAdvanceButtons = GameBoyAdvanceButtons.None;
            postedAdvanceButtons = GameBoyAdvanceButtons.None;

            EmulationSession previousSession = session;
            if (previousSession == null)
            {
                currentRomPath = null;
                DisposeAudioOutput(null);
                UpdateAetherSessionUi(null);
                return true;
            }

            if (audiotoolwindow != null && !audiotoolwindow.IsDisposed)
            {
                audiotoolwindow.Session = null;
            }

            DisposeAudioOutput(previousSession);
            Task shutdown = previousSession.ShutdownAsync();
            Task completed = Task.WhenAny(
                shutdown,
                Task.Delay(SessionShutdownTimeout)).GetAwaiter().GetResult();

            if (!ReferenceEquals(completed, shutdown))
            {
                Debug.WriteLine("The emulation session did not stop within two seconds.");
                return false;
            }

            try
            {
                shutdown.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                // A faulted session has still completed its owner-thread disposal.
                Debug.WriteLine($"The emulation session stopped after a fault: {exception}");
            }

            if (ReferenceEquals(session, previousSession))
            {
                session = null;
                currentRomPath = null;
                settings.UseGameProfile(null);
            }

            UpdateAetherSessionUi(null);
            testerSession?.RecordOperation("session_stop", slot: null, succeeded: true);

            return true;
        }

        private void DisposeAudioOutput(EmulationSession source)
        {
            if (source != null)
            {
                source.AudioSamplesAvailable -= Session_AudioSamplesAvailable;
            }

            Interlocked.Exchange(ref audioOutput, null)?.Dispose();
        }

        private void Session_AudioSamplesAvailable(
            object sender,
            AudioSamplesAvailableEventArgs eventArgs)
        {
            NAudioSoundOut output = Volatile.Read(ref audioOutput);
            if (output == null)
            {
                return;
            }

            try
            {
                output.Submit(eventArgs.GetInterleavedSamplesCopy(), eventArgs.SampleRate, eventArgs.Channels);
            }
            catch (ObjectDisposedException)
            {
                // The owner may have captured the handler while the UI was detaching it.
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Audio output rejected a sample block: {exception}");
            }
        }

        private byte[] LoadBootROM(bool isColor, bool isGameBoyAdvance = false)
        {
            if (!settings.BootRomEnable)
            {
                return null;
            }

            string bootFileName = isGameBoyAdvance
                ? "gba_bios.bin"
                : isColor ? "gbc_boot.bin" : "dmg_boot.bin";
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, bootFileName);
            string managedPath = Path.Combine(WindowsDataPaths.Default.Firmware, bootFileName);
            if (File.Exists(managedPath))
            {
                try { return File.ReadAllBytes(managedPath); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    testerSession?.RecordException("firmware.read_failed", exception);
                    Debug.WriteLine($"Could not read local firmware: {exception}");
                    return null;
                }
            }
            if (File.Exists(bootFileName))
            {
                try
                {
                    return File.ReadAllBytes(bootFileName);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine($"Could not load boot ROM '{bootFileName}': {exception}");
                }
            }
            if (File.Exists(localPath))
            {
                try
                {
                    return File.ReadAllBytes(localPath);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine($"Could not load boot ROM '{localPath}': {exception}");
                }
            }
            return null;
        }

        #region "Menu"
        private void menuOpen_Click(object sender, EventArgs e)
        {
            using var library = new frmRomLibrary(settings.RecentFiles);
            if (library.ShowDialog(this) == DialogResult.OK &&
                library.SelectedRomPath is string path)
            {
                LoadRomFile(path, library.ResumeRequested);
            }
        }

        internal void LoadRomFile(string path, bool resume = false)
        {
            if (stateOperationInProgress) { SetSaveFeedback("Bitte die laufende Speicheraktion abwarten", true); return; }
            if (!File.Exists(path))
            {
                testerSession?.RecordRomLoadRejected(path, "file_missing");
                return;
            }

            testerSession?.RecordRomLoadRequested(path);

            try
            {
                string originalPath = Path.GetFullPath(path);
                path = WindowsRomLibrary.Default.Import(originalPath);
                settings.RecentFiles.RemoveAll(candidate => string.Equals(candidate, originalPath,
                    StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                testerSession?.RecordException("rom.import_failed", exception);
                AetherSignal.Show(this, $"Die ROM konnte nicht in die lokale Bibliothek übernommen werden.\n\n{exception.Message}",
                    "ROM-Import fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            updateTimer.Stop();
            if (!StopSession())
            {
                updateTimer.Start();
                AetherSignal.Show(this,
                    "Der laufende Emulator konnte nicht sicher beendet werden. Die neue ROM wurde nicht geladen.",
                    "Emulator beschäftigt",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                testerSession?.RecordOperation(
                    "rom_load",
                    slot: null,
                    succeeded: false,
                    reason: "session_shutdown_timeout");
                return;
            }

            AddRecentFile(path);
            try { settings.UseGameProfile(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                settings.UseGameProfile(null);
                AetherSignal.Show(this, "Spielprofil nicht lesbar; globale Einstellungen werden verwendet.\n\n" + ex.Message,
                    "Spielprofil", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            pendingResume = resume;
            gamepadAwaitNeutral = true;
            pendingPlaySeconds = 0;
            activityTimestamp = 0;
            activityWasRunning = false;
            lastLibraryFlush = 0;
            lastResumeSave = Environment.TickCount64;
            input.Clear();
            turboPressed = false;
            sessionFaultReported = false;
            batteryRecoveryNoticeShown = false;
            testerRomIdentityRecorded = false;

            bool isGameBoyAdvance = Path.GetExtension(path).Equals(
                ".gba",
                StringComparison.OrdinalIgnoreCase);
            byte[] bootRom = isGameBoyAdvance
                ? LoadBootROM(isColor: false, isGameBoyAdvance: true)
                : LoadBootROM(RomHasColorFeatures(path));
            EmulatorConfiguration configuration = CreateEmulatorConfiguration();
            NAudioSoundOut preparedAudioOutput = null;
            if (configuration.AudioEnabled && !TryCreateAudioOutput(out preparedAudioOutput))
            {
                settings.AudioEnable = false;
                menuAudioOn.Checked = false;
                configuration = CreateEmulatorConfiguration();
                ShowAudioUnavailableMessage();
            }

            try
            {
                session = new EmulationSession(
                    path,
                    WindowsRomLibrary.Default.GetSavePath(path),
                    bootRom,
                    configuration,
                    settings.PaletteIndex);
                currentRomPath = Path.GetFullPath(path);
                currentSessionUsesExternalBootRom = bootRom is not null;
                if (session.LatestSnapshot.Rom is RomSnapshot initialRom)
                {
                    testerSession?.RecordRomStarted(
                        initialRom,
                        currentSessionUsesExternalBootRom);
                    testerRomIdentityRecorded = true;
                }
            }
            catch (Exception exception)
            {
                testerSession?.RecordException("rom.start_failed", exception);
                preparedAudioOutput?.Dispose();
                settings.UseGameProfile(null);
                UpdateAetherSessionUi(null);
                Debug.WriteLine($"Could not start emulation session for '{path}': {exception}");
                AetherSignal.Show(this,
                    $"Die ROM konnte nicht gestartet werden.\n\n{exception.Message}",
                    "ROM konnte nicht geladen werden",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                updateTimer.Start();
                return;
            }

            if (preparedAudioOutput != null)
            {
                Volatile.Write(ref audioOutput, preparedAudioOutput);
                session.AudioSamplesAvailable += Session_AudioSamplesAvailable;
            }

            displayedFrameSequence = 0;
            lastPadState = GamepadInput.GetState();
            UpdateAetherGamepadUi(lastPadState);
            gameView.ClearFrame();
            SetSaveFeedback("F5 speichern · F8 laden · F6 State-Galerie · F11 Vollbild", false);

            if (audiotoolwindow != null && !audiotoolwindow.IsDisposed)
            {
                audiotoolwindow.Session = session;
            }

            UpdateAetherSessionUi(session.LatestSnapshot);
            ApplyGameProfilePreferences();
            updateTimer.Start();
            gameView.Focus();
        }

        private static bool RomHasColorFeatures(string path)
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                if (stream.Length <= 0x143)
                {
                    return false;
                }

                stream.Position = 0x143;
                int colorFlag = stream.ReadByte();
                return colorFlag == 0x80 || colorFlag == 0xC0;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Could not inspect ROM header '{path}': {exception}");
                return false;
            }
        }

        private EmulatorConfiguration CreateEmulatorConfiguration()
        {
            return new EmulatorConfiguration(
                Frameskip: settings.Frameskip,
                AudioEnabled: settings.AudioEnable,
                Channel1Enabled: settings.Channel1Enable,
                Channel2Enabled: settings.Channel2Enable,
                Channel3Enabled: settings.Channel3Enable,
                Channel4Enabled: settings.Channel4Enable,
                SampleRate: 44_100);
        }

        private bool TryCreateAudioOutput(out NAudioSoundOut output)
        {
            try
            {
                output = new NAudioSoundOut(44_100, settings.AudioVolume / 100f, settings.AudioLatencyMs);
                return true;
            }
            catch (Exception exception) when (
                exception is InvalidOperationException ||
                exception is PlatformNotSupportedException ||
                exception.GetType().Namespace == "NAudio")
            {
                Debug.WriteLine($"Windows audio output is unavailable: {exception}");
                output = null;
                return false;
            }
        }

        private void ShowAudioUnavailableMessage()
        {
            AetherSignal.Show(this,
                "Das Windows-Audiogerät konnte nicht geöffnet werden. AetherBoy läuft stumm weiter.",
                "Audio nicht verfügbar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private void AddRecentFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            settings.RecentFiles.RemoveAll(candidate =>
                candidate.Equals(path, StringComparison.OrdinalIgnoreCase));
            settings.RecentFiles.Insert(0, path);
            while (settings.RecentFiles.Count > 8)
            {
                settings.RecentFiles.RemoveAt(settings.RecentFiles.Count - 1);
            }
            RecentRomStore.Save(settings.RecentFiles);
            RebuildRecentFilesMenu();
        }

        private void RebuildRecentFilesMenu()
        {
            menuRecentFiles.DropDownItems.Clear();
            if (settings.RecentFiles.Count == 0)
            {
                var dummy = new ToolStripMenuItem("Keine") { Enabled = false };
                menuRecentFiles.DropDownItems.Add(dummy);
                return;
            }

            foreach (var file in settings.RecentFiles)
            {
                string filePath = file;
                bool exists = File.Exists(filePath);
                var item = new ToolStripMenuItem(
                    exists ? Path.GetFileName(filePath) : $"{Path.GetFileName(filePath)} (fehlt)")
                {
                    Enabled = exists,
                    ToolTipText = filePath
                };
                item.Click += (s, e) => LoadRomFile(filePath);
                menuRecentFiles.DropDownItems.Add(item);
            }
        }

        private void menuSaveStateQuickSave_Click(object sender, EventArgs e) => QuickSave();
        private void menuSaveStateQuickLoad_Click(object sender, EventArgs e) => QuickLoad();
        private void menuBatterySaveSafety_Click(object sender, EventArgs e) => OpenBatterySaveSafety(this);
        private void menuSaveSlot1_Click(object sender, EventArgs e) => SelectSaveSlot(1);
        private void menuSaveSlot2_Click(object sender, EventArgs e) => SelectSaveSlot(2);
        private void menuSaveSlot3_Click(object sender, EventArgs e) => SelectSaveSlot(3);
        private void menuSaveSlot4_Click(object sender, EventArgs e) => SelectSaveSlot(4);
        private void menuSaveSlot5_Click(object sender, EventArgs e) => SelectSaveSlot(5);

        private void menuControlCenter_Click(object sender, EventArgs e) => OpenControlCenter();

        private void OpenControlCenter()
        {
            if (controlCenter != null && !controlCenter.IsDisposed)
            {
                if (controlCenter.WindowState == FormWindowState.Minimized)
                {
                    controlCenter.WindowState = FormWindowState.Normal;
                }
                controlCenter.Activate();
                controlCenter.BringToFront();
                return;
            }

            controlCenter = new frmControlCenter(new ControlCenterBridge
            {
                Settings = settings,
                SnapshotProvider = () => session?.LatestSnapshot,
                GamepadProvider = () => GamepadInput.GetState(),
                RomPathProvider = () => currentRomPath,
                SetPalette = SetPalette,
                SetDisplayFilter = SetDisplayFilter,
                SetWindowScale = ResizeWindow,
                ToggleFullscreen = ToggleAetherFullscreen,
                ApplyAudioSettings = ApplyAudioSettingsFromControlCenter,
                ApplyVideoSettings = ApplyWindowsVideoSettings,
                AudioOutputProvider = DescribeWindowsAudio,
                VideoOutputProvider = DescribeWindowsVideo,
                SaveFeedbackProvider = () => saveFeedback,
                OpenStateGallery = OpenStateGallery,
                ToggleGameProfile = () => ChangeGameProfile(false),
                ResetGameProfile = () => ChangeGameProfile(true),
                OpenQuickMenu = () => _ = OpenQuickMenuAsync(),
                CaptureScreenshot = () => _ = CaptureScreenshotAsync(),
                TogglePerformanceOverlay = TogglePerformanceOverlay,
                MarkProblem = MarkSessionProblem,
                HealthStatusProvider = () => healthMonitor?.Status ?? "Development-Diagnose ist nicht aktiv.",
                SetFrameskip = SetFrameskip,
                SetSaveSlot = SelectSaveSlot,
                OpenControls = () => new frmControls(settings).ShowDialog(controlCenter),
                OpenSaveSafety = () => OpenBatterySaveSafety(controlCenter),
                OpenAudioInspector = () => menuAudioInspector_Click(controlCenter, EventArgs.Empty),
                QuickSave = QuickSave,
                QuickLoad = QuickLoad,
                ResetSettings = ResetSettingsToDefaults,
                TesterModeProvider = () => testerSession is not null,
                TesterLogPathProvider = () => testerSession?.LogFilePath,
                ExportTesterReport = ExportTesterReport,
                OpenTesterFolder = OpenTesterFolder
            });
            controlCenter.FormClosed += (_, _) => controlCenter = null;
            controlCenter.Show(this);
        }

        private void OpenBatterySaveSafety(IWin32Window owner)
        {
            EmulationSession currentSession = session;
            string romPath = currentRomPath;
            RomSnapshot? rom = currentSession?.LatestSnapshot.Rom;
            if (currentSession == null || string.IsNullOrEmpty(romPath) || rom == null)
            {
                AetherSignal.Show(
                    owner,
                    "Starte zuerst ein Spiel. Danach zeigt das Save Safety Center den Batterie-Spielstand und seine Backups.",
                    "Kein Spiel aktiv",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            if (!rom.BatterySave.IsEnabled || rom.BatterySave.ExpectedLength <= 0)
            {
                AetherSignal.Show(
                    owner,
                    "Dieses Spiel besitzt keinen unterstützten Batterie-RAM-Spielstand. Save States bleiben davon unabhängig verfügbar.",
                    "Kein Batterie-Spielstand",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            string savePath = WindowsRomLibrary.Default.GetSavePath(romPath);
            byte[] selectedSaveData;
            int generation;
            try
            {
                using var manager = new frmBatterySaveManager(
                    savePath,
                    rom.BatterySave.ExpectedLength,
                    string.IsNullOrWhiteSpace(rom.Title) ? Path.GetFileNameWithoutExtension(romPath) : rom.Title);
                if (manager.ShowDialog(owner) != DialogResult.OK || manager.SelectedSaveData == null)
                {
                    return;
                }

                selectedSaveData = manager.SelectedSaveData;
                generation = (int)manager.SelectedGeneration;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidDataException)
            {
                AetherSignal.Show(
                    owner,
                    $"Die Sicherungen konnten nicht gelesen werden.\n\n{exception.Message}",
                    "Save Safety nicht verfügbar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            DialogResult confirmation = AetherSignal.Show(
                owner,
                $"Backup {generation} wirklich aktivieren?\n\nDer derzeitige Spielstand wird vorher sicher beendet und als neuestes Backup erhalten. Anschließend startet das Spiel neu.",
                "Backup wiederherstellen",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirmation != DialogResult.Yes)
            {
                return;
            }

            if (stateOperationInProgress) { SetSaveFeedback("Bitte die laufende Speicheraktion abwarten", true); return; }
            stateOperationInProgress = true;
            updateTimer.Stop();
            try
            {
                if (!StopSession(ownsStateOperation: true))
                {
                    AetherSignal.Show(
                        owner,
                        "Der laufende Emulator konnte nicht sicher beendet werden. Das Backup wurde nicht verändert.",
                        "Wiederherstellung abgebrochen",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    updateTimer.Start();
                    return;
                }

                BatterySaveStore.Restore(
                    savePath,
                    rom.BatterySave.ExpectedLength,
                    selectedSaveData);
                stateOperationInProgress = false; // Disk restore finished; the regular ROM-start guard applies again.
                LoadRomFile(romPath);
                if (session != null)
                {
                    AetherSignal.Show(
                        owner,
                        $"Backup {generation} ist jetzt aktiv. Der vorherige Stand liegt weiterhin als rotierende Sicherung vor.",
                        "Spielstand wiederhergestellt",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidDataException)
            {
                Debug.WriteLine($"Could not restore battery save: {exception}");
                if (session == null && File.Exists(romPath))
                {
                    stateOperationInProgress = false;
                    LoadRomFile(romPath);
                }

                AetherSignal.Show(
                    owner,
                    $"Das Backup konnte nicht aktiviert werden.\n\n{exception.Message}",
                    "Wiederherstellung fehlgeschlagen",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                stateOperationInProgress = false;
                if (!updateTimer.Enabled)
                {
                    updateTimer.Start();
                }
            }
        }

        private void SelectSaveSlot(int slot)
        {
            settings.SaveSlot = slot;
            menuSaveSlot1.Checked = slot == 1;
            menuSaveSlot2.Checked = slot == 2;
            menuSaveSlot3.Checked = slot == 3;
            menuSaveSlot4.Checked = slot == 4;
            menuSaveSlot5.Checked = slot == 5;
            menuSaveSlot1.Text = slot == 1 ? "Slot 1 (Aktiv)" : "Slot 1";
            menuSaveSlot2.Text = slot == 2 ? "Slot 2 (Aktiv)" : "Slot 2";
            menuSaveSlot3.Text = slot == 3 ? "Slot 3 (Aktiv)" : "Slot 3";
            menuSaveSlot4.Text = slot == 4 ? "Slot 4 (Aktiv)" : "Slot 4";
            menuSaveSlot5.Text = slot == 5 ? "Slot 5 (Aktiv)" : "Slot 5";
            UpdateAetherSlotButtons();
        }

        private async void QuickSave() => await SaveCheckpointAsync(settings.SaveSlot);
        private async void QuickLoad() => await LoadCheckpointAsync(settings.SaveSlot);

        private async void menuRewind_Click(object sender, EventArgs e)
        {
            EmulationSession currentSession = session;
            if (currentSession == null || stateOperationInProgress)
            {
                return;
            }

            stateOperationInProgress = true;
            try
            {
                if (!await currentSession.RewindAsync().ConfigureAwait(true))
                {
                    testerSession?.RecordOperation(
                        "rewind",
                        slot: null,
                        succeeded: false,
                        reason: "history_empty");
                    AetherSignal.Show(this,
                        "Es ist noch kein früherer Zustand im Rewind-Puffer vorhanden.",
                        "Rewind",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    Volatile.Read(ref audioOutput)?.ClearBuffer();
                    displayedFrameSequence = 0;
                    testerSession?.RecordOperation(
                        "rewind",
                        slot: null,
                        succeeded: true);
                }
            }
            catch (Exception exception)
            {
                testerSession?.RecordException("rewind.failed", exception);
                testerSession?.RecordOperation(
                    "rewind",
                    slot: null,
                    succeeded: false);
                Debug.WriteLine($"Could not rewind: {exception}");
                AetherSignal.Show(this,
                    $"Zurückspulen ist fehlgeschlagen.\n\n{exception.Message}",
                    "Rewind fehlgeschlagen",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                stateOperationInProgress = false;
            }
        }

        private void menuPalettePocket_Click(object sender, EventArgs e) => SetPalette(0);
        private void menuPalettePeaGreen_Click(object sender, EventArgs e) => SetPalette(1);
        private void menuPaletteGBLight_Click(object sender, EventArgs e) => SetPalette(2);
        private void menuPaletteSepia_Click(object sender, EventArgs e) => SetPalette(3);
        private void menuPaletteCyberpunk_Click(object sender, EventArgs e) => SetPalette(4);

        private void SetPalette(int index)
        {
            settings.PaletteIndex = index;
            EmulationSession currentSession = session;
            if (currentSession != null)
            {
                ObserveSessionCommand(currentSession.SetPaletteAsync(index));
            }
            menuPalettePocket.Checked = index == 0;
            menuPalettePeaGreen.Checked = index == 1;
            menuPaletteGBLight.Checked = index == 2;
            menuPaletteSepia.Checked = index == 3;
            menuPaletteCyberpunk.Checked = index == 4;
        }

        private void menuFilterSharp_Click(object sender, EventArgs e) => SetDisplayFilter(0);
        private void menuFilterSmooth_Click(object sender, EventArgs e) => SetDisplayFilter(1);
        private void menuFilterLCDGrid_Click(object sender, EventArgs e) => SetDisplayFilter(2);

        private void SetDisplayFilter(int index)
        {
            if (index < 0 || index > 2)
            {
                index = 0;
            }

            settings.DisplayFilterIndex = index;
            menuFilterSharp.Checked = index == 0;
            menuFilterSmooth.Checked = index == 1;
            menuFilterLCDGrid.Checked = index == 2;
            gameView.Filter = index switch
            {
                1 => GameDisplayFilter.Smooth,
                2 => GameDisplayFilter.LcdGrid,
                _ => GameDisplayFilter.Sharp
            };
            UpdateAetherSessionUi(session?.LatestSnapshot);
        }

        private void menuCheats_Click(object sender, EventArgs e)
        {
            EmulationSession currentSession = session;
            if (currentSession == null)
            {
                AetherSignal.Show(this,
                    "Bitte zuerst eine ROM laden.",
                    "Cheat Manager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            new frmCheats(currentSession).ShowDialog(this);
        }

        private void menuLinkCable_Click(object sender, EventArgs e)
        {
            ShowUnavailableFeature("Link-Kabel Multiplayer");
        }

        private void ShowUnavailableFeature(string feature)
        {
            AetherSignal.Show(this,
                $"{feature} ist in dieser Version experimentell und vorerst deaktiviert.",
                "Funktion deaktiviert",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        private void menuChangelog_Click(object sender, EventArgs e) => new frmChangelog().ShowDialog();

        private void menuRomInfo_Click(object sender, EventArgs e)
        {
            RomSnapshot rom = session?.LatestSnapshot.Rom;
            if (rom != null)
            {
                string model = rom.IsGameBoyAdvance
                    ? "Game Boy Advance"
                    : rom.HasColorFeatures ? "Game Boy Color" : "Game Boy";
                string info = $"Titel: {rom.Title}\n" +
                              $"Typ: {rom.CartridgeType}\n" +
                              $"System: {model}\n" +
                              $"ROM Größe: {rom.RomSize / 1024} KB\n" +
                              $"RAM Größe: {rom.RamSize / 1024} KB\n" +
                              $"Super Game Boy (SGB): {(rom.IsGameBoyAdvance ? "Nicht zutreffend" : rom.HasSuperGameBoyFeatures ? "Ja" : "Nein")}\n" +
                              $"Region: {(rom.IsJapanese ? "Japan" : "International")}";
                AetherSignal.Show(this, info, "ROM Informationen", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                AetherSignal.Show(this, "Keine ROM geladen.", "ROM Informationen", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void menuClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void menuAbout_Click(object sender, EventArgs e)
        {
            new frmAbout().ShowDialog();
        }


        private void menuSize1_Click(object sender, EventArgs e)
        {
            ResizeWindow(1);
            LoadConfiguration();
        }

        private void menuSize2_Click(object sender, EventArgs e)
        {
            ResizeWindow(2);
            LoadConfiguration();
        }

        private void menuSize3_Click(object sender, EventArgs e)
        {
            ResizeWindow(3);
            LoadConfiguration();
        }

        private void menuSize4_Click(object sender, EventArgs e)
        {
            ResizeWindow(4);
            LoadConfiguration();
        }

        private void menuSizeFull_Click(object sender, EventArgs e)
        {
            ToggleAetherFullscreen();
        }

        private void menuAudioC1_Click(object sender, EventArgs e)
        {
            menuAudioC1.Checked = !menuAudioC1.Checked;
            settings.Channel1Enable = menuAudioC1.Checked;
            UpdateEmulatorSettings();
        }

        private void menuAudioC2_Click(object sender, EventArgs e)
        {
            menuAudioC2.Checked = !menuAudioC2.Checked;
            settings.Channel2Enable = menuAudioC2.Checked;
            UpdateEmulatorSettings();
        }

        private void menuAudioC3_Click(object sender, EventArgs e)
        {
            menuAudioC3.Checked = !menuAudioC3.Checked;
            settings.Channel3Enable = menuAudioC3.Checked;
            UpdateEmulatorSettings();
        }

        private void menuAudioC4_Click(object sender, EventArgs e)
        {
            menuAudioC4.Checked = !menuAudioC4.Checked;
            settings.Channel4Enable = menuAudioC4.Checked;
            UpdateEmulatorSettings();
        }

        private void menuAudioOn_Click(object sender, EventArgs e)
        {
            menuAudioOn.Checked = !menuAudioOn.Checked;
            settings.AudioEnable = menuAudioOn.Checked;
            UpdateEmulatorSettings();
        }

        private void ApplyAudioSettingsFromControlCenter()
        {
            menuAudioOn.Checked = settings.AudioEnable;
            menuAudioC1.Checked = settings.Channel1Enable;
            menuAudioC2.Checked = settings.Channel2Enable;
            menuAudioC3.Checked = settings.Channel3Enable;
            menuAudioC4.Checked = settings.Channel4Enable;
            UpdateEmulatorSettings();
        }

        private void SetFrameskip(int frameskip)
        {
            settings.Frameskip = Math.Clamp(frameskip, 0, 4);
            menuFrameSkip0.Checked = settings.Frameskip == 0;
            menuFrameSkip1.Checked = settings.Frameskip == 1;
            menuFrameSkip2.Checked = settings.Frameskip == 2;
            menuFrameSkip3.Checked = settings.Frameskip == 3;
            menuFrameSkip4.Checked = settings.Frameskip == 4;
            UpdateEmulatorSettings();
        }

        private void ResetSettingsToDefaults()
        {
            if (settings.HasGameProfile) settings.ResetGameProfile();
            nanoboy.Properties.Settings.Default.Reset();
            SetPalette(settings.PaletteIndex);
            SetDisplayFilter(settings.DisplayFilterIndex);
            SelectSaveSlot(settings.SaveSlot);
            LoadConfiguration();
            ApplyAudioSettingsFromControlCenter();
        }

        private void menuFrameSkip0_Click(object sender, EventArgs e) => SetFrameskip(0);

        private void menuFrameSkip1_Click(object sender, EventArgs e) => SetFrameskip(1);

        private void menuFrameSkip2_Click(object sender, EventArgs e) => SetFrameskip(2);

        private void menuFrameSkip3_Click(object sender, EventArgs e) => SetFrameskip(3);

        private void menuFrameSkip4_Click(object sender, EventArgs e) => SetFrameskip(4);

        private void menuControls_Click(object sender, EventArgs e)
        {
            frmControls controls = new frmControls(settings);
            controls.ShowDialog();
        }

        private void menuAudioQ1_Click(object sender, EventArgs e)
        {
            settings.SampleRate = 0;
            LoadConfiguration();
        }

        private void menuAudioQ2_Click(object sender, EventArgs e)
        {
            settings.SampleRate = 1;
            LoadConfiguration();
        }

        private void menuAudioQ3_Click(object sender, EventArgs e)
        {
            settings.SampleRate = 2;
            LoadConfiguration();
        }

        private void menuAudioQ4_Click(object sender, EventArgs e)
        {
            settings.SampleRate = 3;
            LoadConfiguration();
        }

        private void menuAudioInspector_Click(object sender, EventArgs e)
        {
            if (audiotoolwindow != null && !audiotoolwindow.IsDisposed)
            {
                audiotoolwindow.Session = session;
                audiotoolwindow.BringToFront();
                return;
            }

            audiotoolwindow = new frmAudioTool();
            audiotoolwindow.Session = session;
            audiotoolwindow.FormClosed += (closedSender, closedArgs) => audiotoolwindow = null;
            audiotoolwindow.Show(this);
        }
        #endregion

        #region "Update"
        private void updateTimer_Tick(object sender, EventArgs e)
        {
            healthMonitor?.Pulse(session, stateOperationInProgress || quickMenuOpen ||
                WindowState == FormWindowState.Minimized || Form.ActiveForm != this, gameView.PresentedFrames);
            PollGamepad();

            EmulationSession currentSession = session;
            if (currentSession == null)
            {
                frameTiming.SetActive(false);
                UpdatePerformanceOverlay(null);
                return;
            }

            if (currentSession.State == SessionState.Faulted && !sessionFaultReported)
            {
                frameTiming.SetActive(false);
                sessionFaultReported = true;
                DisposeAudioOutput(currentSession);
                if (audiotoolwindow != null && !audiotoolwindow.IsDisposed)
                {
                    audiotoolwindow.Session = null;
                }

                Exception fault = currentSession.Fault;
                testerSession?.RecordException("session.faulted", fault);
                Program.WriteCrashLog(fault);
                AetherSignal.Show(this,
                    $"Die Emulation wurde wegen eines Fehlers beendet.\n\n{fault?.Message}",
                    "Emulationsfehler",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            EmulationSnapshot snapshot = currentSession.LatestSnapshot;
            TrackGameActivity(snapshot);
            UpdatePerformanceOverlay(snapshot);
            bool running = snapshot.State == SessionState.Running;
            frameTiming.SetActive(running && WindowState != FormWindowState.Minimized && !snapshot.IsTurboEnabled);
            Volatile.Read(ref audioOutput)?.SetSuspended(!running || snapshot.IsTurboEnabled || stateOperationInProgress);
            if (snapshot.State == SessionState.Starting)
                return;

            if (!testerRomIdentityRecorded && snapshot.Rom is RomSnapshot startedRom)
            {
                testerSession?.RecordRomStarted(
                    startedRom,
                    currentSessionUsesExternalBootRom);
                testerRomIdentityRecorded = true;
            }
            testerSession?.RecordHeartbeat(snapshot, settings, Volatile.Read(ref audioOutput)?.Snapshot,
                gameView.RendererStatus, gameView.PresentedFrames, gameView.SupersededFrames);

            if (gameView.VideoGeometry != snapshot.VideoGeometry)
            {
                gameView.SetVideoGeometry(snapshot.VideoGeometry);
                displayFrame = new int[snapshot.VideoGeometry.PixelCount];
                displayedFrameSequence = 0;
            }

            if (currentSession.TryCopyLatestFrame(displayFrame, ref displayedFrameSequence))
            {
                if (WindowState != FormWindowState.Minimized) gameView.Present(displayFrame);
            }

            UpdateAetherSessionUi(snapshot);

            if (!batteryRecoveryNoticeShown && snapshot.Rom?.BatterySave.RecoveredFromBackup == true)
            {
                batteryRecoveryNoticeShown = true;
                int generation = snapshot.Rom.BatterySave.LoadedGeneration;
                AetherSignal.Show(
                    this,
                    $"Der aktuelle Batterie-Spielstand war nicht verwendbar. AetherBoy hat automatisch Backup {generation} geladen und repariert die Hauptdatei bei der nächsten Sicherung.",
                    "Spielstand automatisch gerettet",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void PollGamepad() => ProcessGamepadState(GamepadInput.GetState(), Form.ActiveForm == this && ContainsFocus && Enabled);

        private void MarkSessionProblem()
        {
            bool recorded = healthMonitor?.MarkProblem() == true;
            SetSaveFeedback(recorded ? "Problemzeitpunkt lokal im Testbericht markiert" :
                "Diagnose nicht aktiv oder Zeitpunkt gerade erst markiert", !recorded);
        }

        internal void ProcessGamepadState(HostGamepadState padState, bool ownsInputFocus)
        {
            testerSession?.RecordGamepadIfChanged(padState);
            UpdateAetherGamepadUi(padState);

            if (!ownsInputFocus || stateOperationInProgress || quickMenuOpen)
            {
                ReleaseGamepadInput(); gamepadAwaitNeutral = true; lastPadState = padState; return;
            }

            if (session == null)
            {
                input.ClearGamepad();
                lastPadState = padState;
                return;
            }

            if (!padState.IsConnected)
            {
                gamepadAwaitNeutral = true;
                if (lastPadState.IsConnected)
                {
                    ReleaseGamepadInput();
                }

                lastPadState = padState;
                return;
            }

            if (gamepadAwaitNeutral)
            {
                gamepadAwaitNeutral = !GamepadNavigationInput.Neutral(padState);
                lastPadState = padState; return;
            }
            const HostGamepadButtons menuChord = HostGamepadButtons.LeftStick | HostGamepadButtons.RightStick;
            if (padState.IsButtonDown(menuChord) && !lastPadState.IsButtonDown(menuChord))
            { lastPadState = padState; _ = OpenQuickMenuAsync(); return; }

            GamepadBindings bindings = settings.GamepadBindings;
            GameBoyButtons gamepadButtons = GamepadMapper.ToGameBoyButtons(padState, bindings);
            if (input.SetGamepadButtons(gamepadButtons))
            {
                PostInputState();
            }

            bool isGameBoyAdvance = session.LatestSnapshot.Rom?.IsGameBoyAdvance == true;
            if (isGameBoyAdvance)
            {
                GameBoyAdvanceButtons advanceButtons = GameBoyAdvanceButtons.None;
                if (padState.IsAnyButtonDown(settings.GamepadL) ||
                    padState.LeftTrigger > GamepadMapper.DefaultStickThreshold)
                {
                    advanceButtons |= GameBoyAdvanceButtons.L;
                }
                if (padState.IsAnyButtonDown(settings.GamepadR) ||
                    padState.RightTrigger > GamepadMapper.DefaultStickThreshold)
                {
                    advanceButtons |= GameBoyAdvanceButtons.R;
                }
                if (gamepadAdvanceButtons != advanceButtons)
                {
                    gamepadAdvanceButtons = advanceButtons;
                    PostAdvanceInputState();
                }

                HostGamepadButtons shoulderBindings = settings.GamepadL | settings.GamepadR;
                if ((bindings.QuickSave & shoulderBindings) == 0 &&
                    GamepadMapper.WasPressed(padState, lastPadState, bindings.QuickSave))
                {
                    QuickSave();
                }
                if ((bindings.QuickLoad & shoulderBindings) == 0 &&
                    GamepadMapper.WasPressed(padState, lastPadState, bindings.QuickLoad))
                {
                    QuickLoad();
                }
            }
            else
            {
                if (gamepadAdvanceButtons != GameBoyAdvanceButtons.None)
                {
                    gamepadAdvanceButtons = GameBoyAdvanceButtons.None;
                    PostAdvanceInputState();
                }

                if (GamepadMapper.WasPressed(
                    padState,
                    lastPadState,
                    bindings.QuickSave))
                {
                    QuickSave();
                }

                if (GamepadMapper.WasPressed(
                    padState,
                    lastPadState,
                    bindings.QuickLoad))
                {
                    QuickLoad();
                }
            }

            lastPadState = padState;
        }

        private void ReleaseGamepadInput()
        {
            if (input.ClearGamepad())
            {
                PostInputState();
            }
            if (gamepadAdvanceButtons != GameBoyAdvanceButtons.None)
            {
                gamepadAdvanceButtons = GameBoyAdvanceButtons.None;
                PostAdvanceInputState();
            }
        }

        #endregion

        #region Joypad
        private void gameView_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                QuickSave();
                return;
            }
            if (e.KeyCode == Keys.F8)
            {
                QuickLoad();
                return;
            }
            if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D5)
            {
                SelectSaveSlot(e.KeyCode - Keys.D1 + 1);
                return;
            }
            EmulationSession currentSession = session;
            if (currentSession == null)
            {
                return;
            }

            if (e.KeyCode == Keys.Space && !turboPressed)
            {
                turboPressed = true;
                ObserveSessionCommand(currentSession.SetTurboAsync(isEnabled: true));
            }

            GameBoyButtons buttons = MapKey(e.KeyCode);
            if (buttons != GameBoyButtons.None && input.SetKeyboardButton(buttons, pressed: true))
            {
                PostInputState();
            }
            SetAdvanceKeyboardButton(e.KeyCode, pressed: true);
        }

        private void gameView_KeyUp(object sender, KeyEventArgs e)
        {
            EmulationSession currentSession = session;
            if (currentSession == null)
            {
                return;
            }

            if (e.KeyCode == Keys.Space && turboPressed)
            {
                turboPressed = false;
                ObserveSessionCommand(currentSession.SetTurboAsync(isEnabled: false));
            }

            GameBoyButtons buttons = MapKey(e.KeyCode);
            if (buttons != GameBoyButtons.None && input.SetKeyboardButton(buttons, pressed: false))
            {
                PostInputState();
            }
            SetAdvanceKeyboardButton(e.KeyCode, pressed: false);
        }

        private GameBoyButtons MapKey(Keys key)
        {
            GameBoyButtons buttons = GameBoyButtons.None;
            if (key == settings.KeyA) buttons |= GameBoyButtons.A;
            if (key == settings.KeyB) buttons |= GameBoyButtons.B;
            if (key == settings.KeyStart) buttons |= GameBoyButtons.Start;
            if (key == settings.KeySelect) buttons |= GameBoyButtons.Select;
            if (key == settings.KeyUp) buttons |= GameBoyButtons.Up;
            if (key == settings.KeyDown) buttons |= GameBoyButtons.Down;
            if (key == settings.KeyLeft) buttons |= GameBoyButtons.Left;
            if (key == settings.KeyRight) buttons |= GameBoyButtons.Right;
            return buttons;
        }

        private void PostInputState()
        {
            EmulationSession currentSession = session;
            if (currentSession != null)
            {
                ObserveSessionCommand(currentSession.SetButtonsAsync(input.Combined));
            }
        }

        private void SetAdvanceKeyboardButton(Keys key, bool pressed)
        {
            if (session?.LatestSnapshot.Rom?.IsGameBoyAdvance != true)
            {
                return;
            }

            GameBoyAdvanceButtons button = GameBoyAdvanceButtons.None;
            if (key == settings.KeyL) button |= GameBoyAdvanceButtons.L;
            if (key == settings.KeyR) button |= GameBoyAdvanceButtons.R;
            if (button == GameBoyAdvanceButtons.None)
            {
                return;
            }

            GameBoyAdvanceButtons nextButtons = pressed
                ? keyboardAdvanceButtons | button
                : keyboardAdvanceButtons & ~button;
            if (nextButtons != keyboardAdvanceButtons)
            {
                keyboardAdvanceButtons = nextButtons;
                PostAdvanceInputState();
            }
        }

        private void PostAdvanceInputState()
        {
            EmulationSession currentSession = session;
            if (currentSession == null)
            {
                return;
            }

            GameBoyAdvanceButtons combined = keyboardAdvanceButtons | gamepadAdvanceButtons;
            if (combined == postedAdvanceButtons)
            {
                return;
            }

            postedAdvanceButtons = combined;
            ObserveSessionCommand(currentSession.SetGameBoyAdvanceButtonsAsync(combined));
        }
        #endregion

        private void ExportTesterReport()
        {
            if (testerSession is null)
            {
                AetherSignal.Show(
                    this,
                    "Development-Builds zeichnen die Entwicklungsdiagnose automatisch lokal auf.",
                    "Entwicklungsdiagnose ist aus",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using var dialog = new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = "zip",
                FileName = $"AetherBoy-TestReport-{DateTime.Now:yyyyMMdd-HHmm}.zip",
                Filter = "AetherBoy test report (*.zip)|*.zip",
                InitialDirectory = Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                OverwritePrompt = true,
                Title = "Lokalen AetherBoy-Testbericht exportieren"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            try
            {
                string reportPath = testerSession.CreateBundle(dialog.FileName);
                AetherSignal.Show(
                    this,
                    $"Der lokale Testbericht wurde gespeichert.\n\n{reportPath}",
                    "Testbericht exportiert",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception exception) when (
                exception is ArgumentException or IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                testerSession.RecordException("report.export_failed", exception);
                AetherSignal.Show(
                    this,
                    $"Der Testbericht konnte nicht exportiert werden.\n\n{exception.Message}",
                    "Export fehlgeschlagen",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OpenTesterFolder()
        {
            if (testerSession is null)
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(testerSession.SessionDirectory)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                testerSession.RecordException("report.open_folder_failed", exception);
                AetherSignal.Show(
                    this,
                    $"Der Testordner konnte nicht geöffnet werden.\n\n{exception.Message}",
                    "Ordner nicht verfügbar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void frmNano_FormClosing(object sender, FormClosingEventArgs e)
        {
            updateTimer.Stop();
            if (!StopSession())
            {
                e.Cancel = true;
                updateTimer.Start();
                AetherSignal.Show(this,
                    "Der Emulator wird noch beendet. Bitte versuchen Sie es gleich erneut.",
                    "Emulator beschäftigt",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void frmNano_Deactivate(object? sender, EventArgs e)
        {
            EmulationSession currentSession = session;
            if (turboPressed)
            {
                turboPressed = false;
                if (currentSession != null)
                {
                    ObserveSessionCommand(currentSession.SetTurboAsync(isEnabled: false));
                }
            }

            if (input.ClearKeyboard() && currentSession != null)
            {
                ObserveSessionCommand(currentSession.SetButtonsAsync(input.Combined));
            }
            if (keyboardAdvanceButtons != GameBoyAdvanceButtons.None)
            {
                keyboardAdvanceButtons = GameBoyAdvanceButtons.None;
                PostAdvanceInputState();
            }
        }

        private void ResizeWindow(int size)
        {
            if (size <= 0) size = 2;
            if (aetherShellInitialized)
            {
                if (immersiveFullscreen) SetImmersiveFullscreen(false);
                if (WindowState != FormWindowState.Normal)
                {
                    WindowState = FormWindowState.Normal;
                }

                ClientSize = GetAetherClientSize(size);
                if (IsHandleCreated) FitWindowToScreen();
                settings.VideoScaleFactor = Math.Clamp(size, 1, 4);
                UpdateEmulatorSettings();
                return;
            }

            if (FormBorderStyle == FormBorderStyle.None)
            {
                FormBorderStyle = FormBorderStyle.Sizable;
            }

            ClientSize = new Size(
                gameView.VideoGeometry.Width * size,
                menuStrip.Height + gameView.VideoGeometry.Height * size);
            if (settings.VideoScaleFactor != size)
            {
                settings.VideoScaleFactor = size;
            }
            UpdateEmulatorSettings();
        }

        private void LoadConfiguration()
        {
            ApplyWindowsVideoSettings();
            menuAudioQ1.Enabled = false;
            menuAudioQ2.Enabled = false;
            menuAudioQ3.Enabled = false;
            menuAudioQ4.Enabled = true;
            menuAudioC1.Checked = settings.Channel1Enable;
            menuAudioC2.Checked = settings.Channel2Enable;
            menuAudioC3.Checked = settings.Channel3Enable;
            menuAudioC4.Checked = settings.Channel4Enable;
            menuAudioQ1.Checked = false;
            menuAudioQ2.Checked = false;
            menuAudioQ3.Checked = false;
            menuAudioQ4.Checked = true;
            menuAudioOn.Checked = settings.AudioEnable;
            menuSize1.Checked = settings.VideoScaleFactor == 1;
            menuSize2.Checked = settings.VideoScaleFactor == 2;
            menuSize3.Checked = settings.VideoScaleFactor == 3;
            menuSize4.Checked = settings.VideoScaleFactor == 4;
            if (!aetherShellInitialized) ResizeWindow(settings.VideoScaleFactor);
            else UpdateEmulatorSettings();
            menuFrameSkip0.Checked = settings.Frameskip == 0;
            menuFrameSkip1.Checked = settings.Frameskip == 1;
            menuFrameSkip2.Checked = settings.Frameskip == 2;
            menuFrameSkip3.Checked = settings.Frameskip == 3;
            menuFrameSkip4.Checked = settings.Frameskip == 4;
        }

        private void UpdateEmulatorSettings()
        {
            EmulationSession currentSession = session;
            if (currentSession == null)
            {
                UpdateAetherSessionUi(null);
                return;
            }

            if (settings.AudioEnable && Volatile.Read(ref audioOutput) == null)
            {
                if (TryCreateAudioOutput(out NAudioSoundOut output))
                {
                    Volatile.Write(ref audioOutput, output);
                    currentSession.AudioSamplesAvailable += Session_AudioSamplesAvailable;
                }
                else
                {
                    settings.AudioEnable = false;
                    menuAudioOn.Checked = false;
                    ShowAudioUnavailableMessage();
                }
            }
            else if (!settings.AudioEnable && Volatile.Read(ref audioOutput) != null)
            {
                DisposeAudioOutput(currentSession);
            }

            NAudioSoundOut activeOutput = Volatile.Read(ref audioOutput);
            if (activeOutput != null)
            {
                activeOutput.Volume = settings.AudioVolume / 100f;
                activeOutput.LatencyMs = settings.AudioLatencyMs;
            }

            ObserveSessionCommand(currentSession.ConfigureAsync(CreateEmulatorConfiguration()));
            UpdateAetherSessionUi(currentSession.LatestSnapshot);
        }

        private async void ObserveSessionCommand(Task command)
        {
            try
            {
                await command.ConfigureAwait(true);
            }
            catch (InvalidOperationException) when (
                session == null ||
                session.State is SessionState.Stopping or SessionState.Stopped or SessionState.Faulted)
            {
                // Commands racing with an intentional shutdown are safely rejected.
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Emulation command failed: {exception}");
            }
        }

    }
}
