// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.Tunnels;

namespace Rts.Sim.Tests.M3.Tunnels;

/// <summary>Network transit (design 05 §1, D-074): queue order, per-entrance throughput, network cap, size checks, transit
/// time, surfacing telegraph, D2 re-route, save/load mid-transit, determinism. Directorate-style types are built in code.</summary>
public sealed class TunnelTransitTests
{
    // Building types (index): 0 hatch (S, 20 ticks/pt), 1 bore head (M, 13 ticks/pt), 2 plain (no entrance). All 2x2, exit (1,2).
    private const int Hatch = 0, BoreHead = 1, Plain = 2;
    // Vehicles: 0 crawler (M), 1 authority tank (None), 2 land-train (L). Infantry: 0 cultist (S, the default).
    private const int Crawler = 0, Tank = 1, Train = 2, Cultist = 0;

    private static SimRules Rules()
    {
        VehicleType v = ScriptedGame.Vehicle("dir_crawler", 30, 100);
        var vehicles = new[]
        {
            v with { TunnelSize = TunnelSize.M },
            ScriptedGame.Vehicle("auth_tank", 30, 100),
            ScriptedGame.Vehicle("dir_train", 20, 300) with { TunnelSize = TunnelSize.L },
        };
        var infantry = new[] { ScriptedGame.Infantry("dir_cultist", 9, 50) };
        var buildings = new[]
        {
            ScriptedGame.Building("dir_hatch", 2, 2, 200) with { Entrance = TunnelSize.S, TicksPerPoint = 20 },
            ScriptedGame.Building("dir_borehead", 2, 2, 400) with { Entrance = TunnelSize.M, TicksPerPoint = 13 },
            ScriptedGame.Building("plain", 2, 2, 300),
        };
        return new SimRules(vehicles, infantry, buildings, Array.Empty<WeaponType>(), Array.Empty<WarheadType>(),
            ScriptedGame.Terrain(), Array.Empty<EffectDef>());
    }

    private static SimSetup Setup(SimRules? rules = null) =>
        new(7, MapData.CreateFlat(64, 64), 2, rules ?? Rules(), 0x7E57UL) { AllowDebug = true };

    /// <summary>New sim with a plain building per player (so losing entrances never defeats anyone).</summary>
    private static Simulation NewSim(SimSetup? setup = null)
    {
        var sim = new Simulation(setup ?? Setup());
        Assert.False(sim.PlaceBuilding(0, Plain, new CellCoord(2, 56)).IsNone);
        Assert.False(sim.PlaceBuilding(1, Plain, new CellCoord(58, 2)).IsNone);
        return sim;
    }

    private static EntityId Place(Simulation sim, int type, int x, int y, byte player = 0)
    {
        EntityId id = sim.PlaceBuilding(player, type, new CellCoord(x, y));
        Assert.False(id.IsNone);
        return id;
    }

    private static ActorRef Inf(Simulation sim, int x, int y, int sub = 0, byte player = 0) =>
        new(ActorKind.Infantry, sim.SpawnInfantry(player, Cultist, new CellCoord(x, y), sub));

    private static ActorRef Veh(Simulation sim, int type, int x, int y, byte player = 0) =>
        new(ActorKind.Vehicle, sim.SpawnVehicle(player, type, new CellCoord(x, y)));

    private static ushort _seq;

    private static Command Enter(Simulation sim, ActorRef unit, EntityId dest, byte player = 0) =>
        new(sim.Tick, player, _seq++, CommandKind.Enter, unit.Id, dest, default, (int)unit.Kind | ((int)ActorKind.Building << 8));

    private static Command Kill(Simulation sim, EntityId building) =>
        new(sim.Tick, 0, _seq++, CommandKind.Debug, building, default, default, (int)DebugOp.Kill, (int)ActorKind.Building);

    private static void Step(Simulation sim, params Command[] cmds) => sim.Step(cmds);

    /// <summary>Steps until <paramref name="done"/> holds after a step; returns sim.Tick then.</summary>
    private static int StepUntil(Simulation sim, Func<bool> done, int max = 1000)
    {
        for (int n = 0; n < max; n++)
        {
            sim.Step(Array.Empty<Command>());
            if (done()) return sim.Tick;
        }
        throw new Xunit.Sdk.XunitException("condition not reached");
    }

    private static ActorLayer Layer(Simulation sim, ActorRef u) => sim.LayerOf(u.Kind, u.Id.Index);

    private static MissionKind Mission(Simulation sim, ActorRef u) =>
        u.Kind == ActorKind.Vehicle ? sim.Units.Mission[u.Id.Index] : sim.Infantry.Mission[u.Id.Index];

    private static CellCoord CellOf(Simulation sim, ActorRef u) =>
        (u.Kind == ActorKind.Vehicle ? sim.Units.Position[u.Id.Index] : sim.Infantry.Position[u.Id.Index]).ToCell();

    private static int NetworkPoints(Simulation sim, byte owner) =>
        sim.Tunnels.Transit.Where(t => t.Owner == owner && Layer(sim, t.Unit) is ActorLayer.Queued or ActorLayer.Tunnel).Sum(t => t.Points);

    private static TunnelTransit TransitOf(Simulation sim, ActorRef u) => sim.Tunnels.Transit.Single(t => t.Unit == u);

    // ---- queue order and throughput -----------------------------------------------------------

    [Fact]
    public void Queue_IsArrivalOrder_AndAHatchDrawsOnePointPerTwentyTicks()
    {
        var sim = NewSim();
        EntityId a = Place(sim, Hatch, 10, 10), b = Place(sim, Hatch, 40, 40);
        ActorRef u1 = Inf(sim, 10, 12), u2 = Inf(sim, 11, 12), u3 = Inf(sim, 12, 12);
        // Ordered u3, u1, u2 on consecutive ticks: all already stand next to the hatch, so that is their arrival order.
        Step(sim, Enter(sim, u3, b));
        Step(sim, Enter(sim, u1, b));
        Step(sim, Enter(sim, u2, b));
        Assert.Equal(new[] { u3, u1, u2 }, sim.Tunnels.Queue.Select(q => q.Unit).ToArray());
        Assert.All(sim.Tunnels.Queue, q => Assert.Equal(a, q.Entrance));
        Assert.Equal(new[] { 1, 2, 3 }, sim.Tunnels.Queue.Select(q => q.Ticket).ToArray());
        Assert.All(new[] { u1, u2, u3 }, u => Assert.Equal(ActorLayer.Queued, Layer(sim, u)));

        int t3 = StepUntil(sim, () => Layer(sim, u3) == ActorLayer.Tunnel);
        Assert.Equal(ActorLayer.Queued, Layer(sim, u1));
        int t1 = StepUntil(sim, () => Layer(sim, u1) == ActorLayer.Tunnel);
        Assert.Equal(ActorLayer.Queued, Layer(sim, u2));
        int t2 = StepUntil(sim, () => Layer(sim, u2) == ActorLayer.Tunnel);
        int perUnit = sim.Setup.Rules.Tunnels.PointsS * 20;
        Assert.Equal(1 + perUnit, t3);   // enqueued and started on tick 0, underground in the step of tick 20
        Assert.Equal(perUnit, t1 - t3);
        Assert.Equal(perUnit, t2 - t1);
    }

    [Fact]
    public void BoreHead_DrawsAtThirteenTicksPerPoint_ForMAndSUnits()
    {
        var sim = NewSim();
        Place(sim, BoreHead, 10, 10);
        EntityId b = Place(sim, BoreHead, 40, 40);
        ActorRef c1 = Veh(sim, Crawler, 10, 12), c2 = Veh(sim, Crawler, 12, 12);
        ActorRef i1 = Inf(sim, 9, 9), i2 = Inf(sim, 9, 9, 1);
        Step(sim, Enter(sim, c1, b), Enter(sim, c2, b));
        Step(sim, Enter(sim, i1, b), Enter(sim, i2, b));
        int m = sim.Setup.Rules.Tunnels.PointsM * 13, s = sim.Setup.Rules.Tunnels.PointsS * 13;
        int tc1 = StepUntil(sim, () => Layer(sim, c1) == ActorLayer.Tunnel);
        int tc2 = StepUntil(sim, () => Layer(sim, c2) == ActorLayer.Tunnel);
        int ti1 = StepUntil(sim, () => Layer(sim, i1) == ActorLayer.Tunnel);
        int ti2 = StepUntil(sim, () => Layer(sim, i2) == ActorLayer.Tunnel);
        Assert.Equal(m, tc2 - tc1);
        Assert.Equal(s, ti1 - tc2);
        Assert.Equal(s, ti2 - ti1);
    }

    [Fact]
    public void NetworkCap_LimitsPointsInTransit_AndAFullNetworkStallsOtherEntrances()
    {
        var sim = NewSim();
        var units = new List<ActorRef>();
        for (int k = 0; k < 4; k++)
        {
            Place(sim, BoreHead, 5 + k * 10, 5);
            units.Add(Veh(sim, Crawler, 6 + k * 10, 7));
        }
        EntityId dest = Place(sim, BoreHead, 45, 45);
        Step(sim, units.Select(u => Enter(sim, u, dest)).ToArray());
        int cap = sim.Setup.Rules.Tunnels.NetworkCapPoints;
        Assert.Equal(9, NetworkPoints(sim, 0));   // three M units drawing in (3 pts each); the fourth would make 12 > 10
        Assert.Equal(3, sim.Tunnels.Transit.Count);
        ActorRef fourth = units.Single(u => !sim.Tunnels.Transit.Any(t => t.Unit == u));
        int firstSurfacing = -1, fourthStart = -1;
        for (int n = 0; n < 300 && fourthStart < 0; n++)
        {
            Step(sim);
            Assert.True(NetworkPoints(sim, 0) <= cap);
            if (firstSurfacing < 0 && units.Any(u => Layer(sim, u) == ActorLayer.Surfacing)) firstSurfacing = sim.Tick;
            if (sim.Tunnels.Transit.Any(t => t.Unit == fourth)) fourthStart = sim.Tick;
        }
        Assert.True(firstSurfacing > 0);
        Assert.Equal(firstSurfacing, fourthStart);   // points free once a unit starts surfacing; the stalled entrance starts that tick
    }

    // ---- size checks ------------------------------------------------------------------------------

    [Fact]
    public void Enter_IsRefused_WhenTheUnitDoesNotFitOrCannotUseTheNetwork()
    {
        var sim = NewSim();
        EntityId hatch = Place(sim, Hatch, 10, 10), bore = Place(sim, BoreHead, 40, 40);
        ActorRef crawlerToHatch = Veh(sim, Crawler, 20, 20), crawlerToBore = Veh(sim, Crawler, 22, 20);
        ActorRef tank = Veh(sim, Tank, 24, 20), train = Veh(sim, Train, 26, 20), cultist = Inf(sim, 28, 20);
        Step(sim, Enter(sim, crawlerToHatch, hatch),
            Enter(sim, crawlerToBore, bore),   // only other entrance is a Hatch: M cannot get in anywhere
            Enter(sim, tank, bore), Enter(sim, train, bore),
            Enter(sim, cultist, bore));        // S fits the Hatch entry and the Bore Head exit
        Assert.NotEqual(MissionKind.EnterTunnel, Mission(sim, crawlerToHatch));
        Assert.NotEqual(MissionKind.EnterTunnel, Mission(sim, crawlerToBore));
        Assert.NotEqual(MissionKind.EnterTunnel, Mission(sim, tank));
        Assert.NotEqual(MissionKind.EnterTunnel, Mission(sim, train));
        Assert.Equal(MissionKind.EnterTunnel, Mission(sim, cultist));
        StepUntil(sim, () => sim.Tunnels.Queue.Count == 1);
        Assert.Equal(hatch, sim.Tunnels.Queue[0].Entrance);
    }

    [Fact]
    public void Walker_GoesToTheNearestFittingEntrance()
    {
        var sim = NewSim();
        EntityId near = Place(sim, Hatch, 20, 10), far = Place(sim, BoreHead, 4, 10), dest = Place(sim, BoreHead, 40, 40);
        ActorRef inf = Inf(sim, 24, 20), crawler = Veh(sim, Crawler, 24, 22);
        Step(sim, Enter(sim, inf, dest), Enter(sim, crawler, dest));
        var entries = new Dictionary<ActorRef, EntityId>();
        StepUntil(sim, () =>
        {
            foreach (TunnelQueueEntry q in sim.Tunnels.Queue) entries[q.Unit] = q.Entrance;
            return entries.Count == 2;
        }, 600);
        Assert.Equal(near, entries[inf]);
        Assert.Equal(far, entries[crawler]);   // the nearer Hatch does not fit an M unit
    }

    // ---- transit time, surfacing, fog ---------------------------------------------------------

    [Fact]
    public void Transit_TakesTransitTicks_ThenSurfacesBehindAUnitTelegraph_AtTheDestinationExit()
    {
        var sim = NewSim();
        Place(sim, Hatch, 10, 10);
        EntityId b = Place(sim, Hatch, 40, 40);
        ActorRef u = Inf(sim, 10, 12);
        ActorRef watcher = Inf(sim, 43, 44, 0, 1);   // enemy standing next to the exit: the area is in its vision
        Step(sim, Enter(sim, u, b));
        int under = StepUntil(sim, () => Layer(sim, u) == ActorLayer.Tunnel);
        Assert.True(sim.IsUnderground(u.Kind, u.Id.Index));
        Assert.DoesNotContain(sim.Snapshot(1).Units, v => v.Id == u.Id && v.Kind == UnitKind.Infantry);
        Assert.Contains(sim.Snapshot(0).Units, v => v.Id == u.Id && v.Underground);
        Assert.True(sim.Map.InfantryAt(new CellCoord(10, 12), 0).IsNone);   // left the surface cell

        int surfacing = StepUntil(sim, () => Layer(sim, u) == ActorLayer.Surfacing);
        Assert.Equal(sim.Setup.Rules.Tunnels.TransitTicks, surfacing - under);
        Telegraph g = Assert.Single(sim.Tunnels.Telegraphs);
        Assert.Equal((TelegraphKind.Unit, (byte)0, u, sim.Setup.Rules.Tunnels.TelegraphTicks), (g.Kind, g.Owner, g.Subject, g.TicksLeft));
        Assert.Equal(new CellCoord(41, 42), g.Cell);   // the destination's exit cell
        Assert.Equal(g.Cell, CellOf(sim, u));
        Assert.Contains(sim.Snapshot(1).Telegraphs, t => t.Cell == g.Cell && t.Kind == TelegraphKind.Unit);
        Assert.Equal(0, NetworkPoints(sim, 0));   // surfacing units no longer count against the cap

        int surfaced = StepUntil(sim, () => Layer(sim, u) == ActorLayer.Surface);
        Assert.Equal(sim.Setup.Rules.Tunnels.TelegraphTicks, surfaced - surfacing);
        Assert.Empty(sim.Tunnels.Telegraphs);
        Assert.Empty(sim.Tunnels.Transit);
        Assert.Equal(u.Id, sim.Map.InfantryAt(new CellCoord(41, 42), sim.Infantry.SubSlot[u.Id.Index]));
        Assert.Contains(sim.Snapshot(1).Units, v => v.Id == u.Id && v.Kind == UnitKind.Infantry && !v.Underground);
        Step(sim);
        Assert.Equal(MissionKind.Guard, Mission(sim, u));
        Assert.True(sim.Infantry.Slots.IsAlive(watcher.Id));
    }

    [Fact]
    public void OrdersToAnUndergroundUnit_AreIgnored()
    {
        var sim = NewSim();
        Place(sim, Hatch, 10, 10);
        EntityId b = Place(sim, Hatch, 40, 40);
        ActorRef u = Inf(sim, 10, 12);
        Step(sim, Enter(sim, u, b));
        StepUntil(sim, () => Layer(sim, u) == ActorLayer.Tunnel);
        Step(sim, new Command(sim.Tick, 0, _seq++, CommandKind.Move, u.Id, default, new CellCoord(5, 5), (int)ActorKind.Infantry));
        Assert.Equal(ActorLayer.Tunnel, Layer(sim, u));
        Assert.Equal(MissionKind.Transit, Mission(sim, u));
        StepUntil(sim, () => Layer(sim, u) == ActorLayer.Surface);
        Assert.Equal(new CellCoord(41, 42), CellOf(sim, u));
    }

    [Fact]
    public void AnotherOrder_TakesAQueuedUnitOutOfTheQueue()
    {
        var sim = NewSim();
        Place(sim, Hatch, 10, 10);
        EntityId b = Place(sim, Hatch, 40, 40);
        ActorRef u1 = Inf(sim, 10, 12), u2 = Inf(sim, 11, 12);
        Step(sim, Enter(sim, u1, b), Enter(sim, u2, b));
        Step(sim, new Command(sim.Tick, 0, _seq++, CommandKind.Move, u2.Id, default, new CellCoord(5, 20), (int)ActorKind.Infantry));
        Assert.Equal(ActorLayer.Surface, Layer(sim, u2));
        Assert.DoesNotContain(sim.Tunnels.Queue, q => q.Unit == u2);
        Assert.Equal(MissionKind.Move, Mission(sim, u2));
    }

    // ---- D2: exit lost ----------------------------------------------------------------------------

    [Fact]
    public void ExitLost_ReroutesTransitAndQueuedUnits_ToTheNearestSurvivingEntrance()
    {
        var sim = NewSim();
        EntityId a = Place(sim, Hatch, 10, 10), b = Place(sim, Hatch, 40, 40);
        EntityId c = Place(sim, Hatch, 46, 40), far = Place(sim, Hatch, 20, 50);
        ActorRef u1 = Inf(sim, 10, 12), u2 = Inf(sim, 11, 12);
        Step(sim, Enter(sim, u1, b));
        Step(sim, Enter(sim, u2, b));
        StepUntil(sim, () => Layer(sim, u1) == ActorLayer.Tunnel);
        Step(sim);
        Step(sim, Kill(sim, b));
        Assert.False(sim.Buildings.Slots.IsAlive(b));
        TunnelTransit t = TransitOf(sim, u1);
        Assert.Equal(c, t.Destination);   // C is 6 cells from B; the Hatch at (20,50) and A are further
        Assert.Equal(sim.Setup.Rules.Tunnels.TransitTicks, t.TicksLeft);   // restarted
        Assert.Equal(c, sim.Tunnels.Queue.Single(q => q.Unit == u2).Destination);
        StepUntil(sim, () => Layer(sim, u1) == ActorLayer.Surface);
        Assert.Equal(new CellCoord(47, 42), CellOf(sim, u1));
        StepUntil(sim, () => Layer(sim, u2) == ActorLayer.Surface);
        CellCoord c2 = CellOf(sim, u2);
        Assert.True(Math.Max(Math.Abs(c2.X - 47), Math.Abs(c2.Y - 42)) <= 1);   // around C's exit
        Assert.True(sim.Buildings.Slots.IsAlive(a) && sim.Buildings.Slots.IsAlive(far));
    }

    [Fact]
    public void ExitLost_WithNoSurvivingEntrance_LosesTheUnitsUnderground()
    {
        var sim = NewSim();
        EntityId a = Place(sim, Hatch, 10, 10), b = Place(sim, Hatch, 40, 40);
        ActorRef u = Inf(sim, 10, 12);
        int cash = sim.Players.Cash[0];
        Step(sim, Enter(sim, u, b));
        StepUntil(sim, () => Layer(sim, u) == ActorLayer.Tunnel);
        Step(sim, Kill(sim, a));   // the entry is gone; the unit is already inside
        Assert.Equal(b, TransitOf(sim, u).Destination);
        Step(sim, Kill(sim, b));
        Assert.Empty(sim.Tunnels.Transit);
        Step(sim);
        Assert.False(sim.Infantry.Slots.IsAlive(u.Id));
        Assert.Equal(cash, sim.Players.Cash[0]);   // no credits back
    }

    [Fact]
    public void EntryLost_DropsItsQueue_UnitsStayOnTheSurface()
    {
        var sim = NewSim();
        EntityId a = Place(sim, Hatch, 10, 10), b = Place(sim, Hatch, 40, 40);
        ActorRef u1 = Inf(sim, 10, 12), u2 = Inf(sim, 11, 12);
        Step(sim, Enter(sim, u1, b), Enter(sim, u2, b));
        StepUntil(sim, () => Layer(sim, u1) == ActorLayer.Tunnel);
        Step(sim, Kill(sim, a));
        Assert.Empty(sim.Tunnels.Queue);
        Assert.Equal(ActorLayer.Surface, Layer(sim, u2));
        Assert.Equal(ActorLayer.Tunnel, Layer(sim, u1));   // already inside: carries on to B
        Assert.Single(sim.Tunnels.Transit);
        Step(sim);
        Assert.Equal(MissionKind.Guard, Mission(sim, u2));
        StepUntil(sim, () => Layer(sim, u1) == ActorLayer.Surface);
    }

    // ---- save / load and determinism --------------------------------------------------------

    /// <summary>Five cultists through A to B, plus a crawler pair between bore heads; run to a tick with units in every phase.</summary>
    private static Simulation BusyNetwork(out ActorRef[] units)
    {
        var sim = NewSim();
        Place(sim, Hatch, 10, 10);
        EntityId b = Place(sim, Hatch, 40, 40);
        Place(sim, BoreHead, 20, 30);
        EntityId bore2 = Place(sim, BoreHead, 50, 20);
        units = new[]
        {
            Inf(sim, 10, 12), Inf(sim, 11, 12), Inf(sim, 12, 12), Inf(sim, 12, 11), Inf(sim, 12, 10),
            Veh(sim, Crawler, 21, 33), Veh(sim, Crawler, 24, 31),
        };
        var cmds = units.Take(5).Select(u => Enter(sim, u, b)).Concat(units.Skip(5).Select(u => Enter(sim, u, bore2))).ToArray();
        Step(sim, cmds);
        return sim;
    }

    [Fact]
    public void SaveLoad_MidTransit_KeepsTheStateHash_AndPlaysOnIdentically()
    {
        var sim = BusyNetwork(out ActorRef[] units);
        for (int n = 0; n < 70; n++) Step(sim);
        var layers = units.Select(u => Layer(sim, u)).ToHashSet();
        Assert.Contains(ActorLayer.Queued, layers);
        Assert.Contains(ActorLayer.Tunnel, layers);
        Assert.Contains(ActorLayer.Surfacing, layers);
        Assert.NotEmpty(sim.Tunnels.Queue);
        Assert.NotEmpty(sim.Tunnels.Telegraphs);

        var ms = new MemoryStream();
        sim.Save(ms);
        Simulation loaded = Simulation.Load(new MemoryStream(ms.ToArray()), sim.Setup);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        for (int n = 0; n < 250; n++)
        {
            Step(sim);
            Step(loaded);
            Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
        }
        Assert.All(units, u => Assert.Equal(ActorLayer.Surface, Layer(sim, u)));
    }

    [Fact]
    public void TwoRuns_GiveTheSameHashes()
    {
        var a = BusyNetwork(out _);
        var b = BusyNetwork(out _);
        for (int n = 0; n < 300; n++)
        {
            Step(a);
            Step(b);
            Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        }
        Assert.Empty(a.Tunnels.Transit);
    }
}
