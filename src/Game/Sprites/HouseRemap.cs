namespace Rts.Game.Sprites;

/// <summary>
/// Byte-level sprite helpers with no GraphicsDevice dependency, so they can be checked headlessly later.
/// Pixels are RGBA, 4 bytes each, row-major. Colours are packed 0xRRGGBB.
/// </summary>
public static class HouseRemap
{
    /// <summary>
    /// Replaces every pixel whose RGB equals <paramref name="fromRgb"/>[i] (and whose alpha is above zero) with
    /// <paramref name="toRgb"/>[i]. Alpha is never changed. Returns the number of pixels changed.
    /// </summary>
    /// <exception cref="ArgumentException">The two palettes differ in length.</exception>
    public static int Remap(byte[] rgba, uint[] fromRgb, uint[] toRgb)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        if (fromRgb.Length != toRgb.Length) throw new ArgumentException("Palettes must have the same length.");

        int changed = 0;
        for (int i = 0; i + 3 < rgba.Length; i += 4)
        {
            if (rgba[i + 3] == 0) continue;
            uint rgb = ((uint)rgba[i] << 16) | ((uint)rgba[i + 1] << 8) | rgba[i + 2];
            int index = Array.IndexOf(fromRgb, rgb);
            if (index < 0) continue;

            uint target = toRgb[index];
            rgba[i] = (byte)(target >> 16);
            rgba[i + 1] = (byte)(target >> 8);
            rgba[i + 2] = (byte)target;
            changed++;
        }
        return changed;
    }

    /// <summary>
    /// Multiplies RGB by alpha so the default sprite blend (premultiplied) renders correctly. Fully opaque and
    /// fully transparent pixels are unchanged apart from transparent RGB being zeroed.
    /// </summary>
    public static void Premultiply(byte[] rgba)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        for (int i = 0; i + 3 < rgba.Length; i += 4)
        {
            int alpha = rgba[i + 3];
            if (alpha == 255) continue;
            for (int c = 0; c < 3; c++)
            {
                rgba[i + c] = (byte)((rgba[i + c] * alpha + 127) / 255);
            }
        }
    }
}
