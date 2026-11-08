// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// S15 (Q-64, D-174): the pure decisions of siege mode. No state: the memory (current target, counters) lives in
/// <see cref="MilitaryManager"/> and is saved with it. Everything is integer maths over the view the AI already has (known enemy
/// buildings, live or ghost), so it is fog-honest and deterministic.
/// </summary>
internal static class SiegePlan
{
    /// <summary>Known enemy defence value (credits) from which a base counts as defended and waves go in siege mode.</summary>
    public const int MinDefenceValue = 800;   // (tune) s15

    /// <summary>Credits a bunker-type structure adds per garrison slot (the infantry inside shoots from behind its walls).</summary>
    public const int GarrisonSlotValue = 60;   // (tune) s15

    /// <summary>Percent of the known defence value the army must be worth before a siege wave leaves, by profile tier (index = tier, 0 and 1 = no siege).</summary>
    private static readonly int[] GatherPct = { 0, 0, 60, 80, 100, 120 };   // (tune) s15

    /// <summary>Upper bound of the army value a siege wave waits for.</summary>
    public const int NeedCap = 7000;   // (tune) s15

    /// <summary>A siege wave waits no longer than this since the last wave (the timed trigger of the normal rules takes over).</summary>
    public const int WaitTicks = 9000;   // (tune) s15

    /// <summary>Fewest army units a siege wave takes, by profile tier (index = tier), plus <see cref="UnitsPerFailure"/> per wave already lost against this enemy (at most <see cref="MaxFailureSteps"/>).</summary>
    private static readonly int[] MinUnitsByTier = { 0, 0, 12, 14, 16, 18 };   // (tune) s15
    public const int UnitsPerFailure = 2, MaxFailureSteps = 4;   // (tune) s15

    /// <summary>Army size at which the wait ends whatever the value.</summary>
    public const int CapUnits = 40;   // (tune) s15

    /// <summary>Weapon range (cells) from which a unit counts as artillery: it fires from behind the line.</summary>
    public const int ArtilleryCells = 7;   // (tune) s15

    /// <summary>Artillery in the wave before the direct-fire units hold the line at firing distance instead of charging the defence.</summary>
    public const int MinArtillery = 3;   // (tune) s15

    /// <summary>Percent of the wave that must stand at the staging cell before a siege wave advances.</summary>
    public const int ReadyPct = 85;   // (tune) s15

    /// <summary>Priority bonus (in cells x 4) of a production / root / income structure that no known defence covers.</summary>
    public const int ReachBonus = 28;   // (tune) s15

    /// <summary>Whether a profile tier sieges at all.</summary>
    public static bool Enabled(int tier) => tier >= 2 && tier < GatherPct.Length;

    /// <summary>Percent of the defence value the siege army must reach (0 = no siege) for <paramref name="tier"/>.</summary>
    public static int Pct(int tier) => tier < 2 ? 0 : GatherPct[Math.Min(tier, GatherPct.Length - 1)];

    /// <summary>True when <paramref name="w"/> can hit ground targets.</summary>
    public static bool HitsGround(WeaponType w) => w.Targets is WeaponTargets.Ground or WeaponTargets.Both;

    /// <summary>True for a building that shoots at ground units (or holds a garrison): a defence the army has to deal with.</summary>
    public static bool IsDefence(SimRules rules, BuildingType bt) =>
        !bt.Wall && !bt.Gate && ((bt.PrimaryWeapon >= 0 && HitsGround(rules.Weapons[bt.PrimaryWeapon])) || bt.Garrison > 0);

    /// <summary>Range of a building's gun in whole cells (garrison bunkers reach a cell further), 0 when it has none.</summary>
    public static int RangeCells(SimRules rules, BuildingType bt)
    {
        int r = bt.PrimaryWeapon >= 0 ? (rules.Weapons[bt.PrimaryWeapon].Range + 255) / 256 : 0;
        if (bt.Garrison > 0) r = Math.Max(r, 3) + 1;
        return r;
    }

    /// <summary>Weapon range (leptons) of a mobile unit, 0 when unarmed.</summary>
    public static int UnitRange(SimRules rules, ViewActor a)
    {
        int w = a.Kind == ActorKind.Vehicle ? rules.Vehicles[a.TypeIndex].PrimaryWeapon : a.Kind == ActorKind.Infantry ? rules.Infantry[a.TypeIndex].PrimaryWeapon : -1;
        return w < 0 ? 0 : rules.Weapons[w].Range;
    }

    /// <summary>True for a ground unit that fires from far away (range of <see cref="ArtilleryCells"/> or more, ground-capable).</summary>
    public static bool IsArtillery(SimRules rules, ViewActor a)
    {
        if (a.Kind == ActorKind.Vehicle && rules.Vehicles[a.TypeIndex].Locomotion == Locomotion.Air) return false;
        int w = a.Kind == ActorKind.Vehicle ? rules.Vehicles[a.TypeIndex].PrimaryWeapon : a.Kind == ActorKind.Infantry ? rules.Infantry[a.TypeIndex].PrimaryWeapon : -1;
        return w >= 0 && HitsGround(rules.Weapons[w]) && rules.Weapons[w].Range >= ArtilleryCells * 256;
    }

    /// <summary>Credits of defence in <paramref name="known"/> (armed ground structures and garrison bunkers), and the longest range among them in cells.</summary>
    public static int DefenceValue(SimRules rules, List<ViewActor> known, out int maxRangeCells)
    {
        int value = 0; maxRangeCells = 0;
        foreach (ViewActor b in known)
        {
            BuildingType bt = rules.Buildings[b.TypeIndex];
            if (!IsDefence(rules, bt)) continue;
            value += bt.Cost + bt.Garrison * GarrisonSlotValue;
            maxRangeCells = Math.Max(maxRangeCells, RangeCells(rules, bt));
        }
        return value;
    }

    /// <summary>Fewest units a siege wave takes at <paramref name="tier"/> after <paramref name="failedWaves"/> lost waves.</summary>
    public static int MinUnits(int tier, int failedWaves) =>
        tier < 2 ? 0 : MinUnitsByTier[Math.Min(tier, MinUnitsByTier.Length - 1)] + UnitsPerFailure * Math.Min(failedWaves, MaxFailureSteps);

    /// <summary>The army value a siege wave waits for: the defence value times the tier's percent, capped.</summary>
    public static int Need(int tier, int defenceValue) =>
        (int)Math.Min(NeedCap, (long)defenceValue * Pct(tier) / 100);

    /// <summary>True for the structures whose loss stops an enemy rebuilding: producers, refineries and tech roots (no prerequisites).</summary>
    public static bool IsVital(BuildingType bt) =>
        !bt.Wall && !bt.Gate && (bt.Produces != null || bt.Refinery || bt.Prerequisites.Count == 0);

    private static int Cheb(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>Known defences that can shoot at <paramref name="cell"/> (the cell lies within their range plus one).</summary>
    public static int Cover(SimRules rules, List<ViewActor> known, CellCoord cell)
    {
        int n = 0;
        foreach (ViewActor d in known)
        {
            BuildingType bt = rules.Buildings[d.TypeIndex];
            if (!IsDefence(rules, bt)) continue;
            if (Cheb(d.Cell, cell) <= RangeCells(rules, bt) + 1) n++;
        }
        return n;
    }

    /// <summary>
    /// The next building the siege wave attacks, or null when none is known. Score (lower first): distance from the army in cells x 4, plus
    /// 0 for a defence, +14 for anything else, but -<see cref="ReachBonus"/> for a vital structure that no known defence covers (it is in
    /// reach: kill it now) and +8 for a covered vital structure (the defences around it go first). <paramref name="skip"/> says which cells
    /// to leave out (goals a stalled wave could not hurt).
    /// </summary>
    public static ViewActor? PickTarget(SimRules rules, List<ViewActor> known, CellCoord from, Func<CellCoord, bool> skip)
    {
        long best = long.MaxValue;
        ViewActor? pick = null;
        foreach (ViewActor b in known)
        {
            BuildingType bt = rules.Buildings[b.TypeIndex];
            if (bt.Wall || bt.Gate) continue;
            if (skip(b.Cell)) continue;
            long score = Cheb(from, b.Cell) * 4L;
            if (IsDefence(rules, bt)) { /* outside-in: nearest defence first */ }
            else if (IsVital(bt)) score += Cover(rules, known, b.Cell) == 0 ? -ReachBonus : 8;
            else score += 14;
            if (score >= best) continue;
            best = score; pick = b;
        }
        return pick;
    }
}
