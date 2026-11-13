// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>What a remain looks like. Append only.</summary>
public enum RemainKind : byte
{
    /// <summary>A low heap on a dead building's footprint.</summary>
    BuildingRubble = 0,

    /// <summary>A charred hull where a ground vehicle died.</summary>
    VehicleWreck = 1,

    /// <summary>A small dark smudge where a soldier fell (no gore).</summary>
    InfantryMark = 2,

    /// <summary>A dark scorch decal on the ground, under everything else.</summary>
    Scorch = 3,
}

/// <summary>
/// One remain of the dead, view only (D-184): where it lies in zoom-1 world pixels, how big, whose palette, and when it appeared.
/// Never sim state: it is rebuilt from the snapshots the renderer sees and forgotten with the screen.
/// </summary>
/// <param name="Kind">What it is.</param>
/// <param name="X">Centre X in zoom-1 world pixels.</param>
/// <param name="Y">Centre Y in zoom-1 world pixels.</param>
/// <param name="WidthCells">Building rubble: footprint width in cells. Scorch: radius in zoom-1 pixels. Others: 0.</param>
/// <param name="HeightCells">Building rubble: footprint height in cells. Scorch: how flat the ellipse is, in permille of its width. Others: 0.</param>
/// <param name="Faction">Palette index (<see cref="Rts.Sim.Rules.Faction"/> as an int).</param>
/// <param name="Seed">Integer hash that picks the variant, so a remain always looks the same.</param>
/// <param name="Born">Sim tick it appeared on.</param>
public readonly record struct Remain(RemainKind Kind, int X, int Y, int WidthCells, int HeightCells, int Faction, uint Seed, int Born);

/// <summary>Life, fade and caps of the remains. Every number is ASSUMED (docs/design/18_art_guidance.md, D3) and is the owner's to tune. Pure.</summary>
public static class RemainsTiming
{
    /// <summary>Ticks per second of the sim, for the second-based numbers below.</summary>
    private const int Tps = SimConstants.TicksPerSecond;

    /// <summary>Building rubble lasts this many ticks (30 s).</summary>
    // ASSUMED (design 18 D3): 30 s.
    public const int BuildingRubbleTicks = 30 * Tps;

    /// <summary>A vehicle wreck lasts this many ticks (15 s).</summary>
    // ASSUMED (design 18 D3): 15 s.
    public const int VehicleWreckTicks = 15 * Tps;

    /// <summary>A scorch lasts this many ticks (30 s).</summary>
    // ASSUMED (design 18 D3): 30 s.
    public const int ScorchTicks = 30 * Tps;

    /// <summary>An infantry mark lasts this many ticks (10 s).</summary>
    // ASSUMED: shorter than the vehicle wreck, there are many more of them.
    public const int InfantryMarkTicks = 10 * Tps;

    /// <summary>A remain fades in over this many ticks so it settles under the blast instead of popping in.</summary>
    // ASSUMED.
    public const int FadeInTicks = 8;

    /// <summary>A remain keeps full strength for this share of its life (permille), then fades linearly to nothing.</summary>
    // ASSUMED: half the life at full strength, half fading.
    public const int HoldPermille = 500;

    /// <summary>At most this many building rubbles at once (the oldest goes first).</summary>
    // ASSUMED.
    public const int MaxRubble = 16;

    /// <summary>At most this many vehicle wrecks at once.</summary>
    // ASSUMED.
    public const int MaxWrecks = 24;

    /// <summary>At most this many infantry marks at once.</summary>
    // ASSUMED.
    public const int MaxMarks = 48;

    /// <summary>At most this many scorch decals at once (design 18: "cap, e.g. 64").</summary>
    // ASSUMED (design 18).
    public const int MaxScorch = 64;

    /// <summary>Full opacity of each kind (0..255): scorch and marks are translucent smudges.</summary>
    public static int MaxAlpha(RemainKind kind) => kind switch
    {
        RemainKind.Scorch => 190,
        RemainKind.InfantryMark => 150,
        _ => 255,
    };

    /// <summary>Life of a kind in ticks.</summary>
    public static int Life(RemainKind kind) => kind switch
    {
        RemainKind.BuildingRubble => BuildingRubbleTicks,
        RemainKind.VehicleWreck => VehicleWreckTicks,
        RemainKind.InfantryMark => InfantryMarkTicks,
        _ => ScorchTicks,
    };

    /// <summary>Cap of a kind.</summary>
    public static int Cap(RemainKind kind) => kind switch
    {
        RemainKind.BuildingRubble => MaxRubble,
        RemainKind.VehicleWreck => MaxWrecks,
        RemainKind.InfantryMark => MaxMarks,
        _ => MaxScorch,
    };

    /// <summary>
    /// Opacity (0..255) of a remain <paramref name="age"/> ticks old: 0 before it exists, a quick fade in, full strength until
    /// <see cref="HoldPermille"/> of its life, then linear down to 0 at the end of its life.
    /// </summary>
    public static int Alpha(RemainKind kind, int age)
    {
        int life = Life(kind), max = MaxAlpha(kind);
        if (age < 0 || age >= life) return 0;
        int a = max;
        int hold = life * HoldPermille / 1000;
        if (age > hold) a = Math.Max(1, max * (life - age) / Math.Max(1, life - hold));   // never 0 while it exists
        if (age < FadeInTicks) a = a * (age + 1) / (FadeInTicks + 1);
        return Math.Clamp(a, 0, 255);
    }
}

/// <summary>The remains that exist now: a list with a cap per kind (the oldest of a kind is dropped to make room) and expiry by age. Pure.</summary>
public sealed class RemainsBook
{
    private readonly List<Remain> _items = new();

    /// <summary>The remains, oldest first.</summary>
    public IReadOnlyList<Remain> Items => _items;

    /// <summary>Adds a remain, dropping the oldest of its kind when that kind is at its cap.</summary>
    public void Add(Remain remain)
    {
        int cap = RemainsTiming.Cap(remain.Kind), count = 0;
        foreach (Remain r in _items) if (r.Kind == remain.Kind) count++;
        for (int i = 0; count >= cap && i < _items.Count; i++)
        {
            if (_items[i].Kind != remain.Kind) continue;
            _items.RemoveAt(i--);
            count--;
        }

        _items.Add(remain);
    }

    /// <summary>Removes the remains whose life is over at <paramref name="tick"/> (and those from a future tick, after a restart).</summary>
    public void Prune(int tick) => _items.RemoveAll(r => tick < r.Born || tick - r.Born >= RemainsTiming.Life(r.Kind));

    /// <summary>Forgets everything.</summary>
    public void Clear() => _items.Clear();
}

/// <summary>What died: a ground thing the renderer saw vanish together with a sim death event.</summary>
/// <param name="Kind">Building, vehicle or soldier (<see cref="RemainKind.BuildingRubble"/>, <see cref="RemainKind.VehicleWreck"/>, <see cref="RemainKind.InfantryMark"/>).</param>
/// <param name="Player">Owner.</param>
/// <param name="TypeIndex">Rules type index of the dead thing.</param>
/// <param name="Pos">Last position (a building: centre of its footprint).</param>
/// <param name="Cell">A building's top-left footprint cell.</param>
/// <param name="Width">A building's footprint width in cells (0 for units).</param>
/// <param name="Height">A building's footprint height in cells (0 for units).</param>
/// <param name="Facing">A unit's last facing.</param>
/// <param name="Tick">Tick of the snapshot that showed it gone.</param>
/// <param name="Cause">What killed it, for the soldier's death animation; <see cref="DeathCause.Unknown"/> while the sim does not report it (M10-14b).</param>
public readonly record struct DeathRecord(RemainKind Kind, byte Player, int TypeIndex, LeptonCoord Pos, CellCoord Cell, int Width, int Height, byte Facing, int Tick, DeathCause Cause = DeathCause.Unknown);

/// <summary>
/// Finds deaths by comparing snapshots: a building or unit that was in the previous snapshot, is gone now and has a matching sim death
/// event (<see cref="FxKind.BuildingDestroyed"/> inside its footprint, <see cref="FxKind.UnitDestroyed"/> within 1.5 cells of where it
/// was) died. A thing that merely left view, was sold, loaded into a transport or went underground has no such event and leaves nothing.
/// View only; updated once per sim tick. Events of ticks the renderer never saw a snapshot of are lost, so very fast game speeds can skip a remain.
/// </summary>
public sealed class DeathWatcher
{
    /// <summary>How far from a unit's last position its death event may be, in leptons (1.5 cells).</summary>
    public const int UnitMatchLeptons = SimConstants.LeptonsPerCell * 3 / 2;

    private Dictionary<EntityId, BuildingView> _buildings = new();
    private Dictionary<UnitKey, UnitView> _units = new();
    private int _tick = int.MinValue;

    /// <summary>Records <paramref name="snapshot"/> and fills <paramref name="deaths"/> (cleared first) with what died since the last tick.</summary>
    public void Observe(SimSnapshot snapshot, List<DeathRecord> deaths)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(deaths);
        deaths.Clear();
        if (snapshot.Tick == _tick) return;
        bool silent = snapshot.Tick < _tick || _tick == int.MinValue;
        _tick = snapshot.Tick;

        var buildings = new Dictionary<EntityId, BuildingView>();
        foreach (BuildingView b in snapshot.Buildings) if (!b.Ghost) buildings[b.Id] = b;
        var units = new Dictionary<UnitKey, UnitView>();
        foreach (UnitView u in snapshot.Units) units[UnitKey.Of(u)] = u;

        IReadOnlyList<FxEvent> events = snapshot.FxEvents ?? Array.Empty<FxEvent>();
        if (!silent && events.Count > 0)
        {
            bool[] used = new bool[events.Count];
            foreach ((EntityId id, BuildingView b) in _buildings.OrderBy(p => p.Key.Index).ThenBy(p => p.Key.Generation))
            {
                if (buildings.ContainsKey(id)) continue;
                int e = FindBuildingEvent(events, used, b);
                if (e < 0) continue;
                used[e] = true;
                LeptonCoord centre = new((b.Cell.X * 2 + b.FootprintW) * SimConstants.LeptonsPerCell / 2, (b.Cell.Y * 2 + b.FootprintH) * SimConstants.LeptonsPerCell / 2);
                deaths.Add(new DeathRecord(RemainKind.BuildingRubble, b.Player, b.TypeIndex, centre, b.Cell, b.FootprintW, b.FootprintH, 0, snapshot.Tick));
            }

            foreach ((UnitKey key, UnitView u) in _units.OrderBy(p => p.Key.Id.Index).ThenBy(p => p.Key.Id.Generation).ThenBy(p => (int)p.Key.Kind))
            {
                if (units.ContainsKey(key)) continue;
                int e = FindUnitEvent(events, used, u.Position);
                if (e < 0) continue;
                used[e] = true;
                RemainKind kind = u.Kind == UnitKind.Vehicle ? RemainKind.VehicleWreck : RemainKind.InfantryMark;
                deaths.Add(new DeathRecord(kind, u.Player, u.TypeIndex, u.Position, default, 0, 0, u.Facing, snapshot.Tick));
            }
        }

        _buildings = buildings;
        _units = units;
    }

    private static int FindBuildingEvent(IReadOnlyList<FxEvent> events, bool[] used, BuildingView b)
    {
        int lo = SimConstants.LeptonsPerCell / 2;
        int x0 = b.Cell.X * SimConstants.LeptonsPerCell - lo, x1 = (b.Cell.X + b.FootprintW) * SimConstants.LeptonsPerCell + lo;
        int y0 = b.Cell.Y * SimConstants.LeptonsPerCell - lo, y1 = (b.Cell.Y + b.FootprintH) * SimConstants.LeptonsPerCell + lo;
        for (int i = 0; i < events.Count; i++)
        {
            FxEvent e = events[i];
            if (used[i] || e.Kind != FxKind.BuildingDestroyed) continue;
            if (e.Pos.X >= x0 && e.Pos.X <= x1 && e.Pos.Y >= y0 && e.Pos.Y <= y1) return i;
        }

        return -1;
    }

    private static int FindUnitEvent(IReadOnlyList<FxEvent> events, bool[] used, LeptonCoord at)
    {
        int best = -1;
        long bestD = (long)UnitMatchLeptons * UnitMatchLeptons;
        for (int i = 0; i < events.Count; i++)
        {
            FxEvent e = events[i];
            if (used[i] || e.Kind != FxKind.UnitDestroyed) continue;
            long dx = e.Pos.X - at.X, dy = e.Pos.Y - at.Y, d = dx * dx + dy * dy;
            if (d <= bestD) { bestD = d; best = i; }
        }

        return best;
    }
}
