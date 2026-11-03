# Campaign pilot report: Authority 01 "Survey Convoy" (M2-15, D-061)

Purpose: measure what one scripted mission costs, so the owner can size the 10 to 12 missions per side (D-030, Q-30).

## What was built
- `src/Scripting` (Rts.Scripting): MoonSharp 2.0.0 host `MissionHost`, plugged into the Sim through one generic
  `ISimExtension` / `SimSetup.Extensions` hook. The Sim does not reference MoonSharp.
- Determinism sandbox (review A-7): integers only at the API boundary, no `os`/`io`/`debug`/`load*`/`require`/`coroutine`,
  no `math.random` and no float math (only `abs min max floor ceil`), `pairs` iterates in canonical key order,
  `Random.Next(n)` has its own generator inside the saved state.
- API: Trigger (AfterDelay, Every, OnEnter, OnDestroyed, OnAllDestroyed), Actor (Create, Order, Alive, Count, CountInZone, Cell),
  Reinforce.Edge, Objective (Add, Complete, Fail), Tech (Grant, Revoke, Has), Media.Message, Player (Cash, AddCash),
  Map (Zone, Center, Width, Height), Mission (Win, Lose, Tick), Random.Next. About 30 functions, as D-015 said.
- Mission data: `data/missions/auth_01/{mission.toml, mission.lua, briefing.md}`.
- Game: Main menu "Campaign" then "Mission 1: Survey", `BriefingScreen`, mission HUD (objectives panel, message ticker),
  GameOverScreen reused, `--mission-check <id> <ticks>`.
- Tests: `tests/Scripting.Tests` (35 tests, about 16 s) including a bot that wins the mission in under 12000 ticks.

## Hours
Honest note: this was written by an AI helper in one session, so the clock hours below are agent working time, estimated from
the order of work (the sandbox clock was not reliable enough to time individual parts). The last column is my estimate of what the
same part would cost the owner working by hand. Treat the human column as the planning number.

| Part | Agent hours (est.) | Human-equivalent hours (est.) |
|---|---|---|
| Host: Sim hook, state part, save/load, sandbox, trigger engine, tech gate | 1.0 | 10 to 14 |
| API: 30 functions, argument checks, actor/zone/tag handling, Reinforce | 0.6 | 5 to 7 |
| Mission script (`mission.lua`, `mission.toml`, zones, actor placement) | 0.3 | 3 to 4 |
| Briefing text | 0.05 | 1 |
| Game UI: menu, briefing screen, objectives and ticker, check command | 0.5 | 4 to 6 |
| Testing and tuning (35 tests, bot play-through, balance of the two raids) | 0.5 | 4 to 6 |
| Docs and credits | 0.1 | 1 |
| Total | about 3 | about 28 to 39 |

The host, API, UI and test harness are one-time costs (roughly 24 to 33 human hours of the total). They are now reusable.

## Cost per future mission
Marginal cost of a mission of this size, with the host in place:

| Item | Human hours |
|---|---|
| Mission design on paper (map use, beats, tech, difficulty) | 2 |
| `mission.toml` (zones, pre-placed actors, player tech) | 1 |
| `mission.lua` (about 100 lines for this size) | 2 to 3 |
| Briefing and radio text | 1 |
| Tuning: play it 4 to 6 times, adjust raids and cash | 3 to 4 |
| A bot play-through test (copy and adapt `PilotMissionTests`) | 1 |
| Total per mission | about 10 to 12 (8 for a simple defend or escort, 15 or more for a build-and-destroy with a full base) |

For one campaign of 10 to 12 missions plus a tutorial: roughly 110 to 150 human hours of mission work, before any art, music or
cutscene stills. Both sides: about 220 to 300. Missions that need new engine features (transports, captured buildings, timers shown
on screen, cutscene triggers, discovery triggers) add the cost of those features once, not per mission.

## What was hard
1. Save and load with closures. Lua functions cannot be saved, so a trigger keeps the global name of its callback and the script
   file only defines functions; `OnInit` runs once on a new game. Authors must write top-level named functions. This is the
   main rule scripters will trip on (the error message says so).
2. Lua state vs. hashing. The `state` table is walked in canonical order at every hash and save. A bad value (a fraction, a function) would
   break the save, so state is validated after every tick that ran a callback and the script is halted with an error instead.
3. The attack-move order had no scripting entry; it needed combat internals, so one small Sim method was added.
4. Build radius is 2 cells (game.toml), so the refinery objective needed a forward Construction Yard dropped by the script near the pit.
   This is a mission-design constraint to remember: a player with no base cannot build.
5. Nothing stops an infinite loop in a script (MoonSharp has no instruction limit). Fine for our own missions, not for mods (D-019).
6. Balance has no tooling: raid strength was tuned by a bot play-through. A faster "run N seeds" harness would pay off at mission 3.

## Open items
- Replays do not cover missions yet: script-driven spawns are not in the setup log, so `replay verify` cannot rebuild a mission
  start. The mission session records nothing. The script hash is in the saved "mission" part (a save from another script is refused).
- The sidebar lists `auth_jeep` as unlocked-by-tech, but it needs a Factory, which is not on the mission list, so it cannot be built.
- Neutral civilians are two idle riflemen (the roster has no civilian types).
