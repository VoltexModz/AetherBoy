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
    private string onlineProfileMessage = "Open a GB/GBC game or an eligible Pokemon Gen3 cartridge.";
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
        InspectOnlineProfile();
    }

    private void InspectOnlineProfile()
    {
        onlineGbaPreview = null;
        onlineProfileMessage = "Open a GB/GBC game or an eligible Pokemon Gen3 cartridge.";
        try
        {
            if (romPath is null || session?.LatestSnapshot.Rom is not { } rom) return;
            if (!rom.IsGameBoyAdvance) { onlineProfileMessage = "GB/GBC serial cable · DEVELOPMENT · no verified Pokemon trade yet"; return; }
            onlineGbaPreview = OnlineGbaProfileInspector(romPath);
            onlineProfileMessage = onlineGbaPreview.DisplayName + " · " + onlineGbaPreview.Reason;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { onlineProfileMessage = "Could not inspect the online profile: " + ex.Message; }
    }

    internal bool StartOnlineLink(bool isHost, bool acceptGbaDevelopment = false)
    {
        if (IsOnlineLink || IsLoading || stateOperation is not null || romPath is null || storage is null ||
            session?.LatestSnapshot.Rom is not { } rom)
        { statusMessage = "Open your own GB/GBC or eligible GBA cartridge first and wait for pending operations."; return false; }
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
                { statusMessage = "Confirm the DEVELOPMENT Gen3 profile and protected save copy on the Online Link page first."; return false; }
            }
            else if (!(Path.GetExtension(path).Equals(".gb", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(path).Equals(".gbc", StringComparison.OrdinalIgnoreCase)))
            { statusMessage = "No online cable profile is available for this cartridge."; return false; }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { statusMessage = "Could not inspect the online profile: " + ex.Message; return false; }
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
            var transport = new WebRtcBrowserTransport();
            try
            {
                string directory = Path.Combine(dataPaths.State, "online-link",
                    $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
                session = OnlineLinkSessionStarter(path, save, directory, isHost, transport, configuration, palette, gba is not null);
                onlineLinkTransport = transport; onlineLinkDirectory = directory;
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
                statusMessage = "DEVELOPMENT online copy prepared. Browser handshake next; a connection does not confirm a trade.";
                SDL.SetWindowTitle(window, "AetherBoy · ONLINE LINK" + (gba is null ? "" : " · GEN3 DEV") + " · " + Path.GetFileNameWithoutExtension(path));
                OpenOnlineLinkBrowser();
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
            statusMessage = "Closing GBA Online safely: waiting for the peer and flushing the private save copy.";
        CloseSession(); romPath = null;
        statusMessage = "Online Link closed. Session saves remain in the online folder; originals were not replaced.";
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
        statusMessage = "Online Link ended. Check the session save copies; originals were not replaced.";
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
        ActionButton(300, 198, 200, 40, "BACK TO TOOLS", () => { showOnlineLinkPage = false; pendingOnlineRole = null; OpenControlPage(ControlCenterPage.Tools); focusedControl = -1; });
        Ink(300, 258, "ONLINE LINK · GB/GBC + GBA GEN3 DEV", 20, Colors.Cyan, true);
        Ink(300, 292, textRenderer.Fit(session?.OnlineLink?.DisplayName ?? onlineProfileMessage, 825, 14), 14, Colors.Cyan);
        Ink(300, 320, "Own game + private save copy. No ROM or full save file is sent to the peer.", 14, Colors.Muted);
        Ink(300, 348, "No verified Pokemon trade. No general GBA / ROM-hack support. Originals stay unchanged.", 14, Colors.Muted);
        Ink(300, 376, "GBA uses HLE BIOS, not your full-BIOS selection. States / rewind / turbo / reset / cheats locked.", 14, Colors.Muted);
        Ink(300, 404, "Exchange browser invitation / answer; keep tab open. Internet may need STUN/TURN.", 14, Colors.Muted);
        bool canStart = session?.LatestSnapshot.Rom is { } rom && (!rom.IsGameBoyAdvance || onlineGbaPreview?.IsDevelopmentCandidate == true)
            && !IsOnlineLink && !IsLoading && stateOperation is null;
        if (pendingOnlineRole is bool role)
        {
            Ink(300, 435, "CONFIRM " + (role ? "HOST" : "GUEST") + ": restart with a DEVELOPMENT save copy? Outcome may remain uncertain.", 13, Colors.Cyan);
            ActionButton(300, 465, 240, 44, "START OWN SAVE COPY", () => StartOnlineLink(role, acceptGbaDevelopment: true), enabled: canStart);
            ActionButton(560, 465, 180, 44, "CANCEL", () => pendingOnlineRole = null);
        }
        else
        {
            ActionButton(300, 465, 210, 44, "CHOOSE HOST", () => pendingOnlineRole = true, enabled: canStart);
            ActionButton(530, 465, 210, 44, "CHOOSE GUEST", () => pendingOnlineRole = false, enabled: canStart);
        }
        ActionButton(760, 465, 210, 44, "CONNECTION PAGE", OpenOnlineLinkBrowser, enabled: IsOnlineLink);
        ActionButton(300, 525, 280, 44, "OPEN ONLINE SAVE FOLDER", () => OpenFolder(onlineLinkDirectory ?? Path.Combine(dataPaths.State, "online-link")));
        ActionButton(600, 525, 180, 44, "DISCONNECT", StopOnlineLink, enabled: IsOnlineLink);
        ActionButton(800, 525, 200, 44, "CHECK SAVE COPIES", OpenOnlineSaveRecovery);
        string state = session?.OnlineLink is { } link
            ? $"{link.Phase} · {link.TransfersCompleted} transfers · {link.Failure ?? "save/trade success is NOT confirmed"}"
            : "Choose one host and one guest. Unknown ROM checksums are refused before the game is stopped.";
        Ink(300, 592, textRenderer.Fit(state, 825, 14), 14, Colors.Cyan);
        Ink(300, 627, "Close settings to resume. After a disconnect, check both saved games before explicitly keeping progress.", 12, Colors.Muted);
    }
}
