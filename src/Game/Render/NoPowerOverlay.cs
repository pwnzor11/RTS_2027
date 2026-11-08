// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>
/// The blinking "no power" marker (D-170, M11-11): drawn over a building the sim has stopped for lack of power. The test is the
/// sim's own hold-fire rule (Simulation.Combat.cs): the building needs power and its owner's power band is 1 or worse.
/// OWNER: M11-11; called by SceneRenderer.
/// </summary>
public static class NoPowerOverlay
{
    /// <summary>Seconds the icon stays on, then the same again off.</summary>
    public const float HalfPeriodSeconds = 0.5f;

    private const int BoltCols = 6, BoltRows = 8;

    /// <summary>The bolt, row by row ('#' = filled). A zig-zag down to a point at the bottom left.</summary>
    private static readonly string[] Bolt =
    {
        "....##",
        "...##.",
        "..##..",
        ".####.",
        "...##.",
        "..##..",
        ".##...",
        ".#....",
    };

    private static readonly Color BoltYellow = new(0xFF, 0xD8, 0x2A), SlashRed = new(0xE0, 0x2A, 0x2A), Dark = new(0x14, 0x12, 0x10);
    private static readonly Stopwatch Clock = Stopwatch.StartNew();   // render clock: the blink is not sim time

    /// <summary>
    /// Whether a building shows the marker: it needs power, its owner's power band is 1 or worse (power short), it is one of the
    /// buildings the viewer sees as its own, and it is standing (not a ghost, not destroyed).
    /// </summary>
    /// <param name="needsPower">The building type's NeedsPower.</param>
    /// <param name="powerBand">The owner's <c>PowerBand</c> (0 = full power, 1..3 = short).</param>
    /// <param name="isLocalPlayer">The building is the viewer's own (or the view is omniscient).</param>
    /// <param name="isAlive">Standing: not a last-seen ghost and with health left.</param>
    public static bool ShouldShow(bool needsPower, int powerBand, bool isLocalPlayer, bool isAlive) =>
        needsPower && powerBand >= 1 && isLocalPlayer && isAlive;

    /// <summary>
    /// Blink phase at a render-clock time in seconds: on for the first half period, off for the second, repeating
    /// (on 0.5 s, off 0.5 s).
    /// </summary>
    public static bool BlinkOn(float seconds)
    {
        long phase = (long)MathF.Floor(seconds / HalfPeriodSeconds);
        return (phase & 1) == 0;
    }

    /// <summary>
    /// Draws the blinking marker centred above every building that shows it (see <see cref="ShouldShow"/>). Only the viewer's
    /// own buildings are marked; with an omniscient view (Viewer &lt; 0, spectator) every building is considered.
    /// Nothing is drawn in the off half of the blink.
    /// </summary>
    /// <param name="batch">Batch in progress.</param>
    /// <param name="pixel">1x1 white texture.</param>
    /// <param name="frame">The frame; the marker needs its sim (for power and rules) and is skipped without one.</param>
    /// <param name="camera">Camera, for the building rectangles and zoom.</param>
    public static void Draw(SpriteBatch batch, Texture2D pixel, SceneFrame frame, Camera camera)
    {
        Simulation? sim = frame.Sim;
        if (sim is null || !BlinkOn((float)Clock.Elapsed.TotalSeconds)) return;
        SimSnapshot snapshot = frame.Snapshot;
        int s = Math.Max(1, (int)MathF.Round(camera.Zoom * 2f));   // a bolt cell is 2 screen px at zoom 1: about 12 x 16 px
        int w = BoltCols * s, h = BoltRows * s;
        foreach (BuildingView b in snapshot.Buildings)
        {
            bool alive = !b.Ghost && b.Health > 0;
            bool needsPower = alive && sim.RulesOf(b.Player).Buildings[b.TypeIndex].NeedsPower;
            bool local = snapshot.Viewer < 0 || b.Player == snapshot.Viewer;
            if (!ShouldShow(needsPower, sim.PowerBand(b.Player), local, alive)) continue;

            float left = b.Cell.X * Camera.CellPx, top = b.Cell.Y * Camera.CellPx;
            Rectangle area = camera.ScreenRect(left, top, left + b.FootprintW * Camera.CellPx, top + b.FootprintH * Camera.CellPx);
            int x0 = (area.Left + area.Right) / 2 - w / 2, y0 = area.Top - h - s;
            DrawIcon(batch, pixel, x0, y0, s);
        }
    }

    private static void DrawIcon(SpriteBatch batch, Texture2D pixel, int x0, int y0, int s)
    {
        int outline = Math.Max(1, s / 2);
        for (int r = 0; r < BoltRows; r++)
            for (int c = 0; c < BoltCols; c++)
            {
                if (Bolt[r][c] != '#') continue;
                int x = x0 + c * s, y = y0 + r * s;
                for (int dy = -outline; dy <= outline; dy += outline)
                    for (int dx = -outline; dx <= outline; dx += outline)
                        if (dx != 0 || dy != 0) Shapes.Fill(batch, pixel, new Rectangle(x + dx, y + dy, s, s), Dark);
            }
        for (int r = 0; r < BoltRows; r++)
            for (int c = 0; c < BoltCols; c++)
                if (Bolt[r][c] == '#') Shapes.Fill(batch, pixel, new Rectangle(x0 + c * s, y0 + r * s, s, s), BoltYellow);

        // The slash: dark edge first, then red, from the top left corner to the bottom right corner of the icon.
        var a = new Vector2(x0 - s / 2f, y0 - s / 2f);
        var b = new Vector2(x0 + BoltCols * s + s / 2f, y0 + BoltRows * s + s / 2f);
        Shapes.Line(batch, pixel, a, b, Dark, Math.Max(1, s) + 2 * outline);
        Shapes.Line(batch, pixel, a, b, SlashRed, Math.Max(1, s));
    }
}
