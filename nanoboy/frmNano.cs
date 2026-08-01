using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Input;

namespace nanoboy
{
    public partial class frmNano : Form
    {

        private Nanoboy nano;
        private NanoboySettings settings;
        private Thread gamethread;
        private CancellationTokenSource gameCts;
        private frmAudioTool audiotoolwindow;
        private volatile bool speedup;
        private string currentRomPath;
        private RewindManager rewindManager = new RewindManager();
        private CheatEngine cheatEngine = new CheatEngine();
        private LinkCable linkCable = new LinkCable();
        private volatile bool isRewinding;
        private readonly int[] displayFrame = new int[Video.FramePixelCount];
        private long displayedFrameSequence;
        private XInputGamepadState lastPadState;
        private static readonly bool SaveStatesAvailable = false;
        private static readonly bool RewindAvailable = false;
        private static readonly bool LinkCableAvailable = false;
        private static readonly TimeSpan GameThreadJoinTimeout = TimeSpan.FromSeconds(2);

        public frmNano()
        {
            InitializeComponent();
            Text = ProductInfo.DisplayName;
            settings = new NanoboySettings();
            LoadConfiguration();
            RebuildRecentFilesMenu();
            SelectSaveSlot(settings.SaveSlot);
            SetPalette(settings.PaletteIndex);
            SetDisplayFilter(settings.DisplayFilterIndex);
            DarkTheme.Apply(this);
        }

        private bool StopGameThread()
        {
            speedup = false;
            isRewinding = false;
            rewindManager.IsRewinding = false;

            CancellationTokenSource cancellation = gameCts;
            Thread thread = gamethread;

            if (cancellation == null)
            {
                gamethread = null;
                return true;
            }

            cancellation.Cancel();

            if (thread != null && thread.IsAlive && !thread.Join(GameThreadJoinTimeout))
            {
                Debug.WriteLine("The emulation thread did not stop within two seconds.");
                return false;
            }

            if (ReferenceEquals(gameCts, cancellation))
            {
                gameCts = null;
                gamethread = null;
            }

            cancellation.Dispose();
            return true;
        }

        private void RunGameLoop(Nanoboy emulator, CancellationToken token)
        {
            var clock = Stopwatch.StartNew();
            double frameTicks = EmulationClock.FrameDuration.TotalSeconds * Stopwatch.Frequency;
            double nextDeadline = clock.ElapsedTicks + frameTicks;

            while (!token.IsCancellationRequested)
            {
                if (RewindAvailable && isRewinding)
                {
                    rewindManager.Rewind(emulator);
                }
                else
                {
                    emulator.Frame();
                    if (RewindAvailable)
                    {
                        rewindManager.CaptureFrame(emulator);
                    }
                    cheatEngine.ApplyCheats(emulator.Memory);
                }

                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (speedup)
                {
                    nextDeadline = clock.ElapsedTicks + frameTicks;
                    continue;
                }

                if (!WaitUntilDeadline(clock, nextDeadline, token))
                {
                    break;
                }

                nextDeadline += frameTicks;
                if (clock.ElapsedTicks - nextDeadline > frameTicks * 4)
                {
                    nextDeadline = clock.ElapsedTicks + frameTicks;
                }
            }
        }

        private static bool WaitUntilDeadline(
            Stopwatch clock,
            double deadline,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                double remainingTicks = deadline - clock.ElapsedTicks;
                if (remainingTicks <= 0)
                {
                    return true;
                }

                TimeSpan remaining = TimeSpan.FromSeconds(
                    remainingTicks / Stopwatch.Frequency);
                if (cancellationToken.WaitHandle.WaitOne(remaining))
                {
                    return false;
                }
            }

            return false;
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
            if (!StopGameThread())
            {
                updateTimer.Start();
                MessageBox.Show(
                    "Der laufende Emulator konnte nicht sicher beendet werden. Die neue ROM wurde nicht geladen.",
                    "Emulator beschäftigt",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            nano?.Dispose();
            nano = null;

            currentRomPath = path;
            AddRecentFile(path);

            if (RewindAvailable)
            {
                rewindManager.Clear();
            }
            ROM rom = new ROM(path, Path.ChangeExtension(path, "sav"));
            byte[] bootRom = LoadBootROM(rom.HasColorFeatures);

            nano = new Nanoboy(rom, bootRom);
            ApplyEmulatorSettings(nano);
            nano.Memory.Video.SetMonochromePalette(settings.PaletteIndex);
            displayedFrameSequence = 0;
            lastPadState = XInputGamepadState.Disconnected;
            gameView.ClearFrame();

            if (audiotoolwindow != null) {
                audiotoolwindow.Nanoboy = nano;
            }

            gameCts = new CancellationTokenSource();
            var token = gameCts.Token;
            Nanoboy emulator = nano;

            gamethread = new Thread(() => RunGameLoop(emulator, token))
            {
                IsBackground = true,
                Name = "AetherBoy emulation",
                Priority = ThreadPriority.Normal
            };
            gamethread.Start();
            updateTimer.Start();
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

        private string GetSaveStatePath(int slot)
        {
            if (string.IsNullOrEmpty(currentRomPath)) return null;
            return currentRomPath + ".ss" + slot;
        }

        private void QuickSave()
        {
            if (!SaveStatesAvailable)
            {
                ShowUnavailableFeature("Save States");
                return;
            }

            string path = GetSaveStatePath(settings.SaveSlot);
            if (!string.IsNullOrEmpty(path) && nano != null)
            {
                bool ok = SaveState.Save(nano, path);
                MessageBox.Show(ok ? $"Save State (Slot {settings.SaveSlot}) gespeichert!" : "Fehler beim Speichern des Save States.", "Save State", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
        }

        private void QuickLoad()
        {
            if (!SaveStatesAvailable)
            {
                ShowUnavailableFeature("Save States");
                return;
            }

            string path = GetSaveStatePath(settings.SaveSlot);
            if (!string.IsNullOrEmpty(path) && nano != null)
            {
                bool ok = SaveState.Load(nano, path);
                MessageBox.Show(ok ? $"Save State (Slot {settings.SaveSlot}) geladen!" : "Save State Datei konnte nicht geladen werden.", "Save State", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
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
            if (nano != null)
            {
                nano.Memory.Video.SetMonochromePalette(index);
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

        private void menuCheats_Click(object sender, EventArgs e) => new frmCheats(cheatEngine).ShowDialog();

        private void menuLinkCable_Click(object sender, EventArgs e)
        {
            if (!LinkCableAvailable)
            {
                ShowUnavailableFeature("Link-Kabel Multiplayer");
                return;
            }

            new frmLink(linkCable).ShowDialog();
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
            if (nano != null && nano.Memory?.ROM != null)
            {
                var rom = nano.Memory.ROM;
                string info = $"Titel: {rom.Title.Trim()}\n" +
                              $"Typ: {rom.CartridgeType}\n" +
                              $"ROM Größe: {rom.ROMSize / 1024} KB\n" +
                              $"RAM Größe: {rom.RAMSize / 1024} KB\n" +
                              $"Color (GBC): {(rom.HasColorFeatures ? "Ja" : "Nein")}\n" +
                              $"Super Game Boy (SGB): {(rom.HasSGBFeatures ? "Ja" : "Nein")}\n" +
                              $"Region: {(rom.Japanese ? "Japan" : "International")}";
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
                audiotoolwindow.Nanoboy = nano;
                audiotoolwindow.BringToFront();
                return;
            }

            audiotoolwindow = new frmAudioTool();
            audiotoolwindow.Nanoboy = nano;
            audiotoolwindow.FormClosed += (closedSender, closedArgs) => audiotoolwindow = null;
            audiotoolwindow.Show(this);
        }
        #endregion

        #region "Update"
        private void updateTimer_Tick(object sender, EventArgs e)
        {
            PollGamepad();

            Nanoboy emulator = nano;
            if (emulator != null && emulator.Memory.Video.TryCopyPublishedFrame(
                    displayFrame,
                    ref displayedFrameSequence))
            {
                gameView.Present(displayFrame);
            }
        }

        private void PollGamepad()
        {
            Nanoboy emulator = nano;
            if (emulator == null)
            {
                lastPadState = XInputGamepadState.Disconnected;
                return;
            }

            XInputGamepadState padState = XInputGamepad.GetState();
            if (!padState.IsConnected)
            {
                if (lastPadState.IsConnected)
                {
                    ReleaseGamepadInput(emulator);
                }

                lastPadState = padState;
                return;
            }

            SetOrUnsetKey(emulator, padState.IsButtonDown(XInputButtons.A), settings.KeyA);
            SetOrUnsetKey(
                emulator,
                padState.IsButtonDown(XInputButtons.B) || padState.IsButtonDown(XInputButtons.X),
                settings.KeyB);
            SetOrUnsetKey(emulator, padState.IsButtonDown(XInputButtons.Start), settings.KeyStart);
            SetOrUnsetKey(emulator, padState.IsButtonDown(XInputButtons.Back), settings.KeySelect);

            bool up = padState.IsButtonDown(XInputButtons.DPadUp) || padState.LeftThumbY > 0.5f;
            bool down = padState.IsButtonDown(XInputButtons.DPadDown) || padState.LeftThumbY < -0.5f;
            bool left = padState.IsButtonDown(XInputButtons.DPadLeft) || padState.LeftThumbX < -0.5f;
            bool right = padState.IsButtonDown(XInputButtons.DPadRight) || padState.LeftThumbX > 0.5f;

            SetOrUnsetKey(emulator, up, settings.KeyUp);
            SetOrUnsetKey(emulator, down, settings.KeyDown);
            SetOrUnsetKey(emulator, left, settings.KeyLeft);
            SetOrUnsetKey(emulator, right, settings.KeyRight);

            // Rewind bleibt bis zur Korrektur des Save-State-Unterbaus deaktiviert.
            bool controllerRewind = RewindAvailable && padState.LeftTrigger > 0.5f;
            isRewinding = controllerRewind;
            rewindManager.IsRewinding = controllerRewind;

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

        private void ReleaseGamepadInput(Nanoboy emulator)
        {
            SetOrUnsetKey(emulator, false, settings.KeyA);
            SetOrUnsetKey(emulator, false, settings.KeyB);
            SetOrUnsetKey(emulator, false, settings.KeyStart);
            SetOrUnsetKey(emulator, false, settings.KeySelect);
            SetOrUnsetKey(emulator, false, settings.KeyUp);
            SetOrUnsetKey(emulator, false, settings.KeyDown);
            SetOrUnsetKey(emulator, false, settings.KeyLeft);
            SetOrUnsetKey(emulator, false, settings.KeyRight);
            isRewinding = false;
            rewindManager.IsRewinding = false;
        }

        private static void SetOrUnsetKey(Nanoboy emulator, bool isPressed, Keys key)
        {
            if (isPressed)
            {
                emulator.SetKey(key);
            }
            else
            {
                emulator.UnsetKey(key);
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
            if (nano != null) {
                if (e.KeyCode == Keys.Space) {
                    speedup = true;
                }
                nano.SetKey(e.KeyCode);
            }
        }

        private void gameView_KeyUp(object sender, KeyEventArgs e)
        {
            if (nano != null) {
                if (e.KeyCode == Keys.Space) {
                    speedup = false;
                }
                nano.UnsetKey(e.KeyCode);
            }
        }
        #endregion

        private void frmNano_FormClosing(object sender, FormClosingEventArgs e)
        {
            updateTimer.Stop();
            if (StopGameThread() && nano != null) {
                nano.Dispose();
                nano = null;
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
            if (nano != null)
            {
                ApplyEmulatorSettings(nano);
            }
        }

        private void ApplyEmulatorSettings(Nanoboy emulator)
        {
            if (emulator.SetSettings(settings))
            {
                return;
            }

            settings.AudioEnable = false;
            menuAudioOn.Checked = false;
            emulator.SetSettings(settings);
            MessageBox.Show(
                "Das Windows-Audiogerät konnte nicht geöffnet werden. AetherBoy läuft stumm weiter.",
                "Audio nicht verfügbar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

    }
}
