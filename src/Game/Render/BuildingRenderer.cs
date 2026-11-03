// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sprites;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// Buildings from the sprite library (or a placeholder), drawn with the manifest origin on the footprint's top-left
/// corner. Ghosts show the idle frame under a dark wash with a dashed footprint. Build and damaged states are chosen
/// by <see cref="SpriteStates.BuildingFrame"/>.
/// </summary>
public static class BuildingRenderer
{
    private const int BorderPx = 2;
    private const int DashPx = 6;
    private const int GapPx = 4;
    private const byte GhostWashAlpha = 120;
    private static readonly Color Border = new(0x10, 0x10, 0x10);
    private static readonly Color Wash = new(0, 0, 0);

    /// <summary>Draws one building or ghost.</summary>
    /// <param name="batch">Batch in progress.</param>
    /// <param name="pixel">1x1 white texture.</param>
    /// <param name="sprites">Sprite resolver.</param>
    /// <param name="painter">Sprite painter.</param>
    /// <param name="memory">Renderer memory (build ages).</param>
    /// <param name="tick">Snapshot tick.</param>
    /// <param name="rules">Rules, or null (then no damaged state).</param>
    /// <param name="building">The building to draw.</param>
    /// <param name="camera">View.</param>
    /// <param name="walls">Wall neighbour cache for this snapshot (D-072), or null to draw every wall as the plain idle state.</param>
    public static void Draw(SpriteBatch batch, Texture2D pixel, SpriteResolver sprites, SpritePainter painter,
        RenderMemory memory, int tick, SimRules? rules, BuildingView building, Camera camera, WallTiler? walls = null)
    {
        ResolvedSprite sprite = sprites.ForBuilding(rules, building);
        int maxHealth = rules is not null && (uint)building.TypeIndex < (uint)rules.Buildings.Count
            ? rules.Buildings[building.TypeIndex].MaxHealth
            : 0;
        int age = memory.BuildingAge(building.Id, tick);
        int frame = SpriteStates.BuildingFrame(sprite.Manifest, age, building.Health, maxHealth, building.Ghost, tick);
        if (walls is not null && rules is not null && (uint)building.TypeIndex < (uint)rules.Buildings.Count
            && rules.Buildings[building.TypeIndex].Wall)
        {
            frame = WallFrame(sprite.Manifest, walls, building, frame, maxHealth, tick);
        }

        Vector2 origin = SpriteLayout.BuildingOrigin(camera, building.Cell);
        painter.Draw(batch, sprite, frame, origin, PlayerColours.Base(building.Player), camera.Zoom);
        if (!building.Ghost && sprite.Source == SpriteSource.Library && sprite.Library is not null)
        {
            FxPlayer.DrawAnchors(batch, sprite.Library, sprite.Manifest, frame, origin, tick, camera.Zoom);
        }

        if (!building.Ghost) return;
        float left = building.Cell.X * Camera.CellPx;
        float top = building.Cell.Y * Camera.CellPx;
        Rectangle r = camera.ScreenRect(left, top,
            left + building.FootprintW * Camera.CellPx, top + building.FootprintH * Camera.CellPx);
        Shapes.Fill(batch, pixel, r, Shapes.Alpha(Wash, GhostWashAlpha));
        Shapes.DashedOutline(batch, pixel, r, Shapes.Alpha(Border, 128), BorderPx, DashPx, GapPx);
    }

    /// <summary>
    /// Swaps the resting frame of a wall for the variant its neighbours call for. Build frames are left alone, and so
    /// is a manifest without the variant states.
    /// </summary>
    private static int WallFrame(SpriteManifest manifest, WallTiler walls, BuildingView building, int frame, int maxHealth, int tick)
    {
        int rest = manifest.FrameIndex(SpriteStates.Idle, 0, Math.Max(0, tick));
        int hurt = SpriteStates.HasState(manifest, SpriteStates.Damaged)
            ? manifest.FrameIndex(SpriteStates.Damaged, 0, Math.Max(0, tick))
            : rest;
        if (frame != rest && frame != hurt) return frame;
        bool damaged = !building.Ghost && maxHealth > 0 && building.Health * SpriteStates.DamagedDivisor < maxHealth;
        int tiled = WallTiling.FrameFor(manifest, walls.Mask(building), damaged, tick);
        return tiled >= 0 ? tiled : frame;
    }
}
