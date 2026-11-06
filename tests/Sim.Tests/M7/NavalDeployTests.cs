// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.M7;

/// <summary>M7-10: Deploy on a ship (the Expedition Command Ship) puts its building on the nearest buildable footprint that touches the
/// ship's cell; with no such spot the order is ignored. Shipped rules, a 30x20 map with sea at x &lt; 12, beach at x = 12..13, ground beyond.</summary>
public sealed class NavalDeployTests
{
    private const int W = 30, H = 20;
    private static readonly RuleSet Rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!);

    private static MapData Map(bool noLand)
    {
        var land = new LandType[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                land[y * W + x] = noLand || x < 12 ? LandType.Water : x < 14 ? LandType.Coast : LandType.Clear;
        return new MapData("deploy", "rust_belt", W, H, new ushort[W * H], land, new byte[W * H], new OverlayKind[W * H], new byte[W * H],
            new[] { new CellCoord(2, 2), new CellCoord(W - 3, H - 3) });
    }

    private static (Simulation Sim, SimSetup Setup, EntityId Ship) New(bool noLand)
    {
        var setup = new SimSetup(5, Map(noLand), 2, Rules.Sim, Rules.RulesHash) { Factions = [Faction.Expedition, Faction.Authority] };
        var sim = new Simulation(setup);
        int ship = SimRules.IndexOf(Rules.Sim.Vehicles, "exp_command_ship", v => v.Id);
        EntityId id = sim.SpawnVehicle(0, ship, new CellCoord(11, 10));
        Assert.False(id.IsNone);
        return (sim, setup, id);
    }

    private static Command Deploy(Simulation s, EntityId ship) =>
        new(s.Tick, 0, 1, CommandKind.Deploy, ship, default, new CellCoord(11, 10), 0);

    private static int Depots(Simulation s)
    {
        int t = SimRules.IndexOf(Rules.Sim.Buildings, "exp_landing_depot", b => b.Id), n = 0;
        for (int i = 1; i < s.Buildings.Capacity; i++) if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.TypeIndex[i] == t) n++;
        return n;
    }

    [Fact]
    public void ADeployedShipPutsTheDepotOnTheNearestBuildableFootprintTouchingIt()
    {
        var (sim, _, ship) = New(noLand: false);
        sim.Step(new[] { Deploy(sim, ship) });
        for (int i = 0; i < 5; i++) sim.Step(Array.Empty<Command>());
        Assert.Equal(1, Depots(sim));
        Assert.False(sim.Units.Slots.IsAlive(ship));
        // first top-left in row-then-column order whose 3x3 is all buildable (beach or ground) and touches (11,10): (12,7)
        int t = SimRules.IndexOf(Rules.Sim.Buildings, "exp_landing_depot", b => b.Id);
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.TypeIndex[i] == t) Assert.Equal(new CellCoord(12, 7), sim.Buildings.Cell[i]);
    }

    [Fact]
    public void WithNoBuildableSpotTheOrderIsIgnoredAndTheShipIsUnchanged()
    {
        var (sim, _, ship) = New(noLand: true);
        ulong before = sim.ComputeStateHash();
        sim.Step(new[] { Deploy(sim, ship) });
        for (int i = 0; i < 5; i++) sim.Step(Array.Empty<Command>());
        Assert.Equal(0, Depots(sim));
        Assert.True(sim.Units.Slots.IsAlive(ship));
        Assert.Equal(new CellCoord(11, 10), sim.Units.Position[ship.Index].ToCell());
        Assert.NotEqual(0UL, before);
    }

    [Fact]
    public void SaveAndLoadAroundTheDeployGiveTheSameStateHash()
    {
        var (sim, setup, ship) = New(noLand: false);
        for (int i = 0; i < 3; i++) sim.Step(Array.Empty<Command>());
        var ms = new MemoryStream();
        sim.Save(ms);
        Simulation loaded = Simulation.Load(new MemoryStream(ms.ToArray()), setup);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        for (int i = 0; i < 40; i++)
        {
            Command[] cmds = i == 0 ? new[] { Deploy(sim, ship) } : Array.Empty<Command>();
            Command[] cmds2 = i == 0 ? new[] { Deploy(loaded, ship) } : Array.Empty<Command>();
            sim.Step(cmds); loaded.Step(cmds2);
            Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        }
        Assert.Equal(1, Depots(loaded));
    }
}
