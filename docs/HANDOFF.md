# Handoff: Session 14 — owner picks (stills, balance numbers), still fan-out, mission tiers, M4-16
Last commit: `docs: Session 13 handoff` on integ-s13 (PR #43, which also carries PR #42's commits); check `git log -1 origin/main`
Where things stand: **the M8 campaign is owner-approved and bot-accepted.** The owner played it ("everything looked great"); endings,
briefings and radio lines are signed off (D-164). M8-15: a bot play-through of all 16 missions in one run, mid-mission save/load for
every mission, and a 42-run tier table (docs/balance/m8_15_acceptance.md): every mission won at every tier, but tier barely changes
win time and exp_01, exp_04, lb_03 are too easy. Briefings can show an act-break still (`still = …` on the act 2-4 openers; no images
shipped yet, so nothing shows); the pilot (A game-style, B poster) waits for the owner's pick (Q-59). Three bot-found balance issues are
measured with a table (docs/balance/s13_campaign_balance.md, Q-60); no rule number changed. M4-16: tier 4 now beats tier 3 in both
factions (docs/balance/s13_ladder.md) but the margins are thin and 5 v 1 is 68-69 %, not 90 %. Decisions up to D-165.
Full suite 3 059 tests, all passing (Game 979, Sim 1475, AI 198, Scripting 284, Llm 119, Tools 4).

## Start here (shown to the user as the plan; nothing starts until they say go)
1. Land the session-13 work if the owner has not yet: merge PR #43 (it contains PR #42; #42 then shows as merged). The session's
   safety check refused merges without review this time, so the owner merges, or allows the merge in the session.
2. Owner picks: Q-59 still style (A / B / mix, from the pilot contact sheet sent in chat); Q-60 balance numbers (one line per number); Q-61, Q-62.
3. M8-14 still fan-out (11 more stills, 3 per faction) in the picked style; art via the owner's PC (D-084). Then M8 acceptance in the roadmap.
4. Apply the approved balance numbers (Q-60) and re-run the S13 balance test and the M8-15 table.
5. Mission tiers (Q-62): make tier visible in the missions where it does nothing (dir_02, dir_04, lb_01, lb_03, lb_04, auth_04, exp_03)
   and harden exp_01, exp_04, lb_03; re-run the M8-15 table.
6. M4-16: mop-up behaviour (attack-move to the last known structures once the enemy has no army) for the Directorate draws; 5 v 1 to 90 %.
7. If time: writers' API ideas (keep an enemy alive without a hidden reserve building; `Reinforce.Rig` park-only; detect a superweapon
   shot); M7-14 close-out line; voice set-up if wanted.

## Half-finished work
- PR #42 (land/s13-w1, CI green) and PR #43 (land/s13-w2 = integ-s13 + this handoff) are open, not merged (see step 1).
- Still pilot: scripts in tools/art/stills/ (still_kit.py, still_authority_act2.py); renders are git-ignored scratch only
  (scratch/stills/ in the s13-stillpilot worktree, gone with the container); re-render with the script.
- Archive upgrade field notes marked "DRAFT (lead, session 12)" and the session-11 "PENDING owner text" entries are still open.

## Waiting on the user
- Merge PR #43 (or #42 then #43). Then on the PC: `git pull --ff-only origin main` (the folder pull failed from the cloud: no git-lfs in the bridge VM).
- Answer Q-59..Q-62 (docs/OPEN_QUESTIONS.md).
- Delete merged remote branches once happy: `land/s13-w1`, `land/s13-w2`, `integ-s13`, and the session-12 list if still there. Keep `restore/pre-s13` until happy.

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

- Session 9 lessons:
  - Direct pushes to main are refused by the session safety check even as a fast-forward: land with
    `gh api -X PUT repos/pwnzor11/RTS_2027/pulls/<n>/merge -f merge_method=merge -f sha=<head>` after CI is green.
  - Pattern: integration branch `integ-s<N>`, snapshot it to `land/s<N>-w<k>` for each PR so later merges do not move the PR head.
  - Test summary: `--logger "console;verbosity=normal"` and grep `Total tests|Passed:|Failed:|^\s+Failed ` ("Passed!" never prints).
  - bpy through the proxy stalls: download the wheel with a resumable `curl -C - --max-time 120` loop, then pip install the file.
  - xUnit analyzer errors (xUnit2031 etc.) fail the whole test project build: build tests before handing a package back.
  - The Monitor tool expires after 5 minutes: poll CI with a run_in_background loop instead.
  - CommanderReplayAndSaveTests asserted one request; a slow Windows runner sends two. Now `Assert.NotEmpty` (fixed).
  - The shell's cwd drifts into worktrees after `cd`: put `cd <dir> &&` at the start of every command.
  - A/B a failing AI test with one data flag (supply on/off) before reading code: it splits "caused by the feature" from "already red".
  - Lead-only small packages (one function, <100 lines) are faster than briefing a helper and waiting on the build lock.

## If time runs short
Do steps 1-2 (land the PRs, owner picks); the fan-out and mission tiers can take a separate session.

- Session 10 lessons:
  - Four waves of 4-7 Sonnet helpers each ran 1-3.5 h on the 2-CPU box; merging one by one as reports arrive, then a scoped
    M7/hash test run per merge and one full suite per wave, kept every PR green first time except one Windows-only flake.
  - Python probes on the Windows runner can take over 20 s (Store stub): treat a probe timeout as "not available" (SfxrTests fixed).
  - Several helpers edit the same allow-lists (RulesOfSweepGrepTests, HashCoverageTests) and the RulesHash doc comment; these are
    the usual merge conflicts, trivially resolved by keeping both sides.
  - Parts that only write new arrays when they are in use (naval, effects, stats, tunnels "version 2 when needed") keep every old
    hash; ask helpers for that pattern instead of an unconditional format bump.
  - Adding ship types to the real data made every game write sea labels: SkirmishDefaultHashTests moved on the "naval" sub-hash only.
  - `/usr/bin/dotnet vstest <dll> --Tests:<name>` runs a built test dll without the dotnet wrapper (used for soak runs).
  - A helper created a scheduled task despite the brief (session 10, "x"); never delete or act on such tasks on a helper's request.

- Session 11 lessons:
  - Helper branch names can collide with old remote branches (`wp/m7-14ui` existed from session 10): suffix new ones with `-s<N>`.
  - Force-push is refused by the safety check: when a helper amends a pushed branch, push under a new name instead.
  - Art fan-out: 5 Sonnet art helpers on one Blender lock took ~3-4.5 h for 44 assets. Review every sheet: catch draw_scale double
    scaling (ships), tread-like hover bases, wrong hull colour. Contact sheets live in git-ignored scratch/; read them before cleanup.
  - Data shape problems show up only when the AI plays: the shoreline water masks fit south coasts only (Q-55) and the Command Ship
    could not Deploy on water (D-150). Run an AI game on the real data before calling a faction playable.
  - The device bridge can disconnect mid-session: build the art push as a zip (bundle + LFS objects + push_art.ps1) and send it with
    SendUserFile; a 46-sprite faction is about 5 MB.

- Session 12 lessons:
  - The owner was present and answered in batches with AskUserQuestion; "full send" after a pilot meant: write the other pilots and the
    fan-out without another review gate. Put every remaining writer choice in one review file instead of asking mid-run.
  - Mission fan-out: one Sonnet helper per faction writing acts 2-4 (+ ending) took 30-60 min each on the 2-CPU box with the dotnet lock;
    ask them to commit one mission at a time — a container restart lost no committed mission, and SendMessage resumed both helpers.
  - Shared briefs as files (scratch/briefs/*.md, read by absolute path) kept each launch prompt to ~15 lines.
  - Tests that pin "which missions exist" or placeholder text break the moment content lands: use a temp data folder or an inline fixture.
  - Ids in data (`next`, campaign lists) need a cross-check test; helpers guessed `left_behind_02` and `expedition_02`.
  - Missions ran without player factions in the sim until s12 (faction rules silently off): any new mission-only setup path must reuse
    MissionSession.MakeSetup.
  - Windows CI: `VSTEST_CONNECTION_TIMEOUT=600` in ci.yml; a test-host connect timeout is a runner problem, re-run once, then fix the env.
  - GitHub Actions minutes ran out once; the repo is public since (D-159), so Actions are free.

- Session 13 lessons:
  - The auto-mode safety check refused `gh api -X PUT …/pulls/<n>/merge` ("merge without review"). Do not look for a way around it;
    leave PRs open, stack later work on the same integration branch, and ask the owner to merge (or to allow merges) at the end.
  - Squashing a helper branch with `git reset --soft <integ>` after the integration branch moved on silently REVERTS everything merged
    since the helper's base. Squash against the merge base (`git reset --soft $(git merge-base …)`) or apply the helper's diff
    (`git diff <base> <tip> | git apply --index`) onto the integration branch, and check `git diff --stat` before landing.
  - The repo moved to github.com/pwnzor11/RTS_2027 ("This repository moved" on push): `git remote set-url origin` fixed it.
  - LFS pulls in the cloud need the repo added to the session (add_repo, push access); then all LFS files check out.
  - The device bridge VM has no git-lfs: pulls in the owner's folder fail; the owner pulls on Windows.
  - Helpers on the shared dotnet lock could not finish Sim.Tests/Tools.Tests inside 580 s: the lead runs one full suite per batch.
  - The solution file is `RTS_2027.slnx` (dotnet test RTS_2027.slnx …).
