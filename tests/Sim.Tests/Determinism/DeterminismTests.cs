using Rts.Sim.Core;

namespace Rts.Sim.Tests.Determinism;

public class DeterminismTests
{
    private static List<ulong> Run(SimSetup setup, List<Command> script, int ticks)
    {
        var sim = ScriptedGame.NewSim(setup);
        var hashes = new List<ulong>();
        for (int i = 0; i < ticks; i++) { sim.Step(script); hashes.Add(sim.ComputeStateHash()); }
        return hashes;
    }

    [Fact]
    public void SameSetupAndCommands_GiveIdenticalHashEveryTick()
    {
        var script = ScriptedGame.Script(500);
        var a = Run(ScriptedGame.Setup(), script, 500);
        var b = Run(ScriptedGame.Setup(), script, 500);
        Assert.Equal(a, b);
        Assert.True(a.Distinct().Count() > 100);
    }

    [Fact]
    public void ChangedSeed_ChangesFinalHash()
    {
        var script = ScriptedGame.Script(500);
        Assert.NotEqual(Run(ScriptedGame.Setup(1), script, 500)[^1], Run(ScriptedGame.Setup(2), script, 500)[^1]);
    }

    [Fact]
    public void ChangedCommand_ChangesFinalHash()
    {
        var script = ScriptedGame.Script(500);
        var tampered = new List<Command>(script);
        int i = tampered.FindIndex(c => c.Kind == CommandKind.Move && c.ExecTick < 100);
        tampered[i] = tampered[i] with { Cell = new CellCoord((tampered[i].Cell.X + 7) % 64, tampered[i].Cell.Y) };
        Assert.NotEqual(Run(ScriptedGame.Setup(), script, 500)[^1], Run(ScriptedGame.Setup(), tampered, 500)[^1]);
    }

    [Fact]
    public void DebugCommand_ConsumesRng_AndIsInHash()
    {
        var a = ScriptedGame.NewSim(ScriptedGame.Setup());
        var b = ScriptedGame.NewSim(ScriptedGame.Setup());
        a.Step(new[] { new Command(0, 0, 0, CommandKind.Debug, default, default, default, 1) });
        b.Step(Array.Empty<Command>());
        Assert.NotEqual(a.ComputeStateHash(), b.ComputeStateHash());
    }

    [Fact]
    public void RecentHashes_EveryIntervalAndCappedAt64()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        for (int i = 0; i < 16 * 70; i++) sim.Step(Array.Empty<Command>());
        Assert.Equal(64, sim.RecentHashes.Count);
        Assert.Equal(16 * 70, sim.RecentHashes[^1].Tick);
        Assert.Equal(16 * 7, sim.RecentHashes[0].Tick);
    }

    [Fact]
    public void CommandsAreAppliedInPlayerSequenceOrder_RegardlessOfListOrder()
    {
        var m1 = new Command(0, 1, 0, CommandKind.Move, new EntityId(11, 1), default, new CellCoord(3, 3), 0);
        var m0a = new Command(0, 0, 1, CommandKind.Move, new EntityId(1, 1), default, new CellCoord(9, 9), 0);
        var m0b = new Command(0, 0, 0, CommandKind.Move, new EntityId(1, 1), default, new CellCoord(5, 5), 0);
        var a = ScriptedGame.NewSim(ScriptedGame.Setup()); a.Step(new[] { m1, m0a, m0b });
        var b = ScriptedGame.NewSim(ScriptedGame.Setup()); b.Step(new[] { m0b, m0a, m1 });
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        Assert.Equal(LeptonCoord.CenterOf(new CellCoord(9, 9)), a.Units.Destination[1]);
    }
}
