// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Xunit.Abstractions;

namespace Rts.Sim.Tests.Balance;

/// <summary>
/// M3-14 balance measurement (design 05 section 1): how much travel time the tunnel network saves on a typical
/// base-to-front route, with the shipped <c>data/rules</c> numbers. Route: 40 cells along a flat map; one entrance 3 cells
/// from the start and one 5 cells from the target. A Directorate infantry squad (4 conscripts, size S, via Hatches) and an
/// M vehicle (rocket truck, via Bore Heads) each travel walking and via the two entrances. The band pins the design target
/// (30-50 percent saved); change the tunnel numbers (<c>[tunnels] transit_ticks</c>, entrance <c>ticks_per_point</c>), not the band.
/// </summary>
public sealed class TunnelTimeTests
{
    private const int MinSavedPercent = 30, MaxSavedPercent = 50;
    private const int StartX = 12, TargetX = 52, Y = 48;   // 40 cells apart
    private const int SquadSize = 4;

    private readonly ITestOutputHelper _out;
    public TunnelTimeTests(ITestOutputHelper output) => _out = output;

    private static SimRules LoadReal()
    {
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        return RulesLoader.Load(dir!).Sim;
    }

    private static ushort _seq;

    /// <summary>Ticks until every unit stands within one cell of the target; <paramref name="viaTunnel"/> sends them through the entrances first.</summary>
    private static int Travel(SimRules rules, bool infantry, bool viaTunnel)
    {
        string entranceId = infantry ? "dir_hatch" : "dir_bore_head";
        int entrance = SimRules.IndexOf(rules.Buildings, entranceId, b => b.Id);
        Assert.True(entrance >= 0);
        var sim = new Simulation(new SimSetup(7, MapData.CreateFlat(96, 96), 2, rules, 0x7E57UL) { AllowDebug = true });
        Assert.False(sim.PlaceBuilding(0, entrance, new CellCoord(StartX + 3, Y - 1)).IsNone);
        EntityId exit = sim.PlaceBuilding(0, entrance, new CellCoord(TargetX - 5, Y - 1));
        Assert.False(exit.IsNone);
        Assert.False(sim.PlaceBuilding(1, entrance, new CellCoord(80, 10)).IsNone);   // keeps player 1 alive

        var units = new List<ActorRef>();
        if (infantry)
        {
            int t = SimRules.IndexOf(rules.Infantry!, "dir_conscript", i => i.Id);
            Assert.True(t >= 0);
            for (int k = 0; k < SquadSize; k++)
                units.Add(new ActorRef(ActorKind.Infantry, sim.SpawnInfantry(0, t, new CellCoord(StartX, Y - 1 + k / 2), k % 2)));
        }
        else
        {
            int t = SimRules.IndexOf(rules.Vehicles, "dir_rocket_truck", v => v.Id);
            Assert.True(t >= 0);
            units.Add(new ActorRef(ActorKind.Vehicle, sim.SpawnVehicle(0, t, new CellCoord(StartX, Y))));
        }

        var target = new CellCoord(TargetX, Y);
        Command Move(ActorRef u) => new(sim.Tick, 0, _seq++, CommandKind.Move, u.Id, default, target, (int)u.Kind);
        Command Enter(ActorRef u) => new(sim.Tick, 0, _seq++, CommandKind.Enter, u.Id, exit, default, (int)u.Kind | ((int)ActorKind.Building << 8));

        sim.Step(units.Select(u => viaTunnel ? Enter(u) : Move(u)).ToArray());
        var underground = new HashSet<ActorRef>();
        var ordered = new HashSet<ActorRef>();
        for (int n = 0; n < 4000; n++)
        {
            var cmds = new List<Command>();
            foreach (ActorRef u in units)
            {
                ActorLayer layer = sim.LayerOf(u.Kind, u.Id.Index);
                if (layer != ActorLayer.Surface) underground.Add(u);
                else if (viaTunnel && underground.Contains(u) && ordered.Add(u)) cmds.Add(Move(u));   // surfaced: walk on
            }
            sim.Step(cmds.ToArray());
            if (units.All(u => Near(sim, u, target))) return sim.Tick;
        }
        throw new Xunit.Sdk.XunitException("the units never reached the target");
    }

    private static bool Near(Simulation sim, ActorRef u, CellCoord target)
    {
        LeptonCoord p = u.Kind == ActorKind.Vehicle ? sim.Units.Position[u.Id.Index] : sim.Infantry.Position[u.Id.Index];
        // Units that have not yet entered the network stand near the start, far from the target, so this cannot fire early.
        return Math.Abs(p.X - (target.X << SimConstants.LeptonShift)) <= 384 && Math.Abs(p.Y - (target.Y << SimConstants.LeptonShift)) <= 384
            && sim.LayerOf(u.Kind, u.Id.Index) == ActorLayer.Surface;
    }

    private int SavedPercent(string label, bool infantry)
    {
        SimRules rules = LoadReal();
        int walk = Travel(rules, infantry, viaTunnel: false);
        int tunnel = Travel(rules, infantry, viaTunnel: true);
        int saved = (walk - tunnel) * 100 / walk;
        _out.WriteLine($"{label}: walking {walk} ticks, via tunnel {tunnel} ticks, saved {saved} percent");
        return saved;
    }

    [Fact]
    public void InfantrySquad_SavesThirtyToFiftyPercent_ViaTwoHatches()
    {
        int saved = SavedPercent("infantry squad of " + SquadSize, infantry: true);
        Assert.InRange(saved, MinSavedPercent, MaxSavedPercent);
    }

    [Fact]
    public void MVehicle_SavesThirtyToFiftyPercent_ViaTwoBoreHeads()
    {
        int saved = SavedPercent("M vehicle", infantry: false);
        Assert.InRange(saved, MinSavedPercent, MaxSavedPercent);
    }
}
