# tools/art/wz: Warzone 2100 models as sprite source (pilot)

Warzone 2100 source and data are GPL-2.0-or-later (its COPYING.README), compatible with our GPL-3.0-or-later; the name "Warzone 2100" may be
credited. Exceptions (COPYING.NONGPL of the data and of the texpages repo): `page-17-droid-weapons.png` and `page-6/30-features-rockies.png`
are CC BY SA 3.0 (not compatible with GPL-3.0), the wheel propulsion `.pie` files are CC BY SA 3.0 too. Do not use those. Check every texture page
against that list before use. Both Warzone clones are read-only reference (`/home/claude/ref/wz2100`, `/home/claude/ref/wz2100-texpages`; override
with `WZ_COMPONENTS`, `WZ_STRUCTS`, `WZ_TEXPAGES`); nothing from them is copied into the repo, only the sprites rendered from them.

Files
- `pie_import.py`: own reader for the text `.pie` format (PIE 2/3/4) and a bpy builder. Axes: `blender = (x, -z, y) * scale`; connectors are
  stored (x, depth, height). Triangles can be split by index (`only=` / `skip=`), which is how house-colour panels are assigned.
  `image_material()` gives a nearest-neighbour textured material plus a matching mask-pass material (tcmask red channel, thresholded).
- `regrade.py`: re-grades a texture page (luminance through a 3-stop ramp: `hull` olive, `track`, `concrete`) so every Warzone page lands in our
  palette family. Output goes to `art/pilot/work/wz/` (git-ignored).
- `contact.py <dir>`: old-vs-candidate contact sheets (tank 8 facings x 2 house colours x 1x/2x, factory).

Candidates (modules, rendered by the normal pipelines, written beside the originals under their own ids)
- `python3 tools/art/build_vehicle.py auth_tank_wz`  (module `tools/art/vehicles/auth_tank_wz.py`; hull + turret layers)
- `python3 tools/art/build_structure.py auth_factory_wz`  (module `tools/art/structures/auth_factory_wz.py`; idle frame only)
Wrap renders in `flock /home/claude/wt/blender.lock`. The pipeline files gained one helper each: `S.mesh()` (adds a ready-made mesh plus an optional
per-object mask material); nothing else in them changed.

Findings: the Warzone body/structure pages carry very little team-colour mask (page-14: ~0.8 % of texels, none on the Cobra body), so house
paint on vehicles is assigned per triangle in the module. The weapon page is not usable (licence), so weapons are geometry only.
