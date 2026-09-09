# Third-Party Notices

This file records direct package dependencies declared by the repository as inspected on 2026-08-01. It does not relicense those components or replace their upstream license files.

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
development. Any future source reuse requires its own provenance/license review.

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

## NAudio.WinMM and NAudio.Core 2.3.0

The NuGet packages declare the MIT license, link to the NAudio repository, identify repository commit `c89fee940ee6f8d7374d18714a6b85d8b7a18ab0`, and record:

Copyright © Mark Heath 2023

Copyright © Mark Heath 2026

## MSTest.Sdk 4.3.2

The NuGet package declares the MIT license, links to the Microsoft TestFX repository, identifies repository commit `c3fdb81b8a5584664d82f4bcec6577f6d918b5fe`, and records:

Copyright © Microsoft Corporation. All rights reserved.

## MIT license text

The following license text applies to the MIT-licensed packages above together with their respective copyright notices:

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

Before publishing binaries, audit the actual publish directory and retain every license or notice required by the resolved runtime packages.
