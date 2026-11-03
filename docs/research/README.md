# Research notes

Session-1 research that feeds the design rounds. Each file stands alone and ends with a
"Implications for RTS_2027" section and a sources list. Files numbered 01–06 study the EA
C&C Remastered source (`/home/claude/ref/CnC_Remastered_Collection`, GPL v3); 10+ are web research.

| File | Topic |
|---|---|
| 01_cnc_sim_core.md | C&C object model, game loop, map/cell/coordinate system, memory model |
| 02_cnc_units_combat_production.md | Unit types data, movement, pathfinding, combat, production, tech tree |
| 03_cnc_networking_determinism.md | Event queue, lockstep, RNG, desync, DLL interface to the remaster |
| 04_cnc_ai_and_scripting.md | House AI, base building, teams, triggers, scenario scripting |
| 05_cnc_maps_and_resources.md | Scenario/INI map format, theaters/templates, Tiberium/ore, harvesting, the C# map editor |
| 06_cnc_red_alert_vs_tiberian_dawn.md | What RA changed over TD and why it matters to us |
| 10_rts_architecture.md | How RTS engines are built: lockstep, determinism, sim/render split, ECS vs OOP, OpenRA/Spring/0AD |
| 11_pathfinding_and_movement.md | RTS pathfinding and group movement |
| 12_map_making.md | How RTS maps are made: tiles, editors, formats, design principles |
| 13_resource_systems.md | Economy and resource system design across RTS games |
| 14_rts_ai.md | Skirmish AI architectures, difficulty design, campaign scripting systems |
| 15_technology_options.md | Language/engine/framework options for a Windows 2D RTS, incl. building on OpenRA |
| 16_campaign_design.md | Campaign and mission design in classic RTS games |
| 17_sprite_art_pipeline.md | 2D sprite production for RTS: pre-rendered 3D, pixel art, facings, palettes, open assets |
