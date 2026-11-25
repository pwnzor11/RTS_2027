# Session 45 test census (T-02, REPO-06)

Scope: skipped tests and tests that cannot fail. Method: grep for `ProbeFact` and `Skip`, then a scripted scan of every
`[Fact]` / `[Theory]` body under `tests/` for a body with no assertion call, followed by a manual check of each hit.

## 1. Env-gated probes (`[ProbeFact]`, skipped unless the env var is set)

| file | test | env var | why it is a probe |
|---|---|---|---|
| tests/Game.Tests/SpriteSheetTests.cs:466 | `HouseColourProofTests.Compose_the_tank_for_eight_houses` | RTS_PROOF_IN (and RTS_PROOF_OUT) | writes a proof image for a human to look at; no assertion on the result |
| tests/Sim.Tests/Vision/VisionPerformanceTests.cs:83 | `RandomWalk_400ActorsOn128x128For200Steps_LogsTicksPerSecond` | RTS_PERF | timing log only, no assertion |
| tests/Game.Tests/Spectator/SpectatorPerfBench.cs:34 | `Eight_times_speed_four_ai_medium_map_rate` | RTS_PERF | timing bench |
| tests/Sim.Tests/M7/CarrierBalanceProbe.cs:106 | `CarrierBalanceProbe.Probe` | RTS_BALANCE_PROBE | writes a balance table to a file, no assertion |

## 2. Cannot-fail tests, fixed

| file | test | why it could not fail | fix |
|---|---|---|---|
| tests/Sim.Tests/S18/PathStallPerfProbe.cs:86 | `Sp01_TwentyTanks_AttackABuilding_AcrossA128Map` | plain `[Fact]`, body returns at once unless RTS_PERF=1, so it passed in every normal run | now `[ProbeFact("RTS_PERF")]`: reported as skipped instead of passed |
| tests/Sim.Tests/S18/PathStallPerfProbe.cs:96 | `Sp09_StateHash_Cost` | same | same |

## 3. Skipped tests, fixed

| file | test | fix |
|---|---|---|
| tests/Game.Tests/Audio/AudioProvenanceTests.cs:120 | `UnknownSourcesAreReported` | un-skipped (T-01); PROVENANCE has 0 unknown sources; passes |

## 4. Cannot-fail candidates, checked and left

The scan also flagged tests whose body is an expression or delegates to a helper, plus lambda bodies. Each one of these
asserts through a helper (`AssertSame`, `AssertSorted`, `AssertFires`, `AssertClean`, `AssertDefeatFlow`, `Check`, `PlayInOrder`,
`Play`, `WithText`, `WithStatics` ...), so the scan's `Assert.` pattern missed them. They are not listed here. Not every helper
was opened: the census trusts the naming and the file-level read, not a line-by-line proof.

Still open (not verified):
- The `Menu*Tests` and `MenuPanelLayoutTests` lambdas (tests/Game.Tests/S18/) were not opened in full.
- Exception swallowing: a grep of `catch` under tests/ found only cleanup catches (`Directory.Delete`, `IOException`
  in `Dispose`) and expected-throw catches. None hides an assertion.
