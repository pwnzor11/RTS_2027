FILE: NOT WRITTEN. The harness refused the Write of `/home/claude/audit/sim-det/findings.md` ("subagents should return findings as text, not write report files"), and I did not work around it. The complete findings file is below, in the brief's format; please save it to that path. The only file in my folder is `/home/claude/audit/sim-det/ruleshash_cov.py`.

COUNTS: critical 0 / major 2 / minor 7 / idea 1

TOP 5:
- SIMDET-01, major: a garrisoned soldier still accepts Move/Stop/Attack/Guard and walks out while staying hidden and untargetable.
- SIMDET-02, major: with the 511-slot building store full, a deploying vehicle (Bore, Expedition depot ship) is removed and nothing is built.
- SIMDET-03, minor: group path results dropped while still queued are never released, so the hashed and saved "paths" part grows all game.
- SIMDET-06, minor: the build string in save and replay headers is never compared or shown, so a replay from another build reports a plain desync.
- SIMDET-04, minor: sight above 64 passes the loader (bound 128) but throws inside Step; latent, as current data tops out at 10.

NOT COVERED: nothing was built or run, so every finding is from reading code. Not read line by line: HarvestMission, CombatMissions, CaptureMission, SalvageMission, most of BurrowMove, Tunnels, Ground and Garrison, all of Hardening, Storm, Beachhead, Veterancy, Raid, Salvage, PlayerView, the small state parts (trusted to HashCoverageTests), and Lua/extension code. For SIMDET-01 the sim path is traced, but whether the in-game selection can pick a garrisoned soldier is unconfirmed.

No determinism break was found inside Step. Nothing here came from memory.

---

# sim-det: src/Sim, determinism / state integrity / save-load / replay — findings

Scope read: `Simulation.cs`, `.Hashing`, `.Actors`, `.Movement`, `.Combat`, `.Production`, `.Economy`, `.Supply`, `.Players`, `.M3`, `.Transport` (commands, capture), `.Carrier`, `.Superweapon`, `.Naval`, `.Shipments`, `.Rigs` (pack/unpack), the command preambles of every other `Simulation.*.cs` partial, `MoveMission` (route state), `MissionHandlers`, `Occupancy`, all of Core / Numerics / Serialization / Replay / Map / Pathing (`PathService`, `RegionCache`) / Vision, the Write/Read of `MobileStore`, `BuildingStore`, `PlayerStore`, `CombatState`, `ProjectileStore`, `EconomyState`, `ProductionState`, `TunnelState`, `GroundState`, `EffectsState`, `StatsState`, `NavalState`, plus `RulesDeriver`, `tests/Sim.Tests/Determinism/HashCoverageTests.cs`, the save/replay tests (by name and structure), and the callers in `src/Game`, `src/AI`, `src/Scripting` needed to check reachability. No build and no test run (per the brief).

NOT read line by line: `HarvestMission`, `CombatMissions`, `CaptureMission`, `SalvageMission`, `Simulation.BurrowMove` (deploy part only), `.Tunnels` (validation and transit only), `.Ground` (emitters only), `.Hardening`, `.Storm`, `.Beachhead`, `.Veterancy`, `.Raid`, `.Salvage`, `.Garrison` (entry and commands only), `View/PlayerView`, the small state parts (garrison, directive, rigs, salvage, veterancy, shipments, beachhead), `RulesHash.cs` beyond a field-coverage script, Lua/extension code.

## Summary (worst first)
- No source of non-determinism found inside Step: no float, no wall clock, one owned PCG32, slot-order iteration, a total command sort, no mutable statics that change results. The hash/save coverage test is strong, and the allow-list reasons I checked (path cache, supply cache, emitter cache, region cache, regrowth, pending buffers) are still true.
- The real problems are state-integrity holes at the edges: garrisoned soldiers accept orders (SIMDET-01), and a deploying vehicle is destroyed when the building store is full (SIMDET-02).
- One slow leak in hashed and saved state: dropped group path results (SIMDET-03).
- Latent crash paths that need unusual data or non-default limits (SIMDET-04, SIMDET-05).
- Replay/save identity: the build string is never checked (SIMDET-06); corrupt saves can throw the wrong exception or fail later inside Step (SIMDET-07).
- For M9 LAN the sim still trusts `Command.Player`, only counts late commands, and keeps no state for a desync diff (SIMDET-09, mostly still open from review A-5/A-8).

## Findings

### SIMDET-01 · major · SIM · confidence medium (sim path high, UI reachability medium)
**What:** A soldier inside a garrison (`ActorLayer.Garrisoned`) still accepts Move, Stop, Scatter, Attack, AttackMove and Guard. The new mission replaces `MissionKind.Garrisoned` and the soldier walks out, but its layer and carrier are never cleared. It stays hidden from enemies, untargetable and shielded from splash while it moves and, on Guard, fires.
**Where:** `src/Sim/Simulation.cs:391-395` (`OwnsSubject`: alive and owner only), `Simulation.Movement.cs:28-55`, `Simulation.Combat.cs:399-426`, `Missions/MoveMission.cs:67-83` (no layer check), `Simulation.Garrison.cs:63-74` (`GarrisonNow`), `Simulation.Garrison.cs:114-117` (only the reverse case is handled), `Simulation.Ground.cs:339-341` (`IsUntargetableBy`), `Simulation.M3.cs:190` (`IsHiddenFrom`), `Simulation.Combat.cs:370` (splash skips Garrisoned).
**Evidence:**
- Transport passengers, tunnel occupants and docked aircraft are pinned every tick against exactly this: `Simulation.Transport.cs:85-97` `SyncPassengers` ("a stray order may have reserved a cell"), `ValidateTunnelTransit` -> `PinUnderground`, `Simulation.Carrier.cs:99-107` `RideAndRepair`. Garrison has no such sync, and `grep Garrisoned src/Sim` shows no command-side check.
- Trace for Move: `ApplyMovementCommand` queues Move; `Commence` sets `Mission = Move`; `HandleMove` -> `Follow` uses `Occupancy.CurCell` = the old `SlotCell`. The soldier drives from the building centre back to that sub-slot without re-claiming it (`GarrisonNow` released it, so two soldiers can share a sub-slot), then paths normally.
- `InfantryLayer` stays `Garrisoned`, so `OccupantsIn` still counts it, `IsHiddenFrom` is true for every enemy and `IsUntargetableBy` is true. `Fire` (`Simulation.Combat.cs:226-234`) blocks only Diving and underground layers, and `ActorLayers.IsUnderground` excludes Garrisoned on purpose.
- Reachability: `Simulation.Snapshot` lists the owner's garrisoned soldiers as ordinary units at the building centre with no flag (`Simulation.cs:775-782`). Nothing under `src/Game` mentions `Garrisoned`, `LayerOf` or `IsCarried`, so I found no selection filter.
- The AI is protected only by `AI/Recruit/RecruitKnowledge.cs:91` (`Mission == Garrisoned`); after a Stop the mission is Guard and that filter no longer matches.
- No test issues an order to a garrisoned soldier. Not executed; confirm with a 10-line Sim test.
**Impact:** A player (or in LAN, any peer sending a crafted command) gets invisible, unattackable infantry that only take damage through the building they left. Occupant counts are wrong and the neutral-ruin flip-back never sees the building empty.
**Fix idea:** In `Apply`, drop Move/Stop/Scatter/Attack/AttackMove/Guard/Harvest/Enter for a subject whose layer `IsCarried` (one helper beside `OwnsSubject`), with a test per command kind. S. Moves pinned hashes only if a pinned scenario issues such an order (none should).

### SIMDET-02 · major · SIM · confidence high (code), likelihood low-medium
**What:** When the building store is full, a vehicle that deploys into a building is removed first and the building is then not created: the unit is gone. The same full store makes Place fail silently while `CanPlace` still answers Ok.
**Where:** `src/Sim/Simulation.BurrowMove.cs:373-375` (`TryDeployInPlace`: `RemoveVehicleForDeploy(i); PlaceBuildingNow(...)`, result ignored), `Simulation.BurrowMove.cs:294-297` (`BeginBreakThrough`, comment "building store full: the Bore is lost"), `Simulation.cs:442-460` (`CanPlace` never checks free slots), `Simulation.Production.cs:199-202`, `Core/SimLimits.cs` (`Default = (2048, 2048, 512, 1024)`; no caller in src overrides it), `World/SlotArray.cs` (index 0 reserved, so 511 usable).
**Evidence:**
- `PlaceBuildingNow` returns `EntityId.None` when `_buildings.Add` cannot allocate.
- `Simulation.Rigs.cs:181-187` (`FinishUnpack`) does it in the safe order (place, then remove) and `FinishPack` has a put-back path, so the pattern is known; the two deploy paths were missed.
- Types that deploy today: the vehicles deploying into `dir_hatch`, `dir_shaft` and `exp_landing_depot` (`data/rules/vehicles.toml:93,205,875`), which are base founders.
- The 511 slots are shared by all players, neutral map features and every wall/gate piece (walls are buildings; `data/rules/buildings.toml` has 3 `wall = true` types).
**Impact:** In a long multi-player game with walls, a founder ship or Bore vanishes on deploy with no message. A player holding a finished structure sees a valid placement that never places.
**Fix idea:** Check for a free slot before removing the vehicle (refuse the deploy, keep the unit) and add `PlacementResult.StoreFull` (append-only) to `CanPlace`. S. No pinned hash moves unless a limit is changed; a limits change alters most parts' byte layout and every pinned hash.

### SIMDET-03 · minor · SIM · confidence high
**What:** A group path request dropped while still queued is never released, so its solved result stays in `PathService._results` for the rest of the game. `_results` is hashed every 16 ticks and written to every save.
**Where:** `src/Sim/Missions/MoveMission.cs:118-130` (`DropRequest`: a group calls `Poll`), `Pathing/PathService.cs:161-176` (`Poll` returns `Pending` for a queued request and releases nothing), `PathService.cs:134-155` (the queue is solved only when `budget > 0`), callers `Simulation.Movement.cs:42` and `OnActorRemoving`.
**Evidence:**
- `Process` skips solving on ticks where the previous batch overdrew the 4000-expansion budget; one long A* on a 128x128 map can cost several ticks of debt. During those ticks a group request sits in `_queue`.
- If a member gets a new order or dies, `DropRequest` -> `Poll(id, index)` hits the `_queue` branch, returns `Pending`, and the actor's `PathId` is set to 0.
- When the request is solved later, that index is never polled; `_results.Remove(id)` happens only when every entry is Unknown (`PathService.cs:172-174`).
- Single requests are fine: `Cancel` removes them from the queue.
**Impact:** Deterministic, so no desync, but the "paths" part grows without bound in long busy games: more bytes per hash pass and per save. Typical trigger: re-clicking a group move during a big battle.
**Fix idea:** Add `PathService.Release(id, index)` that marks the index abandoned while queued, and call it from `DropRequest`. S. Changes "paths" bytes only in games that hit the leak; re-check the golden and skirmish pinned hashes.

### SIMDET-04 · minor · CODE · confidence high (latent)
**What:** A sight radius above 64 passes rules validation but throws `ArgumentOutOfRangeException` inside Step.
**Where:** `src/Sim/Vision/VisionState.cs` (`MaxSightCells = 64`, `CheckSight` in `Reveal`), `Simulation.cs:705` (`UpdateVision`, every tick), loader bound `src/Data/UnitRulesLoader.cs:101,205` and `StructureRulesLoader.cs:52` (`f.Int("sight", 0, SimConstants.MaxMapCells)` = 128), upgrade effects `Rules/RulesDeriver.cs:168,181,192` (`Op(e, x.Sight)`, clamped only to >= 0).
**Evidence:** The largest base sight today is 10 and the sight effects are `add 2` and `mul 1300`, so nothing reaches 65. A data edit to 65..128, or stacked `mul` effects, would load cleanly and crash on the first tick the unit exists.
**Impact:** Hard crash mid-game from a data change the validator accepts.
**Fix idea:** Bound the loader and the sight op in `RulesDeriver` by `VisionState.MaxSightCells`. S. No hash change.

### SIMDET-05 · minor · CODE · confidence high (latent, non-default limits only)
**What:** Vision tables assume the default store limits. `GhostCapacity` is a constant 512 and the per-kind actor table is sized `max(Limits.Vehicles, Limits.Infantry)`, so a setup with more building slots throws inside Step. Separately, the save header accepts limits up to 1,048,576 while `SlotArray` rejects anything above 65,535 with `ArgumentOutOfRangeException`.
**Where:** `src/Sim/Vision/VisionState.cs` (`GhostCapacity`, `CheckGhostIndex`, `Slot`), `Simulation.cs:103-104`, `Serialization/SetupHeader.cs:25,83-86`, `World/SlotArray.cs:21`.
**Evidence:** `UpdateVision` calls `RecordBuildingSeen(viewer, i, ...)` and `Reveal(..., ActorKind.Building, i, ...)` with `i` up to `Limits.Buildings - 1`. Every current caller uses `SimLimits.Default`, so this bites only when someone raises the building limit, which is the obvious answer to SIMDET-02.
**Impact:** Raising `Limits.Buildings` above 512 crashes the first time a viewer sees building slot 512.
**Fix idea:** Size ghosts and the actor table from the limits; bound header limits by 65,535. S-M. Moves hashes only if sizes change.

### SIMDET-06 · minor · PROCESS · confidence high
**What:** The build string is written into every save and replay header but nothing compares or shows it. A replay from an older build with the same format number and rules hash is played with today's sim code and reported as a desync at tick N.
**Where:** `src/Sim/Serialization/SetupHeader.cs:129-153` (`CheckMatches` ignores `Build`), `Replay/ReplayWriter.cs:21` (`Version = 13`), `src/Game/Menu/ReplayPlayback.cs:127` (sets `DesyncTick` on the first hash difference).
**Evidence:** `grep '\.Build\b' src/Game src/Tools src/Scripting` finds no read of `ReplayData.Build` or `SetupHeader.Build`. The format number changes with layout only (HANDOFF line 164 asks helpers to avoid unconditional bumps), while logic changes alter outcomes without touching the rules hash or format.
**Impact:** Old replays kept as a regression corpus produce false desync reports that look like determinism bugs. A tester's real desync report cannot be told apart from "wrong build".
**Fix idea:** On mismatch, and in `replay verify`, print "recorded by build X, this is build Y"; warn up front when they differ. S. No sim change.

### SIMDET-07 · minor · CODE · confidence medium
**What:** A damaged or hand-edited save can crash with something other than `InvalidDataException`, or load and then fail inside Step, because several readers trust lengths and indices.
**Where:** `src/Sim/Pathing/PathService.cs:475,483,487` (`new CellCoord[r.ReadInt32()]`, `new Entry[r.ReadInt32()]`, no bound), `World/MobileStore.cs:149-168` and `World/BuildingStore.cs` Read (Player and TypeIndex unchecked), `Simulation.cs:876-886` (catches only `EndOfStreamException`).
**Evidence:** Other parts do validate (`StateIo.Slot`, `StateIo.Count`, the production queue length, the naval hangar slot). No checksum covers the save body.
**Impact:** Local single-player only today: a corrupt file gives an unhelpful crash. It matters more if M9 ever sends a save blob to a joining or resyncing peer.
**Fix idea:** Bound the two path counts with `StateIo.Count`; validate player and type index on read; store and check an XxHash64 of the body. S-M. No hash change.

### SIMDET-08 · minor · SIM · confidence high (small effect)
**What:** `Command.Sequence` is a `ushort` that wraps. Commands sort by (Player, Sequence), so in the tick where a player's counter wraps, that player's later commands apply before the earlier ones.
**Where:** `src/Sim/Simulation.cs:296-310` (`Compare`), `src/Game/Host/SimHost.cs:32,199`, `src/AI/AiDriver.cs:26,39` (`unchecked(Sequence++)`).
**Evidence:** An AI issues one command per unit order, so 65,536 per slot is reachable in a long game. Every peer sees the same numbers, so it is not a desync.
**Impact:** A rare one-tick ordering glitch (for example Build/Cancel swapped) that cannot be reproduced by hand.
**Fix idea:** Restart the counters at 0 each tick in host and driver, or compare with wrap-aware distance. S. Pinned hashes unaffected (no pinned game passes 65k commands).

### SIMDET-09 · idea · DESIGN · confidence high
**What:** What in src/Sim would block or hurt the M9 LAN lockstep milestone as the code stands.
**Where / evidence:**
1. `Command.Player` is trusted (`Simulation.cs:314` checks range and defeat only); still open from A-5. The net layer must overwrite it from the connection slot, neutral slots included.
2. Late commands are dropped and counted (`Simulation.cs:283`, `LateCommandCount`, not hashed). A command late on one peer and on time on another is a desync seen up to 16 ticks later.
3. Desync diagnosis: hashes every 16 ticks with sub-hashes naming the part, but no state is kept to diff (A-8, still open).
4. Store arrays and `Paths.Request/Cancel` are public and writable outside Step (A-4, partly fixed by `RequireSetupPhase`). No caller in `src/Game`, `src/AI` or `src/Tools` abuses it; the Scripting cash/defeat writes are an extension-side API I did not trace to their call time.
5. The AI runs outside the sim and feeds commands. In LAN exactly one peer may run each AI slot, or every peer must run it bit-for-bit; I found no decision on this.
6. Duplicate commands (same Player and Sequence) are both applied; the net layer must de-duplicate resends.
7. SIMDET-01 and SIMDET-07 become security-relevant once a remote peer can send commands or state.
**Fix idea:** Write items 1, 2, 5 and 6 into the M9 design note as contracts before any net code. M for the design, S each for code.

### SIMDET-10 · minor · TEST · confidence high
**What:** No test sends ordinary unit orders to actors in a carried or hidden layer, and nothing tests a full store.
**Where:** `tests/Sim.Tests/M4` (no Move/Stop/Attack on a garrisoned soldier), `M3` and `M7` (no deploy with a full building store), `Pathing` (no group request dropped while queued).
**Evidence:** `grep -rn "CommandKind.Move" tests/Sim.Tests/M4` finds only a rules-sweep test. SIMDET-01, 02 and 03 each have a one-screen reproduction that no existing test covers.
**Impact:** The command surface (26 kinds, 8 layers) is tested per feature only.
**Fix idea:** One table-driven test: every CommandKind against every ActorLayer, asserting the layer invariants after 50 ticks, plus a tiny-limits fixture such as `SimLimits(8, 8, 4, 8)` for full stores. M.

## Open questions
- SIMDET-01: can the in-game selection actually pick a garrisoned soldier (box-select over the building, control groups made before entering)? I found no filter in `src/Game` but did not read the selection code line by line.
- Is 511 buildings per game, walls included, a deliberate cap? I did not check DECISIONS for it.
- Is "bump the replay format only on layout change" the intended policy? If so, SIMDET-06 is the missing other half.
- The `SimSetup` doc (`Core/SimSnapshot.cs:84-86`) says Limits and AllowDebug are "hashed into the players part"; they are only in the save/replay header. Harmless, but the doc is wrong.

## Things checked and found sound
- Iteration: every loop I read is by ascending slot index; the only `Dictionary` (`PathService._cache`) is lookup-only; `_results` is a `SortedDictionary`; `ReplayPlayer`'s sort has a total key.
- RNG: one PCG32 per sim, saved and hashed; drawn only by scatter, salvo impact and the debug op.
- Command sort is total over every field. Defeated and out-of-range players are ignored. Debug is gated by `Setup.AllowDebug`, which skirmish and missions set false (`SkirmishGame.cs:166,252`, `MissionSession.cs:54`).
- Ownership and liveness checks are present in every command handler I read; stale ids are rejected by generation.
- Path cache: the key includes map version and passer; every input of `Factor` bumps `MapState.Version` (including `Touch()` on gate capture); a hit charges the stored expansion count; consumers copy the path array. A hit equals a fresh solve, so a load cannot diverge.
- Supply, emitter, region-label, regrowth, team-member and derived-rules caches are pure functions of hashed state with the invalidation their allow-list entries claim. `FinishLoad` marks emitters and supply dirty.
- Pending-damage buffer: every path that queues hits outside RunCombat (`ApplySpecial`, `RunSalvos`, `OnDeathWeapon`) flushes at once.
- "Written only when in use" encodings (tunnels vehicle carriers, stats shipments, naval hangar and ammo, effects salvos and storms, production Held bit and no-rebuild clocks, ground grouted mask): the omitted state is all-zero, reads back all-zero, and the sim never treats an all-zero record as in use.
- Slot stores: lowest-free allocation, generation skips 0, dead slots reset on Remove and on Read; side tables are cleared on spawn and removal.
- Fixed point: distances and products widen to long; `ISqrt` exact; `Facing.FromDelta` cannot divide by zero; the three int multiply-then-divide sites cannot overflow with sane data.
- Persisted enums carry explicit values; `MoveClass.NavalDeep = 64` fits the path part's 7-bit loco byte.
- Rules hash: `ruleshash_cov.py` found every record field in `src/Sim/Rules` referenced in `src/Data/RulesHash.cs` (`Veteran` through `CanRank`).
- Replay: the setup log is the single source of spawns; out-of-Step mutators are blocked after tick 0; a loaded game is not recorded; the host stamps ExecTick >= Tick + 1.
- Save/load tests: milestone folders M3 to M7 and the veterancy, supply, production and economy folders each have tests that call `Simulation.Load`, plus the 37-tick sweep and the AI save/continue tests.
- Earlier review A: A-3, A-6, A-8 (hash cost) and A-9 are fixed; A-4 mostly fixed; A-5 comparer and Debug gate fixed, Player trust still open.