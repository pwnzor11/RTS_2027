# Session 32 balance round (Sparker cap on top of D-265; Left Behind diagnosis)

Cells as in sessions 27-31: `ai match` tiered:3 v tiered:3, size 96, layout 1, 30 000 ticks, `--income 1`; decision = seeds 1-64 x 2 start swaps
(128 games per row). Landing rule D-264. Baseline = the s31 main table with M12-16 and D-265 (docs/balance/s31_balance.md). Variant branches
`bal/s32-k9`, `bal/s32-k10` are measurement only. Cluster scripts: laptop k3s-cluster folder `claude/rts/s32/` (build.sh, series.sh, sum32.py,
buildsw.sh, sweep.sh, swsum.py, trace.sh). The whole round (2 builds, 96-index series, 135-index campaign sweep) took about 75 minutes of cluster time.

## Old-rules hint (job s31s3, finished overnight)
On the rules before M12-16 against the s28 table: dk (D-265 + cap 10) moved Dir v Exp 70 -> 58 with no row moving away; kd (cap 8 + Directorate
army cap 30) took Dir v Exp to 48 but Auth v Exp 7.7 points away (fails); k10 alone Dir v Exp 70 -> 65.

## Decision runs (128 games, Expedition rows only; the cap cannot touch the other three rows)
| Row | Main (s31, D-265) | k9 = cap 9 | k10 = cap 10 | k10 away from 50 |
|---|---|---|---|---|
| Authority v Expedition rust | 52-72-4 (41.9 %) | 45-82-1 (35.4 %) | 47-78-3 (37.6 %) | +4.3 (under 5) |
| Directorate v Expedition rust | 64-39-25 (62.1 %) | 62-43-23 (59.0 %) | 58-42-28 (58.0 %) | -4.1 toward |
| Left Behind v Expedition rust | 61-53-14 (53.5 %) | 52-61-15 (46.0 %) | 57-60-11 (48.7 %) | -2.2 toward |
| Expedition v Authority glass | 65-58-5 (52.8 %) | identical | identical | 0 |
| Expedition v Directorate glass | 56-48-24 (53.8 %) | 56-48-24 | 57-47-24 (54.8 %) | +1.0 |
| Expedition v Left Behind glass | 69-31-28 (69.0 %) | identical | identical | 0 |
| Distance over these six rows | 49.4 | 53.3 | 48.3 | |

k9 fails D-264 (Auth v Exp 6.5 points away). k10 passes: the target row moves toward 50 % and no row moves 5 or more away. The glass rows barely move:
on Glass Coast the Expedition seldom reaches 10 anti-air vehicles before it sees aircraft.

Campaign sweep with k10 (45 mission-tiers x 12 seeds, job s32sw): 45/45 meet D-222.

## Decision
k10 lands (D-266): `[faction.expedition] aa_cap_before_air = 10`. Remaining outliers: Authority v Left Behind 69.5 %, Expedition v Left Behind glass
69 %, Directorate v Expedition 58 %.

## Left Behind diagnosis (job s32tr, main, ledgers over 16 seeds + traces)
- In Authority v Left Behind the Left Behind loses when its own income stalls: average income at 5 minutes 13 726 in its losses v 18 929 in its wins
  (Authority income is the same in both, ~18 400-19 000). Directorate v Left Behind shows the same split (8 740 v 19 184).
- The traces show why: the Left Behind's three early haulers empty the ore next to its Mother Rig by tick ~3000 on both maps ("near 0c" in the econ
  line, while the Authority still has ore near its base). The Mother Rig then stays put; haulers make long trips to distant fields, income growth
  stalls (one trace: flat at 22 155 from tick 7500), and the Mother Rig packs up to move only late (tick ~9000) or never.
- So the cause is the Left Behind AI's late relocation/expansion once its home field is empty, not a unit matchup. It shows most against the
  Authority because Authority waves arrive while the Left Behind is starved; against the Expedition (weak early) it costs less.
- Next: an AI rule that packs the Mother Rig (or sends a new rig) toward the nearest rich field when "near" ore drops below a threshold, measured on
  the Left Behind rows (Auth v LB, Dir v LB, LB v Exp, Exp v LB glass). Code change in the Left Behind AI; screen before landing.

## Economic relocation screen (64 games, seeds 1-32; rejected)
Rule (branch `integ-s32b`, commit 3c87b32): at tier 3+, with no armed enemy near the base and harvesters alive, when fewer than N ore cells lie
within R cells of the Mother Rig, the rigs pack and move to the richest ore field (>= F cells within 6) on the own half, 14-44 cells away; only
the refineries left behind are sold. e0 = the code with the rule off (game-for-game main: Auth v LB 39-20-5 on these seeds, same as s31base).

| Row (LB side) | e0 (off) | e1 N4 R12 F10 | e2 N4 R12 F6 | e3 N8 R14 F6 | e4 N2 R10 F6 |
|---|---|---|---|---|---|
| Authority v Left Behind | 66.1 % | 78.3 % | 83.6 % | 81.4 % | 85.0 % |
| Directorate v Left Behind | 51.7 % | 72.9 % | 76.3 % | 70.2 % | 67.7 % |
| Left Behind v Expedition | 52.6 % | 46.4 % | 41.0 % | 31.6 % | 38.3 % |
| Expedition v Left Behind glass | 64.4 % | 82.9 % | 86.7 % | 71.7 % | 77.4 % |

Every setting makes the Left Behind 6-25 points weaker on every row. Moving the base for ore costs more than the shorter harvester trips give
back: the rigs stop producing while packed and the new refinery has to be rebuilt. The diagnosis stands (Left Behind losses follow its income
stalling once its corner is mined out and its harvesters die on long trips), but relocation is the wrong lever. Untried levers: protecting or
replacing the Left Behind's harvesters, an earlier second refinery placed toward the next field, or more starting ore near its corner (map data).

## Harvester replacement screen (64 games, seeds 1-32; rejected)
Data-only: `[faction.left_behind.tier3] harvester_target` (shared veteran table: 1, +1 after the expansion refinery; the Left Behind starts with
3 and rebuilds only up to the target). Branches `bal/s32-h2..h4`; e0 as above.

| Row (LB side) | e0 | h2 = 2 | h3 = 3 | h4 = 4 |
|---|---|---|---|---|
| Authority v Left Behind | 66.1 % | 67.9 % | 74.2 % | 61.4 % |
| Directorate v Left Behind | 51.7 % | 48.3 % | 47.5 % | 45.8 % |
| Left Behind v Expedition | 52.6 % | 50.9 % | 51.9 % | 54.5 % |
| Expedition v Left Behind glass | 64.4 % | 63.0 % | 70.4 % | 72.9 % |

No setting moves the target row toward 50 % without another row failing (h4: Auth v LB -4.7 but Exp v LB glass +8.5); h2 is within noise.
Keeping more harvesters alive does not fix the Left Behind's mid-game stall either (more of them die on the same long trips, and the money held
for them slows the army).
