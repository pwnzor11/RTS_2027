// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.M3.Superweapon;

/// <summary>M3-13 test rules built in code: a power plant, a Sunrise-like superweapon (100 ticks, range 8), a Hatch (small
/// entrance), a wall-less target building and a tough vehicle. Charge 100 ticks keeps the tests short.</summary>
internal static class SuperweaponFixture
{
    public const int Plant = 0, Sun = 1, Hatch = 2, Hut = 3;
    public const int Brute = 0;
    public const int Total = 100, EntRange = 8;
    public static readonly Command[] None = Array.Empty<Command>();
    private static ushort _seq;

    public static SimRules Rules()
    {
        var none = Array.Empty<string>();
        var blast = new WarheadType("sw_blast", new[] { 100, 100, 100, 100, 100 }, 1, false);
        BuildingType B(string id, int power = 0) => new(id, 2, 2, 5000, ArmorClass.Structure, 5, -1, 0, power, none, new[] { id });
        var buildings = new[]
        {
            B("plant", 300),
            B("sun", -20) with { Superweapon = "sw", SuperweaponChargeTicks = Total, SuperweaponEntranceRange = EntRange },
            B("hatch") with { Entrance = TunnelSize.S },
            B("hut"),
        };
        var vehicles = new[] { new VehicleType("brute", Locomotion.Tracked, 16, 8, 2000, ArmorClass.Light, 5, -1, false, 0, false, 0, none) };
        var terrain = new TerrainTable(Fill(RuleCounts.LandTypes * RuleCounts.Locomotions, 100), Buildable());
        return new SimRules(vehicles, Array.Empty<InfantryType>(), buildings, Array.Empty<WeaponType>(), new[] { blast }, terrain,
            Array.Empty<EffectDef>());
    }

    private static int[] Fill(int n, int v) { var a = new int[n]; Array.Fill(a, v); return a; }

    private static bool[] Buildable() { var b = new bool[RuleCounts.LandTypes]; b[(int)LandType.Clear] = true; return b; }

    public static Simulation NewSim(ulong seed = 1)
    {
        var setup = new SimSetup(seed, MapData.CreateFlat(48, 48), 2, Rules(), 0x3613UL) { AllowDebug = true };
        return new Simulation(setup);
    }

    /// <summary>Player 0 base: plant, Sunrise and a Hatch (placed at 10,10 / 14,10 / 20,10); player 1 holds a hut far away.</summary>
    public static (Simulation Sim, EntityId Sun) Base(bool hatch = true, bool plant = true)
    {
        Simulation sim = NewSim();
        if (plant) sim.PlaceBuilding(0, Plant, new CellCoord(10, 10));
        EntityId sun = sim.PlaceBuilding(0, Sun, new CellCoord(14, 10));
        if (hatch) sim.PlaceBuilding(0, Hatch, new CellCoord(20, 10));
        sim.PlaceBuilding(1, Hut, new CellCoord(44, 44));
        return (sim, sun);
    }

    public static Command Fire(Simulation sim, byte player, EntityId sun, int x, int y) =>
        new(sim.Tick, player, _seq++, CommandKind.Special, sun, default, new CellCoord(x, y), 0);

    public static void Run(Simulation sim, int ticks, params Command[] first)
    {
        for (int i = 0; i < ticks; i++) sim.Step(i == 0 ? first : None);
    }

    public static Simulation Roundtrip(Simulation sim)
    {
        var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        return Simulation.Load(ms, sim.Setup);
    }
}
