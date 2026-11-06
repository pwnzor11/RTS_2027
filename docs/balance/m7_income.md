# M7 Expedition early-game income and naval strike (S11, M7-14 / M7-10)

Status: every changed number is `(tune)` and **assumed — confirm** (D-145/D-148). Other factions' numbers are untouched (D-113).

## Method
- `Tools ai match --a tiered:2 --b tiered:2 --faction-a expedition --faction-b <authority|directorate|left_behind> --size <64|96|128> --seeds 10 --ticks 12100`
  (10 seeds x 2 start swaps = 20 games per cell; small/medium/large = map size 64/96/128; ocean north+south, Expedition on the south start, D-151).
  Script: `tools/measure/m7_income.sh`; tables: `tools/measure/m7_income_agg.py`. Per-minute income = (credits harvested + shipments paid) at tick 3600 / 6000 / 12000 divided by 3 / 5 / 10.
- Each of the three Expedition cells per map pools 60 Expedition games; the other factions' rows are their 20 games each against the Expedition (so they are measured as opponents, not mirrors).
  A column only averages games where both sides were still alive at that mark (the "reached 10 min" count is in brackets); the late columns are therefore biased toward the longer games, and Left Behind's 10-minute figure rests on 2-12 games.
- "vs mean" compares the Expedition with the mean of the other three rows of the same table. Left Behind is 25-45 % below the other two (known, D-102; it is a sanity check, not a request, §4), which pulls the mean down.
- New `ai match` output (src/Tools/AiCommands.cs): first-refinery tick, tenders sunk, decay step at 10 min, shipment credits at 3/5/10 min.

## Numbers changed
| File | Key | Old | New |
|---|---|---|---|
| data/rules/game.toml `[shipments]` | `interval_ticks` | 1800 | 600 `(tune)` |
| data/rules/game.toml `[shipments]` | `value` | 1500 | 2000 `(tune)` |
| data/rules/game.toml `[shipments]` | `decay_permille` | 40 | 60 `(tune)` |
| src/AI ExpeditionTactics | `TanksBeforeNavy` | 2 | 1 `(tune)` |
| src/AI ExpeditionTactics | `ArmyBeforeShips` | 3 | 2 `(tune)` |
| src/AI ExpeditionTactics | `FleetLow / FleetMid / FleetTop` | 2 / 3 / 4 | 3 / 4 / 5 `(tune)` |
| src/AI ExpeditionTactics | `StrikeMin` | 2 | 1 `(tune)` |
| src/AI ExpeditionTactics | `ReserveMin` (new; reserve from 4 ships, was 3) | 3 | 4 `(tune)` |
| src/AI ExpeditionRecruitFaction | build order | ...hover_yard, pile_house, plotting_room, beacon, quay, shipyard, beacon, flight_deck | ...hover_yard, plotting_room, quay, shipyard, pile_house, beacon, beacon, flight_deck |
Not changed: `floor_permille` (350), `charter_ticks`, `tenders_max`, `local_ash_bonus_permille` (0), Skimmer capacity/speed, start cash. Trial runs (8 medium games vs Directorate): value 1800 / cap 5 gave -10 / -1 / -3 %,
value 2000 / cap 8 gave -4 / +5 / +4 %, value 2000 / cap 5 (landed) -3 / +6 / +2 %, so the Skimmer knob was not needed. Higher value/interval (3000 / 700 / decay 100) overshot (+7 / +10 / +7 % but heavier shipment dependence).
The decay step at 10 min reaches 8-9 (floor) because shipments are 3x as frequent; the payout sits on the 35 % floor (700 per shipment) at 10 min, i.e. the income curve is front-loaded by design here. The owner may prefer a lower decay with a lower value.

## Naval strike (M7-10, S11)
- Quay/Shipyard/Flight Deck no longer wait for 2 Glide Tanks (1), warships are bought from 2 army units, fleet 3/4/5 by tier, and the build order puts Plotting Room, Quay, Shipyard before the second Pile House and Beacons (first Shipyard ~tick 4700, first warship ~5700).
- A group of one ship may strike; with no known enemy building in gun range the group sails to the sea cell nearest the enemy start (`ChooseProbe`, terrain only) and attack-moves there; the next review picks a real building. Retreat stays at 40 % (`RetreatPct`).
- Test: `ExpeditionAiTests.ItSendsANavalStrikeAgainstAnAuthorityCoastByMinuteFifteen` (strike target chosen and an AttackMove to the firing cell by tick 18000, medium ocean map vs a Recruit-tier Authority) and `ShipsRetreatAtFortyPercent`.
- Honest note: vs a tier-2 AI the fleet is mostly sunk or in withdraw by minute 7-9 (withdraw rule: army value < 60 % of enemy); the strike mostly fires when the enemy is weak or late.

## BEFORE (current numbers, 20 games per cell)

### small map (size 64) -- 20 games per opponent cell
| Faction | 3 min /min | 5 min /min | 10 min /min | vs mean 3/5/10 | Shipment share % | Games (reached 10 min) | first refinery tick | tenders sunk /game | decay step at 10 min |
|---|---|---|---|---|---|---|---|---|---|
| Authority | 2664 | 3251 | 3665 | n/a | n/a | 20 (3) | 660 | 0.00 | 0.0 |
| Directorate | 2730 | 3337 | 3510 | n/a | n/a | 20 (14) | 660 | 0.00 | 0.0 |
| Left Behind | 1829 | 1921 | 1695 | n/a | n/a | 20 (8) | 660 | 0.00 | 0.0 |
| Expedition | 1092 | 1462 | 1872 | -55 % / -48 % / -37 % | 46 / 40 / 40 | 60 (25) | 592 | 0.05 | 1.2 |
(mean of the other three: 2407 / 2837 / 2957; spread of the three at 3/5/10: 49 % / 74 % / 116 %)

### medium map (size 96) -- 20 games per opponent cell
| Faction | 3 min /min | 5 min /min | 10 min /min | vs mean 3/5/10 | Shipment share % | Games (reached 10 min) | first refinery tick | tenders sunk /game | decay step at 10 min |
|---|---|---|---|---|---|---|---|---|---|
| Authority | 2692 | 3194 | 3238 | n/a | n/a | 20 (4) | 660 | 0.00 | 0.0 |
| Directorate | 2679 | 3345 | 3135 | n/a | n/a | 20 (17) | 660 | 0.00 | 0.0 |
| Left Behind | 2236 | 2275 | 1850 | n/a | n/a | 20 (12) | 660 | 0.00 | 0.0 |
| Expedition | 819 | 1477 | 1524 | -68 % / -50 % / -44 % | 32 / 40 / 44 | 60 (33) | 662 | 0.03 | 1.7 |
(mean of the other three: 2536 / 2938 / 2741; spread of the three at 3/5/10: 20 % / 47 % / 75 %)

### large map (size 128) -- 20 games per opponent cell
| Faction | 3 min /min | 5 min /min | 10 min /min | vs mean 3/5/10 | Shipment share % | Games (reached 10 min) | first refinery tick | tenders sunk /game | decay step at 10 min |
|---|---|---|---|---|---|---|---|---|---|
| Authority | 2740 | 2966 | 2200 | n/a | n/a | 20 (5) | 660 | 0.00 | 0.0 |
| Directorate | 2616 | 3023 | 2902 | n/a | n/a | 20 (16) | 660 | 0.00 | 0.0 |
| Left Behind | 2040 | 2001 | 1798 | n/a | n/a | 20 (12) | 660 | 0.00 | 0.0 |
| Expedition | 741 | 1161 | 1463 | -70 % / -56 % / -36 % | 26 / 43 / 45 | 60 (33) | 607 | 0.02 | 1.8 |
(mean of the other three: 2465 / 2663 / 2300; spread of the three at 3/5/10: 34 % / 51 % / 61 %)


## AFTER

### small map (size 64) -- 20 games per opponent cell
| Faction | 3 min /min | 5 min /min | 10 min /min | vs mean 3/5/10 | Shipment share % | Games (reached 10 min) | first refinery tick | tenders sunk /game | decay step at 10 min |
|---|---|---|---|---|---|---|---|---|---|
| Authority | 2714 | 3212 | 3400 | n/a | n/a | 20 (14) | 660 | 0.00 | 0.0 |
| Directorate | 2728 | 3263 | 3317 | n/a | n/a | 20 (19) | 660 | 0.00 | 0.0 |
| Left Behind | 1713 | 1718 | 832 | n/a | n/a | 20 (2) | 660 | 0.00 | 0.0 |
| Expedition | 2683 | 3177 | 3076 | +12 % / +16 % / +22 % | 80 / 78 / 73 | 60 (35) | 592 | 0.10 | 9.2 |
(mean of the other three: 2385 / 2731 / 2516; spread of the three at 3/5/10: 59 % / 90 % / 308 %)

### medium map (size 96) -- 20 games per opponent cell
| Faction | 3 min /min | 5 min /min | 10 min /min | vs mean 3/5/10 | Shipment share % | Games (reached 10 min) | first refinery tick | tenders sunk /game | decay step at 10 min |
|---|---|---|---|---|---|---|---|---|---|
| Authority | 2694 | 3210 | 3097 | n/a | n/a | 20 (16) | 660 | 0.00 | 0.0 |
| Directorate | 2680 | 3253 | 3051 | n/a | n/a | 20 (18) | 660 | 0.00 | 0.0 |
| Left Behind | 2238 | 2275 | 1736 | n/a | n/a | 20 (3) | 660 | 0.00 | 0.0 |
| Expedition | 2384 | 3096 | 2837 | -6 % / +6 % / +8 % | 74 / 75 / 74 | 60 (37) | 662 | 0.03 | 9.1 |
(mean of the other three: 2537 / 2912 / 2628; spread of the three at 3/5/10: 20 % / 43 % / 78 %)

### large map (size 128) -- 20 games per opponent cell
| Faction | 3 min /min | 5 min /min | 10 min /min | vs mean 3/5/10 | Shipment share % | Games (reached 10 min) | first refinery tick | tenders sunk /game | decay step at 10 min |
|---|---|---|---|---|---|---|---|---|---|
| Authority | 2734 | 2989 | 2898 | n/a | n/a | 20 (11) | 660 | 0.00 | 0.0 |
| Directorate | 2616 | 3035 | 2804 | n/a | n/a | 20 (18) | 660 | 0.00 | 0.0 |
| Left Behind | 2040 | 1978 | 2139 | n/a | n/a | 20 (5) | 660 | 0.00 | 0.0 |
| Expedition | 2232 | 2962 | 2418 | -9 % / +11 % / -7 % | 71 / 77 / 76 | 60 (34) | 607 | 0.05 | 8.0 |
(mean of the other three: 2463 / 2667 / 2613; spread of the three at 3/5/10: 34 % / 53 % / 35 %)


## 8-game tier-2 matches, medium map (4 seeds x 2 swaps, 36000 ticks), Expedition vs each faction
| Opponent | Before (S10 report) | Wins | Draws | Losses | Avg end tick |
|---|---|---|---|---|---|
| Authority | 0/8 | 5 | 0 | 3 | 16884 |
| Directorate | 0/8 (4 draws) | 4 | 1 | 3 | 26575 |
| Left Behind | 1/8 | 8 | 0 | 0 | 9850 |

The win-rate jump is mostly the income fix (the Expedition had ~45 % of the others' income and then lost the economy race); the combat units were not changed.
