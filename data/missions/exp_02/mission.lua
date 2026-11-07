-- Expedition 02: The Supply Line (schema 2, M8-12). Voice on the radio: Commissioner Strand (leader_exp_strand); cameos Dr. Roque and Brigadier Wrenfield.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Flow: raise the Anchorage on the north shore -> two convoys sail in -> hold the landing against three Authority columns -> break the garrison on the coast road.
-- The Authority reserve (a relay post in the far corner) keeps the enemy team alive so only this script ends the mission. Enemy pressure scales by unit count only (D-061).

local SPEAKER = "leader_exp_strand"
local SEA_EDGE = { x = 13, y = 0 }
local SHORE = { x = 13, y = 9 }
local HOLD = { x = 18, y = 14 }

function OnInit()
  state.phase = "dock"
  state.docked = false
  state.waves = 0
  state.waveTags = {}
  state.convoys = 0

  Objective.Add("dock", "e2.obj.dock", true)
  Objective.Add("hold", "e2.obj.hold", true)
  Objective.Add("post", "e2.obj.post", true)
  Objective.AddHidden("line", "e2.obj.line", false)
  Media.MessageKey("e2.start")
  Dialogue.Say(SPEAKER, SPEAKER, "e2.radio.start")
  Camera.Focus("shore", 120)

  -- The garrison stays home; the response comes from the script (unit count by tier, D-061).
  AI.Defend(1, "garrison")
  GarrisonExtras()

  Mission.Timer("col1", 1200, WaveOne, "e2.timer.col1")
  Trigger.AfterDelay(3400, WaveTwo)
  Trigger.AfterDelay(5600, WaveThree)
  Trigger.Every(20, Poll)
end

-- Tier 4 and 5 add riflemen to the garrison itself. Unit count only.
function GarrisonExtras()
  local extra = { 0, 0, 0, 2, 4 }
  local n = extra[Mission.Difficulty()]
  for i = 1, n do
    Actor.Create("auth_rifleman", 1, { x = 36 + i * 2, y = 30 })
  end
end

-- One poll covers every lose check, the objective checks and the win check.
function Poll()
  if state.phase == "done" then return end
  if Actor.Count(0, "exp_landing_depot") == 0 then
    Fail("e2.lost.depot")
    return
  end
  CheckDock()
  CheckHeld()
  CheckPost()
  CheckWin()
end

function Fail(key)
  state.phase = "done"
  Objective.Fail("hold")
  Media.MessageKey(key)
  Mission.Lose(0)
end

function CheckDock()
  local docks = Actor.Count(0, "exp_anchorage")
  if not state.docked then
    if docks >= 1 then
      state.docked = true
      Objective.Complete("dock")
      Objective.Reveal("line")
      Media.MessageKey("e2.docked")
      Dialogue.Say(SPEAKER, SPEAKER, "e2.radio.docked")
      Dialogue.Say("leader_exp_roque", "leader_exp_roque", "e2.radio.roque")
      Convoy(6)
      Mission.Timer("convoy2", 1500, ConvoyTwo, "e2.timer.convoy2")
    end
  elseif docks == 0 and Objective.Status("line") == "active" then
    Objective.Fail("line")
    Media.MessageKey("e2.line.lost")
  end
end

-- Convoys: a landing craft with marines aboard, sailing in from the north edge to the shore.
function Convoy(marines)
  local types = { "exp_landing_craft" }
  for i = 1, marines do types[#types + 1] = "exp_marine" end
  Reinforce.Naval(0, types, SEA_EDGE, SHORE)
  state.convoys = state.convoys + 1
  Media.MessageKey("e2.convoy")
end

function ConvoyTwo()
  if state.phase == "done" then return end
  Dialogue.Say(SPEAKER, SPEAKER, "e2.radio.convoy2")
  Convoy(8)
end

-- Wave strength follows the difficulty tier (1..5); tier 3 is the baseline. Never cash or vision (D-061).
function WaveOneSize()
  local sizes = { 3, 3, 4, 5, 6 }
  return sizes[Mission.Difficulty()]
end

function WaveTwoSize()
  local sizes = { 3, 4, 5, 6, 8 }
  return sizes[Mission.Difficulty()]
end

function WaveThreeSize()
  local sizes = { 4, 5, 6, 8, 10 }
  return sizes[Mission.Difficulty()]
end

function Wave(n, zone, size)
  local types = {}
  for i = 1, size do types[i] = "auth_rifleman" end
  if Mission.Difficulty() >= 3 then types[#types + 1] = "auth_jeep" end
  local tags = Reinforce.Zone(1, types, zone, HOLD)
  for i, tag in pairs(tags) do
    state.waveTags[#state.waveTags + 1] = tag
  end
  state.waves = n
end

function WaveOne()
  if state.phase == "done" then return end
  Media.MessageKey("e2.wave1")
  Dialogue.Say("leader_auth_wrenfield", "leader_auth_wrenfield", "e2.radio.wrenfield")
  Camera.Focus("coast_road", 120)
  Wave(1, "coast_road", WaveOneSize())
end

function WaveTwo()
  if state.phase == "done" then return end
  Media.MessageKey("e2.wave2")
  Camera.Focus("south_track", 100)
  Wave(2, "south_track", WaveTwoSize())
end

function WaveThree()
  if state.phase == "done" then return end
  Media.MessageKey("e2.wave3")
  Dialogue.Say(SPEAKER, SPEAKER, "e2.radio.wave3")
  Wave(3, "coast_road", WaveThreeSize())
end

function WavesDown()
  for i, tag in pairs(state.waveTags) do
    if Actor.Alive(tag) then return false end
  end
  return true
end

function CheckHeld()
  if Objective.Status("hold") ~= "active" then return end
  if state.waves < 3 or not WavesDown() then return end
  Objective.Complete("hold")
  Media.MessageKey("e2.held")
end

-- The post is broken when nothing of the garrison stands inside its ground.
function CheckPost()
  if Objective.Status("post") ~= "active" then return end
  if Actor.CountInZone("garrison", 1) == 0 then
    Objective.Complete("post")
    Media.MessageKey("e2.post.down")
    Camera.Focus("garrison", 120)
  end
end

function CheckWin()
  if not Mission.AllPrimaryDone() then return end
  state.phase = "done"
  if Objective.Status("line") == "active" then Objective.Complete("line") end
  Media.MessageKey("e2.win")
  Dialogue.Say(SPEAKER, SPEAKER, "e2.radio.win")
  Mission.Win(0)
end
