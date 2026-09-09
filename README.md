# AetherBoy

> **Status: Alpha / experimentell.** AetherBoy ist eine laufende Modernisierung und noch kein verlässlicher Emulator-Release. GB/GBC besitzen breite automatisierte Abdeckung; der neu integrierte GBA-Pfad braucht noch echte Spiel-, Audio- und Speichertests.

AetherBoy ist ein Emulator für Game Boy, Game Boy Color und experimentell Game Boy Advance in C#. Neben dem vollständigen Windows-Frontend entsteht ein eigener nativer Linux-Host für Wayland und Hyprland; X11/XWayland wird dort bewusst nicht verwendet. Das Projekt begann 2014 als `nanoboy` und wurde später als **ChiiBoy Color** weitergeführt. Produkt und Assembly heißen jetzt einheitlich **AetherBoy 4.8.0-alpha.1**; der historische Namespace und Projektordner `nanoboy` bleiben vorerst erhalten.

AetherBoy ist weder von Nintendo autorisiert noch mit Nintendo verbunden. Game Boy, Game Boy Color, Game Boy Advance und zugehörige Produktnamen sind Marken ihrer jeweiligen Rechteinhaber.

Ausführlicher Projektstand: [Deutsch](docs/PROJECT_STATUS_DE.md) · [English](docs/PROJECT_STATUS_EN.md)

## Technischer Stand

`.gba`-Dateien laufen jetzt im normalen AetherBoy-Fenster über einen vendorten,
verwalteten Snapshot des MIT-lizenzierten GBADotnet-Kerns. Bild, Eingabe inklusive
L/R, PSG plus Direct Sound, SRAM/Flash/EEPROM/RTC, versionierte Save States,
Rewind, Frameskip, sichere GBA-RAM-/CodeBreaker-/GameShark-Codes, lokale
GBA-Kerndiagnostik und eine deterministische Zwei-Core-Linkbasis sind an
AetherBoys Runtime angebunden.
Ohne proprietäre BIOS-Datei übernimmt ein eingebauter HLE-Fallback die üblichen
Systemroutinen. Der Upstream-Kern ist
selbst als WIP gekennzeichnet: Ein ROM-Start ist
daher noch kein Nachweis für vollständige Spielbarkeit. Herkunft, Bestand und
bekannte Grenzen stehen im [GBADotnet-Review](docs/GBADOTNET_REVIEW.md) und im
[GBA-Status](GBA.md). Der kleine eigene ARM/Thumb/Bus/Mode-3-Prototyp bleibt als
separater Lern- und Regressionpfad erhalten; mGBA bleibt reine Referenz.

- Windows-Forms-Anwendung auf **.NET 10 LTS**
- nativer SDL3-Linux-Host für Wayland mit eigenem Hyprland-Profil, XDG-Portal-Öffnen, Audio, Tastatur/Gamepad, Vollbild, Control Center, fünf Save-State-Slots und Rewind
- plattformneutraler `AetherBoy.Core` auf `net10.0` mit DMG- und CGB-Codepfaden
- vendorter, MIT-lizenzierter C#-GBA-Kern hinter demselben Owner-Thread-Vertrag
- exklusiver Emulations-Owner-Thread mit typisierten Befehlen und unveränderlichen Snapshots
- verwaltete WinForms-Bildausgabe mit Sharp-, Smooth- und LCD-Grid-Filter
- eigenständige Aether-Wave-Oberfläche mit rahmenloser Fenster-Chrome, Display-Bühne, Live-Sessionleiste, direktem Command-Deck, persistentem Cartridge-Vault, eigenen Signal-Dialogen und ROM-Drag-and-drop
- zentrales Aether Control Center für Live-Status, Display, Audio, Eingabe, Saves, Emulationsoptionen und lokale Diagnose
- NAudio-WinMM-Ausgabe als Windows-Adapter außerhalb des Emulator-Cores
- zusammengeführte Tastatur- und Gamepad-Eingabe über Windows Gaming Input mit Hot-Plug, semantischer PlayStation-/HID-Unterstützung und XInput-Fallback
- atomare Batterie-Spielstände mit 30-Sekunden-Sicherungsintervall, SHA-256-Integritätswächtern, drei rotierenden Backups, automatischer Rettung und eigenem Save Safety Center
- 305 deterministische Tests: 175 Core-, 87 Runtime-, 28 Windows-Smoke- und 15 Linux-Frontendtests einschließlich Mapper-/MBC1M-, RTC-, CPU-Bus-, Interrupt-, DMA-, PPU-Timing-, APU-Power-, Serial-, Batterie-Save-, Save-State-, Rewind-, Owner-Thread-, WAV-, Gamepad-, Control-Center-, Aether-Wave-UI-, Dialog-Chrome-, Cartridge-Vault-, GBA-HLE-BIOS-, PSG-, GPIO/RTC-, Serial-Link-, Diagnose-, Mosaic-, EEPROM-/Flash-, Cheat-, Header-/Bildgeometrie-, ARM7-, Wayland-Profil- und atomaren Linux-State-Pfaden
- reproduzierbarer NuGet-Restore sowie Windows- und Linux-Gates in GitHub Actions

Phase 8 schließt die ausgewählten Blargg-Soundsuiten auf DMG und CGB mit jeweils 12/12 ab. Dazu gehören Sweep-Shift-0/Negate, DIV-abhängiges APU-Power-On, modellabhängige Wave-Startphasen, DMG-Wave-RAM-Zugriff und Retrigger-Korruption sowie ein analoger Hochpass. Der Serial-Port überträgt nun acht echte Bits mit Normal-, CGB-Fast- oder externer Clock und ist vollständig im deterministischen Zustand enthalten. Die PPU-Auswahl steigt durch die präzisierte Sprite-Transfergrenze auf 11/12. Die aktuelle, reproduzierbare Matrix steht in [COMPATIBILITY.md](COMPATIBILITY.md).

Der vollständige Zustandsvertrag aus Phase 4 bleibt erhalten und wurde für die neuen PPU-, APU- und Serial-Transienten auf Komponentenschema 5 erweitert. Zustände aus älteren Komponentenschemata werden bewusst abgelehnt; eine automatische Migration ist noch nicht vorhanden.

## Funktionsstatus

„Nicht freigegeben“ bedeutet: Code kann vorhanden sein, die Funktion ist aber bis zu einer Korrektur und Verifikation deaktiviert oder unzuverlässig.

| Bereich | Status | Bekannte Einschränkung |
| --- | --- | --- |
| CPU und Scheduler | verbessert, experimentell | Double-Speed, EI/DI, Interruptkosten, HALT-Wakeup, HALT-Bug, STOP-Ruhemodus, Joypad-Wakeup und ungültige Opcodes besitzen Regressionstests. Bus-, Stack- und Interruptzugriffe laufen an T-Zykluspositionen; Instruktionen bleiben gegenüber dem Host atomar und einige IO-Lesephasen sind noch angenähert. |
| Timer | verbessert, experimentell | 16-Bit-Divider, TAC-Flanken, verzögerter Overflow sowie TIMA-/TMA-Schreibkollisionen im Reload-Takt sind getestet; `rapid_toggle` bleibt als bekannte subzyklische Grenze offen. |
| Bildausgabe | verbessert, experimentell | Kombinierte STAT-Flanken, LCD-Abschaltung, VRAM-/OAM-/CGB-Paletten-Zugriffsfenster, Fenster-Clipping, Paletten-Wrap, DMG/CGB-Priorität und eine variable Mode-3-Dauer von 172 bis 289 Dots sind getestet; ein echter Pixel-FIFO, Mid-Scanline-Effekte und exakte Fetch-Abbrüche fehlen noch. |
| DMA | verbessert, experimentell | OAM-DMA kopiert 160 Bytes in 640 T-Zyklen und sperrt den CPU-Bus bis auf HRAM. CGB-General- und HBlank-DMA übertragen progressiv, halten die CPU 32 Dots je Block an, aktualisieren Register, pausieren HBlank-DMA bei HALT und unterstützen Abbruch; seltene Quellbus- und LCD-Umschaltkanten bleiben angenähert. |
| DMG/CGB-ROM-Laden | verbessert, experimentell | Header-, Titel-, Größen- und Truncation-Prüfung sowie stabile ROM-Identität sind vorhanden; nur legal beschaffte ROM-Dumps verwenden. |
| MBC1/MBC1M/MBC2/MBC3/MBC5 | implementiert, experimentell | Banking, RAM-Freigabe, MBC2-Nibble-RAM, MBC3-RTC mit Halt/Carry/Latch und MBC5-Rumble-Maske sind getestet. MBC1M unterstützt alternative Bankverdrahtung und konservative Header-Erkennung für 1-MiB-Multicarts; kein Durchspielnachweis für solche Sammlungen. |
| MBC4 und weitere Spezialmapper | nicht freigegeben | MMM01, MBC4, Pocket Camera, HuC1/HuC3 und weitere Spezialhardware werden mit klarer Fehlermeldung abgelehnt. |
| GBA-Backend | integriert, experimentell | `.gba`, 240×160, ARM/Thumb, Modi 0–5, Sprites, OBJ-Window, Alpha-Blending und Mosaic, PSG plus Direct Sound, remappbare L/R, Frameskip, eigene Save States/Rewind, sichere Raw-/CodeBreaker-/GameShark-v1/v2-Codes, HLE oder optionales Benutzer-BIOS sowie SRAM/Flash/EEPROM und GPIO-RTC sind angebunden. Open-Bus-Lanes, WAITCNT/Prefetch-Grenzen, WRAM-Abschaltung und EEPROM-Größenwechsel sind gehärtet; offen bleiben vor allem vollständig cycle-exaktes Timing, weitere Renderer-Kanten, Action Replay/PAR v3, der Link-UI-Host und ein echter Durchspielnachweis. |
| NAudio-Ausgabe | verbessert, experimentell | Registermasken, DIV-APU, Power-On-Phase, Frame-Sequencer, Längenzähler, Trigger, Sweep, DAC, NR50/NR51, modellabhängiger Hochpass sowie DMG/CGB-Wave-RAM-Verhalten sind getestet; seltene APU-Revisionseffekte und hörbare Langzeitvergleiche bleiben offen. |
| Control Center | implementiert, experimentell | Sieben Bereiche bündeln Live-Status, Filter, DMG-Paletten, Fenstergröße, Audiopegel und Kanäle, Eingabebelegung, Save Safety, Frameskip, Boot-ROM-Policy, Diagnose und sicheren Settings-Reset. Änderungen werden lokal persistent gespeichert. |
| Linux-Wayland-Frontend | implementiert, experimentell | Native SDL3-Ausgabe mit Portal-Öffnen, Audio, Sharp/Smooth/LCD Grid, Frameskip, Paletten, Gamepad, fünf State-Slots und Rewind. Persistente Linux-Einstellungen, freie Eingabebelegung und die erweiterten Windows-Werkzeuge fehlen noch. |
| WAV-Aufnahme | verbessert, experimentell | Schreiben und Header-Finalisierung sind synchronisiert und getestet; Datei-I/O und Stop laufen außerhalb des UI- und Emulations-Threads. Lange Aufnahmen und Gerätefehler benötigen noch breitere Praxistests. |
| Batterie-Spielstände | implementiert, experimentell | Kompatible `.sav`-Rohdaten werden atomar und spätestens alle 1.800 Frames geschrieben. Drei rotierende Backups und separate SHA-256-Wächter erkennen Truncation sowie nach dem ersten geschützten Schreibvorgang auch gleich große Verfälschungen; ältere ungeschützte `.sav`-Dateien bleiben ladbar. |
| Save States | implementiert für GB/GBC/GBA | Fünf Slots (`.ss1` bis `.ss5`) und F5/F8 sind für alle drei Systeme aktiv. Zustände sind SHA-256-geschützt und an die exakte ROM, das Hardwaremodell und – wenn verwendet – das BIOS gebunden; inkompatible Schemata werden abgelehnt. |
| Rewind | implementiert für GB/GBC/GBA | Erfasst alle vier Frames und hält höchstens 150 komprimierte Zustände (rund zehn Sekunden). Der GBA-Puffer besitzt zusätzlich ein Speicherbudget von 96 MiB; alle Puffer sind sitzungsgebunden. |
| Cheats | teilweise implementiert | GB-GameShark-RAM-Writes sowie für GBA Raw-Patches, gängige CodeBreaker-Direkt-/Logik-/Bedingungscodes und rohe oder verschlüsselte GameShark-v1/v2-RAM-Writes sind validiert. Ziele bleiben auf ausgerichtetes EWRAM/IWRAM begrenzt; Action Replay/PAR v3 und komplexe Hook-/Fill-/List-Codes fehlen. |
| Game Genie | deaktiviert | Codes werden noch nicht im ROM-Lesepfad angewendet. |
| Serial/Link-Kabel | teilweise implementiert | GB/CGB besitzen getaktete Bitübertragung und externe Clock. GBA besitzt getaktete 8-/32-Bit-Register, IRQ und eine deterministische lokale Zwei-Core-Kopplung einschließlich externem Clock-Peer. Die koordinierte zweite App-Sitzung, Multiplayerprotokolle und TCP-/IPC-Transport fehlen. |
| Debugger/Disassembler | intern/experimentell | Kein vollständiger Pause-/Step-Workflow; mehrere Grenzfälle sind ungeprüft. |

## Bauen, testen und starten

### Linux / natives Wayland

Voraussetzungen sind eine echte Wayland-Sitzung, das .NET-10-SDK und ein
funktionierender Linux-Grafiktreiber. X11 und XWayland werden vom Linux-Host
absichtlich abgelehnt.

```bash
bash scripts/build-linux.sh
bash scripts/run-linux.sh "/pfad/zu/deinem-spiel.gba"
```

Hyprland wird separat erkannt. Einstieg: [Linux User Guide (English)](docs/LINUX_USER_GUIDE.md) ·
[Linux/Wayland und Hyprland (Deutsch)](docs/LINUX_WAYLAND.md). Der
Linux-Desktopadapter ist noch nicht funktionsgleich mit dem Windows-Control-Center.

### Windows

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

Der separate, plattformneutrale GBA-Probelauf verwendet ausschließlich ein im
Code erzeugtes ARM/Thumb-Testprogramm. Er schreibt das verifizierbare Ergebnis nach
`artifacts/gba-prototype.bmp`:

```powershell
dotnet run --project ./tools/AetherBoy.GbaProbe/AetherBoy.GbaProbe.csproj -c Release
```

Ein eigener `.gba`-Pfad kann optional angegeben werden. Dieser Befehl prüft den
kleinen unabhängigen AetherBoy-Prototyp; die normale Anwendung verwendet dagegen
den breiteren vendorten GBADotnet-Kern.

### Lokale Conformance-ROMs ausführen

Die plattformneutrale CLI akzeptiert eine einzelne `.gb`-/`.gbc`-Datei, durchsucht ein Verzeichnis rekursiv oder führt ein JSON-Manifest aus. Jeder Lauf ist durch eine maximale Framezahl begrenzt; Exitcode `0` bedeutet ausschließlich bestandene Pflichtläufe, `1` mindestens einen blockierenden Fehlschlag, Timeout oder Ladefehler und `2` einen Aufruffehler.

```powershell
dotnet run --project ./tools/AetherBoy.Conformance/AetherBoy.Conformance.csproj -c Release -- `
  ./pfad/zur/legalen-testsuite --max-frames 600 --json ./artifacts/conformance.json

dotnet run --project ./tools/AetherBoy.Conformance/AetherBoy.Conformance.csproj -c Release -- `
  --manifest ./tools/AetherBoy.Conformance/compatibility.example.json `
  --json ./artifacts/compatibility.json
```

Die CLI bringt bewusst keine Test-ROMs mit, verändert die Eingaben nicht und legt temporäre Spielstände außerhalb der Suite ab. Verwende auch hier nur Test-ROMs, die du rechtmäßig beziehen und ausführen darfst.

## ROMs, Boot-ROMs und Spielstände

Dieses Projekt erteilt **keine** Rechte an kommerziellen Spielen, Nintendo-Firmware, Boot-ROMs, Marken, Grafiken oder sonstigen Drittinhalten. Verwende nur ROM- und Firmware-Dumps, die du nach dem für dich geltenden Recht selbst verwenden darfst. Verbreite keine ROMs oder Boot-ROMs zusammen mit Quellcode oder Builds. Diese Hinweise sind keine Rechtsberatung.

Historische `.gb`-/`.gbc`-Dateien und ein persönlicher `.sav` wurden in Phase 0 aus dem veröffentlichbaren Quellbaum entfernt. Die lokale Arbeitskopie bewahrt sie ausschließlich im ignorierten Verzeichnis `.local-assets/roms` auf. Sie sind **nicht** von der GPL des Emulatorcodes umfasst und dürfen nicht zum Repository oder zu einem Release hinzugefügt werden.

Boot-ROM-Dateien wie `dmg_boot.bin`, `gbc_boot.bin` oder das exakt 16 KiB große
`gba_bios.bin` sind zum Bauen nicht erforderlich und müssen – sofern ihre Nutzung
legal ist – vom Benutzer selbst bereitgestellt werden. Ohne GBA-BIOS verwendet
der GBA-Pfad seinen getesteten Startzustand und einen eingebauten HLE-Fallback
für Reset, Wait/Halt, Mathematik, Speichertransfers, affine Matrizen sowie
BitPack-, LZ77-, Huffman-, RLE- und Differential-Dekompression. Firmware wird
nicht ausgeliefert; mit einer echten 16-KiB-Datei bleibt der normale Vektorpfad aktiv.

Save States werden neben der geladenen ROM als `.ss1` bis `.ss5` abgelegt. Sie
enthalten keine ROM- oder Boot-ROM-Daten, sondern deren Identitätsbindung; ein
Zustand lässt sich deshalb nicht versehentlich in eine andere ROM- oder
Firmware-Sitzung laden. GBA-Zustände verwenden einen eigenen versionierten,
Brotli-komprimierten Vertrag und niemals das DMG/CGB-Layout.

Batterie-RAM bleibt als mit anderen Emulatoren kompatible `.sav`-Rohdatei neben der ROM liegen. AetherBoy ergänzt `.sav.bak1` bis `.sav.bak3` sowie kleine `.guard`-Integritätsdateien. Das Save Safety Center ist im Hauptfenster und unter `SYSTEM → Save States` erreichbar; eine Wiederherstellung bewahrt den zuvor aktiven Stand erneut als Backup.

## Repository-Hygiene

Die verbindlichen Abhängigkeiten stehen in den Projektdateien, ihre Auflösung in den Lockfiles. Buildausgaben, IDE-Zustand, lokale SDK-Werkzeuge, ROMs, Boot-ROMs, Save-Dateien, Logs und Symbole sind ausgeschlossen. Der eingeschränkte NuGet-Feed, Vulnerability-Audit, gepinnte GitHub Actions, Linux-Portabilitätsgate und automatische Abhängigkeitsupdates sichern diese Grenzen ab.

## Lizenz und Herkunft

Der Emulatorcode wird als **GNU General Public License Version 3** dokumentiert; siehe [LICENSE](LICENSE). Mangels einer ausdrücklichen „or later“-Erklärung wird konservativ `GPL-3.0-only` verwendet. Copyright und Urheberschaft verbleiben bei den jeweiligen ursprünglichen Autoren und späteren Beitragenden.

Die Projektchronik nennt Frédéric Meyer als ursprünglichen Entwickler (2014) und dokumentiert spätere ChiiBoy-/AetherBoy-Modifikationen. Drittanbieterkomponenten besitzen eigene Lizenzen; direkte Pakete und Hinweise stehen in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
