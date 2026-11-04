# M4-16d: spectator at 8x (headless measurement)

Bench: `RTS_PERF=1 [RTS_PERF_TICKS=n] [RTS_PERF_SIZE=0|1|2] dotnet test tests/Game.Tests -c Release --filter Category=Perf --logger "console;verbosity=detailed"`
(`tests/Game.Tests/Spectator/SpectatorPerfBench.cs`; does nothing without `RTS_PERF=1`). 4 AIs, tier 3, Authority, generated
RustBelt map seed 5, driven through `SimHost.Advance` at 8x (8 ticks per 50 ms interval), then the stats panel refresh and a
snapshot read per frame, as `GameScreen` does. Timing lives in `SimHost.Profile` (null in the game, so no clock is read there).
2-CPU box, shared with other helpers, so absolute numbers vary by roughly 20 %.

| map | ticks | ticks/s (need 160) | AI us/tick | Sim Step us/tick | Snapshot us/tick | Stats panel us/tick |
|---|---|---|---|---|---|---|
| medium (before = after) | 12 000 | 1 086 | 56 | 741 | 119 | 5 |
| large (before = after) | 12 000 | 974 | 48 | 812 | 161 | 4 |

No game-code change was made: the headless rate is 6x the 160 ticks/s target.

## Where the cost is
- `Simulation.Step`: about 80 % of the headless time (not in this package's files). No single dominant function was profiled
  (dotnet-trace is not installed); share only by phase timing.
- `Simulation.Snapshot(viewer)`: 13-16 %, allocates lists, a map-sized `byte[]` ground layer and an `FxEvent[]` every tick.
  Building it only for the last tick of an 8x burst would save about 12 %, but the skipped ticks' `FxEvents` (explosions, audio)
  would be lost, so it was not done. A safe form needs a sim-side change (reuse buffers), proposed under QUESTIONS.
- Stats panel: already refreshed once per frame (once per 8 ticks at 8x), 4-5 us per tick. Not a cost.
- AI decisions: 5-7 %.

## Conclusion
The sim side alone is not the reason 8x feels slow on this hardware. The remaining per-frame cost is rendering (SpriteBatch,
fog, terrain), which cannot be measured here without a GPU. At 8x a frame must also draw once per 8 ticks, so check the draw time on a real machine.
