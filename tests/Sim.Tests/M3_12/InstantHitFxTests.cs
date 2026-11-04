// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.M3_12;

/// <summary>M3-12: an instant (hitscan) weapon raises an impact effect event at its target, as a projectile raises a detonation.</summary>
public class InstantHitFxTests
{
    [Fact]
    public void A_rifle_hit_raises_one_instant_hit_at_the_victim_and_no_detonation()
    {
        var sim = NewSim();
        var shooter = sim.SpawnInfantry(0, Inf("test_rifleman"), new CellCoord(10, 10), 0);
        var victim = sim.SpawnInfantry(1, Inf("test_rifleman"), new CellCoord(10, 7), 0);
        sim.Step(new[] { Attack(sim, 0, ActorKind.Infantry, shooter, ActorKind.Infantry, victim) });

        int hits = 0, detonations = 0;
        for (int t = 0; t < 120; t++)
        {
            sim.Step(None);
            foreach (FxEvent e in sim.FxEvents)
            {
                if (e.Kind == FxKind.InstantHit)
                {
                    hits++;
                    CellCoord c = e.Pos.ToCell();
                    Assert.True(Math.Abs(c.X - 10) <= 1 && Math.Abs(c.Y - 7) <= 1, $"impact at {c}, victim at (10,7)");
                    Assert.Equal(1, e.Size);
                }
                if (e.Kind == FxKind.Detonation) detonations++;
            }
        }
        Assert.True(hits >= 1, "no instant-hit effect was raised");
        Assert.Equal(0, detonations);   // a rifle hit is not a shell: no detonation
    }
}
