# Open questions
🔴 blocks work · 🟡 needed soon · ⚪ later. Code waiting on one carries `// PENDING Q-xx`.

## Open
_The design rounds (M0-08..10) are where these get answered. This list is seeded by session 1's
research; the research docs give the context for each._

- 🔴 **Q-01 · Language and engine.** Options to be laid out in design round 1 with the research in
  hand (e.g. C++ custom engine, C# + MonoGame/FNA, Rust + Bevy/macroquad, Godot, port of the EA code).
- 🔴 **Q-02 · How much EA code to port vs. write fresh.** Ranges from "port the whole sim" to
  "read it for ideas only".
- 🟡 **Q-03 · Faction concept and names.** Two factions; needs original names and identity (no EA marks).
- 🟡 **Q-04 · Resource model.** Single harvestable field resource like Tiberium/ore, multiple
  resources, or something new. Research doc 04 covers the trade-offs.
- 🟡 **Q-05 · Map scale and tile system.** Cell size, map size, isometric vs top-down square grid,
  terrain types, elevation yes/no.
- 🟡 **Q-06 · Map editor.** Build our own, adapt the EA C# editor, or use Tiled with a custom format.
- ⚪ **Q-07 · Campaign length.** Missions per side.
- ⚪ **Q-08 · Art pipeline.** Hand-drawn pixel art, pre-rendered 3D to sprites (how Westwood did it),
  or AI-assisted generation; and who makes it.

## Answered
- Q-00 · (session 1 interview) Licensing, platform, look, mode priorities, doc location → D-001..D-008.
