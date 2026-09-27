using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AetherBoy.Runtime.Netplay;
using nanoboy.Controls;
using nanoboy.Storage;

namespace nanoboy;

/// <summary>Dedicated probe room. No emulator session, ROM path or save API is passed to this window.</summary>
internal sealed class frmOnlineConnectionTest : Form
{
    private readonly string settingsPath, reportDirectory;
    private readonly Func<bool> canStart;
    private readonly Func<OnlineRoomSettings, bool, string, string?, IOnlineProbeSession> factory;
    private readonly TextBox url, key, code, results;
    private readonly CheckBox recording;
    private readonly Label status, feedback;
    private readonly Label[] steps = new Label[4];
    private readonly AetherButton create, join, stop, copyCode, save, copyReport, openReports;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 100 };
    private IOnlineProbeSession? session;
    private bool closing, allowClose;
    private Task? stopping;
    private string? actionError;
    internal bool IsTestActive => session?.Snapshot.Active == true;
    internal Action<string> CopyText = Clipboard.SetText;
    internal Action<IWin32Window, string> OpenReports = WindowsDataPaths.OpenFolder;

    internal frmOnlineConnectionTest(string settingsPath, string reportDirectory, bool recordByDefault,
        Func<bool> canStart, Func<OnlineRoomSettings, bool, string, string?, IOnlineProbeSession> factory)
    {
        this.settingsPath = settingsPath; this.reportDirectory = reportDirectory;
        this.canStart = canStart; this.factory = factory;
        Name = "onlineConnectionTest"; Text = "Verbindung testen";
        ClientSize = new Size(800, 702); StartPosition = FormStartPosition.CenterParent;
        AddLabel(this, "probeIntro", "Zwei PCs. Ein Test-Raum. Keine ROM nötig.\nBeide wählen Verbindung testen; einer erstellt den Raum, der andere tritt per Code bei.", new(24, 12, 752, 38));

        var config = new AetherSurfacePanel { Bounds = new(24, 60, 752, 144), AccentEdge = true };
        Controls.Add(config);
        var settings = OnlineRoomSettings.Load(settingsPath);
        AddLabel(config, "probeServerCaption", "RAUMSERVER · HTTPS", new(16, 10, 416, 22));
        AddLabel(config, "probeKeyCaption", "PRIVATER ZUGANGSSCHLÜSSEL", new(448, 10, 288, 22));
        url = Input(config, "probeServerUrl", "Raumserver HTTPS-Adresse", settings.ServerUrl, new(16, 36, 416, 28));
        key = Input(config, "probeServerKey", "Server-Zugangsschlüssel", settings.AccessKey, new(448, 36, 288, 28));
        key.UseSystemPasswordChar = true;
        save = Button(config, "probeSaveServer", "SERVER SPEICHERN", new(16, 78, 202, 36), SaveSettings);
        recording = new CheckBox { Name = "probeRecording", Text = "Bereinigten Bericht lokal speichern", Checked = recordByDefault,
            Bounds = new(234, 80, 496, 32), AccessibleName = "Diagnosebericht lokal speichern" };
        config.Controls.Add(recording);
        feedback = AddLabel(config, "probeFeedback", "Dieselben Servereinstellungen wie im Online-Link. Kein automatischer Upload.", new(16, 116, 718, 24));

        AddLabel(this, "probeCodeCaption", "TEST-RAUMCODE", new(24, 218, 480, 24));
        code = Input(this, "probeRoomCode", "Test-Raumcode", "", new(24, 248, 486, 38));
        code.MaxLength = 14; code.Font = new Font("Segoe UI", 18); code.PlaceholderText = "ABCDE-FGHJK";
        copyCode = Button(this, "probeCopyCode", "CODE KOPIEREN", new(526, 244, 250, 42), () => Copy(session?.Snapshot.Connection.DisplayCode));
        create = Button(this, "probeCreate", "TEST-RAUM ERSTELLEN", new(24, 304, 240, 42), () => Start(true));
        create.Kind = AetherButtonKind.Primary;
        join = Button(this, "probeJoin", "MIT CODE TESTEN", new(280, 304, 240, 42), () => Start(false));
        stop = Button(this, "probeStop", "ABBRECHEN", new(536, 304, 240, 42), () => _ = StopAsync());
        status = AddLabel(this, "probeStatus", "", new(24, 358, 752, 46));

        string[] captions = { "01 · RAUMSERVER", "02 · GEGENÜBER", "03 · DATENKANAL", "04 · DATENPRÜFUNG" };
        for (int i = 0; i < steps.Length; i++)
        {
            var card = new AetherSurfacePanel { Bounds = new(24 + i * 192, 416, 176, 62) };
            Controls.Add(card);
            AddLabel(card, "probeStepCaption" + i, captions[i], new(10, 7, 158, 20));
            steps[i] = AddLabel(card, "probeStep" + i, "Ausstehend", new(10, 30, 158, 26));
        }
        results = Input(this, "probeResults", "Ergebnis des Verbindungstests", "", new(24, 490, 752, 112));
        results.Multiline = true; results.ReadOnly = true; results.ScrollBars = ScrollBars.Vertical;
        results.MaxLength = 20000; results.Font = new Font("Consolas", 9);
        copyReport = Button(this, "probeCopyReport", "BERICHT KOPIEREN", new(24, 618, 240, 40), () => Copy(session?.GetDiagnosticReport()));
        openReports = Button(this, "probeOpenReports", "BERICHTORDNER", new(280, 618, 240, 40), () => OpenReports(this, reportDirectory));
        Button(this, "probeClose", "SCHLIESSEN", new(536, 618, 240, 40), Close);
        AddLabel(this, "probePrivacy", "Keine ROMs oder Spielstände. Ein bestandener Verbindungstest ist noch kein erfolgreicher Pokémon-Tausch.", new(24, 672, 752, 24));
        AetherDialog.Apply(this, "ONLINE LINK // CONNECTION LAB", "Native Verbindung · Raumcode statt Copy-Paste-SDP · Originalspielstände unberührt");
        timer.Tick += (_, _) => RefreshStatus(); timer.Start();
        FormClosing += OnClosing;
        RefreshStatus();
    }

    private static Label AddLabel(Control owner, string name, string text, Rectangle bounds)
    {
        var label = new Label { Name = name, Text = text, Bounds = bounds, AutoSize = false };
        owner.Controls.Add(label); return label;
    }
    private static TextBox Input(Control owner, string name, string accessibleName, string text, Rectangle bounds)
    {
        var box = new TextBox { Name = name, Text = text, AccessibleName = accessibleName, Bounds = bounds, MaxLength = 256 };
        owner.Controls.Add(box); return box;
    }
    private static AetherButton Button(Control owner, string name, string text, Rectangle bounds, Action action)
    {
        var button = new AetherButton { Name = name, Text = text, Bounds = bounds };
        button.Click += (_, _) => action(); owner.Controls.Add(button); return button;
    }

    private OnlineRoomSettings ReadSettings() => new(url.Text.Trim(), key.Text.Trim());
    private void SaveSettings()
    {
        try { ReadSettings().Save(settingsPath); actionError = null; feedback.Text = "Server gespeichert. Zugangsschlüssel bleibt maskiert und lokal."; }
        catch (ArgumentException) { feedback.Text = "HTTPS-Adresse ohne Zusatzpfad und gültigen Zugangsschlüssel prüfen."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { feedback.Text = "Servereinstellungen konnten nicht gespeichert werden. Ordnerzugriff prüfen."; }
    }

    private void Start(bool host)
    {
        if (IsTestActive || stopping is not null || closing) return;
        actionError = null;
        if (!canStart()) { actionError = "Zuerst die aktive Online-Spielsitzung beenden und laufende Aktionen abwarten."; RefreshStatus(); return; }
        OnlineRoomSettings settings = ReadSettings();
        try { settings.Validate(); }
        catch (ArgumentException) { actionError = "Zuerst eine gültige HTTPS-Raumserveradresse und den privaten Zugangsschlüssel eintragen."; RefreshStatus(); return; }
        string normalized;
        try { normalized = host ? "" : OnlineRoomSettings.NormalizeCode(code.Text); }
        catch (ArgumentException) { actionError = "Der Test-Raumcode muss zehn gültige Zeichen enthalten, z. B. ABCDE-FGHJK."; RefreshStatus(); return; }
        session?.Dispose(); session = null;
        try
        {
            // This factory cannot receive a ROM or save path. Production always uses relay-only probe rooms.
            session = factory(settings, host, normalized, recording.Checked ? reportDirectory : null);
            code.Text = host ? "" : normalized[..5] + "-" + normalized[5..];
        }
        catch (Exception) { actionError = "Verbindungstest konnte nicht starten. Vollständigen Build und Servereinstellungen prüfen."; }
        RefreshStatus();
    }

    private void Copy(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try { CopyText(text); feedback.Text = "In die Zwischenablage kopiert. Kein automatischer Upload."; }
        catch (Exception e) when (e is System.Runtime.InteropServices.ExternalException or System.Threading.ThreadStateException)
        { feedback.Text = "Zwischenablage ist gerade nicht verfügbar."; }
    }

    internal void RefreshStatus()
    {
        if (IsDisposed) return;
        var state = session?.Snapshot;
        bool active = state?.Active == true, busy = active || stopping is not null || closing;
        url.ReadOnly = key.ReadOnly = code.ReadOnly = busy;
        recording.Enabled = save.Enabled = !busy;
        create.Enabled = join.Enabled = !busy && canStart();
        stop.Enabled = active && stopping is null && !closing;
        stop.Text = state?.Phase == OnlineProbePhase.Passed ? "TEST BEENDEN" : "ABBRECHEN";
        copyCode.Enabled = state is { Active: true, Connection.RoomAdmitted: true } && !closing;
        copyReport.Enabled = session is not null;
        openReports.Enabled = state?.DiagnosticPath is not null;
        if (state is { Active: true, Connection.RoomAdmitted: true }) code.Text = state.Connection.DisplayCode;
        status.Text = actionError ?? (state is null
            ? canStart() ? "Bereit. Beide PCs müssen einen Test-Raum verwenden, keinen Spiel-Raum." : "Zuerst die aktive Online-Spielsitzung beenden und laufende Aktionen abwarten."
            : StatusText(state));
        status.ForeColor = actionError is not null || state?.Phase == OnlineProbePhase.Failed ? AetherColors.Danger
            : state?.Phase == OnlineProbePhase.Passed ? AetherColors.Success : AetherColors.Text;
        bool[] done = { state?.Connection.RoomAdmitted == true, state?.Connection.PeerPresent == true,
            state is { Connection.Connected: true } || state?.Connection.Stage == "connected" || state?.Phase is OnlineProbePhase.Testing or OnlineProbePhase.Passed,
            state?.Phase == OnlineProbePhase.Passed };
        for (int i = 0; i < steps.Length; i++)
        {
            steps[i].Text = done[i] ? "Bestätigt" : "Ausstehend";
            steps[i].ForeColor = done[i] ? AetherColors.Success : AetherColors.Muted;
        }
        if (state?.Phase == OnlineProbePhase.Testing)
            steps[3].Text = $"{state.Progress.VerifiedEchoes} / {state.Progress.ExpectedEchoes} geprüft";
        string resultText = FormatResult(state);
        if (results.Text != resultText) results.Text = resultText;
    }

    private static string StatusText(OnlineProbeSnapshot state)
    {
        if (state.Stopping) return "Verbindung wird beendet … Bitte kurz warten.";
        return state.Phase switch
        {
            OnlineProbePhase.Passed => state.Active
                ? "Hier bestanden. Fenster offen halten, bis BEIDE PCs bestanden melden; danach Test beenden."
                : "Test hier bestanden, Verbindung beendet. Das bestätigt keinen Pokémon-Tausch oder Erfolg der Gegenseite.",
            OnlineProbePhase.Cancelled => "Test abgebrochen. Ein neuer Versuch verwendet einen neuen Raum.",
            OnlineProbePhase.Testing => "Verbindung steht. Prüfe 32, 256, 1024 und 4096 Byte in beiden Richtungen …",
            OnlineProbePhase.Failed => FailureText(state.Failure),
            _ => state.Connection.Stage switch
            {
                "remote-answer" => "Test-Raum bereit. Code teilen und auf den zweiten PC warten …",
                "remote-offer" => "Raum gefunden. Warte auf die Einladung des Hosts …",
                "relay-preparation" or "ice-gathering" => "Raumserver bestätigt. Relay-Verbindungswege werden ermittelt …",
                "publish-description" => "Verbindungsdaten werden automatisch vermittelt …",
                "data-channel-open" => "Gegenüber gefunden. Verschlüsselter Datenkanal wird aufgebaut …",
                _ => "Raumserver wird kontaktiert …",
            },
        };
    }
    private static string FailureText(OnlineProbeFailure failure) => failure switch
    {
        OnlineProbeFailure.AccessKey => "Zugangsschlüssel abgewiesen. Den privaten Raumserver-Schlüssel prüfen, nicht das TURN-Passwort.",
        OnlineProbeFailure.RoomMissing => "Raum nicht gefunden oder abgelaufen. Neuen Test-Raum erstellen und Code prüfen.",
        OnlineProbeFailure.RoomMismatch => "Raum ist belegt oder verwendet ein anderes Profil. Beide PCs müssen Verbindung testen wählen.",
        OnlineProbeFailure.ServerProfile => "Raumserver hat die Anfrage abgewiesen. Unterstützt er bereits transport-probe-v1?",
        OnlineProbeFailure.RateLimit => "Zu viele Verbindungsversuche. Eine Minute warten und neu versuchen.",
        OnlineProbeFailure.Timeout => "Zeitlimit erreicht. Letzte bestätigte Stufe und Bericht prüfen; die Ursache ist noch nicht bewiesen.",
        OnlineProbeFailure.Integrity => "Datenprüfung fehlgeschlagen. Beide Builds und Testeinstellungen abgleichen; Bericht prüfen.",
        _ => "Verbindung nicht erfolgreich. Erreichte Stufen und Bericht prüfen: Server/HTTPS, native Bibliothek oder Relay können beteiligt sein.",
    };

    private static string FormatResult(OnlineProbeSnapshot? state)
    {
        if (state is null) return "Geprüft werden 32 Nachrichten je Richtung, Reihenfolge, Inhalt und Antwortzeit.\r\nKeine ROMs, Spielstände oder Zugangsdaten im kopierbaren Bericht.";
        var text = new StringBuilder();
        text.AppendLine($"Eigene Echos: {state.Progress.VerifiedEchoes}/{state.Progress.ExpectedEchoes} | Gegenanfragen beantwortet: {state.Progress.PeerRequestsEchoed}/{state.Progress.ExpectedEchoes}");
        if (state.Result is { } measured)
        {
            text.AppendLine("Bytes   Geprüft     RTT min / Median / P95 / max (ms)");
            foreach (var size in measured.Sizes)
                text.AppendLine(string.Format(CultureInfo.CurrentCulture, "{0,5}   {1,7}     {2:F1} / {3:F1} / {4:F1} / {5:F1}",
                    size.Bytes, size.Verified, size.MinMs, size.MedianMs, size.P95Ms, size.MaxMs));
        }
        else text.AppendLine("Ein Zeitlimit ist kein gemessener UDP-Paketverlust. Keine Erfolgsgarantie für Spiele.");
        text.Append(state.ReportWriteFailed ? "Bericht konnte nicht gespeichert werden; BERICHT KOPIEREN bleibt verfügbar."
            : state.DiagnosticPath is null ? "Bericht nur im Arbeitsspeicher; Speichern war nicht aktiviert."
            : "Bereinigter Bericht wird lokal im Diagnoseordner gespeichert.");
        return text.ToString();
    }

    private async Task StopAsync()
    {
        if (stopping is { } pending) { await pending; return; }
        if (session is not { } active || !active.Snapshot.Active) return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        stopping = completion.Task;
        RefreshStatus();
        try { await active.StopAsync(); }
        catch (Exception) { actionError = "Verbindung konnte nicht vollständig beendet werden. Bericht prüfen."; }
        finally { stopping = null; completion.TrySetResult(); if (!IsDisposed) RefreshStatus(); }
    }
    private async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (allowClose || !IsTestActive) return;
        e.Cancel = true;
        if (closing) return;
        closing = true; RefreshStatus();
        await StopAsync();
        if (!IsDisposed) { allowClose = true; Close(); }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Dispose(); session?.Dispose(); }
        base.Dispose(disposing);
    }
}
