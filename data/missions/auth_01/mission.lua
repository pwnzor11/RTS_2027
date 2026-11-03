-- Authority 01: Survey Convoy (pilot, D-061).
-- Rules of this sandbox: integers only, no pairs on hash tables, callbacks are TOP-LEVEL named functions,
-- every piece of script data lives in the global `state` table (saved and hashed with the game).

function OnInit()
  state.phase = "convoy"
  state.raids = 0
  state.raidTags = {}

  Objective.Add("reach", "Escort the scraper convoy to the survey site in the north-east", true)
  Objective.Add("scrapers", "Keep at least one scraper alive", true)
  Media.Message("Convoy is rolling. Get the scrapers to the survey site.")

  Trigger.Every(20, CheckConvoy)
  Trigger.OnDestroyed("scraper1", ScraperLost)
  Trigger.OnDestroyed("scraper2", ScraperLost)
  Trigger.OnEnter("survey", 0, ScoutArrived)
  Trigger.AfterDelay(400, ConvoyRadio)
end

function ConvoyRadio()
  if state.phase == "convoy" then
    Media.Message("Command: Directorate chatter on the band. Keep the convoy tight.")
  end
end

function ScoutArrived()
  Media.Message("Scouts report the site is clear. Bring the scrapers up.")
end

function ScraperLost()
  if state.phase == "done" then return end
  if not Actor.Alive("scraper1") and not Actor.Alive("scraper2") then
    Objective.Fail("scrapers")
    Media.Message("Both scrapers are lost. The survey is over.")
    Mission.Lose(0)
  end
end

function CheckConvoy()
  if state.phase ~= "convoy" then return end
  local alive = Actor.Count(0, "auth_scraper")
  if alive >= 1 and Actor.CountInZone("survey", 0, "auth_scraper") >= alive then
    state.phase = "survey"
    Objective.Complete("reach")
    Objective.Add("hold", "Hold the survey site until the raiders are beaten", true)
    Media.Message("Survey gear is down. Expect company: two raids inbound.")
    Trigger.AfterDelay(300, RaidOne)
    Trigger.AfterDelay(1500, RaidTwo)
  end
end

function Raid(types, edge)
  local tags = Reinforce.Edge(1, types, edge, Map.Center("survey"))
  for i, tag in pairs(tags) do
    state.raidTags[#state.raidTags + 1] = tag
  end
  state.raids = state.raids + 1
end

function RaidOne()
  Media.Message("Raiders from the east road!")
  Raid({ "auth_rifleman", "auth_rifleman", "auth_rifleman" }, { x = 63, y = 8 + Random.Next(6) })
end

function RaidTwo()
  Media.Message("Second wave from the north!")
  Raid({ "auth_rifleman", "auth_rifleman", "auth_rifleman", "auth_rifleman", "auth_jeep" }, { x = 44 + Random.Next(4), y = 0 })
  Trigger.Every(40, CheckRaids)
end

function CheckRaids()
  if state.phase ~= "survey" or state.raids < 2 then return end
  for i, tag in pairs(state.raidTags) do
    if Actor.Alive(tag) then return end
  end
  state.phase = "build"
  Objective.Complete("hold")
  Actor.Create("auth_conyard", 0, { x = 23, y = 25 }, "yard")
  Player.AddCash(0, 1000)
  Objective.Add("refinery", "Build a Refinery beside the pitchblende pit", true)
  Media.Message("Raiders beaten. A forward Construction Yard has been airlifted to the pit. Power first, then the Refinery.")
  Trigger.Every(40, CheckRefinery)
end

function CheckRefinery()
  if state.phase ~= "build" then return end
  if Actor.CountInZone("pit", 0, "auth_refinery") >= 1 then
    state.phase = "done"
    Objective.Complete("refinery")
    Objective.Complete("scrapers")
    Media.Message("Refinery online. Pitchblende is ours. Well done, Commander.")
    Mission.Win(0)
  end
end
