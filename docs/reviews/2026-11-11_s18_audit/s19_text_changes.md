# S19 text changes (package s19-b-text, D-205)

Every player-visible string changed, old -> new. The Archive (data/archive) was not edited: its titles are the source of truth.

## 1. Sidebar, card, tooltip and "Needs ..." names = Archive titles (M13-14)
Mechanism: `CameoNames.Display` now asks the loaded Archive for the title first (set in `GameScreen`); with no Archive loaded it falls back
to the tidied id (fallback table: `tank` "Medium Tank" -> "Heavy Tank", `jeep` "Jeep" -> "Scout Car"). This also fixes tier headings
("needs Engineering Works"), upgrade effect targets and the supply message. Test: `CameoNamingTests.Every_rules_id_shows_its_archive_title`
(allow-list of exceptions is empty: every rules id has an Archive entry). 80 names changed (old = what the sidebar showed before):

| rules id | old | new (= Archive title) |
|---|---|---|
| auth_refinery | Refinery | Ashfall Refinery |
| auth_factory | War Factory | Vehicle Factory |
| auth_silo | Silo | Storage Silo |
| auth_wall | Wall | Perimeter Wall |
| dir_shaft | Shaft | Command Shaft |
| dir_bunker | Bunker | Bunker Wall |
| dir_rad_mortar | Rad Mortar | Radiation Mortar |
| dir_mast | Mast | Broadcast Mast |
| dir_mooring | Mooring | Mooring Tower |
| dir_sunrise | Sunrise | The Sunrise Device |
| auth_seismograph | Seismograph | Seismograph Station |
| auth_searchlight | Searchlight | Searchlight Tower |
| auth_engineering | Engineering | Engineering Works |
| auth_signals_hq | Signals Hq | Signals Headquarters |
| auth_flak | Flak | Flak Emplacement |
| auth_atomic_battery | Atomic Battery | Atomic Artillery Battery |
| dir_committee_hall | Committee Hall | Committee Chamber |
| neu_ruin_tenement | Ruin Tenement | Ruined Tenement |
| neu_ruin_depot | Ruin Depot | Ruined Depot |
| lb_yard | Yard | Salvage Yard |
| lb_lookout | Lookout | Lookout Mast |
| lb_bench | Bench | Tinker's Bench |
| lb_second_drift | Second Drift | The Second Drift |
| exp_beacon | Beacon | Landing Beacon |
| exp_pillbox | Pillbox | Seawall Emplacement |
| exp_picket | Picket | Sonar Picket |
| exp_sam | Sam | SAM Mount |
| dir_ashthrower | Ashthrower | Ash Thrower |
| auth_engineer | Engineer | Field Engineer |
| exp_diver | Diver | Combat Diver |
| exp_ironbound | Ironbound | Ironbound Trooper |
| auth_jeep | Jeep | Scout Car |
| auth_tank | Medium Tank | Heavy Tank |
| dir_scraper | Scraper | Burrowing Scraper |
| dir_agitprop | Agitprop | Agitprop Crawler |
| dir_landtrain | Landtrain | Land-Train |
| auth_decon_truck | Decon Truck | Decontamination Truck |
| auth_carrier | Carrier | Sealed Carrier |
| auth_foam_truck | Foam Truck | Quick-Set Foam Layer |
| auth_bomber | Bomber | Heavy Bomber |
| auth_howitzer | Howitzer | Field Howitzer |
| auth_bulwark | Bulwark | Bulwark Heavy Tank |
| lb_hauler | Hauler | Salvage Hauler |
| lb_buggy | Buggy | Dune Buggy |
| lb_tow | Tow | Tow Truck |
| lb_mother_rig_packed | Mother Rig Packed | Packed Rig |
| exp_skimmer | Skimmer | Hover Skimmer |
| exp_repair_hover | Repair Hover | Mender |
| exp_launch | Launch | Patrol Launch |
| exp_missile_sub | Missile Sub | Missile Submarine |
| exp_gyro | Gyro | Gyro Scout |
| exp_jet | Jet | Jet Fighter |
| auth_grouting | Grouting | Foundation Grouting |
| auth_up_flak_vests | Up Flak Vests | Flak Vests |
| auth_up_convoy_drill | Up Convoy Drill | Convoy Drill |
| auth_up_composite_armour | Up Composite Armour | Composite Armour |
| auth_up_sodium_lamps | Up Sodium Lamps | Sodium Lamps |
| auth_up_tungsten_shot | Up Tungsten Shot | Tungsten Shot |
| auth_up_signals_net | Up Signals Net | Signals Net |
| dir_up_mass_conscription | Up Mass Conscription | Mass Conscription |
| dir_up_tunnel_bracing | Up Tunnel Bracing | Tunnel Bracing |
| dir_up_hot_cores | Up Hot Cores | Hot Cores |
| dir_up_lead_lining | Up Lead Lining | Lead Lining |
| dir_up_blackout_canisters | Up Blackout Canisters | Blackout Canisters |
| dir_up_order_seven | Up Order Seven | Order Seven |
| dir_up_fervour | Up Fervour | Fervour |
| lb_up_scrap_plate | Up Scrap Plate | Scrap Plate |
| lb_up_ash_wraps | Up Ash Wraps | Ash Wraps |
| lb_up_quick_pack | Up Quick Pack | Quick Pack |
| lb_up_grapple | Up Grapple | Grapple Hooks |
| lb_up_salvage_rights | Up Salvage Rights | Salvage Rights |
| lb_up_patchwork | Up Patchwork | Patchwork |
| exp_up_sonar_nets | Up Sonar Nets | Sonar Nets |
| exp_up_hardened_hulls | Up Hardened Hulls | Hardened Hulls |
| exp_up_vacuum_cores | Up Vacuum Cores | Vacuum Cores |
| exp_up_composite_armour | Up Composite Armour | Composite Armour |
| exp_up_guided_shells | Up Guided Shells | Guided Shells |
| exp_up_fast_charging | Up Fast Charging | Fast Charging |
| exp_up_deep_survey | Up Deep Survey | Deep Survey |
| exp_up_veteran_drill | Up Veteran Drill | Veteran Drill |

## 2. Map and mission names
| where | old | new |
|---|---|---|
| Skirmish map list (and replay list, save list) | ash_plains_m_2p_41 | Ash Reckoning |
| | glass_coast_m_2p_11 | Glass Coast |
| | rustbelt_m2 | Rustbelt |
| | test_32 / test_64 | hidden (dev = true, done by another package) |
| Save list, mission save | "Mission auth_02, 3.0 min" | "Mission: The Junction, 3.0 min" |
| Save list, skirmish save | "Skirmish on rustbelt_m2, ..." | "Skirmish on Rustbelt, ..." |
| Mission list lock hint | "Finish auth_01 first." | "Finish Survey Convoy first." |
| Archive unlock hint | "Unlocks in the campaign (auth_02)." | "Unlocks in the campaign (The Junction)." |

Raw names stay inside saves, replays and hashes. Maps the player made keep their own name. The exp_01 text "Seawall Emplacement" now
matches the sidebar (the Pillbox is "Seawall Emplacement").

## 3. "Progress not saved" Notice (CampaignFlow)
| | old | new |
|---|---|---|
| title | Progress not saved | unchanged |
| text | The campaign progress file could not be written, so this win is not kept. Check the free disk space and that the settings folder can be written. | This win could not be saved. Check that your disk has free space. |

## 4. Missing mechanics reworded (item 26)
Cameo tooltip line (was: the Archive subtitle):
| unit | old | new |
|---|---|---|
| Minelayer | Sea mine layer | Unarmed sea hull. It cannot lay mines yet. |
| Combat Diver | Underwater saboteur | Plants charges on ships and quays from the shore. It cannot swim yet. |
| Drydock | Heavy ship repair | Unlocks the Cruiser, Carrier and Command Ship. It does not build or repair ships itself yet. |
| Destroyer | Escort warship | Gun warship that detects submarines. It has no depth charge yet. |
| Harpoon Wagon | Tethering anti-armour | Anti-armour harpoon gun. It does not tether or slow targets yet. |

Upgrade tooltip effect lines (old = generated jargon or nothing); also shown as the Archive "Effect" stat:
| upgrade | old | new |
|---|---|---|
| Scrap Plate | "<Vehicle>: max health x1.2" lines | +20% health for ground vehicles and packed rigs. / No armour change. Rotors and Kites are not covered. |
| Patchwork | (none) | Tinkers repair vehicles from 5 cells away (3 without it). |
| Quick Pack | (none) | Rigs pack 40% faster. |
| Grapple Hooks | (none) | Hook Runners plunder x1.5, up to the 1000 credit limit. / They cannot breach walls yet. |
| Salvage Rights | (none) | Salvage crews drain a wreck 1.5x faster. / The wreck's value is unchanged. |
| Sonar Nets | "Picket: sight x1.3" | Pickets see 2 cells further. |

Not changed: the Archive subtitle and body of these entries still promise the missing mechanic (lore text, not editable here).

## ROADMAP-ready "build later" list
1. Minelayer: sea-mine laying and sweeping (no rules fields; `exp_minelayer` is unarmed).
2. Combat Diver: swimming in deep water (foot units cannot enter Water), and the sonar-picket counter.
3. Drydock: produce or repair heavy ships (today only a prerequisite for Cruiser, Carrier, Command Ship).
4. Destroyer: depth charge (needs a second weapon slot; the Patrol Launch has the only depth charge).
5. Harpoon Wagon and Harpoon Post: tether and slow the target (warhead `lb_harpoon` has no slow hook).
6. Grapple Hooks: brief wall breach; cap plunder after the x1.5 bonus (mechanic fix, not in this package).
7. Salvage Rights: raise wreck value, not just drain speed (mechanic fix, not in this package).
8. Sonar Nets: add `sonar_radius` as an effect attribute so submerged boats are revealed further (mechanic fix, not in this package).
9. Scrap Plate: a real armour change that also covers Rotor and Kite (reworded instead).
10. Patchwork: a true long-range repair (today only 3 to 5 cells).
When one ships, delete its line in `src/Game/Ui/Sidebar/TodayText.cs`.
