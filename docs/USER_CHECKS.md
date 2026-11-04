# User checks

## Standing smoke test
1. `git -C <repo> log -2 --oneline` shows the `Last commit:` hash from docs/HANDOFF.md as the newest or second-newest commit (the handoff commit may sit on top).
2. (Once there is a build) build from clean clone, run, main menu appears.

## 2026-11-02 · Session 1
- Open `docs/research/README.md` on GitHub → 20 rows (00_SYNTHESIS plus 01–06, 10–17, 20–24) and
  every linked file exists and renders.
- Open `docs/DECISIONS.md` → entries run D-001 through D-034; D-033 says "assumed — confirm".
- Open `docs/design/` → three files: `01_architecture.md`, `02_game_design.md`,
  `03_setting_and_factions.md`.
- Open `docs/OPEN_QUESTIONS.md` → only Q-24 and Q-25 are open; rounds 1–3 are under Answered.
- Open `docs/LICENSING.md` → the EA additional terms are summarised (no trademarks, keep notice, mark
  modifications) and the derived-files register is empty.
- Search the repo for "Tiberium", "GDI", "Nod", "Westwood" → hits only inside `docs/research/`,
  `docs/LICENSING.md`, `docs/00_START_HERE.md` and this file, always as references to the EA games,
  never as names of our own things.
- Open `docs/DECISIONS.md` → runs D-001 through D-035 (supersedes the D-034 line above).

## 2026-11-02 · Session 2
- Clean clone on Windows, then `dotnet build RTS_2027.slnx` → `0 Warning(s)` and `0 Error(s)`.
- GitHub → Actions → latest "CI" run on main is green with artifacts `test-results` and `RTS_2027-win-x64`.
- Open `art/pilot/contact_sheet.png` (v1 pilot) and `art/pilot/contact_sheet_v2.png` (v2 tank, D-043).
- `dotnet run --project src/Game -- --demo` → the old orbit demo (squares circling the centre).
(Test counts, hashes and the default window changed during M1; current values are in the next block.)

### 2026-11-03 · Session 2, M1 (overnight build)
Counts below supersede the M0-11 lines above (the suite grew to 394 tests).
- `dotnet test RTS_2027.slnx` → `Passed!  - Failed:     0, Passed:   394`.
- `dotnet run --project src/Tools -- validate` → `OK rules hash 0x65d5f64698dac4db`, then
  `vehicles 4 · infantry 2 · buildings 4 · weapons 4 · warheads 3 · effects 2`.
- `dotnet run --project src/Tools -- run --ticks 200 --seed 1` twice → both print
  `tick 200 hash 0x56c9dec634c0bdeb`; with `--seed 2` → `0x428e7a1c5ee5812f`.
- `dotnet run --project src/Tools -- map info data/maps/test_64.map.toml` → name test_64, 64x64, starts
  (4,4) and (59,59), map hash `0x38861f8b09f33819`.
- `dotnet run --project src/Tools -- match --ticks 1500 --record match.rpl` then
  `dotnet run --project src/Tools -- replay verify match.rpl` → `OK 1500 ticks, final hash 0x…`.
- `dotnet run --project src/Game` → window titled "RTS_2027 battle sandbox" on test_32: per side 3 tanks, 2 jeeps, 1 artillery
  truck, 5 riflemen (squares) and a turret; player 1 attacks after about 2 seconds. Keys: arrows /
  middle-drag pan, wheel zoom, left-drag select, right-click move (right-click on an enemy: attack), A then
  right-click on ground: attack-move, S stop, G guard, X scatter, F2 cycles fog viewer (you → enemy → all), F3 occupancy,
  F4 paths, F5 weapon ranges, Space pause, `.` single step, Esc quit.
- After Esc the console prints the replay path and the verify command; running it → `OK … ticks`.
- Headless (also run by CI): `dotnet run --project src/Game -- --headless-check 1200` → `sandbox ok tick 1200 p0 4 p1 0 hash 0x0f28036c22a6c8c9`
  on Windows and Linux alike (cross-platform determinism).
- GitHub → Actions → latest CI on main green, including the steps "Headless battle sandbox (end to
  end)", "Verify the sandbox replay" and "Headless match and replay verify".

## Session 3 · M1.5 hardening (2026-11-03)
Pull main first. Requires the .NET 10 SDK.
1. **AI tier matrix:** `dotnet run --project src/Tools -c Release -- ai matrix --ai skirmisher --tiers 1-5 --seeds 2 --ticks 3000`
   prints a 5×5 "Win %" table and a "simulated … ticks in …" line. Placeholder tiers: higher tiers do NOT win yet (M2 tuning).
2. **AI vs idle:** `dotnet run --project src/Tools -- ai match --a skirmisher:3 --b idle --seeds 2 --ticks 3000 --start army` → A wins 4 of 4
   (matches start with an economy by default since M2; the skirmisher only fights, so it needs the army start).
3. **Debug console:** `dotnet run --project src/Game`, press F1, type `help`; with the mouse over open ground type
   `spawn vehicle test_tank 3` + Enter → three tanks appear (stacked on one cell). Esc closes the console without quitting.
   `reveal` toggles the full-map view; `speed 4` runs four times faster.
4. **Published zip:** download the CI artifact `RTS_2027-win-x64-<sha>` from the latest main run, unzip anywhere, run `Game.exe`
   → the sandbox opens. The folder contains `data\`, `LICENSE`, `NOTICE.md`, `THIRD_PARTY_NOTICES.md`.
5. **Crash dialog:** in that unzipped folder rename `data` to `data_x` and run `Game.exe` → a message box says it crashed and
   `logs\crash-*.txt` exists. Rename it back.
6. **Replay survives:** run the sandbox twice → `replays\sandbox_last.rpl` and `replays\sandbox_prev.rpl` both exist;
   `dotnet run --project src/Tools -- replay info replays/sandbox_last.rpl` shows the build and embedded rules.
7. **Git LFS (one time, D-056):** in the repo on Windows: `git lfs install`, then
   `git lfs track "data/sprites/**/*.png" "art/**/*.png"`, `git add .gitattributes`, `git add --renormalize data/sprites art`,
   `git commit -m "chore(repo): track sprite sheets with Git LFS (D-056)"`, `git push`.

## Session 3 · M2 playable skirmish slice (2026-11-03)
Pull main. These were verified headless and from contact sheets only — this is the first on-screen check.
1. **Skirmish:** `dotnet run --project src/Game` → **Skirmish** → `Map seed: 0  (0 = curated)` → **Start**. In the sidebar
   (Q/W/E/R switch Structures / Defences / Infantry / Vehicles) build a Power Plant, wait for READY, click it and place the
   green ghost within 2 cells of your yard; then a Refinery (a Scraper drives out and harvests; credits rise), Barracks,
   Factory and a Tank. Right-click a cameo to cancel. Try Sell and Repair, a factory rally point, and click/drag the
   minimap. Beat or lose to the Recruit AI → a **Victory** or **Defeat** screen. Esc opens Resume / Restart / Quit to menu.
2. **Art in game:** every building plays its construction stages when placed; below half health it shows structural
   damage with animated fire and smoke; tank shells and deaths make explosions; walls join into corners and T pieces.
3. **Your colour:** pick a different colour on the setup screen → your units and buildings use it in the world.
4. **Campaign:** **Campaign** → **Mission 1: Survey** → read the briefing → **Start mission**. Escort the convoy north-east,
   survive two raids, build a refinery in the marked zone → Victory. The objectives panel and message ticker update.
5. **Debug console:** **Sandbox** → F1 → `help`, `spawn vehicle auth_tank 3`, `reveal`, `speed 4`.
6. **Headless (optional, any OS):** `dotnet run --project src/Game -- --skirmish-check 6000`,
   `--mission-check auth_01 6000`; `dotnet run -c Release --project src/Tools -- ai match --a recruit:1 --b recruit:1 --seeds 2 --ticks 30000`.
7. **Report anything that looks wrong** (font halo or missing anti-aliasing on text, sprite offsets, sidebar scaling at your
   resolution) — none of it could be seen on a real screen here.

## Session 4 · M3 so far (2026-11-04)
Only once PR #7 (or a later M3 PR) is merged into main and you have pulled. Saves and replays from earlier builds are
rejected (format bump, D-082). Nothing here has been seen on a real screen yet.
1. **Art push first:** in PowerShell in your RTS_2027 folder run
   `powershell -ExecutionPolicy Bypass -File .\_art_push\push_art.ps1`. It should end with "Done. All art branches are
   on GitHub." Then delete the `_art_push` folder.
2. **Directorate skirmish:** Skirmish → set **Your faction** to Directorate → Start. You start with the Command Shaft
   and four conscripts. Units without finished art show labelled placeholder boxes.
3. **Tunnels:** build a Hatch (or use a production building), select infantry, right-click your own far entrance: they
   queue, vanish, and surface about 5 s later behind a 3 s darkening/cracking telegraph.
4. **Bore:** select a Bore, press **D**, then right-click soft ground: a 5 s telegraph, then a new Hatch. (Upgrading a Hatch to a
   Bore Head has no button yet — M3-18.)
5. **Upgrades:** press **T** for the Upgrades tab. As Authority, build the Engineering building and research Foundation
   Grouting; wall a ring with a gate: your tanks drive through the gate, enemies go round.
6. **Radiation / Blackout:** radiation weapons leave green-tinted ground that hurts infantry; Blackout drops darken
   cells and hide what's inside.
7. **Audio:** select a unit (select sound); gunfire pans and fades with the camera. No music ships yet — see
   docs/audio/music_brief.md for the ACE-Step prompts.
8. **Engineer:** right-click a damaged own building (repair) or a neutral or badly damaged enemy building (capture).
9. **Main menu:** launch the game: an animated lobby with Continue, Campaign, Skirmish, Replays, Archive, How to Play,
   Settings (with Master volume and About) and Quit; LAN Game and Map Editor are greyed. Continue is greyed and Load /
   Replays only list files for now (no in-game save or replay viewer yet, M3-20). F9 opens the Sandbox. Open the Archive and read the lore.
10. **Not wired yet (M3-18):** the Sunrise Device button, turret layers drawn separately in game, the Hatch → Bore Head
   button, Archive unlocks from what you see in games.

## Session 5 · M3 finished + M4 skirmish depth (2026-11-04)
Pull main first (M3 landed in PR #7/#8, M4 in PR #9, the M4 AI in PR #10). Saves and replays from earlier builds are
rejected (D-094). Nothing here has been seen on a real screen yet.
1. **Art push first:** put the three files you were sent (push_art.ps1, rts_art.bundle, art_objects.tar) in a folder
   `_art_push` inside your RTS_2027 folder and run `powershell -ExecutionPolicy Bypass -File .\_art_push\push_art.ps1`.
   It should end with "Done. All art branches (incl. integ-art, every structure from session 5) are on GitHub."
   Then tell Claude "art pushed"; the structures join main through a PR.
2. **Skirmish setup v2:** Skirmish → up to 4 slots; each cycles Kind / Fac / Tier / Team / Col. Pick a map, or
   "Generated" with "Biome: …" and Small / Medium / Large. Set every slot to AI: the button reads **Spectate**
   ("(no Human slot: you will watch)").
3. **Spectator:** in a Spectate game, cycle viewers and toggle vision; the stats panel and the game-over stats appear.
   In the Sandbox, F9 starts a spectator game.
4. **Garrisons:** right-click your own Blockhouse/Redoubt or an empty neutral ruin with infantry → they enter and pips
   appear; select the building and press **D** to unload; engineers capture instead. Enemy pips show only when visible.
5. **Tiers:** the sidebar groups items under "Tier 2" / "Tier 3" headings; locked items show "LOCKED". The AIR tab (Y)
   appears after an Airfield or Mooring. Upgrade tooltips list their effects.
6. **Foam:** the foam truck's shot leaves a tint on the ground that fades; burrowers can't surface through it.
7. **Theaters:** generated Glass Coast, Mountains and Ash Plains maps each have their own colours (ASSUMED, Q-36).
8. **Map editor:** Main menu → **Map Editor** → new map, paint, **Validate** (F5 re-checks), save, then **Play this map**.
9. **Save/load:** Esc → **Quicksave** or **Save game** → choose a slot; Continue and Load on the main menu bring it back
   (the AI restarts its plan after a load).
10. **Replays:** Replays → pick one → it plays back in the viewer.
11. **Sunrise Device and Archive:** build the Sunrise as Directorate; its button, targeting and flash work in game.
    The Archive unlocks entries for what you see.
12. **Headless AI check (optional):** `dotnet run -c Release --project src/Tools -- ai matrix --players 4 --size 64 --seeds 1 --tiers 3 --faction mixed`.
13. **Look at the art notes** in HANDOFF ("Waiting on the user").

## Session 6 · M4-16 round 1, M3-21, Neutral, M5 settings (2026-11-04)
Pull main (PR #11). Saves from session 5 builds still load; their AI starts fresh.
1. **AI keeps its memory on load:** play a skirmish against a tier-2+ AI for a few minutes, save, load. The load note reads
   "The computer players carry on from where they were." and the AI carries on with its build order and waves.
2. **Turret facing:** fire a turreted tank at something off to its side; the turret turns independently of the hull.
3. **Dive marker:** order a Directorate burrower across soft ground; a dashed ring closes in on it while it dives.
4. **Gate open state:** drive one of your units into your own gate: a green wash and opening bar appear (placeholder until the
   open-gate art, ART-S6).
5. **Sunrise:** when it finishes charging the HUD shows "<name> READY - click it, then pick a target" and the button pulses
   gold; click it and move the cursor: the blast circle follows.
6. **Garrisons:** a filled Blockhouse/Redoubt holds against plain infantry but falls to Sappers, a Seismic Mortar or one tank.
7. **Neutral ruins:** idle units next to an empty ruin never shoot it; you can still order an attack on it by hand; the setup
   screen never offers "Neutral" as a faction.
8. **LLM settings:** Settings → "AI commander (LLM)..." → pick a provider, "Set API key..." (masked) → the page shows
   "key set"; "Test connection" answers "not wired yet" (wired in M5-05). Close the game: `%APPDATA%\RTS_2027\settings.toml`
   has `llm_` lines and no key; `llm.key` is binary.
9. **Start corners (optional, tools):** `Tools ai match --a tiered:3 --b tiered:3 --faction-a authority --faction-b authority
   --seeds 8 --ticks 25000 --size 56` prints "Wins by start corner: …" — roughly even is the target.

## Session 7 · M5 LLM commander (2026-11-04)
Pull main (PR #12). **Saves from earlier builds no longer load** (sim save format 9), and replays recorded by earlier builds will
report a desync (the sim state gained a part); record new ones.
1. **Settings:** Settings > "AI commander (LLM)...": pick Anthropic, paste your key, press "Test connection" → "Connected (Anthropic)." (or a
   short "Failed: ..." line that never shows the key). The default model is claude-haiku-5-5; "Use stronger model" is claude-sonnet-5-5.
2. **Setup:** Skirmish setup, press "Cmdr" on an AI slot: the row reads "AI LLM - <tier> ...". With no key it adds
   "(no key - plays as standard AI)".
3. **Commander game:** start it with a key set. After about 30 s of game time the commander's first line appears bottom-left under
   its leader's name (e.g. "Brigadier Hester Wrenfield"); every 30 s it may change strategy.
4. **Talk back:** press Enter, type a message, Enter. Your line shows as "You -> <name>: ..."; the reply comes with its next decision.
   With two commanders, "/to 2 hello" talks to player 2.
5. **No key:** a commander slot without a key shows "<name> is offline — playing the standard AI." once and plays like a normal AI.
6. **Replay:** finish or quit a commander game, open its replay: it plays to the end with no desync and makes no network call
   (works with the network off).
7. **Save/load:** save mid-game and load: the commander comes back and makes its next decision straight away (once past 30 s of game time).
8. **Spectate:** set every slot to AI with "Cmdr" on and Spectate: the spectator table shows "LLM" on those rows.
