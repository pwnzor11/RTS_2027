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
- Known issue (for M2; causes found and fixed or logged in D-063): headless match on test_64 favours the (4,4) start corner whichever player has it; mirror duels on flat maps are exactly symmetric, so the cause is geometric (path tie-breaking / map features).
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

### D-057 · 2026-11-03 · M1.5 details chosen while building
**Decision:** choices made by the lead and helpers under D-052:
- Relations (M1.5-01): Neutral wins over Ally in RelationOf (a neutral slot is neutral to everyone). Vehicles crush Enemy and Neutral infantry, not Ally. Neutral units block movement like enemies. Team vision is an OR over team members at query time; exploration (shroud) is shared too; per-player vision data and hashes are unchanged. Neutral buildings are ghosted like enemy ones.
- Guards (M1.5-02): store arrays stay public readonly (≈150 read sites), but every mutator (Allocate/Add/Remove) is internal. Commands with an out-of-map cell (Move, AttackMove, Guard) are dropped. Debug spawns have no occupancy check. LateCommandCount is diagnostic, not saved.
- Replay/save v6 (M1.5-03): spawns are streamed as spawn records stamped with their tick; the full-hash definition is unchanged (no hash moved); maps are not embedded (`replay verify` still needs `--map`); a loaded sim starts with an empty SetupLog.
- AI (M1.5-06): the AI match runner lives in `src/AI` (start force copied from `match`); `DifficultyProfile` lives in `Rts.Data` (lead fix: Data must not depend on AI); a side without a tier plays *regular*; AI randomness is its own PCG seeded from (game seed, slot).
- Console (M1.5-07): count ≤ 50 per spawn, step ≤ 1000 ticks; Esc closes the console without quitting.
- Hash allocation bound in tests is ≤ 1 KB per call (constant, not exact): runtimes differ.
- Git LFS (D-056) is not yet applied: the workspace proxy blocks LFS uploads, so the owner converts from Windows.
**Status:** confirmed (owner, session 3)

### D-058 · 2026-11-03 · M2 Authority roster: the classic minimum
**Decision:** M2 builds 8 Authority structures — Construction Yard, Power Plant, Refinery (ships with one free
scraper), Barracks, Vehicle Factory, Silo, Wall, Gun Turret — and 5 units — Scraper (harvester), Rifleman,
Rocket infantry, Jeep, Medium Tank. Ids and display names are placeholders until the naming pass (design 03).
**Why:** the smallest set that exercises the full C&C loop (power, economy, storage, production queues, defence).
**Alternatives:** lean 5+3; classic + artillery and engineer.
**Status:** confirmed (owner, session 3; answers Q-27)

### D-059 · 2026-11-03 · v1 content budget: fuller
**Decision:** v1 targets per faction ~12 units and ~16 structures; vehicles 16 facings, infantry 8 (D-032);
announcer voice lines (commissioned or TTS — source decided under Q-31) and a few original music tracks.
Real hours per sprite are logged from M2 on so the budget can be checked against reality.
**Status:** confirmed (owner, session 3; answers Q-28)

### D-060 · 2026-11-03 · Licence version: GPL-3.0-or-later; SPDX headers
**Decision:** RTS_2027 is licensed GPL v3 or (at your option) any later version, with the §7 MonoGame
permission (D-055). Every source file carries `// SPDX-License-Identifier: GPL-3.0-or-later` and
`// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).`; a test enforces it.
**Status:** confirmed (owner, session 3; answers Q-33)

### D-061 · 2026-11-03 · Campaign pilot in M2; honest top AI tier
**Decision:** (1) The last M2 item is a campaign pilot: one scripted Authority mission with a briefing on the M2
map, with hours logged, to measure mission cost (answers Q-30). (2) The *Commissar* tier gets better decisions
only — never resource, vision or speed bonuses (answers Q-32).
**Status:** confirmed (owner, session 3)

### D-062 · 2026-11-03 · M2 owner choices: real art, skirmish menu, win rule, build radius
**Decision:** (1) All 13 M2 types (D-058) get real Blender art at the tank's quality during M2. Hard rule kept:
one structure (the Refinery) is piloted first and approved by the owner before the other 12 fan out.
(2) Games start from a simple menu: Main (Skirmish / Sandbox / Quit) → Skirmish setup (map seed, AI tier,
colour) → game; pause menu with Restart / Quit to menu; a game-over screen. (3) A player is defeated when
they own no structures; walls don't count. (4) New structures must be placed with their footprint within
2 cells (Chebyshev) of a structure the player owns; walls don't extend the radius.
**Status:** confirmed (owner, session 3)

### D-063 · 2026-11-03 · M2-01 corner advantage: causes found, slot-order bias left as a finding
**Decision:** The D-046 "corner advantage" had five geometric causes, all fixed: (1) setup-spawned actors faced north
(facing 0), so the north-west force stood turned away from its enemy and the south-east one faced it; they now face the map
centre (Simulation.SpawnVehicle/SpawnInfantry; tests that assume a north start set facing 0 themselves); (2) the `match`
army's infantry sub-slots were not point-mirrored; (3) A* ties went to the lower cell index, and group-walk and detour ties to
scan order (north first), none of them rotation-symmetric: ties now go to the cell nearest the straight start-goal line, then
the left-hand side; AcquireTarget ties go to the weaker target, then slot order; (4) infantry took "first free" sub-slot
(NW before SE): now nearest free; (5) PathService.Process stopped mid-queue when the budget ran out, so the lowest slots
moved several ticks earlier: it now solves everything queued when budget remains and carries the overdraft as debt.
With these, mirrored armies move as exact mirror images until first contact (about tick 260; test MirrorSymmetryTests).
Finding (not fixed): after contact the player in slot 0 wins about 72% of decisive `skirmisher:2` mirror games (46 of 62 over
32 seeds, 64 games, either corner), because all handlers, instant damage and target removal run in slot order within a tick.
`ai match` therefore trades slots between corners on even seeds (`--starts alt`, default; `slot` = old pairing, `swap`) and
prints wins by start corner and by slot. A fix needs simultaneous damage resolution, which is a combat-system change.
**Status:** assumed — confirm

### D-064 · 2026-11-03 · Refinery pilot verdict; 40° camera standard
**Decision:** The owner approved the Refinery's building design as the template for M2 structures, but not its
state animations: the `build` frames read as the building rising out of the ground rather than being
constructed, and the `damaged` frames don't read as damage. Both get another pilot pass (scaffolding,
framework and staged assembly for construction; structural damage — breaches, collapse, fire and smoke — for
damage) and the owner approves again before structures fan out. Vehicle fan-out (from the approved tank)
and an infantry pilot may proceed meanwhile. All sprites use the 40° camera elevation of the approved tank,
with ground depth stretched so footprints cover their cells exactly.
**Status:** confirmed (owner, session 3)

### D-065 · 2026-11-03 · Simultaneous damage resolution
**Decision:** Combat resolves damage simultaneously within a tick: all hits of a tick are collected, then
applied together, and deaths resolve together, so no player gains from slot order (M2-01 found slot 0
winning ~72% of decisive mirror games). Acceptance: mirror matches in `ai match --starts slot` are even.
**Status:** confirmed (owner, session 3)

### D-066 · 2026-11-03 · Sidebar shows every unlocked item
**Decision:** The sidebar lists every type the player has unlocked; types that can't be queued right now
(queue full, a structure waiting to be placed, a missing factory) are greyed out with the reason on hover.
**Status:** confirmed (owner, session 3)

### D-067 · 2026-11-03 · M2 wave 2 details chosen while building
**Decision:** (1) Outside setup, Place needs a Ready item; with `AllowDebug` a non-Ready Place still works (no
cost, no radius) so tests and the sandbox can stage bases. (2) A player is defeated only once they have owned a
non-wall building and then own none (building-less sandboxes never "lose"). (3) Build time = cost-derived ticks ×
low-power multiplier × 2 / (1 + factories of that category). (4) Harvesters auto-start one tick after spawning;
a harvester with no refinery idles until ordered. (5) Ashfall regrows only on cells that still hold some ash;
emptied cells stay empty (seed markers and spreading are a later design question). (6) Storage cap counts the
Storage of every live own building. (7) Setup spawns face the map centre. (8) Theater id is `rustbelt`.
**Status:** confirmed (owner, session 3 wrap-up)

### D-068 · 2026-11-03 · Vehicles accepted; Rifleman needs definition; separate house palette
**Decision:** (1) The Scraper and Jeep sprites are accepted as they are. (2) The Rifleman pilot "looks decent but a
little like a blob": it gets another pass for definition (clearer silhouette: head/helmet, separated limbs,
readable rifle, contrast between garment parts) and the owner approves again before the Rocketeer fans out.
(3) House colours are a separate palette chosen to contrast faction body paint: default slot order amber/yellow,
red, blue, white, purple, cyan, orange, pink; green is never a house colour on green-bodied factions. Faction
identity stays in the body paint.
**Status:** confirmed (owner, session 3)

### D-069 · 2026-11-03 · Construction approved; fire and smoke become separate effects; Rifleman approved
**Decision:** (1) The Refinery's second-pass construction stages are approved ("construction looks amazing now") and
are the template for all structures. (2) The baked damage fire was rejected ("that fire looks terrible"; the
24-colour art palette has no true red/orange). Fire, smoke and explosions become separate, reusable pixel-art
effect animations with their own fire palette, drawn by the renderer at anchor points listed in each sprite
manifest (`[[fx]]` tables). Baked `damaged` frames keep only structural damage (breaches, collapse, scorch,
soot). The owner approves the new fire before it ships. (3) The second-pass Rifleman is approved; the Rocketeer
is made from it.
**Why:** effects drawn by the game look consistent on every building, animate independently and cost no
re-render per building. Lead's call on the approach (reversible).
**Status:** confirmed (owner approved the effects contact sheet, session 3)

### D-070 · 2026-11-03 · M2 placeholder balance from the AI harness
**Decision:** The M2 balance numbers set by the harness pass (docs/balance/m2_balance.md: income per ash/pitchblende,
regrowth on, build time per credit, structure health, unit speeds/costs, rocket/cannon/turret damage and range, AP vs
unarmoured, Recruit attack threshold) are the working values. Targets met: Recruit-vs-Recruit 94 % decisive, average
game ≈ 11.5 min, first income ≈ 52 s, first tank ≈ 2.6 min. Recruit gains seeded variation (wave timing, unit bags) so
mirror games are decisive. These are placeholders for the owner to tune by playing.
**Status:** confirmed (owner, session 3 wrap-up)

### D-071 · 2026-11-03 · Lua mission host details (campaign pilot)
**Decision:** Missions run in MoonSharp inside `Step` through a generic `ISimExtension` (state part "mission", saved and
hashed). Sandbox: integers only across the API, no os/io/debug/load/require/coroutine/metatables/math.random, ordered
`pairs`. Scripts use their own seeded RNG (`Random.Next`), not the sim RNG (supersedes D-015's "use SimRandom" detail).
Trigger callbacks are named top-level functions so they survive save/load. Mission games record no replay yet (script
spawns aren't in the setup log). Pilot cost estimate: ≈ 10–12 h per future mission of this size (docs/missions/pilot_report.md).
**Status:** confirmed (owner, session 3 wrap-up)

### D-072 · 2026-11-03 · All M2 structures and the Rocketeer approved, with fixes
**Decision:** The owner approved the remaining seven structures and the Rocketeer with three fixes: large flat
house-colour roof panels (Construction Yard, Power Plant, Silo) use the darker house shade so they don't go pastel;
walls get vertical, corner and end variants chosen by neighbours so wall lines connect; the Rocketeer's launch flash
and backblast are smaller and less white.
**Status:** confirmed (owner, session 3)

### D-073 · 2026-11-03 · D-063 confirmed
**Decision:** The owner confirmed D-063 (M2-01 corner-advantage causes and fixes; the slot-order bias was later fixed by D-065).
**Status:** confirmed (owner, session 4)

### D-074 · 2026-11-03 · Tunnel spec (answers Q-29)
**Decision:** As written in design 05 §1. Network transit is a per-entrance queue drawn in at a points-per-second rate
(S = 1 point, M = 3) followed by a flat ~2 s underground, whatever the distance (option A3). Units have a tunnel
size S/M/L; Hatch-class entrances take S, Bore-Head-class take up to M, L never uses the network. Network-wide cap
on points in transit. If the exit is lost, units re-route to the nearest surviving entrance (D2); with none left
they are lost. Free burrowing for Burrow-locomotion units: invisible while burrowed (no trail), cannot attack or be
attacked, surfaces only on soft, unhardened ground not within 1 cell of any structure, behind a 3 s surfacing
telegraph (5 s for a new entrance, which emerges with low HP). Soft = clear, rough, ash. Authority Seismograph
reveals burrowed units in a radius. The AI is fog-honest about all of it. A later Authority weapon targets tunnels
(Seismic Mortar, M4).
**Why:** Owner's choices in session 4. Size and throughput limits were the owner's own requirements; the starting
throughput was lowered after the owner judged 2 infantry/s per Hatch too generous (a barracks makes one every 2 s).
**Alternatives:** instant transit with a cap (A1); travel time by distance (A2); a ripple trail for burrowed units
(dropped — the owner judged it unnecessary; the Seismograph is the counter).
**Status:** confirmed (owner, session 4); numbers are (tune), set by M3-14

### D-075 · 2026-11-03 · Surfacing telegraph look
**Decision:** The ground darkens, then cracks, with dust and a small one-tile shake; only where the enemy has vision.
**Status:** confirmed (owner, session 4)

### D-076 · 2026-11-03 · Hardened ground and gates
**Decision:** Authority upgrade *Foundation Grouting* hardens every soft cell fully enclosed by the team's walls,
gates and structures (option H2): nothing surfaces there and no Bore breaks through. A *Gate* wall piece lets the
owner's team through and always counts as part of the seal; enemies must destroy it. Wall and gate pieces are exempt
from the one-cell gap rule so a base can be closed (supersedes the M2 lead choice in design 04 that walls can't
seal). Breaking any segment re-opens the area.
**Why:** The owner wanted a "safe inside the walls" area, with a way for friendly units through that makes sense.
**Status:** confirmed (owner, session 4)

### D-077 · 2026-11-03 · Faction weapons from ideology
**Decision:** Authority = order, light, clean-up (Decon Truck, Seismograph, Searchlight Tower, later Seismic Mortar and
Quick-Set Foam); Directorate = secrecy, paranoia, contamination, the cult (radiation weapons and ground, Blackout
clouds, Static Broadcast minimap jamming, the Informant, the Collapse Charge, reactor meltdown, Broadcast and the
Sunrise Device). Pairs: light vs dark, clean vs contaminate, seal vs undermine, seismograph vs invisibility.
Mechanics in design 05 §3–4.
**Status:** confirmed (owner, session 4)

### D-078 · 2026-11-03 · Full Directorate roster in M3
**Decision:** M3 builds the full Directorate roster as drafted in session 4 (design 05 §5): 13 units (including the
rail-gun HQ re-imagined as a tracked land-train that deploys into the Command Shaft) and 16 structures. Harvesting
unloads at a dedicated Smelter Vent; production buildings are also tunnel entrances; new entrances are dug by a
Bore unit. Names are working names.
**Status:** confirmed (owner, session 4)

### D-079 · 2026-11-03 · Authority counters in M3; AI tiers 2–5 in M3
**Decision:** M3 adds the Authority's Seismograph, Decon Truck, Searchlight Tower, Gate and the Foundation Grouting
upgrade (with an engineering structure to research it), and AI tiers 2–5 measured with the tier matrix. The rest of
the Authority roster (incl. Seismic Mortar, Quick-Set Foam, air) is M4.
**Status:** confirmed (owner, session 4)

### D-080 · 2026-11-03 · Audio sourcing (answers Q-31)
**Decision:** Voice is text-only (supersedes the voice-line part of D-059; D-030 already allows text radio).
Music is generated with ACE-Step (Apache-2.0, local) on the owner's PC from prompts the lead writes; tracks ship
as CC0 / no rights claimed, with the tool credited (purely AI-generated works carry no US copyright). SFX: CC0
packs plus our own procedural sfxr-style sounds.
**Why:** Suno's post-2025 terms grant use rights rather than ownership, awkward for files redistributed in a public
GPL repo; ACE-Step has no output terms.
**Status:** confirmed (owner, session 4)

### D-081 · 2026-11-03 · Overnight findings policy
**Decision:** For the session 4 overnight build, real findings are fixed without asking; every change to numbers or
visible text is logged as its own decision marked "assumed — confirm".
**Status:** confirmed (owner, session 4)

### D-082 · 2026-11-04 · M3 format bump: factions in saves, replays and rules
**Decision:** RulesHash FormatVersion 5→6, SaveVersion 6→7, Replay Version 6→7; the setup header carries each
player's faction. Older saves and replays are rejected with the existing clear message.
**Why:** M3-01 adds factions and three state parts (tunnels, ground, effects); old files can't describe them.
**Status:** confirmed (lead; required by HANDOFF rule that format bumps get an entry)

### D-083 · 2026-11-04 · Directorate art style and sprite-layer conventions
**Decision:** Directorate art uses palette `PAL_DIR` (tools/art/common.py, 24 colours incl. an umber ramp; no green,
so radiation glows read brass). Owner-approved references: dir_conscript and dir_smelter_vent (round 3). New
sprite conventions: (1) turret layer `<id>_turret.png` + `<id>_turret.sprite.toml` (`layer = "turret"`, pivot in
cells), drawn at hull origin + pivot rotated by the hull facing; the hull bakes a turret collar and contact shadow,
the turret has a soft rim instead of a hard outline over the hull (owner: turrets looked "superimposed"; fixed);
(2) air shadow layer `<id>_shadow.png` drawn at the ground position under the body (manifest `altitude`);
(3) burrowers have a `dive` state (16 facings × 4 frames, 3 ticks each), played in reverse when surfacing.
**Why:** owner reviews of the pilots and batches in session 4.
**Status:** confirmed (owner approved vent, infantry, both vehicle batches; turret rework awaits a morning look)

### D-084 · 2026-11-04 · Art reaches GitHub through the owner's PC
**Decision:** The cloud workspace cannot upload Git LFS objects (proxy blocks the storage host). Art is committed on
`wp/art-*` branches in the workspace; at wrap-up the lead copies the LFS objects into the owner's local
`.git/lfs/objects` and a git bundle plus `push_art.ps1` into `_art_push/`; the owner runs the script.
**Status:** confirmed (owner, session 4)

### D-085 · 2026-11-04 · Main menu lobby (M3-16)
**Decision:** On launch the game opens a lobby over illustrated, data-driven scenes (game sprites, parallax, ash and
ember particles, slow cross-fades; not a live battle). Menu: Continue, Campaign (New/Load), Skirmish (New/Load),
LAN Game and Map Editor (shown greyed: "Coming in a later update"), Replays, Archive, How to Play, Settings, Quit.
Licence and credits live under Settings → About (assumed — confirm; owner did not pick a Credits button).
**Status:** confirmed (owner, session 4) except the About placement

### D-086 · 2026-11-04 · The Archive (M3-17) and the approved lore
**Decision:** In-game Archive with hybrid unlocks: faction history and the basic roster open; leaders' backstories,
secret tech and late units unlock by campaign progress or first sighting in skirmish (fog-honest). Unit, structure
and upgrade entries show live stats from the rules. Lore is drafted by the lead and approved by the owner; the first
draft (docs/lore/) was approved in full. Leader secrets are separate "Classified" entries unlocked by campaign
missions (ids set in M4). Faction names "Authority" and "Directorate" are used as-is.
**Canon introduced:** Director Harrowgate died in 1974 and the Voice (Odette Larkin) reads committee scripts; the
Commissar (Thrall) found "Order Seven" (1963) and drives the Sunrise Device; the Authority knew of the Annex from
1974 ("the hum") and chose not to dig; Brigadier Wrenfield's brother was left behind on the Glass Coast in 1990;
the Annex is failing after the 1994 Shaft Seven reactor loss.
**Status:** confirmed (owner, session 4)

### D-087 · 2026-11-04 · Expansion plan and new milestone order
**Decision:** After M3: skirmish depth and the new factions come BEFORE the campaign, so the campaign is written once
with all four factions. New factions: **The Left Behind** (descendants of the Glass Coast evacuees abandoned in
1990; nomadic ash-scavengers, mobile crawler-rig bases, salvaged tech, raids) and **the Overseas Expedition** (an
old-world remnant — survivors of a real continent's governments, lightly fictionalised: invented names for all
leaders and institutions, no real people or politics; naval and air, beachheads, most advanced, far from home).
Rosters: be creative and build what fits each faction and the game; the "100+ units / 70+ buildings in total" is a
loose target, not a quota. Skirmish depth (equal priority): procedural maps (Rust Belt ruins, Glass Coast dunes,
Mountain passes, Ash plains/rivers; small/medium/large), AI-vs-AI spectator mode, garrisoning, three-tier tech
trees and upgrades, the map editor. LLM opponent: the built-in AI does moment-to-moment control; the LLM sets strategy
every 10–30 s from a battlefield summary and talks in character as a faction leader; API key in Settings for
Anthropic (Claude) or any OpenAI-compatible endpoint, stored only in local settings, never logged or committed; its
orders go through the normal command log so replays and lockstep stay deterministic (D-051). LLM-vs-LLM spectating.
New-faction lore follows D-086 (lead drafts, owner approves).
**Supersedes:** the milestone order in ROADMAP (campaign was M4, LAN M5).
**Status:** confirmed (owner, session 4)

### D-088 · 2026-11-04 · Overnight art approval for new factions
**Decision:** For the new factions the lead renders one pilot unit and building per faction, reviews them itself, then
fans out the roster without waiting; the owner reviews everything in the morning.
**Status:** confirmed (owner, session 4)

### D-089 · 2026-11-04 · Session 4 assumed values (review list)
**Decision:** Every value or behaviour a package had to guess is marked `ASSUMED (M3-xx)` in code or `# ASSUMED` in
data (about 300 `# ASSUMED` tags across data/rules). Notable ones: radiation 10 HP/s per full cell, resist-scaled, no damage
to buildings or underground units; Directorate start kit = dir_shaft + 4 conscripts; engineers capture below 50 % HP;
aura stacking = best single source; one team member's grouting covers the team; land-train deploy is one-way;
Sunrise blast radius 5 cells, 700 damage, enemies are not told a superweapon is charging; keys T (Upgrades) and D
(Deploy); music folder data/music; sfxr output licensed CC0.
**Status:** assumed — confirm (balance pass in M3-14; owner review; Q-34)

### D-090 · 2026-11-04 · Shared .NET build lock for helper waves
**Decision:** Helpers wrap every `dotnet build/test` in `flock /home/claude/wt/dotnet.lock`, like Blender renders.
**Why:** 8 parallel builds on 2 CPUs / 7 GB would run out of memory. Cost: long queues (full suite ≈ 8 min each).
**Status:** confirmed (lead)

### D-091 · 2026-11-04 · Session 5 art fan-out reviewed by the lead
**Decision:** M3-ART follows the D-088 flow: after the owner approved the turret rework and Command Shaft rev 2, the lead fanned out the 14 Directorate structures, 4 Authority structures, the agitprop turret and the Sunrise Device, reviewed each contact sheet itself and logged notes for the owner. The rad mortar's radiation green is lost because PAL_DIR has no green (Q-35).
**Status:** assumed — confirm (owner morning review)

### D-092 · 2026-11-04 · Mission replays
**Decision:** Replay format 8 adds the mission id and the script hash; playback re-runs the script. Tools depends on Scripting. **Supersedes** D-071's "mission games record no replay yet".
**Status:** confirmed (lead)

### D-093 · 2026-11-04 · In-game save/load
**Decision:** Saves use the RTSG envelope v1 around `Simulation.Save`. AI players restart fresh after a load (no AI save API yet → M3-21). Loaded games are not recorded as replays.
**Status:** confirmed (lead)

### D-094 · 2026-11-04 · M4 format bumps and rules schema v4
**Decision:** RulesHash 6→7, SaveVersion 7→8, Replay 8→9 (reads 7..9), MapFile 1→2. Enums: CommandKind.Unload=22, ActorLayer.Garrisoned=7, MissionKind.Garrisoned=15, LandType.Shallow=7 (Coast=8 reserved), FxKind.GarrisonExit=5, DebugOp.GrantUpgrade=6. State parts end "… effects, garrison, stats"; ground part v2 adds Foam. Session-4 saves and replays no longer verify (as D-082).
**Status:** confirmed (lead)

### D-095 · 2026-11-04 · M4 plan questions Q-M4-a..j
**Decision:** All lead recommendations in docs/design/08_m4_plan.md §9 were taken, because the owner said "decide, mark assumed — confirm".
**Status:** assumed — confirm (Q-36)

### D-096 · 2026-11-04 · Build-lock fix
**Decision:** Use `flock -o` (the lock fd is not inherited) and the /usr/local/bin/dotnet wrapper that disables MSBuild node reuse and the shared compiler. **Why:** VBCSCompiler inherited the D-090 lock fd and starved the queue. Amends D-090.
**Status:** confirmed (lead)

### D-097 · 2026-11-04 · M3 landed
**Decision:** M3 landed on main (babbf6b) after Windows CI on PR #7; restore branch `restore/pre-m3-land`. M3-14 balance followed in PR #8 (1a468e9).
**Status:** confirmed (owner said land when CI is green)

### D-098 · 2026-11-04 · M4 landed
**Decision:** M4-01..M4-15 except M4-12 landed on main (7696ca2, PR #9; restore `restore/pre-m4-land`). M4-12 AI follows in PR #10.
**Status:** confirmed (owner said land when CI is green)

### D-099 · 2026-11-04 · Session 6 owner confirmations
**Decision:** D-089 (Q-34) and D-095 (Q-36, M4 plan answers and theater colours) are confirmed. D-091 art is accepted, with the
lead's reworks to do (Seismograph drum, Searchlight lens, Assembly Bore roof, Hatch cage, auth_gate open state). Q-35: (a) one
radiation-green ramp in PAL_DIR, used only for radiation.
**Status:** confirmed (owner, session 6)

### D-100 · 2026-11-04 · Neutral faction (Q-37)
**Decision:** Option A: `Faction.Neutral = 2` (append-only) is the passive owner of ruins and map features. It is not playable,
neutral types cannot be built or produced, and nothing auto-targets neutral-owned buildings; the first player to garrison one
takes it. Manual Attack orders on empty ruins stay allowed (D-054).
**Status:** confirmed (owner, session 6)

### D-101 · 2026-11-04 · Lore approvals
**Decision:** Approved: the M4 Archive entries (data/archive), docs/lore/left_behind.md, docs/lore/expedition.md (Q-38) and
docs/lore/leaders.md (Q-M5-c). world_history, authority, structures, units, directorate and upgrades .md still carry a DRAFT
banner (Q-41).
**Status:** confirmed (owner, session 6)

### D-102 · 2026-11-04 · M6 Left Behind design answers (Q-39)
**Decision:** All recommendations in docs/design/06_left_behind.md §10: a packed rig cannot produce; wrecks last 60 s and are
visible to all; air roster Rotor + Kite; superweapon The Second Drift; no tunnel interaction; campaign role decided later.
**Status:** confirmed (owner, session 6)

### D-103 · 2026-11-04 · M5 LLM commander plan
**Decision:** docs/design/09_m5_plan.md is the plan. Providers: Anthropic and OpenAI-compatible. One strategy decision every
30 s of game time, delivered as a `Directive` command so replays and lockstep stay deterministic. In-character chat on by default.
No cheating: the summary uses only PlayerView. Default model cheap/fast, a stronger one selectable. API key in a DPAPI-encrypted
file `%APPDATA%/RTS_2027/llm.key`, never in settings, logs, saves or replays. Call cap 120 per game. The player may chat to enemy
commanders; flavour only. Contracts: src/Llm/Contracts.cs.
**Status:** confirmed (owner, session 6)

### D-104 · 2026-11-04 · Start bias (M4-16)
**Decision:** The sim's start-0 advantages are fixed: regrowth order, harvest and exit tie-breaks, oriented exits and docks,
centre-most spawn sub-slots (docs/balance/m4_16_start_bias.md). `ai match` gives each game of a pair its own game seed (the two
games used to be one mirrored game counted twice). Mirror-symmetric AI building placement was tried and reverted: identical AIs on
mirror bases stalemate (ADirectorateMirrorAtVeteranDoesNotFreeze); revisit with seeded placement variation.
**Status:** confirmed (lead)

### D-105 · 2026-11-04 · M4-16 round 1 balance
**Decision:** AI (tier 2+): finish won games (ahead = ≥ 8 units and ≥ 150 % of the enemy army seen in 1 200 ticks; stage 6 cells,
timeout 600 ticks, hit production first); 4-player target review 300 ticks, weakest-first weight 3, regroup at 60 % on maps ≥ 80
cells. Tech hold coded but off (`TierPlanner.TechHoldEnabled`). Rules: garrison_damage_percent 25 → 50 for dir_ash,
dir_demolition, auth_seismic; Seismic Mortar damage 20 → 28. The tier ladder target is not met yet (docs/balance/m4_16_ai.md).
**Status:** assumed — confirm (owner play-test; re-measure next session)

### D-106 · 2026-11-04 · M3-21 choices
**Decision:** A gate counts as open while a unit of its owner's team stands in its footprint (no new sim state, D-076). Saves
are header v3 with each AI's state; v1/v2 saves still load with a fresh AI. The Sunrise ready message uses the placeholder text
key `hud.superweapon.ready` until there is a string table.
**Status:** confirmed (lead)

### D-107 · 2026-11-04 · Session 6 landed
**Decision:** Session 6 work landed on main (55a3a27) after Windows CI on PR #11; restore branch `restore/pre-session6`.
**Status:** confirmed (owner said land when CI is green)

### D-108 · 2026-11-04 · Q-40 commander models
**Decision:** Anthropic defaults are `claude-haiku-5-5` (default) and `claude-sonnet-5-5` (stronger), the current models on
platform.claude.com/docs/en/models/overview on 2026-11-04. Requests send `output_config.effort = "low"` (new `effort` key in
data/llm/providers.toml; "" sends nothing, for older models that reject it), and the provider reads the first `text` block because
current models put thinking blocks first. Commander requests use max_tokens 1200 so thinking cannot crowd out the JSON reply.
**Why:** owner asked the lead to look up the models (session 7). **Status:** confirmed (owner delegated)

### D-109 · 2026-11-04 · M5-01 directive contract
**Decision:** `CommandKind.Directive = 23`; Arg packs posture/focus/mix/expand in 4-bit fields and the target in bits 16..23
(`DirectiveCodec`); Cell = focus cell or (-1,-1). The sim clamps on apply (same defaults as the reply parser: balanced, army, auto,
any target), drops a focus cell that is off the map or unexplored for the player, ignores directives from neutral slots, and stores the
latest directive per player in a new "directive" state part after "stats". Save format 9 (format 8 saves are rejected, as at M4-01);
golden and pinned hashes re-pinned for the new part only. The commander reaches the sim through `IDirectiveSource`, polled by
`AiDriver` every tick, so a directive gets the slot's command delay and sequence and is recorded like any AI command.
**Status:** confirmed (lead; implements the approved plan 09_m5_plan.md §2–§3)

### D-110 · 2026-11-04 · Commander chat stays out of the sim
**Decision:** The plan said commander chat goes through `CommandKind.Chat` with an existing text table, but no chat text table exists.
For M5, commander and player chat lines live in the game layer (`CommanderChatLog`) and are not in replays; replays still repeat every
directive exactly. A replay chat block can be added later (M5-10 or M9 LAN chat).
**Why:** keeps text out of the deterministic sim; reversible. **Status:** assumed — confirm
