using Rts.Sim.Core;
using Rts.Sim.Map;

namespace Rts.Sim.Replay;

/// <summary>
/// Outcome of a headless replay. FirstMismatchTick is -1 when every recorded hash matched. FirstMismatchPart names
/// the first state part whose sub-hash differed at that tick, or is null when the replay has no sub-hashes there.
/// </summary>
public readonly record struct ReplayResult(int TicksRun, int FirstMismatchTick, ulong FinalHash, string? FirstMismatchPart);

/// <summary>Re-runs a replay headlessly and verifies each recorded state hash.</summary>
public static class ReplayPlayer
{
    public static ReplayResult Run(Stream replay, SimSetup setup)
    {
        ReplayData data = ReplayReader.Read(replay);
        CheckHeader(data, setup);

        var sim = new Simulation(setup);
        foreach (var s in data.Spawns)
        {
            if (s.Kind == ActorKind.Building) sim.PlaceBuilding(s.Player, s.Type, s.Cell);
            else if (s.Kind == ActorKind.Infantry) sim.SpawnInfantry(s.Player, s.Type, s.Cell, s.SubSlot);
            else sim.SpawnVehicle(s.Player, s.Type, s.Cell);
        }

        var commands = new List<Command>();
        foreach (ReplayRecord r in data.Records)
            if (r.Type == ReplayWriter.TypeCommand) commands.Add(r.Command);

        int first = -1;
        string? part = null;
        foreach (ReplayRecord r in data.Records)
        {
            if (r.Type != ReplayWriter.TypeHashCheck) continue;
            while (sim.Tick < r.HashTick) sim.Step(commands);
            if (first >= 0 || sim.Tick != r.HashTick || sim.ComputeStateHash() == r.Hash) continue;
            first = r.HashTick;
            if (r.SubHashes is not null) part = Simulation.FirstDifferentPart(r.SubHashes, sim.ComputeSubHashes());
        }
        while (sim.Tick < data.FinalTick) sim.Step(commands);
        return new ReplayResult(sim.Tick, first, sim.ComputeStateHash(), part);
    }

    private static void CheckHeader(ReplayData data, SimSetup setup)
    {
        if (data.RulesHash != setup.RulesHash)
            throw new InvalidDataException($"Replay rules hash 0x{data.RulesHash:x16} does not match setup 0x{setup.RulesHash:x16}.");
        if (data.MapHash != MapHasher.Compute(setup.Map))
            throw new InvalidDataException($"Replay map hash 0x{data.MapHash:x16} does not match the setup map.");
        if (data.Seed != setup.Seed)
            throw new InvalidDataException($"Replay seed {data.Seed} does not match setup seed {setup.Seed}.");
        if (data.MapWidth != setup.Map.Width || data.MapHeight != setup.Map.Height)
            throw new InvalidDataException($"Replay map size {data.MapWidth}x{data.MapHeight} does not match setup {setup.Map.Width}x{setup.Map.Height}.");
        if (data.PlayerCount != setup.PlayerCount)
            throw new InvalidDataException($"Replay player count {data.PlayerCount} does not match setup {setup.PlayerCount}.");
    }
}
