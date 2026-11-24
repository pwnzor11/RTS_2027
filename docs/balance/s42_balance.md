# Session 42 balance: Expedition ladder, map screens, the 512-game finish-line table (2026-11-24)

Owner's answers at the start (s42): the Authority tier 5 v 1 target of 90 % is dropped ("in order is enough"); the map-specific fixes are this
session's work; queen stays cordoned; the lead merges. All games: `ai match`, tier 3 v tier 3, size 96, layout 1, 30 000 ticks, both sides of each seed.
Cluster jobs (laptop k3s-cluster folder `claude/rts/s42/`, outputs on /work/out): m42/m42b/m42c (map screens), l42..l42d (ladder), f42r/f42m/f42v/f42w,
fg42, fr42 (512-game table), as42/as42b (Ash confirms), sr42 (stuck recovery), sw42 (campaign sweep), bld42/bld42b (builds).

## 1. Expedition ladder (M13-49)
Main (rust_belt, 256 games, seeds 193-320): 2 v 1 48.1 %, 3 v 2 44.3 %, 4 v 3 92 %, 5 v 1 94 %. Tiers 1-3 play alike; only tiers 4-5 (D-291) pull clear.
Screens (128 games, seeds 129-192):
| variant | 2 v 1 | 3 v 2 |
|---|---|---|
| tier-2 army_cap 20 / 24 / 28 / 32 | 51-53 | 46-48 (28 and 32 give games identical to main; 20 and 24 differ by a few games) |
| tier-2 army_cap 16 / 12 | 33 (32 games) / 9 | - / 82 (96 games) |
| tier-2 attack_threshold 14 / 20 | 62 / 62 | 44 / 42 |
| tier-2 respond_to_attacks 0 | 67 | 48 |
| tier-3 wave_reinforce_ticks 1200 / wave_search 1 (D-302) | 51 / 51 | 48 / 46 |
| **tier-1 cap 10 + tier-2 cap 14 (y1)** | **82** | **69** |
| tier-1 cap 12 + tier-2 cap 16 (y2) | 76 | 62 |
| tier-1 cap 9 + tier-2 cap 12 (y3) | 76 | 84 |
The tier-2 cap is a cliff between 12 and 20 (it first binds between 20 and 28), and weakening tier 2 alone cannot order three tiers that play alike, so tier 1 is capped below tier 2.
Confirmed on fresh seeds 193-320 (256 games): y1 2 v 1 **76.3**, 3 v 2 **67.6**, 4 v 3 92, 5 v 1 96. No tier-3 row moves (tiers 1-2 only). Landed (D-307).
The D-302 wave keys did nothing measurable at tier 3 on the Expedition.

## 2. Map screens (64 games per row, seeds 129-160; round 2 seeds 161-192)
Each biome's keys move only that biome's six rows. 28 one-key variants across the four biomes (round 1, plus the two shift-24 reruns), then 12 combinations and neighbours (round 2):
- Ash Plains: start_row_shift 20/24 (max 24), ford_spacing 40, rivers 0, cliff_plateaus 2/3, symmetry rot2, rock spurs + rock 60, river width 2,
  combinations. Plateaus made Authority v Directorate worse (6-19 % v main 34 % on seeds 161-192); rot2 lifted it (38 %) but sent Directorate v
  Expedition to 83 %.
- Mountains: start_row_shift 20/24, plateaus 0/3/4/5, rock 30, rot2. Plateaus 4 put all six rows inside 32-63 % in both rounds.
- Glass Coast: ocean width 2/3/4/9, contested fields 1/3, extra fields 2, roads 16, rough 40. Ocean 3 cut the Expedition's edge.
- Rust Belt: roads 0/24, rivers 0/2, contested 3, extra 4, rough 30: r1-r5 pushed Directorate v Left Behind down (16-29 % v main ~45 % on those seeds; r6 41 %); none kept.
**Noise:** the same build's Ash Authority v Directorate was 10 % on seeds 129-160 and 34 % on 161-192. 64-game map screens find only large effects.

Ash confirms on seeds 1-128 (256 games, main there 27.3 %): start_row_shift 20 **19.0 %**, rock spurs + rock 60 **25 %**. Neither kept: five kinds of
Ash map change (start shift, plateaus, rivers/fords, rock, symmetry) failed to lift the row without breaking another.

## 3. The 512-game table (seeds 1-256; Glass Coast and one Rust row filled by fg42/fr42)
Main rows: ref41 (c0-7), f41 (c8-15), m42 (c16-19), m42c/f42r (c20-23), f42r/f42m (c24-31). Candidates: f42v (c0-15), m42/m42c (c16-23), f42w (c24-31).
| pairing | Rust | Glass | Mountains | Ash | pooled |
|---|---|---|---|---|---|
| Authority v Directorate | 52.7 | 46.4 -> **54.4** | 34.0 -> **31.5** | **23.6** | 39.2 -> **40.6** |
| Authority v Expedition | 73.8 | 53.7 -> 54.8 | 55.5 -> 56.7 | 49.3 | 58.1 -> 58.6 |
| Authority v Left Behind | **75.1** | 63.3 -> 62.9 | 39.7 -> 43.1 | 36.7 | 53.7 -> 54.4 |
| Directorate v Expedition | 72.5 | 28.0 -> 27.2 | 53.6 -> 50.5 | 54.7 | 52.2 -> 51.2 |
| Directorate v Left Behind | 28.9 | 52.3 -> 44.3 | 41.9 -> 51.0 | 44.9 | 42.0 -> 42.3 |
| Left Behind v Expedition | 53.8 | 34.2 -> 44.0 | 45.4 -> 46.9 | 31.1 | 41.1 -> 44.0 |
(main -> with Mountains plateaus 4 and Glass ocean 3; first-named side's share of decisive games. Commit 33b4257's message quotes an earlier
448-game Glass read; this table is the final one.)
Main fails D-305 (pooled Authority v Directorate 39.2 %). With the two map changes (landed, D-308): **all six pairings inside 40-60 %** (40.6-58.6),
16 rows inside 35-65 %, MAD 11.5 -> 10.9; **two map rows outside 25-75 %: Ash Authority v Directorate 23.6 % and Rust Authority v Left Behind 75.1 %**
(1.4 and 0.1 points outside the band, both within about one standard error, ~2 points). -> Q-88.

## 4. Stuck recovery (D-301), sr42
`[movement] stuck_recovery = 1` on seeds 1-128 (same seeds as main, so game-for-game comparable; 16 of 24 rows started, 14 complete, when stopped):
| row | main | stuck_recovery 1 |
|---|---|---|
| Rust Directorate v Left Behind | 30.6 | **18.1** |
| Glass Directorate v Expedition | 27.3 | **18.4** |
| Mountains Authority v Directorate | 34.8 | 28.7 (toward the Directorate) |
| Glass Authority v Directorate | 48.1 | 41.9 (toward the Directorate) |
| Rust Authority v Expedition | 71.7 | 66.0 |
| Glass Left Behind v Expedition | 35.4 | 30.0 |
Other rows within 3.5 points, except Mountains Authority v Left Behind -5.3. It costs the Directorate 9-12 points in its two weakest rows (v Left Behind on Rust, v Expedition on Glass).
**Stays off** (D-309).

## 5. Campaign (D-222), sw42
Campaign sweep on the landed code (baf0bd3, 45 mission-tiers x 12 seeds): every result identical to session 41 (sw41); all meet D-222.
Missions do not read the skirmish tier tables the Expedition caps change.

## 6. Cloud test suite (baf0bd3)
Sim 1929/1931 passed (2 skipped), AI 430 (one stall rig re-anchored to the pre-s42 ocean width, then 0 failed), Game 2213/2216 (skips),
Scripting 438, Net 175, Llm 174, Tools 144: 0 failed.
