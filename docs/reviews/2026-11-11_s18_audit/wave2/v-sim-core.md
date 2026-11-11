Package v-sim-core (verify only). Every assigned finding was reproduced by a test except three sub-claims, which are confirmed by code reading only and marked as such. No file under `src/` changed. Worktree `/home/claude/wt/s18-v-sim-core`, branch `wp/s18-v-sim-core`, 11 commits on top of e059f0f, working tree clean, nothing pushed.

COMMITS
- aa97ec3 test(sim): pin full building store losing deploying vehicles and ready structures; ready structure cannot be cancelled [S18-SIMDET-02] [S18-SP-02] [S18-SIM-RULES-03]
- dd9509e test(sim): pin Hatch morph charging Cash only, not Stored [S18-SIM-RULES-02]
- cb15308 test(sim): pin ranked stealth shooter staying hidden while firing [S18-SIM-RULES-04]
- ffa3aa9 test(sim,game): pin garrisoned soldier obeying orders and being selectable [S18-SIMDET-01] [S18-SIM-RULES-01]
- 24f465c test(sim): pin splash hurting passengers and Deploy bypassing unload_ticks [S18-SIM-RULES-05] [S18-SIM-RULES-06]
- 84ddc86 test(sim): pin RigMission checking the site with the packed vehicle's 3x3 type [S18-SIM-RULES-07]
- e22f51a test(sim): pin leaked group path result when a member is re-ordered while queued [S18-SIMDET-03]
- 7a60eba test(sim): perf probe for blocked-goal path stall and state hash cost (RTS_PERF=1) [S18-SP-01] [S18-SP-09]
- c846e64 test(sim): pin tanks dropping a Move at a one-cell choke after 13 blocked waits [S18-SP-03]
- b353f10 test(sim): pin harvester trapped by a walled-in nearer field [S18-SP-04]
- 27f0b87 test(sim): pin yard sell loop, carried-APC soldiers on water, damage-0 dealing 1, idle harvester after refinery rebuild [S18-SIM-RULES-09/10/12/13]

Tests are in `tests/Sim.Tests/S18/` (10 files) and `tests/Game.Tests/S18/GarrisonSelectionAuditTests.cs`. Each finding has `Audit_` tests that pass today and a skipped `Desired_` test; several also have a `Control_` test.

VERDICT TABLE
Format: finding | verdict | test | fixed? | note, then "Wave-1 exact?" and "Smallest fix / what moves".

1. **SIMDET-01 / SIM-RULES-01** | CONFIRMED | `GarrisonOrderAuditTests`, `GarrisonSelectionAuditTests` (Game.Tests, shipped rules) | no: awaiting owner
   - Move, Stop, Guard, Scatter, AttackMove and Attack all replace the Garrisoned mission while the layer stays Garrisoned.
   - After a Move the soldier walks 9 cells out and is still counted as an occupant. With an enemy seeing its cell it is hidden, untargetable and absent from the enemy snapshot.
   - Standing next to an enemy rifleman and mortar on Guard for 20 s it takes no damage and fires. A control soldier that never garrisoned is shot in the same spot.
   - UI reachability is confirmed: `Snapshot(0)` lists it as an ordinary unit with no flag. `Selection.Inside` (box), `Selection.Army` and `Selection.Pick` (plain click on the building centre) all return it, and the `OrderBuilder` Move then walks it out.
   - Wave-1 exact? Yes, and the open UI question is now answered yes.
   - Smallest fix: in `Apply`, drop Move/Stop/Scatter/Attack/AttackMove/Guard when the subject's layer `IsCarried`; in Game, skip such units in `Selection.Inside/Army/Pick` (needs a flag on `UnitView` or a `LayerOf` lookup). Moves no pinned hash or golden unless a recorded game orders a garrisoned soldier (none should). `SimSnapshot` is a contract type, so adding a flag needs the owner.

2. **SIMDET-02 / SP-02 / SIM-RULES-14** | CONFIRMED | `FullBuildingStoreAuditTests` | no: awaiting owner
   - With a tiny `SimLimits` and a full store, a land-train Deploy removes the vehicle and builds nothing; a Bore is lost at break-through with no telegraph.
   - A paid, ready structure cannot be placed while `CanPlace` and `CanPlaceReady` say Ok; the lane then reports StructureWaiting.
   - Wave-1 exact? Yes for the store-full part. SIM-RULES-14's extra claims (deploy skips the gap rule and the "structures built" counter) were not tested.
   - Smallest fix: check for a free building slot before `RemoveVehicleForDeploy` in `TryDeployInPlace` and `BeginBreakThrough`; append `PlacementResult.StoreFull`. No hash moves at default limits unless a game fills 511 slots.

3. **SIM-RULES-02** | CONFIRMED | `MorphCostAuditTests` | no: awaiting owner
   - Cash 299 plus Stored 2000 (cost 300) is refused. Cash 300 is charged from Cash only and is not added to CreditsSpent.
   - Wave-1 exact? Yes.
   - Smallest fix: `if (Funds(p) < cost) return; Spend(p, cost);`, and the sidebar offer should read Cash+Stored. Changes players/stats bytes only in games where a morph is paid; any Directorate golden or AI replay that morphs a Hatch needs re-pinning.

4. **SIM-RULES-03** | CONFIRMED | `FullBuildingStoreAuditTests.Audit_ReadyStructure_CannotBeCancelledOrRefunded…`, `…Audit_QueuedItemBehindAReadyStructure…` | no: awaiting owner
   - Cancel at position 0 or -1 leaves the ready item with no refund; the lane refuses new builds and is unchanged 2000 ticks later.
   - Extra: Cancel position 0 while an item is ready cancels the item queued behind it.
   - The "no legal site" cases (beachhead lost, no shore, walled in) were not reproduced; the store-full case in row 2 is one proven instance.
   - Wave-1 exact? Yes.
   - Smallest fix: Cancel with Arg2 = -1 refunds the type cost and clears Ready, plus a UI gesture. No hash moves (new command value only).

5. **SIM-RULES-04** | CONFIRMED | `StealthFireAuditTests` | no: awaiting owner
   - A recruit is unhidden on every shot. At rank 1 and rank 3, at least half the shots in 300 ticks leave it stealthed.
   - The aura variant was not tested; it is the same code line.
   - Wave-1 exact? Yes.
   - Smallest fix: `Fire` zeroes the still counter directly. Moves hashes only in games where a ranked or aura-buffed stealth unit fires.

6. **SIM-RULES-05** | PARTLY | `TransportAuditTests.Audit_Splash_…`, `Audit_SecondSplash_…` | no: awaiting owner
   - Confirmed by test: passengers in a truck are untargetable, yet a splash at distance 1 takes 25 hp from each (the same hit as the truck). Two splashes kill a rider inside a living transport.
   - Docked carrier aircraft and tunnel transit: not tested, code reading only (same loop, position parked on the carrier or exit).
   - Wave-1 exact? Yes for passengers; the rest is unproven by test.
   - Smallest fix: skip `IsUnderground` layers in `Detonate`. Moves hashes in any game where splash lands on a loaded transport, carrier or tunnel exit; expect skirmish and AI goldens with transports to shift.

7. **SIM-RULES-06** | CONFIRMED | `TransportAuditTests.Audit_DeployOrder_…` (two tests) | no: awaiting owner
   - Unload lands one marine per 5 ticks (control). Deploy puts all three ashore in the tick of the order.
   - A Skyhook 12 ticks into a flight drops everyone in one tick and keeps flying.
   - Wave-1 exact? Yes.
   - Smallest fix: `ApplyDeploy` routes landing transports to `ApplyLandingUnload`, or refuses. Moves hashes only where Deploy is sent to a landing craft or Skyhook; the game's D key does this, so check Expedition goldens.

8. **SIM-RULES-07** | CONFIRMED | `RigUnpackAuditTests` | no: awaiting owner
   - A 1x1 rig next to a building: the 1x1 site is legal and the Pack order is accepted, but the mission drops to Guard and the rig never leaves its cell. On open ground it unpacks (control).
   - Wave-1 exact? Yes.
   - Smallest fix: use `sim.UnpackTypeOf(i)` in `RigMission.Handle`. Moves hashes in Left Behind games that unpack a small rig, including the approach cell and where unpacking starts.

9. **SIMDET-03** | CONFIRMED | `PathLeakAuditTests` | no: awaiting owner
   - Service level: a Poll of a queued group entry releases nothing, and the result is still stored after 1000 Process calls.
   - Sim level: two tanks get a group Move while the service is in debt, one is re-ordered the next tick; 1500 ticks later the paths part is larger than empty. The control run ends empty.
   - Wave-1 exact? Yes.
   - Smallest fix: `PathService.Release(id, index)` that marks the index abandoned while queued, called from `DropRequest`. Changes "paths" bytes only in games that hit the leak; long AI goldens may.

10. **SP-01** | CONFIRMED, worse than claimed | `PathStallPerfProbe.Sp01_…` (RTS_PERF=1) | no: awaiting owner
    - Numbers are below. Wave 1 estimated about 3 s without pathing; measured 22.6 s.
    - Wave-1 exact? Mechanism yes, size underestimated.
    - Smallest fix: retarget a blocked goal to the nearest passable cell before searching, and cap one `Process` call. Moves every hash that involves an Attack on a building, so essentially all skirmish and AI goldens.

11. **SP-09** | PARTLY | `PathStallPerfProbe.Sp09_…` | no: perf, no behaviour change
    - Mechanism is real, but the cost is 3–5 ms per hash in Release against the 9–15 ms estimate.
    - Wave-1 exact? Size overestimated by 2–3x.
    - Smallest fix: a buffered sink writing the same bytes. No hash moves.

12. **SP-03** | CONFIRMED | `ChokeGiveUpAuditTests` | no: awaiting owner
    - Group Move through a one-cell gap: 20 tanks all get through; of 40, 3 stop on the near side; of 60, 9. The gap is empty at the end.
    - Exploratory run, not pinned: 60 slow tanks (speed 11) left 21 behind. First give-up at tick 158–349.
    - Wave-1 exact? Yes for Move. The claim that attack/harvest callers stop detouring was not tested.
    - Smallest fix: do not count a wait while the blocker is an allied mover. Moves hashes of any game with a jam; most goldens.

13. **SP-04** | CONFIRMED | `HarvesterUnreachableFieldAuditTests` | no: awaiting owner
    - With a nearer field ringed by rock and an open field further away, 4000 ticks (200 s) give 0 credits; the far field is untouched and the harvester loiters by the ring. Both controls earn.
    - `PickRefinery` was not tested.
    - Wave-1 exact? Yes.
    - Smallest fix: ban cells within about 4 of a bad cell until `Map.Version` changes, or add a region check. Moves hashes only where a harvester meets an unreachable field.

14. **SIM-RULES-09** | PARTLY | `MinorRulesAuditTests.Audit_YardPlaceAndSell_…` | no: awaiting owner (numbers)
    - Shipped data: yard 600, gleaner 600, refund 300. Five place-and-sell cycles give five gleaners plus 5 x 300.
    - "Past the supply cap" was not tested (code reading only).
    - Wave-1 exact? Yes for the loop.
    - Fix is a design or number choice; it changes `data/` and the rules hash.

15. **SIM-RULES-10** | CONFIRMED | `MinorRulesAuditTests.Audit_SoldierInACarriedApc_…` | no: awaiting owner
    - The ship and the APC die; the marine is alive, layer Surface, on a deep-water cell where foot speed is 0.
    - Wave-1 exact? Yes.
    - Smallest fix: `OnTransportDying(v)` in `LoseVehiclePassengers`. Moves hashes only in that case.

16. **SIM-RULES-12** | CONFIRMED | `MinorRulesAuditTests.Audit_ZeroDamage…`, `…Audit_ShippedData_…` | no: awaiting owner
    - `ComputeDamage(0, all-zero warhead)` is 1 for every armour class. The shipped data has exactly the five named damage-0 weapons.
    - Wave-1 exact? Yes.
    - Smallest fix: no hit when weapon damage is 0. Moves hashes wherever those five trucks fire at enemies.

17. **SIM-RULES-13** | CONFIRMED | `MinorRulesAuditTests.Audit_Harvester_StaysIdle_…` | no: awaiting owner
    - After a rebuild the old harvester stays on Guard with its cargo, unmoved for 2000 ticks, while the new refinery's free harvester earns.
    - Wave-1 exact? Yes.
    - Smallest fix: `RunEconomy` restarts Guard-idle harvesters when an own refinery exists. Moves hashes after any refinery loss and rebuild, and would also restart harvesters a player parked on purpose (owner call).

MEASURED (Release, `RTS_PERF=1`, flat 128x128, fixture rules; raw output in `/home/claude/audit/v-sim-core/perf_run1.txt` and `/home/claude/audit/v-sim-core/perf_run2.txt`)

| Scenario | Order tick (wall) | Expansions | Ticks with no path solved | Bystander's 6-cell Move started after |
|---|---|---|---|---|
| Control: 20 tanks Move next to the building | 11–13 ms | 14,868 in one batch | 2 | 7 ticks |
| 20 tanks Attack the building | 171–182 ms | 327,500 in the order tick; 1,899,511 over 21 overdrawn batches | 452 of the next 552 (22.6 s) | 83 ticks (4.2 s) |
| 1 tank Attack the building | about 16 ms | 16,375 | 3 | n/a |

- Budget is 4000 expansions per tick; one expansion costs about 0.55 µs.
- The stall lasts far longer than wave 1 estimated because the batch is re-run about every 10–30 ticks (chase retarget).
- The control group Move also overdraws once (14,868), which is SP-13's no-heuristic group search.

`ComputeStateHash` per call, average of 200:

| Sim | Hash cost | Save size |
|---|---|---|
| Generated Large, 4 players | 5.1 ms | 773 KB |
| Generated Large, 2 players | 2.9–3.6 ms | 703 KB |
| Flat 128, 4 players, 600 tanks | 3.4–4.1 ms | 830 KB |
| Flat 64, 2 players | 1.4–1.6 ms | 280 KB |

Spread over 16 ticks the Large 4-player hash is about 0.32 ms per tick, roughly a quarter of wave 1's 1.13 ms per tick, and one 5 ms spike uses about 80% of the 6.25 ms tick budget at 8x speed.

CHECK
- `dotnet test tests/Sim.Tests --filter "FullyQualifiedName~Rts.Sim.Tests.S18"` (Debug): 57 tests, 42 passed, 15 skipped, 0 failed.
- `dotnet test tests/Sim.Tests --filter "FullyQualifiedName~Rts.Sim.Tests.S18|FullyQualifiedName~SpdxHeader|FullyQualifiedName~Repo"`: 92 tests, 0 failed.
- `dotnet test tests/Game.Tests --filter "FullyQualifiedName~Rts.Game.Tests.S18"`: 3 tests, 2 passed, 1 skipped.
- `RTS_PERF=1 dotnet test tests/Sim.Tests -c Release --filter "FullyQualifiedName~S18.PathStallPerfProbe"`: 2 passed, run twice; the probe returns at once without the variable.
- Full suite not run, per the brief. `git diff origin/main -- src` is empty.

NEW FINDINGS
- NEW-1 · minor · SIM: Cancel at position 0 while a structure is Ready cancels the item queued behind it, not the ready one. A player trying to cancel the stuck READY cameo loses the next building. Pinned by `Audit_QueuedItemBehindAReadyStructure_IsCancelledInstead…`.
- NEW-2 · major · UI: the commonest way into SIMDET-01 is not box-select. A soldier ordered to Enter stays selected (`GameScreen.cs:475` prunes only on owned-and-alive), so the next right-click on ground walks it out invisible. Also, a plain left click on a garrisoned building's centre picks the soldier, because `Selection.Pick` runs before the building pick.
- NEW-3 · minor · PERF: a 20-unit group Move across a 128 map costs 14,868 expansions (3.7 ticks of budget) because the group search has no heuristic (SP-13, now measured).
- NEW-4 · minor · SIM (observed once, not pinned): in the choke scene, 40 slow tanks never all reached Guard within 6000 ticks; some on the far side kept their Move. Consistent with SP-06 or the arrival shuffle; not investigated.
- NEW-5 · note · TEST: a player whose last building dies is defeated and its commands are silently ignored, and a rebuilt refinery brings a free harvester. Both hid the real result while writing the SIM-RULES-09 and -13 tests; anyone testing "rebuild" by hand will be misled the same way.

QUESTIONS FOR THE OWNER
1. Garrisoned soldier given an order: (a) ignore the order, (b) treat Move as "leave the building, then move". I recommend (a) in the sim plus hiding garrisoned soldiers from selection; (b) is a nicer feature but a design change.
2. Full building store: (a) refuse the deploy and report `StoreFull` from `CanPlace`, keeping 511, or (b) raise the limit (changes every pinned hash, and `VisionState.GhostCapacity` must follow). I recommend (a) now and (b) as a separate decision.
3. Ready structure: allow cancel with full refund? I recommend yes; it has no hash impact.
4. Splash versus passengers, docked aircraft and tunnel transit: spare them all, as every other area effect does? I recommend yes; decide air/ground splash separately.
5. Deploy on a landing craft or Skyhook: route to the timed landing or refuse? I recommend routing for craft and refusing for an airborne Skyhook.
6. SP-01 is the one I would schedule first: 20 units attacking a building stalls all pathing for about 22 s. The fix moves nearly every golden; is one re-pin batch acceptable?
7. SIM-RULES-13: should a harvester a player parked with Stop also restart when a refinery exists? I recommend restarting only those that went idle for lack of a refinery, which needs one flag in hashed state.
8. SIM-RULES-09: is one free harvester per yard intended every time, or once per player?

USER CHECK
No fixes in this package. To see the two worst in the game:
- Garrison a rifleman in a Blockhouse, keep him selected, right-click open ground near an enemy: he walks out and the enemy never shoots him.
- Select 20 tanks, right-click an enemy building across a Large map, then order any other unit to move: it waits several seconds before starting.