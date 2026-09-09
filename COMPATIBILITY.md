# AetherBoy-Kompatibilität

Stand: 2026-09-08, erneut geprüfte GB/GBC-Phase-8-Auswahl plus erster
synthetischer Test des integrierten GBA-Backends. Die Matrix beschreibt
reproduzierbare Läufe von AetherBoy 4.8.0-alpha.1. Test-ROMs und kommerzielle
ROMs sind nicht Bestandteil des Repositorys.

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

Der erneute Lauf vom 2026-09-07 bestätigt diese Ergebnisse unverändert. Lokaler,
nicht veröffentlichter Bericht: `artifacts/compatibility-mgba-review-20260907.json`.
Zusätzlich bestehen 290 Code-/Runtime-/Frontend-Tests: 175 Core-, 87 Runtime-
und 28 Windows-Smoke-Tests. Der eigene Prototyp führt ein generiertes Programm
aus und erzeugt ein 240×160-Bild. Der Produktionsadapter führt eine zweite
synthetische ARM-ROM durch den vendorten CPU/Bus/PPU-Pfad. Die GBA-Regressionen
prüfen außerdem L/R, PSG und Direct Sound, Frameskip, Save States/Rewind,
SRAM/Flash/EEPROM, GPIO-RTC, getaktetes Serial samt lokaler Zwei-Core-Kopplung,
Mosaic, OBJ-Window und Blending, Raw-/CodeBreaker-/GameShark-Codes,
Datenspar-Diagnostik, STOP-Wakeup, Open-Bus-/Prefetch-/WRAM-Control-Grenzen und den
eingebauten HLE-BIOS-Pfad einschließlich Speichertransfer und Dekompression.
Das ist weiterhin **kein** Nachweis einer
vollständigen kommerziellen GBA-Spielkompatibilität.

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
- Serielle Bitübertragung mit interner, CGB-Fast- und externer Clock ist vorhanden; der GBA-Kern besitzt zusätzlich eine deterministische lokale Zwei-Core-Kopplung. Ein fertiger Zwei-Sitzungs-Host und TCP-/IPC-Transport fehlen.
- Timer-`rapid_toggle` und OAM-DMA-`sources-GS` bleiben subzyklische Buskonflikt-Grenzen.
- Spezialmapper wie MMM01, MBC4, Pocket Camera, HuC1 und HuC3 sind nicht freigegeben.
- MBC1M-Erkennung ist eine konservative Heuristik für 1-MiB-Abbilder mit passenden,
  gültigen Teilspiel-Headern. Beschädigte oder abweichende Multicarts können
  unerkannt bleiben; ein kommerzieller Durchspieltest steht aus.
- `.gba` ist über den gepflegten MIT-lizenzierten GBADotnet-Fork in der normalen
  Anwendung freigeschaltet. AetherBoy ergänzt inzwischen PSG, GPIO-RTC, Mosaic,
  sichere Raw-/CodeBreaker-/GameShark-v1/v2-RAM-Codes, getaktetes Serial mit
  lokaler Zwei-Core-Basis, Kerndiagnostik ohne ROM-Daten und einen HLE-BIOS-
  Fallback. Die Upstream-Tabelle beweist trotzdem nur 500 Frames und einen
  Screenshot; AetherBoy hat noch keinen kommerziellen Titel durchgespielt.
  Vollständig cycle-exaktes Timing/Open Bus, weitere Renderer-Kanten, fertiger
  Link-Host/Transport und Action Replay/PAR v3 bleiben Risiken. Der separate
  eigene Prototyp beherrscht weiterhin nur erste ARM-/Thumb-Gruppen,
  Basisspeicher und Mode 3.
- Ein Smoke-Pass beweist nur Stabilität im getesteten Zeitfenster, nicht die vollständige Spielkompatibilität.

Verwende ausschließlich Test-ROMs, Firmware und Spiele, die du rechtmäßig beziehen, dumpen und ausführen darfst. Externe Binärdateien dürfen nicht in Commits oder Releases aufgenommen werden.
