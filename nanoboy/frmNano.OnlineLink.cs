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
        var online = new ToolStripMenuItem("Online Link · GB/GBC + GBA Gen3 DEV") { Name = "menuOnlineLink" };
        void Add(string name, string text, Action action)
        {
            var item = new ToolStripMenuItem(text) { Name = name };
            item.Click += (_, _) => action(); online.DropDownItems.Add(item);
        }
        Add("menuOnlineHost", "Sitzung erstellen · Strg+F10", () => StartOnlineLink(true));
        Add("menuOnlineJoin", "Sitzung beitreten · Strg+Umschalt+F10", () => StartOnlineLink(false));
        Add("menuOnlineBrowser", "Verbindungsseite öffnen", OpenOnlineLinkBrowser);
        Add("menuOnlineProfile", "Aktuelles Profil und Grenzen", ShowOnlineLinkProfile);
        Add("menuOnlineSaves", "Online-Spielstände öffnen", () => WindowsDataPaths.OpenFolder(this,
            onlineLinkDirectory ?? Path.Combine(WindowsDataPaths.Default.Development, "OnlineLink")));
        Add("menuOnlineRecovery", "Sitzungskopien prüfen / übernehmen", ShowOnlineSaveRecovery);
        Add("menuOnlineStop", "Online-Verbindung beenden", () =>
        {
            if (IsOnlineLink && StopSession()) SetSaveFeedback("Online-Link beendet · Sitzungskopie bleibt im Online-Ordner", false);
        });
        menuItem21.DropDownItems.Add(online);
    }

    internal bool StartOnlineLink(bool isHost, bool confirm = true)
    {
        if (IsOnlineLink || stateOperationInProgress || currentRomPath is null || session?.LatestSnapshot.Rom is not { } rom)
        { SetSaveFeedback("Zuerst ein eigenes GB/GBC- oder unterstütztes GBA-Spiel öffnen; laufende Aktion abwarten", true); return false; }
        string path = currentRomPath;
        GbaOnlineCompatibility? gba = null;
        try
        {
            if (rom.IsGameBoyAdvance)
            {
                gba = OnlineGbaProfileInspector(path);
                if (!gba.IsDevelopmentCandidate)
                { SetSaveFeedback("GBA-Online nicht freigegeben: " + gba.Reason + " · Lokales Link Lab bleibt verfügbar", true); return false; }
            }
            else if (!(Path.GetExtension(path).Equals(".gb", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(path).Equals(".gbc", StringComparison.OrdinalIgnoreCase)))
            { SetSaveFeedback("Kein passendes Online-Kabelprofil für dieses Spiel", true); return false; }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { SetSaveFeedback("Online-Profil konnte nicht geprüft werden: " + ex.Message, true); return false; }
        string profile = gba is null ? "GB/GBC · KABEL-PROTOTYP" : "GBA · POKÉMON GEN3 · DEVELOPMENT\n" + gba.DisplayName;
        if (confirm && AetherSignal.Show(this,
            profile + "\n\n" +
            "Nicht als erfolgreicher Pokémon-Tausch geprüft. " +
            (gba is null ? "" : "Nur dieses Gen3-Profil, keine Hack-Freigabe. GBA-Online nutzt HLE-BIOS; deine Voll-BIOS-Auswahl wird hier noch nicht übernommen. ") +
            "Dein Spiel startet mit einer lokalen Kopie. Originalspielstände werden nicht automatisch ersetzt.\n\n" +
            "Im Browser " + (isHost ? "eine Einladung erstellen" : "aus der Einladung eine Antwort erstellen") +
            "; beide Codes per Chat austauschen. Browser offen lassen, hier spielen. STUN/TURN kann nötig sein.\n\n" +
            "Keine ROM- oder vollständige Spielstanddatei wird verschickt. Turbo, Rewind, States, Reset und Cheats sind gesperrt. " +
            "Ergebnis nach Abbruch: ungeklärt. Entwicklungssitzung starten?",
            "Online-Link starten", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return false;

        EmulatorConfiguration configuration = CreateEmulatorConfiguration();
        int palette = settings.PaletteIndex;
        string save = WindowsRomLibrary.Default.GetSavePath(path);
        updateTimer.Stop();
        try
        {
            if (!StopSession()) throw new InvalidOperationException("Das aktuelle Spiel konnte nicht sicher beendet werden.");
            settings.UseGameProfile(path);
            var transport = new WebRtcBrowserTransport();
            try
            {
                string directory = Path.Combine(WindowsDataPaths.Default.Development, "OnlineLink",
                    $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
                session = OnlineLinkSessionStarter(path, save, directory, isHost, transport, configuration, palette, gba is not null);
                onlineLinkTransport = transport; onlineLinkDirectory = directory;
                onlineRecoveryTargetSave = save; onlineRecoveryRomPath = path;
                currentRomPath = path;
                pendingResume = false; pendingPlaySeconds = 0; activityWasRunning = false;
                sessionFaultReported = false; testerRomIdentityRecorded = false;
                batteryRecoveryNoticeShown = true; currentSessionUsesExternalBootRom = false;
                displayedFrameSequence = 0; gamepadAwaitNeutral = true; lastOnlineStatus = null;
                lastPadState = GamepadInput.GetState(); gameView.ClearFrame();
                if (configuration.AudioEnabled && TryCreateAudioOutput(out NAudioSoundOut output))
                { Volatile.Write(ref audioOutput, output); session.AudioSamplesAvailable += Session_AudioSamplesAvailable; }
                if (audiotoolwindow is { IsDisposed: false }) audiotoolwindow.Session = session;
                UpdateAetherSessionUi(session.LatestSnapshot);
                OpenOnlineLinkBrowser();
                return true;
            }
            catch { transport.Dispose(); throw; }
        }
        catch (Exception ex)
        {
            testerSession?.RecordException("online_link.start_failed", ex);
            SetSaveFeedback("Online-Link konnte nicht starten: " + ex.Message, true);
            return false;
        }
        finally { updateTimer.Start(); }
    }

    private void OpenOnlineLinkBrowser()
    {
        if (!IsOnlineLink || onlineLinkTransport is null)
        { SetSaveFeedback("Zuerst eine Online-Sitzung erstellen oder beitreten", true); return; }
        try { OnlineLinkBrowserLauncher(onlineLinkTransport.ConnectionPageUrl); }
        catch (Exception ex)
        { SetSaveFeedback("Browser konnte nicht geöffnet werden: " + ex.Message, true); }
    }

    private void UpdateOnlineLinkUi()
    {
        if (session?.OnlineLink is not { } online) return;
        string message = $"ONLINE · {online.DisplayName} · {OnlinePhaseLabel(online.Phase)} · {online.TransfersCompleted} Transfers · " +
            (online.Failure ?? "Sitzungskopie · Tausch/Speichererfolg nicht bestätigt");
        if (message == lastOnlineStatus) return;
        lastOnlineStatus = message;
        SetSaveFeedback(message, online.Failure is not null);
    }

    private void ShowOnlineLinkProfile()
    {
        string text = "GB/GBC verwendet unser serielles Kabelprotokoll. GBA verwendet ein separates Pokémon-Gen3-Entwicklungsprofil.\n\n" +
            "GBA-Online verwendet derzeit HLE-BIOS. Eine ausgewählte vollständige GBA-BIOS-Datei wird in diesem Onlinemodus noch nicht übernommen.\n\n" +
            "Keine allgemeine GBA- oder Hack-Kompatibilität. Ein Netzwerkpaket bestätigt niemals einen abgeschlossenen Tausch. " +
            "Beide Spieler prüfen ihre Sitzungskopien nach dem Neustart; erst danach bewusst übernehmen.";
        try
        {
            if (currentRomPath is not null && session?.LatestSnapshot.Rom?.IsGameBoyAdvance == true)
            {
                var candidate = OnlineGbaProfileInspector(currentRomPath);
                text = candidate.DisplayName + "\n" + candidate.Reason + "\n\n" + text;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { text = "Profil konnte nicht gelesen werden: " + ex.Message; }
        AetherSignal.Show(this, text, "Online-Profil · DEVELOPMENT", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string OnlinePhaseLabel(OnlineLinkPhase phase) => phase switch
    {
        OnlineLinkPhase.WaitingForBrowser => "Browser verbinden",
        OnlineLinkPhase.WaitingForPeer => "Gegenstelle wird geprüft",
        OnlineLinkPhase.Playing => "Verbunden",
        OnlineLinkPhase.WaitingForTransfer => "Warte auf Kabeldaten",
        OnlineLinkPhase.Closed => "Beendet · Ergebnis prüfen",
        OnlineLinkPhase.Faulted => "Abbruch · Ergebnis ungeklärt",
        _ => phase.ToString()
    };
}
