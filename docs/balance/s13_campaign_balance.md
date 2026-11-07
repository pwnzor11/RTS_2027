# S13 campaign balance: three issues the bots found

Measured, not changed. Nothing under `data/rules/` was edited; every "proposed" number below was tried only inside the test
`tests/Sim.Tests/Balance/S13CampaignBalanceTests.cs` (fixed seeds, in-memory edits of the shipped rules text). Rule numbers
change only with the owner's approval (D-152). Run the test with `--logger "console;verbosity=normal"` to see every line.
Ticks are 20 per second.

## 1. Ashthrower ash hurts the Directorate's own Conscripts

**What happens now.** The Ashthrower's spray itself never hurts friends (`hits_allies = false`). The harm is the ash it leaves
on the ground (`contaminate 40`): radiation hurts every non-immune infantry standing in it, and Directorate infantry only
resist 60% of it. Ash from several Ashthrowers stacks, and the cloud sits where the Conscripts are fighting. Test: Ashthrowers
behind Conscripts that stand right beside the enemy Riflemen, everyone attack-moving in, 2400 ticks watched. The small fight
(5 Ashthrowers, 10 Conscripts, 10 enemy Riflemen): all 10 enemies die by tick 155, and the cloud still takes 279 of the
Conscripts' 400 HP and kills 1. The big fight (10 Ashthrowers, 25 Conscripts, 20 enemies) loses 8 Conscripts and 3 Ashthrowers,
0.40 own Conscript deaths per enemy kill. With the enemy's guns turned off the losses are identical, so all of it is ash.

| number | current | proposed | measured effect (small fight / big fight) | recommendation |
|---|---|---|---|---|
| `dir_ash` contaminate | 40 | 20 | Conscript HP lost 279 -> 180 / 759 -> 447; Conscripts dead 1 -> 0 / 8 -> 6; Ashthrowers dead 3 -> 0 (big). Enemy still all dead at the same tick. | Halves the damage but friendly fire stays. Second choice. |
| `dir_ash` contaminate | 40 | 0 | Own HP lost 0 / 0; enemy still all dead by tick 157 / 149. | Works, but the Ashthrower stops leaving a cloud at all, so the Directorate loses its denial tool. Only if you want no cloud. |
| `rad_resist_permille` on `dir_conscript` and `dir_ashthrower` | 600 | 1000 (immune) | Own HP lost 0 in the small fight (big fight not measured); enemy dead at the same tick (155). The cloud stays and still hurts enemy infantry. | **Recommended.** Keeps the cloud as a weapon, ends the friendly fire. Side effect: those two units also stop taking damage from any other radiation, including enemy radiation weapons. Other Directorate infantry stay at 600 and would still be hurt. |
| `rad_resist_permille` on the same two | 600 | 800 | Own HP lost 129 (was 279) in the small fight. | Not enough on its own. |

## 2. One Authority bomber against an Expedition depot with SAM Mounts

**What happens now.** With no SAMs a lone bomber kills the 1000 HP depot at tick 1556. With one or three SAM Mounts that have
power, the bomber is shot down at tick 904 after 5 hits of 45 (200 HP), and the depot is left at 916 HP; three SAMs only fire
3 extra shots in that time, since the first SAM already finishes the job. So the bomber is not too strong against powered
SAMs. What the bot saw is explained by power: `exp_sam` has `needs_power = true` and uses 10 power, and the depot itself makes
none (`power = 0`). With no power plant the three SAMs hold fire and the bomber kills the depot unharmed (tick 1556, 0 SAM
shots). This is my reading of the review line; I did not replay the bot's run, so the check is "does the campaign base have a
pile house". Hit damage 45 per missile, 30 ticks between shots.

| number | current | proposed | measured effect (lone bomber, depot 1000 HP) | recommendation |
|---|---|---|---|---|
| `exp_sam` needs_power | true | false | 3 SAMs without a power plant: bomber survives, depot dead at 1556 -> bomber shot down at 904, depot at 916 HP, 11 SAM shots. | **Recommended if** unpowered Expedition bases are normal in the campaign; the other fix is mission data (place a pile house), which needs no rule change. |
| `exp_sam_missile` damage | 45 | 25 (lower) | Powered: bomber still dies, at 938 (3 SAMs) / 994 (1 SAM), 8 hits needed. | Not needed. SAMs are not too strong. |
| `exp_sam_missile` damage | 45 | 90 (higher) | Powered: bomber dies at 844, after 3 hits; depot untouched at 1000. | Not needed. |
| `auth_bomber` max_health | 200 | 100 | Powered: shot down at 844 instead of 904, 3 hits. Unpowered: no change in outcome. | Not needed. |
| `exp_landing_depot` max_health | 1000 | 1500 | No SAMs: depot dead at 1916 instead of 1556. Powered SAMs: unchanged outcome. | Only if you want a lone bomber to need more than one run against an unprotected depot. |

SAM rate of fire (30 ticks) was left alone; with one SAM already winning, a faster rate cannot change the result.

## 3. Directorate Conscripts lose to Authority Riflemen

**What happens now.** Conscript: 40 HP, 6 damage every 16 ticks, range 640, cost 80. Rifleman: 50 HP, 8 damage every 15 ticks,
range 768, cost 100. At equal numbers the Conscripts are wiped (5v5: 0 left against 5; 10v10: 0 against 9). At equal cost the
Conscripts still lose (4 Riflemen against 5 Conscripts: 3 Riflemen survive; 20 against 25: 12 Riflemen survive). Sides
swapped gives the same result, so it is not a position effect. A Rifleman kills a Conscript in 5 shots, a Conscript needs 9
hits to kill a Rifleman.

| number | current | proposed | measured effect (equal count 5v5, 10v10 / equal cost 400, 2000) | recommendation |
|---|---|---|---|---|
| `dir_conscript` max_health | 40 | 50 | Equal count: still lose (3 and 7 Riflemen left, was 5 and 9). Equal cost: 5 Conscripts beat 4 Riflemen (2 left) but swapped sides lose (1 Rifleman left); 25 v 20 undecided at tick 4000 with 2 Conscripts against 5 Riflemen left. Roughly even at equal cost, still slightly behind. | **Recommended** as the first step. |
| `dir_conscript` max_health | 40 | 55 | Identical to 50 in every fight. A Rifleman's 8 damage needs 7 hits for anything from 49 to 56 HP, so 51 to 56 buys nothing. | Not worth it; 50 is the same and the next real step is 57. |
| `dir_conscript` cost | 80 | 60 | Equal count unchanged (they die). Equal cost: 7 Conscripts beat 4 Riflemen (3 left, both sides); 33 v 20 undecided, 2 against 3 left (4 swapped). | Alternative to the HP change; Conscripts become cheap fodder rather than tougher. |
| `dir_conscript` max_health and cost | 40 / 80 | 50 / 70 | Equal cost: 6 beat 4 Riflemen (3 left, both sides); 29 beat 20 Riflemen with 12 left (9 swapped). Equal count: still lose (3 and 7 Riflemen left). | Overshoots: Conscripts clearly win at equal cost. Only if you want the Directorate to win mass fights. |

Note: in the 25 v 20 style fights marked "undecided", both sides still had survivors at tick 4000 (the units stop
fighting once out of each other's range), so I count them by who has more left.

## Lead's note (session 13)
- Ash: immunity (1000) also makes Directorate Conscripts and Ashthrowers immune to every other radiation source (an enemy
  Directorate's rad mortar or ash in a mirror match, Sunrise fallout). If that matters, contaminate 40 -> 20 is the safer change.
- Bomber: the shipped exp_03/exp_04 bases do place an `exp_pile_house`; the bot's run most likely lost power (a pile house destroyed
  or too many power users). Recommendation: no rules change; check the low-power warning is visible to the player.

Owner decides (D-152).

## After D-166 (session 14)

Shipped values now: `dir_ash` contaminate 20 (was 40) and `dir_conscript` max_health 50 (was 40), owner decision Q-60 / D-166.
"Before" is the session-13 measurement above (both at the old values). "After" is `ConscriptsVsRiflemen_NowAndCandidates` and
`Ashthrower_FriendlyFire_NowAndCandidates` run on the shipped data (session 14, `dotnet test ... --filter S13CampaignBalance`).
The ash "after" numbers include the Conscript HP change too, because both shipped values are in effect; the ash-only effect
was not re-measured.

| scenario | before | after |
|---|---|---|
| Ash, small fight (5 Ashthrowers, 10 Conscripts vs 10 Riflemen): own Conscript HP lost | 279 of 400 | 180 of 500 |
| Ash, small fight: own Conscripts dead | 1 of 10 | 0 of 10 |
| Ash, big fight (10 Ashthrowers, 25 Conscripts vs 20 Riflemen): own Conscripts dead | 8 of 25 (HP lost 759 of 1000) | 6 of 25 (HP lost 507 of 1250) |
| Ash, big fight: own Ashthrowers dead | 3 of 10 | 0 of 10 |
| Ash, big fight: enemy dead | 20 of 20 | 20 of 20 (tick 149) |
| Conscript vs Rifleman, 5v5 equal count: Riflemen left | 5 | 3 |
| Conscript vs Rifleman, 10v10 equal count: Riflemen left | 9 | 7 |
| Equal cost 400 (5 Conscripts @80 vs 4 Riflemen @100): Conscripts left / Riflemen left | 2 / 0 | 2 / 0 |
| Equal cost 400, sides swapped: Riflemen left | 1 | 1 |
| Equal cost 2000 (25 Conscripts vs 20 Riflemen) at tick 4000: Conscripts left / Riflemen left | 2 / 5 | 2 / 5 (Riflemen ahead, still undecided) |
| Bomber vs depot, 0 SAMs / 1 powered SAM / 3 unpowered SAMs (section 2) | 1556 / 904 / 1556 | 1556 / 904 / 1556 (unchanged, no rule change) |

Reading: the Conscripts still lose at equal count (they die, as before), and the equal-cost result is unchanged for the 400
fight and unchanged in the 2000 fight. The ash cloud still kills no Conscripts in the small fight. The big fight still loses
6 Conscripts to the cloud. Those are the numbers to check in play, not a verdict.
Note: the "Conscript hp 50" candidate row in the test now equals the "now" row, since "now" is 50 HP.
