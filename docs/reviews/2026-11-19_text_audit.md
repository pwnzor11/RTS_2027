# On-screen text audit (M11-31)

Owner complaint: the on-screen text reads like development stuff, with odd names and stray periods. This audit lists every
player-visible string the scanner could reach, flags dev-looking text, and records what was done about it.

Scanner: `python3 tools/text/scan_text.py` (stdlib only). It reads the C# UI code in `src/Game/Ui`, `Screens`, `Archive`,
`Campaign`, `Veterancy`, `Menu` (string literals and interpolations, with debug-only code split out), every mission
`locale.toml` value, archive titles/subtitles/bodies/field notes, `briefing.md`, and `data/ui` tips and credits. Objective-panel
code (`src/Game/Ui/Objectives`, `MissionObjectiveLines.cs`) belongs to another helper and is skipped.
The "Distinct flagged strings" row counts a string once even when it trips two categories.

## Before

Strings scanned: C# UI strings (src/Game) 599, Mission locale.toml values 440, Archive titles, bodies, field notes 679, Briefings (briefing.md) 160, data/ui text (tips, credits) 79.

| Category | Flagged strings |
|---|---|
| Raw ids shown to players (snake_case ids, type ids, interpolated ids) | 10 |
| Dotted keys shown to players | 0 |
| Bracketed placeholders ([draft pending], [key]) | 14 |
| TODO / draft / placeholder / test / stub wording | 19 |
| Stray double periods or '...' | 7 |
| Inconsistent capitalisation of unit/faction names vs archive titles | 15 |
| Programmer words in player text (null, id, enum, tick, index, debug, sim, cell coordinates) | 5 |
| **Distinct flagged strings** | **57** |

### C# UI strings (src/Game) (27)

- `src/Game/Archive/ArchiveStats.cs:105` raw_id: list of raw ids joined into text -- `prerequisites.Count == 0 ? "nothing" : string.Join(", ", prerequisites);`
- `src/Game/Archive/ArchiveStats.cs:101` raw_id: interpolated id: {w.Id} -- `{w.Id}: {w.Damage} damage, range {range} cells, reload {Seconds(w.RateOfFire)}`
- `src/Game/Menu/HowToPlayContent.cs:101` punctuation: double period or ellipsis -- `Home centres on your base.   Ctrl+F5..F8 store a view, F5..F8 jump back to it.   F1 or ? lists every key.`
- `src/Game/Menu/HowToPlayContent.cs:102` dev_wording: dev word: for testing -- `Sandbox only: F3 occupancy, F4 paths and F10 ranges overlays (for testing).`
- `src/Game/Screens/ArchiveScreen.cs:401` placeholder_bracket: bracket text: [no text] -- `[no text]`
- `src/Game/Screens/DebriefModel.cs:89` raw_id: list of raw ids joined into text -- `lines.Add("New tech: " + (NewTech.Count == 0 ? "none" : string.Join(", ", NewTech)));`
- `src/Game/Screens/LanBrowserScreen.cs:130` punctuation: double period or ellipsis -- `Looking for games on the network ...`
- `src/Game/Screens/LobbyScreen.cs:235` punctuation: double period or ellipsis -- `Connecting to the host ...`
- `src/Game/Screens/LobbyScreen.cs:274` punctuation: double period or ellipsis -- `Downloading the map from the host ... {_client.MapPercent}%`
- `src/Game/Screens/MissionSelectModel.cs:35` raw_id: mission id used as the title fallback -- `service.Missions(faction).Select(m => new MissionSelectRow(m.Id, m.Exists ? TitleOf(dataDir, m.Id) : m.Id, ...`
- `src/Game/Screens/MissionSelectModel.cs:19` dev_wording: dev word: not written -- `not written yet`
- `src/Game/Screens/MissionSelectModel.cs:39` dev_wording: dev word: not written -- `This mission is not written yet.`
- `src/Game/Screens/ReplayViewerScreen.cs:150` programmer_word: programmer word: tick -- `REPLAY  {state}   tick {clock.Tick} / {clock.FinalTick} ({Percent(clock.Progress)})   view {view}`
- `src/Game/Screens/ReplayViewerScreen.cs:153` programmer_word: programmer word: tick -- `WARNING: the replay no longer matches the game data (differs from tick {_play.DesyncTick})`
- `src/Game/Screens/SkirmishSetupScreen.cs:565` punctuation: double period or ellipsis -- `
... and {mapErrors.Count - 4} more`
- `src/Game/Ui/Llm/LlmUiText.cs:10` punctuation: double period or ellipsis -- `AI commander (LLM)...`
- `src/Game/Ui/Llm/LlmUiText.cs:46` punctuation: double period or ellipsis -- `Set API key...`
- `src/Game/Ui/Llm/LlmUiText.cs:50` dev_wording: dev word: Test connection -- `Test connection`
- `src/Game/Ui/Llm/LlmUiText.cs:54` dev_wording: dev word: not wired -- `not wired yet`
- `src/Game/Ui/Llm/LlmUiText.cs:56` dev_wording: dev word: Test failed -- `Test failed.`
- `src/Game/Ui/Sidebar/BuildingUpgradeOffer.cs:40` raw_id: raw prerequisite id in text -- `return new BuildingUpgradeOffer(target, caption, cost, false, $"Needs {need}");`
- `src/Game/Ui/Sidebar/BuildingUpgradeOffer.cs:53` raw_id: ad-hoc id tidying (use CameoNames.Display) -- `return (u >= 0 && u + 1 < id.Length ? id[(u + 1)..] : id).Replace('_', ' ').ToUpperInvariant();`
- `src/Game/Ui/Sidebar/BuildingUpgradeOffer.cs:36` raw_id: interpolated id: {CaptionOf(to.Id)} -- `UPGRADE {CaptionOf(to.Id)} ${cost}`
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

### Archive titles, bodies, field notes (13)

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


## What was changed

- Raw ids: the debrief "New tech" line, the spectator inspect header, Archive weapon and "Requires" rows, the building-upgrade button and its
  "Needs" reason, and superweapon captions now go through `CameoNames.Display` (Archive title first, tidy fallback). Mission rows with no
  mission file show "Authority, mission 3" through `DisplayNames.Tidy` instead of `auth_03`.
- Placeholders and dev wording: 13 Technology entries had "[draft pending]" bodies; each now has a 3 to 5 sentence in-universe body in the
  voice of the neighbouring entries. "[no text]", "not wired yet", "Test connection", "(for testing)" and "not written yet" are replaced.
- Punctuation: trailing " ..." on menu rows, "Ctrl+F5..F8" (now "Ctrl+F5 to F8"), "... and N more".
- Programmer words: replay "tick N" is now a clock time, the spectator column "STRUCT" is "BASE", the dir_04 briefing's "THREE THOUSAND TICKS"
  is "TWO AND A HALF MINUTES", the credits no longer say "TOML".
- Capitalisation: named structures and units in mission text follow the Archive titles (Power Plant, Reactor Vent, Landing Craft, Pile House,
  Gun Battery). Subtitles are descriptive sentence case and are exempt.
- Tests: `tests/Scripting.Tests/M11_31/MissionLocaleCompleteTests.cs` (resolver and built-key runs, lb_02 post 1..3) extends
  `S22/MissionScriptKeysTests`; `tests/Game.Tests/M11_31/PlayerTextTests.cs` checks UiText, faction text switches, Archive titles and bodies, and that no
  rules id displays with an underscore. Existing tests that pinned old wording were updated.

## After (same scanner, after the fixes)

Strings scanned: C# UI strings (src/Game) 598, Mission locale.toml values 440, Archive titles, bodies, field notes 666, Briefings (briefing.md) 160, data/ui text (tips, credits) 79.

| Category | Flagged strings |
|---|---|
| Raw ids shown to players (snake_case ids, type ids, interpolated ids) | 0 |
| Dotted keys shown to players | 0 |
| Bracketed placeholders ([draft pending], [key]) | 0 |
| TODO / draft / placeholder / test / stub wording | 0 |
| Stray double periods or '...' | 0 |
| Inconsistent capitalisation of unit/faction names vs archive titles | 0 |
| Programmer words in player text (null, id, enum, tick, index, debug, sim, cell coordinates) | 0 |
| **Distinct flagged strings** | **0** |

### C# UI strings (src/Game) (0)

None.

### Mission locale.toml values (0)

None.

### Archive titles, bodies, field notes (0)

None.

### Briefings (briefing.md) (0)

None.

### data/ui text (tips, credits) (0)

None.

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


## Style question for the owner

The briefings (`data/missions/*/briefing.md`) are telegram style: ALL CAPS, numbered paragraphs, "STOP." The Archive field notes are
terse bulletins ("FIELD MAN 1 // ... STOP."). They are deliberate in-universe style, so the pass left them as they are. The scanner found no ids,
keys, stray periods or typos in them (only the dir_04 "TICKS" wording above was changed). Pick one direction:

Briefing, `lb_02`, paragraph 1.
- Telegram (now): `THE SURVEY CONVOY IS ASH AND THE AUTHORITY HAS NOT TAKEN THE HINT. IT HAS STARTED PUTTING POSTS ON THE OLD ROADS: A TURRET, A FEW RIFLES, A FLAG.`
- Plain radio prose: `The survey convoy is ash, and the Authority still has not taken the hint. It has started putting posts on the old roads: a turret, a few rifles, a flag.`

Field note, Authority Rifleman.
- Bulletin (now): `FIELD MAN 1 // SERVING CITIZEN. HOLDS GROUND. REQUIRES DECON AFTER ASHFALL EXPOSURE. STOP.`
- Plain radio prose: `Field Man 1. A serving citizen who holds ground. Send him through decon after any ashfall exposure.`

Recommendation: keep the telegram style for briefings (it reads as each faction's own medium) and keep it ALL CAPS only there; plain prose is
easier to read in a long Archive body. This is a decision for the owner, not made here.

