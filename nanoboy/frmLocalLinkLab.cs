using System;
using System.Collections.Generic;
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
using nanoboy.Storage;

namespace nanoboy;

/// <summary>Two independent machines, one coordinated local session. No network listener.</summary>
internal sealed class frmLocalLinkLab : Form
{
    private readonly NanoboySettings settings;
    private readonly Func<bool> prepareMainWindow;
    private readonly WindowsLocalLinkStorage storage = new(WindowsDataPaths.Default);
    private readonly GameDisplayControl[] displays = new GameDisplayControl[2];
    private readonly Label[] titles = new Label[2];
    private readonly AetherButton[] pickButtons = new AetherButton[2], playerButtons = new AetherButton[2];
    private readonly string?[] selectedRoms = new string?[2];
    private readonly int[][] pixels = [new int[160 * 144], new int[160 * 144]];
    private readonly long[] frameSequences = new long[2];
    private readonly GameBoyButtons[] sentButtons = new GameBoyButtons[2];
    private readonly GameBoyAdvanceButtons[] sentAdvanceButtons = new GameBoyAdvanceButtons[2];
    private readonly bool[] padArmed = new bool[2];
    private readonly HostGamepadState[] previousPads = new HostGamepadState[2];
    private readonly HashSet<Keys> heldKeys = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    private readonly Label status, savePolicy;
    private readonly AetherButton startButton, pauseButton, cableButton, stopButton, audioButton;
    private LocalLinkSession? session;
    private NAudioSoundOut? audioOutput;
    private Task? stopping;
    private bool starting, allowClose, closingRequested, faultShown;
    private int keyboardPlayer;
    private int audioPlayer = -1;
    private string? actionMessage;

    internal frmLocalLinkLab(NanoboySettings settings, string? initialRom, Func<bool> prepareMainWindow)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.prepareMainWindow = prepareMainWindow ?? throw new ArgumentNullException(nameof(prepareMainWindow));
        Name = "localLinkLab";
        Text = "Local Link Lab · GB / GBC / GBA";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1092, 708);
        KeyPreview = true;
        Controls.Add(new Label { Bounds = new(24, 12, 1032, 40),
            Text = global::AetherBoy.Runtime.Localization.UiText.Get("ZWEI CARTRIDGES // ein PC · zwei eigenständige Spielstände · experimenteller Link\r\n") +
                global::AetherBoy.Runtime.Localization.UiText.Get("Zwei GB/GBC oder zwei GBA. Kompatibler Kabelmodus im Spiel nötig; kein Wireless, Internet oder Vier-Spieler-Modus.") });
        for (int index = 0; index < 2; index++)
        {
            int player = index;
            var card = new AetherSurfacePanel { Name = "linkPlayerCard" + index,
                Bounds = new(24 + index * 532, 62, 512, 438), AccentEdge = true };
            titles[index] = new Label { Name = "linkRomTitle" + index, Bounds = new(16, 12, 480, 30),
                Text = global::AetherBoy.Runtime.Localization.UiText.Format("PLAYER {0} · ROM AUSWÄHLEN", index + 1), Tag = "accent", AutoEllipsis = true };
            pickButtons[index] = Button(card, "linkPickRom" + index, global::AetherBoy.Runtime.Localization.UiText.Get("ROM WÄHLEN"), new(16, 46, 228, 36), () => PickRom(player));
            playerButtons[index] = Button(card, "linkKeyboardPlayer" + index, global::AetherBoy.Runtime.Localization.UiText.Format("TASTATUR → P{0}", index + 1),
                new(260, 46, 228, 36), () => SelectKeyboardPlayer(player));
            displays[index] = new GameDisplayControl { Name = "linkDisplay" + index,
                Bounds = new(16, 96, 480, 326), GpuEnabled = settings.GpuRendering,
                // One host window: two independent blocking VSync presents could halve frame rate.
                VSyncEnabled = false, IntegerScaling = settings.IntegerScaling,
                Filter = (GameDisplayFilter)settings.DisplayFilterIndex };
            displays[index].MouseDown += (_, _) => SelectKeyboardPlayer(player);
            card.Controls.Add(titles[index]); card.Controls.Add(displays[index]); Controls.Add(card);
        }
        savePolicy = new Label { Name = "linkSavePolicy", Bounds = new(24, 510, 1032, 42),
            Text = global::AetherBoy.Runtime.Localization.UiText.Get("Verschiedene ROMs: beide bisherigen AppData-Spielstände. Gleiche ROM: P2 erhält einen getrennten Link-Spielstand.") };
        Controls.Add(savePolicy);
        startButton = Button(this, "linkStart", global::AetherBoy.Runtime.Localization.UiText.Get("LINK STARTEN"), new(24, 562, 190, 42), () => _ = StartAsync());
        startButton.Kind = AetherButtonKind.Primary;
        pauseButton = Button(this, "linkPause", global::AetherBoy.Runtime.Localization.UiText.Get("BEIDE PAUSIEREN"), new(226, 562, 198, 42), () => _ = TogglePauseAsync());
        cableButton = Button(this, "linkCable", global::AetherBoy.Runtime.Localization.UiText.Get("KABEL TRENNEN"), new(436, 562, 192, 42), () => _ = ToggleCableAsync());
        audioButton = Button(this, "linkAudio", global::AetherBoy.Runtime.Localization.UiText.Get("AUDIO AUS"), new(640, 562, 166, 42), CycleAudio);
        stopButton = Button(this, "linkStop", global::AetherBoy.Runtime.Localization.UiText.Get("SITZUNG BEENDEN"), new(818, 562, 238, 42), () => _ = StopAsync());
        status = new Label { Name = "linkStatus", Bounds = new(24, 616, 1032, 28), Tag = "value", AutoEllipsis = true };
        Controls.Add(status);
        Controls.Add(new Label { Bounds = new(24, 650, 842, 44),
            Text = global::AetherBoy.Runtime.Localization.UiText.Get("F1/F2 oder Bild anklicken: Tastatur-Spieler wählen. Controller 1/2 → Spieler 1/2. Esc: beide pausieren.\r\n") +
                global::AetherBoy.Runtime.Localization.UiText.Get("GBA: konfigurierte L/R-Tasten (Standard Q/E). Im Spiel speichern; keine Save States, kein Rewind/Turbo/Einzel-Reset.") });
        var close = Button(this, "linkClose", global::AetherBoy.Runtime.Localization.UiText.Get("ZURÜCK"), new(896, 654, 160, 36), Close);
        CancelButton = close;
        AetherDialog.Apply(this, global::AetherBoy.Runtime.Localization.UiText.Get("LOCAL LINK // EXPERIMENTAL"), global::AetherBoy.Runtime.Localization.UiText.Get("Gemeinsame Zeitsteuerung · Integrierter Boot · Kein Netzwerk"),
            gamepadNavigationEnabled: () => !GameInputEnabled);
        if (initialRom is not null && IsSupportedPath(initialRom)) SetRom(0, initialRom);
        playerButtons[0].Selected = true;
        timer.Tick += (_, _) => RefreshSession();
        timer.Start();
        Deactivate += (_, _) =>
        {
            ClearInput();
            if (session?.LatestSnapshot.State == SessionState.Running) _ = PauseForFocusAsync();
        };
        FormClosing += OnClosing;
        Disposed += (_, _) =>
        {
            timer.Dispose();
            Interlocked.Exchange(ref audioOutput, null)?.Dispose();
            if (session is { } active) _ = active.ShutdownAsync();
        };
        RefreshSession();
    }

    private bool GameInputEnabled => session?.LatestSnapshot.State == SessionState.Running &&
        Enabled && ActiveForm == this && (displays[0].ContainsFocus || displays[1].ContainsFocus) && stopping is null;

    internal void SetRom(int player, string path)
    {
        if (player is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(player));
        if (session is not null || starting) throw new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("Die Link-Sitzung zuerst beenden."));
        if (!IsSupportedPath(path)) throw new InvalidDataException(global::AetherBoy.Runtime.Localization.UiText.Get("Local Link unterstützt .gb, .gbc und .gba."));
        selectedRoms[player] = Path.GetFullPath(path);
        titles[player].Text = global::AetherBoy.Runtime.Localization.UiText.Format("PLAYER {0} · {1}", player + 1, Path.GetFileNameWithoutExtension(path));
        actionMessage = null;
        RefreshSession();
    }

    private static bool IsSupportedPath(string path) =>
        Path.GetExtension(path).Equals(".gb", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(path).Equals(".gbc", StringComparison.OrdinalIgnoreCase) ||
        IsAdvancePath(path);

    private static bool IsAdvancePath(string path) => Path.GetExtension(path).Equals(".gba", StringComparison.OrdinalIgnoreCase);

    private bool HasCompatibleSelection => selectedRoms[0] is string first && selectedRoms[1] is string second &&
        IsAdvancePath(first) == IsAdvancePath(second);

    private void PickRom(int player)
    {
        // Selection only: the regular Vault can migrate adjacent saves as part of import.
        // Link storage validates both ROMs first and never imports external save files.
        using var picker = new AetherFileDialog
        {
            Title = global::AetherBoy.Runtime.Localization.UiText.Format("Player {0} · GB-/GBC-/GBA-ROM auswählen", player + 1),
            Filter = "Game Boy / Color / Advance (*.gb;*.gbc;*.gba)|*.gb;*.gbc;*.gba|Game Boy / Color (*.gb;*.gbc)|*.gb;*.gbc|Game Boy Advance (*.gba)|*.gba",
            CheckFileExists = true, Multiselect = false, RestoreDirectory = true
        };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try { SetRom(player, picker.FileName); }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException) { ShowError(global::AetherBoy.Runtime.Localization.UiText.Get("ROM-Auswahl"), ex); }
    }

    internal async Task StartAsync()
    {
        if (session is not null || starting || !HasCompatibleSelection) return;
        starting = true; actionMessage = global::AetherBoy.Runtime.Localization.UiText.Get("ROMs werden geprüft …"); RefreshSession();
        try
        {
            WindowsLocalLinkPlan plan = storage.CreatePlan(selectedRoms[0]!, selectedRoms[1]!);
            if (!prepareMainWindow()) throw new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("Das laufende Spiel konnte nicht sicher beendet werden."));
            // Stopping the main game releases its per-ROM profile; use the same
            // resulting global preferences for both linked displays and machines.
            foreach (var display in displays)
            {
                display.GpuEnabled = settings.GpuRendering;
                display.IntegerScaling = settings.IntegerScaling;
                display.Filter = (GameDisplayFilter)settings.DisplayFilterIndex;
            }
            selectedRoms[0] = plan.FirstRomPath; selectedRoms[1] = plan.SecondRomPath;
            savePolicy.Text = plan.SameRom
                ? global::AetherBoy.Runtime.Localization.UiText.Get("GLEICHE ROM · P1: bisheriger Spielstand. P2: eigener LinkPlayer2-Spielstand (anfangs leer). Keine Kopie, kein Überschreiben von P1.")
                : global::AetherBoy.Runtime.Localization.UiText.Get("ZWEI ROMs · Jeder Spieler nutzt seinen bisherigen AppData-Spielstand. Beide Dateien bleiben getrennt und exklusiv gesperrt.");
            var configuration = new EmulatorConfiguration(settings.Frameskip, settings.AudioEnable,
                settings.Channel1Enable, settings.Channel2Enable, settings.Channel3Enable, settings.Channel4Enable, 44100);
            // The first link milestone uses the integrated boot path on both machines.
            var created = new LocalLinkSession(
                new(plan.FirstRomPath, plan.FirstSavePath, null, configuration, settings.PaletteIndex),
                new(plan.SecondRomPath, plan.SecondSavePath, null, configuration, settings.PaletteIndex));
            session = created;
            for (int player = 0; player < 2; player++)
            {
                var geometry = created.GetVideoGeometry(player);
                pixels[player] = new int[geometry.PixelCount];
                displays[player].SetVideoGeometry(geometry);
                displays[player].ClearFrame();
            }
            created.AudioSamplesAvailable += OnAudio;
            faultShown = false; frameSequences[0] = frameSequences[1] = 0;
            await created.Ready;
            if (IsDisposed || closingRequested || !ReferenceEquals(session, created)) return;
            actionMessage = null;
            SetAudioPlayer(settings.AudioEnable ? 0 : -1);
            SelectKeyboardPlayer(0);
        }
        catch (Exception ex)
        {
            await StopAsync();
            if (!IsDisposed && !closingRequested) ShowError(global::AetherBoy.Runtime.Localization.UiText.Get("Link-Start fehlgeschlagen"), ex);
        }
        finally { starting = false; if (!IsDisposed) RefreshSession(); }
    }

    internal Task StopAsync()
    {
        if (stopping is not null) return stopping;
        if (session is null) return Task.CompletedTask;
        stopping = StopCoreAsync();
        return stopping;
    }

    private async Task StopCoreAsync()
    {
        var active = session!;
        // Ensure StopAsync installs its shared task before cleanup can complete synchronously.
        await Task.Yield();
        ClearInput(); Interlocked.Exchange(ref audioOutput, null)?.Dispose();
        active.AudioSamplesAvailable -= OnAudio;
        actionMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Beide Spiele werden beendet und Spielstände geschrieben …");
        try
        {
            await active.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await active.DisposeAsync();
            if (ReferenceEquals(session, active)) session = null;
            actionMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Beendet · Im Einzelspiel den In-Game-Spielstand laden, keinen älteren Save State fortsetzen.");
        }
        catch (TimeoutException)
        {
            actionMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Beenden dauert noch an. Die Dateisperren bleiben zum Schutz der Spielstände aktiv; bitte erneut versuchen.");
        }
        catch (Exception ex)
        {
            if (active.Completion.IsCompleted)
            {
                if (ReferenceEquals(session, active)) session = null;
                // Completion includes owner-thread cleanup; DisposeAsync repeats the same
                // faulted task, so there is no second disposal operation to await here.
            }
            actionMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Link mit Fehler beendet: ") + ex.GetType().Name + global::AetherBoy.Runtime.Localization.UiText.Get(". Spielstände prüfen.");
        }
        finally
        {
            stopping = null;
            if (!IsDisposed) RefreshSession();
        }
    }

    private async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (allowClose || session is null) return;
        e.Cancel = true;
        if (closingRequested) return;
        closingRequested = true;
        await StopAsync();
        if (session is null) { allowClose = true; Close(); }
        else closingRequested = false;
    }

    private async Task TogglePauseAsync()
    {
        var active = session; if (active is null) return;
        ClearInput();
        try { await active.SetPausedAsync(!active.LatestSnapshot.IsPaused); if (!IsDisposed) SelectKeyboardPlayer(keyboardPlayer); }
        catch (InvalidOperationException ex) { if (!IsDisposed) ShowError(global::AetherBoy.Runtime.Localization.UiText.Get("Link-Pause"), ex); }
    }
    private async Task PauseForFocusAsync()
    {
        var active = session; if (active is null) return;
        try { await active.SetPausedAsync(true); }
        catch (InvalidOperationException) { }
    }
    private async Task ToggleCableAsync()
    {
        var active = session; if (active is null) return;
        ClearInput();
        try { await active.SetConnectedAsync(!active.LatestSnapshot.Connected); }
        catch (InvalidOperationException ex) { if (!IsDisposed) ShowError(global::AetherBoy.Runtime.Localization.UiText.Get("Link-Verbindung"), ex); }
    }

    internal void SelectKeyboardPlayer(int player)
    {
        if (player is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(player));
        ClearInput(); keyboardPlayer = player;
        for (int index = 0; index < 2; index++) playerButtons[index].Selected = index == player;
        displays[player].Focus();
    }

    private void RefreshSession()
    {
        if (IsDisposed) return;
        var current = session;
        var snapshot = current?.LatestSnapshot;
        bool active = current is not null;
        startButton.Enabled = !active && !starting && HasCompatibleSelection;
        foreach (var button in pickButtons) button.Enabled = !active && !starting;
        pauseButton.Enabled = cableButton.Enabled = active && stopping is null && snapshot?.State is SessionState.Running or SessionState.Paused;
        pauseButton.Text = snapshot?.IsPaused == true ? global::AetherBoy.Runtime.Localization.UiText.Get("BEIDE FORTSETZEN") : global::AetherBoy.Runtime.Localization.UiText.Get("BEIDE PAUSIEREN");
        cableButton.Text = snapshot?.Connected == true ? global::AetherBoy.Runtime.Localization.UiText.Get("KABEL TRENNEN") : global::AetherBoy.Runtime.Localization.UiText.Get("KABEL VERBINDEN");
        cableButton.Selected = snapshot?.Connected == true;
        stopButton.Enabled = active && stopping is null;
        audioButton.Enabled = active && stopping is null && settings.AudioEnable;
        audioButton.Text = audioPlayer < 0 ? global::AetherBoy.Runtime.Localization.UiText.Get("AUDIO AUS") : global::AetherBoy.Runtime.Localization.UiText.Format("AUDIO · PLAYER {0}", audioPlayer + 1);
        status.Text = actionMessage ?? (snapshot is null ?
            (selectedRoms[0] is not null && selectedRoms[1] is not null && !HasCompatibleSelection
                ? global::AetherBoy.Runtime.Localization.UiText.Get("NICHT KOMPATIBEL · GB/GBC und GBA können nicht miteinander verkabelt werden.")
                : global::AetherBoy.Runtime.Localization.UiText.Get("BEREIT · Zwei GB-/GBC-ROMs oder zwei GBA-ROMs auswählen.")) :
            global::AetherBoy.Runtime.Localization.UiText.Format("{0} · {1} · Frame {2:N0} · Link-Zähler {3:N0} · Tastatur P{4}", global::AetherBoy.Runtime.Localization.UiLabels.Session(snapshot.State), (snapshot.Connected ? global::AetherBoy.Runtime.Localization.UiText.Get("KABEL VERBUNDEN") : global::AetherBoy.Runtime.Localization.UiText.Get("KABEL GETRENNT")), snapshot.FrameCount, snapshot.ClockEdges, keyboardPlayer + 1));
        var output = Volatile.Read(ref audioOutput);
        output?.SetSuspended(snapshot?.State != SessionState.Running);
        // Control updates and native controller calls can dispatch window messages.
        // A queued StopAsync continuation may retire the owner during this tick.
        if (IsDisposed || !ReferenceEquals(session, current)) return;
        if (current is not null)
        {
            for (int index = 0; index < 2; index++)
            {
                if (IsDisposed || !ReferenceEquals(session, current)) return;
                if (current.TryCopyLatestFrame(index, pixels[index], ref frameSequences[index])) displays[index].Present(pixels[index]);
            }
            if (snapshot?.State == SessionState.Faulted && !faultShown)
            {
                faultShown = true;
                actionMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Link-Fehler: ") + current.Fault?.GetType().Name + global::AetherBoy.Runtime.Localization.UiText.Get(". Sitzung beenden und neu starten.");
                ClearInput();
            }
        }
        if (!GameInputEnabled) { ClearInput(); return; }
        var pads = GamepadInput.GetLocalPairStates();
        if (IsDisposed || !ReferenceEquals(session, current)) return;
        if (!GameInputEnabled) { ClearInput(); return; }
        // Enumeration can move the remaining controller from P2 to P1 on unplug.
        // Re-arm both sides whenever the observed device set/backend changes.
        if (ChangedController(previousPads[0], pads.First) || ChangedController(previousPads[1], pads.Second))
            Array.Clear(padArmed);
        previousPads[0] = pads.First; previousPads[1] = pads.Second;
        for (int player = 0; player < 2; player++)
        {
            var pad = player == 0 ? pads.First : pads.Second;
            settings.UseControllerProfile(pad);
            if (!pad.IsConnected) padArmed[player] = false;
            else if (!padArmed[player]) padArmed[player] = settings.IsControllerNeutral(pad);
            GameBoyButtons mapped = padArmed[player] ? GamepadMapper.ToGameBoyButtons(pad, settings.GamepadBindings,
                settings.ControllerStickEnabled ? settings.ControllerStickThreshold : 1f) : GameBoyButtons.None;
            if (player == keyboardPlayer) mapped |= KeyboardButtons();
            PostButtons(player, mapped);
            GameBoyAdvanceButtons advance = GameBoyAdvanceButtons.None;
            if (current!.IsGameBoyAdvance)
            {
                if (padArmed[player])
                {
                    if (pad.IsAnyButtonDown(settings.GamepadL) || pad.LeftTrigger > GamepadMapper.DefaultStickThreshold)
                        advance |= GameBoyAdvanceButtons.L;
                    if (pad.IsAnyButtonDown(settings.GamepadR) || pad.RightTrigger > GamepadMapper.DefaultStickThreshold)
                        advance |= GameBoyAdvanceButtons.R;
                }
                if (player == keyboardPlayer)
                    foreach (Keys key in heldKeys) advance |= MapAdvanceKey(key);
            }
            PostAdvanceButtons(player, advance);
        }
    }

    private static bool ChangedController(HostGamepadState before, HostGamepadState after) =>
        before.DeviceId != after.DeviceId ||
        before.IsConnected != after.IsConnected || before.Source != after.Source ||
        before.DeviceName != after.DeviceName || before.VendorId != after.VendorId || before.ProductId != after.ProductId;

    private void ClearInput()
    {
        heldKeys.Clear();
        for (int player = 0; player < 2; player++)
        {
            padArmed[player] = false;
            PostButtons(player, GameBoyButtons.None);
            PostAdvanceButtons(player, GameBoyAdvanceButtons.None);
        }
    }
    private void PostButtons(int player, GameBoyButtons buttons)
    {
        if (sentButtons[player] == buttons) return;
        sentButtons[player] = buttons;
        if (session is { } active) _ = ObserveInputAsync(active, player, buttons);
    }
    private static async Task ObserveInputAsync(LocalLinkSession active, int player, GameBoyButtons buttons)
    {
        try { await active.SetButtonsAsync(player, buttons); }
        catch (InvalidOperationException) { }
    }
    private void PostAdvanceButtons(int player, GameBoyAdvanceButtons buttons)
    {
        if (sentAdvanceButtons[player] == buttons) return;
        sentAdvanceButtons[player] = buttons;
        if (session is { IsGameBoyAdvance: true } active) _ = ObserveAdvanceInputAsync(active, player, buttons);
    }
    private static async Task ObserveAdvanceInputAsync(LocalLinkSession active, int player, GameBoyAdvanceButtons buttons)
    {
        try { await active.SetGameBoyAdvanceButtonsAsync(player, buttons); }
        catch (InvalidOperationException) { }
    }
    private GameBoyAdvanceButtons MapAdvanceKey(Keys key)
    {
        GameBoyAdvanceButtons result = GameBoyAdvanceButtons.None;
        if (key == settings.KeyL) result |= GameBoyAdvanceButtons.L;
        if (key == settings.KeyR) result |= GameBoyAdvanceButtons.R;
        return result;
    }
    private GameBoyButtons KeyboardButtons()
    {
        GameBoyButtons result = GameBoyButtons.None;
        foreach (Keys key in heldKeys) result |= MapKey(key);
        return result;
    }
    private GameBoyButtons MapKey(Keys key)
    {
        GameBoyButtons result = GameBoyButtons.None;
        if (key == settings.KeyA) result |= GameBoyButtons.A;
        if (key == settings.KeyB) result |= GameBoyButtons.B;
        if (key == settings.KeyStart) result |= GameBoyButtons.Start;
        if (key == settings.KeySelect) result |= GameBoyButtons.Select;
        if (key == settings.KeyUp) result |= GameBoyButtons.Up;
        if (key == settings.KeyDown) result |= GameBoyButtons.Down;
        if (key == settings.KeyLeft) result |= GameBoyButtons.Left;
        if (key == settings.KeyRight) result |= GameBoyButtons.Right;
        return result;
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (session is not null && keyData is Keys.F1 or Keys.F2) { SelectKeyboardPlayer(keyData == Keys.F1 ? 0 : 1); return true; }
        if (GameInputEnabled)
        {
            if (keyData == Keys.Escape) { _ = PauseForFocusAsync(); ClearInput(); return true; }
            Keys key = keyData & Keys.KeyCode;
            if (MapKey(key) != GameBoyButtons.None ||
                (session!.IsGameBoyAdvance && MapAdvanceKey(key) != GameBoyAdvanceButtons.None))
                { heldKeys.Add(key); return true; }
            if (key is Keys.F5 or Keys.F6 or Keys.F7 or Keys.F8 or Keys.F9 or Keys.F10 or Keys.Tab) return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void OnKeyUp(KeyEventArgs e)
    {
        heldKeys.Remove(e.KeyCode);
        base.OnKeyUp(e);
    }

    private void CycleAudio() => SetAudioPlayer(audioPlayer == 1 ? -1 : audioPlayer + 1);
    private void SetAudioPlayer(int player)
    {
        Interlocked.Exchange(ref audioOutput, null)?.Dispose();
        Volatile.Write(ref audioPlayer, player);
        if (player >= 0 && settings.AudioEnable && session is not null)
            Volatile.Write(ref audioOutput, new NAudioSoundOut(44100, settings.AudioVolume / 100f, settings.AudioLatencyMs));
    }
    private void OnAudio(object? sender, LocalLinkAudioEventArgs e)
    {
        // Capture output before validating the side: a callback in flight must never
        // pick up a replacement device belonging to the newly selected player.
        var output = Volatile.Read(ref audioOutput);
        if (output is null || !ReferenceEquals(sender, session) || e.Player != Volatile.Read(ref audioPlayer)) return;
        try { output.Submit(e.Audio.GetInterleavedSamplesCopy(), e.Audio.SampleRate, e.Audio.Channels,
            e.Audio.PlaybackGeneration, e.Audio.PlaybackSession); }
        catch (ObjectDisposedException) { }
    }

    private void ShowError(string title, Exception exception)
    {
        actionMessage = title + " · " + exception.GetType().Name;
        AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message), title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
    private static AetherButton Button(Control parent, string name, string text, Rectangle bounds, Action action)
    {
        var button = new AetherButton { Name = name, Text = text, Bounds = bounds, Kind = AetherButtonKind.Secondary };
        button.Click += (_, _) => action(); parent.Controls.Add(button); return button;
    }
}
