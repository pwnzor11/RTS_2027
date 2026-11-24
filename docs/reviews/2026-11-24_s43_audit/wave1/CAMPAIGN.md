# CAMPAIGN — mission scripting and campaign data
Scope read: data/missions/*/{mission.toml,mission.lua,locale.toml} (all 17, briefings sampled), data/campaigns/*.toml (8), src/Scripting/MissionHost.{cs,Api,Objectives,Reinforce,Media}.cs, MissionApiContract.cs, src/Game/Campaign/{CampaignService,EndingDef}.cs (allow-list and ending wiring), src/Sim/Simulation.cs (SpawnVehicle/SpawnInfantry), Simulation.Production.cs (defeat rule), data/rules/{buildings,vehicles,infantry,terrain,game}.toml, data/maps/*.map.toml (decoded land/overlay layers), data/audio/voice (180 files), data/archive/leaders.toml, docs/reviews/2026-11-11_s18_audit/README.md + wave1/faction-ad.md. Scripts used are in /home/claude/audit43/tmp/chk*.py (read-only).

## Findings

### CAMPAIGN-01 · MED · bug
What: `Reinforce.Edge` (and the identical `SpawnEdgeBlock` used by `Reinforce.Delayed`) lays units out as a 3-wide block that is `count/3` rows deep, with no terrain, prop or occupancy check. `Reinforce.Zone` was fixed in S18 (MIS-07) to use `FreeCellNear`; Edge never was. Large Edge raids therefore spawn on blocking props, on buildings and far inside the map.
Where: src/Scripting/MissionHost.Api.cs:408-433 (`ReinforceEdge`), src/Scripting/MissionHost.Reinforce.cs:213-232 (`SpawnEdgeBlock`), src/Sim/Simulation.cs:227-243 (`SpawnVehicle`/`SpawnInfantry` only fail when the store is full). Callers: auth_01, auth_02, dir_01, dir_02, lb_01, lb_02.
Evidence: script — I reproduced the block geometry and decoded rustbelt_m2's land and overlay layers. Overlay 6 (Block) and 4 (Tree) lie along the north strip (e.g. cells (39,2) to (39,6), (44,2), (46,3) are `clear` land with a Block overlay). Counts of spawn cells on blocked terrain or props for the largest call at each tier:
- auth_02 tier 5 wave 1 (38 units): 4
- wave 2 (57): 9, plus 2 on start buildings
- wave 3 (29): up to 8
- wave 4 (74): 12
- tier 4 wave 2 (8 units): 2
- dir_01 tier 5 wave 1 (10): 3
- dir_02 tier 5 reserve (11): 3
- lb_02 tier 5 reprisal (9): 3
Depth is the second problem. auth_02 tier 5 wave 2 ends at (44,18), and 18 of its 57 cells lie inside the player's junction zone [35,13,44,23]. Wave 4 (74 units) has 17 cells inside the junction zone. These units appear inside the player's base, not at the map edge.
Impact: tier 5 (also a few cells at tier 3-4) of auth_02, dir_01, dir_02 and lb_02. Raiders start in the defended area, stacked on props or on buildings. What those units then do is a suspect (see below). The D-234 tier-5 sweeps measured this behaviour, so changing it moves those numbers.
Fix idea: build the Edge block with `FreeCellNear` (like `Reinforce.Zone`), or cap the depth and skip blocked cells.   Changes a match outcome / number / visible text?: yes (tier 5 raids, and a fixed-seed replay hash change).

### CAMPAIGN-02 · MED · data
What: s18 #10 / FAD-04 is fixed for Rocketeer, Scraper, Silo, Bunk Gallery, Still Rig and Hauler, but the Directorate Cistern and the Expedition Tank Farm, the storage buildings of the same pair, are in no campaign allow-list. s18's own note (wave1/faction-ad.md:103) said to add the cistern, and roster_directorate.md:45 flagged it "never in campaign". Authority gets `auth_silo` (1500) from auth_02. The Directorate has only the Smelter Vent (1000). The Expedition has only the Dredge Works (800).
Where: data/missions/dir_0{1..4}/mission.toml `tech`, exp_0{1..4}/mission.toml `tech` (no `dir_cistern`, no `exp_tank_farm`); data/rules/buildings.toml (`dir_cistern` storage 1500, `exp_tank_farm` storage 1200).
Evidence: script — a closure check of every mission's tech list against its faction roster shows both ids missing in all four missions. src/Sim/Missions/HarvestMission.cs:401 says `Stored += Math.Min(pay, space)  // the overflow is lost`.
Impact: Directorate and Expedition players lose harvested credits once stored funds reach the cap (1000 and 800). Authority and Left Behind (yard 3000) do not. It matters most in the long missions (dir_04, exp_03, exp_04).
Fix idea: add `dir_cistern` from dir_02 and `exp_tank_farm` from exp_02 to the allow-lists.   Changes a match outcome / number / visible text?: yes (campaign build options).

### CAMPAIGN-03 · MED · data
What: auth_04's allow-list contains `auth_airfield` (cost 2000), unlocked on auth_03 win, but neither aircraft that needs it (`auth_bomber`, `auth_interceptor`) is in any Authority mission's tech or `unlocks_on_win`. The Airfield can be built and produces nothing. s18 FAD-03 fixed the aircraft gate in `IsTypeAllowed` and listed "the Authority gets auth_airfield from auth_03 for nothing" (faction-ad.md:77), but the data side was never changed.
Where: data/missions/auth_04/mission.toml `tech`; auth_03/mission.toml `[unlocks_on_win]`.
Evidence: script — for auth_04, the airfield is present and `auth_bomber`/`auth_interceptor` (prerequisites = auth_airfield, vehicles.toml:289-303) are not. The Expedition (flight deck with jet, gunship, skyhook), Directorate (mooring with dirigible) and Left Behind (rotor pad with rotor, kite) all have their aircraft.
Impact: Authority players in the finale can spend 2000 on a dead building, and `auth_flak` has no purpose without enemy aircraft.
Fix idea: add `auth_interceptor` and/or `auth_bomber` to auth_04, or drop the airfield from the unlock.   Changes a match outcome / number / visible text?: yes.

### CAMPAIGN-04 · LOW · text
What: the lb_02 objective says "Raise a Garage Rig and a Lookout, then train slingers". The script completes `gate` as soon as one Garage Rig and one Lookout stand; training slingers is never checked (the player starts with 11 slingers anyway).
Where: data/missions/lb_02/locale.toml `"lb2.obj.gate"`; data/missions/lb_02/mission.lua CheckMission (`Actor.Count(0,"lb_garage_rig") >= 1 and Actor.Count(0,"lb_lookout") >= 1`).
Evidence: read.
Impact: the HUD text asks for more than the objective needs; harmless but misleading.
Fix idea: drop ", then train slingers" from the label, or add the check.   Changes a match outcome / number / visible text?: yes (text only).

### CAMPAIGN-05 · LOW · data
What: lb_03 tier 5 is easier than tier 4 in the Authority column's timing and rifleman count, against the code comment "sooner at the high tiers". `ColumnDelay` is {4800,4600,4400,3800,4300}, so tier 5 arrives 500 ticks later than tier 4. Riflemen are {5,6,7,8,7}, so tier 5 has fewer than tier 4. The only non-monotone tier tables in all 17 scripts are these two.
Where: data/missions/lb_03/mission.lua:58-62 and 92-101.
Evidence: script — a monotonicity scan of every 5-entry table in the scripts. The jeep and tank increase at tier 5 partly compensates, and the riflemen comment lists "5,6,7,8,7" explicitly, so it looks deliberate, but the `ColumnDelay` comment contradicts the data.
Impact: a tier-5 player gets a slightly softer column.
Fix idea: fix the comment, or the number if unintended.   Changes a match outcome / number / visible text?: no (comment) / yes (number).

### CAMPAIGN-06 · LOW · data
What: the mission AI's build strength follows the difficulty tier in three missions only: auth_04, exp_04 and lb_04 call `AI.SetTier(1, Mission.Difficulty())`. dir_04 is the same finale class (enemy `role = "ai"`, 4000 cash, header comment "The enemy base stays home and builds") and has no `AI.SetTier`. Its base builds at the default tier at all five difficulties. exp_02 (1200 cash) and dir_01-03 (1500 cash) behave the same way, but their AI only defends.
Where: data/missions/dir_04/mission.lua OnInit; data/missions/dir_04/mission.toml players[1].cash = 4000.
Evidence: script — grep of `SetTier` across the scripts.
Impact: dir_04 difficulty scales only through the scripted waves. This may be intended (D-061: unit count only), but it differs from the other three finales.
Fix idea: confirm intent; add `AI.SetTier(1, Mission.Difficulty())` if not.   Changes a match outcome / number / visible text?: yes.

### CAMPAIGN-07 · LOW · data
What: still broken from s18 (wave1/game-screens.md:80). `data/images/endings/` does not exist, so all 18 ending slides (4+4+5+5 across the four `*_ending.toml`) draw the dark fallback panel. EndingScreen.cs:97 looks for `<imagesDir>/<id>.png`, and CampaignFlow.cs:101/113 pass `data/images/endings`.
Where: data/campaigns/*_ending.toml `image =` keys; data/images/ (only `stills/`).
Evidence: script — `find` for `ending_*` returns nothing, and `ls data/images` shows `stills` only.
Impact: finishing a campaign shows text on black. The fallback is graceful (no crash).
Fix idea: ship the 18 stills, or hide the image panel and say so in the docs.   Changes a match outcome / number / visible text?: no.

### CAMPAIGN-08 · LOW · debt
What: dir_04 has zones and actor tags with the same names (`head`, `device`, `west_post`). `Objective.SetTarget`, `Camera.Focus`, `Map.Center` and `Trigger.OnSeen` resolve a zone first ("A zone name wins over a tag", MissionHost.Objectives.cs:~120). `OnDestroyed`, `Actor.Charged` and `Actor.DelayCharge` use the tag.
Where: data/missions/dir_04/mission.toml (zones.head/device/west_post vs tags "head"/"device"/"west_post").
Evidence: script — a set-intersection of zone and tag names across all missions; dir_04 is the only mission with a collision.
Impact: the HUD arrow and camera go to the zone centre, not the building. For the device that is (51,18) against a building at (50,17), so no visible harm today. `OnSeen("west_post")` is satisfied by seeing any part of the zone rather than the post.
Fix idea: rename the three zones.   Changes a match outcome / number / visible text?: no.

## Suspects (unproven)
- CAMPAIGN-01 follow-up: what a unit spawned on a Block/Tree overlay cell does. It may path out, or it may stay stuck and make "kill every raid unit" objectives (auth_01 hold, dir_01 hold, dir_02 reserve, lb_02 hold) unwinnable until the player walks up to it. Proof: a headless sim test that spawns an Edge block at tier 5 on auth_02 and runs 400 ticks, listing units whose position never changes. I could not run one (no .NET).
- tut_01 step 5 baseline: `state.riflemen = Actor.Count(0,"auth_rifleman")` is taken when step 5 starts. If the player loses a rifleman afterwards, "Train a Rifleman" needs two new ones (AI player 1 has cash 0 and no `AI.*` hook, so unlikely). Proof: a test that kills one rifleman at step 5.
- Objective arrows on moving enemy tags (dir_02 convoy lead, dir_03 relief column lead) use the tag's live cell every tick (`TrackActorTargets`), with no fog check. The commit message says "last cell seen", but the code tracks while alive. D-283 may make this intended guidance. Proof: ask the owner.
- auth_02 tier 5 sends 38 conscripts 300 ticks after the start (6 at tier 4); other tier-5 spikes are lighter. This is within D-234's 4-9 of 12 band, so it is only a UX note about the jump size.

## Checked and fine
- s18 #10 (campaign tech lists): fixed. Rocketeer, Scraper and Silo are in auth_02+, Bunk Gallery in dir_02+, Still Rig in lb_01+, Hauler in lb_02+, Ration Hall and Provision Store where needed. (Cistern, Tank Farm and the airfield gap are CAMPAIGN-02 and -03.)
- Every tech item's `prerequisites` are in the same mission's list or are the mission's start building (conyard, shaft, depot, packed rig); no unit needs a forbidden building. `[starts_with].tech` equals the human player's `tech` in all 17 missions. Every `unlocks_on_win` id appears in the next mission's tech.
- All actor types, tech ids and Lua type literals exist in the rules. All trigger, objective-target and camera tags and zones exist, and no `Destroyed` trigger can fire early on a missing tag. All objectives used are defined, and every defined objective has a `Complete` somewhere. Every timer cancel or lookup names a real timer, and every callback name exists.
- All zones and start actors are inside their maps. No start unit sits on impassable terrain or props, no building footprint overlaps another, and no tag is duplicated. Every `Reinforce.Zone` spawn zone is foot-passable (at worst 2/221 cells blocked) and big enough for the tier-5 counts; overflow is handled.
- Locale: every `Media.MessageKey`, `Dialogue.Say` key, objective and timer label resolves. Every `[voice]` path exists and starts with "OggS". There are no orphan voice files (180 of 180 referenced). Every speaker id exists in data/archive/leaders.toml (or is `instructor`), and each voice folder matches its speaker. The dialogue `portrait` argument is not rendered by the game (MissionDialoguePresenter has no portrait code), so no portrait files are missing.
- Campaign TOMLs: `requires` and `next` chains agree, `open_after` is ANY-of and consistent with D-state, all 12 stills exist, and every ending slide has caption text and valid conditions.
- Lose and defeat paths: the Command Ship, Depot, Shaft or Mother Rig loss is scripted in every mission. The sim defeat rule (UpdateDefeat) covers a player who ever had a base (including founders and packed rigs). Missions end only through MissionHost outcome or local defeat (MissionGame.IsOver), so razing the enemy base cannot end a mission early. auth_01 before the conyard airlift is covered by `ScraperLost`.
- Win logic reads sound for every mission: no objective is completable before it is added, `AllPrimaryDone` ordering is safe in dir_01, dir_02, dir_03, lb_01-04 and exp_02-04, and the hidden objectives are non-primary.
- Tier tables: all 5 entries in every `Pick` (no nil index at tier 1-5); `SetDifficulty` is range-checked and clamped at the campaign start.
- Sandbox hygiene: no floats, `math.random` or `os.*`/`io.*` in any script; callbacks are top-level; the `state` values are plain.
- Determinism spot-check: `Tech.List` iterates a `HashSet` of strings (insert and remove only, so the order is deterministic) but is unused by any shipped mission. `TrackActorTargets` is integer-only and is saved.
