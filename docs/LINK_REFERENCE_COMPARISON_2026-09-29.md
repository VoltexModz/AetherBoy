# Link-Kabel: vertiefter Quellenvergleich und nächste Umsetzung

Stand: 29. September 2026. Ziel: Pokémon zwischen zwei unabhängigen AetherBoy-Instanzen auf Windows und Linux tauschen, mit Raumcode und geschützten Spielstandkopien.

Nachtrag: Der unten historisch dokumentierte GBA-Startup-Gegenfall ist inzwischen [gezielt korrigiert und erweitert getestet](LINK_GBA_STARTUP_FIX_2026-09-29.md). Die [Abnahmematrix](LINK_PLATFORM_VALIDATION_2026-09-29.md) umfasst ausdrücklich Windows ↔ Windows, Linux ↔ Linux und beide gemischten Rollen. Die folgenden Analyseergebnisse bleiben als Vorher-Befund erhalten.

## Ergebnis

Der Vergleich liefert keinen Grund, den vorhandenen Netzwerktransport erneut auszutauschen. Er liefert konkrete Arbeit an den **Spielprotokollen über diesem Transport**:

1. **GBA zuerst:** Ein gezielt konstruierter Startablauf reproduziert jetzt einen Widerspruch zwischen dem lokalen Spielmodell und unserem Adapter. Ein Reset der Gegenstelle löscht die lokale Handshake-Historie im Adapter, aber nicht im Spiel. Das muss vor weiteren Kompatibilitätsbehauptungen behoben werden.
2. **GB/GBC getrennt:** DoubleCherryGB enthält keinen kausalen Ersatz für unsere fehleranfällige gleichzeitige Pokémon-Rollenwahl. Eine früh angesetzte, explizit spielbezogene Wahlbarriere ist ein begrenzter eigener Versuch; semantische Gen1-/Gen2-Pufferung ist eine weitergehende Alternative.
3. **Nicht fremde Kompromisse übernehmen:** Künstliche Abschlusskommandos, unzugeordnete Antwortbytes oder stiller Queueverlust dürfen unsere bisherigen Schutzmechanismen nicht ersetzen.

Ein Quellenvergleich ist kein erfolgreicher Tausch. Auch der neue Modelltest beweist nicht, dass genau dieser Ablauf die früheren WAN-Probleme verursacht hat.

## Was tatsächlich geprüft wurde

Drei Agenten haben die folgenden Protokollpfade unabhängig untersucht und ihre Ergebnisse miteinander abgeglichen. Die Hauptanalyse hat den neuen GBA-Test, die maßgeblichen FireRed-Funktionen und die Vergleichsberichte gegengelesen; zusätzlich wurde die ursprüngliche Gen1-/Gen2-Proxy-Implementierung untersucht.

| Referenz | Gelesener Schwerpunkt | Nutzen für AetherBoy |
| --- | --- | --- |
| [DoubleCherryGB, Detailbericht](REFERENCE_DOUBLECHERRY_2026-09-29.md) | SC-Schreibzugriffe, Serial-Abschluss, Default-Netpacket, TCG-Sonderprofil | Ankunftsabhängige Rollenwahl verstehen; keine generische Lösung daraus behaupten |
| [gpSP, Detailbericht](REFERENCE_GPSP_2026-09-29.md) | Gen3-Kabeladapter, Modusauswahl, Handshake, Prüfsumme, acht Kommandowörter | Direkte Vergleichsimplementierung für unseren spezialisierten Gen3-Adapter |
| [Celio-Link und GBLink, Detailbericht](REFERENCE_CELIO_2026-09-29.md) | Firmware, mGBA-Spezialanbindung, Server, Client, MPK1-Brücke | Unabhängige lokale Rundentaktung, Start-/Endzustände und gepufferte Spielkommandos |
| Lorenzooone/PokemonGB_Online_Trades_and_Battles | RBY-/GSC-Datensektionen, vorbereitender Austausch, Auswahl/Zustimmung/Ergebnis, BGB-Anbindung | Konkrete Alternative für semantische Gen1-/Gen2-Pufferung |
| pret/pokefirered und pokered | Tatsächliche Spielregeln hinter Handshake und Rollenwahl | Spielzustand und Adapterzustand voneinander unterscheiden |

Die großen Referenzen sind in den Detailberichten auf konkrete Commit-SHAs festgelegt. Die separate [breite Recherche](LINK_CABLE_RESEARCH_2026-09-29.md) deckt außerdem mGBA, SameBoy, BizHawk, Coffee GB, VBA-M, weitere Kerne und die vorgeschlagenen Hardwareprojekte ab. Nicht jede Datei jedes Projekts wurde gelesen; der vertiefte Vergleich betrifft die beschriebenen Link-, Protokoll- und Lebenszykluspfade.

Es wurde kein fremder Implementierungscode in AetherBoys Produktionscode übernommen. Der neue Test ist ein eigenes C#-Zustandsmodell mit selbst erzeugtem ARM-Testprogramm, kein kopierter Pokémon-Code. Lizenztexte und abweichende Dateiheader sind in den Detailberichten festgehalten.

## Neuer reproduzierter GBA-Befund

Betroffen: `PokemonGen3SerialAdapter.Receive`, `ResetPhase` und `CompleteMultiplayer`.

| Ereignis | Lokales Spielmodell | Unser Adapter |
| --- | --- | --- |
| Zwei echte lokale Handshake-Abschlüsse zeigen zwei Teilnehmer | Merkt sich Teilnehmerzahl 2 | Merkt sich mindestens zwei gültige Runden |
| Gegenstelle meldet nächste Startphase und frischen Handshake; lokales SIO bleibt aktiviert | Eigene Handshake-Historie bleibt erhalten | `ResetPhase` setzt `handshakeRounds` auf 0 |
| Lokaler Master sendet einmalig `8FFF`, beide Teilnehmer bleiben sichtbar | Akzeptiert Mastertoken bei stabiler Teilnehmerzahl und wechselt zur Datenphase | Sieht erst Runde 1 und bleibt im Handshake |
| Lokale Software sendet Prüfsummenplatz und acht Wörter | Erwartet Datenübertragung | Erkennt keinen vollständigen Befehl |

Die Spielregel wurde gegen `DoHandshake`, `SerialCB`, `EnableSerial` und `LinkMain1` in der gelesenen FireRed-Referenz geprüft. Celios PacketLayer und Lua-Modell wechseln beim tatsächlich beobachteten Mastertoken bereits zur Prüfsummenphase; gpSP behandelt dieses Token ebenfalls anders als unser zusätzlicher Rundenzähler. Das sind Vergleichspunkte, kein Auftrag, alle fremden Bedingungen pauschal zu kopieren.

### Ausgeführter Nachweis

Datei: [GbaGen3StartupReferenceTests.cs](../tests/AetherBoy.RuntimeTests/GbaGen3StartupReferenceTests.cs).

Der erste Lauf verlangte für beide Varianten die gewünschte Übereinstimmung zwischen Spiel und Adapter:

- Ohne Peer-Reset: **bestanden**, einschließlich korrekt gerahmtem Acht-Wort-Befehl.
- Mit Peer-Reset: **fehlgeschlagen**, exakt bei `Adapter.ProtocolEstablished`: erwartet `true`, tatsächlich `false`.
- TRX: `artifacts/link-reference-deep-20260929/startup-red/AetherBoy.RuntimeTests_net10.0_x64.trx`.

Im fertigen Arbeitsstand ist der bekannte Fehler als ausdrücklich benannter `ProtocolReviewCurrentLimitation`-Charakterisierungstest erfasst, neben der positiven Kontrolle. Ein grüner Charakterisierungstest bedeutet hier **Fehler reproduziert**, nicht **Fehler repariert**. Die Produktionsreparatur muss anschließend das gewünschte Invariant prüfen: Spiel und Adapter sind gleichzeitig etabliert und der erste echte Befehl wird genau einmal übertragen.

Der abschließende Lauf umfasst `GbaGen3StartupReferenceTests`, `GbaGen3ProtocolReviewTests` und `PokemonGen3SerialAdapterTests`: **49 bestanden, 0 fehlgeschlagen, 0 übersprungen**. Davon sind zwei die neuen Startzustandstests. TRX: `artifacts/link-reference-deep-20260929/startup-characterization/AetherBoy.RuntimeTests_net10.0_x64.trx`. Das ist ein gezielter Windows-Testlauf, kein vollständiger erneuter Plattform- oder WAN-Test.

### Reparaturrichtung

Lokale, bereits an das Spiel gelieferte Handshake-Belege sind nicht dasselbe wie eine neue Netzwerkphase. Bei einer Peer-Startänderung darf der Adapter die im Spiel weiterhin existierende Historie nicht kommentarlos vergessen. Bei einem echten lokalen SIO-Reset muss sie dagegen verworfen werden. Neue Gegenstellenbelege, abgebrochene aktive Wörter, Rollenprüfung und Phasenreihenfolge bleiben gesondert zu prüfen.

Nicht einfach `8FFF` in jedem Zustand akzeptieren oder alle Zähler dauerhaft behalten. Pflichtfälle für die Reparatur: Reset vor dem ersten Abschluss, nach dem ersten/zweiten Abschluss, während eines aktiven Wortes, vor und nach dem Mastertoken, echtes lokales Disable/Enable, fehlende frische Peer-Bereitschaft und vertauschte Host-/Gastrollen. Der Test hier behandelt nur eine gezielte Host-Sequenz, nicht diese gesamte Matrix.

## GB/GBC: Was DoubleCherry klärt und was offen bleibt

DoubleCherrys Default-Empfang entscheidet anhand des SC-Taktbits **bei Paketankunft**, ob ein Byte Anfrage oder Antwort ist. Sind beide Spiele bereits intern mit `01` unterwegs, kann auch dort `01 ↔ 01` entstehen. Die Pokémon-Routine kann dadurch auf beiden Seiten externen Takt wählen. Trifft eine Anfrage dagegen die noch externe `02`-Probe des Partners, entstehen passende Rollen. Netpacket-Host/Gast ist nicht automatisch die vom Spiel gewählte Taktrolle.

Unser vorhandener Test reproduziert diese Problemklasse bereits. Wir sollten weder veraltete externe Angebote nachträglich reaktivieren noch `01` heimlich zu `02` ändern.

Ein **noch nicht implementierter enger Versuch** wäre:

1. Ein ausdrücklich aktiviertes Gen1-Profil parkt beide Owner direkt nach ihrer ersten echten externen Wahlprobe, bevor sie weiterlaufen.
2. Beide bestätigen dieselbe Wahlphase und die zugehörigen Angebote. Nur der Host läuft weiter; der Gast hält seine echte externe Probe bereit.
3. Erst das tatsächlich intern gestartete Host-Angebot löst die normale Übertragung aus. Zwischenzeitliche SB-Schreibzugriffe mit weiterhin externem SC reichen nicht.

Wichtig: Gen1 schreibt dieselbe Kombination `SB=02, SC=80` auch beim Verlassen der Verbindung. Normale Nutzdaten können ebenfalls `02` enthalten. Deshalb ist eine automatische Erkennung nur aus diesen zwei Werten unsicher. Ein initialer, bewusst aktivierter Versuch ist begrenzbar; wiederholtes Betreten, Gen2 und Zeitkapsel brauchen jeweils nachgewiesene Phasenerkennung. Die anschließenden Netzwerkwartezeiten pro Byte verschwinden durch diese Wahlhilfe nicht.

## Weitergehende GB/GBC-Alternative: semantischer Tauschpartner

Zusätzlich geprüft: [Lorenzooone/PokemonGB_Online_Trades_and_Battles](https://github.com/Lorenzooone/PokemonGB_Online_Trades_and_Battles/tree/f007993561b6e3e20e2490cc8baf20fdaaaa475f), Commit `f007993561b6e3e20e2490cc8baf20fdaaaa475f`. Die tatsächliche Root-Lizenz enthält MIT; für eine spätere Übernahme gelten deren Bedingungen. Hier wurde nur gelesen.

Die Implementierung kennt die Spielstruktur. Sie kann zunächst eigene Partydaten in einem vorbereitenden Durchlauf erfassen, komplette Sektionen übermitteln und dem Spiel beim eigentlichen Tausch bereits lokal verfügbare Partnerdaten anbieten. Ohne gültige Gegenstellendaten wird der vorbereitende Durchlauf wieder geschlossen. Das ist ein virtueller Pokémon-Partner, kein transparenter Kabelkanal.

- [RBYTrading](https://github.com/Lorenzooone/PokemonGB_Online_Trades_and_Battles/blob/f007993561b6e3e20e2490cc8baf20fdaaaa475f/utilities/rby_trading.py): eigene `FLL1`-/`SNG1`-Nachrichten, Auswahl-/Zustimmungswerte, drei Hauptsektionen von 10, 418 und 197 Byte im gelesenen Standardprofil.
- [GSCTrading](https://github.com/Lorenzooone/PokemonGB_Online_Trades_and_Battles/blob/f007993561b6e3e20e2490cc8baf20fdaaaa475f/utilities/gsc_trading.py): `get_big_trading_data`, `read_section`, `buffered_trade`, `player_trade` und `do_trade`; vier Hauptsektionen von 10, 444, 197 und 385 Byte, zusätzliche Mail- und Tauschzustände. Regionale Varianten sind getrennt vorhanden.
- [BGB-Link-Anbindung](https://github.com/Lorenzooone/PokemonGB_Online_Trades_and_Battles/blob/f007993561b6e3e20e2490cc8baf20fdaaaa475f/utilities/bgb_link_cable_server.py): lokale Partnerantworten und eigener Master-Sendepfad; keine replizierte zweite vollständige Spielinstanz als Voraussetzung dieses Proxys.

Das ist die konkreteste weitergehende Referenz, falls unser byteweiser Weg die Rollenwahl oder den Durchsatz nicht zuverlässig erreicht. Sie erweitert aber unsere Verantwortung um Partydatenformate, Validierung, regionale Unterschiede, Abbrüche und spielbezogene Semantik. Explizite Erfolgsnachrichten ersetzen keinen Nachweis, dass beide Batterie-Spielstände dauerhaft korrekt gespeichert wurden. Der dort dokumentierte Gen3-Multiboot-Weg ist außerdem nicht unser unveränderter GBA-Kabelclub.

## Reihenfolge für die Umsetzung

1. **GBA-Startzustand reparieren:** neue Reproduktion in einen echten Fix-Test verwandeln; lokale und entfernte Resets sowie ersten Datenbefehl gemeinsam prüfen.
2. **Gen3-Lebenszyklus prüfen:** Start, mehrere Datenabschnitte, tatsächliche Abschlusskommandos, lokaler SIO-Austritt, Wiederbetreten. Die [schon umgesetzten Abschnitts-/Transportkorrekturen](LINK_CABLE_FIXES_2026-09-29.md) erhalten und nicht als neue Änderungen dieses Audits ausgeben.
3. **GB/GBC-Wahlversuch isolieren:** zuerst explizites Gen1-Profil und ein vollständiger Austauschabschnitt mit Verzögerung, danach belastbare Entscheidung zwischen diesem Weg und semantischem Profil. Keine ungeprüfte universelle Freigabe.
4. **Unabhängig Transport bestätigen:** vorhandenen ROM-freien Raum-/Relay-Test Windows ↔ Linux im realen WAN durchführen. Die Protokollanalyse beweist keine Erreichbarkeit eines TURN-Servers.
5. **Mit echten Sitzungskopien abnehmen:** zuerst lokal, dann WAN; beide Rollen, weiterer Tausch, Raum erneut betreten, normal speichern, beide Kopien kalt starten und Ergebnisse prüfen. Originale bis zur bewussten Übernahme schützen.

gpSP-Vergleiche müssen ausdrücklich den Pokémon-**Kabelmodus** verwenden. Seine automatische Auswahl kann bei Feuerrot/Blattgrün/Smaragd stattdessen RFU wählen; eine erfolgreiche Wireless-Vorführung wäre kein Gegenbeweis für einen Kabeladapterfehler.

Die Bedienung bleibt Raum erstellen/beitreten; die verschiedenen Spielprotokolle benötigen keine kryptischen Copy-Paste-Codes. Neue Einschränkungen oder Freigaben gehören in die gemeinsame Runtime und dieselben Profile auf beiden Betriebssystemen.

## Grenzen dieses Arbeitspakets

Neu sind die vertieften Quellenberichte und der eigene Startzustandstest. Keine Produktionsdatei wurde in diesem Arbeitspaket geändert, kein fremder Emulator gebaut oder ausgeführt, kein Server konfiguriert, kein realer Spielstand verändert und nichts committed oder gepusht. Ein funktionierender Pokémon-Tausch zwischen Windows und Linux bleibt eine ausstehende Abnahme, keine durch diese Dokumentation erledigte Aufgabe.
