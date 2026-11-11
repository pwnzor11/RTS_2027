# Objective targets for the campaign missions (M11-05, D-172)

`Objective.SetTarget(id, target)` gives an objective a place for the HUD to point at (edge arrow with distance, pulsing ground marker,
minimap ping, J / Tab). `target` is a zone name (the zone's middle cell), an actor tag (follows the actor while it lives; no target while
it is dead) or a cell table `{x=, y=}`. `Objective.ClearTarget(id)` removes it. See `docs/missions/api.md`.

This file is the plan for applying targets to the 16 missions; the mission scripts are NOT edited yet (another package is editing five of
them). `tut_01` is already done and is the working example. How to apply: in `OnInit`, right after the `Objective.Add` / `AddHidden`
line of the objective, add `Objective.SetTarget("<id>", "<target>")`. Every name below comes from the mission's own `mission.toml`: zones
exist at once, and the tags listed are pre-placed actors (they exist at tick 0). Where a script re-points an objective as the mission
moves on, call `Objective.SetTarget` again (it replaces the old target). A hidden objective may have a target before it is revealed; the
HUD ignores hidden objectives.

Notation: `zone:name` zone middle cell, `tag:name` follows the actor, `cell:(x,y)`, `none` = leave without a target (moving or script-made
actors with no stable tag, or nothing to point at).

| Mission | Objective id | Target | Why / note |
|---|---|---|---|
| tut_01 | move | zone:rally | done in the mission file (all seven lessons have targets) |
| tut_01 | attack | tag:dummy | done |
| tut_01 | power, barracks, train | tag:yard | done (build beside the yard) |
| tut_01 | harvest | cell:(8,5) | done (pitchblende patch) |
| tut_01 | fight | zone:camp | done |
| auth_01 | reach | zone:survey | "survey site in the north-east" |
| auth_01 | scrapers | tag:scraper1 | follows the first scraper; switch to scraper2 if it dies first |
| auth_01 | surveyors (hidden) | tag:svy1 | civilian surveyor; switch to svy2 once svy1 is safe |
| auth_01 | hold | zone:survey | added when "reach" completes |
| auth_01 | refinery | zone:pit | "beside the pitchblende pit" |
| auth_02 | fortify | zone:junction | two turrets on the junction |
| auth_02 | hold | zone:junction | |
| auth_02 | mast (hidden) | tag:mast | listening mast on the ridge |
| auth_03 | escort | zone:coast | "to the shore in the south" |
| auth_03 | picket | zone:picket | Directorate picket covering the landing |
| auth_03 | home | tag:yard | forward yard (zone:home is the area) |
| auth_03 | cache (hidden) | zone:cache | |
| auth_04 | ford | zone:south_bank | bridgehead of six units on the south bank |
| auth_04 | device | tag:device | the Sunrise Device |
| auth_04 | home | tag:yard | |
| auth_04 | vent (hidden) | tag:vent | reactor vent |
| dir_01 | hatch | zone:site | hatch inside the marked site |
| dir_01 | muster | zone:site | |
| dir_01 | hold | zone:site | added when "hatch" completes |
| dir_01 | picket (hidden) | zone:picket | Authority picket to the north-east |
| dir_02 | posts | tag:post_n | three posts; re-point to post_w, then post_e as each falls (or use zone:north_post / west_post / east_post) |
| dir_02 | convoy | none | convoys are script-made (`state.convoyTags`); optionally point at the first live tag in that list |
| dir_02 | depot (hidden) | tag:depot_silo | road depot to the south (zone:depot is the area) |
| dir_03 | alpha | zone:alpha | Pad Seven (tag:pad_a is its watch light) |
| dir_03 | beta | zone:beta | Pad Nine (tag:pad_b is its watch light) |
| dir_03 | relief | none | relief columns arrive by script and move |
| dir_03 | ridge (hidden) | tag:ridge_post | watch post on the south ridge |
| dir_04 | hold | zone:device | the Device and the tunnel head (zone:head is the tunnel head) |
| dir_04 | fire | tag:hq | Authority Ash Ridge command |
| dir_04 | west (hidden) | tag:west_post | watch post on the west bank |
| exp_01 | landing | zone:beach | deploy the Command Ship on the beach |
| exp_01 | build | zone:beach | beachhead buildings |
| exp_01 | hold | zone:beach | |
| exp_01 | craft (hidden) | none | landing craft are script-made and move; the shipments land on zone:beach |
| exp_02 | dock | zone:shore | Anchorage on the north shore |
| exp_02 | hold | zone:landing | |
| exp_02 | post | zone:garrison | garrison on the coast road (tag:g_barracks for the building) |
| exp_02 | line (hidden) | none | "never lose the Anchorage": a standing condition |
| exp_03 | deck | zone:cove_land | Flight Deck and SAM Mount in the cove |
| exp_03 | carrier | zone:cove | bring the carrier into the cove |
| exp_03 | hold | zone:cove_land | |
| exp_03 | fleet (hidden) | none | standing condition |
| exp_04 | fuel | zone:ash_field | Dredge Works on the ash field |
| exp_04 | hold | zone:beachhead | |
| exp_04 | base | tag:shaft | the redoubt's Shaft (tag:device is the Sunrise Device; re-point after the Shaft falls) |
| exp_04 | ash (hidden) | none | standing condition |
| lb_01 | deploy | zone:camp | Mother Rig unpack site (the rig is driven to 26,22; S19 MIS-06) |
| lb_01 | scrapers | zone:survey | scrapers must be stopped before they reach the survey ground |
| lb_01 | escort | zone:ambush | the convoy's escort (zone:camp for the relief) |
| lb_01 | cache (hidden) | zone:cache | south-west gully |
| lb_02 | gate | tag:rig | Garage Rig and Lookout are raised at the rig |
| lb_02 | posts | tag:post1 | three posts; re-point to post2, post3 as each falls |
| lb_02 | hold | zone:camp | the reprisal column's camp area |
| lb_02 | stash (hidden) | zone:stash | north corner |
| lb_03 | vent | tag:vent | hatch field's reactor vent |
| lb_03 | hatches | tag:hatch1 | three hatches; re-point to hatch2, hatch3 as each closes |
| lb_03 | column | zone:camp | |
| lb_03 | mast (hidden) | tag:mast | loudspeaker mast |
| lb_04 | counter | zone:camp | counter-landing at the camp |
| lb_04 | power | tag:pile1 | two pile houses; re-point to pile2 |
| lb_04 | depot | tag:depot | Landing Depot |
| lb_04 | wreck (hidden) | zone:wreck | supply lander on the east plains |

Notes for the lead:
- Objectives marked `none` have no stable place; the HUD simply shows no arrow for them (the guidance falls back to the next active
  objective that has a target, so the player is still pointed somewhere).
- Adding a target to a mission changes that mission's state bytes once a target is set (mission state format 5), so any pinned hash or
  replay of that mission moves. Do it in one pass and regenerate those goldens together.
- Check each tag is still alive when `SetTarget` runs; calling it with a dead or unknown tag is a script error (unknown tag), a dead one
  only shows nothing.
