# Session 37 results: campaign sweep, par times, LAN soak, determinism, balance baseline

All runs on the home cluster (laptop k3s-cluster folder `claude/rts/s37/`: build.sh, base.sh, sweep.sh, soak.sh, det.sh, mir.sh, var.sh,
mkvar.py, sum.py). Build: main `d3dd95d` (session 36 landed) unless a section says otherwise.

## Campaign sweep (job sw37)
45 mission-tiers x 12 seeds (seed = 1000 + k x 7919, k = 1..12), bot walk-throughs. **All 45 meet D-222** (tier 1 >= 10/12, tier 3 >= 8/12,
tier 5 >= 3/12). Nothing since session 21 has broken a mission.

## Suggested par times (Q-73, for the owner; nothing lands)
The bot plays a scripted, near-perfect walk, so its times are far below a person's (the owner's auth_01 best is 8:35 against the bot's 3.2 min).
The suggestion is the bot's tier-3 median win time x 2.5, rounded up to a whole minute. "Timed" means all 12 tier-3 wins came on the same tick, so
the mission most likely ends on a timer; a par time makes no sense there (medal = Completed / No losses only).

| Mission | t1 | t3 | t5 | Bot tier-3 median win | Timed? | Suggested par |
|---|---|---|---|---|---|---|
| auth_01 | 12/12 | 12/12 | 6/12 | 3.2 min | no | 8 min |
| auth_02 | 12/12 | 12/12 | 9/12 | 4.0 min | yes | (4:00 timer) |
| auth_03 | 12/12 | 12/12 | 12/12 | 3.3 min | no | 9 min |
| auth_04 | - | 12/12 | 6/12 | 3.6 min | no | 9 min |
| dir_01 | - | 12/12 | 6/12 | 1.9 min | no | 5 min |
| dir_02 | 12/12 | 12/12 | 7/12 | 6.6 min | no | 17 min |
| dir_03 | 12/12 | 12/12 | 9/12 | 7.7 min | yes | (7:42 timer) |
| dir_04 | 12/12 | 12/12 | 5/12 | 2.7 min | yes | (2:40 timer) |
| exp_01 | - | 12/12 | 12/12 | 4.3 min | no | 11 min |
| exp_02 | 12/12 | 12/12 | 12/12 | 6.4 min | no | 17 min |
| exp_03 | 12/12 | 12/12 | 9/12 | 5.9 min | no | 15 min |
| exp_04 | 12/12 | 12/12 | 11/12 | 10.8 min | no | 28 min |
| lb_01 | 12/12 | 12/12 | 6/12 | 2.5 min | no | 7 min |
| lb_02 | 12/12 | 12/12 | 7/12 | 5.7 min | no | 15 min |
| lb_03 | 12/12 | 12/12 | 12/12 | 4.1 min | yes | (4:03 timer) |
| lb_04 | 12/12 | 12/12 | 12/12 | 8.3 min | no | 21 min |

## LAN lockstep soak (job soak37)
`Tools lan soak`, 54 configurations x 2 seeds = 108 runs of 12 000 ticks: 2/3/4 players, AI tier 1/3/5, packet loss 0/5/15 % with jitter
40/120/250 ms on 30 ms latency, input delay 2 and 3, real UDP sockets on loopback for the loss-free second seed. **108 of 108 in sync**
(all peers agree, bundle replay matches), on both Pi and x86 nodes.

## Cross-architecture determinism (jobs det37a on the Pis, det37x on x86)
All 10 faction pairs (4 mirrors + 6 cross) x 4 biomes, layout 1, seeds 1-2 x 2 swaps, with `--record-dir`: 160 games, run once on arm64 and
once on amd64. **All 160 replay files and all 40 result tables are byte-identical** across the two architectures (sha256 compared).

## Balance baseline on main (job base37)
`ai match` tiered:3 v tiered:3, size 96, layout 1 (the skirmish default), 30 000 ticks, seeds 1-64 x 2 start swaps = 128 games per row, `--income 1`.
Cells: win % of decided games for the first-named faction. For the first time every biome was measured (earlier tables covered Rust Belt and
Glass Coast only). Rust Belt and Glass Coast rows reproduce the s35 table game for game.

| Row | Rust Belt | Glass Coast | Mountains | Ash Plains |
|---|---|---|---|---|
| Auth v Dir | 47.6 % | 49.5 % | 32.7 % | 32.4 % |
| Auth v LB | 64.7 % | 61.1 % | 81.5 % | 83.5 % |
| Auth v Exp | 36.8 % | 43.6 % | 77.4 % | 57.9 % |
| Dir v LB | 47.4 % | 44.6 % | 83.1 % | 81.0 % |
| Dir v Exp | 58.0 % | 26.7 % | 87.6 % | 78.6 % |
| LB v Exp | 49.1 % | 34.4 % | 37.3 % | 25.4 % |

Mirror rows (start bias): the start-0 share is inside 38-60 % on every biome (Rust Belt Directorate 38.5 % is the widest); no start-position fix is
needed beyond D-277. `ai mirror` reports every generated map cell-symmetric (64 probes: 4 biomes x 2 layouts x 4 factions x 2 seeds).

**Finding: Mountains and Ash Plains (layout 1) break the Left Behind and the Expedition.** Their income at 5 minutes fell to about 60-70 % of the
Authority's and Directorate's (mean harvested + shipments at 3/5 min on Mountains: Authority 8 732 / 18 257, Directorate 8 539 / 17 577,
Left Behind 6 091 / 10 787, Expedition 5 729 / 12 540).

## Map and AI fixes (M13-33, M13-34, M13-35)
Both maps use mirror_x left/right starts on the middle row. Three causes, found with new trace tools (`Tools map econ`, per-harvester
`--econ` trace lines):
1. **AI placement used unturned docks (M13-34).** The sim turns a building's dock/exit 180 degrees in the south half of the map; the AI's
   PlacementSearch kept lanes on the authored side. A refinery placed just south of the middle row had its real dock in a 1-cell corridor that
   the AI then walled, and loaded harvesters deadlocked (Left Behind stuck at 1 800 / 3 000 credits). Fix: one shared rule
   (`BuildingOrientation`) used by the sim and, through the view, by the AI. This changes AI placement on every map.
2. **Start row (M13-33).** New biome key `start_row_shift` (6 for mountains and ash_plains): mirror_x starts sit 6 cells above the middle row,
   so the base stays in one half. Rust Belt and Glass Coast maps are byte-identical (pinned by a new test).
3. **Expedition supply lane (M13-35).** The carved shallow channel to an inland Expedition base was 3 wide; the AI's Quay/Shipyard sealed it and
   the start Landing Craft blocked the Anchorage site, so tenders were stranded and shipments stopped. Fix: the channel ends in a 7-wide bay,
   and the AI parks the Landing Craft off the dock water until the Anchorage stands.

Start shift alone (job bm37, on the s37 UX build) fixed the Left Behind on Mountains but not the Expedition; all three together (job bc37, build
`e90dff9`):

| Row | Rust Belt | Glass Coast | Mountains | Ash Plains |
|---|---|---|---|---|
| Auth v Dir | 41.7 % | 32.6 % | 25.5 % | 19.2 % |
| Auth v LB | 59.5 % | 66.7 % | 61.9 % | 79.5 % |
| Auth v Exp | 34.7 % | 35.8 % | 53.9 % | 42.6 % |
| Dir v LB | 32.4 % | 45.6 % | 61.8 % | 65.8 % |
| Dir v Exp | 57.1 % | 33.0 % | 68.7 % | 77.8 % |
| LB v Exp | 47.5 % | 32.0 % | 43.0 % | 21.1 % |

Incomes at 3/5 min are now level on every biome (e.g. Mountains: Authority 8 496 / 17 518, Directorate 9 434 / 18 646, Left Behind 8 503 / 17 641,
Expedition 6 662 / 16 006). The worst rows of the old table (82-88 %) are gone; what remains is faction balance (next section). The AI fix made
the Directorate stronger against the Authority on every map. Campaign sweep on `e90dff9` (job swc37): all 45 mission-tiers meet D-222
(auth_01 tier 5 6 -> 10 of 12, dir_02 tier 5 7 -> 5 of 12; nothing else moved).

## Unit sensitivity screen (jobs v37sa, v37sd, v37se, v37sl)
27 single-knob variants (+15 % cost or health of the units each faction spends most on, from a 192-game unit ledger, job led37b), each on its
faction's 12 cross rows (3 pairs x 4 biomes) at 64 games, compared with the same seeds of the main baseline. Mean change in the varied faction's
win share (one cell's noise is about +/-9 points; a 12-row mean about +/-2.5):

| Knob (+15 %) | Effect | Knob (+15 %) | Effect |
|---|---|---|---|
| Directorate Rocket Truck cost | **-14.9** | Expedition Glide Tank health | **+7.7** |
| Expedition Glide Tank cost | -5.3 | Left Behind Scavenger cost | -5.6 |
| Left Behind Buggy cost | -5.3 | Directorate Ashthrower cost | +5.1 |
| Authority Tank health | +4.0 | Authority Tank cost | -3.3 |
| Expedition Sparker cost | +2.5 | Authority Scraper cost | -2.3 |
| Directorate Scraper cost | -2.2 | Directorate Rocket Truck health | +2.0 |
| Authority Rocketeer cost | +1.3 | Authority Refinery cost | +1.5 |
| Authority Rifleman cost | -1.2 | Authority Jeep cost | +0.7 |
| Directorate Conscript cost | +0.3 | Expedition Marine cost | +0.1 |
| Left Behind Harpoon Wagon cost | -0.6 | Left Behind Buggy health | -0.1 |
| Expedition Supply Tender, LB Mother Rig cost | ~0 (inert) | | |

Per-opponent effects that matter for the package: the Rocket Truck cost hits the Directorate hardest against the Authority (-20 on average);
the Scavenger cost hits the Left Behind hardest against the Authority (-8.5) and least against the Directorate (-2.5).
