# AetherBoy – Changelog

Dieses Dokument unterscheidet bewusst zwischen vorhandenen, verifizierten und noch nicht freigegebenen Funktionen.

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

Phase 2 trennt den Emulator-Core vom Windows-Frontend, führt einen vollständigen Owner-Thread/Command-Queue-Vertrag ein und baut ROM-basierte Hardware-Conformance-Gates aus.
