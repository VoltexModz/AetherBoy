using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using AetherBoy.Runtime;
using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost : IDisposable
{
    private enum ControlCenterPage
    {
        Overview,
        Display,
        Audio,
        Input,
        Saves,
        System,
        Diagnostics
    }

    private readonly record struct DialogSelection(string? Path, string? Error);

    private int LogicalWidth = 1180;
    private int LogicalHeight = 760;
    private const int GameAreaX = 40;
    private const int GameAreaY = 134;
    private int GameAreaWidth => LogicalWidth - 376;
    private int GameAreaHeight => LogicalHeight - 264;
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

    public WaylandEmulatorHost(LinuxDesktopProfile desktop, string? settingsPath = null, bool hidden = false)
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
                SDL.WindowFlags.Resizable | SDL.WindowFlags.HighPixelDensity | (hidden ? SDL.WindowFlags.Hidden : 0),
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
        LoadBrandMark();
        SDL.SetWindowMinimumSize(window, 860, 554);
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

        if (brandTexture != IntPtr.Zero) SDL.DestroyTexture(brandTexture);
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
                    ReleaseMouseTurbo();
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
                mouseTurbo = false;
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
            if (keyEvent.Scancode == SDL.Scancode.Tab && !keyEvent.Repeat)
            {
                int direction = (keyEvent.Mod & SDL.Keymod.Shift) != 0 ? -1 : 1;
                if ((keyEvent.Mod & SDL.Keymod.Ctrl) != 0)
                    SelectControlCenterPage((ControlCenterPage)(((int)controlCenterPage + direction + 7) % 7));
                else if (focusTargets.Count > 0)
                    focusedControl = focusedControl < 0 ? (direction > 0 ? 0 : focusTargets.Count - 1)
                        : (focusedControl + direction + focusTargets.Count) % focusTargets.Count;
                return;
            }
            if (focusedControl >= 0 && focusedControl < focusTargets.Count &&
                keyEvent.Scancode is SDL.Scancode.Return or SDL.Scancode.Space && !keyEvent.Repeat)
            {
                SDL.FRect target = focusTargets[focusedControl];
                HandleMouseClick(target.X + target.W / 2, target.Y + target.H / 2);
                return;
            }
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
            session.SetTurboAsync(isPressed || mouseTurbo).GetAwaiter().GetResult();
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
        focusedControl = -1;
        foreach (var command in shellCommands)
        {
            if (Hit(x, y, command.Bounds.X, command.Bounds.Y, command.Bounds.W, command.Bounds.H))
            {
                command.Action();
                return;
            }
        }
        if (controlCenterVisible) HandleControlCenterAction(x, y);
    }

    private void HandleControlCenterAction(float x, float y)
    {
        switch (controlCenterPage)
        {
            case ControlCenterPage.Display:
                if (Hit(x, y, 300, 230, 180, 42)) SetVideoFilter(LinuxVideoFilter.Sharp);
                else if (Hit(x, y, 494, 230, 180, 42)) SetVideoFilter(LinuxVideoFilter.Smooth);
                else if (Hit(x, y, 688, 230, 180, 42)) SetVideoFilter(LinuxVideoFilter.LcdGrid);
                else if (Hit(x, y, 300, 340, 100, 40)) SetFrameskip(0);
                else if (Hit(x, y, 414, 340, 100, 40)) SetFrameskip(1);
                else if (Hit(x, y, 528, 340, 100, 40)) SetFrameskip(2);
                else
                {
                    for (int index = 0; index < 5; index++)
                    {
                        if (Hit(x, y, 300 + (index * 114), 450, 100, 40))
                        {
                            SetPalette(index);
                            break;
                        }
                    }
                }
                break;

            case ControlCenterPage.Audio:
                if (Hit(x, y, 300, 224, 220, 44))
                {
                    options.AudioEnabled = !options.AudioEnabled;
                    MarkSettingsChanged();
                    TryUiAction(ApplyEmulatorConfiguration, options.AudioEnabled ? "AUDIO ENABLED" : "AUDIO MUTED");
                }
                else if (Hit(x, y, 300, 326, 72, 40))
                {
                    SetVolume(options.AudioVolume - 1);
                }
                else if (Hit(x, y, 560, 326, 72, 40))
                {
                    SetVolume(options.AudioVolume + 1);
                }
                else if (Hit(x, y, 300, 376, 570, 32))
                {
                    draggingVolume = true;
                    SetVolumeFromPointer(x);
                }
                else
                {
                    for (int index = 0; index < 4; index++)
                    {
                        if (Hit(x, y, 300 + (index * 146), 480, 132, 40))
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
                    float buttonX = 424 + (index / 6) * 330;
                    float buttonY = 246 + (index % 6) * 46;
                    if (Hit(x, y, buttonX, buttonY, 156, 36))
                    {
                        BeginRebinding(index);
                        return;
                    }
                }
                if (Hit(x, y, 300, 548, 210, 40))
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
                    if (Hit(x, y, 300 + (index * 114), 232, 100, 42))
                    {
                        SelectSaveSlot(index + 1);
                        return;
                    }
                }

                if (Hit(x, y, 300, 342, 180, 44)) QuickSave();
                else if (Hit(x, y, 494, 342, 180, 44)) QuickLoad();
                else if (Hit(x, y, 688, 342, 180, 44)) Rewind();
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

        focusedControl = -1;
        controlCenterVisible = true;
        controlCenterPage = ControlCenterPage.Overview;
        mouseTurbo = false;
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
        MarkSettingsChanged();
        if (frameTexture != IntPtr.Zero)
        {
            SDL.SetTextureScaleMode(frameTexture, options.TextureScaleMode);
        }

        statusMessage = $"VIDEO FILTER · {filter.ToString().ToUpperInvariant()}";
    }

    private void SetFrameskip(int frameskip)
    {
        options.Frameskip = Math.Clamp(frameskip, 0, 2);
        MarkSettingsChanged();
        TryUiAction(ApplyEmulatorConfiguration, $"FRAMESKIP {options.Frameskip}");
    }

    private void SetPalette(int paletteIndex)
    {
        options.PaletteIndex = Math.Clamp(paletteIndex, 0, 4);
        MarkSettingsChanged();
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

        MarkSettingsChanged();
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

    private void DrawDisplayPage()
    {
        Text(300, 198, "VIDEO FILTER", 161, 173, 192);
        DrawButton(300, 230, 180, 42, "SHARP", options.VideoFilter == LinuxVideoFilter.Sharp);
        DrawButton(494, 230, 180, 42, "SMOOTH", options.VideoFilter == LinuxVideoFilter.Smooth);
        DrawButton(688, 230, 180, 42, "LCD GRID", options.VideoFilter == LinuxVideoFilter.LcdGrid);

        Text(300, 310, "FRAMESKIP", 161, 173, 192);
        for (int value = 0; value <= 2; value++)
        {
            DrawButton(300 + (value * 114), 340, 100, 40, value.ToString(), options.Frameskip == value);
        }

        Text(300, 420, "DMG PALETTE", 161, 173, 192);
        for (int index = 0; index < 5; index++)
        {
            DrawButton(
                300 + (index * 114),
                450,
                100,
                40,
                $"P{index + 1}",
                options.PaletteIndex == index);
        }
    }

    private void DrawAudioPage()
    {
        Text(300, 194, "NATIVE SDL3 AUDIO", 161, 173, 192);
        DrawButton(
            300,
            224,
            220,
            44,
            options.AudioEnabled ? "AUDIO ON" : "AUDIO MUTED",
            options.AudioEnabled && audioOutput is not null);

        Text(300, 302, "MASTER VOLUME", 161, 173, 192);
        DrawButton(300, 326, 72, 40, "-1%", false, options.AudioVolume > 0);
        Text(426, 340, $"{options.AudioVolume}%", 240, 242, 255);
        DrawButton(560, 326, 72, 40, "+1%", false, options.AudioVolume < 100);
        Fill(300, 389, 570, 6, 52, 61, 80);
        Fill(300, 389, options.AudioVolume * 5.7f, 6, 176, 158, 245);
        Fill(300 + options.AudioVolume * 5.7f - 7, 381, 14, 22, 216, 204, 255);
        Text(300, 410, "0%", 161, 173, 192);
        Text(360, 410, "Drag, or use Left / Right for 1% steps.", 161, 173, 192);
        Text(838, 410, "100%", 161, 173, 192);

        Text(300, 452, "HARDWARE CHANNELS", 161, 173, 192);
        bool[] channels =
        {
            options.Channel1Enabled,
            options.Channel2Enabled,
            options.Channel3Enabled,
            options.Channel4Enabled
        };
        for (int index = 0; index < channels.Length; index++)
        {
            DrawButton(300 + (index * 146), 480, 132, 40, $"CH {index + 1}", channels[index]);
        }

        string backend = audioOutput is null ? "NOT OPEN" : audioOutput.DriverName;
        Text(300, 536, $"BACKEND  {Truncate(backend, 56)}", 218, 222, 242);
        if (!string.IsNullOrWhiteSpace(audioError))
        {
            Text(300, 558, Truncate($"LAST ERROR  {audioError}", 80), 255, 132, 156);
        }
    }

    private void DrawInputPage()
    {
        Text(300, 188, "KEYBOARD CONTROLS", 176, 158, 245);
        Text(300, 216, "Click a key, or use arrows + Enter. Esc cancels a change.", 161, 173, 192);
        for (int index = 0; index < BindingActions.Length; index++)
        {
            LinuxInputAction action = BindingActions[index];
            float x = 300 + (index / 6) * 330;
            float y = 246 + (index % 6) * 46;
            Text(x, y + 8, action is LinuxInputAction.L or LinuxInputAction.R ? $"{action} (GBA)" : action.ToString(), 218, 222, 242);
            DrawButton(x + 124, y, 156, 36,
                rebindingAction == action ? "Press a key..." : BindingLabel(action),
                rebindingAction == action || (rebindingAction is null && focusedBinding == index));
        }
        DrawButton(300, 548, 210, 40, "Reset keyboard defaults", false);
        string controller = gamepad == IntPtr.Zero ? "No controller connected" : SDL.GetGamepadName(gamepad) ?? "Controller connected";
        Text(536, 548, textRenderer.Fit(controller, 380), 161, 173, 192);
        Text(536, 570, "Controller uses the standard layout.", 161, 173, 192);
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
        focusedControl = -1;
        FlushSettingsIfDue(force: true);
    }

    private void SetVolumeFromPointer(float x) => SetVolume((int)Math.Round((x - 300) / 5.7f));

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
        Text(300, 198, "ACTIVE SAVE-STATE SLOT", 161, 173, 192);
        for (int index = 0; index < 5; index++)
        {
            int slot = index + 1;
            bool exists = romPath is not null && File.Exists(LinuxSaveStateStore.GetPath(romPath, slot));
            DrawButton(
                300 + (index * 114),
                232,
                100,
                42,
                exists ? $"{slot} ·" : slot.ToString(),
                options.SaveSlot == slot);
        }

        Text(300, 312, "TIMELINE", 161, 173, 192);
        DrawButton(300, 342, 180, 44, "SAVE [F5]", false, session is not null);
        DrawButton(494, 342, 180, 44, "LOAD [F8]", false, session is not null);
        DrawButton(688, 342, 180, 44, "REWIND [F7]", false, session is not null);

        Text(300, 438, "BATTERY SAVE", 161, 173, 192);
        EmulationSnapshot? snapshot = session?.LatestSnapshot;
        string battery = snapshot?.Rom?.BatterySave.IsEnabled == true
            ? $"ACTIVE · {snapshot.Rom.BatterySave.ExpectedLength} BYTES"
            : session is null ? "NO ROM LOADED" : "CARTRIDGE HAS NO BATTERY RAM";
        Text(300, 470, battery, 218, 222, 242);
        Text(300, 514, "STATES .SS1-.SS5 AND BATTERY .SAV LIVE NEXT TO ROM", 161, 173, 192);
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
        MarkSettingsChanged();
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
