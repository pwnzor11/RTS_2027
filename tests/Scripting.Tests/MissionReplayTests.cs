// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Replay;

namespace Rts.Scripting.Tests;

/// <summary>Mission replays (M3-19): a recorded Survey Convoy run plays back to the same state, a changed script fails loudly.</summary>
public sealed class MissionReplayTests
{
    private const int Ticks = 600;

    /// <summary>Records <see cref="Ticks"/> ticks of auth_01 with scripted human commands; returns the bytes and the final hash.</summary>
    private static (byte[] Bytes, ulong FinalHash, MissionSession Session) Record(ReplayMission? overrideMission = null)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "auth_01");
        Simulation sim = m.Sim;
        var ms = new MemoryStream();
        var rec = new ReplayWriter(ms, sim, "test-build", RulesBundle.ReadDirectory(Path.Combine(TestData.DataDir, "rules")),
            overrideMission ?? MissionReplay.RefFor(m));
        ushort seq = 0;
        for (int i = 0; i < Ticks; i++)
        {
            var cmds = new List<Command>();
            if (sim.Tick == 5 || sim.Tick == 300)
            {
                CellCoord dest = sim.Tick == 5 ? new CellCoord(40, 14) : new CellCoord(52, 10);
                for (int u = 1; u < sim.Units.Capacity; u++)
                    if (sim.Units.Slots.IsIndexAlive(u) && sim.Units.Player[u] == 0)
                        cmds.Add(new Command(sim.Tick, 0, seq++, CommandKind.AttackMove, sim.Units.Slots.IdOf(u), default, dest, (int)ActorKind.Vehicle));
                for (int u = 1; u < sim.Infantry.Capacity; u++)
                    if (sim.Infantry.Slots.IsIndexAlive(u) && sim.Infantry.Player[u] == 0)
                        cmds.Add(new Command(sim.Tick, 0, seq++, CommandKind.AttackMove, sim.Infantry.Slots.IdOf(u), default, dest, (int)ActorKind.Infantry));
            }
            foreach (Command c in cmds) rec.RecordCommand(c);
            sim.Step(cmds);
            if (sim.Tick % SimConstants.HashIntervalTicks == 0) rec.RecordHash(sim);
        }
        rec.Finish();
        return (ms.ToArray(), sim.ComputeStateHash(), m);
    }

    [Fact]
    public void Recorded_mission_replays_to_the_same_final_state()
    {
        (byte[] bytes, ulong finalHash, MissionSession m) = Record();
        Assert.Empty(m.Host.Errors);

        ReplayData data = ReplayReader.Read(new MemoryStream(bytes));
        Assert.Equal(new ReplayMission("auth_01", m.Host.SourceHash), data.Mission);
        Assert.True(data.Records.Any(r => r.Type == ReplayWriter.TypeCommand), "the run should contain commands");
        Assert.True(data.Spawns.Count > 0, "pre-placed actors are in the setup log");

        ReplayResult result = MissionReplay.Play(data, TestData.DataDir, out MissionHost host);
        Assert.Equal(-1, result.FirstMismatchTick);
        Assert.Equal(Ticks, result.TicksRun);
        Assert.Equal(finalHash, result.FinalHash);
        Assert.Empty(host.Errors);
        Assert.Equal(m.Host.Objectives.Count, host.Objectives.Count);
    }

    [Fact]
    public void Tampered_script_hash_fails_loudly()
    {
        (byte[] bytes, ulong _, MissionSession m) = Record(new ReplayMission("auth_01", 0x1234UL));
        ReplayData data = ReplayReader.Read(new MemoryStream(bytes));
        var ex = Assert.Throws<InvalidDataException>(() => MissionReplay.Play(data, TestData.DataDir));
        Assert.Contains("Script mismatch", ex.Message);
        Assert.Contains("0x0000000000001234", ex.Message);
        Assert.Contains($"0x{m.Host.SourceHash:x16}", ex.Message);
    }

    [Fact]
    public void Replay_without_a_mission_is_refused()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "auth_01");
        var ms = new MemoryStream();
        var rec = new ReplayWriter(ms, m.Sim, "test-build", Array.Empty<(string, string)>());
        m.Sim.Step(TestData.NoCommands);
        rec.Finish();
        ReplayData data = ReplayReader.Read(new MemoryStream(ms.ToArray()));
        Assert.Throws<InvalidDataException>(() => MissionReplay.Play(data, TestData.DataDir));
    }

    [Fact]
    public void Unknown_mission_id_is_refused()
    {
        (byte[] bytes, ulong _, MissionSession _) = Record(new ReplayMission("no_such_mission", 1));
        ReplayData data = ReplayReader.Read(new MemoryStream(bytes));
        var ex = Assert.Throws<InvalidDataException>(() => MissionReplay.Play(data, TestData.DataDir));
        Assert.Contains("no_such_mission", ex.Message);
    }

    [Fact]
    public void A_changed_command_is_a_desync_not_an_error()
    {
        (byte[] bytes, ulong _, MissionSession _) = Record();
        ReplayData data = ReplayReader.Read(new MemoryStream(bytes));
        ReplayRecord cmd = data.Records.First(r => r.Type == ReplayWriter.TypeCommand);
        bytes[(int)cmd.Offset + 1 + 4 + 1 + 2 + 1 + 8] ^= 0x07;   // cell X of the first command
        ReplayResult result = MissionReplay.Play(ReplayReader.Read(new MemoryStream(bytes)), TestData.DataDir);
        Assert.True(result.FirstMismatchTick > 0);
    }
}
