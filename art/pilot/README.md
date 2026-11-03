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

## Vehicles (M2-13a)
Regenerate (one render at a time on this box): `flock /home/claude/wt/blender.lock python3 tools/art/build_vehicle.py auth_scraper`
(and `auth_jeep`), then `python3 tools/art/vehicle_contact.py` → `art/pilot/vehicles_contact.png`. Models live in
`tools/art/vehicles/<id>.py`; the builder reuses the tank camera (40°), palette, outline and shadow (`structpost.frame_b`).
`auth_tank` is a byte copy of the approved `tank_v2`.

- Frames: tank 96 px, scraper 112 px (idle 16×1, harvest 16×4 at 3 ticks/frame), jeep 88 px (idle 16×1). Origin = cell centre.
- Timings (2 cores, Cycles 24 samples): jeep ≈ 15 min full sheet, scraper ≈ 15 min (80 frames ≈ 12 s each). Post ≈ 3 s.
- Cost log (D-059): ≈ 40 min design + previews for both vehicles, plus ≈ 30 min renders (queued behind other renders).
- Honest notes: the scraper reads as a harvester at 1× (blade, conveyor, ash load, beacon); the jeep is small but distinct.
  House areas are larger than the tank's, but in Authority green they almost vanish on the grey-green hulls (same as the
  refinery) — a palette question for the owner, not a model fix. Optional "loaded" look skipped.
