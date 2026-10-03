using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AetherBoy.Runtime.Netplay;
using nanoboy.Controls;
using nanoboy.Storage;

namespace nanoboy;

public partial class frmNano
{
    // This destination comes from our selected cartridge, never from an untrusted journal.
    private string? onlineRecoveryTargetSave, onlineRecoveryRomPath;

    internal bool PrepareOnlineSaveRecovery(bool confirm = true)
    {
        if (stateOperationInProgress) { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Bitte die laufende Speicheraktion abwarten"), true); return false; }
        string? target;
        try { target = currentRomPath is null ? onlineRecoveryTargetSave : WindowsRomLibrary.Default.GetSavePath(currentRomPath); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Das ausgewählte Spiel konnte nicht geprüft werden: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message), true); return false; }
        string? selectedRom = currentRomPath ?? onlineRecoveryRomPath;
        if (target is null)
        { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Zuerst das eigene Spiel auswählen, dessen Online-Fortschritt geprüft werden soll"), true); return false; }
        if (session is not null)
        {
            if (confirm && AetherSignal.Show(this,
                global::AetherBoy.Runtime.Localization.UiText.Get("Das laufende Spiel muss zuerst sicher beendet werden. Sitzungskopien werden dabei NICHT übernommen.\n\n") +
                global::AetherBoy.Runtime.Localization.UiText.Get("Anschließend kannst du ausschließlich Kopien des gerade ausgewählten Spiels prüfen. Spiel beenden?"),
                global::AetherBoy.Runtime.Localization.UiText.Get("Online-Spielstände prüfen"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return false;
            if (!StopSession()) return false;
        }
        onlineRecoveryTargetSave = target;
        onlineRecoveryRomPath = selectedRom;
        return true;
    }

    internal bool PromoteOnlineSaveCopy(string directory, bool confirm = true, IWin32Window? confirmationOwner = null)
    {
        if (session is not null || stateOperationInProgress || onlineRecoveryTargetSave is null)
        { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Zur Übernahme zuerst das gewählte Spiel beenden und die Sitzungskopien öffnen"), true); return false; }
        string? resumeArchive = null;
        try
        {
            string target = onlineRecoveryTargetSave;
            var info = OnlineSaveRecovery.Inspect(directory);
            if (!string.Equals(Path.GetFullPath(info.OriginalSavePath), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("Diese Kopie gehört nicht zum gewählten Spielstand."));
            if (!info.CanPromote) throw new InvalidOperationException(info.Reason);
            if (confirm && AetherSignal.Show(confirmationOwner ?? this,
                global::AetherBoy.Runtime.Localization.UiText.Get("Nur eine sauber beendete Sitzung kann übernommen werden. Das beweist KEINEN erfolgreichen Pokémon-Tausch.\n\n") +
                global::AetherBoy.Runtime.Localization.UiText.Get("Habt ihr beide KOPIEN der Sitzungsspielstände nach Neustart geprüft? Die unveränderten Original-Sitzungsdateien nicht zum Prüfen starten. Der bisherige lokale Stand wird jetzt ersetzt; ") +
                global::AetherBoy.Runtime.Localization.UiText.Get("vorher wird eine wiederherstellbare Sicherung erstellt.\n\nZiel: ") + target + global::AetherBoy.Runtime.Localization.UiText.Get("\n\nDiese Kopie bewusst übernehmen?"),
                global::AetherBoy.Runtime.Localization.UiText.Get("Online-Fortschritt übernehmen"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return false;
            string backup = OnlineSaveRecovery.Promote(directory, target,
                () => resumeArchive = ArchiveOnlineResume(directory));
            pendingResume = false;
            SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Sitzungskopie übernommen · vorheriger Stand in ") + backup + global::AetherBoy.Runtime.Localization.UiText.Get(" · Alte manuelle States können den Fortschritt rückgängig machen"), false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Sitzungskopie nicht übernommen: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message) + (resumeArchive is null ? "" : global::AetherBoy.Runtime.Localization.UiText.Get(" · Fortsetzen-Sicherung: ") + resumeArchive), true); return false; }
    }

    private string? ArchiveOnlineResume(string directory)
    {
        if (onlineRecoveryRomPath is null) throw new InvalidOperationException(global::AetherBoy.Runtime.Localization.UiText.Get("Das ausgewählte Spiel muss vor der Übernahme erneut geöffnet werden."));
        string resume = WindowsSaveStateStore.Default.PathFor(onlineRecoveryRomPath, WindowsSaveStateStore.ResumeSlot);
        string[] files = new[] { resume, resume + ".preview.json" }.Where(File.Exists).ToArray();
        if (files.Length == 0) return null;
        string archive = Path.Combine(directory, "resume-before-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(archive);
        try { foreach (string file in files) File.Move(file, Path.Combine(archive, Path.GetFileName(file))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new IOException(global::AetherBoy.Runtime.Localization.UiText.Get("Fortsetzen konnte nicht vollständig archiviert werden. Erhaltene Dateien: ") + archive, ex); }
        return archive;
    }

    internal OnlineSaveRecoveryInfo[] ReadOnlineRecoveryCopies()
    {
        if (onlineRecoveryTargetSave is null) return [];
        try
        {
            string root = Path.Combine(WindowsDataPaths.Default.Development, "OnlineLink");
            return Directory.Exists(root) ? Directory.EnumerateDirectories(root).OrderByDescending(Path.GetFileName)
                .Take(256).Select(OnlineSaveRecovery.Inspect)
                .Where(info => string.Equals(info.OriginalSavePath, onlineRecoveryTargetSave, StringComparison.OrdinalIgnoreCase)).ToArray() : [];
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { SetSaveFeedback(global::AetherBoy.Runtime.Localization.UiText.Get("Sitzungskopien konnten nicht gelesen werden: ") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(ex.Message), true); return []; }
    }

    private void ShowOnlineSaveRecovery()
    {
        if (!PrepareOnlineSaveRecovery()) return;
        string target = onlineRecoveryTargetSave!;
        using var dialog = new AetherWindow
        {
            Text = global::AetherBoy.Runtime.Localization.UiText.Get("Online-Spielstände"), StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(850, 460), ShowInTaskbar = false
        };
        var heading = new Label { Location = new Point(25, 22), Size = new Size(790, 30), Text = global::AetherBoy.Runtime.Localization.UiText.Get("KOPIEN DES GEWÄHLTEN SPIELS · KEIN AUTOMATISCHER TAUSCHNACHWEIS"), ForeColor = AetherColors.Cyan };
        var destination = new Label { Location = new Point(25, 55), Size = new Size(790, 26), Text = target, AutoEllipsis = true };
        var note = new Label { Location = new Point(25, 91), Size = new Size(790, 46), Text = global::AetherBoy.Runtime.Localization.UiText.Get("Prüft separate KOPIEN der Sitzungsspielstände; diese Archivdateien unverändert lassen. Aktive/abgebrochene/veränderte Kopien sind gesperrt. Bei Übernahme wird Fortsetzen archiviert; alte manuelle States können den Import rückgängig machen.") };
        dialog.Controls.AddRange(new Control[] { heading, destination, note });
        int page = 0;
        var rows = new Panel { Location = new Point(25, 150), Size = new Size(790, 210) };
        dialog.Controls.Add(rows);
        var previous = new AetherButton { Location = new Point(25, 395), Size = new Size(135, 40), Text = global::AetherBoy.Runtime.Localization.UiText.Get("ZURÜCK") };
        var next = new AetherButton { Location = new Point(170, 395), Size = new Size(135, 40), Text = global::AetherBoy.Runtime.Localization.UiText.Get("WEITER") };
        var close = new AetherButton { Location = new Point(650, 395), Size = new Size(165, 40), Text = global::AetherBoy.Runtime.Localization.UiText.Get("SCHLIESSEN"), DialogResult = DialogResult.Cancel };
        dialog.Controls.AddRange(new Control[] { previous, next, close }); dialog.CancelButton = close;
        void RefreshCopies()
        {
            foreach (Control control in rows.Controls.Cast<Control>().ToArray()) control.Dispose();
            var copies = ReadOnlineRecoveryCopies();
            page = Math.Clamp(page, 0, Math.Max(0, (copies.Length - 1) / 3));
            previous.Enabled = page > 0; next.Enabled = (page + 1) * 3 < copies.Length;
            if (copies.Length == 0) rows.Controls.Add(new Label { Size = new Size(780, 55), Text = global::AetherBoy.Runtime.Localization.UiText.Get("Keine prüfbaren Online-Sitzungen für dieses Spiel gefunden. Ältere Sitzungen ohne Journal bleiben nur über ihren Ordner erreichbar.") });
            foreach (var (item, index) in copies.Skip(page * 3).Take(3).Select((item, index) => (item!, index)))
            {
                int y = index * 70;
                rows.Controls.Add(new Label { Location = new Point(0, y), Size = new Size(475, 55), AutoEllipsis = true,
                    Text = Path.GetFileName(item.SessionDirectory) + " · " + global::AetherBoy.Runtime.Localization.UiLabels.Recovery(item.State) + "\n" + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(item.Reason),
                    ForeColor = item.CanPromote ? AetherColors.Cyan : AetherColors.Muted });
                var folder = new AetherButton { Location = new Point(487, y), Size = new Size(125, 44), Text = global::AetherBoy.Runtime.Localization.UiText.Get("ORDNER") };
                folder.Click += (_, _) => WindowsDataPaths.OpenFolder(dialog, item.SessionDirectory);
                var adopt = new AetherButton { Location = new Point(623, y), Size = new Size(165, 44), Text = global::AetherBoy.Runtime.Localization.UiText.Get("ÜBERNEHMEN"), Enabled = item.CanPromote };
                adopt.Click += (_, _) => { PromoteOnlineSaveCopy(item.SessionDirectory, confirmationOwner: dialog); RefreshCopies(); };
                rows.Controls.Add(folder); rows.Controls.Add(adopt);
            }
        }
        previous.Click += (_, _) => { page--; RefreshCopies(); }; next.Click += (_, _) => { page++; RefreshCopies(); };
        AetherDialog.Apply(dialog, global::AetherBoy.Runtime.Localization.UiText.Get("ONLINE ARCHIVE // SAVE SAFETY"), global::AetherBoy.Runtime.Localization.UiText.Get("Bewusste Übernahme · Original vorher sichern · Ergebnis selbst prüfen"));
        RefreshCopies(); dialog.ShowDialog(this);
    }
}
