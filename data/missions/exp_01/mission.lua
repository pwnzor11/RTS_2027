-- Expedition 01: First Landing (schema 2, M8-10). Voice on the radio: Commissioner Strand (leader_exp_strand).
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Flow: deploy the Command Ship -> depot stands -> shipments arrive by sea over time -> raise the beachhead -> hold against three Authority waves.
-- Enemy pressure scales by unit count only (D-061). Phases: sea, ashore, done.

local SPEAKER = "leader_exp_strand"
local SEA_EDGE = { x = 95, y = 82 }
local SHORE = { x = 85, y = 84 }
local HOLD = { x = 80, y = 82 }

function OnInit()
  state.phase = "sea"
  state.built = false
  state.waves = 0
  state.waveTags = {}
  state.craft = {}
  state.ship2 = false

  Objective.Add("landing", "e1.obj.landing", true)
  Objective.Add("build", "e1.obj.build", true)
  Objective.Add("hold", "e1.obj.hold", true)
  Objective.AddHidden("craft", "e1.obj.craft", false)
  Media.MessageKey("e1.start")
  Dialogue.Say(SPEAKER, SPEAKER, "e1.radio.start")
  Camera.Focus("flagship", 120)

  Trigger.Every(20, Poll)
end

-- One poll covers every lose check and the win check.
function Poll()
  if state.phase == "done" then return end
  local depots = Actor.Count(0, "exp_landing_depot")
  if state.phase == "sea" then
    if depots >= 1 then
      Landed()
    elseif Actor.Count(0, "exp_command_ship") == 0 then
      Media.MessageKey("e1.lost.ship")
      Mission.Lose(0)
    end
    return
  end
  if depots == 0 then
    state.phase = "done"
    Objective.Fail("hold")
    Media.MessageKey("e1.lost.depot")
    Mission.Lose(0)
    return
  end
  CheckBuilt()
  CheckHeld()
end

function Landed()
  state.phase = "ashore"
  Objective.Complete("landing")
  Media.MessageKey("e1.landed")
  Dialogue.Say(SPEAKER, SPEAKER, "e1.radio.landed")
  Camera.Focus("beach", 120)
  Objective.Reveal("craft")
  ShipOne()
  Mission.Timer("ship2", ShipTwoTicks(), ShipTwo, "e1.timer.ship2")
  Trigger.AfterDelay(Pick({ 1300, 1250, 1200, 1100, 1000 }), WaveOne)
  Trigger.AfterDelay(Pick({ 3100, 3050, 3000, 2800, 2600 }), WaveTwo)
  Trigger.AfterDelay(Pick({ 4900, 4850, 4800, 4500, 4200 }), WaveThree)
end

-- Shipments. Each is a landing craft with marines aboard, sailing in from the east edge and landing on the beach.
function Shipment(marines)
  local types = { "exp_landing_craft" }
  for i = 1, marines do types[#types + 1] = "exp_marine" end
  local tags = Reinforce.Naval(0, types, SEA_EDGE, SHORE)
  local craftTag = tags[1]
  state.craft[#state.craft + 1] = craftTag
  Trigger.OnDestroyed(craftTag, CraftLost)
end

function ShipOne()
  Media.MessageKey("e1.ship1")
  Shipment(4)
end

function ShipTwoTicks()
  return 1500
end

function ShipTwo()
  if state.phase ~= "ashore" then return end
  state.ship2 = true
  Media.MessageKey("e1.ship2")
  Dialogue.Say(SPEAKER, SPEAKER, "e1.radio.ship2")
  Shipment(6)
end

function CraftLost()
  if state.phase == "done" then return end
  if Objective.Status("craft") == "active" then
    Objective.Fail("craft")
    Media.MessageKey("e1.craft.lost")
    Dialogue.Say(SPEAKER, SPEAKER, "e1.radio.craft_lost")
  end
end

function CheckBuilt()
  if state.built then return end
  if Actor.Count(0, "exp_pile_house") >= 1 and Actor.Count(0, "exp_billet") >= 1 and Actor.Count(0, "exp_pillbox") >= 1 then
    state.built = true
    Objective.Complete("build")
    Media.MessageKey("e1.built")
    Dialogue.Say(SPEAKER, SPEAKER, "e1.radio.built")
  end
end

-- Wave strength follows the difficulty tier (1..5); tier 3 is the baseline. Never cash or vision (D-061).
function WaveOneSize()
  local sizes = { 3, 3, 4, 4, 5 }
  return sizes[Mission.Difficulty()]
end

function WaveTwoSize()
  local sizes = { 4, 4, 5, 5, 7 }
  return sizes[Mission.Difficulty()]
end

function WaveThreeSize()
  local sizes = { 5, 5, 6, 7, 8 }
  return sizes[Mission.Difficulty()]
end

function Pick(sizes)
  return sizes[Mission.Difficulty()]
end

function Wave(n, zone, size, extra)
  local types = {}
  local d = Mission.Difficulty()
  for i = 1, size do types[i] = "auth_rifleman" end
  -- a jeep at every tier now (it was tier 3 and up), and a tank closes the last wave at tier 5
  types[#types + 1] = extra
  if n == 3 and d >= 5 then types[#types + 1] = "auth_tank" end
  local tags = Reinforce.Zone(1, types, zone, HOLD)
  for i, tag in pairs(tags) do
    state.waveTags[#state.waveTags + 1] = tag
  end
  state.waves = n
end

function WaveOne()
  if state.phase ~= "ashore" then return end
  Media.MessageKey("e1.wave1")
  Dialogue.Say(SPEAKER, SPEAKER, "e1.radio.wave1")
  Camera.Focus("roads", 120)
  Wave(1, "roads", WaveOneSize(), "auth_jeep")
end

function WaveTwo()
  if state.phase ~= "ashore" then return end
  Media.MessageKey("e1.wave2")
  Camera.Focus("west_track", 100)
  Wave(2, "west_track", WaveTwoSize(), "auth_jeep")
end

function WaveThree()
  if state.phase ~= "ashore" then return end
  Media.MessageKey("e1.wave3")
  Dialogue.Say(SPEAKER, SPEAKER, "e1.radio.wave3")
  Wave(3, "roads", WaveThreeSize(), "auth_jeep")
end

function WavesDown()
  for i, tag in pairs(state.waveTags) do
    if Actor.Alive(tag) then return false end
  end
  return true
end

function CheckHeld()
  if state.waves < 3 or not state.built or not WavesDown() then return end
  state.phase = "done"
  Objective.Complete("hold")
  if Objective.Status("craft") == "active" then Objective.Complete("craft") end
  Media.MessageKey("e1.win")
  Dialogue.Say(SPEAKER, SPEAKER, "e1.radio.win")
  Mission.Win(0)
end
