using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Localization;
using nanoboy.Controls;

namespace nanoboy;

internal sealed partial class frmControlCenter
{
    private AetherButton eReaderImport = null!, eReaderClear = null!;
    private Label eReaderStatus = null!, eReaderFeedback = null!;
    private bool eReaderBusy;

    private void BuildEReaderPage()
    {
        var page = NewSection("ereader", "e-Reader", UiText.Get("Digitale Karten mit einer e-Reader-ROM lesen."));
        AddActionButton(page, UiText.Get("Zurück zu den Werkzeugen"), 0, 82, 380, () => ShowPage("tools"));
        AddActionButton(page, UiText.Get("Kartenbibliothek"), 400, 82, 380, () => { ReloadEReaderLibrary(); ShowPage("ereader-library"); });
        page.Controls.Add(CreateSmallLabel(UiText.Get("Öffne zuerst deine e-Reader-ROM (USA) oder Card e-Reader/Card e-Reader+ (Japan). Eine normale GBA-Spiel-ROM reicht nicht aus."), 0, 150, 780, 70));
        eReaderStatus = CreateSmallLabel("", 0, 230, 780, 65); eReaderStatus.Name = "eReaderStatus"; page.Controls.Add(eReaderStatus);
        eReaderImport = AddActionButton(page, UiText.Get("Karte aus Datei laden"), 0, 310, 380, () => { }); eReaderImport.Name = "eReaderImport";
        eReaderImport.Click += async (_, _) =>
        {
            if (eReaderBusy) return;
            using var picker = new AetherFileDialog { Title = UiText.Get("e-Reader-Karte auswählen"), Filter = "e-Reader|*.raw;*.bin|*|*.*", CheckFileExists = true };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            await ImportEReaderCard(picker.FileName);
        };
        eReaderClear = AddActionButton(page, UiText.Get("Scans verwerfen"), 400, 310, 380, () => { }); eReaderClear.Name = "eReaderClear";
        eReaderClear.Click += async (_, _) => await RunEReaderAction(async () =>
        {
            await (bridge.ClearEReaderCards ?? throw new NotSupportedException())();
            if (!IsDisposed) eReaderFeedback.Text = UiText.Get("Kartenwarteschlange und laufender Scan verworfen. Gespeicherte Programme bleiben erhalten.");
        });
        eReaderFeedback = CreateSmallLabel("", 0, 383, 780, 100); eReaderFeedback.Name = "eReaderFeedback"; page.Controls.Add(eReaderFeedback);
        page.Controls.Add(CreateSmallLabel(UiText.Get("Unterstützt: RAW-Streifen mit 1872/2912 Byte und gepackte Punktbilder mit 3520/5456 Byte. Keine Fotos, PNGs oder dekodierten BIN-Dateien. Bis zu 16 Karten warten in Einfügereihenfolge."), 0, 500, 780, 80));
        page.Controls.Add(CreateSmallLabel(UiText.Get("Standalone-Programme wurden mit vollständigen Kartensätzen getestet. Übertragung an ein zweites GBA-Spiel und Online Link sind noch nicht bestätigt."), 0, 595, 780, 100));
        BuildEReaderLibraryPage();
        RefreshEReader();
    }

    private Task ImportEReaderCard(string path) => RunEReaderAction(async () =>
    {
        byte[] card = EReaderInput.ReadFile(path);
        await (bridge.QueueEReaderCard ?? throw new NotSupportedException())(card);
        if (!IsDisposed) eReaderFeedback.Text = UiText.Get("Karte vorgemerkt. Schließe das Menü. Wähle bei Bedarf Scan Card; ein bereits wartender Scan liest sie automatisch.");
    });

    private async Task RunEReaderAction(Func<Task> action)
    {
        if (eReaderBusy) return;
        eReaderBusy = true; RefreshEReader();
        try { await action(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or OperationCanceledException)
        {
            if (!IsDisposed) eReaderFeedback.Text = UiText.Get("Karte nicht übernommen. Prüfe Dateiformat, freie Warteschlangenplätze und die geöffnete e-Reader-ROM.") + "\n" + UiText.TechnicalDetails(ex.Message);
        }
        finally { eReaderBusy = false; if (!IsDisposed) RefreshEReader(); }
    }

    private void RefreshEReader()
    {
        if (eReaderImport is null) return;
        var reader = bridge.SnapshotProvider()?.EReader;
        eReaderImport.Enabled = !eReaderBusy && reader is { QueuedCards: < 16 } && bridge.QueueEReaderCard is not null;
        eReaderClear.Enabled = !eReaderBusy && reader is not null && (reader.HasCard || reader.QueuedCards > 0) && bridge.ClearEReaderCards is not null;
        eReaderStatus.Text = reader is null ? UiText.Get("Keine e-Reader-ROM im Einzelmodus geöffnet.")
            : UiText.Format("Wartende Karten: {0}/16. Begonnene Scans: {1}. Das bestätigt noch keine vom Spiel akzeptierte Karte.", reader.QueuedCards, reader.CardsStarted);
    }
}
