# Session 48 — shared rules for wave A/B (read after docs/COMMON.md; your brief wins if they disagree)

Owner decisions this session (2026-11-27, D-340..D-342): f1 balance landed; campaign outline Q5-Q9 answered (upgrades gated as mission rewards,
three new maps, choice slots at play positions 6 and 10, keep open_after ids, leader secrets hinted on screen and confirmed in the Archive);
Wrenfield is Hester ("her"). Wave A/B builds campaign wave 1 (C-12), the upgrade gate, two maps, M-28b, T-11, T-12. Balance numbers are being screened by the lead on the cluster at the same time: do NOT change any number in data/rules,
data/ai or data/mapgen unless your brief says so. Outcome-neutral work must leave every Golden/replay/skirmish hash unchanged.

## Where you work
- Your worktree is given in your brief (`/home/claude/wt/<id>`, branch `wp/s48-<id>`, based on `integ-s48`). Work only there.
- Never `git stash` (shared across worktrees). Never push. Never touch another worktree or `/home/claude/rts_2027`.
- `cd` into your worktree in EVERY Bash call (the shell does not keep the directory).

## Building and testing (the box has 2 CPUs / 7 GB, shared by up to 9 helpers; dotnet is /usr/local/bin/dotnet, a wrapper that turns build servers off)
- EVERY dotnet command goes through the shared lock (`flock -o /tmp/rts-dn2.lock`: `-o` stops build servers inheriting the lock) with build
  servers off and a time limit, e.g.
  `cd /home/claude/wt/<id> && flock -o /tmp/rts-dn2.lock timeout 580 dotnet test tests/Sim.Tests -v q --filter "FullyQualifiedName~S48" 2>&1 | tail -30`
- Env: `export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_CLI_HOME=/tmp/dn-<id> DOTNET_CLI_USE_MSBUILD_SERVER=0 UseSharedCompilation=false`
  and add `--disable-build-servers` to every dotnet build/test. The old `/tmp/rts-dotnet.lock` is retired (held by orphan compiler servers).
- Run only scoped tests while working (your new test class, plus the area tests your brief names). Build with `-m:1`.
- Before committing, run ONCE: your test project filtered to your area + `Determinism` + `Golden` + `Rules`, e.g.
  `--filter "FullyQualifiedName~S48|FullyQualifiedName~Determinism|FullyQualifiedName~Golden|FullyQualifiedName~Rules"`.
  The lead runs the full suite on the cluster after merging. Do NOT run the whole solution's tests.
- A command that would run longer than ~9 min: start it with `nohup ... > scratch/run.log 2>&1 &` (still under flock) and poll the log.

## Code rules for this wave
- Determinism rules in COMMON.md are hard (integer math, slot order, SimRandom only, no Dictionary iteration in sim).
- New rules fields: add them next to the closest related field (not at the end of the record), with an XML doc line naming the TOML key;
  defaults must leave every unit that does not set the key behaving exactly as today.
- New persistent state must be hashed and saved: follow an existing part (grep `HashCoverageTests`, `SimWriter`, `SimReader`). Keep
  `SaveVersion = 16` in your branch (the lead bumps it once at merge if needed). Append new parts at the end of the part order.
- Pinned hashes (Golden*, map/replay pins) your change moves: re-pin them in your branch and list each in your reply. The lead re-pins
  again after the merge. Never re-pin a hash your change should not move — find out why it moved.
- Numbers: use the design doc's numbers. Where the doc gives none, choose a modest value, put it in data (not code) and list it under
  ASSUMED in your reply (the lead logs it as "assumed — confirm").
- AI: make the skirmish AI use the new ability in the simplest sensible way if the brief says so; no AI rewrite.
- Text: do NOT edit `src/Game/Ui/Sidebar/TodayText.cs` unless your brief owns it (list any line that should change in your reply). If a tooltip or Archive line
  must change, change it in the data/text file your brief names, or list the new line in your reply.
- New code files start with the SPDX header (COMMON.md). Commit format `<type>(<area>): <summary> [M16-<backlog id>]`, e.g. `[M16-U-12]`.
- Never call scheduling tools, never use any mcp__remote-devices__* tool, never push, never write report .md files (put the report in your reply).

## Reply format (under ~25 lines)
COMMITS / CHECK (command -> result) / ASSUMED (numbers and choices) / REPINNED (tests) / SKIPPED / TEXT (tooltip lines to delete or add) /
USER CHECK (one line the owner can try in a skirmish).
