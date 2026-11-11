FILE: I could not write `/home/claude/audit/missions/findings.md`, because the harness refused report files from a subagent. The full findings are below instead. Analysis scripts are saved: `/home/claude/audit/missions/check.py`, `/home/claude/audit/missions/edges.py`, `/home/claude/audit/missions/tech.py`, with output in `/home/claude/audit/missions/check_out.txt`.
COUNTS: critical 3 / major 8 / minor 10 / idea 0
TOP 5:
- MIS-01 critical: in auth_03 and lb_03, losing the mission never ends the game. The player is left in a dead match with no Defeat screen.
- MIS-02 critical: winning a campaign mission after loading a save records nothing (no progress, unlocks, debrief or ending).
- MIS-03 critical: `Actor.Alive` lags `OnDestroyed` by one tick. auth_01 soft-locks if both scrapers die during the convoy, and several other defeats and bonuses break.
- MIS-04 major: aircraft can never be built in any tech-restricted mission (dir_04, exp_03, exp_04, lb_04).
- MIS-05 major: when the enemy loses its last building, the sim ends a campaign mission with "Victory" but the campaign does not advance. exp_01's only enemy building stands next to the wave spawn.
NOT COVERED: `src/Sim/Missions` (unit order handlers) in depth, the replay loader, running anything (no build or test, per the brief), and live AI behaviour in missions (I only read the hook code).

# missions: Scripting host, campaign flow, all 17 missions — findings
Scope read: all of `src/Scripting`; `Simulation.Scripting.cs` and `Simulation.MissionSpawns.cs`; the sim code they call (spawn, `CanPlace`, Occupancy, `RunTriggers`/`RunCleanup`, defeat and game over, `UnlockFilter`, `ApplyUpgrade`, aircraft ammo); `src/Game/Campaign`; `CampaignFlow`; the `GameScreen` mission, campaign and save factories; `GameLoader.LoadMission`; `MissionGame`; the `MissionOverlay` dialogue; the AI hook and superweapon code; every mission's lua, toml, locale and briefing; the campaign and ending files; `api.md`; `objective_targets.md`; `12_m8_plan.md` §3.3-3.5; `m8_15_acceptance.md`; the head of `s13_campaign_balance.md`; the owner's feedback.

## Summary (worst first)
1. **A loss can hang the game.** In auth_03 and lb_03 a third non-neutral team stays alive, so `GameOver` stays false after `Mission.Lose` (MIS-01).
2. **Loading a save drops the campaign.** The restore path has no campaign override, so a win after loading records nothing (MIS-02).
3. **`Actor.Alive` is one tick late.** It still reports a dying actor as alive inside its own `OnDestroyed` callback, which breaks auth_01 (soft-lock), auth_03, dir_01, exp_04 and five objective-marker re-points (MIS-03).
4. **Aircraft are never allowed.** `IsTypeAllowed` has no case for `ProductionCategory.Aircraft` (MIS-04).
5. **The sim can win for the player.** Its own elimination victory pre-empts `Mission.Win`, and campaign progress is lost (MIS-05). lb_01's first objective marker even points at the enemy's only building (MIS-06).
6. **Spawn failures stop missions silently.** A failed spawn aborts the callback halfway and the error is invisible. Tight spawn zones make this a soft-lock in lb_04, exp_04 and lb_03, and auth_01's yard airlift can fail too (MIS-07).
7. **Other major issues:**
   - dir_01 sends the enemy AI's rally point to the player's hatch site (MIS-08).
   - dir_04 is won by razing the HQ with any weapon (MIS-09).
   - Dialogue shows raw speaker ids, e.g. "LEADER_AUTH_WRENFIELD:" (MIS-12).

## Findings
### MIS-01 · critical · CODE · confidence high
**What:** `Mission.Lose(0)` only marks the human as Defeated. In auth_03 and lb_03 another non-neutral team survives, so `Simulation.GameOver` stays false.
**Where:**
- `MissionHost.Api.cs:416-433` (`EndMission`)
- `Simulation.Production.cs:534-549` (`ComputeGameOver` needs every survivor on one team)
- `GameScreen.cs:224` (`IsGameOver = sim.GameOver`)
- auth_03 slot 2 "The Drift": role neutral but team 2. lb_03: slots 1 and 2 on teams 1 and 2. Neither uses team 255, so neither counts as neutral.

**Evidence:**
- auth_03: all three defeats (lua:122-146) leave p1 (team 1) alive, and p2 (team 2) never had a base, so it is never defeated. `second` is true and the game never ends.
- lb_03: the overrun defeat (lua:127) only fires while hatches stand, so p2 is alive by construction.
- The tests only assert `Outcome == Lost` (AuthorityMission03Tests:38-45, LeftBehindMission03Tests), never `GameOver`.

**Impact:** The player sees the defeat message, then a frozen game where every command is refused. There is no Defeat screen and no Retry; the only way out is the pause menu.
**Fix idea:** For missions, end the screen on `Host.Outcome != None` or when the local player is defeated. Size S.

### MIS-02 · critical · CODE · confidence high
**What:** A campaign mission restored from a save loses its campaign wiring. A win records no progress or unlocks and shows no debrief or ending. Restart builds a non-campaign game.
**Where:**
- `GameScreen.cs:233-249` (`CreateFromSave`): no `_gameOverOverride`, and restart is `CreateMission(services, id)`.
- `CreateMission` (`GameScreen.cs:200-208`) uses AI tier 1, never calls `SetDifficulty` (so the script runs at tier 3), and applies no allow-list.
- `CampaignFlow.cs:20-34` is the only caller of `RecordWin`.
- The save header (`MissionGame.cs:69-86`) has no campaign flag, faction or tier.

**Evidence:** Campaign missions are saveable (`SimSnapshotSource.cs:143`, `GameScreen.cs:607`). `LoadGameScreen.cs:38` uses only `CreateFromSave`.
**Impact:** Save and reload during a 10-25 minute mission, then win: the plain Victory menu appears and the mission stays "Next".
**Fix idea:** When `host.Def.Faction` is set, wire the `CampaignFlow` override with tier `Host.DifficultyTier`, and route restart through `CampaignFlow`. Size S.

### MIS-03 · critical · CODE · confidence high
**What:** `Actor.Alive` is `TryTag && TryResolve`, so an actor at health 0 counts as alive until `RunCleanup`. `OnDestroyed` fires on that same tick (M8-17 `IsStanding`).
**Where:**
- `MissionHost.Api.cs:48`, compared with `MissionHost.cs:228,240-251`
- `Simulation.cs:263-265, 660-676`
- `Actor.Cell` and `TryObjectiveCell` (`Objectives.cs:158-161`) have the same lag.

**Evidence:**
- **auth_01 lua:55-63 `ScraperLost`:** when the last scraper dies, `Alive("scraper2")` is still true, so `Lose` never runs and both triggers are spent. In the convoy phase `CheckConvoy` needs `alive >= 1` (lua:68), so the mission can be neither won nor lost: a soft-lock. If the scrapers die later, the player can still win, and lua:139 completes "scrapers" with both dead.
- **auth_03 lua:122-130:** the carrier defeat does not fire. The loss waits for the 9000-tick tide timer and then hits MIS-01.
- **dir_01 lua:49-55:** the picket bonus can never complete.
- **exp_04 lua:46-53,169-172:** the Finale spawns on the second blow, not the first. If the shaft and the device die on the same tick, the Finale never spawns and `CheckWin` needs `state.finale`: a soft-lock (low odds).
- **Marker re-points** pick the dying actor: dir_02:61, lb_02:110, lb_03:149, lb_04:137, auth_01:57.

**Fix idea:** Make `Alive`, `Cell` and `TryObjectiveCell` use `IsStanding`, document it in `api.md`, and test killing the last escort in auth_01 and auth_03. Size S.

### MIS-04 · major · CODE · confidence high
**What:** `IsTypeAllowed` (`MissionHost.cs:302-315`) covers Infantry, Vehicle, Structure and Defence. Aircraft falls through to null, which means false. Air units exist only in the Aircraft category (`Simulation.Production.cs:24-41`), and `IsUnlocked`/`CanBuild` consult the filter (102-108, 143-148).
**Evidence:** These allow-lists include aircraft: dir_04 (dir_dirigible); exp_03 and exp_04 (exp_jet, exp_gunship, exp_skyhook); lb_04 (lb_kite, lb_rotor). exp_03's briefing is about the carrier's aircraft. No test builds an aircraft in a mission.
**Impact:** The Flight Deck, Mooring and Rotor Pad produce nothing in those missions.
**Fix idea:** Add `Vehicle or Aircraft => Vehicles[i].Id` to the switch, plus a test. Size S.

### MIS-05 · major · CODE · confidence high (mechanism) / medium (frequency)
**What:** The sim declares victory when the enemy team has no non-wall buildings left (`Simulation.Production.cs:514-549`). That ends the game before the script wins, `CampaignFlow.cs:26` returns null, and the normal "Victory" menu appears with nothing recorded. `api.md:181` and `test_template.md:57` ask writers to keep an anchor building, but nothing enforces it.
**Evidence:**
- exp_01: the only enemy building is `watch_power` at 66,50, 6 cells from the "roads" spawn zone.
- lb_01 and lb_02: the anchor is `auth_power` at 58,58, which is reachable (see MIS-06).
- auth_01 and dir_01: 3 enemy buildings. auth_02: 4. dir_02 and dir_03: posts plus a 3-building base. exp_02: reserve at 70,13. exp_04: reserve at 86,10. lb_04: 11 buildings.
- tut_01 ends early if `camp_power` dies before its riflemen; the comment at lua:137-139 is wrong about this.
- Safe: auth_03 and exp_03 (a never-defeated third team), auth_04 (device first), dir_04 (hq first).

**Fix idea:** Let only the script or the human's defeat end a scripted mission (same change as MIS-01). Size S.

### MIS-06 · major · DATA-REF · confidence high
**What:** lb_01 `lua:30` `Objective.SetTarget("deploy","anchor")`. The tag "anchor" belongs to player 1's `auth_power` at 58,58 (`mission.toml:72-76`), not the Mother Rig unpack site. The `objective_targets.md` row repeats the mistake.
**Impact:** The first Left Behind objective's arrow and J-jump send the player across the map to the enemy's only building. Destroying it ends the mission with no record (MIS-05).
**Fix idea:** Target a cell or zone near 24-26,20-22. Size S.

### MIS-07 · major · CODE · confidence medium
**What:** A failed spawn (no free cell, or a blocked building site) throws halfway through a callback.
- Units spawned before the failure stay on the map, but their tags are lost.
- State flags set before the call stay set, and timers or triggers scheduled after it are skipped.
- The error goes to `Host.Errors`, which only `--mission-check` shows.
- Code: `Reinforce.cs:153-181`, `MissionHost.cs:253-266`, `Api.cs:248-264`.

**Evidence** (zone capacity from check.py; one soldier fits per cell):
- **lb_04 Landing (lua:106-118):** `landing` is set to true first, then up to 16 units go into the 21-cell "landing" zone, which lies on the player's route to the beach.
  - If the spawn fails, the fleet defeat timer and `SecondLanding` are never set up. At tiers 4-5 "counter" can never complete: a soft-lock.
  - At tiers 1-3 the objective completes at once and the defeat timer never starts.
- **exp_04 Finale (174-198):** 17 to 66 units go in groups of 12 every 20 ticks into the 44-cell "gate", where the player's army is standing. One failed group stops the chain, and `CheckWin` needs it to finish: a soft-lock. ColumnThree at tier 5 puts 29 units into the same 44 cells.
- **lb_03 HatchWave (123-136):** up to 18 units into the 18-cell "mouth" next to hatch1. If it fails, waves stop for good and the overrun defeat can never fire.
- **lb_03 SendColumn:** 13 units into the 14-cell "north" zone. If it fails, "column" completes while the column is still alive.
- **auth_01 CheckRaids (117-132):** the phase and "hold" are set before `Actor.Create(auth_conyard,{23,25})`. Any unit standing on the 3x3 makes `CanPlace` refuse (`Simulation.cs:453-457`), so the refinery objective is never created: a soft-lock.
- **Lower odds, same pattern:** the waves and columns counters in dir_03 and exp_01-03. exp_03 `FleetArrives` sets `arrived` before spawning, so a failed spawn means an instant "carrier lost" defeat.

**Fix idea:** Let spawns overflow outside the zone or retry next tick, and return partial tag lists. Show `Errors` and `Halted` in the overlay. In scripts, set flags after the spawn. Use `FreeCellNear` for the yard. Size M.

### MIS-08 · major · CODE · confidence medium
**What:** dir_01 `lua:24-25` comments "The enemy base stays home" and then calls `AI.Defend(1, "site")`. "site" is the player's hatch zone, and `RecruitAi.cs:306` turns it into the AI's rally cell. Every other mission defends an enemy zone.
**Impact:** The Authority AI (1500 cash, a barracks) sends every unit it builds onto the player's hatch, on top of the scripted waves. No campaign test attaches the AI.
**Fix idea:** Add an `enemy_base` zone around (52-62, 52-62) and defend that. Size S.

### MIS-09 · major · DESIGN · confidence high
**What:** dir_04 `lua:34,122-131` wins on any destruction of "hq": rocket trucks, conscripts, or an AI sale all count. Before the charge it also completes "hold". The briefing says to fire the Device.
**Impact:** The finale's central action can be skipped, and the debrief still says the Sunrise fired.
**Fix idea:** Require the player's Special command (`charged` plus a fired flag), or make the hq untouchable. Size S.

### MIS-10 · minor · DESIGN · confidence low
**What:** At tiers 4-5 the AI fires any ready superweapon it owns (`difficulty.toml:131,157`; `RecruitAi.cs:332`; `SuperweaponManager`). auth_04 and exp_04 pre-place an AI-owned `dir_sunrise` that charges in 3000 ticks (entrance range 8), while the script's timer says 16000.
**Impact:** Story and sim can disagree. This is untested because no test attaches the AI.
**Fix idea:** Match the charge time to the timer, or add a hook that tells the AI not to fire. Size S.

### MIS-11 · minor · TEXT · confidence high
auth_04 promises the vent kill slows the charge (`a4.radio.dray`, `a4.vent.down`). `VentDown` (lua:104-110) leaves the "sunrise" timer unchanged. **Fix idea:** extend the timer in `VentDown`, or reword. Size S.

### MIS-12 · major · TEXT · confidence high
**What:** `MissionOverlay.cs:80-84` shows `Speaker.ToUpperInvariant() + ": "` using the raw id: "LEADER_AUTH_WRENFIELD: …" on about 140 lines across all 16 missions, and "INSTRUCTOR:" in the tutorial. The portrait argument is never drawn. `leader_lb_calloway_ross` (and every Left Behind leader) is missing from `data/archive/leaders.toml`.
**Fix idea:** Map ids to display names (a locale `[speakers]` table or `leaders.toml`), and add the LB leaders. Size S.

### MIS-13 · minor · CODE · confidence high
**What:** Some spawns skip terrain and occupancy checks: mission.toml actors, `Actor.Create`, `Reinforce.Edge` and `Reinforce.Delayed`. `SpawnVehicle` and `SpawnInfantry` (`Simulation.cs:194-232`) never check, and `Occupancy.Claim` (98-106) silently skips a cell another vehicle holds. `api.md` contradicts itself: Delayed spawns "exactly as Edge", yet "nothing is stacked".
**Evidence:**
- auth_01 `svy1` at 55,6 is on water.
- auth_04 conscript at 44,79 is inside a reactor vent; the ashthrower at 47,81 is inside the shaft.
- The edge cells used today are passable (edges.py).

**Fix idea:** Validate actor cells in the loader or a data test, and route Delayed through `FreeCellNear`. Size S.

### MIS-14 · minor · TEXT · confidence high
tut_01 `step.fight` says "Ignore the lone outpost in the far south-east", but no outpost exists. `test_template.md:57` still cites tut_01's `reserve`. Without it the tutorial ends via MIS-05. Size S.

### MIS-15 · minor · DESIGN · confidence high
**What:** auth_03 counts every `auth_carrier`, including buildable ones (lua:122-130, 148-151). Built carriers replace the survey carriers. A spare carrier left at home blocks the escort objective until it is moved. The briefing's "parties aboard" carriers start empty.
**Fix idea:** Track the `carrier1` and `carrier2` tags. Size S.

### MIS-16 · minor · CODE · confidence high
- (a) The hatch→bore head morph (`Simulation.Burrow.cs:119-136`) ignores `UnlockFilter`. `dir_bore_head` is in no allow-list, and dir_01 relies on the morph anyway.
- (b) auth_01 grants `auth_jeep`, but its prerequisite `auth_factory` is not allowed, so the jeep is listed but unbuildable.
- (c) Final-mission unlocks only matter in replays, and replays of act 1 get the whole tech set (by design, plan 3.3).

Size S.

### MIS-17 · minor · CODE · confidence medium (latent)
**What:** The sandbox and state codec have latent determinism and robustness holes. No shipped script triggers them today.
- (a) `next` is still in the globals. It iterates in insertion order, which changes after a load rebuilds `state` in canonical order (`Sandbox.cs:449-453`).
- (b) A sub-table referenced from two places in `state` comes back as two copies after load (`LuaStateCodec.cs:623-662`).
- (c) One fractional value in `state` makes `ValidateState` halt the mission's script permanently, with no retry (`MissionHost.cs:273-286`).
- (d) A huge `string.rep` throws `OutOfMemoryException`, which `Invoke` (line 264) deliberately does not catch, so the game crashes.

Size S.

### MIS-18 · minor · TEXT · confidence medium
dir_04 says "the device is charged" when its own 3050-tick timer runs out (lua:37, 113-120), even if a power loss paused the real charge. Size S.

### MIS-19 · minor · TEXT · confidence high
lb_01's briefing says "two scrapers, a jeep", but the jeep only appears at tier 4 and up (lua:55-62). Size S.

### MIS-20 · minor · CODE · confidence high
The Left Behind "rig lost" check (lb_01:178, lb_02:177, lb_03:189, lb_04:180) also counts `lb_mother_rig_packed`. Every LB rig packs into that type (`buildings.toml:656-763`), so any packed rig keeps the player "alive". Size S.

### MIS-21 · major · TEST · confidence high
**What:** No test covers the paths above:
- The campaign walks never attach the AI.
- Loss tests check `Outcome`, never `GameOver`.
- No test loads a save and then wins, kills the last escort, builds an aircraft, wipes the enemy's buildings, or checks objective marker targets.

**Fix idea:** Add those cases to `Scripting.Tests` and `Game.Tests`. Size S-M.

## Per-mission verdicts
| Mission | Verdict |
|---|---|
| tut_01 | Text names an outpost that does not exist; can end early (harmless) — MIS-14, MIS-05, MIS-12 |
| auth_01 | **Soft-lock** if both scrapers die in the convoy phase; the yard airlift can fail — MIS-03, MIS-07, MIS-05, MIS-13, MIS-16b |
| auth_02 | Script is sound; razing the base wins with no record — MIS-05 |
| auth_03 | **Every defeat hangs the game**; carrier defeat delayed — MIS-01, MIS-03, MIS-15 |
| auth_04 | Vent text false; AI device at tiers 4-5; units inside buildings — MIS-11, MIS-10, MIS-13 |
| dir_01 | AI rallies onto the player's site; bonus impossible — MIS-08, MIS-03, MIS-05 |
| dir_02 | Flow sound; posts marker re-point wrong — MIS-03, MIS-05 |
| dir_03 | Sound (roomy spawn zones) — MIS-05, MIS-07 (low) |
| dir_04 | Win by razing hq; dirigible locked; "charged" can be false; bore head morph — MIS-09, MIS-04, MIS-18, MIS-16a |
| exp_01 | Script sound; one enemy building next to the spawn ends the mission — MIS-05 |
| exp_02 | Sound (anchorage placement proven by a test) — MIS-05 |
| exp_03 | Aircraft locked; fleet spawn order — MIS-04, MIS-07 (low) |
| exp_04 | Finale gate soft-lock risk; finale on the 2nd blow — MIS-07, MIS-03, MIS-04, MIS-10, MIS-05 |
| lb_01 | First objective marker points at the enemy anchor — MIS-06, MIS-05, MIS-19, MIS-20 |
| lb_02 | Flow sound — MIS-03, MIS-20, MIS-05 |
| lb_03 | **Defeats hang**; mouth/north zones too small at tiers 4-5 — MIS-01, MIS-07, MIS-20 |
| lb_04 | Landing failure is a soft-lock at tiers 4-5; kite and rotor locked — MIS-07, MIS-04, MIS-20 |

## Open questions
- Should replaying an early mission really get the whole campaign tech set?
- Every `mission.lua` edit makes that mission's mid-mission saves unloadable (`GameLoader.cs:79`), and edits to `MessageKey` text break replays. Is that acceptable while the owner keeps rewriting text (D-086)?
- dir_04: should killing the hq conventionally be a loss, be ignored, or be impossible?
- Ending slides conditioned on `completed *_03` always show at the ending. Is that intended?

## Things checked and found sound
- **Campaign data:** `next` ids and `requires` agree, and the `open_after` ids exist. Tech carries forward with nothing lost between acts. Every allow-list id exists, and every structure or unit a script requires is reachable through prerequisites.
- **Script tables and references:** all tier tables have 5 entries. Every zone, tag and locale key the scripts reference exists.
- **Script state:** no script keeps mutable globals outside `state`. Shipped scripts use no `next`, floats or `os` calls.
- **Sandbox:** removals are tight, `math` is integer-only, `pairs` is ordered, and the script RNG is seeded and saved.
- **Instruction budget:** a runaway callback becomes a recorded error and the sim keeps running.
- **State part:** versioned append-only, reads all 5 formats, and is guarded by the script hash. Timers, counters, arrivals, hidden flags, targets and the tier are all saved.
- **Spawn data:** edge and naval spawn cells are passable or water, and exp_03's cove has deep water.
- **Briefings and art:** briefing range claims match the weapons data. All 12 act stills exist. Ending images are missing, but that is a known, handled gap.

Memory note: nothing new worth saving; memory was not relevant to this task.