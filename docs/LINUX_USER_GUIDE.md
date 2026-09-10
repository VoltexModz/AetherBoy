# AetherBoy Linux User Guide

AetherBoy has a native Linux frontend for Wayland. It does not run through
Wine, X11 or XWayland. Hyprland, KDE Plasma and GNOME are detected separately,
while the emulator cores and save formats remain shared with the Windows app.

> **Alpha status:** GB/GBC have broad automated coverage, while GBA and the
> Linux desktop adapter still require real-game testing. Use copies of your
> saves and only ROMs or firmware you are legally allowed to use.

## 1. Requirements

- a 64-bit x86 or ARM Linux installation
- a native Wayland session
- the .NET 10 SDK and runtime
- a working Mesa or vendor GPU driver
- PipeWire, PulseAudio or ALSA client libraries for native audio output
- SDL3 runtime files are restored with the project

Check your session:

```bash
echo "Session: ${XDG_SESSION_TYPE:-unset}"
echo "Desktop: ${XDG_CURRENT_DESKTOP:-unset}"
echo "Wayland socket: ${WAYLAND_DISPLAY:-unset}"
dotnet --version
```

`WAYLAND_DISPLAY` must not be empty. A session reported as `x11` is rejected.
On Ubuntu 24.04 or 26.04, install .NET with:

```bash
sudo apt update
sudo apt install dotnet-sdk-10.0
```

On other distributions, install the .NET 10 SDK using the distribution's
official package or Microsoft's Linux instructions.

## 2. Download and build

```bash
git clone https://github.com/VoltexModz/AetherBoy.git
cd AetherBoy
git switch development
bash scripts/build-linux.sh
```

The script detects `linux-x64` or `linux-arm64`, performs a locked dependency
restore and writes the framework-dependent release to `artifacts/`.

## 3. Start a game

Pass a ROM path directly:

```bash
bash scripts/run-linux.sh "$HOME/Games/Pokemon.gbc"
```

Or open AetherBoy without a game and press `O`, click `OPEN ROM`, or drag a
`.gb`, `.gbc` or `.gba` file onto the window. The open button uses the native
asynchronous XDG Desktop Portal path:

```bash
bash scripts/run-linux.sh
```

The host shows loading and recoverable errors in the window. When a replacement
ROM fails to initialize, the previous game resumes. Local `file://` drops and
filenames with spaces are supported; extract archives before opening them.

The Linux shell now follows the Windows Aether layout: title navigation, game
stage, right session rail, bottom command deck, and the seven-section Control
Center (Overview, Display, Audio, Input, Saves, System, Diagnostics). It adapts
to tall tiling windows and wide windows, preserving the game's aspect ratio.
At 1180×760 the stage fits GBA at 3× with Sharp filtering.

The original logo, Noto Sans Regular/Bold, their license, and antialiased glyph
atlases are shipped inside `Assets/`. No system fonts or font downloads are
needed. SDL3_ttf is optional; without it the bundled atlas renders Western
European UI text (unsupported characters in filenames use `?`). For other
scripts, install SDL3_ttf and use the bundled TrueType font's available glyphs.

To check desktop detection without opening a window:

```bash
artifacts/AetherBoy-linux-x64/AetherBoy.Desktop --platform-info
```

To validate both Wayland and the default SDL audio device:

```bash
bash scripts/run-linux.sh --audio-info
```

## 4. Install in the application menu

```bash
bash scripts/install-linux-user.sh
```

This installs only for the current user:

- program files: `~/.local/share/aetherboy`
- launcher: `~/.local/bin/aetherboy`
- desktop entry: `~/.local/share/applications`
- icons: `~/.local/share/icons/hicolor`

You can then open a supported ROM from the application menu or run:

```bash
aetherboy "$HOME/Games/Pokemon.gbc"
```

If the command is not found, add `~/.local/bin` to your shell's `PATH`.

## 5. Controls

| Function | Keyboard | Gamepad |
| --- | --- | --- |
| Direction | Arrow keys | D-pad or left stick |
| A / B | Z / X physical positions (Y / X on German layouts) | South / East |
| Start / Select | Enter / Backspace | Start / Back |
| GBA L / R | Q / E | Left / right shoulder |
| Pause | Space | — |
| Turbo while held | Tab | — |
| Control Center | C | — |
| Select save-state slot | 1–5 | — |
| Quick Save / Load | F5 / F8 | — |
| Rewind one step | F7 | — |
| Fullscreen | F11 | — |
| Close settings / dismiss error / leave fullscreen | Escape | — |
| Move focus / activate control | Tab / Shift+Tab, then Enter or Space in Control Center | — |
| Next / previous settings section | Ctrl+Tab / Ctrl+Shift+Tab | — |
| Exit | Window close button / compositor close shortcut | — |

These are the default bindings. To change them, open **Settings (C) → Input**,
click the key next to an action, then press its replacement. Alternatively,
select a binding with the arrow keys and press Enter. Escape cancels capture.
An occupied key swaps with the previous binding, so every action stays usable.
Movement, A/B, Start/Select, GBA L/R, turbo and pause can all be changed.
O, C, Escape, F5/F7/F8/F11, 1–5 and Super remain reserved for the app or desktop.
The reset button restores keyboard defaults; gamepads retain the standard layout.

The sidebar displays the assigned key labels for your current keyboard layout.
Losing window focus releases held controls and turbo. Escape during ordinary
play no longer closes the application.

**Settings (C) → Audio** provides a draggable 0–100% volume slider and **−1% / +1%**
buttons. Left/Right also change the volume in 1% steps; Home selects 0% (silence)
and End selects 100%. Low settings such as 1% or 5% are supported.
Keyboard bindings, volume, mute, display filter, frameskip, DMG palette, audio
channel switches and save slot save automatically to
`$XDG_CONFIG_HOME/aetherboy/settings.json`, normally
`~/.config/aetherboy/settings.json`, and load at the next start.

Battery saves use `.sav`; quick states use `.ss1` through `.ss5`. They are
stored next to the ROM, so the ROM directory must be writable. The Control
Center exposes Sharp, Smooth and LCD Grid video, frameskip, five DMG palettes,
master audio, all four hardware channels, keyboard bindings and timeline controls.

## 6. Hyprland setup

Install and run `xdg-desktop-portal-hyprland` together with a file-picker portal
such as `xdg-desktop-portal-gtk`. AetherBoy detects Hyprland through
`XDG_CURRENT_DESKTOP` or `HYPRLAND_INSTANCE_SIGNATURE`, publishes the native
Wayland app ID `io.github.VoltexModz.AetherBoy`, and avoids the optional
client-side `libdecor` path.

The repository includes a portal preference file. Review and merge it if you
already have custom portal settings:

```bash
mkdir -p ~/.config/xdg-desktop-portal
cp packaging/linux/hyprland/hyprland-portals.conf \
  ~/.config/xdg-desktop-portal/hyprland-portals.conf
systemctl --user restart xdg-desktop-portal xdg-desktop-portal-hyprland
```

The profile lets Hyprland handle compositor-specific portals and delegates the
file chooser to GTK. Change `gtk` to `kde` if you use the KDE portal instead.

## 7. Troubleshooting

### AetherBoy says native Wayland is required

Log into a Wayland session and verify:

```bash
echo "$XDG_SESSION_TYPE"
echo "$WAYLAND_DISPLAY"
```

Do not set `SDL_VIDEO_DRIVER=x11`; the launcher and application intentionally
reject that fallback.

### The window or renderer cannot be created

Update or install the Mesa/Vulkan/EGL packages appropriate for your GPU and
distribution. In a VM or WSLg, verify that hardware-accelerated Wayland clients
work before diagnosing the emulator core.

### Controller not detected

Reconnect it after AetherBoy starts, verify that Linux creates an input device,
and test it with another SDL3 application. Steam Input can claim or remap some
controllers; test once with Steam closed if the mapping looks wrong.

### SDL reports a missing audio library

Install the PipeWire, PulseAudio or ALSA client library provided by your
distribution. A normal AetherBoy session catches audio-device initialization
errors and continues silently; `--audio-info` deliberately returns an error so
the system setup can be diagnosed.

### Saves are not written

The ROM directory must be writable. Avoid launching games directly from a
read-only archive or protected mounted directory.

## 8. Current Linux limitations

The native frontend supports gameplay video, SDL3 audio, an XDG Portal open
dialog, keyboard/gamepad input, pause, turbo, fullscreen, battery saves, all
five quick-state slots, rewind and a seven-section Control Center. It does not yet
provide gamepad remapping, Cartridge Vault,
cheat/diagnostic/save-safety tools, WAV recording or boot-ROM selection. These
are frontend gaps; the portable emulator core remains shared across Windows and
Linux.

Technical details and the German guide are available in
[`LINUX_WAYLAND.md`](LINUX_WAYLAND.md).

## 9. Verify the Linux shell

The regular DesktopTests cover preferences, paths and save-state handling. An
opt-in Wayland test also renders every Control Center section, checks mouse and
keyboard routes, uses a generated test ROM for pause/save/load/turbo, and verifies
resizing. It uses a hidden window and temporary settings/ROM/save files.

```bash
AETHERBOY_UI_TESTS=1 dotnet test --project tests/AetherBoy.DesktopTests
```

Set `AETHERBOY_UI_CAPTURE_DIR` to an absolute directory to retain screenshots.
Set `AETHERBOY_TEXT_RENDERER=atlas` to verify the bundled font fallback even on a
system with SDL3_ttf installed. Neither variable is needed for normal use.

See [Windows-to-Linux UI mapping](LINUX_UI_PARITY.md) for implementation details
and the remaining Windows-only tools.
