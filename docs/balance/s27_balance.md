# Session 27 balance round (Pi cluster measurements)

All cells: `ai match` tiered:3 v tiered:3, size 96, 30 000 ticks, `--income 1`, 32 seeds x 2 start swaps = 64 games, seeds 1-32, run on the
Pi k3s cluster (pwnzor11/k3s-cluster-code, `claude/examples/rts27/`). Cluster results are seed-for-seed identical to x64 (checked:
Directorate v Expedition rust seed 1, both games, same end ticks and health). Target 45-55 % per cross-faction row. "Share" = A wins / decisive games.

## Baseline: s25after (main 43d4388, PR #89)
Full table: owner's folder `balance_runs\s25after\s25after_summary.md`.

| Row | W-L-D | A share |
|---|---|---|
| Authority v Directorate rust | 19-31-14 | 38 % |
| Authority v Left Behind rust | 39-20-5 | 66 % |
| Authority v Expedition rust | 26-34-4 | 43 % |
| Directorate v Left Behind rust | 29-30-5 | 49 % |
| **Directorate v Expedition rust** | **45-10-9** | **82 %** |
| Left Behind v Expedition rust | 37-21-6 | 64 % |
| Expedition v Authority glass | 32-28-4 | 53 % |
| Expedition v Directorate glass | 34-18-12 | 65 % |
| Expedition v Left Behind glass | 24-17-23 | 59 % |

Campaign sweep at the same commit (12 seeds, ash on): all 45 mission-tiers on target (`balance_runs\s27sweep\s27sweep_summary.md`).

## Diagnosis
- **Expedition economy is map-dependent by design**: its Skimmer carries 5 (a Scraper 20), so it lives on shipments (~11-13k of its
  ~30k credits by 10 min on rust_belt). Mean income per side at 3 / 5 / 10 min, rust_belt: Directorate 9 462 / 19 338 / 35 959 v Expedition
  7 524 / 16 501 / 31 505; on glass_coast (ore gone by ~7500) the shipments carry it and it wins.
- **Expedition harvesters die all game.** Unit ledger, 64 games per row, Skimmers made / lost: v Directorate rust 833 / 774 (12 per game),
  v Left Behind rust 893 / 810, v Directorate glass 607 / 456, v Left Behind glass 543 / 429. Opponents' harvesters: 1-3 lost per game
  (Directorate Scraper 156 / 54 v Expedition). Each Skimmer is 500 credits, 220 hp, light armour; the Directorate raiding party
  (harass_min_army 14) and conscript waves kill them on the fields.
- Trace (Directorate v Expedition rust, seed 1 game 1): Directorate waves from tick 1501 (16 units) keep the Expedition at home; it
  spends on naval tech (shipyard, flight deck), first wave at tick 4201; income 37k v 25k at tick 9600; Expedition sells structures and loses at 11948.

## Variants (32 seeds, same seeds as the baseline)
- VA: Skimmer max_health 220 -> 360 (`bal/s27-va`)
- VB: Skimmer harvest_capacity 5 -> 8 (`bal/s27-vb`)
- VD: Left Behind tier-3 army_cap_until_tick 3600 -> 3000 (`bal/s27-vd`, Left Behind rows only)

### Round 1 (W-L-D, A share = A wins / decisive; rows without the Expedition were unchanged in VA/VB, as they must be)
| Row | Baseline | VA Skimmer 360 hp | VB Skimmer capacity 8 |
|---|---|---|---|
| Authority v Expedition rust | 26-34-4 (43 %) | 31-32-1 (49 %) | 40-21-3 (66 %) |
| Directorate v Expedition rust | 45-10-9 (82 %) | 39-13-12 (75 %) | 48-12-4 (80 %) |
| Left Behind v Expedition rust | 37-21-6 (64 %) | 27-34-3 (44 %) | 44-16-4 (73 %) |
| Expedition v Authority glass | 32-28-4 (53 %) | 38-24-2 (61 %) | 22-41-1 (35 %) |
| Expedition v Directorate glass | 34-18-12 (65 %) | 33-19-12 (63 %) | 21-28-15 (43 %) |
| Expedition v Left Behind glass | 24-17-23 (59 %) | 33-17-14 (66 %) | 13-23-28 (36 %) |

VB is worse for the Expedition everywhere (bigger loads, fewer returns? not traced). VA fixes the rust_belt Expedition rows except the Directorate
one and pushes glass_coast up. Round 2 offsets glass with the shipment value and targets the Directorate raiders:
- VE: VA + shipment value 1200 -> 1100 (`bal/s27-ve`)
- VF: VE + Directorate harass_min_army 14 -> 22 (`bal/s27-vf`)

### VD: Left Behind tier-3 army_cap_until_tick 3600 -> 3000 (dropped)
| Row | Baseline | VD |
|---|---|---|
| Authority v Left Behind rust | 39-20-5 (66 %) | 39-23-2 (63 %) |
| Directorate v Left Behind rust | 29-30-5 (49 %) | 22-35-7 (LB 61 %) |
| Left Behind v Expedition rust | 37-21-6 (64 %) | 45-15-4 (75 %) |
| Expedition v Left Behind glass | 24-17-23 (59 %) | 19-29-16 (LB 60 %) |
Barely moves the Authority row and lifts Left Behind against everyone else.

### Round 2
| Row | Baseline | VE (VA + shipments 1100) | VF (VE + Directorate harass 22) |
|---|---|---|---|
| Authority v Directorate rust | 19-31-14 (38 %) | 19-31-14 (38 %) | same as VE |
| Authority v Left Behind rust | 39-20-5 (66 %) | 39-20-5 (66 %) | same |
| Authority v Expedition rust | 26-34-4 (43 %) | 25-36-3 (41 %) | same |
| Directorate v Left Behind rust | 29-30-5 (49 %) | 29-30-5 (49 %) | same |
| Directorate v Expedition rust | 45-10-9 (82 %) | 35-13-16 (73 %) | same |
| Left Behind v Expedition rust | 37-21-6 (64 %) | 31-24-9 (56 %) | same |
| Expedition v Authority glass | 32-28-4 (53 %) | 34-25-5 (58 %) | same |
| Expedition v Directorate glass | 34-18-12 (65 %) | 24-22-18 (52 %) | same |
| Expedition v Left Behind glass | 24-17-23 (59 %) | 27-18-19 (60 %) | same |
VF is identical to VE game for game: the tier-3 table has no `harass` flag, so `harass_min_army` does nothing at tier 3 (the Skimmer losses
come from waves, not raiders). VE: total distance from 50 % over the six Expedition rows 80 -> 58 points.
Round 3 (on VE): Directorate tier-3 early army cap, VG cap 22 until tick 4200, VH cap 26 until tick 6000 (`bal/s27-vg`, `bal/s27-vh`).

### Round 3 (on VE)
| Row | Baseline | VE | VG (Directorate tier-3 cap 22 until 4200) |
|---|---|---|---|
| Authority v Directorate rust | 19-31-14 (38 %) | 19-31-14 (38 %) | 26-26-12 (50 %) |
| Authority v Left Behind rust | 39-20-5 (66 %) | 39-20-5 (66 %) | 39-20-5 (66 %) |
| Authority v Expedition rust | 26-34-4 (43 %) | 25-36-3 (41 %) | 25-36-3 (41 %) |
| Directorate v Left Behind rust | 29-30-5 (49 %) | 29-30-5 (49 %) | 24-36-4 (40 %) |
| Directorate v Expedition rust | 45-10-9 (82 %) | 35-13-16 (73 %) | 34-12-18 (74 %) |
| Left Behind v Expedition rust | 37-21-6 (64 %) | 31-24-9 (56 %) | 31-24-9 (56 %) |
| Expedition v Authority glass | 32-28-4 (53 %) | 34-25-5 (58 %) | 34-25-5 (58 %) |
| Expedition v Directorate glass | 34-18-12 (65 %) | 24-22-18 (52 %) | 31-19-14 (62 %) |
| Expedition v Left Behind glass | 24-17-23 (59 %) | 27-18-19 (60 %) | 27-18-19 (60 %) |
| Sum of distances from 50 % (9 rows) | 109 | 87 | 95 |
VH (cap 26 until 6000) moved nothing in its first rows (Authority v Directorate 18-31-15) and was stopped. The Directorate's edge over the
Expedition on rust_belt is not its early swarm (the cap left that row at 74 %): next step is per-unit traces of that pairing.

## Recommendation (for the owner)
Land VE: Skimmer max_health 220 -> 360 and shipment value 1200 -> 1100. It moves six rows toward 50 % (sum 109 -> 87) and changes nothing
outside the Expedition. Open: Directorate v Expedition rust 73 %, Authority v Left Behind 66 %, Authority v Directorate 38 % (VG fixes it but
costs the Left Behind and glass rows; not recommended alone).
