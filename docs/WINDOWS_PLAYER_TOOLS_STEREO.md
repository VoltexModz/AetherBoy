# Windows-Paket 4–6: Controller, Screenshots, Performance und Stereo

Stand: 11. September 2026. Fortsetzung von
[State-Galerie, Bibliothek und Spielprofilen](WINDOWS_GAME_COMFORT.md).

Nachtrag: [Paket 8–10](WINDOWS_PATCH_LAB_DIAGNOSTICS.md) ergänzt Patch Lab,
Sitzungsbeobachtung und den hier noch nicht enthaltenen GBA Audio Inspector.
Update: package 8–10 adds the GBA Inspector UI described as out of scope below.

## Deutsch

### 4. Oberfläche mit Controller und Quick Deck

| Aktion | Bedienung |
| --- | --- |
| Quick Deck im Spiel öffnen | **F10**, **QUICK** im Hauptfenster oder **L3+R3** (beide Stick-Tasten) |
| Menüauswahl bewegen | D-Pad oder linker Stick |
| Bestätigen / Button drücken | Untere Aktionstaste, **A / South** |
| Zurück / Dialog schließen | Rechte Aktionstaste, **B / East** |
| Zum vorigen/nächsten Bedienelement | **LB / RB** |
| Liste verlassen / anderes Feld erreichen | **LB / RB**, auch wenn die Liste viele Spiele enthält |
| Auswahlfeld / Schieberegler ändern | Links/rechts; A schaltet Auswahlfelder ebenfalls weiter |

Die Bezeichnungen beziehen sich auf die **Position** der Controller-Taste, nicht
auf die ROM-interne A/B-Belegung. Die Menübedienung bleibt unabhängig vom
Spielprofil. L3+R3 ist für das Quick Deck reserviert; Controller ohne Stick-Tasten
benötigen dafür F10 oder den QUICK-Button.

Das Quick Deck pausiert das Spiel. Darin sind Slotwahl, Speichern/Laden, Galerie,
Bibliothek, Einstellungen, Vollbild, Screenshot und Performance-Schalter erreichbar.
Beim Schließen wird der vorherige Pausenstatus wiederhergestellt, sofern nicht
ausdrücklich „beim Schließen pausiert/weiterspielen“ umgeschaltet wurde. Das Öffnen
der Bibliothek, Einstellungen oder Galerie erfolgt anschließend als eigener Dialog.

Bibliothek, Control Center und die Aether-Dialoge haben eine gemeinsame
Controller-Navigation. Texteingaben bekommen eine kompakte Bildschirmtastatur.
Die ROM-Dateiauswahl läuft ebenfalls im eigenen Fenster: Laufwerk, Downloads,
Dokumente, Elternordner und Unterordner sind erreichbar, angezeigt werden Ordner
und `.gb`/`.gbc`/`.gba`. Pro Verzeichnis werden höchstens 5000 Einträge angezeigt.
Nicht lesbare Verzeichnisse führen zu einem Hinweis, nicht zum Beenden der App.

Beim Fokuswechsel oder Wiederverbinden muss der Controller zunächst neutral sein.
Bestätigen und Zurück reagieren auf neue Tastendrücke; nur Richtungen wiederholen
sich beim Halten. Während der Gamepad-Neubelegung ist die Menünavigation gesperrt.
Solange ein Dialog oder eine andere Anwendung den Fokus hat, gehen keine
Gamepad-Eingaben ins Spiel. Die Navigation steuert keine fremden Anwendungen.

**Abgrenzung:** Windows-eigene Spezialdialoge außerhalb der neuen ROM-Auswahl
(z. B. ein Export-Speicherdialog), Explorer und externe Webseiten bleiben
Betriebssystemoberflächen. Die kompakte Tastatur ersetzt keine vollständige
Windows-Tastatur. Physische Controller-/Remap- und DPI-Kombinationen brauchen
weiterhin praktische Tests.

### 5. Screenshots und optionale Performance-Anzeige

- **F12** speichert das zuletzt veröffentlichte Spielbild in nativer Auflösung:
  GB/GBC **160×144**, GBA **240×160**. Keine Fensterrahmen, Displayfilter oder
  Diagnose-Overlays im PNG. Es wird weder der Windows-Desktop noch eine andere
  Anwendung aufgenommen. Ohne Spielbild wird keine leere Aufnahme erstellt.
- Ablage: `%LOCALAPPDATA%\AetherBoy\Screenshots\<ROM-SHA256>\`. UTC-Zeitstempel und
  eindeutige Kennung verhindern das Überschreiben vorhandener Bilder. Der Ordner
  ist unter **Control Center → Ordner → Screenshots öffnen** erreichbar.
- **F9** schaltet das Overlay ein/aus. Alternativ: Quick Deck oder
  **Control Center → Diagnostics**. Der Schalter wird global gespeichert.
- Angezeigt werden tatsächlich präsentierte neue Bilder pro Sekunde, mittlerer
  Bildabstand und dessen P95, Ausgabebackend sowie Audiokanäle, Pufferstand/-ziel
  und Unterläufe. Die letzten maximal 120 Bildabstände bilden das Messfenster;
  Aktualisierung der Anzeige etwa zweimal pro Sekunde. Pausen/Startzustände zeigen
  keine laufenden Ausgabe-FPS, lange Unterbrechungen setzen das Messfenster zurück.

Das sind **Ausgabe-/Präsentationswerte**, keine exakten GPU-Zeitstempel, keine
CPU-Zeit pro emuliertem Frame und keine Eingabelatenzmessung. Frameskip oder Turbo
können Ausgabe-FPS und Emulationsfortschritt voneinander unterscheiden lassen.
Ein Screenshot stammt vom letzten veröffentlichten Bild, nicht zwingend vom
allerneuesten, noch nicht veröffentlichten Emulationszustand.

Screenshots werden nicht automatisch versendet. Der Development-ZIP-Export bleibt
auf seine bisherige Positivliste (README und Sitzungslog) beschränkt und nimmt
keine PNGs auf. Eine manuelle Aufnahme protokolliert nur Erfolg/Fehler, nicht die
Pixel oder den Screenshot-Pfad.

### 6. Echtes Stereo bis zur Windows-Ausgabe

- **GB/GBC:** NR51-Ausgangsrouting und NR50-Lautstärke bleiben links/rechts getrennt.
  Beide Seiten besitzen eigene Hochpassfilterzustände. Der bisherige Mono-Core-
  Puffer bleibt für ältere Verbraucher vorhanden.
- **GBA:** Der Adapter erhält die zwei vom Kern gelieferten 16-Bit-PCM-Kanäle in
  L/R-Reihenfolge, statt sie vor der Runtime zu einem Mono-Signal zu mischen.
- **Runtime → Windows:** Interleavte Float-Frames, explizite Kanalzahl und ein
  frame-ausgerichteter Mono-/Stereo-Ringpuffer. Formatwechsel bauen die Ausgabe
  neu auf. Pufferzeiten berücksichtigen Samplerate **und** Kanalzahl, damit Stereo
  weder falsche Laufzeiten noch vertauschte Kanäle durch Überläufe erzeugt.
- Der vorhandene GB/GBC-WAV-Aufnahmedialog schreibt jetzt Stereo. `WavRecorder`
  unterstützt zusätzlich weiter Mono-Aufrufer; PCM-Header, Byte-Rate und
  Blockausrichtung entsprechen der gewählten Kanalzahl. Der aktuelle Gesamtstand
  enthält außerdem die [GBA-Inspector-Aufnahme aus Paket 8–10](WINDOWS_PATCH_LAB_DIAGNOSTICS.md).

**Save-State-Kompatibilität:** GB/GBC-Audiozustände erhalten eine optionale
`STER`-Erweiterung für beide Filter und den noch nicht ausgegebenen Stereo-Puffer.
Neue Builds können alte Mono-Zustände lesen. Deren fehlende Stereo-Historie wird
aus den Mono-Werten initialisiert; ein kurzer Audioübergang ist möglich. Alte
Builds können die neue Erweiterung nicht lesen. Batterie-Saves und das
GBA-State-Format werden durch dieses Paket nicht geändert.

### Übergabe an die Linux-Entwicklung

Anders als Paket 1–3 verändert Stereo **gemeinsamen Core-/Runtime-Code**.
Linux-Frontend-Code wurde in diesem Paket nicht geändert. Sein bestehender
`GetSamplesCopy()`-Aufruf erhält bewusst weiterhin einen Mono-Downmix mit einem
Wert pro Audioframe; Geschwindigkeit und Puffergrößen bleiben damit kompatibel.
Linux spielt also nicht allein durch dieses Paket automatisch Stereo ab.

Für eine spätere Linux-Stereo-Anbindung:

```csharp
float[] interleaved = eventArgs.GetInterleavedSamplesCopy(); // L,R,L,R bei Channels == 2
int channels = eventArgs.Channels;
int frames = eventArgs.SampleCount; // Historischer Mono-Vertrag: Anzahl Zeitframes
int scalarSamples = eventArgs.InterleavedSampleCount;
```

Ausgabegerät und Puffer müssen dann gemeinsam auf `SampleRate` und `Channels`
umgestellt werden. `GetSamplesCopy()` und `CopySamplesTo()` bleiben ausdrücklich
Mono-Kompatibilitätsmethoden. Die Runtime besitzt ihre Audiodaten und gibt Kopien
zurück. Die vorhandene begrenzte Audiowarteschlange erhält die Kanalinformation.

### Verifikation und noch offene Praxistests

Automatisierte Tests prüfen Controller-Flanken/Wiederholung/Neutralstellung,
Fokus-Sperre, Bedienung von Feldern und ROM-Auswahl, Quick-Deck-Pause und Speichern,
PNG-Auflösung und eindeutige Dateinamen, FPS-/Intervallberechnung, DMG/CGB-L/R-
Routing, Stereo-State-Restore, alte Mono-Payloads, GBA-PCM-Reihenfolge, Runtime-
Downmix-Kompatibilität, Dispatcher, WAV-Header und Stereo-Ringpufferüberläufe.

UI-Screenshots nutzen selbst erzeugte ROM-/Bilddaten und isolierte Testordner.
Hardwaretests senden stille Puffer; sie sind kein Hörtest. Noch erforderlich:
L/R-Hörprobe mit Spielen, Kopfhörerwechsel, längere GB/GBC/GBA-Sitzungen,
Controller-Bedienung am echten Gerät und zusätzliche Monitor-/DPI-Kombinationen.
Ein nativer Wayland-UI-Test kann auf diesem Windows-PC nicht freigegeben werden.

Gesamtlauf mit aktivierten Windows-Hardwaretests: **399 Tests, 398 bestanden,
0 fehlgeschlagen, 1 Wayland-UI-Test übersprungen**. WASAPI akzeptierte dabei
Mono/Stereo und 44.100/65.536 Hz; Direct2D und die UI-Aufnahmen bestanden ebenfalls.
Ausgeführt mit `AETHERBOY_HARDWARE_SMOKE=1` und
`dotnet test --solution nanoboy.sln -c Release --no-restore --verbosity quiet`.

## English

### Controls and behavior

- **F10 / QUICK / L3+R3:** open the in-game Quick Deck. The game pauses while it is
  open, then returns to its previous pause status unless changed explicitly.
  Slots, save/load, state gallery, settings, library, fullscreen and capture tools
  are available there. Controllers without stick buttons need F10 or QUICK to
  open it.
- **D-pad / left stick:** navigate; **South/A:** accept; **East/B:** back;
  **LB/RB:** previous/next field. Left/right edits selection fields and sliders.
  Menu mappings are independent of game mappings. Focus changes/reconnection
  require a neutral controller; only directions repeat. Remapping suspends menu
  navigation. Gamepad input is not forwarded to gameplay while another window
  owns focus.
- The in-app ROM browser handles drives, Downloads/Documents and folders with
  up to 5000 entries. The compact on-screen keyboard handles common text input.
  External OS dialogs, Explorer and websites remain outside this controller UI.
- **F12:** save native-resolution gameplay PNGs, without display filters or overlays,
  under `%LOCALAPPDATA%\AetherBoy\Screenshots\<ROM-SHA256>`. No desktop capture or
  upload; unique filenames never replace previous pictures. Open the folder from
  Control Center → Ordner. No image is saved before a frame exists.
- **F9:** toggle a globally persisted presentation/audio overlay. It measures
  presented new-frame FPS, mean/P95 presentation intervals and audio queue status,
  not CPU/GPU execution time or end-to-end input latency. At most 120 intervals are
  retained; labels update about twice a second. Pauses do not report running FPS.

### Stereo and shared-code compatibility

GB/GBC output routing and gains now feed independent left/right high-pass filters.
GBA signed 16-bit PCM is retained as interleaved L/R Float32. Windows playback uses
explicit mono/stereo formats and frame-aligned bounded buffers, including correct
latency calculations and format reconfiguration. Existing GB/GBC WAV recording
now preserves stereo; the recorder still accepts legacy mono use. The current combined
build also includes the [GBA Inspector recording UI from package 8–10](WINDOWS_PATCH_LAB_DIAGNOSTICS.md#english).

GB/GBC audio state payloads have an optional `STER` extension. New readers accept
older mono states, initializing the missing stereo history from mono data; a brief
audio transition is possible. Older builds cannot load newly extended states.
Battery-save formats and the GBA state format are unchanged.

The shared core/runtime changed, but this package does not modify Linux frontend
code. Linux's existing `GetSamplesCopy()` continues returning a mono downmix with
one sample per frame. Linux stereo is a separate frontend follow-up: use
`GetInterleavedSamplesCopy()` with `Channels`, configure the device accordingly,
and size queues in frames. `SampleCount` remains the legacy frame count;
`InterleavedSampleCount` counts all channel values. `CopySamplesTo()` remains mono.

### Verification limits

Tests cover navigation/repeat/focus routing, Quick Deck pause/save, ROM browsing,
PNG output, presentation metrics, both GB models' channel routing, stereo/legacy
state restore, GBA PCM order, the backward-compatible transport, dispatcher,
stereo WAV and ring-buffer alignment. UI images are synthetic. Hardware probes use
silence, not an audible L/R test. Real-controller play, listening tests, device
changes, long sessions, more DPI combinations and native Wayland UI validation
remain manual follow-ups. Diagnostics ZIPs still exclude screenshots and saves.

Full-suite result with Windows hardware probes enabled: **399 total, 398 passed,
0 failed, 1 native Wayland UI test skipped**. WASAPI accepted mono/stereo at
44,100/65,536 Hz; Direct2D and UI capture probes passed. Silence-only probes are
not a listening test.
