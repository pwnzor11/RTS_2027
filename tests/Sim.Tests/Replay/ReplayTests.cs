using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Replay;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.Replay;

public class ReplayTests
{
    private static byte[] Record(SimSetup setup, int ticks)
    {
        var script = ScriptedGame.Script(ticks);
        var rec = new ReplayWriter(setup);
        foreach (var s in ScriptedGame.Spawns()) rec.RecordSpawn(s.Player, s.Type, s.Cell);
        foreach (var c in script) rec.RecordCommand(c);
        var sim = ScriptedGame.NewSim(setup);
        for (int i = 0; i < ticks; i++)
        {
            sim.Step(script);
            if (sim.Tick % SimConstants.HashIntervalTicks == 0) rec.RecordHash(sim.Tick, sim.ComputeStateHash());
        }
        var ms = new MemoryStream();
        rec.Finish(ms, sim.Tick);
        return ms.ToArray();
    }

    [Fact]
    public void RecordedGame_PlaysBackWithoutMismatch()
    {
        var setup = ScriptedGame.Setup();
        byte[] bytes = Record(setup, 500);
        var result = ReplayPlayer.Run(new MemoryStream(bytes), setup);
        Assert.Equal(-1, result.FirstMismatchTick);
        Assert.Equal(500, result.TicksRun);

        var direct = ScriptedGame.NewSim(setup);
        var script = ScriptedGame.Script(500);
        for (int i = 0; i < 500; i++) direct.Step(script);
        Assert.Equal(direct.ComputeStateHash(), result.FinalHash);
    }

    [Fact]
    public void TamperedCommandByte_ReportsMismatchTick()
    {
        var setup = ScriptedGame.Setup();
        byte[] bytes = Record(setup, 500);
        var data = ReplayReader.Read(new MemoryStream(bytes));
        var rec = data.Records.First(r => r.Type == ReplayWriter.TypeCommand && r.Command.Kind == CommandKind.Move);
        int cellXOffset = (int)rec.Offset + 1 + 4 + 1 + 2 + 1 + 8;
        bytes[cellXOffset] ^= 0x07;
        var result = ReplayPlayer.Run(new MemoryStream(bytes), setup);
        Assert.True(result.FirstMismatchTick > 0);
        Assert.True(result.FirstMismatchTick >= rec.Command.ExecTick);
        Assert.Equal(0, result.FirstMismatchTick % 16);
    }

    [Fact]
    public void WrongMapHash_Throws()
    {
        var setup = ScriptedGame.Setup();
        byte[] bytes = Record(setup, 32);
        var other = setup with { Map = MapData.CreateFlat(64, 64, "other") };
        Assert.Throws<InvalidDataException>(() => ReplayPlayer.Run(new MemoryStream(bytes), other));
    }

    [Fact]
    public void BadMagic_Throws()
    {
        var setup = ScriptedGame.Setup();
        byte[] bytes = Record(setup, 32);
        bytes[0] = 0;
        Assert.Throws<InvalidDataException>(() => ReplayPlayer.Run(new MemoryStream(bytes), setup));
    }
}
