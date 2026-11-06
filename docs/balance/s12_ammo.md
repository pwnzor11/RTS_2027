# S12 aircraft ammo and rearm (M7-16, D-156)

Owner request: "4 or 5 shots from the aircraft, then they need to go back to the carrier to reload; balance this against the other
units' damage, but the carrier should be pretty powerful."

## Numbers (all `# (tune) D-156` in data/rules/vehicles.toml)

| unit | ammo | rearm_ticks (empty to full) |
|---|---:|---:|
| exp_jet | 5 | 150 |
| exp_gunship | 5 | 150 |
| exp_bomber | 4 | 200 |
| exp_gyro, exp_skyhook (no weapon) | unlimited (0) | none |

Carrier numbers unchanged (cost 2200, 1500 hp, 4 slots, sortie_ticks 200). Other factions unchanged (ammo 0 = unlimited).
Docked aircraft rearm one round per rearm_ticks/ammo ticks while in the hangar, alongside the repair.

## Method

`CarrierBalanceProbe` (tests/Sim.Tests/M7, runs only with env RTS_BALANCE_PROBE=<out file>): the carrier group sits 6 cells off a flat
shoreline, the foe group (other faction, roughly equal cost) attack-moves at it from the shore; whenever every aircraft is docked and
full the carrier launches a sortie at the nearest foe (this is the AI's own launch rule). "unlimited" = the same data with ammo zeroed
(the before case), "ammo" = shipped data. 8000-tick cap. `ai duel` cannot dock aircraft on a carrier, hence the probe.

## Results (own alive / own hp at the end)

| carrier group | cost | foes | cost | before (unlimited) | after (ammo) |
|---|---:|---|---:|---|---|
| carrier + 4 jets | 6200 | 7 auth_tank | 5600 | win, 5/5, 100%, tick 745 | win, 5/5, 100%, tick 1549 |
| carrier + 4 jets | 6200 | 7 auth_howitzer | 5600 | win, 5/5, 100%, tick 745 | win, 5/5, 100%, tick 1549 |
| carrier + 4 jets | 6200 | 10 auth_interceptor (AA) | 6000 | win, 1/5, 60%, tick 1713 | win, 1/5, 55%, tick 1781 |
| carrier + 4 gunships | 5800 | 7 auth_tank | 5600 | win, 5/5, 100%, tick 697 | win, 5/5, 100%, tick 1401 |
| carrier + 4 gunships | 5800 | 7 auth_howitzer | 5600 | win, 5/5, 100%, tick 697 | win, 5/5, 100%, tick 1401 |
| carrier + 4 gunships | 5800 | 10 auth_interceptor | 6000 | win, 5/5, 100%, tick 1965 | win, 5/5, 100%, tick 1965 |
| carrier + 2 bombers + 2 gunships | 6200 | 7 auth_tank | 5600 | win, 3/5, 83%, tick 753 | win, 3/5, 83%, tick 1505 |
| carrier + 2 bombers + 2 gunships | 6200 | 7 auth_howitzer | 5600 | win, 3/5, 83%, tick 753 | win, 3/5, 83%, tick 1505 |
| carrier + 2 bombers + 2 gunships | 6200 | 10 auth_interceptor | 6000 | win, 5/5, 100%, tick 1893 | win, 5/5, 100%, tick 1893 |
| any of the three | ~6000 | 7 dir_rocket_truck (hits air) | 5600 | foes win | foes win (same) |
| any of the three | ~6000 | 10 exp_sparker (AA) | 6000 | stalemate | stalemate (same) |

Reading: ammo roughly doubles the time the group needs against ground armies (aircraft shuttle home between waves) but the carrier group
still wins every equal-cost duel against non-counters with no losses, so it stays clearly strong. Real anti-air groups (rocket trucks,
sparkers) beat or stall it before and after, so ammo does not change the counter relationships. Jets vs the Authority interceptor group
(the closest air-to-air fight) moves from 60% to 55% remaining health: marginal.

Decision: no carrier or weapon changes needed; 5/5/4 rounds and 150/150/200 rearm ticks ship as set.
