// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sprites;

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

/// <summary>One sheet frame to draw, with its pixel offset from the unit's origin (zoom 1).</summary>
public readonly record struct UnitLayer(UnitLayerKind Kind, int Frame, int OffsetX, int OffsetY);

/// <summary>
/// Which layers a unit sprite is drawn with, back to front (M3-18). The extra layers are manifest states named
/// <c>shadow</c>, <c>turret</c> and <c>dive</c>; a manifest without them yields exactly one Body layer, the frame the renderer
/// has always used. Pure, so it is tested without a GPU.
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
    public static IReadOnlyList<UnitLayer> For(SpriteManifest manifest, bool infantry, bool moving, byte facing, byte? turretFacing,
        bool diving, int tick)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        int t = Math.Max(0, tick);
        var layers = new List<UnitLayer>(3);
        if (SpriteStates.HasState(manifest, Shadow))
            layers.Add(new UnitLayer(UnitLayerKind.Shadow, manifest.FrameIndex(Shadow, facing, t), manifest.ShadowDx, manifest.ShadowDy));
        if (diving && SpriteStates.HasState(manifest, Dive))
            layers.Add(new UnitLayer(UnitLayerKind.Dive, manifest.FrameIndex(Dive, facing, t), 0, 0));
        else
            layers.Add(new UnitLayer(UnitLayerKind.Body, SpriteStates.UnitFrame(manifest, infantry, moving, facing, tick), 0, 0));
        if (SpriteStates.HasState(manifest, Turret))
            layers.Add(new UnitLayer(UnitLayerKind.Turret, manifest.FrameIndex(Turret, turretFacing ?? facing, t), 0, 0));
        return layers;
    }
}
