// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// S22 (M13-20): the pure decisions of the mop-up, the part of <see cref="MilitaryManager"/> that finishes a beaten opponent so skirmish games
/// stop ending as draws. No state: the memory (hold timer, threat timer, fog stamps) lives in the manager and is saved with it. Integer maths only.
/// </summary>
internal static class MopupPlan
{
    /// <summary>The mop-up never starts before this tick (before first contact "no enemy army seen" is true too).</summary>
    public const int FromTick = 6000;   // (tune) s22; s23: 9000 -> 6000 (s22mopup: the trigger almost never fired)

    /// <summary>Our army must be worth at least this many credits (about eight real units).</summary>
    public const int MinValue = 3000;   // (tune) s22

    /// <summary>Fewest army units a mop-up wave takes (plus the home guard).</summary>
    public const int MinArmy = 8;   // (tune) s22

    /// <summary>s23: fewest army units for a mop-up when no enemy army was seen in the long window (s22mopup: exhausted endgames, one side with
    /// no army and one building, the other with 4-5 units, stayed draws because of <see cref="MinArmy"/> and <see cref="MinValue"/>).</summary>
    public const int MinArmyNoEnemy = 4;   // (tune) s23

    /// <summary>Percent of the enemy army value (biggest seen in the long window) our army must be worth for the enemy to count as beaten.</summary>
    public const int RatioPct = 250;   // (tune) s22; s23: 400 -> 250

    /// <summary>Ticks the "beaten" test must hold without a break before a mop-up wave leaves (on top of the long enemy-value window).</summary>
    public const int HoldTicks = 600;   // (tune) s22; s23: 1200 -> 600

    /// <summary>Ticks without a threat at home (own building hit, armed enemy unit near the base) before a mop-up wave leaves.</summary>
    public const int CalmTicks = 900;   // (tune) s22

    /// <summary>Cells (Chebyshev) around our start that count as "at home" for the threat test.</summary>
    public const int HomeThreatCells = 20;   // (tune) s22

    /// <summary>Units that always stay at home during a mop-up wave.</summary>
    public const int KeepHome = 2;   // (tune) s22

    /// <summary>Least ticks between two mop-up waves.</summary>
    public const int GapTicks = 1800;   // (tune) s22

    /// <summary>Units per sweep party: a big army searching an empty map splits into parties of about this size, each with its own block.</summary>
    public const int PartySize = 6;   // (tune) s22

    /// <summary>Most sweep parties at once.</summary>
    public const int MaxParties = 8;   // (tune) s22

    /// <summary>Least distance (cells) between the goals of two sweep parties.</summary>
    public const int PartySpacingCells = 16;   // (tune) s22

    /// <summary>Edge (cells) of a block of the fog memory / sweep grid.</summary>
    public const int BlockCells = 8;   // (tune) s22 (s14: the same 8-cell grid)

    /// <summary>A block not in view for this many ticks counts as stale and is swept again.</summary>
    public const int RevisitTicks = 6000;   // (tune) s22

    /// <summary>s30 (<c>mopup_hunt</c>): a block not in view for this many ticks counts as stale (a relocating base can be back in a block long before <see cref="RevisitTicks"/>).</summary>
    public const int HuntRevisitTicks = 3000;   // (tune) s30

    /// <summary>s30 (<c>mopup_hunt</c>): units per sweep party (smaller parties = more blocks looked at in parallel).</summary>
    public const int HuntPartySize = 3;   // (tune) s30

    /// <summary>s30 (<c>mopup_hunt</c>): score a stale block gets knocked off when an enemy structure was seen in it before (4 per cell: about 50 cells of walking).</summary>
    public const int SiteBonus = 200;   // (tune) s30

    /// <summary>s30 (<c>mopup_hunt</c>): score knocked off a stale block within <see cref="RecentCells"/> of where a deployable enemy vehicle was last seen.</summary>
    public const int RecentBonus = 240;   // (tune) s30

    /// <summary>s30 (<c>mopup_hunt</c>): cells (Chebyshev) around the last-seen cell of a deployable enemy vehicle that get <see cref="RecentBonus"/>.</summary>
    public const int RecentCells = 16;   // (tune) s30

    /// <summary>s30 (<c>mopup_hunt</c>): ticks after the last sighting of a deployable enemy vehicle during which its last-seen cell is the goal and its surroundings are favoured.</summary>
    public const int ChaseTicks = 1200;   // (tune) s30

    /// <summary>s23 D-235 (B1): fewest army units a mop-up wave takes (before the home guard): the same minimum <see cref="Beaten"/> uses.</summary>
    public static int MinUnits(int enemyValueSeen) => enemyValueSeen <= 0 ? MinArmyNoEnemy : MinArmy;

    /// <summary>True when the enemy counts as beaten right now: our army is a real force (value and units) and worth at least <see cref="RatioPct"/>
    /// percent of the biggest enemy army seen lately (0 seen = nothing left to fight with). Integer maths.</summary>
    public static bool Beaten(int ownValue, int enemyValueSeen, int armyUnits) =>
        enemyValueSeen <= 0 ? armyUnits >= MinArmyNoEnemy   // s23: an enemy with no army left is beaten by whatever we still have
            : armyUnits >= MinArmy && ownValue >= MinValue && (long)ownValue * 100 >= (long)enemyValueSeen * RatioPct;

    /// <summary>True when a block last in view at <paramref name="stamp"/> (tick + 1, 0 = never) is stale at <paramref name="tick"/>.</summary>
    public static bool Stale(int stamp, int tick) => stamp == 0 || tick + 1 - stamp > RevisitTicks;

    /// <summary>s30: <see cref="Stale(int,int)"/> with the revisit time of the mode (<see cref="RevisitTicks"/>, or <see cref="HuntRevisitTicks"/> when hunting).</summary>
    public static bool Stale(int stamp, int tick, bool hunt) => stamp == 0 || tick + 1 - stamp > (hunt ? HuntRevisitTicks : RevisitTicks);

    /// <summary>s30 (<c>mopup_hunt</c>): what the hunt remembers. <see cref="Sites"/> = per block, non-zero when an enemy structure was seen in it (same grid as the stamps);
    /// <see cref="Recent"/> = the cell where a deployable enemy vehicle was last seen, when that was within <see cref="ChaseTicks"/>.</summary>
    public readonly record struct HuntHints(byte[] Sites, CellCoord? Recent);

    /// <summary>
    /// The next sweep goal: the passable block centre that is stale, lowest score first. Score = distance from <paramref name="from"/> in cells x 4,
    /// +24 for a block that was seen before (never-seen shroud first), +40 when a known enemy defence covers it (outside-in: the defended core last).
    /// Null when every block is fresh or none is reachable.
    /// </summary>
    public static CellCoord? PickSweep(SimRules rules, int[] stamp, int blocksX, int mapW, int mapH, int tick, CellCoord from,
        List<ViewActor> knownBuildings, Func<CellCoord, bool> passable, Func<CellCoord, bool> skip, HuntHints? hunt = null)
    {
        if (blocksX <= 0) return null;
        long best = long.MaxValue;
        CellCoord? goal = null;
        for (int i = 0; i < stamp.Length; i++)
        {
            if (!Stale(stamp[i], tick, hunt is not null)) continue;
            var c = new CellCoord(i % blocksX * BlockCells + BlockCells / 2, i / blocksX * BlockCells + BlockCells / 2);
            if (c.X >= mapW || c.Y >= mapH || !passable(c) || skip(c)) continue;
            long score = Math.Max(Math.Abs(c.X - from.X), Math.Abs(c.Y - from.Y)) * 4L;
            if (stamp[i] != 0) score += 24;
            if (SiegePlan.Cover(rules, knownBuildings, c) > 0) score += 40;
            if (hunt is HuntHints h)   // s30: a block where an enemy structure stood, and the surroundings of a rig that was just seen, first
            {
                if (i < h.Sites.Length && h.Sites[i] != 0) score -= SiteBonus;
                if (h.Recent is CellCoord rc && Math.Max(Math.Abs(c.X - rc.X), Math.Abs(c.Y - rc.Y)) <= RecentCells) score -= RecentBonus;
            }
            if (score >= best) continue;
            best = score; goal = c;
        }
        return goal;
    }

    /// <summary>Number of sweep parties for <paramref name="units"/> wave members: one party per <see cref="PartySize"/> units, at most <see cref="MaxParties"/>, at least 1.</summary>
    public static int Parties(int units) => Math.Clamp(units / PartySize, 1, MaxParties);

    /// <summary>s30: <see cref="Parties(int)"/> with the party size of the mode (<see cref="HuntPartySize"/> when hunting).</summary>
    public static int Parties(int units, bool hunt) => hunt ? Math.Clamp(units / HuntPartySize, 1, MaxParties) : Parties(units);

    /// <summary>
    /// Up to <paramref name="count"/> sweep goals at least <see cref="PartySpacingCells"/> apart, best first (the first is what <see cref="PickSweep"/> returns).
    /// Fewer when the stale blocks run out.
    /// </summary>
    public static List<CellCoord> PickSweeps(SimRules rules, int[] stamp, int blocksX, int mapW, int mapH, int tick, CellCoord from,
        List<ViewActor> knownBuildings, Func<CellCoord, bool> passable, Func<CellCoord, bool> skip, int count, HuntHints? hunt = null)
    {
        var goals = new List<CellCoord>();
        bool Apart(CellCoord c)
        {
            foreach (CellCoord g in goals)
                if (Math.Max(Math.Abs(g.X - c.X), Math.Abs(g.Y - c.Y)) < PartySpacingCells) return false;
            return true;
        }
        for (int i = 0; i < count; i++)
        {
            CellCoord? g = PickSweep(rules, stamp, blocksX, mapW, mapH, tick, from, knownBuildings, passable, c => skip(c) || !Apart(c), hunt);
            if (g is not CellCoord found) break;
            goals.Add(found);
        }
        return goals;
    }
}
