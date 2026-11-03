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
