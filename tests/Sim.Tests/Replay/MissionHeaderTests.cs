// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Replay;
using Rts.Sim.Serialization;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.Replay;

/// <summary>Format 8 (M3-19): the optional mission id + script hash in the replay header, and version-gated reading of format 7.</summary>
public class MissionHeaderTests
{
    private static byte[] Record(SimSetup setup, int ticks, ReplayMission? mission)
    {
        var script = ScriptedGame.Script(ticks);
        var sim = ScriptedGame.NewSim(setup);
        var ms = new MemoryStream();
        var rec = new ReplayWriter(ms, sim, "test-build", ReplayTests.RulesFiles, mission);
        foreach (var c in script) rec.RecordCommand(c);
        for (int i = 0; i < ticks; i++)
        {
            sim.Step(script);
            if (sim.Tick % SimConstants.HashIntervalTicks == 0) rec.RecordHash(sim);
        }
        rec.Finish();
        return ms.ToArray();
    }

    [Fact]
    public void MissionIdAndScriptHash_RoundTrip()
    {
        var setup = ScriptedGame.Setup();
        var data = ReplayReader.Read(new MemoryStream(Record(setup, 64, new ReplayMission("auth_01", 0xDEADBEEF12345678UL))));
        Assert.Equal(ReplayWriter.Version, data.Version);   // 8 (M3-19); 9 since M4-01, 10 since M6-02, 11 since VET-1, 12 since M7-02, same layout
        Assert.Equal(new ReplayMission("auth_01", 0xDEADBEEF12345678UL), data.Mission);
        Assert.False(data.Truncated);
        Assert.Equal(64, data.FinalTick);
    }

    [Fact]
    public void NoMission_ReadsAsNull_AndStillPlays()
    {
        var setup = ScriptedGame.Setup();
        byte[] bytes = Record(setup, 64, null);
        Assert.Null(ReplayReader.Read(new MemoryStream(bytes)).Mission);
        Assert.Equal(-1, ReplayPlayer.Run(new MemoryStream(bytes), setup).FirstMismatchTick);
    }

    [Fact]
    public void Version7Replay_StillLoadsAndPlays()
    {
        var setup = ScriptedGame.Setup();
        byte[] v8 = Record(setup, 200, null);
        // Rebuild it as a format 7 file: version 7 and no mission block (one flag byte right after the SetupHeader).
        var header = new MemoryStream();
        using (var w = new BinaryWriter(header, System.Text.Encoding.UTF8, leaveOpen: true))
            SetupHeader.From(ScriptedGame.NewSim(setup), "test-build", ReplayTests.RulesFiles).Write(w);
        int missionByte = 8 + (int)header.Length;
        Assert.Equal(0, v8[missionByte]);
        byte[] v7 = v8.Take(missionByte).Concat(v8.Skip(missionByte + 1)).ToArray();
        v7[4] = 7;

        var data = ReplayReader.Read(new MemoryStream(v7));
        Assert.Equal(7, data.Version);
        Assert.Null(data.Mission);
        Assert.Equal(200, data.FinalTick);
        var result = ReplayPlayer.Run(new MemoryStream(v7), setup);
        Assert.Equal(-1, result.FirstMismatchTick);
        Assert.Equal(200, result.TicksRun);
    }

    [Fact]
    public void FutureVersion_IsRejected()
    {
        byte[] bytes = Record(ScriptedGame.Setup(), 32, null);
        bytes[4] = 13;   // M4-01 took 9, M6-02 took 10, VET-1 took 11, M7-02 took 12
        var ex = Assert.Throws<InvalidDataException>(() => ReplayReader.Read(new MemoryStream(bytes)));
        Assert.Contains("version 13", ex.Message);
    }

    [Fact]
    public void SetupHook_RunsBeforeFirstStep_AndOwnsTheTickZeroSpawns()
    {
        var setup = ScriptedGame.Setup();
        byte[] bytes = Record(setup, 100, null);
        int hookTick = -1, hookLog = -1;
        var result = ReplayPlayer.Run(ReplayReader.Read(new MemoryStream(bytes)), setup, sim =>
        {
            hookTick = sim.Tick;
            // Do what the setup did (this stands in for a mission placing its actors).
            foreach (var sp in ScriptedGame.Spawns()) sim.SpawnVehicle(sp.Player, sp.Type, sp.Cell);
            foreach (var sp in ScriptedGame.InfantrySpawns()) sim.SpawnInfantry(sp.Player, sp.Type, sp.Cell, sp.SubSlot);
            foreach (var sp in ScriptedGame.BuildingSpawns()) sim.PlaceBuilding(sp.Player, sp.Type, sp.Cell);
            hookLog = sim.SetupLog.Count;
        });
        Assert.Equal(0, hookTick);
        Assert.True(hookLog > 0);
        Assert.Equal(-1, result.FirstMismatchTick);
    }

    [Fact]
    public void SetupHook_ThatPlacesTheWrongThing_FailsLoudly()
    {
        var setup = ScriptedGame.Setup();
        var data = ReplayReader.Read(new MemoryStream(Record(setup, 40, null)));
        Assert.Throws<InvalidDataException>(() => ReplayPlayer.Run(data, setup, _ => { }));
    }
}
