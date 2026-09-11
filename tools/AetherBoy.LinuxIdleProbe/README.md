# Linux idle probe

Measures wall time and process CPU time for an existing Desktop build running
a hidden native Wayland window without a ROM. It uses an isolated temporary
settings directory and does not modify the target build. This is not a visible
monitor, gameplay, input-latency or audio benchmark.

Run in an existing Wayland session after building the probe:

```sh
dotnet build tools/AetherBoy.LinuxIdleProbe -c Release
desktop_build="$PWD/frontends/AetherBoy.Desktop/bin/Release/net10.0"
LD_LIBRARY_PATH="$desktop_build/runtimes/linux-x64/native${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}" \
  dotnet tools/AetherBoy.LinuxIdleProbe/bin/Release/net10.0/AetherBoy.LinuxIdleProbe.dll \
  "$desktop_build" 8
```

Use the matching native runtime directory on ARM64. The target directory must
include its dependencies and assets. For a comparison, run the same command in
separate processes against preserved baseline and current builds on the same
machine. JSON output records duration, CPU seconds and the host assembly SHA-256.
The utility accesses the host through reflection so that the baseline assembly
can remain unchanged; incompatible host constructor changes may require updating
the probe.
