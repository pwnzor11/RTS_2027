FILE: NOT WRITTEN. The Write tool refused `/home/claude/audit/sim-perf/findings.md` ("Subagents should return findings as text, not write report files"). I did not work around the block. The full findings file content is below, ready to save verbatim. Only `matrix_l4.log` and `matrix_l4.md` (tool output) exist in `/home/claude/audit/sim-perf/`.
COUNTS: critical 0 / major 4 / minor 9 / idea 1
TOP 5:
- SP-01 major PERF: a path goal on a blocked or enclosed cell makes A* expand the whole reachable map; `PathService.Process` solves the whole queue in one tick, then no path is solved until the overdraft is paid (20 attackers on a building = about 3 s of no pathing for everyone). Attack orders on buildings always hit this; walled bases too.
- SP-02 major SIM: 511 building slots shared by all players and neutrals, and each 1x1 wall is a building. When full, Place fails silently (ghost green, lane stuck), and a Bore is consumed for nothing.
- SP-03 major SIM: a Move ends as "arrived" after 13 blocked waits (6.5 s) without advancing a cell; attack/harvest callers stop detouring after the same count.
- SP-04 major SIM: harvesters pick fields by distance with no reachability test and remember one bad cell, so an unreachable nearer field traps them in a seek/whole-map-search loop.
- SP-08 minor PERF: `AcquireTarget`, `Detonate` and death cleanup still scan all 4 605 slots per call (old B-6, still open).
NOT COVERED: `Simulation.Tunnels/Transport/Carrier/Economy/Supply/Salvage/Rigs/Shipments/Storm/Superweapon`, `Hardening` beyond its entry, `PlayerView`, `AirMission`, BurrowMove after line 300, all tests. No function-level profiling: every cost figure except the measured 884 ticks/s is an estimate from the code. SP-03, SP-04 and SP-06 were read, not reproduced in a run.
MEASURED: prebuilt Tools, run from the repo root (read-only), `ai matrix --players 4 --tiers 3 --faction mixed --biome rust_belt --size l --seeds 1 --ticks 12000`: 48 000 ticks in 54.3 s = 884 ticks/s, about 1.13 ms per tick including 4 AIs.

================ findings.md content ================

# sim-perf: src/Sim movement correctness and simulation performance/scalability — findings

Scope read: `Simulation.cs` (whole), `Pathing/*` (PathService, PathHeap, RegionCache, MoveClass), `Missions/MoveMission.cs`, `Missions/CombatMissions.cs`, `Missions/HarvestMission.cs`, `Missions/MissionHandlers.cs`, `Movement/Occupancy.cs`, `Movement/Spiral.cs`, `Simulation.Movement.cs`, `Simulation.Combat.cs`, `Simulation.Effects.cs`, `Simulation.Ground.cs`, `Simulation.Actors.cs`, `Simulation.M3.cs`, `Simulation.View.cs`, `Simulation.Hud.cs`, `Simulation.Hashing.cs`, `Simulation.Naval.cs`, `Simulation.Landing.cs`, `Simulation.Players.cs`, `Simulation.BurrowMove.cs` (lines 1-300), `Map/MapState.cs`, `Vision/VisionState.cs`, `World/SlotArray.cs`, `World/MobileStore.cs`, `Core/SimLimits.cs`, `Core/Constants.cs`, `Simulation.Production.cs` (Place path only), `data/rules/terrain.toml`, `data/rules/game.toml [supply]`, `docs/balance/m4_16_perf.md`, `docs/reviews/2026-11-03_adversarial/B_scale.md`, `docs/design/01_architecture.md` §7.
One measurement: prebuilt Tools, `ai matrix --players 4 --tiers 3 --faction mixed --biome rust_belt --size l --seeds 1 --ticks 12000` (output `matrix_l4.log`, `matrix_l4.md` in this folder): 48 000 ticks in 54.3 s = 884 ticks/s = about 1.13 ms per tick including 4 AIs.
NOT read: `Simulation.Tunnels.cs`, `Simulation.Transport.cs`, `Simulation.Carrier.cs`, `Simulation.Hardening.cs` (only its flood entry), `Simulation.Economy/Supply/Salvage/Rigs/Shipments/Storm/Superweapon`, `View/PlayerView.cs`, `Ground/GroundState.cs` (Write only), air movement (`AirMission.cs`), the rest of BurrowMove, and all tests except a file-name search. Nothing was profiled function by function; every cost figure below except the 884 ticks/s is an estimate from reading the code.

## Summary (worst first)
1. A path request whose goal cell is blocked or enclosed makes A* expand the whole reachable map, and `PathService.Process` solves every queued request in one tick however much it costs, then refuses all path work until the overdraft is paid at 4 000 expansions per tick. Attack orders on buildings always have a blocked goal; walled-in bases (walls are exempt from the gap rule) make every approach unreachable. Expected result: a frame hitch, then seconds in which no unit in the game gets a route (SP-01).
2. All players and neutrals share 511 building slots, and each 1x1 wall segment is a building. When the store is full a finished structure cannot be placed, with no message, and a Bore is consumed for nothing (SP-02).
3. A Move gives up for good after 13 blocked waits (6.5 s) without advancing a cell; `StepToward` callers (attack, harvest, guard) stop repathing and detouring after the same count (SP-03).
4. Harvesters choose fields by distance only, never by reachability, and remember one bad cell, so an unreachable field traps a harvester in an endless seek/search loop (SP-04).
5. Group move spreading hands out destination cells on the far side of walls, cliffs and rivers (SP-05); attack-move sends every unit to the same cell (SP-06); unreachable goals end at the top-left-most nearest cell (SP-07).
6. Target acquisition, splash and death cleanup still scan all 4 605 actor slots per call (old B-6, still open) (SP-08).
7. The state hash now feeds roughly 600 000 one-to-eight-byte writes through a virtual stream every 16 ticks (SP-09).
8. `Snapshot` is still built after every tick, with allocations and per-cell work, and each Blackout cell costs a scan of every actor (SP-10).
Average speed is fine today (884 ticks/s with 4 AIs on a Large map). The risks are spikes and stalls, not the average.

## Scale estimate: 4 players at the supply cap on a Large map
Inputs from the code: Large = 128 x 128 = 16 384 cells (`src/Data/MapGenerator.cs:44`, `SimConstants.MaxMapCells`). Supply ceiling 200 per player, unit supply 1/2/3 (`game.toml [supply]`), so 4 players hold about 270-800 mobile units (400 is a fair middle). Stores: 2 047 vehicles, 2 047 infantry, 511 buildings, 1 023 projectiles (`SimLimits.Default`); nothing in Game, Data, AI or Tools overrides them. 20 ticks/s, so the budget is 50 ms per tick at 1x and 6.25 ms at 8x.

Rough operations per tick with about 600 units in a four-way fight (estimates):
| Source | Count per tick | Note |
|---|---|---|
| Fixed sweeps over store capacity (about 25 loops in Step) | 110 000 slot tests | paid even with 10 units alive |
| `AcquireTarget` | AttackMove units without a target scan every 5 ticks, guards every 15: 600/5 x 4 605 = 550 000 worst case, about 180 000 typical | SP-08 |
| `Detonate` (every projectile impact and every splash) | 20-30 impacts x 4 605 = 90 000-140 000 | SP-08 |
| `OnCombatActorRemoving` | deaths x (4 605 + 1 023); 10 deaths = 56 000 | SP-08 |
| `UpdateVision` | 4 605 compares + moving units x 2 discs x about 250 cells = up to 300 000 cell updates; plus 4 x (512 ghost slots + buildings x footprint) | fine; 4 list allocations per tick (`GhostsFor`) |
| Blackout and Foam countdown | 32 768 | whole map every tick |
| Path search | 4 000 expansions on average, each 10-16 `Factor` calls; unbounded in a single tick (SP-01) | |
| State hash (every 16th tick) | about 600 000 stream writes, 1.2 million hasher appends | SP-09 |
| `Snapshot` (once per tick, game only) | 16 384 x 4 ground predicates + 16 384 vision cells + all units + allocations | SP-10 |
Read as time: about 1-3 ms of steady work per tick, which fits the measured 1.13 ms. The spikes are a batch of whole-map searches (tens of ms, SP-01), the hash tick, and an AI placement search (SP-11).

Top 5 hotspots and what to do:
1. **Whole-map A* for blocked goals plus the unbounded batch (SP-01).** Retarget a blocked goal to the nearest passable cell before searching, refuse region-unreachable goals with a region label lookup (`RegionCache` already exists for ships), and cap the work of one `Process` call. Determinism cost: all three are pure functions of sim state; routes and timing change, so goldens and replays move once.
2. **`AcquireTarget` and `Detonate` full scans (SP-08).** Keep the visiting order but shrink the set: per-team compact lists of live slots in ascending slot order, or a coarse bucket grid (8 x 8 cells) updated when an actor changes cell. Determinism cost: none if candidates are visited in (kind, slot) order as today; buckets are derived state, rebuilt on load, not hashed.
3. **State hash (SP-09).** Put a 64 KB buffer between the `BinaryWriter` and the hashers, or write the big arrays as byte spans. Same bytes in the same order, so the hash value is unchanged: zero determinism cost.
4. **Snapshot per tick (SP-10).** Reuse buffers, build the snapshot once per rendered frame while still collecting `FxEvents` per tick, and answer Blackout visibility from a per-team "actor within 1 cell" grid. View side only: zero determinism cost.
5. **`CanPlace` (SP-11) and `RulesOf` (SP-12).** Use the occupancy grid instead of scanning 4 094 unit slots; cache derived rules per player. Pure refactors: zero determinism cost.
Time-slicing target scans across ticks is also possible but changes who fires when, so it moves hashes; do it last, if at all.

## Findings

### SP-01 · major · PERF · confidence high (mechanism), medium (size of the hitch)
**What:** A path request to a blocked or enclosed goal explores every reachable cell before answering Partial, and one tick solves all queued requests regardless of cost; the overdraft then blocks all path solving for (cost / 4 000) ticks.
**Where:** `src/Sim/Pathing/PathService.cs:134-155` (batch rule, debt), `:198-208` (`Factor`: a building cell is 0), `:270-306` (search runs until the heap is empty, then Partial); `src/Sim/Missions/CombatMissions.cs:134-135` (chase cell = the target's footprint cell); `src/Sim/Missions/MoveMission.cs:184-218` (non-settling callers get `WaitTicks` on NotFound and ask again); `src/Sim/Simulation.cs:458` (walls and gates skip the gap rule, so bases can be sealed).
**Evidence:**
- `Engage` sets `e.ChaseCell = tp.ToCell()`, and for a building `TargetPoint` returns a footprint cell centre. That cell has a building, so `Factor` is 0 and A* can never pop the goal. The loop ends only when the open set is empty, after every cell reachable from the attacker has been expanded: about 10 000-13 000 expansions on a 128 x 128 map. This happens for every unit given an Attack order on a building it is not yet in range of (the player's right-click, and the AI at `src/AI/Recruit/MilitaryManager.cs:559,578`, `HarassManager.cs:57`, `ExpeditionTactics.cs:741-903`). Attack is not batched, so 20 selected tanks are 20 separate whole-map searches. Auto-acquired targets are already in range, so they do not trigger it.
- The chase cell is re-evaluated every 10 ticks (`ChaseRetargetTicks`); when the nearest footprint cell changes while the unit drives, `Follow` sees a new goal, resets the path and searches the whole map again.
- `Process`: `if (budget > 0) { solve the whole queue }`, then `_debt = -budget`. 20 x 12 000 = 240 000 expansions in one tick, then `budget = 4000 - debt` stays negative for 59 ticks (about 3 s), during which no request from any player is solved. Requests queued in that time are then solved together in the next open tick, which overdraws again.
- An enclosed goal (a base sealed with walls, which `CanPlace` allows at line 458 and which hardening rewards) gives the same full search for every Move, AttackMove and harvest order into it. A `StepToward` caller that ends in NotFound returns 10 and requests again about 11 ticks later for as long as the order stands; a cache hit is charged the full cost by design (`:238-242`), so repeats still stall the queue. 30 units held outside a sealed base ask for about 30 x 12 000 / 11 = 33 000 expansions per tick against a budget of 4 000.
- Old finding B-5 (path latency) is still open in this new form: the M2-01 batch rule fixed slot-order bias but removed the per-tick cap.
- Cost per expansion is not measured. From the code (8 neighbours, up to 16 `Factor` calls, a 4-key heap) I estimate 150-300 ns, so one whole-map search is 2-4 ms and 20 of them 40-80 ms: more than a 50 ms tick.
**Impact:** A visible stutter when a group is ordered to attack a building or to move into a sealed area, followed by seconds in which newly ordered units of every player stand still. During a siege of a walled base this repeats in cycles.
**Fix idea:** (a) If the goal cell is impassable for the mover, replace it with the nearest passable cell (ring search, orientation-free tie-break) so A* terminates on arrival. (b) Keep region labels for land classes too, so "other region" is answered without a search. (c) Bound `Process`: stop taking requests once the tick's cost passes some multiple of the budget, serve the rest next tick in FIFO order. (d) For a non-settling caller that got NotFound, back off (10, 20, 40... ticks) and do not re-request while `Map.Version` is unchanged. Size M. Moves hashes.

### SP-02 · major · SIM · confidence high
**What:** The building store holds 511 buildings for the whole match, shared by all players and neutral structures, and every wall segment and gate is a building. At the limit, placement fails silently.
**Where:** `src/Sim/Core/SimLimits.cs:13` (`Default = new(2048, 2048, 512, 1024)`); `data/rules/buildings.toml:138-151` (`auth_wall` 1x1, `wall = true`; also lines 329, 874; gate 478); `src/Sim/Simulation.Production.cs:197-203`; `src/Sim/Simulation.cs:575-581`; `src/Sim/Simulation.BurrowMove.cs:297`; `src/Sim/Simulation.Rigs.cs:186`; `src/Sim/Vision/VisionState.cs:20`.
**Evidence:** `PlaceBuilding` returns `EntityId.None` when `_buildings.Add` finds no slot; `ApplyPlace` then returns and leaves `_production.Ready[lane]` set. `CanPlace` does not know about the limit, so the ghost is green, the click is accepted and nothing happens; the lane stays occupied by an item that can never be placed. `BeginBreakThrough` removes the Bore first and comments "building store full: the Bore is lost". No file in `src/Game`, `src/Data`, `src/AI` or `src/Tools` sets a `SimLimits` other than the default. A wall around a 30 x 30 base is about 120 segments; four players doing that use 480 slots before any real building. Old finding B-4 asked whether walls were overlays or buildings: they are buildings. `VisionState.GhostCapacity` is a separate hard-coded 512: raising only `Limits.Buildings` would make `RecordBuildingSeen` throw.
**Impact:** Late in a 4-player game with walls, a finished structure becomes unplaceable with no explanation, and Bores are lost.
**Fix idea:** Short term: make `CanPlace` return a new `PlacementResult.LimitReached` and show it; refuse to start a structure when no slot is free; do not remove the Bore before the slot is secured. Then either raise the default (save and hash format change: needs a decision), give each player a wall quota, or move plain walls to the overlay layer (`OverlayKind.Wall` already exists and blocks movement). Size S for the message, M-L for the real fix.

### SP-03 · major · SIM · confidence medium
**What:** After 13 consecutive blocked waits without entering a new cell (130 ticks, 6.5 s), a Move ends as if it had arrived; for `StepToward` callers the same counter silently switches off repathing and detours.
**Where:** `src/Sim/Missions/MoveMission.cs:27-29` (`WaitTicks = 10`, `MaxStuck = 12`), `:470-475`, `:234-239`, `:466`.
**Evidence:** `Blocked`: every 10th blocked tick does `int stuck = ++store.StuckCount[i]; if (stuck > MaxStuck) { arrived = settle; return 1; }`. With `settle` true (a Move) the unit queues Guard where it stands. With `settle` false nothing happens and the function returns before the detour and repath code, on every later cycle too, because `StuckCount` is only cleared when the unit enters a cell or gets a new goal. The path search ignores units, so a repath through a jam returns the same route; the only ways forward are waiting or `TryDetour`. A unit at the back of a column in front of a ramp (map generator: ramps are 2-4 cells wide) or a gate can stand 6.5 s without advancing one cell. Read from the code, not reproduced.
**Impact:** In large group moves through a choke, rear units drop the order and stop in the approach, where as idle units they are then shoved about by those behind. Players see part of the army "forget" the move. Attackers and harvesters stuck behind a long-lived blocker stop looking for a way round.
**Fix idea:** Count only cycles in which the blocker made no progress either, or reset the counter while the blocker is an allied mover; for `settle == false` keep trying a detour every few cycles instead of returning early. Size S-M. Moves hashes.

### SP-04 · major · SIM · confidence medium
**What:** A harvester picks the richest cell on the nearest ring with no reachability test and excludes only the single last bad cell, so a nearer field it cannot reach (sealed by walls, behind a cliff line, on the far bank) holds it in a loop.
**Where:** `src/Sim/Missions/HarvestMission.cs:162-195` (`FindRich`: tests `TerrainOk` of the cell itself, occupancy and claims only), `:124-125` (`BadCell[i] = goal` after a failed approach), `:91` and `:103` (`BadCell` cleared).
**Evidence:** For an unreachable goal `StepToward` first gets Partial (whole-map search, SP-01), drives to the nearest reachable cell, asks again and gets NotFound, which returns `WaitTicks`; `HandleHarvest` then stores that one cell in `BadCell` and seeks again. `FindRich` skips exactly `c == BadCell[self]`, so the next cell of the same field is chosen and the cycle repeats: two or three whole-map searches per attempt, for ever. `PickRefinery` (`:348-363`) has the same weakness: nearest dock by straight distance, no reachability, no account of queue length.
**Impact:** A harvester idles beside a cliff or an enemy wall while a reachable field exists further away, and its repeated searches feed the stall in SP-01. I did not find a generated map that proves it; it needs a field nearer in Chebyshev distance than any reachable one, which sealed bases and plateau edges can produce.
**Fix idea:** Test reachability with a land region label (see SP-01 b) inside `FindRich`, and replace the single `BadCell` by a "bad field" memory (ban all cells within 4 of the bad one until `Map.Version` changes). Size S-M. Moves hashes.

### SP-05 · minor · SIM · confidence high
**What:** Group move spreading gives units destination cells that are terrain-passable but not connected to the clicked cell.
**Where:** `src/Sim/Simulation.Movement.cs:192-217` (`Spread`), `src/Sim/Movement/Spiral.cs:10` (`MaxRing = 24`).
**Evidence:** `Spread` walks a square spiral and accepts a cell when `Occupancy.TerrainOk` holds and no vehicle outside the batch stands on it. Nothing checks that the cell is on the same side of a wall line, cliff, river or building row as the target. A 40-unit move ordered next to a cliff edge or just outside an enemy wall puts part of the spiral on the other side. Those units follow the shared group route, then request a private tail to their own cell: reachable only by a long way round (they leave the group, possibly into the enemy) or not at all (whole-map search, then they stop at the nearest point).
**Impact:** Parts of a group peel off toward the wrong side of an obstacle; with sealed areas, extra whole-map searches.
**Fix idea:** Accept a spiral cell only if a bounded flood from the target reaches it within about ring + 4 steps. Size S. Moves hashes.

### SP-06 · minor · SIM · confidence medium
**What:** Attack-move, guard-return and chase have no spreading: every unit of a group is sent to the same cell and must stand on that exact cell to finish.
**Where:** `src/Game/Controls/OrderController.cs:177` (one `cell` for every selected unit); `src/Sim/Missions/CombatMissions.cs:111-117` (`Position.ToCell() == e.GuardPoint` ends the mission, else `StepToward(GuardPoint)`); `src/Sim/Missions/MoveMission.cs:476-482`.
**Evidence:** Only `CommandKind.Move` goes through `StartPendingMoves`/`Spread` and the group search. For AttackMove each unit files its own single `Request` to the shared cell (N searches instead of one group search), and the first arrival turns Guard on that cell. Later units find the final cell taken; `Shove` scatters the idle occupant, the next unit takes the cell and is shoved in turn. Once the eight neighbours are full of waiting units `FindScatterCell` fails, the occupant stays, and everybody else remains in AttackMove for good: `Idle` false for the AI (`MilitaryManager.cs:563` skips non-idle units), scanning for targets every 5 ticks instead of every 15, and after SP-03's count no longer repathing. Read from the code, not run.
**Impact:** Attack-moved armies never report arrival, cost about three times the target scans of a guarding army, and funnel through one cell.
**Fix idea:** Batch AttackMove commands of one player to one cell exactly like Move (same `Spread`, same group request) and treat "in my spread cell" as arrival. Size M. Moves hashes.

### SP-07 · minor · SIM · confidence high
**What:** For an unreachable goal the search returns the reachable cell nearest by Chebyshev distance and breaks ties by lowest cell index, the top-left-most candidate; a unit already standing on a nearest cell is sent on to that one.
**Where:** `src/Sim/Pathing/PathService.cs:283` and `:303-305`.
**Evidence:** `if (cheb < bestCheb || (cheb == bestCheb && cur < bestCell))`. After walking a Partial route the unit requests again; if another reachable cell has the same Chebyshev distance and a lower index, the answer is Partial again with a route to it, and only the third request returns NotFound. A unit ordered onto a plateau or into a sealed base from the south slides along the obstacle toward its north-west end. The rest of the path code is deliberately mirror-symmetric (`LineKey`, the M2-01 and M4-16 notes); this tie-break is not, so the top-left start is treated differently from the bottom-right one.
**Impact:** Sideways shuffles at walls and cliffs, one extra whole-map search per unit, and a small start-position bias.
**Fix idea:** Break the tie by squared distance to the goal, then by `LineKey`, and return NotFound when the start is already at the minimum distance. Size S. Moves hashes.

### SP-08 · minor · PERF · confidence high (old B-6, still open)
**What:** `AcquireTarget`, `Detonate` and `OnCombatActorRemoving` each loop over every slot of all three stores.
**Where:** `src/Sim/Simulation.Combat.cs:164-193`, `:339-394`, `:510-525`; scan intervals `:31` (15) and `src/Sim/Missions/CombatMissions.cs:19` (5).
**Evidence:** Each call tests 2 047 + 2 047 + 511 slots whatever the live count. `Detonate` does so for every projectile impact, even splash 0, although victims can only be within `Splash` cells and the map already stores who is in each cell (`MapState.VehicleAt`, `InfantryAt`, `BuildingAt`). Garrison damage adds a 2 047-slot infantry loop per hit on a garrisoned building (`:285-287`). Counts are in the scale table: roughly 300 000-800 000 slot tests per tick in a 600-unit battle.
**Impact:** Not a problem at today's 1.13 ms per tick, but it is the cost that grows with unit count times fights, and it is paid inside the 6.25 ms tick at 8x speed.
**Fix idea:** `Detonate`: visit the cells within the radius through the occupancy arrays, plus a short list of air units (air units hold no cell: `Occupancy.Claim` returns early for them), and queue hits in (kind, slot) order so kill credit is unchanged. `AcquireTarget`: per-team compact slot lists or a coarse bucket grid, visited in the present order. Size M. No hash change if the order is kept.

### SP-09 · minor · PERF · confidence high (mechanism), low (size)
**What:** Every 16 ticks the whole state is serialised field by field into a stream that hashes each tiny write twice.
**Where:** `src/Sim/Simulation.Hashing.cs:64-91` and `:99-106`; `src/Sim/Map/MapState.cs:153-162, 178-181`; `src/Sim/Vision/VisionState.cs:264-282`; `src/Sim/Simulation.cs:267-271`.
**Evidence:** `BinaryWriter` does not buffer primitives, so each `w.Write(ushort)` is a virtual `Stream.Write` followed by `Full.Append` and `Part.Append`. On a 128 x 128 map the map part alone is about 262 000 calls (land and overlay one byte at a time, each `EntityId` as two ushorts, five infantry slots per cell); vision adds 4 x 16 384 counts plus 4 x 2 048 x 5 actor fields, about 110 000; the per-slot parts add the rest. Total about 600 000 stream writes and 1.2 million hasher appends. At a guessed 15-25 ns per write that is 9-15 ms on every 16th tick, which would be a third to a half of the average Step cost and a regular hitch at 8x speed (ten hash ticks per second). Old B-9 (a new MemoryStream per hash) was fixed; this replaced it. `src/Game/Host/SimHost.cs:269` then calls `_recording?.RecordHash`, which may hash a second time (not read).
**Impact:** A periodic spike, largest on Large maps, independent of how many units are alive.
**Fix idea:** Wrap the sink in a 64 KB buffer flushed at part boundaries, and write the cell arrays with `MemoryMarshal.AsBytes`. The byte stream, and so the hash, stays identical. Measure first with a `Stopwatch` around `ComputeStateHash` in the existing perf bench. Size S.

### SP-10 · minor · PERF · confidence high (still open from m4_16_perf.md)
**What:** `SimHost` builds a full `Snapshot` after every tick; it allocates and does several whole-map passes, and each Blackout cell costs a scan of every actor.
**Where:** `src/Game/Host/SimHost.cs:271`; `src/Sim/Simulation.cs:765-820`; `src/Sim/Simulation.View.cs:77-94, 102-108, 140-161`; `src/Sim/Simulation.Ground.cs:111-127, 209-224`; `src/Sim/Simulation.Players.cs:91-92`; `src/Sim/Vision/VisionState.cs:237-246`.
**Evidence:** Per call: two lists sized to the live counts, a telegraph list, `new byte[w*h]` twice (ground and vision, 16 KB each), an `FxEvent[]`, one ghost list; `FillGroundLayers` runs four predicates on each of 16 384 cells. `IsVisibleTo` calls `CanSeeThroughBlackout` for any cell with Blackout, and that function loops over all 4 605 slots until it finds a friendly actor within one cell; for a visible Blackout cell with no friend beside it the loop runs to the end. It is reached from `ApplyBlackoutToGrid`, from `KnownBlackoutAt` in `FillGroundLayers`, and from target visibility inside Step. A trail of 200 Blackout cells (vehicles with `DropsBlackoutTicks` lay one per cell driven) is about 2 x 200 x 4 605 = 1.8 million slot tests per snapshot. `IsGateOpenNow` scans all 4 094 mobile slots per gate shown. At 8x speed all of this runs 8 times per frame though one result is drawn; `m4_16_perf.md` measured 13-16 % of headless time and left it open.
**Impact:** Frame time at high game speed, and a sharp drop when Blackout clouds are in view.
**Fix idea:** Snapshot only for the last tick of a frame and append the `FxEvents` of skipped ticks; pool the arrays and lists; precompute per team a bitmap "friendly actor within 1 cell" for Blackout and stealth reveal. Size M. No sim state involved.

### SP-11 · minor · PERF · confidence medium
**What:** `CanPlace` scans every vehicle and infantry slot and may flood the map; the AI calls it once per candidate cell.
**Where:** `src/Sim/Simulation.cs:452-459` (comment "Map occupancy is not maintained for units yet"), `:465-508`; `src/AI/Recruit/PlacementSearch.cs:69`.
**Evidence:** The comment is out of date: `MapState` has held vehicle and infantry occupancy since M1-08, and `IsBuildable` already reads it. Each call still loops 4 094 slots, then `SealsRegion` rebuilds the 16 384-cell `_open` array whenever `Map.Version` changed and runs a depth-first search that can cover the map before it meets all ring cells. A placement search over a few hundred candidates is millions of operations in one AI think.
**Impact:** Occasional long AI ticks; a small cost for the placement ghost each frame.
**Fix idea:** Check the footprint cells through `MapState` occupancy (air units hold no cell, which matches the current exemption); bound the ring search to a box around the footprint before the full count. Size S. Results unchanged.

### SP-12 · minor · PERF · confidence medium
**What:** Once a player has any upgrade, every `RulesOf(player)` call does a `ConditionalWeakTable` lookup and a linear search.
**Where:** `src/Sim/Simulation.Players.cs:30-35`; `src/Sim/Rules/RulesDeriver.cs:56-69`.
**Evidence:** `RulesOf` is called many times per actor per tick (`SightOf`, `WeaponOf`, `StatsOf`, `ArmorAt`, `BuildingTypeAt`, `IsStealthType`, `SeismicRadiusOf`...). With a zero mask it returns at once; with a non-zero mask it goes through `Memos.GetValue` and scans up to 64 entries. In a late game all four players have upgrades, so this is paid on tens of thousands of calls per tick.
**Impact:** A slow rise of per-tick cost during a match, unrelated to unit count.
**Fix idea:** Keep `SimRules[]` and `ulong[]` scratch per player in `Simulation`, refreshed when the mask differs. Size S. Not state, no hash change.

### SP-13 · minor · PERF · confidence high
**What:** The group search falls back to one private search per unit whenever the bounded box cannot reach the starts, and it allocates a map-sized array per request.
**Where:** `src/Sim/Pathing/PathService.cs:32` (`GroupMargin = 16`), `:343-362` (`new bool[_w * _h]`), `:395-402`; `src/Sim/Missions/MoveMission.cs:209-213`; `PathService.cs:161-176` (`Poll` scans the queue linearly and allocates an `Entry`).
**Evidence:** The backward search is confined to the bounding box of starts and goal plus 16 cells. If the real route leaves that box (a lake or cliff line longer than the margin) every start is NotFound and each unit files its own request on the next tick; all are solved in the same `Process` call (SP-01's batch rule). The group search has no heuristic, so a cross-map group move expands most of the box (up to 16 384 cells, four ticks of budget) by itself.
**Impact:** The case the group search was built for, a large army crossing a map with a long obstacle, becomes N long searches in one tick.
**Fix idea:** On failure retry the group search once with the whole map as the box before falling back; reuse a stamped scratch array for `wanted`. Size S. Routes for the retry case change.

### SP-14 · idea · SIM · confidence high
**What:** Other fixed limits and counters, checked for overflow and silent failure.
**Where:** `src/Sim/Core/SimLimits.cs:13`; `src/Sim/Simulation.Combat.cs:244`; `src/Sim/World/SlotArray.cs:38-41`; `src/Sim/Simulation.cs:166`.
**Evidence:**
- `Tick` is an `int` at 20 ticks/s: overflow after 2^31 / 20 / 3600 = 29 826 hours. Timers are `Tick + small`. No risk.
- Projectiles: 1 023. `Fire` returns false when none is free, without starting the reload, so the unit fires a tick or two later. Harmless.
- Vehicles and infantry: 2 047 each against at most 800 supply-capped units. Safe while supply is on; a mode with supply off has only the store limit (`Simulation.Production.cs:401,414`, not read further).
- Slot generations are 16-bit and the lowest free index is reused first, so a slot's generation wraps after 65 535 reuses (projectile slot 1: 65 535 shots). I found no long-lived holder of projectile ids, so this is a note, not a defect.
- Vision counts are `ushort` per cell per player (at most 4 605 actors): safe. `Map.Version` and path ids are `int`: safe.
- `Spiral.MaxRing = 24`: a burrower with no valid surfacing cell within 24 cells re-scans 2 401 cells every 20 ticks and never surfaces (`Simulation.BurrowMove.cs:74, 236-242`); documented there as ASSUMED.
**Impact:** None today.
**Fix idea:** None beyond SP-02; keep the list for the next limits decision.

## Open questions
- SP-01: was the removal of the per-request cut-off in M2-01 meant to leave a tick's path work unbounded? A cap of about 4x budget per tick with FIFO carry-over would keep "same tick, same answer tick" for normal loads.
- SP-02: is 511 shared buildings an intended cap? If walls stay buildings, should each player get a wall quota, or should the default rise (save, replay and golden-hash change)?
- SP-06: should attack-move hold formation like Move? Today every unit reuses the single guard point.
- SP-04: does the generator guarantee every field is land-reachable from every start? If so the finding is limited to wall-sealed fields.
- Nobody has measured the hash tick or a whole-map search in isolation. Two `Stopwatch` probes in `SpectatorPerfBench` (around `ComputeStateHash`, and the maximum single `Paths.Process` time) would confirm or kill the sizes in SP-01 and SP-09 in minutes.

## Things checked and found sound
- Locomotion and terrain: `MapState.SpeedPercent` is the single passability source for search, stepping, spreading and harvesting; shallows are foot 40, wheeled 0, tracked 0, hover 100; coast is hover 80, naval 0; deep-only ships close Shallow through `MoveClass.NavalDeep`; blocking overlays (walls, props) stop every class except air; diagonal corner-cutting is refused the same way in the search (`PathService.cs:291`) and in the step (`Occupancy.CornersOk`).
- Path cache invalidation: the key holds `Map.Version`, bumped by every building, land and blocking-overlay change and by `Touch`; harvesting does not bump it. Followers re-validate the rest of their route on a version change (`MoveMission.cs:222-227`) and back out of a reserved cell that got built on (`:269-275`).
- Occupancy: claims are released only by their holder; a diagonal step cannot leave the two held cells (`MoveMission.cs:286-305`); stale claims are cleared on sight.
- Ship moves to another sea or onto land are refused before any search (`Simulation.Naval.cs:47-62`).
- Vision is incremental (one compare per unmoved actor); counts cannot underflow silently.
- Step allocates very little: no LINQ, no capturing closures, no string building. The per-tick allocations are four ghost lists in `UpdateVision`, path result arrays and move-batch scratch.
- Head-on allied deadlock is broken by slot order (`MoveMission.cs:483-488`); idle allies are shoved.
- Average throughput: 884 ticks/s for 4 tier-3 AIs on a Large map, in line with `m4_16_perf.md` (974 on its bench).