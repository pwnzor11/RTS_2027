# Design 17 · What well-liked community maps look like (OpenRA study)
Package s15-ora (session 15). **Analysis only: no game code changes.** Follows design 16 (maps overhaul) and D-180. The owner sent 15
OpenRA Red Alert community maps that he and real players liked, as inspiration, not to copy. This document turns them into measured
design targets for M12 wave 3 (zones, lanes, chokes, naturals, resources) and waves 2 and 4 (props, decals). All numbers are measured
with `tools/maps/ora/ora_read.py` plus scratch scripts; ranges are *min to max over the maps*, "median" is over the maps. Rules are
starting values (*tune*), each with the range it comes from.

**Legal frame.** Nothing from the maps is in the repo: no `.oramap`, no preview, no converted map, no tileset file (community maps have
their own authors and licences; Red Alert names and tiles are EA's, D-050). The repo gets the reader tool and this document. Map titles
and authors appear only as credit in the references list. Per-map numbers and the renders stay in the helper scratch.

## 0 · Summary
- **Our generator differs most in five things**: 4 to 6 times less blocked terrain on Rust Belt and Ash Plains (3 to 4 % against 10 to 19 %),
  25 to 30 times fewer props (0.03 to 0.07 per 100 cells against 2.0), resource fields 5 to 8 times smaller (8 to 12 cells against a
  median 67), starts in the extreme corner (4 cells from the edge against a median 17 % of the side) and no clumping at all (every prop is
  alone; in the liked maps 80 % of props sit in clusters of 4 or more).
- **Three places where the liked maps contradict our plan** (section 6): they do **not** have a single 4 to 6 cell main ramp (median 3 exits
  3+ wide, largest exit about 35 cells); their enemy-to-enemy path is **not** longer than the straight distance (median ratio 1.00, 13 of 14 under 1.2);
  the natural sits at 18 to 28 path cells, not 14 to 22.
- **They agree with design 16** on: 2 to 4 lanes (median 3), a wide lane about 14 cells, a contested centre (median 4 contested fields),
  about 2 % of cells held by blocking props, almost no empty ground (1.3 % of cells have nothing within 4), exact symmetry not needed.

## 1 · What was studied
15 maps, all **4-player (2v2)**, so every number is about team maps: allied starts are close (path 14 to 48 cells, median 26, one outlier
at 120) and enemy starts are far (median 105). There is no 1v1 or 3-player map in the set, and none has height levels (Red Alert is flat:
cliffs are terrain, there are no ramps). Plateau and ramp rules therefore have **no** support in this data.

| Map | Author | Playable | Tileset | Water share | Notes |
|---|---|---|---|---|---|
| Badger Hills 2v2 | eskimo / sicaria | 96×96 | temperate | 3 % | open field, 200 trees in groves, 16 fields |
| Island Rush | HAWKNADO | 128×128 | temperate | 50 % | four islands joined by diagonal bridges, 242 trees, 92 civ buildings |
| Minor Pressure[AR] | Jopani Kansaro | 96×96 | temperate | 9 % | small, few props (0.4 per 100), ponds, 17 fields |
| Pitfight-extended[AR] | WhoCares | 128×128 | snow | 0 % | tree groves (395), central pit with props and crates |
| Siege | Ajax Frog | 142×142 | temperate | 0 % | fenced bases with 6-wide gates around one huge gem field (outlier) |
| Bad Neighbors | Nuke'm Bro. | 59×62 | temperate | 2 % | tiny, closed starts, central ruin and a little river |
| Bucharest 2v2 | poop | 110×110 | temperate | 0 % | road web, groves, road-side ore fields, diagonal symmetry (0.99) |
| First Come, First Served | Westwood Studios | 84×84 | temperate | 43 % | island with sea around, two causeways, closed starts |
| fn2018-Doubles | Nuke'm Bro. | 108×50 | snow | 31 % | wide and short, sea on one side |
| Kosovo 2v2 | poop | 132×132 | temperate | 0 % | ring road, many roads, ore fields beside roads, symmetry 0.99 |
| Krakow 2v2 | poop | 130×130 | temperate | 0 % | roads, 134 civ buildings and 180 wall cells in towns, symmetry 0.99 |
| Outdoor Trails | CRLF | 160×160 | temperate | 4 % | big, central lake, 549 trees, 23 fields |
| Pitiless | Widow (after eskimo) | 125×125 | temperate | 3 % | ring of ore fields around a centre with craters, rocks and oddities |
| Tournament Lake 2v2 | Hayaji | 128×128 | temperate | 52 % | two islands joined by one causeway, all gems |
| Yugoslavia ER | Luftwaffe | 168×96 | temperate | 22 % | long map, coast, bridges, 36 tech buildings |

### 1.1 How I measured (so anyone can repeat it)
1. `ora_read.py` reads `map.yaml` and `map.bin` (format 1 or 2) and the OpenRA RA tileset files (fetched into scratch from the OpenRA
   repository), maps each template tile's terrain type to a class (clear, road, rough, water, cliff or rock, beach, bridge), and reads
   actors with footprints from the OpenRA rules (trees 2×2 or 3×2, rocks, civ buildings, tech buildings, walls and fences, barrels and
   crates).
2. Passable = clear, road, rough, beach, bridge. **Blocking** = water, cliff or rock terrain, trees, rocks, civilian and tech buildings.
   Walls, fences, barrels and crates are counted but **not** treated as blocking (they are destroyable or crushable in OpenRA).
   Two maps needed a fallback to connect all four starts (Island Rush: bridges are diagonal, civ buildings and trees on the causeways
   were then ignored; it is excluded from all path and lane figures).
3. Paths: 4-connected BFS over passable cells. Lanes: repeated shortest path that prefers open ground, then the lane's corridor (2 cells
   either side) is blocked and the search repeats, as long as the next route is at most 1.4× the first ("strict": 6 cells blocked, 1.4×).
   Lane width is twice the clearance to the nearest blocker, capped at 19. The vertex min-cut is between 12-cell discs around two enemy
   starts. These are **estimates**; for corner starts the disc clips the map edge, so min-cut is not comparable between the two sets.
4. Resource field = cluster of resource cells within 2 cells of each other. "Natural" = 15 to 30 path cells from the nearest start and at
   least 1.6× further from the nearest enemy; "main" = 14 or fewer; "contested" = nearly equal distance (under 1.35×) or over 45 cells.
5. Our maps: 4 generated per biome at Medium (96) and Large (128), 4 players, seeds 1 to 4 (32 maps; the tables use all of them), by
   `dotnet run --project src/Tools -- map gen --biome <b> --size m|l --players 4 --seed <n>`, converted to the same grid in scratch.
   Neutral ruins count as 3×2 civ buildings. Previews (own colours, not RA art): contact sheet of the 15 maps and of 16 of ours, in
   the helper scratch (not committed).

## 2 · Measured comparison (median, with min to max in brackets)
| Metric | OpenRA 15 maps | Ours Rust Belt | Glass Coast | Mountains | Ash Plains |
|---|---|---|---|---|---|
| Playable cells | 16128 (3658 to 25600) | 12800 (9216 to 16384) | 12800 (9216 to 16384) | 12800 (9216 to 16384) | 12800 (9216 to 16384) |
| Path between nearest enemy pair, median over pairs (cells) | 105 (56 to 196) | 156 (128 to 182) | 128 (104 to 152) | 168 (128 to 210) | 157 (128 to 186) |
| Path / Manhattan, nearest enemy pair | 1.00 (1.00 to 1.27) | 1.07 (1.03 to 1.12) | 1.01 (1.00 to 1.07) | 1.11 (1.04 to 1.35) | 1.10 (1.05 to 1.12) |
| Lanes between enemy starts sharing under 30 % (strict search) | 3 (1 to 5) | 2 (1 to 3) | 3 (3 to 5) | 2 (1 to 3) | 2 (2 to 3) |
| Median width along the widest lane (cells, search caps at 19) | 14 (1 to 19) | 19 (17 to 19) | 19 (11 to 19) | 9 (5 to 14) | 19 (17 to 19) |
| Narrowest point of the narrowest lane (cells) | 3 (1 to 7) | 1 (1 to 3) | 1 (1 to 2) | 1 (1 to 5) | 1 (1 to 3) |
| Vertex min-cut between start discs (cells) | 29 (6 to 71) | 10 (10 to 14) | 34 (28 to 35) | 14 (4 to 20) | 10 (4 to 10) |
| Open share of a 14-cell walk ring around a start | 0.64 (0.27 to 0.92) | 0.35 (0.33 to 0.37) | 0.34 (0.33 to 0.38) | 0.31 (0.09 to 0.36) | 0.35 (0.33 to 0.37) |
| Exits 3+ wide at 14 steps from a start | 3.0 (1.0 to 6.0) | 1.0 (1.0 to 3.0) | 1.5 (1.0 to 3.0) | 2.0 (2.0 to 3.0) | 1.0 (1.0 to 2.0) |
| Largest exit at 14 steps (cells) | 36 (8 to 103) | 38 (13 to 41) | 34 (19 to 42) | 22 (5 to 31) | 37 (19 to 41) |
| Clear radius around a start before the first blocker (cells) | 6.5 (3.0 to 11.5) | 13.5 (9.0 to 25.0) | 5.0 (5.0 to 5.0) | 8.0 (8.0 to 9.0) | 13.0 (8.0 to 25.0) |
| Resource fields (clusters) | 14 (4 to 23) | 20 (16 to 20) | 12 (8 to 12) | 16 (8 to 16) | 24 (12 to 32) |
| Fields per start inside 14 path cells | 1.00 (0.75 to 2.00) | 2.00 (1.00 to 2.00) | 1.00 (1.00 to 2.00) | 2.00 (1.00 to 2.00) | 2.00 (1.00 to 3.00) |
| Natural fields per start (15 to 30 path, 1.6x further for enemy) | 0.25 (0.00 to 1.25) | 1.00 (0.00 to 2.00) | 0.00 (0.00 to 1.00) | 0.00 (0.00 to 1.00) | 0.50 (0.00 to 3.00) |
| Contested fields (map total) | 4 (0 to 13) | 6 (0 to 12) | 0 (0 to 4) | 4 (4 to 8) | 10 (4 to 16) |
| Path to own main field (cells) | 9 (5 to 14) | 7 (5 to 8) | 6 (5 to 10) | 7 (5 to 8) | 6 (5 to 10) |
| Path to natural (cells) | 22 (18 to 28) | 27 (24 to 29) | 26 (24 to 30) | 30 (30 to 30) | 25 (23 to 27) |
| Enemy / owner path ratio, natural | 3.4 (2.5 to 7.3) | 3.3 (2.7 to 5.2) | 2.5 (2.2 to 2.8) | 4.0 (3.0 to 5.0) | 4.3 (3.2 to 5.0) |
| Path to contested field (cells) | 59 (17 to 81) | 58 (43 to 61) | 56 (56 to 56) | 60 (46 to 110) | 56 (48 to 64) |
| Median field size (cells) | 67 (20 to 188) | 8 (5 to 10) | 12 (5 to 18) | 10 (8 to 20) | 11 (4 to 19) |
| Resource cells per start inside 30 path | 141 (32 to 218) | 28 (20 to 39) | 22 (11 to 33) | 26 (18 to 34) | 38 (22 to 60) |
| Resource cells per 100 map cells | 6.8 (4.0 to 28.4) | 1.3 (0.9 to 2.3) | 0.9 (0.6 to 1.5) | 1.5 (1.0 to 1.7) | 2.1 (1.5 to 4.0) |
| Gem share of resource cells | 0.00 (0.00 to 1.00) | 0.17 (0.13 to 0.20) | 0.25 (0.14 to 0.33) | 0.17 (0.09 to 0.20) | 0.11 (0.09 to 0.15) |
| Impassable share (terrain + blocking props) | 0.172 (0.102 to 0.664) | 0.027 (0.024 to 0.033) | 0.285 (0.269 to 0.312) | 0.112 (0.091 to 0.150) | 0.037 (0.032 to 0.044) |
| Water share | 0.030 (0.000 to 0.519) | 0.017 (0.014 to 0.019) | 0.273 (0.256 to 0.300) | 0.009 (0.009 to 0.013) | 0.024 (0.019 to 0.029) |
| Cliff or rock share | 0.081 (0.053 to 0.166) | 0.007 (0.006 to 0.009) | 0.011 (0.011 to 0.013) | 0.102 (0.083 to 0.141) | 0.012 (0.010 to 0.013) |
| Road share | 0.032 (0.000 to 0.100) | 0.166 (0.156 to 0.177) | 0.000 (0.000 to 0.000) | 0.000 (0.000 to 0.000) | 0.002 (0.002 to 0.004) |
| Props per 100 cells (trees, rocks, civ, tech, walls, small) | 2.00 (0.37 to 3.59) | 0.07 (0.05 to 0.09) | 0.00 (0.00 to 0.00) | 0.00 (0.00 to 0.00) | 0.03 (0.02 to 0.04) |
| Trees per 100 cells | 1.41 (0.00 to 2.18) | 0.00 (0.00 to 0.00) | 0.00 (0.00 to 0.00) | 0.00 (0.00 to 0.00) | 0.00 (0.00 to 0.00) |
| Civ buildings per 100 cells | 0.15 (0.00 to 0.79) | 0.07 (0.05 to 0.09) | 0.00 (0.00 to 0.00) | 0.00 (0.00 to 0.00) | 0.03 (0.02 to 0.04) |
| Wall or fence cells per 100 | 0.21 (0.00 to 2.05) | 0.00 (0.00 to 0.00) | 0.00 (0.00 to 0.00) | 0.00 (0.00 to 0.00) | 0.00 (0.00 to 0.00) |
| Median props per cluster (within 1 cell) | 2.0 (1.0 to 12.0) | 1.0 (1.0 to 1.0) | 0.0 (0.0 to 0.0) | 0.0 (0.0 to 0.0) | 1.0 (1.0 to 1.0) |
| Share of props in clusters of 4+ | 0.80 (0.00 to 0.97) | 0.00 (0.00 to 0.00) | 0.00 (0.00 to 0.00) | 0.00 (0.00 to 0.00) | 0.00 (0.00 to 0.00) |
| Cells with nothing within 4 | 0.013 (0.000 to 0.099) | 0.028 (0.013 to 0.034) | 0.001 (0.000 to 0.003) | 0.057 (0.024 to 0.079) | 0.212 (0.110 to 0.300) |
| Cells with nothing within 7 | 0.000 (0.000 to 0.032) | 0.000 (0.000 to 0.000) | 0.000 (0.000 to 0.000) | 0.001 (0.000 to 0.006) | 0.025 (0.003 to 0.062) |
| Best symmetry agreement | 0.86 (0.57 to 0.99) | 1.00 (1.00 to 1.00) | 1.00 (1.00 to 1.00) | 1.00 (1.00 to 1.00) | 1.00 (1.00 to 1.00) |

Reading notes: our column is 8 maps each at 96 and 128 (area 9216 to 16384); the OpenRA maps are larger on average (median 128×128).
Glass Coast's 28 % blocked share is the ocean ring, not obstacles. Our `props_per100` counts only neutral ruins (we have no other
props). Resource cell counts compare **shape and size**, not income: OpenRA ore cell value, density scale and the "mine" actors that keep
seeding ore are different from ours, so the balance helper must convert (see R14).

## 3 · What the liked maps have in common
1. **Direct routes.** Enemy path is about the straight (Manhattan) distance (ratio 1.00 median, 13 of 14 under 1.2) and equals 0.87× the
   playable side (0.51 to 1.20). The interest comes from lanes and obstacles on the way, not from a long detour.
2. **Several lanes, mixed widths.** 3 lanes between enemy starts (1 to 5); the widest is about 14 cells wide, at least one lane is 3 or
   wider at its narrowest in 13 of 14 maps, and single-file gaps (width 1 to 2) between tree lines exist on 6 of 14.
3. **Starts are open with a soft shell, not walled.** Of the cells on a 14-step ring around a start, 64 % are reachable (27 to 92 %);
   median 3 exits 3+ wide and the largest is about 35 cells. Only 5 maps are enclosed (27 to 48 %): First Come First Served (five exits
   3 to 8 wide), Bad Neighbors (exits 3 to 5 plus one 15 to 36), Yugoslavia, Minor Pressure, fn2018. Siege is the closest to a walled main:
   fenced bases with two 6-wide gates (and one fence-free side). Nobody has one ramp.
4. **Starts are not in corners.** Distance from start to the playable edge is 17 % of the side (6 to 32 %); the clear radius around a
   start before the first blocker is 6.5 cells (3 to 11.5); trees almost never grow within 12 cells of a start (1.8 % of trees against
   13.5 % of the area).
5. **Economy comes in three tiers.** Main field at 9 path cells (5 to 14) and 92 cells large; a natural at 22 cells (18 to 28) in 8 of 14
   maps, about 42 cells large, enemy 3.4× as far (2.5 to 7.3); contested fields (median 4, 0 to 13) at about 59 path cells (17 to 81).
   Median 141 resource cells (32 to 218) within 30 path cells of each start. Gems are rare: 0 % of resource cells in the median map,
   present in 7 of 15 maps, under 15 % in five of them (Island Rush 25 %, Tournament Lake all gems).
6. **Dense, clumped decoration.** 2.0 props per 100 cells (0.37 to 3.59); trees are 70 % of them (1.4 per 100). Median cluster
   2 props, 90th percentile 9, and 80 % of props sit in clusters of 4 or more; only 9 % are alone. Blocking props cover 2.2 % of cells
   (0 to 5.9 %) on top of 15 % blocked terrain.
7. **Props hug edges, avoid roads.** Within 3 cells of a cliff or rock edge there are 55 % of the trees but 35 % of the area (1.6×); within
   5 cells of the map edge 32 % against 15 % (2.1×); near water 11 % against 6 % (1.7×); near roads 7 % against 21 % (0.35×).
8. **Very little dead ground.** Cells with nothing (no road, rough, water, prop, resource, start) within 4 cells: 1.3 % (0 to 10 %);
   within 7 cells: 0 % (0 to 3 %).
9. **Terrain mix.** Blocked terrain 15 % (5 to 65 %; 10 to 19 % on the ten land maps), cliff or rock 8 % (5 to 17 %), road 3 % (0 to 10 %),
   rough 1 %. Water maps (5 of 15) are islands with one to four crossings (bridges or causeways, 4 to 10 bridge pieces).
10. **Symmetry is loose.** Best match 0.86 (0.57 to 0.99): five rot2, five diagonal, four mirror, one rot4. The three poop maps (Bucharest,
    Kosovo, Krakow) are nearly exact (0.99); the others have deliberate differences, and still play fairly. Exact symmetry (ours) is stricter
    than needed, and fine.

## 4 · Where our generator differs most (4-player maps)
| # | Difference | Ours | Liked maps | Effect |
|---|---|---|---|---|
| X1 | Blocked terrain | Rust Belt 2.7 %, Ash Plains 3.7 % (Mountains 11 %) | 10 to 19 % on land maps | nothing to funnel armies; the generator's biggest gap |
| X2 | Props | 0.03 to 0.07 per 100 (only ruins), all singletons | 2.0 per 100, 80 % in clusters of 4+ | bare ground (design 16 L9) |
| X3 | Field size | median 8 to 12 cells, main field 10 to 15 | median 67, main 92 | fields read as stamps (L6); economy 5 to 8× smaller in cells |
| X4 | Start position | corner, 4 cells in; allied starts at far corners | 17 % in; allies 14 to 48 path cells apart | no room behind the base, no team play |
| X5 | Roads | Rust Belt 17 % of cells in a 16-pitch grid | 3 % (0 to 10 %), roads link places | roads as chessboard (L2) |
| X6 | Empty ground | Ash Plains 21 % of cells with nothing within 4 (2.5 % within 7); Mountains 5.7 % | 1.3 % (and 0 % within 7) | confirms L4 |
| X7 | Lanes | 1 lane (median) on Rust Belt, Mountains, Ash; Glass Coast 2 to 3 | 3 (1 to 5), widths 14 / 3 to 9 / 1 | confirms L1 |
| X8 | Enemy path | 1.01 to 1.11 × straight, 128 to 168 cells | 1.00 ×, 105 cells (0.87 × side) | ours is already as direct as theirs, but longer |
| X9 | Symmetry | 1.00 (exact) | 0.86 (0.57 to 0.99) | keep ours (fairness by construction) |
| X10 | Natural | Rust Belt 1 per start (inside the open), Glass Coast, Mountains 0 | 0.25 per start (8 of 14 maps) at 18 to 28, protected | confirms L5; but liked maps do not always have one either |

## 5 · Concrete targets for M12 (each with its source range)
Section numbers refer to design 16. "Range" is the min to max over the liked maps (unless marked), "target" is what I suggest *(tune)*.

### 5.1 Zones and start (wave 3)
- **R1 Start position.** Place each start 12 to 25 % of the side from the nearest edge (liked: median 17 %, 6 to 32 %); in 4-player maps
  put allied starts 14 to 48 path cells apart (liked: 14 to 48, median 26) if teams are a goal, otherwise at rot4 corners as now.
- **R2 Start surroundings.** Clear radius 5 to 8 cells (liked: 3 to 11.5, median 6.5); no trees or props within 8 to 12 cells except at
  the cliff edge (liked: 1.8 % of trees within 12).
- **R3 Main entrances.** 2 or 3 openings of width 3 or more within 14 steps of a start, the widest 20 to 40 cells (liked: 3 exits, 1 to 6;
  largest 35, 8 to 103) when the start is open; **if the owner keeps D3 (walled main), take Siege as the model: 2 gates 6 wide, and a
  third closed by a fence or a destroyable wall**. Frontage on a 14-step ring: 30 to 100 cells (median 72).
- **R4 Natural.** 1 field of 30 to 80 cells at 18 to 28 path cells from the start, enemy at least 2.5× as far (liked: 18 to 28, 2.5 to 7.3);
  accessible by the same path as the main exit; require it in about half of the maps, not all.
- **R5 Contested.** 2 to 6 contested fields (median 4, 0 to 13) at 45 to 80 path cells from every start (median 59, 17 to 81), of 20 to 70 cells;
  at most one rich central field.

### 5.2 Lanes and chokes (wave 3)
- **R6 Lane count.** 3 lanes that share under 30 % of cells (liked: 3, 1 to 5; median 3 for the strict search). Never fewer than 2.
- **R7 Lane widths.** One wide lane (median width 12 to 16; liked 14), one medium (5 to 9), one narrow (3 to 5, vehicle choke). One more
  1 to 2 wide infantry gap is acceptable (liked: on 6 of 14 maps). At least a 3-wide lane on every map (liked: 13 of 14).
- **R8 Route length.** Enemy start-to-start path 0.8 to 1.2 × the playable side (liked: 0.51 to 1.20, median 0.87) and 1.0 to 1.15 × straight.
  **Do not require 1.2× (design 16 §3.4):** 13 of 14 liked maps are below 1.2 (see C2).
- **R9 Obstacle share.** 10 to 19 % blocked cells on land maps (terrain plus props), of which cliff or rock 5 to 12 %, blocking props 1 to 4 %, water
  0 to 5 %. Water maps: 30 to 50 % water, 1 to 4 crossings.
- **R10 Roads.** At most 10 % of cells (liked: 0 to 10 %, median 3 %); roads connect a start, a natural, a field and a town; ore fields sit beside roads
  in Bucharest, Kosovo and Krakow.

### 5.3 Resources (wave 3)
- **R11 Field shape and size.** Main 60 to 130 cells (liked median 92, 15 to 262), natural 30 to 80 (median 42, 17 to 81), contested 20 to 70; not diamond
  stamps: elongated or crescent shapes along a road or cliff (Pitfight, Pitiless, Kosovo).
- **R12 Per start.** 100 to 220 resource cells within 30 path cells (liked median 141, 32 to 218); 3 to 4 fields per start counting the shared ones
  (liked: 14 fields median for 4 starts, 4 to 23).
- **R13 Gems.** At most 15 % of resource cells, none in about half the maps; (liked: 0 % median; present in 7 of 15 maps, 5 to 15 % in five, 25 % and 100 % in two).
- **R14 Income.** All of the above are cell counts. Resource value per cell differs between OpenRA and our game, so the balance helper sets the
  money per start; keep the ratio main : natural : contested cells at about 2 : 1 : 1.

### 5.4 Props and decoration (waves 2 and 4)
- **R15 Density.** 1.5 to 3 blocking props per 100 cells overall (liked: 2.0, 0.37 to 3.59), 70 % trees (dead trees in our setting), 5 to 10 %
  civil/ruin buildings (liked 0.15 per 100, 0 to 0.8), rest wrecks, barrels and rocks. Blocking props cover 1 to 4 % of cells (liked: 2.2 %, 0 to 5.9 %),
  consistent with design 16 §2.5 (2 to 4 %). Large maps (160+) work at 2 per 100 too.
- **R16 Clusters.** Groves of 4 to 12, cluster 90th percentile 9 and up to 48 for a forest (liked: median map 2, p90 8.8; 80 % of props in clusters
  of 4 or more, 9 % alone). Clusters are cells within 1 of each other; leave 1-cell gaps for infantry.
- **R17 Placement bias.** Over-represent props near cliff or rock edges (1.6× the area share), near the playable edge (2×), near water (1.7×);
  under-represent near roads (0.35×) and within 12 cells of a start (0.13×).
- **R18 Buildings.** Civil or ruin buildings in groups of 3 to 12 (Krakow: 134 buildings, 180 wall cells, at the towns) with walls or barrels around;
  tech buildings 0.03 per 100 (0 to 0.3), at the contested spots.
- **R19 Empty ground.** At most 1.5 % of cells with nothing within 4 (liked: 1.3 %, 0 to 10 %) and none with nothing within 7 except a declared plains
  budget of 3 % (liked: 0 to 3 %).
- **R20 Symmetry.** Keep exact symmetry for fairness; 0.86 median in the liked maps shows that visual variety (decals, small props) may break it.

## 6 · Contradictions with D-180 and design 16 (for the lead to raise with the owner)
- **C1 · D3 "walled main with one 4 to 6 cell ramp".** Not found. None of the 15 liked maps has a single ramp. Median 3 exits of width 3+, the widest 35;
  5 of 15 are enclosed (five exits 3 to 8 wide in First Come First Served). The closest is Siege (two 6-wide gates plus fences). Caveat: the data is
  flat (no height, no ramps), so a plateau main is untested here. *Suggest:* keep the walled main as an owner choice but allow 2 gates of 4 to 6 (like
  Siege) and test the AI, or relax to "2 to 3 exits".
- **C2 · design 16 §3.4 "main-to-main path at least 1.2× the straight distance".** Liked maps: 1.00 (1.00 to 1.27); only Island Rush (1.64, islands) and fn2018
  (1.27) exceed 1.2 (Island Rush is excluded from path figures). A 1.2 floor would reject 13 of 14. *Suggest* 1.0 and a minimum absolute path (about 0.5 × side, liked 0.51).
- **C3 · design 16 §3.2 natural "path of 14 to 22 cells".** Liked 18 to 28 (median 22). Our 14 is below the range.
- **C4 · design 16 §3.2 "natural entrance 8 to 10 wide" and "enemy 1.6× as far".** Fine for the second (liked 2.5 to 7.3 is stronger). Entrance width not measured.
- **C5 · D3 "natural expansion" always.** Only 8 of 14 liked maps have a natural by this definition (0 to 1.25 per start). Not required by the evidence.
- **C6 · design 16 §3.3 Rust Belt "two wide arterials ... side streets"** and the current 17 % road share: liked road share 0 to 10 %.
- **C7 · Team play.** All 15 are 2v2; D-180 says nothing about team spacing. If 2v2 matters to the owner, rot4 corner starts (allies far apart) is the opposite
  of every liked map (allies 14 to 48 apart). *Question for the owner.*
- **C8 · Plateaus (L3, M12-09).** Not supported by this data (no height); no contradiction, no evidence.

## 7 · Limits of the study
- 15 maps, one genre, all 4-player; chosen by one owner. They are inspiration, not a statistical sample; ranges are wide.
- Lane width and min-cut are estimates (search settings above). Single-file gaps between trees count as lanes of width 1.
- Resource figures are cells; value per cell not compared. OpenRA "mine" actors (22 per map in the median map) keep seeding ore; they are not counted.
- Walls and fences are not blocking in my pass; Siege's fences make its bases much more closed than the numbers show.
- Props of our game do not exist yet, so our props column is ruins only.

## 8 · References (credit; maps are not included in this repository)
OpenRA community maps, Red Alert mod, analysed for measurement only. Titles and authors as in each map's `map.yaml`:
Badger Hills 2v2 (eskimo, sicaria); Island Rush (HAWKNADO); Minor Pressure[AR] (Jopani Kansaro); Pitfight-extended[AR] (WhoCares); Siege (Ajax Frog);
Bad Neighbors (Nuke'm Bro.); Bucharest 2v2 (poop); First Come, First Served (Westwood Studios); fn2018-Doubles (Nuke'm Bro.); Kosovo 2v2 (poop);
Krakow 2v2 (poop); Outdoor Trails (CRLF); Pitiless (Widow, based on eskimo's map); Tournament Lake 2v2 (Hayaji); Yugoslavia ER (Luftwaffe).
File formats and terrain classes were read from the public OpenRA project (GPL v3): `Map.cs` (binary header), `mods/ra/tilesets/*.yaml` and
`mods/ra/rules/*.yaml`. No code was copied; the reader is our own (`tools/maps/ora/ora_read.py`, see its README).
