# Design 01 · Technical architecture
Decided in design round 1 (D-009..D-019). This is the contract the first code is built to. Anything
not pinned here is a lead decision to be made when the code is written, and recorded in DECISIONS
only if it is hard to reverse.

## 1. Stack
- **Language:** C# on the current LTS .NET (10 at time of writing). Nullable on, warnings as errors.
- **Renderer / platform:** **MonoGame (DesktopGL)**, chosen over FNA by the lead because of the
  larger community and active releases; the owner left this sub-choice open. The sim never references
  MonoGame, so switching to FNA later is a renderer-only change.
- **Audio:** MonoGame's SoundEffect/Song for v1; revisit (OpenAL/miniaudio) only if it limits us.
- **Lua:** **MoonSharp** (pure C#, tables are plain CLR objects, easy to serialise and hash) unless
  profiling shows it too slow, in which case NLua. No sandboxing needed (D-019).
- **Data:** TOML via Tomlyn. Lua for missions. PNG spritesheets + TOML sprite metadata.
- **Tests:** xUnit. Headless sim tests are the primary test suite.
- **Build:** `dotnet build`; GitHub Actions on Windows runners; publish `win-x64` self-contained.

## 2. Solution layout
```
RTS_2027.sln
src/Sim/            graphics-free simulation library (no MonoGame, no System.Random, no DateTime)
src/Data/           TOML loaders, schema validation, rule hashing (depends only on Sim types)
src/Scripting/      MoonSharp host and the mission API (depends on Sim)
src/AI/             skirmish AI (depends on Sim; issues Commands only)
src/Net/            (later) LAN lockstep transport
src/Game/           MonoGame front end: rendering, input, UI, audio, menus
src/Tools/          CLI: validate data, run headless matches, replay, map import/export
tests/Sim.Tests/    determinism, replay, save round-trip, unit behaviour
data/               rules/*.toml, maps/, missions/*.lua, sprites/, audio/
docs/
```
A Roslyn analyzer or a simple reflection test forbids `float`, `double`, `System.Random`,
`DateTime`, `Stopwatch`, `Dictionary` iteration and `Parallel` inside `src/Sim`.

## 3. Simulation core
- **Tick:** 20 Hz. `Sim.Step(IReadOnlyList<Command> commandsForThisTick)` advances exactly one tick.
  Systems run in a fixed, documented order (input → missions → movement → combat → economy →
  production → AI bookkeeping → triggers/Lua → cleanup). `Sim.Tick` is the only clock.
- **Units of measure:** a cell is 256 **leptons** on each axis (C&C's number; integer arithmetic,
  shifts for cell↔lepton). Angles are 0–255 (8-bit facing, with 8/16/32-facing lookup tables for
  sprites). Speeds are leptons per tick. Percentages are integers 0–100 or per-mille 0–1000; never a
  float. A small `Fixed32` (16.16 signed) type exists for the few ratios that need it.
- **Map:** square grid up to 128×128 cells (map format allows it; early maps are smaller). Cell
  stores: template id + sub-icon, land type (derived), height level, overlay (resource type +
  density 0..N, walls, crates), smudge, occupancy bits (vehicle / building / 5 infantry sub-slots),
  per-player visibility bits.
- **Entities:** `EntityId = (ushort index, ushort generation)`. Preallocated slot arrays per entity
  kind (Unit, Infantry, Building, Aircraft, Projectile, Anim, Terrain) with a free list in lowest-
  index order. Component structs live in parallel arrays indexed by slot. Every reference held in
  sim state is an `EntityId`, validated against the generation on use.
- **Mission state machine:** each actor has `Mission`, `Status`, `WakeTick` and a queued
  `NextMission`; `AssignMission` queues, `Commence` promotes at a safe point in the tick, each
  mission handler returns how many ticks to sleep. Cross-entity protocols (harvester ↔ refinery,
  passenger ↔ transport) are typed request/response pairs, not a shared message enum.
- **RNG:** one `SimRandom` (xoshiro128** or PCG32, our implementation) owned by `Sim`, seeded from
  the game setup, state included in saves and hashes. A separate `FxRandom` for presentation only,
  never read by sim code.
- **Determinism rules** (enforced by the analyzer and code review): integer math only; iterate
  entities by index; no hash-map iteration; no threads in `Step`; AI and Lua run inside `Step` and
  use `SimRandom`; commands sorted by (player slot, sequence).
- **State hash:** every 16 ticks, a 64-bit hash (xxHash64) over all sim arrays, with per-subsystem
  sub-hashes in debug builds so a desync names the first subsystem that diverged. Kept in a ring of 64.

## 4. Commands
`Command { int ExecTick; byte Player; CommandKind Kind; payload }`. Kinds for v1: Move, AttackMove,
Attack, Stop, Guard, Scatter, Deploy, Harvest, Return, Enter, Build(type), Place(type, cell),
Cancel, Sell, Repair, Rally(cell), SetStance, Power toggle, Special(power, target), Cheat/Debug
(debug builds only), Chat. The UI only emits commands; nothing outside `Sim.Step` mutates sim state.
Single player: `ExecTick = Tick + 1`. LAN: `Tick + delay` (2–4).

## 5. Replay and save
- **Replay** = header (format version, game build hash, rules hash, map hash, seed, player slots,
  factions, difficulty) + stream of `(tick, player, command)` + a state hash every 16 ticks + footer
  index. Recorded from the first playable build (D-017). Headless playback is a regression test.
- **Save** = versioned, field-by-field serialisation of every sim array, RNG state, AI memory and the
  Lua `state` table (plain values only). Save round-trip then step N ticks must produce the same hash
  as stepping N ticks without the round-trip; this is a permanent test.

## 6. Data model
- TOML files in `data/rules/`: `vehicles.toml`, `infantry.toml`, `buildings.toml`, `aircraft.toml`,
  `weapons.toml`, `warheads.toml` (with the warhead × armour matrix), `terrain.toml` (land type ×
  locomotion → speed %, buildable), `factions.toml`, `techs.toml`, `ai_profiles.toml`,
  `difficulty.toml`, `game.toml` (global constants).
- Each type has a string id, optional `base = "other_id"` (one level), named fields, and lists:
  `prerequisites`, `provides` (prerequisite equivalence), `unlocks`.
- **Effects:** `{ op = "enable" | "set" | "add" | "mul", target = "<id>|class:<class>", attr = "...",
  value = n }`. Granted by owning a building, completing a research, or a mission script calling
  `Tech.Grant(effect_id)`. One skirmish tech tree; campaign missions grant subsets. Applied to a
  per-player copy of the type table.
- A rules hash (over the parsed data) is embedded in replays, saves and LAN handshakes.

## 7. Pathfinding and movement
- `IPathfinder` interface. v1: A* on the cell grid, 8-way, terrain-cost weighted, per-tick expansion
  budget shared across requests, paths cached against a map version counter that increments when a
  building is placed or destroyed. Long routes: a 16×16 chunk graph only if profiling demands.
  Group orders share one search; flow fields are a later drop-in behind the same interface.
- Movement keeps C&C's blockage tiers as path-cost classes: free / ally moving / ally stopped /
  destroyable / impassable. Rules: wait if the next cell's occupant is moving forward, sidestep
  otherwise, shove idle allies. Buildings may never fully seal a region (a one-cell gap rule), so
  "no path" only happens for genuinely unreachable terrain.

## 8. Economy and production (defaults; details are round 2)
Single field resource, per-cell density, harvester state machine (seek → load → return → dock →
unload), capped storage + uncapped cash, cost drained over build time with stall when broke, power as
a gate on build speed and defences.

## 9. AI (defaults; details are round 2)
Manager-based (Economy, Production, Military, Scouting, Strategy) running every N ticks, reading a
per-tier capability profile from `difficulty.toml`, fog-honest world model, issuing `Command`s.
Headless AI-vs-AI runner in `src/Tools` from the start.

## 10. Scripting API (shape)
Lua globals: `Trigger` (OnEnter, OnDestroyed, OnAllDestroyed, OnDiscovered, OnIdle, AfterDelay,
Every), `Actor` (find, create, order), `Reinforce` (edge arrival, transports), `Objective`
(add/complete/fail, primary/secondary), `Tech` (grant/revoke unlocks), `Media` (message, music,
briefing text), `Player` (credits, visibility), `Map` (waypoints, zones), `Mission` (win/lose).
All callbacks run inside the tick; state only in `state = {}`.

## 11. Rendering (the thin layer)
Reads a `SimSnapshot` after each step and interpolates positions between the last two ticks for a
smooth 60 fps. Sprite metadata (facings, mirroring, layers, house-colour palette range) in TOML.
Palette-indexed house colours via a shader lookup. Integer scaling, pixel-perfect camera.

## 12. First milestones that follow from this
See ROADMAP M1: the headless sim with determinism tests, replay and save round-trip comes before any
sprite is drawn.
