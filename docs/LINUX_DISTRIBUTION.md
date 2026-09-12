# Linux self-contained distribution

The portable tarball bundles the .NET runtime and SDL alongside AetherBoy. Users
extract it and run the launcher; they do not need to install .NET or build the
project. It is a native Wayland application and continues to store settings,
firmware and saves in the normal XDG user directories. Moving the program folder
does not move those user files.

## Build a package

From a Git checkout with the SDK selected by `global.json`, Python 3.12 or newer,
GNU tar, gzip and the usual Linux command-line tools:

```bash
bash scripts/package-linux.sh
bash scripts/package-linux.sh --runtime linux-arm64
```

The default architecture is the host's `linux-x64` or `linux-arm64`. Use
`--output "/path/with spaces"` to choose the destination. Build output is under
`artifacts/packages` by default, with a `.tar.gz` and companion `.sha256` file.
The script fails on unsupported architectures or missing licenses/assets.

The script takes an isolated source snapshot before building. It restores locked
NuGet dependencies and uses separate build intermediates, leaving development
`bin`/`obj` directories alone. The package records the Git revision, whether the
checkout was modified, SDK and runtime versions in `package-info.json`. That
metadata identifies a local modified build honestly; it is not a release tag.

The archive contains the corresponding Linux source in `source.tar.gz`, the
project license, full runtime/package/font/vendored notices and the required UI
assets. The source snapshot permits only source/build files and branding assets;
it excludes ignored files, build output, ROMs, BIOS images and user data. To
compile the extracted source directly, use the SDK in its `global.json`:

```bash
dotnet publish frontends/AetherBoy.Desktop/AetherBoy.Desktop.csproj \
  -c Release --self-contained true -r linux-x64
```

Use `linux-arm64` for ARM64. The package wrapper itself requires a Git checkout.
Tar entries have sorted names, normalized owners and the commit timestamp;
`gzip -n` omits creation timestamps. Set `SOURCE_DATE_EPOCH` to override the
archive timestamp. These steps make archive construction repeatable for
identical inputs. They are not evidence of byte-for-byte reproducibility across
different SDKs, NuGet runtime packs or operating systems.

## Run an extracted package

For the current project version (adjust the filename for later versions):

```bash
sha256sum -c AetherBoy-4.8.0-alpha.1-linux-x64-self-contained.tar.gz.sha256
tar -xzf AetherBoy-4.8.0-alpha.1-linux-x64-self-contained.tar.gz
cd AetherBoy-4.8.0-alpha.1-linux-x64-self-contained
./launch-linux.sh
./launch-linux.sh "/path/to/game.gba"
```

This format does not install a menu entry, change the existing installation or
provide automatic updates. Extract a newer build into a new folder and launch
that folder. The existing development launcher and user installer keep their
current behavior.

## Host requirements and limits

Self-contained refers to .NET; this is not a complete bundled Linux system.
Users still need:

- the matching x86-64 or ARM64 CPU architecture and glibc **2.38 or newer**;
- native Wayland, appropriate graphics drivers and Wayland client libraries;
- the system dependencies used by the bundled .NET runtime, including ICU,
  OpenSSL, libstdc++, libgcc and libc;
- PipeWire, PulseAudio or ALSA client libraries for sound;
- an XDG desktop portal with a file chooser backend for native file dialogs.

See [the Wayland setup guide](LINUX_WAYLAND.md) for desktop integration. Alpine
Linux/musl is not a supported target of these `linux-*` archives. ARM64 can be
cross-published on x64, but an x64 host cannot validate ARM64 execution. The
packaging check explicitly prints that skip. A successful `--version` check
proves bundled-runtime startup, not rendering, sound or game compatibility.

The glibc floor above comes from inspecting the actual SDL3-CS.Linux 3.4.16
`libSDL3.so` version requirements with `readelf --version-info`: both x64 and
ARM64 require `GLIBC_2.38`. Bundling .NET does not remove that native requirement.
Do not advertise these archives as working on every Linux distribution.

## Isolated package check

The build runs the check automatically. It can also inspect an existing archive:

```bash
python3 scripts/test-linux-package.py \
  artifacts/packages/AetherBoy-4.8.0-alpha.1-linux-x64-self-contained.tar.gz
```

The check extracts into a temporary path containing spaces and a literal `$`,
checks safe archive paths, expected licenses/assets and corresponding source,
and starts the launcher with a PATH that has no `dotnet` and deliberately invalid
`DOTNET_ROOT` values. It uses temporary XDG folders. It does not install the app,
read personal saves, open a game or perform a long-running playtest.

## Later packaging options

AppImage could add a single-file launcher and desktop integration, but the
current tarball's bundled system-library requirements would still need an audit
against the intended distribution baseline. Flatpak would additionally need a
runtime choice, Wayland/audio/controller permissions, portal behavior and an
update/publishing process. Neither format is implemented or represented as
validated by this tarball work.


## Optional native accessibility UI

`./launch-linux.sh --accessible` opens the additional GTK3 Control Center;
Ctrl+F7 opens it from the application. GTK3, ATK and the desktop AT-SPI bridge
are supplied by the Linux distribution, not included in this archive. On Ubuntu
24.04 these are provided by `libgtk-3-0t64` and `at-spi2-core`; other distributions
use their native GTK3 packages. The normal SDL application continues to work
without this optional toolkit. The package's isolated `--version` smoke check
does not establish an Orca or full desktop accessibility qualification.
