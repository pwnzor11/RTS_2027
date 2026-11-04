# M4-16 round 2: re-measure on the fixed starts (session 7)

Build: integ-s7 at f24eacb (Release), which plays exactly like session 6's main for games without an LLM directive (all pinned hashes
and AI tests unchanged apart from the appended "directive" state part). Method as in m4_16_ai.md: `Tools ai match`, 8 seeds x 2
(starts swapped) = 16 games per cell, map size 56, 25 000 ticks, per-game seeds (D-104). Raw tables:
scratch/r2 in the measurement worktree (not committed); each cell took 1–2.5 min, so the whole set runs in about 15 min on 2 cores.

## Tier ladder (A = higher tier) — target >= 60 % A wins: not met in any cell

| Pairing | Authority A / B / draw | A win % | Directorate A / B / draw | A win % |
|---|---|---|---|---|
| regular > recruit (2 v 1) | 5 / 11 / 0 | 31 % | 6 / 1 / 9 | 37 % |
| veteran > regular (3 v 2) | 6 / 10 / 0 | 37 % | 7 / 2 / 7 | 43 % |
| elite > veteran (4 v 3) | 5 / 5 / 6 | 31 % | 4 / 6 / 6 | 25 % |
| commissar > elite (5 v 4) | 5 / 10 / 1 | 31 % | 4 / 3 / 9 | 25 % |

Authority's ladder is inverted at three of four steps: the LOWER tier wins most decisive games. The Directorate ladder points the
right way at the bottom (6/1, 7/2) but drowns in draws (9 and 7 of 16).

## Cross-faction at equal tier (A = Authority, B = Directorate)

| Tier | A / B / draw | Authority win % |
|---|---|---|
| 1 | 4 / 8 / 4 | 25 % |
| 2 | 5 / 4 / 7 | 31 % |
| 3 | 6 / 5 / 5 | 37 % |
| 4 | 9 / 2 / 5 | 56 % |
| 5 | 4 / 5 / 7 | 25 % |

Roughly even apart from tier 1 (Directorate ahead) and tier 4 (Authority ahead). Draws stay high (4–7 of 16).

## Directorate veteran mirror and start bias

Mirror (tier 3 v 3): 5 / 8 / 3 draws — no freeze (session 6 was 5 / 5 / 6). Start corner still matters in some cells: start 0 won
13 of 15 decisive games in Authority 5 v 4, 11 of 13 in the Directorate mirror, 9 of 12 in cross tier 1; other cells are even.

## Reading and next steps (not done this session)

1. The higher tiers lose more than they win in Authority. Something a higher tier does is actively harmful — candidates: tier-2/3
   gate and upgrade spending with the tech hold off (TierPlanner.TechHoldEnabled = false), and the "finish won games" push.
   Next: `Tools ai timeline` on one lost Authority 3 v 2 seed per side to see where the higher tier falls behind (army value at
   ticks 5 000 / 10 000).
2. Then measure, one change at a time: tech hold ON; the Q-43 tier-cost cut (2 500 → 2 000); mirror-symmetric placement together with
   seeded placement variation (start bias).
3. Draws: Directorate cells end in draws 40–55 % of the time at 25 000 ticks; check whether waves stall against Directorate defences.
4. The 4-player matrix was not re-run this session.
