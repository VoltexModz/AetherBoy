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
    private string onlineProfileMessage = "Open a GB/GBC game or an approved Pokémon Gen3 test game.";
    private OnlineLinkPhase? lastGbaOnlineAudioPhase;
    private bool IsOnlineLink => session?.OnlineLink is not null;
    internal Action<string> OnlineLinkBrowserLauncher { get; set; } = url =>
    {
        if (!SDL.OpenURL(url)) throw new IOException("Could not open the browser: " + SDL.GetError());
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
        onlineProfileMessage = "Open a GB/GBC game or an approved Pokémon Gen3 test game.";
        try
        {
            if (romPath is null || session?.LatestSnapshot.Rom is not { } rom) return;
            if (!rom.IsGameBoyAdvance) { onlineProfileMessage = "GB/GBC online play is experimental. Pokémon trading is not yet verified."; return; }
            onlineGbaPreview = OnlineGbaProfileInspector(romPath);
            onlineProfileMessage = onlineGbaPreview.IsDevelopmentCandidate
                ? onlineGbaPreview.DisplayName + " (online test profile)"
                : "This GBA game version has no online test profile. ROM hacks and unknown versions are blocked.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { onlineProfileMessage = "Could not check whether this game supports Online Link: " + ex.Message; }
    }

    internal bool StartOnlineLink(bool isHost, bool acceptGbaDevelopment = false)
    {
        if (IsOnlineLink || IsLoading || stateOperation is not null || romPath is null || storage is null ||
            session?.LatestSnapshot.Rom is not { } rom)
        { statusMessage = "Open your own supported game and wait for the current action to finish."; return false; }
        string path = romPath, save = storage.SavePath;
        GbaOnlineCompatibility? gba = null;
        try
        {
            if (rom.IsGameBoyAdvance)
            {
                gba = OnlineGbaProfileInspector(path);
                if (!gba.IsDevelopmentCandidate)
                { statusMessage = "GBA Online is unavailable for this ROM: " + gba.Reason; return false; }
                if (!acceptGbaDevelopment)
                { statusMessage = "Confirm the Gen3 test profile and save copy on the Online Link page first."; return false; }
            }
            else if (!(Path.GetExtension(path).Equals(".gb", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(path).Equals(".gbc", StringComparison.OrdinalIgnoreCase)))
            { statusMessage = "This game does not support Online Link yet."; return false; }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { statusMessage = "Could not check whether this game supports Online Link: " + ex.Message; return false; }
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
                statusMessage = startNativeRoom ? "Save copy ready. Connecting to the room server…"
                    : "Save copy ready. Finish connecting in the browser. A trade is not yet verified.";
                SDL.SetWindowTitle(window, "AetherBoy · ONLINE LINK" + (gba is null ? "" : " · GEN3 TEST") + " · " + Path.GetFileNameWithoutExtension(path));
                if (!startNativeRoom) OpenOnlineLinkBrowser();
                return true;
            }
            catch { transport.Dispose(); throw; }
        }
        catch (Exception ex)
        {
            if (session is null) { storage?.Dispose(); storage = null; }
            diagnostics.Failure("online_link_start", ex);
            statusMessage = "Online Link could not start: " + ex.Message;
            return false;
        }
    }

    private void OpenOnlineLinkBrowser()
    {
        if (!IsOnlineLink || onlineLinkTransport is null)
        { statusMessage = "Create or join an online session first."; return; }
        try { OnlineLinkBrowserLauncher(onlineLinkTransport.ConnectionPageUrl); }
        catch (Exception ex) { statusMessage = "Could not open the connection page: " + ex.Message; }
    }

    private void StopOnlineLink()
    {
        if (!IsOnlineLink) return;
        if (session?.LatestSnapshot.Rom?.IsGameBoyAdvance == true)
            statusMessage = "Ending the GBA session. Waiting for the other player and saving the session copy…";
        CloseSession(); romPath = null;
        statusMessage = "Online Link ended. Your original save is unchanged. Check the session copy.";
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
        statusMessage = "Online Link ended. Check the session copy before keeping any progress.";
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
        ActionButton(300, 198, 200, 40, "BACK TO TOOLS", () => { showOnlineLinkPage = false; pendingOnlineRole = null; OpenControlPage(ControlCenterPage.Tools); focusedControl = -1; });
        Ink(300, 258, "ONLINE LINK · GB/GBC + GBA GEN3 (TEST)", 20, Colors.Cyan, true);
        Ink(300, 292, textRenderer.Fit(session?.OnlineLink?.DisplayName ?? onlineProfileMessage, 825, 14), 14, Colors.Cyan);
        Ink(300, 320, "Both players need their own game. ROMs and full save files are not shared.", 14, Colors.Muted);
        Ink(300, 348, "Pokémon trading is unverified. GBA online tests are limited to approved Gen3 games; ROM hacks are not supported.", 14, Colors.Muted);
        Ink(300, 376, "GBA uses HLE BIOS. Save states, rewind, turbo, reset and cheats are disabled.", 14, Colors.Muted);
        Ink(300, 404, "Exchange the invitation and answer in the browser. Keep both tabs open.", 14, Colors.Muted);
        bool canStart = session?.LatestSnapshot.Rom is { } rom && (!rom.IsGameBoyAdvance || onlineGbaPreview?.IsDevelopmentCandidate == true)
            && !IsOnlineLink && !IsLoading && stateOperation is null;
        if (pendingOnlineRole is bool role)
        {
            Ink(300, 435, "Start as " + (role ? "host" : "guest") + " with a copy of your save? Online play is experimental.", 13, Colors.Cyan);
            ActionButton(300, 465, 240, 44, "START WITH SAVE COPY", () => StartOnlineLink(role, acceptGbaDevelopment: true), enabled: canStart);
            ActionButton(560, 465, 180, 44, "CANCEL", () => pendingOnlineRole = null);
        }
        else
        {
            ActionButton(300, 465, 210, 44, "HOST A SESSION", () => pendingOnlineRole = true, enabled: canStart);
            ActionButton(530, 465, 210, 44, "JOIN A SESSION", () => pendingOnlineRole = false, enabled: canStart);
        }
        ActionButton(760, 465, 210, 44, "CONNECTION PAGE", OpenOnlineLinkBrowser, enabled: IsOnlineLink);
        ActionButton(300, 525, 280, 44, "OPEN ONLINE SAVE FOLDER", () => OpenFolder(onlineLinkDirectory ?? Path.Combine(dataPaths.State, "online-link")));
        ActionButton(600, 525, 180, 44, "DISCONNECT", StopOnlineLink, enabled: IsOnlineLink);
        ActionButton(800, 525, 200, 44, "CHECK SAVE COPIES", OpenOnlineSaveRecovery);
        string state = session?.OnlineLink is { } link
            ? link.Failure is null
                ? $"{OnlineLinkPhaseText(link.Phase)} Cable exchanges: {link.TransfersCompleted}."
                : "Connection stopped. Check the browser page and your save copies."
            : "Open a supported game first. One player hosts; the other joins.";
        Ink(300, 592, textRenderer.Fit(state, 825, 14), 14, Colors.Cyan);
        Ink(300, 627, "Close settings to play. After disconnecting, check both save copies before keeping progress.", 12, Colors.Muted);
    }

    private static string OnlineLinkPhaseText(OnlineLinkPhase phase) => phase switch
    {
        OnlineLinkPhase.WaitingForBrowser => "Waiting for the browser.",
        OnlineLinkPhase.WaitingForPeer => "Waiting for the other player.",
        OnlineLinkPhase.Playing => "Connected.",
        OnlineLinkPhase.WaitingForTransfer => "Waiting for cable data.",
        OnlineLinkPhase.Closed => "Session ended.",
        OnlineLinkPhase.Faulted => "Connection stopped.",
        _ => "Online Link is starting."
    };
}
