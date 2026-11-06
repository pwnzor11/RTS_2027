// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.View;

namespace AI.Tests;

public class PlayerViewTests
{
    private static readonly int Tank = TestWorld.Vehicle("auth_tank");

    [Fact]
    public void HidesEnemyInFogAndShowsItInVision()
    {
        var sim = TestWorld.FlatSim();
        sim.SpawnVehicle(0, Tank, new CellCoord(5, 5));
        EntityId far = sim.SpawnVehicle(1, Tank, new CellCoord(55, 55));
        var seen = new List<ViewActor>();

        sim.Step(Array.Empty<Command>());
        sim.ViewFor(0).CopyVisible(seen);
        Assert.Empty(seen);

        EntityId near = TestWorld.LateVehicle(sim, 1, Tank, new CellCoord(7, 5));
        sim.Step(Array.Empty<Command>());
        sim.ViewFor(0).CopyVisible(seen);
        var actor = Assert.Single(seen);
        Assert.Equal(near, actor.Id);
        Assert.Equal(Relation.Enemy, actor.Relation);
        Assert.NotEqual(far, actor.Id);
        Assert.Equal(Rts.Sim.Missions.MissionKind.None, actor.Mission);   // enemy orders are hidden

        // the other player sees the same pair from its side, and never the far unit it owns twice over
        sim.ViewFor(1).CopyVisible(seen);
        Assert.Single(seen);
    }

    [Fact]
    public void LandingMarkersAndTheConvoyEdgeAreReadOnlyMapData()
    {
        var (map, meta) = AI.Tests.M7.ExpeditionRig.OceanMap();
        var sim = new Rts.Sim.Simulation(new Rts.Sim.Core.SimSetup(1, map, 2, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash) { LandingMarkers = meta.Landing });
        PlayerView v = sim.ViewFor(0);
        Assert.NotEmpty(meta.Landing);
        Assert.Equal(meta.Landing, v.LandingMarkers);
        Assert.Empty(TestWorld.FlatSim().ViewFor(0).LandingMarkers);   // a host without map metadata gives none
        var edge = new List<CellCoord>();
        v.CopyConvoyEdge(edge);
        Assert.NotEmpty(edge);
        foreach (CellCoord c in edge)
        {
            Assert.True(c.X == 0 || c.Y == 0 || c.X == map.Width - 1 || c.Y == map.Height - 1);
            Assert.True(v.NavalRegionAt(c) > 0);
        }
        foreach (CellCoord c in meta.ConvoyEdge) Assert.Contains(c, edge);   // every generated convoy cell is on the derived edge
    }

    [Fact]
    public void OwnActorsCashAndMapQueries()
    {
        var sim = TestWorld.FlatSim();
        EntityId mine = sim.SpawnVehicle(0, Tank, new CellCoord(5, 5));
        sim.SpawnVehicle(1, Tank, new CellCoord(7, 5));
        sim.Players.Cash[0] = 1234;
        PlayerView v = sim.ViewFor(0);
        var own = new List<ViewActor>();
        v.CopyOwn(own);
        var a = Assert.Single(own);
        Assert.Equal(mine, a.Id);
        Assert.Equal(ActorKind.Vehicle, a.Kind);
        Assert.Equal(Relation.Self, a.Relation);
        Assert.Equal(a.MaxHealth, a.Health);
        Assert.True(a.Idle);
        Assert.Equal(1234, v.Cash);
        Assert.Equal(64, v.MapWidth);
        Assert.True(v.InMap(new CellCoord(0, 0)));
        Assert.False(v.InMap(new CellCoord(64, 0)));
        Assert.True(v.IsPassable(new CellCoord(10, 10), Rts.Sim.Rules.Locomotion.Tracked));
        Assert.Equal(Relation.Enemy, v.RelationOf(1));
        Assert.Equal(Relation.Self, v.RelationOf(0));
    }

    [Fact]
    public void KnownEnemyBuildingsComeFromVision()
    {
        var sim = TestWorld.FlatSim();
        sim.SpawnVehicle(0, Tank, new CellCoord(5, 5));
        int hq = Array.FindIndex(TestWorld.Rules.Sim.Buildings.ToArray(), b => b.Id == "auth_conyard");
        Assert.NotEqual(EntityId.None, sim.PlaceBuilding(1, hq, new CellCoord(50, 50)));
        var known = new List<ViewActor>();
        sim.Step(Array.Empty<Command>());
        sim.ViewFor(0).CopyKnownEnemyBuildings(known);
        Assert.Empty(known);

        Assert.NotEqual(EntityId.None, TestWorld.LateBuilding(sim, 1, hq, new CellCoord(9, 5)));
        sim.Step(Array.Empty<Command>());
        sim.ViewFor(0).CopyKnownEnemyBuildings(known);
        var b = Assert.Single(known);
        Assert.False(b.Ghost);
        Assert.Equal(ActorKind.Building, b.Kind);
    }
}
