// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.Production.ProductionFixture;

namespace Rts.Sim.Tests.M7;

/// <summary>Simfu-2: a ship leaves only from a vehicle factory that reaches water (D-144 follow-up); land units are unchanged.</summary>
public sealed class ShipExitTests
{
    private const int Size = 32, CoastX = 16;

    private static (Simulation Sim, int Boat, int Tank) NewWorld(bool withShipyard)
    {
        var speed = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        foreach (LandType land in Enum.GetValues<LandType>())
            for (int l = 0; l < RuleCounts.Locomotions; l++)
            {
                bool water = land is LandType.Water or LandType.Shallow;
                bool ship = (Locomotion)l == Locomotion.Naval;
                speed[(int)land * RuleCounts.Locomotions + l] = ship ? (water ? 100 : 0) : water ? 0 : 100;
            }
        var buildable = new bool[RuleCounts.LandTypes];
        buildable[(int)LandType.Clear] = true;
        var cells = new LandType[Size * Size];
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++) cells[y * Size + x] = x >= CoastX ? LandType.Water : LandType.Clear;
        var map = new MapData("coast", "rust_belt", Size, Size, new ushort[Size * Size], cells, new byte[Size * Size], new OverlayKind[Size * Size],
            new byte[Size * Size], new[] { new CellCoord(1, 1), new CellCoord(Size - 2, Size - 2) });

        SimRules r = Loaded.Sim;
        VehicleType tank = r.Vehicles[Veh("test_tank")];
        BuildingType factory = r.Buildings[Bld("test_factory")];
        var vehicles = r.Vehicles.Append(tank with { Id = "test_zboat", Locomotion = Locomotion.Naval }).OrderBy(v => v.Id, StringComparer.Ordinal).ToArray();
        var buildings = r.Buildings.Append(factory with { Id = "test_yard", Exit = new CellCoord(4, 1) }).OrderBy(b => b.Id, StringComparer.Ordinal).ToArray();
        SimRules rules = r with { Vehicles = vehicles, Buildings = buildings, Terrain = new TerrainTable(speed, buildable) };

        var sim = new Simulation(new SimSetup(1, map, 2, rules, Loaded.RulesHash));
        int B(string id) => SimRules.IndexOf(rules.Buildings, id, b => b.Id);
        sim.PlaceBuilding(0, B("test_hq"), new CellCoord(4, 4));
        sim.PlaceBuilding(0, B("test_power"), new CellCoord(12, 4));
        sim.PlaceBuilding(0, B("test_factory"), new CellCoord(4, 8));          // lowest slot: the default primary
        if (withShipyard) sim.PlaceBuilding(0, B("test_yard"), new CellCoord(12, 12));   // exit (16, 13) is the first water cell
        sim.Players.PowerOut[0] = 1000; sim.Players.PowerUse[0] = 0;
        sim.Players.Cash[0] = 20000;
        return (sim, SimRules.IndexOf(rules.Vehicles, "test_zboat", v => v.Id), SimRules.IndexOf(rules.Vehicles, "test_tank", v => v.Id));
    }

    private static int Count(Simulation sim) { int n = 0; for (int i = 1; i < sim.Units.Capacity; i++) if (sim.Units.Slots.IsIndexAlive(i)) n++; return n; }

    [Fact]
    public void AShipWaitsWhenNoFactoryReachesWater()
    {
        (Simulation sim, int boat, _) = NewWorld(false);
        Step(sim, Build(sim, ProductionCategory.Vehicle, boat));
        Run(sim, 1500);
        Assert.Equal(0, Count(sim));
        Assert.Equal(1, sim.Production.QueueLength(0, ProductionCategory.Vehicle));
    }

    [Fact]
    public void AShipLeavesFromTheCoastalFactoryEvenWhenALandFactoryIsPrimary_AndATankStillUsesThePrimary()
    {
        (Simulation sim, int boat, int tank) = NewWorld(true);
        Step(sim, Build(sim, ProductionCategory.Vehicle, boat));
        RunUntil(sim, () => Count(sim) == 1, 3000);
        EntityId ship = default;
        for (int i = 1; i < sim.Units.Capacity; i++) if (sim.Units.Slots.IsIndexAlive(i)) ship = sim.Units.Slots.IdOf(i);
        Assert.Equal(LandType.Water, sim.Map.LandAt(sim.Units.Position[ship.Index].ToCell()));

        Step(sim, Build(sim, ProductionCategory.Vehicle, tank));
        RunUntil(sim, () => Count(sim) == 2, 3000);
        EntityId t = default;
        for (int i = 1; i < sim.Units.Capacity; i++) if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.TypeIndex[i] == tank) t = sim.Units.Slots.IdOf(i);
        CellCoord tc = sim.Units.Position[t.Index].ToCell();
        Assert.InRange(tc.X, 3, 9);    // left the land factory at (4, 8), not the yard at (12, 12)
        Assert.InRange(tc.Y, 8, 14);
    }
}
