# M8-15 campaign acceptance: bot play-through and tier table

Produced by `tests/Scripting.Tests/M8_15/BalanceTableTests.cs` (rows printed through `ITestOutputHelper`, lines starting `ROW |`).
Command: `dotnet test tests/Scripting.Tests -m:1 --filter "FullyQualifiedName~M8_15.BalanceTableTests" --logger "console;verbosity=detailed"`.
The same walks back `CampaignPlaythroughTests` (each faction in campaign order at tier 3, plus all sixteen, trait `Category=Campaign16`)
and `MidMissionSaveLoadTests` (save at half the win tick, load, finish the walk, compare the final state hash).

## How the numbers are made
- One scripted human (`MissionBot`) plays each mission with the winning walk of its own test file, refactored into `CampaignWalks.Play(missionId, tier, session)`.
  Tiers are the ones each walk's test supported: 3 and 5 for auth_01..04, dir_01, exp_01; 1, 3 and 5 for the rest. The walk is the same at every tier; only `Mission.Difficulty()` changes.
- Win tick is the sim tick when the mission outcome flipped to Won (20 ticks per second, `SimConstants.TicksPerSecond`); shown as mm:ss (tick).
- "Limit" is the design tick limit the per-mission test asserts (auth_01 14000, auth_04 16000, dir_01/exp_01/lb_01 20000, the other eleven 30000). "% of limit" is win tick over limit.
- Player losses = units + structures the human lost; enemy losses = units + structures every enemy slot lost (the sim's per-player stats counters; reinforcements the script spawns count once they die).
- No campaign AI plays the enemy slots here: enemies are what the mission script and its pre-placed actors provide. The campaign tech allow-list carry-over (`CampaignService.AllowListFor`, in `src/Game`) is not applied; each mission starts from its own `starts_with`.
- Runs are deterministic: the playthrough tests reproduce these ticks and loss counts exactly.

## Table

(Session 14 numbers; the first version of this table is in git history.)

| Mission | Tier | Won | Win time (tick) | Player losses | Enemy losses | Notes |
|---|---|---|---|---|---|---|
| auth_01 | 1 | yes | 4:45 (5701) | 4 | 5 | 40% of limit 11:40 (new row) |
| auth_01 | 3 | yes | 4:31 (5421) | 5 | 8 | 38% of limit 11:40 (unchanged) |
| auth_01 | 5 | yes | 5:37 (6741) | 6 | 12 | 48% of limit 11:40 (was 4:17, 5 losses) |
| auth_02 | 1 | yes | 4:00 (4801) | 0 | 14 | 16% of limit 25:00 (new row) |
| auth_02 | 3 | yes | 4:00 (4801) | 7 | 26 | 16% of limit 25:00 (unchanged) |
| auth_02 | 5 | yes | 4:00 (4801) | 22 | 50 | 16% of limit 25:00 (was 1 loss, 41 enemy) |
| auth_03 | 1 | yes | 3:37 (4344) | 0 | 27 | 14% of limit 25:00 (new row) |
| auth_03 | 3 | yes | 3:34 (4299) | 11 | 47 | 14% of limit 25:00 (unchanged) |
| auth_03 | 5 | yes | 3:34 (4292) | 20 | 76 | 14% of limit 25:00 (was 8 losses, 64 enemy) |
| auth_04 | 3 | yes | 3:44 (4486) | 15 | 40 | 28% of limit 13:20 |
| auth_04 | 5 | yes | 3:51 (4625) | 20 | 73 | 28% of limit 13:20 |
| dir_01 | 3 | yes | 1:57 (2341) | 1 | 5 | 11% of limit 16:40 |
| dir_01 | 5 | yes | 2:05 (2501) | 2 | 10 | 12% of limit 16:40 |
| dir_02 | 1 | yes | 6:34 (7890) | 4 | 11 | 26% of limit 25:00 |
| dir_02 | 3 | yes | 6:34 (7899) | 6 | 14 | 26% of limit 25:00 |
| dir_02 | 5 | yes | 6:35 (7905) | 39 | 30 | 26% of limit 25:00 |
| dir_03 | 1 | yes | 7:42 (9241) | 7 | 11 | 30% of limit 25:00 |
| dir_03 | 3 | yes | 7:42 (9241) | 11 | 13 | 30% of limit 25:00 |
| dir_03 | 5 | yes | 7:48 (9361) | 28 | 19 | 31% of limit 25:00 |
| dir_04 | 1 | yes | 2:40 (3208) | 1 | 10 | 10% of limit 25:00 |
| dir_04 | 3 | yes | 2:40 (3208) | 1 | 15 | 10% of limit 25:00 |
| dir_04 | 5 | yes | 2:40 (3208) | 8 | 21 | 10% of limit 25:00 |
| exp_01 | 3 | yes | 4:21 (5221) | 2 | 18 | 26% of limit 16:40 |
| exp_01 | 5 | yes | 3:54 (4681) | 6 | 23 | 23% of limit 16:40 |
| exp_02 | 1 | yes | 6:31 (7821) | 19 | 21 | 26% of limit 25:00 |
| exp_02 | 3 | yes | 6:12 (7441) | 20 | 29 | 24% of limit 25:00 |
| exp_02 | 5 | yes | 6:47 (8141) | 20 | 42 | 27% of limit 25:00 |
| exp_03 | 1 | yes | 5:45 (6901) | 0 | 12 | 23% of limit 25:00 |
| exp_03 | 3 | yes | 5:50 (7001) | 0 | 25 | 23% of limit 25:00 |
| exp_03 | 5 | yes | 7:36 (9121) | 7 | 72 | 30% of limit 25:00 |
| exp_04 | 1 | yes | 10:28 (12561) | 3 | 64 | 41% of limit 25:00 |
| exp_04 | 3 | yes | 11:14 (13481) | 45 | 86 | 44% of limit 25:00 |
| exp_04 | 5 | yes | 14:34 (17481) | 66 | 163 | 58% of limit 25:00 |
| lb_01 | 1 | yes | 2:51 (3421) | 2 | 5 | 17% of limit 16:40 |
| lb_01 | 3 | yes | 2:55 (3501) | 4 | 7 | 17% of limit 16:40 |
| lb_01 | 5 | yes | 3:19 (3981) | 13 | 17 | 19% of limit 16:40 |
| lb_02 | 1 | yes | 5:41 (6821) | 9 | 14 | 22% of limit 25:00 |
| lb_02 | 3 | yes | 5:52 (7041) | 11 | 20 | 23% of limit 25:00 |
| lb_02 | 5 | yes | 5:48 (6961) | 12 | 26 | 23% of limit 25:00 |
| lb_03 | 1 | yes | 4:14 (5081) | 2 | 24 | 16% of limit 25:00 |
| lb_03 | 3 | yes | 4:00 (4801) | 9 | 32 | 16% of limit 25:00 |
| lb_03 | 5 | yes | 3:50 (4601) | 14 | 42 | 15% of limit 25:00 |
| lb_04 | 1 | yes | 8:08 (9761) | 5 | 15 | 32% of limit 25:00 |
| lb_04 | 3 | yes | 8:16 (9921) | 8 | 21 | 33% of limit 25:00 |
| lb_04 | 5 | yes | 8:29 (10181) | 17 | 39 | 33% of limit 25:00 |

## Session 14 note
What changed since the first table: D-166 (balance numbers, including Conscript health 40 to 50), D-168 (two mission-tuning sets: tier knobs for lb_01, lb_03, lb_04, exp_03 and the earlier set), and objective targets on all 16 missions (M11-05; the mission state format is now 5, which changes mission state bytes but no walk). The walks themselves were not changed: every mission test and every walk passed on the combined state without edits, so nothing was weakened. Win ticks with and without targets are identical, so every shift below comes from D-166 and D-168. One engine fix came with the targets: an objective without a target wrote a null name into the state (crash as soon as a mission had some targets and some not).

## Reading (for the owner)
All 42 runs won, no script errors. Compared with the first table, tier 5 is now clearly harder than tier 1 in every mission where the bot plays both. Limits are still generous: the slowest win is exp_04 at tier 5 (58% of limit).

- **Clear tier steps (losses and/or time):** exp_04 (3 to 66 player losses, 10:28 to 14:34), dir_02 (4 to 39), dir_03 (7 to 28), lb_04 (5 to 17), lb_03 (2 to 14), lb_01 (2 to 13), exp_03 (0 to 7 losses, 5:45 to 7:36), dir_04 (1 to 8). Tier 5 feels harder than tier 1 here.
- **Moderate step:** lb_02 (9 to 12), exp_01 (tier 3 to 5: 2 to 6), auth_04 (tier 3 to 5: 15 to 20), dir_01 (1 to 2, a short mission), exp_02 (19 to 20 at tier 1 and 5, about 20 at tier 3: the garrison fight is hard at every tier and the tier knob does little).
- **Not harder at tier 5:** auth_01 (5 and 5 losses, tier 5 even wins sooner), auth_02 (7 losses at tier 3, 1 at tier 5; the 4:00 win time is identical, so a script timer decides it), auth_03 (11 to 8 losses). These three have only tiers 3 and 5 and the bot's losses are noisy; a human is unlikely to feel a difference. Worth a look if the owner wants tier 5 to bite in the Authority opening missions.
- **Win time:** dir_04 is 2:40 at every tier (a fixed timer), and dir_02, dir_03, dir_01, lb_02 and exp_02 change by under 5%. Tier shows up as losses there, not time. lb_03 is faster at tier 5 (3:50 against 4:14).
- **Still easy for this bot:** exp_03 tier 1 and exp_01 (0 to 2 losses at easy tiers), auth_02 at tier 5 (1 loss against 41 enemy losses). The Expedition missions are easier at low tiers but now bite at tier 5.
- Finales: dir_04 (2:40) is still short next to dir_03 (7:42 to 7:48); exp_04 is now the longest at tier 5 (14:34, 163 enemy losses).

## Session 15 (Q-66)
Owner decision Q-66 (D-175): the Authority opening missions get tier knobs like the other nine. Only the mission scripts of auth_01..03 changed (tier 3 is exactly the old design in all three; the 3-tier rows above are unchanged), and `CampaignWalks.Tiers` now also walks tier 1 for them, so these missions have 1, 3 and 5 rows. All 9 runs won, tier 5 is clearly harder than tier 3, which is harder than tier 1.
- **auth_01** (tutorial-like, stays gentle at tier 1 and 3): the raid sizes were already tiered and are untouched; the new knob is arrival time. Raid 1 arrives 300 ticks after the scrapers reach the site (tiers 1-3) and 260 / 220 at tiers 4 / 5; the raid-2 timer is 1500 (tiers 1-3), 1400 and 1300, so at tier 5 the two raids overlap. Player losses 4 / 5 / 6, enemy losses 5 / 8 / 12, win time 4:45 / 4:31 / 5:37 (tier 5 has to fight longer).
- **auth_02**: raid sizes scale 2 to 9 conscripts with more Ashthrowers and Sappers at tiers 4-5, and the four waves arrive up to 200 ticks earlier at tier 5 (later at tier 1). Player losses 0 / 7 / 22, enemy losses 14 / 26 / 50. The win tick stays 4:00 because the relief timer (4800) decides it.
- **auth_03**: yard waves, road ambush, dune riders and shore counter-attack scale (tier 1: 4 conscripts per group, tier 5: 12 to 16), and the yard waves come up to 1000 ticks earlier at tier 5. Player losses 0 / 11 / 20, enemy losses 27 / 47 / 76. Win time is flat (3:34 to 3:37) because the convoy march sets it.
- Limits: all still under 16% (auth_02/03) or 48% (auth_01) of their design limit. Unit counts and timings only (D-061).
- Tried and dropped: a third raid and 160-tick raid timers at auth_01 tier 5 (the bot could not win inside the limit; the walk was not changed).
