# Expedition campaign (M8-12): exp_01..exp_04 and the ending

Leader on the radio in all four: Commissioner Imelda Strand (`leader_exp_strand`, polite requests by telex). Cameos: Dr. Roque (exp_02, exp_04), Air Marshal Kell (exp_03), Fleet Admiral Aldersey (exp_03, exp_04), Brigadier Wrenfield (exp_02, enemy net), the Voice / Larkin (exp_03, exp_04, enemy broadcast). Existing names only.
Since s12 the human plays the Expedition in the sim, so the beachhead build zone is enforced in every mission. All maps are the shared act maps (D-030).

## Missions
- **exp_01 First Landing** (act 1, glass_coast_e1, south-east beach, vs Authority coast watch): deploy the Command Ship, raise the beachhead, hold three waves. (pilot)
- **exp_02 The Supply Line** (act 2, glass_coast_e1, NW landing on the north shore; the Rust Belt has no sea, so act 2 uses the Glass Coast at a different landing): an advance party is ashore (depot, Hover Yard, Plotting Room). Raise the Anchorage so home shipments sail in (two scripted convoys), hold three Authority columns, break the coast road garrison (power, barracks, two Seawall-class turrets, riflemen, jeep). Bonus: never lose the Anchorage.
- **exp_03 The Fleet Reaches the Coast** (act 3, glass_coast_e1, west coast cove, two enemy players: Authority and Directorate): two fronts (Authority north road, Directorate south dunes) plus an air raid at tier 4+. Raise a Flight Deck and a SAM Mount, bring the carrier group (carrier and two destroyers, `Reinforce.Naval`) into the cove, hold both columns twice. Bonus: lose no destroyer. Carrier ammunition and rearm (D-156) are in the briefing and in Kell's radio line.
- **exp_04 The Fuel** (act 4, ash_reckoning, south landing; sea to the south; finale): put a Dredge Works on the ash field, hold three Directorate columns (the built-in AI is set to the mission tier and sent at the beachhead with `AI.Attack` once the fuel is taken), then break the redoubt (Shaft and Sunrise Device, ringed by pillboxes and rocket nests). Bonus: never lose the Dredge Works. Ends in the Expedition's victory (Q-C) and `expedition_ending`.

## Tech climb
| Mission | Starts with (adds) | Unlocks on win |
|---|---|---|
| exp_01 | pile house, billet, marine, seawall (pillbox), beacon | anchorage |
| exp_02 | + anchorage, dredge works, skimmer, provision store, hover yard, hoverlander, plotting room (tier 2), glide tank, gun battery | quay, landing craft, launch, shipyard, destroyer, sparker |
| exp_03 | + quay, landing craft, launch, shipyard, destroyer, sparker, flight deck, jet, gunship, SAM, skyhook | core hall (tier 3), drydock, cruiser, carrier, missile sub, ironbound, diver |
| exp_04 | + core hall, drydock, cruiser, carrier, missile sub, ironbound, diver | bomber, Salvo Array (superweapon) |

## Ending
`data/campaigns/expedition_ending.toml`: five slides (owner-approved, D-164); slide 4 only when exp_04 was won on difficulty 4 or more.

## Open questions
1. Archive unlocks (m8_13) list the Plotting Room, Shipyard and cruiser entries under exp_02; the missions put the Plotting Room in the player's hands in exp_02 (pre-placed, glide tanks need tier 2). Entries are text only, so nothing breaks, but the owner may want the Archive moved one mission earlier.
2. Marines do about 5 percent damage to fortified emplacements, so acts 2-4 need Glide Tanks (tier 2). Is that the intended role of the marine?
3. A lone Authority power plant or Directorate reactor vent in the far corner keeps each enemy team alive (a bunker does not count as a base). A cleaner API for "enemy cannot be eliminated" would remove these reserve actors.
4. exp_03 uses `Reinforce.Edge` for Authority bombers; one bomber killed the depot in about 1000 ticks even with three SAM Mounts, so the raid is aimed at the carrier cove (tier 4: 1 bomber, tier 5: 2). Air balance is untuned.
5. Starting cash (4000 / 10000 / 10000) stands in for an economy the test bot does not run.
