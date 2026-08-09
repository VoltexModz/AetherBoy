# AetherBoy-Kompatibilität

Stand: Phase 8, 2026-08-09. Die Matrix beschreibt reproduzierbare Headless-Läufe von AetherBoy 4.8.0-alpha.1. Test-ROMs und kommerzielle ROMs sind nicht Bestandteil des Repositorys.

## Verifizierte Matrix

| Suite | Ergebnis | Einordnung |
| --- | ---: | --- |
| Blargg `cpu_instrs/individual` | 11/11 bestanden | Alle Einzeltests sind ein Pflichtgate. |
| Blargg `instr_timing` | 1/1 bestanden | Instruktionskosten und Bus-Scheduler. |
| Mooneye Timer | 12/13 bestanden | Nur `rapid_toggle` bleibt subzyklisch offen. |
| Mooneye PPU | 11/12 bestanden | Nur `lcdon_timing-GS` bleibt als LCD-On-STAT-Lesekante offen. |
| Mooneye OAM-DMA-Auswahl | 5/6 bestanden | Basis, Register, Start, Timing und Restart bestehen; `sources-GS` bleibt offen. |
| Blargg DMG Sound Singles | 12/12 bestanden | Register, Length, Trigger, Sweep, Power, Wave-Zugriff, Retrigger-Korruption und Wave-Schreiben bestehen. |
| Blargg CGB Sound Singles | 12/12 bestanden | Die vollständige ausgewählte CGB-Sound-Einzelsuite besteht. |
| Lokale Spiel-Smokes | 3/3 bestanden | Je 600 Frames ohne Ausnahme, mit unterschiedlichem Frame-SHA-256. Keine Aussage über vollständiges Durchspielen. |

Das vollständige Beispielmanifest umfasst 70 Läufe: **67 bestanden**, zwei informative Fehlschläge, ein informativer Timeout und **keine blockierenden Fehler**. Die drei bekannten Abweichungen sind `rapid_toggle.gb`, `lcdon_timing-GS.gb` und `sources-GS.gb`.

Mooneye-Boot- und Modelltests, die eine echte Nintendo-Boot-ROM voraussetzen, werden ohne lokal und rechtmäßig bereitgestellte Firmware nicht als Emulatorfehler gezählt. Informationsläufe dürfen fehlschlagen, beeinflussen aber den Exitcode eines Manifests nicht; Pflichtläufe tun dies.

## Reproduzieren

Die Beispielkonfiguration erwartet ignorierte, lokal bereitgestellte Verzeichnisse unter `.local-assets/conformance` und `.local-assets/roms`:

```powershell
dotnet run --project ./tools/AetherBoy.Conformance/AetherBoy.Conformance.csproj -c Release -- `
  --manifest ./tools/AetherBoy.Conformance/compatibility.example.json `
  --json ./artifacts/compatibility.json
```

`conformance` verlangt eine erkannte Pass-Signatur. `smoke` verlangt einen begrenzten Lauf ohne Ausnahme und protokolliert End-PC sowie SHA-256 des veröffentlichten Frames. Die CLI verändert keine ROM und speichert temporäre SRAM-Daten außerhalb der Suite.

## Bekannte Grenzen

- Die PPU besitzt noch keinen vollständigen dotgenauen Pixel-FIFO; Mid-Scanline-Abbrüche und die LCD-On-STAT-Lesekante bleiben angenähert.
- Die ausgewählten DMG-/CGB-Soundsuiten bestehen vollständig. Seltene modell- und revisionsabhängige Analog-/APU-Effekte sind damit nicht umfassend bewiesen.
- Serielle Bitübertragung mit interner, CGB-Fast- und externer Clock ist vorhanden; TCP-/IPC-Link-Transport und zwei gekoppelte Emulatorinstanzen fehlen.
- Timer-`rapid_toggle` und OAM-DMA-`sources-GS` bleiben subzyklische Buskonflikt-Grenzen.
- Spezialmapper wie MMM01, MBC4, Pocket Camera, HuC1 und HuC3 sind nicht freigegeben.
- Ein Smoke-Pass beweist nur Stabilität im getesteten Zeitfenster, nicht die vollständige Spielkompatibilität.

Verwende ausschließlich Test-ROMs, Firmware und Spiele, die du rechtmäßig beziehen, dumpen und ausführen darfst. Externe Binärdateien dürfen nicht in Commits oder Releases aufgenommen werden.
