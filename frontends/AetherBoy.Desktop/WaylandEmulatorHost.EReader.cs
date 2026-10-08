using AetherBoy.Runtime;
using AetherBoy.Runtime.Localization;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private bool showEReader;
    private EmulationSession? pickingEReaderSession;
    private string eReaderFeedback = "";
    private bool CanUseEReader => !IsLoading && !IsOnlineLink && localLinkSession is null && session?.LatestSnapshot.EReader is not null;

    private void DrawEReaderPage()
    {
        if (showEReaderLibrary) { DrawEReaderLibrary(); return; }
        Ink(300, 198, "e-Reader", 20, Colors.Cyan, true);
        ActionButton(860, 192, 250, 38, UiText.Get("BACK TO TOOLS"), () => OpenSettingsDestination(LinuxSettingsDestination.Tools));
        ActionButton(580, 192, 260, 38, UiText.Get("Kartenbibliothek"), () => { ReloadEReaderLibrary(); showEReaderLibrary = true; });
        DrawSettingsParagraph(300, 244, UiText.Get("Öffne zuerst deine e-Reader-ROM (USA) oder Card e-Reader/Card e-Reader+ (Japan). Eine normale GBA-Spiel-ROM reicht nicht aus."), 810);
        var reader = session?.LatestSnapshot.EReader;
        DrawSettingsParagraph(300, 305, !CanUseEReader || reader is null ? UiText.Get("Keine e-Reader-ROM im Einzelmodus geöffnet.")
            : UiText.Format("Wartende Karten: {0}/16. Begonnene Scans: {1}. Das bestätigt noch keine vom Spiel akzeptierte Karte.", reader.QueuedCards, reader.CardsStarted), 810);
        ActionButton(300, 367, 390, 42, UiText.Get("Karte aus Datei laden"), () =>
        {
            if (!CanUseEReader || Volatile.Read(ref fileDialogOpen) != 0) return;
            CommitActiveText(); pickingEReaderSession = session; ShowRomDialog();
        }, enabled: CanUseEReader && reader is { QueuedCards: < 16 });
        ActionButton(710, 367, 400, 42, UiText.Get("Scans verwerfen"), () => EReaderAction(() =>
        {
            session!.ClearEReaderCardsAsync().GetAwaiter().GetResult();
            eReaderFeedback = UiText.Get("Kartenwarteschlange und laufender Scan verworfen. Gespeicherte Programme bleiben erhalten.");
        }), enabled: CanUseEReader && reader is not null && (reader.HasCard || reader.QueuedCards > 0));
        DrawSettingsParagraph(300, 419, eReaderFeedback, 810);
        DrawSettingsParagraph(300, 483, UiText.Get("Unterstützt: RAW-Streifen mit 1872/2912 Byte und gepackte Punktbilder mit 3520/5456 Byte. Keine Fotos, PNGs oder dekodierten BIN-Dateien. Bis zu 16 Karten warten in Einfügereihenfolge."), 810);
        DrawSettingsParagraph(300, 559, UiText.Get("Standalone-Programme wurden mit vollständigen Kartensätzen getestet. Übertragung an ein zweites GBA-Spiel und Online Link sind noch nicht bestätigt."), 810);
    }

    private void ImportEReaderCard(string path) => EReaderAction(() =>
    {
        if (!ReferenceEquals(pickingEReaderSession, session)) throw new InvalidOperationException();
        session!.QueueEReaderCardAsync(EReaderInput.ReadFile(path)).GetAwaiter().GetResult();
        eReaderFeedback = UiText.Get("Karte vorgemerkt. Schließe das Menü. Wähle bei Bedarf Scan Card; ein bereits wartender Scan liest sie automatisch.");
    });

    private void EReaderAction(Action action)
    {
        try { if (!CanUseEReader) throw new NotSupportedException(); action(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or OperationCanceledException)
        { eReaderFeedback = UiText.Get("Karte nicht übernommen. Prüfe Dateiformat, freie Warteschlangenplätze und die geöffnete e-Reader-ROM."); }
    }
}
