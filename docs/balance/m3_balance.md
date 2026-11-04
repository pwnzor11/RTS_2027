# M3 balance pass (M3-14)

Method: headless `Tools ai match` on the Release build, real data in `data/rules`, map size 48, 14,000 ticks, 8 seeds x 2 sides = 16 games per cell
(start corners and slots alternate, D-063). Tier AI id `tiered`. One tick = 0.05 s. A "win" is decisive; draws (tick limit) are decided by remaining health in the tiebreak line.
Three tuning rounds were run (budget), then stopped.

```
Tools ai match --a tiered:<t> --b tiered:<t> --faction-b directorate --seeds 8 --ticks 14000 --size 48      (A = Authority, B = Directorate)
Tools ai match --a tiered:<t> --b tiered:<t-1> --faction-a <f> --faction-b <f> --seeds 8 --ticks 14000 --size 48
dotnet test tests/Sim.Tests --filter FullyQualifiedName~TunnelTimeTests
```
New in `src/Tools`: `ai match --faction-a / --faction-b authority|directorate` (cross-faction matches; default authority for both).

## 1. Tunnel time saved (met)
`tests/Sim.Tests/Balance/TunnelTimeTests.cs`: base to a point 40 cells away on a flat map, entrances 3 cells from the start and 5 from the target; walking vs two entrances. Pins 30-50 %.

| Group | Walking | Tunnel before | Saved before | Tunnel after | Saved after |
|---|---:|---:|---:|---:|---:|
| 4 conscripts via Hatches | 728 | 288 | 60 % | 389 | 46 % |
| Rocket truck (M) via Bore Heads | 457 | 184 | 59 % | 265 | 42 % |

Changed: `[tunnels] transit_ticks` 40 -> 100 (game.toml); Hatch class `ticks_per_point` 20 -> 35 (dir_hatch, dir_muster_hatch); Bore class `ticks_per_point` 13 -> 20 (dir_bore_head, dir_shaft, dir_assembly_bore, dir_smelter_vent). All carry `# (tune) M3-14`.
Test pins updated: `DirectorateDataTests.Entrances_...WithThroughputTicks` (20/13 -> 35/20).

## 2. Faction balance (NOT met)
Authority (A) vs Directorate (B) at equal tier, 16 games per cell.

| Tier | Before: A / B / draw | Before: draws ahead A / B | After: A / B / draw | After: draws ahead A / B |
|---|---|---|---|---|
| 1 | 0 / 9 / 7 | 2 / 5 | 0 / 10 / 6 | 2 / 4 |
| 3 | 0 / 1 / 15 | 8 / 7 | 0 / 2 / 14 | 5 / 9 |
| 5 | 0 / 0 / 16 | 10 / 6 | 0 / 0 / 16 | 10 / 6 |

(The small differences between before and after come from the slower tunnels and are within the noise of 16 games.)
Reading: the Directorate wins every decisive tier-1 game (Authority 0 %), by tier 3-5 games are mostly stalemates at the limit. Authority leads on health at tier 5 (10 of 16), Directorate at tier 3.

Tried and reverted (round 1-3), each with the duel/match evidence:
- dir_conscript cost 80 -> 100 -> 130, dir_rifle damage 6 -> 5, dir_scraper cost 600 -> 700, dir_rocket_truck cost 800 -> 900, dir_rocket_salvo damage 90 -> 60.
  Best result: tier 1 A/B/draw 2/5/9 (12 % Authority), tier 5 3/0/13. Never inside 40-60 %, tier 3 stayed 0-6 %.
- Reverted because the combined change stopped the Directorate Recruit AI from ever using the network inside the 9,000-tick window of `DirectorateRecruitTests.DirectorateBuildsHarvestsDigsAndUsesTheNetwork` (fails with `TransitUnits = 0`; also at 12,000 and 16,000 ticks), and I did not want to weaken that test. With the old costs and the new tunnel numbers it passes.
- Evidence the Directorate edge is economy/structure, not unit stats: duels with equal cost are close (4 rocket trucks beat 4 tanks at 90 salvo damage; lose at 60), but Directorate at tier 1 out-builds Authority (structures 13-17 vs 8-10, units 100-160 vs 17-90 per game). A later pass should look at the Authority AI's build order against Smelter Vent / free scraper income, not at unit numbers.
So: no unit/cost/damage change from this pass is shipped. Faction win rates are an open item.

## 3. Tier ladder (NOT met)
Row tier vs the tier below, equal faction, 16 games per cell. Only `attack_threshold` was changed: regular 12 -> 10, veteran 10 -> 8, elite 9 -> 7, commissar 8 -> 6 (`# (tune) M3-14`).

| Pairing | Authority before (win %) | Authority after | Directorate before | Directorate after |
|---|---|---|---|---|
| t2 > t1 | 43 % | 50 % | 0 % | 6 % |
| t3 > t2 | 25 % | 25 % | 12 % | 6 % |
| t4 > t3 | 25 % | 18 % | 6 % | 0 % |
| t5 > t4 | 25 % | 12 % | 6 % | 0 % |

Decisive wins only; draws are 30-100 % of the games. With draws ahead on health: Authority after t2>t1 62 %, t3>t2 43 %, t4>t3 50 %, t5>t4 56 %; Directorate after t2>t1 68 %, t3>t2 75 %, t4>t3 50 %, t5>t4 62 %.
No pairing meets 60 % on decisive wins; the Authority t2>t1 cell is the closest. The earlier threshold change helped t2 and hurt t4/t5; tiers 4-5 still spend army on scouts, raids and retreats (see ai_matrix_m3-11.md). The attack_threshold edits are kept as the best of the three rounds for tiers 2-3 and are a wash for 4-5; the Directorate ladder is dominated by draws because games rarely resolve inside 14,000 ticks. Needs a longer tick limit or a stronger AI push, not more numbers.

## 4. D-089 / Q-34 assumed values
| Value | Verdict |
|---|---|
| Radiation 10 HP/s per full cell, resist-scaled, none on buildings or underground units | kept: data is `damage_permille = 100` every 20 ticks scaled by resist 600 permille for infantry; no match showed radiation deciding a game (RadMoves in the AI tests are 0-11) |
| Directorate start kit = dir_shaft + 4 conscripts | kept: Directorate tier 1 reaches income and structures at least as fast as Authority (it out-builds it); a smaller kit would widen the gap the wrong way |
| Engineers capture below 50 % HP | kept: no balance evidence either way (AI does not use capture) |
| Aura stacking = best single source | kept: not exercised by the matrices |
| One team member's grouting covers the team | kept: skirmish matrices are 1v1 |
| Land-train deploy one-way | kept: no evidence |
| Sunrise blast radius 5, 700 damage, enemy not told | kept: elite/commissar Directorate games did not end by superweapon within 14,000 ticks; charge 3,000 ticks left as is |
| Keys T / D, music folder, sfxr CC0 | not balance items, no change |

## 5. Every changed number
- game.toml: `[tunnels] transit_ticks` 40 -> 100.
- buildings.toml: dir_hatch, dir_muster_hatch `ticks_per_point` 20 -> 35; dir_bore_head, dir_shaft, dir_assembly_bore, dir_smelter_vent `ticks_per_point` 13 -> 20.
- difficulty.toml: `attack_threshold` regular 12 -> 10, veteran 10 -> 8, elite 9 -> 7, commissar 8 -> 6.
- Tests: DirectorateDataTests pins 20/13 -> 35/20.

## 6. Remaining misses
Faction balance at tiers 1, 3, 5 (Directorate far ahead at 1; stalemates 3 and 5); ladder t3>t2, t4>t3, t5>t4 (both factions) and Directorate t2>t1. Next steps: (a) run 25,000-tick games so draws resolve, (b) rebalance the Authority AI economy against the Directorate, then retune costs once `DirectorateRecruitTests` is made robust to the first-network-ride time.
