# GBA-Online-Link für AetherBoy

## Empfehlung

Für Pokémon-Tausch zwischen Windows und Linux ist ein **eigener, ausdrücklich auf Pokémon Generation 3 begrenzter Protokolladapter über die vorhandene WebRTC-Verbindung** der sinnvollste nächste Schritt. Dabei läuft auf jedem Rechner weiterhin nur das eigene Spiel. Der vorhandene GBADotnet-abgeleitete GBA-Kern bleibt erhalten; ein zweiter Emulator ist nicht erforderlich.

Der entscheidende Referenzfund ist gpSP: Der aktuelle Quellstand enthält neben dem Wireless Adapter einen separaten Pokémon-Gen3-Kabelmodus. Spielbezogene serielle Backends wurden mit Commit `5db42ed4` vom 3. September 2025 eingeführt. Die aktuelle Optionsdefinition bietet diesen Modus weiterhin an. Es handelt sich also um einen konkreten Implementierungsansatz, nicht nur um eine Idee auf einer Roadmap.[^1][^2]

**Empfohlenes Produktziel:** Zwei geprüfte Gen3-Editionen können über ihre normalen Kabelräume im Spiel tauschen; die beteiligten Originalspielstände bleiben geschützt. Das ist enger als „jedes GBA-Spiel über Internet“. Diese Einschränkung sollte in der Oberfläche sichtbar bleiben, bis entsprechende Tests zusätzliche Spiele freigeben.

Ein generischer, zyklusnah synchronisierter GBA-Netzwerkkabelmodus bleibt eine wertvolle spätere Ergänzung, insbesondere für LAN und nicht unterstützte Protokolle. Ihn zuerst zum universellen WAN-System auszubauen wäre für das konkrete Tauschziel voraussichtlich der riskantere Weg. Vollständiges Rollback beider emulierter Geräte ist eine dritte, deutlich größere Architektur und kein notwendiger Einstieg.

## Befund und Aussagegrenzen

Bewertungsstand ist der 12. September 2026. Grundlage sind veröffentlichte Originalimplementierungen, technische Dokumentation und der lokale AetherBoy-Arbeitsstand. Quelltext belegt vorhandene Funktionen und deren Mechanik; er beweist weder Fehlerfreiheit noch einen erfolgreichen Tausch mit AetherBoy. Empfehlungen, Lastannahmen und Abnahmekriterien in diesem Bericht sind Entwurfsvorschläge, keine bereits implementierten Eigenschaften.

Die maßgeblichen geprüften Quellstände sind:

| Projekt | Geprüfter Commit | Stand des Commits | Bedeutung |
| --- | --- | --- | --- |
| gpSP / libretro | `8d268a6bb2cd799f8f2791ebb544a7ef550cfc6f` | 25.08.2026 | Pokémon-Protokolladapter, Optionen und Netpacket-Anbindung |
| mGBA | `543a197582c30364584d773a974d7f991892fa43` | 10.09.2026 | SIO-Modi, Transferzeiten, lokaler Koordinator |
| VisualBoyAdvance-M | `fd13034143c128c8b68133a7a18bc785178ec4e4` | 12.09.2026 | Existierende Socket-Kabelimplementierung und Zeitkoordination |
| pret/pokeemerald | `5eff78649e7170a877b961ef0b3da13b81a16038` | 01.09.2026 | Rekonstruktion des tatsächlichen Emerald-Linkverhaltens |
| pret/pokefirered | `c75f352304d529f6ba92d4f74b9cf8b5c3810788` | 04.08.2026 | Gegenprüfung an FireRed |

Die pret-Projekte sind öffentlich zugängliche Rekonstruktionen, keine von Nintendo veröffentlichte Emulator-API. Untersucht wurden die einschlägigen Link-Routinen; weder ROM-Dateien noch Spielressourcen sind Bestandteil dieses Berichts. Eine Region oder ein Hack darf nicht allein wegen ähnlicher Bezeichnungen als kompatibel gelten.

Der lokale AetherBoy-Stand besteht aus HEAD `22a77ef84af1b2f37a9342df908d4abd35efff53` **plus noch nicht committeten Änderungen**. Daher identifiziert der HEAD allein die hier betrachtete Implementierung nicht vollständig. Die Projektquellen und Übergaben am Ende dieses Berichts bezeichnen die tatsächlich relevanten Dateien.[^19]

## Technische Grundlagen

### Kabel, Funkadapter und Internet sind verschiedene Schichten

Der GBA besitzt mehrere serielle Betriebsarten. Im Multiplayer-Kabelmodus sendet jeder Teilnehmer ein 16-Bit-Wort; die vier Empfangsregister bilden die Teilnehmerplätze ab. Normaler serieller Betrieb mit 8 beziehungsweise 32 Bit, UART, GPIO und Joybus sind andere Modi. Für einen Pokémon-Kabeladapter reicht deshalb weder ein GB-Byteprotokoll noch die Aussage „GBA sendet 32 Bit“.[^3]

Ein GBA-Wireless-Adapter ist ein emuliertes Zubehörgerät mit eigener Kommandoschnittstelle. Er wird nicht dadurch bereitgestellt, dass die Rechner über WLAN verbunden sind. Ebenso kann ein virtuelles GBA-Kabel über das Internet laufen, obwohl die Originalspiele nur ein physisches Kabel kennen. Die Wahl des Internettransports bestimmt nicht automatisch den emulierten Zubehörtyp.[^4]

Für die Oberfläche folgt daraus eine klare Trennung: **lokales Kabel**, **Pokémon-Gen3-Onlineprofil** und gegebenenfalls später **GBA-Wireless-Adapter**. „Online“ als einzige Funktionsbeschreibung würde unterschiedliche Kompatibilitätsversprechen vermischen.

### Reale Wartezeit ist nicht automatisch emulierte Wartezeit

Die Behauptung, ein GBA-Spiel müsse bei 40 Millisekunden Netzwerkwarten sofort abbrechen, ist zu pauschal. Steht die gesamte emulierte Maschine still, laufen ihre CPU, Timer und Bildereignisse in dieser Zeit nicht weiter. mGBAs Koordinator verwendet tatsächlich Anhalten und Wiederaufwecken beteiligter Emulationsthreads; das ist von einem bloßen verzögerten Registerupdate bei weiterlaufendem Spiel zu unterscheiden.[^5]

Das löst jedoch nicht alle Probleme: Ein anderer Rechner könnte bereits zu weit vorgelaufen sein. Außerdem können viele aufeinanderfolgende Wartephasen die reale Geschwindigkeit unbrauchbar reduzieren. Eine richtige technische Aussage lautet deshalb: **Die Reihenfolge und der Zeitpunkt emulierter Ereignisse müssen stimmen; zusätzlich muss die Zahl internetabhängiger Wartepunkte klein genug sein.** Diese beiden Anforderungen sind getrennt zu prüfen.

In der untersuchten Emerald-Rekonstruktion gibt es Prüfungen auf neun serielle Interrupts im verbundenen Master-Ablauf und auf mehr als zehn VBlanks ohne seriellen Interrupt beim Gegenstück. Auch Frame-Daten, Prüfsummen und Warteschlangen werden ausdrücklich behandelt. Das widerlegt die Annahme, das Spiel warte beim Tauschen generell unbegrenzt auf beliebige Bytes.[^6]

FireRed enthält entsprechende Mechanismen mit 115200-Bit/s-Konfiguration, Interruptzählung und Leerlaufüberwachung. Unterschiede der Editionen und Builds bleiben dennoch zu berücksichtigen; die ähnliche Struktur ist ein guter Entwicklungsansatz, keine fertige Freigabe aller Sprachfassungen.[^7]

### Warum die Paketgranularität wichtiger ist als die Bandbreite

Ein vereinfachtes Rechenmodell zeigt das Problem. Angenommen, ein nachgebildeter Spielframe benötigt neun streng aufeinanderfolgende Internet-Rundreisen. Bei 40 ms RTT entstehen allein dadurch 360 ms reale Wartezeit. Selbst ohne Rechenarbeit wären so nur ungefähr 2,8 solcher Frames pro Sekunde möglich.

| Angenommene RTT | Neun abhängige Rundreisen | Theoretische Obergrenze dieses Modells |
| --- | ---: | ---: |
| 20 ms | 180 ms | 5,6 Frames/s |
| 40 ms | 360 ms | 2,8 Frames/s |
| 60 ms | 540 ms | 1,9 Frames/s |

Dies ist **keine Messung** einer Deutschland–Österreich-Verbindung und keine Vorhersage für jede Kabelimplementierung. Es illustriert nur die Kosten einer naiven Stop-and-wait-Architektur. Pipelining oder weniger Barrieren verändern die Rechnung; die Internetlatenz selbst verschwindet dadurch nicht.

Ein Wechsel von TCP zu WebRTC behebt diese Abhängigkeiten nicht. Der größere Hebel ist, weniger feingranulare Vorgänge synchron über die Entfernung abzuwickeln. Genau deshalb ist eine höhere, spielbezogene Protokollebene für das begrenzte Pokémon-Ziel interessant.

## Vergleich der Referenzimplementierungen

### gpSP: der relevanteste Ansatz für das konkrete Tauschziel

In `serial_proto.c` rekonstruiert gpSP den Gen3-Ablauf oberhalb einzelner Kabeltransfers. Der Code unterscheidet Handshake und verbundenen Zustand, bündelt acht Datenwörter, bearbeitet Prüfsummen lokal und hält Warteschlangen für Gegenstellen. Auf der Gastseite erzeugt er passende lokale serielle Ereignisse; wenn Nutzdaten fehlen, sieht das Spiel zeitweise Leerlauf. Das ist eine bewusste Approximation eines bekannten Spielprotokolls, keine allgemeine elektrische Kabelsimulation.[^8]

Die Grenzen sind im selben Quelltext sichtbar: festgelegte Handshakewerte, angenäherte Ereignisabstände, Re-Handshake-Erkennung über wiederholte Werte und eine begrenzte Empfangswarteschlange. Bei Überfüllung wird dort ein Paket verworfen. Diese Entscheidungen sind Referenzmaterial, nicht unverändert zu übernehmende Sicherheitsregeln. Insbesondere darf AetherBoy wichtige Tauschdaten nicht still fallen lassen.[^8]

Die Libretro-Anbindung transportiert solche Nachrichten über Netpacket mit zuverlässiger Zustellung und einem Hinweis zum schnellen Senden. Sie vermittelt Empfang und Teilnehmerverwaltung an das jeweilige serielle Backend. Netpacket ist also die Schnittstelle zum Frontend, nicht selbst WebRTC und auch kein automatisch interoperables Pokémon-Netzwerkformat.[^9]

Die Autokonfiguration von gpSP ordnet unter anderem Rubin und Saphir einem Pokémon-Kabelprofil zu; für spätere Editionen existiert daneben der Funkadapterpfad. Für AetherBoy ist diese Differenz wichtig: „Automatisch“ in einem anderen Emulator bedeutet nicht zwingend denselben Modus für jede Gen3-Edition.[^10]

**Bewertung:** Der Quelltext belegt eine tragfähige Architekturidee für einen spezialisierten Adapter. Er liefert keine belastbare Erfolgsquote für alle Editionen, Regionen, Hacks und Internetbedingungen. AetherBoy sollte das Prinzip unabhängig umsetzen und eine eigene Kompatibilitätsmatrix führen.

### mGBA: Referenz für lokalen SIO-Vertrag, nicht fertige WAN-Lösung

Die aktuelle Projektbeschreibung nennt lokales Linkkabel als vorhandene Funktion und Netzwerk-Link als geplant. Diese Aussage wird durch die geprüfte Qt-Anbindung gestützt: Sie verbindet lokale Geräte mit einem `GBASIOLockstepCoordinator`. Das Vorhandensein eines Multiplayer-Menüs ist somit kein Nachweis für einen allgemeinen mGBA-LAN-Modus.[^11][^12]

Die SIO-Implementierung trennt die Betriebsarten und berechnet Transferzeiten einschließlich Teilnehmerzahl. Der Koordinator führt Ereignisse mit emulierten Zeitpunkten, Teilnehmerzuständen und Abschlusskoordination. Das ist für Registerverhalten und lokales Timing besonders nützlich. Es macht daraus noch keinen transportfertigen Internetdienst.[^5][^13]

**Bewertung:** Unser lokaler GBA-Kabelpfad sollte weiterhin gegen diese Art von Hardwarevertrag geprüft werden. Für den spezialisierten WAN-Modus ist mGBA eine ergänzende Referenz, während gpSP die passendere Paketstrategie zeigt.

### VisualBoyAdvance-M: direkter Socket-Link ist tatsächlich möglich

VBA-M enthält einen eigenen `LINK_CABLE_SOCKET`-Pfad. Im geprüften Stand sendet der Parent Transferdaten und einen Zeitbezug; die Gastseite berücksichtigt diese Reihenfolge beim Antworten. Die Socket-Implementierung bremst eine vorauslaufende Gegenstelle und kann sie ohne weitere Emulationsfortschritte warten lassen. Das ist ein konkretes Gegenbeispiel zur Behauptung, roher GBA-Kabelverkehr über ein Netzwerk sei prinzipiell ausgeschlossen.[^14]

Die geprüfte Startfunktion bearbeitet dort den Multiplayer-Modus; Normal-8/32 und UART werden an dieser Stelle nicht als entsprechende Kabeltransfers implementiert. Außerdem benötigt der Ansatz laufende Zeitkoordination. Die Existenz dieses Codes belegt deshalb weder universelle Modusabdeckung noch angenehme WAN-Geschwindigkeit.[^14]

**Bewertung:** Nützliche Referenz für einen späteren generischen Link und für Fehlerfälle wie vorauslaufende Geräte, Pausen und Zeitüberläufe. Kein Grund, Routerfreigaben, globale Zustände oder den gesamten fremden Netzwerkcode in AetherBoy einzubauen.

### Wireless Adapter, Dolphin und Tango

Der gpSP-Entwickler beschreibt bereits im Januar 2024 erfolgreiche Funkadapter-Nutzung unter anderem mit Feuerrot, Blattgrün und Smaragd. Sein Bericht unterscheidet ausdrücklich latenzverträgliche und problematische Spiele. Die damalige Aussage, Kabelunterstützung sei Zukunftsarbeit, wurde durch den späteren spielbezogenen Backend-Commit teilweise überholt. Sie bleibt als Erklärung des Funkansatzes nützlich.[^4]

Dolphins integriertes mGBA ermöglicht synchronisierte GBA-Geräte im Rahmen von GameCube-Verbindungen und Netplay. Die offizielle Anleitung beschreibt GBA↔GameCube-Szenarien und zusätzliche ROM-/Save-Voraussetzungen. Das ist nicht dasselbe Produkt wie zwei unabhängige GBA-Pokémon-Spiele, deren Besitzer keine vollständigen Spielstände austauschen wollen.[^15]

Tango zeigt wiederum, dass spezialisierte GBA-Netplay-Produkte mit Rollback möglich sind. Das Projekt trennt ausdrücklich Emulator-Backend und spielbezogene Unterstützung für Mega Man Battle Network. Es ist ein Architekturbeispiel für eine aufwendigere, gezielte Lösung, kein fertiger Pokémon-Adapter.[^16]

| Ansatz | Eigenes Spiel pro Rechner | Reichweite | Eignung für den nächsten AetherBoy-Schritt |
| --- | --- | --- | --- |
| Gen3-Protokolladapter | Ja | Geprüfte kompatible Gen3-Protokolle | **Empfohlen für Pokémon-Tausch** |
| GBA-Wireless-Adapter | Ja | Spiele mit passender Zubehörunterstützung | Sinnvolle spätere Ergänzung |
| Direkter zyklusnaher Netzwerk-Link | Ja | Potenziell breiter, stark timingabhängig | Separater LAN-/Forschungsmodus |
| Beide Geräte auf beiden PCs, Input-Sync | Nein, beide Spiele müssen lokal berechenbar sein | Potenziell breite lokale Kabelkompatibilität | Höherer Aufwand; andere Datenvoraussetzungen |
| Beide Geräte nur auf einem Host, Fernsteuerung | Nein | Was lokal beim Host funktioniert | Ändert das gewünschte Betriebsmodell |

Die Eignungsbewertung ist eine Abwägung aus vorhandenem AetherBoy-Code, Datensparsamkeit und dem konkreten Tauschziel. Sie ist keine allgemeine Rangliste der Emulatoren.

## Vorgeschlagene AetherBoy-Architektur

### Zuständigkeiten

Das bestehende Transportmodul soll keine Spielkenntnis erhalten. Es liefert begrenzte, verschlüsselt transportierte Nachrichten. Ein gemeinsamer Sitzungskoordinator übernimmt Rollen, Protokollversion, Lebenszeichen, Pause, Beendigung und Diagnose. Erst dahinter entscheidet ein emulationsspezifischer Adapter, wie die empfangenen Daten auf die lokale Maschine wirken.

Für GBA wird ein austauschbarer Anschluss zwischen `SerialController` und seiner Gegenstelle empfohlen. Der jetzige direkte Verweis auf `LocalSerialLink` wird durch einen klaren Vertrag ersetzt; der lokale Kabeladapter bleibt dessen erste Implementierung. Das ist ein begrenzter Umbau an der Schnittstelle, kein Austausch von CPU, Grafik, Audio oder Speicheremulation.

Als neue Implementierung käme ein `PokemonGen3OnlineAdapter` hinzu. Er beobachtet die erlaubten SIO-Operationen, verwaltet den ausgewählten Protokollzustand und plant lokale Empfangsereignisse. Netzwerk-Callbacks dürfen keine Register verändern. Empfangene Nachrichten werden geprüft und anschließend ausschließlich auf dem Emulations-Owner-Thread verarbeitet.

Ein späterer `GbaNetworkCableAdapter` oder `GbaWirelessAdapter` kann denselben Anschluss nutzen. Diese Namen bezeichnen **vorgeschlagene neue Komponenten**, keine bereits vorhandenen Klassen.

### Nachrichten und Sitzungsidentität

Der Entwurf sollte zwei Versionen getrennt führen: die äußere AetherBoy-Sitzungsversion und die Version des ausgewählten GBA-Protokollprofils. Der bestehende GB/GBC-v1-Handshake darf nicht einfach umgedeutet werden. Alte Gegenstellen müssen eine verständliche Inkompatibilitätsmeldung erhalten.

Empfohlene Nachrichtenklassen sind Profilangebot, Profilannahme, bestätigter Verbindungszustand, vollständige Befehlsdaten, Empfangsfortschritt, Pause/Weiter und Abbruch. Jede gehört zu einer zufälligen Sitzung und einer fortlaufenden Verbindungsphase. Ein erneuter spielinterner Handshake eröffnet eine neue Phase; verspätete Pakete aus der alten dürfen darin nicht wieder auftauchen.

Jede Nutzdatenrichtung bekommt eine monotone Sequenznummer. Es ist zu unterscheiden zwischen **im Netzwerk empfangen**, **in die Adapterwarteschlange übernommen** und **dem Spiel zugestellt**. Keine dieser Bestätigungen beweist, dass ein Pokémon-Tausch bereits dauerhaft gespeichert wurde.

Für die erste Version sind kleine feste Obergrenzen, überprüfte Teilnehmer-IDs und begrenzte Warteschlangen wichtiger als maximale Datenraten. Die vorhandene Transportgrenze von 4096 Byte ist dafür ausreichend groß; eine Erhöhung ist nicht begründet. Auf Überlast muss der Adapter mit ausdrücklich sichtbarer Bremse oder Abbruch reagieren, nicht durch unbemerkten Verlust relevanter Befehle.[^19]

### Leerlauf ist nicht gleich gefälschter Erfolg

Ein spezialisierter Adapter muss keinen realen entfernten Interrupt für jedes lokale Ereignis abwarten. Er darf jedoch nur solche lokalen Antworten erzeugen, deren Bedeutung für das ausgewählte Protokoll nachgewiesen ist. Ein echtes „keine neue Nachricht“-Signal ist etwas anderes als das Wiederholen einer alten Tauschbestätigung.

Für AetherBoy wird empfohlen, Leerlauf, Teilnehmerbereitschaft, Nutzdaten und Abschlussmeldungen als getrennte Zustandsfälle zu implementieren. Reale Befehlsdaten müssen unverändert, in der vorgesehenen Reihenfolge und höchstens einmal an das Spiel gelangen. Prüfsummen dürfen den lokalen emulierten Austausch abbilden, aber keinen verlorenen oder veränderten Nutzdatenblock kaschieren.

Unbekannte Signaturen, widersprüchliche Zustandswechsel oder ein Neustart mitten in einer kritischen Phase müssen zu einem nachvollziehbaren Abbruch führen. Ein automatischer Wechsel zwischen HLE-Profil und generischem Kabel während des laufenden Tauschs ist nicht vorgesehen. Das würde die Bedeutung bereits verarbeiteter Daten verändern.

### Kompatibilität erkennen

Ein Dateiname wie „FireRed“ reicht nicht. Empfohlen wird eine lokale Freigabeliste aus ROM-Hash, Spielkennung, Revision, Sprache und Protokollprofil. Der Hash muss nicht zwingend an den Peer übertragen werden; für den Handshake reichen die zur Kompatibilitätsentscheidung benötigten Profilinformationen.

Zwei unterschiedliche kompatible Editionen sollen unterschiedliche ROM-Hashes haben dürfen. Eine globale Forderung nach identischem ROM-Hash würde beispielsweise das geplante Rubin↔Saphir-Szenario unnötig verhindern. Umgekehrt beweist eine unveränderte vierstellige Spielkennung bei einem Hack nicht, dass dessen Linkcode oder Pokémon-Datenformate unverändert sind.

Zunächst werden ausschließlich ausdrücklich geprüfte Fassungen freigegeben. ROM-Hacks bekommen ihren eigenen Status „nicht geprüft“ beziehungsweise ein separates Profil. Für FireRed Rocket Edition ist aktuell weder ein unveränderter Linkmodus noch die Kompatibilität mit unveränderten Editionen nachgewiesen. Der Emulator kann einen im Hack entfernten oder veränderten Tauschablauf nicht durch einen Transportkanal ersetzen.

## Konkreter Abstand zum vorhandenen Code

| Vorhandene Stelle | Festgestellter Stand | Nächste Änderung |
| --- | --- | --- |
| `SerialController` | Kennt direkt `LocalSerialLink`; Register und Scheduler vorhanden | Gegenstellen-Vertrag herauslösen, lokale Semantik durch Regressionstests bewahren |
| `LocalSerialLink` | Normal-8/32 und Multiplayer im lokalen Zweierpaar | Als lokale Referenz behalten; nicht mit WAN-Sonderfällen überladen |
| `GbaProductionMachine` | Kann einzelne GBA-Zyklen ausführen und Frames ohne Rewind abschließen | Begrenzte Online-Ausführung und Adapterereignisse anbinden |
| `OnlineLinkMachine` | Umschließt fest die GB/GBC-`ProductionMachine` | Familienabhängige Erzeugung oder separate GBA-Implementierung |
| `OnlineLinkCoordinator` | Verarbeitet direkt `NetworkSerialCable` für GB/GBC | Gemeinsame Sitzung von familienspezifischer Übertragung trennen |
| `OnlineLinkProtocol` | Handshake und Kabelnachricht ausdrücklich GB/GBC v1 | Neue ausgehandelte Profile; v1 nicht still erweitern |
| `EmulationSession.CreateOnlineLink` | Weist `.gba` ausdrücklich ab | Erst nach Adapterprüfung gezielt freigeben |
| Frontends | Online-Einstieg und geschützte Kopien vorhanden | GBA-Profilstatus, L/R, 240×160, richtige Fähigkeiten und Fehlermeldungen |
| `Device.State` | Schema 6; gekoppelte Geräte lehnen Einzel-Snapshots ab | Für den empfohlenen ersten Adapter beibehalten; Rollback wäre eigenes Teilprojekt |

Diese Befunde stammen aus dem Arbeitsstand, nicht aus einer Vermutung über das Projekt. Insbesondere würde das bloße Entfernen der `.gba`-Sperre keinen funktionsfähigen GBA-Onlinemodus erzeugen.[^19]

Die aktuelle Snapshot-Funktion stabilisiert die CPU durch zusätzliche Ausführung bis zu einer Instruktionsgrenze. Das ist für unabhängige Einzelspieler-States vorgesehen. Für Rollback eines gekoppelten Paars wären gemeinsame Zeitgrenzen, beide Gerätezustände, Kabelereignisse und noch nicht bestätigte Ausgabeseitenwirkungen erforderlich. „Save States sind schon vorhanden“ ist daher kein Beleg dafür, dass generisches Online-Rollback nur noch eingeschaltet werden müsste.[^19]

Windows↔Linux benötigt bei diesem Adapter keine identischen kompletten Maschinenzustände: Die beiden Rechner führen bewusst unterschiedliche eigene Spiele aus. Notwendig sind dagegen identische Paketkodierung, Profilregeln und nachvollziehbare lokale Hardwareabläufe. Integer-Zeitstempel sind sinnvoll; eine pauschale Entfernung jedes `float` aus jeder Audioroutine wäre dafür kein zielgerichteter erster Schritt.

## Verbindung über das Internet

Die bestehende Browserbrücke ist als Transport wiederverwendbar. WebRTC-Datenkanäle bieten zuverlässige beziehungsweise teilweise zuverlässige Übertragung und sichern den Datentransport über DTLS. Für Tauschbefehle wird ein zuverlässiger, geordneter Kanal ohne absichtliches Verwerfen empfohlen. Verschlüsselung ersetzt dabei weder Paketvalidierung noch die Zuordnung zum richtigen Freund.[^17]

ICE prüft Verbindungskandidaten; STUN kann bei der Ermittlung erreichbarer Adressen helfen. TURN stellt bei Bedarf einen Relaypfad bereit. Direkte P2P-Verbindungen sind hinter beliebigen Heimroutern oder Mobilfunkanschlüssen nicht garantiert. Auch Copy-and-paste von Einladung und Antwort beseitigt diese Infrastrukturfrage nicht.[^18]

Für ein komfortables späteres Release wird ein verwalteter Relay-Fallback mit begrenzten, kurzlebigen Zugangsdaten empfohlen. Ein erster betreuter Test kann ausdrücklich konfigurierte Server verwenden. Ein Server wird dadurch nicht bereits betrieben, eingerichtet oder finanziert; dies bleibt eine eigene Produktentscheidung.

Die vorhandene Brücke lauscht nur lokal und verwendet Sitzungsgeheimnis, Host-/Origin-Prüfung sowie Größenlimits. Diese Schutzmaßnahmen sollten beim GBA-Umbau unverändert erhalten bleiben. Keine lokalen Verbindungs-URLs oder TURN-Geheimnisse in Diagnoseberichte aufnehmen. Einladungen können Netzwerkadressen enthalten und gehören nur in den vorgesehenen privaten Austausch.[^19]

## Schutz der Spielstände

Die vorhandenen Sitzungskopien sind eine gute Grundlage. Empfohlen wird zusätzlich ein kleines lokales Sitzungsjournal: verwendetes Profil, Adapterversion, Startzustand, Abbruchgrund, Zeitpunkt des letzten lokalen Flush und ausdrücklicher Status der späteren Übernahme. Normale Logs benötigen keine vollständigen Kabelnutzdaten, Trainernamen oder Pokémon-Datensätze.

Die Originaldatei bleibt während des Onlinebetriebs geschützt. Ein erfolgreiches Transport-ACK darf sie nicht ersetzen. Nach einem kontrollierten Ende sollte zunächst die Arbeitskopie neu geöffnet und das Spiel darin geprüft werden. Die Übernahme in den normalen Einzelspielerstand erfolgt ausdrücklich, mit wiederherstellbarer vorheriger Generation.

Besonders wichtig ist ein asymmetrischer Ausfall: Ein Rechner kann bereits gespeichert haben, während der andere abstürzt. Ohne weitergehendes Wiederherstellungsprotokoll lässt sich daraus kein gleichzeitig abgeschlossener Tausch auf beiden PCs garantieren. Die Oberfläche muss in einem solchen Fall „Ergebnis ungeklärt“ anzeigen können, statt aus einer einseitigen Bestätigung Erfolg abzuleiten.

Abnahmetests müssen deshalb nicht nur die Tauschanimation ansehen. Auf beiden Seiten ist das Spiel vollständig zu schließen, aus den Arbeitskopien neu zu starten und der tatsächliche Besitzstand zu prüfen. Erst dieser Schritt macht den Test für das Produktziel aussagekräftig.

## Entwicklungs- und Abnahmeplan

### 1. Lokalen GBA-Referenzfall absichern

Zuerst ein echtes kompatibles Pokémon-Paar mit Testkopien im vorhandenen lokalen Link Lab verbinden. Falls bereits dies fehlschlägt, den Ablauf aus Registerzugriffen und Ereignissen mit der lokalen Referenz abgleichen. Dabei lokale Kabelgenauigkeit, HLE-Boot, spielinterne Freischaltungen und Saveproblem getrennt diagnostizieren.

Das ist keine Voraussetzung dafür, jede Zeile des Onlineadapters erst später zu schreiben. Es ist aber ein entscheidender Kontrollversuch: Ohne ihn lässt sich ein späterer Netzwerkfehler kaum sicher vom bestehenden GBA-Kernproblem unterscheiden. Ein weißes Bild oder ein betretenes Pokémon-Center ist noch kein Linknachweis.

### 2. Austauschbaren GBA-Anschluss und Profilmaschine bauen

Den Gegenstellen-Vertrag extrahieren, den lokalen Adapter damit weiterbetreiben und einen neuen Gen3-Adapter gegen synthetische Registerfolgen entwickeln. Kein Netzwerkzugriff aus dem Kern. Handshake, Datenphase, Leerlauf, Abbruch, Wiederanmeldung und Prüfsummen bekommen voneinander unabhängige Tests.

Fertig ist diese Etappe, wenn das bestehende lokale Verhalten unverändert geprüft bleibt und der neue Adapter gültige Datenfolgen korrekt akzeptiert, ungültige aber ausdrücklich ablehnt. Noch keine Freigabe für kommerzielle Spielstände.

### 3. Über gemeinsamen Transport verbinden

Den neuen Adapter in die vorhandene Online-Sitzung einhängen, Versionsverhandlung ergänzen und zwei getrennte Prozesse verbinden. Zuerst Windows↔Windows, dann Windows↔Linux. GBA-Eingaben einschließlich L/R, Pause, Fokusverlust, Audioausgabe, Browserende und Appende sind Teil derselben Etappe.

Netzwerkwarten darf nicht als wiederholter alter Audioblock hörbar werden. Bei fehlender neuer Ausgabe braucht es einen definierten Leer-/Stummpfad, keinen unbegrenzt wiederholten Samplepuffer. Die frühere Turbo-Audioproblematik ist deshalb als Regression mitzunehmen.

### 4. Echte Gen3-Tausche und Fehlerfälle qualifizieren

Mit mindestens einer eindeutig bestimmten FireRed/LeafGreen-Paarung sowie Rubin/Saphir beginnen; Smaragd und weitere Paarungen danach ergänzen. Pro Fall Edition, Region, Revision, Bootprofil, Adapterversion und Ergebnis dokumentieren. Gleiche Sprache und bekannte Originalfassungen erleichtern den Einstieg; weitere Kombinationen werden separat freigegeben.

Synthetisch einstellbare Verzögerung prüft zunächst die Adapter- und Warteschlangenschicht. Echter Paketverlust unterhalb von WebRTC muss zusätzlich separat getestet werden, weil SCTP dann anders reagiert als ein direkt weggeworfenes Anwendungspaket. Ein erfolgreicher WSL-Test auf einem PC ersetzt keinen Test mit zwei Anschlüssen.

| Testgruppe | Vorgeschlagene Bedingungen | Erfolgskriterium |
| --- | --- | --- |
| Profilwahl | Andere Version, andere Familie, unbekannter Hack | Klare Ablehnung vor dem Tausch |
| Datenfolge | Duplikate, falsche Reihenfolge, alte Phase, unzulässige Länge | Kein falscher Spieleintrag; kontrollierter Fehler |
| Latenz | 0/20/40/60/100/200 ms modellierte RTT | Integrität immer; Bedienbarkeit und Grenzen messen |
| Schwankungen | Zeitweise Jitter, Bursts, langsamer Peer | Begrenzter Speicher; kein stiller Verlust |
| Transport | Direkter Weg und erzwungener TURN-Relay | Gleiche Protokollwirkung oder erklärter Abbruch |
| Unterbrechung | Vor Auswahl, bei Bestätigung, während Speichern | Original unverändert; Arbeitskopien nachvollziehbar |
| Plattform | Zwei physische PCs, Windows↔Linux/Wayland | Beide nativen Oberflächen benutzbar |
| Persistenz | Beenden, Neustart, Kontrolle beider Arbeitskopien | Beide tatsächlich gespeicherten Tauschresultate korrekt |

Die Zahlen sind bewusst Testpunkte, keine behaupteten unterstützten Netzgrenzen. Eine erste Freigabe sollte wiederholte vollständige Tausche mit beiden Rollen enthalten; eine einzelne erfolgreiche Sitzung reicht nicht als Stabilitätsnachweis.

### 5. Erweiterung nach Ergebnissen

Sind die Gen3-Tausche stabil, können weitere Sprachfassungen und kompatible Hacks einzeln dazukommen. Der Wireless Adapter erweitert danach eine andere Zubehörklasse. Ein generischer Netzwerk-Link und spätere Rollback-Arbeit sollten eigene Kompatibilitäts- und Leistungsziele erhalten.

Der Aufwand für das empfohlene Profil ist **mittel bis hoch**, jedoch deutlich begrenzter als generische GBA-WAN-Emulation. Die größte Unsicherheit liegt nicht im WebRTC-Anschluss, sondern in Protokollvarianten, bestehender Kerngenauigkeit und reproduzierbaren Langläufen. Vor dem ersten echten Referenztausch wäre eine feste Kalenderzusage nicht belastbar.

## Offene Risiken und Entscheidung

Die Quellen rechtfertigen eine konkrete Implementierung, aber kein Versprechen, dass eine unveränderte Portierung des gpSP-Verhaltens alle Tauschfälle erledigt. Hohe Latenz kann weiterhin spielbezogene Zeitgrenzen erreichen. Aggressive Leerlauferzeugung kann Fehler verstecken, und ein zu großzügiges Profilmatching kann bei Hacks die falsche Bedeutung von Daten akzeptieren.

Die sichere Entscheidung lautet deshalb: **AetherBoy-Gen3-Onlineprofil entwickeln, lokalen Kabelmodus unverändert erhalten, Freigaben anhand echter gespeicherter Tauschergebnisse vergeben.** Kein globales „GBA online unterstützt“ allein aufgrund erfolgreicher Paketübertragung.

Die Referenzen müssen bei einer späteren Codeübernahme korrekt behandelt werden. Die gpSP-Serialdateien tragen GPL-2.0-or-later-Hinweise; mGBA kennzeichnet seine Implementierung als MPL-2.0. Dieser Bericht übernimmt keinen fremden Implementierungscode. Eine tatsächliche Übernahme ist separat zu dokumentieren und mit den jeweiligen Lizenzbedingungen abzugleichen; Übersetzen oder Umbenennen ist keine Provenienzbereinigung.[^8][^13]

Das Ergebnis ist eine belastbar begründete Entwicklungsrichtung: **Für das konkrete Pokémon-Ziel ist ein Protokolladapter realistischer als ein universelles Kabel mit einer Internetwartephase pro Wort.** Der Kern, die Windows-/Linux-Architektur, die Verbindung und der Save-Schutz können weiterverwendet werden. GBA-Online selbst ist durch diesen Bericht noch nicht implementiert oder getestet.

## Quellen

Alle externen Quellen wurden am 12.09.2026 geprüft. Commit-Daten oben stammen aus den öffentlichen GitHub-Commitmetadaten. Bei beweglichen Projektseiten bezeichnet „aktueller Stand“ ausschließlich diesen Abrufzeitpunkt. Die verlinkten Dateien mit vollständiger Commitkennung sind eingefrorene Fundstellen.

[^1]: David Guillen Fandos / libretro. [Implement serial port multiplayer using per-game backends](https://github.com/libretro/gpsp/commit/5db42ed4c15408d76781bd2f59837c7dae52f0e8), 03.09.2025. Einführung der spielbezogenen seriellen Backends; später im geprüften gpSP-Master enthalten.

[^2]: libretro/gpSP. [libretro_core_options.h, Link Cable Connectivity](https://github.com/libretro/gpsp/blob/8d268a6bb2cd799f8f2791ebb544a7ef550cfc6f/libretro/libretro_core_options.h#L121-L134), Quellstand 25.08.2026. Explizite Optionen für RFU, Pokémon Gen3 sowie Advance Wars.

[^3]: gbadev.net / gbadoc. [Memory-Mapped Hardware Registers: Serial peripherals](https://gbadev.net/gbadoc/registers.html#serial-peripherals), und Rodrigo Alfonso / r-labs, [gba-link-connection](https://github.com/afska/gba-link-connection), laufende Dokumentationen. Registerzuordnung, Trennung von Kabel, SPI, Funk und Joybus. Die ursprüngliche GBATEK-Seite war über den Webzugriff nicht zuverlässig auslesbar; die Hardwareaussagen wurden zusätzlich gegen mGBAs SIO-Quelltext geprüft.

[^4]: David Guillen Fandos. [Emulating the GBA Wireless Adapter](https://www.davidgf.net/2024/01/13/gba-wireless-adapter/), Januar 2024. Primärbericht des Entwicklers zu RFU, Netpacket und Latenzgrenzen; seine damaligen Kabel-Zukunftsaussagen sind vom späteren Commit in Quelle 1 zu unterscheiden.

[^5]: mGBA. [GBA SIO lockstep.c](https://github.com/mgba-emu/mgba/blob/543a197582c30364584d773a974d7f991892fa43/src/gba/sio/lockstep.c), Quellstand 10.09.2026. Ereigniszeitpunkte, Teilnehmerzustände und Sleep-/Wake-Koordination.

[^6]: pret. [pokeemerald/src/link.c](https://github.com/pret/pokeemerald/blob/5eff78649e7170a877b961ef0b3da13b81a16038/src/link.c#L2090), Quellstand 01.09.2026. Rekonstruierte Routinen `LinkVSync`, `SerialCB`, `DoHandshake`, `DoRecv` und `DoSend`; keine offizielle Nintendo-API.

[^7]: pret. [pokefirered/src/link.c](https://github.com/pret/pokefirered/blob/c75f352304d529f6ba92d4f74b9cf8b5c3810788/src/link.c), Quellstand 04.08.2026. Gegenprüfung von Betriebsart, serieller Ereignisfolge und Frameüberwachung.

[^8]: David Guillen Fandos / gpSP. [serial_proto.c](https://github.com/libretro/gpsp/blob/8d268a6bb2cd799f8f2791ebb544a7ef550cfc6f/serial_proto.c#L83), Copyright-Hinweis 2025, geprüfter Projektstand 25.08.2026. Spielbezogene Zustandsmaschine, Nachrichtenbündelung, lokale Antworten, Grenzen und GPL-Hinweis.

[^9]: libretro/gpSP. [libretro/libretro.c](https://github.com/libretro/gpsp/blob/8d268a6bb2cd799f8f2791ebb544a7ef550cfc6f/libretro/libretro.c), Quellstand 25.08.2026. Abschnitt Netplay/Netpacket und Backend-Weiterleitung; schreibgeschützt über die öffentliche Raw-Datei eingesehen.

[^10]: libretro/gpSP. [gba_over.h](https://github.com/libretro/gpsp/blob/8d268a6bb2cd799f8f2791ebb544a7ef550cfc6f/gba_over.h), Quellstand 25.08.2026. Editions- und regionsbezogene Autokonfiguration, einschließlich `FLAGS_SERIAL_POKE` für Rubin/Saphir.

[^11]: mGBA. [README: Features / Planned features](https://github.com/mgba-emu/mgba/blob/543a197582c30364584d773a974d7f991892fa43/README.md), Quellstand 10.09.2026. Lokales Kabel vorhanden, Netzwerk-Link auf der Planungsliste. Diese Übersicht wurde nicht als alleiniger Funktionsnachweis verwendet.

[^12]: mGBA. [Qt MultiplayerController.cpp](https://github.com/mgba-emu/mgba/blob/543a197582c30364584d773a974d7f991892fa43/src/platform/qt/MultiplayerController.cpp), Quellstand 10.09.2026. Konkrete lokale Koordinator-/Thread-Anbindung.

[^13]: mGBA. [src/gba/sio.c](https://github.com/mgba-emu/mgba/blob/543a197582c30364584d773a974d7f991892fa43/src/gba/sio.c), Quellstand 10.09.2026. Betriebsarten, Transferzeiten und MPL-2.0-Dateihinweis.

[^14]: VisualBoyAdvance-M. [src/core/gba/gbaLink.cpp](https://github.com/visualboyadvance-m/visualboyadvance-m/blob/fd13034143c128c8b68133a7a18bc785178ec4e4/src/core/gba/gbaLink.cpp#L2643), Quellstand 12.09.2026. `StartCableSocket`, `UpdateCableSocket`, Zeitbezug und Begrenzung vorauslaufender Geräte. Kein eigener Lauf dieser Fremdsoftware im Rahmen dieses Berichts.

[^15]: Dolphin-Projekt. [Netplay Guide, Controlling GBAs on Netplay](https://dolphin-emu.org/docs/guides/netplay-guide/), laufende Anleitung; sowie JMC47, [mGBA Integration: Introducing the Integrated GBA](https://docs.dolphin-emu.org/blog/2021/07/), 21.07.2021. GBA↔GameCube-Kontext und integrierte Synchronisierung.

[^16]: Tango-Projekt. [Repository und Architekturübersicht](https://github.com/tangobattle/tango), laufender Projektstand. Rollback für Mega Man Battle Network, getrennte Game-Support-/Backend-Komponenten. Kein Pokémon-Kompatibilitätsnachweis.

[^17]: R. Jesup, S. Loreto, M. Tüxen / IETF. [RFC 8831: WebRTC Data Channels](https://www.rfc-editor.org/rfc/rfc8831.html), Januar 2021, insbesondere Abschnitte 5 und 6. SCTP/DTLS, Zuverlässigkeit und geordnete Datenkanäle.

[^18]: A. Keränen, C. Holmberg, J. Rosenberg / IETF. [RFC 8445: Interactive Connectivity Establishment](https://www.rfc-editor.org/rfc/rfc8445), Juli 2018; T. Reddy et al. / IETF, [RFC 8656: TURN](https://www.rfc-editor.org/rfc/rfc8656.html), Februar 2020. NAT-Durchquerung, Kandidaten und Relay-Fallback.

[^19]: AetherBoy, lokaler nicht committeter Arbeitsstand vom 12.09.2026. [SerialController](../third_party/GBADotnet.Core/src/Serial/SerialController.cs), [LocalSerialLink](../third_party/GBADotnet.Core/src/Serial/LocalSerialLink.cs), [Device.State](../third_party/GBADotnet.Core/src/Device.State.cs), [GbaProductionMachine](../nanoboy/Runtime/GbaProductionMachine.cs), [OnlineLinkMachine](../nanoboy/Runtime/Netplay/OnlineLinkMachine.cs), [OnlineLinkCoordinator](../nanoboy/Runtime/Netplay/OnlineLinkCoordinator.cs), [OnlineLinkProtocol](../nanoboy/Runtime/Netplay/OnlineLinkProtocol.cs), [EmulationSession](../nanoboy/Runtime/EmulationSession.cs), [OnlineSaveWorkspace](../nanoboy/Runtime/Netplay/OnlineSaveWorkspace.cs), [WebRtcBrowserTransport](../nanoboy/Runtime/Netplay/WebRtcBrowserTransport.cs). Dazu [lokale GBA-Übergabe](GBA_LOCAL_LINK_HANDOFF.md), [Online-Link-Übergabe](ONLINE_LINK_HANDOFF.md) und [DoubleCherry-Referenzprüfung](DOUBLECHERRY_NETWORK_REFERENCE.md). Lokaler Zugang; keine Behauptung, dass diese Änderungen bereits auf GitHub liegen.
