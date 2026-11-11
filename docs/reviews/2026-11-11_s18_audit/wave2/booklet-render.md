COMMITS (branch wp/s18-booklet-render, worktree /home/claude/wt/s18-booklet-render, not pushed, no PNG committed):
15ba2f1 feat(tools): booklet hero render mode and laptop script [S18-BOOKLET]
Files, all new, nothing existing edited: tools/booklet/render_heroes.py, render_heroes.ps1, render_manifest.toml, make_manifest.py (generator for the manifest; no bpy needed; re-run it when new model scripts land).

HOW IT WORKS (no hook needed in the sprite scripts):
- render_heroes.py calls the existing build_vehicle.render / build_infantry.render / build_structure.render unchanged.
- auth_tank is built by build_tank.py (alias tank_v2). That script runs main() on import and has no __main__ guard, so I load its source with the trailing main() dropped and call its build() and setup_scene().
- It swaps bpy.ops for a thin proxy. At the first colour-pass render it recolours the house/house2 materials to the faction accent, re-frames the same 40-degree ortho camera tightly (same sun), sets 1600 px on the long side, and renders with Cycles plus OIDN denoise to art/booklet/<faction>/<id>.png. It then aborts the builder so the mask and shadow passes never run.
- No pixelisation, no palette, no outline. The background is transparent and no ground shadow is drawn.
- View: facing 225 degrees clockwise from north (sheet facing 10 of 16, 5 of 8), "idle" state, frame 0. Vehicles with turret layers use the existing "baked" layer (hull and turret in one pass). Buildings use idle frame 0.
- Threads are set to all cores (the sprite scripts pin 2).
- Skips ids already rendered unless --force. One failing id does not stop the run (exit code 1 plus a FAILED list). Prints per-id and total elapsed time, and writes the contact sheet art/booklet/contact_<date>.png.

MANIFEST: 150 ids, 124 ok, 26 no_model. These match GAMEREND-06 exactly.
no_model ids:
- Vehicles (10): auth_carrier, auth_seismic_mortar, auth_foam_truck, auth_interceptor, auth_bomber, auth_howitzer, auth_bulwark, dir_ash_sower, dir_delver, dir_great_dirigible.
- Infantry (4): auth_engineer, auth_marksman, dir_assessor, dir_volunteer.
- Buildings (12): auth_ration_hall, auth_signals_hq, auth_blockhouse, auth_flak, auth_airfield, auth_atomic_battery, dir_bunk_gallery, dir_redoubt, dir_committee_hall, neu_ruin_tenement, neu_ruin_depot, lb_still_rig.
Mapping: vehicles use tools/art/vehicles/<id>.py via build_vehicle.py, except auth_tank (build_tank.py). Infantry use tools/art/infantry/<id>.py via build_infantry.py. Buildings use tools/art/structures/<id>.py via build_structure.py. Faction comes from the rules field, else the id prefix. The only neutral ids are the two ruins, and both are no_model.

ACCENTS (recorded in the manifest under [accent.*]):
- Authority: amber #E0A020, PlayerColours slot 0, as in the pilot.
- Directorate: blue #2F76C0, slot 2. Its body is warm grey and oxide red.
- Left Behind: purple #9360C8, slot 4. Its body is ochre, rust and teal.
- Expedition: pink #E06AA8, slot 7. Its body is navy, white and signal orange. This is a judgement call and the most debatable; orange would clash with the signal-orange trim. Change it in make_manifest.py ACCENTS, or override per run with --accent.

PILOT SET (12): auth_tank, auth_rifleman, auth_conyard; dir_reactor_crawler, dir_conscript, dir_shaft; lb_rolling_fort, lb_scavenger, lb_mother_rig; exp_command_ship, exp_marine, exp_landing_depot. Buildings are each faction's starting building.

OWNER COMMANDS (from the RTS_2027 root, after git pull):
- Pilot: powershell -ExecutionPolicy Bypass -File tools\booklet\render_heroes.ps1 -Pilot
- All: powershell -ExecutionPolicy Bypass -File tools\booklet\render_heroes.ps1 -All
- Specific ids: ... -Ids auth_tank,lb_mother_rig. Other options: -Samples N, -LongSide N, -Force.
The ps1 activates .venv-art. The .ps1 itself is untested here (no PowerShell on this box); the Python was run.

TIME ESTIMATE (honest):
- One hero is a single 1600 px render at 128 samples with denoise. That is roughly 15-30 times the pixels-times-samples of one sprite frame, so it is far more than the 18 s sprite base (which is 16 small frames at 24 samples).
- Measured here on the 2-core box: auth_tank at 32 samples and 1600 px took 133 s, so 128 samples would be about 8 min here.
- On his laptop with all cores: about 1-4 min per id. Pilot (12) about 15-45 min. All (124) about 2-8 h, resumable. -Samples 48 cuts it to about a third.
- This estimate is extrapolated, not measured on his PC.

TEST RENDERS (not committed):
- Required proof: /home/claude/audit/booklet-render/out/authority/auth_tank.png (1600 px, 32 samples, amber house panels, transparent background; viewed and looks right).
- Extra smoke tests at 500 px and 8 samples: auth_rifleman, auth_conyard, dir_reactor_crawler (turret layer via baked), lb_mother_rig, all under /home/claude/audit/booklet-render/out/<faction>/. Contact sheet: /home/claude/audit/booklet-render/out/contact_test.png. auth_carrier was correctly skipped as no_model.
- bpy installed here is 5.3.0; the owner's is 5.2.3. Not tested on 5.2.3.
- Infantry, structure and the other vehicle styles beyond these five (e.g. air units, exp ships) were not rendered; the same code path applies, but a failure would show up as FAILED and not stop the run.

NOTES / NEW FINDINGS:
- Structures have a single facing, so the "south-west" view applies only to vehicles and infantry. Buildings render face-on as in the game.
- The sprite scripts create empty work dirs under art/pilot/work (git-ignored); the script removes the baked one.
- No tests were added; none of this touches game code.