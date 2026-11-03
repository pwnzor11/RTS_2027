# How RTS Maps Are Made

**Summary**

- Most classic RTS maps are grids of cells. Each cell holds a terrain type and a tile variant. Passability (can a unit walk here) is usually a separate layer from what the tile looks like, and the reference games that got this right kept the two apart.
- Autotiling (choosing the right edge or corner tile from neighbours) removes most hand-placement work. A 16-tile dual-grid set handles corners cheaply. A 47-tile blob set looks better and costs more art.
- Elevation is the hard part. 2D C&C-style games fake height with discrete cliff levels and ramps, and the cliff pieces are where most editor effort goes. Tiled-style editors do not handle this natively.
- Editors matter less than the file format. Keep tile identity by name, not by number, because tile IDs change between tilesets (OpenRA's conversion of C&C maps had to remap IDs by hand).
- Competitive RTS map design is mostly measurement: rush distance in seconds of walking, symmetry checks in both directions, resource counts per base, and playtests. Good rules of thumb exist, but they come from forum posts and need our own numbers.
- AoE2's random map scripts show that generated maps need explicit fairness code. The language had no tools for it, and the most popular map still has balance complaints. Procedural generation is a later feature, not a v1 one.

## 1. Tile systems and autotiling

**Square top-down grids** are what Tiberian Dawn and Red Alert use, and what most 2D RTS games copy. Each cell has a fixed size and a terrain type. Units move between cell centres or sub-cell positions. The cost is simplicity: pathing, fog and occupancy are all array lookups.

**Isometric diamond tiles** (Tiberian Sun, Red Alert 2, Age of Empires) look better at a glance, but a diamond tile covers a rhombus of the grid. Height is drawn as vertical offset, so cliffs become a visual problem as much as a pathing one. The editor docs for FA2 and Final Sun reflect this: their feature lists are dominated by cliff and shore drawing, not terrain painting. (Source: CnC resource docs, core and modern editors.)

**Hex grids** avoid square-grid diagonal distortion but are rare in real-time games. No source I read recommends them for a sprite RTS.

**Free-form heightmaps** (Supreme Commander and, in a restricted form, StarCraft II) store a continuous height per vertex. SC2 is not a pure heightmap: its editor offers discrete cliff levels, with cliff brushes that lower or raise terrain in steps. Bridges can only be placed on cliff level 0. (Source: SC2Mapster Terrain Editor wiki.) Heightmaps look good, but pathing from a continuous surface is harder to make both fast and predictable.

**Autotiling** is the technique that makes these grids pleasant to paint. Redblob Games describes the main approaches:

| Technique | Tiles needed | Corners handled | Art cost |
|---|---|---|---|
| 4-bit neighbour bitmask | 16 | No | Low |
| 8-neighbour blob (47 shapes) | 47 | Yes | Medium |
| Dual-grid (display tiles sit between data cells) | 16 | Yes | Low |
| Quarter-tile compositing | 5 unique shapes | Yes | Low |

The 4-bit method checks north, east, south and west, giving 16 combinations. The 8-neighbour method adds diagonals, but a diagonal only counts when both adjacent cardinals are present, which reduces 256 raw values to 47. The dual-grid method is the cheapest way to get clean corners. It is the approach I would start from. (Source: Redblob Games autotile article.)

**Wang tiles** store a colour on each edge or corner, and neighbours must match. Tiled's terrain sets are the practical form of this. In Tiled 1.5 a Corner set needs 16 tiles for two terrains, an Edge set needs 16, and a Mixed set needs 256 for a full set, though reduced sets such as the 47-tile Blob work. Rotation and flipping can cut the blob set to 15 tiles. Limits: terrains only interact inside one terrain set, and all tiles must come from one tileset. (Source: Tiled terrain manual.)

**Passability and land types** should not come from the art. Tiberian Dawn-style games tie terrain type to the tile, so a tile looks like water and blocks like water. That is fine until a river needs a bridge, or a road looks like flat ground but should be passable with a speed bonus. The SC2 editor makes this explicit: roads are visual only, and pathing is painted on a separate layer. Units cannot walk under bridges unless hidden walkable models are added. (Source: SC2Mapster Terrain Editor wiki.)

**Cliffs, ramps and water** in practice:
- Cliffs are placed as edge pieces between levels. Ramps are allowed only on cliff edges, not flat ground. A helipad with no ramp is inaccessible. (Source: SC2 Terrain Module tutorial.)
- The SC2 Height brush raises terrain without cliffs. Cliff brushes make steps. The docs do not give cliff thickness or the number of height levels.
- Water levels are changed by triggers in SC2, not by the terrain. Rivers and bridges are described by the CnCNet forum as hard to use well; the advice is to avoid them for beginners. (Source: CnCNet "Understanding Map Design".)
- Terrain effects on speed were dropped in Tiberian Sun, partly because of balance risk. Designers said they "didn't build maps with terrain modifying unit speeds in mind." (Source: Well Played article on Tiberian Sun.)

## 2. Map editors

**Westwood's original tools** are undocumented in the sources I reached. This is a gap.

**Community editors for the C&C games** (from CnC resource docs):
- **FA2** (Final Alert 2) is the original RA2/Yuri's Revenge editor. **FA2SP** patches it with a tile manager and Ares/Phobos support. **FA2HDM** is a 2025/6 fork with terrain generation, connected cliff and shore drawing, and a DirectX/OpenGL renderer.
- **Final Sun** is the original Tiberian Sun editor. A patched version exists.
- **World Altering Editor (WAE)** is an open-source replacement by Rampastring, supporting TS and RA2. It has a terrain generator, a connected tile drawer for cliffs, shores and roads, a pathfinding check tool, and INI-driven configuration. It is the most modern open option in the docs, but it is written for those games' formats.
- **Relert Sharp** is closed source and slow; **Relert++** is an incomplete C++ rewrite.

**OpenRA's in-game editor** is the closest existing example to what we want. It opens from Extras, supports creating or editing maps by drag and drop, and saves to `.oramap`. The wiki does not describe its brushes or placement tools in detail. Imports of original C&C maps use the command-line utility (`cnc --import-td-map`, `ra --import-ra-map`). (Source: OpenRA Mapping wiki.)

**Tiled** is a general 2D tile editor. It has terrain and Wang sets, custom properties on maps, layers and objects, and object layers for spawns and triggers. It exports JSON and TMX. It has no concept of units, ownership, resources or a playtest button, so an RTS would need custom properties to carry those. (Sources: Tiled terrain manual and Tiled JSON reference.)

**LDtk** is a level editor with IntGrid layers, rule-based auto-layers and typed entity fields, exporting JSON. Its rule details were not verified.

**What a good RTS editor needs**, from the sources and general practice:
- Terrain brushes with autotile, plus a bucket fill and a stamp brush.
- Separate layers for visual terrain, passability and height, each editable on its own.
- Resource painting that sets counts (for example, a field of a set size), not just tiles.
- Unit and structure placement with owner, facing and health, with validation against the rule set.
- Player slots: colour, faction, start position, team and allies.
- Triggers and waypoints for campaign missions.
- A minimap preview and a playtest button that launches the map at once.
- Symmetry tools: mirror and rotate, with a check that both sides match.
- A cell-distance or walk-time readout between chosen points, because rush distance is the main balance number.

## 3. Map file formats

**OpenRA `.oramap`** is a ZIP (or an unpacked folder) containing `map.yaml`, `map.bin`, and `map.png`. Format version 11 is current. `map.yaml` has metadata (`RequiresMod`, `Tileset`, `MapSize`, `Bounds`), a `Players` list and an `Actors` list with owners and positions. Bounds need a one-cell cordon on each side. The `map.bin` file holds tiles, height and resources. The conversion guide says each cell is three bytes and the first byte is the tileset ID; it does not give the other two bytes. Map boundaries must be divisible by 8, and very large maps degrade performance. (Sources: OpenRA map-format wiki and Mapping wiki.)

**Tile IDs are tileset-specific.** The OpenRA conversion guide shows this clearly. Converting a Temperate C&C map needed offsets: water IDs moved from 136–148 to 112–124 (−24), roads from 93–135 to 173–215 (+80), and rocks from 13–50 to 135–172 (+122). The guide admits that even after remapping, water and roads only "largely" display correctly. The lesson for us: store tile names or stable IDs per tileset, and keep the tileset file as the authority.

**Tiled JSON** is easy to read and diff. A map has `width`, `height`, `tilewidth`, `tileheight`, `orientation`, and `layers`. Tile layers store a flat `data` array of tile IDs. Object groups hold spawns and triggers. Custom properties are string key-value pairs on maps, layers, objects and tiles. Tiled 1.5 added Wang sets, and the reference notes Wang sets since 1.1.5. (Source: Tiled JSON format reference.)

**Spring SMF** (heightmap, typemap, and separate tile graphics) was not verified: the Spring wiki blocked access. I cannot say from sources how it stores metal spots or features.

**Versioning** is the other lesson from OpenRA. Before `playtest-20160403`, maps were upgraded silently on load. After that change, an upgrade must be run explicitly, and an upgrade rules pass runs with it. Our format should carry a version number from day one and have an explicit upgrade command.

**Preview images** should be generated from map data. OpenRA does this unless `LockPreview: True` is set.

## 4. Map design principles

**Competitive skirmish maps**

*Symmetry.* Both common symmetries (mirror and rotation) work, but both need checking in every direction. A CnCNet forum post found that units firing north got a +1 range bonus in their layout, and rotating the map 90 degrees removed the bias. A TL.net SC2 analysis measured about a 12% difference on Delta Quadrant between clockwise and anticlockwise main-to-natural routes. Symmetry does not guarantee balance, but it gives a baseline to measure against.

*Start positions and bases.* The CnCNet advice: keep the construction yard "as far back as it can be (within the 'safe' starting area) without stopping the build flow," and count cells, because a one-cell offset can stop a factory reaching the next expansion.

*Resources.* Scarcity is the lever. A forum rule of thumb says about one refinery per 35 cells of tiberium; this is a user's comment, not a measured rule. Split large fields into smaller ones so harvesters travel similar distances. Fields with cliffs on most sides are easier to defend. SC2 guidance on bases is more concrete: standard bases have 8 mineral patches, each saturates with 3 workers, and straight walls of patches let only two workers reach the front, so patches should be spread.

*Chokepoints and ramps.* SC2 guidance: main ramps should be wide enough that both attacker and defender can fight. Avoid a high-ground ramp that is too narrow to attack through. Expansions need room to defend from several directions. Cliffs that block line of sight at chokes make defence harder.

*Routes.* Avoid square, circle and X layouts. They look empty and make the path to the enemy obvious. Give several routes: a main path, a back path and a contested middle. Place expansions along these routes.

*Rush distance.* This is the main balance number in competitive maps. The TL analyses measured worker walking time from ramp to ramp, in seconds at SCV speed. Averages rose from about 37.9 worker-seconds in early 2011 to about 47.9 in mid-2012. Each extra 10 worker-seconds adds about 12.5 seconds for a marine to cross. Modern 1v1 maps range from about 30 (Steppes of War) to 56 (Antiga Shipyards cross). The same map can differ a lot between spawns: Tal'darim Altar is 44 close and 54 cross. Destructible rocks shift distance by 3–6 seconds in one example. Watch towers are used to offset short distances, and some maps omit them to make attacks harder to see.

*Expansions and gating.* TL's Daedalus Point post says a third base should be "somewhat closed off" so it can be defended, and that a natural must be walled for a fast expand. Community designers use gates (rocks, chokes) to decide when an attack can reach a base.

*Playtesting.* The Daedalus post argues that extreme maps need time in team leagues before individual premier events, and that a map should not be judged early. For a hobby project this means: test with at least a few different players, and record rush times in a table.

**Campaign and mission maps**

Campaign maps are asymmetric, scripted and paced. The sources cover this less well than skirmish. The clearest guidance is about terrain as a tool in the story. Tiberian Sun designers said "If you are at a disadvantage, use the terrain – burn the forest down, shoot down the bridge." That is a design promise: the player should be able to change the map, and the map should respond. For reveal pacing and set-piece scripting, the OpenRA mission rules are a concrete reference: a mission needs `Visibility: MissionSelector`, a `Categories: Campaign` entry, a `MissionData` trait, and a mission group in the mod's mission file. Triggers in SC2 set water state and other scripted changes. I did not find a source on Westwood's own mission-making process.

## 5. Procedural generation

AoE2's random map scripting (RMS) is the best-known example in our genre. The built-in language lacks user-defined functions, modules, loops and robust conditionals, so map makers wrote external generators in spreadsheets and Octave. The `aoe2-rms` Racket DSL exists to fix those gaps by transpiling to RMS. (Source: aoe2-rms Motivation page.)

Fairness is the hard part. The Arabia forum thread says RMS was never designed for fair forest distribution and has no tool for it. Developers used "many steps of placing and replacing placeholder terrains" to spread woodlines, and the workaround is error-prone. Players still get games with no woodlines, and the map is widely called buggy. (Source: AoE2 forum Arabia thread.)

The HyperRandom community map is a more modern approach. It varies many parameters but keeps "the same variation for each player." Its `HyperRandomFair` preset mirrors land features across corners, and `HyperRandomLight` turns off most randomisation for competitive play. (Source: Liquipedia HyperRandom.)

**Worth it for us?** Not for v1. Full generation needs a map language, fairness checks based on rush distance, and much playtesting. Hand-made maps with seeded decoration and resource placement give variety without the fairness problem.

## 6. Minimap, previews, size and performance

- **Size.** OpenRA maps need boundaries divisible by 8 and an unplayable cordon. Size is not capped, but very large maps hurt performance. The SC2 terrain tutorial used a 112x112 map. (Classic C&C map sizes were not verified.)
- **Preview.** Generate the minimap from the map data, not from a screenshot. OpenRA generates `map.png` automatically, and mission previews are 360x200.
- **Performance.** Keep passability, fog and occupancy as flat arrays. Do not recompute autotile bitmasks every frame; they change only when a cell or neighbour changes, so cache them. (Redblob Games notes the caching point.)
- **Map size in rush terms.** Use walking time, not tile count. Metropolis feels large but has a short cross distance (44 worker-seconds), so measure the distance you want.

## Implications for RTS_2027

- **Recommendation: square top-down cells as the simulation grid.** It matches the C&C feel and keeps pathing and fog as simple array operations. Render with autotiled sprites on top of that grid.
- **Recommendation: discrete cliff levels (for example 0, 1, 2) plus ramp pieces, not a continuous heightmap.** It matches what C&C and SC2 do, it makes pathing easy to reason about, and it keeps ramps as explicit, testable features. Trade-off: the art has to draw cliff edges. Cliffs need more tiles, but they will be easier to get right.
- **Recommendation: separate layers for visuals, passability and height.** The SC2 roads example shows that mixing these causes bugs (a road that looks like ground but does not allow a bridge). The map file should store each layer on its own.
- **Recommendation: use the dual-grid 16-tile method for ground transitions, and the 47-tile blob method only for the most visible terrain.** It gives clean corners at a small art cost, which fits a solo or small-team project.
- **Recommendation: author maps in Tiled, with a thin importer, for now.** Tiled already has terrain sets, Wang tiles, custom properties and JSON export. The cost is that unit placement, ownership and triggers become custom properties. Building our own editor first would delay the game. Forking the OpenRA editor is a bigger commitment; its licence (GPL v3) allows it, but we would inherit its C# stack and its mod system.
- **Trade-off on format:** Tiled-compatible JSON is readable and diffable but larger and slower to load. A binary format needs its own editor. Pick JSON now; add a binary cache later if load time matters.
- **Store tile identities by name, not number.** The OpenRA conversion shows how fragile numbered IDs are across tilesets. Add a format version field from day one with an explicit upgrade command.
- **Rush distance as the main balance tool.** Build a path-distance measurement into the map editor, in seconds at the infantry speed we pick. Target a range for main-to-natural walking time and record it for every map. Our number is unknown until we set unit speeds, so this is a process recommendation, not a number.
- **Starter rules for skirmish maps:** (1) two-fold symmetry with a rotation check; (2) at least two routes between any two bases; (3) wide main ramps; (4) mineral patches spread, not in straight walls; (5) at least one expansion that is gated by a choke or rocks; (6) no cliffs blocking line of sight at a natural; (7) resource fields split into several smaller fields; (8) a playtest with each start position before a map is accepted.
- **Campaign maps should use the same format with extra scripted data**, not a second format. OpenRA's split between a skirmish map and mission data is a reasonable model.
- **Defer procedural generation.** Use a seeded placement of decorations and resources on hand-made maps first. Full RMS-style generation is a later milestone.

## Open questions for the owner

1. **Map size target.** How many cells per skirmish map, and for how many players? Options: small 64x64 for 2 players (fast games), medium 96x96 for 2 to 4 players (balanced default), or large 128x128 for 4 to 8 players. Each choice changes rush distance and the editor's scope.
2. **Elevation as gameplay or decoration?** Options: (a) cliffs are walls with ramp access only, as in SC2; (b) cliff levels give range or sight bonuses; (c) elevation is cosmetic, as the CnCNet post suggests. Option (b) adds design work but gives the high-ground play the genre is known for.
3. **Editor path.** Options: (a) Tiled plus an importer, fastest to start; (b) our own editor in the game's language, most control; (c) fork OpenRA's editor, most features but a C# base. The answer depends on what language the owner wants to use (decided in another research file).
4. **Campaign scripting.** Options: (a) scripted triggers in a small custom language; (b) Lua, as OpenRA uses; (c) data-only missions with a fixed set of events. Lua is widely used but adds a dependency.
5. **Random maps.** Options: (a) none in v1; (b) decorations and resources randomised on fixed hand-made maps; (c) full procedural generation with fairness checks. Option (b) seems the best trade.

## Sources

Web sources read:

- [OpenRA map format wiki](https://github.com/OpenRA/OpenRA/wiki/map-format)
- [OpenRA Mapping wiki](https://github.com/OpenRA/OpenRA/wiki/Mapping)
- [OpenRA: Convert CNC map to RA](https://github.com/OpenRA/OpenRA/wiki/Convert-CNC-map-to-RA)
- [Redblob Games: Autotiling](https://www.redblobgames.com/articles/autotile/claude/)
- [Tiled manual: Using Terrains](https://doc.mapeditor.org/en/stable/manual/terrain/)
- [Tiled JSON map format reference](https://github.com/bjorn/tiled/blob/ff0c3fbf0e7ba934605cae308540fa653d75a182/docs/reference/json-map-format.rst)
- [LDtk documentation (general)](https://ldtk.io/docs/general/)
- [CnCNet forums: Understanding Map Design (AchromicWhite, 2018)](https://forums.cncnet.org/topic/8062-understanding-map-design/)
- [CnC Resource Docs: Core editors](https://cc-resource-docs.readthedocs.io/core_editors/)
- [CnC Resource Docs: Modern editors](https://cc-resource-docs.readthedocs.io/modern_editors/)
- [Well Played: Command & Conquer Tiberian Sun ambition meets reality](https://www.well-played.com.au/command-conquer-tiberian-sun-ambition-meets-reality/)
- [aoe2-rms Motivation (Racket docs)](https://docs.racket-lang.org/aoe2-rms/Motivation.html)
- [AoE forums: Arabia map thread](https://forums.ageofempires.com/t/how-can-they-have-the-most-popular-map-in-the-game-be-so-buggy-arabia/175763)
- [Liquipedia: HyperRandom](https://liquipedia.net/ageofempires/HyperRandom)
- [SC2 Editor Guides: Terrain Module tutorial](https://s2editor-guides.readthedocs.io/Classic_Tutorials/01_Terrain_Module/1/)
- [SC2Mapster wiki: Terrain Editor](https://sc2mapster.wiki.gg/wiki/Terrain_Editor)
- [Hive Workshop: Making Melee Maps](https://wiki.hiveworkshop.com/index.php/Making_Melee_Maps) (this is a StarCraft II guide, not Warcraft III)
- [TL.net: Rush distance comparisons](https://tl.net/forum/starcraft-2/124510-rush-distance-comparisons?view=all)
- [TL.net: TLMC map analysis (2013)](https://tl.net/forum/sc2-maps/416064-tlmc-map-analysis)
- [TL.net: Map size history and analysis](https://tl.net/forum/starcraft-2/351063-map-size-history-and-analysis?view=all)
- [TL.net: Daedalus Point, a lesson in map making](https://tl.net/forum/sc2-strategy/443505-daedalus-point-a-lesson-in-map-making)

Attempted but not read (blocked or failed): Spring RTS wiki SMF format and the Spring "What makes a good map" thread (both blocked by an access check); the aoe2-rms Concepts page (client error); the CnCNet wiki index (client error); Supreme Commander heightmap guides (search results only, not read).
