// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim;

namespace Rts.Game.Render;

/// <summary>Quick-Set Foam on the ground (design 08 §4), from <see cref="Rts.Sim.Ground.GroundState.Foam"/>, limited to what
/// <c>viewer</c> can see (-1 = everything). OWNER: M4-09; called by SceneRenderer (M4-10). Stub (M4-01): draws nothing.</summary>
public static class FoamOverlay
{
    /// <summary>Draws foamed cells.</summary>
    public static void Draw(SpriteBatch batch, Simulation sim, int viewer, Camera camera)
    {
        // M4-09 fills
    }
}
