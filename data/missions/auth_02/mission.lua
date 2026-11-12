-- Authority 02: The Junction (schema 2, M8-12). Hero on the radio: Brigadier Wrenfield; Administrator Pell sends one teletype.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Shape: fortify the junction (two Turrets), hold it until the relief column arrives (visible timer), four Directorate raids.

local SPEAKER = "leader_auth_wrenfield"
local PELL = "leader_auth_pell"

function OnInit()
  state.fortified = false
  state.wave = 0
  state.over = false
  state.waited = false

  Objective.Add("fortify", "a2.obj.fortify", true)
  Objective.Add("hold", "a2.obj.hold", true)
  Objective.AddHidden("mast", "a2.obj.mast", false)
  -- Objective targets (M11-05): where the HUD points.
  Objective.SetTarget("fortify", "junction")
  Objective.SetTarget("hold", "junction")
  Objective.SetTarget("mast", "mast")
  Media.MessageKey("a2.start")
  Dialogue.Say(PELL, PELL, "a2.radio.pell")
  Dialogue.Say(SPEAKER, SPEAKER, "a2.radio.start")
  Camera.Focus("junction", 120)

  -- the Directorate base stays home; the raids below are the pressure (scripted, so every tier is the same story)
  AI.Defend(1, "enemy_base")

  Mission.Timer("relief", 4800, ReliefArrives, "a2.timer.relief")
  Trigger.AfterDelay(Pick({ 700, 600, 500, 400, 300 }), WaveOne)
  Trigger.AfterDelay(Pick({ 1700, 1600, 1500, 1300, 1100 }), WaveTwo)
  Trigger.AfterDelay(Pick({ 2900, 2800, 2600, 2300, 2000 }), WaveThree)
  Trigger.AfterDelay(Pick({ 3900, 3800, 3600, 3300, 3000 }), WaveFour)
  Trigger.Every(20, CheckFortify)
  Trigger.OnDestroyed("yard", YardLost)
  Trigger.OnSeen("mast", 0, MastSeen)
  Trigger.OnDestroyed("mast", MastDown)
end

-- Raid strength follows the difficulty tier (1..5); tier 3 is the design strength. Unit counts only (D-061).
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

function EastEdge()
  return { x = 63, y = 15 + Random.Next(8) }
end

function NorthEdge()
  return { x = 38 + Random.Next(8), y = 0 }
end

function Raid(types, edge)
  state.wave = state.wave + 1
  return Reinforce.Edge(1, types, edge, Map.Center("junction"))
end

function WaveOne()
  Media.MessageKey("a2.wave1")
  Raid(Squad(Pick({ 2, 3, 4, 6, 8 }), 0, 0), EastEdge())
  Dialogue.Say(SPEAKER, SPEAKER, "a2.radio.wave1")
end

function WaveTwo()
  Media.MessageKey("a2.wave2")
  Dialogue.Say(SPEAKER, SPEAKER, "a2.radio.wave2")
  Raid(Squad(Pick({ 3, 4, 5, 7, 9 }), Pick({ 0, 0, 1, 2, 3 }), 0), NorthEdge())
end

function WaveThree()
  Media.MessageKey("a2.wave3")
  Dialogue.Say(SPEAKER, SPEAKER, "a2.radio.wave3")
  Raid(Squad(Pick({ 2, 2, 3, 4, 5 }), Pick({ 1, 1, 1, 2, 2 }), 0), EastEdge())
  Raid(Squad(Pick({ 2, 2, 3, 4, 5 }), Pick({ 0, 0, 1, 1, 2 }), 0), NorthEdge())
end

function WaveFour()
  Media.MessageKey("a2.wave4")
  Dialogue.Say(SPEAKER, SPEAKER, "a2.radio.wave4")
  Raid(Squad(Pick({ 3, 4, 5, 7, 9 }), Pick({ 1, 1, 2, 3, 4 }), Pick({ 0, 0, 1, 2, 3 })), EastEdge())
end

function CheckFortify()
  if state.fortified or state.over then return end
  if Actor.CountInZone("junction", 0, "auth_turret") >= 2 then
    state.fortified = true
    Objective.Complete("fortify")
    Media.MessageKey("a2.fortified")
    Dialogue.Say(SPEAKER, SPEAKER, "a2.radio.fortified")
  end
end

function MastSeen()
  if state.over then return end
  Objective.Reveal("mast")
  Dialogue.Say(SPEAKER, SPEAKER, "a2.radio.mast")
end

function MastDown()
  if Objective.Status("mast") == "active" then
    Objective.Complete("mast")
    Media.MessageKey("a2.mast.down")
  end
end

function YardLost()
  if state.over then return end
  state.over = true
  Objective.Fail("hold")
  Media.MessageKey("a2.lost")
  Mission.Lose(0)
end

function ReliefArrives()
  if state.over then return end
  if not state.fortified then
    -- the column will not drive into an open junction: it waits once, then turns back
    if state.waited then
      state.over = true
      Objective.Fail("fortify")
      Objective.Fail("hold")
      Media.MessageKey("a2.turned")
      Mission.Lose(0)
      return
    end
    state.waited = true
    Media.MessageKey("a2.relief.wait")
    Mission.Timer("relief", 1200, ReliefArrives, "a2.timer.relief")
    return
  end
  state.over = true
  Objective.Complete("hold")
  Media.MessageKey("a2.win")
  Dialogue.Say(SPEAKER, SPEAKER, "a2.radio.win")
  Camera.Focus("junction", 120)
  Mission.Win(0)
end
