# Session 23 · Directorate too strong after D-229 (s23after), diagnosis and probes

Laptop s23after (main 40cb4df, 64 games/row): Authority v Directorate 16-37, Directorate v Expedition 55-8, Directorate v Left Behind 31-23
(rust_belt L1, tier 3). Cloud cells below: 16 games, tier 3, size 96, rust_belt L1, --income 1, on integ-s23d (with the Grapeshot); about
+-14 points of noise per cell. No rule was changed; all probes were local and reverted.

## What wins (ledger, made / lost)
- The Directorate's core in every pair: a Conscript swarm (1 466-2 004 made, the largest credit line) plus Rocket Trucks (122-158 made,
  ~70 % lost). The truck's splash multiplies against dense infantry.
- The Authority loses 91-97 % of its tanks, rocketeers, howitzers and marksmen; in the traced loss its economy stalled on one Scraper from
  about t4500 while the Directorate's army value ran ~2:1 ahead.
- The Expedition makes 378 Supply Tenders (1 500 cr, 99 % "lost": half its credits). The ledger counts tender/rig/Command Ship transforms
  as losses, so part of this is bookkeeping; still the Expedition has no answer to Conscripts plus trucks (Marines 96 % lost, Grapeshots 81 %).
- Directorate v Left Behind turns on harvester count and the Left Behind swarm timing (D-230 cap), not on the truck.

## Probes (A-B-draws)
| Probe | Auth v Dir | Dir v Exp | Dir v LB |
|---|---|---|---|
| base | 3-9-4 | 10-5-1 | 7-5-4 |
| a: truck damage 90 -> 75 | 6-7-3 | 10-4-2 | 7-7-2 |
| b: truck cost 800 -> 950 | 7-8-1 | 6-6-4 | 6-4-6 |
| c: truck rate_of_fire 60 -> 75 | 4-9-3 | 9-3-4 | 1-11-4 |
| d: truck warhead vs Light 75 -> 60, Heavy 100 -> 70 | 5-8-3 | 10-4-2 | 6-8-2 |
| e: Conscript cost 80 -> 100 | 4-11-1 | 11-4-1 | 9-5-2 |
| f: a + b | 6-7-3 | 2-10-4 | 2-11-3 |

## Options
1. **Truck cost 800 -> 950** (b): the only single lever that moves both Auth v Dir and Dir v Exp; Dir v LB stays within noise. Recommended.
2. Truck damage 90 -> 75 (a): helps Auth v Dir only.
3. Leave the truck; treat the Expedition's tender spending and the Authority's single-Scraper stall as AI problems (separate package).
Never apply a and b together (f overshoots).
