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


## Session 14 · 2026-11-07 (owner picks, balance numbers, 12 act-break stills, mission tiers, M11 UI overhaul)
**Goal:** HANDOFF "Start here" (merge PRs #42/#43, Q-59..Q-62, still fan-out, mission tiers, M4-16), then the owner's campaign feedback
turned into M11 (UI and input overhaul, built before M9 LAN, D-171).
**Done:** PRs #42/#43 merged at the start. Decisions D-166..D-173. Q-60 numbers (ash 20, Conscript 50 HP). Q-61 per-faction raid key.
M8-14: style C "mix" (D-169), Cycles kit v3, all 12 act-break stills. Q-62: nine missions tuned; objective targets in all 17 missions;
M8-15 table re-measured. M8-18: Wyoming Piper batch tool for voice lines, OGG voice playback. M11: owner feedback doc, adversarial UI/input
audit with screenshots, edge scroll, Controls page, full key rebinding, debug keys gated, Home/bookmarks, order rings, rally flag,
attack-move cursor, health-bar setting, power readout, no-power icon (D-170), minimap fit, calmer building animations, units 1.25x,
objective guidance (bar, marker, edge arrow, minimap ping, J/Tab/O, camera cue), bottom-centre selection card (D-172 B), polish pass.
M4-17 no-rebuild defeat rule (D-173). CI artifact retention 3/7 days. Owner's laptop ran 384 AI games (docs/balance/s14_laptop_baseline.md).
**Commits:** integ-s14 (many merges), PR #44 to main: Windows CI green, local full suite 3 254/3 254; the merge was refused by the session safety check (no review), so the owner merges it.
**Helpers:** about 20 packages (Sonnet for code and art, Haiku for small jobs); several were cut off while queued on the dotnet lock and
were finished by follow-up packages or the lead.
**Problems / notes:** the 2-CPU box made the dotnet lock the bottleneck (waits of 30-90 min); helpers that background a queued test and end
their turn report "unverified" - brief them to stay in the foreground. Helpers share /tmp/claude-0 and one overwrote another's script:
give each a private scratch dir. The mop-up AI did not help (shipped off) and D-173 barely changes the draws: Directorate draws are base
stalemates (the loser still has a shaft and factories), so the real fix is AI siege behaviour (Q-64). Bulk artifact deletion, force pushes
and installing a tool on the owner's machine were refused by the session's safety check; the owner has the artifact clean-up command.
**User check:** USER_CHECKS.md "Session 14".
**Next session should:** see HANDOFF.

## Session 15 · 2026-11-07/08 (play-test fixes, M11 batch 3, siege AI, laptop balance series, maps M12 waves 1-2, art pass and open-licence imports)
**Goal:** HANDOFF "Start here" (owner UI play-test, Q-64 siege, Q-65, M11 batch 3, M11-07/08, Q-66). The owner was present all session and added
maps (OpenRA map study) and art (OpenSAGE study, infantry, open-licence packs, Warzone/0 A.D./Kenney) along the way.
**Done:** play-test fixes (harvest/return orders, windowed edge scroll). M11 batch 3: selection toolkit, alert stack + pings + End, cursor per action,
placement ghost, cameo tooltips + shift-queue, production hold, one UI scale rule + text size. Q-66 auth_01..03 tiers. Laptop series (17 rows,
1 088 games, docs/balance/s15_laptop_series.md). Q-64 siege mode + stall/rescue fixes (draws down in most cells, Directorate v Authority not yet).
Q-65 Directorate tier 4-5 economy (5 v Authority 5: 23 -> 46 %) and Left Behind tier-3 early army cap (68 -> 53 % v Authority). Terrain look C,
M12 waves 1-2 (decals, land-linked and blocking props, map format 4, decoration pass, map report/render, old-save message). Map study of 15 OpenRA
maps (design 17) and art guidance from OpenSAGE (design 18). Look pilot (damage tiers, remains, dust, mood tint), all 18 infantry re-rendered,
imported FX/props with credits, Warzone importer. Decisions D-174..D-190.
**Commits:** PR #45-#49 merged to main (main b45630b before this handoff); the Left Behind cap and this handoff land in the next PR.
**Helpers:** about 30 packages (Sonnet for code, AI, art; one container restart, all work survived). Fix rounds: UI scale sidebar (x1), art-packs (x2).
**Problems / notes:** LFS uploads from the cloud are refused, so new sprites went through owner-run push packages (art_push_s15*.zip, bundle +
LFS objects + push_art.ps1); the first packs package missed intermediate LFS versions (collect every version in the range, not just the tip).
OpenGameArt and kenney.nl block automated reads; licences were checked from the packs' own files, GitHub copies (SPDX texts, Warzone COPYING)
and the owner's downloads. Two enum-count pins (M4/M7 contract tests) missed the new Hold command until the full suite. A Windows-only test
failure (replay file still open on delete) was caught by CI and fixed.
**User check:** USER_CHECKS.md "Session 15".
**Next session should:** see HANDOFF (Q-67 Directorate early game, M12 wave 3 zones and lanes with an income re-measure, art follow-ups).

## Session 16 · 2026-11-08/09 (Q-67 Directorate early tech, vehicle team colours, map layout 2 v5, PRs #51-#53)
**Goal:** Q-67 target band (D-191: Left Behind 3 v Directorate 3 at 50-65 % decisive, no rise in draws), the vehicle team-colour pass, and
map layout 2 (M12 waves 1-3). The owner was present all session; map and art pilots stopped for his look before any fan-out.
**Done:** Q-67: Directorate tiers 2-3 hold the Isotope Works and Rad Mortar until 18 units (D-194), tier 5 until 24 units (D-195); the owner's
64-game laptop run closed Directorate tuning (D-196). Team-colour pass: house paint on every facing of the flagged vehicles and turrets (D-192).
Map layout 2 (M12-07..M12-12): zone skeletons, per-seed variety, half-turn 2-player seeds (Medium and Large), 4-player geometry, sea, convoy
cells and landings in every biome, income trims (D-197); 2v2 teams default to 1+2 v 3+4 on layout 2 (D-193). `ai match`, `ai matrix` and
`ai timeline` take `--layout` and `--biome` (docs/balance/s16_layout2.md).
**Decisions:** D-191..D-197 (D-191 Q-67 band and run mode; D-192 vehicle team colours; D-193 2v2 default teams on layout 2; D-194 Q-67 Directorate
tiers 2-3; D-195 Directorate tier 5 ladder; D-196 Directorate tuning closed, art renders on the owner's PC; D-197 layout 2 status after v5).
**Not done / open:** Q-68 (Ash Plains fairness vs two different crossings) and Q-69 (layout 2 income lever) were open at session end; answered
at the start of session 17 as D-198 and D-199. Ash Plains Expedition stall (the AI places nothing on that skeleton; cause not found). Mountains
start skew for Directorate and Left Behind (57 % pooled larger share). Layout 2 stays off by default (`CurrentLayout = 1`): income and fairness
not yet within limits. The owner's PC re-render (branch art/pc-rerender-<date>, local only) and the owner's s15-series regression run came back after
the session; see docs/balance/s16_laptop_regression.md.
**Lessons:** render art on the owner's PC (D-196); the layout-2 income rate follows harvest distance, not field money; mirror-diagonal skeletons
favour start 1.
**PRs:** #51 (Q-67 hold, tier-5 hold, team-colour pass), #52 (map layout 2 v4, all biomes, off by default; 2v2 default teams), #53 (layout 2 v5:
coast for the Expedition, biome skeletons, half-turn 2p, income trims). All three merged to main.
**User check:** USER_CHECKS.md "Session 16".
**Note:** no wrap-up was run; this entry was written at the start of session 17 from the commits and decisions.

## Session 17 · 2026-11-09/10 (session 16 records, Expedition start units, map layout 2 v6, art baseline, layout 1 laptop baseline; PRs #54, #55)
**Goal:** close out session 16 (no wrap-up was run), fix the Ash Plains Expedition stall, build layout 2 v6 from the owner's Q-68 and Q-69 answers,
and resolve the PC re-render drift. The owner was present; the session spanned two days and two container restarts.
**Done:** session 16 log entry, user checks and laptop regression table (docs/balance/s16_laptop_regression.md). Expedition start units:
`SkirmishStart.SpawnUnits` places units the strict ring pass left over, out to ring 16 (6 new tests that fail on the old code); this fixes the
Ash Plains layout 2 stall (tests) and the same stall on Mountains layout 1 found by the baseline (checked by games, no test yet). Layout 2 v6: Ash Plains half-turn with the bridge +
causeway pair on both decks, main fields about 9 cells from the start with a harvest-distance band test, per-start harvest distance in `map report`,
Mountains starts proven exact images; layout 2 stays off by default. Laptop scripts `run_s17_layout1_base.ps1` and `run_s17_layout2.ps1`. Layout 1
baseline on the owner's laptop (16 rows, 64 games each, docs/balance/s17_layout1_baseline.md). Art: the re-render drift was stale sprites, not
scripts (four Left Behind rigs rendered before the shared pad colours changed; `auth_turret` pad-edge rounding); the owner pushed his re-render and
it is the baseline (PR #54).
**Decisions:** D-198 Ash Plains half-turn (Q-68); D-199 fields further from the refinery spot (Q-69); D-200 art baseline = the owner's PC re-render,
dark rig pad; D-201 income test counts the 10-minute point over live games only.
**Not done / open:** the owner's 64-game layout 2 v6 run (script handed over, not yet run); Q-70 (income lever: on 16 sides per biome v6 is within
10 % at 3 minutes, +13 % rust_belt and +28 % glass_coast at 5 minutes); layout 2 default decision; Mountains start skew (not the map); Glass Coast
layout 1 Expedition 55 v 5 by start and the `ai timeline` sea gap (M12-12b). Art follow-ups and the Editor props tool were not started.
**Lessons:** HANDOFF "Session 17 lessons".
**PRs:** #54 (art baseline), #55 (Expedition start units, layout 2 v6, session 16 records, baseline docs), then the handoff PR. Windows CI green on #54 and #55.
**User check:** USER_CHECKS.md "Session 17".

## Session 18 · 2026-11-10/11 (adversarial audit, clear-bug fixes, Field Manual pilot)
**Goal:** owner brief: deep code audit (bugs, issues, optimisation), adversarial UX/UI review, faction units/buildings/upgrades audit,
QoL research from other RTS games, then a 1990s-style game booklet. Three rounds of questions first; owner present throughout.
**Done:** wave 1, 18 read-only reviewers (sim x3, AI/LLM, data/mapgen/tools, scripting/missions, render/audio, screens/saves, in-game UI,
tests/CI/repo, three faction audits incl. a computed rules resolver, text/onboarding, live play of menus and matches, two QoL research
passes). Wave 2, six helpers: verification tests on the real data for most sim/faction/AI findings (Audit_ tests pin today's behaviour,
mostly with skipped twins; numbers measured by duels), fixes for about 45 clear non-sim bugs, duels and matches, a start-skew experiment, and the laptop series
`tools/measure/run_s18_audit.ps1`. Field Manual pilot (9 half-letter pages) with generator scripts; hero render mode and laptop script.
Report and decision table: docs/reviews/2026-11-11_s18_audit/README.md.
**Decisions:** D-202..D-206 (audit rules; dying actors in scripts with exp_04 balance kept; mission replays watchable only; all decision-table
recommendations taken, numbers applied now, QoL list; Field Manual).
**Biggest findings:** Expedition broken on 80-98 % of Mountains layout 1 maps; Expedition with no building never defeated; tender spawn favours
the north-west start (Glass Coast skew cause, measured); garrisoned soldiers walk out invisible; 20 units attacking a building stall all pathing
for 22.6 s; unplaceable ready structures block a lane for the match (UI soft-lock fixed, sim cancel pending); Atomic Battery never fires;
campaign tech lists leave out core units.
**Helpers:** 25 packages (Fable, Opus, Sonnet); several cut off by the 5-hour usage limit and resumed with SendMessage; helpers cannot write
report .md files, so the lead saved every hand-back from the transcripts.
**Problems / notes:** the baseline full-suite run on untouched main hit the 1-hour background limit on the loaded box; the full suite ran
once on integ-s18 instead: 4 110 tests, 4 070 passed, 40 skipped, 0 failed.
**Next session should:** see HANDOFF (M13 batch, owner's laptop series and render pilot, then M14).

## Session 19 · 2026-11-11/12 (M13 audit fix batch, Field Manual renders, README)
**Goal:** build the session 18 audit fixes (M13, D-205) in waves A, B and C, unattended; owner at work, questions by phone.
**Done:** M13-01..10, 12, 14, 15 done; M13-11 and M13-13 mostly (open items listed in ROADMAP); goldens/baselines re-pinned per batch.
Wave A: Expedition edge sea on Mountains L1, founder counts as a base, tenders spawn nearest the dock, carried units ignore orders and
selection, path stall (retarget, 4x cap, group heuristic, D-209) plus a harvester head-on deadlock and goal-held waiting fix, ready-structure
cancel with refund + AI cancel, StoreFull, Atomic Battery range 0, campaign allow-lists, LB lanes pause while packed. Wave B: mission script
fixes (MIS-06..20, owner picks), menus/window (confirms, focus pause, borderless), in-match UI (autosave, F5/F9, control groups in saves,
two-click Sell, edge scroll), splash/subs/stealth, landing craft/rigs/morph/harvester/kill value/Kite, ashfall hazard + attack ground +
power checks, naming pass and honest tooltips, numbers pass (D-208), start-side tie-breaks. Wave C: game speed, idle buttons and alerts,
cameo hotkeys, repeat production, range rings, strong/weak tooltips, stances, shift waypoints, handicap and starting conditions.
Field Manual: render fix (LB structures), all 124 hero renders approved and on main, pilot PDF in docs/field_manual. README rewritten
spoiler-free in the Gnomestead shape with the booklet link. Laptop: s18audit "before" series read; s17l2 read (Q-70).
**Decisions:** D-207..D-211 (owner picks, numbers, path cap, renders, ashfall vs campaign: detour + step-off, missions run with ash off
until the retune M13-17).
**Helpers:** about 25 packages (Sonnet; Opus for path and the campaign bisect); several fix rounds; all reports kept in the session.
**Problems / notes:** the 2-CPU box with 6-9 helpers queued every test run behind one lock (10-15 min each); local batches skipped the slow
Scripting project, so Windows CI found the campaign regressions (ashfall, path) late. lb_03 and exp_04 tier 5 are known skipped shifts until M13-17.
**Next session should:** see HANDOFF (campaign retune with ash on, after-series, follow-ups, then M14).

## Session 19p · 2026-11-11/12 (parallel to session 19: M9 + M10 design, M14-06 art scripts, M9 + M10 code overnight)
**Goal:** owner asked for the M9 LAN design round, then the M10 round, the 26 missing art scripts, a music pipeline, and finally M9 + the
code-only M10 built overnight and landed after session 19.
**Done:** design 20 (M9, D-213) and design 21 (M10, D-214), PRs #64/#65 merged; D-212 confirmations; M14-06 scripts for all 26 ids (pilot
approved, D-215, draft PR #67, laptop render script); ACE-Step 1.5 set up on the owner's CPU-only laptop (UHD 620; ~13 min of compute per
minute of music) with an overnight batch of 20 tracks (tools/audio/ace_step); overnight build (D-216): wave 1 = src/Net (codec, UDP with
reliability, discovery, lockstep core, loopback, soak) + every code-only M10 feature, PR #69 merged; wave 2 = lobby + browser + handshake +
map transfer, in-game LAN (overlay, chat, pause, drop to AI, desync reports + resync, continue vs AI, LAN replays), UDP soak, and 8 fixes from
an independent review.
**Helpers:** Sonnet for art (6 batches) and most packages, Opus for the lockstep core, in-game LAN, the review and the fixes, Haiku for music states.
**Problems / notes:** the ACE-Step batch first failed on a UTF-8 BOM in the job files, then on an emoji print under cp1252, then on ACE-Step's
600 s generation timeout (raised with ACESTEP_GENERATION_TIMEOUT). A build started while a merge was still writing files reported missing
types: never build until a merge has finished. The lan-soak repeat test failed on a fresh Windows runner (first-run "wrote <map>" note).
**Next session should:** see HANDOFF "Parallel session additions".
