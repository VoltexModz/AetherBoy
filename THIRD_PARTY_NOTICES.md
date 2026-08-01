# Third-Party Notices

This file records direct package dependencies declared by the repository as inspected on 2026-08-01. It does not relicense those components or replace their upstream license files.

## Direct dependencies

| Scope | Package | Version | Declared license | Upstream |
| --- | --- | ---: | --- | --- |
| Windows frontend | NAudio.WinMM | 2.3.0 | MIT | <https://github.com/naudio/NAudio> |
| Test/build | MSTest.Sdk | 4.3.2 | MIT | <https://github.com/microsoft/testfx> |

`NAudio.WinMM` depends on `NAudio.Core`. `MSTest.Sdk` brings the Microsoft Testing Platform and its test-host dependencies. The committed `packages.lock.json` files are the authoritative record of the complete resolved dependency graph for each project.

OpenTK, OpenTK.GLControl and the excluded legacy OpenAL implementation were removed in Phase 1. XInput is called as a Windows system API and is not redistributed as a package by this repository.

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
