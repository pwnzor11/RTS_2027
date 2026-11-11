# Session 18 adversarial audit · 2026-11-11

Owner brief: deep dive into the code (bugs, issues, optimisation), adversarial UX/UI review, every faction's units, buildings and
upgrades (conflicts, broken items, imbalances), QoL ideas from other RTS games, then the game booklet. Owner rules for this session:
clear bugs outside match outcomes are fixed now with tests; anything that changes how a match plays, any number, any player-visible
wording or design choice comes to the owner first.

**Method.** Wave 1: 18 read-only reviewers (code by subsystem, faction data by hand and by script, live play of menus and matches under
a virtual display, text and onboarding, two QoL research passes). Wave 2: 6 helpers proved or refuted the findings with tests on the
real data, fixed the clear bugs, measured balance suspects with `ai duel` / `ai match`, ran a start-skew experiment, and wrote a laptop
script. Everything a reviewer claimed and a test could check was checked; findings that only a reading supports say so.
Files: `wave1/<package>.md` (findings), `wave2/<package>.md` (verdicts, fixes, measurements), `img/` (live screenshots).
Rosters for all four factions: `wave1/roster_*.md`.

## 1. Fixed in this session (PR from `integ-s18`)
About 45 findings, each with a test that failed on the old code (or, where noted in wave 2, a compile-only failure). No sim rule,
balance number or pinned hash changed, apart from the mission-script and test changes the owner approved for MIS-03.
- **Campaign and missions** (wave2/fix-missions.md): granted aircraft can be built in tech-restricted missions (dir_04, exp_03, exp_04,
  lb_04); auth_03 and lb_03 losses reach the Defeat screen instead of a frozen match; razing a mission's last enemy building no longer
  ends it without progress; a campaign mission loaded from a save keeps its campaign wiring (progress, debrief, tier); a dying unit no
  longer counts as alive inside its own destroyed callback (the auth_01 soft-lock); the Archive no longer relocks after a campaign
  win; the Campaign menu refreshes after a win and its lock hint names the right faction; unreadable progress and settings files are
  kept as `.bad`, writes are atomic, a failed save is reported; a full reinforcement zone overflows instead of aborting the script.
- **In-match UI** (wave2/fix-ui.md): the structure-lane soft-lock (prerequisite lost while building; screenshot `img/33_*`); right-click
  on allies; zoom to cursor; mission dialogue no longer under the selection card (`img/111_*`); tiny drags count as clicks; minimap
  attack-move; rebound keys shown; second game of a session no longer replays build-ups; per-sheet sprite loading; a log file under
  AppData; UI sounds while paused; placement ghost names units; end table income and columns; chat Backspace repeat; Ctrl+n with a
  building selected; unpack hint overlap; per-frame allocation fixes; a CI test listing the 26 ids without sprites.
- **Menus, setup, tools, repo** (wave2/fix-screens.md): main menu and skirmish setup fit at UI 200 % + extra-large text (`img/m03_*`);
  fixed slot label column and constant panel width (`img/k08_*`); Back buttons, help rows, map editor text fit; data errors show a
  Notice instead of crashing; hand-made skirmish maps are validated; the LLM API key is stored per provider and never sent over plain
  http except to localhost; `ai timeline` plays the same map as `ai match` (M12-12b, timeline half); the About screen lists every
  credit in a published build; test static-state collection, honest probe skips, CI timeouts and permissions, README status, stray file.

## 2. Verified, waiting for the owner (tests pin today's behaviour; a skipped twin holds the wanted behaviour)
Recommendation in **bold**. "Moves hashes" = goldens and AI baselines are re-pinned once in the fix batch.

### 2.1 Game-breaking (sim)
| # | What is wrong (evidence) | Options | Rec. |
|---|---|---|---|
| 1 | **Expedition on Mountains layout 1 (the default) is broken on 80-98 % of maps**: no Command Ship (sea beyond the ring-16 fallback) or one on an inland lake that tenders never reach. Sweep of 50 seeds x 3 sizes x 2 ocean settings (wave2/v-ai-map.md). Session 17's "fixed" covered sea 9-16 cells only. | (a) generator guarantees edge-connected sea near each start, with a whole-map SkirmishStart fallback; (b) fallback only; (c) block Expedition on Mountains L1 | **(a)** |
| 2 | **An Expedition player with no building is never defeated**; the game never ends (sunk Command Ship, or no ship spawned). A second Command Ship with no buildings is defeated at once. | count the founder / start kit as "had a base" | **yes** |
| 3 | **Supply tenders always spawn top-left of the map edge**, so the north-west start gets its shipments first (Glass Coast 12 v 1 by start; start 1 had 0 shipment credits at 3 min). Switch measured: shipment credits become equal. | (a) edge cell nearest the dock; (b) rotate among the k nearest; (c) keep | **(a)** |
| 4 | **Garrisoned soldiers obey orders and walk out invisible and untargetable**; box-select, select-army and a click on the building pick them; a soldier ordered to Enter stays selected. | (a) sim ignores orders to carried units + selection skips them; (b) Move = leave then move | **(a)** |
| 5 | **20 units attacking a building stall all pathing for 22.6 s** (measured: 1.9 M A* expansions over 21 overdrawn batches; a bystander's move waited 4.2 s). | retarget a blocked goal to the nearest passable cell + cap one tick's path work; one re-pin batch | **yes** |
| 6 | **A finished structure that cannot be placed blocks its lane for the match** (no cancel; Cancel at slot 0 cancels the item behind it instead). The AI hits it: both Expedition AIs on Glass Coast seed 4 stop at 2 buildings. | allow cancel with full refund (no hash change) + AI cancels after N failed site searches | **yes** |
| 7 | **Full 511-slot building store** (shared by all players, walls count): a deploying Bore / land-train / depot ship vanishes; placement fails silently. | (a) refuse the deploy, `StoreFull` placement reason; (b) raise the limit (every hash) | **(a)** now |
| 8 | **Authority Atomic Battery can never fire** (needs an own tunnel entrance within 8; no Authority building is one). Same fix as D-132(4) for the Left Behind. | `superweapon_entrance_range` 8 -> 0 | **0** |
| 9 | **Left Behind structure lanes keep building while the Mother Rig is packed** (D-102 says a packed rig cannot produce; the test fixture used a value TOML cannot express). | pause Left Behind lanes only / every faction without a conyard | **LB only** |
| 10 | **Campaign tech lists leave out core units**: Authority never gets Rocketeer, Scraper, Silo; Directorate never gets Bunk Gallery (supply stuck at 20/40, never rebuilt); Left Behind never gets Still Rig or Hauler (supply 20 all campaign). | add them to the allow-lists (exact list in wave1/faction-ad.md FAD-04, faction-le.md LE-09) | **yes** |
| 11 | **Start-side tie-breaks**: start kits are translations, not mirror images, on mirrored maps; AI placement ties go north-west. The placement switch alone took Mountains Directorate 3 v 11 to 7 v 6 (16 games, suggestive). | apply both after the laptop series confirms | **laptop first** |

### 2.2 Mechanics (sim)
| # | What is wrong | Rec. |
|---|---|---|
| 12 | Splash ignores air/ground (flak hurts its own base; artillery hurts aircraft overhead); damage-0 sprays deal 1 per hit. Both touch D-048(3). | **respect air/ground; 0 means 0** |
| 13 | Splash hurts passengers in transports (and, by code reading, docked aircraft and tunnel transit); every other area effect spares them. | **spare them** |
| 14 | A dived missile sub fires and no Authority / Directorate / Left Behind weapon or sonar can touch it (D-143); a surfaced sub keeps "submerged" armour and takes 1 damage from all 16 Expedition anti-ship weapons. | **firing reveals the sub for a few seconds; surfaced armour ship_light** |
| 15 | Veteran or aura-buffed stealth units fire without breaking stealth. | **fix** |
| 16 | Deploy on a landing craft / Skyhook unloads everyone at once, skipping `unload_ticks`. | **route craft to the timed unload; refuse for an airborne Skyhook** |
| 17 | A small Left Behind rig is refused unpack sites because the check uses the 3x3 Mother Rig. | **fix** |
| 18 | Hatch -> Bore Head morph charges starting cash only, not harvested credits. | **use funds** |
| 19 | Melee-range units (Sappers) never engage on attack-move or guard. | **acquire within sight and close in** |
| 20 | Harvesters: an unreachable nearer field traps them for good; after a refinery rebuild the old harvester stays idle; Move gives up after 13 blocked waits at chokes (40 tanks: 3 stop; 60: 9). | **fix all three** |
| 21 | Kill value = victim cost, so one tender (6000), Command Ship (6000) or packed rig (4000) gives instant Hero; a packed 200-credit rig leaves a 1000 wreck. | **`buildable = false` flag + real costs** |
| 22 | Group path results leak into the saved/hashed paths part when a member is re-ordered. | **fix** |
| 23 | Ashfall hazard (D-028) is not implemented; rad resistance only matters against weapons. | owner: **implement** or record a decision to drop |
| 24 | No attack-ground order: Decon Truck, Foam Layer, Ash Sower, Dirigible cannot do their jobs without an enemy in range. | owner: **add attack-ground** |
| 25 | Searchlight and Broadcast Mast work at zero power. | **check power** |
| 26 | Promised but missing: Minelayer (no mines), Diver (cannot swim), Drydock (produces/repairs nothing), Destroyer depth charge, Harpoon tether, Grapple breach, five upgrades doing less than their text. | owner: **hide or reword now, build later** |

### 2.3 Numbers (D-152 path; measure after the fixes above)
Agitprop aura 1250 slows its own conscripts (**800**; duel note: it still wins fights as a damage sponge) · Tow Truck 1000 does nothing
(**1500-2000**) · non-Expedition warheads 100 % vs ship armour (fill the three columns) · tier-1 defences outranged by tier-1 units (Tank
v Pillbox, Rocket Truck v Turret, no return fire) · Bulwark = Tank, Marksman dominates Rifleman, Howitzer weaker than the tier-2 mortar,
aircraft at 0.62 cells/s · Reactor Crawler v Land-Train · Commissar has no aura · Refinery Rig dominated by the Yard · repair auras
heal 1 %/10 ticks under fire · Left Behind has no mobile anti-air · Directorate structures copy the Authority's · tutorial cash trap
(Shift x5 riflemen leaves 1900 v a 2000 Refinery) · Recruit AI wiped two openings at 4-5 min (scripted play, needs a hand check) ·
LB yard sell loop · Salvo Array about a quarter of the other superweapons per charge minute.

### 2.4 UI, wording and design
Confirm on Quit to menu / Restart / Overwrite, and Esc on the main menu should not quit (**yes**) · autosave + quicksave/quickload keys
(**yes**) · pause on focus loss in single player (**yes**) · borderless fullscreen (**yes**) · instant Sell on the card (**second click
within 2 s**) · orders accepted while paused (**keep, it is the tactical-pause QoL item**) · edge scroll over the sidebar (**outer 3 px
only**) · picking a commander without a key silently makes the game Spectate (**Notice first**) · hide test_32/test_64 (**`dev = true`
map key**) · control groups saved with the game (**yes**) · speaker ids in dialogue, sidebar names v Archive titles (77-80 differ),
raw map ids, raw mission ids, upgrade effect text (**one naming pass from data/archive**) · Expedition start cue and Anchorage
"must touch the shore" reason (**yes**) · map editor validates only after the first edit (**yes**) · the old single LLM key is kept
only if the saved provider was Anthropic (**as built**) · "Progress not saved" Notice wording (review) · mission script one-liners
MIS-06/08/09/11/13/14/15/19/20 and GS-07 (exact lines in wave2/fix-missions.md: **approve all**).

## 3. QoL ideas from other RTS games (wave1/qol-cnc.md, wave1/qol-modern.md; about 100 candidates, merged)
Front-end only unless marked. Top picks, in order:
1. Game speed for skirmish including slow (0.5x, 0.75x): best single feature for family play.
2. Idle harvester / factory button with a count and hotkey.
3. Keyboard hotkeys for the sidebar cameo grid.
4. Tactical pause that visibly accepts orders (orders are already accepted while paused).
5. Autosave and quickload (see 2.4).
6. Repeat production on a cameo.
7. Weapon range rings in play (the renderer exists, sandbox only today).
8. "Strong vs / weak vs" line in tooltips generated from the warhead tables.
9. More alerts: upgrade done, production idle or blocked, harvester idle.
10. Per-slot handicap and starting conditions in skirmish (setup layer).
11. Stance buttons and shift-queued waypoints (sim: `SetStance` exists in the enum with no handler).
12. Drill missions with medals; in-game tech-tree viewer from the same data the booklet uses.

## 4. Things found sound
No non-determinism inside Step; hash and save coverage strong; the AI reads only the fog-honest player view (D-051) and has no cheats;
rules data has no broken references, cycles or dead ends; every id in AI code, mission lists and Lua resolves; all 155 sprite manifests
match their PNGs; average speed 884 ticks/s with 4 AIs on a Large map; no crash in about 75 minutes of live play; save/load round
trips work; Archive, How to Play and the replay viewer are polished.
