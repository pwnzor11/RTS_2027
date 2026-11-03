// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Sprites;

/// <summary>
/// Byte-level house-colour maths with no GraphicsDevice dependency. A sheet is split once into a BASE image (every
/// non-house pixel, house pixels made transparent) and a MASK image (house pixels only, as greyscale). The GPU draws
/// BASE untinted, then MASK tinted with the house colour, which gives <c>out = base + mask * colour / 255</c>.
/// <see cref="Compose"/> is the CPU replica of that two-pass blend, used by the proof and by the tests.
/// Pixels are RGBA, 4 bytes each, row-major, premultiplied where noted. Colours are packed 0xRRGGBB.
/// </summary>
public static class HouseSplit
{
    /// <summary>
    /// Greyscale value for each ramp shade: Rec. 601 luma, scaled so the lightest shade is 255. A house pixel of
    /// the lightest shade therefore shows the full house colour, and darker shades show a darker share of it.
    /// </summary>
    /// <param name="ramp">Ramp shades as 0xRRGGBB, darkest first.</param>
    public static byte[] ShadeGreys(uint[] ramp)
    {
        ArgumentNullException.ThrowIfNull(ramp);
        var luma = new int[ramp.Length];
        int max = 0;
        for (int i = 0; i < ramp.Length; i++)
        {
            uint rgb = ramp[i];
            int r = (int)((rgb >> 16) & 0xFF);
            int g = (int)((rgb >> 8) & 0xFF);
            int b = (int)(rgb & 0xFF);
            luma[i] = (299 * r + 587 * g + 114 * b + 500) / 1000;
            max = Math.Max(max, luma[i]);
        }

        var greys = new byte[ramp.Length];
        for (int i = 0; i < ramp.Length; i++)
        {
            greys[i] = max == 0 ? (byte)0 : (byte)((luma[i] * 255 + max / 2) / max);
        }
        return greys;
    }

    /// <summary>
    /// Splits a sheet into BASE and MASK. Only pixels with alpha above zero whose RGB equals a ramp shade are house
    /// pixels; everything else is copied to BASE and MASK is cleared there. Both outputs must be the same length as
    /// <paramref name="source"/>. Alpha is kept on house pixels in MASK and cleared in BASE.
    /// </summary>
    /// <returns>The number of house pixels found.</returns>
    /// <exception cref="ArgumentException">The arrays differ in length or are not whole RGBA pixels.</exception>
    public static int Split(byte[] source, uint[] ramp, byte[] baseRgba, byte[] maskRgba)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(ramp);
        ArgumentNullException.ThrowIfNull(baseRgba);
        ArgumentNullException.ThrowIfNull(maskRgba);
        if (source.Length % 4 != 0) throw new ArgumentException("Source must be whole RGBA pixels.", nameof(source));
        if (baseRgba.Length != source.Length || maskRgba.Length != source.Length)
        {
            throw new ArgumentException("BASE and MASK must be the same length as the source.");
        }

        byte[] greys = ShadeGreys(ramp);
        int houseCount = 0;
        for (int i = 0; i < source.Length; i += 4)
        {
            Array.Copy(source, i, baseRgba, i, 4);
            Array.Clear(maskRgba, i, 4);
            if (source[i + 3] == 0) continue;

            uint rgb = ((uint)source[i] << 16) | ((uint)source[i + 1] << 8) | source[i + 2];
            int shade = Array.IndexOf(ramp, rgb);
            if (shade < 0) continue;

            byte grey = greys[shade];
            maskRgba[i] = grey;
            maskRgba[i + 1] = grey;
            maskRgba[i + 2] = grey;
            maskRgba[i + 3] = source[i + 3];
            baseRgba[i] = 0;
            baseRgba[i + 1] = 0;
            baseRgba[i + 2] = 0;
            baseRgba[i + 3] = 0;
            houseCount++;
        }
        return houseCount;
    }

    /// <summary>
    /// CPU replica of drawing BASE with white, then MASK with <paramref name="tint"/>, both alpha blended
    /// (premultiplied source, <c>src + dst * (1 - srcAlpha)</c>). Inputs must be premultiplied. Each 8-bit product is
    /// rounded to nearest. The GPU's float-to-byte step may differ by one level on some pixels; the house pixels of
    /// the shipped sheet, which are fully opaque, come out the same.
    /// </summary>
    /// <param name="baseRgba">Premultiplied BASE pixels.</param>
    /// <param name="maskRgba">Premultiplied MASK pixels.</param>
    /// <param name="tint">House colour multiplied into MASK.</param>
    /// <param name="destination">Receives the composed pixels, premultiplied. Same length as the inputs.</param>
    public static void Compose(byte[] baseRgba, byte[] maskRgba, Color tint, byte[] destination)
    {
        ArgumentNullException.ThrowIfNull(baseRgba);
        ArgumentNullException.ThrowIfNull(maskRgba);
        ArgumentNullException.ThrowIfNull(destination);
        if (baseRgba.Length != maskRgba.Length || destination.Length != baseRgba.Length || baseRgba.Length % 4 != 0)
        {
            throw new ArgumentException("Compose needs equal-length whole RGBA buffers.");
        }

        for (int i = 0; i < baseRgba.Length; i += 4)
        {
            // Pass 1: BASE over transparent black is BASE itself.
            int dr = baseRgba[i];
            int dg = baseRgba[i + 1];
            int db = baseRgba[i + 2];
            int da = baseRgba[i + 3];

            // Pass 2: MASK tinted. Source colour = mask * tint, source alpha = mask alpha * 1.
            int sa = maskRgba[i + 3];
            int sr = Mul(maskRgba[i], tint.R);
            int sg = Mul(maskRgba[i + 1], tint.G);
            int sb = Mul(maskRgba[i + 2], tint.B);

            int keep = 255 - sa;
            destination[i] = (byte)Math.Min(255, sr + Mul(dr, keep));
            destination[i + 1] = (byte)Math.Min(255, sg + Mul(dg, keep));
            destination[i + 2] = (byte)Math.Min(255, sb + Mul(db, keep));
            destination[i + 3] = (byte)Math.Min(255, sa + Mul(da, keep));
        }
    }

    /// <summary>
    /// Multiplies RGB by alpha so the default alpha blend (premultiplied) renders correctly. Fully opaque pixels are
    /// unchanged; fully transparent pixels get zero RGB.
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
                rgba[i + c] = (byte)Mul(rgba[i + c], alpha);
            }
        }
    }

    // 8-bit unorm product with round to nearest: a * b / 255.
    private static int Mul(int a, int b) => (a * b + 127) / 255;
}
