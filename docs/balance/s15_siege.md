# S15 siege behaviour (Q-64, D-174)

Method: `Tools ai match --a tiered:A --b tiered:B --faction-a F --faction-b F --seeds 16 --size 96 --ticks 30000` (32 games per cell), Release build of the worktree,
tool copies per variant in a scratch dir. Traces with `ai timeline ... --trace N --dump 1`.

## Diagnosis (before any code)

Traced Directorate 5 v 1 (seeds 1 and 4, both drawn at 30 000 ticks).

1. **Seed 1 (the tier 1 side owns the field).** The tier 5 side loses its whole army by t12000 (army 1, then 0) and sits on shaft + vent + one bunk gallery with no cash.
   The tier 1 side has 90 units at home at t30000 and does not finish it. Cause: its single wave (14 roster members) is still in `Assault` since t9000. Five hurt
   conscripts (hp 10-20) stand at 88,90 attack-moving at the shaft (hp 927, repaired/barely scratched), the others are `Move`ing around home. A tier 1 profile has
   `respond_to_attacks = 0`, so none of the end-of-wave rules runs (`Stalled`, `Regroup`, `Unstick`); the roster never drops below a quarter of the launch size, so the
   wave never ends, and the 70 idle units wait at the rally cell for a "next wave" that cannot start. This is a wedge, not a siege problem.
2. **Seed 4 (tier 5 is ahead and slow).** Tier 5 reaches 30 units only at t19500 (income is thin: cash 0 for most of the game, one scraper). Before that it sends
   waves of 8-17 units against a base with pillbox, rad mortar (range 9, splash), a redoubt and 5 bunk galleries, and loses them (army 17 -> 7 at t12000-t18000).
   Waves 6-10 (t21000-t30000) are 20-35 units and do grind the base down (4 buildings left at t30000, no enemy army) but the game is over about 3 000 ticks too late.
   The loser keeps stray hatches (87 hp) that count as bases.
3. Common to both: waves are launched on size/time only, with no idea of how strong the defence is; targets are "nearest building", so the army walks past nothing
   and attacks whatever is closest (often a gallery inside mortar cover) instead of clearing outside-in; every unit attack-moves to the same cell, so long-range
   units (rocket truck range 8, howitzer 10, mortar 9) fight inside the range of the same defences instead of outside it (pillbox 3, turret 5); production structures are
   only preferred while `Ahead`.

Second finding while measuring (seed 11/2/13, 5 v 1): the "no progress" test of `Stalled` reset its low-water mark on every dip, and the defender's repair makes health
zig-zag, so a wave standing in front of a repaired structure never counted as stalled (70 idle units at home for 12 000 ticks, one 14-unit wave "on the attack" for 20 000 ticks).
Third: `_required` (200 % of the last lost wave's value, up to 12 000) kept a 20-unit army at home for 4 000+ ticks against an enemy with no army at all. Fourth: when the
last scraper died with a rescue "harvester" already in the queue but stalled for lack of money, `EconomyRescue` returned early ("already bought") and never sold anything, so
the side sat on 0 credits for the rest of the game (seed 11).

Why Directorate tier 5 underperforms (lead's question): not proven here. Traces show the high-tier Directorate side spends its first 6 000 ticks on tech structures and air/aircraft
(income 0, 15-19 structures, army 7-19) and loses early waves of 8-17 units to a base with a rad mortar and bunk galleries (seed 4: army 17 -> 7), i.e. its army is smaller than a tier 1
side's at the same tick. Siege gathering waits for a bigger force, which helps, but the early economy/tech split is outside this package.

## What changed (src/AI only; no unit or building numbers)
- `SiegePlan` (new, pure): defence estimate (armed ground structures + garrison bunkers, credits), army value needed = defence value x tier % (tier 2 60, 3 80, 4 100, 5 120; tier 1 none; cap 7 000),
  minimum wave size by tier (7/8/9/10), target scoring, artillery test (ground weapon range >= 7 cells), "covered by a known defence" test.
- `MilitaryManager`: a wave is held back (siege hold) while the target player's known defence value is >= 800 and the army is below the need, up to 9 000 ticks after the last wave or 40 units.
  A siege wave stages at the first target, takes no second prong, attacks one building at a time (nearest defence first; a production/tech-root/refinery structure that no known defence
  covers jumps the queue), re-targets its members when the target changes. With `counter_defences` (tiers 3-5) the artillery (range >= 7) gets explicit Attack orders on the current target
  and, with >= 3 of them and a target they outrange, the direct-fire units hold a line at the guns' firing distance instead of charging. A siege wave is called back for reinforcements only
  by a group at least as big as itself. While the enemy has shown no army for the whole memory window, the escalated requirement is dropped and staging is close (6 cells).
- `Stalled` now measures real progress (new low of target health) and also applies to tier 1.
- `EconomyRescue` sells for a harvester that is queued but stalled.
- Save/load: `_siegeWave _siegeHave _failedWaves SiegeWaves SiegeHolds _siegeId _siegeIssued _siegeCell` appended to MilitaryManager.Snapshot/Restore (SiegeSaveTests round-trip + Authority/Directorate save-mid-siege hash tests).
- Left Behind / Expedition use the same MilitaryManager (their tactics files are untouched).

## Before / after (16 seeds x 2 = 32 games per cell, size 96, 30 000 ticks, A = first tier/faction; avg end tick in brackets)

| Cell | Before (A / B / draw) | After (A / B / draw) |
|---|---|---|
| Directorate 5 v Directorate 1 | 11 / 6 / 15 (25285) | 16 / 6 / 10 (24884) |
| Directorate 4 v Directorate 3 | 10 / 12 / 10 (22745) | 7 / 12 / 13 (23224) |
| Directorate 3 v Authority 3 | 10 / 15 / 7 (20951) | 11 / 14 / 7 (20259) |
| Directorate 5 v Authority 5 | 6 / 18 / 8 (20545) | 5 / 17 / 10 (20725) |
| Authority 5 v Authority 1 | 16 / 11 / 5 (17934) | 20 / 9 / 3 (17868) |
| Left Behind 3 v Directorate 3 | 23 / 4 / 5 (15686) | 26 / 4 / 2 (14042) |
| Expedition 3 v Authority 3 | 15 / 13 / 4 (15947) | 16 / 16 / 0 (14356) |

Context (owner's laptop, pre-siege code, 64 games per row, seeds 1-32): Dir 3 v Auth 3 23/23/18, Dir 5 v Auth 5 10/34/20, Dir 5 v Dir 1 22/8/34, LB 3 v Dir 3 44/7/13, Auth 5 v Auth 1 41/18/5, Exp 3 v Auth 3 33/26/5.
32 games per cell: noise about +-18 points on a decisive %, +-3 on a draw count.

## Reading (honest)
- The target (Directorate draws down to <= ~4 / 32) is NOT reached. Directorate 5 v 1 improves clearly (draws 15 -> 10, A wins 11 -> 16: the tier ladder is better, 73 % decisive vs 65 %).
  Directorate 4 v 3 got slightly worse (draws 10 -> 13, A 10 -> 7), within the noise band but in the wrong direction; Dir 3 v Auth 3 is unchanged and Dir 5 v Auth 5 is +2 draws.
- Non-Directorate rows moved the right way or stayed: Authority 5 v 1 20/9/3 (was 16/11/5), Left Behind 3 v Dir 3 26/4/2, Expedition 3 v Authority 3 16/16/0 (draws gone, balance still even).
- Where draws remain (traced): the loser keeps shaft + a few structures behind bunk galleries while the attacker either has no army at that moment (waves keep dying one by one in 5 v 1 seeds 8 and 11 -
  the tier 5 side is itself starved: 0 credits, no scraper) or sits on a very high escalated requirement. Several remaining draws are symmetric economic collapses, not siege failures.
- Tuning history (5 v 1, 32 games, draws): first version (need 100 %/tier, wave size 12-18) 17 -> after stall fix 15 -> need 60 % of the defence value and waves of 7-10 with close staging: 10.
  Raising the gather thresholds made draws worse every time (more idle time at home), so the shipped numbers are the low ones.
- Mop-up switches (`MopupLaunch`, `MopupSweep`): left false. Siege does not make them redundant for the stray-hatch case (a side alive only on an unseen 87 hp hatch is still not found by the
  siege, which only attacks known buildings); it does replace their other purpose (finishing a base with no army left) in the cases that were base stalemates.
- Side effects: none seen in the tier ladder for Authority and Left Behind; Expedition v Authority is now an exact 16/16. Games end on average 0-1 400 ticks earlier except the Directorate cells.

## Open / next
- Directorate 4 v 3 and the mirror-ish cells need a second look (is the tier-4 Directorate gather too long for its thin economy? a Directorate-only `siege percent` in difficulty.toml would be the knob; not added, balance numbers are the owner's).
- A rerun on the laptop series (64 games) would tighten the noise.
