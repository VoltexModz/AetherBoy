# Unabhängige Linux-Kritik — Runde 3

Stand: 12. September 2026. Diese Prüfung folgt dem Nutzerauftrag, die nach
Runde 2 verbleibenden Softwarepunkte zu schließen. Die feste Rubrik bleibt
unverändert; die letzte abgeschlossene Bewertung war **UI 8,9 / Features 8,6**.
Hardware- und Langzeittests verbleiben beim Nutzer. Keine Note wird aus dem
gewünschten Zielwert abgeleitet.

**Abgeschlossen: UI 9,1 / 10; neue Features 8,9 / 10.** Beide geforderten
Schwellen — UI **>9** und Features **>8** — sind im dokumentierten Software-
Prüfrahmen erreicht. Das ist keine Hardware-/Langzeitfreigabe. Die früheren
8,9 / 8,6 bleiben als historische Bewertung unverändert nachvollziehbar.

## Unveränderte Rubrik

| UI-Kriterium | Gewicht | Vorher | Nachher | Begründung der Veränderung |
| --- | ---: | ---: | ---: | --- |
| Lesbarkeit | 20 % | 9,2 | 9,3 | Textfeld bleibt dunkel, Textkontrast erhalten; Caret/Preedit/Auswahl sichtbar. |
| Hierarchie | 15 % | 8,9 | 8,9 | Grundstruktur unverändert; keine zusätzliche Note allein für ein weiteres Fenster. |
| Konsistenz | 15 % | 9,2 | 9,2 | Gemeinsame Aktionen und Einstellungen; native GTK-Darstellung ist ausdrücklich ein alternativer Bedienpfad. |
| Navigation und Feedback | 25 % | 9,0 | 9,4 | Vollständigerer Editor, Fehler-/Fokusschutz, native semantische Aktionen und bewiesene Rückkopplung zum Host. |
| Skalierung und Zugänglichkeit | 25 % | 8,2 | 8,8 | Zusätzlich echter AT-SPI-Buszugriff und GTK-Textbedienung; Hardware/Orca und vollständige SDL-Fontabdeckung bleiben unbewiesen. |
| **UI gesamt** | **100 %** | **8,9** | **9,1** | **Gewichtet 9,125, auf eine Nachkommastelle gerundet.** |

| Feature-Kriterium | Gewicht | Vorher | Nachher | Begründung der Veränderung |
| --- | ---: | ---: | ---: | --- |
| Korrektheit | 25 % | 8,8 | 9,0 | Zustandswechsel, Cancellation, GTK-Routing und Editorverträge gezielt geprüft. |
| Datenintegrität | 30 % | 8,9 | 9,0 | Neue Generationen gehen beim Speichern nicht verloren; Scope-Wechsel, Load-Cancel und GBA-Owner-Barriere sind abgesichert. |
| Reaktionsfähigkeit | 15 % | 8,3 | 8,8 | ROM-Vorbereitung und normale Settings-Persistenz laufen begrenzt im Hintergrund; explizite sichere Abschlussgrenzen bleiben synchron. |
| Vollständigkeit und Bedienbarkeit | 20 % | 8,4 | 8,8 | Editor und zusätzlicher semantischer Bedienpfad schließen konkrete vorige Softwarelücken. |
| Linux-Integration | 10 % | 8,4 | 8,9 | GTK/ATK und separater AT-SPI-Client im isolierten Weston erfolgreich; normale Wayland-Suite und portable Paketierung liegen zusätzlich vor. |
| **Features gesamt** | **100 %** | **8,6** | **8,9** | **Gewichtet 8,92, auf eine Nachkommastelle gerundet.** |

0 bedeutet unbenutzbar, 5 brauchbar mit häufigen Problemen, 7 zuverlässig mit
klaren Einschränkungen, 9 ausgereift und im vereinbarten Umfang gut geprüft.
Gewichtete Mittelwerte werden auf eine Nachkommastelle gerundet. Ein falsches
ROM-Ziel/Datenverlust blockiert die Feature-Abnahme; eine unerreichbare wesentliche
Aktion die UI-Abnahme. Schöne Bilder und Testanzahlen ersetzen keine Prüfung
von Zustandsübergängen.

## Konkrete Review-Befunde und Änderungen

| ID | Befund | Änderung und erforderlicher Nachweis |
| --- | --- | --- |
| R3-T1 | Der vorige Texteditor konnte nur am Ende löschen/anhängen. | Gemeinsamer graphemorientierter Editor: Cursor, Home/End, Shift-Auswahl, Delete, Ctrl+A/C/X/V, Pointer-Auswahl und horizontales Nachführen. Editor-/native Feldtests prüfen echte Inhalte und Auswahlpositionen. |
| R3-T2 | SDL-IME-Offsets sind Codepoints, nicht UTF-16-Indizes oder Bytes. | Offset-Umrechnung gemäß installiertem SDL-Header, separate Preedit-Komposition mit sichtbarer Auswahl; Commit ersetzt die ursprüngliche Auswahl. Escape/Focus/Seitenwechsel hinterlassen keine versteckte Komposition. |
| R3-T3 | Titel-Editor blieb nach Tab/F6 aktives SDL-Textziel. | SDL-Texteingabe wird beendet; Text-/Kompositionsereignisse ohne Editorfokus werden verworfen. Native Regression prüft unveränderten Titel. |
| R3-T4 | Leere Rückgabe von SDL_GetClipboardText kann ein Fehler sein. | Adapter prüft frischen SDL-Fehler statt Auswahl durch eine vermeintlich leere Zwischenablage zu löschen; Cut schreibt vor dem Löschen. |
| R3-A1 | Keine semantische Assistenztechnik-Anbindung der gezeichneten Buttons. | Optionales GTK3-Bedienfenster spiegelt Control-Center-Aktionen und Texte mit nativen Rollen, Namen, Werten, checked/enabled und GTK-Textfeld. Host-Tests plus eigener AT-SPI-Client sind erforderlich. |
| R3-A2 | Neuer Accessibility-Shortcut fing das bestehende F7-Rewind ab. | Accessible UI nutzt Ctrl+F7 und den CLI-Schalter `--accessible`; F7 bleibt Rewind. Direkter Host-Test prüft die Trennung. |
| R3-A3 | Koordinatenfehler entzog Saveslots ihre checked-Semantik; Controllerbuttons hatten nur generische Namen. | Echte aktive Slots und Optionen werden semantisch ausgewiesen; Mapping-Namen nennen Aktion und Taste; Backup-Navigation bleibt normaler Button. |
| R3-A4 | Programmatisches GTK-clicked konnte einen disabled Callback auslösen. | Callback prüft Sensitivität, der Host zusätzlich den aktuellen Command. Lokaler ATK-Test und externer Bus-Test prüfen ausbleibende App-Aktion. |
| R3-A5 | Navigation/Typwechsel eines stabilen GTK-Schlüssels konnte im falschen Container bleiben. | Widget wird passend ersetzt; Fokus bleibt an logischer Identität, keine synthetische Aktivierung durch Refresh. |
| R3-L1 | ROM-Resolve/Hash/Migration/Firmware blockierten den UI-Thread. | Ein begrenzter Worker mit Cancellation und Snapshot-Übergabe; Originalsession und Schreibbesitz bleiben bis zur erfolgreichen Übergabe erhalten. |
| R3-L2 | FocusGained konnte den alten GBA-Owner während Start des neuen Owners reaktivieren. | Fokus-Resume wird während Vorbereitung zurückgestellt. Native GBA→GBA-/Late-Cancel-Regression prüft Owner-Barriere, Lock-Freigabe und erneute Frameproduktion. |
| R3-S1 | Gewöhnlicher Settings-Flush konnte UI-Polling blockieren. | Ein Worker erhält ausschließlich unveränderliche serialisierte Bytes; Generation/dirty/retry verhindert Verlust neuerer Änderungen. Nur explizite Scope-/ROM-/Shutdown-Grenzen warten. |

Der Reviewer hat keine Produktionsdateien geändert. Eigene Prüffälle liegen in
`tests/AetherBoy.DesktopTests/LinuxAccessibilityReviewTests.cs` und
`LinuxAccessibilityBusTests.cs`. Die übrigen neuen Tests stammen vom unabhängigen
Playtest-/Async-Agenten; ihre Ergebnisse werden als solche bezeichnet.

## Accessibility-Nachweis: was er tatsächlich bedeutet

Die ATK-Prüfung verwendet echte native Interfaces für Rolle, StateSet, Action und
EditableText. Der Host-Mirror-Test aktiviert SMOOTH über GTK und prüft die reale
SDL-Konfiguration, aktive Saveslot-Semantik sowie Ablehnung einer alten, bereits
wartenden Aktion nach einem Seitenwechsel.

Der stärkere Bus-Test startet einen separaten Python/GI-AT-SPI-Client. Dieser
sucht das eindeutig benannte GTK-Fenster auf einem privaten Accessibility-Bus,
prüft Namen/Rollen/checked/enabled, aktiviert eine erlaubte Aktion und ändert
Unicode-Text. Die .NET-Seite muss die entsprechenden echten Anfragen empfangen,
eine disabled Aktion hingegen niemals. Das GTK-Fenster wird ausschließlich auf
`wayland-accessibility-test` im isolierten Weston-Container gemappt; zusätzliche
Opt-in-Variablen verhindern den normalen Teststart auf einem persönlichen Display.

Eine anfängliche Testannahme wurde anhand der Primärquelle korrigiert:
Der AT-SPI-Bridge-Code bestätigt `DoAction` bereits vor dem lokalen ATK-Aufruf
und gibt dessen boolesches Ergebnis nicht zurück. Ein positives D-Bus-Reply
beweist deshalb keine Ausführung. Der Test prüft stattdessen State und wirkliche
App-Anfragen nach einem folgenden Text-Roundtrip. Der lokale ATK-Test erwartet
weiterhin `false` für disabled Buttons. Das ist eine begründete Korrektur der
Testsemantik, keine gelockerte Anforderung an disabled Aktionen.
[GNOME AT-SPI-Bridge](https://raw.githubusercontent.com/GNOME/at-spi2-core/main/atk-adaptor/adaptors/action-adaptor.c),
[GTK3-Button-Accessibility](https://raw.githubusercontent.com/GNOME/gtk/gtk-3-24/gtk/a11y/gtkbuttonaccessible.c).

Zwei weitere Fehler lagen im Test selbst: Python/GI verwechselt bei dynamischem
Methodenaufruf den veralteten Accessible-Getter mit der Text-Interface-Methode;
der Client ruft jetzt `Atspi.Text.get_text` explizit auf. Der Ownership-Test nutzt
einen echten zusätzlichen Thread, weil wartendes Task-Scheduling die Aufgabe auf
dem aufrufenden Thread ausführen konnte. Rolle/Wert/Aktions- und Thread-Grenzen
bleiben unveränderte Abnahmekriterien.


## Abschlussbelege und wirklich offene Punkte

- **6/6 GTK-/AT-SPI-Fälle bestanden**, kein Skip. Der Reviewer hat
  `artifacts/gtk-accessibility-bus-final.log` selbst gelesen. Dazu gehören fünf
  lokale ATK-/Host-Fälle und der separate AT-SPI-Client im privaten Bus.
- **9/9 Texteditor-Fälle bestanden**, TRX-Zähler in `artifacts/text-editor.trx`
  selbst gelesen. Zusätzlich führte der Reviewer den bestehenden nativen
  Feldtest nach dem letzten Kontrastfix nochmals einzeln aus: **1/1 bestanden**.
  Der Test nutzt eine Fake-Zwischenablage und ein verstecktes SDL-Fenster.
- Das dabei neu erzeugte `artifacts/comfort-review-round3/title-ime-large.png`
  wurde tatsächlich angesehen: 900×650, größte Textstufe, sichtbare Preedit-
  Auswahl und Caret, dunkles Feld, lesbare Texte, keine überlappte Hauptaktion.
  SAVE TITLE ist während laufender Komposition korrekt deaktiviert.
- **31/31 Async-/Comfort-Fälle** wurden vom zuständigen Agenten gemeldet,
  einschließlich sieben ROM-Vorbereitungs- und fünf Settings-Fälle. Diese Zahl
  wird nicht als ein eigenhändig vom Reviewer gestarteter Lauf ausgegeben.
- Den abschließenden normalen Weston-Lauf hat der Reviewer im Log
  `artifacts/linux-round3-headless-final.log` nachgelesen: **108 bestanden,
  3 bewusst übersprungene Audiofälle, 0 fehlgeschlagen**, insgesamt 111 Fälle.
  Das finale Buildlog meldet null Warnungen und Fehler.

**R3-T1 bis R3-T4, R3-A1 bis R3-A5, R3-L1/L2 und R3-S1 sind im beschriebenen
Software-Prüfrahmen geschlossen.** Der Kontrastfehler des ersten Editorbilds
wurde während des Reviews gefunden und vor der Endbewertung korrigiert; eine
pauschale Abnahme des ersten hübschen Bildes hätte ihn übersehen.

Es bleibt ein sichtbarer Fontabdeckungsrest: eine Emoji-/Symbolglyphe wird im
SDL-Capture nicht dargestellt. Die Zeichen bleiben in den Text-/Busverträgen
intakt; das ist keine vollständige visuelle Unicode-Fontabdeckung. GTK verwendet
Systemfonts. Reale Orca-Bedienung, konkrete IBus/Fcitx-Sitzungen, Hardwaregeräte
und die unten genannten Umgebungsgrenzen werden durch diese Note nicht erfunden.
Die 9,1 bewertet die nun überprüften Softwarepfade; sie ist keine Behauptung,
dass nichts mehr verbessert werden kann.

## Fortbestehende Grenzen

- ATK/AT-SPI-Belege sind keine gehörte Orca-Nutzerstudie und kein Nachweis
  nichtvisueller Spielbarkeit beliebiger Spiele. Der SDL-Zeichenbaum selbst bleibt
  ohne AT-SPI; der optionale GTK-Pfad stellt die semantischen Bedienelemente bereit.
- GTK3/ATK sind optionale Distributionsbibliotheken; bei Fehlen bleibt die normale
  UI verfügbar und zeigt einen verständlichen Hinweis. Pakete sind keine vollständige
  Linux-Systemumgebung.
- Kein neuer physischer Controller-, Mixed-DPI-, ARM64-Ausführungs-, hörbarer
  Audio- oder 30-Minuten-Test. Der private Headless-Compositor hat keinen echten
  Keyboard-Seat; GTK meldet dies. Logische Aktionen/Fokus und Buszugriff sind
  dadurch prüfbar, reale Gerätezustellung jedoch nicht nachgewiesen.
- Cooperative Cancellation kann einen bereits im Betriebssystem blockierten
  Datei-I/O-Aufruf nicht hart abbrechen. Explizite Save-/Scope-/Shutdown-Grenzen
  dürfen zum Schutz von Daten weiterhin warten.
- Keine Prüfung beliebiger Stromausfälle, unzuverlässiger Netzlaufwerke oder
  einer parallelen externen Veränderung aller Benutzerdateien behaupten.
