-- Expedition 05: The South Coast (schema 2, S48 C-12 wave 1, design 22 section 3.4). Voice on the radio: Commissioner Strand (leader_exp_strand) by telex;
-- Admiral Aldersey speaks one line (he wants the log unread). No base: the party is all there is.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Shape: find the wreck of the 1997 relief ship, drive the scavengers off it, take the signal log aboard the Survey Hover, run it out to open sea before the tide turns.

local SPEAKER = "leader_exp_strand"
local ALDERSEY = "leader_exp_aldersey"

function OnInit()
  state.found = false
  state.cleared = false
  state.logged = false
  state.over = false
  state.board = false

  Objective.Add("wreck", "e5.obj.wreck", true)
  Objective.Add("log", "e5.obj.log", true)
  Objective.Add("out", "e5.obj.out", true)
  Objective.AddHidden("board", "e5.obj.board", false)
  Objective.SetTarget("wreck", "relief_wreck")
  Objective.SetTarget("log", "relief_wreck")
  Objective.SetTarget("out", "sea_s")

  Media.MessageKey("e5.start")
  Dialogue.Say(SPEAKER, SPEAKER, "e5.radio.start")
  Camera.Focus("relief_wreck", 120)

  -- the scavengers stay a team for the whole mission (Player.KeepAlive, S47), whatever happens to their units
  Player.KeepAlive(1, true)
  -- the guard on the wreck, a picket at the evacuation platform (counts follow the tier, D-061)
  Actor.CreateGroup(Squad(Pick({ 5, 7, 9, 12, 15 }), 0, Pick({ 0, 1, 2, 3, 4 })), 1, { x = 33, y = 49 })
  Actor.CreateGroup(Squad(Pick({ 2, 2, 3, 4, 5 }), 0, 0), 1, { x = 47, y = 52 })

  Mission.Timer("tide", 4800, TideTurned, "e5.timer.tide")
  Trigger.OnEnter("relief_wreck", 0, WreckFound)
  Trigger.OnEnter("platform", 0, BoardFound)
  Trigger.OnDestroyed("survey", SurveyLost)
  -- no base: the sim will not end the mission when the party dies (EverHadBase), so the script does
  Trigger.OnAllDestroyed(0, PartyLost)
  Trigger.AfterDelay(1800, BandOne)
  Trigger.AfterDelay(3200, BandTwo)
  Trigger.Every(10, Poll)
end

-- Strength follows the difficulty tier (1..5); tier 3 is the design strength. Unit counts only (D-061).
function Pick(sizes)
  return sizes[Mission.Difficulty()]
end

function Squad(scavengers, runners, slingers)
  local types = {}
  for i = 1, scavengers do types[#types + 1] = "lb_scavenger" end
  for i = 1, runners do types[#types + 1] = "lb_hook_runner" end
  for i = 1, slingers do types[#types + 1] = "lb_slinger" end
  return types
end

function InZone(cell, name)
  local z = Map.Zone(name)
  return cell.x >= z.x0 and cell.x <= z.x1 and cell.y >= z.y0 and cell.y <= z.y1
end

function WreckFound()
  if state.over or state.found then return end
  state.found = true
  Objective.Complete("wreck")
  Objective.Reveal("board")
  Objective.SetTarget("board", "platform")
  Media.MessageKey("e5.found")
  Dialogue.Say(SPEAKER, SPEAKER, "e5.radio.found")
  Dialogue.Say(ALDERSEY, ALDERSEY, "e5.radio.aldersey")
end

function BoardFound()
  if state.over or state.board then return end
  state.board = true
  Objective.Reveal("board")
  Objective.Complete("board")
  Media.MessageKey("e5.board")
  Dialogue.Say(SPEAKER, SPEAKER, "e5.radio.board")
end

function BandOne()
  if state.over then return end
  Media.MessageKey("e5.band")
  Dialogue.Say(SPEAKER, SPEAKER, "e5.radio.band")
  Reinforce.Edge(1, Squad(Pick({ 3, 4, 6, 8, 10 }), Pick({ 0, 1, 2, 3, 4 }), 0), { x = 63, y = 30 + Random.Next(9) }, { x = 33, y = 49 })
end

function BandTwo()
  if state.over then return end
  Media.MessageKey("e5.band2")
  Dialogue.Say(SPEAKER, SPEAKER, "e5.radio.band2")
  Reinforce.Edge(1, Squad(Pick({ 2, 3, 4, 6, 8 }), Pick({ 0, 0, 1, 2, 3 }), Pick({ 0, 0, 1, 1, 2 })), { x = 63, y = 12 + Random.Next(9) }, { x = 33, y = 49 })
end

-- One poll covers the three steps: the wreck is cleared, the log is taken, the log reaches open sea.
function Poll()
  if state.over then return end
  if state.found and not state.cleared and Actor.CountInZone("relief_wreck", 1) == 0 then
    state.cleared = true
    Media.MessageKey("e5.cleared")
    Dialogue.Say(SPEAKER, SPEAKER, "e5.radio.cleared")
  end
  if not Actor.Alive("survey") then return end
  local c = Actor.Cell("survey")
  if c == nil then return end
  if state.cleared and not state.logged and InZone(c, "relief_wreck") then
    state.logged = true
    Objective.Complete("log")
    Media.MessageKey("e5.logged")
    Dialogue.Say(SPEAKER, SPEAKER, "e5.radio.logged")
    Reinforce.Edge(1, Squad(0, Pick({ 2, 3, 4, 6, 8 }), 0), { x = 63, y = 30 + Random.Next(9) }, c)
  end
  -- open sea: the deep water south of the shallows, where the launch waits
  if state.logged and InZone(c, "sea_s") and c.y >= 58 then
    state.over = true
    Objective.Complete("out")
    Media.MessageKey("e5.win")
    Dialogue.Say(SPEAKER, SPEAKER, "e5.radio.win")
    Mission.TimerCancel("tide")
    Mission.Win(0)
  end
end

function Fail(key)
  state.over = true
  Mission.TimerCancel("tide")
  if state.logged then Objective.Fail("out") else Objective.Fail("log") end
  Media.MessageKey(key)
  Mission.Lose(0)
end

function SurveyLost()
  if state.over then return end
  Fail("e5.lost.survey")
end

function TideTurned()
  if state.over then return end
  Fail("e5.lost.tide")
end

function PartyLost()
  if state.over then return end
  Fail("e5.lost.party")
end
