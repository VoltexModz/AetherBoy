# Linux-Entwicklung: Übergabe an Windows und den nächsten ChatGPT

Nachtrag 13. September: [Native Raumcodes – Windows-Integration, Build und Abnahme](ONLINE_ROOMS_WINDOWS_HANDOFF.md).
Enthält auch die Zusammenführung mit Voltex' Diagnose-/Session-Fix `23c6fda`.

Nachtrag 13. September / September 13 addendum:
[Übernahme von 6e69f83 / 3850464, Windows-Verbindungsdiagnose und Sitzungsabschluss (DE/EN)](WEBRTC_DIAGNOSTICS_PARITY.md).

Aktuellster gemeinsamer Nachtrag / latest shared addendum:
[GBA Gen3 Online und protokollierte Sitzungsspielstände für Windows/Wayland (DE/EN)](GBA_ONLINE_HANDOFF.md).
Identische Runtime, neuer Linux-Einstieg mit ausdrücklicher Entwicklungsbestätigung;
kein abgeschlossener echter Pokémon- oder WAN-Test. Kein Commit/Push ohne Freigabe.

Neuer gemeinsamer Prototyp / new shared prototype:
[GB/GBC Online Link für Windows und Linux – Anleitung, Architektur und Grenzen (DE/EN)](ONLINE_LINK_HANDOFF.md).
Linux: F10 bzw. Tools → Online Link. Eigene Sitzungskopien, kein automatischer Rückimport.

Integrationsnachtrag 12. September: Der unten dokumentierte Linux-Lieferstand
ist inzwischen als **367674f** verfügbar. Die lokale Windows-Arbeit wird damit
zusammengeführt; noch kein neuer Commit oder Push ohne Nutzerfreigabe.
[Abgleich des Linux-Commits und Integrationsregeln (DE/EN)](LINUX_UPSTREAM_INTEGRATION_REVIEW.md) ·
[Windows ↔ Linux: gemeinsame Audio-/Screenshot-/Performance-Dienste und offene Funktionsgleichheit (DE/EN)](PLATFORM_PARITY.md).
Historische Prüfzahlen unten gelten für ihren jeweils bezeichneten Lieferstand.
Der anschließende Windows-Ausbau ergänzt [Local Link Lab für GB/GBC/GBA](LOCAL_LINK_LAB.md)
mit gemeinsamer Runtime und [GBA-SIO-/CPU-Isolation](GBA_LOCAL_LINK_HANDOFF.md).
Linux besitzt dafür noch keine native Zwei-Spieler-Oberfläche; der Core muss
nicht erneut portiert werden. Neue Schema-6-GBA-States brauchen den neuen Kern,
Schema 5 bleibt lesbar; Batterie-Saves ändern sich nicht.

Stand: **12. September 2026** · Branch: **development** · Version: **4.8.0-alpha.1**

Diese Datei ergänzt die [Windows-Handoff](WINDOWS_DEVELOPMENT_HANDOFF.md).
Bitte beide lesen: Die Windows-Datei beschreibt ihren damaligen Lieferstand;
ihre Aussagen über fehlende Linux-Funktionen sind teilweise durch diese Arbeit
überholt. Es bleibt ein Projekt mit gemeinsamem Core und Runtime.

## Ausgangspunkt und Git-Stand

Der Linux-Ausbau begann auf `d983ed4`. Anschließend wurde der Windows-Commit
**`7ef5b59`** (`feat(windows): deliver player tools, stereo and IPS/BPS/UPS patch lab`)
per Fast-forward übernommen und mit den lokalen Linux-Änderungen zusammengeführt.
Konflikte in Audioverträgen, CI, Changelog und READMEs sind aufgelöst. Die neuen
Windows-Funktionen und die gemeinsame Stereo-Implementierung bleiben erhalten.

Der erste Linux-Ausbau ist als **`c1ffe10`**, das Linux Patch Lab als
**`22a77ef`** committed. Die zweite Komfortrunde und Runde 3 liegen beim Schreiben dieses
Nachtrags als lokale Änderungen auf `22a77ef`; sie wurde nicht automatisch
committed oder gepusht. Vor weiterer Arbeit den tatsächlichen Git-Stand prüfen.

## Aktueller Nachtrag: Restkritik / Runde 3

**Zuerst lesen:** [Fixliste mit IDs, Vorher/Nachher und Nachweisen](LINUX_FIX_LOG.md),
[Playtest Runde 3](LINUX_PLAYTEST_ROUND3.md), [Kritik Runde 3](LINUX_CRITIQUE_ROUND3.md).
Die anschließenden Runde-2-Noten und Testzahlen sind historische Stände.

**Abgeschlossen:** 111 Desktopfälle entdeckt; Logik 78 bestanden/33 Skips,
isoliertes Weston 108 bestanden/3 Audio-Skips, jeweils 0 Fehler. Build ohne
Warnungen/Fehler, x64-/ARM64-Pakete und normaler Start-Build aktualisiert.
Unabhängige Note **UI 9,1/10 / Features 8,9/10** bei unveränderter Rubrik.
Aktuelle Logs, Paketprüfsummen und Reproduktion stehen im Runde-3-Playtest.

Neu: vollständiger gemeinsamer SDL-Texteditor für Titel/Suche/Cheats, optionales
semantisches GTK3-Control-Center, abbrechbare ROM-Vorbereitung samt GBA-Fokusbarriere
und Hintergrundschreiben unveränderlicher Settings-/Profilsnapshots. Diese
Funktionen nicht erneut bauen. Normale Text-/Fensterbedienung behält Entwürfe;
Preedit und unsichtbarer Eingabefokus werden bereinigt. Native Callback- und
Worker-Ergebnisse dürfen keine inzwischen andere Seite/ROM verändern.

GTK öffnen: **Ctrl+F7** oder System → **Accessible UI**; alternativ
`bash scripts/run-linux.sh --accessible`. **F7 allein bleibt Rewind**. GTK3/ATK
sind optionale Systembibliotheken, nicht Bestandteil der Runtime-Archive. Der
SDL-Pfad funktioniert weiterhin ohne sie. Kein globales Desktop-/Accessibility-
Setting ändern, um Tests passend zu machen.

Physische Controller, echte Mixed-DPI-/Orca-/IME-Bedienung, ARM64-Ausführung und
lange Spieltests bleiben getrennte manuelle Qualifikation. Der Nutzer spielt auf
seinem aktiven Workspace: Tests versteckt; unvermeidbare sichtbare Agentenfenster
nur auf Workspace 5 ohne Fokuswechsel. Die AT-SPI-Busprobe mappt ausschließlich
auf ihrem ausdrücklich isolierten Weston-Compositor.

Profil-/ROM-Wechsel und Beenden warten weiterhin auf erfolgreiche Speicherung.
Cancel kann einen laufenden Betriebssystem-I/O-Aufruf nicht hart unterbrechen;
sein spätes Ergebnis wird verworfen und sein Lock freigegeben. Siehe Fixliste
für die genauen Sicherungsgrenzen. Auch diese Runde ist lokal auf `22a77ef`;
keine automatische Commit-/Push-Aktion.

## Historischer Nachtrag: Komfortrunde 2

Prioritäten und Lieferumfang: Hintergrund-Metadaten (10/10), Resume/Galerie/Undo
(9/10), Spielprofile (8/10), Bibliotheksmetadaten (8/10), Text/Navigation (8/10),
Distribution mit eingebetteter Runtime (7/10). Die Zahlen hier bewerten die
Wichtigkeit, nicht die erreichte Qualität.

- Renderpfade verwenden zwischengespeicherte Bibliotheks-, State- und Backupdaten.
  Die Worker sind begrenzt; veraltete Ergebnisse dürfen keine andere ROM ersetzen.
- Resume nutzt `Data/states/<hash>/game.resume`; manuelle Slots bleiben getrennt.
  Preview-Dateien sind an den SHA-256 des State-Inhalts gebunden. Undo lebt nur
  innerhalb der aktuellen Sitzung. Startseite und Overview bieten Fortsetzen.
- Nullable Spielprofile liegen unter `Config/profiles/<hash>.json`; globale
  Änderungen müssen vor einem Scopewechsel erfolgreich gespeichert sein.
- Favoriten, eigene Titel, Systemfilter und aktive Spielzeit ergänzen die Library.
  Spielzeit wird beim Schließen persistiert; sie ist kein dauerhafter Live-Zähler.
- Standard/Large/Larger sind relative Schriftstufen; F6 erschließt Seiteninhalte.
  Damals fehlte semantischer Screenreader-Zugriff; Runde 3 ergänzt den GTK-/AT-SPI-Pfad.
- `scripts/package-linux.sh` erstellt x64-/ARM64-Archive mit Runtime und
  korrespondierenden Quellen. Details und glibc-Untergrenze stehen in
  [Distribution](LINUX_DISTRIBUTION.md). ARM64 wurde cross-published, nicht ausgeführt.

Aktuelle unabhängige Befunde und genaue Prüfzahlen stehen in
[Playtest Runde 2](LINUX_PLAYTEST_ROUND2.md) und
[Kritik Runde 2](LINUX_CRITIQUE_ROUND2.md). Die weiter unten genannten älteren
Testzahlen und Noten dokumentieren historische Stände, keine neue Abnahme.
Die neuen Ziele sind **Features >8/10 und UI >9/10**. Unterschreitungen dürfen
nicht durch angepasste Bewertungsgewichte oder ungeprüfte Annahmen verdeckt werden.
Finale Integrationsprüfung vom 12. September: 84 Desktopfälle entdeckt;
Logiklauf **67 bestanden / 17 native Skips**, isolierter Ubuntu/Weston-Lauf
**81 bestanden / 3 Audio-Skips**, jeweils kein Fehler. CI-Mindestwerte sind auf
67 beziehungsweise 81 angehoben. Der letzte native gezielte Lauf besteht
**20/20** (19 Komfortfälle plus Shell-Integration); zuvor **12/12** bestehende
Navigations-/Patch-/Kurzspiel-Fälle mit realem PipeWire. Das ist keine Behauptung,
alle 84 Fälle zuletzt gemeinsam auf dem Nutzerdesktop ausgeführt zu haben.

`bash scripts/build-linux.sh` hat den normalen Linux-x64-Build aktualisiert.
Beide Runtime-Archive sind frisch gebaut und geprüft; x64 tatsächlich ohne
installierte Runtime im PATH gestartet, ARM64 nur Paket/ELF geprüft. Die
enthaltenen 158 C#-Produktionsdateien entsprechen dem finalen Produktionscode.
Logs liegen unter `artifacts/comfort-{logic,headless,publish}-final.log` und
`artifacts/linux-package-{x64,arm64}.log`. Kein neuer Commit oder Push erfolgte.

Die aktuelle unabhängige Kritik bewertet Features mit **8,6/10**, das UI mit
**8,9/10**: Das Featureziel ist erfüllt, das UI-Ziel bleibt offen.
Keine neuen 30-Minuten-Läufe: Diese übernimmt der Nutzer. Während er spielt,
Tests versteckt ausführen; notwendige sichtbare Fenster nur auf Hyprland
Workspace 5 ohne Fokuswechsel öffnen.

## Was Linux jetzt besitzt

| Bereich | Implementierter Stand |
| --- | --- |
| Datenintegrität | Zentrale XDG-Ablage, ROM-Identität per SHA-256, getrennte Saves/States, exklusive Schreibsperre pro ROM, Migration bestehender Save-Familien, lesbare Settings-Sicherung |
| Installation | Gemeinsamer Launcher, frischer Publish, vorbereitete Release-Ordner vor Aktivierung, absoluter Desktop-Starter, Deinstallation mit Datenerhalt |
| Diagnose | Lokale begrenzte JSONL-Berichte, Buildidentität, Fehlerklassen, Audio-/Frame-Zähler, ZIP-Export und Aufzeichnungspräferenz |
| Audio | GB/GBC/GBA-Stereo über den gemeinsamen Vertrag bis SDL, Formatwechsel, Queue-/Drop-Zähler und Wiederholungsversuch nach Ausgabefehler |
| Ressourcen | Gedrosseltes Zeichnen bei leerem Fenster/Pause; minimiert keine unnötige Darstellung; VSync-Ergebnis berücksichtigt |
| Bibliothek | Suche, zuletzt gespielte ROMs, Öffnen und Neuzuordnung verschobener Dateien; Saves bleiben bei gleicher Inhaltsidentität erhalten |
| Patch Lab | Library-Unterseite mit IPS/BPS/UPS, Dateiauswahl/Drag-and-drop, explizitem UPS-Undo, Hintergrundverarbeitung und Ergebnisstart |
| Save-Werkzeuge | Backup-Inspektion, bestätigtes Restore mit Vorher-Archiv, Export und größengeprüfter roher `.sav`-Import |
| Firmware | DMG-/CGB-Boot-ROM und GBA-BIOS importierbar; Auswahl beim nächsten ROM-Start; Größenprüfung, keine Echtheitsgarantie |
| Controller | GUID-Profile, freie Belegung, Deadzone, Gerätewechsel, Menübedienung, Hotplug-Ersatz und konfigurierbare Fokus-Pause |
| Zusatzwerkzeuge | PCM-WAV-Aufnahme und vom gemeinsamen Core unterstützte Cheats innerhalb einer Sitzung |
| UI | Neun Control-Center-Bereiche, größere Mindestschrift, verbesserter Kontrast, sichtbarer Fokus, verständliche Slot-/Statusanzeigen |

Der zuletzt gemeldete Layoutfehler ist behoben: **Controller Setup** sitzt schmaler
oberhalb der Tastaturbelegungen und überlappt die Up-Taste nicht mehr. Das wurde
an einem nativen Capture geprüft. Tastatur- und Controller-Unterseite sind getrennt.

## Gemeinsame Verträge: beim Weiterentwickeln erhalten

### Stereo und States

- `SampleCount` zählt Zeitframes, `InterleavedSampleCount` einzelne Kanalwerte.
  `GetInterleavedSamplesCopy()` liefert bei Stereo `L,R,L,R,…`.
- `GetSamplesCopy()` und `CopySamplesTo()` bleiben kompatible Mono-Downmix-APIs.
  **Linux benutzt inzwischen die interleavte API**, genau wie die neue
  Windows-Ausgabe. Die alte Handoff-Aussage „Linux weiterhin Mono“ gilt nicht mehr.
- SDL-Kanäle, Samplerate und Pufferzeit werden gemeinsam behandelt:
  `bytes = frames × channels × sizeof(float)`. Bei Formatwechsel wird der Stream
  neu geöffnet. Keine Stereo-Werte an einen Mono-Stream senden.
- Die gemeinsame GB/GBC-`STER`-State-Erweiterung aus dem Windows-Commit bleibt
  erhalten: neue Leser akzeptieren alte States, alte Builds nicht die neue
  Stereo-Erweiterung. Batterie-Saves und GBA-State-Format bleiben unverändert.
- Keine neuen Änderungen am vendorten GBA-Kern durch diesen Linux-Ausbau.
  Keine WinForms-/NAudio-/Direct2D-Abhängigkeit in das Linux-Frontend aufnehmen.

### Daten und Bedienung

- Unter jeder gültigen absoluten XDG-Basis liegt der Unterordner `aetherboy`.
  Relative/leere Umgebungswerte verwenden die üblichen Benutzerverzeichnisse.
- Config: Einstellungen und Sicherung. Data: `saves/<hash>`, `states/<hash>`,
  Bibliothek, Firmware und separate Programm-Releases. State: Sitzungsberichte.
  Cache bleibt für wiederherstellbare Daten vorgesehen.
- Legacy-Saves samt RTC/Guard/Backups werden kopiert; zentrale Daten haben Vorrang,
  Originale bleiben erhalten. Eine laufende Sitzung behält ihre ROM-Schreibsperre
  auch bei Restore. Save-Werkzeuge dürfen diesen Schutz nicht umgehen.
- Die Bibliothek referenziert vorhandene ROM-Dateien; sie ist noch kein vollständiger
  importierender Windows-Vault. Patch-Ergebnisse werden dagegen kontrolliert unter
  `Data/roms/<hash>/` importiert. `.sav`-Import überträgt keine RTC-Begleitdatei.
  Exporte persistierter Daten sind nicht automatisch ein Flush des laufenden Spiels.
- Linux und Windows verwenden unterschiedliche Speicherpfade. Gleicher Hash bedeutet
  keine automatische Synchronisation, Save-Übertragung oder identische Metadaten.
- Linux reserviert **F6 für Hauptfenster-Fokus**, Tab bleibt im Spiel Turbo.
  Windows-F6 öffnet die State-Galerie. Shortcuts nicht blind plattformübergreifend
  übernehmen. Vorhandene Linux-F6-Belegungen werden beim Laden gezielt migriert.
- Such-/Texteingabe wird beim Schließen der Einstellungen beendet; unsichtbare
  Unterseiten dürfen keine Tasten abfangen. Fokus-Pause erhält manuelle Pause.

Diagnose bleibt lokal, ohne automatischen Upload und ohne ROM-/Save-Inhalte oder
private Pfade in Fehlernachrichten. Development-Aufzeichnung ist standardmäßig
aktiv; die UI-Präferenz greift beim nächsten Start. Diagnose-ZIP ist kein Save-Export.

## Orientierung im Code

| Dateien | Verantwortung |
| --- | --- |
| `frontends/AetherBoy.Desktop/LinuxDataPaths.cs`, `LinuxRomStorage.cs`, `LinuxSettingsStore.cs` | XDG, Identität, Migration, Schreibbesitz und Einstellungen |
| `LinuxLibrary.cs`, `WaylandEmulatorHost.Library.cs` | Bibliothek, Relocate und Firmware |
| `LinuxRomPatchService.cs`, `WaylandEmulatorHost.PatchLab.cs` | Linux-Patch-Import, Wiederverwendung bekannter Ergebnisse und native Oberfläche; Parser bleibt gemeinsam |
| `WaylandEmulatorHost.SaveTools.cs` | Backup-/Import-/Export-Abläufe |
| `LinuxGamepadProfile.cs`, `WaylandEmulatorHost.Controller.cs` | Geräteprofile und Controller-Bedienung |
| `LinuxDiagnostics.cs`, `SdlAudioOutput.cs`, `LinuxWavRecorder.cs` | Diagnose, SDL-Ausgabe und Aufnahme |
| `WaylandEmulatorHost.cs`, `.AetherUi.cs`, `.Tools.cs`, `SdlTextRenderer.cs` | Ereignisrouting, Layout, Werkzeuge und Text |
| `nanoboy/Runtime/SnapshotBuffers.cs`, `GbaProductionMachine.cs`, `ProductionMachine.cs` | Gemeinsamer Audiotransport; Windows-Änderungen bereits integriert |
| `scripts/launch-linux.sh`, `build-linux.sh`, `run-linux.sh`, `install-linux-user.sh`, `uninstall-linux-user.sh` | Start, Build und Benutzerinstallation |
| `tests/AetherBoy.DesktopTests/Linux*Tests.cs` | Native UI, Storage, Diagnose, synthetische Spiele und virtuelle Controller |

## Tatsächlich geprüft

**Nach Integration von `7ef5b59` und dem Controller-Layoutfix:**

- **181 Core + 127 Runtime + 57 Desktop = 365 bestandene Tests**, keine Skips in
  diesem Desktop-Lauf. Native kurze GB/GBC/GBA-Szenarien, Save/Load/Rewind,
  Batterie über Neustart, Restore und virtuelle SDL-Controller enthalten.
- Gesamtlösung mit `EnableWindowsTargeting=true`: null Warnungen/Fehler.
  Das ist eine Kompilierungsprüfung; **Windows wurde hier nicht ausgeführt**.
- Linux-x64-Publish erfolgreich; Controller-Layout im nativen Bild geprüft.

Zusätzliche Nachweise **vor der letzten Windows-Zusammenführung**:

- Ubuntu 24.04 x64 im isolierten Container mit echtem Weston-Headless-Compositor:
  54 bestanden, drei Audio-Playtests bewusst übersprungen. Hyprland auf CachyOS
  wurde separat nativ getestet. Keine ARM64-/GPU-/PipeWire-Abnahme durch den Container.
- Echter Installer isoliert geprüft: fehlgeschlagene Migration/Updates, Sonderzeichen
  und Leerzeichen, Minimal-PATH ohne dotnet, Deinstallation mit erhaltenen Saves.
- Kritiker: **UI 8,2/10, Features 7,9/10**; unabhängiger Playtester: **7,8/10**.
  Das sind begründete Bewertungen des dokumentierten Umfangs, keine Release-Freigabe.
  Der spätere Layoutfix hat keine neue unabhängige Gesamtnote erhalten.
- Ein begrenzter Vergleich des versteckten leeren Fensters ergab rund 76 % weniger
  CPU-Zeit. Das ist kein Gameplay-, Eingabelatenz- oder Monitor-Benchmark.

**Keine bestandenen 30-Minuten-Läufe behaupten.** GB wurde vorzeitig per SIGTERM
beendet; GBA auf Nutzerwunsch gestoppt. Der Nutzer übernimmt die Langzeitabnahme
selbst und bat darum, dafür keine weiteren Agentenläufe/Tokens zu verbrauchen.

Die Playtests verwenden generierte Programme, keine echten Spielsessions. Audio
läuft im Test stummgeschaltet über den realen Ausgabepfad; hörbare Qualität ist
damit nicht geprüft. Virtuelle SDL-Geräte ersetzen keine physischen Controller.
Details: [Playtest](LINUX_PLAYTEST.md), [Kritik](LINUX_CRITIQUE.md),
[Roadmap mit Prüfständen](LINUX_ROADMAP.md).

Logs und Captures unter `artifacts/` sind lokal und Git-ignoriert; nach einem Pull
sind sie auf dem anderen Rechner nicht automatisch vorhanden. Die Berichte im
Repository dokumentieren die Ergebnisse; bei Bedarf gezielt reproduzieren.

## Patch-Lab-Nachtrag

Der aktuelle native Desktop-Kurzlauf besteht **65/65 Tests**, ohne Fehler oder
Skips, einschließlich der acht neuen Fälle. CI-Warnungsgate und Linux-x64-Publish
sind ebenfalls geprüft. Ubuntu/Weston im isolierten Container besteht 62 Tests
mit drei bewusst übersprungenen Audio-Playtests; der reine Logiklauf besteht 59
mit sechs nativen Skips. Die früheren 181 Core-/127 Runtime-Tests oben gehören
zur gemeinsamen Basis; der portable Patcher wurde für diese UI-Anbindung nicht geändert.

Acht neue Desktop-Tests prüfen alle drei Formate, explizites UPS-Rückpatchen mit
unveränderten Originalmetadaten/Saves, keine Save-Migration in Hacks, defekte und
übergroße Eingaben, korrupte bestehende Ergebnisse, Katalogfehler und native
Dateiauswahl-Rückmeldungen/Drop/Apply/Open. Zwei gezielte Captures zeigen normales
und kleines Fenster ohne Überlappungen. Echte Portal-Dialogbedienung wird dadurch
nicht ersetzt. Anleitung: [Linux Patch Lab](LINUX_USER_GUIDE.md#11-patch-lab-ips-bps-and-ups).

## Starten und kurze Prüfung

Im Projektverzeichnis unter Linux mit .NET gemäß `global.json`:

```bash
bash scripts/run-linux.sh
# Optional direkt mit eigener ROM:
bash scripts/run-linux.sh "/pfad/zum/spiel.gba"
# Installierten App-Menü-Eintrag aktualisieren:
bash scripts/install-linux-user.sh
```

Kurze Prüfungen, native Fälle nur innerhalb einer echten Wayland-Sitzung:

```bash
dotnet test --project tests/AetherBoy.CoreTests -c Release
dotnet test --project tests/AetherBoy.RuntimeTests -c Release
AETHERBOY_UI_TESTS=1 AETHERBOY_PLAYTEST=1 \
  AETHERBOY_PLAYTEST_SECONDS=10 AETHERBOY_GBA_PLAYTEST_SECONDS=10 \
  dotnet test --project tests/AetherBoy.DesktopTests -c Release
```

CI-Mindestzahlen berücksichtigen Skips: Core 181, Runtime 127, Desktop ohne native
Opt-ins 59, headless Wayland mit virtuellem Controller/Patch Lab 62. Der vollständige
native Kurzlauf mit Audio umfasst 65. Windows-CI behält den übernommenen Mindestwert 440;
der tatsächliche kombinierte Windows-Testlauf muss dort noch erfolgen.

## Noch offen und sinnvolle Anschlussarbeit

1. Den tatsächlichen Linux-Liefercommit auf beiden PCs synchronisieren und die
   gemeinsame Änderung unter Windows prüfen. Nutzerdaten und offene Arbeit erhalten.
2. Reale Spiele, hörbares L/R-Audio, physische Gerätewechsel, Suspend/Resume,
   gemischte DPI/Monitorraten, weitere Compositoren und ARM64 praktisch qualifizieren.
   Langtests übernimmt wie vereinbart der Nutzer.
3. Die Komfortrunde oben ist implementiert. Native Screenshot-Werkzeuge und
   ein GBA-Audio-Inspector bleiben mögliche spätere Erweiterungen.
4. Das Linux Patch Lab ist jetzt implementiert; nicht erneut bauen. Für Änderungen
   den gemeinsamen Parser beibehalten und Windows-Importdienste nicht in Linux
   einbinden. Reale Hack-Kompatibilität bleibt eine eigene Spielprüfung.
5. Flatpak/AppImage, Assistenztechnik und asynchrone explizite Dateiaktionen
   bleiben eigene Folgearbeit. Eingebettete Runtime und Hintergrund-Metadaten
   sind inzwischen implementiert.

Linux besitzt bereits Save-Import/-Export; der in der Windows-Handoff offene
Windows-Punkt 7 ist dadurch **nicht** automatisch erledigt. Ebenso bedeuten die
gemeinsamen Inspector-Snapshots noch keine Linux-Inspector-Oberfläche; die
Linux-Patch-Oberfläche ist inzwischen separat ergänzt.

Für den nächsten ChatGPT: erst diesen Stand mit Code und Git abgleichen, dann
den konkreten Nutzerauftrag bearbeiten. Keine bereits implementierten Funktionen
neu bauen und historische Noten/Testzahlen nicht als frische Prüfung ausgeben.
