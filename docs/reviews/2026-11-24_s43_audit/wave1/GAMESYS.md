# GAMESYS — menus, saves, settings, profile, campaign progress, archive, stats, update, audio, render/sprites robustness
Scope read: src/Game/Program.cs, AppLog.cs, Settings.cs, RtsGame.cs, DisplayPlan.cs; Update/*; Saves/{GameSaver,GameSaveFile,GameSaveHeader,GameLoader,SaveSlots,Autosave,GameSaveTarget}; Menu/{SaveCatalog,ReplayPlaybackLoader,MenuSpriteCache}; Profile/{PlayerProfile,ProfileRecorder}; Campaign/{CampaignProgress,CampaignService}; Archive/{ArchiveCatalog,ArchiveProgress}; Stats/{StatsSampler,MatchTimeline,GraphGeometry}; Audio/{MonoGameAudio,SoundBank,MusicPlayer,AmbiencePlayer,AudioBootstrap,AudioDirector}; Host/SimHost; Sprites/{SpriteLibrary,SpriteSheet}; Render (grep for allocations, texture lifetime, UnitLayerPlan, PuffField, GroundOverlay); plus callers in Screens/{CampaignFlow,LoadGameScreen,GameScreen (save/autosave/quickload),SettingsScreen,MainMenuScreen,ArchiveScreen}.
Not re-reported from S18: settings/profile/campaign/archive `.bad` handling and tmp+move writes (GS-05, GAMEREND-12) are in place and work as designed.

## Findings
### GAMESYS-01 · MED · bug
What: AudioDirector never resets `_lastCombatTick` when a new game (or an earlier save / Retry) starts. The "new game" check only clears `_lastPlayedMs`. Music state is `tick - _lastCombatTick < BattleHoldTicks ? Battle : Tension`, so after a long match the next game's tick is far below the old `_lastCombatTick` and the result is negative, which means Battle.
Where: Audio/AudioDirector.cs:77-93 (`if (tick < _lastTick) _lastPlayedMs.Clear();` ... `_ => tick - _lastCombatTick < BattleHoldTicks ? ...`); `_lastCombatTick` is only assigned at :84. The director is a process-wide singleton (AudioBootstrap.Director).
Evidence: read — grep `_lastCombatTick` shows only the declaration (:43), the assignment (:84) and the comparison (:92); no reset anywhere.
Impact: Play a match whose last fight was at tick T, then start a skirmish, press Retry, or quick-load an earlier save. Battle music plays from tick 0 until the first fight of the new game (or until tick passes T+20s). The fade logic also swaps tracks tension to battle at a new game's start.
Fix idea: in the `tick < _lastTick` branch also set `_lastCombatTick = int.MinValue / 2`.   Changes a match outcome / number / visible text?: no (audio only)

### GAMESYS-02 · MED · debt (policy vs. brief)
What: The Update checker phones home by default. On every launch with `check_for_updates = true` (the default) it sends an HTTPS GET to `https://api.github.com/repos/pwnzor11/RTS_2027/releases/latest` (User-Agent "RTS_2027", through the system proxy). It is not LAN/private only. There is no first-run consent prompt, and the setting is a single row in Settings.
Where: Update/UpdateChecker.cs:33-40, Update/UpdateCheck.cs:14, Settings.cs:72 (`CheckForUpdates = true`), RtsGame.cs:129 (`Updates.Start(_settings.CheckForUpdates, ...)`).
Evidence: read. It is a documented owner decision (D-214 "GitHub Releases + update notice"; README line 70 says it can be turned off). So it is not a bug, but it conflicts with the session-43 "LAN/private only" rule. Only the update checker itself makes outbound calls in this package. The LLM commander (provider default "none") is the other possible outbound path and is outside this package.
Impact: Every player's IP and a "RTS_2027" request reach GitHub at each start unless they find the toggle. Minor robustness points: the whole response body is read with no size cap, though it is limited by the 3 s timeout. The notice text shows a URL that cannot be clicked.
Fix idea: owner call: default `CheckForUpdates` to false (or ask once), or remove it for the private build.   Changes a match outcome / number / visible text?: no (settings default)

### GAMESYS-03 · LOW · bug
What: User-resized windows are not remembered. `Window.AllowUserResizing = true`, but nothing listens to ClientSizeChanged or writes `WindowWidth/Height` except the Settings "Window size" row, which cycles four fixed sizes. Those sizes (up to 2560x1440) are offered regardless of the desktop size, so a window can open larger than the screen.
Where: RtsGame.cs (Initialize: AllowUserResizing; no resize handler); Screens/SettingsScreen.cs:25-26 and :170-175; DisplayPlan.cs:17-21 (windowed size used as is, no desktop clamp).
Evidence: read — `grep -rn "WindowWidth\|ClientSizeChanged" src/Game` finds no other writer.
Impact: A player on a 1080p monitor who clicks the 2560x1440 step gets an off-screen window. A hand-edited `window_width` up to 16384 is accepted by Settings.Parse and used for the back buffer; this might fail at startup (see Suspects).
Fix idea: clamp the windowed plan to the desktop size, and save the size on ClientSizeChanged.   Changes a match outcome / number / visible text?: no

### GAMESYS-04 · LOW · data
What: A single bad value in `campaign_<faction>.toml` (a tier outside 1..5, a non-string in `completed`, a wrong-type `difficulty`) makes `Parse` throw. `Load` then moves the whole file to `.bad` and returns an empty progress. The campaign shows as fully reset with no notice. The same happens for a file written by a newer build (`version > 1`). Settings and Profile degrade per key instead.
Where: Campaign/CampaignProgress.cs:62-78 (throws), :95-110 (`Load` → `KeepAsBad` → `New`).
Evidence: read. The next win writes a fresh file and any later bad file replaces the `.bad`, so the old progress is recoverable only by hand.
Impact: Rare (needs a hand edit or a downgrade), but the player loses all campaign unlocks with no message.
Fix idea: tolerate unknown or bad entries per key as Profile does, or show a notice when a file is set aside.   Changes a match outcome / number / visible text?: no

### GAMESYS-05 · LOW · perf
What: Several per-frame or per-tick allocations and file polls in hot paths:
1. `MainMenuScreen.Update` calls `SaveCatalog.Newest()` every 2 s. This lists both save folders and opens every `.rtss` file to read its header, on the game thread. Every autosave does the same through `target.Existing()` in `Autosave.NextSlot`, and `Describe` opens each save.
2. `UnitLayerPlan.For` allocates a `List<UnitLayer>` per visible unit per frame (UnitRenderer.cs:54).
3. `SimHost.StepCore` builds a full snapshot and a new projectile list on every step, so at 2x game speed, or with `SpeedMultiplier` 8 plus a catch-up of up to 250 ms, that is many snapshots per frame. `AudioComponent` also does `source.Projectiles.ToArray()` and scans the whole projectile store each new tick.
4. `MenuSpriteCache.TryGet` uses `_library.Ids.Contains(id)`, a linear LINQ scan per sprite per frame. `PuffField.Observe` builds a new Dictionary per tick.
Where: Screens/MainMenuScreen.cs:20,68-71; Saves/GameSaveTarget.cs:40; Menu/SaveCatalog.cs:42-55; Render/UnitLayerPlan.cs:67; Host/SimHost.cs:219-236,246-253; Audio/AudioBootstrap.cs (`_previousProjectiles = ...ToArray()`); Menu/MenuSpriteCache.cs:36; Render/PuffField.cs:112.
Evidence: read (not measured).
Impact: GC churn and small hitches. Players with hundreds of saves see a hitch in the main menu every 2 s. Not a correctness problem.
Fix idea: cache `Newest()` by folder mtime, reuse a scratch list for layers, `HashSet` for Ids.   Changes a match outcome / number / visible text?: no

### GAMESYS-06 · LOW · perf
What: `PlayVoice` creates a new `SoundEffect` per call (an OGG is fully decoded to PCM on the game thread every time, with no cache), and the resulting instance is added to `_active` with no voice limiter and no `MaxSimultaneousSfx` check. Instances are only pruned inside `PlaySfx`, and a muted SFX or master volume returns before pruning. Similarly each `PlayMusic` makes a new `Song.FromUri` that is never disposed.
Where: Audio/MonoGameAudio.cs:197-239 (voice), :243-252 (music).
Evidence: read.
Impact: A hitch per mission voice line, and repeated triggers stack voices. The leak is bounded by GC finalisers, so it is small in practice.
Fix idea: cache decoded voice effects (or dispose the effect with its instance), and apply the same ceiling and prune as `PlaySfx`.   Changes a match outcome / number / visible text?: no

### GAMESYS-07 · LOW · data
What: The end-of-match graphs (Stats) are not part of a save. After loading a save, or a quick-load, `StatsSampler` is a fresh field of the new GameScreen, so the timeline starts at the loaded tick and earlier history is missing. A restarted mission via Retry correctly starts afresh.
Where: Stats/StatsSampler.cs:23-40 (Timeline null until first Observe); Screens/GameScreen.cs `_sampler = new StatsSampler()`.
Evidence: read.
Impact: Graphs of a resumed game show only the part after the load; "peak army" values are understated.
Fix idea: note it in the Graphs screen, or store the timeline in the save header.   Changes a match outcome / number / visible text?: no

### GAMESYS-08 · LOW · debt
What: Saves, settings, profile, campaign and archive files are written atomically (tmp then `File.Move(overwrite)`), but the temp file is never flushed to disk (`File.WriteAllText` and a FileStream without `Flush(true)`). After a power cut or OS crash, the renamed file can be empty or truncated. The `.bad` handling then turns an empty settings, profile or campaign file into a reset. Crash reports (`logs/crash-*.txt`) are also never pruned and are written beside the exe (Program Files for installed builds, which fails silently).
Where: Saves/GameSaver.cs:36-44; Settings.cs:~283; Profile/PlayerProfile.cs Save; Campaign/CampaignProgress.cs Save; Archive/ArchiveProgress.cs Save; Program.cs:92-111.
Evidence: read.
Impact: Only on a hard power loss mid-save. Low probability.
Fix idea: flush to disk before the Move for saves and campaign progress.   Changes a match outcome / number / visible text?: no

## Suspects (unproven)
- Reserved Windows device names as save slot names: `SaveSlots.Clean` keeps letters, so typing "con", "nul", "aux" or "prn" in the save-name prompt gives `...\saves\skirmish\con.rtss` (and `con.rtss.tmp`). Windows may treat that as the CON device on some builds. Proof: type "nul" on a Windows build and press Save, then check that the file is written. Also, names differing only by case ("Save A" / "save a") silently overwrite on NTFS. (Saves/SaveSlots.cs:30-34.)
- `MonoGameAudio.Fail` sets `_broken = true` for the whole session on any single InvalidOperationException, which silences music, SFX and ambience. And `InstancePlayLimitException` (thrown by MonoGame's OpenAL when no source is free; I believe it does not derive from InvalidOperationException) is not in the catch filters of `PlaySfx`/`StartAmbience`. Proof: check the MonoGame 3.8.6 source for the exception's base class. The 40-voice SFX cap makes it unlikely.
- Startup with an absurd `window_width/height` (hand-edited up to 16384) on a GPU whose max back buffer is smaller: `ApplyChanges` would throw in `Initialize`, and Program's catch gives the crash dialog on every launch until the file is deleted. Proof: set 16384 x 16384 on a real GPU.
- `CampaignFlow.WinScreen` calls `svc.RecordWin(...)` and `MissionLoader.Load` with no try/catch (Screens/CampaignFlow.cs:31). `RecordWin` throws InvalidOperationException if the mission is not in the campaign list. `Resume` guards this via `CampaignMissionOf`, so it only matters if data and campaign TOML disagree. A thrown exception there is a crash on victory.
- `SimHost.Issue` uses a ushort per-player sequence (`_sequence[player]++`), which wraps at 65536 commands per player. It is unknown whether anything orders by Sequence across the wrap (LAN or replay code, outside this package).
- Sprites: `RtsGame.LoadContent` only catches IOException/InvalidData/Format/InvalidOperation around the sprite library. A manifest with a zero cell size or a corrupt PNG that raises another exception type (DivideByZero, NotSupported) from `LoadAll` would crash at startup. `LoadAll`'s own catch has the same list.

## Checked and fine
- Startup failure paths: missing `data/rules` throws in the RtsGame ctor and is caught by Program (crash log + message box). Missing sprites, menu background, sounds.toml or audio device all degrade (NullAudio, legacy tank sheet, plain colour). No audio, no sprites and no music is not a crash.
- Settings.Load/Parse, PlayerProfile.Load/Parse, ArchiveProgress.Load: tolerant per key, clamped, `.bad` rotation, never throw. Paths all under `%APPDATA%/RTS_2027` (Settings, saves, profile, campaign, archive, logs, user maps, llm.key). The only write outside AppData is the crash log beside the exe, which is optional.
- Save file read path: magic, version, header length (<=16 MB), pending count (<=1M), AI-state length, slot counts and control-group counts are all bounds-checked. Corrupt files give SaveLoadException (shown as a notice), and GameLoader catches the wide set of data exceptions. Map ids reject path separators (`file:`), and mission ids are checked against `Path.GetFileName`. The rules-hash and script-hash mismatches refuse the load rather than load a broken sim.
- `GameSaver.Save` is tmp + move with cleanup in `finally`. `GameSaveTarget.Save` turns IOException/UnauthorizedAccess into a toast. Autosave rotation over 3 slots and the quicksave confirm flow work as intended.
- Quick-load/QuickLoad: reads and validates the save before popping the current game.
- Audio: voice limiter plus per-step combat cap (8) plus 40-voice SFX ceiling bound stacking. Ambience keeps at most a current plus a leaving loop and disposes instances on Stop. Music fades do not leak phases.
- Textures: SpriteSheet, SpriteLibrary, DecorLayer, TerrainLook, FxLook, PlaceholderSheet and MenuSpriteCache all dispose their textures on rebuild or Dispose. The Menu background and per-game scene state are reset (`SceneRenderer.Reset`).
- ArchiveCatalog parse errors are InvalidDataException and every caller (ArchiveScreen, CampaignFlow, GameScreen cameo blurbs, SpeakerNames) catches them and falls back to an empty catalog.
- Update checker robustness: 3 s timeout, all exceptions swallowed, response URL must start with `https://github.com/`, pre-release tags are ignored, and nothing is downloaded or executed.
- AppLog: size-capped (256 KB, one `.old`), never throws, thread-safe through Console's synchronized writer wrapper.
- Graph geometry: no divide-by-zero when the timeline has 0 or 1 samples.
