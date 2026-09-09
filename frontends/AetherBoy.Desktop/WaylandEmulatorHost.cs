using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using AetherBoy.Runtime;
using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed class WaylandEmulatorHost : IDisposable
{
    private enum ControlCenterPage
    {
        Display,
        Audio,
        Input,
        Saves
    }

    private readonly record struct DialogSelection(string? Path, string? Error);

    private const int LogicalWidth = 1180;
    private const int LogicalHeight = 760;
    private const int GameAreaX = 370;
    private const int GameAreaY = 116;
    private const int GameAreaWidth = 704;
    private const int GameAreaHeight = 452;
    private const short AxisThreshold = 16_000;

    private readonly LinuxDesktopProfile desktop;
    private readonly LinuxFrontendOptions options = new();
    private readonly HashSet<SDL.Scancode> pressedKeys = new();
    private readonly ConcurrentQueue<DialogSelection> dialogSelections = new();
    private readonly SDL.DialogFileCallback fileDialogCallback;
    private readonly IntPtr window;
    private readonly IntPtr renderer;

    private EmulationSession? session;
    private SdlAudioOutput? audioOutput;
    private IntPtr frameTexture;
    private IntPtr gamepad;
    private int[] framePixels = Array.Empty<int>();
    private VideoGeometry frameGeometry = VideoGeometry.GameBoy;
    private long displayedFrameSequence;
    private GameBoyButtons postedButtons;
    private GameBoyAdvanceButtons postedAdvanceButtons;
    private string? romPath;
    private string statusMessage = "OPEN OR DROP A ROM";
    private string? audioError;
    private bool running = true;
    private bool isFullscreen;
    private bool controlCenterVisible;
    private bool resumeAfterControlCenter;
    private ControlCenterPage controlCenterPage;
    private int fileDialogOpen;
    private bool disposed;

    public WaylandEmulatorHost(LinuxDesktopProfile desktop)
    {
        this.desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
        fileDialogCallback = OnFileDialogCompleted;
        if (!SDL.CreateWindowAndRenderer(
                $"AetherBoy · {desktop.DisplayName}",
                LogicalWidth,
                LogicalHeight,
                SDL.WindowFlags.Resizable | SDL.WindowFlags.HighPixelDensity,
                out window,
                out renderer))
        {
            throw new InvalidOperationException($"Wayland window creation failed: {SDL.GetError()}");
        }

        SDL.SetRenderLogicalPresentation(
            renderer,
            LogicalWidth,
            LogicalHeight,
            SDL.RendererLogicalPresentation.Letterbox);
        SDL.SetRenderVSync(renderer, 1);
        OpenFirstAvailableGamepad();
    }

    public int Run(string[] args)
    {
        if (args.Length > 1)
        {
            throw new ArgumentException("Usage: AetherBoy.Desktop [game.gb|game.gbc|game.gba]");
        }

        if (args.Length == 1)
        {
            TryLoadRom(args[0]);
        }

        while (running)
        {
            while (SDL.PollEvent(out SDL.Event currentEvent))
            {
                HandleEvent(in currentEvent);
            }

            DrainDialogSelections();
            UpdateEmulation();
            DrawShell();
            SDL.RenderPresent(renderer);
            SDL.Delay(2);
        }

        return session?.Fault is null ? 0 : 1;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CloseSession();
        audioOutput?.Dispose();
        audioOutput = null;
        if (gamepad != IntPtr.Zero)
        {
            SDL.CloseGamepad(gamepad);
            gamepad = IntPtr.Zero;
        }

        if (frameTexture != IntPtr.Zero)
        {
            SDL.DestroyTexture(frameTexture);
            frameTexture = IntPtr.Zero;
        }

        SDL.DestroyRenderer(renderer);
        SDL.DestroyWindow(window);
    }

    private void HandleEvent(in SDL.Event currentEvent)
    {
        SDL.EventType type = (SDL.EventType)currentEvent.Type;
        switch (type)
        {
            case SDL.EventType.Quit:
                running = false;
                break;
            case SDL.EventType.KeyDown:
            case SDL.EventType.KeyUp:
                HandleKeyboard(currentEvent.Key, type == SDL.EventType.KeyDown);
                break;
            case SDL.EventType.MouseButtonDown:
                SDL.Event logicalEvent = currentEvent;
                SDL.ConvertEventToRenderCoordinates(renderer, ref logicalEvent);
                if (logicalEvent.Button.Button == SDL.ButtonLeft)
                {
                    HandleMouseClick(logicalEvent.Button.X, logicalEvent.Button.Y);
                }
                break;
            case SDL.EventType.GamepadAdded:
                if (gamepad == IntPtr.Zero)
                {
                    TryOpenGamepad(currentEvent.GDevice.Which);
                }
                break;
            case SDL.EventType.GamepadRemoved:
                if (gamepad != IntPtr.Zero && SDL.GetGamepadID(gamepad) == currentEvent.GDevice.Which)
                {
                    SDL.CloseGamepad(gamepad);
                    gamepad = IntPtr.Zero;
                    statusMessage = "GAMEPAD DISCONNECTED";
                }
                break;
            case SDL.EventType.DropFile:
                string? droppedPath = Marshal.PtrToStringUTF8(currentEvent.Drop.Data);
                if (!string.IsNullOrWhiteSpace(droppedPath))
                {
                    TryLoadRom(droppedPath);
                }
                break;
        }
    }

    private void HandleKeyboard(SDL.KeyboardEvent keyEvent, bool isPressed)
    {
        if (isPressed)
        {
            pressedKeys.Add(keyEvent.Scancode);
        }
        else
        {
            pressedKeys.Remove(keyEvent.Scancode);
        }

        if (keyEvent.Scancode == SDL.Scancode.Tab && session is not null && !controlCenterVisible)
        {
            session.SetTurboAsync(isPressed).GetAwaiter().GetResult();
        }

        if (!isPressed || keyEvent.Repeat)
        {
            return;
        }

        switch (keyEvent.Scancode)
        {
            case SDL.Scancode.Escape:
                if (controlCenterVisible)
                {
                    CloseControlCenter();
                }
                else
                {
                    running = false;
                }
                break;
            case SDL.Scancode.O:
                ShowRomDialog();
                break;
            case SDL.Scancode.C:
                ToggleControlCenter();
                break;
            case SDL.Scancode.Space when session is not null && !controlCenterVisible:
                bool pause = !session.LatestSnapshot.IsPaused;
                session.SetPausedAsync(pause).GetAwaiter().GetResult();
                statusMessage = pause ? "PAUSED" : "RUNNING";
                break;
            case SDL.Scancode.F5:
                QuickSave();
                break;
            case SDL.Scancode.F7:
                Rewind();
                break;
            case SDL.Scancode.F8:
                QuickLoad();
                break;
            case SDL.Scancode.F11:
                isFullscreen = !isFullscreen;
                SDL.SetWindowFullscreen(window, isFullscreen);
                break;
            case SDL.Scancode.Alpha1:
                SelectSaveSlot(1);
                break;
            case SDL.Scancode.Alpha2:
                SelectSaveSlot(2);
                break;
            case SDL.Scancode.Alpha3:
                SelectSaveSlot(3);
                break;
            case SDL.Scancode.Alpha4:
                SelectSaveSlot(4);
                break;
            case SDL.Scancode.Alpha5:
                SelectSaveSlot(5);
                break;
        }
    }

    private void HandleMouseClick(float x, float y)
    {
        if (!controlCenterVisible)
        {
            if (Hit(x, y, 58, 500, 170, 36))
            {
                ShowRomDialog();
            }
            else if (Hit(x, y, 58, 546, 170, 36))
            {
                ToggleControlCenter();
            }

            return;
        }

        if (Hit(x, y, 1042, 78, 54, 32))
        {
            CloseControlCenter();
            return;
        }

        if (Hit(x, y, 354, 124, 160, 36)) controlCenterPage = ControlCenterPage.Display;
        else if (Hit(x, y, 524, 124, 160, 36)) controlCenterPage = ControlCenterPage.Audio;
        else if (Hit(x, y, 694, 124, 160, 36)) controlCenterPage = ControlCenterPage.Input;
        else if (Hit(x, y, 864, 124, 160, 36)) controlCenterPage = ControlCenterPage.Saves;
        else HandleControlCenterAction(x, y);
    }

    private void HandleControlCenterAction(float x, float y)
    {
        switch (controlCenterPage)
        {
            case ControlCenterPage.Display:
                if (Hit(x, y, 390, 230, 180, 42)) SetVideoFilter(LinuxVideoFilter.Sharp);
                else if (Hit(x, y, 584, 230, 180, 42)) SetVideoFilter(LinuxVideoFilter.Smooth);
                else if (Hit(x, y, 778, 230, 180, 42)) SetVideoFilter(LinuxVideoFilter.LcdGrid);
                else if (Hit(x, y, 390, 340, 100, 40)) SetFrameskip(0);
                else if (Hit(x, y, 504, 340, 100, 40)) SetFrameskip(1);
                else if (Hit(x, y, 618, 340, 100, 40)) SetFrameskip(2);
                else
                {
                    for (int index = 0; index < 5; index++)
                    {
                        if (Hit(x, y, 390 + (index * 114), 450, 100, 40))
                        {
                            SetPalette(index);
                            break;
                        }
                    }
                }
                break;

            case ControlCenterPage.Audio:
                if (Hit(x, y, 390, 224, 220, 44))
                {
                    options.AudioEnabled = !options.AudioEnabled;
                    TryUiAction(ApplyEmulatorConfiguration, options.AudioEnabled ? "AUDIO ENABLED" : "AUDIO MUTED");
                }
                else if (Hit(x, y, 390, 326, 72, 40))
                {
                    options.AudioVolume = Math.Max(0, options.AudioVolume - 25);
                    TryUiAction(ApplyEmulatorConfiguration, $"VOLUME {options.AudioVolume}%");
                }
                else if (Hit(x, y, 650, 326, 72, 40))
                {
                    options.AudioVolume = Math.Min(100, options.AudioVolume + 25);
                    TryUiAction(ApplyEmulatorConfiguration, $"VOLUME {options.AudioVolume}%");
                }
                else
                {
                    for (int index = 0; index < 4; index++)
                    {
                        if (Hit(x, y, 390 + (index * 146), 450, 132, 40))
                        {
                            ToggleAudioChannel(index);
                            break;
                        }
                    }
                }
                break;

            case ControlCenterPage.Input:
                break;

            case ControlCenterPage.Saves:
                for (int index = 0; index < 5; index++)
                {
                    if (Hit(x, y, 390 + (index * 114), 232, 100, 42))
                    {
                        SelectSaveSlot(index + 1);
                        return;
                    }
                }

                if (Hit(x, y, 390, 342, 180, 44)) QuickSave();
                else if (Hit(x, y, 584, 342, 180, 44)) QuickLoad();
                else if (Hit(x, y, 778, 342, 180, 44)) Rewind();
                break;
        }
    }

    private void ToggleControlCenter()
    {
        if (controlCenterVisible)
        {
            CloseControlCenter();
            return;
        }

        controlCenterVisible = true;
        pressedKeys.Clear();
        EmulationSession? currentSession = session;
        if (currentSession is not null)
        {
            currentSession.SetTurboAsync(false).GetAwaiter().GetResult();
            resumeAfterControlCenter = !currentSession.LatestSnapshot.IsPaused;
            if (resumeAfterControlCenter)
            {
                currentSession.SetPausedAsync(true).GetAwaiter().GetResult();
            }
        }
    }

    private void CloseControlCenter()
    {
        controlCenterVisible = false;
        EmulationSession? currentSession = session;
        if (resumeAfterControlCenter && currentSession is not null)
        {
            currentSession.SetPausedAsync(false).GetAwaiter().GetResult();
        }

        resumeAfterControlCenter = false;
    }

    private void SetVideoFilter(LinuxVideoFilter filter)
    {
        options.VideoFilter = filter;
        if (frameTexture != IntPtr.Zero)
        {
            SDL.SetTextureScaleMode(frameTexture, options.TextureScaleMode);
        }

        statusMessage = $"VIDEO FILTER · {filter.ToString().ToUpperInvariant()}";
    }

    private void SetFrameskip(int frameskip)
    {
        options.Frameskip = Math.Clamp(frameskip, 0, 2);
        TryUiAction(ApplyEmulatorConfiguration, $"FRAMESKIP {options.Frameskip}");
    }

    private void SetPalette(int paletteIndex)
    {
        options.PaletteIndex = Math.Clamp(paletteIndex, 0, 4);
        if (session is not null)
        {
            TryUiAction(
                () => session.SetPaletteAsync(options.PaletteIndex).GetAwaiter().GetResult(),
                $"DMG PALETTE {options.PaletteIndex + 1}");
        }
    }

    private void ToggleAudioChannel(int index)
    {
        switch (index)
        {
            case 0: options.Channel1Enabled = !options.Channel1Enabled; break;
            case 1: options.Channel2Enabled = !options.Channel2Enabled; break;
            case 2: options.Channel3Enabled = !options.Channel3Enabled; break;
            case 3: options.Channel4Enabled = !options.Channel4Enabled; break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }

        TryUiAction(ApplyEmulatorConfiguration, $"AUDIO CHANNEL {index + 1} UPDATED");
    }

    private void TryUiAction(Action action, string successMessage)
    {
        try
        {
            action();
            statusMessage = successMessage;
        }
        catch (Exception exception)
        {
            statusMessage = $"SETTING FAILED · {exception.Message}";
            Console.Error.WriteLine(exception);
        }
    }

    private static bool Hit(
        float pointX,
        float pointY,
        float x,
        float y,
        float width,
        float height) =>
        pointX >= x && pointX <= x + width && pointY >= y && pointY <= y + height;

    private void UpdateEmulation()
    {
        EmulationSession? currentSession = session;
        if (currentSession is null)
        {
            return;
        }

        SessionState sessionState = currentSession.State;
        if (sessionState == SessionState.Starting)
        {
            // The owner thread configures the frame exchange before it publishes
            // the first snapshot. Waiting for Running keeps a transient GBA
            // geometry from being copied into the initial Game Boy-sized buffer.
            return;
        }

        if (sessionState == SessionState.Faulted)
        {
            statusMessage = "CORE FAULT · SEE TERMINAL";
            Console.Error.WriteLine(currentSession.Fault);
            running = false;
            return;
        }

        PostInput(currentSession);
        EmulationSnapshot snapshot = currentSession.LatestSnapshot;
        EnsureFrameTexture(snapshot.VideoGeometry);
        if (currentSession.TryCopyLatestFrame(framePixels, ref displayedFrameSequence))
        {
            ReadOnlySpan<byte> pixels = MemoryMarshal.AsBytes(framePixels.AsSpan());
            if (!SDL.UpdateTexture(
                    frameTexture,
                    IntPtr.Zero,
                    pixels,
                    frameGeometry.Width * sizeof(int)))
            {
                throw new InvalidOperationException($"Could not upload emulator frame: {SDL.GetError()}");
            }
        }
    }

    private void PostInput(EmulationSession currentSession)
    {
        GameBoyButtons buttons = ReadButtons();
        if (buttons != postedButtons)
        {
            currentSession.SetButtonsAsync(buttons).GetAwaiter().GetResult();
            postedButtons = buttons;
        }

        GameBoyAdvanceButtons advanceButtons = ReadAdvanceButtons();
        if (advanceButtons != postedAdvanceButtons)
        {
            currentSession.SetGameBoyAdvanceButtonsAsync(advanceButtons).GetAwaiter().GetResult();
            postedAdvanceButtons = advanceButtons;
        }
    }

    private GameBoyButtons ReadButtons()
    {
        GameBoyButtons buttons = GameBoyButtons.None;
        if (controlCenterVisible)
        {
            return buttons;
        }

        AddKey(SDL.Scancode.Up, GameBoyButtons.Up, ref buttons);
        AddKey(SDL.Scancode.Down, GameBoyButtons.Down, ref buttons);
        AddKey(SDL.Scancode.Left, GameBoyButtons.Left, ref buttons);
        AddKey(SDL.Scancode.Right, GameBoyButtons.Right, ref buttons);
        AddKey(SDL.Scancode.Z, GameBoyButtons.A, ref buttons);
        AddKey(SDL.Scancode.X, GameBoyButtons.B, ref buttons);
        AddKey(SDL.Scancode.Backspace, GameBoyButtons.Select, ref buttons);
        AddKey(SDL.Scancode.Return, GameBoyButtons.Start, ref buttons);

        if (gamepad == IntPtr.Zero || !SDL.GamepadConnected(gamepad))
        {
            return buttons;
        }

        AddGamepadButton(SDL.GamepadButton.DPadUp, GameBoyButtons.Up, ref buttons);
        AddGamepadButton(SDL.GamepadButton.DPadDown, GameBoyButtons.Down, ref buttons);
        AddGamepadButton(SDL.GamepadButton.DPadLeft, GameBoyButtons.Left, ref buttons);
        AddGamepadButton(SDL.GamepadButton.DPadRight, GameBoyButtons.Right, ref buttons);
        AddGamepadButton(SDL.GamepadButton.South, GameBoyButtons.A, ref buttons);
        AddGamepadButton(SDL.GamepadButton.East, GameBoyButtons.B, ref buttons);
        AddGamepadButton(SDL.GamepadButton.Back, GameBoyButtons.Select, ref buttons);
        AddGamepadButton(SDL.GamepadButton.Start, GameBoyButtons.Start, ref buttons);

        short x = SDL.GetGamepadAxis(gamepad, SDL.GamepadAxis.LeftX);
        short y = SDL.GetGamepadAxis(gamepad, SDL.GamepadAxis.LeftY);
        if (x < -AxisThreshold) buttons |= GameBoyButtons.Left;
        if (x > AxisThreshold) buttons |= GameBoyButtons.Right;
        if (y < -AxisThreshold) buttons |= GameBoyButtons.Up;
        if (y > AxisThreshold) buttons |= GameBoyButtons.Down;
        return buttons;
    }

    private GameBoyAdvanceButtons ReadAdvanceButtons()
    {
        GameBoyAdvanceButtons buttons = GameBoyAdvanceButtons.None;
        if (controlCenterVisible)
        {
            return buttons;
        }

        if (pressedKeys.Contains(SDL.Scancode.Q) ||
            (gamepad != IntPtr.Zero && SDL.GetGamepadButton(gamepad, SDL.GamepadButton.LeftShoulder)))
        {
            buttons |= GameBoyAdvanceButtons.L;
        }

        if (pressedKeys.Contains(SDL.Scancode.E) ||
            (gamepad != IntPtr.Zero && SDL.GetGamepadButton(gamepad, SDL.GamepadButton.RightShoulder)))
        {
            buttons |= GameBoyAdvanceButtons.R;
        }

        return buttons;
    }

    private void LoadRom(string candidate)
    {
        string path = Path.GetFullPath(candidate);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("ROM file was not found.", path);
        }

        string extension = Path.GetExtension(path);
        if (!extension.Equals(".gb", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".gbc", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".gba", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Only .gb, .gbc and .gba files are supported.");
        }

        CloseSession();
        bool audioReady = EnsureAudioOutput();
        EmulatorConfiguration configuration = options.CreateEmulatorConfiguration(audioReady);
        session = new EmulationSession(
            path,
            Path.ChangeExtension(path, "sav"),
            bootRom: null,
            configuration,
            options.PaletteIndex);
        if (audioReady)
        {
            session.AudioSamplesAvailable += OnAudioSamplesAvailable;
        }

        if (controlCenterVisible)
        {
            session.SetPausedAsync(true).GetAwaiter().GetResult();
            resumeAfterControlCenter = true;
        }

        romPath = path;
        displayedFrameSequence = 0;
        postedButtons = GameBoyButtons.None;
        postedAdvanceButtons = GameBoyAdvanceButtons.None;
        statusMessage = $"RUNNING · SLOT {options.SaveSlot} · F5 SAVE · F8 LOAD";
        SDL.SetWindowTitle(window, $"AetherBoy · {Path.GetFileNameWithoutExtension(path)}");
    }

    private void CloseSession()
    {
        if (session is not null)
        {
            session.AudioSamplesAvailable -= OnAudioSamplesAvailable;
            session.Dispose();
            session = null;
        }

        audioOutput?.Clear();
    }

    private void TryLoadRom(string candidate)
    {
        try
        {
            LoadRom(candidate);
        }
        catch (Exception exception)
        {
            statusMessage = $"ROM ERROR · {exception.Message}";
            Console.Error.WriteLine(exception.Message);
        }
    }

    private bool EnsureAudioOutput()
    {
        if (!options.AudioEnabled)
        {
            return false;
        }

        if (audioOutput is not null)
        {
            audioOutput.Enabled = true;
            return true;
        }

        try
        {
            audioOutput = new SdlAudioOutput(
                LinuxFrontendOptions.SampleRate,
                options.AudioVolume / 100f);
            audioError = null;
            Console.WriteLine($"AetherBoy audio backend: {audioOutput.DriverName}");
            return true;
        }
        catch (Exception exception)
        {
            audioError = exception.Message;
            Console.Error.WriteLine($"Audio unavailable; continuing silently: {exception.Message}");
            return false;
        }
    }

    private void OnAudioSamplesAvailable(
        object? sender,
        AudioSamplesAvailableEventArgs eventArgs)
    {
        SdlAudioOutput? output = audioOutput;
        if (output is null)
        {
            return;
        }

        try
        {
            output.Submit(eventArgs.GetSamplesCopy(), eventArgs.SampleRate);
        }
        catch (Exception exception)
        {
            audioError = exception.Message;
            Console.Error.WriteLine($"SDL audio submission failed: {exception.Message}");
        }
    }

    private void ApplyEmulatorConfiguration()
    {
        bool audioReady = options.AudioEnabled && EnsureAudioOutput();
        if (audioOutput is not null)
        {
            audioOutput.Volume = options.AudioVolume / 100f;
            audioOutput.Enabled = audioReady;
        }

        EmulationSession? currentSession = session;
        if (currentSession is not null)
        {
            currentSession.ConfigureAsync(options.CreateEmulatorConfiguration(audioReady))
                .GetAwaiter()
                .GetResult();
            if (audioReady)
            {
                currentSession.AudioSamplesAvailable -= OnAudioSamplesAvailable;
                currentSession.AudioSamplesAvailable += OnAudioSamplesAvailable;
            }
        }
    }

    private void ShowRomDialog()
    {
        if (Interlocked.CompareExchange(ref fileDialogOpen, 1, 0) != 0)
        {
            return;
        }

        statusMessage = "WAITING FOR WAYLAND FILE PORTAL";
        string? defaultLocation = romPath is null ? null : Path.GetDirectoryName(romPath);
        try
        {
            // SDL's dialog is asynchronous and uses XDG Desktop Portal on Linux.
            // No native filter array is passed because its unmanaged lifetime must
            // extend until the callback; extension validation remains authoritative.
            SDL.ShowOpenFileDialog(
                fileDialogCallback,
                IntPtr.Zero,
                window,
                filters: null,
                nfilters: 0,
                defaultLocation,
                allowMany: false);
        }
        catch (Exception exception)
        {
            Interlocked.Exchange(ref fileDialogOpen, 0);
            statusMessage = $"PORTAL ERROR · {exception.Message}";
        }
    }

    private void OnFileDialogCompleted(IntPtr userdata, IntPtr fileList, int filter)
    {
        try
        {
            if (fileList == IntPtr.Zero)
            {
                dialogSelections.Enqueue(new DialogSelection(null, SDL.GetError()));
                return;
            }

            IntPtr firstPath = Marshal.ReadIntPtr(fileList);
            if (firstPath != IntPtr.Zero)
            {
                dialogSelections.Enqueue(new DialogSelection(
                    Marshal.PtrToStringUTF8(firstPath),
                    null));
            }
            else
            {
                dialogSelections.Enqueue(new DialogSelection(null, null));
            }
        }
        finally
        {
            Interlocked.Exchange(ref fileDialogOpen, 0);
        }
    }

    private void DrainDialogSelections()
    {
        while (dialogSelections.TryDequeue(out DialogSelection selection))
        {
            if (!string.IsNullOrWhiteSpace(selection.Error))
            {
                statusMessage = $"PORTAL ERROR · {selection.Error}";
            }
            else if (!string.IsNullOrWhiteSpace(selection.Path))
            {
                TryLoadRom(selection.Path);
            }
            else
            {
                statusMessage = session is null ? "OPEN OR DROP A ROM" : "RUNNING";
            }
        }
    }

    private void EnsureFrameTexture(VideoGeometry geometry)
    {
        if (frameTexture != IntPtr.Zero && frameGeometry == geometry)
        {
            return;
        }

        if (frameTexture != IntPtr.Zero)
        {
            SDL.DestroyTexture(frameTexture);
        }

        frameTexture = SDL.CreateTexture(
            renderer,
            SDL.PixelFormat.ARGB8888,
            SDL.TextureAccess.Streaming,
            geometry.Width,
            geometry.Height);
        if (frameTexture == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Could not create frame texture: {SDL.GetError()}");
        }

        SDL.SetTextureScaleMode(frameTexture, options.TextureScaleMode);
        frameGeometry = geometry;
        framePixels = new int[geometry.PixelCount];
        displayedFrameSequence = 0;
    }

    private void DrawShell()
    {
        SDL.SetRenderDrawColor(renderer, 5, 7, 18, 255);
        SDL.RenderClear(renderer);

        Fill(28, 28, 238, 704, 18, 21, 42);
        Fill(28, 28, 238, 5, 116, 69, 255);
        Fill(156, 28, 110, 5, 0, 210, 255);
        Fill(292, 28, 860, 704, 11, 14, 30);
        Fill(346, 92, 752, 500, 31, 37, 67);
        Fill(GameAreaX, GameAreaY, GameAreaWidth, GameAreaHeight, 8, 10, 22);
        Fill(346, 626, 752, 54, 17, 21, 40);
        Fill(346, 626, 752, 3, desktop.IsHyprland ? (byte)0 : (byte)193, 82, 255);

        Text(58, 68, "AETHERBOY", 240, 242, 255);
        Text(58, 88, "GAME BOY LAB", 126, 133, 166);
        Text(58, 154, "NATIVE WAYLAND", 0, 210, 255);
        Text(58, 174, desktop.DisplayName, 178, 151, 255);
        Text(58, 240, "KEYBOARD", 126, 133, 166);
        Text(58, 262, "ARROWS  MOVE", 218, 222, 242);
        Text(58, 280, "Z / X   A / B", 218, 222, 242);
        Text(58, 298, "ENTER   START", 218, 222, 242);
        Text(58, 316, "BKSP    SELECT", 218, 222, 242);
        Text(58, 334, "Q / E   L / R", 218, 222, 242);
        Text(58, 376, "SPACE   PAUSE", 218, 222, 242);
        Text(58, 394, "TAB     TURBO", 218, 222, 242);
        Text(58, 412, "F5/F8   SAVE/LOAD", 218, 222, 242);
        Text(58, 430, "F7      REWIND", 218, 222, 242);
        Text(58, 448, "1-5     SLOT", 218, 222, 242);
        Text(58, 466, "F11     FULL", 218, 222, 242);
        DrawButton(58, 500, 170, 36, "OPEN ROM [O]", false);
        DrawButton(58, 546, 170, 36, "CONTROL [C]", controlCenterVisible);
        Text(58, 608, $"SLOT {options.SaveSlot}", 178, 151, 255);
        Text(58, 628, options.AudioEnabled ? "AUDIO ON" : "AUDIO MUTED", 126, 133, 166);

        EmulationSnapshot? snapshot = session?.LatestSnapshot;
        string system = snapshot?.VideoGeometry == VideoGeometry.GameBoyAdvance ? "GBA" : "GB / GBC";
        Text(370, 58, $"SIGNAL // {system}", 126, 133, 166);

        if (frameTexture != IntPtr.Zero && snapshot?.HasVideoFrame == true)
        {
            SDL.FRect destination = GetGameDestination(frameGeometry);
            SDL.RenderTexture(renderer, frameTexture, IntPtr.Zero, in destination);
            if (options.VideoFilter == LinuxVideoFilter.LcdGrid)
            {
                DrawLcdGrid(in destination, frameGeometry);
            }
        }
        else
        {
            Text(484, 326, "OPEN [O] OR DROP A ROM", 178, 151, 255);
            Text(510, 348, ".GB / .GBC / .GBA", 126, 133, 166);
        }

        Text(370, 648, Truncate(statusMessage, 84), 218, 222, 242);
        string gamepadStatus = gamepad == IntPtr.Zero
            ? "GAMEPAD: WAITING"
            : $"GAMEPAD: {SDL.GetGamepadName(gamepad) ?? "CONNECTED"}";
        string audioStatus = audioOutput is null
            ? options.AudioEnabled
                ? session is null ? "AUDIO: READY ON ROM LOAD" : "AUDIO: UNAVAILABLE"
                : "AUDIO: MUTED"
            : $"AUDIO: {audioOutput.DriverName} · {options.AudioVolume}%";
        Text(370, 664, Truncate($"{gamepadStatus}  //  {audioStatus}", 84), 126, 133, 166);

        if (controlCenterVisible)
        {
            DrawControlCenter();
        }
    }

    private SDL.FRect GetGameDestination(VideoGeometry geometry)
    {
        float scale = options.VideoFilter == LinuxVideoFilter.Smooth
            ? Math.Min(
                GameAreaWidth / (float)geometry.Width,
                GameAreaHeight / (float)geometry.Height)
            : Math.Max(
                1,
                Math.Min(GameAreaWidth / geometry.Width, GameAreaHeight / geometry.Height));
        float width = geometry.Width * scale;
        float height = geometry.Height * scale;
        return new SDL.FRect
        {
            X = GameAreaX + (GameAreaWidth - width) / 2f,
            Y = GameAreaY + (GameAreaHeight - height) / 2f,
            W = width,
            H = height
        };
    }

    private void DrawLcdGrid(in SDL.FRect destination, VideoGeometry geometry)
    {
        float xStep = destination.W / geometry.Width;
        float yStep = destination.H / geometry.Height;
        if (xStep < 2f || yStep < 2f)
        {
            return;
        }

        SDL.SetRenderDrawBlendMode(renderer, SDL.BlendMode.Blend);
        SDL.SetRenderDrawColor(renderer, 2, 4, 12, 48);
        for (int row = 1; row < geometry.Height; row++)
        {
            float y = destination.Y + (row * yStep);
            SDL.RenderLine(renderer, destination.X, y, destination.X + destination.W, y);
        }

        for (int column = 1; column < geometry.Width; column++)
        {
            float x = destination.X + (column * xStep);
            SDL.RenderLine(renderer, x, destination.Y, x, destination.Y + destination.H);
        }

        SDL.SetRenderDrawBlendMode(renderer, SDL.BlendMode.None);
    }

    private void DrawControlCenter()
    {
        SDL.SetRenderDrawBlendMode(renderer, SDL.BlendMode.Blend);
        FillAlpha(296, 28, 856, 704, 3, 5, 14, 238);
        SDL.SetRenderDrawBlendMode(renderer, SDL.BlendMode.None);
        Fill(320, 58, 802, 642, 18, 21, 42);
        Fill(320, 58, 802, 5, 116, 69, 255);
        Fill(720, 58, 402, 5, 0, 210, 255);
        Text(354, 86, "AETHER CONTROL CENTER", 240, 242, 255);
        Text(354, 102, "LINUX / NATIVE WAYLAND", 126, 133, 166);
        DrawButton(1042, 78, 54, 32, "X", false);

        DrawButton(354, 124, 160, 36, "DISPLAY", controlCenterPage == ControlCenterPage.Display);
        DrawButton(524, 124, 160, 36, "AUDIO", controlCenterPage == ControlCenterPage.Audio);
        DrawButton(694, 124, 160, 36, "INPUT", controlCenterPage == ControlCenterPage.Input);
        DrawButton(864, 124, 160, 36, "SAVES", controlCenterPage == ControlCenterPage.Saves);

        switch (controlCenterPage)
        {
            case ControlCenterPage.Display: DrawDisplayPage(); break;
            case ControlCenterPage.Audio: DrawAudioPage(); break;
            case ControlCenterPage.Input: DrawInputPage(); break;
            case ControlCenterPage.Saves: DrawSavesPage(); break;
        }

        Text(354, 666, "C / ESC CLOSES · SESSION PAUSED WHILE OPEN", 126, 133, 166);
    }

    private void DrawDisplayPage()
    {
        Text(390, 198, "VIDEO FILTER", 126, 133, 166);
        DrawButton(390, 230, 180, 42, "SHARP", options.VideoFilter == LinuxVideoFilter.Sharp);
        DrawButton(584, 230, 180, 42, "SMOOTH", options.VideoFilter == LinuxVideoFilter.Smooth);
        DrawButton(778, 230, 180, 42, "LCD GRID", options.VideoFilter == LinuxVideoFilter.LcdGrid);

        Text(390, 310, "FRAMESKIP", 126, 133, 166);
        for (int value = 0; value <= 2; value++)
        {
            DrawButton(390 + (value * 114), 340, 100, 40, value.ToString(), options.Frameskip == value);
        }

        Text(390, 420, "DMG PALETTE", 126, 133, 166);
        for (int index = 0; index < 5; index++)
        {
            DrawButton(
                390 + (index * 114),
                450,
                100,
                40,
                $"P{index + 1}",
                options.PaletteIndex == index);
        }
    }

    private void DrawAudioPage()
    {
        Text(390, 194, "NATIVE SDL3 AUDIO", 126, 133, 166);
        DrawButton(
            390,
            224,
            220,
            44,
            options.AudioEnabled ? "AUDIO ON" : "AUDIO MUTED",
            options.AudioEnabled && audioOutput is not null);

        Text(390, 302, "MASTER VOLUME", 126, 133, 166);
        DrawButton(390, 326, 72, 40, "-", false);
        Text(516, 340, $"{options.AudioVolume}%", 240, 242, 255);
        DrawButton(650, 326, 72, 40, "+", false);

        Text(390, 420, "HARDWARE CHANNELS", 126, 133, 166);
        bool[] channels =
        {
            options.Channel1Enabled,
            options.Channel2Enabled,
            options.Channel3Enabled,
            options.Channel4Enabled
        };
        for (int index = 0; index < channels.Length; index++)
        {
            DrawButton(390 + (index * 146), 450, 132, 40, $"CH {index + 1}", channels[index]);
        }

        string backend = audioOutput is null ? "NOT OPEN" : audioOutput.DriverName;
        Text(390, 536, $"BACKEND  {Truncate(backend, 56)}", 218, 222, 242);
        if (!string.IsNullOrWhiteSpace(audioError))
        {
            Text(390, 558, Truncate($"LAST ERROR  {audioError}", 80), 255, 132, 156);
        }
    }

    private void DrawInputPage()
    {
        Text(390, 198, "KEYBOARD MATRIX", 126, 133, 166);
        Text(390, 230, "ARROWS  D-PAD       Z / X  A / B", 218, 222, 242);
        Text(390, 252, "ENTER   START       BKSP   SELECT", 218, 222, 242);
        Text(390, 274, "Q / E   GBA L / R   TAB    TURBO", 218, 222, 242);

        Text(390, 338, "SDL3 GAMEPAD", 126, 133, 166);
        string controller = gamepad == IntPtr.Zero
            ? "WAITING FOR HOT-PLUG"
            : SDL.GetGamepadName(gamepad) ?? "CONNECTED";
        Text(390, 370, Truncate(controller, 70), 178, 151, 255);
        Text(390, 398, "D-PAD / LEFT STICK · SOUTH A · EAST B", 218, 222, 242);
        Text(390, 420, "BACK SELECT · START START · SHOULDERS L/R", 218, 222, 242);
        Text(390, 492, "REMAPPING + PERSISTENCE IS THE NEXT INPUT STEP", 126, 133, 166);
    }

    private void DrawSavesPage()
    {
        Text(390, 198, "ACTIVE SAVE-STATE SLOT", 126, 133, 166);
        for (int index = 0; index < 5; index++)
        {
            int slot = index + 1;
            bool exists = romPath is not null && File.Exists(LinuxSaveStateStore.GetPath(romPath, slot));
            DrawButton(
                390 + (index * 114),
                232,
                100,
                42,
                exists ? $"{slot} ·" : slot.ToString(),
                options.SaveSlot == slot);
        }

        Text(390, 312, "TIMELINE", 126, 133, 166);
        DrawButton(390, 342, 180, 44, "SAVE [F5]", false);
        DrawButton(584, 342, 180, 44, "LOAD [F8]", false);
        DrawButton(778, 342, 180, 44, "REWIND [F7]", false);

        Text(390, 438, "BATTERY SAVE", 126, 133, 166);
        EmulationSnapshot? snapshot = session?.LatestSnapshot;
        string battery = snapshot?.Rom?.BatterySave.IsEnabled == true
            ? $"ACTIVE · {snapshot.Rom.BatterySave.ExpectedLength} BYTES"
            : session is null ? "NO ROM LOADED" : "CARTRIDGE HAS NO BATTERY RAM";
        Text(390, 470, battery, 218, 222, 242);
        Text(390, 514, "STATES .SS1-.SS5 AND BATTERY .SAV LIVE NEXT TO ROM", 126, 133, 166);
    }

    private void DrawButton(
        float x,
        float y,
        float width,
        float height,
        string label,
        bool selected)
    {
        Fill(x, y, width, height, selected ? (byte)66 : (byte)31, selected ? (byte)47 : (byte)37, selected ? (byte)112 : (byte)67);
        Fill(x, y, width, 3, selected ? (byte)0 : (byte)84, selected ? (byte)210 : (byte)91, 255);
        float textX = x + Math.Max(10f, (width - (label.Length * 8f)) / 2f);
        Text(textX, y + ((height - 8f) / 2f), label, 232, 235, 250);
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..Math.Max(0, maximumLength - 3)] + "...";

    private void QuickSave()
    {
        if (session is null || romPath is null)
        {
            return;
        }

        try
        {
            byte[] state = session.CaptureStateAsync().GetAwaiter().GetResult();
            LinuxSaveStateStore.WriteAtomic(romPath, options.SaveSlot, state);
            statusMessage = $"STATE SAVED · SLOT {options.SaveSlot}";
        }
        catch (Exception exception)
        {
            statusMessage = $"SAVE FAILED · {exception.Message}";
            Console.Error.WriteLine(exception);
        }
    }

    private void QuickLoad()
    {
        if (session is null || romPath is null)
        {
            return;
        }

        string path = LinuxSaveStateStore.GetPath(romPath, options.SaveSlot);
        if (!File.Exists(path))
        {
            statusMessage = $"NO STATE IN SLOT {options.SaveSlot}";
            return;
        }

        try
        {
            session.RestoreStateAsync(LinuxSaveStateStore.Read(romPath, options.SaveSlot))
                .GetAwaiter()
                .GetResult();
            audioOutput?.Clear();
            displayedFrameSequence = 0;
            statusMessage = $"STATE LOADED · SLOT {options.SaveSlot}";
        }
        catch (Exception exception)
        {
            statusMessage = $"LOAD FAILED · {exception.Message}";
            Console.Error.WriteLine(exception);
        }
    }

    private void Rewind()
    {
        if (session is null)
        {
            return;
        }

        try
        {
            if (!session.RewindAsync().GetAwaiter().GetResult())
            {
                statusMessage = "REWIND BUFFER IS EMPTY";
                return;
            }

            audioOutput?.Clear();
            displayedFrameSequence = 0;
            statusMessage = "REWOUND · ONE STEP";
        }
        catch (Exception exception)
        {
            statusMessage = $"REWIND FAILED · {exception.Message}";
            Console.Error.WriteLine(exception);
        }
    }

    private void SelectSaveSlot(int slot)
    {
        options.SelectSaveSlot(slot);
        statusMessage = $"SAVE STATE SLOT {slot}";
    }

    private void OpenFirstAvailableGamepad()
    {
        uint[]? gamepads = SDL.GetGamepads(out int count);
        if (gamepads is { Length: > 0 } && count > 0)
        {
            TryOpenGamepad(gamepads[0]);
        }
    }

    private void TryOpenGamepad(uint instanceId)
    {
        IntPtr candidate = SDL.OpenGamepad(instanceId);
        if (candidate == IntPtr.Zero)
        {
            statusMessage = $"GAMEPAD ERROR · {SDL.GetError()}";
            return;
        }

        gamepad = candidate;
        statusMessage = $"GAMEPAD READY · {SDL.GetGamepadName(gamepad) ?? "SDL3"}";
    }

    private void AddKey(SDL.Scancode key, GameBoyButtons button, ref GameBoyButtons buttons)
    {
        if (pressedKeys.Contains(key))
        {
            buttons |= button;
        }
    }

    private void AddGamepadButton(
        SDL.GamepadButton source,
        GameBoyButtons target,
        ref GameBoyButtons buttons)
    {
        if (SDL.GetGamepadButton(gamepad, source))
        {
            buttons |= target;
        }
    }

    private void Fill(
        float x,
        float y,
        float width,
        float height,
        byte red,
        byte green,
        byte blue)
    {
        SDL.SetRenderDrawColor(renderer, red, green, blue, 255);
        SDL.FRect rectangle = new() { X = x, Y = y, W = width, H = height };
        SDL.RenderFillRect(renderer, in rectangle);
    }

    private void FillAlpha(
        float x,
        float y,
        float width,
        float height,
        byte red,
        byte green,
        byte blue,
        byte alpha)
    {
        SDL.SetRenderDrawColor(renderer, red, green, blue, alpha);
        SDL.FRect rectangle = new() { X = x, Y = y, W = width, H = height };
        SDL.RenderFillRect(renderer, in rectangle);
    }

    private void Text(float x, float y, string value, byte red, byte green, byte blue)
    {
        SDL.SetRenderDrawColor(renderer, red, green, blue, 255);
        SDL.RenderDebugText(renderer, x, y, value);
    }
}
