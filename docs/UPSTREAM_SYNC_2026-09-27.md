# Gemeinsamer Stand / Shared upstream integration — 2026-09-27

## Deutsch

### Tatsächlich auf GitHub gefunden

Am 27. September gegen GitHub geprüft und mit `git fetch origin` übernommen:
`development` war gegenüber unserer lokalen Basis `3fb28ca` genau einen Commit
voraus. Der Liefercommit von xJessyX ist
[`0e8cdb25895202e13ddd07981051af9658e0918a`](https://github.com/VoltexModz/AetherBoy/commit/0e8cdb25895202e13ddd07981051af9658e0918a),
**vom 13. September**, nicht ein neuer Commit vom heutigen Tag.

Er enthält:

- Ein separates Node-Testserver-Modul statt Inline-JavaScript in einer
  Windows-Befehlszeile; Konfiguration über die Prozessumgebung.
- Prüfung der Portmeldung, Lesen von stderr und begrenzte Start-/Ende-Wartezeiten.
- Getrennte Windows-CI-Suiten mit TRX-/Diagnose-Artefakten, Mindesttestzahlen und
  Zeitlimits. Weitere Suiten laufen auch nach einem Testfehler weiter.
- Den Windows-CI-Nachtrag in `ONLINE_ROOMS_WINDOWS_HANDOFF.md`.

Keine zusätzliche Änderung am Emulator-Core, Spielprofil oder produktiven
Raumserver ist in diesem Commit enthalten.

### Zusammenführung mit unserer offenen Arbeit

Die lokale Branch-Basis wurde per Fast-forward auf `0e8cdb2` aktualisiert.
Alle vorherigen lokalen Diagnose-/Dialogänderungen bleiben erhalten. Der
Textkonflikt in `OnlineRoomTests.cs` wurde inhaltlich zusammengeführt:
robuster Prozessstart/-stopp vom Kollegen **plus** unsere drei Profile,
Kandidatenpaar-/Datenschutzprüfungen und bidirektionalen Probe-Nachrichten.
Temporäre Berichte werden auch bei einem Fehler beim Beenden des Servers bereinigt.

Damit liegen in demselben Arbeitsstand:

- Gemeinsame native Diagnose für Windows und Linux: bereinigte Fehlertexte,
  ICE-/Kanalzustände und Typen des tatsächlich ausgewählten Kandidatenpaars.
- Das ROM-freie Werkzeug `AetherBoy.OnlineProbe` und dessen separates Serverprofil
  `transport-probe-v1`; keine Vermischung mit Spielpaketen.
- Die bereits vorbereitete Windows-Raumdialogkorrektur samt UI-Tests.
- Der aktuelle Raumserver, der GB, GBA-Gen3 und Probe-Räume unterscheidet.

Die neue CLI wird jetzt auch in der Linux-CI wiederhergestellt, gebaut und mit
`--help` geprüft. Windows baut sie über die Solution und prüft ebenfalls `--help`.
Beide Dependency-Caches berücksichtigen ihre Lockdatei. Die Mindesttestzahlen
wurden auf Runtime 380, Windows Smoke 198 und portable Desktop-Logik 122 angehoben.

### GitHub-CI: belegter alter Fehler, keine voreilige Grünmeldung

Im [CI-Lauf des Kollegen](https://github.com/VoltexModz/AetherBoy/actions/runs/34755810398)
waren Linux x64 und ARM64 erfolgreich. Unter Windows bestanden Core, Runtime
einschließlich nativem Raumtest sowie portable Desktop-Logik. Die Windows-UI-Suite
wurde beim alten Zwei-Minuten-Limit unvollständig beendet: 183/195 Tests gemeldet,
davon 180 bestanden und 3 übersprungen, keine fehlgeschlagene Assertion.
Bis kurz vor dem Abbruch wurden weitere bestandene Tests gemeldet.

Das UI-Suitenlimit ist deshalb auf vier Minuten mit fünf Minuten äußerem
Schrittlimit angepasst. Es werden keine Tests ausgefiltert und keine Fehler
weggeretryt. Ein erneuter GitHub-Lauf muss zeigen, ob dieses Budget auf dem
Hosted Runner ausreicht; der lokale Erfolg ersetzt diesen Nachweis nicht.

### Frische Prüfung des zusammengeführten Arbeitsstands

Windows x64, .NET SDK 10.0.302, vollständiger Locked Restore und Release-Build:
**0 Warnungen, 0 Fehler**. Alle Suiten nacheinander ausgeführt, mit
`AETHERBOY_TEST_NATIVE_ONLINE=1`, ohne produktive TURN-Konfiguration.

| Prüfung | Bestanden | Übersprungen | Fehlgeschlagen |
| --- | ---: | ---: | ---: |
| Core | 232 | 0 | 0 |
| Runtime | 379 | 1 | 0 |
| Portable Desktop-Logik auf Windows | 80 | 42 | 0 |
| Windows Smoke/UI | 194 | 4 | 0 |
| **.NET gesamt** | **885** | **47** | **0** |
| Node: Raumserver und Browser-Bridge | 8 | 0 | 0 |

Zusätzlich: OnlineProbe `--help` erfolgreich. Der lokale Windows-UI-Lauf dauerte
rund 33 Sekunden, Runtime rund 27 Sekunden. Die Skips betreffen POSIX,
Linux/Wayland/AT-SPI und explizite Hardware-/Vordergrundtests, nicht stillgelegte
Fehlerfälle. Die drei nativen Profiltests waren aktiv und bestanden.

Build und TRX-/Diagnoseberichte:
`artifacts/upstream-sync-20260927/`.

- Windows: `bin/nanoboy/release/AetherBoy.exe`.
- ROM-freier Test: `bin/AetherBoy.OnlineProbe/release/AetherBoy.OnlineProbe.exe`.
- CapRover-Upload: `AetherBoy-Online-Rooms.tar.gz`, enthält ausschließlich
  `captain-definition`, `Dockerfile`, `server.mjs`, keine Zugangsdaten.
- SHA-256 des Uploadpakets:
  `fa6fcaedf0d9ce34b656be52da1d32912edaa73543f1ae08bb43e926104a08b5`.

Die EXE benötigt ihren vollständigen Buildordner und die installierten Runtimes;
dies ist kein neu erstelltes, eigenständig lauffähiges Release-Paket.

### Übergabe / noch nicht nachgewiesen

Die Integration wurde zunächst lokal geprüft; am 27. September hat Voltex
**Commit und Push auf `development` freigegeben**. Dieser Nachtrag begleitet
diese Veröffentlichung. Nach Übernahme des Commits denselben Stand auf Linux
prüfen; ein Server-Deployment ist davon getrennt und nicht erfolgt.
Das alte Raumserverpaket vom 13. September enthält das Probe-Profil noch nicht.

Kein produktiver Server wurde geändert, keine echten ROMs oder Originalspielstände
wurden für diese Prüfung geöffnet. Nicht nachgewiesen: frische native Linux-UI
dieses integrierten Stands, produktives TURN, Windows↔Linux über zwei
Internetanschlüsse oder ein erfolgreicher Pokémon-Tausch. Nach Bereitstellung
des aktuellen HTTPS-Raumdiensts zunächst
[den ROM-freien Verbindungstest](ONLINE_CONNECTION_DIAGNOSTICS.md) auf beiden PCs
ausführen; erst danach GB/GBC und GBA-Gen3 getrennt mit Sitzungskopien testen.

## English

Fetched and fast-forwarded `development` from `3fb28ca` to xJessyX's `0e8cdb2`
(authored September 13). The single upstream commit hardens Windows CI and the
local Node room-server fixture; it does not change emulation or game compatibility.
Preserved the existing local native diagnostics, Windows dialog fixes and the
ROM-free probe. Manually combined the overlapping integration test so fixture
startup/shutdown diagnostics and all three native profile checks remain active.

Added OnlineProbe restore/build/help coverage to Linux CI and help/cache coverage
to Windows CI. Raised test-count gates to match the integrated tree. The upstream
Windows UI run reached its two-minute deadline while still reporting passing
tests; the suite now has a four-minute budget and five-minute outer deadline,
without removing tests or hiding failures. A fresh hosted CI run is still needed.

Fresh local Windows verification: full locked restore and Release build with zero
warnings/errors; 885 .NET tests passed, 47 platform/hardware skips, zero failures;
8 Node tests passed and probe CLI help succeeded. Local native transport tests
covered GB, GBA-Gen3 and probe profiles without production TURN credentials.
The shared Linux frontend compiled, but native Linux execution was not repeated.

Current build, test reports and a credential-free CapRover archive are under
`artifacts/upstream-sync-20260927/`. Voltex approved publishing this integration
to `development` on September 27; this handoff accompanies that publication.
After pulling the integration commit, repeat native Linux validation. No
production deployment, real-ROM test or cross-platform WAN/trade verification
was performed.
