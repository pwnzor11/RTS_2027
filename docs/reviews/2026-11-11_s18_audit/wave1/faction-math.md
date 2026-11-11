# faction-math: computed audit of faction data (rules, refs, tech trees, efficiency, counters, upgrades) — findings

Scope read: `src/Data/` loaders (RulesLoader, RulesInheritance, TierSugar, Unit/Weapon/Structure/Upgrade/Effect loaders, RulesReferences, RulesEnums,
FactionAiKeys), `src/Sim/Simulation.Combat.cs`, `Simulation.Upgrades.cs`, `Simulation.Veterancy.cs`, `Simulation.Submerge.cs`, `Simulation.Effects.cs`
(aura part), `Simulation.Production.cs` (queries), `Simulation.Salvage.cs` / `Raid.cs` (value formulas), `Rules/RulesDeriver.cs`, `Scripting/MissionHost.cs`
(unlock gate), `Game/Ui/Sidebar/CameoNames.cs`, `CameoProvider.cs`, `CameoTooltip.cs`, `Game/Render/SpriteResolver.cs`; all of `data/rules/*.toml`;
`data/ai/difficulty.toml`, `data/campaigns/*`, every `data/missions/*/mission.toml` + `mission.lua` (by script), sprite manifests, `data/archive/*`,
`data/audio/sounds.toml`, `docs/lore/*` (by script); `docs/DECISIONS.md` and `docs/balance/*` by search for each item below.
NOT done: no game binary was run (no duels, no matches: every balance number below is a static calculation); movement, pathing, air mission and
garrison code were not read; maps and mapgen were not checked; AI unit choice (`src/AI`) was only scanned for id literals.

Everything is reproducible: `README.md` in this folder says how. Generated files: `rules_resolved.json`, `refs_report.md`, `techtree_*.json/.dot`,
`techtree_report.md`, `efficiency_report.md` / `.csv`, `duels.csv`, `counters_report.md` / `.json`, `economy_report.md`, `upgrades_report.md`.

## Summary (worst first)
1. Aircraft can never be built by a tech-restricted mission player: the mission unlock gate has no Aircraft case. Six aircraft types across dir_04, exp_03, exp_04, lb_04 are in the allow-lists and locked.
2. A dived Missile Submarine cannot be seen or hit by Authority, Directorate or Left Behind at all, and it keeps firing (9 cells, 29 DPS on structures). Only the Expedition has sonar and depth charges.
3. The Agitprop Crawler's aura slows its own conscripts' fire by 25 % (`aura_rof_permille = 1250` is a debuff in the sim; the lore says it "hurries" them).
4. Kill value is victim cost, and three "not meant to be built" ships/rigs cost 4000-6000: one kill of a 250-HP Supply Tender (a new one sails every 30 s) makes any unit a Hero at once.
5. `exp_minelayer` (500 cr, tier 2, buildable, in the campaign allow-lists) has no weapon and no behaviour anywhere in the sim.
6. Tier sugar locks units that mission allow-lists hand out: `dir_sapper` in dir_02 (the radio line tells the player to use Sappers) and `lb_slinger` in lb_01.
7. Authority tier 2-3 is still placeholder numbers: Bulwark = exact clone of the tier-1 tank, Marksman strictly dominates the Rifleman at the same price, the tier-3 Howitzer does less than the tier-2 mortar, both aircraft fly at 0.62 cells/s (slower than a rifleman).
8. Left Behind has no mobile anti-air, and the Expedition Jet (range 6) out-ranges the only AA of Left Behind and the only static AA of the Authority (Flak Net / Flak, range 5).
9. `spoils_permille` is never set, so the raider harvester bounty is off; several upgrades do less than their text (Sonar Nets does not touch sonar).
10. Reference integrity inside `data/rules` is clean (0 misses, 0 cycles, 0 dead ends, nothing reachable for the wrong faction). Outside it: 26 types without art, 18 ending images missing, 80 sidebar names that differ from the Archive titles.

## Findings

### FACTION-MATH-01 · major · CODE · confidence high
**What:** A mission player with a tech allow-list can never build any aircraft, even when the aircraft and its airfield are in the list.
**Where:** `src/Scripting/MissionHost.cs:302-315` (`IsTypeAllowed`); called from `src/Sim/Simulation.Production.cs:106` (`IsUnlocked`), which `CanQueue` (line 139) uses.
**Evidence:** the `switch` maps Infantry, Vehicle, Structure, Defence to an id; `ProductionCategory.Aircraft` falls to `_ => null` and the method returns
`id != null && ...` = false. Aircraft are queued with category Aircraft (`TryTypeInfo`, Production.cs:36-41 refuses an air type in the Vehicle category).
`check_refs.py` lists the allow-list entries hit: dir_04 `dir_dirigible`; exp_03 and exp_04 `exp_jet`, `exp_gunship`, `exp_skyhook`; lb_04 `lb_rotor`, `lb_kite`
(lb_04 even pre-places an `lb_rotor_pad`). `data/missions/exp_03/locale.toml:17`: "Bombers from the west. Mounts and jets, please." No test covers it
(`tests/Scripting.Tests/TechTests.cs` and CampaignServiceTests only probe Vehicle).
**Impact:** in four campaign missions the player owns the air building and cannot build aircraft; the radio asks for units that cannot exist. AI players are unrestricted, so only the human is affected.
**Fix idea:** add `ProductionCategory.Aircraft` to the Vehicle case (same table, same index). One test per category. S.

### FACTION-MATH-02 · major · DESIGN · confidence high (data and code), medium (how much it hurts in play)
**What:** Three of four factions have no weapon that can target a dived submarine and no way to reveal one, while a dived `exp_missile_sub` may still fire.
**Where:** `data/rules/weapons.toml` (only `exp_launch_charge` has `targets = "submerged"`); `sonar_radius` only on `exp_destroyer` (6) and `exp_picket` (9);
`src/Sim/Simulation.Submerge.cs:50-62` (targets), `Simulation.M3.cs:192` (hidden unless sonar), D-143 ("it can still fire").
**Evidence:** `counters_report.md`, section "Target layers...": authority / directorate / left_behind vs submerged = "no weapon at all" and "no sonar_radius on any type".
Sub: 1100 cr, tier 2, `exp_sub_missile` 110 damage, range 2304 (9 cells), reload 90 ticks, warhead versus structure 120 % = 132 per shot = 29 DPS;
a 1000-HP conyard dies in 8 shots (31.5 s) to one sub. Splash also spares a dived ship (`SplashSparesSubmerged`), and every superweapon blast has versus submerged 0 or is ground-only.
Not mentioned in docs/balance or HANDOFF; D-143 decided "can still fire" but not the cross-faction consequence.
**Impact:** any building or unit within 9 cells of deep water can be destroyed by a unit the opponent can never answer. The AI does not build subs
(no `exp_missile_sub` in `src/AI`), so today it is a human-only tool: in skirmish against the three land factions and in exp_03 / exp_04 (sub in the allow-list).
**Fix idea:** owner decision. Smallest options: a dived sub cannot fire (one check in `Fire`), or firing surfaces it for N ticks, or give each land faction one sonar + depth answer. S-M.

### FACTION-MATH-03 · major · NUMBERS · confidence high
**What:** `dir_agitprop` has `aura_rof_permille = 1250`. The sim multiplies reload time by this value, so Conscripts, Ash Throwers and Sappers within 4 cells of their own Agitprop fire 25 % slower.
**Where:** `data/rules/vehicles.toml` `[dir_agitprop]` (line 130, "ASSUMED (M3-07)"); `src/Sim/Simulation.Combat.cs:238` (`ReloadTicks * RofPermille / 1000`);
`src/Sim/Simulation.Effects.cs:19-22` ("for rate of fire the LOWEST AuraRofPermille below 1000 (faster reload) ... a debuff (rof above 1000 ...) applies only when no buff of that stat reaches the actor").
**Evidence:** conscript `dir_rifle` reload 16 ticks -> 16 x 1250 / 1000 = 20 ticks: 7.5 -> 6.0 DPS. The test fixture uses 800 for a buff
(`tests/Sim.Tests/M3/Systems/SystemsFixture.cs:34`); the data test only checks radius and jam (`DirectorateDataTests.cs:311-319`). Archive text:
"It steadies and hurries nearby conscripts". The Agitprop has no weapon; its only other effect is the jam radius.
**Impact:** a 700-credit support unit weakens the army it escorts.
**Fix idea:** `aura_rof_permille = 800` and one data test asserting `< 1000`. S. Moves sim hashes of games with an Agitprop.

### FACTION-MATH-04 · major · NUMBERS · confidence high
**What:** Veterancy kill value = victim's rules cost, and three types carry a "prohibitive" cost only to keep players from building them. Killing one makes the killer a Hero immediately.
**Where:** `src/Sim/Simulation.Veterancy.cs:39-45`; `data/rules/game.toml [veterancy] thresholds = [600, 1600, 3200]`; `data/rules/vehicles.toml`
`exp_supply_tender` cost 6000 (250 HP, unarmed), `exp_command_ship` 6000, `lb_mother_rig_packed` 4000 (comments: "Not meant to be built: gated behind ... at a prohibitive cost").
**Evidence:** 6000 x `kill_permille_unit` 1000 / 1000 = 6000 >= 3200 = rank 3 (damage x1.3, reload x0.8, damage taken x0.8, self-heal). `[shipments]` sends a tender
every 600 ticks, up to 3 at sea. The Expedition's start Command Ship and the Left Behind start rig give the same. The three types also stay buildable
(tender: `exp_anchorage` + any water yard), so the sidebar offers a 6000-credit ship that does nothing when player-built.
**Impact:** any ranked unit that sinks a convoy ship is worth two of its type for the rest of the game; an Expedition opponent hands out a Hero every 30 s. Wreck value is capped at 1000, so salvage is not affected.
**Fix idea:** give vehicles a `buildable = false` (as buildings have) and set real costs, or cap kill value per victim (e.g. 2000). S-M.

### FACTION-MATH-05 · major · DATA-REF · confidence high
**What:** `exp_minelayer` is a buildable 500-credit tier-2 ship with no weapon, and no mine mechanic exists.
**Where:** `data/rules/vehicles.toml [exp_minelayer]`; nothing about mines or the id in `src/Sim` or `src/AI`; archive entry `data/archive/units_expedition.toml:262` says it "drops sea mines".
**Evidence:** `grep -rniw "mine|mines|minelayer" src/Sim src/AI` finds only an unrelated local variable; design 07 line 129 "lays sea mines (optional, cut if scope)"; DECISIONS line 901 "keep both the missile sub and the minelayer".
**Impact:** the player can pay for a unit whose described job does not exist.
**Fix idea:** hide it until mines exist (needs the vehicle `buildable` flag from 04), or cut it. S.

### FACTION-MATH-06 · major · DATA-REF · confidence high
**What:** Two mission allow-lists contain tier-2 infantry whose tier gate is not available in that mission, so they can never be trained.
**Where:** `data/missions/dir_02/mission.toml:17,28` (`dir_sapper`; gate `dir_tier2` comes from `dir_isotope_works`, which is only in `[unlocks_on_win]`);
`data/missions/lb_01/mission.toml:17,29` (`lb_slinger`; gate `lb_tier2` = `lb_lookout`, not in the list). Tier sugar: `src/Data/TierSugar.cs`.
**Evidence:** `check_refs.py` section E (closure of the human player's allow-list + pre-placed and script-created buildings). dir_02 radio line
(`locale.toml:4`): "Sappers are for searchlights." lb_01 hands Slingers out as reinforcements (`mission.lua:51`), so the gap is less visible there.
**Impact:** dir_02 tells the player to use a unit that stays locked.
**Fix idea:** pre-place or allow the gate building, or drop the id from the list and the line. `check_refs.py` can run in CI for this. S.

### FACTION-MATH-07 · major · NUMBERS · confidence high (numbers), medium (that none of it is intended)
**What:** The Authority's M4 additions still carry "ASSUMED (M4-07): copy of ..." numbers that make four of them redundant or near-useless.
**Where:** `data/rules/vehicles.toml` `auth_bulwark`, `auth_howitzer`, `auth_interceptor`, `auth_bomber`; `infantry.toml` `auth_marksman`; `weapons.toml` `auth_howitzer_gun`, `auth_marksman_rifle`.
**Evidence (`efficiency_report.md`, tables and dominated list):**
- `auth_bulwark` (tier 3) is identical to `auth_tank` (tier 1) in every combat field: 800 cr, 400 HP, heavy, speed 20, same `auth_tank_cannon`. Archive: "armour plated like a bank vault ... slow". Design 08: "heavy armour, crusher".
- `auth_marksman` (tier 2, 100 cr) = `auth_rifleman` (100 cr) with range 4 instead of 3 and nothing worse: the Rifleman is strictly dominated once tier 2 is up.
- `auth_howitzer` (tier 3, 800 cr): 20 damage per 90 ticks = 4.4 DPS on infantry, 2.2 on fortified. One Howitzer needs 40 shots = 176 s to kill a 400-HP pillbox or turret.
  The tier-2 `auth_seismic_mortar` (same cost, same hull) does 28 damage at range 9 and also hits burrowed units; the Howitzer's only gain is +1 cell. Design 03 calls it "best late-game artillery".
- `auth_interceptor` and `auth_bomber`: speed 8 leptons/tick = 0.62 cells/s (the dirigible's speed, copied). A rifleman walks 0.94; `lb_rotor` flies 2.34, `exp_jet` 4.69.
  The Interceptor is the Authority's only mobile anti-air and cannot catch anything; the Bomber has unlimited ammo, splash and 40 DPS on infantry for 600 cr (5.4x the tier-3 aircraft median value) and is held back only by that speed.
**Impact:** three sidebar slots that are never the right buy, one that makes a tier-1 unit obsolete, and an air arm that arrives after the fight.
**Fix idea:** owner numbers pass over the five types. M. No D-number covers them; s15_siege.md is about AI siege behaviour, not howitzer damage.

### FACTION-MATH-08 · major · NUMBERS · confidence high (data), medium (play)
**What:** Anti-air coverage is uneven enough to leave units with no counter.
**Where:** `data/rules/weapons.toml` `targets` values; `counters_report.md` "no cost-effective counter" table and layer gaps.
**Evidence:** weapons that can hit aircraft: Authority `auth_flak` (static, range 5) and `auth_interceptor` (0.62 cells/s); Directorate `dir_rocket_truck` (`both`, range 8)
and `dir_rocket_nest` (range 6); Left Behind `lb_flak_net` only (static, range 5, needs power); Expedition Sparker, Ironbound, Jet, SAM, Carrier.
`exp_jet` range 1536 (6 cells) > 5: it can kill Flak and Flak Nets from outside their range. Left Behind then has nothing that can shoot up; the Authority only the Interceptor.
Left Behind vs `auth_bomber`, `dir_great_dirigible`, `exp_gunship`, `exp_bomber`: "static defences only".
**Impact:** an air group is unanswerable for Left Behind in the field; design 06 lists "Flak" as the Rotor's weakness but gives the faction no mobile flak of its own.
**Fix idea:** raise Flak / Flak Net range to 6+ (or lower the Jet's), and give Left Behind one `targets = "both"` weapon (the Harpoon Wagon is the obvious carrier). S.

### FACTION-MATH-09 · minor · NUMBERS · confidence high
**What:** Raider spoils are switched off: `spoils_permille` is never set in `game.toml`, and its default is 0.
**Where:** `data/rules/game.toml [economy]` (key absent); `src/Sim/Rules/EconomyRules.cs:51`; `src/Sim/Simulation.Raid.cs:98-105`. Design 06 line 47 and design 10 line 39 list the key among the M6 numbers.
**Evidence:** `if (permille <= 0) return;`. `plunder_percent` (20) and `salvage_percent` (25) have working defaults; only spoils has none.
**Impact:** `lb_buggy` / `lb_hook_runner` harvester kills pay nothing; half of the documented `raider` effect is absent.
**Fix idea:** set a value (the design gives none) or remove the mechanic from the docs. S.

### FACTION-MATH-10 · minor · NUMBERS · confidence high (numbers)
**What:** Upgrade value is very uneven and several upgrades do less than their text. No upgrade targets a field the sim ignores.
**Where:** `data/rules/upgrades.toml`, `techs.toml`; `upgrades_report.md` (every change with old -> new values).
**Evidence:**
- All 80 upgrade effects use attributes `RulesDeriver` applies (only the unused fixture `test_unlock_turret` does not).
- `exp_up_sonar_nets` (800 cr): `exp_picket.sight` 9 -> 11. Sonar reveal uses `sonar_radius` (9), which no effect can change (not in `BuildingAttrs`). The upgrade does not extend sonar (the TOML comment admits "sight stands in for sonar").
- `auth_up_convoy_drill` (800 cr, "x1100"): integer truncation gives Scraper and Carrier 18 -> 19 (+5.6 %), Jeep 36 -> 39 (+8.3 %).
- `dir_up_mass_conscription`: the `build_time x800` effect multiplies an explicit build time of 0 and changes nothing; the build time still drops 32 -> 25 ticks only because it is derived from the reduced cost (80 -> 64).
- Near-useless for the price: `auth_up_sodium_lamps` 1500 cr for +2 sight on one building type; `auth_up_signals_net` 2500 cr for +2 sight on buildings; `dir_up_blackout_canisters` 1500 cr touches one weapon on one unit (`dir_dirigible`; the Great Dirigible has no blackout weapon although design 08 says "rad bombs + Blackout").
- Must-buy by comparison: `lb_up_scrap_plate` 500 cr = +20 % HP on all nine Left Behind vehicles (the Authority pays 1500 for +20 % on two types); `exp_up_deep_survey` 800 cr = Skimmer load 5 -> 7 (+40 %);
  `exp_up_composite_armour` 1500 cr = +15 % HP on 12 types; `auth_up_tungsten_shot` 2500 cr = +25 % damage on both Authority tanks.
- Research time: Authority / Directorate upgrades have no `build_time`, so 800 / 1500 / 2500 cr take 16 / 30 / 50 s; Expedition and Left Behind upgrades take 5-7.5 s. The Left Behind comment says "about half the build time of the Authority ones"; it is a quarter to a sixth.
- `class:infantry` / `class:building` effects (Flak Vests, Signals Net) rewrite every faction's types in the owner's rules copy; harmless unless a player owns foreign types.
**Impact:** several dead buttons on the upgrade tab, two no-brainers.
**Fix idea:** numbers pass; add `sonar_radius` to the effect attribute lists and point Sonar Nets at it. S-M.

### FACTION-MATH-11 · minor · NUMBERS · confidence high (numbers), low (that it matters: D-102 makes Left Behind cheap on purpose)
**What:** Cost and build time to reach tier 3 and the superweapon differ by 2-3x between factions.
**Where:** `techtree_report.md` (cheapest prerequisite chain + item, structures built one after another).
**Evidence:** tier-3 gate standing for: Left Behind `lb_rotor_pad` 3300 cr / 66 s; Expedition `exp_core_hall` 5250 / 84 s; Authority `auth_signals_hq` 7300 / 126 s;
Directorate `dir_committee_hall` 7600 / 132 s. Superweapon standing: Left Behind 5800 cr / 116 s (charge 240 s), Expedition 7750 / 134 s (charge 360 s),
Authority 10300 / 186 s (150 s), Directorate 10600 / 192 s (150 s). `lb_yard` costs 600 and ships a free 600-credit `lb_gleaner` (net cost 0, storage 3000);
build + sell (50 % refund) yields a Gleaner for 300 without owning a Garage Rig. No cycles, no dead ends, no unreachable item in any tree.
**Impact:** a consequence of D-102 / D-157 the owner may not have seen in one table: Left Behind's tier-3 units can arrive when the others reach tier 2.
**Fix idea:** none if intended; otherwise raise `lb_rotor_pad`. Decide whether the Yard sell loop is acceptable. S.

### FACTION-MATH-12 · minor · NUMBERS · confidence medium (static model: no crushing, no splash on groups, no pathing)
**What:** Static cost-efficiency outliers (beyond 2x / 0.5x the pooled tier-and-role median) that no decision explains.
**Where:** `efficiency_report.md` "Outliers", `counters_report.md` duel matrices, `duels.csv`.
**Evidence:**
- `lb_scavenger` 60 cr: 87 % of a Rifleman's DPS and 90 % of its HP for 60 % of the price; value 2.6x the tier-1 infantry median; equal-budget ratio 2.0 vs Riflemen, 2.2 vs Conscripts, 2.3 vs Marines, 3.3 vs `auth_tank`. Limited by supply (1 each); D-186's army cap treats the symptom.
- Rifle infantry beats tanks for cost unless crushed: 10 Conscripts (800 cr) vs 1 `auth_tank`: the tank needs 4 shots (8 s) per man (AP 25 % = 15 damage vs 50 HP), 80 s in all; the squad needs 32 s. Ratio 0.52.
- `dir_commissar` 400 cr, tier 2 behind the Isotope Works: 90 HP, a 5-DPS pistol, no aura, no special key. Value 0.10x median. `dir_volunteer` 250 cr: 5 DPS plus a 20-damage death burst.
- `dir_rad_mortar` 700 cr defence: 2.7 DPS (20 per 90 ticks); value 0.09x the tier-2 defence median. Its worth is contamination only.
- `lb_rotor` 1000 cr tier-3 aircraft: 17.5 DPS on infantry, 2.5 on heavy; the 300-credit Buggy does 15.0 / 2.5.
- `exp_pillbox` 200 cr, no power: 20 DPS on infantry at 3.75 cells; `auth_turret` 400 cr, needs power: 13.3 DPS; `dir_pillbox` 400 cr, needs power: 17.5 DPS at 3 cells. 4x the value of either.
- `exp_diver` 250 cr: 0 % versus all three mobile armour classes (deals the 1-damage floor); buildings and ships only. `exp_launch` 300 cr can only hit dived subs, which no enemy faction owns.
- `lb_kite` has no one-use rule: it dies because its own `hits_allies` splash (60 at half) equals its 60 HP, and the death weapon then fires the same bomb again (240 total, 288 on structures). `rate_of_fire = 1`: every +60 HP it ever gains is one more 120-damage bomb on the next tick.
- Expedition armed mobile units: median value 39 vs 109-184 for the other three (median cost 900 vs 400-600). docs/balance s12 shows 43-80 % AI win rates, so shipments and range make up for it; it is still the largest systematic gap in the tables.
**Impact:** candidates for the next numbers pass; the suspects list says what to measure.
**Fix idea:** per item, after duels. M.

### FACTION-MATH-13 · minor · TEXT · confidence high
**What:** The sidebar, tooltips and selection card build names from the rules id; 80 of them differ from the owner-approved Archive titles, and every tiered upgrade shows as "Up ...".
**Where:** `src/Game/Ui/Sidebar/CameoNames.cs:11-35`; used by `CameoTooltip.cs:40`, `SidebarPanel.cs:566,809,881`, `CardModelBuilder.cs:152,203`. List: `refs_report.md` "Display-name mismatches".
**Evidence:** `auth_tank` shows "Medium Tank", Archive "Heavy Tank"; `auth_factory` "War Factory" vs "Vehicle Factory"; `auth_jeep` "Jeep" vs "Scout Car"; `exp_repair_hover` "Repair Hover" vs "Mender";
`exp_pillbox` "Pillbox" vs "Seawall Emplacement"; `auth_signals_hq` "Signals Hq"; `exp_sam` "Sam"; `auth_up_flak_vests` "Up Flak Vests"; `lb_mother_rig_packed` "Mother Rig Packed" vs "Packed Rig".
No two types of one faction collide on a sidebar name.
**Impact:** two names for one thing between the sidebar and the Archive; the booklet should take names from `data/archive` titles, not from ids.
**Fix idea:** `CameoNames.Display` looks the Archive title up first (the tooltip already receives the Archive subtitle). S.

### FACTION-MATH-14 · minor · DATA-REF · confidence high
**What:** Missing or dangling assets and entries outside `data/rules` (full list in `refs_report.md` sections E and F).
**Where / Evidence:**
- 26 types have no sprite manifest (generated placeholder in the world and on the cameo): Authority 15 (`auth_airfield`, `auth_atomic_battery`, `auth_blockhouse`, `auth_bomber`, `auth_bulwark`, `auth_carrier`, `auth_engineer`, `auth_flak`,
  `auth_foam_truck`, `auth_howitzer`, `auth_interceptor`, `auth_marksman`, `auth_ration_hall`, `auth_seismic_mortar`, `auth_signals_hq`), Directorate 8 (`dir_ash_sower`, `dir_assessor`, `dir_bunk_gallery`,
  `dir_committee_hall`, `dir_delver`, `dir_great_dirigible`, `dir_redoubt`, `dir_volunteer`), `lb_still_rig`, both `neu_ruin_*`. Nine are tier 1.
- `has_turret = true` with no `<id>_turret` sheet: `auth_tank` (drawn from `tank_v2`) and `dir_landtrain`. `data/sprites/vehicles/auth_tank.*` exists but is never drawn (alias to `tank_v2` in `SpriteResolver.cs:19` and `CameoProvider.cs:17`).
- 18 ending slide images named in `data/campaigns/*_ending.toml` have no file in `data/images/stills` (only the 12 act stills exist).
- `data/archive/technology.toml:133` unlock `campaign:auth_05`: there is no mission auth_05, so `tech_atomic_artillery` can never unlock.
- 42 unit / building / upgrade ids appear nowhere in `docs/lore/*.md` although each has an Archive entry (all M4 additions, all Expedition upgrades); the Archive header says its text is "copied verbatim" from docs/lore.
- `data/rules/techs.toml` still opens with "placeholder fixture, not balance" and two dead effects (`test_armor_up`, `test_unlock_turret`) that are hashed into the rules.
- `data/audio/sounds.toml [weapon]` maps 5 of 61 weapons (the M2 Authority set); the rest use the class sound.
- `supply_unstocked` (on `exp_provision_store`) is parsed and never read (stated in the game.toml comment).
**Impact:** placeholders in tier-1 play for two factions; one Archive entry locked forever.
**Fix idea:** art backlog; fix the `auth_05` unlock; move the fixtures to tests. S each.

### FACTION-MATH-15 · idea · DESIGN · confidence high
**What:** Burrowed units cannot be detected or hit by three factions (Directorate itself, Left Behind, Expedition): `hits_burrowed` exists only on `auth_seismic`, `seismic_radius` only on `auth_seismograph`.
**Where:** `data/rules/warheads.toml`, `buildings.toml`; `Simulation.M3.cs:191`; D-074 pairs the mechanic with the Authority only.
**Evidence:** `counters_report.md` layer gaps. Burrowed units cannot fire, so unlike 02 this is an escape and transit advantage (`dir_scraper`, `dir_mole` with 5 passengers, `dir_delver`), not free damage.
**Impact:** Left Behind and Expedition can never stop a burrowed Mole drop or kill a fleeing burrowed Scraper.
**Fix idea:** owner call when the newer factions get their Directorate matchup pass (a Lookout / Picket `seismic_radius`, one `hits_burrowed` warhead each). S.

## Top 12 suspects worth confirming with real duels or AI matches
Duel syntax: `Tools ai duel --a <id:n,...> --b <id:n,...> [--ticks 3000] [--gap 12]`. "Expect" is what the static model predicts.
1. `--a dir_conscript:6,dir_agitprop:1 --b dir_conscript:6`: expect the side WITH the Agitprop to lose the rifle fight (reload 20 vs 16 ticks) if its conscripts stay within 4 cells of it. Confirms 03.
2. One dived `exp_missile_sub` within 9 cells of an `auth_conyard` + `auth_turret` (sandbox or mission script; `ai duel` cannot dive): expect the buildings to die and the sub to take 0 damage and never be targeted. Confirms 02.
3. Any armed unit sinks an `exp_supply_tender` (`--a exp_destroyer:1 --b exp_supply_tender:1`, or any match log vs the Expedition): expect `RankOf` = 3 on the next tick. Confirms 04.
4. `--a lb_kite:1 --b auth_refinery:1`: expect the Kite to die on its first shot and the Refinery to lose 144 + 144; if the Kite survives a tick, expect 144 per tick. Confirms the Kite note in 12.
5. `--a dir_conscript:10 --b auth_tank:1` and `--a lb_scavenger:13 --b auth_tank:1`: model says the infantry win in about 30 s; if tanks crush on attack-move the result flips. Tells whether tier-1 armour is worth 800.
6. `--a lb_scavenger:10 --b auth_rifleman:6`, `--b dir_conscript:8` (640), `--b exp_marine:4` (600 cr each side): expect Scavengers to win all three with about half their men left.
7. `--a auth_howitzer:3 --b dir_pillbox:1` against `--a auth_seismic_mortar:3 --b dir_pillbox:1`: expect about 60 s for the Howitzers and about 42 s for the tier-2 mortars.
8. `--a exp_jet:2 --b auth_flak:3` and `--b lb_flak_net:3`: if aircraft hold at their 6-cell range the flak never fires. Then `--a exp_jet:2 --b auth_interceptor:4`: expect the Interceptors (0.62 c/s) never to reach firing range.
9. `--a exp_cruiser:1 --b auth_howitzer:3` across a shoreline: expect the Cruiser (range 12, min 3) to kill them from outside range 10. Land answers are aircraft only (`auth_bomber`, `dir_great_dirigible`, `lb_rotor`, `lb_kite`).
10. `--a dir_sapper:3 --b auth_tank:1` (750 vs 800 cr) and `--a dir_sapper:2 --b auth_turret:1`: Sappers (150 damage, 100 % vs heavy and fortified, range 1) should win if one arrives; model ratio 4-6 against vehicles of equal cost.
11. `--a exp_pillbox:2 --b auth_rifleman:8` vs `--a auth_turret:1 --b auth_rifleman:8` vs `--a dir_pillbox:1 --b auth_rifleman:8` (400 cr of defence each): expect the Expedition pair to hold and the other two to fall.
12. AI matches `tiered:3` Left Behind vs each faction with the D-186 `army_cap` removed, logging the tick of the first `lb_rotor_pad` and the Scavenger share of the army: expect tier 3 near tick 2500-3500 (3300 cr chain) against 6000+ for Authority / Directorate.

## Open questions
- Is "a dived sub can still fire" (D-143) meant to stand now that three factions cannot answer it?
- Is the Bulwark meant to differ from the tank in anything? Both have the same `crusher` value in the data.
- Do aircraft hover at weapon range and fire continuously (assumed from `CombatMissions.cs:132-139`)? If so `auth_bomber` (range 2, own `hits_allies` splash 1) may hurt itself whenever it is within 1 cell of the impact; not verified.
- Splash has no air exemption (`Simulation.Combat.cs` Detonate): ground artillery landing under an aircraft hurts it. Intended?
- Should vehicles get a `buildable` flag? Three types use a 4000-6000 cost as a stand-in, with the side effects in 04.
- Is a player-built second `exp_command_ship` (a second Landing Depot for 6000) meant to be possible?

## Things checked and found sound
- `data/rules` internal references: every weapon -> warhead, unit/building -> weapon, death weapon, free unit, deploys / unpacks / packs / morphs, prerequisite, aura target, effect target, upgrade effect, start kit, tier provides, tender type and `<superweapon>_blast` warhead resolves (0 misses). No `base` inheritance is used anywhere.
- No orphan weapon, warhead or provides name; the only dead entries are the two fixture effects.
- Skirmish closure per faction: nothing of another faction is obtainable, and every own type is obtainable (authority 18 buildings / 15 units / 7 upgrades, directorate 19 / 18 / 7, left_behind 15 / 17 / 6, expedition 20 / 26 / 8). Production is gated only by prerequisites, not by faction, and no prerequisite or provides name crosses factions.
- Tech trees: no cycles, no dead ends; every faction has its research buildings and its aircraft / ship producers.
- Every id literal in `src/**/*.cs` (AI build orders included), `difficulty.toml`, mission `tech` lists, pre-placed actors and Lua string literals resolves to a real id (`neu_ruin` in MapReport.cs is a prefix test).
- Campaign files: missions, `requires`, `open_after`, endings, act stills, mission maps, scripts, briefings and locale files all exist.
- Archive: every unit, building and upgrade has an entry with the right category, faction and a subtitle; no entry points at a missing rules id.
- All upgrade effect attributes are applied by `RulesDeriver`; reload is exactly `rate_of_fire` ticks (checked the Fire / RunCombat countdown order).
