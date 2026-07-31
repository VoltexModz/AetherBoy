# AetherBoy

> **Status: Alpha / experimentell.** Dieses Repository ist eine Modernisierungsbaustelle und kein verlässlicher Emulator-Release. Mehrere sichtbare Funktionen sind noch nicht freigegeben oder nachweislich fehlerhaft. Nutze insbesondere Save States und Rewind nicht für unersetzliche Spielstände.

AetherBoy ist ein Windows-Emulator für Game Boy und Game Boy Color in C#. Das Projekt begann 2014 unter dem Namen `nanoboy` und wurde später als **ChiiBoy Color** weitergeführt. Produkt und Assembly heißen jetzt einheitlich **AetherBoy 4.0.0-alpha.1**; der historische C#-Namespace und Projektordner `nanoboy` bleiben bis zur späteren Architekturtrennung bestehen.

AetherBoy ist weder von Nintendo autorisiert noch mit Nintendo verbunden. Game Boy, Game Boy Color und zugehörige Produktnamen sind Marken ihrer jeweiligen Rechteinhaber.

## Technischer Stand

- Windows Forms-Anwendung auf `net8.0-windows`
- LR35902-/Game-Boy-Core mit DMG- und CGB-Codepfaden
- OpenGL-Ausgabe über OpenTK/GLControl
- Standard-Audioausgabe über NAudio
- Cartridge-Codepfade für No-MBC, MBC1 und MBC3; weitere Mapper sind nicht verlässlich implementiert
- optionale, vom Benutzer bereitzustellende DMG-/CGB-Boot-ROMs
- Tastatur- und OpenTK-Gamepad-Eingabe

Die Solution enthält seit Phase 0 einen kleinen automatisierten Smoke-Test für den rekonstruierten Audio Inspector. Eine vollständige Core-, Timing- oder Hardware-Conformance-Testsuite fehlt weiterhin; Kompatibilität und Genauigkeit sind daher noch nicht reproduzierbar belegt.

## Funktionsstatus

„Nicht freigegeben“ bedeutet hier: Der Code oder ein Menüeintrag kann vorhanden sein, die Funktion sollte aber bis zu einer Korrektur und Verifikation als deaktiviert beziehungsweise unzuverlässig behandelt werden.

| Bereich | Status | Bekannte Einschränkung |
| --- | --- | --- |
| CPU und Basisgrafik | experimentell | Keine Conformance- oder Regressionstests; Hardwaregenauigkeit nicht belegt. |
| DMG/CGB-ROM-Laden | experimentell | Nur mit legal beschafften ROM-Dumps testen; Mapper- und CGB-Abdeckung ist unvollständig. |
| MBC1/MBC3 | teilweise implementiert | RTC- und Persistenzverhalten sind nicht vollständig verifiziert. |
| MBC2/MBC4/MBC5 und weitere Mapper | nicht freigegeben | MBC5 wird aktuell fälschlich über den MBC3-Pfad behandelt; andere Mapper fehlen. |
| NAudio-Ausgabe | experimentell | APU-Timing, Kanal-Längenzähler, Noise-Erzeugung, Sample-Rate-Wechsel und Puffergrenzen benötigen Korrekturen. |
| OpenAL-Ausgabe | nicht freigegeben | Nicht Standardbackend; veralteter Thread-Abbruch und unsicherer Ressourcen-/Queue-Lifecycle. |
| WAV-Aufnahme | experimentell | Aufnahme und Stop können aus verschiedenen Threads auf denselben Writer zugreifen; Fehler werden nicht zuverlässig gemeldet. |
| Save States | nicht freigegeben | Das aktuelle v1-Binärformat besitzt einen Feldbreitenfehler und erfasst keinen vollständigen deterministischen Emulatorzustand. Laden kann einen teilweise veränderten Zustand hinterlassen. |
| Rewind | nicht freigegeben | Baut auf demselben unzuverlässigen Save-State-Format auf. |
| GameShark | teilweise implementiert | Einfache RAM-Writes sind vorhanden; Validierung und Nebenwirkungsgrenzen fehlen. |
| Game Genie | deaktiviert | Der Core-Parser kann Codes erkennen, wendet sie aber nicht im ROM-Lesepfad an; die UI weist sie deshalb zurück. |
| Link-Kabel/Netplay | nicht funktionsfähig | TCP-Dialog vorhanden, aber nicht mit der emulierten seriellen Schnittstelle verbunden; keine sichere oder taktgenaue Übertragung. |
| Debugger/Disassembler | intern/experimentell | Nicht in einen vollständigen Pause-/Step-Workflow integriert; mehrere Operand- und Grenzfallfehler. |

## Bauen und starten

Voraussetzungen:

- Windows
- .NET 8 SDK
- eine funktionierende OpenGL- und Audio-Umgebung

Im Repository-Root:

```powershell
dotnet restore .\nanoboy.sln
dotnet build .\nanoboy.sln -c Debug
dotnet run --project .\nanoboy\nanoboy.csproj
dotnet run --project .\tests\AetherBoy.SmokeTests\AetherBoy.SmokeTests.csproj -c Release
```

Diese Befehle folgen ausschließlich dem aktuellen SDK-Projekt. Historische Build-, Paket- und DLL-Kopien wurden in Phase 0 entfernt.

## ROMs, Boot-ROMs und Spielstände

Dieses Projekt erteilt **keine** Rechte an kommerziellen Spielen, Nintendo-Firmware, Boot-ROMs, Marken, Grafiken oder sonstigen Drittinhalten. Verwende nur ROM- und Firmware-Dumps, die du nach dem für dich geltenden Recht selbst verwenden darfst. Verbreite keine ROMs oder Boot-ROMs zusammen mit Quellcode oder Builds. Die Rechtslage unterscheidet sich je nach Land; diese Hinweise sind keine Rechtsberatung.

Historische `.gb`-/`.gbc`-Dateien und ein persönlicher `.sav` wurden in Phase 0 aus dem veröffentlichbaren Quellbaum entfernt. Diese lokale Arbeitskopie bewahrt sie ausschließlich unter dem durch `.gitignore` ausgeschlossenen Pfad `.local-assets/roms` auf. Für diese Dateien ist keine Weiterverbreitungslizenz dokumentiert; sie sind **nicht** von der GPL des Emulatorcodes umfasst und dürfen nicht zum Repository oder zu einem Release hinzugefügt werden.

Boot-ROM-Dateien wie `dmg_boot.bin` oder `gbc_boot.bin` werden nicht benötigt, um das Projekt zu bauen, und müssen – sofern ihre Nutzung legal ist – vom Benutzer selbst bereitgestellt werden.

## Repository-Hygiene

Die verbindliche Abhängigkeitsliste für neue Builds steht in `nanoboy/nanoboy.csproj`. Phase 0 hat `Release_v4.0`, den alten `packages.config`-Bestand, manuell kopierte OpenTK-DLLs, IDE-Zustand und Buildausgaben aus dem veröffentlichbaren Quellbaum entfernt. `.gitignore` schließt diese Artefakte sowie ROMs, Boot-ROMs, Save-Dateien, Logs und PDBs dauerhaft aus.

## Lizenz und Herkunft

Der Emulatorcode wird als **GNU General Public License Version 3** dokumentiert; siehe [LICENSE](LICENSE). Mangels einer ausdrücklichen „or later“-Erklärung in den aktuellen Metadaten wird hier konservativ `GPL-3.0-only` angegeben. Copyright und Urheberschaft verbleiben bei den jeweiligen ursprünglichen Autoren und späteren Beitragenden.

Die Projektchronik nennt Frédéric Meyer als ursprünglichen Entwickler (2014) und dokumentiert spätere ChiiBoy-/AetherBoy-Modifikationen. Die genaue Rechte- und Beitragshistorie sollte weiterhin anhand der ursprünglichen Quelle verifiziert werden.

Drittanbieterkomponenten haben eigene Lizenzen. Die aktuell direkt referenzierten Pakete und ihre Hinweise stehen in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
