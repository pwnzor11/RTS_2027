using Rts.Data;
using Rts.AI;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.View;

namespace AI.Tests;

public class AiDriverTests
{
    private sealed class Recorder : IAiPlayer
    {
        public readonly List<int> ThinkTicks = new();
        public string Id => "recorder";
        public AiContext? Context;
        public void Start(AiContext ctx) => Context = ctx;
        public void Think(PlayerView view, ICommandSink sink)
        {
            ThinkTicks.Add(view.Tick);
            sink.Issue(CommandKind.Move, new EntityId(1, 1), ActorKind.Infantry, new CellCoord(3, 4));
            sink.Issue(CommandKind.Attack, new EntityId(1, 1), ActorKind.Vehicle, default, new ActorRef(ActorKind.Building, new EntityId(9, 2)));
        }
    }

    private static DifficultyProfile Profile(int interval, int delay) =>
        DifficultyProfile.Default with { DecisionIntervalTicks = interval, CommandDelayTicks = delay };

    [Fact]
    public void StampsExecTickSequenceAndArgs()
    {
        var sim = TestWorld.FlatSim();
        sim.SpawnVehicle(0, TestWorld.Vehicle("test_tank"), new CellCoord(4, 6));
        var ai = new Recorder();
        var driver = new AiDriver(77);
        driver.Add(0, ai, Profile(interval: 1, delay: 3));
        var cmds = new List<Command>();
        driver.Collect(sim, cmds);
        sim.Step(Array.Empty<Command>());
        driver.Collect(sim, cmds);

        Assert.Equal(4, cmds.Count);
        Assert.Equal([3, 3, 4, 4], cmds.Select(c => c.ExecTick));         // tick + delay
        Assert.Equal([0, 1, 2, 3], cmds.Select(c => (int)c.Sequence));
        Assert.All(cmds, c => Assert.Equal(0, c.Player));
        Assert.Equal((int)ActorKind.Infantry, cmds[0].Arg);                 // Move: subject kind
        Assert.Equal(new CellCoord(3, 4), cmds[0].Cell);
        Assert.Equal((int)ActorKind.Vehicle | ((int)ActorKind.Building << 8), cmds[1].Arg);
        Assert.Equal(new EntityId(9, 2), cmds[1].Target);
        Assert.Equal(new CellCoord(4, 6), ai.Context!.StartCell);
    }

    [Fact]
    public void RespectsIntervalAndStaggersBySlot()
    {
        var sim = TestWorld.FlatSim();
        var a0 = new Recorder();
        var a1 = new Recorder();
        var driver = new AiDriver(1);
        driver.Add(1, a1, Profile(10, 1));
        driver.Add(0, a0, Profile(10, 1));
        var cmds = new List<Command>();
        for (int t = 0; t < 30; t++)
        {
            driver.Collect(sim, cmds);
            sim.Step(Array.Empty<Command>());
        }
        Assert.Equal([0, 10, 20], a0.ThinkTicks);
        Assert.Equal([9, 19, 29], a1.ThinkTicks);   // (tick + slot) % interval == 0
    }

    [Fact]
    public void DelayOverrideReplacesProfileAndNeverDropsBelowOne()
    {
        var sim = TestWorld.FlatSim();
        var driver = new AiDriver(1) { CommandDelayOverride = 0 };
        driver.Add(0, new Recorder(), Profile(5, 9));
        var cmds = new List<Command>();
        driver.Collect(sim, cmds);
        Assert.All(cmds, c => Assert.Equal(1, c.ExecTick));
    }

    [Fact]
    public void AiRandomIsReproducibleAndSlotDependent()
    {
        var a = new AiRandom(5, 0); var b = new AiRandom(5, 0); var c = new AiRandom(5, 1);
        Assert.Equal(a.NextULong(), b.NextULong());
        Assert.NotEqual(new AiRandom(5, 0).NextULong(), c.NextULong());
    }
}
