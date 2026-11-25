// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>
/// One thrown chunk of a dead vehicle or building, view only (design 18, "Debris / death"). It flies on a ballistic arc from
/// (<see cref="X"/>, <see cref="Y"/>), lands, lies there and fades. All motion is a pure function of its age (see <see cref="DebrisPhysics"/>).
/// </summary>
/// <param name="X">Start X in zoom-1 world pixels (on the ground).</param>
/// <param name="Y">Start Y in zoom-1 world pixels (on the ground).</param>
/// <param name="Vx">Ground speed in X, pixels per tick.</param>
/// <param name="Vy">Ground speed in Y, pixels per tick.</param>
/// <param name="Vz">Launch speed upward, pixels per tick.</param>
/// <param name="Born">Sim tick it was thrown.</param>
/// <param name="Size">Edge length in zoom-1 pixels (2..5).</param>
/// <param name="Rgb">Colour 0xRRGGBB from the dead thing's faction palette.</param>
/// <param name="Triangle">True for a triangular chunk, false for a rectangle.</param>
public readonly record struct DebrisChunk(float X, float Y, float Vx, float Vy, float Vz, int Born, int Size, uint Rgb, bool Triangle);

/// <summary>Where a chunk is at a given age. Pure; floats are fine, this is presentation only.</summary>
public static class DebrisPhysics
{
    /// <summary>Downward pull, pixels per tick squared. ASSUMED.</summary>
    public const float Gravity = 0.9f;

    /// <summary>A landed chunk lies still this many ticks before it fades (1.5 s). ASSUMED.</summary>
    public const int RestTicks = 30;

    /// <summary>It then fades over this many ticks (1 s). ASSUMED.</summary>
    public const int FadeTicks = 20;

    /// <summary>Ticks in the air: up and down again, <c>2 * Vz / g</c>, at least 1.</summary>
    public static int FlightTicks(DebrisChunk c) => Math.Max(1, (int)MathF.Ceiling(2f * c.Vz / Gravity));

    /// <summary>Ticks from the throw until the chunk is gone.</summary>
    public static int LifeTicks(DebrisChunk c) => FlightTicks(c) + RestTicks + FadeTicks;

    /// <summary>
    /// Ground position and height of the chunk <paramref name="age"/> ticks after the throw. <paramref name="z"/> is the height above the ground in
    /// pixels (0 once landed); the chunk is drawn at <c>y - z</c>. It stops moving along the ground when it lands.
    /// </summary>
    public static void Pose(DebrisChunk c, int age, out float x, out float y, out float z)
    {
        float t = Math.Clamp(age, 0, FlightTicks(c));
        x = c.X + c.Vx * t;
        y = c.Y + c.Vy * t;
        z = Math.Max(0f, c.Vz * t - 0.5f * Gravity * t * t);
    }

    /// <summary>Opacity 0..255: 0 before the throw and after the fade, full while flying and resting, then linear down.</summary>
    public static int Alpha(DebrisChunk c, int age)
    {
        int landed = FlightTicks(c) + RestTicks;
        if (age < 0 || age >= landed + FadeTicks) return 0;
        if (age < landed) return 255;
        return Math.Max(1, 255 * (landed + FadeTicks - age) / FadeTicks);
    }
}

/// <summary>The chunks alive now, with a global cap: when it is full the oldest chunk goes first. Pure.</summary>
public sealed class DebrisField
{
    /// <summary>Most chunks alive at once. ASSUMED.</summary>
    public const int MaxChunks = 96;

    private readonly List<DebrisChunk> _chunks = new();

    /// <summary>The chunks, oldest first.</summary>
    public IReadOnlyList<DebrisChunk> Items => _chunks;

    /// <summary>Chunks alive now.</summary>
    public int Count => _chunks.Count;

    /// <summary>Adds a chunk, dropping the oldest when the field is full.</summary>
    public void Add(DebrisChunk chunk)
    {
        if (_chunks.Count >= MaxChunks) _chunks.RemoveAt(0);
        _chunks.Add(chunk);
    }

    /// <summary>Drops the chunks whose life is over at <paramref name="tick"/> (and those from the future, after a restart).</summary>
    public void Prune(int tick) => _chunks.RemoveAll(c => tick < c.Born || tick - c.Born >= DebrisPhysics.LifeTicks(c));

    /// <summary>Forgets everything.</summary>
    public void Clear() => _chunks.Clear();
}

/// <summary>What a death throws. The seed comes from the entity position and tick (never the wall clock and never the sim's random), so a replay looks the same. Pure.</summary>
public static class DebrisPlan
{
    /// <summary>Chunks one vehicle death throws. ASSUMED.</summary>
    public const int VehicleChunks = 5;

    /// <summary>Most chunks one explosion throws, however big the building. ASSUMED.</summary>
    public const int MaxPerExplosion = 8;

    /// <summary>Chunks a building of <paramref name="side"/> cells (its larger footprint side) throws: 4 plus one per cell, capped at <see cref="MaxPerExplosion"/>.</summary>
    public static int BuildingChunks(int side) => Math.Clamp(4 + Math.Max(1, side), VehicleChunks, MaxPerExplosion);

    /// <summary>
    /// Fills <paramref name="into"/> (cleared first) with the chunks of one death: where it happened in zoom-1 world pixels, the size of the
    /// footprint in cells (1 for a vehicle), the faction palette (<see cref="Rts.Sim.Rules.Faction"/> as an int) and the tick.
    /// </summary>
    public static void For(float x, float y, int sideCells, bool building, int faction, int tick, List<DebrisChunk> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        into.Clear();
        int count = building ? BuildingChunks(sideCells) : VehicleChunks;
        (TerrainPalette.Rgb dark, TerrainPalette.Rgb mid, TerrainPalette.Rgb light) = RemainsArt.Tones(faction);
        float reach = building ? 0.55f * Math.Max(1, sideCells) * Camera.CellPx : 14f;   // where the chunks start: inside the footprint
        for (int i = 0; i < count; i++)
        {
            uint h = DecorHash.Mix(0x4442_5253UL, (int)MathF.Round(x), (int)MathF.Round(y), (uint)(tick * 31 + i));
            float angle = (h & 0xFF) * MathF.Tau / 256f;
            float spread = 0.6f + ((h >> 8) & 15) / 15f * 1.6f;               // 0.6 .. 2.2 px per tick on the ground
            float vz = 5f + ((h >> 12) & 15) / 15f * 5f;                        // 5 .. 10 px per tick upward: 11 .. 23 ticks in the air
            float startR = ((h >> 16) & 7) / 7f * reach * 0.5f;
            int size = 2 + (int)((h >> 19) & 3);                                 // 2 .. 5 px
            TerrainPalette.Rgb tone = ((h >> 21) & 7) switch { 0 => new TerrainPalette.Rgb(26, 22, 20), 1 or 2 => dark, 3 or 4 or 5 => mid, _ => light };
            uint rgb = (uint)(tone.R << 16 | tone.G << 8 | tone.B);
            into.Add(new DebrisChunk(
                x + MathF.Cos(angle) * startR, y + MathF.Sin(angle) * startR * 0.6f,
                MathF.Cos(angle) * spread, MathF.Sin(angle) * spread * 0.6f, vz, tick, size, rgb, ((h >> 24) & 1) != 0));
        }
    }
}

/// <summary>Draws the chunks: tinted rectangles and triangles, with a one-pixel dark shadow on the ground under a flying one.</summary>
public static class DebrisPainter
{
    /// <summary>Draws every chunk in view. Call over the ground and the units, under the smoke.</summary>
    public static void Draw(SpriteBatch batch, Texture2D pixel, DebrisField field, Camera camera, Viewport viewport, int tick)
    {
        Rectangle view = viewport.Bounds;
        view.Inflate(24, 24);
        IReadOnlyList<DebrisChunk> items = field.Items;
        for (int i = 0; i < items.Count; i++)
        {
            DebrisChunk c = items[i];
            int age = tick - c.Born;
            int alpha = DebrisPhysics.Alpha(c, age);
            if (alpha <= 0) continue;
            DebrisPhysics.Pose(c, age, out float x, out float y, out float z);
            Vector2 ground = camera.ScreenFromWorld(new Vector2(x, y));
            if (!view.Contains((int)ground.X, (int)(ground.Y - z * camera.Zoom))) continue;
            var colour = new Color((int)(c.Rgb >> 16 & 0xFF), (int)(c.Rgb >> 8 & 0xFF), (int)(c.Rgb & 0xFF));
            if (z > 0.5f)   // a small shadow on the ground, shrinking as the chunk climbs
            {
                int sh = Math.Max(1, (int)MathF.Round(c.Size * camera.Zoom * 0.8f));
                Shapes.Fill(batch, pixel, new Rectangle((int)ground.X, (int)ground.Y, sh, Math.Max(1, sh / 2)), Shapes.Alpha(Color.Black, (byte)(alpha * 90 / 255)));
            }

            float cx = ground.X, cy = ground.Y - z * camera.Zoom;
            int px = Math.Max(1, (int)MathF.Round(c.Size * camera.Zoom));
            if (!c.Triangle)
            {
                Shapes.Fill(batch, pixel, new Rectangle((int)cx - px / 2, (int)cy - px / 2, px, Math.Max(1, px * 2 / 3)), Shapes.Alpha(colour, (byte)alpha));
                continue;
            }

            for (int row = 0; row < px; row++)   // a triangle: a point at the top, widening by a pixel row each step
            {
                int half = 1 + row / 2;
                Shapes.Fill(batch, pixel, new Rectangle((int)cx - half, (int)cy - px / 2 + row, half * 2, 1), Shapes.Alpha(colour, (byte)alpha));
            }
        }
    }
}
