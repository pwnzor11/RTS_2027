# SIMECON — economy, ashfall, supply, shipments, production, upgrades, rigs, salvage, props, storm, raids
Scope read: src/Sim/Simulation.{Economy,Ashfall,Supply,Shipments,Production,Upgrades,Rigs,Salvage,Props,Storm,NoRebuild,Raid,Actors}.cs (all, in full);
src/Sim/Production/ProductionState.cs, Shipments/ShipmentState.cs, Salvage/SalvageState.cs, Economy/RegrowthSchedule.cs, Rules/{RulesDeriver,SupplyRules,EconomyRules}.cs;
parts of Simulation.cs (Apply, CanPlace, RemoveBuilding), Simulation.Ground.cs (RunRadiation), Simulation.M3.cs (OnM3BuildingRemoving), Missions/HarvestMission.cs (unload only);
data/rules/{upgrades,techs,game,buildings,vehicles}.toml (effects cross-checked by script); s18 README items 6 and 7.
s18 status: #6 (finished structure blocks lane) FIXED (CancelReady, Simulation.Production.cs:127-134; AI cancels after N failed searches, BuildOrderManager.cs:141-149).
#7 (full 511-slot store) FIXED (CanPlace returns StoreFull, Simulation.cs:489; BurrowMove.cs:292,351 refuse; tests FullBuildingStoreAuditTests). Two small leftovers in findings 07 and Suspects.

## Findings
### SIMECON-01 · MED · bug
What: prerequisites are checked only when an item is enqueued. A unit/defence/structure/upgrade already in the queue (the front item or any item behind it) keeps building after the tech building that unlocked it is sold or destroyed. Up to 5 items per lane (Economy.MaxQueue = 5) get through.
Where: src/Sim/Simulation.Production.cs:306-327 (AdvanceLane calls only TryTypeInfo, never HasPrerequisites/IsUnlocked); :207-216 (ApplyPlace does not re-check either); src/Sim/Simulation.Upgrades.cs:83-113 (RunUpgradeResearch: no prerequisite re-check).
Evidence: read — grep of HasPrerequisites/IsUnlocked callers: only IsUnlocked (Production.cs:103-108), Burrow.cs:131 and the HUD lists; none in the per-tick path. Test Prerequisites_UnlockWhenTheBuildingExists_AndRelock_WhenItIsGone only checks CanBuild after Sell, not an already queued item. The s18 fix (da3e592, UXG-01) deliberately keeps a FINISHED structure placeable; it says nothing about queued or in-progress items.
Impact: a player queues five tier-3 units, sells the tier-3 building (50 % refund) and still gets all five; an enemy raid that kills the radar/tech building does not stop what is already queued. Human only in practice (the AI re-plans).
Fix idea: in AdvanceLane, if !HasPrerequisites (and UnlockFilter) for the front item, pause the lane like the supply pause (no spend, no progress), or pop with refund.   Changes a match outcome / number / visible text?: yes (rare)

### SIMECON-02 · MED · balance
What: the Second Drift storm is documented as "non-lethal denial ... never above level 64, so it denies building but does not kill outright" (design 06 §6, game.toml comment on storm_contaminate_cap). But radiation damages any non-immune vehicle/infantry from level 32 (damage_threshold = 32) at level x 100 / 1000 HP every 20 ticks. The storm cap is 64, so it deals about 6 HP per second per non-resistant actor on the cell.
Where: data/rules/warheads.toml:164-165 (storm_contaminate_step 4, cap 64); data/rules/game.toml:37-39 (damage_threshold 32, damage_permille 100, damage_interval 20); src/Sim/Simulation.Ground.cs:86-95; src/Sim/Simulation.Storm.cs:52-56.
Evidence: read — arithmetic: step 4 per 10 ticks reaches 32 at tick 80 and 64 at tick 160 of an 800-tick storm; level 64 gives dmg = 64*100/1000 = 6 per 20 ticks; decay is only 1 per 40 ticks, so the cell stays above 32 for roughly another 1280 ticks. Infantry have max_health 50-60 (infantry.toml:9,19,33), most vehicle MaxHealth is a few hundred: a unit that stays about 10 s dies if infantry, and vehicles lose several hundred HP over the storm plus tail. Resistance 600 (infantry.toml:39) cuts it to 2/s.
Impact: the superweapon kills what stands in it (and lingers about 1 minute after it ends) instead of only slowing and blinding; also hurts allied-to-owner units except Left Behind resists. Differs from the stated design.
Fix idea: lower storm_contaminate_cap to 31 (below damage_threshold) or accept and rewrite the design/comment text.   Changes a match outcome / number / visible text?: yes

### SIMECON-03 · LOW · debt
What: `[radiation] no_build_threshold = 64` is parsed, hashed and documented ("lowest level on which the Authority may not place structures") but nothing reads it. Radiation never denies building, which the Second Drift design (06 §6 "denies building") and RadiationRules doc rely on.
Where: src/Sim/Rules/RadiationRules.cs:9,16; src/Data/GameRulesLoader.cs:185-191; data/rules/game.toml:40; no consumer in src (grep NoBuildThreshold: only loader, hash, record).
Evidence: read — grep -rn NoBuildThreshold src returns only those three definition/parse/hash sites.
Impact: Decon Trucks/Foam are never needed to clear ground for placement; the storm's "deny building" half of its design does not exist. Not visible as text anywhere I found (no player-facing claim).
Fix idea: either wire into CanPlace/UnpackSiteOk (changes hashes) or record a decision that it is unused and delete the key.   Changes a match outcome / number / visible text?: no (if only documented)

### SIMECON-04 · LOW · bug
What: UpdateDefeat treats any live vehicle with DeploysInto >= 0 as "a base" (D-205 says: a founder, Command Ship / MCV). The Directorate dir_bore deploys into dir_hatch (a tunnel entrance, not a tech-tree root), so a Directorate player whose last building dies but who keeps one Bore is not defeated by the base rule. The skirmish no-rebuild rule (NoRebuild.cs EnsureLifelineTypes) uses the correct root test, so skirmish ends 120 s later; missions (NoRebuildDefeat off) never defeat the player by sim rule.
Where: src/Sim/Simulation.Production.cs:~556-561 (UpdateDefeat vehicle loop `DeploysInto >= 0`); data/rules/vehicles.toml:93 (dir_bore deploys_into = "dir_hatch"); contrast Simulation.NoRebuild.cs:36-62.
Evidence: read.
Impact: Directorate enemy in a campaign mission survives with a single Bore after its base falls (mission objective scripts may still end it); skirmish: a 2-minute delay.
Fix idea: use the same root-type predicate (_lifelineVehicle / bld[]) in UpdateDefeat.   Changes a match outcome / number / visible text?: yes (rare)

### SIMECON-05 · LOW · balance
What: Left Behind lb_yard gives a free lb_gleaner on placement. Yard 600 + free Gleaner worth 600; selling the Yard refunds 50 % (300). Net 300 credits for a 600-credit harvester, repeatable, and the free unit ignores the supply cap at spawn. Other factions' free-unit buildings are not profitable (auth 1700 vs 700, dir 1700 vs 600, exp 1800 vs 500).
Where: src/Sim/Simulation.Production.cs:223-225 (ApplyPlace FreeUnit), :243-251 (ApplySell); data/rules/buildings.toml:668,675 (lb_yard cost 600, free_unit lb_gleaner); vehicles.toml:403 (gleaner cost 600).
Evidence: script — python read of buildings.toml/vehicles.toml printing cost vs free_unit cost per building (output: lb_yard 600 / lb_gleaner 600; auth_refinery 1700 / 700; dir_smelter_vent 1700 / 600; exp_dredge_works 1800 / 500).
Impact: only an economic oddity (cheap harvesters, 10 power of load while standing, ~200 base build ticks each cycle); no infinite money. Frozen numbers, so report only.
Fix idea: free unit only on first placement, or refund 0 for the free-unit part.   Changes a match outcome / number / visible text?: yes

### SIMECON-06 · LOW · bug
What: money lost on a cost-reducing upgrade finishing mid-build. AdvanceLane drains `cost x (ticks+1)/total` with the CURRENT derived cost; if the cost drops (dir_up_mass_conscription: dir_conscript cost x 0.8) while an item is partly paid, `Spent` already exceeds the new cost share; no one refunds the difference at completion.
Where: src/Sim/Simulation.Production.cs:316-325 (due = max(0, want - Spent)); data/rules/techs.toml dir_fx_conscript_cost (mul 800).
Evidence: read — only conscript carries a cost effect (script over all 85 effects: attr=cost appears once). Loss is at most 16 credits per in-flight conscript. The mirror case in CancelReady refunds derived-now cost (Production.cs:130-131), also off by the same amount if the cost changed between finish and cancel (structures have no cost effects today, so latent).
Impact: up to 16 credits, once per upgrade. Trivial.
Fix idea: on completion refund max(0, Spent - cost).   Changes a match outcome / number / visible text?: no (practically)

### SIMECON-07 · LOW · ux
What: a Left Behind rig that finishes unpacking while the building store is full silently stays packed after spending the whole unpack timer; UnpackSiteOk and ApplyPack's unpack branch never check BuildingStoreFull (s18 #7 fix covered CanPlace, deploy and bore only).
Where: src/Sim/Simulation.Rigs.cs:45-52 (ApplyPack unpack branch), :193-211 (FinishUnpack `if (b.IsNone) { UnpackTicksLeft = 0; QueueGuard; return; }`).
Evidence: read — FinishUnpack comment "building store full"; UnpackSiteOk has no store test. 
Impact: needs 511 buildings in the game (walls count) — rare; player sees nothing.
Fix idea: UnpackSiteOk returns false when BuildingStoreFull so the order is refused at once.   Changes a match outcome / number / visible text?: no

### SIMECON-08 · LOW · balance
What: ashfall damage is 2 per hit, scaled by (1000 - resist)/1000 with integer division and a minimum of 1, so any resistance from 1 to 999 gives the same 1 damage. Resist 600 (Directorate soldiers) and a hypothetical 100 are identical, and Lead Lining effects (rad_resist add) change nothing against ashfall.
Where: src/Sim/Simulation.Ashfall.cs:28-31; data/rules/game.toml:43-44.
Evidence: read — dmg = 2*(1000-r)/1000: r=1 -> 1, r=999 -> 1, r=0 -> 2.
Impact: rad-resist upgrades are flat against ashfall; matches the owner-confirmed numbers (D-207), so report only.
Fix idea: none needed unless ashfall_damage is raised.   Changes a match outcome / number / visible text?: no

### SIMECON-09 · LOW · text
What: the Archive entry for Grapple Hooks says Hook Runners "can tear a gap through a wall for a few seconds"; the sim has no wall breaching (upgrades.toml comment: "a later sim hook"). The in-game sidebar text is honest ("They cannot breach walls yet." TodayText.cs:26).
Where: data/archive/upgrades.toml:238 vs data/rules/upgrades.toml:142 and src/Game/Ui/Sidebar/TodayText.cs:26.
Evidence: read.
Impact: lore text promises a feature the sim lacks.
Fix idea: soften the Archive body or implement later.   Changes a match outcome / number / visible text?: yes (text)

## Suspects (unproven)
- Perf: HudFor (Simulation.Hud.cs:18) runs BuildBuildableList + BuildOptionList, each calling IsUnlocked (OwnsProvider scans all 511 building slots per prerequisite string, with RulesOf per slot) and QueueBlockReason (CountFactories scan, QueuedSupply) for every type (about 190). GameScreen.cs:400,572,775 and AudioBootstrap.cs:105 call it several times per frame. Prove with a profile of one HudFor on a 300-building late-game state.
- Perf: FindFreeExitCell is called every tick while an exit is blocked (Stalled) and costs up to 289 cells x ActorStandsOn (all units + infantry, no spatial index) = O(cells x actors); ActorStandsOn also counts air units that hold no cell (CanPlace skips them), so a hovering aircraft over the exit shifts spawns. Prove with a blocked factory plus ~1000 units.
- FinishPack fallback (Simulation.Rigs.cs:178-184): if SpawnVehicle fails (vehicle store full) the rig building is removed and re-placed with a new EntityId; RemoveBuilding clears garrison/effects/superweapon charge for it (OnM3BuildingRemoving), so the rebuilt building would lose charge and Primary/rally references. Needs a full vehicle store to prove.
- ApplyPlace free unit (Production.cs:223-225): SpawnProducedVehicle failure (no exit cell within ring 8, or full unit store) is ignored, so the refinery's free harvester is lost silently. Needs an enclosed refinery site to prove.
- Factory speed-up 2/(1+n) has no cap (Production.cs:~290): 10 barracks build in about 18 % of the time, limited only by supply and cash. Not a bug by the doc, but unbounded.
- Unload at full Stored (HarvestMission.cs:~402): overflow is discarded while the harvester keeps unloading and cycling; only Cash (refunds, plunder) is uncapped, so a full silo burns the whole harvest. By design (RunEconomy comment), mentioned as a known money sink; no AI/UI guard seen in my scope.

## Checked and fine
- Cancel: front item refunds exactly Spent (Production.cs:122), queued items refund nothing (nothing paid); upgrade lane same (Upgrades.cs:30-31); CancelReady full cost, same cost as paid for structures; AI cancels a stuck ready item (s18 #6).
- Hold/resume: Held cleared when the front leaves (PopQueue), no spend while held, state round-trips (ProductionState flags byte bit 1).
- Queue limits: ApplyBuild/CanBuild enforce both MaxQueue and Economy.MaxQueue; ready-structure lane refuses a second enqueue (StructureWaiting).
- Power band/slowdown, mid-build total change keeps completed fraction (Ticks rescaled), Spent never exceeds cost in the normal path; handicap changes time only.
- Sell refund = SellRefundPermille x derived cost, integer, cannot exceed cost; repair cost carries its remainder (RepairFrac) and stops when broke.
- Supply: cap and use are pure scans with per-tick cache invalidated by lifecycle hooks; a unit that no longer fits pauses without spending and can be cancelled with refund; tender/command ship/packed rig supply 0 or from the unpacked building; packing rig stops counting at once.
- Upgrades: script over techs.toml/upgrades.toml: 85 effects, no duplicate ids, every upgrade effect id exists, every target (type id / provides / weapon: / warhead: / class:) resolves, every (target kind, attr) pair is one RulesDeriver actually applies (no inert effect); pack/plunder/salvage permille and repairs_vehicles_at_range have consumers (Rigs.cs:65, Raid.cs:66, SalvageMission.cs:59, Effects.cs:104); grouting hooks into Hardening. Health upgrades do not heal existing units by decision Q-M4-j.
- Rigs: pack/unpack timers, abort on re-order, lane pause for packed/packing rig (structure lanes via RigLanePaused/RootRigDown, unit lanes via CountFactories), HP ratio preserved both ways, supply cap drops while packing.
- Shipments: payload decay/floor/charter integer math, tender sunk = credits lost (D-126), arrival refund exact, dock-less player resets schedule, edge spawn restricted to a sea region touching an own dock; TendersMax skip does not leak state.
- Salvage: wreck value min/max/percent, decay line, slot replacement of the oldest, state save/load; VehicleWorth fixes packed-rig and tender worth; no wreck from sold buildings.
- Raid: plunder moves victim Stored to thief Cash (no creation), cap and upgrade multiply order is right; spoils path is dead (spoils_permille not set in game.toml), harmless.
- Ashfall/step-off, props (destroy order independent, overlay/land reset), storm slow/relations, no-rebuild lifeline derivation, regrowth schedule (each cell once per interval) read without findings.
- Command validation: Apply rejects c.Player >= PlayerCount and defeated players before any of these handlers index per-player arrays.
