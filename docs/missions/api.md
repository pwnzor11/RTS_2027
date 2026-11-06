# Mission Lua API (M8-02 contract)

Binding for M8-03..M8-06. Source of truth for the function list is `MissionApiContract.Entries` (src/Scripting/MissionApiContract.cs);
`ApiContractTests` fails if this table and the registered functions disagree. Plan: docs/design/12_m8_plan.md section 3.1 and 4.

## Rules for every function
- Only integers cross the boundary (a non-integral number is a script error). Cells are `{x=, y=}` (or `{x, y}`), players are slot numbers,
  types are rules ids (strings), zones are names from `mission.toml`, actors are tags (strings).
- Nothing may be called while the script file loads; call from `OnInit` or a callback. Callbacks are top-level named functions.
- A bad argument raises a script error that starts with the function name (`Mission.Timer: argument 2 must be ...`).
- Status column: **exists** (pilot, unchanged, tests prove it), **implemented** (complete now), **no-op** (validates, then does nothing),
  **throws** (validates, then raises `Table.Fn: not implemented (M8-0x)`). The owning package replaces the stub body, keeps the
  signature and the argument checks, and updates the status in `MissionApiContract`.
- Outputs (dialogue, camera hints) are read by the game layer after the tick and never feed back into the sim. Anything the sim or the
  AI reads is in the hashed "mission" part.

## Pilot functions (exist, do not change)
`Trigger.AfterDelay(ticks, fn)`, `Trigger.Every(period, fn)`, `Trigger.OnEnter(zone, player, fn)`, `Trigger.OnDestroyed(tag, fn)`,
`Trigger.OnAllDestroyed(player, fn)`, `Actor.Create(type, player, cell[, tag])`, `Actor.Order(tag, "move"|"attackmove", cell)`,
`Actor.Alive(tag)`, `Actor.Count(player[, type])`, `Actor.CountInZone(zone, player[, type])`, `Actor.Cell(tag)`,
`Reinforce.Edge(player, {types}, edgeCell, destCell)`, `Objective.Add(id, text, primary)`, `Objective.Complete(id)`, `Objective.Fail(id)`,
`Tech.Grant(player, type)`, `Tech.Revoke(player, type)`, `Tech.Has(player, type)`, `Media.Message(text)`, `Player.Cash(player)`,
`Player.AddCash(player, delta)`, `Map.Zone(name)`, `Map.Center(zone)`, `Map.Width()`, `Map.Height()`, `Mission.Win(player)`,
`Mission.Lose(player)`, `Mission.Tick()`, `Random.Next(n)`.

## New functions
Common argument kinds: `id` = 1-64 characters, no spaces. `fn` = global function name or top-level function value.

### Objectives, timers, area triggers, win/lose helpers, tech helpers (M8-03, file MissionHost.Objectives.cs)
| Function | Arguments | Returns | Checks | Status |
|---|---|---|---|---|
| `Trigger.OnLeave(zone, player, fn)` | zone name, slot, callback | nil | zone known, player valid, fn global; fires once, the first time the zone is empty of the player's mobile units after one was inside (never just because it started empty) | implemented (M8-03) |
| `Trigger.OnHold(zone, player, ticks, fn)` | as above plus ticks 1..1e9 | nil | as above; fires once when a mobile unit of player has been in the zone `ticks` ticks in a row (counter resets when the zone empties; counting starts the tick the trigger is made) | implemented (M8-03) |
| `Trigger.OnSeen(zoneOrTag, player, fn)` | zone name or actor tag, slot, fn | nil | target is a zone or a known tag; fires once when any cell of the zone (or the tagged actor's cell) is visible to the player or a teammate; a tag whose actor died unseen is dropped | implemented (M8-03) |
| `Trigger.OnIdle(tag, ticks, fn)` | actor tag, ticks 1..1e9, fn | nil | tag known; fires once after the actor has had no mission (none, guard, sleep) and nothing queued for `ticks` ticks in a row; dropped if the actor dies | implemented (M8-03) |
| `Objective.AddHidden(id, text, primary)` | id, text, boolean | nil | primary must be a boolean; id unused | implemented (M8-03) |
| `Objective.Reveal(id)` | id | nil | objective exists | implemented (M8-03) |
| `Objective.Status(id)` | id | "active"/"completed"/"failed" | objective exists | implemented (M8-03) |
| `Mission.Timer(id, ticks, fn[, label])` | id, ticks 1..1e9, fn, optional HUD text | nil | fn global; label a string if given; the same id restarts the timer; expiry in creation order, once | implemented (M8-03) |
| `Mission.TimerCancel(id)` | id | nil | id well-formed | implemented (M8-03) |
| `Mission.TimerLeft(id)` | id | ticks left, or nil (unknown or already fired) | id well-formed | implemented (M8-03) |
| `Mission.AllPrimaryDone()` | none | boolean | none | implemented (M8-03) |
| `Mission.AnyPrimaryFailed()` | none | boolean | none | implemented (M8-03) |
| `Mission.Difficulty()` | none | tier 1..5 chosen for this run (default 3; M8-07 calls `MissionHost.SetDifficulty(int)` before the first Step; saved with the mission part as format 3, written only when not 3) | none | implemented (M8-03) |
| `Tech.GrantMany(player, {types})` | slot, 1..64 type ids | nil | every id is a known rules type | implemented (M8-03) |
| `Tech.List(player)` | slot | list of granted ids (sorted) | player valid | implemented (M8-03) |

Hidden objectives (`Objective.AddHidden`, later `Reveal`) set `MissionObjective.Hidden`; the HUD lists `MissionHost.VisibleObjectives`.
Timers live in `MissionHost.Timers` (shown by the HUD). Both are saved in the version-2 tail of the "mission" part.

Hidden objectives (`AddHidden`) are saved in the version-2 tail; the difficulty tier is a version-3 tail field. Trigger counters (`TriggerRecord.Counter`; Leave uses 1 = armed) are saved so a mid-hold save reproduces the same hash.

### Reinforcements and spawn groups (M8-04, file MissionHost.Reinforce.cs; all throw until M8-04)
| Function | Arguments | Returns | Checks | Status |
|---|---|---|---|---|
| `Reinforce.Zone(player, {types}, zone, destCell)` | slot, 1..32 type ids, spawn zone, cell | list of tags | types known, zone known, cell in map | throws M8-04 |
| `Reinforce.Transport(player, {types}, edgeCell, destCell)` | as Edge; arrives by a transport | list of tags | edge cell on the map edge | throws M8-04 |
| `Reinforce.Delayed(ticks, player, {types}, edgeCell, destCell)` | ticks 1..1e9, then as Edge | nil | as Edge | throws M8-04 |
| `Reinforce.Naval(player, {types}, edgeCell, destCell)` | Expedition ships arrive from the sea edge | list of tags | edge cell on the map edge | throws M8-04 |
| `Reinforce.Rig(player, edgeCell, destCell)` | Left Behind rig arrives and may deploy | tag | edge cell on the map edge | throws M8-04 |
| `Actor.CreateGroup({types}, player, cell[, opts])` | opts `{formation="line"\|"column"\|"box"\|"scatter", rank=0..3, tagPrefix="x"}` | list of tags | opts keys validated | throws M8-04 |

### Dialogue, camera, locale (M8-05, file MissionHost.Media.cs; no-ops until M8-05)
| Function | Arguments | Returns | Checks | Status |
|---|---|---|---|---|
| `Dialogue.Say(speaker, portrait, key[, voice])` | ids; voice = `"voice/<speaker>/<key>.ogg"` (D-154) | nil | ids well-formed; voice path shape | no-op (M8-05) |
| `Dialogue.Clear()` | none | nil | none | no-op (M8-05) |
| `Camera.Focus(zoneOrTag, ticks)` | zone name or tag, ticks 1..100000 | nil | target exists | no-op (M8-05) |
| `Media.MessageKey(key)` | locale key | nil | id well-formed | no-op (M8-05) |

Output only. M8-05 appends `MissionDialogue` records to `MissionHost.Dialogue` and sets `MissionHost.CameraHint`; neither is saved or hashed.

### AI hooks (M8-06, file MissionHost.Ai.cs; implemented as flag setters now)
| Function | Arguments | Returns | Checks | Status |
|---|---|---|---|---|
| `AI.Attack(player, zone)` | AI slot, zone | nil | player's role is "ai"; zone known | implemented |
| `AI.Defend(player, zone)` | AI slot, zone | nil | as above | implemented |
| `AI.SetTier(player, tier)` | AI slot, tier 1..5 | nil | as above | implemented |
| `AI.Wave(player, waveId, zone)` | AI slot, id, zone | nil | as above; bumps `WaveSeq` | implemented |
| `AI.Clear(player)` | AI slot | nil | as above; clears attack, defend, tier, wave id (keeps `WaveSeq`) | implemented |

D-051: these never run AI code inside `Step`. They write `AiHookState` (hashed mission state); the AI reads `MissionHost.AiHookOf(slot)` on
its next decision, outside Step. M8-06 owns the reader in src/AI and any extra hook kinds (append to the version-2 tail).

## Examples
```lua
-- M8-03
function OnInit()
  Objective.Add("hold", "Hold the junction", true)
  Objective.AddHidden("bonus", "Save the surveyors", false)
  Mission.Timer("relief", 3600, ReliefArrives, "Relief column")
  Trigger.OnHold("junction", 0, 300, JunctionHeld)
  Trigger.OnLeave("junction", 0, JunctionAbandoned)
  Trigger.OnSeen("depot", 0, DepotSpotted)
  Trigger.OnIdle("scout", 600, ScoutIdle)
end
function JunctionHeld() Objective.Complete("hold"); if Mission.AllPrimaryDone() then Mission.Win(0) end end
function ReliefArrives() Tech.GrantMany(0, { "auth_tank", "auth_turret" }) end
-- M8-04
Reinforce.Zone(0, { "auth_rifleman", "auth_jeep" }, "landing", Map.Center("junction"))
Reinforce.Transport(0, { "auth_tank" }, { x = 0, y = 20 }, { x = 20, y = 20 })
Reinforce.Delayed(900, 1, { "auth_rifleman" }, { x = 63, y = 30 }, { x = 30, y = 30 })
Reinforce.Naval(0, { "exp_landing_craft" }, { x = 0, y = 40 }, { x = 12, y = 38 })
Reinforce.Rig(0, { x = 63, y = 10 }, { x = 40, y = 12 })
local tags = Actor.CreateGroup({ "auth_rifleman", "auth_rifleman" }, 0, { x = 30, y = 30 }, { formation = "box", rank = 1 })
-- M8-05
Dialogue.Say("strand", "strand_stern", "m1.warning", "voice/strand/m1.warning.ogg")
Camera.Focus("depot", 120)
-- M8-06
AI.SetTier(1, 3); AI.Defend(1, "enemy_base"); AI.Attack(1, "junction"); AI.Wave(1, "raid_a", "north_edge")
```

## Data formats (M8-02)
- **Mission schema 2** (`[mission] schema = 2`; schema 1 / no key = pilot, unchanged): extra `[mission]` keys `act` (1..4), `faction`
  (authority, directorate, left_behind, expedition), `next` (id or list of 1-2 ids), `locale` (file name); tables `[starts_with] tech = [...]`
  and `[unlocks_on_win] tech = [...]`. Schema-2 keys in a schema-1 file are an error. Loader: `MissionLoader`, record: `MissionDef`.
- **Campaign list** `data/campaigns/<faction>.toml`, **progress file** `campaign_<faction>.toml` beside settings.toml, **ending file**
  `data/campaigns/<faction>_ending.toml`: types in src/Game/Campaign (`CampaignDef`, `CampaignProgress`, `EndingDef`); samples in data/campaigns.
  Progress is game-layer data, read-only to scripts, never part of a sim save.
- **Save format:** `SimWriter.SaveVersion` is unchanged. The "mission" part writes its old layout (format 1) unless a timer, hidden objective,
  trigger extra or AI hook flag is set, and then writes format 2 (the old bytes plus an appended tail). Both are read.
