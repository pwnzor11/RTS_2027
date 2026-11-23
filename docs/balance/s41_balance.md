# Session 41 balance: a finish line, unit-v-unit fixtures, AI unit lists, joint lever sweep (2026-11-22/23)

## 0. The finish line (owner, s41: D-298)
Balance is done when, on fresh seeds (128 games per row, tier 3 v tier 3, size 96, layout 1):
1. all 24 cross rows are inside 35-65 % (decisive wins of the first-named faction);
2. the AI ladder is in order (a higher tier beats a lower one of the same faction);
3. every campaign mission-tier still meets D-222.
After that, balance numbers are frozen; only the owner's play-tests change them.

## 1. Unit-v-unit matchups (M13-43, `Tools ai duels`)
Two equal-cost armies (budget split by weight; each side spends within half a unit of the budget) on a flat 72x48 map, no AI: both attack-move
at each other, short range in front. Score = share of its own army value (cost x health left) A keeps minus B's share, permille, over 4 layouts x both
sides. About 0.2 s per fight in the cloud. A matchup ignores economy, build time, tech cost and the AI's use of a unit, so it answers "is this unit
worth its money in a straight fight", not "who wins a game".

### 1a. Every armed ground unit v every other faction's (512 matchups, 6 000 credits, 4 seeds; first rounding)
Value index = mean score v all cross opponents (permille). Top: Rocket Truck 814, Reactor Crawler 808, Landtrain 728, Howitzer 630, Seismic Mortar 578,
Organ 363, Tank 362, Marine 351, Barrage Lander 342, Bulwark 295. Bottom (support units, expected): Diver -878, Decon Truck -809, Dust Sail -797,
Foam Truck -790, Volunteer -626, Commissar -619.
- The Rocket Truck beats almost everything, including the Authority's anti-tank Rocketeer (-665 for the Rocketeer): the Directorate's open-field edge.
- Hook Runners (the Left Behind's tier-1 core) lose to nearly everything (-293).

### 1b. The AI's own unit lists (M13-42 question)
Each faction's tier-1 list v its grown tier-3 list, weighted as the AI buys (infantry 40 / vehicles 60 % of spend, by list count x cost), 8 000 credits:
| Faction | grown v tier-1 |
|---|---|
| Authority | +454 |
| Directorate | +120 |
| Left Behind | +385 |
| Expedition | +381 (8 000) / -448 (12 000): flips with army size |

So session 40's reading ("tier-2/3 units are worth less than tier-1 spam") is wrong at equal cost. What is wrong is the lists: the Authority's tier-2
vehicles include the Foam Truck (a support unit, -790), its tier-2 infantry is the Marksman (54 v the Rifleman's 244), and its tier-3 list is two
Bulwarks (the Tank's cannon for 100 more) to one Howitzer. Candidate lists (duels, 8 000 credits; v the other factions' grown lists):
| Authority list | v Dir | v LB | v Exp | v own tier 1 |
|---|---|---|---|---|
| current grown | -336 | +424 | -257 | +316 |
| A2: no Foam/Marksman/Bulwark, Howitzer + Tank | -301 | +469 | -79 | +453 |
| A3: tier 2 Mortar + Tank, tier 3 Howitzer x2, Rocketeer for Marksman | -319 | +508 | +17 | +519 |
| A4: artillery only at tiers 2-3 | -287 | +523 | +19 | +680 |
For the Expedition its current grown list was already the best of three tried.

### 1c. Fixtures (data/balance/duels.toml, DuelFixtureTests)
Eight counter checks (e.g. Rocketeer beats Tank, Tank beats Rifleman, Slinger beats Tank, Howitzer beats Buggies) and three list checks (Authority,
Directorate, Left Behind grown lists beat their tier-1 lists), 8 000 credits, 3 seeds, bands with room (min 0-300). A rules change that breaks one
needs a decision.

## 2. AI unit lists as data (M13-42)
New `[faction.<id>]` keys `infantry_bag`, `vehicle_bag`, `infantry_tier2`, `vehicle_tier2`, `vehicle_tier3` (arrays of unit ids; missing = the built-in
list, so default games are unchanged). Every list variant is now data-only on the cluster.

## 3. In-game list screen (job g41a, 64 games per row, v ref41 on the same seeds)
Variants (data only): gA0 Authority grow_bags at tiers 2-5 with the built-in lists; gA3 grow_bags with tier-2 Mortar + Tank, tier-3 Howitzer x2,
Rocketeer for the Marksman; gA4 artillery-only tier-2/3 vehicles; gE Expedition grow_bags at tiers 4-5. The new build replayed main game for game
(Authority v Directorate Rust, chunk 0).
- 12 Authority rows: MAD gA0 10.8, gA3 13.1, gA4 11.3 v 8.5-9.2 for main on the same seeds; single rows move up to 14 points both ways (e.g. gA3 Rust
  Auth v Dir 47.9 -> 34.0, Ash Auth v Exp 49.2 -> 62.7). No list improves the table on its own; gA3 is the sweep's 15th lever (auth_grow).
- Expedition ladder (rust_belt mirrors): main 4 v 3 58-4, 5 v 1 58-3, 5 v 3 59-5; gE 15-9, 33-5, 22-11. Rejected.
- Authority ladder, main: 2 v 1 43-20, 3 v 1 41-23, **3 v 2 24-40 (37.5 %, out of order)**, 4 v 3 24-19, 5 v 1 39-24, 5 v 3 34-23. The grown lists do
  not fix 3 v 2 (gA0 21-41, gA3 21-27, gA4 23-40).

## 4. Authority ladder: tier 3 v tier 2 (jobs l41, l41b, l41c; rust_belt, 128 games)
Cause: since D-288 tier 3 carries army_cap 40 and tier 2 has no cap. Tier-2 army_cap screen:
| tier-2 cap | 2 v 1 | 3 v 2 |
|---|---|---|
| none (main) | 76-35 (68 %) | 37.5 % (64 games) |
| 40 | 89-38 (70 %) | 55-71 (43.7 %) |
| 35 | 85-42 (67 %) | 59-65 (47.6 %) |
| 30 | 79-48 (62 %) | 62-61 (50.4 %) |
| 25 | 64-64 (50 %) | 79-45 (63.7 %) |
| 20 | 32-94 (25 %) | 98-29 (77.2 %) |
Tier 2's strength has a cliff between 25 and 30. l41c (128 games):
| setting | 2 v 1 | 3 v 2 | 4 v 3 |
|---|---|---|---|
| tier-2 cap 28 | 81-46 (63.8 %) | 66-57 (53.7 %) | 76-42 (64.4 %) |
| tier-2 cap 30, tier-3 cap 45 | 79-48 (62.2 %) | 65-60 (52.0 %) | 73-45 (61.9 %) |
| tier-2 cap 30, tier-3 cap 50 | 79-48 (62.2 %) | 67-58 (53.6 %) | 67-51 (56.8 %) |
Candidate: **tier-2 army_cap 28** (touches no tier-3 table row). 3 v 2 is only 3.7 points above even: confirm on fresh seeds with the final run.

(sweep results follow)
