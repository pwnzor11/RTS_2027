-- Directorate 02: The Convoy Roads (schema 2, M8-12 D2). Hero on the radio: the Voice (Odette Larkin).
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Text: radio and messages come from locale.toml. Objective and timer labels are locale keys.

local SPEAKER = "leader_dir_larkin"

function OnInit()
  state.postsDown = 0
  state.convoys = 0
  state.convoyTags = {}
  state.reserve = false
  state.waveTags = {}
  state.done = false

  Objective.Add("posts", "d2.obj.posts", true)
  Objective.Add("convoy", "d2.obj.convoy", true)
  Objective.AddHidden("depot", "d2.obj.depot", false)
  -- Objective targets (M11-05): where the HUD points.
  Objective.SetTarget("posts", "post_n")
  Objective.SetTarget("depot", "depot_silo")
  Media.MessageKey("d2.start")
  Dialogue.Say(SPEAKER, SPEAKER, "d2.radio.start")
  Camera.Focus("site", 120)

  -- The enemy base stays home; every pressure on the player comes from the script (unit count by tier, D-061).
  AI.Defend(1, "home")
  AddGarrison()

  Trigger.OnDestroyed("shaft", ShaftLost)
  Trigger.OnDestroyed("post_n", PostDown)
  Trigger.OnDestroyed("post_w", PostDown)
  Trigger.OnDestroyed("post_e", PostDown)
  Trigger.OnDestroyed("depot_silo", DepotDown)
  Trigger.OnSeen("depot_silo", 0, DepotSeen)
  Trigger.Every(40, CheckConvoys)
  Mission.Timer("convoy1", 900, ConvoyOne, "d2.timer.convoy1")
  Trigger.AfterDelay(600, RoadRadio)
end

-- Extra garrison by tier: tiers 1-2 none, 3 one, 4-5 three more riflemen spread over the posts.
function AddGarrison()
  local extra = { 0, 0, 1, 3, 3 }
  local cells = { { x = 39, y = 13 }, { x = 11, y = 36 }, { x = 55, y = 33 } }
  for n = 1, extra[Mission.Difficulty()] do
    Actor.Create("auth_rifleman", 1, cells[n])
  end
end

function RoadRadio()
  Dialogue.Say(SPEAKER, SPEAKER, "d2.radio.roads")
end

function ShaftLost()
  if state.done then return end
  Media.MessageKey("d2.lost")
  Mission.Lose(0)
end

function PostDown()
  state.postsDown = state.postsDown + 1
  for _, tag in ipairs({ "post_n", "post_w", "post_e" }) do
    if Actor.Alive(tag) then Objective.SetTarget("posts", tag) break end
  end
  if state.postsDown == 1 then
    Dialogue.Say(SPEAKER, SPEAKER, "d2.radio.post1")
    Trigger.AfterDelay(200, ReserveWave)
  end
  -- tiers 4-5: a second reserve answers the second post, out of the same road
  if state.postsDown == 2 and Mission.Difficulty() >= 4 and not state.done then
    local types = {}
    for i = 1, (Mission.Difficulty() >= 5 and 5 or 3) do types[i] = "auth_rifleman" end
    types[#types + 1] = "auth_jeep"
    Reinforce.Edge(1, types, { x = 63, y = 22 + Random.Next(6) }, Map.Center("site"))
    Media.MessageKey("d2.reserve2")
    Dialogue.Say(SPEAKER, SPEAKER, "d2.radio.reserve2")
  end
  if state.postsDown >= 3 then
    Objective.Complete("posts")
    Media.MessageKey("d2.posts.down")
    CheckWin()
  end
end

-- Convoys: tier 1..5 sets the escort by unit count only.
function EscortSize()
  local sizes = { 1, 1, 1, 2, 5 }
  return sizes[Mission.Difficulty()]
end

function Convoy(edge, dest)
  local types = { "auth_scraper" }
  for i = 1, EscortSize() do types[#types + 1] = "auth_jeep" end
  local tags = Reinforce.Edge(1, types, edge, dest)
  for i, tag in pairs(tags) do
    state.convoyTags[#state.convoyTags + 1] = tag
  end
  state.convoys = state.convoys + 1
end

function ConvoyOne()
  Media.MessageKey("d2.convoy1")
  Dialogue.Say(SPEAKER, SPEAKER, "d2.radio.convoy1")
  Convoy({ x = 38, y = 0 }, { x = 38, y = 14 })
  Camera.Focus("north_post", 160)
  Mission.Timer("convoy2", 1500, ConvoyTwo, "d2.timer.convoy2")
end

function ConvoyTwo()
  Media.MessageKey("d2.convoy2")
  Dialogue.Say(SPEAKER, SPEAKER, "d2.radio.convoy2")
  Convoy({ x = 63, y = 30 }, { x = 57, y = 30 })
  Camera.Focus("east_post", 160)
end

function CheckConvoys()
  if state.convoys < 2 or Objective.Status("convoy") ~= "active" then return end
  for i, tag in pairs(state.convoyTags) do
    if Actor.Alive(tag) then return end
  end
  Objective.Complete("convoy")
  Media.MessageKey("d2.convoys.down")
  Dialogue.Say(SPEAKER, SPEAKER, "d2.radio.convoys")
  CheckWin()
end

-- The reserve answers the first fallen post. Rifles by tier; a jeep from tier 4.
function ReserveSize()
  local sizes = { 1, 2, 3, 5, 10 }
  return sizes[Mission.Difficulty()]
end

function ReserveWave()
  if state.reserve or state.done then return end
  state.reserve = true
  Media.MessageKey("d2.reserve")
  Dialogue.Say(SPEAKER, SPEAKER, "d2.radio.reserve")
  local types = {}
  for i = 1, ReserveSize() do types[i] = "auth_rifleman" end
  if Mission.Difficulty() >= 4 then types[#types + 1] = "auth_jeep" end
  Reinforce.Edge(1, types, { x = 63, y = 22 + Random.Next(6) }, Map.Center("site"))
  Camera.Focus("site", 160)
end

function DepotSeen()
  if Objective.Status("depot") == "active" then
    Objective.Reveal("depot")
    Dialogue.Say(SPEAKER, SPEAKER, "d2.radio.depot")
  end
end

function DepotDown()
  if Objective.Status("depot") == "active" then
    Objective.Reveal("depot")
    Objective.Complete("depot")
    Media.MessageKey("d2.depot.down")
  end
end

function CheckWin()
  if not state.done and Mission.AllPrimaryDone() then
    state.done = true
    if Objective.Status("depot") == "active" then Objective.Reveal("depot") end
    Media.MessageKey("d2.win")
    Dialogue.Say(SPEAKER, SPEAKER, "d2.radio.win")
    Mission.Win(0)
  end
end
