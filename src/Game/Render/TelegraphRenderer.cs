// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>
/// The surfacing telegraph (D-075): where something is about to break through the ground, the cell darkens, then cracks, with
/// dust rising and a small shake of the tile for the whole telegraph. It is drawn procedurally from <see cref="TelegraphView"/>
/// (cell, kind, ticks left), so it needs no art. A unit telegraph covers one cell and lasts 3 s; an entrance telegraph covers a
/// 3x3 patch and lasts 5 s. The <c>Alpha</c>, shake, crack and dust helpers are pure functions of the kind and the ticks left,
/// so they are unit-tested; <see cref="Draw"/> only turns them into rectangles and lines. The sim never sees any of this.
/// </summary>
public static class TelegraphRenderer
{
    /// <summary>Length of a unit telegraph in ticks (3 s at 20 Hz; the sim's <c>telegraph_ticks</c> is 60).</summary>
    public const int UnitTotalTicks = 60;

    /// <summary>Length of an entrance telegraph in ticks (5 s at 20 Hz; the sim's <c>entrance_telegraph_ticks</c> is 100).</summary>
    // ASSUMED (M3-08): the look is timed against the design-05 defaults (60 / 100 ticks). A telegraph with more ticks left than
    // that (a re-tuned rule) simply stays at the "just started" look until it reaches the default length.
    public const int EntranceTotalTicks = 100;

    /// <summary>Darkest the ground gets (0..255 black).</summary>
    public const byte MaxDarkenAlpha = 175;

    /// <summary>Most cracks drawn on a unit telegraph.</summary>
    public const int UnitMaxCracks = 5;

    /// <summary>Most cracks drawn on an entrance telegraph.</summary>
    public const int EntranceMaxCracks = 9;

    private static readonly Color CrackColour = new(0x0C, 0x0A, 0x08);
    private static readonly Color DustColour = new(0xB4, 0xA2, 0x7C);

    /// <summary>Total ticks of a telegraph of this kind.</summary>
    public static int TotalTicks(TelegraphKind kind) => kind == TelegraphKind.Entrance ? EntranceTotalTicks : UnitTotalTicks;

    /// <summary>Cells from the telegraph cell to the edge of its patch: 0 for a unit (one cell), 1 for an entrance (3x3).</summary>
    public static int RingCells(TelegraphKind kind) => kind == TelegraphKind.Entrance ? 1 : 0;

    /// <summary>How far through the telegraph it is: 0 when it starts, 1 when the thing breaks through (clamped).</summary>
    public static float Progress(TelegraphKind kind, int ticksLeft)
    {
        int total = TotalTicks(kind);
        return Math.Clamp((total - ticksLeft) / (float)total, 0f, 1f);
    }

    /// <summary>Opacity (0..255) of the black darkening over the cell: noticeable at once, deepest at the end.</summary>
    public static byte DarkenAlpha(TelegraphKind kind, int ticksLeft)
    {
        float p = Progress(kind, ticksLeft);
        return (byte)MathF.Round(MaxDarkenAlpha * (0.25f + 0.75f * p));
    }

    /// <summary>Opacity (0..255) of the cracks: none for the first fifth of the telegraph, fully visible by the 60% mark.</summary>
    public static byte CrackAlpha(TelegraphKind kind, int ticksLeft)
    {
        float p = Progress(kind, ticksLeft);
        float t = Math.Clamp((p - 0.2f) / 0.4f, 0f, 1f);
        return (byte)MathF.Round(235f * t);
    }

    /// <summary>How many cracks are drawn: 0 before the cracks start, growing to the maximum for the kind.</summary>
    public static int CrackCount(TelegraphKind kind, int ticksLeft)
    {
        float p = Progress(kind, ticksLeft);
        if (p < 0.2f) return 0;
        int max = kind == TelegraphKind.Entrance ? EntranceMaxCracks : UnitMaxCracks;
        float t = Math.Clamp((p - 0.2f) / 0.7f, 0f, 1f);
        return Math.Clamp(1 + (int)(t * max), 1, max);
    }

    /// <summary>Largest shake in zoom-1 pixels at this point: 1 px at the start, growing to 3 (unit) or 4 (entrance).</summary>
    public static int ShakeAmplitudePx(TelegraphKind kind, int ticksLeft)
    {
        if (ticksLeft <= 0) return 0;
        float p = Progress(kind, ticksLeft);
        float max = kind == TelegraphKind.Entrance ? 4f : 3f;
        return (int)MathF.Round(1f + (max - 1f) * p);
    }

    /// <summary>
    /// Offset in zoom-1 pixels to draw the patch at this tick: a fixed pseudo-random wobble within the amplitude (the same inputs
    /// always give the same offset, so a replayed game shakes the same). Zero when the telegraph is over.
    /// </summary>
    /// <param name="kind">Telegraph kind.</param>
    /// <param name="ticksLeft">Ticks left on the telegraph.</param>
    /// <param name="tick">Snapshot tick (the animation clock).</param>
    /// <param name="cell">The telegraph cell, so neighbouring telegraphs do not shake in step.</param>
    public static Point ShakeOffsetPx(TelegraphKind kind, int ticksLeft, int tick, CellCoord cell)
    {
        int amp = ShakeAmplitudePx(kind, ticksLeft);
        if (amp == 0) return Point.Zero;
        uint h = Mix((uint)tick * 0x9E3779B1u ^ (uint)cell.X * 0x85EBCA6Bu ^ (uint)cell.Y * 0xC2B2AE35u);
        int span = amp * 2 + 1;
        return new Point((int)(h % (uint)span) - amp, (int)((h >> 8) % (uint)span) - amp);
    }

    /// <summary>Number of dust specks drawn: a few at the start, more as the break-through nears.</summary>
    public static int DustCount(TelegraphKind kind, int ticksLeft)
    {
        float p = Progress(kind, ticksLeft);
        int max = kind == TelegraphKind.Entrance ? 14 : 7;
        return Math.Clamp(2 + (int)(p * (max - 2)), 0, max);
    }

    /// <summary>
    /// One dust speck: an offset from the patch centre in zoom-1 pixels and an opacity. Specks rise and fade on a 24-tick cycle,
    /// each starting at a different phase.
    /// </summary>
    /// <param name="index">Speck number, 0 up to <see cref="DustCount"/> - 1.</param>
    /// <param name="tick">Snapshot tick.</param>
    /// <param name="cell">The telegraph cell.</param>
    /// <param name="spreadPx">Half-width of the area the specks start in, in zoom-1 pixels.</param>
    /// <param name="offset">Offset from the patch centre.</param>
    /// <param name="alpha">Opacity 0..255.</param>
    public static void DustSpeck(int index, int tick, CellCoord cell, int spreadPx, out Vector2 offset, out byte alpha)
    {
        const int cycle = 24;
        uint h = Mix((uint)index * 0x27D4EB2Fu ^ (uint)cell.X * 0x165667B1u ^ (uint)cell.Y * 0xD3A2646Cu);
        int phase = (int)(h % cycle);
        int age = (tick + phase) % cycle;
        float t = age / (float)cycle;
        float x = ((int)((h >> 5) % (uint)(spreadPx * 2 + 1)) - spreadPx) + (age % 5 - 2) * 0.5f;
        float y = ((int)((h >> 13) % (uint)(spreadPx + 1)) - spreadPx / 2) - t * 26f;
        offset = new Vector2(x, y);
        alpha = (byte)MathF.Round(150f * MathF.Sin(MathF.PI * t));
    }

    /// <summary>
    /// The points of one crack as offsets from the patch centre in zoom-1 pixels: a jagged four-point line that runs outwards at
    /// an angle fixed by the crack number and cell.
    /// </summary>
    /// <param name="cell">The telegraph cell.</param>
    /// <param name="crack">Crack number.</param>
    /// <param name="crackCount">Total cracks (spreads the angles evenly).</param>
    /// <param name="reachPx">How far the crack reaches, in zoom-1 pixels.</param>
    public static Vector2[] CrackPath(CellCoord cell, int crack, int crackCount, float reachPx)
    {
        uint h = Mix((uint)crack * 0x9E3779B1u ^ (uint)cell.X * 0x85EBCA6Bu ^ (uint)cell.Y * 0xC2B2AE35u);
        float angle = MathF.Tau * crack / Math.Max(1, crackCount) + ((h & 0xFF) / 255f - 0.5f) * 0.6f;
        var points = new Vector2[4];
        Vector2 at = Vector2.Zero;
        points[0] = at;
        for (int i = 1; i < points.Length; i++)
        {
            float wobble = (((h >> (8 * i % 24)) & 0xFF) / 255f - 0.5f) * 0.9f;
            float step = reachPx / (points.Length - 1);
            at += new Vector2(MathF.Cos(angle + wobble), MathF.Sin(angle + wobble)) * step;
            points[i] = at;
        }
        return points;
    }

    /// <summary>Draws every telegraph in the snapshot that is on screen. Call after the terrain and before the sprites.</summary>
    /// <param name="batch">A begun batch.</param>
    /// <param name="pixel">1x1 white texture.</param>
    /// <param name="telegraphs">The snapshot's telegraphs (already fog-filtered by the sim).</param>
    /// <param name="tick">Snapshot tick.</param>
    /// <param name="camera">View.</param>
    /// <param name="viewport">Map viewport, for culling.</param>
    public static void Draw(SpriteBatch batch, Texture2D pixel, IReadOnlyList<TelegraphView> telegraphs, int tick, Camera camera,
        Viewport viewport)
    {
        foreach (TelegraphView t in telegraphs)
        {
            int ring = RingCells(t.Kind);
            Point shake = ShakeOffsetPx(t.Kind, t.TicksLeft, tick, t.Cell);
            float left = (t.Cell.X - ring) * Camera.CellPx + shake.X;
            float top = (t.Cell.Y - ring) * Camera.CellPx + shake.Y;
            float size = (ring * 2 + 1) * Camera.CellPx;
            Rectangle patch = camera.ScreenRect(left, top, left + size, top + size);
            if (patch.Right < 0 || patch.Bottom < 0 || patch.Left > viewport.Width || patch.Top > viewport.Height) continue;

            // Darkening: full strength in the middle cell, softer on the ring cells of an entrance patch.
            byte dark = DarkenAlpha(t.Kind, t.TicksLeft);
            for (int dy = -ring; dy <= ring; dy++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    float cl = (t.Cell.X + dx) * Camera.CellPx + shake.X, ct = (t.Cell.Y + dy) * Camera.CellPx + shake.Y;
                    byte a = dx == 0 && dy == 0 ? dark : (byte)(dark / 2);
                    Shapes.Fill(batch, pixel, camera.ScreenRect(cl, ct, cl + Camera.CellPx, ct + Camera.CellPx), Shapes.Alpha(Color.Black, a));
                }
            }

            Vector2 centre = new((t.Cell.X + 0.5f) * Camera.CellPx + shake.X, (t.Cell.Y + 0.5f) * Camera.CellPx + shake.Y);
            DrawCracks(batch, pixel, t, centre, ring, camera);
            DrawDust(batch, pixel, t, centre, tick, ring, camera);
        }
    }

    private static void DrawCracks(SpriteBatch batch, Texture2D pixel, TelegraphView t, Vector2 centre, int ring, Camera camera)
    {
        int count = CrackCount(t.Kind, t.TicksLeft);
        if (count == 0) return;
        byte alpha = CrackAlpha(t.Kind, t.TicksLeft);
        Color colour = Shapes.Alpha(CrackColour, alpha);
        float reach = Camera.CellPx * (0.42f + ring * 0.9f);
        int thick = camera.Zoom < 1f ? 1 : 2;
        for (int k = 0; k < count; k++)
        {
            Vector2[] path = CrackPath(t.Cell, k, count, reach);
            for (int i = 1; i < path.Length; i++)
            {
                Shapes.Line(batch, pixel, camera.ScreenFromWorld(centre + path[i - 1]), camera.ScreenFromWorld(centre + path[i]), colour, thick);
            }
        }
    }

    private static void DrawDust(SpriteBatch batch, Texture2D pixel, TelegraphView t, Vector2 centre, int tick, int ring, Camera camera)
    {
        int count = DustCount(t.Kind, t.TicksLeft);
        int spread = (int)(Camera.CellPx * (0.4f + ring * 0.9f));
        int size = Math.Max(2, (int)MathF.Round(5 * camera.Zoom));
        for (int i = 0; i < count; i++)
        {
            DustSpeck(i, tick, t.Cell, spread, out Vector2 offset, out byte alpha);
            if (alpha == 0) continue;
            Vector2 at = camera.ScreenFromWorld(centre + offset);
            Shapes.Fill(batch, pixel, new Rectangle((int)at.X - size / 2, (int)at.Y - size / 2, size, size), Shapes.Alpha(DustColour, alpha));
        }
    }

    // 32-bit integer finaliser (murmur3 fmix), so the animation is a pure function of its inputs.
    private static uint Mix(uint h)
    {
        h ^= h >> 16;
        h *= 0x85EBCA6Bu;
        h ^= h >> 13;
        h *= 0xC2B2AE35u;
        h ^= h >> 16;
        return h;
    }
}
