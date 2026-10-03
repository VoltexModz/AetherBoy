using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Netplay;
using nanoboy.Controls;
using nanoboy.Core;
using nanoboy.Input;
using nanoboy.Platform.Audio;
using nanoboy.Storage;

namespace nanoboy;

public partial class frmNano
{
    private WebRtcBrowserTransport? onlineLinkTransport;
    private string? onlineLinkDirectory;
    private string? lastOnlineStatus;
    private string? lastOnlineDiagnostic;
    private frmOnlineConnectionTest? onlineProbeDialog;
    internal Func<OnlineRoomSettings, bool, string, string?, IOnlineProbeSession> OnlineProbeSessionFactory =
        (configuration, host, code, reports) => new OnlineProbeSession(configuration, host, code, reports);
    private bool IsConnectionTestActive => onlineProbeDialog?.IsTestActive == true;
    private bool IsOnlineLink => session?.OnlineLink is not null;
    internal Action<string> OnlineLinkBrowserLauncher = url =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    internal Func<string, GbaOnlineCompatibility> OnlineGbaProfileInspector = GbaOnlineProfileCatalog.InspectRom;
    // Test seam only: production always revalidates the exact ROM in the Runtime factory.
    internal Func<string, string, string, bool, IOnlineLinkTransport, EmulatorConfiguration, int, bool, EmulationSession>
        OnlineLinkSessionStarter = (rom, save, directory, host, transport, configuration, palette, allowGba) =>
            EmulationSession.CreateOnlineLink(rom, save, directory, host, transport, configuration, palette, allowGba);

    private void InitializeOnlineLinkTools()
    {
        var online = new nanoboy.Controls.AetherCommand("Online Link · GB/GBC + GBA Gen3 (Test)") { Name = "menuOnlineLink" };
        void Add(string name, string text, Action action)
        {
            var item = new nanoboy.Controls.AetherCommand(text) { Name = name };
            item.Click += (_, _) => action(); online.DropDownItems.Add(item);
        }
        Add("menuOnlineHost", global::AetherBoy.Runtime.Localization.UiText.Get("Sitzung erstellen · Strg+F10"), () => ShowOnlineRoomDialog(true));
        Add("menuOnlineJoin", global::AetherBoy.Runtime.Localization.UiText.Get("Sitzung beitreten · Strg+Umschalt+F10"), () => ShowOnlineRoomDialog(false));
        Add("menuOnlineProbe", global::AetherBoy.Runtime.Localization.UiText.Get("Verbindung testen · ohne ROM"), ShowOnlineConnectionTest);
        Add("menuOnlineManualHost", global::AetherBoy.Runtime.Localization.UiText.Get("Manuell im Browser · Host"), () => StartOnlineLink(true));
        Add("menuOnlineManualGuest", global::AetherBoy.Runtime.Localization.UiText.Get("Manuell im Browser · Gast"), () => StartOnlineLink(false));
        Add("menuOnlineBrowser", global::AetherBoy.Runtime.Localization.UiText.Get("Verbindungsseite öffnen"), OpenOnlineLinkBrowser);
        Add("menuOnlineProfile", global::AetherBoy.Runtime.Localization.UiText.Get("Aktuelles Profil und Grenzen"), ShowOnlineLinkProfile);
        Add("menuOnlineDiagnostic", global::AetherBoy.Runtime.Localization.UiText.Get("Letzte Verbindungsdiagnose"), ShowOnlineLinkDiagnostic);
        Add("menuOnlineNativeReports", global::AetherBoy.Runtime.Localization.UiText.Get("Native Verbindungsberichte öffnen"), () => WindowsDataPaths.OpenFolder(this,
            Path.Combine(WindowsDataPaths.Default.Development, "OnlineDiagnostics")));
        Add("menuOnlineSaves", global::AetherBoy.Runtime.Localization.UiText.Get("Online-Spielstände öffnen"), () => WindowsDataPaths.OpenFolder(this,
            onlineLinkDirectory ?? Path.Combine(WindowsDataPaths.Default.Development, "OnlineLink")));
        Add("menuOnlineRecovery", global::AetherBoy.Runtime.Localization.UiText.Get("Sitzungskopien prüfen / übernehmen"), ShowOnlineSaveRecovery);
        Add("menuOnlineStop", global::AetherBoy.Runtime.Localization.UiText.Get("Online-Verbindung beenden"), () =>
        {
            if (IsOnlineLink && StopSession()) SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Online Link beendet. Deine Spielstandkopie bleibt erhalten."), false);
        });
        menuItem21.DropDownItems.Add(online);
    }

    internal bool StartOnlineLink(bool isHost, bool confirm = true)
    {
        if (IsConnectionTestActive)
        { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Zuerst den laufenden Verbindungstest beenden"), true); return false; }
        if (IsOnlineLink || stateOperationInProgress || currentRomPath is null || session?.LatestSnapshot.Rom is not { } rom)
        { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Zuerst ein eigenes GB/GBC- oder unterstütztes GBA-Spiel öffnen; laufende Aktion abwarten"), true); return false; }
        string path = currentRomPath;
        GbaOnlineCompatibility? gba = null;
        try
        {
            if (rom.IsGameBoyAdvance)
            {
                gba = OnlineGbaProfileInspector(path);
                if (!gba.IsDevelopmentCandidate)
                { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("GBA-Online nicht freigegeben: ") + global::AetherBoy.Runtime.Localization.UiText.Get(gba.Reason) + global::AetherBoy.Runtime.Localization.UiText.Get(" · Lokales Link Lab bleibt verfügbar"), true); return false; }
            }
            else if (!(Path.GetExtension(path).Equals(".gb", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(path).Equals(".gbc", StringComparison.OrdinalIgnoreCase)))
            { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Kein passendes Online-Kabelprofil für dieses Spiel"), true); return false; }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Online-Profil konnte nicht geprüft werden: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message), true); return false; }
        string profile = gba is null ? global::AetherBoy.Runtime.Localization.UiText.Get("GB/GBC: Online Link im Test") : global::AetherBoy.Runtime.Localization.UiText.Get("GBA: Pokémon Gen3 (Test)\n") + gba.DisplayName;
        if (confirm && AetherSignal.Show(this,
            profile + "\n\n" +
            global::AetherBoy.Runtime.Localization.UiText.Get("Online Link ist noch in Entwicklung. Ein erfolgreicher Pokémon-Tausch ist nicht bestätigt. ") +
            (gba is null ? "" : global::AetherBoy.Runtime.Localization.UiText.Get("GBA lässt sich nur mit diesem Gen3-Profil testen, nicht mit ROM-Hacks. Dabei wird das HLE-BIOS verwendet. ")) +
            global::AetherBoy.Runtime.Localization.UiText.Get("Das Spiel startet mit einer Kopie deines Spielstands. Dein Original wird nicht ersetzt.\n\n") +
            global::AetherBoy.Runtime.Localization.UiText.Get("Im Browser ") + (isHost ? global::AetherBoy.Runtime.Localization.UiText.Get("eine Einladung erstellen") : global::AetherBoy.Runtime.Localization.UiText.Get("die Einladung deines Mitspielers beantworten")) +
            global::AetherBoy.Runtime.Localization.UiText.Get(". Schickt euch Einladung und Antwort per Chat. Lasst die Browserseiten offen und spielt im Emulator. ") +
            global::AetherBoy.Runtime.Localization.UiText.Get("Je nach Netzwerk kann ein STUN- oder TURN-Server nötig sein.\n\n") +
            global::AetherBoy.Runtime.Localization.UiText.Get("ROMs und vollständige Spielstände werden nicht übertragen. Turbo, Zurückspulen, Schnellspeicherstände, Neustart und Cheats sind gesperrt. ") +
            global::AetherBoy.Runtime.Localization.UiText.Get("Nach der Sitzung beide Spielstandkopien prüfen. Online Link jetzt starten?"),
            global::AetherBoy.Runtime.Localization.UiText.Get("Online-Link starten"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return false;

        EmulatorConfiguration configuration = CreateEmulatorConfiguration();
        int palette = settings.PaletteIndex;
        string save = WindowsRomLibrary.Default.GetSavePath(path);
        updateTimer.Stop();
        try
        {
            if (!StopSession()) throw new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("Das aktuelle Spiel konnte nicht sicher beendet werden."));
            settings.UseGameProfile(path);
            IOnlineLinkTransport transport = startNativeRoom
                ? new OnlineRoomTransport(roomSettings, isHost, roomCodeInput, gba is null ? "gb-serial-v1" : GbaOnlineProfileCatalog.PokemonGen3Profile,
                    (Program.StartupDiagnosticsDecision ?? Diagnostics.WindowsDiagnosticsPreferences.Default.GetStatus()).RecordingRequested
                        ? Path.Combine(WindowsDataPaths.Default.Development, "OnlineDiagnostics") : null)
                : new WebRtcBrowserTransport();
            try
            {
                string directory = Path.Combine(WindowsDataPaths.Default.Development, "OnlineLink",
                    $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
                session = OnlineLinkSessionStarter(path, save, directory, isHost, transport, configuration, palette, gba is not null);
                onlineLinkTransport = transport as WebRtcBrowserTransport;
                onlineRoomTransport = transport as OnlineRoomTransport; onlineLinkDirectory = directory;
                onlineRecoveryTargetSave = save; onlineRecoveryRomPath = path;
                currentRomPath = path;
                pendingResume = false; activityClock.Reset();
                sessionFaultReported = false; testerRomIdentityRecorded = false;
                batteryRecoveryNoticeShown = true; currentSessionUsesExternalBootRom = false;
                displayedFrameSequence = 0; gamepadAwaitNeutral = true; lastOnlineStatus = null;
                lastOnlineDiagnostic = null;
                lastPadState = GamepadInput.GetState(); gameView.ClearFrame();
                if (configuration.AudioEnabled && TryCreateAudioOutput(out NAudioSoundOut output))
                { Volatile.Write(ref audioOutput, output); session.AudioSamplesAvailable += Session_AudioSamplesAvailable; }
                if (audiotoolwindow is { IsDisposed: false }) audiotoolwindow.Session = session;
                UpdateAetherSessionUi(session.LatestSnapshot);
                if (!startNativeRoom) OpenOnlineLinkBrowser();
                return true;
            }
            catch { transport.Dispose(); throw; }
        }
        catch (Exception ex)
        {
            testerSession?.RecordException("online_link.start_failed", ex);
            SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Online-Link konnte nicht starten: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message), true);
            return false;
        }
        finally { updateTimer.Start(); }
    }

    private void OpenOnlineLinkBrowser()
    {
        if (!IsOnlineLink || onlineLinkTransport is null)
        { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Zuerst eine Online-Sitzung erstellen oder beitreten"), true); return; }
        try { OnlineLinkBrowserLauncher(onlineLinkTransport.ConnectionPageUrl); }
        catch (Exception ex)
        { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Browser konnte nicht geöffnet werden: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message), true); }
    }

    private void ShowOnlineConnectionTest()
    {
        if (onlineProbeDialog is { IsDisposed: false }) { onlineProbeDialog.BringToFront(); return; }
        // A single settings editor prevents the game-room window from retaining stale credentials.
        onlineRoomDialog?.Close();
        var dialog = new frmOnlineConnectionTest(RoomSettingsPath,
            Path.Combine(WindowsDataPaths.Default.Development, "OnlineDiagnostics"),
            (Program.StartupDiagnosticsDecision ?? Diagnostics.WindowsDiagnosticsPreferences.Default.GetStatus()).RecordingRequested,
            () => !IsOnlineLink && !stateOperationInProgress, OnlineProbeSessionFactory);
        onlineProbeDialog = dialog;
        dialog.Disposed += (_, _) => { if (ReferenceEquals(onlineProbeDialog, dialog)) onlineProbeDialog = null; };
        dialog.Show(this);
    }

    private void UpdateOnlineLinkUi()
    {
        if (session?.OnlineLink is not { } online) return;
        string connection = online.Phase == OnlineLinkPhase.WaitingForBrowser && onlineRoomTransport is { } room
            ? RoomStatus(room) : OnlinePhaseLabel(online.Phase);
        connection = connection.TrimEnd(' ', '.', '…');
        string message = online.Failure is null
            ? global::AetherBoy.Runtime.Localization.UiText.Format("Online Link: {0}. Kabelübertragungen: {1}. Tausch noch nicht bestätigt.", connection, online.TransfersCompleted)
            : global::AetherBoy.Runtime.Localization.UiText.Get("Online Link unterbrochen. Prüfe den Verbindungsbericht und beide Spielstandkopien.");
        if (message == lastOnlineStatus) return;
        lastOnlineStatus = message;
        SetSaveFeedback(message, online.Failure is not null);
    }

    private bool FinishStoppedOnlineLink()
    {
        // As on Wayland, a peer may end the owner without a local Disconnect.
        // Never release the session while its owner still flushes/disposes.
        if (session is not { OnlineLink: { } online } current ||
            current.State is not (SessionState.Stopped or SessionState.Faulted)) return false;
        frameTiming.SetActive(false);
        DisposeAudioOutput(current);
        if (!current.Completion.IsCompleted || stateOperationInProgress) return true;

        bool failed = current.State == SessionState.Faulted || online.Phase == OnlineLinkPhase.Faulted;
        bool nativeRoom = onlineRoomTransport is not null;
        bool transportFailed = onlineRoomTransport?.Fault is not null || onlineLinkTransport?.Fault is not null ||
            onlineLinkTransport?.BrowserFailure is not null;
        string reason = failed ? DescribeOnlineSessionFailure(current.Fault, online.Failure, nativeRoom,
            onlineRoomTransport?.Fault ?? onlineLinkTransport?.Fault, onlineLinkTransport?.BrowserFailure)
            : global::AetherBoy.Runtime.Localization.UiText.Get("Online-Link beendet. Beide Sitzungskopien vor einer bewussten Übernahme prüfen.");
        string diagnostic = reason + global::AetherBoy.Runtime.Localization.UiText.Get("\n\nOriginalspielstände wurden nicht ersetzt. Ein Verbindungsende bestätigt keinen erfolgreichen Tausch. ") +
            global::AetherBoy.Runtime.Localization.UiText.Get("Sitzungskopien: TOOLS → Online Link → Online-Spielstände öffnen.\n\n") +
            (failed && !transportFailed
                ? global::AetherBoy.Runtime.Localization.UiText.Get("Beide AetherBoy-Builds und das gewählte Kabelprofil prüfen. Die Sitzung wurde im Emulator beendet; daraus folgt kein nachgewiesener Router- oder Serverfehler.")
                : nativeRoom
                ? global::AetherBoy.Runtime.Localization.UiText.Get("Für einen neuen Versuch das eigene Spiel öffnen und einen neuen Raum erstellen oder per Raumcode beitreten. Servereinstellungen im Raumdialog prüfen.")
                : global::AetherBoy.Runtime.Localization.UiText.Get("Für einen neuen Versuch das eigene Spiel öffnen, eine neue Online-Sitzung starten und neue Einladung/Antwort austauschen."));
        if (failed && !sessionFaultReported)
        {
            sessionFaultReported = true;
            testerSession?.RecordException("online_link.faulted", current.Fault);
            Program.WriteCrashLog(current.Fault);
        }
        if (!StopSession()) return true;
        pendingResume = false; activityClock.Reset();
        gamepadAwaitNeutral = true; lastOnlineStatus = null;
        gameView.ClearFrame();
        lastOnlineDiagnostic = diagnostic;
        // Keep the action legible in the narrow status area; the menu opens the
        // complete cause and save-safety guidance without truncating either.
        SetSaveFeedback(failed ? global::AetherBoy.Runtime.Localization.UiText.Get("Online-Sitzung unterbrochen. Diagnose unter TOOLS → Online Link.")
            : reason + global::AetherBoy.Runtime.Localization.UiText.Get(" · TOOLS → Online Link → Letzte Verbindungsdiagnose"), failed);
        return true;
    }

    internal static string DescribeOnlineRoomFailure(Exception? failure)
    {
        if (failure is OnlineRoomRequestException request)
            return request.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => global::AetherBoy.Runtime.Localization.UiText.Get("Der Server-Zugangsschlüssel stimmt nicht. Prüfe die Servereinstellungen."),
                System.Net.HttpStatusCode.NotFound => global::AetherBoy.Runtime.Localization.UiText.Get("Der Raum wurde nicht gefunden oder ist abgelaufen. Prüfe den Raumcode."),
                System.Net.HttpStatusCode.Conflict => global::AetherBoy.Runtime.Localization.UiText.Get("Der Raum ist voll oder nutzt ein anderes Spielsystem. Einen passenden neuen Raum erstellen."),
                System.Net.HttpStatusCode.BadRequest => global::AetherBoy.Runtime.Localization.UiText.Get("Der Raumserver hat die Anfrage abgelehnt. Serverversion und gewähltes Kabelprofil prüfen."),
                System.Net.HttpStatusCode.TooManyRequests => global::AetherBoy.Runtime.Localization.UiText.Get("Zu viele Verbindungsversuche. Warte eine Minute und versuche es erneut."),
                _ => global::AetherBoy.Runtime.Localization.UiText.Get("Der Raumserver konnte die Anfrage nicht abschließen. Den lokalen Verbindungsbericht prüfen.")
            };
        // Room transports can also carry arbitrary native/JSON exceptions. Never
        // echo their free-form text, URLs or credentials in the diagnostic dialog.
        string? known = failure?.Message switch
        {
            "The server access key is incorrect. Open Server settings." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Der Server-Zugangsschlüssel stimmt nicht. Prüfe die Servereinstellungen."),
            "Cannot reach the room server. Check its address and HTTPS certificate." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Der Raumserver ist nicht erreichbar. Serveradresse und HTTPS-Zertifikat prüfen."),
            "The room server returned an invalid relay configuration." or "Invalid relay configuration." or "Invalid relay URL." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Der Raumserver hat ungültige Relay-Einstellungen geliefert. Die TURN-Konfiguration am Server prüfen."),
            "Native online support is missing from this build. Install a complete AetherBoy build." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Die native Online-Bibliothek fehlt. Einen vollständigen AetherBoy-Build installieren."),
            "The native online library is incompatible. Install the complete libdatachannel 0.24.5 build." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Die native Online-Bibliothek passt nicht zu diesem Build. Einen vollständigen AetherBoy-Build installieren."),
            "This native build needs a TURN/UDP address. Use the manual browser connection for TURN/TCP or TLS." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Dieser native Build benötigt TURN über UDP. Die TURN-Adresse prüfen; TCP und TLS werden hier noch nicht unterstützt."),
            "The peer uses an incompatible data channel." or "The peer opened more than one data channel." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Die Gegenstelle verwendet einen unpassenden Datenkanal. Beide AetherBoy-Builds prüfen."),
            "The online receive queue is full." or "The online send queue is full." or "Invalid online packet size." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Der Datenpuffer ist voll oder ein Paket hat eine unzulässige Größe. Die Sitzung wurde beendet; den Verbindungsbericht prüfen."),
            "The room contains incompatible connection data." or "The room server response is too large." or
            "The room server returned an empty response." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Der Raumserver hat unpassende Verbindungsdaten geliefert. Serverversion und Verbindungsbericht prüfen."),
            _ => null
        };
        if (known is not null) return known;
        if (failure is TimeoutException)
            return global::AetherBoy.Runtime.Localization.UiText.Get("Beim Raumaufbau wurde eine Wartefrist überschritten. Den Verbindungsbericht prüfen und einen neuen Raum erstellen.");
        return global::AetherBoy.Runtime.Localization.UiText.Get("Die Online-Raumsitzung wurde unterbrochen. Raumstatus und lokalen Diagnosebericht prüfen; die genaue Ursache ist nicht bekannt.");
    }

    internal static string DescribeOnlineSessionFailure(Exception? ownerFailure, string? sessionFailure,
        bool nativeRoom, Exception? transportFailure, WebRtcBrowserFailure? browserFailure)
    {
        if (transportFailure is not null || browserFailure is not null)
            return nativeRoom ? DescribeOnlineRoomFailure(transportFailure) : DescribeOnlineLinkFailure(browserFailure);

        // The owner can fail while the transport remains healthy. Do not replace a
        // protocol/save/core error with a guessed ICE failure. Only known messages
        // are translated; arbitrary exception text can contain paths or secrets.
        string? failure = sessionFailure ?? ownerFailure?.GetBaseException().Message;
        string? detail = failure switch
        {
            "The local game requested an internal-clock transfer, but no matching peer offer arrived before the cable timeout. Start a new session." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Das lokale GB/GBC-Spiel wollte Daten senden, aber das passende Übertragungsangebot des Mitspielers blieb aus. Beide Sitzungen neu starten."),
            "Both games offered a serial transfer, but the peer's readiness acknowledgement did not arrive before the cable timeout. Start a new session." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Beide GB/GBC-Spiele boten Daten an, aber die Bereitschaftsbestätigung der Gegenstelle blieb aus. Beide Sitzungen neu starten."),
            "The local serial byte completed, but the peer's completion acknowledgement did not arrive before the cable timeout. Start a new session." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Das lokale GB/GBC-Byte wurde übertragen, aber die Abschlussbestätigung der Gegenstelle blieb aus. Beide Sitzungen neu starten."),
            "The local Gen3 game reported an inconsistent serial checksum." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Die serielle Prüfsumme des GBA-Spiels stimmt nicht mit den übertragenen Wörtern überein."),
            "The Gen3 phase ended with undelivered commands; session stopped to protect its outcome." or
            "An undelivered Gen3 command crossed a phase reset; the outcome is uncertain." or
            "GBA Online ended with undelivered commands. The working copies need manual review." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Der GBA-Linkabschnitt endete mit noch nicht zugestellten Spielbefehlen. Beide Spielstandkopien prüfen."),
            "The local Gen3 game did not finish its link section before the phase deadline; restart both sessions." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Der Mitspieler hat den GBA-Linkabschnitt beendet, das lokale Spiel jedoch nicht innerhalb der Wartefrist. Beide Sitzungen neu starten."),
            "GBA Online ended before the local game finished its link section. The working copies need manual review." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Die GBA-Sitzung endete vor dem lokalen Linkabschnitt. Beide Spielstandkopien prüfen; der Abschluss ist unbestätigt."),
            "The game cancelled a Gen3 serial transfer during an active command frame." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Das GBA-Spiel hat eine laufende Befehlsübertragung abgebrochen."),
            "The Gen3 online profile requires 115200-baud multiplayer mode." or
            "The game changed serial mode or baud during a Gen3 protocol transfer." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Das GBA-Spiel verwendet einen seriellen Modus oder Takt, den dieses Online-Profil nicht unterstützt."),
            "Unsupported AetherBoy online-link protocol." or
            "Incompatible online-link handshake (GB/GBC protocol v1 required)." or
            "GBA Gen3 needs AetherBoy protocol v2; GB/GBC and other profiles cannot be mixed." or
            "Both peers need the same GBA Pokémon Gen3 development profile and explicit consent." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Das empfangene Kabelprotokoll passt nicht zu dieser Sitzung. Beide Builds und Kabelprofile prüfen."),
            "Choose one host and one guest, not two identical roles." or "Choose one host and one guest." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Beide Teilnehmer haben dieselbe Rolle gewählt. Einer erstellt die Sitzung, der andere tritt bei."),
            "The ROM changed after its GBA online profile was inspected." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Die ROM-Datei wurde nach der Profilprüfung verändert. Das Spiel neu öffnen und sein Online-Profil erneut prüfen."),
            "A session cannot connect to itself." or "A GBA online session cannot connect to itself." =>
                global::AetherBoy.Runtime.Localization.UiText.Get("Die Sitzung wurde mit sich selbst verbunden. Die Einladung beziehungsweise den Raumcode des Mitspielers verwenden."),
            _ => null
        };
        if (detail is not null) return global::AetherBoy.Runtime.Localization.UiText.Get("Online-Sitzung unterbrochen: ") + detail;
        if (ownerFailure?.GetBaseException() is InvalidDataException)
            return global::AetherBoy.Runtime.Localization.UiText.Get("Die Sitzung hat unpassende Daten oder einen unerwarteten Ablauf erkannt. Den lokalen Diagnosebericht prüfen; die genaue Ursache ist noch nicht eingegrenzt.");
        if (ownerFailure?.GetBaseException() is TimeoutException)
            return global::AetherBoy.Runtime.Localization.UiText.Get("Die Online-Sitzung hat eine Wartefrist überschritten. Den lokalen Diagnosebericht prüfen; die Meldung allein belegt keinen Netzwerkfehler.");
        if (ownerFailure is not null || !string.IsNullOrWhiteSpace(failure))
            return global::AetherBoy.Runtime.Localization.UiText.Get("Die Online-Sitzung wurde wegen eines Fehlers im Emulator beendet. Den lokalen Diagnosebericht prüfen; die Ursache ist noch nicht eingegrenzt.");
        return nativeRoom ? DescribeOnlineRoomFailure(null) : DescribeOnlineLinkFailure(null);
    }

    internal static string DescribeOnlineLinkFailure(WebRtcBrowserFailure? failure) => failure switch
    {
        WebRtcBrowserFailure.PeerConnection => global::AetherBoy.Runtime.Localization.UiText.Get("WebRTC-Verbindung zum Mitspieler fehlgeschlagen. STUN ermittelt Verbindungswege, ist aber kein Relay. ") +
            global::AetherBoy.Runtime.Localization.UiText.Get("Ein TURN-Relay kann bei blockierter Direktverbindung helfen; die Meldung allein beweist keinen Routerfehler."),
        WebRtcBrowserFailure.DataChannel => global::AetherBoy.Runtime.Localization.UiText.Get("Der WebRTC-Datenkanal ist fehlgeschlagen. Browserstatus prüfen und eine neue Sitzung starten."),
        WebRtcBrowserFailure.ChannelProtocol => global::AetherBoy.Runtime.Localization.UiText.Get("Unpassender Datenkanal. Beide Spieler müssen kompatible AetherBoy-Builds verwenden."),
        WebRtcBrowserFailure.LocalConnection => global::AetherBoy.Runtime.Localization.UiText.Get("Die lokale Verbindung zwischen Browser und Emulator ist unterbrochen. Eine neue Online-Sitzung starten."),
        WebRtcBrowserFailure.SendFailed => global::AetherBoy.Runtime.Localization.UiText.Get("Der Browser konnte die Link-Daten nicht weiterleiten. Eine neue Online-Sitzung starten."),
        WebRtcBrowserFailure.PacketLimit => global::AetherBoy.Runtime.Localization.UiText.Get("Ungültige Link-Pakete oder voller Datenpuffer. Die Verbindung wurde sicher beendet."),
        WebRtcBrowserFailure.EarlyPacket => global::AetherBoy.Runtime.Localization.UiText.Get("Link-Daten wurden vor einem bereiten Browserkanal gesendet. Beide Builds prüfen und neu verbinden."),
        WebRtcBrowserFailure.Unknown => global::AetherBoy.Runtime.Localization.UiText.Get("Der Browser meldet einen Verbindungsfehler. Die Link-Bridge-Seite prüfen und eine neue Sitzung starten."),
        _ => global::AetherBoy.Runtime.Localization.UiText.Get("Die Online-Sitzung wurde unterbrochen. Browserstatus und lokalen Diagnosebericht prüfen; die genaue Ursache ist nicht bekannt.")
    };

    private void ShowOnlineLinkDiagnostic() => AetherSignal.Show(this,
        lastOnlineDiagnostic ?? onlineRoomTransport?.Status ?? global::AetherBoy.Runtime.Localization.UiText.Get("Noch keine abgeschlossene Verbindungsdiagnose vorhanden. Den aktuellen Status auf der Link-Bridge-Seite prüfen."),
        global::AetherBoy.Runtime.Localization.UiText.Get("Online-Link · Verbindungsdiagnose"), MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void ShowOnlineLinkProfile()
    {
        string text = global::AetherBoy.Runtime.Localization.UiText.Get("GB/GBC verwendet unser serielles Kabelprotokoll. GBA verwendet ein separates Pokémon-Gen3-Entwicklungsprofil.\n\n") +
            global::AetherBoy.Runtime.Localization.UiText.Get("GBA-Online verwendet derzeit HLE-BIOS. Eine ausgewählte vollständige GBA-BIOS-Datei wird in diesem Onlinemodus noch nicht übernommen.\n\n") +
            global::AetherBoy.Runtime.Localization.UiText.Get("Keine allgemeine GBA- oder Hack-Kompatibilität. Ein Netzwerkpaket bestätigt niemals einen abgeschlossenen Tausch. ") +
            global::AetherBoy.Runtime.Localization.UiText.Get("Beide Spieler prüfen ihre Sitzungskopien nach dem Neustart; erst danach bewusst übernehmen.");
        try
        {
            if (currentRomPath is not null && session?.LatestSnapshot.Rom?.IsGameBoyAdvance == true)
            {
                var candidate = OnlineGbaProfileInspector(currentRomPath);
                text = candidate.DisplayName + "\n" + global::AetherBoy.Runtime.Localization.UiText.Get(candidate.Reason) + "\n\n" + text;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { text = global::AetherBoy.Runtime.Localization.UiText.Get("Profil konnte nicht gelesen werden: ") + ex.Message; }
        AetherSignal.Show(this, text, global::AetherBoy.Runtime.Localization.UiText.Get("Online-Profil · Testversion"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string OnlinePhaseLabel(OnlineLinkPhase phase) => phase switch
    {
        OnlineLinkPhase.WaitingForBrowser => global::AetherBoy.Runtime.Localization.UiText.Get("Browser wird verbunden"),
        OnlineLinkPhase.WaitingForPeer => global::AetherBoy.Runtime.Localization.UiText.Get("Warte auf den Mitspieler"),
        OnlineLinkPhase.Playing => global::AetherBoy.Runtime.Localization.UiText.Get("Verbunden"),
        OnlineLinkPhase.WaitingForTransfer => global::AetherBoy.Runtime.Localization.UiText.Get("Warte auf Daten aus dem Spiel"),
        OnlineLinkPhase.Closed => global::AetherBoy.Runtime.Localization.UiText.Get("Sitzung beendet"),
        OnlineLinkPhase.Faulted => global::AetherBoy.Runtime.Localization.UiText.Get("Verbindung abgebrochen"),
        _ => global::AetherBoy.Runtime.Localization.UiText.Get("Online Link wird gestartet")
    };
}
