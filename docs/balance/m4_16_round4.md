# M4-16 round 4: wave strength gate and escalation made real, ladder measured, Q-43 table

Build: branch wp/m4-16r4 (integ-m6 at 42158ad plus the commit below). Method as round 3: `Tools ai match`, 32 seeds x 2 sides = 64 games per cell,
size 56, 25 000 ticks, A = the higher tier (or Authority in the cross cell). Games are deterministic: a re-run reproduces every number. Raw outputs: scratch/r4 (git-ignored).
Win % = A wins / 64 (draws count as not won). "Decisive %" = A wins / (A + B wins), shown because Directorate games are mostly draws.

## 1. What changed (AI behaviour only; no rules numbers, nothing in data/rules)

- New `src/AI/Recruit/WaveGate.cs` (pure decisions, constants marked `(tune)`): for tier 2+ with `respond_to_attacks`, a wave leaves only when
  our army value >= 100 % of the biggest enemy army seen at once in the last 3 600 ticks (when one was seen) and >= the escalated requirement;
  the gate lifts at 24 units at home or 6 000 ticks after the last wave. A wave that loses all but a quarter of its roster, or is called off by the
  retreat rule, raises the requirement to 200 % of its launch value (cap 12 000 credits); a stale wave resets it. Tier 1 is unchanged.
- `MilitaryManager` (gate, `_launchValue`, `_required`; both added to Snapshot/Restore), `TierPlanner` (the 3 600-tick enemy peak `_longPeak`, saved too),
  `RecruitAi` (passes our army value and the enemy peak to the military manager each decision).
- Tech hold stays off (round 3: no effect). No scratch Exp.cs code is in the commit.
- Tests: `tests/AI.Tests/M4/WaveGateTests.cs` (7 unit tests: tier 1 ungated, no enemy seen, below / at the gate, unit cap and wait lift, escalation floor / cap,
  required value). AI.Tests 109/109 including the AI save->load determinism tests and `ADirectorateMirrorAtVeteranDoesNotFreeze`. No pinned outcome moved in AI.Tests.

## 2. Before / after (A / B / draw of 64 games; base = integ-m6 without the gate)

| Cell | Base | Round 4 | Round 4 win % | Decisive % |
|---|---|---|---|---|
| Authority 2 v 1 | 32 / 27 / 5 | **40 / 19 / 5** | 62 % | 68 % |
| Authority 3 v 2 | 22 / 33 / 9 | 29 / 31 / 4 | 45 % | 48 % |
| Authority 4 v 3 | 17 / 26 / 21 | 21 / 35 / 8 | 33 % | 38 % |
| Authority 5 v 4 | 22 / 32 / 10 | 18 / 28 / 18 | 28 % | 39 % |
| Directorate 2 v 1 | 20 / 5 / 39 | 20 / 7 / 37 | 31 % | 74 % |
| Directorate 3 v 2 | 27 / 12 / 25 | 25 / 11 / 28 | 39 % | 69 % |
| Directorate 4 v 3 | 6 / 26 / 32 | 12 / 20 / 32 | 19 % | 38 % |
| Directorate 5 v 4 | 15 / 13 / 36 | 15 / 22 / 27 | 23 % | 41 % |
| Cross: Authority 3 v Directorate 3 | 19 / 16 / 29 | 19 / 17 / 28 | 30 % | 53 % |
| Mirror Authority 3 v 3 | 24 / 26 / 14 | 31 / 25 / 8 | 48 % | 55 % |
| Mirror Directorate 3 v 3 | 15 / 23 / 26 | 15 / 19 / 30 | 23 % | 44 % |

Authority base rows are the round-3 numbers (the 3 v 2 base was re-run on this branch and reproduces 22 / 33 / 9 exactly); the Directorate, cross and
mirror base rows were run now. Mirror cells are the sanity check for start bias: a perfect mirror would be 50 % decisive; Authority is, Directorate is not (44 %, start-corner effect).

Reading: the gate fixes the lower rungs (Authority 2 v 1 reaches the 60 % target; 3 v 2 gains 11 points) and Directorate 4 v 3 gains 10 points, but
**the target of >= 60 % for the higher tier is met only for Authority 2 v 1** (and for Directorate 2 v 1 / 3 v 2 on decisive games: 74 % / 69 %).
Authority 4 v 3 and 5 v 4 and Directorate 4 v 3 / 5 v 4 stay below 50 % of decisive games: the gate is not the main problem at the top two rungs.
Directorate 5 v 4 moves the wrong way on decisive games (15 / 13 to 15 / 22); the other changes in the table are within the +-12 points a 64-game cell can resolve.

## 3. Elite scouting + harass off (data/ai/difficulty.toml `[elite]`, tried in the measure worktree only; **not kept**)

| Cell | Round 4 | Elite scouting + harass off |
|---|---|---|
| Authority 4 v 3 | 21 / 35 / 8 (33 %) | **26 / 30 / 8 (40 %)** |
| Authority 5 v 4 | 18 / 28 / 18 (28 %) | 16 / 30 / 18 (25 %) |
| Directorate 4 v 3 | 12 / 20 / 32 (19 %) | 2 / 23 / 39 (**3 %**) |
| Directorate 5 v 4 | 15 / 22 / 27 (23 %) | 17 / 16 / 31 (27 %) |

It helps Authority 4 v 3 only and cripples Directorate elite (scouting/harass is how it finds and hits targets), so it fails the "helps both" rule and is reverted.

## 4. Q-43 table: tier-3 gate building and tier-3 upgrades 2 500 vs 2 000 (measured only; not applied)

Scratch copy of `data/` with `cost = 2000` for auth_signals_hq, dir_committee_hall and the four tier-3 upgrades (auth_up_tungsten_shot, auth_up_signals_net, dir_up_order_seven,
dir_up_fervour), in the measure worktree's scratch/q43. Same AI (round 4). data/rules in the repo is unchanged.

| Cell | 2 500 (A / B / draw, win %) | 2 000 (A / B / draw, win %) | Delta |
|---|---|---|---|
| Authority 2 v 1 | 40 / 19 / 5 (62 %) | 40 / 19 / 5 (62 %) | 0 (no tier-3 spending) |
| Authority 3 v 2 | 29 / 31 / 4 (45 %) | 29 / 34 / 1 (45 %) | 0 |
| Authority 4 v 3 | 21 / 35 / 8 (33 %) | 21 / 35 / 8 (33 %) | 0 |
| Authority 5 v 4 | 18 / 28 / 18 (28 %) | 20 / 25 / 19 (31 %) | +3 |
| Directorate 2 v 1 | 20 / 7 / 37 (31 %) | 20 / 7 / 37 (31 %) | 0 |
| Directorate 3 v 2 | 25 / 11 / 28 (39 %) | 25 / 11 / 28 (39 %) | 0 |
| Directorate 4 v 3 | 12 / 20 / 32 (19 %) | 12 / 21 / 31 (19 %) | 0 |
| Directorate 5 v 4 | 15 / 22 / 27 (23 %) | 18 / 19 / 27 (28 %) | +5 |
| Cross A3 v D3 | 19 / 17 / 28 (30 %) | 21 / 16 / 27 (33 %) | +3 |
| Mirror Authority 3 v 3 | 31 / 25 / 8 (48 %) | 27 / 26 / 11 (42 %) | -6 |
| Mirror Directorate 3 v 3 | 15 / 19 / 30 (23 %) | 13 / 17 / 34 (20 %) | -3 |

All deltas are inside the +-12 point noise of a 64-game cell. Cheaper tier 3 has no measurable effect on the ladder.

## 5. Draws

Directorate cells are 40-60 % draws at 25 000 ticks (27-39 of 64 games), Authority 8-30 %. The gate does not change that for Directorate (2 v 1: 39 to 37 draws) and lowers
Authority draws at 3 v 2 / 4 v 3 (9 to 4, 21 to 8) but raises them at 5 v 4 (10 to 18): at the top rung both sides wait for the other to be weaker. Not investigated further.

## 6. Recommendation for the owner

1. Keep the gate + escalation (AI only, flag-gated by respond_to_attacks, tests green): clear gain at Authority 2 v 1 / 3 v 2, Directorate 4 v 3, Authority mirror; no cell is clearly worse.
2. Do **not** take the Q-43 change (2 500 to 2 000): no cell moves beyond noise, so it is not needed for balance and not worth a player-visible number.
3. Do not take elite "scouting + harass off": it hurts the Directorate elite badly.
4. The ladder is still not monotone at tiers 4 and 5 (higher tier wins about 38-41 % of decisive games, Directorate 4 v 3 and 5 v 4, Authority 4 v 3 and 5 v 4). The remaining cause is not the wave
   policy; the next lever is how elite/commissar spend (air and superweapon structures, round 3 found no effect from delaying them) or whether those two tiers should be tuned through
   the profile numbers (attack_threshold 7 / 6) instead; proposal for a later round, not done here.
