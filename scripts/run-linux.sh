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

if [[ "${XDG_SESSION_TYPE:-}" == "x11" || -z "${WAYLAND_DISPLAY:-}" ]]; then
    printf 'AetherBoy requires native Wayland; X11/XWayland is disabled.\n' >&2
    printf 'XDG_SESSION_TYPE=%s WAYLAND_DISPLAY=%s\n' \
        "${XDG_SESSION_TYPE:-}" "${WAYLAND_DISPLAY:-}" >&2
    exit 2
fi

application="$repository_root/artifacts/AetherBoy-$runtime_id/AetherBoy.Desktop"
if [[ ! -x "$application" ]]; then
    printf 'No Linux build found; building it now.\n'
    bash "$repository_root/scripts/build-linux.sh"
fi

export SDL_VIDEO_DRIVER=wayland

# Framework-dependent apphosts search DOTNET_ROOT and registered system paths.
# Also support user-local SDK installs such as ~/.dotnet that are only on PATH.
if [[ -z "${DOTNET_ROOT:-}" ]] && command -v dotnet >/dev/null 2>&1; then
    dotnet_executable="$(readlink -f "$(command -v dotnet)")"
    export DOTNET_ROOT="$(dirname -- "$dotnet_executable")"
fi

exec "$application" "$@"
