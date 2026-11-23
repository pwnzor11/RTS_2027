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

## 5. Joint lever sweep (job w41; 64 variants x 24 rows x 16 games = 24 576 games, seeds 1-8)
15 levers in a balanced design (14 at three levels, max pairwise correlation 0.14; auth_grow on/off): Authority Tank health 432/488, Rifleman cost
92/108, Rocketeer damage 92/108; Directorate Rocket salvo 81/87, Conscript cost 74/86, Reactor Crawler cost 1380/1620; Left Behind Scavenger cost
50/58, Hook Runner cost 110/130, Buggy health 110/130, Slinger damage 64/76; Expedition Marine cost 141/159, Glide Tank health 357/403, Glide cannon
damage 61/69; ashfall interval 38/22; Authority grown lists (gA3). Fit: per-row binomial logistic model, ridge on the levers (tools: laptop
k3s-cluster folder claude/rts/s41: mksweep.py, tally41.py, fit41.py).

Findings:
1. **Seeds 1-8 are not the 128-game table.** With the same rules, rows on seeds 1-8 differ from the 128-game reference by 12.7 points on average
   (e.g. Directorate v Expedition Rust 14 % v 58 %): each seed is a different generated map, and single maps tilt a row far more than any lever
   step here. Running every variant on the same 8 seeds was a design flaw: the fit's intercepts belong to those 8 maps. The slopes are still
   usable, so rows are anchored at their 128-game value (fit41.py --anchor).
2. **Levers are weak.** One step (4-8 %) moves a row 1.5-4 points on average (mean |b| 0.06-0.17 logit, se ~0.10). The strongest: Rocket salvo
   (0.17), Conscript cost (0.15), Glide Tank health (0.13), Rocketeer damage and Glide cannon (0.12), Authority lists (0.11, se 0.16).
3. **Ash Plains Authority v Directorate cannot be fixed with unit levers.** The Directorate needs help against the Left Behind and Expedition on
   three maps but already beats the Authority on Ash (33 %); every Directorate lever moves both. Solved packages keep sacrificing that row
   (predicted 15-19 %). It needs an Ash-only lever (map).
4. Solved package (steps limited to the tested range, |step| < 0.3 left unchanged, Authority lists left off: weak evidence): Tank health 445,
   Rifleman 106, salvo 86, Conscript 76, Reactor Crawler 1620, Scavenger 50, Hook Runner 130, Buggy health 130, Slinger 73, Marine 159, Glide Tank
   health 389, ashfall interval 38. Predicted (anchored): 23 of 24 rows inside 35-65 %, MAD 9.6 -> 8.9, Ash Auth v Dir the exception.

Confirmation (job f41, fresh seeds 65-128, never used by any screen): main and fin41 (package + Authority tier-2 army_cap 28) on all 24 rows, plus
every faction's ladder for fin41. Ash map screen on top of fin41 (job a41, 64 games, seeds 65-96): contested_fields 1, decor prop_permille 32,
rough_permille 60, extra_fields 3.

### 5a. Confirmation on fresh seeds 65-128 (job f41, 128 games per row)
**fin41 is rejected.** 24 rows: Authority v Directorate Rust 35.6, Glass 39.2, Mountains 9.7, Ash 13.9; Authority v Left Behind Mountains 23.6, Ash 24.2;
Directorate v Expedition Rust 85.2; Directorate v Left Behind Rust 22.5. The anchored model overstated what the package does.
Ladders under fin41 (Rust mirrors): Authority 2 v 1 54.3, 3 v 2 59.2, 4 v 3 53.7, 5 v 1 62.4, 5 v 3 69.2; Directorate 3 v 2 85.0, 5 v 1 69.7; Left Behind 3 v 2 81.1,
5 v 1 93.5; Expedition 3 v 2 44.0 (out of order), 5 v 1 95.2.

### 5b. The map pool is the measurement problem
Main on seeds 1-64 (ref41) v seeds 65-128 (f41), same build:
| row | s1-64 | s65-128 | 256 games |
|---|---|---|---|
| Rust Directorate v Expedition | 57.8 | 79.5 | 67.9 |
| Rust Directorate v Left Behind | 39.3 | 21.2 | 30.6 |
| Mountains Authority v Directorate | 43.0 | 26.6 | 34.8 |
| Rust Left Behind v Expedition | 47.5 | 61.7 | 54.5 |
| Glass Directorate v Expedition | 34.0 | 21.7 | 27.3 |
| Ash Authority v Directorate | 33.0 | 21.7 | 27.3 |
Mean |difference| over 24 rows 7.6 points (binomial noise alone ~5.7); six rows differ by 2-3 standard errors. Each seed is a different generated map and
maps tilt particular pairings, so 128 games (64 maps) cannot place a row reliably inside a 30-point band. On 256 games main is **MAD 11.4, 15 rows in
35-65 %** (s40's 9.7 was seeds 1-64 only). Pooled over the four biomes every pairing is inside 40-60 %: Authority v Directorate 40.5, v Expedition 59.5,
v Left Behind 54.5; Directorate v Expedition 53.0, v Left Behind 42.2; Left Behind v Expedition 41.4. The remaining imbalance is pairing x map, which
unit levers cannot target (§5 finding 3) and the four Ash map variants did not fix:

### 5c. Ash map screen on top of fin41 (job a41, seeds 65-96, 64 games)
Authority v Directorate Ash: fin41 10 % -> contested_fields 1: 10, prop_permille 32: 14, rough_permille 60: 17, extra_fields 3: 5. None works.

### 5d. Outcome
- Kept: the duel harness and fixtures, the unit-list keys, the Authority tier-2 army_cap 28 (D-304). Confirmed alone on fresh seeds 65-128 (job c41,
  128 games): 2 v 1 57.0 (main 69.5), 3 v 2 51.6 (main 40.7), 4 v 3 60.5 (main 60.5). Main's Expedition 3 v 2 is 49.2 % (a ladder item for later).
- Not kept: any unit number change, any Ash map change, the Authority grown lists.
- Open for the owner (Q-87): how to define "finished" given the map noise.
