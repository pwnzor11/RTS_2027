# Third-party notices

This file lists every third-party component in the RTS_2027 Windows build (the publish folder, `publish/Game`),
with its version, licence and upstream source. It is copied into the publish output as `THIRD_PARTY_NOTICES.md`.
The project's own licence and the GPL v3 section 7 permission are in `LICENSE`; project notices are in `NOTICE.md`.
Facts were taken from the NuGet packages the build restores (licence fields and copyright text in each package).

## Components

| Component (file in publish folder) | Version | Licence | Copyright holder | Used for | Upstream source |
|---|---|---|---|---|---|
| MonoGame.Framework.DesktopGL (`MonoGame.Framework.dll`) | 3.8.6 | Microsoft Public License (Ms-PL); some portions MIT (Mono.Xna Team) | MonoGame project contributors | Window, graphics, input, audio (game framework) | https://github.com/MonoGame/MonoGame |
| MonoGame.Library.SDL (`SDL2.dll`) | SDL 2.32.10 | zlib licence | Sam Lantinga and SDL contributors | Window and input back end (native) | https://github.com/libsdl-org/SDL and https://www.libsdl.org/ |
| MonoGame.Library.OpenAL (`openal.dll`) | OpenAL Soft 1.24.3 | GNU LGPL (version 2, per the DLL's own copyright string); portions BSD-3-Clause | OpenAL Soft authors (see upstream; BSD portions credit Archontis Politis, Anis A. Hireche and Christopher Robinson) | Audio back end (native) | https://github.com/kcat/openal-soft |
| NVorbis (`NVorbis.dll`) | 0.10.4 | MIT | Andrew Ward | Ogg Vorbis decoding (used by the MonoGame audio stack) | https://github.com/NVorbis/NVorbis |
| Tomlyn (`Tomlyn.dll`) | 2.10.1 | BSD-2-Clause | Alexandre Mutel | Reads the TOML rule and map files (`src/Data`) | https://xoofx.github.io/Tomlyn/ |
| MoonSharp (`MoonSharp.Interpreter.dll`) | 2.0.0 (2.0.0.0) | BSD-3-Clause (nuspec lists only the licence URL; text from the upstream `LICENSE`); string library portions based on KopiLua (MIT) | Marco Mastropaolo (2014-2016); KopiLua portions Copyright (c) 2012 LoDC | Embedded Lua interpreter for campaign mission scripts (`src/Scripting`) | https://github.com/moonsharp-devs/moonsharp and http://www.moonsharp.org/ |
| System.IO.Hashing (`System.IO.Hashing.dll`) | 10.0.14 | MIT | Microsoft Corporation (.NET Foundation and Contributors) | XxHash64 for rules and simulation state hashes (`src/Sim`, `src/Data`) | https://dot.net/ |
| .NET runtime (`System.*.dll`, `Microsoft.*.dll`, `mscorlib.dll`, `netstandard.dll`, runtime host and native files) | 10.0.14 | MIT (.NET Foundation and Contributors); bundled components keep their own notices, see below | .NET Foundation and Contributors | Runtime, self-contained | https://github.com/dotnet/runtime |

Managed libraries the game uses directly are referenced at build time only (Tests and Tools, not shipped): xUnit,
coverlet and Microsoft.CodeAnalysis.

## Native libraries in the publish folder

The publish folder contains these non-managed (native) DLLs and executables. The first three are third-party
libraries; the rest belong to the .NET runtime.

| File | Origin | Licence |
|---|---|---|
| `SDL2.dll` | MonoGame.Library.SDL | zlib |
| `openal.dll` | MonoGame.Library.OpenAL (OpenAL Soft) | LGPL 2 (see below) |
| `System.IO.Compression.Native.dll` | .NET runtime (includes zlib-ng) | MIT / zlib (see the runtime notices) |
| `msquic.dll` | .NET runtime (MsQuic) | MIT (see the runtime notices) |
| `coreclr.dll`, `clrjit.dll`, `clrgc.dll`, `clrgcexp.dll`, `clretwrc.dll`, `mscorrc.dll`, `mscordaccore.dll`, `mscordaccore_amd64_amd64_10.0.1426.52326.dll`, `mscordbi.dll`, `hostfxr.dll`, `hostpolicy.dll`, `createdump.exe` | .NET runtime | MIT (.NET Foundation and Contributors) |
| `Microsoft.DiaSymReader.Native.amd64.dll` | .NET runtime (debugging symbol support) | Licence as shipped by Microsoft in the .NET runtime pack |

## OpenAL Soft (LGPL): dynamic linking and replacement

`openal.dll` is an OpenAL Soft build (version 1.24.3) that RTS_2027 loads dynamically at run time. It is not
statically linked into `Game.exe`. You may replace `openal.dll` in the game folder with another build of OpenAL
Soft that works with the same interface, and the game will load it.

The source code of OpenAL Soft 1.24.3 is available from its upstream project: https://github.com/kcat/openal-soft
(release 1.24.3). The MonoGame packaging of this build is at https://github.com/MonoGame/MonoGame .
The full GNU Library General Public License version 2 text ships beside the game as `THIRD_PARTY_LGPL-2.0.txt`
(also at https://www.gnu.org/licenses/old-licenses/lgpl-2.0.html).

Licence notice (as packaged with MonoGame.Library.OpenAL):

```text
Portions of this software are licensed under the BSD 3-Clause license.

Copyright (c) 2015, Archontis Politis
Copyright (c) 2019, Anis A. Hireche
Copyright (c) 2019, Christopher Robinson
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

* Neither the name of Spherical-Harmonic-Transform nor the names of its
  contributors may be used to endorse or promote products derived from
  this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

## SDL2 (zlib licence)

Source: https://github.com/libsdl-org/SDL (version 2.32.10). Licence notice (as packaged with MonoGame.Library.SDL):

```text
Copyright (C) 1997-2025 Sam Lantinga <slouken@libsdl.org>
  
This software is provided 'as-is', without any express or implied
warranty.  In no event will the authors be held liable for any damages
arising from the use of this software.

Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:
  
1. The origin of this software must not be misrepresented; you must not
   claim that you wrote the original software. If you use this software
   in a product, an acknowledgment in the product documentation would be
   appreciated but is not required. 
2. Altered source versions must be plainly marked as such, and must not be
   misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.
```

## NVorbis (MIT)

```text
MIT License

Copyright (c) 2020 Andrew Ward

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## .NET runtime (MIT)

The full runtime notices, including those of components bundled into the runtime (zlib-ng, MsQuic and others), are in
the runtime pack file `THIRD-PARTY-NOTICES.TXT` (NuGet package `Microsoft.NETCore.App.Runtime.win-x64` 10.0.14). That
file is not copied into the publish folder yet; see the upstream at https://github.com/dotnet/runtime . Licence notice
for the runtime:

```text
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Tomlyn (BSD-2-Clause)

Source: https://xoofx.github.io/Tomlyn/ (version 2.10.1).

```text
Copyright (c) Alexandre Mutel. All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

## MoonSharp (BSD-3-Clause)

Source: http://www.moonsharp.org/ (version 2.0.0.0; the NuGet package carries only a licence URL, the text below is the
`LICENSE` file of the upstream repository https://github.com/xanathar/moonsharp).

```text
Copyright (c) 2014-2016, Marco Mastropaolo
All rights reserved.

Parts of the string library are based on the KopiLua project (https://github.com/NLua/KopiLua)
Copyright (c) 2012 LoDC

Visual Studio Code debugger code is based on code from Microsoft vscode-mono-debug project
(https://github.com/Microsoft/vscode-mono-debug).
Copyright (c) Microsoft Corporation - released under MIT license.

Remote Debugger icons are from the Eclipse project (https://www.eclipse.org/).
Copyright of The Eclipse Foundation

The MoonSharp icon is (c) Isaac, 2014-2015

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

* Neither the name of the {organization} nor the names of its
  contributors may be used to endorse or promote products derived from
  this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

## System.IO.Hashing (MIT)

Copyright (c) Microsoft Corporation. Licensed under the MIT License; the licence text above (.NET Foundation and
Contributors) is the same MIT licence used for the .NET family of packages.

## MonoGame (Microsoft Public License)

MonoGame.Framework is licensed under the Microsoft Public License (Ms-PL). Per the MonoGame licence file, a few
portions are under the MIT License (copyright The Mono.Xna Team). RTS_2027 is a GPL v3 program; the additional
permission under GPL v3 section 7 in `LICENSE` covers combining RTS_2027 with MonoGame and its native libraries.
The MonoGame source is at https://github.com/MonoGame/MonoGame .

## 0 A.D. art (Creative Commons Attribution-ShareAlike 3.0)

The map prop sprites in `data/sprites/props/zeroad/` are renders of 0 A.D. meshes and textures, recoloured per theater. Source:
0 A.D. (c) Wildfire Games and the 0 A.D. contributors, https://play0ad.com/ , art licensed CC BY-SA 3.0
(https://creativecommons.org/licenses/by-sa/3.0/). The images were changed from the originals (rendered at 64 px with a new camera and
light, palette-mapped). Share-alike: these images, and any work derived from them, must be distributed under CC BY-SA 3.0 or a
compatible licence; they are separate data files and are not covered by the GPL of the game code. See
`data/licences/CC-BY-SA-3.0_0AD_NOTICE.txt` and `docs/CREDITS.md`.
