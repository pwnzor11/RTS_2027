# Pilot E1 "First Landing" (exp_01, M8-10)

Story: the Second Relief Fleet makes the coast in March 2001. The Commander puts the Command Ship ashore on an east-facing beach, raises
the beachhead and holds it while two shipments sail in and the Authority coast watch sends three waves.
Leader on the radio: Commissioner Imelda Strand (`leader_exp_strand`; she sets objectives by telex, Q-F).
Enemy: Authority coast watch. Act 1 is the Authority mobilising, so a conventional coastal garrison fits; the Directorate only surfaces
in the dunes later (act 3 sends both powers to the Glass Coast).

## Tech choices
`[starts_with]`: exp_pile_house, exp_billet, exp_marine, exp_pillbox, exp_beacon. `[unlocks_on_win]`: exp_anchorage (E2 is the supply line).
Cash 2500, the Command Ship deploys into the Landing Depot (not buildable, so not in the list). Shipments are scripted
`Reinforce.Naval` calls (4 marines at landing, 6 more after the visible "Second shipment" timer, 1500 ticks).
Waves (rifles, + a jeep from tier 3): 2,2,3,4,5 / 3,3,4,5,6 / 4,4,5,6,8, spawned by `Reinforce.Zone` from land zones.

## Map
`data/maps/glass_coast_e1.map.toml` (generated: glass_coast, size m, 2 players, seed 11, ocean s). The east coast is clear land with shallows at
x 86-87 and open sea beyond; the ship starts at (86,82) and the depot lands at (83,79). The edge of the map is all sea, so the waves use zones, not `Reinforce.Edge`.

## Agent hours
About 0.8 (read 0.3, map and mission 0.3, tests 0.2).

## API gaps
1. `MissionSession.MakeSetup` does not pass `SimSetup.Factions`, so the Expedition beachhead rule (D-151/D-156: buildings only inside the depot zone) is not enforced in missions; the bot builds inside the zone anyway.
2. No script call to deploy a unit for the player, so the first objective is a manual Deploy order.
3. The tests cannot kill an actor, so the two lose branches (ship lost, depot lost) are untested.
