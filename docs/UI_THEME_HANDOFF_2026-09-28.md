# UI-Farben und Logo-Abgleich — lokaler Arbeitsstand vom 28. September 2026

## CI-Fehler nach dem Desktop-Umbau — 27.10.2026

Der GitHub-Lauf [40516921562](https://github.com/VoltexModz/AetherBoy/actions/runs/40516921562)
führte alle drei Jobs aus. Die E-Mail-Zeile „No jobs were run“ widerspricht den
Job- und TRX-Berichten: Builds, Core, Runtime und portable Frontend-Logik waren
in diesem Lauf erfolgreich. Acht Windows-Fälle und dieselben acht nativen
Linux-Fälle auf x64 und ARM64 scheiterten.

Korrekturen an den vorhandenen Aether-Oberflächen:

- `frmControlCenter` behält ein vor dem ersten `Shown` gewähltes Ziel bei.
  Der Standard bleibt Aussehen; die verzögerte Initialisierung überschreibt
  beispielsweise einen direkten Aufruf der Übersicht oder Spielstände nicht mehr.
- Die Bibliothek reserviert im gemeinsamen `AetherDialog` genügend scrollbare
  Inhaltshöhe für ihre Spieleliste. Ein kleiner Monitor darf die Liste nicht
  auf null Pixel zusammendrücken. Die bestehende Kachelprobe schaltet ausdrücklich
  in die Kachelansicht und prüft die Liste auch bei 1000×720.
- Einstellungsüberschriften haben Platz für Noto Sans; Beschreibung und
  Unterseiten-Schaltflächen liegen darunter.
- Linux meldet den ausgewählten Spielstandplatz über dessen stabile Fokus-ID
  an AT-SPI, unabhängig von seiner verschobenen Bildschirmposition.

Die betroffenen Tests verwenden aktuelle Navigationsziele und Trefferflächen.
Die Windows-Größenprobe berücksichtigt die tatsächliche Monitorfläche. Der
Linux-Spielstandtest wartet nach dem Schreiben auch die asynchrone Slot-Prüfung
ab, bevor er Laden auslöst. Fehler-, Undo-, Controller-, Unicode- und externe
AT-SPI-Prüfungen bleiben erhalten. Keine CI-Schwelle reduziert, keinen Test
abgeschaltet, `WindowsUiDebt.json` unverändert. Die verbindliche Fensterliste
weiter unten bleibt bestehen.

Lokale Abnahme dieses Fixes:

| Prüfung | Ergebnis |
| --- | --- |
| Release-Build der gesamten Solution, Windows-Targeting auf Linux | 0 Fehler, 0 Warnungen |
| Native Windows-11-VM, gesamte Smoke-Suite bei 1024×768 | 371 bestanden, 5 optionale Fälle übersprungen; 2m 37s innerhalb des bestehenden 4m-Limits |
| Darin Windows-UI-Policy | 3 bestanden, Debt-Datei unverändert |
| Ubuntu-24.04-Container, privater Weston-Compositor und AT-SPI-Bus | 234 bestanden, 5 optionale Fälle übersprungen |
| Bilder und Copy-Prüfung | Bibliothek, Updates und Aufnahmeübersicht betrachtet; Scannerhinweise betreffen unveränderte Texte |

Die Bildkontrolle ergänzte die Aufnahmeprobe um das tatsächliche Scrollen mit
`AetherScrollViewport.Reveal` statt dem wirkungslosen nativen Panel-Aufruf.
Die Schaltfläche wird nun innerhalb des sichtbaren Viewports geprüft. Gezielter
Nachlauf mit Bildaufnahme und UI-Policy: 4 bestanden, keine Skips; diese bereits
enthaltenen Fälle nicht zur Gesamtzahl addieren (`results-reviewed/`).

Die ausgelassenen Fälle benötigen zusätzliche Audio-/Grafik-Hardware oder eine
lokal bereitgestellte Battle-Space-ROM. ARM64 wurde nach der Korrektur nicht lokal
ausgeführt; die identischen ursprünglichen Fehler sind in beiden GitHub-Linux-
Berichten belegt. Der zusätzliche Windows-Lauf mit Screenshot-Export überschritt
beim Spielstand-Rundlauf das 4m-Limit; der abschließende Lauf ohne diesen Export
bestand vollständig. Keine Zeitgrenze wurde dafür erhöht.

Belege: `/tmp/aetherboy-ci-linux-after/`, `/tmp/aetherboy-ci-build-final.log` und
`/home/jessy/Documents/Codex/2026-10-18/gi/work/pdf-vm/shared/aetherboy-ci-20261027/results-verified/`.
Die VM-Auflösung ist wieder 1280×800. Die Änderungen sind lokal geprüft; ein neuer
GitHub-Lauf gehört erst zu einem nachfolgenden Push.

## Vollständiger Einstellungsbereich — 27.10.2026

Nachtrag zum Desktop-Design: Die Aussehen-Seite war zunächst als einzige in der
neuen Navigation umgesetzt. Alle acht Kategorien verwenden jetzt denselben
Einstellungsrahmen auf Windows und Linux: Übersicht, Aussehen, Grafik, Audio,
Steuerung, Spielstände, App und Dateien, Diagnose. Auch Unterseiten und Suche
behalten Navigation, Suchfeld und Fußzeile. Strg+Tab wechselt diese Kategorien;
Esc führt aus Unterseiten zurück in den Einstellungsbereich. „Zum Spiel zurück“
verlässt ihn ausdrücklich. Bestehende Werkzeuge und spezialisierte Eingabeeditoren
bleiben über ihre bisherigen Aktionen erreichbar.

Im bestehenden `AeatherBoy.pen` wurden diese Entwürfe ergänzt. Sie verwenden die
vorhandenen Farbvariablen, Noto Sans und Aether-Komponenten. Die ursprüngliche
Aussehen-Seite `HREE0` zeigt ebenfalls die vollständige Einstellungsnavigation.

| Entwurf | Pen-Knoten |
| --- | --- |
| Bausteine, Fokuszustand, Layoutregeln | `S6f9l` |
| App und Dateien mit allen sechs Zeilen | `ZszGR` |
| Übersicht | `v4Nre` |
| Grafik | `k5Df8` |
| Audio | `l7Fj8` |
| Steuerung | `qyT2F` |
| Spielstände ohne geladenes Spiel | `S3Kce4` |
| Aussehen im gemeinsamen Rahmen | `Luhwn` |
| Desktop und Sprache | `h3ysi` |
| Dateien und Updates | `drxep` |
| Diagnose | `dx2Z1` |
| Kompakte Suche, Pocket Light, 1000×740 | `LROh4` |

PNG-Exporte liegen lokal unter `artifacts/settings-design/`. Die Entwürfe legen
Gruppierung, Zeilen, Zustände und gemeinsamen Rahmen fest. Plattformabhängige
Detailaktionen bleiben in den nativen Aether-Komponenten umgesetzt; beispielsweise
verwendet die native Audioseite weiterhin ihre vorhandene Lautstärkeauswahl.

Der im Nutzerbild belegte Linux-Überlauf ist behoben: Die sechs App-/Dateizeilen
liegen vollständig im Inhaltsbereich, statt die letzte Sprachzeile unter eine
fest begrenzte Hintergrundfläche zu setzen. Aktionen stehen rechts, Texte links.
Auch bei 860×554 und großer Schrift bleiben Zeilen, Navigationsstatus und
Tastaturhilfe oberhalb der Fußzeile. Lange Aktionsnamen können bei dieser Breite
gekürzt erscheinen; die zugehörige Überschrift bleibt lesbar. Unter Windows
verwenden Einstellungsgruppen Trennlinien der gemeinsamen `AetherSurfacePanel`;
der Standardmodus anderer Fenster bleibt erhalten. Zwei Audiobeschreibungen
haben genug Höhe für ihren vollständigen Umbruch in DE/EN und 100/150 Prozent.

Abnahme dieses Nachtrags, separat vom vorherigen Desktop-Lauf:

| Prüfung | Ergebnis |
| --- | --- |
| Release-Build der gesamten Solution | 0 Fehler, 0 Warnungen |
| Native Windows-VM: SettingsDesign, SettingsParity, DesktopDesign, UiPolicy, Theme | 40 bestanden, keine Skips |
| Darin Windows-UI-Policy | 3 bestanden; `WindowsUiDebt.json` unverändert |
| Native SDL-/Wayland-Auswahl: SettingsDesign, DesktopDesign, SettingsNavigation, Language, ShellIntegration, Theme | 24 bestanden, keine Skips |
| Gemeinsamer DE/EN-Katalog und Browserlokalisierung | 19 bestanden, keine Skips |
| Copy-Scanner | Hinweise auf geänderten Zeilen geprüft; nur bestehende Bibliotheks-Metadatenformulierung markiert |
| Linux-Launcher | Frisch veröffentlicht, installiert, 64 Dateien per SHA-256 mit Publish verglichen; Desktop-Datei und Start mit minimalem PATH geprüft |

Die 83 Fälle dieses Nachtrags sind nicht zu den 138 Fällen des ersten Umbaus zu
addieren: Die Auswahlen überlappen. In der Windows-Sprachprüfung wurden nur die
veralteten Navigationsziele durch die tatsächlich vorhandenen stabilen IDs
ersetzt; Sprachpersistenz, globale Wirkung und unveränderte Datenkultur werden
weiter geprüft. Die neue Regression prüft auf 17 Einstellungsseiten denselben
Navigationsbereich, erreichbare Schaltflächen, Suchrouting und Zurücknavigation.
Linux prüft alle acht Kategorien in beiden Sprachen und zwei Fenstergrößen.

Belege: `/tmp/aether-design-vm/results-settings-verified`,
`/tmp/aetherboy-settings-linux`, `/tmp/aetherboy-settings-build-final.log`,
`/tmp/aetherboy-settings-localization-final.log`. DE/EN-Fensterbilder wurden
betrachtet; die VM-Auflösung ist wieder 1280×800 und der temporäre Settings-Testtask
ist entfernt. Die unten dokumentierten Grenzen bei echten Monitor-DPI-Wechseln,
Audiogeräten, IME, Screenreadern und physischer Controller-Hardware gelten weiter.
Die Linux-Launcher-Version liegt in `program/releases/build-mdXaE1Wc`; der
`current`-Link zeigt dorthin. Persönliche Daten und frühere Releases bleiben erhalten.

## Desktop-Design aus pen.dev — 27.10.2026

Vorlage: `AeatherBoy.pen`, Dokument `693f2a67-130d-46bc-9dec-eb9647ff2c81`.
Übernommen wurden Spielansicht (`ntiwx`), Bibliothek (`BeEQz`, `rzz59`),
Aussehen (`HREE0`), kompakte Ansicht (`n8di4F`) und leerer Start (`eggUs`).
Die Datei wurde über die Pen-Schnittstelle gelesen; Beispieltitel und Spielbilder
sind keine ausgelieferten Bibliotheksdaten.

- Windows/WinForms und Linux/SDL verwenden gemeinsame Desktop-Maße und Linienicons.
  Kopfzeile 64, Statuszeile 32, Navigation 184 bzw. 72 bei schmalen Fenstern;
  Spielbild, Speicherzustände und Transport sind getrennte Bereiche.
- Das leere Fenster zeigt Dateiöffnung und Bibliothek. Mit Spiel erscheinen
  die tatsächlichen Sitzungswerte und fünf Speicherplätze im Aether-Dropdown.
  Die bisherige Windows-Menüstruktur bleibt unter „Weitere Aktionen“ erreichbar.
  In sehr schmalen Windows-Spielbereichen liegen Zurückspulen/Turbo im Menü.
- Bibliothek mit Suche, System-/Favoritenfilter, Spielvorschau, Sortierung und
  Tabelle. Bestehende Metadaten, Fortsetzen, Kachelansicht und Patch Lab bleiben
  erreichbar. Linux behält für Kacheln und tiefere Werkzeugseiten die bestehenden
  Funktionsansichten mit den gemeinsamen neuen Farben/Komponenten.
- Aussehen mit sechs Theme-Vorschauen, drei editierbaren Farbfeldern, Größe und
  Sprache. Ein laufendes Status-Refresh überschreibt keine unfertigen Farbeingaben.
  Ungültige Farben überschreiben keine gespeicherten Werte.
- Aether Original verwendet nun den Hintergrund `#0D1019` der Vorlage,
  Pocket Light deren warme Oberflächen. Gespeicherte eigene Farben bleiben
  bestehen. Hauptaktionen behalten die zwei Aether-Wave-Schnitte; weitere
  Schaltflächen und Eingaben verwenden die ruhigeren rechteckigen Flächen.
- Windows lädt die bereits lizenziert mitgelieferten Noto-Sans-Schriften über
  `AddFontResourceEx(FR_PRIVATE)` für GDI und eine prozessweit gehaltene
  `PrivateFontCollection` für GDI+. Die native Windows-VM deckte den zuvor
  unbemerkten GDI+-Fallback auf Microsoft Sans Serif auf; Regular und Bold
  verwenden jetzt die private Schriftfamilie. Keine
  Systeminstallation, kein neuer nativer Dialog. Die gebündelte Schrift und ihre
  Nutzung werden im Desktop-Designtest geprüft. `WindowsUiDebt.json` bleibt
  unverändert; keine Ausweitung oder Abschaltung des Policy-Wächters.
- Die native UIA-Prüfung deckte einen blockierenden Bibliotheksaufruf auf:
  Aether-Buttons führen ihre Accessibility-Aktion nun über die UI-Warteschlange
  aus. Der Aufruf kehrt vor einem modalen Dialog zurück. Verfügbarkeit und
  Auswählbarkeit werden vor dem Einreihen und vor dem Klick geprüft; ein neuer
  Regressionstest prüft modale Öffnung und zwischenzeitliche Deaktivierung.

Die verbindliche Fensterliste unten bleibt bestehen. Für `frmNano`,
`frmRomLibrary` und `frmControlCenter` sind neue Start-/Spiel-/Bibliotheks-/Theme-
Aufnahmen sowie Regressionen ergänzt. Prüfung in DE/EN, Original/Pocket Light,
allen sechs Theme-Wechseln, eigenen Farben und kleinen Fenstern; Windows-Slotrail
zusätzlich bei 100/125/150 Prozent UI-Größe. Linux-Layouttests prüfen
Trefferflächen und Überschneidungen bis 860×554 mit großer Schrift.

Prüfstand dieses Umbaus:

| Prüfung | Ergebnis |
| --- | --- |
| Release-Build der gesamten Solution | 0 Fehler, 0 Warnungen |
| Native Windows-UI-, Theme-, Bibliotheks-, Eingabe-, Sprach- und Workflow-Auswahl | 85 bestanden, keine Skips; einschließlich Direct2D und Fensteraufnahmen |
| Darin `WindowsUiPolicyTests` | 3 bestanden, Debt-Datei unverändert |
| WASAPI-Geräteprobe | Erstlauf fehlgeschlagen: VM hat kein Audiogerät (`0x80070490`); im abschließenden Lauf ausdrücklich ausgenommen, weiterhin offen |
| Native SDL-/Wayland-Auswahl | 26 bestanden, keine Skips |
| Gemeinsame Farb-/Aether-Wave-Tests | 8 bestanden |
| Gemeinsame Lokalisierung | 19 bestanden |
| Copy-Scanner und `git diff --check` | gelesen/geprüft; keine Whitespace-Fehler |

Der neue Scannerhinweis zum Bibliotheksformat `{0} · {1} gespielt` betrifft
System und Spielzeit als Metadatenzeile, keinen Warn- oder Erklärungstext.
Weitere Hinweise betreffen unveränderte ältere Texte und Katalog-Aliase.
Neue literale `UiText.Get`-/`Format`-Quellen wurden gegen den Katalog geprüft.
Die zentrale `AetherHeading` hält lange Spieltitel in einer Zeile, einschließlich
150-Prozent-UI; der vollständige Titel bleibt als Labeltext für Accessibility
verfügbar. Die Navigation überlappt auch dort nicht. Unveränderte Sitzungs-Ticks
lösen keinen erneuten Desktop-Layoutdurchlauf aus.

Nach der Freigabe der Nutzer-VM wurde die Wine-Vorprüfung durch einen nativen
Lauf auf Windows 11 Pro, Build 26200, mit .NET 10.0.14 ergänzt. Die vorhandene
VM `pdf-research-windows` läuft mit dem VirtIO-Grafiktreiber. Testbuild und
portable Live-Instanz liegen getrennt unter `C:\AetherBoy-Design-20261027`;
synthetische ROMs und Testdaten berühren keine persönlichen Spielstände.
Der abschließende TRX-Lauf unter `results-verified` enthält 85 bestandene Fälle.
Die anschließende Bildkontrolle fand eine abgeschnittene englische Tastaturhilfe
im Aktionsmenü. Die Hilfszeile wird jetzt gemessen und die folgenden Bereiche
rücken entsprechend nach. Gezielte Nachprüfung unter `results-menu`: 7/7,
einschließlich DE/EN-Aufnahmen und Policy; keine zusätzlichen Fälle mitzählen.
Mit den unveränderten Linux-/Core-/Runtime-Prüfungen sind dies 138 unterschiedliche
bestandene Fälle; die fehlgeschlagene Audiogeräteprobe ist nicht mitgezählt.

Zusätzliche externe UIA-Prüfung: Start, Einstellungen, Theme-Wechsel, eigene
Farben, Größen-Dropdown, Aether-Textkontextmenü und Bibliothek. Der korrigierte
Bibliotheksaufruf kehrt vor dem modalen Dialog zurück; der Client kann danach
dessen Baum lesen, den Unicode-Suchwert `Äther 日本語 Ω` setzen/zurücklesen und
das Fenster schließen. Der native Editor meldet über TextPattern `Noto Sans`.
Native Aufnahmen in DE/EN und Mindestformaten wurden betrachtet. Bildschirmtests
prüfen jetzt das Vordergrundfenster und warten auf die Desktop-Komposition;
der Direct2D-Test prüft die sichtbaren Frame-Pixel mit begrenzter Wartezeit.
Temporäre Hostbelege: `/tmp/aether-design-vm/results-verified`,
`/tmp/aether-design-vm/results-menu` und
`/tmp/aether-design-vm/live`. Die ursprüngliche virtuelle Auflösung 1280×800
ist wiederhergestellt; alle temporären AetherBoy-Prüftasks sind entfernt.

Nachweisgrenzen: Die VM-Prüfung ersetzt keine echten Monitor-DPI-Wechsel,
WASAPI-Ausgabe mit vorhandenem Audiogerät, reale IME-/Screenreaderbedienung oder
Controller-Hardware. Gemessen wurden 96 DPI und 100/125/150 Prozent app-eigene
UI-Größe, keine physischen Monitorwechsel. SDL läuft nativ unter Wayland. GTK 3 kann
sich hier nicht mit Wayland verbinden (`gtk_init_check` liefert 0); die native
GTK-/AT-SPI-Gesamtprobe bleibt offen. Die SDL-Befehlsnamen und modal getrennten
Auswahllisten werden unabhängig davon geprüft. Keine neue Aussage zur
Core-/Online-Link-Kompatibilität.

## UI-Restarbeiten und zusätzliche Abnahmen — 09.10.2026

- Die sieben Softwarepakete aus dem [UI-Arbeitsplan](UI_REVIEW_PLAN_2026-10-08.md)
  sind umgesetzt. Abschließende unabhängige Fremd-Codeprüfung **9,4/10** und
  betrachtete Linux-Darstellung **9,4/10**; keine offenen P1/P2 im geprüften Delta.
  Die Noten gelten für diesen geprüften Umfang, nicht für eine vollständige
  Plattform-/Gerätefreigabe.
- Linux-Speicherseite neu aufgeteilt: fünf Slots, Fehlerstatus und sämtliche
  Nebenaktionen passen in DE/EN auch bei Mindestformat und Textgröße 18.
  Trefferflächen, Umbruch und Fokusreihenfolge geprüft; alle 18 neuen
  NoGame/Saved/Error-Bilder unabhängig betrachtet.
- Privater Weston-/D-Bus-/AT-SPI-Starter vervollständigt; geerbte Display-FDs
  werden entfernt. Nativer UI-Lauf **61/61** sowie externe Komponenten- und
  Emulator-AT-SPI-Probe jeweils **1/1**, ohne Skips. Der Fremdclient führt echte
  Save/Load-/Undo-Aktionen und Auswahl/Kopieren vollständiger Unicode-Details aus.
  Gemeinsame Lokalisierung **19/19**.
- Echte Orca-Softwareprobe deckte eine P1-Flut unveränderter Namen auf:
  Linux-Code vorübergehend **8,7/10**. Zentraler Guardfix setzt native Namen/Labels
  nur bei Änderung; Regression prüft 120 identische Frames ohne Zusatzsignale und
  echte DE-/Unicode-Änderungen mit Signal. Frische 61+1+1 = **63 unterschiedliche
  Linux-Fälle** grün, danach unabhängiger Codereview wieder **9,4/10**, P1 geschlossen.
  Gleicher Orca-Ablauf spricht nun Bibliothek, Suche, Speicher-/Zustandsaktionen
  und readonly Details bis zur stummen Speech-Dispatcher-Ausgabe. Definierte
  Queueing-Namenslogzeilen 60.156→32; keine künstlichen Sprachaufrufe.
- Windows-Kompatibilitätslauf mit offizieller .NET-10-Runtime unter privatem
  Wine **43/43**, ohne Skips: Policy3, Followup15, Remainder10, Theme11, Sprache4.
  Belegter Slotüberlauf durch die eigene Scrollleiste behoben; Mindestformat bei
  100/125/150 Prozent geprüft. Ein einziges handlegebundenes AccessibleObject
  erhält die Buttonrolle und Disabled-/Selected-Zustände nach Handle-Neuerzeugung.
  Bestehende Assertions, Policy und Schulden-Baseline bleiben unverändert.
- Frische Builds ohne Warnungen/Fehler. Tatsächliche Xvfb-Fensterpixel aus
  Original DE und Pocket Light EN unabhängig angesehen; vollständige Slots und
  zentrale Aktionen. Befunde, Noten und reproduzierbare temporäre Belege stehen
  im Arbeitsplan. Diese Ergebnisse aktualisieren den Prüfstand vom 08.10. unten.
- Keine Windows-VM vorhanden (Nutzerbestätigung 09.10.); echte Windows-Live-/UIA-
  Abnahme, Monitor-DPI-Wechsel, reale IME und physischer Controller bleiben offen.
  Externe AT-SPI- und Orca-Softwareprüfungen ersetzen Hörtest und vollständige
  praktische Screenreaderbedienung am echten Desktop nicht. Orca-Startup- und
  Cleanup-Warnungen der temporären Probe bleiben im Arbeitsplan dokumentiert.

## Kritischer UI-Review und Umsetzung — 08.10.2026

- Arbeitsaufteilung, Abnahme und Reviewrunden stehen im
  [UI-Arbeitsplan](UI_REVIEW_PLAN_2026-10-08.md). Drei Umsetzungspakete und
  voneinander getrennte Fremd-Code-/Visualprüfungen; Abschluss erst über 9,3 in
  beiden Bewertungen. Bei zwei stagnierenden Runden wird der Ansatz gewechselt.
  Runde 2: Code **9,4/10**, betrachtete Linux-Darstellung **9,4/10**; keine offenen
  P1/P2 im geprüften Änderungsumfang. Die praktische Plattformabnahme unten
  bleibt offen und wird durch diese Noten nicht ersetzt.
- Linux: themegebundene Speichertexte, ausdrückliche Slotzustände und ein
  scrollbar lesbarer Detailbereich für vollständige Fehlermeldungen. Details
  schützen laufende Sessions, beide lokale Link-Spieler und Metadatenentwürfe
  über Fokuswechsel hinweg; Tastatur-/Controller-Eingaben erreichen kein
  verdecktes Spiel. Vollständige Texte bleiben auswählbar und kopierbar.
- Bibliothek: flächenabhängige Liste/Kacheln, direkte System-/Genre-/Sortierwahl,
  geometrischer Chevron und lesbare Auswahlwerte bei großer Schrift.
  Vorschaustatus bleibt innerhalb der Bildfläche. GTK-/ATK-Hauptspielbefehle,
  Auswahlsemantik und modale Aktionsgrenzen ergänzt; alte wartende Textbefehle
  dürfen unter einem Popup keinen Entwurf speichern.
- Windows: gemeinsame Fensteraktionen und ausgewählte Buttons mit passenden
  Accessibilitynamen/-zuständen. Deaktivierte, versteckte oder entsorgte Buttons
  werden durch Accessibility nicht ausgelöst. Speicherplätze werden im
  Hintergrund für die jeweilige Sitzung gelesen; Dateipicker verwerfen alte
  Ergebnisse und bleiben bei laufenden Zugriffen abbrechbar. Keine neue
  UI-Schuldenausnahme; `WindowsUiDebt.json` unverändert.
- Frische Builds Linux/Windows samt Testprojekten ohne Warnungen/Fehler.
  Gezielter nativer Wayland-/GTK-Lauf **45/45**, anschließende Bibliotheksprüfung
  mit korrigierter großer Schrift **2/2**, Lokalisierung **19/19**; keine Skips.
  Synthetische Spiele, temporäre Saves und versteckte Fenster mit privatem
  D-Bus. Bilder einschließlich echter sechs Themes, DE/EN, heller Eigenfarben
  und Mindestformat in `/tmp/aetherboy-ui-work-review/` sowie im Visualreview.
- Die Windows-UI-Policy-/Funktionsausführung ist hier wegen fehlender
  Windows-Desktop-Runtime nicht möglich; Windows-Livebilder, UIA und DPI bleiben
  offen. Linux-ATK wird nativ geprüft; ein separater AT-SPI-Client unter einem
  privaten Weston-Compositor fehlt. Echte Controller, IME, Screenreader und
  Monitor-DPI-Wechsel bleiben gesonderte praktische Abnahmen.

## e-Reader-Kartenbibliothek — 08.10.2026

- Nachprüfung: `InvalidDataException` wird jetzt bei Bibliotheksaktionen auf
  beiden Plattformen und beim Windows-Neuladen abgefangen. Ungültige Firmware,
  Kartendateien und Metadaten zeigen die vorhandene Fehlermeldung.
  DE/EN-Regressionen prüfen Sitzungs-/Pause-Erhalt, Picker-/Busy-Freigabe und
  unveränderte Startidentitäten/Resume-Stände. Linux samt Theme-Auswahl **5/5**;
  Fehlerbilder dunkel/hell mit großer englischer Schrift geprüft. Windows-Build
  inklusive Smoke-Tests ohne Warnungen/Fehler; UI-Policy-/UI-Testausführung hier
  wegen fehlender Windows-Desktop-Runtime 10 nicht möglich. Die Schulden-Baseline
  bleibt unverändert; Windows-Laufzeitabnahme bleibt offen.
- Eigene Bibliotheksseite auf Windows mit zentralem `AetherSelect`, `AetherTextBox`,
  Aether-Buttons/Dateiauswahl und themegebundener Bildvorschau. Linux nutzt die
  bestehenden SDL-Eingaben, Schaltflächen und Vorschau. Keine neue native UI-Schuld.
- Import/Ergänzen, freie Titel, eigene Firmware und getrennte Saves/Resume pro Set;
  DE/EN im gemeinsamen Katalog. Noch kein Sammlungs-Organizer mit Metadaten,
  Kartenentfernung oder Versionsauswahl. Bedienung/Grenzen in beiden Roadmaps.
- Windows-e-Reader-/UI-Policy-Auswahl 5/5 und Linux-Wayland-Seiten 2/2 bestanden;
  alle sechs Windows-Themes gerendert, dunkel/hell angesehen. Linux mit großer
  englischer Schrift geprüft; Zeilenumbruch und unterer Hilfeabstand korrigiert.
  Fehleranzeige und zentraler Auswahl-Popup zusätzlich erfasst. Vorschauflächen
  dieser UI-Tests sind bewusst synthetisch, keine behaupteten Spielaufnahmen.
- Belege: `artifacts/ereader/library-ui-windows/` und `library-ui-linux/`.
  Reale DPI, Screenreader, IME, Controller und vollständige manuelle Abnahme aller
  Import-/Dateidialogfälle bleiben offen. Themes/Customize wurden nicht ersetzt.
- Die Scannerseite nennt inzwischen vollständige Standalone-Kartentests; die ältere
  Beschränkung auf „Karte 1“ im nachfolgenden historischen Abschnitt ist überholt.

## e-Reader und Barcode-Status — 08.10.2026

- Neue e-Reader-Werkzeugseite auf Windows mit zentralen Aether-Buttons und
  Aether-Dateiauswahl; Linux mit bestehenden SDL-Komponenten/Dateipicker-Routing.
  Keine neue Windows-UI-Ausnahme, keine Änderung der Schulden-Baseline.
- Import, Warteschlangenzähler, Scans verwerfen, leere/falsche ROM und fehlerhafte
  Datei geprüft. Pause und vorgemerkte Karten bleiben bei Fehlern erhalten.
  Sichtbare Zähler behaupten keine erfolgreich vom Spiel eingelesene Karte.
- Gemeinsame DE/EN-Texte, Themes/Customize unverändert. Renderings in Aether
  Original (DE) und hellen Eigenfarben (EN), Linux zusätzlich große Schrift.
  Abstände nach Sichtprüfung korrigiert, damit der Entwicklungsstand-Hinweis
  nicht mit dem unteren Linux-Status kollidiert. Belege: `artifacts/ereader/`.
- Windows-UI/Policy/Theme/Sprache 23/23; Linux-Zubehör/Theme unter WSLg 11/11.
  Physischer Controller, Screenreader und reale Monitor-DPI bleiben offen.
  Die nachgereichte USA-e-Reader-ROM akzeptiert Karte 1 von Donkey Kong-e und
  Balloon Fight-e; beide UI-Hinweise grenzen dies jetzt vom noch offenen
  vollständigen Spielstart ab. Feedback erklärt das automatische Lesen einer
  später eingelegten Karte bei bereits wartendem Scan. Barcode-Status sagt „Scanner bereit“ statt
  eine erfolgreiche Erkennung durch das Spiel vorzutäuschen.
- Nach den Firmware-Tests e-Reader-Hinweise erneut geprüft: Windows-Auswahl
  aus e-Reader/Policy/Theme/Sprache **34/34**, Linux-e-Reader/Theme unter
  WSLg/Wayland **6/6**. Importbestätigung und Fehlerfall in DE/EN, dunkel/hell
  gerendert; Linux mit großer Schrift. Textprüfung: keine Hinweise in den beiden
  e-Reader-Seiten oder ihren geänderten Katalogeinträgen. Andere Fundstellen im
  Gesamtkatalog sind dadurch nicht als bereinigt erklärt.

## Cheat-Einfügeprüfung — 06.10.2026

- Windows ergänzt ausschließlich zentrale Aether-Bauteile: Prüftabelle,
  scrollbar lesbares Detailmemo, Kandidatenbuttons und Bestätigung für den
  möglichen Platzhalter `AAAA`. Kein nativer Dialog/Widget und keine neue
  UI-Schulden-Ausnahme. Sechs Themes und eigene Farben bleiben wirksam.
- Die Tabellenzeile ist per Enter/Doppelklick in der unveränderten Eingabe
  auswählbar; Überschriften, Beschriftungen, `svg` und unsichtbare Zeichen werden
  nicht heimlich entfernt. Kandidatenwahl setzt den vorhandenen Formatselektor.
- Linux verwendet dieselbe gemeinsame Prüfung, aber seine eigenen SDL-Buttons,
  Texteingabe und Vor/Zurück-Navigation. Fehler bleiben auf der Prüfseite lesbar,
  die endgültige Sitzungsprüfung und Online-Link-Sperren bleiben bestehen.
- Deutsche/englische Windows-Renderings in allen sechs Themes; Original und
  Pocket Light visuell geprüft. Native Wayland-Eingabe-/Prüfseitentests und
  Screenshots unter WSLg. Echte Monitor-DPI-/Screenreader-/Controller-Abnahme
  bleibt wie bisher offen. Keine Zusage zur Wirkung der Cheats im Spiel.

## Cheat-Checkboxen — 06.10.2026

- Windows `frmCheats`: alter Umschaltknopf entfernt. `AetherList` zeichnet eigene
  Checkboxen in der Aktiv-Spalte, ohne native Checkbox-/ListView-Fenster.
  Häkchenzeichnung zentral mit `AetherCheckBox` geteilt; sechs Themes und
  Eigenfarben bleiben erhalten. Klick auf Zeilentext wählt nur aus.
- Leertaste und Accessibility-Standardaktion fordern denselben Sitzungswechsel
  an. Während der Bestätigung sind weitere Änderungen gesperrt; keine vorzeitig
  als aktiv gezeichneten Cheats. Neue Einträge sind wie bisher sofort aktiv.
- Die gesonderte Cheat-Modul-Taste ist ein Aether-Häkchen mit klarer Erklärung.
  Formatfehler behalten jetzt ihre konkrete technische Begründung, bekannte
  Meldungen sind deutsch/englisch angebunden.
- Automatisierte Maus-/Tastatur-/Accessibility- und GB/GBC/GBA-Tests; Renderbilder
  in beiden Sprachen für sechs Themes. Aether Original (DE) und Pocket Light (EN)
  visuell geprüft. Interaktive DPI-/Screenreader-Abnahme und Linux-Checkboxen
  bleiben offen. UI-Schulden-Baseline weder erhöht noch regeneriert.

## Transparente Logos für alle sechs Themes — 03.10.2026

**Für Windows und Linux umgesetzt; mit diesem Änderungspaket versioniert.** Nutzerauftrag:
das eigene AB-Logo farblich an jedes Theme anpassen und den Hintergrund freistellen.
Dieser Nachtrag ersetzt die älteren Aussagen weiter unten, nach denen das Logo
immer undurchsichtig und unabhängig vom Theme bleibt. Der ursprüngliche JPEG-Master
wird weiterhin unverändert aufbewahrt.

- Sechs mit dem Bildwerkzeug bearbeitete transparente PNG-Master in
  `branding/themes/`, genaue Bearbeitungsprompts in `generation.json`.
  Sie sind farbliche/freigestellte Ableitungen, keine pixelidentischen Kopien.
  Pocket Light verwendet dunkles Grün/Bronze auf der hellen Oberfläche.
- Windows und Linux verwenden dieselben 512-px-RGBA-Exporte aus
  `branding/exports/themes/`. Der Export baut nur Größen, keine neuen Bilder.
  `branding/exports/theme-overview.png` zeigt alle sechs auf ihren UI-Flächen;
  nur diese Übersicht hat bewusst einen Hintergrund, die einzelnen PNGs nicht.
- Logo-Auswahl gemeinsam über `UiThemePresets.ResolveBrandVariant` aus den beiden
  Akzenten. Ein eigener Hintergrund behält die passende Akzentvariante.
  Freie, nicht zu einem Preset gehörende Akzentpaare verwenden transparentes
  Aether Original; gespeicherte Farben bleiben exakt erhalten.
- Windows aktualisiert bestehende gebundene Bildfelder im zentralen Theme-Lauf.
  Alte Bitmaps werden freigegeben; keine statischen Control-Event-Abonnements.
  Hauptfenster, leere Spielansicht, Über-Fenster und Sofa-Bibliothek folgen mit.
  Die ausdrücklich als Aether Original beschriftete Wiederherstellungsvorschau
  zeigt weiterhin das Original. Das Standard-Intro lädt das aktuelle Theme;
  eigene Intro-Bilder und Sounds bleiben unangetastet.
- Linux tauscht die gemeinsame SDL-Logo-Textur beim Theme-Wechsel, erst nach
  erfolgreichem Laden der Ersatztextur. Standard-Intro und alle `Mark`-Zeichenstellen
  verwenden diese Textur. Alte Texturen werden freigegeben. Zusätzliche vorglättete
  64-/128-px-Exporte verhindern ausgefranste Rahmen in kleinen Linux-Logos;
  die Texturauswahl berücksichtigt die tatsächliche Rendergröße inklusive DPI.
- Windows-EXE/ICO, Linux-Launcher und Betriebssystem-Fenstericons bleiben bewusst
  die ursprüngliche Markenkennung. Kein Umbau von Speicherständen, Core oder Link.

**Prüfung:** Windows- und Linux-Release-Build jeweils ohne Warnungen/Fehler.
22 gezielte Windows-Tests bestanden (Logos, Alpha, Live-Umschaltung, Bitmap-Lebensdauer,
Themes, Intro und UI-Policy); 8 gezielte Linux-Tests bestanden, ohne Skips,
einschließlich nativer Wayland-Shell, sechs Theme-Wechseln und Intro. Zusätzlich
8 Windows-Theme-Renderfälle bestanden. Bilder in
`artifacts/theme-logos-20261003/`; Original und Pocket Light im Frontend sowie alle
sechs Logo-Varianten visuell geprüft. Das ist eine gezielte Logo-Abnahme,
kein neuer vollständiger Anwendungstest.

## Verbliebene Windows-Bauteile ersetzt — 02.10.2026

**Lokal implementiert, nicht committed/gepusht.** Die Fundliste bleibt darunter
als historischer Bestand und manuelle Abnahmeliste erhalten. Die nach Stufe 1
verbliebenen Bauteile sind jetzt zentral ersetzt, nicht nur anders eingefärbt:

| Bereich | Umsetzung |
| --- | --- |
| WUI-03 Eingaben/Memos | `AetherTextBox` kapselt den nativen EDIT ausschließlich für Unicode/IME, Auswahl, Tastatur und Undo. Rahmen, Fokus, Scrollleisten und das Bearbeitungsmenü werden selbst gezeichnet. Alle 26 TextBox-Stellen und die Diagnose-RichTextBox sind umgestellt. Die Diagnose behält Auswahl und Scrollposition beim Aktualisieren. |
| WUI-05 Datei-/Farbauswahl | `AetherFileDialog` ersetzt die vier Öffnen- und zwei Speichern-Dialogstellen. Navigation über Pfad, Laufwerk, Downloads/Dokumente, Ordner, Tastatur und Controller; Filter und Dateiendungen bleiben erhalten. `AetherColorDialog` bietet RGB-Regler, Zahlenfelder, Hex-Wert, Vorschau und explizites Übernehmen/Abbrechen. Alle drei anpassbaren Themefarben benutzen ihn. |
| WUI-06 Listen/Tabellen | Alle fünf Listenstellen verwenden `AetherList`: Cheats, Backups, ROM-Browser, Bibliothek und Archivauswahl. Eigene Tabellenköpfe, größenänderbare Spalten, Zeilen, Kacheln, Auswahl, Scrollen, Tastatur/Controller und Accessibility-Kinder; kein ListView-/Header-Fenster und kein DrawDefault-Fallback. |
| WUI-08 Bearbeitungsmenüs/Details | `AetherPopup` zeichnet Rückgängig, Ausschneiden, Kopieren, Einfügen, Löschen und Alles auswählen. Passwortfelder geben weder Kopieren/Ausschneiden noch ihren Accessibility-Wert frei. `AetherHint` zeigt lange Listendetails auswählbar und scrollbar im vorhandenen Fenster. |
| WUI-10 Rahmen | Die Eingaben und Listen besitzen eigene Aether-Konturen. Die alte generische TextBox-/RichTextBox-/ListView-Stylinglogik ist entfernt. |
| WUI-11 alte Menüstruktur | `AetherCommand`, `AetherCommandCollection` und `AetherCommandSet` sind reine Befehlsdaten. Keine MenuStrip-/ToolStripMenuItem-/Separator-Erzeugung mehr. Bestehende Aktionen, Verfügbarkeiten, Checkzustände und Untermenüs laufen weiter über `AetherCommandMenu`; das Tastenkürzel-Routing bleibt beim Hauptfenster. Der alte native DarkMenuRenderer entfällt. |
| WUI-12 Rahmen-Fallbacks | Alte Designer starten mit `FormBorderStyle.None`, der Sizable-Rückfallpfad ist entfernt. Raum-/Recovery- und neue Auswahldialoge verwenden die gemeinsame sofort rahmenlose `AetherWindow`-Basis und `AetherDialog`. |
| WUI-13 lange Meldungen | Zu lange Signal-/Sicherheitsmeldungen erhalten ein schreibgeschütztes Aether-Memo. Ihr Ende bleibt erreichbar und kopierbar; Schließen einer Ja/Nein-Frage ergibt weiterhin Nein. |

### Sicherheits- und Verhaltensgrenzen

- Der Dateidialog **wählt nur aus**. Er schreibt weder eine neue Datei noch
  überschreibt er eine vorhandene; auch nicht nach der Überschreibbestätigung.
  Die bisherigen Import-/Export-Routinen behalten Format-, Größen-, Integritäts-
  und Fehlerprüfungen. Abbruch lässt `FileName` und Dateien unverändert.
- Es gibt keine Ordner-Erstellen-/Löschen-/Umbenennen-Funktion im neuen Picker.
  Er zeigt bis zu 5000 Einträge; ein vollständiger eingegebener Pfad bleibt nutzbar.
  Netzwerkpfade werden wie andere Pfade synchron gelesen; langsame Freigaben
  sind ein verbleibender Komfort-/Performance-Testfall, keine neue Zusage.
- Native Texteingabe bleibt als **private verschachtelte** `AetherTextBox.Editor`
  erhalten, ohne Windows-Rahmen, Scrollleisten oder Kontextmenü. Ein vollständiger
  Ersatz von IME/Screenreader durch selbst gezeichnete Buchstaben wäre kein
  gleichwertiger Funktionsersatz. Clipboard-Sperren im Bearbeitungsmenü werden
  als Aether-Meldung angezeigt. Löschen/Ersetzen bleibt im Undo-Verlauf.
- `AetherWindow` ist die bewusst geprüfte neue Plattform-Brücke für Fenster,
  keine unkontrollierte neue Standardoberfläche. Zwei Inventareinträge dokumentieren
  ihre Form-Basis und das Setzen von `FormBorderStyle.None`; Regressionstests
  prüfen sie und die gekapselte EDIT-Brücke separat.
- Baseline gezielt reduziert: **82 auf 35 Gruppen**, **155 auf 35 Gesamtstellen**.
  49 entfernte Gruppen stehen zwei dokumentierten Fenster-Brücken-Gruppen gegenüber.
  Die verbleibenden 35 Stellen bestehen aus 25 Form-Basen und 10 Rahmenzuweisungen;
  keine Standard-Widget-/Datei-/Farb-/Menü-Erzeugungsstellen mehr. Der Wächter bleibt
  aktiv und wurde nicht um Ausnahmen für beliebige neue Standardcontrols erweitert.

### Nachweise und noch offene Abnahme

- Release-Build: **0 Warnungen, 0 Fehler**. Neue Regressionstests in
  `WindowsAetherRemainderTests` prüfen Editor/Undo/Unicode/Passwort, offene
  Bearbeitungsmenüs, initiale Scrollbereiche, Listen-/Kachelauswahl,
  Controller-Ordnernavigation, Dateifilter, verweigertes Überschreiben,
  rein lesende Dateiauswahl, Farb-Abbruch/Validierung und lange Warnungen.
- Abschließender Windows-Smoke-Testlauf: **323 Tests insgesamt, 319 bestanden,
  4 umgebungsabhängig übersprungen, 0 fehlgeschlagen**. Die übersprungenen
  Hardware-/Desktopprüfungen sind kein bestandener Praxistest.
- Bisherige Menütests prüfen weiterhin Klicks, Checkzustände und Untermenüs.
  Die obsolete native DropDown-Objektprüfung wurde durch Prüfung des tatsächlichen
  Fensterbaums **und** der kompilierten Befehls-Konstruktoren ersetzt.
- Neue Komponenten in allen sechs Themes gerendert; Original/Pocket Light,
  Bearbeitungsmenü, Dateiauswahl und RGB-Farbauswahl visuell geprüft. Bilder liegen lokal in
  `artifacts/aether-complete-ui-20261002/`. Geöffnete Bearbeitungsmenüs werden
  zusätzlich separat gerendert, da das Fenster-DrawToBitmap die Popup-Z-Reihenfolge
  nicht zuverlässig wiedergibt.
- Neue sichtbare Texte: Copy-Prüfung ohne Treffer. Der zusätzliche breite Scan
  nennt 59 Hinweise in unveränderten bisherigen Oberflächentexten; das ist keine
  abgeschlossene sprachliche Überarbeitung des gesamten Produkts.
- **Noch nicht vollständig abgenommen:** sämtliche 26 bisherigen Fenster und
  Neben-/Fehlerzustände mit echter Maus/Tastatur/Controller-Hardware, echte
  100/150/200-%-Monitor-DPI-Wechsel, IME-Komposition und Screenreader. Ebenso
  das Systemmenü-Verhalten (Alt+Leertaste) und langsame/unzugängliche Netzwerkpfade.
  Strukturtests und Renderings beweisen nicht, dass jeder Laufzeitzustand perfekt ist.
- Keine Linux-Produktivdateien geändert. Themes und Customize, ROMs, Spielstände,
  Emulationskerne, Online-Protokolle und Linux-Funktionalität sind unverändert.

## Aether-Bedienelemente: Umbaustufe 1 — 02.10.2026

**Lokal umgesetzt, nicht committed/gepusht.** Dies ist der erste Implementierungs-
schritt auf Basis der unverändert nachvollziehbaren Fundliste unten. Es ist noch
keine Erklärung, dass sämtliche Standardelemente verschwunden wären.

| Fundbereich | Aktueller Implementierungsstand |
| --- | --- |
| WUI-01: Container-Scrollen | `AetherScrollViewport` und `AetherScrollBar` ersetzen sämtliche bisherigen `AutoScroll`-Container: Dialogkörper, alle Einstellungsseiten, Suchergebnisse, Controller-Seiten, Befehlsmenüs und State-Galerie. Horizontales/vertikales Scrollen, Mausrad, Ziehen, Tastatur, Controller und Fokus-Nachführen sind enthalten. **Interne** Textfeld-/Tabellen-Scrollleisten gehören weiterhin zu WUI-03/06. |
| WUI-02: Dropdowns | Alle neun Erzeugungsstellen verwenden `AetherSelect`: Bibliotheksfilter, Sortierung, Genre/Bewertung, Laufwerke, Cheatformat und beide Slot-Auswahlen. Feld und aufklappende Liste werden selbst gezeichnet; kein nativer ComboBox-/ListBox-Popup. Pfeile, Home/End, PageUp/Down, Anfangsbuchstaben, F4/Alt+Pfeil abwärts, Enter, Escape, Mausrad und Controller sind angebunden. |
| WUI-03: Checkboxen | Alle fünf Stellen verwenden `AetherCheckBox`; Haken, Zwischenzustand, Hover, Fokus und deaktivierter Zustand sind Aether-gezeichnet. Texteingaben/Memos samt Kontextmenüs bleiben offen. |
| WUI-04 und WUI-07 | Die Raumdialog-Buttons verwenden `AetherButton`; GBA Direct Sound verwendet wie die übrigen Audiogruppen `AetherGroupBox`. Vorhandene Click-Handler, Zustimmungen und Sicherheitsprüfungen bleiben bestehen. |
| WUI-09 | Die punktierten Windows-Fokusrechtecke von Buttons und Listenkacheln sind durch die gemeinsame Aether-Kontur ersetzt, Fokus wird nicht versteckt. |
| WUI-10: Farbvorschau | `AetherColorSwatch` zeichnet den Vorschaurahmen selbst. Die eigentliche Farbauswahl und Text-/Listenrahmen sind weiterhin offen. |

### Architektur und Funktionsgrenzen

- `AetherScrollViewport` bewegt einen geclippten Inhaltscontainer. Es aktiviert
  keine nativen `WS_VSCROLL`/`WS_HSCROLL`-Leisten. Statische und dynamische
  Flow-Layouts werden vermessen; verkleinerte Inhalte setzen ungültige Offsets
  zurück. Die vorhandenen Controls und ihre Ereignisse werden weiterverwendet.
- `AetherSelect` öffnet eine Liste **im vorhandenen Fenster**, begrenzt auf dessen
  Fläche. Ein Klick außerhalb, Escape, Deaktivierung, Größenänderung und Dispose
  schließen sie und lösen die Nachrichtenfilter. Eine markierte Zeile wird erst
  mit Bestätigung übernommen; Controller-Zurück schließt zuerst die Liste, nicht
  das komplette Fenster. Alt+F4 bleibt beim Betriebssystem/Fenster.
- Die CheckBox-Basisklasse ist bewusst eine interne Plattform-Brücke für
  Checked/CheckState, Tastatur und Accessibility. `OnPaint` zeichnet sämtliche
  sichtbaren Zustände selbst; kein eingefärbter Standardhaken. Select/Scrollbar
  stellen eigene Accessibility-Rollen, Werte und Auswahlaktionen bereit.
  Ein Test dieser Rollen ersetzt **keinen** manuellen Screenreader-Durchlauf.
- Die sechs Themes und frei gespeicherten Farben bleiben unangetastet. Farben
  werden aus `AetherColors` bezogen; auch geöffnete Listen werden bei einem
  Themewechsel invalidiert. Keine Änderung an Spielständen, Kernen oder Netplay.
- Keine Linux-Produktivdateien in dieser Umbaustufe geändert. Linux verwendet
  weiterhin eigene SDL-Widgets; die gemeinsamen Theme-/Geometrieregeln gelten
  dort ebenso. WinForms-Klassen werden nicht auf Linux übertragen.

### Offene nächste Umbaustufen

1. Zentrale Eingabe/Memo-Komponente mit eigenen Rahmen, Bearbeitungsmenüs und
   Scrollleisten. Unicode/IME, Auswahl, Undo, Passwortfelder, Zwischenablage und
   Controller-Tastatur erhalten; keine Einzellösungen pro Fenster.
2. Tabellen/Kacheln inklusive Header, Scrollen, Tooltip und Auswahl vollständig
   kapseln, danach die alten generischen Styling-Fallbacks abbauen.
3. Eigene Öffnen-/Speichern-/Farbauswahl mit denselben Dateifiltern,
   Pfadprüfungen, Überschreibbestätigungen und Abbruchgarantien.
4. Versteckte Menü-Datenstruktur und Rahmen-Fallbacks auflösen sowie sämtliche
   26 Fenster-/Dialog-Einträge unten visuell und mit echter Eingabe abnehmen.

Die Bestandsbaseline wurde **nur reduziert**, nicht neu erzeugt: von 105 auf
82 Methoden-/Typgruppen, von 157 auf 131 statische native API-Aufrufe (zuzüglich
unverändert 24 Form-Unterklassen). Die 26 entfernten Aufrufe sind 9 ComboBox-,
5 CheckBox-, 2 Button-, 1 GroupBox-Konstruktor, 6 AutoScroll-, 2 Fokusrechteck-
und 1 nativer Farbvorschau-Rahmenaufruf. Dies zählt Quellstellen, nicht sichtbare
Instanzen oder verbliebene Fehler.

Neue Tests in `WindowsAetherWidgetsTests`: Auswahl-/Änderungsereignisse,
Sammlungsänderungen, Tastatur/Controller/Abbruch, Popup-Begrenzung und Lebensdauer,
nicht anklickbare unsichtbare Restzeilen, Checkbox-Zustände/Accessibility,
Scrollbar-Ziehen/Tastatur/Werte, beidseitiges Scrollen/Fokus/Resize, dynamisches
Flow-Layout und sechs Theme-Renderings. Screenshots in
`artifacts/aether-widgets-20261002/` (lokal/ignoriert).

### Validierung dieser Umbaustufe

- Release-Build der Windows-Anwendung und Smoke-Tests: **0 Warnungen, 0 Fehler**.
- Abschließender vollständiger Windows-Smoke-Lauf: **309 bestanden, 4 übersprungen,
  0 fehlgeschlagen** (313 gesamt). Die vier Ausnahmen betreffen Direct2D-/WASAPI-
  Hardware, Live-Screenshot und echte Vordergrund-Tastatureingabe in der Testumgebung.
- Gezielter Lauf für Widgets, UI-Bestandsschutz und Einstellungspersistenz:
  **20 bestanden**. Copy-Prüfung der neuen Komponenten: keine Prüfpunkte;
  `git diff --check` für die bearbeiteten Bereiche ohne Fehler.
- Vor dem Abschluss blieben zwei Gesamtläufe in einem modalen Einstellungs-
  Fehlerdialog hängen und wurden gezielt beendet; sie zählen nicht als bestanden.
  Der Stack zeigte `ReportSettingsSaveFailure` während eines Theme-Tests.
  Ein neuer Paralleltest reproduzierte zusätzlich das Lesen während des Datei-
  Ersetzens. `WindowsSettingsProvider` stimmt Laden und Speichern nun über dieselbe
  Sperre ab und öffnet Leser mit `FileShare.Delete`. Drei parallele Leser prüfen
  vollständige Snapshots während 80 Speicherungen. Fehlerdialoge wurden weder
  unterdrückt noch ihre Prüfungen entfernt. Testdaten liegen isoliert im Tempordner.
- Repräsentative Fenster und alle sechs neuen Widget-Theme-Renderings geprüft.
  Echte Monitor-DPI-Wechsel, IME und Screenreader sowie sämtliche Fensterzustände
  bleiben als manuelle Abnahme offen; ein erfolgreicher Build ersetzt sie nicht.

## Logo-Nachtrag — 02.10.2026

Das vom Nutzer fertig gestaltete AB-Logo wird unverändert als Master-JPEG unter
`branding/aetherboy-logo-2026-10-02.jpg` aufbewahrt. Die vorhandene Export-Pipeline
erzeugt daraus PNG-Größen und das Windows-ICO; Windows und Linux verwenden
dasselbe 512-px-Bild. Historische SVGs bleiben erhalten, sind aber nicht mehr die
aktive Exportquelle. Keine Neugestaltung oder Bildgenerierung.

Der sichtbare Überspringen-Button der Startanimation wurde auf Nutzerwunsch in
beiden Frontends entfernt, einschließlich der Linux-Maus-Trefferfläche. Enter,
Leertaste, Escape und die vorhandenen Controller-Wege bleiben bestehen. Der
zusätzliche einfache Schriftzug entfällt, weil das neue Logo ihn bereits enthält.
Das Bild ist nicht transparent; sein dunkler Hintergrund gehört zur gelieferten
Vorlage. Alle sechs Themes, eigene Farben und Creator-Intro-Dateien bleiben erhalten.

Release-Builds beider Frontends: keine Fehler/Warnungen. Vollständige Windows-
Smoke-Suite: 324 bestanden, 4 Hardware-/Foreground-Skips, keine Fehler; darin
UI-Policy und Intro-Renderings in allen sechs Themes. Die drei Linux-Introtests
unter WSLg/Wayland bestanden ebenfalls. Testbilder:
`artifacts/logo-update-20261002/`. Keine Aussage über neue Core-/Link-Kompatibilität.
Noch kein Commit/Push.

## Windows-Standardelemente: verbindliche Fundliste — 02.10.2026

**Nutzerauftrag:** Die bisherige Liste behalten, tiefer prüfen und künftig nur
noch unsere eigenen sichtbaren Komponenten verwenden. Diese Liste ist die
verbindliche offene UI-Merkliste. Der vorherige Aether-Wave-Umbau hat gemeinsame
Buttons/Panels wiederhergestellt, **nicht sämtliche Standardcontrols ersetzt**.
Die folgenden Fundstellen dokumentieren den **Ausgangsbestand vor Umbaustufe 1**.
Der aktuelle Erledigungsstand steht darüber; die ursprünglichen Fundstellen
bleiben als Abnahmecheckliste erhalten.

### Umfang und Nachweisgrenzen

- Quellprüfung sämtlicher Windows-Formklassen samt Designer-/Partial-Dateien,
  gemeinsamem Styling, dynamischen Dialogen, Menüaufbau und Systemdialog-Aufrufen.
  24 benannte Formklassen einschließlich `AetherSignalDialog`, zusätzlich die
  dynamischen Fenster `ShowOnlineRoomDialog` und `ShowOnlineSaveRecovery`.
- Zusätzlich kompilierten Windows-Code ohne UI-Start nach nativen Erzeugungen
  und ausgewählten UI-APIs durchsucht: **81 Methoden-/API-Gruppen, 157 statische
  Aufrufstellen**, außerdem 24 direkte Form-Unterklassen (zusammen 105
  Baseline-Einträge). Darin 49 Konstruktorstellen für Standard-Bedienelemente,
  7 System-Datei-/Farbdialogstellen und 71 Stellen der noch vorhandenen, versteckten
  Menü-Infrastruktur, zwei dynamische Form-Konstruktoren. Der Rest sind Scroll-,
  Tooltip-, Rahmen- und Fokusaufrufe. Das sind **keine 157 sichtbaren Fehler oder Control-Instanzen**: Eine Factory
  kann mehrere Controls erzeugen, andere Aufrufe schalten natives Aussehen ab.
- Das ist ein vollständiger aktueller **Codebestand innerhalb dieses Umfangs**,
  keine Behauptung, jeden Laufzeitzustand visuell gesehen zu haben. Aufklappmenüs,
  Rechtsklick, Touch-/IME-Zustände, lange Listen, DPI-Wechsel und Fehlerzustände
  müssen bei der jeweiligen Migration zusätzlich visuell geprüft werden.

### Bisherige sechs Fundbereiche — beibehalten und präzisiert

| ID | Offen | Konkrete Fundstellen und Ziel |
| --- | --- | --- |
| WUI-01 | Scrollleisten | `Controls/AetherDialog.cs:Apply` verwendet einen nativen AutoScroll-Viewport für fast alle Dialoge. Zusätzlich `frmControlCenter.cs:CreatePage`, Suche in `frmControlCenter.Navigation.cs`, `frmControls.Sections.cs`, `frmStateGallery.cs` und `Controls/AetherCommandMenu.cs`. Ein zentraler Aether-Scrollbereich muss vertikales/horizontales Scrollen, Mausrad, Drag, Fokus-Nachführen und Controller übernehmen. Weiße Scrollleisten auch bei kleinen Fenstern/hoher Skalierung entfernen. |
| WUI-02 | Dropdowns | Neun `ComboBox`-Erzeugungsstellen: GBA-Cheatformat, drei Bibliotheksfilter/-sortierungen, Genre/Bewertung, Laufwerksauswahl, Quick-Menü-Slot und Sofa-Menü-Slot. Eigene Aether-Auswahl samt **geöffneter Liste**, Scrollen, Fokus, Auswahl und Abbruch; flacher Rahmen allein reicht nicht. |
| WUI-03 | Checkboxen und Eingaben | Fünf Checkboxstellen: Live-Refresh im Audio-Inspector, Online-Link-Zustimmung, Probe-Bericht, Bibliotheksfavoriten, UPS-Richtung. 26 TextBox-Erzeugungsstellen plus eine RichTextBox-Factory: Suche, Kürzel, Cheats, Raumdaten, Titel/Tags, Logs/Infos, Controller-Tastatur, Discord-ID, Patchpfade. Aether-Schalter und Aether-Eingaben inklusive Auswahl, Passwortmaskierung, Platzhalter, Validierung und Mehrzeilenfall. |
| WUI-04 | Standardbuttons | Zwei `new Button`-Stellen in `frmNano.OnlineRooms.cs`: lokale Button-Factory und „Server speichern“. Die Factory erzeugt mehrere Schaltflächen. `AetherDialog.Style` färbt sie nur flach ein; sie bekommen dadurch **keine** AetherButton-Geometrie. Auf `AetherButton` umstellen, ohne DialogResult, Accept/Cancel oder asynchrone Sperren zu ändern. |
| WUI-05 | Datei-/Farbauswahl | Vier OpenFileDialog-Stellen: Batterie-SAV importieren, Firmware importieren, Partner-ROM im Local Link, Patch wählen. Zwei SaveFileDialog-Stellen: Audio-WAV und Testerbericht exportieren. Ein ColorDialog in `AddThemeColorCard`, von allen drei Farbfeldern benutzt. Eigene Öffnen-/Speichern-/Farbauswahl, bestehende Dateifilter, Pfadprüfung und Überschreibschutz beibehalten. |
| WUI-06 | Listen/Tabellen | Fünf ListView-Stellen: Cheats, Batterie-Backups, Bibliothek, ROM-Browser und Archivauswahl. Header, Zeilen und Kacheln sind teilweise selbst gezeichnet, aber Steuerung/Scrollen/Fokus/Popup-Verhalten bleiben nativ. `StyleListView` fällt bei Ansichten außer Details/LargeIcon auf `DrawDefault` zurück. Eigene vollständige Aether-Liste für beide verwendeten Ansichten, nicht nur neue Zeilenfarben. |

Dateinamen dieser Tabellen sind relativ zu `nanoboy/`, Methodennamen bleiben
auch nach dem Verschieben von Quellzeilen als Suchanker nutzbar.

### Zusätzlich tiefer gefunden

| ID | Status | Befund und Folgeschritt |
| --- | --- | --- |
| WUI-07 | Offen, im Code bestätigt | `frmAudioTool.cs:ConfigureAetherLayout` erzeugt für **GBA Direct Sound** einen normalen `GroupBox`. Die älteren fünf Kanalgruppen sind bereits `AetherGroupBox`. Den Nachzügler auf denselben Baustein umstellen. |
| WUI-08 | Teilweise bestätigt, Laufzeit prüfen | Cheats aktiviert ausdrücklich `ListView.ShowItemToolTips`. Bibliothek setzt `ToolTipText`, aktiviert damit allein aber noch keine sichtbaren Tooltips. TextBox/RichTextBox können ihre Windows-Kontextmenüs mitbringen; Rechtsklick, Shift+F10 und Passwortfelder separat prüfen. Aether-Hinweise und Bearbeitungsmenüs vorsehen; niemals geheime Felder per Tooltip offenlegen. |
| WUI-09 | Offen, im Code bestätigt | `AetherButton.OnPaint` nutzt noch `ControlPaint.DrawFocusRectangle`; die ListView-Kachelansicht `DrawListViewItemEventArgs.DrawFocusRectangle`. Eigene Aether-Fokusmarkierungen einführen, **sichtbaren Fokus nicht entfernen**. Alle sechs Themes sowie sehr helle/dunkle Eigenfarben prüfen. |
| WUI-10 | Offen, im Code bestätigt | Die Farbvorschau in `AddThemeColorCard` nutzt `Panel.BorderStyle.FixedSingle`. TextBox-/RichTextBox-/ListBox-/ListView-Styling setzt ebenfalls native FixedSingle-Rahmen. Eigene gezeichnete Rahmen und Fokus-/Fehlerzustände; keine Doppelrahmen. |
| WUI-11 | Versteckte Altstruktur, nicht als sichtbarer Fehler zählen | `frmNano.Designer.cs` und Menü-Ergänzungen erzeugen MenuStrip/ToolStripMenuItem/Separator. `InitializeAetherShell` versteckt die Leiste, `AetherCommandMenu` zeigt eigene Buttons und verwendet die alten Einträge nur als Befehlsdaten. `DarkTheme.DarkMenuRenderer` ist ebenfalls noch vorhanden. Kein direktes Standard-Dropdown-Öffnen gefunden. Später auf ein plattformneutrales Befehlsmodell umstellen; bis dahin keine neue sichtbare native Menüoberfläche daraus öffnen. |
| WUI-12 | Fallback/Initialzustände, nicht als beobachteter Fehler ausgeben | Sechs alte Designer setzen klassische FormBorderStyles, anschließend ersetzt `AetherDialog.Apply` diese durch None. `frmNano.ResizeWindow` enthält außerdem Sizable als Rückfallpfad, wenn die Aether-Shell noch nicht initialisiert ist; die aktuelle Shell nimmt den anderen Zweig. Initialisierung, Wiederherstellen, Alt+Leertaste, Minimieren, Maximieren und DPI-Wechsel visuell abnehmen. Eigene Rahmen müssen auch bei Fehlern gelten. |
| WUI-13 | Prüfschuld | Fehlermeldungen nutzen bereits `AetherSignal`, nicht direkt `MessageBox.Show`. Trotzdem alle Buttons/Abbrüche, lange Sicherheitsmeldungen, Read-only-Textauswahl und kleine Bildschirme testen. Abgeschnittene Warnungen sind nicht durch ein schönes Panel erledigt. |
| WUI-14 | Bereits eigenes Rendering, keine pauschale Neuerstellung | Spielanzeige, `WaveDataControl`, `LevelDisplayControl`, Statuspunkt, Signal-Glyph, AetherGroupBox, AetherButton und AetherSurfacePanel werden selbst gezeichnet. Die nativen Basisklassen allein sind hier kein Stilbruch. Diese Controls in die Theme-/Fokus-/Skalierungsabnahme aufnehmen. |

### Fensterliste zur späteren Abnahme

Jede Zeile bleibt offen, bis Grundzustand **und** betroffene Neben-/Fehlerzustände
abgenommen sind. Ein Aether-Fensterkopf allein ist kein Fertig-Kriterium.

| Fenster / Einstieg | Noch zu berücksichtigen |
| --- | --- |
| `frmNano` / Hauptfenster | Aether-Shell vorhanden; Fokusmarkierung, Menüs/Untermenüs, kleine Größen, F11, Alt+Leertaste und Rahmen-Fallback. |
| `frmControlCenter` | Alle Seiten/Unterseiten: Scroll-Viewport, Suchfeld/-ergebnisse, Discord-ID, Diagnoseausgabe, Farbfelder/-vorschau/-dialog. Diagnose-RichTextBox bekommt nach dem Aufbau bereits ScrollBars.None; nicht fälschlich als aktuell sichtbaren eigenen Scrollbalken melden. |
| `frmControls` | Acht Designer-Tastenfelder plus zusätzliche Binding-Factory, Controller-Scrollseite, Aufnahme einer neuen Taste, Fokus und Gerätewechsel. |
| `frmCheats` | Tabelle inklusive breiter Codes, Tooltips, mehrzeilige Eingabe, Format-Dropdown für GBA, Fehler-/Sperrzustände. GB/GBC/GBA separat ansehen. |
| `frmAudioTool` | Live-Refresh-Checkbox, GBA-Gruppenrahmen, WAV-Speichern, Scrollen im verkleinerten Dialog; eigene Pegel/Wellenformen behalten. |
| `frmBatterySaveManager` | Backup-Tabelle, Import-Dateiauswahl, Auswahl/Fokus, inkompatible/gesperrte Saves und Wiederherstellungsbestätigung. |
| `frmFirmwareManager` | Import-Dateiauswahl und Ersatzbestätigung, fehlende/ungeeignete Datei, Dialog-Viewport. |
| `frmLocalLinkLab` | Partner-ROM-Dateiauswahl, beide Spielansichten, lange Namen, fokussierter Spieler, Fehler-/Ladezustände. |
| `frmLink` | Alter Host/Client-Dialog mit IP-TextBox. `menuLinkCable` führt aktuell ins Local-Link-Lab; alter Dialog ist trotzdem als Restbestand erfasst. |
| dynamisch: `ShowOnlineRoomDialog` | Standardbutton-Factory, Server speichern, drei Textfelder, Zustimmung, Passwortmaske, Raum-/Verbindungsfehler. |
| `frmOnlineConnectionTest` | Textfeld-Factory für URL/Schlüssel/Code/Ergebnisse, Berichtscheckbox, Ergebnis-Scrollbar, Abbruch/Schließen während eines Tests. |
| dynamisch: `ShowOnlineSaveRecovery` | Schon Aether-Buttons; Viewport, lange Pfade/Begründungen, gesperrte Kopien und Bestätigungen vollständig lesbar. |
| `frmRomLibrary` | Drei Dropdowns, Favoritencheckbox, Suche, Listen-/Kachelansicht, Auswahl/Focus/Tooltips und lange Titel. |
| `frmRomBrowser` | Trotz eigenem Fenster noch Standardliste und Laufwerks-Dropdown; daraus keine unveränderte native Grundlage für den neuen Dateidialog machen. |
| `frmArchiveChoice` | ROM-Tabelle, mehrzeiliger Eintragsname samt Scrollleiste, Auswahl/Abbruch, sehr lange Archivpfade. |
| `frmRomMetadataEditor` | Genre/Bewertung als Dropdown, Tag-Textfeld und Validierungsfehler. |
| `frmRomTitleEditor` | Titel-Textfeld, Zeichenlimit, leere Eingabe, Speichern/Abbrechen. |
| `frmRomPatcher` | Patch-Dateidialog, Textfeld-Factory, UPS-Checkbox, schreibgeschützte Pfade und lange Meldungen. |
| `frmQuickMenu` | Speicherplatz-Dropdown, Maus/Tastatur/Controller, deaktivierte Aktionen. |
| `frmSofaQuickMenu` | Speicherplatz-Dropdown, große Schrift, Controllerfokus, Dialog-Viewport und laufende Operation. |
| `frmSofaLibrary` | Eigene Vollbildoberfläche vorhanden; Karten/Tab-Fokus, leere/fehlende Spiele, Rückkehr und DPI. |
| `frmStateGallery` | Native FlowLayout-Scrollfläche, Miniaturen, Auswahl, leere Slots und Dateifehler. |
| `frmAbout` | Mehrzeilige TextBox mit Scrollleiste, Textauswahl/Kontextmenü, lange Lizenz-/Infotexte. |
| `frmChangelog` | Mehrzeilige TextBox mit Scrollleiste, Tastatur/Kontextmenü, langes Änderungsprotokoll. |
| `frmControllerKeyboard` | Schreibgeschütztes natives Textfeld; Fokus, Cursor/Anzeige und Bearbeitungsmenü prüfen. Tasten sind bereits eigene Buttons. |
| `AetherSignalDialog` | Eigene Symbole/Buttons vorhanden; alle Bestätigungsvarianten und lange Warn-/Fehlertexte auf kleinen Bildschirmen abnehmen. |

### Regeln für neue UI und Reihenfolge des Umbaus

1. **Gemeinsame Bausteine zuerst:** Aether-Scrollbereich, Eingabe/Memo samt
   Bearbeitungsmenü, Auswahlfeld samt Popup, Toggle/Checkbox, Liste/Kacheln,
   Fokusmarkierung, Tooltip und Datei-/Farbauswahl. Die Namen beschreiben hier
   Zielkomponenten, nicht bereits fertig implementierte Klassen.
2. Zuerst die wiederkehrenden Elemente in Einstellungen, Bibliothek, Schnell- und
   Sofa-Menü ersetzen; danach Cheats, Audio, Save-/Firmware-/Patch-/Link-Tools.
   Fehlerdialoge und seltene Nebenpfade nicht ans Ende einer unbestimmten Merkliste
   schieben: je Fenster direkt mit abnehmen.
3. Datei-/Farbdialoge inklusive Dateinavigation, Filter, Archiv-/ROM-Unterscheidung,
   nicht existierenden/unzugänglichen Pfaden, bestehender Zieldatei, Abbruch,
   Zugangsfehlern und Tastatur-/Controllerbedienung fertigstellen.
4. Nur bei tatsächlichem Ersatz offene Fundstellen streichen und die unten
   beschriebene Baseline reduzieren. Versteckte Legacy-Menüdaten erst entfernen,
   wenn Befehle, Shortcuts und Tests auf ein gemeinsames Modell übertragen sind.
5. Screenshots/Interaktion aller Fenster mit Aether Original, Pocket Light und
   eigenen Extremfarben; übrige vier Themes mitprüfen. 100–150 % App-Skalierung,
   mindestens 100/150/200 % Windows-DPI, minimale Größe und Monitorwechsel.
   Hover, gedrückt, ausgewählt, deaktiviert, Fokus, Dropdown offen, Rechtsklick,
   leere/lange/fehlerhafte Inhalte, Suchtreffer und Passwortfelder gehören dazu.
6. Linux verwendet denselben visuellen Vertrag und eigene SDL-Komponenten.
   Windows-Controls nicht portieren. Hier erfasste Windows-Fundstellen sind kein
   vollständiger Linux-Audit; die Wayland-Abnahme bleibt separat nachzuweisen.

**Kein Funktionsverlust zugunsten der Optik:** Unicode/IME, Zwischenablage,
Maus-/Tastaturauswahl, Undo, Passwortschutz, Enter/Escape, Tab/Shift+Tab,
Controller, Screenreader-Rollen und Warntexte bleiben erhalten. WinForms kann
weiter die Plattform sein. Ein natives Text-Backend darf nur innerhalb einer
vollständig gestalteten, zentralen Aether-Komponente verwendet werden.
Einfache themegebundene Labels/Bilder und unsichtbare Layoutcontainer sind keine
zu imitierenden Windows-Dialoge. Betriebssystem-eigene Sicherheitsabfragen,
IME/Hilfstechnologie-Fenster und ausdrücklich geöffneter Explorer/Browser bleiben
außerhalb unserer Gestaltung; insbesondere kein nachgebautes UAC-Fenster.

### Automatische Grenze gegen neue Standardcontrols

`WindowsUiInventory` untersucht die **kompilierte Windows-Assembly**:
Konstruktoren, Lambdas, lokale Factories, async-Methoden, Designer-Code und auch
`new()`/Typ-Aliase. Auch neue direkte Form-Unterklassen und dynamische `new Form`
werden erfasst, damit ein neues Fenster nicht unbemerkt den Aether-Rahmen umgeht.
Es startet keine UI, Datei- oder Netzwerkdialoge.
`WindowsUiPolicyTests` vergleicht das Ergebnis mit
`tests/AetherBoy.SmokeTests/WindowsUiDebt.json`. Bestehender Bestand wird nicht
nachträglich als fertig erklärt. Neue/größere oder entfernte/verschobene Gruppen
lassen den Test fehlschlagen; beim Ersatz muss die Baseline bewusst schrumpfen.
Compiler-Ordinalzahlen sind normalisiert, Quellmethoden und lokale Factorynamen
bleiben erhalten. So verursacht eine zusätzliche, unabhängige Methode allein
keine Neunummerierung sämtlicher Fundstellen.

```powershell
dotnet test --project tests/AetherBoy.SmokeTests/AetherBoy.SmokeTests.csproj `
  -c Release --filter FullyQualifiedName~WindowsUiPolicyTests
```

Die vorhandene Windows-CI führt das gesamte Smoke-Test-Projekt bereits aus und
nimmt diese Tests dadurch mit. **Baseline nicht blind regenerieren oder erhöhen.**
Eine notwendige Plattform-Brücke innerhalb eines Aether-Bauteils braucht eine
begründete, dokumentierte Ausnahme und eigene Tests.

Grenzen: Ein Test erkennt nicht jede optische Abweichung. Reflection/dynamisch
nachgeladene Controls, P/Invoke-erzeugte Fenster, intern von Windows erzeugte
Unterfenster und neue selbstgeschriebene, aber hässliche Widgets werden nicht
automatisch vollständig erfasst. Der Test zählt nach Methode/API, nicht pro
Bildschirminstanz; Austausch einer Stelle gegen eine andere innerhalb derselben
Gruppe ist kein vollständiger Änderungsnachweis. Deshalb bleiben Quellenreview,
Fensterliste und visuelle Abnahme verbindlich. Keine aktuell vorhandenen
RadioButton-, TrackBar-, NumericUpDown-, ProgressBar-, TabControl-, TreeView-,
DataGridView-, FolderBrowserDialog-, FontDialog-, TaskDialog- oder direkten
MessageBox.Show-Erzeugungs-/Aufrufstellen gefunden; manche dieser Typen kommen
nur in generischer Eingabebehandlung vor. Der Wächter erfasst neue native
Controls und CommonDialogs dieser Art ebenfalls.

### Validierung dieser Bestandsaufnahme

- Windows-Smoke-Projekt im Release gebaut: **0 Fehler, 0 Warnungen**.
- Neue UI-Policy-Tests: **3 bestanden**. Danach vollständige Windows-Testreihe:
  **297 bestanden, 4 übersprungen**, keine fehlgeschlagenen Tests. Die vier
  ausgelassenen Fälle benötigen Hardware-/Foreground-Zugriff am interaktiven Desktop.
- Baseline nach dem Build geprüft: 105 Einträge, zusammengesetzt aus 157
  statischen API-Aufrufstellen und 24 direkten Form-Unterklassen.
- In diesem Audit keine produktive UI-Logik und keine sichtbaren Texte geändert.
  Die Laufzeit-/Bildprüfung der offenen Fundstellen bleibt Teil ihrer Migration;
  dieser Testlauf ist kein Nachweis, dass alle Windows-Elemente ersetzt wären.

## Aether Wave wiederhergestellt, Themes erhalten — 02.10.2026

**Lokal implementiert für Windows und Linux; noch kein Commit/Push.** Die
Wiederherstellung betrifft die Gestaltung, nicht den funktionalen Stand eines
alten Commits. Die sechs Presets einschließlich Pocket Light und die drei freien
Farbfelder bleiben erhalten. Vorhandene Benutzerfarben werden nicht zurückgesetzt.
Windows: **Einstellungen → Aussehen → Aether Original**; Linux:
**Settings → App & files → Appearance → Aether Original**.

- `UiThemePalette` liefert für Aether Original wieder die unten dokumentierten
  historischen Flächen-, Rahmen- und Textwerte. Andere Presets/Eigenfarben erhalten
  passende dunkle oder helle Flächen. ROM-Farbpaletten sind weiterhin getrennt.
- `UiChamfer` definiert gemeinsam die scharfen Ausschnitte oben rechts und unten
  links. Windows zeichnet damit seine Buttons; Linux verwendet dieselben Punkte
  über `AetherShapeRenderer` und SDL-Geometrie. Die Trefferflächen und bestehenden
  Tastatur-/Controller-Aktionen ändern sich nicht.
- Primäre Aktionen haben wieder einen Verlauf, ausgewählte Menüpunkte einen
  schmalen Akzent und Kontur. Panels sind kantig; Header und Karten tragen die
  Akzentlinien. Gedämpfte/deaktivierte Beschriftung und sichtbarer Fokus bleiben.
- `UiButtonGradient` prüft den Verlauf einschließlich Zwischenfarben, Hover und
  gedrücktem Zustand. Falls nötig werden **nur die gezeichneten Button-Farben**
  für lesbare Schrift angepasst; die gespeicherten HEX-Werte und Farbmuster
  bleiben exakt. Akzenttexte werden auf allen vier Flächen geprüft.
- Die Windows-Listenauswahl benutzt ebenfalls Theme-Farben statt eines fest
  violetten Hintergrunds. Die Kopflinie bleibt unter den angedockten Controls
  sichtbar. Aktuelle Schriftgrößen, Suche, Skalierung und Dialogfunktionen bleiben.

### Validierung dieses Umbaus

- Release-Build der Lösung: **0 Fehler, 0 Warnungen**.
- Core: **256 bestanden**; Windows: **294 bestanden, 4 übersprungen**;
  Linux-Desktop: **141 bestanden, 53 übersprungen**. Die Skip-Zahlen umfassen
  Hardware-/Foreground- bzw. native Wayland-/AT-SPI-/Opt-in-Prüfungen.
- Die 11 Windows-Theme-Fälle prüfen alle sechs Presets, zwei freie Paletten,
  Live-Umschaltung, Speicherung, Zurücksetzen, unveränderte ROM-Palette,
  Button-Geometrie/Verlauf und deaktivierte/kleinste Controls.
- Frische, visuell geprüfte Windows-Testbilder (keine echten Spielstände):
  `artifacts/aether-wave-restored-20261002/windows-theme-logo.png`,
  `windows-appearance-logo.png` und `windows-appearance-pocket.png` sowie
  weitere Theme-Varianten im selben lokalen, ignorierten Ordner.
- Copy-Prüfung der betroffenen UI-Dateien: zwei Hinweise auf den **unveränderten**
  System-Aufzählungstext `GAME BOY · COLOR · ADVANCE`; keine sichtbaren Texte
  dieses Ablaufs umformuliert.

**Linux noch auf einem echten Wayland-System abnehmen.** Die neuen SDL-Vertices
sind hier ohne native Sitzung getestet, nicht die tatsächliche Darstellung.
`NativeShellRoutesControlsRendersAssetsAndPreservesSettings` prüft jetzt zusätzlich
alle sechs Theme-Buttons und nutzt für die freien Farben die aktuellen Feldpositionen.
Auf Linux nach dem Release-Build ausführen:

```bash
AETHERBOY_UI_TESTS=1 AETHERBOY_UI_CAPTURE_DIR="$PWD/artifacts/aether-wave-linux" \
  dotnet test --project tests/AetherBoy.DesktopTests/AetherBoy.DesktopTests.csproj \
  -c Release --no-build --filter FullyQualifiedName~NativeShellRoutesControlsRendersAssetsAndPreservesSettings
```

Zusätzlich Hauptfenster, Einstellungen, Sofa-Modus und ein Werkzeugdialog mit
Aether Original und Pocket Light prüfen: keine überdeckten Texte, diagonale
Ecken, sichtbarer Tastatur-/Controller-Fokus, eigene Farben nach Neustart erhalten.
Dieser Designumbau ist kein neuer Nachweis für Emulations- oder Online-Link-Kompatibilität.

## Ursprüngliches Aether-Wave-Design wiedergefunden — 02.10.2026

Historischer Befund **vor** der oben beschriebenen Wiederherstellung:

Der Nutzer vermisst ausdrücklich die diagonalen Abschrägungen **oben rechts und
unten links**, ursprüngliche Farbflächen und Fenster-/Button-Gestaltung. Der
Befund ist im Git-Verlauf eindeutig nachvollziehbar:

- `099c2f9` vom 10.08.2026 (`feat: launch aether wave interface`) enthält in
  `nanoboy/Controls/AetherUiControls.cs` die sechs Eckpunkte von
  `CreateChamferedPath`. Derselbe Stil ist unmittelbar vor `2a91c9f` noch vorhanden.
- `2a91c9f` vom 28.09.2026 (`Improve link session handling and UI`, xJessyX)
  ersetzt diesen Pfad durch `CreateRoundedPath`, die primären
  Violett–Cyan-Verläufe durch einfarbige Füllungen, kantige Panels durch Rundungen
  sowie Farb-/Akzentlinien im Kopfbereich durch eine neutrale Trennlinie.
  Gleichzeitig ersetzt eine gemeinsame berechnete Palette die festen Farbwerte.
- Die späteren lokalen Änderungen an `UiThemePalette.SurfaceAt` tönen die Flächen
  wieder mit den Akzenten. Die sechs Theme-Presets sind ebenfalls lokal ergänzt.
  Das Preset **Aether Original** verwendet die ursprünglichen drei Grundfarben,
  stellt aber weder die historischen Flächenwerte noch Geometrie und Verläufe
  vollständig wieder her. Die Bezeichnung allein belegt keine Originaltreue.

Historische Werte: Hintergrund `#050712`, Chrome `#080B18`, Flächen `#0C101F`,
angehobene Flächen `#111629`, Umrisse `#2F3753`, Text `#F1F4FF`, gedämpfter Text
`#8B94B1`; Akzente `#8B38FF`, `#A942F5`, `#29E2ED`.

Vorhandene, visuell geprüfte **alte UI-Test-Screenshots**, keine neue Nachbildung:

- `artifacts/parity-review/windows-shell.png`
- `artifacts/parity-review/control-center-display.png`

Diese Artefakte sind lokal/ignoriert und stehen nicht zwangsläufig nach einem
frischen Clone zur Verfügung. Der Git-Code ist die reproduzierbare Referenz.

Aus diesem Befund entstand der oben umgesetzte, gezielte Designumbau. Kein
vollständiges Zurücksetzen von `2a91c9f`; dieser Commit enthält zusätzlich
funktionale Änderungen. Eigene Screenshots des Nutzers waren für diesen
eindeutig gefundenen Stil nicht zwingend nötig.

**Integrationsnachtrag:** Der unten beschriebene damalige lokale Stand ist inzwischen
als `2a91c9f` auf `development` veröffentlicht. [Windows-Abnahme, CI-Testkorrektur
und Slot-Layoutfix](WINDOWS_UI_INTEGRATION_2026-09-28.md) dokumentieren die anschließende
Integration. Die folgenden Aussagen „uncommittet“ beziehen sich auf den ursprünglichen
Schreibzeitpunkt, nicht mehr auf den aktuellen Git-Status.

Basis: `development` auf `4b56039` (Voltex' Diagnose, ROM-freier Windows-Verbindungstest und Linux-Handoff). Die unten beschriebenen UI-Änderungen liegen derzeit **uncommittet** im Linux-Arbeitsbaum; sie sind noch nicht Teil dieses Upstream-Commits. Bestehende lokale Änderungen am neuen Windows-/Linux-Layout und an den README-Screenshots bleiben erhalten.

## Verhalten

- Die Standard-UI nutzt die Markenfarben `#8B38FF` (primärer Akzent), `#29E2ED` (zweiter Akzent) und `#050712` (Hintergrund) aus `branding/BRAND.md`. Das Logo selbst wird nicht umgefärbt.
- Beide Akzente und der Hintergrund sind global frei wählbar. Die Spielbild-/DMG-Palette bleibt eine getrennte Einstellung. Text, Flächen und die Schrift auf primären Schaltflächen werden für hellere und dunklere Hintergrundfarben abgeleitet.
- Windows: **Settings → Appearance**, drei native Farbdialoge mit Vorschau und „Restore logo colors“. Änderungen erscheinen sofort und werden über den bestehenden Windows-Einstellungsspeicher gesichert.
- Linux/Wayland: **Settings → System → Appearance colors**, drei `#RRGGBB`-Felder mit Vorschau, „Apply colors“ und „Restore logo colors“. Die Werte werden im globalen `settings.json` gespeichert, nicht im Spielprofil.
- Die gemeinsame, plattformneutrale Farblogik liegt in `nanoboy/Core/UiThemePalette.cs`. Die Linux-Testoberfläche für den ROM-freien Verbindungstest aus Voltex' Handoff ist weiterhin eine separate offene Aufgabe.

## Prüfung

- Linux- und Windows-Cross-Build: jeweils 0 Warnungen, 0 Fehler.
- Core-Tests: 234 bestanden, darunter Logo-Defaults, freie Hex-Farben und Textkontrast bei hellen, mittleren und dunklen Hintergründen.
- Linux-Desktop-Tests: 123 erfasst, 83 bestanden, 40 Tests ohne native Sitzung/Opt-in übersprungen. Der gezielte native Wayland-Shelltest wurde zusätzlich mit Opt-in ausgeführt: 1 bestanden; er hat die drei Farben über die Textfelder geändert und nach dem Speichern wieder geladen.
- Screenshots der Standard- und frei gewählten hellen Variante: `artifacts/theme-ui-preview/appearance.png` und `appearance-custom.png` (lokale, ignorierte Testartefakte).

Eine visuelle Windows-Abnahme auf einem Windows-Rechner steht noch aus. Die Windows-Oberfläche wurde unter Linux mit aktiviertem Windows-Targeting kompiliert, aber dort nicht gestartet. Kein Commit, Push oder Server-Deployment durch diese UI-Arbeit.
