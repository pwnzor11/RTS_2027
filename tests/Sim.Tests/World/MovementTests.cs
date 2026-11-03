using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Sim.Tests.World;

public class MovementTests
{
    private static Simulation Make(int speed, out EntityId id)
    {
        var setup = new SimSetup(1, 32, 32, 1, new[] { new UnitTypeDef("t", speed, 10) }, 1);
        var sim = new Simulation(setup);
        id = sim.SpawnUnit(0, 0, new CellCoord(10, 10));
        return sim;
    }

    [Fact]
    public void Unit_ReachesDestination_InExactTickCount()
    {
        var sim = Make(30, out var id);
        // 5 cells east = 1280 leptons at 30/tick -> ceil(1280/30) = 43 ticks
        var cmd = new Command(0, 0, 0, CommandKind.Move, id, default, new CellCoord(15, 10), 0);
        sim.Step(new[] { cmd });
        int ticks = 1;
        while (sim.Units.Mission[id.Index] == UnitMission.Move) { sim.Step(Array.Empty<Command>()); ticks++; }
        Assert.Equal(43, ticks);
        Assert.Equal(LeptonCoord.CenterOf(new CellCoord(15, 10)), sim.Units.Position[id.Index]);
        Assert.Equal(UnitMission.Idle, sim.Units.Mission[id.Index]);
    }

    [Theory]
    [InlineData(10, 5, 0)] [InlineData(15, 5, 32)] [InlineData(15, 10, 64)] [InlineData(15, 15, 96)]
    [InlineData(10, 15, 128)] [InlineData(5, 15, 160)] [InlineData(5, 10, 192)] [InlineData(5, 5, 224)]
    public void Facing_MatchesEightDirections(int cx, int cy, int expected)
    {
        var sim = Make(20, out var id);
        sim.Step(new[] { new Command(0, 0, 0, CommandKind.Move, id, default, new CellCoord(cx, cy), 0) });
        Assert.Equal(expected, sim.Units.Facing[id.Index]);
    }

    [Fact]
    public void ForeignCommand_IsIgnored()
    {
        var sim = Make(20, out var id);
        sim.Step(new[] { new Command(0, 1, 0, CommandKind.Move, id, default, new CellCoord(15, 10), 0) });
        Assert.Equal(UnitMission.Idle, sim.Units.Mission[id.Index]);
    }
}
