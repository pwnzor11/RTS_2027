// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Llm;
using Rts.Sim.Core;

namespace Llm.Tests;

public class SummaryFogTests
{
    private static readonly int Tank = SummaryRig.Vehicle("auth_tank");
    private static readonly int Conscript = SummaryRig.Infantry("dir_conscript");
    private static readonly int Shaft = SummaryRig.Building("dir_shaft");

    [Fact]
    public void UnseenEnemyIsNotMentionedAndSeenEnemyIs()
    {
        var sim = SummaryRig.Sim();
        sim.SpawnVehicle(0, Tank, new CellCoord(5, 5));
        sim.SpawnInfantry(1, Conscript, new CellCoord(55, 57));
        sim.PlaceBuilding(1, Shaft, new CellCoord(50, 50));
        SummaryRig.Run(sim, 3);
        var h = new SummaryHistory();
        h.Observe(sim.ViewFor(0));
        string text = BattlefieldSummary.Build(sim.ViewFor(0), h);
        Assert.Contains("PLAYER 1 enemy, faction unknown: no contact", text);
        foreach (string leak in new[] { "dir_", "Directorate", "55", "57", "50,50", "conscript", "shaft" })
            Assert.DoesNotContain(leak, text, StringComparison.OrdinalIgnoreCase);

        SummaryRig.LateInfantry(sim, 1, Conscript, new CellCoord(7, 5));
        SummaryRig.Run(sim, 2);
        h.Observe(sim.ViewFor(0));
        text = BattlefieldSummary.Build(sim.ViewFor(0), h);
        Assert.Contains("dir_conscript x1", text);
        Assert.Contains("faction Directorate", text);
        Assert.DoesNotContain("shaft", text);
    }

    [Fact]
    public void RememberedBuildingShowsLastSeenAgeEvenAfterItIsDestroyed()
    {
        var sim = SummaryRig.Sim();
        EntityId spotter = sim.SpawnVehicle(0, Tank, new CellCoord(45, 50));
        sim.PlaceBuilding(1, Shaft, new CellCoord(48, 50));
        SummaryRig.Run(sim, 3);
        var h = new SummaryHistory();
        h.Observe(sim.ViewFor(0));
        string text = BattlefieldSummary.Build(sim.ViewFor(0), h);
        Assert.Contains("dir_shaft x1 (in view)", text);

        // the spotter dies, then the building is destroyed out of sight; the view still remembers the ghost
        SummaryRig.Kill(sim, 0, spotter, ActorKind.Vehicle);
        SummaryRig.Run(sim, 20 * 40);
        h.Observe(sim.ViewFor(0));
        text = BattlefieldSummary.Build(sim.ViewFor(0), h);
        Assert.Contains("dir_shaft x1 (last seen 4", text);   // 40 s and a few ticks ago
        Assert.Contains("ago)", text);
        Assert.Contains("1xauth_tank", text);                 // the spotter is listed as a loss
    }
}
