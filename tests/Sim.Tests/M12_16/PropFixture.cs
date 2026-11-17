// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.M12_16;

/// <summary>M12-16 test fixture: flat maps with props placed by cell, and a rules set with a splash warhead and a shell weapon.</summary>
internal static class PropFixture
{
    /// <summary>Index of the splash warhead in <see cref="Rules"/> (versus 100 on every class, splash 1).</summary>
    public const int SplashWarhead = 1;

    /// <summary>Index of the splash-0 warhead (small arms).</summary>
    public const int SmallArmsWarhead = 0;

    /// <summary>A flat map of n x n cells with <paramref name="props"/> placed on it (density 0).</summary>
    public static MapData Map(int n, params (CellCoord Cell, OverlayKind Kind)[] props)
    {
        MapData flat = MapData.CreateFlat(n, n, "props");
        var ovl = new OverlayKind[n * n];
        foreach (var (cell, kind) in props) ovl[cell.Y * n + cell.X] = kind;
        return new MapData("props", "rust_belt", n, n, new ushort[n * n], new LandType[n * n], new byte[n * n], ovl, new byte[n * n], flat.StartPositions);
    }

    /// <summary>Terrain where Rough runs at 75 % for every locomotion and everything else at 100 % (Water, Rock, Cliff blocked).</summary>
    public static TerrainTable Terrain()
    {
        var speed = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        Array.Fill(speed, 100);
        foreach (var land in new[] { LandType.Water, LandType.Rock, LandType.Cliff })
            for (int l = 0; l < RuleCounts.Locomotions; l++) speed[(int)land * RuleCounts.Locomotions + l] = 0;
        for (int l = 0; l < RuleCounts.Locomotions; l++) speed[(int)LandType.Rough * RuleCounts.Locomotions + l] = 75;
        var buildable = new bool[RuleCounts.LandTypes];
        buildable[(int)LandType.Clear] = true; buildable[(int)LandType.Road] = true;
        return new TerrainTable(speed, buildable);
    }

    /// <summary>Vehicle type indexes of <see cref="Vehicles"/>.</summary>
    public const int Tank = 0, Jeep = 1, Hauler = 2, Plane = 3, Truck = 4;

    /// <summary>Vehicle types: 0 heavy tracked crusher ("tank"), 1 wheeled light jeep, 2 heavy tracked non-crusher ("hauler"), 3 air, 4 wheeled crusher ("truck").</summary>
    public static VehicleType[] Vehicles() => new[]
    {
        new VehicleType("tank", Locomotion.Tracked, 40, 8, 400, ArmorClass.Heavy, 5, -1, false, 0, true, 0, Array.Empty<string>()),
        new VehicleType("jeep", Locomotion.Wheeled, 60, 8, 150, ArmorClass.Light, 5, -1, false, 0, false, 0, Array.Empty<string>()),
        new VehicleType("hauler", Locomotion.Tracked, 40, 8, 350, ArmorClass.Heavy, 5, -1, false, 0, false, 0, Array.Empty<string>()),
        new VehicleType("plane", Locomotion.Air, 60, 8, 100, ArmorClass.Light, 5, -1, false, 0, false, 0, Array.Empty<string>()),
        new VehicleType("truck", Locomotion.Wheeled, 40, 8, 300, ArmorClass.Heavy, 5, -1, false, 0, true, 0, Array.Empty<string>()),   // wheeled with the crusher flag: still never crushes a prop
    };

    /// <summary>Rules with <see cref="Vehicles"/>, one infantry type, the fixture terrain and two warheads (small arms, splash).</summary>
    public static SimRules Rules(PropRules? props = null) =>
        new(Vehicles(), new[] { ScriptedGame.Infantry("rifle", 9, 50) }, ScriptedGame.Buildings(), Array.Empty<WeaponType>(),
            new[]
            {
                new WarheadType("a_small_arms", new[] { 100, 100, 100, 100, 100, 100, 100, 100 }, 0, false),
                new WarheadType("b_splash", new[] { 100, 100, 100, 100, 100, 100, 100, 100 }, 1, false),
            }, Terrain(), Array.Empty<EffectDef>()) { Props = props ?? PropRules.Default };

    /// <summary>A two-player sim on <paramref name="map"/> with <see cref="Rules"/>.</summary>
    public static Simulation Sim(MapData map, PropRules? props = null, ulong seed = 1) =>
        new(new SimSetup(seed, map, 2, Rules(props), 1) { AllowDebug = true });
}
