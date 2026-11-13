-- Directorate 03: The Launch Sites (schema 2, M8-12 D3). Hero on the radio: the Voice (Odette Larkin).
-- Cameos: Chief Surveyor Fenn (Authority) and Commissioner Strand (Expedition), as intercepted radio.
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- The sea wraps the map: all arrivals use Reinforce.Zone (land zones).

local SPEAKER = "leader_dir_larkin"
local FENN = "leader_auth_fenn"
local STRAND = "leader_exp_strand"

function OnInit()
  state.columns = 0
  state.colTags = {}
  state.alpha = false
  state.beta = false
  state.done = false

  Objective.Add("alpha", "d3.obj.alpha", true)
  Objective.Add("beta", "d3.obj.beta", true)
  Objective.Add("relief", "d3.obj.relief", true)
  Objective.AddHidden("ridge", "d3.obj.ridge", false)
  -- Objective targets (M11-05): where the HUD points.
  Objective.SetTarget("alpha", "alpha")
  Objective.SetTarget("beta", "beta")
  Objective.SetTarget("ridge", "ridge_post")
  Media.MessageKey("d3.start")
  Dialogue.Say(SPEAKER, SPEAKER, "d3.radio.start")
  Camera.Focus("camp", 120)

  -- The enemy base stays home; every pressure on the player comes from the script (unit count by tier, D-061).
  AI.Defend(1, "home")

  Trigger.OnDestroyed("shaft", ShaftLost)
  Trigger.OnDestroyed("pad_a", PadADown)
  Trigger.OnDestroyed("pad_b", PadBDown)
  Trigger.OnDestroyed("ridge_post", RidgeDown)
  Trigger.OnSeen("ridge_post", 0, RidgeSeen)
  Trigger.Every(40, CheckColumns)
  Mission.Timer("col1", 3600, ColumnOne, "d3.timer.col1")
  Trigger.AfterDelay(400, FennRadio)
end

function FennRadio()
  Dialogue.Say(FENN, FENN, "d3.radio.fenn")
end

function ShaftLost()
  if state.done then return end
  Media.MessageKey("d3.lost")
  Mission.Lose(0)
end

-- A pad falls when its mast is down; a citizen standing on the ground for 300 ticks secures it.
function PadADown()
  Media.MessageKey("d3.alphamast")
  Trigger.OnHold("alpha", 0, 300, AlphaHeld)
end

function PadBDown()
  Media.MessageKey("d3.betamast")
  Trigger.OnHold("beta", 0, 300, BetaHeld)
end

function AlphaHeld()
  state.alpha = true
  Objective.Complete("alpha")
  Media.MessageKey("d3.alpha.held")
  Dialogue.Say(SPEAKER, SPEAKER, "d3.radio.alpha")
  CheckWin()
end

function BetaHeld()
  state.beta = true
  Objective.Complete("beta")
  Media.MessageKey("d3.beta.held")
  Dialogue.Say(SPEAKER, SPEAKER, "d3.radio.beta")
  CheckWin()
end

-- Relief columns, by tier. Rifles; a jeep from tier 4. Column one goes to pad alpha, column two to pad beta.
function ColumnSize()
  local sizes = { 2, 3, 3, 4, 11 }
  return sizes[Mission.Difficulty()]
end

function Column(zone, dest)
  local types = {}
  for i = 1, ColumnSize() do types[i] = "auth_rifleman" end
  if Mission.Difficulty() >= 4 then types[#types + 1] = "auth_jeep" end
  local tags = Reinforce.Zone(1, types, zone, Map.Center(dest))
  for i, tag in pairs(tags) do
    state.colTags[#state.colTags + 1] = tag
  end
  state.columns = state.columns + 1
end

function ColumnOne()
  Media.MessageKey("d3.col1")
  Dialogue.Say(SPEAKER, SPEAKER, "d3.radio.col1")
  Column("spawn_s", "alpha")
  Camera.Focus("alpha", 160)
  Mission.Timer("col2", 1500, ColumnTwo, "d3.timer.col2")
end

function ColumnTwo()
  Media.MessageKey("d3.col2")
  Dialogue.Say(STRAND, STRAND, "d3.radio.strand")
  Column("spawn_e", "beta")
  Camera.Focus("beta", 160)
end

function CheckColumns()
  if state.columns < 2 or Objective.Status("relief") ~= "active" then return end
  for i, tag in pairs(state.colTags) do
    if Actor.Alive(tag) then return end
  end
  Objective.Complete("relief")
  Media.MessageKey("d3.relief.down")
  Dialogue.Say(SPEAKER, SPEAKER, "d3.radio.relief")
  CheckWin()
end

function RidgeSeen()
  if Objective.Status("ridge") == "active" then
    Objective.Reveal("ridge")
    Dialogue.Say(SPEAKER, SPEAKER, "d3.radio.ridge")
  end
end

function RidgeDown()
  if Objective.Status("ridge") == "active" then
    Objective.Reveal("ridge")
    Objective.Complete("ridge")
    Media.MessageKey("d3.ridge.down")
  end
end

function CheckWin()
  if not state.done and Mission.AllPrimaryDone() then
    state.done = true
    if Objective.Status("ridge") == "active" then Objective.Reveal("ridge") end
    Media.MessageKey("d3.win")
    Dialogue.Say(SPEAKER, SPEAKER, "d3.radio.win")
    Mission.Win(0)
  end
end
