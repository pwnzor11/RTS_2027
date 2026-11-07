// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Card;

/// <summary>Pure rules for the group grid: ordering by type, the overflow tile and the header text.</summary>
public static class CardGrouping
{
    /// <summary>Most tiles in the grid, the "+N" tile included.</summary>
    public const int MaxTiles = 24;

    /// <summary>
    /// The cameos grouped by type: types in the order they first appear, units of a type in selection order. Stable, so a
    /// click that removes one unit does not reshuffle the others.
    /// </summary>
    public static IReadOnlyList<CardCameo> Order(IReadOnlyList<CardCameo> cameos)
    {
        var typeOrder = new List<string>();
        foreach (CardCameo c in cameos)
            if (!typeOrder.Contains(c.RulesId)) typeOrder.Add(c.RulesId);
        var result = new List<CardCameo>(cameos.Count);
        foreach (string id in typeOrder)
            foreach (CardCameo c in cameos)
                if (c.RulesId == id) result.Add(c);
        return result;
    }

    /// <summary>One group per type, in the order of <paramref name="ordered"/>, with <paramref name="nameOf"/> giving the display name.</summary>
    public static IReadOnlyList<CardGroup> Groups(IReadOnlyList<CardCameo> ordered, Func<string, string> nameOf)
    {
        var groups = new List<CardGroup>();
        foreach (CardCameo c in ordered)
        {
            int i = groups.FindIndex(g => g.RulesId == c.RulesId);
            if (i < 0) groups.Add(new CardGroup(c.RulesId, nameOf(c.RulesId), 1));
            else groups[i] = groups[i] with { Count = groups[i].Count + 1 };
        }

        return groups;
    }

    /// <summary>
    /// How many unit tiles to draw and how many units the "+N" tile stands for. Up to <see cref="MaxTiles"/> units get a tile each;
    /// beyond that the last tile becomes "+N", so the grid never has more than <see cref="MaxTiles"/> tiles.
    /// </summary>
    public static (int Shown, int Overflow) Visible(int count) =>
        count <= MaxTiles ? (Math.Max(0, count), 0) : (MaxTiles - 1, count - (MaxTiles - 1));

    /// <summary>The header of a group: "5 selected  -  4 Rifleman, 1 Scout".</summary>
    public static string Header(int total, IReadOnlyList<CardGroup> groups) =>
        total.ToString(System.Globalization.CultureInfo.InvariantCulture) + " selected  -  "
        + string.Join(", ", groups.Select(g => g.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + g.Name));

    /// <summary>Health as "45 / 50".</summary>
    public static string HealthText(int health, int max) =>
        Math.Max(0, health).ToString(System.Globalization.CultureInfo.InvariantCulture) + " / " + Math.Max(0, max).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Health as a per-mille fill, clamped 0..1000 (0 when the maximum is unknown).</summary>
    public static int HealthPermille(int health, int max) => max <= 0 ? 0 : (int)Math.Clamp(Math.Max(0L, health) * 1000L / max, 0L, 1000L);

    /// <summary>A length or speed with one decimal from tenths: 25 gives "2.5".</summary>
    public static string Tenths(int tenths) =>
        (tenths / 10).ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + (Math.Abs(tenths) % 10).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Armour class as a player-facing word.</summary>
    public static string ArmourText(ArmorClass armor) => armor switch
    {
        ArmorClass.Unarmored => "none",
        ArmorClass.Light => "light",
        ArmorClass.Heavy => "heavy",
        ArmorClass.Structure => "structure",
        ArmorClass.Fortified => "fortified",
        ArmorClass.ShipLight => "light hull",
        ArmorClass.ShipHeavy => "heavy hull",
        ArmorClass.Submerged => "submerged",
        _ => armor.ToString().ToLowerInvariant(),
    };

    /// <summary>
    /// Packs stat texts into lines no wider than <paramref name="maxWidth"/> (two spaces between items), at most
    /// <paramref name="maxLines"/> lines; items that do not fit are dropped from the last line rather than overflowing.
    /// </summary>
    public static IReadOnlyList<string> WrapStats(IReadOnlyList<string> items, int maxWidth, int maxLines, Func<string, int> measure)
    {
        var lines = new List<string>();
        string current = "";
        foreach (string item in items)
        {
            string trial = current.Length == 0 ? item : current + "  " + item;
            if (current.Length == 0 || measure(trial) <= maxWidth) { current = trial; continue; }
            if (lines.Count + 1 >= maxLines) break;
            lines.Add(current);
            current = item;
        }

        if (current.Length > 0) lines.Add(current);
        return lines;
    }
}
