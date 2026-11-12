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
  F4 paths, F10 weapon ranges, Space pause, `.` single step, Esc quit.
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
Pull main (PR #12). **Saves from earlier builds no longer load** (sim save format 9), and replays recorded by earlier builds may
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

## Session 8 · Art on main, reworks, M6 contracts (2026-11-04)
Pull main (PRs #13, #14, #15). **Saves from earlier builds no longer load** (sim save format 10), and replays recorded by earlier
builds may report a desync; record new ones. Run `git lfs pull` after pulling so the new sprites are real images.
1. **Directorate art:** start a skirmish as the Directorate. The structures from session 5 (Assembly Bore, Bore Head, Bunker,
   Cistern, Hatch, Isotope Works, Mast, Mooring, Muster Hatch, Pillbox, Rad Mortar, Reactor Vent, Rocket Nest, Shaft, Smelter
   Vent, Sunrise) show real art in your house colour, not placeholders. The Hatch has a bar cage (no lift car); the Assembly Bore has a
   cornice, clerestory and stacks; the Rad Mortar carries a sickly green glow.
2. **Authority art:** as the Authority, the Seismograph shows a paper drum with ink rings and a pen arm; the Searchlight has a
   glass lens with a cross-bar; the Gate plays an opening animation when your own unit passes; Engineering shows real art.
3. **Turrets on vehicles:** the Decon Truck, Agitprop, Reactor Crawler and Rocket Truck turn their turret separately from the hull
   and the turret stays seated on the hull at every facing; the Dirigible draws a shadow on the ground below it.
4. **Nothing else changes:** M6 (Left Behind) is groundwork only; the faction is not in skirmish setup yet.

## Session 9 · Left Behind playable, veterancy, supply cap (2026-11-05)
Pull main. **Saves from earlier builds no longer load** (sim save format 11) and older replays may report a desync; record new ones.
Run `git lfs pull` after pulling. The Left Behind art reaches main only after you run `_art_push\push_art_s9.ps1` (see HANDOFF);
until then the Left Behind shows placeholder boxes.
1. **Left Behind setup:** Skirmish setup lists "Left Behind" as a faction. Start as Left Behind: the sidebar says "Unpack the Mother
   Rig to build". Select the Mother Rig, press K ("UNPACK RIG (K)"), click a clear site: it unpacks and the build tabs fill.
2. **Pack and move:** select an unpacked rig and press K ("PACK RIG (K)"): it packs into a vehicle; drive it and unpack elsewhere.
   While it is packed its production lane pauses, then carries on after unpacking.
3. **Salvage and raids:** destroy enemy vehicles; wrecks stay on the ground for about a minute; a Hauler or Tinker collects them for
   cash. Send raiders into an enemy refinery or silo: they take cash from its stored ash.
4. **The Second Drift:** build it (after the Rotor Pad); once charged, fire it anywhere: a storm slows enemy vehicles inside it (Left
   Behind units are never slowed) and hides your Veilwalkers as ash does.
5. **Veterancy:** units that kill earn pips ("Veteran", "Elite", "Hero"); a promotion flashes and plays a short cue. Higher ranks
   hit harder, take less damage and fire faster.
6. **Supply cap (off by default for now, D-133):** set `enabled = true` under `[supply]` in data/rules/game.toml. Queue many riflemen;
   once supply runs out the next one is refused with "Supply cap reached: build a supply structure". Build an Authority Ration Hall
   (Directorate: Bunk Gallery; Left Behind: Still Rig) and the queue goes on. The AI builds these too. There is no supply counter on
   screen yet (SUP-4).
7. **AI as Left Behind:** set an AI slot to Left Behind at Veteran: it unpacks, builds, raids and attacks; it may pack and move its
   base when hard pressed.

## Session 10 · Supply on, veterancy AI, naval groundwork, ocean maps (2026-11-05)
Pull main and run `git lfs pull`. **Saves from session 9 builds no longer load** (sim save format 12); replays from older builds
still play. The Expedition is in the data but cannot be picked yet; its ships, landings, submarines, carrier and Salvo Array are
covered by tests only.
1. **Supply counter:** start a skirmish. The sidebar shows "Supply used/cap"; it turns amber when you are 4 from the cap and red at
   the cap. Supply is on by default now (`[supply] enabled = true` in data/rules/game.toml).
2. **At the cap:** queue riflemen until the cap. Hover a greyed infantry cameo: "Supply cap reached. Build more <building>." (the
   Authority names the Ration Hall). A queued unit that no longer fits shows "Waiting for supply" until you build one.
3. **Unit info panel:** select one of your own units: the top-left of the map shows its name, health, rank and "Kills k / t to
   <next rank>".
4. **AI and veterancy:** play a Veteran AI for 15+ minutes. Its ranked units pull back earlier when hurt and its waves go after your
   rank-3 units first.
5. **Left Behind salvage:** play as or against a Left Behind AI. Early on it builds a Hauler, and after fights the Hauler drives to
   the wrecks to collect them.
6. **Ocean maps:** in skirmish setup pick a generated map; a new row reads "Ocean: None". Change it to "Ocean: North and South" and
   start: those edges are sea with shallows and a beach. Save, quit and load the save: the sea is still there.
7. **Map tool:** `dotnet run --project src/Tools -- map gen --biome rust_belt --size s --players 2 --seed 4 --ocean n --out o.map.toml --ascii`
   prints a map with a northern sea and its convoy and landing counts; `map validate o.map.toml` reports no issues.

## Session 11 · The Overseas Expedition is playable (2026-11-06)
Pull main and run `git lfs pull`. The Expedition units and buildings still show placeholder art until the art branch is pushed (see
step 8). Saves from session 10 builds still load (format 12).
1. **Pick the Expedition:** Skirmish setup → change a slot's faction until it reads "Overseas Expedition". With a generated map and
   "Ocean: None", the note "The Expedition needs a coast: one ocean edge added." appears and the map gets a southern sea (D-151, Q-55).
   Pick a hand-made map with no coast: the setup refuses it with "Slot N: the Overseas Expedition needs a map with a landing coast."
2. **Establish:** you start with a Command Ship, a Landing Craft, three Marines and a Skimmer. Sail the Command Ship next to the beach
   and Deploy it: the Landing Depot appears on the nearest free ground touching the ship (D-150). Buildings then go only inside the
   tinted beachhead zone; Beacons and the Quay/Shipyard widen it.
3. **Shipments:** build an Anchorage on a south-facing shore (land above, sea below). The sidebar shows the shipment countdown; a
   tender sails in from the map edge every 30 s and pays 2 000, shrinking 6 % each time (D-152). Sink an enemy tender to stop it.
4. **Ships:** with no Shipyard on water, hovering a ship cameo says "Needs a shipyard on water". Select a Carrier: the panel shows
   "Hangar 2/4" (docked / places). Select a Missile Sub and press D to dive.
5. **Expedition AI:** play against an Expedition AI (tier 2) on a map with an ocean. It lands, builds an Anchorage, gets shipments and
   sends small naval strikes at your coast.
6. **Archive:** Archive → Leaders shows Aldersey, Strand, Roque and Kell; their "Classified" entries stay locked (campaign).
7. **Tooltip:** hover the Fast Charging upgrade: it reads "All players: shipment interval x0.85".
8. **Art push (owner PC, D-084):** unzip `rts2027_art_push_s11.zip` into your RTS_2027 folder (so it is `RTS_2027\_art_push`) and run
   `.\_art_push\push_art.ps1` from the RTS_2027 folder. It pushes branch `wp/art-exp-all` (all 46 Expedition sprites); Claude opens
   the PR next session.

## Testing the LLM commander with your own key (PowerShell)
This plays one AI-vs-AI game where side A is commanded by a language model. It needs your own OpenAI key and internet access.
1. In the PowerShell window you will use, set the key for this window only (it is not saved anywhere, and closing the window removes it):
   `$env:OPENAI_API_KEY = "<your key>"`
2. From the RTS_2027 folder, run (replace `<model>` with any chat model your account has):
   `dotnet run --project src/Tools -c Release -- ai commander --a tiered:3 --b tiered:3 --provider openai --model <model> --seed 1 --ticks 30000 --out commander_run`
   The game waits for each model reply before it goes on, so one game can take several minutes.
3. Output lands in the `commander_run` folder: `summary.md` (winner, ticks, number of calls, errors), `directives.log` (one line per decision: tick, directive, latency in ms, ok or error) and a `commander_seedN.rtsreplay` replay file.
4. If the key is missing, the command prints which variable to set and stops without creating the folder.
5. Send Claude `summary.md` and `directives.log`, never the key.

## Campaign menu (M8-08, M8-16)
1. **Faction pick:** Main menu → Campaign. Four factions show; Authority and Directorate are open, Left Behind and Overseas Expedition say "(locked)" and, when highlighted, "opens after act 2 (act 3) of the Authority or Directorate campaign". "Load game" there still lists campaign saves.
2. **Mission select:** pick Authority. "Survey Convoy [next]" starts; placeholder missions of other factions read "[not written yet]" and do nothing on Enter.
3. **Briefing:** the text types out (Space shows it all), the objectives follow, then a Difficulty row (Left/Right or the -/+ buttons, five tier names) and Start mission. `[[archive_id]]` links in a briefing appear as the entry title, with "(locked)" added while locked (plain text, not greyed).
4. **Win and debrief:** win the mission: a debrief shows time, objectives done, new tech and new Archive entries; Continue returns to the mission select with the mission marked "[completed]". (Since s12 the Authority campaign lists acts 1-4, so the ending shows only after auth_04 is written and won.) The ending sequence: 2 slides with "PENDING owner text" captions on dark panels (put `data/images/endings/<image id>.png` to show a still). Click/Enter = next, Esc = skip.
5. **Loss:** lose the mission: the Defeat menu offers Restart (same difficulty) or Quit to menu; nothing is saved.
6. Progress files are `campaign_<faction>.toml` beside settings.toml; delete one to restart that campaign.

## Tutorial and Survey Convoy rework (M8-09, M8-10 pilot, session 12)
1. **Tutorial:** Main menu → Campaign → Tutorial → Start. Seven lessons in order (move, attack the practice power plant, Power Plant, Barracks, train a Rifleman, Refinery and a Scraper trip, destroy the camp); each new objective appears when the last is done, and the Instructor explains each step at the bottom left. It cannot be lost. Tell me if any lesson is unclear or stalls.
2. **Survey Convoy:** Campaign → Authority → Survey Convoy. Brigadier Wrenfield's radio lines show as "WRENFIELD: ..."; the camera jumps to the convoy at the start; the objectives list shows plain text (not keys like a1.obj.reach). After the first raid a "Second raid m:ss" countdown shows under the objectives. The hidden "surveyors" bonus objective does not show until revealed. Try it on Difficulty tier 1 and tier 5: raids get bigger at the higher tier.
3. Tone check: read the Instructor lines in data/missions/tut_01/locale.toml and Wrenfield's in data/missions/auth_01/locale.toml; this voice becomes the template for the other factions' first missions.


## First missions of the Directorate, Left Behind and Expedition (M8-10, session 12)
Campaign → pick the faction → mission 1 → Start. Left Behind and Expedition open after act 2 / act 3 of either first campaign, which are not written yet. To try them now, quit the game and save this as `campaign_directorate.toml` beside settings.toml (it marks Directorate acts 1-3 done; delete the file afterwards to reset):
```
version = 1
faction = "directorate"
completed = ["dir_01", "dir_02", "dir_03"]
techSet = []

[difficulty]
```
1. **Directorate 1 (dir_01):** Larkin on the radio. Raise a Reactor Vent, then open a Hatch in the marked site; hold two Authority waves (the second on a "Second response" timer). Bonus: destroy the Authority picket in the north-east.
2. **Left Behind 1 (lb_01):** Calloway-Ross on the radio. The Mother Rig arrives packed: unpack it, raise power and a Muster Rig, ambush the Authority convoy at the crossing and beat off its relief. Bonus: an abandoned scraper in the south-west gully. Tell me if the convoy's halt at the crossing feels contrived.
3. **Expedition 1 (exp_01):** Strand on the radio, new coastal map. Deploy the Command Ship on the beach, build only inside the beachhead zone, hold three Authority waves while two shipments land. Bonus: lose no landing craft.

## Full campaign, all four factions (M8-12, session 12)
Every faction now has four missions and an ending. Play each campaign start to finish on tier 3 (tier 1 for a quick pass). After act 2 of Authority or Directorate, Left Behind opens; after act 3, Expedition opens. Win act 4 to see the ending slides (DRAFT text). The questions the writers left for you are in docs/missions/s12_owner_review.md.

## Session 13 · Campaign acceptance, briefing stills hook, tier ladder (M8-14, M8-15, M4-16)
- Campaign: open any act 2-4 mission briefing (for example Authority act 2). It should look exactly as before: no still images are shipped
  yet, so nothing is drawn. Once stills land, the image sits above the text and never pushes the Start/Back buttons off a short window.
- Endings: the four ending files no longer say DRAFT (owner-approved, D-164).
- Bot play-through of all 16 missions (about 2 minutes): `dotnet test tests/Scripting.Tests -c Release --filter "Category=Campaign16"` → passes.
- Tier ladder: `dotnet run --project src/Tools -c Release -- ai match --a tiered:4 --b tiered:3 --faction-a directorate --faction-b directorate --seeds 32 --size 96 --ticks 30000`
  should give 23 / 21 / 20 (tier 4 wins / tier 3 wins / draws).
- Skirmish at tier 4 against tier 3 (or yourself against tier 4 then tier 3): tier 4 should now feel at least as strong; tell me if it doesn't.


## Session 14 · UI overhaul, act-break stills, mission tiers, defeat rule (M11, M8-14, M8-15, M4-17)
Pull first: `git pull --ff-only origin main` (git-lfs is installed on the PC now). Settings are kept; new options get defaults.
- **Camera:** mouse at any screen edge scrolls; hold Shift for double speed; Home centres on your base; Ctrl+F5..F8 stores a view, F5..F8 recalls it.
- **Clean HUD:** no tick/hash/fps line and no key hints on screen. Settings > Controls > "Show diagnostics" brings the line back.
- **Controls page:** F1 or `?` in a game (in the sandbox F1 is the console, so use `?`), or Pause > Settings > Controls. Opaque panel, scrollbar, Back inside the panel. Click a key and press
  a new one; a taken key offers Enter to swap / Esc to cancel; "Reset all keys to defaults". Quit and restart: rebinds are kept.
- **Debug keys:** in skirmish and campaign F2/F3/F4/F9/F10/`.` do nothing; in the sandbox they work (ranges moved from F5 to F10).
- **Orders:** right-click shows a green ring (red on an enemy). Select a factory with a rally point: flag and line. Press A: red crosshair with
  "ATTACK-MOVE"; left click (or right click) sends, Esc cancels first, a second Esc opens the pause menu. A with only a building selected does nothing.
- **Selection card:** select one unit: card at the bottom centre with health, rank/kills, stats and order buttons showing your current keys.
  Box-select a group: cameo grid, click = only that unit, Shift-click = drop it. Select a building: health, power, production, rally, Sell/Repair.
- **Display:** units are about 25 % bigger (look for pixel wobble on moving units and say if it bothers you). Settings: "Health bars" Always /
  Damaged or selected. Sidebar: "POWER out / use" turns red and the bar blinks when low; buildings stopped by low power show a blinking
  bolt-with-slash icon. Buildings animate calmly; construction takes about 3.5 s. Minimap fits the map shape. Cameo tooltip sits over the map.
- **Objectives:** in any mission: one-line bar at the top, "NEW OBJECTIVE" banner, pulsing marker on the target, edge arrow with "N cells
  <direction>" when it is off screen, ring on the minimap. J jumps to it, Tab cycles, O opens the list with the briefing. Settings
  "Camera cue on new objective" turns the short pan off.
- **Briefings:** headings and bold render properly (no `#` or `**`); Start/Back sit at the bottom; act 2-4 openers show the act-break still.
- **Mission tiers:** play exp_04, lb_03 or dir_02 at tier 5: clearly harder than tier 1 (exp_04: the Directorate guard marches out when the
  Shaft or Device is destroyed and must be beaten).
- **Voice lines (optional):** follow the instructions in chat (voice_work\wyoming_batch.py with wyoming-piper on the NAS); a generated
  line under data/audio/voice/<leader>/ plays in the mission instead of text only.
- **Defeat rule (skirmish):** destroy an enemy's construction building, factories and harvesters but leave its defences: about 2 minutes later it
  is defeated.
- **AI ladder repeat (laptop):** re-run balance_runs\run_s14_baseline.ps1 after deleting the s14_*.md results, and compare with
  docs/balance/s14_laptop_baseline.md.

## Session 15 · Play-test fixes, M11 batch 3, siege AI, maps, art (M11-10, M12-01..06, M10-01..04, Q-64..Q-66)
Pull first: `git pull --ff-only origin main` then `git lfs pull` (new sprites). Run: `dotnet run --project src\Game -c Release`.
- **Harvesting:** select a scraper (you get one free with each refinery), right-click an ash or pitchblende field: it drives there and harvests. Right-click
  your own refinery: it delivers. Move it away, right-click a field again: it resumes. The cursor shows the harvest/enter symbol first.
- **Windowed mode:** make the window smaller; the edge band scrolls; moving the mouse onto the title bar or another window stops it.
- **Selection:** Ctrl+1..9 sets a group, 1..9 recalls it, twice quickly centres on it, Shift+number adds. Shift+click/drag adds; double-click or
  Ctrl+click selects all of that type on screen; **C** selects the whole army. Group numbers show next to health bars and in the card.
- **Alerts:** "Base/Unit/Harvester under attack", "Construction complete", "Unit ready", "Low power", "Unit/Building lost" top right with a minimap ping;
  **End** jumps to the last alert, again for the one before.
- **Cursors / placement / tooltips:** cursor changes per action (move, attack, enter, capture, repair, harvest, blocked...). Placing a building shows the
  sprite at half alpha, red/green cells, a reason line and the build radius. Cameo tooltip: cost, time, power, description. Shift+click queues 5.
- **Production hold:** right-click a unit or structure in progress: "ON HOLD", money stops; left-click resumes; right-click again cancels with a refund.
- **Settings:** "UI scale" 75-200 % (shows "limited by window size" when capped), "Text size" Small..Extra large, "Larger infantry" (on).
- **Look:** terrain look C (clear roads, water, cliffs; resource fields as slag heaps and crystals). **H** toggles map decor. Damaged units and buildings
  smoke (under 50 %) and burn (under 25 %); dead buildings leave rubble, vehicles a wreck and scorch; tanks kick up dust; each biome has a slight tint.
  New explosions, blue energy blast (Expedition), fire; rocks/rubble/charred wood (0 A.D.) and concrete blocks/stumps (LPC) on generated maps.
  Old look: set `RTS_TERRAIN_LOOK=classic`, `RTS_FX=classic`, `RTS_FXLOOK=off`, `RTS_PROCEDURAL_PROPS=1` before starting.
- **Infantry:** all 18 types are bigger with clear team colour on every facing.
- **Maps:** generated maps have blocking props (tree/wreck/barrier clusters) that ground units path around; an old skirmish save on a generated map
  shows "This save was made with an older map generator and can't be loaded." Tools: `dotnet run --project src\Tools -- map report <map or seed>`,
  `map render --png`.
- **Missions:** auth_02 (The Junction) at tier 1 vs tier 5: small late raids vs about three times larger raids with Ashthrowers and Sappers.
- **AI:** fewer endless stalemates in skirmish (siege mode: the AI gathers, brings artillery, hits factories first). Laptop repeat (optional):
  `powershell -ExecutionPolicy Bypass -File tools\measure\run_s15_series.ps1` after deleting balance_runs\s15\*.md, compare with
  docs/balance/s15_laptop_series.md.

## Session 16 · Team colours, map layout 2 (off by default), Directorate early tech (D-191..D-197)
- **Vehicles:** team (house) colour reads clearly on every facing of the vehicles and turrets re-coloured in session 16 (D-192).
- **Skirmish default:** a new skirmish still uses map layout 1; layout 2 is not the default (`CurrentLayout = 1`, D-197).
- **AI match:** `ai match --layout 2 --biome rust_belt` runs, and its table heading reads "rust_belt layout 2" (docs/balance/s16_layout2.md).
- **Other biomes:** `ai match --layout 2 --biome mountains` and `ai match --layout 2 --biome glass_coast` run; `ai match --layout 2 --biome ash_plains` runs, but the Expedition still builds nothing there (known, not fixed, D-197).
- **Timeline and matrix:** `ai timeline --layout 2 --biome rust_belt` and `ai matrix --layout 2` run and take the same flags (docs/balance/s16_layout2.md).
- **Optional laptop run:** `tools/measure/run_s16_layout2.ps1` (32 seeds, 64 games per row, rust_belt and ash_plains by default) gives the layout 2 numbers to compare with the layout 1 base column.

## Session 17 · Art baseline, Expedition start units, map layout 2 v6 (D-198..D-201)
- **Left Behind rigs:** Muster Rig, Lookout, Dynamo Rig and Refinery Rig stand on the darker grey-edged pad, like the Mother Rig (D-200).
- **Expedition on Mountains:** start a skirmish as the Expedition on a Mountains map (default layout): you get the Command Ship and the start units
  (before session 17 the start could be empty).
- **Ash Plains layout 2:** `map render --biome ash_plains --size m --players 2 --seed 1 --layout 2 --png a.png` shows a bridge and a broken causeway on
  both sides of the centre field; `map report` with the same arguments prints the harvest distance line.
- **Expedition v Expedition on Ash Plains layout 2:** `ai match --a tiered:3 --b tiered:3 --faction-a expedition --faction-b expedition --size 96 --seeds 1 --layout 2 --biome ash_plains` ends with both sides having built.
- **Laptop run (64 games per row):** `powershell -ExecutionPolicy Bypass -File tools\measure\run_s17_layout2.ps1`, results in `balance_runs\s17l2\s17l2_summary.md`.

## Session 18 · Audit fixes, Field Manual pilot (D-202..D-206)
Back up `%APPDATA%\RTS_2027` first (progress and settings files are now written via a temporary file; unreadable ones are kept as `.bad`).
- 2v2 skirmish: right-click an ally's unit (or a full or non-garrisonable ally building) with units selected: they move, no attack cursor.
  An allied building with room still garrisons infantry.
- Build a War Factory, sell the Refinery before placing it: the Factory cameo stays on the sidebar and can be placed or cancelled.
- Tutorial: select units; the instructor line sits above the selection card.
- Mouse wheel over a fight: the point under the cursor stays put.
- Press A, then right-click the minimap: units attack-move there.
- Settings: UI scale 200 %, Text Extra large, window 1280x720: the main menu and skirmish setup fit; slot labels end in "..." and the panel does not change width.
- Load a corrupt save: a Notice instead of a crash.
- exp_03 or lb_04: aircraft from the Flight Deck / Rotor Pad can be built.
- auth_03: let the tide timer run out: the Defeat screen with Retry appears.
- auth_01: lose both scrapers: the mission is lost, not stuck.
- Campaign: save mid-mission, quit, load, win: the debrief shows and the next mission unlocks.
- Win auth_02, back to the Campaign menu: Left Behind reads open; the lock hint names the highlighted faction.
- exp_04 saves and replays made before session 18 no longer load (script changed, D-203): expected.
- A campaign replay shows "watchable only" and no desync warning; a skirmish replay keeps its check.
- Settings > AI commander (LLM)...: switch Provider between Anthropic and OpenAI-compatible; each keeps its own key. An http base URL to
  another machine refuses to send a key (Test connection obeys the same rule).
- About: the full credits list (Skorpio, LPC, Kenney, 0 A.D.) also in a published build.
- `%APPDATA%\RTS_2027\logs\rts_2027.log` exists after a run.
- Laptop series: `powershell -ExecutionPolicy Bypass -File tools\measure\run_s18_audit.ps1` (about 95 min) -> send back `balance_runs\s18audit\s18audit_summary.md`.
- Booklet hero render pilot: `powershell -ExecutionPolicy Bypass -File tools\booklet\render_heroes.ps1 -Pilot` (12 renders, about 15-45 min) -> send back `art\booklet\contact_<date>.png`.

## Session 19 · M13 audit fix batch (D-205, D-207..D-211)
Back up `%APPDATA%\RTS_2027` first (game saves gained control groups and the setup header gained handicap flags; old saves still load).
- **Saves:** F5 shows "Quicksaved."; F9 asks "Load the quicksave?" and returns there. "autosave 1" appears in Load Skirmish right at the match start and "autosave 2" after 5 minutes
  (Settings: "Autosave: every 5 min"; choices Off, every 1/2/3/5/10/15/30/60 min). Camera bookmarks moved to F6, F7, F8, F12; the sandbox spectator key is P.
- **Menus:** Pause > Quit to menu asks "Quit to menu?"; Restart asks "Restart?"; overwriting a save asks "Overwrite save?". Esc on the main menu
  does nothing. Alt-tab out of a skirmish or mission pauses it. Fullscreen is borderless.
- **Garrison:** riflemen in a building: box-select over it and Select all: not selected; a click on the building selects the building.
- **Ready structure:** wall off every site, then right-click (or Cancel) the ready cameo: full refund, the lane frees.
- **Card Sell:** first click shows "Confirm"; a second click within 2 s sells.
- **Expedition:** Mountains (layout 1), Expedition, a few seeds: the Command Ship floats on open sea (a shallow channel may run along the map
  edge) and shipments arrive; Glass Coast Expedition v Expedition: both sides' first shipment arrives at about the same time; a match start
  shows "Your Command Ship is at the centre of the view. Select it and press {key} to land." A floating dock placed off the shore says "Must touch the shore".
- **Combat:** flak firing at aircraft over your own base does not hurt the base; a dived missile sub that fires can be shot back for 5 s and
  shows surfaced; a Decon Truck with Ctrl + right click on bare hot ground sprays and cleans it.
- **Ashfall (skirmish only):** idle riflemen standing in an ash field lose health and walk to the field edge; ordered onto ash they stay.
  Riflemen ordered past a small field walk around it. Campaign missions have no ash damage (D-211).
- **Units:** a loaded landing craft deployed at the shore (D) lets troops out one by one; a Kite is spent by its bomb; the Agitprop speeds up
  nearby Conscripts; a Commissar among Conscripts makes them fire faster; a Directorate Pillbox costs 340.
- **QoL:** skirmish setup "Game speed: 1x" row; in game = and - change speed ("SPEED 0.5x" in the HUD); I and B jump to an idle harvester /
  factory (bottom-right buttons with counts); number pad 7-9/4-6/1-3/0/./+ press sidebar cameos; Alt+click a cameo shows LOOP (repeat
  production); L toggles weapon range rings; cameo tooltips show "Strong vs:" and "Weak vs:"; M / N / U = aggressive / defensive / hold fire;
  Shift + right click queues waypoints (markers); setup slots offer handicap, starting credits and "small army".
- **Names:** the sidebar says "Heavy Tank", "Vehicle Factory", "Ashfall Refinery"; the Minelayer tooltip says it cannot lay mines yet;
  map list shows "Rustbelt", "Ash Reckoning", "Glass Coast"; mission dialogue shows "Brigadier Hester Wrenfield:".
- **Missions:** dir_04: razing the HQ before the device is charged loses the mission; the script-error line shows only with Show diagnostics on.
- **Laptop "after" series:** `powershell -ExecutionPolicy Bypass -File tools\measure\run_s19_after.ps1` (about 95 min) -> send back
  `balance_runs\s19after\s19after_summary.md`.

## Session 19p · M9 LAN + M10 polish (D-212..D-216)
- **LAN (two PCs on one network):** main menu → LAN Game on both. PC 1: Host game. PC 2: the game appears in the list within about 2 s → Join,
  pick a faction, Ready; PC 1: Start. Both play the same match. Press Space on either: both show "Paused by <name>", Resume counts down 3 s.
  Enter opens chat (Tab = All/Team). Pull PC 2's network cable: PC 1 shows "Waiting for <name>…", after 10 s a Drop button, then an AI plays that side.
- **LAN lobby wait:** wait a minute in the lobby before Start: nobody is dropped at the handover (review fix 1).
- **Firewall:** the first Host shows the Windows Firewall prompt: allow Private networks (or tick the firewall task in the installer).
- **Weather / juice:** Ash Plains skirmish: ash drifts; a big building blowing up shakes the screen. Settings → Weather / Screen shake toggle them.
- **Water:** a coast map: glints, white foam along the shore, wakes behind ships.
- **Graphs / Profile:** win a skirmish → Graphs; main menu → Profile shows records; win a campaign mission → Completed / No losses medals.
- **Setup / loading / credits:** skirmish setup shows a map thumbnail that changes with biome and size; Start shows a loading screen with a tip;
  Settings → Credits rolls the credits.
- **Update notice:** with a newer GitHub release, the main menu shows "New version X.Y.Z available" (Settings → Check for updates turns it off).
- **Soak:** `dotnet src\Tools\bin\Debug\net10.0\Tools.dll lan soak --players 4 --ticks 4000 --jitter-ms 40 --loss 10` → "OK: in sync";
  add `--udp` for real sockets on loopback.

## Session 20 · M13 follow-ups (D-217, D-218)
No save format change (the economy part writes its old layout unless a harvester is skipping a field).
- **Big group through a gap:** sandbox or skirmish, wall off an area leaving a one-cell gap, select 40-60 tanks and give one move order
  through it: every tank arrives; none stops short in the queue.
- **Walled-in field:** a harvester whose nearest ash field is walled off goes to the next reachable field within a few seconds instead of
  parking by the wall.
- **Melee acquire:** Directorate, attack-move a Sapper past an enemy tank about 3 cells off its path: it turns, attacks the tank, then
  carries on to the destination. A guarding Sapper attacks an enemy that enters its sight, then walks back to its post.
- **AI lanes:** watch an AI base for a few minutes: units leaving its factories head toward the map centre; nothing piles up south of the exits.
- **LB corner base:** a Left Behind AI in a corner keeps building Still Rigs while its army uses the supply (about 14-18 by 10 minutes is expected).

## Session 21 · Campaign retune with ash on, sight-range engagement (D-219..D-222)
Back up `%APPDATA%\RTS_2027` first: old development saves and replays may not load (D-220).
- **Sight-range engagement (D-219):** skirmish, park a tank next to your base and drive an enemy jeep to about 5 cells from it (outside the
  tank's gun range, inside its sight): the tank drives over, fights, and returns to where it stood when the jeep leaves. Set the tank to
  Defensive and repeat: it fires only at what is in gun range and chases at most 3 cells.
- **Attack-move into a crowd:** attack-move a group onto a cell where your own harvester is parked: the group stops around it and guards
  (each unit then engages enemies in sight), instead of milling at the cell.
- **Ash in missions:** any campaign mission with an ash field: infantry standing on ash take damage and step off; vehicles are unaffected.
- **auth_01 tier 3:** the second raid has no jeep (from tier 4 it does).
- **auth_03 tier 5:** the shore garrison that answers your landing is about 13 units, not 24; the tide countdown is unchanged (9000).
- **dir_04 tier 5:** the third wave has no tank. **exp_03 tier 5:** two bombers at ~3:10, three more from the south-west sea at ~5:20 (no message).
- **lb_03 tier 5:** the Authority column arrives at ~3:35 with 7 riflemen, 3 jeeps and 2 tanks; the start force begins just west of the ash patch.
- **lb_01 idle:** leave the mission running untouched: the start force shoots the passing convoy and the mission waits for you to deploy (no loss).
- **Art:** every unit and building has a sprite (Authority bomber, carrier, howitzer, Directorate dirigible, ruins...): no grey placeholder boxes.

## Session 22 · Balance round, mop-up AI, dock lanes, audio, wall art (D-223..D-228)
Back up `%APPDATA%\RTS_2027` first (D-220: old development saves and replays may not load). Branch `integ-s22` until its PR lands on main.
- **Music:** the menu plays a track; a skirmish switches between a battle track (when fighting) and a calm track; victory/defeat play once.
- **Voices:** start any campaign mission: the first radio line is spoken (e.g. auth_01 Wrenfield) as well as shown; a mission whose voice file is
  missing still shows the text silently.
- **New radio lines:** exp_03 tier 5: "A second flight of Authority bombers, low over the south-west sea!" with Kell on the radio at ~5:20;
  exp_04: after the Dredge Works, Strand warns "The Directorate army is on the march"; auth_01: the "Raiders from the east road!" ticker is followed by Wrenfield: "Riflemen on the east road. Keep the scrapers behind the line."
- **Ambience:** Settings has an **Ambience volume** slider; in a skirmish nothing changes until the generated ambience loops land (falls back silently).
- **Mop-up AI:** skirmish v a tier 2-5 AI, then wreck its army and leave a few outlying buildings: after about 7-8 minutes of game time the AI
  sends small parties that search the map for your last buildings (it does the same to you). Tier 1 never does.
- **Dead-end dock (Q-76):** wall a refinery or yard dock into a one-cell corridor and send 3+ harvesters: they wait at the corridor mouth and
  each drives in, unloads and out without stopping.
- **Mirror maps:** skirmish Left Behind v Left Behind on Mountains: both bases' units leave their factories and harvesters dock on the side
  facing the centre (mirror images).
- **Balance:** Expedition shipments pay 1400 (was 2000); the Directorate Rocket Truck has no splash; Bulwark 500 HP / 900; Howitzer hits harder;
  the Dirigible moves twice as fast; Left Behind Harpoon wagons and posts shoot aircraft; Refinery Rig 700; tutorial starts with 4500.
- **Wall art:** Authority wall/gate, Directorate bunker wall and Left Behind scrap wall show panels, grime and props in game and in the booklet heroes.
