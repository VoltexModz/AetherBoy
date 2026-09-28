# Windows → Linux: Aether shell

The Windows screenshots correspond to hand-drawn WinForms controls, rather than
a web page or a set of full-window image assets. The Linux frontend renders the
same visual language using SDL3, sharing the existing Core and Runtime projects.
No Wine, WinForms emulation, or new emulator implementation is involved.

| Responsibility | Windows source | Linux source |
| --- | --- | --- |
| Main shell, display, session rail, command deck | `nanoboy/frmNano.AetherUi.cs` | `frontends/AetherBoy.Desktop/WaylandEmulatorHost.AetherUi.cs` |
| Control Center navigation/cards | `nanoboy/frmControlCenter.cs` | `WaylandEmulatorHost.AetherUi.cs` plus existing settings pages in `WaylandEmulatorHost.cs` |
| Colors, six-point buttons, gradients | `nanoboy/Controls/AetherUiControls.cs` | `WaylandEmulatorHost.AetherUi.cs` (same RGB values and geometry) |
| Window icon / empty-state logo | `nanoboy/Branding/AppBrand.cs`, `branding/exports/` | `Assets/aetherboy-mark.png`, linked from the original 256px export |
| Text | Windows Segoe UI | Bundled Noto Sans Regular/Bold; optional SDL3_ttf and bundled atlas fallback |
| Emulator/session commands | `nanoboy/ControlCenterBridge.cs` | existing `WaylandEmulatorHost.cs` handlers calling the same `AetherBoy.Runtime` |

The Linux Control Center is a page in the main SDL window, rather than a separate
WinForms dialog. Opening it pauses a running game; closing it restores the prior
pause state. The shell keeps native Wayland window management and retains Linux
keyboard defaults. Header navigation opens System, Display, Saves and Diagnostics.

## Assets are part of the project

- Original logos: `branding/exports/` and `nanoboy/Branding/`.
- Fonts, license and fallback atlases: `branding/fonts/`.
- Reproducible atlas generator: `scripts/build-font-atlas.py` (Pillow is required
  only for regeneration, not for build or runtime).
- The desktop project copies assets into both build and publish output. The
  launcher detects asset changes as well as source changes before deciding to
  rebuild. All runtime asset paths resolve from the application directory.

The atlas covers Latin and Western European UI text, including German characters.
Other characters become `?` on the atlas path. SDL3_ttf uses the included TrueType
font when available. PNG loading uses SDL3 itself, without SDL_image.

## Implemented behavior

The shell connects ROM opening/drag-and-drop, keyboard/gamepad input, pause,
held turbo, rewind, quick save/load, five slots, fullscreen, display filtering,
frameskip, DMG palettes, volume/mute and four hardware audio channels.
Preferences persist in the existing version-1 settings format, with defaults for
fields absent from older files. The nine Control Center sections are Overview, Graphics, Audio, Controls, Save states,
App & files, Diagnostics, Library and Tools. Settings search and the new Graphics,
Controller and App & files subpages live in `WaylandEmulatorHost.SettingsNavigation.cs`.
The search catalog lives in `LinuxSettingsCatalog.cs`. See the
[settings design notes](LINUX_SETTINGS_DESIGN.md) for rationale and validation. Diagnostic values come from
the live Linux host.

Control Center buttons are reachable through Tab/Shift+Tab with a visible focus
outline; Enter or Space activates a focused control. Ctrl+Tab cycles sections.
F6 switches between the selected sidebar section and its page controls.
Stable focus identities prevent refreshed or reordered rows from activating a
different action. Title, search and cheat fields share grapheme-aware caret/selection,
clipboard, pointer selection, horizontal scrolling and separate IME preedit.
Input still supports arrow-key selection and rebinding; Audio supports 1% steps.

An optional native GTK3 Control Center exposes names, roles, states and editable
text through ATK/AT-SPI, sharing the same validated host commands. Open it with
Ctrl+F7, System → Accessible UI or `--accessible`; F7 remains rewind. This does not
make the SDL game image screen-reader content. GTK dependencies are optional.
ROM preparation and regular settings writes use bounded background workers;
explicit save/scope/shutdown boundaries still wait to protect user data.

## Remaining Windows frontend gaps

Linux now includes controller remapping/profiles, a searchable recent-cartridge
library, firmware import, session cheats, WAV recording, local diagnostics and
battery-backup recovery. State gallery, separate resume points, undo after load,
per-ROM settings, favorites, custom titles and active playtime are also available.
Library → Patch Lab applies IPS/BPS/UPS through the shared
parser, including explicit UPS undo and separate result storage. Saves and states use content-addressed XDG storage.
See [Linux roadmap](LINUX_ROADMAP.md) and [current playtest report](LINUX_PLAYTEST_ROUND3.md).
The full Windows Audio Inspector and its complete Cartridge Vault feature set
remain distinct. Real-game compatibility, physical device changes, screen readers
and multi-monitor DPI behavior require separate qualification.

## Verification

```bash
AETHERBOY_UI_TESTS=1 \
AETHERBOY_UI_CAPTURE_DIR="$PWD/artifacts/ui-review" \
dotnet test --project tests/AetherBoy.DesktopTests

AETHERBOY_UI_TESTS=1 AETHERBOY_TEXT_RENDERER=atlas \
AETHERBOY_UI_CAPTURE_DIR="$PWD/artifacts/ui-review-atlas" \
dotnet test --project tests/AetherBoy.DesktopTests

bash scripts/build-linux.sh
```

The native test requires Wayland and uses a hidden SDL window, temporary
preferences, a generated striped test ROM and temporary save states. It checks
navigation, input routes, persistence, pause/save/load/held-turbo and tall-window
layout. Screenshots cover all sections. This verifies the UI integration, not
real-game compatibility or a physical controller.
