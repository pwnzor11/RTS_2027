// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Menu;

/// <summary>
/// The big block-letter title on the main menu, built from rectangles (the UI font stops at 24 px). Only the characters of the
/// game's name exist; anything else is skipped. Letters are 5 cells wide and 7 tall with one blank column between them.
/// </summary>
public static class TitleArt
{
    private static readonly Dictionary<char, string[]> Glyphs = new()
    {
        ['R'] = new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" },
        ['T'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" },
        ['S'] = new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" },
        ['_'] = new[] { "00000", "00000", "00000", "00000", "00000", "00000", "11111" },
        ['2'] = new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" },
        ['0'] = new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" },
        ['7'] = new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
    };

    /// <summary>Width in cells of <paramref name="text"/>.</summary>
    public static int WidthCells(string text) => text.Length == 0 ? 0 : text.Length * 6 - 1;

    /// <summary>Height in cells.</summary>
    public const int HeightCells = 7;

    /// <summary>
    /// Appends the ops for the title with its top-left at (<paramref name="x"/>, <paramref name="y"/>), each cell
    /// <paramref name="cell"/> pixels square. The colour runs from <paramref name="top"/> to <paramref name="bottom"/> down the letters,
    /// over a soft shadow.
    /// </summary>
    public static void Build(List<DrawOp> ops, string text, float x, float y, float cell, Rgba top, Rgba bottom)
    {
        var shadow = new Rgba(0, 0, 0, 150);
        for (int pass = 0; pass < 2; pass++)
        {
            float dx = pass == 0 ? cell * 0.35f : 0f, dy = pass == 0 ? cell * 0.45f : 0f;
            for (int ci = 0; ci < text.Length; ci++)
            {
                if (!Glyphs.TryGetValue(char.ToUpperInvariant(text[ci]), out string[]? rows)) continue;
                for (int r = 0; r < HeightCells; r++)
                {
                    Rgba colour = pass == 0 ? shadow : Rgba.Lerp(top, bottom, r / (float)(HeightCells - 1));
                    int c = 0;
                    while (c < 5)
                    {
                        if (rows[r][c] != '1') { c++; continue; }
                        int start = c;
                        while (c < 5 && rows[r][c] == '1') c++;
                        ops.Add(DrawOp.Rect(x + dx + (ci * 6 + start) * cell, y + dy + r * cell, (c - start) * cell, cell, colour));
                    }
                }
            }
        }
    }
}
