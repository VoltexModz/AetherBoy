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
