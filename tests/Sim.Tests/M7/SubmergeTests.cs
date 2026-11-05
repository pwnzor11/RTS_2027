// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.View;

namespace Rts.Sim.Tests.M7;

/// <summary>M7-05: submerge and sonar (docs/design/11_m7_plan.md §2.5, 07_expedition.md §6.4). Types are built in code on an open sea:
/// "sub" (submerges), "hunter" (torpedo: naval_submerged), "gunboat" (shell: naval), "picket" (sonar 8, unarmed), "tank" (ground gun).</summary>
public sealed class SubmergeTests
{
    private const int Sub = 0, Hunter = 1, Gunboat = 2, Picket = 3, Tank = 4, W = 40, H = 30;
    private const int Torpedo = 0, Shell = 1, Gun = 2;
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

    private static SimSetup Setup(ulong seed = 7)
    {
        var cells = new LandType[W * H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) cells[y * W + x] = x >= 30 ? LandType.Clear : LandType.Water;
        var map = new MapData("sea", "rust_belt", W, H, new ushort[W * H], cells, new byte[W * H], new OverlayKind[W * H], new byte[W * H],
            new[] { new CellCoord(1, 1), new CellCoord(W - 2, H - 2) });
        VehicleType Ship(string id, int hp, int weapon) =>
            ScriptedGame.Vehicle(id, 40, hp) with { Locomotion = Locomotion.Naval, TurnRate = 16, PrimaryWeapon = weapon };
        VehicleType[] vehicles =
        {
            Ship("sub", 100, Torpedo) with { Submerges = true },
            Ship("hunter", 100, Torpedo),
            Ship("gunboat", 100, Shell),
            Ship("picket", 100, -1) with { SonarRadius = 8 },
            ScriptedGame.Vehicle("tank", 40, 100) with { PrimaryWeapon = Gun },
        };
        WeaponType Weapon(string id, WeaponTargets t) => new(id, 10, 6 * 256, 0, 10, 0, 0) { Targets = t };
        var weapons = new[] { Weapon("torpedo", WeaponTargets.NavalAndSubmerged), Weapon("shell", WeaponTargets.Naval), Weapon("gun", WeaponTargets.Ground) };
        var warheads = new[] { new WarheadType("w", new[] { 100, 100, 100, 100, 100, 100, 100, 100 }, 0, false) };
        SimRules rules = ScriptedGame.RulesWith(vehicles, Array.Empty<InfantryType>()) with
        {
            Weapons = weapons, Warheads = warheads, Terrain = Terrain(),
        };
        return new SimSetup(seed, map, 2, rules, 0xABCDUL) { AllowDebug = true };
    }

    private Command Cmd(Simulation s, CommandKind k, EntityId who, EntityId target = default, byte player = 0, int arg = (int)ActorKind.Vehicle) =>
        new(s.Tick, player, ++_seq, k, who, target, default, arg);

    private Command Dive(Simulation s, EntityId who, byte player = 0) => Cmd(s, CommandKind.Deploy, who, player: player);

    private static void Run(Simulation s, int n) { for (int i = 0; i < n; i++) s.Step(Array.Empty<Command>()); }

    private static bool Down(Simulation s, EntityId e) => s.Naval.Submerged[e.Index];

    private static int Hp(Simulation s, EntityId e) => s.Units.Health[e.Index];

    private static bool Listed(Simulation s, byte viewer, EntityId e)
    {
        var l = new List<ViewActor>();
        s.ViewFor(viewer).CopyVisible(l);
        return l.Exists(a => a.Id == e);
    }

    /// <summary>Sub of player 0 at (10,10), already dived, with an enemy hunter 4 cells away.</summary>
    private (Simulation S, EntityId Sub, EntityId Hunter) Duel(int enemyType = Hunter, params CellCoord[] pickets)
    {
        var s = new Simulation(Setup());
        EntityId sub = s.SpawnVehicle(0, Sub, new CellCoord(10, 10));
        EntityId foe = s.SpawnVehicle(1, enemyType, new CellCoord(14, 10));
        foreach (CellCoord c in pickets) s.SpawnVehicle(1, Picket, c);
        var guard = new Command(s.Tick, 1, ++_seq, CommandKind.Guard, foe, default, new CellCoord(14, 10), (int)ActorKind.Vehicle);
        s.Step(new[] { Dive(s, sub), guard });
        return (s, sub, foe);
    }

    [Fact]
    public void Deploy_TogglesDiveAndSurface_OnlyForOwnSubmersibles()
    {
        var s = new Simulation(Setup());
        EntityId sub = s.SpawnVehicle(0, Sub, new CellCoord(10, 10)), boat = s.SpawnVehicle(0, Gunboat, new CellCoord(10, 14));
        s.Step(new[] { Dive(s, sub, 1) });          // not the owner
        Assert.False(Down(s, sub));
        s.Step(new[] { Dive(s, boat) });            // cannot submerge
        Assert.False(Down(s, boat));
        s.Step(new[] { Dive(s, sub) });
        Assert.True(Down(s, sub));
        Run(s, 3);
        Assert.True(Down(s, sub));
        s.Step(new[] { Dive(s, sub) });
        Assert.False(Down(s, sub));
    }

    [Fact]
    public void Submerged_IsUntargetable_ByWeaponsAndOrders()
    {
        var (s, sub, hunter) = Duel();
        Assert.True(Down(s, sub));
        s.Step(new[] { Cmd(s, CommandKind.Attack, hunter, sub, 1, (int)ActorKind.Vehicle | ((int)ActorKind.Vehicle << 8)) });
        Run(s, 60);
        Assert.Equal(100, Hp(s, sub));
        Assert.NotEqual(MissionKind.Attack, s.Units.Mission[hunter.Index]);
        s.Step(new[] { Dive(s, sub) });             // surface: now the hunter finds it by itself
        Assert.False(Down(s, sub));
        Run(s, 60);
        Assert.True(Hp(s, sub) < 100);
    }

    [Fact]
    public void Sonar_RevealsSubmerged_AndAllowsTargeting()
    {
        var (s, sub, _) = Duel(Hunter, new CellCoord(14, 14));   // picket 4 cells off the sub, inside radius 8
        Assert.False(s.IsHiddenFrom(1, ActorKind.Vehicle, sub.Index));
        Assert.False(s.IsUntargetableBy(1, ActorKind.Vehicle, sub.Index));
        Run(s, 60);
        Assert.True(Down(s, sub));
        Assert.True(Hp(s, sub) < 100, "the hunter shoots a sonar-revealed submarine");
    }

    [Fact]
    public void Sonar_OutOfRange_DoesNotReveal()
    {
        var (s, sub, _) = Duel(Hunter, new CellCoord(14, 25));   // 15 cells away: outside the radius
        Assert.True(s.IsUntargetableBy(1, ActorKind.Vehicle, sub.Index));
        Run(s, 60);
        Assert.Equal(100, Hp(s, sub));
    }

    [Fact]
    public void RevealedSubmarine_IsStillImmuneToNavalOnlyWeapons()
    {
        var (s, sub, gun) = Duel(Gunboat, new CellCoord(14, 14));
        Assert.False(s.IsUntargetableBy(1, ActorKind.Vehicle, sub.Index));
        Run(s, 60);
        Assert.Equal(100, Hp(s, sub));
        Assert.Equal(100, Hp(s, gun));
    }

    [Fact]
    public void WeaponTargets_FollowTheNavalTable()
    {
        var s = new Simulation(Setup());
        EntityId sub = s.SpawnVehicle(0, Sub, new CellCoord(10, 10)), ship = s.SpawnVehicle(0, Gunboat, new CellCoord(10, 12));
        EntityId tank = s.SpawnVehicle(0, Tank, new CellCoord(32, 10));
        s.Step(new[] { Dive(s, sub) });
        WeaponType W(WeaponTargets t) => new("x", 1, 256, 0, 1, 0, 0) { Targets = t };
        bool Can(WeaponTargets t, EntityId e) => s.WeaponCanTarget(W(t), ActorKind.Vehicle, e.Index);
        Assert.Equal(new[] { false, true, true }, new[] { Can(WeaponTargets.Ground, sub), Can(WeaponTargets.Ground, ship), Can(WeaponTargets.Ground, tank) });
        Assert.Equal(new[] { false, true, false }, new[] { Can(WeaponTargets.Naval, sub), Can(WeaponTargets.Naval, ship), Can(WeaponTargets.Naval, tank) });
        Assert.Equal(new[] { true, false, false }, new[] { Can(WeaponTargets.Submerged, sub), Can(WeaponTargets.Submerged, ship), Can(WeaponTargets.Submerged, tank) });
        Assert.Equal(new[] { true, true, false }, new[] { Can(WeaponTargets.NavalAndSubmerged, sub), Can(WeaponTargets.NavalAndSubmerged, ship), Can(WeaponTargets.NavalAndSubmerged, tank) });
        Assert.Equal(new[] { false, true, true }, new[] { Can(WeaponTargets.Both, sub), Can(WeaponTargets.Both, ship), Can(WeaponTargets.Both, tank) });
        Assert.False(Can(WeaponTargets.Air, sub));
    }

    [Fact]
    public void View_HidesEnemySubmarine_UntilSonarSeesIt()
    {
        var (s, sub, _) = Duel();
        var own = new List<ViewActor>();
        s.ViewFor(0).CopyOwn(own);
        Assert.Contains(own, a => a.Id == sub && a.Submerged);   // own: always
        Assert.False(Listed(s, 1, sub), "undetected submarine must not appear");
        Assert.DoesNotContain(s.Snapshot(1).Units, u => u.Id == sub);
        var (s2, sub2, _) = Duel(Hunter, new CellCoord(14, 14));
        s = s2; sub = sub2;
        Assert.True(Listed(s, 1, sub));
        var l = new List<ViewActor>();
        s.ViewFor(1).CopyVisible(l);
        ViewActor seen = l.Find(a => a.Id == sub);
        Assert.True(seen.Submerged && seen.Revealed);
        Assert.Contains(s.Snapshot(1).Units, u => u.Id == sub);
    }

    [Fact]
    public void SaveLoad_ReproducesDiveFlagAndHash()
    {
        var (s, sub, _) = Duel(Hunter, new CellCoord(14, 14));
        Run(s, 5);
        ulong before = s.ComputeStateHash();
        var ms = new MemoryStream();
        s.Save(ms);
        Simulation loaded = Simulation.Load(new MemoryStream(ms.ToArray()), Setup());
        Assert.True(Down(loaded, sub));
        Assert.Equal(before, loaded.ComputeStateHash());
        s.Step(new[] { Dive(s, sub) }); loaded.Step(new[] { Dive(loaded, sub) });
        Assert.False(Down(loaded, sub));
        Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
        Run(s, 20); Run(loaded, 20);
        Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
    }

    [Fact]
    public void DiveFlag_IsPartOfTheStateHash_AndClearsWhenTheShipDies()
    {
        var s = new Simulation(Setup());
        EntityId sub = s.SpawnVehicle(0, Sub, new CellCoord(10, 10));
        ulong up = s.ComputeStateHash();
        s.Naval.Submerged[sub.Index] = true;
        Assert.NotEqual(up, s.ComputeStateHash());
        s.Naval.Submerged[sub.Index] = false;
        Assert.Equal(up, s.ComputeStateHash());
    }

    [Fact]
    public void Replay_SameCommands_SameHashEveryTick()
    {
        ulong[] Play()
        {
            var s = new Simulation(Setup(11));
            _seq = 0;
            EntityId sub = s.SpawnVehicle(0, Sub, new CellCoord(10, 10));
            s.SpawnVehicle(1, Hunter, new CellCoord(14, 10));
            s.SpawnVehicle(1, Picket, new CellCoord(14, 14));
            var hashes = new List<ulong>();
            for (int t = 0; t < 120; t++)
            {
                s.Step(t is 3 or 40 ? new[] { Dive(s, sub) } : Array.Empty<Command>());
                hashes.Add(s.ComputeStateHash());
            }
            return hashes.ToArray();
        }
        Assert.Equal(Play(), Play());
    }
}
