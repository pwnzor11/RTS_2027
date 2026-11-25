# GAMEUI — in-match UI, controls, input (UX adversarial review by reading code)
Scope read: src/Game/Screens/GameScreen.cs (Update, HandleMouse, HandleSelectionKeys, HandleCameraKeys, idle/save/speed/pack/dive/upgrade/superweapon parts), PauseScreen, GameOverScreen, ConfirmScreen, ReplayViewerScreen, FocusPause, MissionOverlay, ScreenStack; src/Game/Controls/* (GameKeys, KeyBindings, RebindSession, OrderController, Selection, ControlGroups, TapTracker, TargetPicker, GarrisonOrders, CursorKind); src/Game/Input/InputFrame; src/Game/Ui/Sidebar/{SidebarPanel,SidebarLayout,SidebarCommands,CameoHotkeys,SidebarTab,LeftBehindText,BuildingUpgradeOffer,CameoNames}; src/Game/Ui/Card/{CardModelBuilder,CardLayout,SelectionCard,SellConfirm}; src/Game/Ui/Superweapon/*; src/Game/Ui/{UiScale,UiText,UiUnit,Hud,ExpeditionCue}; src/Game/Ui/Alerts/IdleFinder; data/ui/tips.toml; data/fonts/*.font.toml (metrics for width arithmetic); data/archive titles. Not read in depth: Editor/, LAN screens, Spectator*, Llm/, DebugConsole, AlertLayout/AlertDeriver, Minimap internals.

Default-binding duplicate script (all `new(GameAction.X, ..., Keys.K[, KeyContext])` rows of GameKeys.cs): the only shared keys are D1-D4 (Group1-4 Play vs SpecSpeed1/2/4/8 Spectator) and Tab (ObjCycle Play vs SpecDetail Spectator). Contexts do not overlap, so there is no default hotkey conflict. enum GameAction (72) = Infos (72) = every action has a Controls row.

## Findings

### GAMEUI-01 · MED · bug
What: At UI scale >= 125 % the bottom-right map buttons (IDLE HARVESTER / IDLE FACTORY, Dive, Pack, Upgrade) overlap the selection card's button block. They are drawn on top of the card, but the click goes to the card button underneath.
Where: GameScreen.cs `IdleButtonRect` (~l.771), `DiveButtonRect`, `PackButtonRect`, `UpgradeButtonRect` (anchored to `MapViewport().Width - w - 8`); `CardLayout.Compute` (card is centred, up to 580*scale wide, buttons at its right edge); GameScreen.Update calls `UpdateCard(input)` (consumes the click) before the idle/pack/dive/upgrade region tests (l.~395-420); Draw order draws card (l.~648) before the buttons.
Evidence: script — a replica of the layout arithmetic (font line heights from data/fonts/noto-sans-regular_*.font.toml, UiScale.Resolve, CardLayout.Compute, IdleButtonRect) for one armed unit selected (7 card buttons) and one idle harvester: 1920x1080 at 100 % no overlap; at 125/150/175/200 % idle button x 1237-1528, y 880-925 vs card buttons x 958-1301, y 788-951 (overlap). Same for 1280x720 at >=125 %, 1366x768 >=125 %, 2560x1440 >=125 %. (Replica is approximate by a few px; the overlap margin is 60-400 px.)
Impact: A player with a bigger UI scale who has an idle harvester and a unit selected sees the idle button over the card; clicking it presses Stop/Scatter/stance instead. Same for Upgrade/Pack/Dive when the card is up.
Fix idea: stack the corner buttons above `_card.Layout.Card.Top` (as mission text already does with `ClearBottom`), and test the corner regions before the card.   Changes a match outcome / number / visible text?: no (layout and click order only)

### GAMEUI-02 · MED · text
What: The superweapon button is sized for the string "SUNRISE  PAUSED" but draws "{archive title in capitals}  {status}". Three of the four factions have a longer title, so the text runs out of the button, past the map view, over the sidebar (the superweapon is drawn after the sidebar).
Where: GameScreen.cs:1108 `BitmapFont.Measure("SUNRISE  PAUSED", scale) + 16`; draw at ~l.1160 `$"{b.Caption}  {b.Status}"`; `SuperweaponButtons.CaptionOf` -> `CameoNames.Display(id, null)` with `ArchiveTitle` set in `LoadCameoBlurbs` (GameScreen.cs ~l.600).
Evidence: script — archive titles: dir_sunrise = "The Sunrise Device" (data/archive/structures_directorate.toml:190), lb_second_drift = "The Second Drift" (structures_left_behind.toml:178), exp_salvo_array = "Salvo Array" (structures_expedition.toml:59). Noto Sans 12 advance sums: "SUNRISE  PAUSED" 103 px (box 119), "THE SUNRISE DEVICE  PAUSED" 174, "THE SECOND DRIFT  PAUSED" 164, "SALVO ARRAY  PAUSED" 130. At scale 1 the text ends 55-63 px beyond the box edge; it grows with the scale. Only Authority ("auth_atomic" has no archive id, falls back to "ATOMIC") fits.
Impact: Directorate, Left Behind and Expedition players see clipped/overflowing text on their most important button every match once the building stands.
Fix idea: size the rect from the actual caption + status (max over states) like `DiveButtonRect` does.   Changes a match outcome / number / visible text?: yes (visible layout only)

### GAMEUI-03 · MED · ux
What: A superweapon click on a cell the sim refuses (Directorate Sunrise has an entrance range of 8 cells) does nothing: no message, no sound, targeting stays armed. The UI never shows the range; `SuperweaponButton.Range` is carried but not used anywhere.
Where: SuperweaponTargeting.cs:39 `if (canFire is not null && !canFire(_armed, cell)) return null;`; GameScreen.HandleSuperweaponTargeting (l.~1085) only issues when a command comes back; DrawSuperweapon draws a red cell + blast radius whatever the validity; data/rules/buildings.toml:433 `superweapon_entrance_range = 8` (the other three are 0 = anywhere).
Evidence: read — grep for `.Range` in GameScreen/Superweapon/TelegraphRenderer finds no consumer.
Impact: A Directorate player under pressure clicks the target, nothing happens, and they cannot tell if it is range, power or a bug. This is the match-deciding action.
Fix idea: draw the legal area (ring around the entrance buildings) and toast "Out of range" on a vetoed click.   Changes a match outcome / number / visible text?: yes (feedback only)

### GAMEUI-04 · MED · text
What: A loading tip tells players "Ctrl+F5 to F8 store a view, and F5 to F8 jump back to it." The default bookmarks are F6, F7, F8 and F12; F5 is Quicksave. Following the tip, Ctrl+F5 quicksaves (modifiers are not checked for QuickSave) and plain F5 quicksaves again, and the fourth bookmark is never found. The tip also ignores rebinding.
Where: data/ui/tips.toml:77; GameKeys.cs Bookmark1..4 = F6, F7, F8, F12 and QuickSave = F5; stale comment GameScreen.cs `_bookmarks` ("F5..F8").
Evidence: read — quoted string vs `new(GameAction.Bookmark1 ... Keys.F6)` and `new(GameAction.QuickSave ... Keys.F5)`.
Impact: Everyone who follows the tip overwrites their quicksave slot (and sees "Quicksaved.") instead of storing a view.
Fix idea: tip text "Ctrl+F6 to F8 store a view..." built from `GameKeys.Active` (or move bookmarks off F5's neighbour layout).   Changes a match outcome / number / visible text?: yes (text)

### GAMEUI-05 · MED · ux
What: Selection-card buttons are never disabled. "Attack" (arm attack-move) is on the card for every unit selection, but `OrderController.KeyPressed(AttackMove)` arms only when a selected unit has a weapon (`CanArm`). Select only Scrapers/Engineers/MCVs and click Attack: nothing happens and the button does not light up. Stance buttons for unarmed units are not shown, but Attack/Guard/Scatter are always shown.
Where: CardModelBuilder.UnitButtons (always adds Attack); OrderController.cs:78-81; SelectionCard.cs:122 (`active` only when armed).
Evidence: read.
Impact: a click that silently does nothing; new players think the game ignored them.
Fix idea: hide or grey Attack when no selected unit can attack, or toast "No armed units selected".   Changes a match outcome / number / visible text?: no

### GAMEUI-06 · MED · ux
What: Repair is a toggle (second click turns it off) but nothing in the UI shows whether a building is being repaired: the card's Repair button is static, the card shows no "Repairing" line, and the sim's `RepairGen` is not exposed to the view or read by any Game code.
Where: CardModelBuilder.BuildingButtons (no `Active`); Simulation.Production.cs:262-268 `ApplyRepair` toggles `_production.RepairGen`; grep for RepairGen/IsRepairing in src/Game: none. Sidebar tooltip even says "click one of your buildings to toggle repair".
Evidence: read (grep across src/Game and Sim/View).
Impact: clicking Repair in repair mode on an already repairing building (or the card button twice) switches repair off without any cue; players lose repairs in a fight.
Fix idea: expose repair state in BuildingView/PlayerView, mark the card button active and draw a wrench on the building.   Changes a match outcome / number / visible text?: no

### GAMEUI-07 · MED · ux
What: A click on a blocked cameo (no cash, supply cap, missing prerequisite, queue full) does nothing and gives no sound or toast. The cameo hotkeys (numpad) have no hover tooltip at all, so a refused hotkey is completely silent. Shift+click or Shift+hotkey fires 5 Build commands where the sim may accept only some.
Where: SidebarCommands.LeftClick returns `CameoClick.None` when `option.Blocked != None`; SidebarPanel.LeftClickCameo ignores `None`; `ActivateCameoSlot` returns false and `HandleCameoKeys` drops the result.
Evidence: read.
Impact: keyboard production fails invisibly when out of cash (classic games say "insufficient funds").
Fix idea: when the click/hotkey is refused, toast `BlockReasonText` for the option.   Changes a match outcome / number / visible text?: no

### GAMEUI-08 · LOW · text
What: The quicksave/autosave failure toast is a single unwrapped line carrying the raw exception message, so it can be wider than the map view and runs off both sides (the box is centred).
Where: GameScreen.cs:866 and l.~901 `UiText.Format(QuicksaveFailed, "reason", error)`; GameSaveTarget.cs:53 `"The game could not be saved: " + e.Message`; `DrawToast` l.~950 sizes the box to the text with no clamp.
Evidence: script — "Quicksave failed: The game could not be saved: Access to the path 'C:\Users\player\AppData\Roaming\RTS_2027\saves\quicksave.rtssave' is denied." = 823 px at font 12 (scale 1); at 1280x720 and scale 1.6 the map view is 976 px wide.
Impact: only on a failing disk or permissions, but then the player cannot read why. The autosave variant repeats every few minutes.
Fix idea: wrap or clip the toast to the map view width.   Changes a match outcome / number / visible text?: yes (layout)

### GAMEUI-09 · LOW · text
What: Button captions "Dive (D)", "Surface (D)", "PACK RIG (K)" and "UNPACK RIG (K)" are hard-coded letters; rebinding Deploy or Pack leaves them wrong. Other captions (idle buttons, tab letters, card buttons) do follow the bindings.
Where: UiText.cs:147-148; LeftBehindText.cs:31,37-38.
Evidence: read.
Impact: wrong hint after rebinding.
Fix idea: build the caption with `KeyNames.Of(GameKeys.Active.Get(...))`.   Changes a match outcome / number / visible text?: yes (text)

### GAMEUI-10 · LOW · ux
What: Esc on the Game Over screen goes straight to the main menu (`GameFlow.QuitToMenu`), with no confirmation and no "keep looking at the map" option. Esc is the same key that opens the pause menu, so it is pressed by reflex in the last seconds of a match (the screen appears at the moment of the win/loss).
Where: GameOverScreen.cs `Update`: `if (input.ConsumePressed(Keys.Escape)) { GameFlow.QuitToMenu(_s); return; }`; menu only has Graphs / Restart / Quit.
Evidence: read.
Impact: the stats table and Graphs can be lost to an Esc press; the player cannot inspect the final board.
Fix idea: make Esc do nothing (or a "Continue watching" item) on the game-over screen.   Changes a match outcome / number / visible text?: no

### GAMEUI-11 · LOW · ux
What: Several selection actions fail silently. (a) `C` (select all combat units) with no combat unit replaces the selection with nothing and clears the selected building. (b) Ctrl+number with an empty selection and recall of an empty group give no message; Ctrl+number gives no "Group 3 set (12)" cue. (c) Bookmark recall with nothing stored does nothing. (d) An invalid left-click in unpack placement gives no reason (ghost only turns red). (e) A right-click on a cell the cursor shows as Blocked still draws the green move ring.
Where: GameScreen.HandleSelectionKeys (l.~1541: `_selection.Replace(Selection.Army(...))` then `ClearBuildingSelection()` unconditionally); HandleCameraKeys; HandlePackKeyAndPlacement (`placing.Click(Me)` null); OrderController.RightClick adds the Move mark whenever `Map.Contains(cell)`.
Evidence: read.
Impact: small, but each one looks like a dropped input.
Fix idea: keep the selection when the new set is empty; toast on group set; skip the ring when blocked.   Changes a match outcome / number / visible text?: no

### GAMEUI-12 · LOW · qol
What: Differences a C&C/StarCraft player will trip on: (a) control groups hold units only, no structure groups (a factory cannot be hotkeyed); (b) Shift+number ADDS THE SELECTION TO the group (StarCraft: adds the group to the selection); (c) Shift+right-click queues only plain moves and attack-moves; Shift+right-click on an enemy replaces the whole queue with a single Attack; (d) double-click / Ctrl+click select-same-type works on units only, not buildings; (e) left-clicking an enemy unit or building only deselects (no info panel); (f) no sell/repair hotkeys; (g) Replay viewer zoom is not anchored on the cursor (GameScreen's is) and replay can only skip forward.
Where: ControlGroups (`UnitKey` only); GameScreen.cs HandleSelectionKeys (`ShiftHeld` -> `_groups.Add`); OrderController.RightClick (`queue` used only in the final Move branch); GameScreen.HandleMouse; ReplayViewerScreen.cs `_camera.ZoomBy(...)` without a cursor argument.
Evidence: read. (Documented in the Controls rows as intended; listed for the "classic expectations" check.)
Impact: muscle-memory misses, not bugs.
Fix idea: optional; (b) and (c) are the cheapest.   Changes a match outcome / number / visible text?: no

### GAMEUI-13 · LOW · ux
What: The 12 sidebar cameo hotkeys default to the number pad only (7 8 9 / 4 5 6 / 1 2 3 / 0 . +). Laptop or tenkeyless players have no keyboard production until they rebind 12 actions one by one (no preset).
Where: GameKeys.cs CameoSlot1-12 defaults; CameoHotkeys.cs doc comment ("rebind them on the Controls page").
Evidence: read.
Impact: keyboard production is effectively off for a share of players; discoverability is only the Controls page.
Fix idea: ship a "QWER/ASDF/ZXCV" preset or a second default.   Changes a match outcome / number / visible text?: no

## Suspects (unproven)
- NumLock off: the game uses MonoGame DesktopGL (src/Game/Game.csproj). If SDL reports Home/Up/PageUp/Left/Right/End/Down/PageDown/Insert/Delete for numpad keys with NumLock off, cameo hotkeys would instead trigger CentreBase (Home), ScrollUp/Down/Left/Right and LastAlert (End). Would be proven by pressing Numpad 7 with NumLock off in a build.
- MissionOverlay wraps dialogue/ticker/objective text by character count (WrapChars = 78, 64 for the expanded panel; Wrap(text, n)), not by pixel width. 78 chars of Noto Sans 12 is ~440 px at scale 1 (script); with Text size "Extra large" (x1.5) on the smallest window (map view about 610 units) that is ~660 px and would run under the sidebar. Needs a run at 1024x600 + Extra large.
- ExpeditionStart toast (427 px at scale 1, 12 s) has the same unwrapped single-line risk at Extra large text on small windows.
- Play-context actions (CentreBase, LastAlert, Bookmark1-4, DecorToggle) are still read in spectator games (HandleCameraKeys has no spectator guard), while ConflictOf lets Spectator-context actions share their keys. Rebinding SpecView etc. to Home or F6 would make one key do both. Needs a rebinding test in spectator mode.
- The sidebar tab buttons/captions at 6 tabs (with Aircraft) and UI scale 200 %: `FitLabel` shrinks text, but I did not measure the minimum readable size.
- Superweapon, Dive, Pack and Upgrade rects are anchored with `MapViewport().Width`, not clamped to the card; if two superweapons stack (Directorate rebuilt twice) they extend down by `h + 4` each and could meet the idle buttons on short windows.

## Checked and fine
- Default hotkey duplicates: none within an overlapping context (script above). `KeyBindings.ConflictOf/TrySwap/FromOverrides` leave no conflict; `IsBindable` rejects Esc, F1, Back, modifiers, Win/Apps/CapsLock/NumLock/Scroll.
- S18 items re-verified fixed: zoom about the cursor (GameScreen.cs:~560 `ZoomBy(..., MousePosition)`), drag threshold scaled by UI (`ClickDrag.IsDrag(..., UiFactor)`), minimap right-click honours armed attack-move (`MinimapOrder`), Ctrl+N with an empty selection keeps the group (`TrySet`), tab-key captions read from bindings, Sell on the card needs two clicks (`SellConfirm`), `?`/F1 help in every mode.
- Empty-selection safety: `OrderController` loops copy `Selection.Units` and `_selection.Count == 0` returns early; `CardModelBuilder.Build` returns `CardModel.Empty`; `GroupCentre`, `IdleFinder.Next`, `AlertJumpCursor` guard empty lists; right-click on a producing building with `_sim.Map.Contains(cell)` check; `enemy!.Value` can only be reached with an enemy (own buildings classify as never-Attack).
- Modal/pause handling: every full-screen modal returns `BlocksUpdateBelow = true`, so a single-player match is frozen under Pause/F1/Settings/Archive/quickload list; `FocusPause` pauses and only resumes its own pause; LAN uses `LanCover` and `GameScreen.Update` returns early when not on top; chat/console `ConsumeKeyboard()` stops hotkeys while typing; Esc order (console, sidebar mode, superweapon, unpack, armed orders, then pause) consumes the key at each step.
- Rally point right-click on a selected producing building, with flag + line drawn (`DrawSelectedBuilding`); waypoint queue drawn (`DrawQueuedPaths`); double-tap group centres the view (`TapTracker`, 350 ms); Ctrl+click / double-click selects same type on screen; Shift+click toggles; Shift+box adds.
- Pause screen items, ConfirmScreen defaults to the Cancel row, Overwrite asks first, QuickLoad asks first.
- Controls page: all 72 actions have a row; rebind flow (Esc cancels, Enter swaps, click elsewhere cancels).
