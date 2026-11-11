# ux-text: first-time-player UX and player-visible text — findings
Scope read: src/Game Screens/Ui/Menu/Controls/Archive/Saves (string literals + key files), data/menu, data/rules (names, upgrades, techs), data/archive, data/missions (tut_01, auth_01, dir_01, lb_01, exp_01 in full; locale-key check on all 17), data/campaigns, the two 2026-11-07 review docs, D-173. NOT done: screenshots in docs/reviews/img (not viewed); spell-check of full archive/lore prose (no dictionary on box; only UK/US mix scan, which is clean: all British); locale/briefing of the other 12 missions read only by script; no live play.

## Summary
1. Sidebar/card/tooltip names come from title-casing rules ids, never the Archive titles: upgrades show as "Up Flak Vests", the Tank is "Medium Tank" but "Heavy Tank" in the Archive, Exp Pillbox is "Pillbox" while exp_01's objective says "Seawall Emplacement".
2. Every campaign radio line is prefixed with the raw speaker id ("LEADER_AUTH_WRENFIELD: ...").
3. Skirmish map picker shows developer map ids ("rustbelt_m2", "ash_plains_m_2p_41", "test_32").
4. Four Left Behind upgrades have no effect text at all; Archive "Effect" line is empty for 27 upgrades; other upgrade lines are raw jargon.
5. Campaign menu locked-faction hint is off by one row (wrong reason shown, Expedition shows none).
6. Tutorial has wrong/missing instructions (nonexistent "Barracks tab", "objective list on the right", never says click READY cameo, no warning that Refinery costs 2000).
7. No help for Left Behind / Expedition mechanics, supply, or the D-173 no-rebuild defeat clock; no in-game pointer to F1 help; no confirm on Restart/Quit/Overwrite.
8. Post-game table: raw enum faction names, INCOME/M always 0, "T" means tier here and team in setup, no loss reason.

## Findings

### UX-01 · major · TEXT · confidence high
**What:** Every radio/dialogue line on screen is prefixed with the raw speaker id, uppercased.
**Where:** src/Game/Screens/MissionOverlay.cs:83; data/missions/*/mission.lua (`local SPEAKER = "leader_auth_wrenfield"`, `Dialogue.Say(SPEAKER, SPEAKER, key)`); src/Scripting/MissionDialogue.cs doc: "Speaker id ... never display text".
**Evidence:** DialogueLines() builds `line.Speaker.ToUpperInvariant() + ": " + line.Text`. Nothing maps ids to names (grep `leader_` in src/Game: none). Tutorial shows "INSTRUCTOR:".
**Impact:** Player reads "LEADER_LB_CALLOWAY_ROSS: Commander, Calloway-Ross. ..." in all 16 campaign missions.
**Fix idea:** Resolve speaker id to a display name (leaders.toml / locale `speaker.<id>`). S.

### UX-02 · major · TEXT · confidence high
**What:** Sidebar cameos, tooltips, selection card and supply message use `CameoNames.Display(id)` (strip prefix, title-case) instead of the Archive titles, giving raw/odd and conflicting names.
**Where:** src/Game/Ui/Sidebar/CameoNames.cs:9-35; used at SidebarPanel.cs:566,809,881, CardModelBuilder.cs:152,203,222, TierGrouping.cs:167,171, UpgradeEffectText.cs:58-60, BuildingUpgradeOffer.cs:36. Archive titles: data/archive/*.toml.
**Evidence (script comparing both, 77 mismatches):** all 28 upgrades show as "Up Flak Vests", "Up Convoy Drill", ... (archive "Flak Vests"); auth_tank "Medium Tank" vs archive "Heavy Tank"; auth_jeep "Jeep" vs "Scout Car"; exp_repair_hover "Repair Hover" vs "Mender"; lb_mother_rig_packed "Mother Rig Packed" vs "Packed Rig"; exp_sam "Sam" vs "SAM Mount"; auth_signals_hq "Signals Hq"; dir_landtrain "Landtrain" vs "Land-Train"; dir_shaft "Shaft" vs "Command Shaft"; exp_pillbox "Pillbox" vs "Seawall Emplacement"; auth_atomic_battery, auth_engineering ("Engineering" vs "Engineering Works", also named in HowToPlay/TierGrouping "(needs Engineering Works)" hard-coded at TierGrouping.cs:169), etc. exp_01 objective/briefing say "Seawall Emplacement" (locale.toml:25, briefing.md:8) which cannot be found in the build menu.
**Impact:** Same thing has two names; a mission objective names a building the player cannot find; upgrades look like developer ids.
**Fix idea:** Add `name` to rules or have Display() look up Archive title first. S-M.

### UX-03 · major · TEXT · confidence high
**What:** Skirmish map list shows developer ids as map names, including test maps.
**Where:** src/Game/Screens/MapPicker.cs:~88-95 (`label = $"{map.Name} {W}x{H}, {max} players"`); data/maps/*.map.toml `name`.
**Evidence:** names are "ash_plains_m_2p_41" (file ash_reckoning), "glass_coast_m_2p_11", "rustbelt_m2", "test_32", "test_64". Label e.g. "rustbelt_m2 96x96, 2 players (curated)". No map preview anywhere in SkirmishSetupScreen.
**Impact:** First screen a family member sees reads like a debug menu; test maps offered.
**Fix idea:** `title` key in map meta, hide test_* from picker. S.

### UX-04 · major · TEXT · confidence high
**What:** Upgrade tooltips: four Left Behind upgrades show no effect line; others show engine jargon; Archive Effect row is blank.
**Where:** data/rules/upgrades.toml:127-157 (lb_up_quick_pack, grapple, salvage_rights, patchwork use schema fields, no `effects`); src/Game/Ui/Sidebar/UpgradeEffectText.cs:25-45; src/Game/Archive/ArchiveStats.cs:48.
**Evidence:** simulated output: "Conscript: rad resist permille set to 900" x7 lines (dir_up_lead_lining), "Hatch: ticks per point x0.75", "Rad Shell warhead: contaminate +40", "Marine: vet threshold permille x0.8", "Rifle weapon: rate of fire x0.9" (reads as slower; also names dir_rifle only), "All players: shipment interval x0.85". auth_grouting shows just "Grouting". ArchiveStats prints `u.Effect`, which is "" for all tiered upgrades.
**Impact:** "What does this upgrade do in numbers" is unanswerable for 5 upgrades and unreadable for ~10; lists of 7-12 units overflow a tooltip.
**Fix idea:** hand-written one-line `summary` per upgrade; fall back to generated text. M.

### UX-05 · major · CODE · confidence high
**What:** Campaign menu lock-reason hint is indexed off by one.
**Where:** src/Game/Screens/CampaignScreen.cs:35 (Tutorial is item 0) vs :81 `_rows[_panel.Selected].Reason`.
**Evidence:** hovering Directorate shows Left Behind's reason, Left Behind shows Expedition's ("opens after act 3"), Expedition shows nothing (4 < 4 false), Authority shows Directorate's (empty).
**Impact:** Wrong unlock instruction for locked campaigns.
**Fix idea:** index `Selected-1`. S.

### UX-06 · major · TEXT · confidence high
**What:** Tutorial instructions are wrong or incomplete.
**Where:** data/missions/tut_01/locale.toml:7 ("Select the Barracks tab"), :5 and :8 ("Structure tab"), briefing.md last paragraph ("objective list on the right"), mission.toml cash=3000.
**Evidence:** tabs are Structures/Infantry (SidebarTab.cs); no Barracks tab. Objectives panel is top-left (MissionOverlay.cs:~108 origin x=8). Steps never say the finished cameo must be clicked when it shows READY and then the ground. Refinery costs 2000 (buildings.toml), start cash 3000, Power 300 + Barracks 300 leave 2400; five Riflemen (Shift-click x5 is advertised in the tooltip) leave under 2000 and income is zero. Cost is paid progressively (ProductionState.Spent) so exact timing unverified: confidence medium for the lock-out. The enemy camp then ends the tutorial early (lua FoesGone), so not a hard soft-lock.
**Impact:** The most likely stuck moments: step 3 (placing), step 5 (finding the tab), step 6 (cannot afford Refinery). Tutorial never mentions F1/Controls, supply, camera keys.
**Fix idea:** fix the wording; add "keep 2000 for the Refinery"; top up cash on step 6 if below cost. S.

### UX-07 · major · DESIGN · confidence high
**What:** No in-game help for faction mechanics or several systems.
**Where:** src/Game/Menu/HowToPlayContent.cs (pages: basics, selecting, moving, building, harvesting, tunnels, upgrades, hotkeys).
**Evidence:** no page for Left Behind (Mother Rig pack/unpack, K key not listed), Expedition (shipments, beachhead, shipyards, landing craft), supply cap (grep "supply": none), power consequences, veterancy, superweapons beyond one line; page 1 describes only Authority and Directorate. Upgrades page says the enemy superweapon "charges for a long time" but no UI shows the enemy charge (only SuperweaponLaunched alert, AlertInfo.cs:34). How to Play is reachable only from the main menu; Pause menu (PauseScreen.cs:28-38) has Settings/Archive but not How to Play/Controls; F1/? exists but nothing on screen says so (GameScreen.cs:369).
**Impact:** A player on LB or Expedition campaign (opens after act 2/3) gets no explanation; Directorate tunnel UI has no network-capacity or link overlay (grep Tunnel in Ui: none; rules cap 10 points).
**Fix idea:** per-faction help pages, "Help" in Pause, first-mission tooltip hints. M.

### UX-08 · major · DESIGN · confidence medium
**What:** The D-173 no-rebuild defeat has no warning or explanation.
**Where:** docs/DECISIONS.md D-173; src/Game/Screens/GameScreen.cs:1260-1265, GameOverScreen.cs:58 (headline only).
**Evidence:** no HUD countdown (NoRebuildTicks not read in src/Game). Player with an army and buildings is declared "Defeat" 2 minutes after losing production, factories and harvesters. Headline() returns "Defeat" also when winningTeam < 0 (a draw).
**Impact:** "Why did I lose" and "who is winning" are unanswerable in single player.
**Fix idea:** red HUD timer "No production: defeat in 1:23"; reason line on the game-over screen; "Draw" headline. S-M.

### UX-09 · minor · CODE · confidence high
**What:** Post-game stats table is the spectator table: raw enum faction ("LeftBehind"), INCOME/M always 0, proportional-font column padding, "T" ambiguity.
**Where:** GameScreen.cs:493 (`Collect(_sim.Sim)` without an IncomeTracker); SpectatorTable.cs:57,61-67 (`{r.Faction}`).
**Evidence:** Collect doc: "income: null gives 0". `T{r.Tier}` here, but SkirmishSetupScreen.cs:271 uses `T{Team+1}` for team. Headers "STRUCT", "UPG", "K/L" abbreviations.
**Impact:** Looks broken; no per-player summary for the human. No replay-save option for non-spectators (GameOverScreen.cs:34-46) and no note that the last game is auto-recorded.
**Fix idea:** player-facing summary (time, built, lost, killed, income earned). S-M.

### UX-10 · minor · TEXT · confidence high
**What:** Superweapon button captions are ids: "SECOND_DRIFT", "SALVO_ARRAY", "ATOMIC"; status "PAUSED" is unexplained; no effect/ETA.
**Where:** SuperweaponButton.cs:69-73, GameScreen.cs:810, SuperweaponText.cs:16 ("SALVO_ARRAY READY - click it, then pick a target").
**Evidence:** superweapon ids buildings.toml:431,568,889,1223; SuperweaponView doc: Paused = low power. Only a percentage shows.
**Fix idea:** use display name, "Charging 62% (about 1:40)", "Paused: low power". S.

### UX-11 · minor · TEXT · confidence high
**What:** Skirmish setup error messages cut mid-sentence at 56 chars; hint buttons cryptic.
**Where:** SkirmishSetupScreen.cs Start() (`Shorten(problems[0], 56)`), ExpeditionSetup.cs:47.
**Evidence:** "Slot 2: the Overseas Expedition needs a map with a landing coast." is 65 chars, shown "…needs a map with a l..."; the fix (landing coast) is the cut part. Row buttons "Kind Fac Tier Cmdr Team Col"; "Cmdr" = LLM commander ("LLM -" label, SetupText.cs:9) unexplained; AI tier names (Recruit..Commissar from difficulty.toml) never described; "Human Authority T1" (T=team).
**Impact:** unexplained options; no random faction, starting credits (fixed 5000, game.toml), game speed, fog toggle, map preview. Click count to start default game is fine: Skirmish > New Skirmish > Start (3).
**Fix idea:** wrap message in the row instead of cutting; tooltips on buttons. S.

### UX-12 · minor · DESIGN · confidence high
**What:** Destructive actions without confirmation or undo.
**Where:** PauseScreen.cs:36-38,53 (Restart, Quit to menu, "Overwrite: <save>" act immediately); GameOverScreen.cs Esc quits; SidebarPanel Sell mode click sells at once.
**Impact:** one misclick loses a game or a save. Map editor does confirm (MapEditorScreen.cs:148) so a pattern exists.
**Fix idea:** two-step confirm for Restart/Quit/Overwrite. S.

### UX-13 · minor · TEXT · confidence high
**What:** Cameo "WAIT" and other terse states give no reason; money has four names.
**Where:** SidebarPanel.cs:902-903 ("WAIT" for a stalled item, i.e. no funds, tooltip does not say), :378 (tooltip "Credits / Cash / Stored / Storage"), :397 area "$ 1234"; HowToPlayContent "credits"; tut_01 "money"; AlertInfo.cs:27 "Harvester" vs Scraper everywhere else; CardModelBuilder.cs:167 "Expedition" vs setup "Overseas Expedition".
**Impact:** "why can't I build this" is answered for locked/queue-full/supply/shipyard (BlockReasonText) but not for no funds; income per minute and per-item ETA are not shown anywhere in single player (only in spectator table).
**Fix idea:** "Waiting for funds" label; pick one word (credits). S.

### UX-14 · minor · TEXT · confidence high
**What:** Controls page and debug leftovers shown to normal players.
**Where:** GameKeys.cs:69-80,123-158 (rows "Sandbox: step the simulation", "Spectator: ..." listed for all); OverlayToggles.cs:23 says "F5 ranges" but key is F10 (GameKeys.cs:72; F5 = bookmark 1); HowToPlayContent.cs:102 "Sandbox only ... (for testing)"; HowToPlay hotkeys page omits C, 1-9, J/Tab/O, End, K; LlmUiText.cs:54 "not wired yet" (unused); Controls row text "Scroll the view (Controls page: Edge scroll)".
**Fix idea:** filter rows by context; fix label. S.

### UX-15 · minor · DESIGN · confidence high
**What:** Colour-blind and text-size gaps.
**Where:** Sprites/PlayerColours.cs:25-34; UiScale.cs (TextSize Small 0.85, UI scale 75%, FloorScale 0.5); AlertInfo/Minimap colours.
**Evidence:** simulated deuteranopia (Machado, script cvd.py): Blue vs Purple dE 4.9, Amber vs Orange 9.6, Cyan vs Pink 14.1 (indistinguishable). Team identity is colour only; no CVD setting, no pattern. UI 75% + Small text at 720p gives ~0.64x of a 7 px font: no floor on the combination. Subtitles: dialogue text exists, alerts have text log; no audio-subtitle option needed beyond that. Pause in SP: Space and menu both pause (works). Key rebinding exists (ControlsScreen).
**Fix idea:** colour-safe palette option; clamp effective text scale. S-M.

### UX-16 · idea · TEXT · confidence medium
**What:** Mission briefings are 100% upper-case, 124-224 words (all 16 files).
**Where:** data/missions/*/briefing.md. Directorate objectives say "Authority" while radio says "tenancy" (dir_01 locale). "Hatch" vs "Muster Hatch" near-collision. Briefings give no controls help; campaign difficulty tiers (D-061) are not described on the Briefing screen.
**Fix idea:** mixed-case or typewriter font; short "what changes" line per tier. S.

## Open questions
- Is the title-case naming intentional until a locale file exists (CameoNames doc says "when the rules carry none")? Then UX-02 needs rules `name` keys.
- Should test_32/test_64 stay selectable (the tutorial uses test_32)?
- Is campaign supply cap on? (not verified; affects tutorial).
- Screenshots in docs/reviews/img were not viewed.

## Things checked and found sound
- UK spelling consistent across archive, missions, UI (scan: no US variants in player text).
- All lua locale keys exist (only lb2.radio.post is built by concatenation).
- Block reasons for queue/supply/shipyard, placement ghost reasons (PlacementCheck.cs:58-68), supply counter with building name, alerts, objective markers, controls page with rebinding, edge scroll, control groups, pause in SP, Restart in game over, no TODO/dev text in archive or locale prose, endings contain approved text.
