# Game Boy Advance in AetherBoy

Status: **experimental GBA backend integrated into the normal AetherBoy window**
(2026-09-08). Commercial games are not yet certified as playable end to end.

AetherBoy now contains two GBA tracks:

1. The small independent prototype in `nanoboy/Core/Advance` remains a focused
   learning and regression path for ARM/Thumb, bus mapping and Mode 3.
2. The production session uses a vendored source snapshot of the MIT-licensed
   [GBADotnet](https://github.com/DaveTCode/GBADotnet) core. Its exact provenance,
   architecture and limitations are documented in
   [docs/GBADOTNET_REVIEW.md](docs/GBADOTNET_REVIEW.md).

The earlier plan to finish a complete new ARM7/PPU/APU implementation before
opening any game is superseded. The imported managed C# core gives us a much
broader base while remaining inspectable and modifiable inside this repository.

## What works in the application

- `.gba` appears in the Cartridge Vault picker and works through drag-and-drop,
  recent files and command-line opening.
- GBA ROMs route to `GbaProductionMachine`, never through the DMG/CGB loader.
- The existing AetherBoy frame pipeline switches from 160×144 to native 240×160
  and preserves the GBA 3:2 aspect ratio in all display filters.
- A/B, Start, Select and the D-pad use the existing keyboard/controller mapping.
  L/R default to Q/E and LB/LT or RB/RT, and their keyboard and gamepad buttons
  are remappable in the same Input Matrix.
- The normal Frameskip setting now controls only GBA frame presentation while
  the complete hardware timeline continues to execute.
- F5/F8 and all five `.ss1`–`.ss5` slots use a dedicated GBA state format. It
  serializes CPU/pipeline, scheduler, bus, RAM, PPU, APU, DMA, timers, IRQ,
  keypad, cartridge save controllers and frame state; ROM, schema and BIOS
  identity are verified before restore.
- Rewind captures a GBA state every four frames for about ten seconds of local
  history, bounded to 150 entries and 96 MiB.
- The four legacy PSG channels now run from a 512 Hz frame sequencer with pulse,
  sweep, envelope, wave and noise state. They are mixed in stereo with both
  timer/DMA Direct Sound FIFOs. The existing four channel switches and Audio
  Inspector work without changing emulated timing.
- SRAM, Flash64, Flash128 and EEPROM storage is routed through AetherBoy's
  atomic `.sav` writer, rotating backups and integrity guards. EEPROM begins at
  512 bytes, expands safely to 8 KiB when the cartridge selects the larger
  address protocol and restores either existing file size on the next launch.
- GPIO cartridges with an `SIIRTC_V`/`RTC_V` marker receive a serial BCD clock,
  12/24-hour control, Save-State state and an integrity-protected `.sav.rtc`
  sidecar with the same rotating-backup policy.
- Safe GBA cheats accept explicit `ADDRESS:VALUE` patches, common CodeBreaker
  direct/logical/conditional lines and GameShark v1/v2 direct RAM writes in raw
  or encrypted form. Multi-line programs use `+` or `;`; every resulting target
  must be aligned EWRAM/IWRAM, so ROM/MMIO writes remain rejected.
- Serial registers, internal-clock 8-/32-bit transfer duration, multiplayer/
  UART disconnected behavior, completion IRQ and in-flight Save-State restore
  are modeled. A deterministic in-process cable can now match two core
  instances, including an external-clock peer; network transport and the
  two-window user flow remain separate future work.
- A bounded local GBA diagnostics buffer records HLE-BIOS service classes,
  undefined ARM/Thumb instruction locations, unmapped I/O access classes and
  local-link matches. It contains no ROM bytes, write values or file paths and
  sends no telemetry; recent events appear in Control Center.
- The title, GBA model, detected save hardware, ROM hash and 240×160 geometry
  flow through snapshots into the UI and diagnostics.
- No proprietary Nintendo BIOS is shipped. A user-provided `gba_bios.bin` is
  accepted only at the hardware size of 16,384 bytes. Without it, a built-in
  HLE fallback covers reset/wait/halt, integer math, memory transfer, affine
  setup and BitPack/LZ77/Huffman/RLE/differential decompression. Supplying a
  real BIOS keeps the normal exception-vector path instead.

## Deliberately not claimed for GBA

- Action Replay/PAR v3, encrypted CodeBreaker master-code streams and complex
  fill/hook/list commands are absent. Unsupported commands fail explicitly.
- The local two-core cable is an engineering foundation, not a finished Link
  Lab workflow. A coordinated second session, multiplayer-mode protocol,
  TCP/IPC transport and internet netplay are absent.
- DMG palette presets do not alter GBA palette RAM.

Battery saving inside a game is supported and is different from a save state.
Closing AetherBoy flushes changed cartridge storage; long sessions also flush at
the regular 1,800-frame safety interval.

## What the integration proves—and what it does not

The automated runtime test executes a self-generated ARM ROM through the
vendored CPU, writes `DISPCNT` and VRAM, publishes a non-black 240×160 Mode-3
frame through the AetherBoy adapter and checks the GBA L/R keypad bits. The full
repository test suite requires no commercial game or Nintendo data.

The imported core is an actively maintained AetherBoy fork rather than a frozen
binary. Hardening now covers cartridge metadata and save markers, Flash128,
EEPROM bit order/window/size, Flash reset, ARM/Thumb exceptions and SBC borrow,
DMA address wrapping, WRAM disable/control mirrors, MMIO open-bus lanes,
WAITCNT/prefetch boundaries, PPU mosaic/OBJ-window/blending/bitmap-VRAM edges,
PSG/Direct Sound, RTC, Serial, STOP clock freeze and the no-firmware HLE path.
These paths have dedicated synthetic regression tests. The current suite has
290 tests (175 Core, 87 Runtime and 28 Windows smoke tests).

GBADotnet's upstream compatibility tool ran 500 frames without input and called
that result “bootable.” Its table includes Pokémon FireRed, LeafGreen, Emerald,
Ruby and Sapphire. That is encouraging for a FireRed-based Team Rocket hack,
but it is not a playthrough, save test or compatibility guarantee. ROM hacks can
exercise different code, save markers and timing behavior than the base game.

## Test the intended game next

Use a legally obtained `.gba` file and verify in this order:

1. Boot logos and title screen render without a blank/white display.
2. Keyboard and controller handle movement, A/B, Start/Select and both shoulders.
3. Press F5, move to a visibly different scene, press F8 and confirm exact
   restoration; then hold Rewind after at least ten seconds of play.
4. Music and effects are present from both PSG and Direct Sound; toggle the four
   channels in Control Center and note distortion, missing voices or DC noise.
5. Start a new game, change maps, open menus, enter and finish a battle.
6. Save from the game's own menu, close AetherBoy completely, reopen and load.
7. Continue for at least 30–60 minutes while watching for freezes, graphics
   corruption, audio drift, input lockups and unusual speed. Repeat a shorter
   boot/save/load pass both with built-in HLE and, if legally available, a real BIOS.

Keep the exact ROM SHA-256 from Control Center diagnostics with every report.
Do not send or commit the ROM itself.

## Independent prototype probe

The earlier self-written execution path remains reproducible:

```powershell
dotnet run --project ./tools/AetherBoy.GbaProbe/AetherBoy.GbaProbe.csproj -c Release
```

Expected facts include `DISPCNT: 0x0403`, a 240×160 frame and SHA-256
`0E2B2DF5975F32E5C5D618F1CC99F895D09E2E830FC9998DE6A5CA9AF48EA801`.
This probe tests only AetherBoy's small independent core, not the production
GBADotnet adapter.

## Reference material

- [GBADotnet integration review](docs/GBADOTNET_REVIEW.md)
- [Independent GBA core architecture map](docs/GBA_CORE_ARCHITECTURE.md)
- [mGBA reference review](docs/MGBA_REVIEW.md)
- [GBATEK hardware reference](https://problemkaputt.de/gbatek.htm)
