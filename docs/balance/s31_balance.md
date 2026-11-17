# Session 31 balance round (Directorate-side levers, D-264 landing rule, Pi cluster)

Cells as in sessions 27-30: `ai match` tiered:3 v tiered:3, size 96, layout 1, 30 000 ticks, `--income 1`; screen = seeds 1-32 x 2 start swaps
(64 games per row), decision = seeds 1-64 (128 games). Landing rule D-264 (supersedes D-257 (1)): the target row moves toward 50 % on 128 games
and no other cross-faction row moves 5 points or more away from 50 %. Variant branches `bal/s31-*` are measurement only. Cluster scripts: laptop
k3s-cluster folder `claude/rts/s31/` (build*.sh, screen*.sh, base.sh, d4i.sh, sum31.py).

## Re-reading sessions 29-30 under the 5-point rule
No old variant passes cleanly. Closest: Sparker cap 8 (k8: Dir v Exp 73 -> 68, Auth v Exp 41 -> 35 = 6 points away) and cap 10 + mop-up (n10: only
1 point of gain).

## Screen (64 games; all rows; main of s28 in brackets on the first column)
| Variant | Change | Result |
|---|---|---|
| d1 | Directorate tier-3 unit_reserve 600 | Auth v Dir 38 -> 53, Dir v LB 49 -> 41, Exp v Dir 52 -> 59, Dir v Exp unchanged 73 |
| d2 | Directorate tier-3 army_cap 30 | Dir v Exp 68, Exp v Dir 63 (11 away): fail |
| d3 | Directorate tier-3 army_cap 24 | Dir v Exp 60, Dir v LB 34 (15 away): fail |
| **d4** | **Directorate tier-3 attack_ratio_pct 140 (shared 110)** | **Dir v Exp 68, Auth v Dir 46, Dir v LB 50, Exp v Dir 57; distance 78 (main 87)** |
| d5 | Directorate tier-3 attack_threshold 12 | Exp v Dir 67, Dir v LB 41: fail |
| d6 / d7 | ratio 125 / 160 | d6: Exp v Dir 59 (5.2 away): fail; d7: no better than d4 on the rows it finished |
| k10 | Expedition aa_cap_before_air 10 | Dir v Exp 69, Auth v Exp 38 (5.7 away v the 128-game main): fail |
| kd | Sparker cap 8 + Directorate army_cap 30 | Dir v Exp 52 (!), but Auth v Dir 36 and Auth v Exp 35 (8 away each): fail |

## Decision runs (128 games)
d4 on the old rules (main of session 28 = 128-game table in s28_balance.md): Auth v Dir 44 -> 50, Dir v Exp 70 -> 64, Dir v LB 46 -> 47,
Exp v Dir glass 53.5 -> 52; the five rows without the Directorate are game-for-game main.

M12-16 (destructible props) changes generated skirmish maps too (same seeds, other end ticks), so main was re-measured with it (integ-s31,
cluster job s31base) and d4 re-measured on top (s31d4i):

| Row | Baseline with M12-16 (128) | d4 with M12-16 (128) | Away from 50 |
|---|---|---|---|
| Authority v Directorate rust | 47-58-23 (44.8 %) | 50-55-23 (47.6 %) | -2.8 toward |
| Authority v Left Behind rust | 82-36-10 (69.5 %) | identical (control) | 0 |
| Authority v Expedition rust | 52-72-4 (41.9 %) | (untouched) | |
| Directorate v Left Behind rust | 53-60-15 (46.9 %) | 54-59-15 (47.8 %) | -0.9 toward |
| Directorate v Expedition rust | 70-30-28 (70.0 %) | 64-39-25 (62.1 %) | -7.9 toward |
| Left Behind v Expedition rust | 61-53-14 (53.5 %) | (untouched) | |
| Expedition v Authority glass | 65-58-5 (52.8 %) | (untouched) | |
| Expedition v Directorate glass | 62-45-21 (57.9 %) | 56-48-24 (53.8 %) | -4.1 toward |
| Expedition v Left Behind glass | 69-31-28 (69.0 %) | (untouched) | |
| Sum of distances from 50 % | 89 | 73 | |

## Decision
d4 lands (D-265): `[faction.directorate.tier3] attack_ratio_pct = 140`. Remaining outliers: Authority v Left Behind 69.5 %, Expedition v Left
Behind glass 69 %, Directorate v Expedition 62 %. kd showed the Sparker cap plus a Directorate cap can take Dir v Exp to ~50 but at the
Authority's cost; with d4 in, a Sparker cap of 9-10 on top is worth a screen next session.
