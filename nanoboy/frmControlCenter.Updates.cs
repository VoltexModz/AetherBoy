using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using AetherBoy.Runtime;
using nanoboy.Controls;
using nanoboy.Storage;

namespace nanoboy;

internal sealed partial class frmControlCenter
{
    private AetherButton updateCheck = null!, updateDownload = null!, updateCancel = null!, updateFolder = null!, updateStartup = null!;
    private Label updateStatus = null!, updatePackage = null!;

    private void BuildUpdatesPage()
    {
        var page = NewSection("updates", "Updates", global::AetherBoy.Runtime.Localization.UiText.Get("Veröffentlichte AetherBoy-Pakete auf GitHub prüfen und herunterladen."));
        AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Zurück zur Bedienung"), 0, 75, 300, () => ShowPage("desktop"));
        string channel = bridge.Updates?.IncludePrereleases == true ? global::AetherBoy.Runtime.Localization.UiText.Get("Vorabversionen eingeschlossen") : global::AetherBoy.Runtime.Localization.UiText.Get("Nur stabile Releases");
        page.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Format("Installiert: {0} · {1}\r\nGit-Commits ohne neues Release werden nicht als Update angeboten.", ProductInfo.Version, channel), 0, 143, 780, 66));
        updateStartup = AddActionButton(page, "", 0, 221, 480, () =>
        { bridge.Settings.UpdateCheckOnStartup = !bridge.Settings.UpdateCheckOnStartup; RefreshUpdates(); });
        updateStartup.Name = "updateStartup";
        page.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Prüfen kontaktiert GitHub: Dabei ist deine IP-Adresse sichtbar. AetherBoy sendet keine ROMs, Spielstände oder Kontodaten. Herunterladen startet nur auf Klick."), 0, 279, 780, 68));
        updateCheck = AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Jetzt prüfen"), 0, 367, 245, () => { if (bridge.Updates is { } service) _ = service.CheckAsync(); RefreshUpdates(); });
        updateCheck.Name = "updateCheck";
        updateDownload = AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Paket herunterladen"), 266, 367, 270, () => { if (bridge.Updates is { } service) _ = service.DownloadAsync(); RefreshUpdates(); });
        updateDownload.Name = "updateDownload";
        updateCancel = AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Abbrechen"), 556, 367, 224, () => bridge.Updates?.Cancel());
        updateCancel.Name = "updateCancel";
        updateStatus = CreateSmallLabel("", 0, 430, 780, 68); updateStatus.Name = "updateStatus"; page.Controls.Add(updateStatus);
        updatePackage = CreateSmallLabel("", 0, 501, 780, 48); updatePackage.Name = "updatePackage"; page.Controls.Add(updatePackage);
        updateFolder = AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Downloadordner öffnen"), 0, 566, 380, () =>
        {
            if (bridge.Updates is { } service)
                WindowsDataPaths.OpenFolder(this, service.Snapshot.DownloadPath is { } path ? Path.GetDirectoryName(path)! : service.DownloadDirectory);
        });
        updateFolder.Name = "updateFolder";
        AddActionButton(page, global::AetherBoy.Runtime.Localization.UiText.Get("Releases auf GitHub öffnen"), 400, 566, 380, () =>
        {
            try { Process.Start(new ProcessStartInfo(ReleaseUpdateService.ReleasesPage) { UseShellExecute = true }); }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException)
            { AetherSignal.Show(this, global::AetherBoy.Runtime.Localization.UiText.Get("Der Browser konnte nicht geöffnet werden. Die Releases findest du im AetherBoy-Repository auf GitHub."), global::AetherBoy.Runtime.Localization.UiText.Get("Browser nicht verfügbar"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        });
        page.Controls.Add(CreateSmallLabel(global::AetherBoy.Runtime.Localization.UiText.Get("Die SHA-256-Prüfung vergleicht das Paket mit GitHubs Prüfsumme; sie ist keine Entwicklersignatur. Noch keine automatische Installation: in einen neuen Ordner entpacken. Portable Spieldaten und den alten Ordner behalten."), 0, 631, 780, 80));
        RefreshUpdates();
    }

    private void RefreshUpdates()
    {
        if (updateCheck is null) return;
        var service = bridge.Updates;
        var status = service?.Snapshot ?? new();
        updateCheck.Enabled = service is not null && !status.Busy;
        updateDownload.Enabled = status.State == ReleaseUpdateState.Available;
        updateCancel.Enabled = status.Busy;
        updateFolder.Enabled = service is not null;
        updateStartup.Text = bridge.Settings.UpdateCheckOnStartup ? global::AetherBoy.Runtime.Localization.UiText.Get("Beim Programmstart prüfen: an") : global::AetherBoy.Runtime.Localization.UiText.Get("Beim Programmstart prüfen: aus");
        updateStartup.Selected = bridge.Settings.UpdateCheckOnStartup;
        updateStatus.Text = DescribeUpdate(status);
        updatePackage.Text = status.PackageName is { } name ? $"{name}\r\n{status.PackageSize / 1048576d:0.0} MiB" : "";
    }

    internal static string DescribeUpdate(ReleaseUpdateSnapshot status) => status.State switch
    {
        ReleaseUpdateState.Checking => global::AetherBoy.Runtime.Localization.UiText.Get("Veröffentlichte Releases werden geprüft …"),
        ReleaseUpdateState.NoReleases => global::AetherBoy.Runtime.Localization.UiText.Get("Für diesen Kanal ist noch kein auswertbares Release veröffentlicht. Das sagt nichts über neue Git-Commits aus."),
        ReleaseUpdateState.NoNewerRelease => global::AetherBoy.Runtime.Localization.UiText.Get("Kein Release mit höherer Versionsnummer gefunden. Lokale Änderungen und Git-Commits werden nicht verglichen."),
        ReleaseUpdateState.NoPackage => global::AetherBoy.Runtime.Localization.UiText.Format("Release {0} ist neuer. Dafür fehlt ein eindeutiges Paket für dieses System mit gültiger SHA-256-Prüfsumme.", status.Version),
        ReleaseUpdateState.Available => global::AetherBoy.Runtime.Localization.UiText.Format("Version {0} ist verfügbar. Du kannst das Paket jetzt herunterladen.", status.Version),
        ReleaseUpdateState.Downloading => global::AetherBoy.Runtime.Localization.UiText.Format("Paket wird heruntergeladen: {0:0.0} von {1:0.0} MiB …", status.ReceivedBytes / 1048576d, status.PackageSize / 1048576d),
        ReleaseUpdateState.Downloaded => global::AetherBoy.Runtime.Localization.UiText.Get("Download vollständig. Größe und SHA-256 stimmen mit den GitHub-Angaben überein. Noch nicht installiert."),
        ReleaseUpdateState.Cancelled => global::AetherBoy.Runtime.Localization.UiText.Get("Abgebrochen. Es wurde nichts installiert. Zum Wiederholen erneut prüfen."),
        ReleaseUpdateState.Failed => status.Error switch
        {
            ReleaseUpdateError.RateLimit => global::AetherBoy.Runtime.Localization.UiText.Get("GitHub begrenzt die Anfragen. Bitte später erneut prüfen."),
            ReleaseUpdateError.Timeout => global::AetherBoy.Runtime.Localization.UiText.Get("Die Anfrage dauerte zu lange. Prüfe die Verbindung und versuche es erneut."),
            ReleaseUpdateError.Integrity => global::AetherBoy.Runtime.Localization.UiText.Get("Größe oder Prüfsumme stimmt nicht. Das unvollständige Paket wird nicht angeboten. Erneut prüfen oder dem Team melden."),
            ReleaseUpdateError.UnsafeRedirect => global::AetherBoy.Runtime.Localization.UiText.Get("GitHub verweist auf ein nicht freigegebenes Downloadziel. Der Download wurde gestoppt. Bitte dem Team melden."),
            ReleaseUpdateError.UnsupportedRuntime => global::AetherBoy.Runtime.Localization.UiText.Get("Für diese Betriebssystem- und Prozessarchitektur gibt es noch keinen Update-Pakettyp."),
            ReleaseUpdateError.UnknownVersion => global::AetherBoy.Runtime.Localization.UiText.Get("Die installierte Versionsnummer lässt sich nicht vergleichen. Bitte dem Team melden."),
            ReleaseUpdateError.InvalidCatalog or ReleaseUpdateError.CatalogLimit => global::AetherBoy.Runtime.Localization.UiText.Get("Die Release-Liste konnte nicht sicher ausgewertet werden. Bitte die Releases auf GitHub prüfen."),
            ReleaseUpdateError.Disk => global::AetherBoy.Runtime.Localization.UiText.Get("Der Download oder das Speichern ist fehlgeschlagen. Prüfe Verbindung, freien Speicher und Schreibrechte."),
            _ => global::AetherBoy.Runtime.Localization.UiText.Get("GitHub konnte nicht gelesen werden. Prüfe die Verbindung und versuche es erneut. Der Versionsstand ist unbekannt.")
        },
        _ => global::AetherBoy.Runtime.Localization.UiText.Get("Noch nicht geprüft. Wähle Jetzt prüfen.")
    };
}
