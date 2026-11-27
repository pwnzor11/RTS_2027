-- Drill: Build order (S47 U-12). Reach a Refinery, 2 Scrapers and a War Factory. The win is polled; the clock is the sim tick (medal by time).
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.

function OnInit()
  state.refinery = false
  state.scrapers = false
  state.factory = false

  Objective.Add("refinery", "db.obj.refinery", true)
  Objective.Add("scrapers", "db.obj.scrapers", true)
  Objective.Add("factory", "db.obj.factory", true)
  Objective.SetTarget("refinery", "patch")
  Objective.SetTarget("scrapers", "patch")
  Objective.SetTarget("factory", "yard")

  Media.MessageKey("db.start")
  Camera.Focus("yard", 100)
  Trigger.Every(10, Poll)
end

function Poll()
  if not state.refinery and Actor.Count(0, "auth_refinery") >= 1 then
    state.refinery = true
    Objective.Complete("refinery")
  end
  if not state.scrapers and Actor.Count(0, "auth_scraper") >= 2 then
    state.scrapers = true
    Objective.Complete("scrapers")
  end
  if not state.factory and Actor.Count(0, "auth_factory") >= 1 then
    state.factory = true
    Objective.Complete("factory")
  end
  if state.refinery and state.scrapers and state.factory then
    Media.MessageKey("db.done")
    Mission.Win(0)
  end
end
