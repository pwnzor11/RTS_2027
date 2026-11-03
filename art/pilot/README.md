# M0-12 art pilot: Authority medium tank, three arms, two cell sizes

Original design (boxy welded hull, wide tracks, low turret, long gun with muzzle brake, rear reactor housing
with ribbed collar, stencilled "07", searchlight). Ortho camera, elevation 47 deg, sun upper-left.
Canvas per frame = 1.5 x cell (72 px for the 48 cell, 96 px for the 64 cell). Frame 0 = north, clockwise, 22.5 deg steps.
Sheets are 4x4, row-major. House colour = 8-shade magenta ramp #400040..#FF80FF (turret side panels + skirt stripe).

## Regenerate (from the repo root)
```
python3 tools/art/build_tank.py            # Blender render, 16 facings x 2 sizes (288 and 384 px) -> art/pilot/work/ (git-ignored)
python3 tools/art/render_a.py              # Arm A -> A_render_48.png, A_render_64.png
python3 tools/art/pixelize.py              # Arm B -> B_pixel_48.png, B_pixel_64.png
python3 tools/art/hand_pixel.py            # Arm C -> C_hand_48.png (N, E, S, W only)
python3 tools/art/remap.py                 # remap_test.png
python3 tools/art/contact_sheet.py         # contact_sheet.png
```
Needs Python 3 with Pillow, numpy and `bpy` (Blender 5.2 module). No downloads, no external assets or fonts.
Delete `art/pilot/work/house_cal.txt` if you change the render (it caches the house-paint luminance range).

## Timings (2 CPU cores, Cycles CPU, 24 samples, no denoise)
build_tank.py about 160 s total (colour + house-mask + shadow passes, 16 frames at 288 and 384 px).
All post-processing scripts together: under 30 s.

## The arms
- **A render**: 4x render, area-filter downscale, soft shadow as partial alpha. House pixels are snapped to the exact
  ramp where at least half the covered area is house paint.
- **B pixel**: same 4x render, box downscale, alpha hard-thresholded at 50%, nearest colour from a fixed 32-colour
  palette (24 faction colours + the 8 exact house shades), 1 px near-black outline outside the silhouette,
  checker-dithered 1-bit shadow. No partial alpha anywhere.
- **C hand pixel**: rect/row lists per part, same palette, 48 px only, N/E/S/W only. Diagonals would be drawn by hand later.
  W is E re-drawn mirrored and re-shaded for the upper-left sun (not a pixel flip).
- **Remap** (`remap_test.png`): frame 2 of A_64 and B_64; exact-match magenta shades replaced by Authority green,
  Directorate rust-red #8B2E1A and blue ally #2E5A8B ramps. 131 pixels changed in each arm.

## Honest notes
1. B reads best at 1x: the outline and hard shadow separate units from the ash ground, and facings stay consistent.
   A is smoother but at 48 px its details (wheels, gun, number) smear into grey mush.
2. The stencilled number is ~4 px tall at 64 and about 3 at 48. It reads as a white blob in A/B at 1x; only C draws a legible "07" (and only because it is drawn larger than true scale).
3. House colour works: the remap is exact in both A and B with no magenta fringe left. House area is thin (stripe 1-2 px); a wider stripe or bigger turret panel would show allegiance better at 1x.
4. C looks clean and deliberate but flat and "toy-like" next to the rendered arms, and its shading style differs from A/B, so mixing hand and rendered units would show. Per-facing hand cost is high (diagonals still missing).
5. The reactor housing dominates the silhouette from the front and rear; the model is too boxy and low-detail for 64 px heroes, and the camera feels more top-down than classic 3/4 (47 deg with a short hull). Shadows are noisy-edged at 12 samples (blurred in post).

---

# v2 polish (M1-ART-01), arm B at 64 px cell only

Owner picked arm B at 64 px (D-039). v1 files are kept unchanged. The model in `build_tank.py` is now the v2 model,
so v1 cannot be re-rendered from the current script (use git history, commit 9700f6a, if ever needed).

What changed: camera elevation 47 to 40 deg; house colour on the full turret side panels and a 3-4 px skirt stripe
(0.3 world units tall); chamfered hull and turret edges; sloped glacis and rear; six road wheels per side with hubs;
two hatches and a commander cupola ring; thicker gun with a slotted muzzle brake (gun shortened slightly so the
muzzle is not cropped when facing east/west); stencil number dropped (it was an unreadable blob at 64 px).

## Regenerate v2 (from the repo root)
```
python3 tools/art/build_tank.py --sizes 384 --tag _v2      # about 75 s; writes art/pilot/work/raw_384_v2/
ART_TAG=_v2 python3 tools/art/pixelize.py v2               # B_pixel_64_v2.png and B_dither_64_v2.png
python3 tools/art/remap.py v2                              # remap_test_v2.png (asserts only exact ramp pixels change)
ART_TAG=_v2 python3 tools/art/contact_sheet_v2.py          # contact_sheet_v2.png (v1 vs v2 vs v2-dither)
```
`art/pilot/sprite_meta_example.toml` describes the sheet (cell 64, frame 96, 16 facings, north, clockwise,
house ramp, anchor pixel (48, 64) from the camera set-up).

## B-dither
Same palette, outline and hard alpha as B. Within each colour family (grey-green, steel, rust, highlight) the pixel is
dithered between its two neighbouring palette shades with a 4x4 Bayer matrix. House pixels are never dithered, so
the remap stays exact.

## v2 notes
1. House colour now reads at 1x from every facing except dead north/south, where only a sliver of the skirt stripe shows.
2. The 40 deg camera and wheel detail give a much more "tank-like" side view; the road wheels with rust hubs are the
   best new detail. The plain B shading is cleaner at 1x.
3. B-dither adds a visible dot texture on flat armour that reads as grit at 3x but is noisy at 1x on large flat
   faces. I would use it only for ground vehicles that should look worn, or not at all.
4. The reactor collar reads as a ribbed drum, but at north/south it still dominates the silhouette.
5. The shadow is a hard dithered blob; it is slightly cropped at the bottom edge on the south facing.

---

# Structures (M2-06): Authority Refinery pilot

Generic building pipeline plus the first building. Output: `data/sprites/buildings/auth_refinery.png` +
`auth_refinery.sprite.toml`; owner sheet `art/pilot/refinery_contact.png`.

## Regenerate (from the repo root)
```
pip install bpy --break-system-packages                 # Blender 5.2 module (got bpy 5.2.3 on Python 3.13)
python3 tools/art/build_structure.py auth_refinery      # render all 16 frames, then post-process, writes the sheet + manifest
python3 tools/art/build_structure.py auth_refinery --post-only      # post-process only (about 3 s)
python3 tools/art/build_structure.py auth_refinery --preview --frames 0 4 11   # fast colour-only look at 2x
python3 tools/art/structure_contact.py auth_refinery    # refinery_contact.png (asserts the remap touches only ramp pixels)
```
A new building = a new module in `tools/art/structures/<id>.py` (contract in the docstring of `auth_refinery.py`:
FOOTPRINT, CANVAS, ORIGIN, DOCK, STATES, MATERIALS, `build(S, state, i, n)`; primitives box/bb/cyl/cone/sphere;
`S.clip_z` for the build-up). Work files go to `art/pilot/work/struct_<id>/` (git-ignored).

## Framing
Same camera as the v2 tank: ortho, elevation 40 deg (not 47, which was the v1 tank), sun upper-left, 64 px per cell
horizontally. A 3x3 footprint is 192 px wide; canvas 192x224, footprint top-left at origin (0, 32), so 32 px of headroom
for the drum and stack. The ground is drawn so a cell is 64x64 px: modules author in ground units and the pipeline
stretches y by 1/sin(40) (radii are never stretched). Dock cell is relative [1,3], the cell just below the south edge
(not in the frame).
Sheet: 4 columns x 4 rows, row-major: idle 0-3, damaged 4-7, build 8-15. Manifest format as briefed
(frame_w, frame_h, origin_x, origin_y, [[state]] name/facings/frames/first/ticks_per_frame); columns = image width / frame_w.

## Timings (2 cores, Cycles CPU, 24 samples, no denoise, 768x896 render)
About 40-60 s per frame (colour + mask + shadow passes), 10-17 min for all 16 frames depending on load. Post-process 3 s.
Cost log (D-059): about 40 min wall-clock from start to this commit: roughly 12 min design/coding, 2 full renders
(about 10 and 17 min) plus several 2x previews, and about 10 min of review/fixes.

## Honest notes
1. At 1x the drum, stack, tube lamps and the big house plaques on the south face all read; the rotating drum plates
   show the idle animation clearly, tube lamps blink as 2-3 white pixels.
2. The roof dominates: the stretched ground plane makes the building look more top-down than the tank. Walls are
   only about 45 px of 224. Raising the walls further would help but costs headroom for the drum.
3. Stencils (white bars, plate numerals) are 1-2 px blobs, decoration only. The hazard stripes and yellow gantry read well.
4. Damaged: scorch and the missing panel read, flames are small yellow blobs, smoke is 2-3 dark puffs per column and only
   partly convincing; smoke near the stack touches the top edge. Damage is hand-placed in code, not a general shader.
5. (Pilot note, superseded by pass 2) Build state used a clipped model; frame 7 equals idle 0.
6. House colour: fixed in pass 2 (roof plaque uses the darker `house2` paint -> mid ramp shades). Pilot problem was:
   the south plaques are darker. Remap is exact (3771 pixels per colour).
7. The pad (full 192 px square) makes a pale-ish rectangle on the ash ground; the pad could be drawn by the terrain instead.
8. Art touches the left/right/bottom canvas edges (pad plus right-falling shadow); build frames 1 and 3 touch the top edge.

---

# Vehicles (M2-13a): Authority scraper and jeep

Generic vehicle pipeline plus two roster vehicles. Output in `data/sprites/vehicles/`: `auth_scraper`, `auth_jeep`
(`.png` + `.sprite.toml`) and `auth_tank` (same pixels as `tank_v2.png`, renamed copy so every roster id has a sprite named
after it). Owner sheet: `art/pilot/vehicles_contact.png`.

## Regenerate (from the repo root)
```
flock /home/claude/wt/blender.lock python3 tools/art/build_vehicle.py auth_jeep      # about 45 s, 16 frames
flock /home/claude/wt/blender.lock python3 tools/art/build_vehicle.py auth_scraper   # about 15 min, 80 frames
python3 tools/art/build_vehicle.py auth_scraper --post-only                          # post-process only
python3 tools/art/build_vehicle.py auth_scraper --preview --states harvest --facings 0 4   # fast half-res colour look
python3 tools/art/vehicle_contact.py                                                 # vehicles_contact.png (asserts remap touches only ramp pixels)
```
A new vehicle = a module `tools/art/vehicles/<id>.py` (contract in the docstring of `auth_jeep.py`: ID, TITLE, CANVAS,
TARGET, STATES, optional MATERIALS, `build(S, state, k, n)`). Same camera, palette, outline and shadow as the v2 tank
(64 px per cell; a bigger CANVAS only adds room). The tank itself still uses `build_tank.py` (output unchanged).
Sheet = 4 columns, frame of facing f, step k = `first + f * frames + k`. Work files: `art/pilot/work/veh_<id>/` (git-ignored).

| id | frame | states | sheet |
|---|---|---|---|
| auth_tank | 96 | idle 16x1 | 384x384 |
| auth_scraper | 112 | idle 16x1, harvest 16x4 (3 ticks/frame) | 448x2240 |
| auth_jeep | 88 | idle 16x1 | 352x352 |

## Cost log (D-059, 2 cores, Cycles CPU, 24 samples)
- auth_jeep: about 10 min design/preview, renders 45 s each (two renders, canvas 80 then 88; queued behind other renders it took longer).
- auth_scraper: about 20 min design/preview (conveyor fix once), full render 907 s; the `loaded` look was skipped.
- Contact sheet, remap check and README: about 10 min. Total about 45 min of attention plus 17 min of render wait.

## Notes
1. House colour is much bigger than the tank's stripe: scraper flanks, cab doors, hopper sides and blade plate; jeep bonnet and doors.
2. Scraper reads at 1x as a boxy hull with a dark ash-filled hopper; the conveyor is clearest from the front/rear and the harvest puff
   (grey blobs by the blade) and blinking beacon show in the 4-frame loop. From dead north the conveyor is hidden behind the hopper.
3. Jeep is small (about 1/2 cell) and reads mostly through the bright bonnet panel; the MG and antenna are 1-2 px details.
4. In 50 of the 80 scraper frames the dithered shadow touches the right canvas edge (art itself never does); at most a few shadow
   pixels are clipped. Widen CANVAS to 120 and re-render if that ever shows.
5. In Authority green the house panels almost vanish on the grey-green hulls (same as the refinery): a palette question for the owner, not a model fix.

# Infantry (M2-13b): Authority Rifleman pilot

Output: `data/sprites/infantry/auth_rifleman.png` + `.sprite.toml`; owner sheet `art/pilot/infantry_contact.png`.
Awaiting owner approval before the Rocketeer (and other infantry) fan out.

## Regenerate (from the repo root)
```
python3 tools/art/build_infantry.py auth_rifleman              # Blender render (under the shared flock) + post-process
python3 tools/art/build_infantry.py auth_rifleman --post-only  # post-process only, a few seconds
python3 tools/art/build_infantry.py auth_rifleman --frames 8 14 --samples 8   # quick partial render (writes a partial sheet!)
python3 tools/art/infantry/contact.py                          # infantry_contact.png
```
A new soldier = a new module in `tools/art/infantry/<id>.py` (contract in the docstring of `auth_rifleman.py`);
`rig.py` gives primitives, a limb helper and two-bone IK, so poses are foot/hand targets, not angles.

## Rocketeer (M2-13d): Authority Rocketeer
Output: `data/sprites/infantry/auth_rocketeer.png` + `.sprite.toml`; owner sheet `art/pilot/rocketeer_contact.png`
(`python3 tools/art/infantry/contact.py auth_rocketeer --out rocketeer_contact.png`; render with
`flock /home/claude/wt/blender.lock python3 tools/art/build_infantry.py auth_rocketeer`).
Module `tools/art/infantry/auth_rocketeer.py` is the approved Rifleman (same body, scale, value separation, house patches, canvas,
origin, state layout) with: a thick olive shoulder tube (flared rear, pale marking bands, dark front collar, rust-red warhead nose,
steel sighting frame) carried high on the right shoulder instead of the rifle; a rocket satchel low on the back with two rocket noses
sticking up beside the house panel; a heavier gait (shorter stride, more lean, bigger bob). Fire 1 = tube on shoulder aiming;
fire 2 = launch with a flame cone and flash ahead of the nose plus a backblast puff behind the tube. contact.py got a Rifleman-vs-
Rocketeer block (idle/fire-2/walk, 1x and 3x) and mixed 3 rocketeer + 2 rifleman cells when the id is not auth_rifleman.
Read at 1x: the rocketeer is told apart by the thick tube at shoulder height (the rifle is a thin diagonal wood/steel line at the
waist) and by spikes above the pack; the silhouettes differ on all 8 facings in the comparison block.
Honest notes: the launch flash/backblast is a large white-yellow blob at 1x (bigger than the rifle flash; may want toning down);
build_infantry warns that frames 75-77 (die) and some fire-2 frames touch the canvas edge; the in-context captions still say
"riflemen" (shared labels). Open: no separate rocket-in-flight effect (belongs with the FX pass, D-069).
Minutes spent: about 25 of work; about 12 of the wall-clock was waiting for the shared Blender lock (one render was lost to the
self-locking deadlock fixed by the lead in d5f3809).

## Format
Same camera, sun, scale and arm-B pipeline as the tank (40 deg, 64 px per cell). Frame canvas 32x32, origin (16, 26) = feet
on the sub-slot centre; shadow falls right/below. Sheet 192x416, 6 columns, 78 frames; frame = first + facing*frames + step.
States: idle (8 facings x1), walk (8x6, 3 ticks), fire (8x2: aim, flash), die (1 facing x6, forward fall drawn facing east).
Contact-sheet sub-slots in a 64 px cell: (32,36), (15,25), (49,25), (15,53), (49,53). House colour: both upper-arm bands,
helmet ridge stripe, back panel of the pack; calibrated so the median paint sits on ramp shade 4 (not pastel).

## Timings
About 0.5 s per frame (colour + mask + shadow at 128 px, 24 samples): full sheet around a minute plus 5 s post-process.
Most of the wall-clock was waiting for the shared Blender lock behind vehicle/structure renders. Minutes spent: not tracked
precisely, roughly 60-70 of work excluding lock waits.

## Second pass (M2-13c, D-068: "a little like a blob")
Changes: figure scaled 0.85 -> 1.05 (about 23 px tall; frame stays 32x32, origin now (16, 27)); value separation (light coat and
mantle, dark trousers, dark pack, dark helmet with a wide dark brim, pale face block, near-black boots); thinner arms held away
from the torso; rifle longer, held diagonally across the body (light wood, dark steel barrel) so it shows on N/S as well; longer
walk stride (feet visibly apart); bigger muzzle flash. House bands/helmet stripe/pack panel unchanged. contact.py remap row is now
amber #E0A020 / red #B02A1A / blue #2E5A8B, plus a BEFORE/AFTER strip (old sheet kept in git-ignored art/pilot/work/old_pilot_sheet.png).
Iterations: 2 renders + 1 tweak (about 25 s each). Minutes spent: roughly 20. The "Honest notes" below are from the first pass;
notes 1-3 are superseded (now about 23 px, face block visible from the front, N/S legs read apart).
Open: the helmet/coat contrast is good but the face is still only a few px; W/E rifle tips run near the canvas edge.

## Honest notes
1. The soldier is about 17-19 px tall; house colour reads at 1x from all facings (back panel, shoulders, helmet stripe).
2. Facing reads mostly through the rifle and the lumpy backpack silhouette; the face (goggles, respirator) is only 1-3 px.
3. The walk cycle reads at 1x on the east/west facings; on N/S the leg swing is small and the figure mostly bobs.
4. Fire frame 2: flash is 3-5 px of yellow-white; the W facing gun runs close to the canvas edge.
5. Die: forward fall, readable as a body on the ground; the last frames are a green slab with the pack panel on top.
6. Shadow touches the right canvas edge in most frames (art itself stays inside); 11 frames touch the bottom edge.
7. Remap is exact (asserted); the shade spread inside the house paint is narrow, so rust/blue look flatter than the magenta.
8. Not tuned after the last render: stride and arm swing may still want a push, and the sheet has not had a second review pass.

## Pass 2 (D-064, owner feedback: build "just rises out of the ground", damage "doesn't look like damage")
Idle is unchanged except that big flat house areas now use mid shades (see 6). Build and damaged were redone from scratch.
- Build (8 frames, no clipping any more; `build_site()` in the module): 0 surveyed plot (dug pit, spoil heaps, stakes, string lines,
  crates/pipes/sign/site cabin); 1 poured slab with shuttering, rebar grid and column stubs, mixer; 2 first steel storey, ring posts,
  scaffold, derrick crane arrives, welding sparks; 3 full steel frame of the hall, drum ring posts, two timber scaffolds, girder on the
  hook; 4 roof trusses, first primer panels, drum base plated; 5 walls mostly clad, roof half decked, drum half plated, stack base,
  roof panel on the hook; 6 the finished model in primer grey (no house paint, drum plates missing) with one scaffold left and
  leftovers on the pad; 7 = idle 0 (verified pixel-identical). New materials (timber, rebar, raw concrete, primer, soil, girder)
  differ from the finished building so each stage reads at 1x.
- Damaged (4 frames): collapsed SE roof corner (void, dangling slab, bent girders, rebar), blown-out wall section with black void and
  bent interior girders, cracked/missing panel pieces, rubble and a fallen girder, scorch radiating from the breaches (wall, roof,
  pad), stack broken and leaning west, soot-darkened building materials (house colours untouched), layered flames (core, orange, red)
  that flicker per frame, a dark plume that rises and grows over the 4 frames, embers.
- Hooks added to `build_structure.py`: `S.skin` (per-frame material swap, used for primer and soot) and `S.hide` (per-frame part
  removal by name prefix); the mask pass treats `house2` (darker house paint) like `house`. `S.clip_z` is still supported, unused here.
- Limits: the fixed 24-colour palette has no real red or orange, so fire is yellow plus a brown-orange (b07445) plus rust; a
  couple of fire ramp colours in `common.PAL` would help (not mine to change). The 4-frame loop makes the plume a chain of blobs
  at 1x. Damaged frames no longer touch the top edge; idle frames 1 and 3 still do (stack puff, as in the pilot).
- The before sheet used by the contact sheet is `art/pilot/refinery_contact_before_sheet.png` (the pilot sprite sheet).
- Cost log (D-059): about 40 min wall-clock, partly waiting on the shared render lock; 3 full renders (5-7 min each),
  2 partial renders and 3 preview passes of about 1 min.

### Structures batch B (M2-13s2): Factory, Wall, Turret
Regenerate inside the lock: `build_structure.py auth_factory` / `auth_wall`, `python3 tools/art/structures/turret_build.py`;
contact sheet `python3 tools/art/structures_contact_b.py` → `art/pilot/structures_b_contact.png`.
- Factory 3×3 (idle 4, damaged 4 structural + 7 fx anchors, build 8, active 4): ≈ 15 min render + 7 min partial re-render.
- Wall 1×1 (idle 1, damaged 1, build 4), tiles horizontally: ≈ 2 min. Known gap: no vertical/corner variants yet, so a
  north–south line reads as stacked horizontal blocks.
- Turret 1×1, rotating head (idle 16, fire 16×2, damaged 16, build 6): ≈ 20 min render; base identical across facings.
- Cost log (D-059): ≈ 90 min design + iteration for the three, most wall-clock spent queued on the shared render lock.

## Structures fan-out A (M2-13s1): Construction Yard, Power Plant, Barracks, Silo
Modules `tools/art/structures/auth_{conyard,power,barracks,silo}.py`, sprites `data/sprites/buildings/<id>.png` + `.sprite.toml`,
owner sheet `art/pilot/structures_a_contact.png` (`python3 tools/art/structures_contact_a.py`; remap amber/red/blue, exact-match asserted).
Same pipeline and camera as the refinery. `auth_conyard.py` also holds the shared build-site helpers (plot, foundation, steel skeleton,
scaffold, derrick, and `stages()`): build stages 0-1 are site/foundation, 2-3 steel skeleton + scaffold + derrick, 4-5 the finished model in
primer cut at a rising height with the skeleton above it (`S.clip_z`), 6 finished in primer with a scaffold tower left, 7 = idle frame 0.
Damage is structural only (D-069); each module has an `FX = [...]` list (pixel anchors in frame space). The manifest writer in this
branch predates FX support, so re-run `python3 tools/art/build_structure.py <id> --post-only` after the FX writer lands to get the `[[fx]]` tables.
- auth_conyard 3x3, canvas 192x248: hangar, open assembly bay, gantry (hook sways), prefab stacks, mast lamp blinks, house flag. Damaged: east roof fallen in, gantry slumped, mast sheared.
- auth_power 2x2, canvas 128x176: dome, amber tube windows pulse, stack puffs, transformers spark. Damaged: dome breach, stack snapped, annex roof caved, transformer toppled.
- auth_barracks 2x2, canvas 128x160, exit cell below the footprint middle: quonset, decon arch, sandbags, flag waves. Damaged: north half collapsed, bent ribs.
- auth_silo 2x2, canvas 128x152: two domed silos, feed pipe, gauge lamp blinks. Damaged: west silo blown open with spilled ash, east silo cracked.
Minutes spent (D-059, excluding ~15 min of waiting on the shared render lock per batch): conyard about 45, power about 25, barracks about 25, silo about 20, contact sheet about 10.
Known: the 4 damaged frames are near-identical (only the conyard cable swings); the barracks DOCK entry is the exit cell, which `buildings.toml` has no field for yet.

## Art polish (M2-20, D-072): roofs, wall variants, Rocketeer flash
Regenerate inside the lock: `build_structure.py auth_conyard|auth_power|auth_silo|auth_wall`, `build_infantry.py auth_rocketeer`;
contact sheet `python3 tools/art/polish_contact.py` → `art/pilot/polish_contact.png` (the "before" images are in `art/pilot/before_m2_20/`).
- Roofs: the roof panels were already `house2`, but the house ramp is calibrated from the brightest 2 % of all house pixels and the big
  flat roof panel *is* those pixels, so it always landed on the lightest shade (pastel). Fix in the pipeline: a module that sets
  `HOUSE2_MID = True` gets a red-only mask for `house2` (build_structure) and structpost calibrates the ramp from plain `house` pixels only
  and pins `house2` to ramp shades 2..4 (mid). Set on conyard, power and silo; other structures are unchanged until re-rendered. The vertical
  `house` panels now use the full ramp, so they read a little lighter than before. Frame counts and `[[fx]]` tables unchanged (checked).
- Wall: one sheet, every state facings = 1: `h v end_n end_e end_s end_w corner_ne corner_nw corner_se corner_sw t_n t_e t_s t_w cross post`
  each with `_damaged`, plus `idle`/`damaged` (= h, kept for the cameo and fallbacks) and the generic `build`. 256x1000 px, 38 frames.
  Naming: the letters are the directions the wall CONTINUES in (`end_n` = a neighbour to the north only, `corner_se` = south and east,
  `t_n` = north, east, west; `post` = none). Pieces that are not straight get a corner pillar; arms, caps, bands and trims run to the cell
  edge, teeth keep the 0.85 pitch (south of east-west arms, east of north-south runs). A north-south piece shows its south end face; the
  southern neighbour's sprite is drawn after it and covers it.
- Game: `WallTiling` (mask bits N1 E2 S4 W8 → state name) and `WallTiler` (per-snapshot cache of own-wall cells and masks) in
  `src/Game/Render`; `BuildingRenderer` swaps the idle/damaged frame of a `wall = true` building for the variant and leaves build frames
  alone; a manifest without the variant states keeps the plain frame. `SceneRenderer` creates the tiler and passes it in (2 small edits).
- Rocketeer: the launch flame is a small warm orange cone with a 1-2 px pale core, the backblast a short grey-brown puff (was a large
  white blob). Known: facing east the tube already reaches the 32 px canvas edge, so most of the flame is cropped there.
- Minutes (D-059, excluding queueing on the shared Blender lock): about 60 of work; renders: conyard 7, power 4, silo 3, wall 8, Rocketeer 5 + 2 test renders.
