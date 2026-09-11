# AetherBoy – Project Status and Provenance

Status date: 11 September 2026
Version: 4.8.0-alpha.1  
Branch: `development`

## Executive summary

AetherBoy is now a modern Windows application with an additional first native
Linux host for Wayland and Hyprland. It emulates Game Boy, Game Boy Color and
experimental Game Boy Advance. GB and GBC originate from the historic
NanoBoy/ChiiBoy codebase, but their architecture, hardware model, interface,
storage, input and tests have been substantially reworked. GBA has two paths: a
small independent learning/regression core and a production source fork of the
MIT-licensed GBADotnet core maintained directly in this repository.

The current .NET 10 code builds without warnings and passes at least 354
deterministic tests on Windows. This proves many defined hardware and application contracts, but it does
not yet prove a complete playthrough of a commercial GBA game.

## Origins and sources used

| Component | Origin | How it is used | License/note |
| --- | --- | --- | --- |
| Historic GB/GBC core and WinForms application | NanoBoy by Frédéric Meyer, later ChiiBoy Color | Starting point, then extensively redesigned and extended | GPL-3.0-only; original copyright and license notices remain |
| Product identity, brand and current interface | AetherBoy project | Original design and implementation | Part of the GPL project |
| Production GBA core | DaveTCode/GBADotnet commit `994c4b225c6e4277ada8d37bb9283f53827ee3e1`, dated 16 May 2022 | Source snapshot imported directly and developed as an AetherBoy fork | Full MIT license at `third_party/GBADotnet.Core/LICENSE.md` |
| mGBA | source archive supplied by the project owner | Architecture and behavioural reference; no mGBA core linked and no mechanical port | MPL-2.0; details in `docs/MGBA_REVIEW.md` |
| GBA hardware behaviour | public hardware documentation and documented BIOS contracts | Used to validate our C# implementations and tests | No Nintendo BIOS file included |
| Audio output | NAudio.WinMM 2.3.0 | Windows adapter outside the emulator cores | MIT |
| Linux desktop | SDL3-CS and SDL3-CS.Linux 3.4.16 | Native Wayland adapter with no X11/XWayland fallback | Zlib |
| Test platform | MSTest.Sdk 4.3.2 | Automated core, runtime and Windows tests | MIT |

The complete dependency attribution is in `THIRD_PARTY_NOTICES.md`. ROMs,
Nintendo firmware, third-party game artwork and diagnostic archives containing
game data are not part of the repository.

## What we modernised

### Architecture and runtime

- Migrated to .NET 10 with reproducible restore and locked dependencies.
- Split the code into a portable core, runtime contract, Windows frontend and
  native Linux Wayland host.
- Added an exclusive emulation owner thread with typed commands instead of UI
  code directly mutating core state.
- Added immutable snapshots and synchronised frame/audio transfer between the
  emulation and UI threads.
- Generalised video geometry for 160×144 GB/GBC and 240×160 GBA output.
- Added Windows and Linux CI gates with an enforced minimum test count plus a
  native Wayland-host build and Hyprland-profile check.

### Interface and controls

- Replaced the old generic UI with the original Aether Wave identity,
  multi-resolution icon and borderless application chrome.
- Added the display stage, Cartridge Vault, drag and drop, recent files,
  command deck and live session instruments.
- Added a central Control Center for video, audio, input, saves, system options
  and local diagnostics.
- Restyled cheats, audio, controls, save safety and information tools using the
  same Aether visual language.
- Added sharp, smooth and LCD-grid display modes with correct aspect ratios.
- Added Windows Gaming Input with XInput fallback, hot-plug and persisted
  keyboard/controller remapping, including GBA L/R.
- Added an SDL3 Wayland host with an Aether-native shell, ROM drag and drop,
  keyboard/gamepad hot-plug, fullscreen and a Hyprland-specific desktop profile.

### Saves and states

- Added atomic battery saves with write-through, SHA-256 integrity guards and
  three rotating backup generations.
- Added automatic recovery from the newest valid generation.
- Added a Save Safety Center for local inspection and controlled restoration.
- Added five save-state slots, quick save/load and rewind for GB, GBC and GBA.
- States are versioned, integrity checked and bound to the exact ROM, hardware
  model and BIOS identity when applicable.
- Connected GBA SRAM, Flash64, Flash128, EEPROM and GPIO RTC persistence.

## GB/GBC core improvements

- Hardened CPU, interrupt, HALT/STOP, EI/DI, stack and bus behaviour with
  regression tests.
- Refined timer edges, delayed TIMA reload and write collisions.
- Improved PPU timing, STAT edges, VRAM/OAM access windows, window clipping,
  sprite selection and DMG/CGB priority behaviour.
- Integrated timed OAM DMA and CGB general/HBlank DMA into deterministic state.
- Improved APU frame sequencing, sweep, envelope, length, wave RAM behaviour,
  high-pass filtering and stereo routing.
- Tested MBC1, MBC1M, MBC2, MBC3 with RTC and MBC5 with rumble masking.
- Added serial bit transfer with normal, CGB fast and external clocks to the
  deterministic state contract.

The selected external GB/GBC conformance matrix reaches 67 of 70 runs. The
three remaining differences are explicitly documented as informative results
and are not presented as passing runs.

## Building and extending GBA support

### Independent prototype

`nanoboy/Core/Advance` contains an independently written small ARM/Thumb, bus
and Mode 3 path. It is deliberately not presented as a complete game core. The
`AetherBoy.GbaProbe` tool runs a generated test program and produces a
reproducible 240×160 image.

### Production core

The GBADotnet source was not connected as an unchanged black box or external
binary. It remains readable under `third_party/GBADotnet.Core`, builds without
the old generator/logging infrastructure and is maintained inside AetherBoy.
Work on that foundation includes:

- complete cartridge header fields and allocation-free save-marker scanning;
- Flash command recovery and 128 KiB banking;
- EEPROM bit order, address window, dummy bits and dynamic capacity;
- ARM/Thumb undefined exceptions, SBC borrow and DMA address masking;
- PPU mosaic, object windows, semi-transparent sprites, bitmap VRAM edges and
  opaque framebuffer alpha;
- WAITCNT, prefetch boundaries, open-bus lanes and internal WRAM control;
- all four PSG channels plus Direct Sound with host channel controls;
- GPIO RTC with save-state data and protected `.sav.rtc` persistence;
- timed 8/32-bit serial registers, IRQ and in-flight transfer restoration;
- an independently written HLE BIOS fallback for reset/wait/halt, maths,
  memory transfer, affine calculations and documented decompression services;
- complete compressed, ROM/BIOS-bound GBA state and bounded rewind;
- safe raw patches, common CodeBreaker direct/logical/conditional programs and
  raw or encrypted GameShark v1/v2 RAM writes;
- deterministic local two-core 8/32-bit serial exchange, including an external
  clock peer;
- bounded local core diagnostics for BIOS calls, unknown instructions,
  unmapped I/O and link matches without ROM bytes, write values or telemetry.

## Currently available functionality

| Area | GB/GBC | GBA |
| --- | --- | --- |
| ROM opening, Vault and drag/drop | Yes | Yes |
| Native geometry and display filters | Yes | Yes |
| Keyboard and gamepad | Yes | Yes, including L/R |
| Audio and four channel switches | Yes | Yes, PSG plus Direct Sound |
| Protected battery saves | Yes | Yes |
| Save states and rewind | Yes | Yes |
| Cheats | GB GameShark RAM | Raw, CodeBreaker, GameShark v1/v2 |
| Control Center diagnostics | Host status | Host status plus core events |
| Local link hardware foundation | Serial clocks present | Two-core peer present |

## Verification

- Release build: 0 warnings, 0 errors.
- 175 core tests.
- 100 runtime tests.
- 38 desktop integration tests: 37 passed, with one Wayland-only UI test skipped outside a Wayland session.
- 42 Windows smoke tests, including central storage, migration, privacy and live-export coverage for development diagnostics.
- Current Windows solution run: 355 tests, 354 passed, 0 failed and 1 skipped.
- Native Ubuntu build and `linux-x64` publish: 0 warnings, 0 errors. The
  published host detected its Hyprland profile and entered a genuine Wayland
  window loop under WSLg.
- The self-contained Windows x64 build was launched and remained stable during
  the automated startup smoke check.
- The package was scanned for ROM, BIOS and save files: 0 matches.

All GBA regressions use generated programs or synthetic data. No commercial ROM
or Nintendo firmware is required by the automated test suite.

## Honest limitations

- GBA remains experimental until real games pass boot, graphics, audio, battle,
  map transition, in-game save and longer-session tests.
- The production GBA core is not fully cycle exact. Rare open-bus, prefetch,
  DMA, PPU and APU edges can still differ from hardware.
- The local GBA link foundation does not yet have a finished two-session host,
  visible Link Lab flow or TCP/IPC/internet transport.
- Action Replay/PAR v3, complex CodeBreaker hook/fill/list commands and
  encrypted master-code streams are not supported.
- Special hardware such as Pocket Camera, HuC1/HuC3, MMM01 and other uncommon
  mappers is not released.
- Experimental GBA save states older than core schema 5 are incompatible.
- The central AppData library and automatic development recording currently apply
  to Windows. Linux uses its XDG settings and ROM-adjacent saves; see
  `docs/LINUX_WAYLAND.md` for frontend details.
- AetherBoy is alpha software and is not yet claimed as a replacement for
  established reference emulators.

## Build and test

```powershell
dotnet restore ./nanoboy.sln --locked-mode --configfile ./NuGet.config
dotnet build ./nanoboy.sln -c Release --no-restore
dotnet test --solution ./nanoboy.sln -c Release --no-build --no-restore --minimum-expected-tests 354
dotnet run --project ./nanoboy/nanoboy.csproj -c Release --no-build
```

Create a self-contained Windows x64 package with:

```powershell
dotnet publish ./nanoboy/nanoboy.csproj -c Release -r win-x64 --self-contained true
```

Build and run the native Wayland host on Linux with:

```bash
bash scripts/build-linux.sh
bash scripts/run-linux.sh "/path/to/your-game.gba"
```

## Recommended next milestone

Phase 9 should qualify real-game playability. Test a legally obtained GBA game
from boot through map navigation, menus and battle, then verify in-game saving,
restart, save states, rewind, audio and at least 30–60 minutes of continuous
play. Each discovered defect should first become reproducible and then receive
a synthetic core regression test.

This document is a technical provenance and progress overview, not legal
advice. Users must ensure that their ROM and firmware use is permitted in their
jurisdiction.
