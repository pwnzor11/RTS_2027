// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sprites;
using Rts.Sim.Numerics;

namespace Rts.Game.Render;

/// <summary>Which layer of a unit sprite a <see cref="UnitLayer"/> is.</summary>
public enum UnitLayerKind
{
    /// <summary>Ground shadow, drawn first.</summary>
    Shadow,
    /// <summary>The hull or the infantry figure.</summary>
    Body,
    /// <summary>Turret, drawn over the body with its own facing.</summary>
    Turret,
    /// <summary>Aircraft dive pose, drawn instead of the body.</summary>
    Dive,
}

/// <summary>Which sprite sheet a layer's frame is taken from.</summary>
public enum UnitLayerSheet
{
    /// <summary>The unit's own sheet (the body sheet, which may carry its shadow and turret as states).</summary>
    Own,
    /// <summary>The companion turret sheet (<c>&lt;id&gt;_turret</c>).</summary>
    TurretCompanion,
    /// <summary>The companion shadow sheet (<c>&lt;id&gt;_shadow</c>).</summary>
    ShadowCompanion,
}

/// <summary>One sheet frame to draw, with its pixel offset from the unit's origin (zoom 1) and the sheet it comes from.</summary>
public readonly record struct UnitLayer(UnitLayerKind Kind, int Frame, int OffsetX, int OffsetY, UnitLayerSheet Sheet = UnitLayerSheet.Own);

/// <summary>
/// A unit's layers back to front, held inline (at most three: shadow, body or dive, turret), so planning a unit allocates nothing.
/// Enumerate it with <c>foreach</c> (a struct enumerator, no boxing) or index it.
/// </summary>
public struct UnitLayers
{
    /// <summary>The most layers a unit can have.</summary>
    public const int Capacity = 3;

    private UnitLayer _a;
    private UnitLayer _b;
    private UnitLayer _c;

    /// <summary>How many layers are set.</summary>
    public int Count { get; private set; }

    /// <summary>The layer at <paramref name="index"/>, back to front.</summary>
    public readonly UnitLayer this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            return index == 0 ? _a : index == 1 ? _b : _c;
        }
    }

    internal void Add(UnitLayer layer)
    {
        switch (Count)
        {
            case 0: _a = layer; break;
            case 1: _b = layer; break;
            case 2: _c = layer; break;
            default: throw new InvalidOperationException("A unit has at most three layers.");
        }
        Count++;
    }

    /// <summary>Struct enumerator for <c>foreach</c>.</summary>
    public readonly Enumerator GetEnumerator() => new(this);

    /// <summary>Allocation-free enumerator over a <see cref="UnitLayers"/>.</summary>
    public struct Enumerator
    {
        private readonly UnitLayers _layers;
        private int _i;

        internal Enumerator(UnitLayers layers)
        {
            _layers = layers;
            _i = -1;
        }

        /// <summary>The current layer.</summary>
        public readonly UnitLayer Current => _layers[_i];

        /// <summary>Steps to the next layer.</summary>
        public bool MoveNext() => ++_i < _layers.Count;
    }
}

/// <summary>
/// Which layers a unit sprite is drawn with, back to front (M3-18). The extra layers are manifest states named
/// <c>shadow</c>, <c>turret</c> and <c>dive</c>; a manifest without them yields exactly one Body layer, the frame the renderer
/// has always used. A body without a turret or shadow state may instead get them from companion sheets (<c>&lt;id&gt;_turret</c>,
/// <c>&lt;id&gt;_shadow</c>): the turret is placed at its hull pivot, rotated by the hull facing. Pure, so it is tested without a GPU.
/// </summary>
public static class UnitLayerPlan
{
    /// <summary>State name of the shadow layer.</summary>
    public const string Shadow = "shadow";
    /// <summary>State name of the turret layer.</summary>
    public const string Turret = "turret";
    /// <summary>State name of the dive layer.</summary>
    public const string Dive = "dive";
    /// <summary>Opacity the shadow layer is drawn at.</summary>
    public const float ShadowAlpha = 0.5f;

    /// <summary>The layers, back to front.</summary>
    /// <param name="manifest">The unit's manifest.</param>
    /// <param name="infantry">True for infantry.</param>
    /// <param name="moving">Whether the unit moved since the previous snapshot.</param>
    /// <param name="facing">Body facing byte.</param>
    /// <param name="turretFacing">Turret facing byte, or null to use the body facing.</param>
    /// <param name="diving">True while an aircraft dives or attacks (uses the dive layer when the manifest has it).</param>
    /// <param name="tick">Simulation tick.</param>
    /// <param name="turretSheet">Companion turret sheet (a <c>turret</c> layer with a pivot), used when the body has no turret state; or null.</param>
    /// <param name="shadowSheet">Companion shadow sheet, used when the body has no shadow state; or null.</param>
    /// <param name="travelLeptons">Total distance the unit has travelled, in leptons; drives the <c>move</c> frames (see <see cref="SpriteStates.UnitFrame"/>).</param>
    public static UnitLayers Plan(SpriteManifest manifest, bool infantry, bool moving, byte facing, byte? turretFacing,
        bool diving, int tick, SpriteManifest? turretSheet = null, SpriteManifest? shadowSheet = null, long travelLeptons = 0)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        int t = Math.Max(0, tick);
        var layers = new UnitLayers();
        if (SpriteStates.HasState(manifest, Shadow))
        {
            layers.Add(new UnitLayer(UnitLayerKind.Shadow, manifest.FrameIndex(Shadow, facing, t), manifest.ShadowDx, manifest.ShadowDy));
        }
        else if (shadowSheet is not null)
        {
            layers.Add(new UnitLayer(UnitLayerKind.Shadow, shadowSheet.FrameIndex(shadowSheet.States[0].Name, facing, t), 0, 0,
                UnitLayerSheet.ShadowCompanion));
        }

        if (diving && SpriteStates.HasState(manifest, Dive))
            layers.Add(new UnitLayer(UnitLayerKind.Dive, manifest.FrameIndex(Dive, facing, t), 0, 0));
        else
            layers.Add(new UnitLayer(UnitLayerKind.Body, SpriteStates.UnitFrame(manifest, infantry, moving, facing, tick, travelLeptons), 0, 0));

        if (SpriteStates.HasState(manifest, Turret))
        {
            layers.Add(new UnitLayer(UnitLayerKind.Turret, manifest.FrameIndex(Turret, turretFacing ?? facing, t), 0, 0));
        }
        else if (turretSheet?.Pivot is { } pivot)
        {
            int hullFacings = manifest.States[0].Facings;
            double hullAngle = hullFacings == 1 ? 0 : 360.0 * Facing.ToFacings(facing, hullFacings) / hullFacings;
            (int dx, int dy) = PivotOffset(pivot, hullAngle, turretSheet.PxPerCell);   // D-275: in the turret sheet's own pixels
            layers.Add(new UnitLayer(UnitLayerKind.Turret, turretSheet.FrameIndex(turretSheet.States[0].Name, turretFacing ?? facing, t),
                dx, dy, UnitLayerSheet.TurretCompanion));
        }
        return layers;
    }

    /// <summary>The layers as a list (allocates; the renderer uses <see cref="Plan"/>). Same content and order as <see cref="Plan"/>.</summary>
    /// <param name="manifest">The unit's manifest.</param>
    /// <param name="infantry">True for infantry.</param>
    /// <param name="moving">Whether the unit moved since the previous snapshot.</param>
    /// <param name="facing">Body facing byte.</param>
    /// <param name="turretFacing">Turret facing byte, or null to use the body facing.</param>
    /// <param name="diving">True while an aircraft dives or attacks.</param>
    /// <param name="tick">Simulation tick.</param>
    /// <param name="turretSheet">Companion turret sheet, or null.</param>
    /// <param name="shadowSheet">Companion shadow sheet, or null.</param>
    /// <param name="travelLeptons">Total distance travelled, in leptons.</param>
    public static IReadOnlyList<UnitLayer> For(SpriteManifest manifest, bool infantry, bool moving, byte facing, byte? turretFacing,
        bool diving, int tick, SpriteManifest? turretSheet = null, SpriteManifest? shadowSheet = null, long travelLeptons = 0)
    {
        UnitLayers planned = Plan(manifest, infantry, moving, facing, turretFacing, diving, tick, turretSheet, shadowSheet, travelLeptons);
        var list = new List<UnitLayer>(planned.Count);
        foreach (UnitLayer layer in planned) list.Add(layer);
        return list;
    }

    /// <summary>
    /// Pixel offset (sheet pixels; zoom 1 for a 64 px sheet) from a unit's origin to a turret's origin, for a hull at <paramref name="hullAngleDeg"/>. The pivot is
    /// in cells at hull facing 0 (east and forward); it is rotated by the hull angle (0 = north, clockwise). Screen y falls with
    /// forward distance by the 40 degree elevation. Height is not used: the turret sheet's origin is already the ground point under
    /// the pivot.
    /// </summary>
    /// <param name="pivot">Turret pivot in cells.</param>
    /// <param name="hullAngleDeg">Hull facing in degrees, 0 = north, clockwise.</param>
    /// <param name="pxPerCell">D-275: the turret sheet's <c>px_per_cell</c> (64 for older sheets, 80 for units rendered at the drawn size); the
    /// result is in that sheet's pixels, so the caller scales it like the sheet (zoom x unit scale x <see cref="SpriteManifest.ArtScale"/>).</param>
    public static (int Dx, int Dy) PivotOffset(TurretPivot pivot, double hullAngleDeg, int pxPerCell = SpriteManifest.WorldPxPerCell)
    {
        double PxPerCell = pxPerCell;
        double theta = hullAngleDeg * Math.PI / 180.0;
        double sin = Math.Sin(theta);
        double cos = Math.Cos(theta);
        double east = pivot.X * cos + pivot.Y * sin;
        double north = -pivot.X * sin + pivot.Y * cos;
        double sinElevation = Math.Sin(40 * Math.PI / 180.0);
        int dx = (int)Math.Round(PxPerCell * east, MidpointRounding.AwayFromZero);
        int dy = (int)Math.Round(-PxPerCell * sinElevation * north, MidpointRounding.AwayFromZero);
        return (dx, dy);
    }
}
