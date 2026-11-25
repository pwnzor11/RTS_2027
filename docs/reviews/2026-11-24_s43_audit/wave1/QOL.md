# QOL — quality-of-life ideas from other RTS games, not yet built in RTS_2027
Scope read: s18 wave1 qol-cnc.md and qol-modern.md (idea lists); docs/ROADMAP.md (M11, M13-15 lines); docs/DECISIONS.md (greps: colour-blind, patrol, power, last-used setup); src/Game Controls/GameAction.cs, Selection.cs, OrderController (grep), Settings.cs, Ui/Alerts/AlertKind.cs, Ui/Sidebar listing, Ui/Card listing, Screens listing, Screens/SlotOptions.cs, SkirmishSetupScreen (grep), GameOverScreen/ReplayViewerScreen (grep), Lan listing; data/ui is not a directory (data/ai only has difficulty.toml). No findings are defects; every item is `kind qol`.
Web used: EA C&C Remastered September patch notes (fetched), PC Gamer on CoH3 tactical pause (fetched), Blizzard SC2 simplified-controls guide (fetched); other URLs are the pages the s18 audit marked as opened ([R]) and are cited as "s18 [R]", not re-opened by me. Northgard and Stormgate searches returned only title lists (SteamDB patch-note links), so no Northgard claim is made; Stormgate items come through s18's [R] reading of playstormgate.com patch 0.3.0.
Owner decisions respected: no colour-blind mode (D-172 (5), DECISIONS.md:1323, 1358); LAN is last priority; balance numbers frozen (D-310), so items that change match outcomes are flagged and are setup/option-only or need an owner call.

## Already built since s18 (so NOT listed below; M13-15 ROADMAP.md:293 plus code)
Game speed incl. slow (GameSpeedUp/Down, Settings.GameSpeedPercent); idle harvester and idle factory buttons/keys (IdleHarvester, IdleFactory, IdleFinder, IdleWatch); alerts UpgradeComplete, ProductionIdle, ProductionBlocked, HarvesterIdle (AlertKind.cs); cameo slot hotkeys 1-12 (CameoSlot1..12, CameoHotkeys.cs); repeat production (RepeatProduction.cs); range rings (RangeRings); strong/weak line (WeaponMatchup.cs); per-slot handicap, credits preset, start kit (SlotOptions.cs); stances (StanceAggressive/Defensive/HoldFire); Shift-queued waypoints (OrderController Shift queue, Command QueueOrder); force fire (OrderController.ForceFireOrder = AttackGround); autosave and quickload (Saves/Autosave.cs, QuickSave/QuickLoad); post-match graphs (GraphsScreen, G in replay); medals and par time (Profile/MissionRecord.cs); focus-loss pause (FocusPause.cs); screen-shake toggle (Settings.ScreenShake); replay speed, step, scrub (ReplayViewerScreen.cs:89-100); LAN chat (Lan/LanChatInput.cs).
Out of scope: colour-blind mode; Gnomestead-style items.

## Findings
Ranked by value for a private single-player + family LAN game. "Outcome" = changes match outcomes if used. Layer FE = front end only (cannot move sim hashes).

### QOL-01 · MED · qol — Remember the last skirmish setup (map, slots, factions, tiers, handicap, colours)
What: Setup screen starts from defaults every time. C&C Remastered added exactly this ("The game now remembers your previous Skirmish settings", EA September patch notes: https://www.ea.com/ea-play/news/september-patch-notes). s18 proposed K4 (modern), status now: not built.
Where: DECISIONS.md:817 "the setup screen keeps no last-used setup"; grep for lastSetup/LastUsed/remember in src/Game finds none (only PlayerProfile tallies).
Evidence: read + grep
Impact: Every family game re-enters 4 slots. Effort S (write skirmish.toml beside settings.toml, FE). Outcome: no.
Fix idea: Persist SkirmishSettings on Start, load in SkirmishSetupScreen ctor; add a "Reset" button.   Changes a match outcome / number / visible text?: no

### QOL-02 · MED · qol — Select all of a type map-wide (not only on screen)
What: Double-click/Ctrl-click selects the type only inside the screen box (Selection.SameTypeInside takes a world box). OpenRA double-tap W = whole map; BAR has whole-map and on-screen variants (s18 [R] https://beyondallreason.info/commands-20; OpenRA Hotkeys wiki). SC2's Ctrl+click is also on-screen only (Blizzard guide), so this is a step beyond SC2. s18 qol-cnc #7 / qol-modern S3: not built.
Where: src/Game/Controls/Selection.cs:139-143.
Evidence: read
Impact: Rally-point armies sit off-screen on 96-cell maps. Effort S (FE, a Shift+double-click or key). Outcome: no.
Fix idea: Add a GameAction "Select same type everywhere".   Changes a match outcome / number / visible text?: no

### QOL-03 · MED · qol — Box select should skip harvesters/builders when fighters are in the box
What: Box select takes every own unit (Selection.Inside has no class filter), so dragging over a battle pulls Scrapers/engineers into an attack-move. Phobos "low priority" and OpenRA priority select are the model (s18 qol-cnc #8, [R] phobos.readthedocs.io User-Interface). Select-army (C) already filters via IsCombatUnit, so the predicate exists.
Where: Selection.cs:126 Inside vs :176 IsCombatUnit.
Evidence: read
Impact: A real pain in fights; harvesters wander into the enemy. Effort S (FE). Outcome: yes in play (fewer lost harvesters) but no rule change.
Fix idea: In box result, if any combat unit is inside, drop non-combat unless Shift/Alt held.   Changes a match outcome / number / visible text?: no

### QOL-04 · MED · qol — Economy readout beside credits: income per minute and harvesters working/total
What: Income tracking exists only for the spectator (Ui/Spectator/IncomeTracker.cs). The player HUD shows credits and power only (no income/harvester count in Hud.cs or SidebarPanel.cs). GAZ economy display and Phobos counters (s18 qol-modern P5/I5, qol-cnc #24). Source: s18 [R] https://github.com/IDragonfire/GAZ_UI.
Where: Hud.cs, SidebarPanel.cs (grep "income" returns nothing); IncomeTracker.cs exists.
Evidence: grep
Impact: Shows whether a second refinery paid off; helps the kids see "why am I broke". Effort S (FE, reuse tracker, read-only snapshot). Outcome: no.
Fix idea: Show "+380/min  Scrapers 2/3" under the credits line.   Changes a match outcome / number / visible text?: no (new text)

### QOL-05 · MED · qol — Jump to selected group / cycle own production buildings
What: Camera keys are CentreBase (Home), bookmarks, LastAlert, idle factory/harvester. There is no "centre on selection" and no key to step through all own production buildings (IdleFactory only reaches idle ones). SC2: Backspace cycles bases, double-tap group number centres, Space cycles alerts (Blizzard guide https://news.blizzard.com/en-us/article/6640645/game-guide-simplified-controls); AoE2 DE shares one key across go-to-building keys (s18 [R] ageofempires.com update-81058). s18 C2/S6/qol-cnc #1-2: not built.
Where: GameAction.cs (no such action); Controls/TapTracker.cs is used for groups.
Evidence: read
Impact: Left Behind rigs move and Expedition ships dock, so "where is my factory" is real. Effort S-M (FE). Outcome: no.
Fix idea: Add "Next production building" (cycle, select, centre) and "Centre on selection".   Changes a match outcome / number / visible text?: no

### QOL-06 · MED · qol — Hover info for any unit or building (including enemies)
What: No hover tooltip or enemy inspect exists in play; GameScreen hover code is placement only (GameScreen.cs:1363). OpenRA/Remastered show name, owner and state on hover (s18 qol-cnc #30, qol-modern S/I1). Cameo tooltips and WeaponMatchup exist but only on the build sidebar.
Where: Screens/GameScreen.cs, Ui/Card (SelectionCard shows own selection only; grep for enemy in CardModelBuilder finds none).
Evidence: grep
Impact: Learning four factions: players cannot read an enemy unit without a pause-and-Archive trip. Effort M (FE; read-only snapshot). Outcome: no.
Fix idea: After 0.5 s hover, small panel: name, owner colour, health, weapon strong/weak line (reuse WeaponMatchup).   Changes a match outcome / number / visible text?: no

### QOL-07 · MED · qol — Skirmish rules: superweapon limit and fog/reveal-map toggle
What: Per-slot handicap, credits and start kit exist (SlotOptions.cs:14-57). No lobby option for "no superweapons / one each" or "fog off / reveal map". Tempest Rising added a superweapon limit in skirmish (s18 qol-cnc #48 [R]); lobby fog/shroud is standard in OpenRA (s18 qol-cnc #46). s18 qol-modern K2 listed starting conditions: credits done, rest not built.
Where: SlotOptions.cs (no superweapon or fog member); SkirmishSetupScreen.cs options row at :494.
Evidence: grep
Impact: Kids' games without the finisher; reveal-map for learning. Effort S-M (setup record, save header version bump, LAN SetupCodec). Outcome: yes (opt-in only; defaults unchanged, so frozen numbers untouched).
Fix idea: Two toggles in the setup screen written into SimSetup; defaults = today.   Changes a match outcome / number / visible text?: yes (optional rule)

### QOL-08 · MED · qol — Tactical pause that visibly accepts orders
What: Space pauses (GameAction.Pause) and draws "PAUSED" (GameScreen.cs:1748). CoH3's tap-space freeze lets you give orders that run in sequence on resume (PC Gamer: https://www.pcgamer.com/company-of-heroes-3s-tactical-pause-lets-you-snatch-control-of-the-battlefield). Whether RTS_2027 accepts orders while paused is not verified (see Suspects); there are no route lines or banner telling the player that it does.
Where: GameScreen.cs:499 (pause toggle), SimHost.cs:140.
Evidence: read (partial)
Impact: Big for a single-player family game: a calm way to plan. Effort M (FE if the command queue already holds commands while paused; else small SimHost change). Outcome: yes in single-player (planning aid); not LAN (LAN has its own pause).
Fix idea: Tint the view and draw order lines while paused; confirm orders issued are applied on resume.   Changes a match outcome / number / visible text?: no

### QOL-09 · LOW · qol — Ping / beacon for teams (minimap and world)
What: No ping or beacon exists (grep beacon/ping finds nothing in Ui/Controls). C&C Remastered added beacons: "Teammates can ping a location ... one beacon at a time, lasting 30 seconds" (EA September patch notes); BAR/Stormgate have pings (s18 qol-modern I4). LAN chat exists (LanChatInput.cs).
Where: Lan/ and Controls/ (none).
Evidence: grep
Impact: Family 2v2 LAN: "attack here". Effort M (needs a small lockstep message or chat-line encoding; LAN is last priority per owner). Outcome: no.
Fix idea: Alt+click sends a "ping x,y" chat line rendered as a minimap ring for 30 s.   Changes a match outcome / number / visible text?: no

### QOL-10 · LOW · qol — Live target lines for selected units
What: Orders draw a ring at the click point (OrderFeedback.cs) but selected units show no line to their target. OpenRA target lines (s18 qol-cnc #27, [R] openra.net playtest-20190825). Also lets you see what an AI ally is doing.
Where: Render/OrderFeedback.cs; no TargetLine symbol.
Evidence: grep
Impact: Readability of attack-move groups. Effort S-M (FE). Outcome: no.
Fix idea: Toggle (hold Alt): thin line from each selected unit to its current target.   Changes a match outcome / number / visible text?: no

### QOL-11 · LOW · qol — Select by class: all vehicles / all infantry
What: Only "select army" (C) and same-type on screen exist (GameAction list). Tempest Rising has class selects (s18 qol-cnc #6 [R] primagames.com/?p=344714).
Where: GameAction.cs; Selection.cs:165 Army.
Evidence: read
Impact: "Send tanks, keep engineers." Effort S (FE, uses UnitKind). Outcome: no.
Fix idea: Two actions SelectVehicles / SelectInfantry, on-screen or map-wide with Shift.   Changes a match outcome / number / visible text?: no

### QOL-12 · LOW · qol — Per-category alert mute and volume
What: Alerts have 14 kinds (AlertKind.cs) but Settings.cs has no per-kind mute; only master/music/sfx/ambience volume. Stormgate 0.3.0 added alert mute (s18 qol-modern A2 [R]). With Shift+5 batches "Unit ready" spams.
Where: Settings.cs record (no alert fields); Ui/Alerts/AlertController.cs.
Evidence: read
Impact: Quieter for kids/long sessions. Effort S (FE; Settings version bump). Outcome: no.
Fix idea: Settings > Alerts: checkbox per kind (UnitReady, UnitUnderAttack).   Changes a match outcome / number / visible text?: no

### QOL-13 · LOW · qol — First-run offer to play the tutorial; adaptive hints
What: No first-run flag (grep FirstRun finds none in src/Game). AoE IV Mission Zero guides new players (s18 qol-modern L4/M3 [R] ageofempires.com quickstart-guide). tut_01 exists but must be found.
Where: Menu/MainMenuModel.cs; Settings.cs.
Evidence: grep
Impact: Family members and guests on a fresh install. Effort S (one dialog + a setting flag). Outcome: no.
Fix idea: On first launch ask "Play the 5-minute tutorial?"; remember the answer.   Changes a match outcome / number / visible text?: no

### QOL-14 · LOW · qol — Drag-line placement for walls and defences; "place last structure again"
What: Structure placement is one click per building (GameScreen placement ghost only). AoE2 DE Shift-click lines, BAR line/grid blueprints, Tempest "N = place last built" (s18 qol-modern P8, qol-cnc #21). No wall-drag symbol in src/Game.
Where: Screens/GameScreen.cs placement code; Ui/Sidebar/PlacementCheck.cs.
Evidence: grep
Impact: Walls and gates cost many clicks. Effort M (FE issues N Build/Place commands; each still validated by sim). Outcome: no.
Fix idea: Hold Shift and drag while placing a wall; N repeats the last placed structure type.   Changes a match outcome / number / visible text?: no

### QOL-15 · LOW · qol — Repair-all / sell-multiple buttons
What: Repair and Sell are per building or per selection; no "repair all damaged" action in GameAction. BAR area repair (s18 qol-modern O7 [R]). After a raid it is N clicks.
Where: GameAction.cs; SelectionCard.cs.
Evidence: read
Impact: Saves clicks after raids; front end sends N Repair commands. Effort S. Outcome: no (same commands as by hand).
Fix idea: "Repair all" key plus a card button when buildings are damaged.   Changes a match outcome / number / visible text?: no

### QOL-16 · LOW · qol — Line-drag move (hold right button, units spread along the line)
What: Move sends units to one cell. BAR move line drag (s18 qol-modern O2 [R] beyondallreason.info/commands/move-line-drag). Not in OrderController.
Where: Controls/OrderController.cs.
Evidence: read
Impact: Firing lines vs splash and defences. Effort M (FE, N Move commands). Outcome: yes (formation play).
Fix idea: Right-drag > 1 cell spreads selection evenly along the drawn line.   Changes a match outcome / number / visible text?: yes (play)

### QOL-17 · LOW · qol — Auto control groups and Ctrl-click on a card cameo to select all of that type
What: No auto-group (Stormgate/BAR, s18 S2) and card clicks select one unit (s18 S4; CardRouting has no modifier handling per grep). SC2 uses panel icons for subgroup select (Blizzard guide lists Tab cycling subgroups).
Where: Ui/Card/CardRouting.cs; Controls/ControlGroups.cs.
Evidence: grep
Impact: Mixed army handling. Effort S-M (FE). Outcome: no.
Fix idea: Ctrl+click cameo = all of that type in the selection; a "keep group updated" flag per group.   Changes a match outcome / number / visible text?: no

### QOL-18 · LOW · qol — Match clock in the HUD; hide-HUD key for screenshots
What: Elapsed time is only in diagnostics / stats (no clock in Hud.cs). Phobos/WC3 timers (s18 qol-cnc #31, qol-modern I5); Homeworld 3 hide-HUD (s18 I6 [R]). A family game wants "who won at 14:32".
Where: Ui/Hud.cs; GameAction.cs.
Evidence: grep
Impact: Polish. Effort S. Outcome: no.
Fix idea: Optional clock under power; H or F11 hides all HUD.   Changes a match outcome / number / visible text?: no

### QOL-19 · LOW · qol — Link from a cameo tooltip to its Archive entry
What: Archive reachable from the pause menu only (s18 L3). CameoTooltip.cs comment also still says "Cameos have no hotkeys of their own ... none is shown" although CameoHotkeys exist (stale comment or missing key chip; check which).
Where: Ui/Sidebar/CameoTooltip.cs:12; Screens/ArchiveScreen.cs.
Evidence: read
Impact: Learning units mid-match without losing context. Effort S. Outcome: no.
Fix idea: F1 while hovering a cameo opens ArchiveScreen at that entry; show the key chip on the cameo.   Changes a match outcome / number / visible text?: no

### QOL-20 · LOW · qol — Reduce flashes toggle (separate from screen shake); second binding per action; hotkey presets
What: Only ScreenShake and Weather toggles exist (Settings.cs:62-63 area). SC2 Classic profile, Stormgate alternate bindings (s18 X2/X3/X6).
Where: Settings.cs; KeyBindings.cs.
Evidence: read
Impact: Accessibility and shared machine. Effort S-M. Outcome: no.
Fix idea: "Reduce flashes" checkbox; "Left-hand layout" preset button.   Changes a match outcome / number / visible text?: no

### Ideas that cost sim work (proposed s18, status now: not built; value lower or need owner call)
Each is effort M-L and changes outcomes; list kept short on purpose.
- QOL-21 · Patrol between two points (BAR, Tempest; s18 O3/#16): no patrol anywhere in src/Game or CommandKind (Command.cs). L, outcome yes.
- QOL-22 · Power-down a building (OpenRA, Tempest; s18 qol-cnc #20): CommandKind.PowerToggle = 17 is reserved and referenced only in Command.cs (grep -rl PowerToggle src finds one file). M, outcome yes, interacts with D-310 balance.
- QOL-23 · Insert-at-front / reorder production queue (BAR, GAZ; s18 P3): no symbol; M (new sim command), outcome yes.
- QOL-24 · Gift credits or units to an ally (AoE2 DE, BAR; s18 K6): LAN/teams only, M-L, outcome yes.
- QOL-25 · AI personality picker (builder / rusher / turtle) (SC2 AI builds; s18 K3): data/ai has only difficulty.toml; M, outcome yes. A "turtle" AI is friendlier for kids.

## Suspects (unproven)
- Orders issued while paused: SimHost.Paused (SimHost.cs:140) only clears accumulated time; I did not trace whether Command submission is accepted and queued during pause. Proof: read SimSnapshotSource.Order and SimHost Step path, or a pause test.
- No enemy click-info: GameScreen/CardModelBuilder grep for enemy found nothing, but I did not read SelectionCard.cs fully; QOL-06 needs a click on an enemy in play to confirm.
- Whether a player can already rebind a "select map-wide" variant through Shift+double-click: not found in GameKeys.cs, unconfirmed in GameScreen input code.
- Northgard and Stormgate (beyond s18's notes): searches returned only SteamDB title lists; no feature from them is claimed by me.

## Checked and fine
- M11 list (edge scroll, control groups, alerts, selection card, cursors, placement ghost, tooltips, rebinding, UI scale) is present.
- Most s18 qol proposals (speed, idle, cameo hotkeys, repeat, range rings, stances, waypoints, handicap, autosave, graphs, medals) are built; not repeated.
- Colour-blind mode is intentionally absent (D-172 (5)); not proposed.
- Force-fire exists (OrderController.cs:119).
