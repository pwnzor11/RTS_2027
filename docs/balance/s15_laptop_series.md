# S15 AI-vs-AI series on the owner's laptop (after D-166 and D-173, before the siege AI)

Run by the owner on Windows with `tools/measure/run_s15_series.ps1` (PR #45) on main 563d888 (session 14 code: D-166 ash 20 /
Conscript 50 HP, D-173 no-rebuild defeat). `ai match --seeds 32 --size 96 --ticks 30000`, tiered AI: 32 seeds x 2 start swaps = 64 games
per row, same seeds as `s14_laptop_baseline.md`, so every difference is the code. Decisive % = A / (A + B), draws left out.
Set A complete; set B complete except Left Behind 5 v Authority 5 and Left Behind 3 v Expedition 3; set C (60 000-tick rows) not yet in.

## Set A — repeat of the session 14 baseline

| Match (A v B) | S14 (A / B / draws) | S15 (A / B / draws) | Decisive % for A, S14 → S15 | Avg end tick S15 |
|---|---|---|---:|---:|
| Directorate 3 v Authority 3 | 24 / 19 / 21 | 23 / 23 / 18 | 56 % → 50 % | 21 724 |
| Directorate 5 v Authority 5 | 7 / 37 / 20 | 10 / 34 / 20 | 16 % → 23 % | 21 633 |
| Authority 5 v Authority 1 | 41 / 18 / 5 | 41 / 18 / 5 | 69 % → 69 % | 17 121 |
| Directorate 5 v Directorate 1 | 17 / 8 / 39 | 22 / 8 / 34 | 68 % → 73 % | 26 001 |
| Left Behind 3 v Directorate 3 | 56 / 6 / 2 | 44 / 7 / 13 | 90 % → 86 % | 16 445 |
| Expedition 3 v Authority 3 | 33 / 26 / 5 | 33 / 26 / 5 | 56 % → 56 % | 15 778 |

## Set B — Q-65 faction map (so far)

| Match (A v B) | A / B / draws | Decisive % for A | Avg end tick |
|---|---|---:|---:|
| Directorate 1 v Authority 1 | 40 / 14 / 10 | 74 % | 18 874 |
| Directorate 1 v Left Behind 1 | 26 / 15 / 23 | 63 % | 19 901 |
| Directorate 3 v Expedition 3 | 29 / 30 / 5 | 49 % | 20 180 |
| Directorate 5 v Expedition 5 | 16 / 40 / 8 | 29 % | 21 352 |
| Directorate 5 v Left Behind 5 | 3 / 44 / 17 | 6 % | 19 079 |
| Left Behind 3 v Authority 3 | 45 / 13 / 6 | 78 % | 14 886 |
| Expedition 5 v Authority 5 | 38 / 20 / 6 | 66 % | 17 521 |

Directorate decisive % by tier: v Authority 74 / 50 / 23 %, v Left Behind 63 / 14 / 6 %, v Expedition – / 49 / 29 % (tiers 1 / 3 / 5).

## Reading (lead, session 15)
- **D-166 and D-173 moved little.** The two rows without a Directorate or Left Behind side reproduce exactly (Authority 5 v 1,
  Expedition 3 v Authority 3), as they should. Directorate rows shifted a few games each; draws fell by 3-5 per row but are still
  18-34 of 64. The siege AI (Q-64, D-174) is measured against these numbers.
- **The Directorate does not scale with tier.** At tier 1 it beats Authority 74 % of decisive games, at tier 3 it is even (50 %), at
  tier 5 it loses (23 %). So the Q-65 problem looks like Directorate tier 4-5 play (AI build/army choices, or late units that don't pay
  off) rather than flat faction strength; a flat Directorate buff would make tiers 1-3 lopsided.
- **Left Behind is strong at mid tiers too:** it beats Authority 45-13 (78 %) and the Directorate 44-7 (86 %) at tier 3, with very large
  armies (often 200-480 units built against 50-150). So Q-65 has two parts: Directorate tier 4-5 play, and Left Behind mid/late strength.
- **Expedition gains with tier** (56 % v Authority at tier 3, 66 % at tier 5); worth watching, not urgent.
- **The tier ladder stays soft** for Authority (tier 1 wins 18 of 64 against tier 5).
