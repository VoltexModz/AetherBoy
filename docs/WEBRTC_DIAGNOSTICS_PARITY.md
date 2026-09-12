# WebRTC-Diagnose: Linux → Windows / diagnostics parity

Stand / Date: **2026-09-13** · **development** · **4.8.0-alpha.1**

## Deutsch

### Was der Linux-Kollege geliefert hat

Der saubere gemeinsame Stand `0566c96` wurde ohne Konflikte per Fast-forward auf
`3850464` aktualisiert. Die beiden Original-Commits bleiben vollständig erhalten:

- **`6e69f83` – Finalize stopped online links and reserve F10:** Wayland entfernt
  vollständig beendete Online-Besitzer vor Eingaben und Bildaktualisierung,
  reserviert F10 einschließlich Migration älterer Tastenbelegungen und ergänzt
  Regressionstests sowie die [Internet-Spieltestanleitung](ONLINE_PLAYTEST_DE.md).
- **`3850464` – Preserve actionable WebRTC bridge failure reasons:** Der Browser
  schickt ausschließlich erlaubte Fehlercodes an die lokale native Brücke.
  Diese bewahrt die konkreten Gründe in der Exception-Kette. Späte Clipboard-
  und Signaling-Antworten dürfen verbundene oder endgültig beendete Statusanzeigen
  nicht überschreiben. Native Tests und vier JavaScript-Tests sichern das ab.

Die Browserdateien und `WebRtcBrowserTransport` liegen im **gemeinsamen Runtime**,
nicht in einer Linux-Sonderkopie. Beide Frontends erhalten sie durch Neubauen.

### Was dieser Windows-Abgleich ergänzt

- `WebRtcBrowserTransport.BrowserFailure` stellt eine thread-sicher gelesene,
  nullable `WebRtcBrowserFailure`-Kategorie bereit. Sie wird erst nach Prüfung
  eines bekannten Codes gesetzt und bleibt nach dem Abbau abrufbar. Keine
  Auswertung englischer Exception-Texte in der Oberfläche, kein Durchreichen
  unvalidierter Browsertexte, SDP-Daten, Adressen oder Zugangsdaten.
- Die bisherige allgemeine Windows-Emulationsfehlermeldung verdeckte den
  konkreten Verbindungsgrund. Online-Fehler werden jetzt ohne blockierenden
  Standardfehlerdialog im Statusbereich angezeigt. **TOOLS → Online Link →
  Letzte Verbindungsdiagnose** öffnet die vollständige Erklärung im eigenen
  AetherBoy-Dialog, auch nachdem die Sitzung bereits freigegeben wurde.
- Für Peer-Verbindungsfehler wird erklärt, dass STUN kein Relay ist. TURN kann
  bei blockierten direkten Wegen helfen; der aggregierte Browserfehler ist
  ausdrücklich **kein Beweis für einen bestimmten Router-/Firewallfehler**.
- Wie unter Wayland wird ein von der Gegenstelle beendeter GBA-Online-Besitzer
  vor weiterer Eingabe/Bildaktualisierung entfernt. Windows behandelt außerdem
  fehlgeschlagene GB-, GBC- und GBA-Online-Besitzer. Die Bereinigung wartet auf
  `Stopped`/`Faulted` **und** `Completion.IsCompleted`; sie unterbricht keine
  noch laufende Finalisierung. Audio und Eingaben werden bereinigt, das letzte
  Bild gelöscht und der normale Spielstart wieder möglich.
- Original-Saves und normale Fortsetzen-Dateien werden nicht übernommen oder
  ersetzt. Die vertrauenswürdigen Wiederherstellungsziele sowie Sitzungskopien
  bleiben erhalten. Fehlerberichte nutzen weiterhin die lokale datensparsame
  Diagnose. Die letzte lesbare Verbindungsdiagnose bleibt bis zur nächsten
  Online-Sitzung bzw. bis zum Schließen des Fensters verfügbar.

### Prüfnachweise

Die Prüfung verwendet selbst erzeugte GB/GBC/GBA-Testprogramme, isolierte
Windows-Datenpfade und lokale Test-WebSockets. Es werden keine echten ROMs,
Spielstände oder fremden Browser-/Peer-Sitzungen angesprochen.

- Release-Build der gesamten Solution: **0 Warnungen, 0 Fehler**.
- Windows-Online-Regressionsgruppe: **14/14 bestanden**. Sechs neue Fälle:
  vollständige Zuordnung der Fehlerkategorien; echte lokale Fehlerweitergabe
  Browserbrücke → GB/GBC/GBA-Besitzer → Windows inklusive erneutem Spielstart;
  GBA-Abschluss durch die Gegenstelle vor Timer- bzw. Tastaturverarbeitung.
- Gemeinsame Runtime unter Windows: **355 bestanden, 1 POSIX-Skip, 0 Fehler**.
- Browser-Callback-Tests des Kollegen: **4/4 bestanden**. Diese verwenden ein
  simuliertes Browsermodell, keinen echten ICE-/DTLS-Internetpfad.
- Vollständige .NET-Suiten unter Windows: **903 entdeckt, 857 bestanden,
  46 übersprungen, 0 Fehler**: Core 232/232; Runtime 355 + 1 Skip;
  Desktop-Logik 80 + 41 Skips; Windows-Smoke 190 + 4 Skips. Darunter sind drei
  optionale Hardwareproben und eine verweigerte native Vordergrundaktivierung;
  die übrigen Skips betreffen Linux/Wayland/POSIX bzw. deren Opt-in-Voraussetzungen.
- Gemeinsame Runtime unter Ubuntu/WSL x64, .NET 10.0.12: **356/356 bestanden**.
  Mit prozesslokalem `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`; keine Änderung
  am Linux-System und kein gestartetes Wayland-Fenster.
- Transparenz: Der erste Windows-Gesamtlauf blieb beim bestehenden
  `ClosingRunningWindowWaitsForBatteryFlushAndReleasesBothWriteLeases` hängen
  und wurde nach mehreren Minuten ausschließlich am Testprozess beendet.
  Der Test bestand separat in unter einer Sekunde; die komplette Wiederholung
  mit 120-Sekunden-Limit bestand in 32 Sekunden. Die Ursache dieses sporadischen
  Testhangers ist **nicht geklärt oder als behoben behauptet**.

Lokale, nicht eingecheckte Nachweise:
`artifacts/webrtc-windows-20260913/test-results/` enthält `online-focused.trx`,
`local-close-isolated.trx`, `linux-runtime.trx` sowie die vollständigen
projektspezifischen Reports (`full-retry.trx` für Windows-Smoke).

Die CI erwartet nun **903 entdeckte .NET-Fälle** statt 897. Native Wayland-,
Audio-/GPU- und interaktive Eingabetests bleiben an ihre expliziten Plattform-
und Hardwarebedingungen gebunden; Skips sind keine bestandenen Hardwaretests.

### Aktualisieren und erneut versuchen

Beide Entwickler holen den neuen `development`-Stand und bauen die App neu.
**Ein laufender Emulator verwendet weiterhin seine alte eingebettete Browserseite.**
Vor dem Neustart eine laufende Online-Sitzung bewusst beenden und ihre Kopien
prüfen. Danach eine neue Sitzung samt neuer Einladung/Antwort erzeugen; die alte
Browserseite nicht nur neu laden. Dieses Paket startet oder beendet keine reale
Spielsitzung automatisch.

Keine neue Protokollversion: WebRTC-Einladungen bleiben Version 1, das getrennte
GBA-Kabelprotokoll behält seine bisherige Version. Kein Core-/Save-Formatwechsel,
kein betriebener TURN-Dienst, keine Garantie für NAT-Durchquerung und weiterhin
**kein nachgewiesener erfolgreicher Pokémon-Tausch über zwei Internetanschlüsse**.

Beim Quellvergleich zusätzlich aufgefallen, **nicht in diesem Windows-Paket
geändert**: `run-linux.sh` berücksichtigt reine `.js`-/`.html`-Änderungen nicht
bei der Aktualitätsprüfung; der Quellfilter in `package-linux.sh` lässt diese
eingebetteten Ressourcen aus. Die C#-Änderungen dieses Pakets lösen den normalen
Start-Neubau aus. Der Linux-Paketpfad sollte gesondert um beide Ressourcentypen
ergänzt und mit einem vollständigen Paketbuild geprüft werden.

## English

The clean `0566c96` checkout was fast-forwarded to the colleague's **3850464**,
preserving both original commits. **6e69f83** adds completed-owner retirement on
Wayland, reserves/migrates F10 and documents the two-site playtest. **3850464**
adds allowlisted browser failure codes, preserves actionable native exception
causes and prevents late clipboard/signaling callbacks from masking connected
or terminal browser status. This is shared Runtime code, already used by both
frontends after a rebuild.

This follow-up adds a validated, thread-safe nullable `BrowserFailure` category
and German Windows explanations without matching English exception strings or
displaying raw signaling data. **TOOLS → Online Link → Letzte Verbindungsdiagnose**
retains the complete explanation after cleanup. No automatic modal blocks the
online failure path. Windows now retires stopped/faulted online owners before
input/frame refresh, but only after owner completion. Audio/input are cleared,
normal game opening becomes available again, and trusted recovery targets and
private session copies remain accessible. Original saves and normal resume
files are never automatically replaced.

Validation uses synthetic programs and isolated local sockets, not real player
data. The full Release build is warning/error-free; **14/14 Windows online
regressions**, **355 passed + 1 POSIX skip Runtime tests on Windows**, and **4/4
simulated browser callback tests** pass. The complete Windows-hosted suites
discover **903 .NET cases: 857 passed, 46 platform/hardware skips, zero failures**.
The shared Runtime also passes **356/356 on Ubuntu/WSL x64**. The first full
Windows run hung in an existing local-link close test and was terminated; that
test passed alone and the complete bounded retry passed in 32 seconds. The
intermittent hang's cause remains unconfirmed, not silently marked fixed.
CI discovery increases from 897 to 903. Platform/hardware opt-in skips do not
prove actual device behavior. Local TRX report locations are listed above.

Rebuild and deliberately restart the app before creating a fresh online session:
the helper is embedded, so an existing process keeps the old code. Invite and
GBA protocol versions, core and save formats are unchanged. Better diagnosis is
not a NAT traversal fix, a TURN service or evidence of a successful real WAN
Pokémon trade. The Linux launcher/package filters additionally omit `.js`/`.html`
resources; that separate packaging issue is recorded above for the Linux owner,
not silently claimed as tested or fixed here.
