# AetherBoy

> **Status: Alpha / experimentell.** AetherBoy ist eine laufende Modernisierung und noch kein verlässlicher Emulator-Release. Save States, Rewind und Link-Kabel bleiben bewusst deaktiviert, bis ihr Zustand vollständig und reproduzierbar getestet ist.

AetherBoy ist ein Windows-Emulator für Game Boy und Game Boy Color in C#. Das Projekt begann 2014 als `nanoboy` und wurde später als **ChiiBoy Color** weitergeführt. Produkt und Assembly heißen jetzt einheitlich **AetherBoy 4.1.0-alpha.1**; der historische Namespace und Projektordner `nanoboy` bleiben vorerst erhalten.

AetherBoy ist weder von Nintendo autorisiert noch mit Nintendo verbunden. Game Boy, Game Boy Color und zugehörige Produktnamen sind Marken ihrer jeweiligen Rechteinhaber.

## Technischer Stand

- Windows-Forms-Anwendung auf **.NET 10 LTS**
- LR35902-/Game-Boy-Core mit DMG- und CGB-Codepfaden
- verwaltete WinForms-Bildausgabe mit Sharp-, Smooth- und LCD-Grid-Filter
- thread-sichere, kopierte Framesnapshots ohne dauerhaft gepinnten Speicher
- NAudio-WinMM-Ausgabe mit begrenztem Puffer
- Tastatur- und XInput-Gamepad-Eingabe
- deterministische Tests für Hardwaretakt, Timer, Interrupts, CPU-Steuerbefehle und Audio-Sampling
- reproduzierbarer NuGet-Restore über Lockfiles sowie GitHub Actions und Dependabot

Phase 1 hat die alten OpenTK-3-Abhängigkeiten vollständig entfernt. Der Core verwendet nun getrennte CPU- und Dot-Taktdomänen, ein driftarmes 59,7275-Hz-Frame-Pacing und einen timergetriebenen Interruptpfad an Instruktionsgrenzen. Das ist eine belastbare Grundlage, aber noch keine vollständige Hardware-Conformance.

## Funktionsstatus

„Nicht freigegeben“ bedeutet: Code kann vorhanden sein, die Funktion ist aber bis zu einer Korrektur und Verifikation deaktiviert oder unzuverlässig.

| Bereich | Status | Bekannte Einschränkung |
| --- | --- | --- |
| CPU und Scheduler | experimentell | Double-Speed, EI/DI, Interruptkosten, HALT-Wakeup und HALT-Bug besitzen Regressionstests; Buszugriffe sind noch nicht T-Zyklus-genau und der vollständige STOP-Ruhemodus fehlt. |
| Timer | verbessert, experimentell | 16-Bit-Divider, TAC-Flanken und verzögerter Overflow sind getestet; seltene Schreibkollisionen im Reload-Takt bleiben angenähert. |
| Bildausgabe | experimentell | Verwalteter Renderer und Snapshots sind GPU-unabhängig; PPU-, STAT-, DMA- und Pixel-Prioritätsgenauigkeit sind noch nicht vollständig belegt. |
| DMG/CGB-ROM-Laden | experimentell | Nur mit legal beschafften ROM-Dumps testen; Mapper- und CGB-Abdeckung ist unvollständig. |
| MBC1/MBC3 | teilweise implementiert | RTC- und Persistenzverhalten sind nicht vollständig verifiziert. |
| MBC2/MBC4/MBC5 und weitere Mapper | nicht freigegeben | MBC5 wird aktuell fälschlich über den MBC3-Pfad behandelt; andere Mapper fehlen. |
| NAudio-Ausgabe | verbessert, experimentell | Masterclock, Sample-Akkumulator, Puffergröße und Kanal-Längenzähler wurden korrigiert; der APU-Frame-Sequencer ist noch nicht vollständig hardwaregetreu. |
| WAV-Aufnahme | experimentell | Aufnahme und Stop benötigen noch einen vollständig synchronisierten Fehler- und Thread-Lifecycle. |
| Save States | nicht freigegeben | Das v1-Binärformat bildet keinen vollständigen deterministischen Emulatorzustand ab. |
| Rewind | nicht freigegeben | Baut auf demselben unzuverlässigen Save-State-Format auf. |
| GameShark | teilweise implementiert | Einfache RAM-Writes sind vorhanden; Validierung und Nebenwirkungsgrenzen fehlen. |
| Game Genie | deaktiviert | Codes werden noch nicht im ROM-Lesepfad angewendet. |
| Link-Kabel/Netplay | nicht funktionsfähig | TCP-Oberfläche und emulierte serielle Hardware sind nicht taktgenau verbunden. |
| Debugger/Disassembler | intern/experimentell | Kein vollständiger Pause-/Step-Workflow; mehrere Grenzfälle sind ungeprüft. |

## Bauen, testen und starten

Voraussetzungen:

- Windows
- .NET SDK **10.0.302** oder ein kompatiblerer Patch derselben Feature-Band
- für Audioausgabe ein funktionierendes Windows-WinMM-Gerät

Im Repository-Root:

```powershell
dotnet restore ./nanoboy.sln --locked-mode --configfile ./NuGet.config
dotnet build ./nanoboy.sln -c Release --no-restore
dotnet test ./nanoboy.sln -c Release --no-build --no-restore
dotnet run --project ./nanoboy/nanoboy.csproj -c Release --no-build
```

`global.json` pinnt das SDK, `packages.lock.json` pinnt den aufgelösten Paketgraphen. Der CI-Workflow führt denselben Restore-, Build- und Testpfad auf Windows aus.

## ROMs, Boot-ROMs und Spielstände

Dieses Projekt erteilt **keine** Rechte an kommerziellen Spielen, Nintendo-Firmware, Boot-ROMs, Marken, Grafiken oder sonstigen Drittinhalten. Verwende nur ROM- und Firmware-Dumps, die du nach dem für dich geltenden Recht selbst verwenden darfst. Verbreite keine ROMs oder Boot-ROMs zusammen mit Quellcode oder Builds. Diese Hinweise sind keine Rechtsberatung.

Historische `.gb`-/`.gbc`-Dateien und ein persönlicher `.sav` wurden in Phase 0 aus dem veröffentlichbaren Quellbaum entfernt. Die lokale Arbeitskopie bewahrt sie ausschließlich im ignorierten Verzeichnis `.local-assets/roms` auf. Sie sind **nicht** von der GPL des Emulatorcodes umfasst und dürfen nicht zum Repository oder zu einem Release hinzugefügt werden.

Boot-ROM-Dateien wie `dmg_boot.bin` oder `gbc_boot.bin` sind zum Bauen nicht erforderlich und müssen – sofern ihre Nutzung legal ist – vom Benutzer selbst bereitgestellt werden.

## Repository-Hygiene

Die verbindlichen Abhängigkeiten stehen in den Projektdateien, ihre Auflösung in den Lockfiles. Buildausgaben, IDE-Zustand, lokale SDK-Werkzeuge, ROMs, Boot-ROMs, Save-Dateien, Logs und Symbole sind ausgeschlossen. Phase 1 ergänzt einen eingeschränkten NuGet-Feed, Vulnerability-Audit, GitHub Actions und automatisierte Abhängigkeitsupdates.

## Lizenz und Herkunft

Der Emulatorcode wird als **GNU General Public License Version 3** dokumentiert; siehe [LICENSE](LICENSE). Mangels einer ausdrücklichen „or later“-Erklärung wird konservativ `GPL-3.0-only` verwendet. Copyright und Urheberschaft verbleiben bei den jeweiligen ursprünglichen Autoren und späteren Beitragenden.

Die Projektchronik nennt Frédéric Meyer als ursprünglichen Entwickler (2014) und dokumentiert spätere ChiiBoy-/AetherBoy-Modifikationen. Drittanbieterkomponenten besitzen eigene Lizenzen; direkte Pakete und Hinweise stehen in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
