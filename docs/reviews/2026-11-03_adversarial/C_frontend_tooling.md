# Lens C: Front-end, data pipeline, tooling and test quality

## Findings (ranked, most dangerous first)

### C-1 · The published game cannot find its own data · severity: WILL-BREAK
- Evidence: `.github/workflows/ci.yml` publish job runs `dotnet publish src/Game ... -o publish/Game` and uploads only `publish/Game`. `src/Game/Game.csproj` has no `Content`/`None` item that copies `data/` to output. `RulesLocator.FindRulesDir` (src/Data/RulesLocator.cs) walks up from `AppContext.BaseDirectory` looking for `data/rules`, and `RtsGame` ctor throws `InvalidOperationException` when it is missing (src/Game/RtsGame.cs:70-71).
- Failure: the `RTS_2027-win-x64` zip starts and closes with no visible error for anyone outside the repo tree (WinExe output, no console). It bites at the first tester build, M1 onward. The sprite sheet path (`data/sprites`) has the same problem.
- Fix: copy `data/**` into the publish folder (csproj `None` item with `CopyToOutputDirectory`, or a publish step). Add a crash handler that writes a log and shows a message box. Add a CI smoke step that runs the published exe from an empty folder with `--headless-check`. When: NOW. Cost: S.

### C-2 · The sim is stepped from the render loop on wall-clock time · severity: WILL-BREAK (M5 LAN), COSTLY-LATER (AI, campaign)
- Evidence: `SimSnapshotSource.Update` (src/Game/Sandbox/SimSnapshotSource.cs:124-142) accumulates `Stopwatch` time and calls `AdvanceOneTick` from `RtsGame.Update` (RtsGame.cs:170). `Order()` hardcodes `Tick + 1` (line 166), and `Enqueue` drops any command with `execTick <= Tick` (line 175). `SceneRenderer` also reads `_sim.Sim` directly for the occupancy overlay (SceneRenderer.cs:29-31), so the renderer is not only a snapshot reader.
- Failure: lockstep needs the sim to advance only when every peer's commands for that tick have arrived. A frame-driven clock with a 250 ms catch-up cap cannot wait, and it silently skips ticks after a stall, so peers diverge. AI and Lua would run at render cadence, and pausing/stepping is tied to the window. This surfaces when M5 starts, but the retrofit touches every caller of `Step`.
- Fix: add a `SimHost` (fixed-step driver with no `Stopwatch`) that owns `Step`, the pending command queue and the advance policy. Make the sandbox one driver of it. Renderer and overlays read only snapshots (copy the occupancy data into the snapshot or an overlay DTO). Before M2 for the split; cost: M.

### C-3 · No screen or state stack; input, drawing and mouse routing are hard-wired to the sandbox · severity: COSTLY-LATER (M2 sidebar, M3 menus)
- Evidence: `RtsGame.Update` (RtsGame.cs:115-189) and `Draw` (192-215) handle keys, camera, selection, orders and HUD inline. `OrderController` is built from `SimSnapshotSource` (line 83). `HandleMouse` (220-258) assumes the whole window is map, and Esc calls `Exit()` directly (121-124).
- Failure: a sidebar, minimap and menus all need input capture regions and a screen stack. Each feature will be grafted onto this one method, and right-click/drag will fire through panels.
- Fix: `IScreen { Update(InputFrame); Draw(SpriteBatch) }` with a stack and an input router that gives UI first refusal. Move the sandbox into a `SandboxScreen`. Before M2; cost: M.

### C-4 · Sprite pipeline makes a full CPU-recoloured copy per house, and is hard-wired to one unit type · severity: COSTLY-LATER (M2 art), with a VRAM risk
- Evidence: `SpriteSheet.Load` (src/Game/Sprites/SpriteSheet.cs:89-99) builds `PlayerColours.Count` (=4, PlayerColours.cs:16) full texture copies. Every vehicle is drawn with the single `tank_v2.png` (RtsGame.cs:26; UnitRenderer.cs:51-52). Facings must be 8/16/32 and equal `columns*rows` (SpriteSheet.cs:58), so animation states need separate sheets. There is no `Dispose`. `Texture(player)` wraps with modulo (line 105), so LAN with 5+ players reuses colours. Architecture §11 says house colours come from a shader lookup, which the code does not do.
- Failure: estimate using the spec's 32 facings × 8 frames at 96 px: 256 × 96² × 4 B ≈ 9.4 MB per copy, × 4 houses ≈ 38 MB per vehicle type. Forty types ≈ 1.5 GB of VRAM before infantry, buildings or effects. The estimate is mine, not measured.
- Fix: one palette-indexed atlas per type (R8 index texture plus a 256-entry palette texture and a small effect shader). Pack animation states into one atlas with a sprite-manifest TOML. Make the house-colour count at least 8 and map by player slot. Before M2 (art pipeline); cost: M.

### C-5 · The test corpus is the shipped data, so balance and content work will break tests · severity: COSTLY-LATER
- Evidence: `data/rules/vehicles.toml` is headed "placeholder fixture, not balance" and holds `test_*` ids. `RulesValidationTests.cs:97-103` asserts `Assert.Equal(5, rules.Sim.Vehicles.Count)`. About 20 `[InlineData]` cases (lines 10-29) find and replace exact fixture text with `TempRules.RealWith`, so a changed value or added line breaks them.
- Failure: the first real unit added to `vehicles.toml` breaks the count test. The first tuned `max_health` breaks validation tests. M2 content work will fight the suite, and the tests will be "fixed" by loosening them.
- Fix: move fixtures to `tests/Sim.Tests/Fixtures/rules` as their own versioned copy. Keep `data/rules` for real content. Validate real data with the `validate` CI step plus a schema test that checks ids and keys, not values. Before M2; cost: S.

### C-6 · Crashes lose the replay, and players get no log · severity: COSTLY-LATER (bug reports from M2)
- Evidence: `Program.Main` has no try/catch (src/Game/Program.cs:15-24). The replay is written only from `Exiting` (RtsGame.cs:84) and overwrites `replays/sandbox_last.rpl`. Any exception skips it. `SimSnapshotSource.SaveReplay` also prints a dev command (`VerifyCommand`) to a console that a WinExe player never sees.
- Failure: once desyncs exist, the first bug report will have no replay. The last session's replay is also overwritten on every launch.
- Fix: a top-level handler that writes `logs/crash-<time>.txt` (exception, last 100 commands, tick, rules hash). Make the replay writer incremental (flush every N ticks) so a crash keeps the recording. Now; cost: S.

### C-7 · Interpolation covers only unit position; snapshots and per-frame maps are rebuilt wholesale · severity: COSTLY-LATER (perf at M2 scale)
- Evidence: `UnitInterpolator.Build` (src/Game/Render/UnitInterpolator.cs:16-35) allocates a new `Dictionary` every frame over all units. `AdvanceOneTick` (SimSnapshotSource.cs:233-243) and `Snapshot(viewer)` (src/Sim/Simulation.cs:531+) allocate new lists per tick. `CaptureProjectiles` allocates per tick, and projectiles are not interpolated (line 103). Facing and health are not interpolated, so a turret or hull turn steps 16 facing units per tick at 20 Hz.
- Failure: with a few hundred units at 60 fps, the per-frame dictionary and list churn creates GC pauses that show as stutter. Turning looks jerky.
- Fix: preallocated arrays indexed by slot, shortest-arc lerp for facing, and projectile interpolation. Add a 500-unit benchmark fixture and log ticks/s. Before M2; cost: S-M.

### C-8 · Fixed window, bitmap-only text, ASCII only, no UI scale · severity: COSTLY-LATER (M2 sidebar, M4 briefings)
- Evidence: preferred backbuffer is fixed at 1280×720 (RtsGame.cs:65-66), with `AllowUserResizing = true` (line 92) but no UI scale. `HudScale = 2` is a constant (line 24). Text is a 5×7 embedded font for ASCII 32..126 only; everything else draws `?` (src/Game/Ui/BitmapFont.cs:12-13). The code says "No content pipeline" (RtsGame.cs:105).
- Failure: sidebar labels (names, costs) in a 5×7 font are unreadable at 1440p or on a scaled display. Accented or CJK translations draw as `?`. Campaign briefings need real fonts.
- Fix: adopt the MonoGame content pipeline with `SpriteFont` for UI, or a generated atlas from a TTF. Add a UI scale factor from backbuffer height. Add a per-locale string table with keyed ids. Before M2; cost: M.

### C-9 · No settings, no config file, no audio plan in code · severity: COSTLY-LATER (M2-M4)
- Evidence: the only inputs are CLI flags (Program.cs:9-10, 17-20). Resolution, zoom levels (Camera.cs:16), seed (SimSnapshotSource.cs:34), replay path (line 28) and the sprite file (RtsGame.cs:26) are constants. `data/` has only `maps`, `rules` and `sprites`. Architecture §2 lists `data/audio/`, but it does not exist, and nothing references `SoundEffect` or `Song`.
- Failure: volume, key bindings, fullscreen, language and difficulty all need persistence by M2-M4. With constants in code, every addition is a code change. Audio called ad hoc from game code will not have a mixer or a null backend for tests.
- Fix: `settings.toml` under the user's app-data folder, with a versioned `Settings` class, defaults, and tolerant load. Key bindings as data. An `IAudio` interface with a null implementation now. Before M2; cost: S.

### C-10 · Tests check too little: disjunctive asserts, wall-clock perf bound, no Game-layer tests · severity: CHEAP-WIN
- Evidence: `CombatTests.cs:76` asserts `projectiles > 0 || health < 50`. `CombatTests.cs:174` asserts `health < 300 || projectiles > 0`. Both pass if either branch holds. The scripted battle test (CombatTests.cs:309-318) asserts only `casualties > 0` and seed sensitivity, with no expected outcome. `VisionPerformanceTests.cs:81-84` asserts a 2000 ms wall-clock bound, which is flaky on CI and says nothing about trends. No test covers `Camera`, `UnitInterpolator`, `Selection`, `SpriteSheet` validation or `BitmapFont`, all of which are pure logic.
- Failure: a sim regression that moves a number from 40 to 39 passes. Pure front-end logic changes go unseen. A slow CI runner breaks the perf test without any real regression.
- Fix: replace disjunctions with exact expected values (the sim is deterministic, so exact is available). Commit one golden replay with its hash list, so every sim change is explicit. Add a `Game.Tests` project for the pure classes. Log ticks/s instead of asserting time. Now; cost: S.

### C-11 · Dev tooling gaps for a hobbyist · severity: CHEAP-WIN
- Evidence: `CommandKind.Debug` exists in the sim (DeterminismTests.cs:48), but nothing in `src/Game` issues one, and there is no spawn, credits or reveal key. Rules are loaded once in the ctor (RtsGame.cs:80), with no reload. Map tooling is `map new` and `map info` only (MapCommands.cs:21-22). `Tools run` prints "sim not implemented yet" (Cli.cs:173-175).
- Failure: balancing M2 means restarting per edit, and testing AI at "hard" means playing it. Bug reports cannot be reproduced on demand.
- Fix: a debug console (F1) with spawn, credits, reveal and speed, issuing `Debug` commands that are recorded in replays. Reload TOML only between ticks in the sandbox, and refuse reload during replay. Now for the console (S); reload is M.

### C-12 · Builds have no version or build id, and CI tracks no perf trend · severity: CHEAP-WIN
- Evidence: `Directory.Build.props` sets no `Version` or `SourceRevisionId`. A grep of `src` and `tests` for build hash, version or GitVersion finds nothing written to replay headers, though architecture §5 requires a build hash there. The publish zip is named `RTS_2027-win-x64` with no version or SHA. The match command prints ticks/s (MatchCommands.cs:118), but CI does not record it.
- Failure: a replay from a tester's build cannot say which build wrote it. A perf regression from M2 AI or pathing is first noticed by players.
- Fix: stamp `<Version>` and the git SHA in `Directory.Build.props`, write them into the replay header, put the zip version in the artifact name, and write match ticks/s to the job summary with a generous floor. Now; cost: S.

## Things you checked that are fine
- `RulesLoader` collects every problem across files with file and key (RulesLoader.cs:48-98), and the validation tests show it.
- The DET001-DET005 Roslyn analyzer runs inside the test suite (tests/Sim.Tests/Determinism/DeterminismRules.cs), which covers float, clocks, threading and unordered iteration.
- The renderer already interpolates unit positions between the last two ticks and draws at its own frame rate (UnitInterpolator.cs). It is not tied to the tick rate.
- CI runs `replay verify` in a separate process after the headless match (ci.yml), which checks more than same-process determinism.
- `Directory.Build.props` enables nullable, warnings-as-errors and invariant globalization.

## Unknowns
- I did not build or run anything, so the VRAM figure (C-4) and the frame-time cost (C-7) are estimates from the spec and the code, not measurements.
- I read `Simulation.Snapshot` only partly, so I did not measure its copy cost.
- MonoGame 3.8.6 DPI behaviour on Windows was not verified.
- I checked `Game.csproj` and `Directory.Build.props` for a data copy rule. Some other mechanism (a `.targets` file, a solution-level item) could exist; none appeared in the files I read.
- I did not read `docs/research/*`, ROADMAP, or the full Tools CLI.
- Test files read: DeterminismTests, RulesValidationTests, CombatTests (plus the test helpers and `VisionPerformanceTests` line range). The other folders were listed but not read.
