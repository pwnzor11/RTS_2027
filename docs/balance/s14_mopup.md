# S14 mop-up (M4-16): result, no gain

Method: `Tools ai match --a tiered:A --b tiered:B --faction-a F --faction-b F --seeds 16 --size 96 --ticks 30000` (32 games per cell, Release build, A = higher tier).
Decisive % = A / (A + B). 32 games: the noise band is about +-18 points, so only large moves mean anything.
Variants were built from the worktree and run from copies of the built tool.

## Diagnosis (Directorate draws)
- Seed 13 game 1 (5 v 1): from t22500 the loser has 1-2 units, no scraper, no cash and 13-15 structures, yet the game draws. The winner's waves (11-13 units)
  were launched again and again (waves 7 -> 11) and lost units each time: the remaining structures include defences (pillbox, rad mortar, bunk galleries).
- Seed 4 (timeline, 5 v 1, swap 0): the tier-5 side lost its whole early army (20 -> 1 unit by t6000) to the recruit, the recruit side (no mop-up, tier 1) never finished it. A draw can come from either side failing to finish.
- Directorate hatches (87 hp, scattered over the map, built by Bores) count as bases (`UpdateDefeat`: any non-wall building), so a player stays alive on stray hatches that nobody has seen.
- The wave code already attacks every remembered structure, nearest first, and falls back to the scouting route when none is known. "No enemy army seen" is rare in these games
  (the loser keeps producing single units), so a rule keyed on it fires seldom.

## Variants (all gated on `respond_to_attacks`, tier 1 untouched)
1. v1: when no enemy mobile unit was seen for 3600 ticks (and tick >= 6000, army >= 6), launch a wave with the whole army at once (ignoring thresholds, gate, cooldown), at most every 1500 ticks.
2. v2: v1 plus a shroud sweep: with nothing known and no enemy army, the wave goal is the nearest never-seen passable cell of an 8-cell grid.
3. v3: v2 with the early launch off (`MopupLaunch = false`), sweep only. The full AI.Tests run then failed `SupplyAiTests.TheAiNeverSitsCappedForMoreThanAMinuteInTheFirstFifteen(LeftBehind, 5)` (passes on the commit before the mop-up), so the sweep is off as well (`MopupSweep = false`).
   Committed state: both halves compiled in but off; behaviour equals the baseline (AI.Tests 199/199).

## Table, A / B / draw (decisive %)

| Cell | Baseline | v1 | v2 | v3 (committed) |
|---|---|---|---|---|
| Directorate 5 v 1 | 6 / 7 / 19 (18 %) | 7 / 6 / 19 (21 %) | 7 / 6 / 19 (21 %) | 6 / 7 / 19 (18 %) |
| Directorate 4 v 3 | 13 / 11 / 8 (40 %) | 9 / 13 / 10 (28 %) | 9 / 13 / 10 (28 %) | 13 / 11 / 8 (40 %) |
| Directorate 5 v 4 | 12 / 5 / 15 (37 %) | 10 / 6 / 16 (31 %) | 11 / 5 / 16 (34 %) | not run |
| Directorate 3 v 3 | 6 / 11 / 15 (18 %) | 9 / 11 / 12 (28 %) | not run | not run |
| Authority 5 v 1 | 16 / 11 / 5 | 16 / 11 / 5 | not run | not run |
| Authority 4 v 3 | 17 / 12 / 3 | 15 / 13 / 4 | not run | not run |
| Authority 5 v 4, 3 v 3 | 16 / 11 / 5, 19 / 11 / 2 | not run | not run | not run |

(Decisive % in the table counts A wins over A + B wins; the 3 v 3 row is a mirror, so it only shows the slot/start split.)

## Reading
- The early-launch mop-up did not cut Directorate draws (5 v 1: 19 -> 19; 4 v 3: 8 -> 10; 5 v 4: 15 -> 16; 3 v 3: 15 -> 12) and it pulled small armies into defences:
  4 v 3 fell from 40 % to 28 % decisive, below the 50 % floor, so it is off.
- The sweep alone changes nothing measurable (v3 equals the baseline within a few ticks of average game length): its trigger (no enemy mobile unit for 3600 ticks) almost never holds.
- The target "5 v 1 >= 90 %" is not reached; in these runs the baseline Directorate 5 v 1 is 18 % decisive on 32 games (s13 reported 68 % on 64 games, different cell filter: here the 19 draws are 59 % of games and A loses as often as it wins, so the tier gap is not visible in this cell).
  Note the baseline here is lower than the s13 table for Directorate 4 v 3 (s13: 52 %, here 40 %), within the noise band.
- Not done: the Authority cells for v2/v3, 5 v 4 and 3 v 3 for v3 (the box was queued for hours). Next lever to try: make the mop-up wait for a bigger army and avoid defences
  (outer structures and remote hatches first), and find hatches by sweeping fog cells, not only shroud. The sweep and counters are in place in `MilitaryManager` (`Mopups`, `SweepGoal`).

## After D-173 (M4-17: skirmish "no way to rebuild for 2 minutes" defeat rule)

Rule: in skirmish and AI matches (`SimSetup.NoRebuildDefeat`, on there, off in missions) a player with no production structure, no unit that can found one and no
harvester for 2 400 ticks is defeated. Production structure = a building type with `produces` set, or a tech-tree root (no prerequisites, listed as a prerequisite by another
building: `auth_conyard`, `dir_shaft`, `lb_mother_rig`, `exp_landing_depot`). Founder = a vehicle whose `deploys_into` / `unpacks_into` (a packed rig: the type it was packed from) is
such a building (`dir_landtrain`, `exp_command_ship`, `lb_mother_rig_packed`; the Authority has none). Harvester = `harvest_capacity > 0`. Units that are only an army do not count.

Method as above (Release Tools, one match process at a time, 16 seeds x 2 = 32 games per cell, 30 000 ticks, size 96, `tiered:A` vs `tiered:B`).
"Before" was re-measured on the commit before the rule (the baseline column in the tables above predates the D-167 merge and no longer matches: it showed Directorate 5 v 1 as 6 / 7 / 19).

| Cell | Before (A / B / draw) | After D-173 (A / B / draw) | Avg end tick before -> after |
|---|---|---|---|
| Directorate 5 v 1 | 11 / 6 / 15 (34 %) | 11 / 6 / 15 (34 %) | 25285 -> 25285 |
| Directorate 4 v 3 | 10 / 11 / 11 (31 %) | 10 / 12 / 10 (31 %) | 22812 -> 22745 |
| Authority 5 v 1 | 16 / 11 / 5 | 16 / 11 / 5 | 17934 -> 17934 |
| Authority 4 v 3 | 17 / 12 / 3 | 17 / 12 / 3 | 18820 -> 18813 |

Reading: the rule ends one game out of 128 (Directorate 4 v 3: a draw becomes a B win) and shortens one other by a few ticks. The Directorate draws are not "loser without a way
to rebuild": in the end-state dump of Directorate 5 v 1 (seed 1, swapped starts) the draw side still owns `dir_shaft`, `dir_assembly_bore`, `dir_muster_hatch`, `dir_isotope_works` and a dozen
defences, so it counts as able to rebuild, and the other side never finishes it. The rule is correct and cheap, but it does not fix the Directorate draw rate; that needs the attacker to
win base fights (see the mop-up notes above) or a stricter definition (for example, no income source or no cash for 2 minutes).

## Session 22 (M13-20): mop-up switched on as a runtime option

Why the two halves failed (diagnosis): (1) the early launch sent the whole army at once whenever "no enemy mobile unit seen for 3 600 ticks" held, which also holds for a small army
in the middle of a game, so it walked into defences (4 v 3 fell 40 % -> 28 % decisive). (2) The shroud sweep only ran when that same trigger held, which almost never happens (the loser keeps
making single units); it had no memory of fog, so it could never revisit a block seen once, and one 80-unit wave searched the map as a single group. (3) The `SupplyAiTests` LeftBehind tier 5 stall
did not reproduce on the s22 base: with `MopupSweep` flipped on, `SupplyAiTests` passes 6/6 (the AI changes since s14 removed it); the sweep is now also bounded by the new trigger, so it cannot keep an army off the
supply cap in the first minutes (nothing runs before tick 9 000).

New design (`MopupPlan`, `MilitaryManager`; on by default for profiles with `respond_to_attacks`, i.e. tier 2 and up; tier 1 unchanged; missions off; `ai match --mopup on|off` switches it for both sides):
- Option: `DifficultyProfile.Mopup` (default true) is read through `MilitaryOptions.Mopup` (= `respond_to_attacks` and `Mopup`). A mission AI (the driver sets script hooks, `RecruitAi.ApplyHooks`) sets `MilitaryManager.MissionBot` and never mops up, so campaign behaviour is unchanged.
- "Beaten" trigger (integer maths): tick >= 9 000, army >= 8 units and worth >= 3 000 credits, and worth >= 4 x the biggest enemy army value seen in the long (3 600 tick) window, held for 1 200 ticks without a break
  (so the enemy has shown no real army for about 4 800 ticks), no threat at home for 900 ticks (no own building hit, no armed enemy unit within 20 cells of our start), and 1 800 ticks since the last wave.
- Launch: the wave thresholds and the strength gate do not apply, but the retreat cooldown, the scripted holds and the siege hold (D-174: a defended base still needs its army value first) do. The wave keeps at least 2 units (or the home guard) at home.
- Targets: the wave picks them with the D-174 siege targeting (`SiegePlan.PickTarget`: outside-in, uncovered production / root structures first, defences before the rest), so remote hatches and stray buildings go before the defended core.
- Sweep (nothing known): the AI keeps a per-8-cell-block memory of the last tick the block was in view (`_stamp`, saved with the manager). A block never seen, or not in view for 6 000 ticks, is stale. The sweep goes to the stale passable block
  nearest the army (never seen first; blocks under known defences last). A wave of 12 or more splits into parties of 6 (up to 8), each with its own block at least 16 cells from the others. A sweeping wave counts as progressing while blocks come back into view, otherwise the stale-wave rule ends it as before.
- Save: `_beatenSince`, `_threatTick`, the stamps and the sweep counters are in `MilitaryManager.Snapshot` / `Restore` (AI state layout 5 and 6; older saves still load with the memory empty).

Smoke, 8 seeds x 2 starts = 16 games per cell, size 96, 30 000 ticks, Release tool, `tiered:A` vs `tiered:B`, Directorate both sides (CLOUD SMOKE, 16 games per cell: noisy, differences of one or two games mean nothing):

| Cell | Mop-up off (A / B / draw, avg end tick) | Mop-up on (A / B / draw, avg end tick) |
|---|---|---|
| Directorate 5 v 1 | 13 / 2 / 1, 15152 | 13 / 2 / 1, 15067 |
| Directorate 4 v 3 | 7 / 6 / 3, 17426 | 7 / 6 / 3, 17476 |

Reading: in these 16 game cells the final result is the same on and off. The draws are different cases from the s14 ones: 4 v 3 seeds 4 and 6 are even fights (both sides 10-20 structures, armies within a factor of two), so the "enemy beaten"
test rightly does not fire; 5 v 1 seed 1 is a tier 5 wave stuck on a siege target for 15 000 ticks, which is the siege code's problem, not the mop-up's. The first S22 version (one big wave sweeping as a single group, hold 2 400) turned the 4 v 3 seed 2 win into a draw
(an 80-unit wave searched 9 000 ticks for the last two buildings); splitting the wave into sweep parties fixed it (that game ends at tick 16 768 on, 16 008 off). The real measurement is `tools/measure/run_s22_mopup.ps1` (64 games per row, on and off) on the owner's laptop.
