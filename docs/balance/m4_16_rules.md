# M4-16c: rules balance (garrison bonus, tunnel counters, tier timings)

Method: scenario tests in `tests/Sim.Tests/Balance` (`GarrisonBalanceTests`, `TunnelCounterBalanceTests`, harness `BalanceArena`) on a
flat 96x96 map with the shipped `data/rules`; set `RTS_RULES_DIR` to measure a scratch copy while tuning. Whole games:
`Tools ai match --a tiered:<t> --b tiered:<t> --faction-a authority --faction-b directorate --seeds 8 --ticks 25000 --size 56`
(16 games per tier), before and after, same binary otherwise. One tick = 0.05 s.

## 1. Values changed (old -> new)

| File / entry | Key | Old | New | Why |
|---|---|---:|---:|---|
| warheads.toml `[dir_ash]` (Ashthrower) | garrison_damage_percent | 25 | 50 | At 25 four Ashthrowers (800) could not clear a Blockhouse and five Sappers (1 250) still failed; counters must win at about 1.25x cost |
| warheads.toml `[dir_demolition]` (Collapse Charge) | garrison_damage_percent | 25 | 50 | same |
| warheads.toml `[auth_seismic]` (Seismic Mortar) | garrison_damage_percent | 25 | 50 | Mortar needed 1 870 ticks to empty a Redoubt; now 610 |
| weapons.toml `[auth_seismic_mortar_gun]` | damage | 20 | 28 | One mortar needed 1 246 ticks, two 680, for a plain Hatch; two now 572 (about 29 s) |

Every changed line carries `# (tune) M4-16`. Not changed: garrison capacity (5), `garrison_range_bonus`, `garrison_rof_permille`
(measured at 1000/range 0 and 1250: the infantry results did not move in the right direction or flipped the Redoubt result; left alone),
foam ticks/speed, bonus_vs_entrance_percent (200), costs, tier gates, research costs, [neu_*].

## 2. Scenario results (tick counts, shipped rules after the change)

Garrison = building + 5 soldiers (Blockhouse 300 + 5 Riflemen 500 = 800; Redoubt 300 + 5 Conscripts 400 = 700).

| Scenario | Cost garrison / attackers | Result |
|---|---|---|
| Blockhouse vs 10 Conscripts | 800 / 800 | holds, 5 inside, attackers dead at 433 |
| Blockhouse vs 4 Ashthrowers | 800 / 800 | holds, attackers dead at 420 |
| Redoubt vs 7 Riflemen | 700 / 700 | holds, attackers dead at 514 |
| Blockhouse vs 5 Sappers | 800 / 1 250 | cleared at 409, building dead at 529, 2 Sappers left (before: held) |
| Redoubt vs 1 Seismic Mortar | 700 / 800 | cleared at 610 (was 1 870), building dead at 3 040 |
| Redoubt vs 1 Tank | 700 / 800 | building dead at 931, tank alive: not decisive alone |
| Blockhouse vs 1 Rocket Truck | 800 / 800 | building dead at 879, truck alive |
| Hatch vs 1 / 2 Seismic Mortars | | dead at 976 / 572 ticks (was 1 246 / 680) |
| Burrowed Delver vs 1 / 2 Mortars / 1 Tank | | dead at 1 897 / 1 060 / never (before: 2 437 / 1 330) |
| Bore break-through, foam laid on the arrival cell | | Hatch still appears (tick 801), on the nearest unfoamed cell, 2 cells away |

Reading: infantry garrisons beat equal-cost infantry, lose to counters at about 1.25x cost, and are not decisive against a vehicle.
Honest limit: Ashthrowers need roughly 8 for a Blockhouse to be cleared (4 and 5 do not), because they die at range first; the
Sapper is the clean counter.

Foam: a foam shot lays a 3x3 block (splash is a 0/1 flag in the loader), 400 ticks. A burrowed unit or Bore surfaces on the nearest
unfoamed cell instead, and foamed cells slow ground units to 50 %. So Foam costs the Directorate a few cells and speed but cannot
shut tunnels off; nothing was changed. A wider foam radius would need a Sim/loader change (not mine).
Mortars are the answer that costs real time: two mortars (1 600) need about 29 s per Hatch and 53 s per burrowed Delver.

## 3. Whole games (cross-faction, equal tier, 16 games, 25 000 ticks)

| Tier | Before A / B / draw (avg end) | After A / B / draw (avg end) | Draw tiebreak before / after (A ahead - B ahead) |
|---|---|---|---|
| 2 | 6 / 5 / 5 (19 637) | 6 / 5 / 5 (19 637) | 3-2 / 3-2 |
| 3 | 6 / 7 / 3 (16 742) | 6 / 7 / 3 (16 821) | 2-1 / 1-2 |
| 4 | 5 / 6 / 5 (17 745) | 5 / 4 / 7 (18 604) | 4-1 / 5-2 |

Not worse: tier 2 identical (the AI there rarely meets garrisons or mortars), tier 3 the same result, tier 4 one fewer Directorate win
and two more draws (within seed noise; more games reach 25 000 ticks). Start bias is unchanged (start 0 wins 9-11 of 11-13 decisive
games); that is the map/sim issue from m4_ai.md section 4.5, owned by m4-16a.

## 4. Tech costs (proposal, not applied)

Gates: tier 2 = Engineering 500 / Isotope Works 800, tier 3 = 2 500 (Signals HQ / Committee Hall); upgrades 800 / 1 500 / 2 500.
m4_ai.md 4.5 blames this up-front spend for tiers losing to the tier below. I did not measure a cheaper curve here (the 8-seed matches
take about 40 min each tier under the shared lock, and the owner confirmed targets, not numbers). One cautious step to try next:
tier-3 gate 2 500 -> 2 000 (`auth_signals_hq`, `dir_committee_hall`) and tier-3 upgrades 2 500 -> 2 000; leave the tier-2 gate (it is
already cheap). Expected effect: the gate lands about 25 % earlier at AI income. Re-run `ai match --a tiered:<t> --b tiered:<t-1>`
for 3-5 before committing to it.
