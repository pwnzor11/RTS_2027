-- Left Behind 05: Under One Banner (schema 2, S48 C-12, design 22 section 3.3). Hero on the radio: Jessup Calloway-Ross, war-captain;
-- the Speaker, Marrow Tindall, asks that the other bands' names stay on the flag. Build-and-destroy in two phases:
-- 1. the Two Fires and Long Haul hold on the north-west road (a Dynamo Rig and a Muster Rig), 2. Nobody's Daughter's well (the same).
-- Hidden bonus: take the well's Mother Rig whole (a Tinker enters it while it is hurt but not dead).
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Text: radio and messages come from locale.toml. Objective and timer labels are locale keys.
-- Pressure scales with Mission.Difficulty() (tier 1..5) by unit count and AI tier only; cash and vision never change (D-061).

local SPEAKER = "leader_lb_calloway_ross"
local TINDALL = "leader_lb_tindall"
local MUSTER_DELAY = 7200    -- ticks until the hold's garrison walks to the well if the hold still stands
local ANSWER_LAG = 600       -- ticks between the hold falling and the well's answer reaching the camp
local DEADLINE = 21600       -- ticks until the Gathering moves on without the banner

function OnInit()
  state.outpost = false
  state.mustered = false
  state.answered = false
  state.answerDown = false
  state.mother = false
  state.done = false
  state.rigGone = 0
  state.outTags = {}
  state.answerTags = {}

  Objective.Add("outpost", "l5.obj.outpost", true)
  Objective.Add("well", "l5.obj.well", true)
  Objective.AddHidden("mother", "l5.obj.mother", false)
  -- Objective targets (M11-05): where the HUD points.
  Objective.SetTarget("outpost", "outpost")
  Objective.SetTarget("well", "well")
  Objective.SetTarget("mother", "well_rig")
  Media.MessageKey("l5.start")
  Dialogue.Say(SPEAKER, SPEAKER, "l5.radio.start")
  Dialogue.Say(TINDALL, TINDALL, "l5.radio.tindall")

  Actor.CreateGroup(StartForce(), 0, { x = 12, y = 9 }, { formation = "box" })
  Camera.Focus("home", 160)

  -- the well is played by the built-in AI: it stays home to defend; the answer below is the pressure it sends (counts follow the tier)
  Player.KeepAlive(1, true)
  AI.SetTier(1, Mission.Difficulty())
  AI.Defend(1, "well")
  Actor.CreateGroup(Squad(Pick({ 2, 3, 4, 6, 8 }), 0, Pick({ 0, 0, 1, 2, 2 })), 1, { x = 11, y = 42 }, { formation = "box" })
  state.outTags = Actor.CreateGroup(Squad(Pick({ 3, 4, 5, 7, 9 }), Pick({ 0, 0, 1, 2, 3 }), 0), 2, { x = 18, y = 23 }, { formation = "box", tagPrefix = "hold" })

  Mission.Timer("muster", MUSTER_DELAY, MusterAtWell, "l5.timer.muster")
  Mission.Timer("gathering", DEADLINE, GatheringLeaves, "l5.timer.gathering")
  Trigger.AfterDelay(1800, HintRadio)
  Trigger.Every(20, CheckMission)
  Trigger.OnSeen("well_rig", 0, WellSeen)
end

-- Strength follows the difficulty tier (1..5); tier 3 is the design strength. Unit counts only (D-061).
function Pick(sizes)
  return sizes[Mission.Difficulty()]
end

-- The column that comes with the war-captain's word: no Lookout yet, so the slingers are all there are. One Tinker, for the bonus.
function StartForce()
  return { "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger", "lb_slinger",
           "lb_scavenger", "lb_scavenger", "lb_scavenger", "lb_scavenger", "lb_scavenger", "lb_scavenger",
           "lb_hook_runner", "lb_hook_runner", "lb_hook_runner", "lb_buggy", "lb_buggy", "lb_tinker" }
end

function Squad(scavengers, hookRunners, slingers)
  local types = {}
  for i = 1, scavengers do types[#types + 1] = "lb_scavenger" end
  for i = 1, hookRunners do types[#types + 1] = "lb_hook_runner" end
  for i = 1, slingers do types[#types + 1] = "lb_slinger" end
  return types
end

function HintRadio()
  if not state.outpost then Dialogue.Say(SPEAKER, SPEAKER, "l5.radio.hint") end
end

function WellSeen()
  if state.done then return end
  Dialogue.Say(TINDALL, TINDALL, "l5.radio.rig")
end

-- The hold's garrison gives up the road and walks to the well: Nobody's Daughter gets stronger the longer the hold stands.
function MusterAtWell()
  if state.done or state.outpost then return end
  state.mustered = true
  Media.MessageKey("l5.muster")
  Dialogue.Say(SPEAKER, SPEAKER, "l5.radio.muster")
  for i, tag in pairs(state.outTags) do
    if Actor.Alive(tag) then Actor.Order(tag, "attackmove", Map.Center("well")) end
  end
end

function OutpostBroken()
  state.outpost = true
  Objective.Complete("outpost")
  Mission.TimerCancel("muster")
  Media.MessageKey("l5.outpost.down")
  Dialogue.Say(SPEAKER, SPEAKER, "l5.radio.outpost")
  Dialogue.Say(TINDALL, TINDALL, "l5.radio.names")
  Trigger.AfterDelay(ANSWER_LAG, SendAnswer)
end

-- Nobody's Daughter answers: its army goes for the camp and a column comes up the west road behind it.
function SendAnswer()
  if state.done then return end
  state.answered = true
  AI.Wave(1, "answer", "home")
  local types = Squad(Pick({ 3, 4, 5, 7, 9 }), Pick({ 0, 1, 2, 2, 3 }), 0)
  state.answerTags = Reinforce.Edge(1, types, { x = 0, y = 34 }, Map.Center("home"))
  Media.MessageKey("l5.answer")
  Dialogue.Say(SPEAKER, SPEAKER, "l5.radio.answer")
end

function AnswerAlive()
  for i, tag in pairs(state.answerTags) do
    if Actor.Alive(tag) then return true end
  end
  return false
end

function AnswerBroken()
  state.answerDown = true
  Media.MessageKey("l5.answer.down")
  Dialogue.Say(SPEAKER, SPEAKER, "l5.radio.answerdown")
end

function WellBroken()
  Objective.Complete("well")
  Media.MessageKey("l5.well.down")
  Dialogue.Say(SPEAKER, SPEAKER, "l5.radio.well")
end

-- The Mother Rig flies our flag: it is in the well and it is ours. Taking it needs a Tinker and a rig that is hurt but not dead.
function MotherTaken()
  state.mother = true
  Objective.Reveal("mother")
  Objective.Complete("mother")
  Media.MessageKey("l5.mother")
  Dialogue.Say(TINDALL, TINDALL, "l5.radio.mother")
end

function CheckMission()
  if state.done then return end
  if Actor.Count(0, "lb_mother_rig") + Actor.CountPacked(0, "lb_mother_rig") == 0 then
    state.rigGone = state.rigGone + 1
    if state.rigGone >= 2 then return LoseMission("l5.lost.rig") end
  else
    state.rigGone = 0
  end
  if not state.outpost and not Actor.Alive("out_dyn") and not Actor.Alive("out_must") then OutpostBroken() end
  if state.answered and not state.answerDown and not AnswerAlive() then AnswerBroken() end
  if Objective.Status("well") == "active" and not Actor.Alive("well_dyn") and not Actor.Alive("well_must") then WellBroken() end
  if not state.mother and Actor.CountInZone("well", 0, "lb_mother_rig") >= 1 then MotherTaken() end
  if Objective.Status("outpost") == "completed" and Objective.Status("well") == "completed" then WinMission() end
end

function GatheringLeaves()
  if state.done then return end
  LoseMission("l5.lost.time")
end

function LoseMission(key)
  state.done = true
  if Objective.Status("outpost") == "active" then Objective.Fail("outpost") else Objective.Fail("well") end
  Media.MessageKey(key)
  Mission.Lose(0)
end

function WinMission()
  state.done = true
  Media.MessageKey("l5.win")
  Dialogue.Say(SPEAKER, SPEAKER, "l5.radio.win")
  Dialogue.Say(TINDALL, TINDALL, "l5.radio.winnames")
  Camera.Focus("well", 120)
  Mission.Win(0)
end
