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
- [ ] M0-12 Art pilot: one vehicle three ways (Blender render, AI-assisted frames, hand pixel) at 2 candidate cell sizes + house-colour remap test — user can try: look at a contact sheet and pick
- [ ] M0-11 Repo skeleton: build system, CI, LICENSE, CLAUDE.md pointing at docs — user can try: clone + build an empty window

## M1 · Core simulation (no graphics beyond debug view)
_To be detailed after design round 1. Expected shape:_ deterministic tick loop, map grid, pathfinding,
unit movement, orders/commands queue, combat, fog, replay recording. User can try: headless sim test +
debug renderer.

## M2 · Playable skirmish slice
Rendering with placeholder sprites, UI (sidebar, selection, minimap), economy loop (harvester →
refinery → credits → build), one faction, one map, dumb AI. User can try: a 10-minute game.

## M3 · Full skirmish
Both factions, full tech tree, map editor, real AI with difficulty levels, audio.

## M4 · Campaign
Mission scripting/trigger system, briefing screens, tech gating, campaign flow, both campaigns.

## M5 · LAN multiplayer
Lockstep over LAN, lobby, desync detection, replays.

## M6 · Polish
Art pass, music/SFX, balance, installer.
