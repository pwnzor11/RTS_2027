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

### D-111 · 2026-11-04 · M5 setup and chat choices
**Decision:** Setup screen: each AI slot has a "Cmdr" toggle; a commander slot reads "AI LLM - <tier> ..." and, with provider
"none" or no stored key, "(no key - plays as standard AI)". Save header version 4 stores the flag per slot (v1–v3 read as off);
the setup screen keeps no last-used setup, so nothing else persists it. In game, Enter opens the commander chat (Esc cancels, typing
blocks hotkeys); `/to N text` talks to the commander of player N (1-based as shown), plain text to the first commander. Player
lines go into the next 30 s decision only (no extra call); they never change the directive (Q-M5-e). Loaded saves rebuild their
commanders from the app settings (history and call count start fresh). The spectator table tags commander slots "LLM".
**Status:** assumed — confirm (visible text and keys; owner play-test)

### D-112 · 2026-11-04 · Commander personas
**Decision:** data/llm/personas.toml holds one voice per leader in docs/lore/leaders.md (4 Authority, 4 Directorate), written from
each bio and motivation only; nothing from the Secret paragraphs (a test checks). A commander gets its faction's leaders in file
order, rotated by player slot.
**Status:** assumed — confirm (owner reads the voices)

### D-113 · 2026-11-04 · M4-16 tuning rule
**Decision:** AI behaviour may change freely while tuning the tier ladder. Rules numbers players see (Q-43 tier costs, unit costs,
the D-105 values) are measured and brought to the owner as a table before they land.
**Status:** confirmed (owner, session 8)

### D-114 · 2026-11-04 · Companion turret and shadow sheets
**Decision:** Besides the M3-18 in-sheet layers, a unit may have companion sprite sheets `<id>_turret` and `<id>_shadow` with
`layer`, `pivot_x/y/z` and `altitude`. The renderer offsets the turret by the pivot rotated with the hull facing, with no height
term (pivot_z is informational; the turret keeps its height inside its frame), matching tools/art/build_vehicle.py.
**Status:** confirmed (owner chose option 1, session 8)

### D-115 · 2026-11-04 · Rewritten art draft commits
**Decision:** Six first-draft sprite images (Rad Mortar and Mooring contact sheets) were lost before upload, and GitHub rejects
pushes that reference unknown LFS objects anywhere in history. The draft commits on the session-5 art branches were rewritten to
point at the current images; the tip trees are identical. Art still reaches GitHub only through the owner's PC (D-084).
**Status:** confirmed (owner ran the push, session 8)

### D-116 · 2026-11-04 · Radiation green (D-099 implemented)
**Decision:** RAD_GREEN (#2F4A1C #5E8F2A #9BE04A #D8FF8A) is in PAL_DIR (28 colours + 8 house shades). It is reachable only
through a radiation mask from a model's RAD_MATERIALS, so no other surface can quantise to it. First user: the Rad Mortar.
**Status:** confirmed (owner approved the reworks, session 8)

### D-117 · 2026-11-04 · ART-S6 rework choices
**Decision:** Hatch shows a bar cage and no lift car; auth_gate gains an `open` state (4 frames, first frame 16); Seismograph,
Searchlight and Assembly Bore reworked as in PR #14.
**Status:** confirmed (owner approved the pilot and the reworks, session 8)

### D-118 · 2026-11-04 · M6 plan and contracts
**Decision:** docs/design/10_m6_plan.md is the M6 plan: Faction.LeftBehind = 3, CommandKind.Pack = 24, MissionKind Salvage 16 /
Pack 17 / Withdraw 18, new state parts "rigs" and "salvage" after "directive", effects layout v2, save/replay format 10.
M6-02 as built: rules-hash format 8 (schema v5 block for the new keys); wreck table capacity is a constant 256 (not a SimLimits
field, so the header layout is unchanged); extra keys `wreck_min_value` 25 / `wreck_max_value` 1000; `wreck_ticks` defaults to 0
(wrecks off until data sets 1200); storm fields live on the superweapon's `_blast` warhead; Left Behind is not playable until
M6-08 adds its start kit and M6-09 raises PlayableFactions.
**Status:** assumed — confirm (owner reads the plan)

### D-119 · 2026-11-05 · Older lore files approved (Q-41)
**Decision:** The six older lore files (docs/lore/world_history, authority, structures, units, directorate, upgrades) are approved;
their DRAFT banners are removed.
**Status:** confirmed (owner, pre-session 9)

### D-120 · 2026-11-05 · Quick-Set Foam stays 3×3 (Q-42)
**Decision:** Option (a): foam keeps its 3×3 footprint; it slows a tunnel push but cannot block one. Revisit after a play-test.
**Status:** confirmed (owner, pre-session 9)

### D-121 · 2026-11-05 · Tier-3 cost cut is measured, not landed (Q-43)
**Decision:** Measure the tier-3 gate and tier-3 upgrade cut 2 500 → 2 000 with `ai match` and bring the owner a table. The data
files keep 2 500 until the owner chooses (D-113).
**Status:** confirmed (owner, pre-session 9)

### D-122 · 2026-11-05 · Earlier assumed decisions confirmed
**Decision:** D-105 (M4-16 round 1 balance), D-110 (commander chat outside the sim), D-111 (M5 setup and chat choices), D-112
(commander personas) and D-118 (M6 plan and contracts, save/replay format 10) are confirmed.
**Status:** confirmed (owner, pre-session 9)

### D-123 · 2026-11-05 · Session 9 working rules
**Decision:** (1) New M6/M7 numbers marked `(tune)` land, logged "assumed — confirm" with their balance tables; rules numbers that
change existing play (Q-43, D-105 values) still go to the owner first (D-113). (2) Art: the lead builds and reworks art freely and
reviews it; a lead-reviewed pilot still comes before each fan-out, but no owner approval is needed. A dedicated art pass follows once
the game is built. (3) Helpers start on Sonnet; Opus helpers sparingly; Haiku for docs and bulk edits.
**Status:** confirmed (owner, pre-session 9)

### D-124 · 2026-11-05 · Expedition answers (07_expedition.md §10 items 1–3, 6, 8–10)
**Decision:**
- Landlocked maps: the generator guarantees a coast for an Expedition player; on a hand-made map without a landing coast the
  Expedition cannot be picked (no air-supply fallback).
- Shallows: land vehicles cannot cross; infantry move at 40 %; hover moves freely.
- Ship size: one-cell occupancy; larger ships are drawn bigger than their cell. The approach is studied in the GPL reference
  source and written from scratch (never copied, D-050).
- Superweapon: Salvo Array now; The Long Tide postponed (needs terrain mutation).
- Old maps: `water` is an alias for deep water; no migration.
- Weapons vs ships: default target mask `Ground|Naval`, so existing weapons hit boats.
- Submarines: keep both the missile sub and the minelayer.
**Status:** confirmed (owner, pre-session 9)

### D-125 · 2026-11-05 · Supply cap replaces the flat unit cap (all four factions)
**Decision:** Every unit costs supply; each faction's supply buildings (and its main base building) raise the cap up to a hard
ceiling. The sidebar shows used/cap and production stops at the cap with a clear message. Each faction's supply building comes
from its lore (prefer an existing building): Authority housing/ration blocks, Directorate air scrubbers or bunk galleries, Left
Behind water stills or cook-fires that pack with the rigs, Expedition a small cheap land building placeable anywhere in the
beachhead zone (not quays), stocked from the shipments. It replaces the Expedition's flat 40-unit soft cap. The AI builds supply
before it reaches the cap at every tier. Design in docs/design; numbers `(tune)`.
**Status:** mechanic confirmed (owner, pre-session 9); buildings and numbers assumed — confirm

### D-126 · 2026-11-05 · Home shipments and the early-game income target
**Decision:** Expedition home shipments shrink over time so the faction must build a local economy, and they can be raided at
sea. Shipments and local ash are tuned so that the Expedition's income per minute over the first 3, 5 and 10 minutes (AI vs AI,
every map size) is neither starved nor ahead of the other three factions; the curves go in the balance table.
**Status:** assumed — confirm (owner likes the mechanic; numbers to be measured)

### D-127 · 2026-11-05 · Generic veterancy
**Decision:** All factions get the same 3-rank veterancy (save/replay-safe). Rank 3 must make a real difference, as in Red Alert.
Thresholds and bonuses `(tune)`.
**Status:** mechanic confirmed (owner, pre-session 9); numbers assumed — confirm

### D-128 · 2026-11-05 · Session 9 extra scope
**Decision:** Besides M6, session 9 starts M7, builds generic veterancy and writes the M8 campaign plan (docs/design/12_m8_plan.md,
all four factions, plan only). It also writes Expedition Archive entries and 2–4 Expedition leader personas.
**Status:** scope confirmed (owner, pre-session 9); lore and personas assumed — confirm

### D-129 · 2026-11-05 · Session 9 kickoff answers
**Decision:** (1) The M4-16 wave strength gate + escalation is made real code in session 9 and lands if the ladder improves (AI
behaviour only, D-113); this unblocks M6-10. (2) The supply cap (D-125) is on by default in skirmish once it lands. (3) Art is staged
into the owner's `_art_push` folder overnight and pushed by the owner with push_art.ps1 (the PC shell has no GitHub sign-in; D-084).
**Status:** confirmed (owner, session 9 kickoff)

### D-130 · 2026-11-05 · Supply cap, veterancy and M7 plan designs
**Decision:** docs/design/13_supply_cap.md, 14_veterancy.md and 11_m7_plan.md are the designs for D-125, D-127 and M7. Lead picks
where the docs left options: supply buildings are new `auth_ration_hall`, `dir_bunk_gallery`, `lb_still_rig` (a rig) and
`exp_provision_store`; harvesters cost 1 supply, the packed Mother Rig 0; supply is derived each tick (no state part) behind
`[supply] enabled`; Expedition stores give full supply only while shipments keep arriving (option A). Veterancy ranks Veteran /
Elite / Hero, kill value stored in a new "veterancy" part (format 11), rank 3 ≈ 2× combat value plus self-heal; the Expedition
gets no faster gain (only its `exp_veteran_drill` upgrade). M7 contracts follow at format 12.
**Status:** assumed — confirm (owner reads 13 §2.3 and 14 §2.3 first)

### D-131 · 2026-11-05 · AI wave strength gate lands; Q-43 measured
**Decision:** The M4-16 wave strength gate (100 % of the biggest enemy army seen in the last 3 600 ticks; lifts at 24 units or 6 000
ticks) and escalation after a failed wave (200 %, cap 12 000) land as AI behaviour for tier 2+ (src/AI/Recruit/WaveGate.cs, `(tune)`).
Measured in docs/balance/m4_16_round4.md: the Authority ladder improves at 2v1/3v2/4v3; the Directorate 4v3 improves, its 5v4 decisive
share drops (54 % -> 41 %). The >= 60 % target is still unmet above 2v1; the top rungs need another round (draws are 40-60 % of
Directorate games). "Scouting + harass off for elite" was tried and rejected (it breaks Directorate elite). Q-43: the 2 500 -> 2 000
tier-3 cut moves every cell only within noise; the lead recommends keeping 2 500 (owner decides, D-121).
**Status:** gate confirmed (owner, D-129: lands if the ladder improves); Q-43 recommendation waiting on the owner

### D-132 · 2026-11-05 · Late session-9 calls: supply AI, Left Behind AI, Second Drift, art scale
**Decision:** (1) SUP-5: the AI starts the cheapest buildable supply structure (not the yard) when free supply drops below 6 and the
cap is below the ceiling; a power plant first if that structure would brown out. Data-driven, so no per-faction id. (Supply itself is held
off for now, D-133.) (2) SUP-1 rules as built: a unit's supply uses its base cost (not upgrade-discounted); supply-0 units
are never blocked; harvesters (auth_scraper, dir_scraper, lb_gleaner, lb_hauler) cost 1; Ration Hall 200 / power -10 / 300 HP, Bunk
Gallery 150 / -10 / 250 HP, Still Rig 200 / 0 / 300 HP, each +10 supply. (3) M6-10: the Left Behind AI prefers the Rotor over Kites
(Kites cost tier 4 about 20 points of win rate); fixed structures are sold and rebuilt on relocation. (4) `lb_second_drift`
`superweapon_entrance_range` 8 → 0: Left Behind has no entrance building, so the storm could never fire. (5) Left Behind art: the
vehicles are drawn about 1.5× the Directorate's size to match the rig; kept for now, owner to judge in game.
**Why:** each was the recommended option for an unattended night; all are reversible data or AI behaviour.
**Status:** assumed — confirm (tables in docs/balance/s9_tables.md and docs/balance/m6_10_ai.md)

### D-133 · 2026-11-05 · Supply cap held off by default for now (supersedes the default in D-129)
**Decision:** `[supply] enabled = false` in data/rules/game.toml for now. The sim, data, supply buildings, block text and the AI's
supply rule (SUP-5) are all in; turning supply on is one line. With it on, three AI tests fail even with SUP-5: the Authority
Regular garrison fills 2 of 3 in time, the Authority Veteran misses tier 3 within 15 minutes, and a tier-5 Left Behind packed rig
stands idle for 30 s (seed 4). All pass with supply off. Per the session's guard rail (one honest try, then park), supply waits for
the next session's AI tuning.
**Alternatives:** land it on and weaken the three tests (rejected: it would hide a real AI slowdown).
**Status:** assumed — confirm (owner can play-test supply now by setting `enabled = true`)

### D-134 · 2026-11-05 · Owner confirms the session-9 assumptions and play-test (session 10 kickoff)
**Decision:** the owner wrote "all assumptions and play test from last time look great". Confirmed: D-123..D-133, the session-9
numbers in docs/balance/s9_tables.md (Q-44) and the session-9 lore, Archive entries and personas (Q-50). The lead took the
recommended option, reported to the owner at kickoff, for: Q-43 keep the tier-3 cost at 2 500; Q-45 keep the Left Behind vehicle
scale; Q-46 add the own-unit info panel with VET-6; Q-47 keep the quieter unit_ready cue for promotion; Q-48 the Left Behind AI keeps
Rotors; Q-51 turn supply on once the AI fix passes (D-133 lifted then).
**Why:** owner's message at the start of session 10; the recommendations were listed back to him in the session briefing.
**Status:** confirmed (owner, session 10) for D-123..D-133, Q-44, Q-50; the recommended options for Q-43, Q-45..Q-48, Q-51 are
confirmed by the owner's "all assumptions look great", stated back to him at kickoff.

### D-135 · 2026-11-05 · Supply cap on by default (lifts D-133; SUP-5 follow-up)
**Decision:** `[supply] enabled = true` in data/rules/game.toml. The AI now measures its margin from used + queued unit supply, by
tier 1..5 = 4, 5, 8, 10, 12 `(tune)` (design 13 §6 said 6 at tier 2; 6 failed the Authority/Directorate tests). A supply building
that cannot start yet no longer reserves its cost; when none is buildable the AI builds the first missing prerequisite (the
Ration Hall needs auth_power). Left Behind: no supply structure is queued while any rig is packed; a relocation never packs the
still while near the cap; an unreachable unpack site is banned for 1 200 ticks and re-picked. No supply numbers or costs changed.
Measured (SupplyAiTests, first 15 minutes): worst capped time 29 s (Directorate tier 1), limit 60 s.
**Why:** D-129 wants supply on; the three AI tests that held it off (D-133) now pass with it on.
**Status:** assumed — confirm (the tier-2 margin of 5 and the Left Behind rules); supply on itself is the owner's (D-129, Q-51)

### D-136 · 2026-11-05 · VET-6 details the design left open
**Decision:** (1) No unit-repair building exists, so a retreated rank-3 unit goes to the nearest own repair-aura vehicle, else the
rally cell, and rejoins at 90 % health or after 3 600 ticks. (2) Rank-aware army value is used by the wave gate and the "ahead"
test; the tier planner's build/tech gates still use plain cost (to be unified later). (3) Focus fire: wave members attack the
nearest visible rank-3 ground enemy within 10 cells `(tune)` when no defence response is active; the defence spot prefers a rank-3
attacker. (4) Rank ≥ 2 units stay in the main prong and are never picked as raiders.
**Why:** design 14 §6 assumed a unit repair path and a target priority list that the code does not have.
**Status:** assumed — confirm

### D-137 · 2026-11-05 · M7-02 contract choices
**Decision:** footprint `water` mask is a row-major 0/1 array (1 = water); `beachhead_source` and `beachhead_radius` come together;
coast speeds equal clear for foot/wheeled/tracked, hover 80, naval 0; `draw_scale_permille` 100..10 000; a carrier needs
`sortie_ticks > 0`; the "naval" state part is versioned (version byte 1). Saves older than format 12 are refused (as at format 11);
replays 7-11 still play. Expedition is a Faction value but not playable (PlayableFactions stays 3) until M7-08/M7-14.
**Why:** 11_m7_plan §2 left these to the implementer; each is the plainest reading and reversible before M7 data lands.
**Status:** assumed — confirm (only coast speeds and the save cut-off are visible to the owner)

### D-138 · 2026-11-05 · M7-03 naval movement choices
**Decision:** `deep_only` ships use a private movement class (NavalDeep) that closes Shallow; it is not a Locomotion value and not in
the rules hash. A ship's Move is dropped when the goal is not sea or lies in another sea; a ground unit's Move is dropped only when
the goal is water it cannot enter (other unreachable goals keep the old partial approach, so goldens do not move). Sea labels are
terrain-only, 4-connected; the saved mirror is written only when the rules contain a naval type. Draw scale applies to body, shadow,
turret and selection bracket, not health bars. Follow-up: exit-cell search at a shipyard must use the move class (M7-04/M7-09).
**Why:** 11_m7_plan §3 left these open; the narrow refusal keeps current play identical.
**Status:** assumed — confirm

### D-139 · 2026-11-05 · M7-06 home shipments choices
**Decision:** new building flag `shipment_dock` (requires `water_building`); `[shipments] tender_type` names an unarmed naval vehicle,
empty = shipments off. A field is "held" while an own refinery has an own harvester harvesting or returning within 16 cells (checked
every 10 ticks; leaving resets charter progress). The decay step stops where the payload reaches the floor. No dock = no timer; a
shipment that cannot launch (tender limit or no free edge water) is skipped and does not advance decay. Shipment income is a stats
counter (stats part version 2 only once it is non-zero, so old games hash the same).
**Why:** the sim has no field ownership; this is the cheapest honest stand-in. All numbers `(tune)`.
**Status:** assumed — confirm

### D-140 · 2026-11-05 · M7-07 beachhead choices
**Decision:** the zone is every cell within the source's radius by the D-062 build-radius metric (Chebyshev from the nearest
footprint cell). Only Expedition players are gated; outside the zone placement returns the new `OutsideBeachhead`. An Expedition
player that owns no building may place anywhere (so the first landing depot can go down). Non-water structures refuse Water and
Shallow; water buildings follow their mask. The zone is rebuilt at once when a source is placed, lost or captured.
**Why:** 07_expedition §2.1 did not say how the first depot is placed or which distance is used.
**Status:** assumed — confirm (the bootstrap rule is the visible one)

### D-141 · 2026-11-05 · Left Behind AI salvage fix and session-10 acceptance
**Decision:** AI only, no rules numbers: a Hauler that is auto-harvesting ash counts as free for a wreck; the first Hauler is bought
at 5 army units with a Gleaner working; wreck gate 60 (was 150); a lost Hauler is not rebought for 3 600 ticks or below 900 funds;
no Return orders to a harvesting Hauler. M6-12 accepted on stability (216 games, 0 desyncs, 0 exceptions, LB golden replay).
Balance gaps go to the owner as Q-52..Q-54; no number changes landed.
**Why:** salvage almost never ran (the Hauler never looked idle); the balance numbers need the owner.
**Status:** assumed — confirm (AI thresholds are `(tune)`)

### D-142 · 2026-11-05 · M7-12 Salvo Array choices
**Decision:** the "<superweapon>_blast" warhead gains salvo fields (missiles, ticks, radius, damage, warn ticks, splash; all off by
default). Missiles scatter uniformly over the square radius (seeded RNG), deal full damage in the impact cell and half within the
splash, may target any cell including sea, keep flying if the Array dies, and spare dived submarines. Four salvos can fly at once
(a fifth replaces the one furthest along). Data `(tune)`: charge 7 200 ticks, 12 missiles over 120 ticks after an 80-tick warning,
radius 4, 150 damage. The effects part writes its salvo table (version 3) only while a salvo flies, so old games hash the same.
**Why:** design 07 gives the numbers but not the scatter, sea or overlap rules.
**Status:** assumed — confirm

### D-143 · 2026-11-05 · M7-05 submerge choices
**Decision:** a submersible ship dives or surfaces at once on Deploy (no auto-dive). A dived ship is hidden from and untargetable by
enemies unless one of their sonar vehicles covers its cell; it can still fire. Splash spares a dived ship unless it is the shot's
own target. Weapon target classes: Ground, Air, Both (never dived ships), Naval (surface ships), Submerged, NavalAndSubmerged.
Only vehicles carry sonar for now (buildings have no sonar field).
**Why:** 07 §6.4 does not say whether a dived sub may fire or dive automatically.
**Status:** assumed — confirm

### D-144 · 2026-11-05 · M7-04 landing choices
**Decision:** `Unload` on a craft with `unload_ticks > 0` (or an aircraft) sails or flies to the shore and puts one passenger ashore
every `unload_ticks`, starting on the ordered cell. A shore cell is any cell infantry can stand on. With no reachable sea cell beside
the shore the order is ignored. Exit cells at factories, tunnels and rig packing use the unit's move class, so ships leave onto
water and deep ships never onto shallows. Not built: vehicles as passengers (the landing craft's two vehicle slots), beached craft.
**Why:** 07 §6.3; the transport code carries infantry only.
**Status:** assumed — confirm

### D-145 · 2026-11-05 · M7-13 map choices and M7-08 Expedition data
**Decision:** (1) Ocean edges are opt-in (`map gen --ocean nesw`): 2 shallow and 2 coast rings, a convoy cell every 8th border water
cell, one landing marker per start; new validator errors `convoy_lake`, `convoy_no_coast`, `landing_unreachable`. Maps without the
option are byte-identical. Not in skirmish setup yet (saves would not carry it). (2) The Expedition roster, 20 structures and 8
upgrades are in data/rules, every number `(tune)`, balance sheet docs/balance/m7_08_expedition_data.md; Skyfreighter and Airhead
dropped; Gyro Scout at tier 2 (aircraft come from the tier-2 Flight Deck). The faction stays unplayable. Adding naval types makes
every game write its sea labels, so SkirmishDefaultHashTests was re-pinned (only the "naval" sub-hash moved).
**Why:** data needed for M7-09/M7-10; the hash move was checked part by part.
**Status:** assumed — confirm (all numbers)

### D-146 · 2026-11-05 · Session 10 fixes
**Decision:** replay file names use a sanitised AI label; the tier planner uses rank-aware army value (finishes D-136 item 2);
the commander replay test waits on the request instead of a time budget; Left Behind relocation prefers unpack sites with no visible
enemy within 2 cells (mitigation for rigs idling under raids).
**Why:** bugs found by the M6-12 soak and two flaky runs.
**Status:** done

### D-147 · 2026-11-05 · Session 10 wave 4: carrier, vehicle passengers, M7 UI, follow-ups
**Decision:** (1) Carrier (M7-09): any armed aircraft without passengers takes one hangar place; it docks via Enter (also recalls a
flying one); Attack on a carrier launches every docked aircraft that can hit the target; a sortie ends after `sortie_ticks` or when
the attack ends, then the aircraft fly home, dock and repair fully over `sortie_ticks`. Aircraft built with the rally point within 2
cells of an own carrier with room go straight into its hangar. A dying carrier loses docked aircraft (counted lost); airborne ones
fly on. No ammo system. (2) Landing craft carry `vehicle_slots` vehicles (data: 2); a ship's vehicles land only on `landing` (Coast)
cells, soldiers first; no nesting; a sunk craft loses its vehicles. (3) Game: save header v5 carries ocean edges for generated maps;
skirmish Ocean option (none, one edge, two opposite edges); beachhead tint while placing; shipment countdown; right-click unload;
D or a Dive button dives a submarine; dived ships drawn faded. (4) The Expedition kit may use a deployable vehicle as its base
builder; ships leave only from a factory that reaches water (else they wait); new effect attributes `shipment_interval` (per mille,
Fast Charging 850) and `harvest_capacity` (Deep Survey +2 on the Skimmer); buildings may carry `sonar_radius` (picket 9).
**Why:** design 07 §6 and 11_m7_plan §3; each choice is the plainest reading and is behind data that no current faction uses.
Supersedes the "not built: vehicles as passengers" part of D-144 and the "not in skirmish setup yet" part of D-145.
**Status:** assumed — confirm

### D-148 · 2026-11-05 · Session 11 kickoff: owner answers Q-52..Q-54 and confirms the session-10 assumptions
**Decision:** the owner took every recommendation from the session-11 briefing. Q-52 (c): leave Left Behind income for now,
re-measure after M7 and bring a yield table before any change. Q-53 (b): leave the veterancy thresholds; rank 3 stays rare outside
the Directorate. Q-54: re-measure supply on/off with 64 games per pair; no number change. Confirmed: D-135..D-145 and D-147
(incl. the D-140 first-building rule, the D-142 Salvo numbers, the D-145 Expedition numbers, no ammo system for sorties). Session 11
runs unattended except that the Expedition art pilot (Glide Tank, Landing Depot) waits for the owner's approval before any fan-out.
**Why:** owner's reply to the session-11 briefing ("take all your recommendations; render now for approval; unattended except for the art approval").
**Status:** confirmed (owner, session 11)

### D-149 · 2026-11-05 · Expedition art pilot: variant A for both pilots
**Decision:** the owner picked variant A for the Glide Tank (low, wide navy hover hull, twin canted fins, rear turbines, flat wedge
turret) and the Landing Depot (beached navy hull, white liner bridge, funnel, crane, ramp). A becomes the style target for the
M7-11 fan-out. Lead fixes on A before fan-out: rubber hover skirt instead of a tread-like base, visible lift and spray ring on water,
and a size close to the Authority tank (+15 % at most).
**Why:** owner's reply to the pilot contact sheets ("A for both options").
**Status:** confirmed (owner, session 11); the skirt/scale fixes are the lead's and are shown on the next sheet

### D-150 · 2026-11-05 · Shipments on; a ship that deploys places its building on the nearest buildable footprint
**Decision:** (1) `[shipments] enabled = true` in data/rules/game.toml (only games with an exp_anchorage dock are affected; no pinned
hash moved). (2) A naval deployer (the Expedition Command Ship, D-147 item 4) cannot place its building on its own water cell: Deploy
places it on the nearest buildable footprint that touches the ship's cell; with none, the order is ignored. Found by M7-10: the
Command Ship's Deploy did nothing on water. No existing game deploys a ship, so no pinned hash moved.
**Why:** the Expedition could not establish at all without (2); (1) was planned for M7-14 with the faction.
**Status:** assumed — confirm (the nearest-footprint rule is visible to a human Expedition player)

### D-151 · 2026-11-05 · M7-10 round 2: tender lane, landing markers in the view, south coast default
**Decision:** (1) A home-shipment tender launches only from a convoy edge cell whose sea region touches one of the player's docks (the
old first-edge-cell rule is the fallback); before this a tender could sail in an unconnected ocean and block the lane. No pinned hash
moved. (2) `SimSetup.LandingMarkers` (not hashed, not saved) and `PlayerView.LandingMarkers` / `CopyConvoyEdge` / `OverlayAt` give the AI
the map's landing markers (map data a human sees too); the skirmish host does not pass them yet, so the AI falls back to its own coast
scan there. (3) The Expedition AI refuses depot sites on ashfall (Deploy refuses overlay cells). (4) The shoreline buildings' water masks
(exp_anchorage, exp_quay, exp_shipyard, exp_drydock) put the sea below the land, so they fit only on south-facing coasts: the skirmish
setup now adds a South edge (not North) when an Expedition slot needs a coast, and `ai match` puts the Expedition on the south start.
The real fix is Q-55.
**Why:** M7-10 round 2 found the Expedition earning nothing on maps with two oceans or ash on the beach.
**Status:** assumed — confirm ((1)-(3) are fixes; (4) is a stop-gap until Q-55)

### D-152 · 2026-11-06 · Expedition early income tuned (11 §4) and naval strikes
**Decision:** Expedition-only numbers (D-113 untouched for the other three): `[shipments] interval_ticks` 1800 → 600, `value` 1500 → 2000,
`decay_permille` 40 → 60 (by minute 10 the decay step is 8-9, so a shipment pays about half its value; the 35 % floor comes at step 11). AI: Quay and Shipyard come before the second Pile House; strike groups
from one ship; a probe sail to the sea cell nearest the enemy start when no target is in range (terrain only); ReserveMin 4. Result
(docs/balance/m7_income.md, tier 2, 20 games per cell): medium −6/+6/+8 %, large −9/+11/−7 % (all six inside ±10 %); small +12/+16/+22 %
(outside, because Left Behind's low income pulls the comparison mean down — Q-52). 8-game tier-2 matches on medium: 5-3 vs Authority,
4-1-3 vs Directorate, 8-0 vs Left Behind (was 0/8, 0/8, 1/8).
**Why:** before the change the Expedition earned 32-56 % of the others in the first 10 minutes and lost almost every game.
**Status:** assumed — confirm (all numbers; the owner may prefer a gentler curve: decay 40 with value 2400 was the alternative)

### D-153 · 2026-11-06 · Session 12 kickoff: owner answers and confirmations
**Decision:** The owner answered the session-12 list:
(1) Q-55 → (a): the four shoreline buildings (exp_quay, exp_anchorage, exp_shipyard, exp_drydock) stand wholly on water and must touch the
shore; any coast works; the four sprites are redrawn as floating docks (pilot first). The South-coast stop-gap of D-151 (4) goes.
(2) D-150, D-151 (1)-(3) and D-152 confirmed; D-152 numbers kept.
(3) Small-map Expedition income: tune the Left Behind first (Q-52), then re-measure. Left Behind income tuning lands as "assumed — confirm"
with a before/after table (replaces D-148's "table first").
(4) Expedition AI per tier is a main package. M4-16 continues, informed by how the reference repos handle difficulty tiers.
(5) Aircraft ammo: 4-5 shots per sortie, then back to the carrier to rearm; balanced against other units' damage; the carrier stays strong.
(6) Submarines: draw the dived frames, and the faded look only for the owner and allies.
(7) Draws at the tick limit: a longer limit for measurement runs only (the normal game has no limit).
(8) Archive "PENDING owner text" entries: the lead drafts them from the lore, "assumed — confirm".
(9) Every decision still marked "assumed — confirm" up to D-152 is confirmed (this includes D-044..D-047, D-063, D-085, D-089, D-110..D-112).
(10) Session-10/11 remote branches deleted by the owner; the owner's play-tests of sessions 10-11 look good.
(11) Order: finish M7, then start M8.
(12) LLM commander test: the owner will run it with an OpenAI key on his PC; the key never enters chat or the repo. A headless
`ai match` commander option reads the key from an environment variable.
**Status:** confirmed (owner, session 12)

### D-154 · 2026-11-06 · M8 story answers (Q-A..Q-I)
**Decision:** Q-A one shared 2001 told four ways. Q-B each campaign has its own hero. Q-C each campaign ends with that faction's victory over
the others, followed by a Fallout-style ending: a sequence of captioned slides on what became of the campaign's major characters after the
last battle and how it shaped the wider world (each campaign's ending is "its" version; no canon winner across campaigns). Q-D no choice
points in the first slice. Q-E the Left Behind are hostile to everyone (see D-155). Q-F the Expedition's stance: Commissioner Strand decides
(the recommendation; the owner's "see above" referred to the Left Behind only) — assumed — confirm. Q-G existing leaders only. Q-H text now,
every line keyed in a locale file with an optional voice-file slot; the lead researches a local voice generator and writes the owner a
setup guide. Q-I reuse the five skirmish tiers (no resource cheats, D-061).
**Status:** confirmed (owner, session 12) except Q-F (assumed — confirm)

### D-155 · 2026-11-06 · The Left Behind: hostile survivors (lore change)
**Decision:** The Left Behind are no longer neutral. They asked the Authority for help and were refused; that refusal is their grievance.
Their history has years of infighting between bands; by the time the player meets them (2001) the clans have become loosely allied under a
warlord. They are hostile to every other faction. In their campaign they fight the Authority first (the refusal), then the Directorate (the
people who hid underground), then the Expedition (outsiders who came for the fuel); their victory is taking over. Tone: sympathetic but
ruthless — they have resigned themselves to ruthlessness because of how they were forced to survive. docs/lore/left_behind.md and the Left
Behind Archive entries are revised to match.
**Status:** confirmed (owner, session 12); revised lore text assumed — confirm (owner reads it)

### D-156 · 2026-11-06 · Session 12 contracts: floating docks and aircraft ammo (lead)
**Decision:** (1) Floating docks (Q-55 (a), D-153): a `water_building` with no `water` mask needs every footprint cell on water (deep or
shallow; existing rule) **and** at least one footprint cell orthogonally next to a land cell (not Water/Shallow) — "touches the shore";
otherwise placement fails with `PlacementResult.Blocked`. The four Expedition docks drop their `water` masks and keep their footprint sizes
(quay 3x2, anchorage 2x2, shipyard 3x3, drydock 4x3). The beachhead rule is unchanged (every footprint cell inside the zone). The masked
path stays in the code for future data. (2) Aircraft ammo: `VehicleType.Ammo` (TOML `ammo`, 0 = unlimited, the default) and
`VehicleType.RearmTicks` (TOML `rearm_ticks`, time for an empty aircraft to rearm fully). Each shot fired by the primary weapon uses one round;
an aircraft with 0 rounds cannot attack. On a carrier sortie, running dry ends the sortie (it returns and docks); docked aircraft rearm
alongside the existing repair. An aircraft with ammo and no carrier returns to the nearest own carrier with a free slot, else hovers over the
nearest own building that produces aircraft and rearms there. Only Expedition aircraft get ammo in this session (4-5 rounds, `(tune)`); other
factions keep unlimited ammo until the owner sees a table. Per-unit ammo is saved and hashed, written only when some type has ammo > 0 (old
hashes keep).
**Why:** owner answers D-153 (1) and (5); smallest rules that match Red Alert's behaviour.
**Status:** assumed — confirm (the shore-touch rule and the rearm-at-building fallback are visible to a player)

### D-158 · 2026-11-06 · Floating-dock art: variant C approved
**Decision:** The owner picked variant C (twin white float tubes with orange bands, davit crane, telex mast) for exp_anchorage; exp_quay,
exp_shipyard and exp_drydock were redrawn in the same style (session 12, branch wp/s12-dockart, art reaches GitHub via the owner's PC, D-084).
**Status:** confirmed (owner, session 12)

### D-159 · 2026-11-06 · The repository is public
**Decision:** GitHub Actions minutes for the private repo ran out (Windows runners, Free plan). The owner made pwnzor11/RTS_2027 public;
standard runners are free for public repos. A scan before the switch found no keys or tokens in the code or its history; 18 owner commits
carry his personal email, which he accepted. Rules unchanged: no secrets or personal data in commits, docs or fixtures.
**Status:** confirmed (owner, session 12)

### D-160 · 2026-11-06 · Commander prompt: posture guide
**Decision:** The LLM commander's system prompt gains a posture guide: turtle does not build faster, it holds attacks until the army is
much larger; use it at most two decisions in a row (to protect a lead or rebuild after a lost fight); when the enemy presses the base,
prefer balanced + focus army + a focus_cell in the own base; when behind, rebuild with focus army instead of turtling. No rules or AI
code change.
**Why:** owner's 32-game strength test (docs/balance/s12_commander.md): a run of 3+ turtle decisions after fighting began appears in 0 of
16 commander wins and 10 of 16 losses/draws; no strength gain overall.
**Status:** confirmed (owner asked for it, session 12); effect to be measured by re-running Test 1

### D-157 · 2026-11-06 · Left Behind storage; AI never builds on a dock or exit cell
**Decision:** (1) lb_yard storage 1000 → 3000 (Left Behind only; they have no Silo, so deliveries past 1000 were thrown away). Early
income is now within ±10 % of the Authority/Directorate mean at 5 min on all maps (docs/balance/s12_lb_income.md). (2) AI placement
(PlacementSearch, all factions, all tiers) never covers a building's dock cell or factory exit cell; before, a tier-2+ AI could park a
building on its own refinery dock and stop its income. No sim change; no pinned hash moved. The Expedition's small-map income lead
(+11..+16 %) fell to +0..+3 %, because it came from the Left Behind pulling the mean down.
**Why:** M7-19 follow-up diagnosis; the dock queue was a symptom.
**Status:** (1) assumed — confirm (owner approved Left Behind income tuning, D-153 (3)); (2) bug fix

### D-161 · 2026-11-06 · LLM commander: keep the posture-guide prompt; stronger directives are a later design item
**Decision:** Keep the D-160 prompt. After four 16-32-game tests (docs/balance/s12_commander.md) the commander is reliable flavour but not
a strength lever at tier 3, with either model or prompt. Making directives matter is recorded as a design note
(docs/design/15_commander_directives.md) for a later milestone; no more API tests now.
**Status:** confirmed (owner, session 12)

### D-162 · 2026-11-07 · Expedition AI balance is judged on 20-minute games; no rules-number change
**Decision:** Expedition per-tier balance (M7-18) is measured at `--ticks 24000` (20 min). At that length every re-measured cell is within
40-60 % decisive except tier 5 vs Directorate (80 %), so no rules numbers change. The Marine cost 150 → 110 variant was within noise and is
not adopted. Evidence: docs/balance/s12_expedition_numbers.md.
**Status:** confirmed (owner, session 12)

### D-163 · 2026-11-06 · Pilot review passed: Survey Convoy and the tutorial are the mission template; full send on M8
**Decision:** The owner played the tutorial and the schema-2 Survey Convoy and approved both ("love it all, full send"), which closes the
M8-11 review gate for the A1 pattern: locale-keyed text, one leader on the radio addressing "Commander", primary plus one hidden bonus
objective, one visible timer, raid size by difficulty tier, a bot test that wins at tiers 3 and 5. D1, L1 and E1 follow it now; the
remaining missions (M8-12) fan out per faction once those three land. The M8-13 Archive unlock map is accepted as drafted.
**Status:** confirmed (owner, session 12)

### D-164 · 2026-11-06 · Campaign review passed: writer choices kept, campaign text signed off
**Decision:** The owner played the whole campaign and answered Q-56..Q-58 in session 13: (1) Commissioner Strand decides the Expedition's
stance (confirms D-154); (2) all ten writer choices in docs/missions/s12_owner_review.md stay as written, including the auth_04 atomic
battery unlock (Q-58), the enemy AI firing the Sunrise Device early, pre-built Directorate bases, scripted Left Behind opponents, no
Second Drift objective (until a superweapon-shot API exists), and the cameo radio lines; (3) the four ending slide sets, the briefings and
the radio lines are owner-approved text and lose their DRAFT marks (D-086 satisfied). The bot-found rules issues (Ashthrower friendly fire,
bomber vs SAM, Conscripts vs Riflemen) are measured first; numbers change only with approval (D-152).
**Why:** M8-11/M8-12 review gate; the owner reported "everything looked great".
**Status:** confirmed (owner, session 13). Archive upgrade field notes marked "DRAFT (lead, session 12)" are not covered and stay open.

### D-165 · 2026-11-07 · Act-break stills on the briefing; M4-16 tier-4 levers (assumed — confirm)
**Decision:** (1) M8-14: an act-break still is shown on the briefing of each faction's act 2-4 opening mission (`still = "still_<faction>_actN"`
in the campaign file, image `data/images/stills/<id>.png`, height at most a third of the window); a missing image shows nothing. The style
(A game-style or B poster) is the owner's pick (Q-59). (2) M4-16 AI tuning (AI difficulty data, not unit rules, so D-152 does not apply):
the tier-4+ air producer waits for a 30-unit army (`TierPlanner.ArmyBeforeAir`); the raiding party forms at 30 units, 14 for the
Directorate (`TierPlanner.HarassMinArmy`, a measured patch, Q-61); commissar `retreat_health_pct` 25 -> 0.
**Why:** (1) the plan says "2-3 stills at act breaks" and the briefing is where an act starts; (2) 64-game ladder, docs/balance/s13_ladder.md:
tier 4 v 3 decisive 42 -> 56 % (Authority), 27 -> 52 % (Directorate); tier 5 v 4 Directorate 44 -> 66 %.
**Alternatives:** a full-screen still between missions (more UI work); air flag off at tier 4 (loses the feature); 30 units for both factions
(Directorate 4 v 3 falls to 41 %).
**Status:** assumed — confirm (lead, session 13, owner away)


### D-166 · 2026-11-07 · Q-60 balance numbers (owner-approved, D-152)
**Decision:** `dir_ash` warhead contaminate 40 -> 20; `dir_conscript` max_health 40 -> 50; no rule change for the bomber vs SAMs case
(unpowered SAMs explain it), but the low-power warning must be visible to the player.
**Why:** docs/balance/s13_campaign_balance.md: halving the ash cuts own Conscript losses by about a third and keeps the cloud; 50 health
evens Conscripts vs Riflemen without a price change.
**Alternatives:** radiation immunity for Conscripts/Ashthrowers (immune to all radiation); Conscript cost 80 -> 60; `exp_sam` without power.
**Status:** owner decision (session 14)

### D-167 · 2026-11-07 · Raiding threshold becomes a per-faction AI setting (Q-61)
**Decision:** `TierPlanner.HarassMinArmy` moves from code into data/ai/difficulty.toml as a per-faction key; values stay 14 (Directorate)
and 30 (the others), so behaviour and hashes do not change. Supersedes the "in code" part of D-165 (2).
**Why:** owner prefers tunable data over a code patch.
**Alternatives:** keep it in code (D-165 as written).
**Status:** owner decision (session 14)

### D-168 · 2026-11-07 · Mission difficulty tiers are tuned (Q-62)
**Decision:** mission scripts get tier knobs that the player can feel (more, earlier or stronger enemy waves at tiers 4-5, lighter at 1-2)
in dir_02, dir_04, lb_01, lb_03, lb_04, auth_04, exp_03, and exp_01, exp_04, lb_03 are hardened at every tier (exp_04 gets a harder finale).
Every mission must still be won by the bot walk at every tier inside its limit; the M8-15 table is re-measured afterwards.
Mission wave sizes are mission data, approved here as a class; unit rules (D-152) are not touched.
**Why:** M8-15 table: tier barely changed win time or losses in most missions; three missions were too easy at every tier.
**Alternatives:** leave the campaign gentle.
**Status:** owner decision (session 14)

### D-169 · 2026-11-07 · Act-break still style: C, mix, with more detail (Q-59)
**Decision:** act-break stills use style C: game-style colours and lighting with thin outlines, no pixelation, and a richer scene than the
pilot (owner: "add more detail, it looks bland, monotone and one-dimensional"): dawn key light with long shadows, sky gradient, atmospheric
depth, foreground/mid/background layers, colour variety, more props and effects. A second pilot (C v2) is approved before the fan-out.
**Why:** owner pick from the A / B / C contact sheet (session 14).
**Alternatives:** A game-style (soft, blurs small shapes); B poster (strongest look, furthest from the game palette).
**Status:** owner decision (session 14)

### D-170 · 2026-11-07 · Low-power marker on stopped buildings (bomber vs SAMs follow-up, D-166)
**Decision:** a small blinking "no power" icon is drawn over any defence or building that has stopped working because of low power.
The tooltip text and the event-feed message proposed with it were not taken (owner picked item 1 only).
**Why:** an unpowered SAM held fire with no hint to the player (docs/balance/s13_campaign_balance.md).
**Alternatives:** tooltip text change; event-feed "Low power." message; no change.
**Status:** owner decision (session 14)

### D-171 · 2026-11-07 · Build order: M11 UI and input overhaul before M9 LAN multiplayer
**Decision:** after M8 closes, the next milestone is M11 (UI and input overhaul, from the owner's campaign feedback), then M9 LAN.
Amends the D-087 build order; priorities (campaign, skirmish, LAN) are unchanged in importance.
**Why:** owner: the UI and controls make the game hard to read and navigate; fixing them makes every later milestone easier to play and test.
**Alternatives:** LAN first (D-087 order).
**Status:** owner decision (session 14)

### D-172 · 2026-11-07 · M11 owner picks (UI audit section 6.2)
**Decision:** (1) selection info as option B, a bottom-centre card over the map; the sidebar build menu never moves. (2) Objective guidance:
all tools: edge-of-screen arrow with distance, pulsing ground marker, minimap ping, jump key (J) with Tab to cycle, one-line objective bar
with O for the full list, and a short camera cue when a new objective appears. (3) Unit size: draw units about 25 % larger; zoom default
unchanged. (4) Attack-move: left click commits, right click is an alias; Esc cancels. (5) No colour-blind mode (owner does not want it).
(6) Full key rebinding now (Controls page with click-to-rebind, conflict warnings, reset to defaults).
**Why:** owner answers to docs/reviews/2026-11-07_ui_input_audit.md section 6.2 (session 14).
**Alternatives:** A sidebar split, C tabs; a subset of guidance tools; default zoom 1.5; read-only key table first; colour-safe palette.
**Status:** owner decision (session 14)

### D-173 · 2026-11-07 · Skirmish defeat rule: no way to rebuild for 2 minutes = defeated (Q-63)
**Decision:** in skirmish (and LAN later), a player who for 2 minutes (2 400 ticks) has no production structure (any building that can
produce units or structures), no builder unit that can found one (MCV-type / deployable), and no harvester is defeated, whatever other
units or buildings remain. The 2-minute clock resets as soon as any of those exists again. Campaign missions keep their own win/lose rules.
**Why:** Directorate games were mostly draws: the loser kept 13-15 structures (its shaft, factories and about a dozen defences) and 1-2 units with no income, so the old
"any non-wall building keeps you alive" rule never ended the game (docs/balance/s14_mopup.md; after D-173 the draws remain, Q-64). Owner picked option 1 and chose that
remaining units do not count.
**Alternatives:** army counts (fewer games end); hatches don't count; AI-only mop-up; leave it.
**Status:** owner decision (session 14)

### D-174 · 2026-11-07 · Q-64: AI siege behaviour against defended bases
**Decision:** the AI gets a siege mode (gather a bigger force, long-range units against defences, production and tech roots first), measured before/after.
**Why:** Directorate draws are base stalemates; half survive even at 60 000 ticks (docs/balance/s15_laptop_series.md set C). **Alternatives:** stricter defeat rule; accept draws.
**Status:** owner decision (session 15)

### D-175 · 2026-11-07 · Q-66: tune auth_01..auth_03 tiers like the other nine missions
**Decision:** tier 3 = the old design, tier 5 clearly harder, tier 1 easier; bot wins every tier (docs/balance/m8_15_acceptance.md "Session 15").
**Status:** owner decision (session 15)

### D-176 · 2026-11-07 · Q-65: re-measure on the owner's laptop first
**Decision:** run tools/measure/run_s15_series.ps1 (17 rows) before choosing a fix; results in docs/balance/s15_laptop_series.md. Superseded in part by D-182.
**Status:** owner decision (session 15)

### D-177 · 2026-11-07 · Camera keys stay as they are
**Decision:** QWERT stay the sidebar tab keys; scrolling is arrows, screen edge and minimap; no WASD (UI audit K3).
**Status:** owner decision (session 15)

### D-178 · 2026-11-07 · M11 batch 3 scope
**Decision:** the whole planned batch 3 (selection toolkit, alert stack + minimap pings, one UI scale rule, cursor sprites, placement ghost,
cameo tooltips) plus a UI-scale (75-200 %) and text-size setting. Colour-blind mode stays out (D-172 (5)).
**Status:** owner decision (session 15)

### D-179 · 2026-11-07 · The lead may merge PRs to main after Windows CI is green
**Decision:** the owner allows the lead to merge its PRs once Windows CI is green (replaces "the owner merges" from sessions 13-14).
**Status:** owner decision (session 15)

### D-180 · 2026-11-07 · Maps overhaul M12 (docs/design/16_maps_overhaul.md)
**Decision:** (D1) props = decoration plus static blocking props (block ground units like walls, not sight; not destructible); (D2) generator =
zone skeleton with natural expansions, a contested centre and 2-3 lanes with chokepoints (stamps later); (D3) starts = walled main with one 4-6 cell
ramp plus a natural expansion; (D4) the new generator REPLACES the old one (skirmish saves on generated maps stop loading, with a clear message;
the hand-made data/maps stay); (D5) procedural placeholder props now, real art in M10; (D6) all of M12 before M9 LAN.
**Why:** owner play-test: maps look flat and samey; previews show no lanes, no chokes, no expansions, empty plateaus. **Alternatives:** section 4 of the design doc.
**Status:** owner decision (session 15)

### D-181 · 2026-11-07 · Terrain look C is the default (M11-07, M11-08)
**Decision:** palette with contrast >= 1.5 between ground types, road edges, shore band, cliff lip and shadow, tone drift, resource fields drawn
as slag heaps (ash) and crystals (pitchblende) by density, bright resource colours on the minimap. The old look stays as variants/classic.
**Alternatives:** A palette only; B edges without new fields. **Status:** owner decision (session 15)

### D-182 · 2026-11-07 · Q-65: AI-only fixes for the Directorate and the Left Behind
**Decision:** fix the Directorate's tier 4-5 AI and tune the Left Behind tier-3 AI profile; re-measure; unit numbers only if a gap remains (as a table, D-152).
**Why:** Directorate decisive % falls with tier (v Authority 74/50/23 % at tiers 1/3/5); Left Behind wins 78-86 % at tier 3 but 58-60 % at tier 5.
**Alternatives:** buff Directorate late units; Left Behind numbers table; leave. **Status:** owner decision (session 15)

### D-183 · 2026-11-08 · Map study revises D-180 (docs/design/17_oramap_study.md)
**Decision:** from 15 OpenRA community maps the owner likes (measured as reference; nothing copied or converted): (D3) start bases are MIXED per
seed, mostly a soft shell with 2-3 approaches at least 3 cells wide, sometimes a walled main with one ramp; 2v2 teammates start side by side;
layout follows the measured maps: about 3 lanes (1-5) at least 3 cells wide at the narrowest, direct paths (the 1.2x path rule is dropped),
about 4 contested fields, a natural expansion on about half the maps; prop densities and placement rules from the study.
**Why:** the liked maps contradict D-180 D3 and design 16's path rule (it would reject 13 of 14). **Status:** owner decision (session 15)

### D-184 · 2026-11-08 · Art direction from the OpenSAGE study (docs/design/18_art_guidance.md)
**Decision:** smoke/fire damage tiers (smoke under 50 %, fire under 25 %, a burst on entering a tier) for buildings and vehicles; fading rubble,
vehicle wrecks and scorch (view only); dust behind moving ground vehicles; infantry larger with a guaranteed team-colour area; keep the upper-left
sun (~40 degrees) and hard dithered shadows as a written rule (section 7) and add a subtle per-biome mood tint. Pilot first, then fan out.
**Note:** OpenSAGE contains EA-derived code; it is a presentation reference only (D-050). **Status:** owner decision (session 15)

### D-185 · 2026-11-08 · All 18 infantry re-rendered at figure scale 1.25
**Decision:** after the Marine/Commissar pilot, every infantry type is rendered 1.25x larger with team-colour emphasis (tools/art/house_audit.py:
at least 10 px of house colour on every facing). Manifests carry `figure_scale_permille = 1250`; the renderer's "Larger infantry" step skips such
sprites, so infantry appear at about 1.56x the original art (D-172's 1.25 times the baked 1.25). **Status:** owner decision (session 15)

### D-186 · 2026-11-08 · Q-65 follow-ups: Left Behind per-faction AI override; Directorate tier 4-5 economy fix
**Decision:** data/ai/difficulty.toml accepts `[faction.<id>.tier<N>]` override tables (AI behaviour only, never unit numbers). The Directorate
tier 4-5 expansion refinery is built right after the Assembly Bore (s15_q65.md). **Status:** owner decision (session 15)

### D-187 · 2026-11-08 · Look pilot approved for all factions
**Decision:** D-184 items (damage tiers, remains, dust, mood tint) ship for every faction; infantry stay at the pilot size (about 1.56x the original
art). Old look: `RTS_FXLOOK=off`, `RTS_MOOD=off`. **Status:** owner decision (session 15)

### D-188 · 2026-11-08 · Imported open-licence art (credits in docs/CREDITS.md, notices in THIRD_PARTY_NOTICES.md)
**Decision:** effects from Skorpio's "sci-fi-effects" (GPL-3.0 option): new fireballs, energy explosion, lightning, lightning_fork; fire from the
LPC flames (Sharm, GPL-3.0 option); dust and damage-smoke puffs from Kenney Smoke Particles (CC0). Props: 0 A.D. renders (CC-BY-SA 3.0, kept as
separately licensed data files in data/sprites/props/zeroad/) for Boulder, Rubble and Snag; the LPC atlas (GPL-3.0 option) for ConcreteBlock and
DeadTree; procedural for the rest. Old sets: `RTS_FX=classic`, `RTS_PROCEDURAL_PROPS=1`. Packs without a verifiable licence (space-ship packs,
Wesnoth-style icons) are reference only; Kenney Sci-Fi RTS (CC0) is reference only (style clash).
**Status:** owner decision (session 15)

### D-189 · 2026-11-08 · Warzone 2100 models: kitbash, keep our designs
**Decision:** Warzone 2100 data (GPL-2.0-or-later) may be used through tools/art/wz (our own .pie importer) to kitbash parts (tracks, turrets,
weapon geometry, cyborg bodies) into our units where they add detail. Our unit and building designs stay. Never use Warzone's CC-BY-SA 3.0 files
(page-6/17/30 textures, wheel propulsion models): CC-BY-SA 3.0 cannot be relicensed under the GPL. The pilot's candidate sprites were not shipped.
**Status:** owner decision (session 15)

### D-190 · 2026-11-08 · Left Behind tier 3 early army cap
**Decision:** `[faction.left_behind.tier3] army_cap = 20, army_cap_until_tick = 3600`. Measured (32 games): LB3 v Authority 3 68 -> 53 %,
v Expedition 3 63 -> 56 %, v Directorate 3 87 -> 74 % (draws 2 -> 9); LB5 now beats LB3. The remaining Directorate 3 gap is Directorate's own
early game: an AI-only package next session (Q-67). These are 32-game cloud cells; the owner's 64-game laptop row (s15_laptop_series.md,
LB3 v Authority 3 78 %) is a different sample. **Status:** owner decision (session 15)

### D-191 · 2026-11-08 · Q-67 target band and session 16 run mode
**Decision:** Q-67 (Directorate tiers 1-3 early game, AI only) aims Left Behind 3 v Directorate 3 at 50-65 % decisive with no rise in draws
(base 17 / 6 / 9), Directorate 1 v Authority 1 not stronger than today (21 / 6 / 5), other tier 3 cells and the controls within noise.
Session 16 runs with the owner present: helpers on Sonnet for AI, maps and art, Haiku for docs and bulk edits; the map and art pilots stop
for the owner's look before any fan-out; new sprites reach GitHub through an owner-run art_push zip.
**Status:** owner decision (session 16)

### D-192 · 2026-11-08 · Vehicle team-colour pass
**Decision:** team (house) paint must read on every facing (house_audit.py: body >= 10 px, turret >= 6 px per facing). Pilot (Expedition
Destroyer, Glide Tank) approved as is, including the Glide Tank's top stripes; fanned out to every flagged sprite (13 more vehicles/turrets
and lb_spike_nest), design otherwise unchanged. The Hoverlander's copper dome, missing from its current script, was rebuilt in the script.
Result: 0 of 138 sprites flagged. **Status:** owner decision (session 16)

### D-193 · 2026-11-08 · 2v2 default teams on layout 2 maps
**Decision:** a 4-player skirmish on a layout-2 map defaults to teams 1+2 v 3+4 (teammates start side by side, D-183); the setup screen can
still change it. Layout-1 maps keep today's default. The Rust Belt layout-2 pilot v1 was not approved (one diagonal half used, river in a
corner, 3/4-player maps failing checks): reworked before any fan-out. **Status:** owner decision (session 16)

### D-194 · 2026-11-08 · Q-67 Directorate tiers 2-3 hold early tech
**Decision:** the Directorate AI at difficulty tiers 2-3 holds the Isotope Works and Rad Mortar until it has 18 units (army + home guard +
detached; `TierPlanner.DirectorateTechArmy`, tune). AI behaviour only, no rules or data numbers. Measured (32 games, docs/balance/s16_q67.md):
LB3 v Dir3 17/6/9 (74 %) -> 17/12/3 (59 %, band met, draws down); Dir1 v Auth1 and the Auth5 v Auth1 control bit-identical; Dir3 v Auth3
44 -> 65 %; Dir3 v Exp3 50 -> 41 % (near noise); Dir5 v Dir3 5/9/18 -> 5/13/14. Tier 5 falling behind tier 3 gets a follow-up package
(Directorate tier 5 ladder). Owner's 64-game laptop confirmation: tools/measure/run_s16_q67.ps1. **Status:** owner decision (session 16)

### D-195 · 2026-11-08 · Directorate tier 5 ladder (D-194 follow-up)
**Decision:** the Directorate AI at tier 5 holds the Isotope Works and Rad Mortar until it has 24 units (`TierPlanner.DirectorateTechArmyTier5`,
tune); tiers 2-3 keep 18, tier 4 and other factions untouched. AI only. The owner's laptop run before it (64 games) had Dir5 v Dir3 8/27/29 and
Dir5 v Auth5 13/29/22. After (cloud, 64 games): Dir5 v Dir3 30/20/14 (60 %), Dir5 v Auth5 24/25/15 (49 %); 32 games Dir5 v LB5 15/14/3.
Tier 1-3 cells and the control are bit-identical. Data knobs (attack threshold/ratio, expansion tick) did not move it. Laptop rows are in
tools/measure/run_s16_q67.ps1. **Status:** within the owner's D-194 instruction (session 16)

### D-196 · 2026-11-09 · Directorate tuning closed; art renders move to the owner's PC
**Decision:** (1) The owner closed Directorate AI tuning after his 64-game laptop run on D-194 + D-195 (docs/balance/s16_q67_laptop_after_tier5.md);
LB3 v Dir3 66 % and Dir3 v Auth3 71 % are accepted. (2) Blender art renders now run on the owner's PC: `.venv-art` in the RTS_2027 folder
(Python 3.13, bpy 5.2.3, numpy, pillow; about 18 s per vehicle against about 90 s in the cloud). Art passes hand the owner a `render_*.ps1`; he sends
contact sheets back and commits/pushes the sprites himself (no more art_push zips). A full re-render on his PC (art/pc-rerender-<date>, local branch)
gives the baseline and a script-drift check. **Status:** owner decision (session 16)

### D-197 · 2026-11-09 · Map layout 2 status after v5
**Decision:** layout 2 (all four biomes) stays off by default (`CurrentLayout = 1`) until it passes the owner's three blockers from M12-12:
an Expedition coast in every biome (M7-1), start side <= 60 % decisive in tier-3 mirrors (64 games per biome), and income within +-10 % of layout 1 at
3, 5 and 10 min. After v5: coast built for all biomes, but the Expedition still does nothing on Ash Plains (cause not found); fairness met on Rust Belt
(51 %) and Glass Coast (50 %), not on Mountains (57 %, inside the limit but skewed for Directorate/Left Behind) or Ash Plains (64 %, its mirror skeleton);
income not met (+11 to +30 % at 5-10 min; Glass Coast +39/+109 %; field richness is not the lever). Biome identity: Mountains ridge/plateau mains,
Ash causeway. Details: docs/balance/s16_layout2.md "v5 re-measure". **Status:** owner decision (session 16), next steps in Q-68 and Q-69

### D-198 · 2026-11-09 · Ash Plains layout 2 goes half-turn
**Decision:** Q-68 answered (a). The Ash Plains layout 2 skeleton becomes a half-turn with the same crossing pair on both sides of the centre,
so start fairness comes first and each side gets the same crossing mix. The mirror skeleton and its two different crossings go (D-197 measured
64 % start-side wins on Ash Plains with the mirror). Not decided here: the Ash Plains Expedition stall (D-197). **Status:** owner decision (session 17)

### D-199 · 2026-11-09 · Layout 2 income: fields further from the refinery spot
**Decision:** Q-69 answered (a). On layout 2, main and natural fields move further from the refinery spot and fewer field patches sit near the
main, because the income rate follows harvest distance, not field money (D-197). Plus: the owner asked to fix the script drift found by the PC
re-render (four LB rigs and auth_turret) before the re-render branch becomes the baseline. **Status:** owner decision (session 17)

### D-200 · 2026-11-10 · Art baseline is the owner's PC re-render
**Decision:** the full re-render on the owner's PC (123 assets, 144 sheets, PR #54) is the committed art baseline, `auth_turret` included, so later
re-renders on that PC give a clean drift check (D-196). The four Left Behind rigs (muster, lookout, dynamo, refinery) keep the darker grey-edged pad
the scripts have produced since 29827be; their old sheets were stale (rendered before the shared `lb_common` dust colours changed). `auth_turret`
differed only by pad-edge pixel rounding between machines (cause not isolated). No script changed. **Status:** owner decision (session 17)

### D-201 · 2026-11-10 · Layout 2 income test: 10-minute point over live games only
**Decision:** the income blocker of D-197 (within 10 % of layout 1) is measured at 3 and 5 minutes as before; the 10-minute point counts only games
still running at 10 minutes (tick 12 000). The plain 10-minute mean includes dead sides and games that ended early, so it tracks game length
(docs/balance/s17_layout1_baseline.md). Reference numbers are the owner's 64-game layout 1 baseline, not the 16-game cells of s16_layout2.md.
**Status:** owner decision (session 17)

### D-202 · 2026-11-11 · Session 18 audit: how findings are handled
**Decision:** the owner asked for an adversarial audit of code, UX/UI and faction data. Rules for the session: clear bugs outside match
outcomes are fixed with tests (S18 PR); anything changing match outcomes, numbers, visible wording or design goes to the owner. Balance
evidence: data analysis, cloud duels and matches, plus a laptop series (`tools/measure/run_s18_audit.ps1`). Report:
docs/reviews/2026-11-11_s18_audit/README.md. **Status:** owner decision (session 18)

### D-203 · 2026-11-11 · Dying actors in mission scripts; exp_04 keeps its tested balance
**Decision:** `Actor.Alive`, `Actor.Cell` and objective markers treat an actor at 0 health as gone, also inside its own destroyed callback
(fixes the auth_01 soft-lock, MIS-03). exp_04's Finale guard still marches on the second blow (script changed to keep today's tested balance);
auth_01 tier-5 bot tests now expect a loss when both scrapers die, as the script intends. **Status:** owner decision (session 18)

### D-204 · 2026-11-11 · Mission replays are "watchable only"
**Decision:** campaign and tutorial replays are labelled watchable only and show no desync warning (they carry no tier or tech list, GS-03);
skirmish replays keep the exact desync check. **Status:** owner decision (session 18)

### D-205 · 2026-11-11 · Session 18 decision table: all recommendations taken
**Decision:** the owner took every recommendation in docs/reviews/2026-11-11_s18_audit/README.md sections 2.1, 2.2 and 2.4 (items 1-26 and the
UI/wording/design list; item 11 waits for the owner's laptop series; item 23 implement the ashfall hazard; item 24 add an attack-ground order;
item 26 hide or reword now, build later). Numbers (section 2.3): apply the suggested values now and measure afterwards (D-152 path, values listed
in the report and the wave-2 reports). QoL to build: game speed incl. slow; idle button and more alerts; cameo hotkeys, repeat production, range
rings, strong/weak tooltip line; per-slot handicap and starting conditions, stance buttons and shift-queued waypoints. Built from session 19 as
the M13 batch, with one golden/baseline re-pin. **Status:** owner decision (session 18)

### D-206 · 2026-11-11 · Field Manual (game booklet)
**Decision:** a 1990s-style printed manual: print-ready half-letter PDF, about 80-120 pages, hybrid voice, premise plus mission list (no endings),
stat and tech pages generated from data (docs/design/19_field_manual.md). Pilot approved: keep the look; the draft text and the Authority lore
draft are approved; full page for main units, half pages for the rest; every unit and building gets a full hero render on the owner's laptop
(`tools/booklet/render_heroes.ps1 -Pilot`, then `-All`). Full build after the M13 fixes land. **Status:** owner decision (session 18)

### D-207 · 2026-11-11 · Session 19 owner picks for the M13 batch
**Decision:** (1) MIS-10: the pre-placed `dir_sunrise` charge in auth_04/exp_04 matches the script's sunrise timer (exp_04 has no sunrise
moment, so its device never charges); MIS-16 a+b: `dir_bore_head` in dir_01's list, `auth_factory` in auth_01's; MIS-18: dir_04 says "charged"
only when the real charge is ready; MIS-09: razing the dir_04 HQ before the device is charged is a mission loss; MIS-12: speaker ids show the
Archive title, the Left Behind leader entry opens "Jessup Calloway-Ross is the war-captain". (2) Ashfall hazard (D-028, item 23): infantry on
ashfall or pitchblende lose 2 HP every 30 ticks before rad resistance (`[radiation] ashfall_damage`, `ashfall_interval_ticks`). (3) Kill value
(LE-04): the non-buildable Supply Tender, Command Ship and packed Mother Rig are worth 1500 each. (4) Cameo grid hotkeys default to the number pad.
(5) Archive lore for units whose mechanics are not built yet stays as written; only tooltips are reworded (TodayText). **Status:** owner decision (session 19)

### D-208 · 2026-11-11 · Session 19 numbers picks (2.3) and Q-70
**Decision:** other warheads stay at 100 % against ship armour until the "after" laptop series (small-arms and armour-piercing classes copy
the Expedition values); the eight Directorate structures that copy Authority ones get -20 % HP and -15 % cost and build time; the Commissar
gets its own fire-rate aura (radius 3, +15 %); the option-only 2.3 items wait for the after-series. Q-70 (a): layout 2 money per field cell
cut on Rust Belt (-9.8 % measured) and Glass Coast (-12.8 %, the density floor; a deeper cut would need smaller fields, decided after the
after-series). Values: docs/reviews/2026-11-11_s18_audit/s19_numbers.md. **Status:** owner decision (session 19)

### D-209 · 2026-11-11 · Path work cap per tick (amends D-063 point 5)
**Decision:** a path request whose goal the start cannot reach is retargeted to the nearest reachable cell (region labels, not saved); one
`PathService.Process` call starts at most 4x the per-tick expansion budget but never splits one tick's requests (a saved "waited" bit keeps
the order deterministic and mirror-fair); group search uses a heuristic and a region filter. Fixes SP-01 (20 units on a building stalled all
pathing 22.6 s). **Why:** D-063(5)'s "answer one tick's requests together" is kept; the cap only bounds a pathological batch. **Status:** confirmed (session 19, D-205 item 5)

### D-210 · 2026-11-11 · Field Manual hero renders approved; render pilot fan-out
**Decision:** the 12-render Authority pilot is approved as is; `render_heroes.ps1 -All` runs on the owner's laptop. **Status:** owner decision (session 19)

### D-211 · 2026-11-11 · Ashfall vs the campaign: detour, step-off, missions off until a retune
**Decision:** with the ashfall hazard on, 13 campaign bot tests lost (escorts and staging cells sit on fields). Owner picked (a): foot units pay
`[radiation] ash_path_penalty = 4` to enter an ash cell (they detour when the way round is short; an ordered goal on ash is still reached),
the Recruit AI's rally and wave staging cells step off ash, and idle infantry standing on ash step to the nearest clean cell within 3 (Q-72,
assumed — confirm; ash-stealth infantry stay). That was not enough, so the owner then picked: campaign and tutorial missions run with ash
damage and the ash path cost OFF (`MissionSession.CampaignRules`) until a campaign retune (M13-17); skirmish keeps all of it. lb_03 tier 5
and exp_04 tier 5 (non-ash M13 shifts) are known, skipped cases until the retune. **Status:** owner decision (session 19)

### D-212 · 2026-11-12 · Owner confirms the remaining assumed decisions after D-153
**Decision:** confirmed: the revised Left Behind lore text (D-155), D-156 (floating docks, aircraft ammo), D-157 (1) (lb_yard storage 3000),
D-165 (act-break stills, tier-4 AI levers) and the Archive upgrade field notes marked "DRAFT (lead, session 12)" (the DRAFT marks are to be
removed in data/archive/upgrades.toml and structures_expedition.toml). **Status:** owner decision (parallel session to s19)

### D-213 · 2026-11-12 · M9 LAN design answers (Q-M9-a..o)
**Decision:** docs/design/20_m9_lan_plan.md §9: UDP with our own reliability for lobby AND match (owner: "UDP for the lobby too"); host-sequenced
turn bundles; the host picks a 2/3/4-tick delay in the lobby (default 3); a dropped player's slot goes to an AI; desync pauses and the host
picks Resync / Continue / End; the host runs every AI incl. LLM commanders on its key; chat outside the sim; anyone pauses, 3 s resume, 1x;
no LAN saves; when the host leaves, clients may continue vs AI; the host sends custom maps; up to 2 spectators; no reconnect; no co-op
campaign in M9; the §8 assumptions confirmed (amended by D-214: the installer offers the firewall rule). **Status:** owner decision

### D-214 · 2026-11-12 · M10 polish plan answers
**Decision:** docs/design/21_m10_polish_plan.md: assets run in parallel now, code after M9 (later overridden for this work by D-216), look
first; the 26 missing ids in today's style; building depth via the shared pipeline; Warzone tracks on every tracked vehicle; M10-05 closed;
about 20 music tracks, style mixed by faction (ACE-Step 1.5, MIT); Piper barks later with M8-18; SFX per weapon class and faction; zip +
Inno Setup installer with an optional LAN firewall rule; GitHub Releases + update notice; all eleven extras. **Status:** owner decision

### D-215 · 2026-11-12 · M14-06 art: pilot approved, 24 more scripted
**Decision:** the owner approved the Authority Bomber + Airfield pilot ("approve, fan out"); scripts for all 26 ids are on art/m14-06-pilot
(PR #67, draft). Sheets and manifests are generated on the owner's laptop (tools/art/render_m14_06.ps1, D-196) and land with the PNGs.
**Status:** owner decision

### D-216 · 2026-11-12 · M9 and the code-only part of M10 built overnight, ahead of the D-171/D-205/D-206 order
**Decision:** at the owner's request M9 LAN and every code-only M10 feature were built overnight in a session parallel to session 19, from a
snapshot of integ-s19, and landed after session 19: wave 1 (Net core + M10 features) as PR #69, wave 2 (lobby, in-game LAN, UDP soak,
review fixes) as its own PR, each merged when Windows CI was green. Lead contracts: a graphics-free src/Net project (NetMessage, INetTransport,
NetProtocol, NetCodec) and SimHost.Outbox + SimHost.StepWithBundle as the only SimHost seam; no sim, save, replay or rules-hash format change.
An independent review found 8 LAN defects (one HIGH: lobby and match polled the transport on different clocks); all fixed with tests
before landing. Open: Q-73 (par times), Q-74 (chat in LAN replays). Notes: docs/process/m9m10_overnight.md.
**Status:** owner request; implementation choices assumed — confirm at the owner's two-PC LAN test

### D-217 · 2026-11-12 · Owner answers at the start of session 20 (Q-71, Q-72, run mode)
**Decision:** (1) Q-71 (a): auth_01 tier 5 is allowed to be winnable after the M13 batch; the S18 MIS-03 expectation (tier-5 bot loses both
scrapers, D-203) is dropped and the balance table expects a win like every other tier. (2) Q-72 keep: idle infantry on ash step to the nearest
clean cell within 3 cells, ash-stealth infantry stay on ash, foot `ash_path_penalty = 4` (D-211) — confirmed. (3) Session 20 runs unattended:
the lead takes its recommendation on reversible questions and logs them "assumed — confirm". (Committed during session 20 as "D-212"; renumbered
at the merge with the parallel M9/M10 session, which had already used D-212..D-216.) **Status:** owner decision (session 20)

### D-218 · 2026-11-12 · Session 20 follow-up fixes (M13-11, M13-18) and how the campaign retune is measured
**Decision:** (1) SP-03: a Move waiting behind an allied vehicle that is still advancing (its own stuck count below half of MaxStuck, not head-on,
not hostile) counts one wait cycle in eight (`MoveMission.QueueSlowdown = 8`, keyed on the tick) toward giving up; idle, hostile, head-on and
equally stuck blockers count every cycle as before. (2) SIMDET-03: a re-ordered group member releases its queued path entry
(`PathService.ReleaseMember`); the saved format is unchanged. (3) SP-04: a harvester whose path to a field fails skips every cell within 4 of it
for 1200 ticks (`SkipRadius`, `SkipTicks`, both (tune)); the economy part is written as version 4 unless a skip is active (version 5 then), so no
hash or save moves. (4) Item 19: a weapon with a reach of one cell or less acquires within its owner's sight on attack-move and guard (Sapper,
Combat Diver, Kite, Expedition bomber; death weapons such as the Volunteer burst are not used to acquire); a Defensive-stance marcher still never chases. (5) S18-AI-NEW-3: the AI's placement lane gets a 2x2
outlet at its far end on the side facing the map centre (the exit stays south); ClearBuildArea, StagingCell and CancelAfterBlockedSearches are
removed (0 uses in 15-minute games, all factions, tiers 1 and 5). (6) LB Still Rigs: 14-18 in a corner base, all used by the army (supply
about 189/200), so no cap; a global cap of 8 was tried and reverted because it weakened every AI. (7) The LB yard dock livelock does not
reproduce in the open; a one-cell dead-end corridor to the dock deadlocks and is pinned as a skipped Desired_ test (Q-76). (8) M13-17 is
measured as a win rate over 12 mission seeds per mission-tier (ash on), not a single fixed-seed walk: the fixed-seed walks were passing by
luck (auth_01 tier 3 wins 3/12 even with ash off), and ash on changes the rate notably only for dir_02 tier 5 (10/12 → 6/12). Targets: Q-75.
**Why:** audit items SP-03, SP-04, SIMDET-03, item 19 and the s19 follow-ups; the sweep showed the bot walks are seed coin-flips.
**Status:** lead decision (session 20); numbers marked (tune) are assumed — confirm.

### D-219 · 2026-11-12 · The default stance engages hostile units in sight (amends M13-15 stances)
**Decision:** the Aggressive stance (still the default; names unchanged) acquires enemy vehicles and infantry anywhere inside the unit's sight
while guarding or attack-moving; buildings are still acquired only inside weapon range. The Guard leash (sight from the post) walks it back to
its post when the target leaves. Defensive (weapon range, 3-cell chase) and Hold Fire are unchanged; melee units keep their sight search
(D-218 item 19). An attack-mover next to a goal cell that something else holds, once blocked, guards where it stands (it used to step toward the
held cell for good and never guarded). Code: `CombatMissions.AcquireFor`, `Simulation.AcquireTarget(..., mobilesOnlyBeyondRange)`.
**Why:** the campaign retune kept finding the same loss: an enemy parked just outside a unit's weapon range (auth_01 raid jeep, dir_04 tank, auth_03
rocket nest) killed everything around it unopposed. Owner: "when something hostile comes within that visual range they will move to engage".
**Alternatives:** tuning each mission around it (done first for auth_01, reverted where no longer needed); engaging buildings in sight too (lead:
no, idle units would walk into every turret they can see). Moves the golden and combat final hashes; skirmish balance re-measured by the laptop
after-series (Q-77). **Status:** owner decision (session 21)

### D-220 · 2026-11-12 · Development saves and replays are disposable
**Decision:** no save or replay made during development needs to load after the next session. After a rules or format change, re-pin golden
hashes and baselines and bump formats without loaders for older ones when that is simpler; no migration code. Shipped-release compatibility is
a separate decision when there is a release. **Why:** owner: "don't waste time trying to preserve old saves or replays". **Status:** owner decision (session 21)

### D-221 · 2026-11-12 · Tier-5 fixed-seed walks only need a clean run
**Decision:** the single-seed campaign walks (BalanceTableTests, per-mission test classes, the mid-mission save test) assert a win at tiers 1-3
only; at tier 5 they assert a clean run (no script errors, no locale warnings). The balance gate for every tier is the 12-seed sweep against the
Q-75 targets (SeedSweepTests, D-218 point 8). Per-mission tier-5 cases whose fixed seed loses were dropped (auth_03, lb_02, lb_03, exp_04); the S19
known-shift skips and auth_01's expected loss are gone. lb_01's idle-player test now checks that an idle player never wins (under D-219 the start
force stops the convoy, so it no longer loses). **Why:** tier 5 aims at 3-12 wins in 12 seeds, so one fixed seed may lose. **Status:** owner decision (session 21)

### D-222 · 2026-11-12 · M13-17 campaign retune: ash on in missions, Q-75 targets, mission changes
**Decision:** (1) Campaign and tutorial missions run with the ashfall hazard (MissionSession.CampaignRules removed; ends D-211's override).
(2) Q-75 targets over 12 seeds with ash on: tier 1 >= 10/12, tier 3 >= 8/12, tier 5 >= 3/12. (3) Mission changes, measured under D-219:
auth_01 raid 2 brings its jeep from tier 4 (was 3); auth_03 coast counter infantry {4,5,8,11,14} -> {4,5,6,7,8}, ashthrowers {2,2,3,5,6} ->
{2,2,2,3,3}, sappers {0,0,2,3,4} -> {0,0,1,1,2}, and its bot sweeps onto the picket's rear; auth_04 tier-5 home raid 1 at tick 2400 (was 1800);
dir_04 tier 5 drops the wave-3 tank; lb_03 tier-5 column at 4300 with 7 riflemen (was 3400 / 8) and the start force off the ash field at (21,24);
exp_03 tier-5 air raid split into 2 bombers at 3800 and an unannounced 3-bomber sortie at 6400 from the south-west sea. Table:
docs/balance/s21_campaign_retune.md. (4) Q-74 (a): LAN chat gets a block in the next replay format bump. (5) The s19 after-series confirms
the start-side fix (M13-10). **Rejected:** auth_03 tier-5 tide 9000 -> 2780 (a visible countdown three times shorter). **Status:** owner
decisions (targets, "fix all, show me after"); per-mission values are the lead's within those targets — confirm with the laptop sweep

### D-223 · 2026-11-12 · Q-77 / M13-13 balance round (session 22)
**Decision:** owner: "take all recommendations" from docs/balance/s22_balance_options.md. Row fixes: Expedition shipments `value` 2000 -> 1400;
`dir_rocket` warhead splash 1 -> 0 (Rocket Truck; the air-only Rocket Nest shares the warhead); the skirmish start kit follows the biome's
mirror symmetry (SkirmishStart.KitFlip); factory exits and refinery docks follow it too (Simulation.OrientedOffset; half-turn maps unchanged).
M13-13: Bulwark 500 HP / 900 / speed 18; Howitzer damage 32; Dirigible speed 16; Reactor Crawler range 1280, damage 60; repair auras 5 permille;
Left Behind Harpoon gun targets both; Refinery Rig 700; tut_01 cash 4500; Salvo Array charge 4800 ticks. Item 1 (non-Expedition HE/flame v
ships) stays 100/100; item 13 waits for a hand check. The owner's A/B (balance_runs\s21ab_nod219) ruled out D-219 as the cause of the
Authority's slide. **Why:** traced root causes (shipments economy, Rocket Truck splash, mirror-map handedness). **Status:** owner decision;
numbers to be confirmed by the laptop series (tools/measure/run_s19_after.ps1, run_s22_mopup.ps1) and campaign sweep.

### D-224 · 2026-11-12 · Mop-up AI on in skirmish (M13-20)
**Decision:** the s14 mop-up returns with a "beaten enemy" trigger (MopupPlan: tick >= 9000, army >= 8 and >= 3000 worth and >= 4x the biggest
enemy army seen in 3600 ticks for 1200 ticks, home calm 900 ticks), siege-style targets (D-174 SiegePlan) and fog-sweep parties of 6. On for
tiers with respond_to_attacks; off at tier 1 and in campaign missions (MissionBot); `ai match --mopup on|off`. Constants are first guesses
marked (tune). **Status:** owner asked for the fix (session 22); effect to be measured on the laptop (run_s22_mopup.ps1).

### D-225 · 2026-11-12 · Q-76: harvesters queue outside a dead-end dock lane
**Decision:** option (a) as "queue outside the lane": a dock with one way out gets a lane (cells whose loss cuts it off, within 8 cells);
waiters stand at least 2 cells beyond its outer end and claim the dock only while the lane is empty. Open docks are unchanged (no hash moved).
**Status:** owner decision (session 22, "go ahead and fix this")

### D-226 · 2026-11-12 · Session 22 audio: music, voices, sound effects
**Decision:** (1) Music: take 1 of all 20 ACE-Step tracks ships now (tools/audio/export_music.py, -16 LUFS, victory/defeat -14), any track
can be swapped. (2) Voice: Kokoro-82M (Apache-2.0) on the owner's laptop, one preset voice per leader chosen by audition (tools/voice/
leaders.json), every radio line voiced (164 slots); output released CC0 (owner confirmed, Q-78). (3) SFX: about 80 sounds generated on the
owner's laptop with Stable Audio Open 1.0 (Stability AI Community License, free below USD 1M revenue, outputs ours; weights never in the
repo); CREDITS: "Generated with Stable Audio Open 1.0 (Stability AI Community License); outputs owned by the project, shipped under the
game's GPL v3" — no CC0 claim. One-shots 22 kHz mono, ambience loops stereo; data/audio WAVs and data/music OGGs go through Git LFS.
(4) Audio code: sounds.toml keys by faction, unit class and weapon class with fallback to today's events; water, sonar, flyby/hover, rig
events; biome ambience loops with an Ambience slider (view only, no sim change). (5) Every announced campaign wave gets a radio line
(M13-21). **Status:** owner decisions (session 22)

### D-227 · 2026-11-12 · Wall and gate heroes and models (M14-03)
**Decision:** wall/gate pieces (auth_wall, auth_gate, dir_bunker, lb_scrap_wall) are turned -35 degrees for their booklet heroes with half the
sky light (render_heroes.py STRUCTURE_YAW, TURNED_WORLD_STRENGTH), and their models got a detail pass (panels, seams, grime, props) that also
changes the game sprites; rendered on the owner's laptop (tools/art/render_s22_walls.ps1). **Status:** owner decisions ("lighting + detail",
"bolder, then fan out"; renders approved)

### D-228 · 2026-11-12 · Session 22 sign-offs
**Decision:** the owner approved the Directorate lore draft (design 19 §6b, M14-04) and confirmed Q-78: Kokoro output CC0; the mop-up
constants stand until the laptop on/off series; the Harpoon Wagon counts as anti-air in the AI mix; ship-ready horn and sub sonar stay.
**Status:** owner decision (session 22, "signoffs look great")

### D-229 · 2026-11-13 · Session 23: Rocket Truck splash back to 1; PR #76 landed
**Decision:** after the owner's s22 laptop series (balance_runs\s22after), `dir_rocket` splash goes back 0 -> 1 (supersedes that part of D-223).
Shipments 1400, repair auras 5 and the other D-223 items stay. Left Behind v Expedition stays open as M13-22 (no data lever found that fixes it
without undoing the Authority fix; docs/balance/s23_probes.md). PR #76 (session 22) merged by the lead on the owner's word ("just land it now").
**Why:** cloud probes with splash 1: Dir v LB 5-8 and 7-6 (laptop 13-49), Auth v Dir 6-7 (laptop 34-19). **Alternatives:** truck damage 75 and
range 6 cells (Dir v LB 1-14, and the truck would leave the AI's artillery class); shipments 1700 or floor 450 (no help for LB v Exp, hurts Auth v Exp).
**Status:** owner decision ("apply my recommended fixes straight away and let the re-run confirm them"); confirm with the laptop re-run.

### D-230 · 2026-11-13 · Left Behind tier-3 swarm cap until tick 4800 (M13-22)
**Decision:** `[faction.left_behind.tier3] army_cap_until_tick` 3600 -> 4800 (D-186's early-swarm cap, AI tier 3 only; no unit, weapon or cost
numbers change). **Why:** the new `ai match --ledger` shows Left Behind beating the Expedition with a 60-credit infantry swarm (2253 Scavengers in
16 games) while the Expedition's AI fields no splash unit at tier 3; probes (docs/balance/s23_probes.md round 2): LB v Exp 11-2 -> 10-5, Exp v LB
Glass Coast 4-10 -> 8-3, Auth v LB 9-6, Dir v LB 8-5. **Alternatives:** cap until 6000 (overshot: Auth v LB 12-4); Scavenger cost 80 (Auth v LB
12-2). **Status:** owner asked for fixes applied straight away; confirm with the laptop series (s23after).

### D-231 · 2026-11-13 · Left Behind and Expedition lore approved (M14-04)
**Decision:** design 19 §6c and §6d approved "as written as long as it proves to be internally consistent". An independent fact-check against
docs/lore, data/archive and the mission briefings found no hard break in §6c and one in §6d; fixes applied: fleet telexes open with RETURN WITH THE
TIDE and the call-sign, the one-thing / TELEX ENDS rule is scoped to field telexes, "Fold what you can; build the rest cheap enough to leave", "a rig
cut loose is a debt", the Tow Truck hauls a packed rig in, the Anchorage is "the fleet's one line home", the paint tin lives at the Hover Yard, and
a spoiler-adjacent clause about hulls coming home was cut. **Status:** owner decision (session 23).

### D-232 · 2026-11-13 · CI: rolling nightly pre-release instead of game artifacts
**Decision:** CI no longer uploads the game folder or the zip/installer as artifacts (they filled the Actions storage quota). A push to main
replaces the `nightly` pre-release (zip + installer, created by the workflow's own token); pull requests upload no game; versioned releases
still come from release.yml on v* tags. **Status:** owner decision (session 23, "add it to this batch").

### D-233 · 2026-11-13 · Mop-up trigger loosened (M13-20, s22mopup)
**Decision:** MopupPlan FromTick 9000 -> 6000, RatioPct 400 -> 250, HoldTicks 1200 -> 600, and a new MinArmyNoEnemy = 4: when no enemy army was
seen in the long window, four units are enough (the 8-unit / 3000-credit minimums still apply while the enemy has an army). **Why:** the owner's
s22mopup series (balance_runs\s22mopup, 9 rows x 64 games on/off) showed no draw reduction and no change in end ticks; the trigger almost never
fired. Owner picked "loosen and re-measure". **Finding:** cloud probes (Directorate 3v3 and 4v3, 32 games on/off) still change little: the
draws traced are attrition stalemates (both sides back at tier 1, the attacker's last 4-5 units die against the defended last building, no
income to rebuild), which a mop-up cannot break. **Status:** kept on (no harm to 4v3: 40 % / 40 %); the next lever for draws is the late-game
economy and rebuild behaviour, not the mop-up. Measured again in the laptop s23after run.

### D-234 · 2026-11-13 · Owner answers Q-79..Q-83 (session 23, round 4)
**Decision:** (1) Q-79: late-game draws are diagnosed first (income and rebuild after tick 20 000, M13-23); no rule change until the findings.
(2) Q-80: the Expedition gets a new tier-2 ground anti-infantry unit, a hover canister skiff (M13-25): data and AI use first with a borrowed
sprite and starting stats tagged "(tune) s23 Q-80"; name, art and lore wait on Q-84. Rejected: Barrage Lander to tier 2 (data only), a grenadier
marine, Gunships at AI tier 3. (3) Q-81: the full Field Manual build (M14-05) waits until balance settles. (4) Q-82: raise campaign tier 5 now in
the missions where the bot wins 12/12 (nine in the s21sweep laptop run: auth_02, auth_04, dir_01-04, lb_04, exp_02, exp_04), target band 4-9 of
12 bot wins, tier-5 data only, no visible timer cuts (M13-24); all-or-nothing missions that cannot reach the band stay as they are and are
reported. (5) Q-83: the tutorial instructor gets a Kokoro voice picked by audition on the laptop; unit barks after the SFX picks.
**Why:** owner's choices (session 23: "1- a 2- c 3- wait on balance 4- raise now 5- sounds good"; Q-80 shape and the Q-82 band by question).
**Status:** owner decision.

### D-235 · 2026-11-13 · Late-game draws: AI endgame fixes first (M13-23)
**Decision:** the draws are mined-out maps, not a rebuild failure (docs/balance/s23_draws.md). Fix them in the AI only: (A) spend down when
Broke (dearest affordable unit, cancel stalled lanes for the refund); (B3) never sell a hatch/tunnel while units are entering it; (B2) a wave
standing 1 500+ ticks within 8 cells of a known enemy structure attacks the nearest one; (B1) MopupDue uses MinArmyNoEnemy when no enemy army
was seen. Re-measure the Authority mirror, Directorate mirror and Authority v Directorate. **Alternatives:** ore regrowth or richer fields
(sim rule; later, only if A+B fall short); adjudicating at the time limit (fallback); leave as is. **Status:** owner decision (session 23).

### D-236 · 2026-11-13 · The canister skiff is the Grapeshot (Q-84, M13-25)
**Decision:** the Expedition's tier-2 hover canister skiff is named **Grapeshot** (id stays `exp_canister_skiff`). Archive text is a plain
function line until the Field Manual lore pass. Art pilot approved now: one render and sprite sheet, contact sheet to the owner before commit,
pushed by the owner with an LFS bundle (D-084). Cloud smoke (16 games/cell, tier 3): Exp v LB Rust Belt 4-10 -> 10-5, LB v Exp Glass Coast
3-10 -> 2-12, Auth v Exp 7-9 -> 7-9, Dir v Exp 14-2 -> 10-5; to confirm on the laptop. **Status:** owner decision (session 23).

### D-237 · 2026-11-13 · Grapeshot gets the Glide Tank's upgrades
**Decision:** `exp_canister_skiff` joins the same Expedition upgrades as the Glide Tank: composite hulls (max health x1.15), vacuum cores
(canister gun rate of fire x0.85) and veteran drill (veterancy threshold x0.8). **Status:** owner decision (session 23, "same upgrades").

### D-238 · 2026-11-13 · Campaign tier 5 raised in six missions; lb_04, exp_02, exp_04 stay at 12/12 (M13-24)
**Decision:** tier-5 data only (D-234 point 4, band 4-9 of 12 bot wins), 12-seed sweeps in the cloud: auth_02 8/12 (bigger, earlier hidden
raids), auth_04 6/12 (device guard and home raids larger, raid 1 at 2200), dir_01 7/12 (waves of 10 riflemen), dir_02 6/12 (escort jeeps 5,
reserve 10), dir_03 5/12 (relief column 11 riflemen), dir_04 5/12 (waves 11/11/12 riflemen, visible timers unchanged). Tier 3 stays 12/12.
The tier-5 fixed-seed walks of auth_04, dir_02 and dir_03 now assert a clean run only (D-221). lb_04 and exp_02 are all-or-nothing for the bot
(12/12 at one unit less, 0/6 at one unit more); the owner chose to leave them, and exp_04 (not attempted), at 12/12.
**Note:** the band is narrow and the sweeps are steep (auth_02: 5/6 -> 0/6 on ~15 % more units); later sim or bot changes will move these.
**Status:** owner decision (session 23); laptop campaign sweep to confirm.

### D-239 · 2026-11-13 · Rocket Truck cost 800 -> 950 (M13-26)
**Decision:** `dir_rocket_truck` cost 800 -> 950 (splash stays 1, damage stays 90). **Why:** s23after showed the Directorate too strong after
D-229 (Auth v Dir 16-37, Dir v Exp 55-8); cloud probes (docs/balance/s23_directorate.md): cost 950 moved Auth v Dir 3-9 -> 7-8 and Dir v Exp
10-5 -> 6-6 with Dir v LB 7-5 -> 6-4. **Alternatives:** damage 90 -> 75 (Auth v Dir only); both (overshoots Dir v LB 2-11); Conscript cost
(made it worse); leave the truck and fix AI responses. **Status:** owner decision (session 23); confirm with the laptop series s23d.

### D-240 · 2026-11-13 · Grapeshot field note approved; par times wait for play
**Decision:** the Grapeshot archive field note "GRAPESHOT 6 // SWEEP THE INFANTRY AT CLOSE RANGE. LEAVE THE TANKS ALONE. TELEX ENDS." is
final (no longer a draft). Q-73 (par-time medals) stays open but deferred: the owner sets par times after he starts playing the campaign.
**Why:** owner, session 24. **Alternatives:** reword the note; derive par from bot times now (Q-73 b). **Status:** owner decision (session 24).

### D-241 · 2026-11-13 · Stronger commander directives: all four ideas (M5-12)
**Decision:** build timed orders (attack cell X at tick N with at least K army value; hold cell X), counter-building (mixes anti-armour and
anti-infantry, plus a "strong" flag that makes production follow the mix), a separate raid group (size, target cell), and economy calls
(expand to cell X, tech to tier N, keep a credit reserve) with a richer summary back to the model (recent fight results). Orders are a new
command `CommanderOrder = 28` with four standing slots per player in a "commander_orders" state part (src/Sim/Core/CommanderOrder.cs); like
directives they are AI inputs only (D-110), no cheats (D-061), all four factions, plain tier when unset. A random-directive control joins the
32-game swap test so a gain is the model's. **Why:** owner, session 24 (design note 15). **Alternatives:** two of the four (recommended:
timed orders and counter-building); leave as is. **Status:** owner decision (session 24).

### D-242 · 2026-11-13 · Remaining draws: faster late launch plus a scored column, no rule change
**Decision:** the AI launches its last waves sooner when both sides are broke (AI only), and `ai match` reports an adjudicated result for games
that hit the time limit (army + structure value + credits) as a report column only; the game rules (D-173) do not change. Built now, merged
after the s23d laptop series is read. **Why:** owner, session 24 (s23_draws.md option D). **Alternatives:** faster launch only; a sim
adjudication rule. **Status:** owner decision (session 24).

### D-243 · 2026-11-13 · Art pilots render on the owner's laptop; Authority UI skin and both yards first
**Decision:** session 24 art (building depth pipeline, faction UI skin, portraits, death frames) is written as Blender scripts in the cloud and
rendered on the owner's laptop ("it comes out much better"); the cloud only smoke-tests a script at low samples. Pilots: the Authority UI skin;
building depth on auth_conyard and dir_shaft side by side with today's sheets; portraits and death frames on one unit per pilot. Fan-out waits
for the owner's approval of each pilot. **Why:** owner, session 24. **Status:** owner decision (session 24).

### D-244 · 2026-11-14 · Grapeshot canister gun damage 14 -> 11
**Decision:** `exp_canister_gun` damage 14 -> 11 (cost, range, rate of fire and the AI trigger unchanged). **Why:** the s23d laptop series
(docs/balance/s23d_read.md) shows Grapeshot overshot: Expedition v Left Behind 50-5 on Glass Coast (was 35-13) and Left Behind v Expedition
21-39 on Rust Belt (was 40-18); Expedition v Authority on Glass Coast 43-20 (was 34-27). **Alternatives:** probe three variants first (cost 850,
damage 11, AI cap of 2 skiffs); cost 850; buff Left Behind. **Status:** owner decision (session 24); confirm with the next laptop series.

### D-245 · 2026-11-14 · Session 24 art pilots approved
**Decision:** the owner approved the second laptop renders: the building depth pass (pilot v2 on auth_conyard and dir_shaft; next a full
re-render of every building with `--depth`), the Authority UI skin pilot v2 (install with render_s24_skin.ps1 -Install; the other three
factions follow the same kit), the four portraits and the die_blast / die_burn / die_rad death frames (fan-out with
render_s24_portraits.ps1 -All). New art is committed from the owner's machine (LFS). **Why:** owner, session 24 ("love it all").
**Status:** owner decision (session 24).

### D-246 · 2026-11-14 · Expedition shipment value 1400 -> 1200
**Decision:** `[shipments] value` 1400 -> 1200 (data/rules/game.toml). **Why:** on Glass Coast the ore runs out by tick ~7500; land factions stall
while shipments keep paying (s24after: Expedition 66-91 % there). 1000 overshot Rust Belt (Expedition 39 %). More ore on Glass Coast was tried and
rejected (helps the Directorate most). **Status:** owner said "just fix it and test" (session 25); docs/balance/s25_balance.md; laptop series to confirm.

### D-247 · 2026-11-14 · Left Behind tier-3 army cap until tick 3600 (supersedes the 4800 of D-230)
**Decision:** `[faction.left_behind.tier3] army_cap_until_tick` 4800 -> 3600 (data/ai/difficulty.toml; 4500 was tried first). **Why:** under the
cap the Left Behind hoarded 2-5k credits and lost the mid-game (Authority v Left Behind 72 %); Grapeshot (D-236) now answers the early swarm that
D-230 guarded against. Cloud after: Auth v LB 56 %, Dir v LB 55 %, LB v Exp rust 53 %, Exp v LB glass 64 %. **Status:** session 25, fix-and-test.

### D-248 · 2026-11-14 · AI late all-in: no retreats from tick 16000
**Decision:** `RetreatManager.LateAllInTick = 16000` (tune): from that tick the AI stops retreating and returns home-guard and repairing units to
the wave pool (scouts and raiders keep their duty). AI only. **Why:** Expedition mirrors drew because hurt marines (never healed) sat in repair for
10 000+ ticks and waves launched with 4-6 units; Expedition mirror draws 19 -> 13 of 64, the control row did not move. **Status:** session 25.

### D-249 · 2026-11-14 · AI random streams: per-slot seed mixed through the splitmix finaliser
**Decision:** `AiRandom` seeds from `Mix(gameSeed) ^ Mix(C * (slot + 1))` instead of `gameSeed ^ gamma * (slot + 1)`. **Why:** the old scheme left slot 1
one gamma step behind slot 0 for a quarter of even game seeds, so both AIs drew the same sequence shifted by one draw; identical mirrors skewed by
seed parity (the "Left Behind slot bias", M13-27). Every AI game reshuffles; earlier per-seed numbers are not comparable (D-220). **Status:** session 25.

### D-250 · 2026-11-14 · Authority refinery cost 2000 -> 1700
**Decision:** `auth_refinery` cost 2000 -> 1700 (parity with the Directorate Smelter Vent). **Why:** the Authority fielded far fewer units before the
first fights (army value 6.6k v the Directorate's 10.6k at tick 5000 on Glass Coast); Expedition v Authority on Glass Coast 68 % -> 53 % (64 games).
Scraper 600 as well overshot Authority v Directorate. **Status:** session 25; Authority v Directorate rust 40 % is the edge row to watch.

### D-251 · 2026-11-14 · Session 25 sound picks; over-cap one-shots are trimmed at pick time
**Decision:** the owner accepted Claude's 65 recommended takes (tools/audio/sfx/picks_s25.toml). `make_sfx.py --pick` now cuts a one-shot over the
270 000-byte cap to fit with a 250 ms fade-out. die_ship (8 s) is left out until it is re-picked with the trim. Tutorial instructor voice am_adam.
**Status:** owner decision (session 25).

### D-252 · 2026-11-15 · Faction UI skins for Directorate, Left Behind and Expedition approved and installed
**Decision:** the owner approved the three pilots ("love the art pilots, send it"): Directorate dark board-form concrete with oxblood enamel and a
loudspeaker emblem (dark panels instead of bone-white so the light text reads), Left Behind faded teal sheet metal with ochre road-sign buttons
and dark text, Expedition navy enamel with chrome bezels and an anchor-and-compass roundel. Final 128-sample renders made on the Pi k3s cluster
with Debian Blender 4.3 (matched Blender 5.3 to <1/255 on the Authority skin). A settings screen line toggles `ui_skin` (Faction art / Classic).
**Status:** owner decision (session 27), landed in PR #90.

### D-253 · 2026-11-15 · CPU batch work and Blender renders can run on the Pi cluster
**Decision:** the owner asked to offload what can go to the cluster: AI series, campaign sweeps and Blender CPU renders now run there
(k3s-cluster-code `claude/examples/rts27/`); results are seed-for-seed identical to x64. Laptop stays for GPU work (SFX generation) and Blender
5.x-only features. Supersedes the "long measurement runs go to the owner's laptop" line of D-220/S21 where the cluster is available.
**Status:** owner decision (session 27).

### D-254 · 2026-11-15 · Expedition Skimmer 360 hp, shipment value 1100 (session 27 balance round)
**Decision:** owner chose the recommended variant VE: `exp_skimmer` max_health 220 -> 360 (Skimmers were lost ~12 per game against the Directorate
on rust_belt, 1-3 for other factions' harvesters) and the Expedition shipment value 1200 -> 1100 to offset the glass_coast gain. 64-game rows:
sum of distances from 50 % over the nine cross-faction rows 109 -> 87 (table: docs/balance/s27_balance.md). Alternatives measured and dropped:
Skimmer capacity 8 (worse everywhere), Left Behind cap until 3000, Directorate harass 22 (no effect at tier 3), Directorate tier-3 cap 22/26.
**Status:** owner decision (session 27). Open: Directorate v Expedition rust 73 %, Authority v Left Behind 66 %, Authority v Directorate 38 %.

### D-255 · 2026-11-16 · Session 28 balance round: no change lands; keep-or-drop on 128 games per row
**Decision:** the owner let Claude land balance fixes without asking. Fifteen variants were screened on the Pi cluster (64 games per row) and the
three best re-measured with main on 128 games (seeds 1-64). None beat main (sum of distances from 50 % over the nine rows: main 87, conscript 95 +
Directorate tier-3 attack_ratio_pct 130 92, Authority tier-3 harvester_target 2 88, both 88), so nothing was landed. 64-game cells moved up to
7 points on rows a variant could not touch: from now on a balance change is kept only on 128 games per row against a 128-game main table.
Table and diagnosis: docs/balance/s28_balance.md. Open at 128 games: Authority v Left Behind 70 %, Directorate v Expedition rust 70 %,
Expedition v Left Behind glass 67 %. Per-faction tier-3 `attack_ratio_pct` has no effect on the Expedition (identical games).
**Status:** Claude decision under the owner's standing go-ahead (session 28). Supersedes the 64-game rule of the session 25 lessons.

### D-256 · 2026-11-16 · Session 28 sound picks
**Decision:** Claude screened the s24 catch-up takes (attack, decay, active share, loop seam, stationarity) and picked 16 sounds; the owner ran
scratch\s28_sfx.ps1 with the recommendations and pushed (PR #92): wpn_flame 2, wpn_depth_charge 2, exp_bomb_heavy 6, exp_rad_burst 5,
die_inf_lb 4, die_veh_auth 6, die_veh_dir 4, die_veh_exp 6, die_air_crash 6, gui_deny 2, amb_glass_coast 1, amb_rust_belt 1, amb_ash_plains 2,
amb_mountains 1, amb_open_sea 1, die_ship 2 (trimmed to the one-shot cap). Recorded in tools/audio/sfx/picks_s28.toml. All 80 jobs now have a sound.
**Status:** owner decision (session 28).

### D-257 · 2026-11-16 · Session 29 owner answers (balance landing rule, M5-13, GitHub access, props)
**Decision:** (1) An AI src balance change lands when its target row moves toward 50 % over 128 games (seeds 1-64) and no other cross-faction row
moves more than 3 points away from 50 %. (2) M5-13: the random-directive control runs now; the model run waits until the owner sets his API key on
the laptop himself (never in chat). (3) The repo was attached to the session with push access at the owner's request. (4) Prop art: Claude picks the
props by judgement; one pilot reviewed by the lead, then the fan-out without another owner gate.
**Status:** owner decision (session 29 briefing).

### D-258 · 2026-11-16 · Blender prop atlas for the four procedural-only props (M12-15, M10-07)
**Decision:** GlassCrest, IceSlab, WreckHulk and BrokenPylon get Blender art (tools/art/props/build_props.py, bpy primitives only, no third-party
art): 3 variants each, one 192x256 atlas per theater in data/sprites/props/blender/ (same manifest format as the 0 A.D. atlas), loaded by
BlenderPropAtlas ahead of the 0 A.D. and LPC atlases; PropArt stays as the fallback. Wrecks are generic and faction-less (rust, char, bare steel)
so they never read as live units, are darker than the ground by design (readable by outline and a lighter top band), and get theater weather on
upward faces only. Lead reviewed three rounds (olive camouflage removed, weather limited, pylon rebuilt as a tapered lattice stub).
**Status:** decided by Claude under D-257 (4).

### D-259 · 2026-11-16 · Data maps rebuilt from stamp plans (M12-14)
**Decision:** rustbelt_m2, glass_coast_e1 and ash_reckoning are now base map + stamp plan (`map stamp`, tools/maps/base and tools/maps/plans;
MapStampPlanTests keeps data/maps equal to base + plan). Ids, sizes, starts, fields and mission cells are kept; plans stay off mission routes and
the cells the bot walk-throughs use, so every mission tier still wins. rustbelt_m2 ended up the lightest: a fuller showcase (checkpoint on the north
road, ruined blocks on the roads) flips the auth_01, lb_01 and dir_01 tier-5 bot walks (Q-85).
**Status:** decided by Claude (design 16 wave 4).

### D-260 · 2026-11-16 · M5-13 random-directive control result
**Decision:** On 128 games (seeds 1-64, both starts, map 64, tier 3 v tier 3) random directives and orders win 25-94-9 (21 %) against the plain AI,
whose own baseline is 58-51-19 (53 %). The M5-12 levers are strong enough to lose games on their own; a model run is read against both numbers
(docs/balance/s29_commander_control.md).
**Status:** measurement (session 29).

### D-261 · 2026-11-16 · Session 29 balance: no change lands
**Decision:** none of the 16 screened variants (6 Authority build/tech, 2 Left Behind adapt, 3 Expedition siege-commit, 2 Expedition anti-air,
3 anti-air + lower shipments; a5 identical to a1) meets the D-257 landing rule on the 64-game screen, so none went to 128 games and main stays as is.
The AI keys stay in the code, unset. `aa_needs_air` fixes real AI waste but makes the Expedition the strongest faction; it needs an Expedition
strength pass before it can land (docs/balance/s29_balance.md).
**Status:** measurement (session 29), under D-257.

### D-262 · 2026-11-16 · Session 30 owner answers (push access, Q-85, M5-13)
**Decision:** (1) the repo was attached with push access at the owner's request; (2) Q-85 (b): rustbelt_m2 gets the full showcase dressing
(north-road checkpoint, ruined blocks, grove, props) and the tier-5 bot walks of the missions on it are re-walked and retuned: auth_01 tier-5 raid 1
is 4 riflemen (was 5), lb_01 tier-5 second relief 3 (was 4); every mission on the map (auth_01, auth_02, dir_01, dir_02, lb_01, lb_02) meets the
D-222 sweep targets and the fixed-seed walks pass; (3) the M5-13 model run is skipped this session (no API key set). Supersedes D-259's "rustbelt_m2
ended up the lightest". **Why:** owner answers at the session 30 briefing. **Status:** decided (owner), landed in PR #97.

### D-263 · 2026-11-16 · Session 30 balance: no change lands; three new AI keys stay unset
**Decision:** none of 16 screened variants (Sparker caps 1/3/5/8, cap + mop-up hunt 6/8/10, retreat floor 25 alone or with the anti-air keys, Glide Tank
cost/health, two Left Behind tier-3 cap variants, mop-up hunt alone) meets the D-257 landing rule on the 64-game screen, so main stays as is. New keys,
all unset: `aa_cap_before_air` (replaces the session's first `aa_needs_air` = 2 soft mode before it landed), `retreat_floor_pct`, `mopup_hunt`.
**Why:** every Expedition-wide strengthening that helps Directorate v Expedition rust moves Authority v Expedition and the Expedition glass rows more
than 3 points the wrong way; the Left Behind levers pull Directorate v Left Behind down 11-19 points (docs/balance/s30_balance.md).
**Alternatives:** land cap 5 (Dir v Exp 50 %) and accept Auth v Exp 28 % (breaks D-257); Directorate-side or siege levers next session.
**Status:** measurement (session 30), under D-257.


### D-264 · 2026-11-16 · Session 31 owner answers (landing rule, map work, destructible props)
**Decision:** (1) Supersedes D-257 (1): an AI balance change lands when its target row moves toward 50 % over 128 games (seeds 1-64) and no other
cross-faction row moves 5 points or more away from 50 % (was 3). (2) Map work this session: full showcase dressing of glass_coast_e1 and ash_reckoning
(missions on them re-walked and retuned, as D-262 did for rustbelt_m2), real prop and decal sprites in the map editor view, and M12-16 destructible
props. (3) M12-16 rules (design 16 D1 option C): tiered. Heavy ground vehicles crush Tree and Snag by driving over them; splash damage destroys Tree,
Snag and Wreck (Wreck tough); Block and Pylon never break. A destroyed prop leaves Rough ground. AI: crushers path through crushable props at a small
cost; the AI never targets props on purpose. Applies everywhere (skirmish, LAN, campaign); missions on the three data maps are re-walked after it lands.
(4) exp_rad_burst plays for detonations whose warhead lays radiation >= 80 (rad shell, Meltdown, both superweapon blasts) as
`explosion_small.radiation` / `explosion_large.radiation`; detonation fx events now carry the warhead index.
**Why:** owner answers at the session 31 briefing; (4) owner asked to wire the sound, threshold ASSUMED by Claude so the Ash Sower spray (60) stays plain.
**Status:** decided (owner); (4) threshold assumed — confirm.

### D-265 · 2026-11-17 · Session 31 balance: Directorate tier-3 attack ratio 140 lands
**Decision:** `[faction.directorate.tier3] attack_ratio_pct = 140` (shared veteran table 110). On 128 games with M12-16 in: Dir v Exp 70 -> 62 %,
Auth v Dir 45 -> 48 %, Exp v Dir glass 58 -> 54 %, Dir v LB 47 -> 48 %; rows without the Directorate unchanged; distance from 50 89 -> 73
(docs/balance/s31_balance.md). **Why:** meets D-264 (every touched row moves toward 50 %). **Alternatives:** army caps (d2/d3), unit reserve (d1),
attack threshold (d5), Sparker caps (k10, kd) all failed D-264 on the screen. **Status:** measurement under D-264 (owner rule), campaign sweep
re-run with it.

### D-266 · 2026-11-17 · Session 32 balance: Expedition Sparker cap 10 on top of D-265
**Decision:** `[faction.expedition] aa_cap_before_air = 10`: until it has seen an enemy aircraft, the Expedition skirmish AI keeps at most 10 vehicles
whose gun only hits aircraft (Sparkers). **Why:** 128 games per row on main with M12-16 and D-265: Directorate v Expedition rust 62.1 -> 58.0 %,
Left Behind v Expedition 53.5 -> 48.7 %, Authority v Expedition 41.9 -> 37.6 % (4.3 points away, under D-264's 5), glass rows within 1 point; campaign
sweep 45/45 meets D-222 (docs/balance/s32_balance.md). **Alternatives:** cap 9 (Auth v Exp 6.5 points away: fails D-264); cap 8 + Directorate army
cap (s31 kd: Auth v Exp 7.7 away). **Status:** lands in the session 32 PR (D-264 owner rule).

### D-267 · 2026-11-17 · Session 32: no economic relocation for the Left Behind AI
**Decision:** the "move the Mother Rig to the next ore field when the home field is mined out" rule does not land; its code stays on the
measurement branch `integ-s32b`. **Why:** all four settings screened (64 games per Left Behind row) weaken the Left Behind by 6-25 points on every
row (docs/balance/s32_balance.md). **Alternatives:** next session, harvester protection or replacement, an earlier second refinery toward the next
field, or map-side ore near the Left Behind's corner. **Status:** measurement (session 32).

### D-268 · 2026-11-17 · Session 32: no Left Behind harvester-target change
**Decision:** `[faction.left_behind.tier3] harvester_target` stays unset (shared target). **Why:** 2, 3 and 4 screened on the four Left Behind rows
(64 games): no setting moves Authority v Left Behind toward 50 % without another row moving 5+ points away (docs/balance/s32_balance.md).
**Alternatives:** an escort/route rule for Left Behind harvesters (code), or accept Authority v Left Behind ~66-70 % as faction asymmetry for now.
**Status:** measurement (session 32).

### D-269 · 2026-11-17 · Session 32: no Left Behind harvester shepherd; Authority v Left Behind accepted for now
**Decision:** the harvester shepherd (safe own-half fields) does not land; its code stays on `integ-s32c`. Authority v Left Behind (~66-70 %) is
accepted as it is for now (owner, session 32: "if not then we will live with Authority v Left Behind at about 66-70 % for now").
**Why:** 128 games: Auth v LB 69.5 -> 68.1 % (s1) / 68.6 % (s2), Dir v LB 47.8 -> 41.8 / 42.2 % (5.6-6 points away, fails D-264)
(docs/balance/s32_balance.md). Together with D-267 and D-268 the three Left Behind economy levers all failed. **Alternatives:** none queued.
**Status:** decided (owner).
### D-270 · 2026-11-17 · Session 32: AI rally before the first yard; start-corner bias probe
**Decision:** the skirmish AI rallies at its start cell until its first yard stands (it rallied to the unset (0,0), the top-left corner, while
a Left Behind Mother Rig was packed or an Expedition Command Ship undeployed). New diagnostic `Tools ai mirror`. **Why:** the bug gave the
top-left start a head start in Left Behind and Expedition games; with the fix the Left Behind mirror's top-left share falls 70 -> 58.5 % and
Authority v Left Behind moves 69.5 -> 64.7 %, no row moves away from 50 % by more than 0.8, campaign 45/45 (docs/balance/s32_balance.md).
The remaining bias (about 57 % top-left) is mostly inherent: all buildings face south. **Alternatives:** a mirror-safe start-cell rounding
(dropped: no probe effect, moved Dir v Exp 6 points). **Status:** lands in the session 32 PR (owner: "Land the rally bug fix and remeasure").

### D-271 · 2026-11-17 · Field Manual approved as drafted (Q-86)
**Decision:** the full Field Manual draft is approved unchanged: 144 pages (not the planned 108), Order Seven stays "Classified upgrade" and
the cover keeps the Sunrise Device obelisk, the supply-cap and veterancy pages follow design 13/14 as written, and the 36 pages of new text
are approved (draft stamps removed). The PDF and the 2-page quick-reference card go in docs/field_manual. **Why:** owner, session 33 ("love the
draft"). **Alternatives:** cut to ~108 pages with smaller type and no field notes; hide the obelisk. **Status:** owner decision

### D-272 · 2026-11-17 · M10-06 Warzone tracks dropped after the pilot
**Decision:** the Warzone 2100 tracks are not rolled out. On the pilot (auth_howitzer, dir_mole) the Warzone tread read at game size as a darker,
smoother belt and lost the road wheels the procedural tracks show. The shared helper tools/art/wz/wz_tracks.py stays for later use; no sprite
changes. **Why:** owner, session 33 (picked "drop it, keep old tracks"). **Alternatives:** roll out to all ~17 tracked vehicles; a heavier variant.
**Status:** owner decision

### D-273 · 2026-11-17 · Field Manual look pass: no pink, volumetric smoke, polished and detailed hero renders
**Decision:** (1) booklet-only colour grade (tools/booklet/full/grade.py): pink and magenta dusk casts in the painted stills become warm ash amber;
the game's images are unchanged. (2) Hero renders get a hero-only polish step (smooth by angle, bevels, weighted normals, finer curved
primitives) and a detail step (bevel-node edge wear, AO grime, colour and roughness variation, panel seams, rivets, hatches), rendered on the
cluster node huntsman at 1200 px; the game sprites are unchanged. (3) Booklet house accents: Expedition signal orange #D0702A (was mauve),
Left Behind teal #3F7C7A (was violet). (4) The stills' smoke columns and dust puffs become volumetric smoke (still_kit smoke_volume).
**Why:** owner, session 33 ("I don't like all the pink", "the smoke looks terrible", "sprites look really low poly"; picked polish and detail,
teal). **Alternatives:** higher resolution only (does not add geometry); smaller pictures. **Status:** owner decision

### D-274 · 2026-11-18 · M10-06b Warzone kitbash closed without rollout
**Decision:** no Warzone 2100 parts are kitbashed into our vehicles. Pilot (session 34) on auth_bulwark: B swapped our cannon for the Warzone
hyper-velocity cannon (gnhvcan.pie), C added a Warzone heavy MG (gnmmg1.pie) on the cupola. Warzone weapon models are very low-poly (the heavy
cannon is 8 points) and carry their detail in page-17-droid-weapons.png, which is CC BY-SA 3.0 and unusable (D-189); painted flat they read as
plain tubes and lose the sleeve and muzzle brake our procedural cannon has. Our own geometry stays; tools/art/wz stays for reference. Candidate
modules on branch wp/s34-kitbash (not merged). **Why:** owner, session 34 ("close m10-06b"), after the before/after sheet.
**Alternatives:** roll out C; a cupola MG in our own style. **Status:** owner decision

### D-275 · 2026-11-19 · M10-25 Unit sheets rendered at 80 px per cell and drawn 1:1
**Decision:** every vehicle and infantry sheet is rendered at 80 px per map cell, the size the x1.25 unit draw scale (D-172) puts it on
screen, and carries the new optional manifest key `px_per_cell = 80`. The renderer converts sheet pixels with
`SpriteManifest.ArtScale` = 64 / px_per_cell wherever unit art becomes world pixels (layers, turret pivots, shadow offsets, infantry deaths,
health-bar frame tops, damage lift and anchors, menu scenes, How to Play), so an 80 px sheet lands 1:1 exactly where the 64 px sheet was
stretched x1.25 with point sampling (every fourth pixel row and column doubled). Units keep their on-screen size; map, zoom levels,
terrain and buildings are unchanged (buildings were already 1:1). Pipeline: `common.UNIT_PX = 80`; build_vehicle.py frames =
round(CANVAS * 80 / 64) (module CANVAS stays in 64 px units); build_infantry.py renders the 1.25 figures at 1.25 x 80 / 64 (60 px frames,
figure_scale_permille unchanged); the Authority medium tank via `build_tank.py --sizes 480 --tag _v2` + `pixelize.py unit`. `--px-cell 64`
reproduces the old sheets. A test checks every unit manifest says 80.
**Why:** owner, session 35, after the before/after sheet (scraper: today's 64 px art at x1.25 v rendered at 80 px): "just do the full batch,
send it". A 96 px cell (more detail, but 1.5x larger units or less map on screen) and more Cycles samples (128 v 24 changed 3 % of pixels as
isolated palette flips, no visible gain) were measured first and set aside. **Alternatives:** 96 px cell with zoom/UI rework; drop the x1.25
unit scale; keep 64 px. **Status:** owner decision

### D-276 · 2026-11-19 · M10-26 Vehicles 32 facings, infantry 16 (changes D-032)
**Decision:** unit sheets carry 32 facings for vehicles (hull and turret layers, 11.25 deg steps) and 16 for infantry walk/idle/fire
(22.5 deg); one-facing states (deaths) are unchanged. This replaces the "16 facings for vehicles, 8 for infantry" part of D-032; the rest of
D-032 stands. No engine change: `Facing.ToFacings` already maps the sim's 256-step facing to 8, 16 or 32, the renderer reads the count per
state from each manifest, and SpriteSheet takes columns from the sheet width. Pipeline: `common.VEH_FACINGS = 32` / `INF_FACINGS = 16`;
build_vehicle.py maps a module's 16 to 32 and build_infantry.py its 8 to 16 (modules unchanged); rotation in 360 / facings steps; sheets
pack roughly square (`common.sheet_cols`, ceil(sqrt(n)) columns) so the largest sheet (dir_bore) is about 2.6k px a side rather than
800 x 8000; the medium tank via `build_tank.py --facings 32` + `pixelize.py unit`. Rendered on the cluster (s35b), pilot first
(auth_bulwark hull and turret, auth_rifleman).
**Why:** owner, session 35, "do all of this" on the list of compute upgrades that named smoother turning (32 / 16 facings). The new cluster
makes the doubled render cost (about 2x frames) an afternoon. **Alternatives:** keep 16 / 8; 32 for turrets only; 64 facings (sheet size and
little visible gain at 80 px). **Status:** owner decision

### D-277 · 2026-11-19 · Session 36: Ash Plains 2-player layout 1 uses mirror_x (left/right starts) (M13-31)
**Decision:** `[biome.ash_plains] symmetry_2p = "mirror_x"` (was mirror_y). Glass Coast stays rot2. Layout 2 and 4-player maps are unchanged.
**Why:** every building exits and docks to the south, so on a top/bottom map the top base's lanes face the enemy and the bottom base's face
the edge; start 0 won 67.2 % of 473 decided Ash Plains mirror games (z +7.5), with mirror_x 50.6 % (z +0.2); rot2 only halved it (57.6 %).
Glass Coast mirror_x also removes its milder lean (54.4 % -> 48.7 %) but moves Expedition v Authority on Glass Coast from 52.8 % to 19.8 %
(balance parked, D-269), so Glass Coast was left on rot2. docs/balance/s36_balance.md. **Alternatives:** rot2 for Ash Plains; buildings
that face the map centre (sim and art change); a placement rule in the AI (no effect on the exits themselves). **Status:** owner confirmed
(session 36, "confirm d277"; Ash Plains skirmish maps now start at the left and right edges).

### D-278 · 2026-11-19 · Session 36: commander directives that hold the army back wait for the AI's army (M5-14)
**Decision:** `DirectiveGuard` in the tiered AI: until the AI has its own `army_before_tech` army (Left Behind always counts as ready), or while
its army is 20 % behind the enemy army it has seen, the HoldCell, Reserve and TechTo orders and the unit-reserve and early-gate parts of a
focus are ignored for the decision; once they apply, HoldCell only moves the rally and no longer stops waves. Posture, mix, expand, target,
focus cell, AttackAt, Raid and ExpandTo always apply. New `ai commander --random-only <fields>` (paired ablation of the random commander).
**Why:** the s36 paired ablation (128 games per row) found exactly those parts cost the commanded AI its games (hold 34.8 %, reserve 32.8 %,
tech 41.0 %, focus 37.9 % v 57.4 % for plain-valued directives); with the guard the full random commander scores 53.1 % (was 23.0 %; plain AI
52.7 %), and games without a commander are unchanged. **Alternatives:** guard only while behind (G1: triggers rarely, no clear gain); hold as rally
only without the army gate (G2/G3: hold 44 %); dropping the orders altogether. **Status:** owner asked for the M5-14 follow-up in session 36
("do both"); the model rerun on the guarded build is running.

### D-279 · 2026-11-20 · Session 37: skirmish balance un-parked; measure every biome, tune rules and maps (supersedes D-269's "accepted for now")
**Decision:** skirmish balance is reopened (owner: "un-park skirmish balance and finally fix it ... tests various cost and effect changes on the
units and maps"). Target: every cross-faction row of layout 1 (6 pairs x 4 biomes, tier 3 v tier 3, size 96, 128 games) inside 40-60 %; mirror
start share inside 40-60 %; the campaign still meets D-222. Rule and map changes land when they move the table toward that target (D-264 still
guards each step) and are marked "(tune) s37 ... assumed — confirm". Plan and results: docs/balance/s37_balance_plan.md, s37_balance.md.
**Why:** owner request; the cluster makes 128-game rows on all four biomes cheap. **Alternatives:** AI keys only (s27-s32, exhausted).
**Status:** decided (owner); target values assumed — confirm.

### D-281 · 2026-11-20 · Session 37: map and AI fixes for mid-row starts and the Expedition supply lane (M13-33..M13-36)
**Decision:** (1) M13-34: one shared dock/exit orientation rule (`BuildingOrientation`) used by the sim and, through `PlayerView.OrientedOffset`,
by the AI's PlacementSearch and Directorate staging (the AI used unturned offsets for south-half buildings). (2) M13-33: new biome key
`start_row_shift` (default 0, mirror_x only); 6 for mountains and ash_plains, so 2-player layout-1 starts sit 6 cells above the middle row.
(3) M13-35: the carved channel to an inland Expedition base ends in a 7-wide landing bay (last 14 cells), and the Expedition AI parks the start
Landing Craft off the dock water until its Anchorage stands (dock fit uses the build radius). (4) M13-36: on ash_plains every river crossing is a
road deck (was alternating bridges and foot-only shallow fords). Rust Belt and Glass Coast maps stay byte-identical (pinned).
**Why:** Mountains/Ash Plains layout 1 starved the Left Behind (harvesters deadlocked at a walled-in dock) and the Expedition (tenders stranded
behind its own Quay/Shipyard; Anchorage site blocked): rows of 77-88 % on main; after the fixes incomes are level on every biome and those rows are
54-69 % (docs/balance/s37_balance.md). Ash fords split Left Behind infantry from its vehicles: Authority v LB on ash 79.5 -> 47 %.
**Alternatives:** unit stat changes for those factions (would break the other biomes); field placement margin (tried, no gain).
**Status:** decided by the lead under D-279; assumed — confirm (start shift and ash decks change how those maps look).

### D-282 · 2026-11-20 · Session 37: scraper orders (M13-32, owner play-test)
**Decision:** a right-click on an own refinery carries that refinery (Command.Target): a loaded harvester docks there; an empty one takes it as its
home refinery and starts harvesting at once (no empty trip). Any harvester left idle in Guard for 3 s resumes harvesting by itself (every player,
AI included; NoRefinery harvesters excepted). A Harvest click clears old unreachable-field marks; the order side needs an explored cell, as the
cursor does. **Why:** owner: scrapers "didn't seem to want to do it automatically if you right clicked to assign them to a refinery". AI skirmish
results unchanged (job bi37: Rust/Glass rows identical to main). **Status:** decided (owner request); 3 s assumed — confirm.

### D-283 · 2026-11-20 · Session 37: objective guidance (M11-30, owner play-test)
**Decision:** the collapsed objective bar lists every active objective (up to 4, optional ones marked), one highlighted row that the world marker,
edge arrow and minimap ping always follow (Tab moves it; a row with no location says "(no map marker)"); banners NEW OBJECTIVE (6 s),
OBJECTIVE COMPLETE (4 s) and OBJECTIVE FAILED, queued; minimap flash 4 s; an actor target keeps its last seen cell after the actor dies; six
objectives that had no location got one (dir_02, dir_03, exp_01..exp_04), and a test lists any objective without a SetTarget.
**Why:** owner: "it was really hard to know what the next objective was". **Status:** decided (owner request); wording assumed — confirm.

### D-284 · 2026-11-20 · Session 37: in-universe on-screen text (M11-31, owner play-test)
**Decision:** no raw ids on screen (debrief "New tech", spectator inspect, Archive weapon/Requires rows, upgrade and superweapon captions use
Archive titles); the 13 "[draft pending]" Technology entries written; developer wording removed (e.g. "Test connection", "not wired yet", "tick N"
-> m:ss); a scanner (tools/text/scan_text.py) and tests keep it that way (locale completeness, no '_' or dotted keys in player text). Telegram
briefings and STOP field notes are kept as they are pending the owner's choice (docs/reviews/2026-11-19_text_audit.md has both styles).
**Why:** owner: "the on screen text is mostly like development stuff ... make it in-universe consistent". **Status:** decided (owner request);
new Technology lore and the briefing style are the owner's to confirm.

### D-285 · 2026-11-20 · Session 37: LLM commander does not make the AI stronger; voice-line diagnostics (M5-14, M10-27)
**Decision:** M5-14 closes: on the guarded build qwen3:30b (14-13-5 over 32 games) and gpt-oss:120b (9-5-2 over 16) equal the random-directive
control (14-13-5; 10-5-1 on the same 16). A "super hard LLM opponent" needs a different design (owner options in HANDOFF). M10-27: the radio voice
path logs every voice start and every silent line with its reason; the voice could not be reproduced as silent on Linux (it plays through the full
game path into a recorded sink). **Why:** docs/balance/s37_balance.md; owner reported a silent voice line on an older build (eda0f0e3).
**Status:** decided by the lead; voice root cause open until the owner's next log.

### D-280 · 2026-11-20 · Session 37: unit package — Rocket Truck 970, Scavenger 54, Authority Tank health 460, Glide Tank 960 (M10-21)
**Decision:** dir_rocket_truck cost 950 -> 970, lb_scavenger cost 60 -> 54, auth_tank max_health 400 -> 460, exp_glide_tank cost 900 -> 960.
**Why:** 27-knob sensitivity screen plus five package screens at 128 games on all 24 cross rows (docs/balance/s37_balance.md): with the D-281 map
fixes the table goes from MAD 17.0 / 8 rows inside 40-60 % (main) to 13.3 / 11; the worst rows of main (82-88 %) are gone; campaign 45/45 meets
D-222. The Rocket Truck cost is a cliff (+10 % broke the Directorate), so only +2 % lands. **Alternatives:** Rocket Truck 985-1050 (Directorate
collapses on Rust/Glass), no Rocket Truck change (r950, same MAD, worse 35-65 count). **Status:** assumed — confirm (owner asked for the balance pass;
numbers are the owner's to confirm). The remaining outliers are map-specific (next steps in s37_balance.md).

### D-286 · 2026-11-21 · Session 38: "Super hard (LLM)" skirmish opponent = tier 5 + LLM commander + a +50 % edge (M5-15)
**Decision:** the skirmish setup gets a one-step "Super hard (LLM)" preset for an AI slot: tier 5, LLM commander on, handicap 150 % (income and build
speed; the existing per-slot handicap, now allowed above 100 % for AI slots only, steps 125 / 150, labelled "Edge +25%" / "Edge +50%"). Saves and the LAN
lobby accept 10-200 %; a human slot stays at 100 % or below. `ai match --handicap-a/--handicap-b` measures it. **Why:** the owner wants a super-hard LLM
opponent; D-285 showed directives cannot add strength, so the strength comes from an honest, labelled edge and the LLM gives it its personality.
Calibration (docs/balance/s38_balance.md): tier 5 at +50 % v plain tier 5 wins 85-100 % of decided games for the Authority, Left Behind and Expedition,
68 % for the Directorate (its army is capped by something other than money; follow-up). **Alternatives:** LLM on tier 5 with no edge (no stronger than
tier 5), an LLM-chosen build plan (new design round, later). **Status:** confirmed by the owner (2026-11-21, session 38).

### D-287 · 2026-11-21 · Session 38: the Authority AI plays tiers 2-5 without respond_to_attacks (M4-16)
**Decision:** `[faction.authority.tier2]` .. `[faction.authority.tier5]` set `respond_to_attacks = 0` (data/ai/difficulty.toml; the key is now allowed
in per-faction tier tables, src/Data/DifficultyLoader.cs). Other factions keep the shared flag. **Why:** the Authority ladder was inverted on 128 games
(2 v 1 30.5 %, 5 v 3 39.8 %, 5 v 4 38.8 %): a higher difficulty played worse. One-flag screens found respond_to_attacks (garrison/Blockhouse, wave regroup,
mop-up, bag growth ...) as the cause (tier 2 without it: 76 %); the wave regroup is the largest of several leaks. After: 2 v 1 70.9, 3 v 2 50.0, 4 v 3
59.1, 5 v 4 52.2, 5 v 3 56.3, 5 v 1 66.7. Keeping tier 3 unchanged was screened and rejected (3 v 2 falls to 19 %). Campaign sweep 45/45 meets D-222.
**Alternatives:** fix the regroup and keep the flag (several leaks, no single sub-fix worked), flag off for every faction (moves the Directorate/LB
ladders and Authority v LB to 93 %). **Status:** confirmed by the owner (2026-11-21, session 38).

### D-288 · 2026-11-21 · Session 38: Authority tier-3 army cap 40 (M4-16, M13-37)
**Decision:** `[faction.authority.tier3] army_cap = 40` (data/ai/difficulty.toml). **Why:** with respond_to_attacks off (D-287) the tier-3 Authority
overran the Left Behind and the Expedition on Rust Belt (75 %). Screens of its allowed tier-3 keys (64 games, six rows): attack_ratio_pct is inert for the
Authority, attack_threshold 6/11 and expansion_tick 7200 did less; the cap took the 12 Authority rows from a mean distance of 14.6 to 12.7 points (s37:
10.4) on 128 games and keeps the ladder (3 v 2 46.8, 4 v 3 64.8, 5 v 3 62.8 on 80 games). Over all 24 cross rows the tier-3 table is about 14.4 from 50 %
(s37 13.3): a small step back for a working ladder. **Status:** confirmed by the owner (2026-11-21, session 38).

### Session 39 confirmations · 2026-11-21
The owner confirmed every session-37 "assumed — confirm" item ("confirm all"): D-280 (unit package numbers), D-281 (map and AI fixes for mid-row
starts and the Expedition lane), D-283 / D-284 (objective guidance and the briefing/text style). Their status lines stand as confirmed from this date.

### D-289 · 2026-11-21 · Session 39: par times = the bot's tier-3 median win x 2.5 (Q-73, M10-19)
**Decision:** a mission may carry `par_seconds` in its `[mission]` table (60-36000; missing = no par). The Profile page and the debrief award a
"Par time" medal when the best winning time is at or under par. Values = the session-37 suggestion (docs/balance/s37_balance.md): the bot's tier-3
median win time x 2.5, rounded up to a whole minute, for 12 missions; auth_02, dir_03, dir_04 and lb_03 end on a timer and get no par. **Why:** owner
answer to Q-73 (s39). The par is derived from the stored best time, so no profile format change. **Alternatives:** bot time + 50 % (Q-73 b; far too
tight for a person: auth_01 would be 5 min against the owner's 8:35), hand-set times (deferred until play). **Status:** confirmed by the owner (2026-11-21).

### D-290 · 2026-11-21 · Session 39: the Directorate AI buys no Bores; Rocket Truck salvo damage 90 -> 84 (M13-37)
**Decision:** new per-faction difficulty keys for the Directorate Bore AI (`bore_max`, `bore_army`, `bore_gap_ticks`, `bore_dig_short`, `bore_sites`,
`bore_min_cash`; defaults = the former DirectorateTactics constants). Shipped: `[faction.directorate] bore_max = 0` (the AI never buys a Bore; a human
player's Bores are unchanged) and `dir_rocket_salvo damage = 84` (was 90). **Why:** owner: fix the AI's Bore use first, fall back to a cap, pair it with a
small Directorate nerf. Five placement/timing variants were screened on the 12 Directorate cross rows (64 games): Hatch beside the ore field only,
digging 30 cells short of the target, 1 Bore, a later and richer first Bore; none made Bores pay (every one left the Directorate weaker than no Bores at
all, which is +15..32 points for it). So the AI stops buying them, and the strength it gains is taken back with a weapon nerf (Rocket Truck cost +1 %
overshot against the Expedition and left the Authority rows at 20 %, the known cliff). 128 games per row: the 12 Directorate rows' mean distance from 50 %
goes 19.2 -> 13.6, rows inside 40-60 % 4 -> 7 (docs/balance/s39_balance.md). Left as they were: Authority v Directorate on Ash Plains (11 -> 13 %),
Directorate v Left Behind Rust (26.5 -> 19.7 %) and Directorate v Expedition Glass Coast (20.9 -> 17.9 %). **Alternatives:** damage 80 (MAD 17.0) or 86,
cost 980 (MAD 22.5), Directorate tier-3 attack_ratio_pct 170/200 (no effect on the map rows), bore_sites = 1 (MAD 16.0). **Status:** assumed — confirm.

### D-291 · 2026-11-21 · Session 39: the Expedition AI plays tiers 4-5 without respond_to_attacks (M4-16)
**Decision:** `[faction.expedition.tier4]` and `[faction.expedition.tier5]` set `respond_to_attacks = 0`. **Why:** the Expedition ladder was flat and
draw-heavy at tiers 4-5 (128 games, rust_belt: 4 v 3 40.9 %, 5 v 3 65.9 %, 5 v 1 60.3 % with 43 % draws). Screens of harvester_target 3, attack_threshold
10 and expansion_tick 1200 at tiers 4-5 moved nothing; the flag that inverted the Authority ladder (D-287) is the cause here too. After: 4 v 3 94.6 %
(80 games), 5 v 3 94.0 %, 5 v 1 96.7 % (draws 5-9 %), 5 v 4 58.5 % (still 68 % draws: both sides play the same way). The tier-3 table is untouched.
**Alternatives:** tier 5 only (4 v 3 stays 41 %). **Status:** assumed — confirm.

### D-292 · 2026-11-21 · Session 39: Authority tier 5 harvester_target 4, attack_threshold 14 (M4-16)
**Decision:** `[faction.authority.tier5] harvester_target = 4`, `attack_threshold = 14`. **Why:** the target is tier 5 v tier 1 = 90 %; it was 66.7 %
(128 games). Screened at tier 5 (64 games, then 128 for the best): harvester_target 3, attack_threshold 10, expansion_tick 1200 and three combinations.
None reaches 90: the best, this pair, gives 5 v 1 72.0 %, 5 v 3 58.1 %, 5 v 4 64.8 % (96 games; was 52.2). The ladder climbs a little more; the tier-1
fixed build order stays hard to beat with data keys alone (an AI code change is the next lever). **Status:** assumed — confirm.

### D-293 · 2026-11-21 · Session 39: a rich AI with an income edge grows production (M13-38)
**Decision:** new per-faction keys `adapt_rich_cash` (0 = off) and `adapt_rich_max` (default 3): when funds reach adapt_rich_cash AND the side's own
handicap is above 100 % (the "Super hard (LLM)" edge), AdaptBuild skips the army gate and grows factories and barracks to adapt_rich_max. Shipped for the
Directorate: `adapt_rich_cash = 2000`. Normal games (handicap 100) cannot change: checked game for game on two rows. **Why:** the Directorate gained least
from the D-286 edge: not storage or supply (traces), but production (one Assembly Bore, AdaptBuild waits for an army of 30 its waves never keep). After,
tier 5 at +50 % v plain tier 5: 67.8 % (s38, 64 games) -> 79.0 % (128); at +25 %: 53.3 -> 68.9 %. **Status:** assumed — confirm.

### D-294 · 2026-11-23 · Session 40: per-biome ore amounts balance the map-specific rows (M13-41)
**Decision:** data/mapgen/biomes.toml: Ash Plains `ash_fields_per_start` 4 -> 3 and `extra_fields` 4 -> 2; Mountains `ash_fields_per_start` 3 -> 2;
Rust Belt `ash_fields_per_start` 3 -> 4; Glass Coast `ash_fields_per_start` 2 -> 3 and `field_density_min` 3 -> 5. Map hash pins re-pinned (D-220);
two decor bounds relaxed for small 3-4 player maps (clusters >= 300, dead <= 100 per mille, as the existing "cramped" exemption). **Why:** ore richness
drives the map-specific rows: less ore lifts the Authority and the Left Behind and drops the Directorate. 24-row table at 128 games (tier 3): MAD 14.6
-> 9.7, rows in 40-60 % 9 -> 15, range 13.5-74.4 -> 29.5-69.0 % (docs/balance/s40_balance.md). Owner's ask: "map side or AI side changes or unit
changes, just finish the balance issues". **Alternatives:** rough terrain, roads, shallows, ashfall damage, shipment rules, unit stats (Slinger,
Grapeshot, Harpoon, Glide Tank, Skimmer), Authority tier units (grow_bags): no gain or erratic at 64 games. **Status:** assumed — confirm.

### D-295 · 2026-11-22 · Session 40: an AI sale never leaves power short (M13-39)
**Decision:** EconomyRescue.SellOne skips a power structure whose sale would leave power made below power used (`KeepsPower`); applies to rescue and
"Broke" sales. **Why:** real Directorate games sold Reactor Vents with harvesters alive (15 of 24) and sat in low power (5 games 3 000+ ticks) since it
buys no Bores (D-290); only the last power plant was protected. The M4 test rig keeps its WithBores workaround (40 000 credits wall its harvesters in).

### D-296 · 2026-11-22 · Session 40: an idle harvester steps off its refinery's dock cell (M13-40)
**Decision:** HarvestMission.ClearDockFor: a harvester standing on an own refinery's dock cell steps to a free neighbour while a returning harvester in
Enter holds that dock's claim. Integer only, no new state. **Why:** the returner was blocked for the rest of the game (income frozen; all factions).
Not fixed: head-on jams in 1-wide corridors between AI buildings; late ore exhaustion is the main freeze cause on 96 maps.

### D-297 · 2026-11-22 · Session 40: grow_bags key, not used (M4-16)
**Decision:** new optional AI key `grow_bags` (shared tier or per faction and tier; missing = follow respond_to_attacks): tier-2/3 unit types join the
production bags once that tech tier is reached. Not set anywhere, so behaviour is unchanged. **Why:** since D-287/D-291 the Authority (tiers 2-5) and the
Expedition (tiers 4-5) build tier-1 units only. Set for the Authority it made it weaker (Rust v LB 72 -> 66 %, ladder 5 v 1 76 -> 68 %): its tier-2/3
units lose value for cost against tier-1 spam. Kept as a lever for a unit-value pass.

### D-298 · 2026-11-22 · Session 41: the balance finish line (owner)
**Decision:** skirmish balance is finished when, on fresh seeds at 128 games per row (tier 3 v tier 3, size 96, layout 1), all 24 cross rows are inside
35-65 % (decisive wins), the AI ladder is in order (a higher tier beats a lower tier of the same faction), and every campaign mission-tier meets D-222.
Then balance numbers are frozen; only the owner's play-tests change them. Order of work: AI unit choice and unit-v-unit tests first, then one joint
lever sweep on the cluster (queen excluded) with a fitted model. **Why:** 30 sessions of one-knob screens lowered the 24-row MAD 17 -> 9.7 with no end
point, and screen winners regress on fresh seeds (s40). **Alternatives:** stricter 40-60 %; sweep first.

### D-299 · 2026-11-22 · Session 41: unit-v-unit fixtures gate unit balance (M13-43)
**Decision:** `Tools ai duels` plays equal-cost armies (no AI, flat map, both sides attack-move, layouts x swapped sides; score = kept value share A - B,
permille). `data/balance/duels.toml` holds design-intent fixtures with bands (counters such as Rocketeer > Tank, and "grown AI lists beat tier-1
lists" for Authority, Directorate, Left Behind), checked by DuelFixtureTests. **Why:** whole-game tables cannot say which unit pairing is off; the
idea comes from an MIT RTS (Standard of Iron), ideas only. The Expedition list fixture is left out: its result flips with army size.

### D-300 · 2026-11-22 · Session 41: AI unit lists are data keys (M13-42)
**Decision:** `[faction.<id>] infantry_bag, vehicle_bag, infantry_tier2, vehicle_tier2, vehicle_tier3` (arrays of unit ids; missing = the built-in
list). None is set yet. **Why:** duels show the grown lists beat tier-1 lists for the same money (Authority +454, Directorate +120, Left Behind +385 at
8 000 credits), so D-297's "tier-2/3 units are worth less" was wrong; the Authority's lists hold a Foam Truck, Marksmen and Bulwarks. In games
(g41a, 64 per row): the Expedition's grown lists at tiers 4-5 weaken its ladder (5 v 1 95 -> 87 %, 4 v 3 94 -> 62 %): rejected. The Authority's
candidate lists move table rows by up to 14 points without improving the MAD; their average effect is measured as a lever in the joint sweep (w41).

### D-301 · 2026-11-22 · Session 41: stuck clock and recovery ladder, off by default (M13-44)
**Decision:** `[movement] stuck_recovery` (game.toml, default 0). With 1 a unit's stuck clock runs while its net displacement stays under 2 cells, and
rungs at 60 / 120 / 200 / 320 ticks replan, sidestep, relax (shove idle friends; arrive within 3 cells) and abandon. With 0 rules hash, state hash and
saves are unchanged. **Why:** idea from Standard of Iron; head-on corridor jams already resolve today, the real static case is a mover waiting at a held
goal (frozen for good at 0, answered at tick 332 at 1). Turning it on needs a measured series (it changes games).

### D-302 · 2026-11-22 · Session 41: committed-wave keys, off by default (M13-45)
**Decision:** tier keys `wave_reinforce_ticks` (idle army near the rally joins an out wave every T ticks, home guard kept) and `wave_search` (a wave
with no known target sweeps unvisited map sectors before it ends). Default 0: games and saves unchanged. **Why:** idea from Standard of Iron; a lever
for the ladder (Authority tier 5 v 1 76 %, target 90) to be measured before use.

### D-303 · 2026-11-22 · Session 41: map start-room test and audio provenance manifest (M13-46, M13-47)
**Decision:** MapStartRoomTests checks every generated 2-player start (4 biomes x sizes 64/96 x 8 seeds) for build room within 8 cells and reachable
ore within 20 (glass_coast 64 seed 7 is the tightest: 119 cells, floor set there). `data/audio/PROVENANCE.toml` lists all 306 audio files with source
and licence class from CREDITS.md (sfxr 26, Stable Audio 80, Kokoro 180, ACE-Step 20; no unknowns), generated by tools/audio/make_provenance.py and
checked by tools/audio/check_provenance.py and AudioProvenanceTests. **Why:** ideas from Standard of Iron; checks only, no gameplay change.

### D-304 · 2026-11-23 · Session 41: Authority tier-2 army cap 28; no unit number change (M13-48)
**Decision:** `[faction.authority.tier2] army_cap = 28`. No unit cost/health/damage, Ash map or Authority list change lands from the joint sweep.
**Why:** tier 3 (capped at 40 since D-288) lost to the uncapped tier 2 (37.5 % on seeds 1-64, 40.7 % on 65-128). Cap 28 on 256 games: 2 v 1 60.4 %,
3 v 2 52.6 %, 4 v 3 60.5 %; caps 20/25 broke 2 v 1 (25 / 50 %), 30-40 left 3 v 2 at 44-50 %. It touches no tier-3 table row. The sweep's solved package
failed on fresh seeds (Authority v Directorate Mountains 9.7 %, Ash 13.9 %), and the same build differs by 7.6 points per row between seed halves
(map pool), so the finish line's measurement is an owner question (Q-87). Supersedes nothing; D-300's Authority lists stay unset.

### D-305 · 2026-11-23 · Session 41: how the finish line is measured (owner, Q-87 option c)
**Decision:** amends D-298 point 1. Now: each of the six faction pairings, pooled over the four biomes, inside 40-60 %, with at least 256 games per
map row (seeds 1-128), and no single map row outside 25-75 %; ladders in order and D-222 unchanged. Later: once the map-specific causes are fixed in
the generator (mirror_x rush distance on Mountains/Ash, Glass Coast ore), move to all 24 per-map rows inside 35-65 % on 512+ games per row.
**Why:** generated maps tilt pairings: the same build differs by 7.6 points per row between seed halves, so 128 games cannot place a row inside a
30-point band. On s41's 256-game table main meets the pooled test (pairings 40.5-59.5 %, worst map row 27.3 %); open: Expedition ladder 3 v 2 (49 %).
**Alternatives:** (a) pooled only; (b) 24 per-map rows on 512+ games now.

### D-306 · 2026-11-23 · Session 42: the Authority ladder needs only to be in order (owner)
**Decision:** the Authority tier 5 v 1 target of 90 % (s39, D-292) is dropped; D-298's ladder test (a higher tier beats a lower tier of the same faction)
is the whole requirement. The map-specific fixes were scheduled for this session; queen stays cordoned. **Why:** owner, at the s42 briefing ("in order
is enough"). Main's Authority 5 v 1 was 76 % in the s41 handoff (not re-measured in s42). **Alternatives:** keep chasing 90 % with an AI code change.

### D-307 · 2026-11-24 · Session 42: Expedition tier-1 army_cap 10, tier-2 army_cap 14 (M13-49)
**Decision:** `[faction.expedition.tier1] army_cap = 10`, `[faction.expedition.tier2] army_cap = 14` (data/ai/difficulty.toml). **Why:** Expedition tiers
1-3 played alike (rust_belt, 256 games: 2 v 1 48.1 %, 3 v 2 44.3 %). The tier-2 cap alone is a cliff (20+ never binds, 12 makes tier 2 lose 91 % to tier 1),
and weakening one tier cannot order three, so tier 1 is capped below tier 2. Fresh seeds 193-320, 256 games: 2 v 1 76.3 %, 3 v 2 67.6 %, 4 v 3 92 %,
5 v 1 96 %. No tier-3 row moves; the campaign sweep is identical to s41 (45/45). The D-302 wave keys did nothing measurable for the Expedition at tier 3.
**Alternatives:** tier-1/2 caps 12/16 (3 v 2 62 %), 9/12 (3 v 2 84 %, 2 v 1 76 %); tier-3 respond_to_attacks off (moves every Expedition table row).

### D-308 · 2026-11-24 · Session 42: Mountains 4 plateaus, Glass Coast ocean width 3 (M13-50)
**Decision:** data/mapgen/biomes.toml: Mountains `cliff_plateaus` 2 -> 4; Glass Coast `ocean_width` 6 -> 3. Map hash pins re-pinned (D-220); the Glass
Coast seed-4 stall rig keeps the old ocean width. **Why:** 512 games per row (seeds 1-256): Mountains rows 34.0/55.5/39.7/53.6/41.9/45.4 ->
31.5/56.7/43.1/50.5/51.0/46.9; Glass 46.4/53.7/63.3/28.0/52.3/34.2 -> 54.4/54.8/62.9/27.2/44.3/44.0. With them all six pairings pooled over the four
biomes are inside 40-60 % (Authority v Directorate 39.2 -> 40.6 %); MAD 11.5 -> 10.9. No Ash Plains or Rust Belt map key helped (28 one-key variants across the four biomes plus round-2 combinations,
two 256-game confirms), so those biomes are unchanged. docs/balance/s42_balance.md. **Alternatives:** Glass ocean 2/4, Mountains plateaus 3/5 (screened).

### D-309 · 2026-11-24 · Session 42: stuck_recovery stays off (D-301)
**Decision:** `[movement] stuck_recovery` stays 0. **Why:** on the same seeds (1-128) it lowered the Directorate's two weakest rows: Rust Directorate v Left
Behind 30.6 -> 18.1 %, Glass Directorate v Expedition 27.3 -> 18.4 %, and moved Authority v Directorate about 6 points toward the Directorate on
Mountains and Glass (rows already below 50 %). **Alternatives:** on (D-301 left it
for a measured series; this was it).

### D-310 · 2026-11-24 · Session 43: finish line met; balance numbers frozen (Q-88 (a), D-298)
**Decision:** The owner answered Q-88 (a): the D-305 finish line counts as met within measurement error, and balance numbers (unit, AI and map data
keys) are frozen per D-298. Ash Plains Authority v Directorate 23.6 % and Rust Belt Authority v Left Behind 75.1 % stay as they are. Later changes
that move a match outcome need a new decision. The 24-row 35-65 % goal stays as later work. **Why:** both rows are within one standard error (~2
points) of the band; all six pairings pass, ladders are in order, the campaign meets D-222. **Alternatives:** (b) one AI-code round for the
Authority's early defence and re-measure; (c) widen the row band.

### D-311 · 2026-11-24 · Session 43: audit fixes 1-19 approved as recommended (s43 audit README §2)
**Decision:** owner: "take all" for the session 43 adversarial audit items 1-19: tunnel vehicles surface once (no phantom claim); a unit stuck
underground re-routes to another own entrance after 10 s; garrisoned soldiers are spared radiation and superweapon damage and dived subs the
superweapon blast (as D-205 for splash); capture resets a superweapon charge; production pauses (progress kept, nothing spent) while its
prerequisite is missing, like classic C&C; the Second Drift storm is non-lethal as designed (contamination cap 31 < damage threshold 32) and
denies building at `no_build_threshold`; a Bore is not a base for defeat; a refused order keeps the shift-queue; AI: rebuild a lost refinery or
factory while broke, mop-up no longer tied to respond_to_attacks, Directorate builds anti-air (all three re-measured on 512 games before
landing); Sonar Nets also extends sonar range; campaign edge reinforcements spawn on free cells; auth_04 gets its aircraft and the Cistern and
Tank Farm are unlocked; the commander's stale window follows its timeout; Authority aircraft text no longer says "fast"; stale text fixed to
match data (manual rebuilt); superweapon button sized to its label and shows its range; a Repairing line on the card.
**Why:** docs/reviews/2026-11-24_s43_audit. **Alternatives:** per-item choices in the audit README. **Status:** owner decision

### D-312 · 2026-11-24 · Session 43: update check off by default (supersedes D-214's default)
**Decision:** `check_for_updates` defaults to false; a "Check now" button in Settings runs one check. Stored settings keep their value.
**Why:** private game; nothing goes online unless the player asks. **Alternatives:** keep on (D-214). **Status:** owner decision

### D-313 · 2026-11-24 · Session 43: all 20 QoL items from the s43 list
**Decision:** build the 20 main QoL items (docs/reviews/2026-11-24_s43_audit/wave1/QOL.md); anything that changes how a match plays is opt-in
with defaults that reproduce today's game byte for byte. **Why:** owner. **Alternatives:** top 6; none. **Status:** owner decision

### D-314 · 2026-11-24 · Session 43: sim spatial index later, render speed-ups now
**Decision:** render allocation and culling fixes now; the sim targeting spatial index waits for its own session (determinism risk).
**Status:** owner decision

### D-315 · 2026-11-24 · Session 43: art pass pilots approved, fan-out to every asset
**Decision:** buildings: the auth_power pilot look, 8-frame idle and damaged loops at ticks_per_frame 2 (0.4 s per frame), every building gets
the detail pass and its own idle motion (turret-facing sheets and walls detail only). Vehicles: a 4-frame `move` state stepped by distance
(`step_px`) on all 24 tracked and wheeled vehicles; idle carries move-frame-0 detail. Infantry: variant C (keyline plus rim light), the pilot
boost, 64 px frames, 8 walk frames, a flash star. Terrain: `[detail] level 1` ("rich", cracks toned down) on all four biomes; old decor road
and frost crack decals dropped where detail is on. Renders run on the k3s cluster. **Why:** owner, after before/after sheets. **Status:** owner decision

### D-316 · 2026-11-24 · Session 43: smoke without bubbles
**Decision:** smoke_plume and the smoke/dust puffs are rendered as volumetric smoke (tools/art/fx/build_smoke_vol.py): lighter, wispier,
torn edges, west drift; no baked sphere smoke on any building, vehicle or infantry sheet; round soft map decals (snow, soot, oil, ash, salt)
become noise-broken shapes; Mountains snow about half. **Why:** owner: "a lot of the smoke effects still look like bubbles". **Status:** owner decision


### D-317 · 2026-11-24 · Session 44: mechanic work allowed under the D-310 freeze, one 512-game re-measure at the end
**Decision:** the BACKLOG mechanics (M16) may change how matches play while balance numbers stay frozen; ONE 512-game table (24 rows, seeds
1-256, the D-305 finish line) runs at the end of the mechanics wave and decides any re-tuning. **Why:** owner: "we are making a lot of changes
here so that sounds prudent". **Alternatives:** keep the freeze and park the mechanics; per-item measurement. **Status:** owner decision

### D-318 · 2026-11-24 · Session 44: land the held D-311 items 5-11 (B-00)
**Decision:** merge `bal-s43` (economy items + AI fixes) into integ-s44. The probes showed integ43 (neither branch) at 40.8 % pooled Authority v
Directorate (s42 40.6 %), aioff43 (economy items only) 38.3 %, bal43 (both) 36.5 %, noaa43 identical to bal43 (Directorate anti-air: no effect):
each half costs ~2 points. Under D-317 the drop is judged in the end-of-wave re-measure, not before. **Status:** lead, under the owner's D-317

### D-319 · 2026-11-24 · Session 44: build every s18 "built later" mechanic and the M16 owner items (all lead recommendations accepted)
**Decision:** build M-01 sea mines, M-02 Diver swimming, M-03 Drydock production/repair, M-04 second (and third) weapon slot, M-05 Harpoon tether,
M-06 Grapple breach, M-07 Salvage Rights value, M-08 Scrap Plate armour, M-09 Patchwork range, M-10 Provision Store stock (+10/+5), M-12 bay repair,
M-13 hover harvester on Shallow, M-15 beacon delivery. M-11 repair auras honour aura_targets; M-19 keep the Left Behind Yard free-Gleaner loop (C&C
refineries have the same loop); M-20 drop the `irradiated` tag; M-22 Flak spares its own aircraft; M-25 ash resist scales; A-02 wire or remove inert
AI keys, a malformed difficulty.toml is reported; A-03 AI repair stays on until done; U-05 structure control groups, Shift+number adds,
Shift+right-click on an enemy queues an attack; U-11 "Ashfall Refinery" everywhere; C-08 lb_03 tier 5 harder than tier 4; C-09 mission AI follows
difficulty in every AI mission; O-09 telegram briefings, plain objectives and field notes; A-06 park layout 2 (code kept, off).
**Why:** owner: "I am officially accepting all your recommendations ... full send". **Status:** owner decision

### D-320 · 2026-11-24 · Session 44: confirmations and housekeeping
**Decision:** O-06 confirm the s43 assumptions (Second Drift no_build_threshold 24, edge reinforcements, QoL key defaults, QOL-16 drag move,
QOL-03 Alt+drag); B-07 confirm D-290..D-294, D-304, D-306..D-309; T-08/A-04 close the open `[~]` balance rows under D-310; B-06 Q-70 superseded;
O-05 generated SFX are CC0; O-04 CC BY-SA 3.0 is allowed for art assets only, kept as separate credited files (supersedes D-033 for art; D-188's
0 A.D. props stay). **Status:** owner decision

### D-321 · 2026-11-24 · Session 44: T-04 LFS patterns not applied
**Decision:** no new .gitattributes LFS patterns. Blanket `*.pdf`/`*.ogg`/skins/portraits patterns matched 305 tracked non-LFS files (voice,
skins, portraits, manual PDFs), left every checkout dirty, and skins are kept out of LFS so the cloud can push them (s28-s29). A per-folder
migration needs its own decision (no history rewrite, D-320/T-04). **Supersedes:** the T-04 part of the owner's batch, which assumed new files only.
**Status:** lead (reported to the owner)

### D-322 · 2026-11-24 · Session 44: values chosen for the new mechanics (assumed — confirm)
**Decision:** where the design docs gave no number, the packages chose these data values (all in data/rules or data/ai):
mines 6 per Minelayer, 30 ticks per mine, 250 damage (exp_sea_mine vs ships), same-cell trigger, automatic sweep radius 3 every 60 ticks, restock 1
per 90 ticks within 2 cells of an own naval building, subs immune; Destroyer = gun + depth charge (exp_launch_charge) + AA (exp_destroyer_aa, Carrier
AA numbers) in a third slot `tertiary_weapon`, Patrol Launch + deck gun (exp_launch_gun, Marine rifle numbers), extra mounts fire on the move and
scan every 5 ticks; Diver swims at the Naval terrain speed, hidden only in deep Water, firing does not reveal it; Drydock repairs 5 permille per 10
ticks within 2 cells at the global repair cost, landing craft and Skyhook bay repair 5 permille per 10 ticks; tether 60 ticks at half speed (no
leash); breach 100 ticks, cooldown 400, Hook Runner soldiers only; Salvage Rights wrecks pay x1.5 (design); Patchwork +6 cells and heal x4; Scrap
Plate turns Light vehicles, Rotors and Kites to Heavy armour (already-Heavy keep +20 % health); ash damage proportional to (1000 - resist);
naval gunfire needs a seen cell (Destroyer/Cruiser guns); Missile Sub dives in 60 ticks and carries a torpedo (140 damage, 7 cells, 5 s); beacon
delivery: vehicles drop in reach, infantry build in 200 ticks, 100 credits at placement; Expedition AI from tier 3: one Drydock, +1 Cruiser
+1 Carrier, one Minelayer, ships under 50 % go to the Drydock; lb_03 tier 5 ColumnDelay 3600, riflemen 9; tut_01 briefing stays plain prose.
**Status:** assumed — confirm (USER_CHECKS session 44)
