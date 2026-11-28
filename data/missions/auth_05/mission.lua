-- Authority 05: Hatch Country (schema 2, S48 C-12, design 22 section 3.1). Hero on the radio: Brigadier Wrenfield (she/her), nobody else.
-- No base: the recon column is all there is. Close three surface hatches (engineer capture or demolition) and pull down the loudspeaker pole.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Shape: a visible timer ("name read out") runs from the start; closing the first hatch calls a patrol down from the north.

local SPEAKER = "leader_auth_wrenfield"
local HATCHES = { "hatch1", "hatch2", "hatch3" }

function OnInit()
  state.closed = 0
  state.shut = { false, false, false }
  state.pole = false
  state.logged = false
  state.over = false

  Objective.Add("hatches", "a5.obj.hatches", true)
  Objective.Add("pole", "a5.obj.pole", true)
  Objective.AddHidden("log", "a5.obj.log", false)
  Objective.SetTarget("hatches", "hatch1")
  Objective.SetTarget("pole", "pole")

  Media.MessageKey("a5.start")
  Dialogue.Say(SPEAKER, SPEAKER, "a5.radio.start")
  Dialogue.Say(SPEAKER, SPEAKER, "a5.radio.pole")
  Camera.Focus("hatch1", 120)

  -- the Directorate stays alive whatever happens to its hatches (Player.KeepAlive, S47); its guards are the pressure (counts follow the tier, D-061)
  Player.KeepAlive(1, true)
  AI.SetTier(1, Mission.Difficulty())
  AI.Defend(1, "pole")

  Actor.CreateGroup(Squad(Pick({ 1, 2, 2, 3, 4 }), 0), 1, { x = 23, y = 15 })
  Actor.CreateGroup(Squad(Pick({ 1, 2, 3, 3, 4 }), Pick({ 0, 0, 0, 1, 1 })), 1, { x = 34, y = 18 })
  Actor.CreateGroup(Squad(Pick({ 2, 2, 3, 4, 5 }), Pick({ 0, 0, 1, 1, 2 })), 1, { x = 44, y = 14 })
  Actor.CreateGroup(Squad(Pick({ 1, 1, 2, 3, 3 }), 0), 1, { x = 40, y = 13 })
  for i = 1, Pick({ 0, 0, 1, 1, 2 }) do
    Actor.Create("dir_pillbox", 1, { x = 37 + 3 * i, y = 14 })
  end

  Mission.Timer("net", 9600, NameRead, "a5.timer.net")
  Trigger.OnEnter("logbook", 0, LogFound)
  Trigger.Every(10, CheckHatches)
  -- no base: the sim will not end the mission when the column dies (EverHadBase), so the script does
  Trigger.OnAllDestroyed(0, ColumnLost)
end

-- Strength follows the difficulty tier (1..5); tier 3 is the design strength. Unit counts only (D-061).
function Pick(sizes)
  return sizes[Mission.Difficulty()]
end

function Squad(conscripts, ashthrowers)
  local types = {}
  for i = 1, conscripts do types[#types + 1] = "dir_conscript" end
  for i = 1, ashthrowers do types[#types + 1] = "dir_ashthrower" end
  return types
end

-- a hatch is closed when no Directorate hatch stands in its zone, the pole is down when no Directorate mast stands (destroyed, or captured)
function CheckHatches()
  if state.over then return end
  if not state.pole and Actor.Count(1, "dir_mast") == 0 then PoleDown() end
  for i = 1, 3 do
    if not state.shut[i] and Actor.CountInZone("hatch" .. i, 1, "dir_hatch") == 0 then
      state.shut[i] = true
      state.closed = state.closed + 1
      HatchShut()
    end
  end
end

function HatchShut()
  if state.closed == 1 then
    Media.MessageKey("a5.hatch1")
    Dialogue.Say(SPEAKER, SPEAKER, "a5.radio.hatch1")
    Trigger.AfterDelay(300, Patrol)
  elseif state.closed == 2 then
    Media.MessageKey("a5.hatch2")
    Dialogue.Say(SPEAKER, SPEAKER, "a5.radio.hatch2")
    if not state.logged then
      Objective.Reveal("log")   -- the second closed hatch gives the bonus away
      Objective.SetTarget("log", "logbook")
    end
  else
    Objective.Complete("hatches")
    Media.MessageKey("a5.hatches")
    Dialogue.Say(SPEAKER, SPEAKER, "a5.radio.hatches")
  end
  for i = 1, 3 do
    if not state.shut[i] then Objective.SetTarget("hatches", HATCHES[i]) break end
  end
  CheckWin()
end

-- the foot patrol that comes down from the north edge toward the pole
function Patrol()
  if state.over then return end
  local types = Squad(Pick({ 0, 2, 3, 4, 6 }), Pick({ 0, 0, 0, 1, 2 }))
  if #types == 0 then return end   -- tier 1 has no patrol
  Media.MessageKey("a5.patrol")
  Dialogue.Say(SPEAKER, SPEAKER, "a5.radio.patrol")
  Reinforce.Edge(1, types, { x = 28 + Random.Next(10), y = 0 }, Map.Center("pole"))
end

function PoleDown()
  if state.over or state.pole then return end
  state.pole = true
  Objective.Complete("pole")
  Media.MessageKey("a5.pole")
  Dialogue.Say(SPEAKER, SPEAKER, "a5.radio.pole.down")
  CheckWin()
end

function CheckWin()
  if state.over or not state.pole or state.closed < 3 then return end
  state.over = true
  Mission.TimerCancel("net")
  Media.MessageKey("a5.win")
  Dialogue.Say(SPEAKER, SPEAKER, "a5.radio.win")
  Mission.Win(0)
end

function LogFound()
  if state.over or state.logged then return end
  state.logged = true
  Objective.Reveal("log")
  Objective.Complete("log")
  Media.MessageKey("a5.log")
  Dialogue.Say(SPEAKER, SPEAKER, "a5.radio.log")
end

function NameRead()
  if state.over then return end
  state.over = true
  if state.closed < 3 then Objective.Fail("hatches") end
  if not state.pole then Objective.Fail("pole") end
  Media.MessageKey("a5.lost.net")
  Mission.Lose(0)
end

function ColumnLost()
  if state.over then return end
  state.over = true
  if state.closed < 3 then Objective.Fail("hatches") end
  if not state.pole then Objective.Fail("pole") end
  Media.MessageKey("a5.lost.column")
  Mission.Lose(0)
end
