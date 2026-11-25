// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.S44;

/// <summary>S44 M-18: dive and surfacing take <c>dive_ticks</c>, the boat keeps its old armour and visibility meanwhile, the torpedo hits
/// surface ships and sonar-seen dived boats. Open sea. "sub" (dive 10 ticks, torpedo in slot 2), "quick" (dive 0), "boat", "picket" (sonar).</summary>
public sealed class SubmarineTests
{
    private const int Sub = 0, Quick = 1, Boat = 2, Picket = 3, W = 40, H = 30;
    private const int Torpedo = 0;
    private const int DiveTicks = 10;
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

    private static SimSetup Setup()
    {
        var cells = new LandType[W * H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) cells[y * W + x] = x >= 30 ? LandType.Clear : LandType.Water;
        var map = new MapData("sea", "rust_belt", W, H, new ushort[W * H], cells, new byte[W * H], new OverlayKind[W * H], new byte[W * H],
            new[] { new CellCoord(1, 1), new CellCoord(W - 2, H - 2) });
        VehicleType Ship(string id, int hp) => ScriptedGame.Vehicle(id, 40, hp) with { Locomotion = Locomotion.Naval, TurnRate = 16, PrimaryWeapon = -1, Armor = ArmorClass.ShipLight };
        VehicleType[] vehicles =
        {
            Ship("sub", 1000) with { Submerges = true, DiveTicks = DiveTicks, SecondaryWeapon = Torpedo },
            Ship("quick", 1000) with { Submerges = true },
            Ship("boat", 1000),
            Ship("picket", 1000) with { SonarRadius = 8 },
        };
        var weapons = new[] { new WeaponType("torpedo", 100, 6 * 256, 0, 10, 0, 0) { Targets = WeaponTargets.NavalAndSubmerged } };
        var warheads = new[] { new WarheadType("w", new[] { 100, 100, 100, 100, 100, 100, 100, 100 }, 0, false) };
        SimRules rules = ScriptedGame.RulesWith(vehicles, Array.Empty<InfantryType>()) with { Weapons = weapons, Warheads = warheads, Terrain = Terrain() };
        return new SimSetup(7, map, 2, rules, 0xABCDUL) { AllowDebug = true };
    }

    private Command Dive(Simulation s, EntityId who, byte player = 0) =>
        new(s.Tick, player, ++_seq, CommandKind.Deploy, who, default, default, (int)ActorKind.Vehicle);

    private static void Run(Simulation s, int n) { for (int i = 0; i < n; i++) s.Step(Array.Empty<Command>()); }

    [Fact]
    public void Dive_TakesDiveTicks_KeepsSurfacedArmourAndVisibilityMeanwhile()
    {
        var s = new Simulation(Setup());
        EntityId sub = s.SpawnVehicle(0, Sub, new CellCoord(10, 10));
        s.Step(new[] { Dive(s, sub) });
        for (int n = 1; n < DiveTicks - 1; n++)
        {
            Assert.False(s.Naval.Submerged[sub.Index], $"still diving after {n} ticks");
            Assert.False(s.IsHiddenSub(ActorKind.Vehicle, sub.Index));
            Assert.Equal(ArmorClass.ShipLight, s.ArmorAt(ActorKind.Vehicle, sub.Index));
            s.Step(Array.Empty<Command>());
        }
        Run(s, 2);
        Assert.True(s.Naval.Submerged[sub.Index]);
        Assert.Equal(0, s.DiveTicksLeft(sub.Index));
        Assert.Equal(ArmorClass.Submerged, s.ArmorAt(ActorKind.Vehicle, sub.Index));
    }

    [Fact]
    public void Surfacing_TakesDiveTicks_StaysHiddenMeanwhile()
    {
        var s = new Simulation(Setup());
        EntityId sub = s.SpawnVehicle(0, Sub, new CellCoord(10, 10));
        s.Naval.Submerged[sub.Index] = true;
        s.Step(new[] { Dive(s, sub) });
        Run(s, 3);
        Assert.True(s.Naval.Submerged[sub.Index]);
        Assert.Equal(ArmorClass.Submerged, s.ArmorAt(ActorKind.Vehicle, sub.Index));
        Run(s, DiveTicks);
        Assert.False(s.Naval.Submerged[sub.Index]);
        Assert.Equal(ArmorClass.ShipLight, s.ArmorAt(ActorKind.Vehicle, sub.Index));
    }

    [Fact]
    public void DeployDuringTheChange_IsIgnored()
    {
        var s = new Simulation(Setup());
        EntityId sub = s.SpawnVehicle(0, Sub, new CellCoord(10, 10));
        s.Step(new[] { Dive(s, sub) });
        Run(s, 2);
        s.Step(new[] { Dive(s, sub) });   // would cancel or reverse it if honoured
        Run(s, DiveTicks);
        Assert.True(s.Naval.Submerged[sub.Index]);
    }

    [Fact]
    public void ZeroDiveTicks_FlipsAtOnce_AsBefore()
    {
        var s = new Simulation(Setup());
        EntityId q = s.SpawnVehicle(0, Quick, new CellCoord(10, 10));
        s.Step(new[] { Dive(s, q) });
        Assert.True(s.Naval.Submerged[q.Index]);
        s.Step(new[] { Dive(s, q) });
        Assert.False(s.Naval.Submerged[q.Index]);
    }

    private (Simulation S, EntityId Boat, EntityId Foe) TorpedoScene(bool picket)
    {
        var s = new Simulation(Setup());
        EntityId sub = s.SpawnVehicle(0, Sub, new CellCoord(10, 10));
        EntityId boat = s.SpawnVehicle(1, Boat, new CellCoord(14, 10));
        EntityId foe = s.SpawnVehicle(1, Quick, new CellCoord(10, 13));
        if (picket) s.SpawnVehicle(0, Picket, new CellCoord(10, 11));
        s.Naval.Submerged[foe.Index] = true;
        Run(s, 80);
        return (s, boat, foe);
    }

    [Fact]
    public void Torpedo_HitsASurfaceShip_ButNotADivedBoatNobodySees()
    {
        var (s, boat, foe) = TorpedoScene(picket: false);
        Assert.True(s.Units.Health[boat.Index] < 1000, "torpedo hits the surface ship");
        Assert.Equal(1000, s.Units.Health[foe.Index]);
    }

    [Fact]
    public void Torpedo_HitsADivedBoat_OnceSonarSeesIt()
    {
        var (s, _, foe) = TorpedoScene(picket: true);
        Assert.True(s.Units.Health[foe.Index] < 1000, "sonar lets the torpedo reach the dived boat");
    }

    [Fact]
    public void SaveRoundTrip_MidDive_KeepsTheTimer_AndHashesDiffer()
    {
        var setup = Setup();
        var s = new Simulation(setup);
        EntityId sub = s.SpawnVehicle(0, Sub, new CellCoord(10, 10));
        ulong idle = s.ComputeStateHash();
        s.Step(new[] { Dive(s, sub) });
        Run(s, 3);
        Assert.NotEqual(0, s.DiveTicksLeft(sub.Index));
        var ms = new MemoryStream();
        s.Save(ms);
        ms.Position = 0;
        Simulation loaded = Simulation.Load(ms, setup);
        Assert.Equal(s.DiveTicksLeft(sub.Index), loaded.DiveTicksLeft(sub.Index));
        Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
        Run(s, DiveTicks); Run(loaded, DiveTicks);
        Assert.True(loaded.Naval.Submerged[sub.Index]);
        Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.NotEqual(idle, s.ComputeStateHash());
    }

    [Fact]
    public void ShippedRules_MissileSubDivesInTime_CarriesATorpedo_AndTheStaleTodosAreGone()
    {
        RuleSet rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!);
        VehicleType sub = rules.Sim.Vehicles.Single(v => v.Id == "exp_missile_sub");
        Assert.True(sub.DiveTicks > 0);
        Assert.Equal(ArmorClass.ShipLight, sub.Armor);
        WeaponType torp = rules.Sim.Weapons[sub.SecondaryWeapon];
        Assert.Equal("exp_sub_torpedo", torp.Id);
        Assert.Equal(WeaponTargets.NavalAndSubmerged, torp.Targets);
        WarheadType wh = rules.Sim.Warheads[torp.Warhead];
        Assert.True(wh.VersusPercent[(int)ArmorClass.ShipLight] >= 100 && wh.VersusPercent[(int)ArmorClass.ShipHeavy] >= 100);
        Assert.Equal(0, wh.VersusPercent[(int)ArmorClass.Structure]);
        Assert.All(rules.Sim.Vehicles.Where(v => v.Id != "exp_missile_sub"), v => Assert.Equal(0, v.DiveTicks));
        string text = File.ReadAllText(Path.Combine(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!, "vehicles.toml"));
        Assert.DoesNotContain("TODO M7-05", text);
        Assert.DoesNotContain("naval gunfire uses Attack on a cell", text);
    }
}
