namespace AetherBoy.Runtime.Localization;

/// <summary>Presentation of stable stored values. Unknown/user-defined values are preserved.</summary>
public static class UiLabels
{
    // Physical key captions only. The enum/scancode stored in settings is never changed.
    public static string Key(string name) => name switch
    {
        "Up" => UiText.Get("Pfeil hoch"), "Down" => UiText.Get("Pfeil runter"),
        "Left" => UiText.Get("Pfeil links"), "Right" => UiText.Get("Pfeil rechts"),
        "Return" or "Enter" => UiText.Get("Eingabetaste"),
        "Back" or "Backspace" => UiText.Get("Rücktaste"),
        "Space" => UiText.Get("Leertaste"),
        "ControlKey" or "Ctrl" => UiText.Get("Strg"),
        "LControlKey" or "Left Ctrl" => UiText.Get("Strg links"),
        "RControlKey" or "Right Ctrl" => UiText.Get("Strg rechts"),
        "ShiftKey" or "Shift" => UiText.Get("Umschalt"),
        "LShiftKey" or "Left Shift" => UiText.Get("Umschalt links"),
        "RShiftKey" or "Right Shift" => UiText.Get("Umschalt rechts"),
        "Delete" => UiText.Get("Entf"),
        "Insert" => UiText.Get("Einfg"),
        "Home" => UiText.Get("Pos1"),
        "End" => UiText.Get("Ende"),
        "PageUp" or "Page Up" or "Prior" => UiText.Get("Bild auf"),
        "PageDown" or "Page Down" or "Next" => UiText.Get("Bild ab"),
        _ => name
    };

    public static string Input(string action) => action switch
    {
        "Up" => UiText.Get("Oben"),
        "Down" => UiText.Get("Unten"),
        "Left" => UiText.Get("Links"),
        "Right" => UiText.Get("Rechts"),
        "QuickSave" => UiText.Get("Schnellspeichern"),
        "QuickLoad" => UiText.Get("Schnellladen"),
        _ => action // Hardware buttons A/B/L/R/Start/Select keep their names.
    };

    public static string Health(string code) => code switch
    {
        "ui.unresponsive_suspected" => UiText.Get("Oberfläche reagiert verzögert"),
        "startup.slow_suspected" => UiText.Get("Start dauert länger als erwartet"),
        "emulation.stalled_suspected" => UiText.Get("Emulation ohne erkennbaren Fortschritt"),
        "video.no_frames_suspected" => UiText.Get("Keine neuen Spielbilder erkannt"),
        "presentation.stalled_suspected" => UiText.Get("Bildausgabe ohne erkennbaren Fortschritt"),
        "video.uniform_suspected" => UiText.Get("Spielbild bleibt einfarbig"),
        _ => code
    };

    public static string Session(SessionState state) => UiText.Get(state switch
    {
        SessionState.Starting => "Wird gestartet",
        SessionState.Running => "Playing",
        SessionState.Paused => "Paused",
        SessionState.Stopping => "Wird beendet",
        SessionState.Stopped => "Stopped",
        _ => "Faulted"
    });

    public static string Link(Netplay.OnlineLinkPhase phase) => UiText.Get(phase switch
    {
        Netplay.OnlineLinkPhase.WaitingForBrowser => "Wartet auf Browser",
        Netplay.OnlineLinkPhase.WaitingForPeer => "Wartet auf Mitspieler",
        Netplay.OnlineLinkPhase.Playing => "Playing",
        Netplay.OnlineLinkPhase.WaitingForTransfer => "Wartet auf Übertragung",
        Netplay.OnlineLinkPhase.Closed => "Stopped",
        _ => "Faulted"
    });

    public static string Recovery(Netplay.OnlineSaveRecoveryState state) => UiText.Get(state switch
    {
        Netplay.OnlineSaveRecoveryState.Active => "Aktiv",
        Netplay.OnlineSaveRecoveryState.CleanStopped => "Ordentlich beendet",
        Netplay.OnlineSaveRecoveryState.Interrupted => "Unterbrochen",
        Netplay.OnlineSaveRecoveryState.Faulted => "Faulted",
        Netplay.OnlineSaveRecoveryState.Promoting => "Import läuft",
        Netplay.OnlineSaveRecoveryState.Promoted => "Importiert",
        _ => "Ungültig"
    });

    public static string Genre(string id) => id switch
    {
        "" => UiText.Get("Keine"),
        "action" => "Action",
        "rpg" => UiText.Get("Rollenspiel"),
        "puzzle" => "Puzzle",
        "sports" => UiText.Get("Sport"),
        "strategy" => UiText.Get("Strategie"),
        "other" => UiText.Get("Sonstiges"),
        _ => id
    };

    public static string LibrarySort(string id) => id switch
    {
        "Recent" => UiText.Get("Zuletzt gespielt"),
        "Title" => UiText.Get("Titel A–Z"),
        "Playtime" => UiText.Get("Spielzeit"),
        "Rating" => UiText.Get("Bewertung"),
        "Genre" => "Genre",
        "System" => UiText.Get("System"),
        "Tags" => "Tags",
        _ => id
    };
}
