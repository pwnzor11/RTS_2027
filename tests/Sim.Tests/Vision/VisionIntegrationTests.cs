using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Vision;

/// <summary>Vision wired into Simulation (lead integration of M1-07): per-viewer snapshots and building ghosts.</summary>
public class VisionIntegrationTests
{
    private static readonly RuleSet Rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!);

    private static Simulation NewSim() =>
        new(new SimSetup(1, MapData.CreateFlat(32, 32), 2, Rules.Sim, Rules.RulesHash));

    private static int Vehicle(string id) => SimRules.IndexOf(Rules.Sim.Vehicles, id, v => v.Id);
    private static int Building(string id) => SimRules.IndexOf(Rules.Sim.Buildings, id, b => b.Id);

    private static void StepN(Simulation sim, int n, params Command[] cmds)
    {
        for (int i = 0; i < n; i++) sim.Step(cmds);
    }

    [Fact]
    public void OwnUnitsAreSeen_EnemiesOutsideSightAreHidden()
    {
        var sim = NewSim();
        sim.SpawnVehicle(0, Vehicle("test_tank"), new CellCoord(3, 3));
        sim.SpawnVehicle(1, Vehicle("test_tank"), new CellCoord(28, 28));
        StepN(sim, 1);

        SimSnapshot view0 = sim.Snapshot(0);
        Assert.Equal(0, view0.Viewer);
        Assert.Equal(32 * 32, view0.Vision.Count);
        Assert.Equal(2, view0.Vision[3 * 32 + 3]);   // own cell visible
        Assert.Equal(0, view0.Vision[28 * 32 + 28]); // enemy corner never seen
        Assert.Single(view0.Units);
        Assert.Equal(0, view0.Units[0].Player);

        Assert.Equal(2, sim.Snapshot().Units.Count); // omniscient view still has both
        Assert.Empty(sim.Snapshot().Vision);
    }

    [Fact]
    public void EnemyBuilding_LeavesGhostAfterViewerLeaves_EvenWhenSold()
    {
        var sim = NewSim();
        EntityId tank = sim.SpawnVehicle(0, Vehicle("test_tank"), new CellCoord(17, 17));
        EntityId barracks = sim.PlaceBuilding(1, Building("test_barracks"), new CellCoord(20, 20));
        Assert.False(barracks.IsNone);
        StepN(sim, 1);

        BuildingView live = Assert.Single(sim.Snapshot(0).Buildings);
        Assert.False(live.Ghost);

        // Drive the tank far away (straight-line move until wave C adds paths).
        var move = new Command(sim.Tick, 0, 0, CommandKind.Move, tank, EntityId.None, new CellCoord(2, 2), (int)ActorKind.Vehicle);
        sim.Step(new[] { move });
        StepN(sim, 400);
        Assert.False(sim.Vision.IsVisible(0, new CellCoord(20, 20)));

        BuildingView ghost = Assert.Single(sim.Snapshot(0).Buildings);
        Assert.True(ghost.Ghost);
        Assert.Equal(new CellCoord(20, 20), ghost.Cell);

        // Player 1 sells it out of sight: the ghost stays until player 0 looks again.
        var sell = new Command(sim.Tick, 1, 0, CommandKind.Sell, barracks, EntityId.None, default, (int)ActorKind.Building);
        sim.Step(new[] { sell });
        Assert.Empty(sim.Snapshot(1).Buildings);
        Assert.True(Assert.Single(sim.Snapshot(0).Buildings).Ghost);
    }

    [Fact]
    public void VisionIsPartOfTheHash()
    {
        var a = NewSim();
        var b = NewSim();
        a.SpawnVehicle(0, Vehicle("test_tank"), new CellCoord(3, 3));
        b.SpawnVehicle(0, Vehicle("test_tank"), new CellCoord(3, 3));
        StepN(a, 1);
        StepN(b, 1);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        b.Vision.ExploreAll(0);
        Assert.NotEqual(a.ComputeStateHash(), b.ComputeStateHash());
    }
}
