-- Left Behind 02: Road Posts (schema 2, M8-12). Hero on the radio: Jessup Calloway-Ross, war-captain.
-- Cameo: Brigadier Wrenfield (leader_auth_wrenfield) on the Authority net.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Text: radio and messages come from locale.toml. Objective and timer labels are locale keys.
-- Pressure scales with Mission.Difficulty() (tier 1..5) by unit count only; cash and vision never change (D-061).

local SPEAKER = "leader_lb_calloway_ross"
local WRENFIELD = "leader_auth_wrenfield"
local REPRISAL_DELAY = 5400   -- ticks until the column comes if no post has fallen yet
local REPRISAL_LAG = 300      -- ticks between the first post going silent and the column appearing
local GARRISON_DELAY = 9000   -- ticks after the column arrives before the posts are fortified for good

function OnInit()
  state.posts = 0
  state.power = 0
  state.postDown = { false, false, false }
  state.reprisal = false
  state.reprisalTags = {}
  state.stash = false
  state.seen = false
  state.done = false
  state.rigGone = 0

  Objective.Add("gate", "lb2.obj.gate", true)
  Objective.Add("posts", "lb2.obj.posts", true)
  Objective.Add("hold", "lb2.obj.hold", true)
  Objective.AddHidden("stash", "lb2.obj.stash", false)
  -- Objective targets (M11-05): where the HUD points.
  Objective.SetTarget("gate", "rig")
  Objective.SetTarget("posts", "post1")
  Objective.SetTarget("hold", "camp")
  Objective.SetTarget("stash", "stash")
  Media.MessageKey("lb2.start")
  Dialogue.Say(SPEAKER, SPEAKER, "lb2.radio.start")

  Actor.CreateGroup(StartForce(), 0, { x = 29, y = 19 }, { formation = "box" })
  Camera.Focus("camp", 160)
  ReinforcePosts()

  Mission.Timer("reprisal", REPRISAL_DELAY, SendReprisal, "lb2.timer.reprisal")
  Trigger.AfterDelay(1200, HintRadio)
  Trigger.Every(20, CheckMission)
  Trigger.OnEnter("stash", 0, StashFound)
  Trigger.OnDestroyed("post1", Post1Down)
  Trigger.OnDestroyed("post2", Post2Down)
  Trigger.OnDestroyed("post3", Post3Down)
  Trigger.OnDestroyed("pow1", PowerDown)
  Trigger.OnDestroyed("pow2", PowerDown)
  Trigger.OnDestroyed("pow3", PowerDown)
  Trigger.OnSeen("post1", 0, PostSeen)
  Trigger.OnSeen("post2", 0, PostSeen)
  Trigger.OnSeen("post3", 0, PostSeen)
end

function StartForce()
  return { "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger", "lb_scavenger", "lb_scavenger", "lb_scavenger",
           "lb_scavenger", "lb_hook_runner", "lb_hook_runner", "lb_buggy", "lb_buggy" }
end

-- Extra post riflemen by tier: 0,1,1,2,3 at each post, and a second turret on post 3 at tier 5.
function ReinforcePosts()
  local extra = { 0, 1, 1, 2, 3 }
  local n = extra[Mission.Difficulty()]
  local cells = { { x = 40, y = 13 }, { x = 10, y = 32 }, { x = 12, y = 42 } }
  for i = 1, 3 do
    if n > 0 then
      local types = {}
      for j = 1, n do types[j] = "auth_rifleman" end
      Actor.CreateGroup(types, 1, cells[i], { formation = "line" })
    end
  end
  if Mission.Difficulty() >= 5 then Actor.Create("auth_turret", 5, { x = 14, y = 40 }, "post3b") end
end

-- Reprisal by tier: riflemen 3,4,5,6,7 plus jeeps 0,0,1,1,2.
function ReprisalTypes()
  local rifles = { 3, 4, 5, 6, 7 }
  local jeeps = { 0, 0, 1, 1, 2 }
  local types = {}
  for i = 1, rifles[Mission.Difficulty()] do types[i] = "auth_rifleman" end
  for i = 1, jeeps[Mission.Difficulty()] do types[#types + 1] = "auth_jeep" end
  return types
end

function HintRadio()
  if state.posts == 0 and not state.seen then Dialogue.Say(SPEAKER, SPEAKER, "lb2.radio.hint") end
end

function PostSeen()
  if state.seen or state.done then return end
  state.seen = true
  Dialogue.Say(SPEAKER, SPEAKER, "lb2.radio.seen")
end

function PowerDown()
  if state.done then return end
  state.power = state.power + 1
  Media.MessageKey("lb2.power.down")
  if state.power == 1 then Dialogue.Say(SPEAKER, SPEAKER, "lb2.radio.power") end
end

function Post1Down() PostDown(1) end
function Post2Down() PostDown(2) end
function Post3Down() PostDown(3) end

function PostDown(n)
  if state.done or state.postDown[n] then return end
  state.postDown[n] = true
  state.posts = state.posts + 1
  for _, tag in ipairs({ "post1", "post2", "post3" }) do
    if Actor.Alive(tag) then Objective.SetTarget("posts", tag) break end
  end
  Media.MessageKey("lb2.post.down")
  Dialogue.Say(SPEAKER, SPEAKER, "lb2.radio.post" .. state.posts)
  if state.posts == 1 and not state.reprisal then
    Mission.TimerCancel("reprisal")
    Dialogue.Say(WRENFIELD, WRENFIELD, "lb2.radio.intercept")
    Trigger.AfterDelay(REPRISAL_LAG, SendReprisal)
  end
  if state.posts >= 3 then
    Objective.Complete("posts")
    Camera.Focus("camp", 100)
  end
end

function SendReprisal()
  if state.reprisal or state.done then return end
  state.reprisal = true
  Mission.TimerCancel("reprisal")
  local tags = Reinforce.Edge(1, ReprisalTypes(), { x = 40 + Random.Next(4), y = 0 }, Map.Center("camp"))
  for i, tag in pairs(tags) do state.reprisalTags[#state.reprisalTags + 1] = tag end
  Media.MessageKey("lb2.reprisal")
  Dialogue.Say(SPEAKER, SPEAKER, "lb2.radio.reprisal")
  Camera.Focus(tags[1], 120)
  Mission.Timer("garrison", GARRISON_DELAY, Garrison, "lb2.timer.garrison")
  Trigger.AfterDelay(GARRISON_DELAY - 1500, GarrisonWarning)
end

function GarrisonWarning()
  if state.done or state.posts >= 3 then return end
  Dialogue.Say(SPEAKER, SPEAKER, "lb2.radio.warn")
end

function Garrison()
  if state.done or state.posts >= 3 then return end
  state.done = true
  Objective.Fail("posts")
  Media.MessageKey("lb2.lost.time")
  Mission.Lose(0)
end

function StashFound()
  if state.stash or state.done then return end
  state.stash = true
  Objective.Reveal("stash")
  Objective.Complete("stash")
  Actor.CreateGroup({ "lb_buggy", "lb_buggy" }, 0, Map.Center("stash"), { formation = "line" })
  Player.AddCash(0, 600)
  Media.MessageKey("lb2.stash")
  Dialogue.Say(SPEAKER, SPEAKER, "lb2.radio.stash")
end

function ReprisalAlive()
  for i, tag in pairs(state.reprisalTags) do
    if Actor.Alive(tag) then return true end
  end
  return false
end

function CheckMission()
  if state.done then return end
  if Objective.Status("gate") == "active" and Actor.Count(0, "lb_garage_rig") >= 1 and Actor.Count(0, "lb_lookout") >= 1 then
    Objective.Complete("gate")
    Media.MessageKey("lb2.gate")
    Dialogue.Say(SPEAKER, SPEAKER, "lb2.radio.gate")
  end
  if Actor.Count(0, "lb_mother_rig") + Actor.CountPacked(0, "lb_mother_rig") == 0 then
    state.rigGone = state.rigGone + 1
    if state.rigGone >= 2 then return LoseMission("lb2.lost.rig", "gate") end
  else
    state.rigGone = 0
  end
  if Objective.Status("hold") == "active" and state.reprisal and not ReprisalAlive() then
    Objective.Complete("hold")
    Media.MessageKey("lb2.reprisal.down")
    Dialogue.Say(SPEAKER, SPEAKER, "lb2.radio.beaten")
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
  Media.MessageKey("lb2.win")
  Dialogue.Say(SPEAKER, SPEAKER, "lb2.radio.win")
  Mission.Win(0)
end
