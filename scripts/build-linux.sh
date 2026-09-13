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

if ! command -v dotnet >/dev/null 2>&1; then
    printf 'The .NET 10 SDK is required. See docs/LINUX_WAYLAND.md.\n' >&2
    exit 2
fi

project="$repository_root/frontends/AetherBoy.Desktop/AetherBoy.Desktop.csproj"
output="$repository_root/artifacts/AetherBoy-$runtime_id"

bash "$repository_root/scripts/build-online-native.sh"

dotnet restore "$project" \
    --locked-mode \
    --configfile "$repository_root/NuGet.config"

dotnet publish "$project" \
    --configuration Release \
    --runtime "$runtime_id" \
    --self-contained false \
    --no-restore \
    -p:ContinuousIntegrationBuild=true \
    --output "$output"

install -m 0755 "$repository_root/scripts/launch-linux.sh" "$output/launch-linux.sh"
printf '%s\n' "$(dirname -- "$(readlink -f -- "$(command -v dotnet)")")" > "$output/dotnet-root.txt"

printf 'AetherBoy was published to %s\n' "$output"
printf 'Run: bash scripts/run-linux.sh "/path/to/game.gba"\n'
