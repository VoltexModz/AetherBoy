# Patch Lab, Sitzungsbeobachtung und GBA Audio Inspector

## Deutsch

Windows-Ausbau 8–10, September 2026. Punkt 7 (Spielstand-Import/-Export) ist nicht
Teil dieses Pakets. Ein gemeinsames Projekt, kein zusätzlicher Emulator.

### 8. Patch Lab

**Spielbibliothek → PATCH LAB · IPS / BPS / UPS** öffnen, eigene Basis-ROM und lokalen
Patch wählen, Bibliothekstitel vergeben, **PATCHEN & IMPORTIEREN** drücken.
Nach „Fertig“ ist das Ergebnis in der Bibliothek ausgewählt. Der Import startet
kein Spiel und unterbricht eine laufende Sitzung nicht.

- Klassische IPS-Patches: Literal, RLE, optionaler Größenabschluss.
  BPS1: alle vier Befehle, relative Rücksprünge und überlappende TargetCopy-Blöcke.
  UPS1: relative XOR-Blöcke, Größenänderungen und ausdrücklich gewähltes Rückpatchen.
- BPS und UPS prüfen Basisgröße sowie Basis-, Patch- und Ergebnis-CRC32. Keine Option zum
  Ignorieren der Prüfung. Andere Sprachversionen/Revisionen können abgelehnt werden.
- IPS besitzt diese Prüfsummen nicht. Ein technisch anwendbarer IPS-Patch ist
  **kein Nachweis der richtigen Basis-ROM oder der Spielbarkeit**.
- Maximal 32 MiB ROM/Ergebnis und 64 MiB Patch. Keine ZIP-/7z-Entpackung,
  kein IPS32, keine Patch-Erstellung und keine automatischen Downloads.
- Originale bleiben unverändert. Ergebnis unter
  `%LOCALAPPDATA%\AetherBoy\Roms\<Ergebnis-SHA256>\`; bereinigter Dateiname.
  Der eigene Bibliothekstitel bleibt auch beim Spielen erhalten.
- Saves, States und Profile folgen der Ergebnis-Identität. Keine Übernahme von
  Basis-Spielständen. Identische bekannte Ergebnisse werden wiederverwendet und
  behalten ihre vorhandenen Spielstände.
- `patch-<Patch-SHA256>.json` im Ergebnisordner enthält Format, Prüfergebnis, die
  Rückpatch-Richtung (`reversed`, bei älteren Einträgen nicht vorhanden) und
  SHA256 von Basis/Patch/Ergebnis, keine Originalpfade oder Patch-Metadaten.
  Scheitert diese Zusatzablage oder der Titel, wird der bereits erfolgte ROM-Import
  mit einer Metadatenwarnung gemeldet.
- Patch-Dateiauswahl: Windows-Dialog. Die übrige Oberfläche verwendet die
  bestehende Aether-/Controller-Navigation.

#### UPS anwenden und rückgängig machen

Für einen normalen UPS-Hack: passende **unveränderte Basis-ROM**, `.ups`-Datei und
Titel wählen; **UPS rückgängig machen** ausgeschaltet lassen. Die ROM-Endung
bleibt `.gb`, `.gbc` oder `.gba`; ZIP-Dateien vorher selbst entpacken.

Zum Rückpatchen: die **gepatchte ROM** als Basis und **denselben UPS-Patch** wählen,
**UPS rückgängig machen: gepatchte ROM → Original** einschalten und
**RÜCKPATCHEN & IMPORTIEREN** drücken. Auch dabei wird keine Eingabedatei überschrieben.
Das wiederhergestellte Original hat exakt seine ursprüngliche Länge, auch wenn der
Hack größer war. Ist es schon in der Bibliothek, bleiben sein Name, Favoriten,
Spielzeit, Profile, Saves und States erhalten. Hack-Spielstände werden nicht übertragen.

Bei falscher Richtung weist der Dialog auf die erforderliche Option hin. Eine
unpassende Revision/Sprachfassung meldet erwartete und tatsächliche Größe/CRC32.
Beschädigte Patches werden abgelehnt; die Auswahl bleibt korrigierbar. Ein Patch,
der überhaupt keine ROM-Bytes oder Größe verändert, erzeugt keinen Bibliothekseintrag.
CRC32 prüft Dateikonsistenz, nicht Vertrauenswürdigkeit oder Spielkompatibilität.

### 9. Lokale Sitzungsbeobachtung

Im normalen Development-Build liest ein Beobachter außerhalb des UI-Threads
einmal pro Sekunde veröffentlichte Runtime-Snapshots und das letzte Spielbild.
Er verändert keinen emulierten Speicher. Mögliche **Verdachtshinweise**:

- UI ohne Lebenszeichen seit 10 Sekunden.
- Startzustand seit 30 Sekunden.
- Keine neuen emulierten Frames seit 10 Sekunden.
- Emulation läuft, aber keine neuen Videoframes seit 10 Sekunden.
- Videoframes laufen, aber Windows präsentiert seit 10 Sekunden kein neues Bild.
- Dasselbe vollständig einfarbige Bild seit 20 Sekunden.

Pausen, Speicheroperationen, Quick Deck, Minimierung und fehlender Hauptfensterfokus
setzen die Verdachtsfenster zurück. Beobachtungslücken über 5 Sekunden, etwa durch
Standby oder Debugger, beginnen neu. Ein nicht-einfarbiges Standbild gilt nicht
allein als Fehler. Absichtlich einfarbige Szenen können trotzdem einen falschen
Verdacht erzeugen. Pro anhaltender Bedingung gibt es einen Eintrag; nach Erholung
kann sie erneut gemeldet werden. Keine automatische Wiederherstellung/Neustarts.
Ein Prozess- oder Betriebssystemabsturz kann den Beobachter ebenfalls beenden;
er ist kein externer Crash-Dumper.

**Quick Deck → PROBLEM MARKIEREN** oder **Control Center → Diagnostics →
PROBLEM MARKIEREN** setzt einen lokalen Zeitstempel mit Zählerständen und letzter
beobachteter aktiver Spielsituation. Doppelklicks werden begrenzt. Der letzte
Verdacht und Aktionsstatus stehen auf der Diagnostics-Seite.

Ablage bleibt `%LOCALAPPDATA%\AetherBoy\development\Sessions\`. Protokolliert
werden Zustände, Zähler und das Merkmal „einfarbig“, keine Bildpixel, ROM-/BIOS-
Bytes, Saves, Eingabeaufzeichnung oder Audiosamples. Der manuelle ZIP-Export bleibt
auf `README.txt` und `session.jsonl` beschränkt. Kein Upload, keine Tester-Sonderversion.

### 10. GBA Audio Inspector

**Control Center → Audio → Audio Inspector** funktioniert jetzt auch für GBA.
Zusätzlich zu vier PSG-Kanälen: Direct Sound A/B mit Samplewert nach Kanalpegel,
FIFO-Füllstand, Timerwahl, 50/100-%-Pegel, L/R-Routing und Hardware-Masterfreigabe.
Das sind Momentaufnahmen, keine vollständigen DMA-Traces oder Frequenzanalysen.

GBA-Wave-RAM wird in 64 Vier-Bit-Samples beider Bänke aufgeteilt; GB/GBC behalten
32 Samples. Die Wave-Anzeige berücksichtigt GBA 75 %. Die Mixzeile zeigt L/R-
Peaks des letzten Audioblocks vor Windows-Lautstärke; nach 500 ms ohne neue Daten
werden die Werte ausgeblendet.

**Audio aufnehmen (.wav)** öffnet den Speicherdialog, standardmäßig im AppData-
Unterordner `Recordings`. GBA-Aufnahmen: Stereo bei 65.536 Hz. Erneutes Drücken
stoppt die Aufnahme und finalisiert den Header. Spielwechsel, Schließen und
Schreibfehler nutzen den bestehenden Recorder-Lebenszyklus. Aufnahme benötigt
laufende Runtime-Audiodaten. WAVs werden nicht automatisch in Berichte übernommen.
Keine getrennten Kanal-Stems.

### Referenzen und Linux-Übergabe

Der bereitgestellte mGBA-Quellstand wurde hinsichtlich Funktionsliste und IPS-
Verarbeitung betrachtet. Für dieses Paket wurde kein mGBA-Kern und keine mGBA-
Implementierung übernommen. Der begrenzte C#-Parser ist eigenständig; BPS folgt
der [öffentlichen Formatspezifikation von byuu](https://github.com/Alcaro/Flips/blob/master/bps_spec.md).
UPS verwendet denselben geprüften Varint-Leser, begrenzte XOR-Bereiche und CRC32,
entsprechend der [UPS-Formatbeschreibung](https://github.com/btimofeev/UniPatcher/wiki/UPS).
Keine zusätzliche Patcher-Bibliothek, keine unsicheren Zeiger.

Linux-Frontend unverändert. Wiederverwendbar: `RomPatcher.Apply(source, patch,
reverseUps: false)` in
`AetherBoy.Runtime.Cartridges`, optionale `DirectSoundA/B` in `AudioSnapshot`
und `WaveChannelSnapshot.OutputGain`. Die GBA-Wellenform im Snapshot enthält
jetzt **64 entpackte Samples**, nicht 32 gepackte Bytes. Emulationsverhalten,
State-/Batterieformate und Mono-/Stereo-Transportvertrag ändern sich durch dieses
Paket nicht. Die GB/GBC-State-Warnung aus [Paket 4–6](WINDOWS_PLAYER_TOOLS_STEREO.md)
bleibt relevant.

### Prüfung und Grenzen

Tests: IPS/BPS/UPS-Befehle/Prüfsummen, relative Rücksprünge/Überlappung, Größenlimits,
abgeschnittene und zufällige Eingaben, unveränderte Originale, getrennte Saves,
UI-Import, Diagnosefenster/Entwarnung/Unterdrückung, Bericht-Datenschutz, GBA-DMA-
Snapshots/Wave-Bänke und Stereo-WAV durch den tatsächlichen Inspector.
UI-Screenshots wurden visuell kontrolliert.
UPS zusätzlich: Vorwärts/Rückwärts, exakte Größen bei Wachstum/Schrumpfung,
XOR-Endmarkierungen, virtuelle Null-Enden, falsche Richtung, Integer-Überlauf,
300 deterministische zufällige gültige Roundtrips und 300 beschädigte Bodies.
Windows: GB/GBC/GBA-Import, Erhalt bestehender Originaldaten und Fehlerbehandlung
für alle drei Formate im tatsächlichen Dialog.

Vollsuite einschließlich UPS-Ausbau mit Windows-Hardwareproben: **440 Tests, 439 bestanden, 0 fehlgeschlagen,
1 nativer Wayland-UI-Test übersprungen**. Nur synthetische ROMs und Patches.
Reale Hacks, Hörtests, lange Spielsitzungen, weitere DPI-/Controller-Kombinationen
und native Linux-UI bleiben praktische Prüfungen.

## English

### Usage

- **Library → PATCH LAB · IPS / BPS / UPS**: select your own base ROM and local patch,
  choose a title, apply and import. Originals remain unchanged. Content-addressed
  results get separate saves; identical existing results keep their saves.
  Import does not launch a game. Custom titles survive subsequent gameplay.
- Classic IPS supports literal/RLE records and optional final output size.
  BPS1 supports all commands, signed relative offsets and overlapping target copies.
  UPS1 supports relative XOR records, size changes and explicit undo.
  BPS/UPS require correct source size and source/patch/target CRC32. IPS cannot verify
  the base. Limits: 32 MiB ROM/output, 64 MiB patch. No archives, IPS32, patch
  creation, downloads or checksum bypass. Patch selection uses a Windows dialog.
- **UPS undo:** select the patched ROM and the same UPS patch, enable **UPS
  rückgängig machen**, then **RÜCKPATCHEN & IMPORTIEREN**. Normal patching leaves
  this option off. The direction is never reversed automatically. Output length
  is exact, including shrinking back to the original. An existing original keeps
  its title, favorites, playtime, profiles, saves and states; hack saves are never
  transferred. Neither input file is overwritten. No-change patches are rejected.
- Wrong direction and invalid patches produce a visible, recoverable error.
  Wrong source revisions report expected/actual size and CRC32. CRCs establish
  consistency, not trustworthiness or gameplay compatibility.
- Provenance stores hashes, format and direction (`reversed`, absent in older
  entries), not source paths or embedded patch metadata.
  Optional metadata failure is reported separately from a successful ROM import.
- **Quick Deck or Control Center → Diagnostics → PROBLEM MARKIEREN** marks an
  issue locally. A background observer flags suspected UI/emulation/video/
  presentation stalls, slow startup and persistent uniform frames. Thresholds:
  10 s stalls, 30 s startup, 20 s uniform output. Pauses, host operations, menus,
  focus loss and observation gaps reset detection. One event per episode;
  no automatic recovery. Uniform scenes can still produce false suspicions.
  A process-wide failure can terminate the observer too.
- **Control Center → Audio → Audio Inspector** now supports GBA: PSG plus Direct
  Sound A/B sample values, FIFO occupancy, timer, gain, routing and master enable.
  Snapshots, not cycle traces. Both wave banks produce 64 unpacked four-bit samples;
  GB/GBC retain 32. Mix peaks represent the latest audio block before host volume.
- Record stereo WAV at GBA's native 65,536 Hz. Stop finalizes the header; the
  existing lifecycle handles game changes and close/write failures. Runtime audio
  must be running. Default folder: `%LOCALAPPDATA%\AetherBoy\Recordings`. No stems.

### Privacy, architecture and verification

Development logs remain local. Health events contain counters/state and a uniform-
frame flag, not image pixels, audio, input recordings, ROM paths or save contents.
Manual ZIP export still only includes README and session JSONL. No uploads.

The supplied mGBA README/IPS implementation was reviewed as reference; no mGBA
implementation was imported in this package. BPS follows the
[byuu specification](https://github.com/Alcaro/Flips/blob/master/bps_spec.md).
UPS follows the [format description](https://github.com/btimofeev/UniPatcher/wiki/UPS)
using the same bounded varint reader and independent XOR application; no additional
patcher library or unsafe pointers. The runtime's optional `reverseUps` argument
defaults to false; existing callers retain forward-only behaviour.
Shared runtime patching and optional Direct Sound snapshots are available for
future Linux UI integration; the Linux frontend is unchanged. GBA wave snapshots
now hold 64 unpacked samples and wave output gain is explicit. Hardware behaviour,
save/state formats and audio transport are unchanged by this package.

The full suite including UPS passed **439/440** tests; only the native Wayland UI test was skipped.
Coverage includes UPS roundtrips/growth/shrink/overflow, all-format UI errors,
existing-original data preservation, malformed patches, import safety, heuristic suppression/recovery,
privacy, GBA snapshots and recording through the Windows inspector. Synthetic
screenshots/hardware probes are not commercial-game compatibility or listening tests.
Real patches, gameplay and more DPI/controller combinations still need testing.
Save import/export (item 7) is outside this package.
