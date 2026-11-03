using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>
/// Parsed rules: game constants, the sim-side type tables (each sorted by id, ordinal) and the canonical
/// rules hash.
/// </summary>
public sealed record RuleSet(GameRules Game, SimRules Sim, ulong RulesHash);
