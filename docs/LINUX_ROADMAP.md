# Linux-Ausbau

Planungsstand: 11. September 2026, nach Aktualisierung von `development` auf
`d983ed4` (`feat(windows): centralize app data and development diagnostics`).
Die Ausgangsanalyse unten beschreibt den Stand vor diesem Ausbau. Der aktuelle Umsetzungsstand steht in der folgenden Tabelle.

## Priorisierung und Umsetzungsstand

Bewertung der Wichtigkeit von 0–10; Datenverlust und Bedienblockaden stehen vor
zusätzlichen Werkzeugen. Umsetzung erfolgt in dieser Reihenfolge, mit L7 als
begleitender Abnahme. Bewertungen der Qualität stehen getrennt in
[Unabhängige Kritik](LINUX_CRITIQUE.md) und [Playtest](LINUX_PLAYTEST.md).

| Rang | Paket | Wichtigkeit | Implementierung |
| --- | --- | ---: | --- |
| 1 | L1 Datenintegrität | 10/10 | XDG-Ablage, SHA-256-Zuordnung, Migration ganzer Save-Familien, exklusive Schreibrechte, Settings-Backup |
| Begleitend | L7 Tests und unabhängige Prüfung | 10/10 | Native Tests, adversarielle Storage-/Bedientests, Playtest-Agent, Kritiker, CI für x64/ARM64 und headless Wayland |
| 2 | L2 Installation | 9/10 | Gemeinsamer Launcher, geprüfte neue Release-Ordner, Wechsel nach Vorbereitung, absoluter Desktop-Start, Uninstaller mit Datenerhalt |
| 3 | L3 Diagnose | 9/10 | Buildidentität, begrenzte lokale Berichte, Fehlercodes, ZIP-Export, Aufnahmepräferenz |
| 4 | UI-Kritik und Bedienblockaden | 9/10 | Lesbare Mindestschrift, Kontrast, Fokusmodell, F6-Navigation, klare Namen und Save-Slot-Status |
| 5 | L4 Audio und Ressourcen | 8/10 | GBA-Stereo bis SDL; Mono-API bleibt kompatibel; Queue-/Drop-Messwerte, kein komplettes Leeren bei Rückstau; gedrosselte Idle-/Pause-Ansicht |
| 6 | L5 Bibliothek und Save Recovery | 8/10 | Durchsuchbare zuletzt-gespielt-Liste, Relocate, Backup-Inspektion, bestätigte Wiederherstellung mit Vorher-Archiv, Export und .sav-Import |
| 7 | L6 Controller und Desktop | 7/10 | GUID-Profile, freie Belegung, Deadzone, Gerätewechsel, Controller-Menünavigation, optionale Fokus-Pause |
| 8 | L5 Firmware | 6/10 | Import von DMG-/CGB-Boot-ROM und GBA-BIOS, Größenprüfung, Auswahl beim nächsten ROM-Start |
| 9 | L5 Zusatzwerkzeuge | 5/10 | WAV-Aufnahme und Verwaltung vom Core unterstützter Cheats innerhalb einer Sitzung |
| Später | Weitere Distributionsformate | 4/10 | Flatpak/AppImage und Paket mit eingebetteter Runtime bleiben eigene Folgearbeit |

### Abnahmegrenzen

Die Umsetzung ersetzt keine Messungen an nicht vorhandener Hardware. Tests auf
KDE/GNOME, physische Controller-/Audiogerätewechsel, Suspend/Resume, Misch-DPI und
Monitore mit 120/144 Hz bleiben manuell zu qualifizieren. Die CI-Erweiterung ist
lokal vorbereitet; ein GitHub-Run und eine reale ARM64-Ausführung stehen aus.
Der nachträglich integrierte Commit `7ef5b59` ergänzt auch Windows-Stereo und
GB/GBC-Stereodaten. Beide Frontends nutzen nun den interleavten Audiopfad; die
Mono-API bleibt kompatibel. Windows-Ausführung muss auf Windows geprüft werden.

Die Testspiele sind erzeugte Programme. Reale GB/GBC/GBA-Spielkompatibilität und
hörbare Audioqualität werden daraus nicht abgeleitet. Die Qualitätsschwellen
UI >8/10 und Features >6/10 gelten für den dokumentierten Prüfbereich, nicht als
pauschale Release-Freigabe. Eine feste Benchmarksuite für Eingabelatenz und
Monitor-Framezeiten bleibt weitere Arbeit.

## Ausgangslage vor der Umsetzung

Linux besitzt bereits einen nativen SDL3-/Wayland-Host mit Hyprland-Erkennung,
Control Center, Portal-Dateidialog, Drag-and-drop, Tastaturbelegung, Gamepad-Hotplug,
Audio, fünf Save-State-Slots und Rewind. Atomare Batterie-Saves mit Backups und
Integritätsprüfung sind vorhanden. VSync wird bereits angefordert. Diese Grundlagen
werden weiterentwickelt.

Der neue Windows-Commit ergänzt zentrale, nach ROM-Inhalt getrennte Ablage und
lokale Development-Berichte. Linux verwendet dagegen weiterhin ROM-nahe Saves
und States. Einstellungen werden atomar unter XDG_CONFIG_HOME gespeichert; bei
unlesbaren Einstellungen werden Defaults geladen, ohne Wiederherstellung aus einer
letzten lesbaren Sicherung.

## Empfohlene Reihenfolge

| Schritt | Priorität | Ergebnis | Umfang |
| --- | --- | --- | --- |
| L1 | Hoch | Zentrale Linux-Datenablage und sichere Migration | Groß |
| L2 | Hoch | Verlässlicher installierter Starter und Aktualisierung | Mittel |
| L3 | Hoch | Lokale Diagnose mit Buildidentität und Export | Mittel |
| L4 | Hoch | Audio und Frame-Pacing anhand von Messwerten verbessern | Groß |
| L5 | Mittel | Bibliothek, Save-Verwaltung und Firmware-Auswahl | Groß, aufteilbar |
| L6 | Mittel | Controller-Einrichtung und Desktop-Alltag verbessern | Mittel |
| L7 | Laufend | Linux-CI, echte Spieltests und Release-Qualifikation | Mittel, fortlaufend |

Die Größen sind relative Planungsschätzungen, keine Terminzusagen. L1 ist der
erste Umsetzungsschritt. L7 begleitet jede Änderung; L3 liefert Messwerte für L4.

## L1 – Datenablage und Spielstände

**Befund:** `WaylandEmulatorHost.TryLoadRom` übergibt eine `.sav` neben der ROM an
die Runtime; `LinuxSaveStateStore.GetPath` bildet `.ss1` bis `.ss5` ebenso. Ein
schreibgeschützter ROM-Ordner ist damit zum Speichern ungeeignet. Gleichnamige ROMs
mit verschiedenen Endungen im selben Ordner erhalten dieselben abgeleiteten Pfade.

**Plan:** Einen Linux-Pfaddienst einführen und Speicheridentitäten aus dem SHA-256
des ROM-Inhalts bilden. Der Runtime können ROM- und Save-Pfad bereits getrennt
übergeben werden. Vorgeschlagene Ablage:

| Basis | Inhalt unter `aetherboy/` |
| --- | --- |
| `$XDG_CONFIG_HOME` | `settings.json`, letzte lesbare Einstellungssicherung |
| `$XDG_DATA_HOME` | `saves/<hash>/`, `states/<hash>/`, `firmware/`, optionale importierte `roms/<hash>/`, Bibliothekskatalog |
| `$XDG_STATE_HOME` | Sitzungsberichte, Crashlogs und zuletzt geöffnete Spiele |
| `$XDG_CACHE_HOME` | Wiederherstellbare Vorschaubilder und andere Caches |

Diese Zuordnung ist eine Projektentscheidung auf Basis der
[XDG Base Directory Specification](https://specifications.freedesktop.org/basedir/latest/).
Leere oder relative XDG-Werte erhalten die spezifizierten Standardpfade.
Spielstände gehören als wertvolle, übertragbare Nutzerdaten in die Datenablage.

Bestehende `.sav`-, RTC-, Guard-, Backup- und State-Dateien beim ersten Öffnen
kontrolliert übernehmen. Zentrale Dateien haben Vorrang, Originaldateien bleiben
erhalten. Unterbrochene Migrationen müssen wiederholbar sein; widersprüchliche
Altbestände müssen sichtbar werden. ROM-Kopien können später als expliziter
Bibliotheksimport hinzukommen. Programmdateien und Nutzerdaten getrennt halten:
Der bisherige Installer verwendet bereits `$XDG_DATA_HOME/aetherboy` als Programmziel.
Für neue Installationen einen eigenen Programm-Unterordner vorsehen und alte
Installationen gezielt migrieren.

**Abnahme:** Speichern aus einem schreibgeschützten ROM-Ordner; identische ROM nach
Verschieben wiedererkennen; gleichnamige unterschiedliche ROMs getrennt halten;
Migration nach Abbruch erneut ausführen; vorhandene zentrale Saves erhalten;
beschädigte Einstellungen aus Sicherung laden. Zwei Instanzen dürfen dieselben
Spielstände nicht unbemerkt überschreiben: Schreibbesitz pro ROM festlegen und testen.

## L2 – Installation und Updates

**Befund:** `run-linux.sh` erkennt ein benutzerlokales .NET über PATH und DOTNET_ROOT.
`install-linux-user.sh` installiert dagegen einen direkten Symlink auf den Apphost.
Der Desktop-Eintrag verwendet `Exec=aetherboy %f` und `TryExec=aetherboy` und hängt
damit vom PATH der grafischen Sitzung ab. Außerdem wird beim Installieren nur bei
fehlender Ausgabe gebaut; ein älterer vorhandener Publish-Ordner kann übernommen werden.

**Plan:** Gemeinsamen Launcher für Terminal und App-Menü erstellen, installierte
Pfade korrekt quotieren und auf einem minimalen Desktop-PATH testen. Vor Installation
frisch veröffentlichen oder Aktualität verlässlich prüfen. Updates zunächst in einem
separaten Verzeichnis vorbereiten und erst nach erfolgreicher Prüfung aktivieren.
Deinstallation entfernt Programmdateien, Starter und Icons; persönliche Spieldaten
werden nur auf ausdrücklichen Wunsch entfernt.

**Abnahme:** Start aus App-Menü und Dateimanager ohne Terminalumgebung; Pfade mit
Leerzeichen; benutzerlokales SDK; angepasste XDG-Verzeichnisse; Update nach neuem
Commit; fehlgeschlagenes Update erhält die vorige Installation.

Danach ein optionales Paket mit eingebetteter .NET-Runtime für x64/ARM64 bewerten.
Systembibliotheken bleiben zu prüfen. Flatpak oder AppImage erst nach diesen
Grundlagen als separaten Distributionsschritt evaluieren.

## L3 – Diagnose für Linux

**Befund:** Die Oberfläche zeigt Live-Werte, `Program` meldet abgefangene Fehler
primär über stderr. Die neue Windows-Sitzungsaufzeichnung ist dort implementiert;
Linux hat noch keinen entsprechenden persistenten Bericht. Die SDL-Metadaten
enthalten eine fest eingetragene Produktversion.

**Plan:** Produktversion, Commit und Buildkanal aus Buildmetadaten beziehen.
Lokale, begrenzte Development-Berichte mit Compositor, SDL-/Audiotreiber,
ROM-Hash, Sessionzustand, Save-/Load-/Rewind-Ergebnissen und Fehlern ergänzen.
Diagnosedaten müssen auch bei Startproblemen außerhalb eines Terminals auffindbar
sein. Export als ZIP im Control Center, begrenzte Aufbewahrung und Abschaltoption.
Keine ROM-/Save-Inhalte, privaten Dateipfade oder automatischen Uploads aufnehmen.

Das Windows-Berichtsformat als Ausgangspunkt verwenden. Wiederverwendbare
Metadaten und Ereignisschemata plattformneutral halten; Linux referenziert keine
WinForms-Klassen. Gemeinsame Änderungen mit Windows abstimmen und dort testen.

**Abnahme:** Fehler beim Start, beim ROM-Laden und beim Speichern erscheinen in
lokalen Berichten; ZIP lässt sich prüfen; Aufbewahrungsgrenze greift; stable- und
development-Kanal verhalten sich dokumentiert; Logging blockiert keine Emulation.

## L4 – Audio, Frame-Pacing und Ressourcenverbrauch

**Befund:** `SdlAudioOutput` öffnet einen Mono-Stream. Der GBA-Runtime-Adapter mischt
Stereo bereits vor der Ausgabe auf Mono herunter. Bei mehr als etwa 125 ms
Eingangsdaten in der Queue wird diese vollständig geleert. Die Hauptschleife
zeichnet fortlaufend, fordert VSync an und wartet zusätzlich zwei Millisekunden.
Eine messbare Verbesserung ist bisher nicht durch Benchmarks belegt.

**Plan:** Zuerst Queue-Füllstand, Leerungen, Audiofehler, produzierte/präsentierte
Frames, Framezeiten und CPU-Verbrauch erfassen. Queue-Länge allein ist keine
vollständige Ausgangslatenzmessung; SDL beschreibt sie als noch nicht konvertierte
Eingangsdaten: [SDL_GetAudioStreamQueued](https://wiki.libsdl.org/SDL3/SDL_GetAudioStreamQueued).
Dann Pufferstrategie und Vorfüllung verbessern, statt bei Rückstau pauschal zu leeren.
Standardgerät-Wechsel, Abziehen und Suspend/Resume mit SDL testen und fehlende
Fehlerbehandlung gezielt ergänzen. Der bestehende
[SDL-Audiostream](https://wiki.libsdl.org/SDL3/SDL_OpenAudioDeviceStream) bleibt Ausgangspunkt.

Stereo als eigenen gemeinsamen Runtime-Schritt planen: Kanalzahl und Sampleformat
im Audiokontrakt ausdrücken, GBA-Stereodaten erhalten und beide Frontends anpassen.
Eine Änderung nur an `Channels = 1` würde das Problem nicht lösen.

VSync-Ergebnis prüfen, Darstellung bei 60/120/144 Hz messen und bei Pause,
Minimierung und leerem Fenster unnötige Renderarbeit reduzieren. Die
Emulationsgeschwindigkeit muss weiterhin unabhängig von der Monitorrate bleiben.

**Abnahme:** Reproduzierbarer Vorher-/Nachher-Bericht auf derselben Hardware;
mindestens 30 Minuten ohne Audioausfall; Gerätewechsel erholt sich; Pause/Turbo/
Rewind erzeugen keine dauerhaft aufgestaute Ausgabe; geringerer Idle-Verbrauch;
kein beschleunigtes Spiel auf Displays mit hoher Bildrate. Latenzziele erst anhand
der Ausgangsmessung festlegen.

## L5 – Bibliothek und fehlende Werkzeuge

Auf L1 aufbauen und in kleine Lieferungen teilen:

1. Bibliothek mit Suche, zuletzt gespielt, fehlenden Pfaden und Ordnerzugriff.
   Hash-basierte Saves bleiben auch nach Verschieben der Original-ROM zugeordnet.
2. Save Center um Backup-Inspektion, gezielte Wiederherstellung und Import/Export
   ergänzen; vorhandene Core-Schutzmechanismen wiederverwenden. Belegte Slots,
   Zeitpunkte und inkompatible States verständlich anzeigen.
3. Boot-ROM-/GBA-BIOS-Auswahl mit Größen-/Formatprüfung, persistenter Auswahl und
   sichtbarem HLE-/BIOS-Status. Aktuell wird beim Laden immer `bootRom: null` übergeben.
4. WAV-Aufnahme und unterstützte Cheats nachordnen. Die Oberfläche muss die
   tatsächlichen Fähigkeiten des gemeinsamen Kerns widerspiegeln.

**Abnahme:** Spiele aus der Bibliothek starten, verschwundene Quellen neu zuordnen,
Backup ohne Verlust des bisherigen Saves wiederherstellen, Firmware-Auswahl über
Neustart erhalten und unpassende Save-State-/BIOS-Kombinationen klar ablehnen.

## L6 – Controller und Desktop-Alltag

Gamepad-Belegung, Geräteauswahl und einstellbare Stick-Deadzone ergänzen; Profile
stabil einem Gerät zuordnen. Hotplug ist vorhanden, aber das erneute Auswählen
eines schon angeschlossenen Ersatzcontrollers nach Abziehen gezielt prüfen.
Wichtige Aktionen und das Control Center vollständig per Controller bedienbar machen.

Fokusverlust löscht bereits Tasten und Turbo. Darauf eine wählbare automatische
Pause aufbauen, die den vorherigen manuellen Pausezustand erhält. Suspend/Resume,
Monitorwechsel und Skalierungen 100/125/150/200 Prozent unter Hyprland, KDE und
GNOME prüfen. Portal-Abbruch und fehlendes Portal brauchen verständliche Rückmeldung.

**Abnahme:** Zwei physische Controller wechseln und neu verbinden; Belegung bleibt
erhalten; keine klemmenden Tasten; Fokuswechsel pausiert/reaktiviert korrekt;
Bedienelemente bleiben bei gemischter DPI-Skalierung erreichbar.

## L7 – Laufende Qualitätssicherung

Die Linux-CI prüft bereits Core, Runtime, Frontend-Logik und Plattform-Erkennung.
Der native UI-Test ist opt-in und gehört in einen zusätzlichen echten oder
geeignet verschachtelten Wayland-Testlauf. Die bloße Angabe von WAYLAND_DISPLAY
ersetzt keinen Compositor. Den bisherigen schnellen Testpfad beibehalten.

Build-/Installations-Smoketests und ARM64-Ausführung ergänzen. Testuntergrenzen
an die aktuellen Suiten anpassen: Die vorhandenen Linux-CI-Grenzen von 87 Runtime-
und 15 Desktop-Tests liegen deutlich unter dem lokal ermittelten Umfang.
Frontend-Verantwortlichkeiten beim jeweiligen Ausbau aus der inzwischen rund
1.800 Zeilen großen Host-/UI-Kombination herauslösen, etwa Storage, Controller und
Diagnose; kein vollständiger Architekturumbau als Vorbedingung.

Pro Linux-Meilenstein mindestens ein GB-, GBC- und GBA-Spiel auf Boot, Spielablauf,
Audio, Ingame-Save, Neustart, State und Rewind testen. GBA zusätzlich 30–60 Minuten
am Stück. Test-ROMs und synthetische UI-Tests ergänzen diese Spieltests. Jeder
Kernfehler erhält einen reproduzierbaren Regressionstest und eine Prüfung beider
Frontends. Windows-spezifische Tests müssen auf Windows laufen.

## Lokal geprüfte Ausgangsbasis

Umgebung: CachyOS, x64, Hyprland/Wayland, .NET SDK 10.0.302.

- `bash scripts/build-linux.sh`: erfolgreich veröffentlicht.
- Core: 175 Tests bestanden.
- Runtime: 100 Tests bestanden.
- Desktop-Logik: 37 bestanden, ein nativer UI-Test zunächst erwartungsgemäß übersprungen.
- Anschließend mit `AETHERBOY_UI_TESTS=1`: alle 38 Desktop-Tests bestanden,
  einschließlich des nativen Wayland-UI-Tests. Insgesamt 313 bestandene Tests
  über Core, Runtime und die vollständige Desktop-Suite.
- `bash scripts/run-linux.sh --audio-info`: Wayland-/Hyprland-Backend und
  PipeWire-Audio erfolgreich initialisiert. Das ist kein Audio-Langzeittest.

Reale Spiel-/Langzeittests und physische Controller-Tests sind nicht Teil dieser
Planerstellung.

## Verifizierter Implementierungsstand

- 175 Core-Tests, 101 Runtime-Tests und 57 Desktop-Tests inklusive nativem
  Wayland und synthetischem GB/GBC/GBA-Playtest bestehen auf CachyOS/Hyprland.
- Die Desktop-Suite besteht auch mit aktivem CI-Warnungsgate. Virtuelle SDL-
  Controller prüfen Eventrouting, Remapping, Fokus und Hotplug ohne Screenshots.
- Ubuntu 24.04 x64 wurde zusätzlich in einem isolierten Container mit echtem
  Weston-Headless-Compositor und Software-Rendering geprüft: 54 bestanden,
  drei Audio-Playtests dort bewusst übersprungen, keine Fehler. Kein GPU-,
  PipeWire- oder ARM64-Nachweis durch diesen Containerlauf.
- Die vollständige Lösung einschließlich Windows-Zielprojekt baut unter Linux
  mit `EnableWindowsTargeting=true`, ohne Warnungen oder Fehler. Windows wurde
  dabei nicht ausgeführt.
- Der reale Installer besteht isolierte Tests für fehlgeschlagene Migration,
  Leerzeichen/Sonderzeichen, Minimal-PATH ohne dotnet, fehlgeschlagenes Update
  und Deinstallation mit erhaltenen Saves (`scripts/test-linux-install.py`).
- Unabhängiger Kritiker: UI **8,2/10**, Features **7,9/10**. Separater Playtester:
  Features **7,8/10**. Kriterien und Grenzen stehen in den verlinkten Berichten.
- Ein einmaliger Vorher-/Nachher-Lauf des versteckten nativen Wayland-Fensters
  ohne ROM über je acht Sekunden benötigt 0,882 gegenüber 0,211 CPU-Sekunden
  (rund 76 % weniger in dieser Messung). Das ist kein sichtbarer Monitor-,
  Eingabelatenz- oder Gameplay-Benchmark. Werkzeug: `tools/AetherBoy.LinuxIdleProbe`.
  JSON mit Assembly-Hashes: `artifacts/idle-baseline.json`, `artifacts/idle-current.json`.

Die begonnenen 30-Minuten-Läufe sind nicht als bestanden abgenommen. Der Nutzer
übernimmt die Langzeitprüfungen selbst; weitere automatisierte Langläufe wurden
auf seinen Wunsch gestoppt. Details und Start-Builds stehen im Playtest-Bericht.


## Nachtrag: neuer Windows-Commit und Input-Layout

`development` wurde auf `7ef5b59` aktualisiert. Die lokalen Linux-Erweiterungen
bleiben erhalten; gemeinsame Stereo-Verträge, README-Tabellen, Changelog und CI
wurden zusammengeführt. Der Controller-Setup-Button sitzt schmaler über der
Tastenliste und überlappt die Up-Belegung nicht mehr (nativer Capture geprüft).

Die kombinierte Version besteht 181 Core-, 127 Runtime- und 57 Desktop-Tests,
einschließlich kurzer nativer GB/GBC/GBA-Läufe. Der vollständige Build einschließlich
Windows-Zielprojekt hat null Warnungen und Fehler; Windows wurde nicht ausgeführt.
Keine erneuten 30-Minuten-Läufe. Die früheren Bewertungen und Messungen oben
beschreiben weiterhin ihre jeweiligen Prüfstände.
