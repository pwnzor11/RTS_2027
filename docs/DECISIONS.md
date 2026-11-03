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

### D-029 · 2026-11-02 · Faction names (working, approved)
**Decision:** Protagonist: the **Continental Reconstruction Authority** ("the Authority", tag CRA).
Antagonist: the **Directorate of the Interior** ("the Directorate", tag DOI). Owner approved keeping
them; can be revisited before the first public build without code impact (names live in data).
**Status:** confirmed

### D-030 · 2026-11-02 · Campaign structure
**Decision:** 10–12 missions per side plus a short tutorial; one spine per side with 2–3 two-way
choice points that rejoin the spine; maps reused across the two campaigns with different scripts;
mix of roughly half build-and-destroy, a quarter defend/escort, a quarter commando/no-base; no unit
carry-over. Story delivery: text briefings with portraits and music, in-mission radio messages,
2–3 still-art cutscenes at act breaks; period flavour (teletype briefings, Directorate propaganda
radio). All text in one locale file so voice can be added later. Q-07.
**Status:** confirmed

### D-031 · 2026-11-02 · Map editor path
**Decision:** Tiled for terrain painting with a JSON importer now; triggers and placement in TOML by
hand; our own C# editor sharing the sim's types once the sim exists. EA's editor is a feature
checklist only, not forked. Q-06.
**Status:** confirmed

### D-032 · 2026-11-02 · Art direction and production
**Decision:** HD look (sprites authored at 2–4× the classic 24-px cell; exact cell pixel size chosen
in the art pilot), 16 facings for vehicles, 8 for infantry. The lead produces all artwork; the owner
approves designs. Blender pre-render pipeline for vehicles, aircraft and buildings where possible;
hand/AI-assisted pixel work for infantry, icons, portraits. **AI generation is permitted for all
art** (concepts, portraits, briefing art, textures, and sprite frames where consistency allows),
disclosed in `docs/CREDITS.md`. Rationale: open-source hobby game for private play, not sold. Q-08.
**Why:** Owner's choice. Note recorded for the owner: AI-generated images have an unsettled copyright
status; for a GPL project that mostly means they may be effectively public domain, which does not
restrict distribution. Some open-source communities object to AI art; irrelevant for private play.
**Status:** confirmed

### D-033 · 2026-11-02 · Licence allow-list for imported assets
**Decision:** Imported third-party assets must be CC0, MIT, BSD, GPL-2+/GPL-3, OFL, or CC BY-SA 4.0
(with attribution). CC BY-SA 3.0, unlicensed packs and EA material are excluded. Every import gets a
line in `docs/CREDITS.md`.
**Status:** assumed — confirm (owner did not answer this sub-question; this is the research's option (b))

### D-034 · 2026-11-02 · LAN scope for v1
**Decision:** Host-star topology (clients talk only to the host), 2–4 humans plus AI slots in one
lobby, UDP broadcast/multicast discovery plus join-by-IP, no host migration (host leaving ends the
game; protocol leaves room for a manual claim later), build/rules/map hash check on join. Built after
skirmish and campaign; the sim and lobby data are shaped for it from the start. Q-23.
**Status:** confirmed

### D-035 · 2026-11-02 · Locomotion classes include "burrow"
**Decision:** The v1 locomotion classes are foot, wheeled, tracked, hover, air, burrow (Directorate
tunnelers, moves under terrain, surfaces on soft ground), with a naval slot reserved. Extends D-023.
**Status:** confirmed (follows from D-027's tunnelling faction; owner approved the setting)

### D-036 · 2026-11-02 · D-033 confirmed; setting draft accepted
**Decision:** The owner confirmed D-033 (asset licence allow-list incl. CC BY-SA 4.0). The owner
accepted `design/03_setting_and_factions.md` as drafted (tunnel network, radiation denial, conscript
swarm stay); refinements happen in the roster pass. Q-25.
**Status:** confirmed

### D-037 · 2026-11-02 · Art pilot arms without an image generator
**Decision:** No image-generation tool or connector is available to the lead (skills and connector
registry searched in session 2). The M0-12 pilot therefore compares (a) Blender pre-render,
(b) the same render with a pixel-art post-process (palette quantise, hard outline, no AA) and
(c) hand-placed pixel art. AI-generated frames stay allowed by D-032 and can be added if a tool
becomes available or the owner supplies frames. Owner's instruction: "make the art however you are
able". Q-24 still decided by the pilot.
**Status:** confirmed

### D-038 · 2026-11-02 · Skeleton conventions
**Decision:** Solution file `RTS_2027.slnx` (the .NET 10 default format); namespaces `Rts.<Project>`;
`Directory.Build.props` sets net10.0, nullable, warnings as errors, invariant globalization;
`global.json` pins SDK 10.0.100 with latestFeature roll-forward. Contract types live in
`src/Sim/Core/` (EntityId, Command, CellCoord/LeptonCoord, SimRandom = PCG32, SimSnapshot, SimSetup).
State hash uses `System.IO.Hashing.XxHash64`. Projects for Scripting/AI/Net are added when first needed.
**Status:** lead decision (easy to reverse)

### D-039 · 2026-11-02 · Sprite style and cell size from the art pilot
**Decision:** Arm B (Blender pre-render → pixel post-process: fixed palette, 1-px outline, hard alpha)
at a **64 px cell**. Vehicles render at 4× and downscale; frame canvas 1.5 × cell (96 px); 16 facings,
frame 0 = north, clockwise; house colour is an 8-shade magenta ramp remapped at runtime (D-032).
Pipeline scripts in `tools/art/`. Follow-up polish (bigger house panel, lower camera ~40°, more hull
and wheel detail) is roadmap item M1-ART-01. Q-24.
**Status:** confirmed (owner's pick)

### D-040 · 2026-11-02 · Overnight M1 run rules
**Decision:** M1 is built in one unattended session. Small design choices (placeholder speeds, damage,
footprints, map-format details, test-map layouts) take the lead's recommendation; placeholder numbers
are marked as fixtures and each choice is logged "assumed — confirm" for the owner's morning review.
Finished waves land by direct push to main after full build, tests, determinism scan and green
Windows CI; a restore tag `pre-m1` is set first. Session ends with the wrap-up checklist.
**Status:** confirmed (owner)

### D-041 · 2026-11-02 · Debug view draws vehicles with the D-039 tank sprite
**Decision:** The M1 debug view draws every vehicle with the D-039 tank sprite and the house-colour
remap; infantry and buildings stay as squares until M2. Q-26.
**Status:** confirmed (owner)

### D-042 · 2026-11-02 · Tracked vehicles turn, then drive
**Decision:** Classic feel: a tracked or wheeled vehicle rotates in place toward its next waypoint
(turn rate from data), then moves; only small corrections happen while moving.
**Status:** confirmed (owner)

### D-043 · 2026-11-02 · Reference tank sprite is v2 without dither
**Decision:** `art/pilot/B_pixel_64_v2.png` (with `sprite_meta_example.toml`) is the reference
vehicle sprite for the debug view and future fan-out. The ordered-dither variant is not used (noisy at
1×). Next polish notes (front house band visible from N/S, smaller reactor drum in N/S silhouette,
shadow crop on the S facing) go to the M2 art pass.
**Status:** assumed — confirm (lead's call under D-040)


### D-044 · 2026-11-02 · M1 wave A details chosen overnight
**Decision:** Lead/helper choices made under D-040 while building M1 wave A:
- M1-01 map generator: sizes 24..128; starts at (4,4)+mirror with 7x7 clear box, features outside 11x11; cliff = 1-cell ring around central plateau (height 1, n/4 in) with two 3-wide ramps (left edge y=n/4+n/8 and mirror); pitchblende 2x2 pits density 8..12 at start+(4,1) and (n/2-4,n/2-2) + mirrors; ashfall 3..5 blobs/half radius 1..2 density 3..12; 1 pond/half r2; rock 2..3/half r0..1; rough 3..4/half r2; 4-way connectivity check for tracked, retry seed+1 ×10; map name part of map hash.
- M1-01 Wall overlay blocks all non-air; MapState.Version bumps on wall/building/land changes, not resource changes.
- A0 default terrain: air 100% everywhere; hover 100% on water; rock/cliff 0 for non-air; rough 75; road 125 wheeled/110 others.
- M1-03: a command's mission starts on its ExecTick (promoted in the same tick's RunMissions); handler-queued missions start next tick. Turn-then-drive threshold: turn when facing error > 16 (one 16-facing step), turn until exactly aligned, aligning tick doesn't move; errors ≤16 corrected while driving. Arrive/Stop queue Guard (one extra tick in Move/Stop). Move/Stop commands carry Arg = ActorKind of the subject.
- M1-02: placeholder rules fixtures (test_* weapons/warheads/vehicles/infantry/buildings, values in the brief); building footprint keys footprint_w/footprint_h (1..6); terrain speed % range 0..1000; RulesHash FormatVersion 3 (old saves/replays invalid — none exist yet).
- M1-04: debug view selects only player 0's units; commands stay queued until their ExecTick; zoom steps 0.5/1/2; generator connectivity uses its own conservative ground rule (lead fix).
- Map generator connectivity uses its own conservative ground rule so generation never depends on rules data.
- Text data files are LF on every OS (`.gitattributes`); tests normalise line endings.
**Status:** assumed — confirm


### D-045 · 2026-11-03 · M1 wave B details chosen overnight
**Decision:** Choices made under D-040 while building M1 wave B:
- M1-05 pathing: group requests not cached, blocked/off-map/out-of-box group starts → NotFound, blocked goal → NotFound for all; single Partial whose closest cell is the start → NotFound; from==to → Found, empty path; start cell passability not checked; cost factor max(1, 10000/speed%) of the entered cell; heuristic octile × min factor; cache cleared at 4096 entries (speed only).
- M1-07 vision: sight capped 0..64 cells; disc = dx²+dy² ≤ s²+s.
- M1-06 buildings: no adjacency, prerequisite or cost check on placement in M1 (M2); units on a footprint block placement; the one-cell gap rule counts 4-way components of tracked-passable, building-free cells before and after.
- Lead integration: state parts are [map, units, infantry, buildings, paths, vision, then combat parts]; vision updates at the end of cleanup every tick; enemy buildings become ghosts when no footprint cell is visible; a ghost is forgotten once its footprint is visible again and no matching building stands there.
**Status:** assumed — confirm


### D-046 · 2026-11-03 · M1 wave C details chosen overnight
**Decision:** Choices made under D-040 while building M1 wave C (movement, combat, desync tooling):
- M1-09 combat: instant splash-0 weapons hit only the target; projectile splash-0 hits every eligible actor in the impact cell (no homing, can be dodged); neighbour splash = half damage (min 1); instant splash weapons detonate at the target point. Attack orders: rejected on own/dead targets or weaponless subjects, ignore fog; Guard/AttackMove/building acquisition need vision. AttackMove scans every 5 ticks; after a kill or arrival the unit guards in place. Guard: nearest target within [MinRange, Range], ties by slot order (vehicles, infantry, buildings); chase leash = Sight cells from guard point; target leaving vision is dropped; Guard command walks to its point; being hit wakes a guard. Inside MinRange: no retreat. Full projectile store: weapon doesn't fire. Projectiles round to nearest per axis, detonate when remaining ≤ speed (diagonals may be ±1 tick), move on their firing tick. Reload = exactly RateOfFire ticks.
- M1-10 tooling: replay format 5 needs --map (default test_64) and checks MapHash; match "alive" counts vehicles+infantry; hash coverage allow-lists vision ghosts (focused test) and path-search scratch arrays.
- M1-08 movement: a Move takes effect one tick after its command (path request in RunMovement); steps end on cell centres; speed uses the cell being entered; crush on reservation; vehicles never share a cell with live infantry (ally infantry asked to scatter), infantry never enter vehicle cells, enemy/ally infantry may share; idle = no queued mission and Mission None/Sleep/Guard/Stop (attackers are busy, waited for then avoided); blocked by enemy/unshovable: wait 10 ticks → sidestep → repath; head-on allied movers: higher slot yields; give up after 12 stuck expiries → Guard; final cell taken: wait for moving ally, else stop where it is after 10 ticks; destination spreading only for ≥2 same-player Moves to one cell in one tick, by command order.
- Known issue (for M2): headless match on test_64 favours the (4,4) start corner whichever player has it; mirror duels on flat maps are exactly symmetric, so the cause is geometric (path tie-breaking / map features).
**Status:** assumed — confirm


### D-047 · 2026-11-03 · M1 wave D (sandbox) details chosen overnight
**Decision:** Choices made under D-040 for the M1-11 battle sandbox: the game starts in sandbox mode on
test_32 (3 tanks, 2 jeeps, 1 artillery truck, 5 riflemen and a turret per side; player 1 attack-moves at
tick 40; `--empty` gives the old 3-tank setup, `--demo` the orbit demo); WASD panning is removed because
S/A/G/X are order keys (arrows and middle-drag still pan); A + right-click on an enemy issues Attack;
health bars are not drawn on ghosts; the F3 occupancy overlay under a fog viewer shows only cells that
viewer can see; the replay is written to `replays/sandbox_last.rpl` on exit. CI runs the sandbox
headless (`--headless-check 1200`), a 1500-tick match, and verifies both replays.
**Status:** assumed — confirm


### D-048 · 2026-11-03 · Corrections to D-040 and D-047
**Decision:** (1) D-040's restore point is the branch `restore/pre-m1` (3279113), not a tag: the workspace
git proxy blocks tag pushes. (2) D-047's controls, as built: right-click on an enemy issues Attack (with
or without A); A arms attack-move (a toggle, not held) and the next right-click on ground issues
AttackMove. (3) Splash, as built: impact cell full damage, every other cell within the warhead's Splash
radius (Chebyshev) half damage, minimum 1.
**Status:** confirmed (records what was done; D-047 itself stays "assumed — confirm")

### D-049 · 2026-11-03 · Owner confirms D-043..D-047
**Decision:** The owner accepted all five overnight decisions (D-043 reference sprite, D-044 map/rules
details, D-045 pathing/vision/buildings, D-046 combat/movement/tooling, D-047 sandbox) as recorded,
with D-048's corrections. Expected to change later and not reopened by this entry: the guard leash and
stuck-retry numbers (retune in M2), and the sandbox as the default boot mode (replaced in M2).
**Why:** reviewed in session 3 as one table; the owner took every recommendation.
**Status:** confirmed

### D-050 · 2026-11-03 · Keep MonoGame; EA source is reference only, never ported
**Decision:** The renderer stays on MonoGame. Because MonoGame's licence (Ms-PL, MIT for portions)
is classed as GPL-incompatible, no EA-licensed code is ever copied or ported into this repo. The EA
source remains a read-only reference for studying mechanics; everything here is written fresh.
`docs/LICENSING.md`'s register of EA-derived files therefore stays empty by rule.
**Why:** adversarial review 2026-11-03, finding 1 (E-1 corrected). Switching renderers costs more
than giving up verbatim reuse, which M1 never needed. Not legal advice.
**Alternatives:** switch to SDL3/Silk.NET bindings; add a GPL §7 linking permission and port anyway
(not possible for EA code, which we cannot relicense).
**Status:** confirmed (owner, session 3)

### D-051 · 2026-11-03 · Skirmish AI is an external command producer
**Decision:** AI code lives outside the simulation (`src/AI`), reads a fog-filtered read-only player
view, and issues ordinary `Command`s into the same recorded stream as human input. It never runs
inside `Step`. AI memory is not part of the sim hash; saves carry it in their own section. In LAN
one peer (the host or the slot owner) runs each AI. `RunAiBookkeeping` in `Step` is removed from the
tick order. Lua missions remain the only in-`Step` extension and get their own decision before M4.
**Why:** replays and determinism then do not depend on AI code; AI can be changed, swapped and
tested without invalidating recordings (review A-1, B-7). Supersedes architecture §3's "AI runs
inside Step".
**Status:** confirmed (owner, session 3)

### D-052 · 2026-11-03 · M1.5 hardening batch before M2
**Decision:** A hardening milestone (M1.5) runs before M2 planning, built from the 2026-11-03
adversarial review: player relations (ally/enemy/neutral, teams), per-player state part, actor-store
abstraction, sim boundary guards, save/replay robustness, publish/licence/notice fixes, test fixture
split, build stamping, debug console, and a headless AI-vs-AI harness with a difficulty schema.
**Why:** each item is cheap now and expensive after M2 adds economy, production and AI on top.
**Status:** confirmed (owner, session 3)

### D-053 · 2026-11-03 · M2 scope choices: Authority first, Rust Belt, Recruit-tier AI
**Decision:** M2 builds the **Authority** as its faction, on a **Rust Belt** map. The M2 AI is the real
framework (managers, `difficulty.toml`, fog-filtered player view) with only tier 1 (*Recruit*)
implemented; it runs in the M1.5 headless harness. The Directorate's harvesting, when it is built, is
**tunnel-borne scrapers** that surface at a drift and dive back to an entrance that acts as the
refinery, reusing the harvester loop with burrow locomotion.
**Why:** the conventional loop is what both factions need and the tunnel spec is not on M2's path;
Rust Belt matches the Authority campaign opening; building the AI framework once avoids M3 rework.
**Alternatives:** Directorate first; both minimal; conscript gangs + mobile smelter; scripted waves.
**Status:** confirmed (owner, session 3)

### D-054 · 2026-11-03 · Fixed teams at setup; neutral is a reserved player slot
**Decision:** `SimSetup` carries one team id per player slot, hashed. Relation(a,b) is Self, Ally
(same team), Enemy, or Neutral. Allies share vision and are never auto-targeted; splash still hurts
them per the warhead's `HitsAllies`. Neutral (civilians, tech buildings) is a reserved player slot
that nothing targets automatically and that targets nothing. No in-game diplomacy.
**Why:** classic C&C model; one small table instead of dynamic state (review B-2).
**Status:** confirmed (owner, session 3)

### D-055 · 2026-11-03 · GPL §7 additional permission for linking MonoGame
**Decision:** Our LICENSE gains a standard GPL §7 additional permission allowing the program to be
combined with and distributed alongside MonoGame (and its bundled native libraries) regardless of
their licences. The permission covers only code we hold copyright on, which under D-050 is all of it.
Not legal advice; the owner may have the wording checked.
**Status:** confirmed (owner, session 3)

### D-056 · 2026-11-03 · Git LFS for rendered sprite sheets
**Decision:** `data/sprites/**/*.png` and `art/**/*.png` are tracked with Git LFS. Blender sources,
scripts and TOML metadata stay in plain git. The owner runs `git lfs install` once on Windows; CI
checks out with `lfs: true`. The owner declined building an M2 system himself (review D-7); agents
keep building and he studies code and decisions.
**Status:** confirmed (owner, session 3)
