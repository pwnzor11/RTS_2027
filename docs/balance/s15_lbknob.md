# S15 lbknob: Left Behind tier 3 per-tier override (D-186, AI changes only)

Method: Release build of `src/Tools` per variant in a scratch dir; `ai match --seeds 16 --size 96 --ticks 30000` (32 games per cell, A = first faction/tier, starts swapped).
Probe runs of 8 seeds screened the first candidates. Games are deterministic: the final binary reproduced the probe cells game for game.
Noise: about +-10 points on a decisive % at 30 decisive games, +-3 on a draw count. "Dec %" = A wins / (A wins + B wins).

## Diagnosis

`ai match --trace` (seed 1, Left Behind 3 v Authority 3): Left Behind fields 16 units at its first wave (t1351) against 5, 33 at t2011 against 6 and 46 at t3000 against 11.
Cumulative income at t3000 is about the same (7 043 v 5 884); the difference is what it buys. Buggy / Hook Runner / Scavenger bags are the cheapest bodies of any faction, the
Yard brings the refinery, silo and a free Gleaner, and the shared tier 3 profile spends everything on units at once. By t3000 the Authority side has 11 units and has not yet
climbed to its tier 2. The snowball is in the first 3 000 ticks; after that both sides trade.

Why the previous shared knobs failed (docs/balance/s15_q65.md): the wave trigger is read by tier 5 too, and a higher or lower trigger moves the Authority and Directorate tier 3 profiles
as well. The shape of the problem is "too many units, too early", which no shared number touches.

## The knob

Two places changed, both behaviour-only:

1. **Data**: `data/ai/difficulty.toml` accepts `[faction.<id>.tier<N>]` (N = 1..5) next to the existing `[faction.<id>]` (D-167) tables. A tier table overrides some numbers of that one tier for that one
   faction. Allowed keys: `attack_threshold`, `attack_ratio_pct`, `expansion_tick`, `harvester_target`, `retreat_health_pct`, `retreat_ratio_pct` and two new ones, `unit_reserve` and `army_cap` /
   `army_cap_until_tick`. Anything else, a float, an out-of-range value or `tier9` is a load problem. Loader: `DifficultyLoader` (`DifficultyProfile.ForFaction`, `WithFactionOverride`);
   `RecruitAi.Init` and `ApplyHooks` pick the faction's version of the tier (the same instance every call, so AI.SetTier still compares by reference).
2. **AI**: `army_cap` (units alive, harvesters excluded: army + home guard + detached) pauses the unit lanes of `ProductionManager` while the army is at the cap and, with `army_cap_until_tick`, only before that tick.
   A threshold override also moves the wave trigger drawn in `Start` (`MilitaryManager.ShiftFirstTrigger`, no extra random numbers). `unit_reserve` (extra credits kept in hand per unit purchase)
   was built and not used: it only delays purchases once, it does not change the pace.

Shipped: `[faction.left_behind.tier3]  army_cap = 20, army_cap_until_tick = 3600`. Nothing else in the data changed; tier 5 and the other tiers of Left Behind are untouched (their `ForFaction` is the shared profile).

No new long-lived AI field: the cap is read from the profile, the shifted trigger is part of the already saved `_nextThreshold`. `LbKnobTests` checks a mid-game save of a Left Behind tier 3 AI across the end of the cap continues
byte for byte like an AI that never saved.

## Probes (what was tried; decisive wins A / B / draws, 16 seeds unless noted)

| Variant (Left Behind 3) | v Authority 3 | v Expedition 3 | v Directorate 3 | Reading |
|---|---|---|---|---|
| base | 21 / 10 / 1 | 17 / 10 / 5 | 26 / 4 / 2 | |
| wave trigger 4 (8 seeds) | 11 / 5 / 0 | - | 12 / 3 / 1 | no effect with the override applied to Left Behind alone |
| cap 16 / 24 / 32 for the whole game (8 seeds, Authority 3) | 1 / 15, 3 / 12, 7 / 7 | - | 2 / 11, 4 / 5, 10 / 3 | monotone and steep; far too strong below 32 |
| cap 36 whole game | 14 / 15 / 3 | 6 / 20 / 6 | 21 / 7 / 4 | Expedition 3 collapses (23 % dec) |
| cap 44 whole game | 19 / 11 / 2 | 8 / 16 / 8 | 24 / 4 / 4 | same |
| cap 52 whole game | 17 / 11 / 4 | 12 / 17 / 3 | 25 / 4 / 3 | Expedition still 41 % |
| cap 28 until t6000 | 19 / 12 / 1 | 10 / 18 / 4 | - | |
| cap 24 until t3000 | 19 / 11 / 2 | 11 / 16 / 5 | 20 / 7 / 5 | |
| cap 16 until t2400 | 15 / 17 / 0 | 16 / 13 / 3 | 22 / 6 / 4 | Authority 47 % |
| cap 18 until t3000 | 18 / 11 / 3 | 12 / 12 / 8 | 22 / 5 / 5 | |
| **cap 20 until t3600 (shipped)** | **16 / 14 / 2** | **15 / 12 / 5** | **17 / 6 / 9** | |

A permanent cap beats Left Behind against the Expedition (whose long game and big ships outlast a capped swarm) much harder than against Authority; the early cap removes the snowball and leaves the
mid game as it was.

## Before / after (32 games per cell, A / B / draws, average end tick)

| Cell | Before | After | Dec % A before -> after |
|---|---|---|---|
| Left Behind 3 v Authority 3 | 21 / 10 / 1 (14023) | 16 / 14 / 2 (15675) | 68 -> 53 (target 50-62: met) |
| Left Behind 3 v Expedition 3 | 17 / 10 / 5 (16519) | 15 / 12 / 5 (17793) | 63 -> 56 (target 50-62: met) |
| Left Behind 3 v Directorate 3 | 26 / 4 / 2 (14042) | 17 / 6 / 9 (18575) | 87 -> 74 (aim < 70: NOT met; draws 2 -> 9) |
| Left Behind 5 v Left Behind 3 | 11 / 17 / 4 (17527) | 15 / 11 / 6 (19395) | 39 -> 58 (tier 5 now beats tier 3) |
| Left Behind 3 v Left Behind 1 | 26 / 4 / 2 (13602) | 26 / 4 / 2 (15207) | 87 -> 87 |
| Left Behind 5 v Authority 5 | 11 / 16 / 5 (17921) | 11 / 16 / 5 (17921) | unchanged |
| Authority 5 v Authority 1 (control) | 20 / 9 / 3 (17868) | 20 / 9 / 3 (17868) | unchanged |

## Reading
- Authority 3 and Expedition 3 are inside the 50-62 % band; the Left Behind ladder is intact (5 beats 3 in 15 games to 11, 3 beats 1 in 26 to 4); the tier 5 row and the control are bit-identical.
- Directorate 3 is the miss. It fell from 87 % to 74 % decisive but did not reach 70 %, and its draws rose from 2 to 9 (Left Behind can no longer finish a defended Directorate base before the cap ends and the
  Directorate 3 economy catches up). Every cap that touched the Directorate more (the whole-game caps at 24-36) did so by breaking the Expedition cell. Directorate 3 is also the weakest tier 3 against
  everything (it loses 11 / 14 to Authority 3), so a Left Behind nerf alone cannot bring that cell to 50-60 %: the remaining gap is Directorate strength, not Left Behind strength.
- The draw increase in that cell is a real cost and is above the +-3 noise. The alternatives with fewer draws (cap 16 until 2400, cap 18 until 3000) pushed Authority below 50 % or Expedition to 50 % with 8 draws.

## Options for the owner
- Accept this (Authority 3 / Expedition 3 in band, ladder fixed, Directorate 3 clearly down but at 74 %).
- Tighten to cap 18 until t3000 (Authority 62 %, Directorate 81 %, Expedition 50 % with 8 draws) or loosen the cap by a few units; each step moves the Directorate cell by under the noise.
- Fix the Directorate 3 side in a separate package (its early army is 5-11 units at t3000).
