// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Controls;

namespace Rts.Game.Render;

/// <summary>
/// The software cursor sprites (M11-10e), drawn in code with no copied art, like <see cref="PlaceholderArt"/>. Every sprite is a
/// <see cref="Size"/> by <see cref="Size"/> grid of pixels with a one-pixel dark outline added around the shapes, so it reads on any
/// ground. Action cursors are a coloured ring with a letter (E enter, C capture, R repair, G garrison, S salvage, H harvest,
/// U unload, D deploy) so they stay distinct without art; move, attack and blocked have their own shapes.
/// </summary>
public static class CursorArt
{
    /// <summary>Width and height of every sprite in pixels (at scale 1).</summary>
    public const int Size = 19;

    private const int Mid = Size / 2;
    private static readonly Color Clear = Color.Transparent;
    private static readonly Color Dark = new(0x14, 0x14, 0x10);
    private static readonly Color White = new(0xF2, 0xEF, 0xD8);
    private static readonly Color Green = new(0x5E, 0xD0, 0x5A);
    private static readonly Color Red = new(0xE8, 0x40, 0x30);
    private static readonly Color Amber = new(0xF0, 0xB8, 0x30);
    private static readonly Color Cyan = new(0x50, 0xC8, 0xE8);
    private static readonly Color Panel = new(0x18, 0x18, 0x14, 0xD0);

    private static readonly Dictionary<char, string[]> Letters = new()
    {
        ['E'] = new[] { "###", "#..", "##.", "#..", "###" },
        ['C'] = new[] { ".##", "#..", "#..", "#..", ".##" },
        ['R'] = new[] { "##.", "#.#", "##.", "#.#", "#.#" },
        ['G'] = new[] { ".##", "#..", "#.#", "#.#", ".##" },
        ['S'] = new[] { ".##", "#..", ".#.", "..#", "##." },
        ['H'] = new[] { "#.#", "#.#", "###", "#.#", "#.#" },
        ['U'] = new[] { "#.#", "#.#", "#.#", "#.#", "###" },
        ['D'] = new[] { "##.", "#.#", "#.#", "#.#", "##." },
    };

    /// <summary>A built sprite: pixels row by row and the hot spot (the pixel that sits on the mouse position).</summary>
    /// <param name="Pixels">Size * Size colours, row-major; transparent where nothing is drawn.</param>
    /// <param name="HotX">Hot spot column.</param>
    /// <param name="HotY">Hot spot row.</param>
    public sealed record Sprite(Color[] Pixels, int HotX, int HotY);

    private static readonly Dictionary<CursorKind, Sprite> Cache = new();

    /// <summary>The sprite for a cursor kind (built once).</summary>
    public static Sprite Of(CursorKind kind)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(kind, out Sprite? s)) Cache[kind] = s = Build(kind);
            return s;
        }
    }

    private static Sprite Build(CursorKind kind)
    {
        var g = new Color[Size * Size];
        Array.Fill(g, Clear);
        int hx = Mid, hy = Mid;
        switch (kind)
        {
            case CursorKind.Select:
                hx = 1; hy = 1;
                for (int y = 0; y < 14; y++)
                    for (int x = 0; x <= (y < 9 ? y : 13 - y); x++)
                        Put(g, x + 1, y + 1, White);
                break;
            case CursorKind.Hover:
                Corners(g, 6, White);
                Put(g, Mid, Mid, White);
                break;
            case CursorKind.Move:
                Ring(g, 6, Green);
                Put(g, Mid, Mid, Green);
                for (int d = 3; d <= 8; d++) { Put(g, Mid - d, Mid, Green); Put(g, Mid + d, Mid, Green); Put(g, Mid, Mid - d, Green); Put(g, Mid, Mid + d, Green); }
                break;
            case CursorKind.Attack:
                Ring(g, 6, Red);
                Put(g, Mid, Mid, Red);
                Ticks(g, Red);
                break;
            case CursorKind.AttackMove:
                Ring(g, 6, Red);
                Ticks(g, Red);
                Corners(g, 8, Amber);
                break;
            case CursorKind.Enter: Badge(g, 'E', Cyan); break;
            case CursorKind.Capture: Badge(g, 'C', Amber); break;
            case CursorKind.Repair: Badge(g, 'R', Green); break;
            case CursorKind.Garrison: Badge(g, 'G', Cyan); break;
            case CursorKind.Salvage: Badge(g, 'S', Amber); break;
            case CursorKind.Harvest: Badge(g, 'H', Green); break;
            case CursorKind.Unload: Badge(g, 'U', Cyan); break;
            case CursorKind.DeployAt: Badge(g, 'D', Amber); break;
            case CursorKind.Blocked:
                Ring(g, 6, Red);
                for (int d = -4; d <= 4; d++) { Put(g, Mid + d, Mid + d, Red); Put(g, Mid + d + 1, Mid + d, Red); }
                break;
        }

        Outline(g);
        return new Sprite(g, hx, hy);
    }

    private static void Put(Color[] g, int x, int y, Color c)
    {
        if ((uint)x < Size && (uint)y < Size) g[y * Size + x] = c;
    }

    private static void Ring(Color[] g, int r, Color c)
    {
        for (int y = -r - 1; y <= r + 1; y++)
            for (int x = -r - 1; x <= r + 1; x++)
            {
                int d2 = x * x + y * y;
                if (d2 >= r * r - r && d2 <= r * r + r) Put(g, Mid + x, Mid + y, c);
            }
    }

    private static void Ticks(Color[] g, Color c)
    {
        foreach (int d in new[] { 4, 5, 8 })
        {
            Put(g, Mid - d - 1, Mid, c); Put(g, Mid + d + 1, Mid, c); Put(g, Mid, Mid - d - 1, c); Put(g, Mid, Mid + d + 1, c);
        }
    }

    private static void Corners(Color[] g, int r, Color c)
    {
        for (int i = 0; i < 3; i++)
        {
            foreach (int sx in new[] { -1, 1 })
                foreach (int sy in new[] { -1, 1 })
                {
                    Put(g, Mid + sx * (r - i), Mid + sy * r, c);
                    Put(g, Mid + sx * r, Mid + sy * (r - i), c);
                }
        }
    }

    private static void Badge(Color[] g, char letter, Color c)
    {
        for (int y = -6; y <= 6; y++)
            for (int x = -6; x <= 6; x++)
                if (x * x + y * y <= 36) Put(g, Mid + x, Mid + y, Panel);
        Ring(g, 7, c);
        string[] rows = Letters[letter];
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 3; x++)
                if (rows[y][x] == '#') Put(g, Mid - 1 + x, Mid - 2 + y, c);
    }

    // One dark pixel around every drawn pixel (4-neighbours), drawn under the shapes.
    private static void Outline(Color[] g)
    {
        var copy = (Color[])g.Clone();
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (copy[y * Size + x].A != 0) continue;
                if (Solid(copy, x - 1, y) || Solid(copy, x + 1, y) || Solid(copy, x, y - 1) || Solid(copy, x, y + 1)) g[y * Size + x] = Dark;
            }
    }

    private static bool Solid(Color[] g, int x, int y) => (uint)x < Size && (uint)y < Size && g[y * Size + x].A != 0;
}
