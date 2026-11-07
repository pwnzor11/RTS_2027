# S13 AI ladder: make tier 4 beat tier 3 (M4-16, package s13-ladder)

Method: `Tools ai match --a tiered:A --b tiered:B --faction-a F --faction-b F --seeds N --size 96 --ticks 30000` (mirror, medium map). Screens: 16 seeds x 2 = 32 games;
final cells: 32 seeds x 2 = 64 games. Variants were run from scratch copies of the built tool with an edited `data/ai/difficulty.toml` (flags) or a temporary
env knob for the two thresholds (removed before the commit). Games are deterministic: the screen and the final 64-game run of the same config agree. Decisive % = A / (A + B).

## Screens: tier 4 (A) v tier 3 (B), 32 games, A / B / draw

| Variant (elite) | Authority | Directorate |
|---|---|---|
| baseline | 13 / 15 / 4 | 4 / 16 / 12 |
| superweapons 0 | 13 / 15 / 4 (identical: never built in Authority) | not run |
| air 0 | n/a (Authority has no air) | 14 / 11 / 7 |
| air tech waits for 30 units | n/a | 13 / 11 / 8 |
| scouting 0 + harass 0 | 16 / 13 / 3 | not run |
| air 30, harass party at 30 units | 17 / 12 / 3 | 6 / 11 / 15 (64 games: 14 / 20 / 30) |
| air 30, harass party at 20 | 14 / 15 / 3 | 8 / 11 / 13 |
| air 30, harass party at 14 (unchanged) | = baseline | 13 / 11 / 8 (64 games: 23 / 21 / 20) |
| commissar `retreat_health_pct` 25 -> 0, 5 v 4 | 16 / 11 / 5 | 12 / 5 / 15 (before: 12 / 27 / 25 at 64) |

## Reading

- Directorate 4 v 3: the cause is the air tech. Tier 4 is the first tier with `air`; the Airfield and its tier gate were added at an 8-unit army and took the army budget.
  The air flag stays on; the air producer now waits for 30 units (`ArmyBeforeAir`, same size as the s12 tech and adapt waits).
- Authority 4 v 3: no air, no superweapon built. The other tier-4 spenders are the scout and the raiding party. The party (3 fastest units) was formed at a 14-unit army and
  weakened the wave; at 30 units Authority is 30 / 24 / 10 against 24 / 33 / 7. Directorate raiders do pay (30 units: 14 / 20 / 30 against 23 / 21 / 20 at 14), so the
  threshold is 14 for Directorate and 30 otherwise (`TierPlanner.HarassMinArmy`).
- With tier 4 repaired, tier 5 was exposed: its `retreat_health_pct = 25` (the lever s12 removed from elite) now cost it the 5 v 4 cells (Directorate 12 / 27 / 25). Set to 0.
- Unchanged: superweapon (never reached in these games), scouting (kept), LeftBehind, attack thresholds, anything under data/rules.

## Before -> after (64 games per cell, A = higher tier / B / draw)
Before = the "After" column of s12_tier5_diagnosis.md.

| Cell | Before | After | Decisive % before -> after |
|---|---|---|---|
| Authority 5 v 1 | 31 / 25 / 8 | 41 / 18 / 5 | 55 -> 69 |
| Authority 5 v 4 | 30 / 21 / 13 | 29 / 25 / 10 | 59 -> 54 |
| Authority 4 v 3 | 24 / 33 / 7 | 30 / 24 / 10 | 42 -> 56 |
| Authority 3 v 2 | 28 / 26 / 10 | 28 / 26 / 10 | 52 -> 52 (unchanged, no tier-3 change) |
| Directorate 5 v 1 | 7 / 11 / 46 | 17 / 8 / 39 | 39 -> 68 |
| Directorate 5 v 4 | 18 / 23 / 23 | 29 / 15 / 20 | 44 -> 66 |
| Directorate 4 v 3 | 12 / 33 / 19 | 23 / 21 / 20 | 27 -> 52 |
| Directorate 3 v 2 | 34 / 3 / 27 | 34 / 3 / 27 | 92 -> 92 (unchanged) |

Draws (Directorate): 20 (4 v 3), 20 (5 v 4), 39 (5 v 1), 27 (3 v 2). Authority: 10, 10, 5, 10.

## Result
Every higher tier is >= 50 % decisive against the tier below in both factions. The margins are thin: at 64 games the 95 % noise band is about +-13 points, so Directorate 4 v 3 (52 %)
and Authority 5 v 4 (54 %) are "not inverted", not "proven ahead". The s12 target of 90 % for 5 v 1 is not reached (69 % / 68 %).

## Draws
Three traces (Directorate 4 v 3 seed 1, 5 v 1 seeds 2 and 3) were all decided, at ticks 14 552, 19 516 and 25 564, so no draw game was traced. What the traces show is a slow finish:
the winner holds a 24-38 unit army and 20+ structures long before the end while the loser keeps a few buildings (6-13 at t18000-24000) that the waves clear slowly. Games that
start that clean-up late hit the 30 000-tick limit as draws. This is a reading from decided games, not a confirmed cause.

## Still open
- Higher tiers win by small margins; 5 v 1 Directorate is draw-heavy (39 of 64). A mop-up behaviour (attack-move to the last known structures once the enemy has no army) is the likely lever.
- Why Directorate raiders help and Authority raiders hurt (unit speeds/cost of the "fastest" picks) is unmeasured; the faction split in `HarassMinArmy` is a measured patch, not a design.
- Tier 3 v 2 Authority is 52 %: tier 3 adds almost nothing there.
- Superweapon behaviour was not exercised in any of these games.
