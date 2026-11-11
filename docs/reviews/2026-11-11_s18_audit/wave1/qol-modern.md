FILE: /home/claude/audit/qol-modern/inventory.md was written. findings.md could NOT be written: the Write tool refused it ("subagents should return findings as text"). I did not work around the refusal, so the full findings are below. Please save them as /home/claude/audit/qol-modern/findings.md.
COUNTS: critical 0 / major 0 / minor 0 / idea 46 (research task, no defects).

# qol-modern: QoL ideas from non-C&C RTS games (findings)

Scope read: the repo inventory (see inventory.md) and web pages on StarCraft II and Remastered, AoE II DE and IV, Warcraft III Reforged, Beyond All Reason (BAR), Supreme Commander (GAZ UI mod), Zero-K, Company of Heroes 3, Homeworld 3, Stormgate, They Are Billions and Stardock/Ashes.
Source tags: [R] = I opened the page and the feature is on it. [L] = search listing or general knowledge only, page not read, so treat as unverified. [–] = no source, standard practice.
Pages I could not open or that were irrelevant: CoH3 reverse move, Liquipedia SC2 AI, SupCom controls wiki, Iron Harvest, Dawn of War, Northgard, Battle Aces, Mechabellum, Total Annihilation. Those games contribute nothing.
Not done: I did not play any competitor game, and I did not build or run RTS_2027.

Layer: FE = front end only (cheap, cannot move sim hashes). SIM = new sim state or a new CommandKind (costly). SETUP = touches the sim only via the start-of-match SimSetup.
Size: S < half day, M 1-3 days, L more. Value 1-5 is my judgement for this game.
Owner decisions respected: no colour-blind mode (D-172 (5)); LAN is last priority (D-005), so LAN-only items are marked.

## Inventory headline (details in inventory.md)
Already present, so not proposed:
- Camera: edge scroll, bookmarks, centre base, last-alert key.
- Selection: Shift add, double-click and Ctrl-click same type, select army (C), control groups 1-9.
- Orders: smart right-click, attack-move, Stop, Guard, Scatter, rally flag, cursors per action.
- Selection card, placement ghost, cameo tooltip, Shift+click queues 5, hold/cancel queue.
- Alerts with minimap pings, full key rebinding, UI scale and text size.
- Replay viewer (speed, step, skip 10 s, views), spectator income tracker, objective guidance.
- Archive (reachable from the pause menu), 8-page How to Play, tutorial mission tut_01.
- Skirmish setup with 2-4 slots, AI tiers 1-5, teams, colours, per-slot LLM commander.
Absent:
- Shift-queued waypoints, patrol, line drag, stance UI. CommandKind.SetStance is in the enum, but my grep found no handler under src/Sim besides Command.cs.
- Per-cameo grid hotkeys, repeat queue, reorder queue, templates.
- Player income rate, range rings in play (F10 is sandbox-only), idle button.
- Upgrade-complete and production-idle alerts.
- Game speed for humans (SimHost.SpeedMultiplier is an int >= 1 and only the spectator and replay use it).
- Handicap, starting conditions, remembered setup, autosave (grep found none), medals, hints that adapt.
Unverified: whether orders given while Space-paused run on resume. SimHost.Advance just returns while paused, and I did not run the game.

## Candidate table (46 items)

### Camera
| ID | Feature | Who does it, how | Source | In RTS_2027? | Why it helps here | Size / layer | Value |
|---|---|---|---|---|---|---|---|
| C1 | Zoomed-out strategic view with icons | BAR has a map-overview toggle; Supreme Commander is the origin (zoom out until units become symbols) | [R] https://beyondallreason.info/commands-20 | Partly: wheel zoom exists (M11-06), no icon mode | Large generated maps are hard to read at minimum zoom | M, FE | 3 |
| C2 | Cycle own structures with one key, camera centres each press | SC2 Backspace cycles town halls; Stormgate Backspace likewise; AoE2 DE lets several go-to-building keys share one key | [R] https://liquipedia.net/starcraft2/Hotkeys , https://playstormgate.com/news/stormgate-patch-0-3-0 , https://www.ageofempires.com/news/age-of-empires-ii-definitive-edition-update-81058/ | Partly: Home = base only | Finding a factory otherwise costs a minimap trip. Cycle by building kind | S, FE | 3 |
| C3 | Camera smoothing tiers | Stormgate has three tiers for edge, key and drag scroll | [R] https://playstormgate.com/news/stormgate-patch-0-3-0 | No | Taste only | S, FE | 1 |

### Selection
| ID | Feature | Who does it, how | Source | In RTS_2027? | Why it helps here | Size / layer | Value |
|---|---|---|---|---|---|---|---|
| S1 | **Idle button and key** (idle harvesters, builders, factories) with count | AoE2 DE marks idle villagers and ships with an exclamation pointer; SC2 F1; Stormgate I / Ctrl+I; BAR jumps to next idle builder; GAZ groups idle builders; WC3 2.0 double-tap snaps camera | [R] https://www.ageofempires.com/news/age-of-empires-ii-definitive-edition-update-81058/ , https://liquipedia.net/starcraft2/Hotkeys , https://primagames.com/tips/stormgate-early-access-complete-controls-for-pc , https://beyondallreason.info/commands-20 , https://us.forums.blizzard.com/en/warcraft3/t/warcraft-iii-reforged-patch-notes-version-201/33148 | No (alerts say a harvester is attacked, none says idle) | Stalled harvesters are the commonest silent money leak; C deliberately excludes harvesters, so no one-key way to reach them | S, FE | 5 |
| S2 | Auto control groups (new units of a type join a chosen group) | Stormgate per-unit-type auto groups; BAR auto-groups | [R] https://playstormgate.com/news/stormgate-patch-0-3-0 , https://beyondallreason.info/commands-20 | No | Casual players forget to re-select reinforcements | S-M, FE | 3 |
| S3 | Select all of a type map-wide | BAR offers whole-map and on-screen variants | [R] https://beyondallreason.info/commands-20 | Partly: on screen only | Rally-point armies sit off-screen | S, FE | 2 |
| S4 | Ctrl-click a card cameo selects all of that type | SC2 panel icons; GAZ middle-click avatar | [R] https://liquipedia.net/starcraft2/Hotkeys , https://github.com/IDragonfire/GAZ_UI | Partly: card click = one unit only | "Just the rocketeers" without re-boxing | S, FE | 2 |
| S5 | Steal-add and remove-from-group chords | Stormgate Alt+Shift+digit, Alt+backtick; SC2 Alt+digit | [R] https://playstormgate.com/news/stormgate-patch-0-3-0 | No | Power-user only | S, FE | 1 |
| S6 | Cycle or select production buildings from the keyboard | AoE2 DE: select-all-buildings keys can share a key and cycle | [R] https://www.ageofempires.com/news/age-of-empires-ii-definitive-edition-update-81058/ | No | Pairs with C2 | S, FE | 2 |

### Orders
| ID | Feature | Who does it, how | Source | In RTS_2027? | Why it helps here | Size / layer | Value |
|---|---|---|---|---|---|---|---|
| O1 | Shift-queued waypoints drawn as a line | BAR queues many orders with Shift; SupCom shows the queue as a path | [R] https://beyondallreason.info/commands-20 | No (OrderController has no shift handling; Command has no queue flag) | Route harvesters or raids around danger. Needs a per-unit order queue in the sim | L, SIM | 4 |
| O2 | Line-drag move: hold right button, sketch a line, units spread along it | BAR move line drag and fight line drag | [R] https://beyondallreason.info/commands/move-line-drag | No | Firing line against defences or splash. Front end computes N cells and sends ordinary Moves | M, FE | 3 |
| O3 | Patrol between points | BAR lists patrol with guard and fight | [R] https://beyondallreason.info/commands-20 | No | Cheap base defence for kids. Clean version needs a sim loop; a front-end timer re-issuing Moves is hacky but replay-safe | M-L, SIM (clean) or FE (hack) | 3 |
| O4 | **Stance buttons** (hold fire, return fire, free fire; hold position vs roam) | BAR fire stances and hold / manoeuvre / roam | [R] https://beyondallreason.info/commands-20 | Partly: SetStance in the enum, no handler found | Armies chase targets off cliffs; the card already has an order row | M, SIM | 4 |
| O5 | Reverse move | Company of Heroes | [L] page read covered only pause: https://www.pcgamer.com/company-of-heroes-3s-tactical-pause-lets-you-snatch-control-of-the-battlefield | No | Pays off only with facing-dependent armour, which this game lacks | L, SIM | 1 |
| O6 | Area-effect preview tinting own units green and enemies red | Stormgate | [R] https://playstormgate.com/news/stormgate-patch-0-3-0 | Unknown: SuperweaponTargeting.cs exists, tint not checked | Prevents superweapon friendly fire | S, FE | 2 |
| O7 | Repair-all / area repair | BAR area repair and reclaim | [R] https://beyondallreason.info/commands-20 | Partly: per building or selection | One key after a raid; front end sends N Repair commands | S, FE | 3 |

### Production and economy
| ID | Feature | Who does it, how | Source | In RTS_2027? | Why it helps here | Size / layer | Value |
|---|---|---|---|---|---|---|---|
| P1 | **Grid hotkeys for sidebar cameos** | Stormgate command-card keys (ZXCVBN / ASDFGH) and quick-macro QWERT; BAR grid layout; SC Remastered reworked scheme | [R] https://primagames.com/tips/stormgate-early-access-complete-controls-for-pc , https://news.blizzard.com/pl-pl/article/20726737/feature-preview-hotkeys ; BAR grid [L] https://www.beyondallreason.info/commands-2-0-grid | No (only tab keys Q-Y) | Sidebar production is mouse-only; the 1-3 column grid maps to a key block | S-M, FE | 4 |
| P2 | **Repeat / loop a production queue** | BAR repeat orders; AoE players ask for AoM-style repeat build | [R] https://beyondallreason.info/commands-20 ; [L] https://ageofnotes.com/forums/topic/is-there-a-technical-design-reason-why-age-of-empires-ii-definitive-edition-cannot-have-a-repeat-build-button-like-age-of-mythology | No (Shift+click queues 5 then stops) | Removes the most repetitive clicking. Front end re-sends Build when the queue empties; deterministic because it is a normal command | M, FE (or S-M SIM flag) | 4 |
| P3 | Reorder or insert-at-front in a queue | BAR insert at front; GAZ drag reorder | [R] https://beyondallreason.info/commands-20 , https://github.com/IDragonfire/GAZ_UI | No | "Engineer now" without cancelling five tanks | M, SIM | 2 |
| P4 | Saved templates (BAR blueprints, GAZ factory queue templates) | BAR blueprint widget; GAZ | [R] https://redlib.ssps.io/r/beyondallreason/comments/1emft47/introducing_the_blueprint_widget , https://github.com/IDragonfire/GAZ_UI | No | Structure templates fit poorly (build then place one); factory-queue templates fit well | M, FE | 2 |
| P5 | Player income and spend rate | GAZ economy display with warning levels | [R] https://github.com/IDragonfire/GAZ_UI | Partly: spectator panel only (IncomeTracker) | Shows whether a second refinery paid off; cheap reuse | S, FE | 3 |
| P6 | Supply tooltip splitting workers and army | SC2 3.6.0 | [R] https://news.blizzard.com/en-us/article/20273794/starcraft-ii-legacy-of-the-void-3-6-0-patch-notes | Partly: supply counter exists | Tiny tooltip | S, FE | 2 |
| P7 | Production-blocked / idle-factory marker | AoE2 DE: global queue icons for pop-capped units glow red | [R] https://ageofempires.com/news/age-of-empires-ii-definitive-edition-update-87863 | Partly: cameo shows block reason, no tab marker | Red dot on a tab when a queue is stuck on supply or power | S, FE | 3 |
| P8 | Drag-line placement of walls, defences | AoE2 DE Shift-click farm ring; BAR lines and grids | [R] https://www.ageofempires.com/news/age-of-empires-ii-definitive-edition-update-111772 , https://beyondallreason.info/commands-20 | Unknown (single-cell placement as far as I read) | Faster perimeters, only if wall segments exist | M, FE | 2 |

### Information and feedback
| ID | Feature | Who does it, how | Source | In RTS_2027? | Why it helps here | Size / layer | Value |
|---|---|---|---|---|---|---|---|
| I1 | **Weapon range rings in play** on selected units, hovered enemy defences, placement | GAZ rings on mouse-over; Stormgate indicators follow range upgrades | [R] https://github.com/IDragonfire/GAZ_UI , https://playstormgate.com/news/stormgate-patch-0-3-0 | No (F10 sandbox-only, GameKeys.cs:70) | Four factions with different ranges; Render/RangeOverlay.cs already exists | S-M, FE | 4 |
| I2 | **Matchup line in tooltips** ("strong vs / weak vs") computed from warhead and armour data | Ashes II dev journal on combat stats and tooltips | [R, thin] https://www.stardock.com/games/article/541648/ashes-ii-video-blog-combat-stats-and-tooltips (numbers are in a video I could not view) | Partly: tooltip has cost, time, power, description | Hardest thing to learn across four factions; derived from data/rules so it never goes stale | M, FE | 4 |
| I3 | Post-match graphs | SC2 / AoE style | [–] | Partly: final table only (GameOverScreen statsLines) | Useful for balance testing | M, FE | 2 |
| I4 | Ally pings, map drawing | BAR draws and pings with jump-to-ping; Stormgate Alt+click | [R] https://beyondallreason.info/commands-20 , https://primagames.com/tips/stormgate-early-access-complete-controls-for-pc | No | LAN co-op only | M, FE | 2 |
| I5 | Harvester count per field, mission clock | WC3 2.0 goldmine worker count toggle, in-game timer | [R] https://us.forums.blizzard.com/en/warcraft3/t/warcraft-iii-reforged-patch-notes-version-201/33148 | No | Shows abandoned or over-harvested fields | S, FE | 3 |
| I6 | Hide-HUD key, 16:9 HUD clamp | Homeworld 3 | [R] https://www.homeworlduniverse.com/homeworld-3-patch-1-3-update-notes | No | Screenshots, ultrawide | S, FE | 1 |

### Alerts
| ID | Feature | Who does it, how | Source | In RTS_2027? | Why it helps here | Size / layer | Value |
|---|---|---|---|---|---|---|---|
| A1 | Upgrade complete alert with minimap ping | Stormgate 0.3.0 | [R] https://playstormgate.com/news/stormgate-patch-0-3-0 | No (AlertKind lacks it) | Upgrades are a whole tab; finishing silently wastes the benefit | S, FE | 3 |
| A2 | Per-category alert mute and volume | Stormgate mute setting | [R] https://playstormgate.com/news/stormgate-patch-0-3-0 | No | "Unit ready" spam on Shift+5 batches | S, FE | 2 |
| A3 | Wave warning countdown before scripted attacks | They Are Billions (timer not confirmed on the page I read) | [L] https://numantiangames.com/TheyAreBillions/ | No | Fairer campaign defence missions; driven by a script variable | S-M, FE + script | 3 |

### Replays and observer
| ID | Feature | Who does it, how | Source | In RTS_2027? | Why it helps here | Size / layer | Value |
|---|---|---|---|---|---|---|---|
| R1 | Replay event markers (base attacked, building lost), jump to next | Stormgate replays switch perspective and keep the info panel live while paused; markers themselves [–] | [R] https://playstormgate.com/news/stormgate-patch-0-3-0 | Partly: pause, 1-8x, step, skip 10 s, F2 view | Alert stream exists; finding the turning point of a lost game | M, FE | 2 |
| R2 | Replay list search, filters, dates | WC3 2.0 / 2.0.1 | [R] https://us.forums.blizzard.com/en/warcraft3/t/warcraft-iii-reforged-patch-notes-version-201/33148 | Partly: ReplayCatalog | Only if the folder grows | S, FE | 1 |

### Settings and accessibility
| ID | Feature | Who does it, how | Source | In RTS_2027? | Why it helps here | Size / layer | Value |
|---|---|---|---|---|---|---|---|
| X1 | **Game speed in skirmish incl. slow (0.5x, 0.75x) and fast** | BAR game-speed adjustment; AoM Retold slow / standard / fast; Wargame-style 1/10 bullet time | [R] https://beyondallreason.info/commands-20 , https://steamcommunity.com/groups/pausetacticality/discussions/0/2906376154321402758 | No (SimHost.SpeedMultiplier is int >= 1, spectator and replay only) | Best feature for a child or grandparent. Wall-clock scaler only, so sim, replays and saves are untouched. LAN: host sets one speed | S-M, FE | 5 |
| X2 | Two keys per action | Stormgate alternate bindings | [R] https://playstormgate.com/news/stormgate-patch-0-3-0 | No | Keep old and new keys on a shared machine | S-M, FE | 2 |
| X3 | Hotkey presets (Classic, Left-hand, Grid) | SC2 Classic profile; SC Remastered scheme | [R] https://news.blizzard.com/en-us/article/20273794/starcraft-ii-legacy-of-the-void-3-6-0-patch-notes , https://news.blizzard.com/pl-pl/article/20726737/feature-preview-hotkeys | No (only reset to default) | Multiplies P1. qol-cnc may cover a C&C preset | S, FE | 2 |
| X4 | High-contrast HUD mode | AoE IV Strong Contrast | [R] https://www.ageofempires.com/age-iv-accessibility/ | No | Not a colour-blind mode, so it fits D-172 (5) | S-M, FE | 2 |
| X5 | Menu narration (TTS) | AoE IV reads menu text; HUD not narrated | [R] https://www.ageofempires.com/age-iv-accessibility/ | No | Niche, menus only | M, FE | 1 |
| X6 | Reduce flashing and shake | Accessibility guideline | [L] https://gameaccessibilityguidelines.com/ (home page only) | No (SuperweaponFx.cs always shakes) | One-line toggle | S, FE | 2 |
| X7 | Sticky Shift / limited-button play | Stormgate macro-tab cycling key is meant for few-button players | [R] https://playstormgate.com/news/stormgate-patch-0-3-0 | No | One-handed play needs toggles for Shift and Ctrl chords | M, FE | 2 |
| X8 | Auto-pause on window focus loss (single-player) | Common in the genre | [–] | Not found | Phone call, lost base; pause infrastructure exists | S, FE | 3 |
| X9 | Colour-blind palette | AoE IV uses OS filters | [R] https://www.ageofempires.com/age-iv-accessibility/ | Declined by owner (D-172 (5)) | Listed only so you see it was considered; not recommended | n/a | n/a |

### Skirmish setup and lobby
| ID | Feature | Who does it, how | Source | In RTS_2027? | Why it helps here | Size / layer | Value |
|---|---|---|---|---|---|---|---|
| K1 | **Per-slot handicap** (health 50-100 %, optionally income) | WC3 handicap lowers hit points from 50 % to 100 % without touching damage | [R] https://us.forums.blizzard.com/en/warcraft3/t/expanded-handicap-option/5563 | No | A child plays at 75 % against dad; better than picking a lower AI tier. Stored in SimSetup, so replays and saves must carry it | M, SETUP | 4 |
| K2 | Starting conditions (credits, start structures, reveal map, fog) | Custom-game lobby options | [–] | No | Quick matches for kids | M, SETUP | 3 |
| K3 | AI personality picker (builder, rusher, turtle, air) | SC2 AI build styles | [L] general knowledge; Liquipedia blocked | No (data/ai/difficulty.toml only) | A turtle AI is the friendliest opponent and tests defences | M, AI data | 3 |
| K4 | Remember last setup, Random buttons | Standard | [–] | Not found | Many rows to re-enter | S, FE | 2 |
| K5 | Co-op: two humans on one team vs AI | AoE IV Co-op vs AI; SC2 Co-op | [R] https://www.ageofempires.com/news/quickstart-guide-age-of-empires-iv/ , https://news.blizzard.com/en-us/article/20273794/starcraft-ii-legacy-of-the-void-3-6-0-patch-notes | Partly: teams and allied AI exist, humans need M9 | The family use case; only setup defaults after M9 | S after M9 | 3 (LAN only) |
| K6 | Gift credits or units to an ally | AoE2 diplomacy menu; BAR share tools | [R] https://ageofempires.com/news/age-of-empires-ii-definitive-edition-update-87863 | No | Stronger sibling helps the weaker; needs a transfer command | M, SIM | 2 (LAN only) |

### Campaign and mission UI
| ID | Feature | Who does it, how | Source | In RTS_2027? | Why it helps here | Size / layer | Value |
|---|---|---|---|---|---|---|---|
| M1 | Medals per mission (bonus objectives, par time) | AoE IV Art of War bronze / silver / gold | [R] https://www.ageofempires.com/news/quickstart-guide-age-of-empires-iv/ | No | Stars on the mission-select tile add a goal | M, FE + mission data | 3 |
| M2 | Story difficulty (enemy holds back); offered after a failure | AoE IV Story difficulty: no probing, light attacks | [R] https://www.ageofempires.com/age-iv-accessibility/ | Partly: tiers 1-5 | "Try tier 1?" on the fail screen is cheap; a passive tier is script work | S (offer) / M (tier) | 3 |
| M3 | Adaptive hints (re-show a hint and highlight a sidebar tab after 60 s idle) | AoE IV Mission Zero walks through basics | [R] https://www.ageofempires.com/news/quickstart-guide-age-of-empires-iv/ | Partly: static strings in tut_01 locale.toml | Script/API work plus a highlight | M, FE + script | 3 |

### Learning and onboarding
| ID | Feature | Who does it, how | Source | In RTS_2027? | Why it helps here | Size / layer | Value |
|---|---|---|---|---|---|---|---|
| L1 | **Drill missions with medals** | AoE IV Art of War: five scored practice sets | [R] https://www.ageofempires.com/news/quickstart-guide-age-of-empires-iv/ | No | Mission scripting and MissionBot exist; two-minute drills teach the four factions | M, mission data | 4 |
| L2 | Visual tech-tree viewer | AoE2 DE in-game tech tree (patch notes fix its costs and requirements) | [R] https://www.ageofempires.com/news/age-of-empires-ii-definitive-edition-update-81058/ | Partly: Archive has Technology, no prerequisite graph | Tier-2/3 gating across four factions; generate from data/rules | M, FE | 3 |
| L3 | Link from a cameo tooltip to its Archive entry | Same source as L2 | [R] same | No | Archive already opens from the pause menu | S, FE | 3 |
| L4 | First-run offer to play the tutorial | AoE IV Mission Zero | [R] https://www.ageofempires.com/news/quickstart-guide-age-of-empires-iv/ | No | One dialog, one flag | S, FE | 2 |

### Playing AI and casual family play
| ID | Feature | Who does it, how | Source | In RTS_2027? | Why it helps here | Size / layer | Value |
|---|---|---|---|---|---|---|---|
| F1 | **Tactical pause that visibly accepts orders** (tint, banner, queued route lines) | CoH3 spacebar freeze with queued orders; Fractured Alliance plan, queue, execute with predicted route lines and a tint; Homeworld 3 research queue during pause; They Are Billions pauses to place and order | [R] https://www.pcgamer.com/company-of-heroes-3s-tactical-pause-lets-you-snatch-control-of-the-battlefield , https://www.gamespress.com/Tactical-Pause-Comes-to-Fractured-Alliance-Plan-Queue-Execute , https://www.homeworlduniverse.com/homeworld-3-patch-1-3-update-notes , https://numantiangames.com/TheyAreBillions/ | Partly: Space pauses; order-on-resume unverified | Skirmish and campaign are single-player; planning removes the speed barrier at zero balance cost | S-M, FE | 4 |
| F2 | Skirmish autosave (every N minutes, 2 rotating slots) | Standard | [–] | Not found by grep | Power cuts, little brothers; SaveSlots and GameSaver exist | S, FE | 3 |
| F3 | Toggleable in-game advisor line ("3000 credits, idle factory") | AoE IV Mission Zero style prompts | [R, partial] https://www.ageofempires.com/news/quickstart-guide-age-of-empires-iv/ | No | Same data as S1 and P7; off by default | M, FE | 2 |
| F4 | Slow-motion hold key | Wargame-style 1/10 speed instead of a true pause | [R] https://steamcommunity.com/groups/pausetacticality/discussions/0/2906376154321402758 | No | Alternative to F1; merge with X1 | S, FE | 2 |

Left out: a global build queue with auto-assign (Zero-K Global Build Command, [R] https://zero-k.info/Forum/Post/134634). The construction yard builds from the sidebar, not with roaming builders, so it does not fit. The Stormgate quick-macro panel as a separate panel is redundant: the sidebar already is a global production panel and only the keys are missing (P1).

## Ranked top 15
1. **X1 Game speed with slow settings (5, S-M, FE).** Allow a fractional factor (0.5, 0.75, 1, 1.25, 1.5) chosen in skirmish setup and changeable with [ and ] in play. The sim ticks at a fixed 20 Hz and only the wall-clock feed changes, so saves, replays and hashes are untouched. Show the speed beside the PAUSED label; on LAN the host sets it.
2. **S1 Idle button (5, S, FE).** A sidebar strip, a count and a hotkey that cycles idle harvesters, then builders, with a row for factories that have an empty queue. It fixes the commonest silent failure for new players and has five games behind it.
3. **P1 Grid hotkeys for the cameo grid (4, S-M, FE).** The tab keys exist, the cells do not. The grid is 1-3 columns wide, so a 3-column key block covers it. Show the letter on each cameo and in the tooltip. It must not clash with the 1-9 control groups.
4. **F1 Tactical pause (4, S-M, FE).** First verify that orders given while Space-paused run on resume. Then add a tint, a PLAN banner, order rings and lines that stay visible, and accept production while paused. This turns a pause button into a planning tool without changing a rule.
5. **P2 Repeat production (4, M, FE).** A loop icon on a queued cameo. When its queue empties the front end re-sends the same Build (subject to credits and supply). It uses ordinary commands, so replays stay deterministic.
6. **I1 Range rings in play (4, S-M, FE).** Ring on a selected unit, on a hovered enemy turret and while placing a defence. Render/RangeOverlay.cs and the F10 toggle exist already.
7. **I2 Matchup line in tooltips (4, M, FE).** "Strong vs / weak vs" generated from the warhead-versus-armour table, one line under the description. It stays right when the owner rebalances.
8. **O4 Stance buttons (4, M, SIM).** Hold position, return fire and free fire in the selection card. The enum value exists but the unit behaviour does not. This is the first item needing sim work, so do it after the front-end wins.
9. **K1 Handicap (4, M, SETUP).** Per-slot health multiplier 50-100 %, optionally income. It lives in skirmish settings and SimSetup and is saved in replays. It is the answer to "my daughter keeps losing to me".
10. **L1 Drill missions with medals (4, M, data).** Four to six two-minute scripted drills with bronze, silver and gold thresholds, reusing the mission system and the test bot.
11. **O1 Shift-queued waypoints (4, L, SIM).** The classic request. It costs a per-unit order queue in the sim and a drawing layer, so keep it behind the cheap items.
12. **X8 + F2 Auto-pause on focus loss and autosave (3 each, S, FE).** Small and boring, but valuable in a household. Do them in one batch.
13. **A1 + P7 Alerts for finished upgrades and blocked or idle production (3, S, FE).** Two new alert kinds plus a red dot on a tab, reusing the alert stack and minimap ping.
14. **S2 Auto control groups (3, S-M, FE).** New tanks join group 2 without a click. A settings page maps unit class to group.
15. **P5 + I5 Income rate and harvester counts (3, S, FE).** Reuse IncomeTracker for "+nnn/min" under credits, and a small number over each resource field.
Near misses (value 3): O2 line-drag move, O7 repair-all, L2 tech-tree viewer, L3 cameo-to-Archive link, M1 medals, M2 easier-tier offer, K2 starting conditions, K3 AI personalities, C1 icon zoom.

## Wireframes for the top 8
Layout facts used (from src/Game/Ui/Sidebar/SidebarLayout.cs): right sidebar, about a fifth of the window width. Top to bottom: credits, supply row, shipment row, power number with a vertical power bar, Sell and Repair, tabs, cameo grid (1-3 columns), minimap at the bottom. The selection card sits bottom centre over the map.

**1. Game speed (X1)**
```
Skirmish setup                           In play, top left of map
  Map: Rust Belt            < >            SPEED 0.75x   (blank at 1x)
  Game speed: Slow 0.75x    < >            PAUSED        (when Space)
Pause menu: Resume | Save | Settings | Speed: 0.75x [<][>] | Archive
Keys: [ slower, ] faster, Backspace = 1x (rebindable)
```
**2. Idle button (S1)**
```
+------------------------+
| CREDITS     $ 4,320    |
| SUP 34/60              |
| [!] IDLE 3 harv | 1 fac |  click = next idle harvester, camera jumps
|  PWR 120/90 ...        |  I cycles harvesters, Shift+I builders, Ctrl+I all
| [SELL][REPAIR]         |  "VEHICLES idle" opens that tab
| STR DEF INF VEH UPG    |
| [cameo][cameo][cameo]  |
```
Idle harvester = no field, no return and no move order for 5 s. Idle factory = empty queue with credits for the cheapest item. The strip pulses for 3 s the first time, with no sound by default, and is hidden when nothing is idle.

**3. Grid hotkeys (P1)**
```
Tab INFANTRY (3 columns)
[Rifle   ][Rocket  ][Medic   ]     Q W E R T Y stay tab keys
[ A      ][ S      ][ D      ]     A S D / Z X C = rows of the visible grid
[Engineer][Sniper  ][        ]     Shift+key queues 5, Ctrl+key = hold
[ Z      ][ X      ][ C      ]
```
The tooltip header reads "Rifleman (A)". A Controls-page group "Cameo grid" lets the player rebind them.

**4. Tactical pause (F1)**
```
+----- map ------------------------------------------+
| PLAN MODE (Space to run)    amber banner, top      |
| 8 % blue tint over the map                          |
|  o--->--->(X)  queued move lines stay on           |
|  [tank]--------> (red ring) attack target          |
+-----------------------------------------------------+
Sidebar: cameos still clickable (queue only)
```
Decide whether placement while paused is allowed. The AoM Retold thread says that game forbids it.

**5. Repeat production (P2)**
```
[Rifleman x3]   stack count top-left
[ loop (R) ]    click or Shift+R toggles repeat; amber when on
```
It stops when credits run out and resumes when they return. "Repeat paused: no power" reuses BlockReasonText.

**6. Range rings (I1)**
```
      ( . . . . . )     thin white ring = longest weapon of a selected unit
    (  [turret]    )    red ring = hovered enemy turret, "range 9" in tooltip
      ( . . . . . )     a placing defence ghost shows its own ring
```
Setting "Range rings: Off / Selected / Selected and hovered enemy", default the last.

**7. Matchup line (I2)**
```
Rocketeer                      $400  0:20  power 0
Anti-vehicle infantry. ...
Strong vs: Vehicles, Structures   (green)
Weak vs:   Infantry, Air          (red)
Range 9 · Sight 7 · HP 90
```
Both lists come from the warhead damage table against each armour class. Ties are omitted.

**8. Stance buttons (O4)**
```
[Attack-move A][Stop S][Guard G][Scatter X] | Stance: (o) Free ( ) Return fire ( ) Hold fire
                                            | Position: ( ) Hold (o) Roam
```
A group with mixed stances shows a dash. Clicking sets all.

## Open questions
- Do orders given while Space-paused run on resume? This decides whether F1 is S or M.
- Is CommandKind.SetStance handled anywhere in src/Sim? My grep found it only in the enum.
- Does the sim already have a per-unit order queue? Command has no queue flag, which suggests not (O1).
- Is there line placement of walls? (P8)
- Does tier 1 AI send early raids? If not, M2 is almost free.
- Overlap with qol-cnc: X3 presets, O1 shift-queue, O3 patrol. The lead should dedupe.

## Things checked and found sound
- Camera, selection, control groups, alerts with a last-alert key, cameo tooltips, placement ghost, rebinding, UI scale and text size match or beat what the competitor patch notes call QoL, so I did not re-propose them.
- Spectator and replay viewer already have speed, step and views. Only event markers and filters are missing.
- Colour-blind mode was an explicit owner decision (D-172 (5)); I did not argue with it.
- Archive, How to Play and the tutorial mission cover the learning basics. The gaps are contextual links and drills.

TOP 5 (by value, then cost):
1. X1 Game speed incl. slow, FE only (value 5)
2. S1 Idle harvester/builder/factory button, FE only (value 5)
3. P1 Grid hotkeys for sidebar cameos, FE (value 4)
4. F1 Tactical pause that shows queued orders, FE (value 4; verify order-on-resume first)
5. P2 Repeat production, FE via re-sent Build (value 4)
NOT COVERED: CoH3 reverse move, Iron Harvest, Dawn of War, Northgard, Battle Aces, Mechabellum, Total Annihilation (no usable page); tooltip numbers in the Ashes II video; any hands-on play of competitor games; any run of RTS_2027 itself.
Memory: nothing new worth saving.