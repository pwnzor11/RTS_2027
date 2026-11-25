// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sandbox;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// One short dash of track on the ground, view only (design 18, "Tread marks"). Position is in zoom-1 world pixels; the dash lies along
/// <see cref="Facing"/> (0 = north, clockwise, 256 steps).
/// </summary>
/// <param name="X">Centre X in zoom-1 world pixels.</param>
/// <param name="Y">Centre Y in zoom-1 world pixels.</param>
/// <param name="Facing">Direction of travel, 0..255.</param>
/// <param name="Born">Sim tick it was laid.</param>
/// <param name="Heavy">True for a tracked hull (wider, darker); false for wheels.</param>
public readonly record struct TreadMark(float X, float Y, byte Facing, int Born, bool Heavy);

/// <summary>Numbers of the tread marks. Every one is ASSUMED (design 18 gives "8 s fade"; the owner asked for about 20 s) and is the owner's to tune. Pure.</summary>
public static class TreadTiming
{
    /// <summary>Most marks alive at once; the oldest is recycled (design 18: "cap on number of track marks").</summary>
    // ASSUMED.
    public const int MaxMarks = 512;

    /// <summary>A mark lasts this many ticks (20 s at 20 ticks per second).</summary>
    // ASSUMED (owner brief: fade over about 20 s).
    public const int LifeTicks = 20 * SimConstants.TicksPerSecond;

    /// <summary>A mark keeps full strength for this many ticks, then fades linearly to nothing.</summary>
    // ASSUMED.
    public const int HoldTicks = 2 * SimConstants.TicksPerSecond;

    /// <summary>A fresh mark presses in over this many ticks so it does not pop in.</summary>
    // ASSUMED.
    public const int FadeInTicks = 3;

    /// <summary>Distance a vehicle travels between two stamps, in zoom-1 world pixels.</summary>
    // ASSUMED.
    public const float SpacingPx = 12f;

    /// <summary>A vehicle that moved farther than this between two ticks (in cells) is treated as teleported: no stamp, the trail restarts.</summary>
    // ASSUMED.
    public const float JumpCells = 2f;

    /// <summary>Opacity (0..255) of a fresh mark of a tracked hull.</summary>
    // ASSUMED.
    public const int HeavyAlpha = 95;

    /// <summary>Opacity (0..255) of a fresh mark of a wheeled hull (lighter, thinner).</summary>
    // ASSUMED.
    public const int LightAlpha = 60;

    /// <summary>Opacity 0..255 of a mark <paramref name="age"/> ticks old: 0 before it exists and after its life, a short press-in, a hold, then linear fade.</summary>
    public static int Alpha(bool heavy, int age)
    {
        if (age < 0 || age >= LifeTicks) return 0;
        int max = heavy ? HeavyAlpha : LightAlpha;
        int a = age <= HoldTicks ? max : Math.Max(1, max * (LifeTicks - age) / (LifeTicks - HoldTicks));
        if (age < FadeInTicks) a = a * (age + 1) / (FadeInTicks + 1);
        return Math.Clamp(a, 0, 255);
    }

    /// <summary>Whether tracks show on this ground: open, ash and rough ground only, never roads, water, rock or cliffs (same rule as dust).</summary>
    public static bool SoftGround(LandType land) => DustRate.DustyGround(land);
}

/// <summary>The marks that exist now: a ring of fixed size, so a new mark replaces the oldest once it is full. No allocation after construction. Pure.</summary>
public sealed class TreadMarkPool
{
    private readonly TreadMark[] _ring;
    private int _start;   // index of the oldest mark
    private int _count;

    /// <summary>Creates a pool of <paramref name="capacity"/> marks (default <see cref="TreadTiming.MaxMarks"/>).</summary>
    public TreadMarkPool(int capacity = TreadTiming.MaxMarks)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _ring = new TreadMark[capacity];
    }

    /// <summary>Most marks the pool holds.</summary>
    public int Capacity => _ring.Length;

    /// <summary>Marks alive now.</summary>
    public int Count => _count;

    /// <summary>The mark at <paramref name="index"/>, 0 = the oldest.</summary>
    public TreadMark this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            return _ring[(_start + index) % _ring.Length];
        }
    }

    /// <summary>Adds a mark, recycling the oldest when the pool is full.</summary>
    public void Add(TreadMark mark)
    {
        if (_count < _ring.Length)
        {
            _ring[(_start + _count) % _ring.Length] = mark;
            _count++;
            return;
        }

        _ring[_start] = mark;
        _start = (_start + 1) % _ring.Length;
    }

    /// <summary>Drops the oldest marks whose life is over at <paramref name="tick"/>, and everything from the future (after a restart or a load).</summary>
    public void Prune(int tick)
    {
        if (_count > 0 && this[_count - 1].Born > tick)
        {
            Clear();
            return;
        }

        while (_count > 0 && tick - _ring[_start].Born >= TreadTiming.LifeTicks)
        {
            _start = (_start + 1) % _ring.Length;
            _count--;
        }
    }

    /// <summary>Forgets every mark.</summary>
    public void Clear()
    {
        _start = 0;
        _count = 0;
    }
}

/// <summary>
/// Lays the marks: each tracked or wheeled ground vehicle that moves on soft ground stamps two dashes (one per track) every
/// <see cref="TreadTiming.SpacingPx"/> pixels of travel. Reads positions from the snapshots only, once per sim tick; no sim data, no randomness.
/// </summary>
public sealed class TreadMarker
{
    /// <summary>Half the gap between the two tracks of a tracked hull, in zoom-1 world pixels.</summary>
    private const float TrackHalfGap = 11f;

    /// <summary>Half the gap between the two wheel lines of a wheeled hull.</summary>
    private const float WheelHalfGap = 8f;

    private Dictionary<UnitKey, (float X, float Y)> _last = new();
    private Dictionary<UnitKey, (float X, float Y)> _spare = new();
    private int _tick = int.MinValue;

    /// <summary>Records <paramref name="snapshot"/> and adds the marks that are due to <paramref name="pool"/>. Does nothing twice for the same tick.</summary>
    public void Observe(SimSnapshot snapshot, SimRules? rules, TreadMarkPool pool)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(pool);
        if (snapshot.Tick == _tick) return;
        bool silent = snapshot.Tick < _tick || _tick == int.MinValue;
        _tick = snapshot.Tick;
        pool.Prune(snapshot.Tick);

        Dictionary<UnitKey, (float X, float Y)> next = _spare;
        next.Clear();
        IReadOnlyList<UnitView> units = snapshot.Units;
        for (int i = 0; i < units.Count; i++)
        {
            UnitView u = units[i];
            if (u.Kind != UnitKind.Vehicle || rules is null || u.Underground || (uint)u.TypeIndex >= (uint)rules.Vehicles.Count) continue;
            Locomotion loco = rules.Vehicles[u.TypeIndex].Locomotion;
            if (!DustRate.Dusty(loco)) continue;
            var key = UnitKey.Of(u);
            (float X, float Y) at = (u.Position.X * Camera.CellPx / (float)SimConstants.LeptonsPerCell, u.Position.Y * Camera.CellPx / (float)SimConstants.LeptonsPerCell);
            if (silent || !_last.TryGetValue(key, out (float X, float Y) from))
            {
                next[key] = at;
                continue;
            }

            float dx = at.X - from.X, dy = at.Y - from.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > TreadTiming.JumpCells * Camera.CellPx || !OnSoftGround(snapshot, u))
            {
                next[key] = at;   // teleported, or on a road or water: restart the trail from here
                continue;
            }

            if (dist < TreadTiming.SpacingPx)
            {
                next[key] = from;   // keep the old stamp point until enough distance has been covered
                continue;
            }

            Stamp(pool, from, at, dist, u.Facing, snapshot.Tick, loco == Locomotion.Tracked);
            next[key] = at;
        }

        _spare = _last;
        _last = next;
    }

    /// <summary>Forgets every vehicle's last stamp point (a new game).</summary>
    public void Reset()
    {
        _last.Clear();
        _spare.Clear();
        _tick = int.MinValue;
    }

    private static bool OnSoftGround(SimSnapshot snapshot, UnitView u)
    {
        CellCoord cell = u.Position.ToCell();
        int i = cell.Y * snapshot.MapWidth + cell.X;
        return cell.X >= 0 && cell.Y >= 0 && cell.X < snapshot.MapWidth && i < snapshot.Land.Count && TreadTiming.SoftGround(snapshot.Land[i]);
    }

    // one pair of dashes per SpacingPx covered since the last stamp, laid along the path from the old point to the new one
    private static void Stamp(TreadMarkPool pool, (float X, float Y) from, (float X, float Y) to, float dist, byte facing, int tick, bool heavy)
    {
        float half = heavy ? TrackHalfGap : WheelHalfGap;
        double a = facing * Math.PI * 2 / 256;
        float rx = (float)Math.Cos(a), ry = (float)Math.Sin(a);   // to the vehicle's right
        int steps = Math.Clamp((int)(dist / TreadTiming.SpacingPx), 1, 4);
        for (int s = 1; s <= steps; s++)
        {
            float t = s / (float)steps;
            float cx = from.X + (to.X - from.X) * t, cy = from.Y + (to.Y - from.Y) * t + 3;
            pool.Add(new TreadMark(cx - rx * half, cy - ry * half, facing, tick, heavy));
            pool.Add(new TreadMark(cx + rx * half, cy + ry * half, facing, tick, heavy));
        }
    }

    /// <summary>Draws the marks as small dark rotated dashes on the ground. Call after the terrain and ground layers, before the units.</summary>
    public static void Draw(SpriteBatch batch, Texture2D pixel, TreadMarkPool pool, Camera camera, Viewport viewport, int tick)
    {
        Rectangle view = viewport.Bounds;
        view.Inflate(16, 16);
        var origin = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < pool.Count; i++)
        {
            TreadMark m = pool[i];
            int alpha = TreadTiming.Alpha(m.Heavy, tick - m.Born);
            if (alpha <= 0) continue;
            Vector2 s = camera.ScreenFromWorld(new Vector2(m.X, m.Y));
            if (!view.Contains((int)s.X, (int)s.Y)) continue;
            float angle = m.Facing * MathF.Tau / 256f - MathF.PI / 2f;   // the dash's long side runs along the direction of travel
            var size = new Vector2(TreadTiming.SpacingPx * 0.85f * camera.Zoom, (m.Heavy ? 4f : 2.5f) * camera.Zoom);
            batch.Draw(pixel, s, null, Shapes.Alpha(new Color(22, 17, 12), (byte)alpha), angle, origin, size, SpriteEffects.None, 0f);
        }
    }
}
