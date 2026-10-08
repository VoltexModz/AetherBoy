# Gemeinsame Stabilisierungsrunde — 02.10.2026

## Zusammenführung und Push-Prüfung — 08.10.2026

Kollegenbeitrag `641cab0` und Zubehör-/Cheat-Stand `a651885` wurden im Merge
`23d0abf` konfliktfrei zusammengeführt. Die vorhandene Historie bleibt erhalten.
README, Änderungsübersicht und neue app-eigene Texte liegen auf Deutsch/Englisch vor.

Frische isolierte Release-Builds: Windows-Gesamtlösung und Linux-Desktoptests,
jeweils ohne Warnungen/Fehler. Danach Windows-Core **287/287**, Runtime **723
bestanden, 79 übersprungen**, ausgewählte Windows-UI-/Theme-/Sprach-/Cheat-/e-Reader-
Prüfungen **38/38**. Die Runtime-Skips betreffen optionale bereitgestellte ROMs/Karten,
nicht aktivierte native Online-Räume und einen Linux-Symlink-Fall; sie sind kein
neuer Spiel- oder WAN-Nachweis. Unter WSLg/Wayland bestanden **16/16** ausgewählte
Linux-Tests einschließlich aller neuen Local-Link-Shutdown-Fälle des Kollegen,
Cheat-Prüfseite, e-Reader und Themes. SDL verwendete nach EGL/Zink-Warnungen einen
Fallbackrenderer; echte GPU-/Audio-Hardware bleibt getrennt abzunehmen.

Lokale TRX/Builds: `artifacts/prepush-20261008/`. Frühere echte Karten-/Barcode-
Spieltests und deren Grenzen stehen in beiden Roadmaps; sie wurden nicht durch
diesen ROM-freien Integrationslauf ersetzt. Fremde OHVL-Projekte, Archive,
Nutzer-ROMs/Karten/Saves und lokale Testausgaben bleiben außerhalb des Commits.

English: integrated the colleague's save-finalization/Discord/CI changes without
rewriting history. Fresh Windows and Linux builds passed; Core 287, Runtime 723
(79 optional/platform skips), Windows UI selection 38 and Linux Wayland selection
16 passed. This is integration evidence, not a new claim of retail/WAN compatibility.

## Commit-Nachprüfung vom 03.10.2026: Linux-Abschluss, Discord und CI

Die Commits `8b67e5d` (gemeinsame Desktop-Werkzeuge, DE/EN und Speicherabschluss)
und `e108490` (transparente Theme-Logos) sind lokal übernommen. Die Nachprüfung
hat zwei konkrete Fehler und veraltete CI-Testuntergrenzen ergeben:

- Linux-Fenster-`Dispose` kehrte nach begrenzten Wartezeiten zurück, obwohl ein
  Local-Link-Owner noch speichern konnte. Ein kontrolliert blockierter Save
  reproduzierte die Rückkehr nach rund 2013 ms im Zustand `Stopping`. Jetzt
  werden Planung, Start und beide Speicherabschlüsse vollständig abgewartet,
  bevor Diagnose und SDL freigegeben werden. Speicherfehler bleiben auch nach
  vorherigem Stoppen erhalten, werden bereinigt protokolliert und nach dem
  Fenster-Cleanup als Fehler gemeldet. Nur der eigene Start-Abbruch gilt als
  erwartete Cancellation; eine fremde Cancellation beim Speichern bleibt ein Fehler.
- DiscordRPC 1.6.1 startet den IPC-Thread vor Abschluss von `Initialize`.
  Ein sofortiges `READY` konnte dadurch vor dem Bereitschaftsereignis verloren
  gehen. Der Adapter hält die erste Verbindung bis zum Initialisierungsabschluss
  zurück. Ein Parallelstart-Probeaufbau verlor vorher 6 von 256 READYs, danach
  0 von 256. Die Tests steuern Handshake/READY gezielt und prüfen Replay anhand
  der zweiten Verbindung. Keine verlängerten Timeouts oder Wiederholungen.
- CI-Untergrenzen entsprechen jetzt Core **271**, Runtime **646**, Desktop
  **215**, Windows-UI **344** und Linux-Headless **212 ausgeführten** Fällen.
  Linux behält TRX und Diagnostik je Architektur; die .NET- und Headless-Suiten
  können nach Fehlern anderer Tests weiterlaufen, sofern ihr Build erfolgreich war.

Finaler lokaler Release-Build einschließlich Windows-Ziel: **0 Warnungen,
0 Fehler**. Core **271/271**, Runtime mit nativen lokalen Verbindungen
**646/646**, Browser-/Raumserver **8/8**. Desktop-Logik **149 bestanden,
66 übersprungen**; isoliertes Weston/D-Bus **212 bestanden, drei Audio-Hardware-
Skips, kein Fehler**. Darin neun neue native Abschlussfälle mit Save-Inhalt,
Integritätsprüfung, Sperrfreigabe, spätem Start und Fehlerpfaden. Copy-Prüfung:
**0 Prüfpunkte**; vorhandener lokalisierter Fehlertext wiederverwendet.
Rohdaten und TRX: `artifacts/commit-followup-20261003/`.
Der lokale Linux-Build unter `artifacts/AetherBoy-linux-x64/` ist aktualisiert;
Version und Plattformabfrage funktionieren. Der isolierte Installationstest
bestätigt Migration, Fehler-Rollback, Pfade mit Leerzeichen/Sonderzeichen,
Start mit minimalem PATH und Deinstallation unter Erhalt der Test-Spielstände.

Die neue Windows-Laufzeit und ARM64 wurden hier nicht ausgeführt; die vorhandenen
CI-Läufe dieser Commits ersetzen keinen CI-Lauf der lokalen Korrekturen.
Die kontrollierten Verzögerungen belegen den Abschlussvertrag, keine bestimmte
Datenträgergeschwindigkeit. Keine privaten ROMs oder Spielstände verwendet.

## Reparaturrunde vom 03.10.2026: Speichern, Fehleranzeige, Sprache

Die unten dokumentierten Befunde wurden anschließend bearbeitet. Dies ist eine
gezielte Reparaturrunde, keine pauschale Release- oder Hardwarefreigabe.

### Speicherabschluss

- `BatterySaveStore` verschiebt bereits dauerhaft geschriebene, geprüfte ältere
  Sicherungen einschließlich ihrer Integritätsdateien. Der aktuelle Save bleibt
  bis zur Veröffentlichung seines Nachfolgers erhalten; seine neue Sicherung
  wird weiterhin dauerhaft geschrieben. Bei drei geschützten Backups sinkt die
  Zahl der expliziten Daten-/Guard-Flushes pro geändertem Save von acht auf vier.
  Das ist keine Zusage einer halbierten Laufzeit.
- `Flush(true)`, atomare Veröffentlichung einzelner Dateien, drei Generationen,
  `.guard.next`-Wiederherstellung und Besitzer-Sperren bleiben erhalten. Keine
  vorgezogene `Stopped`-Meldung und keine höhere Shutdown-Testfrist.
- Aktive Schreibvorgänge melden Generation, Phase sowie Gesamt- und Phasendauer:
  Prüfung, Rotation, Schreiben, Flush, Handle-Schließen und Veröffentlichung.
  Fehler erhalten diese Phase als Zusatzdaten. Keine Pfade, Spieltitel oder
  Save-Inhalte. Tests decken Rotation, Legacy-Saves und unterbrochene Guard-
  Veröffentlichung ab; kein Stromausfallnachweis oder Mehrdateien-Transaktionsversprechen.

### Fehler und Windows-Lebenszyklus

- Hintergrundfehler beim Speichern von Einstellungen/Profilen öffnen keinen
  automatischen modalen Dialog mehr. Ein eigener dauerhafter Aether-Hinweis bietet
  **Erneut speichern** (asynchron) und bewusst aufgerufene **Details**. Das Schließen
  wartet asynchron auf ausstehende Einstellungen und bleibt bei Fehler abbrechbar.
- Direkter Fenster-`Dispose` fordert ebenfalls den Owner-Abschluss an. Transport
  und Spielstand-Sperre werden erst danach freigegeben; eigener Regressionstest.
- Bereinigte Berichte enthalten innere Fehlertypen, HResults, Methodennamen und
  Speicherphasen, keine rohen Fehlermeldungen mit Pfaden/Tokens. Ein unabhängiger
  Windows-Beobachter erfasst aktive Batterie-Schreibvorgänge ab zwei Sekunden,
  höchstens alle fünf Sekunden, auch bei blockierter Oberfläche.

### Sprach- und Layoutstellen

Die acht konkreten Sichtprüfungsbefunde unten sind korrigiert: anpassbare
Hauptfensterbuttons, zweizeilige Controller-Hilfe, getrennte Linux-Überschrift und
Suchhilfe, umgebrochene Navigation, breitere Patch-Aktionen, getrennte ROM-/Patch-
und Save-Ordnerbeschriftungen, lokalisierte Tastennamen sowie kurzer alter Link-Titel.
Themes und eigene Farben bleiben erhalten; gespeicherte Tastencodes und technische
Kennungen unverändert. Pfeiltasten und Bild-auf/ab werden ausgeschrieben, weil
die Linux-Schrift Pfeilsymbole nicht zuverlässig darstellt. Die neue Fehleranzeige
hat eine scrollbare Statusspalte mit passend breiten Zeilen erhalten.

### Prüfstand nach der Reparatur

- Acht vollständige Runtime-Prozesse in zwei Gruppen zu je vier gleichzeitig:
  **5108 bestanden, 40 übersprungen, kein Fehler**. Die unveränderte 12-Sekunden-
  Grenze wurde nicht erneut überschritten. Darin 1312 protokollierte lokale
  GBA-Link-Abschlüsse: 17–4470 ms, Mittel 118 ms. Rohdaten: `runtime-2373e5d0/`
  und `runtime-2046fa65/` unter `artifacts/audit-20261003/`.
- Core vollständig: **271/271** unter Windows. Die elf Batterie-Speichertests
  bestehen zusätzlich nativ unter Ubuntu. Linux-Desktop unter privatem Weston:
  **202 bestanden, 3 Audio-Hardware-Skips, kein Fehler**; nach den letzten
  Tastennamen erneut bestätigt in `final-linux-desktop/`. Linux-Build ebenfalls
  ohne Warnungen/Fehler. Headless-Ausführung meldet EGL-/GDK-Hinweise wegen
  fehlender physischer GPU/Eingabesitze; das ersetzt keinen Hardwarelauf.
- Acht parallele isolierte Windows-Theme-Prozesse: **88/88**. Fenster-/Notice-
  Teilmenge: **14 bestanden, ein Vordergrund-Skip**.
- Abschließend zwei Windows-Gesamtsuiten nacheinander: jeweils **337 bestanden,
  vier Skips, kein Fehler** (`full-91207e39/`, `full-b712106b/`). Skips: drei
  explizite Hardwareprüfungen sowie echte Vordergrund-Eingabe. Danach letzte
  Tastennamenkorrektur: **19/19 Katalogtests**, **10/10 Windows-Sprach-/Notice-Tests**.
  Windows-Release-Build ohne Warnungen oder Fehler.
- Windows: 46 Ansichten jeweils DE/EN in 100/150 % App-Skalierung, zusätzlich
  DE/Pocket-Light (230 Bilder). Linux: 32 Ansichten in DE/EN, zusätzlich DE mit
  Schriftgröße 18 (96 Bilder). Betroffene Ansichten visuell geprüft; Windows-
  Speicherhinweis nach der Scrollspaltenkorrektur nochmals gerendert und angesehen.
- Ein Zwischenlauf legte einen **Testschleifen-Hänger** offen: nach Fenster-
  Schließen wartete `WindowsLocalLinkLabTests` in nativem `GetMessage` innerhalb
  `DoEvents`, ohne verbliebenen Emulations-Owner im Stack. Die äußere Testdeadline
  konnte deshalb nicht mehr laufen. Der Abschlusstest verwendet jetzt die echte
  `Application.Run`-Schleife mit eigener Deadline und separatem Owner-Cleanup.
  Save-Inhalts- und Sperrprüfungen bleiben bestehen. Siehe `full-6456b6b3/stack-0.txt`
  und den abgeglichenen [WinForms-Frameworkpfad](https://github.com/dotnet/winforms/blob/v10.0.0/src/System.Windows.Forms/System/Windows/Forms/Application.LightThreadContext.cs).
- Zwei gleichzeitig gestartete Windows-Gesamtsuiten liefen durch, hatten aber je
  zwei GDI+-Fehler beim Schreiben fester gemeinsamer Test-PNGs (Sofa/Discord/Update).
  Das sind **keine grünen Läufe**; erhalten unter `full-d3a3c4f0/`. Gesamtsuiten
  werden mit diesen bestehenden Screenshot-Tests deshalb nacheinander ausgeführt.

Copy-Prüfung ausgeführt; zwölf Hinweise in betroffenen Quellfiles gelesen
(bestehende Quellschlüssel, kurze Techniklisten und Produkt-/Ordnernamen).
Zusätzlich 183 Hinweise im gesamten Sprachkatalog gelesen, überwiegend bestehende
Kurzlisten und alte Alias-Schlüssel. Neue Hinweistexte und Übersetzungen separat
auf Bedeutung und Breite geprüft; keine pauschale stilistische Katalogbereinigung.

**Weiter beobachten:** Die genaue OS-/Datenträgerschicht der früheren langen
Flush-/Handle-Wartezeiten ist weiterhin nicht bewiesen. Separate Start-/Sitzungs-
journal-Dauermessung, reale Langzeit-Spieltests, Monitor-DPI-Wechsel, Linux/Hyprland-
GPU, Audio und Vordergrund-Eingabe bleiben offen. Keine Aussage über erfolgreiche
Original-Pokémon-Tausche oder WAN-Verbindungen. Keine privaten ROMs/Saves verändert,
kein Commit oder Push.

**English handoff:** Guarded older battery backups are renamed instead of rewritten
and fsynced on every shutdown; the current save stays protected until replacement.
Phase/duration diagnostics preserve privacy. Windows background settings errors use
a persistent nonmodal retry notice; direct form disposal retains save leases until
owner completion. All eight concrete localization/layout findings were repaired
without changing themes or stored keys. Eight full parallel Runtime runs passed
(5108 passes, 40 skips); the original 12-second timeout did not recur. This does
not identify every storage delay or replace physical-device and actual-game tests.

## Nachprüfung vom 03.10.2026: Sichtprüfung und reproduzierter Timeout

**Noch nicht freigegeben.** Dies war eine Diagnose, keine weitere Reparatur.
Produktionscode, Zeitlimits und Speicherschutz wurden dabei nicht geändert.
Die folgenden neuen Nachweise ergänzen die älteren Läufe weiter unten.

### GBA: Timeout erneut beobachtet, diesmal mit Stacks aus den Fehlerläufen

1. Acht parallele, isolierte Prozesse mit `GbaOnlineTests` und
   `LocalGbaLinkSessionTests`: **408/408 Testfälle bestanden**. Darin
   **1312 protokollierte lokale Link-Shutdowns**, 18–3911 ms, im Mittel 233 ms.
   Stacks langsamer lokaler Stopps zeigen `BatterySaveStore` beim dauerhaften
   Schreiben der Sicherungen. Diese positiven Teilmengenläufe allein reichen
   weiterhin nicht als Entwarnung.
2. Danach vier vollständige Runtime-Prozesse gleichzeitig, jeweils 643 Fälle:
   ein Lauf 638 bestanden / 5 übersprungen; drei Läufe je 637 bestanden /
   1 fehlgeschlagen / 5 übersprungen. Native Online-Tests waren in diesem Lauf
   nicht aktiviert (vier Skips), außerdem ein POSIX-Skip.
3. **Zwei dieser Läufe reproduzieren den 12-Sekunden-Shutdown-Timeout** in
   `IndependentGbaOwnersKeepGeometryShouldersSavesAndCooperativePauseWithDelayedWire(0)`:
   `Stopping`, Online `Closed`, kein gemeldeter Owner-Fault. Ein dritter Lauf
   erreicht schon beim Start nicht rechtzeitig zwei emulierte Frames
   (`GbaOnlineTests.cs`, `Until` an Zeile 128). Nicht denselben Fehler daraus machen.
4. Die in genau diesen fehlgeschlagenen Shutdown-Läufen früh erfassten Stacks
   liegen im Pfad `EmulationSession.OwnerThreadMain` →
   `GbaOnlineLinkMachine.Dispose` → `GbaProductionMachine.FlushPersistentState`
   → `BatterySaveStore.RotateValidFiles` / `WriteAtomic`. In Lauf 2 warten beide
   Owner in `FlushFileBuffers` für die Integritätsdateien; in Lauf 3 in
   `SafeFileHandle.ReleaseHandle` beim Schließen der geschriebenen Datei.
   **Der beobachtete Engpass liegt damit in der Save-Finalisierung, nicht mehr
   im seriellen Link-Handshake.** Ein Stack nach zwei Sekunden ist keine
   kontinuierliche Aufzeichnung des gesamten 12-Sekunden-Intervalls. Welche
   Windows-/Dateisystem-/Datenträgerschicht die I/O-Verzögerung verursacht, ist
   damit noch nicht bewiesen; weder defekte Hardware noch Virenscanner behaupten.
5. Die Test-Cleanup verweigert zu Recht das Entfernen noch besessener Dateien.
   Nach Ende beider Testprozesse separat geprüft: beide Original-Test-Saves je
   32768 Bytes vollständig `0xCC`; private Kopien mit den erwarteten ersten
   Bytes `0x21` / `0x72` vorhanden. Keine verlorenen Originaldaten in diesen
   beiden Nachprüfungen. Keine Aussage über echte Spielstände oder Pokémon-Tausche.

Nächste Reparaturarbeit: Start, Backup-Rotation, Daten-/Guard-Flush und
Journalabschluss zeitlich separat erfassen. Backup-Schreibverstärkung und
Finalisierungsablauf gezielt reduzieren, ohne atomare Veröffentlichung,
Integritätsprüfung oder Besitzsperren zu schwächen. Unveränderte GBA-Saves werden
bereits per Hash übersprungen; ein zweiter pauschaler Dirty-Check ist daher
keine belegte Lösung. Ein 12-Sekunden-Testfehler darf nicht durch vorzeitiges
`Stopped`, Save-Verwerfen oder bloßes Heraufsetzen der Deadline verdeckt werden.
Danach denselben vollständigen Lastmix wiederholen, nicht nur die GBA-Teilmenge.

Rohdaten: `artifacts/audit-20261003/gba/` und
`artifacts/audit-20261003/runtime-aeb2eaad/`, insbesondere `run-2` und `run-3`.

### Windows-Hänger bei Themes/Einstellungen

- Der ältere Stack in `artifacts/localization-20261002/theme-observed-recheck/stack.txt`
  zeigt einen **modalen Einstellungs-Speicherfehlerdialog** über
  `frmNano.ReportSettingsSaveFailure`, keinen nachgewiesenen Zeichen-Deadlock.
  Solange niemand den Dialog beantwortet, bleibt der UI-Test dort stehen.
- Frisch: **48 isolierte Theme-Testprozesse, 528/528 Fälle bestanden**; zusätzlich
  vollständige Windows-Suite **331 bestanden / 4 übersprungen**. Die Skips
  betreffen drei explizite Hardwareprüfungen und den echten Vordergrund-Eingabetest.
- Temporärer First-Chance-Observer für die Settings-/Save-Pfade war aktiviert.
  Kein neuer Settings-Speicherfehler in diesen Läufen. Der konkrete Auslöser des
  älteren Dialogs ist damit **nicht geklärt und nicht als repariert abgehakt**.
  Diagnoseverbesserung offen: bereinigte innere Ursache und Speicherphase
  aufzeichnen; automatisierte UI-Tests sollen unerwartete modale Fenster als
  Fehler sichern, statt unbegrenzt auf eine menschliche Antwort zu warten.

Rohdaten: `artifacts/audit-20261003/theme-*/` und `full-68310ee7/`.
Ein früher verworfener Prüfhelfer-Lauf unter `theme/` schrieb Screenshots aus
mehreren Prozessen auf dieselben Namen und verursachte GDI+-Dateikonflikte.
Dieser Helferfehler wurde durch getrennte Ausgabeverzeichnisse beseitigt und
ist **kein Produktfehler**; er ist nicht in den 528 bestandenen Fällen enthalten.

### Sichtprüfung: ursprüngliche Befunde (in der Reparaturrunde oben bearbeitet)

Die echten Windows-Fenster wurden mit isolierten Profilen gerendert:
45 Ansichten jeweils DE/EN bei 100/150 % App-Skalierung, zusätzlich DE mit
Pocket-Light-Theme (225 Bilder). Linux: 32 Ansichten jeweils DE/EN in Schriftgröße
14 und DE in 18 unter privatem Weston/Wayland (96 Bilder). Ausgewählte Bilder
wurden visuell angesehen; Breitenmessungen sind nur Prüfkandidaten, kein Ersatz
für Sichtprüfung. Keine vollständige Abnahme aller dynamischen Zustände,
Monitor-DPI-Wechsel, IME, Screenreader oder echter Linux-GPU-/Hyprland-Hardware.

- [x] **Windows, Hauptfenster:** `Schnellmenü` / `Quick menu` wird am unteren
  Aktionsbutton abgeschnitten, auch bei 150 % App-Skalierung.
- [x] **Windows, Controller-Tastatur:** deutscher Bedienhinweis in der festen
  Kopfzeile endet vor dem vollständigen LB/RB-Hinweis. Mehrzeilig oder passend
  aufgeteilt anzeigen; nicht nur Schrift verkleinern.
- [x] **Linux, Einstellungen:** deutsche Überschrift und Suchhinweis kollidieren
  am festen X-Abstand (`WaylandEmulatorHost.AetherUi.cs`, Positionen 92/242).
  Bei großer Schrift wird außerdem der untere Navigationshinweis gekürzt.
- [x] **Linux, Patch Lab:** `Aktuelles Spiel verwenden` wird bereits bei normaler
  Textgröße im schmalen Button abgekürzt. Kürzere eindeutige Beschriftung oder
  mehr Platz vorsehen.
- [x] **Gemeinsam, Kontext der Übersetzung:** Katalogalias `ROM WÄHLEN` zeigt
  im Windows Local Link Lab fälschlich `Basis-ROM wählen` / `Choose base ROM`.
  Den Patch-Kontext von einer normalen ROM-Auswahl trennen. Technische IDs bleiben gleich.
- [x] **Windows, Ordnerseite:** Batterie-Saves und Save States haben beide die
  Buttonbeschriftung `Spielstände anzeigen`; die Erklärungen unterscheiden sie,
  die Aktion selbst sollte ebenfalls eindeutig sein.
- [x] **Tastennamen DE, beide Plattformen:** angezeigte Namen wie Up/Down/Left/Right
  (Linux außerdem Return/Backspace/Space) sind noch native API-Namen. Für die
  gewünschte vollständig deutsche Bedienoberfläche reine Anzeigebezeichnungen
  ergänzen; gespeicherte Keys/Scancodes ausdrücklich unverändert lassen.
- [x] Niedrige Priorität: Der alte deaktivierte `frmLink`-Entwurf kürzt seine
  sehr lange Titelzeile. In den aktuellen `frmNano`-Dateien kein Aufruf gefunden;
  nicht mit dem aktiven Local Link Lab oder Online-Raumdialog verwechseln.

Beispielbilder liegen unter `artifacts/audit-20261003/visual-*/` sowie
`linux-visual-*/`. Der Linux-Helfer initialisiert die Sprache vor dem Host exakt
wie `Program.Main`; ein anfänglicher englischer Status durch fehlende Initialisierung
im Helfer wurde ausgeschlossen und die Bilder neu erzeugt. Fremde technische
Fehlermeldungen, Eigennamen und Dateipfade sind keine pauschal zu übersetzenden UI-Texte.
Die Hilfsprogramme und Fehleraufzeichnungen bleiben lokal im ignorierten
`artifacts`-Ordner. Kein Commit, Push, Release oder Servereingriff.

**English handoff:** Two full parallel Runtime runs reproduce the 12-second GBA
online shutdown timeout. Stacks from those failing runs show backup/guard flush
and file-handle closure during private save finalization. The exact underlying
OS/storage delay is not yet identified. Original synthetic saves survived unchanged.
48 theme processes and the full Windows UI suite passed, but the historical modal
settings-save failure is still unexplained. Visual review found clipped action/help
text, a German Linux header collision and context-sensitive localization leftovers.
This update records diagnosis only; no product fixes or time-limit changes.

## Nachuntersuchung: GBA-Shutdown unter Last

**Erneutes Auftreten während der Sprachvervollständigung:** Der vollständige
Runtime-Lauf `artifacts/localization-20261002/complete-runtime-final/` meldete
bei `IndependentGbaOwnersKeepGeometryShouldersSavesAndCooperativePauseWithDelayedWire(0)`
einen 12-Sekunden-Timeout: Owner `Stopping`, Online `Closed`, keine Fault-Meldung.
Das noch besessene Testverzeichnis wurde absichtlich nicht entfernt. Aus diesem
Status allein folgt weder ein CPU-Deadlock noch ein bestimmter Save-Fehler.
Die Online-Testhilfe kann jetzt ebenfalls mit `AETHERBOY_SHUTDOWN_STACK_TOOL`
nach zwei Sekunden einen Stack erfassen. Der folgende vollständige beobachtete
Lauf bestand mit 643 Tests und einem POSIX-Skip, ohne erneuten langsamen Online-
Shutdown. **Der Timeout ist weiterhin offen, nicht durch den Nachlauf repariert.**
Keine Shutdown-Deadline erhöht, kein Save verworfen, keine Sperre vorzeitig gelöst.

Der nachfolgende ursprüngliche Bericht bleibt als Ausgangsnachweis erhalten.
Die späteren Pakete müssen nach diesen Änderungen neu gebaut werden.

- Zunächst **16 × 81 Fälle** mit vier parallelen Prozessen: alle bestanden.
- Zusätzliche GBA-Stressfälle: vier Baudraten, je 40 Starts/Stops mit wechselnden
  Speicherinhalten, drei gleichzeitige Shutdown-Aufrufer, Prüfung beider Saves
  und der Freigabe beider Schreibsperren. Nur selbst erzeugte ARM-Testprogramme.
- Acht parallele Prozesse, jeweils 17 Fälle: alle bestanden, **1312 beobachtete
  Shutdowns**. Separate Beobachter-Threads erfassen die Owner-Phase; bei mehr als
  zwei Sekunden erzeugt optional `dotnet-stack` einen echten Prozess-Stack.
- Die erfassten langen Stopps liegen in `FileStream.Flush(true)` über
  `BatterySaveStore.WriteAtomic` / `GbaProductionMachine.Dispose`, also beim
  dauerhaften Schreiben. Kein Kabel-/CPU-Deadlock in diesen Aufzeichnungen.
  Der historische 15-Sekunden-Timeout selbst wurde dabei nicht reproduziert;
  seine identische Ursache ist deshalb **plausibel, nicht abschließend bewiesen**.
- Gezielte Optimierung: temporäre Save- und Guard-Dateien verwenden nicht mehr
  zusätzlich `WriteThrough` für jeden Schreibzugriff. Jede vollständige Datei
  wird weiterhin **vor** dem Austausch ausdrücklich mit `Flush(true)` dauerhaft
  geschrieben. Backup-Rotation, Prüfsummen, `.guard.next`-Wiederherstellung und
  Besitzsperren bleiben unverändert. Keine Zeitlimits erhöht oder Saves verworfen.
  Grundlage: [.NET FileStream.Flush(Boolean)](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0).
- Derselbe Acht-Prozess-Lauf nach der Änderung: alle 136 Testfälle bestanden,
  erneut 1312 beobachtete Shutdowns; etwa **56 statt 90 Sekunden je Testprozess**.
  Maximal beobachtet nach Änderung: 4262 ms. Das ist ein lokaler Belastungsvergleich,
  keine garantierte Obergrenze für langsame/gestörte Datenträger. Die frühere
  Einzelmessung enthielt noch das Warten auf den Stack-Beobachter und ist deshalb
  nicht als präziser Vorher/Nachher-Vergleich einzelner Flush-Zeiten geeignet.
- Lebenszykluskorrektur: auch beim Abbruch aus der Frame-Wartephase wird vor
  Audio-/Kabel-/Save-Finalisierung `Stopping` veröffentlicht. Ein deterministisch
  angehaltener Save-Test prüft: kein vorzeitiges `Stopped`, keine vorzeitig
  freigegebene Schreibsperre, Caller-Abbruch bricht den Save nicht ab.
- Core vollständig: **267 bestanden**. GB/GBC-/GBA-Link-Lebenszyklus nach letzter
  Änderung: **32 bestanden**. Rohdaten und Stacks:
  `artifacts/shutdown-investigation-20261002/` (lokal, nicht im Git).
- Linux-GBA-Gegentest unter WSL/Ubuntu mit `AETHERBOY_SHUTDOWN_STRESS=1`:
  **15 bestanden**, einschließlich 40 Start-/Stop-Runden pro Baudrate in einem
  Prozess. Kein Hardware-/Spielkompatibilitätsnachweis.

Die Messpunkte bleiben erhalten. Ein erneuter Timeout soll mit Stack/Phase
abgeglichen werden, statt allein anhand grüner Folgeläufe als erledigt zu gelten.

## Auftrag und GitHub-Abgleich

1. Gesamttests ausführen.
2. Fehlerfälle und Lebenszyklus prüfen, insbesondere frühere Shutdown-Timeouts.
3. Frische Windows-/Linux-Testpakete mit gemeinsamer Buildkennung bereitstellen.

`origin` wurde abgefragt. Lokales HEAD und `origin/development` stehen auf
`7ced237fbe9f6f2902bc3d38c1965760bc79accc`; keine neuen Remote-Commits gegenüber
unserem Ausgangsstand. Der letzte Commit von xJessyX vom 30.09.2026,
„Raise CI test thresholds and document Linux validation“, ist bereits enthalten.
Er betrifft CI-Testgrenzen und `LINK_CABLE_COMMIT_HANDOFF_2026-09-29.md`.

Die Pakete enthalten die neuere, noch nicht committete lokale Implementierung.
Vorhandene Änderungen und fremde Nebenprojekte wurden nicht zurückgesetzt.
In diesem Auftrag wurde weder committet noch gepusht und kein Server verändert.

## Testnachweise

| Suite | Ausführungsumgebung | Bestanden | Übersprungen |
| --- | --- | ---: | ---: |
| Core vollständig | Windows x64 | 267 | 0 |
| Runtime vollständig, native Online-Tests aktiviert | Windows x64 | 620 | 1 |
| Windows Smoke/UI vollständig | Windows x64 | 325 | 4 |
| Desktop, portable Teilmenge | Windows x64 | 143 | 57 |
| Direct2D und WASAPI, ergänzend zur Smoke-Reihe | Windows x64, echte Geräteinitialisierung | 2 | 0 |
| Core vollständig | WSL Ubuntu 26.04 x64 | 267 | 0 |
| Runtime vollständig, native Online-Tests aktiviert | WSL Ubuntu 26.04 x64 | 621 | 0 |
| Desktop vollständig mit privatem Weston/Wayland und AT-SPI-Bus | WSL Ubuntu 26.04 x64 | 197 | 3 |
| Browser-Brücke und lokaler Raumdienst, Node-Tests | Windows | 8 | 0 |

Windows überspringt den POSIX-Symlinkfall. Die Desktop-Skips auf Windows brauchen
Linux/Wayland. In der ursprünglichen Windows-Smoke-Reihe fehlten drei explizite
Hardwareprüfungen und ein Vordergrund-Tastaturtest. Direct2D und WASAPI wurden
anschließend separat bestanden; der Hardware-Shell-Screenshotfall und die echte
Vordergrund-Tastaturabnahme bleiben offen. Linux überspringt drei ausdrücklich
aktivierungspflichtige Audio-Spieltests. Software-Wayland ist kein Nachweis für
alle echten Linux-GPUs, Audiogeräte oder Hyprland-Konfigurationen.

Der anfängliche vollständige Release-Solution-Build und die Linux-Testbuilds
hatten keine Compilerwarnungen/-fehler. Die eigene synthetische GBA-Probe erzeugte
ein 240×160-Bild, Hash
`0E2B2DF5975F32E5C5D618F1CC99F895D09E2E830FC9998DE6A5CA9AF48EA801`.
Das ist kein Test mit einem kommerziellen Spiel.

### Fehlerfall- und Belastungsrunde

- Drei Runtime-Prozesse parallel, zunächst je 81 Fälle: jeweils 80 bestanden,
  ein Timeout beim Beenden von
  `LocalGbaLinkSessionTests.ArmProgramsExchangeMultiplayerWordsAndExecuteRealSerialInterrupts(0)`.
- Die vier Baudraten dieses Tests bestanden isoliert. Anschließend dreimal je
  **117 Fälle gleichzeitig** mit zusätzlicher `GbaOnlineTests`-Abdeckung:
  alle bestanden. Keine Zeitlimits heraufgesetzt, kein Speicherschutz entfernt.
- Geprüfte Gruppen: GB/GBC-/GBA-Link-Sitzungen, Emulations-Owner, Audio-Warteschlange,
  Aufnahme/Archive, GBA-Online-Lebenszyklus. Die vollständigen UI-Suiten prüfen
  zusätzlich ROM-Wechsel, Abbruch, Zustandsladen, Zurücksetzen, virtuelle Controller
  und Speicherbesitz. Ein virtueller Controller ersetzt keinen physischen Hotplug-Test.
- **Weiter offen:** Ursache des ersten Shutdown-Timeouts unter Parallelbelastung.
  Aus späteren grünen Läufen folgt keine belegte Reparatur. Bei Wiederholung
  Owner-Stack/Phase, Datenträger-Latenz und Threadpool-Auslastung erfassen, bevor
  der gemeinsame Laufzeitcode verändert wird.

### Behobene Prüflücken

Sechs native Linux-Fehlschläge waren veraltete Prüferwartungen:

- AT-SPI: stabilen Rollenwert statt versionsabhängigem Namen „push button“/„button“ prüfen.
- Fehler beim ROM-Vorbereiten und Fortsetzen: aktuelle bereinigte Nutzermeldungen
  prüfen; bestehende Sitzung, Lock-Freigabe und Ablehnung ausgetauschter ROM bleiben geprüft.
- Bibliothek: Details-Button über seine ROM-bezogene Aktionskennung per Tab suchen,
  nicht an einer alten Bildschirmposition. Schutz vor ungewolltem Fokuswechsel bleibt geprüft.
- Texteingabe: tatsächliche Titel-Feldgrenzen statt alter Y-Koordinate verwenden;
  Auswahl, Komposition, Zwischenablage und unverlorene Bearbeitung bleiben geprüft.

Keine Funktionen entfernt oder fehlschlagenden Tests deaktiviert. Nach diesen
Anpassungen besteht die native Desktop-Reihe mit 197/200, den drei Audio-Skips oben.
Lokale CRLF-Zeilenenden der Shellskripte wurden gemäß `.gitattributes` auf LF
normalisiert, damit sie aus dem Windows-Checkout unter Linux laufen.

## Testpakete

Gemeinsame **Buildkennung: `7ced237-20261002-check01-local`**.

Im Ordner `artifacts/stabilization-20261002/packages/`:

- `AetherBoy-Windows-x64-7ced237-20261002-check01-local.zip`
- `AetherBoy-4.8.0-alpha.1-linux-x64-7ced237-20261002-check01-local.tar.gz`
- Linux: begleitende `.sha256`; Windows: vollständiges `SHA256SUMS.txt` im Paket.

Beide enthalten .NET, erforderliche App-Bibliotheken, Quellarchive, Lizenztexte,
`package-info.json` und `TEST_BUILD_CHECKLIST.txt` auf Deutsch/Englisch. Keine ROMs,
BIOS-Dateien oder persönlichen Spielstände beigelegt. Der Paketbau verändert
keine bestehenden Installationen. Nur die vollständigen Archive weitergeben,
nicht einzelne EXE-/DLL-Dateien oder den ganzen Entwicklungsordner.

Windows wird jetzt ebenfalls aus einer isolierten, erlaubnislistenbasierten
Quellkopie gebaut. Die normale NuGet-Abhängigkeitsauflösung wird zuerst im
Locked Mode geprüft; nur deren private Kopie wird für `win-x64` erweitert.
Quell-Sperrdateien und normale Windows-Buildverzeichnisse bleiben dabei unberührt.
Ein fehlerhaftes eigenes Windows-Paket wurde unter `incomplete-windows-license-check`
behalten; das ist **kein auszulieferndes Paket**.

`test-package-pair.py` bestätigt identische gemeinsame **188 Core-/Runtime-/GBA-
Quelldateien**, passend zu Buildkennung/Commit/Kanal, und sämtliche **315 Windows-
Dateiprüfsummen**. Gemeinsamer Quell-Fingerabdruck:
`32c02bc7a4079b6de1d73657033f01e1d696aa4bad8626a76ffeaed60bb9578e`.
Die Plattformprogramme und ihre Laufzeitpakete sind naturgemäß nicht binär identisch
(Windows .NET 10.0.10; Linux .NET 10.0.12).

### Paketprüfung und Start

- Linux-Archiv: Pfad-/Lizenz-/Quellprüfung und Start mit enthaltenem .NET bestanden,
  auch aus einem Pfad mit Leerzeichen und `$`, ohne `dotnet` im PATH.
- Linux-Installer: absichtlich misslingende Migration/Update erhalten alten Stand;
  erfolgreicher Installationslauf, Menüdateiprüfung und Deinstallation erhalten
  die eigenen Test-Spielstände. Alles in isolierten temporären XDG-Ordnern.
- Windows-ZIP separat entpackt, `AetherBoy.exe --portable` gestartet: reagierendes
  Appfenster mit korrekter Buildkennung; ordentlich beendet, Exitcode 0.
  Testprofil ausschließlich unter `artifacts/stabilization-20261002/windows-package-launch/`.
- Neues Logo bleibt in beiden Paketen; Themes und Customize wurden nicht geändert.

Linux-x64 wurde hier auf Ubuntu 26.04 gebaut. Die native Bibliothek benötigt laut
`readelf` u. a. **GLIBC_2.38, GLIBCXX_3.4.32, CXXABI_1.3.15 und OpenSSL 3**.
Diese konkrete Testdatei nicht pauschal für ältere Distributionen freigeben.
ARM64 wurde nicht gebaut/getestet. Weston verwendete Software-Rendering; erwartete
Warnungen zu fehlenden Headless-EGL-Geräten/Keyboard-Seats sind im Rohprotokoll.

## Offene Abnahme, keine Fertigbehauptung

- [ ] Frühen Shutdown-Timeout unter Parallelbelastung kausal aufklären.
- [ ] Kurzen gemeinsamen Checklistenlauf auf echtem Windows und Linux durchführen;
  danach längere Spielsessions, echte Controller und Audiogeräte.
- [ ] Pokémon-Tausch über WAN in den drei Plattformpaarungen separat nachweisen;
  lokale Raum-/Transporttests sind kein Tauschbeweis.
- [ ] Barcode Boy mit eigener Battle-Space-ROM; bleibt ausdrücklich zurückgestellt.
- [ ] Vollständige UI-/DPI-/IME-/Screenreader-Abnahme und ältere Linux-Baselines.

Rohergebnisse: `artifacts/stabilization-20261002/tests/`; insbesondere
`linux/desktop-initial` und `linux/desktop-final`, `linux/runtime` und
`linux/runtime-final`, `stress/run-*` und `stress/recheck-*`. Fehlgeschlagene
Vorläufe werden nicht gelöscht oder als bestandene Prüfungen umgedeutet.

## English handoff

No new upstream commits beyond `7ced237`; local uncommitted features are retained.
Full Windows and native Linux suites were run. Six Linux assertions had stale
labels/coordinates; they now check stable accessibility/action identities and
current sanitized errors without weakening lifecycle or save protections.
Core: 267 passes on each OS. Runtime: 620 + one POSIX skip on Windows, 621 on Linux.
Windows UI: 325 passes + four initial skips; GPU/audio hardware added two passes.
Native Linux desktop: 197 passes + three real-audio skips.

An initial parallel GBA local-link shutdown timeout remains unexplained, although
three expanded concurrent 117-case runs passed afterward. Do not claim it fixed.
Paired self-contained x64 packages use build ID `7ced237-20261002-check01-local`.
Shared-source and Windows hash-manifest verification passed. Linux bundled-runtime
startup and isolated install/update-failure/uninstall tests passed; the extracted
Windows app started with an isolated portable profile and exited cleanly.
Follow the DE/EN checklist in each archive. Real WAN trades, commercial-game
compatibility, Barcode Boy/Battle Space and complete hardware/UI acceptance are
still separate work. No commit, push or release publication was performed.
