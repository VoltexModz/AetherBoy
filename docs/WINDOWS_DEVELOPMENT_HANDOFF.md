# Windows-Entwicklung: Übergabe / Developer handoff

Stand / Date: **2026-09-11** · Branch: **development** · **4.8.0-alpha.1**

## Deutsch

### Kurzfassung für den Linux-Mitentwickler

Dieses Paket bündelt die bisher lokal entwickelten Windows-Funktionen seit
`d983ed4` (`feat(windows): centralize app data and development diagnostics`).
Es bleibt **ein Projekt mit gemeinsamem Core und Runtime**, nicht zwei Emulatoren.
Die bereits übernommenen Linux-/HLE-BIOS-Korrekturen werden nicht zurückgesetzt.
Die Versionsnummer bleibt Alpha; Commit-ID und Buildidentität unterscheiden den Stand.

- Windows: neue Ausgabe, Spielkomfort, Controller-Menüs, Patch Lab und Diagnose.
- Gemeinsam: Stereo-Audio, GB/GBC-Audio-State-Erweiterung, GBA-Inspector-Snapshots,
  WAV-Recorder und plattformneutraler IPS-/BPS-/UPS-Parser.
- Linux: **keine Änderung an Wayland-, SDL-, Portal-, Eingabe- oder Ausgabelogik**.
  Im Linux-`.csproj` ändern sich ausschließlich Team-/Autoren-Metadaten.
- Kein neuer GBA-Kern und keine Änderung unter `third_party/GBADotnet.Core` in
  diesem Paket. SDK und Linux-Paketversionen bleiben unverändert.
- Keine Veröffentlichung als fertige/stabile Version. Keine ROMs, BIOS-Dateien,
  Saves, privaten Referenzarchive oder lokalen Diagnoseberichte im Paket.

### Was auf Windows hinzugekommen ist

| Bereich | Verhalten und Einstieg | Details |
| --- | --- | --- |
| Ausgabe | WASAPI mit WinMM-Fallback, begrenzte Audiopuffer, Geräteerholung; Direct2D/GDI, VSync und Integer Scaling | [Audio/Video/UI](WINDOWS_AUDIO_VIDEO_UI.md) |
| Fenster und Team | Randloses Vollbild mit F11/Alt+Enter, Rückkehr mit Esc, DPI-/Scroll-Anpassung; NekoZDevTeam sichtbar; Coffee-Button noch ohne Ziel-URL | [Audio/Video/UI](WINDOWS_AUDIO_VIDEO_UI.md) |
| Speichern | F6-Galerie: fünf manuelle Slots plus separates Fortsetzen; etwa alle 60 Sekunden und beim regulären Wechsel/Beenden; Strg+F8 für einmaliges Lade-Rückgängig | [Spielkomfort](WINDOWS_GAME_COMFORT.md) |
| Bibliothek | Suche, GB/GBC/GBA-Filter, Favoriten, Spielzeit, Vorschauen, Kacheln/Liste, eigener Titel und ROM-Ordner-Zugang | [Spielkomfort](WINDOWS_GAME_COMFORT.md) |
| Profile | Anzeige-, Audio- und Eingabeausnahmen pro ROM; nicht überschriebene Werte bleiben global; BIOS und manueller Slot bleiben global | [Spielkomfort](WINDOWS_GAME_COMFORT.md) |
| Controller | F10/QUICK/L3+R3 öffnet pausiertes Quick Deck; eigene ROM-Auswahl, Bildschirmtastatur und Dialog-Navigation; Neutralstellung nach Fokuswechsel | [Controller/Stereo](WINDOWS_PLAYER_TOOLS_STEREO.md) |
| Aufnahme/Messwerte | F12 speichert native Spielbild-PNGs; F9 zeigt Präsentations-FPS, Bildintervalle/P95 und Audiopuffer, keine behauptete Eingabelatenz | [Controller/Stereo](WINDOWS_PLAYER_TOOLS_STEREO.md) |
| Patch Lab | Bibliothek → IPS/BPS/UPS; getrenntes Ergebnis, keine Veränderung der Quelle; UPS-Rückpatchen ausdrücklich wählbar | [Patch Lab](WINDOWS_PATCH_LAB_DIAGNOSTICS.md) |
| Diagnose | Lokaler Hintergrundbeobachter für mögliche Hänger/weiße Bilder, mit Pause-/Fokusunterdrückung; manuelles „Problem markieren“ | [Diagnose](WINDOWS_PATCH_LAB_DIAGNOSTICS.md) |
| GBA Audio Inspector | PSG und Direct Sound A/B, FIFO/Timer/Routing/Pegel, beide Wave-Bänke; Stereo-WAV mit 65.536 Hz | [Inspector](WINDOWS_PATCH_LAB_DIAGNOSTICS.md) |

Paketnummern 1–3, 4–6 und 8–10 sind historische Arbeitsgruppen. **Punkt 7
(komfortabler Save-Import/-Export) ist noch offen**, nicht versehentlich mit erledigt.
Die Einzelberichte nennen teilweise frühere Testzahlen; der Gesamtstand steht hier.

### Gemeinsame Schnittstellen: vor Linux-Änderungen lesen

#### 1. Audio: Zeitframes sind nicht Kanalwerte

`AudioSamplesAvailableEventArgs` in `nanoboy/Runtime/SnapshotBuffers.cs` besitzt:

| API | Vertrag |
| --- | --- |
| `SampleRate` | Zeitframes pro Sekunde |
| `Channels` | 1 oder 2 |
| `SampleCount` | Anzahl Zeitframes, unveränderter Legacy-Vertrag |
| `InterleavedSampleCount` | Anzahl einzelner Kanalwerte: Frames × Kanäle |
| `GetSamplesCopy()` / `CopySamplesTo()` | Weiterhin Mono; Stereo wird mit `(L + R) / 2` heruntergemischt |
| `GetInterleavedSamplesCopy()` | Kopie mit `L,R,L,R,…` bei zwei Kanälen |

`WaylandEmulatorHost` verwendet weiterhin `GetSamplesCopy()` und `SdlAudioOutput`
konfiguriert weiterhin einen Kanal. **Linux läuft daher nicht automatisch in Stereo**,
bekommt aber die bisher erwartete Mono-Datenlänge und Geschwindigkeit.

Für eine spätere Stereo-Anbindung gemeinsam ändern: Datenaufruf, SDL-Kanalzahl,
Formatwechsel, Puffergrößen und Zeitberechnung. Bei Float32 gilt:
`Bytes = Frames × Channels × sizeof(float)`. Niemals Stereo-Werte an ein als Mono
konfiguriertes Gerät senden. Die vorhandenen Mono-Methoden für andere Verbraucher
beibehalten. `WavRecorder.Start(path, rate, channels)` akzeptiert ein oder zwei Kanäle;
`AddFrames(eventArgs)` behandelt den Runtime-Vertrag.

#### 2. Save-State-Kompatibilität

GB/GBC `Core/Audio/Audio.cs` schreibt eine optionale **STER-Erweiterung** mit
linkem/rechtem Filterzustand und noch nicht ausgegebenem Stereo-Puffer.

- Neuer Code liest alte Mono-Payloads; fehlende Stereo-Historie wird initialisiert.
- Alte Builds lesen die neue Erweiterung **nicht**. Bei einem Downgrade neue States
  nicht als kompatibel behandeln. Für wichtige Spielfortschritte zusätzlich im Spiel speichern.
- Batterie-Save-Formate und das GBA-State-Format ändern sich in diesem Paket nicht.
- Das gilt auch für neue States, die Linux mit dem gemeinsamen Core erzeugt,
  obwohl dessen Geräteausgabe noch Mono ist. Beide Entwickler sollten vor dem
  Austausch neuer GB/GBC-States auf diesen Stand aktualisieren.
- Windows-Vorschauen/Fortsetzen liegen neben den rohen State-Dateien. Diese
  Komfortfunktionen sind keine automatische Linux-Implementierung.

#### 3. GBA-Inspector-Snapshots

`AudioSnapshot.DirectSoundA/B` sind optional und bei GB/GBC nicht vorhanden.
GBA `WaveChannelSnapshot` enthält jetzt **64 entpackte Vier-Bit-Samples** statt
32 gepackter Bytes. GB/GBC behalten 32 Samples. Die Anzeige sollte `WaveRamLength`
verwenden; `OutputGain` berücksichtigt auch GBA-75-%-Lautstärke. Das sind Snapshots,
keine cycle-genauen DMA-Traces. Keine Änderung des zugrunde liegenden GBA-Mixers.

#### 4. Wiederverwendbarer Patcher

`AetherBoy.Runtime.Cartridges.RomPatcher.Apply(source, patch, reverseUps: false)`
benötigt keine Windows-Bibliothek. `RomPatchResult` liefert `Image`, `Format`,
`ChecksumsVerified` und `Reversed`. Die Standardrichtung bleibt vorwärts.

IPS hat keine eingebauten Prüfsummen. BPS/UPS verlangen passende Basisgröße sowie
Basis-/Patch-/Ergebnis-CRC32. Grenzen: 32 MiB je ROM, 64 MiB je Patch. Ungültige
Eingaben ergeben `InvalidDataException`; ein UI muss diese ausdrücklich behandeln.
UPS kann mit demselben Patch zurückgesetzt werden, aber nur auf ausdrücklichen Wunsch.
Keine Archive, Downloads, Patch-Erstellung oder Prüfsummen-Umgehung.

Der Windows-Importdienst ist **nicht** der portable Teil: Er wählt AppData-Pfade,
Bibliothekstitel und Hash-Provenienz. Eine spätere Linux-Oberfläche kann den Parser
verwenden und ihre eigene bestehende Ablage anbinden. Kopieren und Patchen dürfen
nicht stillschweigend Spielstände zwischen Original und Hack übernehmen.

### Orientierung im Code und Abhängigkeiten

- `nanoboy/frmNano.WindowsExperience.cs`: Vollbild und Ausgabe-Status.
- `nanoboy/frmNano.GameExperience.cs`: Galerie, Fortsetzen, Rückgängig, Spielzeit/Profile.
- `nanoboy/frmNano.PlayerTools.cs`, `frmQuickMenu.cs`, `Input/GamepadNavigation.cs`:
  Quick Deck, Controller-Navigation, Screenshots und Performance-Overlay.
- `nanoboy/Platform/Audio/NAudioSoundOut.cs`, `Platform/Video/`: Windows-Geräteausgabe.
- `nanoboy/Storage/Windows*.cs`: Windows-Ablage, Metadaten, States und Patch-Import.
- `nanoboy/Diagnostics/`: lokale Session- und Verdachtsprotokolle.
- `nanoboy/Runtime/`: gemeinsame Audioverträge, Recorder, Patcher und GBA-Telemetrie.
- `tests/`: Core-/Runtime-Verträge sowie isolierte Windows-Speicher-/UI-Tests.

Neue direkte Windows-Abhängigkeiten: **NAudio.Wasapi 2.3.0** und
**Vortice.Direct2D1 3.8.3**. WinMM 2.3.0 bleibt als Fallback. Vortice bringt unter
anderem SharpGen.Runtime/Runtime.COM **2.4.2-beta** mit; dies ist eine transitive
Abhängigkeit, keine neue Kernbasis. Lockfiles und [Drittanbieterhinweise](../THIRD_PARTY_NOTICES.md)
gehören zum Commit. Keine zusätzliche Patcher-Bibliothek. Vorhandene Herkunfts-
und Lizenzhinweise, insbesondere für GBADotnet, bleiben erhalten.

### Daten und Datenschutz

Windows bleibt unter `%LOCALAPPDATA%\AetherBoy`: `Roms`, `Saves`, `States`,
`Settings/Profiles`, `Library`, `Screenshots`, `Recordings`, `Firmware` und
`development/Sessions` beziehungsweise `development/Crashes`.
ROM-Identitäten beruhen auf SHA-256. Ein anderes Patch-Ergebnis hat eigene Daten;
identische bekannte Ergebnisse werden wiederverwendet. Beim UPS-Rückpatchen auf
ein vorhandenes Original bleiben auch dessen Titel/Favoriten/Spielzeit erhalten.

Die normale Development-Anwendung ist zugleich die Testversion, auch beim
Release-Build. Kein zusätzlicher Tester-Build, kein automatischer Upload. Bilder,
ROMs, BIOS, Saves und Audio werden nicht in den Diagnose-ZIP aufgenommen; seine
Positivliste umfasst README und Sitzungslog. Verdachtsmeldungen sind keine Beweise
für einen Emulationsfehler. Linux-Datenpfade werden durch dieses Paket nicht migriert.

### Aktualisieren und prüfen

Für einen **sauberen Checkout ohne eigene ungepushte Commits**:

```bash
git status --short --branch
git switch development
git pull --ff-only origin development
git rev-parse --short HEAD
```

Wenn eigene Arbeit offen ist oder Fast-forward scheitert: nicht zurücksetzen und
nicht erzwingen. Eigene Änderungen auf einem Arbeitsbranch committen und die
Zusammenführung abstimmen. Unterschiedliche Dateien reduzieren Konflikte, verhindern
aber keine semantischen Konflikte an gemeinsamen Schnittstellen.

Windows, SDK gemäß `global.json` (10.0.302, gleiche Feature-Band):

```powershell
dotnet restore nanoboy.sln --locked-mode --configfile NuGet.config
dotnet build nanoboy.sln -c Release --no-restore -p:ContinuousIntegrationBuild=true
dotnet test --solution nanoboy.sln -c Release --no-build --no-restore --minimum-expected-tests 440
```

Optionale Hardware-/Bildproben auf einem interaktiven Windows-Desktop:

```powershell
$env:AETHERBOY_HARDWARE_SMOKE = '1'
$env:AETHERBOY_SMOKE_SCREENSHOTS = Join-Path (Get-Location) 'artifacts/windows-experience-smoke'
dotnet test --solution nanoboy.sln -c Release --no-build --no-restore
```

Linux, keine Windows-Lösung bauen:

```bash
dotnet test --project tests/AetherBoy.CoreTests/AetherBoy.CoreTests.csproj -c Release
dotnet test --project tests/AetherBoy.RuntimeTests/AetherBoy.RuntimeTests.csproj -c Release
dotnet test --project tests/AetherBoy.DesktopTests/AetherBoy.DesktopTests.csproj -c Release
bash scripts/build-linux.sh
bash scripts/run-linux.sh "/pfad/zur/eigenen-rom.gba"
```

Zusätzliche UI-Prüfung **innerhalb einer echten Wayland-Sitzung**:

```bash
AETHERBOY_UI_TESTS=1 dotnet test --project tests/AetherBoy.DesktopTests/AetherBoy.DesktopTests.csproj -c Release
```

### Verifiziert und noch offen

- Locked-Restore und Release-Build erfolgreich, keine Warnungen/Fehler.
- Gesamtsuite: **440 Tests**. Ohne optionale Hardware: **436 bestanden, 4 übersprungen**.
  Mit Windows-Hardwareproben: **439 bestanden, 1 Wayland-UI-Test übersprungen**.
- Die neuen UPS-UI-Tests installieren den WinForms-Synchronisationskontext vor
  jedem asynchronen Klick und aktivieren die Threadzugriffsprüfung. So werden
  unfertige Dialogupdates nicht mit einem abgeschlossenen Vorgang verwechselt.
- Geprüft: Mono-/Stereo-Verträge, State-Restore, Ringpuffer, Parsergrenzen,
  beschädigte Patches, Import-/Rückpatch-Sicherheit, Profile, Galerie, Diagnose-
  Datenschutz, UI und synthetische WAV-/Bildausgaben. Hardwareproben senden Stille.
- Die CI verlangt nun mindestens 440 Windows- und 126 Runtime-Tests. Der lokale
  Windows-Lauf ist **kein Nachweis** eines tatsächlich ausgeführten Linux-GUI-Tests.
- Offen: echte Pokémon-/Hack-Spieltests, längere GB/GBC/GBA-Sitzungen, hörbare
  L/R-Prüfung, USB/Bluetooth-Gerätewechsel, echte Controller und mehrere DPI-/GPU-Konfigurationen.
- Coffee-Ziel-URL, Save-Import/-Export und Linux-Anbindung der neuen Komforttools
  bleiben Folgeschritte. Kein neuer Konsolenport oder vollständiger Link-/Netzwerkmodus.

Empfohlener nächster gemeinsamer Schritt: gleicher Commit auf beiden PCs, je ein
GB-, GBC- und GBA-Spiel starten, im Spiel speichern/neustarten, State laden und
Audio/Pause/Turbo prüfen. Danach getrennte Windows-/Linux-Frontend-Arbeit; gemeinsame
Core-/Runtime-Änderungen kurz abstimmen und auf beiden Plattformen nachprüfen.

## English

### Scope and delivered features

This development package starts from `d983ed4` and keeps **one shared project**.
The prior Linux/HLE-BIOS fixes remain in place. Version stays `4.8.0-alpha.1`;
use the commit/build identity to distinguish builds. This is not a stable release.

Windows gains WASAPI/WinMM recovery and bounded queues; Direct2D/GDI presentation,
VSync, integer scaling, borderless fullscreen and DPI-aware dialogs; state gallery,
separate resume slot, one-step load undo; searchable favorites/playtime library,
per-ROM profiles; controller Quick Deck and in-app browsing/keyboard; native PNG
capture and presentation metrics; IPS/BPS/UPS Patch Lab; local suspected-stall
monitoring; and a GBA PSG/Direct Sound inspector with stereo WAV recording.
NekoZDevTeam branding and the coming-soon coffee button are visible.

The Linux project file changes only team/author metadata. No SDL, Wayland, portal,
input or output implementation is changed. No vendored GBA-core files, SDK version
or Linux dependency versions change. Shared Core/Runtime changes are intentional:
stereo, audio-state history, inspector snapshots, recorder and patch parsing.

### Shared contracts and migration cautions

- `AudioSamplesAvailableEventArgs.SampleCount` is still the number of **time frames**.
  `Channels` is 1/2; `InterleavedSampleCount` counts channel values.
  `GetSamplesCopy()` and `CopySamplesTo()` remain mono-downmix compatibility APIs.
  `GetInterleavedSamplesCopy()` returns a copy in L/R order for stereo.
- Linux still requests mono data and configures one SDL channel. To add stereo,
  update data access, device format, reconfiguration and queue/latency arithmetic
  together: `bytes = frames × channels × sizeof(float)`. Do not feed stereo into
  the old mono device. `WavRecorder.Start(path, rate, channels)` retains mono support;
  `AddFrames(eventArgs)` handles runtime blocks.
- GB/GBC audio states append `STER` history. New readers accept legacy mono states;
  **old builds cannot load the new extension**. This affects Linux-created states
  too because the Core is shared. Battery saves and GBA state formats are unchanged.
  New Windows preview/resume metadata does not implement those tools on Linux.
- GBA wave snapshots now hold **64 unpacked 4-bit samples**, not 32 packed bytes.
  GB/GBC remain at 32. Use `WaveRamLength` and `OutputGain`, including GBA 75% gain.
  `DirectSoundA/B` are optional on `AudioSnapshot`; these are snapshots, not traces.
- Portable `RomPatcher.Apply(source, patch, reverseUps: false)` returns image,
  format, checksum status and direction. BPS/UPS validate all three CRC32 values
  and expected source size; IPS cannot validate the base ROM. Limits are 32 MiB
  per ROM and 64 MiB per patch. Handle `InvalidDataException` explicitly in a UI.
  UPS undo is opt-in. There are no archive/download/create/bypass features.
- `WindowsRomPatchService` is Windows-specific library/storage orchestration,
  not the shared parser. Never transfer original saves into a hack implicitly.

### Source map, dependencies and storage

The source map and API table above apply to both languages. Start with the
`frmNano.WindowsExperience`, `frmNano.GameExperience` and `frmNano.PlayerTools`
partials for Windows integration; `Platform/Audio`, `Platform/Video`, `Storage`
and `Diagnostics` contain host services. `Runtime/SnapshotBuffers.cs`, `Snapshots.cs`,
`Audio/WavRecorder.cs`, `Cartridges/RomPatcher.cs` and the production machines define
the shared additions. Core stereo history lives in `Core/Audio/Audio.cs`.

New direct Windows dependencies are NAudio.Wasapi 2.3.0 and Vortice.Direct2D1 3.8.3.
WinMM remains. SharpGen.Runtime/Runtime.COM 2.4.2-beta are transitive Vortice
dependencies. Lockfiles and notices accompany the changes. No additional patcher
library or new emulator core is embedded; existing licenses/provenance are retained.

Windows data remains local under `%LOCALAPPDATA%\AetherBoy`, including ROM copies,
saves/states, profiles, library metadata, screenshots, recordings and development
reports. ROM hashes isolate hacks and deduplicate identical results. UPS restoration
preserves an existing original's title, favorites, playtime and game data.
The normal development build is the tester build. No automatic uploads; diagnostic
ZIPs include only their README/session log, not games, firmware, images, saves or audio.
Linux paths are not migrated. Heuristic warnings do not prove emulation defects.

### Updating, verification and follow-up

Use the commands above: inspect `git status`, switch to `development` and use
`git pull --ff-only origin development` only with a clean checkout and no private
commits. Keep your work on a branch if histories diverge; never force/reset to
accept this package. Build Windows with locked restore and the pinned .NET SDK;
on Linux build the portable projects and use `scripts/build-linux.sh`, not the
WinForms solution. Run the native UI test only within a real Wayland session.

Verification: locked restore and Release build passed without warnings/errors.
The full suite contains **440 tests**: **436 passed / 4 skipped** without opt-in
hardware, **439 passed / 1 skipped** with Windows hardware. The remaining native
Wayland UI test was not executed on this Windows machine. The new patch UI tests
explicitly install a WinForms synchronization context and check thread access.
CI floors are 440 Windows tests and 126 portable Runtime tests.

Tests cover audio contracts, states, bounded parsing, malformed patches, safe
imports/undo, profiles, gallery, privacy and actual UI/recording paths using synthetic
data. Hardware smoke probes submit silence: they are not listening tests or
commercial-game compatibility evidence. Real Pokémon/hack play, long sessions,
headset/Bluetooth/device changes, physical controllers and mixed DPI/GPU testing remain.

Next: both developers use the same commit, test GB/GBC/GBA boot, battery save/restart,
state restore, sound, pause and turbo, then continue separate frontend work.
Coordinate shared Core/Runtime changes even when Git reports no line conflicts.
The support URL, friendly save import/export (item 7), Linux comfort-tool integration
and broader testing remain open. No new console or finished network/link mode is claimed.

Detailed feature guides: [output](WINDOWS_AUDIO_VIDEO_UI.md#english),
[game comfort](WINDOWS_GAME_COMFORT.md#english),
[controller/stereo](WINDOWS_PLAYER_TOOLS_STEREO.md#english),
[patching/diagnostics/GBA inspector](WINDOWS_PATCH_LAB_DIAGNOSTICS.md#english).
