# game-ui: src/Game/Ui, Controls, Input, GameScreen — findings
Scope read: Controls/* (all), Input/InputFrame, Screens/GameScreen.cs (all), Screens/MissionOverlay, ControlsScreen (first 200 lines), Ui/Sidebar (SidebarPanel, SidebarLayout, SidebarCommands, CameoTooltip, BlockReasonText, PlacementCheck, SidebarMetrics), Ui/Card (SelectionCard, CardLayout, CardRouting, CardModelBuilder), Ui/Alerts (Controller, Layout, Deriver, Kind), Ui/Objectives/ObjectiveGuidance, Ui/Minimap(+Math), UiScale, UiUnit, Hud, CommanderChatPanel/Model, Settings.cs, HowToPlayContent, sim spots (SimHost, Enqueue, IsValidTarget, CanAttackOwnedBy, production cancel/hold). Not read: DebugConsole body, Spectator panel/table, Superweapon controller/targeting, SoftwareCursor/ArmedCursor/OrderFeedback drawing, Llm settings screens, TierGrouping, CameoProvider, rest of ControlsScreen/SettingsScreen drawing, tests content. No game was run; all evidence is from code. A layout port (layout_sim.py in this folder) checked sidebar geometry at 800x600 to 5120x1440: no broken layout found.

## Summary
1. GAMEUI-01 major: right-click on an ALLIED unit or building issues Attack, which the sim refuses, so the move order is lost and the cursor shows the attack cursor. Team skirmish only; no test.
2. GAMEUI-02 major: wheel zoom keeps the view's top-left fixed (audit C4 still open), so every zoom throws the area of interest off screen.
3. GAMEUI-03 minor: sidebar tab letters are hard-coded "QWERTY" and objective hints hard-code J/Tab/O; they lie after rebinding, which D-172(6) shipped.
4. GAMEUI-04 minor: 4 px click-drag threshold turns a slightly moving click into an empty box select that clears the selection.
5. GAMEUI-05 minor: minimap right-click is a plain Move, ignores armed attack-move and leaves it armed (audit C7 open).
6. Remaining items are minor: sound-only alerts (low funds, upgrade done, enemy superweapon), "WAIT" with no reason, HowToPlay text out of date, edge-scroll band over the sidebar, Ctrl+n with a building selected wipes a group, orders accepted while paused, instant Sell on the card.
Good news: most of the audit is genuinely fixed; keys are centrally defined with conflict checking; typing correctly blocks hotkeys.

## Status of every finding in docs/reviews/2026-11-07_ui_input_audit.md
| Id | Status | Evidence |
|---|---|---|
| C1 edge scroll | fixed | CameraScroll.cs:EdgeDirection; GameScreen.cs:434-439; setting Settings.EdgeScroll. Side effect: GAMEUI-09 |
| C2 scroll speed | partly | default 900 px/s, setting 200-3000, Shift x2 (CameraScroll.cs, Settings.cs). Speed is screen px, so not resolution-relative (GAMEUI-12) |
| C3 zoom steps/default | closed by decision | D-172(3): units 25% larger; Camera.cs:18 still {0.5,1,2} |
| C4 zoom to cursor | STILL OPEN | Camera.cs:31 ZoomBy only changes index; GAMEUI-02 |
| C5 home/last alert/bookmarks | fixed | GameScreen.cs:1219-1235, GameKeys Home/End/F5-F8 |
| C6 minimap shape | fixed | SidebarLayout.MinimapSize, used in Compute |
| C7 minimap orders | STILL OPEN | SidebarPanel UpdatePanel right click -> OrderSelectionTo (plain Move, no feedback); GAMEUI-05 |
| S1 selection toolkit | fixed | Selection.Toggle/AddRange, ControlGroups, TapTracker; GameScreen.cs:630-670, 1173-1200 |
| S2 building card | fixed | CardModelBuilder.ForBuilding |
| S3 overlap | fixed | unit panel replaced by SelectionCard; HudStack/ObjectivesOrigin |
| S4 group card | fixed | CardKind.Multi, CardLayout tiles |
| S5 box takes units only / no enemy info | STILL OPEN | Selection.Inside units only; enemy click shows nothing (inspect exists only for spectators) |
| S6 selection ring | STILL OPEN | UnitRenderer.cs:14-20 white corner brackets only |
| S7 deselect/invalid sound | still open | only UnitSelect notify (GameScreen.cs ~640-660) |
| O1 order feedback | fixed | OrderController.cs:135,182 Feedback.Add, OrderFeedback |
| O2 attack-move | fixed | left click commits (GameScreen HandleMouse), ArmedCursor, Esc cancels (GameScreen.cs:380) |
| O3 waypoint queue | STILL OPEN | no Shift handling in OrderController.RightClick; needs sim queue. Not in Controls page either |
| O4 rally flag | fixed | GameScreen.cs:1142 OrderFeedback.DrawRally |
| O5 guard feedback | still open (minor) | KeyPressed Guard issues orders, no marker |
| O6 factory vs move hint | fixed | card line "Rally point ... (right-click the ground)" CardModelBuilder |
| O7 cursor per action | fixed | CursorPreview.cs, DrawMapCursor. But see GAMEUI-01 (cursor mirrors the ally bug) |
| P1 ghost + reason | fixed | SidebarPanel ExplainGhost, PlacementCheck.Reason, per-cell colour |
| P2 build radius | fixed | DrawBuildRadius dashed outline |
| P3 | n/a | |
| P4 cameo tooltip, hold, shift x5 | fixed | CameoTooltip.Head/ClickHint, SidebarCommands.RightClick/QueueCount. Wording gaps: GAMEUI-08, -13 |
| P5 queue strip | partly | card line "Building X 45% +2 queued" only (CardModelBuilder.ProductionText); badge count on cameo |
| P6 LOCKED/READY text | fixed | SidebarPanel StateStrip/FittedCentredText |
| P7 sell/repair hotkeys | STILL OPEN | no GameAction for Sell/Repair; card/sidebar buttons only |
| K1 rebinding/controls page | fixed | GameKeys, KeyBindings, RebindSession, ControlsScreen. Gaps GAMEUI-03, -14 |
| K2 debug keys | fixed | GameKeys.DebugKeysAllowed; GameScreen.cs:391-413 |
| K3 | closed by decision | D-177 |
| K4 | n/a | |
| K5 idle-unit / builder / stop-all keys | partly | SelectAll (C) exists; no idle cycling, no select-builder |
| F1 alerts | partly | AlertController + minimap pings; LowFunds, UpgradeComplete, Promotion, enemy superweapon still sound-only (GAMEUI-10) |
| F2 cursors | fixed | CursorPreview + SoftwareCursor |
| F3 colour-blind | closed by decision | D-172(5), D-178; consequences noted in "design notes" |
| F4 health bar option | fixed | Settings.HealthBarsAlways, HealthBars.cs |
| F5 | fixed | rally line drawn |
| F6 Esc cancels armed | fixed | GameScreen.cs:380 |
| H1 debug line | fixed | GameScreen.cs:1282 behind ShowDiagnostics |
| H2 key hints | fixed | removed; F1/? Controls page (GameScreen.cs:372) |
| H3 left stack | fixed | objective bar top centre, alerts top right |
| H4 one scale rule | fixed | UiScale.Resolve everywhere checked |
| H5 text size | fixed | UiScale.TextFactor; min atlas 9 px (FontLibrary.Sizes) |
| H6 power number/blink | fixed | SidebarPanel DrawPowerReadout, PowerBarLit |
| H7 pause menu settings | fixed | PauseScreen.cs:35 |
| H8 | fixed | with C6 |
| G1-G3 objectives | fixed | ObjectiveGuidance (TryObjectiveCell, banner, O list) |
| R1-R5 | not audited (render/art); R3 fixed (SpriteStates.cs:38 BuildingIdleSlowdown=4); R5 minimap still fixed green/cyan/red (Minimap.cs:21-23) |
| A1 settings coverage | partly | text size, UI scale, edge scroll, speed, diagnostics, health bars, controls added; no reduce-flash |
| A2 | closed by decision | D-172(5) |
| A3 window sizes | not checked |
| A4/A5 | partly | min font 9 px; alert stack gives captions for most sounds |

## Input binding table (defaults; all rebindable unless marked fixed; source GameKeys.cs Infos, handlers cited)
| Key | Context | Action | Handler |
|---|---|---|---|
| Arrows | play | scroll | GameScreen.cs:~425 |
| Home / End | play | centre base / last alert | GameScreen.cs:1221-1223 |
| F5-F8 (Ctrl+ stores) | play | camera bookmarks | GameScreen.cs:1225-1235 |
| A / S / G / X / D / K | play | attack-move arm / stop / guard / scatter / deploy / pack | OrderController.KeyPressed; HandlePackKey |
| Q W E R T Y | play | sidebar tabs | SidebarPanel.Update |
| Space | both | pause | GameScreen ~400 |
| Enter | play | talk to AI commander (chat submit is fixed Enter) | CommanderChatPanel.cs:69,78 |
| F2 / F3 / F4 / F10 / . / F9 | sandbox+spectator only | fog, occupancy, paths, ranges, step, spectator | GameScreen.cs:391-413 |
| H | both | toggle decor | GameScreen.cs:415 |
| J / Tab / O | missions | jump / next / list objective | ObjectiveGuidance.Update |
| C | play | select all combat units | HandleSelectionKeys |
| 1-9 (Ctrl set, Shift add, twice centre) | play | control groups | GameScreen.cs:1185-1198 |
| V, 1-4, Tab | spectator | view, speed, detail | SpectatorController.Handle |
| Esc, F1, ?, Backspace, Enter, Shift, Ctrl | fixed | back/pause, help (console in sandbox), chat editing, modifiers | GameScreen.cs:369-386 |
| Mouse: left click/drag/Shift/Ctrl/double | map | select, box, add/toggle, same-type | GameScreen.cs:620-675 |
| Mouse: right click | map | move/attack/enter/rally; cancel in modes | OrderController.RightClick |
| Mouse: left on cameo (Shift x5), right (hold, then cancel) | sidebar | build | SidebarPanel.LeftClickCameo/RightClickCameo |
| Mouse: left/right on minimap | sidebar | move view / move order | SidebarPanel UpdatePanel |
| Middle drag, wheel | map | pan, zoom | GameScreen.cs:442-445 |
Conflict check by hand: no same-context duplicates in defaults (D1-D4 and Tab are shared only with spectator-context actions; KeyBindings.Overlaps covers rebinding). No Numpad or middle button needed (arrows, edge scroll and minimap exist). Home/End/F5-F8 need Fn on most laptops. Modifier handling: order keys fire regardless of Ctrl/Shift (Ctrl+A arms attack-move, Ctrl+C selects all); Alt is never read. Key repeat: all hotkeys are edge-triggered, fine; text fields are the exception (GAMEUI-11).

## Findings
### GAMEUI-01 · major · CODE · confidence high
**What:** In a team game, right-clicking an allied unit or an allied building (without garrison room) issues an Attack order that the sim refuses; the units do not move, and the cursor and click ring say "attack".
**Where:** Controls/TargetPicker.cs:63-76 (Pick returns any non-local unit/building); Controls/OrderController.cs:119,150-153; Controls/GarrisonOrders.cs Classify `case Relation.Ally: ... : BuildingClick.Attack`; Controls/CursorPreview.cs For(): `HasEnemy`/`BuildingClick.Attack`. Sim: Simulation.Players.cs:60 CanAttackOwnedBy (Enemy or Neutral only), Simulation.Combat.cs:139 IsValidTarget, ApplyCombatCommand returns silently.
**Evidence:** Pick skips only `Player == localPlayer`; allies appear in the snapshot (shared vision, Simulation.cs Snapshot). Attack on an ally fails `IsValidTarget` so nothing is queued. The order loop has no Move fallback. Tests: no Ally case in tests/Game.Tests/Controls/CursorPreviewTests.cs and no TargetPicker test.
**Impact:** Moving an army through or next to an allied army in 2v2 silently drops orders for the whole selection whenever the click is within about 32 px of an ally; red attack cursor over friends.
**Fix idea:** Make Pick/PickOwn use `RelationOf` (Enemy and Neutral are targets; Ally falls through to Move), mirror in CursorPreview, add tests. S.

### GAMEUI-02 · major · CODE · confidence high (audit C4 still open)
**What:** Wheel zoom changes only the zoom index, so the world point at the top-left of the view stays fixed and everything else slides toward or away from the bottom-right.
**Where:** Render/Camera.cs:31 `ZoomBy`; position formula Camera.cs:45; call at GameScreen.cs:445.
**Evidence:** `_position` is not adjusted, so the point under the cursor moves by (cursor/zoom_old - cursor/zoom_new).
**Impact:** Every zoom needs a re-pan; zooming in on a fight in the middle of the screen loses it.
**Fix idea:** Re-anchor `_position` so the world point under the cursor stays put. S.

### GAMEUI-03 · minor · CODE · confidence high
**What:** Text that names keys is hard-coded and wrong after rebinding. Sidebar tabs show `"QWERTY"[tab]`, tab tooltips show the same letter, the objective bar says "J jump to objective   Tab next objective   O list", the arrow box says "J: jump there".
**Where:** Ui/Sidebar/SidebarTab.cs:83 (used SidebarPanel Draw and UpdatePanel tooltip); Ui/Objectives/ObjectiveGuidance.cs BarLines and DrawWorld.
**Evidence:** Card buttons do use `KeyNames.Of(GameKeys.Active.Get(...))` (CardModelBuilder.OrderButton), so the pattern exists and these places skip it. Also HowToPlayContent "Hotkeys" page.
**Impact:** After rebinding Q, the tab says "Q Structures" and Q does nothing.
**Fix idea:** Read the key from GameKeys.Active. S.

### GAMEUI-04 · minor · CODE · confidence medium
**What:** A click that moves more than 4 px becomes a box select; a tiny box containing no unit centre replaces the selection with nothing.
**Where:** GameScreen.cs:38 and 630 (DragThresholdPx, screen px, not scaled), 636-641 (`Apply(Inside(...))`, then ClearBuildingSelection).
**Evidence:** Inside() tests unit centres only; no fallback to Pick for a tiny box.
**Impact:** On high-DPI or jittery mice a click on a unit sometimes deselects everything instead of selecting it.
**Fix idea:** Scale the threshold with UI scale and fall back to Pick when the box is under about 8 px or empty. S.

### GAMEUI-05 · minor · CODE · confidence high (audit C7 open)
**What:** Right-click on the minimap always issues a plain Move for every selected unit, with no ring, and ignores armed attack-move, which stays armed afterwards.
**Where:** Ui/Sidebar/SidebarPanel.cs UpdatePanel (right click on minimap) and OrderSelectionTo (`CommandKind.Move`).
**Evidence:** OrderController is not consulted, so AttackMoveArmed/Feedback are untouched; harvesters get Move not Harvest.
**Impact:** A player who armed A and clicks the minimap gets a move, and a later left click on the map attack-moves unexpectedly.
**Fix idea:** Route through OrderController with a cell, clear the armed flag, add feedback. S.

### GAMEUI-06 · minor · DESIGN · confidence medium
**What:** The selection card's Sell button sells instantly (50% refund) with no confirmation, sitting right next to Repair.
**Where:** GameScreen.cs UpdateCard `CardActionKind.Sell` -> SidebarCommands.Sell; CardLayout button block.
**Evidence:** No confirm or undo anywhere; sell mode in the sidebar at least needs a deliberate mode click.
**Impact:** One misclick sells a Construction Yard or refinery.
**Fix idea:** Confirm on the card Sell (second click within 2 s) or only allow it on a hold. S.

### GAMEUI-07 · minor · CODE · confidence high
**What:** Ctrl+number with a building (or nothing) selected empties that control group silently.
**Where:** GameScreen.cs:1188 `_groups.Set(number, _selection.Units)`; a selected building clears `_selection`.
**Evidence:** Set() with an empty list clears the group (ControlGroups.cs Set). No sound or message on set either. Shift+n also silently removes units from their old group (ControlGroups.Add).
**Impact:** Accidentally wiping a hotkeyed army with one stray Ctrl+1.
**Fix idea:** Ignore Set when the selection is empty; give a click or HUD toast on set. S.

### GAMEUI-08 · minor · TEXT · confidence high
**What:** The "WAIT" state on a cameo gives no reason, and the power tooltip explains only one of the power effects.
**Where:** SidebarPanel.cs DrawGrid (`CentredText "WAIT"`), tooltip for the power bar (UpdatePanel: "Low power: production is slower"). Sim sets Stalled both for insufficient funds and for "no free exit cell" (Simulation.Production.cs:333,338).
**Evidence:** Low power also holds fire for defences (Simulation.Combat.cs:462) and slows superweapon charge (Simulation.Superweapon.cs:218); the power band has three levels (EconomyRules) but the UI shows one binary "low".
**Impact:** Player cannot tell "no money" from "factory exit blocked"; power tooltip understates the harm.
**Fix idea:** "WAIT: funds" / "WAIT: exit blocked" needs a view flag; extend tooltip text. S-M.

### GAMEUI-09 · minor · CODE · confidence medium
**What:** The 12 px edge-scroll band applies over the sidebar, so hovering the right-hand cameo column, minimap right/bottom edge, credits bar or top row scrolls the map.
**Where:** CameraScroll.cs EdgeBandPx/EdgeDirection; GameScreen.cs:434-439 (`busy` does not include sidebar hover). Sidebar pad is 4 units (SidebarLayout.Compute `pad`), less than the band.
**Evidence:** The audit asked for the right band over the sidebar, but the cameos reach within pad px of the window edge.
**Impact:** Hovering the rightmost cameo column scrolls the map right.
**Fix idea:** Suppress scroll while the cursor is inside a panel's inner rectangle but keep the outer 2-3 px. S.

### GAMEUI-10 · minor · CODE · confidence high
**What:** Several events are still sound-only: low funds, upgrade complete, promotion, and any enemy superweapon launch/charge.
**Where:** Audio/AudioEventKind.cs (LowFunds, UpgradeComplete, Promotion); Ui/Alerts/AlertKind.cs has no matching kinds; AlertDeriver.cs superweapon loops `if (s.Owner != me) continue`.
**Impact:** Deaf players or players looking away miss them; audit F1/A5 only partly fixed.
**Fix idea:** Add alert kinds fed by the same audio events. S.

### GAMEUI-11 · minor · CODE · confidence medium
**What:** Holding Backspace in the commander chat (and the debug console) deletes one character.
**Where:** Ui/Llm/CommanderChatPanel.cs:83 `input.ConsumePressed(Keys.Back)`; also no paste (Ctrl+V works only in TextEntryScreen).
**Evidence:** InputFrame.Pressed is edge-only; control chars from TextInput are dropped by CommanderChatModel.Type.
**Fix idea:** Repeat after 400 ms at 30 ms. S.

### GAMEUI-12 · minor · DESIGN · confidence medium
**What:** Scroll speed and drag threshold are fixed screen pixels, and Auto UI scale tops out at 2 above 1200 px height.
**Where:** Settings.DefaultScrollSpeed=900; UiScale.Auto; GameScreen.cs:38.
**Evidence:** Crossing the screen takes about 1.4 s at 1280 wide and about 4.3 s at 3840; at 2160p the UI is 24 px text (relatively 33% smaller than at 1080p, which gets 1.5). Fixed pixel paddings (Hud.Pad 8, AlertController pad 8, ObjectiveGuidance arrow 26/36 px, minimap ping radii) ignore scale.
**Fix idea:** Default speed = k * width; Auto 3 above ~1800 px; scale pads by UI scale. S.

### GAMEUI-13 · minor · TEXT · confidence high
**What:** HowToPlay and hints are out of date. "Press A, then right-click ground" (D-172(4): left click commits); "Right-click a cameo to cancel" (first right-click puts it on hold, second cancels); Selecting/Hotkeys pages omit Shift, Ctrl+click, double-click, groups 1-9, C, End, J/Tab/O.
**Where:** Menu/HowToPlayContent.cs:52-60, 64, 96-102 vs SidebarCommands.RightClick.
**Fix idea:** Edit text. S.

### GAMEUI-14 · minor · TEXT · confidence medium
**What:** The Controls page lists no sidebar mouse behaviour (cameo Shift x5, right = hold then cancel, minimap right-click, Sell/Repair). The card's "Attack" button actually arms attack-move.
**Where:** GameKeys.cs BuildRows; CardModelBuilder.UnitButtons.
**Fix idea:** Add Fixed rows; rename button "Attack-move". S.

### GAMEUI-15 · minor · CODE · confidence medium
**What:** MissionOverlay wraps ticker, dialogue, briefing and objective lines by character count (78/64/70), not pixels.
**Where:** Screens/MissionOverlay.cs:19 WrapChars, ExpandedLines; ObjectiveGuidance.FormatBriefing.
**Evidence:** At 1280x720 with UI scale 200% (clamped to 1.6) and Extra large text (28 px atlas, about 14 px per char), 78 chars is about 1100 px against a 976 px map view; the sidebar draws over the overflow (DrawHud runs before sidebar Draw).
**Fix idea:** Wrap by BitmapFont.Measure against map width. S.

### GAMEUI-16 · minor · DESIGN · confidence medium
**What:** Orders and build/place/sell commands are accepted while the game is paused; the sidebar and cameo state do not change until unpause, and nothing says so.
**Where:** SimHost.Issue (no paused check, Game/Host/SimHost.cs ~192); SimSnapshotSource.Order/Enqueue; GameScreen.cs ~400.
**Impact:** Pause-building/pause-ordering is possible and invisible; replays record the commands at Tick+1.
**Fix idea:** Decide: block with a "PAUSED" message, or allow and document. S.

### GAMEUI-17 · idea · DESIGN · confidence low
**What:** Smaller items: edge scroll is disabled while box-selecting so a box cannot exceed one screen (CameraScroll busy); F1/? help screen halts the sim without setting Paused (BlocksUpdateBelow); ghost explanation reads sim truth for building/unit occupancy so a stealthed or fogged blocker could show "Blocked" (SidebarPanel.ExplainGhost uses _source.Sim.Map, UnitOnCell uses snapshot); software cursor uses last frame's mouse (one frame lag); selected finished structure cannot be cancelled and the tooltip does not say how to resolve a lane blocked by an unplaceable structure (Sim ApplyCancel; D-066); Pick radius (32 world px) grows in screen px at zoom 2.
**Fix idea:** Take individually. S each.

## Design notes (decided; consequences only)
- D-172(5) no colour-blind mode: placement ghost has reason text (OK), health bars in group tiles have no number (bar length only, SelectionCard.DrawGroup), minimap own/enemy are green vs red-orange (Minimap.cs:21-23), alert stripes have text (OK). Superweapon flash is a full-screen white fade over 24 ticks (SuperweaponFx) with no setting to turn off; ShakeOffset is never applied (dead code, grep).
- Flashing: low-power bar blinks at 1 Hz (PowerBlinkHalfPeriodTicks 10 at 20 tps), objective minimap frame at 3 Hz (`_time*6 &1`) which is at the photosensitivity edge.

## Open questions
- Are passengers inside transports still in snapshot.Units? If not, a control group or selection holding them keeps them selected but invisible (card empty while Selection.Count > 0). Not verified.
- On AZERTY, do the digit-row keys report Keys.D1..D9 without Shift under the project's MonoGame backend? If not, groups need Shift (which means "add"). Rebinding is the workaround. Not verified.
- Is accepting orders during pause intended (GAMEUI-16)?
- Does the Upgrade/Pack/Dive button column overlap the card's Sell/Repair at 1280 wide with Large/Extra large text? Geometry suggests it can (buttons drawn after the card and tested first); unconfirmed.

## Things checked and found sound
- Hotkeys are edge-triggered; typing in chat/console consumes the keyboard before game hotkeys (GameScreen.cs:350-367); Esc handling is one-thing-per-press.
- KeyBindings conflict detection, swap, FromOverrides self-healing; IsBindable excludes Esc/F1/modifiers.
- UI regions (card, HUD, objective bar, superweapon/upgrade/pack/dive buttons, sidebar) consume the mouse before the map; a drag ending over the sidebar is cancelled, not committed.
- Orders on units the player does not own are dropped in SimSnapshotSource.Enqueue (IsOwnedAlive); spectator host refuses all commands.
- Control groups and selection are pruned each frame against IsOwnedAlive; groups are UI-only.
- Minimap click mapping (MinimapMath) is consistent with the drawn image at every scale; sidebar layout stays usable from 800x600 to 5120x1440 at 75-200% and all text sizes (rows >= 2).
- Placement ghost verdict is the sim's own (CanPlaceReady); production hold/cancel/refund matches ApplyHold/ApplyCancel (front item refunded Spent).
