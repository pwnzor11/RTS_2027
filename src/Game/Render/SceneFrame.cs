// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Controls;
using Rts.Game.Sandbox;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// Everything one frame draws. <paramref name="Sim"/> and <paramref name="Rules"/> are null with the demo source,
/// which disables the sim-backed overlays and health bars.
/// </summary>
public readonly record struct SceneFrame(
    SimSnapshot Snapshot,
    IReadOnlyList<RenderUnit> Units,
    IReadOnlyList<LeptonCoord> Projectiles,
    Simulation? Sim,
    SimRules? Rules,
    OverlayToggles Overlays,
    Selection Selection,
    Rectangle? DragRect,
    ControlGroups? Groups = null)
{
    /// <summary>The interpolated world position of a unit as drawn this frame.</summary>
    public bool TryFind(UnitKey key, out Vector2 world)
    {
        foreach (RenderUnit unit in Units)
        {
            if (UnitKey.Of(unit.View) == key)
            {
                world = unit.World;
                return true;
            }
        }
        world = default;
        return false;
    }
}
