# M4-16 round 3: why the higher AI tiers lose, and AI-only fixes

Build: main at ed79d8c plus the Tools-only `ai match --trace N --game G` diagnostic (01cecd4; one extra optional observer parameter
on `AiMatchRunner.Play`, no behaviour change). Method as in round 2: `Tools ai match`, 8 seeds x 2 sides, size 56, 25 000 ticks, per-game
seeds (D-104). Raw outputs: scratch/r3 (git-ignored). Games are deterministic, so a re-run of the same build reproduces the numbers exactly.

## 1. Baseline (re-run on main): round-2 numbers reproduce exactly, cell for cell

| Pairing | Authority A / B / draw | A win % | Directorate A / B / draw | A win % |
|---|---|---|---|---|
| regular > recruit (2 v 1) | 5 / 11 / 0 | 31 % | 6 / 1 / 9 | 37 % |
| veteran > regular (3 v 2) | 6 / 10 / 0 | 37 % | 7 / 2 / 7 | 43 % |
| elite > veteran (4 v 3) | 5 / 5 / 6 | 31 % | 4 / 6 / 6 | 25 % |
| commissar > elite (5 v 4) | 5 / 10 / 1 | 31 % | 4 / 3 / 9 | 25 % |

Cross-faction at equal tier (Authority / Directorate / draw): t1 4/8/4, t2 5/4/7, t3 6/5/5, t4 9/2/5, t5 4/5/7.
Directorate veteran mirror: 5 / 8 / 3. Start-corner wins (start 0 - start 1): Authority ladder 5-11, 10-6, 4-6, 13-2;
Directorate ladder 5-2, 5-4, 7-3, 5-2; cross 9-3, 7-2, 5-6, 6-5, 7-2; mirror 11-2.

## 2. Diagnosis (traces with `ai match --first S --seeds 1 --game G --trace 2500`; "enemy" = true enemy army from the sim, not the AI's view)

Games traced from the higher tier's side (A): Authority 3 v 2 seeds 1, 2, 6 (game 1 each) and Authority 2 v 1 seed 3 (game 1). All four are
losses of A. The games end at tick 8 440 / 9 488 / 14 220 / 14 636, so the ticks 10 000 and 15 000 snapshots exist only where the game
lasted (seed 6: A army 8 units / value 1 650 vs B 23 / 7 550 at tick 10 000).

Army value (credits, harvesters excluded) at the fixed ticks, A (higher tier) vs B (lower tier):

| Game | t 2 500 | t 5 000 | t 7 500 | Outcome |
|---|---|---|---|---|
| 3v2 seed 1 | 2 400 vs 1 900 | 2 750 vs 4 300 | 0 vs 7 950 | B wins 8 440 |
| 3v2 seed 2 | 2 700 vs 2 100 | 2 700 vs 4 350 | 0 vs 8 850 | B wins 9 488 |
| 3v2 seed 6 | 2 500 vs 1 500 | 3 950 vs 3 550 | 4 400 vs 6 550 | B wins 14 220 (A 1 650 vs 7 550 at 10 000) |
| 2v1 seed 3 | 1 500 vs 2 000 | 3 800 vs 7 950 | 5 350 vs 7 500 | B wins 14 636 |

A leads (or is level) on army value at tick 2 500 and has the same income, then falls behind. Where it goes (numbers from the traces):

1. **The higher tier sends a stream of small waves into a bigger army, one after another.** Value of A's army at launch against the
   enemy's whole army at that moment (B's waves for contrast):
   - 3v2 seed 1: A waves at 2 611 / 3 601 / 4 831 / 5 971 launch at 92 % / 66 % / 56 % / 28 % of the enemy value; each loses most of its
     units. B's own waves go at 110 % / 139 % / 795 000 % (enemy army already dead).
   - 3v2 seed 6: A waves #4..#11 (tick 5 821 to 11 251) launch at 53 / 38 / 31 / 88 / 61 / 28 / 19 / 9 %. B waves go at 100–350 %.
   - 2v1 seed 3: A waves at 78 / 65 / 55 / 64 / 52 / 47 / 39 %; B at 143–666 %.
   - 3v2 seed 2: A wave #4 and #5 at 17 % and 13 % (7 units, then the 3 survivors).
   The timed trigger (`n >= MinWave (8)` and 3 000+ ticks since the last wave, and "due at once" after a stale or regrouped wave) launches
   whatever is at home; a failed wave makes the next one due sooner. The lower tier only has the threshold trigger (10 units + bias), so it keeps
   its army at home, where it also fights next to its own turret and blockhouse, and it grows to 20-30 units while A trickles away. Lower attack
   thresholds at higher tiers (8 / 7 / 6) make this worse the higher the tier.
2. **A's waves reach a defence that has all of its army home.** The enemy army in the same lines is 11-30 units; A launches 7-14 units.
3. **Tech spending adds to it at tier 3.** In the 3v2 games A has ordered 3 (seed 1) to 4 (seeds 2, 6) upgrades by tick 5 000, and the tier-2
   gate at tick ~2 372 (500 credits). With research costs of 800-1 500 each that is roughly 3 000-5 000 credits, a third of the 13 500-16 000
   income earned by then, while its army value is 2 750 vs 4 300. The tier-3 gate (2 500) was bought at tick 9 842 in seed 6 with cash 3 496
   and an army of 8 units against 23; the base lost its tier again by 12 377.
4. **Collapse: the base is sold.** After the last wave dies the side has `army 0`, `scrapers 0`, `sold 6` (the no-income rescue sells the spare
   structures) and the tier drops back to 1 (seed 1: tick 6 062; seed 2: 6 272), so the gate money is lost as well.
5. Not the cause: early economy (first refinery 600-643, factory 1 802-1 923, same in both), tier-2 timing, harvesters (1 each until
   expansion). The 2v1 loss has no upgrades at all (`up 0`), so tech spending cannot explain it: the wave policy alone does.

Conclusion: the dominant cause is **wave policy, not tech spending**: higher tiers attack on a clock/threshold with armies that are
28-90 % of what is defending, repeatedly, and feed the enemy; the lower tier wins by massing at home and counter-attacking when the
attacker is dead. Tech spending (3v2 and up) is a secondary drain. Fix order below: (a) tech hold as asked, then (b) a strength gate on
wave launches.
