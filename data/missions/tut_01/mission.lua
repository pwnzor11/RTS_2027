-- Tutorial 01: Basic Training (M8-09). Shared by every faction's menu; the player learns on Authority units.
-- Seven lessons, one objective each, revealed in order. There is no timer and no Mission.Lose: the player can only be
-- beaten by losing everything (the sim ends the game then). Rules of this sandbox: integers only, callbacks are TOP-LEVEL
-- named functions, every piece of script data lives in the global `state` table (saved and hashed with the game).
--
-- Steps: 1 move, 2 attack, 3 power, 4 barracks, 5 train, 6 harvest, 7 fight.

function OnInit()
  state.step = 1
  state.riflemen = 0
  state.loading = false

  Objective.Add("move", "Select your riflemen and move them to the marked ground", true)
  Objective.AddHidden("attack", "Attack the enemy power plant", true)
  Objective.AddHidden("power", "Build a Power Plant", true)
  Objective.AddHidden("barracks", "Build a Barracks", true)
  Objective.AddHidden("train", "Train a Rifleman", true)
  Objective.AddHidden("harvest", "Build a Refinery and collect pitchblende", true)
  Objective.AddHidden("fight", "Destroy the enemy camp", true)

  Say("intro")
  Say("step.move")
  Camera.Focus("rally", 200)

  Trigger.Every(20, Poll)
  Trigger.Every(1800, Hint)
end

-- Speaks one instruction line. The speaker id "instructor" is deliberately unnamed.
function Say(key)
  Dialogue.Say("instructor", "instructor", key)
end

function StepId(n)
  if n == 1 then return "move" end
  if n == 2 then return "attack" end
  if n == 3 then return "power" end
  if n == 4 then return "barracks" end
  if n == 5 then return "train" end
  if n == 6 then return "harvest" end
  return "fight"
end

-- A repeating, harmless reminder for the current lesson (not a fail timer).
function Hint()
  if state.step <= 7 then
    Say("hint." .. StepId(state.step))
  end
end

-- Completes the current lesson and starts the next one, or wins after the last.
function Advance()
  Objective.Complete(StepId(state.step))
  state.step = state.step + 1
  if state.step > 7 then
    Say("done")
    Media.MessageKey("msg.done")
    Mission.Win(0)
    return
  end
  Objective.Reveal(StepId(state.step))
  Say("step." .. StepId(state.step))
  StartStep(state.step)
end

-- One-off set-up when a lesson begins.
function StartStep(n)
  if n == 2 then
    Camera.Focus("dummy", 200)
  elseif n == 5 then
    state.riflemen = Actor.Count(0, "auth_rifleman")
  elseif n == 7 then
    Camera.Focus("camp", 200)
  end
end

function Poll()
  local n = state.step
  if n == 1 then CheckMove()
  elseif n == 2 then CheckAttack()
  elseif n == 3 then CheckPower()
  elseif n == 4 then CheckBarracks()
  elseif n == 5 then CheckTrain()
  elseif n == 6 then CheckHarvest()
  elseif n == 7 then CheckFight()
  end
end

-- 1. Move: any soldier on the rally ground.
function CheckMove()
  if Actor.CountInZone("rally", 0) >= 1 then Advance() end
end

-- 2. Attack: the practice power plant is gone.
function CheckAttack()
  if not Actor.Alive("dummy") then Advance() end
end

-- 3. Power Plant built.
function CheckPower()
  if Actor.Count(0, "auth_power") >= 1 then Advance() end
end

-- 4. Barracks built.
function CheckBarracks()
  if Actor.Count(0, "auth_barracks") >= 1 then Advance() end
end

-- 5. A new rifleman beyond the squad we started the lesson with.
function CheckTrain()
  if Actor.Count(0, "auth_rifleman") > state.riflemen then Advance() end
end

-- 6. Refinery up, then the free Scraper works the pitchblende patch and drives out of it again (it only leaves when its hold is
-- full and it heads for the refinery). The script cannot read stored credits, so the Scraper's trip is the signal.
function CheckHarvest()
  if Actor.Count(0, "auth_refinery") < 1 then return end
  if Actor.CountInZone("pit", 0, "auth_scraper") >= 1 then
    state.loading = true
  elseif state.loading and Actor.Count(0, "auth_scraper") >= 1 then
    Advance()
  end
end

-- 7. The camp is gone: its three riflemen and its power plant. (The far south-east outpost "reserve" stays standing on purpose,
-- so the enemy is never wiped out and the sim never ends the game before the last lesson is done.)
function CheckFight()
  if Actor.Alive("foe1") or Actor.Alive("foe2") or Actor.Alive("foe3") or Actor.Alive("camp_power") then return end
  Advance()
end
