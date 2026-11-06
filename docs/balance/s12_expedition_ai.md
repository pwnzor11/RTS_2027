# S12 Expedition AI per tier (M7-18)

Goal (11 section 6 bullet 1, D-153 (4)): the Expedition AI at tier N should win 40-60 % of its decisive games against each other faction at tier N, N = 1..5, medium coast maps.

## Method
- `Tools ai match --a tiered:N --b tiered:N --faction-a expedition --faction-b <authority|directorate|left_behind> --size 96 --ticks 18000 --seeds S` (S seeds x 2 start swaps; ocean north+south, Expedition on the south start, D-151).
  BEFORE: 16 games per cell (8 seeds), release build of the branch start (4588092). AFTER: 32 games per cell (16 seeds) for the cells that were re-run.
- "Decisive %" = Expedition wins / (Expedition wins + losses), draws left out (a draw is a game with both sides alive at tick 18000). "Win %" counts draws as non-wins. Noise at n=16 is about +-12 points, at n=32 about +-9 on the win %.
- Raw per-cell logs were kept in `scratch/` (not committed); `scratch/run.sh` and `scratch/agg.py` in the worktree reproduce them.

## Table (Expedition wins-losses-draws, then decisive %)
| Tier | vs Authority before | after | vs Directorate before | after | vs Left Behind before | after |
|---|---|---|---|---|---|---|
| 1 | 5-7-4 (42 %) | 16-10-6 (62 %) | 1-2-13 (33 %) | 9-2-21 (82 %) | 16-0-0 (100 %) | 32-0-0 (100 %) |
| 2 | 7-5-4 (58 %) | 18-7-7 (72 %) | 3-1-12 (75 %) | 7-3-22 (70 %) | 15-1-0 (94 %) | 31-1-0 (97 %) |
| 3 | 5-6-5 (45 %) | 9-14-9 (39 %) | 1-7-8 (12 %) | 1-8-23 (11 %) | 4-4-8 (50 %) | 14-6-12 (70 %) |
| 4 | 1-3-12 (25 %) | 5-5-22 (50 %) | 0-5-11 (0 %) | 6-6-20 (50 %) | 8-2-6 (80 %) | 20-2-10 (91 %) |
| 5 | 1-4-11 (20 %) | 4-6-22 (40 %) | 1-4-11 (20 %) | 6-6-20 (50 %) | 9-0-7 (100 %) | 18-1-13 (95 %) |

Tiers 1 and 2 vs Authority and Directorate did not change in code (the plan for tiers 1-2 equals the old behaviour); the before/after difference there is sample size, not a fix. Read the table as: the before row was a weak 16-game sample.

## What changed
- `ExpeditionPlan` (new, `src/AI/Recruit/ExpeditionPlan.cs`): the Expedition's own per-tier flags and numbers (fleet size, cruisers, carriers, escorts, sub hunting, building sorties, wave bonus). The tactics read the plan, not the tier number. `data/ai/difficulty.toml` is untouched. Tier 4-5: fleet 5, one cruiser (two at 5), one carrier, two escorts, building sorties. Tiers 1-3: fleet 3, one escort, no cruiser or carrier (tier 3 keeps the old small fleet; see Not fixed).
- Full-hangar launch: a carrier launches a sortie only when every docked aircraft holds full ammunition (`ExpeditionTactics.HangarFull`). New view fields `ViewActor.AmmoFull` and `ViewActor.DockedIn` (own units only; other players' actors always read full / not docked).
- Building sorties (tiers 4-5, flag `SortieBuildings`): a full carrier with no enemy near also launches against the nearest known enemy building within 60 cells (aircraft fly over land). Before this the carrier only launched at enemies within 14 cells of itself, which almost never happened.
- Landing markers: `SkirmishGame.MakeSetup` now passes `LandingMarkers = meta.Landing` (one line, `src/Game/Sandbox/SkirmishGame.cs`), so the skirmish host gives the AI the map's markers (D-151 (2)). `ai match` already passed them.

## Tried and dropped
- Tier 3 with fleet 4 + cruiser, and with fleet 5 + cruiser + carrier + building sorties: vs Authority 41 % decisive (9-13-10), vs Directorate 17 % (2-10-20), the same as the old small fleet within noise, so the simpler plan stayed.

## Not fixed / honest limits
- **Tier 3 vs Directorate (11 % decisive, 1-8-23)** is far outside 40-60 %. The Directorate tier 3 gets focus fire and its army outgrows the Expedition's few dear units by about 2:1 by minute 10 (units 150-230 vs 60-130); every fleet variant I tried (above) left it at 6-17 %. A fix needs economy or land-army changes (the Expedition's land roster), not escort or strike tactics, and I did not touch rules numbers (D-152).
- **Draws are common** at tiers 3-5 (20-23 of 32): both sides alive at tick 18000. The "decisive %" hides this; the win % (draws as non-wins) is 12-28 % at tier 3-5 vs Authority/Directorate. Longer games or an earlier ship strike are the lever.
- Tier 1-2 vs Directorate: 22 of 32 draws.
- Landing-marker pass-through is a one-liner but the effect in real skirmishes was not measured (`ai match` already had it).
- Human play was not run.

## Tests
`tests/AI.Tests/M7/ExpeditionHangarTests.cs` (full-hangar rule with two docked jets; view fields own-units-only) and `ExpeditionAiTests.ARecruitTierExpeditionEscortsConvoysAndStrikesFromTheSea` (tier 1 vs Directorate: escort orders and a strike target by tick 12000).
