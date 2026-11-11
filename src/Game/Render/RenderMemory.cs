// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>
/// What the renderer remembers between snapshots, purely for looks: when each building first appeared (for its build
/// animation) and which units moved since the previous snapshot (for the infantry walk). None of it is simulation
/// state and nothing reads it back into the sim. Updated once per simulation tick, not once per drawn frame.
/// </summary>
public sealed class RenderMemory
{
    /// <summary>
    /// First-seen tick given to buildings that were already there when the renderer started. It is far enough in the
    /// past that they never play their build animation.
    /// </summary>
    public const int Settled = int.MinValue / 2;

    private int _tick;
    private bool _started;
    private Dictionary<EntityId, int> _built = new();
    private Dictionary<UnitKey, LeptonCoord> _positions = new();
    private HashSet<UnitKey> _moving = new();

    /// <summary>Forgets everything: the next snapshot is treated as the first of a new game, so its buildings count as already built.</summary>
    public void Reset()
    {
        _started = false;
        _tick = 0;
        _built = new Dictionary<EntityId, int>();
        _positions = new Dictionary<UnitKey, LeptonCoord>();
        _moving = new HashSet<UnitKey>();
    }

    /// <summary>Records a snapshot. Does nothing when it has the same tick as the last one recorded.</summary>
    /// <param name="snapshot">The snapshot about to be drawn.</param>
    public void Observe(SimSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (_started && snapshot.Tick == _tick) return;
        if (_started && snapshot.Tick < _tick) Reset();   // S18 GAMEREND-01: time ran backwards, so this is a new game

        var built = new Dictionary<EntityId, int>();
        foreach (BuildingView building in snapshot.Buildings)
        {
            // Ghosts are remembered too, so an enemy building that drops back under fog does not rebuild on screen.
            if (_built.TryGetValue(building.Id, out int first)) built[building.Id] = first;
            else built[building.Id] = _started ? snapshot.Tick : Settled;
        }

        var positions = new Dictionary<UnitKey, LeptonCoord>();
        var moving = new HashSet<UnitKey>();
        foreach (UnitView unit in snapshot.Units)
        {
            UnitKey key = UnitKey.Of(unit);
            positions[key] = unit.Position;
            if (_positions.TryGetValue(key, out LeptonCoord before) && before != unit.Position) moving.Add(key);
        }

        _built = built;
        _positions = positions;
        _moving = moving;
        _tick = snapshot.Tick;
        _started = true;
    }

    /// <summary>Ticks since a building was first seen. Large for buildings that were there at the start.</summary>
    /// <param name="id">Building entity id.</param>
    /// <param name="tick">Current snapshot tick.</param>
    public int BuildingAge(EntityId id, int tick) =>
        _built.TryGetValue(id, out int first) ? tick - first : tick - Settled;

    /// <summary>Whether the unit's position changed since the previous snapshot.</summary>
    /// <param name="key">Unit key.</param>
    public bool IsMoving(UnitKey key) => _moving.Contains(key);
}
