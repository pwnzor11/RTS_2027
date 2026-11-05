# Handoff: Session 9 — M4-16 fixes, M6 wave 2 (rigs, salvage, raiders, effects, storm, data), owner LLM play-test
Last commit: 6da7931 on main (PR #15) + this handoff commit (2026-11-04)
Where things stand: all session-4/5 art and the ART-S6 reworks are on main (PRs #13, #14). M6 has its plan
(docs/design/10_m6_plan.md) and contracts (PR #15): enums, rules fields (all off), "rigs" and "salvage" parts, effects v2, save and
replay format 10 — no gameplay yet. M4-16 round 3 found the cause of the inverted ladder (docs/balance/m4_16_round3.md) but its fixes
are only partly measured and live as scratch knobs on `wp/m4-16r3-wip`. Full suite 2 134 tests (AI 102, Game 789, Llm 107, Scripting 43, Sim 1 093), green
on Linux and Windows CI. Decisions D-113..D-118.

## Start here (shown to the user as the plan; nothing starts until they say go)
1. M4-16 fixes: read docs/balance/m4_16_round3.md ("Status at stop"), take the untested knobs on `wp/m4-16r3-wip` (src/AI/Recruit/Exp.cs
   is scratch — never commit it). Measured in session 8 (64 games): tech hold has no effect (leave off); the wave strength gate +
   escalation helps (Authority 2v1 50 -> 62 %, 3v2 34 -> 45 %, 4v3 26 -> 32 %, 5v4 34 -> 28 %). Make the gate + escalation a real
   feature (tier 2+, constants `(tune)`, new fields in MilitaryManager/TierPlanner Snapshot/Restore), run AI and Game tests, then
   measure the Directorate ladder, cross and mirror cells, try scouting+harass off for elite (4v3 40 %), then the Q-43 table. Measure 64
   games per cell (32 seeds x 2 sides). AI behaviour may change freely; any rules number players see comes to the owner as a table first
   (D-113). Land via PR. Target >= 60 % per ladder cell.
2. M6 wave 2 from `integ-m6` (= main after PR #15): M6-03 rigs, M6-04 salvage, M6-05 raiders, M6-06 effects/auras, M6-07 storm,
   M6-08 faction data, in parallel worktrees (plan §4; contracts §3; D-118 as-built notes). M6-08 must add the start kit and set
   `wreck_ticks = 1200`; M6-04 must not spawn wrecks while `WreckTicks` is 0. Art pilot (Mother Rig) can follow M6-08.
3. Owner LLM play-test (USER_CHECKS session 7) if the owner has a key handy: confirm D-110, D-111, D-112.

## Half-finished work
- M4-16: diagnosis done; the wave gate + escalation is measured on Authority only, as env-var knobs on `wp/m4-16r3-wip` (not for main
  as is); 60 % reached only at 2v1. Tech hold still off on main. Q-43 table not run.
- M5: a loaded game's commanders restart their history and call count; the summary still cannot report enemy superweapon charge.
- Commander chat is game-layer only (no replay chat block) pending D-110.
- M6: Left Behind cannot be set up yet (no start kit; PlayableFactions = 2).

## Waiting on the user
- Read the M6 plan and confirm D-118 (docs/design/10_m6_plan.md).
- Play-test the LLM commander with a real key (Settings > "AI commander (LLM)...").
- Q-41 old lore DRAFT banners; Q-42 foam; Q-43 tier costs. D-105 balance numbers still "assumed — confirm".
- Delete old remote branches: `wp/m2-*`, `wp/m3-*`, `wp/m4-*`, `integ-m2*`, `integ-m3`, `integ-m4`, `integ-s6`, `integ-s7`, `wp/m3-21`,
  `wp/m5-*`, `wp/neutral`, `wp/lore`, `wp/art-*`, `integ-art`, `art-land`, `wp/m6-plan`, `wp/m6-02`, `wp/s8-handoff`; keep `restore/*`
  until the owner is happy (restore/pre-art-land, restore/pre-art-s6, restore/pre-m6-02), `integ-m6`, `wp/m4-16r3*`.
- Optional: delete the `_art_push` folder contents in the RTS_2027 folder (the art is on GitHub now).
- Scheduled tasks: old one-shot helper reminders (all fired and disabled) can be deleted in the scheduled-tasks list.

## How to run this session
- Lead: Opus for build sessions (the owner wants long unattended overnight builds); Sonnet for a pure review/question session. Helpers: Haiku default; Sonnet for sim core,
  determinism, serialisation, movement, combat, AI, scripting, and all Blender art.
- Repo clone: `/home/claude/rts_2027`; main has no branch protection, but land through a PR so Windows CI runs first
  (`gh pr create` fails — use `gh api repos/pwnzor11/RTS_2027/pulls -f title=… -f head=… -f base=main`); GitHub marks the
  PR merged when main fast-forwards to its head. Tag pushes are blocked: use `restore/<name>` branches.
- Setup: `apt-get update && apt-get install -y dotnet-sdk-10.0`; Blender `pip install bpy --break-system-packages`
  (got 5.2.3 on Python 3.13). Fonts come from apt (fonts-noto-core, OFL).
- Shallow clone: add fetch refspecs for other branches, e.g.
  `git config --add remote.origin.fetch '+refs/heads/wp/*:refs/remotes/origin/wp/*'` (and `integ-*`), so pushed
  branches get upstream tracking (the stop hook checks it).
- Helpers: worktrees under /home/claude/wt (create them yourself, from the integration branch that holds the contracts);
  wave rules in docs/process/HELPER_WAVE_RULES.md (copy docs/process/HELPER_WAVE_RULES.md to /home/claude/wt/WAVE1.md at the start of a session); build with
  `-m:1` (2 CPUs). Lead writes contracts first; helpers own files by function; expect merge-time fixes (late spawns in
  new tests, pinned hashes, golden regeneration, part-order tests).
- Art: renders take 5–20 min and share ONE lock — wrap each render in exactly one
  `flock -o /home/claude/wt/blender.lock …` (scripts must not self-lock). Art helpers run out of turns while queued: brief
  them to commit after each asset, and be ready to finish contact sheets yourself. Owner approves a pilot before any
  fan-out; send contact sheets with SendUserFile (tall sheets get scaled — split them to review).
- Windows CRLF: raw string literals in tests keep CRLF on a Windows checkout — normalise before searching for "\n".
  To check, convert all tests/*.cs to CRLF locally and run the suite.
- Sim rules: every new field hashed or allow-listed with a reason in HashCoverageTests; state parts are append-only
  (… players, economy, production, then extension parts such as "mission"); setup calls throw after the first Step (tests
  use Debug/Place commands); golden: `RTS_UPDATE_GOLDEN=1 dotnet test tests/Sim.Tests --filter Golden`.
- Never copy EA code (D-050). AI never runs inside Step (D-051). Lua only via ISimExtension (D-071). No floats or
  dictionary iteration in src/Sim. Scheduling tools stay forbidden for helpers (one created a stray task this session).
- Session 5 lessons:
  - Always `flock -o` (D-096) and install the dotnet wrapper first: /usr/local/bin/dotnet sets DOTNET_CLI_USE_MSBUILD_SERVER=0,
    MSBUILDDISABLENODEREUSE=1, UseSharedCompilation=false, then execs /usr/bin/dotnet. Without it VBCSCompiler holds the lock.
  - After cloning or creating worktrees run `git lfs pull && git lfs checkout` (pointer PNGs break Game tests).
  - Check what the remote integration branches already hold before launching a package (M3-16 was built twice).
  - Base every wave-2 worktree on the integration branch AFTER wave 1 merged (M4-12 missed the generator).
  - `git diff origin/main..` not `main..` (the local main ref goes stale after fast-forward landings).
- Session 4 lessons:
  - .NET builds also share ONE lock: wrap every dotnet build/test in `flock /home/claude/wt/dotnet.lock …` (D-090). Full
    suite ≈ 8 min; with 8 helpers queued, expect 30–60 min waits. Merge in batches and run one full suite per batch.
  - Helpers hand back while still queued, or re-send the same report many times: verify with git log, then TaskStop them.
  - Never `pkill -f` with a pattern that also matches your own shell command (killed the lead twice).
  - Shared enums are a collision point (two helpers both took FxKind = 3). Pre-assign enum values in briefs.
  - Art branches cannot be pushed from the cloud (LFS, D-084): keep them as `wp/art-*`, and at wrap-up rebuild
    `_art_push` in the owner's folder (LFS objects + bundle + push_art.ps1) through the device bridge.
  - Address SendMessage notes to the right helper id (a sprite note went to the audio helper).
  - The owner likes to see art: send every finished contact sheet for approval with a 1-line read per asset.
- Session 6 lessons:
  - Helpers get cut off mid-task while queued on the dotnet lock and hand back uncommitted work. Brief them to commit after
    each step, and when one reports "not finished", either resume it once with numbered steps or finish it yourself.
  - A helper that touches AI managers must also add any new long-lived field to that manager's Snapshot/Restore (M3-21 save
    format); the AI save→load determinism test catches it only for the paths it plays.
  - Mirror-symmetric AI placement stalemates identical AIs (D-104). Any symmetry fix in AI code needs the Directorate mirror
    test (ADirectorateMirrorAtVeteranDoesNotFreeze) and the pinned default-skirmish hash run before merging.
  - `ai match` now seeds each game of a pair separately; numbers from before session 6 are not comparable.
  - Remote branch deletion and folder requests outside RTS_2027 are blocked by the session safety checks: ask the owner.
  - The owner's RTS_2027 folder can be linked through the desktop app (device bridge); read-only checks of it are fine.
- Session 7 lessons:
  - Every helper test command gets a hang guard: `flock /home/claude/wt/dotnet.lock timeout 580 dotnet test ... --blame-hang-timeout 5m`.
    One deadlocked test (an awaited hand-made Task) held the shared lock and stalled the whole wave; kill only that testhost by pid.
  - Helpers that queue on the lock hand back early or reach for scheduling tools (three created "placeholder" reminders even
    though the brief forbade it). Brief them: run builds as FOREGROUND calls with a 600 000 ms tool timeout, rerun if still queued,
    never hand back while queued, no monitors, no scheduling, no mcp__remote-devices__* tools.
  - Haiku managed well-bounded packages (personas, chat panel) but gave up on the setup/save package while queued; Sonnet finished it.
  - After cloning, run `git lfs install` BEFORE `git lfs pull`; check `git lfs ls-files` shows `*` (not `-`) or the scene/menu proof
    tests fail with DivideByZero.
  - A worktree for measurements: `git worktree add --detach /home/claude/wt/measure <branch>`, Release build of src/Tools; each
    16-game `ai match` cell takes 1–2.5 min, the whole M4-16 set about 15 min with two lanes.
  - New sim state parts move the golden hashes, SkirmishDefaultHashTests and CombatTests' final hash; re-pin those only, and only
    after checking that the part sub-hashes other than the new one are unchanged.

- Session 8 lessons:
  - `gh pr checks` uses GraphQL, which is blocked: poll `gh api repos/pwnzor11/RTS_2027/commits/<sha>/check-runs` instead (a watcher
    on `gh pr checks` silently waits forever).
  - GitHub's GH008 hook rejects a push that references any LFS object it lacks anywhere in history; `lfs.allowincompletepush` does not
    help. Rewrite the offending commits (filter-branch index-filter to existing blobs) and check objects with the LFS batch API
    ("download", real sizes, at most 100 objects per request).
  - Building sprite manifests need a [house] block (HouseRamp.Standard) and fx anchors inside the frame, or Game tests fail.
  - `ai match` cells need 64 games (32 seeds x 2 sides): a 16-game cell resolves only about +-12 points; a 32-seed cell takes 8-10 min.
  - Background runs must use run_in_background directly; a trailing `&` inside the command is killed.
  - `ai match --trace N --game G` prints per-side army, income, tier and wave lines for one game (M4-16 diagnosis).
  - Fetch refspec for integration branches: `+refs/heads/integ-*:refs/remotes/origin/integ-*`.
  - When checking on a helper, cd back to your own worktree afterwards: the stop hook checks the current folder and will flag the
    helper's uncommitted work.
  - CommanderReplayAndSaveTests can time out under heavy CPU load (passed 3/3 alone).

## If time runs short
Do item 1 (M4-16 fixes) or start M6 wave 2 with M6-03/04/08; both fill an overnight session.
