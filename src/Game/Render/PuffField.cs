// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sandbox;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// One soft puff of dust or smoke, view only (D-184). Position is in zoom-1 world pixels; the puff rises and grows over its life and its
/// four art frames (<see cref="PuffArt"/>) go from a solid blob to a thin dithered one.
/// </summary>
/// <param name="X">Start X in zoom-1 world pixels.</param>
/// <param name="Y">Start Y in zoom-1 world pixels (it rises from here).</param>
/// <param name="Born">Sim tick it appeared on.</param>
/// <param name="Life">Ticks it lasts.</param>
/// <param name="Rgb">Colour 0xRRGGBB (the art is grey, the colour multiplies it).</param>
/// <param name="MaxAlpha">Opacity at birth, 0..255.</param>
/// <param name="Scale">Size multiplier on the 32 px art frame.</param>
/// <param name="Rise">Pixels it climbs over its life (zoom 1).</param>
public readonly record struct Puff(float X, float Y, int Born, int Life, uint Rgb, int MaxAlpha, float Scale, float Rise);

/// <summary>The puffs alive now, with a cap (the oldest goes first). Pure.</summary>
public sealed class PuffField
{
    /// <summary>Most puffs alive at once. ASSUMED (design 18: effects get a budget, about 150).</summary>
    public const int MaxPuffs = 160;

    private readonly List<Puff> _puffs = new();

    /// <summary>The puffs, oldest first.</summary>
    public IReadOnlyList<Puff> Items => _puffs;

    /// <summary>Adds a puff (dropping the oldest when the field is full).</summary>
    public void Add(Puff puff)
    {
        if (_puffs.Count >= MaxPuffs) _puffs.RemoveAt(0);
        _puffs.Add(puff);
    }

    /// <summary>Drops the puffs whose life is over at <paramref name="tick"/> (and those from the future, after a restart).</summary>
    public void Prune(int tick) => _puffs.RemoveAll(p => tick < p.Born || tick - p.Born >= p.Life);

    /// <summary>Forgets everything.</summary>
    public void Clear() => _puffs.Clear();

    /// <summary>Art frame (0..3) of a puff of <paramref name="age"/> out of <paramref name="life"/> ticks.</summary>
    public static int Frame(int age, int life) => Math.Clamp(age * PuffArt.Frames / Math.Max(1, life), 0, PuffArt.Frames - 1);

    /// <summary>Opacity 0..255: <paramref name="maxAlpha"/> fading linearly to 0 over the life.</summary>
    public static int Alpha(int age, int life, int maxAlpha) => age < 0 || age >= life ? 0 : maxAlpha * (life - age) / Math.Max(1, life);
}

/// <summary>How often a moving ground vehicle kicks up dust. Pure.</summary>
public static class DustRate
{
    /// <summary>Below this speed (leptons per tick, measured from the snapshots) a vehicle raises no dust. ASSUMED.</summary>
    public const int MinSpeed = 4;

    /// <summary>Fastest rate: one puff every this many ticks. ASSUMED.</summary>
    public const int MinPeriod = 3;

    /// <summary>Slowest rate: one puff every this many ticks. ASSUMED.</summary>
    public const int MaxPeriod = 10;

    /// <summary>Numerator of the period: the period is this over the speed, so twice the speed is twice the puffs. ASSUMED.</summary>
    public const int PeriodNumerator = 64;

    /// <summary>Ticks between puffs at <paramref name="speed"/> leptons per tick; 0 = none (too slow).</summary>
    public static int PeriodTicks(int speed) => speed < MinSpeed ? 0 : Math.Clamp(PeriodNumerator / speed, MinPeriod, MaxPeriod);

    /// <summary>
    /// Whether a puff is due at <paramref name="tick"/>, given the previous snapshot's tick. Phase-shifted per vehicle by
    /// <paramref name="phase"/> so a column of tanks does not puff in step; catches up if snapshots skipped ticks.
    /// </summary>
    public static bool Due(int period, int phase, int previousTick, int tick) =>
        period > 0 && Math.Floor((tick + phase) / (double)period) > Math.Floor((previousTick + phase) / (double)period);

    /// <summary>Whether dust rises from this ground: open, ash and rough ground only (not roads, water, rock or cliffs). Design 18.</summary>
    public static bool DustyGround(LandType land) => land is LandType.Clear or LandType.Ash or LandType.Rough;

    /// <summary>Whether a locomotion raises dust: wheels and tracks, not hover, air, burrow, naval or foot.</summary>
    public static bool Dusty(Locomotion locomotion) => locomotion is Locomotion.Wheeled or Locomotion.Tracked;
}

/// <summary>
/// Puts dust puffs behind moving wheeled and tracked vehicles. Reads speed from the change of position between snapshots, so it needs
/// no sim data; updated once per sim tick.
/// </summary>
public sealed class DustEmitter
{
    /// <summary>Puff life in ticks (1 s). ASSUMED.</summary>
    public const int PuffLife = 20;

    private Dictionary<UnitKey, LeptonCoord> _positions = new();
    private int _tick = int.MinValue;

    /// <summary>Records <paramref name="snapshot"/> and adds the puffs that are due to <paramref name="field"/>.</summary>
    /// <param name="snapshot">The snapshot being drawn.</param>
    /// <param name="rules">Rules (vehicle locomotion), or null for no dust.</param>
    /// <param name="field">Where the puffs go.</param>
    /// <param name="dust">Dust colour 0xRRGGBB (the theater's mood dust).</param>
    public void Observe(SimSnapshot snapshot, SimRules? rules, PuffField field, uint dust)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(field);
        int previousTick = _tick;
        if (snapshot.Tick == _tick) return;
        bool silent = snapshot.Tick < _tick || _tick == int.MinValue;
        _tick = snapshot.Tick;

        var positions = new Dictionary<UnitKey, LeptonCoord>();
        foreach (UnitView u in snapshot.Units)
        {
            if (u.Kind != UnitKind.Vehicle) continue;
            var key = UnitKey.Of(u);
            positions[key] = u.Position;
            if (silent || rules is null || u.Underground || (uint)u.TypeIndex >= (uint)rules.Vehicles.Count) continue;
            VehicleType type = rules.Vehicles[u.TypeIndex];
            if (!DustRate.Dusty(type.Locomotion) || !_positions.TryGetValue(key, out LeptonCoord before)) continue;
            int dt = snapshot.Tick - previousTick;
            if (dt <= 0) continue;
            long dx = u.Position.X - before.X, dy = u.Position.Y - before.Y;
            int speed = (int)(Math.Sqrt(dx * dx + dy * dy) / dt);
            int period = DustRate.PeriodTicks(speed);
            if (!DustRate.Due(period, u.Id.Index * 7, previousTick, snapshot.Tick)) continue;
            CellCoord cell = u.Position.ToCell();
            int i = cell.Y * snapshot.MapWidth + cell.X;
            if (cell.X < 0 || cell.Y < 0 || cell.X >= snapshot.MapWidth || i >= snapshot.Land.Count || !DustRate.DustyGround(snapshot.Land[i])) continue;
            field.Add(Make(u, type, snapshot.Tick, dust));
        }

        _positions = positions;
    }

    private static Puff Make(UnitView u, VehicleType type, int tick, uint dust)
    {
        // behind the hull: against the facing (0 = north, clockwise, 256 steps), a little to either side
        double a = u.Facing * Math.PI * 2 / 256;
        float bx = -(float)Math.Sin(a), by = (float)Math.Cos(a);
        uint h = DecorHash.Mix(0x4455_5354UL, u.Id.Index, tick, (uint)u.TypeIndex);
        float side = ((int)(h & 15) - 7.5f) * 1.2f;
        float back = 36 + (int)((h >> 4) & 7);
        float x = u.Position.X * Camera.CellPx / (float)SimConstants.LeptonsPerCell + bx * back - by * side;
        float y = u.Position.Y * Camera.CellPx / (float)SimConstants.LeptonsPerCell + by * back * 0.75f + bx * side * 0.75f;
        float scale = type.Locomotion == Locomotion.Tracked ? 1.0f : 0.75f;
        return new Puff(x, y + 6, tick, PuffLife, dust, 190, scale, 12);
    }
}
