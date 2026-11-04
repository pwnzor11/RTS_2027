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
