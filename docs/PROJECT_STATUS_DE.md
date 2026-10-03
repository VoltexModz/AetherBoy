# AetherBoy – Projektstand und Herkunft

Stand: 11. September 2026
Version: 4.8.0-alpha.1  
Branch: `development`

## Kurzfassung

AetherBoy ist heute eine moderne Windows-Anwendung und besitzt zusätzlich einen
ersten nativen Linux-Host für Wayland und Hyprland. Emuliert werden Game Boy,
Game Boy Color und experimentell Game Boy Advance. GB und GBC beruhen auf dem historischen
NanoBoy-/ChiiBoy-Code, wurden aber in Architektur, Hardwaremodell, Oberfläche,
Speicherung, Eingabe und Tests stark überarbeitet. Für GBA existieren zwei
Pfade: ein kleiner eigener Lern- und Regressionkern sowie ein produktiver,
direkt im Repository gepflegter Fork des MIT-lizenzierten GBADotnet-Kerns.

Der aktuelle Stand baut unter .NET 10 ohne Warnungen. Die zuvor dokumentierte
Windows-Abnahme umfasst mindestens 354 bestandene deterministische Tests; der
Linux-Ausbau wurde separat geprüft (siehe unten). Das beweist viele definierte Hardware- und
Anwendungsfälle, aber noch keinen vollständigen kommerziellen GBA-Durchspieltest.

## Herkunft und verwendete Quellen

| Bestandteil | Herkunft | Art der Verwendung | Lizenz/Hinweis |
| --- | --- | --- | --- |
| Historischer GB/GBC-Kern und WinForms-Anwendung | NanoBoy von Frédéric Meyer, später ChiiBoy Color | Ausgangsbasis; anschließend umfangreich umgebaut und erweitert | GPL-3.0-only; Copyright- und Lizenzhinweise bleiben erhalten |
| Produktname, Marke und neue Oberfläche | AetherBoy-Projekt | Eigenes Design und eigene Implementierung | Bestandteil des GPL-Projekts |
| Produktiver GBA-Kern | DaveTCode/GBADotnet, Commit `994c4b225c6e4277ada8d37bb9283f53827ee3e1` vom 16.05.2022 | MIT-Quellstand direkt aufgenommen und als AetherBoy-Fork weiterentwickelt | Vollständige MIT-Lizenz unter `third_party/GBADotnet.Core/LICENSE.md` |
| mGBA | vom Projektinhaber bereitgestelltes Quellarchiv | Architektur- und Verhaltensreferenz; kein mGBA-Kern eingebunden und keine mechanische Portierung | MPL-2.0; Details in `docs/MGBA_REVIEW.md` |
| GBA-Hardwareverhalten | öffentliche Hardwaredokumentation und dokumentierte BIOS-Verträge | Zur Prüfung unserer C#-Implementierungen und Tests verwendet | Keine Nintendo-BIOS-Datei enthalten |
| Audioausgabe | NAudio.WinMM 2.3.0 | Windows-Adapter außerhalb der Emulator-Cores | MIT |
| Linux-Desktop | SDL3-CS und SDL3-CS.Linux 3.4.16 | Nativer Wayland-Adapter; kein X11/XWayland-Fallback | Zlib |
| Testplattform | MSTest.Sdk 4.3.2 | Automatisierte Core-, Runtime- und Windows-Tests | MIT |

Die genaue Drittanbieterzuordnung steht in `THIRD_PARTY_NOTICES.md`. ROMs,
Nintendo-Firmware, fremde Spielgrafiken und Diagnosearchive mit Spieldaten sind
nicht Teil des Repositorys.

## Was wir grundlegend modernisiert haben

### Architektur und Laufzeit

- Umstellung auf .NET 10 mit reproduzierbarem Restore und gesperrten
  Abhängigkeitsversionen.
- Trennung in plattformneutralen Core, Runtime-Vertrag, Windows-Frontend und
  nativen Linux-Wayland-Host.
- Exklusiver Emulations-Owner-Thread mit typisierten Befehlen statt direkter
  UI-Zugriffe auf veränderlichen Kernzustand.
- Unveränderliche Snapshots und synchronisierter Frame-/Audiotransport zwischen
  Emulations- und UI-Thread.
- Dynamische Bildgeometrie für 160×144 (GB/GBC) und 240×160 (GBA).
- Windows- und Linux-CI-Gates mit einer festen Mindesttestzahl sowie Build und
  Hyprland-Profilerkennung des nativen Wayland-Hosts.

### Oberfläche und Bedienung

- Eigenständige Aether-Wave-Marke, Multi-Resolution-App-Icon und rahmenlose
  Fenster-Chrome anstelle der alten Standardoberfläche.
- Moderne Display-Bühne, Cartridge Vault, Drag-and-drop, Recent Files,
  Command Deck und Live-Sessionstatus.
- Zentrales Control Center für Bild, Audio, Eingabe, Saves, System und lokale
  Diagnose.
- Eigene Aether-Dialoge und modernisierte Werkzeuge für Cheats, Audio,
  Steuerung, Save Safety und Programminformationen.
- Sharp-, Smooth- und LCD-Grid-Ausgabe mit korrektem Seitenverhältnis.
- Windows Gaming Input plus XInput-Fallback, Hot-Plug und frei speicherbare
  Tastatur-/Controllerbelegung einschließlich GBA-L/R.
- Eigener SDL3-Wayland-Host mit Aether-Oberfläche, ROM-Drag-and-drop,
  Tastatur/Gamepad-Hot-Plug, Vollbild und Hyprland-spezifischem Desktopprofil.

### Spielstände und Zustände

- Atomare Batterie-Saves mit Write-Through, SHA-256-Integritätswächtern und
  drei rotierenden Backups.
- Automatische Wiederherstellung aus der jüngsten gültigen Generation.
- Save Safety Center zur lokalen Prüfung und kontrollierten Rücksicherung.
- Fünf Save-State-Slots, Quick Save/Load und Rewind für GB, GBC und GBA.
- Zustände sind versioniert, integritätsgeschützt und an exakte ROM,
  Hardwaremodell sowie gegebenenfalls BIOS gebunden.
- GBA-SRAM, Flash64, Flash128, EEPROM und GPIO-RTC sind persistent angebunden.

## Verbesserungen am GB/GBC-Kern

- CPU-, Interrupt-, HALT-/STOP-, EI/DI-, Stack- und Busverhalten mit
  Regressionstests gehärtet.
- Timerflanken, verzögerter TIMA-Reload und Schreibkollisionen präzisiert.
- PPU-Timing, STAT-Flanken, VRAM/OAM-Zugriffsfenster, Fenster-Clipping,
  Spriteauswahl und DMG/CGB-Prioritäten erweitert.
- OAM-DMA sowie CGB-General-/HBlank-DMA zeitlich und zustandsseitig integriert.
- APU-Frame-Sequencer, Sweep, Envelope, Length, Wave-RAM-Verhalten,
  Hochpassfilter und Stereo-Routing verbessert.
- MBC1, MBC1M, MBC2, MBC3 samt RTC und MBC5 samt Rumble-Maske getestet.
- Serielle Bitübertragung mit normaler, CGB-Fast- und externer Clock in den
  deterministischen Zustand aufgenommen.

Die ausgewählte externe GB/GBC-Konformitätsmatrix erreicht 67 von 70 Läufen;
die drei verbleibenden Abweichungen sind informativ dokumentiert und werden
nicht als bestandene Läufe dargestellt.

## Aufbau und Weiterentwicklung des GBA-Pfads

### Eigener Prototyp

Unter `nanoboy/Core/Advance` liegt ein unabhängig geschriebener kleiner
ARM-/Thumb-, Bus- und Mode-3-Pfad. Er ist bewusst kein vollständiger
Spielekern. Das Werkzeug `AetherBoy.GbaProbe` führt ein selbst erzeugtes
Testprogramm aus und erzeugt daraus ein reproduzierbares 240×160-Bild.

### Produktiver Kern

Der GBADotnet-Quellstand wurde nicht als unveränderte Blackbox oder externe DLL
angebunden. Er liegt lesbar unter `third_party/GBADotnet.Core`, baut ohne die
alte Generator-/Logging-Infrastruktur und wird innerhalb des Projekts gepflegt.
Auf dieser Basis wurden unter anderem ergänzt oder korrigiert:

- vollständige Cartridge-Headerfelder und allokationsarme Save-Marker-Erkennung;
- Flash-Befehle und 128-KiB-Banking;
- EEPROM-Bitreihenfolge, Fenster, Dummybits und dynamische Größe;
- ARM-/Thumb-Undefined-Exceptions, SBC-Borrow und DMA-Adressmaskierung;
- PPU-Mosaic, OBJ-Window, halbtransparente Sprites, Bitmap-VRAM-Kanten und
  deckender Framebuffer-Alpha-Kanal;
- WAITCNT, Prefetch-Grenzen, Open-Bus-Lanes und internes WRAM-Control;
- vier PSG-Kanäle plus Direct Sound mit Host-Kanalsteuerung;
- GPIO-RTC einschließlich Save States und geschützter `.sav.rtc`-Datei;
- getaktete 8-/32-Bit-Serialregister, IRQ und Wiederaufnahme laufender Transfers;
- eigener HLE-BIOS-Fallback für Reset/Wait/Halt, Mathematik, Speichertransfer,
  affine Berechnungen und dokumentierte Dekompressionsdienste;
- vollständiger, komprimierter und ROM-/BIOS-gebundener GBA-Zustand sowie
  begrenztes Rewind;
- sichere Raw-Patches, gängige CodeBreaker-Direkt-/Logik-/Bedingungscodes und
  rohe oder verschlüsselte GameShark-v1/v2-RAM-Writes;
- deterministische lokale Zwei-Core-Kopplung für 8-/32-Bit-Serial einschließlich
  externem Clock-Peer;
- begrenzte lokale Kerndiagnostik für BIOS-Aufrufe, unbekannte Opcodes,
  nicht zugeordnete E/A-Zugriffe und Link-Matches ohne ROM-Bytes, Schreibwerte
  oder Telemetrie.

## Aktuell nutzbarer Funktionsumfang

| Bereich | GB/GBC | GBA |
| --- | --- | --- |
| ROM-Öffnen, Vault, Drag-and-drop | Ja | Ja |
| Native Bildgröße und Filter | Ja | Ja |
| Tastatur und Gamepad | Ja | Ja, einschließlich L/R |
| Audioausgabe und vier Kanalschalter | Ja | Ja, PSG plus Direct Sound |
| Batterie-Saves mit Backups | Ja | Ja |
| Save States und Rewind | Ja | Ja |
| [Sitzungs-Cheats](CHEAT_SUPPORT.md) | GameShark `01`, Game Genie (6/9), CodeBreaker, Raw; mehrzeilige Sets | CodeBreaker, GameShark v1/v2, Action Replay v3: Master/Reseed, Bedingungen, Fills, Hooks, Zeiger, ROM-Patches, Gerätetaste; dokumentierte Ausnahmen |
| Diagnose im Control Center | Ja, Hoststatus | Ja, zusätzlich Kernevents |
| Lokale Link-Hardwarebasis | Serielle Clock vorhanden | Zwei-Core-Peer vorhanden |

## Verifikation

- Release-Build: 0 Warnungen, 0 Fehler.
- 181 Core-Tests.
- 127 Runtime-Tests.
- 65/65 Desktop-Tests unter CachyOS/Hyprland, einschließlich nativem UI,
  synthetischen GB/GBC/GBA-Spielen und virtuellen SDL-Controllern.
- Ubuntu 24.04 x64 mit eigenem Weston-Compositor im Container: 54 bestanden,
  drei Audio-Playtests bewusst übersprungen, keine Fehler.
- Unabhängige Bewertung: UI 8,2/10, Features 7,9/10. Details und Grenzen:
  [Linux-Kritik](LINUX_CRITIQUE.md), [Playtest](LINUX_PLAYTEST.md).
- 42 Windows-Smoke-Tests einschließlich zentraler Datenablage, Migration, Datenschutz und Live-Export der Entwicklungsdiagnose.
- Zuvor dokumentierter Windows-Gesamtlauf (vor diesem Linux-Ausbau): 355 Tests, 354 bestanden, 0 fehlgeschlagen und 1 übersprungen.
- Nativer Ubuntu-Build und `linux-x64`-Publish: 0 Warnungen, 0 Fehler; der
  veröffentlichte Host erkannte Hyprland und öffnete unter WSLg einen echten
  Wayland-Fensterlauf.
- Selbstenthaltener Windows-x64-Build wurde real gestartet und blieb im
  automatischen Start-Smoke stabil.
- Das Build-Paket wurde auf ROM-, BIOS- und Save-Dateien geprüft: 0 Treffer.

Alle GBA-Regressionen verwenden selbst erzeugte Programme oder synthetische
Daten. Das Repository benötigt keine kommerzielle ROM und keine Nintendo-
Firmware für die automatischen Tests.

## Ehrliche Grenzen

- GBA bleibt experimentell, bis echte Spiele Boot, Grafik, Audio, Kämpfe,
  Kartenwechsel, Ingame-Speicherung und längere Sitzungen bestanden haben.
- Der produktive GBA-Kern ist noch nicht vollständig cycle-exakt; seltene
  Open-Bus-, Prefetch-, DMA-, PPU- und APU-Kanten können abweichen.
- Die lokale GBA-Linkbasis besitzt noch keinen fertigen Zwei-Sitzungs-Host,
  keinen sichtbaren Link-Lab-Ablauf und keinen TCP-/IPC-/Internettransport.
- Action Replay v3 unterstützt nur direkte RAM-Schreib- und Additionscodes;
  Master-/Reseed-/Hook-/Bedingungs-/Indirekt-/ROM-Patch-Befehle sowie komplexe
  CodeBreaker-Hook-/Fill-/List- und verschlüsselte Mastercode-Streams fehlen.
- Spezialhardware wie Pocket Camera, HuC1/HuC3, MMM01 und weitere seltene
  Mapper ist nicht freigegeben.
- Ältere experimentelle GBA-Save-States vor Kernschema 5 sind nicht kompatibel.
- Die zentrale AppData-Bibliothek und die automatische Development-Aufzeichnung
  gelten derzeit für Windows. Linux besitzt inzwischen ebenfalls zentrale XDG-Spielstände,
  lokale Diagnose und zusätzliche Werkzeuge; Details zum Frontend stehen in `docs/LINUX_WAYLAND.md`.
- AetherBoy ist Alpha-Software und noch kein versprochener Ersatz für etablierte
  Referenzemulatoren.

## Bauen und testen

```powershell
dotnet restore ./nanoboy.sln --locked-mode --configfile ./NuGet.config
dotnet build ./nanoboy.sln -c Release --no-restore
dotnet test --solution ./nanoboy.sln -c Release --no-build --no-restore --minimum-expected-tests 354
dotnet run --project ./nanoboy/nanoboy.csproj -c Release --no-build
```

Für ein selbstenthaltenes Windows-x64-Paket:

```powershell
dotnet publish ./nanoboy/nanoboy.csproj -c Release -r win-x64 --self-contained true
```

Der native Wayland-Build unter Linux:

```bash
bash scripts/build-linux.sh
bash scripts/run-linux.sh "/pfad/zu/deinem-spiel.gba"
```

## Nächster sinnvoller Meilenstein

Phase 9 sollte die reale Spielbarkeit qualifizieren: ein rechtmäßig vorhandenes
GBA-Spiel von Boot über Karte, Menü und Kampf bis Ingame-Save, Neustart,
Save-State, Rewind, Audio und mindestens 30–60 Minuten Laufzeit testen. Jeder
gefundene Fehler wird zuerst reproduzierbar gemacht und danach mit einem
synthetischen Regressionstest im Kern abgesichert.

Dieses Dokument ist eine technische Herkunfts- und Fortschrittsübersicht, keine
Rechtsberatung. Nutzer müssen selbst sicherstellen, dass sie ROMs und Firmware
in ihrer Rechtsordnung verwenden dürfen.
