// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui.Alerts;

/// <summary>How loud an alert looks: the colour of its stripe.</summary>
public enum AlertTone
{
    /// <summary>Red: something is being hit or was lost.</summary>
    Danger,
    /// <summary>Amber: needs attention (power).</summary>
    Warning,
    /// <summary>Green: good news.</summary>
    Good,
}

/// <summary>The fixed facts about each <see cref="AlertKind"/>: text, tone, merge and rate-limit rules. Times are sim ticks.</summary>
public static class AlertInfo
{
    /// <summary>Sim ticks per second (the sim runs 20).</summary>
    public const int TicksPerSecond = Rts.Sim.Core.SimConstants.TicksPerSecond;

    /// <summary>The line shown for <paramref name="kind"/>.</summary>
    public static string Text(AlertKind kind) => kind switch
    {
        AlertKind.BaseUnderAttack => "Base under attack",
        AlertKind.UnitUnderAttack => "Unit under attack",
        AlertKind.HarvesterUnderAttack => "Harvester under attack",
        AlertKind.ConstructionComplete => "Construction complete",
        AlertKind.UnitReady => "Unit ready",
        AlertKind.LowPower => "Low power",
        AlertKind.BuildingLost => "Building lost",
        AlertKind.UnitLost => "Unit lost",
        AlertKind.SuperweaponReady => "Superweapon ready",
        AlertKind.SuperweaponLaunched => "Superweapon launched",
        _ => kind.ToString(),
    };

    /// <summary>The stripe colour class.</summary>
    public static AlertTone Tone(AlertKind kind) => kind switch
    {
        AlertKind.BaseUnderAttack or AlertKind.UnitUnderAttack or AlertKind.HarvesterUnderAttack or AlertKind.BuildingLost or AlertKind.UnitLost => AlertTone.Danger,
        AlertKind.LowPower => AlertTone.Warning,
        _ => AlertTone.Good,
    };

    /// <summary>True for the alerts whose minimap ping is red (the attack and loss alerts).</summary>
    public static bool IsDanger(AlertKind kind) => Tone(kind) == AlertTone.Danger;

    /// <summary>True when the same alert in different places counts as different (attacks, losses, superweapons); false when one line serves the whole map.</summary>
    public static bool PerArea(AlertKind kind) => kind switch
    {
        AlertKind.ConstructionComplete or AlertKind.UnitReady or AlertKind.LowPower => false,
        _ => true,
    };

    /// <summary>Whether the last-alert key can jump to this kind (it needs a place; routine "ready" alerts are left out so they cannot bury an attack).</summary>
    public static bool InJumpList(AlertKind kind) => kind switch
    {
        AlertKind.ConstructionComplete or AlertKind.UnitReady or AlertKind.LowPower => false,
        _ => true,
    };

    /// <summary>Whether a merged line shows "x3" (not for a state like low power).</summary>
    public static bool ShowsCount(AlertKind kind) => kind != AlertKind.LowPower;

    /// <summary>Minimum sim ticks between two counted occurrences of the same alert in the same area (the rate limit).</summary>
    public static int IntervalTicks(AlertKind kind) => kind switch
    {
        AlertKind.BaseUnderAttack or AlertKind.UnitUnderAttack or AlertKind.HarvesterUnderAttack => 10 * TicksPerSecond,
        AlertKind.LowPower => 30 * TicksPerSecond,
        AlertKind.SuperweaponReady or AlertKind.SuperweaponLaunched => 10 * TicksPerSecond,
        _ => 2 * TicksPerSecond,
    };
}
