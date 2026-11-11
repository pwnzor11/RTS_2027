FILE: none. Helper reports cannot be written, so the full findings follow in the brief's format. Re-verified this turn: GS-01, GS-02 and GS-04 line numbers, and that LoadedGame.Note has no reader. Everything else is from code reading plus the scripts check_campaigns.py and check_archive.py in /home/claude/audit/game-screens/. Nothing was built or run, and line numbers are as of reading.

COUNTS: critical 0 / major 6 / minor 11 / idea 1
TOP 5: GS-01, GS-02, GS-03, GS-04, GS-05 (all major)
NOT COVERED: HowToPlay/About/Controls screens; map editor internals; SceneComposer/DrawOpRenderer; SimSnapshotSource beyond ctors; GameScreen HUD (game-ui); SimReader behaviour on corrupt bodies; whether ArchiveBrowser.Hint resolves names; DECISIONS.md only spot-checked for Esc-quit, autosave and replay-retention rulings. Nothing was rendered.

# game-screens: Screens/Menu/Saves/Campaign/Archive/Sandbox + data/menu, campaigns, images — findings
Scope read: all of src/Game/Saves, Campaign, Archive. Screens: ScreenStack, GameFlow, MainMenu, ModeScreen, Campaign/MissionSelect/Briefing/Debrief/Ending, CampaignFlow, Pause, GameOver, Notice, ListScreen, LoadGame, Settings, SkirmishSetup/Settings/MapPicker/ExpeditionSetup, ReplayViewer, MenuPanel, and the lifecycle half of GameScreen. Menu/: SaveCatalog, ReplayCatalog, ReplayPlayback(+Loader), SceneSet, MenuBackground. Sandbox/: SkirmishGame, MissionGame, CommanderSet, ReplayRecording. Also Program.cs, RtsGame.cs, Llm/Commander.cs. Data: campaigns, endings, stills, menu scenes, all 242 archive entries. Not read: see NOT COVERED above.

## Summary
1. GS-01 (major): a campaign win rewrites archive_progress.toml from a stale copy and drops unlocks earned since the Campaign screen opened.
2. GS-02 (major): a campaign mission loaded from a save skips the campaign flow (no progress, no debrief). Restart rebuilds it at tier 1 without the campaign tech.
3. GS-03 (major): campaign replays carry no difficulty or tech list, so they desync unless played at tier 3 with no unlocked tech.
4. GS-04 (major): the Campaign menu is not refreshed after a win, and its lock-reason hint is off by one row.
5. GS-05 (major): a corrupt or newer-version progress file reads as empty and is overwritten. Archive and Settings writes are non-atomic. Failed saves are silent.
6. GS-06 (major, DESIGN): no autosave, quicksave key or in-game load; destructive menu items have no confirm; Esc on the main menu quits.
7. Content: the 13 Technology entries are "[draft pending]" and one can never unlock. All 18 ending slides lack images.
8. Narrow catch lists turn a bad data file into a crash. Replays live in the working directory while saves live in AppData.

## Findings

### GS-01 · major · CODE · confidence high
**What:** CampaignService.RecordWin saves its own copy of ArchiveProgress, loaded when the Campaign screen opened. Unlocks the game wrote to disk since then are overwritten.
**Where:** src/Game/Campaign/CampaignService.cs:31 (`_archive = archive ?? ArchiveProgress.Load(...)`), :141-142 (MarkCampaignMission, Save). One service per CampaignScreen (CampaignScreen.cs:33). A second instance lives in GameScreen.cs:286 (`new ArchiveTracker(ArchiveProgress.Load())`) and saves on each new encounter (ArchiveTracker.cs:66).
**Evidence:** Open Campaign (snapshot S0). Start a mission. The tracker writes S0 plus "met tank". Win: RecordWin writes S0 plus the mission, so the tank record is gone. The service never refreshes, so every later win in the session also drops the encounters and research from the missions between. CampaignServiceTests.cs:150-170 hands the service one ArchiveProgress and never runs a tracker in between.
**Impact:** Archive entries gated by encounter: and research: (most of the 242) silently relock after campaign wins.
**Fix idea:** RecordWin loads fresh, marks and saves, or the service holds no instance. Add a test with a tracker write in between. S.

### GS-02 · major · CODE · confidence high
**What:** A campaign mission restored from a save is built as a bare mission screen. It has no _gameOverOverride, so RecordWin, the debrief, AfterWin and the ending never run. Its Restart is the legacy CreateMission.
**Where:** src/Game/Screens/GameScreen.cs:233-243 (CreateFromSave mission branch; Restart closure at :240). Compare CreateCampaignMission, which sets the override at :225; the override is used at :492. GameScreen.CreateMission (:205-213) uses MissionGame.Create defaults. GameSaveHeader holds only MissionId (GameSaveHeader.cs:43).
**Evidence:** CreateMission has no campaign tier, no WithAllowList and no SetDifficulty. The loaded game keeps difficulty and granted tech through the mission state part, so only win handling and Restart are wrong.
**Impact:** Save during a campaign mission, quit, load, win: plain Victory/Restart screen, mission not completed, next mission stays locked, no tech unlock, no ending after the last mission. Restart gives an easier, tech-reduced mission.
**Fix idea:** Store faction and tier in the header, or derive them, and route CreateFromSave through the CampaignFlow wiring. S-M.

### GS-03 · major · CODE · confidence medium
**What:** The mission replay stores only mission id and script hash. Playback builds a fresh MissionHost at default difficulty with no campaign tech list.
**Where:** src/Game/Sandbox/MissionGame.cs:66 (ReplayRef); src/Sim/Replay/ReplayMission.cs; src/Game/Menu/ReplayPlaybackLoader.cs:64-81 (`new MissionHost` at :72, no SetDifficulty or WithAllowList). Recording sets both at CampaignService.cs:123-125.
**Evidence:** Mission scripts branch on Mission.Difficulty() for spawns (for example data/missions/auth_01/mission.lua:82,112 and lb_01/mission.lua:58-69), and script spawns are not commands. MissionHost.IsTypeAllowed (MissionHost.cs:302) refuses replayed production of unlocked tech. ReplayViewerTests.cs:79,94 use defaults, so this is untested.
**Impact:** Replays of campaign missions at tier != 3, or with unlocked tech, drift. The viewer shows its "no longer matches the game data" warning (ReplayViewerScreen.cs:133) and the picture diverges.
**Fix idea:** Add difficulty and the tech set to ReplayMission (format bump), apply them in OpenMission, and add a record-at-tier-5 test with zero desync. M.

### GS-04 · major · CODE · confidence high
**What:** (a) CampaignScreen computes its rows once in the constructor, so after a win the newly opened faction still shows "locked" and counts are stale. (b) The lock hint indexes _rows by the panel index although "Tutorial" is panel row 0, so it is off by one.
**Where:** src/Game/Screens/CampaignScreen.cs:34 (`_rows = FactionPickModel.Build(_svc)`), :36 (binding), :81 (`_rows[_panel.Selected].Reason`, guarded by `Selected < _rows.Count`). Only MissionSelectScreen is refreshed after a win (CampaignFlow.cs:68).
**Evidence:** Panel is [Tutorial, Authority, Directorate, Left Behind, Expedition, Load, Back] and rows are the 4 factions. With Left Behind selected the hint shows Expedition's reason. With Expedition selected nothing shows.
**Impact:** After beating act 2 the menu still reads locked, and locked factions get the wrong reason or none.
**Fix idea:** Rebuild on becoming top and use `Selected - 1`. S.

### GS-05 · major · CODE · confidence medium
**What:** Profile files fall back to empty and are then overwritten.
- CampaignProgress.Load returns empty for an unreadable, newer-version or wrong-faction file.
- One bad id makes Parse throw and discards the whole file.
- ArchiveProgress.Parse swallows all errors, treats version > 1 as empty, and Save writes in place.
- Settings.Save writes in place.
- RecordWin ignores the Save bool.
**Where:** src/Game/Campaign/CampaignProgress.cs:104-107 (Load), :111-124 (Save, temp+move), :62-72 (Parse). CampaignService.cs:134-143 (`next.Save` at :140, result unused). src/Game/Archive/ArchiveProgress.cs:79-97 (`catch (Exception)` at :87, version check :91), :112-127 (no temp file). src/Game/Settings.cs:245.
**Evidence:** Only the campaign file uses a temp file. A crash or power cut mid-write truncates the others. A downgrade or truncated file reads as empty and the next win or encounter overwrites it. On a failed write (disk full, AV lock) the debrief still reports "New tech" but progress is lost, with no message.
**Impact:** Silent loss of campaign progress, Archive unlocks or key bindings; no backup.
**Fix idea:** Keep an unparseable file as .bad and warn once. Write via temp+move. Show a Notice when Save returns false. S.

### GS-06 · major · DESIGN · confidence high
**What:** There is no safety net for losing a long mission to a stray click.
**Where:** src/Game/Screens/PauseScreen.cs:32-38 (Quicksave, Save, Restart, Quit; no Load), :53 ("Overwrite: name" acts immediately); GameFlow.cs:12-21; GameOverScreen.cs:85; MainMenuScreen.cs:78-80 (Esc quits, marked ASSUMED M3-16). A grep for autosave/quickload in src/Game found nothing; F5 is a camera bookmark.
**Evidence:** D-018 requires mid-mission saves and they exist, but only through the pause menu. Loading requires Quit to menu, which has no confirm.
**Impact:** Accidental Restart, Quit or Esc loses progress. Save-then-retry is clumsy.
**Fix idea:** Needs an owner decision. Cheapest set: confirms on Restart/Quit/Overwrite, quicksave and quickload keys with a toast, one rolling autosave, and a confirm on main-menu Esc. M.

### GS-07 · minor · DATA-REF · confidence high
**What:** Archive entry tech_atomic_artillery unlocks on campaign:auth_05. The Authority campaign has auth_01 to auth_04 only, so it can never unlock.
**Where:** data/archive/technology.toml:133 (marked ASSUMED M3-17). Found by check_archive.py; it is the only bad reference of 242 entries.
**Fix idea:** Point it at an existing mission or make it open. S.

### GS-08 · minor · TEXT · confidence high
**What:** All 13 Technology entries have the body "[draft pending]".
**Where:** data/archive/technology.toml (tech_ashfall, pitchblende, vacuum_tubes, decontamination, hardened_ground, seismic_detection, tunnel_network, radiation, blackout, static_broadcast, sunrise_device, broadcast_surge, atomic_artillery).
**Fix idea:** Write the bodies or hide the tab. S.

### GS-09 · minor · DATA-REF · confidence high
**What:** data/images/endings/ does not exist, so all 18 ending slides draw a dark rectangle.
**Where:** data/campaigns/*_ending.toml (image = "ending_<faction>_NN"); EndingScreen.cs:77-78 (fallback fill); docs/USER_CHECKS.md:264. Known gap, listed because the ending text is owner-approved.
**Fix idea:** Produce the art or ship a neutral per-faction card. M.

### GS-10 · minor · CODE · confidence medium
**What:** Catch lists are narrower than what the guarded code throws, so bad data crashes instead of showing a notice.
**Where / Evidence:**
- GameLoader.cs:54 and :81. MapFile.Load throws MapFileException (derives from Exception only), which the catch does not list. A corrupt sim stream can also raise KeyNotFound, IndexOutOfRange or Format exceptions (SimReader not verified). LoadGameScreen.Start catches only SaveLoadException.
- GameSaveTarget.cs:46 catches only IOException and UnauthorizedAccessException.
- The CampaignScreen constructor (CampaignDef.Load) and BriefingScreen constructor (MissionLoader.Load) are unguarded. So are the Start button (CampaignFlow.CreateMission -> CampaignService.Start / MissionGame.Create) and MissionSelectScreen.PreviewObjectives.
- GameFlow.cs:12-17: Restart pops the screens before the factory runs, so a throw leaves the stack torn down.
- SkirmishSetupScreen.cs:420 catches only InvalidOperation and Rules exceptions, so RulesBundle.ReadDirectory's IOException escapes.
**Impact:** Mostly triggered by edited or damaged files; the crash handler fires instead of a friendly message.
**Fix idea:** One helper that wraps screen factories and shows a Notice, plus MapFileException in the loader wrap. S.

### GS-11 · minor · CODE · confidence medium
**What:** Replays are written and listed relative to the working directory, while saves, settings and progress go to %APPDATA%.
**Where:** SimSnapshotSource.cs:32-54, ReplayCatalog.cs:29, ReplayRecording.cs:38-43,72,87 (errors go to Console.Error only).
**Impact:** Started from another directory or a read-only install, no replay is recorded and nothing tells the player.
**Fix idea:** Use the AppData folder and show a Notice on failure. S.

### GS-12 · minor · TEXT · confidence high
**What:** Several player-facing lines show internal ids, and others are truncated.
**Where / Evidence:**
- Save summaries "Mission auth_02, 12.3 min" and "Skirmish on <map>, vs recruit tier 2" (GameSaveHeader.cs:64-71).
- Rules-mismatch notice with hex hashes (GameLoader.cs:23-25).
- Debrief "New tech" lists type ids (DebriefModel.cs:83).
- Map list file names and test_32 / test_64 (MapPicker.cs:70-76).
- Setup error cut to 56 characters (SkirmishSetupScreen.cs:410,422).
- A stale _error persists across option changes, and only StepKind clears it.
- Saves cannot be deleted or renamed in the game.
**Fix idea:** Use titles and faction names, and hide test maps. S.

### GS-13 · minor · PERF · confidence medium
**What:** SlotText calls HasCommanderKey on every item-text evaluation. That is a file read plus DPAPI decrypt, per AI slot with Cmdr on, several times a frame (MenuPanel Fitted and Measure).
**Where:** SkirmishSetupScreen.cs:249,254; DpapiSecretStore.cs:29-37.
**Fix idea:** Read the key state once on open and on return from Settings. S.

### GS-14 · minor · CODE · confidence low
**What:** Briefing (84), Ending (70) and Notice (52) text is wrapped by a fixed character count, once. The briefing wraps in its constructor.
**Where:** BriefingScreen.cs:20,60,66,224; EndingScreen.cs:18,80; NoticeScreen.cs:15,26.
**Evidence:** Not rendered. At 1280x720 with a large UI scale and Large/Extra large text I estimate the column is wider than the window. At defaults it fits.
**Fix idea:** Wrap to the viewport width, cached by width. S.

### GS-15 · minor · DESIGN · confidence high
**What:** The spectator "Save replay" button keeps nothing durable.
**Where:** GameOverScreen.cs:38; SimSnapshotSource.cs:340-356, :45-48; ReplayRecording.cs:60-79.
**Evidence:** It only calls Finish. The file is skirmish_last.rpl, which the next game moves to _prev and the one after overwrites. No notice is shown.
**Fix idea:** Copy to a timestamped name and say so. S.

### GS-16 · minor · CODE · confidence high
**What:** Maps that fail to load are dropped from the skirmish map list silently (MapPicker.cs:97-100). LoadedGame.Note (the "AI resumes or starts afresh" message from GameLoader.NoteFor) is never displayed; grep found no reader.
**Where:** src/Game/Saves/LoadedGame.cs:21.
**Fix idea:** List broken maps greyed with the error. Show or delete the note. S.

### GS-17 · minor · DESIGN · confidence medium
**What:** The sim keeps running when the window loses focus. IsActive only gates edge scroll.
**Where:** RtsGame.cs:112, GameScreen.cs:437.
**Fix idea:** Open the pause menu on deactivate in single-player games. S.

### GS-18 · idea · DESIGN · confidence medium
**What:** Consequences of existing decisions, for the owner to weigh.
- Any rules change makes all saves unloadable, and Continue takes the newest save, so it shows the mismatch notice. Saves embed the rules text and Simulation.Load can take them (Simulation.cs:859), but GameLoader does not use that while replays do (ReplayPlaybackLoader.cs:56). Still open from review A-2.
- The LLM call cap resets on load (D-111).
- Hand-made maps always use sim seed 2027 (SkirmishGame.CuratedSimSeed), so identical inputs give identical games.
**Fix idea:** none now.

### GS-19 · minor · TEST · confidence high
**What:** No tests cover these cases:
- a tracker write before RecordWin (GS-01)
- a win on a mission loaded from a save (GS-02)
- playback of a replay recorded at tier != 3 or with a tech set (GS-03)
- the Campaign screen after a win, or its hint index (GS-04)
- a corrupt progress file being preserved (GS-05)
**Where:** tests/Game.Tests/Campaign/CampaignServiceTests.cs, Replays/ReplayViewerTests.cs:79,94, M8_08/CampaignScreensTests.cs.
**Fix idea:** One test per bullet beside the fixes. S each.

## Open questions
- Should campaign replays be exact (format bump) or only watchable (suppress the desync banner for mission replays)?
- Which of confirm, quicksave key or autosave does the owner want? D-018 only requires saving.
- On an unreadable progress file, show a message or restart from a backup? I assumed a message.
- Does F9 sandbox stay in a release build? It cannot leak into saves or normal games.

## Things checked and found sound
- ScreenStack defers push/pop/replace made during Update, and input is edge-triggered. A double click cannot start two games.
- GameScreen.Exit disposes sidebar, cameos, replay and commanders. The Commander task catches all exceptions. There is no other background threading in Game.
- Save format: magic, version, length, newer-build message, temp+move, disk-full message, rules hash, script hash, map and generator-layout checks all give SaveLoadException text. Slot names are generated and cleaned. Mission saves carry Lua state, tech and difficulty. AI memory and pending commands restore.
- Skirmish Validate: one human, at least 2 open slots, at least 2 teams, unique colours, player count vs map, Expedition coast rules, correct start choice for 2 players on a 4-start map, D-193 team defaults on layout 2.
- Campaign data: chains, open_after any-of semantics, 12 stills, ending conditions and text keys resolve. Menu scenes: all sprites exist. Archive: no duplicate ids, all other references and sprites resolve.
- Sandbox: only via F9 or --sandbox. Debug keys and console are off in skirmish, mission and loaded games (DebugKeysAllowed). The sandbox cannot be saved.
- Esc backs out of every list, setup, briefing, archive, notice and pause screen. The map editor asks before discarding.
- MenuPanel re-measures and shrinks to fit every frame, and the Archive, replay viewer and list screens lay out per frame, so resize and UI-scale changes work.