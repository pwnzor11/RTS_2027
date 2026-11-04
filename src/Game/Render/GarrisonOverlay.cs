// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>Occupant pips on garrisoned buildings (design 08 §3), from <see cref="BuildingView.Occupants"/>. OWNER: M4-09; called by
/// SceneRenderer (M4-10). Stub (M4-01): draws nothing.</summary>
public static class GarrisonOverlay
{
    /// <summary>Draws the pips for every building in the snapshot that has occupants.</summary>
    public static void Draw(SpriteBatch batch, SimSnapshot snapshot, Camera camera)
    {
        // M4-09 fills
    }
}
