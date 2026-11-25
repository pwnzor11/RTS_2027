// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.S44;

/// <summary>S44 M-01: sea mines. Open sea, x &lt; 30 water. Types: "layer" (minelayer: 3 mines, sweep 3), "boat" (plain ship, 100 hp),
/// "sub" (submerges), "tank" (ground).</summary>
public sealed class MinesTests
{
    private const int Layer = 0, Boat = 1, Sub = 2, Tank = 3, W = 40, H = 30;
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

    private static SimSetup Setup(ulong seed = 7, int players = 3)
    {
        var cells = new LandType[W * H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) cells[y * W + x] = x >= 30 ? LandType.Clear : LandType.Water;
        var map = new MapData("sea", "rust_belt", W, H, new ushort[W * H], cells, new byte[W * H], new OverlayKind[W * H], new byte[W * H],
            new[] { new CellCoord(1, 1), new CellCoord(W - 2, H - 2) });
        VehicleType Ship(string id, int hp) =>
            ScriptedGame.Vehicle(id, 40, hp) with { Locomotion = Locomotion.Naval, TurnRate = 16, PrimaryWeapon = -1, Armor = ArmorClass.ShipLight };
        VehicleType[] vehicles =
        {
            Ship("layer", 200) with
            {
                MineCapacity = 3, MineLayTicks = 10, MineDamage = 60, MineWarhead = "mine", MineSweepRadius = 3, MineSweepTicks = 5,
                MineRestockTicks = 20,
            },
            Ship("boat", 100),
            Ship("sub", 100) with { Submerges = true },
            ScriptedGame.Vehicle("tank", 40, 100),
        };
        var warheads = new[] { new WarheadType("mine", new[] { 0, 0, 0, 0, 0, 100, 100, 0 }, 0, false) };
        SimRules rules = ScriptedGame.RulesWith(vehicles, Array.Empty<InfantryType>()) with { Warheads = warheads, Terrain = Terrain() };
        // players 0 and 1 are on one team, 2 is the enemy (teams: A A B)
        return new SimSetup(seed, map, players, rules, 0xABCDUL) { AllowDebug = true, Teams = new byte[] { 0, 0, 1 } };
    }

    private Command Lay(Simulation s, EntityId who, CellCoord? at = null, byte player = 0) =>
        new(s.Tick, player, ++_seq, CommandKind.Deploy, who, default, at ?? default, (int)ActorKind.Vehicle, at.HasValue ? 1 : 0);

    private static void Run(Simulation s, int n) { for (int i = 0; i < n; i++) s.Step(Array.Empty<Command>()); }
    private static int Hp(Simulation s, EntityId e) => s.Units.Health[e.Index];

    [Fact]
    public void Deploy_LaysAMineOnTheOwnCell_AndCountsDownTheStock()
    {
        var s = new Simulation(Setup());
        EntityId l = s.SpawnVehicle(0, Layer, new CellCoord(10, 10));
        Assert.Equal(3, s.MineStockOf(l));
        s.Step(new[] { Lay(s, l) });
        Assert.Equal(1, s.Mines.Count);
        Assert.NotEqual(0, s.FindMineAt(new CellCoord(10, 10)));
        Assert.Equal(2, s.MineStockOf(l));
        Assert.Equal(0, s.Units.Health[l.Index] - 200);
    }

    [Fact]
    public void LayOrder_SailsToTheCellAndLaysThere_AndLandCellsAreRefused()
    {
        var s = new Simulation(Setup());
        EntityId l = s.SpawnVehicle(0, Layer, new CellCoord(10, 10));
        s.Step(new[] { Lay(s, l, new CellCoord(35, 10)) });   // land
        Run(s, 5);
        Assert.Equal(0, s.Mines.Count);
        s.Step(new[] { Lay(s, l, new CellCoord(14, 10)) });
        Assert.Equal(0, s.Mines.Count);                       // not there yet
        Run(s, 400);
        Assert.Equal(1, s.Mines.Count);
        Assert.NotEqual(0, s.FindMineAt(new CellCoord(14, 10)));
    }

    [Fact]
    public void Capacity_LimitsTheMinesAndRestockNeedsABuilding()
    {
        var s = new Simulation(Setup());
        EntityId l = s.SpawnVehicle(0, Layer, new CellCoord(10, 10));
        for (int n = 0; n < 5; n++)
        {
            s.Step(new[] { Lay(s, l, new CellCoord(10 + n, 10)) });
            Run(s, 60);
        }
        Assert.Equal(3, s.Mines.Count);
        Assert.Equal(0, s.MineStockOf(l));
        Run(s, 200);
        Assert.Equal(0, s.MineStockOf(l));   // no naval building near: no restock
    }

    private Command MoveTo(Simulation s, EntityId who, CellCoord to, byte player) =>
        new(s.Tick, player, ++_seq, CommandKind.Move, who, default, to, (int)ActorKind.Vehicle);

    [Fact]
    public void AnEnemyShip_TriggersTheMine_OnceAndTheMineIsGone()
    {
        var s = new Simulation(Setup());
        EntityId l = s.SpawnVehicle(0, Layer, new CellCoord(10, 10));
        EntityId foe = s.SpawnVehicle(2, Boat, new CellCoord(16, 10));
        s.Step(new[] { Lay(s, l) });
        s.Step(new[] { MoveTo(s, l, new CellCoord(10, 16), 0) });
        Run(s, 60);
        Assert.Equal(1, s.Mines.Count);
        s.Step(new[] { MoveTo(s, foe, new CellCoord(10, 10), 2) });
        Run(s, 200);
        Assert.Equal(0, s.Mines.Count);
        Assert.Equal(40, Hp(s, foe));
        Run(s, 50);
        Assert.Equal(40, Hp(s, foe));        // one hit only
    }

    [Fact]
    public void OwnAndAlliedShips_NeverTriggerIt_AndDivedSubsPass()
    {
        var s = new Simulation(Setup());
        EntityId l = s.SpawnVehicle(0, Layer, new CellCoord(10, 10));
        EntityId ally = s.SpawnVehicle(1, Boat, new CellCoord(14, 10));   // player 1 shares team 0
        EntityId sub = s.SpawnVehicle(2, Sub, new CellCoord(6, 10));
        s.Step(new[] { Lay(s, l), new Command(s.Tick, 2, ++_seq, CommandKind.Deploy, sub, default, default, (int)ActorKind.Vehicle) });   // lay; dive
        Assert.True(s.Naval.Submerged[sub.Index]);
        s.Step(new[] { MoveTo(s, l, new CellCoord(10, 16), 0) });
        Run(s, 60);
        s.Step(new[] { MoveTo(s, ally, new CellCoord(10, 10), 1) });
        Run(s, 150);
        Assert.Equal(1, s.Mines.Count);
        Assert.Equal(100, Hp(s, ally));
        Assert.Equal(200, Hp(s, l));
        s.Step(new[] { MoveTo(s, ally, new CellCoord(14, 14), 1) });
        Run(s, 100);
        s.Step(new[] { MoveTo(s, sub, new CellCoord(10, 10), 2) });
        Run(s, 200);
        Assert.Equal(1, s.Mines.Count);
        Assert.Equal(100, Hp(s, sub));
    }

    [Fact]
    public void ASweeper_ClearsEnemyMinesInRadius_ButNotFriendlyOnes()
    {
        var s = new Simulation(Setup());
        EntityId enemy = s.SpawnVehicle(2, Layer, new CellCoord(20, 20));
        EntityId friend = s.SpawnVehicle(1, Layer, new CellCoord(12, 20));
        EntityId sweeper = s.SpawnVehicle(0, Layer, new CellCoord(14, 20));   // radius 3: reaches the friend's mine, not the enemy's
        s.Step(new[] { Lay(s, enemy, null, 2), Lay(s, friend, null, 1) });
        Assert.Equal(2, s.Mines.Count);
        Run(s, 12);
        Assert.Equal(2, s.Mines.Count);
        s.Step(new[] { MoveTo(s, sweeper, new CellCoord(18, 20), 0) });
        Run(s, 300);
        Assert.Equal(1, s.Mines.Count);
        Assert.NotEqual(0, s.FindMineAt(new CellCoord(12, 20)));
        Assert.Equal(0, s.FindMineAt(new CellCoord(20, 20)));
    }

    [Fact]
    public void EnemyMines_AreHiddenUnlessSonarCoversThem()
    {
        var s = new Simulation(Setup());
        EntityId l = s.SpawnVehicle(0, Layer, new CellCoord(10, 10));
        s.Step(new[] { Lay(s, l) });
        int slot = s.FindMineAt(new CellCoord(10, 10));
        Assert.True(s.MineVisibleTo(0, slot));
        Assert.True(s.MineVisibleTo(1, slot));    // ally
        Assert.False(s.MineVisibleTo(2, slot));   // enemy, no sonar
    }

    [Fact]
    public void SaveLoad_RoundTripsTheMinesAndTheStock()
    {
        var setup = Setup();
        var s = new Simulation(setup);
        EntityId l = s.SpawnVehicle(0, Layer, new CellCoord(10, 10));
        s.Step(new[] { Lay(s, l) });
        s.Step(new[] { Lay(s, l, new CellCoord(14, 10)) });
        Run(s, 5);
        var ms = new MemoryStream();
        s.Save(ms);
        ms.Position = 0;
        Simulation loaded = Simulation.Load(ms, setup);
        Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(s.Mines.Count, loaded.Mines.Count);
        Assert.Equal(s.MineStockOf(l), loaded.MineStockOf(l));
        Run(s, 300); Run(loaded, 300);
        Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(2, loaded.Mines.Count);
    }

    [Fact]
    public void TheMinesPart_IsHashed()
    {
        var a = new Simulation(Setup());
        var b = new Simulation(Setup());
        EntityId la = a.SpawnVehicle(0, Layer, new CellCoord(10, 10)), lb = b.SpawnVehicle(0, Layer, new CellCoord(10, 10));
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        a.Step(new[] { Lay(a, la) });
        b.Step(Array.Empty<Command>());
        Assert.NotEqual(a.ComputeStateHash(), b.ComputeStateHash());
        Assert.Equal("mines", a.StateParts[^2].Name);   // S44 M-15 appended "beacon_orders" after it
        _ = lb;
    }

    [Fact]
    public void SameInputs_GiveTheSameHash()
    {
        ulong Play()
        {
            var s = new Simulation(Setup(11));
            EntityId l = s.SpawnVehicle(0, Layer, new CellCoord(10, 10)), e = s.SpawnVehicle(2, Boat, new CellCoord(16, 10));
            s.Step(new[] { Lay(s, l, new CellCoord(13, 10)) });
            Run(s, 100);
            s.Step(new[] { new Command(s.Tick, 2, ++_seq, CommandKind.Move, e, default, new CellCoord(8, 10), (int)ActorKind.Vehicle) });
            Run(s, 300);
            return s.ComputeStateHash();
        }
        _seq = 0; ulong h1 = Play(); _seq = 0; ulong h2 = Play();
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void ShippedRules_MinelayerHasTheMineFields_AndOtherUnitsHaveNone()
    {
        RuleSet rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!);
        VehicleType ml = rules.Sim.Vehicles.Single(v => v.Id == "exp_minelayer");
        Assert.True(ml.MineCapacity > 0 && ml.MineDamage > 0 && ml.MineLayTicks > 0 && ml.MineSweepRadius > 0);
        Assert.Contains(rules.Sim.Warheads, w => w.Id == ml.MineWarhead);
        Assert.All(rules.Sim.Vehicles.Where(v => v.Id != "exp_minelayer"), v => Assert.Equal(0, v.MineCapacity));
    }
}
