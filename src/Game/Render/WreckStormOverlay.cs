// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Core;
using Rts.Sim.View;

namespace Rts.Game.Render;

/// <summary>One wreck as drawn: its cell and opacity (fading with the ticks it has left) and a seed for its debris shards.</summary>
public readonly record struct WreckDraw(CellCoord Cell, byte Alpha, int Seed);

/// <summary>One ash storm as drawn: the inclusive cell square it covers (clipped to the map) and its opacity.</summary>
public readonly record struct StormDraw(int Slot, CellCoord Min, CellCoord Max, byte Alpha);

/// <summary>
/// Left Behind map layers (M6-09): wrecks as dim debris on their cell, fading out as the wreck decays, and live ash storms as a
/// translucent dust square (Chebyshev radius, as <c>StormCovers</c> in the sim). Placeholder drawing: rects only, no art needed. The plans
/// (<see cref="PlanWrecks"/>, <see cref="PlanStorms"/>) are pure and unit-tested; <see cref="DrawWrecks"/> and <see cref="DrawStorms"/>
/// only paint them. Both layers sit under the fog, so a wreck or storm in the shroud stays hidden.
/// </summary>
public static class WreckStormOverlay
{
    /// <summary>Opacity of a fresh wreck (0..255).</summary>
    public const byte WreckMaxAlpha = 190;

    /// <summary>Opacity of a wreck about to vanish (never fully transparent while it exists).</summary>
    public const byte WreckMinAlpha = 30;

    /// <summary>Opacity of a storm for most of its life.</summary>
    public const byte StormBaseAlpha = 84;

    /// <summary>A storm fades out over its last this many ticks.</summary>
    public const int StormFadeTicks = 40;

    private static readonly Color Debris = new(0x6E, 0x66, 0x58);
    private static readonly Color DebrisDark = new(0x32, 0x2E, 0x28);
    private static readonly Color Dust = new(0xB2, 0xA0, 0x7C);

    /// <summary>
    /// Opacity of a wreck with <paramref name="ticksLeft"/> of <paramref name="totalTicks"/> left: linear from
    /// <see cref="WreckMaxAlpha"/> (fresh) to <see cref="WreckMinAlpha"/> (last tick). 0 for a wreck with no ticks left.
    /// An unknown total (0) draws at full strength.
    /// </summary>
    public static byte WreckAlpha(int ticksLeft, int totalTicks)
    {
        if (ticksLeft <= 0) return 0;
        if (totalTicks <= 0 || ticksLeft >= totalTicks) return WreckMaxAlpha;
        return (byte)(WreckMinAlpha + (WreckMaxAlpha - WreckMinAlpha) * ticksLeft / totalTicks);
    }

    /// <summary>Opacity of a storm with <paramref name="ticksLeft"/> left at <paramref name="tick"/>: a slow pulse of 12 either way, fading over the last <see cref="StormFadeTicks"/>.</summary>
    public static byte StormAlpha(int ticksLeft, int tick)
    {
        if (ticksLeft <= 0) return 0;
        int pulse = ((tick / 6) % 5) switch { 0 => -12, 1 => -6, 2 => 0, 3 => 6, _ => 12 };
        int a = StormBaseAlpha + pulse;
        if (ticksLeft < StormFadeTicks) a = a * ticksLeft / StormFadeTicks;
        return (byte)Math.Clamp(a, 1, 255);
    }

    /// <summary>Fills <paramref name="dest"/> (cleared first) with one entry per wreck, in wreck slot order.</summary>
    /// <param name="view">Any player's view (wrecks are public).</param>
    /// <param name="wreckTicks">The rules' wreck lifetime (<c>Economy.WreckTicks</c>), the fade's scale.</param>
    /// <param name="scratch">Reusable list the view is read into.</param>
    /// <param name="dest">The draw list.</param>
    public static void PlanWrecks(PlayerView view, int wreckTicks, List<WreckView> scratch, List<WreckDraw> dest)
    {
        dest.Clear();
        view.CopyWrecks(scratch);
        foreach (WreckView w in scratch)
            dest.Add(new WreckDraw(w.Cell, WreckAlpha(w.TicksLeft, wreckTicks), w.Slot));
    }

    /// <summary>Fills <paramref name="dest"/> (cleared first) with one entry per live storm, clipped to the map, in storm slot order.</summary>
    /// <param name="view">Any player's view (storms are public).</param>
    /// <param name="tick">The snapshot tick (drives the pulse).</param>
    /// <param name="mapWidth">Map width in cells.</param>
    /// <param name="mapHeight">Map height in cells.</param>
    /// <param name="scratch">Reusable list the view is read into.</param>
    /// <param name="dest">The draw list.</param>
    public static void PlanStorms(PlayerView view, int tick, int mapWidth, int mapHeight, List<StormView> scratch, List<StormDraw> dest)
    {
        dest.Clear();
        view.CopyStorms(scratch);
        foreach (StormView s in scratch)
        {
            int x0 = Math.Max(0, s.Centre.X - s.Radius), y0 = Math.Max(0, s.Centre.Y - s.Radius);
            int x1 = Math.Min(mapWidth - 1, s.Centre.X + s.Radius), y1 = Math.Min(mapHeight - 1, s.Centre.Y + s.Radius);
            if (x1 < x0 || y1 < y0) continue;
            dest.Add(new StormDraw(s.Slot, new CellCoord(x0, y0), new CellCoord(x1, y1), StormAlpha(s.TicksLeft, tick)));
        }
    }

    /// <summary>
    /// The debris shards of a wreck as rectangles in its cell's own zoom-1 pixels (64 x 64): four slabs of different sizes at positions that
    /// depend only on <paramref name="seed"/>, so a wreck always looks the same.
    /// </summary>
    public static IReadOnlyList<Rectangle> Shards(int seed)
    {
        const int cell = Camera.CellPx;
        var rects = new Rectangle[4];
        uint h = (uint)seed * 2654435761u + 12345u;
        for (int i = 0; i < rects.Length; i++)
        {
            h = h * 1664525u + 1013904223u;
            int w = 12 + (int)((h >> 8) % 14), ht = 8 + (int)((h >> 16) % 12);
            int x = 4 + (int)((h >> 4) % (uint)(cell - w - 8)), y = 6 + (int)((h >> 12) % (uint)(cell - ht - 12));
            rects[i] = new Rectangle(x, y, w, ht);
        }
        return rects;
    }

    /// <summary>Draws the wreck debris. Call after the ground layers, before buildings and units.</summary>
    public static void DrawWrecks(SpriteBatch batch, Texture2D pixel, IReadOnlyList<WreckDraw> wrecks, Camera camera)
    {
        foreach (WreckDraw w in wrecks)
        {
            if (w.Alpha == 0) continue;
            float left = w.Cell.X * Camera.CellPx, top = w.Cell.Y * Camera.CellPx;
            Shapes.Fill(batch, pixel, camera.ScreenRect(left + 2, top + 2, left + Camera.CellPx - 2, top + Camera.CellPx - 2),
                Shapes.Alpha(DebrisDark, (byte)(w.Alpha / 2)));
            foreach (Rectangle r in Shards(w.Seed))
                Shapes.Fill(batch, pixel, camera.ScreenRect(left + r.X, top + r.Y, left + r.Right, top + r.Bottom), Shapes.Alpha(Debris, w.Alpha));
        }
    }

    /// <summary>Draws the storm squares. Call after the sprites, before the fog.</summary>
    public static void DrawStorms(SpriteBatch batch, Texture2D pixel, IReadOnlyList<StormDraw> storms, Camera camera)
    {
        foreach (StormDraw s in storms)
        {
            Rectangle r = camera.ScreenRect(s.Min.X * Camera.CellPx, s.Min.Y * Camera.CellPx, (s.Max.X + 1) * Camera.CellPx, (s.Max.Y + 1) * Camera.CellPx);
            Shapes.Fill(batch, pixel, r, Shapes.Alpha(Dust, s.Alpha));
            Shapes.Outline(batch, pixel, r, Shapes.Alpha(Dust, (byte)Math.Min(255, s.Alpha * 2)), 2);
        }
    }
}
