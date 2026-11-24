# ROSTER-AD — Authority and Directorate units, buildings, upgrades (data review)
Scope read: data/rules/{infantry,vehicles,buildings,upgrades,techs,weapons,warheads}.toml (all A/D entries: 11 infantry, 22 vehicles/aircraft, 37 buildings, 13 upgrades; faction filter by `faction`/id prefix), data/archive/{units,structures}_{authority,directorate}.toml + archive/upgrades.toml (text), data/missions/{auth,dir}_0*/mission.toml (allow-lists), src/Sim/Rules/RulesDeriver.cs, Simulation.{Production,Combat,Ground,Superweapon,Submerge,Upgrades,Effects}.cs, Missions/MoveMission.cs, Game/Ui/Sidebar/UpgradeEffectText.cs, AI/Recruit/*Faction.cs. S18 notes (wave1/faction-ad.md) used for comparison only.
Scripts: /home/claude/audit43/s/ad.py, ad2.py, ad3.py (parse TOML, apply TierSugar rule, fixpoint reachability from conyard/shaft, effect-target/attr validation, weapon-class table).

Script results that came out CLEAN (details in "Checked and fine"): no unbuildable items, no cross-faction prerequisites, every upgrade effect target exists and its attr is applied by RulesDeriver, no upgrade lacks an effect.

## Findings

### ROSTER-AD-01 · MED · text · Authority aircraft are slower than the units the text says they outrun (S18 FAD-07, still open in part)
What: Interceptor/Bomber move at speed 8 (0.62 cells/s). That is slower than a Rifleman (12), a Jeep, and the enemy Dirigible it exists to hunt (16 = 1.25 cells/s, twice as fast). Archive says the Interceptor "is fast" and exists "to keep the sky empty of Directorate dirigibles"; the Dirigible's own entry calls it "a slow, fragile envelope".
Where: vehicles.toml:278 (`auth_interceptor` speed 8), :293 (`auth_bomber` speed 8), dir_dirigible speed 16; archive/units_authority.toml:152 ("It is fast"); archive/units_directorate.toml (dir_dirigible "A slow, fragile envelope").
Evidence: script — ad.py/ad2 dump: air speeds `dir_dirigible 16, auth_interceptor 8, auth_bomber 8, dir_great_dirigible 8`; infantry.toml rifleman speed 12. (Dirigible is the fastest Directorate air unit and 2x any Authority aircraft.)
Impact: Interceptor cannot catch a Dirigible that retreats; booklet/tooltip text promises the opposite. Bulwark/Howitzer/Marksman parts of FAD-07/14 ARE fixed (Bulwark 500 hp/900/speed 18, Howitzer dmg 32, Marksman cost 200 speed 10).
Fix idea: reword the two archive lines (numbers are frozen).   Changes a match outcome / number / visible text?: text only

### ROSTER-AD-02 · LOW · text · Bomber "the Authority's most expensive aircraft" is not true
What: Bomber costs 600, same as Interceptor (600). Text claims it is the most expensive.
Where: archive/units_authority.toml:165; vehicles.toml:290-300 (cost 600); `auth_interceptor` cost 600.
Evidence: script — ad2 dump: both `cost 600`.
Impact: Booklet/Archive text contradicts the cost line shown next to it.
Fix idea: drop the sentence.   Changes ...: text only

### ROSTER-AD-03 · LOW · debt · `dir_fx_conscript_build` is a dead effect (S18 FAD-17, still open)
What: It multiplies `build_time` of `dir_conscript`, which has no `build_time` key (0 = "derive from cost", SimRules.BuildTicks). 0 x 0.8 = 0. The faster build comes only from the cost effect (80 -> 64 -> 25 ticks instead of 32).
Where: techs.toml:94 (`dir_fx_conscript_build`), upgrades.toml `dir_up_mass_conscription` effects; SimRules.cs:99-100; RulesDeriver.cs:~164 (Op on BuildTime).
Evidence: script — ad3.py prints `dir_fx_conscript_build target build_time None`; read — BuildTicks returns `buildTime > 0 ? buildTime : cost*40/100`.
Impact: Tooltip (UpgradeEffectText) lists "Conscript: build time x0.8" as a separate effect that does nothing; if anyone ever gives conscripts an explicit build_time, the speed-up double-counts.
Fix idea: remove the effect (changes RulesHash pin) or leave and note.   Changes a match outcome / number / visible text?: no (tooltip line disappears)

### ROSTER-AD-04 · LOW · text · Fervour tooltip reads as a nerf ("rate of fire x0.9")
What: `rate_of_fire` in data is ticks between shots, so x0.9 = faster. The generated line prints "<weapon>: rate of fire x0.9" with no hint; players read it as -10 % fire rate. The same function prints raw attr names ("rad resist permille set to 900", "ticks per point x0.75", "blackout ticks x1.5").
Where: Game/Ui/Sidebar/UpgradeEffectText.cs:30-45 (Describe), techs.toml `dir_fx_fervour_rof`, `dir_fx_lead_*`, `dir_fx_hatch_ticks`.
Evidence: read — Describe emits `attr.Replace('_',' ')` and `x`+factor verbatim; archive text for dir_up_fervour says "shoot faster".
Impact: Visible in the upgrade tooltip for Directorate players.
Fix idea: map rate_of_fire mul to "reload time" (or invert); friendlier labels for the other three.   Changes ...: visible text only

### ROSTER-AD-05 · LOW · data · `irradiated = true` still has no effect (S18 FAD-18, still open)
What: Volunteer and Reactor Crawler carry `irradiated`; its only consumer is `bonus_vs_irradiated_percent` on a warhead, and no warhead in data/rules sets it (only a schema comment mentions it).
Where: infantry.toml:147 (`dir_volunteer`), vehicles.toml:151 (`dir_reactor_crawler`); Simulation.Combat.cs:291; warheads.toml:5 (comment only).
Evidence: script — grep of all warheads.toml for `bonus_vs_irradiated` returns only the header comment.
Impact: Lore "carries the glow in his blood" has no mechanical meaning; Authority has no warhead that exploits it.
Fix idea: drop the flag or add a bonus to one Authority warhead.   Changes a match outcome?: no / yes if a bonus is added

### ROSTER-AD-06 · MED · bug · Directorate AI never builds anti-air (S18 FAD-21, still open)
What: `DirectorateRecruitFaction` does not override `AntiAirId` (base returns null), so TierPlanner's anti-air step never fires for the Directorate even though `dir_rocket_nest` (tier 1) exists. Authority, Left Behind and Expedition all set it.
Where: src/AI/Recruit/RecruitFaction.cs:82, src/AI/Recruit/TierPlanner.cs:409; DirectorateRecruitFaction.cs (no override).
Evidence: script — `grep -n AntiAirId src/AI/Recruit/*.cs` lists Authority/LB/Expedition only.
Impact: AI Directorate relies only on rocket trucks vs. Authority Interceptor/Bomber raids (T2/T3); an LLM `WantsAntiAir` directive is also ignored for it. Probably covered by an AI package; listed here because it is the Directorate's only static AA.
Fix idea: `AntiAirId => "dir_rocket_nest"`.   Changes a match outcome?: yes (AI games)

### ROSTER-AD-07 · LOW · bug · Authority Flak splash hurts friendly aircraft
What: `auth_flak_gun` uses warhead `auth_he` (`hits_allies = true`, splash 1) with `targets = "air"`. Detonate keeps allies eligible when HitsAllies and the air filter only selects aircraft, so own Interceptors within 1 cell of the target take (half) flak damage. (Buildings are excluded by the D-205 flak rule.)
Where: weapons.toml `auth_flak_gun`; warheads.toml `auth_he`; Simulation.Combat.cs:~395-405 (`!wh.HitsAllies && IsFriendly` skip, then air filter).
Evidence: read — only the shooter itself is exempt from the air filter; friendly check is skipped when HitsAllies is true.
Impact: Dogfights over an Authority base chip its own fighters; the Directorate rocket AA (`dir_rocket`, hits_allies false) does not have this.
Fix idea: separate non-friendly-fire warhead for flak (changes rules hash).   Changes a match outcome?: yes (edge case)

### ROSTER-AD-08 · LOW · text · Several archive/lore lines contradict data (S18 FAD-16 remnants, plus new)
What / Where / Evidence (all read unless noted):
- `dir_ash_sower` archive "A tracked vehicle..." (archive/units_directorate.toml:198) vs vehicles.toml:340 `locomotion = "wheeled"`.
- `dir_rocket_truck` "The rockets are loaded at the Muster Hatch" (units_directorate.toml:134) but it is built at the Assembly Bore (prereq `dir_assembly_bore`).
- `dir_volunteer` "runner / charges the enemy line": speed 12 < Conscript 14 (infantry.toml:139 vs conscript). Script: ad2/units speeds.
- `auth_up_convoy_drill` subtitle "Faster light vehicles": effects hit Jeep (light) and Scraper/Carrier (heavy), not the light Decon/Foam trucks (upgrades.toml `auth_up_convoy_drill`, techs.toml `auth_fx_convoy_*`).
- `dir_mast` "Radar and Static Broadcast tower... watches the sky" and technology.toml:120 "watches the sky for aircraft": no radar or air-detection mechanic exists in the sim (grep "radar" in src/Sim has no hits); only `jam_radius = 6` is real.
- `dir_up_hot_cores` "rounds of the crawler": the effect changes warhead `dir_rad_shell`, which the Rad Mortar and Great Dirigible also use (techs.toml `dir_fx_hot_cores`).
- `auth_foam_truck` "every unit on foot or wheel crawls at half pace": MoveMission.cs:699 slows every non-air locomotion (tracked and burrow too).
- (still true from FAD-16) Signals HQ "pings the map whenever an enemy is spotted": no sim/ UI code for it (grep signals_hq in src outside AI = 0 hits).
Impact: Archive/booklet text; players read these.
Fix idea: edit the text lines.   Changes ...: text only

### ROSTER-AD-09 · LOW · debt · Test-fixture effects still ship in data/rules/techs.toml (S18 FAD-23, still open)
What: techs.toml opens with "placeholder fixture, not balance" and defines `test_armor_up` and `test_unlock_turret`, referenced by no upgrade. The file also holds every real A/D (and LB/Exp) effect.
Where: data/rules/techs.toml:1, :7, :14.
Evidence: script — ad3.py "unreferenced fx ['test_armor_up', 'test_unlock_turret']".
Impact: confusing for modders/booklet author; they are inert.
Fix idea: delete the two entries and fix the header (re-pin RulesHash).   Changes ...: no

## Suspects (unproven)
- No A/D weapon has `targets = submerged|naval-and-submerged`; Expedition submarines hidden when dived are untargetable by Authority/Directorate except for the 5 s after they fire (Simulation.Submerge.cs SubRevealTicks). Likely accepted (land factions) but I found no decision stating it (DECISIONS D-137/D-138 only define the classes). Proof needed: an owner decision or a map with water where A/D must fight Expedition.
- Campaign allow-lists still never grant: Authority `auth_seismic_mortar, auth_foam_truck, auth_interceptor, auth_bomber, auth_blockhouse`; Directorate `dir_informant, dir_assessor, dir_volunteer, dir_bore, dir_agitprop, dir_ash_sower, dir_delver, dir_cistern, dir_bunker, dir_redoubt` (regex over `tech = [...]` in data/missions/*/mission.toml; Tech.Grant calls in lua also scanned). May be deliberate after D-205; MIS package should confirm. Notably `dir_bore` (the only way to extend the tunnel network in the Directorate campaign) is never allowed, and Directorate campaign cannot build a Cistern.
- Directorate has no weapon with `hits_burrowed` (only `auth_seismic`), so burrowers (Scraper, Mole, Delver, Bore) are only hittable on the surface; mirror/other-faction games rely on them having to surface to harvest/attack. Needs a playtest/AI-match look, not a data fix.
- `dir_fx_conscript_cost` also lowers the supply input (SupplyOf(cost)) — band stays 1 for 64 credits, so no effect; fine unless supply bands change.

## Checked and fine
- Reachability (ad3.py): every non-`buildable=false` A/D unit, building and upgrade is reachable from `auth_conyard`/`dir_shaft` through prerequisites + TierSugar; no cycles; no foreign-faction prerequisites; tier-2/3 gates (`auth_engineering`/`auth_signals_hq`, `dir_isotope_works`/`dir_committee_hall`) provide their own tier and exclude themselves.
- Producers: infantry/vehicle/aircraft each have one producing building per faction (barracks/factory/airfield; muster hatch/assembly bore/mooring). Researchers: 4 buildings per faction have `researches`.
- Upgrades (13): all `effects` ids exist, each target (type, provides name, class:, weapon:, warhead:) exists, every `attr` is handled in RulesDeriver (max_health, speed, sight, cost, build_time[see 03], rad_resist_permille, ticks_per_point, superweapon_charge_ticks, damage, rate_of_fire, contaminate, blackout_ticks); effects read via `RulesOf(owner)` (production cost, tunnel TicksPerPoint, charge, warhead). `auth_grouting` uses the `effect` key read in Simulation.Upgrades.cs:110 / Hardening.cs. 28 upgrades total < 64-bit mask.
- Atomic Battery `superweapon_entrance_range = 0` (buildings.toml:569): S18 FAD-01 fixed. Zero-damage sprays no longer deal 1 damage (Combat.cs:288, FAD-13 fixed). Aircraft are queueable in missions (MissionHost.cs:309, FAD-03 fixed). Attack-ground exists (Combat.cs AttackGround), so Decon/Foam/Sower/Seismic Mortar can be used; Agitprop aura is 800 (buff) now; needs_power now gates Searchlight/Mast (Ground.cs:125, 315); names unified through CameoNames/Archive title (FAD-15).
- Anti-class coverage: Authority AA = Flak (T2), Interceptor (T2); Directorate AA = Rocket Nest (T1), Rocket Truck (targets both). Ground weapons default to land + surface ships. Authority has the only burrow-hitting warhead (seismic mortar, now usable via attack-ground). No A/D weapon is data-impossible to fire (no min_range >= range, versus rows non-zero except the intended no-damage sprays).
- Duplicate roles: Tank/Bulwark share a cannon but differ in tier/hp/speed/cost; Marksman vs Rifleman now differ (cost 200, speed 10, range 4); Commissar and Volunteer share `dir_pistol` but Commissar has the fire-rate aura; no strict domination found among same-tier units apart from items already in S18 FAD-12 (Crawler vs Land-Train, numbers frozen).
- Subtitles vs weapons: Flak, Interceptor, Rocket Nest = `targets air`; Seismic Mortar = hits_burrowed + 200 % vs entrances; Rocketeer anti-armour = `auth_ap` 100 % vs heavy; Searchlight `sees_through_blackout`; Agitprop/Commissar auras and Agitprop jam all match data.
