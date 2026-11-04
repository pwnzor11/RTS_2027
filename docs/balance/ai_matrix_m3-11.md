# AI tier matrix, M3-11

Tiers 2-5 run on the Recruit framework (`tiered` AI id, same AI as `recruit`); every capability is gated on a flag in `data/ai/difficulty.toml`.
Command: `Tools ai matrix --ai tiered --faction <authority|directorate> --seeds 4 --ticks 14000 --size 48` (4 seeds x 2 sides = 8 games per cell, mirror faction).
One tuning round was done between the first run and the numbers below (HarassManager.MinArmy 8->14, ScoutManager.MinArmy 3->6, group retreat margin 20->10 percent, retreat_health_pct elite 30->25 and commissar 35->25).

## Result: the 60 percent goal is NOT met everywhere

"Win %" counts only decisive wins. Most games reach the tick limit with both sides alive (the games are long), so the third table, which also credits a draw to the side with more remaining health, is the better reading. Cells are noisy (8 games: one game = 12.5 points).

Adjacent-tier result (row beats the tier below), win % including draws ahead on health:

| pairing | Authority | Directorate |
|---|---:|---:|
| regular vs recruit | 50% (miss) | 62% |
| veteran vs regular | 62% | 75% |
| elite vs veteran | 50% (miss) | 12% (miss) |
| commissar vs elite | 62% | 50% (miss) |

Misses: Authority regular/recruit and elite/veteran; Directorate elite/veteran (worst) and commissar/elite. Likely cause: tier 4+ spends on scouts, raiders and the retreat rule, which costs army strength in a 48-cell map with few engagements; veteran's two-pronged waves and enemy-aware mix are the strongest single gains. Not tuned further (one round by instruction).

## Authority

Row tier's win % against the column tier. 4 seeds x 2 sides per cell, 14000 ticks per game, map size 48. Draws count as non-wins.

## Win %

| row \ col | recruit | regular | veteran | elite | commissar |
|---|---:|---:|---:|---:|---:|
| **recruit** | 50% | 25% | 37% | 25% | 25% |
| **regular** | 50% | 25% | 12% | 0% | 12% |
| **veteran** | 25% | 37% | 37% | 50% | 50% |
| **elite** | 37% | 50% | 50% | 37% | 25% |
| **commissar** | 50% | 37% | 12% | 25% | 0% |

## Draw %

| row \ col | recruit | regular | veteran | elite | commissar |
|---|---:|---:|---:|---:|---:|
| **recruit** | 0% | 25% | 37% | 37% | 25% |
| **regular** | 25% | 50% | 50% | 50% | 50% |
| **veteran** | 37% | 50% | 25% | 0% | 37% |
| **elite** | 37% | 50% | 0% | 25% | 50% |
| **commissar** | 25% | 50% | 37% | 50% | 100% |

Draws are decided by remaining health (both sides alive at the tick limit); the table below counts a win plus every draw in which the row tier is ahead.

## Win % incl. draws ahead on health

| row \ col | recruit | regular | veteran | elite | commissar |
|---|---:|---:|---:|---:|---:|
| **recruit** | 50% | 50% | 62% | 25% | 25% |
| **regular** | 50% | 50% | 37% | 25% | 25% |
| **veteran** | 37% | 62% | 50% | 50% | 50% |
| **elite** | 75% | 75% | 50% | 50% | 37% |
| **commissar** | 75% | 75% | 50% | 62% | 50% |


## Directorate

Row tier's win % against the column tier. 4 seeds x 2 sides per cell, 14000 ticks per game, map size 48. Draws count as non-wins.

## Win %

| row \ col | recruit | regular | veteran | elite | commissar |
|---|---:|---:|---:|---:|---:|
| **recruit** | 12% | 0% | 0% | 0% | 0% |
| **regular** | 0% | 0% | 0% | 0% | 0% |
| **veteran** | 37% | 12% | 0% | 12% | 0% |
| **elite** | 25% | 25% | 0% | 0% | 0% |
| **commissar** | 12% | 0% | 0% | 0% | 0% |

## Draw %

| row \ col | recruit | regular | veteran | elite | commissar |
|---|---:|---:|---:|---:|---:|
| **recruit** | 75% | 100% | 62% | 75% | 87% |
| **regular** | 100% | 100% | 87% | 75% | 100% |
| **veteran** | 62% | 87% | 100% | 87% | 100% |
| **elite** | 75% | 75% | 87% | 100% | 100% |
| **commissar** | 87% | 100% | 100% | 100% | 100% |

Draws are decided by remaining health (both sides alive at the tick limit); the table below counts a win plus every draw in which the row tier is ahead.

## Win % incl. draws ahead on health

| row \ col | recruit | regular | veteran | elite | commissar |
|---|---:|---:|---:|---:|---:|
| **recruit** | 50% | 37% | 0% | 25% | 25% |
| **regular** | 62% | 50% | 25% | 37% | 12% |
| **veteran** | 100% | 75% | 50% | 87% | 75% |
| **elite** | 75% | 62% | 12% | 50% | 50% |
| **commissar** | 75% | 87% | 25% | 50% | 50% |


## Notes
- Directorate aircraft (dirigible) are built in the vehicle lane: the sim has no aircraft production lane. Authority has no air tech, so the `air` flag does nothing for it.
- The superweapon blast radius (5) is hard-coded in SuperweaponManager (`// PENDING M3-21: expose blast radius in view`).
- Tier 1 keeps the original defend-when-hit rule; `respond_to_attacks` adds the home-guard response.
