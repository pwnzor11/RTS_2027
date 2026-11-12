// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>One wake puff: where it was left, which way the ship was heading (unit vector), and the tick it was left.</summary>
public readonly record struct WakePuff(Vector2 Pos, Vector2 Dir, int Born);

/// <summary>
/// The short fading wake behind moving ships (M10-13). A puff is left each time a ship has moved <see cref="MinStepPx"/> since its last puff,
/// lives <see cref="LifeTicks"/> ticks and fades. Pure bookkeeping (no drawing, no RNG); view only.
/// </summary>
public sealed class WaterWake
{
    /// <summary>Ticks a puff lives.</summary>
    public const int LifeTicks = 24;

    /// <summary>A ship leaves a puff after moving this many zoom-1 pixels.</summary>
    public const float MinStepPx = 10f;

    /// <summary>Most puffs kept at once.</summary>
    public const int MaxPuffs = 400;

    /// <summary>Opacity of a fresh puff.</summary>
    public const byte MaxAlpha = 110;

    private readonly List<WakePuff> _puffs = new();
    private readonly Dictionary<EntityId, Vector2> _last = new();
    private int _tick = -1;

    /// <summary>The live puffs, oldest first.</summary>
    public IReadOnlyList<WakePuff> Puffs => _puffs;

    /// <summary>Opacity of a puff of the given age: <see cref="MaxAlpha"/> falling linearly to 0 at <see cref="LifeTicks"/>.</summary>
    public static byte AlphaAt(int age) =>
        age < 0 || age >= LifeTicks ? (byte)0 : (byte)(MaxAlpha * (LifeTicks - age) / LifeTicks);

    /// <summary>Half-size of a puff in zoom-1 pixels: it grows as it ages.</summary>
    public static float RadiusAt(int age) => 2f + Math.Clamp(age, 0, LifeTicks) * 0.25f;

    /// <summary>
    /// Advances to <paramref name="tick"/> (once per tick; repeats and the same tick are ignored) and leaves puffs for the ships that moved.
    /// Pass only ships that are on the water and visible. A tick that goes backwards (a load) clears everything.
    /// </summary>
    public void Update(int tick, IReadOnlyList<(EntityId Id, Vector2 World)> ships)
    {
        if (tick == _tick) return;
        if (tick < _tick) Reset();
        _tick = tick;
        _puffs.RemoveAll(p => tick - p.Born >= LifeTicks);
        if (_last.Count > 2048) _last.Clear();
        foreach ((EntityId id, Vector2 world) in ships)
        {
            if (!_last.TryGetValue(id, out Vector2 last)) { _last[id] = world; continue; }
            Vector2 delta = world - last;
            float len = delta.Length();
            if (len < MinStepPx) continue;
            _last[id] = world;
            if (len > Camera.CellPx * 3) continue;   // a jump (teleport, snap), not sailing
            if (_puffs.Count >= MaxPuffs) _puffs.RemoveAt(0);
            _puffs.Add(new WakePuff(last + delta * 0.5f, delta / len, tick));
        }
    }

    /// <summary>Forgets every puff and every remembered position.</summary>
    public void Reset()
    {
        _puffs.Clear();
        _last.Clear();
        _tick = -1;
    }
}
