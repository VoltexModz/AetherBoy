# Windows ↔ Linux: Funktionsgleichheit / feature parity

Stand: 12. September 2026. Ausgangspunkt: `22a77ef` (Linux Patch Lab),
inhaltlich zusammengeführt mit Linux-Commit `367674f` und lokalem GBA-Link-Ausbau.
**Ziel ist Gleichheit in beide Richtungen. Der vollständige Abgleich ist noch nicht abgeschlossen.**
Dieser Bericht beschreibt die nachfolgenden lokalen Änderungen; kein neuer Release-/Commit-Hash wird vorweggenommen.

Windows-Nachtrag: [Firmware Station, Diagnosewahl, begrenzte Hintergrundaufzeichnung
und Datenschutz (DE/EN)](WINDOWS_FIRMWARE_DIAGNOSTICS.md).

Neuer Windows-Meilenstein: [Local Link Lab für zwei GB-/GBC- oder zwei GBA-Spielinstanzen (DE/EN)](LOCAL_LINK_LAB.md).

Separater gemeinsamer Meilenstein: [GB/GBC Online Link (DE/EN)](ONLINE_LINK_HANDOFF.md)
mit Einstieg in beiden Frontends, gemeinsamer Kabel-Runtime und WebRTC-Browserhelfer.
Keine bestätigte Pokémon-/Internet-Abnahme; eine native Lobby bleibt offen.
Neuer Folgestand: [GBA Gen3 Online und gemeinsame Save-Wiederherstellung (DE/EN)](GBA_ONLINE_HANDOFF.md).
Das GBA-Entwicklungsprofil und die kontrollierte Kopienübernahme sind in beiden
Frontends angebunden; das ersetzt keine native Linux-/Internet-Spielabnahme.
Gemeinsame Core/Runtime, experimentelle Link-Unterstützung;
Linux-Oberfläche und kommerzielle Link-Spieltests stehen noch aus.
Die [Linux-Integrationsprüfung](LINUX_UPSTREAM_INTEGRATION_REVIEW.md) trennt die
Arbeit des Kollegen von unseren Ergänzungen und erklärt die elf Überschneidungen.

## Deutsch

### Was „1:1“ bedeutet

Beide Frontends sollen dieselben Spielerfunktionen, Werkzeuge, Optionen und
Sicherheitsgarantien anbieten. Die Umsetzung der Betriebssystem-Anbindung bleibt
unterschiedlich: WinForms/WASAPI/Direct2D und AppData unter Windows;
SDL/Wayland/PipeWire und XDG unter Linux. Keine zweite Kernentwicklung, kein Wine,
kein Rückschritt auf X11. Eine Runtime-Funktion allein bedeutet noch keine
fertige Schaltfläche in beiden Oberflächen.

### In diesem Paket umgesetzt

- **Turbo-Audio:** Windows gibt bei Turbo weiter Audio aus. Beide Ausgaben
  verwenden `AudioQueuePolicy`: Wenn ein vollständiger neuer Block nicht in den
  begrenzten Puffer passt, bleibt der bereits wartende Ton zusammenhängend;
  der neue Block wird verworfen. Überlange Einzelblöcke werden begrenzt.
  Windows' konfigurierbarer Geräte-/Ringpuffer und SDLs 125-ms-Grenze bleiben
  Backend-Details, keine identischen Ende-zu-Ende-Latenzversprechen.
- **Wechsel absichern:** Runtime-Audio trägt `PlaybackSession` und
  `PlaybackGeneration`. Pause, Turbo und Timeline-Wechsel verwerfen ausstehende
  Dispatcher-Blöcke. Die Ausgaben leeren beim ersten Block des neuen Abschnitts
  ihren Puffer und lehnen verspätete ältere Blöcke ab. Ein neuer ROM-Start kann
  auch nach vielen Turbo-Wechseln der alten Sitzung Audio ausgeben.
  Die Kennungen sind nur Laufzeit-Metadaten; Save-State-Formate ändern sich nicht.
- **Windows-Menüs:** SYSTEM/TUNE/TOOLS/INFO öffnen eigene Aether-Panels im Fenster,
  keine sichtbaren ToolStrip-Dropdowns. Bestehende Befehle, Häkchen, deaktivierte
  Einträge und Untermenüs bleiben angebunden. Pfeile/Tab, Enter, Esc, Zurück,
  Maus und Controller werden unterstützt; Spieleingaben werden währenddessen
  freigegeben. Alte ToolStrip-Objekte bleiben ausschließlich als Befehlsmodell.
- **Schreibschutz:** `RomWriteLease` ist gemeinsam. Linux behält seine vorhandenen
  Lock-Pfade und Migration; Windows hält zusätzlich `game.sav.lock` während einer
  Sitzung. Ein zweites Fenster darf nicht dieselben Daten schreiben. Backup-Restore
  läuft nach sicherem Sitzungsende ebenfalls unter einer Sperre. Lock-Dateien werden
  beim Freigeben nicht gelöscht; ihre Existenz allein bedeutet keine aktive Sperre.
- **Screenshots:** Beide Frontends verwenden `NativeScreenshot`. Native PNGs mit
  160×144 oder 240×160 Pixeln, ohne Fensterrahmen/Filter, eindeutige Namen, temporäre
  Datei vor Aktivierung. Windows behält seinen bisherigen Speicherort.
  Linux: **F12** oder **Tools → Screenshot**, Ablage unter
  `$XDG_DATA_HOME/aetherboy/screenshots/<ROM-SHA256>` (mit normalem XDG-Fallback).
  **Open screenshots** öffnet den Ordner. Schreiben läuft außerhalb der Oberfläche.
- **Performance:** `PresentationStatistics` liegt jetzt in der Runtime. Linux
  erhält **F9** und **Tools → Performance**, inklusive gespeicherter Einstellung.
  Angezeigt werden neue präsentierte Spielbilder/FPS, mittlerer Bildabstand/P95
  und Backend-Audiopufferwerte; keine behauptete Eingabe- oder Gesamtlatenz.
  Alte Linux-Belegungen auf F9/F12 werden gezielt umgelegt; andere Bindings bleiben.

Unbegrenztes Turbo mit verworfenen Blöcken ist **kein** hochwertiges Time-Stretching.
Der gemeldete wiederholte Ton wurde im Gerätekontext noch nicht hörbar reproduziert.
Die Änderungen beseitigen die unterschiedlichen Puffer-/Mute-Regeln und sichern
Rückkehr/Überlauf ab; der Hörtest mit der betroffenen ROM bleibt erforderlich.

### Abgleich und verbleibende Arbeit

| Bereich | Windows | Linux | Nächster Abgleich |
| --- | --- | --- | --- |
| GB/GBC/GBA, Cheats, Rewind, Stereo, State-Codecs | Gemeinsame Core/Runtime | Gemeinsame Core/Runtime | Gleiche Szenarien auf beiden Ausgaben prüfen |
| IPS/BPS/UPS Patch Lab | Vorhanden | Vorhanden seit `22a77ef` | Gleiches Verhalten bei Fehlern/Abbruch/UPS-Rückrichtung qualifizieren |
| Turbo-Audio / Abschnittswechsel | Neue gemeinsame Regeln angebunden | Neue gemeinsame Regeln angebunden | Hörtest und native SDL-Gerätewechsel |
| Schreibsperre pro ROM | Neu angebunden | Bestehend, gemeinsamer Helfer | Zweites Fenster auf beiden Systemen prüfen |
| Native PNG-Screenshots | Gemeinsamer Writer | Neu, F12/Tools | Native Linux-Bedienung/Ordnerzugang prüfen |
| FPS-/P95-Overlay | Vorhanden, gemeinsamer Rechner | Neu, F9/Tools | Native Linux-Darstellung prüfen |
| Eigene Menüs / Control Center | Neue Header-Panels, eigenes Control Center | Eigene SDL-Seiten | Alle Aktionen/Optionen gegenprüfen; nicht nur Optik |
| Bibliothek | Vault mit Favoriten, Spielzeit, Titeln, Filtern und Vorschaubildern | Jetzt ebenfalls Favoriten, Spielzeit, Titel, Systemfilter; vorhandene Suche/Recents/Pfadneuzuordnung | Verhalten und Metadatenmodelle abgleichen, nicht erneut implementieren |
| State-Komfort | Galerie, separate Fortsetzen-Datei, Lade-Rückgängig | Jetzt Galerie, separate Resume-Datei, verifizierte Vorschau und Undo | Gleiche Fehler-/Fortsetzen-Szenarien auf beiden Oberflächen prüfen |
| Profile pro Spiel | Anzeige/Audio/Input-Ausnahmen | Jetzt nullable Spielprofile mit globaler Vererbung | Feldumfang/Vererbung vergleichen; Textgröße/Performance bleiben Linux-global |
| Controller-Werkzeuge | Quick Deck, ROM-Auswahl, Bildschirmtastatur | Controller-Seiten, Profil-/Menübedienung | Gleichwertige Spiel-/Bibliotheksaktionen und Neutralstellung |
| Audio Inspector | Detailliert inkl. GBA Direct Sound/FIFO/Wave | Audio-Seite, Zähler, WAV-Aufnahme | Inspector-Snapshots in Linux darstellen |
| Diagnose | Aufzeichnungswahl, begrenzter Hintergrundschreiber, asynchroner ZIP-Export, Verdachtsbeobachter, manuelle Markierung | Berichte, Export, Aufzeichnungspräferenz, begrenzter Schreiber | Ereignisschema/Retention und Details der sicheren Freigabe bleiben unterschiedlich |
| Firmware / Anzeige | Neue Firmware Station mit Größenprüfung und atomarem Import, GPU/VSync/Integer-Optionen | Firmware-Import, Wayland-/SDL-Anzeige | Linux-Ersetzen/Fehlerpfade und Anzeigeoptionen einzeln abgleichen |
| Lokales GB-/GBC-/GBA-Link-Kabel | Experimentelles Local Link Lab: zwei Geräte derselben Hardwarefamilie, gemeinsame Pause, getrennte Saves, Keyboard-/Controller-Routing inkl. GBA L/R | Gemeinsame Core/Runtime verfügbar; noch keine native Link-Oberfläche | Linux-UI anbinden; auf beiden Systemen echte Link-Spiele, CGB-Fast-Timing und GBA-Multiplayer qualifizieren |
| Texteingabe / Barrierefreiheit | Native Windows-Eingabefelder in eigenen Dialogen | Neuer SDL-Editor mit IME/Markierung/Clipboard und optionalem GTK3/AT-SPI-Control-Center | Bedienung mit IME/Screenreader und Controller getrennt praktisch prüfen |
| Laden / Einstellungs-I/O | Bestehende Windows-Sitzungs- und Speicherabläufe | Neue abbrechbare ROM-Vorbereitung, Fokus-/Owner-Barriere, immutable Hintergrund-Snapshots | Windows-Reaktionsfähigkeit anhand dieser Verträge gesondert auditieren; kein pauschaler Gleichstand |
| Save-Import/-Export | Komfortimport offen | Roher Import/Export vorhanden | **Auf Nutzerwunsch zurückgestellt; private Saves nicht verändert** |
| Online Link | GB/GBC v1 + GBA Gen3 Entwicklungsprofil, privates Sitzungsarchiv und bewusste Übernahme | Dieselbe Runtime und GBA-Profilwahl, Archiv und zweistufige Übernahme in SDL | Original-Pokémon, native Linux-Oberfläche/Browser und WAN/Relay getrennt abnehmen; keine allgemeine ROM-Hack-Freigabe |

Arbeitsaufteilung: Hier entwickelt sich Windows weiter und übernimmt vorhandene
Linux-Funktionen. Der Linux-Mitentwickler übernimmt die zusätzlichen Windows-
Funktionen in seine native Oberfläche: noch Quick Deck, Inspector und Local Link Lab.
Bibliothek/Profile und Galerie/Fortsetzen hat er inzwischen ergänzt; diese werden
nicht erneut als fehlend geführt. Gemeinsame Dienste werden weiterhin nur einmal entwickelt.
Keine Linux-Funktion entfernen, nur weil Windows sie noch nicht besitzt.
Historische Handoffs sind keine aktuelle
Vollständigkeitsliste; diese Matrix muss bei jedem weiteren Paket mitgeführt werden.

### Prüfen und gemeinsam weiterarbeiten

**Aktueller GBA-Online-Folgestand:** Release-Build ohne Warnungen/Fehler;
**885 Testfälle, 844 bestanden, 41 ausgelassen, 0 Fehler** im Windows-Lösungslauf.
Native Ubuntu-Runtime: **346/346**, Core **232/232**; Desktop ohne Wayland-Sitzung:
**82 bestanden, 37 ausgelassen, 0 Fehler**. Ein Windows- und ein Ubuntu-Prozess
tauschten synthetische GBA-Befehle über echte Browser-WebRTC aus. Das ist kein
Original-Pokémon-, Zwei-PC- oder WAN-Nachweis. Ein sporadischer Timeout früherer
Speicherabschlussläufe bleibt trotz grünem Abschlusslauf ausdrücklich offen.
[Prüfbedingungen, Schutzmaßnahmen und offene Abnahmen](GBA_ONLINE_HANDOFF.md#prüfstand).

**Historischer vereinter GBA-/Linux-Abschlusslauf vor GBA Online:** Release-Build ohne Warnungen/
Fehler; **710 Testfälle, 674 bestanden, 36 ausgelassen, 0 Fehler**. Core 197/197,
Runtime 220/220, Windows 178 bestanden/1 Vordergrund-Skip, Desktop-Logik 79
bestanden/35 native Skips. Die Linux-Oberfläche des vereinten Stands braucht
weiterhin eine native Abnahme. [Details](GBA_LOCAL_LINK_HANDOFF.md#prüfung).
Die nachfolgenden kleineren Zahlen dokumentieren frühere Entwicklungsstände.

Letzter dokumentierter Abschlusslauf **vor Local Link Lab**, einschließlich Firmware-/Diagnose-Nachtrag:
**531 Tests, 523 bestanden, 8 ausgelassen, 0 Fehler**.
Build ohne Warnungen/Fehler, Windows-WASAPI-/Direct2D-Proben bestanden.
Die acht ausgelassenen Tests benötigen native Linux-/Wayland-/Unix-Bedingungen.
Runtime separat: **140/140**; Linux-Frontend-Suite unter Windows: **59 bestanden,
8 ausgelassen**. Die CI-Untergrenzen wurden entsprechend angehoben.
Diese Zahlen belegen den vorherigen Meilenstein, nicht die anschließend ergänzten
Link-Tests.

**Nachtrag Local Link Lab:** Release-Build ohne Warnungen/Fehler; abschließender
Lösungstest **591 Fälle, 582 bestanden, 9 ausgelassen, 0 Fehler**. Core 197/197,
Runtime 157/157, Windows 169 bestanden/1 ausgelassen, Desktop-Logik 59 bestanden/
8 ausgelassen. Neben den acht nativen Linux-Prüfungen verweigerte Windows dem
Tastatur-Testfenster den Vordergrund. Der vollständige Tastatur-bis-CPU-Nachweis
bleibt daher offen; der Eingabeschutz bleibt unverändert. Details und weitere
praktische Grenzen stehen in [Local Link Lab (DE/EN)](LOCAL_LINK_LAB.md).

Windows: Lösung bauen und Tests ausführen. Die opt-in Audio-Hardwareproben geben
Stille aus; sie ersetzen keinen Hörtest. Die Menü-Captures zeigen nur das
App-Rechteck, keine beliebigen Desktopbereiche. Native Linux-Tests müssen unter
Wayland laufen; ein erfolgreicher Windows-Build ist dafür kein Nachweis.

```powershell
dotnet build nanoboy.sln -c Release --no-restore
$env:AETHERBOY_HARDWARE_SMOKE = '1'
dotnet test --solution nanoboy.sln -c Release --no-build --no-restore
```

```bash
dotnet test --project tests/AetherBoy.CoreTests -c Release
dotnet test --project tests/AetherBoy.RuntimeTests -c Release
dotnet test --project tests/AetherBoy.DesktopTests -c Release
AETHERBOY_UI_TESTS=1 dotnet test --project tests/AetherBoy.DesktopTests -c Release
```

Manuell auf **beiden** Systemen: GB und GBA starten, Turbo mehrfach halten/lösen,
währenddessen pausieren/fortsetzen, ROM wechseln, F12 auslösen, PNG-Abmessungen
prüfen, F9 ein-/ausschalten und neu starten, zweites Fenster mit derselben ROM
versuchen. Windows zusätzlich alle vier Header-Panels samt Untermenüs mit Tastatur
und Controller prüfen. Keine fremden/privaten ROMs oder Spielstände committen.

## English

The target is **bidirectional feature parity**, not a second emulator or identical
OS internals. Full parity has **not** been reached. This local package starts from
`22a77ef`, keeps the existing Linux Patch Lab, and introduces:

- Shared bounded audio admission preserving queued fragments, with Windows audio
  enabled during turbo. Both outputs honor runtime session/generation tags and
  reject stale audio across turbo, pause, restore and ROM changes. No state-format
  change, no promise of pitch-preserving time stretching. Real-game listening
  verification of the reported repeating tone remains outstanding.
- Custom Windows in-window command panels replacing the four visible native
  dropdowns, retaining command/check/disabled states and nested navigation.
- Shared lifetime ROM-write ownership, now also enforced by Windows. Existing
  Linux ownership paths and migration remain intact. No private saves imported.
- One portable native PNG encoder/writer and one presentation-statistics
  implementation used by both frontends. Linux gains F12 screenshots, F9 metrics,
  Tools buttons, folder access and a persisted overlay preference. Newly reserved
  shortcuts migrate independently without resetting other input bindings.

The fetched Linux commit `367674f` now adds library metadata, state gallery/resume/
load-undo and inheriting per-game profiles. Those features no longer belong on
the missing-UI list. Quick Deck and the full Audio Inspector remain pending.
Its cancellable ROM loading, immutable background settings writes and new
IME/accessibility controls also provide contracts to audit on Windows.
Windows/Linux diagnostics, firmware and display options still
need a detailed behavioral alignment. Save transfer/import is explicitly deferred.
Share those services and their tests before writing separate UI implementations.

Windows now also has the experimental [Local Link Lab](LOCAL_LINK_LAB.md): two
GB/GBC or two GBA machines in one process, coordinated stepping and separate
player saves. The shared Core/Runtime is portable; Linux still needs its native
two-player interface. This is not network play or cycle-exact synchronization,
and no commercial-game link test or 100-game compatibility claim is established.
GB/GBC use instruction/base-dot stepping; GBA uses alternating CPU-cycle stepping
and separate normal/multiplayer serial modes. These are not interchangeable cables.

Verification commands above apply. Windows hardware smoke uses silence; it is not
a listening test. Native Wayland integration and physical controller/device
qualification must be performed on Linux. This GBA follow-up changes serial/core
internals and advances standalone device states to schema 6 while reading schema 5;
battery `.sav` formats remain unchanged. Linux native test dependencies now include
GTK/AT-SPI. No push or stable release is implied.

The Windows-only follow-up adds Firmware Station, a next-launch recording preference,
bounded background diagnostics and asynchronous manual export. See the linked DE/EN
handoff for policy precedence, privacy and remaining Linux integration details.
The Linux colleague owns frontend catch-up; Windows development continues here.

Last recorded verification **before Local Link Lab**, including the firmware/diagnostics follow-up:
531 total, 523 passed, 8 Linux-native/Unix-dependent skips,
zero failures; build clean, including Windows WASAPI/Direct2D smoke. Portable
Runtime: 140/140. CI minimum test counts now guard the new coverage as well.
The newer link tests require a fresh recorded run; the historical numbers above
must not be presented as their result.

The earlier combined GBA/Linux verification superseded the preceding historical figures:
**710 total, 674 passed, 36 skipped, no failures**, clean Release build.
Core 197, Runtime 220, Windows 178+1 skip, Desktop 79+35 native skips.
See [GBA handoff](GBA_LOCAL_LINK_HANDOFF.md) for coverage and real-game limitations.

The current GBA-online follow-up supersedes those counts: **885 total, 844 passed,
41 skipped, zero failed**, clean Release build. Native Ubuntu passed Runtime 346/346,
Core 232/232 and Desktop logic 82+37 skips without a Wayland session. Independent Windows
and Ubuntu owners exchanged synthetic GBA commands over real browser WebRTC on one PC.
Retail trading, two physical PCs and WAN remain unqualified. An intermittent shutdown
timeout in earlier aggregate runs remains under investigation despite the passing final run.
See [current GBA online handoff](GBA_ONLINE_HANDOFF.md#verification-and-outstanding-shutdown-investigation).
