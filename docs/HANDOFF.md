# Handoff: Session 24 — read the laptop runs (s23d series, campaign sweep), then the next balance round
Last commit: `docs: Session 23 handoff` on `land/s23-r4` (PR #80 to main). Check `git log -1 origin/main` and whether PR #80 is merged.
Where things stand: session 23 rounds 1-3 are on main (PRs #77-#79: Rocket Truck splash 1 D-229, `ai match --ledger`, Left Behind tier-3 cap
D-230, lore D-231, nightly pre-release D-232, mop-up loosened D-233). Round 4 is PR #80 (`land/s23-r4` = integ-s23d): owner answers D-234;
late-game draws diagnosed as mined-out maps plus AI endgame fixes D-235 (`ai match --econ 1`; cloud draws 21 % -> 14 %); Expedition tier-2 hover
anti-infantry unit **Grapeshot** (`exp_canister_skiff`, D-236/D-237: data, AI use vs infantry, Glide Tank upgrades, draft field note);
campaign tier 5 raised in auth_02, auth_04, dir_01-04 to 4-9/12 (D-238; lb_04, exp_02, exp_04 stay 12/12); tut_01 instructor voice slots
(M10-24); Rocket Truck cost 800 -> 950 (D-239, M13-26) after the s23after read (docs/balance/s23_directorate.md). Decisions up to D-239.
Open questions: Q-73 only.

## Start here (shown to the user as the plan; nothing starts until they say go)
1. Check PR #80: Windows CI green? merged? (owner may have merged; else merge when green on his word).
2. Read the laptop campaign sweep `balance_runs\s21sweep\s21sweep_summary.md` (on land/s23-r4 @ 8a3b4c9; old one renamed s21sweep_s22):
   tier-5 targets 4-9/12 for the six raised missions, all other rows on Q-75 targets; the truck cost may have moved Directorate missions.
3. Read the laptop series `balance_runs\s23d\s23d_summary.md` against `s23after`: Auth v Dir, Dir v Exp, Dir v LB (truck cost), LB v Exp and
   Exp v LB (Grapeshot), draws per row (D-235), start share (Exp mirror Rust Belt 71 % and LB v Exp 66 % in s23after). Target 45-55 %.
4. Grapeshot art: after the owner's push (art_s23\push_art.ps1 -> wp/art-s23-skiff), merge it (drops the Glide Tank sprite alias).
5. Then the next balance round from the s23d table; if Expedition/Authority still lose to Conscript mass, look at their AI responses
   (Expedition tender spending, Authority single-Scraper stall: docs/balance/s23_directorate.md option 3). M14-05 booklet waits for balance (D-234).

## Half-finished work
- Grapeshot art on `wp/art-s23-skiff` (local commits pushed by the owner with the LFS bundle; not on GitHub until then). Hull number 06.
- Grapeshot field note "GRAPESHOT 6 // ..." is a draft: owner to approve or reword. Trigger (40 % enemy infantry, 2-3 skiffs) is "(tune)".
- Tutorial instructor voice: owner runs the audition (make_audio.ps1 -VoiceAudition -Only leader_tut_instructor), picks, sets leaders.json, runs -Voice.
- SFX: 80 jobs on the owner's laptop, nothing picked yet. Unit barks after SFX (D-234).
- Remaining draws are mostly "attacker arrives late"; next levers: faster wave launch or adjudication at the time limit (s23_draws.md option D).
- Tier 5 bands are narrow; later sim/bot changes will move them (D-238 note).

## Waiting on the user
- Laptop: push art (`powershell -ExecutionPolicy Bypass -File art_s23\push_art.ps1`), then on `origin/land/s23-r4`: campaign sweep, then
  `run_s19_after.ps1 -OutName s23d`; then `git checkout main`. Say "done".
- Merge PR #80 if CI is green and you want it before the next session. Instructor audition and SFX picks when convenient. Q-73 (par times).
- Two-PC LAN test (M9-15). Branch clean-up on GitHub (owner deletes): wp/s22-*, integ-s23, integ-s23b, integ-s23c, and after PR #80 integ-s23d.

## How to run this session
- Session 23 lessons:
  - add_repo can fail for a while ("service temporarily unavailable"); retry later in the session rather than switching to bundles at once.
    Without it, gh, LFS and pushes are all refused (the anonymous clone still works).
  - Files copied to the owner's PC with device_commit_files can be re-encoded by type (PNG LFS objects arrived with other hashes): ship LFS
    objects inside a .zip and check SHA-256 against the oid in push_art.ps1.
  - A server restart stops helpers mid-run and leaves their test processes orphaned and still holding the dotnet lock: kill them by pid
    (never pkill -f), then SendMessage each helper to resume. Two tier-5 helpers had made no commits despite the brief: say "commit each
    mission as soon as it is measured" and check `git status` in their worktrees after any restart.
  - Campaign tier-5 sweeps are steep and some missions are all-or-nothing (one unit flips 12/12 to 0/12): budget 6-seed scouting, then a
    12-seed confirm; when a band is unreachable, report the edge and leave the mission (owner accepted for lb_04/exp_02).
  - `ai match --ledger` has no kill attribution and counts transforms (tenders, rigs, Command Ship) as losses; read loss rates with that in mind.
- Session 20 lessons: (1) a single fixed-seed bot walk is not a balance measure: the campaign walks flip with the seed; judge a mission-tier
  by wins over 12 seeds (scratch/s20-ash-diag S20Sweep, ~2 h for all 45 on the 2-CPU box). (2) To split a rules effect, run the variants
  separately (ash damage only vs path cost only) before tuning. (3) Another session may land on main at any time: fetch main before opening
  or refreshing a PR, and pick D-/Q-numbers from origin/main at wrap-up, not from the session start. (4) A PR whose branch conflicts with
  main gets no CI run at all: check `mergeable` when CI doesn't start. (5) Tools tests that print to the console must be in the
  ToolsConsole collection, or they leak into another test's captured output on Windows CI.
- Lead: Opus for build sessions (the owner wants long unattended overnight builds); Sonnet for a pure review/question session. Helpers: Haiku default; Sonnet for sim core,
  determinism, serialisation, movement, combat, AI, scripting, and all Blender art.
- Repo clone: `/home/claude/rts_2027` (remote URL github.com/pwnzor11/RTS_2027.git; re-set it after a container restart); main has no branch protection, but land through a PR so Windows CI runs first
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
- Session 14 lessons:
  - 2 CPUs + 6-8 code helpers = 30-90 min dotnet lock waits. Brief helpers to run builds/tests in the FOREGROUND with a 600 000 ms timeout
    (or poll); a helper that backgrounds a queued test and ends its turn hands back "unverified" work. Stop such helpers and verify at merge.
  - Give every helper a private scratch dir (/tmp/claude-0/<pkg>/): one overwrote another's measurement script in the shared /tmp.
  - Overlapping UI packages: let ONE integration package merge them, build, and fix (s14-ui3 did ui1+ui2+obj+scale cleanly).
  - The game runs under xvfb for screenshots (recipe in the UI audit "Method"; box-drag needs xdotool mousemove_relative while held).
  - Stills: Cycles renders ~10 min each on one blender lock; three faction helpers in parallel worked (scenes built while others render).
  - Safety check refused: bulk deletes of CI artifacts, force-push, installing binaries on the owner's PC. Don't retry; hand the owner the command.
  - D-173 lesson: check what a diagnosis actually measured (here "13-15 structures" included the shaft and factories) before recommending a rule.
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

- Session 15 lessons:
  - New sprites (LFS) cannot be pushed from the cloud. Package them for the owner: `git bundle create <zip>/x.bundle <base>..<branch>`, copy EVERY
    LFS object referenced by any commit in the range (not just the tip: `git lfs push` checks history; the first packs package missed 9 versions),
    and a push_art.ps1 that fetches, verifies the bundle, copies objects into .git/lfs/objects, `git lfs push origin <branch>`, `git push`.
    Ship it as a .zip (the owner's browser refuses a bare .ps1). Keep art commits off branches you need to push until the owner has pushed.
  - Licences: OpenGameArt and kenney.nl block automated reads. Read the packs' own licence files; get verbatim licence texts from GitHub
    (raw.githubusercontent.com/spdx/license-list-data/main/text/<id>.txt works). CC-BY-SA 3.0 is not GPL-compatible: keep such art as separate,
    separately licensed data files (0 A.D. props) and never in GPL sprite sheets; avoid it in tooling that mixes (Warzone page-6/17/30, wheels).
  - Enum-count pins live in several contract tests (M4, M6, M7): grep for `Enum.GetValues<CommandKind>().Length` when adding a command.
  - Windows CI catches file-lock bugs Linux misses (a test deleted a replay file still open): close sessions (SaveReplay) before deleting.
  - A container restart lost nothing committed; helper reports can be recovered from the end of their task transcripts.
  - When a re-rendered asset and a renderer-side scale option overlap, give the asset a manifest flag (figure_scale_permille) so they do not stack.
  - The owner answered every question within minutes; batching 2-4 questions with AskUserQuestion and sending contact sheets first worked well.
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
- Session 17 lessons:
  - A container restart stops running helpers; nothing committed or on disk is lost and SendMessage to the same helper resumes it with its context.
    Brief every helper to commit after each numbered step, and push helper branches when they have commits (the stop hook asks for it).
  - The owner's laptop runs a 16-row, 64-game series in about 90 minutes; the 2-CPU cloud box needs that long for one row. Put measurement scripts
    in tools/measure, hand them over, and read the results from his folder through the device bridge (logs are ASCII with CRLF; PowerShell `*>`
    logs such as the re-render logs are UTF-16).
  - Measure the baseline before setting a blocker: layout 1 fails the 60 % start limit itself on two biomes, and the plain 10-minute income mean
    tracks game length, not income.
  - A stall with "0 structures, 0 units" for BOTH sides is a setup problem (start units), not an AI problem: check SkirmishStart before the tactics code.
  - Art drift: when a full re-render differs from committed sheets, compare render-commit times with later edits to shared material tables
    (`lb_common.py` MATERIALS) before touching a script; here the sprites were stale, not the scripts.
  - To read LFS files from a local-only branch in the owner's folder (no git-lfs in the bridge VM): `git cat-file -p <branch>:<path>` gives the
    pointer, the object is at `.git/lfs/objects/<aa>/<bb>/<oid>`. In the cloud, `git lfs fetch origin refs/remotes/origin/<branch>` (the short name fails).
  - A helper made one stray device-bridge call despite the brief (no effect). Keep "no mcp__remote-devices__* tools" in every brief.

- Session 19 lessons:
  - Local merge batches must include Scripting.Tests (or the campaign bot classes) whenever a change touches movement, combat, economy or
    infantry rules: the campaign bots swing on small changes and Windows CI found ashfall and path regressions only after the PR.
  - Bisect campaign regressions with temporary rules switches (e.g. `ashfall_damage = 0` locally) and per-merge-commit checkouts before reading
    code; it split "ash" from "path" from "data" quickly. Never commit the local switch.
  - A full Scripting.Tests run plus Sim/AI/Game takes well over an hour on the loaded box; the background run limit (2 h) killed one batch.
    Let Windows CI verify slow projects in parallel by pushing a land/ branch early.
  - The safety check refuses force-pushes even to our own land/ branches: open a new land branch and PR instead (#66 superseded #62).
  - Helpers' "assumed" picks that touch player-visible behaviour go to OPEN_QUESTIONS as "assumed — confirm" (Q-72).
  - The owner answers phone pushes quickly: batch 1-5 picks per push with a recommendation each; PushNotification works.
- Session 22 lessons:
  - add_repo (push access) is refused by the auto-mode check unless the owner asks for it in chat; once added, git push and LFS *downloads* work,
    but LFS *uploads* from the cloud are still refused (S3 "Forbidden"). Keep new LFS files off branches the lead must push, or hand the owner a
    package: bundle + a push script. Do NOT send LFS objects through SendUserFile/device_commit_files: WAVs arrived re-encoded (checksums broke).
    Rebuild objects on the owner's side from his own working files (git show HEAD:<path> | sha256) instead.
  - git commands in the device bridge leave .lock files (no delete permission): use `git show`/`git cat-file`, not status/diff/checkout, and move
    any stray `.git/index.lock` aside.
  - The owner's Windows has Python 3.13 (art venv) but no 3.11; laptop setups now try py -3.11, then uv, then winget's Python 3.11.
  - Laptop scripts written blind need one quick dry run by the owner before a long run: missing packages (torchsde) only show at model load.
  - Never put credentials in docs/briefs; when the owner pastes one, don't use or store it and ask him to rotate it.
  - Art: the owner judges heroes by eye; a quick 24-sample cloud preview sheet per round, then full renders on the laptop, worked well.
- Session 21 lessons:
  - Check GitHub for a parallel session before starting (`git ls-remote origin 'refs/heads/*s<N>*'`): a whole session 20 had run overnight;
    build on its PR and handoff instead of redoing it.
  - Owner policy (D-220): never spend time keeping development saves or replays loadable; re-pin hashes freely.
  - The owner wants campaign sweeps and skirmish series on his laptop, not the cloud box: write a tools/measure script, he runs it and says
    "done"; read the summary through the linked folder. The laptop runs all 45 campaign mission-tiers in ~21 min (cloud: 1-2 h per pass) and
    gives seed-for-seed identical results.
  - Find root causes with the per-unit trace before scaling numbers: three missions lost to the same weapon-range gap, fixed once in the sim.
  - A sim rule change voids every earlier sweep: re-sweep all missions, keep only retunes that are still needed (dir_04's round-1 change was not).
  - Brief retune helpers: don't shorten visible timers, keep each tier's identity, set TMPDIR for the trace probe; tier-5 cliffs are common
    (several missions are all-or-nothing for the bot).
  - Don't merge into the folder a running test reads its data from; don't wrap long test runs in the shared build lock (helpers queue behind it).
  - The proxy blocks Actions caches and numeric-id artifact pagination; page artifacts with &page=N.
- Session 18 lessons:
  - Helpers cannot write report .md files (the harness refuses "report files from subagents"): brief them to hand back the full text, and save
    it from the task transcript (the lead used a small script that pulls the SubagentHandback text out of the .output JSONL).
  - The 5-hour usage limit cut off 5 helpers twice; SendMessage after the reset resumed every one of them with its context and commits.
  - "Verify only" packages that pin today's behaviour (`Audit_` test + skipped `Desired_` twin) let a whole audit land without moving a hash,
    and give the fix session ready-made acceptance tests.
  - A full-suite run in the background on a box loaded with live-play helpers hit the 1-hour background limit: run it per project
    (`dotnet test tests/<P>.Tests --no-build`) after the helpers finish, with run_in_background and a 2-hour timeout.
  - Live play under Xvfb found the worst UI bug (structure-lane soft-lock); code reading alone had missed it. Keep one live-play reviewer.
  - Wave-1 static estimates were wrong both ways when measured (path stall 3 s estimated, 22.6 s measured; hash cost 9-15 ms estimated, 3-5 ms
    measured; Mountains Expedition "3 of 8 seeds" was 80-98 % of maps). Always measure before ranking.
