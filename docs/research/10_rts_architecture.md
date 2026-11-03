# How RTS Engines Are Architected: Lockstep, Determinism, Object Models

**Summary**

- Most classic RTS engines (Age of Empires, Supreme Commander, StarCraft II, OpenRA, Spring, Warzone 2100) use **deterministic lockstep**: every machine runs the same simulation and only exchanges player commands. Bandwidth then depends on the number of commands, not the number of units.
- The price is determinism. A single divergence (an uninitialized float, a pointer-keyed map, a shared RNG call) splits the simulation, and the usual response is to stop the game. Factorio, Riot and OpenRA all built dedicated tooling (per-tick checksums, state logs, replay) to find these.
- Commands are scheduled a fixed number of ticks in the future, which hides network latency. Typical values: 200 ms turns in AoE (about 400 ms effective delay), 100 ms playout in Fiedler's model, 100 ms ticks in Supreme Commander.
- Sim and rendering are separated: a fixed-rate simulation (10 to 60 ticks per second) with rendering and UI running faster and interpolating. Code that touches the render or UI must be kept out of the sim (OpenRA's `RunUnsynced` guard, Spring's SYNCED/UNSYNCED Lua split).
- Object models range from C&C-style class hierarchies, to OpenRA's trait/composition model, to ECS (Bevy, 0 A.D.'s components). For 500 to 2,000 entities any of them works; the deciding factors are determinism discipline and moddability.
- Recommendation for RTS_2027 is at the end of the document.

## 1. The lockstep model

**What goes over the wire.** In Age of Empires, only player commands are sent. The authors rejected sending per-unit position, status and facing, because that approach "capped the game at about 250 moving units" ([1500 Archers on a 28.8](https://www.gamedeveloper.com/programming/1500-archers-on-a-28-8-network-programming-in-age-of-empires-and-beyond)). Fiedler's demo goes further: input is six booleans sampled once per frame, so a million-object simulation needs the same bandwidth as one object ([Deterministic Lockstep](https://gafferongames.com/post/deterministic_lockstep/)). Replays fall out of the same design: they store only commands and re-run the game. The downside is that replays break after any patch that changes simulation behaviour.

**Turn length and command delay.** AoE ran turns of roughly 200 ms. A command issued in turn N executes in turn N+2, so the network has one full turn to deliver it. The turn length was set by the slowest machine's sustainable frame rate, and the design target was a steady 15 fps on a 90 MHz Pentium with a 28.8k modem. Fiedler's example runs 60 inputs per second with a 100 ms playout buffer, and lets the receiver run up to 4 simulated frames per render frame to catch up after jitter. Supreme Commander's simulation runs at a fixed 10 ticks per second, so each tick has 100 ms of budget, and commands are scheduled several hundred milliseconds ahead in practice ([Opinion: Synchronous RTS Engines](https://www.gamedeveloper.com/design/opinion-synchronous-rts-engines-and-a-tale-of-desyncs)).

**Hiding latency.** AoE found that players tolerate a steady delay far better than a variable one: "250 ms was not even noticed, 250 to 500 ms was playable, beyond 500 ms became noticeable," and a steady 500 ms beat a delay that alternated between fast and slow. Immediate visual and audio feedback (a click flash, a sound) covers the rest. Supreme Commander adds client-side previews of animation, which the author notes are hard to get right in edge cases.

**Slow peers.** AoE does not drop slow players. The whole game runs at the pace of the slowest machine. Each client reports its sustainable frame rate and worst ping, and the host recomputes a target turn length each turn. Turn length rises fast to absorb spikes and falls slowly back, aiming for changes of a few milliseconds per turn. Fiedler's model stalls the sim until the missing input arrives, which shows as hitches. Lock-step designs therefore need a policy for disconnects. AoE's text does not describe one.

**Transport.** AoE used UDP with the game handling ordering, drop detection and resend ("When in doubt, assume it dropped"). Fiedler shows why TCP hurts: at 100 ms latency with 1% loss, retransmission waits cause periodic stalls. His UDP design resends every unacknowledged input in every packet, which costs 2 to 15 frames of redundancy on average. Input deltas are encoded at 1 bit when unchanged and 7 bits when changed.

**Detecting and debugging desyncs.** Three techniques recur:

1. **Per-tick or periodic state hashing.** AoE checksummed world objects, pathfinding and targeting. OpenRA's `OrderManager.Tick()` records `world.SyncHash()` before and after each order and sends the list to all peers; `CheckSync` compares byte by byte and on mismatch dumps `syncreport.log`, then identifies the order index that differs. Supreme Commander hashes full state once per second. Factorio computes a CRC of the whole map every tick in a debug mode (slow, "at most units of FPS") and compares a live run against a replay of its save.
2. **Readable state logs and diffs.** Riot writes key-value state each frame, only for values that changed, and a Python script reports the first divergence, since later differences cascade from it. Factorio's debug saves are human-readable, and a binary diff guided by tags finds the first differing variable. The first divergence is usually tiny, often a single value.
3. **Recorded playback plus a test farm.** Riot replays 2,000 to 3,000 public beta games per day on dedicated hardware to catch divergences before release.

Hash-based detection has a cost: Riot notes that checksumming raw memory makes the source hard to locate and requires converting pointers to deterministic handles. Their choice was logs over checksums; OpenRA and Factorio used checksums with separate logs for localization.

**Replays as a byproduct.** Because the sim is a pure function of its inputs, saving the command stream and the seed is enough. Riot's Server Network Recording captures the inputs in arrival order, and it records startup configuration too, which required rewriting startup code that had been stable for nearly a decade.

## 2. Determinism rules

- **Floating point.** Same binary, same CPU and same flags usually gives bit-identical results, but compilers, debug versus release builds, instruction set and OS can each change results. Fiedler's ODE fix worked only on one machine and binary, and he calls floating-point determinism complex with no silver bullet. Bruce Dawson's reprint of his floating-point notes adds that minor rearrangements of a formula can change its result and that `sin(float(pi))` is not zero. Factorio's ARM versus x86 work found real divergences: casting a double to an integer when the value does not fit gives different results on each architecture. The team removed undefined behaviour only where it broke determinism, and checked CRCs on every tick across 2,417 tests ([FFF-370](https://factorio.com/blog/post/fff-370)).
- **Uninitialized values.** Riot's worst bug appeared in about 1 in 5,000 games. A vector class skipped zero-initialization for speed, and a comparison used an uninitialized float in a level-of-detail check. Setting the value to FLT_MAX reproduced the bug every time. A binary search over about 580,000 constructions found the exact one. Riot moved toward inline member initialization but did not zero-initialize everywhere, because of the cost across millions of vector constructions.
- **Fixed-point.** Riot's article shows its positions and velocities as 32-bit floats (`Riot::Vector3f`) and does not say it switched to fixed-point; it controlled determinism through initialization, RNG and ordering (the fixed-point question is unverified for Riot and Factorio). OpenRA's `Sync` only hashes `int`, `bool`, and its own types such as `CPos`, `WPos`, `WDist`, and `WAngle`. It throws `NotImplementedException` for `float`, collections and enums unless you provide a custom hash.
- **RNG.** One PRNG, seeded from something recorded (Riot uses a game ID from the command line, an xorshift generator isolated from the standard library). AoE needed the same number of RNG calls on every machine. Fiedler reseeds per frame in one solver to remove order dependence. Any call that uses `rand()` in an unsynced path can desync when the sim later reads that value.
- **Container iteration.** Riot found that iterating a `std::map` keyed on object pointers changed update order between runs, and differences grew when replaying on a different Windows version. Use stable IDs and ordered containers (or sort by ID) in the sim, never raw pointers as keys.
- **Time.** Riot unified six separate timing APIs into one clock over roughly six months. The sim should read only the tick counter, never wall time. Riot kept a variable frame delta for shipping, and calls a fixed timestep a future improvement.
- **Threads.** Riot excludes the network layer from determinism and keeps the server loop single-threaded, preloading data to avoid I/O races. Supreme Commander's author flags multithreading as the thing that makes determinism hardest.
- **Language traps.** In C++, uninitialized members, pointer ordering, and dangling pointers are the big three (the Demigod desync: a dangling pointer to a deleted pathing component was reused by a later allocation; setting it to NULL fixed it after nearly a week). In C#, `float` and `Dictionary` ordering need care; OpenRA's `Sync` refuses floats outright. In Rust, `HashMap` iteration order is randomized per process, so use `BTreeMap` or sorted keys in the sim.

## 3. Sim and render separation

The sim runs at a fixed tick; rendering runs at display rate and interpolates between the last two sim states. Supreme Commander is the clearest example: 10 sim ticks per second, a roughly 60 fps UI layer that handles rendering, UI and interpolation. Riot uses a variable frame delta throttled to a target rather than a fixed timestep, and says the fixed timestep is a future improvement.

Engines police the boundary:

- **OpenRA** provides `Sync.RunUnsynced`. For the outermost call it compares `world.SyncHash()` before and after the block and throws `InvalidOperationException` if the synced state changed. `AssertUnsynced` throws if code runs outside such a block.
- **Spring / Beyond All Reason** divides Lua into SYNCED and UNSYNCED environments. Synced code runs in lockstep, unsynced code (widgets, UI) does not affect the sim. The Spring wiki that describes the rules could not be read (see gaps). A Spring forum thread on "how does an unsynced widget cause sync errors" exists; I could not read it, so the exact rules are unverified here.
- **Warzone 2100** scripts never mutate game state. They read copies (snapshots) and issue commands that are queued for synchronization, so their effects are not visible until a later frame. Scripts must not store game objects in globals, because the snapshots do not update when the objects die.
- **Widelands** runs Lua through game commands (`Cmd_LuaScript`, `Cmd_LuaCoroutine`) so scripts stay inside the synchronized command stream. Its wiki warns that a debug-console string that changes game state can desync a network game.

**Headless sim.** Because the sim has no render dependency, it can run without a window for tests and AI training. Riot replays recorded games to catch divergences, and Factorio's replay-based CRC check (FFF-47) is the same idea; I did not verify how OpenRA or Warzone run their own headless tests.

## 4. Object model choices

**Class hierarchy (C&C, Tiberian Dawn, Red Alert).** Each unit type is a class. Simple, fast to learn, but adding a behaviour (e.g. "can capture") tends to spread across a hierarchy, and mods must edit C++.

**Trait / composition (OpenRA).** An actor is "a collection of traits." In `Actor.cs`, traits are not stored on the actor; they are registered in `World.TraitDict` through `AddTrait`, and looked up with `Trait<T>()`, `TraitOrDefault<T>()` or `TraitsImplementing<T>()`. Hot paths cache traits by interface in arrays (for example render and targetable) to avoid lookups. Each trait has a `TraitInfo` class populated from MiniYaml, so `Cost: 100` sets the info object's `Cost` field through `FieldLoader`. Result: a new unit is a YAML file plus existing traits, and mods rarely touch C#.

**ECS (Bevy, 0 A.D.).** Bevy stores entities as IDs, components as plain structs, and systems as functions that declare which components they read and write, which lets the scheduler run non-conflicting systems in parallel. Storage is table-based by default (fast, cache friendly, slow to add and remove components) or sparse sets (the reverse). Hugo Peixoto's walkthrough describes the archetype model: entities with identical component sets share a bucket, and adding a component moves the entity. 0 A.D. is ECS-flavoured rather than pure ECS: its `simulation/components/*.js` files each define a `Schema` (RelaxNG XML used for validation), `Init`, and named message handlers. `Health.js` shows the pattern: `OnValueModification` and `OnOwnershipChanged` handle engine messages, and `RegisterHealthChanged` posts `MT_HealthChanged` with old and new values. Components talk through messages and direct method calls, not shared state.

**Trade-offs for 500 to 2,000 entities.**

| Model | Strengths | Weaknesses for an RTS |
|---|---|---|
| Class hierarchy | Simple, debuggable | Mods need code; cross-cutting behaviour is awkward |
| Traits (OpenRA) | Moddable via YAML; behaviours composable | Per-actor dictionary lookups; relies on discipline for sync |
| ECS (archetypes) | Fast iteration, parallel systems, clear data flow | More machinery; hard to inspect; determinism needs fixed system order |
| Components as messages (0 A.D.) | Flexible, dynamic | Message order matters; slower; easy to hide state |

At this scale none of them is a performance problem. The decisive questions are how easily designers add units from data files, and how easily a developer can guarantee deterministic order.

**Data definitions.** OpenRA uses MiniYaml with inheritance (`Inherits: ^Name`); mods change almost all gameplay through YAML, but not UI or new factions. OpenAge uses nyan, a configuration format meant for modding. Warzone uses JavaScript for scenarios and AI. Data-driven rules make balancing fast and let a mission designer work without code.

## 5. Other subsystems

- **Spatial partitioning.** A uniform grid or quadtree over cells; query by radius returns candidates, then exact distance. Iteration must be sorted by entity ID for determinism.
- **Fog of war.** Per-player visibility grids updated when units move. Render and AI must read only the local player's view. Fog state is synced if it affects gameplay (AI and targeting), so it must be deterministic too. 0 A.D.'s Health component calls `Fogging.Activate` when it changes values, so changes are hidden from fogged viewers (the file shows this; the design rationale is my inference).
- **Order / command queue.** Players issue orders; the queue is applied at a scheduled tick. OpenRA's `IssueOrder` appends to a local list; `TickImmediate` sends immediate orders (chat, pause) outside the normal schedule. Orders are the unit of replay and of desync diagnosis.
- **Save games.** Snapshot all sim state, or serialize the command log and replay. Widelands persists Lua globals with the Eris library into `map/globals.dump`. Factorio-style sim-state saves make the desync workflow possible.
- **Scripting boundary.** Scripts should read snapshots and issue commands, never mutate state directly. Game objects should be referenced by ID. Only globals survive saves in Warzone's model. Callbacks must return quickly because the game waits for them.

## 6. Case studies

**OpenRA (C#, .NET, open source, Red Alert/TD-like).** The closest model to our goals. Lockstep with `OrderManager`, `FramesAhead` scheduling, per-order `SyncHash`, traits in `World.TraitDict`, MiniYaml rules, and `Sync.cs`, which generates hash functions with IL emit for `[VerifySync]` members. The lesson: a strict hash whitelist (`int`, `bool`, `CPos`, etc.) forces the developer to think about every type that enters the sim. OpenRA also keeps the sim and UI in one process and separates them with `RunUnsynced`.

**Spring / Beyond All Reason (C++ engine, Lua gameplay).** Gameplay in Lua with a SYNCED/UNSYNCED split; widgets run unsynced. The engine is lockstep, so the sync boundary is a constant source of bugs. I could not read the Spring wiki (blocked by bot protection), so its exact rules and numbers are unverified in this document.

**0 A.D. (Pyrogenesis, C++ engine, JavaScript simulation).** Simulation components are JavaScript files with RelaxNG schemas, message handlers and a `cmpX` access pattern. The engine is C++. Its turn-based lockstep details could not be read here (the wiki is disallowed by robots.txt), so they are unverified.

**Warzone 2100 (C++, JavaScript scripting).** Scripts are the mission and AI layer: global state, event callbacks such as `eventStartLevel()`, commands queued for sync, snapshots for reads. Its scripting doc is the best short description of a sim/script boundary that I found. Save/load rules (globals only, scripts re-run on load) are a warning about hidden script state.

**OpenAge (C++20 core, Python 3 scripting, nyan config).** A community reimplementation of the Genie engine, with no original assets. The README says gameplay is "basically non-functional" and the simulation is being rewritten. Useful as a cautionary tale: a reimplementation needs the sim designed first, then the rest.

**Widelands (C++, Lua via Luna).** Lua classes mirror the C++ hierarchy, and Lua is reached through the synchronized command stream. Persistence goes through Eris. It shows that a scripting layer can stay deterministic if every script entry point is a command.

## Implications for RTS_2027

- **Recommendation: use deterministic lockstep, LAN first.** Commands only on the wire; the sim is a pure function of (initial state, seed, command log). This gives multiplayer and replays from one mechanism, and LAN latency lets us use small command delays.
- **Recommendation: fixed tick rate of 20 Hz (50 ms) with command delay of 2 to 4 ticks (100 to 200 ms).** This sits between AoE's 200 ms turns and Supreme Commander's 10 Hz, and fits the "steady beats variable" finding. Make both values config.
- **Recommendation: integer or fixed-point positions and timers in the sim.** Floats can stay in rendering only. Trade-off: more care with math, but no cross-machine float risk and simple hashing. Use float only where the result is never fed back into the sim.
- **Recommendation: one seeded RNG, owned by the sim.** Log the seed with the replay. Forbid `rand()` and wall-clock reads in sim code, enforced by code review or a lint rule.
- **Recommendation: per-tick (or every-N-tick) state hash from day one**, plus a per-tick debug log of changed values, like OpenRA's `SyncHash` and Riot's logs. Trade-off: a debug-only full hash is slow, so ship a cheap hash on all builds and the full log in debug builds only.
- **Recommendation: ordered iteration by entity ID, never by pointer or hash-map order.** Trade-off: sorted iteration costs a little time, which is negligible at 2,000 entities.
- **Recommendation: data-driven units via YAML or TOML, with a component-style model (entity ID + component structs + system functions called in fixed order).** This is ECS-like without a framework. It is simpler than Bevy and clearer than a deep class hierarchy. Trade-off: we write the scheduler ourselves, and we must keep the system order documented.
- **Recommendation: scripting (Lua or Python) for missions and AI behind a command-only API.** Scripts read snapshots, issue commands, and never store object pointers. Mission scripts run in the synced path; UI and camera hooks run unsynced. Trade-off: a script boundary adds an API to maintain, but it keeps determinism enforceable.
- **Recommendation: a sync boundary check like OpenRA's `RunUnsynced`**, or an assertion that the hash is unchanged around render and UI code in debug builds.
- **Trade-off to accept:** lockstep means one slow machine slows everyone and a disconnect stalls the game. For a LAN, this is acceptable. Plan for a "wait or drop" policy later rather than designing around it now.
- **Trade-off to note:** no mid-game join in a pure lockstep design unless we send the full state at join time. Decide whether this matters before starting the netcode.

## Open questions for the owner

1. **Language.** C++ (matches the EA source we may port, most engine examples), C# (matches OpenRA, faster to write, `float` and hashing rules differ), or Rust (strong typing, but no direct lineage). Options: C++ for direct porting; C# for development speed; Rust for safety and a clean ECS ecosystem.
2. **Multiplayer scope.** Is LAN-only lockstep enough, or do you want internet play someday? LAN allows 100 to 200 ms command delay; internet play needs more, and more pressure on the stall policy. Options: LAN only (simplest); LAN with a host-authoritative fallback later.
3. **Tick rate.** 10 Hz (cheap, coarse motion), 20 Hz (smooth enough, the recommendation), or 30 to 60 Hz (smooth, more CPU and more hash cost). Option A is less code; option B is the balance point.
4. **Mid-game join and reconnect.** Do you need players to rejoin after a crash? Options: no (lockstep only, end the game on disconnect); yes (periodic full-state snapshots, which we can design in later).
5. **Scripting language.** Lua (widest game tradition, what Warzone and Widelands use), Python (what OpenAge uses, easier for a rusty programmer), or no scripting beyond data files for the first milestone. Lua is the default choice unless you prefer to avoid a second language.

## Sources

**Web sources read:**

- [1500 Archers on a 28.8: Network Programming in Age of Empires and Beyond](https://www.gamedeveloper.com/programming/1500-archers-on-a-28-8-network-programming-in-age-of-empires-and-beyond)
- [Glenn Fiedler, Deterministic Lockstep](https://gafferongames.com/post/deterministic_lockstep/)
- [Factorio Friday Facts #47 - CRC fun](https://factorio.com/blog/post/fff-47)
- [Factorio Friday Facts #188 - Bug, Bug, Desync](https://factorio.com/blog/post/fff-188)
- [Factorio Friday Facts #370](https://factorio.com/blog/post/fff-370)
- [Riot Games: Determinism in League of Legends: Implementation](https://technology.riotgames.com/node/68)
- [Riot Games: Determinism in League of Legends: Fixing Divergences](https://technology.riotgames.com/node/84)
- [OpenRA book (print)](https://www.openra.net/book/print.html)
- [OpenRA Actor.cs (bleed branch)](https://github.com/OpenRA/OpenRA/blob/bleed/OpenRA.Game/Actor.cs)
- [OpenRA Sync.cs (bleed branch)](https://raw.githubusercontent.com/OpenRA/OpenRA/bleed/OpenRA.Game/Sync.cs)
- [OpenRA OrderManager.cs (commit 6d6d1e2)](https://github.com/OpenRA/OpenRA/blob/6d6d1e230b6cbad87653f7417424bcf59608c5e3/OpenRA.Game/Network/OrderManager.cs)
- [Opinion: Synchronous RTS Engines and a Tale of Desyncs (Game Developer)](https://www.gamedeveloper.com/design/opinion-synchronous-rts-engines-and-a-tale-of-desyncs)
- [Warzone 2100 Scripting.md](https://www.github.com/Warzone2100/warzone2100/blob/master/doc/Scripting.md)
- [Widelands Lua scripting developer guide (implementation notes)](https://www.widelands.org/documentation/_sources/implementation.rst.txt)
- [OpenAge (Zain-Balkhi fork) README](https://github.com/Zain-Balkhi/openage)
- [0 A.D. Health.js simulation component](https://raw.githubusercontent.com/0ad/0ad/master/binaries/data/mods/public/simulation/components/Health.js)
- [Bevy ECS README](https://github.com/bevyengine/bevy/blob/main/crates/bevy_ecs/README.md)
- [Hugo Peixoto, Rust gamedev: ECS with Bevy](https://hugopeixoto.net/articles/rust-gamedev-ecs-bevy.html)
- [Bruce Dawson floating-point oddities (2012 reprint, Game Developer)](https://gamedeveloper.com/programming/in-depth-floating-point-complexities)

**Could not read (blocked by bot protection or robots.txt):** the Spring wiki pages on Lua environments, Lua System and sync-to-unsync; the Spring forum thread on unsynced widgets; the Spring and 0 A.D. wiki design pages; Beyond All Reason RecoilEngine docs.

**Gaps:**
- Spring/BAR synced vs unsynced rules: unverified. Only OpenRA's and Warzone's equivalents were read.
- 0 A.D. turn-based lockstep mechanism: unverified; only the component file was read.
- StarCraft II and Halo lockstep: only a Halo mention from the Supreme Commander article; no primary source read.
- Factorio FFF-188 does not describe the CRC scheme; the CRC detail comes from FFF-47, which is an older post about an older codebase.
- Riot's fixed-point and float choices are not described in the article read; Riot's use of floats is inferred from its `Riot::Vector3f` mention.
