#!/usr/bin/env bash
# Build from an isolated source snapshot; never touches the development bin/obj.
set -euo pipefail
umask 022
repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
case "$(uname -m)" in x86_64) runtime_id=linux-x64 ;; aarch64|arm64) runtime_id=linux-arm64 ;; *) runtime_id=unsupported ;; esac
output="$repository_root/artifacts/packages"
usage() { printf 'Usage: bash scripts/package-linux.sh [--runtime linux-x64|linux-arm64] [--output DIRECTORY]\n'; }
while (($#)); do
    case "$1" in
        --runtime|--output) if (($# < 2)); then usage >&2; exit 2; fi
            if [[ "$1" == --runtime ]]; then runtime_id="$2"; else output="$2"; fi; shift 2 ;;
        --help|-h) usage; exit 0 ;;
        *) usage >&2; exit 2 ;;
    esac
done
case "$runtime_id" in linux-x64|linux-arm64) ;; *) printf 'Unsupported runtime: %s\n' "$runtime_id" >&2; exit 2 ;; esac
for command in dotnet python3 git tar gzip sha256sum; do command -v "$command" >/dev/null || { printf 'Required build tool missing: %s\n' "$command" >&2; exit 2; }; done
mkdir -p -- "$output"
output="$(cd -- "$output" && pwd)"
staging="$(mktemp -d "${TMPDIR:-/tmp}/aetherboy-package.XXXXXXXX")"
trap 'rm -rf -- "$staging"' EXIT
export SOURCE_DATE_EPOCH="${SOURCE_DATE_EPOCH:-$(git -C "$repository_root" log -1 --format=%ct)}"
[[ "$SOURCE_DATE_EPOCH" =~ ^[0-9]+$ ]] || { printf 'SOURCE_DATE_EPOCH must be an integer.\n' >&2; exit 2; }
revision="$(git -C "$repository_root" rev-parse HEAD)"
source_status=clean
if [[ -n "$(git -C "$repository_root" status --porcelain --untracked-files=normal)" ]]; then source_status=modified; fi
# Only source/build inputs and approved branding assets enter the snapshot. No
# ignored files, local settings, user data, ROMs, firmware, or build outputs.
python3 - "$repository_root" "$staging/source" <<'PY'
import pathlib, shutil, subprocess, sys
root, destination = map(pathlib.Path, sys.argv[1:])
prefixes = ('frontends/AetherBoy.Desktop/', 'nanoboy/Core/', 'nanoboy/Runtime/',
            'third_party/GBADotnet.Core/', 'third_party/online-native-licenses/', 'branding/', 'scripts/')
root_files = {'Directory.Build.props', 'Directory.Build.targets', 'NuGet.config', 'global.json',
              'LICENSE', 'THIRD_PARTY_NOTICES.md', 'CHANGELOG.md',
              'docs/GBADOTNET_REVIEW.md', 'docs/MGBA_REVIEW.md', 'docs/LINUX_DISTRIBUTION.md'}
source_extensions = {'.cs', '.csproj', '.props', '.targets', '.json', '.config', '.md', '.txt', '.sh', '.py', '.html', '.js'}
for raw in subprocess.check_output(['git', '-C', str(root), 'ls-files', '-cz', '--others', '--exclude-standard']).split(b'\0'):
    if not raw: continue
    relative = pathlib.Path(raw.decode())
    name = relative.as_posix()
    if name not in root_files and not name.startswith(prefixes): continue
    if any(part in {'bin', 'obj', '.git'} for part in relative.parts): continue
    if name not in root_files and relative.suffix.lower() not in source_extensions and not (
        name.startswith('branding/') and relative.suffix.lower() in {'.png', '.ttf'}): continue
    source = root / relative
    if source.is_symlink(): raise SystemExit(f'Source symlinks are not accepted: {relative}')
    if not source.is_file(): continue
    target = destination / relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, target)
PY
# Build the native dependency for this host; cross-packaging needs a native build
# from the target architecture in artifacts/native/<rid>.
if [[ ! -f "$repository_root/artifacts/native/$runtime_id/libdatachannel.so" ]]; then
    bash "$repository_root/scripts/build-online-native.sh"
fi
[[ -f "$repository_root/artifacts/native/$runtime_id/libdatachannel.so" ]] || {
    printf 'Build the online native library on the target architecture first: %s\n' "$runtime_id" >&2; exit 2;
}
mkdir -p "$staging/source/artifacts/native/$runtime_id"
cp "$repository_root/artifacts/native/$runtime_id/libdatachannel.so" "$staging/source/artifacts/native/$runtime_id/"
project="$staging/source/frontends/AetherBoy.Desktop/AetherBoy.Desktop.csproj"
dotnet restore "$project" --locked-mode --artifacts-path "$staging/build" --configfile "$staging/source/NuGet.config"
dotnet publish "$project" --no-restore --configuration Release --runtime "$runtime_id" --self-contained true \
    --artifacts-path "$staging/build" --output "$staging/application" \
    -p:ContinuousIntegrationBuild=true -p:DebugType=None -p:DebugSymbols=false \
    -p:SourceRevisionId="$revision" -p:PathMap="$staging/source=/_/"
install -m 0755 "$staging/source/scripts/launch-linux.sh" "$staging/application/launch-linux.sh"
python3 - "$staging" "$runtime_id" "$revision" "$source_status" <<'PY'
import json, pathlib, shutil, subprocess, sys
stage, rid, revision, status = sys.argv[1:]
stage = pathlib.Path(stage)
app = stage / 'application'
assets = json.loads((stage / 'build/obj/AetherBoy.Desktop/project.assets.json').read_text())
package_root = pathlib.Path(next(iter(assets['packageFolders'])))
notices = app / 'licenses'
notices.mkdir(exist_ok=True)
for package in ('SDL3-CS', 'SDL3-CS.Linux'):
    key = next(key for key in assets['libraries'] if key.lower().startswith(package.lower() + '/'))
    shutil.copyfile(package_root / key.lower() / 'LICENSE', notices / f'{package}-LICENSE.txt')
shutil.copyfile(stage / 'source/third_party/GBADotnet.Core/LICENSE.md', notices / 'GBADotnet-LICENSE.md')
runtime_config = json.loads((app / 'AetherBoy.Desktop.runtimeconfig.json').read_text())
frameworks = runtime_config['runtimeOptions'].get('includedFrameworks', [])
if not any(f['name'] == 'Microsoft.NETCore.App' for f in frameworks):
    raise SystemExit('Publish is not self-contained.')
runtime_version = next(f['version'] for f in frameworks if f['name'] == 'Microsoft.NETCore.App')
runtime_package = package_root / f'microsoft.netcore.app.runtime.{rid}' / runtime_version
for filename in ('LICENSE.TXT', 'THIRD-PARTY-NOTICES.TXT'):
    shutil.copyfile(runtime_package / filename, notices / f'DOTNET-{filename}')
for required in ('libdatachannel.so', 'libhostfxr.so', 'libcoreclr.so', 'Assets/Fonts/OFL.txt',
                 'Assets/aetherboy-mark.png', 'LICENSE', 'THIRD_PARTY_NOTICES.md'):
    if not (app / required).is_file(): raise SystemExit(f'Publish is missing required file: {required}')
version = next(key.rsplit('/', 1)[1] for key in json.loads((app / 'AetherBoy.Desktop.deps.json').read_text())['libraries']
               if key.startswith('AetherBoy.Desktop/'))
metadata = {'applicationVersion': version, 'runtimeIdentifier': rid, 'sourceCommit': revision,
            'sourceStatus': status, 'sdkVersion': subprocess.check_output(['dotnet', '--version'], text=True).strip(),
            'frameworks': frameworks}
(app / 'package-info.json').write_text(json.dumps(metadata, indent=2) + '\n')
(app / 'README-PORTABLE.txt').write_text('AetherBoy for Linux / Wayland\n\nRun ./launch-linux.sh or ./launch-linux.sh "/path/to/game.gba".\n'
    'The .NET runtime is bundled. A matching Linux CPU architecture, glibc 2.38+, Wayland desktop,\n'
    'graphics driver and system audio/runtime libraries are still required.\n'
    'Settings and saves use normal XDG user folders, not this extracted directory.\n'
    'No ROMs or BIOS images are included. Corresponding Linux source is in source.tar.gz.\n'
    'Build the native library with bash scripts/build-online-native.sh, then dotnet publish frontends/AetherBoy.Desktop/AetherBoy.Desktop.csproj\n'
    '  -c Release --self-contained true -r linux-x64 (or linux-arm64). SDK: global.json.\n')
(stage / 'package-name').write_text(f'AetherBoy-{version}-{rid}-self-contained')
PY
rm -rf -- "$staging/source/artifacts"
# Sorted entries, normalized ownership/time and gzip without timestamps make
# archive creation repeatable for identical published files and source inputs.
tar --sort=name --mtime="@$SOURCE_DATE_EPOCH" --owner=0 --group=0 --numeric-owner \
    -C "$staging" -cf - source | gzip -n > "$staging/application/source.tar.gz"
package_name="$(cat "$staging/package-name")"
mv -- "$staging/application" "$staging/$package_name"
archive="$output/$package_name.tar.gz"
tar --sort=name --mtime="@$SOURCE_DATE_EPOCH" --owner=0 --group=0 --numeric-owner \
    -C "$staging" -cf - "$package_name" | gzip -n > "$staging/package.tar.gz"
# Validate extraction and --version on the host architecture before publishing
# the archive. Cross-architecture packages receive structural checks only.
python3 "$staging/source/scripts/test-linux-package.py" "$staging/package.tar.gz"
mv -- "$staging/package.tar.gz" "$archive"
(cd -- "$output" && sha256sum -- "$package_name.tar.gz" > "$package_name.tar.gz.sha256")
printf 'Package: %s\nChecksum: %s.sha256\n' "$archive" "$archive"
