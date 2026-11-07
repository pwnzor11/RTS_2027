// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rts.Game.Render;

/// <summary>What an order mark confirms: a move (green) or an attack (red).</summary>
public enum OrderMarkKind
{
    /// <summary>Move, enter, garrison: a green ring.</summary>
    Move,

    /// <summary>Attack or attack-move: a red ring.</summary>
    Attack,
}

/// <summary>
/// Short expanding rings at the target of an order (audit O1) and the rally flag of a selected production building (O4).
/// The timing is pure (<see cref="Progress"/>, <see cref="Radius"/>, <see cref="Alpha"/>) and unit-tested; time is the screen's own
/// frame clock (<see cref="Advance"/>), so it needs no wall clock and runs even while the sim is paused.
/// </summary>
public sealed class OrderFeedback
{
    /// <summary>How long a ring lives, in seconds.</summary>
    public const float DurationSeconds = 0.4f;

    /// <summary>Radius of a ring at the end of its life, in world pixels (one cell is 64).</summary>
    public const float MaxRadiusWorldPx = 28f;

    private const int MaxMarks = 16;
    private static readonly Color MoveColour = new(0x50, 0xE0, 0x60);
    private static readonly Color AttackColour = new(0xF0, 0x40, 0x38);
    private static readonly Color FlagColour = new(0xF0, 0xD0, 0x40);

    private readonly List<(Vector2 World, OrderMarkKind Kind, float Age)> _marks = new();

    /// <summary>Number of rings alive.</summary>
    public int Count => _marks.Count;

    /// <summary>Progress 0..1 of a ring of the given age (clamped).</summary>
    public static float Progress(float ageSeconds) => Math.Clamp(ageSeconds / DurationSeconds, 0f, 1f);

    /// <summary>Ring radius at the given age: grows from 25% to 100% of <paramref name="maxRadius"/>, easing out.</summary>
    public static float Radius(float ageSeconds, float maxRadius)
    {
        float p = Progress(ageSeconds);
        float eased = 1f - (1f - p) * (1f - p);
        return maxRadius * (0.25f + 0.75f * eased);
    }

    /// <summary>Ring opacity 0..1 at the given age: fully opaque at the start, gone at the end.</summary>
    public static float Alpha(float ageSeconds) => 1f - Progress(ageSeconds);

    /// <summary>Queues a ring at a world-pixel point.</summary>
    public void Add(Vector2 world, OrderMarkKind kind)
    {
        if (_marks.Count >= MaxMarks) _marks.RemoveAt(0);
        _marks.Add((world, kind, 0f));
    }

    /// <summary>Ages the rings by <paramref name="seconds"/> and drops the finished ones.</summary>
    public void Advance(float seconds)
    {
        for (int i = _marks.Count - 1; i >= 0; i--)
        {
            var m = _marks[i];
            m.Age += seconds;
            if (m.Age >= DurationSeconds) _marks.RemoveAt(i);
            else _marks[i] = m;
        }
    }

    /// <summary>Draws the live rings (radius scales with zoom).</summary>
    public void Draw(SpriteBatch batch, Texture2D pixel, Camera camera)
    {
        foreach (var (world, kind, age) in _marks)
        {
            Color c = Shapes.Alpha(kind == OrderMarkKind.Attack ? AttackColour : MoveColour, (byte)(255 * Alpha(age)));
            Shapes.Circle(batch, pixel, camera.ScreenFromWorld(world), Radius(age, MaxRadiusWorldPx) * camera.Zoom, c, 2, 24);
        }
    }

    /// <summary>Draws a flag at <paramref name="rallyCell"/> and a thin line to it from <paramref name="buildingCentreWorld"/> (O4).</summary>
    public static void DrawRally(SpriteBatch batch, Texture2D pixel, Camera camera, Vector2 buildingCentreWorld, Rts.Sim.Core.CellCoord rallyCell)
    {
        Vector2 from = camera.ScreenFromWorld(buildingCentreWorld);
        Vector2 to = camera.ScreenFromWorld(Camera.CentreOf(rallyCell));
        Shapes.Line(batch, pixel, from, to, Shapes.Alpha(FlagColour, 140), 1);
        int h = (int)MathF.Max(14f, 22f * camera.Zoom), w = h * 2 / 3;
        batch.Draw(pixel, new Rectangle((int)to.X, (int)to.Y - h, 2, h), FlagColour);                 // pole
        batch.Draw(pixel, new Rectangle((int)to.X + 2, (int)to.Y - h, w, h / 2), FlagColour);         // cloth
        Shapes.Outline(batch, pixel, new Rectangle((int)to.X + 2, (int)to.Y - h, w, h / 2), new Color(0, 0, 0, 200));
    }
}
