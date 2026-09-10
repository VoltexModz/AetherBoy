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
    private const int GameAreaX = 292;
    private const int GameAreaY = 144;
    private const int GameAreaWidth = 856;
    private const int GameAreaHeight = 480;
    private const short AxisThreshold = 16_000;

    private readonly LinuxDesktopProfile desktop;
    private readonly LinuxFrontendOptions options;
    private readonly string settingsPath;
    private bool settingsDirty;
    private long settingsChangedAt;
    private LinuxInputAction? rebindingAction;
    private int focusedBinding;
    private bool draggingVolume;
    private static readonly LinuxInputAction[] BindingActions =
    [
        LinuxInputAction.A, LinuxInputAction.B, LinuxInputAction.L, LinuxInputAction.R,
        LinuxInputAction.Start, LinuxInputAction.Select,
        LinuxInputAction.Up, LinuxInputAction.Down, LinuxInputAction.Left, LinuxInputAction.Right,
        LinuxInputAction.Turbo, LinuxInputAction.Pause,
    ];
    private readonly HashSet<SDL.Scancode> pressedKeys = new();
    private readonly ConcurrentQueue<DialogSelection> dialogSelections = new();
    private readonly SDL.DialogFileCallback fileDialogCallback;
    private readonly IntPtr window;
    private readonly IntPtr renderer;

    private readonly SdlTextRenderer textRenderer;
    private EmulationSession? pendingSession;
    private string? pendingRomPath;
    private bool resumeAfterLoad;
    private string? loadError;
    private bool windowFocused = true;
    private float mouseX = -1;
    private float mouseY = -1;
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

    public WaylandEmulatorHost(LinuxDesktopProfile desktop, string? settingsPath = null)
    {
        this.desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
        this.settingsPath = settingsPath ?? LinuxSettingsStore.DefaultPath;
        options = LinuxSettingsStore.Load(this.settingsPath, out string? settingsError);
        loadError = settingsError;
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
        textRenderer = new SdlTextRenderer(renderer);
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
                try { HandleEvent(in currentEvent); }
                catch (Exception exception) { ReportError(exception); }
            }

            DrainDialogSelections();
            CompletePendingLoad();
            try { UpdateEmulation(); }
            catch (Exception exception) { ReportError(exception); }
            FlushSettingsIfDue();
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
        FlushSettingsIfDue(force: true);
        DisposeSession(pendingSession);
        pendingSession = null;
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

        textRenderer.Dispose();
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
            case SDL.EventType.MouseButtonUp:
                if (currentEvent.Button.Button == SDL.ButtonLeft)
                {
                    draggingVolume = false;
                    FlushSettingsIfDue(force: true);
                }
                break;
            case SDL.EventType.MouseMotion:
                SDL.Event motion = currentEvent;
                SDL.ConvertEventToRenderCoordinates(renderer, ref motion);
                mouseX = motion.Motion.X;
                mouseY = motion.Motion.Y;
                if (draggingVolume) SetVolumeFromPointer(mouseX);
                break;
            case SDL.EventType.WindowMouseLeave:
                mouseX = mouseY = -1;
                break;
            case SDL.EventType.WindowFocusLost:
                windowFocused = false;
                draggingVolume = false;
                rebindingAction = null;
                FlushSettingsIfDue(force: true);
                pressedKeys.Clear();
                if (session is not null && session.State is SessionState.Running or SessionState.Paused)
                {
                    session.SetTurboAsync(false).GetAwaiter().GetResult();
                    PostInput(session);
                }
                break;
            case SDL.EventType.WindowFocusGained:
                windowFocused = true;
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
        if (rebindingAction is { } action)
        {
            pressedKeys.Clear();
            if (!isPressed || keyEvent.Repeat) return;
            if (keyEvent.Scancode == SDL.Scancode.Escape)
            {
                rebindingAction = null;
                statusMessage = "Key change cancelled.";
            }
            else if (!LinuxKeyBindings.CanBind(keyEvent.Scancode))
            {
                statusMessage = "That key is reserved for the app. Choose another, or Esc to cancel.";
            }
            else
            {
                options.Keys.Bind(action, keyEvent.Scancode);
                rebindingAction = null;
                MarkSettingsChanged();
                statusMessage = $"{action} is now {KeyLabel(keyEvent.Scancode)}. Occupied keys are swapped.";
            }
            return;
        }

        if (controlCenterVisible && isPressed)
        {
            if (controlCenterPage == ControlCenterPage.Audio)
            {
                switch (keyEvent.Scancode)
                {
                    case SDL.Scancode.Left: SetVolume(options.AudioVolume - 1); return;
                    case SDL.Scancode.Right: SetVolume(options.AudioVolume + 1); return;
                    case SDL.Scancode.Home: SetVolume(0); return;
                    case SDL.Scancode.End: SetVolume(100); return;
                }
            }
            if (controlCenterPage == ControlCenterPage.Input)
            {
                switch (keyEvent.Scancode)
                {
                    case SDL.Scancode.Up: focusedBinding = (focusedBinding + 11) % 12; return;
                    case SDL.Scancode.Down: focusedBinding = (focusedBinding + 1) % 12; return;
                    case SDL.Scancode.Left:
                    case SDL.Scancode.Right: focusedBinding = (focusedBinding + 6) % 12; return;
                    case SDL.Scancode.Return when !keyEvent.Repeat:
                        BeginRebinding(focusedBinding); return;
                }
            }
        }
        if (isPressed)
        {
            pressedKeys.Add(keyEvent.Scancode);
        }
        else
        {
            pressedKeys.Remove(keyEvent.Scancode);
        }

        if (keyEvent.Scancode == options.Keys[LinuxInputAction.Turbo] && session is not null && !controlCenterVisible && pendingSession is null)
        {
            session.SetTurboAsync(isPressed).GetAwaiter().GetResult();
        }

        if (!isPressed || keyEvent.Repeat)
        {
            return;
        }

        if (keyEvent.Scancode == options.Keys[LinuxInputAction.Pause] && !controlCenterVisible)
        {
            TogglePause();
            return;
        }

        switch (keyEvent.Scancode)
        {
            case SDL.Scancode.Escape:
                if (controlCenterVisible)
                {
                    CloseControlCenter();
                }
                else if (loadError is not null)
                {
                    loadError = null;
                }
                else if (isFullscreen)
                {
                    isFullscreen = false;
                    SDL.SetWindowFullscreen(window, false);
                }
                break;
            case SDL.Scancode.O:
                ShowRomDialog();
                break;
            case SDL.Scancode.C:
                ToggleControlCenter();
                break;
            case SDL.Scancode.Tab when controlCenterVisible:
                SelectControlCenterPage((ControlCenterPage)(((int)controlCenterPage + 1) % 4));
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
            if (Hit(x, y, 24, 134, 216, 46)) ShowRomDialog();
            else if (Hit(x, y, 24, 192, 216, 44)) ToggleControlCenter();
            else if (Hit(x, y, 292, 88, 144, 40)) TogglePause();
            else if (Hit(x, y, 448, 88, 144, 40)) QuickSave();
            else if (Hit(x, y, 604, 88, 144, 40)) QuickLoad();
            else if (Hit(x, y, 1004, 88, 144, 40))
            {
                isFullscreen = !isFullscreen;
                SDL.SetWindowFullscreen(window, isFullscreen);
            }
            else if (session is null && Hit(x, y, 596, 396, 248, 46)) ShowRomDialog();
            return;
        }

        if (Hit(x, y, 1042, 78, 54, 32))
        {
            CloseControlCenter();
            return;
        }

        if (Hit(x, y, 354, 124, 160, 36)) SelectControlCenterPage(ControlCenterPage.Display);
        else if (Hit(x, y, 524, 124, 160, 36)) SelectControlCenterPage(ControlCenterPage.Audio);
        else if (Hit(x, y, 694, 124, 160, 36)) SelectControlCenterPage(ControlCenterPage.Input);
        else if (Hit(x, y, 864, 124, 160, 36)) SelectControlCenterPage(ControlCenterPage.Saves);
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
                    MarkSettingsChanged();
                    TryUiAction(ApplyEmulatorConfiguration, options.AudioEnabled ? "AUDIO ENABLED" : "AUDIO MUTED");
                }
                else if (Hit(x, y, 390, 326, 72, 40))
                {
                    SetVolume(options.AudioVolume - 1);
                }
                else if (Hit(x, y, 650, 326, 72, 40))
                {
                    SetVolume(options.AudioVolume + 1);
                }
                else if (Hit(x, y, 390, 376, 570, 32))
                {
                    draggingVolume = true;
                    SetVolumeFromPointer(x);
                }
                else
                {
                    for (int index = 0; index < 4; index++)
                    {
                        if (Hit(x, y, 390 + (index * 146), 480, 132, 40))
                        {
                            ToggleAudioChannel(index);
                            break;
                        }
                    }
                }
                break;

            case ControlCenterPage.Input:
                for (int index = 0; index < BindingActions.Length; index++)
                {
                    float buttonX = 514 + (index / 6) * 330;
                    float buttonY = 246 + (index % 6) * 46;
                    if (Hit(x, y, buttonX, buttonY, 156, 36))
                    {
                        BeginRebinding(index);
                        return;
                    }
                }
                if (Hit(x, y, 390, 548, 210, 40))
                {
                    options.Keys = new LinuxKeyBindings();
                    rebindingAction = null;
                    pressedKeys.Clear();
                    MarkSettingsChanged();
                    statusMessage = "Default keys restored.";
                }
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

    private void TogglePause()
    {
        if (session is null || pendingSession is not null || controlCenterVisible) return;
        bool pause = !session.LatestSnapshot.IsPaused;
        session.SetPausedAsync(pause).GetAwaiter().GetResult();
        audioOutput?.Clear();
        statusMessage = pause ? $"Paused. Press {KeyLabel(options.Keys[LinuxInputAction.Pause])} to resume." : "Playing";
    }

    private void ToggleControlCenter()
    {
        if (pendingSession is not null) return;
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
        rebindingAction = null;
        draggingVolume = false;
        pressedKeys.Clear();
        FlushSettingsIfDue(force: true);
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
            ReportError(currentSession.Fault ?? new InvalidOperationException("The emulator stopped."));
            CloseSession();
            romPath = null;
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
        if (controlCenterVisible || !windowFocused || fileDialogOpen != 0 || pendingSession is not null)
        {
            return buttons;
        }

        AddKey(options.Keys[LinuxInputAction.Up], GameBoyButtons.Up, ref buttons);
        AddKey(options.Keys[LinuxInputAction.Down], GameBoyButtons.Down, ref buttons);
        AddKey(options.Keys[LinuxInputAction.Left], GameBoyButtons.Left, ref buttons);
        AddKey(options.Keys[LinuxInputAction.Right], GameBoyButtons.Right, ref buttons);
        AddKey(options.Keys[LinuxInputAction.A], GameBoyButtons.A, ref buttons);
        AddKey(options.Keys[LinuxInputAction.B], GameBoyButtons.B, ref buttons);
        AddKey(options.Keys[LinuxInputAction.Select], GameBoyButtons.Select, ref buttons);
        AddKey(options.Keys[LinuxInputAction.Start], GameBoyButtons.Start, ref buttons);

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
        if (controlCenterVisible || !windowFocused || fileDialogOpen != 0 || pendingSession is not null)
        {
            return buttons;
        }

        if (pressedKeys.Contains(options.Keys[LinuxInputAction.L]) ||
            (gamepad != IntPtr.Zero && SDL.GetGamepadButton(gamepad, SDL.GamepadButton.LeftShoulder)))
        {
            buttons |= GameBoyAdvanceButtons.L;
        }

        if (pressedKeys.Contains(options.Keys[LinuxInputAction.R]) ||
            (gamepad != IntPtr.Zero && SDL.GetGamepadButton(gamepad, SDL.GamepadButton.RightShoulder)))
        {
            buttons |= GameBoyAdvanceButtons.R;
        }

        return buttons;
    }

    private void TryLoadRom(string candidate)
    {
        if (pendingSession is not null) return;
        try
        {
            string path = LinuxRomPath.Resolve(candidate);
            if (path == romPath && session is not null)
            {
                loadError = null;
                statusMessage = "This ROM is already open.";
                return;
            }
            loadError = null;
            pressedKeys.Clear();
            resumeAfterLoad = session is not null && !session.LatestSnapshot.IsPaused;
            if (session is not null)
            {
                session.SetTurboAsync(false).GetAwaiter().GetResult();
                session.SetPausedAsync(true).GetAwaiter().GetResult();
                // The vendored CPU has static instruction scratch registers.
                // Finish any partial instruction before another owner starts;
                // the old session can then safely resume if loading fails.
                if (session.LatestSnapshot.Rom?.IsGameBoyAdvance == true)
                    _ = session.CaptureStateAsync().GetAwaiter().GetResult();
            }
            audioOutput?.Clear();
            pendingRomPath = path;
            pendingSession = new EmulationSession(path, Path.ChangeExtension(path, "sav"),
                bootRom: null, options.CreateEmulatorConfiguration(EnsureAudioOutput()), options.PaletteIndex);
            statusMessage = "Loading " + Path.GetFileName(path) + "...";
        }
        catch (Exception exception)
        {
            ResumeAfterFailedLoad();
            ReportError(exception);
        }
    }

    private void CompletePendingLoad()
    {
        EmulationSession? candidate = pendingSession;
        if (candidate is null || candidate.State == SessionState.Starting) return;
        pendingSession = null;
        if (candidate.State == SessionState.Faulted)
        {
            ReportError(candidate.Fault ?? new InvalidOperationException("The ROM could not be started."));
            DisposeSession(candidate);
            ResumeAfterFailedLoad();
            pendingRomPath = null;
            return;
        }
        CloseSession();
        session = candidate;
        romPath = pendingRomPath;
        pendingRomPath = null;
        resumeAfterLoad = false;
        if (audioOutput is not null) session.AudioSamplesAvailable += OnAudioSamplesAvailable;
        if (controlCenterVisible)
        {
            session.SetPausedAsync(true).GetAwaiter().GetResult();
            resumeAfterControlCenter = true;
        }
        displayedFrameSequence = 0;
        postedButtons = GameBoyButtons.None;
        postedAdvanceButtons = GameBoyAdvanceButtons.None;
        statusMessage = "Playing. Battery saves are stored automatically.";
        SDL.SetWindowTitle(window, $"AetherBoy · {Path.GetFileNameWithoutExtension(romPath)}");
    }

    private void ResumeAfterFailedLoad()
    {
        if (resumeAfterLoad && session is not null && session.State == SessionState.Paused)
            session.SetPausedAsync(false).GetAwaiter().GetResult();
        resumeAfterLoad = false;
    }

    private void ReportError(Exception exception)
    {
        loadError = exception.GetBaseException().Message;
        statusMessage = "Could not complete the action. Open another ROM to try again.";
        Console.Error.WriteLine(exception);
    }

    private static void DisposeSession(EmulationSession? current)
    {
        if (current is null) return;
        try { current.Dispose(); }
        catch (Exception exception)
        {
            // Faults have already been reported by the load/update path.
            if (current.Fault is null) Console.Error.WriteLine(exception);
        }
    }

    private void CloseSession()
    {
        EmulationSession? previous = session;
        session = null;
        if (previous is not null)
        {
            previous.AudioSamplesAvailable -= OnAudioSamplesAvailable;
            DisposeSession(previous);
        }
        audioOutput?.Clear();
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
        if (pendingSession is not null) return;
        if (Interlocked.CompareExchange(ref fileDialogOpen, 1, 0) != 0)
        {
            return;
        }

        pressedKeys.Clear();
        if (session is not null) session.SetTurboAsync(false).GetAwaiter().GetResult();
        statusMessage = "Choose a ROM in the file picker. You can also drop a file here.";
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
            ReportError(exception);
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
                ReportError(new IOException(selection.Error));
            }
            else if (!string.IsNullOrWhiteSpace(selection.Path))
            {
                TryLoadRom(selection.Path);
            }
            else
            {
                statusMessage = "File picker closed. Drop a ROM here or press O to choose one.";
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
        SDL.SetRenderDrawColor(renderer, 12, 15, 23, 255);
        SDL.RenderClear(renderer);
        Fill(0, 0, 264, LogicalHeight, 19, 23, 34);
        Fill(263, 0, 1, LogicalHeight, 43, 49, 66);
        Fill(24, 34, 6, 40, 176, 158, 245);
        Label(44, 30, "AetherBoy", 25);
        Text(44, 66, "Your pocket arcade.", 161, 173, 192);
        DrawButton(24, 134, 216, 46, fileDialogOpen != 0 ? "File picker open..." : "Open ROM   [O]", true,
            pendingSession is null && fileDialogOpen == 0);
        DrawButton(24, 192, 216, 44, "Settings   [C]", false, pendingSession is null);

        Text(24, 288, "PLAY CONTROLS", 176, 158, 245);
        DrawKeyHint(328, $"{BindingLabel(LinuxInputAction.Up)}/{BindingLabel(LinuxInputAction.Down)}", "Move (see Input)");
        DrawKeyHint(366, $"{BindingLabel(LinuxInputAction.A)} / {BindingLabel(LinuxInputAction.B)}", "A / B");
        DrawKeyHint(404, BindingLabel(LinuxInputAction.Start), "Start");
        DrawKeyHint(442, BindingLabel(LinuxInputAction.Select), "Select");
        DrawKeyHint(480, $"{BindingLabel(LinuxInputAction.L)} / {BindingLabel(LinuxInputAction.R)}", "L / R");
        DrawKeyHint(518, BindingLabel(LinuxInputAction.Turbo), "Hold for turbo");
        Fill(24, 580, 216, 1, 43, 49, 66);
        Text(24, 604, "F7  Rewind    1–5  Save slot", 161, 173, 192);
        Text(24, 641, gamepad == IntPtr.Zero ? "Keyboard ready" : "Controller connected", 126, 214, 174);
        Text(24, 707, "GB  /  GBC  /  GBA", 161, 173, 192);

        EmulationSnapshot? snapshot = session?.LatestSnapshot;
        string title = pendingRomPath is not null ? Path.GetFileNameWithoutExtension(pendingRomPath)
            : romPath is not null ? Path.GetFileNameWithoutExtension(romPath) : "Ready when you are.";
        Label(292, 27, textRenderer.Fit(title, 650, 24), 24);
        string system = snapshot?.VideoGeometry == VideoGeometry.GameBoyAdvance ? "GAME BOY ADVANCE" : "GAME BOY / COLOR";
        Text(292, 60, session is null ? "Open a cartridge and make yourself at home." : system, 161, 173, 192);
        string state = pendingSession is not null ? "LOADING" : session is null ? "NO ROM"
            : snapshot?.IsPaused == true ? "PAUSED" : "PLAYING";
        Text(1040, 42, state, 126, 214, 174);
        bool playable = session is not null && pendingSession is null;
        DrawButton(292, 88, 144, 40, $"{(snapshot?.IsPaused == true ? "Resume" : "Pause")} [{BindingLabel(LinuxInputAction.Pause)}]", false, playable);
        DrawButton(448, 88, 144, 40, "Save [F5]", false, playable);
        DrawButton(604, 88, 144, 40, "Load [F8]", false, playable);
        DrawButton(1004, 88, 144, 40, "Full screen [F11]", false);

        Fill(292, 144, 856, 480, 6, 8, 13);
        if (frameTexture != IntPtr.Zero && snapshot?.HasVideoFrame == true)
        {
            SDL.FRect destination = GetGameDestination(frameGeometry);
            SDL.RenderTexture(renderer, frameTexture, IntPtr.Zero, in destination);
            if (options.VideoFilter == LinuxVideoFilter.LcdGrid) DrawLcdGrid(in destination, frameGeometry);
        }
        else
        {
            // A small cartridge silhouette keeps the empty state about the game.
            Fill(682, 244, 76, 86, 40, 45, 65);
            Fill(694, 256, 52, 34, 176, 158, 245);
            Fill(700, 308, 40, 14, 18, 22, 32);
            Label(546, 348, "Your next adventure awaits.", 22);
            DrawButton(596, 396, 248, 46, "Choose a ROM", true, pendingSession is null);
            Text(559, 463, "or drop a .gb, .gbc or .gba file here", 161, 173, 192);
        }
        if (pendingSession is not null)
        {
            Fill(442, 328, 556, 112, 27, 32, 47);
            Label(466, 347, "Loading cartridge...", 22);
            Text(466, 388, "Preparing the game and its save data.", 190, 199, 215);
        }
        else if (snapshot?.IsPaused == true && !controlCenterVisible)
        {
            Fill(648, 350, 144, 58, 27, 32, 47);
            Label(678, 365, "Paused", 20);
        }
        if (loadError is not null)
        {
            Fill(292, 638, 856, 96, 55, 30, 40);
            Text(308, 646, "Action failed · " + textRenderer.Fit(loadError, 700), 255, 187, 193);
            Text(308, 674, session is null ? "Open another ROM to try again. Details are in the terminal."
                : "Your previous game is still open. Details are in the terminal.", 236, 205, 209);
            Text(308, 704, "Esc  Dismiss", 236, 205, 209);
        }
        else
        {
            Text(292, 644, textRenderer.Fit(statusMessage, 850), 212, 220, 234);
            Fill(292, 686, 856, 1, 43, 49, 66);
            Text(292, 704, $"Slot {options.SaveSlot} / 5    ·    {options.VideoFilter}", 161, 173, 192);
            string audio = !options.AudioEnabled ? "Muted" : audioOutput is null
                ? session is null ? "Audio ready" : "Audio unavailable" : $"Volume {options.AudioVolume}%";
            Text(620, 704, audio, 161, 173, 192);
            Text(906, 704, "Native Wayland", 161, 173, 192);
        }
        if (controlCenterVisible) DrawControlCenter();
    }

    private void Label(float x, float y, string value, int size) =>
        textRenderer.Draw(x, y, value, 236, 240, 248, size);

    private void DrawKeyHint(float y, string key, string action)
    {
        Fill(24, y, 88, 28, 31, 37, 51);
        textRenderer.Draw(32, y + 4, textRenderer.Fit(key, 72, 12), 212, 220, 234, 12);
        textRenderer.Draw(124, y + 3, textRenderer.Fit(action, 116, 13), 185, 197, 215, 13);
    }

    private static string KeyLabel(SDL.Scancode scan) =>
        SDL.GetKeyName(SDL.GetKeyFromScancode(scan, SDL.Keymod.None, false)) ?? SDL.GetScancodeName(scan);

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

        Text(354, 620, textRenderer.Fit(loadError ?? statusMessage, 720),
            loadError is null ? (byte)212 : (byte)255, 190, 205);
        Text(354, 666, "Tab: next section   ·   C / Esc: return to game", 161, 173, 192);
    }

    private void DrawDisplayPage()
    {
        Text(390, 198, "VIDEO FILTER", 161, 173, 192);
        DrawButton(390, 230, 180, 42, "SHARP", options.VideoFilter == LinuxVideoFilter.Sharp);
        DrawButton(584, 230, 180, 42, "SMOOTH", options.VideoFilter == LinuxVideoFilter.Smooth);
        DrawButton(778, 230, 180, 42, "LCD GRID", options.VideoFilter == LinuxVideoFilter.LcdGrid);

        Text(390, 310, "FRAMESKIP", 161, 173, 192);
        for (int value = 0; value <= 2; value++)
        {
            DrawButton(390 + (value * 114), 340, 100, 40, value.ToString(), options.Frameskip == value);
        }

        Text(390, 420, "DMG PALETTE", 161, 173, 192);
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
        Text(390, 194, "NATIVE SDL3 AUDIO", 161, 173, 192);
        DrawButton(
            390,
            224,
            220,
            44,
            options.AudioEnabled ? "AUDIO ON" : "AUDIO MUTED",
            options.AudioEnabled && audioOutput is not null);

        Text(390, 302, "MASTER VOLUME", 161, 173, 192);
        DrawButton(390, 326, 72, 40, "-1%", false, options.AudioVolume > 0);
        Text(516, 340, $"{options.AudioVolume}%", 240, 242, 255);
        DrawButton(650, 326, 72, 40, "+1%", false, options.AudioVolume < 100);
        Fill(390, 389, 570, 6, 52, 61, 80);
        Fill(390, 389, options.AudioVolume * 5.7f, 6, 176, 158, 245);
        Fill(390 + options.AudioVolume * 5.7f - 7, 381, 14, 22, 216, 204, 255);
        Text(390, 410, "0%", 161, 173, 192);
        Text(450, 410, "Drag, or use Left / Right for 1% steps.", 161, 173, 192);
        Text(928, 410, "100%", 161, 173, 192);

        Text(390, 452, "HARDWARE CHANNELS", 161, 173, 192);
        bool[] channels =
        {
            options.Channel1Enabled,
            options.Channel2Enabled,
            options.Channel3Enabled,
            options.Channel4Enabled
        };
        for (int index = 0; index < channels.Length; index++)
        {
            DrawButton(390 + (index * 146), 480, 132, 40, $"CH {index + 1}", channels[index]);
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
        Text(390, 188, "KEYBOARD CONTROLS", 176, 158, 245);
        Text(390, 216, "Click a key, or use arrows + Enter. Esc cancels a change.", 161, 173, 192);
        for (int index = 0; index < BindingActions.Length; index++)
        {
            LinuxInputAction action = BindingActions[index];
            float x = 390 + (index / 6) * 330;
            float y = 246 + (index % 6) * 46;
            Text(x, y + 8, action is LinuxInputAction.L or LinuxInputAction.R ? $"{action} (GBA)" : action.ToString(), 218, 222, 242);
            DrawButton(x + 124, y, 156, 36,
                rebindingAction == action ? "Press a key..." : BindingLabel(action),
                rebindingAction == action || (rebindingAction is null && focusedBinding == index));
        }
        DrawButton(390, 548, 210, 40, "Reset keyboard defaults", false);
        string controller = gamepad == IntPtr.Zero ? "No controller connected" : SDL.GetGamepadName(gamepad) ?? "Controller connected";
        Text(626, 548, textRenderer.Fit(controller, 380), 161, 173, 192);
        Text(626, 570, "Controller uses the standard layout.", 161, 173, 192);
    }

    private string BindingLabel(LinuxInputAction action) => KeyLabel(options.Keys[action]);

    private void BeginRebinding(int index)
    {
        focusedBinding = index;
        rebindingAction = BindingActions[index];
        pressedKeys.Clear();
        loadError = null;
        statusMessage = $"Press a new key for {rebindingAction}. Esc cancels; occupied keys swap.";
    }

    private void SelectControlCenterPage(ControlCenterPage page)
    {
        rebindingAction = null;
        draggingVolume = false;
        controlCenterPage = page;
        FlushSettingsIfDue(force: true);
    }

    private void SetVolumeFromPointer(float x) => SetVolume((int)Math.Round((x - 390) / 5.7f));

    private void SetVolume(int percent)
    {
        int previous = options.AudioVolume;
        options.SetVolume(percent);
        if (previous == options.AudioVolume) return;
        MarkSettingsChanged();
        TryUiAction(() =>
        {
            if (audioOutput is not null) audioOutput.Volume = options.AudioVolume / 100f;
        }, $"Volume {options.AudioVolume}%");
    }

    private void MarkSettingsChanged()
    {
        settingsDirty = true;
        settingsChangedAt = Environment.TickCount64;
    }

    private void FlushSettingsIfDue(bool force = false)
    {
        if (!settingsDirty || (!force && Environment.TickCount64 - settingsChangedAt < 300)) return;
        settingsDirty = false;
        try { LinuxSettingsStore.Save(settingsPath, options); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ReportError(new IOException("Preferences could not be saved: " + exception.Message, exception));
        }
    }

    private void DrawSavesPage()
    {
        Text(390, 198, "ACTIVE SAVE-STATE SLOT", 161, 173, 192);
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

        Text(390, 312, "TIMELINE", 161, 173, 192);
        DrawButton(390, 342, 180, 44, "SAVE [F5]", false, session is not null);
        DrawButton(584, 342, 180, 44, "LOAD [F8]", false, session is not null);
        DrawButton(778, 342, 180, 44, "REWIND [F7]", false, session is not null);

        Text(390, 438, "BATTERY SAVE", 161, 173, 192);
        EmulationSnapshot? snapshot = session?.LatestSnapshot;
        string battery = snapshot?.Rom?.BatterySave.IsEnabled == true
            ? $"ACTIVE · {snapshot.Rom.BatterySave.ExpectedLength} BYTES"
            : session is null ? "NO ROM LOADED" : "CARTRIDGE HAS NO BATTERY RAM";
        Text(390, 470, battery, 218, 222, 242);
        Text(390, 514, "STATES .SS1-.SS5 AND BATTERY .SAV LIVE NEXT TO ROM", 161, 173, 192);
    }

    private void DrawButton(
        float x, float y, float width, float height, string label, bool selected, bool enabled = true)
    {
        bool hovered = enabled && Hit(mouseX, mouseY, x, y, width, height);
        if (selected && enabled) Fill(x, y, width, height, hovered ? (byte)198 : (byte)176, hovered ? (byte)182 : (byte)158, 245);
        else Fill(x, y, width, height, hovered ? (byte)49 : (byte)29, hovered ? (byte)57 : (byte)35, hovered ? (byte)77 : (byte)49);
        if (hovered) Fill(x, y + height - 2, width, 2, 176, 158, 245);
        int size = 13;
        label = textRenderer.Fit(label, width - 16, size);
        float textX = x + Math.Max(8, (width - textRenderer.Measure(label, size)) / 2);
        byte r = !enabled ? (byte)121 : selected ? (byte)22 : (byte)222;
        byte g = !enabled ? (byte)133 : selected ? (byte)20 : (byte)228;
        byte b = !enabled ? (byte)151 : selected ? (byte)37 : (byte)241;
        textRenderer.Draw(textX, y + (height - 19) / 2, label, r, g, b, size);
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..Math.Max(0, maximumLength - 3)] + "...";

    private void QuickSave()
    {
        if (session is null || romPath is null || pendingSession is not null)
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
        if (session is null || romPath is null || pendingSession is not null)
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
        textRenderer.Draw(x, y, textRenderer.Fit(value, LogicalWidth - x - 24), red, green, blue);
    }
}
