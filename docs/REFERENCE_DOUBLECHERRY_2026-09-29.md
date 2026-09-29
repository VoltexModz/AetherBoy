# DoubleCherryGB: Quellcodevergleich für GB/GBC Online Link

Stand: 29. September 2026. Nur Quellenanalyse; keine Produktionsänderung, kein fremder Emulator ausgeführt, kein Pokémon-Tausch getestet.

## Geprüfter Stand

- Projekt: [TimOelrichs/doublecherryGB-libretro](https://github.com/TimOelrichs/doublecherryGB-libretro).
- Über GitHub API festgestellter `master`: [`03f58ca3dfb4b716f7e66a0e0467f9e85ef82abb`](https://github.com/TimOelrichs/doublecherryGB-libretro/commit/03f58ca3dfb4b716f7e66a0e0467f9e85ef82abb).
- Separater, nicht eingebundener Quellcache: `artifacts/link-reference-deep-20260929/doublecherry/`.
- Gelesen: SC-Schreibpfad, vollständiger serieller Empfang und Abschluss, komplette Netpacket-Manager-/Handler-Dateien, TCG-Sonderhandler und dessen Auswahl, relevantes Libretro-Schnittstellenmodell.
- Lokal verglichen: `NetworkSerialCable.cs`, `OnlineLinkCoordinator.cs`, `NetworkSerialProtocolReviewTests.cs` und der aktuelle Fix-Handoff.

Die README meldet erfolgreiche Gen1-/Gen2-Tausche und eingeschränkte Kämpfe. Das ist eine Autorenangabe, kein in diesem Arbeitspaket reproduzierter Test. Der untersuchte Quellcode zeigt einen deutlich anderen Vertrag als AetherBoys gepaarter Transfer.

## Drei verschiedene Verfahren nicht vermischen

| Verfahren | Tatsächlicher Quellpfad | Aussage für AetherBoy |
| --- | --- | --- |
| Eine emulierte Konsole, Standard-Netpacket | SC-Schreibpfad, Default-Sende-/Empfangshandler | Einfacher Byte-Request/Reply; relevant für Gen1/Gen2, aber nicht zeitlich eindeutig |
| Pokémon Trading Card Game 1/2 | `PokemonTcgNetpacketHandler`, eigene Auswahl in `auto_config_1p_link` | Spielbezogener Idle-Ersatz; kein allgemeiner RBY-/GSC-Tauschalgorithmus |
| Mehrere lokale Konsolen | Gegenseitige lokale `set_target`-Verknüpfung | Anderer lokaler Emulations-/Frontendpfad; kein Beleg für unabhängige WAN-Maschinen |

Die Auswahl ist konkret in [`auto_link_multiplayer`](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/03f58ca3dfb4b716f7e66a0e0467f9e85ef82abb/libretro/inline/inline_functions.h#L698) und [`auto_config_1p_link`](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/03f58ca3dfb4b716f7e66a0e0467f9e85ef82abb/libretro/inline/inline_functions.h#L472) sichtbar. Headernamen dienen dort der Erkennung; sie sind keine belastbare ROM-Revisionsfreigabe.

## Standard-Netpacket: rekonstruierter Datenweg

1. Ein interner SC-Start sendet den momentanen SB-Wert. Ein externer Start sendet nichts.
2. Die CPU läuft zunächst bis zum lokalen seriellen Termin. Dort wartet sie auf die Byte-Queue und pollt weiter das Frontend.
3. Nach drei Sekunden ohne Byte wird `0xFF` eingereiht; der normale Abschluss schreibt SB, beendet SC und erzeugt den seriellen Interrupt.
4. Bei externem Empfang wird der frühere lokale SB-Wert zurückgegeben, anschließend das empfangene Byte eingetragen und ein Interrupt ausgelöst. Die Prüfung auf einen tatsächlich scharfgeschalteten externen Transfer ist auskommentiert.

Belege: [`cpu::io_write`, SC](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/03f58ca3dfb4b716f7e66a0e0467f9e85ef82abb/gb_core/cpu.cpp#L405), [`cpu::receive_from_linkcable`](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/03f58ca3dfb4b716f7e66a0e0467f9e85ef82abb/gb_core/cpu.cpp#L864), [serieller Abschluss in `cpu::exec`](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/03f58ca3dfb4b716f7e66a0e0467f9e85ef82abb/gb_core/cpu.cpp#L1065).

Der Default-Empfangshandler unterscheidet Anfrage und Antwort ausschließlich anhand des **aktuellen SC-Bits 0 beim Empfang**. Extern: lokale Kabelroutine aufrufen und Ergebnis senden. Intern: Byte in FIFO einreihen. Er prüft hier weder einen Transferbezeichner noch eine explizite Paketrolle. Ein nicht mehr gültiges Byte lässt sich damit nicht einer vorherigen SC-Operation zuordnen. Im Handler fehlt auch eine eigene Prüfung auf `len == 1` vor Zugriff auf das erste Byte. Das ist eine zu prüfende Eingangsgrenze, kein hier nachgewiesener Netzwerk-Exploit. [Default-Empfang](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/03f58ca3dfb4b716f7e66a0e0467f9e85ef82abb/libretro/DoubleCherryEngine/Netplay/NetPacketReceiveHandler.cpp#L12)

Der Sender fordert zuverlässige, geordnete Zustellung und sofortiges Flushen an. Die eigentliche Vermittlung, Verbindung und Transporteigenschaften liegen beim Libretro-Frontend. Daraus folgt weder UDP-Zwang noch, dass AetherBoy WebRTC ersetzen müsste. [Default-Sender](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/03f58ca3dfb4b716f7e66a0e0467f9e85ef82abb/libretro/DoubleCherryEngine/Netplay/NetPacketSendHandler.cpp#L10), [Netpacket-API-Vertrag](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/03f58ca3dfb4b716f7e66a0e0467f9e85ef82abb/libretro/DoubleCherryEngine/libretro.h#L5511)

## Die wichtige Frage: löst das unsere gespiegelte Rollenwahl?

**Nein, nicht kausal.** Die folgende Ablaufrekonstruktion folgt den gelesenen Bedingungen. Sie ist keine Messung eines gestarteten DoubleCherry-Prozesses.

| Reihenfolge | Lokaler Zustand A | Lokaler Zustand B | Ergebnis |
| --- | --- | --- | --- |
| Beide Spiele haben vor Paketankunft ihre interne Probe gestartet | SB=`01`, SC=`81` | SB=`01`, SC=`81` | Beide Default-Handler behandeln das Gegenbyte als Antwort und reihen `01` ein |
| Beide Spielroutinen verarbeiten diesen Probe-Reply | empfangen=`01` | empfangen=`01` | Die dokumentierte Pokémon-Wahl kann beidseitig die externe Rolle wählen |
| Danach kein neuer interner Takt | extern | extern | Keine automatische komplementäre Rollenentscheidung im Default-Netzwerkcode |

Die Spiel-Tokenregeln sind in AetherBoys `NetworkSerialProtocolReviewTests` bereits mit eigenem Modell beschrieben. Der First-Host/Second-Guest-Unterschied von Netpacket bezeichnet die Netzwerkadresse, nicht die Pokémon-Rolle. Im Default-Handler gibt es keine Regel, die diese konkurrierende Wahl korrigiert.

Die positive Reihenfolge ist anders: As `01` trifft B noch während dessen externer `02`-Probe. Der Default-Handler antwortet mit Bs bisherigem `02`; B sieht `01`. So können komplementäre Spielrollen entstehen. **Der Zustand bei Paketankunft entscheidet.** Das ist der zentrale Unterschied zu AetherBoys explizitem Angebot mit registriertem SB-/SC-Snapshot und Cancel.

Auch ein bereits deaktivierter externer Port kann hier noch bedient werden. Ein erfolgreicher Versuch beweist deshalb nicht, dass dieselbe Registerfolge mit einer engeren Hardware-/Transferzuordnung funktioniert. Umgekehrt beweist die rekonstruierte ungünstige Reihenfolge nicht, dass sämtliche realen DoubleCherry-Tauschversuche scheitern.

## TCG-Pufferung: nützliche Idee, falsches Ziel für Gen1/Gen2

`PokemonTcgNetpacketHandler` erkennt besondere Handshake- und Idle-Werte. Nach einer hinreichenden Zahl von Übertragungen können beide Seiten in einen ausdrücklich ausgehandelten Idle-Modus wechseln. Dort wird der bekannte Idle-Wert lokal wiederholt statt für jeden Idle-Takt eine Netzwerkrundreise auszuführen. Neue echte Daten deaktivieren diesen Modus. Im Nicht-Idle-Modus besitzt die externe Seite einen eigenen geplanten, wartenden Empfangspfad. [Handler](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/03f58ca3dfb4b716f7e66a0e0467f9e85ef82abb/libretro/DoubleCherryEngine/Netplay/NetpacketHandler/PokemonTcgNetpacketHandler.cpp#L20)

Die Parameter sind unterschiedlich: TCG1 verwendet 130 Übertragungen, Intervall 17336 und Handshake `29/12`; TCG2 verwendet 150, 34656 und `92/21`. Der Idle-Wert ist `AC`. Diese Zahlen gehören zu diesen Profilen, nicht zu Rot/Blau/Gold/Silber. [Profilkonfiguration](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/03f58ca3dfb4b716f7e66a0e0467f9e85ef82abb/libretro/inline/inline_functions.h#L474), [Handler-Zustand](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/03f58ca3dfb4b716f7e66a0e0467f9e85ef82abb/libretro/DoubleCherryEngine/Netplay/NetpacketHandler/PokemonTcgNetpacketHandler.h#L15)

Übertragbare Erkenntnis: Netzwerklatenz lässt sich bei einem verstandenen Spielprotokoll durch ausdrücklich definierte Leerlaufbehandlung reduzieren. Nicht übertragbar: beliebige zuletzt empfangene Bytes als allgemeine Kabelantwort wiederholen. Der TCG-Weg löst auch nicht unsere vorgelagerte Gen1-/Gen2-Rollenwahl.

## Was AetherBoy bewusst anders behalten sollte

| Grenze | DoubleCherry-Standardpfad | Bestehender AetherBoy-Vertrag |
| --- | --- | --- |
| Byte-Zuordnung | FIFO, Rolle bei Ankunft | Transfer-ID und Paar-ID, SC-/SB-Snapshot |
| Abbruch vor Pairing | Kein explizites Cancel im Byteformat | Eigene Cancel-Nachricht |
| Veraltete Bestätigungen | Im Byteformat nicht unterscheidbar | ID-/Replay-Prüfung |
| Rückstau | `std::queue<byte>` ohne hier sichtbares Limit | Begrenzte Queues, sichtbarer Abbruch |
| Fehlender Partner | Nach Ablauf Ersatzbyte und serieller Abschluss | Kooperative Barriere, getrennte Diagnose, kein erfundenes Erfolgsbyte |
| Extern nicht scharfgeschaltet | Empfangsroutine erzwingt trotzdem Registerabschluss | Serieller Controller bestimmt, ob echte Clockkanten angenommen werden |

Der [Netpacket-Manager](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/03f58ca3dfb4b716f7e66a0e0467f9e85ef82abb/libretro/DoubleCherryEngine/Netplay/NetPacketManager.h#L68) hält den FIFO als globalen Singleton-Zustand. Seine gezeigten `start`-/`stop`-Methoden leeren ihn nicht ausdrücklich. Für eine Übernahme müsste Sitzungsbereinigung zusätzlich verifiziert werden; daraus wird hier kein bereits beobachteter Reconnect-Fehler abgeleitet.

AetherBoys zusätzliche Offer-/Ready-/Complete-Barrieren kosten Nachrichten und Latenz. Sie sind aber nicht bloßer Ballast: Sie sichern einen stärkeren Transfervertrag ab. Man kann sie nicht durch zwei rohe Bytes ersetzen und gleichzeitig behaupten, dieselben Garantien beizubehalten.

## Konkrete nächste Experimente

1. Den bestehenden positiven und gespiegelten Gen1-/Gen2-Probe-Test um zeitgestempelte **Registerereignisse** erweitern: SB/SC, Start/Cancel, Interrupt, lokaler emulierter Zeitpunkt, empfangene Transfer-ID. Keine kompletten Pokémon- oder Spielstanddaten in normale Diagnoseberichte.
2. Mindestens diese Ankunftsreihenfolgen getrennt prüfen: Partner noch extern scharf; Partner extern bereits abgemeldet; beide intern; Antwort verspätet nach neuem Start; Raumende während einer Probe.
3. Eine alternative Strategie zuerst isoliert modellieren: entweder koordinierte emulierte Zeit samt nachgewiesenem Skew-Vertrag oder ein offen als solches benanntes Gen1-/Gen2-Spielprotokollprofil. Keine stillen `01 → 02`-Korrekturen.
4. Bei einem Spielprotokollprofil separate Beweise für Gen1, Gen2 und Zeitkapsel verlangen. TCG-Werte dürfen nicht als generisches Pokémon-Protokoll gelten.
5. Erst nach reproduzierbarer Rollenwahl lange Austauschabschnitte bei 0/20/60/120 ms Verzögerung messen. Ein offener Kanal oder ein einzelnes korrektes Byte ist kein Tauschbeweis.

Für den nächsten Produktionsschritt liefert DoubleCherry daher **keinen kleinen, nachweislich sicheren Ersatz für `TryPair`**. Es liefert eine wertvolle Gegenreferenz, warum einfachere Implementierungen manchmal durchkommen und warum ihre Erfolgsmeldungen keinen identischen Hardwarevertrag belegen.

### Enger eigener PoC: Wahlbarriere vor dem Vorlauf

Im Agentenabgleich entstand eine zusätzliche, noch nicht implementierte Möglichkeit: Ein ausdrücklich gewähltes Gen1-/Gen2-Profil könnte die allererste echte `02/80`-Wahlprobe auf beiden Owner-Threads parken, bevor ein Spiel sie ersetzt. Nach beidseitiger Bestätigung derselben Wahl-Epoche und der geparkten Offer-IDs läuft nur der Netzwerkhost weiter. Der Gast bleibt mit seiner echten externen Probe stehen. Erst die anschließend durch das Host-Spiel selbst erzeugte interne `01/81`-Probe löst das normale Pairing aus. Damit würden echte Spielbytes getauscht, nicht `01` in `02` umgeschrieben.

Der bestehende Start/Stop-Vertrag verbietet das nicht grundsätzlich. Allerdings schreibt das Host-Spiel zunächst SB, während SC noch extern ist. `SerialDataWritten` kann deshalb ein vorübergehendes externes `01`-Angebot mit Cancel/Neustart erzeugen. Die zusätzliche Barriere dürfte erst auf das passende **interne** Angebot reagieren, nicht auf irgendein `01`-Byte. `Ready`/`Complete`, Paketkennungen und geordnete Cancel-Verarbeitung bleiben notwendig.

Die [Gen1-Wahlroutine](https://github.com/pret/pokered/blob/master/engine/link/cable_club_npc.asm#L16) enthält die passende kurze externe-zu-interne Reihenfolge. Aber `CloseLinkConnection` setzt ebenfalls `02/80` und kehrt danach zurück. Deshalb ist diese Registerkombination allein **keine sichere Erkennung einer neuen Wahl**. Ein einmaliger PoC mit expliziter Benutzeraktivierung ist plausibel; automatischer Wiedereintritt verlangt geprüfte Profil-/Phasenmarker. Das ist noch keine Gen2-/Zeitkapsel-Freigabe.

Pflichttests wären: gleichzeitiger und stark versetzter Beginn, SB-Umschreibung vor SC, alte Cancel/Ready-Ereignisse, Abbruch eines geparkten Partners, CGB-Doublespeed, normale Nutzlast `02`, Verlassen ohne Wiederbeitritt und eine begrenzte Wandzeitfrist. Dieser Ansatz löst außerdem nicht die anschließenden Rundreisen pro Byte; Durchsatz muss separat gemessen werden.

## Lizenzbefund, keine Codeübernahme

- Die tatsächliche [Root-LICENSE](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/03f58ca3dfb4b716f7e66a0e0467f9e85ef82abb/LICENSE) enthält **GNU Affero General Public License, Version 3**.
- Die [README](https://github.com/TimOelrichs/doublecherryGB-libretro/blob/03f58ca3dfb4b716f7e66a0e0467f9e85ef82abb/README.md) bezeichnet das Projekt dagegen als GPLv3.
- `gb_core/cpu.cpp` und `gb_core/gb.cpp` tragen ältere TGB-Dual-Dateiheader mit **GPLv2 oder später**.
- Die gelesenen neuen Netpacket-Dateien enthalten keinen eigenen abweichenden Lizenztext.

Das ist ein dokumentationsbedürftiger Unterschied; nicht pauschal als MIT, lizenzfrei oder bloß GPLv3 behandeln. Vor wörtlicher Übernahme die konkret betroffenen Dateien und den Projekt-Lizenzkontext klären. In diesem Arbeitspaket wurde ausschließlich gelesen und verglichen, nichts in den Emulator übernommen.
