# Session 38 results: AI ladder, Super hard (LLM), layout 2, Expedition per tier, draws, map-specific balance screens

All runs on the home cluster (laptop k3s-cluster folder `claude/rts/s38/`: tiers.sh, base_l2.sh, edge.sh, ladr.sh, var2.sh (+ mkvar.py with
`mapgen` and string values), tally.py / cmp.py / cmp2.py / inc.py / led.py helpers). Base build: `1e2cbb8` (session 37, = main after PR #118) unless a
section says otherwise. "Decisive %" = wins / (wins + losses) for the first-named side; draws are games still running at 30 000 ticks.
About 25 000 games were played this session across all 11 worker slots groups (Pis and the three x86 nodes).

## Session 37 merged (PR #118)
Full suite on the cluster on `1e2cbb8` (job t38): Sim 1918, Net 175, Scripting 429, AI 413 (+1 skipped), Llm 174, Game 2197 (+2 skipped), Tools 59,
**0 failed**. GitHub shows no Windows CI runs for PRs after Nov 12 (none for PRs #110-#118 either), so the cluster run is the check, as in session 36.

## Verification of what lands (`f7ef0a5`, integ-s38 with D-286..D-288)
Full suite on the cluster (job tf38): Sim 1918, Net 175, Scripting 429, AI 414 (+1 skipped), Llm 174, Game 2205 (+2 skipped), Tools 67, **0 failed**.
Campaign sweep (job swf38, 45 mission-tiers x 12 seeds): **all 45 meet D-222**; tier-5 moves against session 37: auth_04 6 -> 4 of 12 (floor 3).
The default-skirmish state hash was re-pinned for D-287 (SkirmishDefaultHashTests).

## AI difficulty ladder (M4-16; job tr38, then ld38)
Mirror matches, rust_belt layout 1, size 96, 128 games per cell (seeds 1-64 x 2 swaps). Decisive % of the higher tier:

| Cell | Authority before | Authority after D-287 | Directorate | Left Behind | Expedition |
|---|---|---|---|---|---|
| 2 v 1 | **30.5** | 70.9 | 79.2 | 86.8 | 47.8 |
| 3 v 2 | 54.7 | 50.0 | 68.5 | 78.6 | 50.4 |
| 4 v 3 | 50.0 | 59.1 | 53.5 | 63.9 | 40.9 (27 % draws) |
| 5 v 4 | **38.8** | 52.2 | 58.2 | 66.0 | 55.3 (70 % draws) |
| 5 v 3 | **39.8** | 56.3 | 63.6 | 78.9 | 65.9 (34 % draws) |
| 5 v 1 | 52.0 | 66.7 | 90.3 | 92.0 | 60.3 (43 % draws) |

**Cause (Authority):** the shared `respond_to_attacks` flag of tiers 2-5 (garrison and Blockhouse, wave regroup, mop-up, bag growth, siege, home
guard ...). A 64-game screen switching one tier-2 flag off at a time: respond_to_attacks 0 -> 76 %; repair 0 45 %, scatter 0 37.5 %, replace_harvesters 0
37 %, attack_ratio_pct 0 42 %, decision_interval 60 24 %. Tier-5 flags switched off one at a time (Authority 5 v 3, ref 39.8 %): adapt_build 44, harass 40,
multi_front 38, predicted_retreat 31, scouting 47, superweapons 37, two_pronged 36: none. A helper split respond_to_attacks into 15 sub-switches: the
wave regroup (pulls survivors out of an assault when home idle units reach the wave size) is the largest leak (40 -> 56 %), the rest are many small
ones; together off 84 % (32 games). **Fix (D-287):** `[faction.authority.tier2..tier5] respond_to_attacks = 0` (new per-faction override key).
With the tier-3 army cap of D-288 (job vc38, 80 games): 3 v 2 46.8, 4 v 3 64.8, 5 v 3 62.8 (the other cells do not involve tier 3).
Keeping tier 3 unchanged (job vk38) is not an option: tier 3 with the flag v tier 2 without it falls to 19.0 %.
Directorate and Left Behind ladders are fine. The Expedition ladder is flat and draw-heavy at tiers 4-5 (open, not changed).

## Super hard (LLM) opponent (M5-15; job ed38)
Directives cannot add strength (D-285), so the preset is tier 5 + LLM commander + an income and build-speed edge (the existing per-slot handicap, now
allowed up to 150 % for AI slots). `ai match --handicap-a 125|150|175`, tier 5 with the edge v plain tier 5, mirror, rust_belt, 64 games:

| Faction | +25 % | +50 % | +75 % |
|---|---|---|---|
| Authority | 74.6 | 85.0 | (75.0 on 16) |
| Directorate | 53.3 | 67.8 | - |
| Left Behind | 86.2 | 85.7 (16) | - |
| Expedition | 85.2 (58 % draws) | 100 (16) | - |

The edge is +50 % (D-286). The Directorate gains least: its funds peak at the storage limit (4 989 in the logs) and its army stays the same size, so something other than
money (supply, its own caps) limits it; follow-up.

## Layout 2 (M12-07..12; job l238)
Layout 2 is off by default. 128 games per row, tier 3 v 3:

| Row | Rust Belt | Glass Coast | Mountains | Ash Plains |
|---|---|---|---|---|
| Auth v Dir | 14.4 % | 29.8 % | 20.2 % | - |
| Auth v LB | 88.6 % | 84.2 % | 86.0 % | - |
| Auth v Exp | 50.4 % | 54.6 % | 44.5 % | - |
| Dir v LB | 78.4 % | 82.5 % | 88.0 % (96 games) | - |
| Dir v Exp | 86.5 % | 86.3 % | - | - |
| LB v Exp | 33.6 % | 41.3 % | - | - |
| Mirrors (start 0 share) | 43-53 % | 42-47 % | 44-53 % | - |

"-" = not finished at wrap-up (job l238 keeps running; outputs in `/work/out/l238/l2`).

Layout 2 is far from fair (Authority v Left Behind 84-89 %, Directorate v Expedition 86 %, Directorate v Left Behind 78-88 %); it stays off. The layout-1
rows that the job also ran reproduce the session 37 table game for game (a free determinism check).

## Expedition at every tier (M7-14 §6 bullet 1; job tr38)
Expedition tier N v each faction at tier N, layout 1, 32 games (16 seeds x 2), W-L-D for the Expedition:

| Opponent | Map | T1 | T2 | T3 | T4 | T5 |
|---|---|---|---|---|---|---|
| Authority | Rust Belt | 0-28-4 | 13-19-0 | 16-14-2 | 14-10-8 | 16-8-8 |
| Authority | Glass Coast | 3-29-0 | 4-27-1 | 20-11-1 | 8-19-5 | 10-17-5 |
| Authority | Mountains | 3-27-2 | 4-27-1 | 17-14-1 | 13-11-8 | 13-11-8 |
| Authority | Ash Plains | 2-30-0 | 6-23-3 | 11-17-4 | 6-18-8 | 11-15-6 |
| Directorate | Rust Belt | 5-22-5 | 21-2-9 | 11-16-5 | 15-4-13 | 10-3-19 |
| Directorate | Glass Coast | 13-14-5 | 25-4-3 | 22-8-2 | 22-4-6 | 18-7-7 |
| Directorate | Mountains | 4-23-5 | 11-9-12 | 10-14-8 | 13-2-17 | 13-5-14 |
| Directorate | Ash Plains | 2-22-8 | 9-7-16 | 2-22-8 | 11-7-14 | 8-10-14 |
| Left Behind | Rust Belt | 24-7-1 | 25-7-0 | 11-17-4 | 5-8-19 | 10-13-9 |
| Left Behind | Glass Coast | 28-4-0 | 26-6-0 | 14-9-9 | 16-4-12 | 15-6-11 |
| Left Behind | Mountains | 21-9-2 | 21-10-1 | 14-15-3 | 10-7-15 | 12-15-5 |
| Left Behind | Ash Plains | 23-9-0 | 22-10-0 | 21-11-0 | 24-5-3 | 10-14-8 |

(Measured on `1e2cbb8`, before D-287; Authority tiers 2-5 changed since.)

The Expedition AI wins at least some games against every faction at every tier except tier 1 v the Authority (2-3 of 32 on three maps, 0 on Rust Belt). The bullet
itself asks for a human win, so it stays with the owner's play-test.

## Late-game draws (M13-23; from the s37 final table bp37)
787 of 5 120 games (15.4 %) reach the 30 000-tick limit. Glass Coast has the most (Directorate mirror 42 %, Directorate v Left Behind 31 %, LB mirror 31 %,
Authority v Directorate 30 %), where the ore runs out first; the other maps are 2-26 %. In 734 of the 787 draws one side leads by 10 % or more on the
adjudication score (army + structures + credits). No change.

## Map-specific balance screens (M13-37; see the next section for what landed)
The remaining outliers (s37 table): Directorate v Authority on Mountains/Ash Plains (~80 %), the Expedition on Glass Coast (Dir 21 %, LB 34 %), and
Directorate v Left Behind Rust 26.5 % v Ash 77.8 %. Each lever was screened on the rows it can touch, 64 games per row against the same seeds of the
session 37 table (`cmp.py`). What was ruled in or out:

- **Economy, not unit mix.** Mirror incomes differ little by map (Directorate v Authority income ratio at 5 min 0.99-1.07), but head-to-head on
  Mountains the Directorate earns 19.5k v 17.8k at 5 min (Rust Belt: level) and loses 55 % of its Scrapers v 77 % on Rust Belt; the Authority's harvester
  losses go the other way. Unit ledgers (job vl38) show the same Directorate and Authority unit mix on every map.
- **Directorate tunnels (Bores) — not the map cause, but a waste.** Bore cost 60 000 (the AI never buys one; job vt38) made the Directorate 5-33 points
  stronger in every cell (Authority v Directorate Rust 52.7 -> 19.2 %, Directorate v Expedition Rust 57 -> 85 %): it buys ~3 Bores a game and loses 94-99 %
  of them. Removing them is an AI change for a later session (it needs a matching Directorate nerf; the Rocket Truck cost is a cliff: with no Bores,
  970 -> 1000 moved Directorate v Expedition Rust 85 -> 35 %, job vd38).
- **Glass Coast water/navy — rejected** (helper trace: Expedition units fight on land; Destroyer damage 1 changed nothing). The Glass Coast ore runs out by
  ~10 000 ticks and shipments keep paying (D-246). More Glass Coast ore (extra_fields 2, ash_fields_per_start 3) makes the Expedition rows worse.
- **Left/right layout.** Mountains with rot2: Authority v Directorate unchanged (17.3 v 17.0 %). Ash Plains rot2 fixes Directorate v Left Behind
  (77 -> 36 %) and LB v Expedition (38 -> 61 %) but worsens Authority v Directorate (22 -> 12.5 %) and brings back the start bias D-277 removed.
- **Start row shift 10 (Mountains/Ash):** Authority v Directorate Mountains 19.5 -> 39.5 % but Authority v Expedition +28 points; Ash Authority v
  Directorate worse. Mixed.
- **Process finding:** map-generator variants made by copying `data/` into a working tree are inert: `biomes.toml` is read next to `Tools.dll`, not from
  the working directory (rules and AI data are read from the working tree). The first Glass Coast, rot2 and start-row screens were therefore identical to
  the baseline and were rerun with a per-variant copy of the binary folder (var2.sh).

**What landed for balance:** only the Authority change (D-287) and its tier-3 army cap (D-288). Authority cross rows at tier 3, 128 games:

| Authority v | Rust Belt | Glass Coast | Mountains | Ash Plains |
|---|---|---|---|---|
| Directorate (s37 -> D-287 -> +cap 40) | 52.7 -> 40.2 -> 40.9 | 51.1 -> 51.2 -> 48.2 | 20.4 -> 22.7 -> 23.5 | 20.5 -> 12.1 -> 11.0 |
| Left Behind | 64.3 -> 75.4 -> 72.8 | 57.4 -> 51.4 -> 50.5 | 61.7 -> 55.9 -> 51.1 | 52.9 -> 49.1 -> 40.4 (80) |
| Expedition | 42.3 -> 75.0 -> 67.2 | 45.4 -> 45.1 -> 43.3 | 62.1 -> 67.8 -> 63.8 | 48.8 -> 67.2 -> 54.5 (64) |

Mean distance from 50 % over the 12 Authority rows: 10.4 (s37) -> 14.6 (D-287) -> 12.7 (with the cap); over all 24 cross rows about 13.3 -> 14.4. The
tier-3 table is a little less even than session 37's, in exchange for an Authority ladder that climbs. The map-specific rows above are not fixed.
`attack_ratio_pct` is inert for the Authority (games identical); `attack_threshold` 6/11 and `expansion_tick` 7200 were weaker than the cap.
