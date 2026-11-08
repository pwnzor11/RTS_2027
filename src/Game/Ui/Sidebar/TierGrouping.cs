// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Sidebar;

/// <summary>One cameo in a tier-grouped sidebar tab: a real option, or (Locked) a type of a tier the player has not reached yet.</summary>
/// <param name="Option">The HUD option; for a locked entry a synthetic one (Unlocked false, NotBuildable).</param>
/// <param name="Tier">The type's tech tier (1..3).</param>
/// <param name="Locked">True when the type is greyed because its tier gate is missing.</param>
/// <param name="Gate">Rules id of the gate structure to name in the "Needs ..." tooltip ("" when the entry is not locked).</param>
public readonly record struct TierEntry(BuildOption Option, int Tier, bool Locked, string Gate);

/// <summary>One row of the tier-grouped grid: a heading ("Tier 2", Locked when the gate is missing) or up to Columns cameos of one tier.</summary>
/// <param name="Heading">True for a heading row (spans the grid; <see cref="Cells"/> is empty).</param>
/// <param name="Tier">The tier of the heading or of the cameos.</param>
/// <param name="Locked">For a heading: the tier is not reached.</param>
/// <param name="Gate">For a heading: the gate to name when locked.</param>
/// <param name="Cells">The cameos of a cameo row.</param>
public readonly record struct TierRow(bool Heading, int Tier, bool Locked, string Gate, IReadOnlyList<TierEntry> Cells);

/// <summary>
/// Groups a sidebar tab's buildable types under Tier 1/2/3 headings (design 08 §4, pure so it is unit-tested). The HUD lists only
/// unlocked types, so a tier the player has not reached is previewed from the rules: every non-hidden type of the tab's category,
/// faction and that tier, greyed, with the tier's gate named. Tiers the player has reached never show locked entries (those are
/// locked for other reasons and stay hidden, as before). Headings appear only when some tier above 1 has content; a tab with
/// tier 1 only stays a flat grid.
/// </summary>
public static class TierGrouping
{
    /// <summary>The highest tier.</summary>
    public const int MaxTier = 3;

    /// <summary>The tier of the type an option names (1 for unknown indices).</summary>
    public static int TierOf(SimRules rules, BuildOption o)
    {
        int i = o.TypeIndex;
        switch (o.Category)
        {
            case ProductionCategory.Infantry: return (uint)i < (uint)rules.Infantry.Count ? Clamp(rules.Infantry[i].Tier) : 1;
            case ProductionCategory.Vehicle:
            case ProductionCategory.Aircraft: return (uint)i < (uint)rules.Vehicles.Count ? Clamp(rules.Vehicles[i].Tier) : 1;
            case ProductionCategory.Structure:
            case ProductionCategory.Defence: return (uint)i < (uint)rules.Buildings.Count ? Clamp(rules.Buildings[i].Tier) : 1;
            default: return 1;
        }
    }

    /// <summary>The rules id of the structure that provides a faction's tier gate, or the provides name itself when no structure does,
    /// or "" when the faction names no gate for that tier (tier 1, or an older rules set).</summary>
    public static string GateOf(SimRules rules, Faction faction, int tier)
    {
        if (tier < 2 || (int)faction >= rules.StartKits.Count) return "";
        FactionStartKit kit = rules.StartKits[(int)faction];
        string provides = tier == 2 ? kit.Tier2Provides : tier == 3 ? kit.Tier3Provides : "";
        if (provides.Length == 0) return "";
        foreach (BuildingType b in rules.Buildings)
            foreach (string p in b.Provides)
                if (string.Equals(p, provides, StringComparison.Ordinal)) return b.Id;
        return provides;
    }

    /// <summary>The entries of one tab, grouped by tier (tier, then HUD order), with locked previews of unreached tiers.</summary>
    /// <param name="rules">The rules.</param>
    /// <param name="faction">The local player's faction.</param>
    /// <param name="playerTier">The player's tier (<c>Simulation.TierOf</c>).</param>
    /// <param name="category">The tab's production category (Structure, Defence, Infantry, Vehicle or Aircraft).</param>
    /// <param name="options">The visible (already filtered) unlocked options of that category, in HUD order.</param>
    public static IReadOnlyList<TierEntry> Entries(SimRules rules, Faction faction, int playerTier, ProductionCategory category,
        IReadOnlyList<BuildOption> options)
    {
        var list = new List<TierEntry>();
        for (int tier = 1; tier <= MaxTier; tier++)
        {
            foreach (BuildOption o in options)
                if (TierOf(rules, o) == tier) list.Add(new TierEntry(o, tier, false, ""));
            if (tier <= playerTier || tier < 2) continue;

            string gate = GateOf(rules, faction, tier);
            if (gate.Length == 0) continue;
            foreach (BuildOption locked in LockedOf(rules, faction, category, tier, options))
                list.Add(new TierEntry(locked, tier, true, gate));
        }

        return list;
    }

    /// <summary>
    /// Packs entries into rows: with headings, each tier starts with a heading row and its cameos fill rows of <paramref name="columns"/>;
    /// without (<see cref="NeedsHeadings"/> false) the cameos just fill rows. A tier whose gate is missing gets a locked heading.
    /// </summary>
    public static IReadOnlyList<TierRow> Rows(IReadOnlyList<TierEntry> entries, int columns)
    {
        columns = Math.Max(1, columns);
        var rows = new List<TierRow>();
        bool headings = NeedsHeadings(entries);
        int i = 0;
        while (i < entries.Count)
        {
            int tier = entries[i].Tier;
            bool locked = entries[i].Locked;
            string gate = entries[i].Gate;
            int j = i;
            while (j < entries.Count && entries[j].Tier == tier) j++;
            if (headings) rows.Add(new TierRow(true, tier, locked && AllLocked(entries, i, j), gate, Array.Empty<TierEntry>()));
            for (int k = i; k < j; k += columns)
            {
                var cells = new List<TierEntry>();
                for (int c = k; c < Math.Min(j, k + columns); c++) cells.Add(entries[c]);
                rows.Add(new TierRow(false, tier, false, "", cells));
            }

            i = j;
        }

        return rows;
    }

    /// <summary>One visible row of the grid with its top and height relative to the grid's top.</summary>
    /// <param name="Row">Index into the tier rows.</param>
    /// <param name="Y">Top, in pixels from the grid's top.</param>
    /// <param name="Height">Row height: headings take <c>headingH</c>, cameo rows take <c>cameoH</c> (rows size to content).</param>
    public readonly record struct PlacedRow(int Row, int Y, int Height);

    /// <summary>The rows visible from <paramref name="firstRow"/> in a grid <paramref name="gridHeight"/> tall; a row that does not fit whole is not shown.</summary>
    public static IReadOnlyList<PlacedRow> Place(IReadOnlyList<TierRow> rows, int firstRow, int gridHeight, int cameoH, int headingH, int gap)
    {
        var placed = new List<PlacedRow>();
        int y = 0;
        for (int i = Math.Max(0, firstRow); i < rows.Count; i++)
        {
            int h = rows[i].Heading ? headingH : cameoH;
            if (y + h > gridHeight) break;
            // A heading is never shown without the row under it: if that row does not fit too, stop here.
            if (rows[i].Heading && i + 1 < rows.Count && y + h + gap + (rows[i + 1].Heading ? headingH : cameoH) > gridHeight) break;
            placed.Add(new PlacedRow(i, y, h));
            y += h + gap;
        }

        return placed;
    }

    /// <summary>The largest first row that still shows the last row (the scroll limit).</summary>
    public static int MaxFirstRow(IReadOnlyList<TierRow> rows, int gridHeight, int cameoH, int headingH, int gap)
    {
        int used = 0;
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            int h = rows[i].Heading ? headingH : cameoH;
            used += (used > 0 ? gap : 0) + h;
            if (used > gridHeight) return Math.Min(rows.Count - 1, i + 1);
        }

        return 0;
    }

    /// <summary>True when any entry is above tier 1, so the grid shows headings.</summary>
    public static bool NeedsHeadings(IReadOnlyList<TierEntry> entries)
    {
        foreach (TierEntry e in entries)
            if (e.Tier >= 2) return true;
        return false;
    }

    /// <summary>The tooltip lines of a locked entry: its name, cost and what it needs.</summary>
    public static string NeedsText(TierEntry e) => e.Locked ? "Needs " + Sidebar.CameoNames.Display(e.Gate) : "";

    /// <summary>The heading caption: "Tier 2", plus "(needs Engineering Works)" while locked.</summary>
    public static string HeadingText(TierRow row) =>
        row.Locked ? $"Tier {row.Tier}  (needs {Sidebar.CameoNames.Display(row.Gate)})" : $"Tier {row.Tier}";

    private static bool AllLocked(IReadOnlyList<TierEntry> entries, int from, int to)
    {
        for (int k = from; k < to; k++)
            if (!entries[k].Locked) return false;
        return true;
    }

    private static IEnumerable<BuildOption> LockedOf(SimRules rules, Faction faction, ProductionCategory category, int tier,
        IReadOnlyList<BuildOption> options)
    {
        switch (category)
        {
            case ProductionCategory.Infantry:
                for (int i = 0; i < rules.Infantry.Count; i++)
                {
                    InfantryType t = rules.Infantry[i];
                    if (t.Faction == faction && t.Tier == tier && !Has(options, i)) yield return Locked(category, i, t.Cost);
                }

                break;
            case ProductionCategory.Vehicle:
            case ProductionCategory.Aircraft:
                for (int i = 0; i < rules.Vehicles.Count; i++)
                {
                    VehicleType t = rules.Vehicles[i];
                    bool air = t.Locomotion == Locomotion.Air;
                    if (t.Faction == faction && t.Tier == tier && air == (category == ProductionCategory.Aircraft) && !Has(options, i))
                        yield return Locked(category, i, t.Cost);
                }

                break;
            case ProductionCategory.Structure:
            case ProductionCategory.Defence:
                for (int i = 0; i < rules.Buildings.Count; i++)
                {
                    BuildingType t = rules.Buildings[i];
                    if (t.Faction == faction && t.Tier == tier && t.Buildable && t.Category == category && !Has(options, i))
                        yield return Locked(category, i, t.Cost);
                }

                break;
        }
    }

    private static BuildOption Locked(ProductionCategory category, int index, int cost) =>
        new(category, index, false, QueueBlockReason.NotBuildable, cost);

    private static bool Has(IReadOnlyList<BuildOption> options, int typeIndex)
    {
        foreach (BuildOption o in options)
            if (o.TypeIndex == typeIndex) return true;
        return false;
    }

    private static int Clamp(int tier) => Math.Clamp(tier, 1, MaxTier);
}
