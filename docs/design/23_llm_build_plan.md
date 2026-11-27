# Design note · An LLM-chosen build plan (A-09)
Status: design only, nothing built, nothing decided. Backlog A-09; follows D-286 ("an LLM-chosen build plan, new design round, later") and D-285
(directives do not add strength). Written in session 47.

## Why
Session 37 showed that the commander's levers (posture, focus, mix, orders: design 15) only steer decisions the tiered AI already makes. D-286
answered with an honest +50 % edge. The open question is whether the model can add strength where the AI is weakest in judgement rather than in
execution: the opening and the tech path. A build plan is that choice made once, early, and checked rarely, so it also suits a slow, costly model
better than the 30-second directive loop.

## What a build plan is
An ordered list of intent for the first 8-12 minutes, chosen by the model from a closed menu, never free text:
1. **Structure order**: which structure types come in which order (power, refinery, barracks, factory, research, tier gates, defences, expansion),
   today the fixed `build_repertoire` per tier (data/ai/difficulty.toml) plus what `TierPlanner` appends.
2. **Unit composition target**: shares of infantry / vehicles / air / counters at stages (opening, mid, late), today `HeavyShare`,
   `InfantryShare` and the bag in `ProductionManager`.
3. **Timings**: when to expand, when to take each tier, when to first attack (today `expansion_tick`, the tier gates, wave thresholds).
Not part of a plan: anything per tick (placement, micro, targeting), and any number the sim reads. Like a directive, a plan is an AI input only
(D-110) and gives no resource or speed advantage (D-061).

## Options
**A. Named openers (menu).** The data holds 4-8 hand-written openers per faction ("fast expand", "two-factory rush", "tech to air", "turtle
economy", "early superweapon"); the model picks one by id and one of three timing speeds. Pros: every plan is playable and tested for all four
factions; the reply is tiny, easy to parse and validate; balance screens (the lead's cluster runs) cover the whole space; replay is one number.
Cons: strength is capped by the author's openers; the model only chooses, it does not invent.
**B. Parameterised plan (slots).** A fixed template with typed slots: structure order as a list of ids from the faction's buildable set, three
composition shares (permille, sum 1000), three timings (minutes, clamped). The parser validates ids against the rules and repairs or drops bad
entries. Pros: the model can express new openers; one schema for all factions. Cons: most random plans are bad (a random plan is the control
and may well beat the model, as in D-285); a validity layer is needed (prerequisites, power); harder to screen.
**C. Free-form plan text, translated by code.** The model writes prose; a second pass maps it to B. Pros: nicest chat. Cons: a second failure
point and cost, no guarantee of meaning; not recommended.
**D. Re-plan during the game.** Any of A-C, but the model may swap the plan at minute 4, 8 and after a lost fight. Pros: reacts to scouting.
Cons: every swap needs a transition rule (what to do with half-built structures); more calls. Treat as phase 2, not the first step.

## Recommendation
Build **A first, designed so B can follow**: 1. openers as data (`data/ai/openers.toml`, owner of the numbers: the lead), each a named
`build_repertoire` plus composition shares and timing multipliers; 2. the model picks `opener` + `tempo` (slow / normal / fast) in its first reply
(minute 0, from the start summary: faction, map size, start positions, enemy factions); 3. one re-pick allowed at the first lost fight or at
minute 6 (phase 2 of option D with a fixed rule: the unbuilt tail of the repertoire is replaced, built steps stay).
Measure it the way D-285 did: the same 32-game swap test against a **random-opener control** (a seeded random pick of the same menu), and
against plain tier 5. Only a model that beats its own random control has found something; the owner's super-hard opponent can keep the +50 %
edge either way. Cost: one extra call per game at minute 0 (plus one re-pick), against one call per 30 s of game time today (D-103).

## The minimal hook it needs
1. **Data**: `openers.toml` (id, faction, build_repertoire list, hold/shift of `expansion_tick`, composition shares). New loader next to
   `DifficultyLoader`; a missing or invalid file means no openers and no plan (the tier plays as today).
2. **Plan input to the AI**: a new `BuildPlan` value (opener id, tempo, issued tick) in `Rts.Sim.Core`, carried like a directive: a new command
   kind (the next free `CommandKind` number; `CommanderOrder` is 28), one slot per player in a "build_plan" state part (appended last in the part order,
   hashed and saved, `SaveVersion` bump by the lead). The sim stores it and never acts on it. `PlayerView.BuildPlan` exposes it.
3. **AI side**: `RecruitAi.Think` reads `view.BuildPlan` once. When set and the tier is Recruit, `BuildOrderManager` swaps its `_order` (the same
   `Extend`/`InsertAfter` entry points it already has), `TierPlanner` takes the share and tempo multipliers (a `DirectiveBias`-style record,
   `Plain` when unset so every hash stays), `ProductionManager` takes the composition. Plan unset = byte-identical to today.
4. **LLM side**: `ReplyParser` reads `{"plan":{"opener":"<id>","tempo":"slow|normal|fast"}}` (unknown id or tempo = no plan, the standard AI
   goes on); `CommanderPrompt` lists the faction's openers with one line each; `Commander` asks for the plan at the first poll (tick 0, ahead of
   the 600-tick cadence) and again on the re-pick rule; `DirectiveCommands` packs it; the replay records it as any command.
5. **Fallback**: provider off, call failed, reply invalid, stale: no plan command is ever sent. Tests: parse/reject, a scripted fake-provider game
   that builds the named opener's structures in order, plan unset leaves the skirmish and Golden hashes unchanged, replay round trip.

## Risks and open questions
- **Strength may not come from the plan either.** The random-opener control decides this before anything beyond A is built; if the model equals
  the control, stop and keep the edge-based opponent (D-286).
- **Openers are balance content.** Each needs the lead's screen on all 24 cross rows; an opener that wins by exploiting one map is a bug.
- **Fog honesty.** At minute 0 the model knows only its own start; it may not be told the enemy's faction unless the player is told (`PlayerView`).
- **Q for the owner:** should a plan be visible to the human ("Commander X opens with a fast expand") as chat flavour? It costs nothing and
  suits the personas; it does not change play.
- **Q for the lead:** is a new sim command and state part acceptable for an AI-only input (as `CommanderOrder` was in S24), or should the plan
  ride on `Directive` spare bits? The note assumes the command and part, the more honest and replay-safe choice.
