# M8-13 · Archive campaign unlock ids

Status: M8-13 tuning. All 85 `campaign:TBD` unlock lines are replaced with mission ids (`auth_01..04`, `dir_01..04`, `exp_01..04`, `lb_01..04`). Each changed line carries `# M8-13 (tune)`. Entry ids and text are unchanged. The stale `replace TBD` TODO comment above each changed entry was removed.

## Rules used
- **a** own unit, structure or upgrade: the act where the story introduces it; superweapon buildings in act 4.
- **b** leader entry (classified or not): released by another faction's mission in the same act.
- **c** `faction_*.toml`: act 2-3 of the owning faction.
- **d** crossing: placed on another faction's mission to respect the 8-per-mission cap (`max 8`). Flagged below.

## Counts per mission
| mission | entries |
|---|---|
| auth_01 | 1 |
| auth_02 | 6 |
| auth_03 | 5 |
| dir_01 | 5 |
| dir_02 | 6 |
| dir_03 | 8 |
| dir_04 | 8 |
| exp_01 | 8 |
| exp_02 | 8 |
| exp_03 | 8 |
| exp_04 | 2 |
| lb_01 | 1 |
| lb_02 | 3 |
| lb_03 | 8 |
| lb_04 | 8 |

## Entries
| entry id | file | mission | rule |
|---|---|---|---|
| `auth_seismograph` | structures_authority.toml | auth_01 | a |
| `auth_decon_truck` | units_authority.toml | auth_02 | a |
| `auth_engineering` | structures_authority.toml | auth_02 | a |
| `auth_grouting` | upgrades.toml | auth_02 | a |
| `auth_searchlight` | structures_authority.toml | auth_02 | a |
| `exp_up_deep_survey` | upgrades.toml | auth_02 | d |
| `faction_auth_daily_life` | faction_authority.toml | auth_02 | c |
| `auth_gate` | structures_authority.toml | auth_03 | a |
| `exp_up_composite_armour` | upgrades.toml | auth_03 | d |
| `exp_up_vacuum_cores` | upgrades.toml | auth_03 | d |
| `exp_up_veteran_drill` | upgrades.toml | auth_03 | d |
| `faction_auth_army` | faction_authority.toml | auth_03 | c |
| `dir_agitprop` | units_directorate.toml | dir_01 | a |
| `dir_bore_head` | structures_directorate.toml | dir_01 | a |
| `dir_informant` | units_directorate.toml | dir_01 | a |
| `dir_mast` | structures_directorate.toml | dir_01 | a |
| `dir_sapper` | units_directorate.toml | dir_01 | a |
| `dir_ashthrower` | units_directorate.toml | dir_02 | a |
| `dir_commissar` | units_directorate.toml | dir_02 | a |
| `dir_mole` | units_directorate.toml | dir_02 | a |
| `dir_rad_mortar` | structures_directorate.toml | dir_02 | a |
| `faction_dir_ideology` | faction_directorate.toml | dir_02 | c |
| `faction_dir_surfacing` | faction_directorate.toml | dir_02 | c |
| `dir_bore` | units_directorate.toml | dir_03 | a |
| `dir_isotope_works` | structures_directorate.toml | dir_03 | a |
| `dir_mooring` | structures_directorate.toml | dir_03 | a |
| `dir_rocket_nest` | structures_directorate.toml | dir_03 | a |
| `dir_rocket_truck` | units_directorate.toml | dir_03 | a |
| `exp_sparker` | units_expedition.toml | dir_03 | d |
| `exp_up_fast_charging` | upgrades.toml | dir_03 | d |
| `faction_dir_voice` | faction_directorate.toml | dir_03 | c |
| `dir_dirigible` | units_directorate.toml | dir_04 | a |
| `dir_reactor_crawler` | units_directorate.toml | dir_04 | a |
| `leader_auth_dray` | leaders.toml | dir_04 | b |
| `leader_auth_dray_secret` | leaders.toml | dir_04 | b |
| `leader_auth_fenn` | leaders.toml | dir_04 | b |
| `leader_auth_fenn_secret` | leaders.toml | dir_04 | b |
| `leader_auth_pell_secret` | leaders.toml | dir_04 | b |
| `leader_auth_wrenfield_secret` | leaders.toml | dir_04 | b |
| `exp_barrage_lander` | units_expedition.toml | exp_01 | a |
| `exp_core_hall` | structures_expedition.toml | exp_01 | a |
| `exp_destroyer` | units_expedition.toml | exp_01 | a |
| `exp_gun_battery` | structures_expedition.toml | exp_01 | a |
| `exp_pathfinder` | units_expedition.toml | exp_01 | a |
| `exp_picket` | structures_expedition.toml | exp_01 | a |
| `exp_repair_hover` | units_expedition.toml | exp_01 | a |
| `exp_up_sonar_nets` | upgrades.toml | exp_01 | a |
| `exp_cruiser` | units_expedition.toml | exp_02 | a |
| `exp_diver` | units_expedition.toml | exp_02 | a |
| `exp_ironbound` | units_expedition.toml | exp_02 | a |
| `exp_minelayer` | units_expedition.toml | exp_02 | a |
| `exp_plotting_room` | structures_expedition.toml | exp_02 | a |
| `exp_shipyard` | structures_expedition.toml | exp_02 | a |
| `exp_up_guided_shells` | upgrades.toml | exp_02 | a |
| `exp_up_hardened_hulls` | upgrades.toml | exp_02 | a |
| `exp_carrier` | units_expedition.toml | exp_03 | a |
| `exp_drydock` | structures_expedition.toml | exp_03 | a |
| `exp_flight_deck` | structures_expedition.toml | exp_03 | a |
| `exp_gunship` | units_expedition.toml | exp_03 | a |
| `exp_jet` | units_expedition.toml | exp_03 | a |
| `exp_missile_sub` | units_expedition.toml | exp_03 | a |
| `exp_sam` | structures_expedition.toml | exp_03 | a |
| `exp_skyhook` | units_expedition.toml | exp_03 | a |
| `exp_bomber` | units_expedition.toml | exp_04 | a |
| `exp_salvo_array` | structures_expedition.toml | exp_04 | a |
| `lb_slinger` | units_left_behind.toml | lb_01 | a |
| `faction_lb_authority` | faction_left_behind.toml | lb_02 | c |
| `faction_lb_leaders` | faction_left_behind.toml | lb_02 | c |
| `lb_veilwalker` | units_left_behind.toml | lb_02 | a |
| `faction_lb_directorate` | faction_left_behind.toml | lb_03 | c |
| `lb_harpoon_wagon` | units_left_behind.toml | lb_03 | a |
| `lb_rolling_fort` | units_left_behind.toml | lb_03 | a |
| `leader_dir_harrowgate_secret` | leaders.toml | lb_03 | b |
| `leader_dir_larkin_secret` | leaders.toml | lb_03 | b |
| `leader_dir_lund_secret` | leaders.toml | lb_03 | b |
| `leader_dir_thrall` | leaders.toml | lb_03 | b |
| `leader_dir_thrall_secret` | leaders.toml | lb_03 | b |
| `faction_exp_culture` | faction_expedition.toml | lb_04 | c |
| `lb_second_drift` | structures_left_behind.toml | lb_04 | a |
| `leader_dir_lund` | leaders.toml | lb_04 | b |
| `leader_exp_aldersey_secret` | leaders.toml | lb_04 | b |
| `leader_exp_kell` | leaders.toml | lb_04 | b |
| `leader_exp_kell_secret` | leaders.toml | lb_04 | b |
| `leader_exp_roque_secret` | leaders.toml | lb_04 | b |
| `leader_exp_strand_secret` | leaders.toml | lb_04 | b |

## UNSURE / flags
- **Planned Authority missions.** `auth_02`-`auth_04` are not yet in `data/campaigns/authority.toml`; the ids reference the act table. Until they land, these entries stay locked.
- **Crossings (rule d).** Expedition act 2-3 items moved off `exp_04` onto `auth_02`/`auth_03` (`exp_up_deep_survey`, `exp_up_vacuum_cores`, `exp_up_composite_armour`, `exp_up_veteran_drill`) and onto `dir_03` (`exp_sparker`, `exp_up_fast_charging`). `faction_exp_culture` stays on `lb_04`. Story fit is plausible but these are not rule a/b/c mappings.
- **`dir_04` gets Authority leaders (rule b).** Act 4, where the Authority stops the Directorate's device. `dir_reactor_crawler` and `dir_dirigible` are late heavy units that rule a would put in act 3; they sit on `dir_04` at the coordinator's request.
- **Leader act choice is an assumption.** The plan does not say which act each leader meets another faction. Authority-Directorate in act 4 (`dir_04`), Left Behind-Directorate in act 3 (`lb_03`), Left Behind-Expedition in act 4 (`lb_04`).
- **`lb_second_drift` is a superweapon building** (`superweapon = lb_second_drift` in rules), so act 4 (`lb_04`).
- **`auth_01` has one entry** (`auth_seismograph`). The other Authority own entries are spread over `auth_02`/`auth_03`.
- **Test change.** `tests/Game.Tests/Archive/ArchiveLogicTests.cs` (`Browser_navigates_tabs_and_entries_with_wrapping_and_counts_locked_entries`) now marks `dir_04`, `lb_03` and `lb_04` instead of `TBD`. It asserts the unlocked count is below 24 after `dir_04`, then 24 after all three. The 24-entry total is unchanged.
