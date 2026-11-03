# Credits and asset provenance
Every third-party asset, library and AI-assisted artwork is listed here (D-032, D-033).

## Libraries
| Library | Licence | Use |
|---|---|---|
| MonoGame.Framework.DesktopGL 3.8.6 | Microsoft Public License (Ms-PL); some MIT portions (Mono.Xna Team). Used under GPL v3 section 7 permission in `LICENSE` | Game framework (window, graphics, input, audio) |
| MonoGame.Library.SDL (SDL 2.32.10), `SDL2.dll` | zlib | Native window and input back end |
| MonoGame.Library.OpenAL (OpenAL Soft 1.24.3), `openal.dll` | LGPL 2 (dynamically loaded, replaceable); portions BSD-3-Clause | Native audio back end |
| NVorbis 0.10.4 | MIT | Ogg Vorbis decoding (via MonoGame audio) |
| Tomlyn 2.10.1 | BSD-2-Clause | TOML parsing of rules and maps (`src/Data`) |
| MoonSharp 2.0.0 (2.0.0.0) | BSD-3-Clause (nuspec gives only the licence URL; text is the upstream `LICENSE`, copyright 2014-2016 Marco Mastropaolo; its string library is based on KopiLua, MIT, Copyright 2012 LoDC) | Embedded Lua interpreter for campaign mission scripts (`src/Scripting`, D-015) |
| System.IO.Hashing 10.0.14 | MIT | XxHash64 for rules and sim state hashes |
| .NET runtime 10.0.14 (self-contained) | MIT; bundled components keep their own notices | Runtime |

Full notices, licence texts and the list of native files are in `THIRD_PARTY_NOTICES.md` (copied into every publish).

## Imported assets
| Asset | Source | Licence | Use |
|---|---|---|---|
| Noto Sans Regular and Bold (`NotoSans-Regular.ttf`, `NotoSans-Bold.ttf`), version 2.004 (font metadata), unmodified. Debian package `fonts-noto-core` 20201225-2; upstream https://github.com/googlei18n/noto-fonts, Copyright 2015 Google LLC. Licence text in `data/fonts/OFL.txt` | Debian `fonts-noto-core` 20201225-2 (`/usr/share/fonts/truetype/noto/`) | OFL-1.1 | UI text: rasterised to atlases `data/fonts/noto-sans-*_<size>.png` and `.font.toml` by `tools/fonts/build_atlas.py` |

## AI-assisted artwork
| Asset | Tool | Human work done | Notes |
|---|---|---|---|
| (none yet) | | | |
