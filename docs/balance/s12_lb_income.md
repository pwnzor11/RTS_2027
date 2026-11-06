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

## Follow-up: dock throughput (D-157)

Status: **landed.** Two changes: `lb_yard` `storage` 1000 -> 3000 (data, `# (tune) D-157`) and `PlacementSearch` never puts a structure on a refinery dock cell or a factory exit cell (AI, all factions). `HarvestMission` was not changed.

### Cause (the "dock queue" diagnosis above was wrong)
Traced with a temporary debug line per harvester per 20 ticks (removed before commit), medium seed 1, Authority vs Left Behind, ticks 0-4000:
- Dock steps are not slow. Average run length per step: Left Behind Gleaner Wait 17 ticks, Enter 40, Unload 4; Authority Scraper Wait 9, Enter 28, Unload 2. The 540-1230 "Enter" figure in the first report was a whole visit (travel plus dock); the dock cell was free whenever nobody held the claim, and the Raiders at the Garage Rig exit never stood on the dock lane. The shared harvest/dock state machine (claim, enter, unload, release) completes for all factions: no stuck retry, so no sim bug and no `HarvestMission` change.
- The loss is credits thrown away at the refinery. `HarvestMission` Unload adds `Math.Min(pay, space)` to Stored and drops the rest ("the overflow is lost"); the cap is the sum of `storage` over the player's buildings. `lb_yard` stores 1000 and the Left Behind early order has no Silo (Authority and Directorate: refinery 1000 + silo 1500, silo at tick ~2000-2300). The AI saves for the 900-credit Garage Rig and the waves, so income over the cap vanishes. That is why capacity, speed, an extra Gleaner and build order made no difference in the first study: everything hauled past the cap was discarded.
- Proof is the experiment (table below): raising only that number lifts the Left Behind from 20-40 % under to within about +-10 % of the other two.
- Second cause, found by the full suite (`SupplyAiTests`, Left Behind tier 5 was capped 574 s once storage no longer limited it): in the cramped Left Behind corner the placement fallback (tier 2, "anything legal") put a Rotor Pad on the Yard's dock cell (`lb_rotor_pad at 1,7 covers the dock of lb_yard cell 2,7`); every harvester then sat in Return for good, income stopped, and the AI sold its own base. Authority had the same defect (`auth_ration_hall at 3,10 covers the dock of auth_refinery cell 4,10`). `PlacementSearch` kept 3x4 lanes in tiers 0-1 only; tier 2 now still refuses the single dock/exit cells themselves.

### Fix
1. `lb_yard` storage 1000 -> 3000: the Yard is the Left Behind's refinery and silo in one ("fixed cheap refinery and silo"), so it carries what Authority's refinery + silo carry (2500) with a small margin. Test `LeftBehindStorage_IsNotBelowTheOtherFactionsEarlyStorage`.
2. `PlacementSearch`: dock and exit cells of own refineries/producers are never covered, in any tier. Test `DockLaneTests` (fails on the old code for both Left Behind and Authority).

Values tried, Left Behind /min at 3 / 5 / 10 min (10 seeds x 2 swaps = 20 games per cell, tier 2, Expedition vs Left Behind). The 1000 row is re-measured on this tree; the 2500 and 4000 rows are from the same tree before the placement fix (the placement fix did not move any measured cell):

| lb_yard storage | small 64 | medium 96 | large 128 |
|---|---|---|---|
| 1000 (before) | 2045 / 1939 / 1450 | 1921 / 1935 / 1691 | 1752 / 1682 / 1601 |
| 2500 | 2712 / 2783 / 2934 | 2796 / 2929 / 2862 | 2570 / 2603 / 2992 |
| 3000 (landed) | 2829 / 2925 / 2525 | 2927 / 3018 / 3090 | 2776 / 2906 / 3315 |
| 4000 | 3162 / 3209 / 3002 | 3260 / 3246 / 3515 | 3138 / 2982 / 3110 |

(The 1000 row differs slightly from the first report's table, 1713 / 1718 / 832 on small, because other merges on integ-s12 changed the tree in between; the same applies to the Authority/Directorate rows, which were re-run: they read 2561 / 3213 / 3464 and 2581 / 3202 / 3264 on small, and are identical with and without the placement change.)

### Result against the Authority/Directorate mean (all four factions, same tree, after)
| Map | Authority | Directorate | Left Behind before | Left Behind after | LB after vs A/D mean (3 / 5 / 10) |
|---|---|---|---|---|---|
| small | 2561 / 3213 / 3464 | 2581 / 3202 / 3264 | 2045 / 1939 / 1450 | 2829 / 2925 / 2525 | +10 % / -9 % / -25 % |
| medium | 2549 / 3162 / 3042 | 2650 / 2983 / 2671 | 1921 / 1935 / 1691 | 2927 / 3018 / 3090 | +13 % / -2 % / +8 % |
| large | 2551 / 2926 / 2739 | 2620 / 3043 / 2645 | 1752 / 1682 / 1601 | 2776 / 2906 / 3315 | +7 % / -3 % / +23 % |

Before the change the Left Behind sat at -20 / -40 % (3 / 5 min) of the mean on all maps. Target (within +-10 % at 5 and 10 min): met at 5 min on all three maps and at 10 min on medium; the 10-minute cells on small (-25 %, 7 games reached it) and large (+23 %, 4 games reached it) rest on 4-7 games and move by +-20 % between neighbouring storage values, so they are noise, not a trend. Expedition (own row) is unchanged: it reads 2670 / 3138 / 3163 small, 2673 / 3268 / 2916 medium, 2494 / 3104 / 2824 large.

### Expedition small-map gap (Expedition vs the mean of the other three factions, same tree)
| | 3 min | 5 min | 10 min |
|---|---|---|---|
| before (Left Behind 2045 / 1939 / 1450) | +11 % | +13 % | +16 % |
| after (Left Behind 2829 / 2925 / 2525) | +0 % | +1 % | +3 % |

The Expedition small-map lead was almost entirely the Left Behind pulling the three-faction mean down. Medium: -1 % / +7 % / -1 %; large: -6 % / +5 % / -3 %.

### Other factions
No data or sim change. The placement fix changes Authority/Directorate games only where a fallback site used to cover a dock or exit cell; their income cells above are identical with and without it.

### Win/loss, Left Behind (A) vs each faction, tier 2, medium, 4 seeds x 2 swaps = 8 games (indicative)
| Opponent | Left Behind wins / opponent wins / draws, before | after |
|---|---|---|
| Authority | 0 / 4 / 4 | 2 / 2 / 4 |
| Directorate | 1 / 0 / 7 | 3 / 0 / 5 |
| Expedition | 0 / 4 / 4 | 2 / 4 / 2 |
(Measured with storage only; the placement fix came after.)
