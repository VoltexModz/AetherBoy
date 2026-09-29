# gpSP als Gen3-Referenz: Quellcodevergleich

Stand: 29. September 2026. Nur öffentliche Referenzquellen und eigene synthetische Tests; keine ROM, kein Originalspielstand und kein öffentlicher Server wurden ausgeführt oder verändert. Dieser Vergleich ist kein Nachweis eines Pokémon-Tauschs.

Nachtrag: Der hier beschriebene damalige Startup-Fehler wurde im anschließenden [Implementierungsauftrag korrigiert](LINK_GBA_STARTUP_FIX_2026-09-29.md). Die folgenden Vorher-Ergebnisse bleiben als Quellen- und Reproduktionsnachweis erhalten.

## Fixierter Referenzstand

GitHub `libretro/gpsp`, Commit `5819380c2ffb0900219d700a382ee68c464ebb99` vom 19. September 2026, am 29. September über die öffentliche Commit-API geprüft.

Gelesene Hauptdateien:

- [serial_proto.c](https://github.com/libretro/gpsp/blob/5819380c2ffb0900219d700a382ee68c464ebb99/serial_proto.c): vollständiger Pokémon-Pfad, einschließlich Senden, Gasttakt und Netpacket-Empfang.
- [serial.c](https://github.com/libretro/gpsp/blob/5819380c2ffb0900219d700a382ee68c464ebb99/serial.c): Registeranbindung, Rollenbits und Interruptplanung.
- [serial.h](https://github.com/libretro/gpsp/blob/5819380c2ffb0900219d700a382ee68c464ebb99/serial.h): getrennte Modi für Pokémon-Kabel, Wireless Adapter, Advance Wars und Game Boy Player.
- [libretro/libretro.c](https://github.com/libretro/gpsp/blob/5819380c2ffb0900219d700a382ee68c464ebb99/libretro/libretro.c#L478): Netpacket-Callbacks, Teilnehmer-IDs und zuverlässiger Versand mit Flush-Hinweis.
- [gba_memory.c](https://github.com/libretro/gpsp/blob/5819380c2ffb0900219d700a382ee68c464ebb99/gba_memory.c#L3200), `gba_over.h` und `libretro_core_options.h`: tatsächliche Geräteauswahl statt bloßer Menü-/README-Aussagen.
- `main.c`: serielle Ereignisse im CPU-/Timer-Aktualisierungspfad; `rfu.c` gezielt zur Abgrenzung des separaten Wireless-Backends.

Die heruntergeladenen Dateien liegen separat unter `artifacts/link-reference-deep-20260929/gpsp/`. `serial_proto.c` ist bytegleich mit dem früheren Analysecache, SHA-256 `2758E1496A923746FE70FD007928C028C50853A2DEBCF3909B062B77862CEB71`.

Die tatsächlichen Dateiköpfe von `serial_proto.c`, `serial.c` und `serial.h` nennen **GPL Version 2 oder später**. `COPYING` enthält den GPL-v2-Text. Hier wurde kein gpSP-Produktionscode übernommen; die Analyse ist keine Freigabe zum Entfernen von Lizenz-/Herkunftshinweisen bei späterer Übernahme.

## Was gpSP wirklich implementiert

Der Pokémon-Modus ist ausdrücklich eine spielprotokollbezogene Nachbildung, kein beliebiges GBA-Kabel über IP. Er betreibt auf jedem Teilnehmer einen lokalen Gegenstellenersatz. Die echte Spielsoftware sieht regelmäßig serielle Ereignisse; über das Netzwerk gehen vollständige Befehle und Handshakezustände.

| Schicht | gpSP | AetherBoy, untersuchter Stand |
| --- | --- | --- |
| Netzdaten | `MPK1`, 24 Byte: Kennung, Zustand/Flag und acht 16-Bit-Wörter | Eigenes versioniertes Gen3-Profil mit Phasen, Sequenzen, Nonces und acht Wörtern |
| Übergang | PREINIT → HANDSHAKE → CONNECTED; `B9A0`, `8FFF` | Erkannte Handshaketokens plus lokaler Readiness-Zähler |
| Datenrunde | Ein lokales Prüfsummenwort plus acht Befehlswörter | Dasselbe Grundformat, über echte SIO-Abschlüsse im Core |
| Kein Gegenbefehl vorhanden | Nullwörter; leere eigene Runde wird nur als Status gemeldet | Nullwörter; leere eigene Runde erzeugt keinen Command |
| Prüfsumme | Summe der in dieser lokalen Runde sichtbaren Teilnehmerwörter | Lokale Summe beider Richtungen; zusätzliche Prüfung der lokal gesendeten Prüfsumme |
| Gasttakt | Freilaufend mit ungefährem Zyklusabstand, nicht VBlank-gekoppelt | Absolute eigene Emulationszeit; neun Abschlüsse pro 280.896-Zyklen-Runde mit Wortabständen und Pause |
| Reentry | Über 18 aufeinanderfolgende `B9A0`-Wörter und lokale Moduswechsel | Explizite Phasen; lokale SIO-Deaktivierung; ausstehender Peer-Abschluss |
| Überlauf | 128 Befehle pro Peer; zusätzlicher Befehl wird verworfen | 64 Befehle; sichtbarer Abbruch statt stiller Verlust |
| Teilnehmer | Bis zu vier lokale Multiplayer-IDs | Bewusst genau zwei |

Nullrunden und lokal berechnete Prüfsummen sind hier keine zufälligen Ersatzbytes für verlorene Spielbefehle. Die Spiele selbst besitzen leere Runden und nichtleere Befehlsqueues. Echte Befehle dürfen dadurch trotzdem nicht erfunden, verändert oder verworfen werden. Das zeigt der lokale Spieleablauf in `pret/pokefirered/src/link.c`, insbesondere `EnqueueSendCmd`, `DoRecv`, `DoSend` und `SendRecvDone`.

## Wichtige Einschränkung für Vergleichstests: Auto ist nicht immer Kabel

In `gba_memory.c:3235–3243` wählt gpSP bei erkannten Originalfassungen von **Rubin/Saphir** automatisch den Pokémon-Kabelmodus. Für **Feuerrot/Blattgrün/Smaragd** wählt Auto dagegen den **Wireless Adapter (RFU)**. Das RFU-Backend besitzt eigene Kommandos, Discovery, Zustände und Netzwerkpakete.

Ein erfolgreicher FireRed-Netplay-Bericht mit Auto-Einstellung beweist deshalb nicht, dass `serialpoke_*` benutzt wurde. Für einen Vergleich mit unserem Kabelprofil muss auf beiden gpSP-Seiten ausdrücklich **`mul_poke` / Link Cable - Pokemon Gen3 mode** gewählt werden. Umgekehrt darf ein RFU-Menüablauf im Spiel nicht als Test unseres Kabelprofils gelten.

gpSP enthält außerdem heuristische ROM-Hack-Erkennung anhand Header, bekannter Spiele und ROM-Metadaten und setzt erkannte Pokémon-Hacks in Auto auf den Kabelmodus. Daraus folgt keine getestete Rocket-Edition-Kompatibilität. AetherBoys Prüfung exakter Originalfassungen sollte dadurch nicht aufgeweicht werden.

## Neuer konkreter Startup-Gegenfall

Die bereits reparierte Behandlung eines **etablierten** Abschnittsendes wird hier nicht erneut als offener Fehler gezählt. Der neue Gegenfall liegt **vor** der Etablierung:

1. Das lokale Spiel hat zwei serielle Antworten mit zwei erkannten Teilnehmern gesehen. Sein vorheriger Teilnehmerzähler ist 2; der Adapter hat ebenfalls zwei Handshakerunden gezählt.
2. Der andere Rechner beendet einen eigenen Initialisierungsschritt und schickt `Reset(nextPhase)`.
3. Weil der lokale Adapter noch nicht etabliert ist, ruft er `ResetPhase` auf. Dabei setzt er `handshakeRounds` auf 0. Das lokale Spiel deaktiviert sein SIO dabei jedoch nicht und behält seinen vorherigen Teilnehmerzähler.
4. Es trifft ein frisches `B9A0` des Partners für die neue Phase ein. Das lokale Spiel sendet jetzt `8FFF`.
5. Die lokale Spielregel sieht dieselben zwei Teilnehmer wie vorher und das Mastertoken: Das Spiel wechselt in die Datenphase. Der Adapter zählt erst Runde 1 seit seinem internen Reset und bleibt im Handshake.
6. Damit können nachfolgende Prüfsummen-/Datenwörter im falschen Adapterzustand landen.

Quellenanker:

- `PokemonGen3SerialAdapter.Receive`: früher Peer-Reset → `ResetPhase` bei noch nicht etabliertem Protokoll.
- `PokemonGen3SerialAdapter.ResetPhase`: `handshakeRounds = slot = 0`.
- `PokemonGen3SerialAdapter.CompleteMultiplayer`: Mastertoken nur mit `handshakeRounds >= 2` akzeptiert.
- [FireRed `DoHandshake`/`SerialCB`](https://github.com/pret/pokefirered/blob/c75f352304d529f6ba92d4f74b9cf8b5c3810788/src/link.c#L1996): Entscheidung anhand der vom **lokalen Spiel** vorher beobachteten Teilnehmerzahl; ein Netzwerk-Metadatenreset existiert dort nicht.

Der Hauptagent hat den eigenen neuen Test zunächst mit dem gewünschten Invariant ausgeführt: **ohne Reset bestanden, mit Reset an `ProtocolEstablished == true` fehlgeschlagen**. Bericht: `artifacts/link-reference-deep-20260929/startup-red/AetherBoy.RuntimeTests_net10.0_x64.trx`. Der Gegenfall ist damit nicht nur aus Quellen vermutet, sondern gegen den aktuellen Adapter reproduziert.

Damit der reine Quellenanalyseauftrag keinen absichtlich roten Standardtestlauf hinterlässt, enthält `GbaGen3StartupReferenceTests.cs` anschließend zwei klar getrennte Fälle:

- `StableLocalHandshakeFramesTheFirstEightWordCommand`: positive Kontrolle einschließlich acht unveränderter Datenwörter.
- `PeerOnlyStartupResetCanSeparateGameAndAdapterHandshake`, Kategorie `ProtocolReviewCurrentLimitation`: prüft ausdrücklich den **bestehenden Fehler** – lokales Spiel etabliert, Adapter nicht etabliert, kein Command aus der ersten Datenrunde.

Ein grüner Charakterisierungstest repariert den Fehler nicht. Beim späteren Produktionsfix müssen seine Erwartungen auf beidseitig etablierter Zustand und genau einen unveränderten Command umgestellt werden. Beide Fälle modellieren nur die dokumentierte lokale Handshakebedingung mit eigenem Code und echten seriellen Controller-Abschlüssen. Sie führen kein vollständiges Pokémon-Spiel aus.

Der Hauptagent hat den finalen Stand zusammen mit `GbaGen3ProtocolReviewTests` und `PokemonGen3SerialAdapterTests` ausgeführt: 49 bestanden, keine Fehler oder übersprungenen Tests. Bericht: `artifacts/link-reference-deep-20260929/startup-characterization/AetherBoy.RuntimeTests_net10.0_x64.trx`. Der bekannte Startup-Fehler bleibt ausdrücklich offen.

Reparaturrichtung: Lokale, dem Spiel bereits sichtbare Handshake-Evidenz von Remote-Phasenmetadaten trennen. Weder pauschal die Zwei-Runden-Regel entfernen noch jedes `8FFF` ohne vorherige lokale Readiness akzeptieren. Eine lokale SIO-Deaktivierung oder eine tatsächlich ausgelieferte nicht passende Terminalantwort muss diese lokale Evidenz weiterhin ungültig machen.

Der Celio-Referenzagent hat die Ereignisfolge unabhängig gegengeprüft. Celios direkte `8FFF`-Übergänge liefern einen hilfreichen Vergleich, aber keinen Grund, AetherBoys Sicherheitsprüfungen pauschal abzuschalten.

## Timing: Referenzen nicht mit exakten Hardwarewerten verwechseln

gpSP setzt für verbundene Gäste `SLAVE_IRQ_CYCLES_C = 28672`; bei einer GBA-Runde von 280.896 Zyklen wären das idealisiert etwa 9,80 Ereignisse, nicht exakt neun. Zusätzlich verwirft der Zähler nach Auslösung den Rest (`frcnt = 0`) und hängt von der Updategranularität ab. Sein Kommentar nennt selbst Näherungswerte. Dieses Verhalten darf nicht als exakter Hardwaretakt portiert werden.

AetherBoys 18.363 Zyklen zwischen dicht folgenden Wörtern setzen sich aus 5.755 Transferzyklen und `197 × 64 = 12.608` Timerzyklen zusammen. Der Timeranteil steht tatsächlich in FireReds `InitTimer`/`StopTimer`. Die Gastplanung enthält eine längere Pause nach dem achten Wort; beim Host bleibt der Timer des echten Programms maßgeblich.

Der vollständige Spieleablauf prüft mehr als einen passenden Durchschnitt:

- Der Master prüft im VBlank, ob mindestens neun Serial-Callbacks abgeschlossen wurden.
- Der Gast erkennt nach mehr als zehn VBlanks ohne Serial-Callback einen Ausfall.
- Der Serial-Callback stellt den nächsten SEND-Wert bereit.
- Timer 3 löst die nachfolgenden Hosttransfers aus.

Unser vorhandenes ARM-Testprogramm pollt das serielle IF-Bit und startet Hosttransfers unmittelbar. Die neueren Mehrphasen-Tests treiben Wörter von C# aus. Beide sind nützlich, bilden aber diese vollständige VBlank-/Timer-/IRQ-Kette nicht ab.

## Konkrete nächste Prüfungen

1. Den neuen Startup-Gegenfall ausführen und lokal beobachtete Readiness sauber von der Netzwerkphase trennen.
2. Negative Nachbarn sichern: keine Etablierung beim allerersten `8FFF` ohne vorangegangene Teilnehmerbeobachtung; Readiness nach lokaler SIO-Deaktivierung vergessen; ein tatsächlich geliefertes Nullterminal setzt die Stabilität zurück.
3. Ein eigenes ARM-Testprogramm mit echtem VBlank, Timer 3, Serial-IRQ-Routine und IE/IME erstellen. Zwei unabhängige Emulator-Owner mit verspätetem Partnerbeitritt, verschiedener VBlank-Phase, 0–120 ms simuliertem Netzdelay und 0–mehreren VBlank-Ausfällen antreiben.
4. Mehrere nichtleere Runden mit beidseitigen Prüfsummen, Nullrunden dazwischen, 63/64/65 gepufferten Befehlen und klarer Überlaufreaktion prüfen. Nichts still verwerfen.
5. Erst danach echte Spielepaare mit lokalem Hardwarekabelpfad, unserem Gen3-Adapter und gpSP-**Kabelmodus** vergleichen: initialer Eintritt, mehrere Tausche, Schließen, Wiedereintritt und Neustart beider Batteriespielstände.

## Was wir bewusst nicht übernehmen sollten

- gpSP löscht beim Master-Handshake ausstehende Gegenbefehle und verwirft bei Queueüberlauf neue Pakete. Für geschützte Tauschsitzungen ist ein erklärter Abbruch besser als ein verdeckter Datenverlust.
- Die Heuristik „mehr als 18 Mal `B9A0` bedeutet Rehandshake“ kann eine Protokollphase erkennen, ist aber keine allgemeine serielle Hardwareeigenschaft. Echte Commanddaten dürfen nicht bloß nach ihrem Zahlenwert als Reset behandelt werden.
- Vier Teilnehmer verlangen Anpassungen an Empfangsregister, Prüfsummen, Paket-Routing, Rücktritt und Tests; es genügt nicht, die Zwei-Spieler-Grenze in einer Oberfläche zu entfernen.
- Netpacket-Zuverlässigkeit ersetzt keine Anwendungssicherheit. gpSPs Dateiausschnitt verlässt sich auf den Libretro-Transport; AetherBoys Nonces, Reihenfolgeprüfung, Profilprüfung und Spielstandkopien bleiben sinnvoll.

Der direkte Nutzen der Referenz ist damit überprüfbar: Sie bestätigt die bereits gewählte Befehlsbündelung, hilft falsche Timing- und RFU-Annahmen zu vermeiden und hat einen konkreten neuen Startup-Test ergeben. Sie rechtfertigt weder einen Netztransport-Neustart noch eine pauschale Kompatibilitätsfreigabe.
