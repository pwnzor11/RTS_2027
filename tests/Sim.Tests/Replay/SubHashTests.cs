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
    public void ComputeHashes_OnePass_AgreesWithTheSeparateCalls()
    {
        var sim = Stepped(40);
        var sub = sim.ComputeHashes(out ulong full);
        Assert.Equal(sim.ComputeStateHash(), full);
        Assert.Equal(sim.ComputeSubHashes(), sub);
        var into = new List<(string Part, ulong Hash)>();
        Assert.Equal(full, sim.ComputeHashes(into));
        Assert.Equal(sub, into);
    }

    [Fact]
    public void FullHash_KeepsItsDefinition_XxHash64OverTheWrittenState()
    {
        var sim = Stepped(40);
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            Rts.Sim.Serialization.SimWriter.WriteState(w, sim.Tick, SimRandomOf(sim), sim.StateParts);
        Assert.Equal(System.IO.Hashing.XxHash64.HashToUInt64(ms.GetBuffer().AsSpan(0, (int)ms.Length)), sim.ComputeStateHash());
    }

    // The RNG is private to the sim; a save round trip gives an equal one.
    private static SimRandom SimRandomOf(Simulation sim)
    {
        var field = typeof(Simulation).GetField("_rng", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        return (SimRandom)field.GetValue(sim)!;
    }

    [Fact]
    public void HashingAllocatesNothingPerCallAfterWarmUp()
    {
        var sim = Stepped(60);
        var into = new List<(string Part, ulong Hash)>();
        for (int i = 0; i < 5; i++) { sim.ComputeStateHash(); sim.ComputeHashes(into); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) { sim.ComputeStateHash(); sim.ComputeHashes(into); }
        long perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / 200;
        // Constant, not proportional to state (a full serialisation was ~MBs). Exact small numbers differ by runtime/OS
        // (Linux Debug ~40 B, Windows Release ~95 B), so the bound is loose.
        Assert.True(perCall <= 1024, $"{perCall} bytes allocated per hash call");
    }

    [Fact]
    public void RecordedHashChecks_CarrySubHashesByDefault()
    {
        var setup = ScriptedGame.Setup();
        ReplayData data = ReplayReader.Read(new MemoryStream(ReplayTests.Record(setup, 64)));
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
        ReplayData data = ReplayReader.Read(new MemoryStream(ReplayTests.Record(setup, 64, subHashes: false)));
        Assert.All(data.Records.Where(r => r.Type == ReplayWriter.TypeHashCheck), c => Assert.Null(c.SubHashes));
    }
}
