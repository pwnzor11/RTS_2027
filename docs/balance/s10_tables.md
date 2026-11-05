# Session 10 balance tables: SUP-6 (supply before/after) and VET-7 (veterancy off/on)

Every number below comes from runs made in session 10 on branch `wp/bal` (integ-s10: supply ON, veterancy ON, VET-6 AI). Nothing is copied from older
documents. Game code, AI code and committed data are unchanged; the "before" runs use temporary copies of `data/` (see Method).

## Method

Tool: `tools/measure/bal_probe` (new, `BalProbe`). `Tools ai match` prints only wins, so BalProbe plays exactly the same games
(`MapGenerator.Generate(size, seed)`, game seed `seed*16+g`, `alt` start swapping, `AiMatchRunner.Play`, economy start) and adds a sampler
(every 10 ticks, no effect on the sim) that prints one TSV row per side per game. `tools/measure/bal_agg.py` turns the rows into the tables.

- Settings: tier-3 AI (`tiered:3`) on both sides, medium map (96x96), 30000 ticks (25 min) per game, seeds 1..16, each seed played twice with the starts and slots swapped
  = 32 games per pair. Pairs: Authority-Directorate, Authority-Left Behind, Directorate-Left Behind. The sampler's results were checked against the
  sim: while supply was on, its supply-used and cap agreed with `Simulation.SupplyUsed/SupplyCap` in every sample except Left Behind (see Caveats).
- Data override: the tool reads `data/rules` from `--rules <dir>` (BalProbe) or the nearest `data/rules` above the working directory (`Tools ai match`),
  so "before" is a temporary copy of `data/` with one line changed in `rules/game.toml`:
  - `cfg/offsup`: `[supply] enabled = false` (veterancy on)
  - `cfg/after`: unchanged (supply on, veterancy on). This is also the "veterancy on" side of VET-7.
  - `cfg/offvet`: `[veterancy] enabled = false` (supply on)
- Build: `dotnet build tools/measure/bal_probe/BalProbe.csproj -c Release -o $BIN` (under the dotnet lock). Runs use the binary directly, not under the lock.
- Each cell: `tools/measure/bal_run.sh <cfg> <label> <factionA> <factionB> 1 16 <outdir>`, i.e.
  `BalProbe --rules <cfg>/rules --fa authority --fb directorate --tier 3 --size 96 --first 1 --seeds 16 --ticks 30000`.
  Nine cells (3 configs x 3 pairs). Aggregation: `python3 -I tools/measure/bal_agg.py <outdir>`.
- Each faction appears in two pairs, so per-faction rows pool 64 games (one side of each game), except where noted.
- Time used: about 80 minutes of measuring (a 16-seed cell took 7-10 minutes on the shared 2 CPUs; the `acc` helper ran alongside).

Definitions (minute = 1200 ticks):
- Win rate: wins plus half of draws (game still undecided at 25 min), over games. Draws are common (about 20 %), so 25 min is a limit of the method.
- Army supply: supply of all living soldiers and non-harvester vehicles, computed from the rules (also with supply off, so before/after compare). Median over games still running at the minute.
- Capped: samples in minutes 5-15 (ticks 6000-18000) where supply used >= cap, over samples while the player is alive. With supply off the cap is
  hypothetical (what the same buildings would have given) and the column shows how often the game WOULD have been capped.
- Supply buildings: finished buildings with `supply_provided > 0` and cost > 0 (ration hall, bunk gallery, still rig), counted when first seen, mean per game.
- Income: credits harvested up to minute 10 (cumulative, median), and in minutes 8-10 (median). Both from the sim's stats counters.
- Rank counts: each living actor's `RankOf` is sampled; a unit counts once per rank when it first reaches it. Only games still running at the minute count.

Resolution (one standard error of a win rate near 50 %): 64 games (per faction, pooled) = +-6 points, 32 games (per pair) = +-9 points.
A before/after difference of two 64-game cells has a standard error of about 9 points, and two 32-game cells about 12.5 points. So a 5-point target is
checkable only as "no change larger than the noise", not as a precise pass. Medians of army supply and income: rough spread +-10-20 % at these sample sizes (n shown).

## SUP-6: supply before and after

Per faction (64 games each, both pairs pooled). "Off" = `[supply] enabled = false`, "On" = true.

| Faction | Supply | Win % (+-6) | Capped, min 5-15 | Games over 10 % capped | Army supply @5 min | Army supply @10 min | Supply bldgs / game | Credits harvested @10 min | Cap @10 min | Used @10 min |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Authority | off | 54.7 | 56.1 % (would-be) | 51 / 64 | 13.0 | 19 | 0 | 24931 | 20 (would-be) | 21 |
| Authority | on | 58.6 | 0.1 % | 0 / 64 | 13.5 | 19.5 | 3.31 | 25294 | 45 | 21.5 |
| Directorate | off | 46.9 | 33.1 % (would-be) | 36 / 64 | 8.0 | 14 | 0 | 24098 | 20 (would-be) | 16 |
| Directorate | on | 46.9 | 0.0 % | 0 / 64 | 9.0 | 13 | 3.09 | 22693 | 40 | 15 |
| Left Behind | off | 48.4 | 70.0 % (would-be) | 50 / 64 | 15.5 | 42 | 0 | 23602 | 20 (would-be) | 46 |
| Left Behind | on | 44.5 | 0.5 % | 2 / 64 | 16.5 | 26 | 6.75 | 22660 | 70 | 31 |

Credits harvested in minutes 8-10 (median): Authority 4800 off / 4729 on; Directorate 4465 / 6000; Left Behind 5925 / 5082.
Game counts at minute 10 (n of 64): Authority 53 off / 58 on, Directorate 56 / 57, Left Behind 53 / 53.

Per pair (32 games each, +-9; value is the win rate of the first-named faction):

| Pair | Supply off | Supply on |
|---|---:|---:|
| Authority vs Directorate | 42.2 | 48.4 |
| Authority vs Left Behind | 67.2 | 68.8 |
| Directorate vs Left Behind | 35.9 | 42.2 |

Mean game length (ticks): Auth-Dir 23561 off / 22797 on; Auth-LB 16098 / 17443; Dir-LB 20704 / 19600.

### SUP-6 targets (13 section 7)

| Target | Result | Verdict |
|---|---|---|
| No faction capped more than 10 % of minutes 5-15 | Authority 0.1 %, Directorate 0.0 %, Left Behind 0.5 % (worst single games: 2 of 64 Left Behind games above 10 %) | PASS |
| Win-rate spread (best minus worst faction) unchanged within 5 points | off: 54.7 - 46.9 = 7.8; on: 58.6 - 44.5 = 14.1; change +6.3 | NOT MET on the point estimate; the change is smaller than the noise (about +-9 on a difference), so UNRESOLVED at 64 games per faction |

Reading: the cap is never binding for the tier-3 AI once supply is on. With supply off the same AI would sit at the hypothetical 20-supply cap for a third to
two thirds of minutes 5-15, so the cap rules and the AI supply rule (SUP-5) do their job. The Left Behind army at 10 minutes drops from 42 to 26 supply with
the cap on (the unlimited-army version was larger), and Left Behind loses 4 win points while Authority gains 4; Authority-Directorate moved 6 points, all inside the noise.
A re-run with 64 seeds per pair would be needed to settle the spread (it would resolve about +-4.5 per faction).

## VET-7: veterancy

Rank counts per game, for games still running at the minute (units that reached the rank, cumulative, mean per game). "Off" runs show 0 in every rank as expected
(a check that the switch works). Share of rank 3 = living rank-3 units over all living units that can rank (soldiers and armed vehicles), pooled over games.

| Faction | n games @10 / @20 | Rank 1 / 2 / 3 reached by min 10 | Rank 1 / 2 / 3 reached by min 20 | Share of units at rank 3 (min 10 / 15 / 20) | Games with a rank-3 unit by min 15 | by min 20 |
|---|---|---|---|---|---|---|
| Authority | 58 / 21 | 4.84 / 0.93 / 0.22 | 11.10 / 2.14 / 0.19 | 0.9 % / 0.2 % / 0.3 % | 3 / 33 (9 %) | 4 / 21 (19 %) |
| Directorate | 57 / 25 | 3.58 / 1.68 / 0.53 | 6.24 / 2.84 / 1.32 | 1.7 % / 1.6 % / 2.7 % | 21 / 37 (57 %) | 17 / 25 (68 %) |
| Left Behind | 53 / 16 | 4.17 / 0.55 / 0.04 | 9.12 / 1.12 / 0.06 | 0.1 % / 0.0 % / 0.0 % | 0 / 28 (0 %) | 1 / 16 (6 %) |

Most games end before minute 20 (only 16-25 of 64 reach it), so the minute-20 rows rest on few games and are biased toward long, slow games.
The same table from the supply-off runs (veterancy on) gives the same picture: rank 3 by min 15 in 4/34 (Authority), 22/43 (Directorate), 2/27 (Left Behind) games.

Win rate with veterancy off vs on (supply on in both; 64 games per faction, +-6; per pair 32 games, +-9):

| Faction / pair | Veterancy off | Veterancy on | Change |
|---|---:|---:|---:|
| Authority | 54.7 | 58.6 | +3.9 |
| Directorate | 47.7 | 46.9 | -0.8 |
| Left Behind | 47.7 | 44.5 | -3.2 |
| Authority vs Directorate | 45.3 | 48.4 | +3.1 |
| Authority vs Left Behind | 64.1 | 68.8 | +4.7 |
| Directorate vs Left Behind | 40.6 | 42.2 | +1.6 |

Draws (undecided at 25 min) with veterancy off vs on: Authority-Directorate 17 vs 11 of 32, Authority-Left Behind 5 vs 2, Directorate-Left Behind 12 vs 7;
veterancy shortens games.

### VET-7 targets (14 section 7)

| Target | Result | Verdict |
|---|---|---|
| Rank 3 appears in most games after minute 12 | Directorate yes (57 % by min 15, 68 % by min 20); Authority (9 %, 19 %) and Left Behind (0 %, 6 %) no | PARTLY: FAIL for Authority and Left Behind (rank 3 is nearly unreachable for them in the games that end before min 20) |
| Rank 3 in less than 1 in 10 units | Highest observed share 2.7 % (Directorate, min 20) | PASS |
| No faction's win rate moves more than 5 points | Largest move +3.9 (Authority), -3.2 (Left Behind), -0.8 (Directorate); largest pair move +4.7 | PASS on the point estimates, but the noise on each figure is +-9 on a difference, so this only shows that no large effect exists |

Reading: veterancy is mild. Rank 1 is common (4-5 units per game by minute 10), rank 2 uncommon, rank 3 mostly a Directorate thing (fewer, tougher
units, longer games). Rank-3 reach is uneven between factions: Authority and Left Behind produce lots of cheap rank-1 units but few reach 3200 kill value.

## Caveats

- Left Behind: in the supply-on runs the sampler's supply used/cap disagreed with `Simulation.SupplyUsed/SupplyCap` in 282-320 samples (Authority and Directorate: 0).
  The cause was not investigated (a likely suspect is the packed Mother Rig, which the sampler treats as an ordinary unit/building). Treat the Left Behind capped-time
  figure (0.5 %) as approximate; the sim's own value is not available from these runs.
- "Cap @10 min" with supply off is a computation from the buildings alive, not a sim value.
- The draw rate (about 20 %) at 25 min and the swapped-start design mean win rates are of "wins plus half of draws"; counting draws as zero would lower every
  figure and widen the Authority lead.
- `--trace` was not needed: all metrics come from the sampler. Medians come from per-game rows kept in the TSV files (not committed; reproducible with the commands above).
