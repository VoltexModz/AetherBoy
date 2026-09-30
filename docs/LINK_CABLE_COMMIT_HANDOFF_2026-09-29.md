# Link Cable: Übergabe / handoff

Stand / date: 2026-09-29. Gemeinsamer Ausgangspunkt / shared baseline: `9f50dfe` auf / on `development`.

## Deutsch

Dieses Arbeitspaket enthält ausschließlich Link-Kabel-Code, zugehörige Tests und Quellenanalysen. Daneben vorhandene lokale UI-/Einstellungsarbeiten gehören nicht zu diesem Commit.

- GBA: Ein Reset des Partners löscht beim Verbindungsstart nicht mehr die bereits vom lokalen Spiel beobachtete Handshake-Historie. Ein echter lokaler SIO-Neustart löscht sie weiterhin; frische Metadaten des Partners bleiben erforderlich.
- GBA: Abschnittswechsel warten auf den lokalen Abschluss, erhalten letzte vollständige Kommandos und laufen bei ausbleibendem Fortschritt kontrolliert in ein Zeitlimit. Ungültige Phasen und Pakete bleiben Fehler.
- GB/GBC: Genauere Wartegründe unterscheiden fehlendes Angebot, Bereitschaftsbestätigung und Abschlussbestätigung. Die bekannte spielseitige Rollenwahl ist damit noch nicht gelöst.
- Transport: Die HTTP-Frist umfasst auch den Antwortkörper. Geordnetes Schließen lässt bereits angenommene Pakete auslaufen; ein Fehler verwirft die Warteschlange.
- Windows: Ein Fehler im Spielprotokoll wird nicht mehr durch eine pauschale Server-/Browserdiagnose verdeckt. Die Oberfläche verwendet begrenzte, bereinigte Fehlermeldungen.

Die zusätzlichen ARM-Testprogramme sind selbst geschrieben. Externe Projekte dienten als Referenz; deren Code, ROMs, echte Saves und Zugangsdaten sind nicht Bestandteil dieses Pakets.

### Nachweise und Grenzen

Die vor dem Commit ausgeführten Prüfungen und ihre Umgebung stehen im [GBA-Startup-Bericht](LINK_GBA_STARTUP_FIX_2026-09-29.md): Release-Build ohne Warnungen/Fehler, 242 Core-Tests, 446 bestandene Runtime-Tests bei einem unter Windows übersprungenen POSIX-Test, 25 Windows-Online-UI-Tests sowie 80 gezielte portable GBA-Fälle unter WSL. Vier native Windows-Loopback-Fälle wurden ausgeführt, ohne externen TURN-Server.

Vor dem Push wurde der vorgemerkte Commitumfang zusätzlich isoliert aus dem Git-Index ausgecheckt, ohne die übrigen lokalen Änderungen. Auch dort: gesperrte Paketwiederherstellung erfolgreich, kompletter Release-Build mit null Warnungen/Fehlern und erneut 242 Core-, 446 Runtime- sowie 25 Windows-Online-UI-Tests bestanden; derselbe einzelne POSIX-Test übersprungen. Damit hängt dieser Commit nicht von den noch lokalen UI-/Einstellungsarbeiten ab. Die lokalen TRX-Berichte liegen unter `artifacts/link-commit-verification-20260929/artifacts/verification/` und werden nicht mitgeliefert.

Das belegt weder einen echten Pokémon-Tausch noch eine Verbindung zwischen zwei Rechnern. Die sechs Rechner-/Rollenpaarungen und alle echten Tauschnachweise bleiben offen. Auch der WSL-Lauf ersetzt keinen nativen Linux-Frontend- oder Linux-WebRTC-Test.

### Nächste Schritte für Linux

1. Den abgestimmten Commit übernehmen und nativ unter Linux bauen. Der Fix liegt in der gemeinsamen Runtime; keinen zweiten Linux-spezifischen Protokollfix erstellen.
2. Die 80 GBA-Regressionen und vier nativen lokalen Transportfälle gemäß [Plattformmatrix](LINK_PLATFORM_VALIDATION_2026-09-29.md) ausführen; übersprungene Native-Tests nicht als bestanden melden.
3. Den ROM-freien Probe über den echten Raum-/TURN-Dienst gemeinsam ausführen, anschließend Rollen tauschen.
4. Erst danach echte GB-/GBC-/GBA-Spiele getrennt mit geschützten Sitzungskopien prüfen. Einen Tausch erst nach Neustart und Kontrolle beider Sitzungsspielstände als bestätigt dokumentieren.

### Native Linux-Nachprüfung am 30. September 2026

`development` wurde ohne lokale Änderungen per Fast-forward von `9f50dfe` auf
`80cc6b5` aktualisiert. Prüfung auf CachyOS/Linux x64 mit .NET SDK 10.0.302,
Runtime 10.0.10 und der vorhandenen libdatachannel-0.24.5-Bibliothek; kein
Globalization-Invariant-Ausweichmodus und keine produktiven Serverzugänge.

| Prüfung | Ergebnis |
| --- | --- |
| Regulärer Linux-x64-Publish (`bash scripts/build-linux.sh`) | Erfolgreich; Ausgabe in `artifacts/AetherBoy-linux-x64/` |
| Core | 242 bestanden, 0 Fehler |
| Runtime mit `AETHERBOY_TEST_NATIVE_ONLINE=1`, ohne TURN | 447 bestanden, 0 übersprungen, 0 Fehler |
| Darin: gezielte GBA-Regressionen gemäß Plattformmatrix | 80 bestanden |
| Darin: native Raum-/Transportfälle | Alle vier tatsächlich ausgeführt und bestanden |
| Desktop-Logik ohne native Opt-ins | 93 bestanden, 40 übersprungen, 0 Fehler |
| Desktop auf isoliertem Weston/D-Bus, einschließlich GTK/AT-SPI | 130 bestanden, 3 Audiofälle übersprungen, 0 Fehler |
| Browser-Bridge und Raumdienst (Node) | 8 bestanden, 0 Fehler |
| Linux-Installation im temporären XDG-/Bin-Verzeichnis | Erfolgreich: Migration, fehlgeschlagenes Update, Minimal-PATH-Start und Deinstallation mit Datenerhalt |
| OnlineProbe-CLI | Release-Build mit 0 Warnungen/Fehlern; `--help` erfolgreich |
| Publish-Identität und Hyprland-Erkennung | `--version` enthält `80cc6b5`; `--platform-info` erfolgreich |

Die Wayland-Prüfung lief im vorhandenen Ubuntu-24.04-Image
`aetherboy-linux-accessibility-check:latest` mit .NET Runtime 10.0.12,
`--network none`, schreibgeschützter Testassembly und einem eigenen Compositor.
GTK-/Mesa-Meldungen über fehlenden Keyboard-Seat beziehungsweise Grafikgeräte
stammen aus dieser Headless-Umgebung; alle aktivierten Tests bestanden.
Keine Testfenster wurden auf dem Nutzerdesktop geöffnet.

TRX-Nachweise: `artifacts/linux-review-20260930/{core,runtime,desktop-logic,headless}/`.
Ein Core-Test-Restore meldete `NU1900`, weil NuGets Vulnerability-Endpunkt nicht
erreichbar war; der Testlauf bestand. Das ist kein Compiler- oder Testfehler,
bestätigt aber auch keine aktuelle Prüfung der Paket-Sicherheitsmeldungen.

Kein Linux-spezifischer Protokollfix erforderlich. Die CI-Mindestzahlen werden
auf den tatsächlich vorhandenen Umfang angehoben: Core 242, Runtime 447,
Desktop 133 einschließlich Skips beziehungsweise 130 ausgeführte Headless-Fälle.
Diese Nachprüfung aktualisiert den normalen x64-Build; vorhandene portable
Archive wurden nicht neu erzeugt. ARM64-Ausführung, reale Audio-/Hardwaretests,
Produktions-TURN, Rechnerpaarungen und Pokémon-Tausche bleiben offen.
Die bekannte GB/GBC-Rollenwahl und die noch fehlende Linux-Probe-Oberfläche
werden dadurch nicht behoben. Die lokalen CI-/Dokumentationsänderungen wurden
nicht committed oder gepusht.

## English

This package contains link-cable code, its regression tests and source-reference reviews only. Separate local UI/settings work is not included.

- GBA startup now preserves the handshake history actually observed by the local game across a peer-only reset. A genuine local SIO restart still clears it; fresh peer metadata is still required.
- GBA section transitions preserve final complete commands, wait for local completion and have a bounded timeout. Invalid phases and packets still fail closed.
- GB/GBC diagnostics distinguish missing offers, readiness acknowledgements and completion acknowledgements. The known game-side role-election problem is **not fixed** by these diagnostics.
- HTTP deadlines cover streamed response bodies. Accepted packets can drain after orderly closure, but not after a transport fault.
- Windows reports protocol/session failures separately from transport failures using sanitized, bounded messages.

The synthetic ARM programs are original test code. External projects were studied as references; no copied reference implementation, game ROM, real save or credential is included.

Pre-commit verification: a warning-free Release solution build; 242 Core tests passed; 446 Runtime tests passed with one Windows-inapplicable POSIX test skipped; 25 Windows online-UI tests passed; 80 selected portable GBA cases passed under WSL. Four native Windows loopback cases ran without an external TURN service. Environment details and limits are in the [startup report](LINK_GBA_STARTUP_FIX_2026-09-29.md).

The exact staged source tree was also checked out separately from the Git index before pushing. Locked restore, the full Release build and the same Windows test suites passed again (713 passed, one POSIX-only skip). This verifies that the commit does not depend on the unrelated local UI/settings work. Generated test reports are local artifacts, not repository content.

These results are **not** proof of a real Pokémon trade, a two-machine connection or native Linux WebRTC support. Build natively on Linux, run the shared regression and native loopback suites, then follow the [six-row platform/role matrix](LINK_PLATFORM_VALIDATION_2026-09-29.md). Require successful ROM-free probes on both ends before game tests; use protected session copies and verify both saves after a cold restart.

September 30 native Linux x64 follow-up on `80cc6b5`: the normal Linux publish
succeeded; 242 Core and 447 Runtime cases passed, including all 80 selected GBA
regressions and four actual native loopback cases. Isolated Weston/D-Bus/AT-SPI
passed 130 Desktop cases with only three audio cases skipped; eight Node tests
and the temporary-directory installation test passed. OnlineProbe built without
warnings/errors and its CLI help succeeded. No Linux protocol correction was
needed; local CI test-count gates were raised to the current suite sizes. See
the German follow-up above for environments, TRX locations and the unavailable
NuGet vulnerability metadata warning. ARM64 execution, physical hardware/audio,
production relay, two-machine WAN and real trades remain unverified. Portable
archives were not rebuilt; the CI/documentation follow-up is uncommitted.
