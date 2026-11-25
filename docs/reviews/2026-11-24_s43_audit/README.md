# Session 43 adversarial audit (2026-11-24)

Scope: whole game after the M13 balance finish line (D-310 freeze). 15 read-only reviewers (Sonnet), one file each in `wave1/`:
SIMCORE, SIMCOMBAT, SIMMOVE, SIMECON, AI, NETLLM, GAMEUI, GAMESYS, CAMPAIGN, ROSTER-AD, ROSTER-LE, QOL (web research), TEXT, REPO, FOLLOWUP
(status of every session-18 item plus a performance pass). Findings: 135 (2 HIGH, 50 MED, 83 LOW), all from reading code and data or from
small scripts; each fix was proven by a test on the cluster (full suite per branch, k3s-cluster `claude/rts/s43/test43.sh`).

## 1. Session-18 audit
All 26 numbered items are fixed (FOLLOWUP.md table). Loose ends: item 26's "build later" list is tracked nowhere (FOLLOWUP-01, LOW), AI-09 test
still skipped (FOLLOWUP-03, LOW).

## 2. Owner decisions (2026-11-24)
- Items 1-19 as recommended (D-311): see DECISIONS.md D-311 for the list. Items 9-11 (AI) needed a 512-game re-measure before landing (§4).
- Update check off by default + "Check now" (D-312). All 20 QoL items (D-313). Render speed-ups now, sim spatial index later (D-314).
- Art pass (D-315) and smoke without bubbles (D-316) — not audit items, same session.

## 3. Fixed without a decision (no match-outcome or wording change)
LAN: hostile progress values clamped, content hash covers stamps, lowest free peer id, dropped peers are told, LLM replies capped (NETLLM-01..03,
05, 06). Placement bounds use long arithmetic (SIMCORE-01). Save/replay format 14 (SIMCORE-02; old dev files refused cleanly). A throwing AI is
isolated instead of crashing the match (AI-07). Battle music no longer carries into the next game (GAMESYS-01). Replays go to AppData
(REPO-01). Release builds carry the tag version and run the tests (REPO-02). Corner buttons win clicks over the card at UI >= 125 %
(GAMEUI-01); disabled card buttons (GAMEUI-05); refused cameo clicks play the deny cue (GAMEUI-07). Licence docs list the shipped CC BY-SA 3.0
0 A.D. renders and the AI-assisted art (REPO-03/04; an OPEN note asks the owner to reconcile D-033 with D-188).

## 4. Balance re-measure for the economy and AI fixes (D-311 items 5-11: SIMECON-01..04, SIMCORE-03, AI-01, AI-02, ROSTER-AD-06)
Built and test-green on branch `bal-s43` (= integ-s43 at 819a2fd + wp/a43-simB + wp/a43-ai) but NOT on integ-s43: the 512-game finish-line table
(k3s-cluster `claude/rts/s43/bal43.csv`) failed on one pairing: Authority v Directorate pooled 36.5 % (s42 final 40.6 %; rust 45.1, glass 51.1,
mountains 28.1, ash 21.5); the other five pairings pass. Switching DirectorateAntiAir off changed nothing (game for game). Probes aioff43
(AI fixes off) and integ43 (no simB/AI) were running at hand-off; see docs/BACKLOG.md B-00.

## 5. Accounting (script over integ-s43 commit messages, A43 tags only; corrected after a sweep found id collisions with S18 commits)
Fixed on integ-s43 71; held on bal-s43 8 (§4); later by decision 3 (FOLLOWUP-06/07/09, D-314); open 53 — every open one is an item in
docs/BACKLOG.md (owner, s43: these are not small; several are core mechanics).

| Finding | Sev | Status | What |
|---|---|---|---|
| AI-01 | HIGH | held (bal-s43, balance re-measure failed; not on integ) | The income-stall watchdog (`EconomyRescue.Broke`) is true after 2400 ticks (120 s) with no harvester unload, a |
| AI-02 | MED | held (bal-s43, balance re-measure failed; not on integ) | `respond_to_attacks = 0` (D-287 Authority tiers 2-5, D-291 Expedition tiers 4-5) also switches off the mop-up  |
| AI-03 | LOW | open -> docs/BACKLOG.md | `focus_fire = 1` (tiers 3, 4, 5) does nothing for the Authority, Directorate and Left Behind. The Recruit AI r |
| AI-04 | LOW | open -> docs/BACKLOG.md | Tier behaviour is also keyed on the tier number in code, against the toml header ("every capability is gated o |
| AI-05 | LOW | open -> docs/BACKLOG.md | Many shipped keys are inert because no tier or faction sets them: retreat_ratio_pct, unit_reserve, grow_bags,  |
| AI-06 | LOW | open -> docs/BACKLOG.md | A malformed or edited `data/ai/difficulty.toml` is swallowed silently in three places (`LoadProfile` in Skirmi |
| AI-07 | LOW | fixed | AI exceptions are fatal to the host loop: `SimHost.StepCore` calls `ai.Collect` with no try/catch, and there a |
| CAMPAIGN-01 | MED | fixed | `Reinforce.Edge` (and the identical `SpawnEdgeBlock` used by `Reinforce.Delayed`) lays units out as a 3-wide b |
| CAMPAIGN-02 | MED | fixed | s18 #10 / FAD-04 is fixed for Rocketeer, Scraper, Silo, Bunk Gallery, Still Rig and Hauler, but the Directorat |
| CAMPAIGN-03 | MED | fixed | auth_04's allow-list contains `auth_airfield` (cost 2000), unlocked on auth_03 win, but neither aircraft that  |
| CAMPAIGN-04 | LOW | open -> docs/BACKLOG.md | the lb_02 objective says "Raise a Garage Rig and a Lookout, then train slingers". The script completes `gate`  |
| CAMPAIGN-05 | LOW | open -> docs/BACKLOG.md | lb_03 tier 5 is easier than tier 4 in the Authority column's timing and rifleman count, against the code comme |
| CAMPAIGN-06 | LOW | open -> docs/BACKLOG.md | the mission AI's build strength follows the difficulty tier in three missions only: auth_04, exp_04 and lb_04  |
| CAMPAIGN-07 | LOW | open -> docs/BACKLOG.md | still broken from s18 (wave1/game-screens.md:80). `data/images/endings/` does not exist, so all 18 ending slid |
| CAMPAIGN-08 | LOW | open -> docs/BACKLOG.md | dir_04 has zones and actor tags with the same names (`head`, `device`, `west_post`). `Objective.SetTarget`, `C |
| FOLLOWUP-01 | LOW | open -> docs/BACKLOG.md | item 26's "build later" mechanics (Minelayer mines, Combat Diver swimming, Drydock production/repair, Destroye |
| FOLLOWUP-02 | LOW | open -> docs/BACKLOG.md | the Left Behind Yard place-and-sell loop (README 2.3 "LB yard sell loop"; free Gleaner with each 600-credit Ya |
| FOLLOWUP-03 | LOW | open -> docs/BACKLOG.md | AI-09 (RepairManager flips repair on/off every 4 decisions while a building keeps losing health) is parked as  |
| FOLLOWUP-04 | LOW | open -> docs/BACKLOG.md | 2.3 row 13 (Recruit AI wiped two openings at 4-5 min) is still waiting for the "hand check" that s22 asked for |
| FOLLOWUP-05 | LOW | open -> docs/BACKLOG.md | stale ROADMAP text: M12-12b still says "Open: Glass Coast layout 1 Expedition start skew ... (M13-03)" althoug |
| FOLLOWUP-06 | MED | later (D-314) | no spatial index. Every target acquisition, splash, sonar/stealth/blackout visibility query and death clean-up |
| FOLLOWUP-07 | MED | later (D-314) | visibility predicates are O(all actors) and sit inside the per-candidate loop of AcquireTarget, making a scan  |
| FOLLOWUP-08 | LOW | open -> docs/BACKLOG.md | aura recompute is entities x aura sources every tick, with ordinal string compares per pair; harvester dock cl |
| FOLLOWUP-09 | MED | later (D-314) | depleting a single ore cell bumps `MapState.Version`, which invalidates the whole path cache and the per-locom |
| FOLLOWUP-10 | LOW | open -> docs/BACKLOG.md | path polling and release are linear scans of the request queue; a stall of hundreds of queued requests makes e |
| FOLLOWUP-11 | MED | fixed | per-frame garbage in the scene draw. (a) UnitLayerPlan.For allocates a `new List<UnitLayer>(3)` (plus its arra |
| FOLLOWUP-12 | MED | fixed | no view culling in the sprite pass and several full-store sweeps per frame at 60 fps for data that only change |
| GAMESYS-01 | MED | fixed | AudioDirector never resets `_lastCombatTick` when a new game (or an earlier save / Retry) starts. The "new gam |
| GAMESYS-02 | MED | fixed (D-312) | The Update checker phones home by default. On every launch with `check_for_updates = true` (the default) it se |
| GAMESYS-03 | LOW | open -> docs/BACKLOG.md | User-resized windows are not remembered. `Window.AllowUserResizing = true`, but nothing listens to ClientSizeC |
| GAMESYS-04 | LOW | open -> docs/BACKLOG.md | A single bad value in `campaign_<faction>.toml` (a tier outside 1..5, a non-string in `completed`, a wrong-typ |
| GAMESYS-05 | LOW | open -> docs/BACKLOG.md | Several per-frame or per-tick allocations and file polls in hot paths: |
| GAMESYS-06 | LOW | open -> docs/BACKLOG.md | `PlayVoice` creates a new `SoundEffect` per call (an OGG is fully decoded to PCM on the game thread every time |
| GAMESYS-07 | LOW | open -> docs/BACKLOG.md | The end-of-match graphs (Stats) are not part of a save. After loading a save, or a quick-load, `StatsSampler`  |
| GAMESYS-08 | LOW | open -> docs/BACKLOG.md | Saves, settings, profile, campaign and archive files are written atomically (tmp then `File.Move(overwrite)`), |
| GAMEUI-01 | MED | fixed | At UI scale >= 125 % the bottom-right map buttons (IDLE HARVESTER / IDLE FACTORY, Dive, Pack, Upgrade) overlap |
| GAMEUI-02 | MED | fixed | The superweapon button is sized for the string "SUNRISE  PAUSED" but draws "{archive title in capitals}  {stat |
| GAMEUI-03 | MED | fixed | A superweapon click on a cell the sim refuses (Directorate Sunrise has an entrance range of 8 cells) does noth |
| GAMEUI-04 | MED | fixed (with TEXT-03) | A loading tip tells players "Ctrl+F5 to F8 store a view, and F5 to F8 jump back to it." The default bookmarks  |
| GAMEUI-05 | MED | fixed | Selection-card buttons are never disabled. "Attack" (arm attack-move) is on the card for every unit selection, |
| GAMEUI-06 | MED | fixed | Repair is a toggle (second click turns it off) but nothing in the UI shows whether a building is being repaire |
| GAMEUI-07 | MED | fixed | A click on a blocked cameo (no cash, supply cap, missing prerequisite, queue full) does nothing and gives no s |
| GAMEUI-08 | LOW | open -> docs/BACKLOG.md | The quicksave/autosave failure toast is a single unwrapped line carrying the raw exception message, so it can  |
| GAMEUI-09 | LOW | open -> docs/BACKLOG.md | Button captions "Dive (D)", "Surface (D)", "PACK RIG (K)" and "UNPACK RIG (K)" are hard-coded letters; rebindi |
| GAMEUI-10 | LOW | open -> docs/BACKLOG.md | Esc on the Game Over screen goes straight to the main menu (`GameFlow.QuitToMenu`), with no confirmation and n |
| GAMEUI-11 | LOW | open -> docs/BACKLOG.md | Several selection actions fail silently. (a) `C` (select all combat units) with no combat unit replaces the se |
| GAMEUI-12 | LOW | open -> docs/BACKLOG.md | Differences a C&C/StarCraft player will trip on: (a) control groups hold units only, no structure groups (a fa |
| GAMEUI-13 | LOW | open -> docs/BACKLOG.md | The 12 sidebar cameo hotkeys default to the number pad only (7 8 9 / 4 5 6 / 1 2 3 / 0 . +). Laptop or tenkeyl |
| NETLLM-01 | MED | fixed | The join check (`ContentHash`) leaves out `data/stamps/`, but generated LAN maps are built on each peer from t |
| NETLLM-02 | MED | fixed | A client can wedge the host's tick loop with one unreliable `Progress` message. The host stores `SteppedTick`  |
| NETLLM-03 | MED | fixed | Peer ids are never reused or wrapped (`_nextPeer++` on every accepted join) but both lobby codecs reject any p |
| NETLLM-04 | MED | fixed | A commander reply that arrives more than 600 ticks (30 s of game time) after its request is silently thrown aw |
| NETLLM-05 | MED | fixed | There is no cap on the model reply size, and `ReplyParser.Parse` is quadratic on a reply with many `{`. It run |
| NETLLM-06 | MED | fixed | A peer that the host's transport drops for silence (60 s), a protocol violation or a malformed message is remo |
| NETLLM-07 | LOW | open -> docs/BACKLOG.md | The host accepts at most 16 pending (not yet Hello'd) peers, creates one for any datagram that starts with the |
| NETLLM-08 | LOW | open -> docs/BACKLOG.md | A Hello that reaches the host after the match started is never answered. The lobby (which would send "The game |
| NETLLM-09 | LOW | open -> docs/BACKLOG.md | The discovery responder answers any probe from any source address, with a reply about 6 times larger than the  |
| NETLLM-10 | LOW | open -> docs/BACKLOG.md | Each "Continue" after a desync lets play resume on diverged sims, so the next hash check (16 ticks later) repo |
| QOL-01 | MED | fixed | Setup screen starts from defaults every time. C&C Remastered added exactly this ("The game now remembers your  |
| QOL-02 | MED | fixed | Double-click/Ctrl-click selects the type only inside the screen box (Selection.SameTypeInside takes a world bo |
| QOL-03 | MED | fixed | Box select takes every own unit (Selection.Inside has no class filter), so dragging over a battle pulls Scrape |
| QOL-04 | MED | fixed | Income tracking exists only for the spectator (Ui/Spectator/IncomeTracker.cs). The player HUD shows credits an |
| QOL-05 | MED | fixed | Camera keys are CentreBase (Home), bookmarks, LastAlert, idle factory/harvester. There is no "centre on select |
| QOL-06 | MED | fixed | No hover tooltip or enemy inspect exists in play; GameScreen hover code is placement only (GameScreen.cs:1363) |
| QOL-07 | MED | fixed | Per-slot handicap, credits and start kit exist (SlotOptions.cs:14-57). No lobby option for "no superweapons /  |
| QOL-08 | MED | fixed | Space pauses (GameAction.Pause) and draws "PAUSED" (GameScreen.cs:1748). CoH3's tap-space freeze lets you give |
| QOL-09 | LOW | fixed | No ping or beacon exists (grep beacon/ping finds nothing in Ui/Controls). C&C Remastered added beacons: "Teamm |
| QOL-10 | LOW | fixed | Orders draw a ring at the click point (OrderFeedback.cs) but selected units show no line to their target. Open |
| QOL-11 | LOW | fixed | Only "select army" (C) and same-type on screen exist (GameAction list). Tempest Rising has class selects (s18  |
| QOL-12 | LOW | fixed | Alerts have 14 kinds (AlertKind.cs) but Settings.cs has no per-kind mute; only master/music/sfx/ambience volum |
| QOL-13 | LOW | fixed | No first-run flag (grep FirstRun finds none in src/Game). AoE IV Mission Zero guides new players (s18 qol-mode |
| QOL-14 | LOW | fixed | Structure placement is one click per building (GameScreen placement ghost only). AoE2 DE Shift-click lines, BA |
| QOL-15 | LOW | fixed | Repair and Sell are per building or per selection; no "repair all damaged" action in GameAction. BAR area repa |
| QOL-16 | LOW | fixed | Move sends units to one cell. BAR move line drag (s18 qol-modern O2 [R] beyondallreason.info/commands/move-lin |
| QOL-17 | LOW | fixed | No auto-group (Stormgate/BAR, s18 S2) and card clicks select one unit (s18 S4; CardRouting has no modifier han |
| QOL-18 | LOW | fixed | Elapsed time is only in diagnostics / stats (no clock in Hud.cs). Phobos/WC3 timers (s18 qol-cnc #31, qol-mode |
| QOL-19 | LOW | fixed | Archive reachable from the pause menu only (s18 L3). CameoTooltip.cs comment also still says "Cameos have no h |
| QOL-20 | LOW | fixed | Only ScreenShake and Weather toggles exist (Settings.cs:62-63 area). SC2 Classic profile, Stormgate alternate  |
| REPO-01 | MED | fixed | The installed game records replays into `replays/` relative to the working directory. The installer puts the g |
| REPO-02 | MED | fixed | Release version is not applied to the binaries. `release.yml` publishes with no `-p:Version=` and takes the ta |
| REPO-03 | MED | fixed (OPEN note D-033/D-188) | docs/LICENSING.md is stale and contradicts the repo. It says third-party assets are "recorded in docs/CREDITS. |
| REPO-04 | MED | fixed | CREDITS.md header promises "Every third-party asset, library and AI-assisted artwork is listed here (D-032, D- |
| REPO-05 | LOW | open -> docs/BACKLOG.md | The one audio test that is `[Skip]`ped is stale: `UnknownSourcesAreReported` says "owner to confirm the source |
| REPO-06 | LOW | open -> docs/BACKLOG.md | Skip census and tests that cannot fail. Explicit `Skip =` attributes: 2 (above, plus AI.Tests AiStallAuditTest |
| REPO-07 | LOW | open -> docs/BACKLOG.md | CI gaps. (a) Triggers are `push: main` and `pull_request: main` only, so a pushed branch with no PR (HANDOFF l |
| REPO-08 | LOW | open -> docs/BACKLOG.md | Large binaries not under LFS. `.gitattributes` LFS-tracks only data/sprites, art, data/audio wav, data/music o |
| REPO-09 | LOW | open -> docs/BACKLOG.md | Hard-coded personal/host paths in tooling: `/home/claude/wt/blender.lock`, `/home/claude/ref/{zeroad,wz2100,wz |
| ROSTER-AD-01 | MED | fixed | Interceptor/Bomber move at speed 8 (0.62 cells/s). That is slower than a Rifleman (12), a Jeep, and the enemy  |
| ROSTER-AD-02 | LOW | fixed | Bomber costs 600, same as Interceptor (600). Text claims it is the most expensive. |
| ROSTER-AD-03 | LOW | open -> docs/BACKLOG.md | It multiplies `build_time` of `dir_conscript`, which has no `build_time` key (0 = "derive from cost", SimRules |
| ROSTER-AD-04 | LOW | fixed | `rate_of_fire` in data is ticks between shots, so x0.9 = faster. The generated line prints "<weapon>: rate of  |
| ROSTER-AD-05 | LOW | open -> docs/BACKLOG.md | Volunteer and Reactor Crawler carry `irradiated`; its only consumer is `bonus_vs_irradiated_percent` on a warh |
| ROSTER-AD-06 | MED | held (bal-s43, balance re-measure failed; not on integ) | `DirectorateRecruitFaction` does not override `AntiAirId` (base returns null), so TierPlanner's anti-air step  |
| ROSTER-AD-07 | LOW | open -> docs/BACKLOG.md | `auth_flak_gun` uses warhead `auth_he` (`hits_allies = true`, splash 1) with `targets = "air"`. Detonate keeps |
| ROSTER-AD-08 | LOW | fixed |  |
| ROSTER-AD-09 | LOW | open -> docs/BACKLOG.md | techs.toml opens with "placeholder fixture, not balance" and defines `test_armor_up` and `test_unlock_turret`, |
| ROSTER-LE-01 | MED | fixed | Sonar Nets (800 credits, tier 2) does nothing for submarine detection. Its only effect is `exp_picket sight x1 |
| ROSTER-LE-02 | LOW | fixed | The Drydock tooltip says it "Unlocks the Cruiser, Carrier and Command Ship", but the Command Ship has `buildab |
| ROSTER-LE-03 | LOW | fixed | Several units still advertise mechanics that do not exist, and have no TodayText entry (TodayText covers only  |
| ROSTER-LE-04 | LOW | open -> docs/BACKLOG.md | `exp_provision_store` has `supply_unstocked = 5` and its comment says "+10 stocked, +5 unstocked", and game.to |
| ROSTER-LE-05 | LOW | fixed | Upgrade wording overclaims or contradicts the effect list. |
| ROSTER-LE-06 | LOW | open -> docs/BACKLOG.md | `aura_targets` on lb_tinker and exp_repair_hover is dead for repair: the repair branch ignores it and heals ev |
| ROSTER-LE-07 | LOW | fixed | Stale comments on the water buildings. exp_quay, exp_anchorage, exp_shipyard, exp_drydock say "straddles the s |
| ROSTER-LE-08 | LOW | fixed | lb_01 lists `lb_slinger` in the buildable tech although it can never be built there (tier 2 needs lb_tier2 fro |
| SIMCOMBAT-01 | MED | fixed | Capturing a charged superweapon building hands the captor the victim's charge. CaptureBuilding changes the own |
| SIMCOMBAT-02 | MED | fixed | Superweapon area damage (the blast and each Salvo missile) does not spare garrisoned soldiers. D-205 says spla |
| SIMCOMBAT-03 | LOW | fixed | The two superweapon paths disagree about dived submarines, and the blast's own header comment disagrees with t |
| SIMCOMBAT-04 | LOW | open -> docs/BACKLOG.md | A unit left with Health <= 0 at the end of RunCleanup acts for one more tick. Mission handlers and Fire have n |
| SIMCOMBAT-05 | LOW | open -> docs/BACKLOG.md | The LE-05 fix (a dived sub that fires is revealed for 5 s) makes the depth charge unusable at exactly that mom |
| SIMCOMBAT-06 | LOW | open -> docs/BACKLOG.md | Stealth timer after a shot is off by one when the reload is shortened. Fire sets `VehicleStill/InfantryStill = |
| SIMCORE-01 | MED | fixed | `CanPlace` bounds test `x0 + w > Width` overflows for a huge top-left X (or Y), so the OutOfMap guard is skipp |
| SIMCORE-02 | LOW | fixed | Save and replay format numbers were not bumped when new built-in state parts were appended after format 13 (No |
| SIMCORE-03 | LOW | held (bal-s43, balance re-measure failed; not on integ) | `ClearQueueForPlainOrder` runs before the order is validated, so a refused plain order wipes the unit's shift- |
| SIMCORE-04 | LOW | open -> docs/BACKLOG.md | Saves carry no integrity check: no trailing state hash and no end marker, and `Tick` is taken as read. A save  |
| SIMECON-01 | MED | held (bal-s43, balance re-measure failed; not on integ) | prerequisites are checked only when an item is enqueued. A unit/defence/structure/upgrade already in the queue |
| SIMECON-02 | MED | held (bal-s43, balance re-measure failed; not on integ) | the Second Drift storm is documented as "non-lethal denial ... never above level 64, so it denies building but |
| SIMECON-03 | LOW | held (bal-s43, balance re-measure failed; not on integ) | `[radiation] no_build_threshold = 64` is parsed, hashed and documented ("lowest level on which the Authority m |
| SIMECON-04 | LOW | held (bal-s43, balance re-measure failed; not on integ) | UpdateDefeat treats any live vehicle with DeploysInto >= 0 as "a base" (D-205 says: a founder, Command Ship /  |
| SIMECON-05 | LOW | open -> docs/BACKLOG.md | Left Behind lb_yard gives a free lb_gleaner on placement. Yard 600 + free Gleaner worth 600; selling the Yard  |
| SIMECON-06 | LOW | open -> docs/BACKLOG.md | money lost on a cost-reducing upgrade finishing mid-build. AdvanceLane drains `cost x (ticks+1)/total` with th |
| SIMECON-07 | LOW | open -> docs/BACKLOG.md | a Left Behind rig that finishes unpacking while the building store is full silently stays packed after spendin |
| SIMECON-08 | LOW | open -> docs/BACKLOG.md | ashfall damage is 2 per hit, scaled by (1000 - resist)/1000 with integer division and a minimum of 1, so any r |
| SIMECON-09 | LOW | open -> docs/BACKLOG.md | the Archive entry for Grapple Hooks says Hook Runners "can tear a gap through a wall for a few seconds"; the s |
| SIMMOVE-01 | HIGH | fixed | A VEHICLE that surfaces from a tunnel network is handled twice. The tunnel code (TrySurface) puts a Unit teleg |
| SIMMOVE-02 | MED | fixed | A unit underground whose destination exit has no free cell within ring 8 waits there with no limit, still coun |
| SIMMOVE-03 | LOW | fixed | Radiation damages garrisoned soldiers. RunRadiation skips only IsUnderground (Tunnel, Burrowed, Surfacing, Pas |
| TEXT-01 | MED | fixed | The approved Field Manual PDF prints 8 unit numbers that no longer match data/rules (4 units; each also repeat |
| TEXT-02 | MED | fixed | The manual's "Difficulty Tiers" page is generated from the base tier rows only and ignores the per-faction ove |
| TEXT-03 | MED | fixed | The in-game How to Play "Hotkeys" page and a loading-screen tip tell players the camera bookmarks are F5..F8.  |
| TEXT-04 | MED | fixed | Tutorial step "train" tells the player to "Select the Barracks tab". There is no Barracks tab; infantry is bui |
| TEXT-05 | MED | fixed | README rosters list 16 stale costs/tiers, and the unit count is off by one. |
| TEXT-06 | LOW | fixed | README status block claims a state older than the data: "Updated 2026-11-18 (session 34)". It still lists "AI  |
| TEXT-07 | LOW | fixed | Attack-move is described as "press A, then click" in two player-facing places, but the click is a right-click. |
| TEXT-08 | LOW | open -> docs/BACKLOG.md | Name inconsistency: Authority refinery is "Ashfall Refinery" in the Archive, sidebar, manual and README tables |
| TEXT-09 | LOW | fixed | Stale comments that contradict current behaviour: game.toml says the Expedition "is NOT playable yet (RuleCoun |
