# Verbindungstest im Emulator / In-app connection test — 2026-09-27

## Deutsch: Windows-Bedienung

**Tools → Online Link → Verbindung testen · ohne ROM** öffnet einen eigenen
AetherBoy-Dialog. Kein Browser, keine SDP-Texte und kein Spiel erforderlich.

1. HTTPS-Raumserver und privaten Raumdienst-Schlüssel eintragen. Die Einstellungen
   sind dieselben wie für Spiel-Räume; **Server speichern** speichert sie dauerhaft.
   Für den aktuellen Test werden die sichtbaren Eingaben verwendet.
2. Einer erstellt einen **Test-Raum** und teilt nur den kurzen Code. Der andere
   öffnet ebenfalls den Verbindungstest und wählt **Mit Code testen**.
3. Die Oberfläche zeigt bestätigten Raumzugang, Gegenstelle, Datenkanal und
   Datenprüfung sowie die Anzahl bereits geprüfter Antworten.
4. Ergebnis: 32 Nachrichten je Richtung, je acht mit 32/256/1024/4096 Byte.
   Angezeigt werden RTT-Minimum, Median, P95 und Maximum je Größe.
5. **Beide Fenster offen halten, bis beide PCs bestanden melden.** Erst dann
   **Test beenden** oder schließen. Ein lokales Ergebnis bestätigt nicht den
   Ergebnisbildschirm der Gegenseite und niemals einen Pokémon-Tausch.

Der Server muss `transport-probe-v1` unterstützen (bereits in `ab5d777` enthalten).
Das zusätzliche GUI benötigt keine weitere Server-/Protokolländerung.
Ein Spiel-Raum kann nicht versehentlich mit einem Test-Raum verbunden werden.
Die native öffentliche Verbindung erzwingt weiterhin TURN/UDP.

**Abbrechen** signalisiert sofort das Beenden; das Aufräumen läuft asynchron.
Solange es läuft, bleiben Raumstart und Konfigurationsänderung gesperrt. Ein
anschließender Versuch erstellt eine neue Sitzung. Das Schließen des Dialogs
und das direkte Freigeben des Hauptfensters beenden ebenfalls die Testverbindung.
Eine aktive Online-Spielsitzung und der Verbindungstest schließen sich aus;
der Test hat keine Referenz auf ROM-Pfade, Emulator-Sitzungen oder Save-Dienste.
Der Wechsel zum jeweils anderen Raumdialog schließt einen inaktiven Editor,
damit keine zwei unterschiedlichen Kopien der Servereinstellungen offen bleiben.

### Fehler und Datenschutz

- Separat verständliche Meldungen für falschen Schlüssel, fehlenden/abgelaufenen
  Raum, belegten/falschen Profilraum, alte Serveranfrage, Rate-Limit, Timeout und
  fehlgeschlagene Datenintegrität. Sonstige Transportfehler bleiben ausdrücklich
  ungeklärte Verbindungsfehler; sie beweisen keinen Router-/MTU-Fehler.
- **Bereinigten Bericht lokal speichern** übernimmt initial die bestehende
  Aufzeichnungsentscheidung. Ein Opt-out wird nicht still eingeschaltet. Die
  Checkbox ist für diesen Test ausdrücklich änderbar, nicht der globale Schalter.
- Ohne Dateispeicherung bleiben bereinigte Ereignisse im Arbeitsspeicher verfügbar.
  **Bericht kopieren** ist eine ausdrückliche Benutzeraktion, kein Upload.
- Keine Raumcodes, Zugangsschlüssel, SDP-Texte, Endpunkte, ROMs oder Saves im
  Diagnosebericht. Der Raumcode wird nur für die Verbindung und die ausdrückliche
  Aktion **Code kopieren** verwendet. Das Schlüsselfeld ist immer maskiert.
- Bei fehlgeschlagener Berichtsspeicherung bleibt manuelles Kopieren möglich.

## Übergabe an Linux

Die Windows-Oberfläche liegt in `nanoboy/frmOnlineConnectionTest.cs`; die
plattformneutrale Ablaufsteuerung in `nanoboy/Runtime/Netplay/OnlineProbeSession.cs`.
Die Linux-Oberfläche ist **noch nicht erweitert**. Dort bleibt die bereits
vorhandene `AetherBoy.OnlineProbe`-CLI nutzbar; GUI und CLI verwenden dasselbe
`transport-probe-v1`-Format und standardmäßig acht Samples je Größe.

Für den nativen Linux-Dialog keine zweite Testlogik implementieren:

```csharp
IOnlineProbeSession probe = new OnlineProbeSession(
    roomSettings, isHost, roomCode,
    recordingEnabled ? diagnosticDirectory : null);

// Im UI-Tick abfragen, keine Control-Zugriffe aus Netzwerk-Callbacks:
OnlineProbeSnapshot state = probe.Snapshot;

// Aus einer ausdrücklich ausgelösten Kopieraktion:
string safeReport = probe.GetDiagnosticReport();

// Abbruch/Schließen asynchron; kein .Wait() oder .Result im UI-Thread:
await probe.StopAsync();
```

- `Snapshot.Active` bleibt bis zum tatsächlichen Aufräumende wahr. **Passed**
  kann weiterhin aktiv sein: Die Verbindung muss für ausstehende Echos offen bleiben.
- `Completion` umfasst Aufbau, Messung, Halten nach Erfolg und Aufräumen; es ist
  **nicht** lediglich das Task des Messergebnisses. Ergebnis aus `Snapshot.Result`.
- `Connection.Stage` ist ein stabiler Diagnosebezeichner, kein lokalisierter
  Anzeigetext: `room-admission`, `relay-preparation`, `remote-offer`,
  `ice-gathering`, `publish-description`, `remote-answer`, `data-channel-open`,
  `connected`. `RoomAdmitted` bedeutet akzeptierten Raumzugang, nicht nur HTTP-Erreichbarkeit.
- `Failure` liefert feste Fehlerkategorien. Keine beliebigen Exception-Texte
  oder das gesamte Snapshot-Objekt in öffentliche Berichte kopieren: Das Snapshot
  enthält den anzuzeigenden Raumcode und ggf. einen lokalen Berichtspfad.
- `Dispose()` signalisiert Abbruch ohne zu warten; `StopAsync()`/`DisposeAsync()`
  warten asynchron auf das Ende. Pro Fenster genau eine aktive Probe besitzen.
- `OnlineRoomRequestException` erbt von `IOException` und enthält den HTTP-Status
  separat. Bestehende allgemeine I/O-Fehlerbehandlung bleibt erhalten.

## Frische lokale Prüfung

Vollständiger Locked Restore und Release-Build unter Windows x64/.NET 10.0.302:
**0 Warnungen, 0 Fehler**. Getrennte Testläufe mit aktivierten lokalen nativen
Verbindungen und ohne produktive TURN-Zugangsdaten:

| Suite | Bestanden | Übersprungen | Fehler |
| --- | ---: | ---: | ---: |
| Core | 232 | 0 | 0 |
| Runtime | 391 | 1 | 0 |
| Portable Desktop-Logik auf Windows | 80 | 42 | 0 |
| Windows Smoke/UI | 206 | 4 | 0 |
| **.NET gesamt** | **909** | **47** | **0** |
| Node: Raumdienst/Browser-Bridge | 8 | 0 | 0 |

24 neue Testfälle: zwölf Runtime-Fälle (Erfolg mit Halten der Verbindung, Abbruch,
langsames Aufräumen, Timeout, Datenkorruption, HTTP-Fehler, Ablehnung von Spielräumen)
und zwölf Windows-UI-Fälle (Menü ohne ROM, Dialogwechsel, Eingaben, Fortschritt,
Ergebnis, Neustart, Schließen, Opt-out und verständliche Fehler).
Der bestehende native Probe-Profiltest betreibt jetzt zwei echte lokale
`OnlineProbeSession`-Teilnehmer. Die CLI-Hilfe ist weiterhin erfolgreich.

Artefakte: `artifacts/connection-test-ui-20260927/` mit `test-results/`,
gezielten Tests, Test-Screenshots und dem frischen Windows-Build
`bin/nanoboy/release/AetherBoy.exe`. Screenshots nutzen ausschließlich künstliche
Konfiguration und Messwerte zur Layoutprüfung, keinen erfolgreichen WAN-Test.

Keine echten Spielerdaten, kein produktiver Server und keine Linux-Oberfläche
wurden verändert. Native Linux-Ausführung dieses Stands, Produktions-TURN,
Windows↔Linux-WAN und Pokémon-Tausch bleiben separate Abnahmen. Commit und Push
auf `development` wurden am 27. September 2026 freigegeben; dieses Handoff
begleitet die Veröffentlichung. Kein Server-Deployment durch dieses Arbeitspaket.

## English

Windows now exposes **Tools → Online Link → Verbindung testen · ohne ROM**:
a themed, ROM-free room setup and transport test with clear milestones, exact
echo counts and per-size RTT statistics. It reuses the existing room settings,
respects the initial diagnostic opt-out and supports explicit sanitized-report
copying. Setup failure, cancellation, pending cleanup, restart and window disposal
are covered; no emulator or save APIs are passed to the dialog. Game-room and
probe-room settings editors cannot remain open with stale independent settings.

`OnlineProbeSession` is shared Runtime code. The Linux developer should bind
`Snapshot`, `GetDiagnosticReport()` and asynchronous `StopAsync()` to native UI.
The Linux UI has not been implemented in this change; the existing CLI remains
available. The wire format, server profile and relay-only policy are unchanged.
Local PASS deliberately keeps the transport alive until explicit stop or peer
disconnect. Both users must see PASS before closing; this is not a game-trade proof.

Fresh Windows verification: build with zero warnings/errors; 909 .NET passes,
47 platform/hardware skips, zero failures; eight Node passes and CLI help success.
The 24 new cases cover session lifecycle and Windows UI. Real native local
transport integration also runs. Screenshot measurements are synthetic fixtures,
not WAN evidence. Native Linux, production relay and cross-platform trading still
require separate validation. Commit and push to `development` were approved on
September 27, 2026; this handoff accompanies publication. No server deployment
was performed.
