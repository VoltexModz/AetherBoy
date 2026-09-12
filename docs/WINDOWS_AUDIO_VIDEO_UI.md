# Windows: Audio, Video und Bedienung / Audio, video and UI

Aktueller Gesamtstand / Current combined status: [DE/EN-Übergabe](WINDOWS_DEVELOPMENT_HANDOFF.md).
Neuer bidirektionaler Abgleich / New bidirectional work: [Platform parity](PLATFORM_PARITY.md).
Dieses Dokument beschreibt den ersten Ausgabe-Ausbau; spätere Pakete ergänzen
Stereo und GBA-Aufnahme. This document describes the initial presentation package;
later packages add stereo and GBA recording.

## Deutsch

### Bedienung

- **Control Center → Audio:** Die Ausgabe folgt dem Windows-Standardgerät für
  Multimedia. WASAPI ist der bevorzugte Weg; WinMM bleibt als Rückfallpfad erhalten.
  Beim Umstecken wird das Standardgerät spätestens beim nächsten Gerätecheck
  erkannt (nominal jede Sekunde, zuzüglich Treiberlaufzeit).
- Der Standard-Zielwert beträgt **40 ms**. Bei Knacken 60 oder 100 ms probieren;
  20 ms ist eine bewusst aggressivere Einstellung. Zielwert, Vorpuffer, Host-Queue,
  Resampling und Treiber ergeben zusammen die tatsächliche Latenz. „40 ms“ ist
  **keine gemessene Ende-zu-Ende-Latenz**. WinMM verwendet mindestens 60 ms.
- **Control Center → Display:** GPU, VSync und Integer Scaling sind separat
  schaltbar. Sharp, Smooth und LCD Grid funktionieren weiterhin. Integer Scaling
  verwendet bei ausreichendem Platz ganze Pixelvielfache mit schwarzen Rändern;
  in kleineren Fenstern wird proportional verkleinert.
- **F11 / Alt+Enter:** Vollbild auf dem aktuellen Monitor; **Esc** kehrt zum
  vorherigen Fenster zurück. Der normale Maximieren-Knopf bleibt davon getrennt.
- **F5 / F8:** Save State speichern/laden. Die Statusleiste bestätigt erst nach
  erfolgreichem Dateischreiben beziehungsweise Wiederherstellen. Unter **Saves**
  stehen zusätzlich Existenz, Größe und Datum des gewählten Slots. „Datei vorhanden“
  behauptet keine erfolgreiche Zustandsvalidierung; die erfolgt beim Laden.
- Große Dialoge passen ins Arbeitsgebiet des Monitors. Bei hoher Skalierung oder
  kleinen Displays sind verbleibende Inhalte über Scrollbalken erreichbar.
- **Info → Buy us a coffee:** Der Button ist bereits sichtbar. „Kommt bald“
  kennzeichnet den noch fehlenden Support-Link; ein Klick zeigt vorerst einen Hinweis.

### Was technisch geändert wurde

Die Änderungen betreffen den Windows-Host. GB/GBC- und GBA-Kern sowie die Linux-
Audio-/Video-Implementierung wurden hierfür nicht verändert. Geräteausgabe und GPU
sind über die vorhandene Session-Grenze angebunden.

Audio verwendet NAudio.Wasapi 2.3.0 im gemeinsamen, ereignisgesteuerten Modus.
Ein eigener MTA-Thread verwaltet Geräte, Formatwechsel und Wiederverbindungen.
Ein begrenzter Float-Ring ersetzt die Queue je Einzelsample. Seit dem Parity-Paket
bleibt wartender Ton bei Überfüllung erhalten; neue Blöcke werden verworfen.
Turbo gibt weiterhin Audio aus. Sitzungs-/Abschnittswechsel verwerfen veraltete
Blöcke, Pause sperrt den Host-Puffer. Bereits an den
Treiber übergebene Samples können noch kurz auslaufen. Dieser erste Ausbau lieferte
noch Mono. Im gemeinsamen aktuellen Stand ist die durchgehende Stereo-Pipeline aus
[Paket 4–6](WINDOWS_PLAYER_TOOLS_STEREO.md) enthalten.

Direct2D zeichnet das emulierte Bild hardwarebeschleunigt ins WinForms-Fenster.
VSync wartet nur auf dem UI-/Präsentationspfad, nicht auf dem Emulationsthread.
GDI bleibt für Druck/Screenshots und bei GPU-Problemen erhalten. Bei Fehlern gibt es
drei automatische Versuche mit mindestens einer Sekunde Abstand; anschließend
lässt sich über GPU aus/an erneut versuchen. Die Emulation bleibt unverändert bei
ihrer Handheld-Taktung, unabhängig von 60-/120-/144-Hz-Displays. Das bestehende
Windows-Timing erhält während aktiver, nicht minimierter Normalgeschwindigkeit
eine auf 1 ms angefragte Timerauflösung. Der UI-Timer fragt alle 8 ms das neueste
Bild ab und zeichnet nur neue Frames. Das ist kein garantierter 8-ms-Präsentationstakt.

Das Control Center zeigt Ausgabezustand und Zähler. Unterlauf-/Verwerfungszähler
gelten pro Audiopuffer-Konfiguration; Pause und absichtliches Priming werden nicht
als Unterläufe gezählt. Video zählt präsentierte Frames und bereits übergebene,
vor dem Zeichnen ersetzte Frames, nicht alle im Core übersprungenen Frames.
Development-Heartbeats zeichnen ausgewählte Zahlen und Backend-/Fehlercodes lokal
auf; keine ROM-/Save-Inhalte, Audiobuffers oder Audio-Gerätenamen werden hinzugefügt.

### Verifikation und offene Praxisprüfung

Automatisiert geprüft: Ringpuffer-Reihenfolge, Grenzen, Pause, Clear, ungültige
Samples, konkurrierende Produzenten/Verbraucher, beide Core-Raten (44.100 / 65.536 Hz),
GB/GBA-Bildgeometrie, Integer Scaling, GDI-Druckpfad, Vollbild-Rückkehr und UI-Anbindungen.
Die optionalen Hardwaretests haben auf diesem Windows-PC WASAPI für beide Raten
und Direct2D für alle drei Filter einschließlich Resize/Neuinitialisierung bestätigt.
Shell und Control-Center-Seiten wurden zusätzlich als Bilder auf Layoutfehler geprüft.

Noch manuell zu prüfen: echtes Headset-Abziehen/-Anstecken, Bluetooth-/USB-Geräte,
Monitorwechsel mit unterschiedlichen DPI-Werten, hörbare Qualität und Latenz,
Frametiming unter Last, Treiberverlust sowie längere GB/GBC/GBA-Spielsitzungen.
Es wurden keine kommerziellen Spiel-ROMs für diese Prüfung verwendet. Das Ergebnis
ist ein verifizierter Windows-Ausbau, keine vollständige Spiele-Kompatibilitätsfreigabe.

## English

### Usage and scope

- **Control Center → Audio:** event-driven shared WASAPI follows the Windows
  multimedia default endpoint. Device discovery/recovery runs on a separate thread,
  nominally every second. No endpoint means silent waiting, not permanently disabled
  sound. WinMM is available as a fallback.
- Choose **20 / 40 / 60 / 100 ms** target latency (40 ms default). Increase the value
  if audio crackles. This is a requested device buffer, **not measured end-to-end
  latency**; priming, queued samples, resampling and driver latency are additional.
  WinMM requests at least 60 ms. This initial package used mono; the current combined
  build includes [end-to-end stereo](WINDOWS_PLAYER_TOOLS_STEREO.md#english).
- **Display:** toggle GPU rendering, VSync and integer scaling. Direct2D supports
  all existing Sharp/Smooth/LCD Grid filters and falls back to GDI if hardware
  presentation fails. Three automatic retries are followed by manual GPU off/on retry.
- **F11 / Alt+Enter** enters borderless fullscreen; **Esc** restores the window.
  Per-monitor V2 DPI and scrollable dialog content keep settings accessible.
- **F5 / F8** reports save/load progress and completion in the footer. The Saves
  page shows the selected slot's file presence, timestamp and size separately from
  in-game battery saves. A present file is not claimed to be validated before loading.
- **Info → Buy us a coffee** is already visible. Until the team provides its support
  link, the button is marked as coming soon and shows an informational message.

The Windows frontend now requests scoped 1-ms timer precision during active,
non-minimized normal-speed emulation and polls latest-frame delivery every 8 ms.
VSync does not drive the emulator clock. Neither improved end-to-end latency nor
perfect frame pacing is claimed without measurements. Core and Linux implementation
remain unchanged. Development logs add numeric audio/video diagnostics, but no
audio samples, endpoint names, ROM or save contents. Audio counters reset on buffer
reconfiguration; deliberately silent startup/pause is not counted as starvation.

Tests cover ring semantics/concurrency, both sample rates, integer scaling,
GB/GBA geometry, fullscreen restoration and UI bindings. Local hardware smoke tests
passed for WASAPI and Direct2D, including renderer resize/recreation and all filters.
Shell and settings screenshots were visually inspected. Physical unplug/replug,
mixed-DPI displays, Bluetooth, audible output, driver-loss recovery and prolonged
real-game sessions still need hands-on testing. No commercial ROMs were used.

### Reproduce / Reproduzieren

Run from a Windows checkout with the SDK selected by `global.json`:

```powershell
dotnet restore nanoboy.sln --locked-mode
dotnet build nanoboy.sln -c Release --no-restore
dotnet test --solution nanoboy.sln -c Release --no-build --no-restore
```

Optional hardware checks open test windows and submit silent audio. They require
an interactive Windows desktop with a working GPU and playback endpoint:

```powershell
$env:AETHERBOY_HARDWARE_SMOKE = '1'
$env:AETHERBOY_SMOKE_SCREENSHOTS = Join-Path (Get-Location) 'artifacts/windows-experience-smoke'
dotnet test --project tests/AetherBoy.SmokeTests/AetherBoy.SmokeTests.csproj -c Release
```

The hardware tests are opt-in so headless CI does not require sound/GPU hardware.
Screenshots of forms use their own rendering surface, not arbitrary desktop capture.
An optional GPU viewport capture is taken only when the test window is foreground.

### Dependencies / Referenzen

- [NAudio 2.3 WASAPI implementation](https://github.com/naudio/NAudio/blob/v2.3.0/NAudio.Wasapi/WasapiOut.cs)
- [Vortice.Direct2D1 3.8.3](https://www.nuget.org/packages/Vortice.Direct2D1/3.8.3)
- [Redistribution notices](../THIRD_PARTY_NOTICES.md), including Vortice and its
  SharpGen 2.4.2-beta transitive runtime dependencies.
