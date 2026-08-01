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
using nanoboy.Input;
using nanoboy.Platform.Audio;

namespace nanoboy
{
    public partial class frmNano : Form
    {

        private EmulationSession session;
        private NanoboySettings settings;
        private frmAudioTool audiotoolwindow;
        private NAudioSoundOut audioOutput;
        private readonly InputAggregator input = new InputAggregator();
        private bool turboPressed;
        private bool sessionFaultReported;
        private readonly int[] displayFrame = new int[EmulationSnapshot.FramePixelCount];
        private long displayedFrameSequence;
        private XInputGamepadState lastPadState;
        private static readonly bool SaveStatesAvailable = false;
        private static readonly TimeSpan SessionShutdownTimeout = TimeSpan.FromSeconds(2);

        public frmNano()
        {
            InitializeComponent();
            Text = ProductInfo.DisplayName;
            Deactivate += frmNano_Deactivate;
            settings = new NanoboySettings();
            LoadConfiguration();
            RebuildRecentFilesMenu();
            SelectSaveSlot(settings.SaveSlot);
            SetPalette(settings.PaletteIndex);
            SetDisplayFilter(settings.DisplayFilterIndex);
            DarkTheme.Apply(this);
        }

        private bool StopSession()
        {
            turboPressed = false;
            input.Clear();

            EmulationSession previousSession = session;
            if (previousSession == null)
            {
                DisposeAudioOutput(null);
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
            }

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
                output.Submit(eventArgs.GetSamplesCopy(), eventArgs.SampleRate);
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

        private byte[] LoadBootROM(bool isColor)
        {
            string bootFileName = isColor ? "gbc_boot.bin" : "dmg_boot.bin";
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, bootFileName);
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
            if (openRom.ShowDialog() == DialogResult.OK)
            {
                LoadRomFile(openRom.FileName);
            }
        }

        private void LoadRomFile(string path)
        {
            if (!File.Exists(path)) return;

            updateTimer.Stop();
            if (!StopSession())
            {
                updateTimer.Start();
                MessageBox.Show(
                    "Der laufende Emulator konnte nicht sicher beendet werden. Die neue ROM wurde nicht geladen.",
                    "Emulator beschäftigt",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            AddRecentFile(path);
            input.Clear();
            turboPressed = false;
            sessionFaultReported = false;

            byte[] bootRom = LoadBootROM(RomHasColorFeatures(path));
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
                    Path.ChangeExtension(path, "sav"),
                    bootRom,
                    configuration,
                    settings.PaletteIndex);
            }
            catch (Exception exception)
            {
                preparedAudioOutput?.Dispose();
                Debug.WriteLine($"Could not start emulation session for '{path}': {exception}");
                MessageBox.Show(
                    $"Die ROM konnte nicht gestartet werden.\n\n{exception.Message}",
                    "ROM konnte nicht geladen werden",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (preparedAudioOutput != null)
            {
                Volatile.Write(ref audioOutput, preparedAudioOutput);
                session.AudioSamplesAvailable += Session_AudioSamplesAvailable;
            }

            displayedFrameSequence = 0;
            lastPadState = XInputGamepadState.Disconnected;
            gameView.ClearFrame();

            if (audiotoolwindow != null && !audiotoolwindow.IsDisposed)
            {
                audiotoolwindow.Session = session;
            }

            updateTimer.Start();
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

        private static bool TryCreateAudioOutput(out NAudioSoundOut output)
        {
            try
            {
                output = new NAudioSoundOut(44_100);
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

        private static void ShowAudioUnavailableMessage()
        {
            MessageBox.Show(
                "Das Windows-Audiogerät konnte nicht geöffnet werden. AetherBoy läuft stumm weiter.",
                "Audio nicht verfügbar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private void AddRecentFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            settings.RecentFiles.Remove(path);
            settings.RecentFiles.Insert(0, path);
            while (settings.RecentFiles.Count > 5)
            {
                settings.RecentFiles.RemoveAt(settings.RecentFiles.Count - 1);
            }
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
                var item = new ToolStripMenuItem(Path.GetFileName(filePath));
                item.Click += (s, e) => LoadRomFile(filePath);
                menuRecentFiles.DropDownItems.Add(item);
            }
        }

        private void menuSaveStateQuickSave_Click(object sender, EventArgs e) => QuickSave();
        private void menuSaveStateQuickLoad_Click(object sender, EventArgs e) => QuickLoad();
        private void menuSaveSlot1_Click(object sender, EventArgs e) => SelectSaveSlot(1);
        private void menuSaveSlot2_Click(object sender, EventArgs e) => SelectSaveSlot(2);
        private void menuSaveSlot3_Click(object sender, EventArgs e) => SelectSaveSlot(3);
        private void menuSaveSlot4_Click(object sender, EventArgs e) => SelectSaveSlot(4);
        private void menuSaveSlot5_Click(object sender, EventArgs e) => SelectSaveSlot(5);

        private void SelectSaveSlot(int slot)
        {
            settings.SaveSlot = slot;
            menuSaveSlot1.Checked = slot == 1;
            menuSaveSlot2.Checked = slot == 2;
            menuSaveSlot3.Checked = slot == 3;
            menuSaveSlot4.Checked = slot == 4;
            menuSaveSlot5.Checked = slot == 5;
        }

        private void QuickSave()
        {
            ShowUnavailableFeature("Save States");
        }

        private void QuickLoad()
        {
            ShowUnavailableFeature("Save States");
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
        }

        private void menuCheats_Click(object sender, EventArgs e)
        {
            EmulationSession currentSession = session;
            if (currentSession == null)
            {
                MessageBox.Show(
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

        private static void ShowUnavailableFeature(string feature)
        {
            MessageBox.Show(
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
                string info = $"Titel: {rom.Title}\n" +
                              $"Typ: {rom.CartridgeType}\n" +
                              $"ROM Größe: {rom.RomSize / 1024} KB\n" +
                              $"RAM Größe: {rom.RamSize / 1024} KB\n" +
                              $"Color (GBC): {(rom.HasColorFeatures ? "Ja" : "Nein")}\n" +
                              $"Super Game Boy (SGB): {(rom.HasSuperGameBoyFeatures ? "Ja" : "Nein")}\n" +
                              $"Region: {(rom.IsJapanese ? "Japan" : "International")}";
                MessageBox.Show(info, "ROM Informationen", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Keine ROM geladen.", "ROM Informationen", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Location = new Point(0, 0);
            this.Size = new Size(Screen.FromControl(this).Bounds.Width, Screen.FromControl(this).Bounds.Height);
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

        private void menuFrameSkip0_Click(object sender, EventArgs e)
        {
            settings.Frameskip = 0;
            UpdateEmulatorSettings();
            LoadConfiguration();
        }

        private void menuFrameSkip1_Click(object sender, EventArgs e)
        {
            settings.Frameskip = 1;
            UpdateEmulatorSettings();
            LoadConfiguration();
        }

        private void menuFrameSkip2_Click(object sender, EventArgs e)
        {
            settings.Frameskip = 2;
            UpdateEmulatorSettings();
            LoadConfiguration();
        }

        private void menuFrameSkip3_Click(object sender, EventArgs e)
        {
            settings.Frameskip = 3;
            UpdateEmulatorSettings();
            LoadConfiguration();
        }

        private void menuFrameSkip4_Click(object sender, EventArgs e)
        {
            settings.Frameskip = 4;
            UpdateEmulatorSettings();
            LoadConfiguration();
        }

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
            PollGamepad();

            EmulationSession currentSession = session;
            if (currentSession == null)
            {
                return;
            }

            if (currentSession.State == SessionState.Faulted && !sessionFaultReported)
            {
                sessionFaultReported = true;
                DisposeAudioOutput(currentSession);
                if (audiotoolwindow != null && !audiotoolwindow.IsDisposed)
                {
                    audiotoolwindow.Session = null;
                }

                Exception fault = currentSession.Fault;
                MessageBox.Show(
                    $"Die Emulation wurde wegen eines Fehlers beendet.\n\n{fault?.Message}",
                    "Emulationsfehler",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (currentSession.TryCopyLatestFrame(displayFrame, ref displayedFrameSequence))
            {
                gameView.Present(displayFrame);
            }
        }

        private void PollGamepad()
        {
            if (session == null)
            {
                input.ClearGamepad();
                lastPadState = XInputGamepadState.Disconnected;
                return;
            }

            XInputGamepadState padState = XInputGamepad.GetState();
            if (!padState.IsConnected)
            {
                if (lastPadState.IsConnected)
                {
                    ReleaseGamepadInput();
                }

                lastPadState = padState;
                return;
            }

            bool up = padState.IsButtonDown(XInputButtons.DPadUp) || padState.LeftThumbY > 0.5f;
            bool down = padState.IsButtonDown(XInputButtons.DPadDown) || padState.LeftThumbY < -0.5f;
            bool left = padState.IsButtonDown(XInputButtons.DPadLeft) || padState.LeftThumbX < -0.5f;
            bool right = padState.IsButtonDown(XInputButtons.DPadRight) || padState.LeftThumbX > 0.5f;

            GameBoyButtons gamepadButtons = GameBoyButtons.None;
            if (padState.IsButtonDown(XInputButtons.A)) gamepadButtons |= GameBoyButtons.A;
            if (padState.IsButtonDown(XInputButtons.B) || padState.IsButtonDown(XInputButtons.X)) gamepadButtons |= GameBoyButtons.B;
            if (padState.IsButtonDown(XInputButtons.Start)) gamepadButtons |= GameBoyButtons.Start;
            if (padState.IsButtonDown(XInputButtons.Back)) gamepadButtons |= GameBoyButtons.Select;
            if (up) gamepadButtons |= GameBoyButtons.Up;
            if (down) gamepadButtons |= GameBoyButtons.Down;
            if (left) gamepadButtons |= GameBoyButtons.Left;
            if (right) gamepadButtons |= GameBoyButtons.Right;

            if (input.SetGamepadButtons(gamepadButtons))
            {
                PostInputState();
            }

            // Save-State-Shortcuts bleiben deaktiviert, bis vollständige Zustände sicher sind.
            if (SaveStatesAvailable &&
                padState.IsButtonDown(XInputButtons.RightShoulder) &&
                !lastPadState.IsButtonDown(XInputButtons.RightShoulder))
            {
                QuickSave();
            }

            if (SaveStatesAvailable &&
                padState.IsButtonDown(XInputButtons.LeftShoulder) &&
                !lastPadState.IsButtonDown(XInputButtons.LeftShoulder))
            {
                QuickLoad();
            }

            lastPadState = padState;
        }

        private void ReleaseGamepadInput()
        {
            if (input.ClearGamepad())
            {
                PostInputState();
            }
        }

        #endregion

        #region Joypad
        private void gameView_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (SaveStatesAvailable && e.KeyCode == Keys.F5)
            {
                QuickSave();
                return;
            }
            if (SaveStatesAvailable && e.KeyCode == Keys.F8)
            {
                QuickLoad();
                return;
            }
            if (SaveStatesAvailable && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D5)
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
        #endregion

        private void frmNano_FormClosing(object sender, FormClosingEventArgs e)
        {
            updateTimer.Stop();
            if (!StopSession())
            {
                e.Cancel = true;
                updateTimer.Start();
                MessageBox.Show(
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
        }

        private void ResizeWindow(int size)
        {
            if (size <= 0) size = 2;
            if (FormBorderStyle == FormBorderStyle.None)
            {
                FormBorderStyle = FormBorderStyle.Sizable;
            }

            ClientSize = new Size(
                GameDisplayControl.FrameWidth * size,
                menuStrip.Height + GameDisplayControl.FrameHeight * size);
            settings.VideoScaleFactor = size;
            UpdateEmulatorSettings();
        }

        private void LoadConfiguration()
        {
            settings.SampleRate = 3;
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
            ResizeWindow(settings.VideoScaleFactor);
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

            ObserveSessionCommand(currentSession.ConfigureAsync(CreateEmulatorConfiguration()));
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
