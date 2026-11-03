using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rts.Game.Render;

/// <summary>
/// Flat-colour primitives drawn with a 1x1 white texture and a batch that is already begun. No content pipeline:
/// every shape is a rectangle or a short line segment.
/// </summary>
public static class Shapes
{
    /// <summary>
    /// A colour at the given opacity. SpriteBatch blends premultiplied colour, so the RGB values are scaled here.
    /// </summary>
    public static Color Alpha(Color colour, byte alpha) =>
        new(colour.R * alpha / 255, colour.G * alpha / 255, colour.B * alpha / 255, (int)alpha);

    /// <summary>Fills a rectangle.</summary>
    public static void Fill(SpriteBatch batch, Texture2D pixel, Rectangle r, Color colour) =>
        batch.Draw(pixel, r, colour);

    /// <summary>Draws a border of <paramref name="thickness"/> pixels inside the rectangle.</summary>
    public static void Outline(SpriteBatch batch, Texture2D pixel, Rectangle r, Color colour, int thickness = 1)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        int t = Math.Min(thickness, Math.Min(r.Width, r.Height));
        batch.Draw(pixel, new Rectangle(r.Left, r.Top, r.Width, t), colour);
        batch.Draw(pixel, new Rectangle(r.Left, r.Bottom - t, r.Width, t), colour);
        batch.Draw(pixel, new Rectangle(r.Left, r.Top, t, r.Height), colour);
        batch.Draw(pixel, new Rectangle(r.Right - t, r.Top, t, r.Height), colour);
    }

    /// <summary>A border made of dashes that run along each edge.</summary>
    public static void DashedOutline(SpriteBatch batch, Texture2D pixel, Rectangle r, Color colour, int thickness,
        int dash, int gap)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        int period = dash + gap;
        for (int x = r.Left; x < r.Right; x += period)
        {
            int w = Math.Min(dash, r.Right - x);
            batch.Draw(pixel, new Rectangle(x, r.Top, w, thickness), colour);
            batch.Draw(pixel, new Rectangle(x, r.Bottom - thickness, w, thickness), colour);
        }
        for (int y = r.Top; y < r.Bottom; y += period)
        {
            int h = Math.Min(dash, r.Bottom - y);
            batch.Draw(pixel, new Rectangle(r.Left, y, thickness, h), colour);
            batch.Draw(pixel, new Rectangle(r.Right - thickness, y, thickness, h), colour);
        }
    }

    /// <summary>A straight line from a to b, drawn as a thick run of pixels along the line.</summary>
    public static void Line(SpriteBatch batch, Texture2D pixel, Vector2 a, Vector2 b, Color colour, int thickness = 1)
    {
        Vector2 delta = b - a;
        float length = delta.Length();
        if (length < 0.5f) return;
        float angle = MathF.Atan2(delta.Y, delta.X);
        batch.Draw(pixel, a, null, colour, angle, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    /// <summary>A circle outline as <paramref name="segments"/> line pieces.</summary>
    public static void Circle(SpriteBatch batch, Texture2D pixel, Vector2 centre, float radius, Color colour,
        int thickness = 1, int segments = 64)
    {
        if (radius < 1f) return;
        Vector2 previous = centre + new Vector2(radius, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = MathF.Tau * i / segments;
            Vector2 next = centre + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
            Line(batch, pixel, previous, next, colour, thickness);
            previous = next;
        }
    }
}
