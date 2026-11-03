using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim;

// Player relations and per-player rule views (M1.5, D-054). Owned by package M1.5-01.
public sealed partial class Simulation
{
    /// <summary>
    /// How <paramref name="a"/> regards <paramref name="b"/>: Self if equal; Neutral if either slot is neutral;
    /// Ally if both share a team; otherwise Enemy. CONTRACT STUB: M1.5-01 implements it.
    /// </summary>
    public Relation RelationOf(byte a, byte b) => a == b ? Relation.Self : Relation.Enemy;

    /// <summary>
    /// The type tables as <paramref name="player"/> sees them. Every sim type lookup goes through this so
    /// upgrades (design 02 §5) can later give each player its own modified copy. Today: the shared rules.
    /// </summary>
    public SimRules RulesOf(byte player) => Setup.Rules;
}
