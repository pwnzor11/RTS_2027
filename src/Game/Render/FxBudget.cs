// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>What a transient effect is, from least to most important when the budget is tight. Append only.</summary>
public enum FxClass : byte
{
    /// <summary>A smoke or dust puff.</summary>
    Smoke = 0,

    /// <summary>Sparks and muzzle flashes.</summary>
    Sparks = 1,

    /// <summary>Thrown debris chunks.</summary>
    Debris = 2,

    /// <summary>An explosion sprite.</summary>
    Explosion = 3,

    /// <summary>A superweapon blast.</summary>
    Superweapon = 4,
}

/// <summary>
/// One effect that wants to start.
/// </summary>
/// <param name="Class">What it is.</param>
/// <param name="Size">0 = small, 1 = medium, 2 = large, 3 = huge (see <see cref="FxBudget.SizeClassOfScale"/>).</param>
/// <param name="OffScreenCells">How far outside the view it starts, in cells; 0 when it is on screen.</param>
/// <param name="Protected">True for what the budget must never drop: a superweapon, the death of a selected unit.</param>
public readonly record struct FxCandidate(FxClass Class, int Size, int OffScreenCells, bool Protected = false);

/// <summary>
/// The one budget for transient effects (design 18, "Effect priority / budget"): how many may start in one step and how many may be alive at once,
/// and which ones are dropped first when there is no room. Priority, from most to least important: protected effects (never dropped, even over
/// the caps), then effects on screen, then ones just off screen, then far ones; within a tier by class (a superweapon, an explosion, debris, sparks,
/// smoke), then by size, then by nearness. Pure: no graphics, no randomness, the same candidates always give the same answer.
/// </summary>
public static class FxBudget
{
    /// <summary>Most effects that may start in one sim step. ASSUMED.</summary>
    public const int FrameCap = 40;

    /// <summary>Most transient effects alive at once, counting explosions, puffs and debris chunks. ASSUMED (design 18 suggests 150 live; the smoke field alone may hold 160, so the total is a little above that).</summary>
    public const int AliveCap = 260;

    /// <summary>An effect this close outside the view (in cells) still counts as "near". ASSUMED.</summary>
    public const int NearCells = 8;

    /// <summary>Priority score: higher survives. Protected effects score <see cref="int.MaxValue"/>.</summary>
    public static int Score(in FxCandidate c)
    {
        if (c.Protected) return int.MaxValue;
        int off = Math.Max(0, c.OffScreenCells);
        int tier = off == 0 ? 2 : off <= NearCells ? 1 : 0;
        return tier * 100_000 + (int)c.Class * 10_000 + Math.Clamp(c.Size, 0, 99) * 100 + (99 - Math.Min(99, off));
    }

    /// <summary>
    /// Decides which of <paramref name="candidates"/> start. Room is the smaller of <paramref name="frameCap"/> and what is left under
    /// <paramref name="aliveCap"/> after the <paramref name="alive"/> effects already playing; protected candidates are always kept (they may use up
    /// more than the room), the rest are kept best score first, ties in list order, until the room is full.
    /// </summary>
    /// <returns>One flag per candidate, true = start it.</returns>
    public static bool[] Admit(IReadOnlyList<FxCandidate> candidates, int alive, int frameCap = FrameCap, int aliveCap = AliveCap)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var keep = new bool[candidates.Count];
        int room = Math.Max(0, Math.Min(frameCap, aliveCap - alive));
        var rest = new List<int>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Protected)
            {
                keep[i] = true;
                room--;
            }
            else rest.Add(i);
        }

        room = Math.Max(0, room);
        if (rest.Count <= room)
        {
            foreach (int i in rest) keep[i] = true;
            return keep;
        }

        rest.Sort((a, b) =>
        {
            int c = Score(candidates[b]).CompareTo(Score(candidates[a]));   // higher score first
            return c != 0 ? c : a.CompareTo(b);                              // then list order
        });
        for (int k = 0; k < room; k++) keep[rest[k]] = true;
        return keep;
    }

    /// <summary>Size class of an explosion of <paramref name="scale"/> (1 = the sprite as drawn): under 1.2 small, under 1.7 medium, else large.</summary>
    public static int SizeClassOfScale(float scale) => scale < 1.2f ? 0 : scale < 1.7f ? 1 : 2;

    /// <summary>How many cells <paramref name="screen"/> lies outside <paramref name="view"/> (0 inside), measured along the larger axis; <paramref name="cellScreenPx"/> is a cell's size on screen.</summary>
    public static int OffScreenCells(Rectangle view, Vector2 screen, float cellScreenPx)
    {
        float dx = Math.Max(Math.Max(view.Left - screen.X, screen.X - view.Right), 0f);
        float dy = Math.Max(Math.Max(view.Top - screen.Y, screen.Y - view.Bottom), 0f);
        float d = Math.Max(dx, dy);
        return d <= 0f ? 0 : Math.Max(1, (int)MathF.Ceiling(d / Math.Max(1f, cellScreenPx)));
    }

    /// <summary>True when <paramref name="at"/> is within <paramref name="radiusLeptons"/> of any of <paramref name="points"/> (squared distances, no square root).</summary>
    public static bool NearAny(LeptonCoord at, IReadOnlyList<LeptonCoord> points, int radiusLeptons)
    {
        long r2 = (long)radiusLeptons * radiusLeptons;
        for (int i = 0; i < points.Count; i++)
        {
            long dx = at.X - points[i].X, dy = at.Y - points[i].Y;
            if (dx * dx + dy * dy <= r2) return true;
        }

        return false;
    }
}
