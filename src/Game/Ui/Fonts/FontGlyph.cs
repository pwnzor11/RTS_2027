// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui.Fonts;

/// <summary>
/// One glyph from a generated <c>.font.toml</c>. (X, Y, W, H) is its rectangle in the atlas texture; the glyph is drawn
/// at pen position plus (XOffset, YOffset), where the pen starts at the top of the line box, and the pen then moves by
/// <see cref="Advance"/> pixels.
/// </summary>
/// <param name="Codepoint">Unicode scalar value.</param>
/// <param name="X">Atlas x of the glyph's top-left pixel.</param>
/// <param name="Y">Atlas y of the glyph's top-left pixel.</param>
/// <param name="W">Glyph width in pixels; 0 for blank glyphs such as the space.</param>
/// <param name="H">Glyph height in pixels; 0 for blank glyphs.</param>
/// <param name="XOffset">Horizontal offset from the pen position.</param>
/// <param name="YOffset">Vertical offset from the top of the line box.</param>
/// <param name="Advance">Pen advance in pixels, before kerning.</param>
public readonly record struct FontGlyph(int Codepoint, int X, int Y, int W, int H, int XOffset, int YOffset, int Advance);
