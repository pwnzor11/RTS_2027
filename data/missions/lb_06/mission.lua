-- Left Behind 06: Salvage Rights (schema 2, S48 C-12). Hero on the radio: Jessup Calloway-Ross, war-captain.
-- No base: a packed rig, a Yard to drop at, three haulers and a war band. Shot cars leave wrecks (they rot in 1200 ticks); haulers strip them.
-- Sandbox rules: integers only, TOP-LEVEL named callbacks, data in the global `state`. Pressure follows Mission.Difficulty() by unit count only (D-061).

local SPEAKER = "leader_lb_calloway_ross"
local GOAL = 1200          -- ash to bank (nothing can be built, so funds only grow by hauling)
local CREWS_DELAY = 5400   -- ticks until the recovery crews take the train back

function OnInit()
  state.over = false
  state.engine = false
  state.haulers = 3
  state.opened = false

  Objective.Add("open", "lb6.obj.open", true)
  Objective.Add("bank", "lb6.obj.bank", true)
  Objective.AddHidden("engine", "lb6.obj.engine", false)
  Objective.SetTarget("open", "train_wreck")
  Objective.SetTarget("bank", "yard")
  Objective.SetTarget("engine", "engine")
  Media.MessageKey("lb6.start")
  Dialogue.Say(SPEAKER, SPEAKER, "lb6.radio.start")
  Camera.Focus("survey_camp", 160)

  -- the Authority never gets defeated by the sim here, and neither do we: the script decides (no base, EverHadBase)
  Player.KeepAlive(0, true)
  Player.KeepAlive(1, true)

  -- the picket at the east end of the train guards the engine car
  Actor.CreateGroup(Squad(Pick({ 2, 3, 4, 5, 7 }), Pick({ 0, 0, 0, 1, 2 }), Pick({ 0, 0, 1, 1, 2 })), 1, { x = 60, y = 32 })

  Mission.Timer("crews", CREWS_DELAY, CrewsArrive, "lb6.timer.crews")
  Trigger.AfterDelay(900, Hint)
  Trigger.AfterDelay(1800, Scouts)
  Trigger.AfterDelay(2700, Half)
  Trigger.AfterDelay(4500, Late)
  Trigger.Every(20, CheckBank)
  Trigger.OnDestroyed("engine", EngineOpen)
  Trigger.OnDestroyed("yard", YardLost)
  Trigger.OnDestroyed("hauler1", HaulerDown)
  Trigger.OnDestroyed("hauler2", HaulerDown)
  Trigger.OnDestroyed("hauler3", HaulerDown)
  Trigger.OnAllDestroyed(0, ColumnLost)
end

-- Strength follows the difficulty tier (1..5); tier 3 is the design strength. Unit counts only (D-061).
function Pick(sizes)
  return sizes[Mission.Difficulty()]
end

function Squad(riflemen, rocketeers, jeeps)
  local types = {}
  for i = 1, riflemen do types[#types + 1] = "auth_rifleman" end
  for i = 1, rocketeers do types[#types + 1] = "auth_rocketeer" end
  for i = 1, jeeps do types[#types + 1] = "auth_jeep" end
  return types
end

function Hint()
  if state.over then return end
  Dialogue.Say(SPEAKER, SPEAKER, "lb6.radio.hint")
end

-- scouts come down the main street of the mill town to the camp
function Scouts()
  if state.over then return end
  Media.MessageKey("lb6.scouts")
  Dialogue.Say(SPEAKER, SPEAKER, "lb6.radio.scouts")
  Reinforce.Edge(1, Squad(Pick({ 2, 3, 4, 6, 8 }), Pick({ 0, 0, 1, 1, 2 }), Pick({ 0, 0, 1, 1, 2 })), { x = 29 + Random.Next(5), y = 0 }, Map.Center("survey_camp"))
end

function Half()
  if state.over then return end
  Media.MessageKey("lb6.half")
  Dialogue.Say(SPEAKER, SPEAKER, "lb6.radio.half")
end

-- the advance guard of the recovery crews arrives along the rail before the timer ends
function Late()
  if state.over then return end
  Media.MessageKey("lb6.late")
  Dialogue.Say(SPEAKER, SPEAKER, "lb6.radio.late")
  Reinforce.Edge(1, Squad(Pick({ 3, 4, 6, 8, 10 }), Pick({ 0, 1, 1, 2, 3 }), Pick({ 0, 0, 1, 2, 2 })), { x = 63, y = 31 + Random.Next(3) }, Map.Center("survey_camp"))
end

-- primary 1: every car shot open (each leaves a wreck); primary 2: the ash banked at the Yard. Both before the crews come.
function CheckBank()
  if state.over then return end
  if not state.opened then
    for i = 1, 8 do
      if Actor.Alive("car" .. i) then return end
    end
    state.opened = true
    Objective.Complete("open")
  end
  if Player.Funds(0) >= GOAL then
    state.over = true
    Objective.Complete("bank")
    Media.MessageKey("lb6.win")
    Dialogue.Say(SPEAKER, SPEAKER, "lb6.radio.win")
    Mission.Win(0)
  end
end

function EngineOpen()
  if state.over or state.engine then return end
  state.engine = true
  Objective.Reveal("engine")
  Objective.Complete("engine")
  Media.MessageKey("lb6.engine")
  Dialogue.Say(SPEAKER, SPEAKER, "lb6.radio.engine")
end

function CrewsArrive()
  if state.over then return end
  state.over = true
  Objective.Fail(state.opened and "bank" or "open")
  Media.MessageKey("lb6.missed")
  Mission.Lose(0)
end

function YardLost()
  if state.over then return end
  state.over = true
  Objective.Fail(state.opened and "bank" or "open")
  Media.MessageKey("lb6.lost.yard")
  Mission.Lose(0)
end

-- all three haulers gone: nobody left to strip the wrecks
function HaulerDown()
  if state.over then return end
  state.haulers = state.haulers - 1
  if state.haulers <= 0 then
    state.over = true
    Objective.Fail(state.opened and "bank" or "open")
    Media.MessageKey("lb6.lost.haulers")
    Mission.Lose(0)
  end
end

-- no base: the sim will not end the mission when the column dies (EverHadBase), so the script does
function ColumnLost()
  if state.over then return end
  state.over = true
  Objective.Fail(state.opened and "bank" or "open")
  Media.MessageKey("lb6.lost.column")
  Mission.Lose(0)
end
