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
5. Build state: the cut faces quantise to a flat blue-grey slab in frames 0-3, which reads as "poured foundation" but is
   bland; scaffold poles are 1 px lines. Frame 7 equals idle 0.
6. House colour: the roof plaque lands on the top ramp shades, so it looks pastel after remap (see the sheet);
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

## Honest notes
1. The soldier is about 17-19 px tall; house colour reads at 1x from all facings (back panel, shoulders, helmet stripe).
2. Facing reads mostly through the rifle and the lumpy backpack silhouette; the face (goggles, respirator) is only 1-3 px.
3. The walk cycle reads at 1x on the east/west facings; on N/S the leg swing is small and the figure mostly bobs.
4. Fire frame 2: flash is 3-5 px of yellow-white; the W facing gun runs close to the canvas edge.
5. Die: forward fall, readable as a body on the ground; the last frames are a green slab with the pack panel on top.
6. Shadow touches the right canvas edge in most frames (art itself stays inside); 11 frames touch the bottom edge.
7. Remap is exact (asserted); the shade spread inside the house paint is narrow, so rust/blue look flatter than the magenta.
8. Not tuned after the last render: stride and arm swing may still want a push, and the sheet has not had a second review pass.
