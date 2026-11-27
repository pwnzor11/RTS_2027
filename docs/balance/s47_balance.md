# Session 47 · per-map balance (B-01..B-03) and layout 2 (A-06)

Owner decisions: D-334 (freeze lifted for per-map work; s46 rules: paired screens, decide on 512 games, campaign sweep 45/45 under D-222; map
levers first; six pairings stay 40-60; goal all 24 rows 35-65), D-335 (layout 2 finished toward the same gate, stays opt-in).

## Starting point (s46 c7 table = main data, 512 games per row)
Rows outside 35-65: ash Authority v Directorate 25.4; glass Directorate v Expedition 30.0; rust Authority v Expedition 67.9, Authority v
Left Behind 71.0, Directorate v Expedition 74.3, Directorate v Left Behind 30.2. Mountains Authority v Directorate 35.2 (edge).

## Method
- Variants = the c7 patch (D-330 numbers) on build 8d4b009 plus one change, run on the cluster (`k3s-cluster/claude/rts/s47/`), paired with the
  s46 c7 files on the same seed chunks (`pair47.py`). `est512` = the c7 512-game row plus the paired difference.
- **Trap found:** `biomes.toml` is read beside the binaries (BiomeCatalog walks up from AppContext.BaseDirectory), not from the working tree.
  The first screen (job s1) therefore measured nothing; s1b and later give each variant its own bin folder with its own `data/mapgen`.
- `ocean_width` 1, 2 and 3 give the same map at size 96 (scaling), so "ocean 2" on glass equalled today's 3; ocean edges are only drawn when
  an Expedition side plays (M7-10), so an ocean width on Rust Belt changes only the Expedition rows.
- Single-key screens s1b: 4 chunks (64 games) per row; combinations s2-s5: 8 chunks (128 games). Raw table: docs/balance/s47_screens.txt.
- The cloud box ran part of s2 (rust r15, r18-r21) with the same binaries and data (2 cores ~ 9 Pi slots).

## Findings
- **Rust Belt:** ocean edge (r11) pulls Directorate v Expedition from ~77 to ~46 and lifts Directorate v Left Behind (+11) but leaves Authority v
  Left Behind at ~76. Ocean edge + no river (r19): Authority v Directorate 44.7, Authority v Expedition 58.0, Authority v Left Behind 53.6,
  Directorate v Expedition 49.3, Directorate v Left Behind 49.3, Left Behind v Expedition 49.2 (128 games each): all six inside 35-65.
  Most single map keys moved Directorate v Left Behind down (roads, contested fields, shallows, rough).
- **Ash Plains:** no map key fixed Authority v Directorate (rough 60/80, rock 25 neutral or worse; base_land clear had no effect).
  Authority infantry ash resist 400 -> 600 (a15): Authority v Directorate +11.5 (est 36.9), other rows within noise.
- **Glass Coast:** Directorate v Expedition 30 not fixed: ocean 0 makes the Expedition weak everywhere (+18..+28 for its opponents); more
  contested fields help that row but cost Directorate v Left Behind -21; Directorate rockets/slugs v ships (g11) no help. Stays at 30 (inside
  the 25-75 gate, outside the 35-65 goal).
- Mountains not screened (edge row, dropped for time).

## Candidate f1 (branch `bal-s47`, commit ea92bc5 + re-pins): D-339
Authority Rifleman/Rocketeer/Marksman/Engineer `rad_resist_permille` 400 -> 600; Rust Belt `rivers` 1 -> 0, `ocean_width` 0 -> 2.
- Decide job **f1** (cluster, 15 rows x 32 chunks = 512 games: the 12 Authority rows + the 3 other Rust Belt rows; the 9 other rows cannot
  change) was at 118 of 480 pieces at hand-off; campaign sweep **sw47b** on ea92bc5 at 12 of 144.
- Read at the start of session 48: `python3 /work/in/s46/final46.py`-style table for f1 (write final47.py from final46.py: f1 rows replace
  c7 rows, the other 9 rows keep c7), then `python3 /work/in/s47/swsum.py sw47b` (D-222: t1 >= 10/12, t3 >= 8/12, t5 >= 3/12).
- Land `bal-s47` only if: six pairings 40-60, every row 25-75, and sw47b passes; otherwise re-screen the failing piece.

## Layout 2 (A-06)
Plan and new keys: docs/balance/s47_layout2_plan.md. The R0 baseline (job l2r0, build 8873ba6) was paused at 31 of 384 pieces to give the
per-map screens the cluster, then deleted: R0 must be re-run on the data that lands from f1 (Rust Belt changes reach layout 2 maps too).
