# Session log
One short entry per session, newest last. Only the latest entry is read on resume.

## Session 1 · 2026-11-02
**Goal:** Bootstrap the project: handoff docs, clone the EA reference source, study it, research RTS
architecture / maps / resources / AI / campaign / art / technology, study five more open-source RTS
repos the owner supplied, synthesise, and run the design rounds.
**Done:** Handoff doc set, GPL v3 LICENSE, LICENSING.md with EA additional-terms rules, CLAUDE.md.
19 research files in `docs/research/` (01–06 EA code, 10–17 web, 20–24 other repos) plus
`00_SYNTHESIS.md`. Three design rounds completed in chat; decisions D-009..D-034 logged; design docs
`docs/design/01_architecture.md`, `02_game_design.md`, `03_setting_and_factions.md` written.
Setting chosen: alt-history atompunk (1962 exchange, set c. 2001); resource: ashfall + pitchblende;
factions: Continental Reconstruction Authority vs Directorate of the Interior.
**Commits:** 083c46c..(wrap-up commit), ids M0-01..M0-10b.
**Decisions:** D-001..D-035. D-033 is "assumed — confirm". D-006 was lead-proposed and the owner did not
object when told; treated as confirmed.
**Helpers:** 14 first wave (6 Sonnet code studies, 7 Haiku web research, 1 Sonnet technology options)
+ 5 second wave (2 Sonnet, 3 Haiku) + 1 Sonnet fact-check at wrap-up. No escalations. One launch
blocked by the auto-mode classifier for proposing to execute repo code; relaunched read-only.
**Reference clones (cloud workspace, read-only):** `/home/claude/ref/CnC_Remastered_Collection`,
`openage`, `freeaoe`, `ageServer`, `AOEMods.Essence`, `aoc-mgz`.
**Problems / notes:** Haiku helpers hit bot walls on Spring/0 A.D./OpenRA wikis; affected facts are
marked unverified in the files. Several research files run over the word target because of tables.
**User check:** see USER_CHECKS.md, Session 1.
**Next session should:** 1) ask about the setting doc and D-033; 2) M0-11 repo skeleton; 3) M0-12 art
pilot; 4) checkpoint.

## Session 2 · 2026-11-02 → 2026-11-03 (overnight)
**Goal:** M0-11 repo skeleton, M0-12 art pilot, then (owner's call) M1 planning, art polish and all of M1
in one unattended overnight session.
**Done:** .NET 10 solution with deterministic `Sim` (Fixed32, facings, slot arrays, Step, xxHash64 state
hash, save, replay), Roslyn determinism rules test, TOML rules + rules hash, Tools CLI, MonoGame window,
Windows CI (M0-11). Art pilot: one original tank in three arms at 48/64 px; owner picked arm B at 64 px
(D-039); v2 polish (M1-ART-01, D-043). M1 planned in four waves and built: map model/files/generator
(M1-01), rules schema v1 (M1-02), missions + infantry (M1-03), debug view on the real sim (M1-04), path
service (M1-05), buildings + one-cell gap rule (M1-06), fog + ghosts (M1-07), movement/blocking/crushing
(M1-08), combat (M1-09), desync tooling + headless match (M1-10), battle sandbox + overlays (M1-11).
**Commits:** 76b3983..(handoff commit), ids M0-11, M0-12, M1-ART-01, M1-01..M1-11.
**Decisions:** D-036..D-047. D-043..D-047 are "assumed — confirm" (overnight choices).
**Helpers:** 18 launches + 1 follow-up: Sonnet 11 (math, sim core, determinism rules, art, A0 migration,
map, missions, paths, buildings, movement, combat), Haiku 7 (data/CLI, game window + CI, rules schema,
debug view, fog, desync tooling, sandbox). No failed rounds, no escalations.
**Problems / notes:** tag pushes blocked by the workspace proxy (restore point is branch `restore/pre-m1`);
Windows CI caught a line-ending test bug (fixed with `.gitattributes`); a helper scheduled a stray "noop"
message (harmless, fired once). Headless match on test_64 favours the (4,4) start corner (geometric; mirror
duels are exactly symmetric) → M2-01.
**User check:** see USER_CHECKS.md, Session 2 (M1 block).
**Next session should:** owner tries the sandbox, confirm D-043..D-047, M2-01, plan M2.

## Session 3 · 2026-11-03
**Goal:** resume, confirm overnight decisions, adversarial review, M1.5 hardening, then all of M2.
**Done:** D-043..D-047 confirmed (D-049). Five-lens adversarial review (docs/reviews/2026-11-03_adversarial) with lead
verification; MonoGame licence finding corrected → D-050 (never copy EA code), D-055 (GPL §7 linking permission), D-060
(GPL-3.0-or-later, SPDX headers). M1.5 hardening (relations/teams/neutral, player state, actor view, boundary guards,
replay/save v6, publish + notices, test fixtures + golden, AI host + tier harness, debug console). M2 complete
(M2-01..M2-20): schema v2, SimHost + screen stack + menus, Noto UI font, GPU house colours, economy, production,
Authority roster, Rust Belt map, sidebar + minimap + real skirmish, Recruit AI, simultaneous damage, sprite rendering,
real Blender art for all 13 types with owner-approved pilots (Refinery, Rifleman), pixel-art effects, balance pass,
campaign pilot (MoonSharp host + "Survey" mission), sim fixes, art polish. Decisions D-049..D-072.
**Commits:** f10ac5f..(handoff commit); PRs #1..#6 (Windows CI before every landing).
**Helpers:** ≈ 40 launches (reviewers 5; builders mostly Sonnet, Haiku for data/fonts/console/map/publish). Art helpers
repeatedly ran out of turns waiting on the shared Blender lock; the lead finished three of them. One self-deadlock on the
lock (build_infantry re-locking under an outer flock) — fixed. One helper created a stray one-shot scheduled task (fired
once, harmless).
**Problems / notes:** LFS uploads blocked by the workspace proxy (owner converts). Windows CRLF broke raw-string tests
twice (now normalised; whole suite verified with CRLF test files). Mirror AI games were perfectly symmetric after D-065,
so Recruit gained seeded variation. Pilot cost: ≈ 10–12 h per future mission.
**User check:** USER_CHECKS.md, "Session 3 · M2 playable skirmish slice".
**Next session should:** owner play-test report → fixes; plan M3 (Directorate faction needs Q-29 tunnel spec; audio Q-31).

## Session 4 · 2026-11-03 → 2026-11-04 (evening into overnight)
**Goal:** play-test report, M3 planning with the owner, then a large overnight M3 build.
**Done:** owner play-test ("looks awesome") and Git LFS conversion; D-063 confirmed; M3 plan (docs/design/05_m3_plan.md)
with the tunnel spec, hardening + gates, radiation/Blackout, Directorate roster, audio sourcing (D-073..D-081). M3 wave 0
(contracts) and wave 1 (M3-02..M3-09) merged with lead integration fixes; PR #7 opened for Windows CI. Wave 2: M3-10
Directorate AI, M3-12 follow-ups (mission replays deferred), M3-13 Sunrise Device, M3-17 Archive with the owner-approved
lore, M3-INT integration gaps. Art: Directorate palette, pilot (conscript, vent ×3 rounds), 4 infantry, 9 vehicles
(turret layers reworked after owner feedback), dir_shaft structure pilot rev 2 — all owner-approved except the turret
rework and the shaft (morning review). New requests logged: main menu lobby (M3-16, running at wrap-up), Archive, and an
expansion plan — 4 factions, skirmish depth, LLM commander, spectator, procedural maps (D-085..D-088).
**Commits:** 94a16b9..(handoff commit) on integ-m3; art on wp/art-* (local only until the owner runs push_art.ps1).
**Helpers:** ≈ 30 launches (Opus 3: contracts, tunnels, hardening; Sonnet ≈ 22; Haiku 2). Lock queues (Blender and the
new dotnet lock) dominated wall time; several helpers handed back while still queued, and two kept re-sending reports
until stopped.
**Problems / notes:** LFS upload blocked → D-084 morning script. Two helpers collided on FxKind = 3 (fixed: InstantHit = 4).
Lead sent M3-08's sprite-layer notes to the audio helper by mistake → M3-18. pkill with a pattern matching the lead's own
shell killed the lead's command twice — don't pkill by command line.
**User check:** USER_CHECKS.md, "Session 4 · M3 so far".
**Next session should:** see HANDOFF (finish M3, then M4 skirmish depth + new-faction design and lore overnight).

## Session 5 · 2026-11-04 (overnight, unattended)
**Goal:** land M3, finish M3, M4 skirmish depth, M6/M7 design wave.
**Done:** M3 landed (PR #7, D-097) and M3-14 balance (PR #8). M3-11, M3-18, M3-19 (D-092), M3-20 (D-093) and M3-ART (lead-reviewed,
D-091; art on local integ-art) done. M4 plan (08_m4_plan.md) with every Q-M4 answer assumed (D-095); M4-01..M4-15 built and
landed (PR #9, D-094, D-098); M4-12 AI in PR #10. M6-01/M7-01 design docs + lore drafts. Decisions D-091..D-098.
**Commits:** 1a468e9..7696ca2 on main + PR #10 (AI) + this handoff; art on local wp/art-* + integ-art (owner pushes).
**Helpers:** ≈ 30 launches (Opus: M4-02, M4-04, M4-12; Sonnet for most sim/UI/art; Haiku for data/stats).
**Problems / notes:** the dotnet lock was starved by VBCSCompiler holding the inherited fd → `flock -o` + wrapper (D-096).
Fresh worktrees had LFS pointer PNGs (Game test DivideByZero) → `git lfs pull` + `git lfs checkout`. A helper duplicated
the already-merged M3-16 menu (stopped). One helper created a one-shot "check" scheduled task (deletion was blocked; it fired
once, harmless). M4-12 worked on an older base without the M4-02 generator, so its 4-player numbers used a stand-in map.
**User check:** USER_CHECKS.md, "Session 5 · M3 finished + M4 skirmish depth".
**Next session should:** see HANDOFF (owner review + art push, M4-16 balance, M3-21, then M5 LLM commander / M6 contracts).

## Session 6 · 2026-11-04 (attended; owner answering from a phone)
**Goal:** owner review and art push, M4-16 balance, M3-21 follow-ups, then M5 LLM commander foundations.
**Done:** owner answered every open question (D-099..D-103). M4-16 round 1: five start-bias sim fixes and per-game seeds in
`ai match` (D-104), AI finish-won-games + 4-player targeting with the tech hold off, garrison counters 50 % and Seismic Mortar 28
(D-105), 8× spectator benchmark. M3-21 done (D-106; AI state in saves, header v3). Faction.Neutral (D-100). Lore approvals
(D-101). M5 plan approved (09_m5_plan.md) and M5-00/03/04/08 built: src/Llm with contracts, providers, reply parser,
fog-honest summary, LLM settings page and DPAPI key store. Landed on main via PR #11 after Windows CI (D-107).
**Commits:** 6624e50..55a3a27 on main (PR #11) + this handoff.
**Helpers:** 11 launches (Opus 1: start bias; Sonnet 9; Haiku 1: lore). Lead finished the M4-16b AI package (committed, added the
new AI fields to the save snapshot, merged).
**Problems / notes:** the art is still not on GitHub: the owner re-ran the session-4 `_art_push`; only art_objects.tar (session 5)
reached the RTS_2027 folder, rts_art.bundle and the session-5 push_art.ps1 are still missing. Remote branch deletion and a
Downloads folder request were blocked by the session's safety checks (owner deletes old branches on GitHub). Helpers were
repeatedly cut off mid-task while queued on the dotnet lock; several handed back uncommitted work and needed a resume. The
mirror-symmetric AI placement patch made AI mirrors stalemate and was reverted (D-104). The full suite moved to 1 997 tests.
**User check:** USER_CHECKS.md, "Session 6 · M4-16 round 1, M3-21, Neutral, M5 settings".
**Next session should:** see HANDOFF (art push + reworks, M5-01/02/05 sim wiring, AI re-measure).

## Session 7 · 2026-11-04 (owner at work; said "do as much as you can, you decide the order")
**Goal:** M5 sim wiring and as much of the LLM commander as possible; Q-40 model lookup; AI re-measure. Art push waits on the owner.
**Done:** Q-40 answered from the live docs: Haiku 5.5 default, Sonnet 5.5 strong, effort low, first text block after thinking (D-108).
M5-01 directive command + "directive" state part, save format 9 (D-109). IDirectiveSource on AiDriver. M5-02 DirectiveBias in the
tiered AI (behaviour tests: all-in attacks earlier, turtle later, anti-air mix and tech focus shift as asked; plain games unchanged). M5-05 commander loop + real Test connection;
M5-06 eight leader personas; M5-07 chat panel (Enter, /to N); M5-09 Cmdr toggle, save header v4, spectator "LLM" tag; M5-10 replay
and save tests, loaded saves rebuild commanders (CommanderSet). Chat lines stay out of the sim/replays (D-110, assumed). M4-16 round 2
re-measured: tier ladder flat or inverted, target not met (docs/balance/m4_16_round2.md). Landed via PR #12 after Windows CI.
**Helpers:** 6 launches (Sonnet 3: M5-02, M5-05, M5-09 retry; Haiku 3: M5-06, M5-07, M5-09). The Haiku M5-09 gave up while queued and
was replaced by Sonnet. Three helpers created one-shot "placeholder" reminders against the rules (all fired harmlessly); one tried a
device call on the owner's laptop (refused before running: no folder linked).
**Problems / notes:** an M5-05 test deadlocked while holding the shared dotnet lock and stalled every helper for ~10 min (lead killed
the testhost); LFS images were pointer files until a second `git lfs pull` (scene/menu proof tests failed until then). Two pinned
hashes and the goldens moved for the new state part only. Art still not on GitHub.
**User check:** USER_CHECKS.md, "Session 7 · M5 LLM commander".
**Next session should:** see HANDOFF (art push + reworks, owner LLM play-test, M4-16 diagnosis, M6 contracts).

## Session 8 · 2026-11-04 (art push, reworks, M4-16 round 3, M6 contracts)
**Goal:** get the session-4/5 art onto GitHub and main, the ART-S6 reworks, M4-16 diagnosis, M6 contracts.
**Done:** the owner pushed the art from the PC; six lost first-draft LFS images blocked the push (GitHub GH008), so the two draft
commits were rewritten to the current images (D-115). Art merged to main via PR #13 with renderer support for the session-4
companion turret/shadow sheets (D-114) and [house] blocks on 20 manifests. ART-S6 reworks piloted (Seismograph, owner approved)
then fanned out: Searchlight, Gate open state, Assembly Bore, Hatch cage, Rad Mortar with the radiation green (D-116, D-117);
PR #14. M4-16 round 3 diagnosis (docs/balance/m4_16_round3.md): higher tiers lose by sending small timed waves into a bigger home
army; tech spending second; round 2's 2 v 1 inversion was noise. The owner set the tuning rule (D-113). Tech hold measured: no effect.
A wave strength gate + escalation helps (Authority 2v1 50 -> 62 %, 3v2 34 -> 45 %, 4v3 26 -> 32 %, 5v4 not improved); the owner
asked to wrap up before it was made real code, so it is saved as scratch knobs on `wp/m4-16r3-wip` (untested, not on main). M6 plan
(docs/design/10_m6_plan.md) and M6-02 contracts landed via PR #15 (D-118, save format 10).
**Commits:** ed79d8c (PR #13), dfd2bc7 (PR #14), 6da7931 (PR #15), plus this handoff (which also carries the M4-16 round-3 docs and
`ai match --trace`).
**Helpers:** about 9 launches (Sonnet: companion-sheet renderer, three art rework lanes, M4-16 round 3, M6-02; pilot by the lead).
**Problems / notes:** the cloud cannot upload LFS objects (D-084), so art went through the owner's PC twice; `lfs.allowincompletepush`
was a wrong suggestion (GitHub checks server-side). A container restart killed one art helper after its render finished (lead
committed it). CommanderReplayAndSaveTests failed once under load and passed 3/3 alone (flaky under CPU pressure). `gh pr checks`
uses GraphQL, which is blocked: use `gh api repos/pwnzor11/RTS_2027/commits/<sha>/check-runs`.
**User check:** USER_CHECKS.md, "Session 8 · Art on main, reworks, M6 contracts".
**Next session should:** see HANDOFF (M4-16 fixes, M6 wave 2, owner LLM play-test).

## Session 9 · 2026-11-05 (unattended overnight: Left Behind playable, veterancy, supply, M7/M8 plans)
**Goal:** the owner's overnight plan (Project doc claude/session9_overnight_plan.md): M6 waves 1-3, M4-16 round 4, M7 plan with
supply and veterancy designs, art push, wrap-up.
**Done:** Wave 0 logged decisions and cleared the lore DRAFT banners (D-119..D-124). M6-03..08 (rigs, salvage, raiders, auras,
storm, faction data) and the M6-09 game layer, M4-16 round 4 (wave strength gate lands, D-131; Q-43 measured, within noise) and the
M6-10 Left Behind AI. Designs: docs/design/11_m7_plan.md, 12_m8_plan.md, 13_supply_cap.md, 14_veterancy.md (D-125..D-130).
Veterancy VET-1..5 (save format 11). Supply SUP-1..3 + SUP-5, held off by default (D-133) because three AI tests fail with it on.
Left Behind Archive/lore (27 entries), Expedition lore (40) and 7 personas, all assumed — confirm. Left Behind art (pilot approved by
the lead, then 14 structures, the packed rig and 16 units) on local branch `art-s9`, staged in the owner's `_art_push` folder.
Balance tables for the owner: docs/balance/s9_tables.md, m4_16_round4.md, m6_10_ai.md.
**Commits:** PR #17 (6883266, M6 sim + data + plans), PR #18 (d88515a, M6-09 + M4-16), PR #19 (wave 3), plus this handoff.
**Helpers:** about 16 launches: Sonnet for sim, AI, game layer and the two art lanes; Haiku for the tables doc; the lead wrote the
lore/design merges, SUP-5 and the merge-time fixes. The owner answered three kickoff questions before bed (D-129).
**Problems / notes:** a direct push to main was refused (use the PR merge API); one flaky Windows test fixed; the Directorate
mirror test was relaxed with measurements (draws 12 vs 13 of 32); the Second Drift could never fire (entrance range copied from the
Sunrise) — fixed; supply on slows the AI (3 tests) — parked per the guard rail.
**User check:** USER_CHECKS.md, "Session 9 · Left Behind playable, veterancy, supply cap".
**Next session should:** see HANDOFF (art PR, supply on, M6-12 acceptance, VET-6, M7-02).

## Session 10 · 2026-11-05 (supply on, veterancy AI, M6-12 acceptance, M7 naval systems)
**Goal:** HANDOFF "Start here": art PR, supply on (SUP-4/6), M6-12 acceptance, VET-6 + info panel, M7-02 contracts, then M7 water
locomotion and the Expedition skeleton. The owner confirmed every session-9 assumption and the play-test (D-134).
**Done:** Left Behind art PR #20. Wave 1 (PR #21): SUP-4 sidebar, supply on by default (D-135), VET-6 AI and the info panel (D-136),
M7-02 contracts (D-137, save format 12). Wave 2 (PR #22): M7-03 ships, M7-06 shipments, M7-07 beachhead (D-138..D-140), M6-12
acceptance (216-game soak, 0 desyncs, Left Behind golden replay), Left Behind salvage AI fix (D-141), SUP-6/VET-7 tables. Wave 3
(PR #23): M7-04 landing, M7-05 submarines, M7-08 Expedition data, M7-12 Salvo Array, M7-13 ocean maps (D-142..D-145), fixes (D-146).
Wave 4 (PR #24): M7-09 carrier, vehicle passengers, M7 UI, skirmish Ocean option, start kit/ship exit/effects/building sonar (D-147).
Owner questions Q-52..Q-54 (balance); no rules numbers of existing factions changed.
**Commits:** PR #20, #21 (8621f5d), #22 (9cae9e1), #23 (d8cc460), #24, plus this handoff.
**Helpers:** about 24 launches, all Sonnet except fix10 (Haiku); the lead merged, resolved two trivial conflicts, filled the Salvo
data and wrote the docs.
**Problems / notes:** one helper created a stray scheduled task ("x") despite the brief (refused to act on it for the helper); a
Windows-only SfxrTests flake (Python probe timeout) failed PR #23 once, fixed; CommanderReplayAndSaveTests flake fixed for good
(waits on the request task). Balance: Left Behind early income 20-31 % under the others (Q-52); supply win-rate spread unresolved
within noise (Q-54); rank 3 rare outside the Directorate (Q-53).
**User check:** USER_CHECKS.md, "Session 10 · Supply on, veterancy AI, naval groundwork, ocean maps".
**Next session should:** see HANDOFF (owner answers, M7-10 Expedition AI, M7-14 faction pick and acceptance, M7-11 art pilot).

## Session 11 · 2026-11-05/06 (Expedition playable: AI, faction pick, income, acceptance; Expedition art)
**Goal:** HANDOFF "Start here": owner answers, M7-10 Expedition AI, M7-14 faction pick and acceptance, M7-11 art pilot. Unattended
except for the art approval.
**Done:** owner took every recommendation (D-148; Q-52..Q-54 answered). Wave 1 (PR #26): Expedition Recruit AI with naval deploy and
tender-lane fixes (D-150, D-151), Expedition in skirmish setup with coast rules, ship/hangar sidebar, tooltip and editor-marker fixes,
Archive entries (campaign-gated secrets), LLM summary naval fields. Art pilot: owner picked variant A for both (D-149); hover-skirt and
scale fix; fan-out of all 44 remaining sprites by five helpers, lead-reviewed with fix rounds (ships), combined on `wp/art-exp-all`.
Wave 2 (PR #27): Expedition income tuned (D-152) and naval strikes; Expedition golden replay with save/load; 210-game soak with
Expedition (0 desyncs, 0 exceptions, 0 stuck ships); Q-54 re-measure (spread 14.1 off / 13.3 on, no change).
**Commits:** PR #26 (c32b6ab), PR #27 (this handoff), art bundle `rts2027_art_push_s11.zip` for the owner.
**Helpers:** 12 launches (Sonnet: AI, art ×6, balance, acceptance; Haiku: game layer, Archive, LLM); fix rounds for the AI (×1),
Archive (×1), Glide Tank (×1), warships (×2).
**Problems / notes:** shoreline buildings fit only south coasts → Q-55 (stop-gap: South default); a stale session-10 branch name
collided (`wp/m7-14ui`); force-push refused (stale `wp/m7-arch`); device bridge disconnected, so the art goes to the owner as a zip.
§6 bullet 1 (Expedition beats every tier) is not shown yet.
**User check:** USER_CHECKS.md, "Session 11 · The Overseas Expedition is playable".
**Next session should:** see HANDOFF (art PR, Q-55, Expedition per-tier AI).

## Session 12 · 2026-11-06/07 (owner answers, M7 close-out, LLM commander tests, tier 5 fix, the whole M8 campaign)
**Goal:** HANDOFF "Start here" (art PR, Q-55, Expedition per tier), then M7 close-out and M8. Owner at home and answering all session.
**Done:** owner answered every open question (D-153..D-155, D-158..D-163). Expedition art landed (PR #28, #34 docks variant C). Wave 1-2
(PR #29-#33): floating docks (D-156), aircraft ammo and rearm, dived-sub visuals, Left Behind income fix (D-157), AI ladder levers,
Expedition AI per tier, LLM commander CLI with `--swap` and an OpenAI fix (owner ran four API tests: reliable, no strength gain; D-160,
D-161), M8-02..M8-08 mission systems and campaign screens, M8-16 ending sequence (PR #35). Tier 5 diagnosis and fix (PR #36:
army before tech; CI test-host timeout raised). Expedition per tier judged on 20-minute games (D-162, M7-18). M8-09 tutorial,
Survey Convoy on schema 2, HUD timers and locale labels, M8-13 Archive unlocks (PR #37). Owner approved the template (D-163):
first missions D1/L1/E1, M8-17 API follow-ups (Player.Funds, wins on the elimination tick), missions pass player factions to the sim
(PR #39); acts 2-4 for all four factions and DRAFT endings (PR #40). 16 campaign missions + tutorial, all bot-tested.
**Commits:** PR #28-#40 (main 5d0a54b before this handoff).
**Helpers:** about 30 launches (Sonnet for missions, AI, balance, scripting; Haiku for Archive and data); fix rounds: Archive (×1),
Player.Funds contract (×1); one container restart mid-fan-out (Left Behind and Expedition helpers resumed with their context).
**Problems / notes:** GitHub Actions minutes ran out → repo made public (D-159); Windows test host timed out twice (VSTEST_CONNECTION_TIMEOUT
600); campaign tests that assumed unwritten missions broke as missions landed (now fixture-based); `next` ids had to match campaign files
(new test). M4-16 is still open (tier 5 vs tier 1 is 55 % decisive in Authority; tier 3 still beats tier 4; docs/balance/s12_tier5_diagnosis.md).
**User check:** USER_CHECKS.md "Tutorial and Survey Convoy rework", "First missions of the Directorate, Left Behind and Expedition",
"Full campaign, all four factions"; owner review list docs/missions/s12_owner_review.md.
**Next session should:** see HANDOFF (owner campaign review, M8-14 stills, M8-15 acceptance, M4-16).

## Session 13 · 2026-11-07 (owner review answers, M8-15 acceptance, still hook and pilot, balance table, tier ladder)
**Goal:** HANDOFF "Start here": owner review answers, bot-found balance table, M8-14 still pilot, M8-15 acceptance, M4-16. Owner answered the
briefing, then the run was unattended.
**Done:** owner review applied (D-164: Strand decides, all ten writer choices, atomic battery unlock kept, endings/briefings/radio lines
signed off). M8-14: briefing still hook (`still` key, 12 ids on act 2-4 openers, height capped at a third of the window) and a two-style
pilot (A game / B poster, contact sheet sent; Q-59). Balance table for the three bot-found issues, measured only (Q-60). M8-15: shared bot
walks, 16-mission play-through test, mid-mission save/load for every mission, 42-run tier table (all won; Q-62). M4-16: tier 4 beats
tier 3 again in both factions (D-165).
**Commits:** PR #42 (land/s13-w1, CI green, not merged) and PR #43 (integ-s13 with the ladder fix and this handoff).
**Helpers:** 5 packages (still hook, still pilot art, balance, acceptance, ladder); one container restart mid-wave (helpers resumed).
**Problems / notes:** the session's safety check refused merging PRs without review, so both PRs wait for the owner; the repo moved
(remote URL updated); LFS needed the repo added to the session; the owner's folder pull failed (no git-lfs on the bridge), so the owner pulls
on Windows; a lead squash on a stale base reverted earlier merges and was caught by `git diff --stat` before anything was pushed.
**User check:** USER_CHECKS.md "Session 13 · Campaign acceptance, briefing stills hook, tier ladder".
**Next session should:** see HANDOFF (merge PRs, Q-59..Q-62, still fan-out, mission tiers, M4-16 mop-up).

