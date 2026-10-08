# Windows-Ausbau

Aktueller Gesamtstand und gemeinsame Schnittstellen:
[Übergabe an die Entwicklung (DE/EN)](WINDOWS_DEVELOPMENT_HANDOFF.md).

## e-Reader-Kartenbibliothek und Firmware-Forschung — 08.10.2026

Die erste Kartenbibliothek ist auf Windows und Linux implementiert:
**Einstellungen → Werkzeuge → e-Reader → Kartenbibliothek**.

- Einmal die eigene entpackte e-Reader-`.gba` auswählen. Es werden weder Nintendo-
  Firmware noch Karten mitgeliefert.
- RAW-Streifen oder kleine ZIPs für **ein** Programm importieren; weitere Karten
  über „Streifen ergänzen“ demselben Eintrag hinzufügen. Titel frei bearbeiten.
  Unterstützte Größen: 1872/2912 Byte RAW, 3520/5456 Byte gepackte Punktbilder;
  kein allgemeiner Decoder für beliebige Dateien mit Endung `.bin`, keine Fotos.
- „Kartensatz öffnen“ stellt die Streifen bereit. Beim ersten Start weiterhin
  **Scan Card** in der Firmware wählen und deren Karten-/Speicherdialog bedienen.
  Kein ROM-freier Direktstart und kein Skript, das Firmware-Menüs überspringt.
- „Stand und Bild sichern“ speichert den Fortsetzen-Stand samt Spielbild.
  „Fortsetzen“ lädt diesen Zustand einschließlich Scanner/Warteschlange;
  bereits gescannte Karten werden dabei nicht erneut eingelegt.
- Battery-Save, Schnell-Speicherplätze, Resume und Vorschau bleiben pro Kartensatz
  getrennt. Der echte Firmware-Save speichert das Kartenprogramm; ein Save-State
  kann zusätzlich den laufenden Spielzustand sichern. Nicht jedes Kartenprogramm
  besitzt eine eigene reguläre Spielstandfunktion.

Gemeinsames Modell: `nanoboy/Runtime/EReaderLibrary.cs`. Die Identität enthält die
Set-ID, den Firmware-Hash und die geordnete Streifenliste. Zwei Einträge teilen
deshalb auch bei identischer Firmware keine Speicherplätze. Umbenennen und
identische Doppelimporte behalten die Identität. Neue Streifen oder andere
Firmware erzeugen beim nächsten Öffnen einen neuen Speicherbereich; der alte
wird **nicht gelöscht**, ist aber noch nicht über eine Versionsauswahl erreichbar.
Die Firmware wird je Startidentität kopiert (bei USA jeweils 8 MiB); keine Hardlinks.

Windows: `<AetherBoy-Datenordner>/EReader/{cards,sets,firmware,launches}`;
Saves/States verwenden die bestehenden Windows-Speicherordner mit dieser Identität.
Linux verwendet dieselben Manifeste unter `<Datenordner>/ereader`. Quelldateien
bleiben unverändert. Hashprüfungen, begrenzte Archive, ein Writer-Lock und atomare
Metadatenwrites schützen den Import; ZIP-Namen werden nicht als Zielpfade benutzt.
Maximal 16 Streifen und eine verschachtelte ZIP-Ebene. Vollständigkeit, Region und
die Zugehörigkeit kurzer/langer TCG-Streifen prüft weiterhin die Firmware.

**Nachweise:** 13 neue ROM-freie Bibliothekstests je Windows/Linux bestanden;
abschließend einschließlich Sprachkatalog **32/32 je Betriebssystem**.
Windows-e-Reader-/UI-Policy-Auswahl 5/5 und native Linux-/Wayland-Seiten 2/2.
Getrennte Speicherpfade, Vorschau, Auswahlliste, Fehleranzeige und DE/EN sind erfasst.
Windows rendert alle sechs Themes, Linux dunkel/hell mit großer Schrift.
Der manuelle bisherige Scan-Ablauf von Donkey Kong-e/Balloon Fight-e wurde zusätzlich
unverändert erneut geprüft (2/2 Windows). Der vorab bestückte Bibliotheksstart
hat für Balloon Fight eine eigene visuell geprüfte Gameplay-Referenz: der frühere
Kartenzeitpunkt verändert das spätere Bild. Die alte Referenz wurde nicht ersetzt.
Der neue vollständige Ablauf bestand **2/2 auf Windows und 2/2 unter Linux**:
neun Streifen, Gameplay, wechselndes endliches PCM, 3.600 zusätzliche Frames,
fünf identische Zustandswiederholungen, Flash-Speichern und erneutes Öffnen in einer
neuen Maschine ohne Scan. 14 erzeugte Spielbilder stimmen zwischen Windows und
Linux bytegenau überein. Das ist keine pauschale Freigabe aller Kartenspiele.
Reale Spieltests und UI-Belege liegen privat unter `artifacts/ereader/library-*`.
Keine Firmware-/Kartendaten werden ins Repository übernommen.

**Noch offen:** automatische Metadaten/Komplettheitsanzeige, Import einer gesamten
Sammlung mit automatischer Gruppierung, Kartenreihenfolge/Einzelentfernung,
Versionsverwaltung alter Sets, weitere Regionen sowie manuelle Controller-, DPI-,
Screenreader- und Hörabnahme. Karten-Link zu einem zweiten GBA/Online bleibt separat.

### Read-only-Bestandsaufnahme der USA-Firmware

`scripts/inspect-ereader-firmware.ps1 -Path <eigene.gba-oder.zip>` gibt nur
Header/Hash/Byte-Statistik aus; keine ROM, Assets oder Disassemblierung werden
geschrieben. Ergebnis der bereitgestellten Datei:

| Merkmal | Beobachtung |
|---|---|
| Größe | 8.388.608 Byte = 8 MiB, etwa 8,39 MB |
| Titel / Code / Revision | `CARDE READER` / `PSAE` / 0 |
| Header-Prüfsumme | gültig |
| Erster ARM-Sprung | `0x080000C0` |
| Gleichförmiger Dateischluss | 966.108 Byte `FF`; wahrscheinlich Auffüllbereich |
| SHA-256 | `72BF37F887E896ADD1342BF95A7CFE3494A689199F878E5E1AA3072639B1B948` |

Das ist **keine vollständige Codeanalyse**. Entropie und Null-/FF-Anteile trennen
weder ausführbaren Code von Grafiken/Audio noch komprimierte von ungenutzten Daten.
Eine vollständige öffentliche, bytegenau reproduzierbare USA-Firmware-Decompilation
wurde bei dieser Recherche nicht gefunden; das beweist nicht, dass keine existiert.
„Zu fast 100 % dokumentiert“ ist ebenfalls nicht nachgewiesen.

Belastbare Einstiegspunkte:

- [GBATEK e-Reader](https://mgba-emu.github.io/gbatek/#gba-cart-e-reader-overview):
  Scanner, Flash, Formate, ARM-Programme, Software-NES und Z80-artiger Interpreter;
  API-Tabellen enthalten weiterhin unbekannte Einträge. Eine funktionierende
  Hardwareemulation benötigt noch keine vollständige Ersatzimplementierung der API.
- [nedclib](https://github.com/lambda-larry/nedclib): Dotcode-/VPK-Werkzeuge und
  Kartenformate, GPL-2.0; Lizenz vor einer möglichen Codeübernahme berücksichtigen.
- [e-reader-dev](https://github.com/AkBKukU/e-reader-dev): Homebrew-Buildablauf mit
  devkitARM, VPK, Kartenencoding und druckbaren Dotcodes. Firmwarefunktionen und
  darin vorhandene Assets werden von manchen Programmen vorausgesetzt.
- [eReader-Compression](https://github.com/HunterRDev/eReader-Compression):
  Analyse/Neubau von Kartendaten, nicht die vollständige Firmware-Decompilation.
- [Pan Docs CPU-Vergleich](https://gbdev.io/pandocs/CPU_Comparison_with_Z80.html):
  GB/GBC-SM83 ist kein vollständiger Z80. Die e-Reader-Z80-Programme laufen nicht
  nativ auf der GBA-ARM-CPU; unser GB-Core ist daher kein austauschbarer Interpreter.

**Forschungsplan, noch nicht implementiert:** erst eine eigene kleine Homebrew-Karte
und reproduzierbare API-Tests; anschließend Aufruf-/Speicherkarte der Firmware,
streng begrenzter Kartenparser/VPK, kleinster Z80-/API-Ausschnitt mit eigenen
Grafiken/Tönen; danach getrennt NES-Ausführung und ARM-Karten mit Firmwareaufrufen.
Jede Funktion mit Dokumentation und Gegenproben absichern. Eine komplette Matching-
Decompilation ist ein eigenes Ziel und keine Voraussetzung für eine unabhängige
kompatible Laufzeit innerhalb AetherBoy. Eine auf echter Hardware startbare freie
GBA-Firmware wäre nochmals ein anderes Lieferobjekt.

**Grobe Aufwandsordnung**, eine erfahrene Person in Vollzeit, keine Terminprognose:
1–2 Wochen für einen abgegrenzten Untersuchungs-/Homebrew-Einstieg; 4–12 Wochen für
einen schmalen eigenständigen API-/Interpreter-Prototyp. Breite Kompatibilität mit
NES, ARM, Audio, Saves, Regionen und eigenen Ersatzressourcen: eher viele Monate
(als Planungsrahmen 6–18+), nicht „ein paar Tage wegen 8 MB“. Eine vollständige
Matching-Decompilation lässt sich daraus nicht seriös terminieren und kann ein
mehrjähriges Community-Projekt werden. Die Zahlen müssen nach dem ersten Prototyp
an tatsächlich gemessener Abdeckung revidiert werden.

English handoff: shared per-set card library on both hosts; user-supplied firmware
is still required. Immutable launch identities isolate flash, states and previews.
The firmware inventory is read-only, not a completed decompilation. Independent
runtime research remains a separate, staged project; no Nintendo data is bundled.

## Reparatur nach der Nachprüfung — 03.10.2026

Die danach beauftragte Reihenfolge ist umgesetzt: Speicherabschluss, Fehleranzeige,
Sprach-/Layoutstellen. Ältere geprüfte Backups werden verschoben statt erneut
dauerhaft geschrieben; alle Save-Schutzmaßnahmen und Deadlines bleiben erhalten.
Speicherphasen und bereinigte innere Fehler werden erfasst. Automatische Settings-
Fehler sind jetzt nichtmodal mit Wiederholen/Details; direkter Fenster-Dispose
hält die Schreibsperre bis zum tatsächlichen Owner-Ende.
Die bestätigten Kürzungen und mehrdeutigen Texte sind korrigiert. Themes und
Customize bleiben erhalten. Acht volle Runtime-Lastprozesse ohne erneuten Timeout;
zwei abschließende Windows-Gesamtsuiten ohne Fehler. **Keine pauschale Freigabe**
für echte Hardware/Langzeitspiele; Ursache tiefer OS-I/O-Verzögerungen weiter offen.
[Reparaturen, Nachweise und Grenzen](STABILIZATION_2026-10-02.md#reparaturrunde-vom-03102026-speichern-fehleranzeige-sprache).

## Aktuelle Priorität und zurückgestellte Arbeiten — 02.10.2026

**Stabilisierungsrunde 1–3 ausgeführt:** Gesamttests auf Windows und Linux,
Fehlerfall-/Parallelprüfungen sowie gemeinsame x64-Testpakete.
[Ergebnisse, Pakete und verbleibende Grenzen (DE/EN)](STABILIZATION_2026-10-02.md).
Die vertiefte GBA-Shutdown-Untersuchung erfasst lange Wartezeiten beim dauerhaften
Schreiben der Saves. Die zusätzliche WriteThrough-Barriere wurde entfernt;
`Flush(true)`, Sicherungen und Schreibsperren bleiben erhalten. 1312 beobachtete
Shutdowns nach Änderung bestanden unter Acht-Prozess-Last. Der historische
15-Sekunden-Timeout wurde nicht identisch reproduziert und bleibt als solcher
nicht abschließend erklärt; Details im oben verlinkten Bericht.

### Anzeigesprache — lokaler Zwischenstand vom 02.10.2026

#### Vervollständigungsrunde: gemeinsamer Windows-/Linux-Stand

- **2376 DE/EN-Katalogeinträge**; weitere seltene Fehler-/Abbruchpfade,
  Controller- und Audioanzeigen, Profile, Dateifilter, Online-Diagnose,
  Spielstand-Wiederherstellung und die lokale Browserbrücke angebunden.
  Browserprotokoll, Sicherheitsprüfungen und Verbindungsdaten bleiben unverändert.
- Genrenamen, Sortierung, Richtungen sowie Sitzungs-/Recovery-/Diagnosezustände
  werden erst beim Anzeigen übersetzt. Ein neuer Regressionstest prüft insbesondere,
  dass ein Sprachwechsel weder den internen Ordner `Library` noch ROM-Titel,
  Speicherdaten oder gespeicherte Genre-IDs verändert. Ein bei der Migration
  gefundener Ordnernamenfehler wurde vor Abschluss korrigiert.
- App-eigene Meldungen sind übersetzt. Betriebssystem-/Treiberfehler bleiben
  unter einem übersetzten Hinweis „Technische Details“ im Original erhalten.
  Nutzertitel, Dateipfade, Codes, Lizenztexte und technische Produktnamen werden
  bewusst nicht umgeschrieben. Die englische Discord-Statusvorschau ist entsprechend
  beschriftet; sie ist keine Übersetzung der öffentlichen Discord-Aktivität.
- **Noch abzunehmen:** jedes Fenster und jede seltene Fehlersituation in beiden
  Sprachen, lange Texte bei hoher DPI/Textgröße, Screenreader und echte Hardware.
  Geprüfte DE/EN-Dateidialog- und Einstellungsbilder zeigen keine Überläufe in den
  getesteten Größen. Das ist keine Behauptung einer vollständigen visuellen Abnahme.
- Neue Texte müssen zentral angebunden werden; Regeln in `AGENTS.md` und
  `UI_COPY_GUIDE.md` gelten auch für neue Browsertexte. Themes/Customize bleiben.

Aktuelle Prüfnachweise unter `artifacts/localization-20261002/`:

- Release-Solution-Build ohne Warnungen/Fehler. Katalog-/Alias-/Format- und
  Browsermarkerprüfungen; vier JavaScript-Regressionsprüfungen bestanden.
- Runtime-Nachlauf mit nativen Online-Prüfungen: **643 bestanden, 1 POSIX-Skip**
  (`runtime-observed-recheck`), darunter 18 Sprachfälle. **Im vorausgehenden Lauf
  trat der GBA-Shutdown-Timeout erneut auf**; ein grüner Nachlauf ist keine Reparatur.
  Der Online-Test kann nun optional nach zwei Sekunden Owner-Stacks erfassen.
- Windows klassenweise: **42 Klassen abgeschlossen, 320 bestanden, 4 übersprungen**
  (`windows-complete-final`). Die 43. Klasse `WindowsThemeTests` hing erneut.
  Auch der isolierte Nachlauf zeigt im Stack einen modalen
  `ReportSettingsSaveFailure`-Dialog, keinen belegten Theme-Rendering-Deadlock.
  Speicherung/Callback-Isolierung bleiben zu untersuchen; Gesamtsuite nicht grün nennen.
  Die vier neuen Sprach-/Datenpfad-/Dateidialogfälle bestanden auch separat.
  Abschließender Nachlauf der geänderten Windows-Beschriftungen samt UI-Policy:
  **19 bestanden, 3 Hardware-Skips** (`windows-labels-final`). Der endgültige
  Katalog besteht separat mit **18 Sprachfällen** (`catalog-complete-final`).
- Linux nativ im privaten Weston/Wayland: **202 bestanden, 3 Audio-Skips**
  (`linux-complete-final`, nach letzten Beschriftungen erneut `linux-labels-final`).
  Keine allgemeine Hyprland-/Hardware-Freigabe dadurch.
- 251 Copy-Hinweise der großen Runde gelesen; zusätzliche kleine Nachprüfungen
  betreffen vor allem kurze Messwertzeilen, alte Quellenaliases und Systemnamen.
  Bilder unter `screenshots-complete/`; ältere veröffentlichte Testpakete bleiben
  unverändert. Kein Commit, Push oder Release.

#### Historischer Zwischenstand vor dieser Vervollständigungsrunde

Die folgenden Zahlen und damaligen offenen Übersetzungsgruppen dokumentieren den
früheren Stand. Für aktuelle Restarbeiten gilt die Liste oberhalb.

- Windows **Einstellungen → App und System → Anzeigesprache**; alternativ Suche
  nach „Sprache“ oder „language“. Deutsch, English oder Systemsprache, nach Neustart.
- Gemeinsamer DE/EN-Katalog und Resolver für Windows und Linux. Deutsche
  System-UI-Kulturen einschließlich Österreich/Schweiz → Deutsch, sonst Englisch.
  Manuelle Wahl hat Vorrang. Keine Änderung an Spielsprache, Datenkultur,
  Themes/Customize, Spielständen oder Protokollen. Die Wahl ist kein Spielprofilwert.
- Hauptfenster, Einstellungsnavigation, Bibliothek, Sofa-Modus, Datei-/Farb- und
  Eingabebauteile, Cheats, Patcher, Firmware, Barcode Boy und Aufnahmeoberflächen
  sind umfangreich angebunden. Das bedeutet **nicht**, dass jeder ihrer seltenen
  Fehler- und Nebenpfade schon vollständig übersetzt ist.
- **Offene Merkliste:** verbliebene dynamische Status- und Controllerbeschriftungen, spezielle
  Online-/Recovery-/Testdiagnose-Dialoge, Audioprüfer-Details, sichtbare Texte aus
  Profil-/Diagnose-Präsentationsklassen, Filterbeschreibungen und alte Browserbrücke
  systematisch nachziehen. Logs, Protokollkennungen und technische Fehlerdetails
  dürfen ihre stabile Diagnosesprache behalten; verständliche Nutzerhinweise nicht.
- Vollständige Fensterabnahme in beiden Sprachen, allen Themes und großen
  Text-/DPI-Stufen bleibt erforderlich. Erste deutsche/englische Einstellungs-
  Renderings sind geprüft. Keine Behauptung „alles zweisprachig fertig“.
- Migrationshilfe: `tools/AetherBoy.UiMigration`, standardmäßig nur Inventur.
  Ausgabe `artifacts/localization-20261002/remaining-literals.json` enthält auch
  technische Kennungen und ist **keine** Liste ausschließlich sichtbarer Texte.
  Bei `--apply` jede Änderung prüfen, insbesondere Positionsargumente/Tupel mit IDs.
  Katalog mit `UiTextTests` prüfen, Control-Identitäten nicht an Übersetzungen koppeln.

### Nachweise zur Sprachumstellung

- Gemeinsamer Katalog: **1516 DE/EN-Einträge**, **2068 angebundene Textstellen**
  in 91 Frontend-/Bauteildateien.
  Keine prozentuale Vollständigkeitsbehauptung: Quellinventur enthält technische
  Daten und unübersetzte sichtbare Alttexte; Katalogeinträge können mehrere Aliase haben.
- Vollständiger Release-Solution-Build nach Umstellung: **0 Warnungen, 0 Fehler**.
- Runtime vollständig mit lokalen nativen Online-Tests: **639 bestanden, 1 POSIX-
  Symlinktest unter Windows übersprungen**. Darin Sprachresolver und Katalogprüfungen;
  **14 Sprachtests** zusätzlich abschließend grün. Kein WAN-/Pokémon-Nachweis.
- Windows-Smoke abschließend **klassenweise**: alle 42 Klassen durchgelaufen,
  **327 bestanden, 4 Hardware-/Vordergrundtests übersprungen**, kein Fehlschlag
  (`windows-classes-verified`). Alte Beschriftungserwartungen sind angepasst.
  Die neue Sprachseite wird in beiden Sprachen über den tatsächlichen Menübutton
  geöffnet, gespeichert und auf stabile Control-IDs geprüft. Themes/Customize und
  UI-Policy bleiben geprüft. Unter `windows-route-final` zusätzlich 5 bestanden.
- **Offene Testisolierung:** Zwei monolithische Windows-Smoke-Läufe blieben in
  der WinForms-Nachrichtenschleife stehen. Die zweite Stackaufnahme zeigte einen
  nachgelieferten `ReportSettingsSaveFailure`-Dialog in einem späteren Theme-Test.
  Alle betroffenen Klassen laufen einzeln; das ist keine bestätigte Reparatur des
  monolithischen Testlaufs und kein neuer GBA-CPU-Deadlock-Nachweis. Diese
  Fenster-/Settings-Callback-Wechselwirkung separat weiter untersuchen.
- Linux-Desktop vollständig unter privatem Weston/Wayland: **200 bestanden,
  3 reale Audiogerätetests übersprungen**. Neue Sprachseite in Deutsch/Englisch,
  Persistenz und Ausschluss von Spielprofilen enthalten. Software-Wayland ist
  kein Nachweis sämtlicher Hyprland-/GPU-/Controller-Konfigurationen.
- Copy-Prüfung ausgeführt und **155 Hinweise gelesen**: darunter alte Quellaliases,
  gemeinsam gezählte DE/EN-Texte, kompakte Messwertzeilen und echte verbliebene
  Altdialoge. Das ist bewusst keine vollständige sprachliche Abnahme. Ein
  versehentlich deutscher Englisch-Eintrag wurde korrigiert.
- Nachlauf der gemeinsamen Bedienbauteile: zusätzliche Katalogprüfung und
  **64 Windows-Tests bestanden**, Linux erneut **200 bestanden / 3 übersprungen**.
  Die 96 Copy-Hinweise dieser Teilmenge wurden gelesen (überwiegend bereits
  geprüfte Katalogaliases/kompakte Messwertzeilen). Native SDL-Enumvergleiche und
  der Hardware-Tastenname `Select` bleiben unübersetzt; Menüaktion und Taste
  sind nicht derselbe Begriff. Die Migrationshilfe überspringt Vergleiche.
  Ein doppelt vergebenes Katalogalias wurde vor Abschluss vereinheitlicht.
  Abschließend prüfen die 14 grünen Sprachtests auch die Formatargumente sämtlicher
  Quellaliases (`catalog-alias-verified`). Linux-Nachlauf: `linux-controls-verified`.
- Ergebnisse und geprüfte Einstellungsbilder lokal unter
  `artifacts/localization-20261002/`, endgültige Windows-Bilder unter
  `screenshots-final/`. Ältere ausgelieferte Testpakete sind dadurch nicht ersetzt.
  Kein Commit oder Push.

**Neue UI-Vorgabe:** Keine neuen sichtbaren Windows-Standardcontrols, nur unsere
gemeinsamen Aether-Komponenten. Die bisherige Fundliste wurde um alle 24
Fensterklassen, zwei dynamische Dialoge und versteckte/native Nebenpfade ergänzt.
[Verbindliche Fundliste und Migration](UI_THEME_HANDOFF_2026-09-28.md#windows-standardelemente-verbindliche-fundliste--02102026).
Regel in `AGENTS.md`, automatischer Bestandswächter in `WindowsUiPolicyTests`.
**Umbaustufe 1 ist lokal implementiert:** gemeinsame Aether-Dropdowns samt
Auswahlliste, Checkboxen, Scrollcontainer/-leisten, Fokusmarkierungen sowie
Raumdialog-Buttons, GBA-Audiorahmen und Farbvorschau. Themes/Customize bleiben.
**Restlicher Bauteilumbau ebenfalls lokal implementiert:** gekapselte Eingaben
mit eigenen Bearbeitungsmenüs/Scrollleisten, eigene Tabellen/Kacheln, Datei- und
RGB-/Hex-Farbauswahl, reine Befehlsdaten statt versteckter Windows-Menüs sowie
lesbare lange Sicherheitsmeldungen. Themes/Customize und Dateischutz bleiben.
Offen bleibt die vollständige reale Interaktions-/Monitor-DPI-/IME-/Screenreader-
Abnahme aller Fenster und Nebenpfade, nicht die genannten Bauteil-Ersetzungen.

**Nutzerentscheidung:** Die vorhandene Update-Vorbereitung bleibt bestehen.
Automatische Installation, signierte Manifeste, Release-Pipeline und Rückfall-
Mechanismus aus der Phase-5.3-Merkliste sind für später zurückgestellt. Kein
sofortiger weiterer Updater-Ausbau und keine Veröffentlichung dadurch beauftragt.

Das ursprüngliche **Aether-Wave-Design** ist jetzt gezielt wiederhergestellt:
oben rechts und unten links abgeschrägte Buttons, Violett–Cyan-Verläufe,
dunkle blau-schwarze Flächen und kantige Panels. **Alle sechs Themes und die
freien Farben bleiben erhalten**, ebenso die aktuellen Funktionen, Suche,
Skalierung und Tastatur-/Controller-Bedienung. Keine Benutzerfarben zurückgesetzt.
[Umsetzung, Windows-Testbilder und ausstehende native Linux-Abnahme](UI_THEME_HANDOFF_2026-09-28.md#aether-wave-wiederhergestellt-themes-erhalten--02102026).
Kein vollständiges Zurücksetzen auf einen alten Commit. Windows wurde frisch
gebaut und die Theme-Oberfläche visuell geprüft; Linux-Darstellung bleibt vor Ort
abzunehmen. Kein Commit/Push durch diesen Umbau.

Phase 6.1 wurde am 02.10.2026 vom Nutzer präzisiert: **eigene Startanimation**, keine
Nintendo-Startsequenz oder neue Firmwareemulation. Sie ist unten implementiert.
**6.2 Barcode Boy ist jetzt in Core, Runtime und beiden Frontends implementiert.**
Protokoll-/UI-Tests sind nachgewiesen, ein Test mit einem Originalspiel noch nicht.

1. **6.1 Eigener Boot-Start:** AetherBoy-Logo und eigener Klang vor dem Öffnen
   eines Einzelspiels, abschaltbar und mit eigenem Bild/Ton anpassbar. Vorhandene
   Boot-ROM-/BIOS-Verwaltung bleibt unabhängig davon erhalten. Der Nachweis echter
   Firmware-Startsequenzen ist weiterhin ein separater Prüfpunkt, kein Ergebnis
   dieser Markenanimation. Es wird keine Nintendo-Firmware beigelegt.
2. **6.2 Barcode Boy:** Geräteprotokoll und Referenzen geprüft; gemeinsames
   Zubehörmodell mit Barcode-Texteingabe/Dateiimport in beiden Frontends umgesetzt.
   Eigener Battle-Space-Spieltest auf Nutzerwunsch auf später verschoben. Keine
   pauschale Funktion für sämtliche GB-/GBC-/GBA-Spiele versprechen; Kamera-
   Erkennung wäre ein zusätzlicher Ausbau, nicht Voraussetzung des ersten Schritts.

Phase 4 und die schon festgehaltenen Restarbeiten von Phase 1–3 bleiben zurückgestellt.
Der folgende Nachweis trennt technische Tests von der noch offenen Spielabnahme.

## Phase 6.2 — Barcode Boy, 02.10.2026

### Nachtrag: echte Battle-Space-Spielprobe am 06.10.2026

Die vom Nutzer bereitgestellte **Battle Space (Japan)**-ROM wurde isoliert mit
unserem unveränderten GB-Core auf Windows und Linux/WSL ausgeführt. ROM-SHA256:
`85B16134B866E1A1009CB0E8EEA892293D60B7592262E64C0FBA85BF73B67DBC`.
Keine Cheats, ROM-/RAM-Patches oder normale Bibliotheks-/Spielstandpfade verwendet.
Das Original-ZIP blieb nachweislich unverändert; ROM und Zustände bleiben lokal
im ignorierten Testordner, nicht im Repository.

- Scanner im Spiel erkannt, Karteneingabe erreicht.
- **Berserker `4907981000301`** und **Valkyrie `4908052808369`** erscheinen
  jeweils als richtige Figur mit unterschiedlichen Werten; beide lassen sich
  bestätigen, danach läuft das Spiel weiter.
- Bei der Kartenbestätigung „Nein“ gewählt und erneut gescannt: Valkyrie ersetzt
  Berserker korrekt; zwei abgeschlossene Scans innerhalb derselben Sitzung.
- Ein eigener Zustand vor dem Scan lässt sich für beide Karten wiederherstellen.
- Neun identische PNG-Spielbilder auf Windows und Linux/WSL. Lokale Testquelle,
  Zustände, Ereignisprotokolle und Bilder: `artifacts/barcode-live-test/`.

Dies ersetzt den früheren Stand „keine echte ROM vorhanden“ für diese Teilprobe,
nicht die vollständige Abnahme unten. Es war ein skriptgesteuerter Kern-Test,
kein vollständiger Bedienablauf im normalen Windows-/Wayland-Fenster. Offen
blieben zu diesem Zeitpunkt weitere Karten, längeres Spielen, Textdateiimport
im echten Spiel, Savestate während des Empfangs/Rewind/Trennen/Reset mit dieser
ROM sowie die native Linux-Desktop-Abnahme beim Kollegen offen. Der erweiterte
Nachweis folgt direkt unten; die übrigen Barcode-Boy-Spiele bleiben ungeprüft.

English: the supplied Battle Space ROM accepts both built-in example cards and
a repeated scan in an isolated, unmodified-core test on Windows and Linux/WSL.
All nine compared game frames match exactly. Full frontend and long-run game
acceptance remains open; the original ZIP was not changed or committed.

### Historischer Zwischenstand: erweiterte Regression, 06.10.2026

**Vor der anschließenden Stabilisierung nicht vollständig bestanden.** Die echte ROM wird jetzt durch optionale,
wiederholbare Tests geprüft, nicht nur durch die einmalige Kernprobe. Neue
Testdateien: `tests/Shared/BattleSpaceFixture.cs` und
`tests/AetherBoy.RuntimeTests/BattleSpaceBarcodeGameTests.cs`; zusätzlich echte
Spieltests in `WindowsBarcodeBoyTests` und `LinuxBarcodeBoyTests`.

| Prüfbereich | Windows | Linux/WSL bzw. WSLg |
| --- | --- | --- |
| Bestehende Barcode-Core-Protokolltests | 11 bestanden | 11 bestanden |
| Runtime einschließlich echter ROM und Wiederanlauf | 13 bestanden, 2 fehlgeschlagen | 13 bestanden, 2 fehlgeschlagen |
| Scanner-Oberfläche; Windows zusätzlich UI-Policy | 6 bestanden | 5 bestanden |

Gezählt wird jeder Fall je Plattform einmal, keine Wiederholungen zur
Fehlersuche. Linux-Runtime und Wayland-Tests wurden auch mit eigenem Linux-Build
ausgeführt. Das ist keine Abnahme auf dem Rechner des Kollegen.

Bestanden mit der unveränderten Battle-Space-ROM:

- Beide Beispielkarten über den tatsächlichen Scanner-Button der Windows- und
  Wayland-Oberfläche an eine `EmulationSession` übergeben. Nicht nur der Zähler,
  sondern das vollständige Spielbild einschließlich Figur und Werten stimmt.
- Zustand während eines teilweise übertragenen Pakets speichern und laden:
  beide Karten und der vollständige spätere Zustand sind reproduzierbar.
  Ein Frame-Ende ist nicht zwingend mitten in einem Bit; diese Bitphase deckt
  separat der bestehende Core-Test ab.
- Während des Empfangs zurückspulen und anschließend eine andere Karte scannen.
- UTF-8-Textimport mit BOM/Zeilenende; ungültige Eingabe und zweite vorgemerkte
  Karte verändern weder den ersten Transfer noch den gespeicherten Zustand.
- Scanner vor/während Empfang trennen, wieder anschließen und Spiel zurücksetzen;
  außerdem direkt während Empfang zurücksetzen: kein alter Scan wird nachgeliefert.
- Nach rund fünf emulierten Minuten Wartezeit den Spiel-Fehler mit A bestätigen,
  Scanner trennen/verbinden, wieder A und scannen: Berserker funktioniert wieder,
  **ohne Spielreset, Laden eines alten Zustands oder RAM-Patch**.

Zwei bewusst weiterhin rote Abnahmeszenarien, auf beiden Plattformen identisch:

1. **50 abwechselnde Scans ohne Scanner-Neustart:** Die ersten 17 Karten stimmen.
   Beim Rückweg zur Eingabe vor Scan 18 erscheint bereits die Aufforderung des
   Spiels, den Barcode Boy aus-/einzuschalten. Der anschließende Scan liefert
   keine Karte. Der Test bricht dort ab; die restlichen 32 Scans sind ungetestet.
2. **Nach fünf emulierten Minuten unmittelbar scannen:** Bereits vor dem Scan
   zeigt das Spiel „Error“. Der Scan allein schließt den Dialog nicht; Wiederanlauf
   über Bestätigen und Neuverbinden ist dagegen separat bestanden (siehe oben).

Der lesende CPU-I/O-Trace zeigt in beiden Fällen anschließend alle 30 erwarteten
Kartenbytes in der richtigen Reihenfolge. Nach dem 17. Scan ist im Wiedererkennen
zunächst `FF FF 10 07` zu sehen, danach wiederholt das Spiel die Abfrage und bekommt
nur noch `FF`. `BarcodeBoy.Ready` allein beweist also nicht, dass das Spiel gerade
Karten annimmt. **Noch ungeklärt:** Welche Grenze kommt vom Originalspiel
(Eingabe-Timeout/Bedienablauf) und welche von unserer Erkennung oder Zeitsteuerung?
Vor einem Core-Fix gegen Referenzemulation oder Originalhardware abgleichen.
Keine Produktionslogik und keine Timing-Konstante für grüne Tests geändert.

Ausführen: `AETHERBOY_BATTLE_SPACE_ZIP` auf das eigene ZIP setzen, optional
`AETHERBOY_BATTLE_SPACE_CAPTURE_DIR` für Bilder und begrenzte serielle Traces.
Ohne ROM-Variable werden die echten Spieltests als übersprungen gemeldet; die
synthetischen Barcode-Tests benötigen keine ROM. Die Fixture prüft die oben
genannte Revision, schreibt ausschließlich in einen eigenen temporären Ordner
und prüft abschließend die unveränderte SHA-256 des Originalarchivs. Keine ROM,
Spielstände oder kommerziellen Bilddaten werden als Testasset mitgeliefert;
im Code stehen nur Prüfsummen der zuvor visuell geprüften Kartenbilder.

Beispiel: `dotnet test --project tests/AetherBoy.RuntimeTests/AetherBoy.RuntimeTests.csproj -c Release --filter "FullyQualifiedName~BattleSpaceBarcodeGameTests|FullyQualifiedName~BarcodeBoyRuntimeTests"`.
Aktuell mit der bereitgestellten ROM **Fehlerstatus wegen der zwei offenen
Abnahmeszenarien erwartet**, nicht als grüner Gesamtnachweis verwenden.
Lokale Berichte/Bilder/Traces: `artifacts/barcode-extended/`.

English handoff: both real card scans work through Windows and Wayland controls.
Partial-packet save/restore, rewind, text import, invalid/busy input and reset
recovery pass. The two long-run acceptance scenarios still fail identically on
Windows and Linux: re-entry after 17 correct cards, and scanning without
acknowledging the game's timeout after five emulated minutes. All 30 card bytes
are received correctly. Acknowledge/power-cycle/retry recovers without a console
reset. Distinguish original-game behavior from emulator timing/state faults
before changing production code. No general compatibility or endurance claim.

### Stabilisierung und Zubehör-Nachfolge, 06.10.2026

Die beiden ursprünglichen Langzeitbefunde wurden getrennt untersucht:

1. **Erneute Erkennung nach 17 Karten:** Der interne serielle Takt begann bisher
   bei jedem SC-Start wieder bei null. Dadurch konnte der serielle Interrupt einen
   Timer-Interrupt des Spiels so unterbrechen, dass dessen alter Zählerwert den
   gerade gesetzten Empfangs-Timeout überschrieb. Der Scanner-Handshake verwendet
   jetzt die Phase des laufenden Systemteilers; ein DIV-Reset berücksichtigt die
   entsprechende Taktflanke. Referenz für dieses Verhalten:
   [SameBoy timing.c](https://github.com/LIJI32/SameBoy/blob/master/Core/timing.c)
   und [SC-Verarbeitung](https://github.com/LIJI32/SameBoy/blob/master/Core/memory.c).
   Keine übernommenen Quelltextblöcke, keine Abfrage von Spieladressen im Core.
2. **Fünf Minuten ohne Eingabe:** Der untersuchte Spielcode besitzt selbst einen
   Eingabe-Timeout. Der ursprüngliche Test verlangte zu Unrecht, dass ein Scan
   diesen Fehlerdialog ohne Bedienung schließt. Der Ersatztest verlangt den
   reproduzierbaren Originaldialog, keinen erfundenen Scan und weiteren stabilen
   Ablauf bis 15 emulierte Minuten. Ein separater Test bestätigt den Wiederanlauf:
   Fehler mit A bestätigen, Scanner trennen/verbinden, erneut A, Karte scannen.
   Weder ROM-Reset noch RAM-Patch oder vorheriger Spielstand sind dafür nötig.

**Zusätzlicher Befund durch wechselnde Scan-Zeitpunkte:** Ohne Abstand zwischen
den zwei Kartenpaketen kann die Wiederholung den ersten Empfangspuffer während
der Interruptbearbeitung überschreiben. Eine rein emulierte Pause von 14336
Basis-Dots (ca. 3,42 ms) trennt jetzt beide Pakete. Kleinere getestete Pausen ließen
den Pufferverlust bestehen, 16384 Dots konnten dagegen den Spiel-Timeout erreichen.
Die gewählte Pause ist **Kompatibilitätstiming**, keine behauptete Messung des
echten Scanners. Die Bitrate von 512 Basis-Dots bleibt ebenfalls vorläufig.
Andere Spiele und echte Hardware bleiben separat zu prüfen.

Die Pause läuft auch ohne aktiven SC-Empfang weiter, berücksichtigt Double-Speed,
wird durch erneutes SC-Armen nicht verlängert und gehört zum gespeicherten Zustand.
`BCB2` enthält die Restpause; `BCB1` bleibt lesbar und übernimmt die gespeicherte
Bitphase. Alte Programme können neue BCB2-Zustände nicht lesen. Reset/Trennen
verwerfen vorgemerkte Daten; fehlerhafte Zustände werden vor Mutation abgewiesen.

Neue Regression: alle Startphasen des normalen/schnellen internen Takts, vier
DIV-Reset-Flankenfälle, interner Halbbit-Savestate sowie Paketpause/Abbruch,
Double-Speed und BCB1-Migration. Der echte Spieltest enthält jetzt eine unveränderte
50er-Folge und **1000 abwechselnde Karten mit wechselnden Wartezeiten**. Jede Karte
wird gegen das vollständige zuvor geprüfte Spielbild verglichen, nicht nur gegen
den Übertragungszähler. Lokale Testartefakte: `artifacts/barcode-stability/`.

**Historischer Befund vom 06.10.: verbessert, noch keine unterbrechungsfreie Dauerfreigabe.**
Die feste 50er-Folge besteht auf beiden Systemen. Im verschärften 1000er-Lauf
stimmen 596 Kartenbilder; die erneute Geräteerkennung vor Karte 597 endet auf
beiden Systemen in der Aufforderung, den Scanner aus-/einzuschalten. Der Emulator
läuft dabei weiter. Der strenge Test bleibt rot und wurde nicht abgeschwächt.

Der gesicherte Repro zeigt erneut eine verschachtelte Spielroutine: Der Timer
liest `FFCC=2`, der serielle Interrupt setzt den Empfangszähler auf 5, anschließend
schreibt der unterbrochene Timer seinen alten, dekrementierten Wert 1 zurück.
Die vier Handshake-Antworten sind korrekt. Das ist ein beobachteter Ablauf,
**kein Nachweis**, dass Originalhardware denselben Fehler zeigen muss. Ein
zusätzlicher, isolierter SameBoy-Core-Vergleich mit eigenem Testadapter zeigt
ebenfalls solche verlorenen Zähleraktualisierungen; dieser Adapter ist weder
SameBoys eigene Barcode-Boy-Unterstützung noch ein vollständiger Hardwareersatz.
Referenz-Commit: `e108490dce6033b0c75d41baa0ba19436ab0d327`.

Der Windows-Repro lässt sich durch Bestätigen, Scanner trennen/verbinden und
erneutes Bestätigen ohne Konsolenreset zur korrekten Berserker-Karte zurückführen.
Gesicherter Zustand vor der Wiederverbindung, Fehlerzustand, Takttrace und Bild
liegen lokal unter `artifacts/barcode-stability/checkpoint/`. Keine Spieladressen
werden im Produktionscode abgefragt oder verändert. Ein zusätzlicher 1000er-Test
mit **explizit protokollierter Benutzer-Wiederverbindung** prüft die Erholung;
er ersetzt den strengeren Test ohne Wiederverbindung nicht.

**Wiederanlauf-Dauerlauf bestanden auf beiden Systemen:** 1000 richtige Karten
mit **fünf expliziten Scanner-Neuverbindungen**, jeweils vor Scan 597, 652, 790,
936 und 994. Kein Konsolenreset, kein Spiel-RAM-Patch, keine falsche akzeptierte
Karte und kein Emulatorabsturz. Etwa 88 Minuten emulierte Zeit pro OS; 1000
paarweise verglichene Abschlussbilder sind bytegleich. Ein fehlerfreier
unterbrechungsfreier 1000er-Lauf ist damit **nicht** nachgewiesen.

Abschluss des Testpakets: vollständiger Core **285/285 je OS**, Barcode-Runtime
einschließlich Wiederanlauf **16 bestanden / 1 fehlgeschlagen je OS**,
Windows-UI/Policy **6/6**, Linux-/Wayland-UI **5/5**. Insgesamt **613 bestanden,
2 fehlgeschlagen** in 615 Ausführungen, einschließlich der strengen Dauerlauf-
Fehler. Dies ist keine vollständige Gesamtsuite aller Emulatorfunktionen.
Belege: `artifacts/barcode-stability/results/`, `recovery-windows/` und
`recovery-linux/`. Keine Originaldatei verändert, keine ROM/Spielstände eingecheckt,
kein Commit/Push.

#### Barcode-Abschluss des bekannten Repros — 08.10.2026

Der unveränderte strenge Test mit **1000 abwechselnden Karten und wechselnden
Wartezeiten besteht jetzt auf Windows und Linux**, ebenso die feste 50er-Folge.
Jedes vollständige Kartenbild wird weiterhin geprüft. Keine Scanner-Neuverbindung,
kein ROM-Reset, kein Spiel-RAM-Patch. Die Original-ZIP bleibt unverändert.

Reparatur: Der Scanner akzeptiert eine erneute vollständige interne Erkennung
`10 07 10 07`, solange noch kein Kartenbyte übertragen wurde. Das Spiel kann so
nach seiner verlorenen Handshake-Zähleraktualisierung weiter erkennen; eine
bereits vorgemerkte Karte bleibt erhalten. Abgebrochene Transfers ändern den
Erkennungszustand nicht. **Dies ist eine bewusste Kompatibilitätserweiterung**:
Dokumentierte Hardware wartet nach erfolgreicher Erkennung auf Karten und liefert
intern `FF`. Der Fix ist kein Beleg für identisches Originalhardware-Verhalten.
Scanner-Timing und die anderen vier Spiele bleiben separat offen.

Nachweise: vollständiger GB/GBC-Core **287/287 je OS**, strenge 50-/1000-Spieltests
**2/2 je OS** unter `artifacts/barcode-retry/`. Zwei neue Protokolltests schützen
die Erkennungswiederholung mit vorgemerkter Karte/Savestate und den Transferabbruch.
Die Oberflächen melden nun „Scanner bereit“, nicht eine angeblich bestätigte
Spiel-Erkennung. Die Wiederanlauf-Tests bleiben zusätzliche Prüfungen.

#### Als nächstes vorgesehen: e-Reader (gemeinsam für Windows und Linux)

**Seit 08.10.2026 als erstes Einzelgeräte-Paket implementiert; Teilimport mit
echter USA-ROM nachgewiesen, vollständiger Spielstart noch offen.** Kein Ausbau der Barcode-Boy-13-Ziffern-
Schnittstelle und keine automatische Freischaltung in Pokémon-Spielständen.
Der e-Reader verarbeitet Dotcode-Daten über eigene Modulhardware. mGBAs
[e-Reader-Implementierung](https://github.com/mgba-emu/mgba/blob/master/src/gba/cart/ereader.c)
und [Einführung der vollständigen Emulation](https://mgba.io/2021/03/28/mgba-0.9.0/)
sind Referenzen. `EReader.cs` und `EReaderDotCode.cs` sind ausdrücklich
C#-Adaptionen unter **MPL-2.0**, mit Herkunft und Lizenz in
`third_party/mgba-ereader/`. Der Windows-Paketbau nimmt diese Hinweise und den
passenden veränderten Quellcode mit auf. Keine Firmware-/Kartendumps beigelegt.

Umgesetzt: PEAJ/PSAJ/PSAE-Erkennung, Modul-/Scannerregister, serieller Sensor,
Scanposition und Game-Pak-IRQ über emulierte Zyklen. Kartenimport für RAW-Streifen
(1872/2912 Byte) und gepackte Punktbilder (3520/5456 Byte). Keine Fotos/PNGs oder
dekodierten BIN-Karten. Besitzerthread-Befehle und eine begrenzte Warteschlange
mit 16 eigenen Kartenpuffern; ungültige Dateien ersetzen keinen laufenden Scan.
Register, Sensor, Punktebild und Warteschlange werden vollständig gespeichert
(e-Reader-Schema 7). Normale GBA-ROMs schreiben unverändert Schema 6 und lesen
Schema 5/6. Alte e-Reader-Zustände ohne Zubehörzustand werden abgelehnt; stattdessen
den Batteriespielstand laden. Kalibrierung ergänzt nur vollständig gelöschte
Flash-Sektoren, niemals vorhandene Programm-/Kalibrierdaten.

Bedienung: **Einstellungen → Werkzeuge → e-Reader**, oder Suche „e-Reader“.
Eigene e-Reader-ROM als Spiel öffnen, Karte aus Datei laden, Menü schließen und
in der e-Reader-Software scannen. „Scans verwerfen“ leert Warteschlange/aktiven
Scan, nicht gespeicherte Programme. Der Import setzt pausierte Spiele nicht fort.
Der Scan-Zähler bestätigt keine vom Spiel akzeptierte Karte. Aether-Komponenten,
DE/EN und Theme-/Customize-Farben sind auf beiden Plattformen angebunden.

ROM-freie GBA-/Zubehör-/Sprach-Regression: **288/288 je OS**. Vier unabhängig
ausgeführte unveränderte mGBA-C-Dotcode-Decoder-Fälle mit synthetischen Daten
erzeugen dieselben SHA-256-Werte wie der C#-Port. Register, FIFO/Reihenfolge,
IRQ-Zyklen, Reset, Zustandswiederherstellung, beschädigte Zustände und sichere
Ablehnung bei GB/normalen GBA-ROMs geprüft. Auch der echte Runtime-Speicherabschluss
erhält Kalibrierung über Beenden/Öffnen und lässt importierte Flashdaten unverändert;
Integritätsprüfungen verwalteter Saves bleiben aktiv. Windows-UI/Policy/Theme/Sprache:
**23/23**, Linux-Zubehör/Theme unter WSLg/Wayland: **11/11**. Screenshots und
Ergebnisse unter `artifacts/ereader/`. Native Linux-, Controller- und echte
Monitor-DPI-Abnahme bleiben eigene Prüfpunkte.

**Bereitgestellte Karten, 08.10.2026:** Donkey Kong-e (USA), Karte 1 und Balloon
Fight-e (USA), Karte 1 enthalten jeweils zwei RAW-Streifen mit 2912 Byte. Alle
vier Streifen bestehen die Decoder-, Warteschlangen- und Zustand-Roundtrip-
Vorprüfungen auf Windows/Linux (**2/2 Archivfälle je OS**); RAW-/Punktebild-
SHA-256-Werte sind plattformgleich. `ProvidedEReaderCardTests` liest die Original-
ZIPs ausschließlich schreibgeschützt und prüft deren unveränderte Prüfsumme.
Variablen: `AETHERBOY_DONKEY_KONG_CARD1_ZIP` und
`AETHERBOY_BALLOON_FIGHT_CARD1_ZIP`; ohne Dateien sind die Tests bewusst optional.
Keine Kartendaten eingecheckt. Belege: `artifacts/ereader/provided-cards-windows/`
und `provided-cards-linux/`. Diese Vorprüfungen allein bestätigen weder Fehlerkorrektur/Akzeptanz durch
die echte Firmware noch den Spielstart. Zu diesem Zeitpunkt lagen nur Teilkartensätze vor;
die spätere vollständige Sammlung und Spielabnahme sind unten dokumentiert.

**Firmware-Abnahme, 08.10.2026:** Die inzwischen bereitgestellte **e-Reader
(USA)** bootet mit unserem HLE-BIOS. Beide RAW-Streifen von Karte 1 werden bei
Donkey Kong-e und Balloon Fight-e tatsächlich von der Software akzeptiert:
Anwendungsname stimmt, Anzeige wechselt von acht auf sieben fehlende Dotcodes
von insgesamt neun. Das ist ein erfolgreicher Teilimport, **noch kein NES-Spielstart**.
Neue optionale `ProvidedEReaderFirmwareTests` verwenden zusätzlich
`AETHERBOY_EREADER_ROM_ZIP`; keine ROM-/Kartendaten im Repository.

Im echten Ablauf gefunden und im gemeinsamen Kern behoben: Nach einem bereits
beendeten Scan blockierte ein leerer Punktebildpuffer später eingelegte Karten.
Ein wartender Scanner prüft jetzt weitere Einfügungen und beginnt den neuen
Streifen an einer definierten Scanposition. Der ROM-freie Regressionstest wurde
zuerst rot nachgewiesen. Weitere Prüfungen: spätes Einlegen, Import vor Scanstart,
20 Wiederholungen aus einem Zustand mitten im Scan je Kartensatz sowie doppelte
Karte ohne zusätzlichen Fortschritt. Der vollständige Zustand wird zwischen
identisch wiederhergestellten Läufen verglichen. Das ist kein Vergleich mit der
ununterbrochenen Host-Zeitleiste: Restore setzt die automatische Rewind-Taktung
zurück, deren Zustandserfassung die CPU bis zur Instruktionsgrenze weiterschaltet.
Bildbelege und Ergebnisse unter `artifacts/ereader/firmware-windows/` und
`firmware-linux/`; separate Erkundung unter `artifacts/ereader-live/`.
Abschließende GBA-/Zubehör-/Sprachprüfung einschließlich echter Firmware- und
Kartenfälle: **293/293 auf Windows und 293/293 unter Linux/WSL**, keine ausgelassenen
Fälle in dieser Auswahl. Vier akzeptierte Streifen und die Bilder nach Restore
sind plattformgleich. Originalarchive unverändert. Manuelle Zusatzprobe unter
Windows: Balloon-Fight-Streifen in der laufenden Donkey-Kong-Sammlung zeigt die
Warnung der Firmware vor dem Verwerfen des bisher eingelesenen Programms;
Bestätigung wurde nicht ausgeführt. Kein kompletter Kartensatz-/Speichertest.
Aktualisierte UI-Texte: Windows-e-Reader/Policy/Theme/Sprache **34/34** und
Linux-e-Reader/Theme unter WSLg/Wayland **6/6**, Importbestätigung und Fehlerfall
erneut auf Deutsch/Englisch und mit dunklen/hellen Farben gerendert und geprüft.

**Vollständige Sammlung und Standalone-Abnahme, 08.10.2026:** Die nachgereichte
Sammlung enthält 3.187 verschachtelte ZIPs mit 4.458 Nutzdateien: 3.996 strukturell
dekodierbare RAW-Streifen (1.872 bzw. 2.912 Byte), 461 Flash-Abbilder mit je 128 KiB
und einen abgelehnten, um ein Byte verkürzten Tom-Nook-Streifen (2.911 Byte).
Flash-Abbilder sind keine einzuscannenden Karten. Eine erfolgreiche Decoderprüfung
aller RAW-Dateien ist **keine** vollständige Spiel-/Regionsabnahme.

Zwei im echten NES-e-Ablauf nachgewiesene Fehler des gemeinsamen GBA-Kerns behoben:

- ARM-MSR schreibt nur ausgewählte CPSR-/SPSR-Felder. Ein Flags-Schreibzugriff im
  NES-Audiomischer hatte IRQs versehentlich freigegeben, dadurch den Rücksprung
  überschrieben und schließlich Code im RAM zerstört. 14 ROM-freie Tests sichern
  Feldmasken, Privilegien, Registerbänke und IRQ-Rückkehr ab; die ersten elf Fälle
  wurden vor dem Fix rot nachgewiesen.
- Sound-FIFO-DMA1/2 überträgt unabhängig vom Größenbit vier 32-Bit-Wörter und fordert
  ab 16 verbleibenden Bytes Nachschub an. Ein laufender Transfer wird nicht erneut
  gestartet. Sechs ROM-freie Tests; drei schlugen vor dem Fix fehl. Damit liefert
  Balloon Fight nach zuvor konstant null nun wechselnde PCM-Audiosamples.
  Referenz: [GBATEK Sound DMA](https://mgba-emu.github.io/gbatek/#sound-dma-fifo-timing-mode-dma1-and-dma2-only).

`ProvidedEReaderCollectionTests` verwendet zusätzlich
`AETHERBOY_EREADER_COLLECTION_ZIP` (Originalarchiv ausschließlich schreibgeschützt):
vollständige Neun-Streifen-Sets von **Donkey Kong-e und Balloon Fight-e**, sichtbare
Titel-/Spielbilder, Eingaben, 3.600 zusätzliche Frames, wechselndes endliches PCM,
fünf vollständige Zustandswiederholungen. Anschließend im Firmware-Dialog ausdrücklich
**YES** auswählen (Standard ist das blinkende NO), Flash speichern und in einer
neuen Maschine **Access saved data** aufrufen, ohne Karten neu einzulesen.
Save-Datei bleibt beim Abspielen unverändert; Originalarchive bleiben unangetastet.
Kirby Slide Puzzle, Manhole und Air Hockey haben weitere echte Firmware-
Regressionen mit geprüften Spielbildern und je drei Zustandswiederholungen.
Screenshots werden optional über `AETHERBOY_EREADER_TEST_OUTPUT` abgelegt.

Zusätzliche gleiche Abläufe auf Windows und Linux/WSL: alle weiteren elf USA-NES-e-
Sets eingelesen und gestartet — Baseball, Clu Clu Land, Donkey Kong 3, Donkey Kong Jr.,
Excitebike, Golf, Ice Climber, Mario Bros., Pinball, Tennis, Urban Champion.
Excitebike/Tennis benötigen nach der ersten Starttaste eine zweite im Auswahlmenü;
der anschließende Renn-/Tennisbildschirm wurde zusätzlich geprüft.
Standalone: Kirby Slide Puzzle, Manhole Old-e (E3), Air Hockey-e; Celebi-Animation,
Ho-oh-/Rapidash-/Suicune-Infoansichten sowie Kirby Contest Card (Loser) mit
erwarteter Nichtgewinn-Meldung. Bei Celebi ist der lange Streifen ein eigenes
Programm; der kurze bleibt im gemischten Warteschlangenversuch übrig. Nicht alle
langen/kurzen Streifen einer TCG-Karte gehören zu einem gemeinsamen Kartensatz.
Die 19 zusätzlichen Abläufe ergeben **978 identische Bild-/Zustandsdateien** auf
Windows und Linux/WSL; dies sind Start-/Kurzproben, keine durchgespielten Titel.
GBA-/e-Reader-Testauswahl mit bereitgestellten Dateien: **315/315 auf Windows und
315/315 unter Linux/WSL**, keine übersprungenen Fälle. Beide Frontends bauen in
Release ohne Warnungen/Fehler. Die gemeinsamen Fixes betreffen beide Plattformen;
der Linux-Nachweis hier ist WSL, keine erneute native Geräteabnahme beim Kollegen.
Lokale Belege: `artifacts/ereader/matrix-windows-final/`, `matrix-linux/`,
`collection-regression-windows-final/`, `collection-regression-linux/`.

**Breiterer Programmtest, 08.10.2026 (zusätzlicher Durchlauf):**
`ProvidedEReaderExtendedTests` ergänzt 50 optionale Fälle mit denselben beiden
privaten Fixture-Archiven. Keine ROM-, Karten- oder Flash-Daten sind Bestandteil
der Tests im Repository. Die 49 Fälle der Hauptmatrix und vier vertiefte Läufe
(ein neuer Scan-Fall, drei längere Wiederholungen) bestanden auf **Windows 53/53**
und **Linux/WSL 53/53**, jeweils ohne übersprungene Tests. Dies ist zusätzlich zur
oben dokumentierten 315er-Auswahl, kein erneuter gemeinsamer 365er-Testlauf.

| Gruppe | Umfang | Tatsächlich geprüfter Einstieg |
|---|---:|---|
| Mario Party-e | 11 Kartenprogramme | Je beide RAW-Streifen eingescannt; zusätzlich das unabhängige mitgelieferte Flash-Abbild gestartet |
| Pokémon-e TCG | 23 als Minispiele bezeichnete Programme | Mitgeliefertes Flash-Abbild; **kein** vollständiger RAW-Scan-Nachweis für diese Gruppe |
| Pokémon-e TCG | 8 Animationen und 7 Hilfsprogramme | Mitgeliefertes Flash-Abbild, Eingaben, Zustandswiederholung und erneutes Laden |
| Machop At Work | 1 zusätzlicher Scan-Ablauf | Lange Streifen von Machop, Machoke und Machamp nacheinander, ohne eingespritzten Spielstand |

Mario Party-e: Big Boo, Bowser, Daisy, Graceful Princess Peach, Lakitu,
Princess Peach, Super Waluigi, Super Wario, Waluigi, Wario und Yoshi. Nicht jedes
dieser Kartenprogramme ist ein eigenständiges Geschicklichkeitsspiel; darunter
sind auch Zufalls-/Brettspielaktionen.

Pokémon-Minispiele laut Sammlungsbezeichnung:

- Aquapolis: Dream Eater, Harvest Time, Jumping Doduo, Mighty Tyranitar,
  Punching Bags, Rolling Voltorb, Sneak and Snatch.
- Expedition: Diving Corsola, Flower Power, Go, Poliwrath!, Hold Down Hoppip,
  Kingler's Day, Machop At Work.
- Skyridge: Berry Tree, Ditto Leapfrog, Follow Hoothoot, Haunter, Leek Game,
  Night Flight, Pika Pop, Ride the Tuft, Teddiursa, Watch Out!.

Die vollständigen Animations-/Hilfsprogramm-Namen stehen in der Testdatenliste;
darunter Coin Flipper, beide Duel Timer und vier Poké-Power-Anwendungen.
Alle 49 Startbilder wurden zusätzlich betrachtet. Die Tests prüfen endliche
PCM-Samples, vollständige Zustandswiederholung (je fünfmal), Öffnen in einer neuen
Maschine ohne Scan sowie unveränderte Quelldateien. Je Hauptfall kommen 1.800
Frames mit wechselnden Eingaben hinzu. Big Boo, Pika Pop und Coin Flipper laufen
zusätzlich mit jeweils **18.000 weiteren Frames** (rund fünf Minuten emuliert).
Das sind Stabilitätsläufe einschließlich Menüs, Wiederanläufen und Game-over-
Bildschirmen, keine fünf Minuten garantiert ununterbrochener Spielprogression.

Der echte Machop-Scan zeigt den Kartenfortschritt 2 → 1 → vollständig, startet das
Spiel und reagiert sichtbar auf Rechts-Eingabe. Ein geprüftes Spielbild ist als
Regression hinterlegt; fünf Zustandswiederholungen und weitere 1.800 Frames folgen.
Separate Erkundungen bei Hoppip/Hoothoot lesen bislang nur den ersten Streifen:
die Firmware verlangt korrekt weitere Karten. Diese Teilimporte gelten **nicht**
als vollständige Scan-Abnahme; deren Flash-Programme wurden unabhängig geprüft.

Plattformvergleich über Haupt- und vertiefte Läufe: **801 identische PNG-Paare**
und **697 identische Messpunkt-Paare** mit vollständigem Zustands- und Audio-Hash,
Samplezahl/-bereich sowie Diagnosen. Insgesamt 327.644 ausgeführte Frames je OS
(inklusive Boot, Restore-Wiederholungen und Neustart, also keine reine Spielzeit).
Kein Absturz, fehlgeschlagener Replay oder neuer Kernfehler in dieser Auswahl.
Die Mario-Karten starten direkt; die anfängliche Testannahme eines NES-artigen
Speicherdialogs wurde im Test korrigiert, nicht durch eine Änderung am Emulator.
Die unabhängigen Mario-Flash-Abbilder beweisen keinen Speichervorgang nach Scan.
Keine Produktcode-Änderung in diesem erweiterten Testpaket; UI-Policy zusätzlich 3/3.

Optionale Belege über `AETHERBOY_EREADER_EXTENDED_OUTPUT`;
`AETHERBOY_EREADER_SOAK_FRAMES` wählt 1.800 bis 18.000 Zusatzframes.
Lokale Ergebnisse: `artifacts/ereader/extended-windows/`, `extended-linux/`,
`deep-windows/`, `deep-linux/` und `extended-comparison.json`.
Builds ohne Warnungen/Fehler. Für diese Prüfung wurde keine weitere Spiel-ROM
benötigt. Japanische Firmware, externe Spielverbindungen, physischer Vergleich,
Hörprobe und manuelles Durchspielen bleiben getrennte, offene Abnahmen.

**Noch offen:** Hörprobe über echte Audioausgabe, längere manuelle Spielsitzungen,
weitere Karten/Regionen und physischer Hardwarevergleich. Für japanische Karten
wäre die passende **Card e-Reader(+)-ROM (Japan)** nötig; für die oben genannten
USA-Standalone-Fälle werden keine weiteren ROMs benötigt. Keine gebündelten
Nintendo-Daten. Der Diagnose-Runner liest verschachtelte Archive; die normale
Oberfläche ist damit noch **kein Importer für die gesamte Sammlung**.
Kartenverwaltung mit Namen/Einzelentfernung und
Routing zum zweiten lokalen GBA ergänzen. **Pokémon-Übertragung und Online Link
sind nicht angebunden oder nachgewiesen.** Die ursprüngliche Reihenfolge bleibt
die Abnahmeliste; Implementierung ist nicht mit Spielabnahme gleichzusetzen:

1. **Bestandsaufnahme im GBA-Core:** Bus-/Moduladressen, Scannerregister,
   Kartenspeicher, Reset und Flash-Verhalten gegen Dokumentation und mGBA prüfen.
   `GamePak`, Flash und Scheduler sind inzwischen mit dem e-Reader verbunden;
   USA-Firmware führt die oben geprüften vollständigen Kartensätze aus; andere
   Regionen und ein physischer Hardwarevergleich bleiben offen.
2. **Begrenzter Kartenimport:** zuerst dokumentierte digitale Rohkartendateien,
   Größen-/Formatprüfung, kontrollierte Warteschlange und eindeutiger Abbruch.
   Keine Kamera-/OCR-Erkennung als Voraussetzung. Keine fremden ROMs/Karten bündeln.
3. **Eigenes Zubehörmodell im gemeinsamen Core:** Scanner-/Registerzustände und
   emulierte Scan-Zeit, sichere Ausführung auf dem Besitzerthread; keine direkten
   UI-Schreibzugriffe auf Spiel-RAM. e-Reader-Programm als vom Nutzer geladene ROM.
4. **Zustand und Datenhaltbarkeit:** aktive Karte, Scanposition, Warteschlange,
   Register und Speicher vollständig sichern/wiederherstellen. Reset, Rewind,
   Abbruch, beschädigte Datei und getrennte Saves von Anfang an testen.
5. **Beide Oberflächen gleichzeitig:** eigene Aether-Dateiauswahl/Kartenliste,
   Scannen/Abbrechen/Status, DE/EN, Themes/Customize, Tastatur und Controller.
6. **Zunächst Einzelgerät:** mit eigener Homebrew-Testkarte und dann einer
   bereitgestellten e-Reader-ROM eine tatsächlich ausgeführte Karte nachweisen.
7. **Danach zwei lokale GBA:** e-Reader in einer Instanz, kompatibles Zielspiel in
   der anderen; erst ein nachweisbarer Kartentransfer gilt als Erfolg. Unterstützte
   Region, e-Reader-Variante und Spielrevision einzeln dokumentieren. Kein pauschales
   Versprechen für deutsche Pokémon-Versionen. Netzwerk ist ein späterer Nachweis.
8. **Abnahme Windows und Linux:** gleiche Testdaten und Endzustände, lokale
   Originalspielstände unangetastet. Bildscan-Konverter erst nach stabilem Import.

#### Weitere Folgerungen aus der Zubehör-Recherche

- **GBC-Infrarot:** eigenes Folgepaket für Card Pop/Mystery Gift über das IR-Port-
  Modell, nicht über SB/SC oder den Barcode-Dialog. Zunächst zwei lokale CGBs;
  Netzübertragung später separat. Eine GBA-Prototyp-Konstante namens `IR` in
  `IORegs.cs` ist keine vorhandene CGB-IR-Implementierung.
- **Rumble:** MBC5-Signal und Ausgabe über Windows-Gamepad bzw. Linux-SDL sind
  vorhanden. Echte Controller-Abnahme bleibt wichtig; das ist keine allgemeine
  GBA-Rumble-Zusage.
- **Solarsensor:** Boktai wäre ein separates GBA-Modul-/GPIO-Projekt; vorhandenes
  `GpioRtc` ist nur RTC, noch kein Lichtsensor. Nicht mit e-Reader oder Barcode
  Boy vermischen. Andere Sensoren sind vorerst zurückgestellt.
- Die eingefügte Recherche nennt falsche/vermischte Spieletitel. Die fünf in
  [Dan Docs](https://shonumi.github.io/dandocs.html) dokumentierten Barcode-Boy-
  Titel sind Battle Space, Monster Maker: Barcode Saga, Kattobi Road, Family
  Jockey 2 und Famista 3. Bundle-Namen sind keine zusätzlichen Spiele. Es gibt
  dokumentierte Karten mit anderen Präfixen als 49; deshalb keine 49-Sperre,
  keine automatische Prüfzifferkorrektur. Was ein Code erzeugt, entscheidet das
  jeweilige Spiel, nicht ein universeller Zufallsalgorithmus des Zubehörs.

### Gemeinsamer Lieferstand / shared implementation

- Neues Namcot-Zubehör in `Core/BarcodeBoy.cs`, angebunden an SB/SC und die
  emulierte serielle Uhr. Kein Netzwerk, keine Wall-Clock-Sleeps, keine Manipulation
  spielspezifischer RAM-Adressen und keine zweite Linux-Logik.
- Geräteerkennung: GB sendet `10 07 10 07` hex mit internem Takt; Scanner antwortet
  `FF FF 10 07`. Im Empfangsmodus treibt der Scanner den externen Takt und sendet
  `02` + 13 ASCII-Ziffern + `03` zweimal, insgesamt 30 Bytes. Danach ist eine neue
  Geräteerkennung nötig. Kein Transfer ohne aktives SC-Startbit.
- Ein vorgemerkter Scan; ungültige Eingaben und ein zweiter wartender Scan werden
  abgelehnt, nicht still ersetzt. Textimport: UTF-8, optional BOM/äußere Leerzeichen,
  maximal 128 Byte, genau ein Code. Ziffern werden nicht automatisch korrigiert.
- Verbindung, Handshake, Warteschlange und Bitphase werden in Savestates und
  Rewind gespeichert. Alte Zustände ohne Zubehör trennen den Scanner; neue
  Scannerzustände funktionieren nicht in älteren Builds. Anschließen/Trennen
  leert den bisherigen Rewind-Verlauf. Reset behält die Verbindung, setzt aber
  Erkennung und Warteschlange zurück. Neue Spiele starten ohne Scanner, außer
  ein gespeicherter Zustand stellt ihn wieder her.
- Runtime-Befehle laufen auf dem Emulations-Owner. Snapshots liefern Erkennung,
  Warteschlange, Bytefortschritt und Transferzahl. GBA und Online-Link-Wrapper
  unterstützen diese Befehle nicht; ein vorhandenes Kabel darf nicht ersetzt werden.
- Windows: **Einstellungen → Werkzeuge → Barcode Boy öffnen**, alternativ Suche
  „Barcode“. Aether-Buttons, Eingabe, Scrollbereich und eigene Dateiauswahl.
- Linux: **Settings → Tools → Open Barcode Boy**, alternativ dieselbe Suche.
  SDL-Eingabe mit Zwischenablage/Controller-Navigation und bestehender asynchroner
  Portal-Dateiauswahl. Themes und freie Farben bleiben auf beiden Systemen erhalten.
- Beide: anschließen/trennen, manuelle Eingabe, Textdatei, Scan, Status und zwei
  Beispielkarten. Beispielbuttons füllen nur das Feld. Ein vorgemerkter Scan wartet
  auf das fortgesetzte Spiel; kein erzwungenes Entpausieren.

### Geeignetes Spiel und Referenzen

**Battle Space (Japan, Game Boy)** ist der erste geplante Spieltest: Es benötigt
den Namcot Barcode Boy. Weitere dokumentierte Titel: **Monster Maker: Barcode
Saga, Kattobi Road, Family Jockey 2 und Famista 3**. Das ist die historische
Zubehörliste, **keine getestete AetherBoy-Kompatibilitätsliste**. Es sind GB-Titel;
unser GB/GBC-Core kann den Scanner in beiden Hardwaremodi verwenden. **Barcode
Taisen Bardigun** nutzt ein anderes Gerät. GBA **e-Reader**, Kamera- und Bild-/
Barcodeerkennung sind nicht Bestandteil dieses Pakets.

Hardwareprotokoll und Beispielcodes: ausdrücklich gemeinfreie
[Dan Docs](https://shonumi.github.io/dandocs.html), Abschnitt Barcode Boy.
Weitere Referenzen: [Hardwarebericht](https://shonumi.github.io/articles/art7.html)
und [GBE+ Serial-Code](https://github.com/shonumi/gbe-plus/blob/master/src/dmg/sio.cpp).
Die AetherBoy-Implementierung ist neu geschrieben; kein GBE+-Code übernommen.
Beispielkarten: **Berserker `4907981000301`**, **Valkyrie `4908052808369`**.

### Nachweise und offene Merkliste

**Historische Zurückstellung vom 02.10.2026:** Der echte Spieltest war zunächst
vertagt. Mit der am 06.10.2026 bereitgestellten ROM wurde er wieder aufgenommen.
Die inzwischen bestandenen Teilprüfungen und offenen Dauerlauf-Grenzen stehen
oben; eine vollständige Spiele-/Hardwarefreigabe bleibt ausdrücklich offen.

- [ ] **Battle Space (Japan, GB) auf Windows und nativem Linux testen:**
  Geräteerkennung, Berserker-/Valkyrie-Karten im Spiel, wiederholte Scans,
  Textdateiimport, Savestate/Rewind und Trennen/Reset nach dem Testplan unten.
- [x] Teilnachweise mit echter ROM auf Windows und Linux/WSLg: beide Karten,
  Scanner-Buttons, Textimport, Savestate mitten im Paket, Rewind und Reset.
- [x] Wartezeit eingegrenzt: originalen Spiel-Timeout und 15 Minuten weiterlaufenden
  Emulator nachgewiesen; Wiederanlauf ohne ROM-Reset separat bestanden.
- [x] Interner DIV-bezogener Takt, DIV-Reset und gespeicherte Paketpause ergänzt;
  feste 50er-Folge auf beiden Systemen bestanden.
- [x] Unterbrechungsfreie Dauerfolge: 50 und 1000 Karten je OS bestanden, mit
  dokumentierter Erkennungs-Kompatibilitätserweiterung vom 08.10.2026. Kein
  Spiel-RAM-Patch, Scanner-Neuverbinden oder stiller Konsolenreset.
- [ ] Scanner-Timing und die übrigen vier dokumentierten Spiele gesondert prüfen;
  synthetische Tests nicht als allgemeine Spielekompatibilität ausgeben.

- 11 neue Core-Fälle: Handshake, Pakete, IRQ/SC-Zeitpunkt, DMG/CGB-Fast-/Double-
  Speed, Bereitschaft, Abbruch, ungültiger/besetzter Scan, Reset, alter Zustand,
  atomare Ablehnung beschädigter Zustände, Kabelsperre und Rewind.
- 4 Runtime-Fälle: eigenes GB-Testprogramm (DMG und CGB), das Erkennung und beide
  Pakete durch echte CPU-Instruktionen ausführt; begrenzter Textimport und sichere
  Ablehnung der Scannerbefehle bei GBA. Keine kommerziellen ROM-Daten verwendet.
- Windows-Scanner-UI mit echter pausierter Testsitzung geprüft. Vollständige
  Smoke-Reihe: 325 bestanden, 4 hardware-/foregroundabhängige Fälle übersprungen.
  Eigene Komponenten bestehen auch den Windows-UI-Policy-Wächter.
- Linux: Scanner-Seite unter **WSLg/Wayland** bedient und gerendert; Suche,
  Eingabe, Anschluss, Scan, Trennen und Zurück geprüft. Das gemeinsame Testprogramm
  läuft auch unter Linux. Das ersetzt keine Abnahme am Rechner des Kollegen.
- Screenshots: `artifacts/barcode-boy-20261002/`. Beide Oberflächen verwenden das
  neue Logo aus demselben Markenasset. Kein Commit/Push in diesem Arbeitspaket.
- **Offen:** längeres freies Spielen, physischer Hardware-/Timingvergleich und
  die anderen vier Spiele. Der bekannte 597er-Repro und die strengen Dauerläufe
  bestehen seit 08.10.2026. Keine kommerzielle ROM heruntergeladen/beigelegt.
- **Timinggrenze:** 512 Basis-Dots pro Scanner-Bit ist die gewählte anfängliche
  Emulationsrate, keine Messung des Original-Scanneroszillators. Reale Gerätequirks
  und die Abfragen/Timeouts der fünf Spiele müssen gegen echte Software geprüft werden.

### Erster Spieltest auf beiden Plattformen

1. Eigene **Battle Space (Japan)**-ROM öffnen; ROM-Prüfsumme und Build festhalten.
2. Scanner über die Werkzeuge anschließen. Falls das Spiel schon mit „kein Gerät“
   wartet, anschließend zurücksetzen; die Verbindung bleibt dabei bestehen.
3. Spiel bis zur Karteneingabe fortsetzen. Im Menü muss der Scanner als erkannt
   erscheinen. Andernfalls Bild und Zustand sichern, keine Spiel-RAM-Patches nutzen.
4. Berserker-Code einsetzen, **Code scannen**, Menü schließen und Spiel fortsetzen.
   Nicht nur den Zähler prüfen: Das Spiel muss die passende Karte anzeigen.
5. Mit Valkyrie wiederholen; neue Erkennung und beide Pakete prüfen. Kein
   ungewollter Mehrfachscan. Textdatei statt manueller Eingabe ebenfalls testen.
6. Savestate vor/während Empfang, Laden, Rewind, Abbruch, Trennen/Anschließen,
   Reset und ungültige Eingaben separat prüfen.
7. Windows und natives Linux getrennt protokollieren; danach die übrigen Spiele
   und Originalkarten. Bis dahin keine abgeschlossene Spielabnahme behaupten.

English handoff: One shared Barcode Boy serial device and equivalent Windows/Linux
controls are implemented. Synthetic CPU programs and UI integration are tested.
Battle Space passes strict 50/1000-card runs on both platforms with the documented
re-detection compatibility extension. Hardware timing and other games remain
unverified. Bardigun is not included. The separate GBA e-Reader peripheral and
RAW-card UI are implemented; real firmware/card acceptance and transfer to another
GBA are still pending. See the 2026-10-08 handoff above.

## Phase 6.1 — Eigene Startanimation, 02.10.2026

**Für Windows und Linux lokal implementiert; kein Commit/Push.** Kein zusätzliches
Intro beim reinen Programmstart. Vor dem Öffnen eines Einzelspiels (GB/GBC/GBA)
erscheint standardmäßig für **2,4 Sekunden** das vorhandene AetherBoy-Logo mit
Einblenden/Einrasten und einem neu synthetisierten Vierklang. Kein Nintendo-Bild,
keine kopierte Aufnahme und kein Eingriff in emulierte Hardware oder Spielstände.
Ein vorhandenes Einzelspiel pausiert währenddessen. Reset, Schnellladen,
Wiederherstellung und laufende Link-Sitzungen bekommen keine zusätzliche Wartezeit.
Verbindungstests und Headless-/versteckte Teststarts laufen ohne automatisches Intro.

Windows: **Einstellungen → App und System → Bedienung → Startanimation**; Suche
`Intro`, `Logo` oder `Startanimation`. Linux: **Settings → App & files → Firmware →
Logo and start animation**, ebenfalls über die Suche. Beide bieten:

- Intro an/aus, Intro-Ton separat an/aus, Vorschau und Rückkehr zu AetherBoy-Bild/Ton.
- Kein sichtbarer Überspringen-Button. Überspringen per Enter, Leertaste oder Escape; beim tatsächlichen
  Windows-Spielstart auch untere Controller-Aktionstaste, unter Linux untere/rechte
  Aktionstaste. Überspringen startet das gewählte Spiel, es ist kein Ladeabbruch.
- Eigenes **PNG**, maximal 2048 × 2048 Pixel / 4 MiB; Seitenverhältnis bleibt
  erhalten. **WAV**: 16-Bit-PCM, mono/stereo, 8–48 kHz, höchstens 2,4 Sekunden.
  Kein MP3-, Video-, GIF- oder SVG-Import in dieser Stufe.
- Ton respektiert Spiel-Stummschaltung und Lautstärke. Kein Ton aus dem weiterlaufenden
  bisherigen Spiel hinter der Animation. Intro-Audio benutzt einen getrennten,
  begrenzten Ausgabepfad und wird beim Überspringen/Schließen beendet.

`BootIntroStore` und `BootIntroFrame` sind gemeinsam; WinForms-/SDL-Rendering und
Audioausgabe bleiben Frontend-Aufgaben. Eigene Dateien werden **kopiert**, nicht
verschoben: Windows `%LOCALAPPDATA%/AetherBoy/BootIntro`, Linux
`$XDG_DATA_HOME/aetherboy/BootIntro` (Fallback `~/.local/share/aetherboy/BootIntro`).
Portable Mode folgt dem bestehenden Datenprofil. `intro.json` speichert globale
Optionen und relative Hash-Dateinamen, keine fremden absoluten Quellpfade. Import
prüft vor dem Auswählen, publiziert vollständige Kopien und lässt Originale bestehen.
Fehlende/beschädigte eigene Medien fallen auf AetherBoy zurück. Rücksetzen der
Medien löscht keine importierten Dateien; alte Kopien bleiben vorerst erhalten.

### Nachweise und Merkliste zur Startanimation

- Logo-Nachtrag 02.10.: Das vom Nutzer fertig gelieferte AB-Logo ersetzt die alte
  Bildmarke auf Windows und Linux einschließlich Programmsymbolen. Original:
  `branding/aetherboy-logo-2026-10-02.jpg`; nur Größen-/Formatkonvertierung, kein
  neuer Entwurf. Intro ohne zusätzlichen Schriftzug und ohne Überspringen-Button.
  Eigene Intro-Dateien sowie Theme-/Farbeinstellungen werden nicht überschrieben.
- Beide Frontends und Tests mit 0 Warnungen/0 Fehlern gebaut. Acht gemeinsame
  Intro-Tests prüfen PCM, Dauer, Größen, Pfade, Kopien, Einstellungen und Animation.
- Windows: Renderings aller sechs Themes, Settings-Seite, PNG-Import/Fallback,
  Überspringen, Abbruch/Schließen und Pause/Fortsetzen einer synthetischen Spielsitzung.
  Bilder: `artifacts/boot-intro-20261002/`.
- Abschließender vollständiger Windows-Smoke-Testlauf: **328 insgesamt,
  324 bestanden, 4 umgebungsabhängig übersprungen, 0 fehlgeschlagen**. Der
  UI-Bestandswächter bleibt unverändert aktiv; keine neue native UI-Ausnahme.
- Drei neue Linux-Introtests unter echtem Wayland via WSLg bestanden (versteckte
  Fenster, **nur im Testprozess** `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`, weil
  dort ICU fehlt; EGL meldet GPU-Fallback-Warnungen). Kein vollständiger Nachweis
  auf dem Linux-PC des Kollegen oder für dessen Audiogerät.
- Der zusätzliche Linux-Ladeablauftestlauf ergab **11 bestanden, 1 fehlgeschlagen**:
  `NativePreparationFailureReleasesLockAndKeepsActiveCartridge` erwartet noch den
  rohen technischen Fehlertext, während die bereits vorher vorhandene Oberfläche
  eine lesbare Fehlermeldung zeigt. Test und Fehlerdarstellung nicht für das Intro
  umgebogen. Diesen bestehenden Erwartungsabgleich separat nachholen.
- [ ] Klanglautstärke, Lautsprecher/Headset-Wechsel, Fokusverlust und subjektives
  Timing auf echten Windows-/Linux-Audiogeräten abnehmen; alle ROM-Einstiege,
  Sofa-Modus, unterschiedliche Fenstergrößen und Creator-Dateien praktisch testen.
- [ ] Windows-Controllerbedienung der **Vorschau im Einstellungsfenster** prüfen;
  der aktuelle Spielstart-Pfad besitzt eigene Controller-Überspringen-Behandlung.
- [ ] MP3/längere Dateien mit explizitem Zuschnitt und Bereinigung ungenutzter
  Medienkopien nur bei späterem Bedarf ergänzen. Nicht als schon unterstützt nennen.

## Merkliste: Aether-UI praktisch prüfen — 02.10.2026

**Umsetzung abgeschlossen; praktische Gesamtabnahme noch offen.** Auf Wunsch des
Nutzers als späterer Prüfauftrag festgehalten, nicht als bereits bestanden abhaken.
Die [Fund- und Abnahmeliste](UI_THEME_HANDOFF_2026-09-28.md) bleibt maßgeblich.

- [ ] Alle 26 Fenster einschließlich Untermenüs, Rechtsklick, Dropdowns,
  Scrollleisten sowie seltener Fehler- und Bestätigungsdialoge öffnen. Verbliebene
  app-eigene Windows-Optik mit Screenshot und Reproduktionsschritten festhalten;
  auch Alt+Leertaste prüfen. Betriebssystem-Sicherheitsdialoge bleiben ausgenommen.
- [ ] Alle sechs Themes und eigene Farben prüfen: Konturen, Hover, Fokus,
  deaktivierte Zustände, Kontrast und vollständig lesbare lange Warnungen.
- [ ] Maus, Tastatur und Controller durchtesten: Fokuswechsel, Auswahl, Scrollen,
  Eingabe, Bestätigen und Abbrechen; Unicode/IME, Undo, Passwortschutz und
  Screenreader gesondert prüfen.
- [ ] Datei- und Farbauswahl prüfen: Filter, Pfade, Überschreib-Abbruch,
  unveränderte Dateien bei Abbruch sowie Farb-Vorschau und Übernehmen. Auch
  langsame oder nicht erreichbare Netzwerkpfade testen.
- [ ] Echte Monitorwechsel mit 100/150/200 % Skalierung und unterschiedliche
  Fenstergrößen prüfen. Die vier übersprungenen Hardware-/Desktoptests in einer
  geeigneten Umgebung nachholen; automatisierte Tests ersetzen diese Abnahme nicht.

## Phase 5.3 — Updates: Prüfen und Herunterladen, 02.10.2026

**Gemeinsame erste Ausbaustufe für Windows und Linux implementiert. Kein fertiger
Selbstinstaller.** Die laufende Installation wird nicht ersetzt und die Anwendung
wird nicht für ein Update beendet. Spielstände, ROMs und Einstellungen bleiben
unangetastet. Es werden keine Pakete entpackt oder ausgeführt.

Windows: **Einstellungen → App und System → Bedienung → Updates**. Linux:
**Settings → App & files → Files & updates → Updates**. Beide Suchen finden
`Updates`. Der Ablauf ist auf beiden Plattformen derselbe:

1. **Jetzt prüfen / Check now** liest öffentliche GitHub-Releases aus dem fest
   eingestellten Repository `VoltexModz/AetherBoy`, ohne GitHub-Login oder Token.
   Ein Commit auf `development` ist kein Release und wird nicht als Update erkannt.
2. Verglichen werden SemVer-Versionen, nicht Veröffentlichungsdatum oder
   Commit-Hash. `alpha.10` ist neuer als `alpha.2`; `+Buildmetadaten` ändern die
   Reihenfolge nicht. Gleiche/ältere Versionen werden nicht angeboten. Development-
   Builds berücksichtigen auch Vorabversionen, Stable-Builds nur stabile Releases.
3. Genau ein passendes Paket muss vorhanden sein: Windows x64 ZIP, Linux x64 oder
   ARM64 self-contained TAR.GZ. Beim Download werden Dateigröße und SHA-256 aus
   GitHubs Release-Metadaten geprüft. Fehlende Prüfsumme, falsche Architektur,
   doppelte Pakete oder fremde Download-URLs sperren das Angebot. Ein neueres
   Release ohne Paket führt nicht zum stillen Rückgriff auf ein älteres.
4. **Paket herunterladen / Download package** startet erst auf Klick. Abbruch,
   begrenzte Laufzeit, maximal 1 GiB Paketgröße, begrenzte JSON-Antworten und
   höchstens 500 katalogisierte Releases verhindern unbeschränkte Operationen.
   Erlaubte Weiterleitungen führen ausschließlich per HTTPS zu den freigegebenen
   GitHub-Asset-CDNs. Browserseiten, beliebige Hosts, Zugangsdaten in URLs und
   HTTP-Downgrades werden nicht verfolgt.
5. Downloads liegen in einem eindeutigen Unterordner von
   `%LOCALAPPDATA%/AetherBoy/Updates` bzw. `$XDG_CACHE_HOME/aetherboy/updates`
   (normaler Linux-Fallback `~/.cache/aetherboy/updates`). Portable Mode verwendet
   diese Ordner im bestehenden portablen Datenprofil. Vorhandene Downloads werden
   nicht überschrieben. Erst nach vollständiger Prüfung wird `.partial` atomar
   zum Paket; bei Fehler/Abbruch wird nur die eigene temporäre Datei entfernt,
   sofern das Dateisystem dies zulässt. Verbleibende `.partial`-Dateien werden
   niemals als geprüftes Paket angeboten.
6. **Downloadordner öffnen / Open download folder** führt zur geprüften Datei.
   Auch vor einer neuen Prüfung oder nach Neustart bleibt der allgemeine
   Update-Downloadordner über denselben Knopf erreichbar. Bei hartem Programm-
   oder Systemabbruch verbliebene `.partial`-Dateien dürfen dort manuell entfernt
   werden; es gibt noch keine automatische Cache-Bereinigung.
   Manuell in einen **neuen** Programmordner entpacken. Portable Nutzer behalten
   den alten Ordner und übernehmen ihren `AetherBoyData`-Ordner sowie den
   Portable-Marker erst bei beendeter Anwendung bewusst in die neue Installation.
   Paket herunterladen allein migriert keine Daten und aktiviert nicht Portable Mode.

**Datenschutz:** Startprüfung ist zunächst aus und global, kein ROM-Profilwert.
Bei Freigabe einmal pro Programmstart; der Befund steht auf der Updates-Seite,
noch ohne Desktop-Benachrichtigung. GitHub sieht IP-Adresse und einen festen
technischen User-Agent, aber keine ROM-/Save-Pfade, Spieltitel, Kontodaten oder
Online-Link-Kennungen. Backup-Wiederherstellung aktiviert keine alte Startfreigabe
erneut. Eine Fehlermeldung wird nie als „aktuell“ dargestellt.

**Sicherheitsgrenze:** GitHubs HTTPS-Digest prüft Übertragungsintegrität, ist aber
keine unabhängige Signatur des NekoZDevTeams. Ein kompromittiertes Repository wird
damit nicht erkannt. Automatisches Austauschen ausführbarer Dateien bleibt gesperrt.

### Release-Voraussetzungen und offene Merkliste 5.3

- Für jede angebotene Version zuerst `Version` in `Directory.Build.props` erhöhen,
  beide Plattformen bauen/testen und ein dazu passendes GitHub-Release mit SemVer-
  Tag (z. B. `v4.8.0-alpha.2`) veröffentlichen. Am 02.10.2026 lieferte die öffentliche
  Release-API noch keine Releases; echte Verfügbarkeit/Downloads daher noch offen.
- Windows-Paket aus `scripts/package-windows.ps1 -Channel development|stable`:
  vorhandenes `AetherBoy-Windows-x64-<12-stellige Revision>-<UTC Datum>-<Uhrzeit>[-local].zip`
  wird erkannt. Alternativ exakt
  `AetherBoy-<Version>-win-x64-self-contained.zip`. Keine mehreren Windows-Pakete
  derselben Architektur in dasselbe Release stellen.
- Linux: `scripts/package-linux.sh --channel development|stable --runtime linux-x64|linux-arm64`.
  Paketname `AetherBoy-<Version>-<RID>-self-contained.tar.gz`. Die isolierte
  Quellpaketerstellung enthält jetzt auch Discord-, Newtonsoft-, SharpCompress-
  und Cheat-Lizenzen; die Paketprüfung verlangt sie ausdrücklich.
- Pakete als Release-Assets hochladen und GitHubs `digest: sha256:…` kontrollieren.
  Ein separater `.sha256`-Anhang ersetzt das vom Downloader benötigte API-Feld
  nicht. Release-Tag, Paketinhalt und Build-Kanal müssen zusammenpassen. Das ist
  derzeit eine **Release-Verantwortung**, keine Signaturprüfung des Paketinneren.
- Noch umzusetzen: signierte Update-Manifeste mit im Client verankertem
  Vertrauensschlüssel und Schlüsselwechsel; automatisierte, überprüfte
  Release-Pipeline; sicherer Windows-Installationshelfer; Linux-Installationswege
  getrennt von Distro-/Flatpak-Paketverwaltung; Rückfall bei fehlgeschlagenem
  Neustart; Schutz/Übernahme portabler Daten; Benachrichtigung über neue Releases.
- Noch praktisch testen: reales GitHub-Release auf beiden OS herunterladen;
  Wayland-UI unter KDE/GNOME/Hyprland, ARM64 und Netzwerkunterbrechungen während
  großer Downloads. Keine Veröffentlichung oder In-place-Installation in diesem
  Arbeitsschritt.

Automatisiert: `ReleaseUpdateTests`, `WindowsUpdateTests`, `LinuxUpdateTests`.
Synthetische Releases/Pakete ohne Kontakt zu GitHub prüfen Auswahl, Datenbegrenzung,
Umleitungen, Integrität, Wiederholung, Abbruch, Dispose und Datenschutz. Windows-
UI-Bild geprüft; native Linux-UI-Tests bleiben auf Windows ausdrücklich übersprungen.
Ergebnis dieses Schritts: 51 neue Runtime-Tests, vier Windows-Tests und drei
Linux-Logiktests bestanden; ein neuer Wayland-Test auf Windows übersprungen.
Vollständige Regression: Runtime 604 bestanden / 5 übersprungen, Windows
287 bestanden / 4 übersprungen, Desktop 139 bestanden / 53 übersprungen,
Core 253 bestanden.
Release-Build ohne Warnungen oder Fehler. Die Python-Paketprüfer sind syntaktisch
geprüft; kein echter Linux-Paketbau auf diesem Windows-Host behauptet.
Referenz für das API-Schema:
[GitHub Releases](https://docs.github.com/en/rest/releases/releases#list-releases)
und [Release Assets](https://docs.github.com/en/rest/releases/assets#get-a-release-asset).

## Phase 5.2 — Discord-Spielstatus, 02.10.2026

Auf Windows und Linux implementiert. Auf ausdrücklichen Nutzerwunsch **standardmäßig
aktiviert**, jederzeit abschaltbar. Die öffentliche AetherBoy-Application-ID
`1555427237908586616` ist hinterlegt; es werden keine Bot-Tokens, Passwörter oder
OAuth-Anmeldungen benötigt. Eine geänderte, bereits gespeicherte ID schaltet die
Anzeige bis zur erneuten Freigabe aus. Vorhandenes ausdrückliches Ausschalten bleibt
beim Neustart erhalten; eine beschädigte Konfiguration oder Wiederherstellung aus
Backup schaltet die Freigabe vorsichtshalber aus.

- Windows: **Einstellungen → App und System → Bedienung → Discord-Spielstatus**.
  Suche: `Discord`, `Rich Presence`, `Spielstatus`. Linux: **Settings → App & files
  → Desktop & accessibility → Discord activity**, zusätzlich über die Suche.
- GB, GBC und GBA nutzen denselben Dienst. Der normale Einzelspielstatus enthält
  System und Spielen/Pause. **Spieltitel freigeben** ist ein unabhängiger, anfangs
  ausgeschalteter Schalter. Wenn erlaubt, stammt der Titel ausschließlich aus dem
  ROM-Header, nie aus einem Dateinamen oder Bibliotheksalias. Fehlende Titel bleiben
  allgemein. Pfad-/URL-artige Werte, Steuerzeichen und überlange Titel werden begrenzt.
- Keine Übertragung von ROMs, Hashes, Pfaden, Spielständen, Raumcodes, Freunden,
  Bildschirmaufnahmen oder Eingaben. Keine Discord-Einladungen/Join-Secrets.
  Discord erhält die für RPC erforderliche Prozess-ID und die freigegebene Aktivität.
  Ohne aktives Einzelspiel, bei Fehler/Stop und während lokaler/Online-Link-Sitzungen
  wird die Aktivität geleert. Die Vorschau zeigt erlaubte Inhalte, nicht den Nachweis
  einer öffentlich sichtbaren Discord-Anzeige. Der eigentliche Statustext ist auf
  beiden Plattformen Englisch; Windows-Bedienung bleibt Deutsch, Linux Englisch.
- Lokale RPC-Verbindung zur laufenden Discord-Desktop-App desselben Benutzers;
  Discord-Webseite allein genügt nicht. Kein Raumserver/STUN/TURN und kein zusätzlicher
  AetherBoy-Webdienst. Einmal pro Sekunde wird der Snapshot gelesen; normale Änderungen
  werden für fünf Sekunden zusammengefasst. Abschalten/Titelentzug und Leeren warten
  nicht auf dieses Intervall. Keine Netzwerk-/Dateioperation auf dem Emulations-Thread.
- Gemeinsame Runtime mit DiscordRichPresence 1.6.1.70 und Newtonsoft.Json 13.0.4 (MIT).
  Eigener Pipe-Adapter sichert zwei im isolierten Test nachgewiesene Randfälle ab:
  Löschen vor dem Schließen der IPC-Verbindung und Wiederholung nach READY trotz
  zuvor identischer Aktivität. Wartende/replayte Meldungen werden mit der neuesten
  Freigabe geschrieben, damit alte Antworten keinen zurückgenommenen Titel wieder
  veröffentlichen. Schnelles Aus-/Einschalten wartet intern auf das Ende der alten
  Verbindung, ohne die Oberfläche zu blockieren. Nach Prozessabsturz/Verbindungsverlust
  hängt die sichtbare Entfernung auch vom Discord-Client ab; keine Sofortgarantie.

**Abnahme:** automatisierte Tests für Standardwerte, Opt-out, Titelgrenzen,
Header statt Dateiname, alle drei Systeme, Link-Unterdrückung, Fehler/Retry,
Wiederverbindung, Abschalten, alte Einstellungen, Profilisolation und Backup-Recovery.
Die echte RPC-Bibliothek wird gegen eine In-Memory-Gegenstelle geprüft; Frontendtests
verwenden isolierte Clients und veröffentlichen keine Testaktivitäten. Windows-Layout
wird synthetisch gerendert. Native Linux-Oberfläche und echte Discord-Veröffentlichung
sind damit **noch nicht manuell abgenommen**.

Prüfstand: **26 neue Tests bestanden** (19 gemeinsame Policy-/RPC-Tests,
4 Windows, 3 Linux-Speicherung), 1 neuer nativer Wayland-Test übersprungen.
Gesamtsuiten: Core 253 bestanden, Runtime 553 bestanden/5 übersprungen,
Windows 283 bestanden/4 übersprungen, Desktop 136 bestanden/52 übersprungen.
Nach der letzten Teardown-Fehlerabsicherung nochmals alle 19 Discord-Runtime-Tests
bestanden. Release-Build ohne Warnungen/Fehler, Restore im Locked Mode erfolgreich.
UI-Copy-Prüfung: einziges Signal ist der bewusst als Navigation verwendete
Breadcrumb im Windows-Suchkatalog. Lizenzdateien sind in beiden Buildausgaben enthalten.

Manuelle Prüfung auf beiden OS: Discord öffnen, normales GB/GBC/GBA-Spiel starten,
Titel freigeben/entziehen, pausieren, ROM wechseln/schließen, ausschalten, App neu starten,
Discord beenden/neu starten, lokal/online verlinken. Discords eigene Einstellung zur
Aktivitätsfreigabe kann die Anzeige verhindern. Unter Linux können Flatpak/Snap-Sandboxes
den IPC-Socket verbergen; keine automatische Lockerung von Sandbox-Berechtigungen.
App-Name und App-Icon kommen aus dem Discord Developer Portal. Ein zusätzlicher
Rich-Presence-Bildasset-Key, Zeitstempel, Join-/Spectate-Aktionen bleiben Folgearbeit.

Referenzen: [Discord: Rich Presence](https://github.com/discord/discord-api-docs/blob/main/developers/discord-social-sdk/development-guides/setting-rich-presence.mdx),
[RPC-Bibliothek](https://github.com/Lachee/discord-rpc-csharp/tree/v1.6.1),
[Lizenzhinweise](../THIRD_PARTY_NOTICES.md#optional-discord-activity-2026-10-02).

## Phase 5.1 — Sofa-Modus, 01.10.2026

Nach den Nutzerantworten: komfortable Controller-Oberfläche ähnlich Big Picture,
aber im vorhandenen AetherBoy-Theme. **Kein gesperrter Kinder-/Kioskmodus.**
Kein automatischer Start in diesem Modus. Discord und Updater gehören nicht zu 5.1.

- Windows-Einstieg: **SYSTEM → Sofa-Modus öffnen**. Linux: **Sofa mode** in der
  Hauptansicht. Vollbild-Bibliothek mit „Zuletzt gespielt“, „Favoriten“,
  „Meine Auswahl“ und „Alle Spiele“; drei große Karten je Seite, lokale Vorschaubilder.
- Eigene Auswahl und Favoriten werden unabhängig als Bibliotheksmetadaten gespeichert
  (`SofaSelected`). Bestehende Datensätze sind weiter lesbar; ROMs/Saves werden dadurch
  nicht geändert. „Meine Auswahl“ ist ein Filter, keine Zugriffssperre.
- D-Pad/linker Stick navigieren; untere Aktionstaste wählt, rechte Aktionstaste geht
  zurück. Tastatur/Maus bleiben nutzbar. Keine automatische Wiederaufnahme eines
  Savestates: Ein anderes Spiel wird über den normalen sicheren ROM-Ladeweg geöffnet.
  Das bereits laufende Spiel kann ohne Neustart fortgesetzt werden.
- Im Spiel: sichtbares **Spielmenü**, **F10**, **Esc** oder **L3+R3**. Das kompakte Menü
  bietet Weiter, Bibliothek, Slotwahl, Schnellspeichern/-laden, Screenshot und Ausgang.
  **Sofa-Modus verlassen** oder **Strg+Umschalt+F11** stellen den vorherigen
  Vollbild-/Fensterzustand wieder her. Beim Bibliotheks-/Menübesuch pausiert das Spiel;
  ein vorher pausiertes Spiel bleibt beim Zurückgehen pausiert. „Weiter spielen“
  im Spielmenü hebt die Pause dagegen ausdrücklich auf.
- Aktive Online-Link-Sitzungen und laufende Lade-/Speicheraufträge blockieren den
  Einstieg. Linux blockiert zusätzlich die im selben Host laufende lokale Link-Sitzung.
  Das separate Windows-Link-Fenster wird nicht umgebaut. Sofa ist zunächst Einzelspieler.

**Prüfung und offene Abnahme:** gemeinsame Filter-/Seitentests, persistente Metadaten
beider Frontends, Windows-Controller-Fokus, Fenster-Rückkehr und synthetische Layouts.
Native Wayland-Prüfung ist als `LinuxSofaTests` hinter `AETHERBOY_UI_TESTS=1` angelegt
und muss unter Linux ausgeführt werden. Ein realer Controller-Durchlauf auf Windows
und Linux, Couch-Lesbarkeit, Monitorwechsel und hohe DPI bleiben manuelle Abnahme.
Keine Behauptung einer nativen Linux-Abnahme auf dem Windows-Entwicklungsrechner.

Prüfstand dieser Umsetzung: **16 neue Tests bestanden** (9 Windows, 6 gemeinsame
Bibliotheksregeln, 1 Linux-Metadaten), **1 nativer Wayland-Test übersprungen**.
Release-Build ohne Warnungen/Fehler. Core-, Runtime-, Windows- und Desktop-Suiten
liefen ohne Fehler; die letzte kleine Resume-Unterscheidung wurde danach gezielt
mit allen neun Windows-Sofa-Tests und einem Linux-Build nachgeprüft.

## Phase 3 — erster gemeinsamer Ausbau, 01.10.2026

Dies ist die neue Medien-/Archivphase, nicht die historische Windows-W3-Nummer.
Die vorhandenen PNG-Screenshots, WAV-Aufnahmen und das Patch Lab bleiben erhalten.

### Implementiert auf Windows und Linux

- [x] Gemeinsame AVI-Videoaufnahme für eine normale `EmulationSession`: native
  GB/GBC-Auflösung 160×144 oder GBA 240×160, BGR24 und Stereo-PCM16. Bildrate
  262144/4389, Tonrate aus dem jeweiligen Kern, keine externen Encoder notwendig.
  Kein Mikrofon, kein Desktop-Ton und keine Menüaufnahme. Die Aufnahme liegt vor
  dem Lautstärkeregler der Lautsprecherausgabe; bei deaktivierter Kernaudioausgabe
  wird Stille eingefügt. Nicht gelieferte Audioblöcke werden als Stille gezählt,
  nicht durch Wiederholen eines alten Tons verdeckt.
- [x] Dateiausgabe auf einem eigenen Hintergrundauftrag, höchstens 16 wartende
  Bilder und eine Sekunde Audiopuffer. Wenn die Ausgabe nicht hinterherkommt,
  endet der Clip mit Hinweis, statt still Bilder wegzulassen. Pause fügt keine
  Filmzeit hinzu; Frameskip wiederholt das letzte Bild. Turbo, Reset, Rewind,
  State-Laden und Tonratenwechsel beenden den bisherigen Clip.
- [x] Maximale AVI-Größe 2 GiB. Zuerst `.avi.partial`, dann Index/Header/Flush
  und Umbenennen ohne Überschreiben. ROM-Wechsel und normales Beenden schließen
  die Aufnahme ab. Bei Schreibfehler oder Prozessabbruch kann eine unvollständige
  `.partial`-Datei bleiben; diese wird nicht als fertiges Video gemeldet.
- [x] ZIP-/7z-Öffnen über Dateiauswahl, Dateipfad oder Drag-and-drop. Bei mehreren
  `.gb`-, `.gbc`- oder `.gba`-Dateien erfolgt eine ausdrückliche Spielauswahl.
  Die entpackte ROM bleibt als
  inhaltsadressierte Bibliothekskopie erhalten, auch nach Entfernen des Archivs.
  Keine Übernahme archivierter Saves, Firmware, Skripte oder Patchdateien.
- [x] Archivgrenzen: 128 MiB Eingabedatei bzw. deklarierter Gesamtinhalt, 512
  Einträge, 1024 Zeichen pro Eintragsname, 32 MiB für eine ROM. Keine Pfad-Traversierung, absoluten Eintragspfade,
  verschlüsselten Nutzdaten, Links oder unvollständigen Mehrteilarchive. Private
  Staging-Namen; Größenprüfung beim Kopieren; ZIP-CRC und vorhandene 7z-CRC prüfen.
  Der Parser ist SharpCompress 0.50.4 (MIT); Lizenz wird auf beiden Plattformen
  mitgeliefert. Header-/Decoder-Speicherverbrauch ist damit nicht vollständig
  isoliert: die Größenprüfungen sind keine Sandbox für bösartige Archivheader.
- [x] Bestehende IPS-/BPS-/UPS-Ergebnisse bleiben dauerhaft eigene ROMs mit
  Herkunftsmanifest und getrennter Save-Zuordnung. Neue Regressionen prüfen, dass
  das Ergebnis nach Entfernen von Original und Patch weiter auffindbar ist.
  Windows-Patchtitel beachten jetzt die gemeinsame 80-Zeichen-Metadatengrenze.
- [x] Archivauswahl im Windows-Designdialog und als Linux-Seite mit fünf Einträgen
  pro Seite, Maus-/Tastatur-/Controller-Bedienung und Abbruch. Nur die ausgewählte
  ROM wird entpackt; doppelte Dateinamen bleiben durch Eintragsnummern unterscheidbar.
  Die Auswahl ist an den SHA-256 des gesamten Archivs gebunden. Geänderte Archive
  werden nicht mit einer veralteten Auswahl geladen. Bis zum erfolgreichen Import
  bleibt die bisherige Sitzung erhalten.
  Die Linux-Kopie lehnt wie der Windows-Ladeweg identische Inhalte mit einem
  widersprüchlichen GBA-/GB-Systemsuffix ab, statt still den Kern zu wechseln.
- [x] **Patch speichern** bzw. **Apply Patch** bleiben ohne Spielwechsel.
  **Patchen und starten** / **Patch and play** fordert ausdrücklich den Start
  der dauerhaft gespeicherten Ergebnis-ROM an. Windows bietet nach dem reinen
  Speichern zusätzlich **Ergebnis starten**. Auf Linux verfällt ein vorgemerkter
  Start beim Verlassen der Seite oder Öffnen eines anderen Spiels; der Import
  läuft weiter. Patch-Basisdateien müssen weiterhin entpackte ROMs sein.

### Bedienung und Speicherorte

- Windows: **Einstellungen → Übersicht → Gameplay aufnehmen**. Die Suche findet
  `AVI` oder `Videoaufnahme`. Clips: `%LOCALAPPDATA%/AetherBoy/Recordings`;
  ROM-Kopien: `%LOCALAPPDATA%/AetherBoy/Roms/<SHA256>/`.
- Linux: **Tools → Record video → Start video**, danach ins Spiel zurückkehren.
  Die Suche nach `AVI` öffnet dieselbe Seite. Clips unter
  `$XDG_DATA_HOME/aetherboy/recordings`, standardmäßig
  `~/.local/share/aetherboy/recordings`; ROM-Kopien unter `roms/<SHA256>/`.
- Portable Mode verwendet die vorhandenen plattformspezifischen portablen
  Datenwurzeln. „Aufnahmeordner öffnen“/„Open recordings“ führt zum richtigen Ort.
- Unkomprimiertes AVI benötigt ungefähr 250–410 MiB pro Minute. Das ist eine
  erste encoderfreie Implementierung, kein platzsparender MP4-Export.

### Noch offen / bewusst nicht als fertig gewertet

**Merkliste für später (Nutzerentscheidung vom 01.10.2026):** Die folgenden
Phase-3-Erweiterungen und Abnahmen bleiben offen und werden vorerst zurückgestellt.
Das ist keine Freigabe als vollständig getestet und kein Auftrag zur sofortigen Umsetzung.

- [ ] Reale Video-/Tonsynchronität in längeren GB/GBC/GBA-Spielrunden, Abspielen
  mit externen Playern und volle/langsame Datenträger auf beiden Systemen prüfen.
- [ ] Native Wayland-Bedienung auf Linux praktisch abnehmen; ein Windows-Build
  und portable .NET-Tests ersetzen diesen Nachweis nicht.
- [ ] Gespeicherte automatische Patch-Zuordnungen beim Öffnen einer Basis-ROM.
  Nur ausdrücklich vom Nutzer eingerichtet und abschaltbar; Original-ROM und
  ursprüngliche Spielstände bleiben erhalten, die Ergebnis-ROM bleibt gespeichert.
  Der neue ausdrückliche Patch-und-Start-Ablauf ersetzt keine solche Zuordnung.
  Patches neben der ROM oder im ZIP/7z werden nicht automatisch ausgeführt.
- [ ] Platzsparendes Videoformat, längere Clips und Aufnahme der lokalen
  Zwei-Spieler-Ansicht separat ausbauen. Der bestehende Local-Link-Code ist nicht
  an diese Einzelspiel-Aufnahme angeschlossen.

Neue Regressionen: `Phase3CaptureArchiveTests` (37), `WindowsPhase3Tests` (11),
`LinuxPhase3Tests` (8), zusätzlich vier native Wayland-Fälle in `LinuxAsyncOpenTests`
und `LinuxPatchLabTests`. Synthetische ROMs testen auch die echten GB/GBC/GBA-Kerne;
keine privaten Spiele oder Spielstände wurden dafür verwendet. Windows-Karte
und die neuen Archiv-/Patch-Dialoge als Screenshots und mit Textgrößenprüfung
kontrolliert. Copy-Review für diese Fortsetzung: vier bestehende Formulierungen
manuell gelesen, keine neuen Treffer.

Die gesamte Lösung einschließlich Hilfswerkzeugen baut in Release mit **0
Warnungen und 0 Fehlern**. `dotnet restore nanoboy.sln --locked-mode` funktioniert;
die Archivlizenz liegt in beiden Frontend-Ausgabeordnern. Kein Commit/Push.

Letzte vollständige Suitenläufe nach dieser Fortsetzung: Core **253 bestanden**,
Runtime **528 bestanden / 5 übersprungen**, Windows **270 bestanden / 4 übersprungen**,
Desktop **132 bestanden / 50 übersprungen**. Zusammen **1.183 bestanden, 59 übersprungen**,
darunter 56 bestandene Phase-3-Fälle. Native Online-/Hardware-/Wayland-
Opt-in-Tests wurden dadurch nicht nachgewiesen.

Die Windows-UI-Tests installieren vor asynchronen Dialogaktionen ausdrücklich
einen `WindowsFormsSynchronizationContext`, wie beim normalen `Application.Run`.
Ohne diesen Kontext blockierte ein neuer Test im modalen Auswahldialog des
Testläufers; der korrigierte Test weist Auswahl und Abbruch mit der tatsächlichen
Laderoutine nach. Die vorhandene UPS-Regression erwartet die neue Beschriftung
„Original wiederherstellen“ und prüft weiterhin Richtung und Ergebnisbytes.

Ein paralleler Gesamtlauf zeigte Shutdown-Timeouts in GBA-Online- und Windows-
Local-Link-Tests. Die unveränderten Link-Tests bestanden danach im getrennten
Runtime-/Windows-Suitenlauf. Diese Beobachtung ist keine behobene Link-Ursache:
die Abnahme der Shutdown-Zeitbudgets unter paralleler Last bleibt offen.

Referenz für das selbst implementierte Dateiformat:
[Microsoft AVI RIFF specification](https://learn.microsoft.com/en-us/windows/win32/directshow/avi-riff-file-reference).

## Phase-1-Nachprüfung — 01.10.2026

### Update: Phase 1.1 implementiert

Die unten aufgeführten Ausgangsbefunde sind in dieser Folgerunde repariert.
Das ist eine Software-Abnahme, keine vollständige Controller-/USB-Abnahme:

- [x] XInput übergibt `NativeVibration` per Referenz und wertet den Rückgabecode aus.
  Ausgabe, Gerätewechsel, Ausfall, Abschalten, Dispose und Watchdog sind mit
  injizierter Ausgabe/Uhr regressiert. Ein alter Timer-Callback stoppt keine
  inzwischen verlängerte Vibration. Reale Motoren bleiben separat zu prüfen.
- [x] Verwaltete „Zuletzt geöffnet“-Einträge werden als `managed:<SHA256>` gespeichert.
  Alte absolute Bibliothekspfade werden auf eine hashgeprüfte Kopie am aktuellen
  Speicherort aufgelöst, auch wenn die alte Installation noch vorhanden ist.
  Externe absolute Pfade bleiben extern; relative Fremdpfade werden verworfen.
- [x] Gemeinsame Grenzen für Titel, System, Spielzeit, Genre, Bewertung und Tags.
  Semantisch defektes JSON nutzt die letzte gültige Sicherung. Ohne gültige
  Sicherung zeigt die Vault den betroffenen Eintrag mit Hinweis statt abzustürzen;
  beschädigte Dateien werden nicht still überschrieben. Vorschaudaten sind begrenzt.
- [x] Windows und Linux verwenden dieselbe aktive Echtzeituhr: kein Turbo-Multiplikator,
  keine Zeit für Pause, Menüs, Laden, Online-Sitzungen, Start/Stop/Fault oder
  Abtastlücken ab zwei Sekunden. Fehlerhafte Zeit-Checkpoints bleiben pro ROM
  im Speicher für einen erneuten Versuch, auch über einen Spielwechsel hinweg.
- [x] Die portable Schreibprobe schreibt und flusht tatsächlich ein Byte.
  Ein ungültiger Speicherort führt nicht zu einem stillen Profil-Fallback.
- [ ] Native Controller, echte USB-Laufwerke, voller/gesperrter Datenträger und
  vollständige portable App-Starts mit Flag/Marker weiterhin praktisch abnehmen.

Regressionen: `WindowsPhase11RegressionTests`, `Phase11RegressionTests` und
`LinuxPhase11RegressionTests`; sie arbeiten mit synthetischen ROMs/temporären Daten.
Abschlusslauf: Core 253 bestanden; Runtime 491 bestanden/5 übersprungen;
Windows 259 bestanden/4 übersprungen; Desktop 124 bestanden/46 übersprungen.
Insgesamt **1.127 bestanden, 55 übersprungen, 0 fehlgeschlagen**. Davon 35 neue
bestandene Phase-1.1-Testfälle; zwei neue Wayland-Integrationsfälle werden auf
diesem Windows-Host übersprungen. Beide Release-Builds: 0 Warnungen/Fehler.
UI-Copy-Review: vier Hinweise auf bestehende, unveränderte Texte; neue Warntexte
gelesen, Windows-Breitenprüfung im Regressionstest. GBA-Rumble ist **nicht** Teil
dieser Reparatur.
Keine Änderungen an echten Spielständen, kein Commit oder Push.

### Arbeitsstatus und Merkliste nach Phase 1.1

Phase 1 einschließlich Reparaturrunde 1.1 ist für den derzeit vereinbarten
Implementierungsumfang vorerst abgeschlossen. Die nachfolgenden Prüfungen werden
ausdrücklich zurückgestellt, nicht als bestanden gewertet. Phase 2 ist ebenfalls
für diese Entwicklungsrunde abgeschlossen; ihre zurückgestellten Spezialbefehle
und Spieltests stehen in der [Cheat-Merkliste](CHEAT_SUPPORT.md#merkliste-offene-cheat-arbeit-01102026).
Das ist keine vollständige Hardware- oder Cheat-Kompatibilitätsfreigabe.

- [ ] **Controller unter Windows und Linux:** mit echtem MBC5-Rumble-Spiel
  Start/Stopp, Pause, Fokusverlust, Menüs, Controllerwechsel, Abziehen und Beenden
  prüfen. Windows-XInput und WGI sowie Linux-SDL getrennt nachweisen; fehlenden
  Motor oder fehlende Treiberunterstützung ebenfalls testen.
- [ ] **Portable App-Starts auf beiden Plattformen:** Flag und Marker jeweils
  mit anderem Arbeitsordner starten. Vollständigen Datenbaum inklusive ROMs,
  Saves, States, Einstellungen und Firmware auf einen anderen Ordner/USB-Datenträger
  übernehmen. Unter Windows einen anderen Laufwerksbuchstaben prüfen; keine
  Datenübernahme oder Schreibzugriffe ins normale AppData-/XDG-Profil erwarten.
- [ ] **Speicherfehler und Unterbrechungen:** Schreibschutz, voller Datenträger
  und abgebrochener Import mit entbehrlichen Testdaten prüfen. Vorhandene Saves
  und lesbare Sicherungen müssen erhalten bleiben; kein stiller Profil-Fallback.
  Ausstehende Spielzeit darf bei Retry nicht doppelt gezählt werden.
- [ ] **Native Wayland-Abnahme:** neue Resume-Fehler-/Stopped-Spielzeittests sowie
  Bibliotheks-Recovery-Hinweise, Menüs, Spielwechsel und Neustart tatsächlich unter
  Linux ausführen. Übersprungene Tests auf Windows sind dafür kein Nachweis.

GBA-Rumble, ein plattformübergreifend austauschbares portables Profil und die
optionalen Komfortfunktionen sind separate spätere Erweiterungen, keine bereits
fertigen Funktionen dieses Pakets. Die Cheat-Merkliste bleibt unverändert offen.

### Historischer Ausgangsbefund vor der Reparatur

Gemeint ist das neue Paket Portable Mode, Bibliotheksmetadaten und Controller-
Rumble samt vorhandener Spielzeit, nicht die historische W1-Nummerierung unten.
Damals: Grundfunktionen vorhanden, **nicht vollständig abgenommen**.
Die damalige Runde analysierte nur; der aktuelle Reparaturstand steht oben.
Die offenen Cheat-Punkte stehen getrennt in der
[Phase-2-Merkliste](CHEAT_SUPPORT.md#merkliste-offene-cheat-arbeit-01102026).

Vorhanden: Flag/Marker und App-lokale Datenpfade, Schreibprobe ohne stillen
AppData-Fallback, ROM-Kopien, gemeinsame Genre-/Rating-/Tag-Validierung, Suche/
Filter/Sortierung, Favoriten, Sitzungs-/Gesamtspielzeit und MBC5-Rumble-Snapshot.
Windows hat WGI-/XInput-Ausgabe und einen zeitbegrenzten Stopp bei fehlenden
Updates; Linux verwendet SDL-Rumble mit begrenzter Dauer. Die Signalkette
belegt bislang **MBC5-Rumble für GB/GBC**, nicht allgemeines GBA-Rumble.

### Historische Reparaturliste vor Phase 1.1

1. [ ] **P1: XInput-Rumble-Anbindung korrigieren.**
   `nanoboy/Input/XInputGamepad.cs` deklariert `XInputSetState` mit
   `NativeVibration` als Wert. Die native API erwartet `XINPUT_VIBRATION*`.
   Das muss eine passende By-reference-Anbindung werden; anschließend Layout,
   Rückgabecodes, Start/Stopp, Fokusverlust, Abziehen und Watchdog regressieren.
   Keine bestehende native Falschanbindung für einen Hardwaretest aufrufen.
   Der WGI-Standardpfad erklärt, warum dieser XInput-Fallback bisher unbemerkt
   bleiben konnte. [Microsoft-Vertrag](https://learn.microsoft.com/en-us/windows/win32/api/xinput/nf-xinput-xinputsetstate).
2. [ ] **P2: Portable Recents relocierbar machen.**
   `nanoboy/RomFiles.cs` speichert Pfade unverändert. Ein isolierter Test mit
   verschobenem `AetherBoyData` ergibt: Recent-Pfad fehlt, die Vault findet die
   ROM am neuen Ort. App-interne ROMs per Identität/relativem Pfad referenzieren,
   alte absolute Einträge migrieren und keine doppelten Missing-Einträge erzeugen.
   Abnahme: Ordnerwechsel, anderer USB-Laufwerksbuchstabe, externer ROM-Pfad.
3. [ ] **P2: Bibliotheksdaten vor der UI vollständig validieren.**
   `WindowsGameLibraryStore.Read` prüft neue Tags/Genre/Rating, aber nicht alle
   Alt-/JSON-Felder. Künstliches JSON mit `Title:null` liefert einen null-Titel;
   die `Title.Contains`-Suche in `frmRomLibrary` wirft `NullReferenceException`.
   Titel/System/Spielzeit/Vorschau begrenzen bzw. normalisieren, lesbare Sicherung
   bewahren und betroffene Einträge melden statt die ganze Bibliothek zu verlieren.
   Linux und Windows sollen dieselben semantischen Regeln nutzen.
4. [ ] **P2: Phase-1-Abnahme ausweiten.**
   Echte App-Starts mit Flag und Marker, anderer Arbeitsordner, USB-/Pfadwechsel,
   nicht beschreibbarer/voller Datenträger und Unterbrechungen prüfen. Vollständige
   Rumble-Kette mit echter Hardware erst nach Punkt 1; GBA-Rumble ist ein eigener
   Hardware-/Core-Folgepunkt, kein bereits fertiges Nebenprodukt.
5. [ ] **Gemeinsam: Spielzeit verlässlicher vereinheitlichen.**
   Windows persistiert ungefähr alle 30 Sekunden; Linux bislang beim Schließen
   und gekoppelt an Resume (siehe Linux-Roadmap). Eine gemeinsame aktive-Zeit-
   Definition, begrenzte Checkpoints und unabhängige Persistenz festlegen.

Optionale Komfortverbesserungen danach: erkennbarer Portable-Modus mit Datenordner-
Zugang, klarer Kopier-/Importassistent, Rumble-Testtaste/Stärke/Unterstützungsanzeige,
gespeicherte Bibliotheksansichten und Filter. Eine USB-Datenablage ist derzeit je
Plattform implementiert: Windows `AetherBoyData/Roms/...`, Linux
`AetherBoyData/data/roms/...` und andere Metadatenfelder. Kein gemeinsamer
Windows↔Linux-Profilordner ohne explizite Konvertierung zugesichert.

### Nachweise dieser Analyse

- Frisch ausgeführt: Runtime-Phase-1-Tests 2/2, Windows-Phase-1-Tests 2/2,
  portable Linux-Phase-1-Tests 3/3. Die sieben Tests bestehen, enthalten aber
  keine End-to-end-Abnahme der oben genannten Fehlerfälle.
- Isolierter synthetischer Probe-Lauf in ignoriertem `artifacts/phase1-review/`:
  falschen XInput-Parameter nur per Reflection geprüft (kein nativer Aufruf),
  veralteten Recent-Pfad nach Ordnerwechsel und null-Titel/Suchfehler bestätigt.
- Original-ROMs, Saves und persönliche Einstellungen nicht verändert.
  Keine Produktionscode-Änderung, kein Commit/Push in dieser Analyse.

## Historischer Windows-Plan

Abgestimmt am 11. September 2026. Ein Projekt und eine Windows-Anwendung; der
Linux-Frontend-Ausbau bleibt beim Linux-Mitentwickler. Die folgenden Schritte
betreffen die Weiterentwicklung der Windows-Anwendung.

Das Komfortpaket **4–6** (Controller-Quick-Deck, Screenshots/Performance, Stereo)
ist eingebaut: [Bedienung und gemeinsame Audio-Schnittstelle](WINDOWS_PLAYER_TOOLS_STEREO.md).
Die folgenden W-Kategorien sind die ursprüngliche Roadmap, nicht die Nummerierung
dieses Komfortpakets. Linux behält vorerst seinen kompatiblen Mono-Ausgabeaufruf;
die gemeinsame Runtime liefert zusätzlich Stereo.

1. **Zentrale Ablage und Development-Diagnose:** umgesetzt. Die normale EXE
   zeichnet im Development-Kanal automatisch lokal auf. ROMs werden importiert,
   Saves/States nach Inhalt getrennt und Einstellungen versionsunabhängig abgelegt.
   Ordner lassen sich direkt in der Oberfläche öffnen. Eine separate Tester-EXE
   oder ein spezielles Tester-Paket ist nicht vorgesehen.
2. **Windows-Ausgabe (W3):** WASAPI, begrenzte Puffer und Messwerte, Gerätewechsel,
   Direct2D/VSync, Integer Scaling und aktive Timerpräzision sind eingebaut.
   [Details und noch offene Hardwareprüfungen](WINDOWS_AUDIO_VIDEO_UI.md).
   Weiterhin offen: breitere Geräte-/Latenzmessungen und Langzeit-Spieltests.
3. **Oberfläche (W4):** echtes Vollbild, DPI-/Dialoganpassung, Speicherstatus,
   Bibliothek mit Suche/Favoriten/Spielzeit/Vorschau und Profile pro Spiel sind
   eingebaut. [Bedienung und Grenzen](WINDOWS_GAME_COMFORT.md).
   Weitere Controller-Einrichtung und DPI-Prüfungen bleiben mögliche Folgeschritte.
4. **Save-Sicherheit (W5):** bestehende atomare Batterie-Saves, drei Backups,
   Integritätsprüfung und Restore-UI beibehalten. Auf der zentralen Ablage bessere
   Speicherstatus-Anzeigen, State-Galerie, separater Fortsetzen-Slot und einmaliges
   Lade-Rückgängig sind hinzugekommen. Komfortabler Save-Import/-Export bleibt offen.
   Keine erneute Implementierung bereits vorhandener Schutzmechanismen.
5. **Diagnose vertiefen:** Hintergrundbeobachter und manuelle Problemmarkierung
   umgesetzt. Fehlender Emulations-/UI-/Bildfortschritt, lange Starts und einfarbige
   Bilder werden als Verdacht behandelt, mit Unterdrückung bei Pause/Fokusverlust.
   Weiterhin offen: komfortabler Vergleich mehrerer Buildberichte.

Zusätzlich sind IPS-/BPS-/UPS-Patch Lab und GBA Audio Inspector (PSG + Direct Sound,
Stereo-WAV) eingebaut. [Paket 8–10, Bedienung und Grenzen](WINDOWS_PATCH_LAB_DIAGNOSTICS.md).

**W2 bleibt bis zum Windows-Spieltest zurückgestellt.** Laut Linux-Mitentwickler
laufen Pokémon und die HLE-BIOS-Korrekturen dort. Ein Windows-Durchspielnachweis
fehlt noch; synthetische Tests werden nicht als solcher ausgegeben. Gemeinsame
Core-/Runtime-Änderungen sind mit beiden Frontends zu prüfen.

Die Daten bleiben lokal. Keine ROMs oder BIOS-Dateien im Distributionspaket,
kein automatischer Versand von Berichten. Zur Weitergabe reicht der normale
vollständige Publish-Ordner auf einem USB-Stick.
