// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>
/// Remembers which units lost health on the latest sim tick so the renderer can draw them with a white flash for two ticks (M10-14).
/// A flash also ends after <see cref="MaxFrames"/> drawn frames, so a paused game does not keep a unit white. View only.
/// </summary>
public sealed class JuiceHitFlash
{
    /// <summary>How many sim ticks a flash lasts (the tick of the hit counts as the first).</summary>
    public const int FlashTicks = 2;

    /// <summary>Longest a flash can last in drawn frames, whatever the sim does.</summary>
    public const int MaxFrames = 8;

    private Dictionary<EntityId, int> _health = new();
    private Dictionary<EntityId, int> _next = new();
    private readonly Dictionary<EntityId, (int Tick, int Frame)> _flashes = new();
    private readonly List<EntityId> _scratch = new();
    private int _tick = int.MinValue;

    /// <summary>Number of flashes remembered (for tests).</summary>
    public int Count => _flashes.Count;

    /// <summary>Reads the units of a snapshot once per tick (a repeat call with the same tick only prunes) and starts a flash for every unit whose health dropped.</summary>
    /// <param name="tick">Snapshot tick.</param>
    /// <param name="units">Units in the snapshot.</param>
    /// <param name="frame">Running count of drawn frames.</param>
    public void Observe(int tick, IReadOnlyList<UnitView> units, int frame)
    {
        if (tick != _tick)
        {
            bool forward = tick > _tick && _tick != int.MinValue;
            if (!forward) _flashes.Clear();   // first look, or the game restarted
            _next.Clear();
            foreach (UnitView u in units)
            {
                _next[u.Id] = u.Health;
                if (forward && _health.TryGetValue(u.Id, out int before) && u.Health < before) _flashes[u.Id] = (tick, frame);
            }

            (_health, _next) = (_next, _health);
            _tick = tick;
        }

        _scratch.Clear();
        foreach (KeyValuePair<EntityId, (int Tick, int Frame)> f in _flashes)
            if (!Active(f.Value, tick, frame)) _scratch.Add(f.Key);
        foreach (EntityId id in _scratch) _flashes.Remove(id);
    }

    /// <summary>Whether unit <paramref name="id"/> is flashing now.</summary>
    public bool IsFlashing(EntityId id, int tick, int frame) => _flashes.TryGetValue(id, out (int Tick, int Frame) f) && Active(f, tick, frame);

    /// <summary>Forgets everything.</summary>
    public void Reset()
    {
        _health.Clear();
        _flashes.Clear();
        _tick = int.MinValue;
    }

    private static bool Active((int Tick, int Frame) f, int tick, int frame) =>
        tick - f.Tick < FlashTicks && tick >= f.Tick && frame - f.Frame < MaxFrames;
}
