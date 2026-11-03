# Open questions
🔴 blocks work · 🟡 needed soon · ⚪ later. Code waiting on one carries `// PENDING Q-xx`.
Context and trade-offs for every question are in `docs/research/00_SYNTHESIS.md` §2 and §4 and the
file cited after each question. Recommendations are the lead's, from the research.

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
- Round 1 (Q-01, Q-02, Q-09..Q-18) · 2026-11-02 → D-009..D-019. Sub-choice MonoGame vs FNA left to the lead (see design/01_architecture.md).
- Q-00 · (session 1 interview) Licensing, platform, look, mode priorities, doc location → D-001..D-008.
