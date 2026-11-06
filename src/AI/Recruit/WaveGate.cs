// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.AI.Recruit;

/// <summary>
/// M4-16 round 4: the wave strength gate and the escalation after a failed wave (tier 2+ with respond_to_attacks). Pure decisions, no state:
/// the state (<c>_required</c>, <c>_launchValue</c>) lives in <see cref="MilitaryManager"/> and is saved with it.
/// </summary>
internal static class WaveGate
{
    /// <summary>A timed or threshold wave launches only when our army value is at least this percent of the biggest enemy army seen at once lately.</summary>
    public const int GatePct = 100;   // (tune) M4-16 r4

    /// <summary>The gate is lifted once this many units are at home (the army is big enough to go regardless).</summary>
    public const int GateCapUnits = 24;   // (tune) M4-16 r4

    /// <summary>The gate is lifted this many ticks after the last wave (no waiting for ever).</summary>
    public const int GateWaitTicks = 6000;   // (tune) M4-16 r4

    /// <summary>After a failed wave the next one waits for this percent of the failed wave's launch value.</summary>
    public const int EscalatePct = 200;   // (tune) M4-16 r4

    /// <summary>Upper bound of the escalated army value, in credits.</summary>
    public const int EscalateCap = 12000;   // (tune) M4-16 r4

    /// <summary>True when a wave may leave now. Always true without the gate (tier 1: <paramref name="respond"/> false).</summary>
    public static bool Strong(bool respond, int ownValue, int enemySeen, int required, int units, int tick, int lastWave, int ratioPct = 0)
    {
        if (!respond) return true;
        if (units >= GateCapUnits || tick - lastWave >= GateWaitTicks) return true;
        bool notOutgunned = enemySeen <= 0 || (long)ownValue * 100 >= (long)enemySeen * (ratioPct > 0 ? ratioPct : GatePct);
        return notOutgunned && ownValue >= required;
    }

    /// <summary>M4-16 s12: default percent for the predicted-loss retreat: the wave turns back when its local value is below this percent of the visible enemy value near it.</summary>
    public const int RetreatRatioDefaultPct = 70;   // (tune) s12

    /// <summary>M4-16 s12: the local fight is only judged when the visible enemy value near the wave is at least this many credits (no retreat from a skirmish with a scout).</summary>
    public const int RetreatMinEnemy = 600;   // (tune) s12

    /// <summary>M4-16 s12: cells around the wave centre that count as "the local fight".</summary>
    public const int LocalRadiusCells = 9;   // (tune) s12

    /// <summary>True when the local fight is clearly lost: <paramref name="own"/> credits against <paramref name="enemy"/> credits near the wave. Integer maths.</summary>
    public static bool Losing(int own, int enemy, int ratioPct)
    {
        if (enemy < RetreatMinEnemy) return false;
        return (long)own * 100 < (long)enemy * (ratioPct > 0 ? ratioPct : RetreatRatioDefaultPct);
    }

    /// <summary>The army value the next wave waits for after a failed wave launched with <paramref name="launchValue"/>.</summary>
    public static int Escalate(bool respond, int required, int launchValue)
    {
        if (!respond) return required;
        return Math.Min(EscalateCap, Math.Max(required, (int)((long)launchValue * EscalatePct / 100)));
    }
}
