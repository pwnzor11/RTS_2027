# sim-rules: gameplay rules in src/Sim (combat, economy, production, supply, veterancy, transports, garrison, rigs, naval, superweapons) — findings
Scope read: Simulation.cs, .Combat, .Production, .Economy, .Supply, .Upgrades, .Veterancy, .Transport, .VehiclePassengers, .Garrison,
.Carrier, .Naval, .Landing, .Beachhead, .Shipments, .Submerge, .Burrow, .BurrowMove (deploy/break-through part only), .Tunnels, .Rigs,
.Salvage, .Raid, .NoRebuild, .Players, .Actors, .Effects, .Superweapon, .Storm, .Ground, .Stats, .M3/.M4/.M6 (part); Missions: Combat,
Harvest, Capture, Salvage, Rig, Withdraw, MissionHandlers, MoveMission (HandleMove/Follow head); Rules: RulesDeriver, EconomyRules,
SupplyRules, ActorLayer(s); data/rules/*.toml via scripts; design 13 in full, design 02/07/14 and DECISIONS by grep (D-062, D-126, D-127,
D-140, D-142, D-173). NOT read: Simulation.Hardening, .Hud, .View, .Scripting, .MissionSpawns, .M5/.M7, Directives, AirMission,
BurrowMove travel code, State classes (serialisation), design 06 in full. Nothing was built or run: every finding is from reading code
and data, so "what happens" is reasoned, not observed. Line numbers are at main as read on 2026-11-11.

## Summary (worst first)
1. A garrisoned soldier still obeys Move/Attack orders and walks out with layer Garrisoned: invisible, untargetable, splash-immune, able to shoot. Box-select or "select army" reaches it.
2. Building morph (Hatch -> Bore Head) is paid from Cash only; all harvest income lands in Stored, so the upgrade is refused for the rest of the game once starting cash is spent.
3. A finished structure cannot be cancelled. If it cannot be placed (Expedition without a beachhead source, water building with no shore in radius) the Structure or Defence lane is dead for good.
4. Stealth units with any veterancy rank (or a rate-of-fire aura) no longer break stealth by firing: a Veteran Veilwalker or Pathfinder snipes while permanently hidden.
5. Splash damage (Detonate) hurts passengers inside transports, docked carrier aircraft and units in tunnel transit; every other area effect spares them.
6. Deploy on a landing craft or Skyhook unloads everyone at once, bypassing unload_ticks and the shore-cell rule of the Unload order.
7. RigMission validates the unpack site with the generic packed vehicle's type (3x3 Mother Rig), not the rig actually packed: small rigs are refused wherever a 3x3 does not fit.
8. "Prohibitive cost" placeholders (tender 6000, packed rig 4000, command ship 6000) and cost-0 main bases feed cost-derived rules: one tender or packed-rig kill makes a Hero; conyard repair is free.
9. Smaller: lb_yard sell loop gives half-price harvesters past the supply cap; soldiers inside a carried APC survive on the sea when the ship sinks; no-base players are defeated on entering a neutral ruin; dead rule no_build_threshold.

## Findings

### SIM-RULES-01 · critical · SIM · confidence medium-high
**What:** Orders are not filtered by layer, and nothing pins a Garrisoned soldier. A Move, Attack, AttackMove, Guard or Stop order replaces its Garrisoned mission; it walks out of the building while its layer stays Garrisoned.
**Where:** src/Sim/Simulation.Movement.cs:28-48 (ApplyMovementCommand, only OwnsSubject), Simulation.Combat.cs:399-427, Simulation.Garrison.cs:63-75 (GarrisonNow sets layer, leaves SlotCell), :114-118 (RunGarrisoned is the only code that looks at the layer, and only runs under mission Garrisoned), Missions/MoveMission.cs:161-178 (off home spot: DriveWithin back to SlotCell, then a normal route).
**Evidence:** Passengers are re-pinned every tick (Transport.cs:85-97 SyncPassengers) and tunnel units by PinUnderground (Tunnels.cs:388); there is no equivalent for Garrisoned (grep `ActorLayer.Garrisoned` in src/Sim: 11 hits, none resets a mission). After the order: Ground.cs:339 IsUntargetableBy = true, M3.cs:190 IsHiddenFrom = true (not in enemy snapshots), Combat.cs:370 Detonate skips it, Combat.cs:226-234 Fire allows it (Garrisoned is not "underground"). It still counts as an occupant (M4.cs:33) so the building's capacity and a flipped ruin's ownership stay wrong. Reachable by accident: Simulation.cs:774-782 lists own garrisoned soldiers as ordinary UnitViews at the building centre (no flag; `Underground` is false), and src/Game/Controls/Selection.cs:107-150 box-select / Army select do not exclude them. No test orders a garrisoned soldier (tests/Sim.Tests/M4/GarrisonTests.cs test list).
**Impact:** Any player (or AI army order) produces soldiers the enemy cannot see or shoot until their building dies (EjectCore then teleports them back). Not run: the exact walk is inferred from Follow.
**Fix idea:** In Apply (or OwnsSubject) drop mobile orders whose subject layer is Garrisoned/Passenger, or make a Move order to a garrisoned soldier eject it first; hide garrisoned soldiers from the own-unit list or flag them. Add a test. S.

### SIM-RULES-02 · major · SIM · confidence high
**What:** The building Upgrade (morph) command checks and charges `Players.Cash` only. Harvest income goes to `Stored`; Spend drains Cash first. Once the 5000 starting cash is gone, Cash stays 0 and the morph is refused whatever the player owns.
**Where:** src/Sim/Simulation.Burrow.cs:132-134; Missions/HarvestMission.cs:321-322 (unload -> Stored); Simulation.Economy.cs:21-30 (Spend: Cash then Stored); src/Game/Ui/Sidebar/BuildingUpgradeOffer.cs:42 and GameScreen.cs:854 (button mirrors `hud.Cash`).
**Evidence:** `if (_players.Cash[c.Player] < cost) return; _players.Cash[c.Player] -= cost;`. Every other payment uses Spend/Funds. Data: dir_hatch 150 -> dir_bore_head 400, difference 250. The UI then says "Needs $250 more cash" to a player with thousands stored. The charge also bypasses OnStatsSpent.
**Impact:** Directorate cannot upgrade a Hatch to a Bore Head (the M-size tunnel entrance) after the opening minutes, except right after a sale or refund.
**Fix idea:** `if (Funds(p) < cost) return; Spend(p, cost);` and pass Cash+Stored to BuildingUpgradeOffer. S.

### SIM-RULES-03 · major · SIM · confidence high
**What:** A completed structure waiting for placement (`Production.Ready[lane]`) cannot be cancelled or refunded, and it blocks its lane until placed. If no legal site exists the lane never works again.
**Where:** src/Sim/Simulation.Production.cs:155-163 (Cancel only indexes the queue), :128 and :282 (Ready blocks enqueue and progress), :197-203 (Place is the only way to clear it).
**Evidence:** `grep "Ready\[" src/Sim` shows one write to -1, in ApplyPlace. Cases with no legal site: (a) an Expedition player whose beachhead sources (depot, quay, beacon, shipyard) are all dead but who still owns other buildings: Beachhead.cs:100-106 returns OutsideBeachhead for every cell (D-140 says losing a source "blocks new placement"; it does not say the lane is lost); recovery needs a Command Ship (cost 6000, prerequisite exp_drydock; TryDeployInPlace skips CanPlace); (b) a water building finished with no shore inside build radius 2; (c) a base walled in by the one-cell-gap rule.
**Impact:** Structure or Defence production is gone for the rest of the match, with the full price sunk. The AI just retries (AI/Recruit/BuildOrderManager.cs:105-109).
**Fix idea:** Let Cancel with a "ready" position (e.g. Arg2 = -1) refund the item and clear Ready; UI: right-click the READY cameo. S.

### SIM-RULES-04 · major · SIM · confidence high
**What:** RunStealth decides "fired this tick" by `ReloadTicks >= weapon.RateOfFire`. Fire shortens the reload for aura and veterancy, so for a ranked shooter the test is never true and firing no longer resets the stealth timer.
**Where:** src/Sim/Simulation.Ground.cs:146-152; Simulation.Combat.cs:248-250.
**Evidence:** Fire sets `ReloadTicks = rof`, then `* RofPermille/1000`, then `ScaleByRank(ReloadPermille)`. game.toml reload_permille = [950, 900, 800]. lb_veil_rifle rof 80 -> 76 at rank 1; 76 >= 80 is false. Stealth, armed, rank-capable types: lb_veilwalker, exp_pathfinder (range 1536 = 6 cells). Rank 1 needs 600 kill value, i.e. two cheap kills. A stealthed actor is untargetable unless an enemy is within 1 cell or a Searchlight covers it (Ground.cs:209-224, M3.cs:193).
**Impact:** A Veteran sniper standing still fires for ever without being revealed. Same for any stealth unit under a rate-of-fire aura buff.
**Fix idea:** Have Fire reset the still counter directly (`_effects.*Still[i] = 0`) instead of inferring it from the reload. S.

### SIM-RULES-05 · major · SIM · confidence medium-high
**What:** Detonate's area loop skips Garrisoned, Burrowed/Surfacing and submerged actors but not Passenger or Tunnel layer actors, whose Position is their carrier's or destination entrance's cell.
**Where:** src/Sim/Simulation.Combat.cs:365-372 (the only layer filter); Transport.cs:71, VehiclePassengers.cs:86, Carrier.cs:101-102, Tunnels.cs:295 (where those positions are set).
**Evidence:** Passengers and tunnel units are spared by radiation (Ground.cs:85), superweapon blast (Superweapon.cs:90) and salvo (:158) through IsUnderground, and are untargetable; Detonate is the odd one out. 22 warheads have splash 1. One HE shell on a loaded auth_carrier gives each of the 5 soldiers full damage; a hit on a carrier ship damages every docked aircraft; shelling a tunnel exit hurts units still "underground" bound for it. No test covers it (grep passenger+splash in tests: none). The same loop ignores altitude: ground splash hurts aircraft over the cell and flak splash hurts ground units (possibly intended; no decision found).
**Impact:** Transports are death traps against splash; hidden units die to shots that could not target them. Passengers killed this way or lost with a transport are also missing from UnitsLost and kill credit (Transport.cs:144-162 RemoveMobileQuietly).
**Fix idea:** In Detonate skip `IsUnderground(kind,i)` unless the warhead says otherwise; decide air/ground splash explicitly. S.

### SIM-RULES-06 · major · SIM · confidence high
**What:** Deploy on any transport calls the instant ApplyUnload, also for landing transports. The timed, shore-cell landing only runs for CommandKind.Unload.
**Where:** src/Sim/Simulation.M3.cs:107-114 (no IsLandingTransport check) versus Simulation.Garrison.cs:202-208; src/Game/Controls/M3Orders.cs:42 (Passengers > 0 -> Deploy key issues Deploy in place).
**Evidence:** exp_landing_craft has passengers 8, vehicle_slots 2, unload_ticks 10; exp_skyhook is air. Deploy puts all soldiers on rings 1-2 and all vehicles ashore in the same tick (Transport.cs:102-120); the Unload order lands one per 10 ticks. A Skyhook can drop its whole load mid-flight in one tick. LandingTests never sends Deploy.
**Impact:** The D key is a strictly better landing than the designed one (design 07 §6.3, M7-04).
**Fix idea:** In ApplyDeploy route landing transports to ApplyLandingUnload with the craft's own/adjacent shore cell, or refuse. S.

### SIM-RULES-07 · major · SIM · confidence medium-high
**What:** RigMission reads the packed vehicle type's `unpacks_into`, which for the one generic packed vehicle is always lb_mother_rig (3x3). ApplyPack and FinishUnpack use UnpackTypeOf (the type it was packed from).
**Where:** src/Sim/Missions/RigMission.cs:24-34; Simulation.Rigs.cs:48-50, :57-61, :174-176.
**Evidence:** Every rig packs into lb_mother_rig_packed (data: 7 rigs; footprints 2x2 for refinery/dynamo/still/muster, 1x1 lookout). The mission calls `UnpackSiteOk(owner, vt.UnpacksInto, tl, i)` and uses the 3x3 for "inside" and the approach cell. An order ApplyPack accepted for a 2x2 site is ended silently by the mission when the 3x3 at the same top-left touches a building, cliff, map edge, unit or the gap rule. RigTests has no small rig in a tight site.
**Impact:** Left Behind players see small rigs refuse to unpack next to other buildings with no message; a 1x1 Lookout needs a clear 3x3.
**Fix idea:** Use `sim.UnpackTypeOf(i)` in RigMission. S.

### SIM-RULES-08 · major · NUMBERS · confidence high
**What:** Types marked "not meant to be built ... prohibitive cost" and cost-0 main bases distort every rule derived from `cost`.
**Where:** data/rules/vehicles.toml exp_supply_tender cost 6000, lb_mother_rig_packed cost 4000, exp_command_ship cost 6000; buildings.toml auth_conyard/dir_shaft/lb_mother_rig/exp_landing_depot cost 0; src/Sim/Simulation.Veterancy.cs:39-45; Simulation.Salvage.cs:98; Simulation.Production.cs:498-500.
**Evidence:** Kill value = victim cost x 1000/1000; Hero threshold 3200. Sinking one unarmed 250-hp tender (6000) or killing any packed rig, even a packed 200-credit still (4000), takes the killer from recruit to Hero. Each also leaves a 1000-credit wreck (the cap). Cost 0: repair cost = heal x permille x 0, so the main base repairs free; killing it gives no kill value; selling it returns 0. The three vehicles are also real sidebar items for a player with the prerequisite (TryTypeInfo has no "not buildable" flag for vehicles); a bought tender is not a shipment and pays nothing.
**Impact:** Convoy raiding and rig hunting mint Heroes; conyards are free to repair.
**Fix idea:** Add `buildable = false` for vehicles and a separate `value` (or `kill_value`) field; give main bases a nominal value. M.

### SIM-RULES-09 · minor · NUMBERS · confidence high
**What:** lb_yard costs 600, grants a free lb_gleaner (cost 600) and sells for 300: each cycle is a harvester for 300, with no factory, and FreeUnit spawns ignore the supply cap (by design 13 §2.4).
**Where:** data/rules/buildings.toml:660-677; src/Sim/Simulation.Production.cs:213-214, :238-245.
**Evidence:** Other factions lose money on the same loop (2000 -> 1000 back for a 600-700 harvester). Sell refund also ignores health (50 % of cost at 1 hp).
**Impact:** Left Behind exploit for cheap harvesters beyond the cap.
**Fix idea:** Free unit only for the first refinery, or subtract the free unit's cost from the sale. S.

### SIM-RULES-10 · minor · SIM · confidence medium
**What:** When a ship carrying a loaded APC sinks, the APC is removed quietly but its own passengers are not; next tick SyncPassengers drops them "on the surface where it is", i.e. on water, holding no cell.
**Where:** src/Sim/Simulation.VehiclePassengers.cs:105-112 (comment says "and their soldiers" but only RemoveMobileQuietly on the vehicle); Transport.cs:92, :156-162.
**Evidence:** CanBoardVehicle refuses only types with vehicle_slots; auth_carrier, lb_tow, exp_hoverlander (passengers, no vehicle slots) may board exp_landing_craft. RemoveMobileQuietly does not call OnTransportDying.
**Impact:** Stranded soldiers standing on deep water.
**Fix idea:** Call OnTransportDying(v) in LoseVehiclePassengers. S.

### SIM-RULES-11 · minor · SIM · confidence high
**What:** RunProductionTick sets EverHadBase for the owner of any non-wall building, including a flipped neutral ruin; UpdateDefeat excludes flipped ruins from "has a base". A player who never had a base is defeated the tick it garrisons a neutral ruin.
**Where:** src/Sim/Simulation.Production.cs:271-272 versus :521-530.
**Evidence:** The only test (NeutralGarrisonTests.cs:169) uses a player with a conyard. Shipped missions all start with a base, so today this only hits sandbox or future no-base missions on ash_reckoning or generated maps with ruins. A defeated player's commands are ignored (Simulation.cs:314).
**Fix idea:** Add the `HomeOwner == NoOwner` test at :272. S.

### SIM-RULES-12 · minor · SIM · confidence high
**What:** ComputeDamage has a floor of 1, so the five utility weapons with damage 0 and an all-zero versus table deal 1 damage to every enemy in their 3x3.
**Where:** src/Sim/Simulation.Combat.cs:258-263; data/rules/weapons.toml dir_blackout_drop, auth_decon_spray, auth_foam_spray, dir_ash_sow, lb_dust_drop.
**Evidence:** The hits also wake guards, reset the Hero self-heal timer and can take kill credit. There is no attack-ground order (grep finds none), so these trucks only spray when an enemy is in range.
**Fix idea:** No hit when weapon damage is 0; consider an attack-ground order (DESIGN). S.

### SIM-RULES-13 · minor · SIM · confidence high
**What:** A harvester whose last refinery dies goes to Guard and never resumes when a refinery is rebuilt; RunEconomy restarts only harvesters with mission None.
**Where:** src/Sim/Missions/HarvestMission.cs:266; Simulation.Economy.cs:100-103. Same for a harvester put ashore from a transport (VehiclePassengers.cs:144 queues Guard).
**Impact:** Idle harvesters after a rebuild until re-ordered by hand.
**Fix idea:** In RunEconomy also restart Guard-idle harvesters when an own refinery exists. S.

### SIM-RULES-14 · minor · SIM · confidence medium
**What:** Deploy-in-place removes the vehicle before placing the building and ignores a failed placement: with the building store full (511 slots, walls included, all players) a Land Train or Command Ship vanishes. It also skips the one-cell-gap rule and the "structures built" counter.
**Where:** src/Sim/Simulation.BurrowMove.cs:374-375 (compare Rigs.cs:185-187, which checks first).
**Fix idea:** Place first, remove on success. S.

### SIM-RULES-15 · minor · DESIGN · confidence medium
**What:** Inconsistent area rules: the classic superweapon blast hits submerged submarines while salvo and splash spare them (Superweapon.cs:88-93 versus :159; D-142); radiation hurts aircraft flying over hot cells and soldiers inside a garrison (Ground.cs:85); a submerged sub may fire while hidden (Fire has no Submerged test; dive is an instant toggle with no cooldown, Submerge.cs:74).
**Fix idea:** Owner decision per case; one shared "area effect spares" predicate. S-M.

### SIM-RULES-16 · minor · DESIGN · confidence high
**What:** `[radiation] no_build_threshold = 64` is loaded and hashed but no sim code reads it; CanPlace never looks at radiation.
**Where:** src/Sim/Rules/RadiationRules.cs:9-12 ("lowest level on which the Authority may not place structures"); grep NoBuildThreshold: Data loader and RulesHash only.
**Fix idea:** Implement in CanPlace or delete the key. S.

### SIM-RULES-17 · idea · SIM · confidence medium
**What:** Small leftovers. ApplyUnload returns at the first soldier with no room and then skips the vehicle passengers (Transport.cs:110); a soldier in an ally's garrison can only be unloaded by the building's owner (Garrison.cs:199); home shipments keep launching for a defeated player with a dock (Shipments.cs:73-84); veterancy "taken" reduction is not applied to the garrison share (Combat.cs:284-287); a captured superweapon keeps its charge (Transport.cs:206-218).

## Open questions
- Is splash meant to cross altitude (ground shells hurting aircraft, flak hurting ground)? No decision found.
- D-140: is an Expedition player with no beachhead source meant to be unable to build for good?
- Should a sale refund scale with health, and should free harvesters be once only?
- D-173 counts a lone harvester as a lifeline even with no refinery and no way to build one; intended?

## Things checked and found sound
- Simultaneous damage and single kill credit per death (ApplyPendingDamage); projectile/shooter references are generation-checked.
- Queue cancel refunds only what was spent; no free money from cancel, hold, power change or upgrade research; Stored is clamped to storage.
- Supply: enqueue and queue-head tests, cache invalidation on spawn/removal/capture/pack, packed rig supply 0, tender supply 0.
- Veterancy data is cleared on slot reuse; rank-3 heal is bounded by max health.
- Engineer capture ejects the garrison first; emptied flipped ruins revert to neutral.
- Tunnel queue/transit: lost destination re-routes or drops; cap accounting; orders to underground units are pinned.
- Rig pack/unpack keeps the health ratio; unpack re-validates; the free unit is not re-granted on unpack.
- Carrier death: docked aircraft lost and counted, airborne ones orphaned; hangar slots by HangarUsed.
- UpdateDefeat / ComputeGameOver team logic and simultaneous-defeat draw (-1); D-173 code matches the decision text.
- Spoils are off in shipped data (spoils_permille not set), so the "any killer" assumption in Raid.cs is inert.
