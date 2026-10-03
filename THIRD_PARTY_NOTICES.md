# Third-Party Notices

This file records package dependencies and provenance, updated through the Windows development package on 2026-09-11. It does not relicense those components or replace their upstream license files.

## Direct dependencies

The user-supplied mGBA source archive was reviewed on 2026-09-07 as an engineering
reference only. It is MPL-2.0-licensed and remains in an ignored local reference
directory, retaining its original notices. It is not linked, built or included in
the product. No mGBA function bodies were imported or mechanically translated in
the initial review step. During the later HLE-BIOS work, `src/gba/bios.c` was
consulted alongside documented GBA BIOS service contracts to validate observable
service behavior. AetherBoy's `HleBios.cs` is an independently written C#
implementation; no C function body, BIOS binary or test ROM was copied into the
product. The archive identity, inspected areas, hardware-documentation sources
for the newly written code and limitations are recorded in
[docs/MGBA_REVIEW.md](docs/MGBA_REVIEW.md). This is not a claim of formal clean-room
development. The later cheat-decoder source reuse is recorded below separately.

### mGBA cheat cipher adaptations (2026-10-01)

`nanoboy/Runtime/GbaCheatCipher.cs` and `GbaCheatTables.cs` include C# adaptations
of the GameShark/Action Replay TEA/reseed and CodeBreaker cipher algorithms and
four protocol tables from `src/gba/cheats/{gameshark,parv3,codebreaker}.c`.
Copyright (c) 2013-2016 Jeffrey Pfau. These covered files retain MPL-2.0, including
AetherBoy's modifications; this does not replace the license of unrelated files.
The runtime compiler/executor and host UI are integrated in AetherBoy, not an
embedded mGBA emulator or frontend. The supplied archive's SHA-256 is recorded
in [docs/MGBA_REVIEW.md](docs/MGBA_REVIEW.md).

Full license and provenance: [third_party/mgba-cheats](third_party/mgba-cheats).
Build/publish includes both under `licenses/mgba-cheats/`. Distributors must
make corresponding source, including modifications to covered files, available
under MPL-2.0; keep the supplied notices and make the source for the actual binary
revision available. Reference upstream: <https://github.com/mgba-emu/mgba>.

The GBA runtime includes a source snapshot of
[DaveTCode/GBADotnet](https://github.com/DaveTCode/GBADotnet) at commit
`994c4b225c6e4277ada8d37bb9283f53827ee3e1` (2022-05-16). The upstream core is
MIT licensed, Copyright (c) 2022 David Tyler. AetherBoy vendors 66 original core
source files and four outputs produced by the upstream source generators. The
snapshot, its full license and the AetherBoy build wrapper are kept under
[`third_party/GBADotnet.Core`](third_party/GBADotnet.Core); integration and known
limitations are recorded in
[`docs/GBADOTNET_REVIEW.md`](docs/GBADOTNET_REVIEW.md). No upstream ROM, BIOS,
compatibility screenshot or UI project is redistributed.

| Scope | Package | Version | Declared license | Upstream |
| --- | --- | ---: | --- | --- |
| Windows frontend | NAudio.WinMM | 2.3.0 | MIT | <https://github.com/naudio/NAudio> |
| Windows audio | NAudio.Wasapi | 2.3.0 | MIT | <https://github.com/naudio/NAudio> |
| Windows GPU presentation | Vortice.Direct2D1 / DXGI / DirectX | 3.8.3 | MIT | <https://github.com/amerkoleci/Vortice.Windows> |
| Windows GPU dependency | Vortice.Mathematics | 2.1.0 | MIT | <https://github.com/amerkoleci/Vortice.Mathematics> |
| Windows COM dependencies | SharpGen.Runtime / Runtime.COM | 2.4.2-beta | MIT | <https://github.com/SharpGenTools/SharpGenTools> |
| Linux Wayland frontend | SDL3-CS | 3.4.16 | Zlib | <https://github.com/edwardgushchin/SDL3-CS> |
| Linux Wayland native runtime | SDL3-CS.Linux | 3.4.16 | Zlib | <https://github.com/edwardgushchin/SDL3-CS> |
| Test/build | MSTest.Sdk | 4.3.2 | MIT | <https://github.com/microsoft/testfx> |

`NAudio.WinMM` depends on `NAudio.Core`. `MSTest.Sdk` brings the Microsoft Testing Platform and its test-host dependencies. The committed `packages.lock.json` files are the authoritative record of the complete resolved dependency graph for each project.

OpenTK, OpenTK.GLControl and the excluded legacy OpenAL implementation were removed in Phase 1. XInput is called as a Windows system API and is not redistributed as a package by this repository.

## SDL3-CS and SDL3-CS.Linux 3.4.16

Copyright (C) 2024-2026 Eduard Gushchin <eduardgushchin@yandex.ru>

These packages use the zlib license. The license permits use, modification and
redistribution, prohibits misrepresenting the original authorship, requires
altered source versions to be marked, and requires the notice to remain in source
distributions. The complete upstream notice is included in both resolved NuGet
packages; its terms are not replaced by this summary.

## NAudio.WinMM, NAudio.Wasapi and NAudio.Core 2.3.0

The NuGet packages declare the MIT license, link to the NAudio repository, identify repository commit `c89fee940ee6f8d7374d18714a6b85d8b7a18ab0`, and record:

Copyright © Mark Heath 2023

Copyright © Mark Heath 2026

## Vortice and SharpGen Windows presentation dependencies

The resolved Vortice.Direct2D1, Vortice.DXGI and Vortice.DirectX 3.8.3 NuGet
packages identify upstream commit `9e609cb9439c9872aa1b339f177e40ec96f77239`.
These packages and Vortice.Mathematics 2.1.0 declare MIT and record:

Copyright (c) Amer Koleci and Contributors

SharpGen.Runtime and SharpGen.Runtime.COM 2.4.2-beta are dependencies selected
by Vortice 3.8.3, not standalone core replacements. They declare MIT and record:

(c) 2010-2017 Alexandre Mutel, 2017-2023 Jeremy Koritzinsky, 2023-2024 Amer Koleci

The complete MIT text below applies with these notices. Direct2D, DXGI and WASAPI
themselves are Windows system APIs; no Windows system DLL is bundled.

## MSTest.Sdk 4.3.2

The NuGet package declares the MIT license, links to the Microsoft TestFX repository, identifies repository commit `c3fdb81b8a5584664d82f4bcec6577f6d918b5fe`, and records:

Copyright © Microsoft Corporation. All rights reserved.

## MIT license text

The following license text applies to the MIT-licensed packages above together with their respective copyright notices:

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

Before publishing binaries, audit the actual publish directory and retain every license or notice required by the resolved runtime packages.

## Patch Lab format references (no additional runtime dependency)

The BPS format specification by byuu is marked public domain:
https://github.com/Alcaro/Flips/blob/master/bps_spec.md
The bounded C# IPS/BPS/UPS reader in `nanoboy/Runtime/Cartridges/RomPatcher.cs` is an
AetherBoy implementation. The supplied mGBA source archive's feature list and
IPS handling were reviewed for reference; no mGBA implementation or core was
imported as part of the Patch Lab / diagnostics / Inspector package.

## Bundled Noto Sans UI fonts

`branding/fonts/NotoSans-Regular.ttf` and `NotoSans-Bold.ttf` are unmodified
Noto Sans fonts. Copyright 2022 The Noto Project Authors
(https://github.com/notofonts/latin-greek-cyrillic). Distributed under the SIL Open
Font License 1.1; see `branding/fonts/OFL.txt` (published as `Assets/Fonts/OFL.txt`).
The generated UI glyph atlases and metrics accompany the fonts under that license.
Regenerate them with `python3 scripts/build-font-atlas.py` using Pillow.

## Native online room connections (2026-09-13)

AetherBoy's room transport uses **libdatachannel 0.24.5** (MPL-2.0),
Copyright Paul-Louis Ageneau and contributors. Corresponding source, including
submodule revisions, is available at
https://github.com/paullouisageneau/libdatachannel/tree/443f6934d9007eb7076ab7825ba330f355fcbead.
The library is unmodified. `scripts/build-online-native.sh` reproduces the Linux
build with media and WebSocket support disabled. The C# adapter and private room
service are AetherBoy code. The shared library may be replaced by a compatible
build of the same C ABI.

Windows x64 binaries come from `MediaToolkit.WebRtc.Native.win-x64` **0.24.5.1**
(MPL-2.0 package), maintained at https://github.com/Rukhlov/MediaToolkit.NetCore
(package source revision `c8d6edec0032cd99b46193abaee8a2a62b3a9f8b`).
Its `datachannel.dll` includes libjuice (MPL-2.0), usrsctp (BSD), plog (MIT),
libsrtp (BSD), and OpenSSL (Apache-2.0). Linux builds include libjuice, usrsctp and
plog and dynamically link the operating system's OpenSSL 3 libraries.
License texts are distributed in `licenses/online-native/`, sourced from
`third_party/online-native-licenses/`. Upstream source notices remain applicable.

The optional room service image uses Node.js 24's official Alpine image.
Its runtime and operating system retain the notices shipped in that image.
Coturn is independently deployed by the server administrator, not bundled into
the emulator.

## ZIP / 7z ROM import (2026-10-01)

Both frontends use the unmodified **SharpCompress 0.50.4** NuGet package (MIT),
by Adam Hathcock and contributors. Source revision:
https://github.com/adamhathcock/sharpcompress/tree/c083c6efd843a844b0c8f7878787360e815be781.
Upstream license: https://github.com/adamhathcock/sharpcompress/blob/0.50.4/LICENSE.txt.
The license is shipped as `licenses/sharpcompress/LICENSE.txt`, from
`third_party/sharpcompress/LICENSE.txt`. Package metadata additionally states
Copyright (c) 2025 Adam Hathcock. Archive selection, staging, bounds, import and
the AVI capture writer are AetherBoy implementation, not copied upstream code.

## Optional Discord activity (2026-10-02)

Both frontends use the unmodified **DiscordRichPresence 1.6.1.70** NuGet package
by Lachee (MIT, Copyright (c) 2021 Lachee), plus **Newtonsoft.Json 13.0.4**
(MIT, Copyright (c) 2007 James Newton-King). The RPC library performs local
Discord IPC only when the configured activity setting is enabled; application policy and UI are
AetherBoy code. No Discord Social SDK native binary is bundled.

- Source: https://github.com/Lachee/discord-rpc-csharp/tree/v1.6.1
- Source: https://github.com/JamesNK/Newtonsoft.Json/tree/13.0.4
- License texts: `licenses/discord-rpc/LICENSE.txt` and
  `licenses/discord-rpc/Newtonsoft.Json-LICENSE.txt`, copied from
  `third_party/discord-rpc/` in Windows and Linux builds/publish output.
