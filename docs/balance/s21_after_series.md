# Session 21 · skirmish after-series on the session 21 code (Q-77, M13-10)

Owner's laptop, `tools/measure/run_s19_after.ps1 -OutName s21after`, integ-s21 @ 8b418a2e (= main 70a4dbb2 code: PR #70 + D-219), 64 games
per row (32 seeds x 2 start swaps), tiered AI tier 3 both sides, size 96, 30 000 ticks, --income 1, 54 min on 8 CPUs. Full table and income lines:
`balance_runs\s21after\s21after_summary.md` in the owner's folder. Earlier runs: s18audit (7876e11), s19after (6e3c15f9, before #70 and D-219).

| Row | s18 A-B | s19 A-B | **s21 A-B** | s21 start share |
|---|---|---|---|---|
| Expedition mirror, Glass Coast L1 | 28-32 | 21-35 | 20-37 | 58 % |
| Expedition mirror, Rust Belt L1 | 29-30 | 26-32 | 26-28 | 59 % |
| Expedition mirror, Ash Plains L1 | 26-36 | 24-29 | 25-33 | 55 % |
| Expedition mirror, Mountains L1 | 24-16 | 23-25 | 25-26 | 57 % |
| Directorate mirror, Mountains L1 | 23-25 | 26-26 | 34-21 | 58 % |
| Directorate mirror, Mountains L2 | 26-30 | 25-28 | 29-28 | 56 % |
| Left Behind mirror, Mountains L1 | 6-14 | 21-16 | 19-20 | **74 %** |
| Left Behind mirror, Mountains L2 | 23-28 | 34-19 | 31-26 | 51 % |
| Authority v Directorate, Rust Belt | 27-25 | 24-24 | **17-36** | 53 % |
| Authority v Left Behind, Rust Belt | 51-12 | 43-18 | 28-26 | 61 % |
| Authority v Expedition, Rust Belt | 24-37 | 13-50 | **12-51** | 62 % |
| Directorate v Left Behind, Rust Belt | 41-15 | 24-31 | 24-33 | 60 % |
| Directorate v Expedition, Rust Belt | 35-24 | 26-35 | 30-23 | 58 % |
| Left Behind v Expedition, Rust Belt | 29-27 | 21-38 | 29-27 | 59 % |
| Expedition v Authority, Glass Coast | 25-37 | 43-15 | **55-7** | 52 % |
| Expedition v Directorate, Glass Coast | 27-30 | 46-11 | **43-14** | 53 % |
| Expedition v Left Behind, Glass Coast | 33-23 | 43-15 | **43-10** | 62 % |

Readout (lead)
- Start skew (M13-10) is fixed: every row 51-62 % except the Left Behind mirror on Mountains layout 1 (74 %, 39 decisive games). That row has
  a real income gap: start 0 earns 5 820 / 10 911 at 3 / 5 min against 7 632 / 15 107 for start 1 (about -25 %); field placement, not luck.
- The Authority fell behind: v Directorate 24-24 -> 17-36 and v Expedition 12-51 on Rust Belt; v Left Behind is even. Suspect D-219 or PR #70;
  the A/B `tools/measure/run_s21_ab.ps1` replays four rows with D-219 reverted (summary `balance_runs\s21ab_nod219`).
- On Rust Belt the Expedition is now even with the Directorate and the Left Behind; on Glass Coast (mostly sea) it beats every faction 43-55 to 7-14.
