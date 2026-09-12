# GBA Local Link — Entwicklung und Übergabe / development handoff

Stand / status: 12 September 2026. Lokaler, noch nicht veröffentlichter Stand.
Bedienung und Save-Pfade: [Local Link Lab](LOCAL_LINK_LAB.md).
Linux-Beitrag: [Integrationsprüfung](LINUX_UPSTREAM_INTEGRATION_REVIEW.md).

## Deutsch

### Was hinzugekommen ist

- Das Windows-Link-Lab akzeptiert jetzt zwei `.gba`-Dateien, dieselbe ROM zweimal
  oder unterschiedliche kompatible Spiele. Gemischte GB/GBC-GBA-Paare werden vor
  dem Start abgewiesen. Zwei Ansichten erhalten jeweils 240×160 native Pixel;
  beim Wechsel zurück zu GB/GBC werden Puffer und Darstellung auf 160×144 gesetzt.
- L/R ergänzen die bestehenden Tasten. Tastaturwahl, zwei Controller, gemeinsame
  Pause, Fokus-Pause, Kabeltrennung, Audio aus/P1/P2 und geordnetes Beenden bleiben
  im gleichen Ablauf. Ein getrennt gestartetes Programm wird nicht verbunden.
- `LocalLinkSession` nutzt `LocalLinkMachine` für die beiden Hardwarefamilien.
  GBA-Geräte laufen auf einem Owner-Thread abwechselnd je einen CPU-Takt;
  280.896 Takte entsprechen einem Bild. Vor der Kopplung wird kein anfänglicher
  Rewind-Snapshot aufgenommen, der nur ein Gerät vorwärts laufen lassen würde.
- GBA-SIO unterstützt im lokalen Paar Normal-8-/32-Bit und Multiplayer-16-Bit:
  RCNT-Moduswahl, vier Baudraten, IDs, Ready/Busy/Error, Empfangsslots und Serial-IRQ.
  Der Eltern-Port startet Multiplayer; nicht besetzte Empfangsslots liefern FFFF.
  Transferzeit wird im emulierten Scheduler gemessen, nicht über Host-Timer/TCP.
- Ein alter Mehrinstanzfehler wird behoben: mehrtaktige CPU-Befehle besaßen globale
  Zwischenwerte. Diese gehören nun zu genau einem `Core`, einschließlich LDR/STR,
  LDM/STM, Multiply/Long-Multiply sowie ARM-/Thumb-BL-, SWP- und ALU-Zwischenständen.
  Das verhindert, dass ein Gerät die gerade laufende Instruktion des anderen ändert.
  Der Fix ist auch für unabhängig parallel laufende GBA-Geräte relevant.

### Sicherheit und Kompatibilität

Beide Saves bleiben unter exklusivem Schreibbesitz. Gleiche ROM-Inhalte verwenden
für P2 einen dauerhaft getrennten `LinkPlayer2`-Save. Es wird weder ein Save geklont
noch ein externer `.sav`/RTC/State ungefragt übernommen. Einzelne Save States,
Rewind, Turbo und Reset sind im Lab gesperrt; unilateral geladene Zeitstände
würden die Paarung desynchronisieren. Normales Speichern **im Spiel** bleibt möglich.

Der innere GBA-Gerätezustand wird als **Schema 6** geschrieben; **Schema 5** wird
weiter gelesen. Das ergänzt die bisher fehlenden Multiplayer-Empfangsslots und
behandelt alte Normaltransfer-Zustände unter dem zuvor ignorierten GPIO-Modus.
Alte Programmversionen können neue Schema-6-States nicht lesen. Batterie-`.sav`
bleibt unverändert. Während ein GBA-Gerät an einem lokalen Kabel hängt, werden
Capture/Restore vor einer Zustandsänderung abgewiesen, auch bei logisch gezogenem
Kabel. CPU-Zwischenwerte müssen nicht in States aufgenommen werden: der bestehende
Snapshot-Vertrag stabilisiert zuvor auf einer abgeschlossenen Instruktion.

GBA ist als regulär auswählbares System in der Anwendung integriert, mit Save
States, Batterie-Saves, Rewind und weiteren Einzelspieler-Werkzeugen. Das ist
**kein Nachweis gleicher Emulationsgenauigkeit oder Spielekompatibilität** wie
GB/GBC. Das Projekt dokumentiert weiterhin einen Alpha-/experimentellen Status.
Neu entwickelter Multiplayer ist unabhängig davon experimentell.

Es gibt noch keinen Nachweis eines erfolgreichen Pokémon-Tauschs. Spielmodi und
Spiele müssen untereinander kompatibel sein; etwaige spielinterne Voraussetzungen
werden durch ein Kabel nicht aufgehoben. Kein GBA-Wireless, Joybus, Netzwerk,
Vier-Spieler-Modus oder GB↔GBA-Kabel. Das Lab nutzt integrierten Boot/HLE und globale
Profile; eine individuelle BIOS-/Profil-Auswahl je Spieler ist noch nicht angebunden.
HLE-Zeitannahmen und reale Langläufe bleiben zu qualifizieren.

### Orientierung und Herkunft

Der vorhandene GBA-Kern bleibt eine Weiterentwicklung der eingebundenen
GBADotnet-Basis; seine ursprüngliche Herkunft und Lizenz verschwinden dadurch nicht.
Die neuen lokalen Verbindungs-/Runtime-Anpassungen sind C#-Entwicklungsarbeit in
AetherBoy. mGBA wurde für Architektur und Register-/Timing-Verhalten geprüft,
nicht als neuer zweiter Kern hineinkopiert:

- [mGBA GBA SIO](https://github.com/mgba-emu/mgba/blob/master/src/gba/sio.c)
  unterscheidet die GBA-Modi; ein generischer GB-Byte-Austausch reicht dafür nicht.
- [mGBA GBA lockstep](https://github.com/mgba-emu/mgba/blob/master/src/gba/sio/lockstep.c)
  dient als Referenz für emulierte Transferereignisse und koordinierte Geräte.
- [mGBA Qt MultiplayerController](https://github.com/mgba-emu/mgba/blob/master/src/platform/qt/MultiplayerController.cpp)
  zeigt lokale In-Memory-Koordination und getrennte Save-Zuordnung.
- [GBATEK](https://problemkaputt.de/gbatek.htm#gbacommunicationports)
  beschreibt die GBA-Kommunikationsregister. Architekturvergleich bedeutet keine
  Zusicherung derselben Genauigkeit wie mGBA.

### Linux-Anbindung

Nicht neu implementieren: `LocalLinkSession`, GBA-SIO und CPU-Isolation liegen
bereits in den gemeinsamen Projekten. Linux braucht noch die native Zwei-Spieler-
Oberfläche, separate XDG-Save-Zuordnung und Frontend-Lebensdauer. `Ready` abwarten,
Framepuffer aus `GetVideoGeometry(player)` anlegen, L/R über
`SetGameBoyAdvanceButtonsAsync` senden. Alle Gerätebefehle über die Session;
keine Core-Aufrufe aus UI-/Audio-Callbacks. Audioausgabe beim Spieler-/Sitzungswechsel
neu initialisieren und Playback-Generation weiterhin respektieren.

Der neue Linux-Commit enthält Komfort-/Frontend-Verbesserungen, **keinen** anderen
GBA-Kern und **keine** Link-Oberfläche. Seine Änderungen wurden mit unseren
Audio-, Screenshot-, Performance- und Save-Lock-Verträgen vereinigt.

### Prüfung

Automatisierte Tests verwenden ausschließlich synthetische ARM-/Thumb-/GB-Programme,
Registerfolgen und temporäre Saves. Exakte unterschiedliche Framefarben prüfen
Geräteisolation; zwei lediglich sichtbare Bilder reichen dafür nicht. Echte
Controller, hörbare Spiel-Audiospur, langes Spielen und Pokémon-Tausch sind
gesonderte praktische Tests.

Abschließender Release-Build: **0 Warnungen, 0 Fehler**. Gesamtlösung unter
Windows mit aktivierten WASAPI-/Direct2D-Hardwareproben: **710 Testfälle,
674 bestanden, 36 ausgelassen, 0 Fehler**.

| Suite | Bestanden | Ausgelassen |
| --- | ---: | ---: |
| GB/GBC Core | 197 | 0 |
| Gemeinsame Runtime / GBA | 220 | 0 |
| Windows Smoke / Oberfläche | 178 | 1 |
| Linux-Frontend unter Windows | 79 | 35 |

Die Runtime enthält 39 neue GBA-Link-/Serialfälle und 24 CPU-Isolationsfälle.
Letztere vergleichen echte ARM-/Thumb-Programme in beiden Ausführungsreihenfolgen
mit ihren unabhängig laufenden Gegenstücken, einschließlich Peer-Reset/Restore
während einer mehrtaktigen Instruktion. Vier Baudraten führen in synthetischen
ROMs tatsächlich ARM-IRQ-Code aus und schreiben empfangene Werte in getrennte Saves.
Neun Windows-Fälle ergänzen GBA-Saveplanung, Familienablehnung, L/R-Zuordnung,
exakte individuelle Bildfarben und den Wechsel zurück zur GB-Geometrie.

35 Desktopfälle benötigen Unix/Wayland/GTK/AT-SPI/echte Gerätebedingungen.
Windows verweigerte einem vollständigen Tastatur-bis-CPU-Testfenster den nativen
Vordergrund; deshalb ein ehrlicher Skip, kein umgangener Eingabeschutz. Physische
Controller und hörbare Spiel-Audioqualität sind dadurch nicht bestätigt.
Die übrigen Windows-Oberflächenproben bestanden. Der neue Screenshot
`artifacts/parity-review/local-gba-link-lab.png` zeigt echte PPU-Ausgabe unserer
künstlichen roten/grünen ROMs, kein kommerzielles Spiel.

Die CI-Untergrenzen sind jetzt 710 gesamt, 197 Core, 220 Runtime und 114 Desktop.
Kein Commit/Push ohne Freigabe. Die Upstream-Testzahlen des Linux-Kollegen werden
nicht zu diesen Ergebnissen addiert und ersetzen keinen nativen Nachtest des
vereinten Stands.

## English

Windows Local Link Lab now accepts two GBA cartridges as well as GB/GBC pairs.
GBA uses native 240×160 frame buffers, configured shoulder input and the same
shared pause/cable/audio/save-lifetime workflow. Mixed GB/GBA pairs are rejected.
It runs two devices in one process, not independently launched applications or
localhost sockets. The shared runtime alternates individual GBA CPU cycles and
skips the initial unilateral rewind capture before attaching the cable.

GBA normal 8/32-bit and two-device multiplayer 16-bit serial modes include
RCNT selection, baud timing, device IDs, ready/busy/error status, receive slots
and serial interrupts. Stateful instruction helpers are now per CPU, rather than
global scratch space shared across interleaved ARM/Thumb instructions. Existing
single-player operation retains its normal snapshot/rewind workflow.

Device states write schema 6 and read schema 5; old binaries cannot read new
states. Battery saves are unchanged. Linked devices reject unilateral capture/
restore before mutating state, including when logically unplugged. Same-ROM
P2 saves persist separately; external saves are not imported or cloned.

This remains a GBADotnet-derived core with its existing provenance and licensing.
mGBA's serial/lockstep/local multiplayer code and GBATEK, linked above, informed
the architecture; they do not establish equivalent accuracy. Ordinary GBA frontend
feature integration is not proof of equal compatibility with GB/GBC. No commercial
game trade or sustained multiplayer session has been validated in this milestone.
No wireless, Joybus, networking, four-player mode or cross-family connection.

Linux should reuse the shared session/core and add native paired displays/input,
XDG save planning and safe lifecycle/audio handling. Its fetched frontend update
does not add a separate GBA implementation or native link UI. The integration
review records the colleague's contributions and preserved local changes.

Final combined verification: **710 total, 674 passed, 36 skipped, zero failures**;
Release build: zero warnings/errors. Core 197/197, Runtime 220/220, Windows smoke
178 passed/1 foreground-dependent skip, Desktop logic 79 passed/35 native skips.
The runtime includes 39 new serial/link and 24 CPU-isolation cases; Windows adds
nine GBA planning/input/geometry tests. Synthetic ARM ROMs exchange data and run
actual serial interrupt handlers at all four multiplayer baud rates. This is
not proof of Pokémon compatibility, physical-controller operation or native
Linux integration. Real hardware/gameplay validation remains separate.
**No commit or push until the user approves the explained combined changes.**
