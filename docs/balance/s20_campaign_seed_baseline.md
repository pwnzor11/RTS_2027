# Session 20 · campaign win rates over 12 seeds, ash on (M13-17 baseline)

Measured 2026-11-12 on integ-s20 (session 20 wave 1 merged: SP-03, SIMDET-03, SP-04, melee acquire, AI lanes), with the ashfall hazard ON
in missions (ash damage and the infantry ash path cost; skirmish rules). Each mission-tier was walked by the M8-15 campaign bot
(`CampaignWalks.Play`) with 12 different mission seeds (`seed = 1000 + k*7919`, k = 1..12); a run counts as a win when the bot wins inside
the mission's tick limit. Probe: `S20Sweep` and `tools/scratch_s20/make_seed_dirs.py` on branch `scratch/s20-ash-diag` (never merged).

Why seeds: the fixed-seed walks in BalanceTableTests were passing by luck. With ash OFF, auth_01 tier 3 wins 3/12, lb_01 tier 5 4/12,
dir_04 tier 5 0/12 and lb_03 tier 5 0/12; ash ON changed the rate notably only for dir_02 tier 5 (10/12 off, 6/12 on) (D-218 point 8).

Proposed targets (Q-75): tier 1 ≥ 11/12, tier 3 ≥ 9/12, tier 5 ≥ 4/12. **Below target** rows are marked.

| Mission | Tier | Wins /12 | Target | Winning end ticks (L = loss or over the limit) |
|---|---|---|---|---|
| auth_01 | 1 | 12 | ok | 5481 5481 5481 5481 6321 6321 6361 5441 5441 5441 5441 5441 |
| auth_01 | 3 | 3 | **below** | L L 5001 L L L 5001 L L L 5041 L |
| auth_01 | 5 | 10 | ok | 4641 4641 4601 4641 L L 4601 4601 4601 4601 4601 4601 |
| auth_02 | 1 | 12 | ok | 4801 4801 4801 4801 4801 4801 4801 4801 4801 4801 4801 4801 |
| auth_02 | 3 | 12 | ok | 4801 4801 4801 4801 4801 4801 4801 4801 4801 4801 4801 4801 |
| auth_02 | 5 | 12 | ok | 4801 4801 4801 4801 4801 4801 4801 4801 4801 4801 4801 4801 |
| auth_03 | 1 | 6 | **below** | 4180 4631 4356 L L L 4353 L L 4493 4983 L |
| auth_03 | 3 | 11 | ok | 4077 4412 4255 4027 4122 L 4475 4491 4132 4309 4519 5193 |
| auth_03 | 5 | 10 | ok | 5254 4477 4061 L 5065 4408 4225 4367 4186 L 4219 4477 |
| auth_04 | 3 | 12 | ok | 4347 4342 4318 4409 4317 4369 4369 4367 4378 4362 4340 4359 |
| auth_04 | 5 | 0 | **below** | L L L L L L L L L L L L |
| dir_01 | 3 | 12 | ok | 2321 2321 2321 2321 2321 2361 2321 2361 2321 2361 2361 2321 |
| dir_01 | 5 | 7 | ok | 4321 L 4321 4121 L L 2441 L 2481 L 5321 2481 |
| dir_02 | 1 | 12 | ok | 7890 7897 7888 7891 7898 7886 7886 7902 7890 7899 7891 7880 |
| dir_02 | 3 | 12 | ok | 7894 7901 7907 7897 7895 7901 7892 7905 7885 7896 7897 7891 |
| dir_02 | 5 | 6 | ok | 7875 L L 7872 L L 7872 7874 7860 7865 L L |
| dir_03 | 1 | 12 | ok | 9201 9241 9241 9241 9241 9241 9201 9241 9241 9241 9241 9241 |
| dir_03 | 3 | 12 | ok | 9241 9241 9241 9241 9241 9241 9241 9241 9241 9241 9241 9241 |
| dir_03 | 5 | 12 | ok | 9361 9321 9361 9321 9321 9321 9361 9321 9401 9361 9361 9361 |
| dir_04 | 1 | 12 | ok | 3208 3208 3208 3208 3208 3208 3208 3208 3208 3208 3208 3208 |
| dir_04 | 3 | 12 | ok | 3208 3208 3208 3208 3208 3208 3208 3208 3208 3208 3208 3208 |
| dir_04 | 5 | 0 | **below** | L L L L L L L L L L L L |
| lb_01 | 1 | 12 | ok | 2961 2941 2881 2921 2921 2881 2881 2901 2901 2921 2941 2941 |
| lb_01 | 3 | 12 | ok | 2941 2921 2921 2921 2941 2981 2921 2921 2921 2921 2961 2981 |
| lb_01 | 5 | 4 | ok | 3081 3081 L L 3201 L L L L L L 3101 |
| lb_02 | 1 | 12 | ok | 6801 6781 6661 6661 6821 6781 6601 6801 6881 6761 6841 6901 |
| lb_02 | 3 | 12 | ok | 7281 6621 6881 7361 6761 6901 6821 6861 6761 7041 7001 6961 |
| lb_02 | 5 | 11 | ok | 9341 7141 L 7141 8821 12121 7961 8621 7421 7281 9241 6821 |
| lb_03 | 1 | 12 | ok | 5121 5121 5121 5121 5101 5121 5121 5001 5081 5101 5121 5121 |
| lb_03 | 3 | 12 | ok | 4821 4821 4821 4921 5001 4821 4821 4821 5081 4821 4821 4821 |
| lb_03 | 5 | 1 | **below** | L L L L L L L L 4241 L L L |
| lb_04 | 1 | 12 | ok | 9861 9881 9821 9861 9861 9901 9881 9761 9881 9781 9861 9881 |
| lb_04 | 3 | 12 | ok | 10021 10001 9901 9941 9941 10041 10021 9941 9981 10021 10081 10001 |
| lb_04 | 5 | 12 | ok | 10301 10561 10341 10381 10281 10341 10361 10321 10501 10301 10401 10361 |
| exp_01 | 3 | 12 | ok | 5181 5181 5181 5201 5201 5181 5181 5201 5201 5181 5201 5201 |
| exp_01 | 5 | 12 | ok | 5121 5061 5081 5101 5001 5341 5061 5201 4941 5181 5201 4921 |
| exp_02 | 1 | 12 | ok | 7801 7801 7841 7841 7841 7841 7841 7841 7841 7801 7841 7841 |
| exp_02 | 3 | 12 | ok | 7801 7721 7841 7841 7841 7841 7841 7841 7841 7721 7841 7841 |
| exp_02 | 5 | 12 | ok | 8961 8881 8961 8861 8881 8841 8861 8861 8821 8881 8841 8861 |
| exp_03 | 1 | 12 | ok | 6981 6961 6941 6961 6961 6961 6961 6961 6961 6941 6921 6961 |
| exp_03 | 3 | 12 | ok | 7101 7081 7061 7141 7121 7141 7041 7101 7081 7021 7121 7081 |
| exp_03 | 5 | 12 | ok | 9261 9341 9341 9181 9181 9121 9101 9241 9081 9261 9261 9041 |
| exp_04 | 1 | 12 | ok | 12442 12442 12201 12461 12681 13761 16581 12741 12862 12841 12341 12741 |
| exp_04 | 3 | 12 | ok | 13121 13961 17241 12741 14161 15261 13661 12621 13921 12701 13921 15081 |
| exp_04 | 5 | 5 | ok | 14902 L L L 15141 L L 17321 16201 15921 L L |

Below target (5): auth_01 t3 (3/12), auth_03 t1 (6/12), auth_04 t5 (0/12), dir_04 t5 (0/12), lb_03 t5 (1/12).

Notes for the retune (session 21):
- auth_01 tier 3: the trace shows the four riflemen hold the survey site, lose the second raid by a few HP, then a surviving enemy vehicle
  slowly kills both scrapers while the bot stays put. Ash does not hit them en route (they detour). Candidate levers: tier-3 raid 2 size, the
  tier-3 start force, or the bot pulling scrapers back; the road at y=8 on rustbelt_m2 runs through an ash field (x19-26, y5-11) that is
  shared by six missions, so do not edit the map for one mission.
- Only two infantry actors start on ash (dir_01 conscript, exp_04 marine); zones with ash are field land inside bases (scan, session 20).
- auth_01 tier 5 wins 10/12 (Q-71 (a), D-217). exp_04 tier 5 wins 5/12 with ash on: its tick-30000 stall was an ash-off artefact.
- BalanceTableTests still runs one fixed seed per mission-tier with ash OFF; turning ash on (removing `MissionSession.CampaignRules`'
  override) changes those single results as listed in the s20 ash-on run: auth_01 t3, lb_01 t5, dir_02 t5, dir_04 t5, lb_03 t5 fail.
