// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Xunit.Abstractions;

namespace Rts.Sim.Tests.Balance;

/// <summary>
/// M4-16 scenario tests for the answers to a Directorate tunnel push (hatch plus burrowed units), shipped numbers. Goal: the answer
/// costs the Directorate real time but does not shut tunnels off entirely. The Seismic Mortar is the working answer (hits burrowed
/// units, double damage to entrances). Quick-Set Foam only displaces a break-through or surfacing to the nearest unfoamed cell (the
/// loader caps a foam shot at a 3x3 block), so it never shuts a tunnel off: pinned below; see docs/balance/m4_16_rules.md.
/// </summary>
public sealed class TunnelCounterBalanceTests
{
    private readonly ITestOutputHelper _out;
    public TunnelCounterBalanceTests(ITestOutputHelper output) => _out = output;

    private int TicksToKillHatch(string shooter, int count)
    {
        var a = new BalanceArena();
        EntityId hatch = a.Sim.PlaceBuilding(1, a.Building("dir_hatch"), new CellCoord(60, 48));
        var s = a.SpawnVehicles(0, shooter, count, new CellCoord(40, 48));
        a.Run(1, a.AttackBuilding(0, s, hatch));
        for (int t = 0; t < 4000; t++) { a.Sim.Step(Array.Empty<Command>()); if (!a.Alive(ActorKind.Building, hatch)) return t; }
        return -1;
    }

    private int TicksToKillBurrowed(string shooter, int count)
    {
        var a = new BalanceArena();
        var d = a.SpawnVehicles(1, "dir_delver", 1, new CellCoord(60, 48));
        var s = a.SpawnVehicles(0, shooter, count, new CellCoord(40, 48));
        a.Run(1, a.Cmd(1, CommandKind.Move, d[0].Id, default, new CellCoord(60, 60), (int)ActorKind.Vehicle));
        for (int t = 0; t < 200; t++) a.Sim.Step(Array.Empty<Command>());
        Assert.Equal(ActorLayer.Burrowed, a.Sim.LayerOf(ActorKind.Vehicle, d[0].Id.Index));
        a.Run(1, a.AttackMove(0, s, new CellCoord(60, 55)));
        for (int t = 0; t < 4000; t++) { a.Sim.Step(Array.Empty<Command>()); if (!a.Sim.TryResolve(d[0])) return t; }
        return -1;
    }

    [Fact]
    public void TwoSeismicMortars_TakeAHatchDownInAboutHalfAMinute()
    {
        int one = TicksToKillHatch("auth_seismic_mortar", 1), two = TicksToKillHatch("auth_seismic_mortar", 2);
        _out.WriteLine($"hatch: 1 mortar {one} ticks, 2 mortars {two} ticks");
        Assert.InRange(two, 300, 800);   // real time (15 s+), not instant
        Assert.InRange(one, two + 1, 1500);
    }

    [Fact]
    public void SeismicMortars_HitBurrowedUnits_ButItTakesTime_AndTanksCannot()
    {
        int one = TicksToKillBurrowed("auth_seismic_mortar", 1), two = TicksToKillBurrowed("auth_seismic_mortar", 2);
        int tank = TicksToKillBurrowed("auth_tank", 1);
        _out.WriteLine($"burrowed Delver: 1 mortar {one}, 2 mortars {two}, 1 tank {tank} ticks");
        Assert.InRange(two, 600, 1600);
        Assert.InRange(one, two + 1, 3000);
        Assert.Equal(-1, tank);   // a normal gun cannot touch a burrowed unit
    }

    [Fact]
    public void Foam_DisplacesABoreBreakThrough_ButDoesNotShutTheTunnelOff()
    {
        // The Bore is burrowed and arrives at (50,48) near tick 800. Foam over the arrival cell at tick 790 lasts 400 ticks.
        var a = new BalanceArena();
        var bore = a.SpawnVehicles(0, "dir_bore", 1, new CellCoord(20, 48));
        a.Run(1, a.Cmd(0, CommandKind.Deploy, bore[0].Id, default, new CellCoord(50, 48), (int)ActorKind.Vehicle));
        int wh = -1;
        for (int i = 0; i < a.Rules.Warheads.Count; i++) if (a.Rules.Warheads[i].Id == "auth_foam") wh = i;
        Assert.True(wh >= 0);
        int hatchAt = -1;
        CellCoord cell = default;
        for (int t = 0; t < 1500 && hatchAt < 0; t++)
        {
            if (t == 790) a.Sim.Detonate(1, 0, wh, LeptonCoord.CenterOf(new CellCoord(50, 48)), default, false);
            a.Sim.Step(Array.Empty<Command>());
            for (int b = 1; b < a.Sim.Buildings.Capacity && hatchAt < 0; b++)
                if (a.Sim.Buildings.Slots.IsIndexAlive(b) && a.Sim.Buildings.Player[b] == 0 && a.Rules.Buildings[a.Sim.Buildings.TypeIndex[b]].Id == "dir_hatch")
                { hatchAt = t; cell = a.Sim.Buildings.Cell[b]; }
        }
        _out.WriteLine($"hatch appeared at tick {hatchAt}, cell {cell}");
        Assert.True(hatchAt > 0, "foam must not shut the tunnel off");
        Assert.True(a.Sim.Ground.FoamAt(cell) == 0, "the hatch stands outside the foam");
    }
}
