# Design 04 · M2 plan: playable skirmish slice
From D-053, D-058, D-059, D-061, D-062. Planned in session 3. Owner can try at the end: menu → skirmish
vs the Recruit AI on a Rust Belt map → build a base, harvest, produce, win or lose in about 10 minutes.

## Items (ids in ROADMAP)
| Id | What | Wave | Model |
|---|---|---|---|
| M2-01 | Corner advantage on generated maps | 1 | Sonnet |
| M2-02 | Rules schema v2 (economy, production, harvester fields), state-part skeletons, command payloads, HUD contract | 1 (first) | Sonnet |
| M2-03 | Front-end skeleton: `SimHost` fixed-step driver, screen stack + input router, menus (main, skirmish setup, pause, game over) | 1 | Sonnet |
| M2-04 | UI font: OFL TTF → bitmap atlas at build time, UI scale | 1 | Haiku |
| M2-05 | House colours on the GPU: base + house-mask layers tinted by SpriteBatch (no per-house texture copies), sprite manifest for multi-state sheets | 1 | Haiku |
| M2-06 | Art pilot: Refinery (3×3) in Blender at tank quality — idle, damaged, build-up frames — owner approves | 1 | Sonnet |
| M2-07 | Economy sim: harvester missions (seek → load → return → dock → unload trickle), depletion + regrowth, storage cap + silos, power totals | 2 | Sonnet |
| M2-08 | Production sim: queues per category, prerequisites, cost drain + stall, cancel refund, extra factories, build radius, ready-to-place, unit exit + rally, sell, repair, low-power slowdown + unpowered defences, win rule, game over | 2 | Sonnet |
| M2-09 | Authority data: the D-058 roster in `data/rules`, placeholder balance; sandbox/match/AI runner moved to the new ids | 2 | Haiku |
| M2-10 | Rust Belt M2 map: generator theme + terrain colours/texture pass, one curated 64×64 seed | 2 | Haiku |
| M2-11 | Sidebar (classic right-hand: cameos, tabs, queue progress, place cursor with footprint validity, sell/repair, credits + power bar) and minimap | 3 | Sonnet |
| M2-12 | Recruit AI: economy, build order, production, waves — on the harness; must beat `idle` and lose to nobody else yet | 3 | Sonnet |
| M2-13 | Art fan-out: 7 structures + 5 units from the approved pilot (house-colour masks, 16 facings vehicles / 8 infantry) | 3 | Sonnet ×3 |
| M2-14 | Integration: full game loop end to end, headless skirmish in CI, balance pass with `ai match` | 4 | lead |
| M2-15 | Campaign pilot (D-061): minimal MoonSharp host with the A-7 determinism sandbox + one scripted Authority mission with a briefing; hours logged | 4 | Sonnet |

## Contracts (M2-02 lands them; everything else builds to them)
- `ProductionCategory { Infantry, Vehicle, Aircraft, Structure, Defence }`.
- `SimRules.Economy` (init, `[economy]` in game.toml): starting_cash, credits_ashfall, credits_pitchblende
  (per density unit), harvest_ticks_per_unit, unload_ticks_per_unit, regrowth_interval_ticks,
  sell_refund_permille, repair_cost_permille, repair_hp_per_tick, build_radius, max_queue,
  build_ticks_per_100_credits, low_power_slowdown_permille [3] (power ratio < 100 / 75 / 50 %).
- `BuildingType` (init, optional keys): category (structure|defence), produces (""|infantry|vehicle|aircraft),
  build_time (ticks; 0 = from cost), storage, refinery, dock [x,y] (relative cell), exit [x,y], free_unit
  (vehicle id), wall, needs_power. `VehicleType`: harvest_capacity, build_time. `InfantryType`: build_time.
- State parts appended after "players": "economy", "production".
- MissionKind appended: Harvest = 7, Return = 8 (dock + unload).
- Commands: Build (Arg = category, Arg2 = type index), Cancel (Arg = category, Arg2 = queue position), Place
  (Arg = building type; only a ready item outside setup), Rally (Subject = factory building: sets primary +
  rally Cell), Sell (Subject building), Repair (Subject building, toggles), Harvest (Subject vehicle,
  optional Cell), Return (Subject harvester).
- `Simulation.HudFor(byte player) → PlayerHud` (cash, stored, storage cap, power out/use, per-category
  queue items with progress permille, ready-to-place type, buildable types, defeated) and `Simulation.GameOver`
  / `WinningTeam`.

## Lead choices (assumed — confirm, logged as D-063 when built)
- Walls are 1×1 buildings flagged `wall`; they obey the one-cell gap rule (no full sealing) so pathing stays
  sane; they don't count for defeat or build radius.
- The minimap is available from the start in M2 (the roster has no radar building); radar gating comes
  with the Comm Center in M3.
- Terrain stays tinted flat colours plus a light procedural texture in M2; real terrain tiles are M3 art.
- No audio in M2 (the `IAudio` null backend arrives with the screen stack).
