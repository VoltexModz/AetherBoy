# Eigener GBA-Kern: Referenzkarte und Zielarchitektur

Stand: 2026-09-07. Dieses Dokument beschreibt den kleinen unabhängigen,
verwalteten AetherBoy-Prototyp. Der frühere Plan, ihn vor jeder GBA-Freigabe zum
Vollkern auszubauen, ist durch die spätere
[GBADotnet-Integration](GBADOTNET_REVIEW.md) überholt. mGBA wird weiterhin nicht
gelinkt, geladen oder als Binary ausgeliefert.

## Umfang der mGBA-Bestandsaufnahme

Das bereitgestellte Archiv wurde in einem ignorierten Arbeitsverzeichnis
vollständig katalogisiert. 895 relevante Quell-, Header-, Build- und
Lizenz-/Portierungsdateien aus `src`, `include` und dem Repository-Root
wurden in die strukturelle Suche einbezogen. Die sechs für die beiden
Handheld-Kerne zentralen Quellmodule ergeben folgende Größenordnung:

| Modul | Dateien | Zeilen | Rolle |
| --- | ---: | ---: | --- |
| `src/arm` | 10 | 3.531 | ARM7TDMI-Zustand, ARM-/Thumb-Decoder und ISA |
| `src/gba` | 45 | 22.184 | GBA-Maschine und Geräte |
| `src/gb` | 34 | 14.466 | GB/GBC-Maschine; enthält gemeinsam genutzte Audiokonzepte |
| `src/sm83` | 7 | 1.845 | GB-CPU |
| `src/core` | 23 | 8.381 | gemeinsame Core-Verträge |
| `src/util` | 56 | 15.650 | Ereignisse, Speicher-, Datei- und Hilfsinfrastruktur |

Die Zählung umfasst in jedem Modul die Build-Datei. Sie ist eine reproduzierbare
Vollinventur, keine Behauptung, jede der rund 66.000 Zeilen sei formal verifiziert.
Semantisch tief geprüft wurden die GBA-/ARM-Buildgrenzen, Zustands- und
Schedulingpfade sowie CPU, Memory/IO, DMA, Timer, Video, Audio, Saves, BIOS/HLE,
Serial und Cartridge-Erweiterungen. Renderer-, Cheat-, Debugger- und
Plattformvarianten wurden ihrer Rolle und Abhängigkeit nach eingeordnet.

## Was der Referenzkern tatsächlich trennt

| Schicht | Zentrale Referenzbereiche | Erkenntnis für AetherBoy |
| --- | --- | --- |
| CPU | `src/arm/arm.c`, `decoder*.c`, `isa*.c` | ARM und Thumb teilen Zustand und Ausnahmevertrag, brauchen aber klare Decodergrenzen. |
| Maschine | `src/gba/gba.c`, `core.c` | Reset, Frame-Lauf und Hostvertrag gehören nicht in einzelne Geräte. |
| Bus/IO | `memory.c`, `io.c`, Header in `include/.../gba` | Adressdekodierung, Open-Bus, Waitstates und MMIO-Nebenwirkungen müssen zentral abgestimmt sein. |
| Zeit | `timer.c`, `dma.c`, `video.c` | CPU-Zyklen allein genügen nicht; Ereignisse von PPU, Timer, DMA und IRQ konkurrieren auf einer Zeitachse. |
| Bild | `renderers/software-*`, `video-software.c` | Modi 0–5, Hintergründe, Fenster, Blending und OBJ sind ein eigener PPU-/Renderer-Block. OpenGL ist nur Hostbeschleunigung. |
| Ton | `gba/audio.c` plus `../gb/audio.c` | GBA kombiniert ältere PSG-Kanäle mit Direct-Sound-FIFOs und timergetriebenem DMA. |
| Persistenz | `savedata.c`, `serialize.c` | SRAM, Flash und EEPROM brauchen Erkennung, Befehlszustand und eigene State-Versionen. |
| Boot/BIOS | `bios.c`, `hle-bios.c` | SWI, Exceptions und BIOS-Schutzverhalten sind Teil der Maschine, nicht bloß eine optionale Datei. |
| Link/Zubehör | `sio*`, `cart/gpio.c`, weitere `cart/*` | Erst nach Singleplayer-Kern; RTC/GPIO kann jedoch für einzelne Spiele früher nötig sein. |
| Werkzeuge | Cheats, Debugger, Testordner | Diagnoseinterfaces getrennt halten; Debugfunktionen dürfen Timing nicht verändern. |

Die größten GBA-Dateien bestätigen die Risikoverteilung: GL-Renderer 1.968,
Memory 1.821, Core 1.695, e-Reader 1.562, Software-Renderer 1.030, IO 1.013,
Lockstep 1.001, Maschinenlogik 974, BIOS 951 und Save-Hardware 743 Zeilen.
Für ein erstes normales Spiel sind e-Reader, GL, Cheats und Link nicht kritisch;
Memory/IO, CPU, BIOS, Video, Timer/DMA/IRQ und Saves sind es.

## AetherBoy-Zielstruktur

```text
EmulationSession / Owner Thread
             |
         GbaSystem  ------- Diagnose / deterministische Zustände
        /    |    \
  Arm7Cpu  Scheduler  GbaMemoryBus/MMIO
     |       / |  \       /   |    \
 ARM+Thumb PPU Timer DMA  IRQ  Input Save-Geräte
              |              \
         GbaVideo          GbaAudio
```

Regeln für den Ausbau:

- `GbaSystem` besitzt und taktet alle Geräte; kein Gerät startet eigene Threads.
- CPU-Speicherzugriffe gehen ausschließlich über den Bus. MMIO-Register sind
  Geräteverträge, keine frei beschreibbare Byte-Map.
- Der Scheduler arbeitet in GBA-Masterzyklen und liefert das nächste Ereignis;
  Framegrenzen sind Resultat der PPU, keine pauschale CPU-Schleife.
- PPU-Rendering und Audioerzeugung bleiben vom Host-Present/Audio-Gerät getrennt.
- GBA-Saves und Save States erhalten eigene Formate, ROM-Identität und
  Core-Schema. Kein GB-Zustand wird als GBA-Zustand interpretiert.
- Nicht implementierte Befehle und Register stoppen mit Diagnose. Keine
  stillen NOPs oder pauschal beschreibbaren IO-Register vortäuschen.

## Heute vorhandener, realer Pfad

```text
generiertes .gba-Bytearray
  -> Fetch ab 0x08000000
  -> eigener ARM-Decoder / BX nach Thumb
  -> eigener Thumb-1-Decoder / Datenpfad
  -> STR nach 0x04000000 (DISPCNT)
  -> STR nach 0x06000000 (VRAM)
  -> eigener BGR555-Mode-3-Renderer
  -> veröffentlichtes 240x160-ARGB-Bild
  -> BMP + stabiler Frame-Hash
```

Damit läuft erstmals Code auf unserem eigenen GBA-Ausführungspfad. Vorhanden
sind erste ARM-/Thumb-1-Befehlsgruppen, Basisspeicherregionen, ROM-Mirror,
VRAM-Mirror, SRAM-Fläche und Mode 3. Das ist bewusst ein vertikaler Prototyp,
noch kein kommerziell kompatibler Emulator.

## Kritischer Pfad bis Pokémon FireRed-basierte ROM-Hacks starten

1. ARM-Decoder vervollständigen: Multiply, Halfword/Signed Transfer, Block
   Transfer, PSR, SWI und Ausnahme-/Bankregisterverhalten.
2. Thumb-1 vollständig gegen unabhängige Instruktions- und Randfalltests härten;
   Spiele wechseln sehr früh in Thumb-Code.
3. BIOS-Vertrag/HLE, IRQ-Controller, Timer und DMA mit einem gemeinsamen
   Scheduler implementieren.
4. PPU mindestens für tilebasierte Modi 0/1/2, OBJ, Fenster, Blending und
   VBlank/HBlank-Register vervollständigen.
5. Keypad samt L/R sowie Audio-PSG, Direct Sound und FIFO-DMA anbinden.
6. Save-Erkennung für SRAM/Flash/EEPROM und RTC/GPIO implementieren.
7. Erst synthetische Test-ROMs, dann frei verfügbare GBA-Testprogramme und
   schließlich eine lokal bereitgestellte, rechtmäßig verwendbare Spiel-ROM
   als nicht veröffentlichten Smoke-Test verwenden.

Ein FireRed-basiertes Spiel ist daher kein sinnvoller erster Test des kleinen
Prototyps. Es benötigt gleichzeitig Thumb, BIOS/SWI, IRQ/Timer/DMA, tilebasierte
Grafik, Sprites, Eingabe, Audio und Flash-Verhalten. Die normale `.gba`-Auswahl
verwendet inzwischen den breiteren GBADotnet-Pfad; diese Liste bleibt der
Härtungsplan für den unabhängigen Kern.

## Herkunft

Die Referenzdateien verbleiben ignoriert und behalten ihre MPL-2.0-Hinweise.
Der unabhängige Prototyp wurde aus dokumentierten Hardwareverträgen, eigenem
Design und selbst erzeugten Tests aufgebaut. Weil mGBA-Quellcode eingesehen
wurde, wird kein formales Clean-Room-Verfahren behauptet. Details und konkrete
Quellen stehen in [MGBA_REVIEW.md](MGBA_REVIEW.md). Der produktiv verdrahtete
GBADotnet-Snapshot ist ausdrücklich Drittcode und separat lizenziert.
