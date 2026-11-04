// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Controls;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>How many pips to draw on a garrisonable building: filled for occupants, hollow for free room.</summary>
/// <param name="Filled">Occupied pips.</param>
/// <param name="Total">All pips (the capacity, or the occupant count when the capacity is unknown).</param>
public readonly record struct PipCounts(int Filled, int Total);

/// <summary>Occupant pips on garrisonable buildings (design 08 §3), from <see cref="BuildingView.Occupants"/> against the type's
/// capacity. OWNER: M4-09; called by SceneRenderer (M4-10).</summary>
public static class GarrisonOverlay
{
    private const int PipPx = 10, GapPx = 3;
    private static readonly Color Filled = new(0xE8, 0xD8, 0x6A), Hollow = new(0xE8, 0xD8, 0x6A, 110), Back = new(0, 0, 0, 170);
    private static Texture2D? _pixel;
    private static GraphicsDevice? _device;

    /// <summary>Rules used by the 3-argument <see cref="Draw(SpriteBatch, SimSnapshot, Camera)"/> to know capacities. Without them only
    /// occupied pips are drawn. The game screen sets this once per game.</summary>
    public static SimRules? Rules { get; set; }

    /// <summary>Pip counts for a building: occupants clamped to 0..capacity (capacity 0 or unknown: just the occupants).</summary>
    public static PipCounts Pips(int occupants, int capacity)
    {
        int occ = Math.Max(0, occupants);
        if (capacity <= 0) return new PipCounts(occ, occ);
        return new PipCounts(Math.Min(occ, capacity), capacity);
    }

    /// <summary>Draws the pips for every building in the snapshot that has occupants.</summary>
    public static void Draw(SpriteBatch batch, SimSnapshot snapshot, Camera camera) => Draw(batch, snapshot, camera, Rules);

    /// <summary>Draws the pips; with rules, garrisonable own buildings also show hollow pips for free room.</summary>
    public static void Draw(SpriteBatch batch, SimSnapshot snapshot, Camera camera, SimRules? rules)
    {
        Texture2D pixel = Pixel(batch.GraphicsDevice);
        foreach (BuildingView b in snapshot.Buildings)
        {
            if (b.Ghost) continue;
            int capacity = rules is null ? 0 : GarrisonOrders.CapacityOf(rules, b.TypeIndex);
            if (capacity <= 0 && b.Occupants <= 0) continue;
            PipCounts p = Pips(b.Occupants, capacity);
            if (p.Total == 0) continue;

            float left = b.Cell.X * Camera.CellPx, top = b.Cell.Y * Camera.CellPx;
            float right = left + b.FootprintW * Camera.CellPx, bottom = top + b.FootprintH * Camera.CellPx;
            Rectangle area = camera.ScreenRect(left, top, right, bottom);
            int pip = Math.Max(3, (int)MathF.Round(PipPx * camera.Zoom)), gap = Math.Max(1, (int)MathF.Round(GapPx * camera.Zoom));
            int perRow = Math.Max(1, (area.Width - gap) / (pip + gap));
            int rows = (p.Total + perRow - 1) / perRow;
            int x0 = area.X + gap, y0 = area.Bottom - gap - rows * (pip + gap) + gap;
            Shapes.Fill(batch, pixel, new Rectangle(x0 - gap, y0 - gap, Math.Min(p.Total, perRow) * (pip + gap) + gap, rows * (pip + gap) + gap), Back);
            for (int i = 0; i < p.Total; i++)
            {
                var r = new Rectangle(x0 + i % perRow * (pip + gap), y0 + i / perRow * (pip + gap), pip, pip);
                if (i < p.Filled) Shapes.Fill(batch, pixel, r, Filled);
                else Shapes.Outline(batch, pixel, r, Hollow);
            }
        }
    }

    private static Texture2D Pixel(GraphicsDevice device)
    {
        if (_pixel is null || _device != device || _pixel.IsDisposed)
        {
            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _device = device;
        }

        return _pixel;
    }
}
