// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;

namespace Rts.Game.Archive;

/// <summary>
/// Colours of the reading pane for one faction. ASSUMED (M3-17): Authority is grey-green and stencilled (dashed border,
/// upper-case titles); the Directorate is umber with red and brass (solid double border, brass titles).
/// </summary>
/// <param name="Panel">Pane fill.</param>
/// <param name="Border">Outer border.</param>
/// <param name="Accent">Rules and bars.</param>
/// <param name="Title">Title text.</param>
/// <param name="Text">Body text.</param>
/// <param name="Dim">Secondary text.</param>
/// <param name="Emphasis">Emphasised words and headings.</param>
/// <param name="Stencil">True: dashed border and upper-case title.</param>
/// <param name="House">Player slot whose house colour tints the preview sprite.</param>
public sealed record ArchiveTheme(Color Panel, Color Border, Color Accent, Color Title, Color Text, Color Dim, Color Emphasis,
    bool Stencil, int House)
{
    /// <summary>Grey-green, stencilled.</summary>
    public static ArchiveTheme Authority { get; } = new(new Color(0x1B, 0x22, 0x1C), new Color(0x7E, 0x90, 0x76),
        new Color(0x5C, 0x72, 0x58), new Color(0xB4, 0xC8, 0xA4), new Color(0xD2, 0xD9, 0xCB), new Color(0x8C, 0x99, 0x84),
        new Color(0xE6, 0xE2, 0xA8), true, 0);

    /// <summary>Umber, red and brass.</summary>
    public static ArchiveTheme Directorate { get; } = new(new Color(0x26, 0x18, 0x12), new Color(0xB8, 0x92, 0x44),
        new Color(0xA2, 0x2C, 0x24), new Color(0xD8, 0xB0, 0x5C), new Color(0xE4, 0xD6, 0xC4), new Color(0xA0, 0x88, 0x70),
        new Color(0xE8, 0x70, 0x5C), false, 1);

    /// <summary>The plain menu palette, for shared entries.</summary>
    public static ArchiveTheme Neutral { get; } = new(new Color(0x16, 0x18, 0x14), new Color(0x6A, 0x72, 0x60),
        new Color(0x4A, 0x52, 0x40), new Color(0xD8, 0xC8, 0x8A), new Color(0xE2, 0xE4, 0xD8), new Color(0x90, 0x94, 0x86),
        new Color(0xF0, 0xE0, 0x9A), false, 2);

    /// <summary>
    /// Warm dust palette from PAL_LB (tools/art/common.py): dust ochre, canvas, rust red and faded teal. ASSUMED (S9): the
    /// colour roles are a first pass; the owner confirms them.
    /// </summary>
    public static ArchiveTheme LeftBehind { get; } = new(new Color(0x2A, 0x20, 0x18), new Color(0xB3, 0x8A, 0x44),
        new Color(0xA0, 0x4A, 0x2C), new Color(0xD1, 0xA6, 0x59), new Color(0xDA, 0xCB, 0x9E), new Color(0xB5, 0xB0, 0xA6),
        new Color(0x78, 0x98, 0x8F), false, 3);

    /// <summary>
    /// Deep navy, lagoon white and brushed chrome with copper trim and signal orange (design 07 §8). ASSUMED (M7-10): the
    /// exact values are a first pass for the art pass to confirm.
    /// </summary>
    public static ArchiveTheme Expedition { get; } = new(new Color(0x0E, 0x1A, 0x2B), new Color(0xA9, 0xB4, 0xBC),
        new Color(0xB8, 0x73, 0x3A), new Color(0xEE, 0xF5, 0xF3), new Color(0xC9, 0xD3, 0xDA), new Color(0x7F, 0x8F, 0x9C),
        new Color(0xF2, 0x8C, 0x28), false, 5);

    /// <summary>The theme of a faction.</summary>
    public static ArchiveTheme For(ArchiveFaction faction) => faction switch
    {
        ArchiveFaction.Authority => Authority,
        ArchiveFaction.Directorate => Directorate,
        ArchiveFaction.LeftBehind => LeftBehind,
        ArchiveFaction.Expedition => Expedition,
        _ => Neutral,
    };
}
