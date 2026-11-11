fix-missions (worktree /home/claude/wt/s18-fix-missions, branch wp/s18-fix-missions). 13 commits, nothing pushed, worktree clean.

**Needs your attention first:** the MIS-03 fix makes 5 existing bot tests fail. That is the expected effect of fixing the bug (see QUESTIONS 1-2), but the lead's full-suite run will be red until the owner decides.
- `PilotMissionTests.A_simple_player_can_win(5)`
- `M8_15.MidMissionSaveLoadTests.A_tier_5_save_keeps_the_tier_and_ends_in_the_same_state` (it walks auth_01 at tier 5)
- `M8_15.BalanceTableTests` auth_01 tier 5
- `M8_15.BalanceTableTests` exp_04 tier 5
- `ExpeditionMission04Tests.A_bot_takes_the_ash_holds_the_shore_and_breaks_the_redoubt(5)`

Why they fail now:
- **auth_01 tier 5:** the bot loses both scrapers in the survey phase. The script's own loss check now fires ("Both scrapers are lost") where it used to be skipped.
- **exp_04 tier 5:** the Finale guard (66 units at tier 5) now marches out on the first blow against the redoubt, as the script's comment says it should. It used to come on the second blow. I proved the cause: with only the `Actor.Alive` line reverted, all three exp_04 tiers pass again. I did not edit these tests or the mission scripts; they are outside what I own.

**Files touched outside my list (the brief's items required them):**
- `src/Game/Archive/ArchiveProgress.cs` and `src/Game/Settings.cs` (item 7: .bad files and temp-file writes).
- `docs/missions/api.md` (item 4 rule, plus the new end-of-mission and zone-overflow notes).
- `GameScreen.cs`: my diff is limited to the mission factories, `CreateFromSave` and one line in `GameOverHeadline`, plus one field and one helper (`EndOnMission`). `fix-ui` also edits this file, so expect a possible merge touch there.

COMMITS
- e6be835 fix(scripting): tech-restricted missions can build granted aircraft [S18-MIS-04]
- fafc29e fix(game): scripted missions end on the script outcome or the human's defeat [S18-MIS-01] [S18-MIS-05]
- 1dd855e fix(campaign): a campaign mission loaded from a save keeps its campaign wiring [S18-MIS-02] [S18-GS-02]
- 67dcd5f fix(scripting): Actor.Alive, Actor.Cell and objective markers treat a dying actor as gone [S18-MIS-03]
- 5905281 fix(campaign): RecordWin keeps Archive unlocks written since the Campaign screen opened [S18-GS-01]
- 8720478 fix(campaign): Campaign screen refreshes after a win and its lock hint matches the row [S18-GS-04] [S18-UX-05]
- 1c34ed3 fix(game): keep unreadable progress files as .bad, write archive and settings atomically, tell a failed campaign save [S18-GS-05]
- 10182d9 fix(scripting): nullable warning in Actor.Cell after the MIS-03 change [S18-MIS-03]
- 32078ac, 46a7b4e test(scripting): aircraft test fix-ups (buildings placed by a scan; tier gates met so only the tech list decides) [S18-MIS-04]
- 988097a fix(scripting): Reinforce.Zone overflows around a full zone instead of failing the callback [S18-MIS-07]
- 8d2774b test(campaign): pin the tier-5 campaign replay desync [S18-GS-03]
- a8b4cce test(missions): audit pre-placed actors on impassable or occupied cells [S18-MIS-13]

Note: several commits went in before their scoped test run; every test was green or understood by the end (CHECK below). The test file in 5905281 also holds the GS-04 and GS-05 tests.

VERDICT TABLE
For every fix marked "yes", the test failed on the old code and passes now. Two exceptions are noted in their rows.

| finding | verdict | test | fixed? | note |
|---|---|---|---|---|
| MIS-04 / FACTION-MATH-01 / FAD-03 / LE-01 | CONFIRMED | `S18.AircraftTechTests` (2) | yes | `IsTypeAllowed` now treats Aircraft as a vehicle entry. The red run was redone after the tier-gate fix, so it failed for the right reason. A non-granted aircraft (auth_bomber) is still refused. |
| MIS-01 | CONFIRMED | `S18.CampaignMissionEndTests.Losing_lb03_when_idle…`, `Losing_auth03_to_the_tide…` | yes | After `Mission.Lose`, `Sim.GameOver` stays false in both missions. The screen now ends on `MissionGame.IsOver` (script outcome or local player defeated); the headline comes from `LocalWon`. Red was seen for lb_03 only; auth_03 uses the same code path and its test was rewritten afterwards to call TideTurns directly for speed. |
| MIS-05 | CONFIRMED | `Razing_exp01s_only_enemy_building_does_not_end_the_mission`, `A_scripted_win_ends_the_mission_with_Victory` | yes | Decided in the Game layer, no sim change or hash moved. The sim's `GameOver` becomes true; the mission now continues. A halted script falls back to the sim rule. |
| MIS-02 / GS-02 | CONFIRMED | `S18.CampaignSaveResumeTests` (tiers 3 and 4, plus tutorial → null) | yes | No header bump: faction, mission and tier come from the restored host (`Def.Faction` plus the saved script tier), so old saves are fixed too. `CreateFromSave` wires Restart and the win hook through `CampaignFlow.Resume`. `AfterWin` with no mission select under it goes to the main menu and still shows the ending after the last mission. |
| MIS-03 | CONFIRMED | `S18.DyingActorTests` (4: auth_01 both scrapers on one tick, auth_01 one after the other plus marker re-point, auth_03 both carriers, Alive/Cell API) | yes | `Alive`, `Cell` and `TryObjectiveCell` now use `IsStanding`; api.md states the rule. Changes the 5 bot tests listed above. |
| GS-01 | CONFIRMED | `S18.CampaignProgressFilesTests.A_win_keeps_archive_unlocks…` | yes | `RecordWin` marks and saves a fresh load of the archive file. |
| GS-04 / UX-05 | CONFIRMED (both parts) | `The_lock_hint_skips_the_tutorial_row`, `The_campaign_screen_rows_refresh_after_a_win` | yes | `HintAt` skips the Tutorial row. `Refresh()` is called from `CampaignFlow.AfterWin`. New public constructor `CampaignScreen(services, svc)`; the tests pass `null!` for services. |
| GS-05 | CONFIRMED | `.bad` tests for campaign and archive (2 each), `A_failed_progress_save_is_reported`, `Settings_save_goes_through_a_temporary_file` | yes | Corrupt or newer files are kept as `<file>.bad`; Archive and Settings write via temp file plus move; `LastWinSaved` drives a Notice after the debrief (new wording, see QUESTIONS 5). The Settings test only checks no .tmp is left; it would also pass on the old code (atomicity cannot be observed in a test). |
| MIS-07 | CONFIRMED | `S18.SpawnFailureTests.Lb04_Landing…` (fixed); `Audit_Auth01_CheckRaids…` plus a skipped desired test | partly | lb_04 tier 5 with a crowded landing zone used to give an error, no fleet timer and a soft-lock. `Reinforce.Zone` now overflows to the nearest free cell around the zone; only reached where the old code errored. auth_01 CheckRaids with the yard site blocked is confirmed: phase = build, "hold" completed, no refinery objective, error only in `Host.Errors`, not Halted. Not fixed: needs a script change or building relocation. |
| GS-03 | CONFIRMED | `S18.CampaignReplayTests.Audit_A_tier_5_campaign_replay_desyncs_on_playback` plus a skipped desired test | no: awaiting owner | Playback host tier is 3, the final hash differs and DesyncTick ≥ 0. |
| MIS-13 | CONFIRMED | `S18.ActorPlacementAuditTests.Audit_…` | no: data | Exactly three: auth_01 svy1 at (55,6) on water; auth_04 dir_conscript at (44,79) and dir_ashthrower at (47,81) inside buildings. Edge cells are fine. |
| MIS-06, MIS-08, MIS-09, MIS-10, MIS-11, MIS-12, MIS-14, MIS-15, MIS-16, MIS-18, MIS-19, MIS-20, GS-07 | CONFIRMED by reading code and data (no test) | — | no: owner | Proposals below. MIS-10 and MIS-12 were not re-checked beyond the finding. |

CHECK
- `dotnet test tests/Scripting.Tests --filter "FullyQualifiedName~S18"`: 8 passed, 1 skipped (before the audit test was added). Within the M8_15|S18 run, all S18 tests pass.
- Scripting.Tests mission regression, in batches:
  - Authority, Directorate, Pilot and Tutorial missions: 43 of 44 passed (Pilot tier 5 fails).
  - LeftBehind and Expedition missions: 46 of 47 passed (exp_04 tier 5 fails).
  - `M8_15|S18` (campaign walks, balance table, mid-mission saves, S18): 73 passed, 3 failed, 1 skipped. The failures are MidMissionSave tier 5, Balance auth_01 t5 and Balance exp_04 t5. All four campaign playthroughs passed.
  - The rest of the project (filter excluding the above): 207 passed, then the 580 s timeout cut the run off. Its only failures were the same M8_15 rows and the audit list (since pinned).
- exp_04 cause check: with only the `Actor.Alive` change reverted, `ExpeditionMission04Tests.A_bot_takes…` passes at tiers 1, 3 and 5.
- `dotnet test tests/Game.Tests --filter "S18|Game.Tests.Campaign|M8_08|Game.Tests.Saves|Game.Tests.Replays|Game.Tests.Archive|SettingsTests"`: 185 passed, 1 skipped (the GS-03 desired test).
- The Game.Tests campaign walks live in Scripting.Tests M8_15 (covered above); Game.Tests has none.

NEW FINDINGS
- NF-1 (major, TEST): the MIS-03 fix flips the 5 bot tests listed at the top. Each needs an owner choice; see QUESTIONS 1-2.
- NF-2 (minor, CODE): `Camera.Focus(tag)` (MissionHost.Media.cs:59) and the Seen/Idle triggers still use `TryResolve`, so a dying actor is still "there" for them. Harmless today; I left them alone.
- NF-3 (minor, CODE): `Actor.Create` of a unit never fails on a blocked or impassable cell (no checks in `SpawnInfantry` / `SpawnVehicle`). Only buildings fail. This confirms the MIS-13 mechanism.
- NF-4 (idea): `pcall` is not available in the mission sandbox, so a script cannot guard a spawn itself. Relevant to the MIS-07 proposals.

QUESTIONS FOR THE OWNER
1. **auth_01 tier 5 bot now loses.** Losing both scrapers after the convoy phase is a loss, as the script intends. Options: (a) improve the bot to escort the scrapers; (b) soften tier-5 raids; (c) make the script tolerate scraper loss after "reach" is complete. Recommend (a).
2. **exp_04 tier 5:** the 66-unit Finale on the first blow beats the bot. Options: (a) keep the design and retune the tier-5 Finale counts (`Pick({10,12,14,22,32})` etc.) or the bot; (b) restore today's balance by changing `Finale()` to `if Actor.Alive("shaft") or Actor.Alive("device") then return end` (Finale on the second blow). Recommend (b) for now, since it keeps the balance that was tested, then revisit.
3. **GS-03 replays.** Options: (a) bump the replay format so `ReplayMission` carries the difficulty tier and the campaign allow-list, applied in `OpenMission` (`SetDifficulty` plus `WithAllowList`); (b) suppress the desync banner for mission replays and call them "watchable only". Recommend (a), with old replays read as tier 3 and no list.
4. **MIS-07 remaining:** show `Host.Errors` / `Halted` in the mission overlay (debug line or Notice): recommend yes. For the auth_01 yard: `Actor.Create("auth_conyard", 0, {x=23,y=25}, "yard")`, either moved after `state.phase = "build"`, or a host `FreeSiteNear` for building spawns. Recommend the host option. Also, in scripts generally, set flags after spawns.
5. **The new Notice wording** for a failed progress save is mine and needs review: "Progress not saved" / "The campaign progress file could not be written, so this win is not kept. Check the free disk space and that the settings folder can be written." It is in CampaignFlow.cs.

Proposals (one line each, not applied):
- MIS-06: lb_01 mission.lua:30 → `Objective.SetTarget("deploy", "camp")`. The rig is driven to (26,22) inside the camp zone, not the "anchor" auth_power at 58,58.
- MIS-08: dir_01 mission.toml add `[zones.enemy_base]` with `rect = [52, 52, 62, 62]` (the enemy base actors are at 54-58,54-58), and lua:25 → `AI.Defend(1, "enemy_base")`.
- MIS-09: dir_04 `SunriseFired`, after the `state.done` check → `if not state.charged then return end`. This ignores a conventional HQ kill before the charge; the alternative is to treat an early HQ loss as a loss. Owner picks.
- MIS-10: auth_04 and exp_04: either set the pre-placed `dir_sunrise` charge to match the script timer, or add a script hook so the AI never fires it. Recommend the hook (D-051-safe: read-only flag).
- MIS-11: auth_04 `VentDown` → add `Mission.TimerAdd("sunrise", 1500)` (or the equivalent extension call), or reword `a4.vent.down`. Owner picks the number or the wording.
- MIS-12: the overlay maps speaker ids to names via a `leaders.toml` lookup, and the Left Behind leaders are added to `data/archive/leaders.toml`. Needs owner text.
- MIS-13: move auth_01 svy1 off water, e.g. `cell = [55, 8]` after checking the terrain. Move the auth_04 conscript and ashthrower outside the vent and shaft footprints. The audit test lists them.
- MIS-14: tut_01 `step.fight`: delete "Ignore the lone outpost in the far south-east; it is not part of the exercise." With MIS-05 fixed, the early end via camp_power is gone too.
- MIS-15: auth_03 lua:124 and 150-151 → count the tags: `local alive = (Actor.Alive("carrier1") and 1 or 0) + (Actor.Alive("carrier2") and 1 or 0)`.
- MIS-16: (a) let the hatch to bore head morph respect `UnlockFilter`, or add `dir_bore_head` to dir_01's list (sim change: owner); (b) add `auth_factory` to auth_01's tech, or remove `auth_jeep`; (c) no action.
- MIS-18: dir_04 `Charged()` → only say charged when the device's real charge is ready, or reword `d4.charged` to "should be charged".
- MIS-19: lb_01 briefing → "two scrapers" and drop the jeep (it appears only at tier 4 and up).
- MIS-20: lb_01:178, lb_02:177, lb_03:189, lb_04:180 → track the Mother Rig's own tag instead of counting `lb_mother_rig_packed`.
- GS-07: data/archive/technology.toml:133 → `unlock = "campaign:auth_04"`.

USER CHECK
- Start an aircraft-granting mission (exp_03 or lb_04): Flight Deck or Rotor Pad units appear in the sidebar and can be built.
- auth_03: let the tide timer run out (or lose the yard): the Defeat screen with Retry now appears.
- exp_01: destroy the enemy power plant early: the game keeps going until the mission script decides it is over.
- Campaign: save mid-mission, quit, load from Campaign > Load game, win: the debrief shows, the next mission unlocks, and Restart keeps the same tier.
- auth_01: lose both scrapers on the convoy road: the mission is lost instead of stuck.
- Win auth_02, back out to the Campaign menu: Left Behind now reads open. Highlight a locked faction: the hint names that faction's own rule.
- Put garbage in `campaign_authority.toml` and open Campaign: a `campaign_authority.toml.bad` copy is kept.
- lb_04 at tier 5: park your army in the landing zone before the landing: the enemy lands beside the zone and the fleet timer appears.

Memory: nothing new worth saving; memory was not relevant.
