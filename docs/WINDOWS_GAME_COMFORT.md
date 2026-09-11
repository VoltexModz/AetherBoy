# Windows: State-Galerie, Cartridge Vault und Spielprofile

Stand: 11. September 2026. Dieses Paket erweitert ausschließlich das Windows-Frontend.
Die gemeinsamen GB/GBC/GBA-Kerne, die Runtime und das Linux-Frontend wurden dafür
nicht verändert. Alle Daten bleiben auf dem jeweiligen PC.

## Deutsch

### 1. Speicherstände mit Vorschau und Fortsetzen

- **F6** oder **Control Center → Saves → State-Galerie** öffnet die sechs Karten:
  fünf manuelle Slots und einen separaten Fortsetzen-Slot.
- **F5 / F8** speichern/laden weiterhin den ausgewählten manuellen Slot. Jede Karte
  zeigt – sofern vorhanden – Spielbild, ROM-Titel, Datum und Sitzungsframe.
- **Fortsetzen** wird bei laufender Emulation ungefähr alle 60 Sekunden und beim
  regulären Beenden beziehungsweise Spielwechsel gesichert. Es überschreibt keinen
  der fünf manuellen Slots. Auch aus der Galerie lässt sich dieser Slot speichern.
- In der Bibliothek bedeutet **Neu starten** einen normalen ROM-Start mit dem
  Batterie-Spielstand. **Fortsetzen** lädt ausdrücklich den Fortsetzen-State.
  Doppelklick startet normal; es wird nicht stillschweigend ein State geladen.
- **Strg+F8** oder **Letztes Laden rückgängig** stellt den Zustand unmittelbar vor
  dem letzten erfolgreichen State-Laden wieder her. Ein Rückkehrpunkt bleibt im
  Arbeitsspeicher; einmaliges Rückgängig, kein mehrfaches Redo. Beim Spielwechsel
  oder Beenden geht dieser Punkt verloren. Ein fehlgeschlagener Ladeversuch ersetzt
  ihn nicht. Pause bleibt nach einer Speicher-/Ladeaktion erhalten.

Die bisherigen rohen `game.ss1` bis `game.ss5` bleiben verwendbar. Der neue Slot
heißt `game.resume`. Das Vorschau-JSON ist eine separate Datei mit SHA-256 der
State-Bytes: Eine nicht passende oder beschädigte Vorschau wird nicht angezeigt.
Alte States ohne Bilder bleiben ladbar, soweit der Kern ihr Format unterstützt.
Beim Ersetzen bleibt die vorherige rohe Datei als `.bak` erhalten. Diese Sicherung
wird nicht ungefragt geladen. State-Datei und Vorschau sind einzeln atomar ersetzt,
nicht gemeinsam als Dateipaar; die Prüfsumme verhindert eine falsche Zuordnung.

Das Bild stammt vom zuletzt veröffentlichten Emulationsframe. Bei Frameskip kann
es etwas älter als der gespeicherte Kernzustand sein. Die Framezahl ist ein
Sitzungszähler, keine Spielzeit und kein persistenter CPU-Zähler.

**Grenzen:** Bei Absturz, Stromausfall, schreibgeschütztem oder vollem Datenträger
kann der neueste Fortschritt fehlen. Autosave ist kein Ersatz für das Speichern
im Spiel. Save-State-Kompatibilität über spätere Kern-/Formatänderungen wird nicht
garantiert. Batterie-Saves und deren bestehende Backup-Verwaltung bleiben getrennt.

### 2. Cartridge Vault

**Open ROM** öffnet die lokale Bibliothek: Suche nach Titel/Dateiname, Filter für
GB/GBC/GBA, Favoriten, Sortierung nach Titel, letzter Sitzung oder Spielzeit sowie
Kachel- und Listenansicht. **ROM-Ordner öffnen** bleibt direkt erreichbar.

Die Vorschau entsteht aus eigenen Save-State-Aufnahmen, nicht aus heruntergeladenen
Covern. Ohne Bild erscheint das Systemkürzel. Favoriten sind nach dem Import in die
verwaltete Bibliothek verfügbar. Gleiche ROM-Bytes teilen Metadaten über ihren
SHA-256; ein anders gepatchter ROM-Inhalt erhält einen eigenen Datensatz.

Spielzeit zählt aktive reale Sitzungszeit, nicht die durch Turbo beschleunigte
Emulationszeit. Pausen und längere unterbrochene UI-Abfragen zählen nicht mit.
Metadaten werden ungefähr alle 30 Sekunden und beim regulären Beenden gespeichert;
bei einem harten Abbruch kann das letzte noch ungeschriebene Intervall fehlen.
„Zuletzt gespielt“ bezeichnet den letzten gespeicherten Sitzungszeitpunkt.

### 3. Spielbezogene Einstellungen

1. Spiel starten und **Control Center → System → Profil aktivieren** wählen.
2. Anzeige, Audio oder Belegung wie gewohnt einstellen.
3. Nur tatsächlich geänderte Werte werden für diese ROM überschrieben. Nicht
   überschriebene Werte erben weiterhin die globalen Einstellungen.

Ein Profil umfasst Palette, Filter, Skalierung, GPU/VSync/Integer Scaling,
Frameskip, Audio/Lautstärke/Pufferziel/Kanäle sowie Tastatur-/Controllerbelegung.
**Boot-ROM und manueller Save-Slot bleiben global.** Die feste Ausgaberate ist
ebenfalls keine Profiloption. Ausschalten deaktiviert das Profil, erhält aber seine
Werte. **Überschreibungen zurücksetzen** entfernt nur dessen Ausnahmen. Der separate
globale Einstellungsreset setzt zusätzlich die globalen Einstellungen zurück.

### Lokale Ablage und Datenschutz

Unter `%LOCALAPPDATA%\AetherBoy`:

| Ordner | Inhalt dieses Pakets |
| --- | --- |
| `Library/<ROM-SHA256>.json` | Titel, System, Favorit, Spielzeit, letzte Sitzung, optionales PNG |
| `Settings/Profiles/<ROM-SHA256>.json` | Aktivierungsstatus und Einstellungsüberschreibungen |
| `States/<ROM-SHA256>/` | Manuelle States, `game.resume`, Vorschau-JSON und vorherige Dateien |

Die PNGs enthalten lokale Spielbilder. Sie gehören **nicht** zum Development-
Berichtsexport; dessen bestehende Positivliste umfasst nur README und Sitzungslog.
Es gibt keinen Upload. JSON-Metadaten verwenden Größenlimits und einen lesbaren
`.bak`-Rückfallpfad; vollständig unlesbare Bibliotheksdaten werden beim Aktualisieren
nicht stillschweigend durch leere Daten ersetzt.

### Verifikation

`WindowsGameDataTests` prüft Slot-Trennung, Rohdatei-Backup, fehlgeschlagene
Schreibvorgänge, Vorschau-Prüfsummen, Legacy-States, beschädigtes JSON, Profil-
Vererbung, Isolation und Schreibfehler, PNG-Grenzen, Bibliotheksfilter/Favoriten sowie gezeichnete
Kacheln und sechs State-Karten. Mit einem selbst erzeugten GB-Testprogramm werden
Speichern/Laden, Rückgängig und Fortsetzen bis zum byteidentischen Kernzustand
geprüft. UI-Bilder zeigen ausschließlich synthetische Testdaten.

Die Tests verwenden eigene temporäre Datenordner. Diese automatisierte Prüfung
ersetzt keine GB/GBC/GBA-Langzeit-Spieltests und keine Prüfung auf weiteren
Windows-Grafik-/Audiogeräten oder DPI-Konfigurationen.

Gesamtlauf am 11. September 2026: **378 Tests, 374 bestanden, 0 fehlgeschlagen,
4 übersprungen** (drei optionale Windows-Hardwaretests und ein nativer Wayland-UI-Test).
Aufruf: `dotnet test --solution nanoboy.sln -c Release --no-restore --verbosity quiet`.

## English

### Usage

- **F6 / Control Center → Saves → State-Galerie** opens five manual state cards and
  one separate resume card, with local gameplay previews, title, timestamp and
  session-frame count. **F5/F8** still operate on the selected manual slot.
- Resume saves approximately every 60 seconds while running and on normal exit
  or game switch. It never replaces a manual slot. **Fortsetzen** in the library
  explicitly loads it; **Neu starten** and double-click boot normally with the
  battery save. **Ctrl+F8** undoes the latest successful state load once within the
  current session. Failed loads retain the previous undo point; closing/switching
  discards it. Save/load preserves the prior pause status.
- The local **Cartridge Vault** offers title/filename search, GB/GBC/GBA filters,
  favorites, last-played/title/playtime sorting and tile/list views. Images come
  from your state captures, not downloaded covers. **ROM-Ordner öffnen** opens
  the managed ROM folder. Playtime measures active wall time, excludes pauses and
  long UI gaps, and is flushed about every 30 seconds plus normal exit.
- Start a game, then enable its profile under **Control Center → System**. Only
  changed display, audio and input preferences become per-ROM overrides; other
  values continue inheriting global settings. Boot firmware and manual slot
  selection remain global. Disabling retains overrides; resetting overrides clears
  them. The separate global-reset action also resets global preferences.

### Storage and safety

Metadata lives under `%LOCALAPPDATA%\AetherBoy\Library`, per-ROM profiles under
`Settings\Profiles`, and states under `States\<ROM-SHA256>`. Identical ROM content
shares an identity; patched content gets a different identity. Existing raw
`game.ss1`–`game.ss5` files remain supported; `game.resume` is separate. Each preview
JSON includes the state SHA-256. Mismatched/corrupt previews are hidden, and legacy
states do not require images. The image is the latest published frame and may lag
the captured state when skipping presentation frames. The frame count is a session
counter, not persistent CPU state.

Writes replace individual files atomically, retaining previous raw states as
`.bak`; a two-file preview/state pair is not one atomic transaction. Checksums
prevent incorrect associations after an interrupted write. Raw backups are not
automatically restored. JSON recovery preserves a readable backup when repairing
a corrupt primary file. Existing battery-save safety remains independent.

All data, including gameplay PNGs, stays local. The diagnostic ZIP allowlist still
exports only its README and session log, never these screenshots, ROMs or states.
No upload is performed. Power loss, crashes and disk failures can lose the latest
unsaved interval. Autosave does not replace in-game saving. Future core/state-format
compatibility is not guaranteed.

### Verification limits

Windows tests cover slot isolation, prior-state backups, interrupted writes,
checksums, legacy states, JSON recovery, profile inheritance/isolation, PNG limits,
library filters/favorites and actual tile rendering. A generated GB program checks
save/load, undo and cross-session resume against identical core-state bytes. Tests
use isolated temporary storage and synthetic UI images. They are not a GB/GBC/GBA
long-play certification or validation of every Windows device/DPI configuration.

This feature package changes neither shared cores/runtime nor the Linux frontend.

Full-suite result on 11 September 2026: **378 tests, 374 passed, 0 failed, 4 skipped**
(three opt-in Windows hardware tests and one native Wayland UI test).
