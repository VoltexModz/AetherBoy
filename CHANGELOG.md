# AetherBoy – Changelog

Dieses Dokument unterscheidet bewusst zwischen vorhandenen, verifizierten und noch nicht freigegebenen Funktionen.

## 4.5.0-alpha.1 – Phase 5 (2026-08-09)

### CPU, Bus und DMA

- STOP ohne vorbereiteten CGB-Speed-Switch als echten Ruhezustand modelliert; eine neue Joypad-Flanke weckt die CPU, während PC und Hardwaretakte im Schlaf stehen bleiben.
- OAM-DMA von einer sofortigen 160-Byte-Kopie auf ein Byte je vier T-Zyklen beziehungsweise 640 T-Zyklen Gesamtdauer umgestellt.
- CPU-Zugriffe während OAM-DMA auf HRAM begrenzt; der DMA-Lesepfad berücksichtigt Boot-ROM-Mapping und der laufende Transfer ist vollständig im Save State enthalten.
- Generiertes OAM-DMA-Test-ROM auf eine während des Transfers aus HRAM ausgeführte Routine migriert.

### PPU und APU

- Scanline-Rendering ohne Tile-/Zeilen-Heapallokationen neu aufgebaut und mit einem harten Allokationsgate für einen vollständigen Frame abgesichert.
- DMG-Spritepriorität anhand des rohen Hintergrund-Farbindex statt der bereits palettierten ARGB-Farbe korrigiert.
- CGB-Hintergrundattribut Bit 7, OBJ-Priorität und LCDC.0 als Master-Priorität korrekt kombiniert; Hintergrund und Fenster bleiben auf CGB auch bei gelöschtem LCDC.0 sichtbar.
- APU auf den achtstufigen 512-Hz-Frame-Sequencer umgestellt: Länge bei 256 Hz, Channel-1-Sweep bei 128 Hz und Lautstärke-Hüllkurven bei 64 Hz.
- Frame-Sequencer, OAM-DMA, STOP und Serial-Control in das neue Komponentenschema 2 des deterministischen Zustandsvertrags aufgenommen.

### Conformance, Rewind und Tests

- Plattformneutralen, framebegrenzten `HeadlessConformanceRunner` ergänzt, der das verbreitete serielle `Passed`/`Failed`-Protokoll von Test-ROMs auswertet.
- Seriellen Byte-Transfer mit lesbarem Control-Register und unmittelbar ausgelöstem Serial-Interrupt vervollständigt.
- Rewind-Historie auf schnelle Brotli-Kompression umgestellt und eine öffentliche Metrik für tatsächlich gehaltene Bytes ergänzt; Anzahl und Zeitfenster bleiben strikt begrenzt.
- Byte-identischen 300-Frame-Restore/Replay-Langlauftest sowie neue STOP-, DMA-, PPU-Prioritäts-, Renderer-Allokations-, APU-Sequencer-, Serial- und Rewind-Speichertests ergänzt.
- Gesamtsuite auf **107 Tests** erweitert: 81 Core-, 17 Runtime- und 9 Windows-Smoke-Tests; CI-Mindestzahlen entsprechend angehoben.

### Weiterhin offen

- CPU-Instruktionen und allgemeine Buszugriffe sind noch nicht mikrozyklusgenau; CGB-OAM-Buskonflikte und CPU-Stalls bei General-/HBlank-DMA sind angenähert.
- Die PPU verwendet noch keinen echten Pixel-FIFO mit variabler Mode-3-Dauer; APU-DAC-Power, Frequenz-Timer und mehrere Register-Nebenwirkungen benötigen weitere Präzisierung.
- Der Headless-Runner stellt die lokale Conformance-Infrastruktur bereit, enthält aber bewusst keine fremden Test-ROMs. TCP-Link-Kabel und serielles Bit-Timing bleiben offen.
- Save States aus Komponentenschema 1 werden wegen der neuen deterministischen Felder bewusst abgelehnt; eine automatische Migration ist nicht vorhanden.

## 4.4.0-alpha.1 – Phase 4 (2026-08-09)

### Vollständige Save States

- Sämtliche für eine deterministische Fortsetzung relevanten Zustände von CPU, Scheduler, Hauptspeicher, Timer, Interrupts, Video, Audio, Cartridge, DMA, Joypad und Serial in einzeln versionierten `AETHSTAT`-Pflichtsektionen erfasst.
- CPU-Transienten wie EI-Verzögerung, HALT-Bug und Double-Speed-Phase sowie PPU-Arbeits-/Ausgabeframes, APU-Oszillatorphasen, Sample-Akkumulator und noch nicht ausgegebene Samples aufgenommen.
- Boot-ROM-Nutzung per Länge und SHA-256 gebunden, ohne Firmware- oder ROM-Inhalte in Zustandsdateien einzubetten.
- Alle Abschnitte, Mapperregister und Wertebereiche werden vorbereitet und validiert, bevor die erste Komponente verändert wird; falsche ROMs, beschädigte Dateien und semantisch ungültige Payloads lassen die laufende Sitzung unverändert.
- Zustandsdateien besitzen harte Größenlimits, eine Dokument-SHA-256 und werden über eine temporäre Datei atomar ersetzt.

### Runtime und Bedienung

- Capture, Restore und Rewind als typisierte FIFO-Befehle auf dem exklusiven Emulations-Owner-Thread ergänzt.
- Nach Reset, Restore oder Rewind alte Audioereignisse verworfen, Frame-Pacing zurückgesetzt und eine neue monotone Bildgeneration veröffentlicht.
- Fünf Save-Slots neben der ROM (`.ss1` bis `.ss5`), F5/F8 und XInput-Schultertasten aktiviert; Datei-I/O blockiert weder UI noch Emulations-Thread.
- Rewind auf denselben vollständigen Zustandsvertrag migriert, alle vier Frames erfasst und strikt auf 150 Zustände beziehungsweise ungefähr zehn Sekunden begrenzt.
- Das Hauptfenster schreibt beim Start keine unveränderten Einstellungen mehr; fehlende Schreibrechte für die UI-Konfiguration verhindern nicht länger den Programmstart.

### Tests und CI

- Byte-identische Capture/Restore/Capture-Roundtrips und identische zukünftige Ausführung nach Restore verifiziert.
- Falsche ROM, neu signierte aber semantisch ungültige Komponenten, atomare Dateiablage, Rewind-Timeline und Rewind-Kapazität als Regressionstests ergänzt.
- Owner-Thread-Zugriff, defensive Pufferkopien, Frame-Neuveröffentlichung, Audio-Generationswechsel und aktivierte WinForms-Menüs abgesichert.
- Gesamtsuite auf **94 Tests** erweitert: 68 Core-, 17 Runtime- und 9 Windows-Smoke-Tests; CI-Mindestzahlen entsprechend angehoben.

### Weiterhin offen

- Save States sind absichtlich an die exakte ROM und das jeweilige Komponentenschema gebunden; eine automatische Migration künftiger inkompatibler Schemata ist noch nicht vorhanden.
- Rewind ist ein speicherresidenter Sitzungspuffer ohne Kompression oder Vorschau-Timeline.
- Link-Kabel, Game Genie, Spezialmapper und die in Phase 3 genannten Timing-/Hardware-Randfälle bleiben offen.

## 4.3.0-alpha.1 – Phase 3 (2026-08-09)

### Cartridge und Mapper

- ROM-Header auf Mindestlänge, deklarierte ROM-Größe, moderne RAM-Größen und Sondercodes für 72/80/96 ROM-Bänke validiert.
- CGB-Titelfeld korrekt begrenzt und jede geladene ROM über eine stabile SHA-256-Identität gebunden.
- Gemeinsamen Cartridge-Vertrag mit defensiven Mapper-Zuständen, begrenztem RAM und explizitem Flush/Dispose eingeführt.
- MBC1-Banking, RAM-Freigabe und beide Banking-Modi korrigiert; verbotene Banknummern werden hardwaregerecht umgebogen.
- MBC2 mit adressbitgesteuerten Registern, 512×4-Bit-RAM und Spiegelung implementiert.
- MBC3-Banking und RTC-Register mit Latch, Halt, 512-Tage-Carry, injizierbarer Zeitquelle und atomarer RTC-Persistenz implementiert.
- MBC5 mit vollständiger 9-Bit-ROM-Bank, Bank 0, bis zu 16 RAM-Bänken und separater Rumble-Maske implementiert.
- Battery-RAM wird im Speicher geändert und an Lebenszyklusgrenzen atomar ersetzt, statt bei jedem Byte ein Datei-Handle zu öffnen.

### PPU, DMA und APU

- STAT als gemeinsame, flankengesteuerte Interruptleitung für LYC sowie Modi 0/1/2 modelliert; schreibgeschützte Statusbits können nicht mehr überschrieben werden.
- LCD-Abschaltung setzt LY/Modus unmittelbar zurück; CPU-Zugriffe auf VRAM und OAM beachten die gesperrten PPU-Modi.
- Fenster außerhalb des sichtbaren Bereichs, CGB-Paletten-Autoinkrement, Zehn-Sprites-Limit, DMG-Sprite-Reihenfolge und vertikal gespiegelte 8×16-Sprites korrigiert.
- OAM-DMA auf alle 160 Bytes korrigiert.
- CGB-General- und HBlank-DMA kopieren fortlaufende 16-Byte-Blöcke, aktualisieren Quell-/Zielregister und bilden Abschluss sowie Abbruch in FF55 ab.
- NR41–NR44 sind ohne Exception lesbar und bewahren die beschreibbaren Noise-Felder.

### Deterministischer Zustandsvertrag

- Neues `AETHSTAT`-Format mit Versions- und Mindestleserversion, ROM-SHA-256, DMG/CGB-Modell und einzeln versionierten Pflichtsektionen entworfen.
- Deterministische Abschnittsreihenfolge, Duplikatprüfung, Vorwärtskompatibilität für optionale Sektionen, harte Größenlimits und SHA-256-Integritätsprüfung implementiert.
- Mapper-Zustände besitzen einen eigenen begrenzten Binärcodec und können verlustfrei in echte Mapper zurückgespielt werden.
- Das historische unvollständige Save-State-v1 und Rewind bleiben bewusst deaktiviert; Phase 4 liefert vollständige Payloads für alle Pflichtsektionen.

### Tests und CI

- Mapper-, ROM-Header-, RTC-, DMA-, STAT-, Speicherzugriffs- und Zustandsvertrag-Regressionstests ergänzt.
- Zweites vollständig generiertes Test-ROM führt OAM-DMA über CPU, Loader und Bus bis zum letzten OAM-Byte aus.
- Gesamtsuite auf **83 Tests** erweitert; Windows- und Linux-Mindesttestzahlen entsprechend angehoben.

### Weiterhin offen

- T-Zyklus-genaue OAM-DMA-Buskonflikte, vollständiger STOP-Ruhemodus, PPU-FIFO/CGB-Pixelpriorität und APU-Frame-Sequencer.
- MBC1M, MBC4, MMM01, Kamera- und HuC-Spezialhardware.
- Vollständige Save-State-Payloads, Rewind und Link-Kabel bleiben deaktiviert.

## 4.2.0-alpha.1 – Phase 2 (2026-08-01)

### Architektur und Plattformgrenzen

- Den Emulator als eigenständiges `AetherBoy.Core`-Projekt auf plattformneutrales `net10.0` ausgelagert.
- Windows Forms, `System.Drawing`, XInput und NAudio vollständig aus der Core-Assembly entfernt.
- Host-Tastencodes durch die logische `[Flags]`-Eingabemaske `GameBoyButtons` ersetzt.
- Persistente UI-Einstellungen von der unveränderlichen Core-Konfiguration `EmulatorConfiguration` getrennt.
- DMG-Paletten ohne `System.Drawing.Color` als bitidentische `0xAARRGGBB`-Werte abgebildet.
- NAudio als reinen Windows-Ausgabeadapter außerhalb des Cores neu angebunden.

### Owner-Thread und Oberfläche

- `AetherBoy.Runtime` mit einem exklusiven Owner-Thread eingeführt: Konstruktion, Frames, Eingaben, Einstellungen, Cheats, Snapshots und Disposal laufen auf genau diesem Thread.
- Typisierte, geordnete Commands für Eingaben, Konfiguration, Palette, Pause, Turbo, Reset, Cheats und Shutdown ergänzt; endliche Batches verhindern Command-Starvation.
- ROM-, Audio-, Cheat- und Wave-RAM-Snapshots werden unveränderlich und defensiv kopiert veröffentlicht.
- Zwei dauerhaft vorallokierte Frame-Puffer ersetzen die frühere Kopie pro Emulationsframe; ein synchronisierter Austausch liefert der Oberfläche nur vollständige Bilder ohne laufende Large-Object-Heap-Allokationen.
- WinForms greift nicht mehr direkt auf `Nanoboy`, `CPU`, `Memory`, `Video`, `Audio` oder mutable Cheat-Listen zu.
- Tastatur und Gamepad werden als unabhängige Zustände zusammengeführt; ein Gerät kann eine vom anderen gehaltene Taste nicht mehr freigeben.
- Audio-Inspector und WAV-Aufnahme auf Session-Snapshots beziehungsweise kopierte Sampleblöcke umgestellt.
- Externe Audio-Consumer über einen begrenzten Hintergrundkanal vom Emulations-Owner-Thread entkoppelt; langsame Consumer können weder Frames noch Shutdown blockieren.
- WAV-Schreiben und Header-Finalisierung gegen gleichzeitige Zugriffe synchronisiert, Fehlerpfade schließen Handles zuverlässig und Datei-I/O blockiert weder Oberfläche noch Emulations-Thread.
- Geordneter, idempotenter Shutdown entsorgt den Core ausschließlich auf seinem Owner-Thread und meldet Fehler über `Completion`, `Fault` und den Sessionstatus.

### Tests und CI

- 15 deterministische Runtime-Tests für Threadbesitz, FIFO, Pause, Frame-Austausch, begrenzten Audio-Dispatch, Commands, WAV, Shutdown und Fehlerpfade ergänzt.
- Einen vollständig selbst erzeugten 32-KiB-Test-ROM durch Loader, CPU, Scheduler und WRAM ausgeführt; keine fremden ROM-Daten werden eingecheckt.
- Architekturtests verhindern neue WinForms-, Drawing- oder NAudio-Referenzen im Core.
- Joypad-Gesamtzustand, ARGB-Paletten und parallele WAV-Finalisierung mit Regressionstests abgesichert.
- Gesamtsuite auf **59 Tests** erweitert.
- GitHub Actions um ein Linux-Gate für Core und Runtime, feste Mindesttestzahlen sowie die .NET-10-Testsyntax `--solution`/`--project` ergänzt.

### Weiterhin offen

- T-Zyklus-genaue Buszugriffe, vollständiger STOP-Ruhemodus sowie breitere PPU-, DMA-, Mapper- und APU-Conformance.
- Save States, Rewind und Link-Kabel bleiben deaktiviert, bis ihr Zustands- und Timingmodell neu aufgebaut und ROM-basiert verifiziert ist.
- Game Genie bleibt deaktiviert; GameShark-RAM-Writes bleiben experimentell.

## 4.1.0-alpha.1 – Phase 1 (2026-07-31)

### Plattform und Build

- Zielplattform von .NET 8 auf **.NET 10 LTS** aktualisiert und SDK `10.0.302` über `global.json` festgelegt.
- OpenTK 3 und GLControl vollständig entfernt; damit entfällt der .NET-Framework-Kompatibilitätsfallback.
- NAudio auf das kleinere Laufzeitpaket `NAudio.WinMM 2.3.0` aktualisiert.
- Reproduzierbaren Restore über `NuGet.config` und drei `packages.lock.json` eingeführt.
- GitHub Actions mit eingeschränkten Rechten, gepinnten Action-SHAs, Release-Build und Tests ergänzt.
- Dependabot für NuGet- und Actions-Aktualisierungen eingerichtet.

### Core und Timing

- Zentrale Hardwaretaktdaten mit 4.194.304 Hz, 456 Dots pro Scanline und 70.224 Dots pro Frame eingeführt.
- Normal- und Double-Speed-Taktdomänen getrennt; PPU/APU laufen nicht mehr mit doppelter Framezahl.
- Interrupts werden an Instruktionsgrenzen priorisiert, benötigen 20 CPU-Ticks und wecken HALT auch bei gelöschtem IME.
- EI-Verzögerung, DI-Abbruch, RETI, HALT-Bug und CGB-STOP-Speed-Toggle korrigiert.
- Timer auf einen 16-Bit-Divider mit TAC-Falling-Edges und verzögertem TIMA-Reload umgestellt.
- Driftarmes Frame-Pacing auf die tatsächlichen rund 59,7275 Hz statt pauschaler 16 ms umgestellt.

### Bild, Eingabe und Audio

- OpenGL-Immediate-Mode durch einen verwalteten WinForms-Renderer mit Seitenverhältnis, Letterboxing und drei Filtern ersetzt.
- Framebuffer als kopierten, sequenzierten Snapshot veröffentlicht; das frühere GCHandle-Leak und gleichzeitiges Lesen/Schreiben entfallen.
- Gamepad-Eingabe über die Windows-Systemkomponente XInput angebunden; Disconnect setzt Controllerzustände zurück.
- Audio-Sampling auf einen rationalen 4.194.304-Hz-Akkumulator umgestellt.
- Puffergröße von versehentlichen 1025 auf exakt 1024 Samples korrigiert und NAudio-Queue begrenzt.
- Pulse-, Wave- und Noise-Längenzähler sowie Noise-LFSR-Periode korrigiert.
- Headless-Audiobackend für deterministische Tests ergänzt; veralteten OpenAL-/Thread-Abort-Code entfernt.

### Tests

- Den bisherigen Console-Smoke-Test in ein echtes MSTest-/Microsoft-Testing-Platform-Projekt umgewandelt.
- Neues Core-Testprojekt für Hardwaretakt, Timerfrequenzen und -overflow, Interruptpriorität, HALT-Wakeup und -Bug, EI/DI/STOP, PPU-Framegrenzen sowie Audio-Samplezahl und Kanal-Längen ergänzt.

### Weiterhin offen

- T-Zyklus-genaue Buszugriffe, vollständiger STOP-Ruhemodus, vollständige PPU/STAT-/DMA-Prioritäten und APU-Frame-Sequencer.
- Plattformneutrale Trennung des Emulator-Cores vom Windows-Frontend.
- Vollständiger Owner-Thread/Command-Queue-Vertrag für alle UI-Mutationen.

## 4.0.0-alpha.1 – Phase 0 (2026-07-31)

### Geändert

- Projektstatus auf **Alpha / experimentell** korrigiert; die Bezeichnung „High-End Edition“ wurde entfernt.
- Produkt-, Assembly- und Dateiversion zentral auf `4.0.0-alpha.1` beziehungsweise `4.0.0.0` vereinheitlicht.
- Alte ChiiBoy-Produktmetadaten aus `AssemblyInfo.cs` entfernt; Urheberschaft wird weiterhin dokumentiert.
- Visual-Studio-Lösung auf aktuelle Metadaten bereinigt und die verwaiste `Debugger`-Konfiguration entfernt.
- README, GPLv3-Lizenz, Drittanbieterhinweise, `.gitignore`, `.gitattributes` und `.editorconfig` ergänzt.
- Kommerzielle ROMs und der persönliche Spielstand aus dem veröffentlichbaren Quellbaum entfernt und in ein ignoriertes lokales Archiv verschoben.
- Alte Build-, Release-, IDE- und Paketkopien entfernt.
- Crashprotokolle werden nicht mehr ins Arbeitsverzeichnis geschrieben, sondern unter `%LOCALAPPDATA%\AetherBoy\Logs` abgelegt.
- Das In-App-Changelog liest nun dieselbe `CHANGELOG.md`, die auch im Repository liegt.

### Repariert

- Den unvollständigen Audio-Inspector-Designer rekonstruiert.
- Vier Pegelanzeigen, Wellenformanzeige und WAV-Aufnahmebutton werden wieder erzeugt und ins Layout eingebunden.
- Der Aktualisieren-Schalter verwendet nun seinen `Checked`- statt seines `Enabled`-Zustands.
- Ein automatisierter Smoke-Test prüft Initialisierung und Aktualisierungsschalter des Audio Inspectors.
- Veraltete und inkonsistente Fenstertitel durch eine zentrale Produkt-/Versionsanzeige ersetzt.

### Vorläufig deaktiviert

- Save States und Save-Slots: Das bisherige Binärformat ist inkonsistent und bildet keinen vollständigen deterministischen Zustand ab.
- Rewind: Verwendet denselben fehlerhaften Save-State-Unterbau.
- Link-Kabel/Netplay: Die TCP-Oberfläche ist noch nicht mit der emulierten seriellen Hardware verbunden.
- Game Genie: Codes werden noch nicht im ROM-Lesepfad angewendet.

GameShark-RAM-Codes bleiben als **experimentelle** Funktion sichtbar. Das ist keine Aussage über Timing- oder Thread-Sicherheit.

### Bekannte technische Schulden

- Kein automatisiertes Testprojekt und keine Hardware-Conformance-Gates.
- CPU-, Timer-, DMA-, Mapper-, PPU- und APU-Timing benötigen Korrekturen.
- OpenTK 3.x wird unter .NET 8 nur über NuGets .NET-Framework-Kompatibilitätsfallback eingebunden.
- Der OpenAL-Backendcode verwendet noch nicht unterstützte Thread-Abbruchlogik.
- Der Emulator-Core besitzt noch keinen einzelnen Owner-Thread mit Command-Queue und sicheren Snapshots.

## Historie

### ChiiBoy Color 3.2.2 (2021/2022)

- Oberflächen- und Branding-Anpassungen.
- OpenTK-GLControl-Renderer und Audio-Werkzeuge.

### nanoboy 1.0.0 (2014)

- Ursprünglicher kompakter Game-Boy-Color-Emulator von Frédéric Meyer.
- LR35902-Core, grundlegende Mapper-, Video- und Audioimplementierung.
- Projektchronik und Altmetadaten nennen GNU GPLv3 als Lizenz.

## Nächster Meilenstein

Phase 5 konzentriert sich auf breitere ROM-basierte Conformance, T-Zyklus-genauere Bus- und DMA-Effekte, PPU-Prioritäten, APU-Sequenzierung sowie messbare Performance- und Langzeitstabilität.
