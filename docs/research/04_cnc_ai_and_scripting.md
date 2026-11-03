# C&C House AI, Base Building, Teams, Triggers and Mission Scripting

## Summary

- The C&C computer player is three loosely coupled systems: a **production advisor** (`AI_Building/Unit/Infantry/Aircraft`: ratio tables and weighted dice), an **"Expert" loop** (`Expert_AI`, every ~5 s: picks an enemy, orders mass `HUNT` attacks) and a **team system** (grouped units running small scripts).
- Nearly all numbers are INI data (`[AI]`, `[IQ]` in RULES.INI). Strongest point of the design.
- "Difficulty" is mostly stat **multipliers**; real capability gating is a separate **IQ 0-5** scale where each threshold unlocks a behaviour. That is the existing model closest to "real capability upgrades per level".
- Missions use an **event -> action trigger table** (RA: 32 events, 38 actions) attached to cells, objects, houses, teams or the map, with three persistence modes. Simple and editor-friendly, but only boolean globals and no loops.
- Tech gating is `Level` vs. per-mission build level plus prerequisite bitmask; the campaign AI ignores it.
- The AI is predictable and static by design; fine for scripted missions, weak for skirmish.
- Easy/Normal/Hard multiplier values are not in the repo (unverified).

## 1. Skirmish / computer-player AI

### 1.1 Per-frame entry: `HouseClass::AI` (RA HOUSE.CPP:912-1361)

Once per tick per house, a computer house: (1) turns base building on if `IsBaseBuilding` or `IQ >= Rule.IQProduction` (default 5 = max; skirmish AIs are forced to max IQ, SCENARIO.CPP:2908-2910; campaign AIs normally start via `TACTION_BASE_BUILDING`/`BEGIN_PRODUCTION`); (2) applies win/lose flags; (3) when `IsAlerted && AlertTime == 0` spawns attack teams (1.4); (4) when not alerted and `TeamTime == 0` spawns one idle team, then waits `Rule.TeamDelay` minutes; (5) every `Rule.DamageDelay` minutes damages power-hungry buildings by 1 HP while `Power_Fraction() < 1` (applies to everyone); (6) polls its house triggers; (7) runs `Expert_AI` when `AITimer == 0` (returns its next delay: 5 s + up to 0.5 s, HOUSE.CPP:5115); (8) always runs `AI_Building/Unit/Vessel/Infantry/Aircraft`, which only *choose a type* into `BuildStructure/BuildUnit/...`; factories do the spending (BUILDING.CPP:2151 clears `BuildStructure` after placement; the rest of that path is unverified).

### 1.2 Strategy loop: `Expert_AI` (HOUSE.CPP:4823-5116)

- **Enemy selection** (once it has a base and no pending attack): score every non-ally house `2*(2*MAP_CELL_W - baseDistance) + 5*buildingsTheyKilledOfMine + unitsTheyKilledOfMine + (theirUnits-myUnits) + (theirBuildings-myBuildings) + (theirInfantry-myInfantry)/4`, `+100` for the last attacker; highest wins (HOUSE.CPP:4890-4935). It refuses to pick while any enemy has not started a base.
- **Cap tracking:** raises its own max units/buildings/infantry to enemy average + 10 (HOUSE.CPP:4956-4970), a mild limit cheat.
- **State:** `BUILDUP`, `BROKE` (money < 25), `ATTACKED` (hit within the last minute).
- **Urgency arbitration:** ten strategies report NONE..CRITICAL; the loop runs every strategy at the highest level present and stops after the first level where something acted (HOUSE.CPP:5062-5113). Most checkers are **stubs** returning NONE (`Check_Build_Defense/Offense/Income/Engineer`, and `AI_Build_*` return false). What works: attack (after 1 min, CRITICAL, LOW if recently attacked); fire sale (no factory left); raise money (money < 100) selling in a fixed order, tech buildings first, power/airstrip HIGH, construction yard CRITICAL (HOUSE.CPP:5519-5539); raise/lower power (sell tech/defence when power fraction < 0.75, sell plants when surplus > 300).
- **`AI_Attack`** (HOUSE.CPP:5291-5345): 33% of the time just shuffle; otherwise every armed unit, infantry and aircraft gets `MISSION_HUNT` with 75% chance (100% if it has no buildings). Next attack in `AttackInterval(3) * rand(30 s..2 min)`, roughly 1.5 to 6 minutes. No staging and no target choice beyond each unit's own `Greatest_Threat`.

### 1.3 What to build

**Structures** (`AI_Building`, HOUSE.CPP:5664-6003). Each second a candidate list is built with urgencies and the highest wins. Wanted count = `ceil(ratio * CurBuildings)` capped by a limit (defaults, RULES.CPP:95-150, all INI-tunable):

| Need | Condition | Urgency |
|---|---|---|
| Power | `Power <= Drain + 50` | LOW if no refinery, else MEDIUM |
| Refinery | ratio 0.16, limit 4 | HIGH if none, else MEDIUM |
| Barracks | 0.16, limit 2, money > 300 or income | MEDIUM if none, else LOW |
| War factory | 0.10, limit 2, money > 2000 or income | MEDIUM / LOW |
| Ground defence | 0.5, limit 40 | MEDIUM |
| Anti-air | 0.14, limit 10, only if an enemy has aircraft | HIGH if fewer than enemy aircraft |
| Tesla, tech centre | 0.16 / one, full power | MEDIUM |
| Helipad, airstrip | 0.12, limit 5 | HIGH if own aircraft < enemy's |

"Income" = refinery + harvester exist and ore is not short. The `BaseSizeAdd` cap ("stop when bigger than the human's base") is disabled (`// TCTC`, HOUSE.CPP:5691).

**Placement** (`Find_Build_Location`, HOUSE.CPP:4575-4673). The base is divided into 5 zones (core, N, S, E, W) with tracked `AirDefense/ArmorDefense/InfantryDefense` totals. A defensive building is placed in the zone whose defence is furthest below the base average for the stats that building provides (core deficit halved); combat buildings are never placed adjacent to each other. Economic buildings use the same code with zero defence stats, so fall back to random zone selection. It is a genuinely adaptive piece, but small.

**Units** (`AI_Unit`, `AI_Infantry`). Two modes:
- *Team-driven (campaign)*: count what active/pre-built `TeamTypes` still need (`counter[type]`), subtract what already exists and is recruitable, and build the type with the greatest shortfall (random among ties). `IsPrebuilt` team types force production even without a living team (infantry capped at 5 per type) (HOUSE.CPP:6038-6116, 6283-6366). Only builds if cost <= available money.
- *Base-building mode (skirmish)*: units: each buildable unit gets weight 20 if armed, 1 if not (harvesters excluded); roulette-wheel pick (HOUSE.CPP:6118-6147). Infantry: weights E1=3, E2 (grenadier)=5, E3 (rocket)=2, E4 (flame)=5, Tanya/Renovator 1 if none exist; only considered when the enemy has more of that type, or money > `InfantryReserve (2000)`, or infantry < `2 * CurBuildings` (HOUSE.CPP:6392-6453). Aircraft (IQ >= 4): Longbow, then Hind, then Mig, then Yak, one per helipad/airstrip not yet occupied (HOUSE.CPP:6473-6511).

So the "unit mix ratio" is: armed vehicles uniformly random, infantry weighted toward grenadier/flamer, aircraft one per pad, plus a counter-aircraft bias. No counter-unit logic against ground armies.

### 1.4 Attack teams, defence, harvesting, reaction

- **Teams for attack:** when the house is `IsAlerted` and `AlertTime` hits 0, create `rand(2, (TechLevel-1)/3+1)` teams of random eligible *autocreate* types; reset `AlertTime = AutocreateTime(5) * rand(30 s..2 min)` (HOUSE.CPP:979-997). In TD it is hard-coded by the **player's** difficulty: 4-10 min (Hard), 16-40 min (Easy), 5-20 min otherwise (TD HOUSE.CPP:1015-1023).
- **Which team:** `TeamTypeClass::Suggested_New_Team` (TEAMTYPE.CPP:416-494) simply collects up to 20 team types of this house with `Number < MaxAllowed`, filtering autocreate vs. not by the alert flag, and picks **uniformly at random**. The old priority/type-match scoring is compiled out (`#ifdef OBSOLETE`).
- **Defence:** no dedicated defence AI is live (`AI_Base_Defense` is under `#ifdef NEVER`, HOUSE.CPP:5559). Defence is emergent: static defences built by ratio; units in `MISSION_GUARD_AREA` (IQ >= `IQGuardArea` 4) respond; `Adjust_Threat` maintains a coarse region threat map (centre gets full value, edge neighbours `>>1`, diagonals `>>2`, HOUSE.CPP:2408-2438) used by pathing and by team retreat. Also `QUARRY_THREAT` lets a team go after enemies near its own base.
- **Harvesting:** the harvester's own state machine does the work. The house replaces lost harvesters if `IQ >= IQHarvester (3)` and refineries > harvesters, **except** on `DIFF_HARD` (HOUSE.CPP:6031; reason unverified, as written Hard skips this auto-replacement path). TD: also none on Easy.
- **Reaction to attack:** `HouseClass::Attacked` (HOUSE.CPP:1820) is mainly the human "base under attack" voice + `TEVENT_ATTACKED` house triggers. For the AI, `LATime`/`State == ATTACKED` raises power urgency and downgrades `Check_Attack` urgency for a minute. `IsComputerParanoid` (default true): when a max-IQ AI is defeated, every remaining computer house **allies with all other computers and declares war on all humans** (HOUSE.CPP:3751, 7746-7776), a team-up-against-the-player rule for free-for-all games.

## 2. Difficulty

### 2.1 Stat multipliers (`DifficultyClass`, RA RULES.H:41-58)

Fields: `FirepowerBias, GroundspeedBias, AirspeedBias, ArmorBias, ROFBias, CostBias, BuildSpeedBias` (fixed-point multipliers), `RepairDelay` (default 0.02), `BuildDelay` (0.03), bools `IsBuildSlowdown`, `IsWallDestroyer` (default true), `IsContentScan`. Loaded from INI sections `[Easy]`, `[Normal]`, `[Difficult]` by `Difficulty_Get` (RULES.CPP:311-327), defaults of 1.0. `Assign_Handicap` multiplies the house-type's own biases by the difficulty's biases (HOUSE.CPP:280-309). **Actual Easy/Normal/Hard numbers are absent from the repo (data file RULES.INI is not included). Unverified.** Note that RA's `RulesClass::Difficulty()` call is `#if 0`'d out in this tree (RULES.CPP:1047-1051), so the multipliers may be dormant in this build.

Other difficulty effects:
- **Campaign: difficulty belongs to the player's house**, and AI code reads `PlayerPtr->Difficulty` (TD TECHNO.CPP:307-311: for non-Hard games, AI structure build time is averaged toward 2000 or 4000 so cheap buildings are slower; TD DRIVE.CPP:242: scatter only on Hard). In **skirmish**, each AI gets `Scen.CDifficulty`, minus one step if there are 2+ players and `IsCompEasyBonus` (SCENARIO.CPP:2916-2921).
- **`IsBuildSlowdown`** (TECHNO.CPP:6522-6524; TD TECHNO.CPP:327-329): AI build time is multiplied by `2*MaxIQ/(IQ+MaxIQ)`: x2.0 at IQ 0, x1.0 at IQ 5. A "dumb = slow" dial.
- **`IsContentScan`** (or IQ >= 4): when valuing a transport as a target, the AI also counts the value of its cargo (TECHNO.CPP:4877).
- `IsWallDestroyer`: whether the house targets walls.

### 2.2 IQ: capability gates

`RulesClass::IQ` reads `[IQ]` (RULES.CPP:937-951). RA defaults (constructor, RULES.CPP:140-150; TD differs for two values) and every use site found:

| IQ needed | Key | Unlocks | Site |
|---|---|---|---|
| 2 | `AutoCrush` | Vehicles deliberately run over infantry | UNIT.CPP:5041 |
| 2 | `SellBack` | Damaged-building emergency sell threshold uses tech level vs. this | BUILDING.CPP:5781 |
| 3 | `Harvester` (TD: 2) | Auto-replace lost harvesters | HOUSE.CPP:6031 |
| 3 | `RepairSell` (TD: 1) | Building repair and sell-when-ticked-off | BUILDING.CPP:5763 |
| 3 | `Scatter` | Units step aside when a unit is shot at/blocked | CELL.CPP:1999 |
| 4 | `GuardArea` | Idle defenders react in a radius (below this: plain guard) | UNIT.CPP:1427, INFANTRY.CPP:1834, BUILDING.CPP:2076 |
| 4 | `ContentScan` | Counts cargo in target value | TECHNO.CPP:4877 |
| 4 | `Aircraft` | Builds and uses aircraft | HOUSE.CPP:6477 |
| 4 | `SuperWeapons` | Uses superweapons (human or IQ >= 4) | HOUSE.CPP:1475,1555,1605,1693 |
| 5 | `Production` | Self-starts base building and team creation | HOUSE.CPP:924 |

This is exactly the "capability ladder" the owner wants, already built and data-driven: one integer per house, `if (IQ >= X)` at each behaviour. The mission editor exposes IQ per house (`[HouseName] IQ=`, HOUSE.CPP:7384-7386).

### 2.3 Does the AI cheat?

- **Prerequisites/tech:** yes. `Can_Build` returns true for any computer house in campaign (`if (!IsHuman && Session.Type == GAME_NORMAL) return true`, HOUSE.CPP:802), so campaign AIs can build anything the team lists name, ignoring prerequisites (the base-building path still checks `Level <= TechLevel` for infantry and aircraft). In skirmish they obey prerequisites (using `OldBScan`, a record of what existed).
- **Vision:** `Expert_AI` reads `h->CurUnits`, `AScan`, `BScan` of enemy houses directly, ignoring fog. The air-defence logic reacts to enemy aircraft anywhere on the map; enemy base centre is known from the start.
- **Money:** no income bonus code found; the lever is `CostBias` and `BuildSpeedBias` (data). Cap limits track the enemy (+10).

## 3. Teams

### 3.1 Definition (RA TEAMTYPE.H:41-300; limits `MAX_TEAM_CLASSCOUNT = 5`, `MAX_TEAM_MISSIONS = 20`)

A `TeamTypeClass` is a data record: owner house; up to 5 `(unit type, quantity)` slots; up to 20 `(mission, argument)` steps; flags `IsRoundAbout` (avoid high-threat areas), `IsSuicide` (never retreat; fight to death), `IsAutocreate` (eligible for the alert-driven spawner), `IsPrebuilt` (AI produces members even without a team), `IsReinforcable` (accepts replacements while running), `IsTransient` (temporary reinforcement type, deleted when empty); `RecruitPriority` 0-15, `InitNum`, `MaxAllowed`, `Fear`, `Origin` waypoint, and an optional `Trigger` attached to each member. INI form (TEAMTYPE.CPP:1637-1641): `Name = House,flags,RecruitPriority,InitNum,MaxAllowed,Origin,Trigger,ClassCount,Class:Num,...,MissionCount,Mission:Arg,...`.

### 3.2 Lifecycle (`TeamClass::AI`, TEAM.CPP:467-867)

1. **Create** (`Create_One_Of`) from a trigger action, the autocreate timer or a reinforcement.
2. **Recruit** while not moving: for each missing slot take the *nearest* matching idle unit (`Can_Add`, TEAM.CPP:960-1030): alive, own side, not in radio contact, recruitable mission, not in a team of `RecruitPriority >=` this one (higher priority can poach), aircraft need ammo.
3. **Activate** at full strength (or forced).
4. **Run the script**, advancing on step completion; delete the team when the list ends or all members died after reaching full strength.
5. **Regroup:** a *reinforceable* team at `<= 1/3` strength stops and moves to the friendly non-weapon building minimising `distance * (cellThreat+1)` (repair bay halved) via `Safety_Point` (TEAM.CPP:574-618), refills, then resumes. Non-reinforceable and suicide teams never retreat.

### 3.3 Team mission script (RA `TeamMissionType`, TEAMTYPE.H:41-63)

- `ATTACK` (quarry): leader calls `Greatest_Threat` for ANYTHING, BUILDINGS, HARVESTERS, INFANTRY, VEHICLES, VESSELS, FACTORIES, DEFENSE, THREAT (enemies near own base), POWER or FAKES (TEAM.CPP:2686-2749); all members get `MISSION_ATTACK` on it; target dead => next step.
- `ATT_WAYPT`: attack whatever is on a waypoint cell; `ATTACKTARCOM`: attack the pre-set target.
- `MOVE` (waypoint), `MOVECELL` (cell), `PATROL` (move, engaging on the way), `FORMATION` (formation and matched group speed), `HOUND_DOG` (follow nearest friendly).
- `GUARD` (arg = time in 1/10 min; step ends on timeout), `DO` (hold a general mission; never auto-advances).
- `LOOP` (index): jump, the only control flow. `SET_GLOBAL`: set a scenario flag so teams can talk to triggers. `INVULNERABLE`: scripted set pieces.
- `LOAD`/`UNLOAD` (transport cargo), `DEPLOY` (MCV), `SPY` (spy enters building at waypoint).

TD has a smaller list (TD TEAMTYPE.H:45-60): `ATTACKBASE, ATTACKUNITS, ATTACKCIVILIANS, RAMPAGE, DEFENDBASE, MOVE, MOVECELL, RETREAT, GUARD, LOOP, ATTACKTARCOM, UNLOAD`. Nothing can *wait on an event*. The team only picks a target and orders `MISSION_ATTACK`; each unit's own state machine paths and fires (no focus-fire or kiting).

## 4. Triggers (RA)

### 4.1 Structure

A `TriggerTypeClass` (TRIGTYPE.H:56-146) = persistence + owning house + `Event1`, `Event2`, `EventControl`, `Action1`, `Action2`, `ActionControl`. `MultiStyle` = ONLY / AND / OR / LINKED (event i fires action i). A `TriggerClass` is a runtime instance created on demand; it can be attached to many things. INI: `Trigs` section, `name = persist,house,eventControl,actionControl,event1,team,data,event2,...,action1,team,trigger,data,...`.

### 4.2 Events (`TEventType`, TEVENT.H:43-76; 32 + NONE)

Object/cell: `PLAYER_ENTERED` (cell entered by house X), `SPIED`, `THIEVED`, `DISCOVERED`, `ATTACKED`, `DESTROYED`, `ANY`.
Position: `ENTERS_ZONE` (zone of waypoint), `CROSS_HORIZONTAL`, `CROSS_VERTICAL`.
House state: `HOUSE_DISCOVERED`, `UNITS_DESTROYED`, `BUILDINGS_DESTROYED`, `ALL_DESTROYED`, `CREDITS` (>= n), `NBUILDINGS_DESTROYED`, `NUNITS_DESTROYED`, `NOFACTORIES`, `FAKES_DESTROYED` (body is commented out as broken, TEVENT.CPP:344-349), `LOW_POWER`, `EVAC_CIVILIAN`, `BUILD` / `BUILD_UNIT` / `BUILD_INFANTRY` / `BUILD_AIRCRAFT` (a specific type was just produced), `BUILDING_EXISTS`.
Global: `TIME` (elapsed, in 1/10 min units), `MISSION_TIMER_EXPIRED`, `GLOBAL_SET`, `GLOBAL_CLEAR`, `LEAVES_MAP` (a team of type T exited), `ALL_BRIDGES_DESTROYED`.

### 4.3 Actions (`TActionType`, TACTION.H:39-86; 38 + NONE)

Outcome: `WIN`, `LOSE`, `WINLOSE`*, `ALLOWWIN`. Production/AI: `BEGIN_PRODUCTION`, `BASE_BUILDING`, `AUTOCREATE`, `CREATE_TEAM`, `DESTROY_TEAM`, `ALL_HUNT`, `FIRE_SALE`, `PREFERRED_TARGET`. Reinforce: `REINFORCEMENTS`, `DZ` (smoke marker). Information: `TEXT_TRIGGER`, `PLAY_MOVIE`, `PLAY_SOUND`, `PLAY_MUSIC`, `PLAY_SPEECH`, `REVEAL_ALL`, `REVEAL_SOME`, `REVEAL_ZONE`, `CREEP_SHADOW`. Logic: `FORCE_TRIGGER`, `DESTROY_TRIGGER`, `SET_GLOBAL`, `CLEAR_GLOBAL`. Timer: `START_TIMER`, `STOP_TIMER`, `ADD_TIMER`, `SUB_TIMER`, `SET_TIMER`. Misc: `DESTROY_OBJECT`, `1_SPECIAL`, `FULL_SPECIAL` (grant superweapon one-shot/repeating), `LAUNCH_NUKES` (fake).
(*`WINLOSE` has no case in the action switch, TACTION.CPP:367-767, so it falls to the default no-op; unverified whether it is handled elsewhere.)
TD has a much smaller set (17 actions, 17 events; TD TRIGGER.H:44-104), with hard-wired `AIRSTRIKE`, `NUKE`, `ION` and three generic `DESTROY_XXXX/YYYY/ZZZZ` triggers.

### 4.4 Attachment and evaluation

`Attaches_To(event)` (TEVENT.CPP:677-765) says where an event lives: `CELL` (tested as foot units move), `OBJECT` (`->Trigger` pointer on units/buildings/terrain), `MAP` (zones), `HOUSE` (`HouseTriggers[house]`, polled in `HouseClass::AI`), `GENERAL` (`LogicTriggers`, polled every tick in LOGIC.CPP:209-239 for time, globals, bridges, mission timer), `TEAM`.

`TriggerClass::Spring(event, obj, cell, forced)` (TRIGGER.CPP:224-355) evaluates event 1 (and 2 for AND/OR/LINKED) via `TEventClass::operator()` (TEVENT.CPP:217-467): timers/globals first, then "the call's event type must match" for object/cell kinds, then live house scan bitmasks. `IsTripped` latches one-shot events. If sprung, actions run. **Persistence:** `VOLATILE` deletes the trigger; `SEMIPERSISTANT` counts attached objects and acts only when the last has fired ("destroy all 3 generators"); `PERSISTANT` resets events (timers restart) and can fire again. `TIME` uses a per-trigger timer of `Data * (TICKS_PER_MINUTE/10)`, i.e. 6 s units.

**Win/lose** is not an event: `WIN`/`LOSE` actions call `Flag_To_Win/Lose`, applied in `HouseClass::AI` (HOUSE.CPP:933-952). `ALLOWWIN` is the guard: at load each house's `Blockage` is incremented per such trigger (SCENARIO.CPP:682-689); destroyed triggers decrement it with a 4 s grace (TRIGGER.CPP:172-175). Victory needs `Blockage <= 0`, so "win when all objective triggers have fired" works. Multiplayer bypasses this (`MPlayer_Defeated`).

### 4.5 Worked example: ambush when the player enters a zone

Authoring (real enum names): team type `AMBUSH` (house USSR, 3 E1 + 2 light tanks, missions `[ATT_WAYPT:12, ATTACK:ANYTHING]`, `Origin` waypoint). Trigger `Zone12`: `VOLATILE`, house = player, `Event1 = TEVENT_ENTERS_ZONE`, `Action1 = TACTION_REINFORCEMENTS`(AMBUSH), `Action2 = TACTION_TEXT_TRIGGER`, `ActionControl = AND`.

Runtime: the trigger is `ATTACH_MAP`; `FOOT.CPP:1510-1512` tests it as foot units move. A player unit enters the zone => `Spring(TEVENT_ENTERS_ZONE, unit, cell)`, `operator()` checks `object->Owner() == Data.House` and latches `IsTripped`. `Do_Reinforcements` (REINF.CPP:370-529) creates the group, unlimbos it at the house's map edge (infantry may pop out of a building), sets `MISSION_GUARD`, and the Team AI runs the script (it injects `ATT_WAYPT:Origin` if the list is empty, REINF.CPP:384-389). Volatile, so the trigger deletes itself.

## 5. Campaign tech gating

- Each type has `Level` (-1 = never buildable, HOUSE.CPP:771), `Prerequisite` (building bitmask, TYPE.H:457) and an owner mask.
- `HouseClass::Can_Build` (RA HOUSE.CPP:762-861) requires the owner bit, all prerequisite buildings present (advanced power satisfies power), and `Level <= Control.TechLevel`.
- Per-house `TechLevel=` comes from the scenario INI (default = scenario number, HOUSE.CPP:7375), mapped through `_build_tech[] = {2,2,4,5,7,8,9,10,11,12,13}` (SCENARIO.CPP:88-99). TD: `BuildLevel = Scenario` in campaign, `[Basic] BuildLevel=` for custom maps, 98 = everything (TD INI.CPP:316-340, 757-774).
- Hard-coded exceptions (TD HOUSE.CPP:613-644): GDI bazooka infantry only from mission 7, MSAM from 9, Nod stealth tank in mission 11 needs objective buildings.
- Campaign computer houses bypass all this (2.3); designers control AI tech through which team types exist and through IQ.
- Carry-over (SCENARIO.CPP:670-676) passes units/money; tech is recomputed per mission, so "unlock per mission" is trivial and "unlock by player choice" is impossible.

## 6. Assessment

**Strengths:** small and deterministic (sim RNG only); data-driven (ratios, limits, IQ thresholds, difficulty biases are INI keys; scenarios are editable text); the IQ ladder separates "what the AI can do" from "how hard it hits"; teams, triggers and production communicate through simple shared flags.

**Weaknesses:** predictable (timer-driven attacks, closest-enemy targeting, no scouting, counter-building or army retreat); large parts are stubs (`Check_Build_*`, `AI_Base_Defense`, team scoring); random team choice ignores affordability; static base plans exist only in RA campaign (TD's call is `#if (0)`, TD HOUSE.CPP:6276-6283) and rebuild in list order; the trigger language has 2 events, 2 actions, boolean globals, one integer argument, 6 s timer granularity, so complex logic needs chained `FORCE_TRIGGER`/`SET_GLOBAL` hacks; everything is integer indices, so tooling is mandatory.

## Implications for RTS_2027

- **Adopt the IQ ladder as the primary difficulty axis** ("skill tier"): one integer per AI, `if (tier >= X)` per behaviour. It matches the owner's "real capability upgrades". **Recommendation:** five tiers defined as explicit capability sets, e.g. 1 fixed build order, slow waves, no repair; 2 + harvester replacement, repair, crush, scatter; 3 + guard-area response, enemy-aware unit mix, extra refineries, two-pronged attacks; 4 + aircraft/superweapons, scouting, retreat of damaged armies, harassment of harvesters/power; 5 + multi-front timing attacks and build-order adaptation.
- **Keep stat/economy multipliers as a separate, labelled "handicap" option** (EA's `DifficultyClass`), not the default way to go harder.
- **Decide fog-of-war honesty up front.** EA's AI reads enemy scan masks directly. Routing AI knowledge through the player's visibility rules makes scouting a real capability and is far cheaper to do from the start than to retrofit.
- **Reuse the ratio-of-base-size build rules and the urgency arbitration** (NONE..CRITICAL, stop at first level that acted); finish what EA left stubbed (defence, income, engineers). Split the AI into Economy, Military/Teams and Strategy modules with narrow interfaces so tiers can swap implementations and be tested headless.
- **Teams:** keep recruitment priority, script steps and the regroup-at-1/3-strength rule; drop the 5-slot/20-step limits and index references. **Recommendation:** skirmish AI generates team types at runtime from templates (raid, main push, defence response) instead of picking randomly from authored lists.
- **Mission scripting. Recommendation:** embed Lua with a trigger-style API (`on_enter_zone`, `after`, `when_destroyed`, `reinforce`, `win`, `lose`) so 90% of missions read like C&C triggers and the rest can use real logic. Trade-off: Lua must run only inside the deterministic sim tick, with no wall-clock, seeded RNG, and script state serialised as plain tables, otherwise LAN lockstep and save/load break. If lockstep is mandatory and nobody wants to solve that, a pure data trigger table is the safer choice.
- **Make win/lose a first-class objective list** (primary/secondary), replacing EA's `Blockage` counter trick, but keep the idea that objectives are things that can block victory.
- **Tech unlocks:** keep `level + prerequisites + owner`, but store an explicit per-mission allowed/unlocked list and carry campaign state in the save; avoid hard-coded exceptions like EA's bazooka/MSAM checks. Note EA's campaign AI ignores tech rules, so decide whether ours should obey them.
- **Keep persistence modes** (volatile / semi / persistent) as script helpers (`once`, `all_of(group)`, `repeat`), and budget for a trigger/team debug overlay and waypoint/zone editor; tooling saves more time than the language choice.

## Open questions for the owner

1. **Difficulty exposure:** one slider mapping to tier + fixed handicap; separate skill and handicap controls; or fixed per-mission difficulty in campaign with both controls in skirmish?
2. **AI vision:** always fog-honest; omniscient only at the top tier; or omniscient at low tiers for simplicity?
3. **Scripting audience:** only you, or modders too? Data trigger tables; Lua with a trigger API; or tables that compile to Lua?
4. **Is LAN lockstep a hard requirement?** If yes, scripts/AI must be bit-exact, favouring tables or a restricted VM.
5. **Campaign tech progression:** linear per mission (EA model) or branching/choice-based (captured tech, research picks)?
6. **AI personalities in skirmish:** hand-authored (rusher, turtle, air), generated from weight sets, or one adaptive AI?

## Sources

EA repo `/home/claude/ref/CnC_Remastered_Collection` (RA = REDALERT/, TD = TIBERIANDAWN/):

- RA HOUSE.CPP: 280-309, 741-861, 912-1361, 1820-1848, 2383-2438, 3735-3753, 4575-4673, 4823-5116, 5119-5556, 5559-5646, 5664-6003, 6020-6150, 6276-6511, 7375-7391, 7746-7776.
- RA RULES.H 41-58; RULES.CPP 95-200, 311-327, 724-762, 937-951, 1045-1053. RA BASE.CPP 66-491. BUILDING.CPP 2151, 5761-5786.
- RA TEAM.CPP 467-867, 960-1030, 1177-1300, 1633-1718, 2640-2847. TEAMTYPE.H 41-300; TEAMTYPE.CPP 389-494, 1630-1780.
- RA TEVENT.H/.CPP 43-159, 180-467, 563-606, 677-765. TACTION.H/.CPP 39-131, 247-274, 345-769. TRIGTYPE.H 48-146; TRIGTYPE.CPP 1812-1950. TRIGGER.CPP 125-355. LOGIC.CPP 200-265. FOOT.CPP 1472-1512.
- RA REINF.CPP 370-600. SCENARIO.CPP 88-99, 640-709, 2880-2931. TECHNO.CPP 4867-4886, 6517-6524. UNIT.CPP 1427, 5041. CELL.CPP 1999. DEFINES.H 2483-2497, 3546-3548. TYPE.H 440-466.
- TD HOUSE.CPP 239-264, 553-646, 904-1030, 3570-3600, 5383-5412, 6270-6390, 6625-6640. TD RULES.CPP 40-120. TEAMTYPE.H 45-60. TRIGGER.H 40-110. TECHNO.CPP 296-335. INI.CPP 316-340, 757-774. SCENARIO.CPP 166-183. DRIVE.CPP 242.

Gaps: no `.INI` files (RULES.INI, scenarios) are in the repo, so Easy/Normal/Hard values, per-mission IQ and real trigger/team text are unverified; the worked example is built from enum semantics and parsing code.
