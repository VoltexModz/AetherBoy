# Übergabe an Voltex: Native Online-Räume unter Windows

Neuer Windows-Einstieg: [Verbindung testen direkt im Emulator, Runtime-API und
Linux-UI-Übergabe (DE/EN)](CONNECTION_TEST_UI_HANDOFF.md).

Aktueller Abgleich 27. September: [Kollegen-Commit, erhaltene lokale Änderungen,
frische Windows-Prüfung und offene WAN-Abnahme (DE/EN)](UPSTREAM_SYNC_2026-09-27.md).

Stand: 2026-09-13. **Die Windows-Implementierung ist bereits enthalten.** Den
vollständigen Featurecommit übernehmen, bauen und unter Windows prüfen. Eine
separate Portierung der Raumverbindung ist nicht nötig. Dieser Stand ersetzt
den Browser als primären Verbindungsweg, nicht die vorhandenen Kabelprofile.

Nachfolgende native Windows-Prüfung: [Ergebnisse, kleine Dialogkorrektur und
noch offene Serverabnahme](ONLINE_ROOMS_WINDOWS_VALIDATION.md).

## Zusammenführung mit dem neuen Windows-Commit

`23c6fda11b3dd1a735494dc587951ff27d67d2e7` von Voltex
(`fix(windows): retain WebRTC diagnostics and retire completed online sessions`)
ist per Fast-forward übernommen. Die Raumcode-Änderungen liegen darauf auf;
Voltex' Commit muss nicht noch einmal cherry-picked werden.

- `WebRtcBrowserFailure` und seine geprüften Browser-Fehlerkategorien bleiben erhalten.
- `FinishStoppedOnlineLink()` läuft weiterhin vor Eingaben und Frame-Updates.
- Der Hotkey-Konflikt ist kombiniert: zuerst beendete Sessions aufräumen, dann den
  neuen nativen Raumdialog öffnen.
- Die nach Session-Ende abrufbare Windows-Diagnose erhält im nativen Modus den
  Raumfehler und verweist auf neue Raumcodes. Der manuelle Modus behält seine
  Browserdiagnose und Einladung/Antwort-Anleitung.
- Die CI-Untergrenze aus Voltex' Commit bleibt erhalten; Node-Raumtests und native
  Verbindungsprüfungen kommen hinzu.
- Das von Voltex dokumentierte Linux-Paketproblem ist im Raumcode-Paket behoben:
  `.html`/`.js`-Ressourcen und die native Bibliothek gelangen in den Build.

## Verhalten für Spieler

Tools → Online Link → Sitzung erstellen / beitreten öffnet den nativen
Raumdialog. Strg+F10 / Strg+Umschalt+F10 öffnen denselben Dialog.

Einmal HTTPS-Adresse und Zugangsschlüssel des privaten Raumdiensts speichern.
Danach eigenes unterstütztes Spiel öffnen, geschützte Spielstandkopie bestätigen,
Raum erstellen oder einen Code wie `ABCDE-FGHJK` eingeben. Der Code hat zehn
Zeichen ohne Bindestrich. TURN-Daten kommen automatisch vom Raumdienst.

Das Schließen des Raumdialogs über „Zum Spiel“ beendet die Verbindung nicht.
„Verbindung beenden“ beendet die Sitzung. Die Emulation wird beim Online-Start
mit einer eigenen Spielstandkopie neu geöffnet; Originalstände werden nicht
automatisch ersetzt. Der manuelle Browsermodus bleibt über die entsprechenden
Host-/Gast-Menüeinträge verfügbar.

## Welche Dateien zusammengehören

| Bereich | Dateien / Aufgabe |
|---|---|
| Windows-Dialog | `nanoboy/frmNano.OnlineRooms.cs`: Raumcode, maskierter Schlüssel, Servereinstellungen, Status und Abbruch |
| Windows-Einbindung | `frmNano.OnlineLink.cs`, `frmNano.AetherUi.cs`, `frmNano.cs`: Menü, Hotkeys, Transportwahl und Aufräumen |
| Gemeinsamer Transport | `nanoboy/Runtime/Netplay/OnlineRoomTransport.cs`: HTTPS-Signalisierung, Verbindungsaufbau und begrenzte Paketwarteschlangen |
| Native WebRTC-Anbindung | `NativeRtcPeer.cs`: libdatachannel-C-ABI, zuverlässiger geordneter Datenkanal, Callback-Lebensdauer |
| Einstellungen | `OnlineRoomSettings.cs`: Adress-/Codeprüfung und lokale Speicherung |
| Raumdienst | `services/online-rooms/`: Node-Service, Dockerfile, CapRover-Definition und Tests |
| Build / Lizenzen | `AetherBoy.Runtime.csproj`, geänderte `packages.lock.json`, `third_party/online-native-licenses/`, `THIRD_PARTY_NOTICES.md` |
| Linux-Gegenstelle | `WaylandEmulatorHost.OnlineRooms.cs` und zugehörige UI-/Build-Änderungen |

Neue Dateien und Lockfiles mit übernehmen. Binäre Dateien aus `artifacts/` sind
ignoriert und werden nicht committed. `StartOnlineLink(bool, bool)` bleibt für
den manuellen Browserweg und die bestehenden Tests erhalten. Der native Dialog
wählt den neuen Transport; die bestehende Session-Factory, Protokolle und
Spielstandübernahme bleiben gemeinsam genutzt.

## Windows bauen und prüfen

Voraussetzungen: Windows x64, .NET-10-SDK gemäß `global.json`, Node.js 24 für die
Raumtests. Für die native DLL wird außerdem die aktuelle
[Microsoft Visual C++ v14 Redistributable x64](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist)
benötigt. Das Test-ZIP setzt die .NET 10 Desktop Runtime voraus.

PowerShell im Repository:

```powershell
dotnet restore .\nanoboy.sln --locked-mode --configfile .\NuGet.config
dotnet build .\nanoboy.sln -c Release --no-restore
$env:AETHERBOY_TEST_NATIVE_ONLINE = '1'
dotnet test --project .\tests\AetherBoy.RuntimeTests\AetherBoy.RuntimeTests.csproj -c Release --no-build --no-restore
node --test tests/browser/WebRtcBridge.test.cjs services/online-rooms/server.test.mjs
dotnet test --project .\tests\AetherBoy.SmokeTests\AetherBoy.SmokeTests.csproj -c Release --no-build --no-restore
```

`MediaToolkit.WebRtc.Native.win-x64` **0.24.5.1** liefert libdatachannel **0.24.5**.
`datachannel.dll` muss neben der Anwendung bzw. Testassembly liegen; die Runtime-
Projektdatei kopiert sie auch in Publish-Ausgaben. Nicht nur `AetherBoy.exe`
weitergeben. Lizenzverzeichnis ebenfalls mitliefern. Windows ARM64/x86 ist mit
diesem Paket nicht abgedeckt.

Der native Integrationstest startet selbst einen temporären lokalen Raumdienst
und verbindet zwei native Teilnehmer. Dafür sind keine echten Serverzugänge
nötig. `AETHERBOY_TEST_TURN_URL` nur für den ausdrücklich beschriebenen isolierten
TURN-Test setzen, nicht für den Produktionsserver.

## Server und gemeinsamer Internet-Test

Die zusätzliche Raumdienst-App ist **vorbereitet, aber in diesem Arbeitsstand
nicht auf dem echten CapRover deployt oder geprüft**. Coturn ist eine separate,
bereits vom Nutzer eingerichtete App. Einrichtung und genaue ENV-Werte stehen in
[ONLINE_ROOMS_DE.md](ONLINE_ROOMS_DE.md). Beide Emulatoren benötigen dieselbe
HTTPS-Adresse und denselben `ROOM_ACCESS_KEY`, einmalig in den Servereinstellungen.

Die native Bibliothek nutzt **TURN/UDP** und erzwingt Relay-Verbindungen. TURN/TCP
und TURN/TLS sind in diesem Build nicht unterstützt; dafür bleibt der Browserweg.
Der Raumdienst läuft mit einer Instanz, hält Räume nur im Speicher und lässt
wartende Räume nach zehn Minuten verfallen. Eine bestehende WebRTC-Verbindung
braucht anschließend keine weiteren Raumabfragen. Er ist für eine private Gruppe;
authentifizierte Mitglieder können die konfigurierten TURN-Zugangsdaten abrufen.

Abnahme unter Windows und danach mit Jessys Linux-Rechner:

1. Server speichern, Dialog und Emulator neu öffnen: Adresse und Schlüssel bleiben
   erhalten, Schlüssel bleibt verdeckt. Ohne geeignetes Spiel keine Sitzung starten.
2. Windows als Host, Linux als Gast; anschließend Rollen tauschen. Raumcode kopieren,
   beitreten, Verbindungsstatus prüfen und über „Zum Spiel“ weiterspielen.
3. Falschen Code, falschen Zugangsschlüssel, belegten/abgelaufenen Raum und einen
   Abbruch während der Verbindungssuche prüfen. Danach neue Sitzung starten können.
4. Dialog während einer verbundenen Sitzung schließen und wieder öffnen. Verbindung
   muss erhalten bleiben. Explizites Beenden und Peer-Abbruch müssen sauber aufräumen.
5. Unter Windows Fokus/Pause, Tastatur, Controller und DPI-Skalierung prüfen.
6. Erst anschließend mit unterstützten Spielen die eigentliche Kabelfunktion testen.
   Nach einem Tausch beide Sitzungskopien erneut laden und das Ergebnis prüfen, bevor
   jemand die Kopie bewusst als normalen Spielstand übernimmt.

Bei Fehlern Buildstand, Betriebssystem, Host-/Gastrolle, Spielprofil, genaue
Statusmeldung und letzten erfolgreichen Schritt festhalten. Zugangsschlüssel,
TURN-Passwörter und vollständige Verbindungsdaten nicht in den Handoff kopieren.

## Nachtrag: erster GitHub-Windows-Lauf von `3fb28ca`

[CI-Lauf 34727729334](https://github.com/VoltexModz/AetherBoy/actions/runs/34727729334):
Beide Linux-Jobs (x64 und ARM64) erfolgreich; Windows-Restore und -Build erfolgreich.
Der Windows-Job wurde nach dem 15-Minuten-Limit abgebrochen. Er ist nicht grün.

Die Runtime meldete vorher einen fehlgeschlagenen Test:
`NativeRoomsExchangePacketsWithoutBrowserAndCloseTogether` wartete zehn Sekunden
auf die Portausgabe des lokalen Node-Testservers (`OnlineRoomTests.cs:56` im Commit).
Der Timeout trat vor dem Erstellen einer Peer-Verbindung auf. Der damalige Test
leitete stderr zwar um, las es aber nicht; die Ursache des fehlenden Startsignals
ist daher nicht belegt. Die Windows-Smoke-Suite blieb anschließend ohne Abschluss.
Welcher ihrer Tests hing, ist im damaligen Log nicht erkennbar. Der ältere,
bekannte Local-Link-Schließhänger ist nur ein möglicher Zusammenhang.

Vorbereitete Änderungen für den nächsten Commit:

- Node-Testserver als eigene `.mjs`-Datei statt Inline-JavaScript starten;
  Testkonfiguration über die Prozessumgebung, stdin ausdrücklich umleiten.
- Portmeldung prüfen, stderr lesen und bei Startfehlern mit Exitcode ausgeben;
  Start und Prozessende zeitlich begrenzen.
- Windows-Suiten separat und nacheinander ausführen. Jede erhält ein Testlimit von
  zwei Minuten, ein äußeres Schrittlimit von drei Minuten, ausführliche Ausgabe
  und Diagnose-/TRX-Dateien im Artifact `windows-test-diagnostics`.
- Die übrigen Suiten laufen auch nach einem Testfehler, sofern der Build erfolgreich
  war. Fehler bleiben Fehler; kein automatisches Wegfiltern oder grün gewerteter Retry.

Die Änderungen sind auf Linux geprüft. Ein erfolgreicher neuer Windows-CI-Lauf
muss nach Commit/Push noch erfolgen; weder der Serverstart-Timeout noch der
Windows-UI-Hänger werden damit bereits als unter Windows behoben behauptet.

## Tatsächlich geprüft

Nach Integration von `23c6fda` erneut geprüft: gesamter Release-Build einschließlich
Windows-Projekt **0 Warnungen / 0 Fehler**, Runtime **364/364**, Node **7/7**.
Der aktualisierte Linux-Build wurde erneut installiert. Der zusätzliche
Windows-Test `NativeRoomFailureRetainsActionableReasonWithoutBrowserFallback`
wurde mitkompiliert; seine native Windows-Ausführung steht noch aus.
Die folgenden weitergehenden UI-/TURN-/Paketprüfungen stammen vom Raumcode-Stand
vor diesem Integrationsschritt, soweit nicht ausdrücklich erneut genannt.

- Gesamte Lösung auf Linux für Linux/Windows kompiliert: **0 Fehler, 0 Warnungen**.
  Zusätzlich Windows-x64-Publish erstellt. **Keine native Windows-Ausführung**
  dieses neuen Raumdialogs in diesem Arbeitsstand.
- Runtime unter Linux: **364 bestanden, 0 übersprungen, 0 fehlgeschlagen**.
- Desktop unter Wayland: **113 bestanden, 9 übersprungen, 0 fehlgeschlagen**;
  die übersprungenen Fälle benötigen weitere Accessibility-/Audio-Opt-ins.
  Neuer Raumdialog einschließlich maskiertem Schlüssel visuell geprüft; gezielter
  UI-Test nach der Layoutkorrektur nochmals bestanden.
- Raumdienst und bestehende Browser-Callbacks: **7 Node-Tests bestanden**.
- Zwei native Teilnehmer haben lokal 4096 Bytes sowie Antwortdaten übertragen und
  gemeinsam geschlossen; auch über einen isolierten Coturn mit TURN/UDP bestanden.
- Docker-Image gebaut; `/healthz` erfolgreich, Raumzugriff ohne Schlüssel abgewiesen.
- Linux-Installation und portables Paket einschließlich nativer Bibliothek,
  Quellarchiv, Lizenzen und Startprüfung bestanden.

**Noch offen:** neuer nativer Windows↔Linux-Raumweg über zwei Internetanschlüsse,
Windows-UI-Abnahme und ein nachweislich erfolgreicher Pokémon-Tausch. Die früheren
Browser-/WSL-Tests belegen diesen neuen nativen Windows-Weg nicht. Rocket Edition
ist durch diese Änderung nicht allgemein freigegeben; bestehende experimentelle
GB/GBC- und Pokémon-Gen3-GBA-Profile sowie Spielstandschutz gelten weiter.

## Ergänzung 15. September 2026 / September 15 update

Gemeinsame native Diagnose und ROM-freier Verbindungstest:
[ONLINE_CONNECTION_DIAGNOSTICS.md](ONLINE_CONNECTION_DIAGNOSTICS.md).
Das neue Testprofil `transport-probe-v1` erfordert einen aktualisierten Raumdienst.
Kein Server-Deployment und kein WAN-/Pokémon-Tauschnachweis durch diese Änderung.

Windows-Abnahme: frischer kompletter Release-Build 0 Warnungen/0 Fehler;
Core 232/232, Runtime 379 bestanden/1 POSIX-Skip, Windows Smoke 194 bestanden/4
Hardware-/Vordergrund-Skips, Desktop 80 bestanden/42 Linux-/Hardware-Skips.
Gesamt 885 bestanden, 47 übersprungen, 0 Testfehler. Node 8/8 bestanden.
Die 24 gezielten Online-/Diagnosetests wurden nach der letzten Runtime-Änderung
erneut ausgeführt, einschließlich drei echter lokaler nativer Profilverbindungen,
Kandidaten-Typerkennung und bidirektionalem Probe-Lauf. Der erste vollständige
Windows-Testlauf erreichte das zu kurze 60-Sekunden-Limit; der Wiederholungslauf
mit 180-Sekunden-Limit war nach rund 68 Sekunden vollständig erfolgreich.

Frischer Build und TRX-Berichte: `artifacts/online-diagnostics-20260915/`.
Windows-Start: `bin/nanoboy/release/AetherBoy.exe`; ROM-freies Werkzeug:
`bin/AetherBoy.OnlineProbe/release/AetherBoy.OnlineProbe.exe` mit `host`/`join`.
Keine echte ROM, kein Originalspielstand und kein Server wurde verändert.

English: the shared diagnostics and isolated probe are implemented and tested
locally on native Windows. Linux frontend compilation passed, but native Linux
UI execution, the production TURN path and Windows-to-Linux WAN remain unverified.
Existing recording opt-outs are respected. The probe needs the updated room
service; game profiles and saved-game protection were not changed.
