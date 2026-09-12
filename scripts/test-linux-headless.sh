#!/usr/bin/env bash
# A private compositor/session bus: never maps the AT-SPI probe on a user's desktop.
set -euo pipefail
repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
test_assembly="${1:-$repository_root/tests/AetherBoy.DesktopTests/bin/Release/net10.0/AetherBoy.DesktopTests.dll}"
if (($#)); then shift; fi
for dependency in weston dbus-run-session python3 dotnet; do
    command -v "$dependency" >/dev/null || { printf 'Missing test dependency: %s\n' "$dependency" >&2; exit 2; }
done
export XDG_RUNTIME_DIR="$(mktemp -d)"
chmod 700 "$XDG_RUNTIME_DIR"
export WAYLAND_DISPLAY=wayland-accessibility-test
export XDG_SESSION_TYPE=wayland XDG_CURRENT_DESKTOP=Weston SDL_RENDER_DRIVER=software
export GSETTINGS_BACKEND=memory NO_AT_BRIDGE=0 GDK_BACKEND=wayland
unset AT_SPI_BUS_ADDRESS DBUS_STARTER_ADDRESS DBUS_STARTER_BUS_TYPE
compositor_pid=""
cleanup() {
    if [[ -n "$compositor_pid" ]]; then kill "$compositor_pid" 2>/dev/null || true; wait "$compositor_pid" 2>/dev/null || true; fi
    rm -rf -- "$XDG_RUNTIME_DIR"
}
trap cleanup EXIT
weston --backend=headless-backend.so --renderer=pixman --socket="$WAYLAND_DISPLAY" --idle-time=0 --no-config > "$XDG_RUNTIME_DIR/weston.log" 2>&1 &
compositor_pid=$!
for attempt in {1..50}; do
    test -S "$XDG_RUNTIME_DIR/$WAYLAND_DISPLAY" && break
    kill -0 "$compositor_pid" || { cat "$XDG_RUNTIME_DIR/weston.log" >&2; exit 1; }
    sleep 0.1
done
test -S "$XDG_RUNTIME_DIR/$WAYLAND_DISPLAY"
dbus-run-session -- env AETHERBOY_PLAYTEST=0 AETHERBOY_UI_TESTS=1 AETHERBOY_ACCESSIBILITY_TESTS=1 \
    AETHERBOY_ACCESSIBILITY_BUS_TESTS=1 AETHERBOY_ISOLATED_ACCESSIBILITY_BUS=1 \
    dotnet "$test_assembly" "$@"
