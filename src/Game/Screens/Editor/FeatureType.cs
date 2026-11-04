// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens.Editor;

/// <summary>A neutral map feature the editor can place: a building id (<c>neu_*</c>) and its footprint in cells.</summary>
/// <param name="Id">Building type id.</param>
/// <param name="Width">Footprint width in cells.</param>
/// <param name="Height">Footprint height in cells.</param>
public sealed record FeatureType(string Id, int Width, int Height)
{
    /// <summary>The ruins from data/rules/buildings.toml, used when the rules cannot be read (tests, headless).</summary>
    public static IReadOnlyList<FeatureType> Defaults { get; } =
        new[] { new FeatureType("neu_ruin_depot", 2, 2), new FeatureType("neu_ruin_tenement", 3, 2) };

    /// <summary>Every Faction.Neutral building of the rules (the <c>neu_*</c> ruins) in id order; <see cref="Defaults"/> when the rules cannot be read or have none.</summary>
    public static IReadOnlyList<FeatureType> FromRules(string rulesDir)
    {
        try
        {
            var rules = Rts.Data.RulesLoader.Load(rulesDir);
            var list = rules.Sim.Buildings
                .Where(b => b.Faction == Rts.Sim.Rules.Faction.Neutral)   // Q-37: the real faction, not the id prefix
                .OrderBy(b => b.Id, StringComparer.Ordinal)
                .Select(b => new FeatureType(b.Id, b.FootprintW, b.FootprintH))
                .ToArray();
            return list.Length > 0 ? list : Defaults;
        }
        catch (Exception e) when (e is Rts.Data.RulesException or IOException)
        {
            return Defaults;
        }
    }
}
