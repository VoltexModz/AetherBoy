#!/usr/bin/env bash
set -euo pipefail

application_root="$(dirname -- "$(readlink -f -- "${BASH_SOURCE[0]}")")"
export SDL_VIDEO_DRIVER=wayland

# Shared by the development runner and the installed desktop entry. Desktop
# sessions often do not inherit the user's interactive shell PATH.
if [[ -z "${DOTNET_ROOT:-}" ]]; then
    if command -v dotnet >/dev/null 2>&1; then
        export DOTNET_ROOT="$(dirname -- "$(readlink -f -- "$(command -v dotnet)")")"
    elif [[ -x "$HOME/.dotnet/dotnet" ]]; then
        export DOTNET_ROOT="$HOME/.dotnet"
    elif [[ -f "$application_root/dotnet-root.txt" ]]; then
        IFS= read -r installed_runtime < "$application_root/dotnet-root.txt"
        if [[ -x "$installed_runtime/dotnet" ]]; then export DOTNET_ROOT="$installed_runtime"; fi
    fi
fi

exec "$application_root/AetherBoy.Desktop" "$@"
