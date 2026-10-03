# Windows-Abnahme des Linux-Lieferstands / Windows integration review

Stand: **28. September 2026**. Basis: `2a91c9f676e59a87d87e15f1e7bdfd04ad66c3ba`
von xJessyX, übernommen per Fast-forward von `4b56039`. Die folgenden Korrekturen
sind lokal vorbereitet; **noch kein neuer Commit/Push und kein Server-Deployment**.

## Übernommen und erhalten

- Gemeinsame `UiThemePalette`, frei wählbare Akzente/Hintergrund, Windows-Appearance-
  Seite und Linux-Farbverwaltung einschließlich Speicherung.
- Überarbeitete Shells, Online-Link-Texte, README-Bilder und UI-Copy-Regeln.
- Unsere ROM-freie Windows-Testoberfläche, `OnlineProbeSession` und native Diagnose.
- Keine ROMs, echten Spielstände oder produktiven Server angefasst. Fremde lokale
  Projekte und unversionierte Dateien blieben unverändert.

## Fehlerursache und Korrekturen

### 1. CI nach Textüberarbeitung

Der [Upstream-Lauf für 2a91c9f](https://github.com/VoltexModz/AetherBoy/actions/runs/36361142653)
scheiterte auf Windows, Linux x64 und ARM64 im selben Runtime-Test:
`HelperIsLoopbackOnlyUsesFragmentCapabilityAndServesNoExternalAssets`.
Er erwartete noch `AetherBoy Link Bridge`; die HTML-Seite war umbenannt worden.
Eine zweite ebenfalls veraltete Erwartung, `Keine Downloads`, hätte danach
denselben Test weiter scheitern lassen.

Der Test prüft jetzt die tatsächlichen eingebundenen Ressourcen (nur `/bridge.js`)
und die CSP-Regeln `default-src 'none'`, `script-src 'self'`,
`frame-ancestors 'none'`. Loopback-, Capability-, Referrer-, CORS-, Kamera-/Mikrofon-
und externe-STUN-Prüfungen bleiben erhalten. Keine Sicherheitsprüfung wurde aus
dem Transport entfernt, kein Runtime-Protokoll geändert.

### 2. Sichtprüfung: abgeschnittene Speicherplatzknöpfe

In der normalen Windows-Fenstergröße waren die fünf Slot-Knöpfe unten abgeschnitten.
Die FlowLayoutPanel-Zeile ist 36 px hoch; die Standardränder nahmen 6 px weg.
Danach passten 5 px Innenabstand plus 30 px Knopf nicht mehr hinein.

`Margin = Padding.Empty` am Slot-Container stellt die nötige Höhe bereit. Der neue
Layouttest ist vor dieser Änderung in allen drei Farbvarianten reproduzierbar
fehlgeschlagen und danach bestanden. Keine Änderung an Speicherfunktionen.

### 3. Bestehender Diagnosetest las vor dem Schreibabschluss

Der erste vollständige Smoke-Lauf war grün. Beim zusätzlichen Lauf mit dem exakten
`dotnet test --project`-Einstieg schlug
`HealthReportAndManualMarkerContainNoFramePixelsOrExtraFiles` fehl: Der Marker war
angenommen, aber die Datei wurde vor seinem asynchronen Schreibabschluss gelesen.
Der Test nutzt jetzt die bestehende `CreateBundle`-Exportbarriere und prüft die
vollständige `session.jsonl` im Archiv. Die Inhalts-/Datenschutzprüfungen bleiben
erhalten; keine Sleeps, Retries oder Änderungen am Produktionslogger.

### 4. Vier zusätzliche Windows-Tests

`WindowsThemeTests` prüft:

- bestehende Haupt-/Einstellungsfenster bei Logo-, heller und schwarzer Palette;
- Aktualisierung der Flächen-/Textfarben, drei Farbauswahlknöpfe und sichtbare Seite;
- Rückkehr zu den Logo-Farben über den echten Restore-Knopf;
- vollständige Slot-Knopfgeometrie;
- Speicherung/Reload der drei UI-Farben ohne Veränderung der Spielpalette.

Der Restore-Knopf hat dafür einen stabilen Control-Namen; sein Verhalten und Text
sind unverändert. Die Tests benutzen den bestehenden isolierten Windows-Datenordner,
nicht die AppData-Konfiguration eines Spielers. Die Farbauswahl wird für diese
Tests programmatisch gesetzt; der native ColorDialog wurde nicht manuell bedient.

CI-Mindestzahlen wurden an die neuen Tests angepasst: Core 234, Runtime unverändert
392, Desktop 123, Windows Smoke 214, nativer Linux-Headless-Lauf 119. Keine Tests
deaktiviert, keine Fristen verlängert, keine bestehenden Gates abgesenkt.

## Lokaler Prüfstand unter Windows x64

.NET SDK 10.0.302; Locked Restore, vollständiger Release-Build:
**0 Warnungen, 0 Fehler**. `AETHERBOY_TEST_NATIVE_ONLINE=1` mit Node im PATH;
`AETHERBOY_TEST_TURN_URL` nicht gesetzt. Native Runtime-Fixtures bleiben lokal,
kein produktiver TURN-/Raumserver wurde angesprochen.

| Suite | Bestanden | Übersprungen | Fehler |
| --- | ---: | ---: | ---: |
| Core | 234 | 0 | 0 |
| Runtime inklusive lokaler nativer Räume | 391 | 1 | 0 |
| Portable Desktop-Logik auf Windows | 81 | 42 | 0 |
| Windows Smoke/UI | 210 | 4 | 0 |
| **.NET gesamt** | **916** | **47** | **0** |
| Node: Browser-Bridge und Raumdienst | 8 | 0 | 0 |

Die vier neuen Theme-Fälle sind bereits in den 210 Smoke-Erfolgen enthalten.
Übersprungene Prüfungen betreffen insbesondere native Wayland-/POSIX-Funktionen,
Hardware-Opt-ins und einen Test ohne zugelassenen Vordergrundfokus. Keine pauschale
Hardware-, DPI- oder Linux-UI-Abnahme aus diesen Zahlen ableiten.

Die OnlineProbe-CLI-Hilfe ist erfolgreich. Der UI-Copy-Review lief über Control
Center, Windows-Shell, Raumdialog und Browserseite: 19 Hinweise in übernommenen
Texten/Statusformaten, darunter auch zusammengezählte Alternativen einer Quellzeile.
Sie wurden gesichtet; eine vollständige sprachliche Vereinheitlichung der teils
englischen Windows-Oberfläche ist nicht Teil dieses Integrationsfixes.

### Artefakte und Wiederholung

Alle Ausgaben liegen lokal, ignoriert, unter:
`artifacts/windows-theme-integration-20260928/`.

- `test-results/{Core,Runtime,Desktop}/`: finale TRX-Berichte dieser Suites.
- `test-results/Smoke/`: erster vollständiger, grüner UI-Lauf vor der zusätzlichen
  Diagnosetestkorrektur; maßgeblich für den letzten Stand ist `ci-smoke/`.
- `layout-before/`: erwarteter roter Reproduktionslauf vor dem Layoutfix.
- `ci-smoke/`: finaler erfolgreicher CI-Aufruf (214 erfasst, 210 bestanden,
  4 übersprungen). Er ersetzt den früheren Bericht des diagnostizierten Schreibrennens.
- `screenshots/windows-appearance-{logo,light,black}.png` und
  `screenshots/windows-theme-{logo,light,black}.png`: echte WinForms-Renderings
  aus isolierten Tests ohne ROM. Logo-, helle und schwarze Appearance-Ansicht sowie
  die korrigierte Hauptansicht wurden visuell kontrolliert.
- `bin/nanoboy/release/AetherBoy.exe`: frischer Windows-Build. Zum Starten den
  gesamten zugehörigen Ausgabeordner mit nativen Bibliotheken beibehalten.

Buildbefehle (aus dem Repository mit dem SDK aus `global.json`):

```text
dotnet restore nanoboy.sln --locked-mode --configfile NuGet.config --artifacts-path artifacts/windows-theme-integration-20260928
dotnet build nanoboy.sln -c Release --no-restore --artifacts-path artifacts/windows-theme-integration-20260928 -p:ContinuousIntegrationBuild=true
```

Die vollständigen Suites wurden über ihre gebauten Test-DLLs mit `--report-trx`
ausgeführt. Der Windows-CI-Einstieg wurde zusätzlich mit `dotnet test --project`,
demselben `--artifacts-path` und der Mindestzahl 214 erfolgreich geprüft. Keine Benutzer-ROM
und keine Verbindungszugangsdaten sind für diese Regressionstests notwendig.

## Übergabe / weiterhin offen

1. Vor Veröffentlichung die lokale Diff prüfen; Commit/Push erst nach Max' Freigabe.
2. Danach neue GitHub-CI abwarten. Der alte rote Upstream-Lauf wird durch lokale
   erfolgreiche Tests nicht nachträglich grün. Native Linux-/ARM64-Abnahme dieses
   Korrekturstands steht bis zum neuen CI-Lauf aus.
3. Raumdienst und den echten Windows↔Linux-Transporttest nach
   [Linux-Auftrag vom 27. September](LINUX_ONLINE_NEXT_STEPS_2026-09-27.md) fortsetzen.
4. Linux-ROM-freier Testdialog bleibt offen. Keine neue Erfolgsmeldung zu WAN,
   Pokémon oder Save-Übernahme aus dieser UI-Arbeit ableiten.

## English

Fast-forwarded to colleague commit `2a91c9f`, preserving both frontends and shared
runtime. Fixed the stale browser-helper test by checking actual local resources
and CSP rather than two removed UI phrases. Windows visual verification exposed
clipped save-slot buttons; removing the container's default table-cell margin
fixes the reproduced geometry failure. Four new Windows theme cases cover live
recoloring, restore, persistence and slot bounds. CI count gates were raised.
An additional CI-style run exposed a pre-existing test reading a diagnostic log
before its asynchronous writer finished. That test now uses the existing export
barrier and checks the complete archived log, without sleeps or production changes.

Windows Release build: zero warnings/errors. 916 .NET passes, 47 skips, zero
failures; eight Node passes. Real local native room tests ran; screenshots are
isolated WinForms renders without games. No production server, real player saves,
WAN trade or interactive native color-picker acceptance was involved. No new
commit/push yet. Linux GUI parity and actual cross-border trading remain separate
acceptance tasks.
