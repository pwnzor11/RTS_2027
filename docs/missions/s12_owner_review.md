# Session 12 campaign: owner review list (M8-12, D-163)

> **Answered in session 13 (D-164):** the owner played the campaign and kept every choice as written; ending slides, briefings and
> radio lines are signed off. The three bot-found balance issues are measured in docs/balance/s13_campaign_balance.md (numbers change only with approval, D-152).

All 16 missions are written, tested by a bot at tiers 1/3/5 (or 3/5) and run with no script errors. Nothing here blocks play; each line
is a choice the writers made that the owner may want to change. Ending slides and radio lines are DRAFT text (D-086).

## Read first
- Ending slides: data/campaigns/{authority,directorate,left_behind,expedition}_ending.toml
- Campaign pages: docs/missions/{authority,directorate,left_behind,expedition}_campaign.md
- Radio voice: each mission's locale.toml

## Choices to confirm
| # | Mission | Choice made | Alternative |
|---|---|---|---|
| 1 | auth_04 | Winning unlocks the Authority atomic battery (superweapon) | No unlock; the lore calls the Sunrise "a second Exchange" |
| 2 | auth_04 | The enemy AI may fire the pre-placed Sunrise Device before the 16000-tick timer | Device inert until the timer |
| 3 | dir_02-04 | The player starts with a pre-built base | Rebuild from the shaft each act |
| 4 | dir_04 | Win = fire the Sunrise at the Authority command post; destroying their base is not a bonus | Add it as a bonus |
| 5 | lb_02-04 | Posts, hatch field and columns are scripted (role "neutral"); only lb_04 runs the built-in AI | Real AI in more missions |
| 6 | lb_04 | No objective needs the Second Drift (its storm does no damage) | A bonus for firing it (needs an API to detect the shot) |
| 7 | exp_02 | Set on the Glass Coast (Rust Belt has no sea) at a new north-shore landing | — |
| 8 | exp_02 | The player gets the Plotting Room, but its Archive entry unlocks on winning exp_02 | Move that Archive unlock to exp_01 |
| 9 | exp_02-04 | Marines barely hurt emplacements, so Glide Tanks (tier 2) are needed from act 2 | Tune marines vs fortified armour |
| 10 | all | Cameo radio lines from other factions' leaders (e.g. Wrenfield, Larkin, Thrall, Kell) | Remove any |

## Found in play-testing by the bots (rules, not missions)
- The Ashthrower's ash (contaminate 40) kills the Directorate player's own Conscripts (about 25 lost in one dir_02 bot run).
- One Authority bomber killed a 1000 HP Expedition depot in about 1000 ticks even with 3 SAM Mounts: air balance is untuned.
- Directorate Conscripts (40 hp) lose badly to Authority Riflemen; mission waves were sized around it.

## API ideas (later)
- A way to keep an enemy team alive without a hidden "reserve" building (several missions place one in a far corner).
- A "park only" option for `Reinforce.Rig` (lb missions park the rig on an overlay cell so the player deploys it).
- Detect a superweapon firing (dir_04 detects the Sunrise shot by its target dying).
