# Design 16 · Maps overhaul: decoration and layout
Package s15-maps (session 15). **Design only: no game code changes.** Source: owner play-test, 2026-11-07 ("maps in general still
need a lot of work"). Asked what bothers him most, he picked: (1) the look of the ground, (2) detail and decoration, (3) layout and
shape. Item 1 is already being piloted by another helper (`wp/s15-terrain`: palette, cliff and shore edges, resource look; roadmap
M11-07 and M11-08). This document does **not** redo it. It covers items 2 and 3, and says where they must fit with item 1.

All numbers are starting values, marked *(tune)*. Names are working names, assumed, confirm. Nothing here is a decision until the
owner picks (section 4) and the lead records it in DECISIONS.

## 0 · Summary
- **Today's maps are bare for two separate reasons.** Decoration: there is none (ground colour plus noise, a few blob shapes, 0 to 8
  placeholder ruins). Layout: the generator scatters shapes on a flat field, so there are no lanes, no chokepoints and no "natural"
  expansion. A 2-player Rust Belt or Glass Coast map has a straight Manhattan path between the bases and a corridor about 80 cells
  wide.
- **Proposal, decoration:** three classes of prop. *Decals* (craters, cracks, stains) are render-only and cost no sim work. *Land-linked
  sprites* (boulders on Rock, rubble on Rough) need no new data at all. *Blocking props* (dead trees, wrecks, barriers, pylons) are a
  new small overlay family that blocks ground units like a wall and nothing else. Procedural placeholder art first, real art in M10.
- **Proposal, layout:** keep the symmetry machinery and the validator. Replace "scatter shapes" with a **zone skeleton** per start
  (main base with one wide entrance, a natural expansion, a third field, a contested centre) joined by 2 or 3 lanes of different width,
  then paint the biome on top. Add fairness checks that measure lanes, choke width, natural distance and empty area.
- **Six owner decisions** (section 4), **four build waves** under a new milestone **M12** (section 5). Wave 1 changes no sim code.

## 1 · What is wrong now

### 1.1 How I looked
I generated maps with the real generator and rendered them top down from the map files (colours are the theater palette; fields
are drawn in bright yellow and green on purpose so they can be seen; magenta boxes are neutral ruins; red circles are starts).
Commands, so anyone can repeat it:

```
dotnet run --project src/Tools -- map gen --biome <b> --size s|m|l --players 2|3|4 --seed <n> --out <file>
dotnet run --project src/Tools -- map ascii <file>
```

Previews live in the helper scratch (not committed): `/tmp/claude-0/s15-maps/previews/contact_sheet.png` (8 maps: every biome, S/M/L,
2p and 4p) and `handmade.png` (the three data/maps files). I also measured 6 seeds per biome at Medium, 2 players, with a small
script (shortest path between starts, corridor width, empty area). **Roadmap idea M12-08 turns that script into a `map report` tool
so the numbers are always one command away.**

### 1.2 Measured evidence (Medium 96×96, 2 players, average of seeds 1..6)

| Biome | Start-to-start path | Straight (Manhattan) | Cells on a path within +10 % of shortest | Width at the halfway line | Resource fields | Fields within 14 path of a start | Neutral ruins | Plain 9×9 windows with nothing in them |
|---|---|---|---|---|---|---|---|---|
| Rust Belt | 166 | 166 | 8489 | **87** | 12.7 | 2.8 | 4 | 3 % |
| Glass Coast | 138 | 138 | 6372 | **80** | 9.0 | 2.5 | 0 | 0.4 % |
| Mountains | 95 | 83 | 932 | 9 | 10.8 | 3.0 | 0 | 6 % |
| Ash Plains | 94 | 83 | 1214 | 7 | 20.2 | 4.7 | 2 | **26 %** |

(Rust Belt's low "nothing" figure is because the road grid itself counts as something. The road grid is the problem, see below.)

### 1.3 Layout findings

| # | Finding | Evidence | Why it hurts |
|---|---|---|---|
| L1 | **No chokepoints and no lanes on two of four biomes.** Rust Belt and Glass Coast paths equal the straight Manhattan distance, with a corridor 80 to 87 cells wide at the halfway line. Armies never funnel. | table above; contact sheet rows 1 and 3 | Nothing to defend, flank or ambush. This is the "samey, empty" feeling. |
| L2 | **Rust Belt is a uniform chessboard.** Roads every 16 cells in both directions, every block the same size, 1 canal and 4 ruins. | `rust_belt_m2_11`, `rust_belt_l4_7`; `biomes.toml road_grid = 16` | Every seed looks the same. Roads are not lanes: they are painted ground, not walls. |
| L3 | **Mountain plateaus are empty hollow octagons.** Fields need height 0 and ruins need free ground at height 0, so nothing is ever placed on a plateau. Ramps lead to nothing. | `mountains_m2_11`, `mountains_s2_5`; `ProceduralBuilder.FieldCell` | The one biome with natural high ground wastes it. A plateau should be a defensible natural expansion. |
| L4 | **Ash Plains is one huge flat field.** 26 % of the map has nothing within 4 cells. One river, 20 tiny fields. | `ash_plains_m2_11`, `ash_plains_l4_9` | Reads as "the editor default". Needs landmarks and cover. |
| L5 | **No natural expansion.** Fields sit at 5..8 cells from the start (inside the cleared reserve) or at 40..60 % of the way to the centre. There is no mid-distance, protected, single-entrance expansion between those two. | `Fields()` in `ProceduralBuilder` | No build-order choice (expand or not), nothing to harass, and a player's whole economy sits in the open. |
| L6 | **Every field has the same shape.** Diamond blobs of radius 1 to 2 with random density; pitchblende is always a 2×2 square. | `Field()` | Fields look like stamps; no "rich valley" or "road-side yard" variety. (The terrain pilot is restyling how a field is *drawn*; this is about where and how big.) |
| L7 | **Starts are in open corners (or edge middles) with no back and no entrance.** A 5×5 clear square, an 8-cell reserve, then open ground in all directions. | `Reserve()`, `StartSquares()` | No "main base". A rush can arrive from any side, and defence cannot be planned. |
| L8 | **Glass Coast is resource-poor.** 58 resource cells in total at Medium (Rust Belt 82, Ash 148). Dune "crests" are thin noodles that look like cracks. | stats; `glass_coast_m2_11` | Economy is thin and the dunes are not usable as lanes. (Income balance is another helper's area; flagged only.) |
| L9 | **Ruins are rare and arbitrary.** 0 to 8 per map, always placed at random on free ground, never in clusters or beside roads. | `Ruins()` | They are the only landmarks the map has, and they do not form places. |
| L10 | **Strong points are good.** Exact rotational or mirror symmetry, a path-equality check, 5×5 start square, 12-cell start spacing, retry on failure (`MapGenerator.FirstFailure`). | `MapValidator`, `PathSpread` | Keep all of it. Fairness by construction is the right base. |

The three hand-made maps (`handmade.png`): `rustbelt_m2` is a small script-built map with a diagonal river and L-shaped walls
(it is the best layout of the set: it has a river and real shape, but it is 64×64 and used in four missions); `glass_coast_e1` and
`ash_reckoning` are **frozen generator output** with the same problems as above.

### 1.4 Decoration findings (what exists today)

| What | State |
|---|---|
| Ground | One flat colour per land type plus light texture noise, baked once per map (`TerrainBake`, `data/theaters/*.toml`). Being re-tuned by the terrain pilot. |
| Rock, Rough | Coloured cells. A Rock blob is a dark patch; there is no boulder, no rubble pile. |
| Neutral ruins | 0 to 8 per map. They are real buildings (garrison 4 or 6, 400 or 600 HP) with placeholder art (`neu_ruin_depot`, `neu_ruin_tenement`). |
| Walls | Overlay `Wall`, player built, blocks ground units. The only blocking overlay. |
| Trees, craters, wrecks, fences, pylons, debris, decals | **None.** No data field, no art, no renderer path. |
| Template layer | Exists in the map file and the map hash, but is all zeros and never read. Reserved for the later tile set (M10). |
| Smudge layer | Listed in design 02 §3 but never built. |

## 2 · Decoration system proposal

### 2.1 Three classes of prop

| Class | What it is | Sim sees it? | Stored where | Examples |
|---|---|---|---|---|
| **A. Decal** | Flat mark on the ground, drawn under units | No | New optional `decor` layer in the map file, **not in the map hash**; if missing, derived at load from an integer hash of (map hash, cell) | craters, cracks, oil and soot stains, scorch, ripples, ash drifts, snow patches, shells, tyre ruts, manhole covers, scree |
| **B. Land-linked sprite** | A standing picture drawn on a cell the map already marks Rock, Rough or Cliff | No new data (the land type is already in the sim) | Nothing. The renderer picks sprites per cluster of Rock or Rough cells | boulders on Rock, rubble piles on Rough, jagged stone on cliff tops, glass crests on dunes, ice chunks on river banks |
| **C. Blocking prop** | A 1-cell obstacle that ground units cannot enter | **Yes**: blocks all non-air locomotion, like `Wall` | New `OverlayKind` values (append only) in the existing overlay layer; `density` byte holds the sprite variant | dead trees, wrecked cars and tanks, concrete blocks and barriers, broken pylons, fence and wall segments, driftwood snags, fallen aircraft parts |

Why this split: class A and B make the map look full for **zero** sim risk. Class C is the only one that changes play, so it is
small, explicit and testable. Neutral ruins stay as they are (garrisonable buildings, "features"); they are the landmark tier.

### 2.2 Prop kinds per biome (starting set, about 8 to 10 per biome)

D = decal (A), L = land-linked sprite (B), P = blocking prop (C). Names are working names.

| Biome (lore) | Decals (D) | Land-linked (L) | Blocking props (P) |
|---|---|---|---|
| **Rust Belt** (ruined industrial city, snow, black ash) | pothole and crack, soot stain, snow patch, small crater, oil spill, manhole, frost crack on ice | brick and rebar rubble pile on Rough, grey boulder on Rock, ice slab on river banks | bare dead tree, wrecked car or truck, jersey barrier, broken lamp post or pylon, collapsed wall segment, rail-car hulk |
| **Glass Coast** (fused desert, drowned suburbs, ash dunes) | sand ripple, glass glint patch, shells and bone, salt crust, glass-bowl crater, drowned road stripe on Shallow | spiky fused-glass crest on dune Rock, dune lip on Rough | half-buried car, driftwood snag, concrete slab, rusted pylon, fence posts |
| **Mountains** | scree, snow patch, pebbles, frost crack, frozen pool edge | boulder cluster on Rock spurs, dark cliff-top stone | stripped dead pine, fallen boulder (single), aircraft wreck, avalanche debris, wrecked supply truck |
| **Ash Plains** | ash drift, cracked earth, many craters, burnt grass, bones | ash dune on Rough, slag boulder on Rock | charred trunk (grove), burnt-out tank hull, burnt-out truck, fence stubs, broken pylon |

Notes:
- "Trees" fit the setting only as **dead trees** (stripped, charred, snow-laden). That is deliberate: forty years after the exchange,
  nothing green is left. It also keeps art simple (dark silhouettes).
- Everything reads as a *different tone and shape* from the ground. The terrain pilot sets the contrast rules for ground colours; props
  must be a clear step lighter or darker than the ground they sit on (reuse its contrast check), or they become more splatter.
- Wrecks of the two factions' units are allowed, but only generic hulks (no faction colours, no EA-like look, D-050).

### 2.3 What blocks what

| Prop class | Blocks ground movement | Blocks building placement | Blocks sight | Blocks shots | Destructible |
|---|---|---|---|---|---|
| A. Decal | no | no | no | no | no |
| B. Land-linked sprite | follows its land type (Rock blocks, Rough slows 75 %) | follows land type | no | no | no |
| C. Blocking prop | **yes** (speed 0 for non-air, same hook as `Wall`) | **yes** | no | no | not in waves 1 to 3 (see decision D1) |

Sight stays unblocked on purpose: the vision system has no occlusion today and the owner did not ask for it. Air units ignore
props, as they ignore walls.

### 2.4 Storage and the sim contract

- **Map file format 4** (loader still reads 1 to 3): optional `[layers] decor` (one byte per cell: decal kind id, 0 = none). Overlay
  layer may now hold the new prop kinds. `OverlayKind` gains values after `Wall = 3` (for example `Tree = 4`, `Wreck = 5`, `Block = 6`,
  `Pylon = 7`, `Snag = 8`; exact list is wave 2). The loader's range check (`> Wall` is invalid today, `MapFile.cs:130`) is widened.
- **Map hash:** `decor` is **not** hashed (render only, like biome and features metadata today). Props in the overlay **are** hashed
  (the hash already covers overlay and density), so two clients with different blocking props can never start the same match.
- **Sim touch points** (small; `OverlayKind` appears in about a dozen sim places): `MapState.SpeedPercent` (blocks like `Wall`,
  line 123), `MapState.Version` bump rule, building placement check, `HarvestMission` and `Simulation.Economy` (props are not
  resources: they must never be read as a field), burrow movement (Directorate tunnelers cross rock-free ground: props block the
  surfacing point, not the tunnel), `MapValidator` (new codes `prop_blocks_path`, `prop_on_resource`, `prop_in_start_square`).
  Each new sim field goes into `HashCoverageTests` or is allow-listed with a reason (hard rule).
- **Determinism:** everything sim-relevant is integer, seeded and stored in the map (props are in the file, not recomputed on
  load). The generator uses only `SimRandom`. Pure decals use a fixed integer mix function of (map hash, x, y, salt), never
  `System.Random`, `GetHashCode` or the clock, so even the derived fallback looks identical on every machine and in every replay
  screenshot. If decals ever differed between machines the match would still be identical, because the sim never reads them.
- **Old saves and seeds:** a skirmish save stores `(biome, size, players, seed)` and **regenerates** the map on load
  (`GameLoader.cs:123`). Changing generator output for an existing seed would break those saves. So new behaviour sits behind a
  `Layout` version field in `MapGenParams` (0 = today, byte-identical; 1 = new). Saves without it load as 0. See decision D4.

### 2.5 Placement rules in the generator

A new **decoration pass** runs last in `ProceduralBuilder.Build` (after ruins, before the shore rings), on the fundamental domain, so
symmetry writes every image as for any other feature. Rules are data in `data/mapgen/biomes.toml` (`[biome.x.decor]`), not code.

| Rule | What it does |
|---|---|
| **Budget** | Each biome has a per-mille budget for class C (start: 2 to 4 % of free cells) and class A (6 to 10 %). Class B is derived from Rock and Rough. |
| **Clusters, not confetti** | Props come as groves, wreck fields, barrier lines, crater fields: 3 to 12 cells grown from a seed cell, with gaps of 1 to 2 cells inside so infantry and small vehicles can weave. |
| **Edges** | Prefer cells touching cliffs, roads, water edges and plateau ramps (barriers beside a bridge, pylons along a road, trees on a ridge foot). |
| **Near ruins** | A ruin gets a "yard": 3 to 8 props and 4 to 10 decals within 4 cells (wrecks, rubble, soot). This turns a lone box into a place. |
| **Lane shaping** | Barrier lines and wall segments may narrow a lane toward its declared choke width, never below it (rule 3.4). |
| **Keep-out** | Never inside a start square, the start reserve, a ramp approach, a resource field plus 1 cell, a ford or bridge cell, or a landing or convoy marker. |
| **Connectivity** | After placing class C, re-run the existing Foot and Tracked flood fill and the `PathSpread` equality test. A failing cluster is removed (not the whole map). |
| **Symmetry** | Class C and the `decor` layer are mirrored with everything else. Rot4 and mirrors need a symmetric variant byte: variant ids that look different when flipped (text, arrows) are not used. |

### 2.6 Art approach

| Step | What | When |
|---|---|---|
| 1 | **Procedural placeholder sprites** drawn like `PlaceholderArt`: dead trees as dark forked silhouettes, wrecks as rounded boxes with a wheel row, boulders as faceted blobs, barriers as striped blocks. Two variants each. Enough to judge density, readability and performance. | Waves 1 and 2 |
| 2 | A **prop atlas interface** (kind + variant → rect) so real art drops in without code changes. | Wave 1 |
| 3 | **Real art** through the Blender pipeline already used for units and buildings: about 12 hero props per biome with 2 to 3 variants, static, no facings. Cheap compared with units. Owner reviews a contact sheet like the building pilots. | Wave 4 or M10 (decision D5) |

### 2.7 Performance

- Class A decals are **baked once per map** into a second texture the size of the terrain bake, drawn as one quad. No per-frame cost.
- Class B and C sprites are drawn through the existing sorted sprite path (`DrawOrder`) with camera culling. Budget: at most about 8 %
  of cells hold a prop, so a 128×128 map has at most about 1300 props and a screen shows a few hundred. One shared atlas, no per-prop
  state, no animation in v1.
- Props sit under the fog layer, as buildings do, so shroud hides them. Class C props are static map data, so they appear in the
  shroud's remembered image like ground.
- A debug toggle (decor off) and a frame-time test (map with 1300 props vs none) go in wave 1.
- The minimap shows class C props as ground-dark dots (or not at all, decision for the UI helper); decals never.

## 3 · Layout proposal

### 3.1 Principle
Keep `GenGrid`, symmetry images, `MapValidator`, `PathSpread` and the retry loop. Change **what is painted first**. Today the
builder paints terrain shapes and *then* tries to fit fields and starts into what is left. The new builder paints a **zone
skeleton first**, then dresses it with the biome. The skeleton is abstract (a small graph), so it is easy to test and to tune.

### 3.2 The skeleton (one fundamental domain, copied by symmetry)

```
 [Main] ==ramp== [Natural] ---- [Third / side pocket]
    \                \
     \ lane A (wide)  \ lane B (medium)          lane C (narrow choke)
      \________________\_______[ Centre ]_______/
```

| Zone | What it contains | Rule (all *tune*) |
|---|---|---|
| **Main** | The start square, 2 fields (one pitchblende), room for about 24×24 of base | Back and sides closed by cliff, rock, water or map edge; **one entrance 4 to 6 cells wide** (the main ramp) |
| **Natural** | One or two fields, a flat pad for a refinery (7×7), a ruin or two | Reached from the main ramp by a path of 14 to 22 cells; **one entrance 8 to 10 wide**; the enemy needs at least 1.6× as far to reach it as its owner |
| **Third / side pocket** | One field, optional hidden | Reached through a narrow gap (3 to 4 cells) or a ford. A raid target |
| **Centre** | The richest field (pitchblende), 2 to 3 ruins as garrison points, cover props | Every lane meets here. Open, but ringed by props that give 2 to 3 approach angles |
| **Lanes** | 2 or 3 routes between the natural and the centre and onward to the enemy | One **wide lane** 12 to 16 cells, one **medium** 7 to 9, one **narrow choke** 3 to 4 (bridge, pass, gap between ruins). All carry the symmetry |
| **Dead ground** | Rubble, groves, craters between lanes | Slows or blocks, never seals; at most one "shortcut" of width 2 per quadrant for infantry only |

3 players: a Rot4 map with the empty start's **main becomes a contested expansion** (same as design 08, Q-M4-a).
4 players: the same skeleton rotated; the centre is shared and gets 4 lanes in.

### 3.3 Per-biome painting on the skeleton

| Biome | Skeleton becomes | Signature features |
|---|---|---|
| **Rust Belt** | A **ruined town**: two wide arterials (4-cell roads) cross the map; side streets on a *jittered* grid (pitch 10 to 20, not a fixed 16). City blocks of different sizes; some blocks are "filled" with rubble and ruin clusters so they form walls and 3-wide alleys; a few blocks are open plazas (centre, natural). | River with 2 bridges (one is the narrow choke) and 1 ford; rail embankment (low cliff line) with 2 underpasses; wreck yards at junctions |
| **Glass Coast** | Sea behind each main; **long curved dune ridges** (Rock crests with gaps) act as lane walls, with 2 passes and a shallow ford; naturals sit in lagoon bays | Drowned-suburb clusters at lagoon edges (ruins plus slab props); glass crests thick enough to read (2 to 3 cells wide, not noodles) |
| **Mountains** | **Plateaus as naturals**: a high-ground field with one ramp (L3 fix); valleys are the lanes; passes 3 to 5 wide | Cliff lines with matching ramps; boulder falls that narrow a pass; aircraft wreck landmarks |
| **Ash Plains** | River splits the map; **three crossings of different kinds** (wide ford, bridge, broken causeway). Burnt groves and crater fields on the open plain make soft lanes | Landmarks every 20 to 30 cells (tank graveyard, grove, crater field), so no 15×15 window is blank |

### 3.4 Fairness and quality checks (new, in `MapValidator` plus a `map report` tool)

Existing checks stay (connectivity Foot and Tracked, 2 or more fields within 14, exact path equality, 5×5 start square,
12-cell start spacing).

| New check | Pass rule (*tune*) | Catches |
|---|---|---|
| **Lane count** | At least 2 routes between starts that share no more than 30 % of cells (found by blocking the shortest route's corridor and searching again) | L1: single blob corridor |
| **Choke width** | Every map has at least one lane whose narrowest width is 3 to 5 and one whose narrowest is 9 or more (width from a distance transform along the path) | L1 again, and "all lanes are wide" |
| **Main entrance** | Width 4 to 6 for 2 to 4 players, never below 3 | L7; walled-in mains |
| **Natural reach** | Own natural is within 14 to 22 path cells; the enemy's path to it is at least 1.6× the owner's | L5, unfair naturals |
| **Resource parity** | Per start: sum of field density within 30 path cells equal across starts (exact under symmetry; checked anyway to catch transform bugs); total per start at or above a floor set by the balance helper | L8 |
| **Empty area** | No 15×15 window without a prop, a feature, a height change or a non-base land cell, except a declared plains budget (Ash Plains 8 %, others 3 %) | L4 |
| **Spacing and detour** | Main-to-main path at least 1.2× the straight distance, and at least a minimum number of cells *(tune: about 70 at Medium)* | Shortcuts, rushes |
| **Prop safety** | Class C never changes the lane-count, choke or entrance results from "pass" to "fail"; every start still reaches every field (Foot and Tracked) | Props that wall a base in |

Each check is a unit test over a fixed list of seeds (for example 50 seeds × 4 biomes × 3 sizes × 2 to 4 players), so a tuning
change that breaks one case fails the build. Failure rates are reported (how many of the 50 needed a retry) so we notice when a
biome is too tight.

### 3.5 Generation options

| Option | Idea | Pros | Cons |
|---|---|---|---|
| **G1 Tune the scatter** | Keep today's builder, change counts and add a few rules (min gaps, bigger blobs, extra ruins) | Smallest; quick | Cannot create lanes or chokes by chance; fixes L2, L6, L9 only |
| **G2 Zone skeleton + lanes** (proposed) | Section 3.2 and 3.3 | Fixes L1 to L9; testable; keeps fairness by construction | Medium to large; needs per-biome painters and tuning |
| **G3 Skeleton + stamp library** | G2 plus hand-drawn prefab blocks (ruined block, grove, bridge, checkpoint) shared by generator and editor | Best variety and best looking; hand-made maps get the same parts | Largest; needs an editor stamp tool |

### 3.6 Where hand-made maps fit

- **Story missions stay hand-made** (D-030, M8 plan: a procedural map is not used for story missions). They use the same prop kinds,
  painted in the map editor with a new Props tool, and are **not** subject to symmetry or fairness checks; they still pass connectivity
  and the prop-safety check.
- **Competitive 1v1 and 2v2 maps** can be marked `competitive = true` in the map header; `map validate` then runs the full fairness
  suite on them, so a hand-made map can be tested the same way as a generated one.
- **Freeze and edit:** a generated map can be saved, opened in the editor, and adjusted (this is how `glass_coast_e1` and
  `ash_reckoning` were made). With G2 the skeleton zones are also saved as zone names in the map file, so the editor can show "main,
  natural, lane A" overlays and missions can name them (`Zone` in the mission scripting API).
- **Stamps** (G3) are the shared parts library: the generator places them, the editor stamps them by hand. The rebuilt
  `rustbelt_m2` (wave 4) is the showcase.

## 4 · Owner decisions

Each table: option, what it gives, cost, recommendation. Sizes are S (a few days), M (about a week), L (more than a week) in one
helper's time, as in the UI audit.

### D1 · How far should props go in play?

| Option | What it gives | Cost | Recommendation |
|---|---|---|---|
| A. Look only | Decals and sprites on existing Rock and Rough. No blocking props. Map looks full, plays the same | S, no sim change, no hash change | |
| B. Look plus blocking props, static | A plus dead trees, wrecks, barriers, pylons that block ground units. Lanes and cover can be shaped. Never destroyed | M, small sim change (about a dozen sites), new map format 4 | **B** |
| C. B plus destructible or crushable | Tanks crush wrecks, splash clears trees. More alive, more tactics | L: per-cell prop health in sim state, hashing, replay and save changes, AI path invalidation | Later, only if B plays well |

Reasoning: A alone leaves layout unfixed (props cannot shape lanes). C adds sim state for a feature nobody has asked for.

### D2 · How should the generator change?

| Option | What it gives | Cost | Recommendation |
|---|---|---|---|
| G1 Tune the scatter | More ruins, bigger shapes, jittered roads | S to M | |
| G2 Zone skeleton and lanes | Real bases, naturals, lanes, chokes, tests | L (about 4 sessions across 4 biomes) | **G2** |
| G3 G2 plus stamp library and editor stamps | Highest variety; shared parts for hand-made maps | L plus M | **G2 first, add G3 as wave 4** |

### D3 · Shape of a starting base

| Option | What it gives | Cost | Recommendation |
|---|---|---|---|
| A. Keep the open corner square | No AI or balance risk | none | |
| B. Walled main with one wide ramp, plus a natural | Defensible bases, a build-order choice (expand or not), harassable naturals | M, includes an AI check (building placement and a corner-advantage match series like M2-01) | **B** |
| C. B, plus a skirmish setup option "open starts" | Players can choose the old feel | B plus S | B now, C if players ask |

### D4 · What happens to existing seeds, saves and the three data maps?

| Option | What it gives | Cost | Recommendation |
|---|---|---|---|
| A. Versioned: skirmish setup gets "Map style: Classic / New"; old saves keep Classic; New becomes the default when wave 3 lands | Nothing breaks; owner can compare side by side | S (a `Layout` field in `MapGenParams` and the save header) | **A** |
| B. Replace outright | Simplest code | S, but every generated-map save from before stops loading and seeds shared in the past change shape | |
| C. Classic forever | Safest | Two generators to maintain forever | Drop Classic after about two releases |

The three files in `data/maps/` do not change in any option. They get a hand-made rework later (wave 4).

### D5 · When does real prop art come?

| Option | What it gives | Cost | Recommendation |
|---|---|---|---|
| A. Procedural placeholders now; real art in M10 | Fast; the owner judges density and layout without art cost; atlas interface keeps the swap cheap | S now, M later | **A** |
| B. Small Blender prop set in wave 4 (about 12 per biome) | Looks right sooner; uses the Blender slot | M, shares the 2-core box with other art | After the owner has seen wave 2 |
| C. Hand pixel art or a generator | Different style | L | No |

### D6 · Where does this sit in the build order?

| Option | What it gives | Cost | Recommendation |
|---|---|---|---|
| A. All of M12 before M9 (LAN) | Maps fixed before multiplayer; one change to the map hash | L: delays LAN by about four waves | |
| B. M12 after M9 | LAN sooner | Map hash and `MapGenParams` change after LAN ships (matters: LAN joins check the map hash) | |
| C. Waves 1 and 2 before M9, waves 3 and 4 after, **with the format and generator version fields done before M9** | Owner sees a fuller map soon; the compatibility-sensitive parts (format 4, `Layout` field) land once, before LAN | M | **C** |

(Why C: LAN checks the map hash on join, so the version fields must exist before the first LAN release. The rest is content
and can follow.)

## 5 · Build plan

New milestone: **M12 · Maps overhaul** (M9 is LAN, M10 polish, M11 UI and input). It depends on M11-07 and M11-08 (terrain and
resource look, in progress) only for colours: decals and props are drawn on top of the baked ground.

### Wave 1 · Make every map look populated (no sim change, no hash change)
| Id | Item | Size |
|---|---|---|
| M12-01 | Decal pass: derived decals from an integer hash, per-biome decal tables in the theater files, baked to a second texture | M |
| M12-02 | Land-linked sprites: boulders on Rock clusters, rubble on Rough clusters, glass crests, ice slabs (procedural placeholder art) | M |
| M12-03 | Prop atlas interface, decor on/off debug toggle, frame-time test | S |

**Owner can try:** start any skirmish or mission. The ground has craters, stains, rubble and boulders. Press the decor toggle to
compare with the old bare look.

### Wave 2 · Blocking props and decorated generator (map format 4)
| Id | Item | Size |
|---|---|---|
| M12-04 | Format 4: `decor` layer, new `OverlayKind` prop values, loader range, `MapState` blocking, placement and harvest checks, validator codes, hash-coverage entries | M |
| M12-05 | Generator decoration pass (clusters, edges, ruin yards, keep-outs, connectivity guard) behind `Layout` 1; skirmish setup "Map style: Classic / New" | M |
| M12-06 | Editor Props tool and decal brush; `map report` and `map render --png` CLI (previews and the measured table of section 1.2) | M |

**Owner can try:** skirmish with Map style New: dead-tree groves, wreck yards, barrier lines, ruins with yards; paint props in the
map editor; run `map report` on any map file.

### Wave 3 · Layout skeleton (the big one)
| Id | Item | Size |
|---|---|---|
| M12-07 | Zone skeleton and lane painter, Rust Belt "ruined town" first | L |
| M12-08 | New fairness checks of section 3.4 and the 50-seed sweep tests; failure-rate report | M |
| M12-09 | Mountains: plateau naturals and passes | M |
| M12-10 | Ash Plains: crossings, groves, landmarks | M |
| M12-11 | Glass Coast: dune lanes, lagoon naturals, drowned suburbs | M |
| M12-12 | AI check: skirmisher mirror and swapped-start match series on the new layouts (no corner advantage, like M2-01), and AI base placement in a walled main | M |

**Owner can try:** one biome at a time (Rust Belt first): the same seed shows a main with one ramp, a natural, three lanes, a choke
bridge. `map report` shows the lane and choke numbers.

### Wave 4 · Stamps, showcase maps, art
| Id | Item | Size |
|---|---|---|
| M12-13 | Stamp library (ruined block, grove, crater field, bridge, checkpoint) shared by generator and editor; editor stamp tool | M to L |
| M12-14 | Rebuild the three `data/maps/` files from stamps (mission maps keep their ids and start positions; missions re-tested) | M |
| M12-15 | Prop art pass in Blender (if D5 = B), or folded into M10-xx | M |
| M12-16 | Optional: destructible or crushable props (only if D1 = C) | L |

**Owner can try:** open the editor, stamp a ruined block, play it; view the rebuilt campaign map; review a prop art contact sheet.

### Risks and mitigations
| Risk | Mitigation |
|---|---|
| New layouts hurt the AI (walled mains, props in the way) | M12-12 before the new style becomes the default; AI reads only the view, so props appear as blocked cells with no AI code change |
| Props wall a base or a field in | Connectivity guard in the generator, `prop_blocks_path` validator error, 50-seed sweep tests |
| Decoration crowds the ground into "splatter" again (owner item 8) | Budgets per mille; decals low contrast; props must pass the terrain pilot's contrast rule; clusters, not confetti |
| Old saves and replays | `Layout` field in `MapGenParams`; replays need `--map` already; `decor` is not hashed |
| Time | Wave 1 is useful alone; each later wave is shippable alone; Classic stays until the owner says otherwise |

### Open questions for the lead (not owner decisions)
1. Does the minimap show class C props? (UI helper: suggest "dark dot".)
2. Mission scripting: should triggers be able to remove a prop cell (for example "the barricade falls" in a story mission)? Cheap with the overlay layer; not scheduled.
3. Resource floor per start (L8, Glass Coast): the balance helper should set the number the parity check uses.

## Wave 3 as built (M12-07, M12-08)

- `MapGenParams.Layout = 2` (`MapGenerator.ZonedLayout`) paints a zone skeleton first (mains, lanes, plazas), then the biome. Rust Belt only; other biomes
  throw "Layout 2 is not built yet". `CurrentLayout` stays 1 and the legacy golden hashes are unchanged.
- Symmetry: MirrorDiag (2 players, bases at opposite corners of the anti-diagonal, main-to-main path about the side length); a four-image
  dihedral grid for 3-4 players (teams 1+2 and 3+4 side by side, enemy across). One river runs corner to corner between the bases with two decks
  (one is the narrow choke) and a gravel-bar ford in the middle (the centre field). Town fabric: a jittered street grid whose blocks hold ruined
  buildings, standing walls and rubble lots; walled courtyards at the contested yards; main fields are 3+ thick bands.
- `LayoutChecks` (src/Data/MapGen) measures the D-183 rules; `map report --layout 2` prints each with pass/FAIL. Generation retries seed + attempt
  (cap 40) and keeps the first map that passes everything, else the one failing fewest. 3-4 player checks differ slightly (approach width counted from 4,
  contested fields 1..6 at 0.40-0.90 P, 3-player lane mix judged over both enemies, the played 3-start map is judged).
- Sweep (50 seeds x S/M/L x 2/3/4 players): every map passes every check; retries per seed 0.2-1.3 (2p), 1.0-6.7 (3-4p, Small worst).
- Zone names are not saved: map format 4 has no zone field (needs a format bump). Tools: `--layout 1|2`, `--raw`, `--checks`.
- 2v2 teams: `SkirmishSettings.DefaultTeam` gives teams 1+2 vs 3+4 for 4 players on a layout-2 map (setup screen keeps the choice editable);
  the screen has no layout switch yet, so this only applies to a preselected layout-2 map.
