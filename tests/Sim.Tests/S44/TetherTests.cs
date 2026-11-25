// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.S44;

/// <summary>S44 M-05: a harpoon warhead tethers (slows) a surviving target for N ticks; other warheads change nothing; state hashes and saves.</summary>
public sealed class TetherTests
{
    private const int TetherTicks = 40, TetherPermille = 500;

    // vehicles: 0 gunner (weapon 0 = tether warhead, weapon 1 = plain), 1 plain gunner, 2 target. Weapons reload so slowly only one shot lands.
    private static Simulation Make(out EntityId tetherGun, out EntityId plainGun, out EntityId targetA, out EntityId targetB)
    {
        var versus = new[] { 100, 100, 100, 100, 100 };
        var warheads = new[]
        {
            new WarheadType("a_harpoon", versus, 0, false) { TetherTicks = TetherTicks, TetherSpeedPermille = TetherPermille },
            new WarheadType("b_plain", versus, 0, false),
        };
        var weapons = new[]
        {
            new WeaponType("a_harpoon_gun", 10, 8 * 256, 0, 5000, 0, 0),
            new WeaponType("b_plain_gun", 10, 8 * 256, 0, 5000, 0, 1),
        };
        var vehicles = new[]
        {
            ScriptedGame.Vehicle("a_gunner", 10, 500) with { PrimaryWeapon = 0 },
            ScriptedGame.Vehicle("b_plain_gunner", 10, 500) with { PrimaryWeapon = 1 },
            ScriptedGame.Vehicle("c_target", 20, 1000),
        };
        SimRules rules = ScriptedGame.RulesWith(vehicles, Array.Empty<InfantryType>()) with { Weapons = weapons, Warheads = warheads };
        var sim = new Simulation(new SimSetup(1, Rts.Sim.Map.MapData.CreateFlat(64, 64), 2, rules, 0x4405UL) { AllowDebug = true });
        sim.PlaceBuilding(0, 0, new CellCoord(2, 2));
        sim.PlaceBuilding(1, 0, new CellCoord(60, 60));
        tetherGun = sim.SpawnVehicle(0, 0, new CellCoord(10, 10));
        plainGun = sim.SpawnVehicle(0, 1, new CellCoord(10, 14));
        targetA = sim.SpawnVehicle(1, 2, new CellCoord(14, 10));
        targetB = sim.SpawnVehicle(1, 2, new CellCoord(14, 14));
        return sim;
    }

    private static Command Attack(Simulation sim, EntityId gun, EntityId target) =>
        new(sim.Tick, 0, 1, CommandKind.Attack, gun, target, default, (int)ActorKind.Vehicle | ((int)ActorKind.Vehicle << 8));

    private static void Run(Simulation sim, int ticks, params Command[] first)
    {
        sim.Step(first);
        for (int t = 1; t < ticks; t++) sim.Step(Array.Empty<Command>());
    }

    [Fact]
    public void Hit_TethersTheTarget_ThenItExpires()
    {
        Simulation sim = Make(out var gun, out _, out var a, out _);
        Run(sim, 3, Attack(sim, gun, a));
        Assert.True(sim.Units.Health[a.Index] < 1000, "the harpoon hit");
        Assert.True(sim.Effects.VehicleTetherTicks[a.Index] > 0);
        Assert.Equal(TetherPermille, sim.Effects.VehicleTetherPermille[a.Index]);
        Assert.Equal(TetherPermille, sim.TetherPermille(ActorKind.Vehicle, a.Index));
        Run(sim, TetherTicks + 2);
        Assert.Equal(0, sim.Effects.VehicleTetherTicks[a.Index]);
        Assert.Equal(0, sim.Effects.VehicleTetherPermille[a.Index]);
        Assert.Equal(1000, sim.TetherPermille(ActorKind.Vehicle, a.Index));
    }

    [Fact]
    public void OtherWarheads_LeaveTheTargetUntethered()
    {
        Simulation sim = Make(out _, out var plain, out _, out var b);
        Run(sim, 3, Attack(sim, plain, b));
        Assert.True(sim.Units.Health[b.Index] < 1000, "the plain shell hit");
        Assert.Equal(0, sim.Effects.VehicleTetherTicks[b.Index]);
        Assert.Equal(1000, sim.TetherPermille(ActorKind.Vehicle, b.Index));
    }

    [Fact]
    public void TetheredVehicle_MovesSlower_ThanAnUntetheredOne()
    {
        Simulation sim = Make(out var gun, out _, out var a, out var b);
        Run(sim, 3, Attack(sim, gun, a));
        int ax0 = sim.Units.Position[a.Index].X, bx0 = sim.Units.Position[b.Index].X;
        var moves = new[]
        {
            new Command(sim.Tick, 1, 2, CommandKind.Move, a, default, new CellCoord(50, 10), (int)ActorKind.Vehicle),
            new Command(sim.Tick, 1, 3, CommandKind.Move, b, default, new CellCoord(50, 14), (int)ActorKind.Vehicle),
        };
        Run(sim, 30, moves);
        int da = sim.Units.Position[a.Index].X - ax0, db = sim.Units.Position[b.Index].X - bx0;
        Assert.True(db > 0 && da > 0, "both moved");
        Assert.True(da * 10 < db * 8, $"tethered {da} should be well under untethered {db}");
    }

    [Fact]
    public void SecondHit_RefreshesTheTimer_AndKeepsTheStrongerSlow()
    {
        Simulation sim = Make(out _, out _, out var a, out _);
        sim.Effects.VehicleTetherTicks[a.Index] = 5; sim.Effects.VehicleTetherPermille[a.Index] = 300;
        sim.OnTetherHit(ActorKind.Vehicle, a.Index, 0, 0);
        Assert.Equal(TetherTicks, sim.Effects.VehicleTetherTicks[a.Index]);
        Assert.Equal(300, sim.Effects.VehicleTetherPermille[a.Index]);
    }

    [Fact]
    public void TetherState_IsHashedAndSurvivesSaveLoad()
    {
        Simulation sim = Make(out var gun, out _, out var a, out _);
        sim.ComputeHashes(out ulong clean);
        Run(sim, 3, Attack(sim, gun, a));
        Assert.True(sim.Effects.VehicleTetherTicks[a.Index] > 0);
        Simulation twin = Make(out var gun2, out _, out var a2, out _);
        Run(twin, 3, Attack(twin, gun2, a2));
        twin.ComputeHashes(out ulong h1); sim.ComputeHashes(out ulong h2);
        Assert.Equal(h1, h2);
        sim.Effects.VehicleTetherTicks[a.Index]++;
        sim.ComputeHashes(out ulong h3);
        Assert.NotEqual(h2, h3);
        sim.Effects.VehicleTetherTicks[a.Index]--;
        var ms = new MemoryStream();
        sim.Save(ms); ms.Position = 0;
        Simulation loaded = Simulation.Load(ms, sim.Setup);
        Assert.Equal(sim.Effects.VehicleTetherTicks[a.Index], loaded.Effects.VehicleTetherTicks[a.Index]);
        Assert.Equal(TetherPermille, loaded.Effects.VehicleTetherPermille[a.Index]);
        for (int t = 0; t < 60; t++) { sim.Step(Array.Empty<Command>()); loaded.Step(Array.Empty<Command>()); }
        sim.ComputeHashes(out ulong end1); loaded.ComputeHashes(out ulong end2);
        Assert.Equal(end1, end2);
        Assert.NotEqual(clean, end1);
    }

    [Fact]
    public void NoTetherAnywhere_SavesInTheOldLayout()
    {
        Simulation sim = Make(out _, out _, out _, out _);
        Assert.False(sim.Effects.AnyTether);
        var ms = new MemoryStream();
        sim.Effects.Write(new BinaryWriter(ms));
        Assert.Equal(2, ms.ToArray()[0]);   // version byte: the quiet layout of before S44
    }
}
