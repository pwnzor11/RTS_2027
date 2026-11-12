# Session 21 · campaign retune with ash on (M13-17, D-219, D-222)

Wins over 12 mission seeds (`seed = 1000 + k*7919`, k = 1..12) with the ashfall hazard on and the D-219 stance rule, measured in the cloud on
the mission's branch or on integ-s21 (SeedSweepTests). Targets (Q-75, owner): tier 1 >= 10/12, tier 3 >= 8/12, tier 5 >= 3/12. The owner's
laptop re-measured every row (`tools/measure/run_s21_campaign_sweep.ps1`): integ-s21 @ 8b418a2e, all 45 mission-tiers on target, seed for seed
identical to the cloud numbers (Windows and Linux play the campaign the same; 21 min on 8 CPUs).

| Mission | Tier 1 | Tier 3 | Tier 5 | s20 baseline (ash on, before D-219) | Change this session |
|---|---|---|---|---|---|
| auth_01 | 12 | 11 | 6 | 12 / 3 / 10 | raid-2 jeep from tier 4 |
| auth_02 | 12 | 12 | 12 | 12 / 12 / 12 | — |
| auth_03 | 12 | 12 | 6 | 6 / 11 / 10 | smaller coast counter; bot sweeps the picket's rear |
| auth_04 | — | 12 | 12 | — / 12 / 0 | tier-5 home raid 1 at 2400 (all-or-nothing mission) |
| dir_01 | — | 12 | 12 | — / 12 / 7 | — |
| dir_02 | 12 | 12 | 12 | 12 / 12 / 6 | — |
| dir_03 | 12 | 12 | 12 | 12 / 12 / 12 | — |
| dir_04 | 12 | 12 | 12 | 12 / 12 / 0 | tier 5 without the wave-3 tank (all-or-nothing) |
| lb_01 | 12 | 12 | 7 | 12 / 12 / 4 | — (idle player no longer loses, D-221) |
| lb_02 | 12 | 12 | 7 | 12 / 12 / 11 | — |
| lb_03 | 12 | 12 | 4 | 12 / 12 / 1 | tier-5 column 4300 / 7 riflemen; start force off ash |
| lb_04 | 12 | 12 | 12 | 12 / 12 / 12 | — |
| exp_01 | — | 12 | 11 | — / 12 / 12 | — |
| exp_02 | 12 | 12 | 12 | 12 / 12 / 12 | — |
| exp_03 | 12 | 12 | 9 | 12 / 12 / 12 | tier-5 bombers 2 at 3800 + 3 at 6400 |
| exp_04 | 12 | 12 | 11 | 12 / 12 / 5 | — |

Notes
- Root cause found three times: an enemy just outside a unit's weapon range (auth_01 jeep, dir_04 tank, auth_03 rocket nest) won unopposed;
  D-219 fixed it in the sim. The rule cuts both ways: enemy squads also chase what they see, which made auth_03, lb_03 and exp_03 harder.
- Several missions are all-or-nothing for the bot (auth_04, dir_04 end on fixed ticks), so tier 5 lands on 0 or 12 with no setting between.
- Tier 5 wins 12/12 in eight missions: meets the target but is no harder than tier 3 for the bot; a human play-test decides whether to raise it.
- Not covered by a bot walk: tut_01 (its own tests pass).
