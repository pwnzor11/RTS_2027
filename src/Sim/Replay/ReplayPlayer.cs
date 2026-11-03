using Rts.Sim.Core;
using Rts.Sim.Map;

namespace Rts.Sim.Replay;

/// <summary>
/// Outcome of a headless replay. FirstMismatchTick is -1 when every recorded hash matched. FirstMismatchPart names
/// the first state part whose sub-hash differed at that tick, or is null when the replay has no sub-hashes there.
/// Truncated is true when the replay had no end record (a crash); it was verified up to its last hash record.
/// </summary>
public readonly record struct ReplayResult(int TicksRun, int FirstMismatchTick, ulong FinalHash, string? FirstMismatchPart,
    bool Truncated = false);

/// <summary>Re-runs a replay headlessly and verifies each recorded state hash.</summary>
public static class ReplayPlayer
{
    public static ReplayResult Run(Stream replay, SimSetup setup) => Run(ReplayReader.Read(replay), setup);

    /// <summary>Plays back an already parsed replay on <paramref name="setup"/>, which must match its header.</summary>
    public static ReplayResult Run(ReplayData data, SimSetup setup)
    {
        data.Header.CheckMatches(setup, MapHasher.Compute(setup.Map), "Replay", checkSeed: true);

        var sim = new Simulation(setup);

        // Bucket commands by ExecTick (stable: same-tick commands keep file order) so each Step sees only its tick's slice.
        var commands = new List<(int Tick, int Order, Command Command)>();
        foreach (ReplayRecord r in data.Records)
            if (r.Type == ReplayWriter.TypeCommand) commands.Add((r.Command.ExecTick, commands.Count, r.Command));
        commands.Sort(static (a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : a.Order.CompareTo(b.Order));

        var state = new Cursor(sim, data.Spawns, commands);
        state.ApplySpawns();    // the setup, before tick 0
        int first = -1;
        string? part = null;
        var subs = new List<(string Part, ulong Hash)>(sim.StateParts.Count + 1);
        foreach (ReplayRecord r in data.Records)
        {
            if (r.Type != ReplayWriter.TypeHashCheck) continue;
            state.StepTo(r.HashTick);
            if (first >= 0 || sim.Tick != r.HashTick) continue;
            ulong full = sim.ComputeHashes(subs);
            if (full == r.Hash) continue;
            first = r.HashTick;
            if (r.SubHashes is not null) part = Simulation.FirstDifferentPart(r.SubHashes, subs);
        }
        state.StepTo(data.FinalTick);
        state.ApplySpawns();
        return new ReplayResult(sim.Tick, first, sim.ComputeStateHash(), part, data.Truncated);
    }

    /// <summary>Walks the sim forward, applying setup spawns and handing Step only the current tick's commands.</summary>
    private sealed class Cursor(Simulation sim, IReadOnlyList<ReplaySpawn> spawns, List<(int Tick, int Order, Command Command)> commands)
    {
        private readonly List<Command> _slice = new();
        private int _nextSpawn, _nextCommand;

        public void StepTo(int target)
        {
            while (sim.Tick < target)
            {
                ApplySpawns();     // a spawn stamped T happened after the hash check at T, before the Step out of T
                _slice.Clear();
                while (_nextCommand < commands.Count && commands[_nextCommand].Tick < sim.Tick) _nextCommand++;   // late: Step would ignore them
                while (_nextCommand < commands.Count && commands[_nextCommand].Tick == sim.Tick) _slice.Add(commands[_nextCommand++].Command);
                sim.Step(_slice);
            }
        }

        public void ApplySpawns()
        {
            while (_nextSpawn < spawns.Count && spawns[_nextSpawn].Tick <= sim.Tick)
            {
                ReplaySpawn s = spawns[_nextSpawn++];
                if (s.Kind == ActorKind.Building) sim.PlaceBuilding(s.Player, s.Type, s.Cell);
                else if (s.Kind == ActorKind.Infantry) sim.SpawnInfantry(s.Player, s.Type, s.Cell, s.SubSlot);
                else sim.SpawnVehicle(s.Player, s.Type, s.Cell);
            }
        }
    }
}
