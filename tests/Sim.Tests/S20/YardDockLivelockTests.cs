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

    /// <summary>Rock on both sides of the dock cell (5,6) down to row <c>5 + rows</c> leaves a one-cell corridor x = 5 from the south
    /// (rows = 7: the corridor cells (5,7)..(5,12); rows = 3: two cells, (5,7) and (5,8)).</summary>
    private static Action<OverlayKind[], byte[], int> Walls(int rows) => (overlay, density, w) =>
    {
        for (int y = 6; y <= 5 + rows; y++) { overlay[y * w + 4] = OverlayKind.Block; overlay[y * w + 6] = OverlayKind.Block; }
    };

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(5, false)]
    public void SeveralGleaners_OneYard_EveryoneDelivers(int count, bool corridor) => Play(count, corridor ? 7 : 0);

    /// <summary>S22 (Q-76, option a; was the skipped Desired_ test): in a one-cell dead-end corridor to the dock, the waiters stand back outside
    /// the corridor, so the gleaner that has unloaded can leave and the next one go in. Income keeps flowing to the end of the run.</summary>
    [Fact]
    public void DeadEndCorridorToTheDock_KeepsDelivering() => Play(3, 7);

    /// <param name="wallRows">0 = open yard; otherwise the rows of rock on both sides of the dock cell (see <see cref="Walls"/>).</param>
    internal void Play(int count, int wallRows)
    {
        SimRules rules = Rts.Sim.Tests.S18.S18World.RealRules;
        var sim = new Simulation(new SimSetup(20, EconomyFixture.PaintMap(OverlayKind.Ashfall, 12, wallRows > 0 ? Walls(wallRows) : null), 2, rules, 0x520UL)
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
        _out.WriteLine($"count {count} wall rows {wallRows}: harvested {lastPay}, last payment at tick {lastTick}");
        for (int k = 0; k < count; k++)
            _out.WriteLine($"  h{k}: mission {sim.Units.Mission[hs[k].Index]} dock {sim.Economy.DockState[hs[k].Index]} cargo {sim.Economy.Cargo[hs[k].Index]} at {sim.Units.Position[hs[k].Index].ToCell()}");
        Assert.True(lastPay > 0);
        Assert.True(lastTick > 6000 - 2000, $"deliveries stalled at tick {lastTick}");
        for (int k = 0; k < count; k++) Assert.True(sim.Units.Slots.IsAlive(hs[k]));
    }
}
