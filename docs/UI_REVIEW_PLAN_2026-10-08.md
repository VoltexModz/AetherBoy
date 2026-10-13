# Arbeitsplan für die AetherBoy UI

Die UI soll beim Öffnen, Spielen, Speichern und Verwalten von Spielen verständlich,
lesbar und verlässlich bedienbar sein. Grundlage ist der kritische Review vom
8. Oktober 2026. Die sechs Themes, Eigenfarben und Aether-Wave-Komponenten bleiben
erhalten. Zuerst werden belegte Darstellungs- und Bedienungsprobleme behoben;
anschließend werden gemeinsame Komponenten und Fehlerabläufe geprüft.

## Arbeitspakete

| Paket | Priorität | Ergebnis | Zuständigkeit |
| --- | --- | --- | --- |
| 1 Lesbarkeit | Hoch | Linux-Speichertexte verwenden Themefarben und bleiben bei hellen Eigenfarben lesbar. | Linux Darstellung und Speichern |
| 2 Meldungen | Hoch | Kurze Statusmeldung mit vollständig erreichbaren Details, ohne Überlagerung oder verlorene Pfade. | Linux Darstellung und Speichern |
| 3 Speicherzustände | Mittel | Ohne Spiel kein falscher Ladevorgang; leere, belegte und nicht lesbare Plätze unterscheidbar; Laden nur bei sinnvoller Verfügbarkeit. | Linux Darstellung und Speichern sowie Windows Komponenten |
| 4 Bibliothek | Mittel | Flächenabhängige Eintragszahl, direkt auswählbare Filter und Sortierung, erkennbare Bibliotheksüberschrift. | Linux Bibliothek und Accessibility |
| 5 Accessibility | Hoch für betroffene Nutzer | Sprechende Fensteraktionen, Auswahlzustände und zugängliche Hauptspielbefehle. | Linux Bibliothek und Accessibility sowie Windows Komponenten |
| 6 Dateipicker | Mittel | Verzeichnislesen im Hintergrund; Laden, Navigation und Abbruch bleiben verständlich und bedienbar. | Windows Komponenten |
| 7 Link und Integration | Mittel | Fehler- und Rückkehrabläufe geprüft, Warnungen lesbar, Schutz der Sitzungskopie erhalten. | Koordination und Abschlussreview |

### 1 Lesbarkeit

- Verbliebene feste Textfarben auf der Linux-Speicherseite an die gemeinsame
  Palette anbinden. Akzent- und Statusfarben nur nach ihrer tatsächlichen Bedeutung
  verwenden.
- Aether Original, Pocket Light, die vier weiteren Themes sowie helle und dunkle
  Eigenfarben berücksichtigen. Gespeicherte Farbwerte nicht verändern.
- Abnahme: normale und große Schrift, kleinste unterstützte Fenstergröße,
  DE/EN; Speicherstatus und Hilfetexte bleiben lesbar.

### 2 Meldungen

- Fehler im Hauptfenster und in Einstellungen erhalten einen erreichbaren
  Detailzugang. Der vollständige Originaltext bleibt erhalten.
- Ein gemeinsamer Linux-Detailbereich unterstützt Umbruch langer Pfade,
  Scrollen, Auswahl und Kopieren sowie Schließen mit Escape oder Controller.
- Der sichtbare Fehlerbereich reserviert seine tatsächliche Höhe. Weder
  Tastaturhilfe noch Bedienelemente dürfen durch Meldungen verdeckt werden.
- Abnahme: synthetische mehrzeilige Meldung mit langem ungetrenntem Pfad;
  Anfang, entscheidender nächster Schritt und Meldungsende sind erreichbar.
  Maus-, Tastatur- und Controllerfokus bleiben nachvollziehbar.

### 3 Speicherzustände

- Zustände ohne Spiel, während des Lesens, leer, vorhanden und nicht lesbar
  ausdrücklich darstellen. Keine endlose Ladeanzeige ohne geöffnete ROM.
- Windows-Hauptfenster zeigt die Belegung der Plätze und berücksichtigt sie für
  die Ladeaktion. Linux behält seine vorhandene Verfügbarkeitssperre.
- Metadaten im Hintergrund lesen und Ergebnisse nur auf die zugehörige Sitzung
  anwenden. Fehlerbehandlung, Online-Sperren und Schutz laufender Zustände erhalten.
- Abnahme: künstliche ROM und temporäre Speicherplätze; leere, belegte und
  beschädigte Daten, Sitzungswechsel und laufende Speicheraktion prüfen.

### 4 Bibliothek

- Liste und Kacheln nutzen die tatsächlich verfügbare logische Fläche und
  berücksichtigen Schriftgröße. Keine feste Beschränkung auf drei beziehungsweise
  vier Spiele unabhängig vom Fenster.
- Genre, System und Sortierung werden direkt auswählbar. Vorhandene Suche,
  Favoriten, Metadaten und Startbefehle bleiben erreichbar.
- Trefferzahl, Auswahl und Navigation bleiben bei Filteränderungen konsistent.
  Die Überschrift bezeichnet die Bibliothek statt pauschal Einstellungen.
- Abnahme: 100 synthetische Einträge, lange Titel, fehlende Dateien, leere
  Trefferliste, alle Filter, erste und letzte Seite sowie Maus/Tastatur/Controller.

### 5 Accessibility

- Windows-Fensteraktionen erhalten lokalisierte Namen. Ausgewählte zentrale
  Buttons geben ihren Zustand über Accessibility wieder, ohne normalen Buttons
  eine unpassende Rolle zu geben.
- Die vorhandene Linux-GTK-/AT-SPI-Brücke wird auf Erststart und Hauptspielbefehle
  erweitert. Namen, Zustände, sichtbare Grenzen und Aktionen müssen zur SDL-UI
  passen. Die bestehenden Listen- und Eingabefunktionen bleiben erhalten.
- Abnahme: automatisierte Rollen-/Zustands- und Befehlsprüfungen; anschließend
  tatsächlicher Screenreaderablauf Öffnen, Spielen, Pausieren, Speichern und Zurück.
  Ein bestandener Strukturtest ersetzt diese praktische Abnahme nicht.

### 6 Dateipicker

- Öffnen, Ordner- und Filterwechsel lesen Dateisystemdaten im Hintergrund.
  Sichtbare Lade- und Fehlerzustände erklären, was gerade geschieht.
- Ergebnisse alter Navigationen werden verworfen. Schließen und Abbruch sind
  auch bei laufendem oder nicht zeitnah abbrechbarem Dateisystemzugriff möglich.
  Hintergrundaufgaben dürfen nach Schließen keine UI-Handles verwenden.
- Dateifilter, Endungen, Auswahl, Überschreibbestätigung und Importprüfungen
  unverändert erhalten. Keine native Dialogoberfläche oder neue UI-Schuld.
- Abnahme: kontrolliert verzögerte synthetische Zugriffe, schneller Ordnerwechsel,
  Filterwechsel, unzugänglicher Pfad, Abbruch und Schließen während des Ladens.

### 7 Link und Integration

- Vorhandene Link-Prüfungen für fehlendes Spiel, unzulässiges Profil, ungültige
  Server-/Raumdaten, Verbindungsabbruch, Rückkehr und Sitzungskopie ausführen.
- Betroffene Meldungsansichten in DE/EN, hell/dunkel und großer Schrift prüfen.
  Bestehende Zustimmung und Sicherheitsprüfungen erhalten; kein neues
  Kompatibilitäts- oder Tauscherfolgsversprechen.
- Gemeinsame Übersetzungen, Controller-Hooks und Dokumentation zentral integrieren.

## Reihenfolge und Abhängigkeiten

1. Drei Umsetzungspakete arbeiten parallel mit getrennten Produktdateien.
   Katalog, Controller-Hooks und Dokumentation liegen bei der Koordination.
2. Nach Integration erfolgen frische Builds und gezielte Funktions-, Theme-,
   Lokalisierungs- und Windows-UI-Policy-Prüfungen. Gemeinsame Buildausgaben
   werden nicht gleichzeitig beschrieben.
3. Zwei kritische Reviewer prüfen Code und Darstellung über Kreuz. Autoren
   nehmen ihr eigenes Paket nicht ab; die Koordination schließt verbleibende
   Überschneidungen. Jeder Befund braucht eine konkrete Folge und Evidenz.
4. Belegte Regressionen werden behoben und die betroffenen Prüfungen wiederholt.
   Abschließend werden erledigte Punkte und echte Abnahmegrenzen dokumentiert.

## Bewertung und Wechsel des Ansatzes

Code und visuelle Bedienung erhalten jeweils eine eigene Bewertung von 1 bis 10
mit einer Nachkommastelle. Bewertet wird das Ergebnis der Arbeitspakete samt
betroffenen Spielerabläufen. Beide Reviewer begründen die Note mit konkreten
Befunden und unterscheiden geprüfte Zustände von fehlenden Prüfbelegen.

- Fertig ist der geprüfte Umfang erst, wenn **beide Bewertungen über 9,3** liegen,
  keine schwerwiegenden Befunde offen sind und die erforderlichen ausführbaren
  Prüfungen bestanden wurden. Ein Mittelwert genügt nicht.
- Der Codereview bewertet Zustandslogik, Regressionen, Hintergrundarbeit und
  Lebensdauer, Accessibility-Semantik, Lokalisierung und aussagekräftige Tests.
- Der Visualreview bewertet Lesbarkeit, Hierarchie, Platznutzung, Fokus,
  Meldungen und Bedienung in den tatsächlich angesehenen Zuständen.
- Nach zwei aufeinanderfolgenden Review-Runden ohne erkennbare Verbesserung
  (Note unverändert oder nur um höchstens 0,1 erhöht und derselbe Kernbefund
  besteht weiter) wird der Ansatz neu gewählt. Der zuständige Autor benennt die
  Ursache und mindestens eine alternative Lösung mit anderem Aufbau oder
  Interaktionsablauf; der nächste Versuch setzt die gewählte Alternative um.
- Noten werden nicht auf Wunsch erhöht. Fehlende Windows-Live-, Hardware- oder
  Screenreaderbelege werden separat als offene Abnahme dokumentiert. Eine Note
  für den hier geprüften Umfang behauptet keine vollständige Plattformabnahme.

Die Abschlussnotizen halten pro Runde Code- und Visualnote, offene Befunde,
Änderungen und gegebenenfalls den Wechsel des Ansatzes fest.

## Regeln für die Umsetzung

- Vorbestehende e-Reader- und Dokumentationsänderungen bleiben erhalten.
- Neue sichtbare Texte über `UiText` und den gemeinsamen DE/EN-Katalog anbinden;
  technische Kennungen, Nutzerdaten, Pfade und Protokollwerte nicht übersetzen.
- Nur gemeinsame Aether-Komponenten beziehungsweise eigene Linux-SDL-Komponenten
  verwenden. `WindowsUiDebt.json` nicht erhöhen oder blind regenerieren.
- Keine privaten ROMs, Spielstände oder produktiven Einstellungen für Prüfungen.
- Kein Commit, Push, Deployment oder externer Versand im Rahmen dieses Auftrags.

## Abschlussstand — 09.10.2026

Die Softwarearbeiten der sieben Pakete sind umgesetzt. Auf Wunsch wurden auch
die hier ausführbaren automatisierten Abnahmen der betroffenen Abläufe erledigt.
Der Nutzer hat bestätigt, dass keine Windows-VM verfügbar ist. Echte Windows-,
Monitor- und Geräteabnahmen bleiben deshalb ausdrücklich von den folgenden
Softwarebelegen getrennt; dieser Plan erteilt keine pauschale Plattformfreigabe.

| Prüfung | Ergebnis und Aussage |
| --- | --- |
| Linux-Funktions-/UI-Auswahl am privaten Weston | **61/61**, keine Fehler oder Skips. Einschließlich Theme-/Sprachwechsel, Meldungen, Save-/Link-Rückkehr, Bibliothek, Sofamodus, Texteditor, virtueller Controller und native Accessibility-Signalregression. |
| Separater AT-SPI-Client, Komponenten | **1/1**, keine Skips; echter privater Accessibility-Bus. |
| Separater AT-SPI-Client, laufender Emulator | **1/1**, keine Skips; Bibliothek/Dropdowns, Pause/Fortsetzen/Turbo, Empty→Save→Saved→Load mit echter Zustandsdatei und Undo sowie vollständige Unicode-Details, readonly Text, Auswahl, Kopieren und Rückkehr. |
| Orca-Softwareprobe mit privatem Software-Seat | Gleicher echter Hostablauf nochmals **1/1**, keine Skips; normale Fokus-/Sprachpipeline bis zur stummen Speech-Dispatcher-Ausgabe bestätigt. Wiederholung des Hostfalls, kein zusätzlicher unterschiedlicher Test zu 61+1+1 = **63 Linux-Fällen**. |
| Gemeinsame Lokalisierung | **19/19**, keine Skips; gemeinsamer DE/EN-Katalog. |
| Windows-Tests unter Wine mit offizieller Microsoft-.NET-10-Runtime | **43/43**, keine Fehler oder Skips: UI-Policy 3, Review-Followup 15, Aether-Remainder 10, Theme 11, Lokalisierung 4. Kompatibilitätsbeleg, keine echte Windows-UIA-/DPI-Abnahme. |
| Frische Linux-/Windows-Builds samt Testprojekten | Erfolgreich, keine Warnungen oder Fehler. |
| UI-Copy und Struktur | Geänderte Texte geprüft; abschließende Prüfung der Save-/Windows-Deltas enthält nur den vorbestehenden Titel „GAME BOY · COLOR · ADVANCE“. `git diff --check` ohne Fehler. UI-Policy und Schulden-Baseline unverändert. |

Die Belege verwenden synthetische ROMs, temporäre Spielstände und private
Display-/Bus-Sitzungen. Portable Weston-, Wine- und Runtime-Dateien liegen unter
`/tmp`; persönliche Einstellungen wurden nicht für die Prüfung umgestellt.

### Zusätzlich erledigte Restpunkte

- Linux-Speicherseite neu aufgeteilt: alle fünf Slotzustände und vollständige
  Nebenaktionen passen auch bei 860×554 mit Textgröße 18. Status und Hilfe
  reservieren ihre tatsächliche Höhe; Refresh, Hauptaktionen, Details und
  Nebenaktionen folgen der sichtbaren Reihenfolge im Tastaturfokus. Zwei neue
  DE/EN-Testfälle prüfen drei Fenster-/Schriftkombinationen, NoGame/Saved/Error,
  echte Schriftbreite, überlappungsfreie Trefferflächen und Detail-Rückkehr.
- Der Linux-Teststarter entfernt geerbte `DISPLAY`-/`WAYLAND_SOCKET`-Werte und
  baut einen privaten Weston-/D-Bus-/AT-SPI-Kontext auf. Die Probe mit geerbtem
  `WAYLAND_SOCKET=99` besteht; persönliche Display-FDs können den privaten
  Compositor nicht umgehen. Komponenten- und Hostprobe laufen getrennt, damit
  native GTK-Brücken nicht innerhalb desselben Testprozesses kollidieren.
- Windows: Die rechte Leiste berücksichtigt jetzt die 16 Pixel der eigenen
  vertikalen Scrollleiste. Zuvor lag der fünfte Slot vier Pixel außerhalb seiner
  verfügbaren Fläche. Bestehende Theme-Assertions bleiben unverändert;
  Mindestformat wird zusätzlich bei 100/125/150 Prozent UI-Skalierung geprüft.
- Windows: Aether-Buttons geben eine eindeutige Buttonrolle zurück und erhalten
  ausdrücklich gesetzte Rollen. Ein einziges AccessibleObject folgt dem
  Handle-Lebenszyklus; der zuvor separat erzeugte Plattformwrapper konnte einen
  veralteten Disabled-Zustand liefern. Die Prüfung erzwingt Handle-Neuerzeugung
  und behält die harten Sperren für handlelose, deaktivierte, versteckte und
  entsorgte Buttons bei.
- Ein zusätzlicher Fremd-Codeabgleich mit den offiziellen .NET-10-Quellen hat
  keinen belegten Verlust von UIA-Invoke oder ControlType durch die gemeinsame
  Button-Accessibilityklasse ergeben. Das ist ein Quellenbeleg; der Wine-Test
  wurde weiterhin nicht als externer UIA-Clienttest gewertet.
- Linux-Accessibility: Eine echte Orca-Probe deckte zuletzt eine belegte Flut
  unveränderter Namen auf. Die Brücke setzt native ATK-Namen und das Eingabelabel
  jetzt nur bei tatsächlicher Änderung. Die neue native Signalregression beweist
  **0 zusätzliche Namens-/Labelsignale über 120 identische Frames** sowie weiterhin
  korrekte DE-/Unicode-Umbenennungen und readonly Memo-Wechsel. Zustands-, Fokus-
  und Textänderungen bleiben erhalten.
- Die gleiche Orca-Fokusprobe bestätigt nach dem Fix echte Ansagen für Bibliothek,
  Suche, System/Sortierung, Pause/Fortsetzen, Turbo, Slot 2, Speichern/Laden,
  readonly Details mit synthetischem Meldungsinhalt, Kopieren und Schließen.
  Queueing-Namenslogzeilen sinken von **60.156 auf 32**; rohe Namenslogzeilen von
  80.086 auf 67. Dies sind definierte Logzählungen, keine unabhängige Zählung
  eindeutiger nativer Benachrichtigungen. Keine künstlichen Orca-Sprachaufrufe.
- Der Windows-Dateipicker wurde mit blockiertem Zugriff, Navigation, Filter- und
  Fensterwechsel, verspäteten Erfolgen/Fehlern sowie Abbruch/Dispose ausgeführt.
  Save-Inventar, Sitzungswechsel und Ladeverfügbarkeit sind ebenfalls geprüft.

| Paket | Stand |
| --- | --- |
| 1 bis 3 Linux | Implementiert, nativ geprüft; zuvor gekürzte Save-Nebenaktionen vollständig sichtbar. |
| 4 und 5 Linux | Implementiert, native UI- und externe AT-SPI-Prüfungen bestanden. |
| 3, 5 und 6 Windows | Implementiert, kompiliert und 43 gezielte Tests unter Wine bestanden; echte Windows-Liveabnahme offen. |
| 7 Integration und Link | Integriert; native Link-/Rückkehrprüfungen bestanden. |
| Unabhängiger Rest-Codereview | **9,4/10**, keine offenen P1/P2 im geprüften Delta. Nach dem letzten Orca-Befund vorübergehend 8,7, nach Fix und frischer Nachprüfung wieder 9,4. Windows separat 9,4; Root-Save-Reflow durch den Bibliotheksagenten mit 9,4 fremdgeprüft. |
| Abschließender unabhängiger Visualreview | **9,4/10** für den geprüften Linux-Umfang einschließlich Save-Reflow; alle 18 stabilen Abschlussbilder betrachtet. Keine schwere offene visuelle Beanstandung. |

## Reviewrunden

| Runde | Code | Darstellung | Befunde und Folgeschritt |
| --- | --- | --- | --- |
| 1 | 8,2/10 für den Fremd-Codeumfang Bibliothek/Accessibility und Windows | 8,8/10 für die betrachtete Linux-UI | Wartende GTK-Eingabebefehle können unter Auswahlfenstern Entwürfe speichern. Große Schrift lässt Thumbnailtexte überlaufen; Fontsymbol fehlt und Sortierwert wird gekürzt. Dispatcher absichern, Thumbnailzustände begrenzen, Chevron zeichnen und Auswahlwert priorisieren. |
| 2 | 9,4/10 | 9,4/10 für die betrachtete Linux-UI | Modale Eingaben, Thumbnailzustände, Auswahlwerte und Fehlerdetails korrigiert; native Prüfungen grün. Praktische Restabnahmen und gekürzte Save-Nebenaktionen dokumentiert. |
| 3 — Restarbeiten am 09.10. | Rest-Code zunächst 8,8/10, nach Korrekturen **9,4/10** | **9,4/10** für den geprüften Linux-Umfang | Display-Isolation, Fokusreihenfolge, Windows-Slotüberlauf und Accessibility-Lebenszyklus anhand neuer Belege korrigiert; 60 native Tests, zwei externe AT-SPI-Proben und 43 Wine-Tests grün. Alle 18 neuen Save-Bilder fremdgeprüft; Nebenaktionen durch neuen Seitenaufbau vollständig lesbar. |
| 4 — Echte Orca-Softwareprobe | Linux zunächst **8,7/10**, nach Fix **9,4/10**; Windows bleibt 9,4 | 9,4/10 unverändert; kein Renderdelta | Neue P1-Evidenz: unveränderte Namen erzeugen Ereignisflut und blockieren Orca-Fokusverarbeitung. Zentraler Änderungsvergleich, native Signalregression, frische 61+1+1 Fälle und gleicher Orca-Ablauf schließen den Befund. Normale App-Ansagen bis Speech Dispatcher unabhängig bestätigt; keine offenen P1/P2 im geprüften Delta. |

Die Noten gelten für den tatsächlich geprüften Änderungsumfang. Autoren nehmen
ihre eigenen Pakete nicht ab. Ein Ansatzwechsel wegen zwei stagnierender Runden
war nicht erforderlich; neue Restbefunde wurden sachlich behoben und erneut
geprüft. Der neue Save-Seitenaufbau ersetzt die frühere Kürzung der Nebenaktionen.
Die gleiche Visualnote bedeutet keine unverändert offene Kernbeanstandung:
der belegte Breitenengpass ist behoben. Am Mindestformat mit Textgröße 18 bleiben
außerhalb der Save-Karte der gemeinsame Header-Suchhinweis und der linke Hinweis
zum automatischen Speichern eng beziehungsweise teilweise angeschnitten. Dies
ist als kleiner bestehender Rand in der begrenzten Visualnote berücksichtigt.
Beide Noten liegen über 9,3; die unten genannten praktischen Grenzen bleiben.

### Lokale Prüfbelege und Wiederholung

- Frische native Auswahl nach dem Namensfix:
  `/tmp/aetherboy-private-ui-namefix-final/` und gleichnamiges `.log`;
  Komponentenbus: `/tmp/aetherboy-namefix-LinuxAccessibilityBusTests/`;
  Emulatorbus: `/tmp/aetherboy-namefix-LinuxHostAccessibilityBusTests/`.
  Die TRX-Dateien weisen alle ausgeführten Fälle ohne Skips/Inconclusive aus.
- Orca-Vorher/Nachher: `/tmp/aetherboy-orca-probe/README.md`,
  `event-comparison.json`, `before-fix-focus/`, `after-fix-focus/` und
  `after-fix-test-results/`. Eine private Xvfb-/Weston-X11-Sitzung liefert den
  Software-Seat; AetherBoy und GTK bleiben Wayland-Anwendungen. Normale
  AT-SPI-Fokus-/Aktionsaufrufe, originale Orca-Verarbeitung und stumme
  Speech-Dispatcher-Dummy-Ausgabe; persönliche Konfiguration und Audio bleiben
  unberührt. Private Testprozesse sind beendet.
- Windows-Kompatibilitätslauf:
  `/tmp/aetherboy-wine-regression/results/windows-lifecycle-final.trx` und
  `/tmp/aetherboy-wine-regression/windows-lifecycle-final.log`. Getestete
  Hauptassemblies stimmen byteweise mit dem frischen Releasebuild überein;
  Microsoft-Runtimearchive wurden gegen offizielle SHA512-Metadaten geprüft.
- Zwei zusätzliche echte Fensteraufnahmen aus dem privaten Xvfb:
  `/tmp/aetherboy-wine-regression/images/windows-main-original-de-xvfb.png` und
  `windows-main-pocket-light-en-xvfb.png`. Root hat beide unabhängig angesehen:
  fünf Slots vollständig innerhalb der Leiste, zentrale Aktionen und
  Statuswerte lesbar. Keine `DrawToBitmap`-Ansichten als Pixelbeleg verwendet;
  diese Wine-Bilder begründen weiterhin keine echte Windows-Liveabnahme.
- Stabile Save-Matrix: `/tmp/aetherboy-ui-completion-accepted/` enthält 18 Bilder
  (DE/EN × NoGame/Saved/Error × drei Fenster-/Schriftkombinationen).
  `/tmp/aetherboy-ui-completion/` enthält zusätzlich die sechs Themes, helle
  Eigenfarben und Detail-/Bibliothekszustände. Frühere Reviewbilder liegen in
  `/tmp/aetherboy-visual-round2/` und `/tmp/aetherboy-ui-work-review/`.
- Die Dateien unter `/tmp` sind temporär. Native Tests erzeugen Bilder mit
  `AETHERBOY_UI_CAPTURE_DIR` neu. Komponenten- und Host-Busprobe einzeln über
  `scripts/test-linux-headless.sh` mit dem jeweiligen Klassenfilter starten:

```bash
bash scripts/test-linux-headless.sh \
  tests/AetherBoy.DesktopTests/bin/Release/net10.0/AetherBoy.DesktopTests.dll \
  --filter 'FullyQualifiedName~LinuxAccessibilityBusTests' --report-trx

bash scripts/test-linux-headless.sh \
  tests/AetherBoy.DesktopTests/bin/Release/net10.0/AetherBoy.DesktopTests.dll \
  --filter 'FullyQualifiedName~LinuxHostAccessibilityBusTests' --report-trx
```

Weston, SDL3, GTK/AT-SPI, Python-GI, D-Bus und .NET müssen verfügbar sein. Die
Testumgebung darf für den privaten Compositor lokale Sockets anlegen. Für den
hier verwendeten portablen Weston liegt dessen Wrapper unter
`/tmp/aetherboy-weston-portable/bin`; die betreffenden Läufe ergänzten diesen
Pfad in `PATH`. Keine Isolation durch Setzen falscher Desktop-/Busmarker ersetzen.

## Verbleibende praktische Abnahmegrenzen

1. **Echtes Windows:** Keine Windows-VM vorhanden, vom Nutzer am 09.10.
   bestätigt; kein Windows-Host für diesen Auftrag eingerichtet. Dieselbe
   gezielte Auswahl auf Windows ausführen; Fensteraktionen, Slotzustände und
   Dateipicker mit echten UIA-Aktionen betrachten. Dropdowns, Rechtsklick, Scrollleisten, Fehler,
   verzögerte Zugriffe, Abbruch, lange DE/EN-Pfade, alle sechs Themes und
   Eigenfarben praktisch abnehmen. Wine und dessen Bilder ersetzen diesen
   Plattformnachweis nicht.
2. **Geräte und Desktop:** Tatsächliche Monitor-DPI-Wechsel, physischer
   Controller und reale IME-Komposition auf den betroffenen Plattformen fehlen.
   Virtuelle Controller, Texteditor-Kompositionsprüfungen und UI-Skalierung
   bleiben Softwarebelege.
3. **Screenreaderbedienung:** Externe AT-SPI- und echte Orca-Softwarepipeline
   sind geprüft. Hörqualität, physische Bedienung und ein vollständiger
   Screenreader-Nutzerdurchgang am echten Desktop bleiben offen. Orca meldet im
   privaten Probestart doppelte eigene Handler; nach Hostdispose gibt es zwei
   DEAD-Exitereignisse. Gleichzeitiges Beenden von Orca und Speech Dispatcher im
   temporären Cleanup verursachte einen fünfsekündigen Orca-Shutdown-Timeout.
   Diese Grenzen sind erhalten und nicht als fehlerfreier Screenreader-Exit
   freigegeben. Während der aktiven geprüften Appfolge keine hung-Drops.
