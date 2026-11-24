# AI — skirmish AI and LLM commander glue
Scope read: src/AI/*.cs (all; AiDriver, AiRandom, AiRegistry, AiSnapshot, AiMatchRunner, RecruitAi in full), src/AI/Recruit/{EconomyRescue, EconomyManager, BuildOrderManager (Think/NextType), ProductionManager, RecruitFaction, StartRoute, ScoutManager, DirectiveBias, DirectiveGuard, MilitaryOptions, ExpeditionPlan, parts of MilitaryManager/TierPlanner/RecruitKnowledge}, data/ai/difficulty.toml (all), src/Data/{DifficultyLoader, DifficultyProfile, FactionAiKeys, FactionTuning}, src/Sim/Simulation.NoRebuild.cs, src/Sim/View/PlayerView.cs (fog checks), callers in src/Game (SimHost.StepCore, SkirmishGame/GameLoader/MissionGame LoadProfile), docs/balance/s42_balance.md headings, D-287/D-291/D-297. Nothing was built or run; "script" evidence = grep/python over the repo.

## Findings
### AI-01 · HIGH · bug
What: The income-stall watchdog (`EconomyRescue.Broke`) is true after 2400 ticks (120 s) with no harvester unload, and while it is true the build order is frozen (`HoldExcept = -1`) and queued structures are cancelled. It does not tell "map mined out" from "my refinery is dead" or "my factory is dead". Two soft-locks follow. (a) Refinery destroyed, harvester(s) still alive: the harvester stays in Return forever, so `_lastUnload` never advances, `Broke` latches, the rescue only runs its "Saving for a refinery" branch when `Harvesters.Count == 0`, and the refinery step is never started again (cost 2000 for the Authority per the buildings.toml header). The AI can only sell spare structures for units. (b) Refinery stands, all harvesters dead, war factory dead: `CanBuy` is false so `Saving=false` (D-242), but after 2400 ticks `Broke` is true and `HoldExcept=-1` blocks the factory step, so the harvester and the factory are never rebuilt.
Where: src/AI/Recruit/EconomyRescue.cs:66-77 (Broke latch and cancel/sell branch), :84 (rescue armed only with 0 harvesters), :93-95 (D-242 `Saving=false`); src/AI/RecruitAi.cs:352 (`HoldExcept = Saving ? RefineryType : Broke ? -1 : int.MinValue`); src/AI/Recruit/BuildOrderManager.cs:155 (`next = ... : -1`).
Evidence: read — traced the three lines above. Tests only cover `CanBuy`/`KeepsPower` (tests/AI.Tests/S24/RescueSavingTests.cs), none the Broke + dead-refinery or dead-factory case. The sim does not end it: `HasRebuildLifeline` (Simulation.NoRebuild.cs:67-83) counts a living harvester or any producing building, so the side is never defeated. Not run.
Impact: tier 2+ AI (ReplaceHarvesters on) whose refinery is sniped while it holds under ~2000 credits (likely, it spends down) never recovers its economy. It idles as a live but dead-economy side until a human finishes it, and in AI-vs-AI the game runs to the tick limit as a draw.
Fix idea: in the Broke branch exempt the missing refinery / factory (the lifeline types) from HoldExcept and from the structure cancel; or reset `_lastUnload` when `BuildingCount[refinery]==0`.   Changes a match outcome / number / visible text?: yes (AI recovery behaviour only; no data numbers)

### AI-02 · MED · bug (side effect of a decision)
What: `respond_to_attacks = 0` (D-287 Authority tiers 2-5, D-291 Expedition tiers 4-5) also switches off the mop-up and mopup_hunt, because `MopupOn => Options.Respond && Options.Mopup`. The s41 searching keys that would replace it (`wave_search`, `wave_reinforce_ticks`) are set in no tier. These AIs therefore have no end-of-game sweep: with no known enemy building they only cycle the scout route (starts, centre, corners).
Where: src/AI/Recruit/MilitaryOptions.cs:15 (`p.RespondToAttacks && p.Mopup`); MilitaryManager.cs:244; data/ai/difficulty.toml `[faction.authority.tier2..5]`, `[faction.expedition.tier4..5]`; MilitaryManager.cs:1172-1182 (fallback route).
Evidence: read + script (python over difficulty.toml: wave_search, wave_reinforce_ticks never set). D-287 lists "mop-up" among the flag's effects; D-291 records 68 % draws at Expedition 5 v 4.
Impact: against a human who hides a last building or unit (or a Left Behind rig that drove off), the Authority AI at tiers 2-5 and Expedition AI at tiers 4-5 never finishes the game; no one is told.
Fix idea: give those tables an explicit mop-up path that does not ride on respond_to_attacks (for example `wave_search = 1` for them, which the s41 code already has), then re-run the ladder once. Needs owner OK because numbers are frozen.   Changes a match outcome / number / visible text?: yes

### AI-03 · LOW · data
What: `focus_fire = 1` (tiers 3, 4, 5) does nothing for the Authority, Directorate and Left Behind. The Recruit AI reads `FocusFire` only in ExpeditionTactics.cs:775; the other reader is the M1 SkirmisherAi, which the game does not pick when "recruit" is registered. The VET-6 focus on rank-3 enemies (MilitaryManager.cs:871-873) runs at every tier, tier 1 included, with no flag.
Where: data/ai/difficulty.toml tiers veteran/elite/commissar `focus_fire`; src/AI/Recruit/MilitaryManager.cs:871; src/Game/Sandbox/SkirmishGame.cs:149 (`ChooseAiId`).
Evidence: script — `grep -rn FocusFire src` gives SkirmisherAi.cs and ExpeditionTactics.cs only (outside src/Data). The toml header describes the key as "(skirmisher AI)", so this is documented, but the setup screen lists tiers as differing.
Impact: one of the visible tier differences is inert for 3 of 4 factions. No player-visible text claims it.
Fix idea: document it as skirmisher/Expedition only, or gate the VET-6 loop on the flag.   Changes a match outcome / number / visible text?: no for the doc fix, yes if gated

### AI-04 · LOW · debt
What: Tier behaviour is also keyed on the tier number in code, against the toml header ("every capability is gated on a flag, never on the tier number"): `MarginForTier(bp.Tier)` (supply margins 4,5,8,10,12), `HoldsDirectorateTech(bp.Tier)` / `DirectorateTechArmyFor`, Left Behind `Tier >= 3/4` (relocate, salvagers, party caps), `ExpeditionPlan.Tiers[]`. In `ExpeditionPlan` tiers 2 and 3 are identical rows, so Expedition tiers 2 and 3 differ only by the shared toml flags. The tuning cannot be changed from data/ai.
Where: src/AI/RecruitAi.cs:275-282; src/AI/Recruit/ExpeditionPlan.cs:20-26; src/AI/Recruit/LeftBehindTactics.cs:195,208,461.
Evidence: read + script (grep for `Tier >=`/`bp.Tier`).
Impact: tuning and mods cannot reach these numbers; anyone reading the toml header is misled. Overlaps D-310's frozen balance, so debt only.
Fix idea: update the toml header; list the code-held tables.   Changes a match outcome / number / visible text?: no

### AI-05 · LOW · data
What: Many shipped keys are inert because no tier or faction sets them: retreat_ratio_pct, unit_reserve, grow_bags, wave_reinforce_ticks, wave_search, army_before_tech, army_before_adapt, adapt_cash, adapt_max_production, tier2_gate_tick, bore_army/gap/dig_short/sites/min_cash, adapt_rich_max, infantry_bag, vehicle_bag, infantry_tier2, vehicle_tier2, vehicle_tier3, siege_commit_ratio, aa_needs_air, retreat_floor_pct, mopup_hunt. All of them are read by code (no dead loader keys) and the defaults are documented, so this is not a bug. Two matter for users: `retreat_health_pct = 0` in all five tiers, so the "units below N percent leave the fight" rule in the header is off everywhere (the only floor is Expedition's built-in 40); and `[faction.left_behind] harass_min_army = 30` has no effect, since HarassManager is skipped for Left Behind (RecruitAi.cs:366), so tier 4-5 `harass` does nothing for it.
Where: data/ai/difficulty.toml; src/AI/RecruitAi.cs:366.
Evidence: script — python listing keys never set; grep for readers.
Impact: none in play; a contributor tuning one of these will see no change.
Fix idea: one comment line per inert key group.   Changes a match outcome / number / visible text?: no

### AI-06 · LOW · ux
What: A malformed or edited `data/ai/difficulty.toml` is swallowed silently in three places (`LoadProfile` in SkirmishGame, GameLoader, MissionGame returns `DifficultyProfile.Default` on RulesException; the setup screen falls back to "Tier N" names). Every tier then plays the same neutral regular profile, no capabilities, no per-faction tables.
Where: src/Game/Sandbox/SkirmishGame.cs:~306-318, src/Game/Saves/GameLoader.cs:~160-175, src/Game/Sandbox/MissionGame.cs:~133-146, src/Game/Screens/SkirmishSetupScreen.cs:408-426.
Evidence: read. The loader is strict (unknown key, range or type error means a RulesException), so one typo in any per-faction table triggers it.
Impact: a modded or damaged install shows five identical difficulties with no message. Shipped data loads (tests/AI.Tests/DifficultyLoaderTests.cs exists).
Fix idea: log or show the RulesException text once.   Changes a match outcome / number / visible text?: no

### AI-07 · LOW · bug
What: AI exceptions are fatal to the host loop: `SimHost.StepCore` calls `ai.Collect` with no try/catch, and there are two data-triggered throw sites inside Think: `ProductionManager.Resolve` ("unit mix names unknown type") for `infantry_bag`/`vehicle_bag`/`*_tier2/3` ids that the loader only checks as non-empty strings, and `BuildOrderManager.Think` ("build order names unknown structure") for `build_repertoire` ids on the shipped Recruit tier or edited data. `AiDriver.Start` also narrows its LoadState catch to EndOfStream/InvalidData/InvalidOperation/IO, so another exception type from a layout-drifted (but checksum-valid) blob would abort the load instead of setting `Restored = false`.
Where: src/AI/Recruit/ProductionManager.cs:322-329; BuildOrderManager.cs:114-117; src/Game/Host/SimHost.cs:306; src/AI/AiDriver.cs:145-150.
Evidence: read.
Impact: only edited or moded data, or a future manager layout change; a bad unit id crashes the match on the first AI decision, not at load.
Fix idea: validate ids against the rules at load or first Think and fall back to the built-in list; widen the LoadState catch.   Changes a match outcome / number / visible text?: no

## Suspects (unproven)
- HashSet<ulong> iteration in RaidManager.cs:52 (`_group`) is the only order-dependent set walk I found; after a save/load the set is rebuilt sorted, so the loaded AI may issue those commands in a different order than an uninterrupted one. No desync (the AI runs on the host only and its commands are recorded), but the "save equals continue" property would not hold. Proof: run a save/restore mid-raid and diff the command stream.
- `EconomyRescue._harvMission` and `_lastUnload`: a harvester that is idle on the way to a refinery that was just rebuilt may take one decision to restore `Broke=false`; harmless, not traced.
- The AI knows every enemy start position from tick 0 (`AiContext.StartPositions`, M4-12 "public map data"), so on 2-start maps it heads straight for the enemy base. Documented, so not a bug; the owner may want it stated in the tier description.
- Hidden-info: Ghost building views use `Health` captured at last sight; the AI can keep targeting a ghost of a building destroyed out of sight until the cell is seen again. Mop-up handles it for tiers with respond_to_attacks, AI-02 tiers do not. Not traced.

## Checked and fine
- Loader vs. data: every key in difficulty.toml is a known loader key; every profile field is read in src/AI (grep counts non-zero); overrides applied via `ForFaction` consistently in `Init` and `ApplyHooks`; multi_front needs two_pronged (set together at tier 5).
- AiRandom: splitmix64, seeded from (game seed, slot) through separate finalisers, state saved and restored; `Next` rejects <= 0 and all call sites use positive constants or `Count`-derived bounds; no System.Random, DateTime, float/double, Stopwatch or Environment in src/AI.
- Dictionary<string,...> / HashSet<string> iteration: none (AiRegistry uses SortedDictionary).
- Cheating: AI sees only PlayerView (OccupantsOf is fog-honest, Visible is vision-limited, enemy buildings come from remembered ghosts). No cash/stat mutation anywhere in src/AI. Income edges only through the explicit slot Handicap (AI slots only; `rich` growth gated on handicap > 100).
- Stuck-state handling that works: no harvester + no money (rescue sells and cancels, tier 2+); lost refinery with no harvester (rescue buys the refinery); lost yard (RecruitKnowledge falls back to any non-wall building as the anchor, BuildOrder returns safely); no producer and no harvester (sim NoRebuildDefeat after 120 s); Tier 1 never replaces its harvester by design (documented in RecruitAi header).
- AiDriver: all slots think on the same tick, commands stamped tick+delay, per-slot ushort Sequence wraps unchecked, hooks read once per tick; AiSnapshot envelope has magic, version, length and checksum, counts capped at 1<<20.
- Tier differences: tiers 1-5 differ in interval (60/40/30/20/10), command delay, thresholds, ratios and flags; the per-faction ladders are tuned and recorded in D-287/D-291/D-297/D-304/s42.
