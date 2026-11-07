-- Left Behind 03: Hatch Field (schema 2, M8-12). Hero on the radio: Jessup Calloway-Ross, war-captain.
-- Cameo: Commissar Ignatius Thrall (leader_dir_thrall) on the Directorate net.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Text: radio and messages come from locale.toml. Objective and timer labels are locale keys.
-- Pressure scales with Mission.Difficulty() (tier 1..5) by unit count only; cash and vision never change (D-061).

local SPEAKER = "leader_lb_calloway_ross"
local THRALL = "leader_dir_thrall"
local COLUMN_DELAY = 4800   -- ticks until the Authority column reaches the camp
local WAVE_FIRST = 2400     -- ticks until the first hatch wave
local WAVE_GAP = 1500       -- ticks between hatch waves
local WAVE_LIMIT = 8        -- waves after which the hatches have overrun the camp

function OnInit()
  state.waves = 0
  state.hatchesDown = 0
  state.column = false
  state.columnTags = {}
  state.waveTags = {}
  state.seen = false
  state.mast = false
  state.done = false
  state.rigGone = 0

  Objective.Add("vent", "lb3.obj.vent", true)
  Objective.Add("hatches", "lb3.obj.hatches", true)
  Objective.Add("column", "lb3.obj.column", true)
  Objective.AddHidden("mast", "lb3.obj.mast", false)
  Media.MessageKey("lb3.start")
  Dialogue.Say(SPEAKER, SPEAKER, "lb3.radio.start")

  Actor.CreateGroup(StartForce(), 0, { x = 23, y = 26 }, { formation = "box" })
  Camera.Focus("camp", 160)
  ReinforceField()

  Mission.Timer("column", COLUMN_DELAY, SendColumn, "lb3.timer.column")
  Trigger.AfterDelay(WAVE_FIRST, HatchWave)
  Trigger.AfterDelay(1500, HintRadio)
  Trigger.Every(20, CheckMission)
  Trigger.OnDestroyed("vent", VentDown)
  Trigger.OnDestroyed("hatch1", HatchDown)
  Trigger.OnDestroyed("hatch2", HatchDown)
  Trigger.OnDestroyed("hatch3", HatchDown)
  Trigger.OnDestroyed("mast", MastDown)
  Trigger.OnSeen("vent", 0, FieldSeen)
end

function StartForce()
  return { "lb_harpoon_wagon", "lb_harpoon_wagon", "lb_harpoon_wagon", "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger",
           "lb_slinger", "lb_slinger", "lb_scavenger", "lb_scavenger", "lb_scavenger", "lb_scavenger", "lb_scavenger", "lb_scavenger",
           "lb_hook_runner", "lb_hook_runner", "lb_hook_runner", "lb_drifter_brute", "lb_drifter_brute", "lb_tinker",
           "lb_buggy", "lb_buggy", "lb_buggy" }
end

-- Extra conscripts guarding the vent by tier: 0,0,1,2,3.
function ReinforceField()
  local extra = { 0, 0, 1, 2, 3 }
  local n = extra[Mission.Difficulty()]
  if n > 0 then
    local types = {}
    for i = 1, n do types[i] = "dir_conscript" end
    Actor.CreateGroup(types, 2, { x = 57, y = 40 }, { formation = "line" })
  end
end

-- Hatch squads: conscripts per live hatch per wave, 1,1,2,2,3 plus one more every third wave.
function WaveTypes(hatches)
  local base = { 1, 1, 2, 2, 3 }
  local n = base[Mission.Difficulty()]
  if state.waves >= 3 then n = n + 1 end
  if state.waves >= 6 then n = n + 1 end
  local types = {}
  for i = 1, n * hatches do types[i] = "dir_conscript" end
  return types
end

-- Authority column by tier: riflemen 4,5,6,7,8, jeeps 1,1,2,2,3, a tank from tier 4.
function ColumnTypes()
  local rifles = { 4, 5, 6, 7, 8 }
  local jeeps = { 1, 1, 2, 2, 3 }
  local tier = Mission.Difficulty()
  local types = {}
  for i = 1, rifles[tier] do types[i] = "auth_rifleman" end
  for i = 1, jeeps[tier] do types[#types + 1] = "auth_jeep" end
  if tier >= 4 then types[#types + 1] = "auth_tank" end
  return types
end

function HintRadio()
  if not state.seen and Objective.Status("vent") == "active" then Dialogue.Say(SPEAKER, SPEAKER, "lb3.radio.hint") end
end

function FieldSeen()
  if state.seen or state.done then return end
  state.seen = true
  Dialogue.Say(SPEAKER, SPEAKER, "lb3.radio.seen")
end

function HatchesAlive()
  local n = 0
  if Actor.Alive("hatch1") then n = n + 1 end
  if Actor.Alive("hatch2") then n = n + 1 end
  if Actor.Alive("hatch3") then n = n + 1 end
  return n
end

function HatchWave()
  if state.done then return end
  local alive = HatchesAlive()
  if alive == 0 then return end
  if state.waves >= WAVE_LIMIT then return LoseMission("lb3.lost.overrun", "hatches") end
  local tags = Reinforce.Zone(2, WaveTypes(alive), "mouth", Map.Center("camp"))
  for i, tag in pairs(tags) do state.waveTags[#state.waveTags + 1] = tag end
  state.waves = state.waves + 1
  Media.MessageKey("lb3.wave")
  if state.waves == 1 then Dialogue.Say(THRALL, THRALL, "lb3.radio.thrall") end
  if state.waves == 2 then Dialogue.Say(SPEAKER, SPEAKER, "lb3.radio.wave") end
  if state.waves == WAVE_LIMIT - 2 then Dialogue.Say(SPEAKER, SPEAKER, "lb3.radio.warn") end
  Trigger.AfterDelay(WAVE_GAP, HatchWave)
end

function VentDown()
  if state.done or Objective.Status("vent") ~= "active" then return end
  Objective.Complete("vent")
  Media.MessageKey("lb3.vent.down")
  Dialogue.Say(SPEAKER, SPEAKER, "lb3.radio.vent")
  Camera.Focus("vent", 120)
end

function HatchDown()
  if state.done then return end
  state.hatchesDown = state.hatchesDown + 1
  Media.MessageKey("lb3.hatch.down")
  if state.hatchesDown >= 3 then
    Objective.Complete("hatches")
    Dialogue.Say(SPEAKER, SPEAKER, "lb3.radio.hatches")
    Dialogue.Say(THRALL, THRALL, "lb3.radio.thrallout")
  end
end

function MastDown()
  if state.mast or state.done then return end
  state.mast = true
  Objective.Reveal("mast")
  Objective.Complete("mast")
  Player.AddCash(0, 500)
  Media.MessageKey("lb3.mast")
  Dialogue.Say(SPEAKER, SPEAKER, "lb3.radio.mast")
end

function SendColumn()
  if state.column or state.done then return end
  state.column = true
  local tags = Reinforce.Zone(1, ColumnTypes(), "north", Map.Center("camp"))
  for i, tag in pairs(tags) do state.columnTags[#state.columnTags + 1] = tag end
  Media.MessageKey("lb3.column")
  Dialogue.Say(SPEAKER, SPEAKER, "lb3.radio.column")
  Camera.Focus(tags[1], 120)
end

function ColumnAlive()
  for i, tag in pairs(state.columnTags) do
    if Actor.Alive(tag) then return true end
  end
  return false
end

function CheckMission()
  if state.done then return end
  if Actor.Count(0, "lb_mother_rig") + Actor.Count(0, "lb_mother_rig_packed") == 0 then
    state.rigGone = state.rigGone + 1
    if state.rigGone >= 2 then return LoseMission("lb3.lost.rig", "column") end
  else
    state.rigGone = 0
  end
  if Objective.Status("column") == "active" and state.column and not ColumnAlive() then
    Objective.Complete("column")
    Media.MessageKey("lb3.column.down")
    Dialogue.Say(SPEAKER, SPEAKER, "lb3.radio.columndown")
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
  Media.MessageKey("lb3.win")
  Dialogue.Say(SPEAKER, SPEAKER, "lb3.radio.win")
  Mission.Win(0)
end
