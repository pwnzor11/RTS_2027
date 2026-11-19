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
