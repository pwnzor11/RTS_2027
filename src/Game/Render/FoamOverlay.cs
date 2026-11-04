// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>Quick-Set Foam on the ground (design 08 §4), from <see cref="Rts.Sim.Ground.GroundState.Foam"/>, limited to what
/// <c>viewer</c> can see now (-1 = everything). A pale grey-white tint whose alpha falls with the ticks left. OWNER: M4-09;
/// called by SceneRenderer (M4-10).</summary>
public static class FoamOverlay
{
    /// <summary>Ticks left at which the tint is strongest (the Foam Layer's <c>foam_ticks</c>, tune).</summary>
    public const int FullTicks = 400;
    private const int MinAlpha = 40, MaxAlpha = 150;
    private static readonly Color Tint = new(0xE6, 0xE8, 0xE4);
    private static Texture2D? _pixel;
    private static GraphicsDevice? _device;

    /// <summary>Tint alpha for a cell with <paramref name="ticksLeft"/> of foam: 0 for none, else MinAlpha..MaxAlpha rising with ticks.</summary>
    public static byte AlphaFor(int ticksLeft)
    {
        if (ticksLeft <= 0) return 0;
        int t = Math.Min(ticksLeft, FullTicks);
        return (byte)(MinAlpha + (MaxAlpha - MinAlpha) * t / FullTicks);
    }

    /// <summary>Draws foamed cells.</summary>
    public static void Draw(SpriteBatch batch, Simulation sim, int viewer, Camera camera)
    {
        int[] foam = sim.Ground.Foam;
        int w = sim.Map.Width, h = sim.Map.Height;
        if (foam.Length != w * h) return;
        if (!camera.VisibleCells(w, h, batch.GraphicsDevice.Viewport, out int x0, out int y0, out int x1, out int y1)) return;
        Texture2D pixel = Pixel(batch.GraphicsDevice);
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                byte alpha = AlphaFor(foam[y * w + x]);
                if (alpha == 0) continue;
                if (viewer >= 0 && sim.Vision.VisionAt((byte)viewer, new CellCoord(x, y)) != 2) continue;
                Rectangle r = camera.ScreenRect(x * Camera.CellPx, y * Camera.CellPx, (x + 1) * Camera.CellPx, (y + 1) * Camera.CellPx);
                Shapes.Fill(batch, pixel, r, Shapes.Alpha(Tint, alpha));
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
