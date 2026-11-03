# RTS AI: Skirmish Opponents, Difficulty Design, and Campaign Scripting

**Summary**

- Shipped RTS AIs are mostly *manager-based* systems: separate modules for economy, production, attack, defense and scouting, each running on a timer and writing into a shared queue or plan. Petra (0 A.D.) and OpenRA's Lua missions are the best public examples we could read. Behaviour trees, state machines and planners sit underneath, usually at unit level.
- The difficulty levels that players respect are *capability* limits: slower decisions, less information (scouting), smaller build-order repertoire, weaker micro, and worse target choice. Blunt economy bonuses ("cheats") are the cheap tool and the one players complain about most.
- Petra's difficulty mostly changes economy targets, defensive building and trait randomisation. Warzone 2100's skirmish levels change per-AI oil output and whether the AI cheats at all. SC2's ladder names the cheat tiers explicitly (Vision, Resources, Insane). We could not verify AoE2 or Spring/BAR details from primary sources.
- Machine learning is not practical for us at runtime. Offline tuning of a few weights with self-play is practical. LLMs are useful at development time for drafting scripts and mission text, but not as the in-game opponent.
- Campaign logic is best done as an embedded, sandboxed Lua API plus declarative map data. OpenRA's `mods/cnc/maps/*.lua` shows the shape, and it is close to what we need.
- Headless deterministic simulation plus AI-vs-AI win-rate matrices is the standard way to test and balance difficulty levels.

## 1. Skirmish AI architectures

Architecture families, in plain terms:

| Family | How it decides | Typical use |
|---|---|---|
| Scripted build order + triggers | Fixed sequence of steps; triggers fire on conditions (time, unit count, enemy seen) | Early AI in SC1/SC2, AoE2 `.per` scripts, campaign waves |
| Manager / modular | Independent modules (economy, production, attack, defense, scout) each run periodically and write into shared plans | Petra, BWAPI bots (UAlbertaBot), OpenRA bot modules |
| Finite state machine (FSM) | Each agent is in one state; transitions on conditions | Unit idle/attack/retreat logic, small squad behaviour |
| Behaviour tree (BT) | Tree of selectors and sequences re-evaluated each tick | Unit and squad tactics, widely used in shipped games |
| Utility AI | Score each candidate action by weighted considerations; pick the best | Target choice, when to attack, which building next |
| GOAP / HTN planners | Search over actions with preconditions and effects (GOAP) or task decomposition (HTN) | Tactical planning; F.E.A.R.'s soldiers |

Shipped and public examples:

- **0 A.D. Petra** (JavaScript). Petra is a fork of the Aegis bot and has been the default AI since Alpha 17. Its `PetraBot` inherits from `BaseAI`, and its update runs every 8 turns, staggered per player. The Headquarter steers modules. A **QueueManager** is the only code that actually trains or builds: other managers add plans to priority-weighted queues. Other managers are **AttackManager** (picks rush, standard, or large-army attack types and creates attack plans), **BaseManager** (one per Civic Center, handles workers, dropsites, rebuilding), **DefenseManager** (groups nearby enemies into armies and garrisons), **ResearchManager**, **TradeManager**, **NavalManager** and **GarrisonManager**. Attack plans (`attackPlan.js`) train their own units, pick a target player and primary enemy entity, and disband when the army dies or no target remains. Source: `docs.wildfiregames.com/javascript/petra/config.js.html` and the PetraBot wiki (both linked below).
- **OpenRA**. The C&C-style engine has a built-in AI (`HackyAI`, C#) that we could not read; its manager structure is therefore **unverified** here. OpenRA's campaign missions are Lua, covered in section 5.
- **UAlbertaBot** (C++, BWAPI, StarCraft: Brood War). Its wiki describes configurable subsystems for build orders, unit production, combat, worker allocation and pathing. It uses **SparCraft** for combat simulation and **BOSS** for build-order search. It won the 2013 AIIDE competition. The bot is a good reference for how a bot can combine search with managers.
- **StarCraft: Brood War scripts.** Original StarCraft ships scripts as a fixed file of AI routines. The melee script picks one of several preset strategies from the map and the opponent's race. The "Insane" script ignores the starting setup and always runs its scripted rushes. The campaign script is passive: it rebuilds, does not expand, and attacks with timed scripted forces. (The binary format and command list are **unverified**.)
- **F.E.A.R.** (GDC "Three States and a Plan", Orkin). The overview describes an FSM with three states plus an A*-based planner that replans in real time. The example given: if the player slams a door, the soldier re-plans to fire through a window or flank. Full talk is members-only, so detailed planner internals are **unverified** here.
- **Spring / Beyond All Reason, Supreme Commander (Sorian), Company of Heroes, AoE4, SC2 built-in AI.** We could not read primary sources for these; treat their architectures as **unverified**. SC2's built-in AI description says it has "a much wider variety of build orders" at higher levels and that it "does not Micro very good", which is useful for the difficulty section.

**Practical view.** Almost every shipped bot uses managers on the outside and small FSMs or behaviour trees for squads and units. Utility scoring is the common way to choose between options. Planners appear in research bots and a few shipped games, but are rare in commercial RTS AI.

## 2. The sub-problems

| Sub-problem | What shipped bots do | Notes |
|---|---|---|
| Build order | Preset orders with branches (Petra's attack types; SC2's named variants such as "Timing Attack", "Economic Focus", "Full Rush") | Build orders are data. Search (BOSS) helps bots, but a table of 4–8 orders is enough for skirmish |
| Economy | Worker targets per base; Petra caps workers by population (`min(120, maxPop/3)` at Medium and above) | Simple targets beat clever optimisation for players' perception |
| Base layout / placement | Grid search for free footprints near resources, with a spacing rule | Keep it deterministic; reuse the engine's placement query |
| Army composition | Counter tables (unit A beats B) plus a mix target per attack type | A weighted composition table is easy to tune and to explain |
| When to attack | Compare army value (cost-weighted HP x DPS) of ours vs. known enemy under fog; attack when ratio exceeds a threshold | Petra's exact scoring is not described on the page we read (unverified). UAlbertaBot uses SparCraft simulation for this |
| Target selection / micro | Priority list (threats first, then cheap high-value); focus fire; kiting only at higher levels | Micro is the largest source of "skill" difference between human and AI; keep it a difficulty dial |
| Scouting / fog | Scout cadence and information age; the AI acts on last-known positions | Core to "legit" difficulty (section 3) |
| Defense and retreat | Garrison or retreat when the local army value is below a threshold | Defense managers often run every few turns, not every tick |
| Expansion | Expansion trigger on saturation or on a timer, gated by safety | Tie to the economy target |
| Superweapons / abilities | Trigger on charge ready and target value above threshold | Simple rules are enough |
| Multi-front coordination | Attack plans per target; defense manager reacts to threats in any base | Often weak in shipped AIs; a visible weakness that makes "Hard" feel fair if it is absent |

## 3. Difficulty design

### 3.1 Shipped ladders

- **Petra (0 A.D.)**. Levels are numbered 0 to 5: Sandbox (0), Very Easy (1), Easy (2), Medium (3, default), Hard (4), Very Hard (5). The config changes `popScaling` (0.5 at Sandbox/Very Easy, 0.7 at Easy), `supportRatio`, sentry tower counts (1 at Medium, 2 at Hard and above), worker targets, and trader count (`2 + difficulty`). Personality traits are randomised above Sandbox. Sandbox and Very Easy set the city phase to never happen. The page we read does not mention cheats; Petra's difficulty is therefore a *capability and economy* ladder, not a cheat ladder.
- **Warzone 2100**. Skirmish AIs choose their own difficulty behaviour. Nexus "cheats more at higher levels, with extra power boosts and random research gains" and does not cheat on Easy. Semperfi ignores the setting. NullBot uses non-specialised research on Easy and adaptive research at harder levels. The Easy AI gets 25% less oil production and the Insane AI double oil. This is a clean example of economy handicaps expressed as a single, explainable number.
- **SC2 built-in AI**. Ladder order: Very Easy, Easy, Medium, Hard, Harder, Very Hard, Elite, then Cheater 1 (Vision), Cheater 2 (Resources), Cheater 3 (Insane). Lower levels get "just a few basic builds." Higher levels get more build variants. Cheater 1 gives the AI vision of your units. The StarCraft fandom wiki describes the resource cheats as extra mining (it states "2 additional minerals and gas per trip"; unverified against Blizzard's own text). The Cheater levels are the only SC2 levels that are cheats; the rest are capability-only.
- **Green Tea AI (community SC2 mod)**. Per the wiki: Very Easy does not attack for about 10 minutes; Easy has no mineral cheat; Hard has a small mineral cheat; Harder has a 20% income boost over time; Very Hard has 30%. It shows how a community mod layers capability and economy cheats.
- **AoE2**. Community forum sources say the Hard level was the strongest non-cheating level in HD, that higher levels add resource bonuses, and that DE's top level plays without cheating. These are community accounts only and are **unverified**; we could not read the official wiki.

### 3.2 What players perceive as fair

Public threads and wiki pages repeat a few themes: players accept AI that is *slower to react* or *doesn't scout well*; they resent AI that *knows where your units are* or *gets free money*. The AoE2 thread's users say cheating "makes the AI less interesting to fight." Treat that as a design signal, not proof.

### 3.3 Proposed capability axes (design proposal, not sourced)

Every level is a set of numbers, so tuning is visible and testable:

- **Decision interval**: how often each manager runs (in sim ticks).
- **Information**: scout cadence, maximum age of enemy knowledge, whether fog is respected.
- **Build repertoire**: number of build orders available (1 at easy, 4+ at hard).
- **Micro flag**: none, focus-fire only, kiting/retreat on.
- **Target quality**: random target vs. priority list.
- **Attack threshold**: ratio of own to known enemy army value before attacking.
- **Expansion timing**: saturation-triggered vs. timer-triggered.
- **Economy multiplier**: 1.0 for legit levels; 1.2–1.5 only on explicitly labelled "cheat" levels.
- **Personality weights**: aggressive, defensive, cooperative (Petra's three traits are a good model).

### 3.4 Adaptive and personality axes

Petra's trait system (aggressive, cooperative, defensive, each 0–1, randomised, with named behaviour bands) is the cleanest public example of a *second axis* beside difficulty. Rusher, turtle and boomer are then just trait combinations. Dynamic difficulty (adjusting levels mid-game from the player's army value) is possible but risks feeling unfair; if used, make it visible and optional.

## 4. Machine learning

- **AlphaStar** (DeepMind, 2019). Per the DeepMind blog: an imitation-learning policy from human replays first beat more than 84% of active players; a League of main and exploiter agents then trained by self-play, with distillation to keep human strategies. Each race was one neural network. It was limited to a camera view and capped at 22 agent actions per 5 seconds. It reached Grandmaster in all three races, above 99.8% of active players. The blog gives no compute figures. For a hobby project this is not practical in any form.
- **LLM-driven agents.** The MASMP paper (arXiv 2510.18395) uses an LLM to follow state-machine and behaviour-tree prompts, with a small strategic memory. Reported results: 100% win rate against built-in AI levels 1–5, 80% at level 6, 60% at level 7 (baseline 0% at 6 and 7). It was tested on one map, one LLM, with "Easy Build/Control Mode" enabled, which gives rule-based automation. Latency and cost per decision make it unsuitable as the in-game opponent. The method is more interesting as a *design tool* for writing state machines.
- **Practical options for us, in order of value:**
  1. Offline parameter tuning. Put the section 3.3 numbers in a config and tune them with self-play (grid or CMA-ES). Cheap, deterministic, explainable.
  2. Replay imitation for build orders. Record strong human games (or self-play) and mine common openings. Modest effort, improves variety.
  3. LLM at development time. Draft build-order scripts, mission dialogue or test scenarios; every output must pass the simulator and tests before shipping.
  4. Runtime learning (RL, neural policies). Not recommended for this project.

## 5. Campaign scripting

### 5.1 Lua in OpenRA (verified from source and docs)

OpenRA maps enable Lua with a `LuaScript` trait on the world actor in the rules, listing `.lua` scripts. Scripts run on **Lua 5.1** in a sandbox that blocks OS and file access and caps memory and CPU. The API is global tables called as `Table.Function()`:

- `Actor`: `Create(type, addToWorld, initTable)`, `Cost`, `BuildTime`.
- `Map`: `ActorsInBox`, `ActorsInCircle(WPos, WDist, filter)`, `ActorsWithTag`, `NamedActor`, `RandomCell`, `LobbyOption`.
- `Player`: `GetPlayer`, `GetPlayers`; players carry `Handicap` and objective methods.
- `Trigger`: `AfterDelay(ticks, fn)`, `OnKilled(actor, fn)`, `OnAllKilled(team, fn)`, `OnIdle`, `OnDamaged`, `OnEnteredProximityTrigger`, `OnObjectiveCompleted/Failed`, `OnPlayerWon/Lost`, `OnRemovedFromWorld`.
- `Reinforcements`: `Reinforce(owner, types, entryPath, interval, fn)` and `ReinforceWithTransport`.
- `Media`: `DisplayMessage`, `PlaySound`, `PlayMusic`, `PlaySpeechNotification`.
- `Utils`, `DateTime` (`Seconds`, `Minutes`), `Camera.Position`.

Actor and player commands such as `Move`, `Hunt`, `AttackMove`, `Kill` are **queued activities** and run in order.

Mission file `mods/cnc/maps/gdi04a/gdi04a.lua` (verified structure): `WorldLoaded()` runs once and sets up objectives, starts build loops and schedules helicopter waves with `Trigger.AfterDelay`. `Tick()` runs every tick (here it sets Nod's cash to 1000 each tick). Kill counting uses `Trigger.OnKilled`. The mission sends GDI reinforcements after 12 kills (`KillsUntilReinforcements = 12`). Build loops re-arm themselves through `Trigger.OnAllKilled`. The script has **no difficulty checks**: wave sizes, delays and cash are fixed constants. The OpenRA "tutorial" mission (documented on docs.openra.net) shows the same pattern: `OnAllKilled` on two infantry, `AfterDelay` of 5 seconds, `ReinforceWithTransport` to send a helicopter, `OnKilled(vip, MissionFailed)` to fail the mission.

### 5.2 Other systems (from memory, not re-verified)

- **StarCraft (Brood War) trigger editor / StarEdit**: conditions and actions in a GUI, plus AI script commands. The campaign AI script is separate.
- **Warcraft III** GUI triggers and JASS scripts; **C&C** map triggers in the EA source (we did not study them here; see the other code-study notes).
- **AoE2** scenario triggers are GUI-based; AI `.per` scripts are a separate, rule-based language.

### 5.3 How mission designers express the usual things

- **Objectives**: add primary and secondary objectives, mark them completed or failed, and display text; the win condition is "all primary complete."
- **Reinforcements**: a function that calls the reinforce API, guarded by a counter or an event, and re-arms itself.
- **Timers**: `AfterDelay` in ticks (or seconds via a helper).
- **Cutscenes**: camera moves plus scripted messages, with input locked by a flag (OpenRA uses `Camera.Position` and media calls; a full cutscene system is not in the sources we read).
- **Tech unlocks**: flags set in the script that gate production buildings or unit types in the next mission.
- **Difficulty branches**: read the lobby or campaign difficulty value and pick wave sizes or timers; OpenRA's API exposes `LobbyOption` for this, but the sample mission does not use it.

## Implications for RTS_2027

- **Recommendation: manager-based skirmish AI.** Build a small set of managers (economy, production via one queue manager, attack planning, defense, scouting) that each run every N ticks and write into queues or plans. This mirrors Petra and keeps each piece testable. Trade-off: more moving parts than one big script, but it is the pattern the public code actually uses.
- **Recommendation: FSMs or behaviour trees at unit and squad level, utility scores for the big choices.** Use FSMs first; move to behaviour trees if squad logic grows. Skip GOAP for v1; the F.E.A.R. benefit is real but the implementation cost is high for a hobby project.
- **Recommendation: difficulty as a numeric capability profile (section 3.3), not per-level code.** One struct per level, read by the managers. Make cheats a separate, labelled "cheat" flag so that legit levels never silently cheat.
- **Recommendation: give the AI fog-respecting information on all legit levels.** Its world model holds last-known positions and a scout age. Only the top labelled cheat level sees everything. This is the single biggest source of "fair" feel.
- **Recommendation: a difficulty ladder of six levels, matching Petra's count.** Sandbox (no attacks, for learning); Very Easy (slow decisions, one build, no micro, scouts rarely); Easy (one build, slow expansion, focus fire only); Medium (4 builds, normal decisions, retreats); Hard (full repertoire, priority targets, kiting, fast scouting); Very Hard (tight decision interval, multi-front attacks, strong personality use). Add one labelled cheat level only if playtesting asks for it. Exact values are for playtesting.
- **Recommendation: Lua for campaign logic with a small API, plus declarative map data.** Expose about 30 functions in the style of OpenRA's tables (`Actor`, `Trigger`, `Objectives`, `Reinforce`, `Media`, `Timer`). Keep map placement and unit lists in data files. Sandboxing matters only if we load community missions; if not, a plain embedded Lua with a whitelist is enough.
- **Trade-off on scripting.** A pure data-trigger system (like the SC1 editor) is easier for designers but hard to make expressive. Pure Lua is flexible but makes designers depend on programmers. The hybrid is the common answer: data for the 80% case, Lua hooks for the rest.
- **Recommendation: deterministic headless simulation as a prerequisite.** The AI, the campaign scripts and the testing harness all depend on a sim that runs without rendering and with a fixed random seed. Build this before the AI.
- **Recommendation: machine learning limited to offline tuning and dev-time content.** No RL, no runtime LLM. Tune the section 3.3 numbers by self-play; use an LLM only to draft content that the simulator then checks.

## Open questions for the owner

1. **Cheats at all?** Options: (a) no cheats; the top level is capability-only and beatable by skill; (b) one labelled cheat level with resource bonuses; (c) cheat levels with map vision. Option (a) is the most defensible and the hardest to build well.
2. **How hard should Very Hard be?** Options: (a) beatable by a competent casual player; (b) beatable only with good micro and scouting; (c) meant to be an expert challenge. This sets the tuning target for the self-play harness.
3. **Personality variety in v1?** Options: (a) none, one fixed style; (b) three traits (rusher / turtle / boomer) chosen per level; (c) randomised traits per game, as Petra does.
4. **Campaign scripting audience.** Will the owner write missions alone, or will others (community) write them? Options: (a) owner only, Lua is fine; (b) community missions, so sandbox and a stable API are required; (c) designers who do not program, so a data-trigger editor comes first.
5. **Dynamic difficulty?** Options: (a) none; (b) optional, shown to the player; (c) silent. Silent adjustment is the one most likely to read as unfair.

## Sources

Verified by fetching (primary or documentation pages):

- [0 A.D. Petra config (docs.wildfiregames.com)](https://docs.wildfiregames.com/javascript/petra/config.js.html)
- [PetraBot wiki, version 12 (trac.wildfiregames.com)](https://trac.wildfiregames.com/wiki/PetraBot?version=12)
- [OpenRA GDI mission 4A Lua script, commit 18b8eb3 (github.com/OpenRA)](https://github.com/OpenRA/OpenRA/blob/18b8eb30a3ee2ff74fa0fcda3a4518d5c9f8bd6a/mods/cnc/maps/gdi04a/gdi04a.lua)
- [OpenRA Lua API documentation, release (docs.openra.net)](https://docs.openra.net/en/release/lua/)
- [OpenRA Lua tutorial / triggers (github.com/OpenRA wiki)](https://github.com/OpenRA/OpenRA/wiki/Triggers)
- [Warzone 2100 Difficulty Levels (warzone.atlassian.net)](https://warzone.atlassian.net/wiki/spaces/mod/pages/7636789/Difficulty+Levels)
- [Liquipedia: StarCraft II Artificial Intelligence](https://liquipedia.net/starcraft2/Artificial_Intelligence)
- [StarCraft Wiki: AI script](https://starcraft.fandom.com/wiki/AI_script)
- [DeepMind: AlphaStar Grandmaster-level in StarCraft II](https://deepmind.google/blog/alphastar-grandmaster-level-in-starcraft-ii-using-multi-agent-reinforcement-learning/)
- [MASMP: Memory-Augmented State Machine Prompting for RTS LLM agents (arXiv 2510.18395)](https://arxiv.org/pdf/2510.18395)
- [UAlbertaBot wiki (github.com/davechurchill/ualbertabot)](https://github.com/davechurchill/ualbertabot/wiki)
- [GDC Vault: Three States and a Plan (F.E.A.R. overview)](https://gdcvault.com/play/1013282/Three-States-and-a-Plan)
- [AoE2 DE community thread: AI difficulty levels (forums.ageofempires.com)](https://forums.ageofempires.com/t/ai-difficulty-levels/107341) (community opinion, not official)

Found in search but not read (not relied on for facts):

- [Churchill and Buro, Incorporating Search Algorithms into RTS Game Agents (AIIDE)](https://ojs.aaai.org/index.php/AIIDE/article/view/12548) (fetch rate-limited)
- [Spring RTS wiki: AI](https://springrts.com/wiki/AI) and [AI:Skirmish](https://springrts.com/wiki/AI:Skirmish) (access blocked)
- [Spring engine SAIInterfaceCallback.h](https://github.com/spring/spring/blob/develop/rts/ExternalAI/Interface/SAIInterfaceCallback.h) (not fetched)
- [Ontanon et al., survey of RTS AI (robots disallowed; not read)](https://www.cs.mun.ca/~dchurchill/pdf/ontanon2013survey.pdf)

Gaps: OpenRA's `HackyAI.cs` and any C&C mission beyond `gdi04a.lua` were not read; Spring/BAR, Supreme Commander and AoE2 official difficulty details are unverified; the SC2 cheat resource figures come from one fan wiki; the StarCraft 1 script file format is unverified.
