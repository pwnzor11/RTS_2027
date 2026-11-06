# S12 AI ladder: strength-ratio attack gate, predicted-loss retreat (M4-16)

Method: `Tools ai match --a tiered:A --b tiered:B --faction-a F --faction-b F --seeds 32 --size 96 --ticks 30000`, 32 seeds x 2 sides = 64 games per cell,
medium map, same faction on both sides (mirror). A = the higher tier. Games are deterministic. A / B / draw are game counts out of 64.
"Decisive %" = A wins / (A + B wins). Target: higher tier >= 55 % decisive in every cell, no inversion, tier 5 vs 1 >= 90 %.

## Result: target NOT reached

| Cell (A v B) | Before (A / B / draw) | After (A / B / draw) | Decisive before -> after |
|---|---|---|---|
| Authority 3 v 2 | 29 / 30 / 5 | 27 / 31 / 6 | 49 % -> 47 % |
| Authority 4 v 3 | 13 / 38 / 13 | 21 / 33 / 10 | 25 % -> 39 % |
| Authority 5 v 4 | 21 / 25 / 18 | 17 / 36 / 11 | 46 % -> 32 % |
| Directorate 3 v 2 | 34 / 3 / 27 | 34 / 3 / 27 | 92 % -> 92 % |
| Directorate 4 v 3 | 4 / 40 / 20 | 12 / 33 / 19 | 9 % -> 27 % |
| Directorate 5 v 4 | 10 / 26 / 28 | 11 / 33 / 20 | 28 % -> 25 % |
| Authority 5 v 1 | not run | 22 / 37 / 5 | - -> 37 % |
| Directorate 5 v 1 | not run | 3 / 9 / 52 | - -> 25 % |

Only the 4 v 3 cells improved (Authority +8 wins, Directorate +8 wins; +-12 points of noise at 64 games, so both are suggestive, not proven).
Tier 5 is still weaker than tier 4 and tier 1 in the Authority mirror, and tier 5 v 1 is far from 90 %. 3 v 2 is unchanged (Authority shows the inversion at the
lower rungs too; Directorate 3 v 2 is already strong). The 3 v 2, 4 v 3 After rows come from the run with tier 2-4 settings identical to the committed
profile (commissar differed only in `retreat_health_pct`, irrelevant to those cells); 5 v 4 and 5 v 1 are from the committed profile itself.

## Draw rate and the tick limit

`ai match` / `ai matrix` default `--ticks` is now 30 000 (was 6 000; D-153 (7)). Authority 4 v 3 draws: 33 of 64 at 18 000 ticks, 13 at 30 000, 9 at 40 000.
At 30 000: baseline draws are 5-18 of 64 in Authority and 20-28 in Directorate; after the change 6-11 (Authority) and 19-52 (Directorate; the 5 v 1 Directorate cell is 52 draws).
Directorate remains draw-heavy at any limit tried; the normal game has no limit.

## Changes (old -> new)

- New data keys in `data/ai/difficulty.toml` (loader `DifficultyLoader`, record `DifficultyProfile`): `attack_ratio_pct` (0 = 100), `predicted_retreat` (0/1), `retreat_ratio_pct` (0 = 70).
- Lever 1, `attack_ratio_pct` (the wave leaves only at this percent of the biggest enemy army seen in the last 3 600 ticks; `attack_threshold` stays as the minimum size):
  tier 2 / 3 / 4 / 5 = 100 / 110 / 125 / 140 (was a fixed 100 at every tier 2+). `WaveGate.Strong` gets the ratio.
- Lever 2, `predicted_retreat = 1` for elite and commissar: during the assault, own wave value within 9 cells of its centre against visible enemy units and armed
  structures within 11 cells (integer credits, veterancy-weighted, only if the enemy side is worth >= 600) below 70 % calls the wave off (`WaveGate.Losing`, `MilitaryManager.PredictedLoss`).
- Elite `retreat_health_pct` 25 -> 0 (single-unit health retreat and the group call-off). Commissar keeps 25: with 0, `TheAiNeverSitsCappedForMoreThanAMinuteInTheFirstFifteen(Directorate, 5)` fails (capped 248 s); the cause was the zero retreat, not the ratio or predicted retreat (checked by toggling each).
- Lever 3 (scouting split from harassment): the flags already are separate (`scouting`, `harass`). Elite with scouting and harassment off measured 14 / 35 / 15 in Authority 4 v 3 (baseline 13 / 38 / 13): no gain, so the profile is unchanged.
- Lever 4 (defenders by tier): not tried, since levers 1-3 left the top rungs flat and the screens below point at other causes.
- `ai match` / `ai matrix` default `--ticks` 6 000 -> 30 000 (usage text and default literals only).

## What the screens showed (Authority 4 v 3 unless noted, 30 000 ticks, A / B / draw)

| Variant | Result |
|---|---|
| Baseline | 13 / 38 / 13 |
| Ratios 100 / 110 / 125 / 140 only | 13 / 37 / 14 (no effect) |
| Ratios 100 / 120 / 150 / 180 + predicted retreat + elite no harass | 11 / 44 / 9 (worse) |
| Elite health retreat off | 21 / 35 / 8 |
| Elite scouting + harass off | 14 / 35 / 15 |
| Elite superweapon + expansion off | 5 / 52 / 7 (much worse: elite needs them) |
| Elite decision interval 20 -> 30 | 15 / 39 / 10 |
| Retreat off + ratios 100-140 | 18 / 35 / 11 |
| Retreat off + ratios + predicted retreat (kept) | 21 / 33 / 10 |

Reading: the strength-ratio gate alone does not move the result (the tier 3 / 4 armies are rarely smaller than the enemy's biggest seen army, so the gate seldom binds);
the one measurable lever is the elite health retreat, which pulls units out of fights the elite then loses piecemeal. Predicted retreat is neutral to slightly positive. The remaining deficit at 4 v 3 and 5 v 4
is not explained by wave timing; candidates for a next round are economy/expansion timing at tier 4-5 (turning the superweapon off cost 16 wins, so spending is already decisive) and how the top-tier armies are composed.

## Tests

- New `tests/AI.Tests/M4/LadderGateTests.cs` (6 tests): ratio 0 keeps the default gate, a higher ratio needs more value, the unit cap and wait still lift the gate, `Losing` minimum enemy force and ratio, shipped ratios never fall with tier.
- Re-pinned in `TierCapabilityTests`: `CapabilityFlagsOnlyEverGrowWithTheTier` now checks `Tier(4).PredictedRetreat` instead of `RetreatHealthPct > 0`; `EliteSendsAUnitThatIsNearlyDeadHome` and `EliteRetreatsAGroupThatHasLostTooMuch_RegularKeepsFighting` set `RetreatHealthPct = 25` explicitly (the flag still gates the capability; the shipped elite profile has it off).
- `ADirectorateMirrorAtVeteranDoesNotFreeze` and the pinned default-skirmish hash were not touched (see the full-suite result in the commit report).
