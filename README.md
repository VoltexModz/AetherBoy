# AetherBoy

> **Status: Alpha / experimentell.** AetherBoy ist eine laufende Modernisierung und noch kein verlässlicher Emulator-Release. Phase 5 ergänzt Timing- und Prioritätskorrekturen, einen seriellen Headless-Conformance-Runner und komprimierten Rewind; Link-Kabel und mehrere Hardware-Randfälle bleiben offen.

AetherBoy ist ein Windows-Emulator für Game Boy und Game Boy Color in C#. Das Projekt begann 2014 als `nanoboy` und wurde später als **ChiiBoy Color** weitergeführt. Produkt und Assembly heißen jetzt einheitlich **AetherBoy 4.5.0-alpha.1**; der historische Namespace und Projektordner `nanoboy` bleiben vorerst erhalten.

AetherBoy ist weder von Nintendo autorisiert noch mit Nintendo verbunden. Game Boy, Game Boy Color und zugehörige Produktnamen sind Marken ihrer jeweiligen Rechteinhaber.

## Technischer Stand

- Windows-Forms-Anwendung auf **.NET 10 LTS**
- plattformneutraler `AetherBoy.Core` auf `net10.0` mit DMG- und CGB-Codepfaden
- exklusiver Emulations-Owner-Thread mit typisierten Befehlen und unveränderlichen Snapshots
- verwaltete WinForms-Bildausgabe mit Sharp-, Smooth- und LCD-Grid-Filter
- NAudio-WinMM-Ausgabe als Windows-Adapter außerhalb des Emulator-Cores
- zusammengeführte Tastatur- und XInput-Eingabe ohne gegenseitiges Freigeben gehaltener Tasten
- 107 deterministische Tests einschließlich Mapper-, RTC-, STAT-, DMA-, PPU-Prioritäts-, APU-Sequencer-, Save-State-, Rewind-, Owner-Thread-, WAV-, UI- und generierten ROM-End-to-End-Gates
- reproduzierbarer NuGet-Restore sowie Windows- und Linux-Gates in GitHub Actions

Phase 5 schließt mehrere konkrete Hardwarelücken: STOP schläft bis zu einer neuen Joypad-Flanke, OAM-DMA überträgt ein Byte je vier T-Zyklen und sperrt dabei den CPU-Bus, DMG/CGB-Pixelpriorität basiert auf rohen Farbindizes und CGB-Attributen, und die APU taktet Länge, Sweep und Envelope über den 512-Hz-Frame-Sequencer. Ein begrenzter Headless-Runner wertet das serielle `Passed`/`Failed`-Protokoll generierter oder externer Test-ROMs aus. Rewind-Zustände werden komprimiert und ein 300-Frame-Replay schützt die deterministische Fortsetzung.

Der vollständige Zustandsvertrag aus Phase 4 bleibt erhalten und wurde für die neuen CPU-, DMA-, Serial- und APU-Zustände auf Komponentenschema 2 erweitert. Zustände aus dem vorherigen Schema werden bewusst abgelehnt; eine automatische Migration ist noch nicht vorhanden.

## Funktionsstatus

„Nicht freigegeben“ bedeutet: Code kann vorhanden sein, die Funktion ist aber bis zu einer Korrektur und Verifikation deaktiviert oder unzuverlässig.

| Bereich | Status | Bekannte Einschränkung |
| --- | --- | --- |
| CPU und Scheduler | verbessert, experimentell | Double-Speed, EI/DI, Interruptkosten, HALT-Wakeup, HALT-Bug sowie STOP-Ruhemodus und Joypad-Wakeup besitzen Regressionstests; Instruktionen bleiben intern atomar und allgemeine Buszugriffe sind noch nicht T-Zyklus-genau. |
| Timer | verbessert, experimentell | 16-Bit-Divider, TAC-Flanken und verzögerter Overflow sind getestet; seltene Schreibkollisionen im Reload-Takt bleiben angenähert. |
| Bildausgabe | verbessert, experimentell | Kombinierte STAT-Flanken, LCD-Abschaltung, VRAM-/OAM-Zugriffsfenster, Fenster-Clipping, Paletten-Wrap sowie DMG/CGB-Hintergrund-/Sprite-Priorität sind getestet; ein echter Pixel-FIFO und dessen variable Mode-3-Länge fehlen noch. |
| DMA | verbessert, experimentell | OAM-DMA kopiert 160 Bytes in 640 T-Zyklen und sperrt den CPU-Bus bis auf HRAM; General- und HBlank-DMA übertragen sequenziell, aktualisieren Register und unterstützen Abbruch. CGB-Buskonflikte und CPU-Stalls der CGB-DMA-Modi bleiben angenähert. |
| DMG/CGB-ROM-Laden | verbessert, experimentell | Header-, Titel-, Größen- und Truncation-Prüfung sowie stabile ROM-Identität sind vorhanden; nur legal beschaffte ROM-Dumps verwenden. |
| MBC1/MBC2/MBC3/MBC5 | implementiert, experimentell | Banking, RAM-Freigabe, MBC2-Nibble-RAM, MBC3-RTC mit Halt/Carry/Latch und MBC5-Rumble-Maske sind getestet; MBC1M-Sonderverdrahtung bleibt offen. |
| MBC4 und weitere Spezialmapper | nicht freigegeben | MMM01, MBC4, Pocket Camera, HuC1/HuC3 und weitere Spezialhardware werden mit klarer Fehlermeldung abgelehnt. |
| NAudio-Ausgabe | verbessert, experimentell | Masterclock, Sample-Akkumulator, Puffergröße und der 512-Hz-APU-Frame-Sequencer für Länge, Sweep und Envelope sind getestet; DAC-Power, exakte Frequenz-Timer und mehrere Register-Nebenwirkungen bleiben angenähert. |
| WAV-Aufnahme | verbessert, experimentell | Schreiben und Header-Finalisierung sind synchronisiert und getestet; Datei-I/O und Stop laufen außerhalb des UI- und Emulations-Threads. Lange Aufnahmen und Gerätefehler benötigen noch breitere Praxistests. |
| Save States | implementiert, experimentell | Fünf Slots (`.ss1` bis `.ss5`), F5/F8 und Controller-Shortcuts sind aktiv. Zustände sind SHA-256-geschützt und an die exakte ROM sowie DMG/CGB gebunden; eine spätere Schema-Version kann eine Migration erfordern. |
| Rewind | implementiert, experimentell | Erfasst alle vier Frames, hält höchstens 150 Brotli-komprimierte Zustände (rund zehn Sekunden) und veröffentlicht den tatsächlich belegten Speicher. Der Puffer ist sitzungsgebunden und wird bei Reset oder geladenem Save State neu begonnen. |
| GameShark | teilweise implementiert | Einfache RAM-Writes sind vorhanden; Validierung und Nebenwirkungsgrenzen fehlen. |
| Game Genie | deaktiviert | Codes werden noch nicht im ROM-Lesepfad angewendet. |
| Serial/Link-Kabel | teilweise implementiert | Der unmittelbare Byte-Transfer, Serial-Interrupt und ein begrenzter `Passed`/`Failed`-Conformance-Runner sind vorhanden; TCP-Link-Kabel, Bit-Timing und zwei gekoppelte Emulatorinstanzen fehlen. |
| Debugger/Disassembler | intern/experimentell | Kein vollständiger Pause-/Step-Workflow; mehrere Grenzfälle sind ungeprüft. |

## Bauen, testen und starten

Voraussetzungen:

- Windows
- .NET SDK **10.0.302** oder ein kompatiblerer Patch derselben Feature-Band
- für Audioausgabe ein funktionierendes Windows-WinMM-Gerät

Im Repository-Root:

```powershell
dotnet restore ./nanoboy.sln --locked-mode --configfile ./NuGet.config
dotnet build ./nanoboy.sln -c Release --no-restore
dotnet test --solution ./nanoboy.sln -c Release --no-build --no-restore
dotnet run --project ./nanoboy/nanoboy.csproj -c Release --no-build
```

`global.json` pinnt das SDK, `packages.lock.json` pinnt den aufgelösten Paketgraphen. Der CI-Workflow prüft die gesamte Anwendung auf Windows und Core plus Runtime zusätzlich auf Linux.

## ROMs, Boot-ROMs und Spielstände

Dieses Projekt erteilt **keine** Rechte an kommerziellen Spielen, Nintendo-Firmware, Boot-ROMs, Marken, Grafiken oder sonstigen Drittinhalten. Verwende nur ROM- und Firmware-Dumps, die du nach dem für dich geltenden Recht selbst verwenden darfst. Verbreite keine ROMs oder Boot-ROMs zusammen mit Quellcode oder Builds. Diese Hinweise sind keine Rechtsberatung.

Historische `.gb`-/`.gbc`-Dateien und ein persönlicher `.sav` wurden in Phase 0 aus dem veröffentlichbaren Quellbaum entfernt. Die lokale Arbeitskopie bewahrt sie ausschließlich im ignorierten Verzeichnis `.local-assets/roms` auf. Sie sind **nicht** von der GPL des Emulatorcodes umfasst und dürfen nicht zum Repository oder zu einem Release hinzugefügt werden.

Boot-ROM-Dateien wie `dmg_boot.bin` oder `gbc_boot.bin` sind zum Bauen nicht erforderlich und müssen – sofern ihre Nutzung legal ist – vom Benutzer selbst bereitgestellt werden.

Save States werden neben der geladenen ROM als `.ss1` bis `.ss5` abgelegt. Sie enthalten keine ROM- oder Boot-ROM-Daten, sondern deren Identitätsbindung; ein Zustand lässt sich deshalb nicht versehentlich in eine andere ROM-Sitzung laden.

## Repository-Hygiene

Die verbindlichen Abhängigkeiten stehen in den Projektdateien, ihre Auflösung in den Lockfiles. Buildausgaben, IDE-Zustand, lokale SDK-Werkzeuge, ROMs, Boot-ROMs, Save-Dateien, Logs und Symbole sind ausgeschlossen. Der eingeschränkte NuGet-Feed, Vulnerability-Audit, gepinnte GitHub Actions, Linux-Portabilitätsgate und automatische Abhängigkeitsupdates sichern diese Grenzen ab.

## Lizenz und Herkunft

Der Emulatorcode wird als **GNU General Public License Version 3** dokumentiert; siehe [LICENSE](LICENSE). Mangels einer ausdrücklichen „or later“-Erklärung wird konservativ `GPL-3.0-only` verwendet. Copyright und Urheberschaft verbleiben bei den jeweiligen ursprünglichen Autoren und späteren Beitragenden.

Die Projektchronik nennt Frédéric Meyer als ursprünglichen Entwickler (2014) und dokumentiert spätere ChiiBoy-/AetherBoy-Modifikationen. Drittanbieterkomponenten besitzen eigene Lizenzen; direkte Pakete und Hinweise stehen in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
