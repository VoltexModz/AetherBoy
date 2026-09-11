<p align="center">
  <img src="branding/exports/aetherboy-mark-256.png" alt="AetherBoy Logo" width="112" height="112">
</p>

<h1 align="center">AetherBoy</h1>

<p align="center">
  <a href="README.md" lang="en">English</a> · <strong lang="de">Deutsch</strong>
</p>

<p align="center">
  <strong>Drei Handhelds. Eine Oberfläche.</strong><br>
  Game Boy · Game Boy Color · Game Boy Advance<br>
  Ein Emulator in C# für Windows und natives Linux / Wayland.
</p>

<p align="center">
  <a href="CHANGELOG.md"><img src="https://img.shields.io/badge/Version-4.8.0--alpha.1-8B38FF?style=flat-square" alt="Version 4.8.0-alpha.1"></a>
  <a href=".github/workflows/ci.yml"><img src="https://github.com/VoltexModz/AetherBoy/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="global.json"><img src="https://img.shields.io/badge/.NET-10-512BD4?style=flat-square" alt=".NET 10"></a>
  <a href="docs/LINUX_WAYLAND.md"><img src="https://img.shields.io/badge/Linux-Wayland-29E2ED?style=flat-square" alt="Linux mit nativem Wayland"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/Lizenz-GPL--3.0--only-555555?style=flat-square" alt="Lizenz GPL-3.0-only"></a>
</p>

<p align="center">
  <a href="#linux-build">Linux-Build</a> ·
  <a href="#windows-build">Windows-Build</a> ·
  <a href="#steuerung-unter-linux">Steuerung</a> ·
  <a href="COMPATIBILITY.md">Kompatibilität</a> ·
  <a href="#dokumentation">Dokumentation</a> ·
  <a href="https://github.com/VoltexModz/AetherBoy/issues">Issues</a>
</p>

> [!IMPORTANT]
> **AetherBoy ist eine Alpha-Version.** GB und GBC besitzen breite automatisierte Testabdeckung. GBA und das Linux-Frontend sind experimentell und benötigen weitere Spiel-, Audio- und Langzeittests. Ein erfolgreicher ROM-Start ist noch kein Nachweis vollständiger Spielbarkeit.

## Ein Blick auf AetherBoy

Die **Aether-Wave-Oberfläche** verbindet das Spielbild mit einer Live-Sessionleiste, direkten Aktionen und einem zentralen Control Center. Violett, Cyan und dunkle Flächen prägen das gemeinsame Design unter Windows und Linux.

<p align="center">
  <img src="docs/images/aetherboy-linux.png" alt="AetherBoy unter Linux: Hauptfenster mit ROM-Auswahl, Sessionleiste, fünf Save-Slots und direkten Spielaktionen" width="1000">
  <br>
  <sub>Echte Aufnahme des nativen Linux-Frontends, ohne geladene ROM.</sub>
</p>

<details>
<summary><strong>Control Center ansehen</strong></summary>

<p align="center">
  <img src="docs/images/aetherboy-control-center.png" alt="Aether Control Center unter Linux: Display-Einstellungen mit Sharp, Smooth, LCD Grid, Frameskip und DMG-Paletten" width="1000">
</p>

Sieben Bereiche bündeln Übersicht, Display, Audio, Eingabe, Spielstände, System und Diagnose. Die Aufnahme zeigt die Display-Einstellungen des Linux-Clients.

</details>

## Was AetherBoy mitbringt

| Bereich | Funktionen |
| --- | --- |
| **Drei Systeme** | `.gb`, `.gbc` und experimentell `.gba` im selben Frontend; GBA mit optionalem BIOS und eingebautem HLE-Fallback. |
| **Bild und Audio** | Sharp, Smooth und LCD Grid, DMG-Paletten, Frameskip und Audioausgabe; GBA mit PSG und Direct Sound. |
| **Spielsteuerung** | Tastatur und Gamepad, Pause, Turbo, Vollbild und ROM-Drag-and-drop. |
| **Speichern und Zurückspulen** | Batterie-Spielstände, fünf Save-State-Slots und Rewind für GB, GBC und GBA. |
| **Save-Schutz** | Atomare `.sav`-Schreibvorgänge, Integritätsprüfung und drei rotierende Backups. |
| **Lokale Einstellungen** | Persistente Display-, Audio- und Eingabeoptionen im Control Center. |

### Windows und Linux im Vergleich

Beide Frontends verwenden denselben plattformneutralen Core und dieselbe Runtime. Die verfügbaren Desktop-Werkzeuge unterscheiden sich noch:

| | Windows | Linux |
| --- | --- | --- |
| **Frontend** | Windows Forms | SDL3, natives Wayland |
| **Aether-Oberfläche und Control Center** | Vorhanden | Vorhanden, an Kachel- und Breitbildfenster angepasst |
| **ROM öffnen** | Dateidialog und Drag-and-drop | XDG Desktop Portal, Dateipfad und Drag-and-drop |
| **Tastatur / Gamepad** | Beide, mit Belegungseinstellungen | Beide; Tastatur frei belegbar, Gamepad mit Standardbelegung |
| **Batterie-Saves, fünf State-Slots, Rewind** | Vorhanden | Vorhanden |
| **Cartridge Vault, Cheat-Verwaltung, Save Safety Center** | Vorhanden, teils experimentell | Noch nicht als vollständige Werkzeuge verfügbar |
| **WAV-Aufnahme und Boot-ROM-Auswahl** | Vorhanden | Noch nicht in der Oberfläche verfügbar |

Die genaue Zuordnung steht in [Windows → Linux: UI-Stand](docs/LINUX_UI_PARITY.md).

## Linux-Build

**Neu: ein eigener nativer Linux-Client mit SDL3 und Wayland**, einschließlich Hyprland-Profil, Audio, Gamepads, Control Center und Installation ins App-Menü. Die Build-Skripte erkennen **x86-64** und **ARM64** automatisch.

### Voraussetzungen

- Linux mit einer **nativen Wayland-Sitzung** und funktionierendem Grafiktreiber.
- **.NET SDK 10.0.302** oder ein neuerer Patch derselben `10.0.3xx`-Feature-Band, entsprechend [`global.json`](global.json).
- Die üblichen .NET-Systembibliotheken einschließlich ICU sowie PipeWire-, PulseAudio- oder ALSA-Clientbibliotheken für Audio.
- Ein passendes **XDG Desktop Portal** für den Dateidialog. Unter Hyprland: `xdg-desktop-portal-hyprland` plus GTK- oder KDE-Portal für die Dateiauswahl.

SDL3, Logo und Schriften werden über das Projekt mitgeliefert. Der Linux-Client setzt natives Wayland voraus; **X11 und XWayland werden nicht unterstützt**. Hyprland, KDE Plasma und GNOME werden separat erkannt.

### Bauen und starten

Der aktuelle Entwicklungsstand liegt auf dem Branch `development`:

```bash
git clone --branch development https://github.com/VoltexModz/AetherBoy.git
cd AetherBoy
bash scripts/build-linux.sh
bash scripts/run-linux.sh
```

Danach eine `.gb`-, `.gbc`- oder `.gba`-Datei über **OPEN ROM**, die Taste **O** oder Drag-and-drop öffnen. Archive vorher entpacken. Eine ROM lässt sich auch direkt übergeben:

```bash
bash scripts/run-linux.sh "/pfad/zu/deinem-spiel.gba"
```

| Architektur | Build-Ausgabe |
| --- | --- |
| x86-64 | `artifacts/AetherBoy-linux-x64/` |
| ARM64 | `artifacts/AetherBoy-linux-arm64/` |

Der Build benötigt auch beim Ausführen eine installierte **.NET-10-Runtime**; sie wird nicht in die Ausgabe eingebettet. Das SDK bringt die Runtime bereits mit. `run-linux.sh` baut automatisch neu, wenn der Build fehlt oder älter als die Quelldateien ist.

### Im App-Menü installieren

Nach dem Build kann AetherBoy ohne Root-Rechte für den aktuellen Benutzer installiert werden:

```bash
bash scripts/install-linux-user.sh
aetherboy "/pfad/zu/deinem-spiel.gbc"
```

Die Programmdateien liegen standardmäßig unter `~/.local/share/aetherboy`, der Starter unter `~/.local/bin/aetherboy`. Das Skript installiert außerdem den Desktop-Eintrag und die Icons. Für den Terminalaufruf muss `~/.local/bin` im `PATH` liegen. Bei einem ausschließlich benutzerlokal installierten .NET-SDK muss dessen Verzeichnis über `DOTNET_ROOT` auch für den installierten Starter erreichbar sein.

<details>
<summary><strong>Wayland prüfen und Startprobleme eingrenzen</strong></summary>

```bash
echo "$XDG_SESSION_TYPE"
echo "$WAYLAND_DISPLAY"
dotnet --version
```

`WAYLAND_DISPLAY` muss gesetzt sein; eine als `x11` gemeldete Sitzung wird abgelehnt.

Desktop-Erkennung ohne Fenster prüfen:

```bash
dotnet run --project frontends/AetherBoy.Desktop/AetherBoy.Desktop.csproj -- --platform-info
```

Wayland und das Standard-Audiogerät gemeinsam prüfen:

```bash
bash scripts/run-linux.sh --audio-info
```

Falls unter Hyprland kein Dateidialog erscheint, die [Portal-Konfiguration](docs/LINUX_WAYLAND.md#hyprland-profil) prüfen. Weitere Hilfe gibt es im [Linux User Guide](docs/LINUX_USER_GUIDE.md#7-troubleshooting).

</details>

**Weiterlesen:** [Linux / Wayland / Hyprland (Deutsch)](docs/LINUX_WAYLAND.md) · [Linux User Guide (English)](docs/LINUX_USER_GUIDE.md)

## Windows-Build

Benötigt werden Windows und dasselbe **.NET SDK 10.0.302** beziehungsweise ein neuerer Patch der `10.0.3xx`-Feature-Band. Für Audio muss ein funktionierendes Windows-WinMM-Gerät verfügbar sein.

Im Repository-Verzeichnis mit PowerShell ausführen:

```powershell
dotnet restore ./nanoboy.sln --locked-mode --configfile ./NuGet.config
dotnet build ./nanoboy.sln -c Release --no-restore
dotnet run --project ./nanoboy/nanoboy.csproj -c Release --no-build
```

### Windows: lokale Daten und Entwicklungsdiagnose

Die normale Anwendung ist standardmäßig ein Development-Build, auch bei
`-c Release`. Sie zeichnet lokale Sitzungsberichte automatisch auf. Es gibt
keine separate Tester-Anwendung und keinen erforderlichen Startschalter.
Der Buildkanal steht in der EXE; Git wird auf dem Rechner des Spielers nicht benötigt.
Für spätere stabile Veröffentlichungen kann beim Bauen/Publishen
`-p:AetherBoyChannel=stable` gesetzt werden; dann ist die Sitzungsaufzeichnung
standardmäßig aus. `--tester-mode` bleibt als optionaler Diagnoseschalter kompatibel.

Alle verwalteten Windows-Daten liegen unter `%LOCALAPPDATA%\AetherBoy`:

| Unterordner | Inhalt |
| --- | --- |
| `Roms/<SHA-256>/` | Lokale Kopie jeder geöffneten ROM, mit lesbarem Dateinamen |
| `Saves/<SHA-256>/` | `game.sav`, RTC, Integritätsdateien und rotierende Backups |
| `States/<SHA-256>/` | `game.ss1` bis `game.ss5` |
| `Settings/` | `settings.json`, letzte lesbare Sicherung und ROM-Verlauf |
| `Firmware/` | Optional selbst bereitgestellte Boot-ROMs/BIOS |
| `Recordings/` | Standardziel für manuell gespeicherte WAV-Aufnahmen |
| `development/Sessions/` | Diagnoseberichte pro Programmstart |
| `development/Crashes/` | Crashlogs der Development-Builds |

Die ROM-Bibliothek bietet **ROM-Ordner öffnen**, das **Control Center → Ordner**
zusätzlich Zugriff auf die übrigen Daten. Beim Öffnen einer externen ROM wird sie
kopiert; das Original bleibt erhalten. Gleiche Inhalte werden wiederverwendet,
unterschiedliche ROM-Hacks erhalten getrennte Saves. Auch ältere importierte
Spiele bleiben in der Bibliothek auffindbar, unabhängig vom begrenzten Verlauf.

Beim ersten Import werden vorhandene ROM-nahe `.sav`-, RTC-, Backup-, Guard- und
`.ss1`–`.ss5`-Dateien mit übernommen. Bereits vorhandene zentrale Save-/State-Ordner
haben Vorrang; ein erneuter Import überschreibt sie nicht. Alte Dateien werden
nicht gelöscht. Einstellungen werden beim ersten Zugriff aus dem bisherigen
WinForms-Speicherort übernommen. Beschädigte zentrale Einstellungen können aus
der letzten lesbaren Sicherung geladen werden. Stabile Builds speichern Crashlogs
unter `Crashes/`; vorhandene ältere `Logs/` und `TesterSessions/` bleiben erhalten.

Diagnoseberichte enthalten Buildidentität, ROM-Header/Hash, Frame-Fortschritt,
Controllerwechsel und Save-State-/Rewind-Ergebnisse. Sie enthalten keine ROM-Dateien,
ROM-Pfade oder Save-Inhalte. Es gibt keinen Upload. Unter **Diagnostics** kann der
aktive Bericht manuell als ZIP exportiert werden. Ein fortschreitender Framezähler
beweist noch keine korrekte Spielgrafik und ersetzt keinen Spieltest.

Für die Weitergabe per USB den vollständigen Publish-Ordner kopieren. Die EXE
und ihre Abhängigkeiten gehören zusammen; persönliche Spieldaten bleiben auf
dem jeweiligen Rechner. Der weitere Ausbau steht im [Windows-Plan](docs/WINDOWS_ROADMAP.md).

Die historischen Datei- und Ordnernamen `nanoboy` bleiben im Quellbaum erhalten; das Produkt heißt **AetherBoy**.

## Steuerung unter Linux

Die wichtigsten Standardbelegungen; Spieltasten lassen sich unter **Control Center → Input** ändern.

| Aktion | Tastatur | Gamepad |
| --- | --- | --- |
| Steuerkreuz | Pfeiltasten | D-Pad oder linker Stick |
| A / B | Z / X auf US-Layouts, **Y / X auf deutschen Layouts** | Untere / rechte Aktionstaste |
| Start / Select | Enter / Rücktaste | Start / Back |
| GBA L / R | Q / E | Linke / rechte Schultertaste |
| ROM öffnen / Control Center | O / C | — |
| Pause / Turbo halten | Leertaste / Tab | — |
| Save-State-Slot auswählen | 1–5 | — |
| Schnellspeichern / Schnellladen | F5 / F8 | — |
| Einen Rewind-Schritt zurück | F7 | — |
| Vollbild | F11 | — |
| Einstellungen schließen / Vollbild verlassen | Escape | — |

Die A/B-Vorgaben beziehen sich auf die physischen Tastenpositionen. AetherBoy zeigt die Belegung passend zum aktuellen Tastaturlayout an. Alle Shortcuts und die Tastaturnavigation stehen im [Linux User Guide](docs/LINUX_USER_GUIDE.md#5-controls).

## Spielstände und BIOS

- **Batterie-Spielstände:** Unter Windows zentral in `AetherBoy\Saves`; unter Linux weiterhin neben der ROM. `.bak1` bis `.bak3` und `.guard`-Dateien schützen die Spielstände. Nur das jeweilige Save-Verzeichnis muss beschreibbar sein.
- **Save States:** fünf Slots von `.ss1` bis `.ss5`, gebunden an die exakte ROM, das Hardwaremodell und gegebenenfalls das BIOS. Inkompatible Zustandsversionen werden abgelehnt; eine automatische Migration älterer Schemata ist noch nicht vorhanden.
- **Rewind:** ein sitzungsgebundener Puffer mit bis zu etwa zehn Sekunden Historie; für GBA zusätzlich durch ein Speicherbudget begrenzt.
- **Linux-Einstellungen:** unter `$XDG_CONFIG_HOME/aetherboy/settings.json`, normalerweise `~/.config/aetherboy/settings.json`.
- **GBA-BIOS:** ohne eigene Firmware greift der eingebaute HLE-Fallback. Der Kern unterstützt optional ein exakt 16 KiB großes `gba_bios.bin`; die Linux-Oberfläche bietet noch keine BIOS-Auswahl.

ROMs und Boot-ROMs werden nicht mitgeliefert und sind zum Bauen nicht erforderlich.

## Kompatibilität und offene Arbeit

**GB / GBC:** MBC1, MBC1M, MBC2, MBC3 mit RTC und MBC5 sind implementiert. Die ausgewählten Blargg-Soundsuiten bestehen laut dokumentierter Matrix auf DMG und CGB jeweils 12/12 Tests. Das ersetzt keinen Durchspieltest und ist keine pauschale Kompatibilitätsquote. Spezialmapper wie MMM01, MBC4, Pocket Camera und HuC1/HuC3 werden abgelehnt; exakte Pixel-FIFO- und einzelne Timing-Effekte bleiben offen.

**GBA:** Ein vendorter, MIT-lizenzierter [GBADotnet-Kern](third_party/GBADotnet.Core/README.md) ist an Bild, Eingabe, Audio, SRAM/Flash/EEPROM/RTC, Save States und Rewind angebunden. Vollständig zyklusgenaues Timing, weitere Renderer-Grenzfälle und breitere Praxistests stehen noch aus.

**Weitere Grenzen:** Game Genie ist deaktiviert. Cheats sind nur teilweise unterstützt; Action Replay/PAR v3 fehlt. Serial-/Link-Grundlagen sind vorhanden, ein vollständiger Link- oder Netzwerk-Multiplayer-Workflow jedoch noch nicht. Der Debugger bleibt experimentell.

Details und reproduzierbare Ergebnisse: [Kompatibilitätsmatrix](COMPATIBILITY.md) · [GBA-Status](GBA.md) · [Projektstatus](docs/PROJECT_STATUS_DE.md).

## Entwicklung und Tests

Die Lösung trennt **Core**, **Runtime** und **Desktop-Frontends**. Der Emulationszustand gehört einem dedizierten Owner-Thread; die Oberflächen kommunizieren über typisierte Befehle und unveränderliche Snapshots. NuGet-Lockfiles und das gepinnte SDK halten den Build reproduzierbar.

Die [GitHub-Actions-CI](.github/workflows/ci.yml) baut und testet die Lösung auf Windows sowie Core, Runtime und den nativen Desktop-Host auf Linux. Pushes auf `main` und `development` werden geprüft; das Windows-Gate fordert mindestens **354 Tests**. Die Linux-CI prüft Frontend-Logik und Plattform-Erkennung; echte Wayland-UI-Tests laufen separat in einer geeigneten Sitzung.

<details>
<summary><strong>Testbefehle und Entwicklungswerkzeuge</strong></summary>

Gesamte Lösung auf Windows testen, nach dem oben beschriebenen Build:

```powershell
dotnet test --solution ./nanoboy.sln -c Release --no-build --no-restore
```

Plattformneutrale Tests und Linux-Frontend-Tests:

```bash
dotnet test --project tests/AetherBoy.CoreTests/AetherBoy.CoreTests.csproj -c Release
dotnet test --project tests/AetherBoy.RuntimeTests/AetherBoy.RuntimeTests.csproj -c Release
dotnet test --project tests/AetherBoy.DesktopTests/AetherBoy.DesktopTests.csproj -c Release
```

Zusätzlicher nativer UI-Test innerhalb einer Wayland-Sitzung:

```bash
AETHERBOY_UI_TESTS=1 dotnet test --project tests/AetherBoy.DesktopTests
```

Eigene lokale GB-/GBC-Conformance-ROMs ausführen:

```bash
dotnet run --project tools/AetherBoy.Conformance/AetherBoy.Conformance.csproj -c Release -- \
  "/pfad/zur/testsuite" --max-frames 600 --json ./artifacts/conformance.json
```

Die CLI unterstützt einzelne ROMs, Verzeichnisse und ein [JSON-Manifest](tools/AetherBoy.Conformance/compatibility.example.json). Test-ROMs sind nicht enthalten. Exitcode `0` bedeutet bestandene Pflichtläufe, `1` einen blockierenden Fehler oder Timeout und `2` einen Aufruffehler.

Den separaten ARM/Thumb/Mode-3-Prototyp mit einem generierten Testprogramm prüfen:

```bash
dotnet run --project tools/AetherBoy.GbaProbe/AetherBoy.GbaProbe.csproj -c Release
```

Dieser Lern- und Regressionpfad schreibt `artifacts/gba-prototype.bmp`. Er ist vom GBADotnet-Kern der normalen Anwendung unabhängig; mGBA dient ausschließlich als Referenz.

</details>

## Dokumentation

| Thema | Einstieg |
| --- | --- |
| Projektstand und nächste Schritte | [Deutsch](docs/PROJECT_STATUS_DE.md) · [English](docs/PROJECT_STATUS_EN.md) |
| Linux einrichten und bedienen | [Wayland / Hyprland (DE)](docs/LINUX_WAYLAND.md) · [User Guide (EN)](docs/LINUX_USER_GUIDE.md) |
| Windows- und Linux-Oberfläche | [UI-Zuordnung und offene Funktionen](docs/LINUX_UI_PARITY.md) |
| Emulationskompatibilität | [Testmatrix](COMPATIBILITY.md) · [GBA-Status](GBA.md) |
| GBA-Technik und Herkunft | [Architektur](docs/GBA_CORE_ARCHITECTURE.md) · [GBADotnet-Review](docs/GBADOTNET_REVIEW.md) |
| Änderungen und Abhängigkeiten | [Changelog](CHANGELOG.md) · [Drittanbieterhinweise](THIRD_PARTY_NOTICES.md) |

Fehler gefunden? Ein [Issue](https://github.com/VoltexModz/AetherBoy/issues) mit Version, Betriebssystem, unter Linux auch Desktop/Compositor, Reproduktionsschritten und erwarteter Ausgabe hilft bei der Eingrenzung. Keine ROMs, BIOS-Dateien oder persönlichen Spielstände hochladen.

## Lizenz und Herkunft

AetherBoy begann 2014 als **nanoboy** von **Frédéric Meyer**, wurde als **ChiiBoy Color** weitergeführt und wird heute unter dem Namen AetherBoy modernisiert. Urheberschaft und Copyright verbleiben bei den ursprünglichen Autoren und späteren Beitragenden.

Der Emulatorcode steht unter **GPL-3.0-only**; siehe [LICENSE](LICENSE). Drittanbieterkomponenten, insbesondere der MIT-lizenzierte GBADotnet-Kern und die gebündelten Schriften, besitzen eigene Lizenzen: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Verwende nur ROMs und Firmware, zu deren Nutzung du berechtigt bist. Die Codelizenz gewährt keine Rechte an Spielen oder Nintendo-Firmware. AetherBoy ist nicht mit Nintendo verbunden oder von Nintendo autorisiert; Game Boy, Game Boy Color und Game Boy Advance sind Marken ihrer jeweiligen Rechteinhaber.
