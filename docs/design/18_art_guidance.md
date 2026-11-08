# Design 18 · Art guidance from OpenSAGE
Package s15-art (session 15). **Analysis only: no art, data or code changed.** The owner asked: "clone OpenSAGE and use it as inspiration and
guidance on the artwork we have for the game so far." This document compares how that project presents art with what RTS_2027 shows today and
turns the difference into a plan for M10, with a pilot step for every item (owner's rule: pilot one sample, owner approves, then fan out).

**Legal frame (D-050).** OpenSAGE is an open-source reimplementation of an EA engine and contains code ported from EA's released source
(its `LICENSE-EA.md`). It is read-only reference here. Everything below is described in our own words; no code, shader text, data tables or
comments were copied, and no EA names appear in anything we would ship. OpenSAGE ships **no art**: it draws the player's own game files. What it
can teach us is therefore *how art is presented* (states, layering, lighting, effects), not what the art looks like. It also ships **no data
files**, so numbers such as damage thresholds, shadow strength or decal lifetimes are not in it (they live in game data it reads at run time);
where this document gives a number for our game it is marked ASSUMED and is a proposal for the owner.

## 0 · Summary for the owner
- **Our sprites are already in good shape on the hard parts**: one camera (orthographic, 40 degrees), one sun (upper left) on every unit and
  building, a fixed palette, an exact-match house colour ramp, fire/smoke/explosions as separate effects with anchors (D-069), build-up frames on
  all 62 buildings and a damaged frame on all 62. That is the same family of ideas the reference uses (condition states, team-colour meshes,
  FX attached to named anchors).
- **What the reference does that we do not**: (1) buildings and units end in a *rubble* state and leave marks (rubble, scorch, wreck), where ours
  simply vanish after an explosion; (2) it has *two* damage tiers before rubble, plus one-shot effect bursts when a tier is entered; ours has one
  tier for buildings and **none for vehicles and infantry** (only a health bar); (3) moving ground units kick up dust and leave tracks, ours slide
  with no dust, no wheel or tread motion; (4) effects are ranked by priority and cut when busy; (5) terrain gets mood from time-of-day lighting
  and moving cloud shadows; (6) roads are built from curve, junction and end-cap pieces, water is animated.
- **One weakness the reference cannot show us but our own audit did**: house colour on **infantry is almost invisible at 1x** (median 6 pixels of
  ramp colour per facing across the 18 infantry sheets; Marine has facings with none, Commissar 1, Pathfinder/Pioneer/Diver 2 to 9). Allegiance is the
  first thing an RTS player reads.
- **Cheap wins** need no new Blender renders: vehicle damage smoke and fire from existing FX sprites, a burst when a building enters a damaged tier,
  a heavier second FX tier for buildings, dust puffs behind moving vehicles, and an infantry scale test in the renderer.
- **Six owner decisions** are in section 4. My recommendations: keep the current sun and hard dithered shadow (and write it down as a rule);
  two damage tiers done with effects only; fading rubble/scorch/wreck remains; widen house colour on infantry; keep the scale ladder but test
  infantry at 1.25x; a per-theater mood tint, cloud shadows later.

## 1 · What was looked at
**Ours** (contact sheets, helper scratch, not committed; they are regenerated from the repo by a 60-line script over the manifests):
`/tmp/claude-0/s15-art/sheets/` : `auth_units.png`, `dir_units.png`, `lb_units.png`, `exp_units.png` (idle frame, one facing, hull plus turret);
`<faction>_buildings_N.png` for `auth` (2), `dir` (3), `lb` (3), `exp` (4): idle | damaged | first build frame | last build frame per building;
`fx.png` (all six effect sprites, every frame); `scale_lineup.png` (infantry, vehicles, buildings at true size on a 64 px cell grid).
Also read: `art/pilot/README.md`, `tools/art/build_structure.py`, `build_vehicle.py`, `common.py`, `fx/build_fx.py`, manifests under `data/sprites/**`,
`src/Game/Render/{SpriteStates,UnitRenderer,BuildingRenderer,HealthBars,ExplosionPlan,WreckStormOverlay,TerrainBake,DecorBake,PropArt,DecalShape}.cs`,
`data/theaters/ash_plains.toml` (shipped look C, D-181), and the act-break stills in `data/images/stills`.

**Reference** (`/home/claude/ref/OpenSAGE`, `src/OpenSage.Game`): condition-state handling (`Logic/Object/ModelConditionFlag.cs`, `ModelConditionState.cs`,
`Draw/W3dModelDraw.cs`), the damage state machine (`Body/ActiveBody.cs`, `Damage/TransitionDamageFX.cs`), death handling (`Die/*`), tank and truck
draw modules, debris draw, particle systems (`Graphics/ParticleSystems/*`, `FX/FXNuggets/*`), drawable shadows and decals (`Client/Drawable.cs`),
global lighting and shadow settings (`GameData.cs`, `Graphics/Rendering/Shadows`, `Assets/Shaders/Lighting.h`), terrain (`Terrain/*`,
`Assets/Shaders/Terrain.frag`), roads, water, radar (`Radar.cs`, `RadarDrawUtil.cs`).

**Our current art, measured**
- 152 sheets: 18 infantry, about 58 vehicle/air/naval bodies (plus turret and shadow layers), 62 buildings (plus FX sprites).
- Camera orthographic at 40 degrees; sun upper left (same vector for tank, vehicles and structures); 64 px per cell; 16 facings for vehicles, 8
  for infantry; hard alpha (0 or 255); 1 px outline; dithered 1-bit ground shadow in a fixed shadow colour; air units have a separate shadow sheet.
- Palette fixed per faction (24 or 28 colours) plus the 8-shade magenta house ramp, replaced by exact match at draw time.
- Building states: `idle` (4 frames, slowed 4x), `damaged` (shown when health is under half, one tier), `build` (8 frames played over 70 ticks, last
  frame holds), walls have per-piece damaged variants. FX anchors per building: 43 large fires, 86 small fires, 98 smoke plumes across the set.
- Unit states: infantry `idle/walk/fire/die(6 frames)`; vehicles are **one idle frame per facing** (no wheel, tread or rotor animation);
  turrets are separate layers. Death of a vehicle or building is an explosion only (up to 4 large blasts for a building, `ExplosionPlan`).
- FX sprites (hard-pixel, own 5-step fire ramp): explosion large 12 frames, small 10, fire large and small 8, smoke plume 12, sparks 6.
  No muzzle flash, dust, debris chunk or scorch sprite.
- Wrecks exist only as a Left Behind rule (fading placeholder rectangles, `WreckStormOverlay`).
- Terrain (look C): 16 texels per cell, three-shade blotch, 50 % edge blend toward the neighbour's colour where land types meet, road edge lines,
  shore band, cliff lip highlight plus drop shadow, resource heaps/crystals with rim and contact shadow, baked decals (ash drift, cracks, crater with
  lit north-west rim, burnt ground, bones) and props (boulder, rubble) lit from the north-west with a south-east shadow. Selection is white corner
  brackets, health bars are green/yellow/red above the sprite frame.

## 2 · What the reference does, in our words (by area)
1. **Condition states.** Every object has a set of flags (damaged, really damaged, rubble, moving, attacking, garrisoned, under construction,
   night, snow, and a few hundred more). Each visual variant lists the flags it needs; at run time the variant that matches the most active
   flags is chosen. Effects (particle systems, muzzle flash bones, hidden or shown sub-parts) hang off the chosen variant, so switching variant
   switches the effects too. The idea we can borrow is *a short, ordered set of named looks per object, chosen by the renderer from a few flags*.
2. **Damage tiers.** Health maps to four body states: pristine, damaged, really damaged, rubble, using two thresholds read from game data. On each
   change of state a set of one-shot effects fires from the object (a burst of smoke/fire for "damaged", a bigger one for "really damaged", a
   collapse for "rubble"), and a separate looping set runs while in the state. A structure that reaches rubble stays on the map as a low heap
   (its collision height shrinks and path-finding treats it differently); some stay on the radar.
3. **Construction.** Flags walk through awaiting, partially built, being built, complete. While building, the model is lowered into the ground and
   rises in proportion to progress; the terrain under the site is flattened when construction starts. (Our scaffold stages are richer; the
   idea worth noting is the flattened pad under the footprint.)
4. **Team colour.** Parts that carry house colour are marked in the model and tinted by the owner's colour in the shader; everything else is
   untouched. Selection can also flash the house colour. This is the same idea as our exact-match magenta ramp.
5. **Lighting and shadows.** Terrain and objects have separate light sets (ambient plus up to three directional lights), and the game data holds
   one set each for morning, afternoon, evening and night, so a mission can be set at a time of day, with an option that makes models follow it.
   Cloud shadows slide over the terrain (a cloud texture projected through the light). Object shadows come in kinds: none, a real-time shadow
   volume/map (soft, one cascade, a limited distance), or a cheap blob decal under the object; the kind is chosen per object type (infantry and
   small things get blob decals, big things real shadows).
6. **Terrain.** Each cell names a ground texture and may blend toward its neighbours in four straight and four diagonal directions (one-sided or
   two-sided); cliffs have their own texture mapping so they do not stretch; a large low-contrast "macro" noise texture is laid over everything to
   hide tiling; water shows an animated caustic texture, has a depth-based transparency and an optional reflection plane. Roads are separate
   strips built from straight, curved, crossing and end-cap pieces cut from one texture atlas and laid over the terrain; bridges are their own object.
7. **Decals and scorch.** Effects can stamp a decal on the ground (scorch marks of a few preset kinds, dynamic decals with an opacity fade in two
   stages and a lifetime). Selected or special units get a projected ring or marker decal on the ground, which is also how range circles are drawn.
   A setting caps how many tank track marks exist at once.
8. **Particle systems.** A template defines: how long particles live and how long the system lives; start size and growth; up to eight colour and
   alpha keyframes; gravity, drag, spin; emission volume (point, line, sphere, box, cylinder) and velocity type; bursts versus continuous emission;
   whether particles are aligned to the ground; wind sway; a "slave" system that follows (a smoke trail on a debris chunk); blend mode (additive,
   alpha, alpha test, multiply) and a type (sprite particle, streak, volume, a model, and a heat-haze "smudge"). Each system has a **priority class**
   (weapon explosion, scorch, dust trail, build-up, debris trail, unit damage, death explosion, constant, area effect, always render) and a
   **quality floor**, so on a busy screen low-priority effects are dropped first.
9. **Vehicle motion art.** Tracked vehicles scroll the tread texture in proportion to speed and kick up dirt puffs from the left and right track;
   wheeled vehicles spin wheels by speed, sway the body, and add dust (and a powerslide spray). Turret and barrel are separate bones.
10. **Death.** Dying objects can run an effect list, spawn a replacement object (wreck, debris, pilot), or keep the husk for a collapse time.
    Debris chunks are small generic objects thrown with physics.
11. **Radar and markers.** Each object has a radar priority (structure, unit, hidden, local-unit-only) and shows as a dot in its owner's colour;
    events (attack, building lost) pulse for about six seconds and fade over the last 0.75 s, with a colour pair per event type.

Not found in the code: real tank-track decals (the setting exists, the drawing does not), muzzle-flash numbers, shadow darkness, scorch lifetimes.

## 3 · Per-area comparison
Size: S = a helper package or less, no new Blender renders; M = two to four packages or a small render batch; L = re-render of many sheets.
"Pick" = needs an owner decision from section 4.

| Area | Reference convention | Ours now | Gap | Recommendation | Size | Pick |
|---|---|---|---|---|---|---|
| Damage tiers, buildings | Pristine, damaged, really damaged; thresholds in data; one-shot burst on entering each tier; looping effects while in it | One `damaged` tier under 50 % with baked structural damage plus looping fire/smoke at manifest anchors (D-069) | No second tier; no entry burst | Tier 2 under 25 % done **with effects only**: more fire anchors, darker smoke, brief flicker; one-shot burst on entering tier 1 and tier 2 | S | D2 |
| Damage, vehicles and infantry | Same tiers apply to units; units smoke when damaged, burn when badly damaged | None; health bar only (shown when damaged or selected) | A hurt tank looks identical to a fresh one | Smoke trail under 50 %, fire under 25 %, from existing FX sprites, anchored on the hull origin with a per-class offset; infantry none | S | D2 |
| Rubble / remains | Rubble is a body state: structure collapses to a low heap that stays (collision shrinks) | Building vanishes after up to 4 explosions; vehicles vanish; LB wrecks are placeholder rects | Battles leave nothing; no sense of history | Fading rubble remains (view only, not sim) per footprint size class, vehicle wreck plus scorch; reuse `RenderMemory` | M | D3 |
| Scorch / ground marks | Scorch decals of preset kinds stamped by explosions; dynamic decals with fade stages and lifetime | Map decals are baked once per map and static; nothing is stamped in play | No lasting mark from fights | Ring buffer of stamped scorch decals (cap, e.g. 64, ASSUMED) drawn under units; stamped on death and big explosions; fade over 30 s ASSUMED | M | D3 |
| Construction | Flags per stage; model rises from ground by progress; flattened pad under site | 8-frame scaffold build, fixed 70 ticks, approved (D-069); square ground plate is part of every building sprite | Plate edges show against ash ground; build time is fixed not progress-based | Keep the build frames. Add a soft dirt/scorch skirt under footprints to hide the plate edge (view layer) | S | no |
| Team colour | House-colour parts marked and tinted in shader | 8-shade magenta ramp, exact-match remap; buildings median 2.8 % of pixels, vehicles 1.2 %, infantry 2.0 % (6 px median) | Infantry and some small units barely show allegiance | Widen on infantry (shoulder, hat, or armband, at least 10 px every facing); audit script in `tools/art`; pilot on Marine, Commissar | M | D4 |
| Sun / light direction | Light set per time of day; terrain and objects lit separately | One sun, upper left, identical on all sprites; terrain, props, crater rims, cliff drop shadow, resource heaps also NW-lit | Consistent already. Not written down as a rule; stills use a different (cinematic) light | Write the rule (sun from upper left, shadows to lower right, 40 degrees); audit new sprites against it | S | D1 |
| Mood / time of day | Morning, afternoon, evening, night sets; cloud shadows slide over terrain | None; the look is one flat daylight | Every map reads the same hour | Per-theater or per-mission mood: a multiply tint over the whole scene (dusk, night, ash haze) plus lamp glows; later a slow cloud-shadow multiply layer | M | D6 |
| Shadows | Per object kind: none, real-time, or blob decal; soft, short distance | Baked hard 1-bit dithered shadow at 50 % alpha under the body; separate shadow sheet for air units | Fine. Infantry shadows are tiny; building shadows are clipped to their frame | Keep. Pilot a slightly longer infantry shadow only if owner raises it | S | D1 |
| Muzzle flash / fire art | Flash shown only while firing, on a named barrel bone | Infantry/turret `fire` states exist; no flash sprite; no barrel anchor data | Weapons have no visible report | One 3-frame muzzle flash FX and a per-unit-class barrel offset table by facing; pilot on the Authority tank | M | no |
| Vehicle motion | Tread scroll by speed; dirt puffs per track; wheels spin; dust | One frame per facing; no dust | Vehicles slide; ground contact missing | Dust puff FX behind moving ground vehicles on dirt/ash/clear (view only). Tread or wheel shimmer needs re-renders: only if the owner asks | S (dust), L (anim) | no |
| Tread marks | Cap on number of track marks; per-track dirt debris | None | Ground never shows passage | Short-lived 2 px track decals into the scorch ring buffer, 8 s fade ASSUMED, tanks and heavy vehicles only | M | no |
| Debris / death | Thrown debris chunks with gravity; trail effect on chunks | Single explosion sprites | Deaths are flat | 6 to 8 tiny debris chunk sprites (3 to 6 px, faction palette) thrown with gravity, a 2-frame ground impact | M | no |
| Effect priority / budget | Priority classes plus quality floor drop low-priority effects when busy | Effects always drawn | Large fights may clutter or cost | Add a priority class to FX spawns and a cap (e.g. 150 live, ASSUMED); drop smoke before explosions | S | no |
| Blend mode | Additive, alpha, alpha-test, multiply per system | Hard alpha only (own palette) | Fire has no glow | Keep hard pixels (D-069 look). Optional additive 1-frame glow pass under big blasts at night only | S | D6 |
| Terrain blending | Per-cell texture with 8-direction blend, cliff own mapping, macro noise | Blotch hash, 50 % edge blend, cliff lip plus drop shadow (D-181) | Roads and water edges are flat; no large-scale tiling break beyond tone drift | Keep. Add junction/curve/end-cap road pieces (below); no other change | S | no |
| Roads | Strips of straight, curve, crossing, end-cap pieces over terrain | Road is a land type with edge lines | Junctions and dead ends read as blobs | Bake end caps and 4-way/T junction texels in `TerrainBake` | M | no |
| Water | Animated caustics, depth transparency, reflection | Colour bands, shore band, ripple decals (static) | Water is dead | Two-frame shimmer overlay (a scrolling dither pattern, 1 px) over deep water | S | no |
| Selection / markers | Optional ground marker/ring decal; selection flash in house colour; radius decals for ranges | White corner brackets; range overlay; promotion flash | Fine; brackets are the classic look | Keep. Optional ground ellipse (squashed by 1/sin 40 = 1.56) only for owner taste | S | no |
| Health bars | Reference draws bars over objects | Green/yellow/red bar above sprite frame, buildings only when damaged, rank pips | Fine | Keep | - | no |
| Radar / minimap | Priority classes, owner-colour dots, event pings 6 s with a 0.75 s fade | Minimap with resource colours (D-181) and alerts | Parity close | Check event pings last at least 6 s with a fade; no art | S | no |
| Unit scale | Not in code (art-driven). Classic convention: infantry well under one cell, vehicles about one cell, buildings 2 to 4 cells | Infantry about 23 px tall (0.35 cell), jeep 41 px, tank 71 px wide, buildings 128 px for 2x2 | Ladder reads, but infantry are small at 1x | Test infantry at 1.25x in the renderer before touching art | S | D5 |
| Garrison / no-power | Garrisoned and power-down are condition states with their own look | Drawn by code overlays (occupant pips, no-power overlay) | Works, not art | Keep overlays; optional lights-out building tint in the no-power overlay | S | no |

## 4 · Owner decisions (six that matter)
**D1 · Sun direction and shadow style.** The art already agrees on one sun. Options: (a) keep upper-left, hard dithered shadows, write it as a
rule; (b) soft shadows as partial alpha (arm A of the pilot, rejected at the time, D-039); (c) move the sun. **Recommend (a).** Cost of (c) is a full re-render.

**D2 · How many damage tiers.** Options: (a) keep one tier for buildings, nothing for units; (b) two tiers (50 % and 25 %) for buildings and a smoke/fire
look for vehicles, **done with effects only**; (c) bake a third set of frames per building (about 62 sheets, L). **Recommend (b)**: nearly the
same readability for a fraction of the cost, and no sheets change.

**D3 · Remains of the dead.** Options: (a) nothing (today); (b) rubble for buildings and wreck plus scorch for vehicles, fading, view-only; (c)
permanent rubble that blocks movement (a sim change, D-rule needed). **Recommend (b)**: 3 rubble size classes (about 2x2, 3x3, 4x4 and larger),
shared across factions with a faction-tinted palette, building rubble 30 s, vehicle wreck 15 s, scorch 30 s (all ASSUMED). It never touches the sim,
so determinism is safe.

**D4 · Team colour approach.** Options: (a) keep the magenta ramp as it is; (b) keep the ramp but guarantee a minimum area (at least 10 px per facing
on infantry, 2 % on small vehicles) and re-render the infantry that fall short; (c) add a renderer-drawn house-colour ground pip or underline under
every unit (no art change, but a different look). **Recommend (b)**, with (c) kept as a fallback if the owner prefers not to re-render.

**D5 · Unit scale.** Options: (a) keep; (b) enlarge infantry by about 25 % in the renderer; (c) re-render infantry larger. **Recommend running (b) first**
as a test (one line in the renderer behind a setting) and only then deciding on (c).

**D6 · Mood and glow.** Options: (a) one daylight for all maps; (b) a per-theater tint (Ash Plains haze, Mountains cold, Glass Coast bright, Rust Belt
dusk) and a night mission variant with lamp glows; (c) (b) plus slow cloud shadows. **Recommend (b) now, (c) after seeing it.** Additive glow only appears in night variants.

## 5 · Art plan for M10
Order is by value for effort. Each item ends with the pilot step. Nothing here is started; all of it waits for the owner's pick where marked.

**Quick wins before M10 (no new Blender renders, one helper package each, renderer plus data only)**
1. **Vehicle and building damage effects (D2).** Add damage tier 2 (under 25 %) to building FX tables by re-using the existing `[[fx]]` mechanism with
   more anchors and heavier smoke; attach smoke (under 50 %) and fire (under 25 %) to vehicles at a per-class hull offset; one-shot burst
   on entering a tier. *Pilot:* Authority barracks and Authority tank only; owner looks at a staged fight; then all factions by data.
2. **Dust puffs (no pick).** A 4-frame dust puff sprite (`fx/dust_puff`, hard pixels, theater-tinted) emitted behind moving ground vehicles on clear,
   ash and rough ground. *Pilot:* tank and jeep on Ash Plains; owner confirms the amount is subtle.
3. **Infantry scale test (D5).** Setting to draw infantry at 1.25x. *Pilot:* owner plays one skirmish with it on.
4. **House-colour audit tool (D4).** Move the measurement used above into `tools/art/audit_house.py`, listing sprites under a minimum, run it in the
   art check. *Pilot:* run on all 152 sheets and share the list.

**M10 proper**
5. **Remains: rubble, wreck, scorch (D3), M.** Renderer memory of dead buildings and vehicles (cell, footprint size class, time), three rubble sprites plus a
   vehicle wreck sprite and two scorch stamps per faction palette, fade by age. *Pilot:* one 2x2 rubble (Authority palette) and the scorch stamp for a
   tank death; owner approves the look and the fade time before the other classes and factions are made.
6. **House-colour widening on infantry (D4), M.** *Pilot:* Marine and Commissar re-rendered with a larger panel; owner approves; fan out to the other 16.
7. **Muzzle flash and debris chunks, M.** One 3-frame flash and a per-class barrel offset table (by facing) for tanks; debris chunk sprites for deaths.
   *Pilot:* Authority tank firing and dying; then every vehicle by table.
8. **Foundation pad / dirt skirt under buildings, S.** *Pilot:* two buildings with different plate colours (Authority barracks, Expedition core hall)
   on ash and on clear ground.
9. **Mood tint and night variant (D6), M.** *Pilot:* one tint on one theater for one mission, owner checks readability of house colours under it.
10. **Roads: end caps and junctions, M.** *Pilot:* one theater, one junction and one dead end in `TerrainBake`.
11. **Water shimmer, S.** *Pilot:* one map with a lake.
12. **Tread marks, M.** Only if the owner still wants them after item 5; reuses the scorch ring buffer. *Pilot:* tank on clear ground.
13. **Effect priority and cap, S.** No pilot needed; a unit test on the cap.

Not recommended now: wheel and tread animation frames (L; a full vehicle re-render), a baked third damage tier (L), real-time shadows, any change
to the sun.

## 6 · References and credit
OpenSAGE (https://github.com/OpenSAGE/OpenSAGE) is consulted as a read-only reference under D-050; no file from it is in this repository. The
repository's own sources named in section 1 are the only source for the "ours now" columns.
