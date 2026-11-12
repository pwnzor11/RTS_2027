-- Authority 03: The Coast Road (schema 2, M8-12). Hero on the radio: Brigadier Wrenfield. Cameos: Chief Surveyor Fenn, the Left Behind war-captain.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Shape: two fronts. The convoy marches south to the shore (ambushes on the way, a picket at the landing); the yard behind is raided.

local SPEAKER = "leader_auth_wrenfield"
local FENN = "leader_auth_fenn"
local CALLOWAY = "leader_lb_calloway_ross"

function OnInit()
  state.over = false
  state.escortDone = false
  state.picketLeft = 6
  state.midHit = false
  state.dunesHit = false
  state.coastHit = false

  Objective.Add("escort", "a3.obj.escort", true)
  Objective.Add("picket", "a3.obj.picket", true)
  Objective.Add("home", "a3.obj.home", true)
  Objective.AddHidden("cache", "a3.obj.cache", false)
  -- Objective targets (M11-05): where the HUD points.
  Objective.SetTarget("escort", "coast")
  Objective.SetTarget("picket", "picket")
  Objective.SetTarget("home", "yard")
  Objective.SetTarget("cache", "cache")
  Media.MessageKey("a3.start")
  Dialogue.Say(SPEAKER, SPEAKER, "a3.radio.start")
  Camera.Focus("carrier1", 120)

  AI.Defend(1, "enemy_base")

  Mission.Timer("tide", 9000, TideTurns, "a3.timer.tide")
  Trigger.AfterDelay(Pick({ 1200, 1200, 1200, 1000, 800 }), HomeWaveOne)
  Trigger.AfterDelay(Pick({ 3000, 3000, 3000, 2600, 2200 }), HomeWaveTwo)
  Trigger.AfterDelay(Pick({ 4800, 4800, 4800, 4300, 3800 }), HomeWaveThree)
  Trigger.Every(20, CheckEscort)
  Trigger.OnDestroyed("carrier1", CarrierLost)
  Trigger.OnDestroyed("carrier2", CarrierLost)
  Trigger.OnDestroyed("yard", YardLost)
  Trigger.OnDestroyed("pk1", PicketDown)
  Trigger.OnDestroyed("pk2", PicketDown)
  Trigger.OnDestroyed("pk3", PicketDown)
  Trigger.OnDestroyed("pk4", PicketDown)
  Trigger.OnDestroyed("pk5", PicketDown)
  Trigger.OnDestroyed("pk6", PicketDown)
  Trigger.OnEnter("coast", 0, CoastCounter)
  Trigger.OnEnter("mid", 0, MidAmbush)
  Trigger.OnEnter("dunes", 0, DunesAmbush)
  Trigger.OnEnter("cache", 0, CacheFound)
end

-- Raid strength follows the difficulty tier (1..5); tier 3 is the design strength. Unit counts only (D-061).
function Pick(sizes)
  return sizes[Mission.Difficulty()]
end

function Squad(faction, a, b, c)
  local types = {}
  if faction == "dir" then
    for i = 1, a do types[#types + 1] = "dir_conscript" end
    for i = 1, b do types[#types + 1] = "dir_ashthrower" end
    for i = 1, c do types[#types + 1] = "dir_sapper" end
  else
    for i = 1, a do types[#types + 1] = "lb_scavenger" end
    for i = 1, b do types[#types + 1] = "lb_hook_runner" end
  end
  return types
end

-- front two: the yard behind the convoy
function HomeWaveOne()
  Media.MessageKey("a3.home1")
  Dialogue.Say(SPEAKER, SPEAKER, "a3.radio.home1")
  Reinforce.Zone(1, Squad("dir", Pick({ 4, 4, 6, 9, 12 }), Pick({ 0, 0, 1, 2, 3 }), 0), "spawn_e", Map.Center("home"))
end

function HomeWaveTwo()
  Media.MessageKey("a3.home2")
  Dialogue.Say(SPEAKER, SPEAKER, "a3.radio.home2")
  Reinforce.Zone(1, Squad("dir", Pick({ 4, 5, 8, 11, 14 }), Pick({ 1, 1, 2, 4, 5 }), 0), "spawn_e", Map.Center("home"))
end

function HomeWaveThree()
  Media.MessageKey("a3.home3")
  Dialogue.Say(SPEAKER, SPEAKER, "a3.radio.home3")
  Reinforce.Zone(1, Squad("dir", Pick({ 5, 6, 9, 12, 16 }), Pick({ 1, 2, 3, 5, 6 }), Pick({ 0, 0, 1, 3, 4 })), "spawn_e", Map.Center("home"))
end

-- front one: the road
function MidAmbush()
  if state.midHit or state.over then return end
  state.midHit = true
  Media.MessageKey("a3.mid")
  Dialogue.Say(SPEAKER, SPEAKER, "a3.radio.mid")
  Reinforce.Zone(1, Squad("dir", Pick({ 4, 5, 8, 11, 14 }), Pick({ 1, 1, 3, 5, 6 }), Pick({ 0, 0, 1, 2, 4 })), "spawn_mid", Map.Center("mid"))
  Camera.Focus("mid", 100)
end

function DunesAmbush()
  if state.dunesHit or state.over then return end
  state.dunesHit = true
  Media.MessageKey("a3.dunes")
  Dialogue.Say(CALLOWAY, CALLOWAY, "a3.radio.drift")
  Reinforce.Zone(2, Squad("lb", Pick({ 4, 4, 6, 9, 12 }), Pick({ 2, 2, 3, 5, 6 }), 0), "lb_spawn", Map.Center("dunes"))
end

function CoastCounter()
  if state.coastHit or state.over then return end
  state.coastHit = true
  Media.MessageKey("a3.coast")
  Dialogue.Say(SPEAKER, SPEAKER, "a3.radio.coast")
  Reinforce.Zone(1, Squad("dir", Pick({ 4, 5, 6, 7, 8 }), Pick({ 2, 2, 2, 3, 3 }), Pick({ 0, 0, 1, 1, 2 })), "enemy_base", Map.Center("coast"))
end

function CacheFound()
  if state.over or Objective.Status("cache") ~= "active" then return end
  Objective.Reveal("cache")
  Objective.Complete("cache")
  Player.AddCash(0, 600)
  Media.MessageKey("a3.cache")
  Dialogue.Say(FENN, FENN, "a3.radio.cache")
end

function CarrierLost()
  if state.over or state.escortDone then return end
  -- Only the two survey carriers count: a built carrier is not the survey party (S19 MIS-15).
  if not Actor.Alive("carrier1") and not Actor.Alive("carrier2") then
    state.over = true
    Objective.Fail("escort")
    Media.MessageKey("a3.lost.carriers")
    Mission.Lose(0)
  end
end

function YardLost()
  if state.over then return end
  state.over = true
  Objective.Fail("home")
  Media.MessageKey("a3.lost.yard")
  Mission.Lose(0)
end

function TideTurns()
  if state.over or state.escortDone then return end
  state.over = true
  Objective.Fail("escort")
  Media.MessageKey("a3.lost.tide")
  Mission.Lose(0)
end

function CheckEscort()
  if state.escortDone or state.over then return end
  -- The survey carriers by tag, not every auth_carrier: a spare built carrier at home must not block the objective (S19 MIS-15).
  local z = Map.Zone("coast")
  local alive, ashore = 0, 0
  for _, tag in ipairs({ "carrier1", "carrier2" }) do
    if Actor.Alive(tag) then
      alive = alive + 1
      local c = Actor.Cell(tag)
      if c.x >= z.x0 and c.x <= z.x1 and c.y >= z.y0 and c.y <= z.y1 then ashore = ashore + 1 end
    end
  end
  if alive >= 1 and ashore >= alive then
    state.escortDone = true
    Objective.Complete("escort")
    Media.MessageKey("a3.shore")
    Dialogue.Say(FENN, FENN, "a3.radio.shore")
    Camera.Focus("picket", 120)
    Mission.TimerCancel("tide")
    CheckWin()
  end
end

function PicketDown()
  if state.over then return end
  state.picketLeft = state.picketLeft - 1
  if state.picketLeft <= 0 then
    Objective.Complete("picket")
    Media.MessageKey("a3.picket.down")
    CheckWin()
  end
end

function CheckWin()
  if state.over then return end
  if state.escortDone and state.picketLeft <= 0 then
    state.over = true
    Objective.Complete("home")
    Media.MessageKey("a3.win")
    Dialogue.Say(SPEAKER, SPEAKER, "a3.radio.win")
    Mission.Win(0)
  end
end
