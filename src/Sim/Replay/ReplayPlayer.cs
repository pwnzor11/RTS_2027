// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
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
    public static ReplayResult Run(ReplayData data, SimSetup setup) => Run(data, setup, null);

    /// <summary>
    /// Plays back a replay with a setup hook (M3-19). <paramref name="beforeFirstStep"/> runs once, right after the Simulation is built
    /// and before its first Step, and owns the tick-0 setup: it must place exactly the actors the recording placed at tick 0 (the
    /// scripting layer uses it to place a mission's pre-placed actors; its extension comes in through <paramref name="setup"/>).
    /// The recorded tick-0 spawns are then not applied again, but checked against what the hook placed; a difference throws
    /// <see cref="InvalidDataException"/>. With a null hook the recorded spawns are applied as usual.
    /// </summary>
    public static ReplayResult Run(ReplayData data, SimSetup setup, Action<Simulation>? beforeFirstStep)
    {
        data.Header.CheckMatches(setup, MapHasher.Compute(setup.Map), "Replay", checkSeed: true);

        var sim = new Simulation(setup);

        // Bucket commands by ExecTick (stable: same-tick commands keep file order) so each Step sees only its tick's slice.
        var commands = new List<(int Tick, int Order, Command Command)>();
        foreach (ReplayRecord r in data.Records)
            if (r.Type == ReplayWriter.TypeCommand) commands.Add((r.Command.ExecTick, commands.Count, r.Command));
        commands.Sort(static (a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : a.Order.CompareTo(b.Order));

        var state = new Cursor(sim, data.Spawns, commands);
        if (beforeFirstStep is null) state.ApplySpawns();    // the setup, before tick 0
        else
        {
            beforeFirstStep(sim);
            state.SkipSetupSpawns(sim);
        }
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

        /// <summary>The hook placed the tick-0 setup: check it matches the recorded tick-0 spawns and step past them.</summary>
        public void SkipSetupSpawns(Simulation s)
        {
            IReadOnlyList<SetupEntry> log = s.SetupLog;
            int n = 0;
            while (_nextSpawn < spawns.Count && spawns[_nextSpawn].Tick <= 0)
            {
                ReplaySpawn r = spawns[_nextSpawn++];
                bool same = n < log.Count && log[n].Kind == r.Kind && log[n].Player == r.Player && log[n].TypeIndex == r.Type
                    && log[n].Cell == r.Cell && log[n].SubSlot == r.SubSlot;
                if (!same) throw new InvalidDataException($"Replay setup differs from the setup hook's at spawn {n}: the hook placed something else than was recorded.");
                n++;
            }
            if (n != log.Count) throw new InvalidDataException($"Replay recorded {n} setup spawns but the setup hook placed {log.Count}.");
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
