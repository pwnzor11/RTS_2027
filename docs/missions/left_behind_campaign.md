# Left Behind campaign (M8-10, M8-12)

Hero on the radio: Jessup Calloway-Ross (`leader_lb_calloway_ross`). Cameos: Brigadier Wrenfield (lb_02), Commissar Thrall (lb_03), Air Marshal Kell (lb_04).

## Missions
- **lb_01 The Refused** (Rust Belt): unpack the Mother Rig, ambush the Authority survey convoy, beat its relief.
- **lb_02 Road Posts** (Rust Belt, bigger base fight): raise a Garage Rig and a Lookout (tier-2 gate) to train slingers; strip three Authority road posts (each turret draws power from a plant 8+ cells back: kill the plant, then the turret); beat the reprisal column that comes when the first post falls (or at 5400 ticks). Hidden: north stash (cash, two buggies). Timer: reprisal, then garrison deadline.
- **lb_03 Hatch Field** (Glass Coast, two fronts): kill the Directorate reactor vent from outside pillbox range with harpoon wagons, close three hatches that keep pouring soldiers, and hold the camp against an Authority column at 4800 ticks. Hidden: loudspeaker mast (cash). Overrun after 8 hatch waves.
- **lb_04 The Banner** (Ash Plains, finale): break the Expedition counter-landing at the camp (5400 ticks), swing east, kill both pile houses so the two gun batteries go dark, then break the Landing Depot before the second wave (12000 ticks later). Hidden: the supply-lander wreck (cash, two tinkers). Built-in Expedition AI at the difficulty tier, held with AI.Defend.

## Tech climb
| Step | Unlocked by | Ids |
|---|---|---|
| lb_01 kit | start | mother rig, dynamo, muster, yard, scavenger, hook runner, slinger, spike nest |
| lb_01 win | lb_01 | lb_garage_rig |
| act 2 (lb_02 starts) | lb_01 + vehicles | garage, buggy, gleaner, lookout (tier-2 gate) |
| lb_02 win | lb_02 | lb_bench, lb_harpoon_wagon, lb_tinker, lb_drifter_brute |
| lb_03 win | lb_03 | lb_rotor_pad (tier-3 gate), lb_rolling_fort, lb_veilwalker, lb_organ, lb_harpoon_post |
| lb_04 starts | finale gifts | lb_rotor, lb_kite, lb_flak_net, lb_second_drift (superweapon) |

## Notes
- Slingers are tier 2 (need a Lookout), so lb_02 asks for one. Tier 3 units need a Rotor Pad (via the Bench), pre-placed in lb_04.
- Neutral-role slots are used for posts, hatch field and columns (scripted); each lb_02 post has its own player so its turret has its own power. Group spawns are capped at 32 units. Expedition buildings must stand inside a beachhead zone (depot 10 cells, beacon 6), so lb_04 chains two beacons east.
- Ending: `left_behind_ending.toml`, five draft slides, one conditional on lb_03 and one on lb_04 won at tier 4+.

## Open questions
1. Hatch field and posts use `role = "neutral"`; should any use the full AI?
2. The Second Drift is available but never required in lb_04 (storm does no damage). Want a bonus for firing it?
3. Slide text is a draft; the warlord-takes-over beat is in slides 1-2 and the last radio line.
