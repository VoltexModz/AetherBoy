# Link-Protokoll-Audit mit reproduzierbaren Fällen

Stand: 29. September 2026. Ergänzung zur [Quellenrecherche](LINK_CABLE_RESEARCH_2026-09-29.md).

**Historischer Prüfstand vor der Reparatur.** Die anschließend implementierten Korrekturen und ihre erneuten Tests stehen in [Link-Kabel-Korrekturen und Übergabe](LINK_CABLE_FIXES_2026-09-29.md). Die roten Ergebnisse unten dokumentieren die ursprüngliche Reproduktion, nicht den neuesten Arbeitsstand.

## Ergebnis und Arbeitsgrenze

Drei getrennte Audits haben GB/GBC, GBA/Gen 3 und Transport/Sitzungsende untersucht. Die nachfolgend genannten Reproduktionen wurden auf diesem Windows-PC ausgeführt, nicht auf dem Rechner des Linux-Entwicklers und nicht über das Internet.

**Produktionscode wurde in diesem Arbeitspaket nicht geändert.** Neu sind drei Testdateien und dieses Dokument. Vier neue Runtime-Regressionen sind bewusst rot: Sie belegen bisher nicht abgedeckte Fehler. Die vier neuen Core-Tests sind diagnostische Charakterisierungstests und bestehen gerade deshalb, weil sie sowohl den funktionierenden Kontrollfall als auch die aktuelle Einschränkung nachweisen. Daraus folgt keine Freigabe für Pokémon-Tausche.

Vorhandene uncommittete Änderungen anderer Arbeitspakete wurden nicht angepasst. Keine ROMs, Originalspielstände, produktiven Raumdienste oder TURN-Zugangsdaten wurden verwendet. Keine Commits und kein Push.

## Prüfstand

Lokales .NET SDK: `.local-tools/dotnet-10/dotnet.exe`, Version 10.0.302. Locked Restore der bestehenden Pakete; keine Versionsänderungen.

| Prüfung | Ergebnis | Was sie aussagt |
| --- | --- | --- |
| Bestehende Runtime-Suite vor Aufnahme der neuen Fälle | 392 Fälle: 388 bestanden, 4 übersprungen | Bisheriger Prüfstand grün; die neuen Übergangsfälle fehlten. |
| Native lokale Räume, Profile GB, GBA und Transportprobe | 3/3 bestanden | Lokaler Node-Raumdienst, native WebRTC-Verbindung und Paketaustausch funktionieren auf diesem PC. Ohne STUN/TURN. |
| Neue GBA-Phasenregressionen | 2/2 fehlgeschlagen | Vorzeitiger Peer-Reset bricht den lokalen Abschnitt ab. |
| Neue Transportregressionen | 2/2 fehlgeschlagen | Admission-Body-Hänger und nicht mehr lesbares finales Receipt reproduziert. |
| Neue GB/GBC-Charakterisierung | 4/4 bestanden | Rollenstagnation und unterschiedliche Auswirkungen auf den CPU-Fortschritt nachgewiesen. |
| Komplette Core-Suite einschließlich dieser vier Fälle | 238/238 bestanden | Bestehende Kernprüfungen bleiben grün. |

TRX-Berichte liegen lokal unter `artifacts/link-audit-20260929/` in `baseline-runtime`, `native-loopback`, `gba-regressions`, `transport-regressions`, `gb-protocol` und `core-full`. Diese Nachweise dürfen nicht als erfolgreicher WAN- oder Spieltest bezeichnet werden.

## 1. GBA: normaler versetzter Abschnittsabschluss wird abgebrochen

**Priorität: hoch. Kategorie: Codevergleich und synthetisch reproduziert.**

Betroffen:

- `nanoboy/Runtime/Netplay/PokemonGen3SerialAdapter.cs`, insbesondere `Receive`, `RefreshStatus`, `EnsureNoPendingPayload`.
- `nanoboy/Runtime/Netplay/GbaOnlineLinkCoordinator.cs`, Empfangsschleife in `Pump`.

Der Coordinator verarbeitet mehrere empfangene Nachrichten, bevor das lokale Spiel wieder läuft. Ein korrekt geordneter letzter Spielbefehl und der anschließende Reset können deshalb in derselben Runde ankommen.

Die rekonstruierte FireRed-Routine `LinkCB_WaitCloseLink` schließt, sobald das **lokale** Spiel die Bereitschaftsflags aller Teilnehmer verarbeitet hat. Sie beweist damit nicht, dass die Gegenseite den letzten eigenen Befehl bereits verarbeitet hat. Quelle: [FireRed link.c](https://github.com/pret/pokefirered/blob/master/src/link.c), entsprechende Routine auch in [Emerald link.c](https://github.com/pret/pokeemerald/blob/master/src/link.c).

### Minimale Reproduktion

1. Beide Seiten sind in Phase 1 etabliert.
2. Seite A verarbeitet Bs Abschlussbereitschaft und beendet ihren lokalen SIO-Abschnitt.
3. Bei B liegen As letzter `READY_CLOSE_LINK`-Befehl und danach As Reset auf Phase 2 vor.
4. B reiht den Befehl ein, verarbeitet aber den Reset vor weiteren Spielzyklen.
5. `EnsureNoPendingPayload()` bricht wegen des noch nicht zugestellten Befehls ab.

Auch ohne ausstehenden Befehl lehnt der Adapter einen früheren Peer-Reset ab, solange das lokale Spiel noch `ProtocolEstablished` ist. Ein geordnetes asynchrones Ende wird damit mit einem unzulässigen Neustart gleichgesetzt.

### Gemessene Tests

`tests/AetherBoy.RuntimeTests/GbaGen3ProtocolReviewTests.cs`:

- `RemoteCloseResetAfterFinalCommandMustWaitForLocalGameConsumption`: Fehler bei `Receive(Reset)`, bevor der letzte Befehl geliefert werden kann.
- `StaggeredCleanSectionExitMustNotCountTheSameTransitionTwice`: Fehler bei einem früheren Peer-Reset eines ansonsten leeren Abschnitts.

### Reparaturrichtung, noch nicht implementiert

Peer-Reset als ausstehenden Übergang merken. Aktuellen Abschnitt und seine Daten nicht löschen oder sofort neu interpretieren. Erst die eigene SIO-Deaktivierung des lokalen Spiels bestätigt dessen Abschnittsende. Dann die nächste Phase genau einmal eröffnen.

Erforderliche Grenzen: begrenzte Wartefrist; keine Nutzdaten einer neuen Phase in der alten liefern; weiterhin Fehler bei Replay, Phasensprung, Queueoverflow und lokal verworfenen ungelesenen Daten. Eine vollständig zusammengesetzte ausgehende Nachricht ist außerdem von einem unvollständigen Frame zu unterscheiden. Keine Abschlussbefehle erfinden.

Der bestehende Test `PeerResetCannotReinterpretAnEstablishedLocalGameAsHandshake` muss bei einer Umsetzung semantisch präzisiert werden: Nicht sofort in den Handshake zurückspringen bleibt richtig; der sofortige Sitzungsabbruch ist zu streng.

**Nicht bewiesen:** Dass zusätzliche Reset-Ereignisse beim Start allein einen permanenten Start-Deadlock verursachen. Diese anfängliche Hypothese wurde beim Gegenlesen eingeschränkt und ist kein Finding.

## 2. GB/GBC: Hardware-Taktwahl und Spiel-Rollenwahl sind nicht dasselbe

**Priorität: hoch für Gen-1-Verbindungsaufbau. Kategorie: eigener kleiner Rollen-Testtreiber, kein Retail-ROM-Test.**

Betroffen: `nanoboy/Core/NetworkSerialCable.cs`, `TryPair` und `WaitingForPeer`; `nanoboy/Runtime/Netplay/OnlineLinkCoordinator.cs`, Transferfrist.

Pokémon Rot/Blau verwendet `02` für die externe und `01` für die interne Verbindungsprobe. Empfangenes `01` lässt das eigene Spiel die externe Rolle wählen; empfangenes `02` die interne. Quellen: [Cable-Club-Aufbau](https://github.com/pret/pokered/blob/master/engine/link/cable_club_npc.asm), [Serial-Konstanten](https://github.com/pret/pokered/blob/master/constants/serial_constants.asm), [Serial-IRQ](https://github.com/pret/pokered/blob/master/home/serial.asm).

Sind beide kurzen externen Angebote schon durch interne Angebote ersetzt worden, bevor die Nachrichten eintreffen, koppelt unser Code `01` mit `01`. Die Host-Arbitrierung wählt zwar einen physischen Taktgeber, verändert aber folgerichtig nicht die übertragenen Bytes. Beide Spiele wählen danach die externe Rolle. Das erste Byte wurde übertragen, danach entstehen zunächst keine weiteren Kabeltakte.

### Wichtige Unterscheidung

- **Extern/extern:** Die CPU darf weiterlaufen. Das Spiel kann seinen Inaktivitätszähler abarbeiten, schließen und eine erneute Wahl versuchen. Kein nachgewiesener permanenter CPU-Deadlock.
- **Interne Probe ohne passendes Gegenangebot:** Unser Runtime-Vertrag hält die gesamte Maschine an. DIV, Spieltimer und softwareseitige Retry-Schleifen laufen dann nicht weiter. Ein späterer externer Listener kann das lösen; andernfalls greift die standardmäßige Zwei-Minuten-Frist der Runtime.

### Gemessene Tests

`tests/AetherBoy.CoreTests/NetworkSerialProtocolReviewTests.cs`:

1. Gestaffelte externe/interne Probe: komplementäre Rollen und folgendes Datenbyte funktionieren.
2. Spiegelgleiche Probe: zwei externe Rollen und ausbleibender weiterer Serial-Fortschritt; explizites Software-Rearm kann eine neue Wahl ermöglichen.
3. Extern/extern: Owner und DIV laufen tatsächlich weiter.
4. Ungepaarte interne Probe: Owner und DIV stehen; ein späterer externer Listener löst die Barriere.

Alle vier Tests bestanden. Die Fälle 2 und 4 dokumentieren **aktuelle Einschränkungen**, keine erfolgreich reparierte Verbindung.

### Konsequenz

Kein heimliches Umschreiben von `01` auf `02`. Für eine generische Reparatur müssen emulierte Zeit, konkurrierende Takte und das Probe-/Retry-Verhalten gemeinsam modelliert werden. Ein enges Pokémon-Profil wäre eine separate, ausdrücklich zu kennzeichnende Alternative.

Coffee GB besitzt eine Behandlung spiegelgleicher Rollenwahl im Scheduler seiner gemeinsam betriebenen Maschinen. Das ist eine relevante Vergleichsstelle, aber nicht unmittelbar auf zwei unabhängig laufende WAN-Maschinen übertragbar: [LinkedFrameStepper.kt](https://github.com/trekawek/coffee-gb/blob/HEAD/controller/src/main/java/eu/rekawek/coffeegb/controller/link/LinkedFrameStepper.kt).

Zusätzlich kostet der aktuelle GB-Prototyp je Byte beidseitig `Offer`, `Ready`, `Complete` – sechs serielle Nachrichten. Die bisherigen Runtime-Tests übertragen kurze synthetische Folgen. Lange Tauschdaten und echtes Rollenwahlverhalten brauchen zusätzliche Tests; zuverlässige SCTP-Zustellung allein löst diese Fragen nicht.

## 3. Raumaufbau: Antwortheader erhalten, Body hängt ohne Zeitlimit

**Priorität: hoch. Kategorie: echte lokale HTTP-Reproduktion.**

Betroffen: `nanoboy/Runtime/Netplay/OnlineRoomTransport.cs`, `RunAsync` und `Request<T>`.

`HttpClient.Timeout` steht auf 15 Sekunden, `SendAsync` benutzt aber `ResponseHeadersRead`. Nach den Headern wird der Body mit dem äußeren Token gelesen. Die erste Raumaufnahme erfolgt außerdem noch vor Einrichtung des späteren zehnminütigen Setup-Deadlines.

Ein Server, der gültige Header und nur einen Teil des JSON-Bodys sendet, lässt den Raumeintritt deshalb weiter warten. Das [dokumentierte Verhalten von HttpCompletionOption](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcompletionoption?view=net-10.0) erklärt diese Grenze des HttpClient-Timeouts.

**Test:** `AdmissionResponseBodyMustRespectTheRequestTimeoutAfterHeadersArrive` in `OnlineTransportProtocolReviewTests.cs`. Temporärer Loopback-Server liefert `201 Created`, Content-Length und nur `{`. Nach 18 Sekunden läuft die Admission weiterhin: reproduzierbar rot. Der Test beendet Server und Client anschließend kontrolliert.

**Minimalfix:** eigenen verknüpften Request-Token mit 15-Sekunden-Budget über Header- und sämtliche Body-Lesevorgänge verwenden. Timeout von Benutzerabbruch unterscheiden. Nicht die Antwortgrößenbegrenzung entfernen.

Dieser Test zeigt einen möglichen echten Hänger. Er beweist nicht, dass euer produktiver Raumserver genau diese unvollständige Antwort geliefert hat.

## 4. Verbindungsende: bereits empfangene Abschlussnachricht nicht mehr lesbar

**Priorität: hoch für sauberen Abschluss. Kategorie: Browsertransport reproduziert, nativer Transport zusätzlich im Code belegt.**

Betroffen:

- `WebRtcBrowserTransport.TryReceive`
- `OnlineRoomTransport.TryReceive`
- `GbaOnlineLinkCoordinator.Pump`

Der Coordinator möchte finale Close-Receipts nach einem Transportende noch aus der Warteschlange lesen. Beide echten Transporte sperren nach Stop/Cancel jedoch jeden Queue-Zugriff. War der Owner kurz ausgelastet, kann ein bereits empfangenes Receipt dadurch unsichtbar werden.

**Test:** `BrowserTransportMustDrainAlreadyAcceptedReceiptAfterOrderlyPeerClosure`. Ein echter lokaler WebSocket sendet geordnet `READY`, ein gültiges GBA-CloseReceipt und `CLOSED`. Erst danach liest der Owner. `Fault` ist null, aber `TryReceive` liefert false: reproduzierbar rot.

Die vorhandenen synthetischen GBA-Testtransporte erlauben das spätere Lesen. Deshalb hatten ihre erfolgreichen Abschlussprüfungen diese Abweichung zu den echten Transporten verdeckt.

**Minimalfix:** Bei geordnetem Abschluss ohne Fault bereits akzeptierte Nachrichten lesbar lassen. Bei Fehlern weiterhin sperren. Der bestehende Queueoverflow-Test verlangt ausdrücklich, nach einem Überlauf keine gepufferten Daten mehr auszugeben; diese Sicherheitseigenschaft muss bestehen bleiben.

Dies ist kein Beleg für einen anfänglichen ICE-Verbindungsfehler.

## 5. Windows: Protokollursache verschwindet aus der sichtbaren Abschlussdiagnose

**Priorität: mittel, aber wichtig für den nächsten WAN-Test. Kategorie: Quellcodebefund.**

`nanoboy/frmNano.OnlineLink.cs`, `FinishStoppedOnlineLink`, berücksichtigt in der sichtbaren Fehlerbeschreibung nur den Raumtransport-Fault beziehungsweise BrowserFailure. `current.Fault` und `online.Failure` können dagegen bereits den konkreten Emulations-/Adapterfehler enthalten.

Bei gesundem Transport und fehlerhaftem Spieleprotokoll erscheint deshalb eine unspezifische Verbindungsstörung. Die Ausnahme wird teilweise noch in Crash-/Entwicklungslogs geschrieben; sie ist nicht vollständig verloren, aber die Benutzerführung zeigt zur falschen Schicht.

Linux behandelt einen Session-Fault direkt über `ReportError(currentSession.Fault)` im `WaylandEmulatorHost`.

**Minimalfix:** Transport-, Session-/Protokollfehler und normalen Abschluss getrennt klassifizieren; bereinigte konkrete Ursache anzeigen. Vor sichtbaren Textänderungen gilt `docs/UI_COPY_GUIDE.md`. Keine ungeprüften Raw-Exceptions mit möglicherweise enthaltenen Zugangsdaten in Exportberichte übernehmen.

## Was bereits belastbar vorhanden ist

- Native lokale Raumverbindungen mit GB-, GBA- und Probe-Profil bestehen die Tests.
- Native Logger, ICE-Status und ausgewähltes Kandidatenpaar sind angebunden.
- Netzwerkcallbacks schreiben keine emulierten Register; Zustand gehört dem Emulationsthread.
- Gen-3-Grundstruktur aus Prüfsumme und acht Datenwörtern entspricht den geprüften Referenzen.
- Die Gen-3-Zeitkonstante 18.363 Zyklen entspricht 5.755 Transferzyklen plus dem Spieltimer mit `197 × 64` Zyklen.
- Sequenzen, Nonces, Queuegrenzen, Originalspielstandschutz und explizite Sitzungskopien sind vorhanden und dürfen nicht für einen schnellen vermeintlichen Erfolg entfernt werden.

## Noch nicht nachgewiesen

- Windows↔Linux über euren echten TURN-/Raumdienst.
- Erfolgreicher Tausch mit anschließendem Speichern, Neustart und richtigem Pokémon-Bestand auf beiden Geräten.
- Universelle GB/GBC-Rollenwahl unter WAN-Verzögerung.
- Allgemeine GBA-Link-Kompatibilität oder ROM-Hack-Kompatibilität.

Der native Transport unterstützt im betrachteten Stand ausdrücklich TURN/UDP und lehnt TURN/TCP/TLS ab. In einem Netz, das den benötigten UDP-Verkehr blockiert, ist deshalb ein anderer unterstützter Pfad nötig. Aus einem beliebigen Timeout lässt sich eine solche Sperre aber nicht ableiten.

## Empfohlene Reihenfolge der Korrekturen

1. Request-Body-Timeout und geordnetes Queue-Drain korrigieren; die zwei Transportregressionen grün machen und Sicherheitsgegenfälle weiter prüfen.
2. Gen-3-Abschnittswechsel als expliziten, begrenzten Übergang modellieren; letzte echte Spielbefehle vor dem lokalen Abschluss liefern.
3. Einen eigenen längeren Gen-3-Testtreiber ergänzen: Initialisierung, VBlank-/Timer3-Takt, mehrere Befehlsblöcke, sauberer Abschluss, erneuter Eintritt. Der vorhandene synthetische Einzelblocktest reicht dafür nicht.
4. Windows-Diagnose so ändern, dass ein Kabelprotokollfehler nicht als unbekannte Serverstörung erscheint.
5. Für GB/GBC erst anhand der neuen Rollenfälle die Synchronisationsstrategie festlegen; keine Spielbytes fälschen.
6. Danach den ROM-freien Windows↔Linux-Transporttest und erst anschließend die getrennten Gen-1-, Gen-2- und Gen-3-Spieltests durchführen.

Ein Architektur-Neustart ist für die belegten Transport- und GBA-Abschlussfehler nicht erforderlich. Für generischeres GB/GBC-WAN-Verhalten ist dagegen mehr als eine Timeout-Verlängerung nötig.
