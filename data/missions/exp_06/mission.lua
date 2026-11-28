-- Expedition 06: Dredge Rights (schema 2, S48 C-12 wave 1, design 22 section 3.4). Voice on the radio: Commissioner Strand (leader_exp_strand); first lines of Dr. Roque.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Flow: deploy the Command Ship -> depot stands -> raise a Dredge Works (Roque sends the Hover Yard drawings and two prototype Glide Tanks) and put two Skimmers on the ash -> break the Authority coast battery
-- while two columns come down from the inland road. Player.KeepAlive keeps the Authority team in the game, so only this script ends the mission. Pressure scales by unit count only (D-061).

local SPEAKER = "leader_exp_strand"
local ROQUE = "leader_exp_roque"
local SEA_EDGE = { x = 18, y = 63 }
local SHORE = { x = 18, y = 47 }
local HOLD = { x = 22, y = 42 }

function OnInit()
  state.phase = "sea"
  state.works = false
  state.dredged = false
  state.waveTags = {}
  state.waves = 0

  Objective.Add("landing", "e6.obj.landing", true)
  Objective.Add("dredge", "e6.obj.dredge", true)
  Objective.Add("battery", "e6.obj.battery", true)
  Objective.AddHidden("wreck", "e6.obj.wreck", false)
  -- Objective targets (M11-05): where the HUD points.
  Objective.SetTarget("landing", "beach_landing")
  Objective.SetTarget("dredge", "dredge_shallows")
  Objective.SetTarget("battery", "battery")
  Objective.SetTarget("wreck", "relief_wreck")
  Media.MessageKey("e6.start")
  Dialogue.Say(SPEAKER, SPEAKER, "e6.radio.start")
  Camera.Focus("flagship", 120)

  -- the battery stays home; the columns come from the script (unit count by tier, D-061)
  AI.SetTier(1, Mission.Difficulty())
  AI.Defend(1, "battery")
  Player.KeepAlive(1, true)
  BatteryExtras()
  Trigger.OnEnter("relief_wreck", 0, WreckReached)
  Trigger.Every(20, Poll)
end

-- Tier 4 and 5 add riflemen to the battery itself. Unit count only.
function BatteryExtras()
  local n = Pick({ 0, 0, 0, 2, 4 })
  for i = 1, n do
    Actor.Create("auth_rifleman", 1, { x = 34 + i * 2, y = 43 })
  end
end

function Pick(sizes)
  return sizes[Mission.Difficulty()]
end

-- One poll covers every lose check, the objective checks and the win check.
function Poll()
  if state.phase == "done" then return end
  local depots = Actor.Count(0, "exp_landing_depot")
  if state.phase == "sea" then
    if depots >= 1 then
      Landed()
    elseif Actor.Count(0, "exp_command_ship") == 0 then
      Fail("e6.lost.ship")
    end
    return
  end
  if depots == 0 then
    Fail("e6.lost.depot")
    return
  end
  CheckDredge()
  CheckBattery()
  CheckWin()
end

function Fail(key)
  state.phase = "done"
  Media.MessageKey(key)
  Mission.Lose(0)
end

function Landed()
  state.phase = "ashore"
  Objective.Complete("landing")
  Media.MessageKey("e6.landed")
  Dialogue.Say(SPEAKER, SPEAKER, "e6.radio.landed")
  Camera.Focus("beach_landing", 120)
  Shipment(4)
  Mission.Timer("ship2", 1500, ShipTwo, "e6.timer.ship2")
  Mission.Timer("col1", Pick({ 3400, 3300, 3000, 2800, 2600 }), WaveOne, "e6.timer.col1")
  Trigger.AfterDelay(1800, Shells)
  Trigger.AfterDelay(Pick({ 6000, 5800, 5400, 5000, 4600 }), WaveTwo)
end

-- Shipments: a landing craft with marines aboard, sailing in from the south edge to the beach.
function Shipment(marines)
  local types = { "exp_landing_craft" }
  for i = 1, marines do types[#types + 1] = "exp_marine" end
  Reinforce.Naval(0, types, SEA_EDGE, SHORE)
end

function ShipTwo()
  if state.phase ~= "ashore" then return end
  Media.MessageKey("e6.ship2")
  Dialogue.Say(SPEAKER, SPEAKER, "e6.radio.ship2")
  Shipment(6)
end

function Shells()
  if state.phase ~= "ashore" then return end
  Media.MessageKey("e6.shells")
  Dialogue.Say(SPEAKER, SPEAKER, "e6.radio.shells")
  Camera.Focus("battery", 100)
end

-- The Dredge Works makes Roque send the Hover Yard drawings (a reward of this mission, granted here) and two prototype Glide Tanks by sea (the
-- Glide Tank needs the Plotting Room, so it is not buildable yet); the dredge pays its way once two Skimmers are on the ash (the second one
-- comes out of the new Hover Yard), and Roque sends two more prototypes.
function CheckDredge()
  if state.dredged then return end
  if not state.works then
    if Actor.Count(0, "exp_dredge_works") >= 1 then
      state.works = true
      Tech.GrantMany(0, { "exp_hover_yard" })
      Prototypes(2)
      Objective.Reveal("wreck")
      Media.MessageKey("e6.works")
      Dialogue.Say(SPEAKER, SPEAKER, "e6.radio.works")
      Dialogue.Say(ROQUE, ROQUE, "e6.radio.roque")
    end
    return
  end
  if Actor.Count(0, "exp_skimmer") >= 2 then
    state.dredged = true
    Objective.Complete("dredge")
    Media.MessageKey("e6.dredge")
    Dialogue.Say(SPEAKER, SPEAKER, "e6.radio.dredge")
    Prototypes(2)
  end
end

-- Roque's prototype Glide Tanks: hovercraft, they sail in from the south edge on their own cushions.
function Prototypes(n)
  local types = {}
  for i = 1, n do types[i] = "exp_glide_tank" end
  Reinforce.Edge(0, types, SEA_EDGE, SHORE)
end

-- The battery is broken when nothing of the Authority stands inside its ground.
function CheckBattery()
  if Objective.Status("battery") ~= "active" then return end
  if Actor.CountInZone("battery", 1) == 0 then
    Objective.Complete("battery")
    Media.MessageKey("e6.battery")
    Dialogue.Say(SPEAKER, SPEAKER, "e6.radio.battery")
    Camera.Focus("battery", 120)
  end
end

function WreckReached()
  if state.phase == "done" then return end
  if Objective.Status("wreck") == "active" then
    Objective.Reveal("wreck")
    Objective.Complete("wreck")
    Media.MessageKey("e6.wreck")
    Dialogue.Say(ROQUE, ROQUE, "e6.radio.wreck")
  end
end

function CheckWin()
  if not Mission.AllPrimaryDone() then return end
  state.phase = "done"
  Media.MessageKey("e6.win")
  Dialogue.Say(SPEAKER, SPEAKER, "e6.radio.win")
  Mission.Win(0)
end

-- Authority columns come down from the inland road to the beach.
function Wave(size)
  local types = {}
  for i = 1, size do types[i] = "auth_rifleman" end
  if Mission.Difficulty() >= 3 then types[#types + 1] = "auth_jeep" end
  local tags = Reinforce.Zone(1, types, "home_inland", HOLD)
  for i, tag in pairs(tags) do
    state.waveTags[#state.waveTags + 1] = tag
  end
  state.waves = state.waves + 1
end

function WaveOne()
  if state.phase == "done" then return end
  Media.MessageKey("e6.wave1")
  Dialogue.Say("leader_auth_wrenfield", "leader_auth_wrenfield", "e6.radio.wrenfield")
  Camera.Focus("home_inland", 120)
  Wave(Pick({ 3, 3, 4, 5, 6 }))
end

function WaveTwo()
  if state.phase == "done" then return end
  Media.MessageKey("e6.wave2")
  Dialogue.Say(SPEAKER, SPEAKER, "e6.radio.wave2")
  Wave(Pick({ 4, 4, 5, 7, 9 }))
end
