using Rts.Sim.Core;

namespace Rts.Data;

/// <summary>
/// Parsed rules: game constants, unit types sorted by id (ordinal), and the canonical rules hash.
/// </summary>
public sealed record RuleSet(GameRules Game, IReadOnlyList<UnitTypeDef> Units, ulong RulesHash);
