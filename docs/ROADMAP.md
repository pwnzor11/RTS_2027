# Roadmap
Milestones in build order. Each ends with something the owner can try. Ids are stable; tick when done.

## M0 · Foundations (docs, research, design)
- [x] M0-01 Handoff doc set in `docs/`
- [x] M0-02 Clone EA C&C Remastered source as read-only reference
- [ ] M0-03 Research: C&C Remastered code study (`docs/research/01_cnc_code_study.md`)
- [ ] M0-04 Research: RTS engine architecture (`docs/research/02_rts_architecture.md`)
- [ ] M0-05 Research: map making and map formats (`docs/research/03_maps.md`)
- [ ] M0-06 Research: resource/economy systems (`docs/research/04_resources_economy.md`)
- [ ] M0-07 Research: RTS AI (skirmish AI, difficulty, campaign scripting) (`docs/research/05_ai.md`)
- [ ] M0-08 Design round 1: technology, engine/language, sim architecture — user can try: nothing yet; produces `docs/design/`
- [ ] M0-09 Design round 2: game design (factions, economy, tech tree shape, map scale)
- [ ] M0-10 Design round 3: campaign structure and story outline; tooling (map editor) plan
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
