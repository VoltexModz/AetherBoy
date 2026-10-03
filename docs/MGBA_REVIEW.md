# mGBA-Referenzprüfung und eigener Ausbau

Ursprünglicher Stand: 2026-09-08. Damaliges Ergebnis: **mGBA als technische
Referenz, keine direkte Einbindung oder C-nach-C#-Übersetzung.** Nachtrag
01.10.2026: Für Cheat-Entschlüsselung und Reseed-Tabellen werden jetzt ausdrücklich
MPL-2.0-lizenzierte Adaptionen verwendet; siehe den Cheat-Abgleich unten und
`third_party/mgba-cheats`. Es wird weiterhin kein mGBA-Emulatorkern eingebunden.
Unser GB/GBC-Kern bleibt
bestehen. Ein eigener ARM/Bus/Mode-3-Prototyp läuft. Der spätere produktive
GBA-Pfad verwendet den separat geprüften
[MIT-lizenzierten GBADotnet-Snapshot](GBADOTNET_REVIEW.md); kommerzielle GBA-Spiele
sind in AetherBoy weiterhin nicht end-to-end verifiziert. Inzwischen ist dieser
Produktionspfad deutlich über den ursprünglichen Snapshot hinaus erweitert.

## Untersuchte Grundlage und Grenzen

- Vom Projektinhaber bereitgestellt: `1-mgba-master.zip`, 18.432.647 Bytes,
  3.728 Archiveinträge.
- SHA-256: `C18DB94E85713BB55119D848F71DAFB8D7F2D77EC0F6A6E99B4D2AE6EF54B1F7`.
- `CHANGES` beginnt mit `0.11.0: (Future)`. Das ist ein Entwicklungsstand,
  kein Beleg für eine veröffentlichte Version oder einen bestimmten Git-Commit.
- Zunächst 282 gezielte, anschließend insgesamt 895 relevante Quell-, Header-,
  Build-, Lizenz- und Portierungsdateien wurden unter
  `.local-tools/mgba-reference-20260907/mgba-master` abgelegt. Das Verzeichnis
  ist ignoriert und nicht Bestandteil von Build oder Release. Die mitgelieferten
  ROMs, Bilder und ausführbaren Programme wurden nicht extrahiert oder ausgeführt.
- Die gesamte extrahierte Auswahl wurde strukturell inventarisiert und nach
  Modul, Abhängigkeit und Schlüsselsymbolen durchsucht. Semantisch tief geprüft
  wurden Lizenz, Build-Abhängigkeiten und die zentralen ARM-/Thumb-, GBA-Core-,
  Memory/IO-, DMA-, Timer-, Video-, Audio-, Save-, BIOS-, Serial- und
  Cartridge-Pfade. Das ist eine vollständige Kernlandkarte, **kein formales
  zeilenweises Audit jeder Implementierungsvariante**. Zählungen und
  Zielarchitektur: [GBA_CORE_ARCHITECTURE.md](GBA_CORE_ARCHITECTURE.md).
- Kein mGBA-Binary wurde gebaut; es gibt keinen identischen Testlauf beider
  Emulatoren und keinen FPS-/Latenzvergleich. Deshalb keine Prozentangaben zur
  Gleichwertigkeit und keine Behauptung, einer sei in jedem Detail genauer.

## Warum der GBA-Ordner allein nicht genügt

Die Build-Datei `src/gba/CMakeLists.txt` nimmt ausdrücklich `../gb/audio.c` auf.
Auch `include/mgba/internal/gba/audio.h` bindet den GB-Audioteil ein. GBA enthält
neben zwei Direct-Sound-FIFOs verwandte ältere Soundkanäle; die Software nutzt
diese Gemeinsamkeit.

Weitere Abhängigkeiten sind:

| Bestandteil | Im mGBA-Archiv | Bedeutung für unseren eigenen Kern |
| --- | --- | --- |
| Prozessor | `src/arm`, ARM- und Thumb-Decoder/ISA | Neue ARMv4T-CPU; unser GB-Prozessor ist kein Ersatz |
| GBA-Hardware | `src/gba`: Speicher, IO, Timer, DMA, Video, Audio, Saves | Eigenständige Komponenten mit dokumentierten Registerverträgen |
| Gemeinsame Infrastruktur | `src/core`, `src/util`, Header | Zeitsteuerung, Datenströme, Zustand und Schnittstellen selbst passend entwerfen |
| Audio-Gemeinsamkeiten | `src/gb/audio.c` plus GBA-Audio | Hardwarekonzepte wiederverwenden; bestehende GB-Klassen nicht ungeprüft übernehmen |
| Host/Frontend | Plattformcode, optionale Debugger/Skripte | Unsere Oberfläche und Owner-Thread-Grenze erhalten |

Belege: Root-`CMakeLists.txt`, Abschnitte `M_CORE_GB`, `M_CORE_GBA`, `CORE_SRC`;
`src/arm/CMakeLists.txt`; `src/gba/CMakeLists.txt`; `src/gba/core.c`.
Ein GBA-only-Build ist konfigurierbar, aber nicht identisch mit „nur Dateien aus
src/gba kopieren“.

## GB/GBC und Funktionen im Vergleich

„Vorhanden“ beim Referenzprojekt bezeichnet README-Angaben beziehungsweise
sichtbare Implementierungen, nicht eine von uns verifizierte Spielgarantie.

| Bereich | mGBA-Referenz | AetherBoy | Konsequenz |
| --- | --- | --- | --- |
| GB/GBC-Grundemulation | Beide Systeme, eigene Modellwahl | Beide vorhanden, experimentell | Weiterentwickeln, nicht ersetzen |
| Modellwahl | GB-Core trennt Hardwaremodelle und ROM-Eigenschaften | Color-Hardware derzeit eng an ROM-Header gekoppelt (`Memory.cs`) | Modellkonfiguration später ausdrücklich von ROM-Fähigkeiten trennen |
| Grafik innerhalb einer Zeile | `GBVideoProcessDots` zeichnet Bereiche bis zur aktuellen X-Position | `Video.RenderLine` mit angenäherten Mid-Scanline-Effekten; kein vollständiger Pixel-FIFO | Eigene Timing-/Pixelpipeline verbessern; aus mGBA-Code **keine** perfekte FIFO-Treue ableiten |
| Standard-Mapper | MBC1/2/3/5 und mehr | MBC1/2/3/5 vorhanden | Bestehende Regressionstests erhalten |
| MBC1M | Multicart-Verdrahtung vorhanden | In diesem Schritt neu ergänzt | Konkrete kleine Kompatibilitätserweiterung |
| Weitere Module | U. a. MBC30, MBC7; mehrere andere nur teilweise | Viele Spezialtypen abgelehnt | Nach tatsächlichen Spielen priorisieren, nicht pauschal freischalten |
| Super Game Boy | Modell, Rahmen und Paketverarbeitung sichtbar | Bisher nur SGB-Kennung | Eigene Funktion, nicht bloß zusätzlicher Farbfilter |
| Link | GB/GBA-Lockstep-Code für lokale Kopplung | Bitweise Serial-Emulation; noch keine gekoppelte Spielsession | Separates Synchronisationsprojekt; Transport allein genügt nicht |
| Printer/Camera/Sensoren | Printer-Code, Camera sowie Bewegungs-/Lichtsensor-Funktionen dokumentiert; teils begrenzt | Nicht vollständig vorhanden | Optional nach Kernkompatibilität |
| GBA | Voller eigener Systemkern im Referenzprojekt | Produktiver gepflegter C#-Kern mit CPU, PPU, DMA, Timern, IRQ, PSG/Direct Sound, Saves/RTC, Zuständen und HLE-BIOS | Reale Spiele systematisch testen und Timing-/PPU-Randfälle härten |
| Speicherstände/Rewind | Save-Erkennung, Zustände, konfigurierbarer Rewind | Atomare Batterie-Saves, Backups, ROM-gebundene Zustände, begrenzter Rewind | Unsere Sicherungen behalten; für GBA nicht einfach GB-Formate verwenden |
| Archive/Patches | ZIP/7z und IPS/UPS/BPS laut README | Normaler GB/GBC-Dateilader | Sinnvolle spätere Komfortfunktion; Größenlimits und unveränderte Originaldateien voraussetzen |
| Cheats | Getrennte Code-Sets, Game Genie, GBA-Geräte-Decoder | Gemeinsamer GB/GBC/GBA-Pfad für Windows/Linux; GBA inklusive Master-Streams, Bedingungen, Hooks, Zeiger und ROM-Patches | [Befehlsmatrix](CHEAT_SUPPORT.md) und synthetische Regressionen; keine pauschale Spielcode-Kompatibilität behaupten |
| Debugging/Scripting | Speicher-/Registerinformationen, Debugger, Lua laut README | Diagnose und Conformance-CLI, kein vergleichbarer Debugger | Später wertvoll für Fehleranalyse; Plugins/Skripte sind zusätzlicher Sicherheitsumfang |
| Aufnahme | Screenshots und Videoformate dokumentiert | WAV-Aufnahme | Komfortausbau, keine Verbesserung der Emulationsgenauigkeit an sich |
| Oberfläche/Architektur | Eigenes plattformübergreifendes Frontend | Eigene Aether-Wave-UI, Control Center, immutable Snapshots/Owner-Thread | Unsere Produktidentität und Grenzen erhalten |

Weitere konkrete Referenzstellen: `src/gb/mbc.c`,
`src/gb/core.c`, `src/gb/video.c`, `src/gb/renderers/software.c`,
`src/gb/sio/lockstep.c`, `src/gb/sio/printer.c`, `src/gba/savedata.c`
und das Feature-/Mapper-Verzeichnis im README.

Das README führt auch geplante Funktionen auf. Netzwerk-Multiplayer oder
Wireless-Adapter werden hier deshalb nicht als fertig nutzbare Referenzfunktionen
gewertet. Datei-/Symbolpräsenz allein beweist ebenso wenig Produktreife.

**Einordnung:** Der belegte Funktionsumfang und die unterstützte Hardware sind
bei mGBA erheblich breiter. Unsere Architektur, Tests und Save-Sicherheit sind
eine gute eigene Grundlage, aber kein Nachweis für bessere Spielkompatibilität.

## In diesem Schritt selbst implementiert

### MBC1M im produktiven GB-Ladeweg

- `Mbc1` unterstützt alternativ Gruppen von 16 statt 32 ROM-Bänken.
- Die Nullbank-Umsetzung erfolgt weiterhin anhand des vollständigen 5-Bit-
  Registers, erst danach entfällt bei MBC1M das fünfte Bankbit. Insbesondere
  sind Schreibwerte `0x00` und `0x10` nicht gleichbedeutend.
- Konservative automatische Erkennung für 1-MiB-ROMs: übereinstimmende, nicht
  konstante Logo-Felder und gültige Header-Prüfsummen am Anfang und in Bank 0x10.
  Das ist eine Heuristik, keine Authentifizierung. Beschädigte Header und
  Sonderlayouts können unentdeckt bleiben; kein Anspruch auf jede Multicart.
- Normale MBC1-Zustände behalten ihre vier Registerbytes. MBC1M erhält ein
  fünftes Kennungsbyte. Zustände mit anderer Verdrahtung werden abgelehnt;
  alte, mit der falschen Standard-Verdrahtung erzeugte MBC1M-Zustände werden
  nicht still übernommen. Batterie-RAM und dessen Sicherung bleiben separat.
- Sieben neue Tests prüfen Banking, den 0x10-Sonderfall, Erkennung,
  Zustandswiederherstellung, Ablehnung falscher Zustände und den ROM-Ladeweg.
- Kein kommerzielles MBC1M-Spiel wurde durchgespielt; Tests verwenden selbst
  erzeugte Daten, keine aus dem mGBA-Archiv übernommene Test-ROM.

Grundlage: [Pan Docs, MBC1M](https://raw.githubusercontent.com/gbdev/pandocs/master/src/MBC1.md).
Die bestehende C#-Mapperarchitektur wurde erweitert, kein mGBA-Funktionskörper
portiert. Unsere Erkennungsheuristik ist separat beschrieben und getestet.

### Erste eigene ARM7-Datenpfadbausteine

`nanoboy/Core/Advance/Arm7DataPath.cs` enthält reine, zustandslose Operationen:

- Addition mit Carry, Subtraktion mit ARM-„kein Borrow“-Carry und N/Z/C/V-Flags;
- Auswertung der 16 ARM7-Bedingungscodes und Erhaltung der übrigen CPSR-Bits;
- vier Shift-/Rotate-Arten, getrennte Immediate- und Register-Semantik,
  RRX, 0-/32-/größer-32-Randfälle und rotierte 8-Bit-Konstanten.

Neun Tests verwenden unabhängige BigInteger-Ergebnisse, eine schrittweise
Ein-Bit-Referenz, alle 256 Register-Shiftweiten und alle Flagkombinationen.
Die mGBA-ISA-Funktionskörper wurden hierfür nicht als Implementierungsvorlage
verwendet. Grundlage sind dokumentierte ARM-Hardwareoperationen:
[ARM7TDMI TRM](https://documentation-service.arm.com/static/5e8e1323fd977155116a3129)
und die ARM-Shiftbeschreibung, Abschnitt 4.4.2–4.4.3, im
[ARM7DI-Datenblatt](https://documentation-service.arm.com/static/5ed11a2dca06a95ce53f8f99).

### Ausführender ARM/Bus/Mode-3-Prototyp

Auf den Datenpfadbausteinen sitzt jetzt ein eigener ARM-State-Interpreter. Er
holt Code aus dem Cartridge-ROM, wertet Bedingungen aus und beherrscht alle
Data-Processing-Opcodes sowie B/BL/BX und LDR/STR für Wort/Byte in einem ersten
klar begrenzten Umfang. BX wechselt in den eigenen Thumb-1-Pfad mit Rechen-,
Branch-, Load/Store-, Stack- und Mehrfachtransfer-Gruppen. Ein eigener Bus bildet
die GBA-Hauptregionen und Mirrors ab; ein eigener Renderer liest Mode-3-BGR555
aus VRAM.

Das generierte Probe-ROM wechselt per ARM-BX in Thumb und schreibt `0x0403` nach
`DISPCNT` und rote Pixel nach VRAM. Nach einem Frame wurden 140.446
Instruktionen ausgeführt; das 240×160-Ergebnis hat SHA-256
`0E2B2DF5975F32E5C5D618F1CC99F895D09E2E830FC9998DE6A5CA9AF48EA801`.
Elf neue Tests prüfen Reset, Bus/VRAM-Mirrors, Fehlerdiagnose, Branch-Link,
ARM→Thumb, Thumb-Flags/Branches, PC-relative Loads, IO-/RAM-Transfers,
PUSH/POP und den vollständigen ROM-zu-Framebuffer-Pfad.

**Noch nicht enthalten:** weitere ARM-Gruppen, vollständige Thumb-Randfälle,
Registerbanken,
Exceptions/BIOS, zeitgesteuertes MMIO, IRQ, Timer, DMA, tilebasierte Grafik,
Sprites, Audio und GBA-Save-Hardware. Dieser Prototyp ist kein vollständiger
ARM7TDMI und noch kein kommerziell spielbarer GBA-Kern.

## Herkunft und Lizenzdisziplin

Das Archiv enthält MPL 2.0 und dateibezogene Hinweise. Es werden keine
individuell erweiterten Nutzungsrechte aus dem Dateinamen oder der Bereitstellung
abgeleitet. Neue Funktionen werden anhand von Hardwareverträgen und eigenen Tests
entworfen, nicht durch Umbenennen oder automatisches Übersetzen fremder Funktionen.

Wir haben Referenzquellcode gesehen; dies ist daher **keine Behauptung eines
formalen Clean-Room-Verfahrens**. Falls künftig doch schutzfähiger Code übernommen
oder abgeleitet wird, müssen Herkunft, Hinweise und anwendbare Lizenzpflichten
weitergeführt und vor Veröffentlichung geprüft werden. Sprachwechsel oder andere
Variablennamen beseitigen diese Frage nicht. Die
[Mozilla-FAQ](https://www.mozilla.org/en-US/MPL/2.0/FAQ/) erläutert insbesondere
Änderungen, Verteilung und das dateibezogene Copyleft.

Im initialen Referenzschritt wurden keine mGBA-Funktionskörper,
Nintendo-BIOS-Dateien, Test-ROMs oder nativen Emulator-Binaries in den
Produktquellbaum übernommen. Die lokale Referenzkopie behält die ursprünglichen
Hinweise unverändert.

### Späterer HLE-BIOS-Ausbau (2026-09-08)

Für den späteren BIOS-Fallback wurde zusätzlich `src/gba/bios.c` zusammen mit
dokumentierten GBA-BIOS-Verträgen konsultiert, um das von Spielen beobachtbare
Verhalten der SWI-Dienste zu prüfen. `HleBios.cs` wurde eigenständig in C# für
AetherBoys vorhandene Bus-, CPU- und Scheduler-Schnittstellen geschrieben. Es
wurde kein C-Funktionskörper mechanisch portiert, kein BIOS-ROM übernommen und
keine Test-ROM aus dem Archiv eingebunden. Weil Referenzquellcode eingesehen
wurde, bleibt dies ausdrücklich **kein formaler Clean-Room-Anspruch**.

## Nächste sinnvolle Arbeit

Aktueller automatisierter Stand: 290/290 Tests, davon 175 Core-, 87 Runtime- und
28 Windows-Smoke-Tests, ohne übersprungene Tests und mit Compilerwarnungen als
Fehlern. Der historische Bericht des ersten Referenzschritts bleibt unter
`artifacts/compatibility-mgba-review-20260907.json` erhalten. Es gibt weiterhin
keinen mGBA-A/B-Benchmark und noch keinen vollständigen kommerziellen GBA-Spieltest.

1. Mit legal bereitgestellten Spielen reale Boot-, Audio-, Save-, RTC-, Zustands-
   und Langzeittests durchführen und reproduzierbare Fehlerfälle festhalten.
2. Öffentliche CPU-, Timing-, PPU- und BIOS-Test-ROMs als weitere CI-Gates
   aufnehmen, jeweils mit eigener Herkunfts- und Lizenzprüfung.
3. Verbleibende cycle-exakte Bus-/Open-Bus-Details sowie Window-/OBJ-/Mosaic-
   Randfälle gezielt gegen dokumentiertes Verhalten härten.
4. Auf der inzwischen vorhandenen deterministischen Zwei-Core-Gegenstelle einen
   koordinierten zweiten App-Host aufbauen; Netzwerktransport bleibt nachgelagert.
5. Die inzwischen unterstützten CodeBreaker- und GameShark-v1/v2-Pfade durch
   reale, rechtmäßig verwendete Codes prüfen; PAR v3 bleibt separat offen.

Weiterhin keine Telemetrie und kein Upload oder Commit von ROM-, BIOS- oder
Save-Daten. Dieses Dokument beschreibt nur den Referenz- und Entwicklungsweg.

## Cheat-Abgleich vom 01.10.2026

Im bereitgestellten mGBA-Quellarchiv wurden `src/core/cheats.c`,
`src/gb/cheats.c`, `src/gba/cheats.c` sowie die GBA-Decoder für CodeBreaker,
GameShark und PAR v3 erneut als Verhaltensreferenz geprüft. mGBA fasst mehrere
Zeilen in einem schaltbaren Code-Set zusammen. Beim GB ist Game Genie eine
ROM-Leseüberlagerung mit optionalem Vergleich gegen das ursprüngliche Byte;
GameShark arbeitet hingegen mit Speicherschreibzugriffen. Daher reicht das
bloße Erkennen eines Game-Genie-Strings nicht als Unterstützung aus.

AetherBoy verbindet die GB/GBC-ROM-Leseüberlagerung nun mit dem Speicherbus,
akzeptiert sechs- und neunstellige Game-Genie-Codes und behandelt mehrere
GB/GBC-Zeilen atomar als ein Set. Die ROM-Datei wird dabei nicht verändert.
Windows und Linux verwenden denselben Kern und zeigen die tatsächlich
verfügbaren Formate an. GB/GBC akzeptiert zusätzlich CodeBreaker `00AAAA-VV`
und direkte `AAAA:VV`-Speicherschreibzugriffe. Die bankumschaltenden GB-GameShark-
Varianten werden nicht durch Ignorieren ihres Befehlsbytes vorgetäuscht.

Nicht gleichgesetzt werden dürfen ein eingelesener Code, ein wirksamer Code
und ein für ein konkretes Spiel passender Code. Die Spieler wählen ihre Codes
selbst. Der Emulator prüft das Format und die implementierte Operation, aber
nicht die Zugehörigkeit zum Spiel. Die Cheatliste gilt derzeit nur für die
laufende Sitzung.

Für den GBA-Ausbau ist mGBAs Trennung wichtig: PAR v3 besitzt einen eigenen
Decoder und Befehlsumfang; verschlüsselte CodeBreaker-Streams brauchen einen
zustandsbehafteten Mastercode-/Tabellenpfad. Beides lässt sich nicht korrekt
durch Umbenennen vorhandener GameShark-Codes erreichen. AetherBoy dekodiert
PAR v3 deshalb separat und nimmt mit `AR3:` verschlüsselte beziehungsweise
mit `AR3RAW:` bereits entschlüsselte Programme an. Bedingungen (ein/zwei Befehle
oder Block mit ELSE/ENDIF), signed/unsigned Vergleiche, Fills/Slides, Additionen,
Zeiger, ROM-Patches und virtuelle Gerätetaste sind implementiert. CodeBreaker
hat jetzt Master-Verschlüsselung, Slide/List und Hooks; GameShark v1/v2 ergänzt
Reseed, Gruppen, Bedingungen, Hooks, Gerätetaste und ROM-Patches. Thumb-Hooks
werden im CPU-Owner-Thread genau einmal pro passender Instruktion ausgeführt.
Speicherzugriffe verändern nicht das CPU-Waitstate-Budget. ROM-Patches werden
nur auf die geladene Kopie angewandt und beim Abschalten überlappungsfest entfernt.

Die vollständige Matrix samt Grenzen steht in [CHEAT_SUPPORT.md](CHEAT_SUPPORT.md).
Auch mGBA implementiert im geprüften PAR-v3-Pfad keine Slowdown-/Disable-all-
Operationen; AetherBoy weist diese ab. Die Wahl passender Spielcodes wird nicht
automatisiert. Master und zugehörige Zeilen sollten gemeinsam eingegeben werden.

Die C#-Cipher-Adaption und vier Reseed-Tabellen in `GbaCheatCipher.cs` und
`GbaCheatTables.cs` basieren jetzt auf mGBA-Code unter MPL-2.0, mit Copyright,
vollständiger Lizenz und Quellenhinweis im Produkt. Die frühere Aussage, es
seien keinerlei Funktionskörper übernommen/adaptiert, gilt nur für die historischen
Review-/BIOS-Schritte, nicht für diesen Cheat-Ausbau. Testvektoren für CB, GS und
AR3 wurden zusätzlich mit separat kompilierten originalen mGBA-C-Routinen erzeugt
und in den Tests festgehalten; die Produktion nutzt keinen C-Helper. Das ist kein
End-to-end-Kompatibilitätstest des gesamten mGBA-Emulators oder echter Spielcodes.

Zusätzlicher Formatabgleich: [EnHacklopedia GBA](https://doc.kodewerx.org/hacking_gba.html).
Die dokumentierten AR-v3-I/O-Breiten C6/C7 werden mit 16/32 Bit umgesetzt und
getestet; einzelne Implementierungsdetails des Referenzcodes werden nicht blind
übernommen.
