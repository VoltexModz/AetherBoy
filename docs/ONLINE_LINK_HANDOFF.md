# Online Link: GB/GBC development prototype / Entwicklungsprototyp

Status: 2026-09-12. **Not a verified Pokémon trading release.**

Neuer Folgestand / newer follow-up: [GBA Gen3 online development profile and shared save recovery (DE/EN)](GBA_ONLINE_HANDOFF.md).
Der nachfolgende GB/GBC-Bericht dokumentiert den vorherigen Meilenstein; Aussagen über
„noch kein GBA online“ und Prüfzahlen gelten für diesen historischen Stand.
The GB/GBC v1 wire contract below remains valid; GBA uses a separate v2 profile.

## Deutsch

### Abschließender Prüfstand

Release-Build der gesamten Lösung am 2026-09-12: **0 Warnungen, 0 Fehler**. Anschließender Gesamttest: **787 Tests, 749 bestanden, 38 übersprungen, 0 fehlgeschlagen**. Die übersprungenen Fälle benötigen zusätzliche native Plattform-/Hardwarebedingungen; darunter Wayland unter Linux und ein Windows-Tastaturtest mit freigegebenem Vordergrundfokus. Das ist keine vollständige native Linux-UI-Abnahme.

Der erste Gesamtlauf wurde durch einen gefundenen Fehler im bestehenden lokalen Linkfenster unterbrochen: Eine erneute UI-Nachrichtenverarbeitung konnte während der Bildaktualisierung die Sitzung schließen. Der Zugriff auf die danach fehlende Sitzung ist jetzt abgesichert und durch einen gezielten Regressionstest abgedeckt. Erst der erneute, vollständig abgeschlossene Lauf zählt als erfolgreicher Gesamttest.

Zusätzlich bestanden Core (232/232) und Runtime (256/256) unter Ubuntu WSL. Echte WebRTC-Verbindungen zwischen getrennten Windows-/Ubuntu-Prozessen übertrugen mit selbst erzeugten GB- und GBC-Testprogrammen jeweils 16 Kabelbytes pro Seite. Beide Originalspielstände blieben unverändert. Das prüft die gemeinsame technische Strecke, **noch keinen Pokémon-Tausch über zwei Internetanschlüsse**.

### Was eingebaut ist

Ein eigenes C#-Netzwerkkabel für zwei unabhängige GB/GBC-Emulatoren. Jeder Rechner lädt nur seine eigene ROM und seinen eigenen lokalen Spielstand. Das Netzwerk transportiert einen Versions-/Rollen-Handshake, kurze Kabelnachrichten und Lebenszeichen; weder ROM-Dateien noch vollständige Spielstände. Trainer-/Pokémon-Informationen können selbstverständlich Teil der vom Spiel beabsichtigten Kabeldaten sein.

Die gemeinsame Runtime ist plattformneutral. Windows und der native Wayland-Frontend verwenden dieselbe Implementierung. Eine lokale Browserseite stellt WebRTC bereit, während Spiel, Grafik, Eingabe und Audio im normalen Emulatorfenster bleiben. Es gibt keine neue native Netzwerkbibliothek, kein Browser-Plugin, keine Audio-/Kameraübertragung und keinen eingebauten Cloud-Dienst. Die Seite ist eine bewusst vorläufige Verbindungsoberfläche, keine fertig integrierte native Lobby.

### Ausprobieren

1. Beide verwenden denselben AetherBoy-Protokollstand und öffnen ihr eigenes **kompatibles GB/GBC-Spiel**. Ein beliebiges anderes Spiel oder zwei beliebige Editionen werden durch die Verbindung nicht automatisch kompatibel.
2. Windows: **TOOLS → Online Link → Sitzung erstellen / beitreten**, alternativ **Strg+F10 / Strg+Umschalt+F10**. Linux: **TOOLS → Online Link**, alternativ **F10** zur Online-Link-Seite; dort Host bzw. Guest wählen. Genau einen Host und einen Gast verwenden.
3. Der Emulator startet das eigene Spiel mit einer privaten Sitzungskopie neu. Im Browser erstellt der Host eine **Einladung**. Der Gast fügt sie ein und erstellt eine **Antwort**. Der Host fügt diese Antwort ein und übernimmt sie. Die Texte nur über euren vertrauenswürdigen Chat austauschen; **nicht die lokale Browseradresse teilen**.
4. Nach „Verbunden“ beide Browserseiten geöffnet lassen und zu den Emulatorfenstern zurückkehren. Gegebenenfalls Pause/Einstellungen schließen. Erst dann im Spiel die passenden Kabelräume betreten.
5. Im Spiel speichern. **Online-Spielstände öffnen / OPEN ONLINE SAVE FOLDER** zeigt die lokale Sitzungskopie. Sie wird niemals automatisch über das Original geschrieben. Für diesen ersten Prototyp ist die kontrollierte Übernahme nach einem geprüften Tausch ein separater manueller Schritt.

Sitzungsverzeichnisse:

- Windows: `%LOCALAPPDATA%/AetherBoy/development/OnlineLink/<UTC-Zeit-GUID>/game.sav`
- Linux: `${XDG_STATE_HOME:-~/.local/state}/aetherboy/online-link/<UTC-Zeit-GUID>/game.sav`

Die zugehörige RTC-/Backup-/Schutzdateifamilie wird lokal mitkopiert. Das Quellspiel muss vorher sauber beendet werden. Es bleibt exklusiv gesperrt; bei Fehlern wird keine neue Online-Sitzung in ein vorhandenes Sitzungsverzeichnis geschrieben. Auch nach einem Netzfehler bleibt die Arbeitskopie erhalten. Eine solche Kopie beweist nicht, dass beide Spiele einen Tausch vollständig und dauerhaft gespeichert haben.

### Internet, Router und Sicherheit

- WebRTC verwendet einen zuverlässigen, geordneten, verschlüsselten Datenkanal. Das allein ist keine Benutzerkonten- oder Freundesverwaltung. Die Partnerzuordnung hängt vom authentischen Austausch von Einladung und Antwort ab; die darin enthaltenen Zertifikatfingerabdrücke gehören zur WebRTC-Verbindung.
- Der lokale Helfer lauscht ausschließlich an einer zufälligen `127.0.0.1`-Adresse. Ein zufälliges 256-Bit-Geheimnis, exakte Host-/Origin-Prüfung, ein einzelner Browser pro Sitzung, Größenlimits und begrenzte Warteschlangen schützen die lokale Brücke. Keine offenen LAN-Listener, keine permissiven CORS-Regeln, keine externen Skripte.
- Das lokale Geheimnis ist zunächst im URL-Fragment und wird aus der sichtbaren URL entfernt. Es gehört nicht in Logs, Screenshots, Einladungen oder Fehlermeldungen. Einladung/Antwort können dagegen Netzwerkadressen enthalten: nur dem gewünschten Mitspieler geben.
- **Ohne STUN/TURN ist eine Verbindung über beliebige Internetrouter nicht garantiert.** Standardmäßig wird kein solcher öffentlicher Server kontaktiert. Eigene STUN-/TURN-Adressen und gegebenenfalls TURN-Zugangsdaten können auf der Seite nach ausdrücklicher Bestätigung eingetragen werden. Für reine Relay-Verbindungen muss ein funktionierender TURN-Server vorhanden sein.
- Copy-and-paste ersetzt einen Signalisierungsdienst, nicht automatisch die für NAT-Durchquerung nötige Infrastruktur. Eine eigene Lobby, verwaltete Relay-Zugänge und deren Betrieb sind noch nicht implementiert. Keine Router- oder Firewallregeln werden automatisch verändert.
- Wird die Browserseite geschlossen, der Peer getrennt, eine Nachricht ungültig oder eine Grenze überschritten, endet diese Sitzung. Neue Verbindung bedeutet neue Sitzung, neues Geheimnis und neue Transfer-IDs.

### Protokoll und Zeitsteuerung

Der Wire-Handshake prüft Version 1, GB/GBC-Systemfamilie und entgegengesetzte Host-/Gastrollen. Eine zufällige 128-Bit-Kennung je Sender und streng monotone Nachrichtennummern verhindern die Verwechslung von alten Sitzungen/Nachrichten. Mehrbytewerte sind explizit little-endian. Zulässige Nachrichten haben feste Längen: Handshake 24, Lebenszeichen 32, Kabelnachricht 56 Byte.

Die eigene Kabel-Zustandsmaschine verwendet `Offer`, `Ready`, `Complete` und `Cancel` mit beiden Transfer-IDs. Beide Seiten müssen ihren Port aktiviert haben; mindestens eine muss einen internen Takt anbieten. Bei konkurrierendem internen Takt gewinnt der Netzwerkhost als ausdrücklich definierte Prototypregel. Ein angenommener Transfer schiebt tatsächlich acht Bits mit dem gewählten emulierten GB-/CGB-Takt; der Interrupt ist nicht bloß eine sofortige Paketantwort.

Beim Warten stoppt die Runtime zwischen Instruktionen die **gesamte emulierte Zeit**. UI, Abbruch und Netzwerk bleiben bedienbar. Fertige Bytes haben eine beidseitige Abschlussbarriere. Es werden weder ein fehlender Frame gezählt noch erfundene Erfolgsbytes eingespeist. Lebenszeichen werden auch bei UI-Pause bearbeitet. Fehlendes Peer-Lebenszeichen nach 30 Sekunden bzw. nicht abgeschlossene Transferbereitschaft nach zwei Minuten beendet die Sitzung mit Fehler. Die Browser-Verbindungssuche hat zusätzlich eine eigene Grenze.

Unilaterales Laden, Rewind, Turbo, Reset und Cheats sind gesperrt. Normale Singleplayer-Sitzungen und das lokale GB/GBC/GBA Link Lab behalten ihren bisherigen Weg.

Lange UI-Pausen während eines bereits offenen Transfers können trotz funktionierender Lebenszeichen die Zwei-Minuten-Grenze erreichen. In diesem Fall wird abgebrochen; es gibt keine Zusage, einen begonnenen Tausch beliebig lange pausieren zu können.

### Grenzen – wichtig vor Spieltests

- **Paired-transfer-Prototyp, keine universelle Hardware-Netzwerksimulation.** Ein interner Probe-Transfer ohne aktivierten Peer wartet. Er bekommt nicht automatisch ein simuliertes `FF` wie an einem abgezogenen Kabel. Spiele, deren Bereitschaftsprotokoll darauf angewiesen ist, können weitere Arbeit benötigen.
- Änderungen an SB/SC mitten in einem vereinbarten Byte sind noch nicht unterstützt und führen zu einem erklärten Abbruch. Solche Fehler nicht durch falsche Daten kaschieren.
- Mehrere Nachrichtenrunden pro Byte machen hohe Latenzen spürbar. Lange Tauschblöcke können sehr langsam sein. Keine Aussage zu „ruckelfreien Kämpfen“.
- Die Verbindung ist nicht mit DoubleCherry, RetroArch, mGBA oder anderen Emulatoren wire-kompatibel.
- GBA-SIO besitzt andere Modi und braucht einen eigenen Netzwerkadapter. Insbesondere ist Multiplayer 16 Bit je Teilnehmer; normaler serieller GBA-Modus hat eigene 8-/32-Bit-Transfers. Keine Dummy-Antworten als vermeintliche GBA-Lösung.
- Ein erfolgreicher synthetischer Bytetransfer ist kein erfolgreich getesteter Pokémon-Tausch. Windows↔Linux, reale Internetrouter, Paketverlust/Relay und konkrete Spiele sind getrennt zu verifizieren.

### Referenz und Provenienz

DoubleCherryGB wurde nach Nutzerauftrag vollständig als Archiv heruntergeladen und im für Netzwerkkabel relevanten Pfad untersucht. Referenzstand und genaue Ergebnisse stehen in [DOUBLECHERRY_NETWORK_REFERENCE.md](DOUBLECHERRY_NETWORK_REFERENCE.md). Der Quellcode ist nur unter `.local-tools/` abgelegt, nicht Teil des Builds/Vertriebs. Keine Implementierung wurde kopiert oder durch Umbenennen als eigene ausgegeben. Unser C#-Protokoll und die Browserbrücke sind eigenständige Implementierungen; bestehende Fremdkern-Lizenzhinweise bleiben unverändert.

## English

### Verified on 2026-09-12

- Full Release solution build: **0 warnings, 0 errors**. Final full solution test run: **787 total, 749 passed, 38 skipped, 0 failed**, exit code 0. Platform/hardware-dependent skips include native Linux/Wayland checks and a Windows foreground-keyboard test. The final build was followed by `dotnet test --solution nanoboy.sln --configuration Release --no-build --no-restore --minimum-expected-tests 787`, with Windows hardware smoke enabled.
- The first full run was interrupted by a newly found local Link Lab close/refresh reentrancy bug. The owner-reference lifetime is now guarded and covered by `RefreshToleratesOwnerRetiringDuringAControlUpdate`. Only the completed rerun above is counted as the successful full-suite result.
- Core: **232/232** passed on Windows and again in Ubuntu WSL.
- Runtime: **256/256** passed on Windows and again in Ubuntu WSL; neither run skipped tests. Includes **35 new core cases** and **36 new runtime cases** compared with the preceding local-link milestone.
- WSL used its existing .NET **10.0.12** runtime with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` because that test environment lacks ICU. No Linux system packages were installed. This is not a native Wayland UI qualification.
- Two actual Chrome WebRTC data channels transferred **4096 bytes** and a **50-byte** return message through both native loopback bridges, without STUN/TURN.
- **Separate native Windows and Ubuntu-WSL processes** then ran independently generated GB programs through the actual browser WebRTC transport: **16 serial transfers per side**, verified `70..7F` / `20..2F` in their private battery saves. Both original saves remained unchanged.
- The same cross-OS test passed for **GBC with host CPU double speed and fast serial clock**: 16 transfers per side, both working saves byte-verified, both originals unchanged. No browser errors; both processes exited successfully.
- Browser pages in those checks were Windows Chrome test tabs. No claim of native Linux-browser/Wayland interaction, two separate physical PCs, real Internet NAT/TURN, commercial Pokémon trading or WAN performance follows from them.
- The ignored test harness is under `artifacts/webrtc-probe/`; its completed manifests contain results instead of connection capabilities. Redacted screenshots `cross-os-gb-01-host.png` and `cross-os-gbc-01-host.png` show the connected state before teardown. The harness and screenshots are development artifacts, not required app dependencies.

### Delivered scope

An original, shared C# GB/GBC online-link prototype connects two independent emulation owners. Each participant loads only their own cartridge and local save. The wire carries a version/role handshake, fixed-size cable transactions and heartbeats, not ROM or complete save files. Intended cable payloads can contain trainer/Pokémon data.

Windows and native Wayland reuse their regular display, input and audio pipelines. A locally served browser helper provides ordered reliable WebRTC data channels through manual offer/answer exchange. No new native library, hosted lobby, default STUN/TURN service, camera/microphone permission or automatic firewall change is introduced. The browser must remain open. Keep invitation/answer exchange private and authentic; do not share the loopback URL.

Open a GB/GBC game first. On Windows use Tools → Online Link (Ctrl+F10 host, Ctrl+Shift+F10 guest); on Linux open the Online Link Tools page (F10), then choose host/guest. The host generates an invitation, the guest pastes it and generates an answer, and the host applies that answer. Return to the native emulator after connecting. Exactly one host and one guest are required.

The original battery/RTC family is copied into a new per-session working directory and remains locked. All online writes go to the copy. There is **no automatic promotion to the original save**, even after a seemingly successful trade. A disconnected session's files remain available for inspection. States, rewind, cheats, turbo and reset are blocked during online play.

### Safety and limitations

The native bridge binds only to a random IPv4 loopback port. It validates the Host, same-origin WebSocket and per-session random 256-bit capability, admits one browser, caps messages at 4096 bytes and queues at 128 messages. The browser has bounded queues, restrictive resource loading and no telemetry or external assets. The cable layer adds transfer-pair IDs, strict wire sequencing and sender nonces; it never mutates a core from a networking callback.

Direct cable traffic is paused at emulated scheduling boundaries while real-time connection handling continues. Normal/fast CGB serial timing and mixed CPU speeds are represented by actual eight-bit emulated transfers. Waiting does not fabricate frames or received bytes. Both ports must be armed: this deliberately restricted pairing rule differs from a physical idle/disconnected cable and is **not proven compatible with every Pokémon handshake**. Mid-byte SB/SC changes are rejected. WAN byte-by-byte round trips can be slow.

Internet traversal may require explicitly configured STUN/TURN; an arbitrary home-router connection without infrastructure is not promised. Manual signaling removes a signaling service, not NAT or relay requirements. GBA online, native lobby UI, managed relay infrastructure and verified real-game trading remain separate work. Local GBA Link Lab is unaffected.

### Verification and next acceptance gates

The automated suite includes core readiness/timing/arbitration/cancellation, fixed wire-format rejection, separate owner-thread byte streams with delayed packets, private save protection, pause/shutdown and native WebSocket bridge bounds/authentication. Local browser-to-browser WebRTC checks are separate from those tests. Record actual commands, pass/skip counts and environment in the development handoff after each run; do not conflate synthetic tests with a commercial-game trade.

Next gates: verified GB/GBC trade using expendable local save copies; two physical Windows↔Linux PCs with native frontends/browsers; Germany↔Austria with realistic NAT, latency and disconnection; confirmed save recovery; then design GBA's distinct network SIO adapter. No source commit or push is implied by this document.
