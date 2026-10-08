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
        Diagnostics,
        Library,
        Tools
    }

    private readonly record struct DialogSelection(string? Path, string? Error);

    private int LogicalWidth = 1180;
    private int LogicalHeight = 760;
    private const int GameAreaX = 40;
    private const int GameAreaY = 134;
    private int GameAreaWidth => LogicalWidth - 376;
    private int GameAreaHeight => LogicalHeight - 264;


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
    private readonly LinuxDataPaths dataPaths;
    private readonly LinuxDiagnostics diagnostics;
    private readonly LinuxSessionHealth sessionHealth = new();
    private LinuxHealthSample? latestHealthSample;
    private string? latestHealthHint;
    private long audioFramesObserved;
    private long lastProblemMarkerAt = long.MinValue;
    private long lastDiagnosticsAt;
    private long nextAudioRetryAt;
    private readonly bool vsyncEnabled;
    private bool minimized;
    private LinuxRomStorage? storage;
    private LinuxRomStorage? pendingStorage;
    private string? StateBasePath => IsOnlineLink ? null : storage?.StateBasePath;
    private bool HasSelectedState => !IsOnlineLink && StateCard(options.SaveSlot) is { Exists: true, Error: null };
    private string statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("OPEN OR DROP A ROM");
    private string? audioError;
    private bool running = true;
    private bool isFullscreen;
    private bool controlCenterVisible;
    private bool resumeAfterControlCenter;
    private ControlCenterPage controlCenterPage;
    private int fileDialogOpen;
    private bool disposed;

    public WaylandEmulatorHost(LinuxDesktopProfile desktop, string? settingsPath = null, bool hidden = false, LinuxDiagnostics? diagnostics = null)
    {
        this.desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
        this.settingsPath = settingsPath ?? LinuxSettingsStore.DefaultPath;
        dataPaths = settingsPath is null ? LinuxDataPaths.Default
            : LinuxDataPaths.Isolated(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settingsPath))!, "storage"));
        hiddenWindow = hidden;
        library = new LinuxLibrary(dataPaths);
        releaseUpdates = UpdateServiceFactory(Path.Combine(dataPaths.Cache, "updates"));
        this.diagnostics = diagnostics ?? new LinuxDiagnostics(dataPaths, false);
        options = LinuxSettingsStore.Load(this.settingsPath, out string? settingsError);
        AetherBoy.Runtime.Localization.UiText.Initialize(options.DisplayLanguage);
        Colors = new UiColors(new UiThemePalette(options.UiPrimaryColor, options.UiSecondaryColor, options.UiBackgroundColor));
        globalProfile = LinuxGameProfile.Capture(options);
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
        vsyncEnabled = SDL.SetRenderVSync(renderer, 1);
        textRenderer = new SdlTextRenderer(renderer);
        LoadBrandMark();
        SDL.SetWindowMinimumSize(window, 860, 554);
        OpenFirstAvailableGamepad();
        RefreshLibrary();
    }

    public int Run(string[] args)
    {
        bool accessible = args.Contains("--accessible", StringComparer.Ordinal);
        args = args.Where(arg => arg != "--accessible").ToArray();
        if (args.Length > 1)
        {
            throw new ArgumentException(global::AetherBoy.Runtime.Localization.UiText.Get("Usage: AetherBoy.Desktop [--accessible] [game.gb|game.gbc|game.gba]"));
        }

        if (accessible) OpenAccessibleControls();
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
            UpdateBootIntro();
            CompletePendingLoad();
            CompletePendingPatch();
            CompletePendingScreenshot();
            PollSofaController();
            try { UpdateEmulation(); }
            catch (Exception exception) { ReportError(exception); }
            try { UpdateLocalLink(); }
            catch (Exception exception) { ReportError(exception); StopLocalLink(); }
            FlushSettingsIfDue();
            UpdateDiagnostics();
            PollDiscordPresence();
            PollStartupUpdateCheck();
            UpdateOnlineProbe();
            UpdateComfort();
            if (!minimized)
            {
                DrawShell();
                SDL.RenderPresent(renderer);
                RecordPresentation();
            }
            UpdateAccessibleControls();
            // Idle/paused views do not need to redraw at the monitor's maximum rate.
            bool idle = (session is null || session.LatestSnapshot.IsPaused)
                && (localLinkSession is null || localLinkSession.LatestSnapshot.IsPaused) || minimized;
            SDL.Delay(introClock is not null ? 16u : idle ? 50u : vsyncEnabled ? 0u : 2u);
        }

        return session?.Fault is null ? 0 : 1;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        accessibleControls?.Dispose(); accessibleControls = null;
        disposed = true;
        FinishBootIntro(false);
        discordPresence.Dispose();
        releaseUpdates.Dispose();
        // Finish a started import before shutting down; its worker never touches SDL.
        try { libraryMutation?.GetAwaiter().GetResult(); } catch { }
        try { pendingPatch?.GetAwaiter().GetResult(); } catch { /* Reported during normal completion. */ }
        pendingPatch = null;
        if (pendingScreenshot is not null)
        {
            try { pendingScreenshot.GetAwaiter().GetResult(); }
            catch (Exception exception) { diagnostics.Failure("screenshot", exception); }
            pendingScreenshot = null;
        }
        FinishRomPreparation();
        StopLocalLink();
        try { localLinkPlanTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        try { localLinkStartupTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        if (localLinkStartupTask is { IsCompletedSuccessfully: true } unclaimedStartup)
        {
            try { unclaimedStartup.Result.ShutdownAsync().Wait(TimeSpan.FromSeconds(2)); }
            catch (Exception exception) { diagnostics.Failure("local_link_shutdown", exception); }
        }
        else if (localLinkStartupTask is { IsCompleted: false } finishingStartup)
        {
            // A constructor may finish after the bounded window shutdown wait. It must not
            // leave a newly created local-link owner running without a UI to collect it.
            _ = finishingStartup.ContinueWith(async finished =>
            {
                if (finished.IsCompletedSuccessfully) await finished.Result.ShutdownAsync().ConfigureAwait(false);
                else _ = finished.Exception;
            }, TaskScheduler.Default).Unwrap();
        }
        try { localLinkStopTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        ReleaseLocalLinkTextures();
        CloseSecondLocalGamepad();
        FlushSettingsIfDue(force: true);
        StopOnlineProbe();
        try { onlineProbeStopTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        onlineProbe?.Dispose();
        onlineProbe = null;
        DisposeSession(pendingSession);
        pendingSession = null;
        pendingStorage?.Dispose();
        pendingStorage = null;
        CloseSession();
        diagnostics.Dispose();
        StopRecording();
        audioOutput?.Dispose();
        audioOutput = null;
        if (gamepad != IntPtr.Zero)
        {
            StopControllerRumble();
            SDL.CloseGamepad(gamepad);
            gamepad = IntPtr.Zero;
        }

        if (frameTexture != IntPtr.Zero)
        {
            SDL.DestroyTexture(frameTexture);
            frameTexture = IntPtr.Zero;
        }

        if (brandTexture != IntPtr.Zero) SDL.DestroyTexture(brandTexture);
        if (brandTexture64 != IntPtr.Zero) SDL.DestroyTexture(brandTexture64);
        if (brandTexture128 != IntPtr.Zero) SDL.DestroyTexture(brandTexture128);
        ClearLibraryPreviews();
        ClearPreviewTextures();
        textRenderer.Dispose();
        SDL.DestroyRenderer(renderer);
        SDL.DestroyWindow(window);
    }

    private unsafe void HandleEvent(in SDL.Event currentEvent)
    {
        FinishStoppedOnlineLink();
        SDL.EventType type = (SDL.EventType)currentEvent.Type;
        switch (type)
        {
            case SDL.EventType.TextInput:
                ReceiveTextInput(Marshal.PtrToStringUTF8((IntPtr)currentEvent.Text.Text) ?? "");
                break;
            case SDL.EventType.TextEditing:
                ReceiveTextComposition(Marshal.PtrToStringUTF8((IntPtr)currentEvent.Edit.Text) ?? "", currentEvent.Edit.Start, currentEvent.Edit.Length);
                break;
            case SDL.EventType.WindowMinimized: minimized = true; break;
            case SDL.EventType.WindowRestored: minimized = false; break;
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
                    if (!HandleTextPointerDown(logicalEvent.Button.X, logicalEvent.Button.Y, (SDL.GetModState() & SDL.Keymod.Shift) != 0))
                        HandleMouseClick(logicalEvent.Button.X, logicalEvent.Button.Y);
                }
                break;
            case SDL.EventType.MouseButtonUp:
                if (currentEvent.Button.Button == SDL.ButtonLeft)
                {
                    draggingTextSelection = false;
                    draggingVolume = false;
                    ReleaseMouseTurbo();
                    FlushSettingsIfDue();
                }
                break;
            case SDL.EventType.MouseMotion:
                SDL.Event motion = currentEvent;
                SDL.ConvertEventToRenderCoordinates(renderer, ref motion);
                mouseX = motion.Motion.X;
                mouseY = motion.Motion.Y;
                HandleTextPointerMotion(mouseX);
                if (draggingVolume) SetVolumeFromPointer(mouseX);
                break;
            case SDL.EventType.WindowMouseLeave:
                mouseX = mouseY = -1;
                break;
            case SDL.EventType.WindowFocusLost:
                CancelTextComposition();
                windowFocused = false;
                if (options.PauseOnFocusLoss && session is not null && session.State == SessionState.Running)
                { resumeAfterFocus = true; session.SetPausedAsync(true).GetAwaiter().GetResult(); audioOutput?.Clear(); }
                if (localLinkSession is { State: SessionState.Running } link)
                { resumeLocalLinkAfterFocus = true; _ = link.SetPausedAsync(true); localLinkAudioMixer.Clear(); audioOutput?.Clear(); }
                mouseTurbo = false;
                draggingVolume = false;
                rebindingAction = null;
                FlushSettingsIfDue();
                pressedKeys.Clear();
                if (session is not null && session.State is SessionState.Running or SessionState.Paused)
                {
                    if (!IsOnlineLink) session.SetTurboAsync(false).GetAwaiter().GetResult();
                    PostInput(session);
                }
                break;
            case SDL.EventType.WindowFocusGained:
                windowFocused = true;
                // A replacement Runtime owner may already be running. Keep the focus
                // resume request until its preparation has completed or been cancelled.
                if (IsLoading) break;
                if (resumeAfterFocus && session is not null)
                {
                    if (showSofaLibrary) sofaResumeOnClose = true;
                    else if (showQuickDeck) quickDeckResumeOnClose = true;
                    else if (controlCenterVisible) resumeAfterControlCenter = true;
                    else session.SetPausedAsync(false).GetAwaiter().GetResult();
                }
                resumeAfterFocus = false;
                if (resumeLocalLinkAfterFocus && localLinkSession is { } resumedLink && !controlCenterVisible)
                    _ = resumedLink.SetPausedAsync(false);
                resumeLocalLinkAfterFocus = false;
                break;
            case SDL.EventType.GamepadButtonDown:
            case SDL.EventType.GamepadButtonUp:
                HandleGamepadEvent(currentEvent.GButton, type == SDL.EventType.GamepadButtonDown);
                break;
            case SDL.EventType.GamepadAdded:
                if (gamepad == IntPtr.Zero)
                {
                    TryOpenGamepad(currentEvent.GDevice.Which);
                }
                else OpenSecondLocalGamepad();
                break;
            case SDL.EventType.GamepadRemoved:
                if (secondGamepad != IntPtr.Zero && SDL.GetGamepadID(secondGamepad) == currentEvent.GDevice.Which)
                { CloseSecondLocalGamepad(); localLinkMessage = global::AetherBoy.Runtime.Localization.UiText.Get("The second controller disconnected. Player 2 can use the remaining controller."); }
                if (gamepad != IntPtr.Zero && SDL.GetGamepadID(gamepad) == currentEvent.GDevice.Which)
                {
                    StopControllerRumble();
                    SDL.CloseGamepad(gamepad);
                    gamepad = IntPtr.Zero;
                    rebindingGamepad = null;
                    if (session is not null && !IsOnlineLink) session.SetTurboAsync(false).GetAwaiter().GetResult();
                    // Promote the remaining pad through the normal profile path without two live SDL handles to it.
                    CloseSecondLocalGamepad();
                    OpenFirstAvailableGamepad();
                    statusMessage = gamepad == IntPtr.Zero ? global::AetherBoy.Runtime.Localization.UiText.Get("Controller disconnected. Keyboard is ready.") : global::AetherBoy.Runtime.Localization.UiText.Get("Switched to another controller.");
                    OpenSecondLocalGamepad();
                }
                break;
            case SDL.EventType.DropFile:
                string? droppedPath = Marshal.PtrToStringUTF8(currentEvent.Drop.Data);
                if (!string.IsNullOrWhiteSpace(droppedPath))
                {
                    if (showLocalLinkPage) localLinkMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Use Choose game for player 1 or player 2 before starting.");
                    else if (controlCenterVisible && controlCenterPage == ControlCenterPage.Library && showPatchLab)
                        SelectPatchFile(droppedPath, LinuxRomPatchService.IsPatchPath(droppedPath) ? PatchSelection.Patch : PatchSelection.Source);
                    else TryLoadRom(droppedPath);
                }
                break;
        }
    }

    private void HandleKeyboard(SDL.KeyboardEvent keyEvent, bool isPressed)
    {
        if (introClock is not null)
        {
            if (isPressed && !keyEvent.Repeat && keyEvent.Scancode is SDL.Scancode.Escape or SDL.Scancode.Return or SDL.Scancode.Space) FinishBootIntro(true);
            return;
        }
        if (HandleSofaKeyboard(keyEvent, isPressed)) return;
        if (archiveSelection is not null) { HandleArchiveKeyboard(keyEvent, isPressed); return; }
        if (IsLoading)
        {
            if (isPressed && !keyEvent.Repeat)
            {
                if (keyEvent.Scancode == SDL.Scancode.Escape) CancelRomLoad();
                else if (keyEvent.Scancode is SDL.Scancode.Tab or SDL.Scancode.F6) { DrawShell(); focusedControl = focusTargets.Count - 1; }
                else if (keyEvent.Scancode == SDL.Scancode.Return && focusedControl >= 0) CancelRomLoad();
            }
            return;
        }
        if (isPressed && !keyEvent.Repeat && keyEvent.Scancode == SDL.Scancode.F7 && (keyEvent.Mod & SDL.Keymod.Ctrl) != 0 && rebindingAction is null)
        { OpenAccessibleControls(); return; }

        if (rebindingAction is { } action)
        {
            pressedKeys.Clear();
            if (!isPressed || keyEvent.Repeat) return;
            if (keyEvent.Scancode == SDL.Scancode.Escape)
            {
                rebindingAction = null;
                statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Key change cancelled.");
            }
            else if (!LinuxKeyBindings.CanBind(keyEvent.Scancode))
            {
                statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("That key is reserved for the app. Choose another, or Esc to cancel.");
            }
            else
            {
                options.Keys.Bind(action, keyEvent.Scancode);
                rebindingAction = null;
                MarkSettingsChanged();
                statusMessage = global::AetherBoy.Runtime.Localization.UiText.Format("{0} is now {1}. Occupied keys are swapped.", global::AetherBoy.Runtime.Localization.UiLabels.Input(action.ToString()), KeyLabel(keyEvent.Scancode));
            }
            return;
        }

        if (rebindingGamepad is not null && isPressed && keyEvent.Scancode == SDL.Scancode.Escape)
        { rebindingGamepad = null; statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Controller change cancelled."); return; }
        if (controlCenterVisible && isPressed && !keyEvent.Repeat && keyEvent.Scancode == SDL.Scancode.K &&
            (keyEvent.Mod & SDL.Keymod.Ctrl) != 0)
        { FocusSettingsSearch(); return; }
        if (HandleTextEditorKey(keyEvent, isPressed)) return;
        if (showQuickDeck && !controlCenterVisible && isPressed && !keyEvent.Repeat
            && keyEvent.Scancode is SDL.Scancode.Escape or SDL.Scancode.F4)
        { CloseQuickDeck(); return; }
        if (controlCenterVisible && isPressed && keyEvent.Scancode == SDL.Scancode.Escape && ShowingSettingsSearch)
        { ClearSettingsSearch(); return; }
        if (!controlCenterVisible && isPressed && !keyEvent.Repeat)
        {
            if (keyEvent.Scancode == SDL.Scancode.F6 || (focusedControl >= 0 && keyEvent.Scancode == SDL.Scancode.Return))
            {
                bool hadFocus = focusedControl >= 0;
                DrawShell();
                if (hadFocus && focusedControl < 0 && keyEvent.Scancode == SDL.Scancode.Return) return;
            }
            if (keyEvent.Scancode == SDL.Scancode.F6 && focusTargets.Count > 0)
            {
                int direction = (keyEvent.Mod & SDL.Keymod.Shift) != 0 ? -1 : 1;
                focusedControl = (focusedControl + direction + focusTargets.Count) % focusTargets.Count;
                return;
            }
            if (focusedControl >= 0 && focusedControl < focusTargets.Count && keyEvent.Scancode == SDL.Scancode.Return)
            {
                SDL.FRect target = focusTargets[focusedControl];
                HandleMouseClick(target.X + target.W / 2, target.Y + target.H / 2);
                return;
            }
        }
        if (controlCenterVisible && isPressed)
        {
            if (!keyEvent.Repeat && keyEvent.Scancode is SDL.Scancode.Tab or SDL.Scancode.F6 or SDL.Scancode.Return or SDL.Scancode.Space)
            {
                bool hadFocus = focusedControl >= 0;
                // Reconcile dynamic/disabled controls before keyboard activation, even between two render ticks.
                DrawShell();
                if (hadFocus && focusedControl < 0 && keyEvent.Scancode is SDL.Scancode.Return or SDL.Scancode.Space) return;
            }
            if (keyEvent.Scancode == SDL.Scancode.F6 && !keyEvent.Repeat)
            {
                JumpControlCenterRegion();
                return;
            }
            if (keyEvent.Scancode == SDL.Scancode.Tab && !keyEvent.Repeat)
            {
                int direction = (keyEvent.Mod & SDL.Keymod.Shift) != 0 ? -1 : 1;
                if ((keyEvent.Mod & SDL.Keymod.Ctrl) != 0)
                    SelectControlCenterPage((ControlCenterPage)(((int)controlCenterPage + direction + PageNames.Length) % PageNames.Length));
                else if (focusTargets.Count > 0)
                    focusedControl = focusedControl < 0 ? (direction > 0 ? 0 : focusTargets.Count - 1)
                        : (focusedControl + direction + focusTargets.Count) % focusTargets.Count;
                return;
            }
            if (!ShowingSettingsSearch && controlCenterPage == ControlCenterPage.Input && !showController && !showInputShortcuts &&
                keyEvent.Scancode is SDL.Scancode.Up or SDL.Scancode.Down or SDL.Scancode.Left or SDL.Scancode.Right)
                focusedControl = -1;
            if (focusedControl >= 0 && focusedControl < focusTargets.Count &&
                keyEvent.Scancode is SDL.Scancode.Return or SDL.Scancode.Space && !keyEvent.Repeat)
            {
                SDL.FRect target = focusTargets[focusedControl];
                HandleMouseClick(target.X + target.W / 2, target.Y + target.H / 2);
                return;
            }
            if (!ShowingSettingsSearch && controlCenterPage == ControlCenterPage.Audio)
            {
                switch (keyEvent.Scancode)
                {
                    case SDL.Scancode.Left: SetVolume(options.AudioVolume - 1); return;
                    case SDL.Scancode.Right: SetVolume(options.AudioVolume + 1); return;
                    case SDL.Scancode.Home: SetVolume(0); return;
                    case SDL.Scancode.End: SetVolume(100); return;
                }
            }
            if (!ShowingSettingsSearch && controlCenterPage == ControlCenterPage.Input && !showController && !showInputShortcuts)
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
        if (showLocalLinkPage && !controlCenterVisible)
        {
            if (isPressed) pressedKeys.Add(keyEvent.Scancode);
            else pressedKeys.Remove(keyEvent.Scancode);
            if (isPressed && !keyEvent.Repeat && keyEvent.Scancode is SDL.Scancode.Escape or SDL.Scancode.F11)
            {
                if (keyEvent.Scancode == SDL.Scancode.Escape) ToggleLocalLinkPause();
                else { isFullscreen = !isFullscreen; SDL.SetWindowFullscreen(window, isFullscreen); }
            }
            return;
        }
        if (showQuickDeck && !controlCenterVisible)
        {
            if (isPressed && !keyEvent.Repeat && keyEvent.Scancode == SDL.Scancode.F6)
                return; // Focus routing above already handled this key.
            return; // Do not send game or app shortcuts through a modal quick menu.
        }
        if (isPressed)
        {
            if (!controlCenterVisible) focusedControl = -1;
            pressedKeys.Add(keyEvent.Scancode);
        }
        else
        {
            pressedKeys.Remove(keyEvent.Scancode);
        }

        if (keyEvent.Scancode == options.Keys[LinuxInputAction.Turbo] && session is not null && !IsOnlineLink && !controlCenterVisible && !IsLoading)
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
                    BackFromSettings();
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
            case SDL.Scancode.F9:
                TogglePerformanceOverlay();
                break;
            case SDL.Scancode.F12:
                CaptureScreenshot();
                break;
            case SDL.Scancode.F10:
                OpenOnlineLinkPage();
                break;
            case SDL.Scancode.F4:
                ToggleQuickDeck();
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
        if (introClock is not null)
        {
            return;
        }
        if (archiveSelection is not null)
        {
            DrawShell();
            foreach (var command in shellCommands)
                if (Hit(x, y, command.Bounds.X, command.Bounds.Y, command.Bounds.W, command.Bounds.H))
                { command.Action(); return; }
            return;
        }
        if (IsLoading)
        {
            if (Hit(x, y, LogicalWidth / 2f - 105, LogicalHeight / 2f + 33, 210, 42)) CancelRomLoad();
            return;
        }
        if (sofaMode) DrawShell(); // Reconcile modal/page actions before handling queued clicks.
        focusedControl = -1;
        foreach (var command in shellCommands)
        {
            if (Hit(x, y, command.Bounds.X, command.Bounds.Y, command.Bounds.W, command.Bounds.H))
            {
                command.Action();
                return;
            }
        }
        if (IsLoading) return;
        if (controlCenterVisible) HandleControlCenterAction(x, y);
    }

    private void HandleControlCenterAction(float x, float y)
    {
        if ((controlCenterPage == ControlCenterPage.Saves && (showBackups || showGallery)) ||
            (controlCenterPage == ControlCenterPage.Input && showController)) return;
        if (ShowingSettingsSearch || showInputShortcuts) return;
        switch (controlCenterPage)
        {
            case ControlCenterPage.Audio:
                if (Hit(x, y, 300, 224, 220, 44))
                {
                    options.AudioEnabled = !options.AudioEnabled;
                    MarkSettingsChanged();
                    TryUiAction(ApplyEmulatorConfiguration, options.AudioEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("AUDIO ENABLED") : global::AetherBoy.Runtime.Localization.UiText.Get("Sound muted"));
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
                    float buttonY = 276 + (index % 6) * 44;
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
                    statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Default keys restored.");
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
        if (session is null || IsLoading || controlCenterVisible) return;
        bool pause = !session.LatestSnapshot.IsPaused;
        session.SetPausedAsync(pause).GetAwaiter().GetResult();
        audioOutput?.Clear();
        statusMessage = pause ? global::AetherBoy.Runtime.Localization.UiText.Format("Paused. Press {0} to resume.", KeyLabel(options.Keys[LinuxInputAction.Pause])) : global::AetherBoy.Runtime.Localization.UiText.Get("Playing");
    }

    private void ToggleControlCenter()
    {
        if (sofaMode) { if (!showSofaLibrary) ToggleQuickDeck(); return; }
        if (IsLoading) return;
        if (showQuickDeck) CloseQuickDeck();
        if (controlCenterVisible)
        {
            CloseControlCenter();
            return;
        }

        focusedControl = -1;
        controlCenterVisible = true;
        controlCenterPage = ControlCenterPage.Overview;
        ResetSettingsNavigation();
        mouseTurbo = false;
        pressedKeys.Clear();
        EmulationSession? currentSession = session;
        if (currentSession is not null)
        {
            if (!IsOnlineLink) currentSession.SetTurboAsync(false).GetAwaiter().GetResult();
            resumeAfterControlCenter = !currentSession.LatestSnapshot.IsPaused;
            if (resumeAfterControlCenter)
            {
                currentSession.SetPausedAsync(true).GetAwaiter().GetResult();
            }
        }
        if (localLinkSession is { } local)
        {
            resumeLocalLinkAfterSettings = !local.LatestSnapshot.IsPaused;
            if (resumeLocalLinkAfterSettings) _ = local.SetPausedAsync(true);
            localLinkAudioMixer.Clear(); audioOutput?.Clear();
        }
    }

    private void CloseControlCenter()
    {
        pendingPatchLaunch = false;
        if (editingTitleIdentity is not null) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Save or cancel the title before leaving."); return; }
        if (showOnlineProbePage) LeaveOnlineProbePage();
        editingSettingsSearch = false;
        editingSearch = false;
        editingCheat = false;
        editingBarcode = false;
        onlineEditingField = TextField.None;
        titleEditVersion++; editingTitleIdentity = null;
        SDL.StopTextInput(window);
        focusedControl = -1;
        rebindingGamepad = null;
        rebindingAction = null;
        draggingVolume = false;
        pressedKeys.Clear();
        FlushSettingsIfDue();
        controlCenterVisible = false;
        EmulationSession? currentSession = session;
        if (resumeAfterControlCenter && currentSession is not null)
        {
            currentSession.SetPausedAsync(false).GetAwaiter().GetResult();
        }

        resumeAfterControlCenter = false;
        if (resumeLocalLinkAfterSettings && localLinkSession is { } local)
            _ = local.SetPausedAsync(false);
        resumeLocalLinkAfterSettings = false;
    }

    private void SetVideoFilter(LinuxVideoFilter filter)
    {
        options.VideoFilter = filter;
        MarkSettingsChanged();
        if (frameTexture != IntPtr.Zero)
        {
            SDL.SetTextureScaleMode(frameTexture, options.TextureScaleMode);
        }

        statusMessage = filter switch { LinuxVideoFilter.Sharp => global::AetherBoy.Runtime.Localization.UiText.Get("Sharp pixel edges selected."), LinuxVideoFilter.Smooth => global::AetherBoy.Runtime.Localization.UiText.Get("Smooth picture selected."), _ => global::AetherBoy.Runtime.Localization.UiText.Get("LCD grid selected.") };
    }

    private void SetFrameskip(int frameskip)
    {
        options.Frameskip = Math.Clamp(frameskip, 0, 2);
        MarkSettingsChanged();
        TryUiAction(ApplyEmulatorConfiguration, options.Frameskip == 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("Displaying every frame.") : global::AetherBoy.Runtime.Localization.UiText.Format("Skipping {0} display frames between updates.", options.Frameskip));
    }

    private void SetPalette(int paletteIndex)
    {
        options.PaletteIndex = Math.Clamp(paletteIndex, 0, 4);
        MarkSettingsChanged();
        if (session is not null)
        {
            TryUiAction(
                () => session.SetPaletteAsync(options.PaletteIndex).GetAwaiter().GetResult(),
                global::AetherBoy.Runtime.Localization.UiText.Format("DMG PALETTE {0}", options.PaletteIndex + 1));
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
        TryUiAction(ApplyEmulatorConfiguration, global::AetherBoy.Runtime.Localization.UiText.Format("AUDIO CHANNEL {0} UPDATED", index + 1));
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
            diagnostics.Failure("ui_action", exception);
            statusMessage = DescribeActionFailure(exception);
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
        FinishStoppedOnlineLink();
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
            // The preparation worker may still be awaiting this owner. Cancel it first;
            // disposal on the following tick must not race those Runtime commands.
            if (IsLoading) { CancelRomLoad(); return; }
            ReportError(currentSession.Fault ?? new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("The emulator stopped.")));
            CloseSession();
            romPath = null;
            return;
        }

        PostInput(currentSession);
        UpdateOnlineAudioWait(currentSession);
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
        if (controlCenterVisible || showQuickDeck || showSofaLibrary || sofaAwaitNeutral || !windowFocused || fileDialogOpen != 0 || IsLoading)
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

        AddGamepadButton(gamepadProfile.Buttons[LinuxInputAction.Up], GameBoyButtons.Up, ref buttons);
        AddGamepadButton(gamepadProfile.Buttons[LinuxInputAction.Down], GameBoyButtons.Down, ref buttons);
        AddGamepadButton(gamepadProfile.Buttons[LinuxInputAction.Left], GameBoyButtons.Left, ref buttons);
        AddGamepadButton(gamepadProfile.Buttons[LinuxInputAction.Right], GameBoyButtons.Right, ref buttons);
        AddGamepadButton(gamepadProfile.Buttons[LinuxInputAction.A], GameBoyButtons.A, ref buttons);
        AddGamepadButton(gamepadProfile.Buttons[LinuxInputAction.B], GameBoyButtons.B, ref buttons);
        AddGamepadButton(gamepadProfile.Buttons[LinuxInputAction.Select], GameBoyButtons.Select, ref buttons);
        AddGamepadButton(gamepadProfile.Buttons[LinuxInputAction.Start], GameBoyButtons.Start, ref buttons);

        short x = SDL.GetGamepadAxis(gamepad, SDL.GamepadAxis.LeftX);
        short y = SDL.GetGamepadAxis(gamepad, SDL.GamepadAxis.LeftY);
        if (x < -gamepadProfile.Deadzone) buttons |= GameBoyButtons.Left;
        if (x > gamepadProfile.Deadzone) buttons |= GameBoyButtons.Right;
        if (y < -gamepadProfile.Deadzone) buttons |= GameBoyButtons.Up;
        if (y > gamepadProfile.Deadzone) buttons |= GameBoyButtons.Down;
        return buttons;
    }

    private GameBoyAdvanceButtons ReadAdvanceButtons()
    {
        GameBoyAdvanceButtons buttons = GameBoyAdvanceButtons.None;
        if (controlCenterVisible || showQuickDeck || showSofaLibrary || sofaAwaitNeutral || !windowFocused || fileDialogOpen != 0 || IsLoading)
        {
            return buttons;
        }

        if (pressedKeys.Contains(options.Keys[LinuxInputAction.L]) ||
            (gamepad != IntPtr.Zero && SDL.GetGamepadButton(gamepad, gamepadProfile.Buttons[LinuxInputAction.L])))
        {
            buttons |= GameBoyAdvanceButtons.L;
        }

        if (pressedKeys.Contains(options.Keys[LinuxInputAction.R]) ||
            (gamepad != IntPtr.Zero && SDL.GetGamepadButton(gamepad, gamepadProfile.Buttons[LinuxInputAction.R])))
        {
            buttons |= GameBoyAdvanceButtons.R;
        }

        return buttons;
    }

    private void TryLoadRom(string candidate) => BeginRomLoad(candidate);

    private void CompletePendingLoad()
    {
        CompleteRomPreparation();
        EmulationSession? candidate = pendingSession;
        if (candidate is null || candidate.State == SessionState.Starting) return;
        pendingSession = null;
        if (candidate.State == SessionState.Faulted)
        {
            ReportError(candidate.Fault ?? new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("The ROM could not be started.")));
            DisposeSession(candidate);
            pendingStorage?.Dispose();
            pendingStorage = null;
            ResumeAfterFailedLoad();
            pendingRomPath = null;
            pendingResumeIdentity = null;
            return;
        }
        CloseSession();
        session = candidate;
        if (sofaMode) { showSofaLibrary = false; sofaPausedSession = null; sofaResumeOnClose = false; sofaAwaitNeutral = true; }
        pendingGameProfile?.ApplyTo(options);
        usingGameProfile = pendingGameProfile is not null;
        pendingGameProfile = null;
        storage = pendingStorage;
        diagnostics.Record("rom_loaded", new { hash = storage?.Identity });
        diagnostics.Record("rom.started", new { rom_sha256 = storage?.Identity,
            model = candidate.LatestSnapshot.Rom?.IsGameBoyAdvance == true ? "GBA"
                : candidate.LatestSnapshot.Rom?.HasColorFeatures == true ? "GBC" : "GB",
            battery_save_enabled = candidate.LatestSnapshot.Rom?.BatterySave.IsEnabled });
        pendingStorage = null;
        pendingBatteryRestore = null;
        showBackups = false;
        romPath = pendingRomPath;
        try { library.Remember(storage!.Identity, romPath!); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { diagnostics.Failure("library_write", ex); }
        pendingRomPath = null;
        resumeAfterLoad = false;
        if (audioOutput is not null) session.AudioSamplesAvailable += OnAudioSamplesAvailable;
        resumeAfterFocus = false;
        if (controlCenterVisible)
        {
            session.SetPausedAsync(true).GetAwaiter().GetResult();
            resumeAfterControlCenter = true;
        }
        else if (!windowFocused && options.PauseOnFocusLoss)
        {
            session.SetPausedAsync(true).GetAwaiter().GetResult();
            resumeAfterFocus = true;
        }
        ApplyEmulatorConfiguration();
        if (frameTexture != IntPtr.Zero) SDL.SetTextureScaleMode(frameTexture, options.TextureScaleMode);
        RequestDiskRefresh();
        nextResumeAt = Environment.TickCount64 + 60_000;
        playtimeClock.Reset();
        displayedFrameSequence = 0;
        postedButtons = GameBoyButtons.None;
        postedAdvanceButtons = GameBoyAdvanceButtons.None;
        statusMessage = profileNotice ?? storage?.MigrationNotice ?? global::AetherBoy.Runtime.Localization.UiText.Get("Playing. Saves are stored safely in your AetherBoy library.");
        SDL.SetWindowTitle(window, $"AetherBoy · {Path.GetFileNameWithoutExtension(romPath)}");
        bool continueRequested = pendingResumeIdentity == storage?.Identity;
        pendingResumeIdentity = null;
        if (continueRequested) LoadResume();
    }

    private void ResumeAfterFailedLoad()
    {
        if (showSofaLibrary && loadError is not null) sofaNotice = global::AetherBoy.Runtime.Localization.UiText.Get("The game could not be opened. Your previous game is unchanged. Leave sofa mode to inspect the error.");
        if ((resumeAfterLoad || resumeAfterFocus) && session is not null && session.State == SessionState.Paused)
        {
            if (showSofaLibrary) { sofaResumeOnClose = true; resumeAfterFocus = false; }
            else if (controlCenterVisible) { resumeAfterControlCenter = true; resumeAfterFocus = false; }
            else if (!windowFocused && options.PauseOnFocusLoss) resumeAfterFocus = true;
            else { session.SetPausedAsync(false).GetAwaiter().GetResult(); resumeAfterFocus = false; }
        }
        resumeAfterLoad = false;
    }

    private void UpdateDiagnostics()
    {
        if (Environment.TickCount64 - lastDiagnosticsAt < 1000) return;
        lastDiagnosticsAt = Environment.TickCount64;
        if (session is { } observed)
        {
            EmulationSnapshot snapshot = observed.LatestSnapshot;
            latestHealthSample = new LinuxHealthSample(snapshot.State, snapshot.EmulatedFrameCount,
                snapshot.VideoFrameSequence, displayedFrameSequence, Interlocked.Read(ref audioFramesObserved),
                snapshot.IsPaused || !windowFocused || minimized || controlCenterVisible || showQuickDeck);
            foreach (var hint in sessionHealth.Observe(latestHealthSample, Environment.TickCount64))
            {
                latestHealthHint = hint.Code;
                diagnostics.Record("session.health_hint", new { code = hint.Code, duration_ms = hint.DurationMs,
                    suspected_only = true });
            }
        }
        else { latestHealthSample = null; latestHealthHint = null; }
        if (audioError is not null && options.AudioEnabled && session is not null && Environment.TickCount64 >= nextAudioRetryAt)
        {
            nextAudioRetryAt = Environment.TickCount64 + 3000;
            var previous = Interlocked.Exchange(ref audioOutput, null);
            previous?.Dispose();
            if (EnsureAudioOutput())
            {
                ApplyEmulatorConfiguration();
                diagnostics.Record("audio_recovered");
            }
        }
        diagnostics.Record("progress", new { state = localLinkSession is null ? StateLabel : "LocalLink",
            frame = session?.LatestSnapshot.EmulatedFrameCount ?? localLinkSession?.LatestSnapshot.FrameCount,
            presented = session is null && localLinkSession is not null ? Math.Min(localLinkFrameSequences[0], localLinkFrameSequences[1]) : displayedFrameSequence,
            audio_frames = Interlocked.Read(ref audioFramesObserved), audio = audioOutput?.DriverName, desktop = desktop.DisplayName,
            queued_ms = audioOutput?.QueuedMilliseconds, dropped_blocks = audioOutput?.DroppedBlocks,
            empty_queue_observations = audioOutput?.EmptyQueueObservations, vsync = vsyncEnabled });
    }

    private void OpenFolder(string path) => TryUiAction(() =>
    {
        Directory.CreateDirectory(path);
        if (!SDL.OpenURL(new Uri(Path.GetFullPath(path) + Path.DirectorySeparatorChar).AbsoluteUri))
            throw new IOException(global::AetherBoy.Runtime.Localization.UiText.Get("Could not open the folder. ") + SDL.GetError());
    }, global::AetherBoy.Runtime.Localization.UiText.Get("Folder opened."));

    private void ExportDiagnostics() => TryUiAction(() =>
    {
        string exports = Path.Combine(dataPaths.State, "exports");
        diagnostics.Export(exports);
        OpenFolder(exports);
    }, global::AetherBoy.Runtime.Localization.UiText.Get("Diagnostic ZIP saved to the reports folder."));

    private void ReportError(Exception exception)
    {
        diagnostics.Failure("host_action", exception);
        loadError = DescribeActionFailure(exception);
        statusMessage = loadError;
        Console.Error.WriteLine(exception);
    }

    private static string DescribeActionFailure(Exception exception) => exception.GetBaseException() switch
        {
            UnauthorizedAccessException => global::AetherBoy.Runtime.Localization.UiText.Get("File access was denied. Check permissions and try again."),
            InvalidDataException => global::AetherBoy.Runtime.Localization.UiText.Get("The selected data is invalid. Check the file and try again."),
            IOException => global::AetherBoy.Runtime.Localization.UiText.Get("A file could not be read or saved. Check the drive and try again."),
            _ => global::AetherBoy.Runtime.Localization.UiText.Get("The action failed. Review the local report and try again.")
        };

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
        showQuickDeck = false;
        quickDeckResumeOnClose = false;
        latestHealthSample = null;
        latestHealthHint = null;
        sessionHealth.Reset();
        Interlocked.Exchange(ref audioFramesObserved, 0);
        SaveResumeOnClose();
        FlushPlaytimeOnClose();
        preserveResumeAfterFailure = false;
        FlushSettingsIfDue(force: true);
        globalProfile.ApplyTo(options);
        usingGameProfile = false;
        undoState = null; undoIdentity = null; playedSeconds = 0;
        StopRecording();
        EmulationSession? previous = session;
        session = null;
        if (previous is not null)
        {
            previous.AudioSamplesAvailable -= OnAudioSamplesAvailable;
            DisposeSession(previous);
        }
        storage?.Dispose();
        storage = null;
        onlineLinkTransport?.Dispose(); onlineLinkTransport = null;
        onlineRoomTransport?.Dispose(); onlineRoomTransport = null;
        RequestDiskRefresh();
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
        if (output is null || audioError is not null)
        {
            return;
        }

        try
        {
            if (!ReferenceEquals(sender, session)) return;
            Interlocked.Add(ref audioFramesObserved, eventArgs.SampleCount);
            float[] samples = eventArgs.GetInterleavedSamplesCopy();
            recorder?.Submit(samples, eventArgs.SampleRate, eventArgs.Channels);
            output.Submit(samples, eventArgs.SampleRate, eventArgs.Channels,
                eventArgs.PlaybackGeneration, eventArgs.PlaybackSession);
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
        if (IsLoading) return;
        if (Interlocked.CompareExchange(ref fileDialogOpen, 1, 0) != 0)
        {
            return;
        }

        pressedKeys.Clear();
        if (session is not null && !IsOnlineLink) session.SetTurboAsync(false).GetAwaiter().GetResult();
        statusMessage = pickingEReaderLibrary == EReaderPick.Firmware ? global::AetherBoy.Runtime.Localization.UiText.Get("e-Reader-ROM auswählen")
            : pickingEReaderLibrary != EReaderPick.None || pickingEReaderSession is not null ? global::AetherBoy.Runtime.Localization.UiText.Get("e-Reader-Karte auswählen")
            : pickingBarcode ? global::AetherBoy.Runtime.Localization.UiText.Get("Choose a UTF-8 text file containing one 13-digit barcode.")
            : global::AetherBoy.Runtime.Localization.UiText.Get("Choose a ROM in the file picker. You can also drop a file here.");
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
            pickingPatch = PatchSelection.None;
            pickingFirmware = pickingBatterySave = false;
            pickingLocalLinkPlayer = -1;
            pickingIntroImage = null;
            pickingBarcode = false;
            pickingEReaderSession = null;
            pickingEReaderLibrary = EReaderPick.None; pickingEReaderSetId = null;
            ReportError(exception);
        }
    }

    private void OnFileDialogCompleted(IntPtr userdata, IntPtr fileList, int filter)
    {
        // Keep the dialog busy until the UI thread consumes its result, preserving its purpose.
        if (fileList == IntPtr.Zero)
        {
            dialogSelections.Enqueue(new DialogSelection(null, SDL.GetError()));
            return;
        }
        IntPtr firstPath = Marshal.ReadIntPtr(fileList);
        dialogSelections.Enqueue(new DialogSelection(firstPath == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(firstPath), null));
    }

    private void DrainDialogSelections()
    {
        while (dialogSelections.TryDequeue(out DialogSelection selection))
        {
            if (!string.IsNullOrWhiteSpace(selection.Error))
            {
                if (pickingPatch != PatchSelection.None) { patchFailed = true; patchMessage = global::AetherBoy.Runtime.Localization.UiText.Get("File picker failed: ") + selection.Error; }
                ReportError(new IOException(selection.Error));
            }
            else if (!string.IsNullOrWhiteSpace(selection.Path))
            {
                if (pickingEReaderLibrary != EReaderPick.None) ImportEReaderLibraryFile(selection.Path);
                else if (pickingEReaderSession is not null) ImportEReaderCard(selection.Path);
                else if (pickingBarcode) ImportBarcode(selection.Path);
                else if (pickingIntroImage is bool image) ImportIntroAsset(selection.Path, image);
                else if (pickingLocalLinkPlayer >= 0) SelectLocalLinkRom(selection.Path);
                else if (pickingPatch != PatchSelection.None)
                {
                    SelectPatchFile(selection.Path, pickingPatch);
                }
                else if (pickingBatterySave)
                {
                    try { SelectBatteryImport(selection.Path); } catch (Exception ex) { ReportError(ex); }
                }
                else if (pickingFirmware)
                {
                    try { ImportFirmware(selection.Path); } catch (Exception ex) { ReportError(ex); }
                }
                else TryLoadRom(selection.Path);
            }
            else
            {
                statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("File picker closed. Previous selection kept.");
                if (pickingPatch != PatchSelection.None) patchMessage = statusMessage;
            }
            pickingFirmware = false;
            pickingBarcode = false;
            pickingEReaderSession = null;
            pickingEReaderLibrary = EReaderPick.None; pickingEReaderSetId = null;
            pickingIntroImage = null;
            pickingBatterySave = false;
            pickingPatch = PatchSelection.None;
            pickingLocalLinkPlayer = -1;
            Interlocked.Exchange(ref fileDialogOpen, 0);
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
        global::AetherBoy.Runtime.Localization.UiLabels.Key(
            SDL.GetKeyName(SDL.GetKeyFromScancode(scan, SDL.Keymod.None, false)) ?? SDL.GetScancodeName(scan));

    private SDL.FRect GetGameDestination(VideoGeometry geometry)
    {
        float areaX = sofaMode ? 0 : GameAreaX, areaY = sofaMode ? 0 : GameAreaY;
        float areaWidth = sofaMode ? LogicalWidth : GameAreaWidth, areaHeight = sofaMode ? LogicalHeight : GameAreaHeight;
        // The UI uses logical coordinates, but whole-pixel scaling must use output pixels,
        // including compositor/HiDPI scaling and the logical letterbox transform.
        float presentationScale = SDL.GetRenderOutputSize(renderer, out int outputWidth, out int outputHeight)
            ? Math.Min(outputWidth / (float)LogicalWidth, outputHeight / (float)LogicalHeight) : 1;
        if (presentationScale <= 0) presentationScale = 1;
        float scale = options.GameScale((int)MathF.Floor(areaWidth * presentationScale),
            (int)MathF.Floor(areaHeight * presentationScale), geometry.Width, geometry.Height) / presentationScale;
        float width = geometry.Width * scale;
        float height = geometry.Height * scale;
        return new SDL.FRect
        {
            X = areaX + (areaWidth - width) / 2f,
            Y = areaY + (areaHeight - height) / 2f,
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

    private void DrawAudioPage()
    {
        if (showAudioInspector) { DrawAudioInspector(); return; }
        Ink(300, 194, global::AetherBoy.Runtime.Localization.UiText.Get("Sound output"), 14, Colors.Muted);
        DrawButton(
            300,
            224,
            220,
            44,
            options.AudioEnabled ? global::AetherBoy.Runtime.Localization.UiText.Get("Sound on") : global::AetherBoy.Runtime.Localization.UiText.Get("Sound muted"),
            options.AudioEnabled && audioOutput is not null);

        Ink(300, 302, global::AetherBoy.Runtime.Localization.UiText.Get("Volume"), 14, Colors.Muted);
        DrawButton(300, 326, 72, 40, "-1%", false, options.AudioVolume > 0);
        Ink(426, 340, $"{options.AudioVolume}%", 14, Colors.Text);
        DrawButton(560, 326, 72, 40, "+1%", false, options.AudioVolume < 100);
        Paint(300, 389, 570, 6, Colors.Border);
        Paint(300, 389, options.AudioVolume * 5.7f, 6, Colors.Primary);
        Paint(300 + options.AudioVolume * 5.7f - 7, 381, 14, 22, Colors.Cyan);
        Ink(300, 410, "0%", 14, Colors.Muted);
        Ink(360, 410, global::AetherBoy.Runtime.Localization.UiText.Get("Drag, or use Left / Right for 1% steps."), 14, Colors.Muted);
        Ink(838, 410, "100%", 14, Colors.Muted);

        Ink(300, 452, global::AetherBoy.Runtime.Localization.UiText.Get("Game Boy sound channels"), 14, Colors.Muted);
        bool[] channels =
        {
            options.Channel1Enabled,
            options.Channel2Enabled,
            options.Channel3Enabled,
            options.Channel4Enabled
        };
        for (int index = 0; index < channels.Length; index++)
        {
            DrawButton(300 + (index * 146), 480, 132, 40, global::AetherBoy.Runtime.Localization.UiText.Format("Channel {0}", index + 1), channels[index]);
        }

        string backend = audioOutput is null ? global::AetherBoy.Runtime.Localization.UiText.Get("NOT OPEN") : audioOutput.DriverName;
        Ink(300, 536, global::AetherBoy.Runtime.Localization.UiText.Format("Audio driver: {0}", Truncate(backend, 56)), 14, Colors.Text);
        ActionButton(880, 529, 230, 42, global::AetherBoy.Runtime.Localization.UiText.Get("OPEN AUDIO INSPECTOR"), () => { showAudioInspector = true; focusedControl = -1; });
        if (!string.IsNullOrWhiteSpace(audioError))
        {
            Ink(300, 558, Truncate(global::AetherBoy.Runtime.Localization.UiText.Format("LAST ERROR  {0}", audioError), 80), 14, Colors.Danger);
        }
    }

    private void DrawInputPage()
    {
        if (showController) { DrawControllerPage(); return; }
        DrawInputTabs();
        if (showInputShortcuts) { DrawInputShortcuts(); return; }
        Ink(300, 238, global::AetherBoy.Runtime.Localization.UiText.Get("Choose a key to change it. Escape cancels; occupied keys swap."), 14, Colors.Muted);
        for (int index = 0; index < BindingActions.Length; index++)
        {
            LinuxInputAction action = BindingActions[index];
            float x = 300 + (index / 6) * 330;
            float y = 276 + (index % 6) * 44;
            Ink(x, y + 8, action is LinuxInputAction.L or LinuxInputAction.R ? $"{action} (GBA)" : global::AetherBoy.Runtime.Localization.UiLabels.Input(action.ToString()), 14, Colors.Text);
            DrawButton(x + 124, y, 156, 36,
                rebindingAction == action ? global::AetherBoy.Runtime.Localization.UiText.Get("Press a key...") : BindingLabel(action),
                rebindingAction == action || (rebindingAction is null && focusedControl < 0 && focusedBinding == index));
        }
        DrawButton(300, 548, 210, 40, global::AetherBoy.Runtime.Localization.UiText.Get("Reset keys"), false);
        string controller = gamepad == IntPtr.Zero ? global::AetherBoy.Runtime.Localization.UiText.Get("No controller connected") : SDL.GetGamepadName(gamepad) ?? global::AetherBoy.Runtime.Localization.UiText.Get("Controller connected");
        Ink(536, 548, textRenderer.Fit(controller, 380), 14, Colors.Muted);
        Ink(536, 570, global::AetherBoy.Runtime.Localization.UiText.Get("Use the Controller tab to change its buttons."), 14, Colors.Muted);
    }

    private string BindingLabel(LinuxInputAction action) => KeyLabel(options.Keys[action]);

    private void BeginRebinding(int index)
    {
        focusedControl = -1;
        focusedBinding = index;
        rebindingAction = BindingActions[index];
        pressedKeys.Clear();
        loadError = null;
        statusMessage = global::AetherBoy.Runtime.Localization.UiText.Format("Press a new key for {0}. Esc cancels; occupied keys swap.", global::AetherBoy.Runtime.Localization.UiLabels.Input(rebindingAction.Value.ToString()));
    }

    private void SelectControlCenterPage(ControlCenterPage page)
    {
        pendingPatchLaunch = false;
        if (editingTitleIdentity is not null) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Save or cancel the title before changing sections."); return; }
        if (showOnlineProbePage) LeaveOnlineProbePage();
        if (page != ControlCenterPage.Audio) showAudioInspector = false;
        showGameplayCapture = false;
        showBarcodeBoy = editingBarcode = false;
        showEReader = false;
        ResetSettingsNavigation();
        showController = false;
        showBackups = false;
        showGallery = false;
        showAppearance = false;
        showPatchLab = false;
        rebindingAction = null;
        editingSearch = false;
        editingCheat = false;
        onlineEditingField = TextField.None;
        appearanceEditingField = TextField.None;
        titleEditVersion++; editingTitleIdentity = null;
        SDL.StopTextInput(window);
        rebindingGamepad = null;
        if (page == ControlCenterPage.Library) RefreshLibrary();
        if (page == ControlCenterPage.Saves) RequestDiskRefresh();
        draggingVolume = false;
        controlCenterPage = page;
        statusMessage = "";
        focusedControl = -1;
        FlushSettingsIfDue();
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
        }, global::AetherBoy.Runtime.Localization.UiText.Format("Volume {0}%", options.AudioVolume));
    }

    private void MarkSettingsChanged()
    {
        settingsDirty = true;
        settingsGeneration++;
        settingsChangedAt = Environment.TickCount64;
    }

    private void DrawSavesPage()
    {
        if (IsOnlineLink) { DrawOnlineLinkPage(); return; }
        if (showBackups) { DrawBackupPage(); return; }
        if (showGallery) { DrawStateGallery(); return; }
        ActionButton(890, 232, 210, 42, global::AetherBoy.Runtime.Localization.UiText.Get("BACKUPS / EXPORT"), () => showBackups = true);
        Text(300, 198, global::AetherBoy.Runtime.Localization.UiText.Get("ACTIVE SAVE-STATE SLOT"), 161, 173, 192);
        for (int index = 0; index < 5; index++)
        {
            int slot = index + 1;
            bool exists = StateCard(slot)?.Exists == true;
            DrawButton(
                300 + (index * 114),
                232,
                100,
                42,
                StateCard(slot) is null ? $"{slot} ..." : StateCard(slot)?.Error is not null ? global::AetherBoy.Runtime.Localization.UiText.Format("{0} ERROR", slot) : exists ? global::AetherBoy.Runtime.Localization.UiText.Format("{0} SAVED", slot) : global::AetherBoy.Runtime.Localization.UiText.Format("{0} EMPTY", slot),
                options.SaveSlot == slot);
        }

        var selectedCard = StateCard(options.SaveSlot);
        Text(300, 286, selectedCard is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Loading save information…") : selectedCard.Error is not null ? global::AetherBoy.Runtime.Localization.UiText.Get("State cannot be read. Refresh or select another slot.") : selectedCard.Exists ? global::AetherBoy.Runtime.Localization.UiText.Format("Slot {0} saved {1:yyyy-MM-dd HH:mm}", options.SaveSlot, selectedCard.SavedAt) : global::AetherBoy.Runtime.Localization.UiText.Format("Slot {0} is empty. Save here with F5.", options.SaveSlot), 161, 173, 192);
        DrawButton(300, 342, 180, 44, global::AetherBoy.Runtime.Localization.UiText.Get("SAVE [F5]"), false, session is not null);
        DrawButton(494, 342, 180, 44, global::AetherBoy.Runtime.Localization.UiText.Get("LOAD [F8]"), false, HasSelectedState);
        DrawButton(688, 342, 180, 44, global::AetherBoy.Runtime.Localization.UiText.Get("REWIND [F7]"), false, session is not null);

        Text(300, 438, global::AetherBoy.Runtime.Localization.UiText.Get("BATTERY SAVE"), 161, 173, 192);
        EmulationSnapshot? snapshot = session?.LatestSnapshot;
        string battery = snapshot?.Rom?.BatterySave.IsEnabled == true
            ? global::AetherBoy.Runtime.Localization.UiText.Format("ACTIVE · {0} BYTES", snapshot.Rom.BatterySave.ExpectedLength)
            : session is null ? global::AetherBoy.Runtime.Localization.UiText.Get("NO ROM LOADED") : global::AetherBoy.Runtime.Localization.UiText.Get("CARTRIDGE HAS NO BATTERY RAM");
        Text(300, 470, battery, 218, 222, 242);
        Text(300, 514, global::AetherBoy.Runtime.Localization.UiText.Get("Saves follow cartridge content, even when you move the ROM."), 161, 173, 192);
        ActionButton(565, 560, 250, 42, global::AetherBoy.Runtime.Localization.UiText.Get("GALLERY / RESUME"), () => { showGallery = true; focusedControl = -1; RequestDiskRefresh(); });
        ActionButton(835, 560, 270, 42, global::AetherBoy.Runtime.Localization.UiText.Get("UNDO LAST LOAD"), UndoLoad, enabled: undoState is not null && stateOperation is null);
        ActionButton(300, 560, 242, 42, global::AetherBoy.Runtime.Localization.UiText.Get("OPEN SAVE FOLDER"), () => OpenFolder(storage is null ? Path.Combine(dataPaths.Data, "saves") : Path.GetDirectoryName(storage.SavePath)!));
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..Math.Max(0, maximumLength - 3)] + "...";

    private void QuickSave() => QueueSaveState(options.SaveSlot);
    private void QuickLoad() => QueueLoadState(options.SaveSlot);

    private void Rewind()
    {
        if (IsOnlineLink) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Rewind is disabled during Online Link."); return; }
        if (session is null || stateOperation is not null || IsLoading)
        {
            return;
        }

        try
        {
            if (!session.RewindAsync().GetAwaiter().GetResult())
            {
                statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("REWIND BUFFER IS EMPTY");
                return;
            }

            undoState = null; undoIdentity = null;
            audioOutput?.Clear();
            displayedFrameSequence = 0;
            diagnostics.Record("rewind");
            statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("REWOUND · ONE STEP");
        }
        catch (Exception exception)
        {
            statusMessage = global::AetherBoy.Runtime.Localization.UiText.Format("REWIND FAILED · {0}", global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message));
            Console.Error.WriteLine(exception);
        }
    }

    private void SelectSaveSlot(int slot)
    {
        options.SelectSaveSlot(slot);
        MarkSettingsChanged();
        statusMessage = global::AetherBoy.Runtime.Localization.UiText.Format("SAVE STATE SLOT {0}", slot);
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
            statusMessage = global::AetherBoy.Runtime.Localization.UiText.Format("GAMEPAD ERROR · {0}", SDL.GetError());
            return;
        }

        gamepad = candidate;
        SelectGamepadProfile(instanceId);
        diagnostics.Record("controller_connected");
        statusMessage = global::AetherBoy.Runtime.Localization.UiText.Format("GAMEPAD READY · {0}", SDL.GetGamepadName(gamepad) ?? "SDL3");
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
        DescribeAccessibleText(x, y, value);
        textRenderer.Draw(x, y, textRenderer.Fit(value, LogicalWidth - x - 24), red, green, blue);
    }
}
