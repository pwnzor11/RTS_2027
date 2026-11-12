// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Economy;
using Xunit.Abstractions;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.S20;

/// <summary>S20 (M13-18): several Left Behind gleaners sharing one 2x2 yard (dock cell south of the footprint, in the open) must all keep
/// delivering; the dock queue may not swap places forever.</summary>
public sealed class YardDockLivelockTests
{
    private readonly ITestOutputHelper _out;

    public YardDockLivelockTests(ITestOutputHelper output) => _out = output;

    /// <summary>Rock on both sides of the dock cell (5,6) leaves a one-cell corridor x = 5 from the south.</summary>
    private static void Walls(OverlayKind[] overlay, byte[] density, int w)
    {
        for (int y = 6; y <= 12; y++) { overlay[y * w + 4] = OverlayKind.Block; overlay[y * w + 6] = OverlayKind.Block; }
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(5, false)]
    public void SeveralGleaners_OneYard_EveryoneDelivers(int count, bool corridor) => Play(count, corridor);

    /// <summary>Known deadlock (S20, M13-18, not fixed): in a one-cell dead-end corridor to the dock, a gleaner that has unloaded stands on the
    /// dock cell and cannot leave because the next waiter stands behind it, while that waiter cannot enter because the dock cell is taken.
    /// Movement cannot solve it (mutual block, no detour); the queue would have to back the waiters up. Measured: income stops at tick ~500.</summary>
    [Fact(Skip = "S20 known gap: dead-end dock corridor deadlocks; needs a queue back-off design (owner decision)")]
    public void Desired_DeadEndCorridorToTheDock_KeepsDelivering() => Play(3, true);

    private void Play(int count, bool corridor)
    {
        SimRules rules = Rts.Sim.Tests.S18.S18World.RealRules;
        var sim = new Simulation(new SimSetup(20, EconomyFixture.PaintMap(OverlayKind.Ashfall, 12, corridor ? Walls : null), 2, rules, 0x520UL)
            { AllowDebug = true, Factions = new[] { Faction.LeftBehind, Faction.LeftBehind } });
        Assert.False(sim.PlaceBuilding(0, SimRules.IndexOf(rules.Buildings, "lb_yard", b => b.Id), new CellCoord(4, 4)).IsNone);
        int glean = SimRules.IndexOf(rules.Vehicles, "lb_gleaner", v => v.Id);
        var hs = new List<EntityId>();
        for (int k = 0; k < count; k++) hs.Add(sim.SpawnVehicle(0, glean, new CellCoord(5 + k, 9)));
        var delivered = new int[count];
        int lastPay = 0, lastTick = 0;
        for (int t = 0; t < 6000; t++)
        {
            sim.Step(None);
            if (t % 100 == 0)
            {   // keep the store and the field from running out, so only the dock queue limits income
                sim.Players.Stored[0] = 0;
                for (int y = 0; y < 3; y++)
                    for (int x = 0; x < 3; x++) sim.Map.SetOverlay(new CellCoord(14 + x, 8 + y), OverlayKind.Ashfall, 12);
            }
            int paid = sim.StatsOf(0).CreditsHarvested;
            if (paid != lastPay) { lastPay = paid; lastTick = t; }
        }
        _out.WriteLine($"count {count} corridor {corridor}: harvested {lastPay}, last payment at tick {lastTick}");
        for (int k = 0; k < count; k++)
            _out.WriteLine($"  h{k}: mission {sim.Units.Mission[hs[k].Index]} dock {sim.Economy.DockState[hs[k].Index]} cargo {sim.Economy.Cargo[hs[k].Index]} at {sim.Units.Position[hs[k].Index].ToCell()}");
        Assert.True(lastPay > 0);
        Assert.True(lastTick > 6000 - 2000, $"deliveries stalled at tick {lastTick}");
    }
}
