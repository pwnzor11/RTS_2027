// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Reflection;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.M7;

/// <summary>M7-04: landing craft and Skyhook unload (docs/design/11_m7_plan.md §2.5). Map: deep sea x 0..7, shoals x 8..9, beach (Coast)
/// x = 10, grass x &gt;= 11, with a cliff at (10,5) and a lake at (14..15, 12..13). Types: "craft" (naval, 8 seats, unload_ticks 5),
/// "liner" (naval, deep_only), "hook" (air, 6 seats), "tank" (tracked).</summary>
public sealed class LandingTests
{
    private const int Craft = 0, Liner = 1, Hook = 2, Tank = 3, Marine = 0, W = 30, H = 30, PerPassenger = 5;
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
        return new TerrainTable(speed, buildable);
    }

    private static LandType Land(int x, int y)
    {
        if (x == 10 && y == 5) return LandType.Cliff;
        if (x is 14 or 15 && y is 12 or 13) return LandType.Water;
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
            ScriptedGame.Vehicle("craft", 40, 100) with { Locomotion = Locomotion.Naval, TurnRate = 16, Passengers = 8, UnloadTicks = PerPassenger },
            ScriptedGame.Vehicle("liner", 30, 300) with { Locomotion = Locomotion.Naval, TurnRate = 8, DeepOnly = true },
            ScriptedGame.Vehicle("hook", 60, 80) with { Locomotion = Locomotion.Air, Passengers = 6 },
            ScriptedGame.Vehicle("tank", 40, 200),
        };
        SimRules rules = ScriptedGame.RulesWith(vehicles, new[] { ScriptedGame.Infantry("marine", 9, 50) }) with { Terrain = Terrain() };
        return new SimSetup(11, map, 2, rules, 0xABCDUL) { AllowDebug = true };
    }

    private Command Cmd(Simulation s, CommandKind k, EntityId who, EntityId target, CellCoord cell, int arg) =>
        new(s.Tick, 0, ++_seq, k, who, target, cell, arg);

    private static void Run(Simulation s, int n) { for (int i = 0; i < n; i++) s.Step(Array.Empty<Command>()); }

    private static CellCoord CellOf(Simulation s, EntityId e) => s.Units.Position[e.Index].ToCell();

    /// <summary>A craft at (9,10) with three marines standing on the beach beside it, all boarded.</summary>
    private (Simulation Sim, EntityId Craft, EntityId[] Marines) Loaded(int craftType = Craft, int count = 3)
    {
        var s = new Simulation(Setup());
        EntityId c = s.SpawnVehicle(0, craftType, new CellCoord(9, 10));
        var marines = new EntityId[count];
        for (int k = 0; k < count; k++) marines[k] = s.SpawnInfantry(0, Marine, new CellCoord(10, 9 + k));
        foreach (EntityId m in marines)
            s.Step(new[] { Cmd(s, CommandKind.Enter, m, c, default, (int)ActorKind.Infantry | ((int)ActorKind.Vehicle << 8)) });
        return (s, c, marines);
    }

    private Command Unload(Simulation s, EntityId craft, int x, int y) =>
        Cmd(s, CommandKind.Unload, craft, default, new CellCoord(x, y), (int)ActorKind.Vehicle);

    [Fact]
    public void Marines_Board_Sail_AndLandOnePerInterval()
    {
        var (s, craft, marines) = Loaded();
        Assert.Equal(3, s.PassengersIn(craft));
        s.Step(new[] { Cmd(s, CommandKind.Move, craft, default, new CellCoord(4, 20), (int)ActorKind.Vehicle) });
        for (int t = 0; t < 600 && CellOf(s, craft) != new CellCoord(4, 20); t++) Run(s, 1);
        Assert.Equal(new CellCoord(4, 20), CellOf(s, craft));
        Assert.Equal(3, s.PassengersIn(craft));

        s.Step(new[] { Unload(s, craft, 10, 22) });
        var seen = new List<int>();
        for (int t = 0; t < 800 && s.PassengersIn(craft) > 0; t++) { Run(s, 1); if (seen.Count == 0 || seen[^1] != s.PassengersIn(craft)) seen.Add(s.PassengersIn(craft)); }
        Assert.Equal(new[] { 3, 2, 1, 0 }, seen);
        Assert.True(CellOf(s, craft).X >= 9 && Math.Abs(CellOf(s, craft).Y - 22) <= 1);
        foreach (EntityId m in marines)
        {
            Assert.Equal(ActorLayer.Surface, s.Tunnels.InfantryLayer[m.Index]);
            CellCoord mc = s.Infantry.Position[m.Index].ToCell();
            Assert.True(Math.Max(Math.Abs(mc.X - 10), Math.Abs(mc.Y - 22)) <= 2);
        }
        Run(s, 3);
        Assert.NotEqual(MissionKind.Unload, s.Units.Mission[craft.Index]);
        Assert.Equal(0, s.Naval.UnloadTicksLeft[craft.Index]);
    }

    [Fact]
    public void Unload_IsOnePassengerEveryUnloadTicks()
    {
        var (s, craft, _) = Loaded();
        s.Step(new[] { Unload(s, craft, 10, 10) });   // already adjacent: starts counting at once
        var leaveTicks = new List<int>();
        int before = s.PassengersIn(craft);
        for (int t = 0; t < 60 && s.PassengersIn(craft) > 0; t++)
        {
            Run(s, 1);
            if (s.PassengersIn(craft) != before) { leaveTicks.Add(s.Tick); before = s.PassengersIn(craft); }
        }
        Assert.Equal(3, leaveTicks.Count);
        Assert.Equal(PerPassenger, leaveTicks[1] - leaveTicks[0]);
        Assert.Equal(PerPassenger, leaveTicks[2] - leaveTicks[1]);
    }

    [Theory]
    [InlineData(10, 5)]    // cliff
    [InlineData(5, 10)]    // deep water
    [InlineData(8, 10)]    // shoals: not a standable cell for foot in this table
    [InlineData(14, 12)]   // lake
    public void Unload_IsRefusedOnCellsFootCannotStandOn(int x, int y)
    {
        var (s, craft, _) = Loaded();
        s.Step(new[] { Unload(s, craft, x, y) });
        Run(s, 60);
        Assert.Equal(3, s.PassengersIn(craft));
        Assert.NotEqual(MissionKind.Unload, s.Units.Mission[craft.Index]);
        Assert.Equal(0, s.Naval.UnloadTicksLeft[craft.Index]);
    }

    [Fact]
    public void Unload_WithNobodyAboard_IsIgnored()
    {
        var s = new Simulation(Setup());
        EntityId craft = s.SpawnVehicle(0, Craft, new CellCoord(9, 10));
        s.Step(new[] { Unload(s, craft, 10, 10) });
        Run(s, 5);
        Assert.NotEqual(MissionKind.Unload, s.Units.Mission[craft.Index]);
    }

    [Fact]
    public void Skyhook_FliesToTheCell_AndPutsTheSquadDown()
    {
        var (s, hook, marines) = Loaded(Hook, 2);
        s.Step(new[] { Unload(s, hook, 14, 20) });
        for (int t = 0; t < 400 && s.PassengersIn(hook) > 0; t++) Run(s, 1);
        Assert.Equal(0, s.PassengersIn(hook));
        Assert.Equal(new CellCoord(14, 20), CellOf(s, hook));
        foreach (EntityId m in marines)
        {
            CellCoord mc = s.Infantry.Position[m.Index].ToCell();
            Assert.True(Math.Max(Math.Abs(mc.X - 14), Math.Abs(mc.Y - 20)) <= 1);   // the first takes the cell, the next spread out
        }
    }

    [Fact]
    public void SaveLoad_MidUnload_ReproducesTheHash()
    {
        var (s, craft, _) = Loaded();
        s.Step(new[] { Unload(s, craft, 10, 10) });
        Run(s, PerPassenger + 2);   // one ashore, the next countdown running
        Assert.Equal(2, s.PassengersIn(craft));
        Assert.True(s.Naval.UnloadTicksLeft[craft.Index] > 0);
        var ms = new MemoryStream();
        s.Save(ms);
        Simulation loaded = Simulation.Load(new MemoryStream(ms.ToArray()), Setup());
        Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
        for (int i = 0; i < 40; i++)
        {
            s.Step(Array.Empty<Command>());
            loaded.Step(Array.Empty<Command>());
            Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
        }
        Assert.Equal(0, loaded.PassengersIn(craft));
    }

    [Fact]
    public void Replay_OfALanding_IsDeterministic()
    {
        ulong Play()
        {
            var (s, craft, _) = Loaded();
            s.Step(new[] { Cmd(s, CommandKind.Move, craft, default, new CellCoord(4, 20), (int)ActorKind.Vehicle) });
            Run(s, 150);
            s.Step(new[] { Unload(s, craft, 10, 22) });
            Run(s, 300);
            return s.ComputeStateHash();
        }
        _seq = 0; ulong a = Play();
        _seq = 0; ulong b = Play();
        Assert.Equal(a, b);
    }

    private static CellCoord ExitFor(Simulation s, int type, CellCoord exit)
    {
        MethodInfo m = typeof(Simulation).GetMethod("FindFreeExitCell", BindingFlags.NonPublic | BindingFlags.Instance)!;
        VehicleType vt = s.Setup.Rules.Vehicles[type];
        var args = new object?[] { exit, Rts.Sim.Pathing.MoveClass.Of(vt), default(CellCoord) };
        Assert.True((bool)m.Invoke(s, args)!);
        return (CellCoord)args[2]!;
    }

    [Fact]
    public void ExitCell_DeepShipAvoidsShallow_ShipStaysOnWater_LandUnitsUnchanged()
    {
        var s = new Simulation(Setup());
        var shoal = new CellCoord(8, 10);
        CellCoord liner = ExitFor(s, Liner, shoal), craft = ExitFor(s, Craft, shoal), tank = ExitFor(s, Tank, shoal);
        Assert.Equal(LandType.Water, s.Map.LandAt(liner));
        Assert.Equal(shoal, craft);   // a coastal craft may leave onto the shoal itself
        Assert.True(s.Map.LandAt(tank) is LandType.Coast or LandType.Clear);
        CellCoord inland = ExitFor(s, Craft, new CellCoord(20, 20));
        Assert.True(s.Map.LandAt(inland) is LandType.Water or LandType.Shallow);
    }
}
