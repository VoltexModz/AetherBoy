# Linux ROM-start review · 2026-09-10

The supplied FireRed Rocket Edition ROM initially remained white while the
Linux host reported RUNNING. Testing used a local ignored copy with separate
saves. No game data or screenshots are included in the source changes.

## Fixed

- Missing HLE hardware-IRQ bridge: the ARM CPU entered empty BIOS memory rather
  than calling the cartridge's interrupt handler. Original ARM glue now saves
  and restores the required registers and processor state; real BIOS images
  remain unchanged.
- Reversed CpuSet control bits: word copies were being interpreted as fills.
  The existing synthetic test had repeated that mistake. Tests now cover all
  four copy/fill and halfword/word combinations plus CpuFastSet fill rounding.
- Empty compressed assets were rejected. LZ77 now accepts zero-length output.
- After the controls/tutorial screens, an asset with a nonstandard LZ77 type
  tag triggered another exception. The BIOS service selects the algorithm;
  the HLE now accepts that tag while checking output bounds and back-references.
- ROM-start faults no longer terminate the Linux window. Replacement loading
  preserves the previous session until the new one initializes, with readable
  status and errors. URI drops, spaces and mixed-case extensions are handled.
- Focus loss releases buttons and turbo; Escape dismisses settings/errors or
  exits fullscreen rather than quitting ordinary play.
- Linux UI: larger 3× GBA stage at the default size, cached system-font text
  with an SDL bitmap fallback, direct playback/state controls, hover/disabled
  states, and keyboard-layout-aware A/B labels.
- The run script now notices changes to the vendored core and shared build
  properties, avoiding accidentally launching an older published build.

## Verification

- 175 Core, 100 Runtime and 18 Desktop tests passed on Linux (293 total).
- Real Wayland window: title sequence, controls explanation, story pages and
  the transition into character introduction passed; this is the point that
  previously threw the header exception. F5/F8 saved and restored that session.
- A separate native-host harness checked failed replacement, missing paths,
  same-ROM URI loading, quick save/load, settings pause/resume, and recovery
  after an initial load failure.
- No full playthrough, long-term audio comparison or Windows GUI test was done.

## Follow-ups

Keyboard remapping, volume and mute now persist on Linux; display/channel
preferences and gamepad remapping remain follow-ups. SDL's custom-drawn
controls do not expose the accessibility semantics of a native widget toolkit.
The vendored CPU has static instruction scratch state: a broader redesign is
needed before independently running GBA owners can safely share a process.
The replacement loader currently stabilizes the paused owner at an instruction
boundary to avoid this during a ROM switch.

HLE BIOS changes alter the BIOS identity in save states, so old HLE states are
rejected by the existing digest check. Battery `.sav` files remain compatible.

References: [Tonc IRQ convention](https://gbadev.net/tonc/interrupts.html#the-interrupt-process)
and [mGBA's BIOS implementation](https://github.com/mgba-emu/mgba/blob/master/src/gba/bios.c)
were used to verify the behavior; no BIOS image was copied.
