# qol-cnc: QoL ideas from the C&C lineage and descendants (research, not defects)
Scope read: repo inventory in `inventory.md` (UI/input audit, ROADMAP M11, USER_CHECKS s14-s15, src/Game Controls/Ui/Screens/Editor). Web: OpenRA hotkey wiki, release notes (2019-08, 2020-03, 2022-12 via GamingOnLinux, 2019-03 changelog), OpenRA settings wiki, Remastered preview and EA patch notes, Tempest Rising controls guide and patch notes (2025-12, 2026-03), Phobos (RA2 engine extension) UI docs, Iron Curtain design-doc summary of OpenRA features.
Not covered / weak: Red Alert 2 / Yuri's Revenge vanilla, Tiberian Sun, Generals, RA3 radial commands and planning mode, 9-Bit Armies. Searches returned no usable primary source for them (the Wikipedia pages fetched say nothing about their UI). I list no feature as coming from those games unless a page says so. Generals-style "radial commands" and "planning mode" are therefore NOT in the table. The OpenRA map editor was only read at changelog level. Any item marked "verify" was not confirmed in this repo's code.
Column "Sim?": F = front end only (good), S = touches the deterministic sim / command stream / replays (costly), Setup = only the setup record fed to the sim (cheap but changes the match start, so replay and save format versions need care).
Value 1 (meh) to 5 (do soon). "Has" cites `inventory.md` rows.

## Camera
| # | Feature | Who / how | Source | RTS_2027 has? | Why for this game | Size / Sim | Val |
|---|---|---|---|---|---|---|---|
| 1 | Jump to selected units | OpenRA: Home key centres the camera on the current selection. | https://github.com/OpenRA/OpenRA/wiki/Hotkeys | No (Home = base only) | After selecting a control group from the minimap or card you often want to see them; the sidebar game has no other way except double-tap of a group number. | S / F | 2 |
| 2 | Jump to next base / next production building | OpenRA: H cycles through bases; Tab cycles the production structures tied to the sidebar tab. | same + https://www.openra.net/news/playtest-20200303/ | Partly (Home only; Tab is taken by objectives) | Four factions with unusual bases (Left Behind rigs move, Expedition ships). A "next factory" key is the quick way back to the right production building, and it also selects it so Sell/Repair/Rally work. | S / F | 4 |
| 3 | Zoom hotkeys | OpenRA playtest 20200303: [ and ] zoom the battlefield. Remastered: zoom between old and new pixel scales. | https://www.openra.net/news/playtest-20200303/ ; https://www.pcgamesn.com/command-and-conquer-remastered/gameplay-preview | Partly (wheel only, M11-06) | Laptop and touchpad players have no wheel; trivial to add as two rebindable actions. | S / F | 2 |
| 4 | Jump to map edges | OpenRA: Alt+arrows jump to the edge of the map. | OpenRA Hotkeys wiki above | No | Marginal; big generated maps are 96 cells. | S / F | 1 |

## Selection
| # | Feature | Who / how | Source | Has? | Why | Size / Sim | Val |
|---|---|---|---|---|---|---|---|
| 5 | Select / cycle idle harvesters | OpenRA: N selects and cycles harvesters. Phobos: "next idle harvester" selects and centres on it. | https://www.openra.net/news/playtest-20200303/ ; https://phobos.readthedocs.io/en/latest/User-Interface.html | No. C (select army) deliberately skips harvesters (inventory, Selection) so there is no fast way to a lost Scraper. | Harvester care is the weakest part of C&C-style play and the economy is the whole game; the Scraper auto-return is manual-ish (right-click field). Add "N = next harvester, idle first", centre camera. | S / F | 5 |
| 6 | Select by class | Tempest Rising: Shift/Ctrl/Alt with Q or W selects infantry / vehicles / air, all or on-screen. | https://primagames.com/?p=344714 | Partly (C = all combat units, double-click = same type on screen) | Infantry vs vehicles split is the usual need (send tanks, keep engineers). Could be two actions "select all vehicles / infantry". | S / F | 3 |
| 7 | Same type across the whole map | OpenRA: click a unit then double-tap W to select that type everywhere (single W = on screen). | OpenRA Hotkeys wiki | Partly (on screen only) | Cheap extension of an existing feature, helps with spread defenders on 96-cell maps. | S / F | 2 |
| 8 | Low selection priority in box select | Phobos: units flagged low priority (harvesters, builders) are dropped from a box when combat units are also inside; OpenRA Q picks "highest priority units". | https://phobos.readthedocs.io/en/latest/User-Interface.html | Partly (box select takes all own units incl. Scrapers) | Drag-selecting a battle drags the Scrapers into the attack-move. A data flag per unit type. | S / F | 4 |
| 9 | Cycle through selected units / types | Phobos: cycle selection one unit or one type at a time; Tempest: Tab / \ cycles the primary selection by type. | Phobos doc ; Tempest guide above | No | Needed to use a unit's ability (Deploy, Pack) in a mixed group; the card shows the group but a keyboard cycle is faster. | S / F | 2 |
| 10 | Send selection into a group (Alt+number moves) and deselect 5 | Tempest: Alt+number moves selection into a group; Phobos: deselect one or five. | Tempest guide ; Phobos doc | Partly (Ctrl set, Shift add, Shift-click drop) | Small refinement; low value. | S / F | 1 |

## Orders
| # | Feature | Who / how | Source | Has? | Why | Size / Sim | Val |
|---|---|---|---|---|---|---|---|
| 11 | Waypoint queue with Shift | OpenRA and Red Alert: hold Shift while ordering to chain move / attack-move orders, with a planning line drawn. | https://www.openra.net/news/playtest-20190825/ | No (audit O3, marked "L, sim, later") | Escort and harassment routes, and getting around the cliffs on the generated maps. | L / S (needs a per-unit order queue, new command flag, replay change) | 4 |
| 12 | Queued rally-point path | OpenRA 20200303: Shift+click on rally sets several rally waypoints; they stay hidden until set. | https://www.openra.net/news/playtest-20200303/ | No (single rally cell) | Lets freshly built units walk a safe route around the base; needs the same queue as #11 but at one building. | M / S | 2 |
| 13 | Stances (hold fire / return fire / defend / attack anything) and hold-position | OpenRA: Alt+F/D/S/A set stance, V hold position, and a unit command bar with stance buttons; stance colours toggle. | OpenRA Hotkeys wiki ; https://games.softpedia.com/progChangelog/OpenRA-Changelog-75191.html | No: `CommandKind.SetStance` exists (Command.cs:10) but nothing in src/Sim or UI uses it | Fixes the classic C&C problem of infantry chasing a bait unit out of position and of scouts that fight when they should flee. The slot in the command enum is already reserved. | L / S | 4 |
| 14 | Assault move | OpenRA: Ctrl+A is attack-move that also stops to shoot structures and everything, as opposed to normal attack-move. | OpenRA Hotkeys wiki ; softpedia changelog (20171014 added it) | No | Useful only if the current A-move already skips some targets; check `OrderController`/sim first. | M / S | 2 |
| 15 | Force fire / force move modifiers | OpenRA: Ctrl+right-click force-fire (ground, allies), Alt+right-click force-move (no attack). | OpenRA Hotkeys wiki | No (verify) | Artillery (Ashthrower, siege units) cannot hit a cell or a wall without it; force move saves a tank walking into a crowd. | M / S | 3 |
| 16 | Patrol | Tempest: F patrol between two points. | https://primagames.com/?p=344714 | No | Cheap lookout role, AI already guards. Needs the queue (#11) or a mission type. | M / S | 2 |
| 17 | Hold the key for Stop / Guard | Remastered patch: Stop and Guard hotkeys can be held. | https://www.ea.com/ea-play/news/patch-launch | Unclear | Tiny. | S / F | 1 |

## Production / economy
| # | Feature | Who / how | Source | Has? | Why | Size / Sim | Val |
|---|---|---|---|---|---|---|---|
| 18 | Hotkeys per cameo slot | OpenRA: F1-F12 pick sidebar slots, Ctrl+F1-12 second set. Tempest: F1-F12 pick build options, Shift+F1-F10 queue five. | OpenRA Hotkeys wiki ; Tempest guide | No (only the tab keys) | The sidebar is the heart of this game and today every build needs a click on a 40 px cameo. With tabs on Q W E R T the grid slot keys make keyboard-only building possible. | M / F (a slot-key table; rebindable) | 5 |
| 19 | Sell / Repair / Power-down keys | OpenRA: Z sell, C repair, X power mode; Tempest: O sell, I repair, P toggle power. | OpenRA Hotkeys wiki ; Tempest guide | Partly: buttons on the building card (inventory), no keys | Sell/repair under pressure; keys are an S job. Power toggle also needs the sim (CommandKind.PowerToggle is reserved, not implemented) so that part is its own item (#20). | S / F | 3 |
| 20 | Power-down a building | OpenRA and Tempest: toggle a building's power so it stops drawing (turns off defences, radar...). | OpenRA Hotkeys wiki ; Tempest guide | No (command reserved, unimplemented) | Gives the low-power state a player answer other than selling; fits the Left Behind / power-heavy factions. A balance-visible change. | M / S | 3 |
| 21 | Place last built structure again | Tempest: N places the last constructed structure type. | https://primagames.com/?p=344714 | No | Walls, defences, power plants: repeated builds. In this game production-then-place is two steps; a "queue and place again" is a good fit. | S / F (reuses build + placement) | 3 |
| 22 | Queue at front of line | OpenRA 20190314: holding Ctrl puts the unit at the front of the production queue. Also middle-click cancels one or all of a type. | https://games.softpedia.com/progChangelog/OpenRA-Changelog-75191.html ; OpenRA Hotkeys wiki | No (hold and cancel exist) | You can already hold the front item; "front" would let a needed tank jump a queue of 5 riflemen. Reorders the sim queue. | M / S | 2 |
| 23 | Global production queue panel | Tempest (Dec 2025): one window lists all active production; cancel, pause, duplicate, clear-all. March 2026: sort by progress and pin an item. | https://support.saber.games/hc/en/tempest-rising/articles/december-2-2025-patch-notes-17 ; https://support.saber.games/hc/en/tempest-rising/articles/march-10-2026-patch-notes-21 | Partly (per-lane count badge, hold, cancel; audit P5 queue strip not done) | The sidebar shows only the tab you are on. A small strip (icon, progress bar, hold/cancel) for all lanes saves tab-hopping, and is the right place for READY cameos. | M / F | 4 |
| 24 | Harvester count, power delta, income per minute at the credits line | Phobos: counters for active/total harvesters, power surplus, and income gained shown next to credits. | https://phobos.readthedocs.io/en/latest/User-Interface.html | Partly (power in/out exists; no harvester count or income) | Cheap economic awareness: "2/3 harvesters", "+420/min". Income needs a small sim query (read-only) or a tracker; the spectator IncomeTracker exists. | S / F | 4 |
| 25 | Cameo list stays on factory selection | Phobos: switch the sidebar tab when a factory is selected. | Phobos doc | No (verify) | Click a Barracks, the Infantry tab opens. | S / F | 3 |
| 26 | Extended tooltip details for power plants | Tempest (March 2026): power-providing structures show supply in the production tooltip. | March 2026 patch notes above | Yes (power in tooltip, s15) | Already done. | - | - |

## Information / feedback
| # | Feature | Who / how | Source | Has? | Why | Size / Sim | Val |
|---|---|---|---|---|---|---|---|
| 27 | Target lines | OpenRA: a line from selected units to their target; allies' and spectators' lines too; a setting turns it down. | https://www.openra.net/news/playtest-20190825/ ; softpedia changelog | Partly (ring at click point, rally line; no live lines) | Shows what an attack-moving group is actually shooting, and what the AI ally is doing. | S-M / F | 3 |
| 28 | Range circles on hover / selection / placement | Phobos: draws range circles for superweapon designators and for power enhancer when placing. OpenRA: same for support powers. | Phobos doc | Debug only (F-key, sandbox) | Defensive structures and artillery: show reach of a selected tower while placing it, and splash radius while aiming Salvo Array. | M / F | 3 |
| 29 | Relationship colours | OpenRA Settings > Display: override player colours so own = one colour, allies = another, enemies = another. | https://github.com/OpenRA/OpenRA/wiki/FAQ | No (minimap uses player colours, audit F3/A2 mention red-green issues) | Helps colour-blind players and 4-player free-for-all where two colours can be close. | S / F | 3 |
| 30 | Hover tooltip on any unit or structure | OpenRA, Remastered: hovering shows name, owner and state; Phobos adds numeric health. | Phobos doc ; FAQ above | No hover tooltip (audit F2 noted no hover highlight; cursors came in s15) | Lets you read enemy units without needing an Archive trip. Enemy click-info was marked optional in the audit (S5). | M / F | 3 |
| 31 | Game clock and timers | Phobos: elapsed time and real-time timers on screen. | Phobos doc | Only behind Diagnostics | Wave timers in missions ("Second raid m:ss") exist; a small match clock is polite. | S / F | 2 |
| 32 | In-game encyclopedia with live stats | Tempest: Codex page for units and structures, switch between campaign and multiplayer balance. Dune 2000 in OpenRA got an encyclopedia. | https://support.saber.games/hc/en/tempest-rising/articles/december-2-2025-patch-notes-17 ; https://www.gamingonlinux.com/2022/12/openra-for-classic-command-a-conquer-dune-2000-red-alert-has-a-new-playtest/page=1/ | Partly (Archive is lore; cameo tooltips have stats) | Four asymmetric factions, 62 buildings: a "Codex" built from the rules TOML (never stale) answering "what counters this?" | M / F | 3 |

## Alerts
| # | Feature | Who / how | Source | Has? | Why | Size / Sim | Val |
|---|---|---|---|---|---|---|---|
| 33 | More alert kinds: enemy spotted, building captured, silo full / no harvester idle, research done, factory idle | OpenRA lists unit lost, enemy spotted, under attack, building captured as notifications. | https://iron-curtain-engine.github.io/iron-curtain-design-docs/openra-features/player-game-state.html | Partly (AlertKind has 10 kinds, none of these) | "Factory idle" and "harvester idle" prevent the most common slow-play losses. Derive from snapshot, no sim change. | S-M / F | 4 |
| 34 | Quieter, de-duplicated notifications; better for no-sound play | OpenRA 2022: fewer overlapping alert sounds; notifications improved for players without sound. | https://www.gamingonlinux.com/2022/12/openra-for-classic-command-a-conquer-dune-2000-red-alert-has-a-new-playtest/page=1/ | Partly (alert stack exists; verify rate limiting) | Check that 20 simultaneous "unit lost" do not stack. | S / F | 2 |
| 35 | Out-of-focus audio setting | Tempest March 2026: mute or keep playing when window is inactive. | March 2026 patch notes | No | Easy, wanted on a laptop that alt-tabs. | S / F | 2 |

## Replays / observer
| # | Feature | Who / how | Source | Has? | Why | Size / Sim | Val |
|---|---|---|---|---|---|---|---|
| 36 | Observer Army tab, Production tab and Army-value graph | OpenRA: spectator panels with each player's units, what they are building, army value and graph. | https://www.openra.net/news/playtest-20200303/ ; softpedia changelog | Partly (spectator table, income tracker, detail panel) | You watch AI-vs-AI games and replays to balance. An army composition table + value graph is the best balance telescope. | M / F | 4 |
| 37 | Replay browser with metadata, sort, filter | OpenRA: players, map, version, duration, outcome. | Iron Curtain doc above | Partly (LoadGameScreen.CreateReplays, metadata not verified) | Replays pile up from `ai commander` runs. | S-M / F | 3 |
| 38 | Replay seek bar and progress tooltip | OpenRA: tooltip shows game progress; speed keys. | softpedia changelog | Partly (F = +10 s, R restart) | A click-to-seek bar (re-simulates from start, deterministic) for long replays. | M / F | 3 |
| 39 | Post-game stats screen (score, economy graph) | Iron Curtain doc lists periodic score snapshots as the intended OpenRA-like addition. Remastered shows a score screen. | Iron Curtain doc ; EA patch notes | Unverified (GameOverScreen not read) | Gives the skirmish an ending beat; shares the data with #36. | M / F | 3 |

## Settings / accessibility
| # | Feature | Who / how | Source | Has? | Why | Size / Sim | Val |
|---|---|---|---|---|---|---|---|
| 40 | Autosave + Quickload hotkey | OpenRA: AutoSaveInterval and keep last N files. Remastered: Quickload hotkey, quicksave also in custom missions. | https://github.com/OpenRA/OpenRA/wiki/Settings-(playtest) ; https://www.ea.com/ea-play/news/patch-launch | Partly: Quicksave in the pause menu only; no autosave found | Missions are long (4 acts, 16 missions) and a hobby build crashes; this is the cheapest insurance. | S-M / F (uses existing save writer) | 5 |
| 41 | Vsync, frame cap, borderless, monitor | OpenRA Settings: video mode, VSync, CapFramerate, display index. | OpenRA settings wiki above | No (fullscreen toggle only) | S. | S / F | 2 |
| 42 | First-run intro / default zoom ("Battlefield Camera") | OpenRA 20200303: Battlefield Camera sets default zoom; first-run screen covers key options. | https://www.openra.net/news/playtest-20200303/ | Partly (zoom exists; no default-zoom setting or first-run) | A first-run screen asking UI scale + edge scroll + key set would stop the audit's "can't read it" complaints. | S / F | 2 |
| 43 | Classic vs modern mouse scheme | Remastered, OpenRA: choose classic (left-click commands) or modern (right-click commands). | https://www.pcgamesn.com/command-and-conquer-remastered/gameplay-preview ; FAQ | No | The owner prefers modern and has it. Low value. | M / F | 1 |
| 44 | Effects toggles (screen shake, flashes, laser trails) | Phobos: turn off flashes, shake, trails. | Phobos doc | No (verify what effects exist) | Accessibility; the new FX in s15 add flashes. | S / F | 2 |
| 45 | Colour-blind safe mode | Not a C&C feature (SC2 does it; covered by `qol-modern`). Listed only for audit F3/A2 which are still open here. | UI audit F3 | No | See #29. | - | - |

## Skirmish setup / lobby
| # | Feature | Who / how | Source | Has? | Why | Size / Sim | Val |
|---|---|---|---|---|---|---|---|
| 46 | Lobby rules: starting cash, game speed, shroud/fog, tech level, short game, resource regen | OpenRA lobby options: game speed, tech level, starting cash, fog, shroud, short game. Remastered patch added a resource regeneration slider. | Iron Curtain doc ; https://www.ea.com/ea-play/news/patch-launch | No (SkirmishSettings has none) | The cheapest way to make skirmish replayable and teach the game (start rich for a base-building toy; low cash for rush practice). Starting cash/tech go into SimSetup. Fog switch is front end only; game speed is a host setting. | M / Setup (record versions) | 4 |
| 47 | Player and AI handicap | C&C3 skirmish offers AI behaviour presets, difficulty and handicap for AI, player or both. | https://en.wikipedia.org/wiki/Command_%26_Conquer_3:_Tiberium_Wars | Partly (AI tiers 1-5 only; no human handicap) | Mixed-skill LAN games; income/build-speed multiplier per slot. | M / S (rules multiplier) | 3 |
| 48 | Superweapon setting: none / one per player / unlimited | Tempest Dec 2025: Skirmish and Custom limit superweapons. | https://support.saber.games/hc/en/tempest-rising/articles/december-2-2025-patch-notes-17 | No (Salvo Array exists) | Lets people play without the finisher. | S / Setup | 3 |
| 49 | Random faction pick | Tempest: Random in faction selection, player list shows faction. | https://support.saber.games/hc/en/tempest-rising/articles/march-10-2026-patch-notes-21 | Unknown (verify) | Four factions to learn. | S / F | 2 |
| 50 | Map download / hash check for custom maps in multiplayer | Remastered patch: Download button for custom maps in lobby. | https://www.ea.com/ea-play/news/patch-launch | N/A: could not find a LAN layer to check | If LAN play uses user maps, the sender must ship the map file with a hash. Verify first. | M / F | 2 |

## Campaign / mission UI
| # | Feature | Who / how | Source | Has? | Why | Size / Sim | Val |
|---|---|---|---|---|---|---|---|
| 51 | Retry / restart in mission | Remastered: Retry Mission dialog on defeat, also in custom missions. | https://www.ea.com/ea-play/news/patch-launch | Yes (Defeat menu: Restart) | Done. | - | - |
| 52 | Co-op AI ally you can order | Red Alert 3 campaigns: an AI partner shares income and takes simple orders (take a position, strike a target). | https://en.wikipedia.org/wiki/Command_%26_Conquer:_Red_Alert_3 | No (commander directives exist, D-listed M5) | The LLM commander idea fits; needs missions to be co-op-aware. | L / S | 2 |

## Mapping / editor
| # | Feature | Who / how | Source | Has? | Why | Size / Sim | Val |
|---|---|---|---|---|---|---|---|
| 53 | Copy / paste regions | OpenRA 20190314: Copy/Paste tool with a hotkey; multi-category filter on the tile selector. | https://games.softpedia.com/progChangelog/OpenRA-Changelog-75191.html | No (symmetry brushes only) | Build one base, paste it for each start. | M / F | 3 |
| 54 | Per-object property panel | OpenRA 20190314: actor property editor (owner, facing, trigger name...). | softpedia changelog | Partly (Feature tool; no units/triggers: verify) | Needed for hand-made missions, but missions are TOML-authored. | M / F | 2 |

## Ranked top 15
1. **#40 Autosave + Quickload key (5).** Missions run 20 minutes or more and each playtest has hit crashes (the pause menu quicksave is the only recovery). Autosave every N minutes into rotating slots using the existing save writer, F9/Quickload hotkey, "Continue" on the main menu. Pure front end.
2. **#18 Per-slot build hotkeys (5).** Tabs have keys, but a build still needs a pointer travel. Add slot keys 1-0 / QWERTY row inside the active tab, rebindable. Biggest speed gain for a sidebar game.
3. **#5 Next harvester / idle harvester (5).** The one unit C and box-select deliberately avoid, and the one the economy depends on. A single key (N) cycles harvesters, idle first, selecting and centring.
4. **#2 Next production building (4).** Selects and centres the factory of the active tab, so rally/sell/repair can follow.
5. **#33 More alert types (4).** Factory idle, harvester idle/lost, enemy spotted at base edge, building captured, shipment arrived. Derived from the snapshot like the existing alerts.
6. **#24 Economy counters (4).** "Harvesters 2/3" and income per minute next to credits and power.
7. **#23 Global production strip (4).** One row of all lanes with progress, hold, cancel; the READY cameo can be placed from it.
8. **#46 Skirmish rules (4).** Starting credits, fog on/off, game speed, superweapon limit (#48), "short game". Setup-only, big replay value.
9. **#36 Observer army tab and graph (4).** For watching AI games and balance work.
10. **#13 Stances + hold position (4, L).** Reserved enum value; costs sim work and AI awareness.
11. **#11 Shift waypoints (4, L).** Known audit item; still the biggest missing order feature.
12. **#8 Low selection priority (4).** Box select skips Scrapers/builders unless nothing else is in the box.
13. **#21 Place last structure again (3).** Quick wall and defence building.
14. **#27 Target lines (3).** Shows what groups are attacking.
15. **#29 Relationship colours (3).** Own / ally / enemy palette setting, tied to the open colour-blind items.

## Wireframes and exact behaviour for the top 8
Sidebar reference: current layout is minimap at top, credits and "POWER out/use" under it, tab row (Q W E R T Y), the cameo grid, then selection card at the bottom centre of the map view.

**1. Autosave + Quickload (#40).** No new HUD. Settings > Game: "Autosave every [off / 3 / 5 / 10 min]" and "Autosaves kept [3 / 5 / 10]". Alert-stack line "Autosaved" for 2 s. Main menu: "Continue" at the top loads the newest save (any kind). Quickload = F9 (rebindable), Quicksave = F8 (the pause menu already offers it); load-confirm only if the current game has unsaved play of more than 30 s.

**2. Slot hotkeys (#18).**
```
[Q:STRUCT*][W][E][R][T][Y]      active tab = STRUCT
+--------+--------+--------+
|1 Power |2 Refin.|3 Barr. |   key chip, top-left of each cameo
|  $300  |  $500  |  $400  |   (chip uses the player's current binding)
+--------+--------+--------+
|4 ...   |5 ...   |6 ...   |
```
Keys 1-6 (first row) and Z X C V B N (second row) fill the visible grid left to right, top to bottom; locked cameos still reserve their slot so positions never move. Shift = queue five, Ctrl = hold (reuse the existing right-click rules). A conflict with control groups (1-9) is real: use F1-F12 as OpenRA and Tempest do, or require the cameo chip mode to be toggled with a "build mode" key. This clash needs an owner decision.

**3. Next harvester (#5).** Key N (not currently bound). First press: select the first idle harvester (sorted by idle time) and centre; repeat cycles; if none is idle, cycles all. Selection card for one Scraper shows "Idle / Hauling / Returning". Credits line gets "Scrapers 2/3" (#24) which flashes yellow when one is idle; clicking it = N.

**4. Next production building (#2).** Tab is taken by objectives; use ` (backquote) or Backspace-free key such as B. Cycles the building types that feed the active tab (Barracks for Infantry), selects and centres the camera. Selection card then shows its rally and queue.

**5. Alerts (#33).** Same stack top-right as today:
```
                      +-------------------------+
                      | ! Factory idle: Barracks|  click or End = jump
                      | ! Scraper idle (2)      |
                      | ! Enemy spotted  NE     |
                      +-------------------------+
```
Lines merge by kind within 5 s ("Scraper idle (2)"). Idle alerts fire after 20 s of a lane being empty / harvester idle with nothing to do, and repeat at most every 60 s. Each has a minimap ping in a kind colour.

**6. Economy counters (#24).** Under the minimap:
```
$ 4,120   +380/min
Scrapers 2/3     POWER 120/150
```
Income is a rolling 60 s average of refinery deliveries (the spectator IncomeTracker already does this). Scraper count blinks if any is idle or lost.

**7. Global production strip (#23).** One slim bar between the tab row and the grid (or under the minimap on short windows), one icon per active lane:
```
[I Rifle 62%][V Tank 14% ][S Power READY]
```
Left click on a lane icon switches to its tab, on READY starts placement; right click = hold / cancel (the same two-step as now). Max 6 lanes, so it is a fixed-height row and nothing in the grid moves.

**8. Skirmish rules (#46, #48).** Under the slot list on the setup screen, one collapsible "Rules" panel:
```
Rules:  Starting credits [ 2000 v]   Fog of war [on v]
        Superweapons     [ one each v]  Game speed [normal v]
        Short game (lose when construction yard dies) [ ]
```
Defaults equal today's behaviour; the values are saved in SkirmishSettings and written into SimSetup so saves and replays carry them (needs a setup version bump). Starting credits/superweapon limit touch only the setup record; fog toggles the viewer, not the sim; game speed is a host multiplier.

## Open questions
- Is a LAN layer implemented? I found only the "LAN Game" menu entry and no net code under src/Game. If it exists, #50 and the observer/lobby items become more relevant.
- Hotkey plan: control groups use digits 1-9; slot hotkeys (#18) cannot also use digits. Which layout does the owner want (F-keys, home row, or a "build mode")?
- Does attack-move (A) already attack structures? If yes, #14 (assault move) is moot.
- Is stance reserved for an AI-only use (DirectiveBias mentions stance)? The enum exists; no handler found in src/Sim.
- Autosave scope: skirmish, campaign or both?

## Things checked and found sound
- Everything in the M11 list (edge scroll, groups, alerts, selection card, objective guidance, cursor per action, placement ghost, tooltips, hold, rebinding, UI scale) really is covered in the code and USER_CHECKS; I did not propose any of them.
- #26 and #51 are included only to show they are already done.
- Not found in any source I could open: radial commands, planning mode, beacons, side-bar vs bottom-bar comparisons for Generals/RA3. They are untested ideas here, not recommendations. OpenRA's `B` beacon appears only as a hotkey list entry, so a map-ping for LAN play is a possible idea but not described further.
