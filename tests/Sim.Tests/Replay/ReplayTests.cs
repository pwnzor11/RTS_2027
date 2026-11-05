// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Replay;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.Replay;

public class ReplayTests
{
    internal static readonly IReadOnlyList<(string Name, string Text)> RulesFiles =
        new[] { ("game.toml", "[game]\ntick_rate = 20\n"), ("units.toml", new string('x', 4000)) };

    /// <summary>Records the scripted game. finish=false leaves the stream without an end record (a crash).</summary>
    internal static byte[] Record(SimSetup setup, int ticks, bool subHashes = true, bool finish = true)
    {
        var script = ScriptedGame.Script(ticks);
        var sim = ScriptedGame.NewSim(setup);              // spawns are taken from sim.SetupLog, never re-recorded
        var ms = new MemoryStream();
        var rec = new ReplayWriter(ms, sim, "test-build", RulesFiles) { RecordSubHashes = subHashes };
        foreach (var c in script) rec.RecordCommand(c);
        for (int i = 0; i < ticks; i++)
        {
            sim.Step(script);
            if (sim.Tick % SimConstants.HashIntervalTicks == 0) rec.RecordHash(sim);
        }
        if (finish) rec.Finish();
        return ms.ToArray();
    }

    [Fact]
    public void RecordedGame_PlaysBackWithoutMismatch()
    {
        var setup = ScriptedGame.Setup();
        byte[] bytes = Record(setup, 500);
        var result = ReplayPlayer.Run(new MemoryStream(bytes), setup);
        Assert.Equal(-1, result.FirstMismatchTick);
        Assert.Null(result.FirstMismatchPart);
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
        var rec = data.Records.First(r => r.Type == ReplayWriter.TypeCommand && ScriptedGame.IsEffectiveVehicleMove(r.Command));
        int cellXOffset = (int)rec.Offset + 1 + 4 + 1 + 2 + 1 + 8;
        bytes[cellXOffset] ^= 0x07;
        var result = ReplayPlayer.Run(new MemoryStream(bytes), setup);
        Assert.True(result.FirstMismatchTick > 0);
        Assert.True(result.FirstMismatchTick >= rec.Command.ExecTick);
        Assert.Equal(0, result.FirstMismatchTick % 16);
        Assert.Equal("units", result.FirstMismatchPart);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void OldFormatVersions_AreRejectedWithAClearMessage(int version)
    {
        var setup = ScriptedGame.Setup();
        byte[] bytes = Record(setup, 32);
        bytes[4] = (byte)version; // version is a little-endian int32 right after the magic
        var ex = Assert.Throws<InvalidDataException>(() => ReplayReader.Read(new MemoryStream(bytes)));
        Assert.Contains($"version {version}", ex.Message);
        Assert.Contains("reads versions 7 to 10", ex.Message);
    }

    [Fact]
    public void Header_CarriesBuildSetupAndEmbeddedRules()
    {
        var setup = ScriptedGame.Setup() with
        {
            Teams = new byte[] { 0, 0 }, Limits = new SimLimits(300, 200, 100, 64), AllowDebug = true,
        };
        var data = ReplayReader.Read(new MemoryStream(Record(setup, 40)));
        Assert.Equal(10, data.Version);
        Assert.Null(data.Mission);
        Assert.Equal("test-build", data.Build);
        Assert.Equal(setup.Seed, data.Seed);
        Assert.Equal(2, data.PlayerCount);
        Assert.Equal(new byte[] { 0, 0 }, data.Header.Teams);
        Assert.Equal(new SimLimits(300, 200, 100, 64), data.Header.Limits);
        Assert.True(data.Header.AllowDebug);
        Assert.Equal(setup.RulesHash, data.RulesHash);
        Assert.Equal(setup.Map.Name, data.Header.MapName);
        Assert.Equal(RulesFiles, data.RulesFiles);
        Assert.False(data.Truncated);
        Assert.Equal(40, data.FinalTick);
        // The player accepts it with the matching setup, and rejects a setup that differs in any identity field.
        var ok = ReplayPlayer.Run(new MemoryStream(Record(setup, 40)), setup);
        Assert.Equal(-1, ok.FirstMismatchTick);
        Assert.Throws<InvalidDataException>(() => ReplayPlayer.Run(new MemoryStream(Record(setup, 40)), setup with { AllowDebug = false }));
        Assert.Throws<InvalidDataException>(() => ReplayPlayer.Run(new MemoryStream(Record(setup, 40)), setup with { Teams = null }));
        Assert.Throws<InvalidDataException>(() => ReplayPlayer.Run(new MemoryStream(Record(setup, 40)), setup with { Limits = SimLimits.Default }));
    }

    [Fact]
    public void Spawns_AreTakenFromTheSetupLog()
    {
        var setup = ScriptedGame.Setup();
        var data = ReplayReader.Read(new MemoryStream(Record(setup, 20)));
        Assert.Equal(32, data.Spawns.Count);   // 20 vehicles + 10 infantry + 2 buildings
        Assert.Equal(ActorKind.Vehicle, data.Spawns[0].Kind);
        Assert.Equal(ActorKind.Building, data.Spawns[^1].Kind);
        Assert.All(data.Spawns, s => Assert.Equal(0, s.Tick));
    }

    [Fact]
    public void SpawnsMadeAfterTheWriterExists_AreRecordedAndPlayBack()
    {
        var setup = ScriptedGame.Setup();
        var sim = new Simulation(setup);
        var ms = new MemoryStream();
        var rec = new ReplayWriter(ms, sim, "t", RulesFiles);      // header is already out; the sim is still empty
        Assert.True(ms.Length > 0);
        foreach (var s in ScriptedGame.Spawns()) sim.SpawnVehicle(s.Player, s.Type, s.Cell);
        var script = ScriptedGame.Script(100);
        foreach (var c in script) rec.RecordCommand(c);
        for (int i = 0; i < 100; i++)
        {
            sim.Step(script);
            if (sim.Tick % SimConstants.HashIntervalTicks == 0) rec.RecordHash(sim);
        }
        rec.Finish();
        var result = ReplayPlayer.Run(new MemoryStream(ms.ToArray()), setup);
        Assert.Equal(-1, result.FirstMismatchTick);
        Assert.Equal(sim.ComputeStateHash(), result.FinalHash);
    }

    [Fact]
    public void CommandArg2_IsPersisted()
    {
        var setup = ScriptedGame.Setup();
        var sim = ScriptedGame.NewSim(setup);
        var ms = new MemoryStream();
        var rec = new ReplayWriter(ms, sim, "t", RulesFiles);
        var c = new Command(3, 1, 7, CommandKind.Debug, default, default, new CellCoord(4, 5), 11, -123456);
        rec.RecordCommand(c);
        rec.Finish();
        var data = ReplayReader.Read(new MemoryStream(ms.ToArray()));
        Assert.Equal(c, data.Records.Single(r => r.Type == ReplayWriter.TypeCommand).Command);
    }

    [Fact]
    public void StreamIsFlushedEvery64Ticks_AndOnFinish()
    {
        var setup = ScriptedGame.Setup();
        var sim = ScriptedGame.NewSim(setup);
        var stream = new FlushCountingStream();
        var rec = new ReplayWriter(stream, sim, "t", RulesFiles);
        int afterHeader = stream.Flushes;
        for (int i = 0; i < 300; i++)
        {
            sim.Step(Array.Empty<Command>());
            if (sim.Tick % SimConstants.HashIntervalTicks == 0) rec.RecordHash(sim);
        }
        Assert.Equal(afterHeader + 300 / ReplayWriter.FlushIntervalTicks, stream.Flushes);   // ticks 64, 128, 192, 256
        rec.Finish();
        Assert.True(stream.Flushes > afterHeader + 300 / ReplayWriter.FlushIntervalTicks);
    }

    [Fact]
    public void CrashedRecording_ReadsAsTruncated_AndVerifiesUpToTheLastHash()
    {
        var setup = ScriptedGame.Setup();
        byte[] crashed = Record(setup, 300, finish: false);     // the stream is dropped without Finish
        var data = ReplayReader.Read(new MemoryStream(crashed));
        Assert.True(data.Truncated);
        Assert.Equal(288, data.FinalTick);                      // last hash record at or before tick 300
        var result = ReplayPlayer.Run(new MemoryStream(crashed), setup);
        Assert.Equal(-1, result.FirstMismatchTick);
        Assert.Equal(288, result.TicksRun);
        Assert.True(result.Truncated);

        // Same with the last record torn in half: every complete record is still read.
        byte[] torn = crashed[..^3];
        var tornData = ReplayReader.Read(new MemoryStream(torn));
        Assert.True(tornData.Truncated);
        Assert.Equal(-1, ReplayPlayer.Run(new MemoryStream(torn), setup).FirstMismatchTick);

        // A finished replay is not truncated.
        Assert.False(ReplayReader.Read(new MemoryStream(Record(setup, 300))).Truncated);
    }

    [Fact]
    public void HeaderCutShort_IsAnError_NotATruncatedReplay()
    {
        byte[] bytes = Record(ScriptedGame.Setup(), 32);
        Assert.Throws<InvalidDataException>(() => ReplayReader.Read(new MemoryStream(bytes[..30])));
    }

    [Fact]
    public void ManyCommands_PlayBackQuicklyBecauseStepSeesOnlyItsTicksSlice()
    {
        var setup = ScriptedGame.Setup();
        var sim = ScriptedGame.NewSim(setup);
        var ms = new MemoryStream();
        var rec = new ReplayWriter(ms, sim, "t", RulesFiles);
        var script = new List<Command>();
        for (int t = 0; t < 400; t++)
            for (int k = 0; k < 50; k++)
                script.Add(new Command(t, (byte)(k % 2), (ushort)k, CommandKind.Debug, default, default, default, 1));
        foreach (var c in script) rec.RecordCommand(c);
        for (int i = 0; i < 400; i++)
        {
            sim.Step(script);
            if (sim.Tick % SimConstants.HashIntervalTicks == 0) rec.RecordHash(sim);
        }
        rec.Finish();
        var result = ReplayPlayer.Run(new MemoryStream(ms.ToArray()), setup);
        Assert.Equal(-1, result.FirstMismatchTick);
        Assert.Equal(sim.ComputeStateHash(), result.FinalHash);
    }

    private sealed class FlushCountingStream : MemoryStream
    {
        public int Flushes;
        public override void Flush() { Flushes++; base.Flush(); }
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
