# Pilot A1 "Survey Convoy" on schema 2 (M8-10a, session 12)

Template for D1 / L1 / E1. Agent time: about 0.7 agent hours (read 0.2, port 0.3, tests and fixes 0.2).

## What changed
- `mission.toml`: `schema = 2`, `act = 1`, `faction = "authority"`, `next = "auth_02"`, `locale = "locale.toml"`, `[starts_with]` (same six ids
  as `[[players]].tech`, which stays so the allow-list works outside the campaign), `[unlocks_on_win] tech = ["auth_turret"]` (A2 holds a junction
  and needs a defence). The two civilians got tags `svy1`, `svy2`. Map, actors, cash and zones are unchanged.
- `mission.lua`: same shape (convoy, survey site, two raids, build the refinery). New: hidden secondary objective `surveyors` (revealed when a
  scraper-side unit enters the site; failed when a civilian dies; completed on win if still active); one visible timer `raid2` ("Second raid",
  1500 ticks after the site is reached, replacing an `AfterDelay`); `Dialogue.Say` from `leader_auth_wrenfield` (hero); `Camera.Focus` on the
  jeep at start, the site on arrival and the yard when it lands; all messages through `Media.MessageKey`.
- Difficulty (`Mission.Difficulty()`, tiers 1-5): raid 1 rifles 2,2,3,4,5; raid 2 rifles 3,3,4,5,6 plus a jeep from tier 3. Tier 3 equals the
  old mission. Cash, vision and build speed do not change (D-061).
- Tests: `PilotMissionTests` win theory at tiers 3 and 5 (under 14000 ticks), idle still does not win (checks 2 visible + 3 total objectives);
  `MissionSchemaTests` has a schema-2 load test for auth_01. Two tests outside my list used auth_01 as a schema-1 fixture and were adjusted
  minimally: `M8_05/MissionLoaderLocaleTests.Folder` strips v2 keys from the copy; `MissionStateContractTests` format-1 case uses
  `M85Mission.Build` (auth_01 now has a hidden objective, so it writes format 2).

## Locale keys (data/missions/auth_01/locale.toml)
a1.start, a1.radio.start, a1.radio.chatter, a1.site.clear, a1.radio.down, a1.raid1, a1.raid2, a1.radio.civilians, a1.radio.civilians_lost,
a1.raiders.beaten, a1.radio.build, a1.lost, a1.win, a1.radio.win. `[voice]`: a1.radio.start, a1.radio.down, a1.radio.win
(`voice/leader_auth_wrenfield/<key>.ogg`, no files exist yet).
Convention: `<mission>.<topic>`; radio lines have `radio.` after the mission prefix and go through `Dialogue.Say`, messages through `Media.MessageKey`.

## What the API lacked
1. `Objective.Add` / `AddHidden` and `Mission.Timer` labels take plain text, not locale keys, so objective and timer texts stay in Lua.
   Wanted: key resolution there, or a `Locale.Text(key)` function.
2. The briefing is still `briefing.md`, not locale keys.
3. No portrait id list is documented; I used the leader id as the portrait id.
4. `Trigger.OnEnter` cannot say "first unit of type X"; the hidden objective is revealed by any player unit entering the site.
5. `--mission-check` on an idle player ends Lost (raiders win) at tick 14000; it reports `ok` with 0 errors.

## Lead follow-up (s12)
- Objective and timer labels can now be locale keys (`MissionHost.Label`, display only); auth_01 uses `a1.obj.*` and `a1.timer.raid2`.
- The HUD now shows running timers (`MissionObjectiveLines`); before this they existed only in the script. Hidden objectives no longer show on the panel (it read `Objectives`, not `VisibleObjectives`).
- Still open: the briefing (briefing.md) is not keyed; portrait ids are not used by the game yet (speaker id only).
