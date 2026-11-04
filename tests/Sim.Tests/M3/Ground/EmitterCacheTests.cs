// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using static Rts.Sim.Tests.M3.Ground.GroundFixture;
using CF = Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.M3.Ground;

/// <summary>M3-INT: the emitter list behind IsJammedFor / IsSeismicRevealed gives the same answers as a full actor scan.</summary>
public sealed class EmitterCacheTests
{
    private static void AssertSame(Simulation sim, ref int trues)
    {
        for (byte viewer = 0; viewer < 2; viewer++)
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    var c = new CellCoord(x, y);
                    sim.UseEmitterCache = true;
                    bool j1 = sim.IsJammedFor(viewer, c), s1 = sim.IsSeismicRevealed(viewer, c);
                    sim.UseEmitterCache = false;
                    bool j2 = sim.IsJammedFor(viewer, c), s2 = sim.IsSeismicRevealed(viewer, c);
                    Assert.Equal(j2, j1);
                    Assert.Equal(s2, s1);
                    if (j1) trues++;
                    if (s1) trues++;
                }
        sim.UseEmitterCache = true;
    }

    [Fact]
    public void CachedAndScannedResults_MatchOverAScriptedGame()
    {
        var sim = Fresh();
        EntityId jam0 = sim.SpawnVehicle(0, Veh("g_jam"), new CellCoord(8, 8));
        sim.SpawnVehicle(1, Veh("g_jam"), new CellCoord(24, 24));
        sim.PlaceBuilding(0, Bld("g_seismo"), new CellCoord(5, 20));
        Ready(sim);
        int trues = 0;
        for (int t = 0; t < 60; t++)
        {
            if (t == 5) Spawn(sim, 1, false, Veh("g_jam"), 20, 8);   // a new emitter appears mid-game
            if (t == 10) Spawn(sim, 1, true, Inf("g_foot"), 12, 12);
            if (t == 3)   // an emitter walks
                sim.Step(new[] { new Command(sim.Tick, 0, 901, CommandKind.Move, jam0, default, new CellCoord(8, 20), (int)ActorKind.Vehicle) });
            else Steps(sim, 1);
            if (t % 4 == 0) AssertSame(sim, ref trues);
            if (t == 30)   // the emitter dies
                sim.Step(new[] { new Command(sim.Tick, 0, 902, CommandKind.Debug, jam0, default, default, (int)DebugOp.Kill, (int)ActorKind.Vehicle) });
            if (t == 32) AssertSame(sim, ref trues);
        }
        Assert.True(trues > 0, "the script never exercised a true result");
    }
}
