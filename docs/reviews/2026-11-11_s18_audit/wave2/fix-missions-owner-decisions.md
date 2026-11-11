fix-missions: both owner decisions are in on wp/s18-fix-missions (/home/claude/wt/s18-fix-missions), 3 new commits. Every Scripting.Tests batch is green, and so is the scoped Game.Tests run. Nothing pushed; the worktree is clean.

**Things to know before merging:**
- **exp_04 saves and replays:** editing exp_04's mission.lua changes its script hash. Mid-mission saves and replays of exp_04 made before this commit will no longer load (GameLoader / ReplayPlaybackLoader refuse them with their script-changed message).
- **`replay verify` behaviour change:** for mission replays, `Tools replay verify` now exits 0 on drift, printing the approved note plus a developer line `note: it drifts from the recording from tick N`. That extra line is my wording. Skirmish verify still exits 1 on a desync. No Tools test covers replay verify; I only built src/Tools (0 errors).
- **Files touched outside my original list,** as this task required:
  - `src/Game/Menu/ReplayPlayback.cs`
  - `src/Game/Screens/ReplayViewerScreen.cs`
  - `src/Tools/ReplayCommands.cs`
  - the shipped tests `PilotMissionTests.cs`, `M8_15/BalanceTableTests.cs`, `M8_15/MidMissionSaveLoadTests.cs`
  - `data/missions/exp_04/mission.lua`

COMMITS
- e7286b2 fix(missions): exp_04 Finale keeps marching on the second blow after the MIS-03 fix [S18-MIS-03]
  - In `Finale()`, `Actor.Alive("shaft") and Actor.Alive("device")` became `... or ...`, with the comment `-- S18 MIS-03 (owner decision): keep the tested balance, the guard marches on the SECOND blow (Shaft and Device both down).`
  - The `Actor.Alive` fix itself is kept.
- c25cb50 test(missions): the auth_01 tier-5 bot that loses both scrapers now loses [S18-MIS-03]
  - **PilotMissionTests:** `A_simple_player_can_win` is now tier 3 only, with the same assertions as before. The new `A_simple_player_that_loses_both_scrapers_at_tier_5_loses` expects: outcome Lost, "scrapers" Failed, player 0 Defeated, no script errors. The bot loop also stops once the mission has an outcome, which changes nothing for the tier-3 win.
  - **BalanceTableTests:** auth_01 tier 5 expects not won, with "scrapers=Failed" in the diagnostics. The errors, locale and win/tick-limit checks for every other row are unchanged.
  - **MidMissionSaveLoadTests (tier 5):** still requires the resumed walk to end exactly like the uninterrupted one (same outcome, tick, both loss counts, final hash, restored tier, no failure or errors). It now checks the outcome matches rather than requiring a win.
  - Each change carries a `// S18 MIS-03 (owner): ...` comment.
- 4ed8534 feat(replay): mission replays are watchable only, with no desync warning [S18-GS-03]
  - The approved note lives in `MissionReplay.WatchableOnlyNote` (Scripting). `ReplayPlayback` gains `IsMissionReplay` (true when there is a script host, so campaign and tutorial) and `ShowDesyncWarning` (only for non-mission replays).
  - The replay viewer adds the note as a line in the existing top-left HUD panel and shows the WARNING line only when `ShowDesyncWarning` is true.
  - **Tests:** the GS-03 audit test is now `A_tier_5_campaign_replay_drifts_but_is_watchable_only`. The playback still drifts, the warning is hidden, and the text is pinned. The new `A_skirmish_replay_that_desyncs_still_shows_the_warning` forces a desync (+1 cash on the playback sim) and checks the warning shows. No skirmish forced-desync test existed before, so I added this one.

CHECK (all on the final code unless noted)
- `dotnet test tests/Scripting.Tests --filter "M8_15|S18|PilotMission"`: 80 passed, 1 skipped (the S18 MIS-07 desired test, awaiting the owner).
  - BalanceTable exp_04 tier 5 and auth_01 tier 5 pass; all four campaign playthroughs pass.
  - This ran before 4ed8534, whose only Scripting change is an added constant.
- `--filter "LeftBehindMission|ExpeditionMission0"`: 47 of 47 passed. `ExpeditionMission04Tests.A_bot_takes…` passes at tiers 1, 3 and 5.
- `--filter "AuthorityMission|DirectorateMission|TutorialMission"`: 40 of 40 passed.
- The rest of Scripting.Tests (everything not covered by the filters above): 137 of 137 passed.
- `dotnet test tests/Game.Tests --filter "S18|Game.Tests.Campaign|M8_08|Game.Tests.Saves|Game.Tests.Replays|Game.Tests.Archive|SettingsTests|CommanderReplayAndSave|Game.Tests.Menu"`: 250 of 250 passed.
- `dotnet build src/Tools`: 0 errors.

USER CHECK
- exp_04 (any tier): destroy the Shaft first. No Finale guard comes out of the gate until the Device is also down, as before S18.
- auth_01 at tier 5: let both scrapers die after the convoy has reached the survey site. The mission is lost and you get the Defeat screen.
- Watch a campaign or tutorial replay from the replay viewer. The top-left panel says "Mission replay: watchable only, it may drift from the original game" and never shows the red "no longer matches the game data" warning, even for a mission recorded at tier 5.
- Watch a skirmish replay recorded on an older rules build: the desync warning still appears.
