-- Directorate 06: The Assembly Bore (schema 2, S48 C-12, design 22 section 3.2). Hero on the radio: the Voice (Odette Larkin).
-- Dr. Perpetua Lund (Isotope Works) speaks for the first time. Build and destroy: raise the smelter vent, then the cistern, bunk gallery and
-- assembly bore on the scrap field, and clear the Authority survey camp before its column arrives.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Text: radio and messages come from locale.toml. Objective and timer labels are locale keys. Voice lines are text only.

local SPEAKER = "leader_dir_larkin"
local LUND = "leader_dir_lund"

function OnInit()
  state.smelter = false
  state.bore = false
  state.camp = false
  state.done = false
  state.stakes = false

  Objective.Add("smelter", "d6.obj.smelter", true)
  Objective.Add("bore", "d6.obj.bore", true)
  Objective.Add("camp", "d6.obj.camp", true)
  Objective.AddHidden("stakes", "d6.obj.stakes", false)
  -- Objective targets (M11-05): where the HUD points.
  Objective.SetTarget("smelter", "bore_site")
  Objective.SetTarget("bore", "bore_site")
  Objective.SetTarget("camp", "camp_rig")
  Media.MessageKey("d6.start")
  Dialogue.Say(SPEAKER, SPEAKER, "d6.radio.start")
  Camera.Focus("bore_site", 120)

  -- The Authority base stays home; every pressure on the shift comes from the script (unit count by tier, D-061).
  AI.SetTier(1, Mission.Difficulty())
  AI.Defend(1, "home_ne")
  PlaceCamp()
  PlaceStakes()

  Trigger.OnDestroyed("shaft", ShaftLost)
  Trigger.OnDestroyed("stake_rig", StakesDown)
  Trigger.Every(20, CheckBuild)
  Trigger.Every(40, CheckCamp)
  Trigger.AfterDelay(400, MillRadio)
  Trigger.AfterDelay(1800, PatrolNorth)
  Trigger.AfterDelay(3600, RailGang)
  -- the visible clock: the survey column leaves the east edge for the camp and the shaft
  Mission.Timer("survey", 6000, SurveyColumn, "d6.timer.survey")
end

-- Strength follows the difficulty tier (1..5); tier 3 is the design strength. Unit counts only (D-061).
function Pick(sizes)
  return sizes[Mission.Difficulty()]
end

function Troops(rifles, rockets, jeeps)
  local types = {}
  for i = 1, rifles do types[#types + 1] = "auth_rifleman" end
  for i = 1, rockets do types[#types + 1] = "auth_rocketeer" end
  for i = 1, jeeps do types[#types + 1] = "auth_jeep" end
  return types
end

-- The garrison and turrets of the survey camp (the rig, generator and searchlights are in mission.toml). Turrets need the generator's power.
function PlaceCamp()
  Actor.CreateGroup(Troops(Pick({ 2, 3, 4, 5, 7 }), Pick({ 0, 0, 1, 2, 3 }), 0), 1, Map.Center("survey_camp"))
  local spots = { { x = 38, y = 27 }, { x = 45, y = 27 } }
  for i = 1, Pick({ 0, 0, 1, 2, 2 }) do
    Actor.Create("auth_turret", 1, spots[i])
  end
end

-- The hidden bonus: a lone survey stake and a rifleman or two at the pitch spur, beside the pit Lund wants kept quiet.
function PlaceStakes()
  Actor.Create("auth_seismograph", 1, { x = 10, y = 27 }, "stake_rig")
  local n = Pick({ 0, 1, 1, 2, 2 })
  if n > 0 then Actor.CreateGroup(Troops(n, 0, 0), 1, { x = 13, y = 27 }) end
end

function MillRadio()
  if state.done then return end
  Dialogue.Say(LUND, LUND, "d6.radio.lund")
end

function ShaftLost()
  if state.done then return end
  state.done = true
  Media.MessageKey("d6.lost")
  Mission.Lose(0)
end

-- Build progress: the smelter vent opens the drawings; the cistern, gallery and bore finish the shift's work.
function CheckBuild()
  if state.done then return end
  if not state.smelter and Actor.CountInZone("bore_site", 0, "dir_smelter_vent") >= 1 then
    state.smelter = true
    Objective.Complete("smelter")
    Tech.GrantMany(0, { "dir_assembly_bore", "dir_cistern", "dir_bunk_gallery" })
    Objective.Reveal("stakes")
    Objective.SetTarget("stakes", "pitch_spur")
    Media.MessageKey("d6.smelter")
    Dialogue.Say(LUND, LUND, "d6.radio.smelter")
    Dialogue.Say(SPEAKER, SPEAKER, "d6.radio.stakes")
  end
  if state.smelter and not state.bore and Actor.CountInZone("bore_site", 0, "dir_assembly_bore") >= 1
      and Actor.Count(0, "dir_cistern") >= 1 and Actor.Count(0, "dir_bunk_gallery") >= 1 then
    state.bore = true
    Objective.Complete("bore")
    Tech.Grant(0, "dir_rocket_truck")
    Media.MessageKey("d6.bore")
    Dialogue.Say(LUND, LUND, "d6.radio.bore")
    CheckWin()
  end
end

-- The camp falls when its rig, generator and searchlights are down and nobody of the tenancy is left standing in it.
function CheckCamp()
  if state.done or state.camp then return end
  for _, tag in ipairs({ "camp_rig", "camp_power", "camp_light1", "camp_light2" }) do
    if Actor.Alive(tag) then return end
  end
  if Actor.CountInZone("survey_camp", 1) > 0 then return end
  state.camp = true
  Objective.Complete("camp")
  Media.MessageKey("d6.camp")
  Dialogue.Say(SPEAKER, SPEAKER, "d6.radio.camp")
  CheckWin()
end

function StakesDown()
  if state.stakes then return end
  state.stakes = true
  if Objective.Status("stakes") == "active" then
    Objective.Reveal("stakes")
    Objective.Complete("stakes")
  end
  Media.MessageKey("d6.stakes")
end

-- A patrol from the north road looks for the shaft.
function PatrolNorth()
  if state.done then return end
  Media.MessageKey("d6.patrol")
  Dialogue.Say(SPEAKER, SPEAKER, "d6.radio.patrol")
  Reinforce.Edge(1, Troops(Pick({ 2, 3, 4, 5, 7 }), 0, 0), { x = 27 + Random.Next(9), y = 0 }, Map.Center("bore_site"))
end

-- A rail gang out of the west (tier 2 and up) comes along the old rail bed.
function RailGang()
  if state.done then return end
  local types = Troops(Pick({ 0, 2, 3, 4, 6 }), Pick({ 0, 0, 0, 1, 2 }), 0)
  if #types == 0 then return end   -- tier 1 has no rail gang
  Media.MessageKey("d6.rail")
  Dialogue.Say(SPEAKER, SPEAKER, "d6.radio.rail")
  Reinforce.Edge(1, types, { x = 0, y = 30 + Random.Next(4) }, Map.Center("bore_site"))
end

-- The survey column: the clock the player watches. It comes in off the east rail for the camp, then the shaft.
function SurveyColumn()
  if state.done then return end
  Media.MessageKey("d6.column")
  Dialogue.Say(SPEAKER, SPEAKER, "d6.radio.column")
  Reinforce.Edge(1, Troops(Pick({ 3, 4, 5, 7, 9 }), Pick({ 0, 1, 2, 2, 3 }), Pick({ 0, 0, 1, 1, 2 })), { x = 63, y = 30 + Random.Next(4) }, Map.Center("bore_site"))
  Camera.Focus("survey_camp", 120)
end

function CheckWin()
  if not state.done and Mission.AllPrimaryDone() then
    state.done = true
    if Objective.Status("stakes") == "active" then Objective.Reveal("stakes") end
    Media.MessageKey("d6.win")
    Dialogue.Say(SPEAKER, SPEAKER, "d6.radio.win")
    Mission.Win(0)
  end
end
