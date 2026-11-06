# S12 Left Behind early income (Q-52, M7-19): diagnosis, no number landed

Status: **no rules or AI change landed.** Every `lb_*` economy number I tried (listed below) failed to move the Left Behind toward the +-10 % band, so
changing one would be a guess. The owner's D-153 (3) approval is unused. Nothing was committed to data/rules or src/AI.

## Baseline (current numbers; same method as m7_income.md: tier 2, 10 seeds x 2 swaps = 20 games per cell, Expedition vs each faction)
| Map | Faction | 3 min /min | 5 min /min | 10 min /min |
|---|---|---|---|---|
| small (64) | Authority | 2714 | 3212 | 3400 |
| small | Directorate | 2728 | 3263 | 3317 |
| small | Left Behind | 1713 | 1718 | 832 (2 games reached it) |
| medium (96) | Authority | 2694 | 3210 | 3097 |
| medium | Directorate | 2680 | 3253 | 3051 |
| medium | Left Behind | 2238 | 2275 | 1736 (3 games) |
| large (128) | Authority | 2734 | 2989 | 2898 |
| large | Directorate | 2616 | 3035 | 2804 |
| large | Left Behind | 2040 | 1978 | 2139 (5 games) |

This is a re-run of the S11 "AFTER" table and reproduced it to the digit (the sim is deterministic). Left Behind sits at -17..-37 % of the Authority/Directorate mean at 3 min and
-30..-47 % at 5 min. Expedition small-map gap is unchanged because the Left Behind did not change: +12 % / +16 % / +22 % (medium -6/+6/+8, large -9/+11/-7).

## Diagnosis (traced games, seed 1, medium map)
- It is not the Gleaner's numbers. In a traced game the Left Behind Gleaner spends about 68 % of ticks 0-3600 in Harvest step 1 ("Go", driving to or waiting for a field cell), with the
  next-cell reservation flag often set, and only a few percent loading. The Authority Scraper loads for a comparable share but goes to the field with no neighbours.
- The Left Behind has 2 Gleaners plus a Hauler working the same ash field and ONE dock (lb_yard, dock cell just below the 2x2 footprint). Return step "Enter" (d3) takes 540-1230 ticks per
  visit in the trace: harvesters queue behind each other, and a harvester that finished unloading stands on the dock cell in Harvest step 1 while the next one waits.
  Raider infantry from the Muster Rig also stand at cells 11-13,13 with a Move order for 300+ ticks (stuck at the spawn exit next to the Garage Rig); I did not prove they block the dock lane.
- Credit value per trip is not the limit: more capacity or speed made no difference (below).

## Levers tried (24 seeds x 2 swaps = 48 Left Behind games on medium, Left Behind row, 3 / 5 / 10 min per minute)
| Change | 3 min | 5 min | 10 min |
|---|---|---|---|
| none (baseline, 48 games) | 1882 | 1927 | 1503 |
| lb_gleaner harvest_capacity 12 → 20 | 1812 | 1798 | 1713 |
| lb_gleaner harvest_capacity 12 → 30, speed 16 → 26 (separately: speed 26 alone) | 1756 (cap 30) / 1836 (speed 26) | 1595 / 1794 | 1180 / 2017 |
| start kit: second Gleaner (game.toml units) | 1668 | 1610 | 2039 |
| AI build order: Garage Rig before Muster Rig | 1727 | 1773 | 1259 |
(Authority/Directorate means on medium for comparison: about 2690 / 3230.) None of these is a gain beyond noise at 3 and 5 minutes, several are worse, so the cause is throughput at the
dock or field cells (sim movement/queueing), not a Left Behind number.

## What I recommend (owner / lead decision)
1. The fix is probably outside my ownership: a second dock cell on lb_yard (`dock` is a single cell; a data change would need sim support for several docks), or a sim fix for the queue/"Enter"
   stall (src/Sim/Missions/HarvestMission.cs, forbidden for this package). Alternatively the AI builds the second refinery (lb_refinery_rig, 900) earlier: TierPlanner extends it only at ExpansionTick.
   Suggested next test: force a second `lb_yard` or `lb_refinery_rig` at about tick 1500 in the Left Behind order and re-measure (an AI change in src/AI/LeftBehind* territory, one line in DefaultOrder).
2. Alternatively accept the gap: win rates are not skewed by it (S10: Authority-Left Behind about 68-72 % for Authority, Directorate-Left Behind 40 %).
3. The 8-game win/loss before/after and the pinned hashes were not run: no change landed, so no hash moved.

## Files
- tools/measure/s12_lb_cells.sh: runs only the Left Behind cells of m7_income.sh (about 1.2 min per medium cell of 24 seeds); aggregate with tools/measure/m7_income_agg.py.
