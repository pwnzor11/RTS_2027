# ROSTER-LE — Left Behind and Expedition roster review (units, buildings, upgrades)
Scope read: data/rules/{infantry,vehicles,buildings,weapons,warheads,upgrades,techs,game}.toml (all lb_*/exp_* entries, regenerated
into a roster and a prerequisite-closure check with python: /home/claude/audit43/tmp/{load,roster,reach}.py); data/archive/*.toml
(Left Behind and Expedition unit, structure and upgrade text); data/missions/{lb,exp}_0*/mission.toml tech lists; src/Game/Ui/Sidebar/TodayText.cs;
src/Sim/Simulation.{Rigs,Production,Effects,Submerge,Veterancy,Salvage,NoRebuild,Combat,Supply}.cs; src/Data/{TierSugar,EffectRulesLoader}.cs;
src/Scripting/MissionHost.cs (unlock gate); docs/reviews/2026-11-11_s18_audit/{README.md,wave1/faction-le.md,wave1/roster_*.md} (comparison only).

Result in one line: no CRIT or HIGH problem found in the Left Behind / Expedition rosters. Every item is buildable or deliberately not,
every upgrade has an effect the sim reads, and s18 items #2 and #9 are fixed. What is left is text that contradicts data, one upgrade
whose only effect is useless for its stated purpose, and two long-known unimplemented keys.

## Status of the s18 items this package was asked about
- s18 #9 (Left Behind lanes build while the Mother Rig is packed): FIXED. `RigLanePaused` ends in `RootRigDown` for Left Behind
  (Simulation.Rigs.cs:98-139); structure and defence lanes pause while the root rig (packable building with no prerequisites) is packing or
  packed; unit lanes stop because `CountFactories` skips a packing rig (Production.cs:77-83). Test: tests/Sim.Tests/S18/RigLaneAuditTests.cs.
  Evidence: read.
- s18 #2 (Expedition with no building never defeated): FIXED. `UpdateDefeat` counts a live vehicle with `DeploysInto >= 0` (Command Ship) and any
  packed rig as a base (Production.cs:552-560); `EverHadBase` is set from that and is saved (ProductionState.cs:128/152). Test:
  tests/Sim.Tests/S18/DefeatAuditTests.cs. Evidence: read.
- Also fixed since s18 (checked in data/code): #14 missile sub armour is `ship_light` surfaced, `SubRevealTicks` 100 > sub rate_of_fire 90, so a sub that keeps
  firing stays targetable (Submerge.cs:23-24, vehicles.toml:810); #17 unpack site uses the carried type (`UnpackSiteOk(owner, into, ...)`, Rigs.cs FinishUnpack);
  LE-01 aircraft in mission tech lists (MissionHost.cs:309); LE-04 real costs 1500 for packed rig, tender, Command Ship (vehicles.toml:543/780/891) and
  `VehicleWorth` uses the carried building (Salvage.cs:83); LE-08 tow 1750; LE-14 `one_use`; LE-19 founders count; LE-07 ship columns filled.

## Findings
### ROSTER-LE-01 · MED · data
What: Sonar Nets (800 credits, tier 2) does nothing for submarine detection. Its only effect is `exp_picket sight x1.3` (9 -> 11 cells of surface vision). The picket's
detection comes from `sonar_radius = 9`, which no effect can change (`BuildingAttrs` has no `sonar_radius`), and `IsSonarRevealed` reads only `SonarRadius`.
Where: data/rules/techs.toml `exp_fx_sonar_nets_picket` (attr sight); data/rules/upgrades.toml:162 ("sight stands in for sonar"); src/Data/EffectRulesLoader.cs:86; src/Sim/Simulation.Submerge.cs:42-47; data/archive/upgrades.toml `exp_up_sonar_nets` body ("extend the range of each listening post and reveal submerged boats to the whole team for a short time after they are heard").
Evidence: read + script (script listed every upgrade effect: the only effect is attr sight on exp_picket; grep of sim shows sonar uses `SonarRadius` only).
Impact: An Expedition player who buys it against a submarine-heavy enemy gets nothing. The sidebar tooltip is honest (TodayText "Pickets see 2 cells further"); the Archive text is not, and a player who reads the Archive pays 800 for no gain.
Fix idea: add `sonar_radius` to the building effect attributes and use it in the effect; or reword the Archive body and drop the upgrade from the research list.   Changes a match outcome / number / visible text?: yes (if fixed by effect); no (if only reworded)

### ROSTER-LE-02 · LOW · text
What: The Drydock tooltip says it "Unlocks the Cruiser, Carrier and Command Ship", but the Command Ship has `buildable = false` and production never offers it.
Where: src/Game/Ui/Sidebar/TodayText.cs:16; data/rules/vehicles.toml:891 area (exp_command_ship buildable = false, prerequisites ["exp_drydock"]); src/Sim/Simulation.Production.cs:40.
Evidence: read + script (roster script: exp_command_ship buildable False; reach closure lists it as UNREACHED by design).
Impact: Players look for a Command Ship button after raising a 2000-credit Drydock; the Drydock only gates Cruiser and Carrier.
Fix idea: change the tooltip to "Unlocks the Cruiser and Carrier."   Changes a match outcome / number / visible text?: yes (text)

### ROSTER-LE-03 · LOW · text
What: Several units still advertise mechanics that do not exist, and have no TodayText entry (TodayText covers only minelayer, diver, drydock, destroyer, harpoon wagon).
- exp_launch: subtitle "Light scout boat", Archive "twin machine gun"; its only weapon `exp_launch_charge` has `targets = "submerged"` (weapons.toml), so it cannot shoot anything but a dived, sonar-revealed submarine. The AI knows this (ExpeditionTactics.cs:260 builds it only after seeing an enemy sub); a human does not.
- exp_pathfinder ("Spotter and designator"), exp_gyro ("Rotary spotter ... marking targets"), exp_survey_hover ("measures how thick the grey lies"): no designation or richness mechanic (data TODO comments at vehicles.toml:644, 915; infantry.toml pathfinder). They are plain scouts.
- exp_skyhook Archive "room for six infantry or two light vehicles": `passengers = 6`, no `vehicle_slots` (vehicles.toml:932-933 TODO).
- exp_marine subtitle "Amphibious infantry": `foot` only, cannot enter water.
- lb_tow Archive "tow-arm that lifts a packed rig onto its bed": the data is a x1.75 speed aura (radius 2) on nearby packed rigs (vehicles.toml:492-494).
Where: data/archive/units_expedition.toml, units_left_behind.toml; src/Game/Ui/Sidebar/TodayText.cs.
Evidence: read + script (weapon targets and passenger/slot keys dumped).
Impact: Purchase decisions on a 300-credit boat and 250-700 credit scouts rest on text the game cannot honour. Sidebar shows the Archive subtitle.
Fix idea: add TodayText cameos for exp_launch, exp_pathfinder, exp_gyro, exp_survey_hover, exp_skyhook (and reword marine/tow).   Changes a match outcome / number / visible text?: yes (text only)

### ROSTER-LE-04 · LOW · data
What: `exp_provision_store` has `supply_unstocked = 5` and its comment says "+10 stocked, +5 unstocked", and game.toml has `stock_intervals = 3`, but nothing reads them. The store always gives +10.
Where: data/rules/buildings.toml:967,980-982; data/rules/game.toml:70,77; src/Sim/Simulation.Supply.cs:105 (adds `SupplyProvided` only); `SupplyUnstocked`/`StockIntervals` appear only in the loader, the type and RulesHash.cs:293/296. Archive text: "an empty one is the first thing anyone notices".
Evidence: script (grep over src for the two identifiers) + read. Still open from s18 LE-15.
Impact: Dead keys that are hashed; the described "supply drops when shipments stop" never happens. No crash.
Fix idea: implement from the shipments state, or delete the keys and reword.   Changes a match outcome / number / visible text?: yes if implemented; no if deleted

### ROSTER-LE-05 · LOW · text
What: Upgrade wording overclaims or contradicts the effect list.
- Vacuum Cores ("guns of every fleet vehicle") changes four weapons only: glide cannon, canister gun, barrage rockets, gunship rockets (techs.toml `exp_fx_vacuum_cores_*`). Sparker, Destroyer, Cruiser, Jet, Battery are not touched.
- Salvage Rights: data comment "wreck value x1.5" (upgrades.toml:149) but the code multiplies drain speed only (src/Sim/Missions/SalvageMission.cs:59); TodayText already says "The wreck's value is unchanged".
- Scrap Plate Archive says "adds to the armour"; it is +20 % max HP, no armour change (TodayText says so).
Where: as listed. Evidence: script (effect dump) + read.
Impact: Mostly the Archive and the comment; the sidebar lines for Scrap Plate and Salvage Rights are right, the Vacuum Cores line is generated from the effects so is right too.
Fix idea: reword the Archive lines and the comment.   Changes a match outcome / number / visible text?: yes (text)

### ROSTER-LE-06 · LOW · data
What: `aura_targets` on lb_tinker and exp_repair_hover is dead for repair: the repair branch ignores it and heals every friendly vehicle in range, including aircraft and allies (Effects.cs header "It ignores AuraTargets", RecomputeAuras `bestRepair` is taken before the `AuraTargetMatches` test). The lists also drifted: Mender's list lacks exp_canister_skiff (added in s23) and the aircraft; Tinker's lacks lb_rotor and lb_kite, yet both are healed.
Where: data/rules/infantry.toml:206, vehicles.toml:711; src/Sim/Simulation.Effects.cs:25-27,170-175.
Evidence: read.
Impact: A designer who edits the list to change who is repaired gets no change; Mender and Tinker also heal air units for free.
Fix idea: either honour the list in the repair branch or delete the keys and say so in the schema comment.   Changes a match outcome / number / visible text?: yes (if the list is honoured)

### ROSTER-LE-07 · LOW · debt
What: Stale comments on the water buildings. exp_quay, exp_anchorage, exp_shipyard, exp_drydock say "straddles the shoreline (row 0 land, row 1 water / bottom row water)". No entry has a `water` mask, so placement requires every cell on water and the footprint touching land (Beachhead.cs:82-99).
Where: data/rules/buildings.toml:1014,1033,1097,1194.
Evidence: script (grep: no `water =` key in buildings.toml) + read.
Impact: Misleads anyone editing map generation, AI dock fit or art; no player impact.
Fix idea: fix the comments.   Changes a match outcome / number / visible text?: no

### ROSTER-LE-08 · LOW · data
What: lb_01 lists `lb_slinger` in the buildable tech although it can never be built there (tier 2 needs lb_tier2 from the Lookout, which needs the Garage Rig; Garage is only unlocked on win). The briefing says "THE SLINGERS ARE YOUR ONLY ANSWER TO THEIR ARMOUR"; the player gets exactly the five Slingers in `StartForce()` (mission.lua:51) and no more.
Where: data/missions/lb_01/mission.toml:19,31; data/missions/lb_01/mission.lua:51.
Evidence: script (prerequisite closure over each mission's human tech list reports ('lb_slinger','lb_tier2') missing in lb_01 only; every other lb_/exp_ mission is clean).
Impact: A Slinger button never appears in lb_01; harmless but a dead entry, and the "only answer" line means "do not lose them".
Fix idea: drop it from the list or add a note; no change needed to the mission.   Changes a match outcome / number / visible text?: no

## Suspects (unproven)
- exp_diver (50 hp, 250 cr) auto-acquires units although its warhead does 0 % against unarmored, light and heavy (`exp_charge` versus [0,0,0,150,100,150,150,0]): `ComputeDamage` gives a minimum of 1 per hit, so it would spend its 60-tick shots on 1-damage hits against infantry and vehicles instead of walking to a ship or building. Proof needs a run: put a Diver beside a Marine and a Quay and see which it picks (look at target scoring in Simulation.Combat.cs around line 203). The data note "0 % vs units" is in the s18 roster.
- LE-18 (s18) looks unchanged: `CountFactories` counts the Quay and Shipyard as vehicle factories for every vehicle item (Production.cs:77-83, 302), so a Quay speeds Glide Tanks; land vehicles go to `PrimaryFactoryOf` and rely on `FindFreeExitCell` to find land. I did not prove a stall; the exit search should avoid it. Numbers are frozen, so this is noted only.
- lb_up_patchwork "5 cells" and the tooltip text agree with the code (radius 3 + 2). Not a suspect; listed here so nobody re-checks it.

## Checked and fine
- Prerequisite closure (script reach.py, tier sugar applied exactly as TierSugar.cs does): every Left Behind and Expedition unit, building and upgrade is reachable from the start kit. The only unreached ids are the intended `buildable = false` ones: lb_mother_rig, lb_mother_rig_packed, exp_landing_depot, exp_command_ship, exp_supply_tender. No dangling prerequisite name for either faction.
- Every effect id used by an LB/EXP upgrade exists in techs.toml with a valid target and an attribute the loader accepts (max_health, sight, rad_resist_permille, vet_threshold_permille, harvest_capacity, weapon damage/range/rate_of_fire, class:player shipment_interval); the four upgrade-level keys (pack_ticks_permille, plunder_permille, salvage_permille, repairs_vehicles_at_range) are all read (Rigs.cs:65, Raid.cs:66, SalvageMission.cs:59, Effects.cs:104). No orphan effect ids. Upgrade count 28 of 64 (mask limit).
- New unit Grapeshot (exp_canister_skiff, D-236/237) has upgrade coverage in Vacuum Cores, Composite Armour and Veteran Drill exactly as D-237 says. D-223, D-254, D-280, D-244 numbers all match data (Scavenger 54, Glide Tank 960, Skimmer 360, shipment 1100, repair aura 5, Refinery Rig 700, harpoon `both`, Salvo 4800, canister 11).
- Weapon coverage: ground weapons also hit surface ships (WeaponTargetsActor default case), so Left Behind has an answer to every Expedition surface hull (Organ 7 cells, Rotor, Kite). Left Behind anti-air: Flak Net, Harpoon Wagon and Post (`both`), so no air class is unanswerable. The only class neither faction's base kit can target is a dived submarine without sonar (Expedition has Destroyer, Picket and the Launch; Left Behind has nothing, mitigated by the 5-second reveal after each shot, which exceeds the sub's reload). Underground Directorate units are untargetable for both factions (seismograph is Authority only); they surface to fight. Both are recorded decisions.
- Mother Rig rules: pack, unpack, pause lanes, supply, defeat, kill value, wreck value all coherent in code; FinishPack puts the building back if the vehicle store is full; FinishUnpack places before removing the vehicle (no window with no base).
- No unit has a weapon range above the map-relevant sanity limit; only indirect-fire types have range > sight (Organ, Barrage Lander, Missile Sub, Cruiser).
- Supply bands, kill value (tender/Command Ship/packed rig now 1500, Veteran only, not instant Hero), storage and dock offsets looked consistent.
- Campaign tech lists: no list asks for a type whose prerequisite provider is missing, except the lb_01 Slinger above.
- Working files: /home/claude/audit43/tmp/{load,roster,reach}.py (scratch scripts, not part of the deliverable).
