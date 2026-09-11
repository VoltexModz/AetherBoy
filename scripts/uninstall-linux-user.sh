#!/usr/bin/env bash
set -euo pipefail

data_root="${XDG_DATA_HOME:-$HOME/.local/share}"
[[ "$data_root" = /* ]] || data_root="$HOME/.local/share"
binary_root="${AETHERBOY_BIN_HOME:-$HOME/.local/bin}"
[[ "$binary_root" = /* ]] || { printf 'AETHERBOY_BIN_HOME must be absolute.\n' >&2; exit 2; }
program_root="$data_root/aetherboy/program"
if [[ -L "$binary_root/aetherboy" && "$(readlink -- "$binary_root/aetherboy")" == "$program_root/current/launch-linux.sh" ]]; then
    rm -- "$binary_root/aetherboy"
fi
rm -rf -- "$program_root"
rm -f -- "$data_root/applications/io.github.VoltexModz.AetherBoy.desktop"
for size in 16 24 32 48 64 128 256 512; do
    rm -f -- "$data_root/icons/hicolor/${size}x${size}/apps/io.github.VoltexModz.AetherBoy.png"
done
printf 'Removed the installed program. Saves, firmware, recordings, preferences and reports are preserved.\n'
