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

Or open AetherBoy without a game and drag a `.gb`, `.gbc` or `.gba` file onto
the window:

```bash
bash scripts/run-linux.sh
```

To check desktop detection without opening a window:

```bash
artifacts/AetherBoy-linux-x64/AetherBoy.Desktop --platform-info
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
| A / B | Z / X | South / East |
| Start / Select | Enter / Backspace | Start / Back |
| GBA L / R | Q / E | Left / right shoulder |
| Pause | Space | — |
| Turbo while held | Tab | — |
| Quick Save / Load, slot 1 | F5 / F8 | — |
| Fullscreen | F11 | — |
| Exit | Escape | — |

Battery saves use `.sav`; quick states use `.ss1`. They are stored next to the
ROM, so the ROM directory must be writable.

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

### Saves are not written

The ROM directory must be writable. Avoid launching games directly from a
read-only archive or protected mounted directory.

## 8. Current Linux limitations

The first native frontend supports gameplay video, keyboard/gamepad input,
pause, turbo, fullscreen, battery saves and one visible quick-state slot. It
does not yet expose audio output, the complete Control Center, all five visible
slots, rewind controls or an in-app portal file picker. These are frontend gaps;
the portable emulator core remains shared across Windows and Linux.

Technical details and the German guide are available in
[`LINUX_WAYLAND.md`](LINUX_WAYLAND.md).
