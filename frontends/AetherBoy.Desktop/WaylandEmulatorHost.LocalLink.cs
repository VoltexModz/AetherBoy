using System.Runtime.InteropServices;
using System.Text;
using AetherBoy.Runtime;
using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private readonly string?[] localLinkRoms = new string?[2];
    private readonly IntPtr[] localLinkTextures = new IntPtr[2];
    private readonly long[] localLinkFrameSequences = new long[2];
    private readonly GameBoyButtons[] localLinkPostedButtons = new GameBoyButtons[2];
    private readonly GameBoyAdvanceButtons[] localLinkPostedAdvance = new GameBoyAdvanceButtons[2];
    private readonly Task?[] localLinkInputTasks = new Task?[2];
    private readonly Task?[] localLinkAdvanceTasks = new Task?[2];
    private int[][] localLinkPixels = [[], []];
    private VideoGeometry localLinkGeometry = VideoGeometry.GameBoy;
    private readonly LinuxLocalLinkAudioMixer localLinkAudioMixer = new();
    private LocalLinkSession? localLinkSession;
    private Task<LinuxLocalLinkPlan>? localLinkPlanTask;
    private Task<LocalLinkSession>? localLinkStartupTask;
    private Task? localLinkStopTask;
    private Exception? localLinkShutdownError;
    private CancellationTokenSource? localLinkStartCancellation;
    private IntPtr secondGamepad;
    private LinuxGamepadProfile secondGamepadProfile = new();
    private bool showLocalLinkPage;
    private int pickingLocalLinkPlayer = -1;
    private bool resumeLocalLinkAfterSettings;
    private bool resumeLocalLinkAfterFocus;
    private string? localLinkMessage;
    private string? localLinkPreviousRom;

    private void OpenLocalLinkPage()
    {
        if (IsOnlineLink) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("End Online Link before opening local link."); return; }
        if (IsLoading) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Wait for the current game to finish loading."); return; }
        if (controlCenterVisible) CloseControlCenter();
        if (session is not null && localLinkRoms[0] is null) localLinkRoms[0] = romPath;
        showLocalLinkPage = true;
        focusedControl = -1;
        OpenSecondLocalGamepad();
    }

    private void PickLocalLinkRom(int player)
    {
        if (localLinkSession is not null || localLinkPlanTask is not null || localLinkStartupTask is not null || fileDialogOpen != 0) return;
        pickingLocalLinkPlayer = player;
        ShowRomDialog();
    }

    private void SelectLocalLinkRom(string path)
    {
        if (pickingLocalLinkPlayer is not (0 or 1)) return;
        localLinkRoms[pickingLocalLinkPlayer] = path;
        localLinkMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Game selected. Choose the other game, then start local link.");
    }

    private void StartLocalLink()
    {
        if (localLinkSession is not null || localLinkPlanTask is not null || localLinkStartupTask is not null || localLinkStopTask is not null) return;
        if (localLinkRoms[0] is null || localLinkRoms[1] is null)
        { localLinkMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Choose a game for each player before starting."); return; }
        if (IsOnlineLink || IsLoading || stateOperation is not null)
        { localLinkMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Finish the current game operation before starting local link."); return; }
        string first = localLinkRoms[0]!, second = localLinkRoms[1]!;
        localLinkMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Checking both games before touching any save files…");
        localLinkStartCancellation = new CancellationTokenSource();
        localLinkPlanTask = Task.Run(() => new LinuxLocalLinkStorage(dataPaths).CreatePlan(first, second));
    }

    private void UpdateLocalLink()
    {
        if (localLinkStopTask is { IsCompleted: true } stopped)
        {
            try { stopped.GetAwaiter().GetResult(); }
            catch (Exception error) { RecordLocalLinkShutdownFailure(error); }
            localLinkStopTask = null;
        }
        if (localLinkPlanTask is { IsCompleted: true } planned)
        {
            localLinkPlanTask = null;
            try
            {
                LinuxLocalLinkPlan plan = planned.GetAwaiter().GetResult();
                if (localLinkStartCancellation?.IsCancellationRequested == true)
                { localLinkMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Local link start cancelled. No saves were changed."); return; }
                FlushSettingsIfDue(force: true);
                if (settingsDirty) { localLinkMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Settings could not be saved. Retry before starting local link."); return; }
                localLinkPreviousRom = romPath;
                CloseSession(); // Releases the single-player save owner before either link owner starts.
                bool audioReady = options.AudioEnabled && EnsureAudioOutput();
                EmulatorConfiguration config = options.CreateEmulatorConfiguration(audioReady);
                int palette = options.PaletteIndex;
                localLinkMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Preparing separate saves for both players…");
                CancellationToken token = localLinkStartCancellation!.Token;
                localLinkStartupTask = Task.Run(async () =>
                {
                    LocalLinkSession? created = null;
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        using (LinuxRomStorage.Open(dataPaths, plan.FirstRomPath)) { }
                        if (!plan.SameRom) using (LinuxRomStorage.Open(dataPaths, plan.SecondRomPath)) { }
                        token.ThrowIfCancellationRequested();
                        var player1 = new LocalLinkPlayerConfiguration(plan.FirstRomPath, plan.FirstSavePath, null, config, palette)
                        { WriteLeasePath = plan.FirstLeasePath };
                        var player2 = new LocalLinkPlayerConfiguration(plan.SecondRomPath, plan.SecondSavePath, null, config, palette)
                        { WriteLeasePath = plan.SecondLeasePath };
                        created = new LocalLinkSession(player1, player2);
                        await created.Ready.WaitAsync(token).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                        return created;
                    }
                    catch { if (created is not null) await created.DisposeAsync().ConfigureAwait(false); throw; }
                });
            }
            catch (Exception error)
            {
                diagnostics.Failure("local_link_plan", error);
                localLinkMessage = global::AetherBoy.Runtime.Localization.UiText.Get("The two games could not be prepared. Check that both files are valid and their save folders are available.");
                RestorePreviousLocalGame();
            }
            finally
            {
                if (localLinkStartupTask is null)
                { localLinkStartCancellation?.Dispose(); localLinkStartCancellation = null; }
            }
        }
        if (localLinkStartupTask is { IsCompleted: true } startup)
        {
            localLinkStartupTask = null;
            try
            {
                LocalLinkSession ready = startup.GetAwaiter().GetResult();
                if (localLinkStartCancellation?.IsCancellationRequested == true)
                { localLinkStopTask = ready.ShutdownAsync(); return; }
                localLinkSession = ready;
                localLinkShutdownError = null;
                localLinkFrameSequences[0] = localLinkFrameSequences[1] = 0;
                localLinkPostedButtons[0] = localLinkPostedButtons[1] = GameBoyButtons.None;
                localLinkPostedAdvance[0] = localLinkPostedAdvance[1] = GameBoyAdvanceButtons.None;
                localLinkAudioMixer.Clear();
                ready.AudioSamplesAvailable += OnLocalLinkAudio;
                localLinkMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Both games are running. The cable is connected locally.");
                audioOutput?.Clear();
            }
            catch (Exception error)
            {
                diagnostics.Failure("local_link_start", error);
                localLinkMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Local link could not start. Check the local report and try the two games again.");
                RestorePreviousLocalGame();
            }
            finally { localLinkStartCancellation?.Dispose(); localLinkStartCancellation = null; }
        }
        if (localLinkSession is not { } active) return;
        if (active.State is SessionState.Stopped or SessionState.Faulted)
        {
            localLinkMessage = active.Fault is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Local link ended. Your separate saves were kept.")
                : global::AetherBoy.Runtime.Localization.UiText.Get("Local link stopped with an error. Check the local report and both save folders.");
            StopLocalLink();
            return;
        }
        PostLocalLinkInput(active);
        EnsureLocalLinkTextures(active.GetVideoGeometry(0));
        for (int player = 0; player < 2; player++)
            if (active.TryCopyLatestFrame(player, localLinkPixels[player], ref localLinkFrameSequences[player]))
            {
                ReadOnlySpan<byte> pixels = MemoryMarshal.AsBytes(localLinkPixels[player].AsSpan());
                if (!SDL.UpdateTexture(localLinkTextures[player], IntPtr.Zero, pixels, localLinkGeometry.Width * sizeof(int)))
                    throw new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("Could not upload a local link frame: ") + SDL.GetError());
            }
    }

    private void RestorePreviousLocalGame()
    {
        if (localLinkPreviousRom is { } previous && session is null && !IsLoading)
        {
            showLocalLinkPage = false;
            BeginRomLoad(previous);
        }
        localLinkPreviousRom = null;
    }

    private void StopLocalLink()
    {
        localLinkStartCancellation?.Cancel();
        if (localLinkSession is { } active)
        {
            active.AudioSamplesAvailable -= OnLocalLinkAudio;
            localLinkSession = null;
            localLinkStopTask = active.ShutdownAsync();
        }
        localLinkAudioMixer.Clear();
        audioOutput?.Clear();
        ReleaseLocalLinkTextures();
        localLinkPostedButtons[0] = localLinkPostedButtons[1] = GameBoyButtons.None;
        localLinkPostedAdvance[0] = localLinkPostedAdvance[1] = GameBoyAdvanceButtons.None;
        pressedKeys.Clear();
    }

    private void LeaveLocalLinkPage()
    {
        StopLocalLink();
        showLocalLinkPage = false;
        CloseSecondLocalGamepad();
        focusedControl = -1;
    }

    private Exception? FinishLocalLinkForDisposal()
    {
        StopLocalLink();
        // Every startup/owner task runs in the background. Returning from Dispose
        // would let process exit interrupt save finalization, even after a timeout
        // or a detached continuation. Drain them before releasing diagnostics/SDL.
        try
        {
            try { localLinkPlanTask?.GetAwaiter().GetResult(); }
            catch (OperationCanceledException error) when (IsExpectedLocalLinkStartCancellation(localLinkPlanTask, error)) { }
            catch (Exception error) { RecordLocalLinkShutdownFailure(error, "local_link_plan"); }

            LocalLinkSession? unclaimed = null;
            try
            {
                if (localLinkStartupTask is { } startup)
                    unclaimed = startup.GetAwaiter().GetResult();
            }
            // The startup worker awaits its owner's disposal before completing
            // cancellation, so a cancelled task also guarantees released leases.
            catch (OperationCanceledException error) when (IsExpectedLocalLinkStartCancellation(localLinkStartupTask, error)) { }
            catch (Exception error) { RecordLocalLinkShutdownFailure(error); }

            // Cancellation of startup must never hide a failure during saving.
            try { unclaimed?.ShutdownAsync().GetAwaiter().GetResult(); }
            catch (Exception error) { RecordLocalLinkShutdownFailure(error); }

            try { localLinkStopTask?.GetAwaiter().GetResult(); }
            catch (Exception error) { RecordLocalLinkShutdownFailure(error); }
        }
        finally
        {
            localLinkPlanTask = null;
            localLinkStartupTask = null;
            localLinkStopTask = null;
            localLinkStartCancellation?.Dispose();
            localLinkStartCancellation = null;
        }
        return localLinkShutdownError;
    }

    private bool IsExpectedLocalLinkStartCancellation(Task? task, OperationCanceledException error) =>
        task?.IsCanceled == true && localLinkStartCancellation is { IsCancellationRequested: true } cancellation &&
        error.CancellationToken == cancellation.Token;

    private void RecordLocalLinkShutdownFailure(Exception error, string action = "local_link_shutdown")
    {
        localLinkShutdownError ??= error;
        diagnostics.Failure(action, error);
        statusMessage = localLinkMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Local link could not close cleanly. Check the local report.");
    }

    private void EnsureLocalLinkTextures(VideoGeometry geometry)
    {
        if (localLinkTextures[0] != IntPtr.Zero && localLinkGeometry == geometry) return;
        ReleaseLocalLinkTextures();
        localLinkGeometry = geometry;
        for (int i = 0; i < 2; i++)
        {
            localLinkTextures[i] = SDL.CreateTexture(renderer, SDL.PixelFormat.ARGB8888,
                SDL.TextureAccess.Streaming, geometry.Width, geometry.Height);
            if (localLinkTextures[i] == IntPtr.Zero) throw new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("Could not create local link video: ") + SDL.GetError());
            SDL.SetTextureScaleMode(localLinkTextures[i], options.TextureScaleMode);
            localLinkPixels[i] = new int[geometry.PixelCount];
            localLinkFrameSequences[i] = 0;
        }
    }

    private void ReleaseLocalLinkTextures()
    {
        for (int i = 0; i < 2; i++)
        {
            if (localLinkTextures[i] != IntPtr.Zero) SDL.DestroyTexture(localLinkTextures[i]);
            localLinkTextures[i] = IntPtr.Zero;
            localLinkPixels[i] = [];
            localLinkFrameSequences[i] = 0;
        }
    }

    private void DrawLocalLinkPage()
    {
        Paint(0, 0, LogicalWidth, LogicalHeight, Colors.Void);
        Paint(0, 0, LogicalWidth, 82, Colors.Chrome);
        Mark(22, 14, 48);
        Ink(84, 21, global::AetherBoy.Runtime.Localization.UiText.Get("Local Link"), 24, Colors.Text, true);
        ActionButton(LogicalWidth - 218, 20, 190, 42, localLinkSession is null ? global::AetherBoy.Runtime.Localization.UiText.Get("BACK TO APP") : global::AetherBoy.Runtime.Localization.UiText.Get("END AND RETURN"), LeaveLocalLinkPage);
        Ink(30, 96, global::AetherBoy.Runtime.Localization.UiText.Get("Choose two GB/GBC games or two GBA games. Each player keeps a separate save."), 14, Colors.Muted);
        float cardWidth = (LogicalWidth - 90) / 2f;
        for (int player = 0; player < 2; player++)
        {
            float x = 30 + player * (cardWidth + 30);
            Panel(x, 130, cardWidth, 425, stage: true);
            Ink(x + 20, 148, global::AetherBoy.Runtime.Localization.UiText.Format("PLAYER {0}", player + 1), 14, Colors.Cyan, true);
                Ink(x + 20, 181, textRenderer.Fit(localLinkRoms[player] is null ? global::AetherBoy.Runtime.Localization.UiText.Get("No game selected") : Path.GetFileName(localLinkRoms[player])!, cardWidth - 40, 16), 16, Colors.Text, true);
            ActionButton(x + 20, 218, cardWidth - 40, 38, global::AetherBoy.Runtime.Localization.UiText.Get("CHOOSE GAME"), () => PickLocalLinkRom(player),
                enabled: localLinkSession is null && localLinkPlanTask is null && localLinkStartupTask is null);
            float videoX = x + 20, videoY = 270, videoWidth = cardWidth - 40, videoHeight = 265;
            Paint(videoX, videoY, videoWidth, videoHeight, Colors.Chrome);
            if (localLinkSession is not null && localLinkTextures[player] != IntPtr.Zero)
            {
                float scale = Math.Min(videoWidth / localLinkGeometry.Width, videoHeight / localLinkGeometry.Height);
                SDL.FRect destination = new()
                {
                    X = videoX + (videoWidth - localLinkGeometry.Width * scale) / 2,
                    Y = videoY + (videoHeight - localLinkGeometry.Height * scale) / 2,
                    W = localLinkGeometry.Width * scale, H = localLinkGeometry.Height * scale
                };
                SDL.RenderTexture(renderer, localLinkTextures[player], IntPtr.Zero, in destination);
            }
            else Center(x + cardWidth / 2, 386, global::AetherBoy.Runtime.Localization.UiText.Get("Waiting for game video"), 14, Colors.Muted);
        }
        bool busy = localLinkPlanTask is not null || localLinkStartupTask is not null || localLinkStopTask is not null;
        ActionButton(30, 574, 230, 44, busy ? global::AetherBoy.Runtime.Localization.UiText.Get("PREPARING GAMES…") : global::AetherBoy.Runtime.Localization.UiText.Get("START LOCAL LINK"), StartLocalLink,
            enabled: !busy && localLinkSession is null && localLinkRoms[0] is not null && localLinkRoms[1] is not null);
        ActionButton(278, 574, 190, 44, localLinkSession?.LatestSnapshot.IsPaused == true ? global::AetherBoy.Runtime.Localization.UiText.Get("RESUME BOTH") : global::AetherBoy.Runtime.Localization.UiText.Get("PAUSE BOTH"),
            ToggleLocalLinkPause, enabled: localLinkSession is not null);
        ActionButton(486, 574, 190, 44, localLinkSession?.LatestSnapshot.Connected == true ? global::AetherBoy.Runtime.Localization.UiText.Get("UNPLUG CABLE") : global::AetherBoy.Runtime.Localization.UiText.Get("CONNECT CABLE"),
            ToggleLocalLinkCable, enabled: localLinkSession is not null);
        ActionButton(694, 574, 210, 44, global::AetherBoy.Runtime.Localization.UiText.Get("END LOCAL LINK"), StopLocalLink, enabled: localLinkSession is not null || busy);
        Ink(30, 642, textRenderer.Fit(localLinkMessage ?? global::AetherBoy.Runtime.Localization.UiText.Get("Keyboard: player 1. One controller: player 2. Two controllers: one per player."), LogicalWidth - 60, 14), 14, Colors.Muted);
        Ink(30, 674, global::AetherBoy.Runtime.Localization.UiText.Get("Save in each game. Save states, rewind and turbo are unavailable during local link."), 13, Colors.Muted);
        Ink(30, 702, global::AetherBoy.Runtime.Localization.UiText.Get("A connected cable does not prove that a game trade works. Test GB/GBC and GBA separately."), 13, Colors.Muted);
    }

    private void ToggleLocalLinkPause()
    {
        if (localLinkSession is not { } active) return;
        _ = active.SetPausedAsync(!active.LatestSnapshot.IsPaused);
        audioOutput?.Clear(); localLinkAudioMixer.Clear();
    }

    private void ToggleLocalLinkCable()
    {
        if (localLinkSession is not { } active) return;
        _ = active.SetConnectedAsync(!active.LatestSnapshot.Connected);
        audioOutput?.Clear(); localLinkAudioMixer.Clear();
    }

    private void OnLocalLinkAudio(object? sender, LocalLinkAudioEventArgs e)
    {
        if (!ReferenceEquals(sender, localLinkSession) || audioOutput is null || audioError is not null) return;
        try
        {
            Interlocked.Add(ref audioFramesObserved, e.Audio.SampleCount);
            var mixed = localLinkAudioMixer.Add(e.Player, e.Audio);
            if (mixed is { } block)
                audioOutput.Submit(block.Samples, block.Rate, block.Channels, block.Generation, block.Session);
        }
        catch (Exception) { audioError = global::AetherBoy.Runtime.Localization.UiText.Get("Local link audio stopped. Video and saves are still active."); }
    }

    private void PostLocalLinkInput(LocalLinkSession active)
    {
        for (int player = 0; player < 2; player++)
        {
            if (localLinkInputTasks[player] is { } inputTask)
            {
                if (!inputTask.IsCompleted) continue;
                try { inputTask.GetAwaiter().GetResult(); } catch { localLinkMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Controller input stopped. Check the local link session."); }
                localLinkInputTasks[player] = null;
            }
            GameBoyButtons buttons = ReadLocalLinkButtons(player);
            if (buttons != localLinkPostedButtons[player])
            {
                localLinkInputTasks[player] = active.SetButtonsAsync(player, buttons);
                localLinkPostedButtons[player] = buttons;
            }
            if (localLinkAdvanceTasks[player] is { } advanceTask)
            {
                if (!advanceTask.IsCompleted) continue;
                try { advanceTask.GetAwaiter().GetResult(); } catch { localLinkMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Shoulder input stopped. Check the local link session."); }
                localLinkAdvanceTasks[player] = null;
            }
            GameBoyAdvanceButtons shoulders = active.IsGameBoyAdvance ? ReadLocalLinkAdvance(player) : GameBoyAdvanceButtons.None;
            if (shoulders != localLinkPostedAdvance[player])
            {
                localLinkAdvanceTasks[player] = active.SetGameBoyAdvanceButtonsAsync(player, shoulders);
                localLinkPostedAdvance[player] = shoulders;
            }
        }
    }

    private GameBoyButtons ReadLocalLinkButtons(int player)
    {
        if (!windowFocused || controlCenterVisible || fileDialogOpen != 0) return GameBoyButtons.None;
        GameBoyButtons buttons = GameBoyButtons.None;
        if (player == 0)
        {
            AddKey(options.Keys[LinuxInputAction.Up], GameBoyButtons.Up, ref buttons);
            AddKey(options.Keys[LinuxInputAction.Down], GameBoyButtons.Down, ref buttons);
            AddKey(options.Keys[LinuxInputAction.Left], GameBoyButtons.Left, ref buttons);
            AddKey(options.Keys[LinuxInputAction.Right], GameBoyButtons.Right, ref buttons);
            AddKey(options.Keys[LinuxInputAction.A], GameBoyButtons.A, ref buttons);
            AddKey(options.Keys[LinuxInputAction.B], GameBoyButtons.B, ref buttons);
            AddKey(options.Keys[LinuxInputAction.Select], GameBoyButtons.Select, ref buttons);
            AddKey(options.Keys[LinuxInputAction.Start], GameBoyButtons.Start, ref buttons);
        }
        IntPtr pad = player == 0 ? (secondGamepad == IntPtr.Zero ? IntPtr.Zero : gamepad)
            : secondGamepad == IntPtr.Zero ? gamepad : secondGamepad;
        LinuxGamepadProfile profile = pad == secondGamepad ? secondGamepadProfile : gamepadProfile;
        if (pad == IntPtr.Zero || !SDL.GamepadConnected(pad)) return buttons;
        foreach ((LinuxInputAction action, GameBoyButtons mapped) in new[]
        {
            (LinuxInputAction.Up, GameBoyButtons.Up), (LinuxInputAction.Down, GameBoyButtons.Down),
            (LinuxInputAction.Left, GameBoyButtons.Left), (LinuxInputAction.Right, GameBoyButtons.Right),
            (LinuxInputAction.A, GameBoyButtons.A), (LinuxInputAction.B, GameBoyButtons.B),
            (LinuxInputAction.Select, GameBoyButtons.Select), (LinuxInputAction.Start, GameBoyButtons.Start)
        })
            if (SDL.GetGamepadButton(pad, profile.Buttons[action])) buttons |= mapped;
        short x = SDL.GetGamepadAxis(pad, SDL.GamepadAxis.LeftX), y = SDL.GetGamepadAxis(pad, SDL.GamepadAxis.LeftY);
        if (x < -profile.Deadzone) buttons |= GameBoyButtons.Left;
        if (x > profile.Deadzone) buttons |= GameBoyButtons.Right;
        if (y < -profile.Deadzone) buttons |= GameBoyButtons.Up;
        if (y > profile.Deadzone) buttons |= GameBoyButtons.Down;
        return buttons;
    }

    private GameBoyAdvanceButtons ReadLocalLinkAdvance(int player)
    {
        if (!windowFocused || controlCenterVisible || fileDialogOpen != 0) return GameBoyAdvanceButtons.None;
        GameBoyAdvanceButtons shoulders = GameBoyAdvanceButtons.None;
        if (player == 0)
        {
            if (pressedKeys.Contains(options.Keys[LinuxInputAction.L])) shoulders |= GameBoyAdvanceButtons.L;
            if (pressedKeys.Contains(options.Keys[LinuxInputAction.R])) shoulders |= GameBoyAdvanceButtons.R;
        }
        IntPtr pad = player == 0 ? (secondGamepad == IntPtr.Zero ? IntPtr.Zero : gamepad)
            : secondGamepad == IntPtr.Zero ? gamepad : secondGamepad;
        LinuxGamepadProfile profile = pad == secondGamepad ? secondGamepadProfile : gamepadProfile;
        if (pad != IntPtr.Zero && SDL.GamepadConnected(pad))
        {
            if (SDL.GetGamepadButton(pad, profile.Buttons[LinuxInputAction.L])) shoulders |= GameBoyAdvanceButtons.L;
            if (SDL.GetGamepadButton(pad, profile.Buttons[LinuxInputAction.R])) shoulders |= GameBoyAdvanceButtons.R;
        }
        return shoulders;
    }

    private void OpenSecondLocalGamepad()
    {
        if (!showLocalLinkPage || secondGamepad != IntPtr.Zero || gamepad == IntPtr.Zero) return;
        uint firstId = SDL.GetGamepadID(gamepad);
        uint[]? devices = SDL.GetGamepads(out _);
        if (devices is null) return;
        foreach (uint id in devices)
        {
            if (id == firstId) continue;
            IntPtr candidate = SDL.OpenGamepad(id);
            if (candidate == IntPtr.Zero) continue;
            secondGamepad = candidate;
            byte[] buffer = new byte[33];
            SDL.GUIDToString(SDL.GetGamepadGUIDForID(id), buffer, buffer.Length);
            string identity = Encoding.ASCII.GetString(buffer).TrimEnd('\0');
            secondGamepadProfile = options.Gamepads.TryGetValue(identity, out var profile) && profile is not null
                ? profile.Validated() : new LinuxGamepadProfile();
            break;
        }
    }

    private void CloseSecondLocalGamepad()
    {
        if (secondGamepad == IntPtr.Zero) return;
        SDL.CloseGamepad(secondGamepad);
        secondGamepad = IntPtr.Zero;
    }
}
