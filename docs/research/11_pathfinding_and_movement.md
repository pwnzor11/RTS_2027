# RTS Pathfinding and Group Movement

**Summary**

- Real-time strategy movement has two separate problems: finding a route (pathfinding) and not colliding with other units while following it (local avoidance). Most shipped games solve them with different tools.
- For single units on a grid, A* is the baseline. Jump Point Search (JPS) and hierarchical methods (HPA*, OpenRA's hierarchical finder) cut the cost on large maps, mostly by reducing how much of the map is searched.
- For many units heading to one place, the common answer is one shared field per destination (a flow field), not one A* per unit. Supreme Commander 2 reports that a 100-unit group costs the same field generation as a single unit.
- Local crowding is handled either by pushing and sliding (continuous models such as RVO/ORCA or boids-style separation) or by letting units wait, wobble or shove on a shared grid (cell-based models).
- The classic C&C feel comes from discrete occupancy: one vehicle per cell, a few infantry per cell, and units that wait or shuffle. This is compatible with a deterministic, integer simulation and is the better fit for RTS_2027.
- Several claims about StarCraft II's internals could not be verified from primary sources. They are marked as such below.

## 1. Representation: grid vs navmesh vs waypoint graph

**Grid (tiles or sub-tiles).** Each cell is either passable or not, and moves go to neighbours with a cost. It is simple, deterministic, and fits a tile-based map. Its weakness is accuracy: a single unit may not fit through a diagonal gap. The 0 A.D. design doc handles this by using a finer navigation grid than the terrain tiles. Its navcells are 1.0 m, while terrain tiles are 4.0 m, and it notes that the coarse terrain grid cannot capture gaps a single unit can pass through ([0 A.D. pathfinder design](https://gitea.wildfiregames.com/Stan/0ad/raw/commit/63248bdc333141914c5d3f84b1fe2229429f4e4f/docs/pathfinder.pdf)). Each navcell stores one bit per passability class. Obstacles are inflated by the unit's clearance.

**Navmesh.** Walkable space is covered by convex polygons. This gives smooth, any-angle paths and handles clearance naturally, but it is more complex to build and to update when buildings are placed. A navmesh is a poor match for a deterministic grid sim and for the tile-by-tile rules of a C&C-style game.

**Waypoint graph.** Hand-placed or auto-generated nodes. It is cheap to search and easy to hand-tune, but units look robotic off the graph, and every new building can invalidate edges.

**Recommendation for a tile RTS:** use a tile grid for the simulation and for pathfinding, with a finer sub-cell grid only if we need it for unit-size clearance. Navmesh and waypoints add cost without a clear gain at a 128x128 map size.

## 2. Single-unit search

**A\* and its heuristics.** A* is Dijkstra with a heuristic added to the priority. Dijkstra ranks the frontier by distance from the start. A* ranks it by distance plus estimated distance to the goal. If the heuristic never overestimates, A* returns the shortest path. As the heuristic goes to zero it becomes Dijkstra; as it grows it behaves more like greedy best-first search, which is faster and not optimal ([Red Blob Games, A* introduction](https://redblobgames.com/pathfinding/a-star/introduction.html)). For 4-way grids the page uses Manhattan distance. For 8-way movement the usual admissible choice is octile distance, with a diagonal cost of about 1.414 times a straight step. (The page does not cover 8-way heuristics; this is general practice and not sourced here.)

For determinism, use integer costs (for example 10 for a straight step and 14 for a diagonal) and a fixed tie-break order. Floating-point ties can break differently on different machines.

**Jump Point Search.** JPS is an optimal A* variant for uniform-cost 8-connected grids. From a node it scans in a direction and stops only at "jump points," the goal, or a cell with a forced neighbour next to an obstacle. Skipped cells never enter the open list, so JPS needs no preprocessing and no extra memory ([Harabor and Grastien, AAAI 2011](https://harabor.net/data/papers/harabor-grastien-aaai11.pdf)). The paper reports expansion reductions from about 13x to 215x across its four benchmark sets, and wall-clock speedups of 25 to 30 times over A* on two of them. The summary I read does not define the Table 1 column clearly, so treat the exact ratios as approximate. JPS is limited to uniform costs. Terrain costs (roads, mud) break it, which matters for an RTS with varied terrain.

**Hierarchical search (HPA\*).** HPA* covers the map with clusters, computes entrances between neighbouring clusters, precomputes intra-cluster distances, and searches the small abstract graph first. It then refines the result cluster by cluster. The paper reports speedups of up to 10x over a well-tuned A* on BioWare maps, under 10 ms per query on 2000-era hardware, and path quality within 1% of optimal after smoothing. Memory overhead was about 9% in its example data ([Botea, Müller and Schaeffer, HPA*](https://webdocs.cs.ualberta.ca/%7emmueller/ps/hpastar.pdf)). Its strength is that the first steps can be issued before the full path is known.

**OpenRA's hierarchical finder.** OpenRA's `HierarchicalPathFinder.cs` divides the map into 10x10 "grids" (constant `GridSize = 10`, chosen empirically). Each grid is flood-filled into abstract nodes, border crossings become edges, and a search runs on the abstract graph with temporary start and goal edges inserted. A `PathExists` query compares precomputed connected "domains" instead of searching at all. Dynamic changes mark grids dirty and rebuild them lazily before the next query. It deliberately ignores ordinary moving units, and the source notes that searches through unit-blocked chokepoints lose the speedup ([OpenRA source](https://github.com/OpenRA/OpenRA/blob/216758dbc7b77458eb21ddd747f09c5785e5cbfc/OpenRA.Mods.Common/Pathfinder/HierarchicalPathFinder.cs)). OpenRA's 2022 playtest announced this as the headline performance feature for large maps ([OpenRA playtest 20221203](https://www.openra.net/news/playtest-20221203/)).

**Theta\* and any-angle search.** Not verified in this research. Amit Patel's index mentions any-angle movement, but I could not read the relevant section.

**Dynamic blockers.** Placing a building changes the cost grid. The cheapest approach is to mark affected cells and invalidate only the cached paths or abstract-graph pieces that touch them. OpenRA does this per grid and only when reachability actually changes. Factorio's path cache (below) uses the same idea of caching search results and discarding them when the world changes.

**Path smoothing.** HPA* smooths by replacing suboptimal segments with straight lines. Funnel smoothing is the standard companion to navmeshes. On a grid, a simple "line-of-sight shortcut" pass is enough.

## 3. Many units

**Path caching.** Factorio's Friday Facts #121 describes a fixed-size path cache. Each stored path records how expensive it was to compute, and the cheapest one is evicted first. A new request can be answered by joining two shorter searches onto a cached path. A "negative cache" also remembers routes known to be unreachable ([Factorio FFF #121](https://cf-www.factorio.com/blog/post/fff-121)). Factorio is not an RTS, but its pattern of caching routes shared by many similar units transfers directly to group orders.

**Flow fields (integration field).** One search runs outward from the destination over the whole map, storing the cost to reach the goal in each cell. Each unit then reads the direction of its current cell. Cost is paid once per destination, not once per unit. Supreme Commander 2 is the best-documented RTS example. Its design, as summarized from the developer's post on the Wildfire Games forum (which I could not reach directly, so this is secondhand):

- Each destination gets a static vector field. Each tile stores one of 16 directions, a "point directly at goal" marker, or an unexplored state (5 bits per tile).
- Tiles within about 64 tiles of the goal that have a clear straight line point directly at it. Farther tiles follow the expanding search.
- Fields are shared: "moving a group of 100 units has the same flowfield-generation cost as moving 1 unit."
- Tiles are roughly the size of a small unit. The test map was about 1024x1024 tiles.
- Each unit class has a fixed movement type that decides which terrain it can cross.
- Buildings never fully seal an area: a one-tile border always stays open.

([Wildfire Games forum, Supreme Commander 2 pathfinding](https://wildfiregames.com/forum/topic/16018-supreme-commander-2-pathfinding/)). The Game AI Pro chapter on the same system was not accessible, so its details are unverified.

For RTS_2027, a 128x128 map has 16,384 cells. A full integration field over that is small enough to rebuild on demand, which is my estimate and not a measured figure.

**Group movement and formations.** SC2 and AoE2 used different group models, and details are thin in public sources:

- Supreme Commander 2 places units in a grid formation around the target, using ray casts from the target to find slots. Each unit follows the group's field until it has a clear line to its slot. The group's target moves with the average position, so groups form narrow columns until they reach the goal.
- A forum thread about "reaching a point" describes AoE2's model: a formation centre with a slot per unit, which breaks down when units cannot reach their slots ([gamedev.net, group arrival discussion](https://ehe.gamedev.net/forums/topic/688707-what-does-it-mean-for-a-group-of-units-to-reach-a-point)). That thread's suggestions come from forum posters, not shipped code, except for one consultant's advice: give the goal an arrival radius that grows with group size, add weak attraction to the point, and let separation settle units nearby.
- Common practice: when a unit fails to reach its goal for a few seconds, reroute, and stop if it keeps failing. The thread notes that letting units push until everyone arrives works but causes crowding.

**Local avoidance: continuous.** RVO2 and ORCA are the standard continuous methods. Each agent computes a permitted-velocity half-plane for each neighbour. A linear program then picks the velocity closest to the preferred one. Each agent takes half the correction, so no communication is needed ([van den Berg et al., ORCA](https://gamma.cs.unc.edu/ORCA/publications/ORCA.pdf); [RVO2 library](https://gamma.cs.unc.edu/RVO2/)). The horizon parameter tau is the guaranteed collision-free time; the figures use tau = 2. Static obstacles take full responsibility and are never relaxed. The paper reports 5,000 agents on eight cores at about 8 ms per update for a circle scenario and about 15.6 ms for an evacuation. RVO2 is Apache-2.0 licensed, and its generic framework is usable in a GPL project. These figures are for a multi-core machine and a 2011 study, so treat them as order-of-magnitude.

**Local avoidance: boids.** Reynolds' 1986 model uses three rules: separation, alignment and cohesion. Each boid reacts only to neighbours within a distance and an angle. Naive neighbour search is O(n²), but a spatial index brings it close to O(n) ([Reynolds, Boids](http://www.red3d.com/cwr/boids/)). The steering formula (desired velocity minus current velocity) is standard but I did not verify it on the page I read.

**Continuum crowds.** This is a field-based crowd model that discretises space into a grid and adds density to occupied and neighbouring cells, so units avoid crowded cells ([Continuum Crowds](https://old.fbpe.org/continuum-crowds.html)). The page I could read gives agent counts (800 to 4,000) from course projects but no timings. Treat it as a research reference, not a proven RTS solution.

**Cell reservation (classic C&C baseline).** In Tiberian Dawn style games, a cell holds one vehicle, and infantry share a cell through sub-positions. Units that want an occupied cell wait, go around, or shove a friendly unit aside. This model is discrete and deterministic and gives the characteristic "queueing" of classic C&C. I did not verify the Tiberian Dawn code paths for this report; the other code-study helpers cover that.

**Chokepoints and harvesters.** OpenRA's note about unit-blocked chokepoints and the SC2 forum observations both point to the same issue: a static path planned through a choke that units then fill is wrong. The standard fixes are (a) plan with static terrain only and let local avoidance handle units, (b) penalise cells that are currently occupied, or (c) allow a wait-then-reroute. Harvesters should be given their own lane costs or a separate reservation so they do not block the field.

## 4. Determinism and performance

- **Integer arithmetic.** Use fixed-point or integer positions and costs in the simulation. Float differences across machines break LAN lockstep.
- **Stable ordering.** Iterate units and cells in a fixed order. Break ties by unit ID and cell index, never by pointer or hash-map order.
- **Work budgets, not timers.** Time-slice searches by a fixed count of expansions per tick (for example, a few thousand node expansions). Wall-clock budgets differ between machines and desync the sim.
- **Caching keyed on a map version.** Store paths and fields with the version number of the terrain or building layer they were built from. A version bump invalidates them.
- **Lazy rebuild.** OpenRA marks grids dirty and rebuilds on the next query, which spreads cost across ticks.

## 5. Classic C&C vs modern feel

Classic C&C units occupy discrete cells. A tank in a cell blocks it. Infantry use up to five positions per cell in C&C Remastered, with distances measured in leptons (1/256 of a cell). Players learned to control sub-cell placement: attack from the south to get closer before the enemy's range engages, and use Force-Move to push a unit into a free position ([C&C Remastered sub-cell control](https://gamepretty.com/command-conquer-remastered-collection-infantry-sub-cell-control/)). Units that cannot reach a cell wait, and this produces readable queues at chokepoints.

Modern continuous models (ORCA, boids-style separation) produce smooth crowds with no visible queues, but units also push through each other and form blobs. SC2 players describe units that "push through their buddies rather than repath around," which is a forum observation and not a documented rule.

The trade-off: discrete cells give a recognisable C&C feel, clear rules for players, deterministic outcomes and cheap logic. Continuous positions give smoother motion and better handling of large units, at the price of more complex collision and a harder determinism story.

## 6. Case studies

| Game | Model | What is verified | Gap |
|---|---|---|---|
| Supreme Commander 2 | Flow fields per destination, 16-direction tiles | Forum summary of author's post | Game AI Pro chapter not read |
| StarCraft II | Unknown; forum speculation about navmesh and A* | Little | Anhalt's GDC 2011 talk not accessible |
| StarCraft: Brood War | Grid with unit-level blocking, re-pathing | Forum discussion only | No primary source read |
| Age of Empires II | Formation slots with arrival criteria | Forum discussion; openage formations doc only (no pathing) | No pathing source read |
| OpenRA | Hierarchical finder over 10x10 grids | Source file read | No performance numbers |
| Beyond All Reason (Spring) | QTPFS quadtree, resolution adjusted on demand | 2023 engine update notes | No algorithm or numbers |
| 0 A.D. | Two-level: long-range grid ignoring units, short-range avoiding units | Design doc (may be out of date) | Implementation not checked |
| Factorio | Path cache with joins and negative cache | FFF #121 | Not an RTS |

**Beyond All Reason.** The 2023 update made QTPFS the default pathfinder. Reported changes are fewer zigzags for fast vehicles, fewer units trapped by factories or buildings, and better coordination in groups. No numbers are given ([BAR engine update](https://www.beyondallreason.info/microblogs/48)).

**0 A.D.** The long-range pathfinder finds approximate routes and ignores units, because they will have moved by arrival. The short-range pathfinder follows one segment and avoids units. The design targets about 1,000 moving units on a 256x256 terrain-tile map. These are targets, not measurements.

## Implications for RTS_2027

- **Recommendation: discrete, integer, cell-based movement.** Keep unit logic on a tile grid with fixed-point sub-positions for drawing. One vehicle per cell, a small fixed number of infantry sub-slots per cell, and waiting or sliding rules for crowding. This is the most faithful route to the classic feel, and it is easiest to keep deterministic for LAN play.
- **Recommendation: flow fields for group orders, A\* for single units.** Build one integration field per destination when two or more units share an order. Single-unit orders use A* with a per-tick expansion budget. Cost is roughly one search per destination, not per unit.
- **Recommendation: a two-level stack for a 128x128 map.** Use A* (or JPS only if terrain costs stay uniform) for single units, and a 16x16 or 8x8 chunk graph (HPA*-style, with a reachability domain check) for long routes. The map is small enough that a flat A* with caching may be enough; add the hierarchy only if profiling says so.
- **Recommendation: invalidate, don't re-search.** On building placement or destruction, mark the affected chunks dirty and rebuild lazily, as OpenRA does. Cache paths and fields against a map version.
- **Use a budget per tick.** Give searches a fixed count of expansions per tick so that all machines take the same number of steps (determinism).
- **Local crowding as a rule set, not physics.** Adopt cell rules: a unit waits if its next cell is occupied by a unit that is also moving forward, otherwise it sidesteps. Reserve ORCA only if we later move to continuous positions.
- **Group arrival.** Each unit targets a formation slot; if the slot cannot be reached within a few seconds, the unit takes the nearest free cell in the group's area. Groups should settle around the target instead of stopping at a single point.
- **Trade-off to accept.** Cell-based movement will look rigid next to modern games and gives up smooth diagonal crowds. That is the classic feel the project is after, but the owner should confirm it.

## Open questions for the owner

1. **Cell-locked or continuous movement?** Options: (a) classic: one vehicle per cell, infantry sub-slots, visible queues; (b) continuous positions with radius collision, smoother crowds, harder determinism. Recommendation is (a).
2. **How many infantry per cell?** Options: 1 (simplest, very readable), 4 (a square squad fits), 5 (C&C Remastered-style five-dot pattern). Affects squad feel and crowd density.
3. **Should friendly units shove each other?** Options: never (always wait or reroute), shove stationary friendlies only (C&C-like), or shove any unit (more fluid, less readable).
4. **How big are groups in practice?** Flow fields pay off above a handful of units per order. If typical orders are under 5 units, A* with caching may be simpler. If typical orders are 50 or more, flow fields are needed.
5. **Should buildings ever seal an area?** SC2-style walls are impossible if a border is always kept open (as in Supreme Commander 2). Options: always keep a one-cell gap (simpler, no stuck units), or allow sealing with an explicit "no path" outcome.

## Sources

Read in full or in part with the Fetch and Search tools (secondhand summaries noted above):

- [Red Blob Games, A* introduction](https://redblobgames.com/pathfinding/a-star/introduction.html)
- [Red Blob Games, Flow field pathfinding](https://redblobgames.com/blog/2024-04-27-flow-field-pathfinding) (high-level only)
- [Amit Patel, A* pages index](https://theory.stanford.edu/~amitp/GameProgramming/) (outline only)
- [Harabor and Grastien, Online Graph Pruning for Pathfinding on Grid Maps (AAAI 2011)](https://harabor.net/data/papers/harabor-grastien-aaai11.pdf)
- [Botea, Müller, Schaeffer, Near Optimal Hierarchical Path-Finding (HPA*)](https://webdocs.cs.ualberta.ca/%7emmueller/ps/hpastar.pdf)
- [Factorio Friday Facts #121, Path Finder Optimisation II](https://cf-www.factorio.com/blog/post/fff-121)
- [Wildfire Games forum, Supreme Commander 2 pathfinding](https://wildfiregames.com/forum/topic/16018-supreme-commander-2-pathfinding/)
- [0 A.D. pathfinder design document](https://gitea.wildfiregames.com/Stan/0ad/raw/commit/63248bdc333141914c5d3f84b1fe2229429f4e4f/docs/pathfinder.pdf)
- [OpenRA HierarchicalPathFinder.cs](https://github.com/OpenRA/OpenRA/blob/216758dbc7b77458eb21ddd747f09c5785e5cbfc/OpenRA.Mods.Common/Pathfinder/HierarchicalPathFinder.cs)
- [OpenRA playtest 20221203](https://www.openra.net/news/playtest-20221203/)
- [Beyond All Reason engine update microblog](https://www.beyondallreason.info/microblogs/48)
- [van den Berg et al., ORCA paper](https://gamma.cs.unc.edu/ORCA/publications/ORCA.pdf)
- [RVO2 library page](https://gamma.cs.unc.edu/RVO2/)
- [Reynolds, Boids](http://www.red3d.com/cwr/boids/)
- [Continuum Crowds project page](https://old.fbpe.org/continuum-crowds.html)
- [C&C Remastered infantry sub-cell control article](https://gamepretty.com/command-conquer-remastered-collection-infantry-sub-cell-control/)
- [gamedev.net, What's StarCraft 2's pathfinding tech?](https://www.gamedev.net/forums/topic/563819-whats-starcraft2s-pathfinding-tech/) (forum, mostly speculation)
- [gamedev.net, How to do StarCraft 2 pathfinding](https://gamedev.net/forums/topic/648438-how-to-do-starcraft-2-pathfinding/) (forum)
- [gamedev.net, What does it mean for a group of units to reach a point](https://ehe.gamedev.net/forums/topic/688707-what-does-it-mean-for-a-group-of-units-to-reach-a-point) (forum)
- [openage PR 783, AoE2 formations documentation](https://github.com/SFTtech/openage/pull/783) (confirmed it does not cover pathing)

Not accessible: Anhalt, "AI Navigation: It's Not a Solved Problem - Yet" (GDC 2011); Game AI Pro chapter on Supreme Commander 2 flow field tiles; Red Blob flow field page detail (fetched, but it gave only a high-level summary); Brood War and AoE2 movement internals.
