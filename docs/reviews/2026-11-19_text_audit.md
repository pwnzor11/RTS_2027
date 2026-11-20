# On-screen text audit (M11-31)

Owner complaint: the on-screen text reads like development stuff, with odd names and stray periods. This audit lists every
player-visible string the scanner could reach, flags dev-looking text, and records what was done about it.

Scanner: `python3 tools/text/scan_text.py` (stdlib only). It reads the C# UI code in `src/Game/Ui`, `Screens`, `Archive`,
`Campaign`, `Veterancy`, `Menu` (string literals and interpolations, with debug-only code split out), every mission
`locale.toml` value, archive titles/subtitles/bodies/field notes, `briefing.md`, and `data/ui` tips and credits. Objective-panel
code (`src/Game/Ui/Objectives`, `MissionObjectiveLines.cs`) belongs to another helper and is skipped.
The "Distinct flagged strings" row counts a string once even when it trips two categories.

## Before (scanner run on the unmodified text)

Strings scanned: C# UI strings (src/Game) 599, Mission locale.toml values 440, Archive titles, bodies, field notes 679, Briefings (briefing.md) 160, data/ui text (tips, credits) 79.

| Category | Flagged strings |
|---|---|
| Raw ids shown to players (snake_case ids, type ids, interpolated ids) | 11 |
| Dotted keys shown to players | 0 |
| Bracketed placeholders ([draft pending], [key]) | 14 |
| TODO / draft / placeholder / test / stub wording | 17 |
| Stray double periods or '...' | 7 |
| Inconsistent capitalisation of unit/faction names vs archive titles | 18 |
| Programmer words in player text (null, id, enum, tick, index, debug, sim, cell coordinates) | 5 |
| **Distinct flagged strings** | **59** |

### C# UI strings (src/Game) (24)

- `src/Game/Archive/ArchiveStats.cs:105` raw_id: list of raw ids joined into text -- `prerequisites.Count == 0 ? "nothing" : string.Join(", ", prerequisites);`
- `src/Game/Archive/ArchiveStats.cs:101` raw_id: interpolated id: {w.Id} -- `{w.Id}: {w.Damage} damage, range {range} cells, reload {Seconds(w.RateOfFire)}`
- `src/Game/Menu/HowToPlayContent.cs:101` punctuation: double period or ellipsis -- `Home centres on your base.   Ctrl+F5..F8 store a view, F5..F8 jump back to it.   F1 or ? lists every key.`
- `src/Game/Screens/ArchiveScreen.cs:401` placeholder_bracket: bracket text: [no text] -- `[no text]`
- `src/Game/Screens/DebriefModel.cs:89` raw_id: list of raw ids joined into text -- `lines.Add("New tech: " + (NewTech.Count == 0 ? "none" : string.Join(", ", NewTech)));`
- `src/Game/Screens/LanBrowserScreen.cs:130` punctuation: double period or ellipsis -- `Looking for games on the network ...`
- `src/Game/Screens/LobbyScreen.cs:235` punctuation: double period or ellipsis -- `Connecting to the host ...`
- `src/Game/Screens/LobbyScreen.cs:274` punctuation: double period or ellipsis -- `Downloading the map from the host ... {_client.MapPercent}%`
- `src/Game/Screens/MissionSelectModel.cs:35` raw_id: mission id used as the title fallback -- `service.Missions(faction).Select(m => new MissionSelectRow(m.Id, m.Exists ? TitleOf(dataDir, m.Id) : m.Id, ...`
- `src/Game/Screens/ReplayViewerScreen.cs:150` programmer_word: programmer word: tick -- `REPLAY  {state}   tick {clock.Tick} / {clock.FinalTick} ({Percent(clock.Progress)})   view {view}`
- `src/Game/Screens/ReplayViewerScreen.cs:153` programmer_word: programmer word: tick -- `WARNING: the replay no longer matches the game data (differs from tick {_play.DesyncTick})`
- `src/Game/Screens/SkirmishSetupScreen.cs:565` punctuation: double period or ellipsis -- `
... and {mapErrors.Count - 4} more`
- `src/Game/Ui/Llm/LlmUiText.cs:10` punctuation: double period or ellipsis -- `AI commander (LLM)...`
- `src/Game/Ui/Llm/LlmUiText.cs:46` punctuation: double period or ellipsis -- `Set API key...`
- `src/Game/Ui/Llm/LlmUiText.cs:50` dev_wording: dev word: Test -- `Test connection`
- `src/Game/Ui/Llm/LlmUiText.cs:56` dev_wording: dev word: Test -- `Test failed.`
- `src/Game/Ui/Sidebar/BuildingUpgradeOffer.cs:40` raw_id: raw prerequisite id in text -- `return new BuildingUpgradeOffer(target, caption, cost, false, $"Needs {need}");`
- `src/Game/Ui/Sidebar/BuildingUpgradeOffer.cs:53` raw_id: ad-hoc id tidying (use CameoNames.Display) -- `return (u >= 0 && u + 1 < id.Length ? id[(u + 1)..] : id).Replace('_', ' ').ToUpperInvariant();`
- `src/Game/Ui/Sidebar/BuildingUpgradeOffer.cs:36` raw_id: interpolated id: {CaptionOf(to.Id)} -- `UPGRADE {CaptionOf(to.Id)} ${cost}`
- `src/Game/Ui/Sidebar/UpgradeEffectText.cs:33` raw_id: ad-hoc id tidying (use CameoNames.Display) -- `string attr = e.Attr.Replace('_', ' ');`
- `src/Game/Ui/Spectator/SpectatorInspect.cs:49` raw_id: type id is the first info line -- `return new[] { id, $"P{u.Player + 1}  health {u.Health}/{max}" };`
- `src/Game/Ui/Spectator/SpectatorInspect.cs:56` raw_id: type id is the first info line -- `var lines = new List<string> { t.Id, $"P{b.Player + 1}  health {b.Health}/{t.MaxHealth}" + (b.Ghost ? "  (l...`
- `src/Game/Ui/Spectator/SpectatorTable.cs:61` programmer_word: programmer word: STRUCT -- `PLAYER  FACTION    TIER  CASH  INCOME/M  ARMY  STRUCT  POWER  BUILT  LOST  KILLS  UPG`
- `src/Game/Ui/Superweapon/SuperweaponButton.cs:72` raw_id: ad-hoc id tidying (use CameoNames.Display) -- `return (u >= 0 && u + 1 < id.Length ? id[(u + 1)..] : id).ToUpperInvariant();`

### Mission locale.toml values (15)

- `data/missions/auth_03/locale.toml:22` capitalisation: 'left behind' vs archive 'Left Behind' -- `The yard is lost. There is nothing left behind to build from.`
- `data/missions/auth_04/locale.toml:14` capitalisation: 'reactor vent' vs archive 'Reactor Vent' -- `Dray. The device draws from the reactor vent east of it. Kill the vent and the charge slows. The tunnel hea...`
- `data/missions/auth_04/locale.toml:15` capitalisation: 'Reactor vent' vs archive 'Reactor Vent' -- `Reactor vent destroyed. The device is losing its charge.`
- `data/missions/auth_04/locale.toml:26` capitalisation: 'reactor vent' vs archive 'Reactor Vent' -- `Destroy the reactor vent that feeds the device`
- `data/missions/exp_01/locale.toml:17` capitalisation: 'landing craft' vs archive 'Landing Craft' -- `A landing craft is lost.`
- `data/missions/exp_01/locale.toml:18` capitalisation: 'landing craft' vs archive 'Landing Craft' -- `We have lost a landing craft. Please be careful with the rest.`
- `data/missions/exp_01/locale.toml:28` capitalisation: 'landing craft' vs archive 'Landing Craft' -- `Bring both shipments in without losing a landing craft`
- `data/missions/lb_02/locale.toml:6` capitalisation: 'power plant' vs archive 'Power Plant' -- `Each turret runs off its own power plant, a good way back from the gun. Kill the plant and the turret goes ...`
- `data/missions/lb_02/locale.toml:8` capitalisation: 'power plant' vs archive 'Power Plant' -- `A post power plant is down.`
- `data/missions/lb_03/locale.toml:9` capitalisation: 'reactor vent' vs archive 'Reactor Vent' -- `The reactor vent is down. The pillboxes are dark.`
- `data/missions/lb_03/locale.toml:27` capitalisation: 'reactor vent' vs archive 'Reactor Vent' -- `Destroy the hatch field's reactor vent`
- `data/missions/lb_04/locale.toml:12` capitalisation: 'pile house' vs archive 'Pile House' -- `A pile house is down.`
- `data/missions/lb_04/locale.toml:15` capitalisation: 'gun battery' vs archive 'Gun Battery' -- `A gun battery is down.`
- `data/missions/tut_01/locale.toml:4` capitalisation: 'power plant' vs archive 'Power Plant' -- `Good. Now the enemy power plant to the east is your practice target. Select the squad and right-click the b...`
- `data/missions/tut_01/locale.toml:9` capitalisation: 'power plant' vs archive 'Power Plant' -- `Last lesson. A small enemy camp sits in the north-east. Gather the squad and attack-move onto it. Destroy e...`

### Archive titles, bodies, field notes (18)

- `data/archive/structures_authority.toml:60` dev_wording: dev word: TEST -- `[auth_factory.field_note] FACTORY 5 // WELD, FIT, TEST. NOTHING LEAVES WITHOUT A STAMP. STOP.`
- `data/archive/structures_authority.toml:144` dev_wording: dev word: TEST -- `[auth_engineering.field_note] ENGINEERING 12 // DESIGN, TEST, ISSUE AS BULLETIN. NOTHING IS A SECRET. STOP.`
- `data/archive/structures_expedition.toml:11` capitalisation: 'construction yard' vs archive 'Construction Yard' -- `[exp_landing_depot.subtitle] Expedition construction yard`
- `data/archive/technology.toml:11` placeholder_bracket: bracket text: [draft pending]; dev_wording: dev word: draft -- `[tech_ashfall.body] [draft pending]`
- `data/archive/technology.toml:21` placeholder_bracket: bracket text: [draft pending]; dev_wording: dev word: draft -- `[tech_pitchblende.body] [draft pending]`
- `data/archive/technology.toml:31` placeholder_bracket: bracket text: [draft pending]; dev_wording: dev word: draft -- `[tech_vacuum_tubes.body] [draft pending]`
- `data/archive/technology.toml:41` placeholder_bracket: bracket text: [draft pending]; dev_wording: dev word: draft -- `[tech_decontamination.body] [draft pending]`
- `data/archive/technology.toml:51` placeholder_bracket: bracket text: [draft pending]; dev_wording: dev word: draft -- `[tech_hardened_ground.body] [draft pending]`
- `data/archive/technology.toml:61` placeholder_bracket: bracket text: [draft pending]; dev_wording: dev word: draft -- `[tech_seismic_detection.body] [draft pending]`
- `data/archive/technology.toml:71` placeholder_bracket: bracket text: [draft pending]; dev_wording: dev word: draft -- `[tech_tunnel_network.body] [draft pending]`
- `data/archive/technology.toml:81` placeholder_bracket: bracket text: [draft pending]; dev_wording: dev word: draft -- `[tech_radiation.body] [draft pending]`
- `data/archive/technology.toml:91` placeholder_bracket: bracket text: [draft pending]; dev_wording: dev word: draft -- `[tech_blackout.body] [draft pending]`
- `data/archive/technology.toml:101` placeholder_bracket: bracket text: [draft pending]; dev_wording: dev word: draft -- `[tech_static_broadcast.body] [draft pending]`
- `data/archive/technology.toml:111` placeholder_bracket: bracket text: [draft pending]; dev_wording: dev word: draft -- `[tech_sunrise_device.body] [draft pending]`
- `data/archive/technology.toml:121` placeholder_bracket: bracket text: [draft pending]; dev_wording: dev word: draft -- `[tech_broadcast_surge.body] [draft pending]`
- `data/archive/technology.toml:131` placeholder_bracket: bracket text: [draft pending]; dev_wording: dev word: draft -- `[tech_atomic_artillery.body] [draft pending]`
- `data/archive/units_expedition.toml:154` capitalisation: 'Field engineer' vs archive 'Field Engineer' -- `[exp_pioneer.subtitle] Field engineer`
- `data/archive/upgrades.toml:115` capitalisation: 'bore head' vs archive 'Bore Head' -- `[dir_up_tunnel_bracing.subtitle] Faster hatch and bore head work`

### Briefings (briefing.md) (1)

- `data/missions/dir_04/briefing.md:7` programmer_word: programmer word: TICKS -- `2. THE SUNRISE DEVICE LIES IN ITS CHAMBER BENEATH YOUR KEEP. IT NEEDS THREE THOUSAND TICKS TO CHARGE AND CO...`

### data/ui text (tips, credits) (1)

- `data/ui/credits.toml:32` programmer_word: programmer word: TOML -- `TOML parsing of rules and maps. BSD-2-Clause.`

### Debug only (13)

Strings in debug consoles, the script debug line and the Show diagnostics line. Not shown to players by default; not counted above.

- `src/Game/Screens/ControlsScreen.cs:86` programmer word: tick -- `Show diagnostics (tick, hash, fps): {(settings.ShowDiagnostics ? "On" : "Off")}`
- `src/Game/Screens/GameScreen.cs:1747` programmer word: tick -- `tick {snapshot.Tick}  hash {snapshot.StateHash:x16}  fps {_s.Fps}  zoom {_camera.ZoomLa...`
- `src/Game/Ui/DebugCommandParser.cs:82` bracket text: [count] -- `usage: spawn vehicle|infantry <typeId> [count] [player]`
- `src/Game/Ui/DebugCommandParser.cs:121` bracket text: [count] -- `spawn vehicle|infantry <typeId> [count] [player]   at the mouse cell`
- `src/Game/Ui/DebugCommandParser.cs:122` bracket text: [player] -- `cash <amount> [player]   kill (selected units)   reveal   speed 1|2|4|8`
- `src/Game/Ui/DebugCommandParser.cs:123` bracket text: [n] -- `pause   step [n]   help   Up/Down history   Esc or F1 closes this`
- `src/Game/Ui/DebugCommandParser.cs:155` interpolated id: {typeId} -- `unknown {words[1].ToLowerInvariant()} type '{typeId}'; valid: {string.Join(", ", ids)}`
- `src/Game/Ui/DebugCommandParser.cs:175` bracket text: [player] -- `usage: cash <amount> [player]`
- `src/Game/Ui/DebugCommandParser.cs:205` bracket text: [n] -- `usage: step [n]`
- `src/Game/Ui/DebugConsole.cs:174` programmer word: tick -- `cash of player {cash.Player} set to {cash.Amount} (next tick)`
- `src/Game/Ui/DebugConsole.cs:193` programmer word: tick -- `paused; stepped {step.Ticks}, now tick {_sim.Tick}`
- `src/Game/Ui/DebugConsole.cs:213` interpolated id: {spawn.TypeId} -- `queued {spawn.Count} {spawn.TypeId} for player {spawn.Player} at cell {cell.X},{cell.Y}`
- `src/Game/Ui/DebugConsole.cs:230` programmer word: tick -- `killing {units.Count} selected unit(s) (next tick)`

