#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
case "$(uname -m)" in
    x86_64) runtime_id="linux-x64" ;;
    aarch64|arm64) runtime_id="linux-arm64" ;;
    *) printf 'Unsupported architecture.\n' >&2; exit 2 ;;
esac

# Never install an old publish directory after a source update.
bash "$repository_root/scripts/build-linux.sh"
publish_directory="$repository_root/artifacts/AetherBoy-$runtime_id"
data_root="${XDG_DATA_HOME:-$HOME/.local/share}"
[[ "$data_root" = /* ]] || data_root="$HOME/.local/share"
program_root="$data_root/aetherboy/program"
binary_root="${AETHERBOY_BIN_HOME:-$HOME/.local/bin}"
[[ "$binary_root" = /* ]] || { printf 'AETHERBOY_BIN_HOME must be absolute.\n' >&2; exit 2; }
mkdir -p "$program_root/releases" "$binary_root" "$data_root/applications"
staging="$(mktemp -d "$program_root/releases/build-XXXXXXXX")"
committed=false
cleanup() {
    if [[ "$committed" == false ]]; then rm -rf -- "$staging"; fi
    rm -f -- "$program_root/.current-$$" "$binary_root/.aetherboy-$$" "${desktop:-$data_root/applications/io.github.VoltexModz.AetherBoy.desktop}.tmp"
}
trap cleanup EXIT
cp -a "$publish_directory/." "$staging/"
bash "$staging/launch-linux.sh" --version >/dev/null

# Prepare shell and desktop integration before switching the active release.
launcher="$binary_root/aetherboy"
ln -sfn -- "$program_root/current/launch-linux.sh" "$binary_root/.aetherboy-$$"

# Desktop Exec quoting has a second layer of escaping over desktop string values.
desktop_quote() {
    local value="$1"
    value="${value//\\/\\\\}"
    value="${value//\"/\\\"}"
    value="${value//\$/\\\$}"
    value="${value//\`/\\\`}"
    value="${value//%/%%}"
    value="${value//\\/\\\\}"
    printf '\"%s\"' "$value"
}
desktop="$data_root/applications/io.github.VoltexModz.AetherBoy.desktop"
while IFS= read -r line; do
    case "$line" in
        Exec=*) printf 'Exec=%s %%f\n' "$(desktop_quote "$launcher")" ;;
        TryExec=*) : ;; # Absolute Exec is sufficient; do not depend on GUI PATH.
        *) printf '%s\n' "$line" ;;
    esac
done < "$repository_root/packaging/linux/io.github.VoltexModz.AetherBoy.desktop" > "$desktop.tmp"

for size in 16 24 32 48 64 128 256 512; do
    install -Dm 0644 "$repository_root/branding/exports/aetherboy-mark-$size.png" \
        "$data_root/icons/hicolor/${size}x${size}/apps/io.github.VoltexModz.AetherBoy.png"
done
ln -s -- "$staging" "$program_root/.current-$$"
mv -Tf -- "$program_root/.current-$$" "$program_root/current"
committed=true
mv -Tf -- "$binary_root/.aetherboy-$$" "$launcher"
mv -f -- "$desktop.tmp" "$desktop"
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$data_root/applications" || true
fi
printf 'Installed current build. Previous releases and personal saves are preserved.\n'
printf 'Launch from the app menu, or: %s\n' "$launcher"
