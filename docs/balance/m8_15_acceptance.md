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

| Mission | Tier | Won | Win time (tick) | Player losses | Enemy losses | Notes |
|---|---|---|---|---|---|---|
| auth_01 | 3 | yes | 4:31 (5421) | 5 | 8 | 38% of limit 11:40 |
| auth_01 | 5 | yes | 4:17 (5141) | 5 | 12 | 36% of limit 11:40 |
| auth_02 | 3 | yes | 4:00 (4801) | 1 | 26 | 16% of limit 25:00 |
| auth_02 | 5 | yes | 4:00 (4801) | 19 | 41 | 16% of limit 25:00 |
| auth_03 | 3 | yes | 3:28 (4176) | 11 | 45 | 13% of limit 25:00 |
| auth_03 | 5 | yes | 3:46 (4537) | 11 | 64 | 15% of limit 25:00 |
| auth_04 | 3 | yes | 3:42 (4441) | 12 | 43 | 27% of limit 13:20 |
| auth_04 | 5 | yes | 3:39 (4384) | 13 | 55 | 27% of limit 13:20 |
| dir_01 | 3 | yes | 1:57 (2341) | 1 | 5 | 11% of limit 16:40 |
| dir_01 | 5 | yes | 2:05 (2501) | 2 | 10 | 12% of limit 16:40 |
| dir_02 | 1 | yes | 6:35 (7902) | 5 | 12 | 26% of limit 25:00 |
| dir_02 | 3 | yes | 6:35 (7916) | 7 | 14 | 26% of limit 25:00 |
| dir_02 | 5 | yes | 6:34 (7899) | 25 | 20 | 26% of limit 25:00 |
| dir_03 | 1 | yes | 7:42 (9241) | 10 | 11 | 30% of limit 25:00 |
| dir_03 | 3 | yes | 7:42 (9241) | 15 | 13 | 30% of limit 25:00 |
| dir_03 | 5 | yes | 12:40 (15201) | 56 | 18 | 50% of limit 25:00 |
| dir_04 | 1 | yes | 2:40 (3208) | 2 | 12 | 10% of limit 25:00 |
| dir_04 | 3 | yes | 2:40 (3208) | 2 | 15 | 10% of limit 25:00 |
| dir_04 | 5 | yes | 2:40 (3208) | 8 | 21 | 10% of limit 25:00 |
| exp_01 | 3 | yes | 4:38 (5561) | 0 | 15 | 27% of limit 16:40 |
| exp_01 | 5 | yes | 4:46 (5721) | 0 | 22 | 28% of limit 16:40 |
| exp_02 | 1 | yes | 6:31 (7821) | 19 | 21 | 26% of limit 25:00 |
| exp_02 | 3 | yes | 6:12 (7441) | 20 | 29 | 24% of limit 25:00 |
| exp_02 | 5 | yes | 6:47 (8141) | 20 | 42 | 27% of limit 25:00 |
| exp_03 | 1 | yes | 5:44 (6881) | 0 | 16 | 22% of limit 25:00 |
| exp_03 | 3 | yes | 5:46 (6921) | 6 | 25 | 23% of limit 25:00 |
| exp_03 | 5 | yes | 5:57 (7141) | 10 | 43 | 23% of limit 25:00 |
| exp_04 | 1 | yes | 10:04 (12081) | 0 | 42 | 40% of limit 25:00 |
| exp_04 | 3 | yes | 10:33 (12661) | 0 | 52 | 42% of limit 25:00 |
| exp_04 | 5 | yes | 10:32 (12641) | 1 | 69 | 42% of limit 25:00 |
| lb_01 | 1 | yes | 2:51 (3421) | 2 | 5 | 17% of limit 16:40 |
| lb_01 | 3 | yes | 2:55 (3501) | 4 | 7 | 17% of limit 16:40 |
| lb_01 | 5 | yes | 2:59 (3581) | 12 | 11 | 17% of limit 16:40 |
| lb_02 | 1 | yes | 5:41 (6821) | 9 | 14 | 22% of limit 25:00 |
| lb_02 | 3 | yes | 5:52 (7041) | 11 | 20 | 23% of limit 25:00 |
| lb_02 | 5 | yes | 5:48 (6961) | 12 | 26 | 23% of limit 25:00 |
| lb_03 | 1 | yes | 4:17 (5141) | 1 | 18 | 17% of limit 25:00 |
| lb_03 | 3 | yes | 4:17 (5141) | 0 | 25 | 17% of limit 25:00 |
| lb_03 | 5 | yes | 4:43 (5661) | 11 | 35 | 18% of limit 25:00 |
| lb_04 | 1 | yes | 8:15 (9901) | 7 | 15 | 33% of limit 25:00 |
| lb_04 | 3 | yes | 8:16 (9921) | 8 | 21 | 33% of limit 25:00 |
| lb_04 | 5 | yes | 8:18 (9961) | 11 | 27 | 33% of limit 25:00 |

## Reading (for the owner)
All 42 runs won, with no script errors and no locale warnings. Nothing is close to its limit: the slowest win is dir_03 at tier 5 (50% of limit), then exp_04 (40-42%) and auth_01 (36-38%). The limits are generous; a real player has a lot of slack.

- **Tier barely changes these missions.** Win time is identical or within a few percent across tiers for dir_02 (6:34-6:35), dir_04 (2:40 at all three tiers), lb_01, lb_03, lb_04, auth_04, exp_03. Win time in dir_04 and auth_02 (4:00 at tiers 3 and 5) may be decided by a script timer or charge (not checked), not by how hard the enemy is. What a tier does change is mostly the enemy loss count (the script sends more at higher tiers, e.g. exp_02 29 to 42, exp_04 52 to 69, auth_03 45 to 64) and sometimes the player's losses. Flag: tiers 1 to 5 feel the same to this bot; check whether the tier knobs of these scripts do anything a human would notice.
- **Too easy for this bot at every tier:** exp_01 (0 player losses), exp_04 (0 losses at tiers 1 and 3, 1 at tier 5, for a finale), lb_03 (0 or 1 losses at tiers 1 and 3), exp_03 (0 at tier 1), auth_02 at tier 3 (1 loss against 26). The Expedition missions are the softest: the bot sits behind pillboxes and Glide Tanks and loses nothing.
- **Hardest for this bot:** dir_03 at tier 5 (56 player losses, win at 12:40 against 7:42 at tier 3, the only real tier step in the table), dir_02 at tier 5 (25 losses against 7 at tier 3), exp_02 (about 20 losses at every tier, so the garrison fight is hard but not tier-driven) and auth_02 at tier 5 (19 losses against 1 at tier 3). These are the tier-5 spikes a human would feel; whether they are intended is the owner's call.
- **Odd tier order:** auth_01 wins sooner at tier 5 (4:17) than at tier 3 (4:31), and exp_02 at tier 1 (6:31) is slower than at tier 3 (6:12). Bot timing noise, not a defect, but worth knowing if someone tunes by win time.
- **Finale pacing:** dir_04 (2:40) and auth_04 (3:42) end faster than the middle missions of their campaigns; lb_04 (8:16) and exp_04 (10:33) are the long finales. A Directorate finale that ends in under three minutes is short next to dir_03 (7:42).
- The pre-existing per-mission tests cover the same walks; nothing here needed a mission change, and no mission was edited.
