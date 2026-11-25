Campaign ending slide pictures (M16-C-01). One PNG per slide, named `<image id>.png`, 1280x720: `ending_<faction>_NN`.
The ids come from `image = "..."` in `data/campaigns/<faction>_ending.toml` (authority 4, directorate 4, expedition 5, left_behind 5 = 18).
Made in Blender from the scripts in tools/art/endings/ (one per slide, shared harness endkit.py) on the act-still kits in tools/art/stills/
(style C "mix", D-169); stored as plain PNGs, not LFS. A missing file shows the slide on a dark panel. No text in any picture.
Render: `flock -o /home/claude/wt/blender.lock python3 tools/art/endings/ending_<id>.py` (STILL_PREVIEW=1 gives a 640x360 look in scratch/endings_prev/).
