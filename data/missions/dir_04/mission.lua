-- Directorate 04: The Sunrise (schema 2, M8-12 D4). Hero on the radio: the Voice (Odette Larkin).
-- Cameos: Commissar Thrall (Order Seven) and Chief Surveyor Fenn (Authority), as radio.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- The Device (dir_sunrise, tag "device") charges 3000 ticks from tick 0 and fires by a Special command from the player.
-- It only fires when a finished hatch (tag "head") stands within 8 cells of the target (the Authority "hq" at the Ash Ridge).

local SPEAKER = "leader_dir_larkin"
local THRALL = "leader_dir_thrall"
local FENN = "leader_auth_fenn"

function OnInit()
  state.waves = 0
  state.charged = false
  state.done = false

  Objective.Add("hold", "d4.obj.hold", true)
  Objective.Add("fire", "d4.obj.fire", true)
  Objective.AddHidden("west", "d4.obj.west", false)
  -- Objective targets (M11-05): where the HUD points.
  Objective.SetTarget("hold", "device")
  Objective.SetTarget("fire", "hq")
  Objective.SetTarget("west", "west_post")
  Media.MessageKey("d4.start")
  Dialogue.Say(THRALL, THRALL, "d4.radio.thrall")
  Dialogue.Say(SPEAKER, SPEAKER, "d4.radio.start")
  Camera.Focus("device", 120)

  -- The enemy base stays home and builds; the pressure comes from the script, by unit count only (D-061).
  AI.Defend(1, "home")

  Trigger.OnDestroyed("shaft", ShaftLost)
  Trigger.OnDestroyed("device", DeviceLost)
  Trigger.OnDestroyed("head", HeadLost)
  Trigger.OnDestroyed("hq", SunriseFired)
  Trigger.OnDestroyed("west_post", WestDown)
  Trigger.OnSeen("west_post", 0, WestSeen)
  Mission.Timer("charge", 3050, Charged, "d4.timer.charge")
  Mission.Timer("wave1", WaveGap({ 400, 350, 300, 300, 300 }), WaveOne, "d4.timer.wave1")
  Trigger.AfterDelay(1800, FennRadio)
end

function FennRadio()
  if not state.done then Dialogue.Say(FENN, FENN, "d4.radio.fenn") end
end

function ShaftLost()
  if state.done then return end
  Media.MessageKey("d4.lost.shaft")
  Mission.Lose(0)
end

function DeviceLost()
  if state.done then return end
  Media.MessageKey("d4.lost.device")
  Mission.Lose(0)
end

-- The hatch is the Device's way to the target: losing it before the shot ends the order.
function HeadLost()
  if state.done then return end
  Media.MessageKey("d4.lost.head")
  Mission.Lose(0)
end

-- Waves cross the river to the hatch. Counts follow the tier; conscripts lose to riflemen, so the waves are small.
function WaveSizes()
  local sizes = {
    { 2, 3, 3, 4, 5 },
    { 2, 3, 4, 4, 5 },
    { 3, 3, 4, 5, 6 },
  }
  return sizes[state.waves + 1][Mission.Difficulty()]
end

-- Ticks before a wave: slower at the low tiers, earlier and closer together at the high ones.
function WaveGap(ticks)
  return ticks[Mission.Difficulty()]
end

function Wave()
  local types = {}
  for i = 1, WaveSizes() do types[i] = "auth_rifleman" end
  local d = Mission.Difficulty()
  if state.waves == 1 and d >= 4 then types[#types + 1] = "auth_jeep" end
  if state.waves == 2 and d >= 3 then types[#types + 1] = "auth_jeep" end
  if state.waves == 2 and d >= 5 then types[#types + 1] = "auth_tank" end
  Reinforce.Zone(1, types, "spawn", Map.Center("head"))
  state.waves = state.waves + 1
  Camera.Focus("head", 160)
end

function WaveOne()
  Media.MessageKey("d4.wave1")
  Dialogue.Say(SPEAKER, SPEAKER, "d4.radio.wave1")
  Wave()
  Mission.Timer("wave2", WaveGap({ 1000, 950, 900, 900, 900 }), WaveTwo, "d4.timer.wave2")
end

function WaveTwo()
  Media.MessageKey("d4.wave2")
  Wave()
  Mission.Timer("wave3", WaveGap({ 1000, 950, 900, 900, 900 }), WaveThree, "d4.timer.wave3")
end

function WaveThree()
  Media.MessageKey("d4.wave3")
  Wave()
  -- The Authority throws the army it has built so far at the hatch.
  AI.Wave(1, "last_stand", "head")
end

-- The Device is charged: the player fires it (Special command at the Ash Ridge command).
function Charged()
  if state.done then return end
  state.charged = true
  Objective.Complete("hold")
  Media.MessageKey("d4.charged")
  Dialogue.Say(SPEAKER, SPEAKER, "d4.radio.charged")
  Camera.Focus("device", 160)
end

function SunriseFired()
  if state.done then return end
  state.done = true
  if not state.charged then Objective.Complete("hold") end
  Objective.Complete("fire")
  if Objective.Status("west") == "active" then Objective.Reveal("west") end
  Media.MessageKey("d4.win")
  Dialogue.Say(SPEAKER, SPEAKER, "d4.radio.win")
  Mission.Win(0)
end

function WestSeen()
  if Objective.Status("west") == "active" then
    Objective.Reveal("west")
    Dialogue.Say(SPEAKER, SPEAKER, "d4.radio.west")
  end
end

function WestDown()
  if Objective.Status("west") == "active" then
    Objective.Reveal("west")
    Objective.Complete("west")
    Media.MessageKey("d4.west.down")
  end
end
