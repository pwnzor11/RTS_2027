// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.AI.Recruit;

/// <summary>
/// s24 (M13-23, D-242): the pure decision of the endgame launch. When a side has no income left (the map is mined out) the army it owns is all it will
/// ever have, so it goes at the enemy at once, without gathering, staging or a home guard, and later units follow instead of waiting for a next wave.
/// Stateless: derived from the economy rescue's <c>Broke</c> flag, the harvester count and the tick, so nothing is saved.
/// </summary>
internal static class EndgameLaunch
{
    /// <summary>From this tick a side with no harvester left counts as out of income without waiting for the stall watchdog.</summary>
    public const int LateTick = 18000;   // (tune) s24

    /// <summary>Fewest army units an endgame wave takes (a lone unit is not a wave).</summary>
    public const int MinArmy = 2;   // (tune) s24

    /// <summary>True when the side is out of income for good: the economy rescue says Broke (no harvester unloaded for a long time), or the tick is past
    /// <see cref="LateTick"/> and no harvester is alive.</summary>
    public static bool Broke(bool rescueBroke, int harvesters, int tick) => rescueBroke || (tick >= LateTick && harvesters <= 0);

    /// <summary>True when an endgame wave leaves now: out of income and at least <see cref="MinArmy"/> units.</summary>
    public static bool Launch(bool rescueBroke, int harvesters, int tick, int armyUnits) =>
        armyUnits >= MinArmy && Broke(rescueBroke, harvesters, tick);

    /// <summary>Units that stay home during an endgame wave: none, unless the base is being attacked (then the defence code handles it first).</summary>
    public static int Guard(bool endgame, int normalGuard) => endgame ? 0 : normalGuard;
}
