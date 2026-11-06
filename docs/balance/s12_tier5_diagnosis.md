# S12 tier 5 diagnosis (M4-16, package s12-tier5)

Work log, committed after each step. Method: `ai timeline --a tiered:5 --b tiered:1 --faction-a authority --faction-b authority --seed N --size 96 --ticks 30000 --trace 1200`
(slot 0 = commissar, slot 1 = recruit), then 32-game A/B cells (16 seeds x 2) per flag.

## Step 1: single-game traces (commissar vs recruit, Authority mirror, no swap)

Winners over seeds 1-6, both start sides (12 games): recruit won 9 (seeds 1,2,2s,3s,4,4s,5s,6,6s), commissar won 2 (3, 5), 1 draw (1s).

Seed 4 (recruit wins at t9032), per 1200 ticks (army = units excl. harvesters, hp = sum health):

    t3600  P0 commissar army 15 hp 1510 bldg 11 cash 0     | P1 recruit army 21 hp 1940 bldg 9  cash 1935
    t4800  P0 army  4 hp  910 bldg 11                      | P1 army 24 hp 2721 bldg 10
    t6000  P0 army  0 hp    0 bldg  9                      | P1 army 21 hp 2630 bldg 10

Seed 6: t3600 army 12 vs 12; t4800 army 5 (hp 502) vs 17 (hp 1669). Seed 1: t3600 9 vs 15; t4800 10 vs 23; t9600 commissar army 0.
Seed 2: t3600 12 vs 11 (hp 990 vs 600); the commissar then stays level for a long time (25 v 21 at t6000) but never closes the game.

Reading so far (to be confirmed by cells below):
1. The commissar launches its first wave around t3000-3600 with 9-15 units (attack_threshold 6, no enemy army seen yet so the ratio gate does not bind)
   into a recruit that has 15-21 units at home, and loses it (army 15 -> 4 in seed 4, 12 -> 5 in seed 6).
2. The commissar spends everything it has on structures (21-22 buildings against the recruit's 10 by t10000) and its cash is 0 at nearly every sample, while the
   recruit keeps ~20 units with 2x the hp per unit sum.
3. The commissar drops back down the tech tiers (trace shows tier 3 -> 2 -> 1 in seeds 2, 4, 6) as its tech structures die.

## Step 2: one-flag A/B cells, commissar (A) vs recruit (B), Authority mirror, 16 seeds x 2 = 32 games, 30 000 ticks

(Edited copy of `difficulty.toml` run from a scratch build folder; only the `[commissar]` table changes.)

| Variant (commissar) | A wins | B wins | draws | A win % (of 32) |
|---|---:|---:|---:|---:|
| baseline | 11 | 20 | 1 | 34 |
| attack_threshold 6 -> 14 | 11 | 18 | 3 | 34 |
| superweapons 0 | 11 | 20 | 1 | 34 (identical: no superweapon is ever built in these games) |
| expansion_tick 0 | 6 | 19 | 7 | 18 (worse: the expansion is needed) |
| use_upgrades 0 | 15 | 13 | 4 | 46 |
| adapt_build 0 | 14 | 14 | 4 | 43 |
| multi_front 0 | 9 | 20 | 3 | 28 (worse) |
| scouting 0, harass 0 | 11 | 20 | 1 | 34 (no effect) |

Reading: waves, superweapon, scouting and harassment are not the cause. The two capabilities that spend credits on things other than army,
`use_upgrades` (tier gates, research, airfield) and `adapt_build` (extra production and defence structures when cash piles up), each cost the commissar
about 4 wins when on. The recruit, which builds nothing but a fixed order, ends with 10 structures against the commissar's 21 (trace, seeds 2/4/6).
