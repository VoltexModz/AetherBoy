# AetherBoy – Changelog

Dieses Dokument unterscheidet bewusst zwischen vorhandenen, verifizierten und noch nicht freigegebenen Funktionen.

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

Phase 1 trennt einen plattformneutralen Core vom Windows-Frontend und führt einen deterministischen Scheduler, einen klaren Thread-Besitzvertrag sowie erste automatisierte Core-Tests ein.
