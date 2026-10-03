# GBADotnet source review and AetherBoy integration

Status: 2026-09-08. AetherBoy now has an experimental, in-window GBA runtime
based on the MIT-licensed GBADotnet core. This is an integration milestone, not
a claim that commercial games can be completed without defects.

## Provenance and imported scope

- Upstream: `https://github.com/DaveTCode/GBADotnet`
- Exact snapshot: `994c4b225c6e4277ada8d37bb9283f53827ee3e1`
- Commit date and subject: 2022-05-16, `Implemented FIFO DMA based audio channels`
- License: MIT, Copyright (c) 2022 David Tyler
- Imported product scope: 66 original C# core files (10,127 lines) and four
  generated C# files (11,351 lines)
- Excluded: upstream UI, web, benchmarks, compatibility checker, test ROMs,
  commercial-ROM screenshots, BIOS files, build outputs and Git history

The untouched clone is in the ignored local directory
`.local-tools/GBADotnet-reference-20260907`. The redistributable source snapshot
began in `third_party/GBADotnet.Core/src` and is now the maintained AetherBoy
fork. Generated decoder bodies are committed so
the product does not require the old Roslyn source-generator project or its
packages. The unused upstream Serilog package reference was not carried over.

## What the core provides

| Area | Imported implementation | Assessment |
| --- | --- | --- |
| CPU | Cycle-stepped ARM7TDMI pipeline, ARM and Thumb decoders, register banks, exceptions and software interrupts | Far beyond AetherBoy's small independent prototype; broad tests existed upstream, but timing/open-bus edges remain |
| Bus | BIOS, EWRAM, IWRAM, MMIO, palette, VRAM, OAM, ROM, wait states and prefetch | WRAM-control mirrors/disable, MMIO open-bus lanes, WAITCNT and 128-KiB prefetch boundaries are hardened; full cycle accuracy remains open |
| Timing | Central scheduler plus CPU/DMA coordination | Correct ownership shape for AetherBoy; not a proof of cycle accuracy |
| Graphics | Modes 0–5, text/affine backgrounds, sprites, windows, priorities and blending | Mosaic, OBJ-window enable, semi-transparent OBJ, bitmap VRAM holes and opaque RGBA output are hardened; further scanline timing edges remain |
| Audio | Sample scheduler, four PSG channels and timer/DMA FIFO A/B output | PSG length/envelope/sweep/wave/noise and Direct Sound are stereo-mixed, inspectable and host-mutable without altering timing |
| Saves | SRAM, 64/128 KiB Flash, EEPROM and GPIO-RTC | Marker scanning, flash reset/banking, EEPROM bit order/window/dummy bits and dynamic 512-byte/8-KiB sizing are connected to atomic `.sav`/`.sav.rtc` plus backups |
| Input | Ten GBA keys and keypad IRQ register model | A/B/Start/Select/D-pad plus L/R are connected to AetherBoy |
| Link | Serial/General Purpose/Joybus MMIO plus scheduled transfers | Internal/external-clock 8-/32-bit transfers can use a deterministic in-process two-core cable; disconnected multiplayer/UART timing, IRQ and state restore work; no app-level second-session or network transport |

The original snapshot contained well-known gaps rather than a stable release.
AetherBoy has removed the major runtime holes around PSG, RTC, disconnected
Serial, Mosaic, DMA address masks, SBC borrow and internal-memory-control writes.
The most important remaining comments concern sub-cycle bus timing, further
open-bus/rendering edges and unusual instructions.
ARM, Thumb and coprocessor-class undefined instructions enter the ARM7TDMI
exception vector instead of terminating the emulator.

## How it fits AetherBoy

`AetherBoy.Vendored.GBADotnet.Core` is a dependency-free `net10.0` wrapper around
the snapshot. `GbaProductionMachine` adapts the imported `Device` to the same
single-owner-thread contract already used by GB/GBC:

- `.gba` files from 192 bytes through 32 MiB route to the GBA backend; they can
  be selected in the Cartridge Vault or dropped onto the window;
- native 240×160 RGBA output becomes AetherBoy ARGB and travels through the
  dynamic frame exchange into the existing Sharp, Smooth and LCD-grid display;
- normal buttons use the existing input merge; GBA L/R use remappable keyboard
  and controller bindings, with Q/E and LB/RB defaults plus LT/RT fallbacks;
- stereo 16-bit 65,536-Hz PSG/Direct-Sound audio is converted to the host's mono
  stream; audio master, per-PSG-channel switches and inspection are connected;
- SRAM, Flash, EEPROM and RTC state load from and flush through AetherBoy's
  atomic save store with three rotating backups and integrity guards;
- model labels, ROM information, privacy-bounded core diagnostics and the Control Center identify
  GBA sessions; a GBA never tries to load a DMG/CGB boot ROM;
- a dedicated machine-state codec serializes CPU/pipeline, scheduler, bus,
  memory, PPU, APU, DMA, timers, interrupts, keypad, save hardware and current
  frame. Its AetherBoy envelope is compressed, integrity protected and bound to
  exact ROM and BIOS identities; it powers five slots and bounded rewind;
- an optional user-provided 16-KiB `gba_bios.bin` runs through the real vector
  path. Without it, AetherBoy's own HLE handles common BIOS services including
  memory transfer and compression. No firmware is included.

The September 2026 ROM-start review added an original six-instruction ARM IRQ
bridge for HLE boot. It follows the cartridge callback convention documented in
[Tonc](https://gbadev.net/tonc/interrupts.html#the-interrupt-process); supplied
BIOS images are never overwritten. CpuSet now interprets bit 24 as fixed-source
fill and bit 26 as word width. LZ77 uses the SWI-selected algorithm and the
upper 24-bit output length, accepting empty streams and nonstandard type tags
as the [mGBA BIOS reference](https://github.com/mgba-emu/mgba/blob/master/src/gba/bios.c)
does, while retaining bounds checks on back-references.

The changed HLE BIOS contents intentionally change the BIOS digest in GBA save
states: older HLE save states are rejected by the existing identity check.
Ordinary battery `.sav` data remains compatible.

The Linux replacement-ROM path pauses the previous owner and stabilizes GBA
at an instruction boundary before starting a candidate. This matters because
the vendored CPU still has static instruction scratch fields. Independently
running concurrent GBA sessions remain an architectural follow-up; the IRQ
bridge is not a fix for that separate issue.

GBA cheats use the shared runtime compiler/interpreter rather than GB address rules.
Raw address patches remain restricted to EWRAM/IWRAM; device commands can target
mapped memory and I/O. CodeBreaker master encryption, fills/lists, GameShark v1/v2,
Action Replay v3 conditions, hooks, indirect writes and reversible ROM patches are
implemented; see [the support matrix](CHEAT_SUPPORT.md) for precise exceptions.
A Thumb instruction-boundary callback runs hooked sets on the owner thread, once
per matching instruction. Cheat bus access preserves CPU wait-state accounting.
The cipher adaptation/reseed tables retain mGBA's MPL-2.0 notices; the existing
GBADotnet core license is unchanged. A deterministic local link peer is
present at core level; a second-session application host and networking are not.
Save states and rewind use only the GBA-specific contract; the GB format is
never reused.

## Verification performed in this repository

- The unmodified upstream core project was restored and built first.
- The vendored `net10.0` project builds without external package dependencies.
- A synthetic ARM ROM writes Mode 3 and VRAM through the imported CPU/bus. The
  AetherBoy adapter publishes a non-black 240×160 frame and reports a GBA ROM
  snapshot; the same tests check active-low L/R keypad bits and real Frameskip.
- State tests prove byte-identical continuation after restore, reject corruption
  and a different ROM, verify Rewind returns to an earlier machine cycle and
  enforce the 16-KiB BIOS contract.
- Runtime tests and the Windows frontend build pass with the imported project
  connected. No commercial ROM is stored in or required by the test suite.
- Maintained-fork tests cover cartridge metadata/markers, Flash banking and
  recovery, EEPROM bit order, ARM/Thumb exceptions and arithmetic, PSG/Direct
  Sound, Mosaic, GPIO-RTC, timed Serial, STOP, internal bus mirrors and HLE BIOS
  math/memory/compression, PPU blending/window/VRAM edges, bus/prefetch behavior
  dynamic EEPROM sizing, CodeBreaker/GameShark decoding, the local serial peer
  and privacy-bounded diagnostics. The repository currently passes 290 tests in total.

The upstream compatibility table labels a game “bootable” when 500 unattended
frames complete without an exception and records a final screenshot. FireRed,
LeafGreen, Emerald, Ruby and Sapphire meet that narrow upstream check, but this
does not demonstrate input, saving, audio, hours of gameplay or ROM-hack
compatibility. AetherBoy therefore needs real user playtesting before any title
is marked playable.

## Next hardening order

1. Exercise the intended Pokémon ROM hack from boot through an in-game save,
   restart, load, battle, map transition and at least one hour of continuous play.
2. Run legal homebrew CPU, memory, timing, PPU and save-hardware test ROMs
   through the AetherBoy adapter and keep exact expected results in CI.
3. Fix the highest-impact remaining open-bus/renderer/timing failures exposed
   by those ROMs and add regression tests before changing more hardware blocks.
4. Build the second-session Link Lab host on the deterministic local peer before
   considering IPC/network transport; keep PAR-v3 support behind explicit format
   validation and never reuse GB address rules.
5. Build a ROM-hash/version compatibility ledger from reproducible tests rather
   than inheriting the upstream screenshot table as a promise.
