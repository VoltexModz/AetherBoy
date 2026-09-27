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
        var dialog = new Form { Text = "Online Link · Räume", ClientSize = new Size(700, 530), StartPosition = FormStartPosition.CenterParent };
        onlineRoomDialog = dialog;
        Label Label(string text, int x, int y, int width, int height = 28)
        {
            var label = new Label { Text = text, Bounds = new Rectangle(x, y, width, height), AutoSize = false };
            dialog.Controls.Add(label); return label;
        }
        Button Button(string text, int x, int y, int width, Action action)
        {
            var button = new Button { Text = text, Bounds = new Rectangle(x, y, width, 42) };
            button.Click += (_, _) => action(); dialog.Controls.Add(button); return button;
        }
        Label("Ein Raum. Zwei Spieler. Direkt im Emulator spielen.", 24, 18, 650);
        var setup = new Panel { Bounds = new Rectangle(24, 65, 650, 220), Visible = false };
        dialog.Controls.Add(setup);
        // Control.Visible is false while the parent form is hidden, even for a requested
        // visible panel. Keep the page selection independent of the form's lifetime.
        bool showingSetup = false;
        void ShowSetup(bool value) { showingSetup = value; setup.Visible = value; }
        var url = new TextBox { Text = roomSettings.ServerUrl, Bounds = new Rectangle(0, 30, 645, 30), MaxLength = 256, AccessibleName = "Raumserver HTTPS-Adresse" };
        var key = new TextBox { Text = roomSettings.AccessKey, Bounds = new Rectangle(0, 104, 645, 30), MaxLength = 256, UseSystemPasswordChar = true, AccessibleName = "Server-Zugangsschlüssel" };
        setup.Controls.Add(new Label { Text = "Raumserver · HTTPS-Adresse", Bounds = new Rectangle(0, 4, 640, 24) });
        setup.Controls.Add(new Label { Text = "Server-Zugangsschlüssel", Bounds = new Rectangle(0, 78, 640, 24) });
        setup.Controls.Add(url); setup.Controls.Add(key);
        var save = new Button { Text = "Server speichern", Bounds = new Rectangle(0, 154, 200, 42) }; setup.Controls.Add(save);
        var message = Label(currentRomPath is null ? "Zuerst dein eigenes unterstütztes Spiel öffnen." : "Server einmal speichern, dann einen Raum erstellen oder einen Code eingeben.", 24, 310, 650, 64);
        var codeCaption = Label("RAUMCODE", 24, 80, 645);
        var code = new TextBox { Bounds = new Rectangle(24, 116, 400, 42), MaxLength = 14, Font = new Font("Segoe UI", 20), AccessibleName = "Raumcode", PlaceholderText = "ABCDE-FGHJK" };
        dialog.Controls.Add(code);
        OnlineRoomTransport? observed = onlineRoomTransport;
        var consent = new CheckBox { Text = "Mit geschützter Spielstandkopie starten (experimentell)", Bounds = new Rectangle(24, 250, 650, 38) };
        dialog.Controls.Add(consent);
        void Start(bool create)
        {
            try
            {
                roomSettings.Validate(); roomCodeInput = create ? "" : OnlineRoomSettings.NormalizeCode(code.Text);
                if (!consent.Checked) { message.Text = "Bitte die geschützte Spielstandkopie bestätigen."; return; }
                startNativeRoom = true;
                if (StartOnlineLink(create, confirm: false)) observed = onlineRoomTransport;
                else message.Text = "Start nicht möglich. Unterstütztes Spiel öffnen und Profil prüfen (Tools → Online Link).";
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { message.Text = ex.Message; }
            finally { startNativeRoom = false; }
        }
        var create = Button("Raum erstellen", 24, 190, 300, () => Start(true));
        var join = Button("Raum beitreten", 350, 190, 324, () => Start(false));
        var copy = Button("Code kopieren", 446, 116, 228, () =>
        {
            try { if (observed?.RoomCode.Length == 10) Clipboard.SetText(observed.DisplayCode); }
            catch (System.Runtime.InteropServices.ExternalException) { message.Text = "Zwischenablage ist gerade nicht verfügbar."; }
        });
        var server = Button("Server einstellen", 24, 393, 200, () => { if (!IsOnlineLink) ShowSetup(!showingSetup); });
        var stop = Button("Verbindung beenden", 244, 393, 210, () => { if (IsOnlineLink) StopSession(); });
        var back = Button("Zum Spiel", 474, 393, 200, () => dialog.Close());
        Label("Originalspielstände bleiben erhalten. Sitzungskopien danach unter\nTools → Online Link → Sitzungskopien prüfen / übernehmen kontrollieren.", 24, 458, 650, 54);
        save.Click += (_, _) =>
        {
            try
            {
                var candidate = new OnlineRoomSettings(url.Text.Trim(), key.Text.Trim());
                candidate.Save(RoomSettingsPath); roomSettings = candidate; ShowSetup(false);
                message.Text = "Server gespeichert. TURN-Einstellungen kommen automatisch vom Server.";
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { message.Text = ex.Message; }
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
            if (active && observed is not null) { code.Text = observed.DisplayCode; message.Text = observed.Status; }
            else if (observed is not null) message.Text = observed.Fault?.Message ?? "Online-Sitzung beendet. Deine Spielstandkopie bleibt erhalten.";
        }
        setup.VisibleChanged += (_, _) => Refresh();
        timer.Tick += (_, _) => Refresh();
        dialog.FormClosed += (_, _) => { timer.Dispose(); onlineRoomDialog = null; };
        AetherDialog.Apply(dialog, "ONLINE LINK // ROOMS", "Kurzen Code teilen · Browserfrei verbinden · Geschützte Spielstandkopie");
        Refresh(); timer.Start(); dialog.Show(this);
        if (!host && !showingSetup) code.Focus();
    }
}
