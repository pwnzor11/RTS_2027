using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.Missions;

namespace Rts.Sim.Tests.World;

public class MovementTests
{
    private static Simulation Make(int speed, out EntityId id)
    {
        var setup = new SimSetup(1, MapData.CreateFlat(32, 32), 1, ScriptedGame.Rules(ScriptedGame.Vehicle("t", speed, 10)), 1);
        var sim = new Simulation(setup);
        id = sim.SpawnVehicle(0, 0, new CellCoord(10, 10));
        return sim;
    }

    [Fact]
    public void Unit_FacingDestination_ReachesItInExactTickCount()
    {
        var sim = Make(30, out var id);
        // 5 cells north, path-following: tick 0 asks for the path, then 5 cell steps of ceil(256/30) = 9 ticks each (a step ends on
        // the cell centre); already facing north, no turn -> 1 + 45 = 46 ticks
        var cmd = new Command(0, 0, 0, CommandKind.Move, id, default, new CellCoord(10, 5), 0);
        sim.Step(new[] { cmd });
        int ticks = 1;
        while (sim.Units.Position[id.Index] != LeptonCoord.CenterOf(new CellCoord(10, 5))) { sim.Step(Array.Empty<Command>()); ticks++; }
        Assert.Equal(46, ticks);
        Assert.Equal(MissionKind.Move, sim.Units.Mission[id.Index]);   // Guard is queued, commences next tick
        sim.Step(Array.Empty<Command>());
        Assert.Equal(MissionKind.Guard, sim.Units.Mission[id.Index]);
    }

    [Theory]
    [InlineData(10, 5, 0)] [InlineData(15, 5, 32)] [InlineData(15, 10, 64)] [InlineData(15, 15, 96)]
    [InlineData(10, 15, 128)] [InlineData(5, 15, 160)] [InlineData(5, 10, 192)] [InlineData(5, 5, 224)]
    public void Facing_MatchesEightDirections(int cx, int cy, int expected)
    {
        var sim = Make(20, out var id);
        sim.Step(new[] { new Command(0, 0, 0, CommandKind.Move, id, default, new CellCoord(cx, cy), 0) });
        for (int i = 0; i < 40; i++) sim.Step(Array.Empty<Command>());
        Assert.Equal(expected, sim.Units.Facing[id.Index]);
    }

    [Fact]
    public void ForeignCommand_IsIgnored()
    {
        var sim = Make(20, out var id);
        sim.Step(new[] { new Command(0, 1, 0, CommandKind.Move, id, default, new CellCoord(15, 10), 0) });
        Assert.Equal(MissionKind.None, sim.Units.Mission[id.Index]);
    }
}
