# Linux upstream integration review / Integrationsprüfung

Stand / reviewed: **12 September 2026**. This is an integration audit, not a new
Linux hardware or gameplay qualification.

**Git-Abschluss / publication follow-up:** Nach der GBA-Online-Ergebnisübergabe hat der
Nutzer Commit und Push auf `development` ausdrücklich freigegeben. Der erneute Abruf
am 12.09.2026 bestätigte unverändert `367674f`; dieser Linux-Commit bleibt als Vorfahr
in der gemeinsamen Historie erhalten. Die nachfolgenden Angaben zu unverändertem HEAD
und fehlender Git-Freigabe beschreiben den früheren Audit-Zeitpunkt, nicht den späteren
Veröffentlichungsauftrag. Der neuere [Prüfstand](GBA_ONLINE_HANDOFF.md#prüfstand) umfasst
885 Fälle und dokumentiert auch den offenen sporadischen Speicherabschluss-Timeout.
The user has since authorized publication; the pre-push fetch found no newer Linux
commit. Preserve that upstream history, with no force push. Earlier no-commit statements
below are historical audit notes; the linked GBA online handoff contains current evidence.

## Deutsch

### Verifizierter Git-Stand

- Lokale Ausgangsbasis: `22a77ef84af1b2f37a9342df908d4abd35efff53`.
- Abgerufener Linux-Stand: **`367674ffa015b3f211328e14dee9f2ac4904b83c`**.
- Genau ein neuer Commit: **xJessyX**, 12.09.2026, 14:02:12 +02:00,
  `Refactor application architecture and improve feature implementation`.
- Der Commit verändert 48 Dateien: 4.585 hinzugefügte und 301 entfernte Zeilen.
- Der Audit hat nur `origin/development` abgerufen und gelesen. Kein Pull,
  Checkout, Reset, Stash, Commit oder Push wurde durch den Audit ausgeführt.
  Lokale Windows-/Parity-/GB-Link-Änderungen bleiben erhalten.

Die Übergabe wurde vollständig gelesen, ebenso Fixliste, Playtest Runde 3 und
Kritik Runde 3. Ihre Formulierungen „lokal auf 22a77ef“ bzw. „kein Commit/Push“
beschreiben den Zeitpunkt ihrer Erstellung: Der überprüfte Remote-Commit enthält
diese Arbeit inzwischen. Historische Testzahlen und Bewertungen sind keine
neue Prüfung dieses zusammengeführten Windows-Arbeitsstands.

Belege: [Commit](https://github.com/VoltexModz/AetherBoy/commit/367674ffa015b3f211328e14dee9f2ac4904b83c),
[festgeschriebene Übergabe](https://github.com/VoltexModz/AetherBoy/blob/367674ffa015b3f211328e14dee9f2ac4904b83c/docs/LINUX_DEVELOPMENT_HANDOFF.md),
[Fixliste](https://github.com/VoltexModz/AetherBoy/blob/367674ffa015b3f211328e14dee9f2ac4904b83c/docs/LINUX_FIX_LOG.md).

### Was der Linux-Kollege ergänzt hat

| Bereich | Neuer Remote-Stand |
| --- | --- |
| Spielkomfort | Fortsetzen mit eigener Resume-Datei, State-Galerie samt inhaltsgebundener Vorschau, Undo des letzten Ladens innerhalb der Sitzung |
| Profile und Bibliothek | Nullable Spielprofile mit globaler Vererbung; Favoriten, eigene Titel, Systemfilter, beim Schließen gespeicherte Spielzeit |
| Reaktionsfähigkeit | Begrenzte Hintergrund-Metadaten; abbrechbare ROM-Vorbereitung mit Identitätsprüfung und GBA-Fokus-/Owner-Barriere; unveränderliche Settings-/Profilsnapshots mit Generation und Retry |
| Texteingabe | Gemeinsamer graphemorientierter SDL-Editor für Suche, Titel und Cheats; Cursor/Markierung/Clipboard, Pointer-Auswahl und getrennte IME-Komposition |
| Zugänglichkeit | Schriftstufen; optionales GTK3-Control-Center mit nativer Semantik über ATK/AT-SPI; Ctrl+F7 oder `--accessible`, F7 bleibt Rewind |
| Distribution | Selbstständige x64-/ARM64-Archive mit .NET-Runtime, zugehörigen Quellen und Paketprüfung; isolierter Weston-/D-Bus-Teststarter |
| Regressionen | Tests für späte Ladeergebnisse, Locks, GBA-Fokuswechsel, asynchrone Settings, Text und Accessibility; angepasste CI-Abhängigkeiten |

Die Änderungen betreffen Linux-Frontend, Dokumentation, Paketierung und Tests.
**Keine Datei unter `nanoboy/Core`, `nanoboy/Runtime` oder im vendorten GBA-Kern
wurde in diesem Remote-Commit verändert.** Der neue lokale GB-Link und der
laufende GBA-Link-Ausbau haben deshalb hier keine direkte Core-Dateikollision.
Das bedeutet nicht, dass eine Linux-Link-Oberfläche bereits existiert.

### Lokale Überlappungen und Zusammenführungsregeln

Beim Audit waren genau diese elf bereits lokal geänderten Dateien auch upstream
geändert. Unberührte Remote-Dateien können als kompletter Remote-Inhalt
übernommen werden; diese elf brauchen einen inhaltlichen Abgleich:

| Datei | Was von beiden Seiten erhalten bleiben muss |
| --- | --- |
| `.github/workflows/ci.yml` | Lokale höhere Core-/Runtime-/Windows-Gates; neue Linux-GTK-/AT-SPI-Abhängigkeiten und isoliertes Testskript. Endgültige Mindestwerte erst anhand der vereinten Tests festlegen. |
| `README.md` | Lokale Windows-/Link-/Parity-Erweiterungen und neue Linux-Komfort-/Distributionslinks. Keine pauschale 1:1-Parität behaupten. |
| `docs/LINUX_DEVELOPMENT_HANDOFF.md` | Neue R2-/R3-Übergabe plus lokale Screenshot-/Audio-/Parity-Nachträge; Liefer-SHA und historische Prüfstände klar unterscheiden. |
| `docs/LINUX_USER_GUIDE.md` | Neue Linux-Bedienung plus lokale F9-Performance-/F12-Screenshot-Anleitung. |
| `frontends/AetherBoy.Desktop/LinuxFrontendOptions.cs` | Sowohl `TextSize` als auch `PerformanceOverlay`. |
| `frontends/AetherBoy.Desktop/LinuxRomStorage.cs` | Remote `OpenIdentified`, abbrechbares Hashing/Migration und Ressourcenfreigabe mit lokalem gemeinsamem `RomWriteLease` kombinieren. |
| `frontends/AetherBoy.Desktop/LinuxSettingsStore.cs` | `TextSize` und `PerformanceOverlay` laden und in der neuen `SerializeSnapshotBytes`-Methode speichern; kein Rückfall auf synchrone UI-Schreibpfade. |
| `frontends/AetherBoy.Desktop/WaylandEmulatorHost.AetherUi.cs` | Neue Fokus-/Accessibility-/Ladeoverlay-Struktur mit lokaler Performance-Anzeige. |
| `frontends/AetherBoy.Desktop/WaylandEmulatorHost.Tools.cs` | Neuer Cheat-Texteditor sowie lokale Screenshot-/Ordner-/Performance-Aktionen. |
| `frontends/AetherBoy.Desktop/WaylandEmulatorHost.cs` | Neue Lade-/Settings-/Komfort-Partial-Struktur; Screenshot-Abschluss/Shutdown, Präsentationszähler, F9/F12 und Audio-Sitzungs-/Generationsschutz erhalten. |
| `tests/AetherBoy.DesktopTests/LinuxShellIntegrationTests.cs` | Neue Koordinaten und begrenztes Warten auf Settings/State-Metadaten; bestehende F9/F12-/PNG-Prüfungen behalten. |

Zusätzlicher semantischer Abgleich: neue Linux-Profile dürfen die lokale globale
Performance-Präferenz nicht beim ROM-/Scopewechsel verlieren. Alte Buttons und
F9/F12 dürfen die neue Ladebarriere nicht umgehen. Ein erfolgreicher
Textzusammenbau allein beweist diese Eigenschaften nicht.

### Lokale Zusammenführung dieses Auftrags

Die **45 Linux-Dateien** des Commits unter `frontends/`, `scripts/`,
`tests/AetherBoy.DesktopTests/` und `docs/LINUX*` sind inzwischen im Arbeitsbaum
integriert. Neun davon wurden mit bereits vorhandenen lokalen Änderungen
inhaltlich vereinigt. Die übrigen 36 wurden auf Übereinstimmung mit upstream
geprüft; nur zwei anschließend bewusst erweiterte Regressionstestdateien
unterscheiden sich davon. README, Changelog und CI wurden ebenfalls in der gemeinsamen
Abschlussintegration vereinigt. Der Git-HEAD wurde dafür nicht verändert.

Zusätzlich abgesichert: Die neue Settings-Snapshot-Serialisierung erhält
Performance-Anzeige und Schriftgröße unveränderlich; Spielprofile lassen beide
globalen Werte bestehen. F9/F12 und verzögert ausgelöste Tool-Aktionen umgehen
keine laufende ROM-Vorbereitung. Bestehende native Screenshot-Tests wurden in
den asynchron umgebauten Shell-Test übernommen. Diese neuen bzw. erweiterten
Tests wurden vom Integrationsagenten nicht eigenständig ausgeführt; Build und
Gesamtprüfung werden zentral koordiniert.

### Eigener Abschlusslauf des vereinten Stands

Release-Build der gesamten Lösung unter Windows: **0 Warnungen, 0 Fehler**.
Abschließender Lauf: **710 Fälle, 674 bestanden, 36 ausgelassen, 0 Fehler**.
Core 197/197, Runtime 220/220, Windows 178 bestanden/1 Vordergrund-Skip,
Desktop-Logik 79 bestanden/35 native Skips. Darin enthalten sind auch der neue
GBA-Link und die isolierten CPU-Zwischenzustände; Details stehen in der
[GBA-Link-Übergabe](GBA_LOCAL_LINK_HANDOFF.md). Alle übrigen Windows-/Audio-/Menü-
und gemeinsamen Werkzeugregressionen bleiben Bestandteil des Laufs.

Die 35 Desktop-Skips benötigen native Wayland/Unix/GTK/AT-SPI/Gerätebedingungen.
Der vereinte Linux-Stand wurde hier nicht nativ ausgeführt. Das isolierte
Weston-/D-Bus-Skript und die GTK/AT-SPI-CI-Abhängigkeiten sind übernommen; die
Desktop-Mindestzahl ist auf 114, Runtime auf 220, Windows-Gesamt auf 710 gesetzt.

Git bleibt lokal auf `22a77ef`; `origin/development` zeigt auf `367674f`.
Der Arbeitsbaum enthält die vereinigten Inhalte, **noch keinen Merge-Commit**.
Bei späterer Freigabe muss die Veröffentlichung den Linux-Commit in der Historie
erhalten und normal vorwärts pushen; kein Force-Push/Ersetzen seines Commits.
Die neuen ausführbaren Upstream-Skripte `package-linux.sh` und
`test-linux-package.py` behalten beim späteren Staging ihren Modus 100755.

### Zugeschriebene Upstream-Nachweise

Der Kollege berichtet für seinen Stand **111 entdeckte Desktopfälle**:
**78 bestanden / 33 native Skips** im Logiklauf bzw. **108 bestanden / 3
Audio-Skips** in isoliertem Weston/D-Bus; jeweils null Fehler. Diese Zahlen wurden
aus seiner versionierten Dokumentation gelesen, nicht auf diesem PC erneut
ausgeführt. Die Teilmengen werden nicht dazugezählt.

Windows-Ausführung, physische Controller, hörbare Audioqualität, Mixed-DPI,
Orca-/IME-Desktopbedienung, lange echte Spielsessions und ARM64-Ausführung sind
damit nicht bestätigt. ARM64 wurde laut Bericht nur cross-published und im
Archiv/ELF geprüft. Die 9,1-/8,9-Noten sind seine dokumentierte Softwarebewertung,
keine neue Releasefreigabe.

Quelle: [Playtest Runde 3](https://github.com/VoltexModz/AetherBoy/blob/367674ffa015b3f211328e14dee9f2ac4904b83c/docs/LINUX_PLAYTEST_ROUND3.md).

### Integrations- und Freigabeplan

1. Remote-Änderungen dateiweise mit der lokalen Arbeit vereinen, ohne lokale
   Windows-/Link-/Shared-Audio-Funktionen zu ersetzen. Änderungen an gemeinsamem
   Core und Runtime bleiben einer zuständigen Person pro Datei zugeordnet.
2. Gesamtlösung bauen und automatische Tests ausführen. Relevante
   Linux-Logiktests unter Windows sind keine native Wayland-Abnahme.
3. Audio-Sitzungswechsel, ROM-Locks, Settings-Snapshots, Profilwechsel und
   neue Ladebarrieren speziell im kombinierten Stand prüfen. Neue Link-Funktionen
   mit synthetischen Programmen prüfen; echte Spielkompatibilität separat nennen.
4. Vereinten Lieferstand und Restunterschiede zwischen Windows/Linux dokumentieren.
5. Dem Nutzer vor Veröffentlichung erklären, welche Arbeit vom Kollegen stammt,
   was lokal hinzugekommen ist, was getestet wurde und was offen bleibt.
   **Commit und Push erst nach seiner ausdrücklichen Bestätigung.**

## English

The audited local base is `22a77ef84af1b2f37a9342df908d4abd35efff53`. A fetch
resolved `origin/development` to **`367674ffa015b3f211328e14dee9f2ac4904b83c`**:
one commit by **xJessyX**, dated 12 September 2026 at 14:02:12 +02:00, changing
48 files (+4,585 / -301 lines). This audit performed no pull, checkout, reset,
stash, commit or push. Existing uncommitted Windows/parity/link work is retained.

The complete handoff and its round-3 fix, playtest and review documents were
read. Their references to uncommitted work on `22a77ef` are historical: the
fetched commit now contains that work.

The Linux contribution adds resume/gallery/undo, inheriting per-game profiles,
library metadata, bounded background work, cancellable ROM preparation with a
GBA focus/owner barrier, immutable asynchronous settings snapshots, an
IME/grapheme-aware SDL editor, optional GTK3/AT-SPI controls, and self-contained
x64/ARM64 packaging. **It does not change shared Core, Runtime or vendored GBA
files, and does not add a Linux link-cable UI.**

Eleven existing local files overlap, listed above. The merge must retain local
shared save leases, audio session/generation guards, native screenshots and
performance metrics while adopting upstream cancellation, focus, accessibility,
profile and background-write behavior. In particular, `PerformanceOverlay` must
survive the new snapshot serializer and profile/scope transitions. UI input must
not bypass the new busy/loading guards.

The colleague reports 111 discovered desktop tests: 78 passing with 33 native
skips in the logic run, or 108 passing with 3 audio skips on isolated Weston and
D-Bus. These are attributed upstream results, not a fresh run on this machine.
Physical hardware, audible audio, long commercial-game sessions, real Orca/IME
interaction, mixed DPI and ARM64 execution remain separately qualified.

Combined final build: zero warnings/errors. Tests: **710 total, 674 passed,
36 skipped, zero failed** (Core 197, Runtime 220, Windows 178+1 skip, Desktop
79+35 skips). Native Linux conditions and one denied Windows foreground test
explain the skips. This includes the GBA link expansion and CPU-isolation fix;
it does not reproduce native Linux gameplay. README, changelog, parity and CI
now include both contributions. **Do not commit or push until the user explicitly
confirms.** Git HEAD remains on 22a77ef with merged working-tree content, not yet
a merge commit; eventual publication must preserve 367674f ancestry without force.

Integration progress: all 45 owned Linux files are now present in the working
tree, with nine overlapping files deliberately combined. Of the 36 otherwise
unchanged upstream files, only two regression tests were intentionally extended
after import. Profile/global-option and immutable-snapshot coverage now includes
the existing performance overlay; loading guards also cover screenshot and
performance actions. README, changelog and CI were completed in the root agent's
coordinated integration. The integration agent did not run a separate build or
test suite, and did not change Git HEAD or publish anything.
