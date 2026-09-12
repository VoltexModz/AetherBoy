# Linux comfort update — independent playtest

Status: independent comfort acceptance and full native short suite completed on 12 September 2026; later UI revisions must be distinguished from this build.

This round evaluates the Linux improvements after `22a77ef`: background library/save reads, resume and save-state gallery, per-game settings, library organisation, and UI readability/navigation. The initial acceptance author was independent of production implementation. In a later delegated follow-up this agent also implemented stable keyboard focus and F6 region navigation; those navigation tests are implementation regressions, while the separate critic remains the independent UI reviewer. Only synthetic cartridges and isolated temporary data are used. The user owns long-duration and real-game tests; this round starts no 30-minute runs.

## Acceptance cases

| Area | Failure-oriented scenario | Required user-visible outcome |
| --- | --- | --- |
| Async catalog/gallery | Switch from ROM A to B while A's read is pending | B never displays or loads A's cached state data. |
| Save states | Load corrupt/truncated state after a valid load | Current game remains usable; last valid undo is preserved. |
| Undo | Save frame A, advance to B, load A, undo | Restore B; a second game never inherits this undo. |
| Resume | Close/reopen, then move same-content ROM | Resume targets matching cartridge identity; missing/corrupt files fail without destructive replacement. |
| Profiles | Configure A, switch to B/global defaults, restart A | No cross-game leakage; chosen settings persist; invalid profile does not discard valid global settings. |
| Library | Favorite/rename/playtime followed by relink/reopen | Metadata survives ordinary Remember/update calls; playtime excludes paused menu time. |
| UI | Keyboard and pointer navigation, larger text | Actions remain reachable and labels do not overlap; actual image review belongs to independent critic. |

## Evidence

**Comfort acceptance: 19/19 passed, zero skipped**, native Wayland, 7.54 seconds, with `AETHERBOY_UI_TESTS=1`. TRX: `artifacts/comfort-playtest/AetherBoy.DesktopTests_net10.0_x64.trx`.

**Final focused build verification: 20/20 passed**, 8.20 seconds: all 19 comfort cases plus `LinuxShellIntegrationTests`, after the header fix and the legacy helper's separate save-cache completion wait. TRX: `artifacts/comfort-final-focused/AetherBoy.DesktopTests_net10.0_x64.trx`. Final build: zero warnings, zero errors. The final Input/header capture was refreshed at the largest text setting; no audio or long-duration reruns were added.

**Earlier full native Desktop short suite (before final focus/resume refinements): 79 discovered, 78 passed, zero failed, one skipped**, 45.87 seconds. TRX: `artifacts/comfort-full-playtest/AetherBoy.DesktopTests_net10.0_x64.trx`. The virtual-controller case deliberately skipped because a physical controller was already selected. No user device was detached. The command's `--minimum-expected-tests 79` caused exit 9 because only 78 actually executed; this is not a 79/79 pass claim.

Assertions verified actual paused machine-state bytes for save/load/undo, malformed-state rejection retaining both machine and prior undo, resume on close/reopen, and cartridge switch isolation. Profile volume/filter/key overrides survived switching while global preferences remained unchanged. File-level tests covered 30 concurrent metadata increments, favorite/title retention after moving the ROM, invalid metadata preservation, four malformed profile inputs, exact-state preview binding, malformed preview dimensions and separate resume/manual slots.

Additional native acceptance verified that an explicitly delayed old-cartridge disk completion never published old slots after switching, playtime excluded pause/settings time and persisted on close, a rapid profile toggle preserved pending global settings in the global scope, and failed profile writes blocked cartridge switching while keeping edits available for a successful retry.

**Existing-flow navigation regression: 12/12 passed**, 41.19 seconds, including native shell, Patch Lab and the three short synthetic cartridge scenarios. TRX: `artifacts/comfort-navigation-regression/AetherBoy.DesktopTests_net10.0_x64.trx`. A subsequent slower headless runner exposed a test timing race between completed save writes and the refreshed UI cache; the legacy test helpers now wait for both stages before asserting that a pointer click loads the state. The original assertion remains intact.

The reviewer's findings on malformed profile fallback and profile-save failure/scope changes were addressed before these passing cases. Later reviewer findings belong in the critique report and require their own applicable verification.

The full suite also ran synthetic GB, GBC and GBA through real PipeWire output, without audible listening assessment: GB 10.01 s / 59.75 fps / 440,320 sample frames; GBC 10.01 s / 59.75 fps / 441,344 sample frames; GBA 10.00 s / 59.79 fps / 652,160 sample frames. Audio data was fed to muted output; these numbers are not a game-compatibility or sound-quality score.

Targeted hidden renderer captures were provided to the independent critic: gallery, library, Input/System/Display at text size 18 and 900×650, and the main window at that same larger-text setting. Files: `artifacts/comfort-review/`. The sixth capture used one additional focused profile test, passed in 1.21 s. That build completed with zero warnings and zero errors. The five settings captures were subsequently replaced after the final larger-font correction, all at 900×650, and one title-editor capture was added. Additional passing cases cover actual F6 sidebar/content jumps and leaving subviews, keyboard focus surviving disabled preceding controls, prevention of same-title row retargeting, home Continue restoring exact saved bytes, failed Continue preserving the recovery file on close, and actual Ctrl+A/SDL text input replacing a title before saving. The final navigation build completed with zero warnings and zero errors. The critic assigns UI/feature scores independently; this playtest supplies evidence, not a target-driven score.

All native hosts were created with `hidden: true`; captures read the SDL renderer directly, and no external portal windows were requested. The user's Workspace 5/no-focus-stealing requirement remains in force for any visible root/critic capture.

A passing test proves its asserted scenario only; it does not prove arbitrary disk failure recovery, physical-controller compatibility, screen-reader support, or commercial game compatibility. No long-duration runs were performed.

## Reproduce the focused checks

```bash
dotnet build tests/AetherBoy.DesktopTests/AetherBoy.DesktopTests.csproj -c Release
AETHERBOY_UI_TESTS=1 dotnet tests/AetherBoy.DesktopTests/bin/Release/net10.0/AetherBoy.DesktopTests.dll \
  --filter 'FullyQualifiedName~LinuxComfortPlaytestTests' --minimum-expected-tests 19
```

Optional captures require an **absolute** `AETHERBOY_COMFORT_CAPTURE_DIR`; the test runner changes its working directory. `AETHERBOY_MAIN_CAPTURE_ONLY=1` captures only the main window within the profile acceptance case. `AETHERBOY_COMFORT_CAPTURE_PAGE=Input` restricts page captures to that requested page. Leave capture variables unset for ordinary testing.


## Final integration checks by the coordinating agent

After the legacy cache-wait helper fix, the final build discovered 84 desktop
cases. The logic run passed 67 with 17 native opt-in skips; the isolated Ubuntu /
Weston run passed 81 with the three audio cases deliberately skipped. Neither
run failed. CI minimum execution counts are now 67 / 81. Logs:
`artifacts/comfort-logic-final.log`, `artifacts/comfort-headless-final.log`.

The standard Linux-x64 publish succeeded (`artifacts/comfort-publish-final.log`).
Both portable archives were rebuilt from the final production source; their
158 production C# files were compared against the working tree. x64 execution
without dotnet in PATH passed; ARM64 execution remains untested on this x64 host.
Final package SHA-256:

- x64: `75fbbdf982b3dd79cc7725f5940ec4e9b051438ea2d3cc86f89e789ea0bf6bc4`
- ARM64: `cfea14208c5510bf6ec86a09ba6a13ec41b052ad6ffee094f4af7250177c7521`

The coordinating agent also inspected the final Input capture after moving the
header subtitle upward. It now clears the divider at 900×650/largest text.
This small correction does not change the independent critic's 8.9 UI / 8.6
feature scores or the still-open UI >9 target.
