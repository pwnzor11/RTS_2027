-- Drill: Raid (S47 U-12). Destroy the three depot buildings (t1, t2, t3) before the alarm timer runs out. Guards and the turret are optional.
-- Each building has its own OnDestroyed trigger; the last one wins (same tick, so the sim's own game over cannot get in first).
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.

function OnInit()
  state.left = 3

  Objective.Add("raid", "dr.obj.raid", true)
  Objective.SetTarget("raid", "depot")

  Media.MessageKey("dr.start")
  Camera.Focus("depot", 100)
  Mission.Timer("alarm", 4800, Alarm, "dr.timer.alarm")
  Trigger.OnDestroyed("t1", TargetDown)
  Trigger.OnDestroyed("t2", TargetDown)
  Trigger.OnDestroyed("t3", TargetDown)
  Trigger.Every(20, Poll)
end

function TargetDown()
  state.left = state.left - 1
  if state.left <= 0 then
    Objective.Complete("raid")
    Media.MessageKey("dr.done")
    Mission.Win(0)
  else
    Media.MessageKey("dr.hit")
  end
end

function Alarm()
  Media.MessageKey("dr.alarm")
  Mission.Lose(0)
end

function Poll()
  if Actor.Count(0, "auth_rifleman") + Actor.Count(0, "auth_jeep") == 0 then
    Media.MessageKey("dr.lost")
    Mission.Lose(0)
  end
end
