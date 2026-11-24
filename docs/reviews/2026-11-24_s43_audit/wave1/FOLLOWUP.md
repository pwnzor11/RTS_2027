# FOLLOWUP — status of the session 18 audit's open items, plus a performance pass
Scope read: docs/reviews/2026-11-11_s18_audit/README.md (all), s19_numbers.md/s19_text_changes.md (build-later list), docs/DECISIONS.md D-202..D-223, D-308..D-310,
docs/ROADMAP.md M13 section, docs/balance/s22_balance_options.md Part C, git log (M13-xx tags), tests/*/S18/*, tests/AI.Tests/S19/ExpeditionSeaSweepTests.cs;
code: src/Sim (Combat, Ground, Effects, Submerge, Shipments, Missions/HarvestMission, Pathing/PathService, Map/MapState), src/Game/Render (SceneRenderer, UnitRenderer,
UnitLayerPlan, UnitInterpolator, RenderMemory, DamageTiers, PuffField, GroundOverlay). Nothing was built or run (no SDK); "script" evidence = grep/git accounting script.

## Part 1 - accounting table (README sections 2.1 / 2.2, items 1-26)
Method: for each item, a script (/tmp/acct.py, not kept in the repo) greps for the fix's code marker and takes the first git commit tagged with the M13 id; I then read
the marker in context. Only 2 tests in the whole repo are still `Skip` (AiStallAuditTests AI-09, AudioProvenance); every S18 `Desired_` twin is active (grep `Skip *= *"`).
D-205 took every recommendation (items 1-26), so "fixed" = the recommended option.

| # | Item | Status | Evidence |
|---|------|--------|----------|
| 1 | Expedition on Mountains L1 sea | FIXED | M13-01: c1920b7a (edge-sea channel), e1a6d7da (SkirmishStart whole-map fallback), test 736a0e14 ExpeditionSeaSweepTests; later map changes D-308 (see Suspects S-1) |
| 2 | Expedition with no building never defeated | FIXED | 1dc4d34d, Simulation.NoRebuild.cs:15 (founder counts) + Production.cs:287 EverHadBase |
| 3 | Tender spawns top-left | FIXED | 7e78ad52, Simulation.Shipments.cs:229,265 (edge cell nearest dock) |
| 4 | Garrisoned units obey orders / selectable | FIXED | fbc90d04, 2e073878, a37c27d9; Simulation.cs:374 |
| 5 | 20 units on building stall pathing | FIXED | D-209; 7d63c3fc, 039c3620, f38442e5, fbac83bf; PathService.cs:43 BurstFactor, Process() :176 |
| 6 | Ready structure blocks lane | FIXED | 411c4567, 8edd292f, 37a28da5 (AI cancels after 3 failed searches); BuildOrderManager.cs:95. D-218(5) later removed CancelAfterBlockedSearches key as 0-use |
| 7 | Full 511-slot store | FIXED | 1cfa2536, PlacementResult.StoreFull, BurrowMove.cs:292,351, PlacementCheck.cs:77 |
| 8 | Atomic Battery cannot fire | FIXED | a2317a05, buildings.toml:569 range 0 (Directorate Sunrise keeps 8 at :433, correct: it has entrances) |
| 9 | LB lanes run while rig packed | FIXED | 6e44475d, Simulation.Rigs.cs:116 |
| 10 | Campaign tech lists | FIXED | c5ae9983, 2a18122e; tests/Scripting.Tests/S19/CampaignAllowListTests.cs; D-207(1) |
| 11 | Start-side tie-breaks | FIXED | M13-10 9834108b, 502942c0; D-222 after-series 92 % -> 54 %; D-223 KitFlip |
| 12 | Splash air/ground, 0 means 0 | FIXED | 2fcb98ad; Simulation.Combat.cs:399-405 |
| 13 | Splash hurts passengers | FIXED | 2fcb98ad; Simulation.Combat.cs:401 (Passenger, Tunnel spared; Garrisoned at :400) |
| 14 | Dived sub fires / surfaced armour | FIXED | 07984ca3, 3b182f8e; Simulation.Combat.cs:267, Submerge.cs:23 (5 s reveal), ffb72d1d (ship_light) |
| 15 | Veteran stealth fires | FIXED | 07984ca3; Combat.cs:267 comment SIM-RULES-04 (one shared line, so aura variant is covered) |
| 16 | Deploy on landing craft / Skyhook | FIXED | f61f3f83; TransportAuditTests Desired_DeployOrder... + DeployOrder_OnAFlyingSkyhook_IsRefused |
| 17 | Small LB rig unpack check | FIXED | 80762fc5 (RigUnpackAuditTests) |
| 18 | Hatch->Bore morph funds | FIXED | 7281340f (MorphCostAuditTests) |
| 19 | Melee acquire on attack-move/guard | FIXED | 6152db17, D-218(4); CombatMissions.cs:168-181 AcquireReach |
| 20 | Harvester traps / choke give-up | FIXED | rebuild idle 36ec71e5; unreachable field SP-04 D-218(3); choke 747fda6d + D-218(1). Related residue still open: M13-40 "1-wide corridors between AI buildings still jam head-on" (ROADMAP:365) |
| 21 | Kill value = cost | FIXED | d3d87bb4 (buildable=false), D-207(3) = 1500 |
| 22 | Group path leak into hash | FIXED | D-218(2), PathService.ReleaseMember :247 |
| 23 | Ashfall hazard | FIXED | D-207(2), D-211, 5f8b2ac6, 30fdd252; missions retuned with ash on M13-17 (D-222) |
| 24 | Attack-ground | FIXED | 55cab444 (Ctrl+right-click), D-205 |
| 25 | Searchlight/Mast at zero power | FIXED | 7dfd61cf (GroundEffectAuditTests Desired_UnpoweredSearchlightAndMast_DoNothing) |
| 26 | Promised-but-missing mechanics | PARTLY: hidden/reworded = FIXED (08504de0, cf5e9c61); "build later" = NOT TRACKED | see FOLLOWUP-01 |

Section 2.3 (numbers, unnumbered): applied D-208 (Agitprop, Tow 1750, Marksman, ship small-arms/AP, Directorate copy structures -20/-15 %, Commissar aura, layout-2 money) and
D-223 (Bulwark, Howitzer 32, Dirigible 16, Reactor Crawler, repair auras 5, LB Harpoon "both", Refinery Rig 700, tut_01 cash 4500 [verified tut_01/mission.toml:25],
Salvo Array 4800 [buildings.toml:1223]); item 1 (HE vs ships) kept 100/100 (decided-not-to-fix, D-223). NOT closed: s22 Part C row 13 "Recruit AI wiped two openings at 4-5 min"
(needs a hand check; M13-13 is still `[~]`, ROADMAP:291) and "LB yard sell loop" (see FOLLOWUP-02). Balance itself is frozen (D-310).
Section 2.4 (UI/wording): ROADMAP M13-14 `[x]`; spot-checked in code: Autosave.cs, quicksave keys, focus pause (RtsGame IsActive), ConfirmScreen, SellConfirm.cs, control groups in
GameSaveHeader, `dev = true` on test_32/test_64 maps, Borderless in Settings.cs. MIS-10/12/16/18 picks recorded in D-207.
Unclosed owner-decision leftover: AiStallAuditTests.cs:214 `RepairManager_KeepsRepairOn_WhileHealthKeepsFalling` is still `Skip = "S18 AI-09: awaiting owner decision"`
and the pinned twin (:204, expects orders at decisions 0,4,8) still asserts the on/off flip. No decision id mentions AI-09 (grep DECISIONS.md). Listed as FOLLOWUP-03.

## Findings (STILL OPEN only)

### FOLLOWUP-01 · LOW · debt
What: item 26's "build later" mechanics (Minelayer mines, Combat Diver swimming, Drydock production/repair, Destroyer depth charge, Harpoon tether, Grapple breach, Salvage Rights,
Sonar Nets, Scrap Plate, Patchwork) were hidden or reworded in tooltips only; the owner's "build later" has no ROADMAP/OPEN_QUESTIONS entry. The list lives only in
docs/reviews/2026-11-11_s18_audit/s19_text_changes.md:137-148. Archive lore still describes the unbuilt mechanics (D-207(5), by design).
Where: docs/ROADMAP.md (no match for minelayer|drydock|grapple|harpoon wagon|sonar net), src/Game/Ui/Sidebar/TodayText.cs
Evidence: script — grep -i across ROADMAP/OPEN_QUESTIONS/HANDOFF/SESSION_LOG finds none (only docs/archive/m8_13_unlocks.md lists the ids as unlocks).
Impact: players who unlock exp_minelayer/exp_drydock in the campaign (exp_02/exp_03 grants) get units/buildings that do nothing; the plan to build them can be forgotten.
Fix idea: add one ROADMAP line ("M15?: build-later list, see s19_text_changes.md") or record a decision to drop them.   Changes a match outcome / number / visible text?: no

### FOLLOWUP-02 · LOW · balance
What: the Left Behind Yard place-and-sell loop (README 2.3 "LB yard sell loop"; free Gleaner with each 600-credit Yard that refunds 300) was never decided; the S18 pin test still asserts today's behaviour.
Where: data/rules/buildings.toml:675 `free_unit = "lb_gleaner"`; tests/Sim.Tests/S18/MinorRulesAuditTests.cs:81 `Audit_YardPlaceAndSell_GrantsAFreeHarvesterEveryCycle_ForHalfItsPrice` (600 cost, 300 refund, net 300 per 600 harvester)
Evidence: read — no D-xxx or ROADMAP line mentions the loop (grep "sell loop|yard sell|SIM-RULES-09" in DECISIONS/ROADMAP/OPEN_QUESTIONS/balance: none); pin test still `[Fact]` without a twin.
Impact: a human can buy harvesters at half price indefinitely; the AI does not exploit it (Q: AI sells only under EconomyRescue). Numbers are frozen (D-310), so this needs an explicit "keep" record.
Fix idea: record D-xxx "accepted, matches Authority/Directorate/Expedition yards" or cap free_unit to once per player.   Changes a match outcome / number / visible text?: yes (if capped)

### FOLLOWUP-03 · LOW · debt
What: AI-09 (RepairManager flips repair on/off every 4 decisions while a building keeps losing health) is parked as a skipped test "awaiting owner decision" and was never put to the owner after session 18.
Where: tests/AI.Tests/S18/AiStallAuditTests.cs:196-226
Evidence: read — test comment "on, off after 4 decisions, on again after 4 more"; skip reason unchanged; DECISIONS.md has no AI-09.
Impact: AI buildings under fire toggle repair; minor combat inefficiency only.
Fix idea: ask owner or delete both tests with a "keep" decision.   Changes a match outcome / number / visible text?: yes (AI behaviour)

### FOLLOWUP-04 · LOW · debt
What: 2.3 row 13 (Recruit AI wiped two openings at 4-5 min) is still waiting for the "hand check" that s22 asked for; M13-13 stays `[~]` for it.
Where: docs/balance/s22_balance_options.md:99, docs/ROADMAP.md:291
Evidence: read — no later doc mentions the hand check; D-310 froze balance without closing it.
Impact: none unless the owner still wants it; leaves a permanent half-open roadmap item.
Fix idea: tick M13-13 with "dropped by D-310" or run the check.   Changes a match outcome / number / visible text?: no

### FOLLOWUP-05 · LOW · debt
What: stale ROADMAP text: M12-12b still says "Open: Glass Coast layout 1 Expedition start skew ... (M13-03)" although M13-03 is done and the skew was later re-measured (D-277/D-308).
Where: docs/ROADMAP.md:273
Evidence: read.   Impact: misleading status.   Fix idea: edit the line.   Changes a match outcome / number / visible text?: no

## Part 2 - performance by reading (no measurements; counts use capacities UnitStore/InfantryStore 2048, BuildingStore 512, 20 ticks/s)

### FOLLOWUP-06 · MED · perf
What: no spatial index. Every target acquisition, splash, sonar/stealth/blackout visibility query and death clean-up walks all three actor stores up to Capacity (about 4608 slots, bit-tested) per call.
Where: Simulation.Combat.cs:184-205 AcquireTarget (called from RunBuildingCombat :518 every 15 ticks per building, CombatMissions.cs:57/:133 every 5 ticks per guarding/attack-moving unit); Detonate loop Combat.cs:387-420 (every shot/impact); OnCombatActorRemoving Combat.cs:556-566 (every death: 4608 CombatEntry reads plus projectiles)
Evidence: read — loops `for (int i = 1; i < cap; i++)` with `slots.Capacity`, not live count. Estimate: 300 units on guard = 60 scans/tick x ~4600 slots + candidate work = ~280k iterations/tick before any pathing; each candidate building also runs TargetPoint's footprint loop before the range test (Combat.cs:195).
Impact: scales with capacity not with army size; fine at 300 units, a visible tick-time cost at 1000+ units or mass-death ticks (100 deaths = ~460k reads).
Fix idea: iterate the SlotArray bitset words (only live slots) first; later a cell-bucket grid for range queries.   Changes a match outcome / number / visible text?: no (must keep slot order for ties)

### FOLLOWUP-07 · MED · perf
What: visibility predicates are O(all actors) and sit inside the per-candidate loop of AcquireTarget, making a scan O(n^2) whenever stealth units, hidden subs or Blackout exist.
Where: Simulation.Submerge.cs:29-47 IsSonarRevealed (units 2048 + buildings 512 per hidden sub per candidate check); Simulation.Ground.cs:111-125 CanSeeThroughBlackout and :230-245 IsStealthRevealed (all three stores); reached via Simulation.M3.cs:197-198 IsHiddenFrom <- IsUntargetableBy <- AcquireTarget Combat.cs:203, and Players.cs:92 IsVisibleTo when the cell has Blackout.
Evidence: read — AcquireTarget calls IsUntargetableBy for every in-range enemy; each stealthed/dived candidate triggers a full sweep. Example: 40 own shooters scanning every 5 ticks vs 20 Ash-stealth soldiers in range = 40/5 x 20 x 4600 = ~740k slot reads per tick.
Impact: late-game stealth/sub/Blackout fights are where tick time would spike (Expedition navy vs ground).
Fix idea: per-tick cached "revealed cells" bitmap per team (built once per tick from sources), then O(1) lookups.   Changes a match outcome / number / visible text?: no

### FOLLOWUP-08 · LOW · perf
What: aura recompute is entities x aura sources every tick, with ordinal string compares per pair; harvester dock claim scans all unit slots.
Where: Simulation.Effects.cs:127-170 RecomputeAuras + AuraTargetMatches :175-178 (string.Equals on typeId and "class:..." per source per entity); HarvestMission.cs:469-474 TryClaim (each waiting harvester, every QueuePollTicks=3, scans 2048 unit slots), :266 ClaimedByOther (per candidate ore cell in FindRich :235), :171 ClearDockFor (all building slots)
Evidence: read.   Impact: small today (few aura sources); TryClaim grows with queued harvesters at one refinery.   Fix idea: precompute target-type bitmask per source; keep a per-dock wait list.   Changes a match outcome / number / visible text?: no

### FOLLOWUP-09 · MED · perf
What: depleting a single ore cell bumps `MapState.Version`, which invalidates the whole path cache and the per-locomotion region labels (full-map flood fill on the next request per loco/passer combination).
Where: Map/MapState.cs:76-83 SetOverlay (`IsResource(old) != IsResource(new)` -> Version++), called from HarvestMission.cs:~150 on the last density unit; consumers PathService.cs:324 (cache key includes `_map.Version`), :363-368 RegionsFor (Version/Epoch match), :379 flood
Evidence: read — S19 M13-12 added the IsResource clause so soldiers see ash cost changes; it also fires on every harvested-out cell.
Impact: on ore-heavy maps with many harvesters the cache hit rate drops and region labels rebuild several times per second (W x H cells each, up to 8 slots). Not a stall, but wasted CPU in the economy-heavy mid game; matters most on Large maps.
Fix idea: bump only when an overlay that changes ground cost for Foot units appears or disappears AND ash path penalty is on, or keep a separate "cost version" that ignores harvest depletion for non-Foot locomotion.   Changes a match outcome / number / visible text?: no (cache only affects speed; D-209 says eviction never changes results, but verify region labels do not feed results)

### FOLLOWUP-10 · LOW · perf
What: path polling and release are linear scans of the request queue; a stall of hundreds of queued requests makes every poller O(queue).
Where: Pathing/PathService.cs:224-228 Poll, :247-259 ReleaseMember (both `for i < _queue.Count`)
Evidence: read.   Impact: after D-209 the queue is capped per tick, so bounded; only visible during a 100+ unit mass order.   Fix idea: Dictionary id -> Req.   Changes a match outcome / number / visible text?: no
Pathing budget itself: 4000 expansions/tick, one Process may charge up to 4x (BurstFactor 4) then debt delays later batches (PathService.cs:115,176-198); a single huge request is not capped, but ReachableGoal (:334) removes the unreachable-goal case that caused SP-01. Looks sound.

### FOLLOWUP-11 · MED · perf
What: per-frame garbage in the scene draw. (a) UnitLayerPlan.For allocates a `new List<UnitLayer>(3)` (plus its array) for every visible unit every frame and UnitRenderer enumerates it through `IReadOnlyList` (boxed enumerator, a third allocation). (b) SceneRenderer.DrawSprites passes a lambda capturing `frame` to `_walls.Observe` every frame (closure + delegate allocation). (c) Three per-tick observers build new Dictionary/HashSet objects sized to all units each sim tick.
Where: Render/UnitLayerPlan.cs:67 (called from UnitRenderer.cs:54, foreach :56); Render/SceneRenderer.cs:313-314; Render/RenderMemory.cs:45,53,54 (Observe, 3 collections per tick); Render/DamageTiers.cs:71,81 (2 per tick); Render/PuffField.cs:112 (1 per tick); GroundOverlay.cs:72 GroutRects() new List<Rectangle> every frame whenever ground data exists
Evidence: read — contrast Render/UnitInterpolator.cs:24 which reuses its dictionary ("reused: no dictionary per frame"), so the pattern is known and was fixed once (S18 "per-frame allocation fixes").
Impact: 300 visible units x 60 fps = ~54k small allocations/s from (a) alone; 6 dictionaries x 20 ticks/s with growth/rehash for (c). Gen0 GC churn shows as micro-stutter on weak laptops (owner plays on a laptop, D-176/D-243).
Fix idea: UnitLayerPlan.For fills a caller-owned Span/struct list; cache GroutRects in a static; swap two reusable dictionaries in the observers; make the walls predicate a cached delegate.   Changes a match outcome / number / visible text?: no

### FOLLOWUP-12 · MED · perf
What: no view culling in the sprite pass and several full-store sweeps per frame at 60 fps for data that only changes at 20 ticks/s.
Where: Render/SceneRenderer.cs:330-342 (every building and every unit is added to `_items` and sorted every frame, drawn through BuildingRenderer/UnitRenderer with no on-screen test — grep for Cull/VisibleCells in UnitRenderer.cs and BuildingRenderer.cs finds nothing); SceneRenderer.cs:316-327 (CopyOwn + CopyVisible = two full 2048+2048+512 sweeps with fog checks, per frame); :159 PlanWrecks, :174 PlanStorms, :185 RankLookup.Fill + :186-188 dictionary rebuilt from `_ranks` every frame; UnitInterpolator.cs:24-33 refills `before` with all previous units every frame.
Evidence: read.
Impact: cost is O(all units on the map), not O(visible); a 4-AI Large map with 800 units pays for all of them every frame.
Fix idea: cull by camera rect before `_items.Add`; recompute submerged/rank sets only when snapshot.Tick changes (like RenderMemory.Observe's `snapshot.Tick == _tick` guard); build the interpolation `before` map once per tick.   Changes a match outcome / number / visible text?: no

## Suspects (unproven)
- S-1: D-308 narrowed Glass Coast `ocean_width` 6 -> 3 (biomes.toml:92) after M13-35's 7-wide Expedition landing bay (f867a604) and after the Expedition sea sweep (which covers Mountains L1 only, ExpeditionSeaSweepTests.cs; no "glass" match). Would prove: run the sweep for Glass Coast layout 1 and 2 over the 50 seeds x 3 sizes and check a Command Ship/tender can reach open sea and the landing bay fits.
- S-2: SceneRenderer.cs:159 and :174 call `frame.Sim.ViewFor(0)` (player 0) for wrecks and storms, while the neighbouring lines use `snapshot.Viewer`. If the local player or a spectator is not slot 0 (LAN M9 client, hot-seat, spectate), the wrecks/storms drawn follow player 0's fog. Would prove: LAN client as slot 1 and compare wrecks shown under fog.
- S-3: FOLLOWUP-09 effect size is unmeasured; a `PathService` counter of region rebuilds per minute in a 4-AI Large match would show it.
- S-4: README 2.3 row "tier-1 defences outranged" — Rocket Truck got range/damage changes (D-229, D-239, D-280, D-290) but no decision states whether option B of s22 row 5 was applied; frozen anyway (D-310).

## Checked and fine
- All 26 numbered items have a fix commit tagged M13-xx and a marker in current code (table above); no regression of items 1-25 found by reading the marker lines.
- Only 2 skipped tests remain repo-wide; all S18 `Desired_` twins are live `[Fact]`s.
- Item 8 data: Directorate Sunrise keeps range 8 (it has entrance buildings); the three other superweapons are 0 (buildings.toml:433,569,890,1224).
- Pathing: per-tick budget with BurstFactor, FIFO waited bit, ReachableGoal region retarget, reused A* arrays (PathService.cs:123) — no per-request big allocations except result arrays.
- Harvester target search is throttled (RingsPerTick 8, RetryTicks 40, HarvestMission.cs:36-46) so the whole-map ring scan on an exhausted map is not per-tick.
- SceneRenderer `_items`, `_submerged`, `_subScratch`, `_wrecks`, `_storms` are reused collections; sort uses a struct DrawItem with a cached comparer.
- Detonate/AcquireTarget are deterministic (slot order, tie rules) — any optimisation must preserve slot order.
