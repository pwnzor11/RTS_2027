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

Hidden objectives (`AddHidden`) are saved in the version-2 tail; the difficulty tier is a version-3 tail field; queued reinforcement arrivals are the version-4 tail (M8-04), written only while one is queued. Trigger counters (`TriggerRecord.Counter`; Leave uses 1 = armed) are saved so a mid-hold save reproduces the same hash.

### Reinforcements and spawn groups (M8-04, file MissionHost.Reinforce.cs; implemented)
| Function | Arguments | Returns | Checks | Status |
|---|---|---|---|---|
| `Reinforce.Zone(player, {types}, zone, destCell)` | slot, 1..32 type ids, spawn zone, cell | list of tags | types known, zone known, cell in map | implemented |
| `Reinforce.Transport(player, {types}, edgeCell, destCell)` | as Edge; arrives by a transport | list of tags | edge cell on the map edge | implemented |
| `Reinforce.Delayed(ticks, player, {types}, edgeCell, destCell)` | ticks 1..1e9, then as Edge | nil | as Edge | implemented |
| `Reinforce.Naval(player, {types}, edgeCell, destCell)` | Expedition ships arrive from the sea edge | list of tags | edge cell on the map edge | implemented |
| `Reinforce.Rig(player, edgeCell, destCell)` | Left Behind rig arrives and may deploy | tag | edge cell on the map edge | implemented |
| `Actor.CreateGroup({types}, player, cell[, opts])` | opts `{formation="line"\|"column"\|"box"\|"scatter", rank=0..3, tagPrefix="x"}` | list of tags | opts keys validated | implemented |

Behaviour (M8-04). Every spawn uses the pilot path (`Simulation.SpawnVehicle` / `SpawnInfantry`) plus the scripting orders the pilot uses; the
few extra operations (board, unload, unpack, rank) are the public wrappers in `src/Sim/Simulation.MissionSpawns.cs`, which run the rules a
player command runs. No new sim command and no new sim state. Reinforcements are units: a building type is a script error. Every spawn takes
the free cell nearest the wanted one (right terrain, no building, no vehicle, no soldier; search radius 10), so nothing is stacked; if none
is free the call is a script error and the units spawned before it stay.
- `Reinforce.Zone`: units appear on free cells of the zone, nearest its centre first, then attack-move to `destCell`. Tags come back in list order.
- `Reinforce.Delayed`: queued; `ticks` after the call the units appear exactly as `Reinforce.Edge` places them (block just inside the edge,
  attack-move to `destCell`). Errors at arrival time (no room) are collected in `Errors`, not thrown at the call. No tags are returned.
- `Reinforce.Transport`: soldiers in the list ride; any other type drives in by itself like `Edge`. The transport is the smallest ground type
  of the units' faction that has passenger seats (and no `unload_ticks`), the largest if none fits all; more soldiers than seats means more
  transports. The transport spawns on the edge cell, soldiers aboard, and moves to `destCell`; it unloads (the transport Deploy rules) once
  within 1 cell of `destCell`, or after 3000 ticks wherever it is. The returned list holds one tag per listed type, in order; the transport
  itself is not tagged.
- `Reinforce.Naval`: ships in the list (naval locomotion) spawn on the water at or nearest `edgeCell`; every other entry must fit aboard a
  ship of the list (soldiers use `passengers`, ground vehicles `vehicle_slots`, else a script error and nothing spawns) and starts aboard.
  A ship with cargo sails to a water cell beside `destCell` and lands its cargo on it (the landing-craft Unload order, `destCell` is the shore
  cell); an empty ship, or one whose landing order is refused, sails to the water cell nearest `destCell`. Works for any faction that owns
  such types; air and burrowing types are rejected.
- `Reinforce.Rig`: spawns the faction's packed rig (the packed rig of the Left Behind start kit) on the edge. If `destCell` is a legal unpack
  site now it drives there and unpacks (a normal Pack order, so the unpack timer and site rules apply); otherwise it only moves to `destCell`
  and stays packed for the script to deal with. Returns the rig's tag.
- `Actor.CreateGroup`: formations around `cell`: `line` along x, `column` along y (each alternating right/left or down/up of the centre), `box`
  a square filling row by row, `scatter` a random offset of -3..3 per axis from the script's own generator (`Random.Next`'s stream, so it is
  saved and replays). `box` is the default. `rank` raises every member's veterancy to that level (0..3) the way kills would (it needs
  `[veterancy] enabled`, a type that can rank, and is ignored for buildings). `tagPrefix` names the members `prefix1`, `prefix2` ... and an
  already used tag is an error before anything spawns; without it tags are generated. Building types are placed at the wanted cell.
- Save format: queued `Delayed` arrivals and transports still driving are the version-4 tail of the "mission" part (after the M8-03 difficulty tier) (`PendingArrival`), written
  only while at least one is queued (with the version-2 bytes in front of it); with none queued the part is byte-for-byte what it was.
### Dialogue, camera, locale (M8-05, file MissionHost.Media.cs; no-ops until M8-05)

### Dialogue, camera, locale (M8-05, file MissionHost.Media.cs; implemented)
| Function | Arguments | Returns | Checks | Status |
|---|---|---|---|---|
| `Dialogue.Say(speaker, portrait, key[, voice])` | ids; voice = `"voice/<speaker>/<key>.ogg"` (D-154); without it the locale's voice for the key | nil | ids well-formed; voice path shape | implemented (M8-05) |
| `Dialogue.Clear()` | none | nil | none | implemented (M8-05) |
| `Camera.Focus(zoneOrTag, ticks)` | zone name or tag, ticks 1..100000 | nil | target exists | implemented (M8-05) |
| `Media.MessageKey(key)` | locale key | nil | id well-formed | implemented (M8-05) |

Output only. `Dialogue.Say` appends a `MissionDialogue` (speaker, portrait, key, resolved text, voice) to `MissionHost.Dialogue`, a FIFO queue of at most `MissionHost.MaxDialogueQueue` (16) lines: a full queue drops the oldest line; `Dialogue.Clear` empties it. `Camera.Focus` sets `MissionHost.CameraHint` (target name, centre cell, `UntilTick`); the latest call wins. Neither is saved or hashed, and the sim never reads them. `Media.MessageKey` is the exception: it appends to the hashed message list, like `Media.Message`, so its text is part of the hash.

Locale: `locale = "locale.toml"` in `[mission]` (schema 2) loads `data/missions/<id>/locale.toml`: flat `key = "text"` lines (dotted keys quoted), plus `[voice]` lines `key = "voice/<leader_id>/<key>.ogg"`. An unknown key resolves to `[key]`; bad lines and a missing file are listed in `MissionLocale.Warnings`. The game shows a dialogue line for 120 ticks and honours a camera hint once, and plays a voice file only when it exists under `data/audio`.
Labels (s12): an objective text or timer label that is a key in the mission's locale is shown as the locale text on the HUD and the briefing (`MissionHost.Label`, display only; the hashed state keeps the key). Running timers show under the objectives as `label m:ss`. Hidden objectives stay off the panel until revealed.

### AI hooks (M8-06, file MissionHost.Ai.cs; the AI side is live)
| Function | Arguments | Returns | Checks | What the AI does |
|---|---|---|---|---|
| `AI.Attack(player, zone)` | AI slot, zone | nil | player's role is "ai"; zone known | Every wave goes to the zone's centre cell until cleared (staging first, then attack-move to the centre). Unlike an LLM attack focus it does not lapse when the army arrives. |
| `AI.Defend(player, zone)` | AI slot, zone | nil | as above | The rally / defend point becomes the zone centre (if passable) and no new wave launches: the army stays to defend. Waves already out finish. If `Attack` is also set, `Attack` wins the wave hold (waves go to the attack zone); the rally point still moves. |
| `AI.SetTier(player, tier)` | AI slot, tier 1..5 | nil | as above | At its next decision the AI (and the driver's decision interval / command delay) use tier n's profile from data/ai/difficulty.toml. Carries over unchanged: all state (build progress, drawn wave trigger, roster, timers, squads, scout route, RNG). Follows the new profile: capability flags (repair, upgrades, scatter, harass, scouting, ...), retreat percent, wave-gate options, the attack threshold used for the NEXT wave trigger. Fixed at AI creation from its own profile: build-order list and supply margin, faction tactics. |
| `AI.Wave(player, waveId, zone)` | AI slot, id, zone | nil | as above; bumps `WaveSeq` | When `WaveSeq` rises above the last one the AI served, it launches ONE wave with the whole army it has (min 1 unit), ignoring its strength gate, thresholds, timers, retreat cooldown, home guard, `Defend` hold and directive guard, and sends it to the zone centre; then resumes normal rules. The wave id is informational (waves use the army that exists, no composition). A request made while a wave is out waits until it ends; with no army the request stays pending. |
| `AI.Clear(player)` | AI slot | nil | as above; clears attack, defend, tier, wave id (keeps `WaveSeq`) | Back to normal: own goals, own rally, own profile. A pending scripted wave still launches. |

D-051: these never run AI code inside `Step`. They write `AiHookState` (hashed mission state). The game layer adapts it (`Rts.Game.Sandbox.MissionAiHooks`,
implementing `Rts.AI.IMissionAiHooks`, zones resolved to centre cells) and `AiDriver` reads it once per tick per slot, outside Step, and hands it to
`RecruitAi` (`IHookableAi`) before each decision. Only the Recruit-framework AI ("recruit"/"tiered") reacts; other AIs ignore hooks.
**Priority vs LLM directives:** a hook beats a directive where both set the same thing (Attack over the directive's attack cell, Defend over its rally
cell); all other directive knobs still apply. **Save:** the AI's last served `WaveSeq` and a pending/active scripted wave are in the AI snapshot
(state version 2, written only once a hook was served; games without hooks save byte-identically). Attack/Defend/Tier are re-read from the mission state
on the next decision, so a load mid-hook continues identically.

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
