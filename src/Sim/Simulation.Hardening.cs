// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Ground;
using Rts.Sim.Pathing;
using Rts.Sim.Rules;

namespace Rts.Sim;

// Foundation Grouting enclosure and gates (design 05 §2, D-076). OWNER: M3-04. State: Ground.Hardened (bit per player slot),
// Ground.HardeningDirty (set by the building hooks in Simulation.M3.cs), Ground.GroutedPlayers (who had grouting at the last
// recompute). Gate pathing and the walls-may-close placement rule are M3-04's own edits in Pathing/, Movement/Occupancy.cs,
// Missions/MoveMission.cs, Simulation.Movement.cs and Simulation.cs CanPlace (each marked "M3-04").
//
// Enclosure (per team that has grouting): seal cells = every footprint cell of a live building owned by a team member (walls,
// gates and structures alike). A 4-neighbour flood fill runs from every non-seal map-edge cell through non-seal cells; terrain
// does not matter (water or rock does not seal; ASSUMED (M3-04), as design 05 §2 names only buildings). Every cell neither reached nor a seal cell is enclosed; the enclosed SOFT
// cells get the bit of every team member. Cost: O(map cells + building footprints) per hardening team, only on a tick where a
// seal piece appeared/disappeared or a player's grouting status changed. Measured: 128x128 map, 1 team, 236 wall pieces: below
// timing noise (under 0.2 ms) next to a ~3.5 ms debug-build tick (WallPlacementAndSaveTests.RecomputeCost_IsBounded_On128Map).
public sealed partial class Simulation
{
    // Scratch for the enclosure flood fill. Not sim state: fully rewritten on every recompute (stamp-based).
    private int[]? _hardQueue;
    private int[]? _hardSeal;    // == _hardStamp: a seal cell of the team being computed
    private int[]? _hardReach;   // == _hardStamp: reached from the map edge
    private int _hardStamp;

    // Rules-derived (immutable): upgrade bits whose Effect is "grouting". Not sim state.
    private ulong _groutingBits;
    private bool _groutingResolved;

    /// <summary>Per tick (first in RunGround): when Ground.HardeningDirty, or when the set of players with the "grouting"
    /// upgrade effect changed, recompute every team's enclosure, then clear the flag.</summary>
    private void RunHardening()
    {
        ulong grouted = GroutedPlayerMask();
        if (!_ground.HardeningDirty && grouted == _ground.GroutedPlayers) return;
        _ground.HardeningDirty = false;
        _ground.GroutedPlayers = grouted;
        RecomputeHardening(grouted);
    }

    /// <summary>
    /// True if a burrowed unit or Bore of <paramref name="player"/> may NOT surface on / break into <paramref name="cell"/>: the
    /// cell is hardened by a team that is not friendly to the player (design 05 §2, D-076). Off-map cells: false.
    /// For M3-03 (burrowing/surfacing/Bore break-through).
    /// </summary>
    public bool IsHardenedAgainst(byte player, CellCoord cell)
    {
        if (!_ground.Contains(cell)) return false;
        ulong bits = _ground.Hardened[_ground.IndexOf(cell)];
        for (int q = 0; bits != 0; q++, bits >>= 1)
            if ((bits & 1) != 0 && !IsFriendly(player, (byte)q)) return true;
        return false;
    }

    // ASSUMED (M3-04): allies count as the owner's team for gates (IsFriendly); gates need no open/close state in the sim.
    /// <summary>True if <paramref name="building"/> is a live gate that lets <paramref name="player"/> through: the gate's owner is
    /// the player or an ally (D-076: passable for the owner's team, a wall for everyone else).</summary>
    internal bool GateOpenFor(EntityId building, byte player)
    {
        if (building.IsNone || !_buildings.Slots.IsAlive(building)) return false;
        int i = building.Index;
        return BuildingTypeAt(i).Gate && IsFriendly(player, _buildings.Player[i]);
    }

    /// <summary>The passer a path request carries for <paramref name="player"/>: the player itself when the rules have any gate type,
    /// otherwise <see cref="PathService.NoPasser"/> (so requests, the path cache and the "paths" bytes stay as before M3-04).</summary>
    internal byte GatePasser(byte player)
    {
        if (!_gateTypesResolved)
        {
            IReadOnlyList<BuildingType>? types = Setup.Rules.Buildings;
            if (types != null)
                for (int t = 0; t < types.Count; t++) if (types[t].Gate) { _anyGateType = true; break; }
            _gateTypesResolved = true;
        }
        return _anyGateType && player != PathService.NoPasser ? player : PathService.NoPasser;
    }

    private bool _anyGateType;
    private bool _gateTypesResolved;

    /// <summary>Bit p = player slot p (below 64) has an upgrade whose Effect is "grouting".</summary>
    private ulong GroutedPlayerMask()
    {
        if (!_groutingResolved)
        {
            IReadOnlyList<UpgradeType> ups = Setup.Rules.Upgrades;
            for (int u = 0; u < ups.Count && u < RuleCounts.MaxUpgrades; u++)
                if (ups[u].Effect == "grouting") _groutingBits |= 1UL << u;
            _groutingResolved = true;
        }
        if (_groutingBits == 0) return 0;
        ulong mask = 0;
        int n = Math.Min(_players.Count, GroundState.MaxHardeningPlayers);
        for (int p = 0; p < n; p++)
            if ((_players.Upgrades[p] & _groutingBits) != 0) mask |= 1UL << p;
        return mask;
    }

    private void RecomputeHardening(ulong grouted)
    {
        Array.Clear(_ground.Hardened);
        if (grouted == 0) return;
        ulong done = 0;
        for (int p = 0; p < GroundState.MaxHardeningPlayers; p++)
        {
            if ((grouted & (1UL << p)) == 0 || (done & (1UL << p)) != 0) continue;
            // ASSUMED (M3-04): one member's grouting covers the whole team; the seal is every team member's buildings, and the
            // enclosed cells get every member's bit (GroundState doc). A team is computed once, at its lowest grouted slot.
            ulong teamBits = 0;
            foreach (byte q in TeamMembers((byte)p))
                if (q < GroundState.MaxHardeningPlayers) teamBits |= 1UL << q;
            done |= teamBits;
            HardenEnclosure((byte)p, teamBits);
        }
    }

    /// <summary>Flood fill from the map edge for the team of <paramref name="player"/>; sets <paramref name="teamBits"/> on every
    /// enclosed soft cell.</summary>
    private void HardenEnclosure(byte player, ulong teamBits)
    {
        int w = _ground.Width, h = _ground.Height, n = w * h;
        _hardQueue ??= new int[n];
        _hardSeal ??= new int[n];
        _hardReach ??= new int[n];
        int[] queue = _hardQueue, seal = _hardSeal, reach = _hardReach;
        int stamp = ++_hardStamp;

        // Seal cells: footprints of the team's live buildings, in slot order. Gates count (Wall = wall || gate).
        for (int i = 1; i < _buildings.Capacity; i++)
        {
            if (!_buildings.Slots.IsIndexAlive(i) || !IsFriendly(player, _buildings.Player[i])) continue;
            BuildingType bt = BuildingTypeAt(i);
            CellCoord tl = _buildings.Cell[i];
            for (int y = tl.Y; y < tl.Y + bt.FootprintH; y++)
                for (int x = tl.X; x < tl.X + bt.FootprintW; x++)
                    if (x >= 0 && y >= 0 && x < w && y < h) seal[y * w + x] = stamp;
        }

        // Seed: every non-seal edge cell, in index order (array queue, FIFO).
        int head = 0, tail = 0;
        for (int idx = 0; idx < n; idx++)
        {
            int x = idx % w, y = idx / w;
            if (x != 0 && y != 0 && x != w - 1 && y != h - 1) continue;
            if (seal[idx] == stamp || reach[idx] == stamp) continue;
            reach[idx] = stamp; queue[tail++] = idx;
        }
        while (head < tail)
        {
            int cur = queue[head++];
            int cx = cur % w;
            if (cx > 0) Visit(cur - 1);
            if (cx < w - 1) Visit(cur + 1);
            if (cur >= w) Visit(cur - w);
            if (cur < n - w) Visit(cur + w);
        }

        TerrainTable terrain = Setup.Rules.Terrain;
        ulong[] hardened = _ground.Hardened;
        for (int idx = 0; idx < n; idx++)
        {
            if (seal[idx] == stamp || reach[idx] == stamp) continue;
            if (!terrain.Soft(_map.LandAt(new CellCoord(idx % w, idx / w)))) continue;   // design 05 §2: soft cells only
            hardened[idx] |= teamBits;
        }

        void Visit(int j)
        {
            if (seal[j] == stamp || reach[j] == stamp) return;
            reach[j] = stamp; queue[tail++] = j;
        }
    }
}
