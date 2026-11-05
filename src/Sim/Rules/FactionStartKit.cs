// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>
/// A faction's skirmish start (game.toml <c>[faction.authority]</c> / <c>[faction.directorate]</c>): the construction-yard
/// building id placed at the start position and the ids of extra start units (vehicle or infantry ids). Ids are kept as
/// strings and validated lazily (<see cref="Rts.Sim.Rules.SimRules.ResolveStartKit"/>), so a kit may name types that a rules
/// set does not define yet as long as no player of that faction is set up.
/// </summary>
public sealed record FactionStartKit(string Conyard, IReadOnlyList<string> Units)
{
    /// <summary>M4: the provides name a type of tier 2 needs (game.toml <c>tier2</c>), or "" for none. The loader appends it to
    /// the prerequisites of every tier-2 type of the faction (tier sugar, design 08 §4).</summary>
    public string Tier2Provides { get; init; } = "";

    /// <summary>M4: the provides name a type of tier 3 needs (game.toml <c>tier3</c>), or "" for none.</summary>
    public string Tier3Provides { get; init; } = "";

    /// <summary>The pre-M3 skirmish start: one auth_conyard, no units.</summary>
    public static FactionStartKit AuthorityDefault { get; } = new("auth_conyard", Array.Empty<string>());

    /// <summary>The Directorate start (dir_shaft); its types arrive with M3-07.</summary>
    public static FactionStartKit DirectorateDefault { get; } = new("dir_shaft", Array.Empty<string>());

    /// <summary>Placeholder for <see cref="Faction.Neutral"/> (index 2): neutral has no start kit and never resolves.</summary>
    public static FactionStartKit NeutralNone { get; } = new("", Array.Empty<string>());

    /// <summary>Placeholder for <see cref="Faction.Expedition"/> (index 4, M7-02): no kit until M7-08 supplies the real one.</summary>
    public static FactionStartKit ExpeditionNone { get; } = new("", Array.Empty<string>());

    /// <summary>The Left Behind start (M6-08): no conyard building, a packed Mother Rig vehicle, two Scavengers and a Gleaner.</summary>
    public static FactionStartKit LeftBehindDefault { get; } =
        new("", new[] { "lb_mother_rig_packed", "lb_scavenger", "lb_scavenger", "lb_gleaner" });
}
