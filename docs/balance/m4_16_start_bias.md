# M4-16a: start-0 bias on 2-player generated maps

Question (docs/balance/m4_ai.md §4.5): in 2-player AI matches the side at start 0 (4,4) won 2-11x more often than the side at
the mirror start, although `ai match` already trades player slots between the corners (`--starts alt`, D-063). So the cause is
geometric: something in the map or the sim is not symmetric under the map's 180-degree rotation.

## Method

- `Tools ai match --a tiered:3 --b tiered:3 --faction-a F --faction-b F --seeds 8 --ticks 25000 --size 56` (16 games per row,
  size-56 `MapGenerator.Generate(size, seed)` maps). The tool already prints wins by start corner and by slot.
- A scratch harness (not committed) for things the tool cannot do: (1) the same games on transformed maps (transpose, flip,
  left/right mirror) to separate map content from corner position; (2) per-start economy curves (credits harvested, units built at
  ticks 1 000 .. 12 000); (3) a **mirror tracker** that steps an AI game with both AIs on the same random stream and reports the
  first tick at which the two sides stop being exact point mirrors (buildings, vehicles, infantry, harvester state, funds).
  The mirror tracker found every cause below.

## Causes found (all fixed in the sim, tests in `tests/Sim.Tests/Economy/StartBiasTests.cs`)

| # | Cause | Evidence | Fix |
|---|---|---|---|
| 1 | **Ashfall regrowth swept the map in row-major order.** Cell i and its mirror N-1-i sit at opposite ends of the 1 200-tick sweep, so a field near start 0 regrew up to a whole interval before its mirror near start 1. | Mirror tracker, seed 1: identical until tick 1 670, then every cell of P1's home field one density lower than P0's. Per-start income over 12 seeds x 2: start 0 harvested 14.7 % more by tick 12 000 (807 562 vs 703 998). | `RegrowthSchedule`: a cell is keyed by the smallest index among its images under every dihedral symmetry the map shape allows (rot2, mirror x/y, and on square maps rot4 and both diagonals); cells with the same key regrow on the same tick. Derived from the map size, not state (allow-listed in HashCoverageTests). |
| 2 | **Harvester field choice took the first of equally rich cells in row-major order** (top-left first), so mirrored harvesters drove to non-mirrored cells. | Mirror tracker: goals (13,9) vs mirror of (14,9) right after #1 was fixed. | `HarvestMission.FindRich`: equal value -> nearer the search centre, nearer the harvester, then an orientation-free line key (as `PathService.LineKey`, with the harvester's facing when it stands on the centre). |
| 3 | **Factory exits and refinery docks are fixed cells south of the footprint.** For the north-west base that side faces the centre and the enemy; for the south-east base it faces the map edge, so its units leave behind the factory and its harvesters dock on the far side. | Mirror tracker: first divergence at the first unit produced; scraper paths home 6 vs 5 cells. | `Simulation.OrientedOffset`: a building whose footprint centre is in the south half uses its exit / dock turned by 180 degrees inside the footprint (production exits and refinery docks; tunnel exits are unchanged, see open points). |
| 4 | **Blocked factory exit searched ring cells top row first.** | Same pattern for the second unit out of a busy exit. | `FindFreeExitCell`: within a ring the cell nearest the map centre, then an orientation-free line key. |
| 5 | **A spawn into an occupied sub-slot took the first free sub-slot** (centre, NW, NE, SW, SE). | Mirror tracker: soldiers at a crowded exit in mirrored cells but unmirrored corners. | `Occupancy.CentreMostFreeSlot` (ties: left of the centre ray). |

Not causes (checked): the map itself (`MapGeneratorTests` already pin exact point symmetry of land, height, overlay and density;
path lengths to fields / centre / enemy are equal by construction); slot order (D-063 and D-065 already handle it and the tool
alternates slots: the by-slot tally is even in every row); A* and group-walk tie-breaks (D-063); target acquisition (D-063).

## Found but outside this package (AI, owned by m4-16b)

**`PlacementSearch` (src/AI/Recruit) breaks distance ties by (y, x) ascending**, so the south-east AI puts its first power plant,
barracks and silo on different (non-mirrored) spots than the north-west AI (seed 1: power at (3,0) vs the mirror of (4,7)), and
its lanes assume the exit is south of the footprint. A patch is in `scratch/m4-16b_placement_symmetry.patch` in this worktree
(tie-break in the yard's own frame, lanes on the oriented side). With it the mirror tracker shows identical build orders and
placements up to the first contact. It is NOT applied here (AiCommands/src/AI belong to m4-16b).

## Before / after (8 seeds x 2 = 16 games, tiered:3 mirror, size 56, 25 000 ticks)

Start wins = decisive wins by the side at start 0 / start 1. Ratio target 0.67-1.5.

| Run | Authority start0 / start1 / draws | Directorate start0 / start1 / draws |
|---|---|---|
| Before (main 6624e50), seeds 1-8 | 12 / 2 / 2 | 6 / 4 / 6 |
| After sim fixes #1-#4, seeds 1-8 | 12 / 2 / 2 | 8 / 2 / 6 |
| After sim fixes #1-#4, seeds 9-16 | 10 / 4 / 2 | 4 / 0 / 12 |
| + m4-16b placement patch, seeds 1-8 | 14 / 2 / 0 | 10 / 0 / 6 |
| + m4-16b placement patch, seeds 9-16 | 12 / 0 / 4 | 4 / 2 / 10 |

Before, cross-faction (Authority vs Directorate, seeds 1-8): tier 3 11 / 2 / 3, tier 4 10 / 1 / 5.

Harness numbers (16 seeds x 2, different game seed per game, so each game is independent):

| Build | Authority start0 / start1 / draws | Directorate start0 / start1 / draws |
|---|---|---|
| main | 17 / 10 / 5 | 16 / 1 / 15 |
| sim fixes #1-#4 | 21 / 7 / 4 | 11 / 8 / 13 |
| sim fixes #1-#4 + placement patch + oriented lanes | 14 / 16 / 2 | 9 / 10 / 13 |

## Reading and what remains

- **The target is not met in `ai match`.** Each fix above is proven by a test that fails on the old code. Together with the
  m4-16b placement patch they make the two sides exact mirror images for the first ~1 500-2 500 ticks (they were not mirrors from
  tick ~150 before). `ai match` still shows 10-14 : 0-4 for Authority. The harness, which gives every game its own seed, shows
  14 : 16 for the same build. Every `ai match` pair (seed s, game 1 and 2) plays the same game seed with the AI labels swapped,
  so a pair is one game counted twice. Eight independent games cannot separate a real bias from noise; 16-32 independent games
  per row (harness, or `ai match` with game seeds that differ per game, an AiCommands change for m4-16b) are needed.
- Remaining divergences seen by the mirror tracker after all fixes: an infantry sub-slot handover while walking (seed 3, tick 1 428:
  same lepton position, unmirrored sub-slot; suspected boundary rounding in `MoveMission` for walkers moving toward lower
  coordinates, a try did not reproduce in a unit test and was dropped); the infantry spread of a group move
  (`Simulation.Spread` uses `Spiral.Cell`, which starts each ring at its NW corner) and the AI's scatter/stuck random moves. The
  spiral is the next sim candidate (an orientation-free spiral order, like the exit search). Tunnel exits are not oriented
  yet (changing them moves the M3 tunnel tests' fixed exit cells; a follow-up should do both together).
- The golden replay (`tests/Sim.Tests/Golden`) did not move. The pinned default-skirmish hash in
  `tests/Game.Tests/Skirmish/SkirmishDefaultHashTests.cs` moved and was re-pinned (0x4d4963621bb2d0f6 -> 0xa0dcc7c1895b734c).
