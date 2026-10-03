using AetherBoy.Runtime;
using AetherBoy.Runtime.Netplay;
using nanoboy.Core;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private WebRtcBrowserTransport? onlineLinkTransport;
    private string? onlineLinkDirectory;
    private bool showOnlineLinkPage;
    private bool? pendingOnlineRole;
    private GbaOnlineCompatibility? onlineGbaPreview;
    private string onlineProfileMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Open a GB/GBC game or an approved Pokémon Gen3 test game.");
    private OnlineLinkPhase? lastGbaOnlineAudioPhase;
    private bool IsOnlineLink => session?.OnlineLink is not null;
    internal Action<string> OnlineLinkBrowserLauncher { get; set; } = url =>
    {
        if (!SDL.OpenURL(url)) throw new IOException(global::AetherBoy.Runtime.Localization.UiText.Get("Could not open the browser: ") + SDL.GetError());
    };
    internal Func<string, GbaOnlineCompatibility> OnlineGbaProfileInspector = GbaOnlineProfileCatalog.InspectRom;
    internal Func<string, string, string, bool, IOnlineLinkTransport, EmulatorConfiguration, int, bool, EmulationSession>
        OnlineLinkSessionStarter = (rom, save, directory, host, transport, configuration, palette, allowGba) =>
            EmulationSession.CreateOnlineLink(rom, save, directory, host, transport, configuration, palette, allowGba);

    private void OpenOnlineLinkPage()
    {
        OpenControlPage(ControlCenterPage.Tools);
        showOnlineLinkPage = true; focusedControl = -1;
        pendingOnlineRole = null;
        showOnlineSaveRecovery = false;
        LoadRoomSettings();
        InspectOnlineProfile();
    }

    private void InspectOnlineProfile()
    {
        onlineGbaPreview = null;
        onlineProfileMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Open a GB/GBC game or an approved Pokémon Gen3 test game.");
        try
        {
            if (romPath is null || session?.LatestSnapshot.Rom is not { } rom) return;
            if (!rom.IsGameBoyAdvance) { onlineProfileMessage = global::AetherBoy.Runtime.Localization.UiText.Get("GB/GBC online play is experimental. Pokémon trading is not yet verified."); return; }
            onlineGbaPreview = OnlineGbaProfileInspector(romPath);
            onlineProfileMessage = onlineGbaPreview.IsDevelopmentCandidate
                ? onlineGbaPreview.DisplayName + global::AetherBoy.Runtime.Localization.UiText.Get(" (online test profile)")
                : global::AetherBoy.Runtime.Localization.UiText.Get("This GBA game version has no online test profile. ROM hacks and unknown versions are blocked.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { onlineProfileMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Could not check whether this game supports Online Link: ") + ex.Message; }
    }

    internal bool StartOnlineLink(bool isHost, bool acceptGbaDevelopment = false)
    {
        if (IsOnlineLink || IsLoading || stateOperation is not null || romPath is null || storage is null ||
            session?.LatestSnapshot.Rom is not { } rom)
        { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Open your own supported game and wait for the current action to finish."); return false; }
        string path = romPath, save = storage.SavePath;
        GbaOnlineCompatibility? gba = null;
        try
        {
            if (rom.IsGameBoyAdvance)
            {
                gba = OnlineGbaProfileInspector(path);
                if (!gba.IsDevelopmentCandidate)
                { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("GBA Online is unavailable for this ROM: ") + global::AetherBoy.Runtime.Localization.UiText.Get(gba.Reason); return false; }
                if (!acceptGbaDevelopment)
                { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Confirm the Gen3 test profile and save copy on the Online Link page first."); return false; }
            }
            else if (!(Path.GetExtension(path).Equals(".gb", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(path).Equals(".gbc", StringComparison.OrdinalIgnoreCase)))
            { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("This game does not support Online Link yet."); return false; }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Could not check whether this game supports Online Link: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message); return false; }
        var configuration = options.CreateEmulatorConfiguration(EnsureAudioOutput());
        int palette = options.PaletteIndex;
        var activeProfile = LinuxGameProfile.Capture(options);
        bool wasUsingGameProfile = usingGameProfile;
        try
        {
            CloseSession();
            activeProfile.ApplyTo(options);
            usingGameProfile = wasUsingGameProfile;
            // Linux normal sessions use the central cartridge lock, in addition to
            // the Runtime's save-path lock. Keep it while the online copy is active.
            storage = LinuxRomStorage.Open(dataPaths, path);
            IOnlineLinkTransport transport = startNativeRoom
                ? new OnlineRoomTransport(roomSettings, isHost, roomCodeInput, gba is null ? "gb-serial-v1" : GbaOnlineProfileCatalog.PokemonGen3Profile,
                    diagnostics.Enabled ? Path.Combine(dataPaths.State, "online-diagnostics") : null)
                : new WebRtcBrowserTransport();
            try
            {
                string directory = Path.Combine(dataPaths.State, "online-link",
                    $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
                session = OnlineLinkSessionStarter(path, save, directory, isHost, transport, configuration, palette, gba is not null);
                onlineLinkTransport = transport as WebRtcBrowserTransport;
                onlineRoomTransport = transport as OnlineRoomTransport; onlineLinkDirectory = directory;
                onlineRecoveryTargetSave = save; onlineRecoveryRomPath = path;
                romPath = path; loadError = null;
                undoState = null; undoIdentity = null; pendingResumeIdentity = null;
                pendingBatteryRestore = null; showGallery = showBackups = false;
                displayedFrameSequence = 0; postedButtons = GameBoyButtons.None;
                postedAdvanceButtons = GameBoyAdvanceButtons.None; pressedKeys.Clear(); mouseTurbo = false;
                resumeAfterFocus = false; resumeAfterControlCenter = true;
                pendingOnlineRole = null;
                lastGbaOnlineAudioPhase = null;
                if (audioOutput is not null) session.AudioSamplesAvailable += OnAudioSamplesAvailable;
                RequestDiskRefresh();
                statusMessage = startNativeRoom ? global::AetherBoy.Runtime.Localization.UiText.Get("Save copy ready. Connecting to the room server…")
                    : global::AetherBoy.Runtime.Localization.UiText.Get("Save copy ready. Finish connecting in the browser. A trade is not yet verified.");
                SDL.SetWindowTitle(window, "AetherBoy · ONLINE LINK" + (gba is null ? "" : global::AetherBoy.Runtime.Localization.UiText.Get(" · Gen3-Entwicklungstest")) + " · " + Path.GetFileNameWithoutExtension(path));
                if (!startNativeRoom) OpenOnlineLinkBrowser();
                return true;
            }
            catch { transport.Dispose(); throw; }
        }
        catch (Exception ex)
        {
            if (session is null) { storage?.Dispose(); storage = null; }
            diagnostics.Failure("online_link_start", ex);
            statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Online Link could not start: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message);
            return false;
        }
    }

    private void OpenOnlineLinkBrowser()
    {
        if (!IsOnlineLink || onlineLinkTransport is null)
        { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Create or join an online session first."); return; }
        try { OnlineLinkBrowserLauncher(onlineLinkTransport.ConnectionPageUrl); }
        catch (Exception ex) { statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Could not open the connection page: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message); }
    }

    private void StopOnlineLink()
    {
        if (!IsOnlineLink) return;
        if (session?.LatestSnapshot.Rom?.IsGameBoyAdvance == true)
            statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Ending the GBA session. Waiting for the other player and saving the session copy…");
        CloseSession(); romPath = null;
        statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Online Link ended. Your original save is unchanged. Check the session copy.");
    }

    private void FinishStoppedOnlineLink()
    {
        // A GBA peer can end both owners without a local Disconnect action. Retire
        // the completed owner before dispatching input or refreshing the frame.
        if (!IsOnlineLink || session is not { State: SessionState.Stopped, Completion.IsCompleted: true }) return;
        CloseSession();
        romPath = null;
        resumeAfterControlCenter = resumeAfterFocus = false;
        pressedKeys.Clear();
        postedButtons = GameBoyButtons.None;
        postedAdvanceButtons = GameBoyAdvanceButtons.None;
        SDL.SetWindowTitle(window, "AetherBoy · " + desktop.DisplayName);
        statusMessage = global::AetherBoy.Runtime.Localization.UiText.Get("Online Link ended. Check the session copy before keeping any progress.");
    }

    private void UpdateOnlineAudioWait(EmulationSession current)
    {
        if (current.LatestSnapshot.Rom?.IsGameBoyAdvance != true || current.OnlineLink is not { } link) return;
        if (lastGbaOnlineAudioPhase == link.Phase) return;
        lastGbaOnlineAudioPhase = link.Phase;
        if (link.Phase != OnlineLinkPhase.Playing) audioOutput?.Clear();
    }

    private void DrawOnlineLinkPage()
    {
        if (showOnlineSaveRecovery) { DrawOnlineSaveRecoveryPage(); return; }
        if (!showLegacyOnlineLink && (onlineLinkTransport is null)) { DrawOnlineRoomPage(); return; }
        ActionButton(300, 198, 200, 40, global::AetherBoy.Runtime.Localization.UiText.Get("BACK TO TOOLS"), () => { showOnlineLinkPage = false; pendingOnlineRole = null; OpenControlPage(ControlCenterPage.Tools); focusedControl = -1; });
        Ink(300, 258, "ONLINE LINK · GB/GBC + GBA GEN3 (TEST)", 20, Colors.Cyan, true);
        Ink(300, 292, textRenderer.Fit(session?.OnlineLink?.DisplayName ?? onlineProfileMessage, 825, 14), 14, Colors.Cyan);
        Ink(300, 320, global::AetherBoy.Runtime.Localization.UiText.Get("Both players need their own game. ROMs and full save files are not shared."), 14, Colors.Muted);
        Ink(300, 348, global::AetherBoy.Runtime.Localization.UiText.Get("Pokémon trading is unverified. GBA online tests are limited to approved Gen3 games; ROM hacks are not supported."), 14, Colors.Muted);
        Ink(300, 376, global::AetherBoy.Runtime.Localization.UiText.Get("GBA uses HLE BIOS. Save states, rewind, turbo, reset and cheats are disabled."), 14, Colors.Muted);
        Ink(300, 404, global::AetherBoy.Runtime.Localization.UiText.Get("Exchange the invitation and answer in the browser. Keep both tabs open."), 14, Colors.Muted);
        bool canStart = session?.LatestSnapshot.Rom is { } rom && (!rom.IsGameBoyAdvance || onlineGbaPreview?.IsDevelopmentCandidate == true)
            && !IsOnlineLink && !IsLoading && stateOperation is null;
        if (pendingOnlineRole is bool role)
        {
            Ink(300, 435, global::AetherBoy.Runtime.Localization.UiText.Get("Start as ") + (role ? "host" : "guest") + global::AetherBoy.Runtime.Localization.UiText.Get(" with a copy of your save? Online play is experimental."), 13, Colors.Cyan);
            ActionButton(300, 465, 240, 44, global::AetherBoy.Runtime.Localization.UiText.Get("START WITH SAVE COPY"), () => StartOnlineLink(role, acceptGbaDevelopment: true), enabled: canStart);
            ActionButton(560, 465, 180, 44, global::AetherBoy.Runtime.Localization.UiText.Get("CANCEL"), () => pendingOnlineRole = null);
        }
        else
        {
            ActionButton(300, 465, 210, 44, global::AetherBoy.Runtime.Localization.UiText.Get("HOST A SESSION"), () => pendingOnlineRole = true, enabled: canStart);
            ActionButton(530, 465, 210, 44, global::AetherBoy.Runtime.Localization.UiText.Get("JOIN A SESSION"), () => pendingOnlineRole = false, enabled: canStart);
        }
        ActionButton(760, 465, 210, 44, global::AetherBoy.Runtime.Localization.UiText.Get("CONNECTION PAGE"), OpenOnlineLinkBrowser, enabled: IsOnlineLink);
        ActionButton(300, 525, 280, 44, global::AetherBoy.Runtime.Localization.UiText.Get("OPEN ONLINE SAVE FOLDER"), () => OpenFolder(onlineLinkDirectory ?? Path.Combine(dataPaths.State, "online-link")));
        ActionButton(600, 525, 180, 44, global::AetherBoy.Runtime.Localization.UiText.Get("DISCONNECT"), StopOnlineLink, enabled: IsOnlineLink);
        ActionButton(800, 525, 200, 44, global::AetherBoy.Runtime.Localization.UiText.Get("CHECK SAVE COPIES"), OpenOnlineSaveRecovery);
        string state = session?.OnlineLink is { } link
            ? link.Failure is null
                ? global::AetherBoy.Runtime.Localization.UiText.Format("{0} Cable exchanges: {1}.", OnlineLinkPhaseText(link.Phase), link.TransfersCompleted)
                : global::AetherBoy.Runtime.Localization.UiText.Get("Connection stopped. Check the browser page and your save copies.")
            : global::AetherBoy.Runtime.Localization.UiText.Get("Open a supported game first. One player hosts; the other joins.");
        Ink(300, 592, textRenderer.Fit(state, 825, 14), 14, Colors.Cyan);
        Ink(300, 627, global::AetherBoy.Runtime.Localization.UiText.Get("Close settings to play. After disconnecting, check both save copies before keeping progress."), 12, Colors.Muted);
    }

    private static string OnlineLinkPhaseText(OnlineLinkPhase phase) => phase switch
    {
        OnlineLinkPhase.WaitingForBrowser => global::AetherBoy.Runtime.Localization.UiText.Get("Waiting for the browser."),
        OnlineLinkPhase.WaitingForPeer => global::AetherBoy.Runtime.Localization.UiText.Get("Waiting for the other player."),
        OnlineLinkPhase.Playing => global::AetherBoy.Runtime.Localization.UiText.Get("Connected."),
        OnlineLinkPhase.WaitingForTransfer => global::AetherBoy.Runtime.Localization.UiText.Get("Waiting for cable data."),
        OnlineLinkPhase.Closed => global::AetherBoy.Runtime.Localization.UiText.Get("Session ended."),
        OnlineLinkPhase.Faulted => global::AetherBoy.Runtime.Localization.UiText.Get("Connection stopped."),
        _ => global::AetherBoy.Runtime.Localization.UiText.Get("Online Link is starting.")
    };
}
