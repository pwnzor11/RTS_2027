# M4 AI: N players, tier climbing, garrisons, finishing games (M4-12)

Method: headless `Tools ai match` / `ai matrix --players` on the Release build, real data in `data/rules`, `tiered` AI.
2-player cells: 8 seeds x 2 (starts swapped) = 16 games, map size 56, 25 000 ticks (20 min 50 s). One tick = 0.05 s.
Only the AI was tuned (src/AI, data/ai/difficulty.toml); no rules numbers changed (that is M4-16).

```
Tools ai timeline --a tiered:3 --b tiered:3 --faction-a directorate --faction-b directorate --seed 1 --ticks 25000 --size 56 --trace 2500 --dump 1
Tools ai ffa --players 4 --seed 2 --rot 2 --size 64 --trace 2500
Tools ai match --a tiered:<t> --b tiered:<t-1> --faction-a <f> --faction-b <f> --seeds 8 --ticks 25000 --size 56
Tools ai match --a tiered:<t> --b tiered:<t> --faction-a authority --faction-b directorate --seeds 8 --ticks 25000 --size 56
Tools ai matrix --players 4 --biome all --size m --seeds 2 --ticks 25000 --tiers 3
```

## 1. Why games stalled (diagnosis)

Baseline before any change (veteran, size 56, 25 000 ticks, 3 seeds x 2): Authority mirror 6/6 decisive; **Directorate mirror 0/6
decisive (all draws)**; Authority vs Directorate 2 B wins, 2 draws, 0 Authority wins. Stalled games were traced with
`ai timeline --trace --dump` (per-slot army, structures, funds; at the end every unit with mission, attack-move goal and target).
Five distinct causes, most games had two or three at once:

| # | Cause | Evidence (traced game) | Fix (AI only) |
|---|---|---|---|
| 1 | **No income, no way back.** The only harvester dies (raided, or caught by a wave), the funds do not cover a new one, nothing else brings money in. The side freezes with a half-built structure "stalled" in the queue. | Directorate mirror seed 1: both scrapers dead by tick 6000, both sides `cash 0+0` with `dir_smelter_vent(stalled)` for the remaining 19 000 ticks. | `EconomyRescue` (flag replace_harvesters): cancels the item in production for its refund, then sells the dearest spare structure (never yard, refinery, factory, last power plant, an occupied garrison) until the harvester (or a refinery, which brings one) is affordable; holds every other purchase meanwhile. Armed only after a harvester was ever owned (both factions start without one). |
| 2 | **Two survivors cannot kill a building that repairs for free.** The last attackers sit at the enemy yard; it heals (yard cost 0 = free repair) faster than two damaged rocket trucks hurt it. The wave never "ends" because its roster has not shrunk below a quarter, so **every unit built later waited at the rally for the rest of the game**. | Same game: two P0 rocket trucks on AttackMove at the P1 Shaft from tick 6000 to 25 000, its health swinging 730-900; Authority vs Directorate seed 1: 197 Authority units idle at home while 2 tanks shelled the Shaft. | Regroup: during an assault, once the idle units at home reach the next wave's threshold, the old wave is ended and a new one (survivors + fresh units) launches. Stale-assault end: if the known buildings of the target lose no health for 2 400 ticks the wave is ended, the goal is avoided for 6 000 ticks and the next wave is due at once. |
| 3 | **Unreachable attack-move goals.** A goal on the footprint of one of our own buildings (or a cell boxed in by our own base) is never reached, so the unit never goes idle and no manager re-orders it. | Directorate mirror seed 3: P1's later wave (`roster 82`) stood still with goal (3,3) = the enemy yard's top-left cell long after the yard died; 8 units on AttackMove frozen for 15 000 ticks. | Stuck watchdog: a busy army unit that has not changed cell for 400 ticks with no visible enemy within 8 cells gets Stop (then a seeded nearby Move if it sticks again). Units entering a tunnel, garrison or deploying are exempt. |
| 4 | **The tier gate starves.** The 2 500-credit tier-3 gate was started, then units bought with every incoming credit (the build order's reserve only counted the next structure, not the one in production), so the gate sat "stalled" at 30 % for the rest of the game. | `EliteClimbsToTierThree` (Directorate): `dir_committee_hall@303(stalled)` from tick ~9000 to 14 400. | `BuildOrderManager.ReserveInProgress` (tier 2+): the unit reserve includes the unpaid part of structures in production. |
| 5 | **The map is mined out.** All fields empty, both sides with harvesters but no income, a garrison of 5 and a handful of units each, nobody attacks because no wave threshold is reached. | Directorate mirror seed 5: `field at start 640, at end 0`, both sides `cash 0` from tick ~17 000. | Broke mode: no harvester unload for 2 400 ticks = out of income: cancel structures in production, build no more, sell spare non-production structures, empty own garrisons (`Unload`) and launch waves at 3 units. **Only partly fixed**: when both sides are mined out with equal remnants they trade them and the game can still end in a draw (see §4). |

Not causes: tunnels and walls (no traced stall had a unit stuck at a wall or in the network; Directorate transit units arrived),
attack waves too small as such (waves were big; they stopped coming because of #2), radiation.
Also found and fixed on the way: harvester damage from radiation was treated as an attack (the home guard ran to it every few seconds);
a key collision between building and harvester health memory (slot keys without the kind bits) made the defend rule fire on
nothing.

## 2. N players (2-4)

- Target choice: `MilitaryManager` keeps a target PLAYER, re-chosen every 600 ticks (30 s, design 08 §2) or when nothing of it is known:
  the enemy player whose nearest known (live or ghost) building is closest to our start, fewer known buildings counted nearer (weakest
  first on near-ties), a seeded 0-7 tie-break when there are several candidates. Waves, prongs and the faction tactics' goal use only
  that player's buildings. Fog-honest: only `CopyKnownEnemyBuildings`.
- Scouting for multiple starts: `AiContext.StartPositions` (public map data, filled by `AiDriver`); `StartRoute` lists every other
  start nearest first (equally near ones in seeded order), then centre and corners. With 2 starts the M3 route is kept (2-player games
  draw the same random numbers as before).
- `AiMatchRunner.PlayN` (2-4 sides, free-for-all, rotation of sides over starts), `SideTracker` sized by player count.
- `Tools ai matrix --players N [--biome rust_belt|glass_coast|mountains|ash_plains|all] [--size s|m|l|<cells>] [--faction authority|directorate|mixed]`:
  N rotations per seed, per biome: decisive / draws / average end tick, and per START the win share and the outlast share.
- **M4-02 is not merged in this worktree**: `MapGenerator.Generate(MapGenParams)` is still the stub (2 starts, biome ignored). The
  matrix detects this and uses a stand-in map (the generated map's top-left quadrant mirrored into all four corners, 4 starts) and
  prints a note. The biome columns therefore show the same terrain until M4-02 lands; rerun the matrix after the merge.

## 3. Tier climbing, new units, garrisons

- Tier climbing (flag `use_upgrades`, now on from veteran = tier-3 difficulty up): the tier-2 gate (`auth_engineering` /
  `dir_isotope_works`) is added to the build order at tick 3 600, the tier-3 gate (`auth_signals_hq` / `dir_committee_hall`) at tick
  8 400 once the army has 12 units (and the player is at tier 2). A lost gate is rebuilt by the build order. Upgrades: the existing cheapest-first research
  manager now finds tier-2/3 upgrades because the gates are built (from a 12-unit army).
- Unit bags grow at tier 2 and 3 (tier-2+ profiles; the Recruit keeps its M3 mix): Authority + Marksman x2, Seismic Mortar, Foam Layer,
  Tank (T2), Bulwark x2, Howitzer (T3); Directorate + Sapper, Volunteer, Commissar, Delver x2, Ash Sower (T2), Land-train, Reactor
  Crawler (T3). Locked types are skipped (both factions), so a lost gate does not stall a lane. Enemy-aware mix still applies.
- Aircraft lane (flag `air`): Authority Airfield, then the highest-tier ground attacker (Heavy Bomber at T3); the Interceptor only once
  enemy aircraft were seen. Directorate: Great Dirigible at T3, Dirigible before. Anti-air: Authority Flak once enemy aircraft were
  seen. Superweapon (flag `superweapons`): Atomic Battery / Sunrise once at tier 3.
- Garrisons (flag `respond_to_attacks`, tier 2+): `GarrisonManager` fills the faction's Blockhouse / Redoubt (added to the build order
  at a 16-unit army) and empty neutral ruins in view within 14 cells of the yard with idle armed infantry, keeping 12 in the army, at most
  2 orders per decision. Occupancy from `PlayerView.OccupantsOf` (own) and our own order memory (walking in). Broke mode empties them.
- Not done: Blockhouse/Redoubt garrisons are never deliberately emptied to reinforce a defence (only in broke mode); engineers
  (auth_engineer / dir_assessor) and the Carrier / Mole transports are not used by the AI.

## 4. Results

Final numbers (after two tuning rounds; raw output in the worktree's scratch/measure_r3, git-ignored). 16 games per 2-player cell,
size 56, 25 000 ticks. "Win %" = decisive wins of the row side; draws are games still running at 25 000 ticks.

### 4.1 Tier ladder (target: each tier beats the one below in >= 60 %) — NOT met

| Pairing | Authority A / B / draw | A win % | Directorate A / B / draw | A win % |
|---|---|---:|---|---:|
| regular > recruit | 8 / 8 / 0 | 50 % | 3 / 4 / 9 | 18 % |
| veteran > regular | 4 / 10 / 2 | 25 % | 5 / 5 / 6 | 31 % |
| elite > veteran | 6 / 10 / 0 | 37 % | 2 / 6 / 8 | 12 % |
| commissar > elite | 5 / 8 / 3 | 31 % | 6 / 5 / 5 | 37 % |

M3 (14 000 ticks) for comparison: Authority 50 / 25 / 18 / 12 %, Directorate 6 / 6 / 0 / 0 %, with 30-100 % draws.
Draws: Authority 0-19 % (was 30-100 %), Directorate 31-56 %.

### 4.2 Cross-faction at equal tier (target 40-60 % Authority) — met at tier 3 only

| Tier | Authority / Directorate / draw | Authority win % | Draws ahead (A / D) |
|---|---|---:|---|
| 1 recruit | 2 / 7 / 7 | 12 % | 1 / 6 |
| 2 regular | 6 / 5 / 5 | 37 % | 3 / 2 |
| 3 veteran | 7 / 8 / 1 | 43 % | 0 / 1 |
| 4 elite | 6 / 5 / 5 | 37 % | 4 / 1 |
| 5 commissar | 4 / 7 / 5 | 25 % | 1 / 4 |

M3: Authority won 0 decisive games at every tier. Counting draws ahead on health, tier 2 is 56 %, tier 3 43 %, tier 4 62 %.

### 4.3 Four players (`ai matrix --players 4 --faction mixed --size m --seeds 2 --tiers 3`) — NOT met

Each biome: 8 games, 2 decisive, 6 draws, average end tick 23 988; start win / outlast %: 0/41, 12/83, 12/45, 0/29.
All four biome rows are identical because M4-02's generator is not merged here (stand-in map, biome unused). On a 96-cell map
with 4 players most games are 2-3 survivors at 25 000 ticks: the AI clears its nearest neighbour, then the long walk plus
the regroup threshold leaves too little time. The size-64 stand-in (rust_belt, 3 seeds x 4) was 8-11 decisive of 12.

**Regression found at the end:** the Directorate veteran mirror (not in the tables above) was 14/16 decisive after round 1 and is
1/8 decisive after veteran started climbing tiers (round 2). Traced (seed 1): not a freeze; the stronger side grinds the other to
3 buildings by 25 000 ticks with many small waves staged 16 cells short. Finishing a won game faster (stage closer, stop
re-staging when the enemy has no army) is the next AI step.

### 4.4 Other §8 checks
- Tier 3 by 15 min at tier >= 3 difficulty: `VeteranClimbsToTierThreeAndBuildsTierTypes` (both factions, rich economy): Authority
  tier 3 at tick 3 500, Directorate inside the 18 000-tick limit after the reserve fix; in matches the gates come at 3 min / 7 min+.
- Garrisons at least one building when it has one: `RegularFillsItsOwnGarrisonWithIdleInfantry` (both factions),
  `RegularOccupiesAnEmptyNeutralRuinNearItsBase`.
- Fog-honest: only PlayerView (CopyOwn / CopyVisible / CopyKnownEnemyBuildings / OccupantsOf / Tier) and the public map start list.

### 4.5 Reading
- Games now mostly end: the Authority side resolves 81-100 % of its games, Directorate 44-69 %. The remaining Directorate draws are
  mostly mined-out maps (cause 5) and even trades of the last units.
- **Higher tiers still do not reliably beat lower ones.** Each extra capability costs money or units up front (tier gates 500 + 2 500,
  research 800-2 500 per upgrade, scouts, raiders, retreats), and on a 56-cell map a lower tier that spends all of it on units often
  wins the first big fight. Round 2 (tier-3 gate needs time AND a 12-unit army; research waits for 12 units) lifted Authority t3>t2
  from 12 to 25 % and t4>t3 from 31 to 37 %. A real fix is to make higher tiers spend smarter (e.g. no tech while the enemy army is
  bigger), which needs more than one evening's tuning; not attempted.
- **Start bias:** in the 2-player cells start 0 (4,4) won 2-11x more often than the mirror start (e.g. cross t4: 11 vs 0). That is
  not an AI effect (both slots alternate) but the generated maps / sim; it inflates noise in every cell. Report it to M4-16.
- Tier 1 Directorate still beats Tier 1 Authority (12 %): the Recruit tier keeps its M3 behaviour on purpose.


## 5. Every AI change (all `(tune)` constants live in src/AI/Recruit)

- difficulty.toml: veteran `use_upgrades` 0 -> 1 (tier climbing at tier-3 difficulty, design 08 §8). No other value changed.
- MilitaryManager: TargetReviewTicks 600, StaleAssaultTicks 2400, AvoidTicks 6000, StuckTicks 400; a wave on the attack is ended
  (regroup) when the idle units at home reach the next wave's threshold (a first version trickled them in one by one, which lost).
- TierPlanner: Tier2GateTick 3600, Tier3GateTick 8400 AND ArmyBeforeTier3 12, ArmyBeforeGarrison 16.
- UpgradeManager.MinArmy 6 -> 12.
- GarrisonManager: RadiusCells 14, KeepInArmy 12 (a 4-soldier Blockhouse opening cost regular its games against recruit). EconomyRescue: StalledIncomeTicks 2400.
- New behaviour is gated on existing flags (DifficultyLoader is in src/Data, outside this package): respond_to_attacks,
  replace_harvesters, use_upgrades (see the comment block in difficulty.toml). Tier 1 (Recruit) behaves as in M3.
