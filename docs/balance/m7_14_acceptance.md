# M7-14 acceptance, part 1: soak, golden replay and the section 6 checklist

Branch `wp/m7-acc-s11` (from `integ-s11`: Expedition Recruit AI, shipments on, naval deploy, D-151 south coast). Measurement and tests only; no `src/`, `data/` or
Expedition AI change. Every number below comes from the runs described here (210 soak games, one golden replay, the unit tests named).

## Method

- Golden replay: `tests/Sim.Tests/Golden/ExpeditionGoldenTests.cs` + `golden_expedition_hashes.txt` (56 hashes, every 16th tick of 900). Hand-made rules and a hand-made
  64x48 coast (sea x < 12, shoals 12..13, beach x = 14, ground beyond), scripted commands only (no AI), so balance and data changes do not move it. The script:
  Command Ship Deploy into a depot; four buildings placed inside the beachhead (power, tunnel hatch, shipment dock, Salvo Array); two marines and a tank board a
  landing craft that carries a vehicle (`vehicle_slots`); the craft sails out and back; the unload puts all three ashore on the beach cell; a submarine dives, sails
  and surfaces; a beacon is placed (the beachhead zone grows); the first shipment tender is sunk (Debug kill) and later tenders arrive and pay; the Salvo Array
  fires four missiles at a 5x5 block of enemy tanks. The game is saved and loaded at ticks 130 (mid-voyage), 290 (Salvo Array charged and fired, tender
  voyages running) and 352 (landing craft mid-unload); a twin that is never saved must stay on the same hash at every sample. A second test checks the script
  really did each thing (depot stands, craft boarded, zone grew, dive and surface seen, tender lost, shipment income, 4 x 100 salvo damage, passengers ashore).
- Soak: `tests/AI.Tests/Soak/SoakTests.cs`, extended from M6-12: `expedition` is a faction; a pair with an Expedition side plays on `MapGenerator.GenerateWithMeta`
  with ocean north + south (as `ai match` does, D-151: Expedition on the south start); new per-game columns: stuck ships (a ship with a Move order that did not leave
  its cell for 600 ticks, sampled every 20 ticks) and refusals (Move commands in the recording for a ship whose goal is not in the ship's own sea; the sim drops these
  on the spot, `Simulation.MoveGoalRefused`; the region check uses the ship's region and map at the last sample, so it is an approximation); desync check as in
  M6-12 (in-memory replay re-run, every recorded hash plus the final hash). New env var `RTS_SOAK_GAMES` picks the slot order(s).
- Driver: `tools/measure/acc7.sh` (resumable). Cells = 10 pairs x sizes 64/96/128 x tiers 1..5 = 150 cells, seed 1. Pairs with an Expedition side play both slot orders (2 games per
  cell); the other six pairs play one game per cell (slot order alternating), so the 150 cells hold 210 games. Both sides use the same tier; limit 30 000 ticks (a game still
  running then is a draw). Release build, run outside the dotnet lock.

## Soak results (210 games)

| Pair (A-B) | Games | Draws | Avg end tick | Desyncs | Exceptions | Stuck ships | Refusals (of ship Moves) | Max idle rig (ticks) | Stuck units (heuristic) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Authority-Authority | 15 | 3 | 19092 | 0 | 0 | 0 | 0 (n/a) | 0 | 30 |
| Directorate-Directorate | 15 | 4 | 22596 | 0 | 0 | 0 | 0 (n/a) | 0 | 46 |
| LeftBehind-LeftBehind | 15 | 2 | 17482 | 0 | 0 | 0 | 0 (n/a) | 260 | 123 |
| Authority-Directorate | 15 | 4 | 24282 | 0 | 0 | 0 | 0 (n/a) | 0 | 59 |
| Authority-LeftBehind | 15 | 0 | 12896 | 0 | 0 | 0 | 0 (n/a) | 400 | 44 |
| Directorate-LeftBehind | 15 | 2 | 18340 | 0 | 0 | 0 | 0 (n/a) | 380 | 52 |
| Expedition-Expedition | 30 | 5 | 17680 | 0 | 0 | 0 | 0 of 162 | 0 | 49 |
| Expedition-Authority | 30 | 1 | 9969 | 0 | 0 | 0 | 0 of 55 | 0 | 91 |
| Expedition-Directorate | 30 | 5 | 16155 | 0 | 0 | 0 | 0 of 138 | 0 | 136 |
| Expedition-LeftBehind | 30 | 7 | 18438 | 0 | 0 | 0 | 0 of 79 | 400 | 291 |

Totals: 210 games, **0 desyncs, 0 exceptions, 0 stuck ships, 0 unreachable-goal refusals** out of 434 ship Move orders, 33 draws at the limit (16 %), longest idle packed rig 400 ticks
(limit 600). Coverage: every pair x size (64, 96, 128) x tier (1..5) cell was played at least once (one seed; the Expedition pairs in both slot orders). The generator gave every Expedition game a
coast (no game failed to start). Per-game lines: `scratch/acc7/*.txt` (not committed; reproduce with `tools/measure/acc7.sh 0 1 40000`).

Expedition results by tier (A = Expedition; wins-losses-draws over sizes 64/96/128 x 2 slot orders = 6 games per cell): vs Authority 4-25-1 overall (tier 1..5: 0-5-1, 0-6-0, 0-6-0, 2-4-0, 2-4-0), vs Directorate 5-20-5
(0-6-0, 0-3-3, 1-4-1, 2-4-0, 2-3-1), vs Left Behind 13-10-7 (3-3-0, 2-1-3, 2-3-1, 3-1-2, 3-2-1).

Reading and limits: "stuck units" is the M6-12 heuristic (any ground unit with a Move or AttackMove order that stayed in one cell for 1200 ticks, including jams and units still on an
order at game end); it is a pointer, not a verdict, and is highest where Expedition games have many landings (Expedition-Left Behind 291 over 30 games). It was not compared with M6-12 per game
(that run had 3 factions, 4 games per cell). The stuck-ship watch sees a ship only while it holds a Move mission; ships waiting for a cargo or an unload order are not counted.

## 11 section 6 checklist

| # | Bullet | Verdict | Evidence |
|---|---|---|---|
| 1 | An Expedition player can win a skirmish against each tier of the other three factions using only faction features, on a map with a coast | NOT MET (AI evidence only) | No human play was run. The tiered Expedition AI (same tier both sides) wins 4 of 30 vs Authority, 5 of 30 vs Directorate, 13 of 30 vs Left Behind (table above); it wins no game against Authority at tiers 1..3 and none against Directorate at tiers 1..2. Expedition AI tuning is another package (not touched). |
| 2 | Landing, unload, submerge, shipment, tender sinking, beacon placement and the Salvo Array save/load mid-replay exactly (golden replays) | PASS | `ExpeditionGoldenTests` (2 tests, green): hashes equal a never-saved twin after 3 save/loads, and equal `golden_expedition_hashes.txt`. Per-feature save/load tests already existed (`LandingTests`, `SubmergeTests`, `ShipmentTests`, `SalvoArrayTests`, `BeachheadTests`, `NavalDeployTests`). `ExpeditionScript_ReallyExercisesTheFeatures` confirms every step happened; the saves at 130, 290 and 352 fall mid-voyage, around the Salvo Array strike and mid-unload. |
| 3 | AI vs AI soak (all faction pairs, 5 tiers, all map sizes) has no desync, no stuck ship, no unreachable-goal spam | PASS | 210 games, 0 desyncs, 0 exceptions, 0 stuck ships, 0 of 434 ship Moves refused. |
| 4 | The income table (section 4) passes; supply and veterancy tables are attached | see `docs/balance/m7_income.md` (other package); supply table: `docs/balance/s10_tables.md`, section "Session 11 Q-54 re-measure" | Veterancy table: the same re-measure run also produced the VET columns (see that section's note). |
| 5 | A hand-made map with no landing coast refuses the Expedition; the generator always gives one | PASS | `tests/Game.Tests/M7/ExpeditionSetupTests.cs` (existing from M7-14: `HandMadeMap_WithoutLandingCoast_RefusesAnExpeditionSlotWithTheSlotNumber`, `..._WithALandingCoast_IsAccepted`, `..._WithoutExpedition_IsAccepted...`). Added `LandingCoastGeneratorTests`: 4 biomes x 3 sizes x 2/4 players x seeds 1..6 x 5 ocean-edge choices all have landing markers on Coast cells, and an Expedition on a generated map with no ocean is given an edge that also yields markers. |
| 6 | Full suite green on Linux and Windows CI | Linux PASS (see result line below); Windows CI not run here | Full suite run at the end of this package. |

## Not covered / follow-ups

- One seed per soak cell (as M6-12 used two seeds per cell for 4 games). More seeds would need more time; the cells that matter most for Expedition (both slot orders) are doubled.
- Bullet 1 needs either a human check or tuning of the Expedition AI; the numbers above are the baseline for that.
