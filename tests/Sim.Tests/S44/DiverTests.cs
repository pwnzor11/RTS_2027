// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.S44;

/// <summary>S44 M-02: the Combat Diver swims. Sea map: Water for x &lt; 30, Shallow at x 28..29, Clear land from x 30. Infantry 0 = "diver" (swims), 1 = "rifle";
/// vehicles 0 = "gunboat" (naval), 1 = "picket" (naval, sonar 8).</summary>
public sealed class DiverTests
{
    private const int Diver = 0, Rifle = 1, Gunboat = 0, Picket = 1, W = 40, H = 30;
    private ushort _seq;

    private static TerrainTable Terrain()
    {
        var speed = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        foreach (LandType land in Enum.GetValues<LandType>())
            for (int l = 0; l < RuleCounts.Locomotions; l++)
            {
                bool water = land is LandType.Water or LandType.Shallow;
                speed[(int)land * RuleCounts.Locomotions + l] = (Locomotion)l == Locomotion.Naval ? (water ? 100 : 0) : water ? 0 : 100;
            }
        var buildable = new bool[RuleCounts.LandTypes];
        buildable[(int)LandType.Clear] = true;
        return new TerrainTable(speed, buildable);
    }

    private static SimSetup Setup(bool divers = true, ulong seed = 7)
    {
        var cells = new LandType[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++) cells[y * W + x] = x >= 30 ? LandType.Clear : x >= 28 ? LandType.Shallow : LandType.Water;
        var map = new MapData("sea", "rust_belt", W, H, new ushort[W * H], cells, new byte[W * H], new OverlayKind[W * H], new byte[W * H],
            new[] { new CellCoord(1, 1), new CellCoord(W - 2, H - 2) });
        VehicleType Ship(string id, int weapon) =>
            ScriptedGame.Vehicle(id, 40, 100) with { Locomotion = Locomotion.Naval, TurnRate = 16, PrimaryWeapon = weapon };
        VehicleType[] vehicles = { Ship("gunboat", -1), Ship("picket", -1) with { SonarRadius = 8 } };
        InfantryType[] infantry =
        {
            ScriptedGame.Infantry("diver", 20, 50) with { PrimaryWeapon = 0, Swims = divers },
            ScriptedGame.Infantry("rifle", 20, 50),
        };
        var weapons = new[] { new WeaponType("charge", 120, 2 * 256, 0, 10, 0, 0) { Targets = WeaponTargets.Ground } };
        var warheads = new[] { new WarheadType("w", new[] { 100, 100, 100, 100, 100, 100, 100, 100 }, 0, false) };
        SimRules rules = ScriptedGame.RulesWith(vehicles, infantry) with { Weapons = weapons, Warheads = warheads, Terrain = Terrain() };
        return new SimSetup(seed, map, 2, rules, 0xABCDUL) { AllowDebug = true };
    }

    private Command Move(Simulation s, EntityId who, CellCoord to, byte player = 0) =>
        new(s.Tick, player, ++_seq, CommandKind.Move, who, default, to, (int)ActorKind.Infantry);

    private static void Run(Simulation s, int n) { for (int i = 0; i < n; i++) s.Step(Array.Empty<Command>()); }

    private static CellCoord CellOf(Simulation s, EntityId e) => s.Infantry.Position[e.Index].ToCell();

    [Fact]
    public void Diver_CrossesWater_RifleCannot()
    {
        var s = new Simulation(Setup());
        EntityId diver = s.SpawnInfantry(0, Diver, new CellCoord(31, 10)), rifle = s.SpawnInfantry(0, Rifle, new CellCoord(31, 14));
        s.Step(new[] { Move(s, diver, new CellCoord(15, 10)), Move(s, rifle, new CellCoord(15, 14)) });
        Run(s, 400);
        Assert.Equal(new CellCoord(15, 10), CellOf(s, diver));
        Assert.True(CellOf(s, rifle).X >= 28, "a rifleman stays on the beach or fords no further than the Shallow");
        Assert.NotEqual(LandType.Water, s.Map.LandAt(CellOf(s, rifle)));
    }

    [Fact]
    public void Diver_IsHiddenInDeepWater_NotOnLand()
    {
        var s = new Simulation(Setup());
        EntityId wet = s.SpawnInfantry(0, Diver, new CellCoord(10, 10)), dry = s.SpawnInfantry(0, Diver, new CellCoord(33, 10));
        EntityId rifleWet = s.SpawnInfantry(0, Rifle, new CellCoord(34, 10));
        Run(s, 2);
        Assert.True(s.IsHiddenFrom(1, ActorKind.Infantry, wet.Index));
        Assert.True(s.IsUntargetableBy(1, ActorKind.Infantry, wet.Index));
        Assert.False(s.IsHiddenFrom(0, ActorKind.Infantry, wet.Index), "its own team always sees it");
        Assert.False(s.IsHiddenFrom(1, ActorKind.Infantry, dry.Index));
        Assert.False(s.IsHiddenFrom(1, ActorKind.Infantry, rifleWet.Index));
    }

    [Fact]
    public void Diver_WithoutTheFlag_IsAnOrdinarySoldier()
    {
        var s = new Simulation(Setup(divers: false));
        EntityId d = s.SpawnInfantry(0, Diver, new CellCoord(31, 10));
        s.Step(new[] { Move(s, d, new CellCoord(15, 10)) });
        Run(s, 300);
        Assert.True(CellOf(s, d).X >= 28);
    }

    [Fact]
    public void SonarPicket_RevealsDiverWithinRadius_Only()
    {
        var s = new Simulation(Setup());
        EntityId near = s.SpawnInfantry(0, Diver, new CellCoord(12, 10)), far = s.SpawnInfantry(0, Diver, new CellCoord(12, 25));
        s.SpawnVehicle(1, Picket, new CellCoord(16, 12));
        Run(s, 2);
        Assert.False(s.IsHiddenFrom(1, ActorKind.Infantry, near.Index), "4 cells from the picket, radius 8");
        Assert.False(s.IsUntargetableBy(1, ActorKind.Infantry, near.Index));
        Assert.True(s.IsHiddenFrom(1, ActorKind.Infantry, far.Index), "13 cells away");
    }

    [Fact]
    public void Diver_PlantsCharge_OnAShipFromTheWater()
    {
        var s = new Simulation(Setup());
        EntityId diver = s.SpawnInfantry(0, Diver, new CellCoord(10, 10));
        EntityId ship = s.SpawnVehicle(1, Gunboat, new CellCoord(11, 10));
        Assert.Equal(100, s.Units.Health[ship.Index]);
        s.Step(new[] { new Command(s.Tick, 0, ++_seq, CommandKind.Attack, diver, ship, default, (int)ActorKind.Infantry | ((int)ActorKind.Vehicle << 8)) });
        Run(s, 60);
        Assert.True(s.Units.Health[ship.Index] < 100, "the charge hit the boat");
        Assert.Equal(LandType.Water, s.Map.LandAt(CellOf(s, diver)));
    }

    private static ulong Play(ulong seed)
    {
        var s = new Simulation(Setup(seed: seed));
        EntityId a = s.SpawnInfantry(0, Diver, new CellCoord(31, 10)), b = s.SpawnInfantry(0, Diver, new CellCoord(32, 10), 2);
        s.SpawnVehicle(1, Picket, new CellCoord(16, 12));
        var seq = (ushort)0;
        s.Step(new[]
        {
            new Command(s.Tick, 0, ++seq, CommandKind.Move, a, default, new CellCoord(12, 9), (int)ActorKind.Infantry),
            new Command(s.Tick, 0, ++seq, CommandKind.Move, b, default, new CellCoord(20, 20), (int)ActorKind.Infantry),
        });
        for (int i = 0; i < 300; i++) s.Step(Array.Empty<Command>());
        return s.ComputeStateHash();
    }

    [Fact]
    public void Swimming_IsDeterministic()
    {
        Assert.Equal(Play(3), Play(3));
        Assert.NotEqual(Play(3), Play(4));
    }
}
