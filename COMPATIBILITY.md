# AetherBoy-Kompatibilität

Stand: Phase 7, 2026-08-09. Die Matrix beschreibt reproduzierbare Headless-Läufe von AetherBoy 4.7.0-alpha.1. Test-ROMs und kommerzielle ROMs sind nicht Bestandteil des Repositorys.

## Verifizierte Matrix

| Suite | Ergebnis | Einordnung |
| --- | ---: | --- |
| Blargg `cpu_instrs/individual` | 11/11 bestanden | Alle Einzeltests sind ein Pflichtgate. |
| Blargg `instr_timing` | 1/1 bestanden | Instruktionskosten und Bus-Scheduler. |
| Mooneye Timer | 12/13 bestanden | Nur `rapid_toggle` bleibt subzyklisch offen. |
| Mooneye PPU | 10/12 bestanden | Offen: Sprite-Grenzquantisierung und eine LCD-On-STAT-Lesetabelle. |
| Mooneye OAM-DMA-Auswahl | 5/6 bestanden | Basis, Register, Start, Timing und Restart bestehen; `sources-GS` bleibt offen. |
| Blargg DMG Sound Singles | 6/12 bestanden | Register, Length, Trigger, Overflow-on-Trigger, Power-Length und Register-after-Power bestehen. |
| Blargg CGB Sound Singles | 7/12 bestanden | Zusätzlich bestehen CGB-Wave-Trigger und die CGB-Power-Variante. |
| Lokale Spiel-Smokes | 3/3 bestanden | Je 600 Frames ohne Ausnahme, mit unterschiedlichem Frame-SHA-256. Keine Aussage über vollständiges Durchspielen. |

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

- Die PPU besitzt noch keinen vollständigen dotgenauen Pixel-FIFO; Mid-Scanline-Abbrüche und zwei LCD-/Sprite-Grenzen bleiben angenähert.
- Die APU besitzt noch keinen analogen Hochpassfilter. Vollständige Sweep-Negate-Semantik und DMG-Wave-RAM-Zugriffskollisionen sind offen.
- Serielle Bitübertragung, TCP-Link-Kabel und zwei gekoppelte Emulatorinstanzen fehlen.
- Spezialmapper wie MMM01, MBC4, Pocket Camera, HuC1 und HuC3 sind nicht freigegeben.
- Ein Smoke-Pass beweist nur Stabilität im getesteten Zeitfenster, nicht die vollständige Spielkompatibilität.

Verwende ausschließlich Test-ROMs, Firmware und Spiele, die du rechtmäßig beziehen, dumpen und ausführen darfst. Externe Binärdateien dürfen nicht in Commits oder Releases aufgenommen werden.
