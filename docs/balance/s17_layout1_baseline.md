# Layout 1 baseline on the owner's laptop (session 17)

Purpose: a 64-game reference for the layout 2 blockers of D-197 (start side at most 60 % of decisive games; income within 10 % of layout 1).
Script: `tools/measure/run_s17_layout1_base.ps1`. Layout 1 output does not change with the layout 2 work, so these numbers stay valid.

Commit 00bff5a, 8 logical CPUs, 4 jobs, finished 2026-11-10 18:51, wall time 92 min. `ai match` tiered AI, 32 seeds x 2 start swaps = 64 games per row, size 96, --layout 1 --income 1. Start share = larger of the two start-corner win counts / decisive games. Income lines are in each row's .log.

| Biome | Match (A v B) | A wins | B wins | Draws | Avg end tick | Start 0 wins | Start 1 wins | Larger start share |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| rust_belt | authority 3 v authority 3 | 30 | 27 | 7 | 17786 | 29 | 28 | 51 % |
| rust_belt | directorate 3 v directorate 3 | 24 | 26 | 14 | 19949 | 19 | 31 | 62 % |
| rust_belt | left_behind 3 v left_behind 3 | 30 | 23 | 11 | 16167 | 22 | 31 | 58 % |
| rust_belt | expedition 3 v expedition 3 | 29 | 30 | 5 | 18139 | 36 | 23 | 61 % |
| glass_coast | authority 3 v authority 3 | 29 | 28 | 7 | 13075 | 31 | 26 | 54 % |
| glass_coast | directorate 3 v directorate 3 | 28 | 21 | 15 | 17318 | 26 | 23 | 53 % |
| glass_coast | left_behind 3 v left_behind 3 | 24 | 23 | 17 | 16814 | 29 | 18 | 62 % |
| glass_coast | expedition 3 v expedition 3 | 28 | 32 | 4 | 13684 | 55 | 5 | 92 % |
| mountains | authority 3 v authority 3 | 29 | 28 | 7 | 17597 | 24 | 33 | 58 % |
| mountains | directorate 3 v directorate 3 | 23 | 25 | 16 | 20871 | 15 | 33 | 69 % |
| mountains | left_behind 3 v left_behind 3 | 6 | 14 | 44 | 25667 | 9 | 11 | 55 % |
| mountains | expedition 3 v expedition 3 | 8 | 4 | 52 | 26960 | 1 | 11 | 92 % |
| ash_plains | authority 3 v authority 3 | 31 | 27 | 6 | 17466 | 38 | 20 | 66 % |
| ash_plains | directorate 3 v directorate 3 | 26 | 25 | 13 | 21393 | 22 | 29 | 57 % |
| ash_plains | left_behind 3 v left_behind 3 | 23 | 20 | 21 | 19590 | 26 | 17 | 60 % |
| ash_plains | expedition 3 v expedition 3 | 26 | 36 | 2 | 17270 | 32 | 30 | 52 % |

## Start fairness, pooled over the four factions (decisive games by start corner)

| Biome | Start 0 | Start 1 | Larger share | Layout 2 v5 (s16_layout2.md) |
|---|---:|---:|---:|---:|
| rust_belt | 106 | 113 | 52 % | 51 % |
| glass_coast | 141 | 72 | 66 % (56 % without the Expedition row) | 50 % |
| mountains | 49 | 88 | 64 % | 57 % |
| ash_plains | 118 | 96 | 55 % | 64 % |

With 48-60 decisive games a single cell has a standard error of about 7 points, so 58-62 % cells are inside the noise. Outside it:
glass_coast Expedition (55 v 5), mountains Directorate (15 v 33), ash_plains Authority (38 v 20).
Layout 1 itself does not meet the 60 % limit on glass_coast and mountains.

## Income (credits harvested + shipments, cumulative at 3 / 5 / 10 min, mean over both sides of all 64 games)

| Biome | Faction | Cumulative 3 / 5 / 10 min | Per minute 3 / 5 / 10 | Sides with no income at 3 min (of 128) |
|---|---|---|---|---:|
| rust_belt | authority | 8690 / 17858 / 30765 | 2896 / 3571 / 3076 | 0 |
| rust_belt | directorate | 8276 / 16654 / 26680 | 2758 / 3330 / 2668 | 0 |
| rust_belt | left_behind | 7390 / 13860 / 15138 | 2463 / 2772 / 1513 | 0 |
| rust_belt | expedition | 7442 / 17226 / 32032 | 2480 / 3445 / 3203 | 0 |
| glass_coast | authority | 8495 / 15204 / 9278 | 2831 / 3040 / 927 | 0 |
| glass_coast | directorate | 7811 / 14462 / 17116 | 2603 / 2892 / 1711 | 0 |
| glass_coast | left_behind | 8222 / 15184 / 12542 | 2740 / 3036 / 1254 | 0 |
| glass_coast | expedition | 4728 / 13214 / 11326 | 1576 / 2642 / 1132 | 4 |
| mountains | authority | 9051 / 19496 / 33015 | 3017 / 3899 / 3301 | 0 |
| mountains | directorate | 8027 / 17691 / 34231 | 2675 / 3538 / 3423 | 0 |
| mountains | left_behind | 7792 / 14412 / 23301 | 2597 / 2882 / 2330 | 0 |
| mountains | expedition | 547 / 1478 / 2702 | 182 / 295 / 270 | 100 |
| ash_plains | authority | 9905 / 20821 / 36384 | 3301 / 4164 / 3638 | 0 |
| ash_plains | directorate | 8812 / 19175 / 40457 | 2937 / 3835 / 4045 | 0 |
| ash_plains | left_behind | 8028 / 16177 / 24932 | 2676 / 3235 / 2493 | 2 |
| ash_plains | expedition | 7412 / 18351 / 36646 | 2470 / 3670 / 3664 | 1 |

## Reading

- **Mountains layout 1, Expedition: the start-unit stall.** In most games neither side has a structure or a unit (52 draws of 64, 100 of 128 sides without
  income). Same symptom as Ash Plains layout 2 (start units not placed when the sea is more than 8 cells from the start); the session 17 fix in
  `SkirmishStart.SpawnUnits` has to be checked against this case.
- **The 10-minute figure is not a clean income rate.** A side that is dead or a game that ended before 10 minutes contributes a low or zero value, so
  biomes with short games (glass_coast, average end tick 13 075) show a falling 10-minute number. s16_layout2.md read this as "fields run dry"; that is
  not shown by these numbers. Compare layout 2 at 3 and 5 minutes, or at 10 minutes over games that were still running.
- **Glass Coast Expedition** wins 55 v 5 from start 0 on layout 1: a map-side skew for that faction, not yet examined.

## After the fix (cloud, session 17, integ-s17 before PR #55)

Expedition v Expedition, tier 3, size 96, with the `SkirmishStart.SpawnUnits` second pass:

| Map | Games | Result (A / B / draws) | Games where a side built nothing |
|---|---:|---|---:|
| mountains layout 1, 4 seeds, 30 000 ticks | 8 | 1 / 3 / 4 | 0 |
| mountains layout 1, 16 seeds, 3 000 ticks (start check only) | 32 | - | 0 |
| ash_plains layout 2, 4 seeds, 30 000 ticks | 8 | 4 / 2 / 2 | 0 |
| ash_plains layout 2, 16 seeds, 3 000 ticks | 32 | - | 0 |
| mountains layout 2, 16 seeds, 3 000 ticks | 32 | - | 0 |
| glass_coast layout 2, 16 seeds, 3 000 ticks | 32 | - | 0 |
| glass_coast layout 1, 4 seeds, 30 000 ticks | 8 | 3 / 3 / 2 (start 0 won all 6 decisive) | 0 |

The Mountains layout 1 stall is gone in these games; half of the 30 000-tick games there still end as draws. There is no automated test for the
Mountains layout 1 case yet (the new tests cover Ash Plains layout 2). The six new tests were run against the code before the fix by the helper
that wrote them (6 of 6 failed: no Command Ship spawned, 0 structures by tick 3 000).
