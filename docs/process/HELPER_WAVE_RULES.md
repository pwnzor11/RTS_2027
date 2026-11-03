# Wave rules — rules for every package (read after docs/COMMON.md in your worktree)

- Your worktree is `/home/claude/wt/<pkg>` on branch `wp/<pkg>`, already at main. Work ONLY there.
  Never touch `/home/claude/rts_2027` or another worktree. Never push. No scheduling tools.
- Machine has 2 CPUs and 7 GB RAM shared by all helpers. Always:
  `export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1` and build with `-m:1`
  (e.g. `dotnet build RTS_2027.slnx -v q -m:1`, `dotnet test tests/Sim.Tests -m:1 --filter ...`).
  Run scoped tests while working; the full suite (`dotnet test RTS_2027.slnx -v q -m:1`) ONCE at the end.
- The M1.5 contracts in COMMON.md are fixed. If one is wrong for your task, work around it and report it
  under QUESTIONS; do not edit contract files unless your brief says you own them.
- Other packages are editing other parts of the same files at the same time. Edit only what your brief
  lists under YOU OWN; keep diffs tight (no reformatting, no moving code you don't need to change).
- Every new public type: XML doc comment. New test files go in the folder your brief names.
- Determinism rules in COMMON.md apply to everything under `src/Sim`.
- Commit when done: `<type>(<area>): <summary> [<item id>]` (one or more commits), ending with your own
  session's attribution trailer. Then reply ONLY in this format (under 25 lines):
  COMMITS: <hashes + subjects>
  CHECK: <acceptance command> → <result>; full suite → <passed/total>
  SKIPPED: <not done, and why>
  QUESTIONS: <contract problems, stubs, decisions you had to guess>
  USER CHECK: <one line the owner can try>
- Every new .cs file starts with the two SPDX header lines from COMMON.md (a test enforces it).
- Shared resources: NuGet cache and pip are shared; don't delete caches. Blender (`bpy`) work: one render at a time.
- Blender renders: wrap every render command in `flock /home/claude/wt/blender.lock <command>` so only one
  render runs at a time on this 2-core box (several art packages share it). Previews at low samples are fine.
