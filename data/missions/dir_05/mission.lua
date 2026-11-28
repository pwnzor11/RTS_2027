-- Directorate 05: Night Shift (schema 2, S48 C-12 wave 1; design 22 section 3.2). Hero on the radio: the Voice (Odette Larkin).
-- No base: a conscript shift and two sappers cut three Authority power plants (the searchlight line), then come home before dawn.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.

local SPEAKER = "leader_dir_larkin"
local PLANTS = { "plant1", "plant2", "plant3" }

function OnInit()
  state.down = 0
  state.over = false

  Objective.Add("power", "d5.obj.power", true)
  Objective.Add("home", "d5.obj.home", true)
  Objective.AddHidden("sappers", "d5.obj.sappers", false)
  Objective.SetTarget("power", "plant1")
  Objective.SetTarget("sappers", "hatch")
  Media.MessageKey("d5.start")
  Dialogue.Say(SPEAKER, SPEAKER, "d5.radio.lullaby")
  Dialogue.Say(SPEAKER, SPEAKER, "d5.radio.start")
  Camera.Focus("hatch", 120)

  -- the Authority stays alive for the whole mission (Player.KeepAlive); its base stays home, the guards and alarms below are the pressure (tier counts, D-061)
  Player.KeepAlive(1, true)
  AI.SetTier(1, Mission.Difficulty())
  AI.Defend(1, "enemy_base")
  Actor.CreateGroup(Riflemen(Pick({ 1, 1, 1, 2, 3 })), 1, { x = 27, y = 14 })
  Actor.CreateGroup(Riflemen(Pick({ 1, 1, 2, 2, 3 })), 1, { x = 33, y = 7 })
  Actor.CreateGroup(Riflemen(Pick({ 1, 1, 2, 3, 4 })), 1, { x = 42, y = 12 })

  for _, tag in ipairs(PLANTS) do Trigger.OnDestroyed(tag, PlantDown) end
  Mission.Timer("dawn", 5400, DawnBreaks, "d5.timer.dawn")
  -- no base: the sim will not end the mission when the shift dies (EverHadBase), so the script does
  Trigger.OnAllDestroyed(0, ShiftLost)
  Trigger.Every(20, CheckHome)
end

-- Strength follows the difficulty tier (1..5); tier 3 is the design strength. Unit counts only (D-061).
function Pick(sizes)
  return sizes[Mission.Difficulty()]
end

function Riflemen(n)
  local types = {}
  for i = 1, n do types[#types + 1] = "auth_rifleman" end
  return types
end

function PlantDown()
  if state.over then return end
  state.down = state.down + 1
  Media.MessageKey("d5.plant")
  for _, tag in ipairs(PLANTS) do
    if Actor.Alive(tag) then Objective.SetTarget("power", tag) break end
  end
  if state.down == 1 then
    Dialogue.Say(SPEAKER, SPEAKER, "d5.radio.plant1")
    local types = Riflemen(Pick({ 0, 1, 2, 3, 5 }))
    if #types > 0 then
      Media.MessageKey("d5.alarm1")
      Dialogue.Say(SPEAKER, SPEAKER, "d5.radio.alarm1")
      Reinforce.Edge(1, types, { x = 30, y = 0 }, Map.Center("hatch"))
    end
  elseif state.down == 2 then
    Dialogue.Say(SPEAKER, SPEAKER, "d5.radio.plant2")
    local types = Riflemen(Pick({ 0, 0, 1, 2, 4 }))
    if #types > 0 then
      if Mission.Difficulty() >= 4 then types[#types + 1] = "auth_jeep" end
      Media.MessageKey("d5.alarm2")
      Dialogue.Say(SPEAKER, SPEAKER, "d5.radio.alarm2")
      Reinforce.Edge(1, types, { x = 63, y = 14 + Random.Next(5) }, Map.Center("hatch"))
    end
  else
    Objective.Complete("power")
    Objective.SetTarget("home", "hatch")
    Objective.Reveal("sappers")
    Media.MessageKey("d5.dark")
    Dialogue.Say(SPEAKER, SPEAKER, "d5.radio.dark")
  end
end

-- Home = every living unit of the shift stands in the hatch zone, after all three plants are dark.
function CheckHome()
  if state.over or state.down < 3 then return end
  local left = Actor.Count(0)
  if left > 0 and Actor.CountInZone("hatch", 0) >= left then
    state.over = true
    Objective.Complete("home")
    if Actor.Count(0, "dir_sapper") >= 2 then Objective.Complete("sappers") end
    Media.MessageKey("d5.win")
    Dialogue.Say(SPEAKER, SPEAKER, "d5.radio.win")
    Mission.Win(0)
  end
end

function DawnBreaks()
  if state.over then return end
  state.over = true
  Objective.Fail("home")
  Media.MessageKey("d5.dawn")
  Mission.Lose(0)
end

function ShiftLost()
  if state.over then return end
  state.over = true
  Objective.Fail("home")
  Media.MessageKey("d5.lost")
  Mission.Lose(0)
end
