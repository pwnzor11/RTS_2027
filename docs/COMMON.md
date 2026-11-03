# COMMON.md — shared rules for helper agents
Read this before your package brief. The brief wins if they disagree.

## Layout
- `RTS_2027.slnx` · `src/Sim` (namespace `Rts.Sim`, graphics-free sim) · `src/Data` (TOML, `Rts.Data`)
  · `src/Tools` (CLI) · `src/Game` (MonoGame DesktopGL) · `tests/Sim.Tests` (xUnit) · `data/rules/*.toml`.
- Contract types (do not change signatures): `src/Sim/Core/*.cs` and the public members of
  `src/Sim/Simulation.cs`. If a contract is wrong, stop and report it under QUESTIONS.
- Build settings come from `Directory.Build.props` (net10.0, nullable, **warnings are errors**).

## Commands
- Build: `dotnet build RTS_2027.slnx -v q`
- Scoped tests: `dotnet test tests/Sim.Tests --filter "FullyQualifiedName~<YourArea>"`
- Full suite (once, at the end): `dotnet test RTS_2027.slnx`
- Env: `export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1`

## Sim rules (hard — the determinism test enforces them)
- Inside `src/Sim`: no `float`, `double`, `decimal`, `System.Random`, `DateTime`, `Stopwatch`,
  `Environment.TickCount`, `Parallel`, `Task`, threads, `Dictionary`/`HashSet` iteration, LINQ over
  unordered collections, `GetHashCode()` of strings in sim logic. Integer math only; `Fixed32` for ratios.
- Iterate entities by slot index. RNG only via the Simulation's `SimRandom`.
- Positions in leptons (256 per cell). Facing is a byte 0..255, 0 = north, clockwise.

## M1.5 contracts (do not change signatures)
`SimSetup.Teams/Limits/AllowDebug/TeamOf`, `SimLimits`, `Relation`, `Teams.Neutral`, `DebugOp`, `SetupEntry`,
`Command.Arg2`, `PlayerStore` (part "players", last in the part order), `Simulation.Players/SetupLog/InStep`,
`Simulation.RelationOf/RulesOf` (signatures; M1.5-01 fills the bodies), `Rts.Data.BuildInfo.Version`.
- Licence: never copy or port EA code (D-050). AI code never runs inside `Step` (D-051).

## Style
- File-scoped namespaces, one public type per file, XML doc comment on every public type.
- Tests: `tests/Sim.Tests/<Area>/<Thing>Tests.cs`, xUnit `[Fact]`/`[Theory]`, no sleeps, no randomness
  without a fixed seed.

## Commits
- `<type>(<area>): <summary> [M0-11]` with your own session's attribution trailer. Do not push.

## Data safety and forbidden actions
- No PII, credentials or tokens anywhere. Synthetic data only.
- Never: push, use scheduling tools, work outside your own worktree, add NuGet packages not named in
  your brief, call external services, delete files you don't own, use EA names or assets.
- Scratch output goes in `scratch/` (git-ignored) inside your worktree.
