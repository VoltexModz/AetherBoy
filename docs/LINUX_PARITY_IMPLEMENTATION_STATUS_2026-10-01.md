# Linux parity implementation — 1 October 2026

This is an implementation and verification record, not a Linux release approval. The working tree is modified and no commit or package is identified by `7ced237` alone. Online Link remains a development feature; a channel opening is not evidence of a completed Pokémon trade.

| Plan step | Implemented in the working tree | Still required for acceptance |
| --- | --- | --- |
| L0 baseline | The entire solution builds with .NET 10 on Windows. Core, Runtime, Desktop and Windows smoke tests were run. XDG paths and the existing package script were inspected. | Fresh native Linux build and test counts; GNOME/KDE Wayland and Hyprland inventory. |
| L1 ROM-free online probe | Linux Tools offers host/join with a short room code, progress, safe report and cancellation through the shared `OnlineProbeSession`. Leaving the page stops and disposes its session. | Native Wayland UI test, two-PC WAN test, and fault/timeout exercise against the real room service. |
| L2 two-player storage | Both ROMs are validated and hashed before save migration. GB/GBC and GBA families are separated. Same-ROM player 2 has an independent persistent save and lock; player 1 uses the single-player lease. | Linux filesystem permission and failure-path tests on native Linux, including a blocked destination. |
| L3 local link | Two video panes, independent inputs, hotplug path, pause/cable/end actions and bounded two-source audio mixing use one `LocalLinkSession`. State/rewind/turbo actions are not offered there. | Native Wayland/Hyprland controller and audio tests; synthetic GB/GBC and GBA cable cases; actual game save/cold-start checks. |
| L4 quick menu and text | Quick menu offers pause, state slot and video filter. The right-stick Control Center route remains. A controller-operated keyboard uses the existing text editor and masks the room access key. | Controller-only and IME tests on a real desktop, including focus loss and return from local link. |
| L5 audio, library, themes | Audio snapshot inspector, recent/title/playtime sorting, list/grid views, bounded state-preview cache, all six shared theme presets, custom RGB and reset. Existing custom colors remain unchanged until selected. | Contrast, clipping, preview and navigation checks under Wayland and Hyprland. |
| L6 diagnostics and saves | Local problem marker and portable progress hints use stable `session.problem_marked` / `session.health_hint` events. Linux save ZIPs include a SHA-256 manifest and only allowlisted persisted files, including RTC sidecars. `.sav` import checks length before and after reading. | Deliberately broken disk/restore tests and cross-OS report comparison. Hints are suspicions, not confirmed faults. |
| L7 packages/desktops | Shell scripts passed read-only Bash syntax checks after CRLF normalization; `.gitattributes` now requests LF for shell scripts on fresh checkouts. CI now builds, smoke-checks and retains a package on both x64 and ARM64 runners. | Observe the new CI jobs and start both packages on real target desktops; complete Wayland/Hyprland/Orca checks, native online loopback and WAN matrix. No package was created here. |

## Tests in this environment

- `dotnet build nanoboy.sln -c Release --no-restore`: 0 warnings, 0 errors.
- Full solution test before the final GBA storage-plan test: 1044 passed, 53 skipped, 1 GBA-online shutdown timeout while suites ran concurrently. The nine affected parameterized GBA tests passed alone; the full Runtime suite then passed separately (442 passed, 5 native/Unix checks skipped). Desktop logic after the additional test: 110 passed, 44 native/Wayland checks skipped.
- This Windows host's WSL has neither `dotnet` nor Weston. A successful Windows-hosted Desktop test is **not** a native Linux UI or package result.

## Native Linux acceptance sequence

1. Use a clean Linux checkout of the integrated source. Confirm `git status`, `dotnet --version`, Wayland session and architecture. Do not copy private room keys into reports.
2. Build and run `dotnet test tests/AetherBoy.DesktopTests/AetherBoy.DesktopTests.csproj -c Release` plus Core and Runtime tests. Run the opt-in native UI/accessibility tests under an isolated Weston session and then under a real desktop. Record executed versus skipped counts.
3. Open two synthetic local GB/GBC games and then two synthetic GBA games. Exercise different ROMs, the same ROM twice, save ownership, two controllers, keyboard plus controller, unplug/pause, restart and audio. Do not use valuable original saves for this first run.
4. Try all six themes, custom RGB persistence, library sort/grid/list/preview, quick menu, room-code keyboard, audio inspector and problem marker. Export a report and a save ZIP; inspect the manifest and verify the originals remain unchanged.
5. Build `bash scripts/package-linux.sh --runtime linux-x64` on x64 and `--runtime linux-arm64` on ARM64 with the native online dependency. Extract each package into a fresh directory and start it on its target architecture. A cross-compiled archive is not an ARM64 start test.
6. On GNOME/KDE Wayland and Hyprland, test file portal, focus, fullscreen, PipeWire audio, hotplug, IME and optional Orca. Finally run the ROM-free two-PC Online Link probe and only then separate GB/GBC and GBA game tests with disposable save copies.

No commit or push was performed for this work.
