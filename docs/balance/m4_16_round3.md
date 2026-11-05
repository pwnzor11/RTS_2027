# M4-16 round 3: why the higher AI tiers lose, and AI-only fixes

Build: main at ed79d8c plus the Tools-only `ai match --trace N --game G` diagnostic (01cecd4; one extra optional observer parameter
on `AiMatchRunner.Play`, no behaviour change). Method as in round 2: `Tools ai match`, 8 seeds x 2 sides, size 56, 25 000 ticks, per-game
seeds (D-104). Raw outputs: scratch/r3 (git-ignored). Games are deterministic, so a re-run of the same build reproduces the numbers exactly.

## 1. Baseline (re-run on main): round-2 numbers reproduce exactly, cell for cell

| Pairing | Authority A / B / draw | A win % | Directorate A / B / draw | A win % |
|---|---|---|---|---|
| regular > recruit (2 v 1) | 5 / 11 / 0 | 31 % | 6 / 1 / 9 | 37 % |
| veteran > regular (3 v 2) | 6 / 10 / 0 | 37 % | 7 / 2 / 7 | 43 % |
| elite > veteran (4 v 3) | 5 / 5 / 6 | 31 % | 4 / 6 / 6 | 25 % |
| commissar > elite (5 v 4) | 5 / 10 / 1 | 31 % | 4 / 3 / 9 | 25 % |

Cross-faction at equal tier (Authority / Directorate / draw): t1 4/8/4, t2 5/4/7, t3 6/5/5, t4 9/2/5, t5 4/5/7.
Directorate veteran mirror: 5 / 8 / 3. Start-corner wins (start 0 - start 1): Authority ladder 5-11, 10-6, 4-6, 13-2;
Directorate ladder 5-2, 5-4, 7-3, 5-2; cross 9-3, 7-2, 5-6, 6-5, 7-2; mirror 11-2.
