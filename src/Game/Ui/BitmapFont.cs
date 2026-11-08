// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Ui.Fonts;

namespace Rts.Game.Ui;

/// <summary>
/// UI text. Draws from the generated Noto Sans atlases in <c>data/fonts</c> (see <see cref="FontLibrary"/>). When an atlas
/// cannot be loaded it falls back to the embedded 5x7 pixel font, so a missing font never stops the game.
/// </summary>
/// <remarks>
/// Two size conventions. The <c>int scale</c> overloads are the original 5x7 convention, where a font pixel is
/// <c>scale</c> screen pixels; they pick the atlas whose cap height is nearest <c>GlyphHeight * scale</c> (scale 1 gives
/// 12 px, 2 gives 16 px, 3 gives 24 px). The <c>float uiScale</c> overloads take <see cref="UiScale.Resolve"/>
/// and pick the atlas nearest to 12 px times that scale (1 gives 12 px, 1.5 gives 16 px, 2 gives 24 px). Text is never
/// stretched.
/// </remarks>
public static class BitmapFont
{
    /// <summary>Glyph width in font pixels.</summary>
    public const int GlyphWidth = 5;

    /// <summary>Glyph height in font pixels.</summary>
    public const int GlyphHeight = 7;

    /// <summary>Horizontal advance per character in font pixels (glyph plus one blank column).</summary>
    public const int Advance = 6;

    private const int FirstChar = 32;
    private const int LastChar = 126;
    private const int UnknownGlyph = '?' - FirstChar;

    /// <summary>Em size per font pixel for the int-scale overloads: the 5x7 cap height over Noto Sans' cap height (0.714 em).</summary>
    private const float EmPerFontPixel = 1f / 0.714f;

    /// <summary>Em size per <see cref="UiUnit"/> step (screen pixels per 5x7 font pixel): a unit of 2 gives a 16 px font.</summary>
    private const float EmPerUnit = 8f;

    /// <summary>Em size per UI-scale step for the float overloads.</summary>
    private const float BaseEm = 12f;

    /// <summary>Atlas size used for an int scale (see the type remarks).</summary>
    /// <param name="scale">Screen pixels per 5x7 font pixel.</param>
    public static int SizeForScale(int scale) => FontLibrary.NearestSize(GlyphHeight * scale * EmPerFontPixel * UiScale.TextFactor);

    /// <summary>Atlas size used for a <see cref="UiUnit"/> (the Text size setting included).</summary>
    public static int SizeForUnit(UiUnit unit) => FontLibrary.NearestSize(EmPerUnit * unit.Value * UiScale.TextFactor);

    /// <summary>Atlas size used for a UI scale (see the type remarks).</summary>
    /// <param name="uiScale">A scale from <see cref="UiScale.Resolve"/>.</param>
    public static int SizeForUiScale(float uiScale) => FontLibrary.NearestSize(BaseEm * uiScale * UiScale.TextFactor);

    /// <summary>Line pitch in screen pixels for a text drawn at <paramref name="scale"/>.</summary>
    /// <param name="scale">Screen pixels per 5x7 font pixel.</param>
    /// <param name="weight">The weight.</param>
    public static int LineHeight(int scale, FontWeight weight)
    {
        FontAtlas? atlas = FontLibrary.Get(weight, SizeForScale(scale));
        return atlas?.LineHeight ?? GlyphHeight * scale;
    }

    /// <summary>Line pitch in screen pixels for a text drawn at a UI scale.</summary>
    /// <param name="uiScale">A scale from <see cref="UiScale.Resolve"/>.</param>
    /// <param name="weight">The weight.</param>
    public static int LineHeight(float uiScale, FontWeight weight)
    {
        FontAtlas? atlas = FontLibrary.Get(weight, SizeForUiScale(uiScale));
        return atlas?.LineHeight ?? GlyphHeight * LegacyScale(uiScale);
    }

    /// <summary>Line pitch in screen pixels for a text drawn at a <see cref="UiUnit"/>.</summary>
    public static int LineHeight(UiUnit unit, FontWeight weight)
    {
        FontAtlas? atlas = FontLibrary.Get(weight, SizeForUnit(unit));
        return atlas?.LineHeight ?? GlyphHeight * unit.Whole;
    }

    /// <summary>Width in screen pixels of a string drawn at a <see cref="UiUnit"/> (regular weight).</summary>
    public static int Measure(string text, UiUnit unit) => Measure(text, unit, FontWeight.Regular);

    /// <summary>Width in screen pixels of a string drawn at a <see cref="UiUnit"/>.</summary>
    public static int Measure(string text, UiUnit unit, FontWeight weight)
    {
        FontAtlas? atlas = FontLibrary.Get(weight, SizeForUnit(unit));
        return atlas is null ? text.Length * Advance * unit.Whole : atlas.Measure(text);
    }

    /// <summary>Draws text at a <see cref="UiUnit"/> (regular weight).</summary>
    public static void Draw(SpriteBatch batch, Texture2D pixel, string text, Vector2 origin, Color colour, UiUnit unit) =>
        Draw(batch, pixel, text, origin, colour, unit, FontWeight.Regular);

    /// <summary>Draws text at a <see cref="UiUnit"/>.</summary>
    public static void Draw(SpriteBatch batch, Texture2D pixel, string text, Vector2 origin, Color colour, UiUnit unit, FontWeight weight)
    {
        FontAtlas? atlas = FontLibrary.GetWithTexture(batch.GraphicsDevice, weight, SizeForUnit(unit));
        if (atlas is null) DrawLegacy(batch, pixel, text, origin, colour, unit.Whole);
        else DrawAtlas(atlas, batch, text, origin, colour);
    }

    /// <summary>Width in screen pixels of a string drawn at <paramref name="scale"/> (regular weight).</summary>
    public static int Measure(string text, int scale) => Measure(text, scale, FontWeight.Regular);

    /// <summary>Width in screen pixels of a string drawn at an int scale.</summary>
    /// <param name="text">Text to measure.</param>
    /// <param name="scale">Screen pixels per 5x7 font pixel.</param>
    /// <param name="weight">The weight.</param>
    public static int Measure(string text, int scale, FontWeight weight)
    {
        FontAtlas? atlas = FontLibrary.Get(weight, SizeForScale(scale));
        return atlas is null ? text.Length * Advance * scale : atlas.Measure(text);
    }

    /// <summary>Width in screen pixels of a string drawn at a UI scale.</summary>
    /// <param name="text">Text to measure.</param>
    /// <param name="uiScale">A scale from <see cref="UiScale.Resolve"/>.</param>
    /// <param name="weight">The weight.</param>
    public static int Measure(string text, float uiScale, FontWeight weight)
    {
        FontAtlas? atlas = FontLibrary.Get(weight, SizeForUiScale(uiScale));
        return atlas is null ? text.Length * Advance * LegacyScale(uiScale) : atlas.Measure(text);
    }

    /// <summary>Draws text with its top-left corner at <paramref name="origin"/> (regular weight, int scale).</summary>
    /// <param name="batch">A batch already begun.</param>
    /// <param name="pixel">A 1x1 white texture.</param>
    /// <param name="text">Text to draw.</param>
    /// <param name="origin">Top-left corner in screen pixels.</param>
    /// <param name="colour">Text colour.</param>
    /// <param name="scale">Screen pixels per 5x7 font pixel.</param>
    public static void Draw(SpriteBatch batch, Texture2D pixel, string text, Vector2 origin, Color colour, int scale) =>
        Draw(batch, pixel, text, origin, colour, scale, FontWeight.Regular);

    /// <summary>Draws text with its top-left corner at <paramref name="origin"/> at an int scale.</summary>
    /// <param name="batch">A batch already begun.</param>
    /// <param name="pixel">A 1x1 white texture, used by the 5x7 fallback.</param>
    /// <param name="text">Text to draw.</param>
    /// <param name="origin">Top-left corner in screen pixels.</param>
    /// <param name="colour">Text colour.</param>
    /// <param name="scale">Screen pixels per 5x7 font pixel.</param>
    /// <param name="weight">The weight.</param>
    public static void Draw(SpriteBatch batch, Texture2D pixel, string text, Vector2 origin, Color colour, int scale, FontWeight weight)
    {
        FontAtlas? atlas = FontLibrary.GetWithTexture(batch.GraphicsDevice, weight, SizeForScale(scale));
        if (atlas is null)
        {
            DrawLegacy(batch, pixel, text, origin, colour, scale);
        }
        else
        {
            DrawAtlas(atlas, batch, text, origin, colour);
        }
    }

    /// <summary>Draws text at a UI scale.</summary>
    /// <param name="batch">A batch already begun.</param>
    /// <param name="pixel">A 1x1 white texture, used by the 5x7 fallback.</param>
    /// <param name="text">Text to draw.</param>
    /// <param name="origin">Top-left corner in screen pixels.</param>
    /// <param name="colour">Text colour.</param>
    /// <param name="uiScale">A scale from <see cref="UiScale.Resolve"/>.</param>
    /// <param name="weight">The weight.</param>
    public static void Draw(SpriteBatch batch, Texture2D pixel, string text, Vector2 origin, Color colour, float uiScale, FontWeight weight)
    {
        FontAtlas? atlas = FontLibrary.GetWithTexture(batch.GraphicsDevice, weight, SizeForUiScale(uiScale));
        if (atlas is null)
        {
            DrawLegacy(batch, pixel, text, origin, colour, LegacyScale(uiScale));
        }
        else
        {
            DrawAtlas(atlas, batch, text, origin, colour);
        }
    }

    /// <summary>Whole-pixel 5x7 scale used by the fallback for a fractional UI scale (1.5 rounds to 2).</summary>
    private static int LegacyScale(float uiScale) => Math.Max(1, (int)MathF.Round(uiScale, MidpointRounding.AwayFromZero));

    private static void DrawAtlas(FontAtlas atlas, SpriteBatch batch, string text, Vector2 origin, Color colour)
    {
        int x = (int)origin.X;
        int y = (int)origin.Y;
        int previous = -1;
        foreach (char ch in text)
        {
            int codepoint = atlas.ResolveCodepoint(ch);
            if (previous >= 0) x += atlas.KerningBetween(previous, codepoint);
            FontGlyph glyph = atlas.GetGlyph(codepoint);
            if (glyph.W > 0 && glyph.H > 0)
            {
                batch.Draw(atlas.Texture!, new Vector2(x + glyph.XOffset, y + glyph.YOffset),
                    new Rectangle(glyph.X, glyph.Y, glyph.W, glyph.H), colour);
            }
            x += glyph.Advance;
            previous = codepoint;
        }
    }

    private static void DrawLegacy(SpriteBatch batch, Texture2D pixel, string text, Vector2 origin, Color colour, int scale)
    {
        int x = (int)origin.X;
        int y = (int)origin.Y;
        foreach (char ch in text)
        {
            int glyph = ch >= FirstChar && ch <= LastChar ? ch - FirstChar : UnknownGlyph;
            int start = glyph * GlyphHeight;
            for (int row = 0; row < GlyphHeight; row++)
            {
                byte bits = Rows[start + row];
                for (int col = 0; col < GlyphWidth; col++)
                {
                    if ((bits & (0x10 >> col)) != 0)
                    {
                        batch.Draw(pixel, new Rectangle(x + col * scale, y + row * scale, scale, scale), colour);
                    }
                }
            }
            x += Advance * scale;
        }
    }

    // Seven bytes per glyph, ASCII 32 (space) to 126 (~). Bits 4..0 = columns left to right.
    private static readonly byte[] Rows =
    {
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // 32 ' '
        0x04, 0x04, 0x04, 0x04, 0x04, 0x00, 0x04, // 33 !
        0x0A, 0x0A, 0x0A, 0x00, 0x00, 0x00, 0x00, // 34 "
        0x0A, 0x0A, 0x1F, 0x0A, 0x1F, 0x0A, 0x0A, // 35 #
        0x04, 0x0F, 0x14, 0x0E, 0x05, 0x1E, 0x04, // 36 $
        0x18, 0x19, 0x02, 0x04, 0x08, 0x13, 0x03, // 37 %
        0x0C, 0x12, 0x14, 0x08, 0x15, 0x12, 0x0D, // 38 &
        0x0C, 0x04, 0x08, 0x00, 0x00, 0x00, 0x00, // 39 '
        0x02, 0x04, 0x08, 0x08, 0x08, 0x04, 0x02, // 40 (
        0x08, 0x04, 0x02, 0x02, 0x02, 0x04, 0x08, // 41 )
        0x00, 0x04, 0x15, 0x0E, 0x15, 0x04, 0x00, // 42 *
        0x00, 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00, // 43 +
        0x00, 0x00, 0x00, 0x00, 0x0C, 0x04, 0x08, // 44 ,
        0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00, // 45 -
        0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C, // 46 .
        0x00, 0x01, 0x02, 0x04, 0x08, 0x10, 0x00, // 47 /
        0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E, // 48 0
        0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E, // 49 1
        0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F, // 50 2
        0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E, // 51 3
        0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02, // 52 4
        0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E, // 53 5
        0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E, // 54 6
        0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08, // 55 7
        0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E, // 56 8
        0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C, // 57 9
        0x00, 0x0C, 0x0C, 0x00, 0x0C, 0x0C, 0x00, // 58 :
        0x00, 0x0C, 0x0C, 0x00, 0x0C, 0x04, 0x08, // 59 ;
        0x02, 0x04, 0x08, 0x10, 0x08, 0x04, 0x02, // 60 <
        0x00, 0x00, 0x1F, 0x00, 0x1F, 0x00, 0x00, // 61 =
        0x08, 0x04, 0x02, 0x01, 0x02, 0x04, 0x08, // 62 >
        0x0E, 0x11, 0x01, 0x02, 0x04, 0x00, 0x04, // 63 ?
        0x0E, 0x11, 0x01, 0x0D, 0x15, 0x15, 0x0E, // 64 @
        0x0E, 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, // 65 A
        0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E, // 66 B
        0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E, // 67 C
        0x1C, 0x12, 0x11, 0x11, 0x11, 0x12, 0x1C, // 68 D
        0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F, // 69 E
        0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10, // 70 F
        0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F, // 71 G
        0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11, // 72 H
        0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E, // 73 I
        0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C, // 74 J
        0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11, // 75 K
        0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F, // 76 L
        0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11, // 77 M
        0x11, 0x11, 0x19, 0x15, 0x13, 0x11, 0x11, // 78 N
        0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E, // 79 O
        0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10, // 80 P
        0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D, // 81 Q
        0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11, // 82 R
        0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E, // 83 S
        0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, // 84 T
        0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E, // 85 U
        0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04, // 86 V
        0x11, 0x11, 0x11, 0x15, 0x15, 0x15, 0x0A, // 87 W
        0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11, // 88 X
        0x11, 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, // 89 Y
        0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F, // 90 Z
        0x0E, 0x08, 0x08, 0x08, 0x08, 0x08, 0x0E, // 91 [
        0x00, 0x10, 0x08, 0x04, 0x02, 0x01, 0x00, // 92 backslash
        0x0E, 0x02, 0x02, 0x02, 0x02, 0x02, 0x0E, // 93 ]
        0x04, 0x0A, 0x11, 0x00, 0x00, 0x00, 0x00, // 94 ^
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1F, // 95 _
        0x08, 0x04, 0x02, 0x00, 0x00, 0x00, 0x00, // 96 `
        0x00, 0x00, 0x0E, 0x01, 0x0F, 0x11, 0x0F, // 97 a
        0x10, 0x10, 0x16, 0x19, 0x11, 0x11, 0x1E, // 98 b
        0x00, 0x00, 0x0E, 0x10, 0x10, 0x11, 0x0E, // 99 c
        0x01, 0x01, 0x0D, 0x13, 0x11, 0x11, 0x0F, // 100 d
        0x00, 0x00, 0x0E, 0x11, 0x1F, 0x10, 0x0E, // 101 e
        0x06, 0x09, 0x08, 0x1C, 0x08, 0x08, 0x08, // 102 f
        0x00, 0x0F, 0x11, 0x11, 0x0F, 0x01, 0x0E, // 103 g
        0x10, 0x10, 0x16, 0x19, 0x11, 0x11, 0x11, // 104 h
        0x04, 0x00, 0x0C, 0x04, 0x04, 0x04, 0x0E, // 105 i
        0x02, 0x00, 0x06, 0x02, 0x02, 0x12, 0x0C, // 106 j
        0x10, 0x10, 0x12, 0x14, 0x18, 0x14, 0x12, // 107 k
        0x0C, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E, // 108 l
        0x00, 0x00, 0x1A, 0x15, 0x15, 0x15, 0x15, // 109 m
        0x00, 0x00, 0x16, 0x19, 0x11, 0x11, 0x11, // 110 n
        0x00, 0x00, 0x0E, 0x11, 0x11, 0x11, 0x0E, // 111 o
        0x00, 0x00, 0x1E, 0x11, 0x1E, 0x10, 0x10, // 112 p
        0x00, 0x00, 0x0D, 0x13, 0x0F, 0x01, 0x01, // 113 q
        0x00, 0x00, 0x16, 0x19, 0x10, 0x10, 0x10, // 114 r
        0x00, 0x00, 0x0F, 0x10, 0x0E, 0x01, 0x1E, // 115 s
        0x08, 0x08, 0x1C, 0x08, 0x08, 0x09, 0x06, // 116 t
        0x00, 0x00, 0x11, 0x11, 0x11, 0x13, 0x0D, // 117 u
        0x00, 0x00, 0x11, 0x11, 0x11, 0x0A, 0x04, // 118 v
        0x00, 0x00, 0x11, 0x11, 0x15, 0x15, 0x0A, // 119 w
        0x00, 0x00, 0x11, 0x0A, 0x04, 0x0A, 0x11, // 120 x
        0x00, 0x00, 0x11, 0x11, 0x0F, 0x01, 0x0E, // 121 y
        0x00, 0x00, 0x1F, 0x02, 0x04, 0x08, 0x1F, // 122 z
        0x02, 0x04, 0x04, 0x08, 0x04, 0x04, 0x02, // 123 {
        0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, // 124 |
        0x08, 0x04, 0x04, 0x02, 0x04, 0x04, 0x08, // 125 }
        0x00, 0x00, 0x08, 0x15, 0x02, 0x00, 0x00, // 126 ~
    };
}
