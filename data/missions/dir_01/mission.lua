-- Directorate 01: The First Hatch (schema 2, M8-10 D1). Hero on the radio: the Voice (Odette Larkin).
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Text: radio and messages come from locale.toml. Objective and timer labels are locale keys.

local SPEAKER = "leader_dir_larkin"
local NEEDED = 4

function OnInit()
  state.phase = "dig"
  state.waves = 0
  state.waveTags = {}

  Objective.Add("hatch", "d1.obj.hatch", true)
  Objective.Add("muster", "d1.obj.muster", true)
  Objective.AddHidden("picket", "d1.obj.picket", false)
  -- Objective targets (M11-05): where the HUD points.
  Objective.SetTarget("hatch", "site")
  Objective.SetTarget("muster", "site")
  Objective.SetTarget("picket", "picket")
  Media.MessageKey("d1.start")
  Dialogue.Say(SPEAKER, SPEAKER, "d1.radio.start")
  Camera.Focus("shaft", 120)

  -- The enemy base stays home; the response comes only from the script (unit count by tier, D-061).
  AI.Defend(1, "site")

  Trigger.Every(20, CheckHatch)
  Trigger.Every(40, CheckMuster)
  Trigger.OnDestroyed("shaft", ShaftLost)
  Trigger.OnDestroyed("pk1", PicketDown)
  Trigger.OnDestroyed("pk2", PicketDown)
  Trigger.OnSeen("picket", 0, PicketSeen)
  Trigger.AfterDelay(500, DigRadio)
end

function DigRadio()
  if state.phase == "dig" then
    Dialogue.Say(SPEAKER, SPEAKER, "d1.radio.dig")
  end
end

function PicketSeen()
  if Objective.Status("picket") == "active" then
    Objective.Reveal("picket")
    Dialogue.Say(SPEAKER, SPEAKER, "d1.radio.picket")
  end
end

function PicketDown()
  if not Actor.Alive("pk1") and not Actor.Alive("pk2") and Objective.Status("picket") == "active" then
    Objective.Reveal("picket")
    Objective.Complete("picket")
    Media.MessageKey("d1.picket.down")
  end
end

function ShaftLost()
  if state.phase == "done" then return end
  Media.MessageKey("d1.lost")
  Mission.Lose(0)
end

-- A hatch (or the bore head it grows into) standing on the marked ground opens the site.
function SiteHatches()
  return Actor.CountInZone("site", 0, "dir_hatch") + Actor.CountInZone("site", 0, "dir_bore_head")
end

function CheckHatch()
  if state.phase == "dig" then
    if SiteHatches() >= 1 then
      state.phase = "hold"
      Objective.Complete("hatch")
      Objective.Add("hold", "d1.obj.hold", true)
      Objective.SetTarget("hold", "site")
      Media.MessageKey("d1.hatch.open")
      Dialogue.Say(SPEAKER, SPEAKER, "d1.radio.open")
      Camera.Focus("site", 120)
      Trigger.AfterDelay(300, WaveOne)
      Mission.Timer("wave2", 1500, WaveTwo, "d1.timer.wave2")
    end
  elseif state.phase == "hold" or state.phase == "clear" then
    if SiteHatches() < 1 and Objective.Status("hold") == "active" then
      Objective.Fail("hold")
      Media.MessageKey("d1.lost")
      Mission.Lose(0)
    end
  end
end

function CheckMuster()
  if state.phase == "done" then return end
  if Objective.Status("muster") == "active" and Actor.CountInZone("site", 0, "dir_conscript") >= NEEDED then
    Objective.Complete("muster")
    Dialogue.Say(SPEAKER, SPEAKER, "d1.radio.muster")
    CheckWin()
  end
end

-- Response strength follows the difficulty tier (1..5); tier 3 is the base. Never cash or vision (D-061).
function WaveOneSize()
  local sizes = { 1, 2, 2, 3, 4 }
  return sizes[Mission.Difficulty()]
end

function WaveTwoSize()
  local sizes = { 2, 2, 3, 4, 5 }
  return sizes[Mission.Difficulty()]
end

function Wave(types, edge)
  local tags = Reinforce.Edge(1, types, edge, Map.Center("site"))
  for i, tag in pairs(tags) do
    state.waveTags[#state.waveTags + 1] = tag
  end
  state.waves = state.waves + 1
end

function WaveOne()
  Media.MessageKey("d1.wave1")
  Dialogue.Say(SPEAKER, SPEAKER, "d1.radio.wave1")
  local types = {}
  for i = 1, WaveOneSize() do types[i] = "auth_rifleman" end
  Camera.Focus("site", 200)
  Wave(types, { x = 63, y = 22 + Random.Next(6) })
end

function WaveTwo()
  Media.MessageKey("d1.wave2")
  local types = {}
  for i = 1, WaveTwoSize() do types[i] = "auth_rifleman" end
  if Mission.Difficulty() >= 4 then types[#types + 1] = "auth_jeep" end
  Wave(types, { x = 30 + Random.Next(4), y = 0 })
  Trigger.Every(40, CheckWaves)
end

function CheckWaves()
  if state.phase ~= "hold" or state.waves < 2 then return end
  for i, tag in pairs(state.waveTags) do
    if Actor.Alive(tag) then return end
  end
  state.phase = "clear"
  Objective.Complete("hold")
  Media.MessageKey("d1.response.beaten")
  Dialogue.Say(SPEAKER, SPEAKER, "d1.radio.beaten")
  CheckWin()
end

function CheckWin()
  if state.phase == "clear" and Mission.AllPrimaryDone() then
    state.phase = "done"
    if Objective.Status("picket") == "active" then Objective.Reveal("picket") end
    Media.MessageKey("d1.win")
    Dialogue.Say(SPEAKER, SPEAKER, "d1.radio.win")
    Mission.Win(0)
  end
end
