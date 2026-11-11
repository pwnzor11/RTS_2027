fix-ui is finished: every numbered item and UXG-01/03/06/08/10 is fixed and committed. Last scoped run: 489 passed, 0 failed. Items 1-12 and 14-17 are done; item 13 has no automated test and GAMEUI-09 is deferred.

COMMITS (branch wp/s18-fix-ui, worktree /home/claude/wt/s18-fix-ui)
- 799b45c fix(ui): right-click on an ally moves instead of ordering a refused Attack [S18-GAMEUI-01]
- 8901c7b fix(ui): zoom about the cursor, tiny drags are clicks, minimap right-click honours attack-move, rebound keys shown, renderer memory reset per game [GAMEUI-02, -04, -05, -03, GAMEREND-01]. One commit because all five touch the same GameScreen.cs/SidebarPanel.cs hunks.
- a75233f fix(render): sprite loading is per sheet; bad manifests skipped and logged; no-art allow-list test [GAMEREND-02, -06]
- ec3d778 feat(app): size-capped diagnostics log under AppData, console tee, crash report copy [GAMEREND-03]
- 9f8b9b8 fix(audio): UI Notify cooldowns on the wall clock [GAMEREND-04]
- 1f1a58f fix(settings): keep an unreadable or newer-version settings file as .bad (Load path only) [GAMEREND-12]
- c38f8fe perf(render): cheap per-frame allocation fixes [GAMEREND-10]
- a70836d fix(ui): Ctrl+n keeps a group when nothing is selected, chat Backspace repeats, spectator HUD hidden under the final table [GAMEUI-07, GAMEUI-11, UX-LM-06]
- da3e592 fix(ui): sidebar keeps a held structure placeable after its prerequisite is lost; ghost names units; unpack hint clears tier rows [UXG-01, -06, -08]
- b1f768a fix(ui): mission dialogue above the selection card; end table has income and measured columns; HUD overlays hidden under it [UXG-03, UXG-10]

VERDICT TABLE (finding | verdict | test | fixed?)
- GAMEUI-01 | CONFIRMED | S18/UiAllyRightClickTests (4 tests) | yes. Allies are skipped by TargetPicker.Pick and GarrisonOrders.Classify now returns None for an ally without room, so the click falls through to Move. The cursor follows the same path, so it is not Attack over allies.
- GAMEUI-02 | CONFIRMED | UiCameraAndDragTests: Zoom_keeps_the_world_point_under_the_cursor_fixed (4 cases) + Zooming_past_the_last_level_changes_nothing | yes. New Camera.ZoomBy(steps, anchor), called from the wheel handler.
- GAMEUI-04 | CONFIRMED by code only (no GameScreen test) | UiCameraAndDragTests: two ClickDrag tests | yes. New ClickDrag: 8 px × UI scale, and a release inside it counts as a click. The old fixed 4 px is gone.
- GAMEUI-05 | CONFIRMED | UiMinimapOrderTests (2 tests) | yes. New OrderController.MinimapOrder, wired through SidebarPanel.Orders. An armed attack-move or deploy is honoured and the arming ends. Harvesters still get a plain Move from the minimap; I did not change that.
- GAMEUI-03 | CONFIRMED | UiKeyNamesTests, UiObjectiveKeyHintTests | yes. Tab captions and tooltip use SidebarTabs.KeyName (bound key). Objective bar and arrow hints use new ObjectiveKeyHints. The default text is identical. KeyLetter(char) is kept so existing tests still compile.
- GAMEREND-01 | CONFIRMED | RenderMemoryRestartTests | yes. RenderMemory.Reset plus reset on tick regress, and SceneRenderer.Reset is called in GameScreen.Enter. The Enter call also covers a save loaded at a later tick than the previous game.
- GAMEREND-02 | CONFIRMED | RenderSpriteLoadTests | yes. SpriteLibrary.ScanTolerant skips and logs a bad manifest, duplicate id or orphan companion. LoadAll(device, log) drops a sheet that fails and keeps the rest. RtsGame and CameoProvider use them. Strict Scan and LoadAll keep their old behaviour. The good-sheet half of LoadAll cannot be tested without a GPU. ArchiveSprites and MenuSpriteCache (Archive/Menu, not mine) still use the strict scan.
- GAMEREND-03 | CONFIRMED | RenderAppLogTests | yes. New AppLog writes to %APPDATA%/RTS_2027/logs/rts_2027.log, capped per file, with one .old kept. Program.Main installs a tee on Console.Error and Console.Out, so all existing diagnostics reach the file without editing other helpers' files. The crash report is written next to the exe and under AppData; the dialog shows whichever path succeeded.
- GAMEREND-04 | CONFIRMED | AudioNotifyClockTests (2 tests) | yes. AudioDirector takes an optional wallClockMs argument, and Notify cooldowns use it.
- GAMEREND-05 | CONFIRMED by code, not fixed | none | no: owner choice. Only edge scroll reads IsActive; nothing pauses on focus loss.
- GAMEREND-06 | CONFIRMED | RenderSpriteLoadTests.The_rules_ids_without_a_sprite_are_exactly_the_known_list | allow-list test added. The missing set is exactly the 26 ids in the finding; new gaps now fail CI.
- GAMEREND-07 | CONFIRMED by code, not fixed | none | proposal. RtsGame uses IsFullScreen with the default hardware mode switch, which contradicts the "borderless" wording in the Settings doc.
- GAMEREND-10 | PARTLY done | no behaviour tests (existing render and audio tests pass) | Done: SpriteLibrary.Has replaces Ids.Contains (FxPlayer, SpriteResolver), UnitInterpolator reuses a thread-static dictionary, MusicPlayer.Candidates is cached per (state, faction), HealthBars calls ForUnit once. Not done: UnitLayerPlan list per unit (existing tests hold two results at once, so a reused buffer would break them) and SceneRenderer's per-frame player view copy and ranking.
- GAMEREND-12 | CONFIRMED | UiSettingsLoadTests (3 tests) | yes, Load path only. A newer-version or unparsable file is moved to settings.toml.bad; Save is untouched. Window sizes are still not checked against the display.
- GAMEUI-07 | CONFIRMED | UiMinorsTests | yes. ControlGroups.TrySet, so an empty selection keeps the group.
- GAMEUI-11 | CONFIRMED | UiMinorsTests | yes for the chat box. New KeyRepeat: 24 frames delay, then every 2 frames. The debug console still has no repeat.
- UX-LM-06 | CONFIRMED by code | none (GameScreen.Draw needs a window) | yes. DrawSpectator returns early once the game-over screen is up.
- UXG-01 | CONFIRMED | UiLiveReviewTests: the sim-driven repro + SidebarQueueOptions test | yes, front-end only, no sim change. The test builds Power, Refinery, queues a Factory, sells the Refinery after the Factory is ready, and asserts the HUD no longer lists the Factory while ReadyTypeIndex still holds it. The new SidebarQueueOptions adds held ready and in-progress types back to the sidebar. No missing view field was needed.
- UXG-03 | CONFIRMED | UiLiveReviewTests (2 cases) | yes. CardLayout.ClearBottom puts the ticker and dialogue above the card when it shows.
- UXG-06 | CONFIRMED | UiLiveReviewTests | yes. New PlacementCheck.CellAt uses a land-only test, so the unit reason is reached. PlacementCheck.Cell now also names a building or overlay on buildable ground.
- UXG-08 | CONFIRMED by code | TierGrouping.ContentBottom test | yes. The hint is drawn below the tier previews.
- UXG-10 | CONFIRMED | UiLiveReviewTests (table alignment) | yes. GameScreen now has an IncomeTracker, so INCOME/M is real. SpectatorTable.Final(rows, measure) lines the columns up to within one space width. The alert stack and mission overlay are hidden while the game-over table shows. It is not measured on screen; it assumes BitmapFont.Measure counts trailing spaces.
- GAMEUI-09 (edge-scroll band over the sidebar) | PARTLY | none | not fixed. CameraScroll's own doc says the window's right edge, which is the sidebar's outer edge, scrolls on purpose. That is a design choice.
- UX-01 to UX-04 | verify only, not fixed | wording: raw speaker ids, CameoNames.Display titles, map ids in the picker, and upgrade effect text.
- GAMEUI-06, -16, -08 | proposals only, no edits | see questions below.
- UXG-07, -09, -11, -12, -13 | proposals only, no edits.

CHECK
- Command pattern: flock /home/claude/wt/dotnet.lock dotnet test tests/Game.Tests -m:1 ... --filter "FullyQualifiedName~Tests.S18" (and wider scoped filters).
- Items 1-12 run: S18 filter, 31/31 passed.
- Wider run (S18, Audio, Render, Controls, Ui, Sprite, Fx): 790/790 passed.
- Final run (S18, Ui, Spectator, Sidebar, Placement, M4Tier, Wiring): 489/489 passed.
- I did not run the full suite.

Tests failing before the fix:
- Observed red on the old code: UiAllyRightClickTests (3 of 4 red; the enemy-attack test passed), the RenderMemory restart test, the rebound-tab-key test, and the Settings .bad tests (3 of 3 files' tests: the newer-version and unparsable ones were red, the good-file one passed).
- Not observed red: the other new tests call APIs that do not exist on the old code, so they only fail by compile error there. That covers Camera.ZoomBy(steps, anchor), ClickDrag, OrderController.MinimapOrder, SidebarPanel.Orders, ObjectiveKeyHints, ScanTolerant, AppLog, the AudioDirector wall clock, KeyRepeat, TrySet, SidebarQueueOptions/VisibleOptions, CellAt, ContentBottom, ClearBottom, and Final(rows, measure). I did not run them against old code; the UXG-01 sim repro asserts the bad HUD state directly.

NEW FINDINGS
- Tests mutate the static GameKeys.Active (several existing files plus my two). They can race when run in parallel; my tests use a collection, but the existing ones do not.
- MissionOverlay.Wrap wraps by character count (78/64/70) and ObjectiveGuidance.FormatBriefing calls it. This is GAMEUI-15; the file is in fix-screens' territory, so I did not touch it. Fix: wrap by BitmapFont.Measure.
- OrderController picks allied buildings via Pick so garrisoning still works; allied units are skipped. A Neutral player's units are still attack targets, as before.
- Program.Main now also logs stdout lines ("replay saved", "verify with") to the AppData log; harmless.

QUESTIONS FOR THE OWNER
1. Edge scroll over the sidebar (GAMEUI-09): keep the 12 px band everywhere, or suppress it over the panel's interior and keep only the outer 2-3 px? I recommend suppressing it, because hovering the cameo column currently scrolls the map.
2. Instant Sell on the card (GAMEUI-06): confirm with a second click, or leave as is? I recommend a second click within 2 s.
3. Orders and build commands accepted while paused (GAMEUI-16): block with a "PAUSED" notice or document it? I recommend block, since replays record those commands at Tick+1.
4. "WAIT" with no reason on a cameo (GAMEUI-08) needs a view flag for funds vs exit blocked; it is a wording change, so I left it.
5. Pause on focus loss (GAMEREND-05): I recommend pausing single-player only.
6. Fullscreen (GAMEREND-07): either set HardwareModeSwitch=false for true borderless or change the Settings doc wording. I recommend borderless.
7. UXG-07 (Anchorage shore reason text) and UXG-09 (Expedition start cue): wording/design.
8. UXG-11 (off-map void at full zoom-out) and UXG-12 (control groups not saved): design.
9. UXG-13 (placeholder text cameos) and UX-01 to UX-04 (speaker ids, CameoNames vs Archive titles, map ids and test maps in the picker, upgrade effect text): wording, so I did not fix them.

USER CHECK
- 2v2 skirmish: right-click an ally's tank or building with units selected; they should move, with no red attack cursor.
- Scroll-wheel over a fight: the point under the cursor stays put.
- Click a unit with a slightly moving mouse: it selects instead of clearing.
- Press A, then right-click the minimap: the units attack-move there.
- Rebind Q in Controls: the Structures tab shows the new key.
- Play two games in a row: the second game's starting buildings do not replay the build-up.
- Hold P (pause) and click units: the select sounds keep playing.
- Put a bad file in data/sprites: the game starts and the error is in %APPDATA%/RTS_2027/logs/rts_2027.log.
- Edit settings.toml to "version = 99": it is kept as settings.toml.bad.
- Build a War Factory, sell the Refinery before placing it: the Factory cameo stays on the sidebar and can be placed or cancelled.
- Tutorial: select units; the instructor line sits above the card.
- Drag a placement ghost onto your own units: it says "Blocked by a unit".
- Left Behind start: the "Unpack the Mother Rig" hint is under the tier headings.
- Finish a game: the end table shows income and aligned columns, with no alert or objective overlay on top.
- Spectator Game Over: the stats table is no longer printed over the spectator HUD.
- Chat box: hold Backspace to delete repeatedly.
- With a building selected, press Ctrl+1: the old group 1 survives.

Memory facts: none new.