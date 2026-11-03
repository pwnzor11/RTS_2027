using Rts.Sim.Core;
using Rts.Sim.Map;

namespace Rts.Sim.Replay;

/// <summary>Outcome of a headless replay. FirstMismatchTick is -1 when every recorded hash matched.</summary>
public readonly record struct ReplayResult(int TicksRun, int FirstMismatchTick, ulong FinalHash);

/// <summary>Re-runs a replay headlessly and verifies each recorded state hash.</summary>
public static class ReplayPlayer
{
    public static ReplayResult Run(Stream replay, SimSetup setup)
    {
        ReplayData data = ReplayReader.Read(replay);
        if (data.RulesHash != setup.RulesHash || data.Seed != setup.Seed || data.MapWidth != setup.Map.Width
            || data.MapHeight != setup.Map.Height || data.PlayerCount != setup.PlayerCount
            || data.MapHash != MapHasher.Compute(setup.Map))
            throw new InvalidDataException("Replay header does not match setup.");

        var sim = new Simulation(setup);
        foreach (var s in data.Spawns) sim.SpawnVehicle(s.Player, s.Type, s.Cell);

        var commands = new List<Command>();
        foreach (ReplayRecord r in data.Records)
            if (r.Type == ReplayWriter.TypeCommand) commands.Add(r.Command);

        int first = -1;
        foreach (ReplayRecord r in data.Records)
        {
            if (r.Type != ReplayWriter.TypeHashCheck) continue;
            while (sim.Tick < r.HashTick) sim.Step(commands);
            if (sim.Tick == r.HashTick && sim.ComputeStateHash() != r.Hash && first < 0) first = r.HashTick;
        }
        while (sim.Tick < data.FinalTick) sim.Step(commands);
        return new ReplayResult(sim.Tick, first, sim.ComputeStateHash());
    }
}
