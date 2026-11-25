# Session 44 — shared rules for the mechanics wave (read after docs/COMMON.md; your brief wins if they disagree)

Owner decisions this session (2026-11-24): build every s18 "built later" mechanic (BACKLOG M-01..M-10, M-12, M-13, M-15) and the
rule fixes M-11, M-20, M-22, M-25, A-02, A-03, U-05, U-11, C-08, C-09, O-09. Mechanic work is allowed under the D-310 balance freeze;
ONE 512-game re-measure runs at the end of the wave (lead). So: build the mechanic as designed, do not tune balance numbers of other things.

## Where you work
- Your worktree is given in your brief (`/home/claude/wt/<id>`, branch `wp/s44-<id>`, based on `integ-s44`). Work only there.
- Never `git stash` (shared across worktrees). Never push. Never touch another worktree or `/home/claude/rts_2027`.
- `cd` into your worktree in EVERY Bash call (the shell does not keep the directory).

## Building and testing (the box has 2 CPUs / 7 GB, shared by ~12 helpers)
- EVERY dotnet command goes through the shared lock and a time limit, e.g.
  `cd /home/claude/wt/<id> && flock /tmp/rts-dotnet.lock timeout 580 dotnet test tests/Sim.Tests -v q --filter "FullyQualifiedName~S44" 2>&1 | tail -30`
- Env: `export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_CLI_HOME=/tmp/dn-<id>`.
- Run only scoped tests while working (your new test class, plus the area tests your brief names). Build with `-m:1`.
- Before committing, run ONCE: your test project filtered to your area + `Determinism` + `Golden` + `Rules`, e.g.
  `--filter "FullyQualifiedName~S44|FullyQualifiedName~Determinism|FullyQualifiedName~Golden|FullyQualifiedName~Rules"`.
  The lead runs the full suite on the cluster after merging. Do NOT run the whole solution's tests.
- A command that would run longer than ~9 min: start it with `nohup ... > scratch/run.log 2>&1 &` (still under flock) and poll the log.

## Code rules for this wave
- Determinism rules in COMMON.md are hard (integer math, slot order, SimRandom only, no Dictionary iteration in sim).
- New sim logic goes in your own partial file `src/Sim/Simulation.<Name>.cs`. The lead pre-added step hooks:
  `RunMines()` (after RunMovement, Simulation.Mines.cs), `RunTethers()` (before RunEffects, Simulation.Tether.cs),
  `RunDrydocks()` (after RunProduction, Simulation.Drydock.cs). Do not reorder StepCore; if you need another hook, add ONE line and say so.
- New rules fields: add them next to the closest related field (not at the end of the record), with an XML doc line naming the TOML key;
  defaults must leave every unit that does not set the key behaving exactly as today.
- New persistent state must be hashed and saved: follow an existing part (grep `HashCoverageTests`, `SimWriter`, `SimReader`). Keep
  `SaveVersion = 14` in your branch (the lead bumps it once to 15 at merge). Append new parts at the end of the part order.
- Pinned hashes (Golden*, map/replay pins) your change moves: re-pin them in your branch and list each in your reply. The lead re-pins
  again after the merge. Never re-pin a hash your change should not move — find out why it moved.
- Numbers: use the design doc's numbers. Where the doc gives none, choose a modest value, put it in data (not code) and list it under
  ASSUMED in your reply (the lead logs it as "assumed — confirm").
- AI: make the skirmish AI use the new ability in the simplest sensible way if the brief says so; no AI rewrite.
- Text: do NOT edit `src/Game/Ui/Sidebar/TodayText.cs` (the lead deletes the "cannot ... yet" lines at merge). If a tooltip or Archive line
  must change, change it in the data/text file your brief names, or list the new line in your reply.
- New code files start with the SPDX header (COMMON.md). Commit format `<type>(<area>): <summary> [M16-<backlog id>]`, e.g. `[M16-M-01]`.

## Reply format (under ~25 lines)
COMMITS / CHECK (command -> result) / ASSUMED (numbers and choices) / REPINNED (tests) / SKIPPED / TEXT (tooltip lines to delete or add) /
USER CHECK (one line the owner can try in a skirmish).
