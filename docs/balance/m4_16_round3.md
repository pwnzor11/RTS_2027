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

## 3. Noise warning (measured)

A 16-game cell resolves only about +-12 points. Authority 2 v 1 on seeds 1-8 gives 5 / 11 / 0 (31 %), on seeds 9-16 8 / 4 / 4 (50 %), and
on 32 seeds (64 games) 32 / 27 / 5 (**50 %**). So round 2's "inverted ladder" at 2 v 1 was largely sampling noise; 3 v 2 on 32 seeds is
22 / 33 / 9 (34 %), a real deficit. Decisions below use 32 seeds x 2 sides (64 games) per cell.

## Status at stop (session 8)

The owner ended the session; round 3 is **not finished**. No AI change is committed on this branch (only the Tools diagnostic and this doc).
The experiment code (env-var knobs in `src/AI/Recruit/Exp.cs`, untested, not for main) is saved on branch `wp/m4-16r3-wip`.
Not done: the Directorate ladder with any change (a 16-seed base/candidate run was queued and stopped), cross-faction and mirror with
a change, the combined elite/commissar cells, the Q-43 scratch table, the draws investigation, test updates, the full suite.
All cells are Authority, map 56, 25 000 ticks, A = higher tier, "A / B / draw" games; 8 seeds = 16 games, 32 seeds = 64 games. 16-game
cells are noise-limited (+-12 points; see section 3), so only the 64-game rows are conclusions.

### What was tried (names are the knobs in the WIP branch)

- **Tech hold on** (`TierPlanner.TechHoldEnabled`): 8 seeds 2v1 5/11/0, 3v2 6/9/1 (base 5/11/0, 6/10/0). On top of the wave gate, 32 seeds 3v2: 28/30/6 vs
  29/31/4 without it. No effect (the AI rarely sees the enemy army, so "behind" seldom fires). Left off.
- **Wave strength gate** (`MilitaryManager`, tier 2+ with respond_to_attacks): a wave launches only when our army value is at least
  `GATE_PCT` % of the biggest enemy army seen at once in the last 3 600 ticks (when one was seen), unless 24 units or 6 000 ticks since the
  last wave. 8 seeds 3v2: 100 % 7/7/2, 130 % 4/12/0 (2v1 5/11/0 and 4/12/0).
- **Escalation after a failed wave** (`ESC_PCT`): a wave that loses all but a quarter of its roster, or is called off, raises the army value
  the next wave waits for to ESC_PCT % of the failed wave's launch value (cap 12 000; reset when a wave goes stale). 8 seeds 2v1 / 3v2:
  150 % 7/7/2, 7/8/1; 200 % + gate 100 % 8/7/1, 8/7/1 (best); 250 % 4/12/0, 7/9/0; with a value-per-tick floor (`TIMEVAL` 700) 5/11/0, 7/9/0 and
  with 150 % 7/7/2, 6/8/2.
- **Ablations of tier-2 behaviour (2v1, 8 seeds, base 5/11/0):** all M4 respond_to_attacks features off 10/5/1 (but on seeds 9-16 8/4/4 equals
  the base 8/4/4, so this is noise); repair+replace_harvesters+scatter off 4/11/1; decision interval 60 3/11/2; one at a time off: garrison 6/9/1,
  in-progress reserve 7/8/1, tier-2/3 bags 5/11/0, home-guard call 5/11/0, stale-assault end 5/11/0, regroup 4/11/1, stuck watchdog 6/10/0,
  ahead staging 3/13/0, defend harvester 4/10/2, production-first goal 5/11/0. Nothing stands out.
- **Elite (4v3, 32 seeds, base 17/26/21 = 26 %)** with the wave gate (200/100): 21/35/8 (32 %). On top of that: airfield/superweapon only at 16 / 24 units
  19/31/14, 20/31/13 (no effect); retreat 25 -> 0 21/29/14; scouting+harass off **26/30/8 (40 %)**; threshold 7 -> 10 23/29/12; expansion 3 600 -> 4 800
  19/37/8. The combined (scouting+harass off, threshold 10) cells for 4v3 and 5v4 were running when stopped.

### Results with 64 games per cell (the ones to trust)

| Cell | Base A/B/draw | Wave gate 100 + escalation 200 | Other |
|---|---|---|---|
| Authority 2 v 1 | 32 / 27 / 5 (50 %) | **40 / 19 / 5 (62 %)** | esc 150 alone: 40 / 15 / 9 (62 %) |
| Authority 3 v 2 | 22 / 33 / 9 (34 %) | **29 / 31 / 4 (45 %)** | esc 150: 26/34/4 (40 %); + hold 28/30/6 (43 %) |
| Authority 4 v 3 | 17 / 26 / 21 (26 %) | 21 / 35 / 8 (32 %) | scouting+harass off on top: 26/30/8 (40 %) |
| Authority 5 v 4 | 22 / 32 / 10 (34 %) | 18 / 28 / 18 (28 %) | not improved (draws up) |

### Diagnosis stays as in section 2 (plus, new)

- The 2 v 1 "inversion" of round 2 was mostly noise (base 50 % on 64 games). The real deficits are 3 v 2 (34 %), 4 v 3 (26 %), 5 v 4 (34 %).
- Elite builds the Authority airfield (2 000 credits) at tick ~3 300 with 9 units (4v3 seed 1 game 1: army 4 vs 7 at tick 5 000, 0 vs 15 at 7 500); but
  delaying it to 16 / 24 units changed nothing, so it is not the main drain. Scouting + harass off was the only elite change that moved (40 %).

### What looked promising / exact next step

1. Make the **wave strength gate with escalation** (200 / 100, constants as `(tune)` in `MilitaryManager`/`TierPlanner`, state in Snapshot/Restore - the WIP
   branch already adds `_launchValue`, `_required`, the long enemy peak) a real, flag-gated feature (respond_to_attacks), then run the AI and Game tests and
   update the pinned-outcome tests it changes (list them in this doc).
2. Measure it with 32 seeds on the **Directorate** ladder (base vs change) and the cross/mirror cells before keeping it; the Directorate cells were not run.
3. Try scouting + harass off for elite/commissar (difficulty.toml, behaviour only) with 32 seeds on 4v3 / 5v4; then the draws question on Directorate.
4. Then the Q-43 scratch run (tier gate 2 500 -> 2 000, revert after) and the table for the owner. Proposals only; nothing in data/rules was touched.
5. Use 32 seeds (64 games) for every keep/revert decision.
