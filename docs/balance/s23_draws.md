# Session 23 · late-game draws (M13-23, Q-79a, D-234)

Diagnosis only, no rule change. Tool: `ai match ... --trace N --game G --econ 1` (yard/refinery/factory counts, power, harvester states,
queue stalls, ore left map-wide and within 24 cells of the base, army positions and missions). Cloud runs, tiered:3 v tiered:3, rust_belt
layout 1, size 96, 30 000 ticks, `--income 1`.

## Draw rates
| Pair | Games | Draws |
|---|---|---|
| Authority mirror | 16 | 4 (25 %) |
| Directorate mirror | 40 | 4 (10 %) |
| Authority v Directorate | 16 | 4 (25 %) |

## Why nobody rebuilds: the map is mined out
- The whole map starts with 558-914 ore density units (64-114 cells), about 84k-137k credits for both players together. Emptied cells
  never regrow (D-067 item 5: regrowth only where ash remains); these maps have no pitchblende.
- Ore near each base runs out at ticks 8 000-13 000 and map-wide at 13 500-23 000; each side's total income stops at about 38-48k.
- Decisive games end about when the ore runs out. Draws are games where the fight isn't finished when the money stops.
- Not the cause: harvesters and refineries mostly survive (1-2 each at t30000, idle with no ore). Every queue stall is a money stall.
  The Broke rule sells structures down to tier 1 by design.
- Minor: ProductionManager keeps reserving the next vehicle price (350-800) while idle harvesters are alive. 88-462 credits are left unspent
  at t30000.

## What the draws look like (8 traced)
| Type | Games | Detail |
|---|---|---|
| Frozen winner | 2 | a4: 5 units on AttackMove stop 4-6 cells from the enemy yard (target pick only takes buildings in weapon range; the "next to goal" rule needs 1 cell). d13: 10 conscripts stuck in EnterTunnel after their hatches were sold by the Broke rule; the sweep has no known enemy building |
| Too slow / clock ran out | 4 | the loser's last buildings lose HP slowly, or the attacker arrives late |
| Last units die at the last building (D-233 pattern) | 2 | x1, x3 |

The mop-up sweep arms ("beaten") but never launches with 4-7 units: MopupDue still requires MinArmy 8 + guard. D-233's MinArmyNoEnemy only
changes Beaten().

## Options (owner picks)
- **A. Spend down when Broke.** The AI spends its last cash on the dearest affordable unit and cancels stalled lanes for the refund. AI only;
  low risk, about +1-4 units per side.
- **B. Unfreeze the winner.** (B3) never sell a hatch while units are entering it; (B2) a wave idle 1 500+ ticks within 8 cells of a known enemy
  structure attacks the nearest one; (B1) MopupDue uses the 4-unit minimum when no enemy army was seen. AI only; low-medium risk; aims at 6 of 8
  traced draws.
- **C. Ore regrowth or richer fields.** A sim rule (hash) and the balance of every matchup; high risk; only moves the exhaustion tick later.
- **D. Adjudicate.** Score the game at the time limit, or end it when both sides are broke with no army. A harness or rules change; a sim defeat
  rule would change D-173.

Recommendation (helper and lead): B (B3, B2, B1) plus A, re-measured on the same three pairs; C only after that; D as a fallback.

## After the fixes (D-235, wp/s23-endgame, cloud)
| Pair | Games | Before W-L-D (mean end) | After W-L-D (mean end) |
|---|---|---|---|
| Authority mirror, seeds 1-8 | 16 | 6-6-4 (20240) | 7-6-3 (19264) |
| Directorate mirror, seeds 9-20 | 24 | 8-12-4 (18660) | 10-12-2 (18757) |
| Authority v Directorate, seeds 1-8 | 16 | 3-9-4 (17717) | 4-9-3 (16981) |
Draws 12/56 (21 %) -> 8/56 (14 %). B2 (idle wave attacks a nearby structure) and the mop-up fired in traced games that used to draw. The
remaining draws are mostly "attacker arrives late" (e.g. Authority mirror s6g2: the last wave reaches the yard at ~27500 and the clock runs
out), which needs a faster launch or adjudication (option D). Laptop series to confirm.

## Session 24: endgame launch (D-242, wp/s24-draw, cloud)
`EndgameLaunch` (AI only): when the economy rescue says Broke, or the tick is 18 000+ with no harvester alive, `MilitaryManager.Think` launches with 2+ units,
home guard 0, no staging (straight to Assault), and later units join the running wave. Same flags as above (Authority seeds 1-8, tiered:3, rust_belt layout 1, size 96, 30 000 ticks).
| Pair | Games | Before W-L-D (mean end) | After W-L-D (mean end) |
|---|---|---|---|
| Authority mirror | 16 | 7-6-3 (19264) | 8-5-3 (19982) |
| Authority v Directorate | 16 | 4-9-3 (19267) | 4-8-4 (20308) |
Result: no gain (draws 6 -> 7 of 32, mean end tick later). Games reshuffle (the AI is chaotic), so single draws move both ways. Traced mirror s6g2 after the change: the draw is
not a late launch. Both sides end with 0 army; side A has 1345 credits, tier 2, a barracks and no harvester, and never spends it on infantry (production issue, not the launch).
