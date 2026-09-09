#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
architecture="$(uname -m)"

case "$architecture" in
    x86_64) runtime_id="linux-x64" ;;
    aarch64|arm64) runtime_id="linux-arm64" ;;
    *)
        printf 'Unsupported Linux architecture: %s\n' "$architecture" >&2
        exit 2
        ;;
esac

publish_directory="$repository_root/artifacts/AetherBoy-$runtime_id"
if [[ ! -x "$publish_directory/AetherBoy.Desktop" ]]; then
    bash "$repository_root/scripts/build-linux.sh"
fi

data_root="${XDG_DATA_HOME:-$HOME/.local/share}"
application_root="$data_root/aetherboy"
binary_root="$HOME/.local/bin"

mkdir -p "$application_root" "$binary_root" "$data_root/applications"
cp -a "$publish_directory/." "$application_root/"
ln -sfn "$application_root/AetherBoy.Desktop" "$binary_root/aetherboy"
install -m 0644 \
    "$repository_root/packaging/linux/io.github.VoltexModz.AetherBoy.desktop" \
    "$data_root/applications/io.github.VoltexModz.AetherBoy.desktop"

for icon_size in 16 24 32 48 64 128 256 512; do
    icon_source="$repository_root/branding/exports/aetherboy-mark-$icon_size.png"
    icon_target="$data_root/icons/hicolor/${icon_size}x${icon_size}/apps/io.github.VoltexModz.AetherBoy.png"
    install -Dm 0644 "$icon_source" "$icon_target"
done

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$data_root/applications"
fi

printf 'Installed AetherBoy for the current user.\n'
printf 'Ensure %s is on PATH, then launch: aetherboy "/path/to/game.gba"\n' "$binary_root"
