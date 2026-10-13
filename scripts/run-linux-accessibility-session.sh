#!/usr/bin/env bash
# Invoked only inside test-linux-headless.sh's private compositor and session bus.
set -euo pipefail
[[ -z "${WAYLAND_SOCKET+x}" && "${WAYLAND_DISPLAY:-}" == wayland-accessibility-test && "${XDG_CURRENT_DESKTOP:-}" == Weston &&
   -S "${XDG_RUNTIME_DIR:?}/$WAYLAND_DISPLAY" && -n "${DBUS_SESSION_BUS_ADDRESS:-}" ]] || {
    printf 'A private Weston display and session bus are required.\n' >&2; exit 2;
}
registry=""
for candidate in /usr/lib/at-spi2-registryd /usr/libexec/at-spi2-registryd; do
    if [[ -x "$candidate" ]]; then registry="$candidate"; break; fi
done
[[ -n "$registry" ]] || { printf 'Missing AT-SPI registry daemon.\n' >&2; exit 2; }
command -v dbus-daemon >/dev/null || { printf 'Missing dbus-daemon.\n' >&2; exit 2; }
mkdir -p "$XDG_RUNTIME_DIR/accessibility-services"
cat > "$XDG_RUNTIME_DIR/accessibility-services/org.a11y.atspi.Registry.service" <<EOF
[D-BUS Service]
Name=org.a11y.atspi.Registry
Exec=$registry
EOF
cat > "$XDG_RUNTIME_DIR/accessibility.conf" <<EOF
<!DOCTYPE busconfig PUBLIC "-//freedesktop//DTD D-Bus Bus Configuration 1.0//EN" "http://www.freedesktop.org/standards/dbus/1.0/busconfig.dtd">
<busconfig>
  <type>accessibility</type>
  <listen>unix:path=$XDG_RUNTIME_DIR/accessibility-bus</listen>
  <auth>EXTERNAL</auth>
  <servicedir>$XDG_RUNTIME_DIR/accessibility-services</servicedir>
  <policy context="default">
    <allow user="*"/>
    <allow own="*"/>
    <allow send_destination="*"/>
    <allow receive_sender="*"/>
  </policy>
</busconfig>
EOF
# Explicit daemon avoids distro-specific systemd activation inside the private bus.
# The 0700 runtime directory restricts both sockets to this test's user.
export AT_SPI_BUS_ADDRESS="unix:path=$XDG_RUNTIME_DIR/accessibility-bus"
dbus-daemon --config-file="$XDG_RUNTIME_DIR/accessibility.conf" --nofork > "$XDG_RUNTIME_DIR/accessibility-bus.log" 2>&1 &
accessibility_pid=$!
cleanup() { kill "$accessibility_pid" 2>/dev/null || true; wait "$accessibility_pid" 2>/dev/null || true; }
trap cleanup EXIT
for attempt in {1..50}; do
    test -S "$XDG_RUNTIME_DIR/accessibility-bus" && break
    kill -0 "$accessibility_pid" || { cat "$XDG_RUNTIME_DIR/accessibility-bus.log" >&2; exit 1; }
    sleep 0.1
done
test -S "$XDG_RUNTIME_DIR/accessibility-bus"
export AETHERBOY_PLAYTEST=0 AETHERBOY_UI_TESTS=1 AETHERBOY_ACCESSIBILITY_TESTS=1
export AETHERBOY_ACCESSIBILITY_BUS_TESTS=1 AETHERBOY_ISOLATED_ACCESSIBILITY_BUS=1
dotnet "$@"
