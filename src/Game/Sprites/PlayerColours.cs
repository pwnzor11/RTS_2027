using Microsoft.Xna.Framework;

namespace Rts.Game.Sprites;

/// <summary>
/// House colours per player. Each player has one base colour and an 8-step dark-to-light ramp computed from it:
/// the darkest step is 40 % of the base, the lightest 130 % (clamped to 255), with linear steps between. The ramp
/// replaces the magenta house ramp of the tank sheet (see <see cref="HouseRemap"/>). Debug-view values, not final art.
/// </summary>
public static class PlayerColours
{
    /// <summary>Number of steps in a house ramp. Must match the sheet's house_ramp length.</summary>
    public const int RampSteps = 8;

    /// <summary>Number of player colours defined. Players beyond this wrap around.</summary>
    public const int Count = 4;

    private static readonly Color[] Bases =
    {
        new(0x6F, 0x8A, 0x6A), // 0 Authority: grey-green
        new(0x8B, 0x2E, 0x1A), // 1 Directorate: rust-red
        new(0x2E, 0x5A, 0x8B), // 2 blue ally
        new(0xB8, 0x86, 0x2E), // 3 ochre
    };

    /// <summary>Base colour of a player, used for infantry squares and HUD hints.</summary>
    public static Color Base(int player) => Bases[player % Count];

    /// <summary>The eight ramp colours for a player as 0xRRGGBB, darkest first.</summary>
    public static uint[] Ramp(int player)
    {
        Color b = Base(player);
        var ramp = new uint[RampSteps];
        for (int i = 0; i < RampSteps; i++)
        {
            float t = i / (float)(RampSteps - 1);
            float r = Lerp(b.R * 0.4f, MathF.Min(255f, b.R * 1.3f), t);
            float g = Lerp(b.G * 0.4f, MathF.Min(255f, b.G * 1.3f), t);
            float bl = Lerp(b.B * 0.4f, MathF.Min(255f, b.B * 1.3f), t);
            ramp[i] = ((uint)MathF.Round(r) << 16) | ((uint)MathF.Round(g) << 8) | (uint)MathF.Round(bl);
        }
        return ramp;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
