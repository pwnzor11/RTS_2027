-- Drill: Hold the line (S47 U-12). Three scripted waves come through the east gate; your force is fixed. Win when wave three is dead.
-- The win is polled; the raiders keep a power plant, so the sim never ends the game on its own. Medal by units and structures lost.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.

function OnInit()
  state.waves = 0
  state.foes = {}

  Objective.Add("hold", "dh.obj.hold", true)
  Objective.SetTarget("hold", "gate")

  Media.MessageKey("dh.start")
  Camera.Focus("line", 100)
  Trigger.AfterDelay(300, WaveOne)
  Trigger.AfterDelay(1100, WaveTwo)
  Trigger.AfterDelay(2000, WaveThree)
  Trigger.Every(20, Poll)
end

-- Sends a squad from the spawn zone through the gate to the middle of your line.
function Wave(types)
  state.waves = state.waves + 1
  local tags = Reinforce.Zone(1, types, "spawn", Map.Center("line"))
  for i = 1, #tags do state.foes[#state.foes + 1] = tags[i] end
end

function WaveOne()
  Media.MessageKey("dh.wave1")
  Wave({ "auth_rifleman", "auth_rifleman", "auth_rifleman", "auth_rifleman" })
end

function WaveTwo()
  Media.MessageKey("dh.wave2")
  Wave({ "auth_rifleman", "auth_rifleman", "auth_rifleman", "auth_rifleman", "auth_rifleman", "auth_rifleman", "auth_jeep" })
end

function WaveThree()
  Media.MessageKey("dh.wave3")
  Wave({ "auth_rifleman", "auth_rifleman", "auth_rifleman", "auth_rifleman", "auth_rifleman", "auth_rifleman", "auth_rifleman", "auth_rifleman", "auth_jeep", "auth_jeep" })
end

function Poll()
  if Actor.Count(0, "auth_rifleman") + Actor.Count(0, "auth_tank") == 0 then
    Media.MessageKey("dh.lost")
    Mission.Lose(0)
    return
  end
  if state.waves < 3 then return end
  for i = 1, #state.foes do
    if Actor.Alive(state.foes[i]) then return end
  end
  Objective.Complete("hold")
  Media.MessageKey("dh.done")
  Mission.Win(0)
end
