using System.Text.Json;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Localization;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private bool showEReaderLibrary, editingEReaderTitle;
    private string eReaderTitleInput = "", eReaderLibraryFeedback = "";
    private EReaderLibrary CardLibrary => new(Path.Combine(dataPaths.Data, "ereader"));
    private IReadOnlyList<EReaderLibraryEntry> eReaderSets = Array.Empty<EReaderLibraryEntry>();
    private int eReaderSetIndex;
    private enum EReaderPick { None, NewSet, Append, Firmware }
    private EReaderPick pickingEReaderLibrary;
    private string? pickingEReaderSetId;
    private EReaderLibraryEntry? SelectedEReaderSet => eReaderSets.Count == 0 ? null : eReaderSets[Math.Clamp(eReaderSetIndex, 0, eReaderSets.Count - 1)];

    private void DrawEReaderLibrary()
    {
        var entry = SelectedEReaderSet;
        bool idle = !IsLoading && !IsOnlineLink && localLinkSession is null && stateOperation is null && Volatile.Read(ref fileDialogOpen) == 0;
        Ink(300, 198, UiText.Get("Kartenbibliothek"), 20, Colors.Cyan, true);
        ActionButton(850, 192, 260, 38, UiText.Get("Zurück zum Scanner"), () => { CommitActiveText(); showEReaderLibrary = false; });
        ActionButton(300, 241, 260, 38, UiText.Get("e-Reader-ROM auswählen"), () => PickEReaderLibrary(EReaderPick.Firmware), enabled: idle);
        ActionButton(575, 241, 260, 38, UiText.Get("Kartensatz importieren"), () => PickEReaderLibrary(EReaderPick.NewSet), enabled: idle);
        ActionButton(850, 241, 260, 38, UiText.Get("Streifen ergänzen"), () => PickEReaderLibrary(EReaderPick.Append), enabled: idle && entry is not null);
        ActionButton(300, 297, 55, 38, "<", () => SelectEReaderSet(eReaderSetIndex - 1), enabled: eReaderSetIndex > 0);
        DrawTextEntry(TextField.EReaderTitle, 370, 297, 470, 38, UiText.Get("Titel des Kartensatzes"));
        ActionButton(855, 297, 55, 38, ">", () => SelectEReaderSet(eReaderSetIndex + 1), enabled: eReaderSetIndex + 1 < eReaderSets.Count);
        ActionButton(925, 297, 185, 38, UiText.Get("Titel speichern"), () => EReaderLibraryAction(() =>
        {
            if (entry is null) return;
            CommitActiveText(); CardLibrary.Rename(entry.Id, eReaderTitleInput); ReloadEReaderLibrary(entry.Id);
        }), enabled: idle && entry is not null);
        if (entry?.LaunchId is { } identity) DrawLibraryPreview(identity, 300, 352, 240, 160);
        else { Paint(300, 352, 240, 160, Colors.Chrome); DrawSettingsParagraph(314, 405, UiText.Get("No preview"), 210); }
        DrawSettingsParagraph(560, 352, entry is null ? UiText.Get("Importiere einen Kartensatz, um zu beginnen.")
            : UiText.Format("{0} Streifen. Spielstand und Schnell-Speicherplätze sind von anderen Kartensätzen getrennt.", entry.Cards.Length) + " " +
              (CardLibrary.HasFirmware ? UiText.Get("Ein Speicherplatz pro Kartensatz. Deine e-Reader-ROM wird weiterhin benötigt.") : UiText.Get("Wähle zuerst deine e-Reader-ROM aus.")), 550);
        ActionButton(560, 449, 265, 40, UiText.Get("Kartensatz öffnen"), () => OpenEReaderSet(false), enabled: idle && entry is not null && CardLibrary.HasFirmware);
        bool hasResume = entry?.LaunchId is { } id && File.Exists(Path.Combine(dataPaths.Data, "states", id, "game.resume"));
        ActionButton(845, 449, 265, 40, UiText.Get("Fortsetzen"), () => OpenEReaderSet(true), enabled: idle && hasResume);
        bool active = entry?.LaunchId is { } activeId && activeId == storage?.Identity && CanUseEReader;
        ActionButton(300, 528, 310, 40, UiText.Get("Stand und Bild sichern"), () => { QueueSaveState(0); eReaderLibraryFeedback = UiText.Get("Das Ergebnis der Speicheraktion erscheint in der Statusleiste."); }, enabled: idle && active);
        DrawSettingsParagraph(630, 521, eReaderLibraryFeedback, 480);
        DrawSettingsParagraph(300, 578, UiText.Get("Importiere nur die Streifen eines Programms (RAW/BIN oder ZIP, bis zu 16). Ergänze weitere Karten im selben Eintrag. Die Vollständigkeit und Region prüft das Spiel. Beim ersten Start Scan Card wählen."), 810);
    }

    private void SelectEReaderSet(int index)
    {
        CommitActiveText(); eReaderSetIndex = Math.Clamp(index, 0, Math.Max(0, eReaderSets.Count - 1));
        eReaderTitleInput = SelectedEReaderSet?.Title ?? ""; focusedControl = -1;
    }
    private void ReloadEReaderLibrary(string? id = null) => EReaderLibraryAction(() =>
    {
        id ??= SelectedEReaderSet?.Id; eReaderSets = CardLibrary.ReadEntries();
        SelectEReaderSet(Math.Max(0, eReaderSets.ToList().FindIndex(e => e.Id == id))); ClearLibraryPreviews();
    });
    private void PickEReaderLibrary(EReaderPick purpose)
    {
        if (IsLoading || Volatile.Read(ref fileDialogOpen) != 0) return;
        CommitActiveText(); pickingEReaderLibrary = purpose; pickingEReaderSetId = SelectedEReaderSet?.Id; ShowRomDialog();
    }
    private void ImportEReaderLibraryFile(string path) => EReaderLibraryAction(() =>
    {
        string? id = pickingEReaderSetId;
        if (pickingEReaderLibrary == EReaderPick.Firmware) CardLibrary.SetFirmware(path);
        else id = CardLibrary.Import(path, pickingEReaderLibrary == EReaderPick.Append ? id ?? throw new InvalidOperationException() : null).Id;
        ReloadEReaderLibrary(id); eReaderLibraryFeedback = UiText.Get("Lokale Kopie übernommen. Originaldateien bleiben unverändert.");
    });
    private void OpenEReaderSet(bool resume) => EReaderLibraryAction(() =>
    {
        if (SelectedEReaderSet is not { } entry) return;
        string path = resume && entry.LaunchId is { } id ? CardLibrary.LaunchPath(id) : CardLibrary.PrepareLaunch(entry.Id);
        string identity = CardLibrary.IdentifyLaunch(path, verify: true)!;
        ReloadEReaderLibrary(entry.Id);
        CloseControlCenter();
        if (storage?.Identity == identity) { if (resume) LoadResume(); return; }
        BeginRomLoad(path, resume ? identity : null);
    });
    private void EReaderLibraryAction(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or NotSupportedException)
        { eReaderLibraryFeedback = UiText.Get("Aktion in der Kartenbibliothek fehlgeschlagen. Prüfe Dateien, Kartensatz und Schreibrechte."); diagnostics.Failure("ereader_library", ex); }
    }
}
