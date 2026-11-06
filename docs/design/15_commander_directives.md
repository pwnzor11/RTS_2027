# Design note · Making the LLM commander's directives matter (later milestone)
Status: idea for a future milestone; nothing decided (D-161). Owner asked for this note after the session-12 tests.

## Why
Session 12 measured the LLM commander on the owner's PC (docs/balance/s12_commander.md): about 2 500 calls, 0 errors, sensible play, but
no measurable strength change at tier 3 with either gpt-5.4-mini or gpt-5.4, old or new prompt (6-9 wins of 16 in every run). The
directive levers only nudge the built-in AI, so a better model has nothing strong to pull.

## What a directive does today (src/AI/Recruit/DirectiveBias.cs)
- posture scales the wave threshold (turtle 1.6x, aggressive 0.7x, all-in 0.45x), home guard, retreat threshold and garrison keep.
- focus shifts spending (economy, tech, air, superweapon); mix biases the unit choice; target picks the enemy player.
- focus_cell is an attack cell (aggressive/all-in) or a rally cell (turtle). expand toggles new bases.
All of these are soft biases on decisions the tiered AI already makes on its own schedule.

## Ideas (to weigh, not all at once)
1. **Timed orders** the AI must carry out: "attack cell X at minute N with at least K army value", "hold cell X until told otherwise".
   A clear command the model can plan around.
2. **Counter-building:** mix names a counter to what the summary shows (anti-air vs aircraft seen, anti-armour vs tanks), with a
   strong production weight instead of a bias.
3. **Split armies:** a raid group (size, target cell) separate from the main army; harassment the built-in AI never does at tier 3.
4. **Economy calls:** "expand now to cell X", "tech to tier N now", spending reserve to keep.
5. **Richer summary back to the model:** recent fight outcomes (won/lost, value traded), so it can learn within a game.
6. **Measure:** the same 32-game swap test (Test 1 method) plus a "random directive" control, so a gain is the model's and not the levers'.

## Constraints
Directives stay outside the sim (D-110): they are AI inputs, recorded in replays as now. No cheats (D-061). Whatever is added must work
for all four factions and fall back to the standard AI when the provider is off.
