# Linux-Ausbau

## e-Reader-Kartenbibliothek — 08.10.2026

Unter **Einstellungen → Werkzeuge → e-Reader → Kartenbibliothek** gibt es dieselben
Grundfunktionen wie Windows: eigene entpackte Firmware auswählen, RAW/kleine ZIP-
Kartensätze importieren und ergänzen, Titel bearbeiten, Stand mit Vorschau sichern
und fortsetzen. SDL-/Aether-Darstellung, DE/EN und Themes bleiben erhalten.
Die Oberfläche verwendet vorhandenes Datei-Picker-Routing; keine Windows-Controls.

Gemeinsame Manifest-/Hashlogik in `AetherBoy.Runtime.EReaderLibrary`; Daten unter
`<dataPaths.Data>/ereader`, normalerweise `$XDG_DATA_HOME/aetherboy/ereader`
(die tatsächliche Basis folgt `LinuxDataPaths`, einschließlich Portable-Modus).
Speicherpfade folgen der gefrorenen Set-/Firmware-/Streifenidentität, nicht nur dem
ROM-Hash. Resume lädt den vollständigen Scannerzustand ohne doppelten Kartenimport.
Die neue Seite verwendet die vorhandene Zustands-/Vorschauverwaltung.

13 gemeinsame Bibliothekstests (abschließend 32/32 inklusive Sprachkatalog) und
2 native DE/EN-Wayland-Seitentests bestanden;
Linux-Screenshots unter `artifacts/ereader/library-ui-linux/`, dunkel/hell und große
Schrift. WSLg-Fallbackrenderer, keine zusätzliche Hyprland-/Audio-Geräteabnahme.
Der reale Bibliotheksablauf mit Donkey Kong-e und Balloon Fight-e bestand 2/2:
neun Streifen, Spielbild, variable endliche PCM-Daten, zusätzliche 3.600 Frames,
fünf komplette Save-State-Replays und Neustart aus gespeichertem Flash ohne Scan.
Originalarchive bleiben unangetastet. Belege: `artifacts/ereader/library-games-linux/`.

Kein Import der gesamten Sammlung, keine automatische Kartenerkennung/-gruppierung,
kein ROM-freier Start und keine bestätigte Verbindung zum zweiten GBA-Spiel.
Änderungen am Kartensatz erzeugen einen neuen Speicherbereich; alte Daten bleiben,
eine Versionsauswahl fehlt noch. Vollständiger gemeinsamer Umfang, Firmware-
Bestandsaufnahme, Forschungsquellen und offene Punkte stehen im
[Windows-Arbeitsplan](WINDOWS_ROADMAP.md#e-reader-kartenbibliothek-und-firmware-forschung--08102026).

## Reparatur nach der Nachprüfung — 03.10.2026

Deutsche Einstellungsüberschrift/Suchhilfe sind getrennt, Navigationshinweise
mehrzeilig und Patch-Aktionen breiter. Tastennamen werden lokalisiert angezeigt;
Pfeil- und Bildtasten ausgeschrieben, da Pfeilzeichen in der Schrift fehlen.
Gespeicherte Scancodes, Themes und Customize bleiben unverändert. 96 native
Weston-Bilder neu erzeugt, betroffene Ansichten auch mit großer Schrift geprüft.
Die gemeinsame Backup-Rotation und neue Speicherphasen-Diagnose gelten ebenso
für Linux; elf Speichertests nativ bestanden. Desktop: 202 bestanden, drei
Audio-Hardware-Skips. Echte Hyprland-/GPU-/Audio-Abnahme und Langzeitspiele bleiben
offen; Windows-Lastbefunde sind kein Nachweis desselben OS-Problems unter Linux.
[Reparaturen, Nachweise und Grenzen](STABILIZATION_2026-10-02.md#reparaturrunde-vom-03102026-speichern-fehleranzeige-sprache).

## Anzeigesprache — 02.10.2026

Gleicher DE/EN-Katalog und dieselbe automatische Spracherkennung wie Windows.
Auswahl unter **App and files → Display language** (deutsch: App und System →
Anzeigesprache), außerdem über die Einstellungssuche. Deutsch, English oder
Systemsprache; gilt nach Neustart. Alte Einstellungen ohne Sprachfeld bleiben
kompatibel. Spielprofile dürfen die Sprache nicht überschreiben. Die eigene
Wayland-Oberfläche und Themes/Customize bleiben bestehen.

Die native Sprachseite wurde in beiden Sprachen unter privatem Weston/Wayland
geprüft. Das ist keine vollständige Übersetzungs-/Layoutabnahme jedes Dialogs.
Die Vervollständigungsrunde ergänzt Controller-/Audio-Details, Dateifilter,
Profile, Fehler-/Abbruchpfade, Wiederherstellung, lesbare Diagnosezustände und
die lokale Browserbrücke. Gemeinsamer Katalog: **2376 DE/EN-Einträge**.
Genres/Sortierung/Tasten werden nur zur Anzeige übersetzt; gespeicherte IDs,
SDL-Tastennamen und Protokollwerte bleiben stabil. Native Fremdfehler behalten
unter „Technische Details“ ihren Originaltext. Die vollständige native Desktop-
Prüfung besteht mit **202 Fällen und 3 Audio-Skips**; echte Hardware und sämtliche
Fenster bei hoher DPI bleiben separat abzunehmen.
Gemeinsame offene Liste und Grenzen:
[Sprachmigration](WINDOWS_ROADMAP.md#anzeigesprache--lokaler-zwischenstand-vom-02102026).

## Nächste Priorität — 02.10.2026

Auf Nutzerwunsch bleibt Phase 5.3 beim vorbereiteten Prüfen/Herunterladen.
Selbstinstallation, Signaturen, Release-Pipeline und Rückfall sind für später
auf der gemeinsamen Merkliste. Das ursprüngliche Aether-Wave-Design ist lokal
für beide Frontends wiederhergestellt; **alle sechs Themes und frei wählbare
Farben bleiben bestehen**. Kantige Panels, diagonal geschnittene Buttons und
gezielte Akzentverläufe verwenden gemeinsame Farben/Geometrie. Die bestehende
Wayland-Bedienung bleibt erhalten. Phase 6.1 ist inzwischen als **eigene
Startanimation** umgesetzt; auch **6.2 Barcode Boy** ist jetzt in beiden Frontends
und im gemeinsamen Core implementiert. Echte Battle-Space-Tests und die spätere
Stabilisierung sind im Abschnitt Phase 6.2 unten dokumentiert.
Details und Grenzen stehen in der
[gemeinsamen Reihenfolge](WINDOWS_ROADMAP.md#aktuelle-priorität-und-zurückgestellte-arbeiten--02102026).
Der Designumbau ist gebaut, aber **noch nicht unter echtem Wayland visuell
abgenommen**. Testbefehl und Theme-/Customize-Prüfschritte stehen im
[UI-Handoff](UI_THEME_HANDOFF_2026-09-28.md#aether-wave-wiederhergestellt-themes-erhalten--02102026).
Kein Commit/Push durch diese Änderung.

## Phase 6.2 — Barcode Boy, 02.10.2026

**Settings → Tools → Open Barcode Boy** oder Suche „Barcode“: Scanner verbinden,
13-stelligen Kartencode eingeben oder aus einer UTF-8-Textdatei laden, dann scannen.
Gleiche Runtime/Serial-Implementierung wie Windows; kein zweiter Zubehör-Core.
Die neue Seite wurde unter WSLg/Wayland bedient und als Screenshot geprüft.
Das synthetische GB-Testprogramm empfängt unter Linux beide vollständigen Pakete.

**Nachtrag 06.10.2026:** Die bereitgestellte **Battle Space (Japan, GB)**-ROM
akzeptiert Berserker und Valkyrie im isolierten GB-Core-Test auf Linux/WSL und
Windows. Figuren/Werte erscheinen im Spiel, Bestätigung und ein wiederholter
Scan funktionieren; neun verglichene Spielbilder sind identisch. Original-ZIP
unverändert, keine Cheats oder RAM-/ROM-Patches. Details und ROM-Prüfsumme im
Windows-Plan, lokale Belege unter `artifacts/barcode-live-test/`.
**Historischer Zwischenstand vor Stabilisierung:** Auch beide Scanner-Buttons mit echter ROM
unter WSLg/Wayland geprüft: beide Kartenbilder stimmen. Teilpaket-Savestate,
Rewind, Textimport, ungültige/besetzte Eingabe und Trennen/Reset bestehen.
11 Barcode-Core-Fälle und 5 Linux-UI-Fälle bestanden; Runtime einschließlich
Wiederanlauf: 13 bestanden, **2 fehlgeschlagen**, dieselben wie Windows.
Nach 17 korrekten Karten verlangt das Spiel vor der nächsten Eingabe einen
Scanner-Neustart; nach fünf emulierten Minuten Wartezeit steht bereits ein
Fehlerdialog. Die anschließend gelesenen 30 Kartenbytes sind korrekt. Fehler
bestätigen, Scanner trennen/verbinden und erneut bestätigen erlaubt wieder einen
Scan, ohne Spielreset. Spiel-Timeout und Emulator-/Handshakefehler noch nicht
abschließend getrennt; **keine vollständige Dauerabnahme**.
Gemeinsame optionale ROM-Tests: `AETHERBOY_BATTLE_SPACE_ZIP`; UI zusätzlich
`AETHERBOY_UI_TESTS=1` in einer Wayland-Sitzung. Belege unter
`artifacts/barcode-extended/`, vollständige Grenzen/Startanleitung im Windows-Plan.
**Anschließende Stabilisierung am selben Tag:** Der gemeinsame Core korrigiert
die interne Taktphase und den DIV-Reset. Zwischen den beiden Barcode-Paketen liegt
eine gespeicherte, Double-Speed-feste Kompatibilitätspause. Der originale
Eingabe-Timeout bleibt bestehen; Wiederanlauf ist ein eigener Test, kein RAM-Hack.
50 aufeinanderfolgende Karten und die 15-Minuten-Idle-Prüfung bestehen. Der
strengere 1000-Karten-Lauf mit wechselnden Wartezeiten scheitert nach 596
korrekten Karten bei der nächsten Geräteerkennung, identisch zu Windows.
Der zusätzliche 1000er-Wiederanlauf besteht: fünf explizite Scanner-Neuverbindungen
vor Scan 597, 652, 790, 936 und 994, kein ROM-Reset, alle Karten korrekt. Etwa
88 emulierte Minuten, alle 1000 Abschlussbilder identisch zu Windows.
Vollständiger Core 285/285, Barcode-Runtime 16 bestanden / 1 fehlgeschlagen, UI 5/5.
Der strengere Test ohne Wiederverbinden bleibt rot; der zusätzliche Dauerlauf
ist kein Ersatz für einen unterbrechungsfreien Nachweis. Ergebnisse,
Quellen, gesicherter Repro und Grenzen stehen im
[Stabilisierungsabschnitt](WINDOWS_ROADMAP.md#stabilisierung-und-zubehör-nachfolge-06102026).
**Damals weiter offen:** der seltene Handshake-Zählerkonflikt, längeres tatsächliches
Spielen und native Desktop-Abnahme beim Kollegen, die übrigen vier Spiele und
gemessenes physisches Scanner-Timing.
Die gemeinsame offene Checkliste steht im unten verlinkten Windows-Plan;
dieser Prüfpunkt blockiert vorerst keine weitere Entwicklung.
Barcode Boy ist kein GBA e-Reader oder Bardigun; kein gleichzeitiger Link-Kabel-Betrieb.
512 Basis-Dots je Scanner-Bit sind eine vorläufige Emulationsrate, noch kein
Hardware-Timingnachweis. Native Abnahme am Linux-Rechner bleibt auf der Merkliste.

**Nachtrag 08.10.2026:** Der bekannte Repro ist behoben. Der Scanner erlaubt eine
erneute vollständige Erkennung, bevor Kartenbytes übertragen werden, ohne eine
vorgemerkte Karte zu verlieren. Bewusste Kompatibilitätserweiterung, kein Beweis
identischen Hardwareverhaltens. Strenge 50-/1000-Kartenfolgen bestehen nun auch
unter Linux ohne Neuverbinden oder ROM-Reset; jedes Kartenbild geprüft. Core
**287/287**. Hardware-Timing, weitere Spiele und Abnahme beim Kollegen bleiben offen.
Belege: `artifacts/barcode-retry/`; Details im gemeinsamen Windows-Plan.

### Nächstes Zubehörprojekt: e-Reader

Seit 08.10.2026 erster Einzelgeräte-Stand: derselbe GBA-Zubehör-Core wie Windows,
kein zweiter Parser. Modul-/Scannerregister, Scan-Zyklen/IRQ, geschützte Flash-
Kalibrierung und vollständiger Zubehör-Savestate. RAW 1872/2912 Byte sowie gepackte
Punktbilder 3520/5456 Byte, maximal 16 wartende Karten; keine Bilder/dekodierten BINs.
MPL-2.0-Adaptionen aus mGBA in zwei getrennt gekennzeichneten Dateien, Lizenz wird
mit ausgeliefert. Normale GBA-Savestates bleiben im bisherigen Schema 6.

**Settings → Tools → Open e-Reader**, DE/EN, Theme-/Customize-Farben, Kartenimport
über den vorhandenen asynchronen Dateipicker und „Scans verwerfen“. Die Auswahl
bleibt an die ursprüngliche Sitzung gebunden: kein versehentliches ROM-Laden.
Abbruch/falsche Dateigröße behalten die vorhandene Warteschlange und Pause.
ROM-freie GBA-/Zubehör-/Sprach-Regression **288/288**, einschließlich Kalibrierung
über Beenden/Öffnen und Erhalt importierter Flashdaten. Zubehör-/Theme-UI unter
WSLg/Wayland **11/11**. DE/EN in Original/hellen Eigenfarben und Schriftgröße 14/18
gerendert und geprüft. Lokale Artefakte: `artifacts/ereader/`.

Zusätzlich die bereitgestellten USA-Karte-1-ZIPs von Donkey Kong-e und Balloon
Fight-e geprüft: je zwei RAW-Streifen à 2912 Byte, **2/2 Archivfälle je OS**;
Decoder-Punktebilder und gespeicherte Warteschlangen sind plattformgleich.
Original-ZIPs unverändert, keine Kartendaten gebündelt. Mit der nachgereichten
USA-e-Reader-ROM folgt jetzt der echte Firmware-Scan: beide Streifen von Karte 1
werden je Spiel erkannt (acht, dann sieben fehlende Dotcodes von insgesamt neun).
Gemeinsamer Kernfix für spätes Einlegen in einen bereits wartenden zweiten Scan;
ROM-freier Fehlernachweis und echte Scan-/Zustands-Wiederholungsprüfungen ergänzt.
`ProvidedEReaderFirmwareTests`: zusätzlich `AETHERBOY_EREADER_ROM_ZIP`, je
Kartensatz 20 identisch wiederhergestellte Läufe mitten im Scan sowie doppelte
Karte ohne Fortschrittszuwachs. Belege: `artifacts/ereader/firmware-linux/`.
Bei dieser ersten Teilprüfung fehlten je Spiel noch sieben Dotcode-Streifen;
die inzwischen vollständige Sammlung und Spielabnahme folgen unten.
Abschließende Runtime-Auswahl mit diesen echten Firmware-/Kartenprüfungen:
**293/293 auf Linux/WSL und Windows**, keine übersprungenen Fälle. Die akzeptierten
Teilimporte und ihre Bildprüfsummen sind plattformgleich; keine vollständige
Spiel-/Flashspeicher-Abnahme. Ursprüngliche ZIPs bleiben unverändert.
Optionale Fixture-Variablen und Grenzen im gemeinsamen Windows-Plan.

**Erweiterte Sammlung, 08.10.2026:** Derselbe gemeinsame ARM-MSR-/Sound-FIFO-DMA-Fix
wie unter Windows: Flags-Schreibzugriffe erhalten die IRQ-Sperre, Sound-DMA1/2
erzwingt vier 32-Bit-Wörter und korrekte Nachfüllanforderungen. Vollständige
Donkey-Kong-/Balloon-Fight-Sets booten, liefern Spielbilder/wechselndes PCM und
bestehen Zustand-Wiederholungen, Flash-Speichern sowie Öffnen in einer neuen
Maschine ohne erneute Scans. Kirby Slide Puzzle, Manhole und Air Hockey sind
zusätzliche Standalone-Regressionen. Weitere elf NES-e-Sets, vier Pokémon-
Promoansichten/Animationen und Kirby Contest Card geprüft. Die 19 zusätzlichen
Windows-/Linux-Abläufe erzeugen 978 identische Bild-/Zustandsdateien; kein
Durchspielnachweis und keine Prüfung aller Karten der Sammlung.
GBA-/e-Reader-Testauswahl **315/315 je OS**, keine übersprungenen Fälle;
Linux-Desktop- und Windows-Release-Build ohne Warnungen/Fehler. Linux hier unter WSL.
Die USA-e-Reader-ROM reicht dafür aus, keine weitere Spiel-ROM nötig.
`AETHERBOY_EREADER_COLLECTION_ZIP` aktiviert die optionalen Sammlungstests.
Belege: `artifacts/ereader/matrix-linux/` und `collection-regression-linux/`.
Dateien/Formatgrenzen und genaue Abnahmetiefe stehen im gemeinsamen Windows-Plan.

**Zusätzliche breite Prüfung, 08.10.2026:** `ProvidedEReaderExtendedTests` ergänzt
50 optionale Fälle. Die 49er-Matrix und vier vertiefte Läufe (drei davon längere
Wiederholungen) bestehen auf **Linux/WSL 53/53 und Windows 53/53**, keine
übersprungenen Fälle. Elf Mario-Party-e-Kartenprogramme werden über je zwei echte
RAW-Streifen gestartet; 38 Pokémon-e-TCG-Programme über die mitgelieferten
Flash-Abbilder (23 Minispiele laut Sammlung, acht Animationen, sieben Hilfsprogramme).
Flash-Start ist kein RAW-Kartenscan-Nachweis. Zusätzlich werden Machop, Machoke und
Machamp vollständig eingescannt und Machop At Work gestartet, ohne Flash-Injektion.

Je Fall fünf vollständige Zustandswiederholungen; PCM und Eingaben erfasst,
Originalarchive unverändert. Hauptmatrix mit 1.800 Zusatzframes je Programm;
Big Boo, Pika Pop und Coin Flipper zusätzlich je 18.000 Frames. Diese Läufe
enthalten auch Menüs/Game-over und ersetzen keine manuelle Spielabnahme.
**801 identische Bildpaare und 697 identische Messpunkt-Paare** (Zustand, PCM,
Diagnosen) zwischen Windows und Linux. 327.644 ausgeführte Frames je Plattform
inklusive Restore und Boot, keine reine Spielzeit. Kein neuer Kernfehler in dieser
Auswahl; nur Tests und Dokumentation ergänzt. Builds ohne Warnungen/Fehler.
Belege in `artifacts/ereader/extended-linux/`, `deep-linux/` und
`extended-comparison.json`; vollständige Titel und Testgrenzen im Windows-Plan.
`AETHERBOY_EREADER_EXTENDED_OUTPUT` aktiviert Berichte/Bilder,
`AETHERBOY_EREADER_SOAK_FRAMES` wählt 1.800–18.000 Zusatzframes.
Dies ist weiterhin Linux unter WSL, keine native Desktop-/Audiogeräte-Abnahme.

**Offen:** Hörprobe/Controller und längere Spielsitzungen, andere Firmware-Regionen,
Kartenliste/Einzelentfernung, vollständiger Sammlungsimport in der Oberfläche, anschließend zweiter lokaler GBA
und echter Kartentransfer. Eine Pokémon-ROM allein reicht nicht. Keine ROM/Karte
gebündelt; Online-Transfer und Bildkonvertierung noch nicht angebunden. Native
Desktop-/Controller-Abnahme beim Kollegen offen. GBC-IR bleibt ein separates Projekt.
[Verbindliche gemeinsame Reihenfolge](WINDOWS_ROADMAP.md#als-nächstes-vorgesehen-e-reader-gemeinsam-für-windows-und-linux).

[Gemeinsame Implementierung, Referenzen und vollständiger Spieltestplan](WINDOWS_ROADMAP.md#phase-62--barcode-boy-02102026).

## Phase 6.1 — Logo und Klang vor dem Spiel, 02.10.2026

Dieselbe globale Intro-Einstellung, dieselbe Dauer und dieselben Dateiregeln wie
Windows: 2,4 Sekunden vor einem Einzelspiel (GB/GBC/GBA), standardmäßig an,
separat abschaltbarer Ton, Vorschau, eigenes PNG und kurzes PCM-WAV. Kein Intro
beim reinen App-Start, Reset oder Schnellladen und keine Wartephase in Link-Sitzungen.
Eigene Dateien werden unter `data/BootIntro` im bestehenden XDG-/Portable-Profil
kopiert; eine eigene Datei muss nicht an ihrem ursprünglichen Ort bleiben.

**Settings → App & files → Firmware → Logo and start animation**, oder Suche
`Intro`/`Startanimation`. Die Bezeichnung Firmware bleibt technisch getrennt:
das AetherBoy-Intro emuliert keine Boot-ROM und verändert `UseFirmware` nicht.
SDL zeichnet die Animation, Tastatur/Controller überspringen sie; kein sichtbarer
Überspringen-Button und keine unsichtbare Maus-Trefferfläche. Ein
vorhandenes Einzelspiel pausiert. Vollständige Regeln, Testnachweise und offene
Abnahme: [gemeinsame Phase 6.1](WINDOWS_ROADMAP.md#phase-61--eigene-startanimation-02102026).
Die drei neuen Introtests wurden unter WSLg/Wayland bestanden; der ältere
Ladefehlertest erwartet noch einen inzwischen anders formulierten Fehlertext.
Audio-Hardwareabnahme und der Test auf dem Linux-Rechner des Kollegen bleiben offen.

## Phase 5.3 — Update-Prüfung und Paketdownload, 02.10.2026

**Settings → App & files → Files & updates → Updates** oder Suche `Updates`.
Dieselbe `ReleaseUpdateService`-Implementierung wie Windows; keine getrennte
Versions-/Downloadlogik. Englischsprachige Oberfläche, manuelle Prüfung und
Paketdownload, optional einmalige Prüfung beim Programmstart (zunächst aus).
Linux x64 und ARM64 werden getrennt ausgewählt. Downloads bleiben im XDG-Cache
beziehungsweise unter `AetherBoyData/cache/updates` im Portable Mode.

Release-Version, Paketverfügbarkeit, Fehler, Downloadfortschritt und SHA-256-
Abschluss werden unterschieden. Noch **keine automatische Installation** und kein
Eingriff in Benutzerinstaller, Systempakete oder laufende Spiele. Anweisungen,
Sicherheitsgrenzen, Release-Konventionen und verbleibende Selbstinstaller-Arbeit:
[gemeinsame Phase 5.3](WINDOWS_ROADMAP.md#phase-53--updates-prüfen-und-herunterladen-02102026).

`package-linux.sh` unterstützt nun `--channel development|stable`; der Kanal steht
auch in `package-info.json`. Die Quellpaket-Auswahl enthält die neuen Lizenzordner
für Discord/Newtonsoft, SharpCompress und den Cheat-Code. Der Pakettester prüft
diese Dateien. Tatsächlicher self-contained Build/Start unter Linux und die neue
Wayland-Seite müssen auf Linux validiert werden; ein Windows-Build belegt das nicht.

Tests: globale Einstellung mit Neustart/Backup-Wiederherstellung, Profile ohne
Update-Freigabe, Suche und englische Statusmeldungen. Der native UI-Test läuft nur
mit `AETHERBOY_UI_TESTS=1` in einer echten Wayland-Sitzung. Für alle Plattformen
kommen synthetische Download-, Versions-, Redirect- und Abbruchtests im Runtime-
Projekt hinzu. Die reale Release-API lieferte zum Prüfzeitpunkt noch keine Pakete.

## Phase 5.2 — Discord activity, 02.10.2026

Gleicher gemeinsamer Dienst wie Windows, auf Nutzerwunsch **standardmäßig an**,
Spieltitel zunächst ausgeblendet. Die öffentliche Application ID
`1555427237908586616` ist bereits hinterlegt. Ein einmal ausgeschalteter Status
bleibt aus; ROM-Profile verändern diese globale Freigabe nicht.

**Settings → App & files → Desktop & accessibility → Discord activity** oder
Suche nach `Discord`. **Privacy** enthält beide Schalter und die Vorschau;
**Setup** zeigt die bereits konfigurierte öffentliche ID für Entwickler.
Keine Bot-Tokens eingeben. Die vollständige ID lässt sich über den vorhandenen
SDL-Texteditor samt Zwischenablage und Bildschirmtastatur bearbeiten.

System/Pausenstatus für GB, GBC und GBA, optional der echte ROM-Header-Titel.
Ohne Einzelspiel und im Link-Modus wird die Aktivität entfernt. Keine Pfade,
Spielstände oder Raumcodes. Derselbe IPC-Adapter, dieselben Lebenszyklus- und
Datenschutzregeln wie unter Windows; kein X11-Pfad und kein Raumdienst erforderlich.
Laufende Discord-Desktop-App mit demselben Benutzerkonto nötig. Bei Flatpak/Snap
kann der lokale IPC-Socket unerreichbar sein. AetherBoy ändert keine Sandbox-Regeln.

Automatisiert: Einstellungen/Migration, Profilisolation, Backup-Recovery,
ungültige IDs und gemeinsamer RPC-Dienst. Der native Test
`LinuxDiscordTests.NativeSettingsCanEditIdWithoutEnablingActivity` benötigt
`AETHERBOY_UI_TESTS=1` unter Wayland. UI-Tests benutzen einen Fake-Discord-Client;
sie veröffentlichen auch mit hinterlegter Standard-ID keine Aktivitäten.
Echte Discord-Anzeige, Linux-Paketvarianten und Darstellung bei großer Schrift
bleiben manuelle Abnahme, nicht als bestanden behauptet.

Details und gemeinsame Abnahmeliste:
[Phase 5.2 auf Windows und Linux](WINDOWS_ROADMAP.md#phase-52--discord-spielstatus-02102026).

## Phase 5.1 — Sofa mode, 01.10.2026

Implementiert passend zur Windows-Sofa-Ansicht: sichtbarer Einstieg **Sofa mode**,
Vollbild-Bibliothek mit Recent/Favorites/My selection/All games, Vorschauen,
getrennt gespeicherter eigener Auswahl, Controller-/Tastaturbedienung und kleinem
Spielmenü. **F10**, **Esc** oder **L3+R3** öffnen im Sofa-Spiel das Menü;
**Ctrl+Shift+F11** oder **Leave sofa mode** beenden den Modus. Außerhalb des Sofa-Modus
bleibt F10 für Online Link reserviert. Kein automatischer Start, keine Kiosksperre.

Die Metadaten-/Filtertests laufen auch auf Windows. Der native Test
`LinuxSofaTests.NativeSofaNavigationRestoresWindowAndPreservesAlreadyPausedSession`
benötigt `AETHERBOY_UI_TESTS=1` unter echtem Wayland. Dort bitte zusätzlich Controller
abziehen/wieder verbinden, Laden abbrechen, Slot laden, lange Titel, alle Themes,
Monitorwechsel und Rückkehr in ein vorher pausiertes Spiel prüfen.
Der normale Linux-Fenstermanager besitzt die Fensterposition; die App stellt den
vorherigen Fullscreen-Zustand über SDL wieder her, ohne X11-spezifische Positionierung.
Umfang, Sicherheitsgrenzen und Abnahme: [gemeinsame Sofa-Liste](WINDOWS_ROADMAP.md#phase-51--sofa-modus-01102026).

## Phase 3 — Medien und ROM-Archive, 01.10.2026

Der erste Ausbau ist auf beiden Oberflächen angebunden. Umfang, Grenzen und
offene Folgearbeit stehen in der [gemeinsamen Phase-3-Liste](WINDOWS_ROADMAP.md#phase-3--erster-gemeinsamer-ausbau-01102026).

Die offenen Phase-3-Punkte sind auf Nutzerwunsch vom 01.10.2026 auf die
[Merkliste für später](WINDOWS_ROADMAP.md#noch-offen--bewusst-nicht-als-fertig-gewertet)
gesetzt: optionale gespeicherte Patch-Zuordnungen, native Wayland-Abnahme,
Video-/Audio-Langzeittests, platzsparendes Videoformat, längere Clips und
Aufnahme der lokalen Zwei-Spieler-Ansicht. Keine sofortige Umsetzung; weiterhin offen.

- [x] ZIP-/7z-ROMs über den normalen Ladeauftrag vorbereiten und dauerhaft nach
  `data/roms/<SHA256>/` importieren. Die Bibliothek verweist auf diese Kopie;
  archivierte Saves werden nicht übernommen. Mehrere ROMs öffnen eine Spielauswahl
  mit Seiten, Tastatur/Controller und Abbruch; die Auswahl ist an den Archivhash gebunden.
- [x] **Tools → Record video** und Suchziel `AVI` öffnen die gemeinsame
  Einzelspiel-AVI-Aufnahme mit Bild und Ton. Ausgabe unter `data/recordings`.
  Pausen werden ausgelassen; Turbo und Zeitsprünge beenden den Clip.
- [x] Vorhandene Patch-Lab-Ergebnisse bleiben erhalten. Tests für GB/GBC/GBA-
  Archivimporte, Wiederverwendung der Kopie und Patch-Persistenz ergänzt.
- [x] **Patch and play** bzw. **Restore and play** importieren und fordern danach
  den Start über den normalen sicheren Ladeablauf an. **Apply Patch** bleibt ohne
  Spielwechsel. Verlassen der Patch-Seite oder Öffnen eines anderen Spiels verwirft
  den vorgemerkten Start, nicht das gespeicherte Ergebnis.
- [ ] Unter nativem Wayland testen: Archiv über Portal und Drop öffnen,
  Bibliotheks-Neustart ohne Originalarchiv, Video starten/beenden, ROM-Wechsel,
  Pause/Turbo und Tonratenwechsel. Die portable Testsuite auf Windows kann
  diese UI-/Treiberprüfung nicht ersetzen.
- [ ] Neue Wayland-Tests ausführen: `NativeArchiveChoiceSupportsPagesCancelAndLoadsOnlySelectedGame`,
  `NativeArchiveChangedAfterChoiceKeepsPreviousSession` und beide Fälle von
  `NativePatchAndPlayStartsOnlyWhileRequestRemainsOnPatchPage`. Dazu
  `AETHERBOY_UI_TESTS=1 dotnet test tests/AetherBoy.DesktopTests/AetherBoy.DesktopTests.csproj -c Release`
  in der nativen Wayland-Sitzung verwenden. Abbruch/Seitenwechsel und geändertes Archiv
  zusätzlich manuell mit Controller prüfen.
- [ ] Längere Clips in einem Videoplayer ansehen und hören. AVI ist noch
  unkomprimiert und auf 2 GiB begrenzt; MP4, Local-Link-Split-Video und automatische
  Patch-Auswahl sind nicht Teil des ersten Lieferumfangs.

## Phase-1-Nachprüfung — 01.10.2026

### Update: Phase 1.1 implementiert

- [x] Spielzeit wird unabhängig von Resume/Savestates gesichert: Checkpoint etwa
  alle 30 Sekunden über höchstens einen Hintergrundauftrag und eigener Flush beim
  Schließen, auch bei gestoppter/fehlerhafter Sitzung oder gescheitertem Resume.
  Fehlgeschlagene Schreibvorgänge bleiben pro ROM für einen erneuten Versuch erhalten;
  erfolgreich gesicherte Zeit wird nicht erneut addiert. Das gilt auch bei Spielwechsel.
- [x] Gemeinsame `ActivePlaytimeClock` mit Windows. Gezählt wird aktive Echtzeit,
  nicht emulierte Zeit. Pause, Menüs, Laden, Online-Sitzungen, Start/Stop/Fault und
  Abtastlücken ab zwei Sekunden zählen nicht. Online-Spielzeit bleibt bewusst ausgenommen.
- [x] Gemeinsame Metadatengrenzen, atomare Library-Schreibvorgänge mit Flush und
  letzter gültiger `.bak`. Wiederhergestellte oder unlesbare Einträge werden
  in der Bibliothek gemeldet. Identität und relativer Pfad werden geprüft.
  Ohne gültige Sicherung bleibt beschädigtes JSON unangetastet.
- [x] SDL-Rumble-Rückgabewerte werden ausgewertet. Fehlgeschlagene Ausgabe wird
  protokolliert und begrenzt wiederholt, nicht als erfolgreiche Erstaktivierung geführt.
  Erfolgreiche Impulse sind weiterhin auf 180 ms begrenzt; Gerätewechsel und Stopp
  sind unabhängig von SDL-Hardware regressiert.
- [x] Die gemeinsame portable Schreibprobe schreibt und flusht tatsächlich ein Byte.
- [ ] Native Wayland-/Controller-Abnahme und vollständige portable App-Starts mit
  Flag/Marker, echtem USB-Medium, Schreibschutz/vollem Datenträger bleiben offen.

`LinuxPhase11RegressionTests` prüft Recovery, defekte Felder/Pfade, Schreibfehler,
Spielzeit-Retry und Rumble-Ausgabe ohne native Geräte. Zusätzlich sind zwei native
Komforttests für Resume-Fehler/gestoppte Sitzung ergänzt: auf einem Windows-Host
werden sie ausdrücklich übersprungen, nicht als bestanden gewertet.
Aktueller vollständiger Desktop-Testlauf: **124 bestanden, 46 übersprungen,
0 fehlgeschlagen**. Runtime: 491 bestanden/5 übersprungen. Release-Build des
Frontends unter Windows erfolgreich, 0 Warnungen/Fehler. Das ist keine native
Wayland-Ausführung; die nativen Komfortfälle benötigen `AETHERBOY_UI_TESTS=1`
in einer isolierten Wayland-Sitzung.
Bei normal funktionierendem Datenträger begrenzen Checkpoints das Crash-Verlustfenster
auf ungefähr 30 Sekunden plus laufende I/O-Zeit. Bei dauerhaftem Schreibfehler oder
Prozessabbruch kann noch ungesicherte Zeit verloren gehen; Batterie-Saves sind getrennt.
Kein allgemeines GBA-Rumble, kein neues gemeinsames Windows/Linux-USB-Datenformat.

### Arbeitsstatus und zurückgestellte Abnahme

Phase 1 einschließlich 1.1 ist für diese Implementierungsrunde vorerst abgeschlossen.
Die offenen Controller-, Portable-/USB-, Speicherfehler- und Wayland-Prüfungen sind
in der [gemeinsamen Phase-1-Merkliste](WINDOWS_ROADMAP.md#arbeitsstatus-und-merkliste-nach-phase-11)
einzeln festgehalten. Sie bleiben bis zur tatsächlichen Durchführung offen.
Phase 2 ist im vereinbarten Umfang ebenfalls vorerst abgeschlossen; offene
Cheat-Spezialbefehle und echte Spieltests bleiben auf der
[Phase-2-Merkliste](CHEAT_SUPPORT.md#merkliste-offene-cheat-arbeit-01102026).
Kein Anspruch auf vollständige Hardware- oder Code-Kompatibilität.

### Historischer Ausgangsbefund vor der Reparatur

Das neue Phase-1-Paket (Portable Mode, Bibliotheksmetadaten, Rumble plus vorhandene
Spielzeit) ist in Grundzügen eingebaut, aber noch nicht vollständig abgenommen.
Der folgende Ausgangsbefund war die Analyse/Merkliste; aktueller Reparaturstand oben.
Plattformübergreifende Übersicht und Windows-Befunde:
[Windows-Phase-1-Nachprüfung](WINDOWS_ROADMAP.md#phase-1-nachprüfung--01102026).
Offene Cheat-Spezialbefehle und Spieltests:
[Phase-2-Merkliste](CHEAT_SUPPORT.md#merkliste-offene-cheat-arbeit-01102026).

Vorhanden: `--portable`/Marker, isolierte Data/Config/State/Cache-Pfade,
hashgeprüfte portable ROM-Kopie, relative Bibliothekspfade beim Verschieben,
Genre/Bewertung/Tags mit Suche/Sortierung/Filter, MBC5-Rumble-Ausgabe über SDL
und konfigurierbare Aktivierung. Die portable Runtime deckt GB/GBC-MBC5 ab;
GBA liefert noch kein eigenes Rumble-Signal. Native Ausgabe ist dadurch nicht
automatisch für alle Controller nachgewiesen.

### Historische Reparaturliste vor Phase 1.1

1. [ ] **P2: Spielzeit unabhängig von Resume sichern.**
   `WaylandEmulatorHost.Comfort.cs/SaveResumeOnClose` schreibt Spielzeit erst
   nach erfolgreicher State-Sicherung im selben try-Block. Bei Resume-Fehler
   überspringt die Ausführung das Update; bei Faulted/Stopped erfolgt bereits
   vorher der Rücksprung. Es gibt keine regelmäßigen Spielzeit-Checkpoints.
   Das kann die gesamte ungesicherte Sitzungszeit verlieren, nicht den Spielsave.
   Zeit-Flush vom Savestate entkoppeln, in begrenzten Abständen sichern, ausstehende
   Zeit bei I/O-Fehlern behalten und beim normalen Schließen separat abschließen.
2. [ ] **P2: Aktive Zeit exakt definieren und regressieren.**
   `UpdateComfort` prüft bisher im Zähler nicht explizit `State == Running`
   und verwendet andere Menüs-/Online-/Pause-Regeln als Windows. Starten,
   Pause, Menüs, Laden, Fault, Suspend, Turbo und Online-Warten mit kontrollierter
   Uhr testen; dabei keine künstlichen Emulationssekunden zählen.
3. [ ] **P2: Daten-/Fehlerfestigkeit der Library angleichen.**
   Linux normalisiert Titel/Zeit schon beim Lesen, Windows hat dafür noch eine
   nachgewiesene Lücke. Linux wiederum ersetzt Library-JSON atomar, führt hier
   aber keine letzte lesbare `.bak` wie Windows. Beschädigte Einträge nicht
   nur still überspringen; Meldung/Recovery ergänzen und gemeinsame Grenzen prüfen.
4. [ ] **P2: Native Rumble-Abnahme und Fehlerstatus.**
   Erfolg von `SDL.RumbleGamepad` wird bislang nicht ausgewertet. Controller ohne
   Motor/mit fehlender Treiberunterstützung von erfolgreicher Ausgabe unterscheiden;
   Fokusverlust, Pause, Hotplug, Ausschalten und Beenden nativ testen. Virtuelle
   Controller-Eingabetests beweisen keinen Vibrationsmotor.
5. [ ] **P2: Portable-Start als echter Ablauf prüfen.**
   Nicht nur Store-Methoden testen: Flag/Marker, anderer Arbeitsordner, verschobener
   kompletter Datenbaum samt Saves/States/Firmware/Settings, voller oder gesperrter
   USB-Datenträger, abgebrochener Import. Keine automatische Profilübernahme und
   kein stiller XDG-Fallback. Plattformübergreifendes USB-Profil wäre ein eigener
   Format-/Migrationsschritt, nicht heute bereits garantiert.

Neue Tests dieser Analyse: `LinuxPhase1LibraryTests` 3/3 auf dem Windows-Host,
zusätzlich gemeinsame Runtime 2/2 und Windows 2/2. Kein neuer nativer Wayland-
oder physischer Controller-Test; alte Linux-Hardware-Ergebnisse unten sind
historische Nachweise. Keine Produktionscode-Änderung und kein Push.

## Historischer Linux-Plan

Aktueller Stand: **12. September 2026**, Runde 3 auf `22a77ef` plus lokale Änderungen.
Ursprünglicher Plan: 11. September nach Aktualisierung auf `d983ed4`
(`feat(windows): centralize app data and development diagnostics`).
Die Ausgangsanalyse unten beschreibt den Stand vor diesem Ausbau. Der aktuelle Umsetzungsstand steht in der folgenden Tabelle.

## Restkritik / Runde 3 — aktueller Stand

Der Folgeauftrag wird anhand fester Fix-IDs nachverfolgt. Vollständige
Problem-/Lösungszuordnung: [Linux-Fixliste](LINUX_FIX_LOG.md).

| Rang | Fix | Wichtigkeit | Lieferstand |
| --- | --- | ---: | --- |
| 1 | R3-05 GBA-Owner bei Fokuswechsel und Laden schützen | 10/10 | Implementiert; deterministische native Regression |
| 2 | R3-01/02 Texteditor, Clipboard, IME und Eingabefokus | 9/10 | Implementiert für Titel, Suche und Cheats |
| 3 | R3-03 Semantische GTK-/ATK-/AT-SPI-Bedienung | 9/10 | Optionaler nativer Bedienpfad implementiert; tatsächliche Schnittstellen geprüft |
| 4 | R3-04/07 Abbrechbare ROM-Vorbereitung und Busy-Guards | 9/10 | Implementiert, höchstens ein Hintergrundauftrag |
| 5 | R3-06 Einstellungen ohne blockierendes regelmäßiges Schreiben | 8/10 | Implementiert; Snapshot/Generation/Retry, sichere Abschlussgrenzen |
| 6 | R3-08 Headerabstand | 6/10 | Bereits korrigiert und visuell geprüft |
| Manuell | Reale Geräte, Misch-DPI, Orca/IME, ARM64 und lange Spiele | 9/10 | Offen; keine automatisierte Behauptung einer Hardwareabnahme |

**Abschluss:** 108 Headless-Tests bestanden, 3 Audio-Skips, 0 Fehler.
Unabhängige Bewertung **UI 9,1/10 / Features 8,9/10**; beide Zielwerte im
geprüften Softwareumfang erreicht. x64-/ARM64-Pakete und normaler Start-Build
aktualisiert; ARM64-Ausführung bleibt offen.

Aktuelle Ergebnisse: [Playtest Runde 3](LINUX_PLAYTEST_ROUND3.md),
[Kritik Runde 3](LINUX_CRITIQUE_ROUND3.md). Frühere Pläne und Bewertungen unten
bleiben historische Nachweise. Offene Hardwareprüfungen und bewusste synchrone
Sicherungsgrenzen stehen ausdrücklich in der Fixliste.

## Komfortrunde 2 — 12. September 2026

Auf Basis von `22a77ef`, lokal implementiert:

| Rang | Verbesserung | Wichtigkeit |
| --- | --- | ---: |
| 1 | Datei-/Backup-Metadaten aus dem Renderpfad; begrenzte Worker | 10/10 |
| 2 | Resume, State-Galerie und Undo nach Laden | 9/10 |
| 3 | Getrennte Spielprofile mit globaler Vererbung | 8/10 |
| 4 | Favoriten, Titel, Systemfilter und aktive Spielzeit | 8/10 |
| 5 | Schriftstufen, echte Palettenmuster und Inhaltsnavigation | 8/10 |
| 6 | Geprüfte x64-/ARM64-Archive mit eingebetteter Runtime | 7/10 |

Historische Abnahme dieser Runde: [Kritik Runde 2](LINUX_CRITIQUE_ROUND2.md),
[Playtest Runde 2](LINUX_PLAYTEST_ROUND2.md). Ziele: Features >8, UI >9.
Die folgenden Abschnitte bleiben als historischer Plan erhalten. Langläufe
übernimmt weiterhin der Nutzer.

## Priorisierung und Umsetzungsstand

Bewertung der Wichtigkeit von 0–10; Datenverlust und Bedienblockaden stehen vor
zusätzlichen Werkzeugen. Umsetzung erfolgt in dieser Reihenfolge, mit L7 als
begleitender Abnahme. Bewertungen der Qualität stehen getrennt in
[Unabhängige Kritik](LINUX_CRITIQUE.md) und [Playtest](LINUX_PLAYTEST.md).

| Rang | Paket | Wichtigkeit | Implementierung |
| --- | --- | ---: | --- |
| 1 | L1 Datenintegrität | 10/10 | XDG-Ablage, SHA-256-Zuordnung, Migration ganzer Save-Familien, exklusive Schreibrechte, Settings-Backup |
| Begleitend | L7 Tests und unabhängige Prüfung | 10/10 | Native Tests, adversarielle Storage-/Bedientests, Playtest-Agent, Kritiker, CI für x64/ARM64 und headless Wayland |
| 2 | L2 Installation | 9/10 | Gemeinsamer Launcher, geprüfte neue Release-Ordner, Wechsel nach Vorbereitung, absoluter Desktop-Start, Uninstaller mit Datenerhalt |
| 3 | L3 Diagnose | 9/10 | Buildidentität, begrenzte lokale Berichte, Fehlercodes, ZIP-Export, Aufnahmepräferenz |
| 4 | UI-Kritik und Bedienblockaden | 9/10 | Lesbare Mindestschrift, Kontrast, Fokusmodell, F6-Navigation, klare Namen und Save-Slot-Status |
| 5 | L4 Audio und Ressourcen | 8/10 | GBA-Stereo bis SDL; Mono-API bleibt kompatibel; Queue-/Drop-Messwerte, kein komplettes Leeren bei Rückstau; gedrosselte Idle-/Pause-Ansicht |
| 6 | L5 Bibliothek und Save Recovery | 8/10 | Durchsuchbare zuletzt-gespielt-Liste, Relocate, Backup-Inspektion, bestätigte Wiederherstellung mit Vorher-Archiv, Export und .sav-Import |
| 7 | L6 Controller und Desktop | 7/10 | GUID-Profile, freie Belegung, Deadzone, Gerätewechsel, Controller-Menünavigation, optionale Fokus-Pause |
| 8 | L5 Firmware | 6/10 | Import von DMG-/CGB-Boot-ROM und GBA-BIOS, Größenprüfung, Auswahl beim nächsten ROM-Start |
| 9 | L5 Zusatzwerkzeuge | 5/10 | WAV-Aufnahme und Verwaltung vom Core unterstützter Cheats innerhalb einer Sitzung |
| Später | Weitere Distributionsformate | 4/10 | Flatpak/AppImage und Paket mit eingebetteter Runtime bleiben eigene Folgearbeit |

### Abnahmegrenzen

Die Umsetzung ersetzt keine Messungen an nicht vorhandener Hardware. Tests auf
KDE/GNOME, physische Controller-/Audiogerätewechsel, Suspend/Resume, Misch-DPI und
Monitore mit 120/144 Hz bleiben manuell zu qualifizieren. Die CI-Erweiterung ist
lokal vorbereitet; ein GitHub-Run und eine reale ARM64-Ausführung stehen aus.
Der nachträglich integrierte Commit `7ef5b59` ergänzt auch Windows-Stereo und
GB/GBC-Stereodaten. Beide Frontends nutzen nun den interleavten Audiopfad; die
Mono-API bleibt kompatibel. Windows-Ausführung muss auf Windows geprüft werden.

Die Testspiele sind erzeugte Programme. Reale GB/GBC/GBA-Spielkompatibilität und
hörbare Audioqualität werden daraus nicht abgeleitet. Die Qualitätsschwellen
UI >8/10 und Features >6/10 gelten für den dokumentierten Prüfbereich, nicht als
pauschale Release-Freigabe. Eine feste Benchmarksuite für Eingabelatenz und
Monitor-Framezeiten bleibt weitere Arbeit.

## Ausgangslage vor der Umsetzung

Linux besitzt bereits einen nativen SDL3-/Wayland-Host mit Hyprland-Erkennung,
Control Center, Portal-Dateidialog, Drag-and-drop, Tastaturbelegung, Gamepad-Hotplug,
Audio, fünf Save-State-Slots und Rewind. Atomare Batterie-Saves mit Backups und
Integritätsprüfung sind vorhanden. VSync wird bereits angefordert. Diese Grundlagen
werden weiterentwickelt.

Der neue Windows-Commit ergänzt zentrale, nach ROM-Inhalt getrennte Ablage und
lokale Development-Berichte. Linux verwendet dagegen weiterhin ROM-nahe Saves
und States. Einstellungen werden atomar unter XDG_CONFIG_HOME gespeichert; bei
unlesbaren Einstellungen werden Defaults geladen, ohne Wiederherstellung aus einer
letzten lesbaren Sicherung.

## Empfohlene Reihenfolge

| Schritt | Priorität | Ergebnis | Umfang |
| --- | --- | --- | --- |
| L1 | Hoch | Zentrale Linux-Datenablage und sichere Migration | Groß |
| L2 | Hoch | Verlässlicher installierter Starter und Aktualisierung | Mittel |
| L3 | Hoch | Lokale Diagnose mit Buildidentität und Export | Mittel |
| L4 | Hoch | Audio und Frame-Pacing anhand von Messwerten verbessern | Groß |
| L5 | Mittel | Bibliothek, Save-Verwaltung und Firmware-Auswahl | Groß, aufteilbar |
| L6 | Mittel | Controller-Einrichtung und Desktop-Alltag verbessern | Mittel |
| L7 | Laufend | Linux-CI, echte Spieltests und Release-Qualifikation | Mittel, fortlaufend |

Die Größen sind relative Planungsschätzungen, keine Terminzusagen. L1 ist der
erste Umsetzungsschritt. L7 begleitet jede Änderung; L3 liefert Messwerte für L4.

## L1 – Datenablage und Spielstände

**Befund:** `WaylandEmulatorHost.TryLoadRom` übergibt eine `.sav` neben der ROM an
die Runtime; `LinuxSaveStateStore.GetPath` bildet `.ss1` bis `.ss5` ebenso. Ein
schreibgeschützter ROM-Ordner ist damit zum Speichern ungeeignet. Gleichnamige ROMs
mit verschiedenen Endungen im selben Ordner erhalten dieselben abgeleiteten Pfade.

**Plan:** Einen Linux-Pfaddienst einführen und Speicheridentitäten aus dem SHA-256
des ROM-Inhalts bilden. Der Runtime können ROM- und Save-Pfad bereits getrennt
übergeben werden. Vorgeschlagene Ablage:

| Basis | Inhalt unter `aetherboy/` |
| --- | --- |
| `$XDG_CONFIG_HOME` | `settings.json`, letzte lesbare Einstellungssicherung |
| `$XDG_DATA_HOME` | `saves/<hash>/`, `states/<hash>/`, `firmware/`, optionale importierte `roms/<hash>/`, Bibliothekskatalog |
| `$XDG_STATE_HOME` | Sitzungsberichte, Crashlogs und zuletzt geöffnete Spiele |
| `$XDG_CACHE_HOME` | Wiederherstellbare Vorschaubilder und andere Caches |

Diese Zuordnung ist eine Projektentscheidung auf Basis der
[XDG Base Directory Specification](https://specifications.freedesktop.org/basedir/latest/).
Leere oder relative XDG-Werte erhalten die spezifizierten Standardpfade.
Spielstände gehören als wertvolle, übertragbare Nutzerdaten in die Datenablage.

Bestehende `.sav`-, RTC-, Guard-, Backup- und State-Dateien beim ersten Öffnen
kontrolliert übernehmen. Zentrale Dateien haben Vorrang, Originaldateien bleiben
erhalten. Unterbrochene Migrationen müssen wiederholbar sein; widersprüchliche
Altbestände müssen sichtbar werden. ROM-Kopien können später als expliziter
Bibliotheksimport hinzukommen. Programmdateien und Nutzerdaten getrennt halten:
Der bisherige Installer verwendet bereits `$XDG_DATA_HOME/aetherboy` als Programmziel.
Für neue Installationen einen eigenen Programm-Unterordner vorsehen und alte
Installationen gezielt migrieren.

**Abnahme:** Speichern aus einem schreibgeschützten ROM-Ordner; identische ROM nach
Verschieben wiedererkennen; gleichnamige unterschiedliche ROMs getrennt halten;
Migration nach Abbruch erneut ausführen; vorhandene zentrale Saves erhalten;
beschädigte Einstellungen aus Sicherung laden. Zwei Instanzen dürfen dieselben
Spielstände nicht unbemerkt überschreiben: Schreibbesitz pro ROM festlegen und testen.

## L2 – Installation und Updates

**Befund:** `run-linux.sh` erkennt ein benutzerlokales .NET über PATH und DOTNET_ROOT.
`install-linux-user.sh` installiert dagegen einen direkten Symlink auf den Apphost.
Der Desktop-Eintrag verwendet `Exec=aetherboy %f` und `TryExec=aetherboy` und hängt
damit vom PATH der grafischen Sitzung ab. Außerdem wird beim Installieren nur bei
fehlender Ausgabe gebaut; ein älterer vorhandener Publish-Ordner kann übernommen werden.

**Plan:** Gemeinsamen Launcher für Terminal und App-Menü erstellen, installierte
Pfade korrekt quotieren und auf einem minimalen Desktop-PATH testen. Vor Installation
frisch veröffentlichen oder Aktualität verlässlich prüfen. Updates zunächst in einem
separaten Verzeichnis vorbereiten und erst nach erfolgreicher Prüfung aktivieren.
Deinstallation entfernt Programmdateien, Starter und Icons; persönliche Spieldaten
werden nur auf ausdrücklichen Wunsch entfernt.

**Abnahme:** Start aus App-Menü und Dateimanager ohne Terminalumgebung; Pfade mit
Leerzeichen; benutzerlokales SDK; angepasste XDG-Verzeichnisse; Update nach neuem
Commit; fehlgeschlagenes Update erhält die vorige Installation.

Danach ein optionales Paket mit eingebetteter .NET-Runtime für x64/ARM64 bewerten.
Systembibliotheken bleiben zu prüfen. Flatpak oder AppImage erst nach diesen
Grundlagen als separaten Distributionsschritt evaluieren.

## L3 – Diagnose für Linux

**Befund:** Die Oberfläche zeigt Live-Werte, `Program` meldet abgefangene Fehler
primär über stderr. Die neue Windows-Sitzungsaufzeichnung ist dort implementiert;
Linux hat noch keinen entsprechenden persistenten Bericht. Die SDL-Metadaten
enthalten eine fest eingetragene Produktversion.

**Plan:** Produktversion, Commit und Buildkanal aus Buildmetadaten beziehen.
Lokale, begrenzte Development-Berichte mit Compositor, SDL-/Audiotreiber,
ROM-Hash, Sessionzustand, Save-/Load-/Rewind-Ergebnissen und Fehlern ergänzen.
Diagnosedaten müssen auch bei Startproblemen außerhalb eines Terminals auffindbar
sein. Export als ZIP im Control Center, begrenzte Aufbewahrung und Abschaltoption.
Keine ROM-/Save-Inhalte, privaten Dateipfade oder automatischen Uploads aufnehmen.

Das Windows-Berichtsformat als Ausgangspunkt verwenden. Wiederverwendbare
Metadaten und Ereignisschemata plattformneutral halten; Linux referenziert keine
WinForms-Klassen. Gemeinsame Änderungen mit Windows abstimmen und dort testen.

**Abnahme:** Fehler beim Start, beim ROM-Laden und beim Speichern erscheinen in
lokalen Berichten; ZIP lässt sich prüfen; Aufbewahrungsgrenze greift; stable- und
development-Kanal verhalten sich dokumentiert; Logging blockiert keine Emulation.

## L4 – Audio, Frame-Pacing und Ressourcenverbrauch

**Befund:** `SdlAudioOutput` öffnet einen Mono-Stream. Der GBA-Runtime-Adapter mischt
Stereo bereits vor der Ausgabe auf Mono herunter. Bei mehr als etwa 125 ms
Eingangsdaten in der Queue wird diese vollständig geleert. Die Hauptschleife
zeichnet fortlaufend, fordert VSync an und wartet zusätzlich zwei Millisekunden.
Eine messbare Verbesserung ist bisher nicht durch Benchmarks belegt.

**Plan:** Zuerst Queue-Füllstand, Leerungen, Audiofehler, produzierte/präsentierte
Frames, Framezeiten und CPU-Verbrauch erfassen. Queue-Länge allein ist keine
vollständige Ausgangslatenzmessung; SDL beschreibt sie als noch nicht konvertierte
Eingangsdaten: [SDL_GetAudioStreamQueued](https://wiki.libsdl.org/SDL3/SDL_GetAudioStreamQueued).
Dann Pufferstrategie und Vorfüllung verbessern, statt bei Rückstau pauschal zu leeren.
Standardgerät-Wechsel, Abziehen und Suspend/Resume mit SDL testen und fehlende
Fehlerbehandlung gezielt ergänzen. Der bestehende
[SDL-Audiostream](https://wiki.libsdl.org/SDL3/SDL_OpenAudioDeviceStream) bleibt Ausgangspunkt.

Stereo als eigenen gemeinsamen Runtime-Schritt planen: Kanalzahl und Sampleformat
im Audiokontrakt ausdrücken, GBA-Stereodaten erhalten und beide Frontends anpassen.
Eine Änderung nur an `Channels = 1` würde das Problem nicht lösen.

VSync-Ergebnis prüfen, Darstellung bei 60/120/144 Hz messen und bei Pause,
Minimierung und leerem Fenster unnötige Renderarbeit reduzieren. Die
Emulationsgeschwindigkeit muss weiterhin unabhängig von der Monitorrate bleiben.

**Abnahme:** Reproduzierbarer Vorher-/Nachher-Bericht auf derselben Hardware;
mindestens 30 Minuten ohne Audioausfall; Gerätewechsel erholt sich; Pause/Turbo/
Rewind erzeugen keine dauerhaft aufgestaute Ausgabe; geringerer Idle-Verbrauch;
kein beschleunigtes Spiel auf Displays mit hoher Bildrate. Latenzziele erst anhand
der Ausgangsmessung festlegen.

## L5 – Bibliothek und fehlende Werkzeuge

Auf L1 aufbauen und in kleine Lieferungen teilen:

1. Bibliothek mit Suche, zuletzt gespielt, fehlenden Pfaden und Ordnerzugriff.
   Hash-basierte Saves bleiben auch nach Verschieben der Original-ROM zugeordnet.
2. Save Center um Backup-Inspektion, gezielte Wiederherstellung und Import/Export
   ergänzen; vorhandene Core-Schutzmechanismen wiederverwenden. Belegte Slots,
   Zeitpunkte und inkompatible States verständlich anzeigen.
3. Boot-ROM-/GBA-BIOS-Auswahl mit Größen-/Formatprüfung, persistenter Auswahl und
   sichtbarem HLE-/BIOS-Status. Aktuell wird beim Laden immer `bootRom: null` übergeben.
4. WAV-Aufnahme und unterstützte Cheats nachordnen. Die Oberfläche muss die
   tatsächlichen Fähigkeiten des gemeinsamen Kerns widerspiegeln.

**Abnahme:** Spiele aus der Bibliothek starten, verschwundene Quellen neu zuordnen,
Backup ohne Verlust des bisherigen Saves wiederherstellen, Firmware-Auswahl über
Neustart erhalten und unpassende Save-State-/BIOS-Kombinationen klar ablehnen.

## L6 – Controller und Desktop-Alltag

Gamepad-Belegung, Geräteauswahl und einstellbare Stick-Deadzone ergänzen; Profile
stabil einem Gerät zuordnen. Hotplug ist vorhanden, aber das erneute Auswählen
eines schon angeschlossenen Ersatzcontrollers nach Abziehen gezielt prüfen.
Wichtige Aktionen und das Control Center vollständig per Controller bedienbar machen.

Fokusverlust löscht bereits Tasten und Turbo. Darauf eine wählbare automatische
Pause aufbauen, die den vorherigen manuellen Pausezustand erhält. Suspend/Resume,
Monitorwechsel und Skalierungen 100/125/150/200 Prozent unter Hyprland, KDE und
GNOME prüfen. Portal-Abbruch und fehlendes Portal brauchen verständliche Rückmeldung.

**Abnahme:** Zwei physische Controller wechseln und neu verbinden; Belegung bleibt
erhalten; keine klemmenden Tasten; Fokuswechsel pausiert/reaktiviert korrekt;
Bedienelemente bleiben bei gemischter DPI-Skalierung erreichbar.

## L7 – Laufende Qualitätssicherung

Die Linux-CI prüft bereits Core, Runtime, Frontend-Logik und Plattform-Erkennung.
Der native UI-Test ist opt-in und gehört in einen zusätzlichen echten oder
geeignet verschachtelten Wayland-Testlauf. Die bloße Angabe von WAYLAND_DISPLAY
ersetzt keinen Compositor. Den bisherigen schnellen Testpfad beibehalten.

Build-/Installations-Smoketests und ARM64-Ausführung ergänzen. Testuntergrenzen
an die aktuellen Suiten anpassen: Die vorhandenen Linux-CI-Grenzen von 87 Runtime-
und 15 Desktop-Tests liegen deutlich unter dem lokal ermittelten Umfang.
Frontend-Verantwortlichkeiten beim jeweiligen Ausbau aus der inzwischen rund
1.800 Zeilen großen Host-/UI-Kombination herauslösen, etwa Storage, Controller und
Diagnose; kein vollständiger Architekturumbau als Vorbedingung.

Pro Linux-Meilenstein mindestens ein GB-, GBC- und GBA-Spiel auf Boot, Spielablauf,
Audio, Ingame-Save, Neustart, State und Rewind testen. GBA zusätzlich 30–60 Minuten
am Stück. Test-ROMs und synthetische UI-Tests ergänzen diese Spieltests. Jeder
Kernfehler erhält einen reproduzierbaren Regressionstest und eine Prüfung beider
Frontends. Windows-spezifische Tests müssen auf Windows laufen.

## Lokal geprüfte Ausgangsbasis

Umgebung: CachyOS, x64, Hyprland/Wayland, .NET SDK 10.0.302.

- `bash scripts/build-linux.sh`: erfolgreich veröffentlicht.
- Core: 175 Tests bestanden.
- Runtime: 100 Tests bestanden.
- Desktop-Logik: 37 bestanden, ein nativer UI-Test zunächst erwartungsgemäß übersprungen.
- Anschließend mit `AETHERBOY_UI_TESTS=1`: alle 38 Desktop-Tests bestanden,
  einschließlich des nativen Wayland-UI-Tests. Insgesamt 313 bestandene Tests
  über Core, Runtime und die vollständige Desktop-Suite.
- `bash scripts/run-linux.sh --audio-info`: Wayland-/Hyprland-Backend und
  PipeWire-Audio erfolgreich initialisiert. Das ist kein Audio-Langzeittest.

Reale Spiel-/Langzeittests und physische Controller-Tests sind nicht Teil dieser
Planerstellung.

## Verifizierter Implementierungsstand

- 175 Core-Tests, 101 Runtime-Tests und 57 Desktop-Tests inklusive nativem
  Wayland und synthetischem GB/GBC/GBA-Playtest bestehen auf CachyOS/Hyprland.
- Die Desktop-Suite besteht auch mit aktivem CI-Warnungsgate. Virtuelle SDL-
  Controller prüfen Eventrouting, Remapping, Fokus und Hotplug ohne Screenshots.
- Ubuntu 24.04 x64 wurde zusätzlich in einem isolierten Container mit echtem
  Weston-Headless-Compositor und Software-Rendering geprüft: 54 bestanden,
  drei Audio-Playtests dort bewusst übersprungen, keine Fehler. Kein GPU-,
  PipeWire- oder ARM64-Nachweis durch diesen Containerlauf.
- Die vollständige Lösung einschließlich Windows-Zielprojekt baut unter Linux
  mit `EnableWindowsTargeting=true`, ohne Warnungen oder Fehler. Windows wurde
  dabei nicht ausgeführt.
- Der reale Installer besteht isolierte Tests für fehlgeschlagene Migration,
  Leerzeichen/Sonderzeichen, Minimal-PATH ohne dotnet, fehlgeschlagenes Update
  und Deinstallation mit erhaltenen Saves (`scripts/test-linux-install.py`).
- Unabhängiger Kritiker: UI **8,2/10**, Features **7,9/10**. Separater Playtester:
  Features **7,8/10**. Kriterien und Grenzen stehen in den verlinkten Berichten.
- Ein einmaliger Vorher-/Nachher-Lauf des versteckten nativen Wayland-Fensters
  ohne ROM über je acht Sekunden benötigt 0,882 gegenüber 0,211 CPU-Sekunden
  (rund 76 % weniger in dieser Messung). Das ist kein sichtbarer Monitor-,
  Eingabelatenz- oder Gameplay-Benchmark. Werkzeug: `tools/AetherBoy.LinuxIdleProbe`.
  JSON mit Assembly-Hashes: `artifacts/idle-baseline.json`, `artifacts/idle-current.json`.

Die begonnenen 30-Minuten-Läufe sind nicht als bestanden abgenommen. Der Nutzer
übernimmt die Langzeitprüfungen selbst; weitere automatisierte Langläufe wurden
auf seinen Wunsch gestoppt. Details und Start-Builds stehen im Playtest-Bericht.


## Nachtrag: neuer Windows-Commit und Input-Layout

`development` wurde auf `7ef5b59` aktualisiert. Die lokalen Linux-Erweiterungen
bleiben erhalten; gemeinsame Stereo-Verträge, README-Tabellen, Changelog und CI
wurden zusammengeführt. Der Controller-Setup-Button sitzt schmaler über der
Tastenliste und überlappt die Up-Belegung nicht mehr (nativer Capture geprüft).

Die kombinierte Version besteht 181 Core-, 127 Runtime- und 57 Desktop-Tests,
einschließlich kurzer nativer GB/GBC/GBA-Läufe. Der vollständige Build einschließlich
Windows-Zielprojekt hat null Warnungen und Fehler; Windows wurde nicht ausgeführt.
Keine erneuten 30-Minuten-Läufe. Die früheren Bewertungen und Messungen oben
beschreiben weiterhin ihre jeweiligen Prüfstände.


## Nachtrag: Linux Patch Lab auf Basis von `c1ffe10`

Library → Patch Lab verbindet jetzt den vorhandenen gemeinsamen IPS/BPS/UPS-Patcher
mit Dateiauswahl, Drag-and-drop und einem Hintergrundauftrag. Ergebnisse werden
unter `Data/roms/<hash>/` abgelegt und können direkt geöffnet werden. Originale und
Saves bleiben unverändert; bekannte Ergebnisse werden wiederverwendet. UPS-Undo
ist ausdrücklich auswählbar. Patch-Erstellung und Downloads gehören nicht dazu.

Acht neue Tests prüfen Import, Fehler, Save-Erhalt, UPS-Undo und native Bedienung;
aktuell 65/65 Desktop-Tests bestanden. Zwei gezielte Captures zeigen normales und
kleines Fenster ohne Überlappung. Die bisherige unabhängige UI-Gesamtnote wurde
für diese neue Unterseite nicht neu erhoben. Anleitung und Übergabe stehen in
[Linux User Guide](LINUX_USER_GUIDE.md#11-patch-lab-ips-bps-and-ups) und
[Linux-Handoff](LINUX_DEVELOPMENT_HANDOFF.md).
