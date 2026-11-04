// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Replay;
using static Rts.Sim.Tests.M4.GarrisonFixture;

namespace Rts.Sim.Tests.M4;

/// <summary>M4-03: a garrison fight (enter, fire out, building dies, ejection, fight on) records and replays without a mismatch,
/// and two runs of the same script end on the same hash.</summary>
public sealed class GarrisonReplayTests
{
    private static readonly IReadOnlyList<(string Name, string Text)> RulesFiles = new[] { ("game.toml", "[game]\ntick_rate = 20\n") };

    private static Simulation NewScene(SimSetup setup)
    {
        var sim = new Simulation(setup);
        sim.PlaceBuilding(0, Bunker, new CellCoord(10, 10));
        sim.PlaceBuilding(1, Tenement, new CellCoord(25, 10));
        for (int k = 0; k < 4; k++) sim.SpawnInfantry(0, Rifle, new CellCoord(12, 10 + k % 2), k % 3);
        for (int k = 0; k < 4; k++) sim.SpawnInfantry(1, Rifle, new CellCoord(24, 10 + k % 2), k % 3);
        sim.SpawnVehicle(1, Mortar, new CellCoord(17, 10));
        sim.SpawnVehicle(0, Mortar, new CellCoord(20, 12));
        return sim;
    }

    private static List<Command> Script()
    {
        var sim = NewScene(Setup());   // only to learn the ids: spawns are deterministic, so the ids match in every run
        var bunker = new EntityId(1, 1);
        var tenement = new EntityId(2, 1);
        ushort seq = 0;
        var list = new List<Command>();
        Command C(int tick, byte player, CommandKind kind, EntityId s, EntityId t, int arg) =>
            new(tick, player, seq++, kind, s, t, default, arg, 0);
        for (int k = 1; k <= 4; k++)
        {
            list.Add(C(0, 0, CommandKind.Enter, sim.Infantry.Slots.IdOf(k), bunker, (int)ActorKind.Infantry | ((int)ActorKind.Building << 8)));
            list.Add(C(0, 1, CommandKind.Enter, sim.Infantry.Slots.IdOf(4 + k), tenement, (int)ActorKind.Infantry | ((int)ActorKind.Building << 8)));
        }
        list.Add(C(10, 1, CommandKind.Attack, sim.Units.Slots.IdOf(1), bunker, (int)ActorKind.Vehicle | ((int)ActorKind.Building << 8)));
        list.Add(C(10, 0, CommandKind.Attack, sim.Units.Slots.IdOf(2), tenement, (int)ActorKind.Vehicle | ((int)ActorKind.Building << 8)));
        list.Add(C(400, 1, CommandKind.Unload, tenement, default, (int)ActorKind.Building));
        return list;
    }

    [Fact]
    public void GarrisonFight_RecordsAndReplaysWithoutMismatch_AndIsRepeatable()
    {
        const int ticks = 700;
        SimSetup setup = Setup();
        List<Command> script = Script();
        Simulation sim = NewScene(setup);
        var ms = new MemoryStream();
        var rec = new ReplayWriter(ms, sim, "test-build", RulesFiles) { RecordSubHashes = true };
        foreach (var c in script) rec.RecordCommand(c);
        var fxExit = 0;
        for (int i = 0; i < ticks; i++)
        {
            sim.Step(script);
            fxExit += sim.FxEvents.Count(f => f.Kind == FxKind.GarrisonExit);
            if (sim.Tick % SimConstants.HashIntervalTicks == 0) rec.RecordHash(sim);
        }
        rec.Finish();
        Assert.True(sim.Buildings.Slots.Count < 2 || fxExit > 0, "the fight should have produced ejections");

        ReplayResult result = ReplayPlayer.Run(new MemoryStream(ms.ToArray()), setup);
        Assert.Equal(-1, result.FirstMismatchTick);
        Assert.Equal(sim.ComputeStateHash(), result.FinalHash);

        Simulation again = NewScene(setup);
        for (int i = 0; i < ticks; i++) again.Step(script);
        Assert.Equal(sim.ComputeStateHash(), again.ComputeStateHash());
    }
}
