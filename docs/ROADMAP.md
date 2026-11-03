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
- [ ] M1.5-01 Player relations (Self/Ally/Enemy/Neutral, fixed teams, neutral slot), shared allied vision, `RulesOf(player)` for every type lookup, one actor-store view replacing the per-kind switches (review B-1..B-3) — user can try: allied sandbox units ignore each other
- [ ] M1.5-02 Sim boundary guards: mutators only at setup, total command order, late-command counter, command player validation, `AllowDebug` + debug ops, store-full handled without throwing mid-Step (A-4, A-5, B-4)
- [ ] M1.5-03 Saves and replays that survive: replay v6 header (setup, teams, limits, build, embedded rules), streamed writing that survives a crash, spawns from the sim's setup log, per-tick playback, full-field hash coverage, many-tick save/load test, single-pass hashing (A-2, A-3, A-6, A-8, A-9)
- [ ] M1.5-04 Publish and licence: data shipped with the build, crash handler + log, published-exe smoke test in CI, licence/notices/source offer in the zip, CREDITS libraries, README, .gitignore, version + SHA stamping, CI LFS checkout and AI matrix summary (C-1, C-6, C-12, E-1, E-3, E-4, E-7)
- [ ] M1.5-05 Tests: fixtures split from `data/rules`, exact asserts, golden replay with regeneration switch, perf logged not asserted (C-5, C-10)
- [ ] M1.5-06 AI host and AI-vs-AI harness: `src/AI`, fog-filtered `PlayerView`, `IAiPlayer`, `AiDriver`, `data/ai/difficulty.toml` (5 tiers), placeholder skirmisher AI, `Tools ai match` / `Tools ai matrix` (D-051, D-4/D-5)
- [ ] M1.5-07 In-game debug console (F1): spawn, cash, kill, reveal, speed — recorded `Debug` commands (C-11)
- [ ] M1.5-08 Lead: GPL §7 MonoGame permission (D-055), SPDX headers, Git LFS for sprite sheets (D-056), golden regeneration, docs

## M2 · Playable skirmish slice
- [ ] M2-01 Corner advantage on generated maps: headless match on test_64 favours the (4,4) start whichever player has it (mirror duels are symmetric) — find the geometric cause (path tie-breaks, map features) and fix — user can try: `Tools match` both seeds end near-even
Rendering with placeholder sprites, UI (sidebar, selection, minimap), economy loop (harvester →
refinery → credits → build), one faction, one map, dumb AI. User can try: a 10-minute game.
Scope fixed in D-053: Authority faction, Rust Belt map, AI framework with the Recruit tier only.
Before planning: owner questions on the Authority M2 roster, a content budget (review D-2), and the
Directorate tunnel spec (D-3) logged in OPEN_QUESTIONS.

## M3 · Full skirmish
Both factions, full tech tree, map editor, real AI with difficulty levels, audio.

## M4 · Campaign
Mission scripting/trigger system, briefing screens, tech gating, campaign flow, both campaigns.

## M5 · LAN multiplayer
Lockstep over LAN, lobby, desync detection, replays.

## M6 · Polish
Art pass, music/SFX, balance, installer.
