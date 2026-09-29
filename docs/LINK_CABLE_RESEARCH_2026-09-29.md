# Link-Kabel-Recherche: GB, GBC und GBA

Stand: 29. September 2026. Schwerpunkt: Pokémon zwischen Windows und Linux über das Internet; zusätzlich allgemeine lokale Link-Emulation.

## Ergebnis vorweg

Das Kabel und wesentliche Spieleprotokolle sind bereits ausführlich reverse-engineert. Es existieren mehrere unterschiedliche, tatsächlich implementierte Lösungswege. Besonders relevant für AetherBoy sind:

1. **pret/pokered, pokecrystal, pokefirered und pokeemerald:** Was die Spiele tatsächlich senden und erwarten.
2. **mGBA, SameBoy und BizHawk:** Wie lokale Geräte in emulierter Zeit verbunden werden.
3. **gpSP:** Ein ausdrücklich auf Pokémon Gen 3 zugeschnittener Netzwerk-Kabelmodus, zusätzlich zur separaten Wireless-Adapter-Emulation.
4. **DoubleCherryGB:** GB/GBC-Netpacket-Verkehr mit bekannten Einschränkungen; kein GBA-Kern.
5. **Coffee GB und mgba-rollback:** Beide Geräte auf beiden Rechnern ausführen, Eingaben austauschen und gegebenenfalls gemeinsam zurücksetzen.
6. **GB-Link/gblink-netplay-bridge:** Eine weitere Implementierung des gpSP-Pokémon-Paketprotokolls, die auch echte GBA-Hardware anbindet.

Es gibt keinen Beleg für die frühere Behauptung, ein einfacher Socket-Austausch mit `Thread.Sleep` garantiere fehlerfreie Tausche. Ebenso falsch wäre, GBA-Link über das Internet grundsätzlich für unmöglich zu erklären.

## Umfang und Aussagekraft

- Untersucht wurden öffentliche Repository-Verzeichnisse, Dokumentationen und ausgewählte Implementierungsdateien. 90 Textdateien wurden als Recherchekopien unter `artifacts/link-research-20260929/sources/` abgelegt. Nicht jede Datei jedes Repositorys wurde gelesen.
- Die Links zeigen überwiegend auf den jeweiligen `HEAD`; das ist eine datierte Bestandsaufnahme, kein auf Commit-SHAs festgeschriebener Benchmark.
- **Code geprüft** bedeutet: Der beschriebene Mechanismus ist in den genannten Dateien vorhanden. Es bedeutet nicht, dass ich die Software gebaut oder einen Tausch damit durchgeführt habe.
- **Dokumentiert** bedeutet: Die Projektverantwortlichen beschreiben die Funktion. Deren Spieltests sind keine eigenen Testergebnisse von AetherBoy.
- Es wurden keine fremden Programme ausgeführt, keine ROMs übertragen und keine öffentlichen Server verändert. Bestehende AetherBoy-Änderungen bleiben unangetastet.
- Die Liste ist breit, aber keine beweisbar vollständige Aufzählung aller jemals veröffentlichten Emulator-Forks. Codeberg wurde ebenfalls durchsucht; dort konnte ich in dieser Recherche keinen zusätzlichen eigenständigen WAN-Link-Emulator ausreichend belegen.

## 1. Was wurde überhaupt „decompiled“?

Ein Game Boy ist Hardware. Seine CPU, Register, Leitungen und Zeitabläufe werden reverse-engineert und dokumentiert. Boot-ROMs und Spieleprogramme kann man dagegen disassemblieren oder dekompilieren.

Für uns sind drei Ebenen auseinanderzuhalten:

| Ebene | Was sie festlegt | Geeignete Referenz |
| --- | --- | --- |
| Hardware-Schnittstelle | Register, Bits, Taktraten, Interrupts, Übertragungsmodi | Pan Docs, GBA-Registerbibliotheken, Emulator-SIO-Kerne |
| Spieleprotokoll | Rollenwahl, Handshake, Datenblöcke, Prüfsummen, Wiederanlauf | pret-Pokémon-Quellen, aufgezeichnete Hardware-Transfers |
| Netzwerk-Emulation | Wie zwei entfernte PCs diese Abläufe trotz Verzögerungen ausführen | gpSP, Coffee GB, DoubleCherryGB, VBA-M, mgba-rollback |

Die Erkenntnis eines Spiels ist nicht automatisch eine generische Kabel-Emulation für alle anderen Spiele.

### Konkrete Pokémon-Quellen

| Spiel | Art | Besonders wichtige Dateien |
| --- | --- | --- |
| Rot/Blau | Assembly-Disassembly | [pokered/home/serial.asm](https://github.com/pret/pokered/blob/HEAD/home/serial.asm) |
| Kristall | Assembly-Disassembly | [pokecrystal/home/serial.asm](https://github.com/pret/pokecrystal/blob/HEAD/home/serial.asm), [engine/link](https://github.com/pret/pokecrystal/tree/HEAD/engine/link) |
| Feuerrot/Blattgrün | C-Decompilation | [pokefirered/src/link.c](https://github.com/pret/pokefirered/blob/HEAD/src/link.c) |
| Smaragd | C-Decompilation | [pokeemerald/src/link.c](https://github.com/pret/pokeemerald/blob/HEAD/src/link.c) |

In `pokered` sieht man beispielsweise den Serial-Interrupt, das Bereitstellen des nächsten Sendebytes, das erneute Aktivieren des externen Takts und die Wartelogik in `Serial_ExchangeByte`.

In `pokefirered` sind unter anderem `SerialCB`, `StartTransfer`, `DoHandshake`, `DoRecv`, `DoSend` und `SendRecvDone` vorhanden. Das Spiel erkennt die Teilnehmer, bildet Prüfsummen und verwaltet Befehlswarteschlangen. RFU/Wireless-Code liegt separat. Das ist für Fehleranalysen viel präziser als die Aussage „Pokémon sendet halt seine Pokémon-Daten“.

Diese Quellen sind Verhaltensreferenzen, keine pauschale Erlaubnis, Nintendo-Spiele oder deren Daten mit AetherBoy auszuliefern.

### GB/GBC: Was wirklich über die Leitung läuft

`SB` bei `0xFF01` ist ein Schieberegister; `SC` bei `0xFF02` steuert Start und Takt. Beide Geräte schieben gleichzeitig ihre ausgehenden Bits heraus und die eingehenden Bits hinein. Nach acht Takten ist das Byte ausgetauscht. Der Serial-Interrupt liegt bei **0x58**, nicht 0x60.

Wichtige Korrektur zur früheren Diskussion: Beim klassischen internen Takt mit 8192 Bit/s dauert ein Byte ungefähr **0,977 ms**, nicht 128 µs. Der schnelle CGB-Modus arbeitet mit 262144 Bit/s, bei doppeltem CPU-Takt mit 524288 Bit/s. Externer Takt kann auf Hardware warten; daraus folgt aber keine beliebige Netzwerk-Toleranz des gesamten Spiels. Dessen Timer und Protokollzustände laufen ebenfalls.

Quelle: [Pan Docs – Serial Data Transfer](https://github.com/gbdev/pandocs/blob/master/src/Serial_Data_Transfer_(Link_Cable).md).

### GBA: Nicht einfach das gleiche Kabel mit größeren Bytes

GBA-SIO hat unterschiedliche Betriebsarten. Besonders wichtig ist der **Multiplayer-Modus**: bis zu vier Teilnehmer, 16-Bit-Werte und Teilnehmer-IDs. Normaler 8-/32-Bit-Modus, JoyBus und Wireless-Adapter sind davon zu unterscheiden.

Die Hardwarebibliothek [LinkRawCable.hpp](https://github.com/afska/gba-link-connection/blob/HEAD/lib/LinkRawCable.hpp) zeigt das direkt: `activate`, `transfer`, `transferAsync`, Ready-/Error-/Start-Bits und ein Antwortarray mit vier 16-Bit-Werten. Die höheren Spielregeln kommen erst darüber.

## 2. Bewertung der fünf vorgegebenen Projekte

### Palmr/gb-link-cable

[Repository](https://github.com/Palmr/gb-link-cable)

- KiCad-Schaltung, Platine und Fertigungsdaten für eine Link-Port-Breakout-Platine.
- Hilfreich, um reale Signale an Mikrocontroller oder Messgeräte zu führen.
- Keine Emulator-Synchronisation und kein fertiges Internetprotokoll.
- Im untersuchten Wurzelverzeichnis war keine explizite Lizenzdatei erkennbar; aus öffentlicher Sichtbarkeit keine Übernahmefreigabe ableiten.

**Nutzen:** spätere Hardware-Messungen und Referenzaufzeichnungen, nicht der direkte Software-Unterbau.

### CableClub/cable-link

[Repository](https://github.com/CableClub/cable-link), [src/main.c](https://github.com/CableClub/cable-link/blob/HEAD/src/main.c), [Protokollkonstanten](https://github.com/CableClub/cable-link/blob/HEAD/src/pokemon_gen1_link_protocol.h)

- RP2040/Pico-Firmware mit SPI und Gen-1-Zuständen für Verbindungsaufbau und Tauschraum.
- Die untersuchte Hauptdatei ist nur ein früher Prototyp: Im Datenteil wird empfangenes Material zurückgespiegelt, `SEND_PATCH` ist noch nicht ausgearbeitet. Außerdem enthält die Hauptschleife eine feste 250-ms-Pause.
- Der Projektname ist kein Beleg für eine vollständige GBA-Link-Implementierung.
- Software-Lizenzdatei: Apache-2.0; Hardware-Unterverzeichnis: CC BY 4.0. Diese Bereiche nicht verwechseln.

**Nutzen:** anschauliche Gen-1-Konstanten. Keine fertige Basis für unseren sicheren Online-Tausch.

### agtbaskara/game-boy-pico-link-board

[Repository](https://github.com/agtbaskara/game-boy-pico-link-board)

- Hardwareadapter für Raspberry Pi Pico; berücksichtigt unterschiedliche Signalspannungen bei GB/GBC und GBA.
- Verweist auf separate Firmwareprojekte und auf einen Nachfolger der Platine.
- Die eigentliche Protokollintelligenz liegt nicht im PCB-Layout.

**Nutzen:** geeignetes Hardwarekonzept für eine spätere USB-Link-Anbindung. Wir brauchen für zwei Emulatoren auf PCs keine solche Platine.

### queueRAM/ti_graph_link

[Repository](https://github.com/queueRAM/ti_graph_link)

- Disassemblierte Firmware und Schaltungen für **Texas-Instruments-Taschenrechnerkabel**.
- PIC-/8051-, serielle und USB-bezogene Reverse-Engineering-Arbeit; kein Nintendo-Link-Protokoll.

**Nutzen:** methodisch interessant, fachlich kein passender Kabeltreiber für GB/GBC/GBA.

### FIX94/gba-link-cable-dumper

[Repository](https://github.com/FIX94/gba-link-cable-dumper), [GBA-Seite](https://github.com/FIX94/gba-link-cable-dumper/blob/HEAD/gba/source/main.c), [GameCube/Wii-Seite](https://github.com/FIX94/gba-link-cable-dumper/blob/HEAD/source/main.c)

- GameCube/Wii-Homebrew und ein auf den GBA geladenes Programm zum Auslesen von ROM/BIOS/Spielstand sowie zum Zurückschreiben von Spielständen.
- Der Code benutzt `SI_Transfer` beziehungsweise `REG_JOYTR`, `REG_JOYRE` und `REG_HS_CTRL`: **GameCube–GBA/JoyBus**, nicht der normale Pokémon-GBA-Multiplayerraum.
- MIT-Lizenz im Repository.

**Nutzen:** JoyBus, Multiboot und mögliche spätere Import-/Backup-Werkzeuge. Keine unmittelbar passende WAN-Tauschlösung.

## 3. Emulatoren und Kerne mit relevanter Link-Implementierung

Die Varianten werden bewusst benannt: Eine Funktion eines libretro-Forks ist nicht automatisch in jeder Standalone-Ausgabe desselben Kerns vorhanden.

| Projekt / Variante | Systeme im betrachteten Link-Pfad | Mechanismus und Bedienkonzept | Nachweis und Grenze |
| --- | --- | --- | --- |
| **mGBA** | GB/GBC, GBA | Mehrere lokale Kerne; SIO-Koordinator synchronisiert emulierte Ereignisse und Zyklen. Desktop-Multiplayer über zusätzliche Instanzen. | GB- und GBA-Lockstep-Code geprüft. Lokaler Link ist nicht automatisch WAN; README führt Netzwerk-Link weiterhin als geplant. |
| **VBA-M** | GB/GBC, GBA | Lokale IPC-Verbindungen sowie TCP-Socket-Modi; getrennte Kabel-, RFU- und Dolphin-Wege. | `gbaLink.h/.cpp` geprüft. Vorhandener Netzwerkmodus belegt keine universelle WAN-Kompatibilität. |
| **VBALink** | Vor allem GBA | Historischer VBA-Fork; mehrere Fenster oder LAN-Host/Clients, bis vier Geräte im Multiplayer-Modus. | Projektseite bietet Quellen und beschreibt Grenzen. Kein vollständiger aktueller Quellcode-Audit dieser historischen Ausgabe. |
| **SameBoy** | GB/GBC | Serial-Bit-Callbacks verbinden zwei Kerne; im libretro-Frontend zwei ROM-Slots/Subsystem. | Core- und libretro-Pfad geprüft. Netzwerkzugang hängt vom Frontend und dessen gemeinsamer Emulation ab. |
| **TGB Dual / libretro, historische L-Varianten** | GB/GBC | Zwei emulierte Game Boys miteinander verbunden; libretro-Dualbetrieb. Historische Windows-Netplay-Dokumentation verlangt beide ROMs. | Dual-Core-Quelle und historische Netplay-Anleitung geprüft; nicht jede L/Kai-Variante separat auditiert. |
| **DoubleCherryGB** | GB/GBC | TGB-Abkömmling mit Mehrgerätebetrieb; zusätzlich Netpacket-Austausch von Linkdaten zwischen unterschiedlichen Spielen. | Sende-/Empfangshandler und CPU-Wartepfad geprüft. Dokumentation nennt Einschränkungen besonders bei Kämpfen. |
| **Gambatte-libretro mit HAVE_NETWORK** | GB/GBC | Eigene Game-Link-Einstellungen für TCP-Server/Client und IP/Port; kein bloßes Aktivieren von gewöhnlichem RetroArch-Netplay. | `NetSerial` ist tatsächlich an `setSerialIO` angebunden. Build-Option und Variante wichtig. |
| **BizHawk: GBHawkLink / GambatteLink** | GB/GBC | Zwei Kerne, eigener Link-Wrapper, gemeinsames Frame-Stepping und Zustände; zusätzliche Mehrgerätevarianten vorhanden. | C#-Quellen geprüft. Daraus folgt kein eingebauter allgemeiner WAN-Dienst. |
| **Gearboy** | GB/GBC | Desktop: zwei Prozesse treten derselben lokalen Session bei; Shared Memory. libretro: eigener lokaler Dual-Link-Pfad. | Manager, Wire-Struktur und libretro-Code geprüft. Kein Internettransport im betrachteten Desktop-Pfad. |
| **Coffee GB** | GB/GBC | Beide Maschinen auf jedem Peer; synchronisierte Eingaben, Snapshots und Rollback. | Kotlin-/Java-Code und Protokoll v9 geprüft. Direkter TCP-Transport ist laut Dokumentation unverschlüsselt. |
| **GBmulator** | GB/GBC | TCP-Verbindung, Austausch von Initialdaten und Start einer zweiten lokalen Maschine; anschließend Steuerdaten. | Core-Link und Plattform-Link geprüft. Nicht mit „nur serielle Bytes über TCP“ verwechseln. Browserausgabe ohne diesen Link. |
| **GBE+ / GB Enhanced+** | GB/GBC im geprüften Netzwerkpfad | Netzwerkschnittstelle mit Synchronisationsmeldungen, Hard-/Soft-Sync; auch Infrarot und Spezialgeräte. | `src/dmg/sio.cpp` und Konfiguration geprüft. GBA-Emulation im selben Programm beweist keinen allgemeinen GBA-WAN-Link. |
| **gpSP / libretro** | GBA | Netpacket-basiert: eigener Pokémon-Gen3-Kabelmodus, Advance-Wars-Modi und separate RFU-Emulation. | `serial.c`, `serial.h`, `serial_proto.c`, `rfu.c` vorhanden und relevante Pokémon-Pfade geprüft. Gezielte Protokollemulation, kein Universal-Kabel für jedes Spiel. |
| **Dolphin mit integriertem mGBA** | GameCube ↔ GBA | GBA-Kerne werden in Dolphins emulierte Zeit eingebunden; gemeinsame Zustände und Netplay. | `SI_DeviceGBAEmu.cpp` geprüft. JoyBus ist nicht FireRed↔Emerald-Multiplayer. |
| **Tango** | GBA, unterstützte Mega-Man-Battle-Network-Spiele | Spielbezogene Anpassungen und Rollback auf Emulatorbasis. | README und mGBA-Link-Anbindung geprüft. Kein fertiger Pokémon-Modus. |

### Direkte Quellcode-Einstiege zu dieser Tabelle

- mGBA: [GB-Lockstep](https://github.com/mgba-emu/mgba/blob/HEAD/src/gb/sio/lockstep.c), [GBA-Lockstep](https://github.com/mgba-emu/mgba/blob/HEAD/src/gba/sio/lockstep.c), [Qt-MultiplayerController](https://github.com/mgba-emu/mgba/blob/HEAD/src/platform/qt/MultiplayerController.cpp), [README](https://github.com/mgba-emu/mgba/blob/HEAD/README.md).
- VBA-M: [Link-Modi](https://github.com/visualboyadvance-m/visualboyadvance-m/blob/HEAD/src/core/gba/gbaLink.h), [Implementierung](https://github.com/visualboyadvance-m/visualboyadvance-m/blob/HEAD/src/core/gba/gbaLink.cpp).
- VBALink: [Projektseite mit Source-Download und Einschränkungen](https://vbalink.info/).
- SameBoy: [Core/timing.c](https://github.com/LIJI32/SameBoy/blob/HEAD/Core/timing.c), [libretro-Anbindung](https://github.com/LIJI32/SameBoy/blob/HEAD/libretro/libretro.c), [Subsystem-Bedienung](https://github.com/libretro/docs/blob/HEAD/docs/library/sameboy.md).
- TGB Dual: [CPU/IO-Code](https://github.com/libretro/tgbdual-libretro/blob/HEAD/gb_core/cpu.cpp), [libretro](https://github.com/libretro/tgbdual-libretro/blob/HEAD/libretro/libretro.cpp), [historische Netplay-Anleitung](https://github.com/libretro/tgbdual-libretro/blob/HEAD/docs/Netplay.txt).
- DoubleCherryGB: [Empfangshandler](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/HEAD/libretro/DoubleCherryEngine/Netplay/NetPacketReceiveHandler.cpp), [Sendehandler](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/HEAD/libretro/DoubleCherryEngine/Netplay/NetPacketSendHandler.cpp), [README](https://github.com/TimOelrichs/doublecherryGB-libretro).
- Gambatte: [net_serial.cpp](https://github.com/libretro/gambatte-libretro/blob/HEAD/libgambatte/libretro/net_serial.cpp), [aktive Frontend-Anbindung](https://github.com/libretro/gambatte-libretro/blob/HEAD/libgambatte/libretro/libretro.cpp).
- BizHawk: [GBHawkLink.IEmulator.cs](https://github.com/TASEmulators/BizHawk/blob/HEAD/src/BizHawk.Emulation.Cores/Consoles/Nintendo/GBHawkLink/GBHawkLink.IEmulator.cs), [GambatteLink](https://github.com/TASEmulators/BizHawk/blob/HEAD/src/BizHawk.Emulation.Cores/Consoles/Nintendo/Gameboy/GambatteLink.cs).
- Gearboy: [Desktop-Manager](https://github.com/drhelius/Gearboy/blob/HEAD/platforms/shared/desktop/link_cable/link_cable_manager.cpp), [libretro-Link](https://github.com/drhelius/Gearboy/blob/HEAD/platforms/libretro/libretro_link.cpp).
- Coffee GB: [LinkedController.kt](https://github.com/trekawek/coffee-gb/blob/HEAD/controller/src/main/java/eu/rekawek/coffeegb/controller/link/LinkedController.kt), [LinkedFrameStepper.kt](https://github.com/trekawek/coffee-gb/blob/HEAD/controller/src/main/java/eu/rekawek/coffeegb/controller/link/LinkedFrameStepper.kt), [Protokoll v9](https://github.com/trekawek/coffee-gb/blob/HEAD/docs/netplay-protocol-v9.md).
- GBmulator: [lokale Serial-Emulation](https://github.com/mpostaire/gbmulator/blob/HEAD/src/core/gb/link.c), [Verbindung und zweite Maschine](https://github.com/mpostaire/gbmulator/blob/HEAD/src/platform/common/link.c).
- GBE+: [DMG-SIO](https://github.com/shonumi/gbe-plus/blob/HEAD/src/dmg/sio.cpp), [Netplay-Einstellungen](https://github.com/shonumi/gbe-plus/blob/HEAD/src/gbe.ini).
- gpSP: [serial_proto.c](https://github.com/libretro/gpsp/blob/HEAD/serial_proto.c), [Modi](https://github.com/libretro/gpsp/blob/HEAD/serial.h), [RFU](https://github.com/libretro/gpsp/blob/HEAD/rfu.c).
- Dolphin: [SI_DeviceGBAEmu.cpp](https://github.com/dolphin-emu/dolphin/blob/HEAD/Source/Core/Core/HW/SI/SI_DeviceGBAEmu.cpp), [Entwicklerbericht zur Integration](https://dolphin-emu.org/blog/2021/07/21/integrated-gba/).
- Tango: [Projekt](https://github.com/tangobattle/tango), [mGBA-Link-Backend](https://github.com/tangobattle/tango/blob/HEAD/tango-backend-mgba/src/link.rs).

## 4. Weitere Implementierungen und Forschungsprojekte

| Projekt | Fund | Bewertung für AetherBoy |
| --- | --- | --- |
| [tangobattle/mgba-rollback](https://github.com/tangobattle/mgba-rollback) | Rust-Bibliothek für mehrere gekoppelte mGBA-Kerne, gemeinsames Snapshot/Restore und Rollback; eigene SIO-Test-ROM. | Sehr interessante langfristige GBA-Referenz, ausdrücklich experimentell und keine fertige Pokémon-App. |
| [Aelvryx/mgba-wifi-link](https://github.com/Aelvryx/mgba-wifi-link) | Zwei GBA-Kerne auf jedem Peer, Eingabesynchronisation über RetroArch-Netpacket; Delay statt Rollback. | Alpha für zwei Teilnehmer im LAN und identische ROM-Daten. Kein Beleg für komfortablen WAN-Tausch verschiedener Editionen. |
| [Spuds0588/mgba-splitscreen](https://github.com/Spuds0588/mgba-splitscreen) | mGBA-Lockstep in Desktop-/WASM-Oberfläche, lokale 1–4 Spieler und gemeinsame Zustände. | Bedien- und Integrationsreferenz, kein eigenständiger Internet-Kabeldurchbruch. Einordnung hier überwiegend nach Projektdokumentation. |
| [ImLunaHey/emulators](https://github.com/ImLunaHey/emulators) | `duoLink.ts` betreibt zwei lokale GBA-Kerne in Zyklenscheiben und verbindet deren Serial-Daten; Netplay-Oberfläche vorhanden. | Experimenteller weiterer Dual-Core-Ansatz; keine belastbare Pokémon-Kompatibilitätsfreigabe aus dem Code allein. |
| [amyanger/gba-emulator](https://github.com/amyanger/gba-emulator) | Kleiner lokaler SIO-Link über Unix-Domain-Sockets; 16-Bit-Austausch mit Timeout. | Im gelesenen Code ist der Windows-Pfad ein Unsupported-Stub. Kein plattformübergreifendes WAN-Vorbild. |

Zusätzliche Quellen: [mgba-rollback/src/lib.rs](https://github.com/tangobattle/mgba-rollback/blob/HEAD/src/lib.rs), [Netplay-Tests](https://github.com/tangobattle/mgba-rollback/blob/HEAD/tests/netplay.rs), [mgba-wifi-link Architektur](https://github.com/Aelvryx/mgba-wifi-link/blob/HEAD/docs/gba-wifi-link.md), [Wi-Fi-Link-Code](https://github.com/Aelvryx/mgba-wifi-link/blob/HEAD/src/platform/libretro/gba-wifi-link.c), [duoLink.ts](https://github.com/ImLunaHey/emulators/blob/HEAD/apps/web/src/io/duoLink.ts), [Unix-SIO-Link](https://github.com/amyanger/gba-emulator/blob/HEAD/src/sio/sio_link.c).

### Nicht als fertige Open-Source-WAN-Lösung mitzählen

- **BGB:** kein offenliegender Emulator-Quellcode, aber eine wertvolle [öffentliche TCP-Protokollspezifikation](https://bgb.bircd.org/bgblink.html). Acht-Byte-Nachrichten, Versionsaushandlung, Zeitstempel, `sync1/2/3` und Statusmeldungen. Das [Handbuch](https://bgb.bircd.org/manual.html) warnt ausdrücklich vor Latenzgrenzen. TCP allein macht daraus keinen komfortablen Internetmodus.
- **Boytacean:** [Serial-Code](https://github.com/joamag/boytacean/blob/HEAD/src/serial.rs) ist vorhanden; das [README](https://github.com/joamag/boytacean) führt NetPlay aber unter fehlenden Funktionen. Suchtreffer können diese Überschrift unterschlagen.
- **FameBoy-Color:** gefunden, aber in dieser Recherche keine ausreichend belegte, vollständige Link-Implementierung festgestellt. Deshalb keine positive Kompatibilitätsbehauptung.
- **Game-Boy-Printer-Emulation, Debug-Ausgabe an SB, gewöhnliches Netplay mit einem einzigen GB-Kern oder Fernsteuerung eines Emulators:** jeweils kein Nachweis für zwei gekoppelte Spiele.
- **RetroArch:** Frontend und Transport-/Netplay-Infrastruktur, nicht selbst der Game-Boy-Kern. Ein mGBA-, gpSP-, SameBoy- oder DoubleCherry-Modus muss einzeln betrachtet werden.

## 5. Die wichtigsten Mechanismen im Quellcode

### Lokales Kabel: SameBoy, BizHawk und mGBA

SameBoy verbindet Serial-Bit-Callbacks mit dem zweiten Kern. BizHawks `GBHawkLink.do_frame` führt beide Maschinen schrittweise aus und berücksichtigt den internen beziehungsweise externen Serial-Takt. Das ist für unser C#-Projekt eine besonders gut lesbare Referenz.

mGBAs GB-Treiber merkt sich ausgehende Daten und organisiert Phasen von Transferstart bis Abschluss. Der GBA-Koordinator verwaltet Teilnehmer, Modi, Ereignisse, Zyklusversätze und Wartezustände; mehrere Finish-Pfade behandeln Multiplayer und normale Übertragungen getrennt. Im untersuchten Stand gibt es außerdem Save-/Load-Code für den Link-Treiberzustand.

**Lehre:** Ein gemeinsamer Bildschirm ist nicht der schwierige Teil. Entscheidend sind die relative emulierte Zeit und der Zustand des Kabels zwischen den Geräten. Ein vollständiger Link-Snapshot muss mehr enthalten als zwei getrennte CPU-Savestates.

### DoubleCherryGB: Geräte getrennt, Linkdaten übers Netz

Der Standard-Empfangshandler betrachtet die aktuelle Serial-Rolle. Auf der extern getakteten Seite ruft er `receive_from_linkcable` auf und schickt die Antwort zurück; auf der intern getakteten Seite legt er das empfangene Byte in einer Warteschlange ab. Dazu kommt ein CPU-Wartepfad für ausstehende Linkdaten.

Das vermeidet das Replizieren beider vollständiger Spiele. Der kleine Handler ist jedoch keine vollständige portable Netzwerkspezifikation mit Sitzungsepoche und Transfer-ID. Projektseitig werden Tausche günstiger als Kämpfe beurteilt; daraus keine allgemeine Spielkompatibilität ableiten.

**Lehre:** Rollenwechsel, verspätete Antworten und Abbrüche müssen explizit modelliert werden. Eine Transportverbindung mit zuverlässiger Reihenfolge ersetzt das nicht.

### gpSP: Pokémon-Gen3-Befehle statt jedes Kabelwort separat

`serial_proto.c` enthält einen Pokémon-spezifischen Adapter mit Handshake- und Connected-Phasen. Eine emulierte Befehlsrunde umfasst Prüfsumme und acht 16-Bit-Datenwörter. Der Netzwerkweg bündelt die acht Wörter mit Protokollkennung und Zustandsinformationen in einem 24-Byte-`MPK1`-Paket.

Lokale Serial-Ereignisse und Netzwerknachrichten sind dadurch nicht mehr im Verhältnis „ein Kabelwort = ein kompletter Internet-Roundtrip“ gekoppelt. Das ist gezielte Protokollemulation, nicht die Behauptung, beliebige Antwortdaten würden jedes GBA-Spiel austricksen.

**Lehre:** Für das konkrete Gen-3-Ziel ist dies eine sehr wertvolle Vergleichsimplementierung. Neue Spiele und ROM-Hacks brauchen weiterhin passende Profile oder Nachweise. RFU ist ein anderer Pfad.

### Coffee GB: beide Game Boys auf beiden Rechnern

Die verbundenen Maschinen laufen lokal gemeinsam. Netzwerkpakete liefern Eingaben; verspätete Eingaben können Snapshot-Wiederherstellung und erneute Berechnung auslösen. `LinkedController` verwaltet Historie und Rollback; `LinkedFrameStepper` übernimmt das gemeinsame Voranschreiten.

Der [Entwicklerbericht](https://blog.rekawek.eu/2025/07/26/rollback-netplay-gb/) beschreibt ausdrücklich, dass der erste einfache TCP-Kabelversuch auf einem Rechner funktionierte und unter Netzwerklatenz scheiterte. Daraus entstand der replizierte Ansatz. Einzelne Vereinfachungen und Rollenwahl-Heuristiken im aktuellen Stepper sind trotzdem kein Hardware-Referenzstandard.

**Lehre:** Rollback ist ein real implementierter Weg für Link-Spiele. Sein Preis sind vollständige gemeinsame Zustände, gleiche Startbedingungen und zusätzliche Rechenarbeit. Den dort dokumentierten unverschlüsselten TCP-Transport sollten wir nicht übernehmen.

### mgba-rollback: vollständiger GBA-Link-Zustand

Die Bibliothek betreibt mehrere Kerne kooperativ. Ein Snapshot enthält die Kernzustände und Link-Treiber-/Koordinatorzustände. Zustands-Digests und wiederholte Simulation erlauben Vergleichstests.

Der gelesene Netplay-Test erzeugt eigene SIO-Testprogramme und simuliert fünf Frames Einweglatenz bei zwei Frames Präsentationsverzögerung. Dadurch werden Fehlvorhersagen und Rollbacks erzwungen; bestätigte Zustände werden verglichen. Ich habe diese Tests nicht selbst ausgeführt.

**Lehre:** Ein geeigneter Prototyp für spätere generische GBA-Netzwerksynchronisation. Seine Teststruktur ist unmittelbar nützlicher als ein Video mit zwei verbundenen Fenstern. Noch kein Beleg für erfolgreiche Pokémon-Tausche auf unseren beiden Betriebssystemen.

### VBA-M und Gearboy: IPC ist nicht Internet

VBA-M trennt lokale IPC-, TCP-Kabel-, RFU- und andere Modi. Für robuste Transportabwicklung sind die exakten Lesewege und Fehlerbehandlung interessant; die große gemeinsame Implementierungsdatei ist kein Vorbild für unsere Modulstruktur.

Gearboys Desktop-Link arbeitet mit gemeinsamem Speicher, atomaren Zuständen, Fortschrittsinformationen und Partnerüberwachung. Das ist für getrennte Prozesse auf einem Computer geeignet, aber kein NAT-Durchdringungsmechanismus.

## 6. Hardware- und Protokollwerkzeuge, die zusätzlich helfen

### GB-Link-Firmware und Netplay-Bridge

[GBLink-Firmware](https://github.com/GB-Link/GBLink-Firmware) trennt unter anderem physische Link-, Serial- und USB-Schichten. [gblink-netplay-bridge](https://github.com/GB-Link/gblink-netplay-bridge) verbindet die Hardwareseite mit RetroArch/gpSP.

In [linkBridge.ts](https://github.com/GB-Link/gblink-netplay-bridge/blob/HEAD/src/engine/bridge/linkBridge.ts) werden gpSPs `MPK1`-Regeln, Handshake-Freigabe und neue Verbindungsabschnitte explizit behandelt. Alte Daten dürfen nicht in eine neue Handshake-Phase hineinwirken. Die README beschreibt Gen-3-Kabelbetrieb gegen Emulatorpartner; das ist eine Projektangabe, kein von mir durchgeführter Hardwaretest.

**Wert:** zweite Implementierung zum Gegenlesen unseres Gen-3-Adapters; später auch Brücke zu echten Geräten. Ein USB-Adapter ist für unseren PC↔PC-Weg nicht erforderlich.

### afska/gba-link-connection

[Projekt](https://github.com/afska/gba-link-connection), [höhere Kabelschicht](https://github.com/afska/gba-link-connection/blob/HEAD/lib/LinkCable.hpp), [Wireless-Analyse](https://github.com/afska/gba-link-connection/blob/HEAD/docs/wireless_adapter.md)

Bibliothek für GBA-Homebrew auf realer Hardware. Besonders hilfreich für eigene kleine Diagnostik-ROMs, die Spieler-ID, empfangene Wörter, Taktmodus und Interrupts anzeigen. Damit kann man lokale Emulation prüfen, ohne Nintendo-Spieldaten als Testfixture zu veröffentlichen.

### gbplay

[mwpenny/gbplay](https://github.com/mwpenny/gbplay) enthält Hardware-/Serverarbeit sowie eine [Analyse mehrerer Spieleprotokolle](https://github.com/mwpenny/gbplay/blob/HEAD/docs/_posts/2022-07-24-Multi-Game-Link-Cable-Protocol-Analysis.md) mit Aufzeichnungen.

**Wert:** zeigt, dass Tetris, Pokémon und andere Spiele ihre Daten unterschiedlich organisieren. Aufzeichnungen ergänzen Emulator-gegen-Emulator-Tests, bei denen zwei gleiche Fehler sonst unbemerkt zusammenpassen könnten.

### PokemonGB_Online_Trades

[Lorenzooone/PokemonGB_Online_Trades](https://github.com/Lorenzooone/PokemonGB_Online_Trades) unterstützt spezialisierte Handelsabläufe und enthält einen [BGB-Protokolladapter](https://github.com/Lorenzooone/PokemonGB_Online_Trades/blob/HEAD/utilities/bgb_link_cable_server.py).

Wichtige Grenze: Der dort beschriebene Gen-3-Weg verwendet ein zusätzliches Multiboot-Programm aus `Pokemon-Gen3-to-Gen-X`. Das ist nicht derselbe Nachweis wie ein normaler FireRed-Kabelraum zwischen zwei unangepassten Spielen. Für Gen 1/2 sind Pufferung und Protokollprüfungen dennoch interessante Referenzen.

## 7. Was das für AetherBoy konkret bedeutet

### Wir beginnen nicht wieder bei null

Im vorhandenen Repository liegen bereits unter anderem:

- `nanoboy/Runtime/Netplay/PokemonGen3SerialAdapter.cs`
- `nanoboy/Runtime/Netplay/GbaOnlineLinkCoordinator.cs`
- `nanoboy/Runtime/Netplay/GbaOnlineLinkMachine.cs`
- `nanoboy/Runtime/Netplay/GbaOnlineLinkProtocol.cs`

Der gelesene Gen-3-Adapter kennt Handshake-, Command- und Reset-Nachrichten, Phasen, Sequenzen, begrenzte Warteschlangen und Prüfsummen. Er verlangt das passende 115200-Baud-Multiplayerprofil. Das passt grundsätzlich zur Familie der spielbezogenen Ansätze; es ist aber noch kein Kompatibilitätsnachweis für eine Edition oder einen ROM-Hack.

Der nächste sinnvolle Arbeitsschritt ist ein **gezielter Verhaltensvergleich**, kein erneuter Austausch des gesamten Netzwerk-Stacks:

1. Erwartete Abläufe aus `pret/pokefirered`/`pokeemerald` festhalten.
2. Unseren Adapter bei Rollenwahl, Prüfsummen, Leerrunden, Reset und erneutem Betreten des Kabelraums gegen gpSP und die GB-Link-Bridge vergleichen.
3. Abweichungen durch kleine reproduzierbare Tests belegen, erst danach ändern.
4. GB/GBC mit SameBoy-/BizHawk-artiger lokaler Synchronisation und getrennten Gen-1-/Gen-2-Testfällen bewerten.

### Drei Wege mit unterschiedlichen Zielen

| Weg | Vorteil | Schwierigkeit | Sinnvolle Rolle |
| --- | --- | --- | --- |
| Präzise lokale Hardware-Verbindung | Gute Grundlage für viele Spiele; keine Netzwerklatenz zwischen Geräten | Korrekte Zyklen, Modi, IRQs, Kabelzustände | Unverzichtbare Basis und lokale Referenz |
| Spielbezogene Netzwerkprotokolle | Keine zweite ROM-Instanz auf jedem PC erforderlich; kompakte Nachrichten | Profile, Besonderheiten, ROM-Hack-Abweichungen, saubere Übergänge | Kurzfristig Pokémon-Tausch, insbesondere Gen 3 |
| Replizierte gekoppelte Geräte mit Eingabesynchronisation/Rollback | Weniger spielbezogene Eingriffe; Kabel bleibt lokal | Determinismus, Zustände beider Kerne plus Kabel, Startdaten, Leistung | Langfristige generischere Online-Lösung |

Diese Wege können dieselbe sichere Transport- und Raumdienste-Schicht benutzen. Keiner zwingt uns zurück zu kryptischen Copy-Paste-SDPs. Umgekehrt behebt ein Raumcode allein keinen Timingfehler.

### Welche Nachweise vor einer Freigabe fehlen

- **Transport separat:** geordnete, nummerierte Testnachrichten verschiedener Größe, Abbruch/Neuaufbau, Diagnose ohne Geheimnisse.
- **Serial/SIO separat:** Rollenwechsel, externer Takt, CGB-Schnelltakt, GBA-Spieler-IDs, fehlende Partner und Transfers mitten im Abbruch.
- **Profil separat:** vollständiger Eintritt in den Kabelraum, Tausch, Rückkehr, zweiter Tausch; beide Host-/Client-Richtungen.
- **Verzögerung absichtlich einbauen:** beispielsweise 20/50/100 ms RTT, Schwankungen, verspätete alte Nachrichten und Verbindungsabbruch. Das sind Testparameter, keine Kompatibilitätszusagen.
- **Betriebssysteme kreuzen:** Windows↔Windows, Linux↔Linux und Windows↔Linux mit demselben Protokollstand.
- **Spielstände prüfen:** Originale unangetastet lassen, mit Sitzungskopien arbeiten, nach regulärem Speichern und Neustart auf beiden Seiten den richtigen Pokémon-Bestand prüfen. Ein offener Datenkanal und ein erreichter Tauschbildschirm reichen nicht.
- **Versionen/ROM-Hacks getrennt führen:** Ein Erfolg mit unverändertem Feuerrot bestätigt weder Rocket Edition noch alle regionalen Ausgaben. Ungeprüfte Kombinationen nicht automatisch freigeben.

### Sicherheit und Übernahme fremden Codes

Eine Bibliothek für zuverlässige UDP-/TCP-Pakete liefert nicht automatisch Authentifizierung, Verschlüsselung, Raumzugriffsschutz oder NAT-Erreichbarkeit. Diese Aufgaben von der Emulationslogik getrennt halten. Keine öffentlichen Rohlogs mit Zugangsdaten oder vollständigen Sitzungsdokumenten erzeugen.

Quellcode nur unter den konkret geltenden Bedingungen übernehmen. Besonders auffällig: DoubleCherryGBs README und die gelesene Root-Lizenzdatei sind nicht deckungsgleich; `LICENSE` enthält AGPLv3. Vor einer Übernahme muss das für die betreffenden Dateien geklärt werden. mGBAs MPL-, gpSPs GPL- und permissiv lizenzierte Komponenten ebenfalls nicht als austauschbare „frei verwendbare“ Bausteine behandeln. In dieser Recherche wurde kein fremder Implementierungscode in AetherBoy übernommen.

## Prioritätsempfehlung

**Für den nächsten tatsächlichen Entwicklungsauftrag:** vorhandenen Gen-3-Adapter anhand von pret, gpSP und GB-Link-Bridge prüfen und mit reproduzierbaren Tests absichern. Parallel im fachlichen Sinn, nicht als bereits gestartete Arbeit: GB/GBC gegen die lokalen Referenzen und dokumentierten Spielabläufe überprüfen. Danach gezielt Windows↔Linux-WAN mit geschützten Spielstandkopien testen.

**Für später:** einen kleinen, separaten Prototyp für gemeinsame Link-Snapshots und Eingabesynchronisation nach Coffee GB beziehungsweise mgba-rollback bewerten. Erst wenn Nutzen und Determinismus belegt sind, daraus eine allgemeine Online-Architektur machen.

Das Internetproblem und das Emulationsproblem bleiben zwei getrennte Prüfungen. Wir haben jetzt konkrete Referenzen für beide, aber noch keinen durch diese Recherche bestätigten Pokémon-Tausch in AetherBoy.
