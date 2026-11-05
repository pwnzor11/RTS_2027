# Session 9 balance tables — every row is assumed — confirm unless marked confirmed

The Q-43 table is in `docs/balance/m4_16_round4.md` (not copied here).

Sources: `data/rules/game.toml`, `infantry.toml`, `vehicles.toml`, `buildings.toml`, `warheads.toml` (storm row only).
Design context: `docs/design/13_supply_cap.md`, `docs/design/14_veterancy.md`. Where they disagree, the data wins (see the notes at the end).

## 1. Supply: constants

| Key | Value | Source | Note |
|---|---|---|---|
| `enabled` | false | game.toml `[supply]` | Will be flipped on (D-129) when the AI builds supply (SUP-5). |
| `ceiling` | 200 | `[supply]` | Hard cap. |
| `band1_max` | 400 | `[supply]` | Cost <= 400 gives band 1. |
| `band2_max` | 900 | `[supply]` | Cost <= 900 gives band 2; above gives band 3. |
| `band_values` | [1, 2, 3] | `[supply]` | Supply per band. |
| `stock_intervals` | 3 | `[supply]` | Parsed for the Expedition store only; no behaviour yet. |

Rule used for every unit below: supply = the type's `supply` override if present and >= 0; otherwise band by `cost`.
Structures cost 0 supply.

Overrides present in the data (all `(tune)`):

| Type | Faction | Cost | Override | Band would give | File |
|---|---|---|---|---|---|
| `auth_scraper` | Authority | 700 | 1 | 2 | vehicles.toml |
| `dir_scraper` | Directorate | 600 | 1 | 2 | vehicles.toml |
| `lb_gleaner` | Left Behind | 600 | 1 | 2 | vehicles.toml |
| `lb_hauler` | Left Behind | 500 | 1 | 2 | vehicles.toml |
| `lb_mother_rig_packed` | Left Behind | 4000 | 0 | 3 | vehicles.toml |

## 2. Supply: supply-providing buildings

| Id | Faction | Cost | Power | supply_provided | Tier | Max health | Note |
|---|---|---|---|---|---|---|---|
| `auth_conyard` | Authority | 0 | 0 | 20 | 1 | 1000 | Main base; not buildable |
| `auth_ration_hall` | Authority | 200 | -10 | 10 | 1 | 300 | New supply building |
| `dir_shaft` | Directorate | 0 | 0 | 20 | 1 | 1000 | Main base; not buildable |
| `dir_bunk_gallery` | Directorate | 150 | -10 | 10 | 1 | 250 | New supply building |
| `lb_mother_rig` | Left Behind | 0 | 0 | 20 | 1 | 900 | Main base; not buildable; packs into `lb_mother_rig_packed` |
| `lb_still_rig` | Left Behind | 200 | 0 | 10 | 1 | 300 | Packs into `lb_mother_rig_packed`, which gives 0 supply |

## 3. Supply: per-unit tables

### Authority (15 units: 8 at supply 1, 7 at supply 2)

| Id | Tier | Cost | Override | Supply | Max health |
|---|---|---|---|---|---|
| `auth_rifleman` | 1 | 100 | - | 1 | 50 |
| `auth_rocketeer` | 1 | 300 | - | 1 | 60 |
| `auth_engineer` | 1 | 100 | - | 1 | 50 |
| `auth_marksman` | 2 | 100 | - | 1 | 50 |
| `auth_scraper` | 1 | 700 | 1 | 1 | 350 |
| `auth_jeep` | 1 | 350 | - | 1 | 150 |
| `auth_decon_truck` | 2 | 400 | - | 1 | 300 |
| `auth_foam_truck` | 2 | 400 | - | 1 | 300 |
| `auth_tank` | 1 | 800 | - | 2 | 400 |
| `auth_carrier` | 2 | 700 | - | 2 | 350 |
| `auth_seismic_mortar` | 2 | 800 | - | 2 | 400 |
| `auth_interceptor` | 2 | 600 | - | 2 | 200 |
| `auth_bomber` | 3 | 600 | - | 2 | 200 |
| `auth_howitzer` | 3 | 800 | - | 2 | 400 |
| `auth_bulwark` | 3 | 800 | - | 2 | 400 |

### Directorate (18 units: 8 at supply 1, 8 at supply 2, 2 at supply 3)

| Id | Tier | Cost | Override | Supply | Max health |
|---|---|---|---|---|---|
| `dir_conscript` | 1 | 80 | - | 1 | 40 |
| `dir_assessor` | 1 | 80 | - | 1 | 40 |
| `dir_ashthrower` | 1 | 200 | - | 1 | 50 |
| `dir_sapper` | 2 | 250 | - | 1 | 60 |
| `dir_volunteer` | 2 | 250 | - | 1 | 60 |
| `dir_informant` | 2 | 300 | - | 1 | 40 |
| `dir_commissar` | 2 | 400 | - | 1 | 90 |
| `dir_scraper` | 1 | 600 | 1 | 1 | 300 |
| `dir_rocket_truck` | 1 | 800 | - | 2 | 250 |
| `dir_bore` | 1 | 800 | - | 2 | 300 |
| `dir_agitprop` | 2 | 700 | - | 2 | 300 |
| `dir_dirigible` | 2 | 600 | - | 2 | 200 |
| `dir_mole` | 2 | 900 | - | 2 | 400 |
| `dir_delver` | 2 | 800 | - | 2 | 300 |
| `dir_ash_sower` | 2 | 800 | - | 2 | 250 |
| `dir_great_dirigible` | 3 | 600 | - | 2 | 200 |
| `dir_reactor_crawler` | 3 | 1500 | - | 3 | 900 |
| `dir_landtrain` | 3 | 2000 | - | 3 | 1200 |

### Left Behind (17 units: 1 at supply 0, 9 at supply 1, 5 at supply 2, 2 at supply 3)

| Id | Tier | Cost | Override | Supply | Max health |
|---|---|---|---|---|---|
| `lb_scavenger` | 1 | 60 | - | 1 | 45 |
| `lb_hook_runner` | 1 | 120 | - | 1 | 40 |
| `lb_buggy` | 1 | 300 | - | 1 | 120 |
| `lb_gleaner` | 1 | 600 | 1 | 1 | 260 |
| `lb_hauler` | 1 | 500 | 1 | 1 | 260 |
| `lb_slinger` | 2 | 200 | - | 1 | 45 |
| `lb_tinker` | 2 | 150 | - | 1 | 40 |
| `lb_drifter_brute` | 2 | 300 | - | 1 | 140 |
| `lb_veilwalker` | 3 | 400 | - | 1 | 40 |
| `lb_harpoon_wagon` | 2 | 700 | - | 2 | 260 |
| `lb_organ` | 2 | 800 | - | 2 | 250 |
| `lb_tow` | 2 | 600 | - | 2 | 300 |
| `lb_dust_sail` | 2 | 600 | - | 2 | 200 |
| `lb_kite` | 3 | 500 | - | 2 | 60 |
| `lb_rolling_fort` | 3 | 1400 | - | 3 | 700 |
| `lb_rotor` | 3 | 1000 | - | 3 | 180 |
| `lb_mother_rig_packed` | 1 | 4000 | 0 | 0 | 1000 |

### Units per supply value

| Faction | Units | Supply 0 | Supply 1 | Supply 2 | Supply 3 |
|---|---|---|---|---|---|
| Authority | 15 | 0 | 8 | 7 | 0 |
| Directorate | 18 | 0 | 8 | 8 | 2 |
| Left Behind | 17 | 1 | 9 | 5 | 2 |

## 4. Veterancy

Constants (game.toml `[veterancy]`, all `(tune)`):

| Key | Value | Note |
|---|---|---|
| `enabled` | true | |
| `kill_permille_unit` | 1000 | Kill value = victim cost x permille / 1000 |
| `kill_permille_building` | 500 | |
| `heal_interval_ticks` | 20 | 1 s |
| `heal_quiet_ticks` | 100 | 5 s without firing or being hit |

Per-rank rows. Rookie is the baseline (1000 for every permille row; no key in the data). Thresholds are the kill value needed to reach the rank.

| Rank | Kill value needed | Damage dealt | Damage taken | Reload time | Speed | Self-heal (permille of max HP per interval) |
|---|---|---|---|---|---|---|
| 0 Rookie | 0 | 1000 | 1000 | 1000 | 1000 | 0 |
| 1 Veteran | 600 | 1100 | 950 | 950 | 1000 | 0 |
| 2 Elite | 1600 | 1200 | 900 | 900 | 1050 | 0 |
| 3 Hero | 3200 | 1300 | 800 | 800 | 1100 | 10 |

Types that opt out (`veteran = false`): `dir_dirigible`, `dir_ash_sower`, `auth_decon_truck`, `auth_foam_truck`, `lb_dust_sail`. The file comments say these weapons deal no damage. No infantry opts out.

## 5. Left Behind (M6)

### Buildings (lb_ prefix, buildings.toml)

| Id | Tier | Cost | Power | Max health | supply_provided | Packs into packed rig | Note |
|---|---|---|---|---|---|---|---|
| `lb_mother_rig` | 1 | 0 | 0 | 900 | 20 | yes | Not buildable; packs |
| `lb_yard` | 1 | 600 | -10 | 500 | - | no | Refinery; storage 1000 |
| `lb_refinery_rig` | 1 | 900 | -20 | 500 | - | yes | Refinery; storage 600 |
| `lb_dynamo_rig` | 1 | 200 | 60 | 300 | - | yes | Power |
| `lb_still_rig` | 1 | 200 | 0 | 300 | 10 | yes | Supply building |
| `lb_muster_rig` | 1 | 250 | -10 | 350 | - | yes | Infantry production |
| `lb_garage_rig` | 1 | 900 | -20 | 600 | - | yes | Vehicle production |
| `lb_lookout` | 2 | 400 | -10 | 250 | - | yes | Provides `lb_tier2` |
| `lb_bench` | 2 | 800 | -20 | 400 | - | no | Researches |
| `lb_rotor_pad` | 3 | 1000 | -20 | 450 | - | no | Aircraft; provides `lb_tier3` |
| `lb_spike_nest` | 1 | 200 | 0 | 300 | - | no | Defence |
| `lb_harpoon_post` | 2 | 400 | -10 | 350 | - | no | Defence |
| `lb_flak_net` | 2 | 350 | -10 | 300 | - | no | Defence |
| `lb_scrap_wall` | 1 | 30 | 0 | 150 | - | no | Wall |
| `lb_second_drift` | 3 | 2500 | -40 | 700 | - | no | Superweapon |

### Units (lb_ prefix)

Units are in section 3 with cost, override, supply and max health. Other lb_ values (infantry.toml and vehicles.toml, all `(tune)`):

| Id | Key | Value |
|---|---|---|
| `lb_scavenger` | `salvage_rate` | 4 (ash per 10 ticks) |
| `lb_tinker` | `salvage_rate` | 6 |
| `lb_tinker` | `aura_radius` / `aura_repair_permille` | 3 / 10 |
| `lb_hauler` | `salvage_rate` | 12 |
| `lb_hauler` | `harvest_capacity` | 10 |
| `lb_dust_sail` | `drops_blackout_ticks` | 200 |
| `lb_tow` | `tow_bonus_permille` | 1000 |
| `lb_veilwalker` | `ash_stealth_delay_ticks` / `ash_stealth_threshold` | 20 / 4 |

Raiders (flag only, no numeric raid key in these files): `lb_hook_runner` (infantry.toml) and `lb_buggy` (vehicles.toml) both have `raider = true`.

### Wreck, salvage, raid and storm tunables

| Key | Value | File | Note |
|---|---|---|---|
| `wreck_ticks` | 1200 | game.toml `[economy]` | Wrecks on for every faction (M6-08) |
| `salvage_rate` (lb_scavenger) | 4 | infantry.toml | Ash per 10 ticks |
| `salvage_rate` (lb_tinker) | 6 | infantry.toml | |
| `salvage_rate` (lb_hauler) | 12 | vehicles.toml | Drains wrecks into the hopper |
| `drops_blackout_ticks` (lb_dust_sail) | 200 | vehicles.toml | Blackout layer |
| `blackout_ticks` (lb_second_drift_blast) | 200 | warheads.toml | |
| `storm_radius` | 6 | warheads.toml, `lb_second_drift_blast` | Cells |
| `storm_ticks` | 800 | same | 40 s at 20 ticks/s |
| `storm_contaminate_step` | 4 | same | Low steps, never above the cap |
| `storm_contaminate_cap` | 64 | same | Denies building, does not kill |
| `storm_speed_permille` | 600 | same | Enemy vehicles slowed to 60 percent |
| `splash` / `hits_allies` | 0 / false | same | |
| `superweapon_charge_ticks` | 4800 | buildings.toml, `lb_second_drift` | |
| `superweapon_entrance_range` | 8 | same | |

## Questions for the owner

- `lb_mother_rig_packed` (vehicles.toml) costs 4000, the highest cost in the rules read here; the next highest is 3000 (`dir_sunrise`, `auth_atomic_battery`). Its file comment says the cost is prohibitive. Confirm 4000.
- Four harvesters carry `supply = 1` against a band 2 cost: `auth_scraper` (700), `dir_scraper` (600), `lb_gleaner` (600), `lb_hauler` (500). Confirm the override is intended for all four.
- `auth_bomber` (T3) and `auth_interceptor` (T2) are identical in cost 600, health 200, speed 8, sight 8. `dir_great_dirigible` (T3) and `dir_dirigible` (T2) are identical in cost 600, health 200, speed 8, sight 8. Confirm the tier 3 aircraft have no stat difference.
- `auth_howitzer` and `auth_bulwark` (both T3) copy `auth_tank` numbers (cost 800, health 400, which is T1). The file says these are placeholders. Confirm.
- `lb_veilwalker` (T3, cost 400, health 40) has supply 1, the same as `lb_scavenger` (cost 60). Units on the band edge: cost 400 gives 1 (`auth_foam_truck`, `auth_decon_truck`, `dir_commissar`, `lb_veilwalker`); cost 900 gives 2 (`dir_mole`). Confirm the edges are inclusive.

## Doc vs data notes

- Design 13 section 4 lists `[supply] enabled` with data value "true". The data has `false` until SUP-5. The tables follow the data.
- Design 13 section 2.1 lists scrapers in band 2, but the data overrides `auth_scraper` and `dir_scraper` to 1 (the harvester rule in the same section).
- Design 13 sections 2.2 and 4 name the packed rig `lb_mother_rig` with supply 0. The data's packed unit is `lb_mother_rig_packed` (supply 0). The unpacked `lb_mother_rig` gives 20.
- Design 13 names an Expedition store (`exp_provision_store`) and a "depot" main base. Neither appears in the data files read here, so neither is in the tables.
- Design 14 section 2.1 says a type ranks by default when it has a weapon. The data opts out five armed-by-name types (listed in section 4). The file comments give the reason (no damage dealt). The design does not list them.
