# Decisions
Numbered, never edited. A reversal is a new entry that supersedes the old one.

### D-001 · 2026-11-02 · Open source forever, GPL v3
**Decision:** RTS_2027 is open source under GPL v3 and stays that way.
**Why:** Owner's explicit wish. Also lets us reuse/port code from EA's GPL v3 C&C Remastered source.
**Status:** confirmed

### D-002 · 2026-11-02 · EA C&C Remastered source may be reused, under its terms
**Decision:** We may copy or port code from the EA repo. Every derived file is marked and listed in
`docs/LICENSING.md`. No EA trademarks, no EA assets.
**Why:** Owner chose reuse over reference-only. The additional terms are easy to honour.
**Alternatives:** Reference only (cleanest, slower).
**Status:** confirmed

### D-003 · 2026-11-02 · Windows only
**Decision:** Target Windows only. Cross-platform is not a goal.
**Why:** Everyone the owner wants to play with has Windows.
**Status:** confirmed

### D-004 · 2026-11-02 · 2D sprites, classic look
**Decision:** 2D sprite rendering like the original games, not 3D.
**Why:** Owner's preference; also far cheaper for a hobby team to produce art for.
**Status:** confirmed

### D-005 · 2026-11-02 · Mode priorities: campaign > skirmish > LAN
**Decision:** Build order and design weight: (1) two-faction single-player campaign with story and a
tech tree that unlocks across missions; (2) skirmish vs AI with several difficulty levels that give
the AI real capability upgrades, full tech from the start; (3) LAN multiplayer for home network play.
No public servers, matchmaking or hosting.
**Why:** Owner's stated priorities. LAN is "would be cool", not required.
**Status:** confirmed

### D-006 · 2026-11-02 · Deterministic simulation from day one
**Decision:** The game simulation is deterministic: integer/fixed-point math, seeded RNG, no
wall-clock or float in sim code, rendering decoupled from sim ticks.
**Why:** Even though LAN is lowest priority, lockstep networking (how C&C and every classic RTS does
multiplayer) depends on determinism and cannot be added later. It also gives replays and reproducible
bug reports for free, which matters for AI development.
**Alternatives:** Non-deterministic sim with client-server state sync (heavier, unusual for RTS).
**Status:** confirmed (owner asked for LAN as an option; this keeps it open at near-zero cost)

### D-007 · 2026-11-02 · Research and design docs live in the repo
**Decision:** Research write-ups go in `docs/research/`, design decisions in `docs/design/`, alongside
the handoff docs. No separate external document.
**Why:** Owner's choice; keeps everything in one place and under version control.
**Status:** confirmed

### D-008 · 2026-11-02 · Design rounds before any game code
**Decision:** Several rounds of design discussion with the owner happen before building. Session 1
produces research and a list of design questions; later sessions settle them.
**Why:** Owner's explicit request.
**Status:** confirmed

### D-009 · 2026-11-02 · C#: sim library + thin FNA/MonoGame renderer
**Decision:** The game is written in C# (.NET). The simulation is a graphics-free class library; a
thin XNA-style front end (MonoGame or FNA) renders it. Q-01.
**Why:** Fastest iteration, OpenRA proves the C#-integer-sim model for this genre, best fit for
AI-assisted development. Research 15 scoring table.
**Alternatives:** OpenRA fork; C++/SDL3; Rust/Bevy; Godot/Unity/Unreal (ruled out).
**Status:** confirmed

### D-010 · 2026-11-02 · No OpenRA spike; build our own engine
**Decision:** We do not prototype on OpenRA first. Q-09.
**Why:** The owner's goal is to learn by building; OpenRA hides most of that.
**Status:** confirmed

### D-011 · 2026-11-02 · Reuse from other repos = algorithms and numbers, re-implemented
**Decision:** From the EA source (and openage, freeaoe, aoc-mgz, AOEMods.Essence) we take ideas,
algorithms and tuning numbers and re-implement them; we do not port files. Each borrowed algorithm
is still noted in `docs/LICENSING.md`. "Learn from these repos, don't copy their mistakes." Q-02.
**Status:** confirmed

### D-012 · 2026-11-02 · Object model: data-oriented components in slot arrays + mission state machine
**Decision:** Entities are ids into preallocated slot arrays of component structs; systems run in a
fixed documented order; each entity has a per-entity mission/status state machine with a wake-up
tick (C&C's `Mission`/`Status`/sleep model). No deep class hierarchy, no general ECS framework. Q-10.
**Status:** confirmed

### D-013 · 2026-11-02 · Cell-locked classic movement
**Decision:** One vehicle per cell, a fixed small number of infantry sub-slots per cell, integer
sub-cell positions for rendering, crowding resolved by rules (wait / sidestep / shove idle allies),
not by physics. Q-11.
**Status:** confirmed

### D-014 · 2026-11-02 · Sim tick 20 Hz, command delay 2 ticks
**Decision:** The sim steps at 20 Hz (50 ms). Commands execute at `tick + 1` in single player and
`tick + 2..4` on LAN. Both are config values. Q-12.
**Why:** Smoother interpolation and faster order response than C&C's 15 Hz; cheaper for pathfinding,
AI and state hashing than 30 Hz; 50 ms divides evenly into 1 s. Owner deferred to the lead on this.
**Status:** confirmed

### D-015 · 2026-11-02 · Mission scripting in embedded Lua behind a trigger-style API
**Decision:** Missions are Lua scripts using a small (~30 function) API: triggers, timers,
reinforcements, objectives, media, tech unlocks. Lua runs inside the sim tick, uses the sim RNG,
has no wall-clock access, and keeps all mutable state in plain tables so it can be saved and hashed. Q-13.
**Status:** confirmed

### D-016 · 2026-11-02 · Data format TOML
**Decision:** Units, weapons, warheads, armour matrix, terrain costs, AI profiles, difficulty profiles
and tech effects live in TOML with string ids, one level of inheritance (`base = "..."`) and
effect/patch lists. Q-14.
**Status:** confirmed (owner waved through)

### D-017 · 2026-11-02 · Replays from day one; AI uses the player command queue
**Decision:** The sim records (tick, player, command) plus a periodic state hash from the first
playable build, as a test and debugging tool. The skirmish AI issues commands through the same queue
as human players. Q-15, Q-16.
**Status:** confirmed (owner waved through)

### D-018 · 2026-11-02 · Mid-mission saves are required
**Decision:** Any mission can be saved and resumed. Therefore all sim state, RNG state, AI memory and
Lua script state are serialisable by field with a version number. Q-17.
**Why:** Owner requirement. Constrains AI and scripting design from the start.
**Status:** confirmed

### D-019 · 2026-11-02 · No third-party modding support
**Decision:** Data lives in external TOML/Lua for our own convenience, but there is no mod loader,
no Lua sandbox for untrusted scripts, no content-pack system and no API stability promise. Q-18.
**Why:** Owner's choice; removes a large amount of tooling and sandboxing work.
**Status:** confirmed

### D-020 · 2026-11-02 · Two factions, StarCraft-style asymmetry, clear protagonist and antagonist
**Decision:** Exactly two factions that play deeply differently (distinct mechanics, not re-skins).
Campaigns are parallel and opposed (both sides of one war). One side is the clear protagonist, the
other the clear antagonist, and the antagonist is designed to be the more fun side to play. Setting
is near-future military sci-fi, possibly alt-history; chosen in round 3. Q-03 (partial).
**Why:** Owner's choice; two factions keeps deep asymmetry affordable.
**Status:** confirmed

### D-021 · 2026-11-02 · Economy defaults, resource identity deferred to theme
**Decision:** One harvested field resource, harvester trips, trickle unload, capped storage with silos
and overflow loss, regrowth on by default as a per-map setting; harder AI gets more harvesters under
the same rules, never a hidden income bonus. What the resource *is* (and whether the two factions
harvest it differently) is decided with the setting. Q-04.
**Status:** confirmed

### D-022 · 2026-11-02 · Map scale 128×128 max, cliffs as walls with ramps, two theaters
**Decision:** Map format and sim support 128×128; early skirmish maps 64–96. Elevation is discrete
cliff levels acting as walls, with ramps; no height stat bonuses. Two theaters for v1, each with an
internally consistent theme and design rules. Q-05.
**Status:** confirmed

### D-023 · 2026-11-02 · Tech tree: unlocks and stat upgrades; two hard-to-get superweapons per side; no navy in v1
**Decision:** Techs include both unlocks (building X enables Y) and stat upgrades (research applies
effects). Each faction has exactly two superweapons that are expensive and deep in the tree. No naval
units in v1, but locomotion classes (tracked, wheeled, foot, hover, air, and the data slot for naval)
are first-class in the terrain-cost table from the start. Q-19.
**Status:** confirmed

### D-024 · 2026-11-02 · Production details
**Decision:** Cost drains over build time, production stalls when broke; one queue per category
(infantry, vehicles, aircraft, structures, defences); extra factories speed the queue; five infantry
per cell; splash damage hits allies. Q-20.
**Status:** confirmed

### D-025 · 2026-11-02 · Shroud plus true fog of war
**Decision:** Unexplored shroud, plus true fog: enemy units are hidden when out of sight, buildings
remain as last-seen ghosts. The AI's world model obeys the same visibility. Q-21.
**Status:** confirmed

### D-026 · 2026-11-02 · AI difficulty: five fog-honest capability tiers, three personalities, expert top tier
**Decision:** Five difficulty tiers, none of which cheat; each tier adds real behaviours (capability
profile in data). An optional, labelled "cheater" resource bonus toggle may be added if playtesting
wants it. Three personalities (rusher, turtle, boomer) selectable or random per game. The top tier is
tuned as an expert challenge. Q-22.
**Status:** confirmed

### D-027 · 2026-11-02 · Setting: alt-history atompunk, 1962 war, campaign set c. 2001
**Decision:** The Cuban Missile Crisis went nuclear in 1962. The game is set about forty years later
on a scarred Earth where microelectronics never happened: hardened vacuum-tube electronics,
nuclear-powered vehicles, 1960s design language pushed to extremes. Protagonist faction (working
name: the Continental Reconstruction Authority) holds and rebuilds the surface with conventional
heavy forces. Antagonist faction (working name: the Directorate of the Interior) is a paranoid,
theatrical bunker-state that fights from underground with tunnelling units, radiation weapons and
mass conscripts, built to be the more fun side. Two theaters: Rust Belt (ruined industrial cities,
snow) and Glass Coast (fused desert, drowned suburbs). Names are placeholders until round 3 settles them.
**Why:** Owner's choice among four proposals; "loved C".
**Alternatives:** The Thaw (Arctic corporate war), Bloom (fungal bio vs machine), Skyfall (orbital collapse).
**Status:** confirmed

### D-028 · 2026-11-02 · Resource: Ashfall, with pitchblende pits as the rich tier
**Decision:** The single field resource is **ashfall**: radioactive ash that drifts into fields and
is scraped up and refined into fissile concentrate (credits). Pitchblende (raw uranium ore) pits are
the rare high-density tier of the same resource. Ashfall fields can regrow via weather deposition
(per-map setting, D-021); pitchblende does not regrow. Ashfall is hazardous to unprotected infantry.
Resolves the resource identity left open in D-021.
**Why:** Fits the nuclear-powered world ("the war's cause is the war's fuel"), gives regrowth a
thematic reason, and gives maps fought-over rich spots.
**Alternatives:** pitchblende only; hot glass crater fields; tar sands; clean soil; salvage.
**Status:** confirmed
