# BACKLOG — everything promised, parked or reworded and not done (built in session 43, 2026-11-25)

**Why this exists.** The owner asked (s43) for every leftover, not only the session-43 audit's 53 open items (all LOW): "these are not small issues,
some of them are fundamental parts of gameplay and unit mechanics." A read-only sweep of all 43 sessions (SESSION_LOG + archive, HANDOFF,
DECISIONS, OPEN_QUESTIONS, ROADMAP, design/*, the s18 and s43 audits, balance/*, USER_CHECKS, code TODO/PENDING/Skip, data TODO comments)
found 108 items. Each row was checked against the code; ids here (M-, C-, A-, U-, V-, N-, T-, B-, O-) are the work ids for ROADMAP M16.
The s43 audit ids (e.g. SIMECON-07) are kept in each row so the audit README §5 links here.

**Severity is not size.** The audit rated many of these LOW because a player rarely meets them in one match; several are whole mechanics
(M-01..M-09, M-13, M-15, U-05, C-02..C-04). Treat this list as the main work of the next sessions.

## Status after session 44 (2026-11-25)
Done on main (D-317..D-322): B-00; M-01..M-18 (M-14 overlay, M-16 rule, M-13 already worked), M-20..M-26 (M-26 three off-by-ones), A-02, A-03,
U-05, U-11, C-08, C-09, O-04, O-05, O-06, B-06, B-07, T-08/A-04 (closed under D-310), M-19 (kept, D-319), A-06 (parked). O-09: 16 of 17 briefings
were already telegram style; tut_01 stays plain (assumed). T-04 not applied (D-321). AI naval use (Drydock, heavy ships, Minelayer) added, off by default (D-323).
Still open: everything else below, including the correctness wave rows not listed here, the art wave, and the owner-only tests (O-01..O-03, O-08, O-10).

## Status after session 45 (2026-11-25)
Done on main with PR (D-324..D-327): M-27, M-30, M-31 (Salvo Array and Carrier hangar comments; exit cells kept), C-01, C-05, C-06 (park-only Rig; KeepAlive
needs a sim hook, later), C-07, C-10, U-01..U-04, U-07..U-10, U-14, V-03, V-04, V-05, V-06, N-01..N-05, T-01..T-03, T-05..T-07 (T-06: ArchiveProgress
TODO and ComingLater removed; SuperweaponManager PENDING and ExitNotImplemented kept, still used). New: B-08 `damage_taken_permille` attribute;
the Repair Hover list fix (D-325). Waiting: U-06 (Q-91), T-04 (D-321), balance gate (Q-93).

## 0. Before any of it: the balance gate and the freeze
- **B-00 (first, small):** D-311 items 5-11 (production pause on a lost prerequisite, non-lethal Second Drift storm + no_build_threshold,
  Bore not a base, refused order keeps the queue, AI rebuild while broke, mop-up without respond_to_attacks, Directorate anti-air) live on
  `bal-s43` (test-green) but failed the 512-game gate on one pairing: Authority v Directorate 36.5 % pooled (s42 final 40.6 %). DirectorateAntiAir
  alone: no effect. Probes `aioff43` (bal-s43 with all AiFixes off) and `integ43` (integ-s43, neither branch) were running on the cluster at
  hand-off (k3s-cluster `claude/rts/s43/`, out `/work/out/<job>`; tally `/work/in/s41/tally41.py /work/out/<job> 0 31`). Read them first: they
  say whether the economy fixes, the AI fixes or earlier merges moved the pairing. Then land what passes; bisect the rest.
  **Result:** integ-s43 40.5 % (unchanged from s42), economy only 38.5 %, economy + AI 36.5 % — see HANDOFF step 1 for the bisect plan.
- **Freeze:** D-310 froze balance numbers. Most mechanics below change how matches play (mines, diver, depth charge, tether, breach, armour
  upgrades). Ask the owner for one decision that allows mechanic work under the freeze, with a full 512-game re-measure at the end of the
  mechanics wave (the D-305 finish line as the gate), instead of asking per item.

## Suggested order (sessions 44+)
1. **Session 44 opening (lead, one question batch):** B-00 result; the freeze decision above; build-or-drop for each "built later" mechanic
   (M-01..M-09, M-12, M-13, M-15); M-19 yard loop; M-20/M-22/M-25 intent; U-05 C&C habits; U-11 refinery name; C-08/C-09 campaign tiers;
   A-03 repair flip; O-04 licences; O-06 s43 assumptions; A-06 layout 2 finish-or-drop; C-02..C-04 campaign scope (later sessions).
2. **Mechanics wave (big; Sonnet/Opus helpers, sim + data + art + text per mechanic):** the s18 build-later list M-01..M-09, then M-10..M-18,
   M-20, M-22, M-25, M-26. Each mechanic: sim rule + data + AI use + tooltip/Archive text (remove the "cannot ... yet" lines in src/Game/Ui/Sidebar/TodayText.cs) +
   art if needed + tests + campaign check. Re-measure balance at the end (gate D-305).
3. **Correctness wave (Haiku/Sonnet, no outcome change):** M-21, M-23, M-24, M-27, M-30, M-31, C-05..C-07, C-10, A-02, U-01..U-04, U-06..U-10,
   U-14, V-06, N-01..N-05, T-01..T-08.
4. **Art wave (cluster):** V-03 (Expedition + Left Behind infantry; pilot method on main), C-01 ending slides (18), V-04/V-05.
5. **Later, own sessions:** M-28 sim spatial index (D-314); C-02 campaign expansion, C-03 choice points, C-04 co-op; A-06 layout 2;
   A-08/A-09 LLM commander; U-12/U-13 more QoL; B-01..B-03 deeper balance (needs the freeze lifted).

## 1. Mechanics / units (31 rows)
| id | what | where parked | evidence still not done | size | owner? |
|---|---|---|---|---|---|
| M-01 | Minelayer lays/sweeps sea mines | s18 item 26 (D-205), s19_text_changes.md "build later" 1; D-124 kept it ("cut last") | `TodayText.cs` exp_minelayer "cannot lay mines yet"; vehicles.toml:837 TODO M7-05; no mine type anywhere in src/Sim | L | yes: build or drop the unit |
| M-02 | Combat Diver swims in deep water; sonar-picket counter | same, build-later 2 | infantry.toml:273 TODO M7-05; `TodayText` exp_diver "cannot swim yet"; foot units cannot enter Water | M | yes |
| M-03 | Drydock produces / repairs heavy ships | same, build-later 3 | `TodayText` exp_drydock; buildings.toml:1194 comment promises repair; it is only a prerequisite (vehicles.toml:853,871,892) | M | yes |
| M-04 | Second weapon slot: Destroyer depth charge + AA, Patrol Launch gun | build-later 4; design 07 §5 | vehicles.toml:746,801 TODO M7-05; no `secondary_weapon` key in src/Sim/Rules; `TodayText` exp_destroyer "no depth charge yet" | M | no (sim, hashes change) |
| M-05 | Harpoon Wagon / Harpoon Post tether and slow the target | build-later 5 | warheads.toml:136 and vehicles.toml:444 "slow is a later sim hook"; `TodayText` lb_harpoon_wagon | M | yes (balance frozen D-310) |
| M-06 | Grapple Hooks: brief wall breach; cap plunder after x1.5 | build-later 6; SIMECON-09 | upgrades.toml:141 "later sim hook"; `TodayText` lb_up_grapple "cannot breach walls yet"; Archive still promises it | M | yes |
| M-07 | Salvage Rights raises wreck value, not only drain speed | build-later 7 | `TodayText` lb_up_salvage_rights "wreck's value is unchanged" | S | yes |
| M-08 | Scrap Plate as a real armour change covering Rotor and Kite | build-later 9 | `TodayText` lb_up_scrap_plate "No armour change. Rotors and Kites are not covered" | S | yes |
| M-09 | Patchwork true long-range repair (today 3 to 5 cells) | build-later 10 | `TodayText` lb_up_patchwork | S | yes |
| M-10 | Expedition Provision Store stocked/unstocked supply (+10 / +5, `stock_intervals`) | ROSTER-LE-04; buildings.toml:982 TODO M7-06 | game.toml:70 "no behaviour yet"; store always gives +10 | S | yes (number) |
| M-11 | `aura_targets` ignored for repair auras (Tinker, Mender heal every friendly vehicle incl. aircraft/allies) | ROSTER-LE-06 | s43 README: not changed; Effects.cs header "ignores AuraTargets" | S | yes (outcome) |
| M-12 | Landing-craft/Skyhook bay repair (units inside heal slowly) | vehicles.toml:712 TODO M7-09 | no rules field, no `bay_repair` in src | S | yes |
| M-13 | Hover harvester works Shallow cells | vehicles.toml:610 TODO M7-04 | HarvestMission has no Shallow handling | M | yes |
| M-14 | Survey Hover "reveals ash richness" (view feature) | vehicles.toml:644 TODO M7-14 | no richness view code in src/Game | S | no |
| M-15 | Landing Beacon delivered by landing craft/Skyhook to any soft cell; Pioneer/Diver build a beacon | design 07 §2.1; buildings.toml:1065, infantry.toml:261, vehicles.toml:933 | beacon is a normal structure-queue building; no airlift/beacon delivery code | M | no |
| M-16 | Deploy Command Ship/Landing Depot only on a landing-coast cell | vehicles.toml:897 TODO M7-04 | comment says not enforced by data (verify against s18 item 16 fix) | S | no |
| M-17 | Naval gunfire designating a ground cell | vehicles.toml:915 TODO M7-14 | comment only; no rule field | S | no |
| M-18 | Surfaced vs submerged armour, dive timing, torpedoes | vehicles.toml:820 TODO M7-05 | comment still present; s18 item 14 fixed part (ship_light); torpedoes absent | S | no |
| M-19 | Left Behind yard free-Gleaner loop (600 yard, free 600 harvester, 50 % refund) never decided | s18 §2.3; FOLLOWUP-02, SIMECON-05 | buildings.toml:675 `free_unit = "lb_gleaner"`; MinorRulesAuditTests pins it; no D-id says "keep" | S | yes |
| M-20 | `irradiated` tag has no consumer (Volunteer, Reactor Crawler) | ROSTER-AD-05 | no warhead sets `bonus_vs_irradiated_percent` | S | yes |
| M-21 | `dir_up_mass_conscription` multiplies a build_time that is 0 | ROSTER-AD-03 | not changed (s43 README) | S | no |
| M-22 | Flak Gun hurts own aircraft within 1 cell (`auth_he` hits_allies) | ROSTER-AD-07 | not changed | S | yes (outcome) |
| M-23 | Cost-reducing upgrade finishing mid-build never refunds the difference | SIMECON-06 | not changed | S | no |
| M-24 | LB rig finishes unpacking with the building store full and stays packed | SIMECON-07 | UnpackSiteOk does not check BuildingStoreFull | S | no |
| M-25 | Ashfall resist: any resist 1..999 gives the same 1 damage (Lead Lining does nothing) | SIMECON-08 | not changed | S | yes |
| M-26 | Unit at 0 HP acts one more tick; depth charge cannot aim at a just-fired sub; stealth timer off by one | SIMCOMBAT-04/05/06 | not changed | S | no |
| M-27 | Saves have no integrity hash or end marker | SIMCORE-04 | not changed | S | no |
| M-28 | Sim spatial index (acquire, splash, sonar/stealth/blackout scans, aura recompute, path-cache invalidation by ore depletion, linear path queue) | D-314, M15-09, FOLLOWUP-06..10 | `[ ]` M15-09; AcquireTarget still walks all 4608 slots | L | no (own session, determinism risk) |
| M-29 | D-311 items 5-11 landing (SIMECON-01..04, SIMCORE-03 sim half) | M15-04 `[~]`, HANDOFF checkpoint | on `bal-s43` / `wp/a43-simB`, not on integ-s43 (no_build_threshold 64, no QueueStamp) | M | yes: land only if bal43 stays inside D-305; assumed Second Drift threshold 24 |
| M-30 | `techs.toml` and `terrain.toml` still headed "placeholder fixture" and `test_armor_up`, `test_unlock_turret` unused | ROSTER-AD-09 | not changed | S | no |
| M-31 | Stale TODO text in data that may already be built: Salvo Array "12 missiles over 6 s" (buildings.toml:1225), Carrier hangar (vehicles.toml:876), exit cells (buildings.toml:1031,1114) | data TODO M7-xx | verify, then delete the comments | S | no |

## 2. Campaign / missions (11 rows)
| id | what | where parked | evidence still not done | size | owner? |
|---|---|---|---|---|---|
| C-01 | 18 ending slides have no pictures (dark fallback panel) | s18 game-screens.md:80; CAMPAIGN-07 | `data/images/endings/` does not exist (only `stills/`); EndingScreen.cs:97 | M (art) | yes (style) |
| C-02 | Campaign length: D-030 says 10-12 missions per faction; 4 per faction + tutorial built | design 12 §1 "M8b later expansion" | data/missions has 17 folders; no M8b line in ROADMAP | L | yes |
| C-03 | Two-way choice points that rejoin the spine (D-030) | design 12 §3.6 "not used in the slice (Q-D)" | no choice mechanism in data/campaigns/*.toml | L | yes |
| C-04 | Co-op campaign over LAN | Q-M9-n, design 20 §4.14 "possible later milestone" | no co-op code | L | yes |
| C-05 | Bonus/objective for firing the Second Drift (lb_04) and Sunrise detection | s12_owner_review.md #4, #6, "API ideas (later)" | no superweapon-fired trigger in MissionApiContract; dir_04 detects the shot by the target dying | S | no |
| C-06 | Mission API ideas: keep an enemy team alive without a hidden reserve building; `Reinforce.Rig` park-only | s12_owner_review.md "API ideas (later)" | not in docs/missions/api.md | S | no |
| C-07 | lb_02 objective says train slingers; script only checks Garage Rig + Lookout | CAMPAIGN-04 | not changed | S | no |
| C-08 | lb_03 tier 5 is easier than tier 4 (ColumnDelay {4800,4600,4400,3800,4300}, riflemen {5,6,7,8,7}) | CAMPAIGN-05 | lb_03 mission.lua:59,92 | S | yes (campaign sweep D-222) |
| C-09 | Mission AI scales with difficulty only in auth_04, exp_04, lb_04; dir_04 finale has no `AI.SetTier` | CAMPAIGN-06 | grep AI.SetTier in data/missions: 3 hits | S | yes |
| C-10 | dir_04 zone and actor-tag names clash (`head`, `device`, `west_post`) | CAMPAIGN-08 | not changed | S | no |
| C-11 | Telegram-style briefings and STOP field notes: both kept pending the owner's pick | D-284, ROADMAP M11-31 | docs/reviews/2026-11-19_text_audit.md holds both styles | S | yes |

## 3. AI (11 rows)
| id | what | where parked | evidence still not done | size | owner? |
|---|---|---|---|---|---|
| A-01 | AI-01 income-stall rebuild, AI-02 mop-up decoupled from `respond_to_attacks`, ROSTER-AD-06 Directorate anti-air | D-311 items 9-11 | commits 61bd8c48, 65f1aa94, 76550986 only on `wp/a43-ai`/`bal-s43`; same landing gate as M-29 | M | yes (re-measure) |
| A-02 | AI-03 `focus_fire` inert for Authority/Directorate/Left Behind; AI-04 tier behaviour keyed on tier number in code, not data; AI-05 inert shipped keys (`retreat_ratio_pct`, `unit_reserve`, ...); AI-06 malformed `difficulty.toml` swallowed silently | s43 README lists "fixed" | no A43 commit for any; difficulty.toml:40-44 still documents the inert keys | M | yes (changes AI play) |
| A-03 | RepairManager toggles repair every 4 decisions under fire (AI-09) | s18; FOLLOWUP-03 | `AiStallAuditTests.cs:214` `Skip = "S18 AI-09: awaiting owner decision"`; no D-id | S | yes |
| A-04 | "Recruit AI wiped two openings at 4-5 min" hand check | s22_balance_options row 13; M13-13 `[~]`; FOLLOWUP-04 | no later doc closes it | S | yes (or close by D-310) |
| A-05 | `stuck_recovery` (D-301) and `wave_reinforce_ticks` / `wave_search` (D-302) built, off | D-309, D-302 | `game.toml [movement] stuck_recovery = 0`; keys unset | S | decided off; keep as option-only |
| A-06 | Map layout 2 (all four biomes) built, off by default; table far from fair (Auth v LB 84-89 %, Dir v Exp 86 %) | D-196, ROADMAP M12-07..12 | `MapGenerator.CurrentLayout = 1`; the three owner blockers unmet | L | yes: finish or drop |
| A-07 | Map stamps (ruins, groves, craters) generator pass off by default, lobby switch only | M12-13 | `Stamps` is a lobby opt-in | S | no |
| A-08 | LLM commander directives that matter: timed orders, counter-building, split armies, richer feedback | D-161, design 15 (Raid, AttackAt, ExpandTo, Reserve exist; no strength gain, D-285) | doc says "nothing decided"; ladders equal the random control | L | yes |
| A-09 | LLM-chosen build plan ("new design round, later") | D-286 alternatives | no design doc | L | yes |
| A-10 | M5-14 model rerun on the guarded build (qwen3:30b job cm36); real-API (gpt) run waits for the owner's key (D-257) | ROADMAP M5-14 "Open" | no result file after s37 | M | yes (key) |
| A-11 | Directorate-only `siege percent` knob; 1-wide corridors between AI buildings still jam head-on (M13-40); LB v Exp loss tables (M13-22) | s15_siege "Open / next"; M13-40; s23_probes "Open" | not added; balance frozen | M | yes |

## 4. UI / UX (14 rows)
| id | what | where parked | evidence still not done | size | owner? |
|---|---|---|---|---|---|
| U-01 | Hotkey letters hard-coded in captions "Dive (D)", "Surface (D)", "PACK RIG (K)" (GAMEUI-09) | s43 audit: open | UiText.cs:153-154 still literal | S | no |
| U-02 | Silent selection actions: `C` with no army clears selection, Ctrl+n gives no message (GAMEUI-11) | s43 audit: open | no A43-GAMEUI-11 commit | S | no |
| U-03 | Save/autosave failure toast unwrapped, raw exception text (GAMEUI-08) | s43 not changed | | S | no |
| U-04 | Esc on Game Over exits to menu with no confirm (GAMEUI-10) | s43 not changed | | S | no |
| U-05 | C&C/StarCraft habit gaps: no structure control groups, Shift+number semantics, Shift+right-click on enemy replaces queue (GAMEUI-12) | s43 not changed | | M | yes |
| U-06 | Cameo hotkeys default to the numpad only; no laptop preset (GAMEUI-13) | s43 not changed | GameKeys.cs:103 | S | no |
| U-07 | Resized window not remembered; sizes up to 2560x1440 offered on any desktop (GAMESYS-03) | s43 not changed | no ClientSizeChanged handler in src | S | no |
| U-08 | One bad value in `campaign_<faction>.toml` resets all campaign progress with no notice (GAMESYS-04) | s43 not changed | | S | no |
| U-09 | End-of-match graphs lost after a load (GAMESYS-07) | s43 not changed | StatsSampler not in the save | M (save format) | no |
| U-10 | Atomic writes never flushed to disk (GAMESYS-08) | s43 not changed | no `Flush(true)` in src | S | no |
| U-11 | Authority refinery called "Ashfall Refinery" in Archive/manual/README, "Refinery" in tutorial and tips (TEXT-08) | s43 not changed | | S | yes (name) |
| U-12 | In-game tech-tree viewer; drill missions with medals | s18 README §3 top-12 #12 | no TechTree or Drill code in src/Game | M | yes |
| U-13 | About 70 of about 100 s18 QoL candidates unbuilt (only top-12 and the s43 20 are done) | wave1/qol-cnc.md, qol-modern.md | D-313 built 20; rest unscreened | L | yes (pick) |
| U-14 | GAMESYS-05 menu polls `SaveCatalog.Newest()` every 2 s and opens every save header; per-frame allocation half was fixed | s43 audit: open | only FOLLOWUP-11/12 commits | S | no |

## 5. Audio / voice / art (8 rows)
| id | what | where parked | evidence still not done | size | owner? |
|---|---|---|---|---|---|
| V-01 | Unit voice barks (select/move acknowledgements) | D-214 "Piper barks later with M8-18"; D-234 Q-83 "unit barks after the SFX picks" | sounds.toml `unit_select`/`move_ack` are click samples; no unit voice folder (data/audio/voice has 12 leader/instructor dirs) | M | no (approved order) |
| V-02 | Radio voice silent on the owner's PC | M10-27 `[~]`, D-285 | diagnostics only; root cause waits for the owner's log | S | yes (owner log) |
| V-03 | Expedition + Left Behind infantry art pass (11 units) | M15-11 `[ ]` | helper stopped by a safeguard; work uncommitted in cloud `/home/claude/wt/infB` only | M | no |
| V-04 | Art gaps from design 18 that were never built: tread-mark decals, debris chunks, effect budget/priority cap, cloud shadows, road junction/end-cap pieces, water shimmer, additive glow | design 18 §3 (most marked "no pick") | no hits for tread/junction/shimmer/cloud shadow in src; Debris only as props | M | yes (taste) |
| V-05 | Per-vehicle FX anchors (muzzle, dust) wait for "the next art pass that opens the vehicle scripts" | M10-06b, D-274 | not done | M | no |
| V-06 | `PlayVoice` decodes an OGG on the game thread every call, no voice limiter; `Song` never disposed (GAMESYS-06) | s43 not changed | | S | no |
| V-07 | Generated SFX licence "CC0 (ASSUMED, M3-INT: owner to confirm)" | CREDITS.md:34 | still assumed | S | yes |
| V-08 | Ending-slide art | see C-01 | | | |

## 6. LAN / net (5 rows)
| id | what | where parked | evidence still not done | size | owner? |
|---|---|---|---|---|---|
| N-01 | Chat kept in replays (Q-M9-g, Q-74 "at the next replay format bump") | D-110/D-222; ROADMAP M9-15 | `LanSession.cs:491` PENDING Q-M9-g-replay; replay format went to 14 in s43 with no chat block; no chat in src/Sim/Replay | M | no (decided) |
| N-02 | Pending peers never expire; any 0x52 datagram creates one (NETLLM-07) | s43 not changed | UdpTransport MaxPending 16 | S | no |
| N-03 | Hello after match start never answered (NETLLM-08) | s43 not changed | | S | no |
| N-04 | Discovery responder answers any source, 6x amplification, leaks Windows user name (NETLLM-09) | s43 not changed | | S | no |
| N-05 | Repeated "Continue" after a desync writes unlimited report folders (NETLLM-10) | s43 not changed | | S | no |

## 7. Tools / CI / repo (10 rows)
| id | what | where parked | evidence still not done | size | owner? |
|---|---|---|---|---|---|
| T-01 | `UnknownSourcesAreReported` still `[Fact(Skip=...)]` though PROVENANCE has 0 unknown (REPO-05, open) | s43 | AudioProvenanceTests.cs:120 | S | no |
| T-02 | Skip census: 4 `[ProbeFact]` env-gated tests; tests that cannot fail (REPO-06) | s43 not changed | | S | no |
| T-03 | CI runs only on push/PR to main; branch pushes and bot PRs get no run (REPO-07); HANDOFF s38 "no Windows CI since Nov 12" never explained | s43 not changed | ci.yml triggers | S | no |
| T-04 | Large binaries not in LFS: Field Manual PDF 38.6 MB, voice ogg, skins, portraits (REPO-08) | s43 not changed | .gitattributes | S | yes (history rewrite) |
| T-05 | Hard-coded `/home/claude/...` paths in art tools (REPO-09) | s43 not changed | build_infantry.py:37, wz_tracks.py:19, auth_tank_wz.py:30, terrain_sheet.py:7 | S | no |
| T-06 | Stale comments: `ArchiveProgress.cs:48` TODO M3-17 (wired in CampaignService), `SuperweaponManager.cs:18` PENDING M3-21 (done), `MainMenuModel.ComingLater` unused, `Cli.cs` ExitNotImplemented dead, ROADMAP:273 M12-12b "Open" (FOLLOWUP-05) | s43 | grep | S | no |
| T-07 | Booklet builder keeps "This part is not built yet" fallback pages | tools/booklet/full/book.py:26, part3_factions.py:22 | harmless guards | S | no |
| T-08 | Fourteen ROADMAP `[~]` rows (M4-16, M7-14, M13-13/16/19/22/23/26/28/29/37, M15-04 ...) never closed after D-310 freeze | ROADMAP | text says "laptop series pending" | S | yes (close by decision) |
| T-09 | (done s43) README §5 accounting corrected | s43 README | fixed at wrap-up | S | no |
| T-10 | Branches `integ-s40/41/42` still to delete | HANDOFF | | S | yes |

## 8. Balance follow-ups (8 rows)
| id | what | where parked | evidence still not done | size | owner? |
|---|---|---|---|---|---|
| B-01 | 24-row 35-65 % goal "stays as later work" | D-310 | Ash Auth v Dir 23.6 %, Rust Auth v LB 75.1 % (docs/balance/s42_balance.md) | L | yes (new decision to unfreeze) |
| B-02 | Per-map causes: Auth v Dir Ash 13 %, Dir v LB Rust 20 %, Dir v Exp Glass 18 % (s39 numbers) | M13-37, Q-87 (c) "24 per-map rows later" | ROADMAP M13-37 "Still open" | L | yes |
| B-03 | Late-game draws 15.4 % (Glass Coast ore exhaustion), income freezes | M13-23, M13-40 | s38 "no change" | M | yes |
| B-04 | M7-14 acceptance: Expedition AI never wins at tier 1 v Authority; §6 bullet 1 | ROADMAP M7-14 `[ ]` | s38 note | M | yes |
| B-05 | Authority tier 5 v 1 at 72-76 % (90 % target dropped D-298) | M4-16 `[~]` | closed by decision, row still open | S | no |
| B-06 | Glass Coast field density decision (Q-70) | D-208 | superseded by D-294? not stated | S | yes |
| B-07 | "assumed, confirm" balance numbers: D-290..D-294, D-304, D-306..D-309, D-311 (Second Drift 24) | USER_CHECKS s39-s43 | D-310 froze without per-item confirm | S | yes |
| B-08 | s18 §2.3 residue: tier-1 defences outranged; Salvo Array and Recruit-AI hand check | s18 README | verify only | S | yes |

## 9. Owner-only tests / decisions (11 rows)
| id | what | source | status | size | owner? |
|---|---|---|---|---|---|
| O-01 | Two-PC LAN test and a Wi-Fi test | M9-15 `[ ]` | never run | S | yes |
| O-02 | Expedition play-test (M7-14 acceptance) | M7-14 | never run | S | yes |
| O-03 | Radio voice log for M10-27 | HANDOFF | waiting | S | yes |
| O-04 | Reconcile D-033 (no CC BY-SA 3.0) with D-188 (0 A.D. props ship) | LICENSING.md:55 OPEN | undecided | S | yes |
| O-05 | Confirm SFX CC0 assumption | CREDITS.md:34 | assumed | S | yes |
| O-06 | Confirm s43 assumptions: Second Drift 24, edge reinforcements keep block depth, QoL key defaults (Z, F, PageDown, comma, Insert, F11 ...), QOL-16 drag move, QOL-03 Alt+drag | HANDOFF checkpoint | assumed | S | yes |
| O-07 | Land or switch off D-311 items 5-11 after bal43 | HANDOFF | waiting on cluster result | S | yes |
| O-08 | Approve art pass contact sheets (67 buildings, 24 vehicles, 11 infantry, terrain) | D-315 | merged, not reviewed | S | yes |
| O-09 | Pick briefing style (telegram vs plain) | D-284 | open | S | yes |
| O-10 | Hand-set par times per mission (D-289 alternative) | D-240 | deferred to play | S | yes |
| O-11 | Delete branches integ-s40/41/42 | HANDOFF | open | S | yes |
