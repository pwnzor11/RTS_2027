// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.M7;

/// <summary>M7-04b: vehicle passengers (vehicle_slots). Map as in LandingTests: deep sea x 0..7, shoals x 8..9, beach (Coast, a landing
/// cell) x = 10, grass x &gt;= 11, cliff at (10,5). Types: "craft" (naval, 8 seats, 2 vehicle slots), "bare" (naval, 8 seats, no vehicle
/// slots), "liner" (naval), "tank" (tracked).</summary>
public sealed class VehiclePassengerTests
{
    private const int Craft = 0, Bare = 1, Liner = 2, Tank = 3, Marine = 0, W = 30, H = 30, PerPassenger = 5;
    private static readonly int VehicleArg = (int)ActorKind.Vehicle | ((int)ActorKind.Vehicle << 8);
    private ushort _seq;

    private static TerrainTable Terrain()
    {
        var speed = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        foreach (LandType land in Enum.GetValues<LandType>())
            for (int l = 0; l < RuleCounts.Locomotions; l++)
            {
                var loco = (Locomotion)l;
                bool water = land is LandType.Water or LandType.Shallow;
                int v = loco == Locomotion.Air ? 100
                    : loco == Locomotion.Naval ? (water ? 100 : 0)
                    : water || land is LandType.Rock or LandType.Cliff ? 0 : 100;
                speed[(int)land * RuleCounts.Locomotions + l] = v;
            }
        var buildable = new bool[RuleCounts.LandTypes];
        buildable[(int)LandType.Clear] = true;
        var landing = new bool[RuleCounts.LandTypes];
        landing[(int)LandType.Coast] = true;
        return new TerrainTable(speed, buildable, null, landing);
    }

    private static LandType Land(int x, int y)
    {
        if (x == 10 && y == 5) return LandType.Cliff;
        return x <= 7 ? LandType.Water : x <= 9 ? LandType.Shallow : x == 10 ? LandType.Coast : LandType.Clear;
    }

    private static SimSetup Setup()
    {
        var cells = new LandType[W * H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) cells[y * W + x] = Land(x, y);
        var map = new MapData("coast", "rust_belt", W, H, new ushort[W * H], cells, new byte[W * H], new OverlayKind[W * H], new byte[W * H],
            new[] { new CellCoord(1, 1), new CellCoord(W - 2, H - 2) });
        VehicleType[] vehicles =
        {
            ScriptedGame.Vehicle("craft", 40, 100) with { Locomotion = Locomotion.Naval, TurnRate = 16, Passengers = 8, UnloadTicks = PerPassenger, VehicleSlots = 2 },
            ScriptedGame.Vehicle("bare", 40, 100) with { Locomotion = Locomotion.Naval, TurnRate = 16, Passengers = 8, UnloadTicks = PerPassenger },
            ScriptedGame.Vehicle("liner", 30, 300) with { Locomotion = Locomotion.Naval, TurnRate = 8 },
            ScriptedGame.Vehicle("tank", 40, 200),
        };
        SimRules rules = ScriptedGame.RulesWith(vehicles, new[] { ScriptedGame.Infantry("marine", 9, 50) }) with { Terrain = Terrain() };
        return new SimSetup(11, map, 2, rules, 0xABCDUL) { AllowDebug = true };
    }

    private Command Cmd(Simulation s, CommandKind k, EntityId who, EntityId target, CellCoord cell, int arg) =>
        new(s.Tick, 0, ++_seq, k, who, target, cell, arg);

    private static void Run(Simulation s, int n) { for (int i = 0; i < n; i++) s.Step(Array.Empty<Command>()); }

    private void Board(Simulation s, EntityId tank, EntityId craft) => s.Step(new[] { Cmd(s, CommandKind.Enter, tank, craft, default, VehicleArg) });

    private Command Unload(Simulation s, EntityId craft, int x, int y) =>
        Cmd(s, CommandKind.Unload, craft, default, new CellCoord(x, y), (int)ActorKind.Vehicle);

    private static bool Aboard(Simulation s, EntityId v) => s.Tunnels.VehicleLayer[v.Index] == ActorLayer.Passenger;

    /// <summary>A craft at (9,10) and three tanks on the beach at (10,9..11).</summary>
    private (Simulation Sim, EntityId Craft, EntityId[] Tanks) Beach(int craftType = Craft)
    {
        var s = new Simulation(Setup());
        EntityId c = s.SpawnVehicle(0, craftType, new CellCoord(9, 10));
        var tanks = new EntityId[3];
        for (int k = 0; k < 3; k++) tanks[k] = s.SpawnVehicle(0, Tank, new CellCoord(10, 9 + k));
        return (s, c, tanks);
    }

    [Fact]
    public void TwoVehiclesLoad_TheThirdIsRefused()
    {
        var (s, craft, tanks) = Beach();
        Board(s, tanks[0], craft); Board(s, tanks[1], craft); Board(s, tanks[2], craft);
        Assert.True(Aboard(s, tanks[0])); Assert.True(Aboard(s, tanks[1]));
        Assert.False(Aboard(s, tanks[2]));
        Assert.Equal(2, s.PassengersIn(craft));
        Assert.Equal(ActorLayer.Surface, s.Tunnels.VehicleLayer[tanks[2].Index]);
        Run(s, 5);
        Assert.Equal(s.Units.Position[craft.Index], s.Units.Position[tanks[0].Index]);   // rides along
        Assert.True(s.Map.VehicleAt(new CellCoord(10, 9)).IsNone);                       // holds no cell
    }

    [Fact]
    public void ShipsAndTransportsWithoutSlots_RefuseVehicles()
    {
        var (s, bare, tanks) = Beach(Bare);
        Board(s, tanks[0], bare);
        Assert.False(Aboard(s, tanks[0]));
        var (s2, craft, _) = Beach();
        EntityId liner = s2.SpawnVehicle(0, Liner, new CellCoord(8, 12));
        Board(s2, liner, craft);
        Assert.False(Aboard(s2, liner));
    }

    [Fact]
    public void Vehicle_DrivesToTheCraft_ThenBoards()
    {
        var (s, craft, _) = Beach();
        EntityId far = s.SpawnVehicle(0, Tank, new CellCoord(14, 10));
        Board(s, far, craft);
        for (int t = 0; t < 200 && !Aboard(s, far); t++) Run(s, 1);
        Assert.True(Aboard(s, far));
    }

    [Fact]
    public void Vehicles_LandOnTheCoast_OnePerInterval()
    {
        var (s, craft, tanks) = Beach();
        Board(s, tanks[0], craft); Board(s, tanks[1], craft);
        s.Step(new[] { Unload(s, craft, 10, 10) });
        var seen = new List<int>();
        for (int t = 0; t < 100 && s.PassengersIn(craft) > 0; t++) { Run(s, 1); if (seen.Count == 0 || seen[^1] != s.PassengersIn(craft)) seen.Add(s.PassengersIn(craft)); }
        Assert.Equal(new[] { 2, 1, 0 }, seen);
        foreach (EntityId t in tanks.Take(2))
        {
            Assert.Equal(ActorLayer.Surface, s.Tunnels.VehicleLayer[t.Index]);
            CellCoord c = s.Units.Position[t.Index].ToCell();
            Assert.Equal(LandType.Coast, s.Map.LandAt(c));
            Assert.Equal(t, s.Map.VehicleAt(c));
        }
        Assert.NotEqual(s.Units.Position[tanks[0].Index].ToCell(), s.Units.Position[tanks[1].Index].ToCell());
    }

    [Theory]
    [InlineData(10, 5)]   // cliff
    [InlineData(5, 10)]   // deep water
    [InlineData(12, 10)]  // grass: no sea cell beside it, and not a landing cell
    public void Unload_OfVehicles_IsRefusedOffTheBeach(int x, int y)
    {
        var (s, craft, tanks) = Beach();
        Board(s, tanks[0], craft);
        s.Step(new[] { Unload(s, craft, x, y) });
        Run(s, 80);
        Assert.True(Aboard(s, tanks[0]));
        Assert.NotEqual(MissionKind.Unload, s.Units.Mission[craft.Index]);
    }

    [Fact]
    public void Vehicle_StaysAboard_WhenNoCoastCellIsFree()
    {
        var (s, craft, tanks) = Beach();   // tanks at (10,9), (10,10), (10,11)
        EntityId filler = s.SpawnVehicle(0, Tank, new CellCoord(11, 9));
        foreach (int y in new[] { 8, 12 }) s.SpawnVehicle(0, Tank, new CellCoord(10, y));
        Board(s, tanks[0], craft);
        s.Step(new[] { Cmd(s, CommandKind.Move, filler, default, new CellCoord(10, 9), (int)ActorKind.Vehicle) });   // closes the gap: the whole beach is taken
        Run(s, 30);
        Assert.Equal(new CellCoord(10, 9), s.Units.Position[filler.Index].ToCell());
        s.Step(new[] { Unload(s, craft, 10, 10) });
        Run(s, 80);
        Assert.True(Aboard(s, tanks[0]));   // grass behind the beach is free, but a ship's cargo lands only on `landing` cells
        Assert.NotEqual(MissionKind.Unload, s.Units.Mission[craft.Index]);
    }

    [Fact]
    public void MixedLoad_SoldiersLandFirst_ThenVehicles()
    {
        var (s, craft, tanks) = Beach();
        EntityId m1 = s.SpawnInfantry(0, Marine, new CellCoord(11, 9)), m2 = s.SpawnInfantry(0, Marine, new CellCoord(11, 11));
        foreach (EntityId m in new[] { m1, m2 })
            s.Step(new[] { Cmd(s, CommandKind.Enter, m, craft, default, (int)ActorKind.Infantry | ((int)ActorKind.Vehicle << 8)) });
        Board(s, tanks[0], craft); Board(s, tanks[1], craft);
        for (int t = 0; t < 100 && s.PassengersIn(craft) < 4; t++) Run(s, 1);   // the soldiers walk to the craft first
        Assert.Equal(4, s.PassengersIn(craft));
        s.Step(new[] { Unload(s, craft, 10, 10) });
        var order = new List<string>();
        int inf = 2, veh = 2;
        for (int t = 0; t < 200 && s.PassengersIn(craft) > 0; t++)
        {
            Run(s, 1);
            int ni = (Aboard(s, tanks[0]) ? 1 : 0) + (Aboard(s, tanks[1]) ? 1 : 0);
            int nf = (s.Tunnels.InfantryLayer[m1.Index] == ActorLayer.Passenger ? 1 : 0) + (s.Tunnels.InfantryLayer[m2.Index] == ActorLayer.Passenger ? 1 : 0);
            if (nf != inf) { order.Add("inf"); inf = nf; }
            if (ni != veh) { order.Add("veh"); veh = ni; }
        }
        Assert.Equal(new[] { "inf", "inf", "veh", "veh" }, order);
        Assert.Equal(0, s.PassengersIn(craft));
    }

    [Fact]
    public void SaveLoad_MidVoyage_ReproducesTheHash()
    {
        var (s, craft, tanks) = Beach();
        Board(s, tanks[0], craft); Board(s, tanks[1], craft);
        s.Step(new[] { Cmd(s, CommandKind.Move, craft, default, new CellCoord(4, 20), (int)ActorKind.Vehicle) });
        Run(s, 30);
        Assert.True(Aboard(s, tanks[1]));
        var ms = new MemoryStream();
        s.Save(ms);
        Simulation loaded = Simulation.Load(new MemoryStream(ms.ToArray()), Setup());
        Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
        Assert.Equal(2, loaded.PassengersIn(craft));
        Assert.True(Aboard(loaded, tanks[0]));
        for (int i = 0; i < 40; i++)
        {
            s.Step(Array.Empty<Command>());
            loaded.Step(Array.Empty<Command>());
            Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
        }
    }

    [Fact]
    public void Replay_OfAVehicleLanding_IsDeterministic()
    {
        ulong Play()
        {
            var (s, craft, tanks) = Beach();
            Board(s, tanks[0], craft); Board(s, tanks[1], craft);
            s.Step(new[] { Cmd(s, CommandKind.Move, craft, default, new CellCoord(4, 20), (int)ActorKind.Vehicle) });
            Run(s, 150);
            s.Step(new[] { Unload(s, craft, 10, 22) });
            Run(s, 300);
            Assert.Equal(0, s.PassengersIn(craft));
            return s.ComputeStateHash();
        }
        _seq = 0; ulong a = Play();
        _seq = 0; ulong b = Play();
        Assert.Equal(a, b);
    }

    [Fact]
    public void SinkingCraft_LosesItsVehicles()
    {
        var (s, craft, tanks) = Beach();
        Board(s, tanks[0], craft);
        s.Step(new[] { new Command(s.Tick, 0, ++_seq, CommandKind.Debug, craft, default, default, (int)DebugOp.Kill, (int)ActorKind.Vehicle) });
        Run(s, 3);
        Assert.False(s.Units.Slots.IsAlive(craft));
        Assert.False(s.Units.Slots.IsAlive(tanks[0]));
        Assert.True(s.Units.Slots.IsAlive(tanks[1]));
    }

    [Fact]
    public void Infantry_OnlyTransport_IsUnchanged()
    {
        var (s, bare, _) = Beach(Bare);
        EntityId m = s.SpawnInfantry(0, Marine, new CellCoord(10, 10));
        s.Step(new[] { Cmd(s, CommandKind.Enter, m, bare, default, (int)ActorKind.Infantry | ((int)ActorKind.Vehicle << 8)) });
        Assert.Equal(1, s.PassengersIn(bare));
        s.Step(new[] { Unload(s, bare, 10, 10) });
        Run(s, 40);
        Assert.Equal(0, s.PassengersIn(bare));
    }
}
