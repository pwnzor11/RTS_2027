// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Rules;

namespace Rts.Tools;

/// <summary>S24 (M13-23, D-242): score adjudication for an `ai match` game that reaches the time limit. Each side scores its army value
/// (unit costs alive), its structure value (costs of standing buildings, walls excluded) and its credits (cash + stored). The side with the
/// higher score wins the adjudication when it beats the other score by at least <see cref="MarginPercent"/> %; otherwise the game is
/// even. Measurement only: the game's result stays a draw (D-242), and nothing here changes a rule. Read-only; it never touches the sim.</summary>
internal static class MatchAdjudication
{
    /// <summary>The margin a score must beat the other side's score by (of that other score) to win the adjudication.</summary>
    public const int MarginPercent = 10;

    /// <summary>Adjudicated winner: 0 = side A, 1 = side B, -1 = even.</summary>
    public const int Even = -1;

    /// <summary>
    /// The adjudication of two scores. Equal scores are even. Otherwise the higher score wins when it is at least
    /// <see cref="MarginPercent"/> % above the lower one (higher * 100 &gt;= lower * 110), else the game is even.
    /// </summary>
    public static int Adjudicate(long scoreA, long scoreB)
    {
        if (scoreA == scoreB) return Even;
        long hi = Math.Max(scoreA, scoreB), lo = Math.Min(scoreA, scoreB);
        if (hi * 100 < lo * (100 + MarginPercent)) return Even;
        return scoreA > scoreB ? 0 : 1;
    }

    /// <summary>The adjudicated winner as the output shows it: <c>A</c>, <c>B</c> or <c>even</c>.</summary>
    public static string Label(int adjudicated) => adjudicated == 0 ? "A" : adjudicated == 1 ? "B" : "even";

    /// <summary>One side's score at the end of a game, by component.</summary>
    public readonly record struct Breakdown(long Army, long Structures, long Credits)
    {
        /// <summary>The score the adjudication compares: army + structures + credits.</summary>
        public long Total => Army + Structures + Credits;
    }

    /// <summary>
    /// The score of <paramref name="player"/> in the end state of <paramref name="sim"/>: army value (cost of each unit alive with health),
    /// structure value (cost of each standing building with health, walls excluded) and credits (cash + stored).
    /// </summary>
    public static Breakdown ScoreOf(Simulation sim, byte player, SimRules rules)
    {
        long army = 0, structures = 0;
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player && sim.Units.Health[i] > 0)
                army += rules.Vehicles[sim.Units.TypeIndex[i]].Cost;
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == player && sim.Infantry.Health[i] > 0)
                army += rules.Infantry[sim.Infantry.TypeIndex[i]].Cost;
        for (int i = 1; i < sim.Buildings.Capacity; i++)
        {
            if (!sim.Buildings.Slots.IsIndexAlive(i) || sim.Buildings.Player[i] != player || sim.Buildings.Health[i] <= 0) continue;
            BuildingType type = rules.Buildings[sim.Buildings.TypeIndex[i]];
            if (!type.Wall) structures += type.Cost;
        }
        return new Breakdown(army, structures, sim.Funds(player));
    }
}
