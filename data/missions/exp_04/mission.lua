-- Expedition 04: The Fuel (schema 2, M8-12, the finale). Voice on the radio: Commissioner Strand (leader_exp_strand); cameos Fleet Admiral Aldersey, Dr. Roque and the Directorate's Voice (Larkin).
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Flow: a Dredge Works on the ash field (the fuel) -> three Directorate columns march out of the redoubt gate (and the built-in AI is sent at the beachhead) ->
-- break the redoubt: its Shaft and its Sunrise Device. The reserve bunker in the far corner keeps the Directorate alive so only this script ends the mission.
-- Enemy pressure scales by unit count only (D-061) plus the AI tier. Phases: ashore, done.

local SPEAKER = "leader_exp_strand"
local SEA_EDGE = { x = 48, y = 95 }
local SHORE = { x = 45, y = 88 }
local HOLD = { x = 46, y = 76 }

function OnInit()
  state.phase = "ashore"
  state.fuel = false
  state.waves = 0
  state.finale = false
  state.waveTags = {}

  Objective.Add("fuel", "e4.obj.fuel", true)
  Objective.Add("hold", "e4.obj.hold", true)
  Objective.Add("base", "e4.obj.base", true)
  Objective.AddHidden("ash", "e4.obj.ash", false)
  -- Objective targets (M11-05): where the HUD points.
  Objective.SetTarget("fuel", "ash_field")
  Objective.SetTarget("hold", "beachhead")
  Objective.SetTarget("base", "shaft")
  Media.MessageKey("e4.start")
  Dialogue.Say(SPEAKER, SPEAKER, "e4.radio.start")
  Camera.Focus("ash_field", 120)

  -- The redoubt's own AI plays at the chosen tier and stays home until the fuel is taken, then marches on the beachhead.
  AI.SetTier(1, Mission.Difficulty())
  AI.Defend(1, "redoubt")

  -- The redoubt's Device is a story prop here: the script has no Sunrise moment, so keep its real charge from ever completing (S19 MIS-10).
  Actor.DelayCharge("device", 1000000000)
  Trigger.AfterDelay(500, ShipOne)
  Mission.Timer("col1", Pick({ 2600, 2500, 2400, 2200, 2000 }), ColumnOne, "e4.timer.col1")
  Trigger.AfterDelay(Pick({ 5400, 5300, 5200, 4700, 4200 }), ColumnTwo)
  Trigger.AfterDelay(Pick({ 8800, 8600, 8400, 7600, 6800 }), ColumnThree)
  Trigger.AfterDelay(9000, ShipTwo)
  -- Callbacks run before the sim's own defeat check, so the last blow on the redoubt is seen on the tick it lands.
  Trigger.OnDestroyed("shaft", BaseBlow)
  Trigger.OnDestroyed("device", BaseBlow)
  Trigger.Every(20, Poll)
end

function BaseBlow()
  if state.phase == "done" then return end
  if not Actor.Alive("shaft") and Actor.Alive("device") then Objective.SetTarget("base", "device") end
  Finale()
  CheckHeld()
  CheckBase()
  CheckWin()
end

-- One poll covers every lose check, the objective checks and the win check.
function Poll()
  if state.phase == "done" then return end
  if Actor.Count(0, "exp_landing_depot") == 0 then
    state.phase = "done"
    Objective.Fail("hold")
    Media.MessageKey("e4.lost.depot")
    Mission.Lose(0)
    return
  end
  CheckFuel()
  CheckHeld()
  CheckBase()
  CheckWin()
end

-- Final shipments: a landing craft with marines aboard, sailing in from the south edge.
function Shipment(marines)
  local types = { "exp_landing_craft" }
  for i = 1, marines do types[#types + 1] = "exp_marine" end
  Reinforce.Naval(0, types, SEA_EDGE, SHORE)
end

function ShipOne()
  Media.MessageKey("e4.ship")
  Shipment(8)
end

function ShipTwo()
  if state.phase == "done" then return end
  Media.MessageKey("e4.ship")
  Dialogue.Say("leader_exp_aldersey", "leader_exp_aldersey", "e4.radio.aldersey")
  Shipment(8)
end

-- The fuel: a Dredge Works standing on the ash field.
function CheckFuel()
  local works = Actor.CountInZone("ash_field", 0, "exp_dredge_works")
  if not state.fuel then
    if works >= 1 then
      state.fuel = true
      Objective.Complete("fuel")
      Objective.Reveal("ash")
      Media.MessageKey("e4.fuel")
      Dialogue.Say(SPEAKER, SPEAKER, "e4.radio.fuel")
      Dialogue.Say("leader_exp_roque", "leader_exp_roque", "e4.radio.roque")
      Dialogue.Say(SPEAKER, SPEAKER, "e4.radio.march")
      -- The fuel is taken: the built-in AI now sends its whole army at the beachhead.
      AI.Attack(1, "beachhead")
    end
  elseif works == 0 and Objective.Status("ash") == "active" then
    Objective.Fail("ash")
    Media.MessageKey("e4.ash.lost")
  end
end

-- Column strength follows the difficulty tier (1..5); tier 3 is the baseline. Never cash or vision (D-061).
function Pick(sizes)
  return sizes[Mission.Difficulty()]
end

function Column(size, extra, minTier, zone)
  local types = {}
  for i = 1, size do types[i] = "dir_conscript" end
  local d = Mission.Difficulty()
  if d >= minTier then types[#types + 1] = extra end
  -- the heavier tiers send a second and third special unit
  if d >= 4 then types[#types + 1] = extra end
  if d >= 5 then types[#types + 1] = extra end
  local tags = Reinforce.Zone(1, types, zone, HOLD)
  for i, tag in pairs(tags) do
    state.waveTags[#state.waveTags + 1] = tag
  end
  state.waves = state.waves + 1
end

function ColumnOne()
  if state.phase == "done" then return end
  Media.MessageKey("e4.col1")
  Dialogue.Say("leader_dir_larkin", "leader_dir_larkin", "e4.radio.larkin")
  Camera.Focus("gate", 120)
  Column(Pick({ 7, 8, 10, 13, 17 }), "dir_ashthrower", 1, "gate")
end

function ColumnTwo()
  if state.phase == "done" then return end
  Media.MessageKey("e4.col2")
  Dialogue.Say(SPEAKER, SPEAKER, "e4.radio.col2")
  Camera.Focus("flank", 100)
  Column(Pick({ 7, 9, 11, 15, 20 }), "dir_sapper", 2, "flank")
end

function ColumnThree()
  if state.phase == "done" then return end
  Media.MessageKey("e4.col3")
  Dialogue.Say(SPEAKER, SPEAKER, "e4.radio.col3")
  Column(Pick({ 9, 11, 13, 19, 26 }), "dir_ashthrower", 1, "gate")
  if Mission.Difficulty() >= 3 then
    local heavy = {}
    for i = 1, Pick({ 0, 0, 1, 2, 3 }) do heavy[#heavy + 1] = "dir_reactor_crawler" end
    local tags = Reinforce.Zone(1, heavy, "gate", HOLD)
    for i, tag in pairs(tags) do
      state.waveTags[#state.waveTags + 1] = tag
    end
  end
end

function WavesDown()
  for i, tag in pairs(state.waveTags) do
    if Actor.Alive(tag) then return false end
  end
  return true
end

-- The finale: the first blow on the redoubt (Shaft or Device) brings out its last guard. It marches on the player's army
-- from the gate, and the mission is not won until it is down.
function Finale()
  if state.finale then return end
  -- S18 MIS-03 (owner decision): keep the tested balance, the guard marches on the SECOND blow (Shaft and Device both down).
  if Actor.Alive("shaft") or Actor.Alive("device") then return end
  state.finale = true
  Media.MessageKey("e4.finale")
  Dialogue.Say(SPEAKER, SPEAKER, "e4.radio.finale")
  local types = {}
  for i = 1, Pick({ 10, 12, 14, 22, 32 }) do types[#types + 1] = "dir_conscript" end
  for i = 1, Pick({ 3, 4, 5, 9, 14 }) do types[#types + 1] = "dir_ashthrower" end
  for i = 1, Pick({ 2, 2, 3, 6, 10 }) do types[#types + 1] = "dir_sapper" end
  for i = 1, Pick({ 1, 1, 2, 4, 6 }) do types[#types + 1] = "dir_rocket_truck" end
  for i = 1, Pick({ 1, 1, 2, 3, 4 }) do types[#types + 1] = "dir_reactor_crawler" end
  -- The guard marches out in groups of 12, a second apart, so the gate has room to hold each group.
  state.finaleTypes = types
  state.finaleAt = 1
  FinaleGroup()
  Camera.Focus("gate", 120)
end

function FinaleGroup()
  local types = state.finaleTypes
  local last = state.finaleAt + 11
  if last > #types then last = #types end
  local group = {}
  for i = state.finaleAt, last do group[#group + 1] = types[i] end
  local tags = Reinforce.Zone(1, group, "gate", Map.Center("redoubt"))
  for i, tag in pairs(tags) do
    state.waveTags[#state.waveTags + 1] = tag
  end
  state.finaleAt = last + 1
  if state.finaleAt <= #types then Trigger.AfterDelay(20, FinaleGroup) end
end

function CheckHeld()
  if Objective.Status("hold") ~= "active" then return end
  if state.waves < 3 or not WavesDown() then return end
  Objective.Complete("hold")
  Media.MessageKey("e4.held")
end

-- The redoubt is broken when its Shaft and its Sunrise Device are both gone.
function CheckBase()
  if Objective.Status("base") ~= "active" then return end
  if not Actor.Alive("shaft") and not Actor.Alive("device") then
    Objective.Complete("base")
    Media.MessageKey("e4.base.down")
    Camera.Focus("redoubt", 160)
  end
end

function CheckWin()
  if not Mission.AllPrimaryDone() then return end
  if not state.finale or state.finaleAt <= #state.finaleTypes or not WavesDown() then return end
  state.phase = "done"
  if Objective.Status("ash") == "active" then Objective.Complete("ash") end
  AI.Clear(1)
  Media.MessageKey("e4.win")
  Dialogue.Say(SPEAKER, SPEAKER, "e4.radio.win")
  Mission.Win(0)
end
