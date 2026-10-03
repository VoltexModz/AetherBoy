#!/usr/bin/env python3
"""Read-only comparison of a Windows ZIP and Linux tarball from one test build."""
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import sys
import tarfile
import zipfile


def safe_name(name):
    path = PurePosixPath(name)
    assert not path.is_absolute() and ".." not in path.parts and "\\" not in name, name
    assert path.suffix.lower() not in {".gb", ".gbc", ".gba", ".sav", ".srm"}, name


def zip_contents(stream):
    with zipfile.ZipFile(stream) as archive:
        result = {}
        for item in archive.infolist():
            safe_name(item.filename)
            if not item.is_dir():
                assert item.filename not in result, item.filename
                result[item.filename] = archive.read(item)
        return result


def tar_contents(stream):
    with tarfile.open(fileobj=stream, mode="r:gz") as archive:
        result = {}
        for item in archive:
            safe_name(item.name)
            assert item.isfile() or item.isdir(), item.name
            if item.isfile():
                assert item.name not in result, item.name
                result[item.name] = archive.extractfile(item).read()
        return result


def unroot(files):
    roots = {name.split("/", 1)[0] for name in files}
    assert len(roots) == 1, roots
    return {name.split("/", 1)[1]: data for name, data in files.items()}


windows = unroot(zip_contents(sys.argv[1]))
with open(sys.argv[2], "rb") as stream:
    linux = unroot(tar_contents(stream))
win_info = json.loads(windows["package-info.json"])
linux_info = json.loads(linux["package-info.json"])
for key in ("buildId", "sourceCommit", "sourceStatus", "channel"):
    assert win_info[key] == linux_info[key], key
assert win_info["runtimeIdentifier"] == "win-x64"
assert linux_info["runtimeIdentifier"] == "linux-x64"
assert windows["TEST_BUILD_CHECKLIST.txt"] == linux["TEST_BUILD_CHECKLIST.txt"]
required = {"AetherBoy.exe", "hostfxr.dll", "coreclr.dll", "datachannel.dll", "LICENSE",
            "THIRD_PARTY_NOTICES.md", "source.zip", "START_HERE.txt"}
assert required <= windows.keys(), required - windows.keys()
runtime = json.loads(windows["AetherBoy.runtimeconfig.json"])
assert {f["name"] for f in runtime["runtimeOptions"]["includedFrameworks"]} == {
    "Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App"}
manifest = {}
for line in windows["SHA256SUMS.txt"].decode().splitlines():
    digest, name = line.split("  ", 1)
    assert name not in manifest
    manifest[name] = digest
assert manifest.keys() == windows.keys() - {"SHA256SUMS.txt"}
for name, digest in manifest.items():
    assert hashlib.sha256(windows[name]).hexdigest() == digest, name
win_source = unroot(zip_contents(io.BytesIO(windows["source.zip"])))
linux_source = unroot(tar_contents(io.BytesIO(linux["source.tar.gz"])))
assert win_source["nanoboy/Branding/AetherBoyMark.png"] == linux["Assets/aetherboy-mark.png"], "Logo assets differ"
prefixes = ("nanoboy/Core/", "nanoboy/Runtime/", "third_party/GBADotnet.Core/")


def shared_source(files):
    return {name: data.replace(b"\r\n", b"\n") for name, data in files.items()
            if name.startswith(prefixes) and Path(name).suffix in {".cs", ".csproj"}}


first, second = shared_source(win_source), shared_source(linux_source)
assert first and first.keys() == second.keys(), "Shared source file lists differ"
for name, data in first.items():
    assert data == second[name], name
assert win_source["Directory.Build.props"] == linux_source["Directory.Build.props"]
digest = hashlib.sha256()
for name, data in sorted(first.items()):
    digest.update(name.encode() + b"\0" + hashlib.sha256(data).digest())
print(json.dumps({"result": "PASS", "buildId": win_info["buildId"],
                  "sharedSourceFiles": len(first), "sharedSourceSha256": digest.hexdigest(),
                  "windowsManifestFiles": len(manifest)}, indent=2))
