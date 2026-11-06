# S12 · LLM commander, owner's key test (M5-10, M5-11)

Run by the owner on his PC, 2026-11-06: `ai commander --a tiered:3 --b tiered:3 --provider openai --model gpt-5.4-mini`, map 64,
30000-tick limit. Side A = tier-3 AI with LLM directives (persona Brigadier Hester Wrenfield); side B = plain tier 3.

## Reliability
After the max_completion_tokens fix (PR #30): 120 calls over 5 games, 120 ok, 0 errors, latency 0.7-2.9 s (median about 0.9 s).
Before the fix: every call HTTP 400.

## Results
| Seed | Winner | End tick |
|---:|---|---:|
| 1 | A (commander) | 12684 |
| 2 | A | 12956 |
| 3 | A | 20700 |
| 4 | B | 13676 |
| 5 | A | 13372 |

Commander side 4-1. Baseline, plain tier 3 vs tier 3 on the same seeds with starts swapped (10 games): A 4, B 5, 1 draw, and the
start-0 corner won 6 of 9 decisive games. The commander side always played from start 0 (the command has no swap), so its expected
rate without the LLM is about 67 %. 4/5 is within noise of that: the test shows the commander works and plays sensibly, not that it
is stronger.

## How it played
- Opens with 3-6 decisions of turtle/economy, switches to army at about tick 3000-4200, names a target cell once it has seen the
  enemy, goes aggressive when ahead and walks the cell toward the enemy yard.
- Seed 4 (the loss): after a failed push at tick 9600 it chose turtle/economy for six decisions in a row ("rebuild first") while the
  enemy pressed, and lost. Candidate prompt change: tell the commander that turtle slows the army and is for holding a lead, and
  to name a defensive cell at its own base when the enemy army is near. Not changed yet (owner to decide).

## Next (optional)
A fair strength test needs `--swap` support in `ai commander` and 16+ games per side; cost is about 25 calls per game.

## Test 1 (strength, both corners) — 2026-11-06
`ai commander --a tiered:3 --b tiered:3 --model gpt-5.4-mini`, seeds 1-16, `--swap 0` and `1` (32 games). Baseline: `ai match --a tiered:3
--b tiered:3 --seeds 16` (32 games, same seeds, starts swapped): 15-16-1, start 0 won 20 of 31 decisive games.

| | Commander side wins-losses-draws | Commander win rate (decisive) | Plain tier 3 from that corner |
|---|---|---:|---:|
| start 0 (`--swap 0`) | 11-5-0 | 69 % | 65 % (20/31) |
| start 1 (`--swap 1`) | 5-9-2 | 36 % | 35 % (11/31) |
| both | 16-14-2 | 53 % | 50 % |

Reliability: 893 calls, 893 directives, 0 errors; latency median 0.9 s, 95th percentile 1.2 s, max 4.7 s.

**Reading:** no measurable strength gain from the commander at tier 3 (the corner explains the whole split; a real effect smaller than
about 15 points would not show at 32 games). Pattern worth acting on: a run of 3+ "turtle" decisions after the commander has started
fighting appears in 0 of its 16 wins and in 10 of its 16 losses/draws (cause or symptom unclear: it may turtle because it is already
losing). The candidate prompt change from seed 4 (turtle is for holding a lead; defend at home with a cell instead) is the next test.

## Test 3 (bigger model, old prompt) — 2026-11-06
Same as Test 1 but `--model gpt-5.4`, seeds 1-8, both corners (16 games); compared with the same 16 games of Test 1 (gpt-5.4-mini).

| Model | Wins-losses-draws | Decisive win rate | Latency median / p95 | Calls, errors |
|---|---|---:|---|---|
| gpt-5.4-mini | 7-9-0 | 44 % | 0.9 s / 1.2 s | 405, 0 |
| gpt-5.4 | 9-5-2 | 64 % | 1.5 s / 1.9 s | 387, 0 |

Six of the 16 outcomes changed: four losses became wins, two wins became draws (tick limit). Suggestive, not conclusive at 16 games.
The bigger model turtles even more (turtle is 34-71 % of its decisions in its losses and draws, runs of 9-20 in a row) but wins faster
when it wins (average end tick 11 000 vs 15 700 for its losses). Every loss and draw has a long turtle run, so the posture guide (D-160)
matters for both models. Next: Test 1b (mini, new prompt), then optionally gpt-5.4 with the new prompt.

## Test 1b (mini, posture guide D-160) — 2026-11-06
Same 32 games as Test 1 with the new prompt.

| | Test 1 (old prompt) | Test 1b (posture guide) |
|---|---|---|
| Start 0 | 11-5-0 | 9-7-0 |
| Start 1 | 5-9-2 | 8-6-2 |
| Total | 16-14-2 (53 % decisive) | 17-13-2 (57 % decisive) |
| Postures chosen | turtle 16 %, balanced 52 %, aggressive 30 % | balanced 94 %, aggressive 5 %, turtle 0 % |

15 of 32 outcomes changed (8 better, 7 worse): noise. Calls 901, errors 0, latency median 0.86 s. The guide removed turtle completely
but mini now says "balanced" almost every time and rarely presses (aggressive 30 % → 5 %), so it adds little over the built-in AI.
Reading: with gpt-5.4-mini the commander is reliable flavour (persona lines, sensible targets), not a strength gain; the bigger model
showed a hint of real gain (Test 3). Next if wanted: gpt-5.4 with the new prompt (16 games).

## Test 3b (gpt-5.4, posture guide) and summary — 2026-11-06
Seeds 1-8, both corners (16 games each), commander side wins-losses-draws:

| Run | Model | Prompt | Result | Postures |
|---|---|---|---|---|
| Test 1 (seeds 1-8) | gpt-5.4-mini | old | 7-9-0 | turtle 16 %, balanced 52 %, aggressive 30 % (all 32 games) |
| Test 1b (seeds 1-8) | gpt-5.4-mini | D-160 | 9-5-2 | balanced 94 %, aggressive 5 % |
| Test 3 | gpt-5.4 | old | 9-5-2 | heavy turtle in losses |
| Test 3b | gpt-5.4 | D-160 | 6-10-0 | balanced 90 %, aggressive 9 % |

All four sit between 6 and 9 wins of 16, inside the noise of a 16-game cell (about ±12 points). Calls 379, errors 0, latency median
1.4 s. **Conclusion:** at tier 3 neither model nor prompt makes a measurable difference; the directive levers (posture, focus, mix,
target, cell) move the built-in AI too little for an LLM to win or lose games with them. The commander is reliable flavour (persona
voice, chat, sensible targets). Making it a strength lever is a design question (stronger directive effects), not a prompt one.
