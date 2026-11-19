# Session 35 checks: balance table on main, cross-architecture determinism, map fairness, LLM commander

Run on the home cluster (laptop k3s-cluster folder `claude/rts/s35b/`: build.sh, series.sh, det.sh, fair.sh, cmd*.sh). Build: Tools at
`b0b5703d` (main 89a2fdd plus the provider timeout option only; no sim or AI change). No balance change follows from this note (D-269).

## Balance table on main (job ser35)
Cells as in sessions 27-32: `ai match` tiered:3 v tiered:3, size 96, layout 1, 30 000 ticks, seeds 1-64 x 2 start swaps (128 games per row).
Win % = A wins / decided games (A = the first faction named).

| Row | W-L-D | A win % | s32 final (s32_balance.md) |
|---|---|---|---|
| Authority v Directorate rust | 50-55-23 | 47.6 % | not rerun in s32 |
| Authority v Expedition rust | 46-79-3 | 36.8 % | 36.8 % |
| Authority v Left Behind rust | 77-42-9 | 64.7 % | 64.7 % |
| Directorate v Expedition rust | 58-42-28 | 58.0 % | 58.0 % |
| Directorate v Left Behind rust | 54-60-14 | 47.4 % | 47.4 % |
| Left Behind v Expedition rust | 57-59-12 | 49.1 % | 49.1 % |
| Expedition v Authority glass | 65-58-5 | 52.8 % | 52.8 % |
| Expedition v Directorate glass | 57-47-24 | 54.8 % | 54.8 % |
| Expedition v Left Behind glass | 69-31-28 | 69.0 % | 69.0 % |

Every row that s32 measured comes out game for game the same on today's main: nothing since s32 has moved balance. The open outliers are
therefore exactly the s32 ones: Expedition v Left Behind on Glass Coast (69 %), Authority v Left Behind (64.7 %), Expedition over the Authority
on Rust Belt (63.2 % for the Expedition) and Directorate v Expedition (58 %). The older roadmap rows M13-26 / M13-28 / M13-29 ("laptop series to
confirm") are answered by this table: their s23-s25 fixes are in, and the remaining gaps are the ones listed here.

## Cross-architecture determinism (jobs deta on the Pis, detx on x86)
The same nine rows, seeds 1-8 x 2 swaps (144 games, about 2.1 million simulated ticks), run once on arm64 (Raspberry Pi 4, Debian 13) and once
on amd64 (i5 nodes) with `--record-dir`. All 144 replay files and all nine result tables are byte-identical; only the timing lines in the run
logs differ (for example 233 752 ticks in 666 s on a Pi core v 202 s on an x86 core). The deterministic-sim rule (D-006: no float, seeded RNG) holds across CPU
architectures, so lockstep multiplayer between ARM and x86 machines needs no extra work.

## Map fairness screen (job fair35)
Mirror matches (same faction both sides, tiered:3 v tiered:3, size 96, 30 000 ticks) on all four biomes x both layouts, seeds 1-32 x 2 start
swaps (64 games per cell, 256 per map layout). In a mirror the factions cancel, so any lean is the map: the cell shows wins from start 0
(top-left, 4,4) v start 1 (the mirror corner), draws left out. ** = more than 2.6 standard errors from 50 %.

| Map | Layout | Authority | Directorate | Expedition | Left Behind | All four (start 0 share, z) |
|---|---|---|---|---|---|---|
| Ash Plains | 1 | 44-18 (71 %) ** | 36-17 (68 %) ** | 42-22 (66 %) | 43-17 (72 %) ** | 69.0 % (z +5.9) ** |
| Ash Plains | 2 | 39-23 (63 %) | 25-34 (42 %) | 30-31 (49 %) | 21-41 (34 %) | 47.1 % (z -0.9) |
| Glass Coast | 1 | 25-26 (49 %) | 29-16 (64 %) | 35-23 (60 %) | 25-20 (56 %) | 57.3 % (z +2.1) |
| Glass Coast | 2 | 25-33 (43 %) | 30-27 (53 %) | 34-28 (55 %) | 32-32 (50 %) | 50.2 % (z +0.1) |
| Mountains | 1 | 31-24 (56 %) | 22-28 (44 %) | 30-25 (55 %) | 19-24 (44 %) | 50.2 % (z +0.1) |
| Mountains | 2 | 32-26 (55 %) | 29-29 (50 %) | 21-38 (36 %) | 24-37 (39 %) | 44.9 % (z -1.6) |
| Rust Belt | 1 | 28-30 (48 %) | 19-35 (35 %) | 33-25 (57 %) | 36-19 (65 %) | 51.6 % (z +0.5) |
| Rust Belt | 2 | 25-37 (40 %) | 25-34 (42 %) | 30-28 (52 %) | 26-37 (41 %) | 43.8 % (z -1.9) |

- **Ash Plains layout 1 favours the top-left start: 69 % over 239 decided games (z +5.9), and the lean is in every faction (66-72 %).** This
  is a map problem, not an AI one; the next step is a trace of a few Ash Plains l1 games to find what the top-left base gets (nearer ore,
  shorter path to the middle, a choke) and a map-data fix, screened with this same cell.
- Glass Coast layout 1 leans the same way, more mildly (57 %, z +2.1): worth a second look after Ash Plains.
- Every other map layout is within 2 standard errors of 50 % overall (44-52 %). Single faction cells swing 34-65 % on 50-60 decided games,
  which is within the noise of one cell (one standard error is about 6.5 points); none of them is flagged.

## M5-13: LLM commander with a local model (jobs cmod, cctl, cbase)
Setup as in s29 (docs/balance/s29_commander_control.md): tier 3 v tier 3, default factions, map 64, 30 000 ticks, seeds 1-16 x swap 0/1
(32 games, D-241). Side A is commanded, side B is the plain AI.
- **Model:** qwen3:30b on the NAS's Ollama (CPU only, no API key), through a localhost proxy that turns the OpenAI chat call into
  Ollama `/api/chat` with reasoning off and `format: "json"` (laptop folder `claude/rts/s35b/cmd_model.sh`). `timeout_seconds = 180` in
  providers.toml (new option, this PR). Without JSON mode qwen3 wrote until the 1200-token cap (about 2 minutes a call); with it, replies
  are 130-250 tokens of valid JSON.
- **Control:** `--random-directives` on the same 32 games. **Base:** plain v plain on the same seeds.

| Side A | W-L-D | Win % of decided | Score (draw = 0.5) |
|---|---|---|---|
| qwen3:30b commander | 6-19-7 | 24 % | 29.7 % |
| Random directives (control) | 6-24-2 | 20 % | 21.9 % |
| Plain AI (base) | 13-12-7 | 52 % | 51.6 % |

- Paired game for game (win > draw > loss), the model did better than the control in 10 games and worse in 6. That is no significant
  difference (sign test p about 0.45). Most of the gain is draws: the model held on to the tick limit in 7 games where the control lost 5
  of the same games. Wins are equal (6 and 6).
- **Both commanded sides are far below the plain AI** (about 22-30 % v 52 %), as in s29 (control 21 % v plain 53 % on 128 games). So
  steering the tier-3 AI with directives costs it more than a 30B local model's choices give back. The bottleneck is how the AI carries
  out directives and orders, not the model's JSON.
- Calls: 865, mean 31 s, slowest 72.5 s. 10 calls (1.2 %) got an HTTP 500 from Ollama; the commander logged the error and the game went
  on. The NAS also hosts the queen VM, which ran cluster jobs during the series and shares its CPU with Ollama.
- Next (M5-14 candidates): find which directive or order kinds lose games (per-kind outcomes from the replays), so the AI follows a
  directive only where it does not undercut its own plan; then rerun this same 32-game set (model and control) to measure.
