# UI-Texte für AetherBoy

Diese Regeln gelten für alle, die sichtbare Texte im Emulator oder auf der lokalen Online-Link-Seite schreiben. Ziel ist eine klare Stimme, die einem Spieler hilft, den nächsten Schritt zu verstehen.

## Vor dem Schreiben

1. Kläre für den konkreten Bildschirm: Was will der Spieler gerade tun? Was muss er wissen, bevor er klickt?
2. Windows, Linux und die lokale Browserbrücke verwenden die gewählte Anzeigesprache Deutsch/Englisch. Neue app-eigene Texte in beiden Sprachen im gemeinsamen `nanoboy/Runtime/Localization/UiText.json` hinterlegen und über `UiText.Get` bzw. `UiText.Format` anbinden. Browserressourcen markieren ihre Texte ausdrücklich mit `{{ui:Quelltext}}` (HTML) bzw. `/*ui*/"Quelltext"` (JavaScript); `BrowserUiText` setzt nur diese Stellen mit passendem HTML-/JSON-Escaping ein.
3. Prüfe jede technische Aussage im Code oder in der Dokumentation. Online Link ist experimentell; die Verbindung allein bestätigt keinen Tausch. Das Original bleibt erhalten, die Sitzung nutzt eine Kopie des Spielstands.
4. Lies den Text laut. Wenn er wie ein Statusbericht, eine Werbezeile oder eine interne Entwicklernotiz klingt, schreibe ihn als normalen Satz neu.

## Do / Don't

| Do | Don't | Warum |
| --- | --- | --- |
| „Teile den Raumcode mit deinem Mitspieler.“ | „Ein Raum. Zwei Spieler. Direkt im Emulator spielen.“ | Sagt, was als Nächstes zu tun ist. |
| „Die Antwort ist fertig. Kopiere sie und schick sie zurück.“ | „Antwort bereit · Kopieren · Weitergeben“ | Ein vollständiger Satz ist leichter zu scannen. |
| „Dein Originalspielstand bleibt erhalten. Diese Sitzung nutzt eine Kopie.“ | „Geschützte Save Copy · Originale unberührt“ | Nutzt die Wörter der Spieler und erklärt die Folge. |
| „Die Verbindung ist abgebrochen. Starte im Emulator eine neue Sitzung.“ | „Peer failure / data channel / reconnect“ | Fehler nennen einen nächsten Schritt. |
| „GBA-Online lässt sich nur mit freigegebenen Gen3-Spielen testen.“ | „GBA Gen3 DEV · kein universeller Kabelsupport“ | Die Grenze bleibt konkret, ohne einen erfolgreichen Tausch zu versprechen. |

## Schreibregeln

- **Buttons:** Verb + Objekt, wenn die Handlung sonst unklar ist: „Raum erstellen“, „Code kopieren“, „Verbindung beenden“.
- **Hilfetexte:** Ein Gedanke pro Satz. Nenne den nächsten Schritt vor Hintergrundwissen. Streiche Sätze, die nur die Überschrift wiederholen.
- **Status:** Sage, was jetzt passiert. „Warte auf den Mitspieler …“ ist hilfreicher als ein interner Phasenname. Ein Transferzähler ist kein bestätigter Pokémon-Tausch.
- **Fehler:** Sage, was schiefging und was der Spieler tun kann. Zeige technische Details im Bericht, wenn die kleine Statuszeile dafür zu eng ist.
- **Warnungen:** Sage genau, was ungetestet oder eingeschränkt ist. Sicherheit und Kompatibilität nicht stärker versprechen, als der Code belegt.
- **Begriffe:** „Raumcode“ für native Räume, „Einladung“ und „Antwort“ für die manuelle Browserverbindung, „Spielstandkopie“ für die lokale Sitzungskopie. STUN, TURN, HLE-BIOS und ähnliche Fachwörter nur bei Einstellungen oder Profilinformationen verwenden.
- **Ton:** Direkt, ruhig, ohne Werbewörter wie „nahtlos“, „revolutionär“, „mühelos“, „seamless“ oder „game-changing“. Keine künstlichen Dreiergruppen aus Satzfragmenten, dekorativen Großbuchstaben oder Schrägstrichlisten.
- **Länge:** Schreibe kurz genug für die tatsächliche Breite. Lass Sicherheitsangaben und die nächste Handlung sichtbar; kürze keine entscheidenden Aussagen nur für gleich hohe Felder.

## Review

Die vertiefte Sprachmigration vom 02.10.2026 umfasst auch Nebenpfade und die
Browserbrücke; der gemeinsame Katalog enthält derzeit **2376 DE/EN-Einträge**.
Eine vollständige manuelle Abnahme jedes Fensters und Fehlerfalls bleibt offen.
Systemauswahl: `de-DE`, `de-AT`, `de-CH` und weitere deutsche UI-Kulturen wählen
Deutsch; alle anderen Englisch. Sprache wird getrennt von Zahlen-/Datumsformaten
und Spielprofilen gespeichert. Ein Neustart verhindert gemischte offene Fenster.
`UiText` ausschließlich mit app-eigenen Texten aufrufen, nicht pauschal mit jedem
Label: Spieltitel, Pfade, eingegebene Codes und technische Kennungen bleiben Daten.
Dynamische Werte über Platzhalter einsetzen; keine laufzeitabhängigen Texte als
Übersetzungsschlüssel bilden. Control-Namen und Beschriftungen müssen getrennt sein.
Katalogtests prüfen alle Alias-Zuordnungen und Formatplatzhalter. Eine grüne Prüfung
beweist nicht, dass alle vorhandenen Quelltexte bereits im Katalog stehen.

Stabile Werte wie Genres, Eingabeaktionen, Sitzungs-/Recovery-Zustände und
Diagnosecodes über `UiLabels` erst an der Präsentationsstelle beschriften.
Gespeicherte Werte, Enumvergleiche, SDL-Tastennamen, Dateifiltermuster und
Ordnernamen bleiben unverändert. Fremde bzw. native Fehlermeldungen werden unter
`UiText.TechnicalDetails` unverändert angezeigt; nur exakt bekannte app-eigene
Meldungen werden übersetzt. Keine Ersetzung einzelner Wörter in Pfaden/Fehlertexten.
Lizenztexte, mitgelieferte Originaldokumente, ROM-/Nutzertitel und Gerätenamen sind
keine UI-Übersetzung. Fach-/Produktnamen wie GameShark oder CodeBreaker bleiben.

Vor einer mechanischen Migration Inventur und Diff prüfen. `--verify` der
Migrationshilfe prüft angebundene Literalquellen, nicht die semantische Vollständigkeit
der Oberfläche. Die verbleibende Literalinventur enthält auch technische IDs,
Dateinamen, Suchstichwörter und Einheiten; deren Anzahl ist keine Übersetzungsschuld.

`python3 scripts/review-ui-copy.py <dateien>` markiert einige typische KI-Muster in Quellzeilen mit UI-Text. Die Treffer sind Prüfpunkte, keine automatische Bewertung. Lies danach alle sichtbaren Strings im geänderten Ablauf und prüfe Dialoge, Statuswechsel, Fehlerfälle und schmale Fensterbreiten. Halte Protokolltexte und technische Kennungen unverändert.
