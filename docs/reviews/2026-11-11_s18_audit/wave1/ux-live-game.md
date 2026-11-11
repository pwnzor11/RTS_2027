FILE: none. The harness blocked me from writing `findings.md` ("subagents should return findings as text, not write report files"), so the full findings follow below; save them as `/home/claude/audit/ux-live-game/findings.md` if you need the file. Screenshots: 31 key ones in `/home/claude/audit/ux-live-game/shots/`, the rest in `shots/extra/`.
COUNTS: critical 1 / major 4 / minor 8 / idea 1
TOP 5:
- UXG-01 critical: one structure can lock all structure building for the rest of the match. If its prerequisite is lost while it builds, it finishes invisibly and every other structure says "Place the finished structure first", with no place or cancel. Reproduced live.
- UXG-02 major: the tutorial cash trap. 3000 − Power Plant − Barracks − one Shift x5 of riflemen leaves 1900, and the 2000 Refinery stalls at $0. The only escape is Sell, which the tutorial never teaches.
- UXG-03 major: mission instructor lines are drawn under the selection card, so most tutorial instructions are unreadable.
- UXG-04 major: the tutorial's "camp" riflemen are run by a full AI and attack the player's yard during lessons 2-3.
- UXG-05 major (design, low confidence): Recruit, the easiest AI, wiped Directorate and Left Behind openings at 4-5 game minutes with tanks.
NOT COVERED: patrol, waypoints, rotation and game speed (these don't exist in the game); rally points; double-click select; pings; air and naval combat; Directorate tunnels and Bore; Expedition land combat; a 15-minute frame-time curve in a real skirmish (memory only); sound (no audio device).

All processes I started are stopped by PID (game, openbox, Xvfb :92); one openbox zombie is left, which takes no resources. Nothing was written to the repo.

# ux-live-game: live play inside a match (4 skirmish factions + tutorial) — findings

**Scope read:** I played the prebuilt Debug Game.dll (copied to `/home/claude/audit/ux-live-game/bin`; its `data/` matches the repo byte for byte) under Xvfb :92 at 1600x900, openbox and llvmpipe, driven by xdotool, with HOME set to my folder. Frame rate was 20-35 fps.
- Authority on rustbelt_m2 (64x64) against Regular, twice: one loss at about 6 min, one run testing controls.
- Directorate on rustbelt_m2 against Recruit: loss at about 5 min.
- Left Behind on generated ash_plains 96x96 against Recruit: loss at about 4-5 min.
- Expedition on a generated Rust Belt 96x96 map (Ocean North, seed 1) against Recruit: played about 6 min, never attacked.
- Tutorial played to Victory.
- Sandbox with 163 units; resize; focus loss with a key held; quicksave and load twice; memory sampling.
- Headless `--skirmish-check` at 2400/3600/4800/6000 ticks.

## Findings

### UXG-01 · critical · CODE · confidence high
**What:** If a structure's prerequisite dies or is sold while the structure is building, the finished item is hidden from the sidebar but still holds the structure lane's ready-to-place slot. The lane is then blocked for good.

**Where:**
- `src/Sim/Simulation.Production.cs:282`: the lane waits while `Ready >= 0`.
- `Simulation.Production.cs:128`: every other structure then reports `StructureWaiting`.
- `Simulation.Production.cs:605`: `BuildOptionList` skips types that fail `IsUnlocked`.
- `src/Game/Ui/Sidebar/SidebarPanel.cs:548,583`: the sidebar draws only `hud.Options`.
- `TierGrouping.cs:78-83`: no locked preview is drawn for tier 1.
- `RunProductionTick` never re-checks prerequisites.

**Repro:**
1. Authority, rustbelt_m2: build Power Plant, Barracks and Refinery.
2. Queue a War Factory (needs `auth_refinery`).
3. Sell the Refinery while the War Factory is building.

**Result:** No War Factory cameo, nothing shows READY, and every structure is greyed with "Place the finished structure first" (`shots/33_SOFTLOCK_hidden_ready_warfactory.png`). The Refinery cannot be rebuilt. Cancel needs the cameo, which isn't drawn. The money is spent. No test covers losing a prerequisite mid-build. The AI may hit the same state.

**Fix (S, UI only):** always include the lane's ready type in the options so it can be placed or cancelled; `ApplyPlace` doesn't check prerequisites. The alternative is to refund in the sim, which moves hashes. Add a test.

### UXG-02 · major · NUMBERS · confidence high
**What:** The tutorial runs the player out of money if they use the Shift x5 queue that the tooltip advertises.

**Where:** `data/missions/tut_01/mission.toml:25` (`cash = 3000`); costs Power Plant 300, Barracks 300, Rifleman 100, Refinery 2000.

**Evidence:** After one Shift-click of riflemen I had $1900. The Refinery then sat at "WAIT" with $0 (`shots/122_tut_cash_trap_refinery_wait.png`). Income is zero until the Refinery exists. The hint "give the Scraper time" is misleading. I escaped only by selling the Barracks.

**Fix (S):** start with more cash, grant cash at the harvest step, or stop the train step after one rifleman.

### UXG-03 · major · CODE · confidence high
**What:** The mission dialogue (INSTRUCTOR lines) is drawn before the selection card, at the same bottom-left spot, so the card covers it.

**Where:** `src/Game/Screens/GameScreen.cs:514-515`: `DrawHud` runs first, then `_card.Draw` on top.

**Evidence:** `shots/111_tut_instructor_hidden_by_card.png`, `extra/115`, `129`.

**Impact:** The text is unreadable whenever units are selected. This affects every campaign mission, not just the tutorial.

**Fix (S):** draw the dialogue after the card, or move it above the card.

### UXG-04 · major · DESIGN · confidence medium
**What:** The tutorial's step-7 camp riflemen belong to slot 1 with `role = "ai"`. `MissionGame.cs:106-113` gives that slot the full tier-1 AI, so the riflemen walk across the map and attack the player's yard during step 2.

**Evidence:**
- "Base under attack x5" fired during step 2; End jumped to my damaged yard with enemy riflemen beside it (`shots/110_tut_camp_guards_at_base.png`).
- The alert count reached x12 by step 3.
- The final kill count (5) shows these were the camp guards.

**Fix (S):** make the slot neutral or passive, or have the script hold the guards in place.

### UXG-05 · major · DESIGN · confidence low
**What:** Recruit, the easiest AI (`difficulty.toml [recruit]`), wipes a normal opening at 4-5 game minutes with tanks.

**Evidence:**
- Directorate defeated at about 5 min; the end table shows the AI's army value at 12450 with 38 units built (`shots/60_dir_defeat_table_misaligned.png`).
- Left Behind defeated at about 4-5 min (`shots/75_lb_ref_placed.png`).
- Headless check: Recruit kills an idle player by tick 6000.

**Caveat:** my scripted play was slow (about one action per 5-10 s). Recheck by hand before acting on it.

**Fix (owner's call):** delay Recruit's first wave, or cap its early army (`army_cap_until_tick`, D-186).

### UXG-06 · minor · CODE · confidence high
**What:** The placement ghost says "Blocked by terrain" when units stand on the spot.

**Where:** `SidebarPanel.cs:270-271` passes `MapState.IsBuildable` as the terrain test. That function (`MapState.cs:137-140`) already returns false for a vehicle or infantry, so `PlacementCheck.Cell` (`PlacementCheck.cs:38`) never reaches the unit check.

**Evidence:** `shots/39_ghost_on_units_says_terrain.png`, `shots/125_tut_ghost_units_says_terrain.png`.

**Fix (S):** pass a land-only test (terrain and overlay, no units).

### UXG-07 · minor · CODE · confidence high
**What:** The Expedition Anchorage must touch the shore. When it doesn't, the ghost just says "Blocked".

**Where:** `Simulation.Beachhead.cs:97` returns `Blocked` from the shore check; `PlacementCheck.Reason` falls through to the plain "Blocked" text.

**Evidence:** `shots/92_exp_anchorage_generic_blocked.png` and `shots/93_exp_anch_shore.png`. It took about 8 tries to find a valid spot. The tooltip says nothing about the shore.

**Fix (S):** a "Must touch the shore" reason.

### UXG-08 · minor · CODE · confidence high
**What:** The Left Behind hint "Unpack the Mother Rig to build" is drawn on top of the "Tier 2 (needs Lookout)" heading. The hint code (`SidebarPanel.cs:357-367`) assumes the grid is empty, but the locked tier previews now fill it.

**Evidence:** `shots/63_lb_sidebar_hint_overlap.png`.

### UXG-09 · minor · DESIGN · confidence high
**What:** The Expedition start gives no cue. The camera centres on land with the ship half off-screen, and there is no "press D to land" hint like the Left Behind one.

**Evidence:** `shots/80_exp_start_no_hint.png`, `shots/81_exp_scroll_up.png`.

### UXG-10 · minor · CODE · confidence high
**What:** The end-of-match table always shows INCOME/M as 0, its columns don't line up, and it overlaps the alerts and objective banner.

**Where:**
- `GameScreen.cs:493` calls `SpectatorTable.Collect` without an income tracker.
- `SpectatorTable.cs:61-67` pads columns with spaces, which doesn't work in a proportional font.

**Evidence:** `shots/60_dir_defeat_table_misaligned.png`, `shots/129_tut_end.png`.

### UXG-11 · minor · CODE · confidence medium
**What:** At full zoom-out (0.5) the view shows large off-map void, even on 96x96 maps. I didn't trace the camera clamp.

**Evidence:** `shots/97_zoom_out_max.png`, `shots/133b_zoomed_out_selection.png`.

### UXG-12 · minor · CODE · confidence high
**What:** Control groups and camera bookmarks are not saved. After loading, pressing 1 selects nothing.

**Where:** groups live only in `GameScreen.cs:1158`; nothing in `src/Game/Saves` stores them.

Save and load twice in a row was otherwise fine.

### UXG-13 · minor · TEXT · confidence medium
**What:** Placeholder text cameos are still in the default game:
- "ASSE" (Assessor), "GALL" (Bunk Gallery), "RIG" (Still Rig), "HALL" (Ration Hall).
- Large-group cards show "TANK" / "RIFL" text instead of icons.

Also, the tutorial text mentions a "lone outpost in the far south-east" (`tut_01/locale.toml:9`), but no actor in `mission.toml` matches it. I didn't check the map file.

### UXG-14 · idea · DESIGN
These don't exist in the game (`GameKeys.cs:46-95`): patrol, hold, waypoint queue, building rotation, skirmish game speed. Listed only so the owner knows they were looked for.

### PERF (not a finding)
- 20-35 fps on llvmpipe, including 163 selected units (fps 20-28). No crashes in about 75 min; stderr shows only audio-device errors.
- RSS went from 1.09 GB to 1.25 GB over 55 min of repeated matches. A fresh sandbox held flat at 1.160 GB with units fighting (`/home/claude/audit/ux-live-game/perf.log`).

## Per-faction "first 8 minutes" diary

**Authority**
- Starts with a yard and $5000 but no units; nothing to do for the first 30 s.
- Clear: tier groups, the READY cameo, the dashed build radius, green/red ghost, Shift x5 count badges.
- Not clear: where the ore is. It's off-screen to the south; I found it via the minimap.
- Run 1: an AI wave at about 6 min, then UXG-01, then defeat.
- These controls worked: box select, Ctrl+1, C (select all), A + right-click attack-move, the S/G/X card buttons, sell mode, quicksave/load, Home.

**Directorate**
- Only Reactor Vent is buildable at the start, which is clear.
- Smelter Vent sends out a drill harvester by itself; income rose fast.
- Not clear: what a Hatch is for. The underground is never explained in-match, and the cameo art is placeholder.
- Recruit's tank wave at about 4 min; defeat at about 5 min.

**Left Behind**
- The rig card's "UNPACK RIG (K)" button is good.
- In unpack site mode the red ghost gives no reason (`shots/66_lb_unpacked.png`, `67_lb_unpack_ghost_clear.png`).
- After unpacking, the rig economy chain is clear.
- Wiped at about 4-5 min.

**Expedition**
- Couldn't start on the default map. The setup message is truncated: "needs a map with a la..." (menus reviewer's area).
- No cue to deploy the ship (UXG-09).
- The Anchorage placement was a puzzle (UXG-07).
- The shipment timer works well.
- Cash piled up to $9740 with nothing to spend it on; no attack in about 6 min.

**Tutorial**
- Banner, markers and camera cues are good.
- Problems: UXG-02, UXG-03, UXG-04; a world label overlaps the banner (`shots/128_tut_camp.png`); the Victory panel offers only Restart or Quit.

## Open questions
- Can the AI hit UXG-01 too?
- Does the "outpost" exist in `test_32.map.toml`?
- Move orders seemed to fail for the debug-spawned 150-unit stack. This is probably an artefact of the spawn command, so I didn't report it.

## Things checked and found sound
- Resize mid-game, paused and unpaused.
- Focus loss while holding a key: no stuck scrolling (`shots/137_alttab_strip.png`).
- Load twice in a row.
- "Off the map" placement text.
- Alert merging (x12) and the End key jump.
- The "+N" overflow on large-group cards.
- Shift x5 queueing.
- No crash logs.
