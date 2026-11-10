# Session 16 laptop regression (session 15 AI-vs-AI series on commit 886c5a3)

Run on the owner's laptop after session 16, on commit 886c5a3 (8 logical CPUs, 4 jobs, finished 2026-11-08 23:35, wall time 221 min). The series
is the session 15 one, repeated as a regression check after the session 16 changes. `ai match` with the tiered AI, 32 seeds x 2 start swaps =
64 games per row, size 96; set A ran 30000 ticks. Decisive % = A / (A + B). Directorate tuning is closed (D-196), so this is a check, not a tuning run.

| Set | Match (A v B) | Ticks | A wins | B wins | Draws | Decisive % for A | Avg end tick | S14 (A / B / draws) |
|---|---|---:|---:|---:|---:|---:|---:|---|
| A | authority 5 v authority 1 | 30000 | 44 | 16 | 4 | 73 % | 17162 | 41 / 18 / 5 |
| A | left_behind 3 v directorate 3 | 30000 | 39 | 20 | 5 | 66 % | 15481 | 56 / 6 / 2 |
| A | expedition 3 v authority 3 | 30000 | 37 | 27 | 0 | 58 % | 15618 | 33 / 26 / 5 |
| A | directorate 5 v authority 5 | 30000 | 24 | 25 | 15 | 49 % | 19903 | 7 / 37 / 20 |
| A | directorate 5 v directorate 1 | 30000 | 31 | 19 | 14 | 62 % | 21448 | 17 / 8 / 39 |
| A | directorate 3 v authority 3 | 30000 | 36 | 15 | 13 | 71 % | 19799 | 24 / 19 / 21 |
| B | directorate 5 v expedition 5 | 30000 | 27 | 25 | 12 | 52 % | 19425 |  |
| B | directorate 3 v expedition 3 | 30000 | 35 | 24 | 5 | 59 % | 18838 |  |
| B | left_behind 3 v authority 3 | 30000 | 38 | 22 | 4 | 63 % | 16551 |  |
| B | left_behind 5 v authority 5 | 30000 | 27 | 28 | 9 | 49 % | 18067 |  |
| B | left_behind 3 v expedition 3 | 30000 | 29 | 27 | 8 | 52 % | 16569 |  |
| B | expedition 5 v authority 5 | 30000 | 32 | 18 | 14 | 64 % | 19036 |  |
| B | directorate 1 v authority 1 | 30000 | 41 | 12 | 11 | 77 % | 19156 |  |
| B | directorate 5 v left_behind 5 | 30000 | 22 | 35 | 7 | 39 % | 16656 |  |
| B | directorate 1 v left_behind 1 | 30000 | 28 | 15 | 21 | 65 % | 19398 |  |
| C | directorate 5 v directorate 1 | 60000 | 35 | 20 | 9 | 64 % | 26129 |  |
| C | directorate 3 v authority 3 | 60000 | 41 | 16 | 7 | 72 % | 23485 |  |

## Reading

- No regression against the accepted D-196 numbers: left_behind 3 v directorate 3 66 % and directorate 3 v authority 3 71 %, as in
  docs/balance/s16_q67_laptop_after_tier5.md.
- Two rows to watch, no action because Directorate tuning is closed (D-196): directorate 5 v left_behind 5 39 %, directorate 1 v authority 1 77 %.
