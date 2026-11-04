// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.AI.Recruit;

/// <summary>
/// M5-02 (docs/design/09_m5_plan.md §4): a commander's <see cref="Directive"/> turned into the knob values the tiered AI's managers
/// already have. A pure function of the directive: rebuilt once per Think by <see cref="RecruitAi"/>, never stored, so nothing here is
/// saved. Integer math only (permille = 1000 means "as today"). <see cref="Directive.None"/> (and any directive that is not set)
/// gives <see cref="Plain"/>, the exact values the managers used before directives existed, so a game without one is unchanged.
/// </summary>
internal readonly record struct DirectiveBias
{
    /// <summary>True when the commander has given a directive; the managers change nothing when false.</summary>
    public bool Set { get; init; }
    /// <summary>Multiplier (permille) on the wave army-size threshold, the timed-wave interval and the smallest timed wave.</summary>
    public int WavePermille { get; init; }
    /// <summary>Units of the army that stay at home when a wave launches (the wave also waits for them).</summary>
    public int HomeGuard { get; init; }
    /// <summary>Multiplier (permille) on the profile's retreat health percent (0 = never retreat; a profile that never retreats stays so).</summary>
    public int RetreatPermille { get; init; }
    /// <summary>The player the waves are sent to, or -1 for the AI's own choice (honoured only when the view knows a building of it).</summary>
    public int TargetPlayer { get; init; }
    /// <summary>The unit-bag weighting (<see cref="DirectiveCodec.MixAuto"/> etc.).</summary>
    public byte Mix { get; init; }
    /// <summary>Chance in permille that a build in a lane takes a unit of the preferred <see cref="Mix"/> when one can be built (0 = bag only).</summary>
    public int MixPermille { get; init; }
    /// <summary>Credits units leave in hand for structures and research (spend priority: economy and tech come before units).</summary>
    public int UnitReserve { get; init; }
    /// <summary>Army size before research is bought.</summary>
    public int UpgradeMinArmy { get; init; }
    /// <summary>Credits left in hand after paying for an upgrade.</summary>
    public int UpgradeCushion { get; init; }
    /// <summary>Multiplier (permille) on the ticks at which the tier gates are added to the build order.</summary>
    public int GatePermille { get; init; }
    /// <summary>Multiplier (permille) on the army size that brings the tier-3 gate forward.</summary>
    public int Tier3ArmyPermille { get; init; }
    /// <summary>The gates are built even by a profile without research (tech focus).</summary>
    public bool ForceTech { get; init; }
    /// <summary>The air tech / superweapon do not wait for an army of <see cref="TierPlanner.ArmyBeforeLateTech"/> units.</summary>
    public bool EarlyLateTech { get; init; }
    /// <summary>Aircraft kept flying or queued at once.</summary>
    public int AircraftCap { get; init; }
    /// <summary>Build the second refinery and want the extra harvester at once; garrison neutral ruins further out.</summary>
    public bool Expand { get; init; }
    /// <summary>Infantry kept out of garrisons (garrisons fill with the surplus).</summary>
    public int GarrisonKeep { get; init; }
    /// <summary>Cells from the yard a garrisonable building may be to be filled.</summary>
    public int GarrisonRadius { get; init; }
    /// <summary>Where waves go first (aggressive / all-in with a focus cell); <see cref="Directive.NoCell"/> = no attack point.</summary>
    public CellCoord AttackCell { get; init; }
    /// <summary>The rally / defend point (turtle with a focus cell); <see cref="Directive.NoCell"/> = the AI's own rally cell.</summary>
    public CellCoord RallyCell { get; init; }

    /// <summary>True when <see cref="AttackCell"/> names a cell.</summary>
    public bool HasAttackCell => AttackCell.X >= 0;
    /// <summary>True when <see cref="RallyCell"/> names a cell.</summary>
    public bool HasRallyCell => RallyCell.X >= 0;
    /// <summary>True when the preferred mix is anti-air.</summary>
    public bool WantsAntiAir => Mix == DirectiveCodec.MixAntiAir && MixPermille > 0;

    /// <summary>The values the managers use with no directive.</summary>
    public static DirectiveBias Plain { get; } = new()
    {
        Set = false, WavePermille = 1000, HomeGuard = 0, RetreatPermille = 1000, TargetPlayer = -1, Mix = DirectiveCodec.MixAuto, MixPermille = 0,
        UnitReserve = 0, UpgradeMinArmy = UpgradeManager.MinArmy, UpgradeCushion = UpgradeManager.Cushion, GatePermille = 1000,
        Tier3ArmyPermille = 1000, ForceTech = false, EarlyLateTech = false, AircraftCap = TierPlanner.MaxAircraft, Expand = false,
        GarrisonKeep = GarrisonManager.KeepInArmy, GarrisonRadius = GarrisonManager.RadiusCells,
        AttackCell = Directive.NoCell, RallyCell = Directive.NoCell,
    };

    /// <summary>Chance (permille) that a lane takes a unit of the preferred mix.</summary>
    public const int PreferredMixPermille = 750;   // (tune) M5-02

    /// <summary>The knob values for a directive. <see cref="Directive.None"/> (not set) returns <see cref="Plain"/>.</summary>
    public static DirectiveBias For(Directive d)
    {
        if (!d.Set) return Plain;
        DirectiveBias b = Plain with { Set = true };
        switch (d.Posture)
        {
            case DirectiveCodec.PostureTurtle:
                b = b with { WavePermille = 1600, HomeGuard = 4, RetreatPermille = 1500, GarrisonKeep = 4, RallyCell = d.FocusCell };
                break;
            case DirectiveCodec.PostureAggressive:
                b = b with { WavePermille = 700, RetreatPermille = 600, GarrisonKeep = 24, AttackCell = d.FocusCell };
                break;
            case DirectiveCodec.PostureAllIn:
                b = b with { WavePermille = 450, RetreatPermille = 0, GarrisonKeep = 1000, AttackCell = d.FocusCell };
                break;
        }
        switch (d.Focus)
        {
            case DirectiveCodec.FocusEconomy:
                b = b with { UnitReserve = 300, Expand = true, UpgradeMinArmy = 18 };
                break;
            case DirectiveCodec.FocusTech:
                b = b with { UnitReserve = 200, GatePermille = 300, Tier3ArmyPermille = 500, ForceTech = true, UpgradeMinArmy = 6, UpgradeCushion = 150 };
                break;
            case DirectiveCodec.FocusAir:
                b = b with { UnitReserve = 150, GatePermille = 600, EarlyLateTech = true, AircraftCap = TierPlanner.MaxAircraft + 2 };
                break;
            case DirectiveCodec.FocusSuperweapon:
                b = b with { UnitReserve = 200, GatePermille = 400, Tier3ArmyPermille = 500, ForceTech = true, EarlyLateTech = true };
                break;
        }
        if (d.Expand) b = b with { Expand = true, GarrisonRadius = GarrisonManager.RadiusCells + 8 };
        if (d.TargetPlayer != DirectiveCodec.AnyTarget) b = b with { TargetPlayer = d.TargetPlayer };
        if (d.Mix != DirectiveCodec.MixAuto && d.Mix <= DirectiveCodec.MixAntiStructure) b = b with { Mix = d.Mix, MixPermille = PreferredMixPermille };
        return b;
    }

    /// <summary>The profile's retreat percent scaled by <see cref="RetreatPermille"/> (never above 100; 0 stays 0).</summary>
    public int RetreatPct(int profilePct) => Math.Clamp((int)((long)profilePct * RetreatPermille / 1000), 0, 100);

    /// <summary>A wave army-size threshold or interval scaled by <see cref="WavePermille"/> and never below <paramref name="floor"/>.</summary>
    public int Wave(int value, int floor) => WavePermille == 1000 ? value : Math.Max(floor, (int)((long)value * WavePermille / 1000));
}
