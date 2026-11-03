using Rts.Sim.Core;
using Rts.Sim.Replay;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.Replay;

public class SubHashTests
{
    private static Simulation Stepped(int ticks)
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        for (int i = 0; i < ticks; i++) sim.Step(Array.Empty<Command>());
        return sim;
    }

    [Fact]
    public void SubHashes_StartWithCoreThenEveryPartInOrder()
    {
        var sim = Stepped(4);
        var names = sim.ComputeSubHashes().Select(s => s.Part).ToList();
        var expected = new List<string> { "core" };
        expected.AddRange(sim.StateParts.Select(p => p.Name));
        Assert.Equal(expected, names);
    }

    [Fact]
    public void SubHashes_AreDeterministicForEqualSims()
    {
        Assert.Equal(Stepped(20).ComputeSubHashes(), Stepped(20).ComputeSubHashes());
    }

    [Fact]
    public void FirstDifferentPart_IsNullForIdenticalSims()
    {
        Assert.Null(Simulation.FirstDifferentPart(Stepped(20).ComputeSubHashes(), Stepped(20).ComputeSubHashes()));
    }

    [Fact]
    public void FirstDifferentPart_FindsTheMutatedPart()
    {
        var a = Stepped(20);
        var b = Stepped(20);
        b.Units.Health[1] += 1; // touches the units part only
        Assert.Equal("units", Simulation.FirstDifferentPart(a.ComputeSubHashes(), b.ComputeSubHashes()));
    }

    [Fact]
    public void FirstDifferentPart_ReportsCoreWhenTickDiffers()
    {
        Assert.Equal("core", Simulation.FirstDifferentPart(Stepped(20).ComputeSubHashes(), Stepped(21).ComputeSubHashes()));
    }

    [Fact]
    public void FirstDifferentPart_ReportsFirstPartOnlyPresentInOneList()
    {
        var a = new List<(string Part, ulong Hash)> { ("core", 1UL), ("map", 2UL) };
        var b = new List<(string Part, ulong Hash)> { ("core", 1UL), ("map", 2UL), ("units", 3UL) };
        Assert.Equal("units", Simulation.FirstDifferentPart(a, b));
    }

    [Fact]
    public void RecordedHashChecks_CarrySubHashesByDefault()
    {
        var setup = ScriptedGame.Setup();
        var bytes = RecordWith(setup, 64, new ReplayWriter(setup));
        ReplayData data = ReplayReader.Read(new MemoryStream(bytes));
        var checks = data.Records.Where(r => r.Type == ReplayWriter.TypeHashCheck).ToList();
        Assert.Equal(4, checks.Count);
        foreach (ReplayRecord c in checks)
        {
            Assert.NotNull(c.SubHashes);
            Assert.Equal("core", c.SubHashes![0].Part);
        }
    }

    [Fact]
    public void RecordSubHashesOff_WritesNoSubHashes()
    {
        var setup = ScriptedGame.Setup();
        var bytes = RecordWith(setup, 64, new ReplayWriter(setup) { RecordSubHashes = false });
        ReplayData data = ReplayReader.Read(new MemoryStream(bytes));
        Assert.All(data.Records.Where(r => r.Type == ReplayWriter.TypeHashCheck), c => Assert.Null(c.SubHashes));
    }

    private static byte[] RecordWith(SimSetup setup, int ticks, ReplayWriter rec)
    {
        var script = ScriptedGame.Script(ticks);
        foreach (var s in ScriptedGame.Spawns()) rec.RecordSpawn(ActorKind.Vehicle, s.Player, s.Type, s.Cell);
        foreach (var s in ScriptedGame.InfantrySpawns()) rec.RecordSpawn(ActorKind.Infantry, s.Player, s.Type, s.Cell, s.SubSlot);
        foreach (var s in ScriptedGame.BuildingSpawns()) rec.RecordSpawn(ActorKind.Building, s.Player, s.Type, s.Cell);
        foreach (var c in script) rec.RecordCommand(c);
        var sim = ScriptedGame.NewSim(setup);
        for (int i = 0; i < ticks; i++)
        {
            sim.Step(script);
            if (sim.Tick % SimConstants.HashIntervalTicks == 0) rec.RecordHash(sim.Tick, sim.ComputeStateHash(), sim.ComputeSubHashes());
        }
        var ms = new MemoryStream();
        rec.Finish(ms, sim.Tick);
        return ms.ToArray();
    }
}
