-- Left Behind 04: The Banner (schema 2, M8-12, finale). Hero on the radio: Jessup Calloway-Ross, now the warlord.
-- Cameo: Air Marshal Perpetua Kell (leader_exp_kell) on the Expedition net.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Text: radio and messages come from locale.toml. Objective and timer labels are locale keys.
-- Pressure scales with Mission.Difficulty() (tier 1..5) by unit count and AI tier only; cash and vision never change (D-061).

local SPEAKER = "leader_lb_calloway_ross"
local KELL = "leader_exp_kell"
local FLEET_DELAY = 12000    -- ticks after the landing before the second wave of the fleet arrives and the coast is lost

function OnInit()
  state.piles = 0
  state.landing = false
  state.landing2 = false
  state.landingTags = {}
  state.wreck = false
  state.done = false
  state.rigGone = 0

  Objective.Add("counter", "lb4.obj.counter", true)
  Objective.Add("power", "lb4.obj.power", true)
  Objective.Add("depot", "lb4.obj.depot", true)
  Objective.AddHidden("wreck", "lb4.obj.wreck", false)
  -- Objective targets (M11-05): where the HUD points.
  Objective.SetTarget("counter", "camp")
  Objective.SetTarget("power", "pile1")
  Objective.SetTarget("depot", "depot")
  Objective.SetTarget("wreck", "wreck")
  Media.MessageKey("lb4.start")
  Dialogue.Say(SPEAKER, SPEAKER, "lb4.radio.start")

  -- The built-in AI holds the beachhead at the chosen tier; the script sends the counter-landing.
  AI.SetTier(1, Mission.Difficulty())
  AI.Defend(1, "beach")

  Actor.CreateGroup(StartForce(), 0, { x = 46, y = 24 }, { formation = "box" })
  Actor.CreateGroup(StartInfantry(), 0, { x = 52, y = 24 }, { formation = "box" })
  Camera.Focus("camp", 160)
  ReinforceBeach()

  Mission.Timer("landing", LandingDelay(), Landing, "lb4.timer.landing")
  Trigger.AfterDelay(1800, HintRadio)
  Trigger.Every(20, CheckMission)
  Trigger.OnEnter("wreck", 0, WreckFound)
  Trigger.OnDestroyed("pile1", PileDown)
  Trigger.OnDestroyed("pile2", PileDown)
  Trigger.OnDestroyed("depot", DepotDown)
  Trigger.OnDestroyed("battery1", BatteryDown)
  Trigger.OnDestroyed("battery2", BatteryDown)
end

function StartForce()
  return { "lb_rolling_fort", "lb_rolling_fort", "lb_rolling_fort", "lb_harpoon_wagon", "lb_harpoon_wagon", "lb_harpoon_wagon", "lb_harpoon_wagon",
           "lb_organ", "lb_organ", "lb_buggy", "lb_buggy", "lb_tinker", "lb_tinker", "lb_veilwalker", "lb_veilwalker" }
end

function StartInfantry()
  return { "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger",
           "lb_drifter_brute", "lb_drifter_brute", "lb_drifter_brute", "lb_drifter_brute",
           "lb_scavenger", "lb_scavenger", "lb_scavenger", "lb_scavenger", "lb_scavenger", "lb_scavenger",
           "lb_hook_runner", "lb_hook_runner", "lb_hook_runner" }
end

-- Extra marines at the beachhead by tier: 0,0,2,5,8.
function ReinforceBeach()
  local extra = { 0, 0, 2, 5, 8 }
  local n = extra[Mission.Difficulty()]
  if n > 0 then
    local types = {}
    for i = 1, n do types[i] = "exp_marine" end
    Actor.CreateGroup(types, 1, { x = 47, y = 76 }, { formation = "line" })
  end
end

-- The counter-landing by tier: marines 5,6,8,10,12 plus pathfinders 0,0,1,3,4. It comes earlier at the high tiers,
-- and tiers 4-5 get a second landing group (see SecondLanding).
function LandingTypes()
  local marines = { 5, 6, 8, 10, 12 }
  local scouts = { 0, 0, 1, 3, 4 }
  local types = {}
  for i = 1, marines[Mission.Difficulty()] do types[i] = "exp_marine" end
  for i = 1, scouts[Mission.Difficulty()] do types[#types + 1] = "exp_pathfinder" end
  return types
end

function LandingDelay()
  local delays = { 5400, 5400, 5400, 4800, 4200 }
  return delays[Mission.Difficulty()]
end

-- Tiers 4-5: a second landing group, marines 4,6, comes ashore 1500 ticks after the first.
function SecondLanding()
  if state.done then return end
  local types = {}
  for i = 1, (Mission.Difficulty() - 2) * 2 do types[i] = "exp_marine" end
  types[#types + 1] = "exp_pathfinder"
  local tags = Reinforce.Zone(1, types, "landing", Map.Center("camp"))
  for i, tag in pairs(tags) do state.landingTags[#state.landingTags + 1] = tag end
  state.landing2 = true
  Media.MessageKey("lb4.landing2")
  Dialogue.Say(SPEAKER, SPEAKER, "lb4.radio.landing2")
end

function HintRadio()
  if state.piles == 0 and not state.landing then Dialogue.Say(SPEAKER, SPEAKER, "lb4.radio.hint") end
end

function Landing()
  if state.landing or state.done then return end
  state.landing = true
  local tags = Reinforce.Zone(1, LandingTypes(), "landing", Map.Center("camp"))
  for i, tag in pairs(tags) do state.landingTags[#state.landingTags + 1] = tag end
  Media.MessageKey("lb4.landing")
  Dialogue.Say(KELL, KELL, "lb4.radio.kell")
  Dialogue.Say(SPEAKER, SPEAKER, "lb4.radio.landing")
  Camera.Focus(tags[1], 120)
  if Mission.Difficulty() >= 4 then Trigger.AfterDelay(1500, SecondLanding) end
  Mission.Timer("fleet", FLEET_DELAY, FleetArrives, "lb4.timer.fleet")
  Trigger.AfterDelay(FLEET_DELAY - 2400, FleetWarning)
end

function FleetWarning()
  if state.done or Objective.Status("depot") ~= "active" then return end
  Dialogue.Say(KELL, KELL, "lb4.radio.fleet")
  Dialogue.Say(SPEAKER, SPEAKER, "lb4.radio.warn")
end

function FleetArrives()
  if state.done or Objective.Status("depot") ~= "active" then return end
  state.done = true
  Objective.Fail("depot")
  Media.MessageKey("lb4.lost.fleet")
  Mission.Lose(0)
end

function PileDown()
  if state.done then return end
  state.piles = state.piles + 1
  if Actor.Alive("pile2") then Objective.SetTarget("power", "pile2") end
  Media.MessageKey("lb4.pile.down")
  if state.piles >= 2 then
    Objective.Complete("power")
    Media.MessageKey("lb4.dark")
    Dialogue.Say(SPEAKER, SPEAKER, "lb4.radio.dark")
    Camera.Focus("battery1", 120)
  end
end

function BatteryDown()
  if state.done then return end
  Media.MessageKey("lb4.battery.down")
end

function DepotDown()
  if state.done or Objective.Status("depot") ~= "active" then return end
  Objective.Complete("depot")
  Media.MessageKey("lb4.depot.down")
  Dialogue.Say(KELL, KELL, "lb4.radio.kellout")
  Camera.Focus("depot", 120)
end

function WreckFound()
  if state.wreck or state.done then return end
  state.wreck = true
  Objective.Reveal("wreck")
  Objective.Complete("wreck")
  Actor.CreateGroup({ "lb_tinker", "lb_tinker" }, 0, Map.Center("wreck"), { formation = "line" })
  Player.AddCash(0, 700)
  Media.MessageKey("lb4.wreck")
  Dialogue.Say(SPEAKER, SPEAKER, "lb4.radio.wreck")
end

function LandingAlive()
  for i, tag in pairs(state.landingTags) do
    if Actor.Alive(tag) then return true end
  end
  return false
end

function CheckMission()
  if state.done then return end
  if Actor.Count(0, "lb_mother_rig") + Actor.CountPacked(0, "lb_mother_rig") == 0 then
    state.rigGone = state.rigGone + 1
    if state.rigGone >= 2 then return LoseMission("lb4.lost.rig", "counter") end
  else
    state.rigGone = 0
  end
  if Objective.Status("counter") == "active" and state.landing and (Mission.Difficulty() < 4 or state.landing2) and not LandingAlive() then
    Objective.Complete("counter")
    Media.MessageKey("lb4.counter.down")
    Dialogue.Say(SPEAKER, SPEAKER, "lb4.radio.counter")
  end
  if Mission.AllPrimaryDone() then WinMission() end
end

function LoseMission(key, objective)
  state.done = true
  Objective.Fail(objective)
  Media.MessageKey(key)
  Mission.Lose(0)
end

function WinMission()
  state.done = true
  Media.MessageKey("lb4.win")
  Dialogue.Say(SPEAKER, SPEAKER, "lb4.radio.win")
  Mission.Win(0)
end
