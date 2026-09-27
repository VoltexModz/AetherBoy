# Auftrag an den Linux-Entwickler und seinen ChatGPT

Stand: **27. September 2026**. Arbeitsbasis: `development`, mindestens
**`f94bdefa22d9086d4a901297d736899f1116d408`**. Dieses Dokument ist eine
Arbeitsanweisung mit offenen Abnahmen, kein Bericht über einen erfolgreichen WAN-Test.

## 1. Ziel und Reihenfolge

Max nutzt Windows in Deutschland, sein Kollege Linux in Österreich. Beide sollen
über einen kurzen Raumcode verbinden und später Pokémon tauschen können. Es bleibt
**ein Projekt mit gemeinsamer Runtime**, nicht zwei Emulator-Forks.

Bitte in dieser Reihenfolge arbeiten:

1. Git-Stand prüfen und Windows-Änderungen erhalten.
2. Raumdienst mit gültigem HTTPS und dem aktuellen Testprofil bereitstellen.
3. Linux-Konsolentest bauen; **zuerst Windows-GUI ↔ Linux-CLI über das Internet testen**.
4. Linux-Oberfläche für denselben Verbindungstest ergänzen. Das ist kein Hindernis
   für Schritt 3; bei fehlendem Testpartner kann die UI-Arbeit vorgezogen werden.
5. Fehler gezielt mit beiden Diagnoseberichten eingrenzen, nicht den Transport ersetzen.
6. Erst nach bestandenem Transporttest GB, GBC und GBA separat mit Save-Kopien prüfen.
7. Ergebnisse, offene Fehler und nächste Schritte an Max zurückgeben.

**Erster benötigter Rücklauf an Max:** erreichbare HTTPS-Basisadresse des Raumdienstes,
deployed Git-Stand, `/healthz`-Ergebnis, Bestätigung der privaten Schlüsselverteilung
und ein gemeinsamer Testtermin. Keine Zugangsdaten in den Handoff oder öffentlichen Chat.

## 2. Was bereits vorhanden und geprüft ist

- `ab5d777`: Integration des Kollegenstands, native Diagnose, gemeinsamer
  ROM-freier Transporttest und Profil `transport-probe-v1` im Raumserver.
- `f94bdef`: Windows-Dialog unter **Tools → Online Link → Verbindung testen · ohne ROM**,
  gemeinsame `OnlineProbeSession`, Fortschritt/RTT, Abbruch und bereinigte Berichte.
- Native **libdatachannel 0.24.5**, zuverlässiger geordneter WebRTC-Datenkanal;
  der öffentliche native Raumweg erzwingt **Relay / TURN über UDP**.
- Kurzer **10-stelliger** Code, z. B. `ABCDE-FGHJK`. Offer/Answer werden automatisch
  vermittelt. Keine manuellen SDP-Texte für den normalen Verbindungsweg.
- Native Fehlermeldungen, ICE/Gathering, Datenkanal und ausgewählter Kandidatenpaar-Typ
  sind schon angebunden. Diese Diagnose nicht erneut parallel implementieren.
- Die Linux-Oberfläche hat bereits Spielräume, Servereinstellungen und Berichtsordner,
  aber **noch keine Oberfläche für den neuen ROM-freien Test**. Die CLI existiert.
- [CI für f94bdef](https://github.com/VoltexModz/AetherBoy/actions/runs/36319356576):
  Windows Release, Linux x64 und Linux ARM64 erfolgreich. Linux-CI enthält native
  lokale Transporttests sowie isolierte Wayland-UI-/Accessibility-Prüfungen.
- Lokale Windows-Abnahme: Build ohne Warnungen/Fehler; 909 .NET-Tests bestanden,
  47 übersprungen, 0 fehlgeschlagen; 8 Node-Tests bestanden. Die 24 neuen Tests sind
  in diesem Ergebnis enthalten, nicht zusätzlich dazu zu zählen.

**Noch nicht nachgewiesen:** produktive Raumserver-/Coturn-Konfiguration, euer realer
Windows↔Linux-WAN-Pfad, Hyprland-Bedienung der noch zu bauenden Testoberfläche und
ein auf beiden Rechnern dauerhaft gespeicherter Pokémon-Tausch. Grüne CI ist kein
Ersatz dafür. Ältere Handouts enthalten historische Browser-Anleitungen und Testzahlen.

## 3. Git sicher zusammenführen

Im Repository zuerst nur prüfen:

```bash
git status --short --branch
git fetch origin
git log --oneline --decorate -12
git log --oneline HEAD..origin/development
git log --oneline origin/development..HEAD
git diff --stat
```

Ist der Arbeitsbaum sauber und der aktuelle Branch `development`, ist ein
Fast-forward möglich:

```bash
git merge --ff-only origin/development
git merge-base --is-ancestor f94bdefa22d9086d4a901297d736899f1116d408 HEAD
```

Der letzte Befehl muss Exitcode 0 liefern. Bei lokalen Änderungen, anderem Branch
oder divergierenden Commits erst die Differenz prüfen und den Integrationsweg mit
dem Kollegen klären. **Kein Hard Reset, kein Force-Push, keine fremden Änderungen
verwerfen.** Dieses Handoff verpflichtet nicht zum Anlegen eines PRs. Commit/Push
im Linux-Arbeitsbereich nur entsprechend der dortigen Nutzerfreigabe.

## 4. Zwei verschiedene Serveraufgaben

| Bestandteil | Aufgabe | Was er nicht beweist |
| --- | --- | --- |
| HTTPS-Raumdienst, `services/online-rooms` | Raumcode, Teilnehmerzuordnung, Offer/Answer und Relay-Konfiguration | Ein gesundes HTTP-Ende beweist keinen Datenkanal |
| Coturn | Relay für den verschlüsselten Datenkanal | Eine TURN-Adresse stellt keine Website und kein `/healthz` bereit |

### 4.1 Bestehenden Raumdienst prüfen oder bereitstellen

Nur mit Berechtigung des Serverbetreibers deployen; ohne Zugang die fehlende
Information anfordern, keine erfundenen Adressen oder Zugangsdaten einsetzen.
Die genaue vorhandene CapRover-Konfiguration ist hier nicht bekannt.

1. Prüfen, ob die CapRover-App `aetherboy-rooms` schon existiert. Keine zweite
   gleichnamige Infrastruktur daneben aufbauen. Vor einem Update Konfiguration
   und bisherigen Deployment-Stand festhalten; Secrets nicht exportieren.
2. Aus dem abgeglichenen Repository ein frisches, geheimnisfreies Paket bauen:

   ```bash
   mkdir -p artifacts/online-room-handoff
   tar -czf artifacts/online-room-handoff/AetherBoy-Online-Rooms.tar.gz \
     -C services/online-rooms captain-definition Dockerfile server.mjs
   sha256sum artifacts/online-room-handoff/AetherBoy-Online-Rooms.tar.gz
   git rev-parse HEAD
   ```

   Paket enthält **nur diese drei Dateien**, keine `.env`, ROM, Saves oder
   Nutzerkonfiguration. SHA-256 und Quellcommit im Deploymentprotokoll notieren.
3. CapRover → Deployment → Deploy via Tarball. Das Dockerfile verwendet Node 24
   und startet als nicht privilegierter Benutzer `node`.
4. Umgebungsvariablen ausschließlich in der Serververwaltung setzen:

   | Variable | Vorgabe |
   | --- | --- |
   | `PORT` | `8080` |
   | `ROOM_ACCESS_KEY` | Privater Zufallsschlüssel, 32–256 druckbare ASCII-Zeichen ohne Leerzeichen |
   | `TURN_URL` | Eigener erreichbarer `turn:<host>:<port>?transport=udp`-Endpunkt |
   | `TURN_USER` | Benutzer der bestehenden Coturn-Konfiguration |
   | `TURN_PASSWORD` | Zugehöriges Coturn-Passwort |

   Für neue Schlüssel einen kryptografischen Zufallsgenerator verwenden.
   Bereits im Chat offengelegte Schlüssel/Passwörter als offengelegt behandeln
   und mit dem Betreiber koordiniert ersetzen; danach beide Clients aktualisieren.
   Keine echten Werte in Shell-History, Git, Screenshots oder Diagnoseanhänge schreiben.
5. Container HTTP Port **8080**, **genau eine Replik**, kein persistentes Volume
   erforderlich. Räume sind im RAM, verfallen nach zehn Minuten; Neustarts entfernen
   die Signalisierungsräume und können laufende Aufbauten unterbrechen.
6. Eine verfügbare Subdomain zuweisen, gültiges HTTPS aktivieren und HTTPS erzwingen.
   Eine IP-Adresse ist nur mit einem für genau diese IP gültigen, vom Client
   akzeptierten Zertifikat geeignet. Nicht die Zertifikatsprüfung deaktivieren.
   Keine Host-Port-Freigabe für 8080 nötig, wenn CapRover den HTTP-Proxy übernimmt.
7. Prüfen, dass der Proxy authentifizierte Antworten nicht zwischenspeichert und
   keine Authorization-Header, Teilnehmer-Tokens oder SDP-Inhalte protokolliert.
   Es ist HTTP-Polling, kein neu einzurichtender WebSocket-Dienst. CORS ist für die
   nativen Clients kein Lösungsansatz.

Öffentlicher Gesundheitstest, Beispieladresse vorher ersetzen:

```bash
curl --fail --show-error --silent https://rooms.example.com/healthz
```

Erwartet: `{"status":"ok","protocol":1}`. **Kein `curl -k`.** Im Emulator nur
`https://rooms.example.com` speichern, **ohne** `/healthz`, `/v1`, Zugangsdaten,
Query oder Fragment. Die Prüfung unter `/healthz` erfordert keinen Schlüssel,
bestätigt aber weder authentifizierten Raumzugang noch `transport-probe-v1`.
Die Profilunterstützung durch den deployed Quellstand und einen echten Test-Raum
bestätigen. Keine rohen Create-/Join-Antworten veröffentlichen: Sie enthalten Secrets.

Der Dienst ist eine **private Gruppenlösung**: Wer den Zugangsschlüssel besitzt,
kann auch die konfigurierten TURN-Zugänge erhalten. Nicht als offene öffentliche
Lobby freischalten. Aktuell maximal 100 Räume und global 60 Aufnahmeversuche/Minute.

### 4.2 Coturn separat prüfen

- Mit dem Betreiber die tatsächliche UDP-Listener-Adresse, `external-ip` bei NAT,
  `min-port`/`max-port`, Port-Mappings und Firewall-Regeln vergleichen. Die alten
  Notizen nennen 49160–49200/UDP; **nicht als aktuelle Serverkonfiguration voraussetzen**.
- Listener-Port und konfigurierter Relay-Portbereich müssen passend geroutet sein.
  HTTP/HTTPS-Reverse-Proxy allein stellt kein UDP-Relay bereit.
- Keine vollständige Firewall-Abschaltung und kein pauschales `allow-loopback-peers`.
  Erlaubte Gegenstellen gezielt prüfen; nicht ohne Befund Sicherheitsregeln lockern.
- Für den Test UTC-Zeitfenster bereithalten und Allocation, Permission sowie ggf.
  ChannelBind methodenbezogen prüfen. Serverlogs bleiben zunächst beim Betreiber.
- TURN-401 kann der normale Realm/Nonce-Schritt sein. Erst die folgende Sequenz
  beurteilen. HTTP-401 des Raumdienstes ist ein anderer Fehler. TURN-403 ist nicht
  automatisch ein falsches Passwort. Fehlende Kandidaten sind kein Firewall-Beweis.
- Dieser native Build ist auf TURN/UDP ausgelegt. UDP-gesperrte Netze sind damit
  nicht automatisch abgedeckt; keinen stillen Browser-/TCP-Fallback hinzufügen.

## 5. Linux bauen und den vorhandenen Konsolentest nutzen

Voraussetzungen aus `global.json`/Buildskript: .NET SDK **10.0.302** bzw. erlaubter
Patch derselben SDK-Linie, Node 24, Git, CMake, C/C++-Buildwerkzeuge, OpenSSL-3-
Entwicklungsdateien. Auf Ubuntu heißen die nativen Buildpakete `build-essential`,
`cmake`, `libssl-dev`; auf CachyOS/Arch die passenden Distributionspakete verwenden.
Installation nicht ungefragt ausführen. Native Frontend-Abhängigkeiten stehen in
[LINUX_WAYLAND.md](LINUX_WAYLAND.md).

Im Repository-Root, in dieser Reihenfolge:

```bash
dotnet --version
node --version
bash scripts/build-online-native.sh
dotnet restore tools/AetherBoy.OnlineProbe/AetherBoy.OnlineProbe.csproj --locked-mode --configfile NuGet.config
dotnet build tools/AetherBoy.OnlineProbe/AetherBoy.OnlineProbe.csproj -c Release --no-restore
dotnet run --project tools/AetherBoy.OnlineProbe/AetherBoy.OnlineProbe.csproj -c Release --no-build --no-restore -- --help
```

Das native Skript baut den im Repository festgeschriebenen libdatachannel-Commit.
Nicht nebenbei auf eine andere Version wechseln. **Nach** dem nativen Build die
.NET-Projekte bauen, damit `libdatachannel.so` mitkopiert wird. Bei Loaderfehlern
Ausgabeverzeichnis, Architektur und mit `ldd` die Abhängigkeiten der selbst gebauten
Bibliothek prüfen. Nicht einzelne zufällige `.so`/DLLs austauschen.

Frontend bei Bedarf frisch bauen/starten:

```bash
bash scripts/build-linux.sh
bash scripts/run-linux.sh
```

Linux → **F10 / Tools → Online Link → SERVER SETTINGS**: dieselbe HTTPS-Basisadresse
und denselben privat verteilten Raumdienstschlüssel speichern wie unter Windows.
Datei: `${XDG_CONFIG_HOME:-$HOME/.config}/aetherboy/online-room.json`.
Die Anwendung speichert sie mit Benutzer-Lese-/Schreibrechten. Nicht committen.
`--settings <datei>` erlaubt bei der CLI einen abweichenden Speicherort; es ist
kein Argument für den Schlüssel selbst.

Für den ersten gemeinsamen Lauf erstellt **Windows** den Test-Raum. Linux führt aus:

```bash
dotnet run --project tools/AetherBoy.OnlineProbe/AetherBoy.OnlineProbe.csproj -c Release --no-build --no-restore -- join
```

Den von Windows erhaltenen Code erst am Prompt eingeben. Für den Rollenwechsel:

```bash
dotnet run --project tools/AetherBoy.OnlineProbe/AetherBoy.OnlineProbe.csproj -c Release --no-build --no-restore -- host
```

Dann den angezeigten Code in Windows unter **Mit Code testen** eingeben. Diese
zwei Befehle sind Alternativen auf dem Linux-PC, kein hintereinander auszuführendes
Zweierpaar. Keine ROM nötig. Kein Browser, keine Portfreigabe am Heimrouter als
erster Schritt und kein manuelles SDP. In beiden Varianten Standard **8 Samples
je Größe** belassen; Windows verwendet ebenfalls acht.

## 6. Gemeinsamer WAN-Test und Abnahme

1. Beide notieren Quellcommit/Build, Betriebssystem, Rollen und UTC-Startzeit.
   Für die erste Abnahme denselben Quellstand verwenden. Zwei echte PCs mit
   getrennten Internetanschlüssen, nicht zwei Prozesse am selben PC.
2. Frischen Test-Raum erstellen; keine alten Codes wiederverwenden. Windows zeigt
   Raumzugang → Gegenstelle → Datenkanal → Datenprüfung.
3. Jeder Ursprung prüft **32 eigene Echos**, acht je 32/256/1024/4096 Byte.
   Sequenz und Inhalt müssen stimmen; RTT-Minimum/Median/P95/Maximum notieren.
   Eine feste RTT unter 50 ms ist keine Korrektheitsbedingung.
4. **Beide Teilnehmer offen lassen, bis beide PASS/bestanden melden.** Linux erst
   dann Enter drücken, Windows erst dann Test beenden. Ein lokales PASS bestätigt
   nicht automatisch das Ergebnis des anderen Rechners.
5. Bereinigte Berichte beider Seiten ansehen: `native-config` mit `relay-only=true`,
   ausgewähltes `candidate-pair` mit `local=relay remote=relay` bei dieser beidseitigen
   Relay-Konfiguration und offener Datenkanal. Bei fehlendem Paar ist der erwartete
   Pfad nicht vollständig dokumentiert; nicht als bewiesen abhaken.
6. Drei frische Läufe mit beiden Rollenverteilungen anstreben. Zusätzlich Abbruch
   während des Aufbaus, sauberes Beenden und erneuten Verbindungsaufbau prüfen.
   Erwartung: UI bleibt bedienbar, kein hängender Besitzer, keine alten Raumdaten.
7. Bei Scheitern zunächst beide Berichte und das Coturn-Zeitfenster sichern. Nicht
   wahllos Server, SDK, Transport und Core gleichzeitig ändern.

Berichte:

- Windows: `%LOCALAPPDATA%\AetherBoy\development\OnlineDiagnostics`, oder im Dialog
  **Bericht kopieren**. Speichern ist optional, vorhandenen Opt-out respektieren.
- Linux: `${XDG_STATE_HOME:-$HOME/.local/state}/aetherboy/online-diagnostics`;
  CLI-Option `--reports <ordner>`. Die ausdrücklich gestartete CLI schreibt ihren
  eigenen Testbericht unabhängig vom Schalter für normale Spielsitzungen.

Die Runtime maskiert sensible Inhalte; dennoch vor dem Weitergeben kontrollieren.
Keine kompletten Konfigurationsdateien, SDPs, Rohantworten, Schlüssel, Raumcodes,
ROMs/Saves oder unbereinigten Coturn-Logs anhängen. RTT-Auswertung separat aus der
CLI bzw. Oberfläche festhalten. Ein Timeout misst **keinen UDP-Paketverlust**;
4096 Byte Anwendungsdaten sind nicht automatisch ein einzelnes UDP-Datagramm.

### Fehler gezielt einordnen

| Beobachtung | Nächste Prüfung, keine vorschnelle Ursachenzusage |
| --- | --- |
| `/healthz` scheitert | URL, DNS/Zertifikat, Proxy, Container, Port; ohne Zertifikats-Bypass |
| Raum-HTTP 401 | Raumdienstschlüssel auf Server und Client, nicht zuerst TURN ändern |
| Raum-HTTP 400 | Operation und Profil prüfen; alter Server oder ungültige Anfrage möglich |
| Raum-HTTP 404 / 409 | Neuer gültiger Code / richtiger Raumtyp / bereits belegter Raum |
| Raum-HTTP 429 | Aufnahmeversuche stoppen, Limitfenster abwarten; keine Retry-Schleife |
| Keine Relay-Kandidaten | Native Diagnose und TURN-Konfiguration/Erreichbarkeit/Auth prüfen |
| ICE hängt trotz Allocation | Relay-Ports, Peer-Permissions und beidseitige Ereignisse prüfen |
| ICE verbunden, kein Kanal | Native DTLS/SCTP-Meldungen prüfen; MTU ist nur eine Hypothese |
| Kanal offen, Echo fehlt/falsch | Probe-Version, Größen, Reihenfolge, Lifecycle/Disconnect prüfen |

## 7. Linux-Testoberfläche implementieren

**Gemeinsame Logik wiederverwenden, keine zweite Netzwerk-Zustandsmaschine bauen.**
Windows ist Verhaltensreferenz, nicht in Wayland zu portierendes WinForms-Markup.

Relevante Dateien:

- `nanoboy/Runtime/Netplay/OnlineProbeSession.cs`: öffentlicher Sitzungsbesitzer.
- `nanoboy/Runtime/Netplay/OnlineTransportProbe.cs`: Testformat/Resultate.
- `nanoboy/Runtime/Netplay/OnlineRoomTransport.cs`, `NativeRtcPeer.cs`,
  `OnlineRoomDiagnostics.cs`: vorhandener Transport und Diagnose.
- `nanoboy/frmOnlineConnectionTest.cs`: Windows-Bedienung/Lifecycle als Referenz.
- `frontends/AetherBoy.Desktop/WaylandEmulatorHost.OnlineRooms.cs` und
  `WaylandEmulatorHost.OnlineLink.cs`: Linux-Einstieg, Settings und Spielraum-Guards.
- `tests/AetherBoy.RuntimeTests/OnlineProbeSessionTests.cs`, `OnlineRoomTests.cs`
  sowie `tests/AetherBoy.SmokeTests/WindowsConnectionTestTests.cs`: Vertrags-/Referenztests.
- [Detaillierte gemeinsame API-Übergabe](CONNECTION_TEST_UI_HANDOFF.md).

Empfohlene UI-Ergänzung: eigene Partial-Datei im Wayland-Frontend und Tests im
vorhandenen `AetherBoy.DesktopTests`-Projekt. Den SDL/Wayland-Renderer und bestehende
Text-, Fokus-, Clipboard- und Accessibility-Hilfen nutzen, keine X11-Abhängigkeit.

Pflichtverhalten:

- Einstieg **ohne geladene ROM**; nur eine aktive Testsitzung.
- Gleiche `online-room.json`, maskierter Schlüssel, bewusster Speichern-Knopf.
  Keine konkurrierenden Einstellungseditoren mit veralteten Kopien.
- Erstellen, Code kopieren/eingeben, Beitreten, vier bestätigte Stufen,
  Anzahl geprüfter Echos, RTT nach Größe, verständliche Fehler und erneuter Versuch.
- `new OnlineProbeSession(settings, host, code, diagnosticDirectory)` verwenden;
  bei ausgeschalteter Dateiaufzeichnung `null` übergeben. Vorhandene globale
  Diagnoseentscheidung als Anfangswert übernehmen, nicht still einschalten.
- `Snapshot` auf dem UI-Thread abfragen, keine Controls aus Netzwerkcallbacks ändern.
  `Snapshot.Active` bleibt bis zum vollständigen Aufräumen wahr. **Passed kann
  weiterhin Active sein**; nicht unmittelbar nach PASS freigeben.
- `Completion` umfasst auch Offenhalten und Aufräumen. Nicht auf `Completion`
  warten, um erst dann das Ergebnis anzuzeigen; `Snapshot.Result` verwenden.
- Abbruch/Schließen über `StopAsync()`/`DisposeAsync()`. Kein `.Wait()`/`.Result`
  im UI-Thread. App-Dispose muss ebenfalls Abbruch signalisieren. Neustart erst
  nach abgeschlossenem Aufräumen. Die Lifecycle-Tests decken langsames Cleanup ab.
- Während des Tests neue Online-Spielsitzungen blockieren und umgekehrt.
  Dem Test keine ROM-/Save-Dienste übergeben, kein Emulator-Core nötig.
- `GetDiagnosticReport()` nur ausdrücklich kopieren; Berichtspfad öffnen, sofern
  vorhanden. Bei Speicherfehler weiterhin bereinigtes Kopieren ermöglichen.
  Niemals das komplette Snapshot-Objekt exportieren: Es enthält Code und lokalen Pfad.
- Fehlermeldungen aus `OnlineProbeFailure` ableiten. Keine beliebigen Exceptions
  in öffentliche Berichte übernehmen, keine unbewiesene Router-Schuld behaupten.

UI-Tests mindestens für: Einstieg ohne ROM, Settings, Host/Join, Fortschritt,
beidseitig kompatible Standard-Samples, PASS mit offenem Kanal, Abbruch, langsames
Cleanup, Close/App-Dispose, Neustart, Spielraum-Guards, Opt-out, Berichtsschreibfehler,
HTTP-Fehlerkategorien. Danach real unter **Hyprland/Wayland** Tastatur, Fokus,
Einfügen/Kopieren, Größenänderung und Schließen prüfen; headless CI allein reicht
für diese Desktop-Abnahme nicht. Linux-Testzahl bei neuen Tests in CI anheben,
bestehende Gates nicht abschwächen.

## 8. Linux-Regressionsprüfung

Nach nativem Build, vom Repository-Root; keine Produktions-TURN-Werte für Fixtures:

```bash
unset AETHERBOY_TEST_TURN_URL
export AETHERBOY_TEST_NATIVE_ONLINE=1
for project in tests/AetherBoy.CoreTests/AetherBoy.CoreTests.csproj \
               tests/AetherBoy.RuntimeTests/AetherBoy.RuntimeTests.csproj \
               tests/AetherBoy.DesktopTests/AetherBoy.DesktopTests.csproj; do
  dotnet restore "$project" --locked-mode --configfile NuGet.config || exit 1
  dotnet build "$project" -c Release --no-restore -p:ContinuousIntegrationBuild=true || exit 1
done
dotnet test --project tests/AetherBoy.CoreTests/AetherBoy.CoreTests.csproj -c Release --no-build --no-restore --minimum-expected-tests 232
dotnet test --project tests/AetherBoy.RuntimeTests/AetherBoy.RuntimeTests.csproj -c Release --no-build --no-restore --minimum-expected-tests 392
dotnet test --project tests/AetherBoy.DesktopTests/AetherBoy.DesktopTests.csproj -c Release --no-build --no-restore --minimum-expected-tests 122
node --test tests/browser/WebRtcBridge.test.cjs services/online-rooms/server.test.mjs
```

Jeden Exitcode prüfen; bei einem fehlgeschlagenen Test nicht die folgenden Erfolge
als Gesamterfolg ausgeben. Zahlen gelten für `f94bdef`; nach neuen Tests entsprechend
erhöhen. CI nutzt Microsoft.Testing.Platform (`dotnet test --project`), nicht
ungeprüft alte VSTest-Filter übernehmen. Node muss für native Raum-Fixtures im PATH sein.

Mit den in CI dokumentierten Weston/D-Bus-Abhängigkeiten zusätzlich:

```bash
bash scripts/test-linux-headless.sh tests/AetherBoy.DesktopTests/bin/Release/net10.0/AetherBoy.DesktopTests.dll --minimum-expected-tests 118
bash scripts/build-linux.sh
```

Das Headless-Gate zählt ausgeführte Tests anders als der vorherige Discovery-Lauf;
die drei optionalen Audioplaytests sind in diesem Gate nicht enthalten. Den
aktuellen Workflow `.github/workflows/ci.yml` als Referenz verwenden. Nach Änderungen
an Runtime/Protokoll auch Windows-CI verlangen; Linux-Erfolg ersetzt diese nicht.

## 9. Erst danach echte Spiele

**GBA-Singleplayer funktioniert nicht automatisch gleich gut wie GBA-Online.**
Die bestehenden Onlinepfade bleiben Entwicklungsprofile:

- GB/GBC: `gb-serial-v1`, eigene Kabel-Zustandsmaschine. Keine pauschale Zusage für
  alle Spiele oder jede Pokémon-Paarung. Bekannte Bereitschafts-/Timing-Grenzen
  stehen im [GB/GBC-Handoff](ONLINE_LINK_HANDOFF.md).
- GBA: `gba-pokemon-gen3-v1`, begrenzter Pokémon-Gen3-Adapter; keine allgemeine
  GBA-Onlinefreigabe. `GbaOnlineProfileCatalog.cs` prüft konkrete ROM-Inhalte.
  **Rocket Edition nicht als ersten Test verwenden**, Freigaben/Hashprüfungen
  nicht umgehen. Erkannt bedeutet Entwicklungskandidat, nicht geprüfter Tausch.
- Kein GB↔GBA-Kabeltausch, kein Wireless-Adapter, kein automatischer Save-/ROM-Austausch.
  Die vorgesehenen Spiel-Kabelräume nutzen; Freischaltungen im Spiel separat prüfen.

Für GB, GBC und eine erkannte GBA-Originalfassung **je eine eigene Testzeile**:

1. Spiel, Sprache/Revision und Paarung notieren, nur lokal vorhandene rechtmäßig
   genutzte Dateien verwenden. Spielstände vorab sichern und im Spiel speichern.
2. Die vorgesehenen geschützten Online-Sitzungskopien bestätigen. Ein Host, ein Gast.
   Turbo, Rewind, Reset, Cheats und einseitige Save-State-Ladevorgänge bleiben gesperrt.
3. Zuerst Verbindung im Spiel, dann einen entbehrlichen Pokémon-Tausch testen.
   Host-/Gastrolle und genaue Stelle eines Fehlers festhalten.
4. Beide im Spiel speichern und kontrolliert beenden. Originale nicht automatisch
   überschreiben. Ein sauberer Netzwerkabschluss ist kein vollständiger Tauschbeweis.
5. Separate Prüfkopien der Sitzungssaves neu laden und auf beiden Seiten Inhalt
   kontrollieren. **Journalisierte Archivdateien nicht direkt starten/verändern**,
   sonst stimmen deren Integritätswerte nicht mehr.
6. Erst nach beidseitiger Prüfung bewusst über **Session copies / Übernehmen**
   übernehmen. Bei unklarem Abschluss Kopien erhalten, keinen erzwungenen Rückimport.
   [Save-Schutz und Wiederherstellung](GBA_ONLINE_HANDOFF.md).

## 10. Verbindlicher Rückbericht an Max / Windows-ChatGPT

Im Repository einen neuen datierten Handoff ergänzen und oben im
`LINUX_DEVELOPMENT_HANDOFF.md` verlinken. Alte Aussagen als Historie erhalten.
Nicht nur „geht jetzt“ schreiben, sondern dieses Raster ausfüllen:

```text
Basiscommit und neuer Commit:
Geänderte Dateien / Linux-UI / shared Runtime / Server:
Windows-Auswirkungen oder erforderliche Folgeänderungen:
Raumdienst-URL, deployed Quellstand, Paket-SHA256, healthz (keine Secrets):
Coturn-Prüfung: durchgeführt / nicht durchgeführt; Ergebnis:
Build-/Testbefehle und Exitcodes, bestanden / übersprungen / fehlgeschlagen:
Native Linux-/Hyprland-Abnahme:
WAN-Lauf 1..3: UTC, Rollen, beide Builds, beide PASS?, Größen/RTT, Relay-Paar:
Abbruch und Wiederverbindung:
GB / GBC / GBA-Spieltests jeweils: nicht getestet / fehlgeschlagen / bestanden:
Beidseitig neu geladene Saves geprüft? Originale unverändert?:
Bereinigte Diagnoseanhänge und belegte Fehlerursache oder offene Hypothesen:
Was Max als Nächstes konkret tun soll:
```

Nicht vor Ort verfügbare Prüfungen ausdrücklich **nicht getestet** nennen.
Keine Netzwerkgarantie aus Unit-Tests ableiten. Ohne Serverzugang trotzdem bauen,
lokal testen und die Linux-UI vorbereiten; den Produktions-WAN-Schritt offen lassen.

## English summary for the receiving assistant

Start from `development` including `f94bdef`. Preserve both developers' work.
The native libdatachannel transport, relay-only room flow, sanitized diagnostics
and `transport-probe-v1` already exist. Do not replace them with raw UDP or manual
SDP exchange. All three CI jobs passed; a production cross-border connection and
a persistent Pokémon trade remain unverified.

First deploy/verify the separate HTTPS room service and existing Coturn with the
operator's authorization. Send Max the service base URL and deployed revision,
not credentials. Build Linux's existing OnlineProbe CLI and run it against the
Windows in-app test before making the Linux GUI a prerequisite. Keep both sides
open until both pass. Then bind `OnlineProbeSession` to the native Wayland UI,
preserving async cleanup, diagnostics opt-out and game-room exclusion. Verify
GB/GBC and the restricted GBA Gen3 profile separately with protected save copies.
Return actual commands, test counts, both-side WAN evidence and remaining limits.
