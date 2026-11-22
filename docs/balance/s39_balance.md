# Session 39 results: Directorate Bores, map rows, AI ladders, Directorate handicap edge, par times

All runs on the home cluster (queen cordoned; Pis + huntsman + prince). Laptop k3s-cluster folder `claude/rts/s39/`: mk_*.sh (variant sets),
b39.sh / v39*.sh (the s38 `var2.sh` pattern; `/work/in/s39/mkvar.py` also appends a missing `[section]`), e39.sh (handicap cells), cmp39.py
(variant v baseline, same chunks, several variant dirs). "Decisive %" = wins / (wins + losses) of the first-named side; size 96, layout 1, 30 000 ticks,
128 games per row unless a cell says otherwise (64 = screen). Baseline `b39` = main's sim (`f7ef0a5`); the s39 key commit `fe9c063` reproduced it game
for game (defaults unchanged). About 13 000 games.

## Harvester losses on Mountains / Ash Plains (M13-37b, helper trace, 8 games per biome)
The maps are symmetric (same reachable ore per start). Directorate Scrapers do die (Rust Belt), but only when the Authority army reaches their field;
on Ash Plains it rarely gets there. Credits lost per zone: on Ash the Authority loses 1.5-2x the Directorate's credits in the middle of the map
(e.g. 38.3k v 24.8k), on Rust Belt the middle is even. The Directorate's conscript + Rocket Truck waves arrive ~2 000 ticks earlier on the mirror_x
(mid-row) maps; the Authority loses its Factory and tier 2, then its harvesters. Harvester losses are the symptom, not the cause.

## Directorate Bores (M13-37a, D-290)
New keys (`[faction.directorate]`, defaults = the old constants): bore_max, bore_army, bore_gap_ticks, bore_dig_short, bore_sites, bore_min_cash.
12 Directorate cross rows, 64 games (mean distance from 50 %, baseline 17.8): no Bores (bore_max 0) 28.0 (the Directorate +15..32 points), 1 Bore 22.3,
first Bore at army 15 and 1 500 spare 24.3, Hatch beside the ore field only 17.4, enemy-side Hatch 30 cells short 17.8. No placement made a Bore worth
its 800: every variant was weaker for the Directorate than buying none. Fallback: no Bores plus a Directorate nerf.

| Variant (with bore_max 0) | MAD 12 Dir rows | Note |
|---|---|---|
| Rocket Truck cost 980 | 22.7 | the cost cliff: Dir v Exp Rust 85 -> 59, Authority rows stay ~20 % |
| Rocket salvo damage 86 | 16.8 | |
| **Rocket salvo damage 84 (128 games)** | **13.6 (baseline 19.2)** | kept |
| Rocket salvo damage 80 | 17.0 | Dir v LB Glass 20 % |
| (no Bore change) Directorate tier-3 attack_ratio_pct 170 / 200 | 16.5 / 18.2 | the helper's lever; does not move the map rows |

Directorate rows, 128 games, before -> after D-290:

| Row | Rust Belt | Glass Coast | Mountains | Ash Plains |
|---|---|---|---|---|
| Auth v Dir | 40.9 -> 45.2 | 48.2 -> 45.0 | 23.5 -> 35.5 | 11.0 -> 13.2 |
| Dir v LB | 26.5 -> 19.7 | 39.8 -> 43.5 | 53.9 -> 49.5 | 77.8 -> 54.5 |
| Dir v Exp | 56.9 -> 51.1 | 20.9 -> 17.9 | 66.3 -> 48.9 | 86.1 -> 75.9 |

All 24 cross rows: mean distance from 50 % 14.7 -> 11.9, rows inside 40-60 % 10 -> 13. Still out: Authority v Directorate Ash (13 %) and Mountains
(36 %), Directorate v Expedition Glass Coast (18 %) and Ash (76 %), Directorate v Left Behind Rust (20 %), plus the unchanged non-Directorate rows
(Authority v LB Rust 72.8, LB v Exp Ash 28.1, Glass 34.4, Authority v Exp Rust 67.2 / Mountains 63.8).

## AI ladders (M4-16; rust_belt mirrors, 128 games)
Baseline (main): Authority 2 v 1 70.9, 3 v 2 52.4, 4 v 3 64.1, 5 v 4 52.2, 5 v 3 57.0, 5 v 1 66.7. Expedition 2 v 1 47.8, 3 v 2 50.4, 4 v 3 40.9 (27 % draws),
5 v 4 55.3 (70 % draws), 5 v 3 65.9, 5 v 1 60.3 (43 % draws).

**Expedition (D-291):** at tiers 4-5, harvester_target 3, attack_threshold 10 and expansion_tick 1200 moved nothing (64 games); respond_to_attacks 0
(as D-287 for the Authority): 4 v 3 94.6 (80 games), 5 v 3 94.0, 5 v 1 96.7 (draws 5-9 %), 5 v 4 58.5 (68 % draws).

**Authority tier 5 (D-292):** target 5 v 1 = 90 %. 64-game screens: harvester_target 3 75.0, attack_threshold 10 77.4 (71.8 on 128), expansion_tick 1200
69.8, h3 + t10 66.7, h3 + t10 + x1200 65.6, **h4 + t14 74.6 -> 72.0 on 128** (5 v 3 58.1, 5 v 4 64.8 on 96). Kept h4 + t14; 90 % is out of reach for the
tier-5 data keys.

## Directorate handicap edge (M13-38, D-293)
Helper traces (tier 5 at +50 %): funds sit at 0 most of the time, not at the storage cap (the "4 989" was the starting cash); supply was never the
limit. The Directorate's production is: one Assembly Bore, and AdaptBuild waits for an army of 30 that its waves never keep. Fix: adapt_rich_cash
(funds) lets a side with handicap > 100 grow factories/barracks to adapt_rich_max (3). Directorate tier 5 v plain tier 5: +50 % 67.8 (s38, 64) -> 79.0 %,
+25 % 53.3 -> 68.9 % (128 games, job e39). Handicap-100 rows are identical to main (Authority v Directorate Mountains 23.5 %, Directorate mirror 47.0 %).

## Par times (D-289)
`par_seconds` in 12 missions from the s37 table (bot tier-3 median x 2.5); none for auth_02, dir_03, dir_04, lb_03 (timer missions).

## Verification
- Campaign sweep on `2a94d97` (job sw39, 45 mission-tiers x 12 seeds): **all 45 meet D-222**. Moves against s38: exp_03 tier 5 9 -> 3 of 12 (at the
  floor of 3), exp_04 tier 3 12 -> 10 (floor 8), auth_01 tier 5 10 of 12.
- The landed data reproduces the screened variant game for game (job v39h: 6 chunks of Authority v Directorate Rust and Directorate v Expedition Glass
  identical to the nbd84 screen).
- Tests: Scripting 438 passed on the cluster (job t39); Sim 1918 (+2 skipped), Game 2209 (+2 skipped), AI 416 (+1 skipped), Net 175, Llm 174, Tools 67,
  0 failed, in the cloud workspace on the final branch (the only failure on the way was the s38 ladder test pinning the Authority tier-5 threshold; updated).
