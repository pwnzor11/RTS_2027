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
    /// <summary>The pre-M3 skirmish start: one auth_conyard, no units.</summary>
    public static FactionStartKit AuthorityDefault { get; } = new("auth_conyard", Array.Empty<string>());

    /// <summary>The Directorate start (dir_shaft); its types arrive with M3-07.</summary>
    public static FactionStartKit DirectorateDefault { get; } = new("dir_shaft", Array.Empty<string>());
}
