// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.S44;

/// <summary>S44 M-04 (second and third weapon slot) and M-26 (SIMCOMBAT-04/05/06). Types are built in code on an open sea (land from x = 30):
/// "ship" (gun + depth charge + AA, sonar), "sub", "boat", "jet", "hunter" (depth charge only), "stealth" (ground gun), "booster" (fire-rate aura).</summary>
public sealed class SecondWeaponTests
{
    private const int Ship = 0, Sub = 1, Boat = 2, Jet = 3, Hunter = 4, Stealth = 5, Booster = 6, Dummy = 7, W = 40, H = 30;
    private const int Shell = 0, Charge = 1, Aa = 2, Gun = 3;
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

    private static SimSetup Setup(bool secondSlots = true)
    {
        var cells = new LandType[W * H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) cells[y * W + x] = x >= 30 ? LandType.Clear : LandType.Water;
        var map = new MapData("sea", "rust_belt", W, H, new ushort[W * H], cells, new byte[W * H], new OverlayKind[W * H], new byte[W * H],
            new[] { new CellCoord(1, 1), new CellCoord(W - 2, H - 2) });
        VehicleType Naval(string id, int weapon) =>
            ScriptedGame.Vehicle(id, 40, 1000) with { Locomotion = Locomotion.Naval, TurnRate = 16, PrimaryWeapon = weapon };
        VehicleType[] vehicles =
        {
            Naval("ship", Shell) with { SonarRadius = 8, SecondaryWeapon = secondSlots ? Charge : -1, TertiaryWeapon = secondSlots ? Aa : -1 },
            Naval("sub", -1) with { Submerges = true },
            Naval("boat", -1),
            ScriptedGame.Vehicle("jet", 40, 1000) with { Locomotion = Locomotion.Air, PrimaryWeapon = -1 },
            Naval("hunter", Charge),
            ScriptedGame.Vehicle("stealth", 40, 1000) with { PrimaryWeapon = Gun, Stealth = true },
            ScriptedGame.Vehicle("booster", 40, 1000) with { AuraRadius = 8, AuraRofPermille = 500, AuraTargets = new[] { "class:vehicle" } },
            ScriptedGame.Vehicle("dummy", 40, 100000),
        };
        WeaponType Weapon(string id, WeaponTargets t) => new(id, 10, 6 * 256, 0, 10, 0, 0) { Targets = t };
        var weapons = new[]
        {
            Weapon("shell", WeaponTargets.Naval), Weapon("charge", WeaponTargets.Submerged), Weapon("aa", WeaponTargets.Air), Weapon("gun", WeaponTargets.Ground),
        };
        var warheads = new[] { new WarheadType("w", new[] { 100, 100, 100, 100, 100, 100, 100, 100 }, 0, false) };
        SimRules rules = ScriptedGame.RulesWith(vehicles, Array.Empty<InfantryType>()) with { Weapons = weapons, Warheads = warheads, Terrain = Terrain() };
        return new SimSetup(7, map, 2, rules, 0xABCDUL) { AllowDebug = true };
    }

    private Command Cmd(Simulation s, CommandKind k, EntityId who, byte player = 0) =>
        new(s.Tick, player, ++_seq, k, who, default, default, (int)ActorKind.Vehicle);

    private Command GuardCmd(Simulation s, EntityId who, CellCoord at, byte player = 0) =>
        new(s.Tick, player, ++_seq, CommandKind.Guard, who, default, at, (int)ActorKind.Vehicle);

    private static void Run(Simulation s, int n) { for (int i = 0; i < n; i++) s.Step(Array.Empty<Command>()); }

    private static int Hp(Simulation s, EntityId e) => s.Units.Health[e.Index];

    /// <summary>Ship (player 0) at (10,10) on guard, an enemy dived sub at (14,10), an enemy boat at (10,14) and an enemy jet at (10,7).</summary>
    private (Simulation S, EntityId Ship, EntityId Sub, EntityId Boat, EntityId Jet) Scene(bool secondSlots = true)
    {
        var s = new Simulation(Setup(secondSlots));
        EntityId ship = s.SpawnVehicle(0, Ship, new CellCoord(10, 10));
        EntityId sub = s.SpawnVehicle(1, Sub, new CellCoord(14, 10));
        EntityId boat = s.SpawnVehicle(1, Boat, new CellCoord(10, 14));
        EntityId jet = s.SpawnVehicle(1, Jet, new CellCoord(10, 7));
        s.Step(new[] { Cmd(s, CommandKind.Deploy, sub, 1), GuardCmd(s, ship, new CellCoord(10, 10)) });
        Assert.True(s.Naval.Submerged[sub.Index]);
        return (s, ship, sub, boat, jet);
    }

    [Fact]
    public void EachSlot_FiresAtItsOwnTarget()
    {
        var (s, ship, sub, boat, jet) = Scene();
        Run(s, 40);
        Assert.True(Hp(s, boat) < 1000, "primary gun hits the surface ship");
        Assert.True(Hp(s, sub) < 1000, "depth charge hits the dived sub (sonar reveals it)");
        Assert.True(Hp(s, jet) < 1000, "AA mount hits the aircraft");
        Assert.Equal(1000, Hp(s, ship));
    }

    [Fact]
    public void Slots_KeepTheirOwnCooldowns_AndMayFireInTheSameTick()
    {
        var (s, ship, sub, boat, jet) = Scene();
        bool both = false;
        for (int n = 0; n < 60 && !both; n++)
        {
            int u = Hp(s, sub), j = Hp(s, jet);
            Run(s, 1);
            both = Hp(s, sub) < u && Hp(s, jet) < j;
        }
        Assert.True(both, "depth charge and AA fired in one tick");
        ref var e = ref s.CombatData.At(ActorKind.Vehicle, ship.Index);
        Assert.True(e.Reload2 > 0 && e.Reload3 > 0);
        int r2 = e.Reload2;
        Run(s, 1);
        Assert.Equal(r2 - 1, s.CombatData.At(ActorKind.Vehicle, ship.Index).Reload2);
    }

    [Fact]
    public void AaSlot_NeverHitsGround_AndDepthChargeNeverHitsSurface()
    {
        var s = new Simulation(Setup());
        EntityId ship = s.SpawnVehicle(0, Ship, new CellCoord(10, 10));
        EntityId boat = s.SpawnVehicle(1, Boat, new CellCoord(10, 14));
        s.Step(new[] { GuardCmd(s, ship, new CellCoord(10, 10)) });
        Run(s, 30);
        Assert.True(Hp(s, boat) < 1000);
        // The boat took shell hits only: 10 per shot, one shot per 10 ticks at most. Depth charge and AA would double that.
        int shots = (1000 - Hp(s, boat)) / 10;
        Assert.True(shots <= 4, $"only the primary hits a surface ship (shots {shots})");
    }

    [Fact]
    public void UnitWithoutSecondSlot_NeverUsesExtraReloads_AndStaysDefault()
    {
        var (s, ship, sub, boat, jet) = Scene(secondSlots: false);
        Run(s, 40);
        Assert.True(Hp(s, boat) < 1000);
        Assert.Equal(1000, Hp(s, sub));
        Assert.Equal(1000, Hp(s, jet));
        ref var e = ref s.CombatData.At(ActorKind.Vehicle, ship.Index);
        Assert.Equal(0, e.Reload2);
        Assert.Equal(0, e.Reload3);
    }

    [Fact]
    public void ExtraReloads_AreSavedAndHashed()
    {
        var (s, ship, sub, boat, jet) = Scene();
        Run(s, 12);
        Assert.True(s.CombatData.At(ActorKind.Vehicle, ship.Index).Reload2 > 0 || s.CombatData.At(ActorKind.Vehicle, ship.Index).Reload3 > 0);
        ulong before = s.ComputeStateHash();
        s.CombatData.At(ActorKind.Vehicle, ship.Index).Reload3 += 7;
        Assert.NotEqual(before, s.ComputeStateHash());
        s.CombatData.At(ActorKind.Vehicle, ship.Index).Reload3 -= 7;
        Assert.Equal(before, s.ComputeStateHash());

        var ms = new MemoryStream();
        s.Save(ms);
        ms.Position = 0;
        var loaded = Simulation.Load(ms, Setup());
        Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(s.CombatData.At(ActorKind.Vehicle, ship.Index).Reload2, loaded.CombatData.At(ActorKind.Vehicle, ship.Index).Reload2);
        for (int i = 0; i < 40; i++)
        {
            s.Step(Array.Empty<Command>()); loaded.Step(Array.Empty<Command>());
            Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
        }
    }

    [Fact]
    public void ShippedData_GivesDestroyerAndLaunchTheirSecondSlots()
    {
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        SimRules r = RulesLoader.Load(dir!).Sim;
        VehicleType destroyer = r.Vehicles.Single(v => v.Id == "exp_destroyer"), launch = r.Vehicles.Single(v => v.Id == "exp_launch");

        Assert.Equal("exp_destroyer_gun", r.Weapons[destroyer.PrimaryWeapon].Id);
        Assert.Equal(WeaponTargets.Submerged, r.Weapons[destroyer.SecondaryWeapon].Targets);
        Assert.Equal(WeaponTargets.Air, r.Weapons[destroyer.TertiaryWeapon].Targets);
        Assert.Equal(WeaponTargets.Submerged, r.Weapons[launch.PrimaryWeapon].Targets);
        Assert.Equal("exp_launch_gun", r.Weapons[launch.SecondaryWeapon].Id);
        Assert.Equal(-1, launch.TertiaryWeapon);
        Assert.All(r.Vehicles.Where(v => v.Id is not ("exp_destroyer" or "exp_launch")), v => Assert.True(v.SecondaryWeapon < 0 && v.TertiaryWeapon < 0));
    }

    // ---- M-26 ----------------------------------------------------------------------------------

    [Fact]
    public void M26_04_ActorAtZeroHealth_DoesNotFire()
    {
        var s = new Simulation(Setup(false));
        EntityId stealth = s.SpawnVehicle(0, Stealth, new CellCoord(32, 10));
        EntityId dummy = s.SpawnVehicle(1, Dummy, new CellCoord(34, 10));
        s.Step(new[] { GuardCmd(s, stealth, new CellCoord(32, 10)) });
        s.Units.Health[stealth.Index] = 0;          // left alive at 0 HP by a cleanup pass
        int before = Hp(s, dummy);
        s.Step(Array.Empty<Command>());
        Assert.Equal(before, Hp(s, dummy));
    }

    [Fact]
    public void M26_04_LivingActor_StillFires()
    {
        var s = new Simulation(Setup(false));
        EntityId stealth = s.SpawnVehicle(0, Stealth, new CellCoord(32, 10));
        EntityId dummy = s.SpawnVehicle(1, Dummy, new CellCoord(34, 10));
        s.Step(new[] { GuardCmd(s, stealth, new CellCoord(32, 10)) });
        Run(s, 30);
        Assert.True(Hp(s, dummy) < 100000);
    }

    [Fact]
    public void M26_05_DepthCharge_CanAimAtJustFiredSub()
    {
        var s = new Simulation(Setup(false));
        EntityId hunter = s.SpawnVehicle(0, Hunter, new CellCoord(10, 10));
        EntityId sub = s.SpawnVehicle(1, Sub, new CellCoord(14, 10));
        s.Step(new[] { Cmd(s, CommandKind.Deploy, sub, 1), GuardCmd(s, hunter, new CellCoord(10, 10)) });
        Assert.True(s.Naval.Submerged[sub.Index]);
        Run(s, 20);
        Assert.Equal(1000, Hp(s, sub));             // dived and hidden: no sonar, no shot
        for (int n = 0; n < 30; n++) { s.Naval.RevealLeft[sub.Index] = 100; Run(s, 1); }   // it just fired: visible for 5 s
        Assert.True(Hp(s, sub) < 1000, "the depth charge hits a dived sub that has just fired");
    }

    [Fact]
    public void M26_06_StealthTimer_ZeroOnTheShotTick_EvenWithAShortenedReload()
    {
        var s = new Simulation(Setup(false));
        EntityId stealth = s.SpawnVehicle(0, Stealth, new CellCoord(32, 10));
        s.SpawnVehicle(0, Booster, new CellCoord(32, 12));          // halves the reload: 5 ticks, below the weapon's base rate of 10
        EntityId dummy = s.SpawnVehicle(1, Dummy, new CellCoord(34, 10));
        s.Step(new[] { GuardCmd(s, stealth, new CellCoord(32, 10)) });
        int shots = 0;
        for (int n = 0; n < 60; n++)
        {
            int before = Hp(s, dummy);
            Run(s, 1);
            if (Hp(s, dummy) >= before) continue;
            shots++;
            Assert.Equal(0, s.Effects.VehicleStill[stealth.Index]);
        }
        Assert.True(shots >= 2);
    }
}
