#!/usr/bin/env python3
"""Exercise the real installer in isolated XDG/bin directories, never the user's installation."""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

repo = Path(__file__).resolve().parent.parent

with tempfile.TemporaryDirectory(prefix="aetherboy install ") as directory:
    root = Path(directory)
    data = root / "data with spaces"
    binary = root / 'bin with spaces $ literal'
    app = data / "aetherboy"
    binary.mkdir(parents=True)
    (app / "saves").mkdir(parents=True)
    sentinel = app / "saves" / "keep.txt"
    sentinel.write_text("personal save")
    legacy = app / "AetherBoy.Desktop"
    legacy.write_text("#!/bin/sh\nprintf 'legacy\\n'\n")
    legacy.chmod(0o755)
    launcher = binary / "aetherboy"
    launcher.symlink_to(legacy)
    desktop = data / "applications" / "io.github.VoltexModz.AetherBoy.desktop"
    desktop.parent.mkdir()
    desktop.write_text("legacy desktop")
    env = os.environ | {"XDG_DATA_HOME": str(data), "AETHERBOY_BIN_HOME": str(binary)}
    fault = root / "fault"
    fault.mkdir()
    fail_install = fault / "install"
    fail_install.write_text('#!/bin/bash\ncase "$*" in *.png*) exit 71;; *) exec /usr/bin/install "$@";; esac\n')
    fail_install.chmod(0o755)
    broken = env | {"PATH": str(fault) + ":" + os.environ["PATH"]}

    def install(environment, success=True):
        result = subprocess.run(["bash", str(repo / "scripts/install-linux-user.sh")], env=environment,
                                cwd=repo, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        print(result.stdout)
        assert (result.returncode == 0) == success, result.returncode

    install(broken, False)
    assert launcher.resolve() == legacy
    assert desktop.read_text() == "legacy desktop"
    assert sentinel.read_text() == "personal save"
    install(env)
    current = (app / "program/current").resolve()
    subprocess.run(["desktop-file-validate", str(desktop)], check=True)
    # A desktop PATH with no dotnet executable must still use the recorded runtime root.
    minimal = root / "minimal-path"
    minimal.mkdir()
    for command in ["bash", "dirname", "readlink"]:
        (minimal / command).symlink_to(shutil.which(command))
    desktop_env = env | {"PATH": str(minimal)}
    desktop_env.pop("DOTNET_ROOT", None)
    version = subprocess.check_output([str(launcher), "--version"], env=desktop_env, text=True).strip()
    assert version.startswith("4.8.0"), version
    install(broken, False)
    assert (app / "program/current").resolve() == current
    assert launcher.resolve().parent == current
    subprocess.run(["bash", str(repo / "scripts/uninstall-linux-user.sh")], env=env, check=True)
    assert sentinel.read_text() == "personal save"
    assert not launcher.exists()
    assert not (app / "program").exists()
    print("PASS: legacy migration failure, spaces/special characters, minimal PATH, failed update, data-preserving uninstall")
