# Session 40 balance: per-biome ore, two economy bugs, tier units (2026-11-22/23)

All rows: tier 3 v tier 3, size 96, layout 1, 30 000 ticks, percent = decisive wins of the first-named faction; "d" = draw share. Screens: 64 games
(seeds 1-32, chunks 0-3) v the baseline on the same seeds; decisions: 128 games (seeds 1-64). Cluster scripts: laptop `k3s-cluster/claude/rts/s40/`,
outputs `/work/out/<job>` (b40, b41, v40a, v41a-h, fin40, ref41). About 9 000 cluster games plus ~1 000 in the cloud.

## 1. Code changes first (they move every row)
- **M13-39 (D-295):** in real games the Directorate AI sold Reactor Vents while it still had harvesters (15 of 24 probe games) and 5 games sat in
  low power 3 000+ ticks: EconomyRescue.SellOne protected only the last power plant. A sale may no longer leave power made below power used.
- **M13-40 (D-296):** an idle harvester parked on its refinery's dock cell blocked the returner holding the dock claim for the rest of the game (all
  factions). It now steps off. Remaining freeze causes: 1-wide corridors between AI buildings (head-on jams) and late ore exhaustion on 96 maps.
- **grow_bags key (D-297):** since D-287/D-291 the Authority (tiers 2-5) and the Expedition (tiers 4-5) built tier-1 units only (ledgers: no Marksman,
  Mortar, Bulwark or Howitzer), because only respond_to_attacks grew the bags. New key `grow_bags`; set for the Authority it made it weaker (Rust v LB
  72 -> 66, v Expedition 75 -> 69; ladder 5 v 1 76 -> 68 % over 64 games), so it is not set anywhere: behaviour unchanged.

Baseline after the two fixes (b41 = 4390655, 128 games): MAD 14.6 (main e162498 / s39: 11.9); the fixes moved several rows (Mountains Auth v Dir
35 -> 29, Rust LB v Exp 53 -> 72, Mountains Dir v Exp 49 -> 63).

## 2. Screens (64 games, d = change v baseline on the same seeds)
| Tag | Change | Result |
|---|---|---|
| e1 / e2 / e3 | shipments floor 200 / decay 100 / value 1000 | Glass rows barely move; no use |
| h2 | no ashfall damage | 24-row MAD 12.8 -> 11.8; small |
| a1 | Ash 3 fields per start, 2 extra (was 4, 4) | Auth v Dir +17, Dir v Exp -13, LB v Exp +12; 6-row MAD 17.3 -> 8.6 |
| a3 / a4 / a5 | Ash 3 + 0 extra / a1 + density_min 3 / 3 + 1 | 9.4 / 15.4 / 11.8 |
| a2, m1 | rough 90 on Ash / 100 on Mountains | worse |
| m2 | Mountains 2 fields per start (was 3) | Auth v Dir 25 -> 50; MAD 8.5 -> 3.2 |
| r1 | Rust road_grid 0 | Dir v LB +12 (old base); r4 (r2 + no roads) worse than r2 |
| r2 / r3 / r6 | Rust 4 fields / density_min 5 / 5 fields | 8.8 / 17.7 / 21.2 (from 15.1) |
| g1, g2 | Glass density_max 8 / no shallows | worse |
| g5 / g6 / g7 / g8 | Glass 3 fields / density_min 5 / both / density 6 + 1 extra | 13.5 / 10.9 / 9.3 / 10.7 (from 13.0); g7 Dir v Exp 21 -> 41 |
| gb | Authority tier-3 grow_bags | 12-row MAD 11.3 -> 11.4; Authority weaker on Rust |
| x1 / x2 | decay 100 + Glide Tank hp x1.15 / Skimmer hp x1.10 | Expedition stronger / weaker; no gain |
| k1 / k5 | Grapeshot damage 11 -> 8 / 10 | LB v Exp +14..29 but Auth v Exp Rust 74; no gain |
| k2 | Harpoon damage 55 -> 63 | no gain |
| k3 / k4 | Slinger damage 70 -> 80 / 75 | k3: LB v Exp +2..19, Glass Auth v LB 69 -> 59, but Ash Auth v LB 41 -> 27; k4 erratic |

Biome keys only change their own biome's maps, so per-biome screens compose exactly (seed for seed identical): p1 reproduced r2/m2/a1/g6 rows.

## 3. Decision (D-294): a1 + m2 + r2 + g7, 128 games
| Row | Rust Belt | Glass Coast | Mountains | Ash Plains |
|---|---|---|---|---|
| Authority v Directorate | 51.5 (49.1) | 45.8 (48.1) | 43.0 (28.8) | 33.0 (17.0) |
| Authority v Expedition | 68.9 (74.4) | 60.0 (44.3) | 53.8 (60.9) | 49.6 (44.6) |
| Authority v Left Behind | 68.7 (68.7) | 69.0 (60.6) | 43.7 (44.4) | 34.7 (36.7) |
| Directorate v Expedition | 57.8 (54.3) | 34.0 (13.5) | 51.7 (63.1) | 57.1 (72.0) |
| Directorate v Left Behind | 39.3 (26.8) | 48.3 (42.3) | 41.4 (37.0) | 41.0 (59.6) |
| Left Behind v Expedition | 47.5 (72.2) | 32.5 (32.0) | 43.1 (47.0) | 29.5 (24.4) |

(b41 in brackets.) **MAD 14.6 -> 9.7; rows inside 40-60: 9 -> 15; all rows now 29.5-69 % (was 13.5-74.4).** Screen winners regress on fresh seeds
(chunks 0-3 MAD 8.1, chunks 4-7 11.7).

## 4. What is left
- Rows outside 35-65: Rust Auth v Exp 68.9 and Auth v LB 68.7, Glass Auth v LB 69.0, Dir v Exp 34.0, LB v Exp 32.5, Ash Auth v Dir 33.0, Auth v LB
  34.7, LB v Exp 29.5. The remaining spread is pairwise (Left Behind v Expedition favours the Expedition on 3 maps; the Authority is strong on Rust/Glass,
  weak on Ash), not a single faction or map.
- Unit tweaks at this level (Slinger, Grapeshot, Harpoon, Glide Tank) move single rows by 10-25 points between seed sets; a next round needs 128-game
  screens or a matchup-level study (Left Behind v Expedition unit mix) rather than more 64-game knob turns.
- Authority tier 5 v tier 1: 76 % on 64 cloud games (target 90); tier-2/3 units do not help.
