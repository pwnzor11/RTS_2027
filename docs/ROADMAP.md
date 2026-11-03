# Roadmap
Milestones in build order. Each ends with something the owner can try. Ids are stable; tick when done.

## M0 · Foundations (docs, research, design)
- [x] M0-01 Handoff doc set in `docs/`
- [x] M0-02 Clone EA C&C Remastered source as read-only reference
- [x] M0-03 Research: C&C Remastered code study (`docs/research/01..06_cnc_*.md`)
- [x] M0-04 Research: RTS engine architecture (`docs/research/10_rts_architecture.md`, `11_pathfinding_and_movement.md`, `15_technology_options.md`)
- [x] M0-05 Research: map making and map formats (`docs/research/12_map_making.md`)
- [x] M0-06 Research: resource/economy systems (`docs/research/13_resource_systems.md`)
- [x] M0-07 Research: RTS AI (skirmish AI, difficulty, campaign scripting) (`docs/research/14_rts_ai.md`)
- [x] M0-07b Research: other open-source RTS repos (openage, freeaoe, ageServer, AOEMods.Essence, aoc-mgz) (`docs/research/2x_*.md`)
- [x] M0-07c Research synthesis and design-round agenda (`docs/research/00_SYNTHESIS.md`)
- [x] M0-08 Design round 1: technology, engine/language, sim architecture — user can try: nothing yet; produces `docs/design/`
- [x] M0-09 Design round 2: game design (factions, economy, tech tree shape, map scale)
- [x] M0-10 Design round 3: setting, campaign structure, tooling, art, LAN scope
- [x] M0-10b Design docs: `docs/design/02_game_design.md`, `03_setting_and_factions.md` written from D-020..D-034
- [x] M0-12 Art pilot: one vehicle three ways (Blender render, AI-assisted frames, hand pixel) at 2 candidate cell sizes + house-colour remap test — user can try: look at a contact sheet and pick
- [x] M0-11 Repo skeleton: build system, CI, LICENSE, CLAUDE.md pointing at docs — user can try: clone + build an empty window

## M1 · Core simulation (no graphics beyond debug view)
Goal: a headless, deterministic sim with terrain, pathfinding, movement, combat, fog and building
placement, driven by placeholder data, plus a debug view that runs the real sim. Economy, production
and AI are M2. Planned in session 2; expect two sessions. Lead writes each wave's contracts first.

**Wave A — foundations (parallel)**
- [x] M1-01 Map model: cell layers per design 02 §3 (template, land type, height, overlay resource +
  density, smudge, occupancy bits incl. 5 infantry sub-slots), map file (TOML header + packed cell
  data), `terrain.toml` land × locomotion speed %, map hash in replays/saves; Tools `map new` makes
  synthetic test maps — Sonnet — user can try: `Tools map new --size 64` then `Tools map info`
- [x] M1-02 Rules schema v1: units, infantry, buildings, weapons, warheads, armour matrix, `base`
  inheritance, prerequisites/provides, effect lists; placeholder data marked as fixtures; rules hash
  covers all — Haiku — user can try: `Tools validate` lists every type
- [x] M1-03 Mission framework: Mission/Status/WakeTick/NextMission per actor, AssignMission →
  Commence at the safe point, handlers return sleep ticks; entity kinds Unit, Infantry, Building,
  Projectile in slot arrays — Sonnet — user can try: tests only
- [x] M1-04 Debug view on the real sim: Game runs `Simulation` at 20 Hz with interpolation, loads a
  map, built-in bitmap font for tick/hash/FPS, click-select and right-click move — Haiku —
  user can try: open the window, move units around

**Wave B — space (parallel, needs A)**
- [x] M1-05 Pathfinding: `IPathfinder`, 8-way A* with terrain cost per locomotion, per-tick expansion
  budget, cache keyed on map version, shared group search — Sonnet
- [x] M1-06 Building placement: footprints, buildable check, one-cell gap rule, occupancy, Place/
  Sell commands (no economy yet) — Sonnet
- [x] M1-07 Fog and shroud: per-player visibility bits, incremental update, last-seen building
  ghosts, snapshot respects the viewing player — Haiku

**Wave C — action (parallel, needs B)**
- [x] M1-08 Movement and locomotion: follow paths, turn rates, blockage tiers (wait / sidestep /
  shove idle allies), infantry sub-cells, crushing — Sonnet
- [x] M1-09 Combat: weapon → projectile → warhead × armour, range/ROF in leptons/ticks, turret facing,
  splash (impact cell full damage, surrounding cells half, radius from the warhead), Attack / AttackMove / Guard / Stop missions, death and cleanup — Sonnet
- [x] M1-10 Desync tooling: per-subsystem sub-hashes, `Tools replay verify`, `Tools match --ticks N`
  headless soak (two scripted armies), hash coverage test that fails if a new sim array is unhashed — Haiku

**Wave D — together**
- [x] M1-11 Debug overlays (paths, occupancy, fog, ranges) and a "battle sandbox" map: two armies
  meet in the window, replay saved and verified — Haiku + lead review — user can try: watch a fight,
  then `Tools replay verify` on the saved replay
- [x] M1-ART-01 Tank polish round on the D-039 pipeline: bigger house panel, camera ~40°, more hull/wheel
  detail; becomes the reference sprite for fan-out — user can try: look at the updated sheet

## M1.5 · Hardening (D-052, from docs/reviews/2026-11-03_adversarial)
Planned in session 3. User can try at the end: `Tools ai matrix` prints a tier win-rate table; the
published zip starts from any folder; F1 opens a debug console in the sandbox.
- [x] M1.5-01 Player relations (Self/Ally/Enemy/Neutral, fixed teams, neutral slot), shared allied vision, `RulesOf(player)` for every type lookup, one actor-store view replacing the per-kind switches (review B-1..B-3) — user can try: allied sandbox units ignore each other
- [x] M1.5-02 Sim boundary guards: mutators only at setup, total command order, late-command counter, command player validation, `AllowDebug` + debug ops, store-full handled without throwing mid-Step (A-4, A-5, B-4)
- [x] M1.5-03 Saves and replays that survive: replay v6 header (setup, teams, limits, build, embedded rules), streamed writing that survives a crash, spawns from the sim's setup log, per-tick playback, full-field hash coverage, many-tick save/load test, single-pass hashing (A-2, A-3, A-6, A-8, A-9)
- [x] M1.5-04 Publish and licence: data shipped with the build, crash handler + log, published-exe smoke test in CI, licence/notices/source offer in the zip, CREDITS libraries, README, .gitignore, version + SHA stamping, CI LFS checkout and AI matrix summary (C-1, C-6, C-12, E-1, E-3, E-4, E-7)
- [x] M1.5-05 Tests: fixtures split from `data/rules`, exact asserts, golden replay with regeneration switch, perf logged not asserted (C-5, C-10)
- [x] M1.5-06 AI host and AI-vs-AI harness: `src/AI`, fog-filtered `PlayerView`, `IAiPlayer`, `AiDriver`, `data/ai/difficulty.toml` (5 tiers), placeholder skirmisher AI, `Tools ai match` / `Tools ai matrix` (D-051, D-4/D-5)
- [x] M1.5-07 In-game debug console (F1): spawn, cash, kill, reveal, speed — recorded `Debug` commands (C-11)
- [x] M1.5-08 Lead: GPL §7 MonoGame permission (D-055), golden regeneration, docs, SPDX headers + enforcing test (D-060). Git LFS conversion done by the owner from Windows (USER_CHECKS S3 item 7).

## M2 · Playable skirmish slice
Plan: docs/design/04_m2_plan.md (D-053, D-058, D-059, D-061, D-062). User can try: menu → skirmish vs the Recruit
AI on a Rust Belt map, a 10-minute game with real art for the Authority slice.
- [x] M2-01 Corner advantage on generated maps — user can try: `Tools ai match --a skirmisher --b skirmisher` with swapped starts ends near-even
- [x] M2-02 Rules schema v2 + M2 contracts (economy/production fields, parts, commands, HUD)
- [x] M2-03 Front-end skeleton: SimHost, screen stack, input router, menus — user can try: main menu → sandbox
- [x] M2-04 UI font from an OFL TTF + UI scale
- [x] M2-05 House colours on the GPU (base + mask layers), sprite manifest
- [ ] M2-06 Art pilot: Refinery — building approved (D-064); construction/damage states redone in M2-06b, owner approves again
- [x] M2-07 Economy sim (harvesters, depletion, regrowth, storage, power)
- [x] M2-08 Production sim (queues, prerequisites, drain/stall, placement radius, rally, sell, repair, low power, win rule)
- [x] M2-09 Authority roster data (D-058) and id migration
- [x] M2-10 Rust Belt M2 map
- [ ] M2-11 Sidebar and minimap
- [ ] M2-12 Recruit-tier AI
- [ ] M2-13 Art fan-out (12 types)
- [ ] M2-14 Integration, headless skirmish in CI, balance pass
- [ ] M2-15 Campaign pilot (D-061)
- [ ] M2-16 Simultaneous damage resolution (D-065)
- [ ] M2-17 Buildings and units drawn from sprites, placeholders for missing art

## M3 · Full skirmish
Both factions, full tech tree, map editor, real AI with difficulty levels, audio.

## M4 · Campaign
Mission scripting/trigger system, briefing screens, tech gating, campaign flow, both campaigns.

## M5 · LAN multiplayer
Lockstep over LAN, lobby, desync detection, replays.

## M6 · Polish
Art pass, music/SFX, balance, installer.
