# Lens A: Determinism, lockstep, replay/save robustness

## Findings (ranked, most dangerous first)

### A-1 · AI and Lua cannot join the hashed/saved state, and the docs contradict each other on where they run · severity: WILL-BREAK
- Evidence: `Simulation.cs:25-27,46-48` builds `_parts` as a private readonly array inside the constructor. There is no registration API, and `RunAiBookkeeping`/`RunTriggers` are empty private methods (`:431-432`). Architecture §2 says `src/AI` "issues Commands only", while §3 says "AI and Lua run inside `Step`". `src/AI` and `src/Scripting` are separate assemblies that depend on Sim.
- Failure: M2 AI memory (build orders, targets, timers) has nowhere to live. If it sits outside the sim it is not saved, so a loaded skirmish plays differently. If it runs per peer inside `Step`, any AI tweak changes the output of every old replay, because the replay stream only holds human commands. M4 Lua `state` has the same problem. Decide this before the first AI line is written.
- Fix: (a) AI is an external command producer that reads a snapshot and emits ordinary `Command`s. Those commands go through the same recorded stream, and in LAN only the host or slot owner runs it. Replays then do not depend on AI code, and AI memory stays out of the hash (but is saved in the save file's own section). (b) Lua is the only in-Step extension. Give `Simulation` an `ISimExtension` (OnStep, plus an `ISimStatePart`) passed in `SimSetup`, and put a script hash in the replay and save headers. When: NOW, as a DECISIONS entry plus the hook, before M2. Cost: S for the decision, M for the hook.

### A-2 · Saves and replays are hard-bound to the exact rules hash and to list indices · severity: COSTLY-LATER
- Evidence: `ReplayPlayer.cs:309` and `SimReader.cs:497` throw on any `RulesHash` difference. Sim state and commands store `TypeIndex` into id-sorted lists (`SimRules.cs` doc; `Command.Arg` = type index for Build/Place; `ReplaySpawn.Type`).
- Failure: the first balance tweak, or adding one unit with an id that sorts in the middle, invalidates every save and replay. Campaign progress saves between missions (priority 1) break on every patch. Replays are useless as a regression corpus after M2 data churn.
- Fix: embed the canonical rules TOML (compressed, tens of KB) in the replay and save, and load playback rules from that. Also record ids rather than indices in commands, or add a persisted index-to-id table. When: before M2 locks the data schema. Cost: M.

### A-3 · Replay recorder is "write at Finish" with a thin header; spawns are duplicated by hand · severity: COSTLY-LATER
- Evidence: `ReplayWriter.cs:417` buffers everything in a `MemoryStream` and only `Finish` writes it. Architecture §5 promises a footer index and a header with build hash, player slots, factions and difficulty, but the header has only rules, map, seed, size and player count (`:467-469`). `RecordSpawn` says it "must be called before tick 0, in the same order as the Simulation spawns". `ReplayRecording` repeats every `SpawnVehicle` call by hand.
- Failure: a crash or alt-F4 loses the replay, which is exactly the one you want for a desync report. A start state with credits, factions, handicaps, AI slots or mission setup cannot be rebuilt from `ReplaySpawn`. Forgetting one `RecordSpawn` makes a bogus desync.
- Fix: `Simulation` keeps its own append-only `SetupLog` of out-of-Step spawns and placements. The writer reads that log instead of the caller re-recording it. Stream-write records with a flush every N ticks. Make the header a serialised `SimSetup` (players, factions, difficulty, AI slots, build hash). When: before M2. Cost: M.

### A-4 · Public mutators let sim state change outside `Step`, unrecorded · severity: COSTLY-LATER
- Evidence: `SpawnVehicle/SpawnInfantry/PlaceBuilding/AssignMission` are public with no tick guard (`Simulation.cs:85-105,207,366`). The store arrays are public and writable (`MobileStore.cs:97+`). The Game sandbox and Tools call them directly. The doc rule "nothing outside Sim.Step mutates sim state" is enforced by comment only.
- Failure: any M2 UI shortcut or cheat that calls one of these mid-game desyncs LAN and breaks replays, and the hash only flags it 16 ticks later.
- Fix: throw if `Tick > 0` and not inside `Step` (a `_inStep` flag), with an explicit `SetupMutator` scope for scenario code. Make the arrays `internal` with read-only views. When: NOW. Cost: S.

### A-5 · The command pipeline is not yet lockstep-safe · severity: COSTLY-LATER
- Evidence:
  - `Simulation.cs:148-152` orders by (Player, Sequence) only. A tie keeps input-list order, which on a network differs per peer. `Sequence` is a `ushort` and nothing says who assigns it or guarantees uniqueness.
  - `ApplyInput:136` silently drops `ExecTick != Tick`, so a late command vanishes without a trace.
  - `Command.Player` is trusted (only `Place` checks `< PlayerCount`).
  - `CommandKind.Debug` is live in every build (`:180`); the architecture says "debug builds only".
  - `Place` has no owner, cost or adjacency check (M1 stub).
  - `Guard` with an out-of-map cell is queued and stored in `NextCell` (`Simulation.Combat.cs:340-342`).
- Failure: M5 gets order-dependent ties, undetected late-command divergence and spoofable player ids. A peer can send `Debug` to burn RNG or any `Player` value.
- Fix: make `Compare` total by adding Kind, Subject, Target, Cell and Arg after Sequence. Make `Step` fail loudly (debug) on `ExecTick < Tick`. The Net layer overwrites `Player` from the connection slot. Wrap `Debug` and `Chat` in `#if DEBUG` or a setup flag, and keep `Chat` out of the sim stream. When: comparer and guards NOW (S), the rest before M5. Cost: S.

### A-6 · Save and hash tests are blind to state outside "array fields of parts" · severity: COSTLY-LATER
- Evidence: `HashCoverageTests` walks only array-typed fields of parts. Scalars such as `PathService._nextId/_debt`, and any field on `Simulation` itself (`_pendingMoves`, `_openVersion`), are not covered. `SaveTests.SaveRoundTrip_ThenStepN` saves once, at tick 200, with one script, and compares only through the hash. State that is in neither the hash nor the save is invisible to it.
- Failure: each M2 system (economy, production queues, power, credits) adds scalars and lists. One missed in `Write` gives a "loaded game differs after 3 minutes" bug that no test names.
- Fix: extend the reflection test to every instance field of `Simulation` and its parts (scalars, lists, nested records), with a reasoned allowlist for scratch fields. Run the save/load comparison at many ticks (for example every 37 ticks over 2000 ticks) with combat and building scripts. Add `PlayerCount` to the save header. When: NOW. Cost: S-M.

### A-7 · Determinism scan covers only `src/Sim`, and Lua numbers are doubles · severity: COSTLY-LATER
- Evidence: `DeterminismRulesTests.SimFiles()` scans only `src/Sim`. Architecture §3 puts AI inside `Step` and §1 chooses MoonSharp, whose numbers are doubles, with "no sandboxing needed (D-019)". The analyzer has no rule for static mutable fields (it checked none exist today) or `Math.Sin/Pow`.
- Failure: AI code and the Lua host get no protection. Lua `math.random`, `os.time`, `pairs` order, `%` on doubles and float-to-int conversion all enter the sim unchecked at M4. D-019 is right about security, but determinism needs a sandbox anyway.
- Fix: extend the scan to `src/AI` and the sim-facing Scripting code. Expose only an integer API (reject non-integral numbers). Strip `os`, `io` and `math.random`, and route randomness to `SimRandom`. Add a DET rule for non-readonly statics. When: before M4. Cost: S-M.

### A-8 · Hashing does three full serialisations and gives no desync response · severity: CHEAP-WIN
- Evidence: `Step` calls `ComputeStateHash` (`Simulation.cs:123`). The sandbox's `RecordHash` calls `ComputeStateHash` plus `ComputeSubHashes`, which is two more full passes (`ReplayRecording.cs:30`). Each pass allocates a `MemoryStream` over the whole state: map layers and three id arrays at 128x128, vision per player, 2048-slot stores with path arrays. `_recentHashes` keeps hashes only, never states.
- Failure: M5 gets an allocation and CPU spike every 0.8 s, which is fine at 2 players and bad at 6 on a 128x128 map. A mismatch tells you the part name and a 16-tick window, but there is no state to diff, so you cannot find the entity.
- Fix: compute the sub-hashes once and derive the full hash from them, using streaming `XxHash64.Append` with a reused buffer. Keep a `Save()` blob at the last two agreed checkpoints, and dump both sides on mismatch. Specify the protocol (hash exchange at tick+N, then halt and dump) in a Net design note. When: before M5. Cost: S.

### A-9 · `ReplayPlayer` is O(ticks x commands) · severity: CHEAP-WIN
- Evidence: `ReplayPlayer.cs:298,303` passes the full command list to every `Step`, and `ApplyInput` scans it linearly for `ExecTick == Tick`.
- Failure: a 30-minute game is 36,000 ticks times about 10k+ commands, which is hundreds of millions of comparisons per verification. It makes a "replay all recorded games in CI" regression corpus slow.
- Fix: bucket commands by `ExecTick` and pass only that tick's slice. The LAN layer needs the same per-tick slice anyway. When: NOW. Cost: S.

## Things you checked that are fine
- Rules and TOML parsing: `RulesFields.Int` accepts only `long`, floats are rejected, and lists are ordinal-sorted by id. `Directory.Build.props` has `InvariantGlobalization=true`. There is no double-then-convert path.
- Lepton and `Fixed32` math: positions are about 32k leptons on 128 cells, distance-squared and multiplications widen to `long` first, and `ISqrt` is exact. `Fixed32` is used only in its own tests, so its wrapping behaviour is not a live risk.
- No mutable statics in `src/Sim` (only readonly tables), so two `Simulation` instances in one process are independent. The path cache stores `Expansions`, so cache hits cost the same budget and a save/load that empties the cache stays equivalent.
- `SlotArray` allocates lowest-free-first with generation checks, `PathService` results use `SortedDictionary`, RNG is an owned PCG32, and the command sort is an insertion sort that does not depend on the .NET unstable sort.
- Save/replay headers check magic, version, rules hash and map hash; the round-trip-then-step-N test exists and passes in principle.

## Unknowns
- I did not read `Occupancy`, `VisionState`, `MoveMission` or `CombatState` line by line, so I cannot rule out an unhashed scalar there (see A-6).
- I did not run the tests. "Passes" means I read what they assert.
- `src/Net`, `src/AI` and `src/Scripting` do not exist yet, so the lockstep and desync protocol is judged from the architecture doc only.
