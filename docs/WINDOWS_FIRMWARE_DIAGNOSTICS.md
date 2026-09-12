# Windows: Firmware Station und lokale Diagnose / local diagnostics

Stand / Date: **2026-09-12**, lokaler Entwicklungsstand nach dem ersten
[Funktionsabgleich](PLATFORM_PARITY.md). Kein neuer Release, Commit oder Push.

## Deutsch

### Aufteilung mit der Linux-Entwicklung

Wir entwickeln hier Windows weiter und übernehmen vorhandene Linux-Funktionen.
Zusätzliche Windows-Funktionen werden für den Linux-Mitentwickler dokumentiert;
seine native Oberfläche und seine Hardwaretests bleiben bei ihm. Dieser Nachtrag
ändert weder die Linux-Oberfläche noch die Emulator-Kerne oder Save-Formate.
Die bereits zuvor vorhandenen lokalen Linux-Änderungen bleiben erhalten.

### Firmware Station

Einstieg: **TOOLS → Firmware Station** oder **Control Center → SYSTEM →
FIRMWARE VERWALTEN**. Die eigene Aether-Oberfläche unterstützt Tastatur und
Controller. Importiert werden ausschließlich vom Benutzer ausgewählte Dateien:

| System | Erwartete Größe | Verwaltete Windows-Datei |
| --- | ---: | --- |
| Game Boy / DMG | 256 Bytes | `%LOCALAPPDATA%/AetherBoy/Firmware/dmg_boot.bin` |
| Game Boy Color / CGB | 2.304 Bytes | `%LOCALAPPDATA%/AetherBoy/Firmware/gbc_boot.bin` |
| Game Boy Advance / GBA | 16.384 Bytes | `%LOCALAPPDATA%/AetherBoy/Firmware/gba_bios.bin` |

Die Größe prüft das Format, **nicht Echtheit, Rechte oder Funktionsfähigkeit**.
Keine Downloads oder mitgelieferten BIOS-Dateien. CGB-Dateien mit 2.048 Bytes
werden nicht akzeptiert. Der ausgewählte Dateiname ist beliebig; ein aus Linux
stammendes `cgb_boot.bin` wird beim Windows-Import als `gbc_boot.bin` abgelegt.

Der Import liest zunächst eine begrenzte Momentaufnahme. Erst nach erfolgreicher
Prüfung und gegebenenfalls ausdrücklicher Ersetzen-Bestätigung wird eine temporäre
Datei geschrieben, auf den Datenträger gespült und atomar aktiviert. Abbrechen,
ungültige Größen oder fehlgeschlagenes Ersetzen erhalten die vorhandenen Bytes.
Die Originaldatei bleibt unangetastet. Nach erfolgreichem Import wird die
automatische externe Firmware-Auswahl eingeschaltet.

Beim Laden bleibt die Suchreihenfolge **AppData → Arbeitsordner → Programmordner**.
Eine vorhandene, aber ungültige oder gesperrte höher priorisierte Datei wird nicht
durch irgendeine andere externe Firmware ersetzt. Stattdessen verwendet Windows
den integrierten Startpfad und zeigt eine Warnung in der Statuszeile. Die Station
unterscheidet Größe geprüft, fehlend, ungültig und nicht lesbar.

**ROM erneut öffnen:** Import und Boot-Auswahl verändern keine laufende Maschine.
Ein einfacher Emulator-Reset liest die Firmwaredatei nicht neu ein.

### Diagnose: aktuelle Sitzung und nächster Start getrennt

**Control Center → DIAGNOSTICS** zeigt die tatsächliche Aufzeichnung, ihren
Puffer-/Fehlerzustand und eine getrennte gespeicherte Auswahl für den nächsten
Programmstart. Der Schalter startet oder beendet die aktuelle Aufnahme nicht.

- Ohne eigene Auswahl: Development zeichnet auf, Stable standardmäßig nicht.
- Die globale Einstellung `DiagnosticsRecording` liegt in `Settings/settings.json`:
  leer = Build-Standard, `True` / `False` = ausdrückliche Wahl. Kein Spielprofil.
- `--tester-mode` aktiviert nur den ausdrücklich so gestarteten Prozess, auch bei
  gespeicherter Auswahl AUS. `AETHERBOY_DIAGNOSTICS=0` verhindert die Aufnahme immer.
- Nicht lesbare/beschädigte Einstellungen ohne lesbare Sicherung führen zu AUS,
  nicht zu einer stillen Rückkehr zum Development-Standard.
- Der allgemeine Emulator-Einstellungsreset erhält diese Datenschutzwahl.
- Minimale lokale Crashlogs bleiben unabhängig von der Sitzungsaufzeichnung aktiv.
  Vorhandene Berichte werden weder beim Umschalten noch beim Reset gelöscht.

Sitzungsprotokolle: `%LOCALAPPDATA%/AetherBoy/development/Sessions`.
**OPEN TEST FOLDER** bleibt auch ohne aktuelle Aufnahme erreichbar. Der Export
schreibt nur nach Dateiauswahl ein ZIP; er arbeitet im Hintergrund und verhindert
gleichzeitige Exporte. Es gibt keinen automatischen Upload.

Die Aufzeichnung verwendet einen Hintergrundschreiber mit maximal **256 wartenden
Ereignissen und 8 MiB pro Sitzung**. Bei vollem Puffer werden neue Ereignisse gezählt
und verworfen. Bei erreichtem Dateilimit stoppt die Aufnahme; bisherige Daten bleiben
exportierbar. Eine separate Export-Barriere kann nicht durch Pufferüberlast verloren
gehen. Warten auf den Schreiber ist beim Export auf fünf, beim Beenden auf zwei
Sekunden begrenzt; das sind keine Zeitgarantien für sämtliche Datenträgeroperationen.
Ältere Sitzungen werden nicht automatisch gelöscht; die Gesamtmenge vieler Berichte
ist deshalb nicht begrenzt.

ZIP-Inhalt bleibt `README.txt` + `session.jsonl`, Schema 1. Enthalten sind Build,
Plattform, Controllerbezeichnung/-IDs, ROM-Hash/Modell/Größen und strukturierte
Laufzeitereignisse. Controller- und Audiogerätenamen können benutzerdefiniert sein.
Keine ROM- oder Save-Inhalte, ROM-Dateipfade oder freien Exception-Texte.
ROM-Titel werden nicht mehr protokolliert, weil der GBA-Kern bei leerem Titel auf
den Dateinamen zurückfallen kann. Externe Auswerter dürfen `details.title` daher
nicht voraussetzen. Die kopierbare Live-Zusammenfassung enthält ebenfalls keine
ROM-Titel/-Pfade oder freien Aktions-/Kernmeldungen; sie behält strukturierte
Kennungen und Gerätestatus. Eigene Scroll-Schaltflächen und Textauswahl bleiben
bei Live-Aktualisierungen nutzbar.

### Geprüft und noch offen

Gesamtlauf: **531 Tests, 523 bestanden, 8 Linux-native/Unix-bedingte Auslassungen,
0 Fehler**. Build: 0 Warnungen, 0 Fehler. Neue Abdeckung: 43 Fälle für Import,
Abbruch/Ersetzen, Lesesperren, Boot-Lader, Einstellungsprioritäten, Datenschutzreset,
Puffergrenzen, Export, Diagnose-UI und Firmware-Bedienung. App-Screenshots geprüft.
Tests verwenden synthetische Firmware/ROMs und isolierte Verzeichnisse; keine
privaten Spielstände wurden importiert oder geändert.

```powershell
dotnet build nanoboy.sln -c Release --no-restore
$env:AETHERBOY_HARDWARE_SMOKE = '1'
$env:AETHERBOY_SMOKE_SCREENSHOTS = "$PWD/artifacts/parity-review"
dotnet test --solution nanoboy.sln -c Release --no-build --no-restore --minimum-expected-tests 531
```

Echte BIOS-Boots und der gemeldete Turbo-Ton benötigen weiterhin einen Spiel-/Hörtest.
Die Hardwareproben geben Stille aus. Native Linux-Tests wurden hier nicht durchgeführt.

Für den Kollegen: Linux hat Firmwareimport und Aufzeichnungswahl bereits. Dort
lohnt der Abgleich von Ersetzen-Bestätigung/atomarem Import, nicht verwerfbarer
Export-Barriere, Beibehaltung der Diagnosewahl beim Reset und pfadfreier Freigabe.
Ereignisschemata und Aufbewahrung bleiben unterschiedlich: Linux behält derzeit
20 Sitzungsdateien, Windows löscht keine. Noch offene Linux-Komfortfunktionen
stehen in der [Abgleichsmatrix](PLATFORM_PARITY.md); Save-Import bleibt zurückgestellt.

## English

This Windows-only follow-up adds a custom **Firmware Station** through TOOLS or
Control Center → SYSTEM. It imports user-selected DMG (256 B), CGB (2304 B) and
GBA (16384 B) firmware, reports missing/invalid/unreadable files and requires
explicit confirmation before replacing managed firmware. Validation and an
immutable snapshot precede atomic activation. No firmware is supplied or downloaded;
size validation does not establish authenticity or compatibility. Windows keeps
`gbc_boot.bin`; Linux's `cgb_boot.bin` can be selected and imported under that name.
Existing load precedence is managed storage, working directory, executable directory.
Invalid external firmware safely falls back to built-in boot, with visible feedback.
**Reopen the ROM** to apply changes; a machine reset does not reread firmware.

DIAGNOSTICS now separates actual recording from the saved **next application start**
preference. Development defaults on, stable defaults off. Explicit `--tester-mode`
overrides a saved off choice for that process; `AETHERBOY_DIAGNOSTICS=0` always
disables recording. Unreadable preferences fail closed. General settings reset
preserves this global privacy choice; minimal local crash logs remain independent.
The report folder remains accessible without active recording.

The Windows recorder now has a background writer, a 256-event queue and an 8-MiB
session limit. Overflow drops incoming events with accounting; reaching the size
limit stops recording but preserves export. Export uses a non-droppable checkpoint
and immutable snapshot, runs off the UI thread and rejects overlapping UI exports.
Writer checkpoint/shutdown waits are bounded; arbitrary filesystem operations are
not guaranteed to finish within those bounds. Previous sessions are never deleted,
so total storage across sessions remains unbounded.

The manual ZIP still contains only `README.txt` and `session.jsonl` (schema 1),
including drop/limit information. ROM hashes/model/sizes replace titles: a blank GBA
header can otherwise expose a filename. Device names/IDs and build/platform details
are disclosed; no ROM/save contents, ROM paths, free-form exception text or automatic
uploads. Copyable diagnostics also omit ROM titles/paths and free-form action/core
messages. Consumers must not require the former `details.title` field.

Verification: **531 total, 523 passed, 8 native Linux/Unix-dependent skips, zero
failures; build clean**. 43 new cases, synthetic data, isolated storage and inspected
Windows UI captures. Real BIOS boots and the reported turbo-audio symptom still
need player verification. No native Linux execution, commit or push is implied.

The Linux colleague owns Linux UI catch-up. This follow-up changes no Linux frontend,
core or save formats; earlier local parity changes remain intact. Linux already has
firmware import/recording preferences; compare replacement safety, export barriers,
privacy-preserving reset and report disclosure. Event schemas and retention are not
identical (Linux currently keeps 20 sessions, Windows deletes none). See the shared
matrix for library/profile/gallery/Quick Deck/Inspector gaps; save transfer is deferred.
