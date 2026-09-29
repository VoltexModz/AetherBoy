# Celio-Link und GBLink: Quellcodevergleich für AetherBoy

Stand: 29. September 2026. Gelesene Referenzen sind auf die unten genannten Commits festgelegt. Es wurden keine fremden Programme gestartet, keine Hardware angeschlossen und keine Pokémon-Tausche durchgeführt. Öffentlich beschriebene Erfolge sind keine eigene Abnahme unseres Emulators.

## Ergebnis

Celio-Link bestätigt die grundsätzliche Architektur unseres GBA-Gen3-Profils: Ein lokaler Endpunkt bedient das Spiel mit korrekten Linkrunden, während vollständige Spielkommandos unabhängig vom Internet-Takt übertragen werden. Es ist **kein universeller transparenter GBA-Kabeltunnel**. Die wichtigen Vorbilder sind die Trennung von Hardwaretakt und Spielkommandos, frische Handshake-Belege nach jedem Abschnitt und die Zustellung letzter Abschlusskommandos.

Nicht übernehmen sollten wir künstliche Abschlusskommandos, stillschweigend verworfene Nutzdaten, unbeschränkte Warteschlangen oder feste Wartezeiten ohne nachgewiesenen Bezug zu unserer eigenen Emulation. Die Referenzen enthalten solche Kompromisse. Deshalb ersetzen weder ihr Quellcode noch ihre Demos die Prüfung unserer eigenen Zustandsmaschine.

## Reproduzierbare Quellenstände

| Repository | Commit | Lizenzdatei gelesen | Schwerpunkt |
| --- | --- | --- | --- |
| [Celio-Firmware](https://github.com/Celio-Link/Celio-Firmware/tree/4aef46a1aff688cc0a68df666858eccb9b5ae291) | `4aef46a1aff688cc0a68df666858eccb9b5ae291` | `LICENSE`, GPL Version 3 | RP2040-PIO, lokale Rundentaktung, USB-Puffer, Abschnittsende |
| [Celio-mGBA-Link](https://github.com/Celio-Link/Celio-mGBA-Link/tree/5f29daba2a331f721203f430b28750e72e68ac02) | `5f29daba2a331f721203f430b28750e72e68ac02` | `LICENSE.txt`, GPL Version 3 | Lua-Gerätemodell, Emulator-Hooks, Timer, Socket-Verbindung |
| [Celio-Server](https://github.com/Celio-Link/Celio-Server/tree/d19ad22a0c6a11467c9234c6cacd291eb0f15dbd) | `d19ad22a0c6a11467c9234c6cacd291eb0f15dbd` | `LICENSE.txt`, GPL Version 3 | Raumcode, Rollen, Handshake-Koordination, Datenweiterleitung |
| [Celio-Client](https://github.com/Celio-Link/Celio-Client/tree/1d712b2849c3cf248c367035cc0222798b0b4162) | `1d712b2849c3cf248c367035cc0222798b0b4162` | `LICENSE.txt`, GPL Version 3 | Sequenzen, Empfangsbestätigung, USB-/Socket-Grenze |
| [GBLink Netplay Bridge](https://github.com/GB-Link/gblink-netplay-bridge/tree/61e4f2004e0df9d0847d1c7e85eff28bbd512e0a) | `61e4f2004e0df9d0847d1c7e85eff28bbd512e0a` | `LICENSE`, GPL Version 3 | Celio-Firmware ↔ gpSP-MPK1, Wiedereintritt, letzte Daten |

Lokale Textkopien liegen in `artifacts/link-reference-deep-20260929/celio/<Organisation>/<Repository>/`. Der Schwerpunkt lag auf den Protokolldateien, nicht auf sämtlichen UI-Assets oder Builddateien. Es wurde kein fremder Implementierungscode in Produktionsdateien übernommen.

## 1. Firmware: Was tatsächlich über das Internet läuft

Der physische Takt bleibt auf dem RP2040. [`linkLayer_pio.c`](https://github.com/Celio-Link/Celio-Firmware/blob/4aef46a1aff688cc0a68df666858eccb9b5ae291/src/layers/linkLayer_pio.c) implementiert getrennte PIO-Programme für Master und Slave. `pioIsr_tx` lädt das nächste Wort und die lokale Taktvorgabe; `pioIsr_done` liefert empfangenes und zuletzt gesendetes Wort an die höhere Ebene. Kabelerkennung und SD-Pin-Umschaltung betreffen echte Hardware und sind kein Emulator-Netzwerkverfahren.

[`PacketLayer`](https://github.com/Celio-Link/Celio-Firmware/blob/4aef46a1aff688cc0a68df666858eccb9b5ae291/src/layers/packetLayer.hpp) bildet drei Zustände ab: Handshake, Prüfsumme, acht Kommandowörter. Die Prüfsumme summiert beide Richtungen der lokalen Verbindung. `receiveCrc` ignoriert allerdings die eingehende Prüfsumme; AetherBoy prüft diese bereits strenger. Die Datentransportschicht führt keine einzelne Netzwerkanfrage pro serieller Clockkante aus.

[`usbLinkCommand.cpp`](https://github.com/Celio-Link/Celio-Firmware/blob/4aef46a1aff688cc0a68df666858eccb9b5ae291/src/callbacks/usbLinkCommand.cpp) nimmt jeweils vier Acht-Wort-Kommandos auf. Fehlt ein eingehendes Kommando, liefert `usbLinkTransive` Nullwörter, während die lokale Verbindung weiterläuft. Die Queue fasst 200 Kommandos. Die Rückgabewerte der vier nicht blockierenden `k_msgq_put`-Aufrufe werden nicht geprüft: Die Existenz einer begrenzten Queue allein beweist hier also keine verlustfreie Überlastbehandlung.

[`UsbSection::bufferReceivedPackets` und `flush`](https://github.com/Celio-Link/Celio-Firmware/blob/4aef46a1aff688cc0a68df666858eccb9b5ae291/src/sections/usbSection.hpp) bündeln vier Runden in 64 Byte, überspringen vollständig leere Blöcke und unterdrücken wiederholte `CAFE 0011`-Kommandos ohne Richtungsänderung. Das ist eine spielbezogene Optimierung und keine allgemeine Regel für beliebige GBA-Programme.

Für uns: Null-Idle, Acht-Wort-Pakete und lokale Prüfsummen passen zum bestehenden `PokemonGen3SerialAdapter`. Die bereits vorhandenen harten Queuegrenzen und sichtbaren Abbrüche bei Überlauf sollten nicht durch Celios stilles Verhalten ersetzt werden. Eine optionale Unterdrückung von Bewegungs-Idle müsste separat durch eine Spielprotokollspezifikation und Regressionen begründet werden.

## 2. Handshake, Rollen und Abschnittswechsel

[`UsbSection::establishConncection`](https://github.com/Celio-Link/Celio-Firmware/blob/4aef46a1aff688cc0a68df666858eccb9b5ae291/src/sections/usbSection.cpp) meldet zuerst den vom eigenen Spiel beobachteten Handshake. Erst eine Steueranweisung erlaubt das Antworten. Master- und Slave-Rollen werden von außen gewählt; ein Netzteilnehmer muss nicht aus der Ankunftszeit eines Nutzdatenbytes die Rolle erraten.

In [`PacketLayer::onTransiveDone`](https://github.com/Celio-Link/Celio-Firmware/blob/4aef46a1aff688cc0a68df666858eccb9b5ae291/src/layers/packetLayer.cpp) reicht ein tatsächlich gesendetes oder empfangenes `8FFF` für den Übergang in die Prüfsummenphase. Diese Regel ist nicht identisch mit unserem zusätzlichen `handshakeRounds >= 2`-Kriterium. Insbesondere ein nur im Adapter zurückgesetzter Handshakezähler bei bereits weitergelaufener Spiel-Zustandsmaschine verdient einen eigenen Test. Die Referenz ist dafür ein Hinweis, noch kein Beweis für eine konkrete AetherBoy-Fehlersituation.

`UsbSection::process` verfolgt die tatsächlich übertragenen `5FFF`-Abschlusskommandos beider Richtungen. Wenn beide beobachtet wurden, wird der Restpuffer geleert. Ein Ausgang aus dem Raum über ein entsprechendes Tastenkontrollkommando beendet die Sitzung; andere Abschnittswechsel dürfen die Verbindung weiterverwenden. [`LinkModule::execute`](https://github.com/Celio-Link/Celio-Firmware/blob/4aef46a1aff688cc0a68df666858eccb9b5ae291/src/module/link.cpp) baut dafür nach 400 ms eine neue Section auf. Der Master versucht beim Abschalten außerdem höchstens 100 ms auf das Ende einer lokalen Runde zu warten; der Slave kann deren Fortsetzung nicht selbst garantieren.

Für AetherBoy ist der **lokale SIO-Austritt** das aussagekräftigere Signal für das tatsächliche Ende des eigenen Spielabschnitts. Unsere zuletzt ergänzte vorgemerkte Peer-Phase verhindert, dass ein früheres Ende des Partners das eigene Spiel vorzeitig in einen neuen Handshake versetzt. Die 400-ms-Pause ist eine Hardware-Implementierungsentscheidung, kein allgemeiner Emulatorfix.

## 3. Celio-mGBA-Link ist kein unverändertes mGBA

[`main.lua`](https://github.com/Celio-Link/Celio-mGBA-Link/blob/5f29daba2a331f721203f430b28750e72e68ac02/main.lua) akzeptiert ausdrücklich nur mGBA-Commit `3da13060a586f2da8eb4ecbb167f642e2e4889c2` und verweist bei Abweichung auf einen Spezialfork. Ein Watchpoint auf das Lesen von `SIOMULTI1` bildet die Empfangsregister; zusätzliche `vblankIRQ`- und `timer3IRQ`-Callbacks treiben die Partnerseite. Das ist kein fertiges Drop-in-Script für unser C#-Serial-Interface oder beliebige mGBA-Versionen.

[`celio_device.lua`](https://github.com/Celio-Link/Celio-mGBA-Link/blob/5f29daba2a331f721203f430b28750e72e68ac02/celio_device.lua) folgt wieder Handshake → Prüfsumme → acht Wörter, verwendet Nullkommandos bei leerem Netzpuffer und setzt die Section erst zurück, nachdem echte `5FFF` in beiden Richtungen verarbeitet wurden. Die Socket-Schicht wird nicht als emulierter Takt verwendet.

Der Timerquelltext enthält einen besonders guten Grund, Kommentare nicht blind als Messwert zu übernehmen: `enable_timer3` schreibt `FED0`, nennt im Kommentar aber 197 Ticks. Rein numerisch sind `10000 - FED0 = 304` Ticks. Welches Verhalten der gepinnte Spezialfork daraus erzeugt, wurde nicht ausgeführt. Für AetherBoy bleiben das eigene Timer-/SIO-Verhalten und die gegengeprüfte Spiel-Zustandsmaschine maßgeblich.

## 4. Was der Celio-Server zusätzlich übernimmt

[`Session::handleStatusMessage`](https://github.com/Celio-Link/Celio-Server/blob/d19ad22a0c6a11467c9234c6cacd291eb0f15dbd/src/session.ts) weist Rollen zu, wartet auf Handshake-Belege beider Geräte und steuert anschließend deren Start. Datenpakete tragen Sequenzen, Status-/Steuerpakete UUIDs. Der Server leitet Nutzdaten über Socket.IO weiter; er ist nicht bloß ein SDP-Adressbuch. Geordnete Bestätigungsversuche werden über `concatMap` serialisiert. Beide gemeldeten Linkabschlüsse führen nach einer kurzen Wartezeit zum Entfernen der Sitzung.

Die [Client-Seite](https://github.com/Celio-Link/Celio-Client/blob/1d712b2849c3cf248c367035cc0222798b0b4162/src/shared/linkExchange/linkExchangeSession.ts) sortiert Sequenzen und verwirft bereits empfangene Nummern. In [`CommandEmitterSocketIO`](https://github.com/Celio-Link/Celio-Client/blob/1d712b2849c3cf248c367035cc0222798b0b4162/src/shared/linkExchange/commandEmitter/commandEmitter.socketIO.ts) wird aber schon vor der Weiterverarbeitung bestätigt. Ein solcher ACK ist ausdrücklich kein Nachweis, dass ein Game Boy das Kommando konsumiert oder den Tausch gespeichert hat.

[`SessionManager`](https://github.com/Celio-Link/Celio-Server/blob/d19ad22a0c6a11467c9234c6cacd291eb0f15dbd/src/sessionManager.ts) erzeugt vierstellige Raumnummern mit `Math.random`. Der [Server-Einstieg](https://github.com/Celio-Link/Celio-Server/blob/d19ad22a0c6a11467c9234c6cacd291eb0f15dbd/src/index.ts) nutzt einen vom Client gelieferten Identifikator zur Wiederanbindung. Das ist eine Beschreibung dieser geprüften Dateien, keine vollständige Sicherheitsbewertung der öffentlichen Installation. Für unsere eigenen Zugangstokens, Größenlimits und Raumvergabe ist es kein Sicherheitsvorbild.

Ein eigener authentifizierter WSS-Datenrelay wäre grundsätzlich eine alternative Transportoption für unser bereits paketorientiertes Gen3-Profil. Celios Architektur zeigt, warum dafür keine universelle Echtzeitübertragung einzelner SIO-Bits notwendig ist. Sie beweist weder, dass unser TURN-Problem damit gelöst wäre, noch rechtfertigt sie einen ungeprüften Austausch unseres vorhandenen nativen Transports.

## 5. GBLink-Netplay-Brücke: Besonders wertvolle Grenzfälle

[`mpk1.ts`](https://github.com/GB-Link/gblink-netplay-bridge/blob/61e4f2004e0df9d0847d1c7e85eff28bbd512e0a/src/engine/mpk1.ts) beschreibt gpSP-Pakete mit exakt 24 Byte: Kennung, Zustand/Datennutzungsflag und optional acht 16-Bit-Wörter. Die Bridge übersetzt zwischen diesem Protokoll und der Firmware; sie ist keine vollständige GBA-Emulation.

Die entscheidenden Regeln in [`LinkBridge`](https://github.com/GB-Link/gblink-netplay-bridge/blob/61e4f2004e0df9d0847d1c7e85eff28bbd512e0a/src/engine/bridge/linkBridge.ts):

- Nach dem Abschnittsende gelten alte `CONNECTED`-Keepalives nicht als neue Bereitschaft. `peerReadySeen` verlangt einen frischen Handshake-Zustand.
- Frühe Nutzdaten nach diesem frischen Handshake werden bis zum Öffnen des Schreibpfads gepuffert. Sie können einmalige Tauschbestätigungen enthalten und dürfen nicht einfach verloren gehen.
- `resetSection` und `onFwRound` senden letzte Firmware-Daten auch dann noch, wenn eine Reconnect-Statusmeldung vorher verarbeitet wurde. Zwei USB-Endpunkte können diese Reihenfolge umkehren.
- Die Join-Seite lässt vor dem Verbindungswort 150 ms für reale lokale Handshake-Runden. Das kompensiert Firmware-/Hardwarevoraussetzungen und ist kein allgemeines WAN-Zeitlimit.
- 4 Sekunden ohne Peerstatus, begrenzte Wiederanlaufversuche und eine Schutzzeit nach eigenem Cancel verhindern bestimmte Wiederanlaufschleifen.

[`PacedEmitter` und `RoundBatcher`](https://github.com/GB-Link/gblink-netplay-bridge/blob/61e4f2004e0df9d0847d1c7e85eff28bbd512e0a/src/engine/bridge/pumps.ts) trennen Status-Heartbeats von Datenausgabe, entfernen leere Runden und bündeln USB-Schreibvorgänge. Beim Ende umgeht `flushNow` die normale Ausgabebremse, damit Abschlusskommandos nicht hinter einem abgeschalteten Timer bleiben.

Diese Punkte bestätigen unsere Trennung von Abschnittszustand, Warteschlangen und sauberem Transportende. AetherBoy hat nur einen geordneten Datenkanal; wir müssen keine USB-Endpunktumordnung nachbauen. Ein empfangenes Paket muss aber trotzdem bis zum Emulations-Thread erhalten bleiben. Der bereits ergänzte Close/Drain-Fix behandelt genau diese Grenze.

## 6. Workarounds, die wir nicht übernehmen

1. Die Firmware ersetzt Kommandowort `FF02`, `FF06` oder `FF07` in bestimmten Fällen durch `5FFF`; der Code enthält dazu selbst eine offene Ursachenfrage. AetherBoy darf Statusdaten nicht zu erfundenen Spiel-Abschlusskommandos umdeuten.
2. `LinkBridge::gracefulClose` speist synthetische `5FFF`-Runden ein. Für einen Hardwareadapter kann dies eine kontrollierte Abbruchstrategie sein. Bei unseren geschützten Spielstandkopien darf das niemals als vom Partner bestätigter Tausch gewertet werden; wir behalten einen sichtbaren Abbruch statt fingierter Bestätigung.
3. Die Firmware ignoriert eingehende Prüfsummen, und ihre USB-Empfangsqueue prüft Überlauf-Rückgaben nicht. Wir behalten unsere strengeren Integritätsprüfungen.
4. Bridge-Puffer wie `pendingRounds` sind im gelesenen Code nicht hart begrenzt. AetherBoy benötigt weiterhin Mengen- und Zeitgrenzen, auch wenn ein anderer Client unbegrenzt zwischenspeichert.
5. Starre Hardwarepausen und Lua-Timerüberschreibungen dürfen weder unsere Core-Zeitbasis noch echte Spiele-IRQ-Handler ersetzen.

## 7. Konkrete Folgerungen für unsere Tests

Priorität hat ein spielnaher Gen3-Handshake-/IRQ-Treiber zusätzlich zum vorhandenen Adapter-Rundentreiber:

1. Host und Gast betreten den Kabelclub mit unterschiedlich weit fortgeschrittener lokaler Handshake-Zustandsmaschine. Einen Peer-Reset zwischen bereits gezählten Teilnehmern und dem einmaligen `8FFF` platzieren.
2. Initiale ignorierte Prüfsumme, erste Nutzdatenrunde und echte Timer-3-/VBlank-IRQ-Reihenfolge getrennt aufzeichnen. Adapterzustand und Spielzustand dürfen nicht auseinanderlaufen.
3. Schlusskommando, Peer-Reset und noch ausstehende eigene Ausführung versetzen; danach mehrfach neu verbinden. Frühe nächste Handshakes dürfen keine alten Kommandos neu interpretieren.
4. Beide Richtungen mit längeren Idle-Phasen, Datenbursts und verzögerten Owner-Pumps prüfen. Fehlende Daten dürfen weder zu kopierten alten Kommandos noch zu fingierten Abschlussbytes werden.
5. Erst nach diesen Regressionen echte Spielstandkopien lokal und anschließend Windows ↔ Linux im WAN prüfen. Speichern und Kaltstart beider Kopien gehören zur Abnahme.

GB/GBC benötigen weiterhin einen eigenen Ansatz. Die Gen3-Kommandostruktur und deren erlaubte Null-Idle-Runden lösen die bei Gen1/Gen2 empfangsabhängige Rollenwahl nicht.

## Abstimmung und Nachweisgrenze

Mit der gpSP-/pret-Analyse wurden Prüfsummenrunden, frische Handshake-Belege, Abschnittsgrenzen und das mögliche Auseinanderlaufen von Adapter- und Spielzustand abgeglichen. Mit der DoubleCherryGB-Analyse wurde die Trennung zwischen spezialisiertem Pokémon-Profil und universeller serieller Hardwareemulation bestätigt.

Dieser Vergleich ergänzt die [bereits getesteten Link-Korrekturen](LINK_CABLE_FIXES_2026-09-29.md). Er behauptet keine ausgeführten Hardwaretests oder bestätigten Pokémon-Tausche. Es wurden in diesem Teilauftrag keine Produktionsdateien verändert, keine Builds gestartet und keine Zugangsdaten verwendet.
