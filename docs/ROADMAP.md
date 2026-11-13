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
- [x] M2-06 Art pilot: Refinery — building approved (D-064); construction/damage states redone in M2-06b, owner approves again
- [x] M2-07 Economy sim (harvesters, depletion, regrowth, storage, power)
- [x] M2-08 Production sim (queues, prerequisites, drain/stall, placement radius, rally, sell, repair, low power, win rule)
- [x] M2-09 Authority roster data (D-058) and id migration
- [x] M2-10 Rust Belt M2 map
- [x] M2-11 Sidebar and minimap
- [x] M2-12 Recruit-tier AI
- [x] M2-13 Art fan-out: all 13 D-058 types have real art (Rifleman, Refinery, effects approved; 7 structures + Rocketeer awaiting owner review)
- [x] M2-14 Integration, headless skirmish in CI, balance pass
- [x] M2-15 Campaign pilot (D-061)
- [x] M2-16 Simultaneous damage resolution (D-065)
- [x] M2-19 Sim fixes from the balance pass: speed-16 scraper never harvests, harvester field search radius, projectile hits a whole infantry cell, explosion events for effects
- [x] M2-17 Buildings and units drawn from sprites, placeholders for missing art

## M3 · Full skirmish
Both factions, full tech tree, map editor, real AI with difficulty levels, audio. Plan: docs/design/05_m3_plan.md.
- [x] M3-01 Contracts: factions, schema v3, enums, state parts, payloads, view/HUD
- [x] M3-02 Tunnel network transit
- [x] M3-03 Burrow locomotion, surfacing telegraph, Bore/Hatch/Bore Head, land-train deploy
- [x] M3-04 Hardening (grouting), gates, team-aware pathing
- [x] M3-05 Radiation, decon, Blackout, Seismograph, Searchlight, stealth, jam
- [x] M3-06 Upgrades, auras, transports, air, death weapon, engineer
- [x] M3-07 Directorate + Authority-counter data
- [x] M3-08 Game: faction pick and new renderers
- [x] M3-09 Audio host, SFX generator, music player and brief
- [x] ART-S6 (done session 8: session-4/5 art on main via PR #13 with companion turret/shadow sheets D-114; reworks via PR #14, D-116/D-117) Session-5 art to main once the owner pushes the session-5 bundle (D-084); owner reworks (D-099): Seismograph drum,
  Searchlight lens, Assembly Bore roof, Hatch cage, auth_gate open state; one radiation-green ramp in PAL_DIR for the rad mortar (Q-35)
- [x] M3-ART Art fan-out (session 5, lead-reviewed per D-088/D-091; owner review pending): turret rework + dir_shaft rev 2 owner-approved; 14 Directorate structures, auth_seismograph/searchlight/gate/engineering, agitprop turret, Sunrise, rad mortar rev 2. Art is on local `integ-art` until the owner runs push_art.ps1 (D-084). Effects art not done → M10
- [x] M3-10 Directorate AI (Recruit tier; 4 test games vs Authority all draws; one racy test skipped → fix in M3-11)
- [x] M3-11 AI tiers 2–5 (session 5)
- [x] M3-12 Session 3 follow-ups (mission replays not done → M3-19; wall art 1× and Rocketeer flash crop → M3-ART)
- [x] M3-13 Superweapon: the Sunrise Device (sim + UI pieces; GameScreen wiring → M3-18)
- [x] M3-14 Integration and balance pass (docs/balance; tunnel time saved 42–46 %; faction/tier targets not met → M4-16) s14: per-faction raid key (D-167); mop-up measured and shipped off; draws are base stalemates (Q-64).
- [ ] M3-15 Map editor → moved to M4 (D-087)
- [x] M3-16 Main menu lobby: illustrated animated background, full menu (Continue, Campaign new/load, Skirmish new/load, Replays, Archive, How to play, Settings, Quit; LAN and Map editor greyed until built)
- [x] M3-17 Archive: screens, unlock tracking, approved lore in data/archive (wiring into GameScreen/menu → M3-18; Technology entries still stubs)
- [x] M3-18 Game wiring left by wave 2: sprite turret/shadow/dive layers in the renderer, Sunrise button + targeting + flash in GameScreen, ArchiveTracker.Observe in GameScreen, a Hatch → Bore Head button (sim ApplyUpgrade exists; no UI issues CommandKind.Upgrade)
- [x] M3-20 Menu back-ends: in-game save/load for skirmish and campaign (rebuild AI + script state around a loaded sim) so
  Continue/Load work; a replay viewer behind Replays; UI sound ids (ui_move/ui_select/ui_back/ui_deny) in sounds.toml;
  check building manifests' [house] ramps (menu needed HouseRamp.Standard)
- [x] M3-19 Mission replays (D-092) (record mission id + script hash; replay re-runs the script; ReplayPlayer hook for PlaceInitialActors)
- [x] M3-21 Follow-ups found in session 5 (done session 6, PR #11; open-gate ART still placeholder → ART-S6): turret facing drawn in the view, dive flag in the view, Sunrise blast radius in the view, AI save/restore (AI restarts fresh after load, D-093), gate open/closed state + auth_gate open art, Sunrise ready state
- [x] M3-INT Wave-1 integration gaps (underground vision, Blackout fog, jam/seismic emitter cache, air vs placement, engineer right-click, audio listener + select sound, CREDITS)

## M4 · Skirmish depth (D-087)
Equal priority: procedural maps (Rust Belt ruins, Glass Coast dunes, Mountain passes, Ash plains/rivers; S/M/L), AI-vs-AI
spectator mode, garrisoning, three-tier tech trees and upgrades for both factions, the map editor (was M3-15), fill out
the Authority and Directorate rosters creatively (no quota).
Plan: docs/design/08_m4_plan.md (D-094, D-095). User can try: Skirmish → 4 slots, Biome/Size picker, all-AI slots → Spectate;
Main menu → Map Editor.
- [x] M4-01 Contracts: schema v4, enums, state parts, format bumps (D-094)
- [x] M4-02 Procedural generator: 4 biomes × S/M/L, 2–4 players, `MapValidator`, `map gen` / `map validate`
- [x] M4-03 Garrison sim
- [x] M4-04 Per-player rules, tiers, upgrade prerequisites, Aircraft lane
- [x] M4-05 Warhead mechanics + Quick-Set Foam
- [x] M4-06 Stats part
- [x] M4-07 Data: tiers, gate structures, +14 Authority / +7 Directorate types, 13 upgrades, neutral `neu_*`
- [x] M4-08 Spectator front end
- [x] M4-09 Garrison + tier UI (manual checks in USER_CHECKS session 5)
- [x] M4-10 Theaters: Glass Coast, Mountains, Ash Plains palettes (colours ASSUMED)
- [x] M4-11 Skirmish setup v2
- [x] M4-12 AI: 2–4 players, tier climbing, garrisons, stall fixes (docs/balance/m4_ai.md; ladder ≥60 % and 4-player fairness NOT met)
- [x] M4-13 Map editor + main menu entry
- [x] M4-14 RulesOf sweep
- [x] M4-15 Neutral map features (ruins)
- [ ] M4-16 Integration + balance. Session 13: tier 4 beats tier 3 in both factions (air tech and raiders wait for an army, commissar holds; D-165, docs/balance/s13_ladder.md); 5 v 1 at 68-69 %, not 90 %; Directorate draws (mop-up behaviour next). Session 12: research how the reference repos tier their AI (docs/research/25_ai_difficulty.md). Session 8: round-3 diagnosis in docs/balance/m4_16_round3.md (main cause: higher tiers launch small waves on a timer into a bigger home army; tech spending second; round-2 2 v 1 inversion was sampling noise; 64 games per cell from now on); fixes in progress on `wp/m4-16r3-wip`, not landed. Session 7: round-2 re-measure in docs/balance/m4_16_round2.md (ladder flat or inverted, not met). Session 6 (PR #11): start-bias sim fixes + `ai match` per-game seeds (D-104); AI round 1 (D-105:
  finish won games, 4-player targeting; tech hold off); rules garrison counters + Seismic Mortar (D-105); Faction.Neutral (D-100);
  8× spectator benchmark (headless 1 000 ticks/s, fine). **Still open:** re-measure the tier ladder (target ≥ 60 %), cross-faction,
  Directorate veteran mirror and 4-player matrix on the fixed starts; mirror-symmetric AI placement + seeded placement variation
  (reverted, stalemates mirrors); tier-cost proposal (Q-43); foam area (Q-42); AI use of engineers/transports; per-tick snapshot
  allocations; walking sub-slot handover and NW-first `Simulation.Spread` (residual asymmetries)

- [x] M4-17 Skirmish defeat rule: no production, no builder, no harvester for 2 min = defeated (D-173, s14)
## M5 · LLM commander (D-087)
Built-in AI does micro; an LLM sets strategy every 30 s (D-103) and talks in character as a faction leader. API key in Settings
(Anthropic or OpenAI-compatible), local only. LLM-vs-LLM spectating. Plan: docs/design/09_m5_plan.md (approved, D-103).
- [x] M5-00 src/Llm project + contracts (src/Llm/Contracts.cs)
- [x] M5-01 Sim contracts: CommandKind.Directive = 23, directive state part + hash, PlayerView.Directive, pack/unpack (session 7; save format 9, D-109)
- [x] M5-02 DirectiveBias in the tiered AI (session 7: behaviour tests show each field changes play; plain game unchanged)
- [x] M5-03 Providers (Anthropic, OpenAI-compatible), fake provider, reply parser
- [x] M5-04 Fog-honest battlefield summary + SummaryHistory
- [x] M5-05 Commander loop: 30 s cadence, apply-at-tick, drop stale, fallback to plain tier; wire LlmHooks.TestConnection (session 7)
- [x] M5-06 Personas from docs/lore/leaders.md (data/llm/personas.toml, 8 leaders; voices assumed — confirm, D-112)
- [x] M5-07 Chat panel + player→commander chat (talkback, flavour only) (session 7; Enter, /to N)
- [x] M5-08 LLM settings, DPAPI key store, settings page (model names: Q-40 answered session 7, D-108)
- [x] M5-09 Setup + spectator: LLM kind per slot, LLM-vs-LLM (session 7; save header v4)
- [x] M5-10 Replay/save tests with recorded directives; owner play-test (tests done session 7, loaded saves rebuild commanders; owner play-test with a real key open) — owner ran it with gpt-5.4-mini in session 12: 21/21 directives ok
- [x] M5-11 Headless commander match (`ai match` with an LLM commander slot, key from an environment variable) for the owner's OpenAI test (D-153 (12))
- [ ] M5-12 Stronger commander directives (design note docs/design/15_commander_directives.md, D-161): later milestone, not scheduled

## M6 · The Left Behind (D-087)
Third faction: Glass Coast evacuees abandoned in 1990; nomadic scavengers, mobile crawler-rig bases, salvaged tech, raids.
Lore (owner approves), roster, art (pilot → fan-out, D-088), AI.
- [x] M6-01 Design doc + lore draft (docs/design/06_left_behind.md, docs/lore/left_behind.md — DRAFT, owner approves)
- [x] M6-02 Plan (docs/design/10_m6_plan.md) + contracts: Faction.LeftBehind, CommandKind.Pack, missions, rules fields (off), "rigs" and "salvage" parts, effects v2, save/replay format 10 (D-118; PR #15)
- [x] M6-03 Rig pack/unpack (session 9; one generic packed vehicle, unpack via PackedFrom)
- [x] M6-04 Wrecks and salvage (session 9)
- [x] M6-05 Raiders (plunder, withdraw, spoils) (session 9)
- [x] M6-06 Ash stealth, Blackout trail, repair and tow auras, upgrade hooks (session 9)
- [x] M6-07 The Second Drift storm (session 9)
- [x] M6-08 Faction data (roster, structures, upgrades, tiers, start kit) + Archive entries and personas (session 9)
- [x] M6-09 Game layer (faction pick, sidebar, Pack button, wreck/storm rendering)
- [x] M6-10 AI Raider Warlord (docs/balance/m6_10_ai.md; Second Drift use and Kites not measured)
- [x] M6-11 Art: pilot + fan-out, 14 structures + 16 units (on `wp/art-lb*`, reaches main after the owner pushes it, D-084)
- [x] M6-12 Acceptance (soak, golden replays, balance table to the owner) — session 10: 216-game soak 0 desyncs, LB golden replay, income tables (docs/balance/m6_12_acceptance.md); LB balance → Q-52

## M7 · The Overseas Expedition (D-087)
Fourth faction: an old-world remnant (fictionalised; no real people or politics). Naval and air, beachheads. Needs naval
systems (water locomotion, ships, landings). Lore, roster, art, AI.
- [x] M7-01 Design doc with naval contract sketch + lore draft (docs/design/07_expedition.md, docs/lore/expedition.md — DRAFT)
- [x] M7-PLAN M7 plan (docs/design/11_m7_plan.md), supply-cap design (13_supply_cap.md), veterancy design (14_veterancy.md) (D-130, session 9)
- [x] M7-LORE Archive entries for the whole Expedition roster (session 9, assumed — confirm)
- [x] M7-VET Generic veterancy (all factions; VET-1..4 sim+data, VET-5 game, VET-6 AI) — VET-1..7 done (session 10; VET-7 table docs/balance/s10_tables.md, rank-3 reach → Q-53)
- [x] M7-SUP Supply cap (all factions; SUP-1..6) — SUP-1..6 done, supply ON by default (D-135; SUP-6 table docs/balance/s10_tables.md, spread → Q-54)
- [x] M7-02 Naval contracts (11_m7_plan.md) — session 10, PR #21 (D-137)
- [x] M7-03 Naval locomotion, sea regions, unreachable-goal refusal, draw scale (session 10, D-138)
- [x] M7-06 Home shipments: tenders, decay, charter, stats (session 10, D-139; off until Expedition data)
- [x] M7-07 Beachhead zone, water-building placement (session 10, D-140; zone overlay not drawn yet)
- [x] M7-04 Landing and unload, Skyhook, vehicle passengers (session 10, D-144, D-147)
- [x] M7-05 Submerge and sonar incl. building sonar (session 10, D-143, D-147)
- [x] M7-08 Expedition data (session 10, D-145; numbers to the owner: docs/balance/m7_08_expedition_data.md)
- [x] M7-12 Salvo Array (session 10, D-142)
- [x] M7-13 Ocean-edge generator, naval map checks, skirmish Ocean option (session 10, D-145, D-147)
- [x] M7-09 Carrier and air (session 10, D-147; no ammo system, no hangar UI)
- [x] M7-10 AI Expedition (session 11, PR #26: establish, convoy escorts, strikes, withdraw; naval deploy + tender lane fixes D-150/D-151; Archive entries; LLM naval fields) — naval strikes and income tuning continue in M7-14
- [x] M7-11 Art (owner PC, D-084): all 46 Expedition sprites (PR #28, variant A, D-149) and the four floating docks (PR #34, variant C, D-158)
- [ ] M7-14 Game layer + acceptance — game layer done (PR #26); income tuned (D-152, docs/balance/m7_income.md: medium/large pass, small +12..22 %); golden replay + 210-game soak 0 desyncs (docs/balance/m7_14_acceptance.md); open: §6 bullet 1 (win vs every tier), Q-55 docks, owner play-test

- [x] M7-15 Floating docks (Q-55 (a), D-153, D-156): wholly-on-water docks touching the shore, AI siting, drop the South stop-gap; redraw the four dock sprites (pilot first)
- [x] M7-16 Aircraft ammo (D-156): 4-5 rounds, rearm at the carrier, carrier balance table
- [x] M7-17 Submarine dived frames drawn; dived look only for owner and allies
- [x] M7-18 Expedition AI per tier (§6 bullet 1): accepted at 20-min games (D-162); tier 5 vs Directorate 80 % noted. M7 acceptance close-out still open with M7-14
- [x] M7-19 Left Behind early income (Q-52, D-153) and Expedition small-map re-measure
- [x] M7-20 Archive drafts (8 upgrade notes, Provision Store) and Left Behind lore revision (D-155)

## M8 · Campaign (was M4)
- [x] M8-02 Contracts: mission API stubs (docs/missions/api.md), schema v2, campaign progress/list/ending files, mission part v2 (session 12)
- [x] M8-02a Plan update with the owner's story answers (D-154/D-155), Fallout-style endings, voice slot; voice tool guide (docs/audio/voice_setup.md) (session 12)
- [x] M8-03 Objectives (primary/hidden), timers, area triggers (leave/hold/seen/idle), win/lose and tech helpers, Mission.Difficulty (session 12)
- [x] M8-04 Reinforcements: zone, transport, delayed, naval (Expedition), rig arrival (Left Behind), spawn groups with rank (session 12)
- [x] M8-05 Dialogue (speaker, portrait, locale key, optional voice), camera hints, locale loader (session 12)
- [x] M8-06 AI script hooks drive the AI: attack/defend zone, set tier, scripted wave (session 12)
- [x] M8-07 Campaign progress, tech carry-over, faction unlock order (assumed — confirm), Archive wiring (session 12)
- [x] M8-08 Game UI: faction pick, mission select, briefing v2 with difficulty, debrief, ending sequence (M8-16; images in data/images/endings/) (session 12)
- [x] M8-09 Tutorial mission + mission-check and bot test template (tut_01, MissionBot, Campaign → Tutorial)
- [x] M8-10 Pilot missions, one per faction (A1 port, D1, L1, E1; s12, D-163) — then owner review (M8-11)
- [x] M8-13 Archive campaign unlock ids (s12; docs/archive/m8_13_unlocks.md)
- [x] M8-17 Mission API follow-ups from the tutorial (s12): `Player.Funds` (Cash + Stored) for income checks; a script Win on the same tick as elimination must still run (check MissionCheck and the game loop stop at GameOver); tut_01 keeps a hidden reserve power plant so the enemy team survives until the script wins
- [x] M8-12 Fan-out: acts 2-4 for all four factions plus DRAFT ending slides (s12; owner review list docs/missions/s12_owner_review.md)
- [x] M8-14 act-break stills (s13 hook + pilot; s14: style C mix v2/v3, D-169; all 12 stills rendered, tools/art/stills/)
- [x] M8-15 Acceptance (s13): owner play-through ("everything looked great", D-164); bot play-through of all 16 missions in one run; mid-mission save/load for every mission; tier table docs/balance/m8_15_acceptance.md (all won; tier tuning → Q-62); bot-found balance table docs/balance/s13_campaign_balance.md (Q-60)
- [x] M8-18 Voiced radio lines (s14): Wyoming Piper batch tool (tools/voice/wyoming_batch.py, owner runs it on the NAS; its commit says M8-16 by mistake); voices chosen by the owner, licence checked per voice, credits in CREDITS.md; game must play the OGG voice files s22: switched to Kokoro on the laptop (D-226); 164 voice slots, all voiced and on integ-s22
- [x] M8-01 Campaign plan (docs/design/12_m8_plan.md, plan only; story questions open) (session 9)
Mission scripting/trigger system, briefing screens, tech gating, campaign flow; written once with all four factions.
Wires Archive campaign unlocks.

## M11 · UI and input overhaul (owner feedback 2026-11-07; built before M9, D-171)
Source: docs/reviews/2026-11-07_owner_campaign_feedback.md. Audit: docs/reviews/2026-11-07_ui_input_audit.md (s14).
- [x] M11-01 Adversarial UI and input audit, with layout options for the owner (s14)
- [x] M11-02 Edge scrolling (mouse at the screen edge pans the view)
- [x] M11-03 Control hints off the HUD; Controls page in Settings
- [x] M11-04 Selection card at the bottom centre for a unit, group or building; the sidebar build menu never moves (D-172 B, s14)
- [x] M11-05 Objective guidance (s14, D-172): target API, HUD (bar, marker, edge arrow, minimap ping, J/Tab/O, banner, camera cue), targets in all 17 missions
- [x] M11-06 Unit scale / zoom
- [x] M11-07 Terrain readability (paths, water, cliffs distinguishable) — look C (D-181, s15)
- [x] M11-08 Resource field readability — slag heaps / crystals by density, minimap colours (D-181, s15)
- [x] M11-09 Building animation speed calmer
- [x] M11-11 Blinking "no power" icon over buildings stopped by low power (D-170, s14)
- [x] M11-12 Full key rebinding (D-172)
- [x] M11-10 Fixes from the M11-01 audit and the s15 play-test (D-178):
  - [x] M11-10a Harvesters: right-click a field = harvest there, own refinery = return (s15 play-test)
  - [x] M11-10b Edge scroll measured in window client pixels (windowed mode, s15 play-test)
  - [x] M11-10c Selection toolkit: Shift-add, control groups (Ctrl/Shift + 1-9), double-click / Ctrl-click same type, select army (C)
  - [x] M11-10d Alert stack, minimap pings, last-alert key (End)
  - [x] M11-10e Cursor per action (CursorPreview mirrors OrderController)
  - [x] M11-10f Placement ghost with per-cell colour, reason line, build radius
  - [x] M11-10g Cameo tooltips, Shift+click queues 5
  - [x] M11-10h One UI scale rule (75-200 %), text size setting, more font sizes
  - [x] M11-10i Hold a production item with right-click (CommandKind.Hold, s15)

## M12 · Maps overhaul (D-180, docs/design/16_maps_overhaul.md; all of it before M9)
- [x] M12-01 Decal pass (derived decals, per-biome tables, second baked texture) (s15)
- [x] M12-02 Land-linked placeholder props on rock/rough/glass/ice (s15)
- [x] M12-03 Prop atlas interface, decor toggle (H), budget and frame-time tests (s15)
- [x] M12-04 Map format 4: decor layer, blocking prop overlay kinds, placement/harvest checks, hash coverage (s15)
- [x] M12-05 Generator decoration pass with the map-study densities (D-183); replaces the old generator (D-180 D4); old saves refused with a message (s15)
- [x] M12-06 `map report`, `map render --png` (s15)
- [ ] M12-06b Editor Props tool and decal brush (skipped in s15)
- [~] M12-07..12 (s16: layout 2 built for all four biomes, off by default. s17: v6 = Ash half-turn D-198, fields further out D-199, Expedition start units fixed; open: 64-game fairness and income on v6, Q-70, Mountains skew is not the map) Zone skeleton, ~3 lanes and chokes, mixed start shapes (mostly soft shell, some walled with a ramp), natural expansion on about half the maps, 2v2 teammates side by side, fairness checks, income re-measure (design 16 wave 3, revised by D-183 / design 17)
- [~] M12-12b `ai timeline` now plays the same map as `ai match` (s18). Open: Glass Coast layout 1 Expedition start skew: cause measured in s18 (tender spawn, M13-03)
- [ ] M12-13..16 Stamps, rebuilt data maps, prop art (M10), optional extras (design doc wave 4)

## M13 · Audit fix batch (session 18 audit, D-205; docs/reviews/2026-11-11_s18_audit/README.md)
- [x] M13-00 Clear non-sim bugs fixed with tests: campaign/missions, in-match UI, menus/setup/tools, LLM key per provider, credits (s18 PR)
- [x] M13-01 Expedition on Mountains layout 1: edge-connected sea near each start (generator) + whole-map SkirmishStart fallback (item 1)
- [x] M13-02 Expedition founder counts as a base for defeat (item 2)
- [x] M13-03 Supply tender spawns at the edge cell nearest the dock (item 3)
- [x] M13-04 Garrisoned/carried units ignore orders and are not selectable (item 4)
- [x] M13-05 Path stall: retarget blocked goals, cap per-tick path work, group search retry (item 5, SP-01/SP-13; s19, D-209; also the harvester head-on deadlock and goal-held waiting)
- [x] M13-06 Ready structure: cancel with refund; AI cancels after failed site searches; Cancel slot fix (item 6)
- [x] M13-07 Full building store: refuse deploy, StoreFull reason (item 7)
- [x] M13-08 Data: Atomic Battery range 0, campaign allow-lists (items 8, 10)
- [x] M13-09 Left Behind lanes pause while the Mother Rig is packed (item 9)
- [x] M13-10 Start-side tie-breaks (item 11): mirror-image placement tie-break and start kits; wider AI placement window (s19; confirmed by the after-series: worst start share 92 % -> 54 %, D-222)
- [x] M13-11 Mechanics items 12-22, 25 (s19: 12-18, 20 harvester-after-rebuild, 21, 25, LE-14; s20: 19 melee acquire, 20 unreachable field (SP-04) + choke give-up (SP-03 Move half), 22 path leak (SIMDET-03), D-218)
- [x] M13-12 Ashfall hazard (item 23), attack-ground order (item 24), hide/reword missing mechanics (item 26)
- [~] M13-13 Numbers pass (s19, D-208: Agitprop, Tow, Marksman, ship warheads small-arms/AP, Directorate copy structures, Commissar aura, layout 2 money; option-only items wait for the after-series) s22: items 2-6, 8, 9, 11, 12, 14 applied (D-223); item 1 kept; item 13 waits for a hand check
- [x] M13-14 UI/wording/design list (section 2.4) incl. mission-script one-liners, naming pass from data/archive
- [x] M13-15 QoL: game speed incl. slow; idle button and alerts; cameo hotkeys, repeat production, range rings, strong/weak line; handicap and starting conditions; stances and shift-queued waypoints
- [~] M13-16 Re-pins done per merge batch (s19); owner laptop series before (s18audit), after (s19after) and on the s21 code (s21after, docs/balance/s21_after_series.md): done
- [x] M13-17 Campaign retune with the ashfall hazard on (missions run with ash damage and ash path cost off until then, D-211). s20: 12-seed win-rate baseline with ash on for all 45 mission-tiers (docs/balance/s20_campaign_seed_baseline.md, D-218 point 8); auth_01 tier 5 winnable accepted (D-217). s21: ash on in missions, D-219 stance rule, retunes of auth_01/03/04, dir_04, lb_03, exp_03 to the Q-75 targets (D-222, docs/balance/s21_campaign_retune.md); confirmed by the owner's laptop sweep on 8b418a2e: all 45 mission-tiers on target, identical to the cloud numbers
- [x] M13-18 Follow-ups found in s19 (s20, D-218): placement lane outlet toward the map centre (S18-AI-NEW-3) done; ClearBuildArea removed; LB Still Rigs 14-18 and used, no cap; LB yard dock livelock not reproduced in the open, dead-end corridor case open (Q-76); walls/gates hero renders re-angled to 3/4 (M14) open s22: dead-end dock lane done (Q-76, D-225); walls/gates heroes re-angled and detailed (D-227)

- [~] M13-19 Skirmish balance after s21 (Q-77): Authority behind (v Directorate 17-36, v Expedition 12-51), Glass Coast Expedition 43-55 v 7-14, s22: diagnosis docs/balance/s22_balance_options.md, all picks applied (D-223); laptop series pending s23: s22after read; Rocket Truck splash back to 1 (D-229); laptop re-run s23after pending
  Left Behind mirror Mountains L1 start corner -25 % income. First the owner's A/B (tools/measure/run_s21_ab.ps1), then tune (docs/balance/s21_after_series.md)

## M14 · Field Manual (D-206, docs/design/19_field_manual.md)
- [x] M13-20 Mop-up AI fixed and on in skirmish (s22, D-224); laptop on/off series pending
- [x] M13-21 Radio lines for every unannounced or announced enemy wave (s22, D-226 point 5)
- [ ] M13-22 Left Behind v Expedition after s22 (LB wins 48-9 Rust Belt, 37-16 Glass Coast): loss tables, then a matchup-specific change (s23 probes: docs/balance/s23_probes.md)
- [x] M14-01 Pilot: 9 pages, generator scripts (tools/booklet), design 19 (s18)
- [x] M14-02 Hero render mode and laptop script (tools/booklet/render_heroes.ps1) (s18); s19: module cache fix (LB structures)
- [x] M14-07 Pilot PDF and previews in docs/field_manual/, linked from the README (s19)
- [x] M14-03 Hero renders: pilot approved, all 124 rendered on the laptop and on main (s19, D-210; PR #63). Open: walls/gates re-angle s22: walls/gates re-angled -35 deg with half sky light and detail-passed models, laptop renders on integ-s22 (D-227)
- [ ] M14-04 Directorate lore draft (Authority approved), then the other three faction chapters s22: Directorate draft approved (design 19 §6b, D-228); Left Behind and Expedition chapters next
- [ ] M14-05 Full build (about 108 pages) after M13 lands; quick-reference card
- [x] M14-06 Models and sprites for the 26 ids without art (scripts D-215; owner rendered on the laptop; landed in PR #74 s21; no rules id is left without art)

## M9 · LAN multiplayer (was M5)
Plan: docs/design/20_m9_lan_plan.md (§9 = owner answers, D-213). Built overnight (D-216). User can try: two PCs → LAN Game → host / join → play.
- [x] M9-00 Contracts: src/Net, NetMessage/INetTransport/NetProtocol, SimHost Outbox + StepWithBundle
- [x] M9-01 NetCodec, UDP transport with reliability (acks, resend, ordering, fragments), LAN discovery
- [x] M9-03 Lockstep core (scheduler, buffer, host, client; pause, drop to AI, hash compare, desync), loopback network
- [x] M9-04 `Tools lan soak` (loopback and `--udp`), UDP lockstep test
- [x] M9-05/06/13 LAN browser, lobby (host/client, slots, map with preview, options, delay, spectators, chat), handshake (version + content hash), custom map transfer
- [x] M9-07/08/10 In-game LAN: lockstep wiring, waiting overlay + drop, pause countdown, chat, AI takeover, desync reports + resync, host left → continue vs AI, LAN replays
- [x] M9-14 Independent review fixes (shared network clock, desync resolve, bundle cap, failed start, hash bounds, late leavers, resync errors)
- [ ] M9-15 Owner two-PC LAN test (and a Wi-Fi test); Q-74 chat in replays

## M10 · Polish
Plan: docs/design/21_m10_polish_plan.md (D-214). Code-only parts built overnight (D-216, PR #69):
- [x] M10-09c Faction-aware music states (tension, briefing, credits); tracks: tools/audio/ace_step (owner's laptop, 20 tracks take 1 overnight) s22: take 1 of all 20 tracks exported to data/music (LFS)
- [x] M10-12 Weather per biome (toggle) · [x] M10-13 Animated water, shore foam, ship wakes · [x] M10-14 Battle juice (shake toggle, hit flash, muzzle flash)
- [x] M10-17 End-of-match graphs · [x] M10-18 Map preview thumbnails · [x] M10-19 Medals and records (Profile page; par medals Q-73)
- [x] M10-20 Loading screen with tips, credits roll · [x] M10-22 Inno Setup installer + CI artifacts + release workflow · [x] M10-23 Update notice
- [ ] M10-06 Warzone tracks on every tracked vehicle · [ ] M10-08 Building depth pipeline · [~] M10-10 SFX pass (s22: Stable Audio Open laptop generator, 80 jobs, sound keys and ambience code done; laptop generation pending) · [ ] M10-11 Portraits, death frames
- [ ] M10-15 Faction UI skin (pilot first) · [ ] M10-16 Portraits in the card · [ ] M10-14b Death animations (need frames) · [ ] M10-21 Final balance pass
- [ ] M10-xx Building art: less flat, more depth (owner 2026-11-07, later polish run)
- [x] M10-01 Damage smoke/fire tiers, rubble/wrecks/scorch, vehicle dust, biome mood tint, larger-infantry option (D-184, D-187, s15)
- [x] M10-02 All 18 infantry re-rendered at figure scale 1.25 with team-colour emphasis; tools/art/house_audit.py (D-185, s15)
- [x] M10-03 Imported FX (Skorpio, LPC flames, Kenney puffs) and props (0 A.D., LPC) with credits and licence texts (D-188, s15)
- [x] M10-04 Warzone 2100 .pie importer and kitbash tooling (D-189, s15)
- [x] M10-05 (closed, D-214: D-192 left 0 of 138 flagged) Team-colour audit fixes for vehicles (11 of 45 hulls, 8 of 13 turrets under 10 px; tools/art/house_audit.py)
- [ ] M10-06 Kitbash Warzone parts into our vehicles where they add detail (D-189); per-vehicle FX anchors in manifests
- [ ] M10-07 Props still procedural: GlassCrest, IceSlab, WreckHulk, BrokenPylon (no fitting open art yet)
Art pass, music/SFX, balance, installer.
