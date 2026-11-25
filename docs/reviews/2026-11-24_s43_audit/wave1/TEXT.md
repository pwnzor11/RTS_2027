# TEXT — player-visible text, onboarding/tutorial, Field Manual drift
Scope read: data/ui/tips.toml, data/ui/credits.toml (diff only), data/menu/scenes.toml (header only), data/missions/tut_01/* (locale, briefing, mission.toml, mission.lua head), mission locale diffs since the manual build, data/archive (grep), data/rules/game.toml, src/Game/Menu/HowToPlayContent.cs, src/Game/Controls/GameKeys.cs, src/Game/Ui/Sidebar/{SidebarTab,TodayText}.cs, tools/booklet/{README,data/resolve_rules.py,full/*.py,full/*.toml}, README.md, docs/field_manual/RTS_2027_Field_Manual.pdf (pdftotext -layout, 11,038 lines, 144 pp) and RTS_2027_Quick_Reference.pdf (2 pp).

How the manual gets its numbers: every stat/tech-tree/appendix number is generated from data/rules (tools/booklet/full/part3_entries.py card()/stat_list(), via rules_resolved.json), keys from GameKeys.cs, difficulty table from data/ai/difficulty.toml. Nothing is retyped by hand, so drift = the committed PDF was built before later data changes. The committed PDF was last rebuilt 2026-11-18 (cd4883c6, git log -- docs/field_manual); data changes after that: 61fdf21e (s37, Nov 20), 1145c3b5 (s39, Nov 22), AI-difficulty commits Nov 21-24.

## Findings

### TEXT-01 · MED · data
What: The approved Field Manual PDF prints 8 unit numbers that no longer match data/rules (4 units; each also repeats in the tech-tree and cost-table pages).
Where: docs/field_manual/RTS_2027_Field_Manual.pdf; data/rules/vehicles.toml, infantry.toml, weapons.toml (commits 61fdf21e, 1145c3b5)
Evidence: script — /tmp/a43/fm_drift.py (scripts kept at /tmp/a43/fm_drift.py and /tmp/a43/readme_drift.py) parses all 145 entry cards (765 fields: cost, build time, HP, speed, sight, range, damage) against rules_resolved.json built from HEAD. Output:
  MISMATCH auth_tank        hp     manual=400  data=460   (card "HEAVY TANK", fm.txt:2390)
  MISMATCH dir_rocket_truck cost   manual=950  data=970   (fm.txt:4286)
  MISMATCH dir_rocket_truck build  manual=19.0 data=19.4
  MISMATCH dir_rocket_truck dmg    manual=90   data=84    (weapons.toml dir_rocket_salvo, D-290)
  MISMATCH exp_glide_tank   cost   manual=900  data=960   (fm.txt:7978)
  MISMATCH exp_glide_tank   build  manual=18.0 data=19.2
  MISMATCH lb_scavenger     cost   manual=60   data=54    (fm.txt:5910)
  MISMATCH lb_scavenger     build  manual=1.2  data=1.1
  Repeats of the same stale cost elsewhere in the PDF (grep): Rocket Truck 950 at fm.txt:5443 (Directorate tree), Scavenger 60 at :7316, Glide Tank 900 at :9628. All other 757 compared fields match. The 6 ids without a parsed card (auth_gate, auth_wall, dir_bunker, lb_scrap_wall, neu_ruin_*) are wall/neutral entries printed in a table, not cards; not checked (see Suspects).
Impact: Anyone reading the printed manual or the 2-page card sees old prices/HP for 4 units (Heavy Tank HP, Rocket Truck, Glide Tank, Scavenger). Balance is frozen (D-310), so the manual needs a rebuild, not the data.
Fix idea: Re-run tools/booklet pipeline (README steps 1-3) and re-commit the PDF as the last step before release; add the fm_drift check to the release checklist.   Changes a match outcome / number / visible text?: no (text only)

### TEXT-02 · MED · text
What: The manual's "Difficulty Tiers" page is generated from the base tier rows only and ignores the per-faction overrides added in s38-s41, so it now states things that are false for some factions.
Where: manual page "Difficulty Tiers" (fm.txt:1685-1730); tools/booklet/full/part2_basics.py:349-370; data/ai/difficulty.toml [faction.*]
Evidence: read+script — table prints Regular "sends idle units to a structure under attack" (respond_to_attacks) and Commissar "Attacks at army 6". But difficulty.toml: [faction.authority.tier2..tier5] respond_to_attacks = 0 (D-292 era commits 0d920d39, 86b92d4c), [faction.expedition.tier4/tier5] respond_to_attacks = 0, [faction.authority.tier5] attack_threshold = 14 (commit 2a94d97a). Also unlisted: army_cap per faction/tier (authority 28/40, expedition 10/14, left_behind 20 until tick 3600). The page was last rebuilt before these commits (Nov 18).
Impact: The manual and README ("Regular repairs and replaces harvesters...") describe the AI differently from what Authority/Expedition AIs do. Low player impact; misleading for anyone tuning against it.
Fix idea: Add one sentence "Some factions differ; see data/ai/difficulty.toml" or print the overrides in the table.   Changes a match outcome / number / visible text?: yes (text)

### TEXT-03 · MED · text
What: The in-game How to Play "Hotkeys" page and a loading-screen tip tell players the camera bookmarks are F5..F8. They are F6, F7, F8 and F12, and F5 is Quicksave.
Where: src/Game/Menu/HowToPlayContent.cs:101 ("Ctrl+F5 to F8 store a view, F5 to F8 jump back to it"); data/ui/tips.toml:77; stale code comment src/Game/Screens/GameScreen.cs:1584,1586; docs/USER_CHECKS.md:303
Evidence: read — GameKeys.cs: Bookmark1 = Keys.F6, Bookmark2 = F7, Bookmark3 = F8, Bookmark4 = F12; QuickSave = Keys.F5; QuickLoad = F9. The manual (fm.txt:1630, 1657) and README Controls table already say F6/F7/F8/F12 and F5 quicksave, so the three sources disagree. tips.toml's own header says every tip must match How to Play; the s37 text pass (7227e34d) reworded this very line ("F5..F8" -> "F5 to F8") without catching it. Note also: the tip is chosen at random on each loading screen.
Impact: Every new player who follows the tip presses F5 and quicksaves (and Ctrl+F5 does not store a view), F12 bookmark is never mentioned. Seen on the Hotkeys page and on loading screens.
Fix idea: Change both strings to "Ctrl+F6, F7, F8 or F12 store a view; the same key jumps back. F5 saves, F9 loads."   Changes a match outcome / number / visible text?: yes (text)

### TEXT-04 · MED · text
What: Tutorial step "train" tells the player to "Select the Barracks tab". There is no Barracks tab; infantry is built on the Infantry tab (caption INF, key E).
Where: data/missions/tut_01/locale.toml `"step.train"` ("Select the Barracks tab and train a Rifleman. New soldiers join the squad at the Barracks."); its hint says "select the Barracks and queue a Rifleman"
Evidence: read — src/Game/Ui/Sidebar/SidebarTab.cs: tabs are Structures, Defences, Infantry, Vehicles, Upgrades, Aircraft; Caption() STR/DEF/INF/UPG/AIR/VEH. Sibling steps ("Open the Structure tab") also use the singular "Structure", while the tab title is "Structures" (tooltip) and the sidebar shows "STR". Voiced line (voice/leader_tut_instructor/step.train.ogg) would need re-recording to change the wording.
Impact: Every first-time player at lesson 4 looks for a tab that does not exist (the hint, "select the Barracks", is also not how the sidebar works).
Fix idea: "Open the Infantry tab (E) and train a Rifleman." (re-record voice).   Changes a match outcome / number / visible text?: yes (text + voice)

### TEXT-05 · MED · data
What: README rosters list 16 stale costs/tiers, and the unit count is off by one.
Where: README.md "Rosters" tables, "Today's game" paragraph, factions table ("26 / 20 / 8")
Evidence: script — /tmp/a43/readme_drift.py (111 roster rows compared with data/rules by Archive title):
  Marksman 100 vs data 200 (D-205/154e7c38); Bulwark Heavy Tank 800 vs 900; Ashfall Refinery 2000 vs 1700; Rocket Truck 800 vs 970; Reactor Vent 300 vs 260; Smelter Vent 2000 vs 1700; Muster Hatch 300 vs 260; Assembly Bore 2000 vs 1700; Cistern 150 vs 130; Bunker Wall 50 vs 40; Pillbox 400 vs 340; Scavenger 60 vs 54; Glide Tank 900 vs 960; Packed Rig 4000 vs 1500; Supply Tender 6000 vs 1500; Command Ship 6000 vs 1500.
  Counts: README "76 units" / Expedition "26 units" vs resolved 77 units (Expedition 27: exp_grapeshot, added Nov 13 and absent from the README roster); Archive "243 entries" vs 244 in data/archive (77+74+28+25+20+13+7); README's "72 structures" is right (74 minus 2 neutral).
Impact: README is the public front page; the Field Manual is generated, README is hand-typed, so it is the one place numbers drift unnoticed.
Fix idea: Generate the roster tables from rules_resolved.json (as the booklet does) or drop the cost column.   Changes a match outcome / number / visible text?: yes (text)

### TEXT-06 · LOW · text
What: README status block claims a state older than the data: "Updated 2026-11-18 (session 34)". It still lists "AI difficulty ladder ... not yet evenly spaced" and start-corner bias as open without mentioning the s35-s41 map-fairness and AI-ladder work (D-269, D-294, D-304 era commits).
Where: README.md "Where it stands", "Known gaps"
Evidence: read — git log since Nov 18 shows mapgen fairness (112c8481, 1d04af56, a1bb7a68), AI tier fixes (ac8ce6e9, 713f0ba4).
Impact: Visitors read an out-of-date status.
Fix idea: Refresh the paragraph at release.   Changes a match outcome / number / visible text?: no

### TEXT-07 · LOW · text
What: Attack-move is described as "press A, then click" in two player-facing places, but the click is a right-click.
Where: GameKeys.cs Infos AttackMove "Attack-move: press, then click the ground" (shown on the Controls screen); README Controls table "**A** then click is attack-move"
Evidence: read — OrderController.cs:15-20 "A arms attack-move (the next right-click on the ground is an attack-move)"; HowToPlayContent.cs:60 and tips.toml say "right-click" correctly.
Impact: Players left-click and nothing happens (left-click on the ground clears selection).
Fix idea: Say "then right-click the ground".   Changes a match outcome / number / visible text?: yes (text)

### TEXT-08 · LOW · text
What: Name inconsistency: Authority refinery is "Ashfall Refinery" in the Archive, sidebar, manual and README tables, but plain "Refinery" in the tutorial (6 strings), HowToPlay and tips ("Build a Refinery"), and in auth_01 locale.
Where: data/missions/tut_01/locale.toml (step.harvest, hint.harvest), data/missions/auth_01/locale.toml, HowToPlayContent.cs:75
Evidence: read — data/archive/structures_authority.toml auth_refinery title = "Ashfall Refinery" (verified with a tomllib script); the sidebar cameo uses the Archive title.
Impact: Minor: the cameo is labelled "Ashfall Refinery" while the instructor says "Refinery". Also generic guide text "Build a Refinery. A Scraper comes with it" is true for Authority/Directorate only (Left Behind: Gleaner/Salvage Yard; Expedition: Skimmer/Dredge Works) — wording, not a bug.
Fix idea: Use the Archive titles in tutorial text.   Changes a match outcome / number / visible text?: yes (text)

### TEXT-09 · LOW · debt
What: Stale comments that contradict current behaviour: game.toml says the Expedition "is NOT playable yet (RuleCounts.PlayableFactions stays 3)" although README/manual say all four are playable.
Where: data/rules/game.toml (comment above [faction.expedition]); GameScreen.cs:1584-1586 (F5..F8 comment, see TEXT-03)
Evidence: read — README "All four are playable in skirmish".
Impact: Misleads a maintainer; none for players.
Fix idea: Delete the comment.   Changes a match outcome / number / visible text?: no

## Suspects (unproven)
- Walls/gates/neutral ruins: fm_drift.py did not parse auth_wall, auth_gate, dir_bunker, lb_scrap_wall, neu_ruin_*; they are in the "walls" table (part3_entries.py walls_table). Prove with: compare that table's cost/HP column to rules; no data commit touched walls since Nov 11 (git log), so probably fine.
- Manual tech-tree SVG / quick-reference counter chart: warhead percentages are generated; no warheads.toml change after Nov 18 (git log -- data/rules/warheads.toml shows none) so they should match; not extracted from the PDF.
- Tutorial briefing says "watch the objective list on the right"; objective bar layout was reworked in 0fd563d5 (Nov 20). Prove with a screenshot of the HUD.
- Voice-over: mission locale strings were re-capitalised in 7227e34d (e.g. "Landing Craft", "Reactor Vent"); only capitalisation changed in voiced tut lines, but dir_04 briefing text changed wording ("THREE THOUSAND TICKS" -> "TWO AND A HALF MINUTES"); if briefings are voiced, the audio would differ. Prove with: ls data/audio/voice for dir_04.
- TodayText.cs hard-coded upgrade numbers (+20% hull, Tinker 5/3 cells, pack 40% faster, plunder x1.5 cap 1000, salvage 1.5x, picket +2 sight) were checked against techs.toml/upgrades.toml/Simulation.Effects.cs and match today; they are literals, so a future tune would silently disagree.

## Checked and fine
- 765 card fields vs data (all except TEXT-01 items) incl. every faction's range, speed, sight, damage/reload.
- Manual Part II numbers (build seconds per 100 credits, sell refund, repair, veterancy, supply cap, bookmarks/quicksave keys, key table, quick-reference hotkeys) are generated from game.toml/GameKeys.cs and match current values.
- tips.toml claims vs game.toml: pitchblende 2x ashfall (300/150), sell refund half (500 permille), ash damage to infantry only, Hero heal, supply cap, LB/Expedition start units.
- Faction names (Authority/Directorate/Left Behind/Expedition, long forms) consistent across README, manual, mission text and src; no typos/double words found in tips, HowToPlay, tutorial, mission locales, part*_text.toml.
- Tutorial: tech list, free Scraper with the refinery (buildings.toml free_unit), "four riflemen", cash 4500, Power Plant/Barracks names; sunrise charge "two and a half minutes" = 3000 ticks at 20 Hz.
- README quick facts: 144 and 2 pages, 16 missions + tutorial, 28 upgrades, 72 faction structures, supply 20/10 ceiling 200, five AI tiers.

## Scripts (read-only; run with `python3 -I`)
/tmp/a43/fm_drift.py <repo> <rules_resolved.json> <pdftotext -layout fm.txt>   (rules_resolved.json from `python3 tools/booklet/data/resolve_rules.py <repo> /tmp/rr.json`; fm.txt from `pdftotext -layout docs/field_manual/RTS_2027_Field_Manual.pdf /tmp/fm.txt`)
 - finds each "TITLE ....... cost" card heading, then regex-extracts BUILD TIME, "N HP", SPEED, SIGHT, WEAPON RANGE, DAMAGE / RELOAD from the next 40 lines and compares with the resolved rules (tolerance 0.06 on derived seconds/cells; exact on cost/HP/sight/damage).
/tmp/a43/readme_drift.py <repo> <rules_resolved.json>   (README roster rows -> Archive title -> cost/tier).
