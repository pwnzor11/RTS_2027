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
