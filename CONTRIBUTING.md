# Contributing to AetherBoy

Bug reports, documentation, tests and focused code changes are welcome. Use
[GitHub issues](https://github.com/VoltexModz/AetherBoy/issues) to discuss substantial
changes or report a reproducible problem.

## Set up a checkout

1. Fork the repository and branch from `development` for your change.
2. Follow the [README setup instructions](README.md#getting-started). Use the SDK
   selected by [global.json](global.json) and retain dependency lockfiles.
3. Read [AGENTS.md](AGENTS.md) for repository rules. Preserve unrelated local work.
4. Keep each pull request focused on one problem, with documentation updated when
   behavior or setup changes.

The [project structure](README.md#project-structure) explains the shared Core and
Runtime layers and the two platform frontends. Route emulation changes through
the session owner thread; keep platform-specific code in the frontends. Follow
[.editorconfig](.editorconfig) and the surrounding code style.

## Verify your change

Use the [README test commands](README.md#development-and-testing) and the
[CI workflow](.github/workflows/ci.yml). Run the suites affected by your change;
shared Core/Runtime changes should be checked against both frontends. State which
platforms you tested and which you could not test.

For emulation or save-data fixes, add a focused regression using generated programs
or synthetic data where possible. Keep real-game playtests distinct from automated
hardware/protocol checks: a connection or successful ROM boot does not establish
trade success or general compatibility. Online sessions must preserve original
saves through the existing session-copy workflow.

Do not commit ROMs, BIOS images, e-Reader firmware/cards, personal saves, private
credentials or diagnostic archives containing game data. Preserve third-party
notices and update [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) when dependencies
or adapted source change. Dependency updates should include their intentional
lockfile changes, rather than bypassing locked restores.

## UI and localization changes

Read the [UI copy guide](docs/UI_COPY_GUIDE.md) and
[mandatory Aether component inventory](docs/UI_THEME_HANDOFF_2026-09-28.md#windows-standardelemente-verbindliche-fundliste--02102026)
before changing the interface.

- Reuse shared Aether components and add missing components centrally. Preserve
  all six themes, custom colors, keyboard/controller access, DPI behavior and
  accessibility. Linux uses its own SDL implementation of the shared design.
- Add app-owned text through `AetherBoy.Runtime.Localization.UiText` and the
  [shared catalog](nanoboy/Runtime/Localization/UiText.json). Use explicit
  localization markers in embedded browser resources. Keep user data, paths,
  protocol keys and diagnostic identifiers unchanged.
- Run `python3 scripts/review-ui-copy.py <changed-files>` after UI copy edits and
  review its findings manually.
- For Windows UI changes, run `WindowsUiPolicyTests` in the SmokeTests project
  and the affected functional/theme tests. Reduce removed entries in
  [WindowsUiDebt.json](tests/AetherBoy.SmokeTests/WindowsUiDebt.json); do not increase
  or blindly regenerate the baseline to make tests pass.
- Check actual windows, dropdowns, context menus, scrolling, focus and error states.
  Automated structure checks do not replace visual and interaction testing.

## Submit a pull request

Target `development` and explain the problem, resulting behavior and verification.
Link the related issue when there is one. Include reproduction steps for fixes and
screenshots or interaction evidence for visible UI changes. Mention known limits,
skipped checks and any save-state or data-format compatibility impact.

For bug reports, include the version/commit, operating system, Linux compositor
where relevant, reproduction steps and expected versus actual behavior. Review
logs before attaching them and remove private information.
