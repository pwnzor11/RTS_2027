// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Production;

/// <summary>Helpers for production tests: fixture rules, a flat map, setup-time bases, command builders.</summary>
internal static class ProductionFixture
{
    public static readonly RuleSet Loaded = RulesLoader.Load(TestData.RulesDir);
    public static readonly Command[] None = Array.Empty<Command>();

    public static int Veh(string id) => SimRules.IndexOf(Loaded.Sim.Vehicles, id, v => v.Id);
    public static int Inf(string id) => SimRules.IndexOf(Loaded.Sim.Infantry, id, v => v.Id);
    public static int Bld(string id) => SimRules.IndexOf(Loaded.Sim.Buildings, id, v => v.Id);

    /// <summary>A sim on the fixture rules (AllowDebug off, so Place needs a built item) with the given setup buildings for
    /// player 0 and a synced power total. Cash starts at 0 for everyone.</summary>
    public static Simulation NewSim(IEnumerable<(string Id, int X, int Y)> player0, byte[]? teams = null, int players = 2,
        IEnumerable<(byte Player, string Id, int X, int Y)>? others = null, int size = 32, bool debug = false)
    {
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(size, size), players, Loaded.Sim, Loaded.RulesHash)
        { Teams = teams, AllowDebug = debug });
        foreach (var (id, x, y) in player0)
            if (sim.PlaceBuilding(0, Bld(id), new CellCoord(x, y)).IsNone) throw new InvalidOperationException("setup place " + id);
        if (others != null)
            foreach (var (p, id, x, y) in others)
                if (sim.PlaceBuilding(p, Bld(id), new CellCoord(x, y)).IsNone) throw new InvalidOperationException("setup place " + id);
        SyncPower(sim);
        return sim;
    }

    /// <summary>The usual base: HQ (4,4), power plant (12,4), barracks (8,4), factory (4,8). Power is plentiful.</summary>
    public static Simulation Base(int cash = 10000, int extraBarracks = 0, int extraFactories = 0)
    {
        var list = new List<(string, int, int)>
        { ("test_hq", 4, 4), ("test_power", 12, 4), ("test_barracks", 8, 4), ("test_factory", 4, 8) };
        for (int i = 0; i < extraBarracks; i++) list.Add(("test_barracks", 8 + 3 * i, 12));
        for (int i = 0; i < extraFactories; i++) list.Add(("test_factory", 16, 8 + 4 * i));
        var sim = NewSim(list);
        sim.Players.Cash[0] = cash;
        return sim;
    }

    /// <summary>Set PowerOut/PowerUse from the player's buildings (what the economy part computes), so tests do not depend on it.</summary>
    public static void SyncPower(Simulation sim)
    {
        var out_ = new int[sim.Players.Count]; var use = new int[sim.Players.Count];
        for (int i = 1; i < sim.Buildings.Capacity; i++)
        {
            if (!sim.Buildings.Slots.IsIndexAlive(i)) continue;
            int pw = Loaded.Sim.Buildings[sim.Buildings.TypeIndex[i]].Power;
            if (pw > 0) out_[sim.Buildings.Player[i]] += pw; else use[sim.Buildings.Player[i]] -= pw;
        }
        for (int p = 0; p < sim.Players.Count; p++) { sim.Players.PowerOut[p] = out_[p]; sim.Players.PowerUse[p] = use[p]; }
    }

    private static ushort _seq;

    public static Command Build(Simulation sim, ProductionCategory cat, int type, byte player = 0) =>
        new(sim.Tick, player, _seq++, CommandKind.Build, default, default, default, (int)cat, type);

    public static Command Cancel(Simulation sim, ProductionCategory cat, int position, byte player = 0) =>
        new(sim.Tick, player, _seq++, CommandKind.Cancel, default, default, default, (int)cat, position);

    public static Command Place(Simulation sim, int type, int x, int y, byte player = 0) =>
        new(sim.Tick, player, _seq++, CommandKind.Place, default, default, new CellCoord(x, y), type);

    public static Command Rally(Simulation sim, EntityId factory, int x, int y, byte player = 0) =>
        new(sim.Tick, player, _seq++, CommandKind.Rally, factory, default, new CellCoord(x, y), 0);

    public static Command Sell(Simulation sim, EntityId b, byte player = 0) =>
        new(sim.Tick, player, _seq++, CommandKind.Sell, b, default, default, (int)ActorKind.Building);

    public static Command Repair(Simulation sim, EntityId b, byte player = 0) =>
        new(sim.Tick, player, _seq++, CommandKind.Repair, b, default, default, (int)ActorKind.Building);

    public static void Step(Simulation sim, params Command[] cmds) => sim.Step(cmds);

    public static void Run(Simulation sim, int ticks) { for (int i = 0; i < ticks; i++) sim.Step(None); }

    /// <summary>Steps until the predicate is true (at most max ticks); returns the number of steps taken.</summary>
    public static int RunUntil(Simulation sim, Func<bool> done, int max = 2000)
    {
        for (int i = 1; i <= max; i++) { sim.Step(None); if (done()) return i; }
        throw new InvalidOperationException("condition not reached");
    }

    public static EntityId BuildingOf(Simulation sim, int x, int y) => sim.Map.BuildingAt(new CellCoord(x, y));
}
