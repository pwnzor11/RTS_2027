// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.S44;

/// <summary>S44 M-17: naval gunfire at a ground cell. Open sea, land from x = 30. "cruiser" (needs_spotter gun, range 12 cells, sight 3),
/// "spotter" (land scout, sight 5, unarmed), "howitzer" (land gun, no spotter rule), "dummy" (sturdy enemy target).</summary>
public sealed class NavalGunfireTests
{
    private const int Cruiser = 0, Spotter = 1, Howitzer = 2, Dummy = 3, W = 40, H = 30;
    private const int NavalGun = 0, Howit = 1;
    private static readonly CellCoord Target = new(34, 10);
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
        VehicleType[] vehicles =
        {
            ScriptedGame.Vehicle("cruiser", 40, 1000) with { Locomotion = Locomotion.Naval, TurnRate = 16, PrimaryWeapon = NavalGun, Sight = 3, HasTurret = false },
            ScriptedGame.Vehicle("spotter", 40, 1000) with { PrimaryWeapon = -1, Sight = 5 },
            ScriptedGame.Vehicle("howitzer", 40, 1000) with { PrimaryWeapon = Howit, Sight = 3 },
            ScriptedGame.Vehicle("dummy", 40, 100000) with { PrimaryWeapon = -1, Sight = 1 },
        };
        var weapons = new[]
        {
            new WeaponType("naval_gun", 40, 12 * 256, 0, 10, 0, 0) { NeedsSpotter = true },
            new WeaponType("howit", 40, 12 * 256, 0, 10, 0, 0),
        };
        var warheads = new[] { new WarheadType("w", new[] { 100, 100, 100, 100, 100, 100, 100, 100 }, 1, false) };
        SimRules rules = ScriptedGame.RulesWith(vehicles, Array.Empty<InfantryType>()) with { Weapons = weapons, Warheads = warheads, Terrain = Terrain() };
        return new SimSetup(7, map, 2, rules, 0xABCDUL) { AllowDebug = true };
    }

    private Command Ground(Simulation s, EntityId who, CellCoord at) =>
        new(s.Tick, 0, ++_seq, CommandKind.AttackGround, who, default, at, (int)ActorKind.Vehicle);

    private static void Run(Simulation s, int n) { for (int i = 0; i < n; i++) s.Step(Array.Empty<Command>()); }

    private static int Hp(Simulation s, EntityId e) => s.Units.Health[e.Index];

    /// <summary>Cruiser (player 0) at (24,10), 10 cells from the target; an enemy dummy stands on the target cell.</summary>
    private (Simulation S, EntityId Ship, EntityId Dummy, EntityId Spotter) Scene(bool spotter = false)
    {
        var s = new Simulation(Setup());
        EntityId ship = s.SpawnVehicle(0, Cruiser, new CellCoord(24, 10));
        EntityId dummy = s.SpawnVehicle(1, Dummy, Target);
        EntityId spot = spotter ? s.SpawnVehicle(0, Spotter, new CellCoord(33, 10)) : default;
        Run(s, 2);
        return (s, ship, dummy, spot);
    }

    [Fact]
    public void UnseenCell_RefusesTheOrder_NothingIsHit()
    {
        var (s, ship, dummy, _) = Scene();
        Assert.False(s.IsVisibleTo(0, Target));
        s.Step(new[] { Ground(s, ship, Target) });
        Assert.NotEqual(MissionKind.AttackGround, s.Units.Mission[ship.Index]);
        Run(s, 60);
        Assert.Equal(100000, Hp(s, dummy));
    }

    [Fact]
    public void SpottedCell_ShellsTheCell_FromBeyondTheShipsOwnSight()
    {
        var (s, ship, dummy, _) = Scene(spotter: true);
        Assert.True(s.IsVisibleTo(0, Target));
        s.Step(new[] { Ground(s, ship, Target) });
        Assert.Equal(MissionKind.AttackGround, s.Units.Mission[ship.Index]);
        Run(s, 60);
        Assert.True(Hp(s, dummy) < 100000, "the shells land on the designated cell");
        Assert.Equal(new CellCoord(24, 10), s.Units.Position[ship.Index].ToCell());   // in range: the ship did not sail
    }

    [Fact]
    public void LosingTheSpotter_LapsesTheDesignation()
    {
        var (s, ship, dummy, spot) = Scene(spotter: true);
        s.Step(new[] { Ground(s, ship, Target) });
        Run(s, 20);
        Assert.Equal(MissionKind.AttackGround, s.Units.Mission[ship.Index]);
        s.Units.Health[spot.Index] = 0;
        Run(s, 5);
        Assert.NotEqual(MissionKind.AttackGround, s.Units.Mission[ship.Index]);
        int hp = Hp(s, dummy);
        Run(s, 60);
        Assert.Equal(hp, Hp(s, dummy));
    }

    [Fact]
    public void WeaponWithoutTheSpotterRule_StillFiresAtAnUnseenCell()
    {
        var s = new Simulation(Setup());
        EntityId gun = s.SpawnVehicle(0, Howitzer, new CellCoord(31, 10));
        EntityId dummy = s.SpawnVehicle(1, Dummy, new CellCoord(38, 10));
        Run(s, 2);
        Assert.False(s.IsVisibleTo(0, new CellCoord(38, 10)));
        s.Step(new[] { Ground(s, gun, new CellCoord(38, 10)) });
        Assert.Equal(MissionKind.AttackGround, s.Units.Mission[gun.Index]);
        Run(s, 60);
        Assert.True(Hp(s, dummy) < 100000);
    }

    [Fact]
    public void ShippedRules_DestroyerAndCruiserGunsNeedASpotter_AndNoOtherWeaponDoes()
    {
        RuleSet rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!);
        var spotting = rules.Sim.Weapons.Where(w => w.NeedsSpotter).Select(w => w.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "exp_cruiser_gun", "exp_destroyer_gun" }, spotting);
        string gun(string v) => rules.Sim.Weapons[rules.Sim.Vehicles.Single(x => x.Id == v).PrimaryWeapon].Id;
        Assert.Equal("exp_cruiser_gun", gun("exp_cruiser"));
        Assert.Equal("exp_destroyer_gun", gun("exp_destroyer"));
    }

    [Fact]
    public void SaveRoundTrip_KeepsTheMissionAndHash()
    {
        var (s, ship, _, _) = Scene(spotter: true);
        s.Step(new[] { Ground(s, ship, Target) });
        Run(s, 15);
        var ms = new MemoryStream();
        s.Save(ms);
        ms.Position = 0;
        Simulation loaded = Simulation.Load(ms, Setup());
        Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
        Run(s, 40); Run(loaded, 40);
        Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
    }
}
