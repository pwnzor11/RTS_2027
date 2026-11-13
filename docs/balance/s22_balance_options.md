# Session 22 · balance diagnosis and options (M13-19 / Q-77 prep, M13-13 options)

## Summary (10 lines)
1. Nothing in `data/` or `src/` is changed by this package; all experiments ran on copies of the rules (`alt_*` folders) or on a scratch build with diagnostics.
2. Authority v Expedition, Rust Belt L1 (s21: 12-51; here 1 win in 12): the Expedition economy is the shipments. With shipments off the row flips to 12-0 for Authority (Expedition income 1.6k at 3 min against 11.1k); with them on, Expedition out-earns Authority by minute 10.
3. Expedition v everyone, Glass Coast L1 (55-7, 43-14, 43-10): the same shipments lever plus Glass Coast scarcity (Q-70 cut the fields to 2406 money); the other factions' ship armour columns (LE-07) are already 70/30 or 90/70, so a lack of anti-ship is not the lead cause.
4. One shipments number cannot centre both rows: `value` 2000 -> 1400 puts Exp v Auth Glass Coast at about 58 % and Auth v Exp Rust Belt at 16-25 % Authority, but takes Exp v Left Behind to 25 % (n=12 per cell, plus or minus 14 points).
5. Authority v Directorate, Rust Belt (s19 24-24, s21 17-36): baseline here is about 30 % Authority. The only code change between s19 and s21 that touches both sides' fighting is D-219 (Aggressive stance engages in sight, commit 7ae2931). PR #70 is NOT the delta: 6e3c15f9 already contains it, contrary to the brief and `s21_after_series.md`.
6. The Directorate Rocket Truck splash (`dir_rocket` splash 1) is the strongest single signal: splash off gives Authority 58 % (7-1-4) against about 30 % at base (12 games, noisy).
7. Left Behind mirror, Mountains L1 (74 % start 1): the map is exactly mirror-symmetric and field placement is not the cause. Start 0 earns about 73 % of start 1's income from minute 1 (ratio 0.86 at tick 1200, 0.71 at tick 2400).
8. One real defect found: `SkirmishStart` flips the start kit by half-turn (rot2), but Mountains is `mirror_x` (Ash Plains `mirror_y`). Flipping X only moves the income ratio 0.73 -> 0.79. The remaining gap is not yet located (candidates in Row 4).
9. Recommended order: (a) shipments value 2000 -> 1400, (b) Rocket Truck splash 1 -> 0 or damage 90 -> 70 with range 8 -> 6 cells, (c) SkirmishStart use the biome symmetry (code), (d) find the LB early-economy asymmetry, then re-run the 17-row series.
10. Section C gives 2 options and a recommendation for the 12 open M13-13 items; items 2, 3, 4, 5, 6, 8, 9, 11, 12, 14 are data only, 1 is a keep, 13 needs a hand check.

Method. Tools build of main at 392baae with throw-away diagnostics (reverted, not committed). `ai match`, tiered:3 both sides, size 96, 30 000 ticks, `--income 1`, layout 1, 6 seeds x 2 swaps = 12 games per cell unless stated (the owner's series uses 64). Cells are therefore noisy: one standard error is about 14 points, so only differences of 30 points or more are read as signals. "wins" below means wins of the named faction; draws are not wins.

## Part A · root causes

### Row 1 · Authority v Expedition, Rust Belt L1
Measured (12 games): Authority 1 win, Expedition 10, 1 draw (8 %); s21 12-51.
- Shipments off (`alt_noship`): Authority 12-0. Expedition income at 3 min is 1600 (harvest only) against Authority 11 134. The Expedition build is effectively starved without shipments, so shipments carry the whole faction.
- Shipments on: Expedition income overtakes by minute 10 (seed 1, shipments 1500: Authority 44.8k, Expedition 49.5k; at 3 min Authority is still ahead, 11.1k v 7.0k). The AI converts that late income into glide-tank masses that Authority AP rounds trade badly against.
- LE-07 (ship armour columns) has no effect on this land row (`alt_le07`: identical 1-10-1).
- Shipments are map-independent (`interval 600, value 2000, decay 6 %, floor 35 %, tenders_max 3`), so they cost nothing in scarcity maps such as Glass Coast.

### Row 2 · Authority v Directorate, Rust Belt L1 and what could have moved it since s19
Measured: base 5-8-3 (16 games), 7-17-8 (32 games), 5-5-2 (12 games): about 30 % Authority, 55 % Directorate. s19 24-24, s21 17-36.
- Loss tables (DIAG): Authority loses its tanks and riflemen in the field (17 of 17 tanks lost, 94 of 94 riflemen), and Directorate builds 190 conscripts plus 9 Rocket Trucks. Directorate conscripts lost 164, so the exchange is not one-sided; the trucks (range 8 cells, 90 damage, splash 1 radius, `hits_allies` false) hit clumped Authority infantry and the single-target AP cannot answer them.
- Rocket Truck splash 1 -> 0 (`alt_trucknosplash`): Authority 7 wins, 1 loss, 4 draws (58 %). Damage 90 -> 70 (`alt_truck70`): 3-8-1 (25 %), i.e. no signal at this n.
- Toggling single AI behaviours (melee acquire reach, move queue, harvest skip, placement lane) gave 18-37 % with no pattern: those are not causes (n=16 each).
- PR #70: `git merge-base --is-ancestor` shows PR #70 inside 6e3c15f9, the commit labelled s19. So s19 -> s21 is NOT the effect of #70. The 46 commits between 6e3c15f9 and 70a4dbb are mostly campaign data, art and tests. The one sim change is D-219 (7ae2931): Aggressive units, the default, now acquire vehicles and infantry in sight and walk back to their post. It lengthens engagements and rewards the side with more cheap bodies and splash (Directorate). The s19 binary could not be rebuilt in the CPU budget, so D-219 is the lead suspect, not a measured cause. To prove it: run `git worktree` at 6e3c15f9 and at 7ae2931^ and 7ae2931 with `ai match` on this row (3 x 64 games).

### Row 3 · Expedition v everyone, Glass Coast L1
Measured Expedition wins at base: v Authority 10-2 (83 %), v Directorate 5-6-1, v Left Behind 6-4-2; s21 55-7, 43-14, 43-10.
- Shipments dominate again. Expedition wins less as `value` falls: v Authority 83 % (2000), 66 % (1500), 58 % (1400), 41 % (1200).
- Glass Coast scarcity: Q-70 already cut the fields to 2406 (12.8 %), which hurts the harvesters (Authority, Directorate, Left Behind) and not the shipments.
- Naval units: the Expedition build is mostly land glide tanks and hovers on this map; Command Ship and naval kills are not the main loss item in the loss tables. Other factions' ship armour columns are now 70/30 (small arms) and 90/70 (AP), so the lack of anti-ship is not the decisive point (and raising it would hit this row the wrong way, see C1).
- Draw-heavy against Left Behind at 1500 (3-3-6): a slow row, not a broken one.

### Row 4 · Left Behind mirror, Mountains L1 (start 1 wins 74 %)
Facts: the map is exactly mirror-x symmetric (field cells compared one by one, kit cells too), so the field layout is not the cause. Start 0 income against start 1 (12 seeds, `ai timeline`): ratio 0.86 at 1200 ticks, 0.71 at 2400, 0.73 at 3600. The gap exists by minute 1, so it is the opening.
- `SkirmishStart` ring scan with `SecondHalf` flips by half-turn only. Mountains is mirror_x, so start 1 gets the rotated kit and not the mirrored one. Forcing an X-only flip raises the ratio 0.73 -> 0.79 (about a quarter of the gap).
- `LeftBehindTactics.FindSite` flip (tie-break handedness) had zero effect: identical results, since the call is made around `r.Cell`.
- Still open: the remaining 0.79 -> 1.0. Candidates in order: `HarvestMission.TieKey` handedness for equal-distance fields and docks; the LB early economy (Mother Rig deploy site and first Gleaner route read the map in row-major order); `StartRoute` scan order. Each is a one-line mirror-safe fix once the field the AI picks in the two corners is printed (add a trace of the first harvest target per start).

## Part B · fixes per row (old -> new)

### Row 1 and Row 3 together (shipments)
Expedition win % in Glass Coast cells and Authority win % in Rust Belt (12 games per cell, A=Authority row 1; E=Expedition rows 3):

| Setting | R1 Auth v Exp (Auth win) | R3 Exp v Auth | R3 Exp v Dir | R3 Exp v LB |
|---|---|---|---|---|
| base value 2000 | 8 % (1/12) | 83 % | 41 % | 50 % |
| 1500 | 25 % | 66 % | 16 % | 25 % |
| 1400 | 16 % and 25 % (two seed sets) | 58 % | 33 % | 25 % |
| 1200 | 41 % | 41 % | 33 % | 16 % |
| shipments off | 100 % | 0 % | not run | not run |

| # | Fix | Player-facing effect | Risk to other rows | Verdict |
|---|---|---|---|---|
| 1 | game.toml `[shipments] value` 2000 -> 1400 | Expedition shipments pay 30 % less; slower early expansion | lowers Exp v Dir / LB toward 25-33 % (they are 41 / 50 % now); Expedition mirrors unchanged | **recommended first step** |
| 2 | `value` 2000 -> 1400 and `floor_permille` 350 -> 450 | late shipments hold up better, early are cheaper | small; reduces the end-game drop-off | try if option 1 undershoots Exp v LB |
| 3 | Rust-Belt-only: `decay_permille` 60 -> 90 | shipments fade faster on land maps only if the data supports a per-biome value (it does not today: code) | new code | not recommended |

### Row 2
| # | Fix | Player-facing effect | Risk | Verdict |
|---|---|---|---|---|
| 1 | weapons `dir_rocket_salvo` damage 90 -> 70 (alone) | weaker salvo | measured 25 %, no help at this n | no |
| 2 | warheads `dir_rocket` splash 1 -> 0 | trucks no longer kill clumped infantry in groups | Directorate loses its answer to Authority infantry; check Dir v LB and Dir mirror | **recommended** if the owner accepts less area damage |
| 3 | `dir_rocket_salvo` range 2048 -> 1536 (8 -> 6 cells), cost 800 -> 900 | truck out-ranges tier-1 defences by less; also answers M13-13 item 5 | mild; Dir v Exp may drop a little | alternative, pairs with item 5 |

### Row 3 (beyond shipments)
| # | Fix | Effect | Risk | Verdict |
|---|---|---|---|---|
| 1 | shipments 1400 (row 1 option 1) | Expedition 83 % -> 58 % v Authority | see above | recommended |
| 2 | Glass Coast `MoneyPermille` stays; add 2 more field cells per half (layout 1 hashes are pinned: layout 2 only) | not available for layout 1 | pinned hashes | no |
| 3 | `exp_command_ship` cost +10 % | slower Expedition naval | untested; Command Ship was not the lead item | no |

### Row 4
| # | Fix | Effect | Risk | Verdict |
|---|---|---|---|---|
| 1 | code: `SkirmishStart` takes the biome symmetry (rot2 / mirror_x / mirror_y) | start 1 gets the mirrored kit on Mountains and Ash Plains | changes pinned start hashes of those two biomes only; layout-1 map hashes are unchanged | **recommended** (closes about a quarter of the gap) |
| 2 | trace the first harvest/dock target per corner, then make `HarvestMission.TieKey` and `StartRoute` mirror-safe | would close the rest | code; hashes change for LB games | needed after 1 |
| 3 | data: LB opening +/- 100 cash on start 0 | masks the bias, not the cause | none | no |

## Part C · open M13-13 items (current -> options -> recommendation)
Numbers: ranges in leptons (256 per cell); speeds in leptons per tick; none of these were measured in series, they are values for the owner to pick.

1. **Non-Expedition HE / flame / area vs ship armour.** Now 100/100 (exp naval HE is 100/100). A: keep. B: HE/area 80/60, flame (`dir_ash`, `lb_flame`) 50/30. **Keep (A)**: Expedition already dominates Glass Coast, cutting anti-ship would worsen it. Data only.
2. **Bulwark = Tank.** Now 400 HP, 800 cost, speed 20, `auth_tank_cannon` (60 damage, 5 cells) as the Tank. A: HP 400 -> 600, speed 20 -> 16, cost 800 -> 1000. B: HP 500, cost 900, speed 18. **A** if Authority is fixed elsewhere; use B while Authority is weak (Row 1). Data only.
3. **Howitzer weaker than the tier-2 mortar.** Now damage 20, range 2560, cost 800, HP 400 (mortar 28, range 2304). A: damage 20 -> 32. B: damage 20 -> 28 and range 2560 -> 2816. **A**. Data only.
4. **Aircraft 0.62 cells/s.** The Dirigible is speed 8 (Rotor 30, Jet 60). A: 8 -> 16 (1.25 cells/s). B: 8 -> 22 (1.7). **A**. Data only.
5. **Tier-1 defences outranged.** Pillbox range 768, turret 1280, Rocket Truck 2048. A: pillbox 768 -> 1024, turret 1280 -> 1536, truck unchanged. B: truck 2048 -> 1536 and damage 90 -> 75. **B** (also helps Row 2). A Directorate tier-1 anti-vehicle emplacement is new data plus **new art**; not recommended now.
6. **Reactor Crawler v Land-Train.** Now range 1024, damage 40, 900 HP, cost 1500, speed 10. A: range 1280, damage 60. B: cost 1500 -> 1100. **A**. Data only.
8. **Repair auras 1 %/10 ticks under fire.** `aura_repair_permille` 10 on LB Mender and Expedition repair hover. A: 10 -> 5. B: flat heal cap (needs a new field: **new code**). **A**. Data only; check whether auras stack.
9. **LB mobile anti-air.** None today (Flak Net structure, 18 damage, 5 cells). A: `lb_harpoon_gun` `targets` -> "both" (already used by `dir_rocket_salvo`). B: Flak Net range 1280 -> 1536 (structure only, no mobile). C: flak wagon (**new unit and art**). **A**. Data only.
11. **Refinery Rig dominated by the Yard.** Rig 900 cost, storage 600, mobile; Yard 600, fixed. A: Rig 900 -> 700. B: free Gleaner with the Rig (**new code**). **A**. Re-run the Left Behind mirror afterwards.
12. **Tutorial cash trap.** Player starts with 3000 in `tut_01/mission.toml`. A: 3000 -> 4500. B: grant cash in `mission.lua` at the harvest step (script change). **A**. Data only.
13. **Recruit AI wiped two openings at 4-5 min.** Scripted play, needs a human hand check before any number; if confirmed, A: lower the early `army_cap` of the AI in `difficulty.toml`; B: raise `attack_threshold` for the first minutes. No recommendation until hand-checked.
14. **Salvo Array.** Now 7200 charge ticks (6 min), cost 2500, one flat hit (TODO M7-12). The other superweapons charge in 3000-4800 ticks. A: 7200 -> 4800 (as the Second Drift). B: 7200 -> 5400 plus 12 missiles over 6 s (**new code**). **A** now, B when M7-12 lands.

## What I could not do
- Reproduce the s19 numbers on the exported 816eaf2/6e3c15f9 source; the build did not run in the CPU budget. The D-219 explanation for Row 2 is a suspect, not a result.
- Find the last part of the Left Behind mirror gap.
- Run the owner's 64-game series; all cells are 12-32 games.
