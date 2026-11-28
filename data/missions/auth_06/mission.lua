-- Authority 06: The Mill Town (schema 2, S48 C-12, design 22 section 3.1). Hero on the radio: Brigadier Wrenfield; Pell by teletype.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Shape: build the first full base in the south-west corner, put scrapers on the pitchblende spur, break the shift's four hatches and
-- two reactor vents under the mill town before the visible grain-train clock runs out. Hidden bonus: the grain elevator stays standing.
-- Pressure is unit counts and the AI tier only (D-061): guards at the hatches, two raids on the yard, a sortie when the shift starts to fall.

local SPEAKER = "leader_auth_wrenfield"
local PELL = "leader_auth_pell"

function OnInit()
  state.over = false
  state.hatches = 4
  state.vents = 2
  state.spur = false
  state.tank = false
  state.hall = false
  state.sortie = false
  state.elevatorSeen = false

  Objective.Add("hatches", "a6.obj.hatches", true)
  Objective.Add("vents", "a6.obj.vents", true)
  Objective.Add("yard", "a6.obj.yard", true)
  Objective.Add("spur", "a6.obj.spur", false)
  Objective.AddHidden("elevator", "a6.obj.elevator", false)
  Objective.SetTarget("hatches", "hatch_a")
  Objective.SetTarget("vents", "vent_w")
  Objective.SetTarget("yard", "yard")
  Objective.SetTarget("spur", "pitch_spur")
  Media.MessageKey("a6.start")
  Dialogue.Say(PELL, PELL, "a6.radio.pell")
  Dialogue.Say(SPEAKER, SPEAKER, "a6.radio.start")
  Camera.Focus("yard", 120)

  -- the Directorate's own army stays in the town and defends the shift (AI tier follows the mission tier)
  AI.SetTier(1, Mission.Difficulty())
  AI.Defend(1, "mill_town")
  Guards()

  Mission.Timer("train", 15000, TrainTurnsBack, "a6.timer.train")
  Trigger.AfterDelay(Pick({ 4200, 4200, 3600, 3200, 3000 }), RaidWest)
  Trigger.AfterDelay(Pick({ 9000, 8400, 7600, 6800, 6000 }), RaidSouth)
  Trigger.Every(40, CheckBuild)
  Trigger.OnSeen("elevator", 0, ElevatorSeen)
  Trigger.OnDestroyed("elevator", ElevatorLost)
  Trigger.OnDestroyed("yard", YardLost)
  Trigger.OnDestroyed("hatch_a", HatchDown)
  Trigger.OnDestroyed("hatch_b", HatchDown)
  Trigger.OnDestroyed("hatch_c", HatchDown)
  Trigger.OnDestroyed("hatch_d", HatchDown)
  Trigger.OnDestroyed("vent_w", VentDown)
  Trigger.OnDestroyed("vent_e", VentDown)
end

-- Strength follows the difficulty tier (1..5); tier 3 is the design strength. Unit counts only (D-061).
function Pick(sizes)
  return sizes[Mission.Difficulty()]
end

function Squad(conscripts, ashthrowers, sappers)
  local types = {}
  for i = 1, conscripts do types[#types + 1] = "dir_conscript" end
  for i = 1, ashthrowers do types[#types + 1] = "dir_ashthrower" end
  for i = 1, sappers do types[#types + 1] = "dir_sapper" end
  return types
end

-- the shift's guards: a squad on each hatch and vent, and pillboxes on the avenues (none at tier 1)
function Guards()
  for i, tag in ipairs({ "hatch_a", "hatch_b", "hatch_c", "hatch_d" }) do
    Actor.CreateGroup(Squad(Pick({ 1, 2, 2, 3, 4 }), Pick({ 0, 0, 1, 1, 2 }), 0), 1, Actor.Cell(tag))
  end
  for i, tag in ipairs({ "vent_w", "vent_e" }) do
    Actor.CreateGroup(Squad(Pick({ 2, 3, 3, 4, 6 }), Pick({ 0, 1, 1, 2, 3 }), 0), 1, Actor.Cell(tag))
  end
  local spots = { { x = 26, y = 21 }, { x = 40, y = 21 }, { x = 43, y = 22 } }
  for i = 1, Pick({ 0, 1, 2, 2, 3 }) do
    Actor.Create("dir_pillbox", 1, spots[i])
  end
end

function RaidWest()
  if state.over then return end
  Media.MessageKey("a6.raid1")
  Dialogue.Say(SPEAKER, SPEAKER, "a6.radio.raid1")
  Reinforce.Zone(1, Squad(Pick({ 3, 4, 5, 7, 9 }), Pick({ 0, 0, 1, 2, 3 }), 0), "spawn_w", Map.Center("home_sw"))
end

function RaidSouth()
  if state.over then return end
  Media.MessageKey("a6.raid2")
  Dialogue.Say(SPEAKER, SPEAKER, "a6.radio.raid2")
  Reinforce.Zone(1, Squad(Pick({ 4, 5, 7, 9, 12 }), Pick({ 0, 1, 2, 2, 4 }), Pick({ 0, 0, 1, 1, 2 })), "spawn_s", Map.Center("home_sw"))
  -- the army that was defending the town goes for the yard once, then goes home to defend again
  AI.Wave(1, "shift", "home_sw")
end

-- when half the shift is gone the Directorate sends a column down from the north road onto the town
function Sortie()
  if state.over or state.sortie then return end
  state.sortie = true
  Media.MessageKey("a6.sortie")
  Dialogue.Say(SPEAKER, SPEAKER, "a6.radio.sortie")
  Reinforce.Zone(1, Squad(Pick({ 3, 4, 6, 8, 10 }), Pick({ 0, 1, 1, 2, 3 }), Pick({ 0, 0, 0, 1, 2 })), "spawn_n", Map.Center("mill_town"))
end

-- the build beats: first tank, ration hall, two scrapers on the spur
function CheckBuild()
  if state.over then return end
  if not state.tank and Actor.Count(0, "auth_tank") >= 1 then
    state.tank = true
    Media.MessageKey("a6.tank")
    Dialogue.Say(SPEAKER, SPEAKER, "a6.radio.tank")
  end
  if not state.hall and Actor.Count(0, "auth_ration_hall") >= 1 then
    state.hall = true
    Media.MessageKey("a6.hall")
    Dialogue.Say(SPEAKER, SPEAKER, "a6.radio.hall")
  end
  if not state.spur and Actor.CountInZone("pitch_spur", 0, "auth_scraper") >= 2 then
    state.spur = true
    Objective.Complete("spur")
    Media.MessageKey("a6.spur")
    Dialogue.Say(PELL, PELL, "a6.radio.spur")
  end
end

function ElevatorSeen()
  if state.over or state.elevatorSeen then return end
  state.elevatorSeen = true
  Objective.Reveal("elevator")
  Objective.SetTarget("elevator", "elevator")
  Media.MessageKey("a6.elevator")
  Dialogue.Say(PELL, PELL, "a6.radio.elevator")
end

function ElevatorLost()
  if state.over then return end
  if Objective.Status("elevator") == "active" then
    Objective.Reveal("elevator")
    Objective.Fail("elevator")
  end
  Media.MessageKey("a6.elevator.lost")
end

-- the HUD marker follows the first hatch or vent still standing
function Retarget()
  for i, tag in ipairs({ "hatch_a", "hatch_b", "hatch_c", "hatch_d" }) do
    if Actor.Alive(tag) then Objective.SetTarget("hatches", tag) break end
  end
  for i, tag in ipairs({ "vent_w", "vent_e" }) do
    if Actor.Alive(tag) then Objective.SetTarget("vents", tag) break end
  end
end

function HatchDown()
  if state.over then return end
  state.hatches = state.hatches - 1
  Media.MessageKey("a6.hatch")
  if state.hatches == 2 then
    Dialogue.Say(SPEAKER, SPEAKER, "a6.radio.half")
    Trigger.AfterDelay(Pick({ 600, 600, 500, 400, 300 }), Sortie)
  end
  if state.hatches <= 0 then Objective.Complete("hatches") end
  Retarget()
  CheckWin()
end

function VentDown()
  if state.over then return end
  state.vents = state.vents - 1
  Media.MessageKey("a6.vent")
  if state.vents <= 0 then Objective.Complete("vents") end
  Retarget()
  CheckWin()
end

function CheckWin()
  if state.hatches > 0 or state.vents > 0 then return end
  state.over = true
  Mission.TimerCancel("train")
  Objective.Complete("yard")
  if Objective.Status("elevator") == "active" then
    Objective.Reveal("elevator")
    Objective.Complete("elevator")
    Dialogue.Say(PELL, PELL, "a6.radio.grain")
  end
  Media.MessageKey("a6.win")
  Dialogue.Say(SPEAKER, SPEAKER, "a6.radio.win")
  Camera.Focus("mill_town", 120)
  Mission.Win(0)
end

function YardLost()
  if state.over then return end
  state.over = true
  Objective.Fail("yard")
  Media.MessageKey("a6.lost.yard")
  Mission.Lose(0)
end

function TrainTurnsBack()
  if state.over then return end
  state.over = true
  Media.MessageKey("a6.lost.train")
  Mission.Lose(0)
end
