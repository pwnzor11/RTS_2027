# Research synthesis and design-round agenda

Session 1 produced 19 research files (see README.md): six studies of the EA C&C Remastered source,
eight web-research topics, and five studies of other open-source RTS repos. This file pulls their
recommendations together, notes where they agree and disagree, and lists the decisions the owner has
to make, grouped into three design rounds. Where a point is backed by several files the numbers are
given in brackets.

## 1. Where the research agrees (strong consensus)

These came up independently in three or more files, from both the code studies and the web research.
They are proposed as the default unless the owner objects.

1. **Fixed-tick, deterministic, lockstep-ready simulation.** Commands only; the sim is a pure
   function of (initial state, seed, command log). Integer or fixed-point math, one seeded RNG owned
   by the sim, iteration in entity-id order, no wall-clock, state hash every N ticks from day one.
   Single player runs through the same command path with a 1-tick delay. [01, 03, 06, 10, 15, 20, 21]
   Already recorded as D-006. Both AoE reimplementations (openage's wall-clock curve model, freeaoe's
   wall-clock loop) are cited as cautionary examples of what happens when this is not done first.
2. **Sim as a headless library; renderer reads snapshots and interpolates.** The Remaster needed
   ~7.8k lines of glue to separate C&C's sim from its drawing. [01, 03, 06, 10, 14, 15]
3. **Every cross-entity reference is a typed handle (kind, index, generation), never a pointer.**
   C&C's 16-bit TARGET made saves, networking and replays work; it lacked a generation counter and
   paid for it. [01, 03, 21]
4. **All game data in text files with named fields: units, weapons, warhead×armour matrix, terrain
   cost table, AI parameters, difficulty profiles.** C&C's 30-positional-boolean constructors are the
   thing every study calls the worst part; RA's RULES.INI is the thing every study calls the best
   retrofit. [02, 06, 10, 15, 20, 21, 23]
5. **Tech tree and campaign unlocks as data "effects" or "patches"** (`{op, target, attribute,
   value}` lists granted by a building, a research, or a mission start). This is how AoE2, openage's
   nyan and AoE4 all do it, and it is exactly what mission-by-mission campaign unlocks need. Keep one
   skirmish tech tree; each campaign mission grants a subset. [02, 04, 16, 20, 21]
6. **Object model: data-oriented composition, not a deep class hierarchy and not a general ECS
   framework.** Components in typed slot arrays, systems called in a fixed order, a per-entity mission
   state machine with a wake-up tick (C&C's best idea). [01, 10, 20]
7. **Cell-based movement with integer sub-cell positions**, one vehicle per cell, a few infantry
   sub-slots, crowding handled by rules not physics. Replace C&C's wall-following pathfinder with
   grid A* (budgeted per tick) now, flow fields for big groups later, behind an interface. [02, 11,
   20, 21]
8. **Economy: one harvested field resource, harvester trips, refinery, capped storage plus uncapped
   cash, cost drained over build time, power as a gate.** Per-cell density, slow seeded regrowth as a
   per-map setting. [02, 05, 13]
9. **Skirmish AI: manager-based (economy / production / military / scouting), a numeric capability
   profile per difficulty tier (the RA "IQ ladder" generalised), fog-honest on all non-cheat tiers,
   cheats as a separate labelled flag.** The AI issues commands through the same queue as humans.
   [03, 04, 14, 21]
10. **Mission scripting: embedded Lua with a trigger-style API** (`on_enter`, `after`, `when_destroyed`,
    `reinforce`, `objective`), run inside the sim tick with sim RNG and serialisable state; declarative
    map data for placement. [04, 10, 12, 14, 16]
11. **Maps: square top-down cell grid, C&C-style multi-cell templates with hole masks for terrain,
    separate layers for visual / passability / height, text entity file plus binary terrain layer,
    string ids, format version from day one.** Discrete cliff levels with ramps if elevation is wanted.
    [05, 12]
12. **Art: placeholders first (CC0), Blender pre-render pipeline for vehicles and buildings, hand
    pixel art for infantry and portraits, palette-indexed house colours, 8 or 16 facings, strict
    licence allow-list.** [17, 20]
13. **Campaign shape: 10–12 missions per side plus tutorial, one spine with 2–3 choice points, maps
    reused across sides with different scripts, text briefings with portraits and music, no unit
    carry-over.** [16]
14. **LAN (when it comes): lobby on host, star topology through the host, UDP broadcast discovery
    plus join-by-IP, build/rules/map hash check on join, fixed command delay of 2–4 ticks.** [03, 10, 22]
15. **Replays: header (build hash, rules hash, map hash, seed, players) + (tick, player, command)
    records + a real state hash every N ticks + a footer index; derive statistics by headless
    re-simulation.** AoE2's replays show the cost of command-only logs bound to an exact build and of
    checksums that are not state hashes. [03, 10, 24]
16. **Port algorithms and numbers from EA, not files.** Nothing in the EA tree is clean enough to
    drop in; the ideas (mission/status state machine, TARGET handles, event queue shape, armour matrix,
    harvester ring search, Tiberium sweep, IQ ladder, team scripts, trigger persistence modes) are
    all worth copying. [01, 02, 03, 04, 05, 15]

## 2. Where the research disagrees or leaves a real choice

| Topic | Positions | Files |
|---|---|---|
| **Language / stack** | C# custom on FNA or MonoGame (recommended by 15, easiest with AI assistance, matches OpenRA's proven integer-sim model); OpenRA fork or mod (fastest to playable, but bound to its engine and release cadence; 15 suggests a 2–3 week spike first); C++ with SDL3 (closest to EA source, more desync risk); Rust/Bevy (API churn risk). Godot/Unity/Unreal ruled out (determinism, licence, overkill). | 10, 15, 20, 21 |
| **Tick rate** | 15 Hz (C&C's rate, cheapest) vs 20 Hz (10's recommendation) vs 30 Hz. All agree it is a config value and that rendering interpolates. | 01, 03, 10 |
| **Pathfinding ambition** | Start with flat A* plus cache and add hierarchy/flow fields only when profiling says so (11, 20, 21) vs build flow fields from the start for large groups (11 leans this way for group orders). | 02, 11, 20, 21 |
| **Map editor** | Tiled plus importer now, own editor later (12, 15) vs own editor from the start in the game's language (05) vs fork EA's C# editor (05 says no: needs EA textures, hard-coded tables). | 05, 12, 15 |
| **Scripting** | Lua (most files) vs pure data trigger tables if bit-exact lockstep must never be at risk (04 and 14 both mention this fallback). | 04, 10, 14, 16 |
| **Income feel** | Trickle unload bail by bail (TD, 05's preference) vs lump sum at dock (RA, simpler). | 05, 13 |
| **Map size** | 64×64 (authentic, fast) vs 96×96 vs 128×128; format should allow 128 either way. | 01, 05, 12 |
| **AI authoring** | Managers in code (14) vs a `.per`-style rule script language for strategy (21 suggests a hybrid: code for mechanics, a small rule script for strategy and difficulty variants). | 14, 21 |
| **Elevation** | Cosmetic vs walls-with-ramps vs gameplay bonuses. | 12 |

## 3. Things the research flags as "decide now or pay later"

- Determinism and the command queue (D-006, consensus 1 above).
- Handles with generation counters (consensus 3).
- Data-driven rules with no compiled fallback (consensus 4).
- Separate visual / passability / height map layers (consensus 11).
- Fog-honest AI information model (retrofitting it is expensive; consensus 9).
- Map format version field and string ids (consensus 11).
- Whether mid-mission saves are required (constrains AI and script state design). [03]
- Whether scripts and AI must be bit-exact for LAN (decides Lua vs tables). [04, 14]

## 4. Design-round agenda

### Round 1 · Technology and simulation architecture (blocks everything)
- R1-1 Language and stack (Q-01).
- R1-2 How much EA code to port (Q-02).
- R1-3 Learning vs shipping: do we spend 2–3 weeks on an OpenRA spike first?
- R1-4 Object model: data-oriented structs (recommended) / ECS library / class hierarchy.
- R1-5 Movement model: cell-locked classic (recommended) / continuous.
- R1-6 Tick rate: 15 / 20 / 30 Hz.
- R1-7 Scripting: Lua with trigger API (recommended) / data tables / none at first.
- R1-8 Data format: TOML / JSON / YAML.
- R1-9 Replays as an early test tool (recommended yes); AI through the command queue (recommended yes); mid-mission saves required?
- R1-10 Mod-ability scope: balance-only files / full content packs.

### Round 2 · Game design (shapes the data and the art budget)
- Faction concept and names (Q-03); morality and relationship of the two campaigns.
- Resource model details (Q-04): regrowth, trickle vs lump, power gate, silos.
- Map scale and tile system (Q-05): size, elevation, theaters count.
- Tech tree shape: unlocks only vs stat upgrades; superweapons; naval in or out.
- Production: drain model, queues, parallel factories; friendly fire; infantry per cell.
- Fog: permanent shroud vs true fog.
- AI: difficulty ladder (how many tiers, cheats at all, personalities), how hard is Very Hard.

### Round 3 · Campaign, tooling and art
- Campaign length, structure, story delivery, voice vs text, carry-over, meta-map (Q-07).
- Map editor path (Q-06) and tooling scope.
- Art pipeline and style (Q-08): resolution, facings, Blender commitment, AI-art policy, licence stance.
- LAN scope for v1: topology, host migration, player count.

## 5. A plausible default architecture (if every recommendation is accepted)

C# (.NET, FNA or MonoGame front end); a graphics-free `Sim` library with integer cell/lepton
coordinates (1024 or 256 per cell), 20 Hz tick, command queue with execution-tick stamps, one seeded
PRNG, xxHash state hash every 16 ticks, handles with generations, components in slot arrays, systems
in a fixed order; TOML data files with one-level inheritance and effect/patch lists for techs and
mission unlocks; grid A* with per-tick budget behind a `Pathfinder` interface; single-resource
harvester economy with drain production; manager-based AI reading a per-tier capability profile;
Lua (MoonSharp or NLua) mission scripts behind a ~30-function trigger API; map = TOML entities +
binary terrain layers; Tiled for terrain prototyping, own editor sharing the sim's types later;
replay = header + (tick, player, command) + periodic hashes; LAN later as host-star lockstep.
