# freeaoe (sandsmark) - an Age of Empires II engine reimplementation

## Summary

- freeaoe is a mostly one-person, GPL v3, C++/SFML engine that runs AoE2's original data files directly (via the genieutils library). About 46k lines in `src/` (plus ~9.4k lines of generated parser). It can load scenarios, move, gather, build, train, research and fight. The README calls it "early development" and a "single-person pet project".
- Its distinctive design choice is to hardcode almost nothing: units, techs, civs, tasks, graphics and terrain all come from the original `.dat`. AoE2's tech tree is applied through "effects" that patch per-civ copies of unit data. That is a good model for a data-driven tech tree.
- It ships a parser and object model for AoE2's `.per` AI script language (Bison/Flex grammar, generated from `.list` files). However, the interpreter is **not finished or wired into the game**: `AiPlayer` is used only by `ai-test`, `AiScript::update` has a "TODO: loop through our rules", and the timer loop has a bug. Treat it as a reference for the language, not a working AI.
- The main loop is wall-clock driven (SFML clock in ms), not a fixed-tick lockstep sim. Pathfinding is a coarse-grid A* with a 50 ms timeout. Neither is suitable for deterministic LAN multiplayer.
- Rendering decodes SLP sprites into `sf::Texture`s on demand and bakes terrain blending in software on the CPU. It is tightly bound to the Genie formats and is not directly reusable for a C&C-style game.
- Most reusable ideas: the effect-command tech system, the task/action model (data-driven orders), the `.per` language design, and the fog/visibility counter map. Little code is portable as-is because of the genieutils and SFML coupling.

## 1. What it is, status, licence, build

**Status.** The README says you can "load and play scenarios, show the history screen, move around, attack, build and stuff". There is also a WIP Star Wars: Galactic Battlegrounds mode. The README TODO list (self-described as possibly outdated) names missing features: victory screen, formations, patrolling, campaign cinematics, random-map (RMS) script generation, pathfinding performance, and several AI conditions. It calls itself "mostly a single-person pet project, so if you want a more serious project, try openage". It started as a fork of apreiml/freeaoe.

**Last activity.** The clone's HEAD commit ("remove unused") carries a date of 3 Aug 2021. The code uses C++20 `std::erase_if` and `README.md` mentions Bison 3.4. I ran git only for this one check, so the date is a hint and the true upstream last-activity date is unverified.

**Licence.** GPL v3 (`COPYING`). The README is careful to say no copyrighted assets are shipped and that no reverse engineering was used. You must own AoE2 (or use the trial demo from archive.org). The same GPL v3 licence as RTS_2027 means code can be copied in, but the genieutils and `.dat` format knowledge is only useful for AoE2 data.

**Dependencies** (`CMakeLists.txt`, `.gitmodules`):
- SFML (system, window, graphics): windowing, input, textures, drawing.
- `genieutils` (submodule, sandsmark's fork, shared with the AGE editor): parses `.dat`, `.slp`, `.drs`, `.scx`, `.cpx`, palettes, blend-o-matic files. **The submodule directory is empty in this checkout**, so I could not read it. All "genie::" class descriptions below are inferred from how freeaoe uses them.
- `miniaudio` and `tinysoundfont` (audio and MIDI) under `src/extern/`.
- Bison and Flex are only needed to regenerate the AI parser; generated files (`grammar.gen.tab.cpp`, `lex.yy.cc`) are checked in.
- Build: `cmake .. && make`, with `--recurse-submodules`. macOS does not build. Windows builds come from AppVeyor.

**Size.** Lines of code per directory in `src/` (counted with wc over .cpp and .h):

| Subsystem | Lines |
|---|---|
| ai (incl. ~9.4k generated parser, 1.5k log-name table) | 15,632 |
| mechanics (units, buildings, map, players, scenario triggers) | 9,815 |
| ui | 5,323 |
| resource (assets, sprites, terrain textures, data manager) | 4,643 |
| render | 2,512 |
| actions | 2,393 |
| core (utilities, signals, logging) | 2,041 |
| audio | 1,090 |
| global (events, config) | 1,056 |
| communication, server, client (networking stubs) | ~1,000 |

Hand-written AI code (excluding generated files) is only roughly 4k lines.

## 2. Architecture

### Main loop and tick model
`Engine::run` (`src/Engine.cpp` ~100-190) is a classic single-threaded SFML loop: poll events, call `state->update(GameClock.getElapsedTime().asMilliseconds())`, update UI panels, then redraw only if something reports "updated". Otherwise it sleeps ~16 ms. Game logic receives a wall-clock `Time` and each action computes its own `elapsed = time - m_prevTime`. There is no fixed timestep and no command queue between input and simulation, so the sim is neither deterministic nor replayable. `GameState::update` (`mechanics/GameState.cpp:149`) calls `UnitManager::update`, `ScenarioController::update` (trigger scripting), then checks win/loss (last player alive wins).

Networking is a stub: `communication/` has command/tunnel classes and `server/GameServer.cpp` is 182 lines. The `game_server_->update()` calls are commented out in `GameState`.

### Entity and unit model
Inheritance, not ECS:
- `Entity` (`mechanics/Entity.h:51`, `enable_shared_from_this`) holds position, id, and a `GraphicRender`.
- `StaticEntity` extends it (corpses, decaying effects, fog "doppelgangers").
- `Unit : Entity` holds a pointer to its `genie::Unit` data record, hit points, stance, an `UnitActionHandler actions`, annexes (e.g. building attachments) and garrison links. `Building` and `Farm` extend `Unit`; `Missile` is a separate entity.
- Hardcoded IDs live in an enum in `Unit.h` (`TownCenter = 109`, `MaleVillager = 83`, `Farm = 50`, etc.), mainly for scenario setup and special cases like villager task swapping.
- `UnitManager` (1,044 lines) owns all entities, spatial lookup via `Map::entitiesAt(col,row)` (each tile holds a vector of `weak_ptr<Entity>`), selection, missiles, and the "available actions" list for the UI.

All stats come from the `genie::Unit` struct (hit points, line of sight, `Combat.Attacks`, `Combat.Armours`, `Creatable.ResourceCosts`, `Action.TaskSwapGroup`, `TerrainRestriction`...). A unit's data is a pointer into the player's civ-specific copy, so techs change stats for every unit of that type at once.

### Orders: tasks and actions
- A `Task` (`actions/IAction.h`) is a thin wrapper around a `genie::Task` record from the dat. In AoE2 each unit type lists the tasks it can perform (gather, attack, build, garrison, ...) with target classes, and `UnitActionHandler::findMatchingTask` picks the right task from a right-click target. So the "default action on right click" rule table is data, not code.
- `IAction` subclasses: `ActionMove`, `ActionAttack`, `ActionBuild`, `ActionGather` (plus `ActionDropOff`), `ActionFly`, `ActionGarrison`, each implementing `update(Time) -> UpdateResult {Updated, NotUpdated, Completed, Failed}`.
- Each unit has a current action plus a queue (`setCurrentAction`, `queueAction`, `prependAction`). Actions chain themselves: gathering hands off to `ActionDropOff` queued on the unit, then returns to gather.
- `checkForAutoTargets` makes idle units pick fights. It runs only when `m_unitsMoved` is flagged, which is a cheap trick to avoid scanning every frame.

### Data loading and the dat model
`DataManager` (272 lines) loads the `.dat` through genieutils and exposes techs, effects, terrain blocks, player colours, terrain restrictions. `AssetManager` (893 lines) finds and opens `.drs` archives (SLP, palettes, interface files, sounds) and a table of hardcoded DRS ids.

The dat model, as freeaoe uses it:
- Per civ: a full array of `genie::Unit` (index = unit id), starting resources (including pseudo-resources such as `CurrentAge` and age-tech ids).
- `Tech` (research): cost, `ResearchLocation` (building id), `RequiredTechs`, `Civ`, `EffectID`.
- `Effect`: a list of `EffectCommand {Type, TargetUnit, UnitClassID, AttributeID, Amount}`.

### How effects are applied (the tech tree)
`Player::applyResearch` -> `applyTechEffect(effectId)` (`mechanics/Player.cpp:52-115`). The effect id is put in a set of active techs (so it applies once), then each command goes through `applyTechEffectCommand` (line 117), a switch on command type:
- `EnableUnit`: `civilization.enableUnit(id)` flips the unit's Enabled flag in the player's own copy, which makes it appear in the build/train lists.
- `UpgradeUnit`: replaces the data of every existing unit of the "from" type (`unit->setUnitData`).
- `AttributeMultiplier`, `RelativeAttributeModifier`, `AbsoluteAttributeModifier`: go to `Civilization::applyUnitAttributeModifier` (`Civilization.cpp:104-330`). A tiny template `modifyAttribute` does `*=`, `+=` or `=` on the field. Then a big switch on `AttributeID` (hit points, speed, range, armor class, work rate, carry capacity, costs...) targets either one `TargetUnit` id or every unit whose `Class` equals `UnitClassID`.
- `ResourceModifier` / `ResourceMultiplier`: change player resources.
- Not implemented: tech cost/time modifiers and `DisableTech`.

Implicit techs (those with no research location, such as civ bonuses) are applied automatically after each research when all their required techs are active. Each player owns a `Civilization` object with its own mutable copy of the unit table, so effects never leak between players.

This is the cleanest idea in the codebase for RTS_2027: a tech is just an ID pointing to a list of `(target, attribute, op, amount)` records, and the engine contains one generic patcher. Both campaign unlocks and civ bonuses become data. The weakness is that attributes are enumerated in a manual switch, which must be extended whenever a new stat is added.

### Map, terrain, elevation
`Map` (736 lines) is a flat vector of `MapTile` (row-major, `row*cols+col`), with sizes from 72 (tiny) to 255 (gigantic). Tiles are 48x48 "map pixels" (`Constants::TILE_SIZE`); `MapPos` is in those units and is projected to isometric `ScreenPos` (96x48 diamond tiles, with a 24-pixel height step). Each tile has a terrain id and an `elevation` (int8). `Map::elevationAt(pos)` interpolates inside a tile for smooth height, and `MapTile` holds computed slope info and blend lists. `updateMapData` recomputes derived data (slopes, blends) when tiles change. The `setupBasic` and `setupAllunitsMap` functions build debug maps in code.

### Pathfinding
`ActionMove::findPath` (`actions/ActionMove.cpp:~650-870`, ~1,080 lines for the whole file):
- A* variant over a grid with a `coarseness` parameter (grid cell = N map pixels), 8-connected, cost 2 straight / 3 diagonal, Euclidean heuristic times `PATHFINDING_HEURISTIC_WEIGHT`.
- Uses `std::priority_queue`, `unordered_map` cameFrom, `unordered_set` visited. Goal test is "inside a target rectangle" sized by the unit's range and the target's clearance size.
- Passability is `isPassable(x,y)` against the terrain restriction from the dat plus entity footprints, with a per-action cache.
- 50 ms timeout per search (logs a warning). There is a `std::thread m_pathfindingThread` member, but the call path I read is synchronous. Whether threading is actually used is unverified.
- Repathing happens when the next waypoint becomes blocked. There is an intermediate-path fallback at coarseness 1. There is dead code for path simplification (`simplifyAngles`, `simplifyRdp`), disabled with `#if 0`.
- The README lists "Performance (especially the path finding)" as a TODO. I did not see flow fields, hierarchical pathing or unit-to-unit avoidance beyond repathing.

### Fog and visibility
`VisibilityMap` (`mechanics/Player.h:25`) is a per-player array indexed by `tileY * MAP_MAX_SIZE + tileX` of ints. `Unexplored = INT_MIN`, `Explored = 0`, and any positive value means "number of units currently seeing this tile". Units add/remove themselves as they move (`addUnitLookingAt` / the matching remove), so visible = counter > 0. Tile discovery fires `EventManager::tileDiscovered`, and `UnitManager::onTileDiscovered` creates "doppelganger" static entities so that buildings you saw remain drawn as last seen under fog. This is a compact, well-known design that is directly reusable.

### Gathering, buildings, combat
- Gathering (`ActionGather.cpp`, 224 lines): villagers carry a resource count, auto-find the nearest dropsite when full (`maybeDropOff` queues `ActionDropOff`), and return to the resource. Villager "role" changes are done by swapping the unit data to a different unit id in the same `TaskSwapGroup` (e.g. villager -> lumberjack), which is how AoE2 itself does it.
- Buildings (`Building.cpp`, `ActionBuild.cpp`): placement uses a foundation terrain id, progress is driven by builder work rate, and training queues live in the building. Research is available at a building via `Civilization::researchAvailableAt(creator)`.
- Combat (`ActionAttack.cpp`, `Unit::receiveAttack`, `Unit.cpp:271`): for each attack in the attacker's dat record, find the armour with the same class and subtract: `damage += max(attack.Amount - armor.Amount, 0)`, minimum 1, times a multiplier (elevation bonus is a TODO). Ranged attackers spawn `Missile` entities, which fly, explode and apply the same damage.

## 3. The AI: `.per` script interpreter

### The language
AoE2 `.per` scripts are Lisp-like lists of rules:

```
(defrule
  (can-train villager)          ; condition(s), implicitly ANDed
  (food-amount >= 50)
=>
  (train villager))             ; actions, executed in order
```

Rules also support `(not ...)`, `(or ...)`, `defconst` named constants, `load-if-defined`/`else`/`end-if` conditional compilation, and strategic numbers (`set-strategic-number sn-...`) that tune the built-in AI engine's behaviour. The engine re-evaluates every rule each AI pass, so the script is a priority-ordered production system, with `disable-self` to make a rule one-shot and goals/timers/`set-signal` as state.

freeaoe drives its parser from plain-text lists in `src/ai/lists/`:
- `facts.list` (109 conditions), `actions.list` (55 actions), `parameters.list` (17), `system-constants.list` (8), `system-symbols.list` (83). Entries look like `building-type-count <building> <rel-op> <Number>`.
- `generate.sh` (a bash script, with its own TODO to rewrite it) turns the lists into `gen/enums.h`, flex tokens (`tokenizer.gen.flex`, 727 lines) and Bison rules. These are merged with `parser.head.y` / `parser.mid.y` / `parser.tail.y` into `grammar.gen.tab.cpp` (9,420 lines of generated output).

### Interpreter structure
- `ScriptLoader` (594 lines) is the Bison driver. For each `(defrule ... => ...)` it calls `createCondition(...)` / `createAction(...)`, overloaded for every parameter-type combination (about 30 condition overloads, 19 action overloads), and `addRule(conditions, actions)`. Difficulty-dependent conditions and `load-if-defined` are resolved at parse time into `ConstantCondition`.
- `Condition` (`ai/conditions/Conditions.h/.cpp`, ~1,060 lines) is a polymorphic class with `bool satisfied(AiRule*)`. It also emits a `SatisfiedChanged` signal. Concrete types: `And`, `Or`, `Not`, `Compare`, `ResourceValue`, `UnitTypeCount`, timers, goals, and so on. Many register as `EventListener`s (`UnitCreated`, `UnitDestroyed`, `PlayerResourceChanged`...) and keep their own running counters (e.g. `UnitTypeCount` increments/decrements on unit events instead of scanning all units).
- `AiRule` has conditions and actions. `onConditionSatisfied` re-checks all conditions and, if all pass, executes all actions in order.
- `Action` (`ai/actions/Actions.cpp`, 426 lines): implemented ones include `SetStrategicNumber`, `DisableSelf`, `Buy/SellCommodity` (with a 1.3 fee factor), `SetEscrowPercent`/`ReleaseEscrow`, `TrainUnit` (tries a list of unit ids, e.g. all variants for the civ, and trains the first one that works), `BuildBuilding`, `Research`, `SetGoal`, `Enable/DisableTimer`, `CheatAddResource`, debug messages.
- `Ids.cpp` (752 lines) maps AI symbolic names (`archer-line`, `barracks`, `villager`) to sets of actual dat unit ids per civ.
- `AiPlayer : Player` adds difficulty level and **escrow**: a percentage of every income is diverted into `m_reserves` and excluded from `resourcesAvailable`, which implements the `.per` escrow semantic (saving for a goal). `canAffordUnitWithEscrow` etc. re-add the reserve.

### Design: event-driven rather than polling
Instead of "every pass, evaluate all rules", freeaoe makes conditions *push*: a condition watches game events and emits `SatisfiedChanged`, which triggers the parent rule to re-check and fire. It is efficient in principle, but it differs from real AoE2 semantics, where a rule whose conditions have been true for a while fires again each pass (that is how production loops like "train villager while affordable" work). With a push model, a rule fires only when something changes, so repeated production would stall unless some event retriggers it.

### How good is it?
It is a partial, unfinished implementation:
- `AiScript::update` ends with `// TODO: I guess we should loop through our rules here` and returns false. The timer loop has `if (it->time < time) { continue; }` without incrementing the iterator, which would loop forever once a timer is active (and the comparison looks inverted).
- `AiPlayer` is not instantiated anywhere in the game code: `grep` shows it only in `src/ai/` and `src/test/ai-test.cpp`, which just parses a file and exits. So no AI plays in freeaoe.
- README lists missing conditions (`town-under-attack`, `game-time`, `timer-triggered`, `difficulty`, `players-stance`, etc.). `createAction` for `set-difficulty-parameter` just warns "unimplemented". No attack-group management, no strategic number consumption, no build-placement logic was visible (I did not read all of `BuildBuilding`; treat deeper claims as unverified).
- Chat-triggered rules (`onChatMessage`) are a TODO stub.

**What is valuable:** the language catalogue in `lists/*.list` (109 facts, 55 actions) is a compact, accurate inventory of what a mainstream RTS AI script exposes, and the escrow, goal, timer, strategic-number and difficulty-constant concepts are illustrated. The overall structure (parse into condition/action objects; keep rules data-driven; separate "what to do" in script from "how to do it" in engine) is a sound pattern. The actual interpreter code is not a good base: heavy `shared_ptr` use (a comment admits "should really have unique_ptr, but bison..."), signal graphs, and the missing evaluation loop.

## 4. Rendering

- **SLP decoding**: genieutils parses the SLP; `Sprite::slpFrameToImage` (`resource/Sprite.cpp:66`, 547 lines) converts a frame into an `sf::Image`/`sf::Texture` using the palette from the DRS, applying player-colour remap (`DataManager::getPlayerColor`) and generating variants (base, outline, construction, "unavailable"). Textures are created lazily and cached per `SpriteState {frame, angle, playerColor, imageType}`.
- **Graphics selection**: `GraphicRender` (454 lines) drives animation per entity: frame timing, angle selection, and delta (overlay) graphics, all from the dat's graphic records.
- **Terrain**: `TerrainSprite` (480 lines) builds a texture per tile type and blend combination. It builds an alpha mask from the original `blendomatic` data (`BlendMode.alphaValues`) and mixes the neighbour terrain's pixels in software, per pixel (`TerrainSprite.cpp:~120-330`, with variants for the HD PNG textures). It also bakes lighting/shading for slopes from several lightmap textures. `MapRenderer` (303 lines) computes the visible row/column range from the camera corners, renders tiles to an off-screen texture and redraws only when the camera or tiles change.
- **Units**: `UnitsRenderer` collects visible units and sorts them with a `MapPositionSorter` for painter's-algorithm depth. Elevation shifts sprites vertically.
- **UI**: hand-rolled widgets (`ui/`, 5.3k lines): action panel (from dat-defined icons), minimap, unit info panel, dialogs, history screen. The UI SLPs come from the original game's DRS files.
- Rendering is abstracted behind `IRenderTarget`/`SfmlRenderTarget` (487 lines), a partial abstraction layer, but it still passes `sf::` types in many signatures. HD asset support (PNG sheets) exists via `AssetManager_HD.h`.

## 5. Code quality and reusability

Code is readable and heavily GPL-headered but includes opinionated swearing in comments, TODOs, `#if 0` blocks and warning-spam logging. The `core/` layer (Logger, `SignalEmitter` for Qt-like signals, `EventManager`) is bespoke. There is an `lgtm.yml` and a small test directory (`src/test/` with `ai-test.cpp` and a sample harness), but no unit-test suite for the simulation.

| Piece | Self-contained? | Portable to RTS_2027? |
|---|---|---|
| `.per` parser generator + condition/action objects | Medium: depends on `Player`, `EventManager`, genie types in conditions and actions | The grammar and `lists/*.list` approach is portable; the interpreter would need rewriting around our own game API |
| Effect/tech application (`Player::applyTechEffectCommand`, `Civilization::applyUnitAttributeModifier`) | Low-medium: tied to `genie::EffectCommand` | Re-implement the idea (about 200 lines), not the code |
| A* (`ActionMove::findPath`) | Low: interleaved with unit/map classes | Poor: slow, grid-coarse, no avoidance; C&C needs its own cell-based design anyway |
| `VisibilityMap` counters | High | Easy to copy as a pattern |
| Terrain blending | Low: needs blendomatic data | Not applicable to a C&C-style tile set |
| Sprite/SLP/palette/player-colour pipeline | Medium | The caching key and player-colour remap are useful ideas; the formats are AoE-specific |
| Task/action queue with `update()` returning a result enum | High | Pattern is directly useful |

**Dependency cost.** genieutils is an AoE-specific parsing library with its own build (and an empty submodule here), so using it would tie us to AoE data. SFML is a reasonable, small dependency, but the engine passes SFML types through many layers. Bison/Flex add toolchain complexity on Windows (the generated sources are checked in, which avoids it).

## 6. Lessons

- freeaoe's approach is pragmatic and monolithic: one C++ codebase, no scripting layer, reading original data in place. This gets visible results quickly (a playable scenario with moving, gathering, building and fighting) but leaves a long tail: AI unwired, no determinism, no multiplayer, performance issues. By contrast, openage (covered separately) is a layered C++/Python design aimed at a long-term clean-room architecture and conversion of data.
- A single developer achieved a broad feature set because the original data defined the content. For RTS_2027, where we make our own content, the data-driven mindset (everything tunable in data files) is the transferable lesson. The scope trap is also visible: terrain blending, sprite variants, scenario triggers and AI script compatibility consumed the effort that could have gone into a working sim loop.
- Hardcoding is pushed into small enumerated tables (`HardcodedTypes`) and special cases (farms, mills, wall dragging), which is realistic: even a "data-driven" engine accumulates special cases.

## Implications for RTS_2027

- **Tech tree as effect lists (strong fit for the mission-by-mission unlock goal).** Model each tech as `{id, cost, location, requires, effectId}` and each effect as a list of `{op: enable|upgrade|add|mul|set, target: unit id or class, attribute, amount}`. Apply to a per-player copy of the unit table. Campaign unlocks become "grant effect X at mission start". **Recommendation:** adopt this design, with a typed attribute enum and an assertion for unhandled ones rather than freeaoe's warn-and-ignore.
- **Do not copy the wall-clock loop.** freeaoe's sim runs on wall-clock ms with per-action elapsed time. For optional LAN multiplayer, deterministic replays and fair AI at all speeds, use a fixed-tick sim with a command queue (as the EA games do). **Recommendation:** fixed-tick from day one; it is costly to retrofit.
- **Task/action model.** Data-defined task lists per unit type, plus an action object with `update()` returning `Updated/NotUpdated/Completed/Failed`, plus a queue and auto-target scanning, is a clean, extensible structure for orders. Trade-off: shared_ptr/weak_ptr everywhere is convenient but heavy; ids with a central unit table are simpler and more deterministic.
- **AI scripting.** A `.per`-style rules language is a good approach to difficulty levels: one engine, many scripts, with difficulty as a constant resolved at load time and strategic numbers as tuning knobs. freeaoe shows the cost: a generated Bison/Flex parser from 200+ language entries and nothing driving it. **Recommendation:** if we do a rule-script AI, write a small hand-written recursive-descent parser for an s-expression subset (rules, `not`/`or`, constants, a few dozen facts/actions) and a polling evaluator that runs every N ticks, instead of freeaoe's event-push model. Use the `facts.list`/`actions.list` as a checklist for the vocabulary.
- **Polling vs event-driven rule evaluation.** Event-push is cheaper but breaks the "rule keeps firing while true" semantic that production scripts rely on; polling a few dozen rules every 0.5 s is negligible cost. Choose polling for simplicity and predictable behaviour.
- **Escrow is worth stealing.** A reserve percentage that diverts income so the AI can save for an expensive item is simple (about 40 lines in `AiPlayer`) and gives noticeably more human-like economies.
- **Fog of war.** The per-tile "viewer count" with Unexplored/Explored/Visible plus "last seen" ghost entities is cheap and proven. Fits tile-based C&C maps directly.
- **Pathfinding: do not reuse.** Coarse-grid A* with a 50 ms timeout inside the unit action is the weakest part. For C&C-style games, plan a separate pathing module (A* on the cell grid with a path cache, and optionally flow fields) with a per-tick budget.
- **Scope control.** freeaoe's rendering of terrain blending and scenario trigger compatibility is large effort for compatibility reasons that we do not have. Keep terrain as fixed tile sprites and build the economy-combat-AI loop first.
- **Tooling.** A small `ai-test` style CLI that loads a script and exits is a good habit; extend it to run headless AI-vs-AI matches for difficulty tuning.

## Open questions for the owner

1. **AI approach.** Do you want skirmish AI authored as a script language (data files, many difficulty variants, learning-friendly, needs a parser/evaluator) or as C++ code (state machines or behaviour trees; faster to start, harder to give modders/difficulty variants)? Option C: hybrid, with C++ doing build placement/attack micro and a small rule script choosing strategy.
2. **Moddability.** Should units, techs and effects live in data files (JSON/INI/TOML) that mods can edit, like freeaoe/AoE2, or is hardcoded C++ tables acceptable for a first release?
3. **Determinism.** Is LAN multiplayer a real requirement or a "maybe"? If real, the fixed-tick lockstep sim and integer or fixed-point math must be decided now; if not, a simpler float-based loop is allowed.
4. **Tech tree scope.** Should techs be only unlocks (C&C-style "building X enables unit Y") or also stat upgrades with generic attribute effects (AoE2-style)? The second needs the attribute-patching machinery from section 2.
5. **Language and dependencies.** If you stay with C++, is SFML (as freeaoe uses) acceptable, or do you prefer something with built-in audio/input handling? freeaoe also shows the cost of mixing a third-party format library (genieutils) with game logic.

## Sources

All paths are under `/home/claude/ref/freeaoe` (read-only; not the EA repo):
- `README.md` (status, dependencies, TODO, legal), `COPYING`, `CMakeLists.txt:8-420`, `.gitmodules`
- `src/Engine.cpp:100-190` (main loop); `src/mechanics/GameState.cpp:149-190` (update)
- `src/mechanics/Entity.h:51-130`, `Unit.h:1-140`, `Unit.cpp:271-310` (damage), `UnitManager.cpp:150-300`
- `src/mechanics/Player.cpp:40-200` (research and effect application), `Player.h:25-75` (VisibilityMap)
- `src/mechanics/Civilization.cpp:89-330`, `Civilization.h`
- `src/mechanics/Map.h`, `Map.cpp:40-200` (elevation)
- `src/actions/IAction.h`, `UnitActionHandler.h`, `ActionMove.cpp:55-120, 330-570, 650-870` (pathing), `ActionGather.cpp:35-224`, `ActionAttack.cpp:170-180`
- `src/ai/AiScript.h/.cpp`, `AiRule.h/.cpp`, `AiPlayer.h/.cpp`, `ScriptLoader.h`, `ScriptLoader.cpp:217-221, 571`, `parser.head.y`, `parser.mid.y`, `generate.sh`
- `src/ai/lists/*.list`, `src/ai/conditions/Conditions.h/.cpp`, `src/ai/actions/Actions.h/.cpp:1-100, 224-240, 397-415`, `src/ai/Ids.cpp`
- `src/test/ai-test.cpp`
- `src/render/MapRenderer.cpp:1-120`, `UnitsRenderer.cpp:138`, `GraphicRender.cpp:148-157`
- `src/resource/Sprite.cpp:44-250, 358-381`, `TerrainSprite.cpp:120-330`, `AssetManager.cpp`
- `src/core/Constants.h:23-31`
- Not readable: `src/extern/genieutils` (empty submodule). Line counts are from `wc -l` over src directories. The git HEAD date came from a single `git log -1` run.
