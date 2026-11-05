# M7-08 Expedition data: balance sheet for owner review

Data for The Overseas Expedition (`exp_` ids) in data/rules, sources: docs/design/07_expedition.md §3-§5, 11_m7_plan §2.3, §4, 13_supply_cap §2.3, docs/lore/expedition.md.
**Every number is `(tune)` and assumed — confirm.** Nothing here has been play-tested: the faction is not playable yet (`PlayableFactions` stays 3, no
faction pick, no AI). The other three factions' numbers are unchanged. Supply is the band rule of docs/design/13 (cost <= 400 = 1, <= 900 = 2, above = 3)
unless an override is shown. Speed is leptons per tick (256 = one cell). Dropped on purpose (landlocked rule, 11 §2.5): `exp_skyfreighter`, `exp_airhead`.

## Income target (11 §4) on paper

Reference (docs/balance/m6_12_acceptance.md, medium map, tier-2 AI mirrors): Authority 1892 / 2065 / 1903, Directorate 2001 / 2124 / 1709,
Left Behind 1246 / 1384 / 1320 credits per minute at 3 / 5 / 10 minutes. Mean of the three: **1713 / 1858 / 1644**; the +-10 % band is
1542-1884 / 1672-2044 / 1480-1808.

Model. Per harvester, harvested units per tick = `cap / (6 x cap + 5120 / speed)` (4 ticks to dig and 2 to unload per unit, a 10-cell one-way trip).
Scraper (cap 20, speed 18) = 0.0495; Gleaner (cap 12, speed 16) = 0.0306 = 62 % of a Scraper. The measured Left Behind / Authority ratio is
0.60-0.69 on the small map and 0.66-0.70 on the medium one, so the model matches the one faction pair that differs only by harvester. Income is taken
as proportional to it with the same harvester count (about two per side in the opening: the refinery's free one plus one built).

| Part | Assumption | Credits per minute |
|---|---|---|
| Local ash | 2 Skimmers (1 from the start kit, 1 free with the Dredge Works). Skimmer cap 5, speed 26 = 0.0220 = 44 % of a Scraper; Authority ~2000 per two scrapers | ~ 880-940 |
| Home shipments | `[shipments]` unchanged: 1500 every 1800 ticks (90 s), decay 40 permille per shipment, floor 35 %. About 2 paid by minute 3, 3 by 5, 6 by 10 (sailing time and building the Anchorage delay the first) | ~ 800 / 880 / 810 average over 0-3 / 0-5 / 0-10 min |
| **Expedition total** | | **~ 1735 / 1815 / 1745** = +1 % / -2 % / +6 % of the three-faction mean (inside the band) |

Weak spots to measure first in M7-14: the AI may build a third Skimmer (+450) or delay the Anchorage (-300 at minute 3); decay is held by charter
resets, which this estimate does not count (+); small maps raise everybody's number. Knobs in the order of 11 §4 are untouched: `[shipments] value`,
`interval_ticks`, `decay_permille`, `floor_permille`, then `local_ash_bonus_permille` (0) and the Skimmer capacity, then start cash.


## Infantry (5)

| id | cost | hp | speed | armor | weapon | supply | tier | needs | other |
|---|---|---|---|---|---|---|---|---|---|
| exp_marine | 150 | 70 | 14 | unarmored | exp_marine_rifle (10 dmg, 3.25 cells, rof 14, ground, exp_small_arms) | 1 | 1 | exp_billet | - |
| exp_pioneer | 200 | 45 | 12 | unarmored | none | 1 | 1 | exp_billet | engineer |
| exp_diver | 250 | 50 | 10 | unarmored | exp_diver_charge (120 dmg, 1 cells, rof 60, ground, exp_charge) | 1 | 1 | exp_billet | - |
| exp_pathfinder | 350 | 50 | 12 | unarmored | exp_pathfinder_rifle (40 dmg, 6 cells, rof 70, ground, exp_small_arms) | 1 | 2 | exp_billet | stealth |
| exp_ironbound | 900 | 220 | 9 | unarmored | exp_ironbound_pod (30 dmg, 5 cells, rof 30, both, exp_guided_ap) | 2 | 3 | exp_billet | - |

## Hover and land vehicles (7)

| id | cost | hp | speed | armor | locomotion | weapon | supply | tier | needs | other |
|---|---|---|---|---|---|---|---|---|---|---|
| exp_skimmer | 500 | 220 | 26 | light | hover | none | 1 | 1 | exp_dredge_works | harvest_capacity 5 |
| exp_hoverlander | 450 | 260 | 34 | light | hover | none | 2 | 1 | exp_hover_yard | passengers 5 |
| exp_survey_hover | 250 | 100 | 44 | light | hover | none | 1 | 1 | exp_hover_yard | - |
| exp_glide_tank | 900 | 380 | 32 | light | hover | exp_glide_cannon (65 dmg, 5.5 cells, rof 38, ground, exp_guided_ap) | 2 | 2 | exp_hover_yard | - |
| exp_sparker | 600 | 220 | 30 | light | hover | exp_sparker_missile (40 dmg, 6 cells, rof 30, air, exp_aa_missile) | 2 | 2 | exp_hover_yard | - |
| exp_repair_hover | 550 | 200 | 30 | light | hover | none | 2 | 2 | exp_hover_yard | - |
| exp_barrage_lander | 1100 | 300 | 14 | heavy | tracked | exp_barrage_rockets (40 dmg, 9 cells, rof 80, ground, exp_rocket_salvo) | 3 | 3 | exp_hover_yard | - |

## Ships (9)

| id | cost | hp | speed | armor | locomotion | weapon | supply | tier | needs | other |
|---|---|---|---|---|---|---|---|---|---|---|
| exp_launch | 300 | 160 | 40 | ship_light | naval | exp_launch_charge (40 dmg, 3 cells, rof 40, submerged, exp_depth_charge) | 1 | 1 | exp_quay | - |
| exp_landing_craft | 500 | 300 | 24 | ship_light | naval | none | 2 | 1 | exp_quay | passengers 8 |
| exp_supply_tender | 6000 | 250 | 16 | ship_heavy | naval | none | 0 | 1 | exp_anchorage | - |
| exp_destroyer | 900 | 450 | 28 | ship_heavy | naval | exp_destroyer_gun (30 dmg, 5.5 cells, rof 30, ground, exp_naval_shell) | 2 | 2 | exp_shipyard | sonar_radius 6 |
| exp_missile_sub | 1100 | 320 | 22 | submerged | naval | exp_sub_missile (110 dmg, 9 cells, rof 90, ground, exp_missile_strike) | 3 | 2 | exp_shipyard | deep_only, submerges |
| exp_minelayer | 500 | 220 | 24 | ship_light | naval | none | 2 | 2 | exp_shipyard | - |
| exp_cruiser | 1700 | 1200 | 16 | ship_heavy | naval | exp_cruiser_gun (90 dmg, 12 cells, rof 70, ground, exp_naval_shell_he) | 3 | 3 | exp_drydock | draw_scale_permille 2000, deep_only |
| exp_carrier | 2200 | 1500 | 12 | ship_heavy | naval | exp_carrier_aa (20 dmg, 5 cells, rof 24, air, exp_aa_missile) | 3 | 3 | exp_drydock | draw_scale_permille 2400, aircraft_slots 4, deep_only |
| exp_command_ship | 6000 | 1000 | 14 | ship_heavy | naval | none | 0 | 1 | exp_drydock | draw_scale_permille 1800 |

## Aircraft (5)

| id | cost | hp | speed | armor | locomotion | weapon | supply | tier | needs | other |
|---|---|---|---|---|---|---|---|---|---|---|
| exp_gyro | 350 | 100 | 30 | light | air | none | 1 | 2 | exp_flight_deck | - |
| exp_skyhook | 700 | 220 | 34 | light | air | none | 2 | 2 | exp_flight_deck | passengers 6 |
| exp_jet | 1000 | 160 | 60 | light | air | exp_jet_missile (35 dmg, 6 cells, rof 24, both, exp_aa_missile) | 3 | 2 | exp_flight_deck | - |
| exp_gunship | 900 | 260 | 30 | light | air | exp_gunship_rockets (25 dmg, 4.5 cells, rof 16, ground, exp_rocket_salvo) | 2 | 2 | exp_flight_deck | - |
| exp_bomber | 1100 | 200 | 44 | light | air | exp_bomb_run (200 dmg, 1 cells, rof 120, ground, exp_bomb) | 3 | 3 | exp_flight_deck | - |

## Structures (20)

| id | size | cost | hp | power | weapon | supply provided | tier | needs | other |
|---|---|---|---|---|---|---|---|---|---|
| exp_landing_depot | 3x3 | 0 | 1000 | 0 | none | +20 | 1 | (deployed ship) | beachhead 10; provides exp_landing_depot |
| exp_pile_house | 2x2 | 250 | 350 | 80 | none | - | 1 | exp_landing_depot | - |
| exp_dredge_works | 3x2 | 1800 | 650 | -30 | none | - | 1 | exp_pile_house | refinery, storage 800, free exp_skimmer |
| exp_tank_farm | 1x2 | 150 | 250 | -10 | none | - | 1 | exp_dredge_works | storage 1200 |
| exp_provision_store | 1x2 | 150 | 250 | 0 | none | +10 / +5 unstocked | 1 | exp_landing_depot | - |
| exp_billet | 2x2 | 250 | 400 | -10 | none | - | 1 | exp_pile_house | produces infantry |
| exp_hover_yard | 3x2 | 1000 | 600 | -30 | none | - | 1 | exp_dredge_works | produces vehicle |
| exp_quay | 3x2 | 600 | 500 | -20 | none | - | 1 | exp_pile_house | produces vehicle; water building; beachhead 8 |
| exp_anchorage | 2x2 | 400 | 350 | -10 | none | - | 1 | exp_pile_house | water building; shipment dock |
| exp_beacon | 1x1 | 100 | 120 | 0 | none | - | 1 | exp_landing_depot | beachhead 6 |
| exp_pillbox | 1x1 | 200 | 300 | 0 | exp_pillbox_gun (12 dmg, 3.75 cells, rof 12, ground, exp_small_arms) | - | 1 | exp_billet | - |
| exp_plotting_room | 2x2 | 700 | 400 | -30 | none | - | 2 | exp_hover_yard | researches; provides exp_tier2 |
| exp_shipyard | 3x3 | 1500 | 800 | -40 | none | - | 2 | exp_quay | produces vehicle; water building; beachhead 8 |
| exp_flight_deck | 3x3 | 1200 | 700 | -40 | none | - | 2 | exp_hover_yard | produces aircraft |
| exp_gun_battery | 2x2 | 700 | 500 | -20 | exp_battery_gun (60 dmg, 8 cells, rof 50, ground, exp_naval_shell) | - | 2 | exp_billet | - |
| exp_picket | 1x1 | 300 | 200 | -10 | none | - | 2 | exp_billet | - |
| exp_sam | 1x1 | 350 | 300 | -10 | exp_sam_missile (45 dmg, 6 cells, rof 30, air, exp_aa_missile) | - | 2 | exp_billet | - |
| exp_core_hall | 3x3 | 1500 | 700 | -60 | none | - | 3 | exp_plotting_room | researches; provides exp_tier3 |
| exp_drydock | 4x3 | 2000 | 900 | -50 | none | - | 3 | exp_shipyard | water building |
| exp_salvo_array | 3x3 | 2500 | 700 | -80 | none | - | 3 | exp_core_hall | superweapon, charge 7200 ticks |

## Upgrades (8)

| id | cost | tier | effects | note |
|---|---|---|---|---|
| exp_up_sonar_nets | 800 | 2 | 1 | stand-in or empty: see TODO in upgrades.toml |
| exp_up_hardened_hulls | 1000 | 2 | 9 | - |
| exp_up_vacuum_cores | 1500 | 3 | 3 | - |
| exp_up_composite_armour | 1500 | 3 | 12 | - |
| exp_up_guided_shells | 1500 | 3 | 5 | - |
| exp_up_fast_charging | 1200 | 3 | 1 | class:player shipment_interval mul 850 (simfu-3) |
| exp_up_deep_survey | 800 | 2 | 1 | exp_skimmer harvest_capacity add 2 (5 to 7; simfu-3) |
| exp_up_veteran_drill | 1200 | 3 | 8 | - |

## Weapons

| id | damage | range (cells) | min range | rate of fire | targets | warhead |
|---|---|---|---|---|---|---|
| exp_marine_rifle | 10 | 3.25 | 0 | 14 | ground | exp_small_arms |
| exp_diver_charge | 120 | 1 | 0 | 60 | ground | exp_charge |
| exp_pathfinder_rifle | 40 | 6 | 0 | 70 | ground | exp_small_arms |
| exp_ironbound_pod | 30 | 5 | 0 | 30 | both | exp_guided_ap |
| exp_glide_cannon | 65 | 5.5 | 0 | 38 | ground | exp_guided_ap |
| exp_sparker_missile | 40 | 6 | 0 | 30 | air | exp_aa_missile |
| exp_barrage_rockets | 40 | 9 | 2 | 80 | ground | exp_rocket_salvo |
| exp_launch_charge | 40 | 3 | 0 | 40 | submerged | exp_depth_charge |
| exp_destroyer_gun | 30 | 5.5 | 0 | 30 | ground | exp_naval_shell |
| exp_sub_missile | 110 | 9 | 0 | 90 | ground | exp_missile_strike |
| exp_cruiser_gun | 90 | 12 | 3 | 70 | ground | exp_naval_shell_he |
| exp_carrier_aa | 20 | 5 | 0 | 24 | air | exp_aa_missile |
| exp_jet_missile | 35 | 6 | 0 | 24 | both | exp_aa_missile |
| exp_gunship_rockets | 25 | 4.5 | 0 | 16 | ground | exp_rocket_salvo |
| exp_bomb_run | 200 | 1 | 0 | 120 | ground | exp_bomb |
| exp_pillbox_gun | 12 | 3.75 | 0 | 12 | ground | exp_small_arms |
| exp_battery_gun | 60 | 8 | 0 | 50 | ground | exp_naval_shell |
| exp_sam_missile | 45 | 6 | 0 | 30 | air | exp_aa_missile |

## Targets and armour (warhead damage percent; order Unarmored, Light, Heavy, Structure, Fortified, ShipLight, ShipHeavy, Submerged)

| warhead | versus | splash |
|---|---|---|
| exp_small_arms | [100, 60, 20, 10, 5, 70, 30, 0] | 0 |
| exp_guided_ap | [25, 85, 100, 60, 40, 90, 70, 0] | 0 |
| exp_aa_missile | [60, 100, 80, 40, 40, 80, 60, 0] | 0 |
| exp_naval_shell | [80, 90, 80, 100, 70, 100, 90, 0] | 0 |
| exp_naval_shell_he | [100, 85, 70, 130, 90, 100, 100, 0] | 1 |
| exp_missile_strike | [50, 80, 100, 120, 80, 110, 130, 0] | 0 |
| exp_rocket_salvo | [100, 80, 60, 70, 50, 80, 60, 0] | 1 |
| exp_bomb | [60, 70, 90, 160, 120, 100, 150, 0] | 1 |
| exp_charge | [0, 0, 0, 150, 100, 150, 150, 0] | 0 |
| exp_depth_charge | [0, 0, 0, 0, 0, 40, 30, 200] | 1 |
| exp_salvo_array_blast | [100, 100, 100, 100, 100, 100, 100, 100] | 1 |

## Tiers and start kit

- Tier 1 (landing): depot (from the deployed Command Ship), pile house, dredge works, tank farm, provision store, billet, hover yard, quay, anchorage, beacon, pillbox.
  Units: marine, pioneer, diver, skimmer, hoverlander, survey hover, launch, landing craft.
- Tier 2 gate `exp_tier2` = `exp_plotting_room` (needs the Hover Yard). Fleet: shipyard, flight deck, gun battery, picket, SAM; pathfinder, glide tank,
  sparker, mender, destroyer, missile sub, minelayer, gyro, skyhook, jet, gunship. The design also asks for a flight deck or shipyard; here either is a tier-2 building under the gate.
- Tier 3 gate `exp_tier3` = `exp_core_hall` (needs the Plotting Room). Core: drydock, Salvo Array; ironbound, barrage lander, cruiser, carrier, bomber. Cruiser and carrier also need the Drydock.
- Start kit: Command Ship (deploys into `exp_landing_depot`), 1 Landing Craft, 3 Marines, 1 Skimmer; no conyard building, `conyard = ""`.
- Design deviation: the Gyro Scout is tier 2 (design says T1) because aircraft are built at the Flight Deck, a tier-2 building.

## Not built yet (TODO lines in the data)

M7-04 landing rules (landing-cell check, beacon delivery, vehicle slots, Skyhook airlift), M7-05 submerge detail (surfaced armour, torpedoes, building sonar, diver swimming,
mines, a second weapon slot for depth charges and AA), M7-06 shipment upgrade attribute (`exp_up_fast_charging` is empty), M7-09 carrier hangar and bay repair, M7-12 Salvo Array
barrage, `harvest_capacity` effect attribute for `exp_up_deep_survey` (empty). Ships share the "vehicle" production lane with the Hover Yard (D-138 follow-up).
