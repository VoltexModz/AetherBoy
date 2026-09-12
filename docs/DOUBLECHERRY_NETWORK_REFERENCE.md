# DoubleCherryGB: Netzwerk-Link-Referenz / network-link reference

Stand / reviewed: 2026-09-12. This is a source audit, not a compatibility claim or a port of DoubleCherryGB.

## Quelle und Umfang / source and scope

- Upstream: [TimOelrichs/doublecherryGB-libretro](https://github.com/TimOelrichs/doublecherryGB-libretro).
- Downloaded master snapshot: `493a9ab52dc9fc17965e413a119b76d1b94d7261`, identified from the GitHub ZIP end-of-central-directory comment. The wrapper reports `v0.19.0`.
- Archive SHA-256: `CEE1E764CD5588D5D910ECEFA02BE4E64E63CF2DB19F22F55639697EEDE13BBB`.
- Local reference: `.local-tools/doublecherry-reference-20260912/doublecherryGB-libretro-master/`. The reference directory and ZIP are ignored by Git, not compiled, and not shipped with AetherBoy.
- Read the netpacket manager, default send/receive handlers, Pokémon TCG handler, CPU serial integration, GB execution wrapper, libretro wrapper, option/activation functions, link-master helper and license text. The full peripheral catalogue, graphics/audio implementations, and vendored networking libraries were not exhaustively audited; they are not the direct cable-data path.
- No DoubleCherryGB implementation was copied into AetherBoy by this review. Suggestions below describe an independently designed protocol, not wire compatibility with DoubleCherryGB.

### Lizenzbefund / license finding

The downloaded root `LICENSE` contains **GNU AGPL version 3**, whereas the README calls the project **GPLv3**. The inherited CPU/GB files and link-master helper contain **GPL version 2 or later** notices. This is an upstream provenance inconsistency, not a permissive/no-license grant. Inspect the exact files and clarify the intended grant before any future source reuse. Renaming classes or translating C++ to C# would not itself resolve licensing obligations. This review uses the project as a technical reference only. [Pinned LICENSE](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/493a9ab52dc9fc17965e413a119b76d1b94d7261/LICENSE)

## Deutsch

### Ergebnis

DoubleCherryGB bestätigt, dass GB/GBC-Netzwerktausch mit **einem eigenen Spiel pro Rechner und Kabeldaten statt vollständiger Spielstände** möglich ist. Es enthält aber keine allgemeine GBA-Lösung, keinen eigenen WebRTC-Verbindungsaufbau und keine Garantie für beliebige Link-Spiele. RetroArch übernimmt den eigentlichen Transport, Verbindungsaufbau und Lobbybetrieb. Die Netpacket API ist die Schnittstelle zwischen Kern und Frontend, kein eigenständiges Netzwerkprotokoll mit eingebauter Verschlüsselung.

Das Projekt unterstützt daneben einen getrennten Mehrkern-/Savestate-Netplay-Pfad. Diese beiden Betriebsarten dürfen nicht vermischt werden. Die README empfiehlt den direkten Kabelpfad hauptsächlich zum Tauschen; Kämpfe sind eingeschränkt. Die Aktivierung im untersuchten Code ist enger als die vereinfachte README-Anleitung: Bei einer emulierten Konsole wird die Schnittstelle für erkannte Handel-/Kampf-Titel oder bei ausdrücklich erzwungenem Kabel-over-IP-Modus registriert. Eine Titel-Heuristik beweist keine getestete Spielkompatibilität.

### Tatsächlicher Ablauf im Standardpfad

1. Beim Schreiben von `SC` werden Transferstart und interner/externer Takt ausgewertet. Nur ein Start mit internem Takt sendet sofort das aktuelle `SB` als **ein Byte Nutzlast** an Client 0 oder 1. Ein Abschlussereignis wird anhand der emulierten Uhr vorgemerkt.
2. Die Standard-Sendefunktion verlangt zuverlässige, geordnete Zustellung und sofortiges Leeren des Sendepuffers. Die Paketnutzlast enthält weder Typ noch Sitzungsnummer, Transfernummer, Zykluszeit, Prüfsumme oder ROM-/Save-Daten.
3. Die Empfangsfunktion entscheidet anhand des **jetzigen** `SC`-Taktbits, ob ein Paket eine Anfrage oder Antwort sein soll. Beim extern getakteten Gerät ruft sie direkt die serielle Empfangsfunktion auf und antwortet mit dessen vorherigem `SB`. Beim intern getakteten Gerät legt sie das Byte in eine FIFO.
4. Die serielle Empfangsfunktion schreibt `SB`, löscht das Startbit und fordert den seriellen Interrupt an. Die Abfrage, ob der Empfänger tatsächlich extern getaktet **und aktiviert** ist, ist im untersuchten Stand auskommentiert. Deshalb kann der Standardpfad auch bei nicht scharfgeschaltetem externem Port einen Interrupt auslösen. Das ist eine Approximation, die wir nicht übernehmen sollten.
5. Die CPU fragt eingehende Pakete zwischen Instruktionen ab. Am vorgesehenen Transferabschluss wartet der interne Taktgeber bei leerer FIFO in einer Schleife. Diese ruft den Empfang auf und schläft jeweils 1 ms reale Zeit. **Während dieses Wartens laufen die emulierten CPU-/Timer-Zähler nicht weiter.**
6. Nach höchstens drei Sekunden realer Zeit legt der Standardpfad `0xFF` in die FIFO. Anschließend wird das Byte übernommen, der Transfer beendet und der Interrupt angefordert. Das verhindert ein unbegrenztes Warten, unterscheidet aber einen Netzfehler nicht durch einen eigenen Protokollzustand von einem empfangenen Byte.

Relevante Quellen: [CPU-Integration](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/493a9ab52dc9fc17965e413a119b76d1b94d7261/gb_core/cpu.cpp), [Paketverwaltung](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/493a9ab52dc9fc17965e413a119b76d1b94d7261/libretro/DoubleCherryEngine/Netplay/NetPacketManager.h), [Standardempfang](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/493a9ab52dc9fc17965e413a119b76d1b94d7261/libretro/DoubleCherryEngine/Netplay/NetPacketReceiveHandler.cpp).

### Grenzen der Referenzimplementierung

- **Keine echte Master-Arbitration auf Netzwerkebene:** Client-/Hostrolle ist nicht die serielle Taktgeberrolle. Wenn beide Seiten gleichzeitig intern starten, landen die beiden nackten Anfragen jeweils als vermeintliche Antwort in der FIFO. Das ist keine explizite Konfliktauflösung. Ebenso kann ein Taktwechsel zwischen Senden und Empfang die Interpretation eines späten Bytes ändern.
- **Keine Transferzuordnung:** Verspätete Antworten nach einem Timeout können im nächsten Transfer landen. Zuverlässige Reihenfolge allein verhindert dieses Problem nicht.
- **Unbegrenzte FIFO und fehlende Standard-Längenprüfung:** Der Standardempfänger greift auf das erste Byte zu, ohne `len` zu prüfen. Er validiert auch keine erlaubte Gegenstelle selbst. Frontend-Schutzmaßnahmen wurden hier nicht vollständig auditiert; die Beobachtung betrifft diese Kernschicht.
- **Unvollständiger Abbau:** `stop()` löscht Callback-Zeiger, aber nicht ausdrücklich Aktivflag, FIFO, Wartezustand oder Clientnummer. `disconnected()` verringert nur einen Zähler. Wir brauchen einen eigenen vollständig zurückgesetzten Sitzungszustand und Abbruchmöglichkeiten während jeder Wartephase.
- **Timing nicht blind übernehmen:** Der untersuchte `SC`-Pfad verwendet für DMG einen anderen Abschlussabstand als für CGB mit normalem seriellen Takt. Seine Konstanten sind kein Hardware-Orakel. Für unseren Kern gelten Hardware-Dokumentation und Regressionstests.
- **Kein atomarer Online-Savestate:** Die Paket-FIFO und laufende Netzwerktransaktion werden nicht als konsistenter gemeinsamer Zustand beider PCs gesichert. Der vorhandene Savestate-/Rollbackpfad ist ein anderer Betriebsmodus. Einzelnes Laden, Rewind oder Turbo während einer direkten Kabelsitzung müssen gesperrt oder gemeinsam koordiniert werden.

### Pokémon-TCG-Sonderbehandlung

Der TCG-Handler ist kein allgemeines Pokémon-RPG- oder GBA-Protokoll. Er erkennt spielabhängige Handshake-/Idle-Bytes und zählt Transfers. TCG 1 und 2 bekommen unterschiedliche Schwellen und Intervalle. Nach ausreichend vielen übereinstimmenden Idle-Transfers wird ein Zweibyte-Kommando zum Umschalten des Idle-Modus gesendet. In diesem Modus werden erwartete Idle-Antworten lokal erzeugt und entsprechende Netzwerktransfers unterdrückt. Sobald Nicht-Idle-Daten auftreten, wird wieder auf echten Austausch umgeschaltet. Im normalen Datenmodus wartet auch die externe Seite an vorgesehenen seriellen Grenzen.

Das ist ein bewusst spielbezogener Optimierungs-/Kompatibilitätsweg. Daraus folgt ausdrücklich **nicht**, dass beliebige Dummy-Daten einen allgemeinen GBA-Link stabil machen. [TCG-Handler](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/493a9ab52dc9fc17965e413a119b76d1b94d7261/libretro/DoubleCherryEngine/Netplay/NetpacketHandler/PokemonTcgNetpacketHandler.cpp)

### Korrekturen am vorgeschlagenen Fahrplan

- Reale Wartezeit und emulierte Zeit sind verschieden. Eine 40-ms-Netzwerkwartezeit löst nicht automatisch einen 40-ms-Spieltimeout aus, wenn der gesamte relevante emulierte Zeitablauf anhält. Nur die CPU zu stoppen, während Timer/DMA/PPU weiterlaufen, wäre hingegen inkonsistent. Auch die andere Konsole und gegebenenfalls Echtzeituhrquellen müssen berücksichtigt werden.
- Pokémon-Kämpfe sind rundenbasiert. Dass die README sie als instabiler beschreibt, beweist keine einzelne Ursache wie einen bestimmten Echtzeit-Heartbeat. Dafür wären Protokolltraces und ROM-spezifische Reproduktionen nötig.
- GBA-Multiplayer überträgt 16 Bit je Teilnehmer; normaler serieller GBA-Modus hat getrennte 8-/32-Bit-Varianten. Diese Modi dürfen im Netzwerkprotokoll nicht vermischt werden.
- Ein erfolgreicher lokaler Tausch belegt diesen getesteten Ablauf, nicht die Perfektion aller Kabelmodi, Abbrüche, Editionen und Spiele.
- Für den direkten Kabelpfad müssen nicht beide kompletten Spiele deterministisch repliziert werden. Ein späterer Eingabe-/Rollbackpfad braucht dagegen vollständige gemeinsame Snapshots einschließlich Kabelzustand. Ein pauschales Verbot von `float`/`double` in der ganzen Anwendung ist dafür nicht nötig: Entscheidend sind reproduzierbare emulationsrelevante Zustände, Scheduler, RTC-/Zufallsquellen und alle rückwirkungsfähigen Berechnungen. Reine Darstellung ist gesondert zu betrachten.

### Eigenständige AetherBoy-Architektur: Anforderungen

1. **Transport und Emulation trennen.** Eine plattformneutrale, authentisierte und verschlüsselte Transport-Schnittstelle trägt kompakte Binärnachrichten. WebRTC ist eine mögliche Implementierung, kein Ersatz für die Kabel-Zustandsmaschine. NAT-Durchquerung kann STUN/TURN und Signalisierung benötigen; eine garantierte Verbindung hinter beliebigen Routern ohne jegliche Infrastruktur wird nicht versprochen.
2. **Versionierter Sitzungs-Handshake.** Protokollversion, Systemfamilie, Fähigkeiten, Rollen und Grenzen prüfen. GB/GBC untereinander zulassen; GB/GBC gegen GBA ablehnen. Unterschiedliche kompatible Editionen dürfen unterschiedliche ROM-Hashes haben. ROMs und vollständige Saves werden nicht automatisch übertragen.
3. **Explizite Transaktionen.** Jede Start-/Antwort-/Abbruchnachricht bekommt Sitzungskennung und Transferkennung; Nachrichtentyp, Länge, Flags und Gegenstelle werden validiert. Ein wiederholtes Paket darf keinen zweiten Hardwareinterrupt erzeugen. Alte Sitzungs- oder Transferpakete werden verworfen oder führen zu einem erklärten Protokollabbruch.
4. **Klare Takt- und Bereitschaftsregeln.** Netzwerkhost und serieller Taktgeber sind getrennte Rollen. Aktive externe Transfers, unbewaffnete Ports, gleichzeitige interne Starts, Wechsel und Abbruch brauchen definierte Regeln. Kein Erfolgs-Byte erfinden, um einen ungelösten Konflikt zu verstecken.
5. **Emulationsgrenzen statt UI-Blockaden.** Nur der Emulationsbesitzer ändert Register. Netzwerkempfang liefert begrenzte Nachrichtenwarteschlangen. Beim Warten bleibt die Oberfläche bedienbar; Abbruch und getrennte Wandzeit-Timeouts bleiben möglich. Nicht autorisierte Änderungen an laufenden Transfers werden abgewiesen.
6. **Spielstandschutz.** Eine Sitzung darf nicht blind bestehende Saves ersetzen. Vorherige lokale Sicherung und separate Sitzungsdaten sind sinnvoll; bei Abbruch nicht behaupten, beide Spiele hätten den Tausch dauerhaft abgeschlossen. Persönliche Trainer-/Pokémon-Daten gehören zum beabsichtigten Kabelinhalt, vollständige Dateien jedoch nicht.
7. **Ehrliche Stufen.** Zuerst GB/GBC-Nachrichten-/Registertests, dann zwei Prozesse, dann Windows/Linux und absichtlich verzögerte/verlorene/duplizierte/veraltete Pakete, zuletzt echte kompatible Spiele mit Testspielständen. GBA benötigt denselben Sicherheitsrahmen, aber eine eigene SIO-Zustandsmaschine und eigene Spieltests. Eine getestete Byte-Demo ist kein nachgewiesener Pokémon-Tausch.

Hardwaregrundlage für GB/GBC: Ein normaler Transfer braucht acht serielle Takte; bei 8192 Hz sind das etwa 0,977 ms pro Byte. CGB-Fast-Clock bei normaler CPU-Geschwindigkeit liefert etwa 30,5 µs pro Byte. Diese Hardwarezeiten sind nicht mit zulässiger Internetlatenz gleichzusetzen. [Pan Docs: serial transfer](https://github.com/gbdev/pandocs/blob/master/src/Serial_Data_Transfer_(Link_Cable).md)

## English

### What the source actually demonstrates

DoubleCherryGB offers two distinct mechanisms: replicated multi-instance/savestate netplay, and a one-active-console-per-peer cable-data mode. The latter is useful evidence for private GB/GBC trading without exchanging both ROMs or complete save files. It is not a ready-made GBA backend and does not provide its own WebRTC stack. RetroArch supplies the connection/transport facilities behind the netpacket callbacks.

The default serial path sends one payload byte when an internal-clock transfer is armed. Incoming packets are classified by the receiver's current serial clock bit. An externally clocked receiver immediately exchanges its current `SB`, clears transfer state and raises the serial interrupt; its armed-state check is commented out. An internally clocked receiver queues the byte. At its scheduled completion boundary, the CPU polls that FIFO with 1-ms wall-time sleeps for up to three seconds, without advancing emulated CPU/timer counters during the wait. Timeout injects `0xFF` and completes the transfer. This can prevent permanent hangs, but it is not an explicit network-failure transaction.

Reliability and ordering come from RetroArch's send flags. Payloads do not carry session/transfer identifiers, message types, timestamps, or per-transfer authentication. The inspected core-side default handler has no payload-length check or queue bound. Stop/disconnect handling does not comprehensively clear its protocol state. Crossed internal starts are interpreted as queued replies rather than handled by an explicit arbitration protocol. A late reply can be confused with a later transaction.

Activation is restricted to known trading/battling title heuristics or a forced cable-over-IP option when one console is active. The README's simpler one-console instructions must not be interpreted as verified support for every game. The Pokémon TCG 1/2 handler is a separate game-specific optimisation: it detects idle exchanges, communicates an idle-mode command, suppresses repetitive network transfers and locally synthesises known idle responses. These are not arbitrary safe replacement bytes for GBA Pokémon trades.

### Implications for our implementation

- Preserve the architectural idea, not the approximation: own versioned binary protocol, bounded queues, explicit session/transfer identities, validated peer/messages, finite cancellable waits and clean session teardown.
- Network host/client and hardware internal/external clock roles are different. Test master changes, crossed starts, unarmed responders, abort/restart and stale replies explicitly.
- Do not guess success bytes. Freeze the relevant emulated timeline at a defined barrier while the UI remains responsive, or use a genuinely consistent rollback design. Sleeping for 40 ms of wall time is not inherently a 40-ms in-game timeout. Independent peer scheduling still needs careful coordination.
- GBA Multiplayer is 16-bit per participant; GBA normal serial 8-/32-bit modes are separate. They need their own scheduler/state implementation. DoubleCherry's GB code is not evidence of GBA compatibility.
- Separate normal transport faults from emulated unplug behavior. A real disconnected serial input can produce ones; that does not justify quietly continuing a failed online trade as if it had succeeded.
- Keep ROM and save files local by default. A compatible-edition handshake need not require identical ROM hashes. Cable data intentionally contains the game information needed for trading, which may include trainer and Pokémon information.
- Protect existing saves, lock time-manipulation features or coordinate them across peers, and make incomplete/aborted sessions explicit. The other peer's eventual durable save cannot be guaranteed solely by a successful local network write.
- WebRTC can provide an encrypted transport with ICE/STUN/TURN support, but its integration, signaling and relay deployment are independent work. The Libretro netpacket API is not synonymous with WebRTC, peer authentication or encryption.
- Claim only what was verified: source review, protocol tests, local process tests, native Windows/Linux tests and actual game trades are separate evidence levels. No real Pokémon trade or WAN performance was validated by this reference audit.

The downloaded reference stays outside the product and Git history. No commit or push was performed as part of this audit.
