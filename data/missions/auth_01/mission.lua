-- Authority 01: Survey Convoy (schema 2, M8-10 pilot). Hero on the radio: Brigadier Wrenfield.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Text: radio and messages come from locale.toml. Objective and timer texts are plain strings (the API does not resolve keys there).

local SPEAKER = "leader_auth_wrenfield"

function OnInit()
  state.phase = "convoy"
  state.raids = 0
  state.raidTags = {}
  state.surveyors = 2

  Objective.Add("reach", "a1.obj.reach", true)
  Objective.Add("scrapers", "a1.obj.scrapers", true)
  Objective.AddHidden("surveyors", "a1.obj.surveyors", false)
  -- Objective targets (M11-05): where the HUD points.
  Objective.SetTarget("reach", "survey")
  Objective.SetTarget("scrapers", "scraper1")
  Objective.SetTarget("surveyors", "svy1")
  Media.MessageKey("a1.start")
  Dialogue.Say(SPEAKER, SPEAKER, "a1.radio.start")
  Camera.Focus("jeep", 120)

  Trigger.Every(20, CheckConvoy)
  Trigger.OnDestroyed("scraper1", ScraperLost)
  Trigger.OnDestroyed("scraper2", ScraperLost)
  Trigger.OnDestroyed("svy1", SurveyorLost)
  Trigger.OnDestroyed("svy2", SurveyorLost)
  Trigger.OnEnter("survey", 0, ScoutArrived)
  Trigger.AfterDelay(400, ConvoyRadio)
end

function ConvoyRadio()
  if state.phase == "convoy" then
    Dialogue.Say(SPEAKER, SPEAKER, "a1.radio.chatter")
  end
end

function ScoutArrived()
  Media.MessageKey("a1.site.clear")
  if state.phase ~= "done" then
    Objective.Reveal("surveyors")
    Dialogue.Say(SPEAKER, SPEAKER, "a1.radio.civilians")
  end
end

function SurveyorLost()
  if state.surveyors > 0 then state.surveyors = state.surveyors - 1 end
  if Objective.Status("surveyors") == "active" then
    Objective.Fail("surveyors")
    Dialogue.Say(SPEAKER, SPEAKER, "a1.radio.civilians_lost")
  end
end

function ScraperLost()
  if state.phase == "done" then return end
  if not Actor.Alive("scraper1") and Actor.Alive("scraper2") then Objective.SetTarget("scrapers", "scraper2") end
  if not Actor.Alive("scraper1") and not Actor.Alive("scraper2") then
    Objective.Fail("scrapers")
    Media.MessageKey("a1.lost")
    Mission.Lose(0)
  end
end

function CheckConvoy()
  if state.phase ~= "convoy" then return end
  local alive = Actor.Count(0, "auth_scraper")
  if alive >= 1 and Actor.CountInZone("survey", 0, "auth_scraper") >= alive then
    state.phase = "survey"
    Objective.Complete("reach")
    Objective.Add("hold", "a1.obj.hold", true)
    Objective.SetTarget("hold", "survey")
    Dialogue.Say(SPEAKER, SPEAKER, "a1.radio.down")
    Camera.Focus("survey", 120)
    Trigger.AfterDelay(Pick({ 300, 300, 300, 260, 220 }), RaidOne)
    Mission.Timer("raid2", Pick({ 1500, 1500, 1500, 1400, 1300 }), RaidTwo, "a1.timer.raid2")
  end
end

-- Raid strength and arrival time follow the difficulty tier (1..5); tier 3 is the original raid. Never cash or vision (D-061).
function Pick(sizes)
  return sizes[Mission.Difficulty()]
end

function RaidOneSize()
  return Pick({ 2, 2, 3, 4, 5 })
end

function RaidTwoSize()
  return Pick({ 3, 3, 4, 5, 6 })
end

function Raid(types, edge)
  local tags = Reinforce.Edge(1, types, edge, Map.Center("survey"))
  for i, tag in pairs(tags) do
    state.raidTags[#state.raidTags + 1] = tag
  end
  state.raids = state.raids + 1
end

function RaidOne()
  Media.MessageKey("a1.raid1")
  Dialogue.Say(SPEAKER, SPEAKER, "a1.radio.raid1")
  local types = {}
  for i = 1, RaidOneSize() do types[i] = "auth_rifleman" end
  Raid(types, { x = 63, y = 8 + Random.Next(6) })
end

function RaidTwo()
  Media.MessageKey("a1.raid2")
  Dialogue.Say(SPEAKER, SPEAKER, "a1.radio.raid2")
  local types = {}
  for i = 1, RaidTwoSize() do types[i] = "auth_rifleman" end
  if Mission.Difficulty() >= 4 then types[#types + 1] = "auth_jeep" end
  Raid(types, { x = 44 + Random.Next(4), y = 0 })
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
  Objective.Add("refinery", "a1.obj.refinery", true)
  Objective.SetTarget("refinery", "pit")
  Media.MessageKey("a1.raiders.beaten")
  Dialogue.Say(SPEAKER, SPEAKER, "a1.radio.build")
  Camera.Focus("yard", 120)
  Trigger.Every(40, CheckRefinery)
end

function CheckRefinery()
  if state.phase ~= "build" then return end
  if Actor.CountInZone("pit", 0, "auth_refinery") >= 1 then
    state.phase = "done"
    Objective.Complete("refinery")
    Objective.Complete("scrapers")
    if Objective.Status("surveyors") == "active" then Objective.Complete("surveyors") end
    Media.MessageKey("a1.win")
    Dialogue.Say(SPEAKER, SPEAKER, "a1.radio.win")
    Mission.Win(0)
  end
end
