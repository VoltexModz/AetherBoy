# Native Online-Räume: Windows-Abnahme vom 13. September 2026

Historischer Prüfbericht. Aktueller Integrations- und Veröffentlichungsstand:
[Upstream-Abgleich vom 27. September (DE/EN)](UPSTREAM_SYNC_2026-09-27.md).

## Übernommener Stand

`development` wurde per Fast-forward von `23c6fda` auf **`3fb28ca`** gebracht.
Jessys Raumcode-Implementierung enthält bereits Windows und Linux; unsere
vorherigen Windows-Verbindungsdiagnosen bleiben erhalten. Kein Reset, kein
Cherry-pick, keine Veränderung echter ROMs, Spielstände oder Benutzereinstellungen.

Der folgende Teststand enthält zusätzlich eine kleine lokale Windows-Korrektur
und neue Regressionstests. Diese Ergänzungen sind noch nicht committed/gepusht.

## Hier tatsächlich auf Windows x64 geprüft

- Gesperrte NuGet-Abhängigkeiten wiederhergestellt; kompletter Release-Build mit
  SDK **10.0.302**: **0 Fehler, 0 Warnungen**.
- Native Bibliothek `datachannel.dll` vorhanden und auf diesem Rechner geladen;
  installierte Microsoft-Visual-C++-x64-Runtime vorhanden.
- Zwei native Teilnehmer verbinden sich über einen temporären lokalen Raumdienst,
  **ohne Browser und ohne echte Serverzugangsdaten**. Prüfung sowohl mit
  `gb-serial-v1` als auch mit `gba-pokemon-gen3-v1`.
- Je Profil: 4096 Byte exakt übertragen, Antwort zurück übertragen, 4097-Byte-Paket
  abgewiesen, Host beendet und Gast sauber getrennt.
- Windows-Raumdialog: Serveradresse und maskierten Schlüssel speichern/erneut laden,
  Host-/Gastdialog öffnen, vorhandenen Dialog wiederverwenden, ohne geladenes Spiel
  keinen Raumstart zulassen, ungültige HTTP-Adresse ablehnen und gültige Einstellungen
  erhalten. Screenshots mit synthetischen Testwerten geprüft.

| Suite | Gesamt | Bestanden | Übersprungen | Fehler |
|---|---:|---:|---:|---:|
| Core | 232 | 232 | 0 | 0 |
| Runtime einschließlich beider nativer Raumprofile | 365 | 364 | 1 | 0 |
| Windows Smoke einschließlich Raumdialog | 198 | 194 | 4 | 0 |
| Desktop unter Windows | 122 | 80 | 42 | 0 |
| **.NET gesamt** | **917** | **870** | **47** | **0** |
| Node: Raumdienst und Browser-Callbacks | 7 | 7 | 0 | 0 |

Die Übersprünge betreffen Linux-/Wayland-/POSIX-Prüfungen sowie explizite
Windows-Hardware-/Vordergrundtests. Sie sind kein Nachweis dieser Funktionen.
Die native Verbindung ist **nicht** übersprungen worden.

## Kleine Windows-Korrektur

`Control.Visible` liefert vor dem Anzeigen des Elternfensters `false`, selbst
wenn eine Unteransicht sichtbar werden soll. Dadurch konnten Servereinstellungen
und Raumcode-Bedienelemente beim Öffnen kurz übereinander erscheinen; auch ein
Seitenwechsel wartete bisher auf den 200-ms-Status-Timer.

`frmNano.OnlineRooms.cs` hält die gewünschte Ansicht jetzt ausdrücklich fest und
aktualisiert die Bedienelemente unmittelbar beim Sichtbarkeitswechsel. Drei neue
Windows-Testfälle sichern die Erstansicht, Konfiguration und Fehlerbehandlung ab.
Emulationskerne und das Netzwerkprotokoll wurden nicht verändert.

Der native Runtime-Test prüft jetzt beide Raumprofile. Eine leere optionale
`AETHERBOY_TEST_TURN_URL` wird wie eine fehlende Variable behandelt. Der erste
lokale Testversuch war ausschließlich an dieser leeren Testvariable gescheitert;
nach vollständigem Entfernen funktionierte der unveränderte native Transport.
Das war kein Fehler eurer TURN-Zugangsdaten.

## Lokale Ergebnisse und Wiederholung

Alles liegt separat unter `artifacts/online-rooms-windows-20260913/`:

- `Windows-x64/AetherBoy.exe`: frisch veröffentlichter Windows-Build; immer den
  kompletten Ordner einschließlich nativer Bibliothek und Lizenzen behalten.
- `AetherBoy-Online-Rooms.tar.gz`: CapRover-Upload aus `captain-definition`,
  `Dockerfile` und `server.mjs`, ohne Zugangsdaten.
- `test-results/`: insbesondere `runtime-final.trx`, `windows-smoke-verified.trx`,
  `windows-room-ui-final.trx`, `core.trx` und `desktop.trx`.
- `screenshots/`: Raumdialog und Servereinstellungen mit künstlichen Testwerten.

PowerShell; .NET und Node müssen im Suchpfad liegen:

```powershell
dotnet restore nanoboy.sln --locked-mode --configfile NuGet.config --artifacts-path artifacts/online-rooms-windows-20260913
dotnet build nanoboy.sln -c Release --no-restore --artifacts-path artifacts/online-rooms-windows-20260913 -p:ContinuousIntegrationBuild=true
$env:AETHERBOY_TEST_NATIVE_ONLINE = '1'
Remove-Item Env:AETHERBOY_TEST_TURN_URL -ErrorAction SilentlyContinue
dotnet artifacts/online-rooms-windows-20260913/bin/AetherBoy.RuntimeTests/release/AetherBoy.RuntimeTests.dll --filter 'FullyQualifiedName~OnlineRoomTests' --timeout 60s
dotnet artifacts/online-rooms-windows-20260913/bin/AetherBoy.SmokeTests/release/AetherBoy.SmokeTests.dll --timeout 120s
node --test services/online-rooms/server.test.mjs tests/browser/WebRtcBridge.test.cjs
```

## Noch nicht erledigt: echter Raumdienst und gemeinsamer Test

Die CapRover-Verwaltungsadresse und der Zugang sind noch nicht bekannt; Jessy war
offline. **Kein produktiver Raumdienst wurde von diesem Windows-Rechner deployt
oder konfiguriert.** Das Uploadpaket ist lediglich vorbereitet.

Sobald der Zugriff vorhanden ist, nach [ONLINE_ROOMS_DE.md](ONLINE_ROOMS_DE.md):

1. Vorhandene CapRover-App prüfen bzw. `aetherboy-rooms` anlegen; Paket deployen,
   Container-Port 8080, genau eine Instanz, HTTPS und Force HTTPS konfigurieren.
2. Neuen privaten `ROOM_ACCESS_KEY` sowie die aktuellen TURN/UDP-Zugangsdaten nur
   in der Serverkonfiguration setzen. Früher im Chat geteilte Passwörter ersetzen.
3. `/healthz`, abgewiesenen Zugriff ohne Schlüssel und authentifizierte
   Raumerstellung prüfen. Coturn separat prüfen: 3478/UDP und tatsächlich
   konfigurierter Relay-Portbereich, hier laut Übergabe 49160–49200/UDP.
4. Beide Emulatoren mit identischer HTTPS-Adresse und privatem Raumdienst-Schlüssel
   konfigurieren. Windows als Host/Linux als Gast testen, anschließend Rollen tauschen.
5. Verbindungsabbruch, neuen Raum, falschen Code und belegten Raum prüfen. Erst danach
   den Pokémon-Tausch mit geschützten Sitzungskopien und anschließendem Neuladen testen.

**Grenze dieses Nachweises:** Der lokale Test benutzt direkte ICE-Verbindungen.
Er belegt weder den produktiven TURN-Relay-Datenweg noch Internetverbindungen
zwischen Windows/Linux, vollständiges GBA-Link-Verhalten oder einen Pokémon-Tausch.
Die normale native Benutzerverbindung erzwingt weiterhin TURN/UDP; diese Vorgabe
wurde nicht abgeschwächt. Rocket Edition bleibt außerhalb der bisherigen Freigabe.

## English handoff

Fast-forwarded Windows development to `3fb28ca`, retaining `23c6fda`. A clean,
locked .NET 10.0.302 Release build succeeds. Native libdatachannel peers on Windows
exchange exact 4096-byte payloads and replies, reject oversized packets and close
together for both GB/GBC and Pokémon Gen-3 GBA room profiles. No browser is involved.

Fixed transient overlapping server/room views in the Windows dialog and added
three UI regressions; the runtime integration test now covers both profiles and
tolerates an empty optional test TURN URL. Final results: **870 .NET passed,
47 platform/hardware skips, zero failures; 7 Node tests passed**. Local additions
are not committed or pushed. Real player files and server credentials are untouched.

The Windows folder and credential-free CapRover upload are ready. Production
deployment remains blocked on the administration address/access. Direct local
ICE tests do not validate production TURN/UDP, Windows-to-Linux WAN connectivity
or Pokémon trading. Run the joint acceptance steps above once access is available.
