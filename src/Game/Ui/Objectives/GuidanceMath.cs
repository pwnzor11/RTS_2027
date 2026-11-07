// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Sim.Core;

namespace Rts.Game.Ui.Objectives;

/// <summary>Where the edge arrow goes for a target: on screen (no arrow), or a point on the view's edge and the direction to the target.</summary>
/// <param name="OnScreen">True when the target is inside the view (draw the ground marker instead).</param>
/// <param name="Edge">The point on the inset view border the arrow tip sits at (the target itself when on screen).</param>
/// <param name="Direction">Unit vector from the view centre towards the target.</param>
public readonly record struct ArrowPlacement(bool OnScreen, Vector2 Edge, Vector2 Direction);

/// <summary>Pure maths for the objective guidance (M11-05): arrow placement, distance text, camera-pan easing.</summary>
public static class GuidanceMath
{
    /// <summary>
    /// Places the arrow for a target at <paramref name="target"/> (screen pixels) in <paramref name="view"/>. The view is inset by
    /// <paramref name="margin"/> so the arrow stays fully visible; a target inside the inset view is on screen.
    /// </summary>
    public static ArrowPlacement Place(Vector2 target, Rectangle view, int margin)
    {
        var centre = new Vector2(view.X + view.Width / 2f, view.Y + view.Height / 2f);
        float halfW = Math.Max(1f, view.Width / 2f - margin), halfH = Math.Max(1f, view.Height / 2f - margin);
        Vector2 d = target - centre;
        if (MathF.Abs(d.X) <= halfW && MathF.Abs(d.Y) <= halfH)
            return new ArrowPlacement(true, target, d.LengthSquared() < 1e-6f ? Vector2.UnitY : Vector2.Normalize(d));
        float tx = d.X == 0f ? float.MaxValue : halfW / MathF.Abs(d.X);
        float ty = d.Y == 0f ? float.MaxValue : halfH / MathF.Abs(d.Y);
        float t = MathF.Min(tx, ty);
        return new ArrowPlacement(false, centre + d * t, Vector2.Normalize(d));
    }

    /// <summary>"34 cells east": the larger of the two axis distances in cells, and one of eight compass words. "here" for the same cell.</summary>
    public static string DistanceText(CellCoord from, CellCoord to)
    {
        int dx = to.X - from.X, dy = to.Y - from.Y;
        int ax = Math.Abs(dx), ay = Math.Abs(dy);
        int n = Math.Max(ax, ay);
        if (n == 0) return "here";
        string ns = dy < 0 ? "north" : "south", ew = dx < 0 ? "west" : "east";
        string dir = ay * 2 < ax ? ew : ax * 2 < ay ? ns : ns + "-" + ew;
        return n + (n == 1 ? " cell " : " cells ") + dir;
    }

    /// <summary>Smoothstep ease of <paramref name="t"/> (clamped to 0..1): slow start, slow end.</summary>
    public static float Ease(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Shortens <paramref name="text"/> to at most <paramref name="max"/> characters, ending in "..." when cut.</summary>
    public static string Trim(string text, int max) => text.Length <= max ? text : max <= 3 ? text[..Math.Max(0, max)] : text[..(max - 3)] + "...";

    /// <summary>
    /// Wraps <paramref name="text"/> into at most <paramref name="maxLines"/> lines of at most <paramref name="maxChars"/> characters at word
    /// breaks. Only when the text does not fit in that many lines is the last line cut with "...".
    /// </summary>
    public static List<string> WrapLabel(string text, int maxChars, int maxLines)
    {
        var lines = new List<string>();
        string line = "";
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > maxChars)
            {
                lines.Add(line);
                line = word;
            }
            else line = line.Length == 0 ? word : line + " " + word;
        }

        if (line.Length > 0) lines.Add(line);
        if (lines.Count > maxLines)
        {
            string rest = string.Join(" ", lines.Skip(maxLines - 1));
            lines = lines.Take(maxLines - 1).ToList();
            lines.Add(rest);
        }

        for (int i = 0; i < lines.Count; i++) lines[i] = Trim(lines[i], maxChars);
        return lines;
    }
}
