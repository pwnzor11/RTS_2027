I could not write findings.md. The Write tool refused ("Subagents should return findings as text, not write report files"), so the full findings are below. Screenshots are in /home/claude/audit/ux-live-menus/shots/ (31 files). My Xvfb and game processes are killed.

# ux-live-menus: menus and non-match screens, played live

Scope read: I ran the prebuilt Debug binary (v0.1.5+e059f0f) under Xvfb :91 with software GL, no window manager and dummy audio. HOME and the XDG dirs were under my folder. Windows were 1280x720 (default) and 1600x900 (via settings file).
- **Played:** main menu; Settings (every row, Controls, LLM, About); Skirmish menu and New Skirmish setup (curated and generated maps, slots, Kind/Fac/Tier/Cmdr/Team/Col); starting a skirmish; pause menu; save; quit to menu; Continue; Load Skirmish and Load Campaign; Campaign menu (Tutorial, Authority, locked factions); the Authority m1 and Tutorial briefings; Archive; How to Play; Replays and the replay viewer; Map Editor entry; a spectator Game Over. I also tried UI 200% with Text Extra large on the main menu, settings and skirmish setup.
- **Not reached live:** Directorate/Left Behind/Expedition mission lists (locked on a fresh profile); Victory/Defeat/Debrief/Ending for a human player; stills; the Quit button itself (only Esc-to-exit was seen). There is no credits screen except Settings > About.
- **Harness artefacts, discarded:** without a window manager, an in-game Fullscreen or window-size change does not resize the X window, so captures look cropped. Fast xdotool clicks and keys are often dropped at 6-30 fps. The clipping findings were re-checked with Fullscreen Off.

## Summary (worst first)
1. UX-LM-01 (major): at UI 200% + Text XL the 1280x720 main menu loses "Quit" and the caption overprints "Settings" and the version string.
2. UX-LM-02 (major): Skirmish setup at 200%/XL is unusable (title and edges off-screen, Team and Col buttons not visible).
3. UX-LM-03 (major): slot rows hide the team tag behind the colour swatch, the panel resizes under the cursor, and one mis-click made my Human slot AI so the game silently became "Spectate".
4. UX-LM-04 (major): no confirmation for Quit to menu, Restart, Quicksave/Overwrite or main-menu Quit, and Esc on the main menu quits. Three Esc presses from a briefing closed the game.
5. UX-LM-05..07 (minor): Save replay gives no feedback; the spectator Game Over stats table is drawn over the HUD; the load and replay summaries are misleading.
6. UX-LM-08 (idea/design): the skirmish setup is far thinner than C&C Remastered, OpenRA or AoE2 DE, and the dev test maps are offered to players.

What works: first impression is good. Campaign is preselected, Continue is greyed with a reason before the first save, and locked entries explain themselves on click. Archive and How to Play are polished. Back or Esc exists everywhere, and keyboard navigation works. Settings persist (checked settings.toml) and UI scale applies immediately.

## Flow table (clicks from launch; Campaign is highlighted at start)
| Screen | Reached by | Clicks | Verdict |
|---|---|---|---|
| Main menu | launch | 0 | good at 100%; broken at 200%/XL (01) [m03_menu_ui200_xl_1280, m01_menu_ui200_xl] |
| Settings | Settings | 1 | one flat 14-row page, no Reset; fits at 200%/XL [s09_ui200_textXL, s10_settings_ui200_xl_fits] |
| Controls | Settings > Controls | 2 | OK, but lists debug keys (15) [c01_controls] |
| LLM / About | Settings > ... | 2 | OK; the About Back label overflows its box (10) [l10_llm] |
| Skirmish setup | Skirmish > New | 2 | works but bare (08); slot rows (03); broken at 200%/XL (02) [k04_setup_default, k08_slots, k09_team, q4, k02_setup_ui200_xl] |
| Load Skirmish | Skirmish > Load | 2 | lists the save; wording (07) [sv01_load_skirmish] |
| Campaign | Campaign | 1 | Tutorial, Authority 0/4, Directorate 0/4, two locked [c10_campaign] |
| Locked faction | click Left Behind | 2 | reason only as a toast (09) [c11_locked] |
| Authority missions | Campaign > Authority | 2 | "[next]" / "[locked]" only [c13_authority] |
| Briefing | ... > mission 1 | 3 | good; difficulty defaults to Veteran (09) [b01_briefing, b02_briefing_full] |
| Tutorial briefing | Campaign > Tutorial | 2 | hard line break (13) |
| Load Campaign (empty) | Campaign > Load game | 2 | the empty message is a selectable row (12) [w07_campaign_load] |
| Archive | Archive | 1 | very good [a01_archive] |
| How to Play | How to Play | 1 | good; the Back box is mis-sized (10) [h01_howto] |
| Replays and viewer | Replays | 1+1 | works, "3 players" (07) [r01_replays, r02_replay_viewer] |
| Map Editor | Map Editor | 1 | opens with an error and clipped text (14) [e01_editor] |
| Pause | Esc in match | n/a | no confirms (04) [p01_pause] |
| Save game | Pause > Save game | 1 | works; list styling (12) [p02_save, p03_after_save] |
| Continue | main menu after a save | 1 | loads the newest save [p07_quit_to_menu] |
| Spectator Game Over | AI-vs-AI skirmish ends | n/a | overprint (06), no save feedback (05) [q7] |
| 1600x900 | via settings file | n/a | fine [w05_1600_menu] |
| Human Victory/Defeat/Debrief/Ending/Credits | not reached | | code only |

## Findings

### UX-LM-01 · major · CODE · confidence high
**What:** At UI 200% + Text XL on 1280x720, the main menu does not fit. "Quit" is half off the bottom, the caption ("Glass Coast - grey ash dunes...") is printed over "Settings", and it runs into the version string. 200% with Normal text already pushes Quit off the bottom.
**Where:** `src/Game/Menu/LobbyMenuView.cs`, `src/Game/Screens/MainMenuScreen.cs:94-110`. `MenuPanel.cs:176-189` (`Fitted`) shrinks every other panel to fit; the main menu has no equivalent.
**Evidence:** [m03_menu_ui200_xl_1280.png] (Fullscreen Off, settings.toml `ui_scale_percent = 200`, `text_size = 3`) and [m01_menu_ui200_xl.png]. The Settings row itself says "(limited by window size)" [s09_ui200_textXL.png], so the rule exists but the main menu ignores it.
**Impact:** The people the setting is for get an overlapping menu with an unclickable Quit.
**Fix idea:** Run the lobby layout through the same fit loop, or move the caption. S.

### UX-LM-02 · major · CODE · confidence high
**What:** Skirmish setup at 200%/XL on 1280x720 runs off every side. The title is gone, the panel is wider than the window, Team and Col buttons are off-screen, and the top row is clipped.
**Where:** `SkirmishSetupScreen.cs:179-216`, `MenuPanel.cs:176-189`. `Fitted` floors at 0.5x of the requested scale and still does not fit.
**Evidence:** [k02_setup_ui200_xl.png].
**Impact:** Team and colour cannot be used at the largest accessibility settings.
**Fix idea:** Let `Fitted` shrink further, or stack the slot buttons under the label. M.

### UX-LM-03 · major · CODE · confidence high
**What:** Slot rows are fragile at normal size, and the panel resizes while you click.
- (a) The label is hidden behind the colour swatch. With "Regular Authority" the "T1" vanishes, with "Commissar Authority" the "T2" does, and with Commander on the label is cut mid-word ("...(no key - plays as standard").
- (b) Panel width follows the longest text, so the panel recentres after each click and buttons move under the pointer. My next "Team" click hit "Kind" on row 1, so my Human slot became AI.
- (c) With no Human slot the game silently becomes "Spectate"; only a hint line and the Start label change.
**Where:** `SkirmishSetupScreen.cs:157-175` (swatch at `firstButton.X - size - 3*scale`), `:206-210`, `SlotLabel :253-262`, `MenuPanel.Measure :191-203`.
**Evidence:** [k04_setup_default.png], [k08_slots.png], [k09_team.png], [q4.png].
**Impact:** The team assignment is invisible once names get long, and mis-clicks change the game type unnoticed.
**Fix idea:** Fixed-width label column with an ellipsis, swatch in its own column, constant panel width, and a text "Human / AI / Closed". S-M.

### UX-LM-04 · major · DESIGN · confidence high
**What:** No destructive action asks to confirm, and Esc quits from the main menu.
- Pause "Quit to menu" and "Restart" act at once (`PauseScreen.cs:37-45`, `GameFlow.cs:12-20`).
- "Quicksave" and the "Overwrite: name" rows overwrite silently (`PauseScreen.cs:38,48-52`).
- Main menu: Esc quits (`MainMenuScreen.cs:79-83`, marked ASSUMED M3-16) and Quit has no confirm.
- Game Over "Restart" and "Quit to menu" are immediate (`GameOverScreen.cs:44-45`).
**Evidence:** I left the Tutorial briefing with three Esc presses (briefing > Campaign > main menu > exit) and game.log shows a clean exit. [p01_pause.png]; [p07_quit_to_menu.png] is the result of one click on "Quit to menu" mid-match.
**Impact:** One mis-click or one extra Esc loses a match or closes the game. C&C Remastered, AoE2 DE and OpenRA all ask first.
**Fix idea:** Shared confirm notice for Restart, Quit to menu, Overwrite and Quit; make Esc on the main menu do nothing. S.

### UX-LM-05 · minor · CODE · confidence high
**What:** Spectator Game Over "Save replay" shows no notice, file name or folder.
**Where:** `GameOverScreen.cs:38`, called from `GameScreen.cs:494`; the replay is already auto-saved at `GameScreen.cs:298`.
**Evidence:** I clicked it twice and the screen did not change [q7.png is the screen before]. `bin/replays/skirmish_last.rpl` was written once at match end. The Replays list keeps only `skirmish_last` and `skirmish_prev` [r01_replays.png], so the button implies a permanent copy that it does not make.
**Fix idea:** Show "Saved as X" and write a named file, or drop the item. S.

### UX-LM-06 · minor · CODE · confidence high
**What:** On the spectator Game Over the final stats table is drawn over the live spectator HUD lines, so both are unreadable ("P1 Authority T2 $37 P1+75..."). The HUD uses "T1/T2" for tech tier, while the setup rows use "T1/T2" for team.
**Where:** `GameScreen.cs:494` (`statsLines`) plus the spectator header.
**Evidence:** [q7.png].
**Fix idea:** Hide the spectator HUD while the table shows; rename one "T". S.

### UX-LM-07 · minor · TEXT · confidence medium
**What:** Load Skirmish reads "Skirmish on rust_belt_s_2p_1, vs recruit tier 3, 0.2 min". "recruit" is the AI registry id, while tier 3 is called Veteran everywhere else; only the first AI is listed and no factions are shown. The Replays list shows the same 2-slot match as "3 players". The cause is unchecked (a neutral player is probably counted).
**Where:** `src/Game/Saves/GameSaveHeader.cs:69`; `src/Game/Menu/ReplayCatalog.cs:60`.
**Evidence:** [sv01_load_skirmish.png], [r01_replays.png].
**Fix idea:** Print the tier name (as `LoadTierNames` does) and the factions; exclude neutral from the count. S.

### UX-LM-08 · idea · DESIGN · confidence medium
**What:** The skirmish setup is thin next to C&C Remastered, OpenRA and AoE2 DE.
- Maps show as file ids ("rustbelt_m2 64x64, 2 players (curated)", "ash_plains_m_2p_41", "glass_coast_m_2p_11"), with no preview or description.
- The developer fixtures "test_32 32x32" and "test_64 64x64" are in the player cycle, 3 and 4 clicks of ">" from the default (`MapPicker.List`, `data/maps`).
- "Generated" is the last entry, so Biome/Size/Ocean/Seed are only found after five clicks.
- The column buttons "Kind Fac Tier Cmdr Team Col" have no headers, and tier names have no explanation.
- Personas cannot be chosen. Cmdr toggles an LLM commander and the persona is picked automatically ("Commissioner Imelda Strand is offline - playing the standard AI."). No UI references personas; this matches `Sandbox/CommanderSet.cs` only.
- No random faction, and no cash/speed/fog options. Check DECISIONS before treating those as gaps.
**Where:** `SkirmishSetupScreen.cs:179-216`, `MapPicker.cs:70-80`.
**Evidence:** [k04_setup_default.png], [k08_slots.png]. I saw the test maps in the cycle live, but that capture was not kept.
**Fix idea:** Hide `test_*`, show map names from `MapMeta`, make Generated its own page, add one help line for the selected cell. M.

### UX-LM-09 · minor · DESIGN · confidence medium
**What:** The lock reason for Left Behind and Expedition ("opens after act 3 of the Authority or Directorate campaign") appears only as a toast at the bottom of the window and fades within about a second [c11_locked.png]. The mission list has only "[next]/[locked]" tags. Mission 1's difficulty defaults to Veteran (tier 3) for a new profile [b02_briefing_full.png]; `MissionSelectScreen.cs:93` uses `GetValueOrDefault(missionId, 3)`.
**Fix idea:** Put the reason in the row text; consider Regular as the first-play default. S.

### UX-LM-10 · minor · CODE · confidence medium
**What:** The "Back" label on How to Play and About is wider than its box and pokes past the frame.
**Where:** `HowToPlayScreen.cs`, `AboutScreen.cs`.
**Evidence:** [h01_howto.png]; the About capture was not kept.
**Fix idea:** Size the button from the measured label. S.

### UX-LM-11 · minor · TEXT · confidence high
**What:** A locked Archive unit reads "Unlocks in the campaign (auth_02)." The player does not know raw mission ids.
**Where:** `src/Game/Archive/ArchiveUnlock.cs:48`.
**Evidence:** Archive > Unit, sixth entry; seen live, capture not kept.
**Fix idea:** Resolve the id to the mission title. S.

### UX-LM-12 · minor · CODE · confidence medium
**What:** List screens draw help text in the same style as rows. Save game shows "New save: save 1", "Quicksave slot", "Writes a new save slot.", "Choose a slot." as if they were four choices [p02_save.png]. An empty Load list highlights "No saved games yet." as a selectable row [w07_campaign_load.png].
**Where:** `ListScreen.cs`, `PauseScreen.cs:44-55`, `LoadGameScreen.cs:19-21`.
**Fix idea:** Dim the detail and footer text; make the empty message non-selectable. S.

### UX-LM-13 · minor · TEXT · confidence high
**What:** The Tutorial briefing has a hard line break mid-sentence ("...the practice" / "target is due east..."), so the paragraph wraps badly.
**Where:** `data/missions/tut_01/briefing.md:17-18`. I saw it live, capture lost.
**Fix idea:** Join the two lines. S.

### UX-LM-14 · minor · CODE · confidence medium
**What:** The Map Editor opens with a red "Validation: 1 error / E starts_.." that is cut off at the right edge. The toolbar sits flush with the top of the window, the status line "rust_belt_new 64x64" is half cut off at the bottom, and group labels are overlapped by buttons.
**Where:** `src/Game/Screens/Editor/*`.
**Evidence:** [e01_editor.png].
**Fix idea:** Validate only after the first edit; add padding. S.

### UX-LM-15 · minor · TEXT · confidence high
**What:** Settings > Controls lists sandbox and spectator keys with the player keys ("Sandbox: occupancy overlay", "Sandbox: step the simulation once", "Spectator: speed 8x"). The "F1 or ?" row says "F1 opens the console in the sandbox". Four setting rows share one list with the key table.
**Where:** `src/Game/Controls/GameKeys.cs:70-72` and neighbours; [c01_controls.png] shows the mixed rows.
**Fix idea:** Group the debug keys under "Developer" or hide them outside a debug build. S.

### UX-LM-16 · idea · DESIGN · confidence low
**What:** Settings is one flat page with no "Reset to defaults" and no revert prompt after a Full screen change. UI 150% and 175% look identical at 720p because of the "(limited by window size)" clamp, so those steps do nothing visible. Volume is an ASCII "[####---]" bar with -/+ buttons.
**Where:** `SettingsScreen.cs:36-60`.
**Not claimed:** the first mouse click on a main-menu item sometimes only highlighted it (seen about four times). I could not separate a game bug from the harness.
**Fix idea:** Add a Reset row and skip no-op scale steps. S.

## Open questions
- Is the missing scale clamp on the main menu (UX-LM-01) deliberate?
- Should `test_32` and `test_64` be in the player skirmish list?
- Is default mission difficulty 3 (Veteran) a decision, or a leftover from the pre-tier design?
- Why does the replay summary say "3 players" for a 2-slot match?
- The Human Victory/Defeat/Debrief/Ending screens and the other three mission lists need a profile with campaign progress, so they are unverified live. The next reviewer could seed `home/.config/RTS_2027/` campaign progress.

## Things checked and found sound
- Main menu at 100%: readable, Campaign preselected, Continue greyed with a reason before the first save and enabled after.
- Keyboard: arrows, Enter and Esc work on Settings, setup and pause. Back or Esc exists on every screen except the main menu, where Esc quits.
- Settings persist (settings.toml written on leaving Settings); UI scale and text size apply at once; the Settings page fits at 200%/XL.
- Save/load round trip: new save, quit to menu, Continue loads the match; Load Skirmish lists the save.
- Archive, How to Play, Replays and the replay viewer are clear and well made.
- Briefing: the typewriter text scrolls, the wheel scrolls back, and objectives and difficulty are shown.
- No exception across about 15 screens and a 2-AI 8x spectator match played to its end; the replay was written.
- 1600x900 window: main menu, Campaign, Skirmish and Load screens scale cleanly.

FILE: none written (Write tool refused). Text above; screenshots in /home/claude/audit/ux-live-menus/shots/
COUNTS: critical 0 / major 4 / minor 10 / idea 2
TOP 5: UX-LM-01 major main menu clips at UI 200%/XL (Quit lost, caption overlaps); UX-LM-02 major skirmish setup broken at 200%/XL; UX-LM-03 major slot rows hide the team tag, panel resizes under the cursor, silent Spectate; UX-LM-04 major no confirmation on destructive exits and Esc quits from the main menu; UX-LM-05 minor Save replay gives no feedback.
NOT COVERED: human Victory/Defeat/Debrief/Ending screens; Directorate, Left Behind and Expedition mission lists; stills; real fullscreen and window-resize behaviour (no window manager here); LLM key entry; the Quit button (only Esc-to-exit was seen).