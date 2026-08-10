# AetherBoy – Changelog

Dieses Dokument unterscheidet bewusst zwischen vorhandenen, verifizierten und noch nicht freigegebenen Funktionen.

## Unveröffentlicht – Aether-Wave-UI

### Markenfundament

- Die vom Projektinhaber entworfene Aether-Wave-Richtung als reproduzierbare Vektormarke rekonstruiert: ein verlaufendes `A`, eine aufgelöste Signalwelle und eine integrierte Handheld-Silhouette.
- Detaillierte Mastermarke und optisch vereinfachte Small-Mark für Windows-Systemflächen getrennt, damit das Zeichen auch bei 16 × 16 Pixeln lesbar bleibt.
- Multi-Resolution-ICO mit 16, 20, 24, 32, 40, 48, 64, 128 und 256 Pixeln sowie 512-Pixel-Anwendungsgrafik aus denselben SVG-Quellen erzeugt.
- Altes `N3`-Bild, historische Designer-Icons und das kryptisch benannte 2018er Anwendungsicon entfernt.
- EXE, Hauptfenster, Werkzeuge und About-Dialog beziehen ihre Marke nun aus einer zentralen eingebetteten Branding-Ressource.
- Markenfarben, Größenregeln und der absichtlich sparsame Einsatz des Aether-Verlaufs in `branding/BRAND.md` dokumentiert.

## 4.8.0-alpha.1 – Phase 8 (2026-08-09)

### APU und Wave-RAM

- Sweep-Shift 0 führt auf dem 128-Hz-Takt die Overflow-Prüfung aus, ohne die Frequenz zurückzuschreiben; Sweep-Negate-Latch und zweistufige Overflow-Prüfung sind vervollständigt.
- APU-Power-On richtet den Frame-Sequencer an der aktuellen DIV-Hälfte aus und überspringt die nächste Flanke, wenn die Quellflanke beim Einschalten bereits hoch war.
- DMG- und CGB-Wave-Startphase, laufende Frequenzänderungen, modellabhängige Wave-RAM-Lese-/Schreibfenster sowie die DMG-Retrigger-Korruption implementiert.
- Analogen modellabhängigen Hochpass mit gehaltenem Kondensatorzustand bei abgeschalteten DACs ergänzt.
- Blargg `dmg_sound` und `cgb_sound` bestehen nun jeweils **12/12** Einzel-ROMs.

### PPU, Serial und Diagnose

- Sprite-Transfergrenze in Mode 3 um die inklusive letzte Fetch-Kante präzisiert; die Mooneye-PPU-Auswahl steigt auf **11/12**.
- Serial-Port von sofortigem Byteabschluss auf acht hardwaregetaktete Bits umgestellt: 512 T-Zyklen pro DMG/CGB-Normalbit, 16 T-Zyklen im CGB-Fast-Modus sowie externer Clock-Eingang und optionaler Bit-Gerätevertrag.
- Laufende Serial-Transfers einschließlich Clockphase, Restbits und Schieberegister in Save States aufgenommen.
- Conformance-Timeouts berichten nun Blargg-Zwischenstand, CPU-Register, DIV, NR52 und aktuellen Opcode statt eines leeren Ergebnisses.

### Verifikation und Grenzen

- Testsuite auf **167 Tests** erweitert: 141 Core-, 17 Runtime- und 9 Windows-Smoke-Tests. Das Save-State-Komponentenschema steigt für neue PPU-, APU- und Serial-Transienten auf 5.
- Reproduzierbare Matrix: **67/70** Läufe bestanden, keine blockierenden Fehler; offen bleiben `rapid_toggle`, `lcdon_timing-GS` und `sources-GS`. Drei lokale Spiele liefen erneut jeweils 600 Frames absturzfrei.
- Ein echter Pixel-FIFO, subzyklische Timer-/DMA-Buskonflikte und ein konkreter Link-Transport zwischen zwei Instanzen bleiben offen. Serielle Bitsemantik und externe Clock-Anbindung sind vorhanden, TCP/Netzwerk bewusst noch nicht.

## 4.7.0-alpha.1 – Phase 7 (2026-08-09)

### Kompatibilität und CPU-Bus

- Manifestgesteuerte Conformance-CLI mit Pflicht- und Informationsläufen, Conformance-/Game-Smoke-Modus, Protokolldiagnosen, finalem PC, Frame-SHA-256 und JSON-Berichten ergänzt.
- CPU-Buszugriffe an T-Zykluspositionen verschoben; Stack, CALL/RET/RST/PUSH, Interrupt-Re-Selektion und Abbruch, DAA, signierte SP-Flags sowie mehrere `(HL)`-Kosten korrigiert.
- Blargg-CPU-Einzelsuite mit 11/11 und `instr_timing` vollständig bestanden.

### Timer, DMA und PPU

- TIMA-/TMA-Schreibkollisionen im Reload-Takt, IF-Lesemaske sowie OAM-DMA-Register, Startverzögerung und Neustart während eines Transfers implementiert.
- LCD-Aus-/Einschaltphase, LYC-Freeze, OAM-Randfenster, überlappende Sprite-Fetches und der DMG-spezifische OAM-STAT-Impuls bei VBlank gehärtet.
- Mooneye-Ergebnis auf 12/13 Timer- und 10/12 PPU-ROMs erhöht; bekannte Restfälle sind in `COMPATIBILITY.md` dokumentiert.

### APU, Zustände und Gates

- APU-Registermasken, DIV-APU-Resetflanke, echte 64-/256-Schritt-Längenzähler, Trigger-Extra-Clock, DMG-Power-Off-Längenregister und initiale Sweep-Overflow-Prüfung implementiert.
- Blargg-Sound-Fortschritt auf 6/12 DMG- und 7/12 CGB-Einzel-ROMs erhöht; Sweep-Negate, analoger Hochpass und DMG-Wave-RAM-Kollisionen bleiben offen.
- Testsuite auf **154 Tests** erweitert: 128 Core-, 17 Runtime- und 9 Windows-Smoke-Tests. Save-State-Komponentenschema wegen neuer CPU-, PPU- und APU-Transienten auf 4 angehoben.
- Drei lokal ignorierte Spiele jeweils 600 Frames absturzfrei ausgeführt; ROMs und externe Test-Binärdateien bleiben außerhalb des Repositorys.

## 4.6.0-alpha.1 – Phase 6 (2026-08-09)

### CPU, Bus und CGB-DMA

- Die elf nicht belegten LR35902-Opcodes verriegeln die CPU nun nach dem Opcode-Fetch bis zum Reset; anstehende Interrupts lösen die Verriegelung nicht.
- General-DMA und HBlank-DMA von sofortiger Blockkopie auf progressive Übertragung mit einem Byte je zwei Dots und 32 CPU-Stall-Dots pro 16-Byte-Block umgestellt.
- HBlank-DMA startet keinen Block während CPU-HALT, bleibt blockweise abbrechbar und hält Quelle, Ziel, Restblöcke sowie Teilblockfortschritt vollständig im Save State.
- CGB-exklusive DMA-, Palette-, VRAM-/WRAM-Bank- und Speed-Register im DMG-Modell geschlossen; unbenutzte Bits von SVBK werden auf CGB hoch gelesen.

### PPU und APU

- Mode 3 von einer festen Dauer auf 172 bis 289 Dots umgestellt. Fine-Scroll, sichtbarer Fensterstart und bis zu zehn ausgewählte Sprites liefern deterministische Fetch-Strafen; die feste Scanline-Dauer von 456 Dots bleibt erhalten.
- CGB-Paletten-RAM während Mode 3 genauso wie VRAM gesperrt und die maximale Mode-3-Dauer mit einem eigenen Grenztest abgesichert.
- NR52 als APU-Master-Power und Kanalstatus implementiert: Abschalten leert die APU-Register und Kanäle, erhält Wave-RAM und blockiert reguläre APU-Schreibzugriffe bis zum Wiedereinschalten.
- DAC-Abschaltung beendet aktive Kanäle sofort; Längenablauf aktualisiert die NR52-Statusbits. Puls-, Wave- und Noise-Kanäle laufen über ganzzahlige Hardwareperioden statt über host-sampleratenabhängige Phasen.
- NR50/NR51 steuern jetzt Masterlautstärke und Links-/Rechts-Routing im Mixer. Ein Vollframe-Allokationsgate schützt den APU-Hotpath.

### Conformance, Langlauf und Tests

- `HeadlessConformanceRunner` um Blargg-Memory-Status und Mooneye-Registersignaturen ergänzt; serielles `Passed`/`Failed`, Fehlerausgabe und Timeouts liefern nun das erkannte Protokoll zurück.
- Neue plattformneutrale `AetherBoy.Conformance`-CLI für einzelne ROMs oder rekursive lokale Suites ergänzt, einschließlich Frame-Limit, aussagekräftiger Exitcodes und optionalem JSON-Bericht.
- Restore/Replay-Langlauf von 300 auf 600 Frames verdoppelt und um laufende DMA-, PPU- und APU-Teilzustände erweitert.
- Gesamtsuite auf **127 Tests** erweitert: 101 Core-, 17 Runtime- und 9 Windows-Smoke-Tests. Windows- und Linux-CI bauen die Conformance-CLI; Linux führt zusätzlich ihren Hilfe-Smoke-Test aus.
- Deterministischen Zustandsvertrag wegen der neuen CPU-, DMA-, PPU- und APU-Felder auf Komponentenschema 3 angehoben und zusätzliche Plausibilitätsprüfungen für aktive Transfers und Audiokanäle ergänzt.

### Weiterhin offen

- CPU-Instruktionen und allgemeine Buszugriffe bleiben intern atomar; DMA-Quellbuskonflikte und seltene LCD-/HBlank-Umschaltkanten sind noch nicht vollständig mikrozyklusgenau.
- Die variable Mode-3-Dauer modelliert Fetch-Strafen ohne echten Pixel-FIFO. Mid-Scanline-Registereffekte, Sprite-Fetch-Abbrüche und mehrere Grenzkombinationen bleiben angenähert.
- Der digitale APU-Pfad besitzt noch keinen analogen Hochpassfilter; modellabhängige Power-off-Längenregister und seltene Frame-Sequencer-Schreibkanten sind offen.
- Externe Conformance-ROMs sind aus Lizenzgründen nicht enthalten. Link-Kabel, serielles Bit-Timing, Game Genie und Spezialmapper bleiben spätere Arbeit.
- Save States aus Komponentenschema 1 oder 2 werden bewusst abgelehnt; eine automatische Migration ist nicht vorhanden.

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

Nach Phase 6 folgt die Release-Härtung: breitere, lokal bereitgestellte Conformance-Suites, echte Pixel-FIFO- und Bus-Mikrozyklen, APU-Analogeffekte sowie Praxisvalidierung auf unterstützten Windows-Systemen.
