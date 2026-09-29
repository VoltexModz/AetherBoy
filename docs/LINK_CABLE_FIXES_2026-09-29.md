# Link-Kabel: Korrekturen nach dem Quellcode-Audit

Stand: 29. September 2026. Baut auf dem [reproduzierbaren Audit](LINK_PROTOCOL_AUDIT_2026-09-29.md) und der [Quellenrecherche](LINK_CABLE_RESEARCH_2026-09-29.md) auf.

## Was geändert wurde

### 1. Raumserver: Zeitlimit umfasst die vollständige Antwort

`nanoboy/Runtime/Netplay/OnlineRoomTransport.cs`

Ein verknüpftes 15-Sekunden-Budget umfasst jetzt HTTP-Header und sämtliche Lesezugriffe auf den Antwortbody. Zuvor konnte die Aufnahme in einen Raum nach gelieferten Headern unbegrenzt auf unvollständiges JSON warten. Der Abbruch durch den Benutzer bleibt von einem internen Timeout unterscheidbar. Antwortgrößenlimits, Weiterleitungsverbot, Zugangsschutz und Protokollkennungen bleiben unverändert.

### 2. Transportende: bereits empfangene Abschlussnachrichten bleiben verfügbar

`OnlineRoomTransport.TryReceive` und `WebRtcBrowserTransport.TryReceive` erlauben das Leeren der vorhandenen Empfangsqueue nach einem sauberen Verbindungsende. Sobald ein Fehler vorliegt, bleiben gepufferte Pakete gesperrt, insbesondere nach Überfüllung oder ungültigen Nachrichten.

Das garantiert weder die Zustellung eines noch ausstehenden Sendepuffers noch einen erfolgreichen Tausch. Es behebt ausschließlich den Verlust des Zugriffs auf **bereits angenommene** Pakete, die der Emulations-Thread noch nicht gelesen hat.

### 3. GBA: versetztes Ende eines etablierten Linkabschnitts

`PokemonGen3SerialAdapter.cs`, `GbaOnlineLinkCoordinator.cs`

Ein Reset des Partners auf die nächste Phase beendet nicht mehr sofort den eigenen, noch etablierten Abschnitt:

1. Der Adapter merkt den angekündigten Phasenwechsel vor.
2. Letzte echte Spielbefehle bleiben in der alten Phase lieferbar.
3. Erst die lokale SIO-Deaktivierung beendet den lokalen Abschnitt und wechselt einmal zur nächsten Phase.
4. Bereits vollständig zusammengesetzte ausgehende Befehle bleiben vor dem Reset in der geordneten Warteschlange.
5. Höchstens 64 frühe Handshake-Meldungen der nächsten Phase werden berücksichtigt; neue Nutzdaten dürfen die Phasengrenze nicht überqueren.

Eine monotone Frist von 30 Sekunden begrenzt den ausstehenden Wechsel. Handshakes, Heartbeats und Pause verlängern diese Frist nicht. Ein ausstehender Wechsel darf nicht als sauberer Sitzungsabschluss bestätigt werden.

Unvollständige oder ungelesene Nutzdaten, wiederholte Sequenzen, unzulässige Phasensprünge, falsche Prüfsummen und Überläufe brechen weiterhin ab. Keine erfundenen Abschlusskommandos, keine automatische Übernahme von Spielständen. Frühe Initialisierungs-Resets vor einem etablierten Spielprotokoll behalten ihr bisheriges Verhalten.

### 4. GB/GBC: tatsächlicher Wartegrund statt pauschaler Netzwerkursache

`NetworkSerialCable.cs`, `OnlineLinkCoordinator.cs`

Ein rein lesender `WaitReason` unterscheidet fehlendes Übertragungsangebot, passives Warten auf externen Takt sowie fehlende Bereitschafts- oder Abschlussbestätigung. Der bestehende Timeout nennt den fehlenden Protokollschritt.

**Dies repariert nicht die Rollenwahl.** Der Ausführungsstopp, die Takt-Arbitrierung, der Standardtimeout und alle übertragenen Bytes bleiben unverändert. Zwei externe Taktrollen können weiterhin ohne Clockkanten bleiben; bei einer ungepaarten internen Probe bleibt die Maschine bis zum echten Gegenangebot oder dem bestehenden Timeout angehalten.

Die Quellenprüfung hat keinen verantwortbaren kleinen Fix ergeben: Ohne gemeinsame emulierte Zeit lässt sich ein noch unterwegs befindliches Gegenangebot nicht sicher von einem tatsächlich inaktiven Partner unterscheiden. Coffee GB kontrolliert zwei lokale Maschinen und besitzt dafür andere Voraussetzungen. Seine Scheduler-Heuristik wird nicht blind auf unabhängige WAN-Maschinen übertragen.

### 5. Windows: Fehler im Spielprotokoll nicht mehr als Serverstörung ausgeben

`nanoboy/frmNano.OnlineLink.cs`

Ein bestätigter Transportfehler behält seine Transportdiagnose. Ohne Transportfehler berücksichtigt die Abschlussdiagnose stattdessen die Sitzungsausnahme und den vom Emulations-Thread gemeldeten Fehler. Bekannte Ursachen wie Prüfsummenfehler, fehlende GB-Bestätigungen oder unvollständige GBA-Abschnitte erhalten deutsche Beschreibungen.

Unbekannter Ausnahmeinhalt wird nicht ungeprüft in die Oberfläche übernommen. Die vollständige Ursache bleibt im vorhandenen lokalen Fehlerbericht; der Dialog behauptet keine unbewiesene Routerursache. Die schmale Statuszeile verweist auf **TOOLS → Online Link → Letzte Verbindungsdiagnose**, statt einen langen Fehler samt Sicherheitshinweis abzuschneiden.

## Prüfstand

Alle folgenden Läufe erfolgten lokal auf Windows mit .NET SDK 10.0.302. Testberichte liegen unter `artifacts/link-fixes-20260929/`. Verwendet wurden synthetische Testprogramme und temporäre lokale Dienste; keine produktiven Zugangsdaten oder Originalspielstände.

| Prüfung | Ergebnis | Bericht-Unterordner |
| --- | --- | --- |
| Gesamte Core-Suite | 242 bestanden, 0 Fehler | `core-full` |
| Gesamte Runtime-Suite | 416 bestanden, 5 übersprungen, 0 Fehler | `runtime-full` |
| Vier native Raum-/Datenkanaltests separat aktiviert | 4 bestanden, 0 Fehler | `native-loopback` |
| Windows-Online-Smoke-Tests | 25 bestanden, 0 Fehler | `windows-online-final` |
| Gesamte portable Desktop-Suite auf Windows | 91 bestanden, 42 übersprungen, 0 Fehler | `desktop-full` |

Die vier nativen Fälle sind dieselben vier Opt-in-Fälle, die im allgemeinen Runtime-Lauf übersprungen wurden. Der verbleibende Runtime-Skip benötigt Linux-Symlinks. Die Desktop-Skips benötigen unter anderem echte Wayland-, GTK-/AT-SPI-, Audio- oder Unix-Berechtigungsbedingungen. Damit sind **778 unterschiedliche Fälle bestanden**, aber keine native Linux-Abnahme erfolgt.

Die vier ursprünglich roten Runtime-Regressionen bestehen jetzt. Zusätzlich prüfen die Tests weiterhin Fehlerabschluss, Benutzerabbruch, Replay, Queueüberlauf und Originalspielstandschutz. Im Windows-Integrationstest erzeugt jeder der drei Systeme GB/GBC/GBA absichtlich einen Protokollfehler bei gesundem Transport; die UI muss die richtige Fehlerklasse anzeigen und den Owner samt Save-Sperre sauber freigeben.

Der neue native Abschlussfall prüft den gesamten Send/Close/Drain-Pfad der echten Bibliothek. Ein leerer lokaler Sendepuffer ist dabei kein Remote-Empfangsnachweis; der Test setzt zusätzlich den geordneten SCTP-Abschluss voraus. Die separate Browser-Regression isoliert das Nachlesen bereits angenommener Abschlussnachrichten.

Copy-Prüfung ausgeführt und gelesen: ein Hinweis auf einen englischen internen Vergleichsstring im Übersetzungs-Switch. Dieser String wird nicht angezeigt und muss zum Runtime-Fehlertext passen; die deutsche Ausgabe steht daneben. `git diff --check` ohne Whitespacefehler. Der erste Runtime-Build eines neuen Testtreibers scheiterte am Zugriff auf eine interne Core-Methode; der Test nutzt jetzt öffentliche CPU-Schritte und ist im grünen Gesamtlauf enthalten.

Die GBA-Regressionen prüfen zusätzlich eine autonome Gastseite und drei aufeinanderfolgende Abschnitte mit beiden echten Adaptern und seriellen Controllern. Pro Abschnitt werden zwölf vollständige Acht-Wort-Befehle je Richtung übertragen, Prüfsummen verglichen und Abschluss sowie Wiedereintritt versetzt ausgeführt. Das ist ein programmatischer Treiber, **nicht** die vollständige FireRed-Software mit VBlank, Timer 3, Pokémon-Daten und Speicherung.

## Übergabe an den Linux-Entwickler

- Die Transport-, Core- und Runtime-Korrekturen sind gemeinsame Komponenten und gelten beim nächsten Build für beide Plattformen. Es braucht keine separate Linux-Kopie dieser Logik.
- Beide Teilnehmer mit demselben neuen Stand bauen. Wireformat und Gen3-Profilrevision wurden nicht verändert; ein älterer Build kann den normalen versetzten Abschluss weiterhin ablehnen.
- Auf Linux Core-, Runtime- und Desktop-Tests ausführen. Die nativen Loopback-Tests zusätzlich mit `AETHERBOY_TEST_NATIVE_ONLINE=1`; zunächst ohne `AETHERBOY_TEST_TURN_URL`.
- Lokale automatische Tests auf diesem Windows-PC ersetzen weder einen nativen Linux-Lauf noch eine Windows↔Linux-WAN-Prüfung.
- Raumdienst und TURN-Konfiguration wurden nicht verändert. Der native Client ist weiterhin TURN/UDP-basiert; es wurde kein TCP-/TLS-Ausweichweg hinzugefügt.
- Die neuen Fehlerursachen im englischen Linux-Fehlerbericht kontrollieren. Keine allgemeine GBA-Kompatibilität oder erfolgreich abgeschlossenen Tausche aus einem offenen Kanal ableiten.

## Noch notwendige Abnahme

1. ROM-freie native Verbindung über die beiden echten Internetanschlüsse prüfen und bereinigte Berichte beider Rechner vergleichen.
2. GBA: freigegebene Originalfassungen und gesicherte Spielstandkopien zunächst lokal, dann im WAN prüfen. Mehrere Tausche, Verlassen und Wiederbetreten des Kabelclubs sowie Rollenwechsel testen.
3. Anschließend beide Sitzungskopien aus dem Batteriespielstand neu starten und das Ergebnis auf beiden Seiten kontrollieren. Erst danach manuell übernehmen.
4. GB/GBC: Rollenwahl und Probe-/Retry-Verhalten mit abgestimmtem Zeitmodell lösen oder ein ausdrücklich spielbezogenes Gen1/Gen2-Profil gesondert entwickeln. Nicht durch manipulierte Antwortbytes kaschieren.

**Kein bestätigter WAN-Pokémon-Tausch. Kein Commit oder Push in diesem Arbeitspaket.** Fremde, bereits vorhandene Änderungen im Arbeitsverzeichnis bleiben bestehen.

## English handoff

The shared runtime now bounds complete room HTTP responses, retains already accepted packets after orderly closure, and defers a Gen3 peer phase reset until the local game finishes its current section. Incomplete payload, replay and overflow guards remain fail-closed. Pending Gen3 transitions have a 30-second monotonic deadline, including while paused.

GB/GBC gained precise wait diagnostics only; mirrored role election and unpaired internal-clock execution barriers remain unresolved. Windows now separates known session/protocol failures from actual transport failures and avoids displaying arbitrary exception text. Both peers need the updated build; protocol identifiers are unchanged. Local synthetic/loopback checks are not retail-game, Linux-runtime or WAN-trade validation. Original saves and production infrastructure were not touched.
