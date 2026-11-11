-- Authority 04: The Sunrise Device (schema 2, M8-12). Finale. Hero on the radio: Brigadier Wrenfield.
-- Cameos: Lieutenant Dray (the defector), Commissar Thrall (Directorate), Administrator Pell (the last word).
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Shape: cross the river, break a built-up base (the built-in AI plays it at the chosen tier), destroy the device before the visible timer ends.
-- Raiders hit the yard behind you twice (a second front); the pressure scales by unit count and AI tier only (D-061).

local SPEAKER = "leader_auth_wrenfield"
local DRAY = "leader_auth_dray"
local THRALL = "leader_dir_thrall"
local PELL = "leader_auth_pell"

function OnInit()
  state.over = false
  state.crossed = false
  state.seen = false

  Objective.Add("ford", "a4.obj.ford", true)
  Objective.Add("device", "a4.obj.device", true)
  Objective.Add("home", "a4.obj.home", true)
  Objective.AddHidden("vent", "a4.obj.vent", false)
  -- Objective targets (M11-05): where the HUD points.
  Objective.SetTarget("ford", "south_bank")
  Objective.SetTarget("device", "device")
  Objective.SetTarget("home", "yard")
  Objective.SetTarget("vent", "vent")
  Media.MessageKey("a4.start")
  Dialogue.Say(PELL, PELL, "a4.radio.pell")
  Dialogue.Say(SPEAKER, SPEAKER, "a4.radio.start")
  Camera.Focus("yard", 120)

  -- the built-in AI plays the Directorate base at the mission tier and stays home to defend the device
  AI.SetTier(1, Mission.Difficulty())
  AI.Defend(1, "device_site")

  Mission.Timer("sunrise", 16000, SunriseFires, "a4.timer.sunrise")
  -- The Device belongs to the built-in AI: hold its real charge back to the timer so the AI cannot fire it before the scripted moment (S19 MIS-10).
  Actor.DelayCharge("device", 16000)
  Trigger.AfterDelay(Pick({ 3600, 3300, 3000, 2400, 1800 }), HomeRaidOne)
  Trigger.AfterDelay(Pick({ 7000, 6800, 6500, 4000, 3200 }), HomeRaidTwo)
  AddDefenders()
  Trigger.Every(20, CheckCrossing)
  Trigger.OnSeen("device", 0, DeviceSeen)
  Trigger.OnDestroyed("device", DeviceDown)
  Trigger.OnDestroyed("vent", VentDown)
  Trigger.OnDestroyed("yard", YardLost)
end

function Pick(sizes)
  return sizes[Mission.Difficulty()]
end

-- Tougher defenders at the device on the high tiers (unit counts only, D-061).
function AddDefenders()
  local d = Mission.Difficulty()
  if d < 4 then return end
  local types = Squad(Pick({ 0, 0, 0, 5, 8 }), Pick({ 0, 0, 0, 2, 4 }), Pick({ 0, 0, 0, 0, 2 }))
  Reinforce.Zone(1, types, "device_site", Map.Center("device_site"))
end

function Squad(conscripts, ashthrowers, sappers)
  local types = {}
  for i = 1, conscripts do types[#types + 1] = "dir_conscript" end
  for i = 1, ashthrowers do types[#types + 1] = "dir_ashthrower" end
  for i = 1, sappers do types[#types + 1] = "dir_sapper" end
  return types
end

-- the second front: raiders out of the west, on the yard
function HomeRaidOne()
  if state.over then return end
  Media.MessageKey("a4.raid1")
  Dialogue.Say(SPEAKER, SPEAKER, "a4.radio.raid1")
  Reinforce.Zone(1, Squad(Pick({ 4, 5, 7, 9, 11 }), Pick({ 0, 1, 2, 2, 3 }), 0), "spawn_w", Map.Center("home"))
end

function HomeRaidTwo()
  if state.over then return end
  Media.MessageKey("a4.raid2")
  Reinforce.Zone(1, Squad(Pick({ 5, 6, 8, 10, 12 }), Pick({ 1, 2, 3, 3, 4 }), Pick({ 0, 0, 1, 2, 3 })), "spawn_w", Map.Center("home"))
end

function CheckCrossing()
  if state.crossed or state.over then return end
  if Actor.CountInZone("south_bank", 0) >= 6 then
    state.crossed = true
    Objective.Complete("ford")
    Media.MessageKey("a4.crossed")
    Dialogue.Say(SPEAKER, SPEAKER, "a4.radio.crossed")
    Dialogue.Say(THRALL, THRALL, "a4.radio.thrall")
    Camera.Focus("south_bank", 100)
    -- the Directorate throws its reserve at the bridgehead: the AI's whole army plus a scripted column
    AI.Wave(1, "counter", "south_bank")
    Reinforce.Zone(1, Squad(Pick({ 5, 6, 8, 10, 12 }), Pick({ 2, 2, 3, 4, 5 }), Pick({ 0, 1, 2, 2, 3 })), "enemy_base", Map.Center("south_bank"))
  end
end

function DeviceSeen()
  if state.seen or state.over then return end
  state.seen = true
  Objective.Reveal("vent")
  Media.MessageKey("a4.device.seen")
  Dialogue.Say(DRAY, DRAY, "a4.radio.dray")
  Camera.Focus("device", 120)
end

function VentDown()
  if state.over then return end
  if Objective.Status("vent") == "active" then
    Objective.Complete("vent")
    Media.MessageKey("a4.vent.down")
    -- The vent fed the Device: its charge slows, so the scripted moment moves out by 1500 ticks (S19 MIS-11).
    local left = Mission.TimerLeft("sunrise")
    if left then
      Mission.Timer("sunrise", left + 1500, SunriseFires, "a4.timer.sunrise")
      Actor.DelayCharge("device", left + 1500)
    end
  end
end

function YardLost()
  if state.over then return end
  state.over = true
  Objective.Fail("home")
  Media.MessageKey("a4.lost.yard")
  Mission.Lose(0)
end

function SunriseFires()
  if state.over then return end
  state.over = true
  Objective.Fail("device")
  Media.MessageKey("a4.lost.sunrise")
  Mission.Lose(0)
end

function DeviceDown()
  if state.over then return end
  state.over = true
  Mission.TimerCancel("sunrise")
  Objective.Complete("device")
  if Objective.Status("ford") == "active" then Objective.Complete("ford") end
  Objective.Complete("home")
  Media.MessageKey("a4.win")
  Dialogue.Say(SPEAKER, SPEAKER, "a4.radio.win")
  Dialogue.Say(PELL, PELL, "a4.radio.pellwin")
  Mission.Win(0)
end
