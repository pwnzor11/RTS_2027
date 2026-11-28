// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Runtime.InteropServices;
using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Sim;

/// <summary>
/// M16 M-28b (D-338 (2)): a spatial index over the LIVE positions of one mobile store, for the scans that read where an actor is now
/// (splash, sonar, stealth reveal, blackout sight). It wraps the M-28 bucket grid (<see cref="SpatialIndex"/>) and keeps a copy of the
/// positions it was built from. Every query first checks that the store still matches that copy (same allocation serial, same positions
/// for every slot, one vectorised compare) and rebuilds the grid when it does not, so the answer always reflects the positions of this
/// instant whoever moved, spawned or removed an actor and without hooking any of the writers. A slot that died after the build stays in
/// its bucket as a candidate: callers already test liveness, exactly as the full walk does. Candidates come back in ascending slot order.
/// Derived scratch: never saved, never hashed, rebuilt from the store on first use (so a load needs nothing).
/// </summary>
internal sealed class LiveSpatialIndex
{
    private readonly SpatialIndex _grid;
    private readonly LeptonCoord[] _built;   // positions of every slot at build time (dead slots included)
    private int _builtSerial = -1;
    private bool _valid;

    /// <summary>Number of grid rebuilds so far (test and bench counter; scratch).</summary>
    internal int Rebuilds;

    public LiveSpatialIndex(int slotCapacity)
    {
        _grid = new SpatialIndex(slotCapacity);
        _built = new LeptonCoord[slotCapacity];
    }

    /// <summary>Slots of <paramref name="store"/> whose live position can lie within <paramref name="radius"/> leptons (Euclidean or Chebyshev) of <paramref name="from"/>, ascending; a span over a reused buffer, valid until the next call.</summary>
    internal ReadOnlySpan<int> Query(MobileStore store, int mapWidthCells, int mapHeightCells, LeptonCoord from, int radius)
    {
        EnsureCurrent(store, mapWidthCells, mapHeightCells);
        return _grid.Query(from, radius);
    }

    private void EnsureCurrent(MobileStore store, int w, int h)
    {
        LeptonCoord[] pos = store.Position;
        int serial = store.Slots.AllocSerial;
        if (_valid && serial == _builtSerial && SamePositions(pos)) return;
        _grid.Build(store.Slots, pos, w, h);
        Array.Copy(pos, _built, Math.Min(pos.Length, _built.Length));
        _builtSerial = serial;
        _valid = true;
        Rebuilds++;
    }

    private bool SamePositions(LeptonCoord[] pos)
    {
        int n = Math.Min(pos.Length, _built.Length);
        // LeptonCoord is two ints with no padding: compare eight bytes per slot.
        return MemoryMarshal.Cast<LeptonCoord, long>(pos.AsSpan(0, n)).SequenceEqual(MemoryMarshal.Cast<LeptonCoord, long>(_built.AsSpan(0, n)));
    }
}
