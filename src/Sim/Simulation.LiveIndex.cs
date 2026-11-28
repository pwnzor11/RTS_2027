// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// M16 M-28b (D-338 (2)): the live-position index for the scans M-28 left on the full slot walk (splash in Detonate, sonar, stealth
// reveal, Blackout sight). All of it is derived scratch: never saved, never hashed, results identical to the walk it replaces.
public sealed partial class Simulation
{
    private readonly LiveSpatialIndex?[] _live = new LiveSpatialIndex?[2];

    /// <summary>Test hook (M-28b): false forces the old full slot walk in every live scan, to compare it with the index. Scratch, not state.</summary>
    internal bool LiveIndexEnabled = true;

    internal int LiveIndexRebuilds => (_live[0]?.Rebuilds ?? 0) + (_live[1]?.Rebuilds ?? 0);

    /// <summary>
    /// Candidate slots of a MOBILE kind whose live position can lie within <paramref name="cellRadius"/> cells (Chebyshev) of
    /// <paramref name="cell"/>, in ascending slot order, for the scans that walk every slot of that kind. False means "walk every slot".
    /// The span is only valid until the next call; callers still apply their own liveness and exact distance tests.
    /// </summary>
    internal bool TryLiveCandidates(ActorKind kind, CellCoord cell, int cellRadius, out ReadOnlySpan<int> candidates)
    {
        candidates = default;
        if (!LiveIndexEnabled || kind == ActorKind.Building || kind > ActorKind.Infantry) return false;
        int k = (int)kind;
        MobileStore store = kind == ActorKind.Vehicle ? _units : _infantry;
        LiveSpatialIndex idx = _live[k] ??= new LiveSpatialIndex(store.Capacity);
        // The cell's centre +- (radius cells + half a cell) covers every position whose cell is within radius cells of it.
        long r = ((long)Math.Max(0, cellRadius) << SimConstants.LeptonShift) + SimConstants.LeptonsPerCell / 2;
        candidates = idx.Query(store, Setup.Map.Width, Setup.Map.Height, LeptonCoord.CenterOf(cell), (int)Math.Min(int.MaxValue / 4, r));
        return true;
    }

    // Largest sonar radius (cells) of any vehicle type a player can field: memoised per rule set object (RulesOf is cached per upgrade mask).
    private SimRules?[] _sonarRulesSeen = Array.Empty<SimRules?>();
    private int[] _sonarVehicleMax = Array.Empty<int>();

    private int MaxVehicleSonarRadius()
    {
        int best = 0, players = Math.Max(Setup.PlayerCount, _players.Count);
        if (_sonarVehicleMax.Length < players) { _sonarVehicleMax = new int[players]; _sonarRulesSeen = new SimRules?[players]; }
        for (int p = 0; p < players; p++)
        {
            SimRules r = RulesOf((byte)p);
            if (!ReferenceEquals(_sonarRulesSeen[p], r))
            {
                int m = 0;
                IReadOnlyList<VehicleType> v = r.Vehicles;
                for (int t = 0; t < v.Count; t++) if (v[t].SonarRadius > m) m = v[t].SonarRadius;
                _sonarVehicleMax[p] = m; _sonarRulesSeen[p] = r;
            }
            if (_sonarVehicleMax[p] > best) best = _sonarVehicleMax[p];
        }
        return best;
    }
}
