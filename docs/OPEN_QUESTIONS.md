# Open questions
🔴 blocks work · 🟡 needed soon · ⚪ later. Code waiting on one carries `// PENDING Q-xx`.
Context and trade-offs for every question are in `docs/research/00_SYNTHESIS.md` §2 and §4 and the
file cited after each question. Recommendations are the lead's, from the research.

## Round 1 · Technology and simulation architecture (🔴 all block the repo skeleton)
- 🔴 **Q-01 · Language and stack.** (a) **C# custom sim library + FNA/MonoGame renderer (recommended)**;
  (b) OpenRA fork/mod; (c) C++ custom on SDL3, porting EA algorithms; (d) Rust/Bevy. [15, 10]
- 🔴 **Q-02 · How much EA code to port.** (a) **algorithms and rule numbers, re-implemented (recommended)**;
  (b) whole sim ported to a new host; (c) reference only. [01, 03, 15]
- 🔴 **Q-09 · OpenRA spike first?** (a) no, start our own; (b) **2–3 week spike building a mod, then decide (recommended by 15)**. [15]
- 🔴 **Q-10 · Object model.** (a) **data-oriented components in slot arrays + per-entity mission state machine (recommended)**; (b) an ECS library; (c) class hierarchy like C&C. [01, 10, 20]
- 🔴 **Q-11 · Movement model.** (a) **cell-locked classic: one vehicle per cell, infantry sub-slots (recommended)**; (b) continuous positions with radius collision. [11, 02]
- 🔴 **Q-12 · Sim tick rate.** 15 Hz (C&C) / **20 Hz (recommended)** / 30 Hz. [10, 03]
- 🔴 **Q-13 · Mission/AI scripting.** (a) **embedded Lua with a trigger-style API, run inside the sim tick (recommended)**; (b) pure data trigger tables; (c) both (data for 80%, Lua hooks). [04, 14, 16]
- 🔴 **Q-14 · Data file format.** **TOML (recommended)** / JSON / YAML, with one-level inheritance and effect/patch lists. [20, 21, 23]
- 🟡 **Q-15 · Replays as an early test tool?** **yes (recommended)** / design constraint only / player-facing from the start. [03, 24]
- 🟡 **Q-16 · AI issues commands through the same queue as humans?** **yes (recommended)** / AI mutates sim directly. [03]
- 🟡 **Q-17 · Mid-mission saves required?** Constrains AI and script state. [03]
- 🟡 **Q-18 · Mod-ability scope.** (a) balance files only; (b) **full content packs: new units + art (recommended if open-source-forever is meant to attract contributors)**. [06, 21]

## Round 2 · Game design
- 🟡 **Q-03 · Faction concept and names.** Two factions; original names; morality (equal / one protagonist); relationship of the two campaigns (parallel / one canon / endings interact). [16]
- 🟡 **Q-04 · Resource model details.** Regrowth (yes / no / per-map); trickle vs lump unload; silos and overflow loss; AI economy on harder tiers (same rules / visible multiplier / hidden). [05, 13]
- 🟡 **Q-05 · Map scale and terrain.** 64 / 96 / 128 cells; elevation (cosmetic / walls+ramps / bonuses); number of theaters. [05, 12]
- 🟡 **Q-19 · Tech tree scope.** Unlocks only vs stat upgrades too; superweapons (fixed set / in tech tree); naval in v1? [06, 21]
- 🟡 **Q-20 · Production details.** Drain vs lump; queues; parallel factories; friendly fire on splash; infantry per cell (1/4/5). [02]
- 🟡 **Q-21 · Fog.** Permanent shroud (C&C) / shroud + true fog / reveal-all option. [05, 21]
- 🟡 **Q-22 · AI difficulty ladder.** Number of tiers (5–6); cheats at all; personalities; how hard is the top tier; dynamic difficulty. [04, 14]

## Round 3 · Campaign, tooling, art
- ⚪ **Q-07 · Campaign structure.** Missions per side (8/10/12); choice points; voice vs text; carry-over; meta-map. [16]
- ⚪ **Q-06 · Map editor path.** Tiled + importer now / own editor from start / (not) fork EA's. [05, 12, 15]
- ⚪ **Q-08 · Art.** Faithful 24px look vs HD; 8/16 facings; Blender commitment; AI-art policy; licence stance (CC0/MIT/BSD/GPL/OFL only, or + CC BY-SA 4.0). [17]
- ⚪ **Q-23 · LAN scope.** Topology (host-star recommended), host migration (none in v1), player count, AI slots in LAN lobbies. [22, 03]

## Answered
- Q-00 · (session 1 interview) Licensing, platform, look, mode priorities, doc location → D-001..D-008.
