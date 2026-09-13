#!/usr/bin/env python3
"""Validate an archive in temporary directories, without the user's installation."""
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import tarfile
import tempfile

if len(sys.argv) != 2:
    raise SystemExit('Usage: python3 scripts/test-linux-package.py PACKAGE.tar.gz')
with tempfile.TemporaryDirectory(prefix='aetherboy package $ check ') as directory:
    root = Path(directory)
    with tarfile.open(sys.argv[1], 'r:gz') as archive:
        members = archive.getmembers()
        for member in members:
            path = Path(member.name)
            if path.is_absolute() or '..' in path.parts or member.issym() or member.islnk():
                raise SystemExit(f'Unsafe archive entry: {member.name}')
            if path.suffix.lower() in {'.gba', '.gb', '.gbc', '.sav', '.bios'}:
                raise SystemExit(f'User/game data unexpectedly packaged: {member.name}')
        archive.extractall(root, filter='data')
    applications = list(root.iterdir())
    assert len(applications) == 1 and applications[0].is_dir(), 'Expected one application directory'
    app = applications[0]
    metadata = json.loads((app / 'package-info.json').read_text())
    machine = {'linux-x64': 62, 'linux-arm64': 183}[metadata['runtimeIdentifier']]
    for required in ('AetherBoy.Desktop', 'libhostfxr.so', 'libcoreclr.so', 'libSDL3.so',
                     'launch-linux.sh', 'LICENSE', 'licenses/DOTNET-LICENSE.TXT',
                     'licenses/DOTNET-THIRD-PARTY-NOTICES.TXT',
                     'licenses/SDL3-CS-LICENSE.txt', 'licenses/SDL3-CS.Linux-LICENSE.txt',
                     'licenses/GBADotnet-LICENSE.md', 'Assets/Fonts/OFL.txt'):
        assert (app / required).is_file(), required
    for executable in ('AetherBoy.Desktop', 'libhostfxr.so', 'libcoreclr.so', 'libSDL3.so', 'libdatachannel.so'):
        with (app / executable).open('rb') as stream:
            header = stream.read(20)
        assert header[:6] == b'\x7fELF\x02\x01', f'Expected 64-bit little-endian ELF: {executable}'
        assert int.from_bytes(header[18:20], 'little') == machine, f'Wrong CPU architecture: {executable}'
    with tarfile.open(app / 'source.tar.gz') as source:
        names = source.getnames()
        assert 'source/frontends/AetherBoy.Desktop/AetherBoy.Desktop.csproj' in names
        assert 'source/scripts/package-linux.sh' in names
        assert not any('/bin/' in name or '/obj/' in name or '/.git/' in name for name in names)
    host_rid = {'x86_64': 'linux-x64', 'aarch64': 'linux-arm64', 'arm64': 'linux-arm64'}.get(platform.machine())
    if metadata['runtimeIdentifier'] == host_rid:
        minimal = root / 'minimal-path'
        minimal.mkdir()
        for command in ('bash', 'dirname', 'readlink'):
            (minimal / command).symlink_to(shutil.which(command))
        env = os.environ | {'PATH': str(minimal), 'DOTNET_ROOT': str(root / 'no-installed-runtime'),
                            'DOTNET_ROOT_X64': str(root / 'no-installed-runtime'),
                            'DOTNET_ROOT_ARM64': str(root / 'no-installed-runtime'),
                            'XDG_DATA_HOME': str(root / 'data'), 'XDG_CONFIG_HOME': str(root / 'config'),
                            'XDG_STATE_HOME': str(root / 'state'), 'XDG_CACHE_HOME': str(root / 'cache')}
        version = subprocess.check_output([str(app / 'launch-linux.sh'), '--version'], env=env,
                                          cwd=root, text=True, timeout=20).strip()
        assert version.startswith(metadata['applicationVersion']), version
        print(f'PASS: archive, source, notices, paths with spaces/$, bundled-runtime launch: {version}')
    else:
        print(f"PASS: archive/source/notices for {metadata['runtimeIdentifier']}; execution SKIPPED on {host_rid}")
