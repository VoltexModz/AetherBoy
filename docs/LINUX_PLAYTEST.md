# Linux-Playtest

Prüfdatum: 11. September 2026. Unabhängiger Playtest-Agent; lokaler Arbeitsstand
auf Basis `d983ed4`, einschließlich der im aktuellen Durchlauf ergänzten zentralen
Linux-Datenablage. Die 30-Minuten-Abnahme übernimmt der Nutzer selbst.

## Methode und Grenzen

Native SDL3-/Wayland-Fenster auf CachyOS x64 mit Hyprland und PipeWire.
Die automatisierten Szenarien verwenden versteckte echte SDL-Fenster und rufen
gezielt Host-Aktionen auf. Dadurch werden echte Runtime, Speichern, Audiostreams
und der Host getestet, ohne bildbasierte Mausklick-Schleifen. Der Playtest erzeugt
**keine Screenshots**; die visuelle Bewertung erfolgt separat durch den UI-Kritiker.
Der vorhandene native UI-Test ergänzt Pointer-Koordinaten, Tastaturnavigation und
Darstellung. Direkte Host-/Runtime-Aufrufe ersetzen keinen physischen Eingabetest.

Alle ROMs werden im Test aus kleinen Programmen erzeugt. Der GB/GBC-Test isoliert
alle vier XDG-Verzeichnisse, beide Tests verwenden temporäre Einstellungen und
temporäre App-Daten. Persönliche ROMs und Saves werden nicht verändert.
Im Projekt einschließlich des dokumentierten `.local-assets`-Orts und im
vorhandenen Verzeichnis `~/Games` wurden keine GB-/GBC-/GBA-ROM-Dateien gefunden.

Audio läuft über das echte PipeWire-Gerät bei Ausgabelautstärke null. Nichtleere
Audiobatches und beim GB/GBC-Programm tatsächlich von null verschiedene Samples
werden geprüft. Dies belegt den digitalen Ausgabepfad; Hörqualität, Knackser,
Gerätewechsel und physische Ausgangslatenz sind damit nicht gemessen.
Das 10-ms-Polling des versteckten Hosts entspricht nicht der vollständigen
Darstellungs-Hauptschleife und ist kein Monitor-/VSync-/Frame-Pacing-Benchmark.

## Bisherige Ergebnisse

Die vor Beginn dokumentierte Basis umfasst 175 Core-, 100 Runtime- und
38 Desktop-Tests inklusive nativer UI-Prüfung. Die folgenden Messwerte stammen
aus dem neuen unabhängigen Playtest und sind keine übernommenen Projektangaben.

| Szenario | Ergebnis | Beleg |
| --- | --- | --- |
| GB, 10 Sekunden reguläres Tempo | Bestanden: 59,58 emulierte fps, 440.320 Audiosamples, 3,33 s Prozess-CPU | `artifacts/playtest-current/AetherBoy.DesktopTests_net10.0_x64.trx` |
| GBC, 10 Sekunden reguläres Tempo | Bestanden: 59,66 emulierte fps, 440.320 Audiosamples, 2,61 s Prozess-CPU | Aktueller TRX-Bericht |
| Fortschritt | Jede der neun vollständigen 1-s-Stichproben pro Kurzlauf schreitet voran; keine Session-/Audiofehler | Neue Tests |
| Pause / Turbo | Pause hält Framezähler an; Turbo schaltet ein und zurück | Beide GB/GBC-Läufe |
| Save State / Load State / Rewind | Host-QuickSave/QuickLoad und Runtime-Rewind erfolgreich | Beide GB/GBC-Läufe |
| Ingame-Batterie / Neustart | ROM schreibt `0x42` in Cartridge-RAM; nach Host-Neustart liest ROM denselben Wert und persistiert ihn in einer zweiten RAM-Zelle | Beide GB/GBC-Läufe |
| Native GBA-Prüfung | Bestanden: 59,59 fps, 652.672 Audiosamples in 10,00 s; native Ausgabe mit zwei Kanälen, rote Mode-3-Pixel, State/Load/Rewind | Aktueller TRX-Bericht |
| Spielereingabe | Konfigurierte A-Taste gelangt über Host und JOYP in das generierte GB/GBC-Spiel; Neustart löscht den Tastenzustand | Beide GB/GBC-Läufe |
| ROM verschieben | Bereits laufende ROM wird verschoben; neuer Pfad repariert Bibliothek ohne Austausch der Session; Saves über Neustart erhalten | Beide GB/GBC-Läufe |
| Batterie wiederherstellen | Falsche Importgröße abgelehnt; importiertes `0x66` wird nach Neustart tatsächlich vom ROM gelesen; vorheriges `0x42` bleibt im ZIP erhalten | Beide GB/GBC-Läufe |
| Bibliothek und Tastaturfokus | Defekte Katalogeinträge übersprungen; geschlossene Suche fängt keine Spieltasten ab; Tab → Pfeil → Enter bindet richtige Aktion | Aktueller TRX-Bericht |
| Firmware und Controllerprofil | Ungültige Firmwaregröße abgelehnt; Import und Auswahl erhalten dieselben Bytes; Controllerbelegung tauscht Duplikate und persistiert pro Gerät | Aktueller TRX-Bericht; keine echte Firmware-/Controller-Ausführung |
| Virtuelle SDL-Controller | Echte SDL-Hotplug-/Button-/Axis-Events prüfen Remapping, Deadzone, Menü, Ersatzcontroller und gespeicherte Wiederverbindung; eingespeiste Fokusereignisse prüfen Turbo-Stopp/Autopause/manuelle Pause | `artifacts/playtest-controller.trx`; prozesslokale virtuelle Geräte, keine physische Hardware |
| Storage-Grenzen | Schreibgeschützter ROM-Ordner, kontrollierter Migrationsabbruch und sichere Wiederholung bestanden | Aktueller TRX-Bericht |
| Installation | Alter Layout-Wechsel bei Fehler sicher; Pfade mit Leerzeichen/`$`; Desktop-Eintrag validiert; Minimal-PATH ohne `dotnet`; fehlgeschlagenes Update erhält aktive Version; Deinstallation erhält Save-Datei | `scripts/test-linux-install.py`, `artifacts/linux-install-test.log` |
| Diagnose und WAV | Private Exception-Nachrichten bleiben aus Exporten; deaktivierte Diagnose und begrenzte Aufbewahrung geprüft; WAV-Header und Stereoaufzeichnung geprüft | Aktueller TRX-Bericht |
| Leerlauf-Hauptschleife | 8-s-Probe ohne ROM im versteckten Wayland-Fenster: Prozess-CPU von 0,882 s auf 0,211 s gesunken | `artifacts/idle-baseline.json`, `artifacts/idle-current.json`, jeweils mit Assembly-Hash |
| GB, geplante 30 Minuten | **Nicht abgenommen:** nach ungefähr 24 Minuten durch SIGTERM beendet (Exit 143); keine Testfehlerausgabe, kein abschließender TRX-Bericht. Ursache nicht belegt. | `artifacts/playtest-soak.log` |
| GBA, geplante 30 Minuten | **Nicht abgenommen:** auf ausdrücklichen Nutzerwunsch vor Ablauf gestoppt. Der Nutzer übernimmt die Langzeitprüfung selbst. Kein abschließender TRX-Bericht. | `artifacts/playtest-gba-soak.log` |

Der aktuelle TRX-Bericht enthält **57/57 bestandene Desktop-Tests**, einschließlich
der drei nativen Audio-/Emulationsszenarien und des virtuellen Controller-Tests.
Dieser bestand zusätzlich mit `AETHERBOY_UI_TESTS=1` und `AETHERBOY_PLAYTEST=0`
in 1,442 s (`artifacts/playtest-controller-ci.trx`). Der letzte Release-Build mit
`ContinuousIntegrationBuild=true` und `-warnaserror` hat null Warnungen/Fehler.
Der erste GB/GBC-Kurzlauf vor den weiteren
Änderungen liegt zusätzlich in `artifacts/playtest-short.trx` (59,67 bzw. 59,77 fps).
Die Langläufe halten bewusst ihren Start-Build fest. Der abgebrochene GB-Lauf
belegt keinen bestandenen 30-Minuten-Test und wird nicht als Emulatorfehler
gewertet: Das vorliegende Signal belegt allein die Beendigung des Testprozesses.
Später ergänzte UI-/Werkzeug-
Änderungen werden durch aktuelle Kurztests geprüft; die Langläufe dürfen nicht als
30-Minuten-Nachweis exakt des endgültigen Arbeitsstands dargestellt werden.

SHA-256 der Desktop-Assembly des GB-Langlaufs:
`5a1e920d4a727e1907ffee00d8e09bcbf960c8a4a228cad5108b035fbbb32ef1`.
SHA-256 der Desktop-Assembly des GBA-Langlaufs:
`a196c2b92fc3415eba399be654567009e819cc979126e785be705dca72fe27a1`.

CPU-Werte sind die gesamte im Testprozess verbrauchte CPU-Zeit, nicht die
Prozentanzeige eines Kerns und nicht isolierter Emulatorverbrauch. Nebenläufige
Entwicklungsarbeit kann die Messung beeinflussen. Es gibt noch keinen belastbaren
Vorher-/Nachher-Vergleich für die Audio-Ausgangslatenz. Die separate 8-s-Probe
misst eine konkrete Verbesserung der Leerlauf-Hauptschleife; sie ist kein
umfassender Energieverbrauchs- oder Spielperformance-Benchmark.

## Bewertung der vorhandenen Funktionen

Bewertung des geprüften Linux-Funktionsumfangs: **7,8 / 10**.
Die Zahl beurteilt Bedienbarkeit und Zuverlässigkeit der vorhandenen Funktionen
mit den unten ausdrücklich begrenzten Belegen; sie ist keine Kompatibilitätsquote
und keine Release-Freigabe. Nicht implementierte geplante Zusatzwerkzeuge werden
als Roadmap-Lücken ausgewiesen. Nicht ausgeführte Tests werden nicht als bestanden
eingerechnet.

Bewertungsraster: 0 = unbrauchbar; 3 = große Funktionsausfälle; 5 = grundsätzlich
nutzbar mit wesentlichen Lücken; 7 = tragfähiger Alltagspfad mit bekannten Grenzen;
9 = umfangreich in echten Nutzungssituationen bestätigt; 10 = außergewöhnlich
vollständig und umfassend abgesichert. Der gewünschte Schwellenwert >6 ist ein
Abnahmewunsch, kein Grund, ungeprüfte Funktionen besser zu bewerten.

| Bereich | Gewicht | Punkte | Begründung |
| --- | ---: | ---: | --- |
| GB/GBC-Ausführung | 25 % | 8,0 | Native Ausgabe und zwei synthetische Produktionsläufe funktionieren; dokumentierte Core-Regressionsbasis vorhanden. Aktueller echter Spielverlauf fehlt. |
| Spielstände und Timeline | 25 % | 8,5 | Persistierte Batterie über Neustart, QuickSave/QuickLoad, Rewind, Restore und Vorher-ZIP geprüft; read-only ROM-Pfade und unterbrochene Migration bestehen. |
| Audio und Tempo | 15 % | 7,5 | PipeWire erhält Samples ohne gemeldete Fehler, reguläres Tempo aller drei Plattformen passt; GBA-Stereo bis zum geöffneten SDL-Stream geprüft. Hörqualität, Gerätewechsel und Monitorraten bleiben offen. |
| Linux-Bedienung und Einstellungen | 15 % | 7,5 | Native Navigation, Input-Fokus, geschlossene Suche, ROM-Umzug, Firmwareauswahl und Controllerprofile geprüft; keine physischen Controller-/gemischten DPI-Tests. |
| GBA-Ausführung | 10 % | 6,5 | Synthetischer nativer Produktionslauf mit Video, Audio, State und Rewind besteht zusätzlich zur Runtime-Regressionsbasis. Reale Spielsessions fehlen weiterhin. |
| Installation und Diagnose | 10 % | 7,5 | Isolierter echter Installer besteht Migration-/Updatefehler, Sonderzeichen, Minimal-PATH und datenerhaltende Deinstallation; lokale Diagnose und ZIP vorhanden. Weitere Distributionen und ARM64 sind nicht praktisch freigegeben. |

Gewichtetes Ergebnis: 7,775, gerundet als 7,8 ausgewiesen. Jeder hier bewertete
Funktionsbereich liegt über 6. Die Bewertung bezieht sich auf die vorhandenen
Funktionen im beschriebenen Prüfrahmen; offene reale Spiel-/Hardwaretests und die
nicht abgeschlossenen Langzeitprüfungen werden dadurch nicht zu bestandenen Abnahmen.
Der separate UI-Kritikbericht verwendet eine eigene, unveränderte Rubrik und kann
deshalb eine leicht andere Funktions-Gesamtnote nennen.

## Reproduzieren

Einmal bauen, anschließend gezielt ohne Build starten:

```bash
dotnet build tests/AetherBoy.DesktopTests/AetherBoy.DesktopTests.csproj -c Release --no-restore
AETHERBOY_PLAYTEST=1 dotnet tests/AetherBoy.DesktopTests/bin/Release/net10.0/AetherBoy.DesktopTests.dll \
  --filter FullyQualifiedName~LinuxPlaytestTests --output Detailed --show-stdout All \
  --report-trx --report-trx-filename "$PWD/artifacts/playtest-short.trx"
```

Für 30 Minuten pro GB/GBC-Szenario zusätzlich
`AETHERBOY_PLAYTEST_SECONDS=1800` setzen; für GBA entsprechend
`AETHERBOY_GBA_PLAYTEST_SECONDS=1800`. Einzelne DataRows können anhand der aktuell
mit `--list-tests json` gelieferten UID über `--filter-uid` ausgewählt werden.
Eine UID nicht blind aus einem älteren Build übernehmen. Bei parallelem Ausbau
den kompletten Test-Ausgabeordner kopieren und diese Kopie starten, damit der
Langlauf eindeutig bei einem Buildstand bleibt.

Ohne `AETHERBOY_PLAYTEST=1` werden die drei Audio-/Zeit-abhängigen Szenarien bewusst
übersprungen. Der schnelle virtuelle Controller-Test darf alternativ mit
`AETHERBOY_UI_TESTS=1` laufen und benötigt kein Audio-Gerät. Ein CI-Lauf ohne die
Audio-Szenarien gilt nicht als ausgeführter Audio-/Langzeitplaytest.

## Noch benötigte praktische Abnahme

1. Rechtmäßig vorhandene GB-, GBC- und GBA-Spiele: Spielstart, tatsächliche
   Eingaben, mindestens ein sinnvoller Spielabschnitt, Ingame-Save und Neustart;
   GBA 30–60 Minuten. Keine ROM-Inhalte in Berichte oder Commits aufnehmen.
2. Audio hörend kontrollieren, Standardgerät wechseln, abziehen und
   Suspend/Resume prüfen. Lautstärke null im Automaten ersetzt das nicht.
3. Zwei physische Controller wechseln; Hotplug, Deadzone, Belegung und
   Fokusverlust prüfen. L/R-API-Aufrufe bestätigen keinen Controller.
4. Sichtbare App unter 60/120/144 Hz sowie gemischter DPI-Skalierung prüfen,
   Hauptschleifen-CPU bei Pause und ohne ROM vergleichen.

Diese Grenzen lassen sich ohne geeignete Hardware bzw. reale Spiele nicht durch
mehr Screenshots oder höhere synthetische Testzahlen beseitigen.
