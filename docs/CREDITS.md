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

## Audio
| Asset | Source | Licence | Use |
|---|---|---|---|
| Generated sound effects (`data/audio/sfx/`) | `tools/audio/sfxr.py` (this repository) | GPL-3.0-or-later (code); output released CC0 (ASSUMED, M3-INT: owner to confirm) | Sound effects |
| Real sound recordings | none yet; add each one here with its download page. CC0 only: Kenney (kenney.nl), OpenGameArt, Freesound (CC0 filter), BigSoundBank (`docs/audio/music_brief.md`) | CC0 | Sound effects |
| Music tracks (`data/music/*.ogg`) | Music generated with ACE-Step (Apache-2.0). Released CC0 / no rights claimed (D-080) | CC0 | Menu and in-game music |

## AI-assisted artwork
| Asset | Tool | Human work done | Notes |
|---|---|---|---|
| data/images/stills/still_authority_act2.png | Blender 5.2 (Cycles), procedural scene written by Claude (session 14), tools/art/stills/still_kit.py + still_authority_act2.py / _act3.py / _act4.py, still_authority_extra.py | Owner picked the style (D-169) and approved the pilot | Original, built from primitives in code; no external models, textures or images; no text |
| data/images/stills/still_authority_act3.png | Blender 5.2 (Cycles), procedural scene written by Claude (session 14), tools/art/stills/still_kit.py + still_authority_act2.py / _act3.py / _act4.py, still_authority_extra.py | Owner picked the style (D-169) and approved the pilot | Original, built from primitives in code; no external models, textures or images; no text |
| data/images/stills/still_authority_act4.png | Blender 5.2 (Cycles), procedural scene written by Claude (session 14), tools/art/stills/still_kit.py + still_authority_act2.py / _act3.py / _act4.py, still_authority_extra.py | Owner picked the style (D-169) and approved the pilot | Original, built from primitives in code; no external models, textures or images; no text |
| data/images/stills/still_directorate_act2.png | Blender 5.2 (Cycles), procedural scene written by Claude (session 14), tools/art/stills/still_kit.py + still_directorate_kit.py + still_directorate_actN.py | Owner picked the style (D-169) and approved the pilot | Original, built from primitives in code; no external models, textures or images; no text |
| data/images/stills/still_directorate_act3.png | Blender 5.2 (Cycles), procedural scene written by Claude (session 14), tools/art/stills/still_kit.py + still_directorate_kit.py + still_directorate_actN.py | Owner picked the style (D-169) and approved the pilot | Original, built from primitives in code; no external models, textures or images; no text |
| data/images/stills/still_directorate_act4.png | Blender 5.2 (Cycles), procedural scene written by Claude (session 14), tools/art/stills/still_kit.py + still_directorate_kit.py + still_directorate_actN.py | Owner picked the style (D-169) and approved the pilot | Original, built from primitives in code; no external models, textures or images; no text |
| data/images/stills/still_left_behind_act2.png | Blender 5.2 (Cycles), procedural scene written by Claude (session 14), tools/art/stills/still_kit.py + still_lb_kit.py + still_lb_actN.py | Owner picked the style (D-169) and approved the pilot | Original, built from primitives in code; no external models, textures or images; no text |
| data/images/stills/still_left_behind_act3.png | Blender 5.2 (Cycles), procedural scene written by Claude (session 14), tools/art/stills/still_kit.py + still_lb_kit.py + still_lb_actN.py | Owner picked the style (D-169) and approved the pilot | Original, built from primitives in code; no external models, textures or images; no text |
| data/images/stills/still_left_behind_act4.png | Blender 5.2 (Cycles), procedural scene written by Claude (session 14), tools/art/stills/still_kit.py + still_lb_kit.py + still_lb_actN.py | Owner picked the style (D-169) and approved the pilot | Original, built from primitives in code; no external models, textures or images; no text |
| data/images/stills/still_expedition_act2.png | Blender 5.2 (Cycles), procedural scene written by Claude (session 14), tools/art/stills/still_kit.py + still_expedition_kit.py + still_expedition_actN.py | Owner picked the style (D-169) and approved the pilot | Original, built from primitives in code; no external models, textures or images; no text |
| data/images/stills/still_expedition_act3.png | Blender 5.2 (Cycles), procedural scene written by Claude (session 14), tools/art/stills/still_kit.py + still_expedition_kit.py + still_expedition_actN.py | Owner picked the style (D-169) and approved the pilot | Original, built from primitives in code; no external models, textures or images; no text |
| data/images/stills/still_expedition_act4.png | Blender 5.2 (Cycles), procedural scene written by Claude (session 14), tools/art/stills/still_kit.py + still_expedition_kit.py + still_expedition_actN.py | Owner picked the style (D-169) and approved the pilot | Original, built from primitives in code; no external models, textures or images; no text |
