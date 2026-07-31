using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.IO;
using nanoboy.Core;

using System.Threading;
using System.Diagnostics;

using OpenTK.Graphics.OpenGL;

namespace nanoboy
{
    public partial class frmNano : Form
    {

        private Nanoboy nano;
        private NanoboySettings settings;
        private Thread gamethread;
        private CancellationTokenSource gameCts;
        private frmAudioTool audiotoolwindow;
        private bool loadedgl;
        private int textureid = -1;
        private bool speedup = false;
        private bool glerror = false;
        private string currentRomPath;
        private RewindManager rewindManager = new RewindManager();
        private CheatEngine cheatEngine = new CheatEngine();
        private LinkCable linkCable = new LinkCable();
        private bool isRewinding = false;
        private static readonly bool SaveStatesAvailable = false;
        private static readonly bool RewindAvailable = false;
        private static readonly bool LinkCableAvailable = false;

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

        private void StopGameThread()
        {
            if (gameCts != null)
            {
                gameCts.Cancel();
                gamethread?.Join(500);
                gameCts.Dispose();
                gameCts = null;
                gamethread = null;
            }
        }

        private byte[] LoadBootROM(bool isColor)
        {
            string bootFileName = isColor ? "gbc_boot.bin" : "dmg_boot.bin";
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, bootFileName);
            if (File.Exists(bootFileName))
            {
                try { return File.ReadAllBytes(bootFileName); } catch { }
            }
            if (File.Exists(localPath))
            {
                try { return File.ReadAllBytes(localPath); } catch { }
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

            currentRomPath = path;
            AddRecentFile(path);

            updateTimer.Stop();
            StopGameThread();
            nano?.Dispose();

            if (RewindAvailable)
            {
                rewindManager.Clear();
            }
            ROM rom = new ROM(path, Path.ChangeExtension(path, "sav"));
            byte[] bootRom = LoadBootROM(rom.HasColorFeatures);

            nano = new Nanoboy(rom, bootRom);
            nano.SetSettings(settings);
            nano.Memory.Video.SetMonochromePalette(settings.PaletteIndex);

            if (audiotoolwindow != null) {
                audiotoolwindow.Nanoboy = nano;
            }

            gameCts = new CancellationTokenSource();
            var token = gameCts.Token;

            gamethread = new Thread(() => {
                Stopwatch stopwatch = new Stopwatch();
                while (!token.IsCancellationRequested) {
                    stopwatch.Restart();
                    if (RewindAvailable && isRewinding)
                    {
                        rewindManager.Rewind(nano);
                    }
                    else
                    {
                        nano.Frame();
                        if (RewindAvailable)
                        {
                            rewindManager.CaptureFrame(nano);
                        }
                        cheatEngine.ApplyCheats(nano.Memory);
                    }
                    stopwatch.Stop();
                    if (stopwatch.ElapsedMilliseconds < 16 && !speedup) {
                        int sleepMs = 16 - (int)stopwatch.ElapsedMilliseconds;
                        if (sleepMs > 0)
                            Thread.Sleep(sleepMs);
                    }
                }
            });
            gamethread.Priority = ThreadPriority.Highest;
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
            settings.DisplayFilterIndex = index;
            menuFilterSharp.Checked = index == 0;
            menuFilterSmooth.Checked = index == 1;
            menuFilterLCDGrid.Checked = index == 2;
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
        private Image ResizeImage(Image image, Size size, bool preserveAspectRatio = true)
        {
            int newWidth;
            int newHeight;
            if (preserveAspectRatio)
            {
                int originalWidth = image.Width;
                int originalHeight = image.Height;
                float percentWidth = (float)size.Width / (float)originalWidth;
                float percentHeight = (float)size.Height / (float)originalHeight;
                float percent = percentHeight < percentWidth ? percentHeight : percentWidth;
                newWidth = (int)(originalWidth * percent);
                newHeight = (int)(originalHeight * percent);
            }
            else
            {
                newWidth = size.Width;
                newHeight = size.Height;
            }
            Image newImage = new Bitmap(newWidth, newHeight);
            using (Graphics graphicsHandle = Graphics.FromImage(newImage))
            {
                graphicsHandle.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphicsHandle.DrawImage(image, 0, 0, newWidth, newHeight);
            }
            return newImage;
        }

        private void gameView_Load(object sender, EventArgs e)
        {
            loadedgl = true;
            GL.ClearColor(Color.Black);
            GL.Enable(EnableCap.Texture2D);
            SetupViewport();
        }

        private void SetupViewport()
        {
            int width = gameView.Width;
            int height = gameView.Height;
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(0, width, height, 0, -1, 1);
            GL.Viewport(0, 0, width, height);
        }

        private void updateTimer_Tick(object sender, EventArgs e)
        {
            PollGamepad();
            gameView.Refresh();
        }

        private OpenTK.Input.GamePadState lastPadState;
        private bool isKeyboardRewinding = false;

        private void PollGamepad()
        {
            if (nano == null) return;

            try
            {
                var padState = OpenTK.Input.GamePad.GetState(0);
                if (!padState.IsConnected) return;

                // GamePad Buttons
                SetOrUnsetKey(padState.Buttons.A == OpenTK.Input.ButtonState.Pressed, settings.KeyA);
                SetOrUnsetKey(padState.Buttons.B == OpenTK.Input.ButtonState.Pressed || padState.Buttons.X == OpenTK.Input.ButtonState.Pressed, settings.KeyB);
                SetOrUnsetKey(padState.Buttons.Start == OpenTK.Input.ButtonState.Pressed, settings.KeyStart);
                SetOrUnsetKey(padState.Buttons.Back == OpenTK.Input.ButtonState.Pressed, settings.KeySelect);

                // DPad & Left Thumbstick
                bool up = padState.DPad.Up == OpenTK.Input.ButtonState.Pressed || padState.ThumbSticks.Left.Y > 0.5f;
                bool down = padState.DPad.Down == OpenTK.Input.ButtonState.Pressed || padState.ThumbSticks.Left.Y < -0.5f;
                bool left = padState.DPad.Left == OpenTK.Input.ButtonState.Pressed || padState.ThumbSticks.Left.X < -0.5f;
                bool right = padState.DPad.Right == OpenTK.Input.ButtonState.Pressed || padState.ThumbSticks.Left.X > 0.5f;

                SetOrUnsetKey(up, settings.KeyUp);
                SetOrUnsetKey(down, settings.KeyDown);
                SetOrUnsetKey(left, settings.KeyLeft);
                SetOrUnsetKey(right, settings.KeyRight);

                // Rewind bleibt bis zur Korrektur des Save-State-Unterbaus deaktiviert.
                if (RewindAvailable && padState.Triggers.Left > 0.5f)
                {
                    isRewinding = true;
                    rewindManager.IsRewinding = true;
                }
                else if (!RewindAvailable || !isKeyboardRewinding)
                {
                    isRewinding = false;
                    rewindManager.IsRewinding = false;
                }

                // Save-State-Shortcuts bleiben deaktiviert, bis vollständige Zustände sicher sind.
                if (SaveStatesAvailable && padState.Buttons.RightShoulder == OpenTK.Input.ButtonState.Pressed && lastPadState.Buttons.RightShoulder == OpenTK.Input.ButtonState.Released)
                {
                    QuickSave();
                }
                if (SaveStatesAvailable && padState.Buttons.LeftShoulder == OpenTK.Input.ButtonState.Pressed && lastPadState.Buttons.LeftShoulder == OpenTK.Input.ButtonState.Released)
                {
                    QuickLoad();
                }

                lastPadState = padState;
            }
            catch { }
        }

        private void SetOrUnsetKey(bool isPressed, Keys key)
        {
            if (isPressed) nano.SetKey(key);
            else nano.UnsetKey(key);
        }

        private void gameView_Paint(object sender, PaintEventArgs e)
        {
            if (loadedgl && nano != null) {
                if (!glerror) {
                    GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                    GL.MatrixMode(MatrixMode.Modelview);
                    GL.LoadIdentity();
                    textureid = TextureFromArray(nano.Memory.Video.Screen, textureid);
                    GL.BindTexture(TextureTarget.Texture2D, textureid);
                    GL.Begin(PrimitiveType.Quads);
                    {
                        GL.TexCoord2(0f, 0f);
                        GL.Vertex2(0f, 0f);
                        GL.TexCoord2(1f, 0f);
                        GL.Vertex2(gameView.Width, 0f);
                        GL.TexCoord2(1f, 1f);
                        GL.Vertex2(gameView.Width, gameView.Height);
                        GL.TexCoord2(0f, 1f);
                        GL.Vertex2(0f, gameView.Height);
                    }
                    GL.End();

                    // Render LCD Grid lines overlay if LCD Grid filter is enabled
                    if (settings.DisplayFilterIndex == 2)
                    {
                        GL.Enable(EnableCap.Blend);
                        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                        GL.BindTexture(TextureTarget.Texture2D, 0);

                        GL.Color4(0.0f, 0.0f, 0.0f, 0.20f);
                        GL.Begin(PrimitiveType.Lines);

                        float colStep = (float)gameView.Width / 160f;
                        for (int x = 0; x <= 160; x++)
                        {
                            float posX = x * colStep;
                            GL.Vertex2(posX, 0f);
                            GL.Vertex2(posX, gameView.Height);
                        }

                        float rowStep = (float)gameView.Height / 144f;
                        for (int y = 0; y <= 144; y++)
                        {
                            float posY = y * rowStep;
                            GL.Vertex2(0f, posY);
                            GL.Vertex2(gameView.Width, posY);
                        }

                        GL.End();
                        GL.Color4(1.0f, 1.0f, 1.0f, 1.0f);
                        GL.Disable(EnableCap.Blend);
                    }

                    gameView.SwapBuffers();
                } else {
                    Bitmap screen_original = new Bitmap(160, 140, 160 * 4, System.Drawing.Imaging.PixelFormat.Format32bppArgb, nano.Memory.Video.Screen);
                    Image screen_resized = ResizeImage(screen_original, new Size(gameView.Width, gameView.Height), false);
                    e.Graphics.DrawImage(screen_resized, new Point(0, 0));
                }
            }
        }

        public int TextureFromArray(IntPtr arrayptr, int texturexid = -1)
        {
            int id = texturexid == -1 ? GL.GenTexture() : texturexid;
            GL.BindTexture(TextureTarget.Texture2D, id);

            var filterMode = settings.DisplayFilterIndex == 1 ? TextureMagFilter.Linear : TextureMagFilter.Nearest;
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)filterMode);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)filterMode);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 160, 144, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, arrayptr);

            if (GL.GetError() != ErrorCode.NoError) {
                glerror = true;
            }
            return id;
        }

        private void gameView_Resize(object sender, EventArgs e)
        {
            SetupViewport();
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
            StopGameThread();
            if (nano != null) {
                nano.Dispose();
            }
        }

        private void ResizeWindow(int size)
        {
            if (size <= 0) size = 2;
            int diffwidth = Math.Max(16, this.Width - gameView.Width);
            int diffheight = Math.Max(60, this.Height - gameView.Height);
            this.Size = new Size(160 * size + diffwidth, 144 * size + diffheight);
            settings.VideoScaleFactor = size;
            UpdateEmulatorSettings();
        }

        private void LoadConfiguration()
        {
            menuAudioC1.Checked = settings.Channel1Enable;
            menuAudioC2.Checked = settings.Channel2Enable;
            menuAudioC3.Checked = settings.Channel3Enable;
            menuAudioC4.Checked = settings.Channel4Enable;
            menuAudioQ1.Checked = settings.SampleRate == 0;
            menuAudioQ2.Checked = settings.SampleRate == 1;
            menuAudioQ3.Checked = settings.SampleRate == 2;
            menuAudioQ4.Checked = settings.SampleRate == 3;
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
            nano?.SetSettings(settings);
        }

    }
}
