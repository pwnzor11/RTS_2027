using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim;

// Player relations, team vision and per-player rule views (M1.5, D-054). Owned by package M1.5-01.
public sealed partial class Simulation
{
    /// <summary>
    /// How <paramref name="a"/> regards <paramref name="b"/>: Self if equal; Neutral if either slot is neutral;
    /// Ally if both share a team; otherwise Enemy. Slots outside the player range count as Enemy.
    /// </summary>
    public Relation RelationOf(byte a, byte b)
    {
        if (a == b) return Relation.Self;
        byte[] team = _players.Team;
        if (a >= team.Length || b >= team.Length) return Relation.Enemy;
        byte ta = team[a], tb = team[b];
        if (ta == Teams.Neutral || tb == Teams.Neutral) return Relation.Neutral;
        return ta == tb ? Relation.Ally : Relation.Enemy;
    }

    /// <summary>
    /// The type tables as <paramref name="player"/> sees them. Every sim type lookup goes through this so
    /// upgrades (design 02 §5) can later give each player its own modified copy. Today: the shared rules.
    /// </summary>
    public SimRules RulesOf(byte player) => Setup.Rules;

    /// <summary>True for Self and Ally: the actor is "ours" for movement, shoving and vision.</summary>
    internal bool IsFriendly(byte a, byte b)
    {
        Relation r = RelationOf(a, b);
        return r == Relation.Self || r == Relation.Ally;
    }

    /// <summary>True if <paramref name="player"/> may be ordered to attack something owned by <paramref name="owner"/>
    /// (Enemy or Neutral) or may crush it.</summary>
    internal bool CanAttackOwnedBy(byte player, byte owner)
    {
        Relation r = RelationOf(player, owner);
        return r == Relation.Enemy || r == Relation.Neutral;
    }

    // Team members per player slot, built on first use from the (immutable) team table. A neutral slot is its own team
    // of one, so it shares vision with nobody. Not sim state.
    private byte[][]? _teamMembers;

    internal ReadOnlySpan<byte> TeamMembers(byte player)
    {
        if (_teamMembers == null)
        {
            int n = _players.Count;
            var all = new byte[n][];
            for (int p = 0; p < n; p++)
            {
                int count = 0;
                for (int q = 0; q < n; q++) if (p == q || IsFriendly((byte)p, (byte)q)) count++;
                var list = new byte[count];
                int w = 0;
                for (int q = 0; q < n; q++) if (p == q || IsFriendly((byte)p, (byte)q)) list[w++] = (byte)q;
                all[p] = list;
            }
            _teamMembers = all;
        }
        return player < _teamMembers.Length ? _teamMembers[player] : new[] { player };
    }

    /// <summary>True if the viewer or any ally currently sees the cell (team vision).</summary>
    internal bool IsVisibleTo(byte viewer, CellCoord cell) => _vision.IsVisibleToAny(TeamMembers(viewer), cell);
}
