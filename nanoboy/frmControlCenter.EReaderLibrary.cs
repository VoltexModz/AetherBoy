using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Localization;
using nanoboy.Controls;
using nanoboy.Storage;

namespace nanoboy;

internal sealed partial class frmControlCenter
{
    private EReaderLibrary CardLibrary => new(WindowsDataPaths.Default.EReader);
    private AetherSelect eReaderSets = null!;
    private AetherTextBox eReaderSetTitle = null!;
    private Label eReaderSetInfo = null!, eReaderLibraryFeedback = null!;
    private PictureBox eReaderSetPreview = null!;
    private AetherButton eReaderSetStart = null!, eReaderSetResume = null!, eReaderSetSave = null!, eReaderSetAppend = null!;
    private EReaderLibraryEntry? SelectedEReaderSet => eReaderSets.SelectedItem as EReaderLibraryEntry;

    private void BuildEReaderLibraryPage()
    {
        var page = NewSection("ereader-library", UiText.Get("Kartenbibliothek"), UiText.Get("Ein Speicherplatz pro Kartensatz. Deine e-Reader-ROM wird weiterhin benötigt."));
        AddActionButton(page, UiText.Get("Zurück zum Scanner"), 0, 82, 380, () => ShowPage("ereader"));
        AddActionButton(page, UiText.Get("e-Reader-ROM auswählen"), 400, 82, 380, () => PickEReaderLibraryFile(firmware: true));
        eReaderSets = new AetherSelect { Name = "eReaderSets", Bounds = new Rectangle(0, 150, 780, 38), AccessibleName = UiText.Get("Kartensatz") };
        page.Controls.Add(eReaderSets);
        eReaderSets.SelectedIndexChanged += (_, _) => RefreshEReaderSet();
        AddActionButton(page, UiText.Get("Kartensatz importieren"), 0, 205, 380, () => PickEReaderLibraryFile());
        eReaderSetAppend = AddActionButton(page, UiText.Get("Streifen ergänzen"), 400, 205, 380, () => PickEReaderLibraryFile(append: true));
        eReaderSetTitle = new AetherTextBox { Name = "eReaderSetTitle", Bounds = new Rectangle(0, 261, 540, 38), MaxLength = 120, AccessibleName = UiText.Get("Titel des Kartensatzes") };
        page.Controls.Add(eReaderSetTitle);
        AddActionButton(page, UiText.Get("Titel speichern"), 560, 261, 220, () => _ = CardLibraryAction(() =>
        {
            if (SelectedEReaderSet is not { } entry) return Task.CompletedTask;
            CardLibrary.Rename(entry.Id, eReaderSetTitle.Text); ReloadEReaderLibrary(entry.Id); return Task.CompletedTask;
        }));
        eReaderSetPreview = new PictureBox { Name = "eReaderSetPreview", Bounds = new Rectangle(0, 325, 240, 160), SizeMode = PictureBoxSizeMode.Zoom, BackColor = AetherColors.Void };
        page.Controls.Add(eReaderSetPreview);
        page.Disposed += (_, _) => eReaderSetPreview.Image?.Dispose();
        eReaderSetInfo = CreateSmallLabel("", 260, 323, 520, 170); page.Controls.Add(eReaderSetInfo);
        eReaderSetStart = AddActionButton(page, UiText.Get("Kartensatz öffnen"), 0, 507, 250, () => _ = LaunchEReaderSet(false));
        eReaderSetResume = AddActionButton(page, UiText.Get("Fortsetzen"), 265, 507, 250, () => _ = LaunchEReaderSet(true));
        eReaderSetSave = AddActionButton(page, UiText.Get("Stand und Bild sichern"), 530, 507, 250, () => _ = CardLibraryAction(async () =>
        {
            if (!SelectedEReaderSetIsActive()) return;
            await (bridge.SaveEReaderResume ?? throw new NotSupportedException())(); RefreshEReaderSet();
            eReaderLibraryFeedback.Text = bridge.SaveFeedbackProvider();
        }));
        eReaderLibraryFeedback = CreateSmallLabel("", 0, 570, 780, 90); page.Controls.Add(eReaderLibraryFeedback);
        page.Controls.Add(CreateSmallLabel(UiText.Get("Importiere nur die Streifen eines Programms (RAW/BIN oder ZIP, bis zu 16). Ergänze weitere Karten im selben Eintrag. Die Vollständigkeit und Region prüft das Spiel. Beim ersten Start Scan Card wählen."), 0, 675, 780, 100));
        ReloadEReaderLibrary();
    }

    private void PickEReaderLibraryFile(bool firmware = false, bool append = false)
    {
        string? id = append ? SelectedEReaderSet?.Id : null;
        if (append && id is null) return;
        using var picker = new AetherFileDialog { Title = UiText.Get(firmware ? "e-Reader-ROM auswählen" : "e-Reader-Karte auswählen"),
            Filter = firmware ? "GBA|*.gba" : "e-Reader|*.raw;*.bin;*.zip", CheckFileExists = true };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        _ = CardLibraryAction(async () =>
        {
            string path = picker.FileName;
            string? imported = await Task.Run(() =>
            {
                if (firmware) { CardLibrary.SetFirmware(path); return null; }
                return CardLibrary.Import(path, id).Id;
            });
            ReloadEReaderLibrary(imported ?? id);
            eReaderLibraryFeedback.Text = UiText.Get("Lokale Kopie übernommen. Originaldateien bleiben unverändert.");
        });
    }

    private void ReloadEReaderLibrary(string? selectId = null)
    {
        if (eReaderSets is null) return;
        try
        {
            selectId ??= SelectedEReaderSet?.Id;
            var entries = CardLibrary.ReadEntries();
            eReaderSets.Items.Clear(); foreach (var entry in entries) eReaderSets.Items.Add(entry);
            eReaderSets.SelectedIndex = entries.Count == 0 ? -1 : Math.Max(0, entries.ToList().FindIndex(e => e.Id == selectId));
            RefreshEReaderSet();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { ShowEReaderLibraryError(ex); }
    }

    private bool SelectedEReaderSetIsActive() => SelectedEReaderSet?.LaunchId is { } id && bridge.RomPathProvider() is { } path &&
        CardLibrary.IdentifyLaunch(path) == id && bridge.SnapshotProvider()?.EReader is not null;

    private void RefreshEReaderSet()
    {
        if (eReaderSetTitle is null) return;
        var entry = SelectedEReaderSet;
        eReaderSetTitle.Text = entry?.Title ?? "";
        eReaderSetAppend.Enabled = entry is not null;
        eReaderSetStart.Enabled = entry is not null && CardLibrary.HasFirmware && bridge.LaunchEReaderSet is not null;
        string? launch = entry?.LaunchId is { } id ? CardLibrary.LaunchPath(id) : null;
        var preview = launch is null ? null : new WindowsSaveStateStore(WindowsDataPaths.Default).Inspect(launch, 0);
        eReaderSetResume.Enabled = preview?.Exists == true && bridge.LaunchEReaderSet is not null;
        eReaderSetSave.Enabled = SelectedEReaderSetIsActive() && bridge.SaveEReaderResume is not null;
        var previous = eReaderSetPreview.Image;
        eReaderSetPreview.Image = WindowsSaveStateStore.DecodePreview(preview?.Preview?.Png); previous?.Dispose();
        eReaderSetInfo.Text = entry is null ? UiText.Get("Importiere einen Kartensatz, um zu beginnen.") :
            UiText.Format("{0} Streifen. Spielstand und Schnell-Speicherplätze sind von anderen Kartensätzen getrennt.", entry.Cards.Length) + "\n\n" +
            UiText.Get(preview?.Exists == true ? "Ein Fortsetzen-Stand ist vorhanden." : "Noch kein Fortsetzen-Stand. Öffne den Kartensatz und sichere im Spiel Stand und Bild.") +
            (CardLibrary.HasFirmware ? "" : "\n\n" + UiText.Get("Wähle zuerst deine e-Reader-ROM aus."));
    }

    private Task LaunchEReaderSet(bool resume) => CardLibraryAction(async () =>
    {
        if (SelectedEReaderSet is not { } entry) return;
        string path = resume && entry.LaunchId is { } id ? CardLibrary.LaunchPath(id) : await Task.Run(() => CardLibrary.PrepareLaunch(entry.Id));
        await (bridge.LaunchEReaderSet ?? throw new NotSupportedException())(path, resume);
    });

    private async Task CardLibraryAction(Func<Task> action)
    {
        if (eReaderBusy) return;
        eReaderBusy = true;
        try { await action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or NotSupportedException)
        { if (!IsDisposed) ShowEReaderLibraryError(ex); }
        finally { eReaderBusy = false; }
    }
    private void ShowEReaderLibraryError(Exception ex)
    {
        if (eReaderLibraryFeedback is not null)
            eReaderLibraryFeedback.Text = UiText.Get("Aktion in der Kartenbibliothek fehlgeschlagen. Prüfe Dateien, Kartensatz und Schreibrechte.") + "\n" + UiText.TechnicalDetails(ex.Message);
    }
}
