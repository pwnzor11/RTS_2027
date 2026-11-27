-- Authority 07: The Defector (schema 2, S47 pilot, design 22 section 6). Hero on the radio: Brigadier Wrenfield; Pell by teletype;
-- Lieutenant Dray (the defector) and Thrall (enemy net) each speak a line or two. No base: the column is all there is.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Shape: reach the spoil heaps before a visible rendezvous timer ends, pick Dray up, bring him home alive while Shaft Four hunts for him.

local SPEAKER = "leader_auth_wrenfield"
local PELL = "leader_auth_pell"
local DRAY = "leader_auth_dray"
local THRALL = "leader_dir_thrall"

function OnInit()
  state.found = false
  state.alerted = false
  state.over = false
  state.logged = false

  Objective.Add("reach", "a7.obj.reach", true)
  Objective.AddHidden("log", "a7.obj.log", false)
  Objective.SetTarget("reach", "spoil")

  Media.MessageKey("a7.start")
  Dialogue.Say(PELL, PELL, "a7.radio.pell")
  Dialogue.Say(SPEAKER, SPEAKER, "a7.radio.start")
  Camera.Focus("spoil", 120)

  -- the Directorate stays alive for the whole mission (Player.KeepAlive, S47), whatever happens to its base
  Player.KeepAlive(1, true)
  -- the base stays home; the scripted groups and the alert wave below are the pressure (counts follow the tier, D-061)
  AI.SetTier(1, Mission.Difficulty())
  AI.Defend(1, "enemy_base")

  -- the bridge picket at the south end of the short bridge, and the guard on the spoil heaps
  for i = 1, Pick({ 0, 1, 1, 2, 2 }) do
    Actor.Create("dir_pillbox", 1, { x = 45 + 3 * i, y = 22 })
  end
  Actor.CreateGroup(Squad(Pick({ 2, 3, 4, 6, 8 }), Pick({ 0, 0, 1, 1, 2 }), 0), 1, { x = 47, y = 23 })
  Actor.CreateGroup(Squad(Pick({ 1, 2, 3, 4, 5 }), 0, 0), 1, Actor.Cell("spoil_hatch"))

  Mission.Timer("rendezvous", 4800, RendezvousMissed, "a7.timer.rendezvous")
  Trigger.OnEnter("spoil", 0, DrayFound)
  Trigger.OnEnter("culvert", 0, LogFound)
  -- no base: the sim will not end the mission when the column dies (EverHadBase), so the script does
  Trigger.OnAllDestroyed(0, ColumnLost)
end

-- Strength follows the difficulty tier (1..5); tier 3 is the design strength. Unit counts only (D-061).
function Pick(sizes)
  return sizes[Mission.Difficulty()]
end

function Squad(conscripts, ashthrowers, sappers)
  local types = {}
  for i = 1, conscripts do types[#types + 1] = "dir_conscript" end
  for i = 1, ashthrowers do types[#types + 1] = "dir_ashthrower" end
  for i = 1, sappers do types[#types + 1] = "dir_sapper" end
  return types
end

function Hunters()
  return Squad(Pick({ 3, 4, 6, 8, 10 }), Pick({ 0, 1, 1, 2, 3 }), 0)
end

function InZone(cell, name)
  local z = Map.Zone(name)
  return cell.x >= z.x0 and cell.x <= z.x1 and cell.y >= z.y0 and cell.y <= z.y1
end

-- where the hunters head: Dray's cell (a rider counts), or the long bridge if it is not known
function DrayCell()
  local c = Actor.Cell("dray")
  if c == nil then return Map.Center("bridge_sw") end
  return c
end

function DrayFound()
  if state.over or state.found then return end
  state.found = true
  Actor.Create("auth_engineer", 0, Map.Center("spoil"), "dray")
  Objective.Complete("reach")
  Mission.TimerCancel("rendezvous")
  Objective.Add("bring", "a7.obj.bring", true)
  Objective.SetTarget("bring", "home")
  Media.MessageKey("a7.found")
  Dialogue.Say(DRAY, DRAY, "a7.radio.dray")
  Dialogue.Say(SPEAKER, SPEAKER, "a7.radio.found")
  Objective.Reveal("log")
  Objective.SetTarget("log", "culvert")
  Dialogue.Say(DRAY, DRAY, "a7.radio.log")
  Trigger.OnDestroyed("dray", DrayLost)
  Mission.Timer("alert", 600, ShaftAlert, "a7.timer.alert")
  Trigger.Every(10, CheckHome)
end

function ShaftAlert()
  if state.over then return end
  state.alerted = true
  Media.MessageKey("a7.alert")
  Dialogue.Say(THRALL, THRALL, "a7.radio.thrall")
  Dialogue.Say(SPEAKER, SPEAKER, "a7.radio.alert")
  -- the base's whole army goes for the short bridge (its size follows the AI tier), and hunters close it hard
  AI.Wave(1, "alert", "bridge_ne")
  Reinforce.Zone(1, Hunters(), "ambush_ne", Map.Center("bridge_ne"))
  Trigger.AfterDelay(1200, HuntEast)
  Trigger.AfterDelay(2400, HuntSouth)
end

function HuntEast()
  if state.over then return end
  Media.MessageKey("a7.hunt1")
  Dialogue.Say(SPEAKER, SPEAKER, "a7.radio.hunt1")
  Reinforce.Edge(1, Squad(Pick({ 2, 3, 4, 6, 8 }), 0, Pick({ 0, 0, 1, 2, 2 })), { x = 63, y = 26 + Random.Next(9) }, DrayCell())
end

function HuntSouth()
  if state.over then return end
  local types = Squad(Pick({ 0, 2, 3, 5, 7 }), Pick({ 0, 0, 1, 1, 2 }), 0)
  if #types == 0 then return end   -- tier 1 has no southern hunt
  Media.MessageKey("a7.hunt2")
  Dialogue.Say(SPEAKER, SPEAKER, "a7.radio.hunt2")
  Reinforce.Edge(1, types, { x = 30 + Random.Next(11), y = 63 }, DrayCell())
end

-- Dray's cell follows the carrier while he rides (Actor.Cell reads the rider's synced position), so this wins either way.
function CheckHome()
  if state.over then return end
  local c = Actor.Cell("dray")
  if c ~= nil and InZone(c, "home") then
    state.over = true
    Objective.Complete("bring")
    Media.MessageKey("a7.win")
    Dialogue.Say(SPEAKER, SPEAKER, "a7.radio.win")
    Camera.Focus("home", 120)
    Mission.Win(0)
  end
end

function LogFound()
  if state.over or state.logged then return end
  state.logged = true
  if Objective.Status("log") == "active" then
    Objective.Reveal("log")
    Objective.Complete("log")
  end
  Media.MessageKey("a7.log")
  if state.found then Dialogue.Say(DRAY, DRAY, "a7.radio.thanks") end
end

function DrayLost()
  if state.over then return end
  state.over = true
  Objective.Fail("bring")
  Media.MessageKey("a7.lost.dray")
  Mission.Lose(0)
end

function RendezvousMissed()
  if state.over or state.found then return end
  state.over = true
  Objective.Fail("reach")
  Media.MessageKey("a7.missed")
  Mission.Lose(0)
end

function ColumnLost()
  if state.over then return end
  state.over = true
  if state.found then Objective.Fail("bring") else Objective.Fail("reach") end
  Media.MessageKey("a7.lost.column")
  Mission.Lose(0)
end
