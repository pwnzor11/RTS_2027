# Session 25 balance round (cloud measurements)

Baseline: owner's laptop series s24after (balance_runs\s24after, commit 0b2b5030). Target 45-55 % per cross-faction row. Cells are tier 3 v tier 3, size 96, 30 000 ticks, --income 1; 16 seeds = 32 games (+-12 points), 32 seeds = 64 games.

## M13-29 Expedition too strong on Glass Coast (s25-glass)
Cause (trace, Exp v Auth glass seed 1): glass_coast ore is nearly gone by tick ~7500. Authority's harvested income goes flat at ~23k and its
production queue stalls at 0 credits from tick 9000, while Expedition shipments (which do not need ore) keep paying (22.7k -> 27.5k over ticks
7500-13500). Income at 10 min is similar on both sides, so the edge comes after the ore runs out. Expedition also loses far fewer units. Duels
(Glide Tank v tank, marines v riflemen) show no outlier unit. Fix: shipment value, a map-independent rule; rust_belt limits how far it can go.
16 seeds (32 games); baseline = s24after logs, seeds 1-16. A share = A wins / decisive.
| Row | Before W-L-D | value 1000 | value 1200 (kept) |
|---|---|---|---|
| Exp v Auth glass | 20-8-4 (71 %) | 15-15-2 (50 %) | 18-14-0 (56 %) |
| Exp v Dir glass | 17-11-4 (61 %) | 13-12-7 (52 %) | 15-11-6 (58 %) |
| Exp v LB glass | 22-2-8 (92 %) | 19-10-3 (66 %) | 23-5-4 (82 %) |
| Auth v Exp rust_belt | 15-16-1 (48 %) | 19-12-1 (Exp 39 %) | 16-15-1 (52 %) |
Value 1000 overshoots rust_belt; 1200 keeps it in range. Left: Exp v LB glass 82 %, Exp v Dir glass 58 %.

### s25-glass2: more ore on glass_coast (M13-29), tried and dropped
Base: shipment value 1200 + late retreat release; 16 seeds x 2, glass_coast layout 1. V1 = rust_belt field counts (14-16 fields instead of 6-10),
V2 = ash_fields_per_start 2 -> 3 only.
| Row | Before | V1 | V2 |
|---|---|---|---|
| Exp v Auth | 18-14-0 (56 %) | 11-19-2 (37 %) | 19-11-2 (63 %) |
| Exp v Dir | 15-11-6 (58 %) | 5-23-4 (18 %) | 10-19-3 (34 %) |
| Exp v LB | 23-5-4 (82 %) | - | 20-7-5 (74 %) |
| Auth v Dir (control) | 15-10-7 (60 %) | - | 7-18-7 (28 %) |
More ore helps the Directorate most (the control row moved 8 wins), and V1 removes the Expedition's edge entirely. Nothing kept.

## s25-lb: Left Behind (M13-27 slot bias, M13-28 weakness v Authority)
### M13-27 mirror "slot bias"
s24after logs plus fresh seeds 33-48: slot 0 is not favoured (s24after l1 28-22, l2 23-36; fresh l1 12-14, l2 15-13), and start corners are balanced.
What leans is side A (34-16, 35-24; fresh 15-11, 17-11). Split by game, the winner is more often the slot whose index matches the game-seed
parity (game 1 / even seed: slot 0 50-34; game 2 / odd seed: slot 1 51-28). Swapping the game seeds reversed the A edge (15-11 -> 11-15) while the
slot split stayed level, so the edge follows the per-game AI seed, not a slot or LB code path.
### M13-28 Left Behind v Authority
Traces (rust_belt l1, --econ, --ledger): LB held 2 000-5 000 credits idle in the early and mid game while Authority spent down; at tick 3 600 LB had
20-21 units (~2 800 value) against Authority's 22-26 (~5 100-5 500). Cause: `[faction.left_behind.tier3] army_cap = 20` until tick 4800
(data/ai/difficulty.toml, D-186/D-230) pauses unit production. A second Muster Rig in the build order made it worse (reverted).
| cap until tick | Auth v LB W-L-D | LB share | seeds |
|---|---|---|---|
| 4800 (before) | 21-11-0 | 34 % | 16 |
| 4500 (kept) | 16-14-2 | 47 % | 16 |
| 4200 | 11-18-3 | 62 % | 16 |
| 3600 | 10-19-3 | 66 % | 16 |
| 3000 | 4-10-2 | 71 % | 8 |
At 4500: Directorate v LB 17-10-5 -> 14-13-5 (52 %); Expedition v LB glass 22-2-8 -> 20-6-6 (LB 23 %).
LB also re-deploys its Mother Rig many times per game (11 in one trace); not changed.

## s25 draws (M13-23, M13-30; wp/s25-draw, cloud)
Baseline is the s24after laptop logs (seeds 1-16 only). All cells are tiered:3 v tiered:3, size 96, layout 1, 30 000 ticks, `--income 1`, 16 seeds = 32 games.

### Root cause (Expedition mirror, mountains, seed 4 game 1)
- Both armies stayed at about 15-21 units, but only 2-8 were in the wave pool; 8-12 sat detached for repair for 10 000+ ticks.
- Expedition has `RepairAll=true` and `RetreatPct=40`. Hurt marines never heal (the Mender repairs vehicles only), and after `RepairMaxTicks` a still-weak
  unit is sent back to repair at once, so it never rejoins. Waves of 4-6 units met each other and neither side could finish.
### Fix
`RetreatManager.LateAllInTick = 16000` (tune): from that tick nobody retreats, and home-guard and repairing units rejoin the wave pool
(`Squads.ReleaseRetreated`). Scouts and raiders keep their duty. AI only; no new saved fields.
### Results (W-L-D from A's view; A share = A wins / decisive games)
| Row | Before | After | Draws | A share |
|---|---|---|---|---|
| Expedition mirror, mountains | 13-10-9 | 13-12-6 | 9 -> 6 | 57 % -> 52 % |
| Expedition mirror, ash_plains | 11-11-10 | 12-13-7 | 10 -> 7 | 50 % -> 48 % |
| Directorate mirror, mountains (control) | 14-10-8 | 14-10-8 | 8 -> 8 | 58 % -> 58 % |
| Expedition v Directorate, glass_coast | 17-11-4 | 16-11-5 | 4 -> 5 | 61 % -> 59 % |
Not fixed: Expedition ships and utility units are not in the wave pool, so the last land units still stalemate in some games.
### M13-30
`tools/measure/run_s19_after.ps1`: summary gains AdjW / AdjL / AdjEven columns; title "Series <OutName> summary". Test in tests/Tools.Tests/MatchAdjudicationTests.cs.

## Lead: AI seed fix (M13-27) and Left Behind cap 3600 (M13-28)
The s25-lb finding (mirror skew follows game-seed parity) pointed at `AiRandom`: the per-slot seed `gameSeed ^ gamma * (slot + 1)` left slot 1's
splitmix state exactly one gamma step ahead of slot 0's for 15 of 64 even game seeds used by `ai match` (16*s+g), so the two AIs drew the same
sequence shifted by one draw. Both halves now go through the splitmix finaliser first (no seed in 0..200 000 gives states a gamma multiple apart).
All AI games reshuffle; AI.Tests 392/393 (1 skipped), Sim.Tests 1839/1841 (2 skipped) and Windows CI (incl. Scripting) passed.
Left Behind mirror after the fix (16 seeds): l1 7-14-11, l2 18-14-0; winner slot by game: game 1 11/14, game 2 14/14 (before: 50/34 and 28/51).
With the new seeds the cap-4500 result did not hold: Authority v Left Behind rust 39-19-6 over 64 games (LB 33 %). Cap until tick 3600:
| Row | W-L-D | A share |
|---|---|---|
| Authority v Left Behind rust (32 seeds) | 32-25-7 | 56 % |
| Directorate v Left Behind rust | 16-13-3 | 55 % |
| Left Behind v Expedition rust | 16-14-2 | 53 % |
| Expedition v Left Behind glass | 14-8-10 | 64 % (was 22-6-4 at 4500) |
| Left Behind mirror mountains l1 | 9-12-11 | 43 % |
Other rows on the same build: Expedition v Authority glass 41-19-4 over 64 games (68 %), Expedition v Directorate glass 13-12-7 (52 %),
Authority v Directorate rust 11-14-7 (44 %). Expedition v Authority on Glass Coast is the open row (s25-authglass).

## M13-29 Authority v Expedition on Glass Coast (s25-authglass)
Same build as above (shipment 1200, Left Behind cap 3600, new AI seeds); baseline measured fresh. Trace (seed 3 game 2, ledger): the Authority sat at
tier 1 all game with army value 6.6k at tick 5000 against the Directorate's 10.6k at similar harvested income. Its economy start costs more
(refinery 2000 against the Smelter Vent's 1700), so it fields fewer units before the first fights; losses per credit are similar to the Directorate's.
Fix: `auth_refinery` cost 2000 -> 1700.
| Row | Before W-L-D | After W-L-D | A share |
|---|---|---|---|
| Exp v Auth glass, 32 seeds | 41-19-4 | 32-28-4 | 68 % -> 53 % |
| Auth v Exp rust, 16 seeds | 16-15-1 (old seeds) | 14-17-1 | 45 % |
| Auth v Dir rust, 16 seeds | 11-14-7 | 10-15-7 | 40 % |
Rejected: Scraper 700 -> 600 as well (Auth v Dir rust 6-19-7); Authority tier 2 at army 16 (glass unchanged 24-8-0).
Edge rows for the next laptop series: Auth v Dir rust 40 %, Auth v Exp rust 45 %, Exp v LB glass 64 %.
