# Session 48 balance (B-01..B-03, A-06)

## f1 decide: landed (D-340)
Job **f1** (cluster, build 8d4b009 + patch f1: Authority infantry `rad_resist_permille` 600; Rust Belt `rivers` 0, `ocean_width` 2), 15 rows x 32 chunks
= 512 games per row; the 9 rows f1 cannot change keep the s46 c7 table (`/work/in/s47/final47.py`).

| Pairing (pooled, 4 maps) | f1 | r45 (before s46) |
|---|---|---|
| Authority v Directorate | 42.9 | 33.6 |
| Authority v Expedition | 52.6 | 64.6 |
| Authority v Left Behind | 51.8 | 50.2 |
| Directorate v Expedition | 46.4 | 48.5 |
| Directorate v Left Behind | 51.7 | 44.8 |
| Left Behind v Expedition | 50.9 | 56.9 |

All six pairings inside 40-60 and all 24 rows inside 25-75 (D-305). Rows outside the 35-65 aim: **Glass Coast Directorate v Expedition 30.0**,
**Ash Plains Authority v Directorate 32.0**. Highest row: Glass Coast Authority v Directorate 57.9 (no row above 60).
Campaign sweep **sw47b** (ea92bc5): 48 cases, every tier 1 >= 10/12, tier 3 >= 8/12, tier 5 >= 3/12 (D-222); the nine tier-5 cases at 11-12/12
(auth_01 11, auth_03, auth_07, exp_01..04, lb_03, lb_04 at 12) are unchanged from sw47a. Landed by PR #131.

## Screens for the two low rows (job s48a, 64 games per row, paired with f1/c7 on the same chunks: `/work/in/s48/pair48.py`)
| Variant | Change (on top of f1) | Effect |
|---|---|---|
| d1 | Directorate rockets and slugs v ShipLight 120, ShipHeavy 100 | Glass Dir v Exp -5.4; others within +-4. No help. |
| d2 | the same at 150 / 120 | Glass Dir v Exp +0.7. No help. |
| d3 | rockets only, ShipLight 130 | Glass Dir v Exp -0.3. No help. |
| e1 | Authority infantry ash resist 750 | Ash Auth v Dir +1.3, Rust +11.1, Mountains +5.6, Glass +2.6 (noisy, small) |
| e2 | ash resist 900 | Auth v Dir +18 to +24 on every map (Ash 54, Glass 77, Mountains 58, Rust 69): too strong |

Reading: (1) the Glass Coast Directorate v Expedition gap is not about Directorate damage against ships (s47 g11/g12 said the same); the only lever that
moved it in s47 was removing the Glass ocean (gc2, +28), which also lifts the other two Glass Expedition rows by ~19 (too far). It needs a new kind of
lever: candidates for the next round are a Glass-only Expedition cost (landing craft / hover yard) or a Directorate anti-hover change aimed at Light
armour on Glass only (no such per-biome key exists today). (2) Authority ash resist has a cliff between 750 and 900 and moves Authority v
Directorate on every map, not only Ash Plains. Round 2 (job **s48b**, 825 and 850 on the four Authority v Directorate rows) was running at
hand-off; a value that brings Ash to ~40 while keeping Glass <= 65 would also move the pooled pairing toward 50.

## Layout 2 R0 (A-06): running
Jobs **l2r0b** (24 pairing rows x 16 chunks, 128 games per row) and **l2m** (16 mirror rows x 8 chunks) on build d16844b (main + f1 data).
Script `k3s-cluster/claude/rts/s48/l2r0.sh`, `l2m.sh`; outputs `/work/out/l2r0b`, `/work/out/l2m`. Tally with the s46/s47 tally scripts
(`/work/in/s41/tally41.py /work/out/l2r0b 0 15`).
