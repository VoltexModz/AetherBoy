# Linux settings: graphics, controllers and finding options

## Audit and direction

The previous overview repeated session information already shown beside the game.
System mixed profiles, colors, firmware and file actions in one panel. Graphics
mixed filters, frame skipping, palettes and interface text size. Controller
mapping existed behind a small secondary button. There was no settings search.

The revision retains the bundled Noto Sans fonts, logo, theme colors and native
SDL frontend. A quieter sidebar and descriptive rows give each page a clear
purpose. The overview starts with graphics and controller setup. These are the
user's requested priorities, rather than another session dashboard.

The research used primary Linux desktop sources:

- [KDE layout and navigation](https://develop.kde.org/hig/layout_and_nav/):
  group related controls, use consistent spacing and provide a persistent
  navigation control for many destinations.
- [KDE simple by default](https://develop.kde.org/hig/simple_by_default/):
  keep common tasks easy to find and move complexity to relevant contexts.
- [GNOME preferences dialog](https://gnome.pages.gitlab.gnome.org/libadwaita/doc/main/class.PreferencesDialog.html):
  organize preferences into pages and groups with search.

These inform the interaction design; the app continues to render through SDL,
with its existing optional GTK accessibility surface.

## Result

- Sidebar search supports setting names, purposes and common German search terms.
  Results identify their section and open the corresponding page, including the
  separate controller stick page. Search cannot trigger hidden page controls.
  Enter opens the first visible result; Escape restores the previous page.
- Graphics has Picture, Game Boy colors and Performance tabs. The new persisted
  `VideoScaling` setting offers Auto, Whole pixels and Fit. Auto retains the
  former filter-dependent choice. Whole pixels is calculated in renderer output
  pixels, accounting for SDL's logical presentation transform. Fit preserves the
  game's aspect ratio. Scaling also participates in game-profile inheritance.
- Controls has Keyboard, Controller and App shortcuts tabs. Controller buttons
  show SDL's recognized labels with their physical positions. Mapping, device
  selection, reset and stick settings are discoverable. The stick page explains
  the deadzone and shows live movement against its threshold. Device-specific
  changes are disabled when no controller is connected.
- App & files separates Appearance, Desktop & accessibility, Game profiles,
  Firmware and Files. Interface text size is placed with app appearance and
  accessibility, separate from game graphics.
- New controls retain keyboard focus and GTK action/state semantics. Color and
  text size choices remain global; controller settings stay with their device.
  Graphics, audio and keyboard overrides can stay with a game profile.

The graphics page exposes the implemented SDL filters and scaling options. It
adds no shader loader or emulator-core graphics features. Online Link protocols,
save protection and firmware validation are unchanged.

## Validation

Build and run the native tests in a Wayland session:

```bash
dotnet build tests/AetherBoy.DesktopTests/AetherBoy.DesktopTests.csproj \
  --no-restore -m:1 -p:UseSharedCompilation=false --disable-build-servers
GDK_BACKEND=wayland AETHERBOY_UI_TESTS=1 AETHERBOY_ACCESSIBILITY_TESTS=1 \
  SDL_GAMECONTROLLER_IGNORE_DEVICES_EXCEPT=0x1209/0xAEB0,0x1209/0xAEB1 \
  AETHERBOY_UI_CAPTURE_DIR="$PWD/artifacts/settings-redesign-final" \
  dotnet tests/AetherBoy.DesktopTests/bin/Debug/net10.0/AetherBoy.DesktopTests.dll
```

The SDL device allowlist is process-local and lets the virtual-controller test
run when a physical controller is connected. Test settings and saves use temporary
folders. The tests cover search routing, empty results, keyboard navigation,
profile isolation, scaling persistence, physical pixel scale, real SDL virtual
button events, remapping, reset and deadzone feedback. The GTK role checks accept
both names of the native `ATK_ROLE_BUTTON`/`ATK_ROLE_PUSH_BUTTON` alias.

The shell test captures the default window and 860×554 with the largest interface
text, plus a tall window. Visual review includes dark and custom light themes.
The copy check's remaining findings are the unchanged system-name line,
keyboard-shortcut legend and compact diagnostic counters.

Validation on 2026-09-28: build succeeded with 0 warnings and 0 errors. The native
Desktop run passed 129 of 133 tests; 4 were intentionally skipped (the isolated
AT-SPI bus scenario and 3 audio hardware playtests). The virtual-controller
scenario passed, including UI-driven mapping, reset and live deadzone feedback.
