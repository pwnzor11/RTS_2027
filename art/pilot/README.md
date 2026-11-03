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
