-- Expedition 03: The Fleet Reaches the Coast (schema 2, M8-12). Voice on the radio: Commissioner Strand (leader_exp_strand); cameos Fleet Admiral Aldersey, Air Marshal Kell and the Directorate's Voice (Larkin).
-- Rules of this sandbox: integers only, callbacks are TOP-LEVEL named functions, script data lives in the global `state`.
-- Flow: raise a Flight Deck and a SAM Mount -> the carrier group arrives by sea and anchors in the cove -> hold against two fronts (an Authority column from the north,
-- a Directorate column from the south dunes) and an Authority air raid. Enemy pressure scales by unit count only (D-061). Two far reserve posts keep the enemy teams alive.
-- Phases: wait (before the carrier group), cove (carrier anchored), done.

local SPEAKER = "leader_exp_strand"
local SEA_EDGE = { x = 0, y = 62 }
local COVE_SHORE = { x = 9, y = 46 }
local AIR_EDGE = { x = 0, y = 30 }
local HOLD = { x = 17, y = 45 }

function OnInit()
  state.phase = "wait"
  state.arrived = false
  state.deck = false
  state.waves = 0
  state.waveTags = {}
  state.fleet = {}
  state.third = false

  Objective.Add("deck", "e3.obj.deck", true)
  Objective.Add("carrier", "e3.obj.carrier", true)
  Objective.Add("hold", "e3.obj.hold", true)
  Objective.AddHidden("fleet", "e3.obj.fleet", false)
  -- Objective targets (M11-05): where the HUD points.
  Objective.SetTarget("deck", "cove_land")
  Objective.SetTarget("carrier", "cove")
  Objective.SetTarget("hold", "cove_land")
  Media.MessageKey("e3.start")
  Dialogue.Say(SPEAKER, SPEAKER, "e3.radio.start")
  Camera.Focus("cove", 120)

  Mission.Timer("fleet", 900, FleetArrives, "e3.timer.fleet")
  Trigger.AfterDelay(1800, AuthorityOne)
  Trigger.AfterDelay(3000, DirectorateOne)
  Trigger.AfterDelay(3800, AirRaid)
  Trigger.AfterDelay(5200, AuthorityTwo)
  Trigger.AfterDelay(6600, DirectorateTwo)
  if Mission.Difficulty() >= 5 then Trigger.AfterDelay(6400, AirStraggler) end
  if Mission.Difficulty() >= 4 then Trigger.AfterDelay(8400, ThirdWave) end
  Trigger.Every(20, Poll)
end

-- One poll covers every lose check, the objective checks and the win check.
function Poll()
  if state.phase == "done" then return end
  if Actor.Count(0, "exp_landing_depot") == 0 then
    Fail("e3.lost.depot")
    return
  end
  if state.arrived and Actor.Count(0, "exp_carrier") == 0 then
    Fail("e3.lost.carrier")
    return
  end
  CheckDeck()
  CheckCarrier()
  CheckHeld()
  CheckWin()
end

function Fail(key)
  state.phase = "done"
  Objective.Fail("hold")
  Media.MessageKey(key)
  Mission.Lose(0)
end

function CheckDeck()
  if state.deck then return end
  if Actor.Count(0, "exp_flight_deck") >= 1 and Actor.Count(0, "exp_sam") >= 1 then
    state.deck = true
    Objective.Complete("deck")
    Media.MessageKey("e3.deck")
    Dialogue.Say("leader_exp_kell", "leader_exp_kell", "e3.radio.kell")
  end
end

-- The carrier group: carrier and two destroyers sail in from the south-west edge and anchor in the cove.
function FleetArrives()
  if state.phase ~= "wait" then return end
  state.phase = "cove"
  state.arrived = true
  Media.MessageKey("e3.fleet")
  Dialogue.Say("leader_exp_aldersey", "leader_exp_aldersey", "e3.radio.aldersey")
  Camera.Focus("cove", 160)
  local tags = Reinforce.Naval(0, { "exp_carrier", "exp_destroyer", "exp_destroyer" }, SEA_EDGE, COVE_SHORE)
  state.fleet = tags
  Trigger.OnDestroyed(tags[2], DestroyerLost)
  Trigger.OnDestroyed(tags[3], DestroyerLost)
end

function DestroyerLost()
  if state.phase == "done" then return end
  if Objective.Status("fleet") == "active" then
    Objective.Fail("fleet")
    Media.MessageKey("e3.destroyer.lost")
    Dialogue.Say("leader_exp_aldersey", "leader_exp_aldersey", "e3.radio.destroyer")
  end
end

function CheckCarrier()
  if Objective.Status("carrier") ~= "active" or not state.arrived then return end
  if Actor.CountInZone("cove", 0, "exp_carrier") >= 1 then
    Objective.Complete("carrier")
    Objective.Reveal("fleet")
    Media.MessageKey("e3.carrier")
    Dialogue.Say(SPEAKER, SPEAKER, "e3.radio.carrier")
  end
end

-- Wave strength follows the difficulty tier (1..5); tier 3 is the baseline. Never cash or vision (D-061).
function Pick(sizes)
  return sizes[Mission.Difficulty()]
end

function Wave(player, kind, zone, size, extra, minTier)
  local types = {}
  for i = 1, size do types[i] = kind end
  if Mission.Difficulty() >= minTier then types[#types + 1] = extra end
  local tags = Reinforce.Zone(player, types, zone, HOLD)
  for i, tag in pairs(tags) do
    state.waveTags[#state.waveTags + 1] = tag
  end
  state.waves = state.waves + 1
end

function AuthorityOne()
  if state.phase == "done" then return end
  Media.MessageKey("e3.auth1")
  Dialogue.Say(SPEAKER, SPEAKER, "e3.radio.auth1")
  Camera.Focus("north_road", 120)
  Wave(1, "auth_rifleman", "north_road", Pick({ 2, 3, 4, 6, 8 }), "auth_jeep", 3)
end

function DirectorateOne()
  if state.phase == "done" then return end
  Media.MessageKey("e3.dir1")
  Dialogue.Say("leader_dir_larkin", "leader_dir_larkin", "e3.radio.larkin")
  Camera.Focus("south_dunes", 120)
  Wave(2, "dir_conscript", "south_dunes", Pick({ 3, 4, 5, 8, 11 }), "dir_ashthrower", 3)
end

-- The air raid: Authority bombers over the west sea, tier 4 and up (unit count only).
function AirRaid()
  if state.phase == "done" then return end
  local n = Pick({ 0, 0, 0, 2, 2 })
  if n == 0 then return end
  Media.MessageKey("e3.raid")
  Dialogue.Say("leader_exp_kell", "leader_exp_kell", "e3.radio.raid")
  local types = {}
  for i = 1, n do types[i] = "auth_bomber" end
  local tags = Reinforce.Edge(1, types, AIR_EDGE, Map.Center("cove"))
  for i, tag in pairs(tags) do
    state.waveTags[#state.waveTags + 1] = tag
  end
end

-- Tier 5: a second air sortie (three bombers from the south-west sea) follows the first pair (unit count only).
function AirStraggler()
  if state.phase == "done" then return end
  local tags = Reinforce.Edge(1, { "auth_bomber", "auth_bomber", "auth_bomber" }, { x = 0, y = 58 }, Map.Center("cove"))
  for i, tag in pairs(tags) do
    state.waveTags[#state.waveTags + 1] = tag
  end
end

function AuthorityTwo()
  if state.phase == "done" then return end
  Media.MessageKey("e3.auth2")
  Wave(1, "auth_rifleman", "north_road", Pick({ 3, 4, 6, 9, 12 }), "auth_rocketeer", 4)
end

function DirectorateTwo()
  if state.phase == "done" then return end
  Media.MessageKey("e3.dir2")
  Dialogue.Say(SPEAKER, SPEAKER, "e3.radio.last")
  Camera.Focus("south_dunes", 100)
  Wave(2, "dir_conscript", "south_dunes", Pick({ 4, 5, 7, 10, 14 }), "dir_ashthrower", 2)
  state.waves = 5
end

-- Tiers 4-5: a last combined push from both fronts after the second pair of waves.
function ThirdWave()
  if state.phase == "done" then return end
  Wave(1, "auth_rifleman", "north_road", Pick({ 0, 0, 0, 6, 9 }), "auth_rocketeer", 4)
  Wave(2, "dir_conscript", "south_dunes", Pick({ 0, 0, 0, 6, 9 }), "dir_ashthrower", 4)
  state.waves = 5
  state.third = true
end

function WavesDown()
  for i, tag in pairs(state.waveTags) do
    if Actor.Alive(tag) then return false end
  end
  return true
end

function CheckHeld()
  if Objective.Status("hold") ~= "active" then return end
  if state.waves < 5 or (Mission.Difficulty() >= 4 and not state.third) or not WavesDown() then return end
  Objective.Complete("hold")
  Media.MessageKey("e3.held")
end

function CheckWin()
  if not Mission.AllPrimaryDone() then return end
  state.phase = "done"
  if Objective.Status("fleet") == "active" then Objective.Complete("fleet") end
  Media.MessageKey("e3.win")
  Dialogue.Say(SPEAKER, SPEAKER, "e3.radio.win")
  Mission.Win(0)
end
