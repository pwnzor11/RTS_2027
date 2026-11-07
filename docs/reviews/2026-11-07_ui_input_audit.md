# UI and input audit (M11-01)

Session 14, 2026-11-07. Review only: no game code was changed. Source of the brief: `docs/reviews/2026-11-07_owner_campaign_feedback.md`.
Images are in `docs/reviews/img/2026-11-07_ui/` (`shot_*` are real screenshots of the running game at 1600x900, `mock_*` are wireframes).

## Summary (read this first)

1. The owner's six complaints are all real, and the code confirms each one. The causes are small and fixable; nothing needs a rewrite.
2. **Objectives are the worst gap.** A mission objective is only a line of text. It has no map position, so nothing can be drawn on the map, the minimap or at the screen edge. The tutorial says "move to the marked ground" and there is no marked ground on screen (`shot_02`).
3. **The camera has no edge scroll** and the keyboard scroll is slow (320 px per second). Zoom has only three steps (0.5, 1, 2) and starts at 1, where a rifleman is about 16 px tall on a 900p window.
4. **Selection is the thinnest part of the input path.** Click and box-drag only. There is no shift-add, no control groups, no double-click "select all of this type", no select-all. Selecting a *building* shows a white outline and nothing else (`shot_04`).
5. **A real bug is visible on screen:** the single-unit info panel is drawn on top of the debug line and the objectives panel, so the text overprints (`shot_03`).
6. **Clutter** comes from a debug line (tick, hash, fps) shown to every player, four lines of key hints, and a stack of up to five text panels in the left column (`shot_02`).
7. **Feedback is mostly missing:** no cursor change except Sell and Repair, no move-order marker, no rally-point flag, no "attack-move armed" indicator in skirmish or campaign, and under-attack and low-power alerts are audio only.
8. **Settings** has display, UI scale and volume only. The "UI scale" setting does not reach the sidebar at all (it is read in one place, the sidebar uses another). There is no controls page, no edge-scroll option and no colour-blind option. The pause menu cannot open Settings.
9. **Readability** is a palette problem as much as an art problem: road, ash, water and cliff are within 1.06 to 1.2 contrast ratio of each other, so they read as one colour. Building idle animations run at 4 frames per second on every building, all the time.
10. **Recommendation:** selection info as a bottom-centre card (option B) because the build menu is the heart of this game and should stay one click away; edge scroll plus a Controls page plus the overlap fix as the first batch (all small, no design decision). The ranked plan is in section 6.

## Method and limits

- **Screenshots worked.** I could not use my own build (the `dotnet` lock queue was behind the full test suite). I ran a Debug `Game.dll` that the `s14-bal` worktree had already built. `git diff integ-s14 wp/s14-bal -- src/Game` is empty, so the UI code is identical to the code audited here. The binary was copied to the scratchpad and run read-only against this worktree's `data/`. It ran under `xvfb-run` at 1600x900 with Mesa software GL (about 6 to 30 fps), driven with `xdotool` under a minimal window manager, captured with ImageMagick.
- **Not captured:** a group of units selected (my scripted drag did not select; that is probably the scripted mouse rather than a game bug, but I could not confirm it), resource fields (none in view on the tutorial map), building construction animation, attack-move, under-attack alerts. Those findings come from code and data and are marked "code".
- `file:line` references are to this worktree at `fb8d831`.

---

## 1. Findings

Severity: **blocker** = a player cannot do the thing or cannot understand the game; **major** = clearly hurts play, fix soon; **minor** = polish.
Size: S = under half a day, M = 1 to 3 days, L = more than 3 days or touches the sim. Tag: **M11** = UI/code, **M10** = art.

### 1.1 Camera

| ID | Sev | Finding | Evidence | What other games do | Fix | Size |
|---|---|---|---|---|---|---|
| C1 | major | **No edge scrolling.** The view moves only with arrow keys, middle-drag or the minimap. This is the owner's item 3. | `GameScreen.cs:426-439` (keys and middle-drag only); no mouse-edge code anywhere in `src/Game` | Red Alert, C&C, SC2, AoE II DE and OpenRA all scroll when the cursor touches the window edge. All of them let you turn it off. | Add an edge band (about 6 px, plus a larger "soft" corner), scroll speed by distance, only while the window is focused, off while typing in chat or console. The right-hand band must trigger over the sidebar too (the sidebar is the right edge of the window). Setting to turn it off. | S |
| C2 | major | Keyboard scroll is slow and does not scale with zoom or resolution: a fixed 320 screen px per second. Crossing one 20-cell screen takes 4 s; a 64-cell map edge to edge about 13 s. | `GameScreen.cs:38`, `:435` | Typical edge scroll crosses a screen in about 1.5 s; speed is a setting. | Speed setting (default about 2.5x current), faster at zoom-out, hold Shift for double speed. | S |
| C3 | major | Zoom has three fixed levels and starts at 1. Units look small (owner item 4): rifleman frame is 32x32 px on a 64 px cell, the body is about 16 px; the selection bracket for infantry is 18 px wide. | `Camera.cs:291-293`, `UnitRenderer.cs:18-21`, `data/sprites/infantry/exp_marine.sprite.toml` (`frame_w = 32`), `shot_03` | SC2 and AoE II DE have smooth zoom; Remastered C&C has a zoomed tile set. | Default zoom by window height (for example 1.5 at 900p, 2 at 1440p), more steps (0.75, 1, 1.5, 2), remember the choice in settings. Zoom toward the cursor, not the centre. Needs a look at point sampling at non-integer zoom (sprites use `PointClamp`, so 1.5 can look uneven; 1, 2 are clean). Owner pick: see section 6. | M |
| C4 | minor | Wheel zoom jumps without animation and re-centres on the view origin, so the thing under the cursor moves away. | `Camera.cs:306-309` (`ZoomBy` only changes the index) | Zoom to cursor is standard in every modern RTS. | Keep the world point under the cursor fixed while zooming. | S |
| C5 | minor | No jump-to-base key, no "go to last alert" key, no camera bookmarks. The game centres once at start and on a mission camera cue. | `GameScreen.cs:303`, `:446` (cue is honoured once, `MissionCameraCue.cs`) | Home or Backspace to base, Space to last alert (SC2, AoE), F-key bookmarks (OpenRA). | Home = base, Space-or-other = last alert, Ctrl+F5..F8 save/recall a view. (Space is already Pause; see K3.) | S |
| C6 | minor | The minimap is a fixed square, 40% of window height, even when the map is not square. On the tutorial map a third of it is empty black. | `SidebarLayout.cs:147-149`, `shot_02` | OpenRA and AoE fit the radar to the map shape. | Size the minimap to the map aspect, give the freed space to the build grid. | S |
| C7 | minor | Minimap clicks: left click and drag move the view (good); right click orders a plain Move (no attack-move, no feedback). | `SidebarPanel.cs:339-351`, `:435-438` | SC2 right-click on minimap is move, A+click is attack-move. | Make armed attack-move work on the minimap; flash the destination. | S |

### 1.2 Selection

| ID | Sev | Finding | Evidence | What other games do | Fix | Size |
|---|---|---|---|---|---|---|
| S1 | **blocker** | **No army-management toolkit.** Selection can only be *replaced*: no shift-click add or remove, no control groups, no double-click or Ctrl+click for "all units of this type on screen", no select-all, no idle-unit cycling. Not one `Shift`/`Control` check exists in the game screen. | `Selection.cs:34-39` (`Replace` is the only mutator), `GameScreen.cs:520-557`; no `LeftShift`/`LeftControl` in `GameScreen.cs` or `Controls/` | Red Alert: shift-add, Ctrl+number groups, Tab to cycle. SC2: all of it plus double-click. OpenRA: all of it. | Shift-click toggle; Ctrl+1..9 assign, 1..9 recall (double-tap centres the camera); double-click or Ctrl+click selects same type on screen; Ctrl+A all army; Shift-drag adds. Show group numbers on the unit icons. | M |
| S2 | major | Selecting a building shows only a 2 px white outline. No name, no health, no queue, no rally flag. The How to Play page says "its health and what it can do show in the sidebar", which is not true. | `GameScreen.cs:1009-1019`, `shot_04`, `Menu/HowToPlayContent.cs:52` | Every RTS shows a panel for the selected structure. | The selection card (section 3) also covers a building: name, health bar, power use, production state, rally point, Sell/Repair shortcuts. | M |
| S3 | major | **Unit info panel overlaps other HUD text.** It is placed at the top-left of the map view, exactly where the debug line and the objectives panel already are, so the three overprint ("Rifleman" over "hash ...", "Kills" over "OBJECTIVES"). | `SidebarPanel.cs:664` (origin `MapView.X + 8, Y + 8`) vs `GameScreen.cs:1132` and `MissionOverlay.cs:76`; `shot_03` | n/a (a layout bug) | Quick fix: move the panel below the other panels. Real fix: the selection card (owner item 5). | S |
| S4 | major | Info is shown for exactly one selected unit; with two or more selected there is nothing at all. | `SidebarPanel.cs:633` (`_selection.Count != 1`) | SC2 and AoE show a grid of the group. | Group card (mock 02, 04, 06). | M (with S2) |
| S5 | minor | Box select takes units only, never buildings; a click on a unit overrides a building underneath. Own units only: enemies cannot be clicked for info (fine for C&C, but modern games show enemy info on click). | `Selection.cs:48-80`, `GameScreen.cs:541-552` | Same in C&C. SC2 shows enemy info on click. | Optional: click an enemy to show a read-only card (the spectator already has this: `SpectatorInspect`). | M |
| S6 | minor | Selection feedback is four white corner brackets, 1 to 2 px, plus a health bar. At default scale this is small and white-on-light-ground is weak. | `UnitRenderer.cs:69-76`, `shot_03` | SC2 and AoE use a coloured ground ring in the player colour plus an optional team ring. | Ground ellipse in the player colour with a dark outline; keep brackets as an option. | S |
| S7 | minor | Selection sound only plays for a new selection; no sound for deselect or for an invalid click. | `GameScreen.cs:537`, `:546` | Standard. | Optional. | S |

### 1.3 Orders

| ID | Sev | Finding | Evidence | What other games do | Fix | Size |
|---|---|---|---|---|---|---|
| O1 | major | **No visual confirmation of an order.** A right-click move or attack gives a sound only. No click marker, no line to the target, no flash on the targeted enemy. | `OrderController.cs:153-217` (issues commands, draws nothing), `shot_08`; audio `AudioEventDeriver.cs:199` | SC2 green circle, AoE flag, OpenRA move flash. | Expanding ring at the destination (0.4 s), red ring on an attack target, dashed line while a group is selected. | S |
| O2 | major | **Attack-move is armed by pressing A, then a *right* click, and the only indicator exists in the sandbox.** In skirmish and campaign there is no sign that A is armed. `A` toggles, `Esc` does not cancel it. | `OrderController.cs:131-133`, `GameScreen.cs:1085-1091` (the "ATTACK-MOVE" text is inside `!_skirmishMode`) | Red Alert: A or Ctrl-click, left click to commit. SC2: A, left click. OpenRA: A, left click. A target cursor shows. | Show an attack-move cursor and a banner while armed; let **left** click commit, keep right click as an alias; Esc cancels. | S |
| O3 | major | **No order queue or waypoints.** Shift is never read, so units cannot be given a path. | grep: no `Shift` in `Controls/`; `OrderController` issues one order per unit | Red Alert and SC2 queue with Shift. | Shift+right-click queues move/attack. This needs a sim-side order queue (the sim has one mission slot per unit), so it is large. Do after S1. | L |
| O4 | major | **Rally point is invisible.** A factory selected plus right click sets the rally, with no flag or line drawn and no sound. The only mention is in the help text. | `GameScreen.cs:565-571`, `SidebarCommands.cs:100`; no rally drawing in `Render/` | Red Alert shows a flag; SC2 draws a line from the building. | Draw a flag at the rally cell and a faint line from the selected factory. Show it in the selection card. | S |
| O5 | minor | Guard is "guard the spot I stand on" with no feedback and no radius; Scatter and Stop also give nothing but a sound. | `OrderController.cs:134-148` | Same in Red Alert (guard = stay). | Small icon above units on guard; part of the order feedback pass. | S |
| O6 | minor | Right-clicking a selected factory (a building) with units unselected sets the rally; with a mixed idea (units and a building cannot be selected together) it is consistent, but a player who wants to *move* units while a factory is selected must deselect it first, with no hint. | `GameScreen.cs:568-575`, `ClearBuildingSelection` calls | n/a | Covered by the selection card showing "Right-click: set rally point". | S |
| O7 | minor | The context order is purely right-click. Salvage, enter, garrison, capture, repair and unload are all decided by what is under the cursor, with no cursor preview. | `OrderController.cs:176-215` | Red Alert and Remastered show a different cursor per action. | Cursor per action (move, attack, enter, capture, repair, salvage, blocked). See F2. | M |

### 1.4 Building placement and production

| ID | Sev | Finding | Evidence | What other games do | Fix | Size |
|---|---|---|---|---|---|---|
| P1 | major | The placement ghost is a flat green or red rectangle. It does not show the building, and red gives **no reason** (too far from base? on a unit? on cliff?). A click on red silently does nothing. | `SidebarPanel.cs:186-195`, `:365-374`, `shot_06` | Red Alert shows the footprint grid with per-cell red or green and the build radius. | Draw the building sprite at 50% under the footprint, colour cells individually, show one line of reason near the cursor ("Too far from your base", "Blocked by a unit"). The sim already says valid or not; the reason needs a small query. | M |
| P2 | minor | No build-radius display while placing (Red Alert greys out the map outside it). | `SidebarPanel.cs:186-195` | Same. | Dim cells outside the buildable area. Needs the same query as P1. | M |
| P3 | minor | Placement cancel works (Esc, right-click, the "Right click or Esc to leave" tooltip) and is reasonable. No rotation is needed. | `SidebarPanel.cs:388-392` | n/a | None. | n/a |
| P4 | major | Cameos have no information beyond cost: no build time, power use, hotkey, prerequisites (only on locked ones). No way to "hold" or repeat production. Left click queues, right click cancels the front item of that type. | `SidebarPanel.cs:560-569`, `SidebarCommands.cs:34-60` | Red Alert: hold on right click, then cancel. SC2: shift-click queues five. | Tooltip: cost, time, power, short description, what it needs. Right click once = hold, twice = cancel. Shift-click queues 5. | M |
| P5 | minor | Queue is only visible as a count badge on the cameo and a veil; no ordered queue strip. | `SidebarPanel.cs:787-821` | AoE II and SC2 show a queue strip with icons. | Small queue strip under the tabs (can live in the selection card for a selected factory). | M |
| P6 | minor | "LOCKED" cameos draw the label twice (a ghost "LOCKED" under the real one) and the "READY" overlay is clipped by the cameo (`shot_05`). | `SidebarPanel.cs:806-811`, `shot_02`, `shot_05` | n/a | Draw the label once and size it to the cameo. | S |
| P7 | minor | Sell and Repair are mouse-only modes with no hotkey. | `SidebarPanel.cs:152-157` (only tab keys) | Red Alert has hotkeys. | Hotkeys, see K1. | S |

### 1.5 Hotkeys

| ID | Sev | Finding | Evidence | Fix | Size |
|---|---|---|---|---|---|
| K1 | major | **Hotkeys are fixed in code, undiscoverable in play, and cannot be rebound.** There is no settings page for them. The only list is the four-line help block (on screen all the time) and How to Play, which is reachable only from the main menu. | `GameScreen.cs:40-47`, `SidebarPanel.cs:152-157`, `OrderController.cs:127-150`; `Settings.cs` has no key fields | Controls page (owner item 2), then a rebinding table (mock 09). Rebinding is M; showing the page is S. | S (page) / M (rebind) |
| K2 | major | **Debug keys are live in skirmish and campaign.** F2 cycles the fog viewer (a vision cheat: omniscient view), F3, F4 and F5 turn on debug overlays, and `.` single-steps the sim. Only F1 (console) is gated. | `GameScreen.cs:406` (F2 runs for every non-spectator game), `:410`, `:413-415` | Gate F2 to sandbox and spectator, and F3-F5 and `.` to a Diagnostics setting. | S |
| K3 | minor | Space is Pause, but Space is also the usual "go to last alert" key. Q W E R T Y are taken by sidebar tabs, which blocks the common WASD camera layout. | `GameScreen.cs:405`, `SidebarPanel.cs:152-157` | Keep QWERT tabs (it is the left-hand sidebar-key convention in this game). Camera is arrows plus edge plus minimap. Pause on Pause key and P. | S |
| K4 | minor | A, S, G, X, D act on the selection even when the key was meant for a typed field (chat uses Enter to open, which is handled), and `S` Stop also fires when a factory is selected (harmless). | `GameScreen.cs:379-382` | Fine. | n/a |
| K5 | minor | No Sell/Repair/Select-all/Stop-all keys; no key for "next idle unit" or "select base builder". | none present | Add with the control-group batch. | S |

### 1.6 Feedback

| ID | Sev | Finding | Evidence | What other games do | Fix | Size |
|---|---|---|---|---|---|---|
| F1 | major | **Under-attack, low-power, low-funds and "ready" alerts are audio only.** The audio events exist (`UnderAttack`, `LowPower`, `LowFunds`, `UnitReady`, `ConstructionComplete`), but nothing draws on screen. A player with sound off, or looking at the other end of the map, gets nothing. Only a thin red power bar changes. | `Audio/AudioEventKind.cs:21-47`, `AudioEventDeriver.cs:89`, `:172`; `SidebarPanel.cs:667-678` | All RTS games show a text alert and ping the minimap at the attacked location; Red Alert's minimap flashes. | Alert stack (top-right of the map, 4 s) plus minimap ping ring plus "jump to last alert" key. Reuse the audio event stream, so it needs no sim change. | M |
| F2 | major | The mouse cursor never changes during play, except Crosshair for Sell and Hand for Repair. No hover highlight on units, enemies or buildings. | `SidebarPanel.cs:865-883` | Red Alert cursors for move, attack, enter, blocked. | Software cursor sprites drawn at the mouse, per context. | M |
| F3 | major | **Health bars are not colour-blind safe.** They run green, yellow, red on a black plate; the minimap uses green for own units and orange-red for enemies; building placement uses green and red. Red-green colour blindness (about 8% of men) cannot tell these apart. | `HealthBars.cs:24-27`, `:81`, `Minimap.cs:21-23`, `SidebarPanel.cs:192` | SC2 has a colour-blind mode; AoE II DE uses team colours and shapes. | Use a blue to yellow to orange ramp for health in a "colour-safe" mode, add a pattern (tick marks) to the bar, use the player colour for minimap blips, and add a shape cue (check/cross) to the placement ghost. | M |
| F4 | minor | Health bars appear only on selected or damaged units, and infantry bars are 16 px wide, 4 px high. Rank pips are tiny. | `HealthBars.cs:18-23`, `:47` | SC2 shows an option "always show health". | Setting "health bars: always / hurt or selected"; scale bar with zoom (it already scales). | S |
| F5 | minor | Selected rally and move paths are not drawn; the sandbox path overlay (F4) is a debug tool only. | `GameScreen.cs:414` | See O1, O4. | n/a | n/a |
| F6 | minor | The Esc key opens the pause menu even when it was used to leave armed attack-move or deploy (they are not cancelled by Esc; only sidebar modes, superweapon and unpack are). | `GameScreen.cs:384-392` | Esc cancels the armed order first. | S |

### 1.7 HUD layout and clutter

| ID | Sev | Finding | Evidence | Fix | Size |
|---|---|---|---|---|---|
| H1 | major | **A debug line is shown to every player at all times:** `tick N  hash 16-hex  fps  zoom`. In campaign and skirmish it is useless to a player and pushes the objectives panel down. | `GameScreen.cs:1078`, `shot_02` | Move behind a Diagnostics setting (F10 or Settings). Keep `PAUSED` as a banner. | S |
| H2 | major | **Four lines of key hints sit bottom-left permanently** (owner item 2). They are about 800 px wide at 900p, longer than half the map view. | `GameScreen.cs:41-47`, `:1136-1138`, `shot_02` | Remove from HUD; Controls page in Settings plus a `?` button or F1 overlay. | S |
| H3 | major | **Left-column text stack.** The debug line, objectives, tutorial ticker, dialogue box and help block all draw down the left edge, each as a separate dark panel. Objective and dialogue lines wrap at 78 characters in 2x text, so they cover about 500 px of the map. | `MissionOverlay.cs:22`, `:89-103`, `GameScreen.cs:1132-1139`, `shot_02` | One objective line top-centre (expands on hover), subtitles in one bottom-left box, alerts top-right (mock 08). | M |
| H4 | major | **Two different UI scale rules.** Panels drawn by the game screen use `Settings.EffectiveUiScale` (integer: 2 at 900p, 3 from 1000p up), while the sidebar uses `UiScale.FromBackbuffer` (1.5 at 900p, 2 over 1200p). The user setting reaches only the first. `UiScale.Override` is never set anywhere, so Settings > UI scale does **nothing** to the sidebar, cameos, tooltips or tabs. | `GameServices.cs:71`, `Settings.cs:54-55`, `Ui/UiScale.cs:12-22`, `SidebarPanel.cs:131`, `SettingsScreen.cs:133-134`; grep finds no assignment to `UiScale.Override` | One scale function used everywhere; the setting feeds it; allow 75% to 200% in 25% steps, not just 1x to 4x. | M |
| H5 | minor | Text size equals UI scale: there is no separate text-size option. HUD text at 900p is 2x of a 7 px font (14 px) which reads well; sidebar text at 1.5x is smaller (about 10 px for names, 11 px for tabs). The sidebar tab labels, cameo names and tooltips are the smallest text in the game. | `SidebarPanel.cs:243-249`, `:746` (0.75 of scale for names), `shot_02` | Raise the minimum text size to 12 px; add a text-size option. | S |
| H6 | minor | Power is a 10-unit-wide vertical bar with no number or label; the tooltip needs a hover. At low power the only cue is the bar turning red and the audio line. | `SidebarLayout.cs:151-154`, `SidebarPanel.cs:667-678` | Number readout under credits (`+120 / -150`), blink at low power, alert (F1). | S |
| H7 | minor | The Pause menu has no Settings, Controls or How to Play. A player cannot lower the volume or look up a key mid-game. | `PauseScreen.cs:28-37` | Add Settings and Controls to Pause. | S |
| H8 | minor | The sidebar is 20% of the width (320 px at 1600). Of that, about 300 px of height is a minimap that is mostly black on a fogged or non-square map. | `SidebarLayout.cs:106-110`, `shot_01` | See C6. | S |

### 1.8 Objective guidance

| ID | Sev | Finding | Evidence | Fix | Size |
|---|---|---|---|---|---|
| G1 | **blocker** | **An objective has no location.** `MissionObjective` is `(Id, Text, Primary, Status, Hidden)`. The only guidance is text, and one optional camera cue that is honoured once and then forgotten. The tutorial says "move them to the marked ground" and nothing on screen is marked (`shot_02`, `shot_08`, and the instructor says "the east"). | `Scripting/MissionObjective.cs:11`, `Screens/MissionObjectiveLines.cs:12-30`, `MissionCameraCue.cs`, `GameScreen.cs:446` | Add an optional location (cell and radius, or "follow this unit/building") to objectives in the mission script layer, then draw it (section 4). Scripts that already call a camera hint can reuse the cell. | M |
| G2 | major | Objective panel has no emphasis on *new* or *changed* objectives, no timer emphasis, and long objectives are cut at the map edge when the sidebar is in the way (`shot_02`: "...to the marked ground" touches the black region). | `MissionOverlay.cs:89-92`, `shot_02` | Banner on change, flash the objective panel, wrap to a fixed width. | S |
| G3 | minor | No way to re-read the briefing during play. | `MissionOverlay` has no briefing key | O opens the objective list and the briefing text. | S |

### 1.9 Readability (terrain, resources, buildings)

| ID | Sev | Finding | Evidence | Fix | Size / Tag |
|---|---|---|---|---|---|
| R1 | major | **Terrain types are tonally identical.** Contrast ratios (WCAG formula) between land base colours: road to ash 1.20, road to water 1.13, ash to water 1.06, ash to cliff 1.10, water to cliff 1.17, rough to shallow 1.00. Only clear ground and rock separate from the dark group. That is the "one-dimensional map" the owner describes (item 7). The look is flat colour plus noise, with a 1-texel blend at borders. | `data/theaters/rustbelt.toml:10-55`; `Render/TerrainBake.cs:102-115`; `shot_01` | Re-tune the palette: water clearly blue and lighter, road lighter with edge lines, cliff with a light top edge and dark face, ash as a different hue; add a cliff shadow and a shoreline. A palette change is data only. A proper tile set is later. | M (palette, M11-07) / L (tiles, M10) |
| R2 | major | **Resources read as a splatter.** Pitchblende is a 2x2-texel greenish-yellow chunk laid at random with up to 60% coverage plus one-in-seven bright glints; ashfall is a dark speck (`#17171A`) at up to 45% coverage, which on ash ground (`#2E3033`) is only 1.2:1. No edge, no richness cue other than density. | `data/theaters/rustbelt.toml:57-70`, `TerrainBake.cs:81-97` | Draw a field as a *shape* (clustered blobs with a soft edge, a darker rim), keep glints rare and animated, add a minimap colour for fields, and show remaining amount with density. Harvester targets could pulse. | M (M11-08); art follow-up M10 |
| R3 | major | **Building animation is fast and constant.** Every building loops a 4-frame idle at 5 ticks per frame (20 ticks/s), which is a 4 Hz flicker on all 62 buildings at once; the 8-frame build animation plays in 2 s. 7 static buildings use `ticks_per_frame = 1`. | `data/sprites/buildings/*.sprite.toml` (`auth_conyard.sprite.toml:21` and 51 others; `Constants.cs:8` 20 ticks/s), `Render/SpriteStates.cs:50-69` | Quickest: a global idle slowdown factor in `SpriteStates` (for example 4x) with per-building override. Better: generator emits slower idle or fewer idle frames. Build animation about 3 to 4 s with an ease-out. | S (code) / M10 for the art generator |
| R4 | minor | Buildings and units are flat (owner item 10, already routed to M10). Selection brackets and house colours help but do not give depth. | owner feedback item 10 | Art polish run. | M10 |
| R5 | minor | Minimap colours are fixed own = green, ally = cyan, enemy = red, not the player colours. | `Minimap.cs:21-23` | Use player colours with a white outline for own units. | S |

### 1.10 Settings, accessibility and resolution

| ID | Sev | Finding | Evidence | Fix | Size |
|---|---|---|---|---|---|
| A1 | major | **Settings coverage is thin.** Present: full screen, window size (four fixed 16:9 sizes), UI scale, three volumes, LLM, About. Missing: controls and rebinding, edge scroll and speed, default zoom, diagnostics toggle, health bars, colour-blind mode, text size, subtitles, vsync or frame cap. | `SettingsScreen.cs:36-50`, `shot_07`, `Settings.cs:12-24` | Split Settings into pages (Display, Audio, Controls, Accessibility, AI), mock 09. | M |
| A2 | major | No colour-blind support anywhere (see F3). Player colours are chosen from a palette; red and green exist in it. | `Sprites/PlayerColours.cs`, `HealthBars.cs`, `Minimap.cs` | Colour-safe palette option, shapes or patterns for state, test with a colour-vision simulator. | M |
| A3 | minor | Window sizes cycle through 1280x720, 1600x900, 1920x1080, 2560x1440. A laptop at 1366x768 or a 16:10 monitor cannot pick its size, and a window the user resizes by hand is not remembered unless it matches. The window is resizable (`AllowUserResizing = true`) and the layout copes (the sidebar and map view are computed from the backbuffer each frame). | `SettingsScreen.cs:23-24`, `RtsGame.cs:80`, `SidebarLayout.cs:119-177` | Save the window size on exit, list the desktop's native modes, add borderless full screen. | S |
| A4 | minor | At small heights (720p) the UI scale drops to 1x, and the 7 px base font plus 0.75x names becomes very small. | `UiScale.cs:20-22`, `SidebarPanel.cs:746` | Minimum readable text size, see H5. | S |
| A5 | minor | The audio for alerts can be heard but there are no subtitles for non-dialogue sounds; dialogue has text (good). | `MissionDialoguePresenter.cs` | Alert stack (F1) doubles as captions. | S |

---

## 2. Cross-cutting notes

- **What is good already:** build tabs with Q W E R T, tier groups with "needs ..." text on locked cameos, a tooltip for every cameo, minimap drag, minimap fog, right-click-to-cancel in production, a placement ghost with valid/invalid colour, Esc backs out of modes, the whole HUD uses a clear font. The input layer (`InputFrame`, "consume" so a click on the sidebar never reaches the map) is clean and makes the additions below easy.
- **One structural cause for several findings:** the HUD is a set of independent panels that each pick their own corner (`GameScreen.DrawHud`, `MissionOverlay.Draw`, `SidebarPanel.DrawUnitInfo`). That is why panels overlap (S3). The fix in section 5 is a small layout step that gives each region a slot.
- **The `.` step key, F-key overlays and the fog switch** are developer tools. They are useful and should stay, behind a Diagnostics setting (K2).

---

## 3. Proposal (a): selection info panel

All three options show the same content: portrait, name, health bar and number, rank and kills, a few stats, and order buttons that double as hotkey reminders. A building shows name, health, power, production and rally. The wireframes use a plain frame and the real sidebar proportions (320 px at 1600x900).

| | A. Sidebar splits (owner's idea) | B. Bottom-centre card (recommended) | C. Tabbed sidebar |
|---|---|---|---|
| Wireframes | `mock_01_selection_A_single.png`, `mock_02_selection_A_multi.png` | `mock_03_selection_B_single.png`, `mock_04_selection_B_multi.png` | `mock_05_selection_C_single.png`, `mock_06_selection_C_multi.png` |
| Where | Card at the top of the sidebar; build grid shrinks to a two-row strip; restores when nothing is selected | Card over the lower edge of the map, centred in the map view; sidebar is unchanged | Sidebar gets BUILD and SELECTED tabs; selecting switches tab |
| Build menu while a unit is selected | Visible but only two rows; scrolling needed for tier 2 and 3 | Fully visible | Hidden unless you switch (badge shows when something is ready) |
| Multi-select | Small icon grid (about 12 icons) | Wide icon grid (24 icons), click an icon to pick one | Icon grid, plenty of room |
| Map space used | None | About 20% of map height at centre-bottom (hides terrain in front of the base) | None |
| Layout jumpiness | High: the build grid resizes on every select and deselect, moving cameos under the mouse | None | Medium: tab flips on select (can be set to stay on BUILD) |
| Familiar from | Red Alert 2 style sidebar only | SC2, AoE II DE, OpenRA (similar) | Some C&C clones |
| Cost | M | M | M |

**Recommendation: B.** Reasons: (1) in a C&C-style game the sidebar *is* the economy, and a build menu that shrinks every time you click a tank is the most common thing to fight against while micro-managing a battle; (2) B has no layout movement, so cameos never jump under the cursor; (3) it handles 20 or more units and a selected building with the same card; (4) it removes the card from the top-left, which solves the overlap bug (S3) for good. Its cost is the map space it covers; it can be made smaller at low resolutions and it only exists while something is selected. If the owner prefers the sidebar, **A** is the runner-up, but I would then keep the strip at three rows and stop the grid from jumping by animating the resize. **C** is the weakest: it hides production, which is what players check most.

## 4. Proposals (b) objective guidance, (c) decluttered HUD, (d) controls page

### (b) Objective guidance — `mock_07_objective_guidance.png`

The four tools are cheap together and all depend on one thing: giving an objective an optional location (G1).

| Tool | What it does | Size | Pick? |
|---|---|---|---|
| 1. Edge-of-screen arrow | When the objective is off screen, an arrow on the nearest screen edge with a label and distance ("34 cells east") | S after G1 | Recommended |
| 2. Ground marker | A pulsing diamond and a short label on the target cell when it is on screen | S after G1 | Recommended |
| 3. Minimap ping | A ring on the minimap at the objective, plus a flash when a new objective arrives | S | Recommended |
| 4. Jump key | `J` (or Home-alt) centres the camera on the current objective; `Tab` cycles objectives | S | Recommended |
| 5. Objective list panel | Collapse the left-column text to one line; `O` opens the full list and the briefing text | S | Recommended with 1 to 4 |
| 6. Auto camera cue on new objective | A short pan when an objective appears (the mission script already supports one cue) | S | Optional; some players dislike camera moves |

### (c) Decluttered HUD — `mock_08_decluttered_hud.png`

Top: one objective line (top-centre). Top-right of the map: alert stack (attack, power, ready, funds). Bottom-left: one subtitle box. Bottom-centre: selection card (option B), only when something is selected. No debug line, no key hints, a small `?` for the controls overlay. The sidebar stays as is, with a power number and a minimap that fits the map shape.

### (d) Controls page — `mock_09_controls_page.png`

Settings becomes pages: Display, Audio, Controls, Accessibility, AI commander. Controls holds the edge scroll and speed, default zoom, diagnostics toggle, and the full key table grouped by Camera, Selection, Orders and Build/System, with click-to-rebind, conflict warnings and "Reset to defaults". The same page opens from Pause and from `F1` in play. Shipping the table as read-only first is S; rebinding is the extra M.

---

## 5. Notes for the implementer (not needed to decide)

- **Edge scroll (C1):** in `GameScreen.Update` next to the existing arrow-key block (`:426`). Use `_s`/game `IsActive` so a cursor leaving the window does not scroll; apply `Camera.PanScreen`. Convert the pan speed to world pixels so zoom does not change how fast it feels.
- **Layout slots (H3, S3):** one small `HudLayout` (top-left, top-centre, top-right, bottom-left, bottom-centre) and each panel asks for a slot. This replaces the hand-stacking in `MissionOverlay.ObjectivesOrigin` and `SidebarPanel.DrawUnitInfo`.
- **Alerts (F1):** derive from the audio event list in `AudioEventDeriver`; add a `GameAlert` list for the HUD and the minimap. No sim change.
- **Objective location (G1):** the scripting side (`Rts.Scripting`) owns the type; the UI side only reads it. Touching it is why this is M rather than S.
- **Order queue (O3):** the only item that needs sim work (a per-unit order queue); keep it out of the first batches.

---

## 6. Prioritised plan

Roadmap ids M11-02 to M11-09 already exist for the owner's own items; new work is added as M11-10 sub-items after the owner picks.

### 6.1 Quick wins (S, no design decision) — do first, one batch

| Order | Item | Finding | Roadmap | Tag |
|---|---|---|---|---|
| 1 | Edge scrolling with an on/off setting | C1 | M11-02 | M11 |
| 2 | Scroll speed setting, faster default, Shift boost | C2 | M11-02 | M11 |
| 3 | Debug line behind Diagnostics; key hints off the HUD; Controls page (read-only table) in Settings, `?`/F1 overlay, Settings in Pause | H1, H2, H7, K1 | M11-03 | M11 |
| 4 | Fix the overlap of the unit info panel (move it under other panels until option B lands) | S3 | M11-10 | M11 |
| 5 | Gate F2, F3, F4, F5 and `.` in skirmish and campaign | K2 | M11-10 | M11 |
| 6 | Attack-move armed indicator and cursor; Esc cancels armed orders; left click commits | O2, F6 | M11-10 | M11 |
| 7 | Move-order ring, attack-target ring, rally flag and line | O1, O4 | M11-10 | M11 |
| 8 | Building animation: idle 4x slower, build 3 to 4 s | R3 | M11-09 | M11 (code), M10 (generator) |
| 9 | Health bar choice "always / hurt or selected", bigger infantry bars | F4 | M11-10 | M11 |
| 10 | Home key to base; Ctrl+F5..F8 view bookmarks | C5 | M11-10 | M11 |
| 11 | Locked and Ready cameo text drawn once and sized to fit | P6 | M11-10 | M11 |
| 12 | Power number readout and low-power blink | H6 | M11-10 | M11 |
| 13 | Minimap sized to the map shape | C6, H8 | M11-10 | M11 |

### 6.2 Needs the owner's pick

| Decision | Options | My recommendation | Items |
|---|---|---|---|
| Selection info panel | A, B or C (section 3) | **B** | S2, S3, S4, P5 → M11-04 |
| Objective guidance set | Tools 1 to 6 (section 4b) | Tools 1 to 5; tool 6 optional | G1-G3 → M11-05 |
| Default zoom and unit size | (i) default zoom 1.5 at 900p with more steps; (ii) keep zoom, draw units 25% larger; (iii) both | (i) first, check the look at non-integer zoom, then decide on (ii) | C3, C4 → M11-06 |
| Camera keys | Keep QWERT for tabs (arrows plus edge plus minimap) or move tabs so WASD can scroll | Keep QWERT; add WASD only if the owner wants it and tabs move | K3 |
| Attack-move click | Right click (now), left click, or both | Left commit, right click alias | O2 |
| Colour-blind approach | Palette option only, or palette plus shapes and patterns | Both (health ticks, ghost shapes, minimap player colours) | F3, A2 |
| Key rebinding in 1.0 | Read-only table or full rebinding | Read-only now, rebinding after control groups land | K1 |

### 6.3 Larger work

| Item | Finding | Size | Roadmap | Tag |
|---|---|---|---|---|
| Selection toolkit: shift-add, control groups, double-click same type, select-all, group numbers on icons | S1 | M | M11-10 | M11 |
| Selection card (chosen option) including building info and group grid | S2, S4 | M | M11-04 | M11 |
| Objective location in the script layer and the on-map guidance | G1 | M | M11-05 | M11 |
| Alert stack, minimap pings and "last alert" key | F1 | M | M11-10 | M11 |
| One UI scale rule; 75% to 200%; text-size option | H4, H5 | M | M11-10 | M11 |
| Cursor sprites per context | F2, O7 | M | M11-10 | M11 |
| Placement ghost with sprite, per-cell colour, reason line, build radius | P1, P2 | M | M11-10 | M11 |
| Cameo tooltips (time, power, description), hold, shift-queue | P4 | M | M11-10 | M11 |
| Terrain palette re-tune (contrast between road, ash, water, cliff; shore and cliff edges) | R1 | M | M11-07 | M11 (palette), M10 (tiles) |
| Resource field look (shapes, rim, minimap colour) | R2 | M | M11-08 | M11 (shape), M10 (art) |
| Colour-safe mode and accessibility page | F3, A1, A2 | M | M11-10 | M11 |
| Key rebinding | K1 | M | M11-10 | M11 |
| Waypoint queue (Shift+right-click) | O3 | L (sim order queue) | later | M11 + sim |
| Terrain tile set, building depth, units art | R1, R4 | L | M10 | M10 |

### 6.4 Suggested batching

1. **Batch 1 (S, about 3 days):** items 1 to 7 and 11 to 13 of 6.1. Result: the screen is clean, the camera is easy, orders give feedback, and the overlap bug is gone.
2. **Batch 2 (after the owner's picks):** selection card, objective location and guidance, default zoom.
3. **Batch 3:** selection toolkit, alerts and one UI scale rule, accessibility page.
4. **Batch 4:** terrain and resource look (palette first), then placement ghost and cursors.
5. **Later:** waypoint queue (sim), rebinding, M10 art.

## 7. Evidence index

| Image | Shows |
|---|---|
| `shot_01_skirmish_start_nothing_selected.png` | Skirmish start: debug line, four-line hint block, sidebar, empty minimap area |
| `shot_02_campaign_mission_start.png` | Tutorial start: left-column stack (debug, objectives, instructor, hints); no marked ground in view |
| `shot_03_unit_selected_overlap_bug.png` | One rifleman selected: info panel overprints the debug line and objectives; tiny unit and bracket |
| `shot_04_building_selected_outline_only.png` | Construction Yard selected: white outline, no info |
| `shot_05_build_ready_tooltip.png` | Power Plant ready, tooltip, clipped READY text |
| `shot_06_placement_ghost_red.png` | Flat red placement rectangle, no reason given |
| `shot_07_settings_menu.png` | Settings: display, UI scale, volume only |
| `shot_08_move_order_no_feedback.png` | After a right-click order: nothing drawn |
| `mock_01` to `mock_06` | Selection card options A, B, C, single and multi |
| `mock_07_objective_guidance.png` | Objective tools together |
| `mock_08_decluttered_hud.png` | Whole-HUD proposal |
| `mock_09_controls_page.png` | Settings > Controls |
