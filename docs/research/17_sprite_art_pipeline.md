# 2D Sprite Art Pipeline for RTS_2027

**Summary**
- Classic Westwood sprites are 8-bit indexed frames drawn against a shared 256-colour VGA palette. House colour is handled by reserving a palette range that is remapped at draw time. The Tiberian Sun notes confirm a red remap range; the Tiberian Dawn terrain cell is 24x24 pixels.
- The popular claim that classic units were 3D-rendered from 3D Studio models is not confirmed by any source I read. Treat it as unverified.
- We have no artists and no EA art. Open art exists but is patchy. Kenney's CC0 tank pack gives usable placeholder vehicles but no buildings. Several RTS-grade packs (Hard Vacuum, the Tyrian remaster) have missing or unnamed licences. Much 0 A.D. and Warzone 2100 art is CC BY-SA 3.0, which I believe is not compatible with GPLv3 (from memory, needs checking).
- Blender pre-rendering is the best route to 8, 16 or 32 facings. Its output belongs to us. Hand pixel art in LibreSprite (GPL) or Pixelorama (MIT) is the best route for small hero art. Avoid Aseprite binaries, whose EULA forbids redistribution.
- AI image tools are improving but still fail on consistency across facings, frames and palettes. US copyright law gives no protection to outputs made from prompts alone. Disclosure rules and open-source bans on AI art are spreading, so this is an owner decision.
- My rough budget is 1,700 hours of hand pixel art or about 500 hours with a Blender pipeline. Facings and animation frames, not unit count, drive the cost.

## 1. How classic RTS sprites were made and stored

**Tiberian Dawn SHP (verified from the format page).** A 14-byte header holds the frame count, width, height, a `DeltaSize` (the largest buffer needed to decompress one frame) and flags. Bit 1 of the flags enables an embedded palette, which the games ignore because scenes already carry a palette. A frame table follows with `Frames + 2` entries of 8 bytes each. Each entry holds a 24-bit data offset, a format byte, a 24-bit reference offset and a reference format byte. The format byte selects one of three encodings:
- `0x80`: a full frame compressed with LCW.
- `0x40`: an XOR delta against a stored base frame.
- `0x20`: an XOR delta against the previous XOR frame, to save space in animations.

The palette is a 256-entry, 6-bit VGA palette stored in a separate `.PAL` file.

**Tiberian Sun / RA2 SHP (verified).** The TS format is simpler. It has an 8-byte header and 24-byte frame records holding x, y, width, height, flags (transparency and RLE), an RGB colour used for the minimap, and a data offset. Compression is a per-line RLE-Zero, not LCW. Shadows are separate frames painted in palette index 1 (earlier games used index 4). Tiberium uses the red remap range, and the page notes that the encoder "seems to have contained some system to compensate for remappable house colours." It does not list the exact indices.

**Other classic assets (verified).** Terrain cells are 24x24 pixels in the tileset format, and the game "defines cells as 24x24." Interpolation tables (`.PAL`, `.VQP`) stretch WSA and VQA animations. Fading tables (`.MRF`) are palette remapping tables used as colour filters for special effects. The Dune II-style SHP format is used only for the mouse cursor.

**Facings, turrets and infantry (partly verified).** A TS forum post states that TS allows "8 facings for SHP units (32 for the turret)." I found no verified facing counts for Tiberian Dawn or Red Alert units, or their pixel sizes. The RA2 "30x30" figure in the brief could not be verified. The isometric tile sources I read use 2:1 diamonds, not 30x30.

**Remaster approach (partly verified).** The 2020 Remastered Collection kept the 1995 engine. Lemon Sky Studios updated the graphics and animations to "rebuilt graphics" at 4K. The sources I read do not say whether sprites were redrawn, upscaled or rebuilt from the originals, and do not mention 3D renders.

**Dune II and Warcraft (partly verified).** The Dune II article does not describe how its art was made. It does say that Blizzard's Warcraft: Orcs & Humans started by "copying all artwork from Dune II" and then redesigning each asset. That is a useful model for what a legal, open clone can and cannot do.

**Unverified:** pre-rendering from 3D Studio, the number of frames per unit, and rotoscoping of infantry. These are common claims and should be checked against a developer interview before we rely on them.

## 2. Modern production options

### 2a. Blender pre-render pipeline

Blender is GPL. Its FAQ says that output made with it "is your sole property," so renders are ours to license.

The most concrete method I read is Clint Bellanger's isometric tile article (verified):
- Use a 2:1 tile. The base floor tile is 64x32; tall objects use 64x128, and footprints scale up (a 2x3 building is 160x256).
- Use an orthographic camera (no perspective shrink) at RotX 60, RotY 0, RotZ 45.
- Use Sun lamps, not point lamps, and the same lights for every render, so shading stays consistent.
- Put the camera and lights under an empty named "RenderPlatform." A script rotates that empty in 45-degree steps and renders 8 directions.
- Render at exactly the tile size, at 100% scale, as RGBA PNG with straight alpha.

SpriteForge (BSD-3-Clause, verified) is a standalone OpenGL tool, not a Blender add-on. It renders an `.obj` model into 8 directional PNGs, with a default camera elevation of 15 degrees and a default output of 512px. It also has an animation mode for a sequence of OBJ frames and can build a combined sheet. It has only 13 commits and no date, so treat it as a reference, not a dependency. A Blender forum thread ("Advice needed for rendering 3D blender meshes") points to a texture-atlas exporter, BlenderSpriter and Spritify, but the thread's author had not tested them, so they are unverified. A Blender Market paid kit and an itch "Pre-render Creator" appeared in search results but I did not read them.

**Sprytile is not suitable.** Its repository is a fork labelled as an "unofficial port for blender 2.8." It builds low-poly tile meshes, not sprites, has no releases, and I could not confirm its licence.

Post-processing (general method, not sourced): render large, downscale with nearest or box filtering, then quantise to the game palette with a script (Pillow or ImageMagick). The thread above uses ImageMagick `montage` to assemble sheets.

### 2b. Hand pixel art

- **Aseprite** is the best-known editor, but its EULA is not open source. Section 2(b) says "You may not distribute copies of the SOFTWARE PRODUCT to third parties." Section 2(g) allows compiling source "for your own personal purpose or to propose a contribution." We must not ship Aseprite, and we should ask a lawyer whether using it for our art is fine.
- **LibreSprite** is GPL and forked from Aseprite's last GPLv2 commit. It supports indexed colour, frame tags and onion skinning.
- **Pixelorama** is MIT. It supports indexed colour, isometric and hex tilemaps, animation timelines with frame tags, and PNG and spritesheet export. I did not confirm JSON sheet export.

Time per item is not in any source I read. My estimates: 8–15 hours for an infantry type at 8 facings, 25–50 hours for a vehicle at 16 body and 16 turret facings, and 10–30 hours for a building with construction stages.

### 2c. AI image generation

The 2026 state, from an industry guide (verified, but a single secondary source):
- **PixelLab** supports 4 and 8 direction rotation, skeleton animation and tilesets. Its top tier caps output at 400x400.
- **Sprixen** offers a "Style Lock" for palette, resolution and proportions.
- **Scenario** and **Layer** train custom models on our own art. Layer's animation is "limited."
- **Licences vary.** Scenario's free tier is "personal and evaluation use only." Leonardo's free tier is non-commercial. Layer has a commercial licence on every plan. The guide advises checking current terms for each tier. None of the sources I read covers training-data provenance.
- **Limitations:** generators that ignore palettes produce "thousands of near-identical shades," which breaks recolouring. Animation frames drift in proportion and colour. Outlines and tileset joins are hard.

Two legal and social facts matter more than the tooling:
- **Copyright (verified).** The US Copyright Office's Part 2 report (January 2025) finds that prompts alone do not give authorship. Human expressive contributions are needed, and "AI elements in a larger work" do not bar copyright. Output that is not copyrightable cannot be licensed by us, so GPL protection over it is weak.
- **Community (verified).** A GDC 2026 survey of 2,300 respondents found 52% saying generative AI has a negative impact on the industry (30% last year). Visual and technical artists were the most negative group, with 64% holding unfavourable views (as reported by TechSpot). Steam requires disclosure of AI content on store pages. OpenGameArt has no official policy, and a jam host requires disclosure and documentation of models and licences. The Widelands team drafted a rule to reject AI-generated contributions, including graphics and music, citing copyright and quality. A survey of 32 open-source AI policies found copyright among the main concerns, though it did not say which projects ban AI art.

### 2d. Vector and flat styles

Not researched. In principle, flat vector art is scalable and easy to recolour with palette tricks, and suits cameos and icons better than sprites. It moves away from the classic look, so it is an owner decision, not a default.

## 3. Asset budget for a two-faction game

The counts below are my estimates, not sourced. Two factions, about 14 combat and support types each.

| Category | Count (2 factions) | Frames per item | Hand hours per item | Blender hours per item |
|---|---|---|---|---|
| Infantry | 12 | ~136 (8 facings: idle, walk 6, fire 4, death 6) | 8–15 | 1–2 |
| Vehicles and harvesters | 16 | ~40 (16 body + 16 turret + death, flash) | 25–50 | 3–6 |
| Aircraft | 4 | ~64 (32 facings) | 30–50 | 5–8 |
| Buildings | 24 | ~11 (idle, 6 construction, 2 damage, rubble, shadow) | 10–30 | 2–4 |
| Terrain tiles | 3 theatres x ~200 | variants | ~40 per theatre | ~20 per theatre |
| Effects (explosions, smoke, flash) | ~40 sets | ~8 | 1–3 | 1 |
| UI (sidebar, buttons, cursors, fonts) | ~150 elements | 1–3 | ~100 total | same |
| Portraits and cameos | ~24 | 1–2 | 1–3 | 1 (plus AI cleanup) |

Totals: about 1,700 hand hours or about 500 Blender hours, plus 40–80 hours to build the pipeline. Frame count is dominated by facings times animations. Halving vehicle facings from 32 to 16 roughly halves that category. Whether mirroring left and right halves infantry work is unverified.

## 4. Open asset sources and licences

| Source | Licence (as found) | Fit for RTS_2027 | Verdict |
|---|---|---|---|
| Kenney, Top-Down Tanks Redux (itch.io) | CC0 1.0 (verified) | Over 180 sprites: tanks, missiles, explosions, crates, roads, tilesheets. No buildings. Pixel sizes not stated. | Use as placeholder. kenney.nl blocked my fetch (robots.txt). |
| Hard Vacuum art, Daniel Cook (OpenGameArt) | Not named in sources read. OpenHV says "released under a free license." | About 60 vehicles, 120 terrain tile sets, 30 buildings, UI screens, made on an Amiga 1000. Closest in style to our needs. | Promising. Get the licence text from the original download before use. OpenGameArt blocked my fetch. |
| OpenHV (Hard Vacuum revival) | Code GPL-3.0; assets CC BY-SA 4.0 (verified, but these are OpenHV's assets, not confirmed as Hard Vacuum's) | Engine-based RTS, art pipeline reference. | Study, do not assume its art is ours to use. |
| Remastered Tyrian Graphics (Daniel Cook) | Licence field blank on OpenGameArt | Space shooter art, not RTS | Avoid until licence is known. |
| 0 A.D. | Code GPL-2.0-or-later; binaries GPL-3.0-only; art CC BY-SA 3.0 (verified, Wikipedia) | Strong on armour and weapon detail | Licence mismatch risk for a GPLv3 project. Reference only. |
| Warzone 2100 data | Most data GPL-2+; wheels, backdrops and two texture pages CC BY-SA 3.0; one sky CC0 (verified, Debian copyright) | Mixed; GPL-2+ parts are usable under GPLv3 | Use only the GPL-2+ and CC0 files, with a manifest. |
| OpenRA | Engine GPL. Dune 2000 assets adopted in 2025; source licence not stated in what I read. | Original-game art largely comes from EA material (unverified here). | Do not use. Brief bars EA assets. |

Seen in search but not read: "Medieval RTS 120" and "Pirate pack 190" (OpenGameArt), and "Glest" and "Widelands" art. Their licences are unverified.

**Licence rule (from memory, verify):** CC BY-SA 4.0 is listed by the FSF as one-way compatible with GPLv3. CC BY-SA 3.0 is not. We should accept CC0, MIT, BSD, GPL-2-or-later, OFL fonts and CC BY-SA 4.0, and exclude CC BY-SA 3.0 and any pack without a stated licence.

## 5. Technical pipeline

- **Spritesheets and metadata.** Pack per unit and per facing where possible. Store animation metadata in our own format (JSON or TOML): animation name, facing count, frame list, duration, anchor point, shadow offset and hit box. Aseprite's JSON export is a usable model, but a community thread complains that its `frames` layout is awkward, so do not make the engine depend on it.
- **House colour (recommended: shader palette lookup).** Reserve a palette range, as the classics did, and store art as palette indices. A shader maps index to colour per player. This needs one art set, not one per colour. Pre-baking colour variants is simpler but multiplies disk and art edits. Shader and palette-swap techniques are common; I did not read a specific reference, so the mechanism is from general knowledge.
- **Shadows.** Classic games used a separate shadow frame in a known index (TS). Use a shadow sprite, not runtime projection, for the first pilot.
- **Draw order.** Top-down: sort by bottom edge (y), then by layer (ground decals, building shadows, ground units, air, effects). Isometric: sort by footprint depth, with tall objects using their footprint's front edge. General method, not sourced.
- **Damage and construction states.** Swap whole frames in the building record (intact, damaged, rubble) rather than compositing overlays.
- **Fog and shroud.** The Tiberian Dawn `.MRF` fading tables show a precedent: pre-computed colour filters. A darkened palette row or a tinted overlay is the modern equivalent.
- **Scaling.** Use integer scaling and nearest-neighbour filtering with a pixel-perfect camera. I could not verify engine-specific settings from the Godot docs page, which was truncated.
- **Asset tree.** Keep sources (`.blend`, `.aseprite`/`.ase`, `.xcf`) separate from exported PNGs. Use `assets/<faction>/<unit>/<anim>_<facing:02d>_<frame:02d>.png` with a `SOURCE.md` or manifest entry per folder recording author, URL, licence and date. Refer to art by id in unit definitions so mods replace art without code changes.

## 6. UI and UX art

- **Sidebar, cameos and minimap.** The TS format stores a minimap colour per frame, so each unit can have a representative colour. The sidebar and cameo layout itself is not covered in the sources I read; design it from our own UI sketches.
- **Cursors.** The classic cursors were SHP frames (Dune II style). Plan cursors as small indexed sprites, one per state (select, move, attack, repair, sell).
- **Fonts.** Original C&C fonts are `.FNT` files from EA. Do not use them. SIL's Open Font License allows commercial use and bundling with GPL software, and says a modified font with a Reserved Font Name must be renamed. I did not verify any specific pixel font's licence.

## Implications for RTS_2027

- **Recommendation:** Start with placeholders. Use Kenney's CC0 tanks plus our own greybox shapes for the first year of code. Art stays out of the critical path.
- **Recommendation:** Keep simulation coordinates in cells (24x24 in the classic model) and treat art scale as a rendering choice. This lets us change the look without touching game logic.
- **Recommendation:** Make Blender the default for vehicles, aircraft and buildings (about 3x cheaper by my estimate). Use hand pixel art for infantry, portraits and icons where hand detail matters. Renderers such as SpriteForge show the method, but we should write a small Blender script ourselves so we control camera and palette.
- **Recommendation:** Use palette-indexed art with a reserved house-colour range and a shader lookup. This keeps one art set for all colours and matches the classic design.
- **Trade-off:** 8 facings is cheap and looks acceptable at small sizes. 16 facings is smoother for vehicles at roughly double the frames. Choose per category.
- **Recommendation:** Adopt a licence rule before importing any art: CC0, MIT, BSD, GPL-2-or-later, OFL and CC BY-SA 4.0 only, each with a manifest entry. Exclude CC BY-SA 3.0, unlicensed packs and EA material.
- **Recommendation:** Use LibreSprite or Pixelorama. Do not install or redistribute Aseprite binaries.
- **Recommendation:** Keep AI out of the core sprite path for now. Consider it for concept art and portraits only, with disclosure, because frame consistency is poor and the copyright position is weak.
- **Trade-off:** Hard Vacuum is the most useful open RTS art I found, but its licence is not confirmed. Verify it before building anything around it.
- **Recommendation:** Run the pilot in Open questions before committing to a pipeline. It costs about two weeks and answers the style and time questions with data.
- **Risk:** Hand art cost dominates the budget. A Blender pipeline plus limited facings is the main lever to keep the art problem within a hobby schedule.

## Open questions for the owner

1. **Visual style.** Faithful low-resolution look (24-pixel cells, 256 colours) or modern HD (2x to 4x). The faithful look is much cheaper and fits the C&C feel. HD costs more art and needs more consistent scaling.
2. **AI policy.** Options: (a) no AI art at all; (b) concepts and portraits only, disclosed; (c) AI for cleanup and iteration, disclosed, with human-made final frames. Consider our future contributors and the copyright gap.
3. **Facings target.** Options: 8 for everything (cheapest); 16 for vehicles and 8 for infantry (recommended middle); 32 for vehicles (smoothest, most work).
4. **Licence stance for art.** Options: (a) strict permissive only (CC0, MIT, BSD, GPL, OFL); (b) also accept CC BY-SA 4.0 with attribution, which is GPLv3-compatible; (c) also accept CC BY-SA 3.0 with a separate art licence. Option (a) is safest.
5. **Blender commitment.** Are you willing to learn Blender for the pipeline? Blender is GPL and free, but the learning curve is real. The alternative is hand pixel art for everything at roughly 2.5x the hours.

## Sources

Read for this report:
- [Westwood SHP Format (TD)](https://moddingwiki.shikadi.net/wiki/Westwood_SHP_Format_(TD))
- [Westwood SHP Format (TS)](https://moddingwiki.shikadi.net/wiki/Westwood_SHP_Format_(TS))
- [Command & Conquer (moddingwiki)](https://moddingwiki.shikadi.net/wiki/Command_&_Conquer)
- [Command & Conquer Tileset Format](https://moddingwiki.shikadi.net/wiki/Command_%26_Conquer_Tileset_Format)
- [Command & Conquer Remastered Collection (Wikipedia)](https://en.wikipedia.org/wiki/Command_%26_Conquer_Remastered_Collection)
- [TechRadar: C&C Remastered 4K graphics](https://www.techradar.com/news/command-and-conquer-remastered-boasts-4k-graphics-and-a-pile-of-fan-requested-features)
- [Dune II (Wikipedia)](https://en.wikipedia.org/wiki/Dune_II)
- [GamingOnLinux: Hard Vacuum revived with OpenRA](https://www.gamingonlinux.com/2020/09/a-cancelled-old-rts-named-hard-vacuum-gets-revived-with-openra)
- [OpenHV on itch.io](https://openhv.itch.io/openhv)
- [Kenney, Top-Down Tanks Redux on itch.io](https://kenney-assets.itch.io/top-down-tanks-redux)
- [Debian copyright for warzone2100](https://metadata.ftp-master.debian.org/changelogs/main/w/warzone2100/stable_copyright)
- [Aseprite repository](https://github.com/aseprite/aseprite)
- [Aseprite EULA](https://github.com/aseprite/aseprite/blob/main/EULA.txt)
- [Pixel art editors overview (Itechguides)](https://www.itechguides.com/12-best-free-and-open-source-pixel-art-editors/)
- [Pixelorama repository](https://github.com/orama-interactive/pixelorama)
- [Sprytile fork (cschram)](https://github.com/cschram/Sprytile)
- [SpriteForge (Ztry8)](https://github.com/Ztry8/SpriteForge)
- [Clint Bellanger, Isometric tiles](https://clintbellanger.net/articles/isometric_tiles/)
- [LPC forum: rendering Blender meshes to sprite sheets](https://lpc.opengameart.org/node/18092)
- [Blender FAQ](https://www.blender.org/support/faq/)
- [0 A.D. (Wikipedia)](https://en.wikipedia.org/wiki/0_A.D._(video_game))
- [GamingOnLinux: OpenRA 2025 update](https://www.gamingonlinux.com/2025/03/openra-update-brings-a-revamped-map-editor-high-quality-dune-2000-assets-more-red-alert-missions/)
- [Remastered Tyrian Graphics (OpenGameArt)](https://opengameart.org/content/remastered-tyrian-graphics-0)
- [PPM forums: converting TD/RA units to TS](https://ppmforums.com/topic-29735/convert-units-from-ra1-or-td-into-tiberian-sun-help/)
- [SIL Open Font License FAQ](https://openfontlicense.org/ofl-faq/)
- [US Copyright Office, AI report Part 2 (newsnet)](https://www.copyright.gov/newsnet/2025/1060.html)
- [GamingOnLinux: Widelands draft AI ban](https://www.gamingonlinux.com/2026/02/widelands-the-open-source-settlers-like-devs-plan-to-ban-all-ai-generated-contributions/)
- [itch.io jam: clarification on AI-generated art](https://itch.io/jam/opengameartorg-spring-game-jam-2025/topic/4762321/a-clarification-on-the-use-of-ai-generated-art)
- [TechSpot: GDC 2026 survey on generative AI](https://www.techspot.com/news/111128-more-than-50-game-developers-generative-ai-harming.html)
- [RedMonk: generative AI policy landscape in open source](https://redmonk.com/kholterhoff/2026/02/26/generative-ai-policy-landscape-in-open-source/)
- [Cinevva: AI pixel art generators 2026](https://app.cinevva.com/guides/ai-pixel-art-generators)
- [Godot 2D sprite animation docs](https://docs.godotengine.org/en/stable/tutorials/2d/2d_sprite_animation.html) (truncated; little used)

Not verified (no fetchable source): 3D-rendered classic sprites, classic facing counts for TD and RA, RA2 cell size, OpenGameArt and Kenney site contents (blocked by robots.txt), Hard Vacuum licence text.
