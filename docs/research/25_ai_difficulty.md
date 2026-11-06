# AI difficulty tiers: what reference RTS games do (M4-16)

Read-only research for M4-16. Our rule: the AI is fog-honest and gets no resource or information cheats (D-061). Tiers must differ by decision quality.

## 1. Summary

- Reference games separate tiers mainly with decision levers: how often the AI decides, how old its information may be, when it attacks and how big the army is, when it retreats, what it builds, and how many workers it runs. All of these can be honest.
- Reference games also use cheats. EA's difficulty table scales firepower, armour, rate of fire, cost and build time per level. 0 A.D. Petra multiplies gather and trade rates (0.42 to 1.56) and scales build time (1.40 to 1.00). Both break D-061 and are excluded.
- Our own profile points at the cause of the flat ladder. In `data/ai/difficulty.toml` the attack threshold falls with tier (10, 10, 8, 7, 6 for tiers 1 to 5). Our round-3 trace showed that a lower count makes the higher tier send small waves into a bigger defence. The direction of the profile is the problem, not only its numbers.
- The better reference pattern gates attacks on relative strength, not on unit count. OpenRA's `AttackOrFleeFuzzy` compares own attack power with the enemy's and decides attack or flee. Our round-4 wave gate uses a fixed 100 % ratio at every tier 2+, so the ratio does not rise with tier.
- UAlbertaBot runs a combat simulation before each regroup or retreat decision. That is the honest "strong AI" pattern: predict the local fight from our own unit stats and pull back when it loses.
- Petra ties defender counts and attack sizes to difficulty. That is a decision about allocation, so it is honest.
- Round 4 found that elite scouting together with harassment cripples Directorate elite. Scouting and harassment should be separate switches.

## 2. Per source

### EA C&C Remastered (`REDALERT/`, repo electronicarts/CnC_Remastered_Collection)
- Difficulty is a three-level enum (`REDALERT/DEFINES.H`). `HouseClass::Assign_Handicap` in `REDALERT/HOUSE.CPP` applies a per-level table (read by `Difficulty_Get` in `REDALERT/RULES.CPP`) of firepower, armour, rate of fire, cost, build time and build speed. This is a resource and combat bias, so it is a cheat under our rules.
- Separately, an IQ number (`RULES.CPP`, IQ section) switches behaviours on at thresholds: superweapons, production, aircraft, harvester balancing, scatter, repair and sell, guard area. This on/off capability ladder is the honest shape, and our flags already use it.
- Top-level IQ also makes all AI houses "paranoid" (`HOUSE.CPP`, `Computer_Paranoid`), a reaction-mode switch rather than a bonus.
- One harvester-balancing check is turned off on the hardest level (`HOUSE.CPP`, the `Difficulty != DIFF_HARD` test), so difficulty also changes decisions directly.

### OpenRA (repo OpenRA/OpenRA)
- In this version the built-in bots differ by type, not by skill: `rush`, `normal`, `turtle`, `naval` (`mods/ra/rules/ai.yaml`). I found no Easy, Normal, Hard or Brutal setting in the RA rules. So OpenRA does not separate tiers by skill here.
- The types differ in data: squad size (rush 20, normal 40, turtle 10), unit-builder weights, and timers. Turtle sets its rush interval and minimum attack delay to 100 000 ticks, which means it effectively never attacks. Timers live in `OpenRA.Mods.Common/Traits/BotModules/SquadManagerBotModule.cs`.
- `Squads/AttackOrFleeFuzzy.cs` holds fuzzy rules over relative attack power. The rush variant attacks only at "strong" relative power and flees when weak. This is the strength-gate pattern.
- Respond-to-attack has a 30-tick cooldown so the AI does not chase every hit.
- Whether OpenRA bots respect fog of war was not verified.

### openage and freeaoe (our own notes)
- openage has no AI in its C++ engine yet (`docs/research/20_openage.md`). Nothing to take for difficulty.
- freeaoe resolves difficulty as a constant in its rule script at load time, and tunes the built-in engine through "strategic numbers" (`docs/research/21_freeaoe.md`). The lesson is one script with difficulty as data, not the code, which is unfinished.

### UAlbertaBot (repo davechurchill/ualbertabot, `UAlbertaBot/Source/`)
- The strategy is chosen from configuration (`Config.cpp`). `StrategyManager.cpp` keeps per-opponent result files so a later game can choose a strategy from past results. Cross-game learning is honest.
- Build orders come from BOSS, a search with a time budget (`BOSSFrameLimit` in `Config.cpp`, used in `BOSSManager.cpp`).
- `Squad.cpp` calls `CombatSimulation::simulateCombat()` before a regroup or retreat and uses the score. This is the pattern for an elite tier.
- The SparCraft manager under `research/sparcraft/` exists, but I found no call to it from the rest of the bot source. Treat it as research code.
- The Churchill, Saffidine and Buro work on predicting army combat outcomes (AIIDE 2013) is the basis for the simulation approach. I read its title in search results only.

### 0 A.D. Petra (repo 0ad/0ad, `binaries/data/mods/public/simulation/ai/petra/`)
- Difficulty is an integer from 0 (Sandbox) to 5 (Very Hard) (`config.js`). Many settings scale with it: defender count (`baseManager.js`, 3 + 2 × (level − 3) from Medium up), sentry towers, worker targets (by max population), trade targets, and attack size (`attackPlan.js`: level ± 1 units, one more for a huge attack).
- Low levels cut attack priority and randomise sizes less (`attackPlan.js`).
- `Cheat()` in `config.js` multiplies gather rate and trade gain, and build time. This is the economy cheat we exclude.
- Honest parts to copy in spirit: defence and attack size tied to difficulty, and difficulty-aware priorities.

## 3. Lever table

| Lever | Who uses it | Honest? | Expected effect | How we could apply it in RTS_2027 |
|---|---|---|---|---|
| Strength-based attack gate (attack only when own value is at least k × scouted enemy value) | OpenRA `AttackOrFleeFuzzy`; our wave gate | Yes, if the enemy value is from scouting | Fewer feeding waves at high tiers | Tier 2 keeps 100 %. Raise k with tier, for example 1.0 at tier 2 up to about 1.5 at tier 5 (values to tune). |
| Retreat on predicted loss | UAlbertaBot `Squad.cpp` with `CombatSimulation` | Yes, using our own stats | Better trades; fewer whole-army losses | Integer-only local fight estimate from our unit stats. Elite and commissar only. |
| Attack threshold rising with tier | Petra sizes attacks by difficulty | Yes | Higher tier waits for a real advantage | Replace the falling count (10 to 6) with a rising value ratio. |
| Defender count by tier | Petra `baseManager.js` | Yes | Higher tier holds home and frees the army | Tier 3 and up keep 3 + 2 × (tier − 3) defenders at home. |
| Decision interval and command delay | EA IQ gates; our profile | Yes | Faster reactions at high tiers | Already used. Keep as is; do not add more. |
| Scouting without harassment | Round-4 elite test | Yes | Keeps information without the harassment cost | Split the `scouting` and `harass` flags for elite. |
| Counter-mix by seen enemy armour | OpenRA unit-builder weights; our `enemy_aware_mix` | Yes | Better trades against armour | Already at tier 3 and up. Do not extend first. |
| Build-order search with time budget | UAlbertaBot BOSS | Yes | Better openers | Not now: our repertoire is fixed and the search cost is large. |
| Firepower, armour, cost or build-time bias by level | EA `Assign_Handicap` | No | Would make tiers stronger | Excluded (D-061). |
| Gather-rate or build-time multiplier | Petra `Cheat()` | No | Would make tiers stronger | Excluded (D-061). |

## 4. Recommendations for M4-16 (ordered)

1. **Make the attack gate rise with tier in strength terms.** Replace the falling thresholds with a value ratio to the scouted enemy army, rising with tier. Check: `ai match` on the 4 v 3 and 5 v 4 cells, both sides, 64 seeds each (see note 5). Success: higher tier wins at least 55 % of decisive games in each cell, and no cell inverts. Also trace launch values with `--trace`: median launch ratio at tiers 4 and 5 should be at least 1.0.
2. **Add a predicted-loss retreat for elite and commissar.** Integer-only, using our own unit stats, modelled on UAlbertaBot's squad check. Check: units lost per enemy unit killed at tier 4 against tier 3 in traces, which should fall below round 4. Decisive win rate for elite over veteran should reach at least 50 % in both factions.
3. **Split scouting from harassment for elite.** Round 4 shows the combined flag hurts Directorate (3 % with harassment off, 19 % with it on, both at elite). Check: Directorate 4 v 3 decisive rate at least 45 %, and Authority 4 v 3 not worse than round 4's 33 % beyond noise.
4. **Tie defender count to tier (Petra pattern).** Check: the draw rate in 5 v 4 cells (round 4 had 18 of 64 draws for Authority, 27 of 64 for Directorate). Fewer draws with an unchanged win rate would show that the top tiers stop stalling; a rise in draws would show they wait too long.
5. **Control noise before judging.** Round 4 used 64 games per cell, with roughly ±12 points of noise. A 10-point gain can hide inside that. Run the 4 v 3 and 5 v 4 cells at 128 games per cell (64 seeds × 2 sides) before deciding a lever failed. The seed count option of `ai match` is what round 4 used; double it.

## 5. Sources

- EA C&C Remastered Collection: https://github.com/electronicarts/CnC_Remastered_Collection (read: `REDALERT/DEFINES.H`, `REDALERT/HOUSE.CPP`, `REDALERT/RULES.CPP`; described in own words, D-050)
- OpenRA: https://github.com/OpenRA/OpenRA (read: `mods/ra/rules/ai.yaml`, `OpenRA.Mods.Common/Traits/BotModules/SquadManagerBotModule.cs`, `OpenRA.Mods.Common/Traits/BotModules/Squads/AttackOrFleeFuzzy.cs`)
- openage and freeaoe: our notes `docs/research/20_openage.md` and `docs/research/21_freeaoe.md`
- UAlbertaBot: https://github.com/davechurchill/ualbertabot (read: `UAlbertaBot/Source/Squad.cpp`, `CombatSimulation.cpp`, `BOSSManager.cpp`, `StrategyManager.cpp`, `Config.cpp`, `research/sparcraft/`)
- 0 A.D.: https://github.com/0ad/0ad (read: `binaries/data/mods/public/simulation/ai/petra/config.js`, `attackPlan.js`, `baseManager.js`, `headquarters.js`)
- Churchill, Saffidine, Buro, "Predicting Army Combat Outcomes in StarCraft" (AIIDE 2013): https://skatgame.net/mburo/ps/aiide13-combat-outcomes.pdf (title only, from search)

Not verified: OpenRA fog behaviour; whether UAlbertaBot's SparCraft manager runs in a match; any in-game play of any reference game. All clones succeeded.
