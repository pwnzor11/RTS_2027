using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>Projectiles as small bright dots at their positions.</summary>
public static class ProjectileRenderer
{
    private const int DotPx = 4;
    private static readonly Color Bright = new(0xFF, 0xF4, 0xA0);

    /// <summary>Draws one dot per projectile.</summary>
    public static void Draw(SpriteBatch batch, Texture2D pixel, IReadOnlyList<LeptonCoord> projectiles, Camera camera)
    {
        foreach (LeptonCoord position in projectiles)
        {
            Vector2 screen = camera.ScreenFromWorld(Camera.WorldFromLepton(position));
            int x = (int)MathF.Round(screen.X);
            int y = (int)MathF.Round(screen.Y);
            Shapes.Fill(batch, pixel, new Rectangle(x - DotPx / 2, y - DotPx / 2, DotPx, DotPx), Bright);
        }
    }
}
