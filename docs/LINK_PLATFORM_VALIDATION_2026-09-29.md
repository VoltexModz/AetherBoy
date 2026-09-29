# Link-Abnahme: Windows und Linux in allen Paarungen

Stand: 29. September 2026. Verbindlich sind Windows ↔ Windows, Linux ↔ Linux und Windows ↔ Linux. In jedem Fall werden Host und Gast getauscht. GB, GBC und GBA erhalten getrennte Spielergebnisse; ein erfolgreicher GBA-Test gibt GB/GBC nicht frei.

## Aktuell tatsächlich nachgewiesen

| Ebene | Windows | Linux | Bedeutung |
| --- | --- | --- | --- |
| GBA-Start, Abschnittswechsel, Datenintegrität, IRQ-/Timer-Testprogramme | Lokal ausgeführt | Unter Ubuntu 26.04 / WSL2 x64 ausgeführt | Gemeinsame Runtime verhält sich in den geprüften synthetischen Fällen korrekt |
| Zwei unabhängige GBA-Emulations-Threads mit künstlicher Latenz/Jitter | Lokal ausgeführt | Unter WSL2 ausgeführt | Tatsächliche Owner-Threads, vollständige Kommandos, synthetische Sitzungskopien; kein Netz zwischen Rechnern |
| Nativer Raum-/WebRTC-Transport mit zwei Endpunkten auf einem Rechner | Lokal ausgeführt, ohne TURN | In diesem Arbeitspaket nicht ausgeführt | Windows-Loopback, nicht Windows ↔ Linux und nicht WAN |
| Zwei Rechner über verschiedene Internetanschlüsse | Offen | Offen | Für jede Paarung separat durchzuführen |
| Echter Pokémon-Tausch und beidseitiger Save-Neustart | Offen | Offen | Nicht durch grüne Transport- oder Modelltests ersetzt |

Der Linux-Lauf nutzte die vorhandene .NET-10.0.12-Laufzeit und dieselben portablen Testassemblies, die unter Windows gebaut wurden. Wegen fehlender ICU-Bibliotheken in dieser WSL-Installation war **nur für den Testprozess** `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` gesetzt. Das ist keine Linux-Frontend-, Paketierungs-, Wayland- oder native-libdatachannel-Abnahme. Der normale Linux-Build des Kollegen soll mit vollständigen Systemabhängigkeiten und ohne diesen Ausweichmodus geprüft werden.

Details zum implementierten Fix und den Testergebnissen: [GBA-Startup-Fix](LINK_GBA_STARTUP_FIX_2026-09-29.md).

## Verbindliche echte Testmatrix

A und B bezeichnen unterschiedliche Rechner. Für einen WAN-Nachweis müssen sie über unterschiedliche Internetanschlüsse kommunizieren. Zwei Prozesse, zwei VMs oder Windows und WSL auf demselben PC reichen dafür nicht.

| ID | Host | Gast | ROM-freier WAN-Probe | GB-Tausch | GBC-Tausch | GBA-Tausch + beide Saves neu laden |
| --- | --- | --- | --- | --- | --- | --- |
| WW-A | Windows A | Windows B | Offen | Offen | Offen | Offen |
| WW-B | Windows B | Windows A | Offen | Offen | Offen | Offen |
| LL-A | Linux A | Linux B | Offen | Offen | Offen | Offen |
| LL-B | Linux B | Linux A | Offen | Offen | Offen | Offen |
| WL | Windows | Linux | Offen | Offen | Offen | Offen |
| LW | Linux | Windows | Offen | Offen | Offen | Offen |

Keine Zelle wird wegen eines Erfolgs in einer anderen Zeile automatisch grün. Linux-x64 und Linux-arm64 werden bei realer Prüfung getrennt angegeben. Unterschiedliche Editionen, Sprachen, Revisionen, die Gen1-/Gen2-Zeitkapsel und ROM-Hacks sind zusätzliche Kompatibilitätsfälle, keine stillschweigend enthaltene Freigabe.

## 1. Gleichen Quell- und Protokollstand herstellen

- Beide Teilnehmer verwenden denselben abgestimmten Quellstand einschließlich dieses Fixes. Die konkrete Commit-ID für jeden Test mit `git rev-parse HEAD` erfassen; der Umfang dieser Übergabe steht im [Commit-Überblick](LINK_CABLE_COMMIT_HANDOFF_2026-09-29.md).
- Die gemeinsame Runtime liegt in `nanoboy/Runtime/Netplay`; der Startup-Fix benötigt keine abweichende Linux-Implementierung.
- Pro Lauf Buildkennung und Änderungen erfassen. Die sichtbare Versionsnummer allein reicht nicht, weil mehrere Entwicklungsstände dieselbe Version tragen können. Bei lokalen Änderungen zusätzlich die SHA-256-Werte der tatsächlich verwendeten Runtime-/Core-DLLs festhalten.
- Keine ROMs, Spielstände, Raumzugangsschlüssel oder TURN-Passwörter ins Repository oder in öffentliche Testberichte legen.

## 2. Gemeinsame GBA-Regressionen ausführen

Aus dem Repository; .NET entsprechend `global.json` muss verfügbar sein:

```text
dotnet restore tests/AetherBoy.RuntimeTests/AetherBoy.RuntimeTests.csproj --locked-mode --configfile NuGet.config
dotnet test --project tests/AetherBoy.RuntimeTests/AetherBoy.RuntimeTests.csproj -c Release --no-restore --filter "FullyQualifiedName~GbaGen3StartupReferenceTests|FullyQualifiedName~GbaGen3ProtocolReviewTests|FullyQualifiedName~PokemonGen3SerialAdapterTests|FullyQualifiedName~GbaGen3IrqLifecycleTests|FullyQualifiedName~AutonomousArmProgramsExchangeGen3CommandsThroughIndependentOwners" --timeout 2m --report-trx --results-directory artifacts/link-validation/protocol
```

Zum dokumentierten Stand sind das **80 Fälle**. Die Fälle prüfen auch Gastrollen, frühe und späte Resets, Abschluss und Wiederbetreten sowie unabhängige Owner-Threads. Fehlende Native-Bibliotheken dürfen für diesen gezielten Modelltest keine angeblichen WAN-PASS-Ergebnisse erzeugen; er ist kein nativer Transporttest.

## 3. Nativen Transport zunächst lokal prüfen

Windows: Node im Suchpfad, Windows-Native-Bibliothek wird mit den gesperrten NuGet-Abhängigkeiten bereitgestellt.

```powershell
$env:AETHERBOY_TEST_NATIVE_ONLINE = '1'
$env:AETHERBOY_TEST_TURN_URL = ''
dotnet test --project tests/AetherBoy.RuntimeTests/AetherBoy.RuntimeTests.csproj -c Release --no-restore --filter "FullyQualifiedName~NativeRoomsExchangePacketsWithoutBrowserAndCloseTogether|FullyQualifiedName~NativeRoomMustDrainFinalReceiptAfterOrderlyPeerClosure" --timeout 2m --report-trx --results-directory artifacts/link-validation/native-local
```

Linux: zusätzlich Git, CMake, C/C++-Buildwerkzeuge und OpenSSL-Entwicklungsdateien gemäß vorhandener Linux-CI bereitstellen. Danach:

```bash
bash scripts/build-online-native.sh
AETHERBOY_TEST_NATIVE_ONLINE=1 AETHERBOY_TEST_TURN_URL= dotnet test --project tests/AetherBoy.RuntimeTests/AetherBoy.RuntimeTests.csproj -c Release --no-restore --filter 'FullyQualifiedName~NativeRoomsExchangePacketsWithoutBrowserAndCloseTogether|FullyQualifiedName~NativeRoomMustDrainFinalReceiptAfterOrderlyPeerClosure' --timeout 2m --report-trx --results-directory artifacts/link-validation/native-local
```

Erwartet: vier ausgeführte Fälle, nicht vier übersprungene. Diese Prüfungen nutzen einen temporären lokalen Raumdienst und keine echten Zugangsdaten. Sie belegen noch keinen funktionierenden externen TURN-Pfad.

## 4. Für jede Matrixzeile den bestehenden WAN-Probe verwenden

Voraussetzungen: tatsächlicher HTTPS-Raumdienst mit `transport-probe-v1`, funktionierender TURN/UDP-Dienst, private Serverkonfiguration auf beiden Rechnern. Kein neuer SDP-Copy-Paste-Ablauf erforderlich. Hier wurden keine vorhandenen Serverzugänge ausprobiert oder Server verändert.

Vorbereitung auf beiden Rechnern:

```text
dotnet restore tools/AetherBoy.OnlineProbe/AetherBoy.OnlineProbe.csproj --locked-mode --configfile NuGet.config
dotnet build tools/AetherBoy.OnlineProbe/AetherBoy.OnlineProbe.csproj -c Release --no-restore
```

Auf Linux muss `build-online-native.sh` **vor** diesem Build erfolgreich gewesen sein, damit `libdatachannel.so` ins Ausgabeverzeichnis kommt.

Host:

```text
dotnet run --project tools/AetherBoy.OnlineProbe/AetherBoy.OnlineProbe.csproj -c Release --no-build --no-restore -- host --samples 20 --reports artifacts/link-validation/WW-A/host
```

Gast:

```text
dotnet run --project tools/AetherBoy.OnlineProbe/AetherBoy.OnlineProbe.csproj -c Release --no-build --no-restore -- join --samples 20 --reports artifacts/link-validation/WW-A/guest
```

`WW-A` durch die tatsächlich geprüfte Matrix-ID ersetzen. Der Gast gibt den kurzen Raumcode interaktiv ein. Ohne `--settings` verwendet das Werkzeug die vorhandene private Konfiguration:

- Windows: `%LOCALAPPDATA%/AetherBoy/Settings/online-room.json`.
- Linux: `$XDG_CONFIG_HOME/aetherboy/online-room.json`, standardmäßig `~/.config/aetherboy/online-room.json`.

Beide Programme offen lassen, bis **beide** PASS melden; erst dann Enter zum Beenden. Für diese Einstellung sind 80 eigene geprüfte Echos pro Teilnehmer vorgesehen: jeweils 20 für 32, 256, 1024 und 4096 Byte. In beiden Berichten müssen der geöffnete Datenkanal, der erfolgreiche Probe und der ausgewählte Relay-Pfad erkennbar sein. Ein einzelnes PASS oder bloß `connected` reicht nicht.

Anschließend neue Sitzung mit getauschten Rollen. Nicht das Ergebnis einer alten Sitzung dem neuen Raum zuordnen. Beide bereinigten Diagnoseberichte gemeinsam archivieren; keine Roh-SDPs oder Zugangsdaten hinzufügen.

## 5. Erst danach mit Spielen und Sitzungskopien testen

Für jedes geprüfte GB-/GBC-/GBA-Spielepaar:

1. Exakte Edition, Sprache, Revision und lokales ROM-Profil erfassen; GBA nur mit dem ausdrücklich unterstützten Entwicklungsprofil. In-Game-Voraussetzungen des Kabelraums müssen erfüllt sein.
2. Regulär im Spiel speichern. Online-Sitzung mit den vorhandenen geschützten Kopien starten, nicht mit ungesicherten Originalen experimentieren.
3. Kabelraum betreten, einen Tausch durchführen, nochmals tauschen, geordnet verlassen, erneut betreten und einen weiteren Austausch versuchen.
4. Auf beiden Seiten regulär speichern und Sitzung geordnet beenden. Beide **Sitzungsspielstände** aus einem echten Neustart laden, nicht aus Quick-Saves.
5. Auf beiden Seiten prüfen, ob die richtigen Pokémon mit erwarteten Daten vorhanden sind. Erst danach je Teilnehmer die bewusste Übernahme der Kopie erwägen.
6. Separate Kopien für Abbruchfälle nutzen: vor Zustimmung, während Übertragung, während Speichern, nach einseitigem Verlassen. Keine automatische Fortsetzung oder Übernahme bei unklarem Ausgang.

Ein Kabel-Abschlusskommando, eine leere Queue oder ein Close-Receipt ist kein atomarer Speicher-Commit beider PCs. Diese Grenze bleibt bestehen.

## Nachweis pro Lauf

Matrix-ID, Datum, Host-/Gast-Plattform und Architektur, Build/Quellstand, .NET-/Native-Version, Testart (Loopback, LAN oder WAN), beide Diagnose-Dateinamen, tatsächlich beobachtetes Ergebnis und Abweichungen erfassen. Für Spieltests zusätzlich Profil und beidseitige Kaltstartkontrolle. Keine personenbezogenen Trainer-/Pokémon-Datensätze oder Saves für diese Zusammenfassung erforderlich.

Ein Fehlerbericht soll sagen, **welche Stufe** scheiterte: Raumdienst, ICE/Relay, Datenkanal, Probeintegrität, Spiel-Handshake, laufender Austausch oder Speicherprüfung. Aus einem Zeitlimit allein keine Router-, MTU- oder Core-Ursache ableiten.

## Was als Nächstes vom Linux-Kollegen gebraucht wird

Den abgestimmten Quellstand nativ auf Linux bauen, die 80 gemeinsamen GBA-Fälle und vier nativen Loopback-Fälle ausführen und die tatsächlichen Ergebnisse zurückgeben. Danach gemeinsam die sechs WAN-Zeilen abarbeiten. Der hiesige WSL-Modelllauf ersetzt diese native Linux-/GUI- und Rechnerpaar-Abnahme nicht.
