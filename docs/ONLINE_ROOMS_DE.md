# Online Link mit Raumcodes

Entwickler-Übergabe für Windows: [Build, Integration und Abnahme](ONLINE_ROOMS_WINDOWS_HANDOFF.md).

Beide Spieler können im Emulator einen Raum erstellen oder einen kurzen Code
wie `ABCDE-FGHJK` eingeben. Der Browser und das manuelle Austauschen von
Verbindungsdaten entfallen. Linux und Windows verwenden denselben Raumdienst,
dieselbe native WebRTC-Bibliothek und dieselben Kabelprofile.

## Einmal auf CapRover einrichten

Coturn läuft weiter wie bisher. **Zusätzlich** eine normale CapRover-App
`aetherboy-rooms` anlegen. Der Raumdienst ordnet Codes den zwei Teilnehmern zu;
Coturn überträgt weiterhin die verschlüsselten WebRTC-Pakete.

1. Das bereitgestellte `AetherBoy-Online-Rooms.tar.gz` unter **Deployment →
   Deploy via Tarball** hochladen. Alternativ im Repository:
   `tar -czf AetherBoy-Online-Rooms.tar.gz -C services/online-rooms captain-definition Dockerfile server.mjs`.
2. Unter **App Configs → Environmental Variables** diese Werte setzen:

   | Variable | Wert |
   |---|---|
   | `PORT` | `8080` |
   | `ROOM_ACCESS_KEY` | Neuer zufälliger Schlüssel, z. B. 64 Hex-Zeichen aus `openssl rand -hex 32` |
   | `TURN_URL` | `turn:217.172.182.79:3478?transport=udp` |
   | `TURN_USER` | Derselbe Benutzername wie in deiner Coturn-App |
   | `TURN_PASSWORD` | Dasselbe Passwort wie in deiner Coturn-App |

   Der **ROOM_ACCESS_KEY ist ein neuer Schlüssel für den Raumdienst**.
   Das TURN-Passwort gehört nur in die Serverkonfiguration. Keinen Schlüssel in
   Git eintragen. Emulator-Benutzer mit dem Raumdienst-Schlüssel können die
   TURN-Zugangsdaten technisch abrufen; der Dienst ist für eine private Gruppe.
3. Unter **HTTP Settings** den **Container HTTP Port auf 8080** setzen.
   Eine Domain zuweisen, HTTPS aktivieren und **Force HTTPS** einschalten.
   Beispiel: `https://aetherboy-rooms.deine-caprover-domain.de`.
   Keine zusätzlichen Host-Port-Zuordnungen für diese App nötig.
4. **Genau eine Instanz** laufen lassen. Räume liegen im Arbeitsspeicher und
   verfallen nach zehn Minuten. Neustarts löschen wartende Räume.
5. `https://DEINE-DOMAIN/healthz` im Browser aufrufen. Erwartet:
   `{"status":"ok","protocol":1}`. Die eigentlichen Raum-APIs benötigen den Schlüssel.

Die bestehende Coturn-Konfiguration muss weiterhin 3478/UDP und den konfigurierten
Relay-Portbereich (bei unserem Aufbau 49160–49200/UDP) erreichbar machen.
Die native Bibliothek in diesem Build unterstützt **TURN über UDP**. Für Netze,
die ausschließlich TURN/TCP oder TURN/TLS erlauben, bleibt der manuelle
Browsermodus verfügbar.

## Einmal in jedem Emulator eintragen

- **Linux:** Online Link (F10) → **Server settings**.
- **Windows:** Tools → Online Link → **Sitzung erstellen / beitreten** →
  **Server einstellen**; auch Strg+F10 / Strg+Umschalt+F10.

Die HTTPS-Adresse des Raumdiensts und dessen `ROOM_ACCESS_KEY` eingeben und
speichern. Beide benötigen dieselbe Adresse und denselben Schlüssel. Der
Raumcode ist davon getrennt: Er wird für jede neue Sitzung automatisch erstellt.

Die Einstellungen liegen unter Linux in `$XDG_CONFIG_HOME/aetherboy/online-room.json`
(standardmäßig `~/.config/aetherboy/online-room.json`, nur für den Benutzer lesbar)
und unter Windows im AetherBoy-Benutzerdatenordner `Settings/online-room.json`.
Der Schlüssel wird lokal gespeichert; das Eingabefeld ist verdeckt.

## Danach gemeinsam spielen

1. Beide öffnen ihr eigenes unterstütztes Spiel.
2. Die geschützte Spielstandkopie bestätigen.
3. Einer wählt **Create room / Raum erstellen** und teilt den angezeigten Code.
4. Der andere gibt den Code ein und wählt **Join room / Raum beitreten**.
5. Sobald **Connected** erscheint, zum Spiel zurückkehren. Der andere Emulator
   muss geöffnet bleiben. Kein Browserfenster nötig.

Beim Wechsel wird die Emulation mit einer **separaten Spielstandkopie neu
geöffnet**. Der Raumdialog zeigt dabei den Verbindungsaufbau. Nach dem Test
Sitzungskopien prüfen und erst dann bewusst übernehmen. Die Netzwerkverbindung
allein bestätigt keinen erfolgreichen Pokémon-Tausch. Die bestehenden
experimentellen GB/GBC- und Pokémon-Gen3-GBA-Profile gelten weiterhin;
insbesondere ist damit keine allgemeine Freigabe für Rocket Edition verbunden.

## Entwicklung und Prüfung

Linux benötigt zum Bauen zusätzlich Git, CMake, einen C/C++-Compiler und OpenSSL-3-
Entwicklungsdateien (`build-essential cmake libssl-dev` auf Ubuntu).
`bash scripts/build-linux.sh` baut die native Bibliothek automatisch.
Vor direktem `dotnet build` auf Linux zuerst `bash scripts/build-online-native.sh`.
Windows x64 erhält die Bibliothek aus dem gesperrten NuGet-Paket. Zum Ausführen
sind .NET 10 Desktop Runtime und die aktuelle
[Microsoft Visual C++ v14 Redistributable (x64)](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist)
erforderlich.

- Raum-API: `node --test services/online-rooms/server.test.mjs`
- Native lokale Verbindung: `AETHERBOY_TEST_NATIVE_ONLINE=1 dotnet test --project tests/AetherBoy.RuntimeTests/AetherBoy.RuntimeTests.csproj -c Release --filter 'FullyQualifiedName~OnlineRoomTests'`
- Derselbe Test kann mit `AETHERBOY_TEST_TURN_URL` TURN erzwingen. Dafür ausschließlich
  einen isolierten Testserver mit Benutzer `roomtest` und Passwort
  `test-password-local-only` verwenden. Keine Produktionszugangsdaten einsetzen.

Lokal sind native Paketübertragung, Schließen beider Teilnehmer sowie TURN/UDP
mit einem isolierten Coturn geprüft. Ein echter Linux/Windows-Test über zwei
Internetanschlüsse und ein erfolgreicher Pokémon-Tausch stehen noch aus.
