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
        private AetherBoy.Runtime.Storage.RomWriteLease? romWriteLease;
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
        private readonly GamepadRumble gamepadRumble = new();
        private string currentRomPath;
        private bool stateOperationInProgress;
        private Platform.Video.WindowsFrameTiming frameTiming;
        private bool batteryRecoveryNoticeShown;
        private bool testerRomIdentityRecorded;
        private bool closingSettingsFlush;
        private bool stopSessionSettingsFailed;
        private bool currentSessionUsesExternalBootRom;
        private readonly WindowsTesterSession? testerSession;
        private readonly WindowsSessionHealthMonitor? healthMonitor;
        private static readonly TimeSpan SessionShutdownTimeout = TimeSpan.FromSeconds(2);
        // Gen3 closes through a 3-second peer handshake plus receipt drain and a final private-save flush.
        private static readonly TimeSpan GbaOnlineShutdownTimeout = TimeSpan.FromSeconds(5);

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
            settings.ProfileSaveFailed += exception => ReportSettingsSaveFailure(global::AetherBoy.Runtime.Localization.UiText.Get("Spielprofil"), "profile.save_failed", exception);
            settings.ControllerSaveFailed += exception => ReportSettingsSaveFailure(global::AetherBoy.Runtime.Localization.UiText.Get("Controller-Profil"), "controller_profile.save_failed", exception);
            settings.GlobalSaveFailed += exception => ReportSettingsSaveFailure(global::AetherBoy.Runtime.Localization.UiText.Get("Einstellungen"), "settings.save_failed", exception);
            Disposed += (_, _) => settings.Dispose();
            Disposed += (_, _) => gamepadRumble.Dispose();
            Disposed += (_, _) => discordPresence.Dispose();
            Disposed += (_, _) => releaseUpdates.Dispose();
            LoadConfiguration();
            RebuildRecentFilesMenu();
            SelectSaveSlot(settings.SaveSlot);
            SetPalette(settings.PaletteIndex);
            SetDisplayFilter(settings.DisplayFilterIndex);
            AetherColors.Apply(new UiThemePalette(settings.UiPrimaryColor, settings.UiSecondaryColor, settings.UiBackgroundColor));
            DarkTheme.Apply(this);
            InitializeAetherShell();
            InitializeWindowsExperience();
            InitializePlayerTools();
            InitializeSystemTools();
            InitializeOnlineLinkTools();
            if (testerSession != null)
            {
                healthMonitor = new WindowsSessionHealthMonitor(testerSession);
                Disposed += (_, _) => healthMonitor.Dispose();
            }
            testerSession?.RecordWindowReady(settings);
            updateTimer.Start();
        }

        private void StopSessionAfterDirectDispose()
        {
            // Dispose can bypass FormClosing (including disposal during an async settings flush).
            // Request the owner stop, but keep leases/transports alive until its final save completes.
            CancelRomPreparation();
            EmulationSession previous = session;
            session = null;
            var lease = romWriteLease; romWriteLease = null;
            var browserTransport = onlineLinkTransport; onlineLinkTransport = null;
            var roomTransport = onlineRoomTransport; onlineRoomTransport = null;
            DisposeAudioOutput(previous);
            Task stopped = previous?.ShutdownAsync() ?? Task.CompletedTask;
            _ = stopped.ContinueWith(done =>
            {
                if (done.Exception is { } error) testerSession?.RecordException("session.dispose_failed", error);
                try { browserTransport?.Dispose(); }
                finally { try { roomTransport?.Dispose(); } finally { lease?.Dispose(); } }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private bool StopSession(bool ownsStateOperation = false)
        {
            stopSessionSettingsFailed = false;
            if (stateOperationInProgress && !ownsStateOperation) return false;
            // A ROM transition is a durability boundary. Ordinary slider/button changes are
            // asynchronous, but the previous ROM's profile must be on disk before switching.
            try { settings.FlushPendingSavesAsync().GetAwaiter().GetResult(); }
            catch (Exception exception)
            {
                stopSessionSettingsFailed = true;
                testerSession?.RecordException("settings.switch_flush_failed", exception);
                AetherSignal.Show(this,
                    global::AetherBoy.Runtime.Localization.UiText.Get("Die letzten Einstellungen konnten nicht gespeichert werden. Das laufende Spiel bleibt geöffnet. Prüfe den Speicherort und versuche den Spielwechsel erneut.\n\n") +
                    (exception.InnerException?.Message ?? global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message)),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Spielwechsel nicht möglich"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
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
                romWriteLease?.Dispose();
                romWriteLease = null;
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
            bool gbaOnlineClosing = previousSession.OnlineLink?.ProfileId == AetherBoy.Runtime.Netplay.GbaOnlineProfileCatalog.PokemonGen3Profile;
            if (gbaOnlineClosing && !previousSession.Completion.IsCompleted)
            {
                SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("GBA-Online wird sicher beendet · Gegenstelle abmelden und Sitzungskopie sichern · bis zu 5 Sekunden"), false);
                Update();
            }
            Task shutdown = previousSession.ShutdownAsync();
            Task completed = Task.WhenAny(
                shutdown,
                Task.Delay(gbaOnlineClosing ? GbaOnlineShutdownTimeout : SessionShutdownTimeout)).GetAwaiter().GetResult();

            if (!ReferenceEquals(completed, shutdown))
            {
                Debug.WriteLine("The emulation session did not stop within its bounded shutdown budget.");
                if (gbaOnlineClosing) SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("GBA-Online wird noch beendet · Sitzung und Schreibschutz bleiben erhalten · bitte erneut Beenden wählen"), true);
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
                onlineLinkTransport?.Dispose();
                onlineLinkTransport = null;
                onlineRoomTransport?.Dispose(); onlineRoomTransport = null;
                romWriteLease?.Dispose();
                romWriteLease = null;
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
                if (!ReferenceEquals(sender, session)) return;
                output.Submit(eventArgs.GetInterleavedSamplesCopy(), eventArgs.SampleRate,
                    eventArgs.Channels, eventArgs.PlaybackGeneration, eventArgs.PlaybackSession);
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
            firmwareLoadWarning = null;
            if (!settings.BootRomEnable)
            {
                return null;
            }

            var kind = isGameBoyAdvance ? WindowsFirmwareKind.Gba
                : isColor ? WindowsFirmwareKind.Cgb : WindowsFirmwareKind.Dmg;
            try
            {
                return new WindowsFirmwareStore(WindowsDataPaths.Default).Load(kind);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                testerSession?.RecordException("firmware.read_failed", exception);
                firmwareLoadWarning = global::AetherBoy.Runtime.Localization.UiText.Get("Firmware ungültig oder nicht lesbar · integrierter Start verwendet · SYSTEM → Firmware");
                return null;
            }
        }

        #region "Menu"
        private void menuOpen_Click(object sender, EventArgs e)
        {
            using var library = new frmRomLibrary(settings.RecentFiles);
            if (library.ShowDialog(this) == DialogResult.OK &&
                library.SelectedRomPath is string path)
            {
                _ = PrepareRomLoadAsync(path, library.ResumeRequested);
            }
        }

        internal void LoadRomFile(string path, bool resume = false)
        {
            if (stateOperationInProgress) { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Bitte die laufende Speicheraktion abwarten"), true); return; }
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
                AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.Format("Die ROM konnte nicht in die lokale Bibliothek übernommen werden.\n\n{0}", global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message)),
                    global::AetherBoy.Runtime.Localization.UiText.Get("ROM-Import fehlgeschlagen"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            StartImportedRomFile(path, resume);
        }

        private void StartImportedRomFile(string path, bool resume, bool allowRecovery = true, bool showFailure = true)
        {
            string? previousRomPath = currentRomPath;
            updateTimer.Stop();
            if (!StopSession())
            {
                updateTimer.Start();
                if (!stopSessionSettingsFailed)
                    AetherSignal.Show(this,
                        global::AetherBoy.Runtime.Localization.UiText.Get("Der laufende Emulator konnte nicht sicher beendet werden. Die neue ROM wurde nicht geladen."),
                        global::AetherBoy.Runtime.Localization.UiText.Get("Emulator beschäftigt"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                testerSession?.RecordOperation(
                    "rom_load",
                    slot: null,
                    succeeded: false,
                    reason: "session_shutdown_timeout");
                return;
            }

            try { settings.UseGameProfile(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                settings.UseGameProfile(null);
                AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.Get("Spielprofil nicht lesbar; globale Einstellungen werden verwendet.\n\n") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Spielprofil"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            pendingResume = resume;
            gamepadAwaitNeutral = true;
            sessionPlaySeconds = 0;
            activityClock.Reset();
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
                string savePath = WindowsRomLibrary.Default.GetSavePath(path);
                romWriteLease = AetherBoy.Runtime.Storage.RomWriteLease.Acquire(savePath + ".lock");
                session = new EmulationSession(
                    path,
                    savePath,
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
                romWriteLease?.Dispose();
                romWriteLease = null;
                preparedAudioOutput?.Dispose();
                settings.UseGameProfile(null);
                UpdateAetherSessionUi(null);
                Debug.WriteLine($"Could not start emulation session for '{path}': {exception}");
                bool recovered = allowRecovery && previousRomPath != null &&
                    !string.Equals(previousRomPath, path, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(previousRomPath) && TryRecoverPreviousRom(previousRomPath);
                if (showFailure)
                {
                    string nextStep = recovered ? global::AetherBoy.Runtime.Localization.UiText.Get("Das vorherige Spiel wurde wieder geöffnet.") :
                        previousRomPath is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Wähle ein anderes Spiel aus der Bibliothek.") :
                        global::AetherBoy.Runtime.Localization.UiText.Get("Das vorherige Spiel konnte nicht wieder geöffnet werden. Öffne es erneut über die Bibliothek.");
                    AetherSignal.Show(this,
                        global::AetherBoy.Runtime.Localization.UiText.Format("Die ROM konnte nicht gestartet werden.\n\n{0}\n\n{1}", global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message), nextStep),
                        global::AetherBoy.Runtime.Localization.UiText.Get("ROM konnte nicht geladen werden"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                updateTimer.Start();
                return;
            }

            if (preparedAudioOutput != null)
            {
                Volatile.Write(ref audioOutput, preparedAudioOutput);
                session.AudioSamplesAvailable += Session_AudioSamplesAvailable;
            }

            displayedFrameSequence = 0;
            AddRecentFile(path);
            lastPadState = GamepadInput.GetState();
            UpdateAetherGamepadUi(lastPadState);
            gameView.ClearFrame();
            SetSaveFeedback(firmwareLoadWarning ?? global::AetherBoy.Runtime.Localization.UiText.Get("F5 speichern · F8 laden · F6 State-Galerie · F11 Vollbild"), firmwareLoadWarning is not null);

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
                global::AetherBoy.Runtime.Localization.UiText.Get("Das Windows-Audiogerät konnte nicht geöffnet werden. AetherBoy läuft stumm weiter."),
                global::AetherBoy.Runtime.Localization.UiText.Get("Audio nicht verfügbar"),
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
                var dummy = new nanoboy.Controls.AetherCommand(global::AetherBoy.Runtime.Localization.UiText.Get("Keine")) { Enabled = false };
                menuRecentFiles.DropDownItems.Add(dummy);
                return;
            }

            foreach (var file in settings.RecentFiles)
            {
                string filePath = file;
                bool exists = File.Exists(filePath);
                var item = new nanoboy.Controls.AetherCommand(
                    exists ? Path.GetFileName(filePath) : global::AetherBoy.Runtime.Localization.UiText.Format("{0} (fehlt)", Path.GetFileName(filePath)))
                {
                    Enabled = exists,
                    ToolTipText = filePath
                };
                item.Click += (s, e) => _ = PrepareRomLoadAsync(filePath);
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
                PreviewBootIntro = () => PlayGameIntroAsync(CancellationToken.None, controlCenter),
                SetBarcodeBoyEnabled = enabled => (session ?? throw new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("Kein Spiel geöffnet."))).SetBarcodeBoyEnabledAsync(enabled),
                ScanBarcodeBoy = code => (session ?? throw new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("Kein Spiel geöffnet."))).ScanBarcodeBoyAsync(code),
                GamepadProvider = () => GamepadInput.GetState(),
                RomPathProvider = () => currentRomPath,
                SetPalette = SetPalette,
                SetDisplayFilter = SetDisplayFilter,
                SetWindowScale = ResizeWindow,
                ToggleFullscreen = ToggleAetherFullscreen,
                ApplyAudioSettings = ApplyAudioSettingsFromControlCenter,
                ApplyVideoSettings = ApplyWindowsVideoSettings,
                ApplyDiscordSettings = UpdateDiscordPresence,
                DiscordStatusProvider = () => discordPresence.Status,
                DiscordPreviewProvider = () => discordPresence.Preview,
                Updates = releaseUpdates,
                ApplyUiTheme = () => AetherColors.Apply(new UiThemePalette(settings.UiPrimaryColor, settings.UiSecondaryColor, settings.UiBackgroundColor)),
                AudioOutputProvider = DescribeWindowsAudio,
                VideoOutputProvider = DescribeWindowsVideo,
                SaveFeedbackProvider = () => saveFeedback,
                OpenStateGallery = OpenStateGallery,
                ToggleGameProfile = () => ChangeGameProfile(false),
                ResetGameProfile = () => ChangeGameProfile(true),
                OpenFirmwareManager = OpenFirmwareManager,
                FirmwareStatusProvider = DescribeFirmware,
                RecordNextSessionProvider = () => WindowsDiagnosticsPreferences.Default.GetStatus().RecordNextSession,
                DiagnosticsPreferenceStatusProvider = DescribeDiagnosticsPreference,
                SetRecordNextSession = SetRecordNextSession,
                OpenQuickMenu = () => _ = OpenQuickMenuAsync(),
                OpenLibrary = () => menuOpen_Click(controlCenter, EventArgs.Empty),
                OpenPatchLab = OpenWindowsPatchLab,
                OpenCheats = () => menuCheats_Click(controlCenter, EventArgs.Empty),
                CreateOnlineRoom = () => ShowOnlineRoomDialog(true),
                JoinOnlineRoom = () => ShowOnlineRoomDialog(false),
                TestOnlineConnection = ShowOnlineConnectionTest,
                CaptureScreenshot = () => _ = CaptureScreenshotAsync(),
                ToggleGameplayRecording = () => _ = ToggleGameplayRecordingAsync(),
                GameplayRecordingActive = () => (session?.GameplayRecording ?? lastGameplayRecording)?.IsRecording == true,
                GameplayRecordingStatus = GetGameplayRecordingStatus,
                TogglePerformanceOverlay = TogglePerformanceOverlay,
                MarkProblem = MarkSessionProblem,
                HealthStatusProvider = () => healthMonitor?.Status ?? global::AetherBoy.Runtime.Localization.UiText.Get("Development-Diagnose ist nicht aktiv."),
                SetFrameskip = SetFrameskip,
                SetSaveSlot = SelectSaveSlot,
                OpenControls = () => new frmControls(settings).ShowDialog(controlCenter),
                OpenSaveSafety = () => OpenBatterySaveSafety(controlCenter),
                OpenAudioInspector = () => menuAudioInspector_Click(controlCenter, EventArgs.Empty),
                QuickSave = QuickSave,
                QuickLoad = QuickLoad,
                ResetSettings = ResetSettingsToDefaults,
                TesterModeProvider = () => testerSession?.IsRecording == true,
                TesterReportAvailableProvider = () => testerSession is not null && !testerExportInProgress,
                TesterRecordingStatusProvider = DescribeActiveRecording,
                TesterLogPathProvider = () => testerSession?.LogFilePath,
                ExportTesterReport = ExportTesterReport,
                OpenTesterFolder = OpenTesterFolder
            });
            controlCenter.FormClosed += (_, _) => controlCenter = null;
            controlCenter.Show(this);
        }

        private void OpenBatterySaveSafety(IWin32Window owner)
        {
            if (IsOnlineLink) { WindowsDataPaths.OpenFolder(owner, onlineLinkDirectory!); return; }
            EmulationSession currentSession = session;
            string romPath = currentRomPath;
            RomSnapshot? rom = currentSession?.LatestSnapshot.Rom;
            if (currentSession == null || string.IsNullOrEmpty(romPath) || rom == null)
            {
                AetherSignal.Show(
                    owner,
                    global::AetherBoy.Runtime.Localization.UiText.Get("Starte zuerst ein Spiel. Danach zeigt das Save Safety Center den Batterie-Spielstand und seine Backups."),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Kein Spiel aktiv"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            if (!rom.BatterySave.IsEnabled || rom.BatterySave.ExpectedLength <= 0)
            {
                AetherSignal.Show(
                    owner,
                    global::AetherBoy.Runtime.Localization.UiText.Get("Dieses Spiel besitzt keinen unterstützten Batterie-RAM-Spielstand. Save States bleiben davon unabhängig verfügbar."),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Kein Batterie-Spielstand"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            string savePath = WindowsRomLibrary.Default.GetSavePath(romPath);
            byte[]? selectedSaveData;
            BatterySaveAction selectedAction;
            string? selectedSourceName;
            int generation;
            try
            {
                using var manager = new frmBatterySaveManager(
                    savePath,
                    rom.BatterySave.ExpectedLength,
                    string.IsNullOrWhiteSpace(rom.Title) ? Path.GetFileNameWithoutExtension(romPath) : rom.Title);
                if (manager.ShowDialog(owner) != DialogResult.OK || manager.SelectedAction == BatterySaveAction.None)
                {
                    return;
                }

                selectedSaveData = manager.SelectedSaveData;
                selectedAction = manager.SelectedAction;
                selectedSourceName = manager.SelectedSourceName;
                generation = (int)manager.SelectedGeneration;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidDataException)
            {
                AetherSignal.Show(
                    owner,
                    global::AetherBoy.Runtime.Localization.UiText.Format("Die Sicherungen konnten nicht gelesen werden.\n\n{0}", global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message)),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Save Safety nicht verfügbar"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (selectedAction != BatterySaveAction.ExportArchive)
            {
                string question = selectedAction == BatterySaveAction.ImportFile
                    ? global::AetherBoy.Runtime.Localization.UiText.Format("{0} für dieses Spiel importieren?\n\nDie Dateigröße passt, aber die Herkunft kann nicht geprüft werden. Der bisherige Stand wird zuvor als Archiv gesichert. Danach startet das Spiel neu.", selectedSourceName)
                    : global::AetherBoy.Runtime.Localization.UiText.Format("Backup {0} wirklich aktivieren?\n\nDer bisherige Stand wird zuvor als Archiv gesichert. Danach startet das Spiel neu.", generation);
                if (AetherSignal.Show(owner, question,
                    selectedAction == BatterySaveAction.ImportFile ? global::AetherBoy.Runtime.Localization.UiText.Get("Spielstand importieren") : global::AetherBoy.Runtime.Localization.UiText.Get("Backup wiederherstellen"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            }

            if (stateOperationInProgress) { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Bitte die laufende Speicheraktion abwarten"), true); return; }
            stateOperationInProgress = true;
            updateTimer.Stop();
            try
            {
                if (!StopSession(ownsStateOperation: true))
                {
                    AetherSignal.Show(
                        owner,
                        global::AetherBoy.Runtime.Localization.UiText.Get("Der laufende Emulator konnte nicht sicher beendet werden. Das Backup wurde nicht verändert."),
                        global::AetherBoy.Runtime.Localization.UiText.Get("Wiederherstellung abgebrochen"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    updateTimer.Start();
                    return;
                }

                string archivePath;
                using (AetherBoy.Runtime.Storage.RomWriteLease.Acquire(savePath + ".lock"))
                {
                    archivePath = WindowsSaveArchive.Export(WindowsDataPaths.Default,
                        WindowsRomLibrary.Default.GetIdentity(romPath), savePath, rom.BatterySave.ExpectedLength);
                    if (selectedAction != BatterySaveAction.ExportArchive)
                        BatterySaveStore.Restore(savePath, rom.BatterySave.ExpectedLength, selectedSaveData!);
                }
                stateOperationInProgress = false; // Disk restore finished; the regular ROM-start guard applies again.
                LoadRomFile(romPath);
                if (session != null)
                {
                    string message = selectedAction switch
                    {
                        BatterySaveAction.ImportFile => global::AetherBoy.Runtime.Localization.UiText.Format("Der gewählte Spielstand ist aktiv. Der vorherige Stand liegt im Archiv:\n{0}", archivePath),
                        BatterySaveAction.ExportArchive => global::AetherBoy.Runtime.Localization.UiText.Format("Spielstand und Sicherungen wurden exportiert:\n{0}", archivePath),
                        _ => global::AetherBoy.Runtime.Localization.UiText.Format("Backup {0} ist aktiv. Der vorherige Stand liegt im Archiv:\n{1}", generation, archivePath)
                    };
                    AetherSignal.Show(
                        owner,
                        message,
                        selectedAction == BatterySaveAction.ExportArchive ? global::AetherBoy.Runtime.Localization.UiText.Get("Archiv erstellt") : global::AetherBoy.Runtime.Localization.UiText.Get("Spielstand aktiviert"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidDataException)
            {
                Debug.WriteLine($"Could not complete battery save action: {exception}");
                if (session == null && File.Exists(romPath))
                {
                    stateOperationInProgress = false;
                    LoadRomFile(romPath);
                }

                AetherSignal.Show(
                    owner,
                    global::AetherBoy.Runtime.Localization.UiText.Format("Die Speicheraktion konnte nicht abgeschlossen werden. Der bisherige Stand bleibt erhalten oder liegt im zuvor erstellten Archiv.\n\n{0}", global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message)),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Speicheraktion fehlgeschlagen"),
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
            menuSaveSlot1.Text = slot == 1 ? global::AetherBoy.Runtime.Localization.UiText.Get("Slot 1 (Aktiv)") : "Slot 1";
            menuSaveSlot2.Text = slot == 2 ? global::AetherBoy.Runtime.Localization.UiText.Get("Slot 2 (Aktiv)") : "Slot 2";
            menuSaveSlot3.Text = slot == 3 ? global::AetherBoy.Runtime.Localization.UiText.Get("Slot 3 (Aktiv)") : "Slot 3";
            menuSaveSlot4.Text = slot == 4 ? global::AetherBoy.Runtime.Localization.UiText.Get("Slot 4 (Aktiv)") : "Slot 4";
            menuSaveSlot5.Text = slot == 5 ? global::AetherBoy.Runtime.Localization.UiText.Get("Slot 5 (Aktiv)") : "Slot 5";
            UpdateAetherSlotButtons();
        }

        private async void QuickSave() => await SaveCheckpointAsync(settings.SaveSlot);
        private async void QuickLoad() => await LoadCheckpointAsync(settings.SaveSlot);

        private async void menuRewind_Click(object sender, EventArgs e)
        {
            if (IsOnlineLink) { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Rewind ist im Online-Link gesperrt"), true); return; }
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
                        global::AetherBoy.Runtime.Localization.UiText.Get("Es ist noch kein früherer Zustand im Rewind-Puffer vorhanden."),
                        global::AetherBoy.Runtime.Localization.UiText.Get("Rewind"),
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
                    global::AetherBoy.Runtime.Localization.UiText.Format("Zurückspulen ist fehlgeschlagen.\n\n{0}", global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message)),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Rewind fehlgeschlagen"),
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
            ApplyWindowsVideoSettings();
            UpdateAetherSessionUi(session?.LatestSnapshot);
        }

        private void menuCheats_Click(object sender, EventArgs e)
        {
            if (IsOnlineLink) { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Cheats sind im Online-Link gesperrt"), true); return; }
            EmulationSession currentSession = session;
            if (currentSession == null)
            {
                AetherSignal.Show(this,
                    global::AetherBoy.Runtime.Localization.UiText.Get("Bitte zuerst eine ROM laden."),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Cheat Manager"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            new frmCheats(currentSession).ShowDialog(this);
        }

        private void menuLinkCable_Click(object sender, EventArgs e)
        {
            using var link = new frmLocalLinkLab(settings, currentRomPath, () => StopSession());
            discordLocalLinkOpen = true;
            UpdateDiscordPresence();
            try { link.ShowDialog(controlCenter is { IsDisposed: false } ? controlCenter : this); }
            finally { discordLocalLinkOpen = false; UpdateDiscordPresence(); }
        }

        private void ShowUnavailableFeature(string feature)
        {
            AetherSignal.Show(this,
                global::AetherBoy.Runtime.Localization.UiText.Format("{0} ist in dieser Version experimentell und vorerst deaktiviert.", feature),
                global::AetherBoy.Runtime.Localization.UiText.Get("Funktion deaktiviert"),
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
                string info = global::AetherBoy.Runtime.Localization.UiText.Format("Titel: {0}\n", rom.Title) +
                              global::AetherBoy.Runtime.Localization.UiText.Format("Typ: {0}\n", rom.CartridgeType) +
                              global::AetherBoy.Runtime.Localization.UiText.Format("System: {0}\n", model) +
                              global::AetherBoy.Runtime.Localization.UiText.Format("ROM Größe: {0} KB\n", rom.RomSize / 1024) +
                              global::AetherBoy.Runtime.Localization.UiText.Format("RAM Größe: {0} KB\n", rom.RamSize / 1024) +
                              global::AetherBoy.Runtime.Localization.UiText.Format("Super Game Boy (SGB): {0}\n", (rom.IsGameBoyAdvance ? global::AetherBoy.Runtime.Localization.UiText.Get("Nicht zutreffend") : rom.HasSuperGameBoyFeatures ? global::AetherBoy.Runtime.Localization.UiText.Get("Ja") : global::AetherBoy.Runtime.Localization.UiText.Get("Nein"))) +
                              global::AetherBoy.Runtime.Localization.UiText.Format("Region: {0}", (rom.IsJapanese ? "Japan" : "International"));
                AetherSignal.Show(this, info, global::AetherBoy.Runtime.Localization.UiText.Get("ROM Informationen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.Get("Keine ROM geladen."), global::AetherBoy.Runtime.Localization.UiText.Get("ROM Informationen"), MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            try { settings.FlushPendingSavesAsync().GetAwaiter().GetResult(); }
            catch (Exception exception)
            {
                AetherSignal.Show(this,
                    global::AetherBoy.Runtime.Localization.UiText.Get("Offene Änderungen konnten nicht gespeichert werden. Prüfe den Speicherort und versuche das Zurücksetzen erneut.\n\n") +
                    (exception.InnerException?.Message ?? global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message)),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Zurücksetzen nicht möglich"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (settings.HasGameProfile) settings.ResetGameProfile();
            nanoboy.Properties.Settings.Default.Reset();
            AetherColors.Apply(new UiThemePalette(settings.UiPrimaryColor, settings.UiSecondaryColor, settings.UiBackgroundColor));
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
            PollDiscordPresence();
            PollStartupUpdateCheck();
            if (FinishStoppedOnlineLink()) return;
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
                // The owner may have faulted after this tick's initial check.
                // Online cleanup retains the actionable browser reason on the next tick.
                if (currentSession.OnlineLink is not null) return;
                AetherSignal.Show(this,
                    global::AetherBoy.Runtime.Localization.UiText.Format("Die Emulation wurde wegen eines Fehlers beendet.\n\n{0}", fault?.Message),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Emulationsfehler"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            EmulationSnapshot snapshot = currentSession.LatestSnapshot;
            UpdateOnlineLinkUi();
            TrackGameActivity(snapshot);
            UpdatePerformanceOverlay(snapshot);
            bool running = snapshot.State == SessionState.Running;
            frameTiming.SetActive(running && WindowState != FormWindowState.Minimized && !snapshot.IsTurboEnabled);
            bool gbaNetworkWait = snapshot.Rom?.IsGameBoyAdvance == true &&
                currentSession.OnlineLink is { Phase: not AetherBoy.Runtime.Netplay.OnlineLinkPhase.Playing };
            Volatile.Read(ref audioOutput)?.SetSuspended(!running || stateOperationInProgress || gbaNetworkWait);
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
                    global::AetherBoy.Runtime.Localization.UiText.Format("Der aktuelle Batterie-Spielstand war nicht verwendbar. AetherBoy hat automatisch Backup {0} geladen und repariert die Hauptdatei bei der nächsten Sicherung.", generation),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Spielstand automatisch gerettet"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void PollGamepad()
        {
            HostGamepadState state = GamepadInput.GetState();
            if (bootIntro is not null)
            {
                if ((state.Buttons & HostGamepadButtons.South) != 0 && (lastPadState.Buttons & HostGamepadButtons.South) == 0) bootIntro.Skip();
                lastPadState = state; return;
            }
            bool active = Form.ActiveForm == this && ContainsFocus && Enabled;
            EmulationSnapshot? current = session?.LatestSnapshot;
            gamepadRumble.Update(state, settings.RumbleEnabled && active && current is { State: SessionState.Running, IsPaused: false, RumbleActive: true }
                && !IsOnlineLink && !stateOperationInProgress && !quickMenuOpen);
            if (active) aetherCommandMenu?.ProcessGamepad(state);
            ProcessGamepadState(state, active && aetherCommandMenu is null);
        }

        private void MarkSessionProblem()
        {
            bool recorded = healthMonitor?.MarkProblem() == true;
            SetSaveFeedback(recorded ? global::AetherBoy.Runtime.Localization.UiText.Get("Problemzeitpunkt lokal im Testbericht markiert") :
                global::AetherBoy.Runtime.Localization.UiText.Get("Diagnose nicht aktiv oder Zeitpunkt gerade erst markiert"), !recorded);
        }

        internal void ProcessGamepadState(HostGamepadState padState, bool ownsInputFocus)
        {
            if (padState.DeviceId != lastPadState.DeviceId) { ReleaseGamepadInput(); gamepadAwaitNeutral = true; }
            settings.UseControllerProfile(padState);
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
                gamepadAwaitNeutral = !settings.IsControllerNeutral(padState);
                lastPadState = padState; return;
            }
            const HostGamepadButtons menuChord = HostGamepadButtons.LeftStick | HostGamepadButtons.RightStick;
            if (padState.IsButtonDown(menuChord) && !lastPadState.IsButtonDown(menuChord))
            { lastPadState = padState; _ = OpenQuickMenuAsync(); return; }

            GamepadBindings bindings = settings.GamepadBindings;
            GameBoyButtons gamepadButtons = GamepadMapper.ToGameBoyButtons(padState, bindings,
                settings.ControllerStickEnabled ? settings.ControllerStickThreshold : 1f);
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
            if (bootIntro is not null) return;
            FinishStoppedOnlineLink();
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

            if (e.KeyCode == Keys.Space && !turboPressed && !IsOnlineLink)
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
            FinishStoppedOnlineLink();
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

        private async void ExportTesterReport()
        {
            if (testerExportInProgress) return;
            if (testerSession is null)
            {
                AetherSignal.Show(
                    this,
                    global::AetherBoy.Runtime.Localization.UiText.Get("Für diesen Programmstart ist keine Sitzungsaufzeichnung verfügbar. ") +
                    global::AetherBoy.Runtime.Localization.UiText.Get("Im Control Center unter DIAGNOSTICS kannst du sie für den nächsten Start aktivieren. ") +
                    global::AetherBoy.Runtime.Localization.UiText.Get("Bereits vorhandene Berichte bleiben im Development-Ordner erhalten."),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Entwicklungsdiagnose ist aus"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using var dialog = new AetherFileDialog
            {
                Save = true,
                AddExtension = true,
                DefaultExt = "zip",
                FileName = $"AetherBoy-TestReport-{DateTime.Now:yyyyMMdd-HHmm}.zip",
                Filter = "AetherBoy test report (*.zip)|*.zip",
                InitialDirectory = Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                OverwritePrompt = true,
                Title = global::AetherBoy.Runtime.Localization.UiText.Get("Lokalen AetherBoy-Testbericht exportieren")
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            testerExportInProgress = true;
            try
            {
                string destination = dialog.FileName;
                string reportPath = await Task.Run(() => testerSession.CreateBundle(destination));
                if (IsDisposed) return;
                AetherSignal.Show(
                    this,
                    global::AetherBoy.Runtime.Localization.UiText.Format("Der lokale Testbericht wurde gespeichert.\n\n{0}", reportPath),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Testbericht exportiert"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception exception) when (
                exception is ArgumentException or IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                testerSession.RecordException("report.export_failed", exception);
                if (IsDisposed) return;
                AetherSignal.Show(
                    this,
                    global::AetherBoy.Runtime.Localization.UiText.Format("Der Testbericht konnte nicht exportiert werden.\n\n{0}", global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message)),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Export fehlgeschlagen"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally { testerExportInProgress = false; }
        }

        private void OpenTesterFolder()
        {
            if (testerSession is null)
            {
                WindowsDataPaths.OpenFolder(controlCenter is { IsDisposed: false } ? controlCenter : this,
                    WindowsDataPaths.Default.Development);
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
                    global::AetherBoy.Runtime.Localization.UiText.Format("Der Testordner konnte nicht geöffnet werden.\n\n{0}", global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message)),
                    global::AetherBoy.Runtime.Localization.UiText.Get("Ordner nicht verfügbar"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private async void frmNano_FormClosing(object sender, FormClosingEventArgs e)
        {
            CancelRomPreparation();
            if (closingSettingsFlush) { e.Cancel = true; return; }
            if (settings.HasPendingSaves)
            {
                e.Cancel = true;
                closingSettingsFlush = true;
                try
                {
                    await settings.FlushPendingSavesAsync();
                    if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(Close));
                }
                catch (Exception exception) { ReportSettingsSaveFailure("", "settings.close_flush_failed", exception); }
                finally { closingSettingsFlush = false; }
                return;
            }
            updateTimer.Stop();
            if (!StopSession())
            {
                e.Cancel = true;
                updateTimer.Start();
                if (!stopSessionSettingsFailed)
                    AetherSignal.Show(this,
                        global::AetherBoy.Runtime.Localization.UiText.Get("Der Emulator wird noch beendet. Bitte versuchen Sie es gleich erneut."),
                        global::AetherBoy.Runtime.Localization.UiText.Get("Emulator beschäftigt"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
            }
        }

        private void ReportSettingsSaveFailure(string area, string diagnosticCode, Exception exception)
        {
            testerSession?.RecordException(diagnosticCode, exception);
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(() => ShowSettingsSaveFailure(exception))); }
                catch (InvalidOperationException) { /* The window has finished closing. The failure is already recorded. */ }
                return;
            }
            ShowSettingsSaveFailure(exception);
        }

        private void ShowSettingsSaveFailure(Exception exception)
        {
            if (!IsDisposed && IsHandleCreated) settingsSaveNotice?.SetFailure(exception);
        }

        private void frmNano_Deactivate(object? sender, EventArgs e)
        {
            PauseForFocusLoss();
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

            ClientSize = new Size(
                gameView.VideoGeometry.Width * size,
                gameView.VideoGeometry.Height * size);
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
