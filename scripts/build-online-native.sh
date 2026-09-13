#!/usr/bin/env bash
set -euo pipefail
repo="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
case "$(uname -m)" in
  x86_64) rid=linux-x64 ;;
  aarch64|arm64) rid=linux-arm64 ;;
  *) printf 'Unsupported online native architecture\n' >&2; exit 1 ;;
esac
revision=443f6934d9007eb7076ab7825ba330f355fcbead
source_dir="$repo/artifacts/native-source/libdatachannel"
build_dir="$repo/artifacts/native-build/$rid"
output_dir="$repo/artifacts/native/$rid"
if [[ -f "$output_dir/revision" && -f "$output_dir/libdatachannel.so" ]] && [[ "$(cat "$output_dir/revision")" == "$revision" ]]; then
  exit 0
fi
if [[ ! -d "$source_dir/.git" ]]; then
  git clone --branch v0.24.5 --depth 1 --recurse-submodules --shallow-submodules \
    https://github.com/paullouisageneau/libdatachannel.git "$source_dir"
fi
[[ "$(git -C "$source_dir" rev-parse HEAD)" == "$revision" ]] || { printf 'Unexpected native dependency revision\n' >&2; exit 1; }
cmake -S "$source_dir" -B "$build_dir" -DCMAKE_BUILD_TYPE=Release \
  -DNO_MEDIA=ON -DNO_WEBSOCKET=ON -DNO_EXAMPLES=ON -DNO_TESTS=ON \
  -DUSE_GNUTLS=OFF -DUSE_MBEDTLS=OFF -DBUILD_SHARED_LIBS=ON
cmake --build "$build_dir" --parallel "${AETHERBOY_BUILD_JOBS:-4}"
mkdir -p "$output_dir"
cp -L "$build_dir/libdatachannel.so" "$output_dir/libdatachannel.so"
printf '%s\n' "$revision" > "$output_dir/revision"
