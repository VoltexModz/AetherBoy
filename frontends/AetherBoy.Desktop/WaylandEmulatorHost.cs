using System.Runtime.InteropServices;
using AetherBoy.Runtime;
using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed class WaylandEmulatorHost : IDisposable
{
    private const int LogicalWidth = 1180;
    private const int LogicalHeight = 760;
    private const int GameAreaX = 370;
    private const int GameAreaY = 116;
    private const int GameAreaWidth = 704;
    private const int GameAreaHeight = 452;
    private const short AxisThreshold = 16_000;

    private readonly LinuxDesktopProfile desktop;
    private readonly HashSet<SDL.Scancode> pressedKeys = new();
    private readonly IntPtr window;
    private readonly IntPtr renderer;

    private EmulationSession? session;
    private IntPtr frameTexture;
    private IntPtr gamepad;
    private int[] framePixels = Array.Empty<int>();
    private VideoGeometry frameGeometry = VideoGeometry.GameBoy;
    private long displayedFrameSequence;
    private GameBoyButtons postedButtons;
    private GameBoyAdvanceButtons postedAdvanceButtons;
    private string? romPath;
    private string statusMessage = "DROP ROM OR PASS A PATH";
    private bool running = true;
    private bool isFullscreen;
    private bool disposed;

    public WaylandEmulatorHost(LinuxDesktopProfile desktop)
    {
        this.desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
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
            LoadRom(args[0]);
        }

        while (running)
        {
            while (SDL.PollEvent(out SDL.Event currentEvent))
            {
                HandleEvent(in currentEvent);
            }

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
                    LoadRom(droppedPath);
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

        if (keyEvent.Scancode == SDL.Scancode.Tab && session is not null)
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
                running = false;
                break;
            case SDL.Scancode.Space when session is not null:
                bool pause = !session.LatestSnapshot.IsPaused;
                session.SetPausedAsync(pause).GetAwaiter().GetResult();
                statusMessage = pause ? "PAUSED" : "RUNNING";
                break;
            case SDL.Scancode.F5:
                QuickSave();
                break;
            case SDL.Scancode.F8:
                QuickLoad();
                break;
            case SDL.Scancode.F11:
                isFullscreen = !isFullscreen;
                SDL.SetWindowFullscreen(window, isFullscreen);
                break;
        }
    }

    private void UpdateEmulation()
    {
        EmulationSession? currentSession = session;
        if (currentSession is null)
        {
            return;
        }

        if (currentSession.State == SessionState.Faulted)
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
        var configuration = new EmulatorConfiguration(
            Frameskip: 0,
            AudioEnabled: false,
            Channel1Enabled: true,
            Channel2Enabled: true,
            Channel3Enabled: true,
            Channel4Enabled: true,
            SampleRate: 44_100);
        session = new EmulationSession(
            path,
            Path.ChangeExtension(path, "sav"),
            bootRom: null,
            configuration,
            paletteIndex: 0);
        romPath = path;
        displayedFrameSequence = 0;
        postedButtons = GameBoyButtons.None;
        postedAdvanceButtons = GameBoyAdvanceButtons.None;
        statusMessage = "RUNNING · F5 SAVE · F8 LOAD";
        SDL.SetWindowTitle(window, $"AetherBoy · {Path.GetFileNameWithoutExtension(path)}");
    }

    private void CloseSession()
    {
        if (session is not null)
        {
            session.Dispose();
            session = null;
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

        SDL.SetTextureScaleMode(frameTexture, SDL.ScaleMode.PixelArt);
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
        Text(58, 412, "F11     FULL", 218, 222, 242);

        EmulationSnapshot? snapshot = session?.LatestSnapshot;
        string system = snapshot?.VideoGeometry == VideoGeometry.GameBoyAdvance ? "GBA" : "GB / GBC";
        Text(370, 58, $"SIGNAL // {system}", 126, 133, 166);

        if (frameTexture != IntPtr.Zero && snapshot?.HasVideoFrame == true)
        {
            SDL.FRect destination = GetGameDestination(frameGeometry);
            SDL.RenderTexture(renderer, frameTexture, IntPtr.Zero, in destination);
        }
        else
        {
            Text(500, 326, "DROP .GB / .GBC / .GBA ROM", 178, 151, 255);
            Text(548, 348, "OR PASS A FILE PATH", 126, 133, 166);
        }

        Text(370, 648, statusMessage, 218, 222, 242);
        string gamepadStatus = gamepad == IntPtr.Zero
            ? "GAMEPAD: WAITING"
            : $"GAMEPAD: {SDL.GetGamepadName(gamepad) ?? "CONNECTED"}";
        Text(370, 664, gamepadStatus, 126, 133, 166);
    }

    private static SDL.FRect GetGameDestination(VideoGeometry geometry)
    {
        int scale = Math.Max(
            1,
            Math.Min(GameAreaWidth / geometry.Width, GameAreaHeight / geometry.Height));
        int width = geometry.Width * scale;
        int height = geometry.Height * scale;
        return new SDL.FRect
        {
            X = GameAreaX + (GameAreaWidth - width) / 2f,
            Y = GameAreaY + (GameAreaHeight - height) / 2f,
            W = width,
            H = height
        };
    }

    private void QuickSave()
    {
        if (session is null || romPath is null)
        {
            return;
        }

        byte[] state = session.CaptureStateAsync().GetAwaiter().GetResult();
        string path = Path.ChangeExtension(romPath, "ss1");
        string temporaryPath = path + ".tmp";
        File.WriteAllBytes(temporaryPath, state);
        File.Move(temporaryPath, path, overwrite: true);
        statusMessage = "STATE SAVED · SLOT 1";
    }

    private void QuickLoad()
    {
        if (session is null || romPath is null)
        {
            return;
        }

        string path = Path.ChangeExtension(romPath, "ss1");
        if (!File.Exists(path))
        {
            statusMessage = "NO STATE IN SLOT 1";
            return;
        }

        session.RestoreStateAsync(File.ReadAllBytes(path)).GetAwaiter().GetResult();
        displayedFrameSequence = 0;
        statusMessage = "STATE LOADED · SLOT 1";
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

    private void Text(float x, float y, string value, byte red, byte green, byte blue)
    {
        SDL.SetRenderDrawColor(renderer, red, green, blue, 255);
        SDL.RenderDebugText(renderer, x, y, value);
    }
}
