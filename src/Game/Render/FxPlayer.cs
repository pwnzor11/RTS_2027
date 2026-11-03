// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sprites;

namespace Rts.Game.Render;

/// <summary>
/// Draws effect animations: the ones a building manifest anchors (<see cref="DrawAnchors"/>) and one-shot effects the
/// game spawns at a world position (<see cref="Spawn"/>, for example explosions). Effects are never house-coloured.
/// SceneRenderer feeds it the snapshot's FxEvents: a detonation or unit death spawns <c>"explosion_small"</c>, a building death
/// <c>"explosion_large"</c> (position = the event position in zoom-1 world pixels).
/// </summary>
public sealed class FxPlayer
{
    private readonly record struct Active(string Kind, Vector2 World, int SpawnTick);

    private readonly List<Active> _active = new();

    /// <summary>Number of spawned effects still playing.</summary>
    public int ActiveCount => _active.Count;

    /// <summary>Starts a one-shot effect.</summary>
    /// <param name="kind">Effect sprite id, for example "explosion_small".</param>
    /// <param name="worldPos">Zoom-1 world position of the effect's origin pixel.</param>
    /// <param name="tick">Tick it starts on.</param>
    public void Spawn(string kind, Vector2 worldPos, int tick)
    {
        ArgumentException.ThrowIfNullOrEmpty(kind);
        _active.Add(new Active(kind, worldPos, tick));
    }

    /// <summary>Draws the spawned effects (dropping finished ones). Kinds missing from the library are dropped.</summary>
    /// <param name="batch">Batch in progress.</param>
    /// <param name="library">Library whose sheets are loaded.</param>
    /// <param name="camera">View.</param>
    /// <param name="tick">Current tick.</param>
    public void Draw(SpriteBatch batch, SpriteLibrary library, Camera camera, int tick)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Active a = _active[i];
            if (!library.Ids.Contains(a.Kind, StringComparer.Ordinal))
            {
                _active.RemoveAt(i);
                continue;
            }
            SpriteManifest m = library.Manifest(a.Kind);
            int frame = FxFrames.Spawned(m, a.SpawnTick, tick);
            if (frame < 0)
            {
                _active.RemoveAt(i);
                continue;
            }
            Vector2 screen = SpriteLayout.UnitOrigin(camera, a.World);
            library.Sheet(a.Kind).Draw(batch, frame, screen, Color.White, camera.Zoom);
        }
    }

    /// <summary>
    /// Draws the effects anchored to the state a building frame belongs to. Smoke is drawn last so it sits on top.
    /// </summary>
    /// <param name="batch">Batch in progress.</param>
    /// <param name="library">Library whose sheets are loaded.</param>
    /// <param name="building">The building's manifest.</param>
    /// <param name="frame">The building frame being drawn (its state picks the anchors).</param>
    /// <param name="origin">Screen position of the building manifest's origin pixel.</param>
    /// <param name="tick">Snapshot tick.</param>
    /// <param name="zoom">Camera zoom.</param>
    public static void DrawAnchors(SpriteBatch batch, SpriteLibrary library, SpriteManifest building, int frame,
        Vector2 origin, int tick, float zoom)
    {
        if (building.Fx.Count == 0) return;
        string? state = FxFrames.StateOf(building, frame);
        if (state is null) return;
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (FxAnchor a in building.FxFor(state))
            {
                bool smoke = a.Kind.StartsWith("smoke", StringComparison.Ordinal);
                if (smoke != (pass == 1) || !library.Ids.Contains(a.Kind, StringComparer.Ordinal)) continue;
                SpriteManifest m = library.Manifest(a.Kind);
                Vector2 at = origin + new Vector2(a.X - building.OriginX, a.Y - building.OriginY) * zoom;
                library.Sheet(a.Kind).Draw(batch, FxFrames.Anchored(m, tick, a.Phase), at, Color.White, zoom);
            }
        }
    }
}
