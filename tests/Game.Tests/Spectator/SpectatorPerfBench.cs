// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Tests.S18;
using System.Diagnostics;
using Rts.Data;
using Rts.Game.Host;
using Rts.Game.Screens;
using Rts.Game.Sandbox;
using Rts.Game.Ui.Spectator;
using Rts.Sim.Rules;
using Xunit.Abstractions;

namespace Rts.Game.Tests.Spectator;

/// <summary>
/// Headless benchmark of the spectator at 8x (M4-16). Skipped unless the environment variable RTS_PERF=1 is set:
/// <c>RTS_PERF=1 dotnet test tests/Game.Tests --filter Category=Perf</c> (Release recommended). It plays a 4-AI tier-3 match on a
/// generated map through <see cref="SimHost.Advance"/> the way the game screen does (one 8-tick burst per call, then the stats panel
/// refresh and a read of the snapshot) and prints the cost per tick split by phase.
/// </summary>
[Trait("Category", "Perf")]
public sealed class SpectatorPerfBench
{
    private readonly ITestOutputHelper _out;

    public SpectatorPerfBench(ITestOutputHelper output) => _out = output;

    private static string RulesDir => RulesLocator.FindRulesDir(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("data/rules not found");

    private static int EnvInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out int v) ? v : fallback;

    [ProbeFact("RTS_PERF")]
    public void Eight_times_speed_four_ai_medium_map_rate()
    {
        if (Environment.GetEnvironmentVariable("RTS_PERF") != "1") return;

        int ticks = EnvInt("RTS_PERF_TICKS", 2000);
        var size = (MapSize)EnvInt("RTS_PERF_SIZE", (int)MapSize.Medium);
        var slots = new SkirmishSlot[4];
        for (int i = 0; i < 4; i++) slots[i] = new SkirmishSlot(SlotKind.Ai, Faction.Authority, 3, i, i);
        var settings = new SkirmishSettings(new MapChoice(null, new MapGenParams(Biome.RustBelt, size, 4, 5)), slots, 5);
        SkirmishGame game = SpectatorSetup.Create(RulesDir, settings);
        SimHost host = SpectatorSetup.CreateHost(game);
        var controller = new SpectatorController(host);
        var panel = new SpectatorPanel(controller);
        host.SetSpeedStep(3);
        var prof = new HostProfile();
        host.Profile = prof;

        long uiTicks = 0, readTicks = 0;
        int frames = 0, readUnits = 0;
        long start = Stopwatch.GetTimestamp();
        while (host.Tick < ticks)
        {
            host.Advance(SimHost.TickMicroseconds);   // one interval = 8 ticks at 8x
            long a = Stopwatch.GetTimestamp();
            panel.Update(host.Sim);                    // what GameScreen does each frame
            long b = Stopwatch.GetTimestamp();
            readUnits += host.Current.Units.Count + host.Previous.Units.Count;   // renderer / audio read
            readTicks += Stopwatch.GetTimestamp() - b;
            uiTicks += b - a;
            frames++;
        }

        double wall = (Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency;
        double perTick(long t) => t * 1e6 / Stopwatch.Frequency / host.Tick;
        _out.WriteLine($"map {size} 4x AI tier 3, {host.Tick} ticks in {wall:F2} s = {host.Tick / wall:F0} ticks/s (need 160)");
        _out.WriteLine($"per tick us: AI {perTick(prof.AiTicks):F0}  Step {perTick(prof.StepTicks):F0}  snapshot {perTick(prof.SnapshotTicks):F0}  "
            + $"stats panel {perTick(uiTicks):F0}  frame read {perTick(readTicks):F0}   (snapshots built: {prof.Snapshots} of {prof.Steps} ticks, {frames} frames)");
        _out.WriteLine($"state hash {host.Sim.ComputeStateHash():X16}  units seen {readUnits}");
        Assert.True(host.Tick >= ticks);
    }
}
