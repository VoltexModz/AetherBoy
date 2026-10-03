using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AetherBoy.Runtime.Netplay;
using nanoboy.Controls;
using nanoboy.Storage;

namespace nanoboy;

public partial class frmNano
{
    private OnlineRoomTransport? onlineRoomTransport;
    private OnlineRoomSettings roomSettings = new();
    private string roomCodeInput = "";
    private bool startNativeRoom;
    private Form? onlineRoomDialog;
    private string RoomSettingsPath => Path.Combine(WindowsDataPaths.Default.Settings, "online-room.json");

    private void ShowOnlineRoomDialog(bool host)
    {
        if (onlineProbeDialog is { IsDisposed: false })
        {
            if (IsConnectionTestActive) { onlineProbeDialog.BringToFront(); return; }
            onlineProbeDialog.Close();
        }
        if (onlineRoomDialog is { IsDisposed: false }) { onlineRoomDialog.BringToFront(); return; }
        roomSettings = OnlineRoomSettings.Load(RoomSettingsPath);
        var dialog = new AetherWindow { Text = global::AetherBoy.Runtime.Localization.UiText.Get("Online Link · Räume"), ClientSize = new Size(700, 530), StartPosition = FormStartPosition.CenterParent };
        onlineRoomDialog = dialog;
        Label Label(string text, int x, int y, int width, int height = 28)
        {
            var label = new Label { Text = text, Bounds = new Rectangle(x, y, width, height), AutoSize = false };
            dialog.Controls.Add(label); return label;
        }
        Button Button(string text, int x, int y, int width, Action action)
        {
            var button = new AetherButton { Kind = AetherButtonKind.Secondary, Text = text, Bounds = new Rectangle(x, y, width, 42) };
            button.Click += (_, _) => action(); dialog.Controls.Add(button); return button;
        }
        Label(global::AetherBoy.Runtime.Localization.UiText.Get("Mit Raumcode verbinden und im Emulator spielen."), 24, 18, 650);
        var setup = new Panel { Bounds = new Rectangle(24, 65, 650, 220), Visible = false };
        dialog.Controls.Add(setup);
        // Control.Visible is false while the parent form is hidden, even for a requested
        // visible panel. Keep the page selection independent of the form's lifetime.
        bool showingSetup = false;
        void ShowSetup(bool value) { showingSetup = value; setup.Visible = value; }
        var url = new nanoboy.Controls.AetherTextBox { Text = roomSettings.ServerUrl, Bounds = new Rectangle(0, 30, 645, 30), MaxLength = 256, AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Raumserver HTTPS-Adresse") };
        var key = new nanoboy.Controls.AetherTextBox { Text = roomSettings.AccessKey, Bounds = new Rectangle(0, 104, 645, 30), MaxLength = 256, UseSystemPasswordChar = true, AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Server-Zugangsschlüssel") };
        setup.Controls.Add(new Label { Text = global::AetherBoy.Runtime.Localization.UiText.Get("Raumserver · HTTPS-Adresse"), Bounds = new Rectangle(0, 4, 640, 24) });
        setup.Controls.Add(new Label { Text = global::AetherBoy.Runtime.Localization.UiText.Get("Server-Zugangsschlüssel"), Bounds = new Rectangle(0, 78, 640, 24) });
        setup.Controls.Add(url); setup.Controls.Add(key);
        var save = new AetherButton { Kind = AetherButtonKind.Secondary, Text = global::AetherBoy.Runtime.Localization.UiText.Get("Server speichern"), Bounds = new Rectangle(0, 154, 200, 42) }; setup.Controls.Add(save);
        var message = Label(currentRomPath is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Zuerst dein eigenes unterstütztes Spiel öffnen.") : global::AetherBoy.Runtime.Localization.UiText.Get("Server speichern. Danach kannst du einen Raum erstellen oder beitreten."), 24, 310, 650, 64);
        var codeCaption = Label(global::AetherBoy.Runtime.Localization.UiText.Get("RAUMCODE"), 24, 80, 645);
        var code = new nanoboy.Controls.AetherTextBox { Bounds = new Rectangle(24, 116, 400, 42), MaxLength = 14, Font = new Font("Segoe UI", 20), AccessibleName = global::AetherBoy.Runtime.Localization.UiText.Get("Raumcode"), PlaceholderText = "ABCDE-FGHJK" };
        dialog.Controls.Add(code);
        OnlineRoomTransport? observed = onlineRoomTransport;
        var consent = new AetherCheckBox { Text = global::AetherBoy.Runtime.Localization.UiText.Get("Online Link mit einer Kopie meines Spielstands testen (experimentell)"), Bounds = new Rectangle(24, 250, 650, 38) };
        dialog.Controls.Add(consent);
        void Start(bool create)
        {
            try
            {
                roomSettings.Validate(); roomCodeInput = create ? "" : OnlineRoomSettings.NormalizeCode(code.Text);
                if (!consent.Checked) { message.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Bitte zuerst die Spielstandkopie bestätigen."); return; }
                startNativeRoom = true;
                if (StartOnlineLink(create, confirm: false)) observed = onlineRoomTransport;
                else message.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Start nicht möglich. Unterstütztes Spiel öffnen und Profil prüfen (Tools → Online Link).");
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { message.Text = global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message); }
            finally { startNativeRoom = false; }
        }
        var create = Button(global::AetherBoy.Runtime.Localization.UiText.Get("Raum erstellen"), 24, 190, 300, () => Start(true));
        var join = Button(global::AetherBoy.Runtime.Localization.UiText.Get("Raum beitreten"), 350, 190, 324, () => Start(false));
        var copy = Button(global::AetherBoy.Runtime.Localization.UiText.Get("Code kopieren"), 446, 116, 228, () =>
        {
            try { if (observed?.RoomCode.Length == 10) Clipboard.SetText(observed.DisplayCode); }
            catch (System.Runtime.InteropServices.ExternalException) { message.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Zwischenablage ist gerade nicht verfügbar."); }
        });
        var server = Button(global::AetherBoy.Runtime.Localization.UiText.Get("Server einstellen"), 24, 393, 200, () => { if (!IsOnlineLink) ShowSetup(!showingSetup); });
        var stop = Button(global::AetherBoy.Runtime.Localization.UiText.Get("Verbindung beenden"), 244, 393, 210, () => { if (IsOnlineLink) StopSession(); });
        var back = Button(global::AetherBoy.Runtime.Localization.UiText.Get("Zum Spiel"), 474, 393, 200, () => dialog.Close());
        Label(global::AetherBoy.Runtime.Localization.UiText.Get("Dein Originalspielstand bleibt erhalten. Nach der Sitzung kannst du die Kopie unter\nTools → Online Link → Sitzungskopien prüfen / übernehmen ansehen."), 24, 458, 650, 54);
        save.Click += (_, _) =>
        {
            try
            {
                var candidate = new OnlineRoomSettings(url.Text.Trim(), key.Text.Trim());
                candidate.Save(RoomSettingsPath); roomSettings = candidate; ShowSetup(false);
                message.Text = global::AetherBoy.Runtime.Localization.UiText.Get("Server gespeichert. Du kannst jetzt einen Raum erstellen oder beitreten.");
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { message.Text = global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message); }
        };
        bool Configured() { try { roomSettings.Validate(); return true; } catch (ArgumentException) { return false; } }
        ShowSetup(!Configured() && !IsOnlineLink);
        var timer = new System.Windows.Forms.Timer { Interval = 200 };
        void Refresh()
        {
            bool active = onlineRoomTransport is not null;
            foreach (Control control in new Control[] { codeCaption, code, consent, create, join, copy }) control.Visible = !showingSetup;
            code.ReadOnly = active; consent.Enabled = !active;
            create.Enabled = join.Enabled = !active && !IsOnlineLink && !IsConnectionTestActive && consent.Checked && Configured() && currentRomPath is not null;
            copy.Enabled = active && observed?.RoomCode.Length == 10;
            stop.Enabled = IsOnlineLink; server.Enabled = !IsOnlineLink;
            if (active && observed is not null) { code.Text = observed.DisplayCode; message.Text = RoomStatus(observed); }
            else if (observed is not null) message.Text = observed.Fault is null ? global::AetherBoy.Runtime.Localization.UiText.Get("Online-Sitzung beendet. Deine Spielstandkopie bleibt erhalten.") : RoomStatus(observed);
        }
        setup.VisibleChanged += (_, _) => Refresh();
        timer.Tick += (_, _) => Refresh();
        dialog.FormClosed += (_, _) => { timer.Dispose(); onlineRoomDialog = null; };
        AetherDialog.Apply(dialog, global::AetherBoy.Runtime.Localization.UiText.Get("ONLINE LINK · RÄUME"), global::AetherBoy.Runtime.Localization.UiText.Get("Erstelle einen Raum oder tritt mit einem Code bei."));
        Refresh(); timer.Start(); dialog.Show(this);
        if (!host && !showingSetup) code.Focus();
    }

    private static string RoomStatus(OnlineRoomTransport room)
    {
        if (room.Fault is OnlineRoomRequestException request)
            return request.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => global::AetherBoy.Runtime.Localization.UiText.Get("Der Server-Zugangsschlüssel stimmt nicht. Prüfe die Servereinstellungen."),
                System.Net.HttpStatusCode.NotFound => global::AetherBoy.Runtime.Localization.UiText.Get("Der Raum wurde nicht gefunden oder ist abgelaufen. Prüfe den Code."),
                System.Net.HttpStatusCode.Conflict => global::AetherBoy.Runtime.Localization.UiText.Get("Der Raum ist voll oder nutzt ein anderes Spielsystem."),
                System.Net.HttpStatusCode.TooManyRequests => global::AetherBoy.Runtime.Localization.UiText.Get("Zu viele Verbindungsversuche. Warte eine Minute und versuche es erneut."),
                _ => global::AetherBoy.Runtime.Localization.UiText.Get("Der Raumserver hat die Anfrage abgelehnt. Prüfe die Servereinstellungen.")
            };
        if (room.Fault is not null)
            return global::AetherBoy.Runtime.Localization.UiText.Get("Verbindung fehlgeschlagen. Prüfe die Servereinstellungen und starte einen neuen Raum.");
        OnlineProbeConnectionState state = room.ConnectionState;
        if (state.Connected) return global::AetherBoy.Runtime.Localization.UiText.Get("Verbunden. Du kannst zum Spiel zurückkehren.");
        return state.Stage switch
        {
            "room-admission" => global::AetherBoy.Runtime.Localization.UiText.Get("Verbindung zum Raumserver wird hergestellt …"),
            "relay-preparation" or "ice-gathering" or "publish-description" => global::AetherBoy.Runtime.Localization.UiText.Get("Verbindung wird vorbereitet …"),
            "remote-offer" => global::AetherBoy.Runtime.Localization.UiText.Get("Warte auf den Spieler, der den Raum erstellt hat …"),
            "remote-answer" => state.PeerPresent ? global::AetherBoy.Runtime.Localization.UiText.Get("Mitspieler ist beigetreten. Verbindung wird aufgebaut …") : global::AetherBoy.Runtime.Localization.UiText.Get("Raum bereit. Teile den Code mit deinem Mitspieler."),
            "data-channel-open" => global::AetherBoy.Runtime.Localization.UiText.Get("Verbindung zum Mitspieler wird aufgebaut …"),
            _ => global::AetherBoy.Runtime.Localization.UiText.Get("Verbindung wird aufgebaut …")
        };
    }
}
