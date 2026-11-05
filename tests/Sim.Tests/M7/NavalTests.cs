// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.M7;

/// <summary>M7-03: ships move (docs/design/11_m7_plan.md §2.5, D-124). Own test types (no Expedition roster yet): "boat" (naval), "liner"
/// (naval, deep_only), "tank" (tracked). Maps are built here: Water = deep sea, Shallow = shoals, Clear = land.</summary>
public sealed class NavalTests
{
    private const int Boat = 0, Liner = 1, Tank = 2, W = 40, H = 30;
    private ushort _seq;

    private static TerrainTable Terrain()
    {
        var speed = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        foreach (LandType land in Enum.GetValues<LandType>())
            for (int l = 0; l < RuleCounts.Locomotions; l++)
            {
                bool water = land is LandType.Water or LandType.Shallow;
                bool ship = (Locomotion)l == Locomotion.Naval;
                speed[(int)land * RuleCounts.Locomotions + l] = ship ? (water ? 100 : 0) : water ? 0 : land is LandType.Rock or LandType.Cliff ? 0 : 100;
            }
        var buildable = new bool[RuleCounts.LandTypes];
        buildable[(int)LandType.Clear] = true;
        return new TerrainTable(speed, buildable);
    }

    private static SimSetup Setup(Func<int, int, LandType> land, ulong seed = 7)
    {
        var cells = new LandType[W * H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) cells[y * W + x] = land(x, y);
        var map = new MapData("sea", "rust_belt", W, H, new ushort[W * H], cells, new byte[W * H], new OverlayKind[W * H], new byte[W * H],
            new[] { new CellCoord(1, 1), new CellCoord(W - 2, H - 2) });
        VehicleType[] vehicles =
        {
            ScriptedGame.Vehicle("boat", 40, 100) with { Locomotion = Locomotion.Naval, TurnRate = 16, DrawScalePermille = 1500 },
            ScriptedGame.Vehicle("liner", 30, 300) with { Locomotion = Locomotion.Naval, TurnRate = 8, DeepOnly = true, DrawScalePermille = 2000 },
            ScriptedGame.Vehicle("tank", 40, 200),
        };
        SimRules rules = ScriptedGame.RulesWith(vehicles, Array.Empty<InfantryType>()) with { Terrain = Terrain() };
        return new SimSetup(seed, map, 2, rules, 0xABCDUL) { AllowDebug = true };
    }

    /// <summary>Sea everywhere except an island (x 12..27, y 8..21) that holds a lake (x 16..19, y 12..14).</summary>
    private static LandType Island(int x, int y)
    {
        if (x is >= 16 and <= 19 && y is >= 12 and <= 14) return LandType.Water;
        return x is >= 12 and <= 27 && y is >= 8 and <= 21 ? LandType.Clear : LandType.Water;
    }

    /// <summary>Sea cut by a shoal column at x = 20; with <paramref name="gap"/> the last two rows of the column are deep.</summary>
    private static Func<int, int, LandType> Shoals(bool gap) =>
        (x, y) => x == 20 && !(gap && y >= H - 2) ? LandType.Shallow : LandType.Water;

    private Command Move(Simulation s, EntityId who, int x, int y) =>
        new(s.Tick, 0, ++_seq, CommandKind.Move, who, default, new CellCoord(x, y), (int)ActorKind.Vehicle);

    private static void Run(Simulation s, int n) { for (int i = 0; i < n; i++) s.Step(Array.Empty<Command>()); }

    private static CellCoord CellOf(Simulation s, EntityId e) => s.Units.Position[e.Index].ToCell();

    private static byte[] PathBytes(Simulation s)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) s.Paths.Write(w);
        return ms.ToArray();
    }

    [Fact]
    public void Ship_SailsAroundAnIsland_AndNeverLeavesTheSea()
    {
        var s = new Simulation(Setup(Island));
        EntityId b = s.SpawnVehicle(0, Boat, new CellCoord(2, 2));
        s.Step(new[] { Move(s, b, 35, 25) });
        for (int t = 0; t < 900 && CellOf(s, b) != new CellCoord(35, 25); t++)
        {
            s.Step(Array.Empty<Command>());
            Assert.Equal(LandType.Water, s.Map.LandAt(CellOf(s, b)));
        }
        Assert.Equal(new CellCoord(35, 25), CellOf(s, b));
    }

    [Fact]
    public void Ship_RefusesAnInlandGoalAtOnce_WithoutASearch()
    {
        var s = new Simulation(Setup(Island));
        EntityId b = s.SpawnVehicle(0, Boat, new CellCoord(2, 2));
        byte[] before = PathBytes(s);
        foreach (var goal in new[] { new CellCoord(20, 10), new CellCoord(17, 13) })   // island land, then the landlocked lake
        {
            s.Step(new[] { Move(s, b, goal.X, goal.Y) });
            Assert.NotEqual(MissionKind.Move, s.Units.Mission[b.Index]);
            Assert.NotEqual(MissionKind.Move, s.Units.NextMission[b.Index]);
        }
        Run(s, 40);
        Assert.Equal(new CellCoord(2, 2), CellOf(s, b));
        Assert.Equal(before, PathBytes(s));   // no request was ever made (the next request id is part of the path state)
    }

    [Fact]
    public void LargeShip_AvoidsShallows_SmallShipCrossesThem()
    {
        var gap = new Simulation(Setup(Shoals(gap: true)));
        EntityId liner = gap.SpawnVehicle(0, Liner, new CellCoord(2, 2));
        EntityId boat = gap.SpawnVehicle(0, Boat, new CellCoord(2, 4));
        gap.Step(new[] { Move(gap, liner, 35, 2), Move(gap, boat, 35, 4) });
        bool boatWaded = false, linerThere = false, boatThere = false;   // arrivals are checked separately: an idle ship may be asked to make room
        for (int t = 0; t < 1500 && !(linerThere && boatThere); t++)
        {
            gap.Step(Array.Empty<Command>());
            Assert.NotEqual(LandType.Shallow, gap.Map.LandAt(CellOf(gap, liner)));
            boatWaded |= gap.Map.LandAt(CellOf(gap, boat)) == LandType.Shallow;
            linerThere |= CellOf(gap, liner) == new CellCoord(35, 2);
            boatThere |= CellOf(gap, boat) == new CellCoord(35, 4);
        }
        Assert.True(linerThere);
        Assert.True(boatThere);
        Assert.True(boatWaded);

        // No gap: the shoals split the sea for the liner only.
        var closed = new Simulation(Setup(Shoals(gap: false)));
        EntityId l2 = closed.SpawnVehicle(0, Liner, new CellCoord(2, 2));
        EntityId b2 = closed.SpawnVehicle(0, Boat, new CellCoord(2, 4));
        closed.Step(new[] { Move(closed, l2, 35, 2) });
        Assert.NotEqual(MissionKind.Move, closed.Units.Mission[l2.Index]);
        closed.Step(new[] { Move(closed, b2, 35, 4) });
        Assert.Equal(MissionKind.Move, closed.Units.Mission[b2.Index]);
        var view = closed.ViewFor(0);
        Assert.False(view.CanOwnShipReach(l2, new CellCoord(35, 2)));
        Assert.True(view.CanOwnShipReach(b2, new CellCoord(35, 4)));
        Assert.NotEqual(view.NavalRegionAt(new CellCoord(2, 2), deepOnly: true), view.NavalRegionAt(new CellCoord(35, 2), deepOnly: true));
        Assert.Equal(view.NavalRegionAt(new CellCoord(2, 2)), view.NavalRegionAt(new CellCoord(35, 2)));
        Assert.Equal(0, view.NavalRegionAt(new CellCoord(20, 5), deepOnly: true));
    }

    [Fact]
    public void View_ReportsOnlyOwnShips_AndNoLandRegion()
    {
        var s = new Simulation(Setup(Island));
        EntityId mine = s.SpawnVehicle(0, Boat, new CellCoord(2, 2));
        EntityId theirs = s.SpawnVehicle(1, Boat, new CellCoord(3, 2));
        EntityId tank = s.SpawnVehicle(0, Tank, new CellCoord(14, 10));
        var v = s.ViewFor(0);
        Assert.True(v.CanOwnShipReach(mine, new CellCoord(35, 25)));
        Assert.False(v.CanOwnShipReach(mine, new CellCoord(20, 10)));
        Assert.False(v.CanOwnShipReach(mine, new CellCoord(17, 13)));   // the lake is another sea
        Assert.False(v.CanOwnShipReach(theirs, new CellCoord(35, 25)));
        Assert.False(v.CanOwnShipReach(tank, new CellCoord(35, 25)));
        Assert.Equal(0, v.NavalRegionAt(new CellCoord(20, 10)));
        Assert.NotEqual(0, v.NavalRegionAt(new CellCoord(17, 13)));
    }

    [Fact]
    public void LandTank_RefusesAWaterGoal()
    {
        var s = new Simulation(Setup(Island));
        EntityId t = s.SpawnVehicle(0, Tank, new CellCoord(14, 10));
        byte[] before = PathBytes(s);
        s.Step(new[] { Move(s, t, 2, 2) });      // open sea
        s.Step(new[] { Move(s, t, 17, 13) });    // the lake inside the island
        Assert.NotEqual(MissionKind.Move, s.Units.Mission[t.Index]);
        Run(s, 30);
        Assert.Equal(new CellCoord(14, 10), CellOf(s, t));
        Assert.Equal(before, PathBytes(s));
        s.Step(new[] { Move(s, t, 25, 19) });    // land is still fine
        Run(s, 400);
        Assert.Equal(new CellCoord(25, 19), CellOf(s, t));
    }

    [Fact]
    public void Ships_NeverShareACell()
    {
        var s = new Simulation(Setup(Island));
        var ids = new List<EntityId>();
        for (int i = 0; i < 6; i++) ids.Add(s.SpawnVehicle(0, i % 2 == 0 ? Boat : Liner, new CellCoord(2 + i % 2, 2 + i / 2)));
        s.Step(ids.Select(i => Move(s, i, 34, 26)).ToArray());
        for (int t = 0; t < 700; t++)
        {
            if (t == 150) s.Step(ids.Select((i, k) => Move(s, i, 3, 26 - k)).ToArray());   // turn the whole fleet round, through each other
            else s.Step(Array.Empty<Command>());
            var seen = new HashSet<CellCoord>();
            foreach (EntityId i in ids) Assert.True(seen.Add(CellOf(s, i)), $"two ships in {CellOf(s, i)} at tick {s.Tick}");
        }
    }

    [Fact]
    public void SaveLoad_MidVoyage_ReplaysToTheSameHash()
    {
        Simulation a = Build();
        Run(a, 70);
        var ms = new MemoryStream();
        a.Save(ms);
        ms.Position = 0;
        Simulation b = Simulation.Load(ms, a.Setup);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        Run(a, 400); Run(b, 400);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        Simulation c = Build();
        Run(c, 470);
        Assert.Equal(a.ComputeStateHash(), c.ComputeStateHash());
    }

    [Fact]
    public void Replay_SameCommands_SameHashEveryTick()
    {
        Simulation a = Build(), b = Build();
        for (int t = 0; t < 500; t++)
        {
            Run(a, 1); Run(b, 1);
            Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        }
    }

    [Fact]
    public void NavalRegions_AreSavedLabels_OfTheSea()
    {
        var s = new Simulation(Setup(Island));
        Run(s, 2);
        ushort sea = s.Naval.NavalRegion[2 * W + 2], lake = s.Naval.NavalRegion[13 * W + 17];
        Assert.NotEqual(0, sea);
        Assert.NotEqual(0, lake);
        Assert.NotEqual(sea, lake);
        Assert.Equal(1, sea);   // labels count in cell-index order: the first water cell is in the open sea
        Assert.Equal(0, s.Naval.NavalRegion[10 * W + 20]);
    }

    [Fact]
    public void RulesWithoutShips_LeaveTheNavalPartEmpty()
    {
        SimSetup setup = Setup(Island);
        var s = new Simulation(setup with { Rules = ScriptedGame.RulesWith(new[] { ScriptedGame.Vehicle("tank", 40, 200) }, Array.Empty<InfantryType>()) with { Terrain = Terrain() } });
        Run(s, 5);
        Assert.All(s.Naval.NavalRegion, r => Assert.Equal(0, r));
    }

    private Simulation Build()
    {
        _seq = 0;
        var s = new Simulation(Setup(Island));
        EntityId b = s.SpawnVehicle(0, Boat, new CellCoord(2, 2));
        EntityId l = s.SpawnVehicle(0, Liner, new CellCoord(2, 4));
        EntityId e = s.SpawnVehicle(1, Boat, new CellCoord(36, 26));
        s.Step(new[] { Move(s, b, 35, 25), Move(s, l, 35, 5), Move(s, e, 3, 3) with { Player = 1 } });
        return s;
    }
}
