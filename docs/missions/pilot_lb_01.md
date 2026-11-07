# Pilot L1 "The Refused" (lb_01, M8-10)

Story: act 1 seen from the other side of auth_01. The Left Behind, refused by the Authority in 1999, ambush an Authority survey convoy on the
Rust Belt road. Unpack the Mother Rig, raise a small camp, then break the convoy at the crossing and beat off its relief.
Leader on the radio: Jessup Calloway-Ross (`leader_lb_calloway_ross`), the warlord, loud and blunt; Marrow Tindall is only named in the briefing.
Agent time: about 1.5 agent hours (read 0.3, write 0.4, balance and bot 0.8).

## Shape
- Primary: unpack the Mother Rig (`Reinforce.Rig` parks the packed rig on an ash cell, which is not a legal site, so the player picks the ground
  and unpacks it); destroy both Authority scrapers before they reach the survey ground; break the escort and its relief.
- Hidden bonus `cache`: a scraper abandoned in the south-west gully (zone `cache`); entering it gives an Authority scraper and 600 cash.
  A radio line 900 ticks in hints at it.
- Visible timer `convoy` (2700 ticks): when the convoy leaves the depot. Both scrapers drive to the crossing (zone `ambush`), the escort
  attack-moves there, and after 700 ticks all of them go on to the survey ground. A scraper in `survey` loses the mission; so does a deadline of
  6000 ticks after departure, or the loss of the Mother Rig (checked twice in a row so the pack/unpack swap cannot trigger it).
- Difficulty by unit count only: escort riflemen 2,2,3,3,4 plus a jeep from tier 4; relief (called by the first loss, arrives 300 ticks later
  from the north edge) 1,1,2,2,3 plus a jeep at tier 5. The player's force, cash and tech never change.
- Authority team is `role = "neutral"` team 1 (the script drives it; an AI would send the scrapers harvesting). An `auth_power` anchor far
  in the south-east keeps the team alive until the script ends the mission.

## Tech
`[starts_with]` / players tech: lb_mother_rig, lb_dynamo_rig, lb_muster_rig, lb_yard, lb_scavenger, lb_hook_runner, lb_slinger, lb_spike_nest.
Slingers are tier 2 (not trainable without a lookout), so the five starting slingers are the answer to the heavy-armoured scrapers
(small arms do 20 percent to heavy armour). `[unlocks_on_win]`: lb_garage_rig (L2 strips road posts and needs vehicles).

## API gaps and notes
1. No way to give a unit a queue of orders, so the convoy halts via two timed legs in the script.
2. Scrapers path along the southern road while infantry take the northern one; the escort and scrapers meet at the crossing because of the first leg.
3. No capture or ownership-change call: the "captured scraper" bonus creates a new `auth_scraper` for the player.
4. Pathing through the ash overlay: a rig parked on an ash cell is the only way to make `Reinforce.Rig` leave the unpack to the player; it relies on
   overlay cells being illegal sites.
5. `--mission-check` with an idle player ends Lost (the scrapers reach the survey ground) with 0 errors.
