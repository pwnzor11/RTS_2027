# Session 44 balance: the end-of-mechanics-wave re-measure (D-317, gate D-305)

Gate (D-305): every pairing pooled over the four biomes inside 40-60 %, every row inside 25-75 %. All rows tier 3 v tier 3, size 96, layout 1,
30 000 ticks, seeds 1-256 (512 games per row, both sides), `Tools ai match`; win % = wins / (wins + losses), draws left out (tally41.py).

## Builds measured
- **f44** = integ-s44 at 5cd0c80 (every S44 merge, Expedition AI naval plan on at tier 3): all 24 rows.
- **g44** = integ-s44 at d01a89c (the same, naval plan off, D-323): the 12 Expedition rows. The naval keys touch only the Expedition AI, so the
  12 other rows are f44's.
- Reference: **bal43** (session 43, integ-s43 + the held D-311 items 5-11, commit 328a4d4), the last full table.

## f44 (naval plan on)
| row | s43 bal43 | s44 f44 |
|---|---|---|
| ash_plains authority v directorate | 21.5 | 16.5 (OUT) |
| ash_plains authority v expedition | 44.1 | 71.3 |
| ash_plains authority v left_behind | 36.2 | 34.6 |
| ash_plains directorate v expedition | 55.1 | 63.5 |
| ash_plains directorate v left_behind | 47.5 | 52.7 |
| ash_plains left_behind v expedition | 31.3 | 68.5 |
| glass_coast authority v directorate | 51.1 | 50.0 |
| glass_coast authority v expedition | 55.7 | 67.5 |
| glass_coast authority v left_behind | 60.1 | 51.8 |
| glass_coast directorate v expedition | 28.0 | 35.6 |
| glass_coast directorate v left_behind | 46.5 | 47.7 |
| glass_coast left_behind v expedition | 44.3 | 69.6 |
| mountains authority v directorate | 28.1 | 29.2 |
| mountains authority v expedition | 55.1 | 71.4 |
| mountains authority v left_behind | 46.8 | 46.0 |
| mountains directorate v expedition | 47.7 | 56.8 |
| mountains directorate v left_behind | 52.1 | 53.5 |
| mountains left_behind v expedition | 48.0 | 68.6 |
| rust_belt authority v directorate | 45.1 | 44.4 |
| rust_belt authority v expedition | 73.0 | 89.3 (OUT) |
| rust_belt authority v left_behind | 72.6 | 66.6 |
| rust_belt directorate v expedition | 71.7 | 87.1 (OUT) |
| rust_belt directorate v left_behind | 29.7 | 27.8 |
| rust_belt left_behind v expedition | 54.8 | 84.8 (OUT) |

Pooled (f44): Authority v Directorate 33.6 (OUT), Authority v Expedition 75.0 (OUT), Authority v Left Behind 50.2, Directorate v Expedition
60.6 (OUT), Directorate v Left Behind 44.8, Left Behind v Expedition 73.0 (OUT). **Gate: failed.** Rows without the Expedition moved little
(largest: glass Authority v Left Behind -8.3, rust -6.0, ash Authority v Directorate -5.0); every Expedition row lost 8-37 points.

## Finding the Expedition drop (128-game probes, rust_belt and glass_coast Expedition rows)
- Not the cause: M-10 Provision Store stock (`supply_unstocked = 10`: unchanged), M-16 beach-only deploy (build without M-16: unchanged),
  A-03 AI repair (build without A-03: unchanged), A-02 focus fire at tier 3 (off: unchanged).
- Bisect over the integ-s44 merges (rust Authority v Expedition / Left Behind v Expedition):
  bal-s43 merge 68 / 48; + input, text, weapons 68 / 49; + campaign, expedition 68 / 49; **+ diver, Left Behind upgrades 73 / 75**; + mines 73 / 75;
  **+ AI keys, rule fixes 81 / 73**; + tether, M-16 fix, drydock 81 / 74; **+ weapon UI, game errors, AI naval use 90 / 83**.
- So three steps: (1) the Left Behind upgrades (M-07..M-11; Scrap Plate turns Light vehicles, Rotors and Kites into Heavy armour, the likely
  part); (2) the A-02/rule-fix merge for the Authority (most likely M-22: Flak no longer hurts Authority aircraft; not isolated); (3) the
  Expedition AI naval plan (Drydock, heavy ships, Minelayer at tier 3), ~9 points in every row.
- Step 3 is fixed (D-323: naval plan off). Steps 1-2 are owner balance calls (next session): the values are D-322 "assumed — confirm".

## g44 (naval plan off) — the final table
| row | s43 bal43 | f44 (naval on) | final (naval off) |
|---|---|---|---|
| ash plains authority v directorate | 21.5 | 16.5 | 16.5 (OUT) |
| ash plains authority v expedition | 44.1 | 71.3 | 59.4 |
| ash plains authority v left behind | 36.2 | 34.6 | 34.6 |
| ash plains directorate v expedition | 55.1 | 63.5 | 61.4 |
| ash plains directorate v left behind | 47.5 | 52.7 | 52.7 |
| ash plains left behind v expedition | 31.3 | 68.5 | 56.2 |
| glass coast authority v directorate | 51.1 | 50.0 | 50.0 |
| glass coast authority v expedition | 55.7 | 67.5 | 66.5 |
| glass coast authority v left behind | 60.1 | 51.8 | 51.8 |
| glass coast directorate v expedition | 28.0 | 35.6 | 35.6 |
| glass coast directorate v left behind | 46.5 | 47.7 | 47.7 |
| glass coast left behind v expedition | 44.3 | 69.6 | 67.8 |
| mountains authority v directorate | 28.1 | 29.2 | 29.2 |
| mountains authority v expedition | 55.1 | 71.4 | 69.9 |
| mountains authority v left behind | 46.8 | 46.0 | 46.0 |
| mountains directorate v expedition | 47.7 | 56.8 | 48.6 |
| mountains directorate v left behind | 52.1 | 53.5 | 53.5 |
| mountains left behind v expedition | 48.0 | 68.6 | 65.7 |
| rust belt authority v directorate | 45.1 | 44.4 | 44.4 |
| rust belt authority v expedition | 73.0 | 89.3 | 82.9 (OUT) |
| rust belt authority v left behind | 72.6 | 66.6 | 66.6 |
| rust belt directorate v expedition | 71.7 | 87.1 | 81.4 (OUT) |
| rust belt directorate v left behind | 29.7 | 27.8 | 27.8 |
| rust belt left behind v expedition | 54.8 | 84.8 | 75.6 (OUT) |

Pooled (final, wins over all four biomes): authority v directorate 33.6 (OUT), authority v expedition 69.7 (OUT), authority v left behind 50.2,
directorate v expedition 56.1, directorate v left behind 44.8, left behind v expedition 66.3 (OUT). Rows outside 25-75: 4.
**Gate D-305: not met.** Turning the naval plan off won back 0-12 points per Expedition row (rust 6-9, ash 2-15, mountains 1-8, glass 0-2).

## Campaign sweep (sw44, D-222; 45 cases x 12 runs, f44 build)
All 45 cases meet their targets (tier 1 >= 10/12, tier 3 >= 8/12, tier 5 >= 3/12). Changed v s42: auth_01 t5 10 -> 9, auth_02 t5 9 -> 7, dir_01 t5
6 -> 9, dir_02 t5 5 -> 6, exp_03 t5 3 -> 10.
