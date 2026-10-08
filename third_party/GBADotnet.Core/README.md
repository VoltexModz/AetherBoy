# Vendored GBADotnet core

This directory contains AetherBoy's maintained source fork of the MIT-licensed
[DaveTCode/GBADotnet](https://github.com/DaveTCode/GBADotnet) core at commit
`994c4b225c6e4277ada8d37bb9283f53827ee3e1` (2022-05-16).

The original 66 core source files and four source-generator outputs are kept in
`src`, together with AetherBoy's maintained extensions. Generated output is
committed deliberately so AetherBoy does not need the old Roslyn generator
package at build time. The upstream Serilog reference was unused by core
sources and is not carried into this project.

The initial import is preserved in the ignored reference clone. This maintained
copy targets .NET 10 and may be corrected or extended directly. AetherBoy's
first hardening pass fixes cartridge-header widths, allocation-heavy save-marker
detection, `SRAM_F` recognition, flash command recovery/banking, EEPROM bit
ordering and ARM7TDMI undefined-instruction exceptions. The maintained fork now
also owns a complete integrity-checked machine-state codec and instance-local
DMA pipeline state. Later passes add the four GBA PSG channels, harden Direct
Sound FIFO handling, implement cartridge GPIO/RTC and serial-register timing,
fix DMA/PPU/ARM edge cases, model STOP wake-up and provide an independently
written high-level BIOS-service fallback when no user BIOS is present. The
latest pass hardens OBJ-window and semi-transparent blending, bitmap VRAM
mapping, open-bus/WAITCNT/prefetch behavior, dynamic EEPROM sizing and reset
semantics across cartridge hardware, timers and interrupts. The current pass
adds a deterministic local two-core serial peer and bounded diagnostics for
BIOS calls, undefined instructions and unmapped I/O without ROM payload data.
The September 2026 link expansion implements normal 8/32-bit and two-device
multiplayer 16-bit SIO, RCNT mode gating, transfer timing/status/interrupts and
disconnect handling. In-flight ARM/Thumb instruction scratch is now CPU-local,
including the maintained generated LDM/STM output: interleaving two devices must
not share load/store, multiply, branch or swap intermediates. The debug register
name map supports aliases without lazy-initialization races. Device states write
schema 6 for ordinary cartridges and schema 7 for e-Reader cartridges. Schema 5/6
remain readable for non-e-Reader cartridges; battery save formats are unchanged. Unilateral
capture/restore is forbidden while attached to a local cable.

The e-Reader extension (`src/Rom/EReader.cs` and `EReaderDotCode.cs`) is adapted
from mGBA and licensed **MPL-2.0**, not MIT. See `../mgba-ereader/NOTICE.txt` and
`LICENSE.txt`. It implements raw digital strip input, cartridge registers,
Game Pak IRQ and a bounded FIFO with full accessory state. Photographic input,
decoded BIN conversion and paired-game UI are not included in this first pass.
Optional tests now run owner-provided US e-Reader firmware and digital cards,
without bundling their bytes. Complete Donkey Kong-e and Balloon Fight-e sets
boot and produce changing PCM audio; tests cover full-state replay, application
flash saving and reopening without rescanning. Kirby Slide Puzzle, Manhole and
Air Hockey add standalone-card regressions. Other regions and paired-game
transfers remain unverified; passing these cases is not universal compatibility.

The real NES-e runs exposed two shared ARM7TDMI/Direct Sound defects fixed in
October 2026: MSR now merges only selected CPSR/SPSR fields (flags-only writes
must not unmask IRQs or corrupt an IRQ return), and sound FIFO DMA1/2 forces four
32-bit transfers irrespective of the programmed width/count. FIFO refill is
requested at sixteen bytes remaining without restarting an active burst.
ROM-free `GbaStatusRegisterTests` and `GbaSoundDmaTests` cover both corrections.

AetherBoy wraps the core payload with ROM/BIOS identity, compression, rewind,
atomic save/RTC persistence and safe raw-RAM, CodeBreaker and GameShark v1/v2
programs. Product-window and host storage integration remain in AetherBoy's
Runtime project so the core stays platform-neutral. Remaining limits include
native Linux link UI, network/wireless/four-player transport, fully
cycle-exact timing/open-bus behavior, some PPU edge cases and Action Replay/PAR
v3 cheat formats. See
`../../docs/GBADOTNET_REVIEW.md`, `../../docs/GBA_LOCAL_LINK_HANDOFF.md` and
`LICENSE.md`. The Windows two-player Link Lab is experimental; synthetic serial
and CPU-isolation tests do not establish commercial-game compatibility.
