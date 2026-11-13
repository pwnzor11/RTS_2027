-- Left Behind 01: The Refused (schema 2, M8-10). Hero on the radio: Jessup Calloway-Ross, war-captain.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Text: radio and messages come from locale.toml. Objective and timer labels are locale keys.
-- Pressure scales with Mission.Difficulty() (tier 1..5) by unit count only; cash and vision never change (D-061).

local SPEAKER = "leader_lb_calloway_ross"
local CONVOY_DELAY = 2700   -- ticks until the convoy leaves the depot (time to unpack and raise a camp)
local HALT_TICKS = 700     -- ticks after departure the convoy waits at the crossing for its escort to close up
local DEADLINE = 6000       -- ticks after departure before a scraper that is still alive counts as through

function OnInit()
  state.deployed = false
  state.departed = false
  state.relief = false
  state.relief2 = false
  state.leg = 0
  state.departTick = 0
  state.reliefCalled = false
  state.cache = false
  state.done = false
  state.rigGone = 0
  state.enemy = {}
  state.reliefTags = {}

  Objective.Add("deploy", "lb1.obj.deploy", true)
  Objective.Add("scrapers", "lb1.obj.scrapers", true)
  Objective.Add("escort", "lb1.obj.escort", true)
  Objective.AddHidden("cache", "lb1.obj.cache", false)
  -- Objective targets (M11-05): where the HUD points.
  Objective.SetTarget("deploy", "camp")
  Objective.SetTarget("scrapers", "survey")
  Objective.SetTarget("escort", "ambush")
  Objective.SetTarget("cache", "cache")
  Media.MessageKey("lb1.start")
  Dialogue.Say(SPEAKER, SPEAKER, "lb1.radio.start")

  -- The packed Mother Rig parks on an ash cell (not a legal site): the player picks clear ground and unpacks it.
  state.rig = Reinforce.Rig(0, { x = 0, y = 22 }, { x = 26, y = 22 })
  Reinforce.Edge(0, StartForce(), { x = 0, y = 25 }, { x = 24, y = 19 })
  Camera.Focus(state.rig, 160)

  SpawnConvoy()
  Mission.Timer("convoy", CONVOY_DELAY, Depart, "lb1.timer.convoy")
  Trigger.AfterDelay(900, HintRadio)
  Trigger.Every(20, CheckMission)
  Trigger.OnEnter("cache", 0, CacheFound)
  for i, tag in pairs(state.enemy) do Trigger.OnDestroyed(tag, EnemyLost) end
end

function StartForce()
  return { "lb_buggy", "lb_buggy", "lb_scavenger", "lb_scavenger", "lb_hook_runner", "lb_hook_runner", "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger" }
end

-- Escort riflemen by tier: 2,2,3,3,4 plus a jeep from tier 4. The scrapers are placed by mission.toml.
function EscortTypes()
  local sizes = { 2, 2, 3, 3, 4 }
  local types = {}
  for i = 1, sizes[Mission.Difficulty()] do types[i] = "auth_rifleman" end
  if Mission.Difficulty() >= 4 then types[#types + 1] = "auth_jeep" end
  if Mission.Difficulty() >= 5 then types[#types + 1] = "auth_jeep" end
  return types
end

-- Relief by tier: 1,1,2,3,4 riflemen plus a jeep at tier 5. Tiers 4-5 get a second, later relief group.
function ReliefTypes()
  local sizes = { 1, 1, 2, 3, 4 }
  local types = {}
  for i = 1, sizes[Mission.Difficulty()] do types[i] = "auth_rifleman" end
  if Mission.Difficulty() >= 5 then types[#types + 1] = "auth_jeep" end
  return types
end

function SpawnConvoy()
  state.enemy[1] = "scraper1"
  state.enemy[2] = "scraper2"
  local tags = Actor.CreateGroup(EscortTypes(), 1, { x = 14, y = 8 }, { formation = "line", tagPrefix = "esc" })
  for i, tag in pairs(tags) do state.enemy[#state.enemy + 1] = tag end
end

function HintRadio()
  if not state.cache then Dialogue.Say(SPEAKER, SPEAKER, "lb1.radio.hint") end
end

function Depart()
  state.departed = true
  state.leg = 1
  state.departTick = Mission.Tick()
  Media.MessageKey("lb1.depart")
  Dialogue.Say(SPEAKER, SPEAKER, "lb1.radio.depart")
  SendConvoy(Map.Center("ambush"))
  Camera.Focus("scraper1", 160)
  Trigger.AfterDelay(DEADLINE, Deadline)
end

-- The scrapers drive, everything else attack-moves, to the given cell.
function SendConvoy(cell)
  for i, tag in pairs(state.enemy) do
    if Actor.Alive(tag) then
      if tag == "scraper1" or tag == "scraper2" then
        Actor.Order(tag, "move", cell)
      else
        Actor.Order(tag, "attackmove", cell)
      end
    end
  end
end

-- First leg ends at the crossing: the convoy waits there for its escort, then goes on to the survey ground.
function ConvoyMoveOn()
  state.leg = 2
  Dialogue.Say(SPEAKER, SPEAKER, "lb1.radio.moveon")
  SendConvoy(Map.Center("survey"))
end

function EnemyLost()
  if state.done or state.reliefCalled then return end
  state.reliefCalled = true
  Dialogue.Say(SPEAKER, SPEAKER, "lb1.radio.contact")
  Trigger.AfterDelay(300, SendRelief)
end

function SendRelief()
  if state.done then return end
  local tags = Reinforce.Edge(1, ReliefTypes(), { x = 40 + Random.Next(4), y = 0 }, Map.Center("ambush"))
  for i, tag in pairs(tags) do state.reliefTags[#state.reliefTags + 1] = tag end
  state.relief = true
  if Mission.Difficulty() >= 4 then Trigger.AfterDelay(500, SendSecondRelief) end
  Media.MessageKey("lb1.relief")
  Dialogue.Say(SPEAKER, SPEAKER, "lb1.radio.relief")
  Camera.Focus(tags[1], 120)
end

-- Tiers 4-5: a second group arrives from the opposite edge of the north road.
function SendSecondRelief()
  if state.done then return end
  local n = Mission.Difficulty() - 1
  local types = {}
  for i = 1, n do types[i] = "auth_rifleman" end
  local tags = Reinforce.Edge(1, types, { x = 44 + Random.Next(4), y = 0 }, Map.Center("ambush"))
  for i, tag in pairs(tags) do state.reliefTags[#state.reliefTags + 1] = tag end
  state.relief2 = true
  Media.MessageKey("lb1.relief2")
  Dialogue.Say(SPEAKER, SPEAKER, "lb1.radio.relief2")
end

function CacheFound()
  if state.cache or state.done then return end
  state.cache = true
  Objective.Reveal("cache")
  Objective.Complete("cache")
  Actor.Create("auth_scraper", 0, { x = 5, y = 46 }, "gullyscraper")
  Player.AddCash(0, 600)
  Media.MessageKey("lb1.cache")
  Dialogue.Say(SPEAKER, SPEAKER, "lb1.radio.cache")
end

function ScrapersAlive()
  return Actor.Alive("scraper1") or Actor.Alive("scraper2")
end

function EnemyAlive()
  for i, tag in pairs(state.enemy) do
    if Actor.Alive(tag) then return true end
  end
  for i, tag in pairs(state.reliefTags) do
    if Actor.Alive(tag) then return true end
  end
  return false
end

function CheckMission()
  if state.done then return end
  if not state.deployed and Actor.Count(0, "lb_mother_rig") >= 1 then
    state.deployed = true
    Objective.Complete("deploy")
    Media.MessageKey("lb1.deployed")
    Dialogue.Say(SPEAKER, SPEAKER, "lb1.radio.deployed")
    Camera.Focus("camp", 120)
  end
  if Actor.Count(0, "lb_mother_rig") + Actor.CountPacked(0, "lb_mother_rig") == 0 then
    state.rigGone = state.rigGone + 1
    if state.rigGone >= 2 then return LoseMission("lb1.lost.rig", "deploy") end
  else
    state.rigGone = 0
  end
  if state.leg == 1 and Mission.Tick() >= state.departTick + HALT_TICKS then ConvoyMoveOn() end
  if Actor.CountInZone("survey", 1, "auth_scraper") >= 1 then return LoseMission("lb1.lost.site", "scrapers") end
  if Objective.Status("scrapers") == "active" and not ScrapersAlive() then
    Objective.Complete("scrapers")
    Media.MessageKey("lb1.scrapers.down")
    Dialogue.Say(SPEAKER, SPEAKER, "lb1.radio.scrapers")
  end
  if Objective.Status("escort") == "active" and Objective.Status("scrapers") == "completed" and state.relief and (Mission.Difficulty() < 4 or state.relief2) and not EnemyAlive() then
    Objective.Complete("escort")
  end
  if Mission.AllPrimaryDone() then WinMission() end
end

function Deadline()
  if state.done then return end
  if ScrapersAlive() then LoseMission("lb1.lost.site", "scrapers") end
end

function LoseMission(key, objective)
  state.done = true
  Objective.Fail(objective)
  Media.MessageKey(key)
  Mission.Lose(0)
end

function WinMission()
  state.done = true
  Media.MessageKey("lb1.win")
  Dialogue.Say(SPEAKER, SPEAKER, "lb1.radio.win")
  Mission.Win(0)
end
