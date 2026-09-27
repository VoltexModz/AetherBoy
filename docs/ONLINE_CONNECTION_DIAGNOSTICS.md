# Native Online-Diagnose / Native online diagnostics

## Deutsch

Windows-Einstieg direkt im Emulator: **Tools → Online Link → Verbindung testen ·
ohne ROM**. [Bedienung, Datenschutz und Linux-UI-Übergabe](CONNECTION_TEST_UI_HANDOFF.md).
Die unten beschriebene CLI bleibt für Linux und gezielte Konsolentests verfügbar.

Der normale native Verbindungsweg benutzt einen **10-stelligen Raumcode** wie
`ABCDE-FGHJK`. Keine SDP-Texte kopieren: Offer und Answer werden automatisch über
den privaten HTTPS-Raumdienst ausgetauscht. Der alte Browser-/SDP-Weg bleibt eine
separate manuelle Alternative. Ohne eingerichteten Raumdienst funktioniert der
Raumcode-Weg noch nicht; diese Änderung deployt keinen Server.

### Automatische Berichte im Emulator

Die gemeinsame Runtime erfasst Raum-HTTP-Status mit Operation (ohne URL),
Verbindungsphase, ICE/Gathering/Peer-Zustände, Datenkanal-Öffnen/Schließen,
bereinigte native Meldungen und die Typen des ausgewählten Kandidatenpaars.
Native Relay-Erzwingung erfolgt über `iceTransportPolicy`, nicht per SDP-Filter.
Der Bericht unterscheidet ein fehlendes Kandidatenpaar von einem Lesefehler.

- Windows: `%LOCALAPPDATA%\AetherBoy\development\OnlineDiagnostics`;
  **Tools → Online Link → Native Verbindungsberichte öffnen**.
- Linux: `${XDG_STATE_HOME:-$HOME/.local/state}/aetherboy/online-diagnostics`;
  **Online Link → CONNECTION REPORTS**.

Automatisches Speichern folgt der aktiven Diagnoseentscheidung des Frontends;
ein vorhandener Datenschutz-Opt-out bleibt wirksam. Bei aktivem Speichern wird
ein eindeutiger Bericht pro Verbindung etwa alle zwei Sekunden atomar erneuert
und beim Beenden abgeschlossen. Maximal 512 Ereignisse pro Bericht;
`discardedEvents` zählt verdrängte Einträge. Alte Berichte werden nicht gelöscht.
Nicht mehr benötigte Berichte gelegentlich manuell entfernen.

Es gibt keinen Upload. Keine ROMs, Spielstände, Raumcodes, Adressen, SDP-Texte,
TURN-Passwörter oder Sitzungstokens im Bericht. Freie native Texte werden VOR der
Aufzeichnung auf ein Diagnosevokabular begrenzt; unbekannte Tokens sind maskiert.
Dadurch können auch harmlose Details fehlen. Der native Debug-Logger ist
prozessweit: `native-process` ist ausdrücklich **keiner einzelnen Peer-ID**
zugeordnet, wenn mehrere Verbindungen laufen. Zustandsereignisse sind dagegen
der jeweiligen Sitzung zugeordnet. Zeitangaben: UTC plus monotone Laufzeit.

### Verbindung ohne ROM testen

`AetherBoy.OnlineProbe` ist ein separates Konsolenwerkzeug für Windows und Linux.
Beide Seiten benötigen denselben aktuellen Build und einen Raumdienst, der das
neue Profil **`transport-probe-v1`** akzeptiert. Alte Raumdienste müssen dafür
aktualisiert werden; GB-/GBA-Räume funktionieren unverändert. Der Server lehnt
das Mischen von Probe- und Spielprofilen ab. Kein Testpaket wird in einen
laufenden Emulator-Core eingeschleust.

Zuerst im Emulator einmal HTTPS-Raumserver und Zugangsschlüssel speichern.
Das Werkzeug liest dieselbe `online-room.json`. Keine Zugangsdaten in Befehle
oder den Chat schreiben. Im Repository mit .NET SDK 10:

```text
dotnet restore tools/AetherBoy.OnlineProbe/AetherBoy.OnlineProbe.csproj --locked-mode --configfile NuGet.config
dotnet run --project tools/AetherBoy.OnlineProbe -c Release --no-restore -- host
dotnet run --project tools/AetherBoy.OnlineProbe -c Release --no-restore -- join
```

Host-Befehl auf PC 1, Join-Befehl auf PC 2 ausführen, nicht beide hintereinander
in derselben Konsole. Host teilt den kurzen Code; Gast tippt ihn am Prompt ein.
Linux benötigt vorher `bash scripts/build-online-native.sh`; danach erneut bauen,
damit `libdatachannel.so` neben dem Werkzeug liegt. Windows benötigt die native
DLL aus dem Paket und die Visual-C++-x64-Runtime. Bei portabler Weitergabe immer
den gesamten Publish-Ordner einschließlich Bibliothek/Lizenzen verwenden.

Optionen: `--settings <datei>`, `--reports <ordner>`, `--samples 1..100`.
Beide Teilnehmer müssen dieselbe Sample-Anzahl wählen (Standard: 8 je Größe).
Das ausdrücklich gestartete Diagnosewerkzeug schreibt seinen eigenen Bericht,
unabhängig vom Aufzeichnungsschalter für normale Spielsitzungen.

Es sendet in beiden Richtungen nummerierte Nachrichten mit **32, 256, 1024 und
4096 Byte** Gesamtgröße. Jeder Ursprung prüft seine Echos bytegenau und misst
RTT mit seiner eigenen monotonen Uhr. Angezeigt werden Minimum, Median, P95 und
Maximum. Keine synchronisierten PC-Uhren nötig. Eine ausstehende Antwort hat
20 Sekunden Frist; Sitzung insgesamt 12 Minuten. Ctrl+C bricht ab.

**Beide Fenster offen halten, bis BEIDE PASS melden; erst dann Enter.**
Der Transport arbeitet weiter, während das Werkzeug auf Enter wartet, damit
bereits eingereihte Antworten die Gegenseite erreichen können.
Fehlende Antworten sind unbestätigte Zustellung, kein gemessener UDP-Paketverlust.
Große Anwendungsnachrichten sind nicht identisch mit einzelnen UDP-Datagrammen;
größenabhängige Fehler beweisen keine bestimmte MTU-Ursache.

### Nachweis und nächste Abnahme

Die automatisierten nativen Tests starten einen temporären Raumdienst und zwei
Teilnehmer **auf einem Windows-PC ohne produktiven TURN-Server**. Das ist ein
lokaler Direktverbindungsnachweis, kein Windows↔Linux-WAN-/Relay-Nachweis.

Für den echten Test zuerst beide Berichte mit Coturn-Logs desselben Zeitfensters
vergleichen: erfolgreiche Allocation reicht nicht; Permissions, ggf. ChannelBind,
ausgewählter Pfad und Datenkanal müssen passen. TURN-401 kann die normale
Realm/Nonce-Anfrage sein; TURN-403 kann eine administrative Ablehnung sein.
HTTP-401 des Raumdienstes ist davon unabhängig. Keine vorsorglichen Firewall-
Abschaltungen oder Loopback-Freigaben. Erst nach erfolgreichem Transporttest
GB/GBC und GBA-Gen3 separat mit geschützten Spielstandkopien testen.

## English

Windows also offers an in-app **Verbindung testen · ohne ROM** entry under
Tools → Online Link. See the [UI and shared session handoff](CONNECTION_TEST_UI_HANDOFF.md).
The CLI remains available; the Linux GUI binding is separate follow-up work.

Native rooms use a short **10-character room code**, not manual SDP copying.
The HTTPS room service exchanges descriptions automatically. A deployed,
configured room service is still required; this change does not deploy one.

Shared Runtime diagnostics cover sanitized HTTP operations/status, setup phase,
ICE/gathering/peer states, channel lifecycle, native errors and selected candidate
types. Windows and Linux use the same format. Native logs are process-wide and
not attributed to a particular peer. No endpoints, SDP, room codes, credentials,
ROMs or saves are recorded or uploaded. Unknown native tokens are redacted before
storage. The 512-event ring exposes a dropped-event counter. Existing frontend
recording preferences govern automatic files; old reports are not deleted.

The separate `AetherBoy.OnlineProbe` console tool loads the app's room settings.
Use the commands above (`host` on one PC, `join` on the other). The room service
must support **transport-probe-v1**; it cannot match a probe participant with a
game participant. On Linux build the native library before building this tool.
The public probe always requires native TURN/UDP; it does not silently fall back
to direct ICE, a browser or another transport. Its explicit diagnostic run saves
a report even when ordinary game-session recording is disabled.

Both directions test 32/256/1024/4096-byte messages (8 each by default), sequence,
exact echoed content and origin-measured monotonic RTT. Keep both tools open
until both report PASS, then press Enter. `--settings`, `--reports`, `--samples`
and `--help` are available. Never put access keys/TURN passwords on the command
line. A timeout is an unverified delivery, not a measurement of underlying UDP
loss. PASS confirms this transport test only, not a Pokemon trade.

Local native integration tests on Windows do not prove the production relay or
cross-platform WAN path. Correlate reports with Coturn allocation/permission/
channel logs, then validate GB/GBC and GBA-Gen3 separately using save copies.
