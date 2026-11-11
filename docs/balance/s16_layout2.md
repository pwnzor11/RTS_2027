# S16 M12-12: AI check and income re-measure on map layout 2

Method: Release build of `src/Tools` in a scratch dir (run the dll directly, one match process at a time). New flags `--layout 1|2` and `--biome rust_belt|glass_coast|mountains|ash_plains`
on `ai match` and `ai timeline` (`--layout` on the classic `ai matrix`; `ai matrix --players` already had `--biome` and takes `--layout` too); `--income 1` prints harvested credits at 3 / 5 / 10 minutes per game.
With neither flag the synthetic `MapGenerator.Generate(size, seed)` map is used, so every older number is reproducible (controls below).
All runs: Medium (`--size 96`), 2 players, `--ticks 30000`, tiered AI, seeds 1..N with the two start positions swapped (2N games per cell). Columns are A wins / B wins / draws.
"Decisive %" = A / (A + B). Noise: about +-10 points on a decisive % at 30 decisive games, and worse when draws take half the games.

Controls (layout 1, synthetic map, new build): Authority 5 v Authority 1 = 20 / 9 / 3, avg end tick 17868; Left Behind 3 v Directorate 3 = 17 / 12 / 3, avg end tick 15883.
Both equal the Q-67 table in `s16_q67.md` game for game. No AI code was changed in this package.

## Tooling

- `ai match --layout 2 --biome ash_plains ...`: builds the map with `MapGenerator` at that biome and layout (size class from the cell count: below 80 small, below 112 medium, else large).
  The table heading names it ("rust_belt layout 2"). `--layout 1` with a biome uses the biome generator at layout 1; the old synthetic map is only used when neither flag is given.
- Layout 2 has no ocean edges. For layout 2 an Expedition side therefore gets no `OceanEdges`/landing markers (layout 1 keeps the M7-10 north + south sea).
- `ai timeline` takes the same two flags. `ai matrix` (classic tier matrix) takes `--layout`.
- 4p 2v2: `ai match` is a 2-player tool, and `ai matrix --players 3|4` is free-for-all with rotated starts (no team option). Team games are not supported by any `ai` command, so the 4p 2v2 start check
  is **reported, not built**.
- Tests: `tests/Tools.Tests/AiMatchLayoutTests.cs` (flag parsing, layout 1 vs 2 maps differ, a short layout-2 match runs and the table names the layout).
- Laptop script: `tools/measure/run_s16_layout2.ps1` (32 seeds, 64 games per row, rust_belt and ash_plains by default, 7 rows each, base column = the layout-1 numbers below).

## Start fairness (mirror matches, tier 3 both sides, 16 seeds = 32 games per cell)

"Start 0 / start 1" = wins by the start position in `map.StartPositions` order. On layout 2 the skeleton is turned per seed (`LayoutVariant`), so start 0 is not one fixed corner; the check is whether one start index wins more.
Pass rule: no start wins more than 60 % of decisive games.

| Biome | Faction | A / B / draws | Start 0 / start 1 wins | Larger share | Verdict |
|---|---|---|---|---|---|
| rust_belt | Authority | 12 / 17 / 3 | 10 / 19 | 66 % start 1 | over 60 |
| rust_belt | Directorate | 12 / 5 / 15 | 6 / 11 | 65 % start 1 | over 60 (15 draws) |
| rust_belt | Left Behind | 10 / 14 / 8 | 10 / 14 | 58 % start 1 | pass |
| rust_belt | Expedition | 0 / 0 / 32 | 0 / 0 | n/a | does not play (see AI issues) |
| ash_plains | Authority | 16 / 12 / 4 | 20 / 8 | 71 % start 0 | over 60 |
| ash_plains | Directorate | 10 / 8 / 14 | 11 / 7 | 61 % start 0 | marginal |
| ash_plains | Left Behind | 9 / 12 / 11 | 13 / 8 | 62 % start 0 | over 60 |
| ash_plains | Expedition | 0 / 0 / 32 | 0 / 0 | n/a | does not play |
| mountains | Authority | 18 / 11 / 3 | 20 / 9 | 69 % start 0 | over 60 |
| mountains | Directorate | 13 / 13 / 6 | 18 / 8 | 69 % start 0 | over 60 |
| mountains | Left Behind | 10 / 10 / 12 | 10 / 10 | 50 % | pass |
| mountains | Expedition | 0 / 0 / 32 | 0 / 0 | n/a | does not play |
| glass_coast | Authority | 19 / 13 / 0 | 13 / 19 | 59 % start 1 | pass |
| glass_coast | Directorate | 14 / 8 / 10 | 10 / 12 | 55 % start 1 | pass |
| glass_coast | Left Behind | 7 / 13 / 12 | 8 / 12 | 60 % start 1 | borderline |
| glass_coast | Expedition | 11 / 12 / 9 | 13 / 10 | 57 % start 0 | pass |

Pooled over the three working factions (Expedition on Glass Coast included there): rust_belt start 0 / start 1 = 26 / 44 (63 % start 1), ash_plains 44 / 23 (66 % start 0), mountains 48 / 27 (64 % start 0),
glass_coast 44 / 53 (55 % start 1).

Reading: the check is **not met** on rust_belt, ash_plains and mountains. The direction is not one start index everywhere (start 1 on rust_belt, start 0 on ash_plains and mountains), which fits a
skeleton-dependent advantage (a base nearer the natural, a shorter ramp, an easier river deck) rather than a fault in start order. Caveat: 25-30 decisive games per cell is +-10 points, and the pooled
rust_belt (70 games) and ash_plains / mountains (67 and 75) figures are 2 to 3 sigma. For reference, the same mirror at layout 1 with 8 seeds (16 games) already showed skew: rust_belt Directorate
3 / 11 (start 0 / start 1), mountains Authority 4 / 11, mountains Directorate 3 / 11, so layout 1 is no cleaner at that sample size. A 32-seed rerun per biome would settle it; that is the next owner step if
start fairness gates the default.

## AI behaviour on the new maps

Looked at: placement in walled mains, expansion to the natural, harvesters, draw spikes. Evidence: `ai timeline --trace` on seeds 1-8 for all four biomes at both layouts, and a temporary counter
(not committed) in `PlacementSearch.Find` that fired whenever a structure had no legal site.

| Question | Finding |
|---|---|
| Builders failing to place in a walled main | **None.** 0 "no site" failures across 12 cells (4 biomes x Authority / Directorate / Left Behind) x 6 seeds x 12 000 ticks on layout 2. The 12-cell search radius around the yard always found a site. |
| Not expanding to the natural / harvesters stuck | Harvesters: 4 live scrapers at 9 min in 8 of 8 rust_belt layout-2 seeds (layout 1: 4, 4, 3, 2, 4, 4, 4, 4). No harvester stuck in the traces looked at. |
| Stalls at the front | Draw counts are higher on layout 2 for Directorate mirrors (rust_belt 15 of 32, ash_plains 14, glass_coast 10; layout 1: 2-4 of 16) and Left Behind mirrors (8-12 of 32 vs 4-9 of 16). In the drawn Directorate games both sides sit at 450-520 units at tick 30 000 (seed 2, 4, 5, 8 on rust_belt): a defended-base stalemate, the same pattern `s16_q67.md` found for Directorate tier 5 v 3, not a stuck AI. |
| Cash banked | Layout 2 shows 2 000-3 500 unspent credits at 6-12 minutes more often than layout 1 (e.g. seed 2 rust_belt Authority, 3 000+ from 6 to 13 minutes), with no placement failure. This is the income gain (below) meeting a build order and a supply cap that do not scale with it. Not a layout fault. |
| Expedition does not start on layouts without sea | **Found, not fixed.** On layout 2 rust_belt, ash_plains and mountains an Expedition side has 0 structures and 0 units for the whole game (32 of 32 draws, cash 5 000 unspent): it needs sea and landing markers, which layout 2 does not generate. Glass Coast works (it has sea). Layout 1 mountains with ocean edges is also near-dead for Expedition (income 347 per minute). A generator item (landing / sea for layout 2), outside this package's ownership. |

No AI change was made. The brief allowed only small, behaviour-only fixes that leave layout 1 bit-identical, and nothing in the traces pointed at one.

## Income (credits harvested per minute, tier 3 mirror, Medium 2p, 8 seeds = 16 games per cell, both sides pooled)

Per-minute rate = harvested credits at the mark / minutes. Only games that reached the mark are averaged (the 10-minute column drops games that ended earlier; n shown in the notes).
Layout 1 here = the biome generator at layout 1 (`--layout 1 --biome X`), not the synthetic map. Change = layout 2 against layout 1.

| Biome | Faction | L1 3 / 5 / 10 min | L2 3 / 5 / 10 min | Change 3 / 5 / 10 min |
|---|---|---|---|---|
| rust_belt | Authority | 3204 / 3579 / 3605 | 3311 / 4273 / 5076 | +3 / +19 / +41 % |
| rust_belt | Directorate | 2856 / 3314 / 3666 | 3055 / 4635 / 5725 | +7 / +40 / +56 % |
| rust_belt | Left Behind | 2296 / 2348 / 2204 | 2952 / 3942 / 3833 | +29 / +68 / +74 % |
| rust_belt | Expedition | 2384 / 3473 / 3723 | 0 | no start on layout 2 |
| ash_plains | Authority | 3425 / 4147 / 4579 | 3199 / 4383 / 5199 | -7 / +6 / +14 % |
| ash_plains | Directorate | 2978 / 3851 / 4657 | 3180 / 4214 / 5446 | +7 / +9 / +17 % |
| ash_plains | Left Behind | 2676 / 3302 / 3031 | 2905 / 3601 / 3310 | +9 / +9 / +9 % |
| ash_plains | Expedition | 2487 / 3701 / 4590 | 0 | no start on layout 2 |
| mountains | Authority | 2922 / 3820 / 3681 | 3034 / 4188 / 5471 | +4 / +10 / +49 % |
| mountains | Directorate | 2660 / 3671 / 3636 | 3115 / 4554 / 5648 | +17 / +24 / +55 % |
| mountains | Left Behind | 2604 / 2949 / 2477 | 2974 / 3660 / 3786 | +14 / +24 / +53 % |
| mountains | Expedition | 347 / 379 / 326 | 0 | layout 1 already near-dead |
| glass_coast | Authority | 2771 / 3003 / 2078 | 3172 / 4309 / 5123 | +14 / +43 / +147 % |
| glass_coast | Directorate | 2760 / 2978 / 2557 | 3006 / 4299 / 5448 | +9 / +44 / +113 % |
| glass_coast | Left Behind | 2793 / 3264 / 2089 | 2825 / 3661 / 3521 | +1 / +12 / +69 % |
| glass_coast | Expedition | 1446 / 2481 / 2286 | 2406 / 3208 / 3921 | +66 / +29 / +72 % |

Samples (both sides x games reaching the mark): 32 at 3 and 5 minutes in every cell; at 10 minutes between 16 (glass_coast Authority, layout 1) and 32.

Reading: income is about equal at 3 minutes (-7 to +29 %, mostly under +15 %) and pulls ahead from 5 minutes, +40 to +56 % at 10 minutes on rust_belt and mountains for Authority and Directorate, and
over +100 % on glass_coast where layout 1 income *falls* after 5 minutes (cause not investigated; the layout-1 Glass Coast fields may run out). Likely cause (not tested): layout 2 main fields are 3+ thick bands plus a contested yard, so fields last longer. Layout 2 is a much richer economy than layout 1 in the second half of the game;
the build order does not spend it all (cash banked above).

## Balance (tier 3 cross-faction cells and Directorate 5 v 3, layout 2, 16 seeds = 32 games; layout-1 column from `s16_q67.md` / `s15_lbknob.md`, synthetic map, 32 games)

Expedition cells only on glass_coast, the one biome where Expedition starts on layout 2.

| Cell (A v B) | Biome | Layout 2: A / B / draws (avg end tick) | Decisive % A | Layout 1 (synthetic map) A / B / draws | Decisive % A |
|---|---|---|---|---|---|
| Left Behind 3 v Directorate 3 | rust_belt | 0 / 12 / 20 (24669) | 0 % | 17 / 12 / 3 | 59 % |
| Left Behind 3 v Directorate 3 | glass_coast | 2 / 11 / 19 (24658) | 15 % | | |
| Directorate 3 v Authority 3 | rust_belt | 27 / 3 / 2 (18874) | 90 % | 17 / 9 / 6 | 65 % |
| Directorate 3 v Authority 3 | glass_coast | 24 / 4 / 4 (20047) | 86 % | | |
| Left Behind 3 v Authority 3 | rust_belt | 9 / 13 / 10 (21585) | 41 % | 16 / 14 / 2 | 53 % |
| Left Behind 3 v Authority 3 | glass_coast | 3 / 20 / 9 (19773) | 13 % | | |
| Directorate 3 v Expedition 3 | glass_coast | 20 / 3 / 9 (20054) | 87 % | 12 / 17 / 3 | 41 % |
| Left Behind 3 v Expedition 3 | glass_coast | 10 / 7 / 15 (23240) | 59 % | 15 / 12 / 5 | 56 % |
| Authority 3 v Expedition 3 | glass_coast | 18 / 7 / 7 (18905) | 72 % | no 16-seed number in the docs read | |
| Directorate 5 v Directorate 3 | rust_belt | 15 / 9 / 8 (20765) | 63 % | 21 / 8 / 3 | 72 % |
| Directorate 5 v Directorate 3 | glass_coast | 13 / 10 / 9 (22633) | 57 % | | |

Rust Belt and Glass Coast are the two biomes run. The layout-1 column is the synthetic map, not the biome generator at layout 1, so it is a reference, not a like-for-like control.

## v5 re-measure (M12-12 follow-up, partial: work stopped when the shell tool became unavailable)

Sea for the Expedition: layout 2 now builds sea behind every main (rock masses that reach the map edge become water with a beach), convoy cells and one landing per start whenever the generator gets an ocean request, in all four biomes (`MapGenerator.WithMarkers`; test `Layout2_WithOcean_HasSeaConvoyAndLandings`). `ai match` now asks for the sea on layout 2 as well. Expedition v Expedition at layout 2 plays on rust_belt (24 / 28 / 12), mountains (26 / 26 / 12) and glass_coast (25 / 25 / 14). Ash Plains is still 0 / 0 / 64 (no structure, no unit): not yet found; the map has convoy cells and two landings and passes the validator, so it is probably the Expedition start ship choosing a site (ExpeditionTactics.ChooseSite) or the start kit placement on this skeleton. Not fixed.

Income (tier 3 Authority mirror, Medium 2p, 8 seeds, harvested credits per minute at 3 / 5 / 10 min, layout 2 after the trims: money per start about 42 % of layout 1 (30 % on glass_coast), contested and natural fields cut, ford bar 3 pitch cells, natural on 4 seeds in 10, main fields kept 3+ thick). Layout 1 numbers are the M12-12 table above (Authority).

| Biome | L1 | L2 before | L2 after | After vs L1 |
|---|---|---|---|---|
| rust_belt | 3204 / 3579 / 3605 | 3311 / 4273 / 5076 | 3157 / 4211 / 4671 | -1 / +18 / +30 % |
| mountains | 2922 / 3820 / 3681 | 3034 / 4188 / 5471 | 3021 / 4240 / 4760 | +3 / +11 / +29 % |
| ash_plains | 3425 / 4147 / 4579 | 3199 / 4383 / 5199 | 2965 / 4181 / 4564 | -13 / +1 / 0 % |
| glass_coast | 2771 / 3003 / 2078 | 3172 / 4309 / 5123 | 3180 / 4166 / 4337 | +15 / +39 / +109 % |

Not within +-10 % yet at 5 and 10 minutes on rust_belt, mountains and glass_coast: the harvest rate per minute is limited by harvesters and travel more than by field richness (cutting the money from 100 % to 25 % of layout 1 moved the 10-minute rate from 5796 to 4768 only), so the next lever is field distance / count near the main, not density. Glass Coast layout 1 falls after 5 minutes (its fields run dry), which layout 2 does not copy.

Start fairness (tier 3 mirror, 32 seeds = 64 games per cell; wins by start index, start 0 / start 1), after making every Medium and Large 2-player seed half-turn (a mirror-diagonal skeleton gave 64-70 % to one start; rot2 skeletons measure about even):

| Biome | Authority | Directorate | Left Behind | Expedition | Pooled larger share |
|---|---|---|---|---|---|
| rust_belt | 35 / 25 | 20 / 27 | 20 / 23 | 28 / 24 | 51 % |
| mountains | 31 / 28 | 30 / 16 | 29 / 13 | 24 / 28 | 57 % |
| glass_coast | 30 / 25 | 22 / 26 | 22 / 19 | 23 / 27 | 50 % |
| ash_plains (mirror skeleton, kept for its two different crossings) | 17 / 27 | 9 / 23 | 16 / 24 | no start | 64 % |

Mountains Directorate and Left Behind are still skewed (65 % and 69 % to start 0), ash_plains is over the limit. Cause not found for Mountains (the skeleton is the rot2 one that is even on rust_belt; the plateau mains and ridge river differ). For Ash Plains the mirror-diagonal skeleton is the likely cause, since the same skeleton measured 64-70 % on rust_belt and mountains before v5; moving Ash Plains to half-turn needs its three different crossings redesigned (the two decks of a half-turn map are images of each other).

Reading: on layout 2 the order of the factions changes a lot. Directorate tier 3 beats Authority (86-90 %), Expedition (87 %) and Left Behind (85-100 %); Left Behind loses to Authority on Glass Coast
(13 %) and to Directorate everywhere, and is the only faction whose position drops on both maps. Directorate's early-army hold (D-191, tier 3 army 18 before tech) plus layout 2's long, bridged
approach seems to favour a faction that fields a larger defended army before attacking; Left Behind's tier 3 pressure relies on an early raid that has to cross a river deck or a ford and a walled main
(hypothesis from the traces and the draw counts: 19-20 of 32 Left Behind v Directorate games end as draws, no run tested it directly). Directorate 5 v 3: tier 5 takes 63 % (rust_belt) and 57 % (glass_coast) of decisive games against 72 % on layout 1, within the noise.

## Recommendation on making layout 2 the default

Do not make layout 2 the default yet. In order:

1. **Expedition cannot play on three of four biomes on layout 2** (no sea, no landing markers; 0 units for 30 000 ticks). A default switch would break every Expedition skirmish and mission on rust_belt, ash_plains and mountains.
   This needs a generator decision first (sea edges / landing sites for layout 2, or "Expedition requires Glass Coast").
2. **Start fairness is not met** on rust_belt (63 % start 1), ash_plains (66 % start 0) and mountains (64 % start 0), at 70 to 75 decisive games each. Glass Coast passes. A 32-seed rerun per biome, then a
   look at which skeleton variants carry the skew (`LayoutVariant` orientation against start index), before deciding.
3. **Faction balance shifts** by 20-40 points for Directorate up and Left Behind down. The Q-67 and LB-knob work that put the cells in band on layout 1 would have to be redone on layout 2 (laptop script
   `tools/measure/run_s16_layout2.ps1` gives the 64-game numbers).
4. What is fine: no builder failed to place anywhere, harvesters keep working, income is equal at 3 minutes and higher later, and the walled mains cause no AI stall. The AI itself needs no change for layout 2.

## v6 (session 17, M12-12: Ash Plains half turn, field distance, Mountains skew)

Owner decisions applied: Q-68 (a) Ash Plains 2-player Medium and Large maps are half-turn like the other biomes; Q-69 (a) layout-2 fields moved away from the refinery, no richness cut, no faction or AI change. `CurrentLayout` stays 1 and layout 1 output is untouched (all changes sit in the layout-2 half-turn builder, `map report` and `LayoutVariant` / `LayoutChecks` call sites for Ash).

**Ash Plains.** `LayoutVariant.For` no longer excludes Ash. Each of the two decks (images of each other) is now a pair: the intact bridge at the deck row and, 2 water rows south of it, a 5-row broken causeway (shallow edge rows, a 3-wide road/rubble way). Both banks flare over the causeway rows so the pair is one reachable crossing area. Ash `y0` is capped so the causeway ends above the ford. Small, 3-player and 4-player Ash maps keep the mirror skeleton (bridge + causeway on different decks). 50-seed sweep (`Layout2_Sweep_50Seeds_AllSizes_AllPlayerCounts`, Ash, run after step 1): Medium 2p 50/50, Large 2p 50/50 pass every layout check (0 failures; 4.6 and 2.2 retries per seed); other cells unchanged by this step (Small 2p 50, 3p 47/50 Small, 4p 48/50 Small, 49/50 Medium 4p; the fallback best attempt is used there as before). Start fairness of the new Ash skeleton was NOT measured (no 64-game series run here; the laptop script does it).

**Fields.** `Layout2Fields` per biome (`BiomeCatalog.Layout2FieldsFor`): main fields now lie along the far (east) wall of the main, at least 7 cells (Chebyshev) from the start, in two patches (north and south of a 3-row gap), with a main cell worth half a cell elsewhere (`MainShare` 2 against 4; the money per start is unchanged). Hard limits found: the validator wants 2 fields within 14 path cells (so 2 patches, not 1), the layout check wants 100 cells within 30 path cells (so about 100 main cells; the natural yard sits at 25-30+ path cells and adds almost none to that count, and a main of half the cells fails it, 64 cells). The same values apply to all four biomes. Mirror-skeleton maps (Small, 3p, 4p) are unchanged.

Harvest distance (path cells from the start cell to the nearest field cell / mean over the 60 nearest field cells; `map report`, `MapReport.HarvestDistance`; Medium 2p, seeds 1-20, both starts equal):

| Biome | Layout 1 | Layout 2 v5 | Layout 2 v6 |
|---|---|---|---|
| rust_belt | 5.7 / 55.3 | 4.2 / 7.9 | 9.2 / 13.3 |
| mountains | 6.2 / 38.3 | 4.3 / 7.8 | 9.4 / 13.3 |
| ash_plains | 5.5 / 19.6 | 4.2 / 7.8 | 9.2 / 13.3 |
| glass_coast | 6.5 / 67.0 | 4.1 / 7.7 | 9.2 / 13.3 |

Pinned by `Layout2_HarvestDistance_StaysInItsBand` (nearest 7.0-14.0, mean 10.0-24.0, seeds 1-6, every biome). Retries per seed on 20 seeds: rust 3.7, mountains 6.7, ash 3.5, glass 5.3, no seed left on a failing map.

**Income** (tier 3 Authority mirror, Medium 2p, 4 seeds x 2 swaps = 8 games = 16 sides per cell, 12 100 ticks; harvested credits per minute at 3 / 5 / 10 min; the 10-minute figure averages games that reached it, n 10-16). Layout 1 was re-measured with the same script and seeds as the control (it matches the 8-seed table above within noise). 4 seeds are noisy (+-5 % easily).

| Biome | L1 (same 4 seeds) | v5 table (8 seeds) | v6 | v6 vs L1 |
|---|---|---|---|---|
| rust_belt | 3232 / 3686 / 3661 | 3157 / 4211 / 4671 | 3132 / 4041 / 4553 | -3 / +10 / +24 % |
| mountains | 2906 / 3902 / 3913 | 3021 / 4240 / 4760 | 3087 / 3960 / 4661 | +6 / +1 / +19 % |
| ash_plains | 3712 / 4329 / 4529 | 2965 / 4181 / 4564 | 3107 / 3938 / 4100 | -16 / -9 / -9 % |
| glass_coast | 2655 / 2998 / 1937 | 3180 / 4166 / 4337 | 3061 / 3883 / 4492 | +15 / +30 / +132 % |

Finding: moving the fields from 4 to 9 path cells and cutting the main's money share moved the rates by only a few percent. The first harvester earns about 5 500 per minute on both layouts (timeline, seed 1), and the AI puts the refinery within a few cells of the yard, so the trip depends on the nearest field cell, which is bounded by the main's size (13-16 cells east of the start) and the 100-cells check. The only change that matched layout 1 at 5 and 10 minutes (rust_belt 2955 / 3824 / 3736) halved the main's cells, which fails the layout check (64 cells within 30 path cells), so it was not kept. Not within +-10 % at 10 minutes on rust_belt, mountains, glass_coast (glass_coast layout 1 itself falls to 1937; not a fair target). Next levers that need an owner decision: relax the 100-cells check for layout 2 (Q-70 proposal), or cut the money permille again (Q-69 excluded it).

**Mountains skew** (Directorate 30/16, Left Behind 29/13 by start index). Checked over 50 seeds (Medium 2p): the two starts are exact half-turn images in land, height level, overlay, density and ruin features (`Layout2_Mountains_TwoPlayers_StartsAreExactImages`), and `map report` per-start harvest distances are identical on all 50 seeds (Mountains and rust_belt). So plateau mains, ridge river, ramp side, natural side and field distance do not differ between the two starts; the skew is not in the map data. Not fixed, nothing invented: the cause is in how the sim or AI treats slot / start order on this terrain (candidate: harvester and placement tie-breaks, `HarvestMission.FindRich`, or the AI's search order), outside this package. The 64-game laptop series will show whether it reproduces (rust_belt and glass_coast mirrors measured even).

Laptop series: `tools/measure/run_s17_layout2.ps1` (tier-3 mirror per faction plus Expedition v Authority, all four biomes, 32 seeds x 2, `--income 1`, writes `balance_runs\s17l2\s17l2_summary.md`); compare with the layout-1 baseline script `run_s17_layout1_base.ps1`.
