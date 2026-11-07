# S14 baseline AI-vs-AI runs on the owner's laptop (before D-166 and D-173)

Run by the owner on Windows with balance_runs/run_s14_baseline.ps1 at commit c770c9b (main before session 14).
`ai match --seeds 32 --size 96 --ticks 30000`: 32 seeds x 2 start swaps = 64 games per row. Deterministic: the two rows that
session 13 also measured (Authority 5 v 1, Directorate 5 v 1) reproduce its numbers exactly.

| Match (A v B) | A wins | B wins | Draws | Decisive % for A | Avg end tick |
|---|---:|---:|---:|---:|---:|
| Directorate 3 v Authority 3 | 24 | 19 | 21 | 56 % | 22 224 |
| Directorate 5 v Authority 5 | 7 | 37 | 20 | 16 % | 22 163 |
| Authority 5 v Authority 1 | 41 | 18 | 5 | 69 % | 17 146 |
| Directorate 5 v Directorate 1 | 17 | 8 | 39 | 68 % | 27 198 |
| Left Behind 3 v Directorate 3 | 56 | 6 | 2 | 90 % | 13 879 |
| Expedition 3 v Authority 3 | 33 | 26 | 5 | 56 % | 15 778 |

Decisive % = A / (A + B), draws left out.

## Reading (lead, session 14)
- **Draws are a Directorate problem.** Every row with a Directorate side has 20-39 draws in 64; rows without one have 5. This is what D-173
  (the "no way to rebuild for 2 minutes" defeat rule) targets.
- **Directorate is the weak faction.** Left Behind beats it 56-6 at tier 3, and Authority beats it 37-7 at tier 5 (at tier 3 it is roughly
  even). Something in the Directorate's tier 4-5 play or its late units does not scale.
- **The tier ladder is soft.** Tier 1 still beats tier 5 in 18 of 64 Authority games.
- **Expedition v Authority is balanced** (56 % decisive, few draws).
- Not yet measured: the D-166 Conscript/ash change and D-173. Re-run the same six rows after both land to compare (same seeds).
