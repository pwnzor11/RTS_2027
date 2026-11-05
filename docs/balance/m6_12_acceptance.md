# M6-12 acceptance, part 1: soak, golden replay and early-game income

Branch `wp/acc` (based on `integ-s10`: supply on, veterancy on, VET-6 AI). Measurement and tests only; no game, AI or data change.
All numbers below come from the runs described here (216 soak games, 180 income games). Nothing is estimated.

## Method

Tools (all new, no tool or game code changed):
- `tests/AI.Tests/Soak/SoakTests.cs`: env-gated soak driver (`RTS_SOAK=1`). It plays `AiMatchRunner.Play` games exactly as `ai match` does
  (tiered AI, economy start, `MapGenerator.Generate(size, seed)`, game seed `seed*16+game`, even seeds also swap the start corners), records every
  game into memory, re-runs the recording with `ReplayPlayer` and compares every recorded hash plus the final hash (desync check), and samples both
  sides every 20 ticks. Without `RTS_SOAK` it runs one short Left Behind vs Directorate game and asserts no desync (a normal test).
- `tests/AI.Tests/Soak/IncomeSampleTests.cs`: env-gated income sampler (`RTS_INCOME=1`); reads `sim.Stats.CreditsHarvested` (the stats counter:
  harvest unloads, salvage payouts and raid plunder) of both slots at ticks 3600, 6000 and 12000 (20 ticks = 1 s, so minutes 3, 5, 10).
- `tools/measure/acc.sh`: resumable driver that calls the test binary once per cell (`soak` and `income` modes, two lanes).
- Why not `ai match` for the soak: `ai match --record-dir` fails for any non-Authority side (the replay file name contains the label
  `tiered:veteran/leftbehind`, whose `/` is a directory separator; see QUESTIONS in the hand-back) and the tool prints no desync, stuck or
  exception column. A one-off check with the tool (`ai match ... --starts slot --record-dir` with pre-made directories, then `replay verify --map`)
  gave `OK` for a Left Behind vs Directorate game; the soak table uses the in-process check for all 216 games.

Commands (Release builds, run with the built binary, not under the dotnet lock):
```
flock /home/claude/wt/dotnet.lock dotnet build tests/AI.Tests -c Release -m:1
tools/measure/acc.sh soak 0 520 & tools/measure/acc.sh soak 1 520 & wait     # repeated until all 54 cells exist
tools/measure/acc.sh income 0 480 & tools/measure/acc.sh income 1 480 & wait # repeated until all 9 cells exist
# one cell by hand, e.g.:
RTS_SOAK=1 RTS_SOAK_PAIRS=leftbehind-directorate RTS_SOAK_TIERS=5 RTS_SOAK_SIZES=96 RTS_SOAK_SEEDS=1-2 RTS_SOAK_TICKS=30000 RTS_SOAK_OUT=/abs/path/out.txt \
  /usr/bin/dotnet vstest tests/AI.Tests/bin/Release/net10.0/AI.Tests.dll --Tests:ALeftBehindGameReplaysWithoutDesync
```
Soak cell = faction pair x map size x tier, 4 games = seeds 1-2 x (A in slot 0, A in slot 1); both sides use the same tier; 30 000 ticks
(25 minutes) limit; a game still running at the limit is a draw. Sizes are the generator's cells per side: small 64, medium 96, large 128
(`MapGenerator.CellsFor`). Pair "A-B": A is the first-named faction. In a mirror pair A and B are only the slot order, so A/B wins carry no balance meaning there.

Checks recorded per game:
- **Desync**: the in-memory replay re-run mismatches any recorded hash (every 16 ticks) or the final hash.
- **Exceptions**: any exception thrown by the game or the replay re-run (caught, logged, game counted).
- **Idle packed rig**: longest run (ticks) in which one side had a packed rig that was idle (not packing/unpacking) at every 20-tick sample
  (the same test as `ALeftBehindAiPlaysAFullGameWithoutAStuckRig`, which allows < 600 = 30 s).
- **Stuck units** (my own heuristic, an upper bound): mobile units that kept a Move or AttackMove order in the same cell for 1200 ticks (60 s).
  Units waiting in a jam, or still sitting on an order when the game ended, are counted too; it is a pointer for the lead, not a verdict.
- **Never ends**: draws at the 30 000 tick limit.

Income: mirror games (faction vs the same faction), tier-2 AI, seeds 1-10 x (slot order 0/1) = 20 games per cell, same seeds for all three factions
and for all sizes, 12 000 ticks. "/min" = CreditsHarvested of that period divided by the minutes, averaged over both sides of every game that
reached the sample tick (so up to 40 side-samples per cell; the Games column gives the exact counts and how many games ended before minute 10).
"vs mean" compares each faction to the mean of the three factions in the same column. Shipment share is n/a: only the Expedition has shipments.
Differences from the 11_m7_plan section 4 method: three factions (no Expedition), and mirror pairings.

## Soak results
| Pair (A-B) | Size | Tier | Games | A wins | B wins | Draws | Avg end tick | Desyncs | Max idle rig (ticks) | Stuck units (A/B) | Exceptions |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Authority-Authority | 64 | 1 | 4 | 2 | 2 | 0 | 13005 | 0 | 0 | 0/0 | 0 |
| Authority-Authority | 64 | 3 | 4 | 2 | 1 | 1 | 22194 | 0 | 0 | 10/1 | 0 |
| Authority-Authority | 64 | 5 | 4 | 1 | 3 | 0 | 11688 | 0 | 0 | 0/18 | 0 |
| Authority-Authority | 96 | 1 | 4 | 0 | 4 | 0 | 14970 | 0 | 0 | 0/0 | 0 |
| Authority-Authority | 96 | 3 | 4 | 1 | 3 | 0 | 15104 | 0 | 0 | 0/3 | 0 |
| Authority-Authority | 96 | 5 | 4 | 3 | 1 | 0 | 18684 | 0 | 0 | 4/8 | 0 |
| Authority-Authority | 128 | 1 | 4 | 1 | 3 | 0 | 18200 | 0 | 0 | 0/0 | 0 |
| Authority-Authority | 128 | 3 | 4 | 0 | 3 | 1 | 22943 | 0 | 0 | 0/12 | 0 |
| Authority-Authority | 128 | 5 | 4 | 1 | 0 | 3 | 28963 | 0 | 0 | 9/8 | 0 |
| Directorate-Directorate | 64 | 1 | 4 | 2 | 2 | 0 | 19249 | 0 | 0 | 0/1 | 0 |
| Directorate-Directorate | 64 | 3 | 4 | 2 | 2 | 0 | 19614 | 0 | 0 | 12/4 | 0 |
| Directorate-Directorate | 64 | 5 | 4 | 1 | 2 | 1 | 22568 | 0 | 0 | 5/12 | 0 |
| Directorate-Directorate | 96 | 1 | 4 | 1 | 2 | 1 | 24592 | 0 | 0 | 2/0 | 0 |
| Directorate-Directorate | 96 | 3 | 4 | 2 | 2 | 0 | 24404 | 0 | 0 | 4/4 | 0 |
| Directorate-Directorate | 96 | 5 | 4 | 2 | 1 | 1 | 23331 | 0 | 0 | 4/5 | 0 |
| Directorate-Directorate | 128 | 1 | 4 | 0 | 2 | 2 | 26442 | 0 | 0 | 3/2 | 0 |
| Directorate-Directorate | 128 | 3 | 4 | 1 | 1 | 2 | 24586 | 0 | 0 | 11/2 | 0 |
| Directorate-Directorate | 128 | 5 | 4 | 3 | 0 | 1 | 27359 | 0 | 0 | 34/7 | 0 |
| LeftBehind-LeftBehind | 64 | 1 | 4 | 1 | 2 | 1 | 17399 | 0 | 0 | 17/13 | 0 |
| LeftBehind-LeftBehind | 64 | 3 | 4 | 1 | 3 | 0 | 11139 | 0 | 200 | 6/12 | 0 |
| LeftBehind-LeftBehind | 64 | 5 | 4 | 0 | 2 | 2 | 21552 | 0 | 960 | 32/23 | 0 |
| LeftBehind-LeftBehind | 96 | 1 | 4 | 3 | 1 | 0 | 18705 | 0 | 0 | 15/6 | 0 |
| LeftBehind-LeftBehind | 96 | 3 | 4 | 3 | 1 | 0 | 14148 | 0 | 200 | 5/3 | 0 |
| LeftBehind-LeftBehind | 96 | 5 | 4 | 1 | 2 | 1 | 20127 | 0 | 320 | 63/17 | 0 |
| LeftBehind-LeftBehind | 128 | 1 | 4 | 0 | 3 | 1 | 24349 | 0 | 0 | 5/14 | 0 |
| LeftBehind-LeftBehind | 128 | 3 | 4 | 1 | 3 | 0 | 15891 | 0 | 320 | 0/33 | 0 |
| LeftBehind-LeftBehind | 128 | 5 | 4 | 3 | 0 | 1 | 26819 | 0 | 180 | 26/18 | 0 |
| Authority-Directorate | 64 | 1 | 4 | 0 | 2 | 2 | 20503 | 0 | 0 | 14/3 | 0 |
| Authority-Directorate | 64 | 3 | 4 | 3 | 0 | 1 | 21405 | 0 | 0 | 11/0 | 0 |
| Authority-Directorate | 64 | 5 | 4 | 4 | 0 | 0 | 22128 | 0 | 0 | 5/0 | 0 |
| Authority-Directorate | 96 | 1 | 4 | 0 | 2 | 2 | 20939 | 0 | 0 | 0/0 | 0 |
| Authority-Directorate | 96 | 3 | 4 | 2 | 1 | 1 | 23974 | 0 | 0 | 7/0 | 0 |
| Authority-Directorate | 96 | 5 | 4 | 2 | 0 | 2 | 25688 | 0 | 0 | 8/0 | 0 |
| Authority-Directorate | 128 | 1 | 4 | 0 | 3 | 1 | 19095 | 0 | 0 | 5/4 | 0 |
| Authority-Directorate | 128 | 3 | 4 | 1 | 2 | 1 | 24555 | 0 | 0 | 6/2 | 0 |
| Authority-Directorate | 128 | 5 | 4 | 4 | 0 | 0 | 20732 | 0 | 0 | 12/0 | 0 |
| Authority-LeftBehind | 64 | 1 | 4 | 4 | 0 | 0 | 8048 | 0 | 0 | 0/0 | 0 |
| Authority-LeftBehind | 64 | 3 | 4 | 4 | 0 | 0 | 11951 | 0 | 200 | 7/0 | 0 |
| Authority-LeftBehind | 64 | 5 | 4 | 1 | 2 | 1 | 21482 | 0 | 180 | 7/22 | 0 |
| Authority-LeftBehind | 96 | 1 | 4 | 4 | 0 | 0 | 11887 | 0 | 0 | 0/0 | 0 |
| Authority-LeftBehind | 96 | 3 | 4 | 3 | 1 | 0 | 18929 | 0 | 180 | 15/8 | 0 |
| Authority-LeftBehind | 96 | 5 | 4 | 2 | 1 | 1 | 20891 | 0 | 0 | 7/17 | 0 |
| Authority-LeftBehind | 128 | 1 | 4 | 4 | 0 | 0 | 11800 | 0 | 0 | 0/0 | 0 |
| Authority-LeftBehind | 128 | 3 | 4 | 3 | 1 | 0 | 13435 | 0 | 100 | 7/3 | 0 |
| Authority-LeftBehind | 128 | 5 | 4 | 4 | 0 | 0 | 18498 | 0 | 260 | 15/4 | 0 |
| Directorate-LeftBehind | 64 | 1 | 4 | 2 | 0 | 2 | 20254 | 0 | 0 | 1/1 | 0 |
| Directorate-LeftBehind | 64 | 3 | 4 | 1 | 1 | 2 | 20815 | 0 | 120 | 1/22 | 0 |
| Directorate-LeftBehind | 64 | 5 | 4 | 3 | 0 | 1 | 16759 | 0 | 240 | 15/27 | 0 |
| Directorate-LeftBehind | 96 | 1 | 4 | 3 | 0 | 1 | 18238 | 0 | 0 | 1/0 | 0 |
| Directorate-LeftBehind | 96 | 3 | 4 | 2 | 2 | 0 | 16546 | 0 | 120 | 6/19 | 0 |
| Directorate-LeftBehind | 96 | 5 | 4 | 0 | 1 | 3 | 25734 | 0 | 0 | 1/20 | 0 |
| Directorate-LeftBehind | 128 | 1 | 4 | 4 | 0 | 0 | 13096 | 0 | 0 | 0/0 | 0 |
| Directorate-LeftBehind | 128 | 3 | 4 | 2 | 2 | 0 | 21619 | 0 | 80 | 4/15 | 0 |
| Directorate-LeftBehind | 128 | 5 | 4 | 0 | 0 | 4 | 30000 | 0 | 160 | 17/28 | 0 |

Totals: 216 games, 0 desyncs, 0 exceptions, 44 draws at the tick limit (20 %), longest game 30 000 ticks (the limit).

## Early-game income (credits harvested, salvaged or plundered per minute, tier 2, mirror pairs)

### Small (64 cells)

| Faction | 3 min /min | 5 min /min | 10 min /min | vs mean 3/5/10 | Shipment share % | Games |
|---|---|---|---|---|---|---|
| Authority | 2174 | 2553 | 2311 | +14.3 % / +16.1 % / +13.5 % | n/a | 20 (40/40/34 side-samples; 3 ended before 10 min) |
| Directorate | 2220 | 2510 | 2147 | +16.7 % / +14.2 % / +5.4 % | n/a | 20 (40/40/40 side-samples; 0 ended before 10 min) |
| Left Behind | 1313 | 1534 | 1650 | -31.0 % / -30.3 % / -18.9 % | n/a | 20 (40/40/18 side-samples; 11 ended before 10 min) |

### Medium (96 cells)

| Faction | 3 min /min | 5 min /min | 10 min /min | vs mean 3/5/10 | Shipment share % | Games |
|---|---|---|---|---|---|---|
| Authority | 1892 | 2065 | 1903 | +10.4 % / +11.1 % / +15.8 % | n/a | 20 (40/40/28 side-samples; 6 ended before 10 min) |
| Directorate | 2001 | 2124 | 1709 | +16.8 % / +14.4 % / +3.9 % | n/a | 20 (40/40/40 side-samples; 0 ended before 10 min) |
| Left Behind | 1246 | 1384 | 1320 | -27.2 % / -25.5 % / -19.7 % | n/a | 20 (40/40/36 side-samples; 2 ended before 10 min) |

### Large (128 cells)

| Faction | 3 min /min | 5 min /min | 10 min /min | vs mean 3/5/10 | Shipment share % | Games |
|---|---|---|---|---|---|---|
| Authority | 1817 | 2027 | 1664 | +13.7 % / +12.9 % / +11.0 % | n/a | 20 (40/40/36 side-samples; 2 ended before 10 min) |
| Directorate | 1754 | 2027 | 1617 | +9.8 % / +12.9 % / +7.8 % | n/a | 20 (40/40/38 side-samples; 1 ended before 10 min) |
| Left Behind | 1223 | 1331 | 1217 | -23.5 % / -25.8 % / -18.8 % | n/a | 20 (40/40/36 side-samples; 2 ended before 10 min) |

Income read-out. Left Behind's mean income is below the mean of all three by 23 to 31 % at minutes 3 and 5 on every map size, and by 19 to 20 %
at minute 10. Against the mean of the other two factions only, Left Behind is -40 / -39 / -26 % (small), -36 / -34 / -27 % (medium) and
-32 / -34 / -26 % (large) at minutes 3 / 5 / 10. Authority and Directorate are within 10 percentage points of each other everywhere except
medium at minute 10 (+15.8 % vs +3.9 % of the three-way mean, a ratio of 1.11). The minute-10 column is biased for Left Behind on small maps
(only 18 of 40 side-samples: 11 of the 20 games had ended before minute 10) and, to a lesser degree, for the others where games ended early.

## Golden replay

`tests/Sim.Tests/Golden/LeftBehindGoldenTests.cs` with `golden_left_behind_hashes.txt` (56 lines, a hash every 16 ticks over 900 ticks, the
sim state hash as in `GoldenTests`). One scripted game on hand-made rules (balance data changes do not move it): the rig building packs into a
vehicle and a second rig unpacks elsewhere, a packed start rig unpacks, a harvester salvages a wreck, a raider plunders an enemy silo, and The
Second Drift storm is fired. The game is saved and loaded at tick 320; the loaded game must keep the hashes of an identical never-saved twin
(asserted at every recorded tick), and a second test asserts that each of the features really happened. `RTS_UPDATE_GOLDEN=1` regenerates the file.

## Findings

1. **No desync, no exception** in 216 games (every recording re-ran to identical hashes at every 16th tick and the same final hash).
   Save to load in the middle of a game is covered by the golden test, not by the soak.
2. **One idle packed rig over the 30 s limit**: Left Behind mirror, tier 5, small map (64), seed 2, game 1 (side B, slot 0; even seed so the start
   corners are swapped): a packed rig idled for 960 ticks (48 s); the game ended in a draw at tick 30 000 with 25 flagged units on side A. All other
   Left Behind games stayed at or below 320 ticks (16 s).
3. **Games that never end**: 44 of 216 games (20 %) were drawn at the 30 000 tick limit. By pair: Directorate-Left Behind 13 of 36, Authority-Directorate 10
   of 36, Directorate mirror 8, Left Behind mirror 6, Authority mirror 5, Authority-Left Behind 2. Large map, tier 5, Directorate vs Left Behind drew all four.
   Draws are not errors by the tool's rules (decided by health), but they show the AI often cannot finish an opponent.
4. **Stuck units (heuristic, see Method)**: at least one unit held a move order in one cell for 60 s in 132 of 216 games. Totals by pair (both sides, all
   cells): Left Behind mirror 308, Directorate-Left Behind 178, Authority-Left Behind 112, Directorate mirror 112, Authority-Directorate 77, Authority mirror 73.
   So Left Behind games carry about three times the Authority-only figure. Worst single games: LB mirror t5 medium seed 2 game 1 (38), Directorate mirror t5
   large seed 1 game 1 (30), LB mirror t3 large seed 2 game 1 (29), LB mirror t5 medium seed 1 game 0 (26).
5. **Left Behind income is outside the +-20 % band** of the other factions at minutes 3 and 5 on all three map sizes (see income tables). It is the only
   faction outside; Authority and Directorate stay within +-20 % of each other and of the three-way mean.
6. Cross-faction results (4 games per cell, so only a pointer): Authority beat Left Behind in 29 of 36 games (Left Behind 5, 2 draws); against Directorate
   Left Behind won 6 of 36 and lost 17 (13 draws). Authority vs Directorate: Authority 16, Directorate 10, 10 draws; Directorate won most tier-1 games, Authority tiers 3 and 5.

## Reproduce a finding
```
RTS_SOAK=1 RTS_SOAK_PAIRS=leftbehind-leftbehind RTS_SOAK_TIERS=5 RTS_SOAK_SIZES=64 RTS_SOAK_SEEDS=2-2 RTS_SOAK_TICKS=30000 RTS_SOAK_OUT=/abs/out.txt \
  /usr/bin/dotnet vstest tests/AI.Tests/bin/Release/net10.0/AI.Tests.dll --Tests:ALeftBehindGameReplaysWithoutDesync
```
(game=1 of the two lines is the idle-rig game; per-game lines look like `pair=... tier=5 size=64 seed=2 game=1 winner=draw end=30000 desync=no idleRig=0/960 ...`).
